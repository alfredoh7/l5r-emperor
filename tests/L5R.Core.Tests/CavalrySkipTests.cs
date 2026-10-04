using static RegressionAssertions;
using L5R.Core;

// Issue #627: a Cavalry Maneuvers segment (attackers' or defenders') in which the player to act has no Cavalry unit he can assign, and no
// other usable action, is passed for him (the empty assignment) -- he is never offered "Attack ... 0 Cavalry units".
internal static class CavalrySkipTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Neither player has Cavalry: after Infantry Maneuvers the phase goes straight to Battle Selection, Cavalry Maneuvers is skipped, and a cavalrySkipped event says so", NeitherHasCavalry),
            ("Only the attacker has a Cavalry unit: he is offered it, and the defender (none) is skipped in Cavalry Defense", OnlyAttackerHasCavalry),
            ("Only the defender has a Cavalry unit: the attacker's Cavalry segment is skipped and the defender's is offered", OnlyDefenderHasCavalry),
            ("A player with no Cavalry unit is offered no zero-unit assignment (the engine offers no other action in a Cavalry segment); a Cavalry owner is offered his units", OtherActionKeepsSegment),
            ("A bowed Cavalry unit alone cannot be assigned, so there is nothing to assign and the segment is skipped", BowedCavalryAloneIsSkipped),
        ];
        runner.Run("cavalry-skip", tests);
    }

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false) => Batch336Support.Add(s, def, owner, zone, bowed, null, null);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string? message = null) { if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new Exception($"{message ?? "Values differ"}: expected {expected}; got {actual}"); }

    // Player 0 attacks; the Declaration Segment is taken and the engine is left in Infantry Maneuvers.
    private static MatchEngine Declared(Action<MatchState> arrange)
    {
        var s = MatchEngine.CreateStarterMatch().State; s.Phase = Phase.AttackDeclaration; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("declareAttack", 0, e.Version));
        return e;
    }
    private static void PassInfantry(MatchEngine e)
    {
        Eq(e.State.Phase, Phase.InfantryDeclaration);
        Accept(e, new("pass", 0, e.Version));
        Eq(e.State.Phase, Phase.DefenseAssignment);
        Accept(e, new("pass", 1, e.Version));
    }
    private static bool Skipped(MatchEngine e, int player) => e.GetView(0).Events.Concat(e.GetView(1).Events).Any(ev => ev.Type == "cavalrySkipped" && ev.Message.Contains(e.State.Players[player].Name));
    private static bool NoZeroUnitOption(MatchEngine e, int player) => !e.QueryActions(player).Any(a => a.Type is "attack" or "assignDefense" && a.Label.Contains(" 0 Cavalry units"));

    private static void NeitherHasCavalry()
    {
        var e = Declared(s => { Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        PassInfantry(e);
        Eq(e.State.Phase, Phase.BattleSelection, "no Cavalry on either side: Cavalry Maneuvers is not presented");
        Check(Skipped(e, 0) && Skipped(e, 1), "a cavalrySkipped event for each player");
        Eq(string.Join(",", e.State.UnresolvedBattlefields), "0,1,2,3", "the battlefields declared earlier are untouched by the skip");
    }

    private static void OnlyAttackerHasCavalry()
    {
        CardInstance cavalry = null!;
        var e = Declared(s => { Add(s, "training-crane-samurai", 0, Zone.Home); cavalry = Add(s, "training-cavalry", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        PassInfantry(e);
        Eq(e.State.Phase, Phase.CavalryDeclaration); Eq(e.State.PriorityPlayerId, 0);
        var offered = e.QueryActions(0).Where(a => a.Type == "attack" && a.Enabled).ToArray();
        Check(offered.Length > 0 && offered.All(a => a.Command!.Assignments!.Keys.SequenceEqual([cavalry.Id])), "the attacker is offered exactly his Cavalry unit");
        Check(!Skipped(e, 0), "the attacker is not skipped");
        Accept(e, offered.First(a => a.Command!.Assignments![cavalry.Id] == 1).Command!);
        Eq(e.State.Cards[cavalry.Id].ProvinceIndex, 1);
        Eq(e.State.Phase, Phase.BattleSelection, "the defender has no Cavalry unit: his Cavalry Defense is skipped");
        Check(Skipped(e, 1) && !Skipped(e, 0), "only the defender was skipped");
    }

    private static void OnlyDefenderHasCavalry()
    {
        CardInstance cavalry = null!;
        var e = Declared(s => { Add(s, "training-crane-samurai", 0, Zone.Home); cavalry = Add(s, "training-cavalry", 1, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        PassInfantry(e);
        Eq(e.State.Phase, Phase.CavalryDefense, "the attacker's Cavalry segment is skipped; the defender's is next"); Eq(e.State.PriorityPlayerId, 1);
        Check(Skipped(e, 0) && !Skipped(e, 1), "only the attacker was skipped");
        var offered = e.QueryActions(1).Where(a => a.Type == "assignDefense" && a.Enabled).ToArray();
        Check(offered.Length > 0 && offered.All(a => a.Command!.Assignments!.Keys.SequenceEqual([cavalry.Id])), "the defender is offered exactly his Cavalry unit");
        Accept(e, offered.First().Command!);
        Eq(e.State.Cards[cavalry.Id].Zone, Zone.Battlefield);
        Eq(e.State.Phase, Phase.BattleSelection);
    }

    private static void OtherActionKeepsSegment()
    {
        // The engine offers no usable action in a Cavalry segment besides the assignment and the pass (Open/Battle abilities and Order Seppuku
        // are Action/Battle Phase actions), so the "other action" that keeps a segment is an open decision. FromTestState does not run the skip,
        // so the segment can be inspected as a state: a player with no Cavalry unit is offered the pass and nothing that lists 0 units.
        foreach (var phase in new[] { Phase.CavalryDeclaration, Phase.CavalryDefense })
        {
            var s = MatchEngine.CreateStarterMatch().State; s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = phase == Phase.CavalryDeclaration ? 0 : 1; s.UnresolvedBattlefields = [0, 1, 2, 3];
            Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var player = s.PriorityPlayerId;
            Eq(e.State.Phase, phase, "a state placed in the segment is not skipped by being loaded");
            Check(NoZeroUnitOption(e, player), "no 'Attack/Defend ... 0 Cavalry units' option for a player with no Cavalry unit");
            Check(!e.QueryActions(player).Any(a => a.Type is "attack" or "assignDefense"), "no assignment option at all");
            Check(e.QueryActions(player).Any(a => a.Type == "pass" && a.Enabled), "the pass is still offered");
        }
        // A player who has a Cavalry unit is offered units in the segment, never a zero-unit option, and keeps the segment.
        var cs = MatchEngine.CreateStarterMatch().State; cs.Phase = Phase.CavalryDeclaration; cs.ActivePlayerId = 0; cs.PriorityPlayerId = 0;
        Add(cs, "training-cavalry", 0, Zone.Home);
        var ce = MatchEngine.FromTestState(cs);
        Check(NoZeroUnitOption(ce, 0) && ce.QueryActions(0).Any(a => a.Type == "attack" && a.Enabled), "a Cavalry unit is offered, with no zero-unit option");
    }

    private static void BowedCavalryAloneIsSkipped()
    {
        var e = Declared(s => { Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-cavalry", 0, Zone.Home, bowed: true); Add(s, "training-lion-samurai", 1, Zone.Home); Add(s, "training-cavalry", 1, Zone.Home, bowed: true); });
        PassInfantry(e);
        Eq(e.State.Phase, Phase.BattleSelection, "a bowed Cavalry unit cannot be assigned, so there is nothing to assign");
        Check(Skipped(e, 0) && Skipped(e, 1), "both players' segments were skipped");
    }
}
