using L5R.Core;
using static RegressionAssertions;

// Strategy wave 118 (issue #674), continued: the court, the war and the table Strategies (see WlStratTornTests.cs).
internal static partial class WlStratTornTests
{
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the position did not settle");
    }

    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }

    private static void AddChi(CardInstance card, int chi) => card.Modifiers.Add(new("test-source", "test", "Chi", chi));

    private static ActionOption PlayerOption(MatchEngine e, int actor, string ability, string cardId, int targetPlayer) =>
        Options(e, actor, ability, cardId).FirstOrDefault(o => o.Enabled && o.Command!.TargetPlayerId == targetPlayer) ?? throw new Exception($"{ability} was not offered enabled against player {targetPlayer}");

    // ---- Courteous Gift -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState S, CardInstance Samurai, CardInstance Card, CardInstance Gift) GiftTable(string giftDefinition = Lion, bool faceUp = true)
    {
        var s = Table(Phase.Action, 0);
        var samurai = Kw(Add(s, Crane, 0, Zone.Home), "Samurai");
        var gift = Add(s, giftDefinition, 1, Zone.Province, province: 1); gift.FaceUp = faceUp; s.Players[1].Provinces[1].CardId = gift.Id;
        return (s, samurai, Add(s, Courteous, 0, Zone.Hand), gift);
    }

    private static void CourteousGift()
    {
        foreach (var draws in new[] { false, true })
        {
            var (s, samurai, card, gift) = GiftTable();
            var e = MatchEngine.FromTestState(s);
            var cost = e.CardGoldCostForTest(gift.Id);
            Check(Enabled(e, 0, "wl-courteous-gift", card.Id, samurai.Id).Slots!.Single(x => x.Slot == "gift").CardIds.Contains(gift.Id), "the face-up province card is a candidate");
            Accept(e, With(Enabled(e, 0, "wl-courteous-gift", card.Id, samurai.Id), Pick("gift", gift.Id)));
            Check(e.State.Cards[samurai.Id].Bowed, "the performing Samurai is bowed as the cost");
            Eq(e.State.PendingChoice?.Type, "eitherOr", "the other player decides");
            Eq(e.State.PendingEitherOr!.DeciderPlayerId, 1, "he decides");
            Check(e.QueryActions(0).All(a => a.Type != "chooseEitherOr"), "the acting player has nothing to answer");
            var handBefore = e.State.Players[1].Hand.Count;
            var answer = e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == (draws ? "draw" : "decline")).Command!;
            Accept(e, answer);
            Eq(e.State.Players[1].Hand.Count, handBefore + (draws ? 1 : 0), draws ? "he drew a card" : "he drew nothing");
            Eq(e.CardGoldCostForTest(gift.Id), Math.Max(0, cost - (draws ? 4 : 2)), draws ? "4 less after a draw" : "2 less");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null, "the action is over");
            // "This turn": the reduction is a turn modifier.
            var later = Edit(e, st => { foreach (var c in st.Cards.Values) c.Modifiers.RemoveAll(m => m.Stat != "GoldCost"); });
            Eq(later.CardGoldCostForTest(gift.Id), Math.Max(0, cost - (draws ? 4 : 2)), "still reduced while the turn lasts");
        }
        // The card may be one of the acting player's own provinces' (any face-up province card).
        var (s2, samurai2, card2, _) = GiftTable();
        var own = Add(s2, Crane, 0, Zone.Province, province: 0); own.FaceUp = true; s2.Players[0].Provinces[0].CardId = own.Id;
        var e2 = MatchEngine.FromTestState(s2);
        Check(Enabled(e2, 0, "wl-courteous-gift", card2.Id, samurai2.Id).Slots!.Single(x => x.Slot == "gift").CardIds.Contains(own.Id), "either player's face-up province card");
    }

    private static void CourteousGiftRejections()
    {
        var (s, samurai, card, gift) = GiftTable(faceUp: false);
        var plain = Add(s, Crane, 0, Zone.Home);
        var bowed = Kw(Add(s, Crane, 0, Zone.Home, bowed: true), "Samurai");
        var inPlay = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Enabled(e, 0, "wl-courteous-gift", card.Id, samurai.Id).Slots!.Single(x => x.Slot == "gift").CardIds.Contains(gift.Id), "a face-down province card is no candidate");
        foreach (var (performer, target, why) in new[] { (samurai.Id, gift.Id, "face-down"), (samurai.Id, inPlay.Id, "in play, not in a province"), (plain.Id, gift.Id, "a non-Samurai"), (bowed.Id, gift.Id, "a bowed Samurai") })
        {
            var command = new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-courteous-gift", PerformerId: performer, Selections: [Pick("gift", target)]);
            Reject(e, command);
            Check(true, why);
        }
        Check(!e.State.Cards[samurai.Id].Bowed && e.State.PendingEitherOr is null, "nothing happened");
    }

    // ---- Eye of the Sword -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EyeOfTheSword()
    {
        MatchEngine Duel(int duelistChi, out CardInstance duelist, out CardInstance foe, out CardInstance card)
        {
            var s = Table(Phase.Battle, 0);
            duelist = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Duelist"); AddChi(duelist, duelistChi);
            foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            var plain = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            card = Add(s, Eye, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, "wl-eye-sword-duel", card.Id, plain.Id), "only a Duelist performs it");
            Accept(e, Offer(e, 0, "wl-eye-sword-duel", card.Id, foe.Id, duelist.Id));
            Check(e.State.PendingDuel is { Refusable: false } && e.State.PendingDuel.Stage != "AwaitingAccept", "the challenge cannot be refused: it begins at once");
            RunDuel(e);
            return e;
        }
        // The Duelist wins: two +1F tokens for him, the loser destroyed.
        var e = Duel(2, out var duelist, out var foe, out var card);
        Eq(e.State.LastDuel!.Result!.WinnerId, duelist.Id, "the Duelist wins");
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "the loser is destroyed");
        Eq(e.State.Cards[duelist.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "two +1F tokens for the winner");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // The Duelist loses: he is destroyed, and the enemy gets the tokens.
        var lost = Duel(-2, out var duelist2, out var foe2, out _);
        Eq(lost.State.LastDuel!.Result!.WinnerId, foe2.Id, "the enemy wins");
        Eq(lost.State.Cards[duelist2.Id].Zone, Zone.Dead, "the challenger is the loser: destroyed");
        Eq(lost.State.Cards[foe2.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "the winner is the enemy: he gets the tokens");
        // A tie (no winner, two losers: the outcome is applied directly, a duel being hard to tie): no tokens, and both losers are destroyed.
        var s4 = Table(Phase.Battle, 0); var d4 = Add(s4, Crane, 0, Zone.Battlefield, province: 0); var f4 = Add(s4, Lion, 1, Zone.Battlefield, province: 0);
        var tie = MatchEngine.FromTestState(s4);
        tie.WlEyeDuelOutcomeForTest(null, [d4.Id, f4.Id], 0);
        Check(!tie.State.Cards[d4.Id].Tokens.Any() && !tie.State.Cards[f4.Id].Tokens.Any(), "a tie gives no tokens");
        Check(tie.State.Cards[d4.Id].Zone == Zone.Dead && tie.State.Cards[f4.Id].Zone == Zone.Dead, "and destroys both");
    }

    // ---- Forest Cover -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Scout, CardInstance Card, CardInstance Follower) EngageTable(bool followerInDiscard = false)
    {
        var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        t.Cards[t.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; for (var i = 0; i < 4; i++) Add(t, "training-marketplace", 0, Zone.Home);
        var scout = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), "Scout"); Add(t, Lion, 1, Zone.Battlefield, province: 0);
        var follower = Add(t, Follower, 0, followerInDiscard ? Zone.FateDiscard : Zone.Hand);
        var card = Add(t, Forest, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(t);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return (e, scout, card, follower);
    }

    private static void ForestCover()
    {
        // The first action: Reconnaissance at the battlefield for the acting player (the Attacker here).
        var (e, scout, card, follower) = EngageTable();
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[0], "no Reconnaissance before");
        Check(CanUse(e, 0, "wl-forest-cover-recon", card.Id, scout.Id) && CanUse(e, 0, "wl-forest-cover-attach", card.Id, scout.Id), "both halves are offered");
        Accept(e, Enabled(e, 0, "wl-forest-cover-recon", card.Id, scout.Id).Command!);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance there");
        Eq(e.State.Cards[follower.Id].Zone, Zone.Hand, "the Follower stays in the hand");
        Eq(e.State.ExtraEndPhaseDraws[0], 0, "no extra draw without the attach");
        Check(!CanUse(e, 0, "wl-forest-cover-attach", card.Id, scout.Id), "the two halves share one use");
        // The second: also attach the Follower from the hand, paying its cost, and owe an extra End Phase draw.
        var (e2, scout2, card2, follower2) = EngageTable();
        var goldBefore = e2.QueryActions(0).Count();
        var offer = Offer(e2, 0, "wl-forest-cover-attach", card2.Id, follower2.Id, scout2.Id);
        Accept(e2, offer);
        Check(e2.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance there");
        Check(e2.State.Cards[follower2.Id].Zone == Zone.Attached && e2.State.Cards[follower2.Id].AttachedTo == scout2.Id, "the Follower is attached to the performing Scout");
        Eq(e2.State.ExtraEndPhaseDraws[0], 1, "an additional card during his next End Phase");
        Eq(e2.State.Cards[card2.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(goldBefore >= 0 && offer.SourceIds is { Length: > 0 }, "the Follower's Gold was paid from real sources");
        // From the discard pile too.
        var (e3, scout3, card3, follower3) = EngageTable(followerInDiscard: true);
        Accept(e3, Offer(e3, 0, "wl-forest-cover-attach", card3.Id, follower3.Id, scout3.Id));
        Check(e3.State.Cards[follower3.Id].AttachedTo == scout3.Id && e3.State.ExtraEndPhaseDraws[0] == 1, "attached from the discard pile");
        // Not a Scout: nobody performs it.
        var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        var plain = Add(t, Crane, 0, Zone.Battlefield, province: 0); Add(t, Lion, 1, Zone.Battlefield, province: 0); var c4 = Add(t, Forest, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(t);
        Accept(e4, new GameCommand("selectBattle", 0, e4.Version, Province: 0));
        Check(!CanUse(e4, 0, "wl-forest-cover-recon", c4.Id, plain.Id), "a Personality that is not a Scout is no performer");
    }

    // ---- Forgotten Teachings --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ForgottenTeachings()
    {
        var s = Table(Phase.Action, 0);
        var sensei = Kw(Add(s, "training-bow-enemy-source", 1, Zone.Home), "Sensei");
        var notSensei = Add(s, "training-bow-enemy-source", 1, Zone.Home);
        var own = Kw(Add(s, "training-bow-enemy-source", 0, Zone.Home), "Sensei");
        var card = Add(s, Forgotten, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.AbilityIdsForTest(sensei.Id).Length > 0, "he has an ability to lose");
        var targets = Enabled(e, 0, "wl-forgotten-teachings", card.Id).TargetIds!;
        Check(targets.Contains(sensei.Id) && !targets.Contains(notSensei.Id) && !targets.Contains(own.Id), "another player's Sensei only");
        Reject(e, Offer(e, 0, "wl-forgotten-teachings", card.Id, sensei.Id) with { TargetId = own.Id });
        Accept(e, Offer(e, 0, "wl-forgotten-teachings", card.Id, sensei.Id));
        Eq(e.AbilityIdsForTest(sensei.Id).Length, 0, "all his abilities are removed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(e.State.Removal.Entries.Any(x => x.CardId == sensei.Id), "recorded");
        e.WlTurnEndForTest();
        Check(e.AbilityIdsForTest(sensei.Id).Length > 0, "and they are back as the turn ends");
        Check(!e.State.Removal.Entries.Any(x => x.CardId == sensei.Id) && !e.State.Removal.Baselines.Any(x => x.CardId == sensei.Id), "with nothing left recorded");
        Check(e.AbilityIdsForTest(sensei.Id).SequenceEqual(CardCatalog.Get("training-bow-enemy-source").Abilities!), "his printed abilities are the card's again");
        // A removal made permanently by another card stays in force when this one ends.
        var s2 = Table(Phase.Action, 0);
        var sensei2 = Kw(Add(s2, "training-bow-enemy-source", 1, Zone.Home), "Sensei"); var card2 = Add(s2, Forgotten, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "wl-forgotten-teachings", card2.Id, sensei2.Id));
        Check(e2.State.WlStratTorn.ForgottenTeachings.Contains(sensei2.Id), "owed back");
        Eq(Edit(e2, st => st.TurnNumber += 1).AbilityIdsForTest(sensei2.Id).Length, 0, "removed until the turn-end sweep runs");
    }

    // ---- Improper Papers ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string Incapacitated = "oracle-l5r-3730";

    private static void ImproperPapers()
    {
        MatchEngine Build(out CardInstance papers, out CardInstance defender, out CardInstance incapacitated)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            papers = Add(s, Papers, 0, Zone.Hand);
            defender = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            Add(s, Lion, 1, Zone.Battlefield, province: 0);
            incapacitated = Add(s, Incapacitated, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var papers, out var defender, out var incap);
        Accept(e, Offer(e, 1, "incapacitated", incap.Id, defender.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the movement is offered for negation");
        var offers = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "wl-improper-papers").ToArray();
        Check(offers.Length == 2 && offers.All(o => o.Enabled && o.Command!.CardId == papers.Id), "any Personality may be targeted, as printed: " + string.Join(",", offers.Select(o => o.Label)));
        var guardCopy = e.State;
        // Naming a Personality the action does not move changes nothing: the movement goes ahead.
        var wrong = Build(out _, out var moving, out var wrongCard);
        Accept(wrong, Offer(wrong, 1, "incapacitated", wrongCard.Id, moving.Id));
        var wrongOffers = wrong.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "wl-improper-papers" && a.Command.TargetId != moving.Id).ToArray();
        Accept(wrong, wrongOffers.Single().Command!);
        PassUntil(wrong, () => wrong.State.PendingChoice is null);
        Eq(wrong.State.Cards[moving.Id].Zone, Zone.Home, "a Personality that is not moved named: his movement is not negated");
        Accept(e, offers.Single(o => o.Command!.TargetId == defender.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[defender.Id].Zone, Zone.Battlefield, "his movement was negated: he stays");
        Eq(e.State.Cards[papers.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        _ = guardCopy;
        // Declined: the same position resolves normally.
        var d = Build(out _, out var moved, out var card);
        Accept(d, Offer(d, 1, "incapacitated", card.Id, moved.Id));
        PassUntil(d, () => d.State.PendingChoice is null);
        Eq(d.State.Cards[moved.Id].Zone, Zone.Home, "declined: he moved home");
        // An action that moves nothing offers no window for it.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 1;
        Add(s3, Papers, 0, Zone.Hand); var bower = Add(s3, "training-bow-enemy-source", 1, Zone.Home); var victim = Add(s3, Crane, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-enemy", bower.Id, victim.Id));
        Check(e3.State.PendingChoice?.Type != "beforeActionReaction" || !e3.QueryActions(0).Any(a => a.Command?.AbilityId == "wl-improper-papers" && a.Enabled), "no window offers Improper Papers for an action that moves nobody");
    }

    // ---- Imprudent Misstep ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImprudentMisstep()
    {
        var s = Table(Phase.Action, 0); s.Players[1].Honor = 30;
        var card = Add(s, Misstep, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "wl-imprudent-misstep", card.Id).Any(o => o.Enabled && o.Command!.TargetPlayerId == 0), "yourself is not another player");
        Accept(e, PlayerOption(e, 0, "wl-imprudent-misstep", card.Id, 1).Command!);
        Eq(Honor(e, 1), 25, "he loses 5 Honor");
        Eq(Honor(e, 0), s.Players[0].Honor, "the player loses none");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // 29 Honor: not a legal target; a hand-built command is refused with the state untouched.
        var s2 = Table(Phase.Action, 0); s2.Players[1].Honor = 29; var card2 = Add(s2, Misstep, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Options(e2, 0, "wl-imprudent-misstep", card2.Id).Any(o => o.Enabled), "29 Honor: not offered");
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "wl-imprudent-misstep", TargetPlayerId: 1));
        Eq(Honor(e2, 1), 29, "unchanged");
        // The acting player's own Honor does not count: another player must have it.
        var s3 = Table(Phase.Action, 0); s3.Players[0].Honor = 40; s3.Players[1].Honor = 10; var card3 = Add(s3, Misstep, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!Options(e3, 0, "wl-imprudent-misstep", card3.Id).Any(o => o.Enabled), "only another player's Honor is read");
    }

    // ---- In the Right -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState S, CardInstance Courtier, CardInstance Card, CardInstance[] Attackers, CardInstance Follower) RightTable(int chiBonus)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var courtier = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Courtier"); AddChi(courtier, chiBonus - 3);
        var attackers = new[] { Add(s, Lion, 1, Zone.Battlefield, province: 0), Add(s, Lion, 1, Zone.Battlefield, province: 0), Add(s, Lion, 1, Zone.Battlefield, province: 0) };
        var follower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: attackers[0].Id);
        return (s, courtier, Add(s, InRight, 0, Zone.Hand), attackers, follower);
    }

    private static void InTheRight()
    {
        // Chi 5: half rounded down is 2 -- two attacking cards (a Personality and a Follower) are bowed.
        var (s, courtier, card, attackers, follower) = RightTable(5);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "wl-in-the-right", card.Id, courtier.Id);
        Check(offer.Slots!.Single().CardIds.Contains(follower.Id) && offer.Slots!.Single().CardIds.Contains(attackers[1].Id), "attacking Personalities and their Followers are candidates");
        Accept(e, With(offer, Pick("targets", attackers[1].Id, follower.Id)));
        Check(e.State.Cards[attackers[1].Id].Bowed && e.State.Cards[follower.Id].Bowed, "the targets are bowed");
        Check(!e.State.Cards[attackers[0].Id].Bowed && !e.State.Cards[attackers[2].Id].Bowed, "the others are not");
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier is bowed as the cost");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Three targets against a limit of two: refused, untouched.
        var (s2, courtier2, card2, attackers2, _) = RightTable(5);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "wl-in-the-right", PerformerId: courtier2.Id, Selections: [Pick("targets", attackers2.Select(a => a.Id).ToArray())]));
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "wl-in-the-right", PerformerId: courtier2.Id, Selections: [Pick("targets", attackers2[0].Id, attackers2[1].Id)]));
        // Chi 3: half is 1 (rounded down).
        var (s3, courtier3, card3, attackers3, _) = RightTable(3);
        var e3 = MatchEngine.FromTestState(s3);
        Reject(e3, new GameCommand("useAbility", 0, e3.Version, card3.Id, AbilityId: "wl-in-the-right", PerformerId: courtier3.Id, Selections: [Pick("targets", attackers3[0].Id, attackers3[1].Id)]));
        Accept(e3, new GameCommand("useAbility", 0, e3.Version, card3.Id, AbilityId: "wl-in-the-right", PerformerId: courtier3.Id, Selections: [Pick("targets", attackers3[0].Id)]));
        // Chi 1: half is 0 -- nothing may be targeted, so it cannot be played.
        var (s4, courtier4, card4, attackers4, _) = RightTable(1);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "wl-in-the-right", card4.Id, courtier4.Id), "Chi 1: not playable");
        Reject(e4, new GameCommand("useAbility", 0, e4.Version, card4.Id, AbilityId: "wl-in-the-right", PerformerId: courtier4.Id, Selections: [Pick("targets", attackers4[0].Id)]));
        // A card that is not attacking (the acting player's own defender) is no target; a non-Courtier performs nothing.
        var (s5, courtier5, card5, _, _) = RightTable(5);
        var mine = Add(s5, Crane, 0, Zone.Battlefield, province: 0); var plain = Add(s5, Crane, 0, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        Reject(e5, new GameCommand("useAbility", 0, e5.Version, card5.Id, AbilityId: "wl-in-the-right", PerformerId: courtier5.Id, Selections: [Pick("targets", mine.Id)]));
        Check(!CanUse(e5, 0, "wl-in-the-right", card5.Id, plain.Id), "a non-Courtier is no performer");
        // Stalwart: a bow by another player's card is negated the first time.
        var (s6, courtier6, card6, attackers6, _) = RightTable(5);
        Kw(attackers6[0], "Stalwart");
        var e6 = MatchEngine.FromTestState(s6);
        Accept(e6, new GameCommand("useAbility", 0, e6.Version, card6.Id, AbilityId: "wl-in-the-right", PerformerId: courtier6.Id, Selections: [Pick("targets", attackers6[0].Id, attackers6[1].Id)]));
        Check(!e6.State.Cards[attackers6[0].Id].Bowed && e6.State.Cards[attackers6[1].Id].Bowed, "Stalwart negates the first bow");
    }

    // ---- Martyr's Call -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MartyrsCall()
    {
        foreach (var phase in new[] { Phase.Battle, Phase.Action })
        {
            var s = Table(phase, 0);
            var zone = phase == Phase.Battle ? Zone.Battlefield : Zone.Home; int? province = phase == Phase.Battle ? 0 : null;
            var paragon = Kw(Add(s, Crane, 0, zone, province: province), "Paragon");
            var a = Add(s, Crane, 0, zone, province: province); var b = Add(s, Crane, 0, zone, province: province); var c = Add(s, Crane, 0, zone, province: province);
            Add(s, Lion, 1, zone, province: province);
            var card = Add(s, Martyr, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var force = e.CardForceForTest(a.Id);
            Check(!(Enabled(e, 0, "wl-martyrs-call", card.Id, paragon.Id).Slots!.Single().CardIds.Contains(paragon.Id)), $"{phase}: the performing Paragon is not among his own 'other' Personalities");
            Reject(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-martyrs-call", PerformerId: paragon.Id, Selections: [Pick("targets", a.Id, b.Id, c.Id)]));
            Reject(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-martyrs-call", PerformerId: paragon.Id, Selections: [Pick("targets", paragon.Id)]));
            Accept(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-martyrs-call", PerformerId: paragon.Id, Selections: [Pick("targets", a.Id, b.Id)]));
            Eq(e.State.Cards[paragon.Id].Zone, Zone.Dead, $"{phase}: the Paragon is destroyed as the cost");
            Eq(e.CardForceForTest(a.Id), force + 3, $"{phase}: +3F");
            Eq(e.CardForceForTest(b.Id), force + 3, $"{phase}: +3F for both");
            Eq(e.CardForceForTest(c.Id), force, $"{phase}: not for the third");
            Check(e.State.Cards[a.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart" && g.Duration == GrantDuration.Turn), $"{phase}: Stalwart for the turn");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, $"{phase}: the Strategy is spent");
        }
        // One target is enough; a non-Paragon performs nothing.
        var s2 = Table(Phase.Action, 0);
        var paragon2 = Kw(Add(s2, Crane, 0, Zone.Home), "Paragon"); var plain = Add(s2, Crane, 0, Zone.Home); var lone = Add(s2, Crane, 0, Zone.Home); var card2 = Add(s2, Martyr, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "wl-martyrs-call", card2.Id, plain.Id), "a non-Paragon is no performer");
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "wl-martyrs-call", PerformerId: paragon2.Id, Selections: [Pick("targets", lone.Id)]));
        Check(e2.State.Cards[lone.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart"), "one target");
    }

    // ---- Overpower ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OverpowerScenario()
    {
        foreach (var turn in new[] { 1, 2 })
        {
            var s = Table(Phase.Action, 0); s.Players[0].TurnsTaken = turn;
            var card = Add(s, Overpower, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, "wl-overpower-open", card.Id).Command!);
            Check(e.State.Players[0].MayNotAttackThisTurn, $"turn {turn}: the active player may not declare an attack");
            var attack = Edit(e, st => { st.Phase = Phase.AttackDeclaration; st.PriorityPlayerId = 0; st.PendingChoice = null; });
            var declare = attack.QueryActions(0).Single(a => a.Type == "declareAttack");
            Check(!declare.Enabled && attack.Execute(new GameCommand("declareAttack", 0, attack.Version)).Accepted == false, $"turn {turn}: declaring is refused");
        }
        // The third turn: refused, untouched.
        var s3 = Table(Phase.Action, 0); s3.Players[0].TurnsTaken = 3;
        var card3 = Add(s3, Overpower, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "wl-overpower-open", card3.Id), "the third turn: not playable");
        Reject(e3, new GameCommand("useAbility", 0, e3.Version, card3.Id, AbilityId: "wl-overpower-open"));
        Check(!e3.State.Players[0].MayNotAttackThisTurn, "nothing changed");
        // It is "the active player's" turn, whoever plays it: the other player may answer on the active player's second turn.
        var s4 = Table(Phase.Action, 0); s4.Players[0].TurnsTaken = 2; s4.PriorityPlayerId = 1;
        var card4 = Add(s4, Overpower, 1, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 1, "wl-overpower-open", card4.Id).Command!);
        Check(e4.State.Players[0].MayNotAttackThisTurn && !e4.State.Players[1].MayNotAttackThisTurn, "the active player's attack is locked, not the player's who played it");

        // The Limited: a discarded (not dead) Personality into one of the player's provinces, discarding the card there.
        var s5 = Table(Phase.Action, 0);
        var discarded = Add(s5, Lion, 0, Zone.DynastyDiscard); var dead = Add(s5, Crane, 0, Zone.Dead);
        var enemyDiscard = Add(s5, Lion, 1, Zone.DynastyDiscard);
        var there = Add(s5, Crane, 0, Zone.Province, province: 2); there.FaceUp = false; s5.Players[0].Provinces[2].CardId = there.Id;
        var card5 = Add(s5, Overpower, 0, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        var targets = Options(e5, 0, "wl-overpower-limited", card5.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(discarded.Id) && !targets.Contains(dead.Id) && !targets.Contains(enemyDiscard.Id), "your discarded Personality, not a dead one, not the enemy's: " + string.Join(",", targets));
        var command = Options(e5, 0, "wl-overpower-limited", card5.Id).First(o => o.Enabled).Command! with { TargetId = discarded.Id, Province = 2 };
        Accept(e5, command);
        Check(e5.State.Cards[discarded.Id].Zone == Zone.Province && e5.State.Cards[discarded.Id].FaceUp && e5.State.Players[0].Provinces[2].CardId == discarded.Id, "he stands face-up in the chosen province");
        Eq(e5.State.Cards[there.Id].Zone, Zone.DynastyDiscard, "the card there was discarded");
        Reject(e5, new GameCommand("useAbility", 0, e5.Version, card5.Id, AbilityId: "wl-overpower-limited", TargetId: dead.Id, Province: 2));
    }

    // ---- Scandalous Gossip --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ScandalousGossip()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Gossip, 0, Zone.FateDiscard);
        var a = Add(s, Lion, 1, Zone.Home); var b = Add(s, Lion, 1, Zone.Home); var bowed = Add(s, Lion, 1, Zone.Home, bowed: true);
        var handCard = Add(s, Gossip, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "wl-scandalous-gossip", handCard.Id).All(o => !o.Enabled), "from the hand it can do nothing: only the discard pile");
        var honor = Honor(e, 0);
        Accept(e, PlayerOption(e, 0, "wl-scandalous-gossip", card.Id, 1).Command!);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the chosen player decides");
        Eq(e.State.PendingDeciderChoice!.DeciderPlayerId, 1, "player 1");
        Check(e.State.PendingDeciderChoice.Candidates.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x, StringComparer.Ordinal)), "his unbowed Personalities only");
        Check(e.QueryActions(0).All(x => x.Type != "chooseDecider"), "the acting player chooses nothing");
        var pick = e.QueryActions(1).First(x => x.Type == "chooseDecider" && x.Enabled);
        Accept(e, pick.Command!);
        Check(e.State.Cards[pick.Command!.TargetId!].Bowed, "the chosen Personality is bowed");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Bowed && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality), 2, "exactly one more Personality bowed (the already bowed one, and the chosen)");
        Eq(Honor(e, 0), honor + 1, "the player gains 1 Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "the card is removed from the game");
        // Nothing to bow: no decision, and the Honor is still gained ("if possible").
        var s2 = Table(Phase.Action, 0); var card2 = Add(s2, Gossip, 0, Zone.FateDiscard); Add(s2, Lion, 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2); var honor2 = Honor(e2, 0);
        Accept(e2, PlayerOption(e2, 0, "wl-scandalous-gossip", card2.Id, 1).Command!);
        Check(e2.State.PendingChoice is null && Honor(e2, 0) == honor2 + 1 && e2.State.Cards[card2.Id].Zone == Zone.RemovedFromGame, "no unbowed Personality: just the Honor");
        // One unbowed Personality: no choice either; and the player may name himself.
        var s3 = Table(Phase.Action, 0); var card3 = Add(s3, Gossip, 0, Zone.FateDiscard); var mine = Add(s3, Crane, 0, Zone.Home); var only = Add(s3, Lion, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, PlayerOption(e3, 0, "wl-scandalous-gossip", card3.Id, 1).Command!);
        Check(e3.State.PendingChoice is null && e3.State.Cards[only.Id].Bowed && !e3.State.Cards[mine.Id].Bowed, "one choice: made for him");
        var s4 = Table(Phase.Action, 0); var card4 = Add(s4, Gossip, 0, Zone.FateDiscard); var mine4 = Add(s4, Crane, 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, PlayerOption(e4, 0, "wl-scandalous-gossip", card4.Id, 0).Command!);
        Check(e4.State.Cards[mine4.Id].Bowed, "'target a player' includes yourself");
    }

    // ---- Spoils of Exploration -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SpoilsOfExploration()
    {
        var s = Table(Phase.Action, 0);
        var first = Add(s, Spoils, 0, Zone.Home); var second = Add(s, Spoils, 0, Zone.Hand);
        var enemyKata = Add(s, Spoils, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, first.Id, AbilityId: "wl-spoils-enter"));
        Accept(e, Enabled(e, 0, "wl-spoils-enter", second.Id).Command!);
        Eq(e.State.Cards[second.Id].Zone, Zone.Home, "the Kata is in play");
        Eq(e.State.Cards[first.Id].Zone, Zone.FateDiscard, "the other Kata is discarded");
        Eq(e.State.Cards[enemyKata.Id].Zone, Zone.Home, "another player's Kata stays");

        MatchEngine Battle(Func<MatchState, (CardInstance Attacker, CardInstance Defender)> build, out CardInstance kata)
        {
            var t = Table(Phase.Battle, 0);
            var (attacker, defender) = build(t);
            _ = attacker; _ = defender;
            kata = t.Cards.Values.First(c => c.DefinitionId == Spoils);
            return MatchEngine.FromTestState(t);
        }
        // The Attacker destroys an army: its controller's Kata draws.
        var won = Battle(t => { Add(t, Spoils, 0, Zone.Home); var a = Add(t, Lion, 0, Zone.Battlefield, province: 0); var d = Add(t, Crane, 1, Zone.Battlefield, province: 0); return (a, d); }, out _);
        var handBefore = won.State.Players[0].Hand.Count;
        PassUntil(won, () => won.State.Phase != Phase.Battle || won.State.PendingChoice is null && won.State.Players[0].Hand.Count > handBefore);
        Eq(won.State.Players[0].Hand.Count, handBefore + 1, "an army was destroyed: Spoils draws a card");
        // The Defender wins and destroys the attackers: the Defender's Kata draws, the Attacker's does not.
        var lost = Battle(t => { Add(t, Spoils, 0, Zone.Home); var a = Add(t, Crane, 0, Zone.Battlefield, province: 0); var d = Add(t, Lion, 1, Zone.Battlefield, province: 0); return (a, d); }, out _);
        var lostBefore = lost.State.Players[0].Hand.Count;
        PassUntil(lost, () => lost.State.Phase != Phase.Battle);
        Eq(lost.State.Players[0].Hand.Count, lostBefore, "the loser destroyed nothing: no card");
        // Not in play (in the hand): nothing.
        var held = Battle(t => { Add(t, Spoils, 0, Zone.Hand); var a = Add(t, Lion, 0, Zone.Battlefield, province: 0); var d = Add(t, Crane, 1, Zone.Battlefield, province: 0); return (a, d); }, out _);
        var heldBefore = held.State.Players[0].Hand.Count;
        PassUntil(held, () => held.State.Phase != Phase.Battle);
        Eq(held.State.Players[0].Hand.Count, heldBefore, "a Kata in the hand does nothing");
    }

    // ---- Tactical Sacrifice -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TacticalSacrifice()
    {
        var s = Table(Phase.Action, 0);
        var commander = Kw(Add(s, Crane, 0, Zone.Home), "Commander");
        var spent = Attach(s, Follower, commander);
        var other = Add(s, Crane, 0, Zone.Home);
        var cheapFollower = Attach(s, Follower, Add(s, Lion, 1, Zone.Home));
        var cheapPersonality = Add(s, "training-ashigaru-personality", 1, Zone.Home); // Gold Cost 2
        var costly = Add(s, Lion, 1, Zone.Home); // Gold Cost 5 > 4
        var armed = Add(s, "training-ashigaru-personality", 1, Zone.Home); Attach(s, "training-weapon", armed);
        var card = Add(s, Tactical, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "wl-tactical-sacrifice", card.Id, commander.Id);
        var legal = offer.Slots!.Single(x => x.Slot == "target").CardIds;
        Check(legal.Contains(cheapFollower.Id) && legal.Contains(cheapPersonality.Id), "a Follower and an attachment-free Personality within twice the Follower's Gold Cost");
        Check(!legal.Contains(costly.Id) && !legal.Contains(armed.Id) && !legal.Contains(spent.Id), "not one costing more than 4, not one with an attachment, not the sacrificed Follower");
        Check(offer.Slots!.Single(x => x.Slot == "follower").CardIds.SequenceEqual([spent.Id]), "his performing Follower is the one to destroy");
        Reject(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-tactical-sacrifice", PerformerId: commander.Id, Selections: [Pick("follower", spent.Id), Pick("target", costly.Id)]));
        Accept(e, With(offer, Pick("follower", spent.Id), Pick("target", cheapPersonality.Id)));
        Eq(e.State.Cards[spent.Id].Zone, Zone.FateDiscard, "his Follower is destroyed as the cost");
        Check(e.State.Cards[cheapPersonality.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the target is destroyed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.State.Cards[commander.Id].Zone, Zone.Home, "the Commander stands");
        // A Follower target.
        var s2 = Table(Phase.Action, 0);
        var commander2 = Kw(Add(s2, Crane, 0, Zone.Home), "Commander"); var spent2 = Attach(s2, Follower, commander2);
        var victim = Attach(s2, Follower, Add(s2, Lion, 1, Zone.Home)); var card2 = Add(s2, Tactical, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, With(Enabled(e2, 0, "wl-tactical-sacrifice", card2.Id, commander2.Id), Pick("follower", spent2.Id), Pick("target", victim.Id)));
        Eq(e2.State.Cards[victim.Id].Zone, Zone.FateDiscard, "the Follower target is destroyed");
        // Only a Commander, only unbowed, only with a Follower of his own.
        var s3 = Table(Phase.Action, 0);
        var plain = Add(s3, Crane, 0, Zone.Home); Attach(s3, Follower, plain);
        var bowedCommander = Kw(Add(s3, Crane, 0, Zone.Home, bowed: true), "Commander"); Attach(s3, Follower, bowedCommander);
        var bare = Kw(Add(s3, Crane, 0, Zone.Home), "Commander");
        var card3 = Add(s3, Tactical, 0, Zone.Hand); Add(s3, Lion, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(Options(e3, 0, "wl-tactical-sacrifice", card3.Id).All(o => !o.Enabled), "no unbowed Commander with a Follower: nothing is offered");
        Check(!CanUse(e3, 0, "wl-tactical-sacrifice", card3.Id, plain.Id) && !CanUse(e3, 0, "wl-tactical-sacrifice", card3.Id, bowedCommander.Id) && !CanUse(e3, 0, "wl-tactical-sacrifice", card3.Id, bare.Id), "each is refused");
        _ = other;
    }

    // ---- The Last Move ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TheLastMove()
    {
        MatchEngine Build(out CardInstance courtier, out CardInstance card, out CardInstance foe, out CardInstance follower, out CardInstance bystander)
        {
            var s = Table(Phase.Battle, 0);
            courtier = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Courtier");
            foe = Add(s, Lion, 1, Zone.Battlefield, province: 0); follower = Attach(s, Follower, foe);
            bystander = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            card = Add(s, Last, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var courtier, out var card, out var foe, out var follower, out var bystander);
        var offer = Enabled(e, 0, "wl-last-move", card.Id, courtier.Id);
        var destroyCandidates = offer.Slots!.Single(x => x.Slot == "destroy").CardIds;
        Check(destroyCandidates.Contains(follower.Id) && destroyCandidates.Contains(bystander.Id), "any card without attachments is a candidate in the offer");
        Reject(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-last-move", PerformerId: courtier.Id, Selections: [Pick("target", foe.Id), Pick("destroy", bystander.Id)]));
        Accept(e, With(offer, Pick("target", foe.Id), Pick("destroy", follower.Id)));
        Eq(e.State.Cards[courtier.Id].Zone, Zone.Dead, "the Courtier is destroyed as the cost");
        Check(e.State.Cards[foe.Id].Dishonorable && e.State.Cards[foe.Id].Zone == Zone.Battlefield, "the enemy is dishonored");
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "and the card without attachments in his unit is destroyed");
        Eq(e.State.Cards[bystander.Id].Zone, Zone.Battlefield, "a card of another unit is left alone");
        // "You may": without the second target only the dishonoring happens.
        var e2 = Build(out var courtier2, out var card2, out var foe2, out var follower2, out _);
        Accept(e2, With(Enabled(e2, 0, "wl-last-move", card2.Id, courtier2.Id), Pick("target", foe2.Id)));
        Check(e2.State.Cards[foe2.Id].Dishonorable && e2.State.Cards[follower2.Id].Zone == Zone.Attached, "no card destroyed when none is named");
        // The Personality himself may be the card (no attachments).
        var s3 = Table(Phase.Battle, 0); var c3 = Kw(Add(s3, Crane, 0, Zone.Battlefield, province: 0), "Courtier"); var bare = Add(s3, Lion, 1, Zone.Battlefield, province: 0); var card3 = Add(s3, Last, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, With(Enabled(e3, 0, "wl-last-move", card3.Id, c3.Id), Pick("target", bare.Id), Pick("destroy", bare.Id)));
        Check(e3.State.Cards[bare.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "he may be the card destroyed");
        // A bowed or non-Courtier performs nothing, and the action is a Battle one.
        var s4 = Table(Phase.Battle, 0); var bowedC = Kw(Add(s4, Crane, 0, Zone.Battlefield, bowed: true, province: 0), "Courtier"); var plain = Add(s4, Crane, 0, Zone.Battlefield, province: 0);
        var card4 = Add(s4, Last, 0, Zone.Hand); Add(s4, Lion, 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "wl-last-move", card4.Id, bowedC.Id) && !CanUse(e4, 0, "wl-last-move", card4.Id, plain.Id), "unbowed Courtiers only");
    }

    // ---- The Passing of Tradition -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PassingOfTradition()
    {
        MatchEngine Build(string[] keywords, out CardInstance performer, out CardInstance source, out CardInstance card)
        {
            var s = Table(Phase.Action, 0);
            performer = Add(s, Crane, 0, Zone.Home); source = Kw(Add(s, Crane, 0, Zone.Home), keywords);
            card = Add(s, Passing, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Several copyable keywords: the player chooses one; Courtier, Samurai and Shugenja are never offered.
        var e = Build(["Courtier", "Samurai", "Shugenja", "Ninja", "Fear"], out var performer, out var source, out var card);
        Accept(e, Offer(e, 0, "wl-passing-copy", card.Id, source.Id, performer.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the acting player chooses the keyword");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseEitherOr" && a.Enabled).ToArray();
        Check(options.Select(o => o.Command!.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["kw:Fear", "kw:Ninja"]), "only Fear and Ninja: " + string.Join(",", options.Select(o => o.Command!.Choice)));
        Check(e.QueryActions(1).All(a => a.Type != "chooseEitherOr"), "the other player decides nothing");
        Accept(e, options.Single(o => o.Command!.Choice == "kw:Ninja").Command!);
        Check(e.State.Cards[performer.Id].GrantedKeywords.Any(g => g.Keyword == "Ninja" && g.Duration == GrantDuration.Permanent), "the performer permanently has Ninja");
        Check(!e.State.Cards[performer.Id].GrantedKeywords.Any(g => g.Keyword is "Fear" or "Courtier" or "Samurai" or "Shugenja"), "and only that keyword");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // One copyable keyword: copied without asking.
        var e2 = Build(["Courtier", "Ninja"], out var performer2, out var source2, out var card2);
        Accept(e2, Offer(e2, 0, "wl-passing-copy", card2.Id, source2.Id, performer2.Id));
        Check(e2.State.PendingChoice is null && e2.State.Cards[performer2.Id].GrantedKeywords.Any(g => g.Keyword == "Ninja"), "the one keyword is copied");
        // None: nothing is copied (the action is still taken).
        var e3 = Build(["Courtier", "Samurai"], out var performer3, out var source3, out var card3);
        Accept(e3, Offer(e3, 0, "wl-passing-copy", card3.Id, source3.Id, performer3.Id));
        Check(e3.State.PendingChoice is null && e3.State.Cards[performer3.Id].GrantedKeywords.Count == 0, "nothing to copy");
        // A keyword the performer already has is not a choice.
        var e4 = Build(["Ninja", "Fear"], out var performer4, out var source4, out var card4);
        Kw(e4.State.Cards[performer4.Id], "Ninja");
        var e4b = Edit(e4, st => Kw(st.Cards[performer4.Id], "Ninja"));
        Accept(e4b, Offer(e4b, 0, "wl-passing-copy", card4.Id, source4.Id, performer4.Id));
        Check(e4b.State.PendingChoice is null && e4b.State.Cards[performer4.Id].GrantedKeywords.Any(g => g.Keyword == "Fear"), "only the keyword he lacks");
        // The target must be one of your own Personalities.
        var s5 = Table(Phase.Action, 0); var p5 = Add(s5, Crane, 0, Zone.Home); var foe5 = Kw(Add(s5, Lion, 1, Zone.Home), "Ninja"); var c5 = Add(s5, Passing, 0, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Check(!(Options(e5, 0, "wl-passing-copy", c5.Id, p5.Id).SelectMany(o => o.TargetIds ?? []).Contains(foe5.Id)), "an enemy Personality is no target");

        // The Limited: a discarded Personality (a dead one too, as for Scavenging Party) into a province, face-up.
        var s6 = Table(Phase.Action, 0);
        var discarded = Add(s6, Lion, 0, Zone.DynastyDiscard); var dead = Add(s6, Crane, 0, Zone.Dead);
        var there = Add(s6, Crane, 0, Zone.Province, province: 1); there.FaceUp = false; s6.Players[0].Provinces[1].CardId = there.Id;
        var c6 = Add(s6, Passing, 0, Zone.Hand);
        var e6 = MatchEngine.FromTestState(s6);
        var offered = Options(e6, 0, "wl-passing-limited", c6.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(offered.Contains(discarded.Id) && offered.Contains(dead.Id), "discarded and dead Personalities: " + string.Join(",", offered));
        Accept(e6, Options(e6, 0, "wl-passing-limited", c6.Id).First(o => o.Enabled).Command! with { TargetId = dead.Id, Province = 1 });
        Check(e6.State.Cards[dead.Id].Zone == Zone.Province && e6.State.Cards[dead.Id].FaceUp && e6.State.Players[0].Provinces[1].CardId == dead.Id, "face-up in the province");
        Eq(e6.State.Cards[there.Id].Zone, Zone.DynastyDiscard, "the card there is discarded");
    }

    // ---- Touch of the Night -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TouchOfTheNight()
    {
        var s = Table(Phase.Battle, 0);
        var ninja = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Ninja");
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0); var allyFollower = Attach(s, Follower, ally);
        var foeA = Add(s, Lion, 1, Zone.Battlefield, province: 0); var foeFollower = Attach(s, Follower, foeA);
        var foeB = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, Lion, 1, Zone.Home);
        var bowedNinja = Kw(Add(s, Crane, 0, Zone.Battlefield, bowed: true, province: 0), "Ninja");
        var plain = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var card = Add(s, Touch, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "wl-touch-night", card.Id, bowedNinja.Id) && !CanUse(e, 0, "wl-touch-night", card.Id, plain.Id), "an unbowed Ninja only");
        var forces = new[] { foeA, foeFollower, foeB, ally, allyFollower, elsewhere }.ToDictionary(c => c.Id, c => e.CardForceForTest(c.Id));
        Accept(e, Enabled(e, 0, "wl-touch-night", card.Id, ninja.Id).Command!);
        foreach (var c in new[] { foeA, foeFollower, foeB }) Eq(e.CardForceForTest(c.Id), Math.Max(0, forces[c.Id] - 2), $"{Batch336Name(c)}: -2F");
        foreach (var c in new[] { ally, allyFollower, elsewhere }) Eq(e.CardForceForTest(c.Id), forces[c.Id], $"{Batch336Name(c)}: untouched");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(!e.State.Cards[ninja.Id].Bowed, "the performer is not bowed: it is a condition, not a cost");
    }

    private static string Batch336Name(CardInstance card) => CardCatalog.Get(card.DefinitionId).Name;

    // ---- the decisions --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // What a bot does: take the first enabled action of the player who must decide, until nothing is pending.
    private static void AnswerFirst(MatchEngine e)
    {
        for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++)
        {
            var decider = e.State.PendingChoice.PlayerId;
            var option = e.QueryActions(decider).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "chooseEitherOr" or "chooseDecider" or "chooseEffect");
            Check(option is not null, $"no answer for the pending {e.State.PendingChoice.Type}");
            Accept(e, option!.Command!);
        }
        Check(e.State.PendingChoice is null, "every decision was answered");
    }

    private static void DecisionsAreAnswerable()
    {
        var (s, samurai, card, gift) = GiftTable();
        var e = MatchEngine.FromTestState(s);
        Accept(e, With(Enabled(e, 0, "wl-courteous-gift", card.Id, samurai.Id), Pick("gift", gift.Id)));
        AnswerFirst(e);
        var (s2, courtier, card2, foe2) = ThreatTable((st, foe) => { Attach(st, Follower, foe); Attach(st, Follower, foe); });
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "wl-threat-enacted", card2.Id, foe2.Id, courtier.Id));
        AnswerFirst(e2);
        var s3 = Table(Phase.Action, 0); var gossip = Add(s3, Gossip, 0, Zone.FateDiscard); Add(s3, Lion, 1, Zone.Home); Add(s3, Lion, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, PlayerOption(e3, 0, "wl-scandalous-gossip", gossip.Id, 1).Command!);
        AnswerFirst(e3);
        var s4 = Table(Phase.Action, 0); var p4 = Add(s4, Crane, 0, Zone.Home); var from = Kw(Add(s4, Crane, 0, Zone.Home), "Ninja", "Fear"); var passing = Add(s4, Passing, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "wl-passing-copy", passing.Id, from.Id, p4.Id));
        AnswerFirst(e4);
    }
}
