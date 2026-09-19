using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using L5R.Core;
using L5R.Server;
using Microsoft.AspNetCore.Builder;

var cases = new (string Name, Func<Task> Test)[]
{
    ("two-seat authorization, version handshake and hidden information", AuthorizationAndPrivacy),
    ("duplicate commands, stale versions and recovery across restart", IdempotencyAndRecovery),
    ("concurrent commands serialize into exactly one accepted change", ConcurrentCommands),
    ("persistence failures leave gameplay unchanged", PersistenceRollback),
    ("reconnect replaces old connection and rejects malformed messages", ReconnectAndMalformedInput),
    ("every command field and every Gold quote cross the wire intact (issue #303)", GoldWire),
    ("a deck search survives a server restart: the journal recovers the shuffle exactly, and each seat sees only what it may (issue #312)", SearchAndShuffleSurviveRestart),
    ("a look at the opponent's top cards survives a server restart: the viewer's ordered cards reach only the viewer's seat, the arrangement is journaled (issue #322)", LookSurvivesRestart)
};
var failures = 0;
foreach (var (name, test) in cases)
{
    Console.WriteLine($"RUN  {name}");
    try { await test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}\n{exception}"); }
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} server integration checks passed.");
return failures == 0 ? 0 : 1;

static async Task AuthorizationAndPrivacy()
{
    await using var server = await TestServer.StartAsync();
    var host = await server.CreateAsync();
    var guest = await server.JoinAsync(host.RoomCode);
    Check(host.PlayerId == 0 && guest.PlayerId == 1 && host.SeatToken != guest.SeatToken, "Unique seats are issued.");
    var extra = await server.Http.PostAsync($"/rooms/{host.RoomCode}/join", null);
    Check(extra.StatusCode == HttpStatusCode.Conflict, "A third player cannot claim a seat.");
    var unauthorized = false;
    using (var invalid = new ClientWebSocket())
    {
        invalid.Options.SetRequestHeader("Authorization", "Bearer invalid-token");
        try { await invalid.ConnectAsync(server.SocketUri(host.RoomCode), CancellationToken.None); }
        catch (WebSocketException) { unauthorized = true; }
    }
    Check(unauthorized, "Invalid seat token is rejected before upgrade.");

    using (var incompatible = await server.ConnectAsync(host, hello: false))
    {
        await SendAsync(incompatible, new { type = "hello", protocolVersion = 999, contentVersion = Protocol.ContentVersion });
        var error = await ReadTypeAsync(incompatible, "error");
        Check(error.GetProperty("error").GetProperty("code").GetString() == "version_mismatch", "A version mismatch is explicit.");
    }

    using var first = await server.ConnectAsync(host);
    var firstState = await ReadTypeAsync(first, "state");
    using var second = await server.ConnectAsync(guest);
    var secondState = await ReadTypeAsync(second, "state");
    firstState = await ReadTypeAsync(first, "state");
    Check(firstState.GetProperty("playersConnected").EnumerateArray().All(v => v.GetBoolean()), "Presence updates reach both players.");
    VerifyPrivacy(firstState, 0);
    VerifyPrivacy(secondState, 1);
    foreach (var hidden in secondState.GetProperty("view").GetProperty("players")[1].GetProperty("hand").EnumerateArray())
        Check(!firstState.GetRawText().Contains('"' + hidden.GetProperty("id").GetString() + '"', StringComparison.Ordinal),
            "Opponent hand identities do not appear in snapshots, events, or action queries.");

    await SendAsync(first, new
    {
        type = "command", commandId = Guid.NewGuid().ToString(), expectedVersion = 0,
        command = new { type = "concede", playerId = 1 }
    });
    var spoof = await ReadTypeAsync(first, "error");
    Check(spoof.GetProperty("error").GetProperty("code").GetString() == "invalid_json", "Command actors cannot be spoofed.");
    await SendAsync(first, Envelope("concede", 0));
    Check((await ReadTypeAsync(first, "commandResult")).GetProperty("accepted").GetBoolean(), "Authorized player may concede.");
    var final = await ReadTypeAsync(first, "state");
    Check(final.GetProperty("view").GetProperty("winnerId").GetInt32() == 1, "The token owner's player conceded.");
}

