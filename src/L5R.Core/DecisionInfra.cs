using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// P25 (issue #723): DECISION INFRASTRUCTURE -- four small pieces that remove the need to edit shared files for every new kind of decision. Nothing here is wired to a card.
//
//   1. MULTI-PICK EITHER/OR. PendingEitherOr.Min/Max (omitted from a saved game until used): a decision that collects several of its options. The deciding player is offered the options not
//      yet picked and a "Done" option (key "done"; legal once Min are picked). Each pick is an ordinary "chooseEitherOr" command, so a client that renders the option list needs no change.
//      The picks are APPLIED, in the order picked, when the decision ends (Done, Max reached, or no legal option left), each through the spec's own ApplyEitherOr hook; then one final
//      call is made with the synthetic option "done" (for a spec that acts on the whole set, or on an empty one: PendingEitherOr.Picked holds the keys). Open one with
//      OpenEitherOr(..., min: m, max: n). Options that cost Gold are not supported in a multi-pick decision.
//   2. EngineHooks.PendingActions: the actions a hook-registered pending decision offers, consulted by QueryActions before its chain, so a new pending type needs no QueryActions branch.
//      With the two EffectChoice hooks (BranchEffects: what a branch effect does; EffectChoiceContinuations: a chosen option that opens a further decision) a new decision kind lives in
//      its own feature file. The client treats any pending type it has no dedicated screen for as a generic decision (Main.Decisions.cs).
//   3. TargetSlot.CrossConstraint (TargetSlots.cs): a predicate over the cards chosen for a slot and every earlier card slot, validated after the per-slot checks and reflected in the
//      candidates offered for the slot.
//   4. PENDING SHOWN CHOICE: a decider's choice among cards that were SHOWN (MatchState.ShownCards), named by their position in that record (ShownCard.Sequence) and their definition, never
//      by an instance id (the cards may still be in a deck). The decider is offered one "chooseShown" action per candidate and, for a choice of several, a "done". The client renders the
//      faces from MatchView.ShownCards (Main.ShownChoice.cs).
//
// Everything is public: the options are fixed by cards in play or already shown, and both players see the result.
// ======================================================================================================================================================================================

/// <summary>What a PendingActions hook is asked: who is asking, and the pending decision that is open.</summary>
internal readonly record struct PendingActionsContext(int PlayerId, PendingChoice Choice);

internal static partial class EngineHooks
{
    /// <summary>The actions of an open pending decision. First answer wins: return null when the pending type is not yours, an empty sequence (never null) for a player who has no decision to make.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, PendingActionsContext, IEnumerable<ActionOption>?>> PendingActions = new();
    /// <summary>What a branch effect (EffectOption.Effects) does. Return true when the effect is yours. Called after the built-in effects, with the effect's own command, source and target.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, string, GameCommand, CardInstance, CardInstance?, bool>> BranchEffects = new();
    /// <summary>After a chosen option's effects applied: return true when the option opened a further decision (the action then finishes with that decision, not here).</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, PendingEffectChoice, EffectOption, bool>> EffectChoiceContinuations = new();
    /// <summary>What a decided shown choice does. Return true when the spec is yours. It may open a further decision; otherwise the paused action (if any) finishes.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, PendingShownChoice, bool>> ShownChoiceApply = new();
}

/// <summary>A decider choosing among cards that were shown (MatchState.PendingShownChoice). Candidates are positions in MatchState.ShownCards (ShownCard.Sequence).</summary>
public sealed class PendingShownChoice
{
    public string SpecId { get; set; } = "";
    public string SourceCardId { get; set; } = "";
    public int ActingPlayerId { get; set; }
    public int DeciderPlayerId { get; set; }
    /// <summary>The ShownCard.Sequence of each candidate, in the order shown.</summary>
    public List<int> Candidates { get; set; } = [];
    public int Min { get; set; }
    public int Max { get; set; }
    public List<int> Chosen { get; set; } = [];
    /// <summary>The action to finish once the choice is applied (null when the spec's own apply hook finishes things).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingEffectChoice? Paused { get; set; }
}

public sealed partial class MatchState
{
    // 22700-22799 is reserved for P25. Omitted while unused, so a match that never opened a shown choice serializes exactly as before.
    [JsonPropertyOrder(22700)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingShownChoice? PendingShownChoice { get; set; }
}

public sealed partial class MatchEngine
{
    /// <summary>The key of the synthetic "Done" option of a multi-pick Either/Or.</summary>
    internal const string EitherOrDoneKey = "done";
    internal const string ShownChoiceType = "shownChoice", ShownChoiceCommand = "chooseShown";

