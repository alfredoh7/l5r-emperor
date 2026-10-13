using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P14 (issue #713, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): the Honor-change modifier registry.
//
// GainHonor applied a dozen hard-wired modifiers and had no standing store, so "increase the next Honor gain or loss from your cards by 1" (Court Scribe) or "reduce each Honor gain to 0 this turn" had
// no place to live. Two pieces:
//   THE CHANNEL    EngineHooks.HonorChange: Func<HonorChangeContext, HonorChangeResult?>, consulted by GainHonor at two stages. STANDING is before the Honor-change Reaction window (EmperorRulingR4Honor.cs),
//                  so the window opens with the amount as modified; a redirect applies here too. APPLIED is where Shiro Chugo's reduction always ran, after the window's Reactions and the other reductions.
//                  Registrants chain: each sees the amount the one before it left, returns null when it has nothing to say, and the context names every fact a card text may ask about.
//   THE STORE      MatchState.HonorModifiers: standing modifiers a card adds with AddHonorModifier. HonorModifierStore (the first STANDING registrant) reads it. A modifier that has expired, or been consumed,
//                  is removed; the window's abandon-and-replay (the command replays from the state it started in) consumes it again exactly once.
// A change is described by HonorChangeContext: who, how much, why, which action, which card caused it, whether it is a Proclaim, and the cards the action targeted.

/// <summary>The two points GainHonor consults the channel at.</summary>
public enum HonorChangeStage { Standing, Applied }

/// <summary>An Honor change about to land. <see cref="SourceCardId"/> is the card the caller named, or the card of the action in progress; null for a rulebook change (Proclaim excepted, see IsProclaim).</summary>
internal sealed record HonorChangeContext(MatchEngine Engine, HonorChangeStage Stage, int PlayerId, int Amount, string Reason, string? ActionKey, string? SourceCardId, int? SourcePlayerId,
    bool CausedByOwnCard, bool IsProclaim, IReadOnlyList<string> TargetedCardIds);

/// <summary>What a registrant did to the change: the new amount (same sign or 0), a player to take it instead, and the id of a store modifier it consumed (removed by GainHonor).</summary>
internal sealed record HonorChangeResult(int NewAmount, int? RedirectToPlayer = null, string? ConsumedModifierId = null);

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<HonorChangeContext, HonorChangeResult?>> HonorChange = new();
}

/// <summary>Kinds of standing Honor modifier.</summary>
public static class HonorModifierKinds
{
    /// <summary>The first qualifying change (gain or loss): its size is changed by Delta, or set to SetTo.</summary>
    public const string NextChange = "next-change";
    /// <summary>The next change that is a Proclaim's gain.</summary>
    public const string NextProclaim = "next-proclaim";
    /// <summary>Every qualifying gain, until it expires: changed by Delta (negative reduces), or set to SetTo.</summary>
    public const string EachGain = "each-gain";
    /// <summary>Every qualifying gain is reduced to 0 until it expires.</summary>
    public const string NoGain = "no-gain";
    /// <summary>Every qualifying loss is reduced to 0 until it expires.</summary>
    public const string NoLoss = "no-loss";
}

/// <summary>Filters a modifier may carry; every one must hold for the modifier to apply. An unknown filter never matches.</summary>
public static class HonorModifierFilters
{
    /// <summary>The change comes from a card the affected player owns (a rulebook change has no card).</summary>
    public const string OwnCards = "own-cards";
    /// <summary>The change is not a Proclaim's.</summary>
    public const string NotProclaim = "not-proclaim";
    /// <summary>The change is an effect of an action (not rulebook Honor, not a Focus Effect, not a Proclaim).</summary>
    public const string ActionEffects = "action-effects";
    /// <summary>Only while the Action Phase is the phase.</summary>
    public const string ActionPhaseOnly = "action-phase-only";
    /// <summary>"unless-controls:Keyword": the modifier does nothing while the affected player controls a card with the keyword.</summary>
    public const string UnlessControlsPrefix = "unless-controls:";
    /// <summary>"targeted-by-other:cardId": the change comes from another player's action that targeted the card.</summary>
    public const string TargetedByOtherPrefix = "targeted-by-other:";
}