static async Task IdempotencyAndRecovery()
{
    var storage = NewStorage();
    SeatReservation host;
    SeatReservation guest;
    CommandEnvelope command;
    int actor;
    try
    {
        await using (var server = await TestServer.StartAsync(storage))
        {
            host = await server.CreateAsync();
            guest = await server.JoinAsync(host.RoomCode);
            using var first = await server.ConnectAsync(host);
            var firstState = await ReadTypeAsync(first, "state");
            using var second = await server.ConnectAsync(guest);
            await ReadTypeAsync(second, "state");
            actor = firstState.GetProperty("view").GetProperty("priorityPlayerId").GetInt32();
            var socket = actor == 0 ? first : second;
            command = Envelope("pass", 0);
            await SendAsync(socket, command);
            var accepted = await ReadTypeAsync(socket, "commandResult");
            Check(accepted.GetProperty("accepted").GetBoolean(), "The priority player's initial pass is accepted.");
            await ReadTypeAsync(socket, "state", state => state.GetProperty("sequence").GetInt64() == 1);
            await SendAsync(socket, command);
            var duplicate = await ReadTypeAsync(socket, "commandResult");
            Check(duplicate.GetProperty("accepted").GetBoolean() && duplicate.GetProperty("duplicate").GetBoolean()
                && duplicate.GetProperty("sequence").GetInt64() == 1, "An identical retry is acknowledged without reapplying it.");
            await SendAsync(socket, command with { Command = new WireGameCommand("concede") });
            var reused = await ReadTypeAsync(socket, "commandResult");
            Check(ErrorCode(reused) == "command_id_reused", "An accepted ID cannot be reused for another command.");
            await SendAsync(socket, Envelope("pass", 0));
            Check(ErrorCode(await ReadTypeAsync(socket, "commandResult")) == "stale_version", "A new command with an old version is rejected.");
        }

        var journal = await File.ReadAllTextAsync(Path.Combine(storage, host.RoomCode + ".room.json"));
        Check(!journal.Contains(host.SeatToken, StringComparison.Ordinal) && !journal.Contains(guest.SeatToken, StringComparison.Ordinal),
            "Recovery stores token hashes, never plaintext tokens.");
        await using (var recovered = await TestServer.StartAsync(storage))
        {
            using var resumed = await recovered.ConnectAsync(actor == 0 ? host : guest);
            var state = await ReadTypeAsync(resumed, "state");
            Check(state.GetProperty("sequence").GetInt64() == 1, "Accepted gameplay survives server restart.");
            VerifyPrivacy(state, actor);
            await SendAsync(resumed, command);
            var duplicate = await ReadTypeAsync(resumed, "commandResult");
            Check(duplicate.GetProperty("duplicate").GetBoolean(), "Accepted command IDs survive restart.");
        }
    }
    finally { Directory.Delete(storage, recursive: true); }
}

static async Task ConcurrentCommands()
{
    await using var server = await TestServer.StartAsync();
    var host = await server.CreateAsync();
    var guest = await server.JoinAsync(host.RoomCode);
    using var first = await server.ConnectAsync(host);
    await ReadTypeAsync(first, "state");
    using var second = await server.ConnectAsync(guest);
    await ReadTypeAsync(second, "state");
    await Task.WhenAll(SendAsync(first, Envelope("concede", 0)), SendAsync(second, Envelope("concede", 0)));
    var replies = await Task.WhenAll(ReadTypeAsync(first, "commandResult"), ReadTypeAsync(second, "commandResult"));
    Check(replies.Count(r => r.GetProperty("accepted").GetBoolean()) == 1, "Exactly one concurrent change is accepted.");
    Check(replies.Count(r => !r.GetProperty("accepted").GetBoolean() && ErrorCode(r) == "stale_version") == 1,
        "The other concurrent change is rejected as stale.");
    Check(replies.All(r => r.GetProperty("sequence").GetInt64() == 1), "Both players observe the same authoritative version.");
}

