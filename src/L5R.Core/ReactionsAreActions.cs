namespace L5R.Core;

// "Reactions count as actions" (rules lead, 2026-09-19; docs/decisions/2026-09-19-battle-rulings-round-2.md, ruling 3). IsAction is the one
// classification. Being an action does not change how a Reaction takes its turn: it is played in a reaction window and keeps that window's
// own alternation. A window a Reaction opens (a before-action window, a forced discard, Unimpeachable Name, an "after this action" window)
// suspends the one the Reaction was played in (MatchState.SuspendedWindows) and gives it back, as it was, when it is done.
public sealed partial class MatchEngine
{
    internal static bool IsAction(AbilityDefinition ability) => ability.Timing is "Limited" or "Open" or "Battle" or "BattleOrOpen" or "Reaction";

    // Additional Actions (rules lead, 2026-09-19, docs/decisions/2026-09-19-battle-rulings-round-2.md ruling 2): each action of a burst is atomic and
    // independent, and a Reaction played meanwhile only interrupts it. An ordinary action that is not itself one of the burst's additional actions
    // ends the burst (its unspent additional actions are forfeited); a Reaction never does, whatever it resolves or is negated as. What does end a
    // burst is the negation of the action that CREATED the additional actions, and that needs no forfeiture here: a negated action does not resolve,
    // so the additional actions it would have granted are never created.
    internal static bool ForfeitsAdditionalActions(AbilityDefinition ability) => ability.Timing != "Reaction";

