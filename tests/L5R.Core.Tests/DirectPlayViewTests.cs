using L5R.Core;
using static Batch336Support;

internal static class DirectPlayViewTests
{
    public static void Run(TestRunner runner) => runner.Run("direct-play-view", [
        ("Plans Within Plans armed state is shown only to its owner, and only at an Action Phase opportunity", ArmedFlag)
    ]);

    private static MatchEngine Engine(bool armed, Phase phase)
    {
        var s = MatchEngine.CreatePublishedTestMatch(42).State;
        s.Phase = phase; s.ActivePlayerId = 1; s.PriorityPlayerId = 0; s.PendingChoice = null;
        s.Players[0].PlansWithinPlansArmed = armed; s.Players[0].PlansWithinPlansSets = armed ? 1 : 0;
        return MatchEngine.FromTestState(s);
    }

    private static void ArmedFlag()
    {
        var armed = Engine(true, Phase.Action);
        Batch336Support.Check(armed.GetView(0).Players[0].PlansWithinPlansArmed, "the armed player sees it");
        Batch336Support.Check(!armed.GetView(1).Players[0].PlansWithinPlansArmed, "the opponent's view does not reveal it");
        Batch336Support.Check(!Engine(false, Phase.Action).GetView(0).Players[0].PlansWithinPlansArmed, "not armed");
        Batch336Support.Check(!Engine(true, Phase.Battle).GetView(0).Players[0].PlansWithinPlansArmed, "outside the Action Phase there is no Open opportunity to replace");
    }
}