static async Task PersistenceRollback()
{
    var storage = NewStorage();
    try
    {
        await using var server = await TestServer.StartAsync(storage);
        var host = await server.CreateAsync();
        await server.JoinAsync(host.RoomCode);
        using var socket = await server.ConnectAsync(host);
        await ReadTypeAsync(socket, "state");
        Directory.Move(storage, storage + ".backup");
        await File.WriteAllTextAsync(storage, "Simulate an unavailable storage directory.");
        var command = Envelope("concede", 0);
        await SendAsync(socket, command);
        var rejected = await ReadTypeAsync(socket, "commandResult");
        Check(ErrorCode(rejected) == "storage_unavailable" && rejected.GetProperty("sequence").GetInt64() == 0,
            "An unsaved command does not commit game state.");
        File.Delete(storage);
        Directory.Move(storage + ".backup", storage);
        await SendAsync(socket, command);
        var retried = await ReadTypeAsync(socket, "commandResult");
        Check(retried.GetProperty("accepted").GetBoolean() && !retried.GetProperty("duplicate").GetBoolean(),
            "The unchanged command can be retried after storage recovers.");
    }
    finally
    {
        if (File.Exists(storage)) File.Delete(storage);
        if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
        if (Directory.Exists(storage + ".backup")) Directory.Delete(storage + ".backup", recursive: true);
    }
}

static async Task ReconnectAndMalformedInput()
{
    await using var server = await TestServer.StartAsync();
    var host = await server.CreateAsync();
    await server.JoinAsync(host.RoomCode);
    using var first = await server.ConnectAsync(host);
    await ReadTypeAsync(first, "state");
    using var second = await server.ConnectAsync(host);
    Check((await ReadTypeAsync(second, "state")).GetProperty("playerId").GetInt32() == 0, "A token resumes its existing seat.");
    await SendAsync(second, new { type = "command", command = new { type = "concede" } });
    Check((await ReadTypeAsync(second, "error")).GetProperty("error").GetProperty("code").GetString() == "invalid_json",
        "Missing command IDs and versions are rejected.");
    var oversized = new byte[Protocol.MaximumMessageBytes + 1];
    Array.Fill(oversized, (byte)' ');
    await second.SendAsync(oversized.AsMemory(), WebSocketMessageType.Text, true, CancellationToken.None);
    Check((await ReadTypeAsync(second, "error")).GetProperty("error").GetProperty("code").GetString() == "message_too_large",
        "Oversized messages are rejected without changing state.");
}

// Issue #312: the server persists a room as its journal (seed + accepted commands) and recovers it with MatchEngine.Replay, so a
// shuffle that used a persisted, deterministic generator is reproduced exactly -- and one that did not would come back different.
// A room is written to disk mid-search (a Terrain found, the discard still to choose), a server is started on it, the seats connect,
// the search finishes, the server is RESTARTED, and the turn is played out; the cards player 0 then draws depend on the shuffled
// Fate deck, so they must equal those of a reference match that never restarted. Three seeds keep a coincidence from passing.
static async Task SearchAndShuffleSurviveRestart()
{
    foreach (var seed in new ulong[] { 11, 22, 33 }) await SearchAndShuffleSurviveRestartFor(seed);
}

