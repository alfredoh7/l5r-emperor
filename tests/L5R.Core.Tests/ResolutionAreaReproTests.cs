using L5R.Core;

// REPRODUCTION ONLY -- docs/decisions/2026-10-13-action-resolution-area.md (VALIDATION_REPORT finding 5). NOT FOR MERGE: this fails on current main by design.
// A hand-played Strategy stays in Zone.Hand (and in Player.Hand / PlayerView.HandCount) while the announce window and the other pre-resolution windows are open.
internal static class ResolutionAreaReproTests
{
    private const string StrategicStrike = "oracle-l5r-7525", NoHiding = "oracle-l5r-5599";

    public static void Run(TestRunner runner) => runner.Run("resolution-area-REPRO", [
        ("REPRO: a Strategic Strike paused in the announce window is still in its owner's hand list and counted in the public hand count", AnnouncedStrategyStillInHand),
    ]);

    private static void AnnouncedStrategyStillInHand()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var strike = Batch336Support.Add(s, StrategicStrike, 0, Zone.Hand);
        var hiding = Batch336Support.Add(s, NoHiding, 0, Zone.Hand); // its announce Reaction pauses the action while the Strike is still unresolved
        var scout = Batch336Support.Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        RegressionAssertions.Accept(e, Batch336Support.Offer(e, 0, "strategic-strike-attack", strike.Id, enemy.Id, scout.Id));
        Batch336Support.Eq(e.State.PendingChoice?.Type, "announceReaction", "the announcement paused for a Reaction");
        // Expected after the fix: the Strike is in the resolution area, so the hand is only No Hiding Place.
        var own = e.GetView(0).Players[0]; var opp = e.GetView(1).Players[0];
        Batch336Support.Check(!own.Hand.Any(c => c.Id == strike.Id), "the played Strategy is not in its owner's hand list");
        Batch336Support.Eq(own.HandCount, 1, "own hand count excludes the played card");
        Batch336Support.Eq(opp.HandCount, 1, "the opponent's view of the hand count excludes the played card");
        Batch336Support.Check(e.State.Cards[strike.Id].Zone != Zone.Hand, "the played card has left Zone.Hand");
    }
}

