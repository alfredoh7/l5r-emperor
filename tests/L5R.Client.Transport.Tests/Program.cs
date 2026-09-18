using System.Collections.Concurrent;
using L5R.Client;
using L5R.Core;
using L5R.Server;

var storage = Path.Combine(Path.GetTempPath(), "l5r-client-transport-" + Guid.NewGuid().ToString("N"));
var app = await ServerApplication.BuildAsync(["--urls", "http://127.0.0.1:0", "--L5R:StoragePath", storage,
    "--Logging:LogLevel:Default", "Critical"]);
var failures = 0;
try
{
    await app.StartAsync();
    var url = app.Urls.Single();
    var cases = new (string Name, Func<Task> Test)[]
    {
        ("client create/join, typed actions, commands and authenticated actor", () => LivePlay(url)),
        ("published match transmits Border Keep ability and restores it on reconnect", () => PublishedPlay(url)),
        ("client disconnect/resume and overlapping reconnects", () => Reconnect(url)),
        ("client rejects insecure remote URLs and invalid room reservations", () => InvalidConnections(url)),
        ("client command payload carries every GameCommand field the server accepts (issue #303)", () => { OutgoingPayload(); return Task.CompletedTask; })
    };
    foreach (var (name, test) in cases)
    {
        Console.WriteLine($"RUN  {name}");
        try { await test(); Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}\n{exception}"); }
    }
    Console.WriteLine($"{cases.Length - failures}/{cases.Length} client transport integration checks passed.");
}
finally
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await app.StopAsync(timeout.Token);
    await app.DisposeAsync();
    if (Directory.Exists(storage)) Directory.Delete(storage, recursive: true);
}
return failures == 0 ? 0 : 1;

static async Task LivePlay(string url)
{
    using var host = new MatchSession();
    using var guest = new MatchSession();
    RoomSeat? savedHost = null;
    var notices = new ConcurrentQueue<string>();
    host.SeatCreated += (seat, _) => savedHost = seat;
    host.Notice += notices.Enqueue;
    guest.Notice += notices.Enqueue;
    await host.CreateOrJoin(url, null);
    await WaitFor(() => host.Snapshot is not null && !host.Busy);
    Check(savedHost is not null && savedHost == host.Seat, "Creating a room delivers a resumable seat.");
    await guest.CreateOrJoin(url, host.Seat!.RoomCode);
    await WaitFor(() => guest.Snapshot is not null && !guest.Busy);
    await WaitFor(() => host.Snapshot?.PlayersConnected?.All(value => value) == true
        && guest.Snapshot?.PlayersConnected?.All(value => value) == true);
    Check(host.Connected && guest.Connected && host.Online && guest.Online, "Both sessions are connected.");
    Check(host.Seat.PlayerId == 0 && guest.Seat!.PlayerId == 1, "Joining creates the second seat.");
    foreach (var session in new[] { host, guest })
    {
        var snapshot = session.Snapshot!;
        Check(snapshot.Mode == "Private room" && snapshot.RoomCode == host.Seat.RoomCode, "The client consumes the room envelope.");
        Check(snapshot.View.Players[1 - snapshot.View.ViewerId].Hand.Length == 0, "The opponent's hand is private.");
        Check(snapshot.Actions.Length > 0, "Typed action deserialization succeeds.");
        Check(snapshot.Actions.Where(a => a.Command is not null).All(a => a.Command!.PlayerId == session.Seat!.PlayerId
            && a.Command.ExpectedVersion == snapshot.View.Version), "Templates bind the authenticated actor and latest version.");
    }
    var actor = host.Snapshot!.View.PriorityPlayerId == 0 ? host : guest;
    var pass = actor.Snapshot!.Actions.First(a => a.Enabled && a.Command?.Type == "pass").Command!;
    await actor.Submit(pass);
    await WaitFor(() => host.Snapshot!.View.Version == 1 && guest.Snapshot!.View.Version == 1 && !actor.Busy);
    Check(notices.IsEmpty, "An actual legal command reaches the server without schema errors.");

    var count = notices.Count;
    await actor.Submit(new GameCommand("pass", actor.Seat!.PlayerId, 0));
    await WaitFor(() => !actor.Busy && notices.Count > count);
    Check(actor.Snapshot!.View.Version == 1, "A stale rejection clears Busy without modifying the view.");

    // Deliberately supply a different actor: the client must omit it on the wire, and the server must bind the token.
    await actor.Submit(new GameCommand("concede", 1 - actor.Seat.PlayerId, 1));
    await WaitFor(() => host.Snapshot!.View.Version == 2 && guest.Snapshot!.View.Version == 2 && !actor.Busy);
    Check(actor.Snapshot!.View.WinnerId == 1 - actor.Seat.PlayerId,
        "The command payload omits playerId, and the authenticated seat is the player that concedes.");
}

