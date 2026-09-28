namespace L5R.Core;

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, string, GameCommand, bool>> PendingChoice = new();
}
