namespace L5R.Core;

internal static partial class EngineHooks
{
    // Id must be nameof(MatchState.PendingX), so the existing reflection guard still detects omissions.
    internal static readonly OrderedHooks<Func<MatchState, bool>> DecisionState = new();
    internal static readonly OrderedHooks<Func<MatchState, bool>> NonDecisionState = new();
}
