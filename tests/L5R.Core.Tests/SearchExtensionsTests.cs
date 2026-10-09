using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #728, primitive P20 (SearchExtensions.cs, DeckSearch.cs, DeckLook.cs): the search and look extensions, with no card wired. Every piece is driven by test-only specs registered below (all keyed on
// "training-p20-" ids, so no other suite sees them): the three new sources, ChooseOne against Ordered, the GoldProducing and DifferentTitles criteria, a selection of several cards, the dispositions
// AttachToPersonality, Bottom, CelestialIntoPlay, RecruitFromProvinceIgnoringCosts and PlayAsAdditionalAction, the other player's choice, and a reveal-until look. A search is started through the
// BeginSearchForTest seam and then driven only through the commands QueryActions offers (or the illegal ones the engine must refuse).
internal static class SearchExtensionsTests
{
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai", Item = "training-item", Weapon = "training-weapon", Kama = "training-kama";
    // Two Holdings that appear nowhere on the table before a test places them (found by looking at the other seat's view of the bare table).
    private static string[] Decoys => Pool.Where(d => !JsonSerializer.Serialize(MatchEngine.FromTestState(Start()).GetView(1)).Contains(d, StringComparison.Ordinal)).Take(2).ToArray();
    private static readonly string[] Pool = ["training-duel-reactor", "training-expensive-holding", "training-bamboo", "training-marketplace", "training-castle", "training-farm"];
    private static string Hidden1 => Decoys[0];
    private static string Hidden2 => Decoys[1];
    private const string BorderKeep = "training-border-keep", Farm = "training-farm", Castle = "training-castle", Armory = "training-discard-armory";
    private const string Ebisu = "oracle-l5r-2276", Megumi = "oracle-l5r-10028", ExtendedManeuvers = "oracle-l5r-2419", FloodedPass = "oracle-l5r-2613";

    public static void Run(TestRunner runner) => runner.Run("search-extensions", new (string, Action)[]
    {
        ("OwnProvinces: face-up and face-down province cards are listed to the searcher only, nothing is shuffled, only the chosen card is shown, and the province is refilled face down", ProvinceSearchIsPrivate),
        ("RecruitFromProvinceIgnoringCosts: a Personality found in a province (even face down, even above the Honor Requirement) enters play at home with no payment, and the province refills; a Unique copy in play bars it", ProvinceRecruit),
        ("AnyPlayerDynastyDiscard and AnyPlayerFateDiscard: either player's pile is listed, a card of the other player's is put into play, a Celestial replaces the controller's, and an Item is attached, all under the searcher's control; nothing is shuffled", AnyPlayerPiles),
        ("ChooseOne offers every source; Ordered still forces the first (R-011) and wins over ChooseOne, the next place opening only after a pass", ChooseOneAndOrdered),
        ("GoldProducing lists only Gold-producing Holdings with a reason for the rest; Bottom keeps the card in the deck, shuffles it, and only then puts it last", GoldProducingAndBottom),
        ("DifferentTitles: a second pick with the same title is refused with its reason; the count is Min to Max, and what cannot be reached (too few cards, too few titles) is not required", DifferentTitlesAndSelectCount),
        ("SelectCount: the whole selection is one command, shown in order, taken from the lowest ids; Min 0 lets the searcher take none; the offered default selection is legal and validates on a fresh engine", SelectCountBounds),
        ("AttachToPersonality: a follow-up target slot offers only Personalities the card can attach to, pays no Gold, a single host needs no decision, and no host makes the card ineligible", AttachToPersonality),
        ("CelestialIntoPlay: the Celestial enters through the shared entry, the controller's other Celestials are discarded and no province is refilled", CelestialIntoPlay),
        ("PlayAsAdditionalAction: the found card goes to the hand and is played with a mandatory additional action, or discarded when it cannot be played", PlayAsAdditionalAction),
        ("The other player chooses: he is offered exactly the shown definitions, sees nothing else, the chosen card takes the chosen disposition and the rest the option's own", OtherPlayerChooses),
        ("A reveal-until look reveals the top cards up to the first match publicly, forces the match to its disposition, puts the rest where the spec says, and reveals the whole deck when nothing matches", RevealUntilLook),
        ("A game saved in the middle of a multi-card selection, an attach follow-up, the other player's choice and a reveal-until look resumes exactly as the original", MidSearchRoundTrips),
        ("Refusals leave the state unchanged: the wrong player, the wrong command, an unlisted or ineligible card, a pass while cards qualify, and a spec combining things the flow cannot do", RefusalsLeaveStateUnchanged),
        ("Unused means unchanged: a search that uses none of the extensions saves and shows none of the new fields, and every catalogued spec but the fixtures carries the defaults", UnusedMeansUnchanged),
    });

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static string Seq(IEnumerable<string> items) => string.Join(",", items);

