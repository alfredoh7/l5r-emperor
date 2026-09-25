using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: Moon and Sun, Naoharu's Gift, Rebuilding the Empire, Benefices of the Emperor and Abandoning the Fortunes (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    // Player 0 has no Fate card to draw at his End Phase, so a hand emptied for the test is still empty when the Event resolves.
    private static void NoFateDraw(MatchState s, int player)
    {
        foreach (var id in s.Players[player].FateDeck.ToArray()) s.Cards.Remove(id);
        s.Players[player].FateDeck.Clear();
    }

    // ---- Moon and Sun -----------------------------------------------------------------------------------------------------------------------------------------

    private static void MoonAndSunTests()
    {
        // Each player who has an Event in his discard pile may shuffle one into his Dynasty deck, the revealing player first.
        CardInstance? mine = null, theirs = null;
        var e = Reveal(MoonAndSun, out var moon, s => { mine = Add(s, CherryBlossomFestival, 1, Zone.DynastyDiscard); theirs = Add(s, Disgrace, 0, Zone.DynastyDiscard); });
        Eq(e.State.PendingChoice?.Type, "eventChoice", "the revealing player is asked first");
        Eq(e.State.PriorityPlayerId, 1, "player 1 decides");
        Check(EventOptions(e, 1).Any(l => l.Contains("Cherry Blossom")) && EventOptions(e, 1).Any(l => l.EndsWith("none")) && !EventOptions(e, 1).Any(l => l.Contains("Disgrace")), "only his own discarded Events, or none: " + string.Join("; ", EventOptions(e, 1)));
        var deck1 = e.State.Players[1].DynastyDeck.Count;
        AnswerEvent(e, target: mine!.Id);
        Eq(e.State.Cards[mine.Id].Zone, Zone.DynastyDeck, "his Event was shuffled into his Dynasty deck");
        Check(e.State.Players[1].DynastyDeck.Contains(mine.Id) && e.State.Players[1].DynastyDeck.Count >= deck1, "it is in the deck list");
        Eq(e.State.PendingChoice?.Type, "eventChoice", "then the other player is asked");
        Eq(e.State.PriorityPlayerId, 0, "player 0 decides");
        AnswerEvent(e, choice: "none");
        Check(e.State.PendingChoice is null && e.State.Cards[theirs!.Id].Zone == Zone.DynastyDiscard, "he shuffled nothing: his Event stays discarded");
        Eq(e.State.Cards[moon.Id].Zone, Zone.DynastyDiscard, "Moon and Sun is discarded once resolved");
        Eq(e.State.Phase, Phase.Action, "the Events Phase carried on");
        // A player with no Event in his discard pile is not asked; with none at all, nothing happens.
        var e2 = Reveal(MoonAndSun, out _, s => Add(s, CherryBlossomFestival, 0, Zone.DynastyDiscard));
        Eq(e2.State.PriorityPlayerId, 0, "only the other player has an Event");
        Check(EventOptions(e2, 0).Any(), "he is asked");
        AnswerEvent(e2, target: e2.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.DynastyDiscard && CardCatalog.Get(c.DefinitionId).Type == CardType.Event).Id);
        Check(e2.State.PendingChoice is null, "and that is all");
        var e3 = Reveal(MoonAndSun, out var moon3);
        Check(e3.State.PendingChoice is null && e3.State.Cards[moon3.Id].Zone == Zone.DynastyDiscard, "no Event in either discard pile: nothing to ask: " + string.Join("; ", EventOptions(e3, e3.State.PriorityPlayerId)) + " " + e3.State.PendingChoice?.Type);
    }

    // ---- Naoharu's Gift ---------------------------------------------------------------------------------------------------------------------------------------

    private static void NaoharusGiftTests()
    {
        CardInstance? gift = null, other = null;
        MatchEngine Offered(out CardInstance evt) => Reveal(NaoharusGift, out evt, s => { gift = Add(s, "training-focus-strategy", 1, Zone.Hand); other = Add(s, "training-item", 1, Zone.Hand); });
        // He shows a card, and the other player takes it: the giver gains Honor equal to its base Focus Value.
        var e = Offered(out var evt);
        Eq(e.State.PriorityPlayerId, 1, "the revealing player chooses the card to show");
        Eq(EventOptions(e, 1).Length, 2, "any card in his hand");
        var honor = Honor(e, 1);
        AnswerEvent(e, target: gift!.Id);
        Check(e.State.ShownCards.Any(c => c.DefinitionId == "training-focus-strategy"), "the card was shown to the other player");
        Eq(e.State.PriorityPlayerId, 0, "the other player decides");
        Check(EventOptions(e, 0).Any(l => l.Contains("put")) && EventOptions(e, 0).Any(l => l.Contains("refuse")), "put it in his hand, or refuse");
        AnswerEvent(e, choice: "yes");
        Check(e.State.Players[0].Hand.Contains(gift.Id) && !e.State.Players[1].Hand.Contains(gift.Id), "the gift is in his hand");
        Eq(e.State.Cards[gift.Id].OwnerId, 0, "his to play while it is in his hand");
        Eq(Honor(e, 1), honor + 3, "the giver gained Honor equal to its base Focus Value (3)");
        Eq(e.State.Cards[evt.Id].Zone, Zone.DynastyDiscard, "the Event is over");
        // Refused: the card is discarded and its owner draws two cards.
        var e2 = Offered(out _);
        var handBefore = e2.State.Players[1].Hand.Count; var honor2 = Honor(e2, 1);
        AnswerEvent(e2, target: gift!.Id);
        AnswerEvent(e2, choice: "no");
        Eq(e2.State.Cards[gift.Id].Zone, Zone.FateDiscard, "the refused gift is discarded");
        Eq(e2.State.Cards[gift.Id].OwnerId, 1, "into its owner's pile");
        Eq(e2.State.Players[1].Hand.Count, handBefore - 1 + 2, "and he draws two cards");
        Eq(Honor(e2, 1), honor2, "no Honor");
        // With no card to show there is nothing to offer.
        var e3 = Reveal(NaoharusGift, out var evt3);
        Check(e3.State.PendingChoice is null && e3.State.Cards[evt3.Id].Zone == Zone.DynastyDiscard, "an empty hand shows nothing");
    }

    // ---- Rebuilding the Empire --------------------------------------------------------------------------------------------------------------------------------

    private static void DestroyProvinceOf(MatchState s, int player, int province)
    {
        var p = s.Players[player].Provinces[province];
        if (p.CardId is { } id) { s.Cards[id].Zone = Zone.DynastyDiscard; s.Cards[id].ProvinceIndex = null; }
        p.CardId = null; p.Destroyed = true;
    }

    private static void RebuildingTheEmpireTests()
    {
        // Nobody has three or fewer provinces: the Event does nothing.
        var e0 = Reveal(RebuildingTheEmpire, out var evt0);
        Check(e0.State.PendingChoice is null && e0.State.Cards[evt0.Id].Zone == Zone.DynastyDiscard, "with four provinces each nobody is eligible: " + string.Join("; ", EventOptions(e0, e0.State.PriorityPlayerId)) + " " + e0.State.PendingChoice?.Type + " players provinces " + e0.State.Players[0].Provinces.Count(p => !p.Destroyed) + "/" + e0.State.Players[1].Provinces.Count(p => !p.Destroyed));
        // Player 0 has three: he is asked (the revealing player, with four, is not).
        var e = Reveal(RebuildingTheEmpire, out var evt, s => { DestroyProvinceOf(s, 0, 3); s.Players[0].Honor = StartingHonorOf(s, 0) + 7; });
        Eq(e.State.PriorityPlayerId, 0, "the eligible player decides");
        Check(EventOptions(e, 0).Any(l => l.Contains("gain a province")) && EventOptions(e, 0).Any(l => l.Contains("decline")), "gain or decline");
        var start = CardCatalog.Get(e.State.Cards[e.State.Players[0].StrongholdId].DefinitionId).StartingHonor;
        var turn = e.State.TurnNumber;
        AnswerEvent(e, choice: "yes");
        var gained = e.State.Players[0];
        Eq(gained.Provinces.Count, 5, "he gained a province");
        Eq(gained.Provinces[^1].Index, 4, "to the right of his rightmost province");
        Check(!gained.Provinces[^1].Destroyed && gained.Provinces[^1].CardId is not null, "filled face-down from his Dynasty deck");
        Eq(gained.Provinces[^1].Strength, CardCatalog.Get(e.State.Cards[gained.StrongholdId].DefinitionId).ProvinceStrength, "at the Stronghold's base strength");
        Eq(gained.Honor, start, "his Family Honor was lowered to its starting value");
        Eq(gained.AttackLockedThroughTurn, turn + 1, "and he may not declare an attack until the revealing player's next turn begins");
        Eq(e.State.Cards[evt.Id].Zone, Zone.DynastyDiscard, "the Event is over");
        // The lock: the declare-an-attack option is refused on his own turn (the very next one), and lifted the turn after.
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.AttackDeclaration);
        var declare = e.QueryActions(0).First(o => o.Type == "declareAttack");
        Check(!declare.Enabled, "no attack on his next turn: " + declare.Reason);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.AttackDeclaration);
        Check(e.QueryActions(0).First(o => o.Type == "declareAttack").Enabled, "the lock lapsed when the revealing player's next turn began");
        // Declined: nothing happens.
        var e2 = Reveal(RebuildingTheEmpire, out _, s => DestroyProvinceOf(s, 0, 3));
        AnswerEvent(e2, choice: "no");
        Eq(e2.State.Players[0].Provinces.Count, 4, "declined: no province");
        Eq(e2.State.Players[0].AttackLockedThroughTurn, -1, "and no lock");
        // Already at or below his starting Family Honor: nothing to lower (it is only ever lowered).
        var e3 = Reveal(RebuildingTheEmpire, out _, s => { DestroyProvinceOf(s, 0, 3); s.Players[0].Honor = StartingHonorOf(s, 0) - 4; });
        var low = e3.State.Players[0].Honor;
        AnswerEvent(e3, choice: "yes");
        Eq(e3.State.Players[0].Honor, low, "his Honor was below its starting value already: unchanged");
        // Both players eligible: the revealing player is asked first, then the other.
        var e4 = Reveal(RebuildingTheEmpire, out _, s => { DestroyProvinceOf(s, 0, 3); DestroyProvinceOf(s, 1, 2); });
        Eq(e4.State.PriorityPlayerId, 1, "the revealing player first");
        AnswerEvent(e4, choice: "yes");
        Eq(e4.State.PriorityPlayerId, 0, "then the other player");
        AnswerEvent(e4, choice: "no");
        Check(e4.State.Players[1].Provinces.Count == 5 && e4.State.Players[0].Provinces.Count == 4, "only the one who said yes gained one");
    }

    // ---- Benefices of the Emperor -----------------------------------------------------------------------------------------------------------------------------

    private static void BeneficesTests()
    {
        // Player 1 (the revealing player, asked first) discards a Focus 1 card, player 0 a Focus 3 card: the lowest gains a province to the left of his leftmost, the highest +3 hand size and three cards.
        CardInstance? low = null, high = null;
        var e = Reveal(Benefices, out var evt, s => { low = Add(s, "training-stat-cost-low-strategy", 1, Zone.Hand); high = Add(s, "training-focus-strategy", 0, Zone.Hand); }, province: 1);
        Eq(e.State.PriorityPlayerId, 1, "the revealing player chooses first");
        Check(EventOptions(e, 1).Any(l => l.EndsWith("none")), "he may choose none");
        var handBefore0 = e.State.Players[0].Hand.Count;
        AnswerEvent(e, target: low!.Id);
        Eq(e.State.PriorityPlayerId, 0, "then the other player");
        Check(e.State.Cards[low.Id].Zone == Zone.Hand, "nothing is discarded until everyone has chosen");
        AnswerEvent(e, target: high!.Id);
        Eq(e.State.Cards[low.Id].Zone, Zone.FateDiscard, "all chosen cards are discarded");
        Eq(e.State.Cards[high.Id].Zone, Zone.FateDiscard, "both");
        var p1 = e.State.Players[1];
        Eq(p1.Provinces.Count, 5, "the lowest Focus Value's player gained a province");
        Eq(p1.Provinces[0].Index, 0, "to the left of his leftmost province");
        Check(p1.Provinces[0].CardId is not null, "filled from his Dynasty deck");
        foreach (var province in p1.Provinces.Where(p => p.CardId is not null))
            Eq(e.State.Cards[province.CardId!].ProvinceIndex, province.Index, $"the card in province {province.Index} names it");
        Eq(e.State.Players[0].Provinces.Count, 4, "the other player's row is unchanged");
        var p0 = e.State.Players[0];
        Eq(p0.MaxHandBonus, 3, "the highest Focus Value's player has +3 maximum hand size, permanently");
        Eq(e.MaxHandSizeForTest(0), 11, "reading 11");
        Eq(p0.Hand.Count, handBefore0 - 1 + 3, "and drew three cards");
        Eq(e.State.Phase, Phase.Action, "the Events Phase carried on (each province revealed once)");
        Eq(p1.Provinces.Count(p => p.CardId is not null && e.State.Cards[p.CardId].FaceUp), p1.Provinces.Count(p => p.CardId is not null && e.State.Cards[p.CardId].FaceUp), "sanity");
        // Equal Focus Values: neither is the only one, so nothing is gained.
        CardInstance? a = null, b = null;
        var e2 = Reveal(Benefices, out _, s => { a = Add(s, "training-focus-strategy", 1, Zone.Hand); b = Add(s, "training-focus-strategy", 0, Zone.Hand); });
        AnswerEvent(e2, target: a!.Id); AnswerEvent(e2, target: b!.Id);
        Check(e2.State.Players[1].Provinces.Count == 4 && e2.State.Players[0].MaxHandBonus == 0 && e2.State.Players[1].MaxHandBonus == 0, "a tie is nobody's");
        Eq(e2.State.Cards[a.Id].Zone, Zone.FateDiscard, "yet the chosen cards are discarded");
        // Only one player chooses a card: it is the lowest and the highest, so he gets both.
        CardInstance? only = null;
        var e3 = Reveal(Benefices, out _, s => { only = Add(s, "training-focus-strategy", 1, Zone.Hand); NoFateDraw(s, 0); });
        var handBefore = e3.State.Players[1].Hand.Count;
        AnswerEvent(e3, target: only!.Id);
        Check(e3.State.PendingChoice is null, "the other player has no card to choose");
        Eq(e3.State.Players[1].Provinces.Count, 5, "a province");
        Eq(e3.State.Players[1].MaxHandBonus, 3, "and +3 maximum hand size");
        Eq(e3.State.Players[1].Hand.Count, handBefore - 1 + 3, "and three cards");
        // Choosing none: the other's lone card gets both.
        CardInstance? theirs = null;
        var e4 = Reveal(Benefices, out _, s => { Add(s, "training-item", 1, Zone.Hand); theirs = Add(s, "training-item", 0, Zone.Hand); NoFateDraw(s, 0); });
        AnswerEvent(e4, choice: "none");
        AnswerEvent(e4, target: theirs!.Id);
        Check(e4.State.Players[0].Provinces.Count == 5 && e4.State.Players[0].MaxHandBonus == 3, "he chose none; the other player took both prizes");
        // Nobody with a card: nothing.
        var e5 = Reveal(Benefices, out var evt5, s => NoFateDraw(s, 0));
        Check(e5.State.PendingChoice is null && e5.State.Cards[evt5.Id].Zone == Zone.DynastyDiscard, "two empty hands: nothing to choose: " + string.Join("; ", EventOptions(e5, e5.State.PriorityPlayerId)) + " hands " + e5.State.Players[0].Hand.Count + "/" + e5.State.Players[1].Hand.Count);
    }

    // ---- Abandoning the Fortunes ------------------------------------------------------------------------------------------------------------------------------

    private static void AbandoningTheFortunesTests()
    {
        MatchState Setup(int emptyProvinces, bool emptyDeck, out CardInstance evt, out CardInstance[] mine)
        {
            var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
            var original = s.Cards.Keys.ToHashSet();
            evt = Add(s, AbandoningTheFortunes, 1, Zone.EventInPlay);
            mine = [Add(s, "training-crane-samurai", 0, Zone.Home), Add(s, "training-marketplace", 0, Zone.Home), Add(s, "training-crane-samurai", 0, Zone.Home), Add(s, "training-marketplace", 0, Zone.Home)];
            Add(s, "training-lion-samurai", 1, Zone.Home);
            if (emptyDeck) { foreach (var id in s.Players[0].DynastyDeck.ToArray()) s.Cards.Remove(id); s.Players[0].DynastyDeck.Clear(); }
            for (var i = 0; i < emptyProvinces; i++)
            {
                var p = s.Players[0].Provinces[i];
                if (p.CardId is { } id) s.Cards.Remove(id);
                p.CardId = null;
            }
            foreach (var c in s.Cards.Values.Where(c => original.Contains(c.Id) && c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type is CardType.Personality or CardType.Holding).ToArray()) s.Cards.Remove(c.Id);
            return s;
        }
        void EndDynasty(MatchEngine e) => Accept(e, e.QueryActions(0).First(o => o.Type == "endDynasty").Command!);
        // Two provinces without a Dynasty card and an empty Dynasty deck: as his turn ends he chooses and destroys two Personalities or Holdings, one at a time.
        var s1 = Setup(2, true, out _, out var mine1);
        var e1 = MatchEngine.FromTestState(s1);
        EndDynasty(e1);
        Eq(e1.State.PendingChoice?.Type, "eventChoice", "he must choose before his turn ends");
        Eq(EventOptions(e1, 0).Length, 4, "one of his own Personalities or Holdings (not the other player's)");
        AnswerEvent(e1, target: mine1[0].Id);
        Eq(e1.State.Cards[mine1[0].Id].Zone, Zone.Dead, "the first was destroyed");
        Eq(e1.State.PendingChoice?.Type, "eventChoice", "and he chooses the second");
        Eq(EventOptions(e1, 0).Length, 3, "from what is left");
        AnswerEvent(e1, target: mine1[1].Id);
        Check(e1.State.Cards[mine1[1].Id].Zone is Zone.DynastyDiscard or Zone.Dead, "the Holding was destroyed too");
        Eq(e1.State.ActivePlayerId, 1, "then his turn ended");
        // The Dynasty deck is not empty: nothing. No province without a card: nothing.
        var e2 = MatchEngine.FromTestState(Setup(2, false, out _, out _));
        EndDynasty(e2);
        Check(e2.State.PendingChoice is null && e2.State.ActivePlayerId == 1, "a Dynasty deck with cards: his turn simply ends");
        var e3 = MatchEngine.FromTestState(Setup(0, true, out _, out _));
        EndDynasty(e3);
        Check(e3.State.PendingChoice is null && e3.State.ActivePlayerId == 1, "no province without a Dynasty card: nothing to destroy");
        // No more candidates than picks: everything he has is destroyed without a choice.
        var s4 = Setup(3, true, out _, out var mine4);
        foreach (var extra in mine4.Skip(2)) s4.Cards.Remove(extra.Id);
        var e4 = MatchEngine.FromTestState(s4);
        EndDynasty(e4);
        Check(e4.State.PendingChoice is null && e4.State.ActivePlayerId == 1, "with fewer candidates than picks nothing is asked");
        Check(e4.State.Cards[mine4[0].Id].Zone != Zone.Home && e4.State.Cards[mine4[1].Id].Zone != Zone.Home, "both were destroyed");
        // The other player's turn ending asks him: one Personality of his, one province empty.
        var s5 = Table(Phase.Dynasty, 1); s5.PriorityPlayerId = 1;
        var original5 = s5.Cards.Keys.ToHashSet();
        Add(s5, AbandoningTheFortunes, 0, Zone.EventInPlay);
        var doomed = Add(s5, "training-lion-samurai", 1, Zone.Home);
        foreach (var id in s5.Players[1].DynastyDeck.ToArray()) s5.Cards.Remove(id);
        s5.Players[1].DynastyDeck.Clear();
        var pv = s5.Players[1].Provinces[0]; s5.Cards.Remove(pv.CardId!); pv.CardId = null;
        foreach (var c in s5.Cards.Values.Where(c => original5.Contains(c.Id) && c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type is CardType.Personality or CardType.Holding).ToArray()) s5.Cards.Remove(c.Id);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, e5.QueryActions(1).First(o => o.Type == "endDynasty").Command!);
        Check(e5.State.PendingChoice is null && e5.State.Cards[doomed.Id].Zone != Zone.Home, "each player's turn: his own only Personality is destroyed at his turn's end");
    }
}