static async Task SearchAndShuffleSurviveRestartFor(ulong seed)
{
    var storage = NewStorage();
    try
    {
        var hostToken = "host-token-" + seed; var guestToken = "guest-token-" + seed; const string room = "SRCHTEST";
        static string HashToken(string token) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var journal = MatchEngine.CreateDeckSearchShowcase(seed); var reference = MatchEngine.CreateDeckSearchShowcase(seed);
        var shizuka = journal.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "oracle-l5r-10292").Id;
        foreach (var engine in new[] { journal, reference })
            foreach (var command in new[] { new GameCommand("useAbility", 0, 0, shizuka, AbilityId: "shizuka-search"), new GameCommand("chooseSearchDeck", 0, 1, Choice: "Fate"), new GameCommand("selectSearchCard", 0, 2, Choice: "training-terrain") })
                if (!engine.Execute(command).Accepted) throw new InvalidOperationException("Setup command rejected: " + command.Type);
        var store = new RoomStore(storage);
        await store.SaveAsync(new PersistedRoom(1, room, DateTimeOffset.UtcNow, [new SeatCredential(0, HashToken(hostToken)), new SeatCredential(1, HashToken(guestToken))], journal.ExportReplay(), []));

        JsonElement[] latest = new JsonElement[2];
        async Task Connect(TestServer server, ClientWebSocket[] sockets)
        {
            sockets[0] = await server.ConnectAsync(new SeatReservation(room, 0, hostToken));
            latest[0] = await ReadLargeTypeAsync(sockets[0], "state");
            sockets[1] = await server.ConnectAsync(new SeatReservation(room, 1, guestToken));
            latest[1] = await ReadLargeTypeAsync(sockets[1], "state");
            latest[0] = await ReadLargeTypeAsync(sockets[0], "state", s => s.GetProperty("playersConnected").EnumerateArray().All(v => v.GetBoolean()));
        }
        async Task Act(ClientWebSocket[] sockets, int seat, WireGameCommand command)
        {
            var version = latest[seat].GetProperty("sequence").GetInt64();
            await SendAsync(sockets[seat], new CommandEnvelope("command", Guid.NewGuid().ToString(), version, command));
            var reply = await ReadLargeTypeAsync(sockets[seat], "commandResult");
            Check(reply.GetProperty("accepted").GetBoolean(), $"The server rejected {command.Type}: {reply.GetRawText()}");
            var sequence = reply.GetProperty("sequence").GetInt64();
            for (var s = 0; s < 2; s++) latest[s] = await ReadLargeTypeAsync(sockets[s], "state", state => state.GetProperty("sequence").GetInt64() == sequence);
            var mirrored = reference.Execute(command.BindToSeat(seat, reference.Version));
            Check(mirrored.Accepted && mirrored.Version == sequence, "The reference match rejected what the server accepted: " + mirrored.Error);
        }
        static string? Pick(IEnumerable<(string Type, bool Enabled)> actions) => actions.Where(a => a.Enabled).Select(a => a.Type).FirstOrDefault(t => t == "endDynasty") ?? actions.Where(a => a.Enabled).Select(a => a.Type).FirstOrDefault(t => t is "pass" or "discardHand");

        // ---- first server: the recovered mid-search room ----
        var sockets = new ClientWebSocket[2];
        await using (var first = await TestServer.StartAsync(storage))
        {
            await Connect(first, sockets);
            var host = latest[0].GetProperty("view"); var guest = latest[1].GetProperty("view");
            Check(latest[0].GetProperty("sequence").GetInt64() == 3, "The room recovered at the journal's version");
            Check(host.GetProperty("pendingSearch").ValueKind == JsonValueKind.Object && host.GetProperty("pendingSearch").GetProperty("stage").GetString() == "ChooseDiscard", "The searcher's recovered view holds the search");
            Check(guest.GetProperty("pendingSearch").ValueKind == JsonValueKind.Null, "The opponent's view has no deck viewer");
            Check(host.GetProperty("shownCards").GetArrayLength() == 1 && guest.GetProperty("shownCards").GetArrayLength() == 1
                && host.GetProperty("shownCards").GetRawText() == guest.GetProperty("shownCards").GetRawText(), "The shown-cards record is public and recovered");
            var foundId = reference.State.Players[0].Hand.Select(id => reference.State.Cards[id]).Single(c => c.DefinitionId == "training-terrain").Id;
            Check(!latest[1].GetRawText().Contains('"' + foundId + '"', StringComparison.Ordinal), "The recovered opponent envelope holds no id of the hand card that was found");
            Check(!latest[1].GetRawText().Contains("rngState", StringComparison.OrdinalIgnoreCase), "No generator state on the wire");
            var actionTypes = latest[0].GetProperty("actions").EnumerateArray().Where(a => a.GetProperty("enabled").GetBoolean()).Select(a => a.GetProperty("type").GetString()).ToArray();
            Check(actionTypes.Contains("chooseSearchDiscard") && actionTypes.All(t => t is "chooseSearchDiscard" or "concede"), "The recovered searcher is offered exactly the discard choice");
            // A stray command mid-search is rejected by the server-side engine, atomically.
            await SendAsync(sockets[0], new CommandEnvelope("command", Guid.NewGuid().ToString(), 3, new WireGameCommand("passSearch")));
            Check(ErrorCode(await ReadLargeTypeAsync(sockets[0], "commandResult")) == "illegal_action", "The wrong search command is refused");
            var discard = host.GetProperty("players")[0].GetProperty("hand")[0].GetProperty("id").GetString()!;
            await Act(sockets, 0, new WireGameCommand("chooseSearchDiscard", TargetId: discard));
            Check(latest[0].GetProperty("view").GetProperty("pendingSearch").ValueKind == JsonValueKind.Null && latest[0].GetProperty("view").GetProperty("events").EnumerateArray().Any(e => e.GetProperty("type").GetString() == "deckShuffled"), "The search finished and the deck was shuffled");
            foreach (var socket in sockets) socket.Dispose();
        }

        // ---- second server: restart, then play the turn out until player 0 has drawn ----
        sockets = new ClientWebSocket[2];
        await using (var second = await TestServer.StartAsync(storage))
        {
            await Connect(second, sockets);
            Check(latest[0].GetProperty("sequence").GetInt64() == reference.Version, "Accepted gameplay, the search included, survives the restart");
            for (var step = 0; step < 40 && latest[0].GetProperty("view").GetProperty("activePlayerId").GetInt32() == 0; step++)
            {
                var seat = latest[0].GetProperty("view").GetProperty("priorityPlayerId").GetInt32();
                var type = Pick(latest[seat].GetProperty("actions").EnumerateArray().Select(a => (a.GetProperty("command").ValueKind == JsonValueKind.Object ? a.GetProperty("command").GetProperty("type").GetString()! : "", a.GetProperty("enabled").GetBoolean())));
                Check(type is not null, "No way to continue the turn");
                await Act(sockets, seat, new WireGameCommand(type!));
            }
            Check(latest[0].GetProperty("view").GetProperty("activePlayerId").GetInt32() == 1, "Player 0's turn ended");
            var serverHand = latest[0].GetProperty("view").GetProperty("players")[0].GetProperty("hand").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToArray();
            var referenceHand = reference.GetView(0).Players[0].Hand.Select(c => c.Id).ToArray();
            Check(serverHand.SequenceEqual(referenceHand), $"Seed {seed}: the cards drawn from the shuffled Fate deck differ after a restart");
            Check(latest[0].GetProperty("view").GetProperty("events").GetRawText() == JsonSerializer.Serialize(reference.GetView(0).Events, Protocol.Json), "The public event log survives the restart unchanged");
            foreach (var socket in sockets) socket.Dispose();
        }
    }
    finally { Directory.Delete(storage, recursive: true); }
}

