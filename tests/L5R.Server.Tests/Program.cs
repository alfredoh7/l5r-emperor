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
    ("every command field and every Gold quote cross the wire intact (issue #303)", GoldWire)
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

// Issue #303: a command field or quote missing from the wire records is silently dropped online (DeckPosition and
// TargetPlayerId were), so compare the wire command with GameCommand by reflection and round-trip real quotes.
static Task GoldWire()
{
    var command = new GameCommand("useAbility", 1, 9, "card", ["source"], 2, true, true, new() { ["unit"] = 1 }, ["card-a"], "target",
        "ability", "performer", "choice", "Top", 1, 4);
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
        new Dictionary<string, GoldQuote> { ["t1"] = quote, ["t2"] = quote with { Cost = 5, Kind = GoldCostKind.Derived } });
    var action = AvailableAction.FromCore(option);
    var wire = JsonSerializer.Serialize(action, Protocol.Json);
    var back = JsonSerializer.Deserialize<AvailableAction>(wire, Protocol.Json)!;
    Check(back.Gold is { Cost: 3, Kind: GoldCostKind.Variable, MinAmount: 0, MaxAmount: 6 } && back.Gold.PaymentsByAmount![3][0].SourceIds[0] == "a", "The Gold quote and its per-amount payments cross the wire.");
    Check(back.TargetGold!["t2"].Cost == 5 && back.TargetGold["t2"].Kind == GoldCostKind.Derived, "Per-target quotes cross the wire.");
    Check(back.Command!.GoldAmount == 4, "The chosen amount is part of the wire command.");
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
