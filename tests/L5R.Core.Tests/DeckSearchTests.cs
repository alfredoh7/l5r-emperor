using static RegressionAssertions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

// Deck search primitive (issue #312): the shared "search a deck, select a legal card, show it, apply the effect,
// shuffle" flow, exercised through Ikoma Shizuka (two decks, a Region half and a Terrain half), Imperial Gift (an
// Event-driven search), the training fixtures for the criteria vocabulary and the optional flag, and the persisted
// deterministic shuffle. Every test is written to be able to fail: each asserts both what must happen and what must not.
internal static class DeckSearchTests
{
    private const string Shizuka = "oracle-l5r-10292";
    private const string ImperialGift = "oracle-l5r-3694";
    private const string FallowLands = "oracle-l5r-9865";
    private const string OtherRegion = "oracle-l5r-4777";
    private const string Terrain = "training-terrain";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Shizuka and Imperial Gift are published exactly as their scans print, wired to the search primitive", PublishedShape),
            ("The searcher's deck viewer groups the deck by card with counts and per-entry eligibility for each deck, sorted by type then name and never by deck order; nothing is listed before a deck is chosen", CandidateList),
            ("An ineligible pick, a card not in the deck, an early pass and a stray command are rejected with the state unchanged; only the searcher may answer", IneligibleSelectionRejected),
            ("Pass is rejected while any card qualifies and accepted when nothing does (or the deck is empty): no effect, the deck is still shuffled, the action resolves", PassOnlyWhenNothingQualifies),
            ("Order Seppuku is refused and not offered while a search is open, and only at the player's own turn to act afterwards (issue #368)", SeppukuCannotInterruptASearch),
            ("A found Terrain cannot be discarded to its own effect (issue #368): excluded from the offered and accepted discards, another copy may be discarded, and with no other card in hand nothing is discarded and there is no error", TerrainCannotBeDiscardedToItsOwnEffect),
            ("Shizuka, Dynasty deck: a Region goes to the only free province automatically, or to the chosen one among several; destroyed and already-regioned provinces are never offered", DynastyRegionAttach),
            ("Shizuka, Dynasty deck: with every province already holding a Region (or destroyed), the found Region is shown, discarded and the deck reshuffled", DynastyRegionDiscardedWhenNoProvince),
            ("Shizuka, Fate deck: a Terrain is shown, put in the hand, then a discard-a-card choice, then the deck is reshuffled", FateTerrainToHandAndDiscard),
            ("Shizuka: with no Terrain in the Fate deck (or no Region in the Dynasty deck) the only legal answer is Pass, which shuffles and resolves the action", NoEligibleCardPasses),
            ("A bowed Ikoma Shizuka cannot search, and her Limited action is once per turn", BowedShizukaRejected),
            ("A costed Region is paid through the payment path: one Gold quote per province option, exact sources charged, a wrong payment rejected atomically, an unaffordable Region discarded", CostedRegionPayment),
            ("The deferred action still resolves like any other: a Reaction to it having bowed a Personality (Tamori Ruya) opens after the search finishes", BowReactionAfterSearch),
            ("The order is always: show the card, apply its effect, shuffle the deck -- for a Region, a Terrain, an Item, a discarded Region and a pass alike", ShowEffectShuffleOrder),
            ("The shown-cards record is public to both players, holds exactly the shown card with no instance id, and is append-only", ShownCardsRecord),
            ("Hidden information: the searcher's list, deck contents, instance ids and the RNG state never reach the opponent's view (or the searcher's own event log)", HiddenInformation),
            ("The shuffle is deterministic across State copies, replay and journal recovery, continues identically afterwards, really reorders the deck, and is never advanced by a rejected command", ShuffleDeterministicAndPersisted),
            ("Setup shuffles are byte-for-byte unchanged: pinned deck orders for three seeds, and the persisted RNG state is exactly the setup sequence continued", SetupShufflesUnchanged),
            ("The criteria vocabulary: types, keywords, non-Unique and Gold/Focus comparisons are evaluated by the engine per candidate, with a short reason; a single-deck search skips the deck choice", CriteriaVocabulary),
            ("A search marked optional may be passed even while a card qualifies; a required one may not", OptionalSearch),
            ("Imperial Gift: gain 2 Honor, search the Fate deck for an Item, show it, put it in the hand, shuffle, then the Event is discarded and the Events Phase resumes -- or pass when no Item qualifies", ImperialGiftEvent),
            ("The Events Phase reveal resumes after a deferred Event from the next province: a province refilled by Harsh Choices or Imperial Gift is not revealed in the same phase", EventsRevealResumesFromNextProvince),
            ("The searcher always has a legal, offered answer at every stage (deck, list, province, discard), and every offered command validates on a fresh engine", SearcherAlwaysHasALegalAnswer),
        ];
        runner.Run("deck-search", tests);
    }

    // ---- fixtures ----

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Equal<T>(T actual, T expected, string message = "Values differ") =>
        Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    /// <summary>A quiet Action Phase for player 0: the economy scenario's board with both decks emptied (each test fills exactly the deck it needs).</summary>
    private static MatchState Start()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.DynastyDeck or Zone.FateDeck).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.DynastyDeck.Clear(); p.FateDeck.Clear(); }
        return s;
    }

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, bool bowed = false, int? province = null)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"test-{n}")) n++; // unique even after a card was removed
        var c = new CardInstance { Id = $"test-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck), Bowed = bowed, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }

    private static void Deck(MatchState s, int owner, Zone zone, params string[] definitions) { foreach (var d in definitions) Put(s, d, owner, zone); }
    private static void AttachRegion(MatchState s, int province, string region = OtherRegion, int owner = 0)
    {
        var r = Put(s, region, owner, Zone.Region, province: province);
        s.Players[owner].Provinces[province].RegionId = r.Id;
    }

    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    private static string ShizukaId(MatchEngine e, int owner = 0) => e.State.Cards.Values.First(c => c.DefinitionId == Shizuka && c.OwnerId == owner).Id;
    private static GameCommand Use(MatchEngine e, string source, string ability = "shizuka-search", int player = 0) => new("useAbility", player, e.Version, source, AbilityId: ability);
    private static GameCommand ChooseDeck(MatchEngine e, string deck, int player = 0) => new("chooseSearchDeck", player, e.Version, Choice: deck);
    private static GameCommand Select(MatchEngine e, string definition, int player = 0) => new("selectSearchCard", player, e.Version, Choice: definition);
    private static GameCommand PassSearch(MatchEngine e, int player = 0) => new("passSearch", player, e.Version);
    private static GameCommand ChooseProvince(MatchEngine e, int province, string[]? sources = null, int player = 0) => new("chooseSearchProvince", player, e.Version, Province: province, SourceIds: sources);
    private static GameCommand Discard(MatchEngine e, string cardId, int player = 0) => new("chooseSearchDiscard", player, e.Version, TargetId: cardId);

    /// <summary>Shizuka in play, then a search started: the engine is left at the deck choice.</summary>
    private static MatchEngine Searching(MatchState s, bool shizukaBowed = false)
    {
        Put(s, Shizuka, 0, Zone.Home, shizukaBowed);
        var e = Engine(s);
        Accept(e, Use(e, ShizukaId(e)));
        return e;
    }

    private static SearchView Search(MatchEngine e, int player = 0) => e.GetView(player).PendingSearch ?? throw new Exception("No search view for player " + player);
    private static int EventIndex(MatchEngine e, string type) => Array.FindIndex(e.GetView(0).Events, ev => ev.Type == type);
    private static string[] EventTypes(MatchEngine e) => e.GetView(0).Events.Select(ev => ev.Type).ToArray();

    private static void AssertOrder(MatchEngine e, params string[] types)
    {
        var events = e.GetView(0).Events; var previous = -1;
        foreach (var type in types)
        {
            var index = Array.FindIndex(events, previous + 1, ev => ev.Type == type);
            Check(index > previous, $"Event '{type}' is missing or out of order; events: {string.Join(", ", events.Select(ev => ev.Type))}");
            previous = index;
        }
    }

    private static void AssertSearcherHasAnswer(MatchEngine e)
    {
        var pending = e.State.PendingChoice ?? throw new Exception("No pending choice.");
        var offered = e.QueryActions(pending.PlayerId).Where(a => a.Enabled && a.Type is not ("concede" or "wait")).ToArray();
        Check(offered.Length > 0, $"The searcher has no offered answer at {pending.Type}: a bot would stall");
        Check(offered.All(a => a.Command is not null), "Every offered search answer is a complete command.");
        var opponent = e.QueryActions(1 - pending.PlayerId).Where(a => a.Enabled).Select(a => a.Type).ToArray();
        Check(opponent.All(t => t is "wait" or "concede"), $"The opponent may only wait or concede during a search; got {string.Join(",", opponent)}");
    }

    // ---- published shape ----

    private static void PublishedShape()
    {
        var shizuka = CardCatalog.Get(Shizuka);
        Equal(shizuka.Name, "Ikoma Shizuka"); Equal(shizuka.Type, CardType.Personality); Equal(shizuka.Clan, "Lion");
        Equal(shizuka.GoldCost, 5); Equal(shizuka.Force, 2); Equal(shizuka.Chi, 3); Equal(shizuka.HonorRequirement, 3); Equal(shizuka.PersonalHonor, 2);
        foreach (var keyword in new[] { "Lion Clan", "Samurai", "Scout", "Cavalry" }) Check(shizuka.Keywords!.Contains(keyword), "Missing keyword " + keyword);
        Equal(shizuka.Text, "Limited: Bow Shizuka: Search your Dynasty or Fate deck. If you find a Region, attach it to one of your provinces, paying all costs. If you find a Terrain, put it in your hand and discard a card.");
        Check(!shizuka.Text.Contains("Search you Dynasty"), "The catalogue's typo must not survive: the scan prints 'your'");
        Equal(string.Join(",", shizuka.Abilities!), "shizuka-search"); Check(!shizuka.ScenarioOnly);
        var spec = AbilityCatalog.All["shizuka-search"];
        Equal(spec.Timing, "Limited");
        Equal(spec.Search!.Decks.Length, 2);
        var dynasty = spec.Search.Decks.Single(d => d.Deck == SearchDeck.Dynasty); var fate = spec.Search.Decks.Single(d => d.Deck == SearchDeck.Fate);
        Equal(string.Join(",", dynasty.Criteria.Types!), "Region"); Equal(dynasty.Effect, SearchEffect.AttachRegion);
        Equal(string.Join(",", fate.Criteria.Types!), "Strategy"); Equal(string.Join(",", fate.Criteria.Keywords!), "Terrain"); Equal(fate.Effect, SearchEffect.ToHandThenDiscard);
        Check(!spec.Search.Optional, "Shizuka's search is not optional");

        var gift = CardCatalog.Get(ImperialGift);
        Equal(gift.Name, "Imperial Gift"); Equal(gift.Type, CardType.Event); Equal(gift.Effect, "imperial-gift");
        Equal(gift.Text, "Gain 2 Honor and search your Fate deck for an Item, show it, and put it into your hand.");
        Check(gift.Keywords!.Contains("Unique") && gift.Keywords.Contains("Imperial"), "Imperial Gift is Imperial * Unique");
        var search = SearchCatalog.ForEventEffect("imperial-gift")!;
        Equal(search.Decks.Length, 1); Equal(search.Decks[0].Deck, SearchDeck.Fate); Equal(string.Join(",", search.Decks[0].Criteria.Types!), "Item"); Equal(search.Decks[0].Effect, SearchEffect.ToHand);
        Check(SearchCatalog.ForEventEffect("harsh-choices") is null, "Only Imperial Gift maps to a search");
    }

    // ---- the candidate list ----

    private static MatchState TwoDeckState()
    {
        var s = Start();
        // Deliberately interleaved and uneven, so grouping, counting and ordering are all observable.
        Deck(s, 0, Zone.DynastyDeck, "training-crane-samurai", FallowLands, "training-farm", OtherRegion, "training-crane-samurai", FallowLands, "training-crane-samurai");
        Deck(s, 0, Zone.FateDeck, "training-item", Terrain, "training-rally", "training-item", Terrain, "training-follower");
        return s;
    }

    private static void CandidateList()
    {
        var s = TwoDeckState();
        var e = Searching(s);
        // Before a deck is chosen: only the deck choice, with public counts; not one card of either deck is listed.
        Equal(e.State.PendingChoice?.Type, "searchDeck");
        var choice = Search(e);
        Equal(choice.Stage, SearchStage.ChooseDeck); Check(choice.Entries is null, "No deck contents before the deck is chosen");
        Equal(choice.DeckChoices!.Length, 2);
        Equal(choice.DeckChoices.Single(d => d.Deck == "Dynasty").CardsRemaining, 7); Equal(choice.DeckChoices.Single(d => d.Deck == "Fate").CardsRemaining, 6);
        Equal(choice.DeckChoices.Single(d => d.Deck == "Dynasty").Criteria, "Region"); Equal(choice.DeckChoices.Single(d => d.Deck == "Fate").Criteria, "Terrain");
        Equal(choice.SourceName, "Ikoma Shizuka");
        var offered = e.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").ToArray();
        Equal(string.Join(",", offered.Select(a => a.Type + ":" + a.Command!.Choice)), "chooseSearchDeck:Dynasty,chooseSearchDeck:Fate");

        // Dynasty deck: grouped by card with counts, an eligibility flag and a reason.
        var dynasty = MatchEngine.FromTestState(s); Accept(dynasty, Use(dynasty, ShizukaId(dynasty))); Accept(dynasty, ChooseDeck(dynasty, "Dynasty"));
        var view = Search(dynasty);
        Equal(view.Stage, SearchStage.Select); Equal(view.Deck, "Dynasty"); Equal(view.Criteria, "Region"); Equal(view.CardsRemaining, 7);
        var entries = view.Entries!;
        Equal(entries.Length, 4); Equal(entries.Sum(x => x.Count), 7); Equal(entries.Select(x => x.DefinitionId).Distinct().Count(), 4);
        Equal(entries.Single(x => x.DefinitionId == FallowLands).Count, 2); Check(entries.Single(x => x.DefinitionId == FallowLands).Eligible);
        Equal(entries.Single(x => x.DefinitionId == OtherRegion).Count, 1); Check(entries.Single(x => x.DefinitionId == OtherRegion).Eligible);
        Check(entries.Single(x => x.DefinitionId == FallowLands).Reason is null, "An eligible entry has no reason");
        var samurai = entries.Single(x => x.DefinitionId == "training-crane-samurai");
        Equal(samurai.Count, 3); Check(!samurai.Eligible); Equal(samurai.Reason, "Not a Region");
        Check(!entries.Single(x => x.DefinitionId == "training-farm").Eligible);
        Check(entries.All(x => x.Definition.Id == x.DefinitionId), "Each entry carries its own definition for the hover preview");
        var types = entries.Select(x => (int)x.Definition.Type).ToArray();
        Check(types.SequenceEqual(types.Order()), "Entries are sorted by type, then name");
        Check(!view.CanPass, "Something qualifies, so Pass is not offered"); Check(!view.Optional);
        Equal(string.Join(",", dynasty.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type + ":" + a.Command!.Choice).Order()),
            string.Join(",", new[] { $"selectSearchCard:{FallowLands}", $"selectSearchCard:{OtherRegion}" }.Order()), "Exactly the two eligible Regions are offered, ineligible cards are not");

        // Deck order never shows: the same deck in the opposite order yields byte-identical entries.
        var reversed = TwoDeckState(); reversed.Players[0].DynastyDeck.Reverse(); Put(reversed, Shizuka, 0, Zone.Home);
        var other = Engine(reversed); Accept(other, Use(other, ShizukaId(other))); Accept(other, ChooseDeck(other, "Dynasty"));
        Equal(Json(Search(other).Entries), Json(entries), "Entries must not depend on deck order");

        // Fate deck: Terrain only.
        var fate = Engine(s); Accept(fate, Use(fate, ShizukaId(fate))); Accept(fate, ChooseDeck(fate, "Fate"));
        var fateEntries = Search(fate).Entries!;
        Equal(fateEntries.Length, 4); Equal(fateEntries.Sum(x => x.Count), 6);
        Equal(fateEntries.Single(x => x.DefinitionId == Terrain).Count, 2); Check(fateEntries.Single(x => x.DefinitionId == Terrain).Eligible);
        foreach (var id in new[] { "training-item", "training-rally", "training-follower" })
        {
            Check(!fateEntries.Single(x => x.DefinitionId == id).Eligible, id + " is not a Terrain");
            Equal(fateEntries.Single(x => x.DefinitionId == id).Reason, "Not a Terrain");
        }
        Equal(Search(fate).Deck, "Fate");

        // The opponent gets no list and only waits.
        Check(dynasty.GetView(1).PendingSearch is null, "The opponent has no deck viewer");
        Equal(string.Join(",", dynasty.QueryActions(1).Select(a => a.Type)), "wait,concede");
    }

    // ---- selection is enforced by the engine ----

    private static void IneligibleSelectionRejected()
    {
        var s = TwoDeckState();
        var e = Searching(s);
        // Wrong stage: selecting before the deck is chosen, or passing.
        Reject(e, Select(e, FallowLands)); Reject(e, PassSearch(e)); Reject(e, ChooseDeck(e, "Discard")); Reject(e, ChooseDeck(e, "")); Reject(e, new("pass", 0, e.Version));
        Accept(e, ChooseDeck(e, "Dynasty"));
        // An ineligible card (a Personality in the Dynasty deck), a card that is not in this deck at all, nothing, a Fate-deck card.
        Reject(e, Select(e, "training-crane-samurai"));
        Reject(e, Select(e, "training-farm"));
        Reject(e, Select(e, "training-item"));
        Reject(e, Select(e, Terrain));
        Reject(e, Select(e, "no-such-card"));
        Reject(e, Select(e, ""));
        Reject(e, new("selectSearchCard", 0, e.Version));
        // Pass while a Region qualifies.
        Reject(e, PassSearch(e));
        // Stray commands mid-search cannot be smuggled in (a discard, a province, the deck choice again, an ordinary action).
        Reject(e, ChooseDeck(e, "Fate")); Reject(e, ChooseProvince(e, 0)); Reject(e, Discard(e, e.State.Players[0].Hand[0]));
        Reject(e, Use(e, ShizukaId(e), "training-search-cheap")); Reject(e, new("endDynasty", 0, e.Version));
        // Only the searcher may answer, even with a legal-looking command.
        Reject(e, Select(e, FallowLands, player: 1));
        Reject(e, new("passSearch", 1, e.Version));
        // The state survived all of that, and the legal pick still works.
        Equal(e.State.PendingChoice?.Type, "searchSelect");
        Accept(e, Select(e, FallowLands));
    }

    // ---- pass ----

    private static void PassOnlyWhenNothingQualifies()
    {
        var s = Start();
        Deck(s, 0, Zone.DynastyDeck, "training-crane-samurai", "training-farm", "training-crane-samurai", "training-marketplace", "training-crane-courtier", "training-lion-samurai", "training-crane-veteran", "training-lion-veteran", "training-crane-dignitary", "training-lion-courtier");
        var handBefore = s.Players[0].Hand.ToArray();
        var e = Searching(s);
        Accept(e, ChooseDeck(e, "Dynasty"));
        var before = e.State.Players[0].DynastyDeck.ToArray();
        var view = Search(e);
        Check(view.CanPass, "Nothing qualifies: Pass is the way out"); Check(view.Entries!.All(x => !x.Eligible));
        var offered = e.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").ToArray();
        Equal(string.Join(",", offered.Select(a => a.Type)), "passSearch", "Pass is the only offered answer, and no select action is listed for an ineligible card");
        Reject(e, Select(e, "training-farm"));
        Accept(e, PassSearch(e));
        var after = e.State;
        Equal(after.PendingChoice, null); Check(after.PendingSearch is null, "The search is over");
        Equal(after.PriorityPlayerId, 1, "The action resolved and priority passed on");
        Check(after.Cards[ShizukaId(e)].Bowed, "Bowing Shizuka is the cost, paid whether or not a card qualified");
        Equal(Json(after.Players[0].Hand), Json(handBefore), "The effect does not occur: the hand is untouched");
        Equal(after.Players[0].DynastyDeck.Count, before.Length, "No card left the deck");
        Check(after.Players[0].DynastyDeck.OrderBy(x => x).SequenceEqual(before.OrderBy(x => x)), "The same cards remain");
        Check(!after.Players[0].DynastyDeck.SequenceEqual(before), "The deck is still shuffled when the player passes");
        Equal(after.ShownCards.Count, 0, "Nothing was found, so nothing was shown");
        AssertOrder(e, "searchStarted", "deckSearched", "searchPassed", "deckShuffled");
        Check(e.GetView(1).Events.Any(ev => ev.Type == "searchPassed" && ev.Message.Contains("found no eligible card")), "The opponent sees the mandatory pass");

        // An empty deck: nothing qualifies, so Pass is legal, and an empty deck shuffles harmlessly.
        var empty = Start();
        var e2 = Searching(empty); Accept(e2, ChooseDeck(e2, "Fate"));
        Check(Search(e2).CanPass && Search(e2).Entries!.Length == 0 && Search(e2).CardsRemaining == 0);
        Accept(e2, PassSearch(e2));
        Equal(e2.State.PendingChoice, null);
        AssertOrder(e2, "searchPassed", "deckShuffled");
    }

    // Order Seppuku is only taken at one's own turn to act with nothing pending (issue #368; it was refused for a search alone in #312, because it
    // cut through every pending choice and its own Overconfident window would have overwritten the search's): refused, and not offered, until the search is over.
    private static void SeppukuCannotInterruptASearch()
    {
        var s = TwoDeckState();
        var samurai = Put(s, "training-samurai", 0, Zone.Home); samurai.Dishonorable = true;
        var enemy = Put(s, "training-samurai", 1, Zone.Home); enemy.Dishonorable = true;
        var e = Searching(s);
        Check(!e.QueryActions(0).Any(a => a.Type == "orderSeppuku") && !e.QueryActions(1).Any(a => a.Type == "orderSeppuku"), "No Seppuku is offered to either player mid-search");
        Reject(e, new("orderSeppuku", 0, e.Version, CardId: samurai.Id)); Reject(e, new("orderSeppuku", 1, e.Version, CardId: enemy.Id));
        Accept(e, ChooseDeck(e, "Fate")); Reject(e, new("orderSeppuku", 0, e.Version, CardId: samurai.Id));
        Accept(e, Select(e, Terrain)); Reject(e, new("orderSeppuku", 0, e.Version, CardId: samurai.Id));
        Accept(e, Discard(e, e.State.Players[0].Hand[0]));
        // The search was a Limited action: the opportunity is the opponent's now, and Seppuku is only ever taken at one's own turn to act (issue #368).
        Equal(e.State.PriorityPlayerId, 1);
        Check(!e.QueryActions(0).Any(a => a.Type == "orderSeppuku"), "Not offered to the player whose action just resolved: it is the opponent's turn to act");
        Reject(e, new("orderSeppuku", 0, e.Version, CardId: samurai.Id));
        var handedBack = e.State; handedBack.PriorityPlayerId = 0; var back = MatchEngine.FromTestState(handedBack);
        Check(back.QueryActions(0).Any(a => a.Type == "orderSeppuku"), "Available again at their own turn to act once the search is over");
        Accept(back, new("orderSeppuku", 0, back.Version, CardId: samurai.Id));
    }

    // ---- Shizuka: Dynasty deck / Region ----

    private static void DynastyRegionAttach()
    {
        // One free province: nothing to choose, the Region attaches by itself.
        {
            var s = Start();
            Deck(s, 0, Zone.DynastyDeck, "training-crane-samurai", FallowLands, "training-farm", "training-crane-veteran", "training-lion-samurai", "training-crane-courtier", "training-marketplace", "training-crane-dignitary", "training-lion-veteran", "training-lion-courtier");
            for (var i = 0; i < 3; i++) AttachRegion(s, i);
            var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty"));
            var deckBefore = e.State.Players[0].DynastyDeck.ToArray();
            var shownId = e.State.Cards.Values.Single(c => c.DefinitionId == FallowLands).Id;
            Accept(e, Select(e, FallowLands));
            var after = e.State;
            Equal(after.PendingChoice, null, "One place to put it, nothing to pay: no further choice");
            Equal(after.Players[0].Provinces[3].RegionId, shownId); Equal(after.Cards[shownId].Zone, Zone.Region); Equal(after.Cards[shownId].ProvinceIndex, 3); Check(after.Cards[shownId].FaceUp);
            Equal(after.Players[0].DynastyDeck.Count, deckBefore.Length - 1); Check(!after.Players[0].DynastyDeck.Contains(shownId));
            Check(!after.Players[0].DynastyDeck.OrderBy(x => x).Except(deckBefore).Any() && !after.Players[0].DynastyDeck.SequenceEqual(deckBefore.Where(x => x != shownId)), "The rest of the deck was shuffled");
            Equal(after.PriorityPlayerId, 1);
            AssertOrder(e, "cardShown", "regionAttached", "deckShuffled");
            Equal(e.GetView(0).Players[0].Provinces[3].Region!.Id, shownId, "The Region is public on the province");
            Equal(e.GetView(1).Players[0].Provinces[3].Region!.Definition!.Id, FallowLands, "The opponent sees it too");
            Equal(e.GetView(0).Players[0].Provinces[3].Strength, 8, "Fallow Lands' static +2 applies at once, as after an Events Phase reveal");
        }
        // Several free provinces: the player chooses; destroyed and already-regioned provinces are not options.
        {
            var s = Start();
            Deck(s, 0, Zone.DynastyDeck, FallowLands, "training-crane-samurai", "training-farm");
            AttachRegion(s, 1);
            s.Players[0].Provinces[2].Destroyed = true;
            var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty")); Accept(e, Select(e, FallowLands));
            Equal(e.State.PendingChoice?.Type, "searchRegionProvince"); Equal(e.State.PendingSearch?.Stage, SearchStage.ChooseProvince);
            var view = Search(e);
            Equal(view.Stage, SearchStage.ChooseProvince); Equal(view.Selected?.Id, FallowLands);
            var options = e.QueryActions(0).Where(a => a.Type == "chooseSearchProvince").ToArray();
            Equal(string.Join(",", options.Select(a => a.Command!.Province).Order()), "0,3", "Only provinces that are not destroyed and hold no Region are offered");
            Check(options.All(a => a.Gold is null && a.Enabled), "A free Region needs no payment panel");
            Check(e.State.Players[0].DynastyDeck.Any(id => e.State.Cards[id].DefinitionId == FallowLands), "The Region is still in the deck until it is attached");
            Reject(e, ChooseProvince(e, 1)); // already has a Region
            Reject(e, ChooseProvince(e, 2)); // destroyed
            Reject(e, ChooseProvince(e, 4)); Reject(e, ChooseProvince(e, -1));
            Reject(e, new("chooseSearchProvince", 0, e.Version)); // no province at all
            Reject(e, PassSearch(e)); Reject(e, Select(e, FallowLands));
            Accept(e, ChooseProvince(e, 0));
            var after = e.State;
            Check(after.Players[0].Provinces[0].RegionId is not null && after.Players[0].Provinces[3].RegionId is null);
            Equal(after.Cards[after.Players[0].Provinces[0].RegionId!].DefinitionId, FallowLands); Equal(after.Cards[after.Players[0].Provinces[0].RegionId!].Zone, Zone.Region);
            AssertOrder(e, "cardShown", "regionAttached", "deckShuffled");
            Equal(after.PriorityPlayerId, 1);
        }
    }

    private static void DynastyRegionDiscardedWhenNoProvince()
    {
        var s = Start();
        Deck(s, 0, Zone.DynastyDeck, "training-crane-samurai", FallowLands, "training-farm", OtherRegion, "training-crane-veteran", "training-lion-samurai", "training-crane-courtier", "training-marketplace", "training-crane-dignitary", "training-lion-veteran");
        AttachRegion(s, 0); AttachRegion(s, 1); AttachRegion(s, 2); s.Players[0].Provinces[3].Destroyed = true; // 3 Regions + 1 destroyed province
        var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty"));
        Check(Search(e).Entries!.Single(x => x.DefinitionId == FallowLands).Eligible, "A Region is still found (and shown) even though no province can take it");
        var region = e.State.Cards.Values.Single(c => c.DefinitionId == FallowLands).Id;
        var deckBefore = e.State.Players[0].DynastyDeck.ToArray();
        Accept(e, Select(e, FallowLands));
        var after = e.State;
        Equal(after.PendingChoice, null, "Nothing to choose: it is discarded straight away");
        Equal(after.Cards[region].Zone, Zone.DynastyDiscard); Check(after.Cards[region].FaceUp); Check(after.Players[0].Provinces.All(p => p.RegionId != region));
        Equal(after.Players[0].DynastyDeck.Count, deckBefore.Length - 1);
        Check(!after.Players[0].DynastyDeck.SequenceEqual(deckBefore.Where(x => x != region)), "The Dynasty deck is reshuffled");
        Equal(after.ShownCards.Count, 1, "It was shown before it was discarded"); Equal(after.ShownCards[0].DefinitionId, FallowLands);
        AssertOrder(e, "cardShown", "regionDiscarded", "deckShuffled");
        Check(e.GetView(1).Events.Any(ev => ev.Type == "regionDiscarded" && ev.Message.Contains("none of")), "The opponent is told why");
        Check(e.GetView(0).Players[0].Discard.Any(c => c.Id == region), "It is in the Dynasty discard pile, a public zone");
        Equal(after.PriorityPlayerId, 1);
    }

    // ---- Shizuka: Fate deck / Terrain ----

    private static void FateTerrainToHandAndDiscard()
    {
        var s = TwoDeckState();
        var handBefore = s.Players[0].Hand.Count;
        var e = Searching(s); Accept(e, ChooseDeck(e, "Fate"));
        var terrain = e.State.Cards.Values.Where(c => c.DefinitionId == Terrain).OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;
        var deckBefore = e.State.Players[0].FateDeck.ToArray();
        Accept(e, Select(e, Terrain));
        var mid = e.State;
        Equal(mid.PendingChoice?.Type, "searchDiscard"); Equal(mid.PendingSearch?.Stage, SearchStage.ChooseDiscard);
        Check(mid.Players[0].Hand.Contains(terrain) && mid.Cards[terrain].Zone == Zone.Hand, "The Terrain is in the hand");
        Equal(mid.Players[0].Hand.Count, handBefore + 1); Equal(mid.Players[0].FateDeck.Count, deckBefore.Length - 1);
        Equal(mid.ShownCards.Count, 1);
        Check(Array.FindIndex(e.GetView(0).Events, ev => ev.Type == "deckShuffled") < 0, "The deck is shuffled only after the discard: the effect includes it");
        Check(e.GetView(0).PendingSearch!.Selected!.Id == Terrain, "The searcher sees which card is being resolved");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseSearchDiscard").ToArray();
        Equal(options.Length, handBefore, "One discard option per hand card OTHER than the found Terrain (issue #368)");
        Check(options.All(a => a.Command!.TargetId != terrain), "The found Terrain is not offered for its own discard");
        Reject(e, Discard(e, terrain));
        Equal(e.State.PendingChoice?.Type, "searchDiscard", "and the refusal leaves the choice open");
        Reject(e, Discard(e, "not-a-card")); Reject(e, Discard(e, mid.Players[1].Hand[0])); Reject(e, Discard(e, mid.Players[0].FateDeck[0]));
        Reject(e, new("chooseSearchDiscard", 0, e.Version)); Reject(e, PassSearch(e)); Reject(e, Select(e, Terrain));
        var discarded = mid.Players[0].Hand.First(id => id != terrain);
        Accept(e, Discard(e, discarded));
        var after = e.State;
        Equal(after.PendingChoice, null); Equal(after.Cards[discarded].Zone, Zone.FateDiscard); Check(after.Cards[discarded].FaceUp);
        Equal(after.Players[0].Hand.Count, handBefore, "One in, one out");
        Check(after.Players[0].Hand.Contains(terrain), "The Terrain stays in the hand: any other card was discarded");
        var expectedRest = deckBefore.Where(x => x != terrain).ToArray();
        Check(after.Players[0].FateDeck.OrderBy(x => x).SequenceEqual(expectedRest.OrderBy(x => x)) && !after.Players[0].FateDeck.SequenceEqual(expectedRest), "The Fate deck was shuffled");
        AssertOrder(e, "cardShown", "searchToHand", "fateDiscarded", "deckShuffled");
        Equal(after.PriorityPlayerId, 1);
    }

    // Mendon's ruling (issue #368): a Terrain that is found is placed in play and is no longer in the hand, so it cannot be the card discarded to its own
    // effect. Every other card in the hand still can (including another copy of the same Terrain), and when there is none the discard cannot be
    // satisfied: nothing is discarded and there is no error -- the search carries on to its shuffle.
    private static void TerrainCannotBeDiscardedToItsOwnEffect()
    {
        // (1) With other cards in hand the found Terrain is excluded from the offered discards and from the accepted ones -- through QueryActions.
        var s = TwoDeckState();
        var e = Searching(s); Accept(e, ChooseDeck(e, "Fate")); Accept(e, Select(e, Terrain));
        var found = e.State.PendingSearch!.SelectedCardId!;
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseSearchDiscard").Select(a => a.Command!.TargetId!).ToArray();
        Equal(offered.Length, e.State.Players[0].Hand.Count - 1, "every hand card but the found Terrain");
        Check(!offered.Contains(found) && e.State.Players[0].Hand.Contains(found), "the Terrain is in the hand but not among the discards offered");
        Check(e.QueryActions(0).Where(a => a.Type == "chooseSearchDiscard").All(a => a.Enabled), "and every offered discard is enabled");
        var before = Json(e.State);
        Reject(e, Discard(e, found));
        Equal(Json(e.State), before, "the refused discard leaves the state untouched");
        Check(e.State.Players[0].Hand.Contains(found) && e.State.Cards[found].Zone == Zone.Hand, "the Terrain stays in the hand");
        Accept(e, Discard(e, offered[0]));
        Equal(e.State.Cards[offered[0]].Zone, Zone.FateDiscard); Equal(e.State.PendingChoice, null);

        // (2) A second copy of the same Terrain already in the hand may be discarded: only the ONE just found is excluded.
        var two = TwoDeckState();
        var held = Put(two, Terrain, 0, Zone.Hand);
        var e2 = Searching(two); Accept(e2, ChooseDeck(e2, "Fate")); Accept(e2, Select(e2, Terrain));
        var found2 = e2.State.PendingSearch!.SelectedCardId!;
        Check(found2 != held.Id, "the copy found is a different instance");
        Check(e2.QueryActions(0).Any(a => a.Type == "chooseSearchDiscard" && a.Command!.TargetId == held.Id), "the copy already held is offered");
        Reject(e2, Discard(e2, found2)); Accept(e2, Discard(e2, held.Id));
        Equal(e2.State.Cards[held.Id].Zone, Zone.FateDiscard); Check(e2.State.Players[0].Hand.Contains(found2));

        // (3) Exactly one other card: it is the only discard offered.
        var one = TwoDeckState(); one.Players[0].Hand.Skip(1).ToList().ForEach(id => { one.Players[0].Hand.Remove(id); one.Cards.Remove(id); });
        var e3 = Searching(one); Accept(e3, ChooseDeck(e3, "Fate")); Accept(e3, Select(e3, Terrain));
        var only = e3.QueryActions(0).Where(a => a.Type == "chooseSearchDiscard").ToArray();
        Equal(only.Length, 1, "exactly one discard is offered"); Check(only[0].Command!.TargetId != e3.State.PendingSearch!.SelectedCardId);

        // (4) No other card in the hand: the discard cannot be satisfied -- nothing is discarded, no error, no stage to answer; the deck is
        // still shuffled, the Terrain stays in the hand, and the action resolves.
        var empty = TwoDeckState();
        foreach (var id in empty.Players[0].Hand.ToArray()) { empty.Players[0].Hand.Remove(id); empty.Cards.Remove(id); }
        var e4 = Searching(empty); Accept(e4, ChooseDeck(e4, "Fate"));
        var deckBefore = e4.State.Players[0].FateDeck.ToArray(); var pileBefore = e4.State.Cards.Values.Count(c => c.Zone == Zone.FateDiscard);
        var result = e4.Execute(Select(e4, Terrain)); Check(result.Accepted, "no error: " + result.Error);
        Equal(e4.State.PendingChoice, null, "nothing left to answer"); Equal(e4.State.PendingSearch, null);
        Equal(e4.State.Players[0].Hand.Count, 1, "only the found Terrain is in the hand");
        Equal(e4.State.Cards[e4.State.Players[0].Hand[0]].DefinitionId, Terrain);
        Equal(e4.State.Cards.Values.Count(c => c.Zone == Zone.FateDiscard), pileBefore, "nothing was discarded");
        AssertOrder(e4, "cardShown", "searchToHand", "searchDiscardSkipped", "deckShuffled");
        Check(e4.State.Players[0].FateDeck.OrderBy(x => x).SequenceEqual(deckBefore.Where(x => x != e4.State.Players[0].Hand[0]).OrderBy(x => x)) && !e4.State.Players[0].FateDeck.SequenceEqual(deckBefore.Where(x => x != e4.State.Players[0].Hand[0])), "the deck was still shuffled");
        Equal(e4.State.PriorityPlayerId, 1, "the action resolved and the opportunity passed");
        Check(!e4.QueryActions(0).Any(a => a.Type == "chooseSearchDiscard"), "no discard is offered");
        // The same holds for a bot that answers whatever is offered: it never stalls in the empty case (no offered answer is needed).
        Check(e4.GetView(0).PendingSearch is null, "and the searcher's view holds no search");
    }

    private static string Put1(MatchEngine e) => e.State.Cards.Values.First(c => c.OwnerId == 0 && (c.DefinitionId == Shizuka || c.DefinitionId == "training-search-source")).Id;

    private static void NoEligibleCardPasses()
    {
        // Fate deck without a Terrain.
        var s = Start();
        Deck(s, 0, Zone.FateDeck, "training-item", "training-rally", "training-follower", "training-item", "training-pressure", "training-armor", "training-weapon", "training-kama");
        Deck(s, 0, Zone.DynastyDeck, FallowLands);
        var handBefore = s.Players[0].Hand.ToArray();
        var e = Searching(s); Accept(e, ChooseDeck(e, "Fate"));
        Check(Search(e).CanPass && Search(e).Entries!.All(x => !x.Eligible));
        Equal(string.Join(",", e.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type)), "passSearch");
        Reject(e, Select(e, "training-item"));
        var before = e.State.Players[0].FateDeck.ToArray();
        Accept(e, PassSearch(e));
        Equal(e.State.PendingChoice, null); Equal(Json(e.State.Players[0].Hand), Json(handBefore)); Equal(e.State.ShownCards.Count, 0);
        Check(!e.State.Players[0].FateDeck.SequenceEqual(before) && e.State.Players[0].FateDeck.OrderBy(x => x).SequenceEqual(before.OrderBy(x => x)), "Shuffled, same cards");
        Equal(e.State.PriorityPlayerId, 1);
        // The Region in the Dynasty deck was never touched.
        Equal(e.State.Players[0].DynastyDeck.Count, 1);

        // Dynasty deck without a Region.
        var s2 = Start();
        Deck(s2, 0, Zone.DynastyDeck, "training-crane-samurai", "training-farm", "training-marketplace");
        Deck(s2, 0, Zone.FateDeck, Terrain, Terrain);
        var e2 = Searching(s2); Accept(e2, ChooseDeck(e2, "Dynasty"));
        Equal(string.Join(",", e2.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type)), "passSearch");
        Accept(e2, PassSearch(e2));
        Equal(e2.State.Players[0].FateDeck.Count, 2, "The Terrains in the other deck were never touched");
        AssertOrder(e2, "searchPassed", "deckShuffled");
    }

    private static void BowedShizukaRejected()
    {
        var s = TwoDeckState();
        Put(s, Shizuka, 0, Zone.Home, bowed: true);
        var e = Engine(s);
        Reject(e, Use(e, ShizukaId(e)));
        var offered = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Label.StartsWith("Bow Shizuka: search"));
        Check(!offered.Enabled && offered.Reason is not null, "Offered disabled with a reason while she is bowed");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null);

        // Unbowed: usable; after the search she is bowed, so she cannot search again this turn (Limited, and her own cost).
        var s2 = Start(); Deck(s2, 0, Zone.DynastyDeck, "training-farm", "training-marketplace"); Put(s2, Shizuka, 0, Zone.Home); // no Region: Pass is the way out
        var e2 = Engine(s2); var id = ShizukaId(e2);
        Check(e2.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "shizuka-search").Enabled);
        Accept(e2, Use(e2, id)); Accept(e2, ChooseDeck(e2, "Dynasty")); Accept(e2, PassSearch(e2)); // no Region: pass
        Check(e2.State.Cards[id].Bowed);
        Equal(e2.State.PriorityPlayerId, 1);
        Reject(e2, Use(e2, id, player: 1)); // not the opponent's card, not their action
        // The opponent cannot search with someone else's Shizuka either.
        var s3 = TwoDeckState(); Put(s3, Shizuka, 0, Zone.Home); s3.PriorityPlayerId = 1;
        var e3 = Engine(s3); Reject(e3, Use(e3, ShizukaId(e3), player: 1));
    }

    // The search defers the action, so the "after your action bowed a Personality" check an ordinary action does at its tail
    // (Tamori Ruya) must still happen once the search has finished: Shizuka's cost bowed a Personality.
    private static void BowReactionAfterSearch()
    {
        var s = Start(); Deck(s, 0, Zone.DynastyDeck, "training-farm", "training-marketplace");
        var ruya = Put(s, "oracle-l5r-7789", 0, Zone.Home); Put(s, "training-crane-samurai", 0, Zone.Home);
        var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty")); Accept(e, PassSearch(e));
        Equal(e.State.PendingChoice?.Type, "bowReaction", "Ruya may react to Shizuka's action having bowed a Personality"); Equal(e.State.PendingChoice?.CardId, ruya.Id);
        AssertOrder(e, "searchPassed", "deckShuffled", "reactionWindow");
        Equal(e.State.PriorityPlayerId, 0);
        Accept(e, new("declineReaction", 0, e.Version)); Accept(e, new("declineReaction", 1, e.Version));
        Equal(e.State.PendingChoice, null); Equal(e.State.PriorityPlayerId, 1, "The action resolved and priority passed on once the window closed");

        // Without such a watcher there is no window (nothing changes for every other search).
        var plain = Start(); Deck(plain, 0, Zone.DynastyDeck, "training-farm");
        var pe = Searching(plain); Accept(pe, ChooseDeck(pe, "Dynasty")); Accept(pe, PassSearch(pe));
        Equal(pe.State.PendingChoice, null); Equal(pe.State.PriorityPlayerId, 1);
    }

    // ---- costed Region: the payment path ----

    private static MatchState CostedRegionState()
    {
        var s = Start();
        Deck(s, 0, Zone.DynastyDeck, "training-crane-samurai", "training-costed-region", "training-farm", FallowLands);
        return s;
    }

    private static void CostedRegionPayment()
    {
        // Several provinces: one option per free province, each carrying the same authoritative Gold quote and the engine's suggested sources.
        var s = CostedRegionState();
        var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty"));
        Accept(e, Select(e, "training-costed-region"));
        Equal(e.State.PendingChoice?.Type, "searchRegionProvince", "A costed Region always asks: the payment needs the player's chosen sources");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseSearchProvince").ToArray();
        Equal(options.Length, 4);
        foreach (var option in options)
        {
            Check(option.Gold is { Cost: 2, Kind: GoldCostKind.Fixed } && option.Gold.Basis.Contains("Training costed Region cost 2") && option.Gold.Payments.Length > 0, "Every option carries the one Gold quote");
            Equal(Json(option.Command!.SourceIds), Json(option.Gold!.Payments[0].SourceIds), "The command is pre-filled with the engine's suggested payment");
        }
        var sources = options[2].Command!.SourceIds!;
        var untouched = e.State.Players[0].Provinces.Select(p => p.RegionId).ToArray();
        Reject(e, ChooseProvince(e, 2));                              // no payment at all
        Reject(e, ChooseProvince(e, 2, []));
        Reject(e, ChooseProvince(e, 2, [ShizukaOrHome(e)]));         // a source that produces no Gold
        Reject(e, ChooseProvince(e, 2, [sources[0], sources[0]]));    // the same source twice
        Reject(e, ChooseProvince(e, 2, ["no-such-card"]));
        Check(e.State.Players[0].Provinces.Select(p => p.RegionId).SequenceEqual(untouched), "Nothing attached by a rejected payment");
        var bowedBefore = e.State.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet();
        Accept(e, ChooseProvince(e, 2, sources));
        var after = e.State;
        Check(after.Players[0].Provinces[2].RegionId is not null, "Attached to the chosen province");
        Equal(after.Cards[after.Players[0].Provinces[2].RegionId!].DefinitionId, "training-costed-region");
        Check(sources.All(id => after.Cards[id].Bowed), "Exactly the chosen sources were bowed");
        Equal(after.Cards.Values.Count(c => c.Bowed && !bowedBefore.Contains(c.Id)), sources.Length, "Nothing else was bowed by the payment");
        var payment = e.GetView(1).Events.Single(ev => ev.Type == "payment");
        Check(payment.Message.Contains("paid 2 Gold for Training costed Region"), payment.Message);
        AssertOrder(e, "cardShown", "payment", "regionAttached", "deckShuffled");
        Equal(after.PriorityPlayerId, 1);

        // A single free province still asks (a payment needs sources), and offers exactly that province.
        var s2 = CostedRegionState(); AttachRegion(s2, 0); AttachRegion(s2, 1); AttachRegion(s2, 3);
        var e2 = Searching(s2); Accept(e2, ChooseDeck(e2, "Dynasty")); Accept(e2, Select(e2, "training-costed-region"));
        var only = e2.QueryActions(0).Where(a => a.Type == "chooseSearchProvince").Single();
        Equal(only.Command!.Province, 2);
        Accept(e2, only.Command!);
        Equal(e2.State.Players[0].Provinces[2].RegionId is not null, true);

        // Unaffordable: every Gold source is bowed, so it cannot be paid, and like any Region that fails to attach it is discarded.
        var s3 = CostedRegionState();
        foreach (var c in s3.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        var e3 = Searching(s3); Accept(e3, ChooseDeck(e3, "Dynasty"));
        var regionId = e3.State.Cards.Values.Single(c => c.DefinitionId == "training-costed-region").Id;
        Accept(e3, Select(e3, "training-costed-region"));
        Equal(e3.State.PendingChoice, null); Equal(e3.State.Cards[regionId].Zone, Zone.DynastyDiscard);
        Check(e3.State.Players[0].Provinces.All(p => p.RegionId is null));
        Check(e3.GetView(1).Events.Any(ev => ev.Type == "regionDiscarded" && ev.Message.Contains("cannot pay its 2 Gold cost")), "The opponent is told the cost could not be paid");
        AssertOrder(e3, "cardShown", "regionDiscarded", "deckShuffled");
        Check(!e3.GetView(1).Events.Any(ev => ev.Type == "payment"), "No Gold was charged for a discarded Region");

        // A free Region takes no sources: passing some is rejected, and the free Region (Fallow Lands) never shows a payment panel.
        var s4 = CostedRegionState(); var e4 = Searching(s4); Accept(e4, ChooseDeck(e4, "Dynasty")); Accept(e4, Select(e4, FallowLands));
        Reject(e4, ChooseProvince(e4, 0, [Sources(e4)[0]]));
        Accept(e4, ChooseProvince(e4, 0, []));
    }

    private static string[] Sources(MatchEngine e) => e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && !c.Bowed && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
    private static string ShizukaOrHome(MatchEngine e) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == Shizuka).Id;

    // ---- order: show, effect, shuffle ----

    private static void ShowEffectShuffleOrder()
    {
        // Region attached (free).
        var s = Start(); Deck(s, 0, Zone.DynastyDeck, FallowLands, "training-farm"); AttachRegion(s, 0); AttachRegion(s, 1); AttachRegion(s, 2);
        var e = Searching(s); Accept(e, ChooseDeck(e, "Dynasty")); Accept(e, Select(e, FallowLands));
        AssertOrder(e, "abilityUsed", "searchStarted", "deckSearched", "cardShown", "regionAttached", "deckShuffled");
        Equal(e.GetView(0).ShownCards!.Single().EventSequence, e.GetView(0).Events.Single(ev => ev.Type == "cardShown").Sequence, "The record points at its own event");

        // Terrain: to hand, then the discard, then the shuffle.
        var t = TwoDeckState(); var te = Searching(t); Accept(te, ChooseDeck(te, "Fate")); Accept(te, Select(te, Terrain));
        var showAt = EventIndex(te, "cardShown"); Check(showAt >= 0 && EventIndex(te, "searchToHand") > showAt && EventIndex(te, "deckShuffled") < 0, "Shown, then in hand, and not shuffled yet");
        Accept(te, Discard(te, te.State.Players[0].Hand[0]));
        AssertOrder(te, "cardShown", "searchToHand", "fateDiscarded", "deckShuffled");

        // An Item put into the hand (the shape Imperial Gift uses), through the training search.
        var i = Start(); Deck(i, 0, Zone.FateDeck, "training-item", "training-rally"); Put(i, "training-search-source", 0, Zone.Home);
        var ie = Engine(i); Accept(ie, Use(ie, Put1(ie), "training-search-cheap"));
        Equal(ie.State.PendingChoice?.Type, "searchSelect", "One deck: no deck choice"); Accept(ie, Select(ie, "training-item"));
        AssertOrder(ie, "cardShown", "searchToHand", "deckShuffled"); Equal(ie.State.PendingChoice, null);

        // Pass: no show, no effect, still shuffled.
        var p = Start(); Deck(p, 0, Zone.DynastyDeck, "training-farm"); var pe = Searching(p); Accept(pe, ChooseDeck(pe, "Dynasty")); Accept(pe, PassSearch(pe));
        AssertOrder(pe, "searchStarted", "deckSearched", "searchPassed", "deckShuffled");
        Check(EventIndex(pe, "cardShown") < 0, "Nothing is shown when nothing was found");

        // The shuffle is exactly one step of the same generator: two shuffles in one turn (two searches) advance it twice.
        var two = Start(); Deck(two, 0, Zone.FateDeck, "training-item", "training-weapon", "training-armor", "training-follower");
        Put(two, "training-search-source", 0, Zone.Home);
        var ee = Engine(two); var rng0 = ee.State.RngState;
        Accept(ee, Use(ee, Put1(ee), "training-search-cheap")); Accept(ee, Select(ee, "training-item"));
        var rng1 = ee.State.RngState; Check(rng1 != rng0, "The first shuffle advanced the persisted generator");
        Equal(ee.State.PriorityPlayerId, 1);
    }

    // ---- shown cards ----

    private static void ShownCardsRecord()
    {
        var s = TwoDeckState();
        var decoys = new[] { "training-armor", "training-kama", "training-two-handed-weapon" };
        Deck(s, 0, Zone.FateDeck, decoys); // definitions found nowhere else on the board
        var e = Searching(s); Accept(e, ChooseDeck(e, "Fate")); Accept(e, Select(e, Terrain));
        var found = e.State.Cards.Values.Single(c => c.Zone == Zone.Hand && c.OwnerId == 0 && c.DefinitionId == Terrain).Id;
        // Mid-search (before the discard) and after it, both players see the same public record. The discarded card
        // goes to a PUBLIC pile, so only what stays in the hand (the found Terrain included) is asserted hidden.
        Accept(e, Discard(e, e.State.Players[0].Hand.First(id => id != found)));
        var handIds = e.State.Players[0].Hand.ToArray();
        var mine = e.GetView(0).ShownCards!; var theirs = e.GetView(1).ShownCards!;
        Equal(mine.Length, 1); Equal(Json(mine), Json(theirs), "Identical in both players' views");
        var shown = theirs[0];
        Equal(shown.ShownByPlayerId, 0); Equal(shown.Definition.Id, Terrain); Equal(shown.Definition.Name, "Training terrain");
        Equal(shown.SourceCardId, ShizukaId(e)); Equal(shown.SourceName, "Ikoma Shizuka"); Equal(shown.Deck, "Fate"); Equal(shown.Sequence, 1); Equal(shown.TurnNumber, e.State.TurnNumber);
        Check(shown.Context.Contains("Fate deck search"), shown.Context);
        // The record is the shown card ONLY: no instance id of it, no deck contents, no other hidden card.
        var recordJson = Json(theirs);
        Check(!recordJson.Contains(found), "The record does not carry the shown card's instance id");
        foreach (var decoy in decoys) Check(!recordJson.Contains(decoy) && !Json(e.GetView(1)).Contains(decoy), $"Deck card {decoy} was never shown and must not appear");
        HiddenIdsAbsent(e.GetView(1), handIds);
        HiddenIdsAbsent(e.GetView(1), e.State.Players[0].FateDeck);
        // The shown card is still hidden in the hand -- only the record reveals what it was.
        Equal(e.GetView(1).Players[0].Hand.Length, 0); Check(e.GetView(1).Players[0].HandCount == handIds.Length);

        // Append-only: a second search adds a second entry and leaves the first untouched.
        var second = Engine(e.State); // a fresh engine on the same state: the record must survive the copy
        Equal(Json(second.GetView(0).ShownCards), Json(mine), "The record survives State copies");
        var t = TwoDeckState(); Put(t, "training-search-source", 0, Zone.Home); Put(t, Shizuka, 0, Zone.Home);
        var te = Engine(t);
        Accept(te, Use(te, ShizukaId(te))); Accept(te, ChooseDeck(te, "Fate")); Accept(te, Select(te, Terrain)); Accept(te, Discard(te, te.State.Players[0].Hand[0]));
        var afterFirst = Json(te.GetView(0).ShownCards);
        // Player 1 acts (pass), then it is player 0's again: a second search with the other source.
        Accept(te, new("pass", 1, te.Version)); // priority is back with player 0
        Accept(te, Use(te, te.State.Cards.Values.First(c => c.DefinitionId == "training-search-source" && c.OwnerId == 0).Id, "training-search-cheap"));
        var cheap = Search(te).Entries!.First(x => x.Eligible).DefinitionId;
        Accept(te, Select(te, cheap));
        var both = te.GetView(1).ShownCards!;
        Equal(both.Length, 2); Equal(both[0].Sequence, 1); Equal(both[1].Sequence, 2); Equal(Json(both.Take(1).ToArray()), Json(JsonSerializer.Deserialize<ShownCardView[]>(afterFirst)!), "The first entry is unchanged");
        Equal(both[1].Definition.Id, cheap); Equal(both[1].SourceName, "Training search source");
        Check(both[1].EventSequence > both[0].EventSequence);
    }

    // ---- hidden information ----

    private static void HiddenInformation()
    {
        var s = TwoDeckState();
        Deck(s, 0, Zone.FateDeck, "training-armor", "training-kama");
        Deck(s, 1, Zone.FateDeck, "training-two-handed-weapon", "training-unique-weapon");
        var hand0 = s.Players[0].Hand.ToArray(); var hand1 = s.Players[1].Hand.ToArray();
        var e = Searching(s);
        var deckIds = s.Players[0].DynastyDeck.Concat(s.Players[0].FateDeck).ToArray();
        var rng = e.State.RngState.ToString();

        foreach (var stage in new[] { "deck choice", "list" })
        {
            var view1 = e.GetView(1); var view0 = e.GetView(0);
            HiddenIdsAbsent(view1, deckIds); HiddenIdsAbsent(view1, hand0);
            HiddenIdsAbsent(view0, deckIds); // even the searcher's own view lists definitions, never instance ids
            Check(view1.PendingSearch is null, $"The opponent has no search view at the {stage}");
            var opponentJson = Json(view1);
            foreach (var secret in new[] { "training-armor", "training-kama", "training-crane-samurai", FallowLands }) Check(!opponentJson.Contains(secret), $"{secret} leaked to the opponent at the {stage}");
            foreach (var json in new[] { opponentJson, Json(view0) })
            {
                Check(!json.Contains("rng", StringComparison.OrdinalIgnoreCase), "No RNG field in any view");
                Check(!json.Contains(rng), "The RNG state value is in no view");
            }
            Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "The opponent's action list reveals nothing");
            Check(!Json(e.QueryActions(1)).Contains("Fallow"), "No search content in the opponent's actions");
            if (stage == "deck choice") Accept(e, ChooseDeck(e, "Dynasty"));
        }
        // The public prompt names the source and the deck kind only.
        var pending = e.GetView(1).PendingChoice!;
        Equal(pending.Type, "searchSelect"); Equal(pending.PlayerId, 0);
        Check(!pending.Prompt.Contains("Fallow") && !pending.Prompt.Contains("samurai"), "The public prompt carries no card names");
        // The opponent's own hand and deck were never involved.
        HiddenIdsAbsent(e.GetView(0), hand1);
        // The searcher's own list carries no event-log leak either: events never name an instance id of a deck card.
        var events = Json(e.GetView(0).Events);
        foreach (var id in deckIds) Check(!events.Contains(id), "A deck card's id is in the event log");

        // After the card is taken, the (now hidden) shown card's id is in no event or opponent view.
        Accept(e, Select(e, FallowLands));
        HiddenIdsAbsent(e.GetView(1), deckIds); HiddenIdsAbsent(e.GetView(1), hand0);
    }

    // ---- the shuffle ----

    private static void ShuffleDeterministicAndPersisted()
    {
        var find = (MatchEngine e, string def) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == def).Id;
        var play = (ulong seed) =>
        {
            var e = MatchEngine.CreateDeckSearchShowcase(seed);
            Check(e.State.Phase == Phase.Action && e.State.ActivePlayerId == 0, "The fixture opens in player 0's Action Phase");
            Accept(e, Use(e, find(e, Shizuka)));
            Accept(e, ChooseDeck(e, "Fate"));
            Accept(e, Select(e, Terrain));
            Accept(e, Discard(e, e.State.Players[0].Hand[0]));
            return e;
        };
        var a = play(20260906);
        // State copies are detached and identical.
        var first = a.State; var second = a.State;
        Equal(first.RngState, second.RngState); Equal(Json(first), Json(second));
        first.RngState = 1; first.Players[0].FateDeck.Reverse();
        Equal(a.State.RngState, second.RngState, "Mutating a copy never touches the engine's generator"); Equal(Json(a.State), Json(second));
        // The shuffle changed the order (it is not a no-op) and advanced the generator.
        var fresh = MatchEngine.CreateDeckSearchShowcase(20260906);
        var deckBefore = fresh.State.Players[0].FateDeck.ToArray(); var rngBefore = fresh.State.RngState;
        Accept(fresh, Use(fresh, find(fresh, Shizuka))); Accept(fresh, ChooseDeck(fresh, "Fate")); Accept(fresh, Select(fresh, Terrain));
        var taken = deckBefore.Except(fresh.State.Players[0].FateDeck).Single();
        Accept(fresh, Discard(fresh, fresh.State.Players[0].Hand[0]));
        var expected = deckBefore.Where(x => x != taken).ToArray(); var actual = fresh.State.Players[0].FateDeck.ToArray();
        Check(actual.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "Same cards");
        Check(!actual.SequenceEqual(expected), "The shuffle really reordered a 33-card deck");
        Check(fresh.State.RngState != rngBefore, "The persisted generator advanced");
        Equal(Json(fresh.State), Json(a.State), "Same seed, same commands: identical state, deck order and generator included");
        Check(Json(play(1).State.Players[0].FateDeck) != Json(a.State.Players[0].FateDeck), "A different seed shuffles differently");

        // Replay and journal recovery: the server persists exactly this record and recovers with Replay.
        var journal = JsonSerializer.Deserialize<ReplayRecord>(Json(a.ExportReplay()))!;
        var recovered = MatchEngine.Replay(journal);
        Equal(Json(recovered.State), Json(a.State), "Replay reproduces the shuffled decks and the generator");
        Equal(recovered.State.RngState, a.State.RngState);
        // ... and the generator carries on identically afterwards: a second search on both.
        foreach (var engine in new[] { a, recovered })
        {
            Accept(engine, new("pass", 1, engine.Version));
            Accept(engine, Use(engine, find(engine, "training-search-source"), "training-search-cheap"));
            Accept(engine, Select(engine, Search(engine).Entries!.First(x => x.Eligible).DefinitionId));
        }
        Equal(Json(recovered.State), Json(a.State), "After recovery the next shuffle is the one the live match would have made");

        // A rejected command never advances the generator (Execute rolls back the copy, generator included).
        var before = Json(a.State);
        Reject(a, new("passSearch", 0, a.Version)); Reject(a, Select(a, "no-such-card"));
        Equal(Json(a.State), before);

        // Each Dynasty-deck shuffle is a deterministic function of the state too.
        var d1 = MatchEngine.CreateDeckSearchShowcase(7); var d2 = MatchEngine.CreateDeckSearchShowcase(7);
        foreach (var d in new[] { d1, d2 }) { Accept(d, Use(d, find(d, Shizuka))); Accept(d, ChooseDeck(d, "Dynasty")); Accept(d, Select(d, FallowLands)); Accept(d, ChooseProvince(d, 0)); }
        Equal(Json(d1.State.Players[0].DynastyDeck), Json(d2.State.Players[0].DynastyDeck));
        Equal(Json(MatchEngine.Replay(d1.ExportReplay()).State), Json(d1.State));
    }

    private static void SetupShufflesUnchanged()
    {
        // Recorded from origin/main before this feature existed (re-pinned by definition when card ids became opaque): SHA-256 (first 16 hex digits) of each player's Dynasty
        // deck order, Fate deck order, opening hand and province cards. Any change to how setup consumes the generator
        // changes one of these.
        var golden = new Dictionary<string, string[]>
        {
            ["starter 1"] = ["CE57DDB95720EB22", "5E30152FB8FACFC6"], ["starter 42"] = ["6C85BDF581E790B2", "DD3D7560702197CC"], ["starter 801"] = ["86EE0F9E44456744", "910ACB968F30497B"],
            ["test 1"] = ["C19E786CA3E959E7", "7690256BD01EA9D3"], ["test 42"] = ["0FB68796D66E5553", "C01708755F7CEC80"], ["test 801"] = ["2BEBF8EDE59F536C", "FCC205FFF2801E51"]
        };
        foreach (var (key, expected) in golden)
        {
            var seed = ulong.Parse(key.Split(' ')[1]);
            var state = (key.StartsWith("starter") ? MatchEngine.CreateStarterMatch(seed) : MatchEngine.CreatePublishedTestMatch(seed)).State;
            // Card ids are an opaque permutation (hidden-information fix), so each position is hashed by the definition it holds.
            string Defs(IEnumerable<string?> ids) => Json(ids.Select(id => id is null ? null : state.Cards[id].DefinitionId));
            var actual = state.Players.Select(p => Hash(Defs(p.DynastyDeck) + "|" + Defs(p.FateDeck) + "|" + Defs(p.Hand) + "|" + Defs(p.Provinces.Select(x => x.CardId)))).ToArray();
            Equal(string.Join(",", actual), string.Join(",", expected), $"Setup for '{key}' changed");
            // The persisted generator is the setup sequence, continued: four Fisher-Yates shuffles of 40 cards consume 4 * 39 draws.
            Equal(state.RngState, unchecked(seed + 156UL * 0x9E3779B97F4A7C15UL), $"'{key}': nothing but the four setup shuffles may have consumed the generator");
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    // ---- the criteria vocabulary and the optional flag ----

    private static MatchEngine TrainingSearch(string ability, params string[] fateDeck)
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, fateDeck); Put(s, "training-search-source", 0, Zone.Home);
        var e = Engine(s); Accept(e, Use(e, Put1(e), ability)); return e;
    }

    private static void CriteriaVocabulary()
    {
        // Types + non-Unique + a Gold comparison.
        var e = TrainingSearch("training-search-cheap", "training-follower", "training-item", "training-unique-follower", "training-follower-crane-guard", "training-rally", "training-weapon", "training-item");
        Equal(e.State.PendingChoice?.Type, "searchSelect", "A one-deck search skips the deck choice");
        var view = Search(e); Equal(view.Deck, "Fate"); Equal(view.Stage, SearchStage.Select);
        var by = view.Entries!.ToDictionary(x => x.DefinitionId);
        foreach (var ok in new[] { "training-follower", "training-item", "training-weapon" }) Check(by[ok].Eligible, ok + " is a cheap non-Unique Follower or Item");
        Equal(by["training-item"].Count, 2);
        Check(!by["training-unique-follower"].Eligible); Equal(by["training-unique-follower"].Reason, "Unique cards cannot be chosen");
        Check(!by["training-follower-crane-guard"].Eligible); Equal(by["training-follower-crane-guard"].Reason, "GoldCost 4, needs <= 2");
        Check(!by["training-rally"].Eligible); Equal(by["training-rally"].Reason, "Not a cheap non-Unique Follower or Item");
        Reject(e, Select(e, "training-unique-follower")); Reject(e, Select(e, "training-follower-crane-guard")); Reject(e, Select(e, "training-rally"));
        Accept(e, Select(e, "training-weapon"));
        Equal(e.State.Players[0].Hand.Count(id => e.State.Cards[id].DefinitionId == "training-weapon"), 1);

        // A Focus comparison.
        var f = TrainingSearch("training-search-focus", "training-focus-strategy", "training-duel-challenge", "training-rally", "training-item");
        var fb = Search(f).Entries!.ToDictionary(x => x.DefinitionId);
        Check(!fb["training-focus-strategy"].Eligible); Equal(fb["training-focus-strategy"].Reason, "Focus 3, needs <= 2");
        Check(fb["training-duel-challenge"].Eligible && fb["training-rally"].Eligible && !fb["training-item"].Eligible);
        Reject(f, Select(f, "training-focus-strategy")); Accept(f, Select(f, "training-duel-challenge"));
    }

    private static void OptionalSearch()
    {
        var e = TrainingSearch("training-search-optional", "training-weapon", "training-item", "training-kama");
        var view = Search(e);
        Check(view.Optional && view.CanPass, "A printed 'you may search' can be declined even while a Weapon qualifies");
        Check(view.Entries!.Any(x => x.Eligible));
        var offered = e.QueryActions(0).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type).ToArray();
        Check(offered.Contains("passSearch") && offered.Contains("selectSearchCard"), "Both taking a card and declining are offered");
        Accept(e, PassSearch(e));
        Equal(e.State.ShownCards.Count, 0); Equal(e.State.PendingChoice, null);
        AssertOrder(e, "searchPassed", "deckShuffled");
        Check(e.GetView(1).Events.Any(ev => ev.Type == "searchPassed" && ev.Message.Contains("chose not to take a card")), "Declining is announced as a choice, not as finding nothing");
        // Taking the card still works too.
        var e2 = TrainingSearch("training-search-optional", "training-weapon", "training-item");
        Accept(e2, Select(e2, "training-weapon")); Equal(e2.State.ShownCards.Count, 1);
        // A required search (Shizuka, over a Dynasty deck holding a Region) is not optional.
        var s = Start(); Deck(s, 0, Zone.DynastyDeck, FallowLands); var r = Searching(s); Accept(r, ChooseDeck(r, "Dynasty"));
        Check(!Search(r).Optional && !Search(r).CanPass); Reject(r, PassSearch(r));
    }

    // ---- Imperial Gift ----

    /// <summary>Replaces player <paramref name="owner"/>'s card in a province with a fresh FACE-DOWN card, as after a refill.</summary>
    private static CardInstance SetProvince(MatchState s, int owner, int index, string definition)
    {
        var province = s.Players[owner].Provinces[index];
        if (province.CardId is string old) s.Cards.Remove(old);
        var card = Put(s, definition, owner, Zone.Province, province: index); card.FaceUp = false; province.CardId = card.Id;
        return card;
    }

    private static MatchState GiftState(params string[] fateDeck)
    {
        var s = Start();
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; // player 0 will end their turn; player 1's Events Phase reveals the Gift
        SetProvince(s, 1, 0, ImperialGift);
        Deck(s, 1, Zone.FateDeck, fateDeck);
        Deck(s, 1, Zone.DynastyDeck, "training-lion-samurai", "training-lion-courtier", "training-lion-veteran", "training-lion-dignitary");
        return s;
    }

    /// <summary>Ends player 0's turn so player 1's Straighten and Events Phases run (paying the hand-limit discard if it comes up).</summary>
    private static MatchEngine EndTurn(MatchState s)
    {
        var e = Engine(s); Accept(e, new("endDynasty", 0, e.Version));
        if (e.State.PendingChoice?.Type == "discardHand") Accept(e, new("discardHand", 0, e.Version, CardIds: e.State.Players[0].Hand.Take(e.State.Players[0].Hand.Count - 8).ToArray()));
        return e;
    }

    private static void ImperialGiftEvent()
    {
        var s = GiftState("training-rally", "training-item", "training-pressure", "training-weapon", "training-follower", "training-item");
        var honor = s.Players[1].Honor; var handCount = s.Players[1].Hand.Count;
        var gift = s.Players[1].Provinces[0].CardId!;
        var e = EndTurn(s);
        // Player 1's Straighten -> Events: the Gift is revealed, resolved: +2 Honor, then the Fate search opens (one deck: straight to the list).
        Equal(e.State.ActivePlayerId, 1); Equal(e.State.Phase, Phase.Events, "The reveal is mid-flight while the search is open");
        Equal(e.State.PendingChoice?.Type, "searchSelect"); Equal(e.State.PriorityPlayerId, 1);
        Equal(e.State.Players[1].Honor, honor + 2, "Gain 2 Honor first");
        Check(e.State.Cards[gift].FaceUp && e.State.Cards[gift].Zone == Zone.Province, "The Event stays face up in its province while its text resolves");
        var view = e.GetView(1).PendingSearch!;
        Equal(view.SourceName, "Imperial Gift"); Equal(view.Deck, "Fate"); Equal(view.Criteria, "Item");
        Check(view.Entries!.Where(x => x.Eligible).Select(x => x.DefinitionId).Order().SequenceEqual(new[] { "training-item", "training-weapon" }), "Items only");
        Equal(view.Entries!.Single(x => x.DefinitionId == "training-item").Count, 2);
        Equal(view.Entries!.Single(x => x.DefinitionId == "training-rally").Reason, "Not an Item");
        Check(!view.CanPass); Check(e.GetView(0).PendingSearch is null, "The opponent sees the face-up Event and that a search happened, not the list");
        AssertSearcherHasAnswer(e);
        Reject(e, Select(e, "training-rally", player: 1)); Reject(e, PassSearch(e, player: 1)); Reject(e, Select(e, "training-item", player: 0));
        var fateBefore = e.State.Players[1].FateDeck.ToArray(); var taken = e.State.Cards.Values.Where(c => c.OwnerId == 1 && c.DefinitionId == "training-weapon" && c.Zone == Zone.FateDeck).Single().Id;
        Accept(e, Select(e, "training-weapon", player: 1));
        var after = e.State;
        Equal(after.Players[1].Hand.Count, handCount + 1, "The Item is in the hand");
        Equal(after.ShownCards.Count, 1); Equal(after.ShownCards[0].PlayerId, 1); Equal(after.ShownCards[0].DefinitionId, "training-weapon");
        Equal(e.GetView(0).ShownCards![0].SourceName, "Imperial Gift");
        Equal(after.Cards[gift].Zone, Zone.DynastyDiscard, "The Event resolved and was discarded");
        Check(after.Players[1].Provinces[0].CardId != gift, "The province refilled");
        Equal(after.Phase, Phase.Action, "The Events Phase completed"); Equal(after.PendingChoice, null); Check(after.PendingSearch is null);
        AssertOrder(e, "honorChanged", "searchStarted", "deckSearched", "cardShown", "searchToHand", "deckShuffled", "eventResolved");
        var expectedRest = fateBefore.Where(id => id != taken).ToArray();
        Check(after.Players[1].FateDeck.OrderBy(x => x).SequenceEqual(expectedRest.OrderBy(x => x)) && !after.Players[1].FateDeck.SequenceEqual(expectedRest), "The Fate deck was shuffled after the card was taken");
        Check(after.Players[1].Hand.Contains(taken) && after.Cards[taken].Zone == Zone.Hand);

        // No Item in the deck: Pass, still shuffled, the Event still resolves and the phase completes.
        var s2 = GiftState("training-rally", "training-pressure", "training-follower", "training-rally", "training-pressure", "training-follower");
        var e2 = EndTurn(s2);
        Equal(e2.State.PendingChoice?.Type, "searchSelect"); Check(Search(e2, 1).CanPass);
        Equal(string.Join(",", e2.QueryActions(1).Where(a => a.Enabled && a.Type != "concede").Select(a => a.Type)), "passSearch");
        var before = e2.State.Players[1].FateDeck.ToArray();
        Accept(e2, PassSearch(e2, player: 1));
        Equal(e2.State.Phase, Phase.Action); Equal(e2.State.ShownCards.Count, 0);
        Check(e2.State.Players[1].FateDeck.OrderBy(x => x).SequenceEqual(before.OrderBy(x => x)) && !e2.State.Players[1].FateDeck.SequenceEqual(before), "Shuffled with nothing taken");
        Equal(e2.State.Players[1].Honor, s2.Players[1].Honor + 2, "The Honor was gained regardless");
        AssertOrder(e2, "searchStarted", "searchPassed", "deckShuffled", "eventResolved");
    }

    private static void EventsRevealResumesFromNextProvince()
    {
        // Imperial Gift in province 1, a Region revealed before it (province 0), Suspicions revealed after it (province 2).
        var s = GiftState("training-item", "training-rally", "training-weapon", "training-rally", "training-item", "training-rally");
        var region = SetProvince(s, 1, 0, FallowLands);
        var gift = SetProvince(s, 1, 1, ImperialGift).Id;
        var suspicions = SetProvince(s, 1, 2, "oracle-l5r-7675");
        var e = EndTurn(s);
        Equal(e.State.PendingChoice?.Type, "searchSelect");
        Equal(e.State.Cards[region.Id].Zone, Zone.Region, "The province before it was revealed and resolved first");
        Equal(e.State.Cards[suspicions.Id].Zone, Zone.Province, "The province after it has not been revealed yet");
        Accept(e, Select(e, "training-item", player: 1));
        Equal(e.State.Cards[suspicions.Id].Zone, Zone.EventInPlay, "The reveal resumed with the next province");
        Equal(e.State.Phase, Phase.Action);
        // The province refilled by the deferred Event holds a face-down card: it was not revealed in the same phase.
        var refilled = e.State.Players[1].Provinces[1].CardId!;
        Check(refilled != gift && !e.State.Cards[refilled].FaceUp, "The card that refilled the Event's own province stays face down");
        var refilled0 = e.State.Players[1].Provinces[0].CardId!;
        Check(!e.State.Cards[refilled0].FaceUp, "So does the province refilled before it");

        // Harsh Choices, declined: the refilled card is not revealed in the same Events Phase either (the shared resume path).
        var h = Start(); h.Phase = Phase.Dynasty;
        var harsh = SetProvince(h, 1, 0, "oracle-l5r-2978");
        Deck(h, 1, Zone.DynastyDeck, "training-lion-samurai", "training-lion-courtier", "training-lion-veteran", "training-lion-dignitary");
        var he = EndTurn(h);
        Equal(he.State.PendingChoice?.Type, "harshChoices");
        Accept(he, new("harshChoices", 1, he.Version, Choice: "decline"));
        var newCard = he.State.Players[1].Provinces[0].CardId!;
        Check(newCard != harsh.Id && !he.State.Cards[newCard].FaceUp, "Harsh Choices' province refilled face down and stays so this phase");
        Equal(he.State.Phase, Phase.Action);
    }

    // ---- no deadlock ----

    private static void SearcherAlwaysHasALegalAnswer()
    {
        // Walk every branch of a search by always taking the first offered enabled action (what a bot does), across seeds.
        foreach (var seed in new ulong[] { 1, 2, 3, 42, 20260906 })
            foreach (var ability in new[] { "shizuka-search", "training-search-cheap", "training-search-focus", "training-search-optional" })
                foreach (var forceDeck in new[] { "Dynasty", "Fate" })
                {
                    if (ability != "shizuka-search" && forceDeck == "Dynasty") continue;
                    var e = MatchEngine.CreateDeckSearchShowcase(seed);
                    var source = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && (CardCatalog.Get(c.DefinitionId).Abilities ?? []).Contains(ability)).Id;
                    Accept(e, Use(e, source, ability));
                    var steps = 0;
                    while (e.State.PendingChoice is { } pending && pending.Type.StartsWith("search"))
                    {
                        AssertSearcherHasAnswer(e);
                        var offered = e.QueryActions(pending.PlayerId).Where(a => a.Enabled && a.Type is not ("concede" or "wait")).ToArray();
                        var pick = offered.FirstOrDefault(a => a.Type == "chooseSearchDeck" && a.Command!.Choice == forceDeck) ?? offered[0];
                        // Every offered command validates on a fresh engine from the same state (the offered-actions contract).
                        var state = e.State;
                        foreach (var option in offered) Accept(MatchEngine.FromTestState(state), option.Command!);
                        Accept(e, pick.Command!);
                        Check(++steps < 8, "The search did not finish in a bounded number of decisions");
                    }
                    Check(e.State.PendingSearch is null && e.State.PendingChoice is null, $"{ability}/{forceDeck}/{seed}: the search finished");
                    Equal(e.State.PriorityPlayerId, 1, "The action resolved and play continued");
                }
    }
}
