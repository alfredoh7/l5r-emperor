using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Look at the top N cards (issue #322): the sibling of the deck search primitive. A private, ordered view of the top of a
// deck (own, the opponent's, either of the four), one arrangement command that names every viewed card's disposition and the
// order the rest go back in, no shuffle. Every test asserts both what must happen and what must not.
internal static class DeckLookTests
{
    private const string PublicRecords = "oracle-l5r-6095";
    private const string Tamaki = "oracle-l5r-3918";
    private const string BanishAllDoubt = "oracle-l5r-690";
    private const string KahokuExp2 = "oracle-l5r-10479";
    private const string SignsAndPortents = "oracle-l5r-7253";
    private const string UnimpeachableName = "oracle-l5r-8989";   // Political Strategy
    private const string CourtesyOfTheCrane = "oracle-l5r-7996";  // Political Strategy
    // Deck cards whose definitions appear nowhere else on the board, so a leak of any of them is unmistakable.
    private static readonly string[] Decoys = ["training-armor", "training-kama", "training-two-handed-weapon", "training-unique-weapon", "training-reserve-item", "training-spell"];

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("The five consumers are published exactly as their scans print, each wired to a declarative LookSpec", PublishedShape),
            ("Public Records (reorder only): the top three in order, the rest of the deck never sent, any order accepted, no shuffle, the opponent sees only that a look happened", PublicRecordsReorderOnly),
            ("Every illegal arrangement is rejected with the state unchanged: wrong count, duplicates, out-of-range, unoffered dispositions, bad grammar, the wrong player", IllegalArrangementsRejected),
            ("Isawa Tamaki (one card, may go to the bottom), a short deck (fewer than N shows what is left) and an empty deck (nothing to look at)", TamakiBottomAndShortDecks),
            ("Banish All Doubt: exactly one card to the hand, the other three to the bottom in any order; the performer must be an unbowed Tactician in play; the Strategy is spent when played", BanishAllDoubtTakeOneBottomRest),
            ("Bayushi Kahoku exp2 (show then take): only a Political Strategy may be shown, at most one, the show is public without an instance id, the rest go back in any order", KahokuShowThenTake),
            ("Signs and Portents (an Event, ANY Fate deck): the deck choice, the opponent's deck revealed to the viewer only, a discard into the deck owner's pile, the Event resumes the Events Phase", SignsAndPortentsOpponentsDeck),
            ("Looking at any of the four decks: a card may go into your hand only from your own deck; min/max counts per disposition are enforced; Dynasty discards go to the Dynasty pile", AnyDeckDispositions),
            ("A required disposition with criteria (exactly one Item to the hand): the minimum is capped when no viewed card qualifies, and an ineligible card is refused with its reason", RequiredDispositionWithCriteria),
            ("A fixed-order look keeps each group's viewed order; a caller-chosen count overrides the spec's N; a shown card is taken", FixedOrderAndCountOverride),
            ("Hidden information: the viewer's list, the deck contents, instance ids and the RNG state never reach the opponent, including when the viewer looks at the OPPONENT's deck; never more than N cards are sent", HiddenInformation),
            ("No shuffle and full determinism: a look never touches the generator, keeping everything leaves the deck byte-identical, and the match replays exactly", NoShuffleAndReplay),
            ("Order Seppuku is refused and not offered while a look is open (an Open action is only taken at one's own turn to act with nothing pending, issue #368)", SeppukuCannotInterruptALook),
            ("A before-action reaction window (issue #327) the opponent opens ahead of a look or a search resolves first; the deferred action then re-executes into the look or search, which ends with priority to the opponent and no window left open", BeforeActionWindowAheadOfLookAndSearch),
            ("The viewer always has a legal, offered answer at every stage (deck choice, arrangement), every offered command validates, and the options stay bounded", ViewerAlwaysHasALegalAnswer),
        ];
        runner.Run("deck-look", tests);
    }

    // ---- fixtures ----

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Equal<T>(T actual, T expected, string message = "Values differ") =>
        Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

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
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"test-{n}")) n++;
        var c = new CardInstance { Id = $"test-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck), Bowed = bowed, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }

    private static void Deck(MatchState s, int owner, Zone zone, params string[] definitions) { foreach (var d in definitions) Put(s, d, owner, zone); }
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);

    private static string[] Top(MatchEngine e, int owner, string deck, int count = int.MaxValue)
    {
        var state = e.State; var list = deck == "Fate" ? state.Players[owner].FateDeck : state.Players[owner].DynastyDeck;
        return list.Take(count).Select(id => state.Cards[id].DefinitionId).ToArray();
    }

    private static GameCommand Use(MatchEngine e, string source, string ability, int player = 0, string? performer = null) => new("useAbility", player, e.Version, source, AbilityId: ability, PerformerId: performer);
    private static GameCommand Arrange(MatchEngine e, params string[] tokens) => new("arrangeTopCards", 0, e.Version, CardIds: tokens);
    private static GameCommand ArrangeAs(MatchEngine e, int player, params string[] tokens) => new("arrangeTopCards", player, e.Version, CardIds: tokens);
    private static GameCommand ChooseDeck(MatchEngine e, string deck, int owner, int player = 0) => new("chooseLookDeck", player, e.Version, Choice: deck, TargetPlayerId: owner);
    private static LookView Look(MatchEngine e, int player = 0) => e.GetView(player).PendingLook ?? throw new Exception("No look view for player " + player);

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

    /// <summary>The viewer has a complete legal answer offered; the opponent may only wait or concede.</summary>
    private static ActionOption[] AssertViewerHasAnswer(MatchEngine e)
    {
        var pending = e.State.PendingChoice ?? throw new Exception("No pending choice.");
        var offered = e.QueryActions(pending.PlayerId).Where(a => a.Enabled && a.Type is not ("concede" or "wait")).ToArray();
        Check(offered.Length > 0, $"The viewer has no offered answer at {pending.Type}: a bot would stall");
        Check(offered.All(a => a.Command is not null), "Every offered look answer is a complete command");
        Check(e.QueryActions(1 - pending.PlayerId).All(a => a.Type is "wait" or "concede"), "The opponent may only wait or concede during a look");
        return offered;
    }

    private static MatchState SignsState(string[] viewerFate, string[] opponentFate)
    {
        var s = Start();
        s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Phase = Phase.Dynasty; // player 0 ends their turn; player 1's Events Phase reveals Signs and Portents
        var province = s.Players[1].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var events = Put(s, SignsAndPortents, 1, Zone.Province, province: 0); events.FaceUp = false; province.CardId = events.Id;
        // Player 0's End Phase draws the top card of THEIR Fate deck before the turn passes, so a filler goes on top of it.
        Deck(s, 1, Zone.FateDeck, viewerFate); Deck(s, 0, Zone.FateDeck, ["training-follower", .. opponentFate]);
        Deck(s, 1, Zone.DynastyDeck, "training-lion-samurai", "training-lion-courtier", "training-lion-veteran", "training-lion-dignitary");
        return s;
    }

    private static MatchEngine EndTurn(MatchState s)
    {
        var e = Engine(s); Accept(e, new("endDynasty", 0, e.Version));
        if (e.State.PendingChoice?.Type == "discardHand") Accept(e, new("discardHand", 0, e.Version, CardIds: e.State.Players[0].Hand.Take(e.State.Players[0].Hand.Count - 8).ToArray()));
        return e;
    }

    // ---- published shape ----

    private static void PublishedShape()
    {
        var records = CardCatalog.Get(PublicRecords);
        Equal(records.Type, CardType.Holding); Equal(records.GoldCost, 2); Equal(records.GoldProduction, 2, "The scan prints 'Bow this card: Produce 2 Gold.'"); Check(records.Keywords!.Contains("Singular"));
        Equal(records.Text, "Bow this card: Produce 2 Gold. Limited: Bow this card: Look at the top three cards of your Fate deck. Put them back in any order.");
        Equal(string.Join(",", records.Abilities!), "public-records-look");
        var recordsLook = AbilityCatalog.All["public-records-look"].Look!;
        Equal(recordsLook.Count, 3); Equal(recordsLook.Targets.Length, 1); Check(!recordsLook.Targets[0].Opponent); Equal(recordsLook.Targets[0].Deck, "Fate"); Equal(recordsLook.Rules.Length, 0); Check(recordsLook.Reorder); Equal(recordsLook.Remainder, "top");

        var tamaki = CardCatalog.Get(Tamaki);
        Equal(tamaki.Type, CardType.Personality); Equal(tamaki.Clan, "Phoenix"); Equal(tamaki.Force, 4); Equal(tamaki.Chi, 3); Equal(tamaki.HonorRequirement, 5); Equal(tamaki.GoldCost, 7); Equal(tamaki.PersonalHonor, 2);
        Check(tamaki.Keywords!.Contains("Void") && tamaki.Keywords.Contains("Shugenja") && tamaki.Keywords.Contains("Phoenix Clan"));
        Equal(tamaki.Text, "Limited: Look at the top card of your Fate deck. You may put it at the bottom of the deck.");
        Check(!tamaki.Text.Contains("Void"), "The scan prints no Void prefix");
        var tamakiLook = AbilityCatalog.All["isawa-tamaki-look"].Look!;
        Equal(tamakiLook.Count, 1); Equal(string.Join(",", tamakiLook.Rules.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "bottom:0-1");

        var banish = CardCatalog.Get(BanishAllDoubt);
        Equal(banish.Type, CardType.Strategy); Equal(banish.GoldCost, 0); Equal(banish.Focus, 3); Check(banish.Keywords!.Contains("Tactical"));
        Equal(banish.Text, "Limited: Choose your performing unbowed Tactician: Look at the top four cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of your deck in any order.");
        var banishAbility = AbilityCatalog.All["banish-all-doubt-look"];
        Equal(banishAbility.Look!.Count, 4); Equal(banishAbility.Look.Remainder, "bottom"); Check(banishAbility.Look.Reorder); Equal(string.Join(",", banishAbility.Look.Rules.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "hand:1-1");
        Equal(string.Join(",", banishAbility.RequiredKeywords!), "Tactician");

        var kahoku = CardCatalog.Get(KahokuExp2);
        Equal(kahoku.Force, 5); Equal(kahoku.Chi, 4); Equal(kahoku.HonorRequirement, 0); Equal(kahoku.GoldCost, 9); Equal(kahoku.PersonalHonor, 2); Check(kahoku.Keywords!.Contains("Experienced 2") && kahoku.Keywords.Contains("Unique"));
        Equal(kahoku.Text, "Limited: Look at the top three cards of your Fate deck. You may show one of them that is a Political Strategy and put it in your hand. Put the remaining cards back in any order.");
        var kahokuRule = AbilityCatalog.All["kahoku-exp2-look"].Look!.Rules.Single();
        Equal(kahokuRule.Disposition, "show"); Equal(kahokuRule.Min, 0); Equal(kahokuRule.Max, 1);
        Equal(string.Join(",", kahokuRule.Criteria!.Types!), "Strategy"); Equal(string.Join(",", kahokuRule.Criteria.Keywords!), "Political");

        var signs = CardCatalog.Get(SignsAndPortents);
        Equal(signs.Type, CardType.Event); Equal(signs.Effect, "signs-and-portents"); Check(signs.Keywords!.Contains("Unique"));
        Equal(signs.Text, "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in any order.");
        var look = LookCatalog.ForEventEffect("signs-and-portents")!;
        Equal(look.Count, 4); Equal(look.Targets.Length, 2); Check(look.Targets.Count(t => t.Opponent) == 1 && look.Targets.All(t => t.Deck == "Fate"));
        Equal(string.Join(",", look.Rules.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "discard:0-1");
        Check(LookCatalog.ForEventEffect("imperial-gift") is null);
    }

    // ---- Public Records: reorder only ----

    private static void PublicRecordsReorderOnly()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys);
        var records = Put(s, PublicRecords, 0, Zone.Home);
        var deckIds = s.Players[0].FateDeck.ToArray(); var handBefore = s.Players[0].Hand.ToArray();
        var e = Engine(s); var rng = e.State.RngState;
        Accept(e, Use(e, records.Id, "public-records-look"));
        Equal(e.State.PendingChoice?.Type, "lookArrange");
        var view = Look(e);
        Equal(view.Stage, "Arrange"); Equal(view.SourceName, "Public Records"); Equal(view.Requested, 3); Equal(view.CardsRemaining, 6);
        Equal(view.Cards!.Length, 3, "Exactly the top three, never more");
        Equal(string.Join(",", view.Cards.Select(c => c.DefinitionId)), string.Join(",", Decoys.Take(3)), "In deck order, top first");
        Equal(string.Join(",", view.Cards.Select(c => c.Position)), "0,1,2");
        Check(view.Cards.All(c => c.Definition.Id == c.DefinitionId && c.Dispositions.Length == 1 && c.Dispositions[0].Disposition == "top" && c.Dispositions[0].Legal));
        Check(view.Rules!.Length == 0 && view.CanReorder && view.Remainder == "top" && view.OwnDeck && view.DeckOwnerId == 0 && view.Deck == "Fate");
        var viewerJson = Json(e.GetView(0));
        Check(!viewerJson.Contains(Decoys[3]) && !viewerJson.Contains(Decoys[4]) && !viewerJson.Contains(Decoys[5]), "Cards below the top three are never sent");
        HiddenIdsAbsent(e.GetView(0), deckIds); HiddenIdsAbsent(e.GetView(1), deckIds);
        Check(e.GetView(1).PendingLook is null, "The opponent has no viewer");
        var opponentJson = Json(e.GetView(1));
        foreach (var d in Decoys) Check(!opponentJson.Contains(d), d + " leaked to the opponent");
        Equal(string.Join(",", e.QueryActions(1).Select(a => a.Type)), "wait,concede");
        // Keep-order is the only arrangement a legal-actions client is offered for a look that keeps order; reordering is the client's own command.
        var offered = AssertViewerHasAnswer(e);
        Equal(offered.Length, 1); Equal(string.Join(",", offered[0].Command!.CardIds!), "0:top,1:top,2:top");
        // Any order is accepted: the deck's top three become c2, c0, c1; nothing else moved; no shuffle.
        Accept(e, Arrange(e, "2:top", "0:top", "1:top"));
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", new[] { Decoys[2], Decoys[0], Decoys[1], Decoys[3], Decoys[4], Decoys[5] }), "Reordered, the rest untouched");
        Equal(e.State.RngState, rng, "A look never touches the generator");
        Equal(Json(e.State.Players[0].Hand), Json(handBefore)); Check(e.State.Cards[records.Id].Bowed, "Bowing is the cost");
        Check(e.State.PendingChoice is null && e.State.PendingLook is null); Equal(e.State.PriorityPlayerId, 1, "The action resolved and priority passed on");
        AssertOrder(e, "abilityUsed", "topCardsLooked", "topCardsArranged");
        Check(e.GetView(1).Events.Any(ev => ev.Type == "topCardsLooked" && ev.Message == "Crane looks at the top 3 cards of their Fate deck."), "The opponent is told only that a look happened");
        Check(!Json(e.GetView(1).Events).Contains(Decoys[0]) && !Json(e.GetView(1).Events).Contains(Decoys[2]), "The public log names none of the cards");
        Equal(e.State.ShownCards.Count, 0, "Nothing was shown");
        // Bowed Public Records cannot look; it is Limited, so once per turn.
        Reject(e, Use(e, records.Id, "public-records-look", player: 1));
        var e2 = Engine(s); Accept(e2, Use(e2, records.Id, "public-records-look")); Accept(e2, Arrange(e2, "0:top", "1:top", "2:top"));
        Equal(string.Join(",", Top(e2, 0, "Fate")), string.Join(",", Decoys), "Keeping the order leaves the deck exactly as it was");
        var s3 = Start(); Deck(s3, 0, Zone.FateDeck, Decoys); var bowed = Put(s3, PublicRecords, 0, Zone.Home, bowed: true); var e3 = Engine(s3);
        Reject(e3, Use(e3, bowed.Id, "public-records-look"));
    }

    // ---- illegal arrangements ----

    private static void IllegalArrangementsRejected()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys);
        var records = Put(s, PublicRecords, 0, Zone.Home);
        var e = Engine(s); Accept(e, Use(e, records.Id, "public-records-look"));
        Reject(e, Arrange(e, "0:top", "1:top"));                        // too few
        Reject(e, Arrange(e, "0:top", "1:top", "2:top", "3:top"));      // too many (the fourth card was never shown)
        Reject(e, Arrange(e, "0:top", "0:top", "1:top"));               // duplicate
        Reject(e, Arrange(e, "0:top", "1:top", "3:top"));               // position past the viewed cards
        Reject(e, Arrange(e, "0:top", "1:top", "-1:top"));
        Reject(e, Arrange(e, "0:top", "1:top", "2:bottom"));            // bottom is not offered here
        Reject(e, Arrange(e, "0:top", "1:top", "2:hand"));              // nor hand
        Reject(e, Arrange(e, "0:top", "1:top", "2:discard"));
        Reject(e, Arrange(e, "0:top", "1:top", "2:show"));
        Reject(e, Arrange(e, "0:top", "1:top", "2:TOP"));               // the grammar is exact
        Reject(e, Arrange(e, "0:top", "1:top", "2"));
        Reject(e, Arrange(e, "0:top", "1:top", "two:top"));
        Reject(e, Arrange(e, "0:top", "1:top", "2:top:extra"));
        Reject(e, new("arrangeTopCards", 0, e.Version));                // no arrangement at all
        Reject(e, ArrangeAs(e, 1, "0:top", "1:top", "2:top"));          // not the viewer's turn to answer
        Reject(e, new("chooseLookDeck", 0, e.Version, Choice: "Fate", TargetPlayerId: 0)); // wrong stage
        Reject(e, new("pass", 0, e.Version)); Reject(e, new("passSearch", 0, e.Version)); Reject(e, new("endDynasty", 0, e.Version));
        Reject(e, Use(e, records.Id, "public-records-look"));
        Equal(e.State.PendingChoice?.Type, "lookArrange");
        Accept(e, Arrange(e, "1:top", "0:top", "2:top"));
    }

    // ---- Tamaki: one card, bottom; short and empty decks ----

    private static void TamakiBottomAndShortDecks()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys);
        var tamaki = Put(s, Tamaki, 0, Zone.Home);
        var e = Engine(s); Accept(e, Use(e, tamaki.Id, "isawa-tamaki-look"));
        var view = Look(e);
        Equal(view.Cards!.Length, 1); Equal(view.Cards[0].DefinitionId, Decoys[0]); Equal(view.Requested, 1);
        Equal(string.Join(",", view.Cards[0].Dispositions.Select(d => d.Disposition)), "top,bottom");
        Equal(string.Join(",", view.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "bottom:0-1");
        Check(!Json(e.GetView(0)).Contains(Decoys[1]), "Only the top card is sent");
        Equal(string.Join(",", AssertViewerHasAnswer(e).Select(a => string.Join("|", a.Command!.CardIds!)).Order()), "0:bottom,0:top");
        Reject(e, Arrange(e, "0:discard")); Reject(e, Arrange(e, "0:hand")); Reject(e, Arrange(e, "0:bottom", "0:top"));
        Accept(e, Arrange(e, "0:bottom"));
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", Decoys.Skip(1).Append(Decoys[0])), "The looked-at card is now the bottom of the deck");
        Check(e.State.Cards[tamaki.Id].Bowed == false, "Tamaki's Limited action has no bow cost");
        Equal(e.State.PriorityPlayerId, 1);
        AssertOrder(e, "topCardsLooked", "topCardsArranged");
        Check(e.GetView(1).Events.Last(ev => ev.Type == "topCardsArranged").Message.Contains("1 put on the bottom"), "The opponent sees that a card went to the bottom, not which");

        var keep = Engine(s); Accept(keep, Use(keep, tamaki.Id, "isawa-tamaki-look")); Accept(keep, Arrange(keep, "0:top"));
        Equal(string.Join(",", Top(keep, 0, "Fate")), string.Join(",", Decoys), "Leaving it there changes nothing");

        // Fewer than N cards remain: the viewer sees what is left, and the arrangement names exactly those.
        var short3 = Start(); Deck(short3, 0, Zone.FateDeck, Decoys[0], Decoys[1]); var records = Put(short3, PublicRecords, 0, Zone.Home);
        var se = Engine(short3); Accept(se, Use(se, records.Id, "public-records-look"));
        var shortView = Look(se);
        Equal(shortView.Requested, 3); Equal(shortView.Cards!.Length, 2, "Fewer than N cards remaining shows what is left"); Equal(shortView.CardsRemaining, 2);
        Reject(se, Arrange(se, "0:top", "1:top", "2:top"));
        Accept(se, Arrange(se, "1:top", "0:top"));
        Equal(string.Join(",", Top(se, 0, "Fate")), Decoys[1] + "," + Decoys[0]);
        Check(se.GetView(1).Events.Any(ev => ev.Message == "Crane looks at the top 2 cards of their Fate deck."));

        // An empty deck: nothing to look at, so there is nothing to answer -- the action simply resolves.
        var empty = Start(); var emptyTamaki = Put(empty, Tamaki, 0, Zone.Home); var ee = Engine(empty);
        Accept(ee, Use(ee, emptyTamaki.Id, "isawa-tamaki-look"));
        Check(ee.State.PendingChoice is null && ee.State.PendingLook is null && ee.GetView(0).PendingLook is null, "No pending look for an empty deck");
        Equal(ee.State.PriorityPlayerId, 1); Check(ee.GetView(1).Events.Any(ev => ev.Type == "topCardsLooked" && ev.Message.Contains("no cards remain")));
    }

    // ---- Banish All Doubt: exactly one to the hand, the rest to the bottom ----

    private static void BanishAllDoubtTakeOneBottomRest()
    {
        string[] fate = [Decoys[0], Decoys[1], Decoys[2], Decoys[3], Decoys[4], Decoys[5]];
        MatchState Fixture(bool tactician = true, bool bowed = false, Zone zone = Zone.Home)
        {
            var s = Start(); Deck(s, 0, Zone.FateDeck, fate);
            if (tactician) Put(s, "training-tactician", 0, zone, bowed);
            Put(s, BanishAllDoubt, 0, Zone.Hand);
            return s;
        }
        // The performer must be an unbowed Tactician IN PLAY.
        foreach (var (s, why) in new[] { (Fixture(false), "no Tactician"), (Fixture(true, true), "a bowed Tactician"), (Fixture(true, false, Zone.DynastyDiscard), "a Tactician in a discard pile"), (Fixture(true, false, Zone.Dead), "a dead Tactician") })
        {
            var e0 = Engine(s); var strategy0 = e0.State.Cards.Values.Single(c => c.DefinitionId == BanishAllDoubt).Id;
            var tacticianId = e0.State.Cards.Values.FirstOrDefault(c => c.DefinitionId == "training-tactician")?.Id;
            Reject(e0, Use(e0, strategy0, "banish-all-doubt-look", performer: tacticianId));
            var option = e0.QueryActions(0).SingleOrDefault(a => a.Type == "useAbility" && a.Label.StartsWith("Choose your performing unbowed Tactician"));
            Check(option is null || !option.Enabled, $"Banish All Doubt must not be offered enabled with {why}");
        }
        var state = Fixture(); var e = Engine(state);
        var strategy = e.State.Cards.Values.Single(c => c.DefinitionId == BanishAllDoubt); var performer = e.State.Cards.Values.Single(c => c.DefinitionId == "training-tactician");
        Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.PerformerId == performer.Id && a.Command.AbilityId == "banish-all-doubt-look"), "Offered with an unbowed Tactician");
        Accept(e, Use(e, strategy.Id, "banish-all-doubt-look", performer: performer.Id));
        Equal(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "The Strategy is spent as it is played");
        Check(!e.State.Cards[performer.Id].Bowed, "The performer is chosen, not bowed");
        var view = Look(e);
        Equal(view.Cards!.Length, 4); Equal(view.Remainder, "bottom"); Check(view.CanReorder);
        Equal(string.Join(",", view.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "hand:1-1", "Exactly one to the hand");
        Equal(string.Join(",", view.Cards[0].Dispositions.Select(d => d.Disposition)), "bottom,hand", "Unclaimed cards go to the bottom, so top is not offered");
        var handBefore = e.State.Players[0].Hand.Count; var deckIds = e.State.Players[0].FateDeck.ToArray();
        Reject(e, Arrange(e, "0:bottom", "1:bottom", "2:bottom", "3:bottom"));   // none to the hand
        Reject(e, Arrange(e, "0:hand", "1:hand", "2:bottom", "3:bottom"));       // two to the hand
        Reject(e, Arrange(e, "0:top", "1:top", "2:top", "3:hand"));              // top is not offered
        Reject(e, Arrange(e, "0:hand", "1:bottom", "2:bottom", "3:discard"));
        Reject(e, Arrange(e, "0:hand", "1:bottom", "2:bottom"));                 // one card unnamed
        var offered = AssertViewerHasAnswer(e);
        Equal(offered.Length, 4, "One 'take card i' arrangement per viewed card, keeping the bottom group's viewed order");
        Equal(string.Join(",", offered[0].Command!.CardIds!), "0:hand,1:bottom,2:bottom,3:bottom");
        // Take the third card; the other three go to the bottom in the order the viewer chose (the last named is the very bottom).
        Accept(e, Arrange(e, "2:hand", "3:bottom", "0:bottom", "1:bottom"));
        Equal(e.State.Players[0].Hand.Count, handBefore + 1); Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == Decoys[2] && e.State.Cards[id].Zone == Zone.Hand));
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", new[] { Decoys[4], Decoys[5], Decoys[3], Decoys[0], Decoys[1] }), "The rest of the deck, then the three in the chosen order");
        HiddenIdsAbsent(e.GetView(1), deckIds); HiddenIdsAbsent(e.GetView(1), e.State.Players[0].Hand);
        Check(e.GetView(1).Events.Any(ev => ev.Type == "topCardsArranged" && ev.Message.Contains("1 put in their hand") && ev.Message.Contains("3 put on the bottom")));
        Equal(e.State.ShownCards.Count, 0, "Banish All Doubt does not show the card");
        Equal(e.State.PriorityPlayerId, 1);

        // Fewer than four cards left: exactly one of what is there.
        var few = Fixture(); few.Players[0].FateDeck.RemoveRange(2, 4);
        var fe = Engine(few); var fStrategy = fe.State.Cards.Values.Single(c => c.DefinitionId == BanishAllDoubt); var fPerformer = fe.State.Cards.Values.Single(c => c.DefinitionId == "training-tactician");
        Accept(fe, Use(fe, fStrategy.Id, "banish-all-doubt-look", performer: fPerformer.Id));
        Equal(Look(fe).Cards!.Length, 2); Reject(fe, Arrange(fe, "0:bottom", "1:bottom")); Accept(fe, Arrange(fe, "1:hand", "0:bottom"));
        Equal(fe.State.Players[0].FateDeck.Count, 1);
    }

    // ---- Kahoku: show then take ----

    private static void KahokuShowThenTake()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, UnimpeachableName, Decoys[0], CourtesyOfTheCrane, Decoys[1], Decoys[2]);
        var kahoku = Put(s, KahokuExp2, 0, Zone.Home);
        var deckIds = s.Players[0].FateDeck.ToArray();
        var e = Engine(s); Accept(e, Use(e, kahoku.Id, "kahoku-exp2-look"));
        var view = Look(e);
        Equal(view.Cards!.Length, 3); Equal(string.Join(",", view.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "show:0-1", "At most one shown, and none is required");
        var political = view.Cards.Where(c => c.Dispositions.Single(d => d.Disposition == "show").Legal).Select(c => c.DefinitionId).ToArray();
        Equal(string.Join(",", political), UnimpeachableName + "," + CourtesyOfTheCrane, "Only the Political Strategies may be shown");
        var armor = view.Cards[1].Dispositions.Single(d => d.Disposition == "show");
        Check(!armor.Legal); Equal(armor.Reason, "Not a Political Strategy");
        Reject(e, Arrange(e, "0:show", "1:show", "2:top"));            // an ineligible card
        Reject(e, Arrange(e, "0:show", "1:top", "2:show"));            // two shown
        Reject(e, Arrange(e, "0:hand", "1:top", "2:top"));             // hand is not offered: only 'show and take'
        Reject(e, Arrange(e, "0:discard", "1:top", "2:top"));
        var offered = AssertViewerHasAnswer(e);
        Equal(string.Join(" / ", offered.Select(a => string.Join(",", a.Command!.CardIds!))), "0:top,1:top,2:top / 0:show,1:top,2:top / 0:top,1:top,2:show", "Keep everything, or show one of the two eligible cards");
        // Show the second Political Strategy and put the others back in a new order.
        Accept(e, Arrange(e, "2:show", "1:top", "0:top"));
        Equal(e.State.Players[0].Hand.Count(id => e.State.Cards[id].DefinitionId == CourtesyOfTheCrane), 1, "Taken into the hand");
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", new[] { Decoys[0], UnimpeachableName, Decoys[1], Decoys[2] }), "The others back in the chosen order, the rest untouched");
        AssertOrder(e, "topCardsLooked", "cardShown", "topCardsArranged");
        var shown = e.GetView(1).ShownCards!;
        Equal(shown.Length, 1); Equal(shown[0].Definition.Id, CourtesyOfTheCrane); Equal(shown[0].ShownByPlayerId, 0); Equal(shown[0].Deck, "Fate"); Equal(shown[0].SourceName, "Bayushi Kahoku");
        Check(shown[0].Context.Contains("Fate deck look"), shown[0].Context);
        Equal(Json(e.GetView(0).ShownCards), Json(shown), "The same public record for both players");
        var handCard = e.State.Players[0].Hand.Single(id => e.State.Cards[id].DefinitionId == CourtesyOfTheCrane);
        Check(!Json(shown).Contains(handCard) && !Json(e.GetView(1)).Contains('"' + handCard + '"'), "The record carries no instance id, and the shown card is still hidden in the hand");
        HiddenIdsAbsent(e.GetView(1), deckIds.Where(id => id != handCard));

        // Nothing eligible among the top three: showing is impossible, keeping everything is the only answer.
        var none = Start(); Deck(none, 0, Zone.FateDeck, Decoys); var k2 = Put(none, KahokuExp2, 0, Zone.Home); var ne = Engine(none);
        Accept(ne, Use(ne, k2.Id, "kahoku-exp2-look"));
        Equal(Look(ne).Rules![0].Max, 0, "The maximum is capped: no viewed card qualifies");
        Reject(ne, Arrange(ne, "0:show", "1:top", "2:top"));
        Accept(ne, Arrange(ne, "2:top", "1:top", "0:top"));
        Equal(ne.State.ShownCards.Count, 0);
        // Keeping everything is legal even when a Political Strategy is on view ("you may").
        var may = Start(); Deck(may, 0, Zone.FateDeck, UnimpeachableName, Decoys[0], Decoys[1]); var k3 = Put(may, KahokuExp2, 0, Zone.Home); var me = Engine(may);
        Accept(me, Use(me, k3.Id, "kahoku-exp2-look")); Accept(me, Arrange(me, "0:top", "1:top", "2:top")); Equal(me.State.ShownCards.Count, 0);
    }

    // ---- Signs and Portents: an Event that looks at ANY Fate deck ----

    private static void SignsAndPortentsOpponentsDeck()
    {
        string[] own = [Decoys[0], Decoys[1], Decoys[2], Decoys[3], Decoys[4]];
        string[] theirs = ["training-battle-maiden", "training-brash-personality", "training-cavalry", "training-monk", "training-ninja-source", "training-berserker"];
        var s = SignsState(own, theirs);
        var signs = s.Players[1].Provinces[0].CardId!;
        var opponentDeckIds = s.Players[0].FateDeck.Skip(1).ToArray();   // after the filler that player 0's End Phase draws into their own hand
        var e = EndTurn(s);
        Equal(e.State.PendingChoice?.Type, "lookDeck"); Equal(e.State.Phase, Phase.Events); Equal(e.State.PriorityPlayerId, 1);
        var choice = Look(e, 1);
        Equal(choice.Stage, "ChooseDeck"); Equal(choice.DeckChoices!.Length, 2); Check(choice.Cards is null, "No cards before a deck is chosen");
        Check(choice.DeckChoices.Single(d => d.OwnDeck).CardsRemaining == 5 && choice.DeckChoices.Single(d => !d.OwnDeck).CardsRemaining == 6 && choice.DeckChoices.Single(d => !d.OwnDeck).OwnerId == 0);
        Check(choice.DeckChoices.Single(d => !d.OwnDeck).Label.Contains("opponent"), choice.DeckChoices[1].Label);
        Check(e.GetView(0).PendingLook is null, "The other player has no viewer");
        var offered = AssertViewerHasAnswer(e);
        Equal(string.Join(",", offered.Select(a => $"{a.Type}:{a.Command!.Choice}:{a.Command.TargetPlayerId}")), "chooseLookDeck:Fate:1,chooseLookDeck:Fate:0");
        Reject(e, ChooseDeck(e, "Fate", 0, player: 0));                  // not the viewer
        Reject(e, ChooseDeck(e, "Dynasty", 0, player: 1));               // not an offered deck
        Reject(e, ChooseDeck(e, "Fate", 2, player: 1));                  // nor a player
        Reject(e, ChooseDeck(e, "Fate", 1, player: 0)); Reject(e, Arrange(e, "0:top"));
        Accept(e, ChooseDeck(e, "Fate", 0, player: 1));                 // the OPPONENT's deck
        var view = Look(e, 1);
        Equal(view.Stage, "Arrange"); Check(!view.OwnDeck); Equal(view.DeckOwnerId, 0); Equal(view.Requested, 4);
        Equal(string.Join(",", view.Cards!.Select(c => c.DefinitionId)), string.Join(",", theirs.Take(4)), "The opponent's top four, top first");
        Check(view.Cards!.All(c => c.Dispositions.Select(d => d.Disposition).SequenceEqual(new[] { "top", "discard" })), "Only keep and discard: nothing may be taken from the opponent's deck");
        // The deck owner learns only that a look happened.
        Check(e.GetView(0).PendingLook is null);
        var ownerJson = Json(e.GetView(0));
        foreach (var d in theirs.Take(4)) Check(!ownerJson.Contains(d), $"{d} (a card of the owner's own deck) was revealed to its owner through the viewer's look");
        HiddenIdsAbsent(e.GetView(0), opponentDeckIds.Take(4)); HiddenIdsAbsent(e.GetView(1), opponentDeckIds);
        Check(e.GetView(0).Events.Any(ev => ev.Type == "topCardsLooked" && ev.Message == "Lion looks at the top 4 cards of Crane's Fate deck."), "The deck owner is told a look happened");
        Check(!Json(e.GetView(0).Events).Contains("training-battle-maiden") && !Json(e.GetView(0).PendingChoice).Contains("Battle Maiden"), "The public log and prompt name none of the cards");
        Reject(e, Arrange(e, "0:discard", "1:discard", "2:top", "3:top"));       // one discard at most
        Reject(e, Arrange(e, "0:hand", "1:top", "2:top", "3:top"));              // hand from the opponent's deck: not offered
        Reject(e, Arrange(e, "0:show", "1:top", "2:top", "3:top"));
        Reject(e, Arrange(e, "0:bottom", "1:top", "2:top", "3:top"));
        Reject(e, ArrangeAs(e, 0, "0:top", "1:top", "2:top", "3:top"));
        var ownDeckBefore = e.State.Players[1].FateDeck.ToArray(); var rng = e.State.RngState;
        // Discard the second card and put the rest back in a new order.
        Accept(e, ArrangeAs(e, 1, "3:top", "1:discard", "0:top", "2:top"));
        var after = e.State;
        var discarded = after.Cards.Values.Single(c => c.DefinitionId == theirs[1] && c.Zone == Zone.FateDiscard);
        Equal(discarded.OwnerId, 0, "A card from the opponent's deck goes to ITS owner's discard pile"); Check(discarded.FaceUp);
        Check(e.GetView(0).Players[0].Discard.Any(c => c.Id == discarded.Id) && e.GetView(1).Players[0].Discard.Any(c => c.Id == discarded.Id), "A discard pile is public");
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", new[] { theirs[3], theirs[0], theirs[2], theirs[4], theirs[5] }), "The opponent's deck: the four rearranged minus the discard, the rest untouched");
        Equal(Json(after.Players[1].FateDeck), Json(ownDeckBefore), "The viewer's own deck is untouched"); Equal(after.RngState, rng);
        Equal(after.ShownCards.Count, 0); AssertOrder(e, "topCardsLooked", "cardDiscarded", "topCardsArranged");
        Equal(after.Cards[signs].Zone, Zone.DynastyDiscard, "The Event resolved and was discarded"); Equal(after.Phase, Phase.Action, "The Events Phase resumed and completed");
        Check(after.PendingChoice is null && after.PendingLook is null);

        // The viewer's own deck instead.
        var e2 = EndTurn(SignsState(own, theirs)); Accept(e2, ChooseDeck(e2, "Fate", 1, player: 1));
        var own2 = Look(e2, 1); Check(own2.OwnDeck); Equal(own2.DeckOwnerId, 1); Equal(string.Join(",", own2.Cards!.Select(c => c.DefinitionId)), string.Join(",", own.Take(4)));
        Accept(e2, ArrangeAs(e2, 1, "0:top", "1:top", "2:top", "3:top"));
        Equal(e2.State.Phase, Phase.Action); Equal(string.Join(",", Top(e2, 1, "Fate")), string.Join(",", own));

        // A chosen deck with nothing in it: nothing to look at, the Event still resolves.
        var s3 = SignsState(own, []); var e3 = EndTurn(s3); Accept(e3, ChooseDeck(e3, "Fate", 0, player: 1));
        Equal(e3.State.Phase, Phase.Action); Check(e3.State.PendingLook is null && e3.State.PendingChoice is null);
    }

    // ---- any of the four decks ----

    private static MatchEngine AnyDeckFixture(out string sourceId, bool dynastyCards = true)
    {
        var s = Start();
        Deck(s, 0, Zone.FateDeck, "training-armor", "training-kama", "training-two-handed-weapon", "training-unique-weapon");
        // Definitions found nowhere else on the board (the hands hold Followers, Items and Strategies; the provinces hold Personalities).
        if (dynastyCards) Deck(s, 0, Zone.DynastyDeck, "training-farm", "training-castle", "training-dojo", "training-market-holding");
        Deck(s, 1, Zone.FateDeck, "training-reserve-item", "training-spell", "training-berserker", "training-tactician");
        Deck(s, 1, Zone.DynastyDeck, "training-mine-holding", "training-temple-holding", "training-expensive-holding", "training-unique-personality");
        sourceId = Put(s, "training-look-source", 0, Zone.Home).Id;
        return Engine(s);
    }

    private static void AnyDeckDispositions()
    {
        var e = AnyDeckFixture(out var source);
        Accept(e, Use(e, source, "training-look-any"));
        var choice = Look(e);
        Equal(choice.DeckChoices!.Length, 4);
        Equal(string.Join(",", choice.DeckChoices.Select(d => $"{d.Deck}:{d.OwnerId}:{d.OwnDeck}")), "Fate:0:True,Dynasty:0:True,Fate:1:False,Dynasty:1:False");
        Equal(AssertViewerHasAnswer(e).Length, 4);

        // The opponent's Fate deck: hand is not offered (a card may only go into your hand from your own deck), discard/bottom are.
        Accept(e, ChooseDeck(e, "Fate", 1));
        var theirs = Look(e);
        Check(!theirs.OwnDeck);
        Equal(string.Join(",", theirs.Cards![0].Dispositions.Select(d => $"{d.Disposition}:{d.Legal}")), "top:True,discard:True,bottom:True,hand:False");
        Equal(theirs.Cards[0].Dispositions.Single(d => d.Disposition == "hand").Reason, "Only cards from your own deck may be put in your hand");
        Equal(string.Join(",", theirs.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "discard:0-1,bottom:0-2,hand:0-0", "The hand maximum is capped to 0 for the opponent's deck");
        Reject(e, Arrange(e, "0:hand", "1:top", "2:top"));
        Reject(e, Arrange(e, "0:discard", "1:discard", "2:top"));                 // one discard at most
        Reject(e, Arrange(e, "0:bottom", "1:bottom", "2:bottom"));                // two to the bottom at most
        Accept(e, Arrange(e, "0:discard", "1:bottom", "2:bottom"));
        var discarded = e.State.Cards.Values.Single(c => c.DefinitionId == "training-reserve-item");
        Equal(discarded.Zone, Zone.FateDiscard); Equal(discarded.OwnerId, 1, "Into the deck owner's pile");
        Equal(string.Join(",", Top(e, 1, "Fate")), "training-tactician,training-spell,training-berserker", "The remaining card, then the two sent to the bottom in the chosen order");

        // The opponent's Dynasty deck goes to the Dynasty pile; a look at your own Dynasty deck may take a card into your hand.
        var e2 = AnyDeckFixture(out var source2);
        Accept(e2, Use(e2, source2, "training-look-any")); Accept(e2, ChooseDeck(e2, "Dynasty", 1));
        Accept(e2, Arrange(e2, "1:discard", "0:top", "2:top"));
        Equal(e2.State.Cards.Values.Single(c => c.DefinitionId == "training-temple-holding").Zone, Zone.DynastyDiscard, "A Dynasty card goes to the Dynasty discard pile");
        var e3 = AnyDeckFixture(out var source3);
        Accept(e3, Use(e3, source3, "training-look-any")); Accept(e3, ChooseDeck(e3, "Dynasty", 0));
        var own = Look(e3); Check(own.OwnDeck);
        Equal(string.Join(",", own.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "discard:0-1,bottom:0-2,hand:0-1");
        Reject(e3, Arrange(e3, "0:hand", "1:hand", "2:top"));                    // one to the hand at most
        Reject(e3, Arrange(e3, "0:show", "1:top", "2:top"));                     // 'show' is not offered here, only 'hand'
        var handBefore = e3.State.Players[0].Hand.Count;
        Accept(e3, Arrange(e3, "0:hand", "1:discard", "2:bottom"));
        Equal(e3.State.Players[0].Hand.Count, handBefore + 1); Check(e3.State.Players[0].Hand.Any(id => e3.State.Cards[id].DefinitionId == "training-farm"), "The Dynasty card went into the hand");
        Equal(e3.State.Cards.Values.Single(c => c.DefinitionId == "training-castle").Zone, Zone.DynastyDiscard);
        Equal(string.Join(",", Top(e3, 0, "Dynasty")), "training-market-holding,training-dojo", "The Dynasty deck: what remained, with the bottom card last");
        Equal(e3.State.ShownCards.Count, 0);
    }

    // ---- required disposition with criteria ----

    private static void RequiredDispositionWithCriteria()
    {
        MatchEngine Look3(out string source, params string[] fate)
        {
            var s = Start(); Deck(s, 0, Zone.FateDeck, fate); source = Put(s, "training-look-source", 0, Zone.Home).Id;
            var e = Engine(s); return e;
        }
        // One Item among the top three: it must be the one taken.
        var e = Look3(out var source, "training-rally", "training-item", "training-pressure", "training-follower");
        Accept(e, Use(e, source, "training-look-take-item"));
        var view = Look(e);
        Equal(string.Join(",", view.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "hand:1-1");
        var rally = view.Cards![0].Dispositions.Single(d => d.Disposition == "hand");
        Check(!rally.Legal); Equal(rally.Reason, "Not an Item");
        Check(view.Cards[1].Dispositions.Single(d => d.Disposition == "hand").Legal);
        Reject(e, Arrange(e, "0:top", "1:top", "2:top"));                         // exactly one is required
        Reject(e, Arrange(e, "0:hand", "1:top", "2:top"));                        // a Strategy is not an Item
        Reject(e, Arrange(e, "0:top", "1:top", "2:hand"));
        Equal(string.Join(" / ", AssertViewerHasAnswer(e).Select(a => string.Join(",", a.Command!.CardIds!))), "0:top,1:hand,2:top");
        Accept(e, Arrange(e, "1:hand", "2:top", "0:top"));
        Equal(string.Join(",", Top(e, 0, "Fate")), "training-pressure,training-rally,training-follower");
        // Two Items: either one, never both.
        var two = Look3(out var source2, "training-item", "training-weapon", "training-rally");
        Accept(two, Use(two, source2, "training-look-take-item"));
        Reject(two, Arrange(two, "0:hand", "1:hand", "2:top"));
        Equal(AssertViewerHasAnswer(two).Length, 2); Accept(two, Arrange(two, "1:hand", "0:top", "2:top"));
        // No Item at all: the minimum is capped to what is possible, so keeping everything is legal (and nothing can be taken).
        var none = Look3(out var source3, "training-rally", "training-pressure", "training-follower", "training-item");
        Accept(none, Use(none, source3, "training-look-take-item"));
        Equal(string.Join(",", Look(none).Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "hand:0-0", "No viewed card qualifies: the required minimum is capped");
        Reject(none, Arrange(none, "0:hand", "1:top", "2:top"));
        Accept(none, Arrange(none, "0:top", "1:top", "2:top"));
    }

    // ---- fixed order and the caller's count ----

    private static void FixedOrderAndCountOverride()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys); var source = Put(s, "training-look-source", 0, Zone.Home).Id;
        var e = Engine(s); Accept(e, Use(e, source, "training-look-fixed"));
        var view = Look(e);
        Check(!view.CanReorder); Equal(view.Cards!.Length, 3);
        Reject(e, Arrange(e, "1:top", "0:top", "2:top"));                        // the order may not change
        Reject(e, Arrange(e, "2:top", "1:top", "0:top"));
        Reject(e, Arrange(e, "2:top", "1:discard", "0:top"));                    // a discard does not license a reorder of the rest
        Equal(AssertViewerHasAnswer(e).Length, 4, "Keep everything, or discard one of the three, each keeping the viewed order");
        Accept(e, Arrange(e, "0:top", "1:discard", "2:top"));
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", new[] { Decoys[0], Decoys[2], Decoys[3], Decoys[4], Decoys[5] }));
        Equal(e.State.Cards.Values.Single(c => c.DefinitionId == Decoys[1]).Zone, Zone.FateDiscard);

        // The ability passes its own N (the spec says three, this look is two) and a card must be shown and taken.
        var s2 = Start(); Deck(s2, 0, Zone.FateDeck, Decoys); var source2 = Put(s2, "training-look-source", 0, Zone.Home).Id;
        var e2 = Engine(s2); Accept(e2, Use(e2, source2, "training-look-show"));
        var two = Look(e2);
        Equal(two.Requested, 2); Equal(two.Cards!.Length, 2, "The caller's count, not the spec's"); Equal(string.Join(",", two.Rules!.Select(r => $"{r.Disposition}:{r.Min}-{r.Max}")), "show:1-1");
        Reject(e2, Arrange(e2, "0:top", "1:top")); Reject(e2, Arrange(e2, "0:show", "1:show"));
        Accept(e2, Arrange(e2, "1:show", "0:top"));
        Equal(e2.State.ShownCards.Count, 1); Equal(e2.State.ShownCards[0].DefinitionId, Decoys[1]); Equal(e2.GetView(1).ShownCards![0].SourceName, "Training look source");
        Equal(string.Join(",", Top(e2, 0, "Fate")), string.Join(",", new[] { Decoys[0], Decoys[2], Decoys[3], Decoys[4], Decoys[5] }));
    }

    // ---- hidden information ----

    private static void HiddenInformation()
    {
        foreach (var (deck, owner, viewedDefinitions, hiddenBelow) in new[]
        {
            ("Fate", 0, new[] { "training-armor", "training-kama", "training-two-handed-weapon" }, "training-unique-weapon"),
            ("Fate", 1, new[] { "training-reserve-item", "training-spell", "training-berserker" }, "training-tactician"),
            ("Dynasty", 0, new[] { "training-farm", "training-castle", "training-dojo" }, "training-market-holding"),
            ("Dynasty", 1, new[] { "training-mine-holding", "training-temple-holding", "training-expensive-holding" }, "training-unique-personality"),
        })
        {
            var e = AnyDeckFixture(out var source);
            var allDeckIds = e.State.Players.SelectMany(p => p.FateDeck.Concat(p.DynastyDeck)).ToArray(); var rng = e.State.RngState.ToString();
            Accept(e, Use(e, source, "training-look-any")); Accept(e, ChooseDeck(e, deck, owner));
            var view = Look(e);
            Check(view.Cards!.Length == 3 && view.Cards.Length <= view.Requested, "Never more than N cards");
            var mine = Json(e.GetView(0)); var theirs = Json(e.GetView(1));
            foreach (var d in viewedDefinitions) Check(mine.Contains(d), $"{d} should be visible to the viewer");
            Check(!mine.Contains(hiddenBelow), $"{hiddenBelow} lies below the top N and must never be sent");
            HiddenIdsAbsent(e.GetView(0), allDeckIds); HiddenIdsAbsent(e.GetView(1), allDeckIds);
            foreach (var d in viewedDefinitions.Append(hiddenBelow)) Check(!theirs.Contains(d), $"{d} leaked to the other player (deck owner {owner})");
            Check(e.GetView(1).PendingLook is null && e.GetView(1).PendingChoice!.Prompt.Contains("look at the top 3 cards"), "The other player sees the public prompt only");
            foreach (var json in new[] { mine, theirs }) Check(!json.Contains("rng", StringComparison.OrdinalIgnoreCase) && !json.Contains(rng), "No generator state in any view");
            Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede") && !Json(e.QueryActions(1)).Contains(viewedDefinitions[0]), "The other player's action list reveals nothing");
            // Finish (keep everything): still nothing leaks, and the log names no card.
            Accept(e, Arrange(e, "0:top", "1:top", "2:top"));
            foreach (var d in viewedDefinitions.Append(hiddenBelow)) Check(!Json(e.GetView(1)).Contains(d), $"{d} leaked after the look (deck owner {owner})");
            Check(!Json(e.GetView(0).Events).Contains("training-") && !Json(e.GetView(0).Events).Contains("Training armor"), "The public event log names no card");
            HiddenIdsAbsent(e.GetView(1), allDeckIds);
        }
    }

    // ---- no shuffle, determinism, replay ----

    private static void NoShuffleAndReplay()
    {
        var e = MatchEngine.CreateDeckSearchShowcase(20260906);
        string SourceOf(string definition) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == definition).Id;
        var source = SourceOf("training-look-source"); var before = e.State; var rng = before.RngState;
        Accept(e, Use(e, source, "training-look-any")); Accept(e, ChooseDeck(e, "Fate", 0));
        Accept(e, Arrange(e, "0:top", "1:top", "2:top"));
        Equal(Json(e.State.Players[0].FateDeck), Json(before.Players[0].FateDeck), "Keeping everything leaves the deck byte-identical: there is no shuffle");
        Equal(e.State.RngState, rng);
        // A real rearrangement: reverse the top three, send one card of the opponent's deck to the bottom on the next turn.
        Accept(e, new("pass", 1, e.Version));
        Accept(e, Use(e, source, "training-look-fixed", player: 0));
        Check(e.State.PendingLook is not null);
        Accept(e, Arrange(e, "0:top", "1:discard", "2:top"));
        Equal(e.State.RngState, rng, "No look ever advances the generator");
        var journal = JsonSerializer.Deserialize<ReplayRecord>(Json(e.ExportReplay()))!;
        var recovered = MatchEngine.Replay(journal);
        Equal(Json(recovered.State), Json(e.State), "Replay reproduces the decks, the discard and the generator exactly");
        Equal(Json(recovered.GetView(0)), Json(e.GetView(0)));
        // The same seed and commands give the same match; and the deck really changed relative to the start.
        Check(Json(e.State.Players[0].FateDeck) != Json(before.Players[0].FateDeck));
        // Continue identically after recovery.
        foreach (var engine in new[] { e, recovered })
        {
            Accept(engine, new("pass", 1, engine.Version));
            Accept(engine, Use(engine, SourceOfIn(engine, "training-look-source"), "training-look-take-item"));
            var first = engine.QueryActions(0).First(a => a.Type == "arrangeTopCards"); Accept(engine, first.Command!);
        }
        Equal(Json(recovered.State), Json(e.State));
    }

    private static string SourceOfIn(MatchEngine e, string definition) => e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == definition).Id;

    // ---- seppuku and the bow reaction ----

    private static void SeppukuCannotInterruptALook()
    {
        var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys);
        var records = Put(s, PublicRecords, 0, Zone.Home);
        var samurai = Put(s, "training-samurai", 0, Zone.Home); samurai.Dishonorable = true;
        var enemy = Put(s, "training-samurai", 1, Zone.Home); enemy.Dishonorable = true;
        var e = Engine(s); Accept(e, Use(e, records.Id, "public-records-look"));
        Check(!e.QueryActions(0).Any(a => a.Type == "orderSeppuku") && !e.QueryActions(1).Any(a => a.Type == "orderSeppuku"), "No Seppuku is offered to either player mid-look");
        Reject(e, new("orderSeppuku", 0, e.Version, CardId: samurai.Id)); Reject(e, new("orderSeppuku", 1, e.Version, CardId: enemy.Id));
        Accept(e, Arrange(e, "0:top", "1:top", "2:top"));
        Check(e.QueryActions(0).Any(a => a.Type == "orderSeppuku") || e.State.PriorityPlayerId == 1, "Seppuku is available again once the look is over");

        // Tamaki's look bows nobody, so a watching Tamori Ruya has nothing to react to and the action simply resolves. (The shared
        // tail that opens a bow Reaction after a deferred action is exercised by the deck search suite, whose Shizuka bows herself.)
        var r = Start(); Deck(r, 0, Zone.FateDeck, Decoys);
        Put(r, "oracle-l5r-7789", 0, Zone.Home); var tamaki = Put(r, Tamaki, 0, Zone.Home);
        var re = Engine(r); Accept(re, Use(re, tamaki.Id, "isawa-tamaki-look")); Accept(re, Arrange(re, "0:top"));
        Equal(re.State.PendingChoice, null); Equal(re.State.PriorityPlayerId, 1);
    }

    // ---- the alternating before-action window (issue #327) ----

    // Unimpeachable Name is offered against ANY action of the other player (here because the reactor controls a Dishonorable
    // Personality it could rehonor), so the reactor's window opens BEFORE a Limited look or search resolves. The whole command is
    // deferred (never partly applied): nothing may be looked at, bowed or paid while the window is open, and once both players
    // pass, the same command re-executes into the look or search, which must still end with priority to the opponent.
    private static void BeforeActionWindowAheadOfLookAndSearch()
    {
        MatchState WithReactor(out CardInstance source, string definition)
        {
            var s = Start(); Deck(s, 0, Zone.FateDeck, Decoys); Deck(s, 0, Zone.DynastyDeck, "training-lion-samurai", "training-lion-courtier");
            source = Put(s, definition, 0, Zone.Home);
            var dishonorable = Put(s, "training-lion-courtier", 1, Zone.Home); dishonorable.Dishonorable = true;
            Put(s, UnimpeachableName, 1, Zone.Hand);
            return s;
        }
        void PassWindow(MatchEngine e)
        {
            Equal(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "The reactor's before-action window opens first");
            Equal(e.State.PendingChoice!.PlayerId, 1);
            Check(e.GetView(0).PendingLook is null && e.GetView(0).PendingSearch is null && e.GetView(1).PendingLook is null && e.GetView(1).PendingSearch is null, "No look or search exists while the window is open");
            // Rejecting a look/search answer during the window leaves it untouched.
            Reject(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:top"])); Reject(e, new("chooseSearchDeck", 0, e.Version, Choice: "Fate"));
            // Both players pass the window (the reactor first, as the window offers it), and the deferred action re-executes.
            for (var i = 0; i < 4 && e.State.PendingChoice?.Type == "unimpeachableNameReaction"; i++)
                Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        }

        // A look (Isawa Tamaki).
        var s = WithReactor(out var tamaki, Tamaki);
        var e = Engine(s); var before = Top(e, 0, "Fate");
        Accept(e, Use(e, tamaki.Id, "isawa-tamaki-look"));
        PassWindow(e);
        Equal(e.State.PendingChoice?.Type, "lookArrange", "After the window the deferred action re-executes into the look");
        Equal(e.State.PendingChoice!.PlayerId, 0);
        Equal(string.Join(",", Look(e).Cards!.Select(c => c.DefinitionId)), before[0], "The look shows the top card, which nothing disturbed");
        Check(e.GetView(1).PendingLook is null, "Still private to the viewer");
        Accept(e, Arrange(e, "0:bottom"));
        Equal(e.State.PendingChoice, null, "No window is left open after the look"); Equal(e.State.PriorityPlayerId, 1, "Priority passes to the opponent");
        Equal(string.Join(",", Top(e, 0, "Fate")), string.Join(",", before.Skip(1).Append(before[0])));

        // The reactor may instead USE its reaction (rehonoring), and the look still follows.
        var used = Engine(WithReactor(out var tamaki2, Tamaki)); Accept(used, Use(used, tamaki2.Id, "isawa-tamaki-look"));
        var reactionCard = used.State.Cards.Values.First(c => c.DefinitionId == UnimpeachableName);
        var dishonorable = used.State.Cards.Values.First(c => c.OwnerId == 1 && c.Dishonorable);
        Accept(used, new("useAbility", 1, used.Version, CardId: reactionCard.Id, TargetId: dishonorable.Id, AbilityId: "unimpeachable-name-reaction"));
        for (var i = 0; i < 4 && used.State.PendingChoice?.Type == "unimpeachableNameReaction"; i++) Accept(used, new("declineReaction", used.State.PriorityPlayerId, used.Version));
        Equal(used.State.PendingChoice?.Type, "lookArrange", "The look follows a window in which a reaction was used");
        Check(!used.State.Cards[dishonorable.Id].Dishonorable, "The reaction itself resolved");

        // A search (Ikoma Shizuka bows herself as her cost -- only AFTER the window).
        var ss = WithReactor(out var shizuka, "oracle-l5r-10292");
        var se = Engine(ss);
        Accept(se, Use(se, shizuka.Id, "shizuka-search"));
        Check(!se.State.Cards[shizuka.Id].Bowed, "The cost is not paid while the before-action window is open");
        PassWindow(se);
        Equal(se.State.PendingChoice?.Type, "searchDeck", "After the window the deferred action re-executes into the search");
        Check(se.State.Cards[shizuka.Id].Bowed, "The cost is paid once, when the action really resolves");
        Accept(se, new("chooseSearchDeck", 0, se.Version, Choice: "Fate"));
        Accept(se, new("passSearch", 0, se.Version));
        Equal(se.State.PendingChoice, null, "No window is left open after the search"); Equal(se.State.PriorityPlayerId, 1, "Priority passes to the opponent");
    }

    // ---- no deadlock ----

    private static void ViewerAlwaysHasALegalAnswer()
    {
        foreach (var seed in new ulong[] { 1, 2, 42, 20260906 })
            foreach (var ability in new[] { "training-look-dynasty", "training-look-any", "training-look-take-item", "training-look-fixed", "training-look-show" })
                for (var deckIndex = 0; deckIndex < 4; deckIndex++)
                {
                    var e = MatchEngine.CreateDeckSearchShowcase(seed);
                    Accept(e, Use(e, SourceOfIn(e, "training-look-source"), ability));
                    var steps = 0;
                    while (e.State.PendingChoice is { } pending && pending.Type.StartsWith("look"))
                    {
                        var offered = AssertViewerHasAnswer(e);
                        Check(offered.Length <= 64, "The options stay bounded");
                        var state = e.State;
                        foreach (var option in offered) Accept(MatchEngine.FromTestState(state), option.Command!);   // every offered command validates on a fresh engine
                        var decks = offered.Where(a => a.Type == "chooseLookDeck").ToArray();
                        Accept(e, decks.Length > 0 ? decks[Math.Min(deckIndex, decks.Length - 1)].Command! : offered[0].Command!);
                        Check(++steps < 6, "The look did not finish in a bounded number of decisions");
                    }
                    Check(e.State.PendingLook is null && e.State.PendingChoice is null, $"{ability}/{seed}: the look finished");
                    Equal(e.State.PriorityPlayerId, 1, "The action resolved and play continued");
                }
        // The Event path: both deck choices of Signs and Portents.
        string[] own = [Decoys[0], Decoys[1], Decoys[2], Decoys[3], Decoys[4]];
        for (var i = 0; i < 2; i++)
        {
            var e = EndTurn(SignsState(own, ["training-cavalry", "training-monk", "training-berserker"]));
            var offered = AssertViewerHasAnswer(e); Accept(e, offered[i].Command!);
            Equal(e.State.PendingChoice?.Type, "lookArrange");
            var arrangements = AssertViewerHasAnswer(e);
            Check(arrangements.Length >= 2, "Keep everything, or discard any one card");
            Accept(e, arrangements[^1].Command!);
            Equal(e.State.Phase, Phase.Action);
        }
    }
}