// Issue #322: a room journaled while a player is looking at the OPPONENT's deck is recovered by a restarted server with the look intact --
// the viewer's ordered cards reach only the viewer's seat, the deck owner's seat gets nothing but the public log, the arrangement command
// (CardIds tokens) is accepted over the wire, and the rearranged deck survives a second restart (the cards the owner then draws are the
// ones the arrangement put on top).
static async Task LookSurvivesRestart()
{
    var storage = NewStorage();
    try
    {
        const string room = "LCKTEST2"; var hostToken = "look-host-token"; var guestToken = "look-guest-token";
        static string HashToken(string token) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var journal = MatchEngine.CreateDeckSearchShowcase(77); var reference = MatchEngine.CreateDeckSearchShowcase(77);
        var source = journal.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-look-source").Id;
        foreach (var engine in new[] { journal, reference })
            foreach (var command in new[] { new GameCommand("useAbility", 0, 0, source, AbilityId: "training-look-any"), new GameCommand("chooseLookDeck", 0, 1, Choice: "Fate", TargetPlayerId: 1) })
                if (!engine.Execute(command).Accepted) throw new InvalidOperationException("Setup command rejected: " + command.Type);
        var opponentTop = reference.State.Players[1].FateDeck.Take(3).Select(id => reference.State.Cards[id].DefinitionId).ToArray();
        await new RoomStore(storage).SaveAsync(new PersistedRoom(1, room, DateTimeOffset.UtcNow, [new SeatCredential(0, HashToken(hostToken)), new SeatCredential(1, HashToken(guestToken))], journal.ExportReplay(), []));

        var latest = new JsonElement[2];
        async Task<ClientWebSocket[]> Connect(TestServer server)
        {
            var sockets = new ClientWebSocket[2];
            sockets[0] = await server.ConnectAsync(new SeatReservation(room, 0, hostToken)); latest[0] = await ReadLargeTypeAsync(sockets[0], "state");
            sockets[1] = await server.ConnectAsync(new SeatReservation(room, 1, guestToken)); latest[1] = await ReadLargeTypeAsync(sockets[1], "state");
            latest[0] = await ReadLargeTypeAsync(sockets[0], "state", s => s.GetProperty("playersConnected").EnumerateArray().All(v => v.GetBoolean()));
            return sockets;
        }
        // ---- first server: the recovered mid-look room ----
        ClientWebSocket[] sockets;
        await using (var first = await TestServer.StartAsync(storage))
        {
            sockets = await Connect(first);
            var host = latest[0].GetProperty("view"); var guest = latest[1].GetProperty("view");
            Check(latest[0].GetProperty("sequence").GetInt64() == 2, "The room recovered at the journal's version");
            var look = host.GetProperty("pendingLook");
            Check(look.ValueKind == JsonValueKind.Object && look.GetProperty("stage").GetString() == "Arrange" && !look.GetProperty("ownDeck").GetBoolean() && look.GetProperty("deckOwnerId").GetInt32() == 1, "The viewer's recovered seat holds the look at the opponent's deck");
            Check(look.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("definitionId").GetString()).SequenceEqual(opponentTop), "The opponent's top cards, in order");
            Check(guest.GetProperty("pendingLook").ValueKind == JsonValueKind.Null, "The deck owner's seat has no viewer");
            var ownerText = latest[1].GetRawText();
            // Showcase decks share common definitions with the owner's own hand, so the leak test is on the cards' instance ids.
            foreach (var id in reference.State.Players[1].FateDeck.Take(3)) Check(!ownerText.Contains('"' + id + '"', StringComparison.Ordinal), $"The deck owner's recovered envelope carries the id of {id}, a card being looked at");
            Check(!ownerText.Contains("rngState", StringComparison.OrdinalIgnoreCase), "No generator state on the wire");
            // A wrong arrangement is refused by the engine behind the server; the right one is accepted.
            await SendAsync(sockets[0], new CommandEnvelope("command", Guid.NewGuid().ToString(), 2, new WireGameCommand("arrangeTopCards", CardIds: ["0:hand", "1:top", "2:top"])));
            Check(ErrorCode(await ReadLargeTypeAsync(sockets[0], "commandResult")) == "illegal_action", "Taking a card from the opponent's deck into the hand is refused");
            // Discard the first card, send the second to the bottom, keep the third: the owner's top card becomes the third.
            await SendAsync(sockets[0], new CommandEnvelope("command", Guid.NewGuid().ToString(), 2, new WireGameCommand("arrangeTopCards", CardIds: ["2:top", "0:discard", "1:bottom"])));
            var reply = await ReadLargeTypeAsync(sockets[0], "commandResult"); Check(reply.GetProperty("accepted").GetBoolean(), reply.GetRawText());
            for (var s = 0; s < 2; s++) latest[s] = await ReadLargeTypeAsync(sockets[s], "state", state => state.GetProperty("sequence").GetInt64() == 3);
            Check(reference.Execute(new GameCommand("arrangeTopCards", 0, 2, CardIds: ["2:top", "0:discard", "1:bottom"])).Accepted, "The reference match accepts the same arrangement");
            Check(latest[0].GetProperty("view").GetProperty("pendingLook").ValueKind == JsonValueKind.Null, "The look is over");
            Check(latest[1].GetProperty("view").GetProperty("players")[1].GetProperty("fateDeckCount").GetInt32() == reference.State.Players[1].FateDeck.Count, "The owner's deck count reflects the discard");
            foreach (var socket in sockets) socket.Dispose();
        }
        // ---- second server: restart; the arrangement is part of the journal ----
        await using (var second = await TestServer.StartAsync(storage))
        {
            sockets = await Connect(second);
            Check(latest[0].GetProperty("sequence").GetInt64() == 3 && latest[0].GetProperty("view").GetProperty("events").GetRawText() == JsonSerializer.Serialize(reference.GetView(0).Events, Protocol.Json), "The arrangement and its public events survive the restart");
            var ownerView = latest[1].GetProperty("view");
            Check(ownerView.GetProperty("events").EnumerateArray().Any(e => e.GetProperty("type").GetString() == "topCardsLooked" && e.GetProperty("message").GetString()!.Contains("Lion's Fate deck")), "The deck owner was told a look happened");
            foreach (var socket in sockets) socket.Dispose();
        }
    }
    finally { Directory.Delete(storage, recursive: true); }
}

