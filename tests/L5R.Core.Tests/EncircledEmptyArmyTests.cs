using L5R.Core;

// Issue #642 (found by the bot soak): Encircled Terrain asked a player to "choose one of your Personalities at this battlefield to keep" even when he had none there, a prompt
// with no possible answer, so the whole match stalled. A player with no Personality at the battlefield now has nothing to select and his turn passes by itself.
internal static class EncircledEmptyArmyTests
{
    private const string Terrain = "oracle-l5r-2332"; // Encircled
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run(TestRunner runner) => runner.Run("encircled-empty-army", [
        ("Encircled Terrain: a player with no Personality at the battlefield is never asked to keep one, whichever side selects first; the other still keeps one", Scenarios),
    ]);

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, int? province = null)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"enc-{n}")) n++;
        var c = new CardInstance { Id = $"enc-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        return c;
    }

    private static void Scenarios()
    {
        foreach (var emptySide in new[] { 0, 1 })
            foreach (var firstSelect in new[] { "attacker", "defender" })
            {
                var s = MatchEngine.CreateEconomyScenario().State;
                s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
                foreach (var c in s.Cards.Values.Where(c => c.Zone == Zone.Battlefield).ToArray()) s.Cards.Remove(c.Id);
                Put(s, Terrain, 0, Zone.Battlefield, 0); // controlled by the Attacker
                var keep = 1 - emptySide;
                var kept = Put(s, "training-crane-veteran", keep, Zone.Battlefield, 0);
                var other = Put(s, "training-crane-samurai", keep, Zone.Battlefield, 0);
                var e = MatchEngine.FromTestState(s);
                for (var i = 0; i < 2; i++) Check(e.Execute(new("pass", e.State.PriorityPlayerId, e.Version)).Accepted, "pass into the battle resolution window");
                Check(e.State.PendingChoice?.Type == "encircledOrder", $"the Terrain's controller orders the selections ({emptySide}/{firstSelect}): " + e.State.PendingChoice?.Type);
                Check(e.Execute(new("encircledOrder", 0, e.Version, Choice: firstSelect)).Accepted, "ordering accepted");
                // Whoever has no Personality here is skipped; the other is asked, once.
                Check(e.State.PendingChoice is { Type: "encircledSelect" } p && p.PlayerId == keep, $"only the player with Personalities is asked ({emptySide}/{firstSelect}): {e.State.PendingChoice?.Type} for {e.State.PendingChoice?.PlayerId}");
                Check(e.Execute(new("encircledSelect", keep, e.Version, TargetId: kept.Id)).Accepted, "the keeper answers");
                Check(e.State.PendingChoice?.Type != "encircledSelect", "no second, unanswerable prompt follows");
                Check(e.State.Cards[other.Id].Zone == Zone.Home, "every other Personality was still sent home");
            }
    }
}
