namespace L5R.Core;

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.AttackStrengthBeforeContext, int?>> AttackStrengthBefore = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.AttackStrengthAfterContext, int?>> AttackStrengthAfter = new();
    internal static readonly OrderedHooks<Func<MatchEngine.AttackKindContext, string?>> AttackKind = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.PerformerAllowsContext, bool>> PerformerAllows = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.TargetAllowsContext, bool>> TargetAllows = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.PerformerCandidatesContext, string?[]?>> PerformerCandidates = new();
    internal static readonly OrderedHooks<Func<MatchEngine.DestroysTargetContext, bool>> DestroysTarget = new();
    internal static readonly OrderedHooks<Func<MatchEngine.BowsTargetContext, bool>> BowsTarget = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.PersonalityForceContext, int>> PersonalityForce = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.ProvinceBonusContext, int>> ProvinceBonus = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.TargetedReactionContext, bool>> TargetedReaction = new();
}
