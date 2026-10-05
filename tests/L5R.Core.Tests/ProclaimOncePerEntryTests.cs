using L5R.Core;

// Issue #642 (found by the bot soak, seed 23): Kakita Yusugi may be Proclaimed "even if you have already used Proclaim this turn", but only for his own entry. The
// Proclaim window used to offer him again and again while it stayed open (the opponent still held a Reaction), a loop that never ended. Once an entry's Proclaim is taken
// it is spent, whatever ignores the once-per-turn limit.
internal static class ProclaimOncePerEntryTests
{
    private const string Yusugi = "oracle-l5r-10051";
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run(TestRunner runner) => runner.Run("proclaim-once-per-entry", [
        ("A Personality that ignores the once-per-turn Proclaim limit is offered the Proclaim once per entry, and a second Proclaim for the same entry is refused", Scenarios),
    ]);

    private static (MatchEngine Engine, string Id) Window(bool alreadyProclaimed)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; s.Players[0].Clan = "Crane";
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"pro-{n}")) n++;
        var yusugi = new CardInstance { Id = $"pro-{n}", DefinitionId = Yusugi, OwnerId = 0, Zone = Zone.Home, FaceUp = true };
        s.Cards.Add(yusugi.Id, yusugi);
        s.Players[0].ProclaimUsedTurn = s.TurnNumber; // Proclaim already used this turn: only Yusugi's own exception keeps the window open to him
        if (alreadyProclaimed) s.Players[0].ProclaimedCardId = yusugi.Id;
        s.PendingChoice = new("proclaim", 0, yusugi.Id, "After entry: Proclaim for Honor or pass.");
        return (MatchEngine.FromTestState(s), yusugi.Id);
    }

    private static void Scenarios()
    {
        var (fresh, _) = Window(alreadyProclaimed: false);
        var offered = fresh.QueryActions(0).Where(a => a.Type == "proclaim").ToArray();
        Check(offered.Length == 1 && offered[0].Enabled, "Yusugi is offered his Proclaim after a Proclaim this turn (his printed exception)");
        Check(fresh.Execute(offered[0].Command!).Accepted, "and may take it");

        var (spent, _) = Window(alreadyProclaimed: true);
        var again = spent.QueryActions(0).Where(a => a.Type == "proclaim").ToArray();
        Check(again.Length == 1 && !again[0].Enabled && again[0].Reason is { Length: > 0 }, "once this entry's Proclaim is taken it is offered no more (disabled, with a reason)");
        var before = spent.Version;
        Check(!spent.Execute(new("proclaim", 0, spent.Version)).Accepted && spent.Version == before, "and a second Proclaim for the same entry is refused without changing anything");
    }
}