// Issue #303: a command field or quote missing from the wire records is silently dropped online (DeckPosition and
// TargetPlayerId were), so compare the wire command with GameCommand by reflection and round-trip real quotes.
static Task GoldWire()
{
    var command = new GameCommand("useAbility", 1, 9, "card", ["source"], 2, true, true, new() { ["unit"] = 1 }, ["card-a"], "target",
        "ability", "performer", "choice", "Top", 1, 4, "destroy", [new SlotSelection("slot", ["card-b"], 1)]);
    var properties = typeof(GameCommand).GetProperties().Where(p => p.Name is not ("PlayerId" or "ExpectedVersion" or "EqualityContract")).ToArray();
    foreach (var property in properties)
    {
        Check(typeof(WireGameCommand).GetProperty(property.Name) is not null, $"WireGameCommand has no {property.Name}: it would be dropped online.");
        var value = property.GetValue(command);
        Check(value is not null && !(property.PropertyType.IsValueType && Equals(value, Activator.CreateInstance(property.PropertyType))),
            $"The sample command leaves {property.Name} at its default, so this test would not notice it being dropped.");
    }
    var json = JsonSerializer.Serialize(WireGameCommand.FromCore(command), Protocol.Json);
    var bound = JsonSerializer.Deserialize<WireGameCommand>(json, Protocol.Json)!.BindToSeat(1, 9);
    Check(JsonSerializer.Serialize(bound) == JsonSerializer.Serialize(command), "Every GameCommand field survives the wire and the seat binding.");

    var quote = new GoldQuote(3, GoldCostKind.Variable, "You choose", null, [new PaymentOption(["a"], 4, 3, 1)], 0, 6,
        new Dictionary<int, PaymentOption[]> { [0] = [new PaymentOption([], 0, 0, 0)], [3] = [new PaymentOption(["a"], 4, 3, 1)] });
    var option = new ActionOption("useAbility", "Pay", true, null, command, "card", ["t1", "t2"], null, null, quote,
        new Dictionary<string, GoldQuote> { ["t1"] = quote, ["t2"] = quote with { Cost = 5, Kind = GoldCostKind.Derived } },
        [new TargetSlotOption("attachments", "One to three attachments", TargetSlotKinds.Card, 1, 3, ["t1", "t2"], []), new TargetSlotOption("player", "A player", TargetSlotKinds.Player, 1, 1, [], [0, 1])]);
    var action = AvailableAction.FromCore(option);
    var wire = JsonSerializer.Serialize(action, Protocol.Json);
    var back = JsonSerializer.Deserialize<AvailableAction>(wire, Protocol.Json)!;
    Check(back.Gold is { Cost: 3, Kind: GoldCostKind.Variable, MinAmount: 0, MaxAmount: 6 } && back.Gold.PaymentsByAmount![3][0].SourceIds[0] == "a", "The Gold quote and its per-amount payments cross the wire.");
    Check(back.TargetGold!["t2"].Cost == 5 && back.TargetGold["t2"].Kind == GoldCostKind.Derived, "Per-target quotes cross the wire.");
    Check(back.Command!.GoldAmount == 4, "The chosen amount is part of the wire command.");
    Check(back.Command!.Selections is [{ Slot: "slot", PlayerId: 1 }] && back.Command.Selections[0].CardIds![0] == "card-b", "Multi-target selections are part of the wire command (issue #336).");
    Check(back.Slots is [{ Slot: "attachments", Min: 1, Max: 3 }, { Slot: "player", Kind: TargetSlotKinds.Player }] && back.Slots[0].CardIds.SequenceEqual(new[] { "t1", "t2" }) && back.Slots[1].PlayerIds.SequenceEqual(new[] { 0, 1 }), "The slots of a multi-target action cross the wire with their candidates (issue #336).");
    return Task.CompletedTask;
}