/// <summary>A standing Honor modifier. A negative Delta shrinks the change; SetTo (e.g. a discarded card's Focus Value) replaces its size. UntilTurn: it lapses after that turn number (null: until consumed).</summary>
public sealed class HonorModifier
{
    public string Id { get; set; } = "";
    public int OwnerPlayerId { get; set; }
    /// <summary>The player whose changes it modifies; null for either player's.</summary>
    public int? AffectedPlayerId { get; set; }
    public string Kind { get; set; } = HonorModifierKinds.NextChange;
    public int Delta { get; set; }
    public int? SetTo { get; set; }
    public List<string> Filters { get; set; } = [];
    public int? UntilTurn { get; set; }
    public string? SourceCardId { get; set; }
}

public sealed partial class MatchState
{
    // 3000-3099 is reserved for P14. Both properties are omitted while unused, so a match that never had a modifier serializes exactly as before.
    [JsonPropertyOrder(3000)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HonorModifier>? HonorModifiers { get; set; }
    [JsonPropertyOrder(3001)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int HonorModifierSequence { get; set; }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterHonorModifierHooks()
    {
        EngineHooks.HonorChange.Register("HonorModifierStore", 10000, static context => context.Engine.StoredHonorModifiers(context));
        // Shiro Chugo's "reduce any Honor gain or loss from the action's effects by 2" (EmperorStrongholdBatch2.cs): the first registrant at the APPLIED stage, where it always ran.
        EngineHooks.HonorChange.Register("ChugoHonorReduction", 10100, static context =>
            context.Stage == HonorChangeStage.Applied && context.Engine.ChugoReducedHonorChange(context.PlayerId, context.Amount, context.Reason) is var reduced && reduced != context.Amount
                ? new HonorChangeResult(reduced) : null);
    }

    /// <summary>Adds a standing modifier and returns its id.</summary>
    internal string AddHonorModifier(HonorModifier modifier)
    {
        _state.HonorModifiers ??= [];
        PruneHonorModifiers();
        _state.HonorModifiers ??= [];
        modifier.Id = string.IsNullOrEmpty(modifier.Id) ? $"honor-modifier-{++_state.HonorModifierSequence}" : modifier.Id;
        _state.HonorModifiers.Add(modifier);
        return modifier.Id;
    }

    internal IReadOnlyList<HonorModifier> HonorModifierList => (IReadOnlyList<HonorModifier>?)_state.HonorModifiers ?? [];

    private void PruneHonorModifiers()
    {
        if (_state.HonorModifiers is null) return;
        _state.HonorModifiers.RemoveAll(m => m.UntilTurn is { } until && _state.TurnNumber > until);
        if (_state.HonorModifiers.Count == 0) _state.HonorModifiers = null;
    }

    /// <summary>GainHonor's consult of the channel. Returns the player who takes the change and the amount to apply (0 stops the change).</summary>
    private (int PlayerId, int Amount) ApplyHonorChangeHooks(HonorChangeStage stage, int playerId, int amount, string reason, CardInstance? source)
    {
        if (amount == 0) return (playerId, amount);
        // A rulebook change (a Dishonorable death's loss) is caused by no card and no action: no card effect's modifier, scope or targeting reaches it.
        var actionKey = "";
        if (!_rulebookHonorChange) CurrentActionFacts(out actionKey, out _);
        var command = _rulebookHonorChange ? null : _runningCommand;
        var card = _rulebookHonorChange ? null : source ?? (command?.CardId is { } id && _state.Cards.TryGetValue(id, out var running) ? running : null);
        var targeted = new List<string>();
        if (command?.TargetId is { } target) targeted.Add(target);
        if (command?.CardIds is { } ids) targeted.AddRange(ids);
        var context = new HonorChangeContext(this, stage, playerId, amount, reason, string.IsNullOrEmpty(actionKey) ? null : actionKey, card?.Id, card?.OwnerId, card is not null && card.OwnerId == playerId,
            _proclaimAction is not null && !_rulebookHonorChange, targeted);
        foreach (var hook in EngineHooks.HonorChange.Items)
        {
            if (hook(context) is not { } result) continue;
            if (result.ConsumedModifierId is { } consumed) { _state.HonorModifiers?.RemoveAll(m => m.Id == consumed); PruneHonorModifiers(); }
            if (result.RedirectToPlayer is { } to && to != playerId)
            {
                Emit("honorRedirected", $"{Player(to).Name} takes the Honor {(amount > 0 ? "gain" : "loss")} of {Math.Abs(amount)} from {reason} instead of {Player(playerId).Name}.", to);
                playerId = to;
            }
            if (result.NewAmount != amount && stage == HonorChangeStage.Standing)
                Emit("honorChangeModified", $"{Player(playerId).Name}'s Honor {(amount > 0 ? "gain" : "loss")} from {reason} {(result.NewAmount == 0 ? "is reduced to 0" : $"becomes {Math.Abs(result.NewAmount)}")}.", playerId);
            amount = result.NewAmount;
            if (amount == 0) break;
            context = context with { PlayerId = playerId, Amount = amount };
        }
        return (playerId, amount);
    }

    // The store's registrant: every modifier that applies, in the order they were added; a next-change/next-proclaim one is consumed and ends the walk.
    private HonorChangeResult? StoredHonorModifiers(HonorChangeContext context)
    {
        if (context.Stage != HonorChangeStage.Standing || _state.HonorModifiers is null) return null;
        PruneHonorModifiers();
        var amount = context.Amount;
        string? consumed = null;
        foreach (var modifier in (_state.HonorModifiers ?? []).ToArray())
        {
            if (amount == 0) break;
            if (modifier.AffectedPlayerId is { } who && who != context.PlayerId) continue;
            if (!HonorModifierQualifies(modifier, context with { Amount = amount })) continue;
            var next = modifier.Kind switch
            {
                HonorModifierKinds.NoGain or HonorModifierKinds.NoLoss => 0,
                _ => modifier.SetTo is { } fixedSize ? Math.Sign(amount) * Math.Max(0, fixedSize) : Math.Sign(amount) * Math.Max(0, Math.Abs(amount) + modifier.Delta),
            };
            if (modifier.Kind is HonorModifierKinds.NextChange or HonorModifierKinds.NextProclaim) { consumed = modifier.Id; amount = next; break; }
            amount = next;
        }
        return amount == context.Amount && consumed is null ? null : new HonorChangeResult(amount, null, consumed);
    }

    private bool HonorModifierQualifies(HonorModifier modifier, HonorChangeContext context)
    {
        var gain = context.Amount > 0;
        switch (modifier.Kind)
        {
            case HonorModifierKinds.NextProclaim: if (!context.IsProclaim || !gain) return false; break;
            case HonorModifierKinds.EachGain or HonorModifierKinds.NoGain: if (!gain) return false; break;
            case HonorModifierKinds.NoLoss: if (gain) return false; break;
            case HonorModifierKinds.NextChange: break;
            default: return false;
        }
        foreach (var filter in modifier.Filters)
        {
            if (filter == HonorModifierFilters.OwnCards) { if (!context.CausedByOwnCard) return false; }
            else if (filter == HonorModifierFilters.NotProclaim) { if (context.IsProclaim) return false; }
            else if (filter == HonorModifierFilters.ActionEffects) { if (context.ActionKey is null || context.IsProclaim) return false; }
            else if (filter == HonorModifierFilters.ActionPhaseOnly) { if (_state.Phase != Phase.Action) return false; }
            else if (filter.StartsWith(HonorModifierFilters.UnlessControlsPrefix, StringComparison.Ordinal)) { if (ControlsKeyword(context.PlayerId, filter[HonorModifierFilters.UnlessControlsPrefix.Length..])) return false; }
            else if (filter.StartsWith(HonorModifierFilters.TargetedByOtherPrefix, StringComparison.Ordinal))
            {
                if (context.SourcePlayerId is null || context.SourcePlayerId == context.PlayerId || context.ActionKey is null
                    || !context.TargetedCardIds.Contains(filter[HonorModifierFilters.TargetedByOtherPrefix.Length..], StringComparer.Ordinal)) return false;
            }
            else return false; // an unknown filter never matches (it would otherwise silently widen the modifier)
        }
        return true;
    }

    internal void GainHonorForTest(int playerId, int amount, string reason, string? sourceCardId, bool proclaim = false)
    {
        var before = _proclaimAction;
        if (proclaim) _proclaimAction = ($"{playerId}|proclaim|test", playerId);
        try { GainHonor(playerId, amount, reason, sourceCardId is null ? null : Card(sourceCardId)); } finally { _proclaimAction = before; }
    }
}
