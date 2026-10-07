using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #690, primitive P24 (EffectPrimitivesTokens.cs): the three-stat token, the two shuffles and the control return owed when its source straightens. Fixture abilities only.
internal static class EffectPrimitivesZoneTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);

    public static void Run(TestRunner runner) => runner.Run("p24-zones-tokens", new (string, Action)[]
    {
        ("Three-stat token: one token gives +1F/+1C/+1PH, read by every stat; it is one token to count and to destroy", ThreeStatToken),
        ("Three-stat token: each stat is negated on its own; a Chi left at 0 destroys the Personality; one- and two-stat tokens are unchanged", TokenStatsNegatedSeparately),
        ("CardToken with a third stat round-trips, and a token saved before it existed loads with none", TokenSavedState),
        ("ShuffleIntoDeck: one to three chosen non-Unique Strategies leave the discard pile for their owner's deck, which is shuffled; the count is enforced", ShuffleDiscardsIntoDeck),
        ("ShuffleHandsAndRedraw: each player's hand goes into his deck and he draws as many; the card being played stays; nothing hidden is logged", ShuffleHandsRedraw),
        ("SourceStraightens: control returns after the source straightens, or leaves play, and not while it stays bowed", ControlReturnsOnStraighten),
        ("SourceStraightens: a source that was never bowed has not straightened; the owed return survives a saved game", ControlReturnNeedsAStraighten),
    });

    private static void Grant(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static CardInstance Now(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];

    // ---- tokens ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ThreeStatToken()
    {
        var s = Batch336Support.Table(Phase.Action);
        var source = Add(s, "training-lion-courtier", 1, Zone.Home); Grant(source, EffectPrimitiveFixtures.ThreeStatToken);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var (force, chi, honor) = (e.CardForceForTest(target.Id), e.PersonalityChiForTest(target.Id), e.PersonalHonorForTest(target.Id));
        Accept(e, Batch336Support.Offer(e, 1, EffectPrimitiveFixtures.ThreeStatToken, source.Id, target.Id)); Settle(e);
        Eq(e.CardForceForTest(target.Id), force + 1, "+1 Force"); Eq(e.PersonalityChiForTest(target.Id), chi + 1, "+1 Chi"); Eq(e.PersonalHonorForTest(target.Id), honor + 1, "+1 Personal Honor");
        var tokens = Now(e, target).Tokens;
        Eq(tokens.Count, 1, "it is one token");
        Eq(tokens[0], new CardToken(tokens[0].Id, "Tomb", source.Id, "Force", 1, "Chi", 1, "PersonalHonor", 1), "carrying its three stats");
        Check(e.State.Events.Any(x => x.Type == "tokenCreated" && x.Message.Contains("+1F/+1C/+1PH")), "the log names all three: " + e.State.Events.Last(x => x.Type == "tokenCreated").Message);

        // A token is not a turn-long modifier: it is still there next turn, and all three stats go with it when it is destroyed.
        var later = Batch336Support.Edit(e, st => st.TurnNumber++);
        Eq(later.CardForceForTest(target.Id), force + 1, "still +1 Force next turn");
        var gone = Batch336Support.Edit(e, st => st.Cards[target.Id].Tokens.Clear());
        Eq((gone.CardForceForTest(target.Id), gone.PersonalityChiForTest(target.Id), gone.PersonalHonorForTest(target.Id)), (force, chi, honor), "without the token every stat is back");
    }

    private static void TokenStatsNegatedSeparately()
    {
        var s = Batch336Support.Table(Phase.Action);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home); // Force 4, Chi 3, Personal Honor 2
        target.ForcePenaltiesNegatedTurn = s.TurnNumber;           // Tireless Efforts' shape: Force penalties on him are negated this turn
        var e = MatchEngine.FromTestState(s);
        var token = e.CreateMultiStatToken(e.CardForTest(target.Id), "Curse", "test-source", ("Force", -1), ("Chi", -1), ("PersonalHonor", -1));
        Eq(token, new CardToken(token!.Id, "Curse", "test-source", "Chi", -1, "PersonalHonor", -1), "the negated Force penalty is left off; the other two stay, packed");
        Eq((e.CardForceForTest(target.Id), e.PersonalityChiForTest(target.Id), e.PersonalHonorForTest(target.Id)), (4, 2, 1), "Force untouched, Chi and Personal Honor lowered");

        // A Chi brought to 0 destroys the Personality, as for any token.
        var s2 = Batch336Support.Table(Phase.Action);
        var frail = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        e2.CreateMultiStatToken(e2.CardForTest(frail.Id), "Curse", "test-source", ("Force", -1), ("Chi", -3), ("PersonalHonor", -1));
        Check(Now(e2, frail).Zone != Zone.Home, "a Chi of 0 destroyed him: " + Now(e2, frail).Zone);

        // One and two stats make the tokens the engine already had.
        var s3 = Batch336Support.Table(Phase.Action);
        var plain = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        var one = e3.CreateMultiStatToken(e3.CardForTest(plain.Id), "Fire", "test-source", ("Force", 1));
        var two = e3.CreateMultiStatToken(e3.CardForTest(plain.Id), "Training", "test-source", ("Force", 1), ("Chi", 1));
        Eq(one, new CardToken(one!.Id, "Fire", "test-source", "Force", 1), "a one-stat token");
        Eq(two, new CardToken(two!.Id, "Training", "test-source", "Force", 1, "Chi", 1), "a two-stat token");
        Eq((e3.CardForceForTest(plain.Id), e3.PersonalityChiForTest(plain.Id), e3.PersonalHonorForTest(plain.Id)), (6, 4, 2), "each read once");
    }

    private static void TokenSavedState()
    {
        var token = new CardToken("token-0001", "Tomb", "src", "Force", 1, "Chi", 1, "PersonalHonor", 1);
        Eq(JsonSerializer.Deserialize<CardToken>(JsonSerializer.Serialize(token)), token, "a three-stat token round-trips");
        // The shape every save written before this primitive has: no Stat3, no Amount3.
        var legacy = JsonSerializer.Deserialize<CardToken>("""{"Id":"token-0007","Type":"Poison","Source":"src","Stat":"Force","Amount":-1,"Stat2":"Chi","Amount2":-1}""");
        Eq(legacy, new CardToken("token-0007", "Poison", "src", "Force", -1, "Chi", -1), "an older two-stat token loads with no third stat");
        var oldest = JsonSerializer.Deserialize<CardToken>("""{"Id":"token-0002","Type":"Fire","Source":"src","Stat":"Force","Amount":1}""");
        Eq(oldest, new CardToken("token-0002", "Fire", "src", "Force", 1), "and so does a one-stat token");

        // In a whole saved game: a state with a three-stat token reloads with it, and reads the same stats.
        var s = Batch336Support.Table(Phase.Action);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        e.CreateMultiStatToken(e.CardForTest(target.Id), "Tomb", "src", ("Force", 1), ("Chi", 1), ("PersonalHonor", 1));
        var reloaded = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Eq(reloaded.State.Cards[target.Id].Tokens.Single(), e.State.Cards[target.Id].Tokens.Single(), "the token survives the save");
        Eq(reloaded.PersonalHonorForTest(target.Id), e.PersonalHonorForTest(target.Id), "and its third stat is still read");
    }

    // ---- shuffles --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShuffleDiscardsIntoDeck()
    {
        MatchEngine Rig(out CardInstance source, out CardInstance[] mine, out CardInstance unique, out CardInstance enemy)
        {
            var s = Batch336Support.Table(Phase.Action);
            source = Add(s, "training-lion-courtier", 1, Zone.Home); Grant(source, EffectPrimitiveFixtures.ShuffleDiscards);
            mine = [Add(s, "training-rally", 1, Zone.FateDiscard), Add(s, "training-pressure", 1, Zone.FateDiscard), Add(s, "training-rally", 1, Zone.FateDiscard), Add(s, "training-pressure", 1, Zone.FateDiscard)];
            unique = Add(s, "training-rally", 1, Zone.FateDiscard); unique.Printed = CardCatalog.Get("training-rally") with { Keywords = ["Unique"] };
            enemy = Add(s, "training-rally", 0, Zone.FateDiscard);
            return MatchEngine.FromTestState(s);
        }

        var e = Rig(out var source, out var mine, out var unique, out var enemy);
        var slot = Batch336Support.Enabled(e, 1, EffectPrimitiveFixtures.ShuffleDiscards, source.Id).Slots!.Single();
        Check(slot.Min == 1 && slot.Max == 3, "one to three");
        Check(slot.CardIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(mine.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)), "his own non-Unique Strategies in his discard pile: " + string.Join(",", slot.CardIds));
        var deck = e.State.Players[1].FateDeck.Count;
        Accept(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.ShuffleDiscards, source.Id, null, null, Pick("cards", mine[0].Id, mine[1].Id, mine[3].Id))); Settle(e);
        foreach (var card in new[] { mine[0], mine[1], mine[3] })
        {
            Eq(Now(e, card).Zone, Zone.FateDeck, "the chosen card is in the Fate deck");
            Check(e.State.Players[1].FateDeck.Contains(card.Id) && !Now(e, card).FaceUp, "in its owner's deck list, face down");
        }
        Eq(e.State.Players[1].FateDeck.Count, deck + 3, "three cards deeper");
        Eq(Now(e, mine[2]).Zone, Zone.FateDiscard, "the card not chosen stays in the discard pile");
        Check(e.State.Events.Any(x => x.Type == "deckShuffled" && x.PlayerId == 1), "the deck was shuffled");

        var r = Rig(out var source2, out var mine2, out var unique2, out var enemy2);
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.ShuffleDiscards, source2.Id));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.ShuffleDiscards, source2.Id, null, null, Pick("cards", mine2.Select(c => c.Id).ToArray())));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.ShuffleDiscards, source2.Id, null, null, Pick("cards", unique2.Id)));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.ShuffleDiscards, source2.Id, null, null, Pick("cards", enemy2.Id)));

        // The helper skips a card that has left the discard pile since it was chosen, and says how many went in.
        var h = Rig(out var source3, out var mine3, out _, out _);
        var moved = Batch336Support.Edit(h, st => st.Cards[mine3[0].Id].Zone = Zone.RemovedFromGame);
        Eq(moved.ShuffleIntoDeck([moved.CardForTest(mine3[0].Id), moved.CardForTest(mine3[1].Id)], moved.CardForTest(source3.Id)), 1, "only the card still in a discard pile is shuffled in");
        Eq(moved.State.Cards[mine3[0].Id].Zone, Zone.RemovedFromGame);
        Check(unique.OwnerId == 1 && enemy.OwnerId == 0, "fixture sanity");
    }

    private static void ShuffleHandsRedraw()
    {
        var s = Batch336Support.Table(Phase.Action);
        var source = Add(s, "training-lion-courtier", 1, Zone.Home); Grant(source, EffectPrimitiveFixtures.ShuffleRedraw);
        var hand1 = new[] { Add(s, "training-rally", 1, Zone.Hand), Add(s, "training-pressure", 1, Zone.Hand), Add(s, "training-rally", 1, Zone.Hand) };
        var hand0 = new[] { Add(s, "training-rally", 0, Zone.Hand), Add(s, "training-pressure", 0, Zone.Hand) };
        s.RemAShuffledCount = 7; // another card's own count: not this primitive's to change
        var e = MatchEngine.FromTestState(s);
        var (deck1, deck0) = (e.State.Players[1].FateDeck.Count, e.State.Players[0].FateDeck.Count);
        var seen = e.State.Events.Count;
        Accept(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.ShuffleRedraw, source.Id)); Settle(e);
        var after = e.State;
        Eq((after.Players[1].Hand.Count, after.Players[0].Hand.Count), (3, 2), "each player holds as many cards as he shuffled in");
        Eq((after.Players[1].FateDeck.Count, after.Players[0].FateDeck.Count), (deck1, deck0), "and each deck is as deep as it was");
        foreach (var player in after.Players)
        {
            Check(player.Hand.All(id => after.Cards[id].Zone == Zone.Hand && after.Cards[id].OwnerId == player.Id), "every hand card is in its owner's hand");
            Check(player.FateDeck.All(id => after.Cards[id].Zone == Zone.FateDeck), "every deck card is in the deck");
            Eq(player.Hand.Concat(player.FateDeck).Distinct().Count(), player.Hand.Count + player.FateDeck.Count, "no card is in two places");
        }
        Check(hand1.Concat(hand0).All(c => after.Cards[c.Id].Zone is Zone.Hand or Zone.FateDeck), "the cards that were in hand are in the deck or drawn back");
        var events = after.Events.Skip(seen).ToArray();
        Eq(events.Count(x => x.Type == "handShuffled"), 2, "both hands were shuffled in");
        Eq(events.Count(x => x.Type == "cardDrawn"), 5, "three and two cards drawn");
        Check(events.Where(x => x.Type is "handShuffled" or "cardDrawn" or "deckShuffled").All(x => x.CardId is null && !x.Message.Contains("rally", StringComparison.OrdinalIgnoreCase) && !x.Message.Contains("pressure", StringComparison.OrdinalIgnoreCase)), "no hidden card is named");
        Eq(after.RemAShuffledCount, 7, "Face of Ninube's count is left alone");

        // A Strategy played from the hand is not shuffled into the deck by its own effect; the count it returns is the cards that were.
        var s2 = Batch336Support.Table(Phase.Action);
        var played = Add(s2, "training-rally", 1, Zone.Hand); Add(s2, "training-pressure", 1, Zone.Hand); Add(s2, "training-pressure", 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var counts = e2.ShuffleHandsAndRedraw([1, 0], e2.CardForTest(played.Id));
        Eq((counts[1], counts[0]), (2, 0), "two of the three cards (the played one is left out), and none for the empty hand");
        Eq(e2.State.Cards[played.Id].Zone, Zone.Hand, "the played card is still in the hand");
        Eq(e2.State.Players[1].Hand.Count, 3, "the played card and the two drawn");
    }

    // ---- the straighten-triggered return ---------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1's Action Phase: his taker (the fixture's source) and a second card that can straighten an Air Personality; the Defender's Personality to take.
    private static (MatchEngine E, CardInstance Taker, CardInstance Straightener, CardInstance Taken) TakeRig()
    {
        var s = Batch336Support.Table(Phase.Action);
        var taker = Add(s, "training-lion-veteran", 1, Zone.Home); Grant(taker, EffectPrimitiveFixtures.TakeUntilStraighten);
        taker.GrantedKeywords.Add(new("Air", "test-fixture", GrantDuration.Permanent));
        var straightener = Add(s, "training-lion-courtier", 1, Zone.Home); Grant(straightener, "air-straighten");
        var taken = Add(s, "training-crane-samurai", 0, Zone.Home);
        return (MatchEngine.FromTestState(s), taker, straightener, taken);
    }

    private static MatchEngine MyTurnAgain(MatchEngine e) => Batch336Support.Edit(e, st => { st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; st.ConsecutivePasses = 0; });

    private static void ControlReturnsOnStraighten()
    {
        var (e, taker, straightener, taken) = TakeRig();
        Accept(e, Batch336Support.Offer(e, 1, EffectPrimitiveFixtures.TakeUntilStraighten, taker.Id, taken.Id)); Settle(e);
        Check(Now(e, taker).Bowed, "the source was bowed as the cost");
        Eq((Now(e, taken).OwnerId, Now(e, taken).OriginalOwnerId), (1, (int?)0), "control was taken");
        Eq(e.State.MechB.ControlReturns.Single().Trigger, ControlReturnTriggers.SourceStraightens, "and its return is owed on the source straightening");

        // Another command while the source stays bowed: nothing returns.
        var still = MyTurnAgain(e);
        Accept(still, new GameCommand("pass", still.State.PriorityPlayerId, still.Version));
        Eq(still.State.Cards[taken.Id].OwnerId, 1, "while the source stays bowed, control stays");

        // The source is straightened by an effect: control returns at once.
        var back = MyTurnAgain(e);
        Accept(back, Batch336Support.Offer(back, 1, "air-straighten", straightener.Id, taker.Id)); Settle(back);
        Check(!back.State.Cards[taker.Id].Bowed, "the source straightened");
        Eq((back.State.Cards[taken.Id].OwnerId, back.State.Cards[taken.Id].OriginalOwnerId), (0, (int?)null), "control was given back to his owner");
        Eq(back.State.MechB.ControlReturns.Count, 0, "and nothing is owed any more");
        Check(back.State.Events.Any(x => x.Type == "controlReturned" && x.CardId == taken.Id), "the return is logged");

        // The source leaves play instead: control returns as well.
        var left = MyTurnAgain(e);
        left = Batch336Support.Edit(left, st => st.Cards[taker.Id].Zone = Zone.Dead);
        Accept(left, new GameCommand("pass", left.State.PriorityPlayerId, left.Version));
        Eq(left.State.Cards[taken.Id].OwnerId, 0, "the source left play: control was given back");
    }

    private static void ControlReturnNeedsAStraighten()
    {
        // Registered while the source is unbowed (its bow cost was waived, say): it has not straightened, so nothing returns until it has been bowed and then straightens.
        var (e, taker, _, taken) = TakeRig();
        var live = Batch336Support.Edit(e, st => { st.Cards[taken.Id].OriginalOwnerId = 0; st.Cards[taken.Id].OwnerId = 1; });
        live.ReturnControlWhenSourceStraightens(live.CardForTest(taken.Id), live.CardForTest(taker.Id), "test: after it straightens or leaves play");
        Accept(live, new GameCommand("pass", live.State.PriorityPlayerId, live.Version));
        Eq(live.State.Cards[taken.Id].OwnerId, 1, "an unbowed source has not straightened");
        Check(!live.State.MechB.ControlReturns.Single().SourceSeenBowed, "and has not been seen bowed");

        var bowed = MyTurnAgain(Batch336Support.Edit(live, st => st.Cards[taker.Id].Bowed = true));
        Accept(bowed, new GameCommand("pass", bowed.State.PriorityPlayerId, bowed.Version));
        Eq(bowed.State.Cards[taken.Id].OwnerId, 1, "bowed: still owed");
        Check(bowed.State.MechB.ControlReturns.Single().SourceSeenBowed, "now seen bowed");

        // Through a saved game, then straightened.
        var saved = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(bowed.State))!;
        Check(saved.MechB.ControlReturns.Single().SourceSeenBowed, "the owed return and what it has seen survive the save");
        saved.Cards[taker.Id].Bowed = false; saved.PriorityPlayerId = 1; saved.ActivePlayerId = 1; saved.PendingChoice = null; saved.ConsecutivePasses = 0;
        var straight = MatchEngine.FromTestState(saved);
        Accept(straight, new GameCommand("pass", straight.State.PriorityPlayerId, straight.Version));
        Eq(straight.State.Cards[taken.Id].OwnerId, 0, "it straightened: control was given back");
    }
}
