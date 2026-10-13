using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// REPRODUCTION, not a fix (issue #913, validation-report finding EE-Areas-of-the-game-047). Comprehensive Rules, "Battlefields": "If multiple battlefields are associated with a single
// province, they are treated as battlefields arranged in a line between the flanking provinces ... each of the multiple battlefields is adjacent to its neighbor(s) within that province."
// These tests assert what the engine does TODAY (AdjacentBattlefields, EmperorRulingR1.cs) so the suite stays green; each comment states the rule's answer. Real card: Sea Troll Predator
// (oracle-l5r-6522, "Move ... to the same adjacent unresolved battlefield"). Nothing here is a ruling; change it together with EmperorRulingR1Tests.NoAdjacentBattlefields if the topology is changed.
internal static class AdjacencyProbeTests
{
    private const string Predator = "oracle-l5r-6522", Move = "predator-move";
    private const int Extra = MatchEngine.FirstAdditionalBattlefield;

    public static void Run(TestRunner runner) => runner.Run("adjacency-probe", [("Two battlefields at one province (Two Fronts): Sea Troll Predator's offered destinations today, against the Comprehensive Rules' line", TwoFrontsPredator)]);

    private static void TwoFrontsPredator()
    {
        // Defender (player 0) line 0-1-2-3, Two Fronts at province 1: battlefields 1 and Extra both at province 1. Predator and his target fight at battlefield 1.
        var s = Table(); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = 1;
        s.R1.Additional[Extra] = 1; s.UnresolvedBattlefields = [0, 1, 2, 3, Extra];
        var p = Batch336Support.Add(s, Predator, 1, Zone.Battlefield, province: 1);
        Batch336Support.Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        var offered = Enabled(e, 1, Move, p.Id).Provinces!.OrderBy(x => x).ToArray();
        Console.WriteLine("[probe] Predator at battlefield 1, extra at province 1, unresolved 0,1,2,3,Extra -> offered: " + string.Join(",", offered));
        // Today: only the battlefields of the neighbouring provinces 0 and 2 (and 3 is not adjacent). The sibling battlefield at the same province is never offered.
        Eq(string.Join(",", offered), "0,2", "today: 0 and 2 only");
        // Rule: the two battlefields at province 1 are in a line between provinces 0 and 2, each adjacent to its neighbour within the province, so battlefield Extra is ALSO a legal destination
        // (and only ONE of 0 / 2 is next to battlefield 1, the other next to Extra).
        Eq(string.Join(",", e.AdjacentBattlefieldsForTest(0)), $"1,{Extra}", "today: province 0's battlefield touches BOTH battlefields at province 1; the rule gives it only the nearer end of the line");
        Eq(string.Join(",", e.AdjacentBattlefieldsForTest(Extra)), "0,2", "today: Extra touches both flanks and not its sibling; the rule has it touch its sibling and one flank only");
    }
}
