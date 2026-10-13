using L5R.Core;

// Primitive P14 (issue #713, HonorModifiers.cs): the Honor-change modifier store and the EngineHooks.HonorChange channel. The test-only registrant is inert unless a test sets Mode.
internal static class HonorModifiersTests
{
    private static string Mode = "";

    public static void Run(TestRunner runner) => runner.Run("honor-modifiers", [
        ("A next-change modifier from your cards is consumed by the first change of your own card, not by a rulebook gain or the other player's card", NextChangeFromOwnCards),
        ("A not-proclaim next-change modifier leaves a Proclaim alone; a next-proclaim modifier sets the Proclaim gain to the stored Focus Value and spares other gains", ProclaimKinds),
        ("An each-gain reduction applies to both players when it says so, floors at 0 and is not consumed; no-gain and no-loss stop only their direction", EachAndNo),
        ("A modifier lapses after its turn; an unless-controls filter and the action-phase filter switch it off; an unknown filter never matches", FiltersAndExpiry),
        ("A registrant's redirect moves the change to the other player, and a match with no modifier serializes without the store", RedirectAndBaseline),
        ("A Dishonorable death's loss is the rulebook's: an own-card next-change modifier neither changes nor is consumed by it, and a real card effect still consumes it", DishonorableDeathIsRulebook),
        ("Without Shiro Chugo's mark the APPLIED-stage registrant leaves a change alone (his reduction itself stays covered by the existing Chugo suites)", ChugoThroughChannel),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks() =>
        EngineHooks.HonorChange.Register("TestP14Redirect", 19100, static context =>
            Mode == "redirect" && context.Stage == HonorChangeStage.Standing && context.PlayerId == 0 && context.Amount > 0 ? new HonorChangeResult(context.Amount, 1) : null);

    private static (MatchEngine Engine, CardInstance Mine, CardInstance Theirs) Table()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var mine = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var theirs = Batch336Support.Add(s, "training-crane-samurai", 1, Zone.Home);
        return (MatchEngine.FromTestState(s), mine, theirs);
    }

    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    private static void NextChangeFromOwnCards()
    {
        var (e, mine, theirs) = Table();
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NextChange, Delta = 1, Filters = [HonorModifierFilters.OwnCards, HonorModifierFilters.NotProclaim] });
        var start = Honor(e, 0);
        e.GainHonorForTest(0, 2, "rulebook", null);
        Eq(Honor(e, 0), start + 2, "a rulebook gain has no card: not modified");
        e.GainHonorForTest(0, 2, "other player's card", theirs.Id);
        Eq(Honor(e, 0), start + 4, "the other player's card is not 'your cards'");
        Eq(e.HonorModifierList.Count, 1, "still waiting");
        e.GainHonorForTest(0, 2, "own card", mine.Id);
        Eq(Honor(e, 0), start + 7, "the first own-card gain grows by 1");
        Eq(e.HonorModifierList.Count, 0, "consumed");
        e.GainHonorForTest(0, 2, "own card", mine.Id);
        Eq(Honor(e, 0), start + 9, "the second is unchanged");
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NextChange, Delta = 1, Filters = [HonorModifierFilters.OwnCards] });
        e.GainHonorForTest(0, -2, "own loss", mine.Id);
        Eq(Honor(e, 0), start + 6, "a loss grows by 1 as well (3 lost)");
    }

    private static void DishonorableDeathIsRulebook()
    {
        var (e, mine, theirs) = Table();
        mine.Dishonorable = true; // training-crane-samurai: a Personality with a printed Personal Honor
        var loss = CardCatalog.Get("training-crane-samurai").PersonalHonor;
        e.State.Players[0].Honor = 10;
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NextChange, Delta = 1, Filters = [HonorModifierFilters.OwnCards, HonorModifierFilters.NotProclaim] });
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NoLoss, Filters = [HonorModifierFilters.ActionEffects] });
        e.DestroyUnitsForTest(mine.Id);
        Eq(e.State.Cards[mine.Id].Zone, Zone.Dead, "he died");
        Eq(Honor(e, 0), 10 - loss, "the rulebook loss is his printed Personal Honor, unmodified");
        Eq(e.HonorModifierList.Count, 2, "neither modifier was consumed or applied");
        e.GainHonorForTest(0, -2, "own card effect", theirs.Id.Length > 0 ? mine.Id : null);
        Eq(e.HonorModifierList.Count, 1, "a real own-card effect consumes the next-change modifier");
        Eq(Honor(e, 0), 10 - loss - 3, "and its loss grew by 1");
    }

    private static void ProclaimKinds()
    {
        var (e, mine, _) = Table();
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NextChange, Delta = 1, Filters = [HonorModifierFilters.NotProclaim] });
        var start = Honor(e, 0);
        e.GainHonorForTest(0, 3, "Proclaim", mine.Id, proclaim: true);
        Eq(Honor(e, 0), start + 3, "a Proclaim does not use the not-proclaim modifier");
        Eq(e.HonorModifierList.Count, 1, "still waiting");
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.NextProclaim, SetTo = 5 });
        e.GainHonorForTest(0, 2, "ordinary gain", mine.Id);
        Eq(Honor(e, 0), start + 6, "an ordinary gain meets the next-change modifier only (+1), not the Proclaim one");
        e.GainHonorForTest(0, 3, "Proclaim", mine.Id, proclaim: true);
        Eq(Honor(e, 0), start + 11, "the Proclaim gain became the stored Focus Value 5");
        Eq(e.HonorModifierList.Count, 0, "both consumed");
    }

    private static void EachAndNo()
    {
        var (e, mine, theirs) = Table();
        e.AddHonorModifier(new() { OwnerPlayerId = 0, Kind = HonorModifierKinds.EachGain, Delta = -2 });
        var (a, b) = (Honor(e, 0), Honor(e, 1));
        e.GainHonorForTest(0, 3, "a", mine.Id); e.GainHonorForTest(1, 1, "b", theirs.Id); e.GainHonorForTest(0, 3, "c", mine.Id);
        Eq(Honor(e, 0), a + 2, "3-2 twice");
        Eq(Honor(e, 1), b, "1-2 floors at 0, applies to the other player too");
        e.GainHonorForTest(0, -3, "loss", mine.Id);
        Eq(Honor(e, 0), a - 1, "losses are untouched by an each-gain modifier");
        Eq(e.HonorModifierList.Count, 1, "not consumed");
        e.AddHonorModifier(new() { OwnerPlayerId = 1, AffectedPlayerId = 1, Kind = HonorModifierKinds.NoLoss });
        e.GainHonorForTest(1, -4, "loss", mine.Id); e.GainHonorForTest(1, 4, "gain", theirs.Id);
        Eq(Honor(e, 1), b + 2, "no-loss stops the loss; the gain is reduced by 2 (the each-gain still stands)");
        e.AddHonorModifier(new() { OwnerPlayerId = 1, AffectedPlayerId = 1, Kind = HonorModifierKinds.NoGain });
        e.GainHonorForTest(1, 4, "gain", theirs.Id);
        Eq(Honor(e, 1), b + 2, "no-gain stops it");
    }

    private static void FiltersAndExpiry()
    {
        var (e, mine, _) = Table();
        var turn = e.State.TurnNumber;
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.EachGain, Delta = 1, UntilTurn = turn - 1 });
        Eq(e.HonorModifierList.Count, 1);
        var start = Honor(e, 0);
        e.GainHonorForTest(0, 2, "late", mine.Id);
        Eq(Honor(e, 0), start + 2, "a lapsed modifier does nothing");
        Eq(e.HonorModifierList.Count, 0, "and is removed");
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.EachGain, Delta = 1, UntilTurn = turn, Filters = [HonorModifierFilters.UnlessControlsPrefix + "Ancestor"] });
        e.GainHonorForTest(0, 2, "no Ancestor", mine.Id);
        Eq(Honor(e, 0), start + 5, "without an Ancestor the modifier applies (+1)");
        var withAncestor = e.State;
        Batch336Support.Add(withAncestor, "training-ancestor", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(withAncestor);
        e3.GainHonorForTest(0, 2, "controls an Ancestor", mine.Id);
        Eq(Honor(e3, 0), start + 7, "unless-controls: the Ancestor switches it off");
        start += 1;
        e.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.EachGain, Delta = 1, Filters = ["no-such-filter"] });
        e.GainHonorForTest(0, 2, "unknown", mine.Id);
        Eq(Honor(e, 0), start + 7, "an unknown filter never matches (the Ancestor-less each-gain still adds 1)");
        var other = Batch336Support.Table(Phase.Battle, 0);
        var mine2 = Batch336Support.Add(other, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(other);
        e2.AddHonorModifier(new() { OwnerPlayerId = 0, AffectedPlayerId = 0, Kind = HonorModifierKinds.EachGain, Delta = 1, Filters = [HonorModifierFilters.ActionPhaseOnly] });
        var s2 = Honor(e2, 0);
        e2.GainHonorForTest(0, 2, "battle", mine2.Id);
        Eq(Honor(e2, 0), s2 + 2, "action-phase-only: not in a battle");
    }

    private static void RedirectAndBaseline()
    {
        var (e, mine, _) = Table();
        Check(!System.Text.Json.JsonSerializer.Serialize(e.State).Contains("HonorModifiers", StringComparison.Ordinal), "an unused store is omitted from the saved state");
        Mode = "redirect";
        try
        {
            var (a, b) = (Honor(e, 0), Honor(e, 1));
            e.GainHonorForTest(0, 2, "redirected", mine.Id);
            Eq(Honor(e, 0), a, "the owner gets nothing");
            Eq(Honor(e, 1), b + 2, "the other player takes the gain");
        }
        finally { Mode = ""; }
        e.AddHonorModifier(new() { OwnerPlayerId = 0, Kind = HonorModifierKinds.NextChange, Delta = 1 });
        var copy = MatchEngine.FromTestState(e.State);
        Eq(copy.HonorModifierList.Count, 1, "the store round-trips through the saved state");
    }

    private static void ChugoThroughChannel()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var mine = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var start = Honor(e, 0);
        e.GainHonorForTest(0, 3, "no action marked", mine.Id);
        Eq(Honor(e, 0), start + 3, "without Chugo's mark the change is untouched");
    }
}