static void VerifyPrivacy(JsonElement state, int playerId)
{
    var text = state.GetRawText();
    foreach (var privateField in new[] { "seed", "replay", "tokenHash", "seats" })
        Check(!text.Contains('"' + privateField + '"', StringComparison.OrdinalIgnoreCase), $"No private {privateField} is transmitted.");
    var players = state.GetProperty("view").GetProperty("players");
    Check(players[1 - playerId].GetProperty("hand").GetArrayLength() == 0, "Opponent hands are redacted.");
    Check(players[playerId].GetProperty("hand").GetArrayLength() == players[playerId].GetProperty("handCount").GetInt32(),
        "A player receives their own hand.");
}

static CommandEnvelope Envelope(string type, long version) => new("command", Guid.NewGuid().ToString(), version, new WireGameCommand(type));
static string? ErrorCode(JsonElement reply) => reply.GetProperty("error").GetProperty("code").GetString();
static string NewStorage() => Path.Combine(Path.GetTempPath(), "l5r-server-tests-" + Guid.NewGuid().ToString("N"));
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static Task SendAsync<T>(ClientWebSocket socket, T message) => socket.SendAsync(
    JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json).AsMemory(), WebSocketMessageType.Text, true,
    CancellationToken.None).AsTask();

// ClientConnection.ReceiveAsync enforces the 32 KiB limit on what a CLIENT may send the server; server-to-client state messages
// have no such cap (MatchSession accepts up to 4 MiB). A state that carries a whole deck viewer (issue #312) exceeds 32 KiB, so the
// search test reads with the client's own limit instead.
static async Task<JsonElement> ReadLargeTypeAsync(ClientWebSocket socket, string type, Func<JsonElement, bool>? predicate = null)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var buffer = new byte[16384];
    while (true)
    {
        using var message = new MemoryStream();
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            if (part.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("The socket closed before the expected message.");
            message.Write(buffer, 0, part.Count);
            if (message.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Server snapshot exceeds the client's supported size.");
        } while (!part.EndOfMessage);
        using var document = JsonDocument.Parse(message.ToArray());
        var value = document.RootElement;
        if (value.GetProperty("type").GetString() == type && (predicate is null || predicate(value))) return value.Clone();
    }
}

