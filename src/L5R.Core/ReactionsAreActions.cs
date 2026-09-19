namespace L5R.Core;

// "Reactions count as actions (they follow the same flow in the rulebook)" -- the rules lead's ruling, and the repo's own rules reference
// (docs/rules/EMPEROR_EDITION_CORE_RULES.md, "When can an action be taken?": Limited, Open, Battle and Reaction actions; Coils of Madness:
// Engage plays exactly like a Reaction, Interrupt plays exactly like a Reaction to "Before an action's effects resolve"). So everything
// the engine asks of "an action" -- what an action that targets triggers, what a first-action claim or a negation applies to, what an
// "after the resolution of your [keyword] action" trait or a once-per-phase trait watches -- is asked of a Reaction too, through the ONE
// predicate below.
//
// What being an action does NOT change: how a Reaction takes its turn. A Reaction is played inside a reaction window and keeps that
// window's own priority behaviour (a play hands the opportunity to the other player and resets the pass count, two consecutive passes
// close it; it neither consumes nor is a pass of the Battle-action alternation). Actions that print no designator a player can use are not
// actions: a trait (mandatory, automatic), a Focus Effect, a before-resolution trait.
//
// Because a Reaction is an action, another Reaction (or a trait) may respond to it -- and a Reaction is only ever played inside a window
// that is itself waiting. PendingChoice holds one window, so a window that opens while a Reaction is being announced or has just resolved
// suspends the one the Reaction was played in (SuspendedWindows) and gives it back, exactly as it was, when the new one has been dealt with.
public sealed partial class MatchEngine
{
    // The one classification of "is this an action?" for an ability: anything a player takes by using it. (A Reaction is; a trait or a Focus
    // Effect is not.) Where an ability is the thing being asked about, ask this -- never Timing == "Reaction".
    private static bool IsAction(AbilityDefinition ability) => ability.Timing is "Limited" or "Open" or "Battle" or "BattleOrOpen" or "Reaction";

    private static bool IsAction(GameCommand command) =>
        command.Type == "useAbility" && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && IsAction(ability);

    // Whether a watching card's own once-per-turn use of an ability is still available. An "after this action" window opens only for a watcher
    // that could still use its reaction (as the before-action and Engage windows already do): a Reaction that has resolved is an action too,
    // and would otherwise reopen the very window it was played in, for a card that has just spent its use.
    private bool Unspent(CardInstance card, string abilityId) =>
        !card.AbilityUses.TryGetValue(AbilityCatalog.All[abilityId].UsageKey ?? abilityId, out var used) || used != _state.TurnNumber;

    // A window is about to open while another is still waiting (a Reaction was played inside it): set the waiting one aside, together with
    // the transient state that belongs to it, and leave PendingChoice free for the new one.
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

    // The most recently suspended window is open again, as it was. A Reaction that was announced inside it and is still to carry on
    // (ContinueSuspendedReaction) finds it at the priority it had; one that has already resolved (ResumeIdleSuspendedWindow) hands the
    // opportunity on, the same as a Reaction played in the window directly.
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

    // A Reaction that was set aside behind a window of its own (a before-action window, a forced discard, Unimpeachable Name) carries on now:
    // it does so inside the window it was played in, so every effect reads the same window it would have read had nothing intervened.
    private SuspendedWindow? ContinueSuspendedReaction(GameCommand continued)
    {
        if (_state.SuspendedWindows.Count == 0 || _state.PendingChoice is not null || !AbilityCatalog.All.TryGetValue(continued.AbilityId ?? "", out var ability) || ability.Timing != "Reaction") return null;
        return RestoreSuspendedWindow(handOn: false);
    }

    // Apply()'s last step: whatever a Reaction opened has been dealt with and nothing else is waiting, so the window it was played in is open
    // again, with the opportunity handed to the other player.
    private void ResumeIdleSuspendedWindow()
    {
        if (_state.SuspendedWindows.Count == 0 || _state.PendingChoice is not null || _state.PendingDuel is not null || _state.WinnerId is not null) return;
        RestoreSuspendedWindow(handOn: true);
    }
}
