using L5R.Core;

// Audit: every real Personality in the catalogue, sitting unbowed at home, can be assigned to attack in the Maneuvers segment its Cavalry keyword puts it in -- unless its own printed text
// says it may not be assigned / may not attack. Doji Atsumichi was reported as never assignable in the Crane-vs-Lion practice match.
internal static class AttackAssignmentAuditTests
{
    public static void Run(TestRunner runner) => runner.Run("attack-assignment-audit", [
        ("The harness assigns a plain Personality (control)", Control),
        ("Doji Atsumichi can be assigned to attack", Atsumichi),
        ("Atsumichi is offered in Infantry Maneuvers and again in Cavalry Maneuvers (declare, Infantry, Defense, Cavalry) in the published Crane-vs-Lion match", RealFlow),
        ("Every Personality in the catalogue can be assigned to attack unless its text says otherwise", EveryPersonality),
    ]);

    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);

    private static string? TryAssign(string definitionId)
    {
        var s = MatchEngine.CreateStarterMatch().State;
        var card = Batch336Support.Add(s, definitionId, 0, Zone.Home);
        s.Phase = Phase.InfantryDeclaration; // a Cavalry unit may assign in either Maneuvers segment, an Infantry unit only here s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        s.UnresolvedBattlefields = s.Players[1].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToList();
        var e = MatchEngine.FromTestState(s);
        var offered = e.QueryActions(0).Any(a => a.Type == "attack" && a.Enabled);
        var result = e.Execute(new GameCommand("attack", 0, e.Version, Assignments: new() { [card.Id] = 0 }));
        if (!result.Accepted) return (result.Error ?? "rejected") + (offered ? "" : " [attack not offered]");
        return offered ? null : "assignment accepted but no enabled attack action was offered";
    }

    private static void Control() { var r = TryAssign("training-crane-samurai"); Check(r is null, "control failed: " + r); }
    private static void Atsumichi() { var r = TryAssign("oracle-l5r-2060"); Check(r is null, "Doji Atsumichi: " + r); }

    private static void RealFlow()
    {
        var s = MatchEngine.CreatePublishedMatch().State;
        var card = Batch336Support.Add(s, "oracle-l5r-2060", 0, Zone.Home);
        s.Phase = Phase.AttackDeclaration; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, new GameCommand("declareAttack", 0, e.Version));
        Check(e.State.Phase == Phase.InfantryDeclaration, "infantry: " + e.State.Phase);
        Check(e.QueryActions(0).Any(a => a.Type == "attack" && a.Enabled && a.TargetIds!.Contains(card.Id)), "Atsumichi (Cavalry) is offered in Infantry Maneuvers");
        RegressionAssertions.Accept(e, new GameCommand("pass", 0, e.Version));
        Check(e.State.Phase == Phase.DefenseAssignment, "defense: " + e.State.Phase);
        RegressionAssertions.Accept(e, new GameCommand("pass", 1, e.Version));
        Check(e.State.Phase == Phase.CavalryDeclaration, "cavalry: " + e.State.Phase);
        var offered = e.QueryActions(0).Where(a => a.Type == "attack" && a.Enabled).ToArray();
        Check(offered.Length > 0 && offered.All(a => a.Command!.Assignments!.ContainsKey(card.Id)), "attack offered with Atsumichi: " + string.Join(";", e.QueryActions(0).Select(a => a.Type + ":" + a.Label + ":" + a.Enabled)));
        RegressionAssertions.Accept(e, offered[0].Command!);
        Check(e.State.Cards[card.Id].Zone == Zone.Battlefield, "Atsumichi is on a battlefield");
    }

    private static void EveryPersonality()
    {
        var failures = new List<string>();
        foreach (var c in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Type == CardType.Personality && !c.Text.Contains("may not assign", StringComparison.OrdinalIgnoreCase)))
        {
            var r = TryAssign(c.Id);
            if (r is not null) failures.Add($"{c.Name} ({c.Id}): {r}");
        }
        Check(failures.Count == 0, failures.Count + " Personality(ies) cannot be assigned:\n" + string.Join("\n", failures));
    }
}
