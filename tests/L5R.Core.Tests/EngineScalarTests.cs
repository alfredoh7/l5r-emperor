using L5R.Core;

// Issue #875: the client reads a few scalars every frame; MatchEngine.State is a full copy, so they have their own accessors. They must agree with the state after every command.
internal static class EngineScalarTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [("Engine scalar accessors match the State copy through a played game", ScalarsMatchState)];
        runner.Run("engine-scalars", tests);
    }

    private static void ScalarsMatchState()
    {
        foreach (var seed in new ulong[] { 11, 12 })
        {
            var engine = MatchEngine.CreateStarterMatch(seed);
            for (var step = 0; step < 120 && engine.WinnerId is null; step++)
            {
                var state = engine.State;
                if (engine.WinnerId != state.WinnerId || engine.ActivePlayerId != state.ActivePlayerId || engine.PriorityPlayerId != state.PriorityPlayerId || engine.HasPendingChoice != (state.PendingChoice is not null))
                    throw new InvalidOperationException($"Seed {seed} step {step}: a scalar accessor disagrees with State.");
                var action = engine.QueryActions(engine.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null);
                if (action is null || !engine.Execute(action.Command!).Accepted) break;
            }
        }
    }
}
