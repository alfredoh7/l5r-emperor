using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Battlefield adjacency (validation-report finding EE-Areas-of-the-game-047; Mendon's ruling 2026-10-13, RULINGS.md R-081). Comprehensive Rules, "Battlefields": "If multiple battlefields are associated
// with a single province, they are treated as battlefields arranged in a line between the flanking provinces ... each of the multiple battlefields is adjacent to its neighbor(s) within that province",
// and a battlefield with no province is adjacent to nothing. Order within a province (Mendon): the primary battlefield is leftmost, additional ones follow in creation order (the last created is
// rightmost, nearest the right province). Real card: Sea Troll Predator (oracle-l5r-6522, "Move ... to the same adjacent unresolved battlefield").
internal static class AdjacencyProbeTests
{
    private const string Predator = "oracle-l5r-6522", Move = "predator-move";
    private const int Extra = MatchEngine.FirstAdditionalBattlefield;

    public static void Run(TestRunner runner) => runner.Run("adjacency-line", [
        ("Two Fronts: Sea Troll Predator is offered the sibling battlefield and the near flank only", TwoFrontsPredator),
        ("Siblings at one province are adjacent to each other", SiblingsAdjacent),
        ("Flanking provinces touch only the nearer end of the line", FlanksTouchNearerEnd),
        ("Three battlefields at one province: the middle touches only its two neighbours", ThreeInALine),
        ("A destroyed province slides the line together", DestroyedProvinceSlides),
        ("A battlefield with no province is adjacent to nothing, and nothing is adjacent to it", NoProvinceAdjacentToNothing),
    ]);

    // Defender (player 0) line 0-1-2-3, battlefields 0..3 plus one additional battlefield per entry of `additionalProvinces` (Extra, Extra + 1, ...; -1 = no province).
    private static MatchEngine Engine(int[] additionalProvinces, int current = 1, int? destroyed = null)
    {
        var s = Table(); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = current;
        var unresolved = new List<int> { 0, 1, 2, 3 };
        for (var i = 0; i < additionalProvinces.Length; i++) { s.R1.Additional[Extra + i] = additionalProvinces[i]; unresolved.Add(Extra + i); }
        s.UnresolvedBattlefields = unresolved;
        if (destroyed is int d) s.Players[0].Provinces[d].Destroyed = true;
        return MatchEngine.FromTestState(s);
    }

    private static string Adjacent(MatchEngine e, int battlefield) => string.Join(",", e.AdjacentBattlefieldsForTest(battlefield));

    private static void TwoFrontsPredator()
    {
        // Two Fronts at province 1: battlefields 1 and Extra both at province 1. Predator and his target fight at battlefield 1.
        var s = Table(); s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = 1;
        s.R1.Additional[Extra] = 1; s.UnresolvedBattlefields = [0, 1, 2, 3, Extra];
        var p = Batch336Support.Add(s, Predator, 1, Zone.Battlefield, province: 1);
        Batch336Support.Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        var offered = Enabled(e, 1, Move, p.Id).Provinces!.OrderBy(x => x).ToArray();
        Eq(string.Join(",", offered), $"0,{Extra}", "battlefield 1 is next to province 0's battlefield and to its sibling, not to province 2's (the far end of the line)");
    }

    private static void SiblingsAdjacent()
    {
        var e = Engine([1]);
        Eq(Adjacent(e, 1), $"0,{Extra}", "the primary battlefield touches the left province and its sibling");
        Eq(Adjacent(e, Extra), "1,2", "the additional battlefield touches its sibling and the right province");
    }

    private static void FlanksTouchNearerEnd()
    {
        var e = Engine([1]);
        Eq(Adjacent(e, 0), "1", "the left province's battlefield touches the leftmost battlefield of the line only");
        Eq(Adjacent(e, 2), $"{Extra},3", "the right province's battlefield touches the rightmost battlefield of the line only");
    }

    private static void ThreeInALine()
    {
        var e = Engine([1, 1]);
        Eq(Adjacent(e, 1), $"0,{Extra}", "leftmost: the left province and the next battlefield");
        Eq(Adjacent(e, Extra), $"1,{Extra + 1}", "middle (first additional, creation order): only its two neighbours");
        Eq(Adjacent(e, Extra + 1), $"{Extra},2", "rightmost (last created): its neighbour and the right province");
        Eq(Adjacent(e, 0), "1", "the left flank touches only the primary");
        Eq(Adjacent(e, 2), $"{Extra + 1},3", "the right flank touches only the last created");
    }

    private static void DestroyedProvinceSlides()
    {
        // Province 1 destroyed: provinces 0 and 2 slide together. A second front at province 2 makes the line [2, Extra]: 0 touches 2, Extra touches 3.
        var e = Engine([2], current: 0, destroyed: 1);
        Eq(Adjacent(e, 0), "2", "province 0 is next to province 2's leftmost battlefield");
        Eq(Adjacent(e, 2), $"0,{Extra}", "province 2's primary touches the slid-together province 0 and its sibling");
        Eq(Adjacent(e, Extra), "2,3", "and the sibling touches province 3");
        Eq(Adjacent(e, 3), $"{Extra}", "province 3 touches the rightmost of the line");
        // A battlefield at the destroyed province (primary or additional) is without a province, and the line slides over it.
        var f = Engine([1], current: 0, destroyed: 1);
        Eq(Adjacent(f, 1), "", "the destroyed province's primary battlefield has no province");
        Eq(Adjacent(f, Extra), "", "an additional battlefield at the destroyed province has none either");
        Eq(Adjacent(f, 0), "2", "and the line slides over both");
    }

    private static void NoProvinceAdjacentToNothing()
    {
        var e = Engine([-1, 2]);
        Eq(Adjacent(e, Extra), "", "a battlefield without a province touches nothing");
        Batch336Support.Check(!Enumerable.Range(0, 4).Append(Extra + 1).Any(b => e.AdjacentBattlefieldsForTest(b).Contains(Extra)), "and nothing touches it");
        Eq(Adjacent(e, 1), "0,2", "the neighbours are unaffected by it");
    }
}
