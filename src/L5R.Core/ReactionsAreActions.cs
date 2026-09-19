namespace L5R.Core;

// "Reactions count as actions" (rules lead, 2026-09-19; docs/decisions/2026-09-19-battle-rulings-round-2.md, ruling 3). IsAction is the one
// classification. Being an action does not change how a Reaction takes its turn: it is played in a reaction window and keeps that window's
// own alternation. A window a Reaction opens (a before-action window, a forced discard, Unimpeachable Name, an "after this action" window)
// suspends the one the Reaction was played in (MatchState.SuspendedWindows) and gives it back, as it was, when it is done.
public sealed partial class MatchEngine
{
    internal static bool IsAction(AbilityDefinition ability) => ability.Timing is "Limited" or "Open" or "Battle" or "BattleOrOpen" or "Reaction";

    private bool Unspent(CardInstance card, string abilityId) =>
        !card.AbilityUses.TryGetValue(AbilityCatalog.All[abilityId].UsageKey ?? abilityId, out var used) || used != _state.TurnNumber;

    private void SuspendOpenWindow()
    {
        if (_state.PendingChoice is not { } open) return;
        _state.SuspendedWindows.Add(new SuspendedWindow
        {
            Choice = open, ReactingPlayerId = _state.PriorityPlayerId, ConsecutivePasses = _state.ConsecutivePasses,
            NegatableAbility = _state.PendingNegatableAbility, ActionNegated = _state.DeferredActionNegated, NegatedEffects = _state.NegatedEffects,
            OutOfPlayActionReaction = _state.PendingOutOfPlayActionReaction, HonorLossReductionPlayerId = _state.HonorLossReductionPlayerId,
            ForcedDiscardContinuation = _state.ForcedDiscardContinuation, BowReactionCandidateIds = _state.BowReactionCandidateIds,
            MoveReactionTargetId = _state.MoveReactionTargetId, NikutaiReactionTargetId = _state.NikutaiReactionTargetId,
        });
        _state.PendingChoice = null;
        _state.PendingNegatableAbility = null; _state.DeferredActionNegated = false; _state.NegatedEffects = [];
        _state.PendingOutOfPlayActionReaction = null; _state.HonorLossReductionPlayerId = null; _state.ForcedDiscardContinuation = null;
        _state.BowReactionCandidateIds = []; _state.MoveReactionTargetId = null; _state.NikutaiReactionTargetId = null;
    }

    private SuspendedWindow RestoreSuspendedWindow(bool handOn)
    {
        var window = _state.SuspendedWindows[^1];
        _state.SuspendedWindows.RemoveAt(_state.SuspendedWindows.Count - 1);
        _state.PendingChoice = window.Choice;
        _state.PendingNegatableAbility = window.NegatableAbility; _state.DeferredActionNegated = window.ActionNegated; _state.NegatedEffects = window.NegatedEffects;
        _state.PendingOutOfPlayActionReaction = window.OutOfPlayActionReaction; _state.HonorLossReductionPlayerId = window.HonorLossReductionPlayerId;
        _state.ForcedDiscardContinuation = window.ForcedDiscardContinuation; _state.BowReactionCandidateIds = window.BowReactionCandidateIds;
        _state.MoveReactionTargetId = window.MoveReactionTargetId; _state.NikutaiReactionTargetId = window.NikutaiReactionTargetId;
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

    // Nothing the Reaction started is waiting any more: its window is open again, the opportunity handed to the other player.
    private void ResumeIdleSuspendedWindow()
    {
        if (_state.SuspendedWindows.Count == 0 || _state.PendingChoice is not null || _state.PendingDuel is not null || _state.WinnerId is not null) return;
        RestoreSuspendedWindow(handOn: true);
    }
}
