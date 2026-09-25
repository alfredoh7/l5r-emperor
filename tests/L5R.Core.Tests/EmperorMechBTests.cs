using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #501, mechanics group B (Mendon's items 6-10 and the engine side of item 32): control transfer and the view of a copied card. Every position is built explicitly and driven only through
// QueryActions and the commands it offers (the primitives are in src/L5R.Core/EmperorMechB*.cs).
internal static class EmperorMechBTests
{
    private const string Copyist = "training-copyist";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static CardView View(MatchEngine e, int viewer, string id) =>
        e.GetView(viewer).Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame)).Single(c => c.Id == id);

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-B", [
        // Item 32 (engine side): the view of a copy.
        ("Copies in the view (item 32): a created copy says it is a copy, names the original card and its definition (whose scan the client shows) and its conditions, in both players' views; an ordinary card says none of it", CopyView),
        ("Copies in the view (item 32): a copy of a copy names the first original; a copying card's own conditions (MarkAsCopy) come first and a repeated one is kept once; a card created until the battle ends says so", CopyViewConditions),
    ]);

    // ---- item 32: the view of a copy ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CopyView()
    {
        var s = Table(Phase.Action, 0);
        var maker = Add(s, Copyist, 0, Zone.Home);
        var foe = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-personality", maker.Id, foe.Id));
        var copy = e.State.Cards.Values.Single(c => c.CreatedCopy);
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = View(e, viewer, copy.Id);
            Check(view.IsCopy, "the copy says it is a copy");
            Eq(view.CopyOfCardId, foe.Id, "the original card");
            Eq(view.CopyOfDefinitionId, "training-lion-veteran", "the original's definition: its scan is the one shown");
            Eq(view.CopyConditions, "A created copy: it is removed from the game if it leaves play.", "the engine's own condition on a created copy");
            var original = View(e, viewer, foe.Id);
            Check(!original.IsCopy && original.CopyOfCardId is null && original.CopyOfDefinitionId is null && original.CopyConditions is null, "the original is not a copy");
            Eq(original.RealOwnerId, (int?)null, "an ordinary card is controlled by its owner");
        }
    }

    private static void CopyViewConditions()
    {
        var s = Table(Phase.Action, 0);
        var maker = Add(s, Copyist, 0, Zone.Home);
        var foe = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-personality", maker.Id, foe.Id));
        var first = e.State.Cards.Values.Single(c => c.CreatedCopy);
        var again = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[maker.Id].AbilityUses.Clear(); });
        Accept(again, Offer(again, 0, "training-copy-personality", maker.Id, first.Id));
        var second = again.State.Cards.Values.Single(c => c.CreatedCopy && c.Id != first.Id);
        Eq(View(again, 1, second.Id).CopyOfCardId, foe.Id, "a copy of a copy shows the first original");
        Eq(View(again, 1, second.Id).CopyOfDefinitionId, "training-lion-veteran");
        // A copying card's own condition (the seam is what a copying card calls), then the engine's; a repeated condition is kept once.
        again.MarkAsCopyForTest(second.Id, first.Id, "Removed from the game when your next turn begins.", "Removed from the game when your next turn begins.");
        Eq(View(again, 0, second.Id).CopyConditions, "Removed from the game when your next turn begins. A created copy: it is removed from the game if it leaves play.");
        // An ordinary card a copying card marks is a copy too, with only the copying card's conditions.
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        e2.MarkAsCopyForTest(plain.Id, foe.Id, "Removed from the game when the turn ends.");
        var view = View(e2, 1, plain.Id);
        Check(view.IsCopy && view.CopyOfCardId == foe.Id && view.CopyOfDefinitionId == "training-lion-veteran" && view.CopyConditions == "Removed from the game when the turn ends.", "a marked copy: " + view.CopyConditions);
        // A card created until the battle ends says so.
        var e3 = Edit(again, st => st.CreatedUntilBattleEndsIds.Add(first.Id));
        Eq(View(e3, 0, first.Id).CopyConditions, "Removed from the game when this battle ends. A created copy: it is removed from the game if it leaves play.");
    }
}