static async Task<JsonElement> ReadTypeAsync(ClientWebSocket socket, string type, Func<JsonElement, bool>? predicate = null)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (true)
    {
        var bytes = await ClientConnection.ReceiveAsync(socket, timeout.Token)
            ?? throw new InvalidOperationException("The socket closed before the expected message.");
        using var document = JsonDocument.Parse(bytes);
        var value = document.RootElement;
        if (value.GetProperty("type").GetString() == type && (predicate is null || predicate(value))) return value.Clone();
    }
}

sealed class TestServer(WebApplication app, string storage, bool ownsStorage) : IAsyncDisposable
{
    public HttpClient Http { get; } = new() { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
    public static async Task<TestServer> StartAsync(string? storage = null)
    {
        var owns = storage is null;
        storage ??= Path.Combine(Path.GetTempPath(), "l5r-server-tests-" + Guid.NewGuid().ToString("N"));
        var app = await ServerApplication.BuildAsync(["--urls", "http://127.0.0.1:0", "--L5R:StoragePath", storage,
            "--Logging:LogLevel:Default", Environment.GetEnvironmentVariable("L5R_TEST_LOG_LEVEL") ?? "Critical"]);
        await app.StartAsync();
        return new TestServer(app, storage, owns);
    }
    public async Task<SeatReservation> CreateAsync() => await ReservationAsync("/rooms");
    public async Task<SeatReservation> JoinAsync(string code) => await ReservationAsync($"/rooms/{code}/join");
    private async Task<SeatReservation> ReservationAsync(string path)
    {
        using var response = await Http.PostAsync(path, null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SeatReservation>(Protocol.Json)
            ?? throw new InvalidOperationException("Missing reservation response.");
    }
    public Uri SocketUri(string code) => new UriBuilder(Http.BaseAddress!) { Scheme = "ws", Path = $"/rooms/{code}/play" }.Uri;
    public async Task<ClientWebSocket> ConnectAsync(SeatReservation reservation, bool hello = true)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + reservation.SeatToken);
        await socket.ConnectAsync(SocketUri(reservation.RoomCode), CancellationToken.None);
        if (hello)
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new ClientHello("hello", Protocol.Version, Protocol.ContentVersion),
                Protocol.Json).AsMemory(), WebSocketMessageType.Text, true, CancellationToken.None);
        return socket;
    }
    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await app.StopAsync(timeout.Token);
        await app.DisposeAsync();
        if (ownsStorage && Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
    }
}
