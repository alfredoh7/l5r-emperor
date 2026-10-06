using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: the second half of holding-wave-13's tests (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    private const string Moneylender = "oracle-l5r-5224", ChugoSeido = "oracle-l5r-1373", FallingRainDojo = "oracle-l5r-2455", GovernorsCourt = "oracle-l5r-2878", RugashiBazaar = "oracle-l5r-6396", TsujikenExp = "oracle-l5r-10045";

    private static void Drain(MatchEngine e) { for (var i = 0; i < 12 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    private static void RugashiBazaarTests()
    {
        // "This card will not be destroyed": the destruction fails; another Holding is destroyed as usual.
        var s = Table(Phase.Action, 0);
        var bazaar = Add(s, RugashiBazaar, 0, Zone.Home);
        var other = Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(bazaar.Id, 0), 3, "Rugashi Bazaar produces 3 Gold");
        Eq(e.DestroyUnitsForTest(bazaar.Id), 0, "it was not destroyed");
        Eq(e.State.Cards[bazaar.Id].Zone, Zone.Home, "Rugashi Bazaar is still in play");
        Eq(e.DestroyUnitsForTest(other.Id), 1, "an ordinary Holding is destroyed");

        // Revenge Battle/Open: needs the Honor loss condition, and gives one to four performers their base Personal Honor as Force.
        MatchState Setup(Phase phase, bool lost, out CardInstance bazaarCard, out CardInstance[] performers)
        {
            var t = Table(phase, phase == Phase.Battle ? 1 : 0); t.PriorityPlayerId = 0;
            t.HonorLostToOtherActionSinceTurnEnded[0] = lost;
            bazaarCard = Add(t, RugashiBazaar, 0, Zone.Home);
            var zone = phase == Phase.Battle ? Zone.Battlefield : Zone.Home;
            int? province = phase == Phase.Battle ? 0 : null;
            performers = Enumerable.Range(0, 5).Select(_ => Add(t, "training-crane-samurai", 0, zone, province: province)).ToArray();
            if (phase == Phase.Battle) Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return t;
        }
        // The Open half (Action Phase): two performers at home get +2F each (base Personal Honor 2).
        var s1 = Setup(Phase.Action, true, out var bazaar1, out var perf1);
        var e1 = MatchEngine.FromTestState(s1);
        var before = e1.CardForceForTest(perf1[0].Id);
        Accept(e1, Use(e1, 0, "rugashi-revenge", bazaar1.Id, selections: Pick("performers", perf1[0].Id, perf1[1].Id)));
        Eq(e1.CardForceForTest(perf1[0].Id), before + 2, "the first performer has +2F (his base Personal Honor)");
        Eq(e1.CardForceForTest(perf1[1].Id), before + 2, "the second performer has +2F");
        Eq(e1.CardForceForTest(perf1[2].Id), before, "a Personality not chosen is unchanged");
        // Not more than four; not none.
        var e1b = MatchEngine.FromTestState(Setup(Phase.Action, true, out var bazaar1b, out var perf1b));
        Check(Rejection(e1b, Use(e1b, 0, "rugashi-revenge", bazaar1b.Id, selections: Pick("performers", perf1b.Select(p => p.Id).ToArray()))).Length > 0, "five performers are too many");
        Check(Rejection(e1b, Use(e1b, 0, "rugashi-revenge", bazaar1b.Id, selections: Pick("performers"))).Length > 0, "at least one performer");
        // Without the Honor loss it cannot be used.
        var e2 = MatchEngine.FromTestState(Setup(Phase.Action, false, out var bazaar2, out var perf2));
        Check(Rejection(e2, Use(e2, 0, "rugashi-revenge", bazaar2.Id, selections: Pick("performers", perf2[0].Id))).Length > 0, "no Honor lost to another player's action, no Revenge");
        // The Battle half: performers at the battlefield (a Battle action) -- four of them.
        var s3 = Setup(Phase.Battle, true, out var bazaar3, out var perf3);
        var e3 = MatchEngine.FromTestState(s3);
        var force3 = e3.CardForceForTest(perf3[0].Id);
        Accept(e3, Use(e3, 0, "rugashi-revenge", bazaar3.Id, selections: Pick("performers", perf3[0].Id, perf3[1].Id, perf3[2].Id, perf3[3].Id)));
        DeclineWindows(e3);
        Eq(e3.CardForceForTest(perf3[3].Id), force3 + 2, "four performers at the battle each get +2F");
        // A Personality at home cannot perform a Battle action.
        var s4 = Setup(Phase.Battle, true, out var bazaar4, out _);
        var home = Add(s4, "training-crane-samurai", 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Check(Rejection(e4, Use(e4, 0, "rugashi-revenge", bazaar4.Id, selections: Pick("performers", home.Id))).Length > 0, "a performer of a Battle action must be at the battlefield");

        // The condition is real: another player's action that costs Honor sets it, one's own does not; it ends when the loser's own turn ends (not when it begins).
        var loss = Table(Phase.Action, 1);
        var tsujiken = Add(loss, TsujikenExp, 1, Zone.Home);
        var victim = Add(loss, "training-crane-samurai", 0, Zone.Home);
        var el = MatchEngine.FromTestState(loss);
        Check(!el.State.HonorLostToOtherActionSinceTurnEnded[0], "nothing lost yet");
        Accept(el, Offer(el, 1, "tsujiken-exp-dishonor", tsujiken.Id, victim.Id));
        Drain(el);
        Check(el.State.HonorLostToOtherActionSinceTurnEnded[0], "player 0 lost Honor from player 1's action");
        RunUntil(el, () => el.State.ActivePlayerId == 0 && el.State.Phase == Phase.Action);
        Check(el.State.HonorLostToOtherActionSinceTurnEnded[0], "her own turn began, but the flag lasts until it ENDS");
        RunUntil(el, () => el.State.ActivePlayerId == 1 && el.State.Phase == Phase.Action);
        Check(!el.State.HonorLostToOtherActionSinceTurnEnded[0], "cleared when her turn ended");
        var own = Table(Phase.Action, 0);
        var tsujikenOwn = Add(own, TsujikenExp, 0, Zone.Home);
        var mine = Add(own, "training-crane-samurai", 0, Zone.Home);
        var eown = MatchEngine.FromTestState(own);
        Accept(eown, Offer(eown, 0, "tsujiken-exp-dishonor", tsujikenOwn.Id, mine.Id));
        Drain(eown);
        Check(!eown.State.HonorLostToOtherActionSinceTurnEnded[0], "a loss from one's own action does not count");
    }

    private static void GovernorsCourtTests()
    {
        MatchState Setup(bool courtBowed, bool courtierBowed, bool enemyUnbowed, out CardInstance court, out CardInstance courtier)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            court = Add(s, GovernorsCourt, 0, Zone.Home, bowed: courtBowed);
            courtier = Add(s, "training-courtier-personality", 0, Zone.Home, bowed: courtierBowed);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: !enemyUnbowed);
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Home && c.Id != enemy.Id && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality)) c.Bowed = true;
            return s;
        }
        // The other player declines: the Court's controller gains 2 Honor, and the Courtier (his bow being the cost) bowed.
        var s1 = Setup(true, false, true, out var court1, out var courtier1);
        var e1 = MatchEngine.FromTestState(s1);
        var honor = Honor(e1, 0);
        var option = e1.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "governors-court-open" && o.Command.TargetPlayerId == 1 && o.Label.Contains("Bow the Courtier"));
        Accept(e1, option.Command!);
        DeclineWindows(e1);
        Eq(e1.State.PendingChoice?.Type, "deciderChoice", "the other player decides");
        var decline = e1.QueryActions(1).First(o => o.Type == "chooseDecider" && o.Label.StartsWith("Choose none"));
        Check(e1.State.Cards[courtier1.Id].Bowed && e1.State.Cards[court1.Id].Bowed, "the Courtier was bowed as the cost; the Court was already bowed and is not");
        Accept(e1, decline.Command!);
        Eq(Honor(e1, 0), honor + 2, "the other player did not choose, so the Court's controller gains 2 Honor");
        // The other player chooses to bow one of his unbowed Personalities: no Honor.
        var s2 = Setup(false, true, true, out var court2, out var courtier2);
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        var bowCourt = e2.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "governors-court-open" && o.Command.TargetPlayerId == 1 && o.Label.Contains("Bow this card"));
        Accept(e2, bowCourt.Command!);
        DeclineWindows(e2);
        Check(e2.State.Cards[court2.Id].Bowed && e2.State.Cards[courtier2.Id].Bowed, "a bowed Courtier cannot pay, so the Court was bowed for it");
        var pick = e2.QueryActions(1).First(o => o.Type == "chooseDecider" && o.Label.StartsWith("Bow"));
        var chosen = e2.State.Cards[pick.Command!.TargetId!];
        Check(!chosen.Bowed && chosen.OwnerId == 1, "only his unbowed Personalities are offered");
        Accept(e2, pick.Command!);
        Check(e2.State.Cards[chosen.Id].Bowed, "he chose to bow one of his Personalities");
        Eq(Honor(e2, 0), honor2, "so no Honor is gained");
        // Even if the Court is bowed, the Courtier can pay; if both are bowed nothing can.
        var s3 = Setup(true, true, true, out var court3, out var courtier3);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "governors-court-open", court3.Id), "with the Court and the Courtier both bowed, the cost cannot be paid");
        // The other player must have an unbowed Personality in play.
        var s4 = Setup(false, false, false, out var court4, out _);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!e4.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "governors-court-open" && o.Command.TargetPlayerId == 1), "another player with an unbowed Personality is needed");
        Check(!e4.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "governors-court-open" && o.Command.TargetPlayerId == 0), "and it is another player");
    }

    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();

    private static void ChugoSeidoTests()
    {
        // Player 1 (the Attacker) plays a Battle action that bows player 0's Personality `a`; player 0's Chugo Seido may destroy itself to redirect it to another Samurai at the same location.
        MatchState Setup(out CardInstance chugo, out CardInstance a, out CardInstance samuraiHere, out CardInstance samuraiHome, out CardInstance notSamurai, out CardInstance bushido, bool chugoBowed = false)
        {
            var s = Table(Phase.Battle, 1);
            chugo = Add(s, ChugoSeido, 0, Zone.Home, bowed: chugoBowed);
            a = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
            samuraiHere = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
            notSamurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            samuraiHome = Add(s, "training-samurai", 0, Zone.Home);
            Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            bushido = Add(s, "training-bushido-virtue", 1, Zone.Hand);
            return s;
        }
        var s1 = Setup(out var chugo1, out var a1, out var here1, out var home1, out var plain1, out var bushido1);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Offer(e1, 1, "training-bow-at-battle", bushido1.Id, a1.Id));
        Eq(e1.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for Chugo Seido's controller");
        var offers = Listed(e1, 0, "chugo-seido-redirect").Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual([here1.Id]), "one option: the other Samurai at the same location (not the target, not the non-Samurai, not the one at home): " + string.Join(",", offers));
        Accept(e1, Listed(e1, 0, "chugo-seido-redirect").First(o => o.Enabled).Command!);
        for (var guard = 0; e1.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++) Accept(e1, new GameCommand("declineReaction", e1.State.PriorityPlayerId, e1.Version));
        Check(e1.State.Cards[here1.Id].Bowed && !e1.State.Cards[a1.Id].Bowed, "the action landed on the chosen Samurai");
        Check(e1.State.Cards[chugo1.Id].Zone != Zone.Home, "Chugo Seido was destroyed as the cost");
        // Not offered with no other Samurai at the location; nor for another player's Reaction on your own action; nor when Chugo Seido is bowed.
        var s2 = Setup(out _, out var a2, out var here2, out _, out _, out var bushido2);
        s2.Cards.Remove(here2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", bushido2.Id, a2.Id));
        Check(e2.State.PendingChoice is null || !Listed(e2, 0, "chugo-seido-redirect").Any(o => o.Enabled), "no other Samurai at the location, no redirect");
        var s3 = Setup(out _, out var a3, out _, out _, out _, out var bushido3, chugoBowed: true);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", bushido3.Id, a3.Id));
        Check(e3.State.PendingChoice is null || !Listed(e3, 0, "chugo-seido-redirect").Any(o => o.Enabled), "a bowed Chugo Seido cannot react");
        // Your own action targeting your Personality is not "another player's action".
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        Add(s4, ChugoSeido, 0, Zone.Home);
        var mine = Add(s4, "training-samurai", 0, Zone.Home);
        Add(s4, "training-samurai", 0, Zone.Home);
        var bushido4 = Add(s4, "training-bushido-virtue", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "training-bow-any", bushido4.Id, mine.Id));
        Check(e4.State.PendingChoice is null && e4.State.Cards[mine.Id].Bowed, "no window for your own action");
        // Any action of another player counts, not only a Battle action: an Open action in his own Action Phase, redirected to another Samurai at home.
        var s5 = Table(Phase.Action, 1); s5.PriorityPlayerId = 1;
        var chugo5 = Add(s5, ChugoSeido, 0, Zone.Home);
        var target5 = Add(s5, "training-samurai", 0, Zone.Home);
        var other5 = Add(s5, "training-samurai", 0, Zone.Home);
        var bushido5 = Add(s5, "training-bushido-virtue", 1, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 1, "training-bow-any", bushido5.Id, target5.Id));
        Eq(e5.State.PendingChoice?.Type, "beforeActionReaction", "an Open action targeting your Personality opens the window too");
        Accept(e5, Listed(e5, 0, "chugo-seido-redirect").First(o => o.Enabled && o.Command!.TargetId == other5.Id).Command!);
        for (var guard = 0; e5.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++) Accept(e5, new GameCommand("declineReaction", e5.State.PriorityPlayerId, e5.Version));
        Check(e5.State.Cards[other5.Id].Bowed && !e5.State.Cards[target5.Id].Bowed && e5.State.Cards[chugo5.Id].Zone != Zone.Home, "the Open action landed on the other Samurai");
    }

    // A duel of the challenger's Duelist (player 0) against player 1's Personality, driven to the window between the Duelist re-focus and the Focus Effects.
    private static MatchEngine DuelToFocusEffects(int enemyChiBonus, bool duelistIsDuelist, bool withDojo, bool dojoBowed, out CardInstance dojo, out CardInstance duelist, out CardInstance rival)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        dojo = Add(s, FallingRainDojo, 0, Zone.Home, bowed: dojoBowed);
        if (!withDojo) s.Cards.Remove(dojo.Id);
        duelist = Add(s, duelistIsDuelist ? "training-duelist-personality" : "training-crane-samurai", 0, Zone.Home); // Chi 3
        rival = Add(s, "training-lion-samurai", 1, Zone.Home); // Chi 3
        if (enemyChiBonus != 0) rival.Modifiers.Add(new("test", "test", "Chi", enemyChiBonus));
        var strategy = Add(s, "training-duel-open", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-duel-open", strategy.Id, rival.Id, duelist.Id));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && e.State.PendingDuel is { Stage: "Reaction" } && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new("strike", 0, e.Version));
        if (e.State.PendingDuel is { Stage: "DuelistRefocus" }) Accept(e, new("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        return e;
    }

    private static void FallingRainDojoTests()
    {
        // The Duelist (Chi 3) faces a Personality with Chi 5: the window opens, the Dojo gives +1 to his duel stat until the duel ends.
        var e = DuelToFocusEffects(2, true, true, false, out var dojo, out var duelist, out _);
        Eq(e.State.PendingChoice?.Type, "duelReaction", "the window before Focus Effects is open");
        Check(e.State.PendingDuel is { Stage: "FocusEffectsReaction" }, "the duel waits at the Focus Effects");
        var boost = e.QueryActions(0).FirstOrDefault(o => o.Command?.AbilityId == "falling-rain-boost" && o.Enabled);
        Check(boost is not null, "Falling Rain Dojo's Reaction is offered: " + string.Join("; ", e.QueryActions(0).Select(o => o.Label)));
        Accept(e, boost!.Command!);
        // Issue #644: nobody holds another reaction, so the window closes at once and the duel goes on; the +1 is read from the resolved duel below.
        Check(!e.QueryActions(0).Any(o => o.Command?.AbilityId == "falling-rain-boost" && o.Enabled), "once per turn");
        for (var guard = 0; e.State.PendingDuel is not null && guard < 12; guard++)
            Accept(e, e.State.PendingChoice?.Type == "duelReaction" ? new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.LastDuel!.Result!.ChallengerStat, 4, "the duel resolved with the Duelist's stat at Chi 3 + 1");
        Check(e.State.PendingDuel is null, "the duel ended, and the bonus with it");

        // Not lower: equal duel stats offer nothing (the window does not even open); a stronger Duelist neither.
        var equal = DuelToFocusEffects(0, true, true, false, out _, out _, out _);
        Check(equal.State.PendingChoice?.Type != "duelReaction" || !equal.QueryActions(0).Any(o => o.Command?.AbilityId == "falling-rain-boost"), "an equal duel stat is not lower");
        Check(equal.State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "no window without a legal Reaction");
        // Only a Duelist: a Personality without the Duelist keyword does not qualify.
        var plain = DuelToFocusEffects(2, false, true, false, out _, out _, out _);
        Check(plain.State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "his Personality must be a Duelist");
        // A bowed Dojo cannot react; no Dojo, no window.
        var bowed = DuelToFocusEffects(2, true, true, true, out _, out _, out _);
        Check(bowed.State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "a bowed Dojo cannot react");
        // The opposing player's Dojo cannot help my Duelist.
        var theirs = MatchState_TheirDojo();
        Check(theirs.State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "only the Dojo of the Duelist's controller works");
    }

    private static MatchEngine MatchState_TheirDojo()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        Add(s, FallingRainDojo, 1, Zone.Home);
        var duelist = Add(s, "training-duelist-personality", 0, Zone.Home);
        var rival = Add(s, "training-lion-samurai", 1, Zone.Home); rival.Modifiers.Add(new("test", "test", "Chi", 2));
        var strategy = Add(s, "training-duel-open", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-duel-open", strategy.Id, rival.Id, duelist.Id));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new("strike", 0, e.Version));
        if (e.State.PendingDuel is { Stage: "DuelistRefocus" }) Accept(e, new("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        return e;
    }

    private static int Wealth(MatchEngine e, string id) => e.State.Cards[id].Tokens.Count(t => t.Type == "Wealth");

    private static void MoneylenderTests()
    {
        // Entering play: Wealth tokens equal to the Gold paid for it (a Gold Cost of 0 printed; raised to 3 for this recruit).
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = Moneylender; s.Cards[cardId].FaceUp = true;
        s.Cards[cardId].Modifiers.Add(new("test", "test", "GoldCost", 3));
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchaseForTest(0, cardId);
        Eq(quote, 3, "the recruit price fixture is 3 Gold");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
        Eq(Wealth(e, cardId), 3, "it entered play with a Wealth token for each Gold paid");
        // At its printed cost of 0 it enters with no tokens.
        var s0 = MatchEngine.CreateEconomyScenario().State;
        var id0 = s0.Players[0].Provinces[0].CardId!; s0.Cards[id0].DefinitionId = Moneylender; s0.Cards[id0].FaceUp = true;
        var e0 = MatchEngine.FromTestState(s0);
        Accept(e0, new GameCommand("purchase", 0, e0.Version, CardId: id0, SourceIds: []));
        Eq(Wealth(e0, id0), 0, "paid 0 Gold: no tokens");

        // Limited: bow it and pay any amount of Gold to put that many tokens on it.
        var a = Table(Phase.Action, 0); a.PriorityPlayerId = 0;
        var lender = Add(a, Moneylender, 0, Zone.Home);
        var ea = MatchEngine.FromTestState(a);
        var invest = Enabled(ea, 0, "moneylender-invest", lender.Id);
        Accept(ea, invest.Command! with { GoldAmount = 4, SourceIds = ea.PaymentOptions(0, 4).First().SourceIds });
        Eq(Wealth(ea, lender.Id), 4, "four Gold paid, four Wealth tokens");
        Check(ea.State.Cards[lender.Id].Bowed, "bowed as its cost");
        Check(!CanUse(ea, 0, "moneylender-invest", lender.Id), "once a turn, and bowed");

        // As a Gold source: it produces Gold equal to the tokens it removes -- only as many as the payment needs, bowing it; unbowed with no tokens it produces nothing.
        var b = Table(Phase.Action, 0); b.PriorityPlayerId = 0;
        var lender2 = Add(b, Moneylender, 0, Zone.Home);
        for (var i = 0; i < 5; i++) lender2.Tokens.Add(new CardToken($"tok-{i}", "Wealth", "test"));
        foreach (var c in b.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != lender2.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        var peddler = Add(b, TravelingPeddler, 0, Zone.Home, bowed: true);
        var eb = MatchEngine.FromTestState(b);
        Eq(eb.GoldProduced(lender2.Id, 0), 5, "it can produce Gold equal to its five tokens");
        Check(eb.PaymentOptions(0, 3).Any(p => p.SourceIds.SequenceEqual([lender2.Id])), "it pays for a 3-Gold cost");
        var peddler2 = Add(b, TravelingPeddler, 0, Zone.Home);
        var ec = MatchEngine.FromTestState(b);
        var draw = ec.QueryActions(0).First(o => o.Command?.AbilityId == "peddler-draw" && o.Command.CardId == peddler2.Id && o.Enabled);
        Accept(ec, draw.Command! with { SourceIds = [lender2.Id] });
        Eq(Wealth(ec, lender2.Id), 2, "3 Gold were needed, so only 3 of its 5 tokens were removed");
        Check(ec.State.Cards[lender2.Id].Bowed, "it bowed to produce");
        // A Moneylender without tokens (or bowed) is not a Gold source.
        var c0 = Table(Phase.Action, 0);
        var empty = Add(c0, Moneylender, 0, Zone.Home);
        var ed = MatchEngine.FromTestState(c0);
        Eq(ed.GoldProduced(empty.Id, 0), 0, "no tokens, no Gold");
        Check(!ed.PaymentOptions(0, 1).Any(p => p.SourceIds.Contains(empty.Id)), "an empty Moneylender is not offered as a source");
    }
}