    // ---- 2. hook dispatch -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<ActionOption>? RegisteredPendingActions(int playerId)
    {
        if (_state.PendingChoice is not { } choice) return null;
        var context = new PendingActionsContext(playerId, choice);
        foreach (var hook in EngineHooks.PendingActions.Items)
            if (hook(this, context) is { } options) return options;
        return null;
    }

    private bool RegisteredBranchEffect(string effect, GameCommand command, CardInstance source, CardInstance? target)
    {
        foreach (var hook in EngineHooks.BranchEffects.Items)
            if (hook(this, effect, command, source, target)) return true;
        return false;
    }

    private bool RegisteredEffectChoiceContinue(PendingEffectChoice pending, EffectOption option)
    {
        foreach (var hook in EngineHooks.EffectChoiceContinuations.Items)
            if (hook(this, pending, option)) return true;
        return false;
    }

    /// <summary>For a hook-registered pending type: opens it. <paramref name="type"/> is the PendingChoice type string (also what the PendingActions and PendingChoice hooks match on); the decider
    /// holds priority until the type's PendingChoice hook closes it with <see cref="ResumeAfterDecision"/>.</summary>
    internal void OpenGenericChoice(string type, int deciderId, string? cardId, string prompt, int? actingPlayerId = null)
    {
        _state.PendingChoice = new(type, deciderId, cardId, prompt, actingPlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
    }

    /// <summary>For a hook-registered pending type: refuses the command being answered (the engine reports <paramref name="message"/> and leaves the state unchanged).</summary>
    internal static void RefuseCommand(string message) => throw new RuleViolation(message);

    /// <summary>For a hook-registered pending type: closes the open decision and gives the action opportunity on, as the end of an action does.</summary>
    internal void ResumeAfterDecision(int playerId)
    {
        _state.PendingChoice = null;
        NextAction(playerId);
    }

    // ---- 1. multi-pick Either/Or ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsMultiPick(PendingEitherOr p) => p.Max > 0;

    /// <summary>What is on offer now: the spec's options for an ordinary decision; for a multi-pick one, those not yet picked plus "Done".</summary>
    private EitherOrOption[] EitherOrOffered(PendingEitherOr p)
    {
        var all = EitherOrOptions(p);
        if (!IsMultiPick(p)) return all;
        var picked = p.Picked ?? [];
        var open = all.Where(o => !picked.Contains(o.Key)).Select(o => o.GoldCost > 0 ? o with { Legal = false, Reason = "This option cannot be combined with others." } : o).ToList();
        open.Add(new(EitherOrDoneKey, picked.Count == 0 ? "Done (none chosen)" : $"Done ({picked.Count} chosen)",
            picked.Count == 0 ? "Choose none of the options." : "Finish with the options chosen so far.", EitherOrKind.Choose, 0, picked.Count >= p.Min, $"Choose at least {p.Min}."));
        return open.ToArray();
    }

    /// <summary>The words added to a multi-pick decision's prompt (nothing for an ordinary one).</summary>
    private static string MultiPickPromptSuffix(PendingEitherOr p) =>
        !IsMultiPick(p) ? "" : $" Choose {(p.Min == p.Max ? $"{p.Min}" : $"{p.Min} to {p.Max}")} ({(p.Picked?.Count ?? 0)} chosen so far), then Done.";

    /// <summary>OpenEitherOr's check for a multi-pick decision: with nothing to choose beyond what is forced (every legal option is needed to reach Min), the picks are made and applied here
    /// and the decision never opens. True when it resolved itself.</summary>
    private bool AutoResolveMultiPick(PendingEitherOr pending, CardInstance source, int deciderId, EitherOrOption[] options)
    {
        var legal = options.Where(o => o.Legal && o.GoldCost == 0).ToArray();
        if (legal.Length > pending.Min) return false;
        Emit("eitherOrAutoResolved", $"{Definition(source).Name}: {Player(deciderId).Name} has no choice to make ({(legal.Length == 0 ? "nothing to choose" : string.Join(", ", legal.Select(o => o.Label)))}).", deciderId, source.Id);
        pending.Picked = legal.Select(o => o.Key).ToList();
        ApplyMultiPicks(pending);
        return true;
    }

    // One answer to a multi-pick decision: a pick (kept until the decision ends) or "done".
    private void ResolveMultiPickCommand(PendingEitherOr pending, GameCommand command)
    {
        var option = EitherOrOffered(pending).FirstOrDefault(o => o.Key == command.Choice);
        Require(option is not null, "Choose one of the offered options.");
        Require(option!.Legal, option.Reason ?? "That option is not available.");
        Require(command.SourceIds is null or { Length: 0 }, "This option needs no Gold sources.");
        var source = Card(pending.SourceCardId); var decider = Player(pending.DeciderPlayerId);
        if (option.Key != EitherOrDoneKey)
        {
            (pending.Picked ??= []).Add(option.Key);
            Emit("eitherOrPicked", $"{decider.Name} chose {option.Label} ({Definition(source).Name}).", decider.Id, source.Id);
            var more = pending.Picked.Count < pending.Max && EitherOrOffered(pending).Any(o => o.Key != EitherOrDoneKey && o.Legal);
            if (more)
            {
                _state.PendingChoice = _state.PendingChoice! with { Prompt = EitherOrPrompt(pending) + MultiPickPromptSuffix(pending) };
                return;
            }
        }
        else Emit("eitherOrChosen", $"{decider.Name} finished choosing ({Definition(source).Name}).", decider.Id, source.Id);
        ApplyMultiPicks(pending);
        // A pick that hands a further decision to a player (as for any Either/Or) finishes the action when that one is answered.
        if (_state.PendingEitherOr is { } chained && !ReferenceEquals(chained, pending)) return;
        FinishEitherOr(pending);
    }

    // Each pick through the spec's own apply (in the order picked), then one call with the synthetic "done" for a spec that acts on the whole set.
    private void ApplyMultiPicks(PendingEitherOr pending)
    {
        var source = Card(pending.SourceCardId); var decider = Player(pending.DeciderPlayerId);
        var affected = pending.AffectedCardId is null ? null : Card(pending.AffectedCardId);
        var all = EitherOrOptions(pending);
        foreach (var key in pending.Picked ?? [])
        {
            var option = all.First(o => o.Key == key);
            ApplyEitherOr(pending, option, null);
            if (_state.PendingEitherOr is { } chained && !ReferenceEquals(chained, pending)) return;
        }
        var done = new EitherOrOption(EitherOrDoneKey, "Done", "", EitherOrKind.Choose, 0, true, null);
        RegisteredApplyEitherOr(pending, done, source, decider, affected, null);
    }

    // ---- 4. a decider's choice among shown cards -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Hands a choice among <paramref name="candidates"/> (ShownCard.Sequence values) to <paramref name="deciderId"/>. Returns true when it is open (the caller's action waits) and false
    /// when there was nothing to ask (no candidate, or all are needed): the choice was applied at once and the caller finishes the action itself. <paramref name="paused"/> is the action to finish
    /// when the choice is applied; null when the spec's own hook does.</summary>
    internal bool OpenShownChoice(string specId, CardInstance source, int actingPlayerId, int deciderId, IEnumerable<int> candidates, int min, int max, string prompt, PendingEffectChoice? paused = null)
    {
        var list = candidates.ToList();
        Require(list.All(s => _state.ShownCards.Any(c => c.Sequence == s)) && list.Distinct().Count() == list.Count, "A shown choice names cards that were shown.");
        Require(min >= 0 && max >= min && max >= 1, "A shown choice picks at least zero and at most one or more.");
        var pending = new PendingShownChoice { SpecId = specId, SourceCardId = source.Id, ActingPlayerId = actingPlayerId, DeciderPlayerId = deciderId, Candidates = list, Min = min, Max = max, Paused = paused };
        if (list.Count == 0 || min == max && list.Count <= min)
        {
            Emit("deciderAutoResolved", list.Count == 0 ? $"{Definition(source).Name}: {Player(deciderId).Name} has nothing to choose." : $"{Definition(source).Name}: {Player(deciderId).Name} has one choice.", deciderId, source.Id);
            pending.Chosen = list.Take(max).ToList();
            if (!ApplyShownChoice(pending, finish: false)) throw new RuleViolation("Unknown choice.");
            return _state.PendingChoice is not null;
        }
        _state.PendingShownChoice = pending;
        SuspendOpenWindow();
        _state.PendingChoice = new(ShownChoiceType, deciderId, source.Id, prompt, actingPlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        Emit("shownChoiceOffered", $"{Player(deciderId).Name} must choose among the shown cards ({Definition(source).Name}).", deciderId, source.Id);
        return true;
    }

    private string ShownCandidateName(int sequence) => CardCatalog.Get(_state.ShownCards.First(c => c.Sequence == sequence).DefinitionId).Name;

    private void ResolveShownChoiceCommand(GameCommand command)
    {
        var pending = _state.PendingShownChoice ?? throw new RuleViolation("No choice is pending.");
        Require(command.Type == ShownChoiceCommand, "Make the choice.");
        Require(command.PlayerId == pending.DeciderPlayerId, "Only the player who decides may answer.");
        var done = command.Choice == "done";
        if (done) Require(pending.Chosen.Count >= pending.Min, $"Choose at least {pending.Min}.");
        else
        {
            Require(int.TryParse(command.Choice, out var sequence) && pending.Candidates.Contains(sequence) && !pending.Chosen.Contains(sequence), "Choose one of the shown cards offered.");
            Require(pending.Chosen.Count < pending.Max, $"Choose at most {pending.Max}.");
            pending.Chosen.Add(sequence);
            Emit("shownChosen", $"{Player(pending.DeciderPlayerId).Name} chose {ShownCandidateName(sequence)}.", pending.DeciderPlayerId, pending.SourceCardId);
            if (pending.Chosen.Count >= pending.Max || pending.Chosen.Count == pending.Candidates.Count) done = true;
        }
        if (!done) return;
        var savedChoice = _state.PendingChoice;
        _state.PendingChoice = null; _state.PendingShownChoice = null;
        if (!ApplyShownChoice(pending, finish: true)) { _state.PendingChoice = savedChoice; _state.PendingShownChoice = pending; throw new RuleViolation("Unknown choice."); }
    }

    // What a decided choice does, then the paused action finishes unless the apply hook opened a further decision. False when no hook knows the spec.
    private bool ApplyShownChoice(PendingShownChoice pending, bool finish)
    {
        var handled = false;
        foreach (var hook in EngineHooks.ShownChoiceApply.Items)
            if (hook(this, pending)) { handled = true; break; }
        if (!handled) return false;
        if (!finish || _state.PendingChoice is not null) return true;
        if (pending.Paused is { } paused)
            CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore,
                paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
        return true;
    }

    private IEnumerable<ActionOption> ShownChoiceActions(int playerId)
    {
        if (_state.PendingShownChoice is not { } p || p.DeciderPlayerId != playerId) yield break;
        if (p.Chosen.Count < p.Max)
            foreach (var sequence in p.Candidates.Where(c => !p.Chosen.Contains(c)))
                yield return new(ShownChoiceCommand, $"Choose · {ShownCandidateName(sequence)}", true, null,
                    new GameCommand(ShownChoiceCommand, playerId, _state.Version, CardId: p.SourceCardId, Choice: sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)), p.SourceCardId);
        if (p.Chosen.Count >= p.Min && (p.Max > 1 || p.Min == 0))
            yield return new(ShownChoiceCommand, p.Chosen.Count == 0 ? "Choose none" : $"Done ({p.Chosen.Count} chosen)", true, null,
                new GameCommand(ShownChoiceCommand, playerId, _state.Version, CardId: p.SourceCardId, Choice: "done"), p.SourceCardId);
    }

    private bool HandleShownChoice(string type, GameCommand command)
    {
        if (type != ShownChoiceType) return false;
        WithResumedAction(() => ResolveShownChoiceCommand(command));
        return true;
    }

    // ---- test seams (L5R.Core.Tests has InternalsVisibleTo) ---------------------------------------------------------------------------------------------------------------------------------------

    internal bool OpenEitherOrForTest(string specId, string sourceId, int actingPlayerId, int deciderId, string? affectedId, int min = 0, int max = 0) =>
        OpenEitherOr(specId, Card(sourceId), actingPlayerId, deciderId, affectedId is null ? null : Card(affectedId), "action", min: min, max: max);
    internal bool OpenShownChoiceForTest(string specId, string sourceId, int actingPlayerId, int deciderId, int[] candidates, int min, int max) =>
        OpenShownChoice(specId, Card(sourceId), actingPlayerId, deciderId, candidates, min, max, "Training: choose among the shown cards.");
    /// <summary>Appends a shown card (as a search or a look shows one) and returns its position.</summary>
    internal int ShowCardForTest(int playerId, string definitionId, string sourceId)
    {
        var source = Card(sourceId);
        _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, playerId, definitionId, source.Id, source.DefinitionId, "Fate", "test", _state.TurnNumber, _state.Events.Count));
        return _state.ShownCards.Count;
    }
    internal bool SlotSelectionLegalForTest(GameCommand command, string abilityId)
    {
        try { ValidateSlots(command, AbilityCatalog.All[abilityId]); return true; }
        catch (RuleViolation) { return false; }
    }
    internal string? SlotSelectionErrorForTest(GameCommand command, string abilityId)
    {
        try { ValidateSlots(command, AbilityCatalog.All[abilityId]); return null; }
        catch (RuleViolation e) { return e.Message; }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterDecisionInfraHooks()
    {
        EngineHooks.PendingChoice.Register("P25ShownChoice", 16700, static (engine, type, command) => engine.HandleShownChoice(type, command));
        EngineHooks.PendingActions.Register("P25ShownChoiceActions", 16700, static (engine, context) => context.Choice.Type == ShownChoiceType ? engine.ShownChoiceActions(context.PlayerId).ToArray() : null);
        EngineHooks.DecisionState.Register(nameof(MatchState.PendingShownChoice), 16700, static state => state.PendingShownChoice is not null);
        RegisterDecisionInfraShowcase();
    }
}