    private bool Unspent(CardInstance card, string abilityId)
    {
        var ability = AbilityCatalog.All[abilityId];
        return !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId);
    }

    private void SuspendOpenWindow()
    {
        if (_state.PendingChoice is not { } open) return;
        _state.SuspendedWindows.Add(new SuspendedWindow
        {
            Choice = open,
            ReactingPlayerId = _state.PriorityPlayerId,
            ConsecutivePasses = _state.ConsecutivePasses,
            NegatableAbility = _state.PendingNegatableAbility,
            ActionNegated = _state.DeferredActionNegated,
            NegatedEffects = _state.NegatedEffects,
            OutOfPlayActionReaction = _state.PendingOutOfPlayActionReaction,
            HonorLossReductionPlayerId = _state.HonorLossReductionPlayerId,
            ForcedDiscardContinuation = _state.ForcedDiscardContinuation,
            ForcedDiscardResume = _state.ForcedDiscardResume,
            BowReactionCandidateIds = _state.BowReactionCandidateIds,
            MoveReactionTargetId = _state.MoveReactionTargetId,
            NikutaiReactionTargetId = _state.NikutaiReactionTargetId,
            ReactionWindowTargetId = _state.ReactionWindowTargetId,
            ReactionWindowRemaining = _state.ReactionWindowRemaining,
            WatcherQueue = _state.WatcherQueue,
        });
        _state.PendingChoice = null;
        _state.PendingNegatableAbility = null; _state.DeferredActionNegated = false; _state.NegatedEffects = [];
        _state.PendingOutOfPlayActionReaction = null; _state.HonorLossReductionPlayerId = null; _state.ForcedDiscardContinuation = null; _state.ForcedDiscardResume = null;
        _state.BowReactionCandidateIds = []; _state.MoveReactionTargetId = null; _state.NikutaiReactionTargetId = null;
        _state.ReactionWindowTargetId = null; _state.ReactionWindowRemaining = []; _state.WatcherQueue = [];
    }

    private SuspendedWindow RestoreSuspendedWindow(bool handOn)
    {
        var window = _state.SuspendedWindows[^1];
        _state.SuspendedWindows.RemoveAt(_state.SuspendedWindows.Count - 1);
        _state.PendingChoice = window.Choice;
        _state.PendingNegatableAbility = window.NegatableAbility; _state.DeferredActionNegated = window.ActionNegated; _state.NegatedEffects = window.NegatedEffects;
        _state.PendingOutOfPlayActionReaction = window.OutOfPlayActionReaction; _state.HonorLossReductionPlayerId = window.HonorLossReductionPlayerId;
        _state.ForcedDiscardContinuation = window.ForcedDiscardContinuation; _state.ForcedDiscardResume = window.ForcedDiscardResume; _state.BowReactionCandidateIds = window.BowReactionCandidateIds;
        _state.MoveReactionTargetId = window.MoveReactionTargetId; _state.NikutaiReactionTargetId = window.NikutaiReactionTargetId;
        _state.ReactionWindowTargetId = window.ReactionWindowTargetId; _state.ReactionWindowRemaining = window.ReactionWindowRemaining; _state.WatcherQueue = window.WatcherQueue;
        if (window.LastResolvedAction is not null) _state.LastResolvedAction = window.LastResolvedAction; // issue #898: the action this window is about, not the Reaction that was just played in it
        _state.ConsecutivePasses = handOn ? 0 : window.ConsecutivePasses;
        _state.PriorityPlayerId = handOn ? 1 - window.ReactingPlayerId : window.ReactingPlayerId;
        return window;
    }

    // A Reaction that waited behind a window of its own carries on inside the window it was played in.
    private SuspendedWindow? ContinueSuspendedReaction(GameCommand continued)
    {
        if (_state.SuspendedWindows.Count == 0 || _state.PendingChoice is not null || !AbilityCatalog.All.TryGetValue(continued.AbilityId ?? "", out var ability) || ability.Timing != "Reaction") return null;
        return RestoreSuspendedWindow(handOn: false);
    }

    // Set while a Reaction that waited behind a window of its own resolves inside the window it was played in (ResolveDeferredActionInItsWindow): what it negates of the held action.
    private List<NegatedEffect>? _reactionNegationSink;
    private bool _reactionNegatedHeldAction;

    // The deferred action's resolution (ResolveDeferredAction), inside the window a Reaction was played in when it is one: with the negations of the window that
    // just closed, and the window's own given back afterwards.
    private void ResolveDeferredActionInItsWindow(GameCommand deferred)
    {
        var negated = _state.DeferredActionNegated; var negations = _state.NegatedEffects;
        var outer = ContinueSuspendedReaction(deferred);
        _state.DeferredActionNegated = negated; _state.NegatedEffects = negations;
        var suspended = _state.SuspendedWindows.Count;
        // What the Reaction itself negates (the whole held action, or some of its effects) belongs to the window it was played in: a window held open against the Reaction
        // (Inexorable declined, a counter-reaction passed) must not give that window back as it was before the Reaction resolved (issue #833). NegateDeferredAction and
        // NegateEffects report into this sink while the Reaction resolves.
        var (priorSink, priorHeld) = (_reactionNegationSink, _reactionNegatedHeldAction);
        _reactionNegationSink = outer is null ? null : []; _reactionNegatedHeldAction = false;
        ResolveDeferredAction(deferred);
        var (sink, heldNegated) = (_reactionNegationSink, _reactionNegatedHeldAction);
        (_reactionNegationSink, _reactionNegatedHeldAction) = (priorSink, priorHeld);
        if (_state.SuspendedWindows.Count > suspended) { _state.SuspendedWindows[^1].ActionNegated = outer?.ActionNegated ?? false; _state.SuspendedWindows[^1].NegatedEffects = outer?.NegatedEffects ?? []; }
        else
        {
            _state.DeferredActionNegated = (outer?.ActionNegated ?? false) || heldNegated;
            _state.NegatedEffects = [.. outer?.NegatedEffects ?? [], .. sink ?? []];
        }
    }

    // Nothing the Reaction started is waiting any more: its window is open again, the opportunity handed to the other player.
    private bool ResumeIdleSuspendedWindow()
    {
        if (_state.SuspendedWindows.Count == 0 || _state.PendingChoice is not null || _state.PendingDuel is not null || _state.WinnerId is not null) return false;
        RestoreSuspendedWindow(handOn: true);
        return true;
    }
}