static async Task Reconnect(string url)
{
    using var host = new MatchSession();
    using var guest = new MatchSession();
    await host.CreateOrJoin(url, null);
    await WaitFor(() => host.Snapshot is not null && !host.Busy);
    var saved = host.Seat!;
    await guest.CreateOrJoin(url, saved.RoomCode);
    await WaitFor(() => guest.Snapshot is not null && !guest.Busy);
    var actor = host.Snapshot!.View.PriorityPlayerId == 0 ? host : guest;
    await actor.Submit(actor.Snapshot!.Actions.First(a => a.Command?.Type == "pass").Command!);
    await WaitFor(() => host.Snapshot!.View.Version == 1 && guest.Snapshot!.View.Version == 1);
    host.Disconnect();
    Check(!host.Connected && !host.Online && !host.Busy, "Disconnect leaves no active transport.");
    await host.Resume(url, saved);
    await WaitFor(() => host.Snapshot?.View.Version == 1 && !host.Busy);
    Check(host.Seat == saved && host.Snapshot!.View.ViewerId == saved.PlayerId, "Resume retains identity and authoritative state.");

    // A canceled older Connect/Receive must never disconnect or overwrite the latest session.
    var old = host.Resume(url, saved);
    var latest = host.Resume(url, saved);
    try { await old; } catch (Exception e) when (e is OperationCanceledException or System.Net.WebSockets.WebSocketException or ObjectDisposedException) { }
    await latest;
    await WaitFor(() => host.Connected && host.Snapshot?.View.Version == 1 && !host.Busy);
    await host.Submit(new GameCommand("concede", saved.PlayerId, 1));
    await WaitFor(() => host.Snapshot?.View.Version == 2 && !host.Busy);
    Check(host.Snapshot!.View.WinnerId == 1, "The latest connection remains usable after overlapping reconnects.");
}

// The outgoing mapping is an explicit field list, so a new GameCommand field is silently dropped online unless it is
// added there (DeckPosition and TargetPlayerId were). Compare against GameCommand by reflection, and prove the
// server's strict schema (unmapped members are errors) accepts exactly what the client sends.
static void OutgoingPayload()
{
    var command = new GameCommand("useAbility", 1, 9, "card", ["source"], 2, true, true, new() { ["unit"] = 1 }, ["card-a"], "target",
        "ability", "performer", "choice", "Top", 1, 4);
    var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    var json = System.Text.Json.JsonSerializer.Serialize(MatchSession.ToWire(command), web);
    using var document = System.Text.Json.JsonDocument.Parse(json);
    var sent = document.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    var expected = typeof(GameCommand).GetProperties().Where(p => p.Name is not ("PlayerId" or "ExpectedVersion" or "EqualityContract"))
        .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..]).ToHashSet();
    Check(sent.SetEquals(expected), $"The client wire payload and GameCommand disagree: missing [{string.Join(",", expected.Except(sent))}] extra [{string.Join(",", sent.Except(expected))}]");
    var wire = System.Text.Json.JsonSerializer.Deserialize<WireGameCommand>(json, Protocol.Json)!;
    Check(System.Text.Json.JsonSerializer.Serialize(wire.BindToSeat(1, 9)) == System.Text.Json.JsonSerializer.Serialize(command), "The server binds exactly the command the client meant, GoldAmount included.");
}

static async Task InvalidConnections(string url)
{
    using var session = new MatchSession();
    var insecureRejected = false;
    try { await session.CreateOrJoin("http://example.com", null); }
    catch (InvalidOperationException) { insecureRejected = true; }
    Check(insecureRejected && !session.Busy && !session.Connected, "Plaintext remote servers are rejected before sending a request.");
    var missingRejected = false;
    try { await session.CreateOrJoin(url, "NOTAROOM"); }
    catch (InvalidOperationException) { missingRejected = true; }
    Check(missingRejected && !session.Busy && !session.Connected, "An invalid room leaves the client usable.");
}

static async Task WaitFor(Func<bool> condition)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (!condition()) await Task.Delay(10, timeout.Token);
}
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

