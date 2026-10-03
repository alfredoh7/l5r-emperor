using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: Successful Bounty and Alter History (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    private static int StartingHonorOf(MatchState s, int player) => CardCatalog.Get(s.Cards[s.Players[player].StrongholdId].DefinitionId).StartingHonor;

    // ---- Successful Bounty ------------------------------------------------------------------------------------------------------------------------------------

    private static void SuccessfulBountyTests()
    {
        var e = Reveal(SuccessfulBounty, out var bounty);
        Eq(e.State.Cards[bounty.Id].ExpiresAtEventsPhaseTurnsTaken, e.State.Players[1].TurnsTaken + 3, "three turns from now");
        // Tsujiken - exp dishonors a Personality (a Limited action, so "from an action"): 1 Honor from his own text, and 2 more from the Bounty when the Personality opposes a Magistrate or Courtier.
        MatchState Setup(string opposingCard, bool atBattlefield, bool withBounty, out CardInstance tsujiken, out CardInstance victim)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            if (withBounty) Add(s, SuccessfulBounty, 1, Zone.EventInPlay);
            tsujiken = Add(s, TsujikenExp, 0, Zone.Home);
            victim = Add(s, "training-lion-samurai", 1, atBattlefield ? Zone.Battlefield : Zone.Home, province: atBattlefield ? 0 : null);
            Add(s, opposingCard, 0, atBattlefield ? Zone.Battlefield : Zone.Home, province: atBattlefield ? 0 : null);
            return s;
        }
        int Loss(MatchState s, CardInstance tsujiken, CardInstance victim)
        {
            var en = MatchEngine.FromTestState(s);
            var before = Honor(en, 1);
            Accept(en, Offer(en, 0, "tsujiken-exp-dishonor", tsujiken.Id, victim.Id));
            Drain(en);
            Check(en.State.Cards[victim.Id].Dishonorable, "he became dishonorable");
            return before - Honor(en, 1);
        }
        var s1 = Setup("training-magistrate-personality", true, true, out var t1, out var v1);
        Eq(Loss(s1, t1, v1), 3, "opposing a Magistrate: Tsujiken's 1 Honor and the Bounty's 2");
        var s2 = Setup("training-courtier-personality", true, true, out var t2, out var v2);
        Eq(Loss(s2, t2, v2), 3, "opposing a Courtier too");
        var s3 = Setup("training-lion-samurai", true, true, out var t3, out var v3);
        Eq(Loss(s3, t3, v3), 1, "opposing neither: only Tsujiken's own loss");
        var s4 = Setup("training-magistrate-personality", false, true, out var t4, out var v4);
        Eq(Loss(s4, t4, v4), 1, "not at a battlefield he opposes nobody");
        var s5 = Setup("training-magistrate-personality", true, false, out var t5, out var v5);
        Eq(Loss(s5, t5, v5), 1, "without the Event, no extra loss");
        // Ends as its owner's Events Phase three turns from now begins.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == e.State.Cards[bounty.Id].ExpiresAtEventsPhaseTurnsTaken);
        Check(e.State.Cards[bounty.Id].Zone == Zone.DynastyDiscard, "discarded as his Events Phase three turns on began");
    }

    // ---- Alter History ----------------------------------------------------------------------------------------------------------------------------------------

    private static void AlterHistoryTests()
    {
        var e = Reveal(AlterHistory, out var history);
        Eq(e.State.Cards[history.Id].Zone, Zone.EventInPlay, "it waits in play");
        // The next time its owner (player 1) destroys a province as the Attacker: the Defender loses 5 Honor, and when that brings him below his starting Family Honor he ignores Honor Requirements.
        MatchState Setup(out CardInstance evt, out CardInstance attacker)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1;
            evt = Add(s, AlterHistory, 1, Zone.EventInPlay);
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var s1 = Setup(out var evt1, out var atk1);
        var start = StartingHonorOf(s1, 0);
        s1.Players[0].Honor = start + 2; // 2 above the start: the loss of 5 brings him below it
        var e1 = MatchEngine.FromTestState(s1);
        e1.ProvinceDestroyedForTest(1, 0, 1, [atk1.Id]);
        Eq(Honor(e1, 0), start - 3, "the Defender lost 5 Honor");
        Check(e1.State.Players[0].IgnoresHonorRequirements, "and, below his starting Family Honor, ignores Honor Requirements from now on");
        Eq(e1.State.Cards[evt1.Id].Zone, Zone.DynastyDiscard, "the Event is spent");
        // Honor Requirements are ignored when he brings a Personality into play: a 4-requirement Personality he cannot otherwise afford by Honor.
        var s1b = Setup(out _, out _);
        var recruitCard = s1b.Players[0].Provinces[0].CardId!; s1b.Cards[recruitCard].DefinitionId = "training-crane-samurai"; s1b.Cards[recruitCard].FaceUp = true;
        s1b.Players[0].Honor = 0; // below the Personality's Honor Requirement of 4
        var without = MatchEngine.FromTestState(s1b).QuotePurchase(0, recruitCard);
        Check(without.Error is not null && without.Error.Contains("Honor"), "without the effect a Personality with Honor Requirement 4 cannot be recruited at 0 Honor: " + without.Error);
        s1b.Players[0].IgnoresHonorRequirements = true;
        Check(MatchEngine.FromTestState(s1b).QuotePurchase(0, recruitCard).Error is null, "with it he can");
        // Not below his starting Family Honor afterwards: no waiver. And "the next time": a second destroyed province does nothing more.
        var s2 = Setup(out _, out var atk2); s2.Players[0].Honor = StartingHonorOf(s2, 0) + 9;
        var e2 = MatchEngine.FromTestState(s2);
        e2.ProvinceDestroyedForTest(1, 0, 1, [atk2.Id]);
        Eq(Honor(e2, 0), StartingHonorOf(s2, 0) + 4, "lost 5");
        Check(!e2.State.Players[0].IgnoresHonorRequirements, "still at or above his starting Family Honor: no waiver");
        e2.ProvinceDestroyedForTest(1, 0, 2, [atk2.Id]);
        Eq(Honor(e2, 0), StartingHonorOf(s2, 0) + 4, "the Event was spent: the next destroyed province costs nothing");
        // Only as the Attacker: a province the Event's owner loses does nothing.
        var s3 = Setup(out _, out var atk3); s3.Players[0].Honor = 10; s3.Players[1].Honor = 10;
        var e3 = MatchEngine.FromTestState(s3);
        e3.ProvinceDestroyedForTest(0, 1, 1, [atk3.Id]);
        Eq(Honor(e3, 1), 10, "the Event's owner was the Defender: no loss");
    }
}