    // ---- the fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string ProvDiscard = "training-p20-prov-discard", ProvRecruit = "training-p20-prov-recruit", ProvBottom = "training-p20-prov-bottom", AnyDynasty = "training-p20-any-dynasty",
        AnyCelestial = "training-p20-any-celestial", AnyFateAttach = "training-p20-any-fate-attach", ChooseSpec = "training-p20-choose", OrderedSpec = "training-p20-ordered", GoldBottom = "training-p20-gold-bottom",
        TwoItems = "training-p20-two-items", UpToThree = "training-p20-up-to-three", NeedFour = "training-p20-need-four", UpToTwoOrNone = "training-p20-up-to-two-or-none", AttachSpec = "training-p20-attach",
        CelestialSpec = "training-p20-celestial", PlaySpec = "training-p20-play", OtherSpec = "training-p20-other", BadForeign = "training-p20-bad-foreign", BadMany = "training-p20-bad-many",
        BadRecruit = "training-p20-bad-recruit", BadOther = "training-p20-bad-other", PlainSpec = "training-p20-plain", RevealLook = "training-p20-reveal", RevealOther = "training-p20-reveal-none";
    private static readonly SearchCriteria AnyCard = new("card");

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.Searches.Register("TestP20Searches", 19700, static () =>
        [
            new SearchSpec(ProvDiscard, "Search your provinces for a card", [new(SearchDeck.OwnProvinces, AnyCard, SearchEffect.Discard)]),
            new SearchSpec(ProvRecruit, "Search your provinces for a Personality", [new(SearchDeck.OwnProvinces, new("Personality", [CardType.Personality]), SearchEffect.RecruitFromProvinceIgnoringCosts)]),
            new SearchSpec(ProvBottom, "Search your provinces for up to two cards with different titles", [new(SearchDeck.OwnProvinces, new("card", DifferentTitles: true), SearchEffect.Bottom)], SelectCount: new(1, 2)),
            new SearchSpec(AnyDynasty, "Search any Dynasty discard pile for a Personality", [new(SearchDeck.AnyPlayerDynastyDiscard, new("Personality", [CardType.Personality]), SearchEffect.PutIntoPlay)]),
            new SearchSpec(AnyCelestial, "Search any Dynasty discard pile for a Celestial", [new(SearchDeck.AnyPlayerDynastyDiscard, new("Celestial", [CardType.Celestial]), SearchEffect.CelestialIntoPlay)]),
            new SearchSpec(AnyFateAttach, "Search any Fate discard pile for an Item", [new(SearchDeck.AnyPlayerFateDiscard, new("Item", [CardType.Item]), SearchEffect.AttachToPersonality)]),
            new SearchSpec(ChooseSpec, "Search your provinces or your Fate deck", [new(SearchDeck.OwnProvinces, AnyCard, SearchEffect.Discard), new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)], ChooseOne: true),
            new SearchSpec(OrderedSpec, "Search your Fate discard pile, then your Fate deck", [new(SearchDeck.FateDiscard, new("Item", [CardType.Item]), SearchEffect.ToHand), new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)],
                Ordered: true, ChooseOne: true),
            new SearchSpec(GoldBottom, "Search your Dynasty deck for a Gold-producing Holding", [new(SearchDeck.Dynasty, new("Gold-producing Holding", [CardType.Holding], GoldProducing: true), SearchEffect.Bottom)]),
            new SearchSpec(TwoItems, "Search your Fate deck for two Items with different titles", [new(SearchDeck.Fate, new("Item", [CardType.Item], DifferentTitles: true), SearchEffect.ToHand)], SelectCount: new(2, 2)),
            new SearchSpec(UpToThree, "Search your Fate deck for one to three Items", [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)], SelectCount: new(1, 3)),
            new SearchSpec(NeedFour, "Search your Fate deck for four Items", [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)], SelectCount: new(4, 4)),
            new SearchSpec(UpToTwoOrNone, "Search your Fate deck for up to two Items", [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)], SelectCount: new(0, 2)),
            new SearchSpec(AttachSpec, "Search your Fate deck for an Item and attach it", [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.AttachToPersonality)]),
            new SearchSpec(CelestialSpec, "Search your Dynasty deck for a Celestial", [new(SearchDeck.Dynasty, new("Celestial", [CardType.Celestial]), SearchEffect.CelestialIntoPlay)]),
            new SearchSpec(PlaySpec, "Search your Fate deck for a Terrain and play it", [new(SearchDeck.Fate, new("Terrain", [CardType.Strategy], ["Terrain"]), SearchEffect.PlayAsAdditionalAction)]),
            new SearchSpec(OtherSpec, "Search your Fate deck for two Items; the other player chooses one to discard", [new(SearchDeck.Fate, new("Item", [CardType.Item], DifferentTitles: true), SearchEffect.ToHand)],
                SelectCount: new(1, 2), OtherPlayerChooses: new(SearchEffect.Discard)),
            new SearchSpec(PlainSpec, "Search your Fate deck for an Item", [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.ToHand)]),
            new SearchSpec(BadForeign, "Invalid: a card of the other player's to the hand", [new(SearchDeck.AnyPlayerFateDiscard, AnyCard, SearchEffect.ToHand)]),
            new SearchSpec(BadMany, "Invalid: several cards with a follow-up", [new(SearchDeck.Fate, AnyCard, SearchEffect.AttachToPersonality)], SelectCount: new(1, 2)),
            new SearchSpec(BadRecruit, "Invalid: a recruit from a deck", [new(SearchDeck.Dynasty, AnyCard, SearchEffect.RecruitFromProvinceIgnoringCosts)]),
            new SearchSpec(BadOther, "Invalid: the other player chooses among one", [new(SearchDeck.Fate, AnyCard, SearchEffect.ToHand)], OtherPlayerChooses: new(SearchEffect.Discard))
        ]);
        EngineHooks.Looks.Register("TestP20Looks", 19701, static () =>
        [
            new LookSpec(RevealLook, "Reveal cards from your Fate deck until you reveal an Item", [new("Fate")], 0, [], LookDisposition.Bottom, Reorder: true, RevealUntil: new("Item", [CardType.Item])),
            new LookSpec(RevealOther, "Reveal until an Item; discard the rest", [new("Fate")], 0, [], LookDisposition.Discard, RevealUntil: new("Item", [CardType.Item]), MatchDisposition: LookDisposition.Show)
        ]);
    }

    // The economy table with both decks, hands and provinces emptied: every card in a test is one the test put there. Player 0 is to act in the Action Phase.
    private static MatchState Start(Phase phase = Phase.Action)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.DynastyDeck or Zone.FateDeck or Zone.Hand or Zone.Province).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.DynastyDeck.Clear(); p.FateDeck.Clear(); p.Hand.Clear(); foreach (var province in p.Provinces) province.CardId = null; }
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    private static int _n;
    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, int? province = null, bool? faceUp = null)
    {
        var c = new CardInstance { Id = $"p20-{_n++}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp ?? zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck), ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        if (zone == Zone.Province) p.Provinces[province!.Value].CardId = c.Id;
        return c;
    }

    private static void Deck(MatchState s, int owner, Zone zone, params string[] definitions) { foreach (var d in definitions) Put(s, d, owner, zone); }
    private static MatchEngine Searching(MatchState s, string spec, string sourceId, int player = 0)
    {
        var e = MatchEngine.FromTestState(s);
        e.BeginSearchForTest(spec, sourceId, player);
        return e;
    }

    private static GameCommand Take(MatchEngine e, int player, params string[] definitions) => new("selectSearchCards", player, e.Version, CardIds: definitions);
    private static GameCommand One(MatchEngine e, string definition, int player = 0) => new("selectSearchCard", player, e.Version, Choice: definition);
    private static GameCommand ChooseSource(MatchEngine e, string deck, int player = 0) => new("chooseSearchDeck", player, e.Version, Choice: deck);
    private static GameCommand Pass(MatchEngine e, int player = 0) => new("passSearch", player, e.Version);
    private static SearchView View(MatchEngine e, int player = 0) => e.GetView(player).PendingSearch ?? throw new Exception("No search view for player " + player);
    private static string[] Eligible(MatchEngine e) => View(e).Entries!.Where(x => x.Eligible).Select(x => x.DefinitionId).ToArray();
    private static string[] EventTypes(MatchEngine e) => e.State.Events.Select(x => x.Type).ToArray();

    // Every command the engine offers the searcher is a complete command that validates on a fresh engine copy.
    private static void OfferedCommandsValidate(MatchEngine e)
    {
        var pending = e.State.PendingChoice ?? throw new Exception("No pending choice.");
        var offered = e.QueryActions(pending.PlayerId).Where(a => a.Enabled && a.Type is not ("concede" or "wait")).ToArray();
        Check(offered.Length > 0, $"The decider has no offered answer at {pending.Type}: a bot would stall");
        foreach (var action in offered)
        {
            Check(action.Command is not null, "an offered answer is a complete command");
            var copy = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(Json(e))!);
            Check(copy.Execute(action.Command!).Accepted, $"offered {action.Type} '{action.Label}' is accepted on a fresh engine");
        }
    }

    // ---- OwnProvinces ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ProvinceSearchIsPrivate()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        var baseline = JsonSerializer.Serialize(MatchEngine.FromTestState(Start()).GetView(1));
        Check(!baseline.Contains(Hidden1, StringComparison.Ordinal) && !baseline.Contains(Hidden2, StringComparison.Ordinal), "the decoys appear nowhere else on the table");
        var down = Put(s, Hidden1, 0, Zone.Province, 0, false); var up = Put(s, Farm, 0, Zone.Province, 1, true); var other = Put(s, Hidden2, 0, Zone.Province, 2, false);
        var theirs = Put(s, Armory, 1, Zone.Province, 0, false);
        Deck(s, 0, Zone.DynastyDeck, "training-marketplace", "training-marketplace");
        var e = Searching(s, ProvDiscard, source.Id);
        var rng = e.State.RngState;
        // The searcher sees all three of his own province cards, the face-down ones included, and none of the opponent's.
        Eq(Seq(View(e).Entries!.Select(x => x.DefinitionId).Order(StringComparer.Ordinal)), Seq(new[] { Hidden1, Hidden2, Farm }.Order(StringComparer.Ordinal)), "the province cards are listed");
        Eq(View(e).Place, "provinces", "the viewer names the place");
        Check(e.GetView(1).PendingSearch is null, "the other seat has no search view");
        var theirView = JsonSerializer.Serialize(e.GetView(1));
        foreach (var hidden in new[] { down.Id, other.Id, Hidden1, Hidden2 }) Check(!theirView.Contains(hidden, StringComparison.Ordinal), $"the other seat's view leaks {hidden}");
        Check(theirView.Contains(Farm, StringComparison.Ordinal), "the face-up province card was never hidden");
        Check(!JsonSerializer.Serialize(View(e)).Contains(Armory, StringComparison.Ordinal), "the opponent's province cards are not searched");
        OfferedCommandsValidate(e);
        // Taking the face-down card: shown (now public), discarded, the province refilled from the Dynasty deck face down; nothing is reshuffled.
        Accept(e, One(e, Hidden1));
        Eq(e.State.ShownCards.Count, 1); Eq(e.State.ShownCards[0].DefinitionId, Hidden1);
        Eq(e.State.Cards[down.Id].Zone, Zone.DynastyDiscard, "the found card was discarded");
        var refill = e.State.Players[0].Provinces[0].CardId;
        Check(refill is not null && e.State.Cards[refill].Zone == Zone.Province && !e.State.Cards[refill].FaceUp, "the province refilled face down");
        Eq(e.State.Players[0].DynastyDeck.Count, 1, "one card left the Dynasty deck for the refill");
        Check(e.State.Cards[up.Id].Zone == Zone.Province && e.State.Cards[other.Id].Zone == Zone.Province, "the other province cards stay");
        Eq(e.State.RngState, rng, "no generator draw: nothing was reshuffled");
        Check(!EventTypes(e).Contains("deckShuffled"), "no shuffle was announced");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        // The other seat learned only the one card that was shown.
        var after = JsonSerializer.Serialize(e.GetView(1));
        Check(!after.Contains(other.Id, StringComparison.Ordinal) && !after.Contains(refill!, StringComparison.Ordinal), "no instance id of a card that stayed hidden reached the other seat (the discarded one is in a public pile)");
        Check(e.State.Events.Where(ev => ev.Type == "cardShown").All(ev => ev.CardId is null), "the shown event names no instance id");
        Check(!after.Contains(Hidden2, StringComparison.Ordinal), "the unchosen face-down card stayed hidden");
        // The opponent cannot answer, and a card from his province cannot be named.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        Put(s2, Hidden1, 0, Zone.Province, 0, false); Put(s2, Armory, 1, Zone.Province, 0, false);
        var f = Searching(s2, ProvDiscard, src2.Id);
        Reject(f, One(f, Hidden1, player: 1)); Reject(f, One(f, Armory)); Reject(f, Pass(f));
    }

    private static void ProvinceRecruit()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        s.Players[0].Honor = 0;
        var veteran = Put(s, "training-lion-veteran", 0, Zone.Province, 0, false); // Honor Requirement 6, cost 8, face down
        var holding = Put(s, Castle, 0, Zone.Province, 1, true);
        var unique = Put(s, "training-unique-personality", 0, Zone.Province, 2, true);
        Put(s, "training-unique-personality", 0, Zone.Home);
        Deck(s, 0, Zone.DynastyDeck, "training-marketplace");
        var e = Searching(s, ProvRecruit, source.Id);
        Eq(Seq(Eligible(e)), "training-lion-veteran", "only the Personality the player may bring into play");
        var reasons = View(e).Entries!.Where(x => !x.Eligible).ToDictionary(x => x.DefinitionId, x => x.Reason!);
        Check(reasons[Castle].StartsWith("Not a", StringComparison.Ordinal) && reasons["training-unique-personality"].Contains("already control", StringComparison.Ordinal), "reasons: " + Seq(reasons.Values));
        Reject(e, One(e, "training-unique-personality")); Reject(e, One(e, Castle));
        var honor = e.State.Players[0].Honor;
        Accept(e, One(e, "training-lion-veteran"));
        var card = e.State.Cards[veteran.Id];
        Eq(card.Zone, Zone.Home, "he entered play at home"); Check(card.FaceUp, "face up");
        Eq(e.State.Players[0].Honor, honor, "no Honor was spent"); Check(!EventTypes(e).Contains("payment"), "no payment: the costs are ignored");
        Check(e.State.Cards[e.State.Players[0].Provinces[0].CardId!].Zone == Zone.Province, "the province refilled");
        Eq(e.State.LastProvinceEntryCardId, veteran.Id, "recorded as a province entry");
        Check(e.State.PendingSearch is null, "the search is over");
    }

    // ---- either player's piles ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AnyPlayerPiles()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        var mine = Put(s, Lion, 0, Zone.DynastyDiscard); var theirs = Put(s, Crane, 1, Zone.DynastyDiscard); var dead = Put(s, "training-crane-veteran", 1, Zone.Dead);
        var e = Searching(s, AnyDynasty, source.Id);
        var rng = e.State.RngState;
        Eq(Seq(Eligible(e).Order(StringComparer.Ordinal)), Seq(new[] { Lion, Crane, "training-crane-veteran" }.Order(StringComparer.Ordinal)), "both players' piles, the dead pile included");
        Eq(View(e).Place, "Dynasty discard piles");
        Accept(e, One(e, Crane));
        var entered = e.State.Cards[theirs.Id];
        Eq(entered.Zone, Zone.Home, "put into play"); Eq(entered.OwnerId, 0, "controlled by the searcher"); Eq(entered.OriginalOwnerId, 1, "still the other player's card");
        Eq(e.State.RngState, rng, "a discard pile is never shuffled"); Check(!EventTypes(e).Contains("deckShuffled"));
        // A Celestial from the other player's pile replaces the searcher's own, with no province refilled.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        var own = Put(s2, Megumi, 0, Zone.Celestial); var foreign = Put(s2, Ebisu, 1, Zone.DynastyDiscard);
        var province = Put(s2, Castle, 0, Zone.Province, 0, false);
        Deck(s2, 0, Zone.DynastyDeck, "training-marketplace");
        var f = Searching(s2, AnyCelestial, src2.Id);
        Accept(f, One(f, Ebisu));
        Eq(f.State.Cards[foreign.Id].Zone, Zone.Celestial, "in play"); Eq(f.State.Cards[foreign.Id].OwnerId, 0, "under the searcher's control");
        Eq(f.State.Cards[own.Id].Zone, Zone.DynastyDiscard, "the controller's other Celestial was discarded");
        Eq(f.State.Players[0].Provinces[0].CardId, province.Id, "no province was refilled or touched"); Eq(f.State.Players[0].DynastyDeck.Count, 1);
        // An Item from either Fate discard pile is attached to a Personality of the searcher's.
        var s3 = Start();
        var host = Put(s3, Lion, 0, Zone.Home);
        var theirItem = Put(s3, Item, 1, Zone.FateDiscard); Put(s3, Weapon, 0, Zone.FateDiscard); Put(s3, Item, 0, Zone.Hand);
        var g = Searching(s3, AnyFateAttach, host.Id);
        Eq(Seq(Eligible(g).Order(StringComparer.Ordinal)), Seq(new[] { Item, Weapon }.Order(StringComparer.Ordinal)));
        Accept(g, One(g, Item));
        var attached = g.State.Cards[theirItem.Id];
        Eq(attached.Zone, Zone.Attached); Eq(attached.AttachedTo, host.Id, "a single host needs no decision"); Eq(attached.OwnerId, 0); Eq(attached.OriginalOwnerId, 1);
        Check(g.State.PendingSearch is null && !EventTypes(g).Contains("deckShuffled"), "finished, nothing shuffled");
    }

    // ---- ChooseOne against Ordered ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChooseOneAndOrdered()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Put(s, Castle, 0, Zone.Province, 0, false); Put(s, Item, 0, Zone.FateDeck); Put(s, Weapon, 0, Zone.FateDiscard);
        var e = Searching(s, ChooseSpec, source.Id);
        Eq(View(e).Stage, SearchStage.ChooseDeck, "the player picks the source");
        Eq(Seq(View(e).DeckChoices!.Select(d => d.Deck)), "OwnProvinces,Fate", "both sources are offered");
        Eq(Seq(View(e).DeckChoices!.Select(d => d.Label)), "Search your provinces,Search your Fate deck");
        Eq(e.QueryActions(0).Count(a => a.Type == "chooseSearchDeck"), 2);
        Reject(e, ChooseSource(e, SearchDeck.Hand)); // not a source of this search
        Accept(e, ChooseSource(e, SearchDeck.Fate));
        Eq(View(e).Deck, SearchDeck.Fate); Eq(Seq(Eligible(e)), Item);
        // Ordered: the first source is forced, with no choice, even though ChooseOne is set.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        Put(s2, Item, 0, Zone.FateDeck); Put(s2, Weapon, 0, Zone.FateDeck); Put(s2, "training-spell", 0, Zone.FateDiscard);
        var f = Searching(s2, OrderedSpec, src2.Id);
        Eq(View(f).Stage, SearchStage.Select, "no choice of source"); Eq(View(f).Deck, SearchDeck.FateDiscard, "the first source is searched first");
        Check(f.QueryActions(0).All(a => a.Type != "chooseSearchDeck"), "no source is offered");
        Reject(f, ChooseSource(f, SearchDeck.Fate));
        Check(Eligible(f).Length == 0, "nothing in the discard pile qualifies");
        // A pass at the first place goes on to the next (R-011); the discard pile is not shuffled, the deck searched next is.
        Accept(f, Pass(f));
        Eq(View(f).Deck, SearchDeck.Fate, "the next place is opened only after a pass"); Eq(Seq(Eligible(f).Order(StringComparer.Ordinal)), Seq(new[] { Item, Weapon }.Order(StringComparer.Ordinal)));
        // When the first place holds a card, it must be taken from there.
        var s3 = Start();
        var src3 = Put(s3, Lion, 0, Zone.Home);
        Put(s3, Weapon, 0, Zone.FateDiscard); Put(s3, Item, 0, Zone.FateDeck);
        var g = Searching(s3, OrderedSpec, src3.Id);
        Eq(Seq(Eligible(g)), Weapon); Reject(g, Pass(g)); Reject(g, One(g, Item));
    }

    // ---- GoldProducing and Bottom ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void GoldProducingAndBottom()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.DynastyDeck, BorderKeep, Farm, Castle, Armory, "training-lion-samurai");
        var farm = s.Players[0].DynastyDeck.Select(id => s.Cards[id]).First(c => c.DefinitionId == Farm);
        var e = Searching(s, GoldBottom, source.Id);
        Eq(Seq(Eligible(e).Order(StringComparer.Ordinal)), Seq(new[] { BorderKeep, Farm }.Order(StringComparer.Ordinal)), "only Holdings that produce Gold");
        var reasons = View(e).Entries!.Where(x => !x.Eligible).ToDictionary(x => x.DefinitionId, x => x.Reason!);
        Check(reasons[Castle].StartsWith("Not a", StringComparison.Ordinal) && reasons[Armory].StartsWith("Not a", StringComparison.Ordinal) && reasons[Lion].StartsWith("Not a", StringComparison.Ordinal), "a reason for each other card");
        Reject(e, One(e, Castle));
        Accept(e, One(e, Farm));
        Eq(e.State.Players[0].DynastyDeck.Count, 5, "the card stays in the deck"); Eq(e.State.Players[0].DynastyDeck[^1], farm.Id, "and is put last, after the shuffle");
        Eq(e.State.Cards[farm.Id].Zone, Zone.DynastyDeck);
        Eq(EventTypes(e).Count(t => t == "deckShuffled"), 1, "the searched deck was shuffled");
        Check(EventTypes(e).ToList().IndexOf("searchedCardOnBottom") > EventTypes(e).ToList().IndexOf("deckShuffled"), "the bottom comes after the shuffle");
        Check(e.State.PendingSearch is null);
    }

    // ---- DifferentTitles and SelectCount -----------------------------------------------------------------------------------------------------------------------------------------

    private static void DifferentTitlesAndSelectCount()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Item, Weapon, Kama);
        var e = Searching(s, TwoItems, source.Id);
        Eq(View(e).MinSelect, 2); Eq(View(e).MaxSelect, 2); Eq(View(e).DifferentTitles, true, "the viewer is told what the set must satisfy");
        Reject(e, Take(e, 0, Item, Item));          // two copies, one title
        Reject(e, Take(e, 0, Item));                // too few
        Reject(e, Take(e, 0, Item, Weapon, Kama));  // too many
        Reject(e, Take(e, 0, Item, "no-such-card"));
        Reject(e, One(e, Item));                    // one card is fewer than the minimum
        Reject(e, Pass(e));                         // cards qualify and the minimum is two
        Reject(e, Take(e, 1, Item, Weapon));        // not the searcher
        var refusal = e.Execute(Take(e, 0, Item, Item));
        Check(!refusal.Accepted && refusal.Error!.Contains("different title", StringComparison.Ordinal), "the reason is the title rule: " + refusal.Error);
        Accept(e, Take(e, 0, Weapon, Item));
        Eq(e.State.ShownCards.Count, 2, "each card taken is shown, in the order taken");
        Eq(Seq(e.State.ShownCards.Select(c => c.DefinitionId)), $"{Weapon},{Item}");
        Eq(Seq(e.State.Players[0].Hand.Select(id => e.State.Cards[id].DefinitionId).Order(StringComparer.Ordinal)), Seq(new[] { Item, Weapon }.Order(StringComparer.Ordinal)));
        Eq(e.State.Players[0].FateDeck.Count, 2, "an Item and the Kama remain"); Eq(EventTypes(e).Count(t => t == "deckShuffled"), 1);
        // Fewer different titles than the minimum: the most that can be taken is required, and no more is allowed.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        Deck(s2, 0, Zone.FateDeck, Item, Item);
        var f = Searching(s2, TwoItems, src2.Id);
        Reject(f, Take(f, 0, Item, Item)); Reject(f, Pass(f));
        Accept(f, Take(f, 0, Item));
        Eq(f.State.Players[0].Hand.Count, 1);
    }

    private static void SelectCountBounds()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Item, Weapon);
        var e = Searching(s, UpToThree, source.Id);
        Eq(View(e).MinSelect, 1); Eq(View(e).MaxSelect, 3); Check(View(e).DifferentTitles is null, "titles may repeat");
        Reject(e, Take(e, 0));                                  // none, though a card qualifies
        Reject(e, Take(e, 0, Weapon, Weapon));                  // only one Weapon there
        Reject(e, Take(e, 0, Item, Item, Weapon, Weapon));      // over Max
        OfferedCommandsValidate(e);
        var taken = e.QueryActions(0).Single(a => a.Type == "selectSearchCards");
        Eq(taken.Command!.CardIds!.Length, 3, "the offered default takes the most it may");
        Accept(e, Take(e, 0, Item, Item, Weapon));
        Eq(e.State.Players[0].Hand.Count, 3); Eq(e.State.Players[0].FateDeck.Count, 0);
        Eq(e.State.ShownCards.Count, 3, "all three shown");
        // The two copies of one definition are taken from the lowest ids, whatever order the deck is in.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        var first = Put(s2, Item, 0, Zone.FateDeck); var second = Put(s2, Item, 0, Zone.FateDeck);
        s2.Players[0].FateDeck.Reverse();
        var f = Searching(s2, UpToThree, src2.Id);
        Accept(f, Take(f, 0, Item));
        Eq(f.State.Players[0].Hand[0], first.Id, "the lowest id is the one taken"); Check(f.State.Cards[second.Id].Zone == Zone.FateDeck);
        // Fewer cards than the minimum: what exists is required, and a pass is refused while any qualifies.
        var s3 = Start();
        var src3 = Put(s3, Lion, 0, Zone.Home);
        Deck(s3, 0, Zone.FateDeck, Item, Weapon, Kama);
        var g = Searching(s3, NeedFour, src3.Id);
        Reject(g, Take(g, 0, Item, Weapon)); Reject(g, Pass(g));
        Accept(g, Take(g, 0, Item, Weapon, Kama));
        // Min 0: taking none is legal while cards qualify, and the deck is still shuffled.
        var s4 = Start();
        var src4 = Put(s4, Lion, 0, Zone.Home);
        Deck(s4, 0, Zone.FateDeck, Item, Weapon);
        var h = Searching(s4, UpToTwoOrNone, src4.Id);
        Check(View(h).CanPass && View(h).Optional, "the view allows taking none");
        Check(h.QueryActions(0).Any(a => a.Type == "passSearch" && a.Enabled), "and the engine offers it");
        Accept(h, Pass(h));
        Eq(h.State.ShownCards.Count, 0); Check(EventTypes(h).Contains("deckShuffled")); Eq(h.State.Players[0].Hand.Count, 0);
        // Nothing qualifies: the only answer is Pass.
        var s5 = Start();
        var src5 = Put(s5, Lion, 0, Zone.Home);
        Deck(s5, 0, Zone.FateDeck, "training-spell");
        var k = Searching(s5, TwoItems, src5.Id);
        Check(k.QueryActions(0).Where(a => a.Enabled && a.Type is not ("concede" or "wait")).All(a => a.Type == "passSearch"), "only a pass is offered");
        Accept(k, Pass(k)); Check(k.State.PendingSearch is null);
    }

    // ---- AttachToPersonality ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AttachToPersonality()
    {
        var s = Start();
        var lion = Put(s, Lion, 0, Zone.Home); var crane = Put(s, Crane, 0, Zone.Home); var enemy = Put(s, Crane, 1, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Weapon);
        var item = s.Players[0].FateDeck.Select(id => s.Cards[id]).First(c => c.DefinitionId == Item);
        var e = Searching(s, AttachSpec, lion.Id);
        Accept(e, One(e, Item));
        Eq(e.State.PendingChoice?.Type, "searchAttachTarget", "a follow-up target slot opens");
        Eq(View(e).Stage, SearchStage.ChooseAttachTarget); Eq(View(e).Selected?.Id, Item, "the found card is shown with the decision");
        var offered = e.QueryActions(0).Where(a => a.Type == "chooseSearchAttachTarget").Select(a => a.Command!.TargetId!).Order(StringComparer.Ordinal).ToArray();
        Eq(Seq(offered), Seq(new[] { lion.Id, crane.Id }.Order(StringComparer.Ordinal)), "only Personalities the searcher controls");
        Check(e.GetView(1).PendingSearch is null && e.QueryActions(1).All(a => !a.Enabled || a.Type is "wait" or "concede"), "the other seat only waits");
        OfferedCommandsValidate(e);
        Reject(e, new("chooseSearchAttachTarget", 0, e.Version, TargetId: enemy.Id));    // a Personality of the opponent's
        Reject(e, new("chooseSearchAttachTarget", 0, e.Version, TargetId: item.Id));     // not a Personality
        Reject(e, new("chooseSearchAttachTarget", 1, e.Version, TargetId: lion.Id));     // not the searcher
        Reject(e, new("chooseSearchAttachTarget", 0, e.Version));
        Reject(e, Pass(e));
        Accept(e, new("chooseSearchAttachTarget", 0, e.Version, TargetId: crane.Id));
        Eq(e.State.Cards[item.Id].Zone, Zone.Attached); Eq(e.State.Cards[item.Id].AttachedTo, crane.Id);
        Check(!EventTypes(e).Contains("payment"), "no Gold was paid"); Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Eq(EventTypes(e).Count(t => t == "deckShuffled"), 1, "the Fate deck was shuffled");
        Eq(e.State.Players[0].FateDeck.Count, 1);
        // A Personality that already carries a Weapon is not offered a second one.
        var s2 = Start();
        var a = Put(s2, Lion, 0, Zone.Home); var b = Put(s2, Crane, 0, Zone.Home);
        var carried = Put(s2, "training-unique-weapon", 0, Zone.Attached); carried.AttachedTo = a.Id; carried.ProvinceIndex = null;
        Deck(s2, 0, Zone.FateDeck, Weapon);
        var f = Searching(s2, AttachSpec, a.Id);
        Accept(f, One(f, Weapon));
        Eq(f.State.Cards.Values.Single(c => c.DefinitionId == Weapon && c.OwnerId == 0).AttachedTo, b.Id, "the only Personality that can carry it needed no decision");
        // No Personality can have it: the card is ineligible with a reason, and the only answer is Pass.
        var s3 = Start();
        var src3 = Put(s3, "training-castle", 0, Zone.Home);
        Deck(s3, 0, Zone.FateDeck, Item);
        var g = Searching(s3, AttachSpec, src3.Id);
        Check(Eligible(g).Length == 0 && View(g).Entries![0].Reason!.Contains("No Personality", StringComparison.Ordinal), "ineligible with a reason");
        Reject(g, One(g, Item)); Accept(g, Pass(g));
        Eq(g.State.Cards.Values.Single(c => c.DefinitionId == Item).Zone, Zone.FateDeck);
    }

    // ---- CelestialIntoPlay -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CelestialIntoPlay()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        var inPlay = Put(s, Ebisu, 0, Zone.Celestial); var theirs = Put(s, Ebisu, 1, Zone.Celestial);
        var p0 = Put(s, Castle, 0, Zone.Province, 0, false);
        Deck(s, 0, Zone.DynastyDeck, Megumi, "training-marketplace");
        var found = s.Players[0].DynastyDeck.Select(id => s.Cards[id]).First(c => c.DefinitionId == Megumi);
        var e = Searching(s, CelestialSpec, source.Id);
        Eq(Seq(Eligible(e)), Megumi);
        Accept(e, One(e, Megumi));
        Eq(e.State.Cards[found.Id].Zone, Zone.Celestial, "in play"); Eq(e.State.Cards[inPlay.Id].Zone, Zone.DynastyDiscard, "the controller's other Celestial was discarded");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Celestial, "the opponent's stays");
        Eq(e.State.Players[0].Provinces[0].CardId, p0.Id, "no province was refilled"); Eq(e.State.Players[0].DynastyDeck.Count, 1);
        Check(EventTypes(e).Contains("celestialEnteredPlay") && EventTypes(e).Contains("celestialDiscarded"), "entered through the shared entry");
        Eq(EventTypes(e).Count(t => t == "deckShuffled"), 1, "the Dynasty deck was shuffled");
    }

    // ---- PlayAsAdditionalAction ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PlayAsAdditionalAction()
    {
        MatchState Battle()
        {
            var s = Start(Phase.Battle);
            var unit = Put(s, Lion, 0, Zone.Battlefield, 0); Put(s, Crane, 1, Zone.Battlefield, 0);
            Deck(s, 0, Zone.FateDeck, ExtendedManeuvers);
            return s;
        }
        var s = Battle();
        var source = s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var e = Searching(s, PlaySpec, source.Id);
        Reject(e, Pass(e)); // a Terrain qualifies
        Accept(e, One(e, ExtendedManeuvers));
        var found = e.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        Eq(found.Zone, Zone.Hand, "the found card is in the hand to be played");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.Mandatory && g.ScopeCardIds is { Length: 1 } ids && ids[0] == found.Id), "a mandatory additional action scoped to it");
        Check(e.State.SearchPlayOwes.Any(o => o.CardId == found.Id), "the play is owed");
        Check(e.QueryActions(0).Any(a => a.Type == "pass" && !a.Enabled), "the pass is disabled while it is owed");
        Accept(e, new("playStrategy", 0, e.Version, CardId: found.Id));
        Eq(e.State.Cards[found.Id].Zone, Zone.Battlefield, "played");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "the grant was spent");
        // A Terrain that cannot be played now is discarded: one is already at this battlefield.
        var t = Battle();
        Put(t, FloodedPass, 1, Zone.Battlefield, 0);
        var f = Searching(t, PlaySpec, t.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Id);
        Accept(f, One(f, ExtendedManeuvers));
        var thrown = f.State.Cards.Values.Single(c => c.DefinitionId == ExtendedManeuvers);
        Eq(thrown.Zone, Zone.FateDiscard, "discarded when it cannot be played"); Check(f.State.SearchPlayOwes.Count == 0 && f.State.AdditionalActions.Count == 0, "nothing is owed");
        Check(EventTypes(f).Contains("searchPlayDiscarded"));
    }

    // ---- the other player chooses -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OtherPlayerChooses()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Weapon, Kama);
        var kama = s.Players[0].FateDeck.Select(id => s.Cards[id]).First(c => c.DefinitionId == Kama);
        var e = Searching(s, OtherSpec, source.Id);
        Accept(e, Take(e, 0, Item, Weapon));
        Eq(e.State.PendingChoice?.Type, "shownChoice"); Eq(e.State.PendingChoice!.PlayerId, 1, "the other player decides");
        Eq(View(e).Stage, SearchStage.OtherPlayerChooses);
        Eq(e.State.ShownCards.Count, 2, "both were shown");
        // He is offered exactly the two shown definitions.
        var choices = e.QueryActions(1).Where(a => a.Type == "chooseShown").ToArray();
        Eq(choices.Length, 2); Check(choices.All(a => a.Enabled && a.Command is not null), "complete commands");
        Eq(Seq(choices.Select(a => a.Label).Order(StringComparer.Ordinal)), Seq(new[] { "Choose · " + CardCatalog.Get(Item).Name, "Choose · " + CardCatalog.Get(Weapon).Name }.Order(StringComparer.Ordinal)));
        Check(e.QueryActions(0).All(a => a.Type is "concede" or "wait" || !a.Enabled), "the searcher only waits");
        // He sees the shown definitions and nothing of the deck: not the Kama still in it, no instance id.
        var view = JsonSerializer.Serialize(e.GetView(1));
        Check(!view.Contains(Kama, StringComparison.Ordinal) && !view.Contains(kama.Id, StringComparison.Ordinal), "the unshown Kama never reached him");
        foreach (var id in e.State.Players[0].FateDeck.Concat(e.State.Players[0].Hand)) Check(!view.Contains($"\"{id}\"", StringComparison.Ordinal), $"{id} leaked");
        Check(e.GetView(1).PendingSearch is null, "and he has no deck viewer");
        OfferedCommandsValidate(e);
        Reject(e, new("chooseShown", 0, e.Version, Choice: e.State.ShownCards[0].Sequence.ToString())); // the searcher may not answer
        Reject(e, new("chooseShown", 1, e.Version, Choice: "99"));                                       // not a shown card
        Reject(e, new("chooseShown", 1, e.Version, Choice: "done"));                                     // exactly one is chosen
        Reject(e, Pass(e, 0));
        var weaponId = e.State.Cards.Values.Single(c => c.DefinitionId == Weapon).Id; var itemId = e.State.Cards.Values.Single(c => c.DefinitionId == Item).Id;
        Accept(e, new("chooseShown", 1, e.Version, Choice: e.State.ShownCards.Single(c => c.DefinitionId == Weapon).Sequence.ToString()));
        Eq(e.State.Cards[weaponId].Zone, Zone.FateDiscard, "the chosen card took the chosen disposition");
        Eq(e.State.Cards[itemId].Zone, Zone.Hand, "the other went to the hand"); Eq(e.State.Cards[itemId].OwnerId, 0);
        Eq(e.State.Cards[kama.Id].Zone, Zone.FateDeck, "the deck keeps the rest");
        Check(e.State.PendingSearch is null && e.State.PendingShownChoice is null && e.State.PendingChoice is null, $"everything is closed: {e.State.PendingSearch?.Stage} {e.State.PendingShownChoice?.SpecId} {e.State.PendingChoice?.Type}");
        Eq(EventTypes(e).Count(t => t == "deckShuffled"), 1, "the searched deck was shuffled");
        // Finding one card only: nothing to choose, so it takes the chosen disposition at once.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        Deck(s2, 0, Zone.FateDeck, Item);
        var f = Searching(s2, OtherSpec, src2.Id);
        Accept(f, Take(f, 0, Item));
        Eq(f.State.Cards.Values.Single(c => c.DefinitionId == Item).Zone, Zone.FateDiscard, "the only card found is the one chosen");
        Check(f.State.PendingSearch is null && f.State.PendingChoice is null);
    }

    // ---- reveal-until looks ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RevealUntilLook()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, "training-spell", "training-follower", Item, Weapon, Kama);
        var ids = s.Players[0].FateDeck.ToArray();
        var e = MatchEngine.FromTestState(s);
        e.BeginLookForTest(RevealLook, source.Id, 0);
        var look = e.GetView(0).PendingLook!;
        Eq(look.Stage, "Arrange"); Eq(look.Cards!.Length, 3, "up to and including the first Item"); Eq(Seq(look.Cards.Select(c => c.DefinitionId)), $"training-spell,training-follower,{Item}");
        Eq(e.State.ShownCards.Count, 3, "each revealed card is public"); Eq(Seq(e.State.ShownCards.Select(c => c.DefinitionId)), $"training-spell,training-follower,{Item}");
        Check(e.GetView(1).PendingLook is null, "the other seat has no look view, only the revealed cards");
        var other = JsonSerializer.Serialize(e.GetView(1));
        Check(!other.Contains(Weapon, StringComparison.Ordinal) && !other.Contains(ids[3], StringComparison.Ordinal) && !other.Contains(ids[0], StringComparison.Ordinal), "nothing below the match nor any id reached the other seat");
        Check(look.Rules!.Any(r => r.Disposition == LookDisposition.Hand && r.Min == 1 && r.Max == 1), "the match must take its disposition");
        Check(look.Cards[2].Dispositions.Single(d => d.Disposition == LookDisposition.Hand).Legal && !look.Cards[0].Dispositions.Single(d => d.Disposition == LookDisposition.Hand).Legal, "only the matching card may take it");
        OfferedCommandsValidate(e);
        Reject(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:bottom", "1:bottom", "2:bottom"]));    // the match must go to the hand
        Reject(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:hand", "1:bottom", "2:bottom"]));      // a card that is not the match cannot
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:bottom", "1:bottom", "2:hand"]) with { PlayerId = 1 });
        var rng = e.State.RngState;
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["1:bottom", "0:bottom", "2:hand"]));
        Eq(e.State.Cards[ids[2]].Zone, Zone.Hand, "the match is in the hand");
        Eq(Seq(e.State.Players[0].FateDeck), $"{ids[3]},{ids[4]},{ids[1]},{ids[0]}", "the rest are on the bottom in the order chosen");
        Eq(e.State.RngState, rng, "no shuffle"); Check(!EventTypes(e).Contains("deckShuffled"));
        Check(e.State.PendingLook is null && e.State.PendingChoice is null, "the look is over");
        // No match: the whole deck is revealed and nothing is forced.
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home);
        Deck(s2, 0, Zone.FateDeck, "training-spell", "training-follower");
        var f = MatchEngine.FromTestState(s2);
        f.BeginLookForTest(RevealOther, src2.Id, 0);
        Eq(f.GetView(0).PendingLook!.Cards!.Length, 2, "the whole deck"); Eq(f.State.ShownCards.Count, 2);
        Check(f.GetView(0).PendingLook!.Rules!.All(r => r.Min == 0), "nothing is required when nothing matches");
        Accept(f, new("arrangeTopCards", 0, f.Version, CardIds: ["0:discard", "1:discard"]));
        Eq(f.State.Players[0].FateDeck.Count, 0, "the rest were discarded"); Eq(f.State.Players[0].Hand.Count, 0);
        // A matching card taking Show goes to the hand and is shown a second time as a take.
        var s3 = Start();
        var src3 = Put(s3, Lion, 0, Zone.Home);
        Deck(s3, 0, Zone.FateDeck, Item, Weapon);
        var g = MatchEngine.FromTestState(s3);
        g.BeginLookForTest(RevealOther, src3.Id, 0);
        Eq(g.GetView(0).PendingLook!.Cards!.Length, 1);
        Accept(g, new("arrangeTopCards", 0, g.Version, CardIds: ["0:show"]));
        Eq(g.State.Players[0].Hand.Count, 1); Eq(g.State.Players[0].FateDeck.Count, 1);
        Check(g.State.ShownCards.Count == 2, "revealed, then shown as it was taken");
    }

    // ---- round trips ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine Restore(MatchEngine e) => MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(Json(e))!);

    private static void SameFuture(MatchEngine original, Func<MatchEngine, GameCommand> next, string what)
    {
        var copy = Restore(original);
        Eq(Json(copy), Json(original), what + ": a restored game saves byte for byte the same");
        Eq(JsonSerializer.Serialize(copy.GetView(0)), JsonSerializer.Serialize(original.GetView(0)), what + ": the same view");
        var actions = Seq(original.QueryActions(original.State.PendingChoice!.PlayerId).Select(a => a.Type + ":" + a.Label));
        Eq(Seq(copy.QueryActions(copy.State.PendingChoice!.PlayerId).Select(a => a.Type + ":" + a.Label)), actions, what + ": the same actions");
        Accept(original, next(original)); Accept(copy, next(copy));
        Eq(Json(copy), Json(original), what + ": both resume to the same position");
    }

    private static void MidSearchRoundTrips()
    {
        // The set is a single command, so the save points are the decisions around it: the source, the follow-up targets, the other player's choice.
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Put(s, Castle, 0, Zone.Province, 0, false); Put(s, Farm, 0, Zone.Province, 1, true);
        Deck(s, 0, Zone.FateDeck, Item, Weapon);
        var e = Searching(s, ChooseSpec, source.Id);
        SameFuture(e, x => ChooseSource(x, SearchDeck.OwnProvinces), "the source choice");
        SameFuture(e, x => Take(x, 0, Castle) with { Type = "selectSearchCard", CardIds = null, Choice = Castle }, "a province selection");
        var s2 = Start();
        var src2 = Put(s2, Lion, 0, Zone.Home); var other = Put(s2, Crane, 0, Zone.Home);
        Deck(s2, 0, Zone.FateDeck, Item);
        var f = Searching(s2, AttachSpec, src2.Id);
        Accept(f, One(f, Item));
        SameFuture(f, x => new("chooseSearchAttachTarget", 0, x.Version, TargetId: other.Id), "the attach target");
        var s3 = Start();
        var src3 = Put(s3, Lion, 0, Zone.Home);
        Deck(s3, 0, Zone.FateDeck, Item, Weapon, Kama);
        var g = Searching(s3, OtherSpec, src3.Id);
        var before = Json(g);
        SameFuture(g, x => Take(x, 0, Item, Weapon), "the selection");
        var g2 = Searching(s3, OtherSpec, src3.Id);
        Accept(g2, Take(g2, 0, Item, Weapon));
        var saved = Json(g2);
        Check(saved.Contains("PickedCardIds", StringComparison.Ordinal) && saved.Contains("ShownSequences", StringComparison.Ordinal) && saved.Contains("PendingShownChoice", StringComparison.Ordinal), "the pending state carries the picks");
        SameFuture(g2, x => new("chooseShown", 1, x.Version, Choice: x.State.ShownCards[0].Sequence.ToString()), "the other player's choice");
        Check(before != saved);
        var s4 = Start();
        var src4 = Put(s4, Lion, 0, Zone.Home);
        Deck(s4, 0, Zone.FateDeck, "training-spell", Item, Weapon);
        var h = MatchEngine.FromTestState(s4);
        h.BeginLookForTest(RevealLook, src4.Id, 0);
        SameFuture(h, x => new("arrangeTopCards", 0, x.Version, CardIds: ["0:bottom", "1:hand"]), "the reveal-until look");
    }

    // ---- refusals and validation -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RefusalsLeaveStateUnchanged()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Weapon);
        var e = Searching(s, TwoItems, source.Id);
        foreach (var command in new GameCommand[]
        {
            new("chooseSearchDeck", 0, e.Version, Choice: SearchDeck.Fate), new("selectSearchCards", 0, e.Version), new("selectSearchCards", 0, e.Version, CardIds: []),
            new("chooseSearchAttachTarget", 0, e.Version, TargetId: source.Id), new("chooseShown", 0, e.Version, Choice: "1"), new("selectSearchCards", 1, e.Version, CardIds: [Item, Weapon]),
            new("pass", 0, e.Version), new("endAction", 0, e.Version)
        })
            Reject(e, command);
        // A spec the flow cannot do is refused before anything changes.
        foreach (var bad in new[] { BadForeign, BadMany, BadRecruit, BadOther })
        {
            var t = Start();
            var src = Put(t, Lion, 0, Zone.Home);
            var f = MatchEngine.FromTestState(t);
            var before = Json(f);
            Check(Throws(() => f.BeginSearchForTest(bad, src.Id, 0)), bad + " is refused");
            Eq(Json(f), before, bad + ": nothing changed");
        }
        // A stage-specific refusal: at the attach slot a plain selection is not an answer.
        var s2 = Start();
        var a = Put(s2, Lion, 0, Zone.Home); Put(s2, Crane, 0, Zone.Home);
        Deck(s2, 0, Zone.FateDeck, Item, Weapon);
        var g = Searching(s2, AttachSpec, a.Id);
        Accept(g, One(g, Item));
        Reject(g, One(g, Weapon)); Reject(g, Take(g, 0, Weapon)); Reject(g, ChooseSource(g, SearchDeck.Fate));
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    // ---- unused means unchanged ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void UnusedMeansUnchanged()
    {
        var s = Start();
        var source = Put(s, Lion, 0, Zone.Home);
        Deck(s, 0, Zone.FateDeck, Item, Weapon);
        var e = Searching(s, PlainSpec, source.Id);
        string[] added = ["PickedCardIds", "ShownSequences", "PutOnBottomIds"], viewAdded = ["MinSelect", "MaxSelect", "DifferentTitles", "Place"];
        var open = Json(e); var view = JsonSerializer.Serialize(e.GetView(0));
        foreach (var field in added) Check(!open.Contains(field, StringComparison.Ordinal), $"the saved search carries {field} only when used");
        foreach (var field in viewAdded) Check(!view.Contains(field, StringComparison.Ordinal), $"the view shows {field} only when used");
        Accept(e, One(e, Item));
        foreach (var field in added) Check(!Json(e).Contains(field, StringComparison.Ordinal), $"nor afterwards: {field}");
        // Every catalogued spec but the fixtures (and Hantei XVI, issue #734: "your discard pile or Fate deck", the searcher picks the source) carries the defaults, and the old specs behave as before.
        var fixtures = SearchCatalog.All.Values.Where(x => x.Id.StartsWith("training-p20-", StringComparison.Ordinal) || x.Id.StartsWith("p20-showcase-", StringComparison.Ordinal) || x.Id == "w734-hantei-search").Select(x => x.Id).ToHashSet();
        Check(fixtures.Count >= 20, "the fixtures are catalogued");
        // Dark Audience (a set of two, the other player chooses) and Jade Pearl Inn (a Gold-producing Holding) are the published cards that use the extensions (issue #739).
        var wired = new[] { "w739-dark-audience", "w739-inn" };
        foreach (var spec in SearchCatalog.All.Values.Where(x => !fixtures.Contains(x.Id) && !wired.Contains(x.Id)))
        {
            Check(!spec.ChooseOne && spec.SelectCount is null && spec.OtherPlayerChooses is null, $"{spec.Id}: the new spec fields are unused");
            foreach (var option in spec.Decks) Check(!option.Criteria.GoldProducing && !option.Criteria.DifferentTitles && option.Deck is not (SearchDeck.OwnProvinces or SearchDeck.AnyPlayerFateDiscard or SearchDeck.AnyPlayerDynastyDiscard), $"{spec.Id}: the new criteria and sources are unused");
        }
        foreach (var look in LookCatalog.All.Values.Where(x => !x.Id.StartsWith("training-p20-", StringComparison.Ordinal))) Check(look.RevealUntil is null && look.Count > 0, $"{look.Id}: no reveal-until");
        // A look with a fixed count saves exactly the fields it did.
        var t = Start();
        var src = Put(t, Lion, 0, Zone.Home); Deck(t, 0, Zone.FateDeck, Item, Weapon, Kama);
        var l = MatchEngine.FromTestState(t);
        l.BeginLookForTest("public-records", src.Id, 0);
        Check(l.GetView(0).PendingLook!.Cards!.Length == 3 && l.State.ShownCards.Count == 0, "a fixed look reveals nothing");
        // The classic single selection is untouched by SelectCount: the original command and the original offered list.
        var u = Start();
        var usrc = Put(u, Lion, 0, Zone.Home); Deck(u, 0, Zone.FateDeck, Item, Weapon);
        var plain = Searching(u, PlainSpec, usrc.Id);
        Eq(Seq(plain.QueryActions(0).Where(a => a.Type != "concede").Select(a => a.Type).Distinct()), "selectSearchCard", "one select action per eligible entry, as always");
    }
}
