namespace L5R.Core;

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, PendingEitherOr, string, string, string?>> EitherOrPrompt = new();
    internal static readonly OrderedHooks<Func<MatchEngine, PendingEitherOr, CardInstance?, CardInstance, Func<int, bool>, MatchEngine.EitherOrOption[]?>> EitherOrOptions = new();
    internal static readonly OrderedHooks<Func<MatchEngine, PendingEitherOr, MatchEngine.EitherOrOption, CardInstance, PlayerState, CardInstance?, string[]?, bool>> ApplyEitherOr = new();
    internal static readonly OrderedHooks<Func<MatchEngine, PendingEitherOr, bool>> FinishEitherOr = new();
    internal static readonly OrderedHooks<Func<MatchEngine, PendingDeciderChoice, string?>> DeciderPrompt = new();
    internal static readonly OrderedHooks<Func<MatchEngine, string, int, GameCommand, (List<string> Candidates, int Min, int Max)?>> DeciderOptions = new();
    internal static readonly OrderedHooks<Func<MatchEngine, PendingDeciderChoice, bool>> ApplyDecider = new();
    internal static readonly OrderedHooks<Func<MatchEngine, string, string?>> DeciderVerb = new();
    internal static readonly OrderedHooks<Func<MatchEngine, string, GameCommand, int?>> DeciderIdBefore = new();
    internal static readonly OrderedHooks<Func<MatchEngine, string, GameCommand, int?>> DeciderIdAfter = new();
}

public sealed partial class MatchEngine
{
    private string? RegisteredEitherOrPrompt(PendingEitherOr p, string source, string affected)
    {
        foreach (var hook in EngineHooks.EitherOrPrompt.Items)
            if (hook(this, p, source, affected) is { } result) return result;
        return null;
    }
    private EitherOrOption[]? RegisteredEitherOrOptions(PendingEitherOr p, CardInstance? affected, CardInstance source, Func<int, bool> payable)
    {
        foreach (var hook in EngineHooks.EitherOrOptions.Items)
            if (hook(this, p, affected, source, payable) is { } result) return result;
        return null;
    }
    private bool RegisteredApplyEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider, CardInstance? affected, string[]? sourceIds)
    {
        foreach (var hook in EngineHooks.ApplyEitherOr.Items)
            if (hook(this, p, option, source, decider, affected, sourceIds)) return true;
        return false;
    }
    private bool RegisteredFinishEitherOr(PendingEitherOr p)
    {
        foreach (var hook in EngineHooks.FinishEitherOr.Items)
            if (hook(this, p)) return true;
        return false;
    }
    private string? RegisteredDeciderPrompt(PendingDeciderChoice p)
    {
        foreach (var hook in EngineHooks.DeciderPrompt.Items)
            if (hook(this, p) is { } result) return result;
        return null;
    }
    private (List<string> Candidates, int Min, int Max)? RegisteredDeciderOptions(string specId, int deciderId, GameCommand command)
    {
        foreach (var hook in EngineHooks.DeciderOptions.Items)
            if (hook(this, specId, deciderId, command) is { } result) return result;
        return null;
    }
    private bool RegisteredApplyDecider(PendingDeciderChoice p)
    {
        foreach (var hook in EngineHooks.ApplyDecider.Items)
            if (hook(this, p)) return true;
        return false;
    }
    private string? RegisteredDeciderVerb(string specId)
    {
        foreach (var hook in EngineHooks.DeciderVerb.Items)
            if (hook(this, specId) is { } result) return result;
        return null;
    }
    private int? RegisteredDeciderIdBefore(string specId, GameCommand command)
    {
        foreach (var hook in EngineHooks.DeciderIdBefore.Items)
            if (hook(this, specId, command) is { } result) return result;
        return null;
    }
    private int? RegisteredDeciderIdAfter(string specId, GameCommand command)
    {
        foreach (var hook in EngineHooks.DeciderIdAfter.Items)
            if (hook(this, specId, command) is { } result) return result;
        return null;
    }
}
