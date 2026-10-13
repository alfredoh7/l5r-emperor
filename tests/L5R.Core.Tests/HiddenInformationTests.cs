using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// EE-STATE-014 / EE-Areas-of-the-game-071: no seat's projection carries a current deck count, not even the deck owner's.
// EE-Areas-of-the-game-072: a rejected command must not reveal what a hidden card is or can do.
internal static class HiddenInformationTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Neither seat's view, events or look/search deck choices carry a current Dynasty/Fate deck count: the serialized views are identical whatever the deck sizes", NoDeckCountsAnywhere),
            ("A request naming a hidden card (own deck, own face-down province, opponent's hand, unknown id) is refused with one generic error and an unchanged version, whether or not the card has the ability", HiddenSourceRefusalIsGeneric),
            ("The generic refusal does not hide what a visible card can do: a visible card without the ability is still told so. (Legitimate private looks are covered by the deck-look suite.)", VisibleSourceStillInformative),
        ];
        runner.Run("hidden-information", tests);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }

    private static MatchState Action()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        return s;
    }

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, int? province = null, bool faceUp = false)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"test-{n}")) n++;
        var c = new CardInstance { Id = $"test-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp || zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck or Zone.Province), ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }

    private static void NoDeckCountsAnywhere()
    {
        var small = MatchEngine.CreateEconomyScenario(); var large = MatchEngine.CreateEconomyScenario();
        foreach (var owner in new[] { 0, 1 })
            for (var i = 0; i < 9; i++) { Put(large.State, "training-armor", owner, Zone.FateDeck); Put(large.State, "training-farm", owner, Zone.DynastyDeck); }
        foreach (var seat in new[] { 0, 1 })
        {
            var a = Json(small.GetView(seat)); var b = Json(large.GetView(seat));
            Check(a == b, $"Seat {seat}'s view depends on the deck sizes");
            Check(!a.Contains("deckcount", StringComparison.OrdinalIgnoreCase) && !a.Contains("cardsremaining", StringComparison.OrdinalIgnoreCase), $"Seat {seat}'s view still names a deck count");
        }
        var published = MatchEngine.CreateStarterMatch();
        foreach (var seat in new[] { 0, 1 })
        {
            var json = Json(published.GetView(seat));
            Check(!json.Contains("DeckCount", StringComparison.OrdinalIgnoreCase) && !json.Contains("deckCount", StringComparison.Ordinal), "The opening view names a deck count");
        }
    }

    private const string Honor = "shunya-honor";

    private static CommandResult Try(MatchEngine e, string cardId) => e.Execute(new("useAbility", 0, e.Version, cardId, AbilityId: Honor));

    private static void HiddenSourceRefusalIsGeneric()
    {
        var s = Action();
        var withDeck = Put(s, "training-honor-personality", 0, Zone.FateDeck); var withoutDeck = Put(s, "training-armor", 0, Zone.FateDeck);
        var withDynasty = Put(s, "training-honor-personality", 0, Zone.DynastyDeck); var withoutDynasty = Put(s, "training-farm", 0, Zone.DynastyDeck);
        var withProvince = Put(s, "training-honor-personality", 0, Zone.Province, 3); var withoutProvince = Put(s, "training-farm", 0, Zone.Province, 3);
        var withTheirHand = Put(s, "training-honor-personality", 1, Zone.Hand); var withoutTheirHand = Put(s, "training-armor", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var pairs = new (string With, string Without)[] { (withDeck.Id, withoutDeck.Id), (withDynasty.Id, withoutDynasty.Id), (withProvince.Id, withoutProvince.Id), (withTheirHand.Id, withoutTheirHand.Id), ("no-such-card", "another-missing-card") };
        string? generic = null;
        foreach (var (with, without) in pairs)
        {
            var before = e.Version;
            var a = Try(e, with); var b = Try(e, without);
            Check(!a.Accepted && !b.Accepted, $"{with}/{without} must both be refused");
            Check(a.Error == b.Error, $"The refusal differs by hidden ability: '{a.Error}' vs '{b.Error}' ({with})");
            Check(a.Version == b.Version && a.Version == before && e.Version == before, "A refusal never changes the version");
            generic ??= a.Error;
            Check(a.Error == generic, $"The hidden-card refusal is one fixed message; got '{a.Error}' and '{generic}'");
        }
        Check(generic is not null && !generic.Contains("ability", StringComparison.OrdinalIgnoreCase) && !generic.Contains("zone", StringComparison.OrdinalIgnoreCase), $"The generic error names no ability or zone: '{generic}'");
    }

    private static void VisibleSourceStillInformative()
    {
        var s = Action();
        var withHome = Put(s, "training-honor-personality", 0, Zone.Home); var withoutHome = Put(s, "training-armor", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var a = Try(e, withHome.Id); var b = Try(e, withoutHome.Id);
        Check(b.Error is not null && !b.Accepted, "The visible card without the ability is refused");
        Check(b.Error != null && b.Error.Contains("does not provide", StringComparison.OrdinalIgnoreCase), $"A visible card is still told why it cannot: '{b.Error}'");
        Check(a.Error != b.Error || a.Accepted, "A visible card's own legality is not flattened into the hidden-card message");
    }
}
