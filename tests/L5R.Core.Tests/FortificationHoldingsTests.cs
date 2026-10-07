using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Primitive P15 (issue #719, FortificationHoldings.cs): Fortification Holdings enter straightened, guard one Province and go with it. The keyword is granted on the fixture card (no card is wired yet).
internal static class FortificationHoldingsTests
{
    public static void Run(TestRunner runner) => runner.Run("fortification-holdings", [
        ("A Fortification Holding bought from a province enters straightened and guards that province; an ordinary Holding still enters bowed", PurchaseStraightened),
        ("Destroying the province discards the Fortification (and its attachments) and clears it from the view; other provinces' Fortifications stay", DestroyDiscards),
        ("A Fortification that did not enter from a province asks its owner which province it guards; one remaining province is chosen at once", NoProvinceAsks),
        ("The record is dropped when the Holding leaves play, survives a saved-state round trip, and is omitted from the saved state when unused", RecordLifecycle),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    private static CardInstance Fortification(MatchState s, Zone zone, bool keyword = true)
    {
        var card = Batch336Support.Add(s, "training-marketplace", 0, zone);
        if (keyword) card.GrantedKeywords.Add(new("Fortification", "test-fixture", GrantDuration.Permanent));
        return card;
    }

    private static CardInstance InProvince(MatchState s, int index, bool keyword)
    {
        var province = s.Players[0].Provinces[index];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; province.CardId = null; }
        var holding = Fortification(s, Zone.Province, keyword); holding.FaceUp = true; holding.ProvinceIndex = index; province.CardId = holding.Id;
        return holding;
    }

    private static void Buy(MatchEngine e, string cardId)
    {
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == cardId);
        Accept(e, option.Command!);
    }

    private static void PurchaseStraightened()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var fort = InProvince(s, 1, true);
        var plain = InProvince(s, 2, false);
        var e = MatchEngine.FromTestState(s);
        Buy(e, fort.Id);
        Eq(e.State.Cards[fort.Id].Zone, Zone.Home, "it is in play");
        Check(!e.State.Cards[fort.Id].Bowed, "straightened");
        Eq(e.FortificationProvinceForTest(fort.Id), 1, "guarding the province it came from");
        Eq(e.GetView(0).Players[0].Provinces[1].FortificationIds?.Single(), fort.Id, "the view names it on that province");
        Check(e.GetView(0).Players[0].Provinces[0].FortificationIds is null, "other provinces show none");
        Buy(e, plain.Id);
        Check(e.State.Cards[plain.Id].Bowed, "an ordinary Holding still enters bowed");
        Eq(e.FortificationProvinceForTest(plain.Id), null, "and guards nothing");
    }

    private static void DestroyDiscards()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var a = InProvince(s, 1, true); var b = InProvince(s, 2, true);
        var attachment = Batch336Support.Add(s, "training-item", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Buy(e, a.Id); Buy(e, b.Id);
        e = Batch336Support.Edit(e, st => { st.Cards[attachment.Id].Zone = Zone.Attached; st.Cards[attachment.Id].AttachedTo = a.Id; });
        e.DestroyProvinceForTestP15(0, 1);
        Eq(e.State.Cards[a.Id].Zone, Zone.DynastyDiscard, "the guard went to the Dynasty discard");
        Eq(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, "its attachment to the Fate discard");
        Eq(e.FortificationProvinceForTest(a.Id), null, "no record");
        Eq(e.State.Cards[b.Id].Zone, Zone.Home, "the other province's guard stays");
        Eq(e.FortificationProvinceForTest(b.Id), 2, "and still guards");
        Check(e.GetView(0).Players[0].Provinces[1].FortificationIds is null, "the destroyed province lists none");
    }

    private static void NoProvinceAsks()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var holding = Fortification(s, Zone.Home, true); holding.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        e.SweepFortificationsForTest();
        Check(!e.State.Cards[holding.Id].Bowed, "straightened");
        Eq(e.State.PendingChoice?.Type, "eventChoice", "the owner is asked");
        var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Eq(options.Length, 4, "one option per remaining province");
        Accept(e, options.Single(o => o.Label.EndsWith("province 3")).Command!);
        Eq(e.FortificationProvinceForTest(holding.Id), 2, "the chosen province");
        Check(e.State.PendingChoice is null, "the choice closed");

        var t = Batch336Support.Table(Phase.Action, 0);
        foreach (var index in new[] { 0, 1, 2 }) t.Players[0].Provinces[index].Destroyed = true;
        var lone = Fortification(t, Zone.Home, true);
        var e2 = MatchEngine.FromTestState(t);
        e2.SweepFortificationsForTest();
        Eq(e2.FortificationProvinceForTest(lone.Id), 3, "one remaining province is chosen at once");
        Check(e2.State.PendingChoice is null, "no question");
    }

    private static void RecordLifecycle()
    {
        var plain = Batch336Support.Table(Phase.Action, 0);
        Check(!JsonSerializer.Serialize(MatchEngine.FromTestState(plain).State).Contains("Fortifications"), "an unused match keeps its saved state");
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        var fort = InProvince(s, 1, true);
        var e = MatchEngine.FromTestState(s);
        Buy(e, fort.Id);
        var saved = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Eq(saved.FortificationProvinceForTest(fort.Id), 1, "the record survives a save");
        var gone = Batch336Support.Edit(e, st => st.Cards[fort.Id].Zone = Zone.DynastyDiscard);
        gone.SweepFortificationsForTest();
        Eq(gone.FortificationProvinceForTest(fort.Id), null, "dropped once the Holding left play");
    }
}