static async Task PublishedPlay(string url)
{
    using var crane = new MatchSession(); using var lion = new MatchSession();
    await crane.CreateOrJoin(url, null, published:true);
    await WaitFor(() => crane.Snapshot is not null && !crane.Busy);
    await lion.CreateOrJoin(url, crane.Seat!.RoomCode);
    await WaitFor(() => lion.Snapshot is not null && !lion.Busy && crane.Snapshot?.PlayersConnected?.All(p=>p)==true);
    Check(lion.Snapshot!.View.Scenario==PublishedContent.Scenario && lion.Snapshot.View.PriorityPlayerId==1,"Published setup reaches both clients.");
    // Battle Phase timing (issue #310): the public battle-step view is part of every state message, identical for both seats, and survives the
    // strict server serializer and the client's own lenient one alike (an added view field must be optional so an older payload still parses).
    Check(lion.Snapshot.View.BattleStep is { Segment: BattleSteps.None, Step: BattleSteps.None, DecisionPlayerId: null } && crane.Snapshot!.View.BattleStep is { Segment: BattleSteps.None },
        "The battle-step view crosses the live wire (outside the Attack Phase: nothing in progress).");
    var stepView = lion.Snapshot.View with { BattleStep = new BattleStepView(BattleSteps.FightBattles, BattleSteps.Engage, 0, 0, 2, 1, BattleSteps.ReasonDefenderOverride, false,
        [new FirstOpportunityClaim(0, "Naval", "card-1"), new FirstOpportunityClaim(1, "Engage", null)], BattleSteps.LimitPerformerOnly, "card-2", 1, ["card-3"], "card-3", "engageReaction") };
    // The same discipline the GameCommand wire test applies: a member added to the public view later cannot silently fail to cross the wire.
    using (var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(stepView, Protocol.Json)))
        foreach (var type in new[] { typeof(MatchView), typeof(BattleStepView), typeof(FirstOpportunityClaim) })
        {
            var element = type == typeof(MatchView) ? json.RootElement : type == typeof(BattleStepView) ? json.RootElement.GetProperty("battleStep") : json.RootElement.GetProperty("battleStep").GetProperty("engageClaims")[0];
            foreach (var property in type.GetProperties())
                Check(element.TryGetProperty(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name), out _), $"{type.Name}.{property.Name} is missing from the serialized view.");
        }
    foreach (var options in new[] { Protocol.Json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) })
    {
        var message = new StateMessage("state", "ROOM", 0, 1, stepView, [], [true, true]);
        var round = System.Text.Json.JsonSerializer.Deserialize<StateMessage>(System.Text.Json.JsonSerializer.Serialize(message, options), Protocol.Json)!;
        Check(System.Text.Json.JsonSerializer.Serialize(round.View.BattleStep) == System.Text.Json.JsonSerializer.Serialize(stepView.BattleStep),
            "Every BattleStepView field, claims included, round-trips through the protocol serializer.");
        var older = System.Text.Json.JsonSerializer.Deserialize<StateMessage>(System.Text.Json.JsonSerializer.Serialize(message with { View = stepView with { BattleStep = null } }, options), Protocol.Json)!;
        Check(older.View.BattleStep is null, "A payload without the field still deserializes.");
    }
    var before = lion.Snapshot.View.Players[1].Provinces.Select(p=>p.Card!.Id).ToArray();
    var option = lion.Snapshot.Actions.First(a=>a.Enabled && a.Command?.AbilityId=="border-first");
    await lion.Submit(option.Command! with {CardIds=[before[0],before[2]]});
    await WaitFor(() => crane.Snapshot!.View.Version==1 && lion.Snapshot!.View.Version==1 && !lion.Busy);
    Check(lion.Snapshot!.View.Players[1].Provinces[0].Card!.Id!=before[0],"Ability ID and multi-card selection were sent to the authoritative engine.");
    Check(lion.Snapshot.View.Players[1].Provinces[1].Card!.Id==before[1],"Unselected province was retained.");
    Check(crane.Snapshot!.View.Players[1].Provinces[2].Card!.Definition is not null,"Opponent receives revealed replacements.");
    lion.Disconnect(); await lion.Resume(url,lion.Seat!);
    await WaitFor(() => lion.Snapshot?.View.Version==1 && !lion.Busy && lion.Connected);
    Check(lion.Snapshot!.View.Events.Any(e=>e.Type=="abilityUsed"),"Ability state survives reconnection.");
    var wire = WireGameCommand.FromCore(new GameCommand("useAbility",1,1,"source",TargetId:"target",AbilityId:"shinden-choice",PerformerId:"performer",Choice:"honor"));
    var bound = wire.BindToSeat(0,7);
    Check(bound.AbilityId=="shinden-choice" && bound.PerformerId=="performer" && bound.Choice=="honor" && bound.PlayerId==0 && bound.ExpectedVersion==7,"New fields survive wire conversion while actor/version remain authenticated.");
}
