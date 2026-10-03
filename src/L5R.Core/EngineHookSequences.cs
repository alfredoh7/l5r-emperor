namespace L5R.Core;

public sealed partial class MatchEngine
{
    private void RegisteredValidateEarly(ValidationHookContext context)
    {
        foreach (var hook in EngineHooks.ValidateEarly.Items) hook(this, context);
    }
    private void RegisteredValidateMiddle(ValidationHookContext context)
    {
        foreach (var hook in EngineHooks.ValidateMiddle.Items) hook(this, context);
    }
    private void RegisteredValidateLate(ValidationHookContext context)
    {
        foreach (var hook in EngineHooks.ValidateLate.Items) hook(this, context);
    }
    internal readonly record struct ValidationHookContext(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source);
    internal readonly record struct RunHookContext(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source,
        CardInstance? target, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBeforeBattleAction,
        HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants);
    private bool? RegisteredRunEffects(RunHookContext context)
    {
        foreach (var hook in EngineHooks.RunEffects.Items)
            if (hook(this, context) is { } result) return result;
        return null;
    }
}

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Action<MatchEngine, MatchEngine.ValidationHookContext>> ValidateEarly = new();
    internal static readonly OrderedHooks<Action<MatchEngine, MatchEngine.ValidationHookContext>> ValidateMiddle = new();
    internal static readonly OrderedHooks<Action<MatchEngine, MatchEngine.ValidationHookContext>> ValidateLate = new();
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.RunHookContext, bool?>> RunEffects = new();
}
