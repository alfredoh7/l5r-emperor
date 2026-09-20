namespace L5R.Core;

// One before-action reaction window PER TARGET (Mendon's ruling, issue #336): an action with several targets does not react only to its first.
// Each target that has a possible reactor -- either player's, judged for THAT target by BeforeActionReactionApplies (the curated before-action
// reactions) and by TargetedActionReactionApplies (the "targeted by an action" reactions of #335: redirect, re-check, negate) -- gets its own window, in slot
// order; a target nobody could react to opens none. Each window is exactly the alternating window of #318/#327: the player whose turn it is has the
// first opportunity when he holds a reaction for that target (otherwise the other player does), a reaction played resets the pass count and hands the
// opportunity over, and two passes in succession close it. Closing one opens the next; when the last has closed the deferred action resolves once,
// with the negations every window set (MatchState.NegatedEffects composes per card) or negated whole (a reaction that negates the action skips the
// windows still to come: there is nothing left to react to).
//
// Kept apart from CardAbilities.cs on purpose: it is the only place that knows about more than one window, so the "targeted by an action" plumbing
// (BeforeActionReactionApplies, TargetedActionReactionApplies and the reactions' own effects) reads ReactionAffectedId / the window's subject instead of
// command.TargetId and nothing else changes. A single-target action is the same list of one, and an action that names no card is a list of one "no target"
// (a Mochitoko-protected cost may still open its window, exactly as before).
public sealed partial class MatchEngine
{
    // The cards the windows are opened for, in order: every card of every up-front card slot of a multi-target action (slot order, then the order
    // chosen within a slot), otherwise the command's single target (null when it names no card). A follow-up slot names no card and a player slot no card,
    // so neither has a window.
    private static string?[] ReactionWindowTargets(GameCommand command)
    {
        if (command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Slots is not null)
        {
            var ids = ability.Slots.Where(s => !s.IsPlayer && !s.AtResolution)
                .SelectMany(s => command.Selections?.FirstOrDefault(x => x.Slot == s.Id)?.CardIds ?? []).Distinct().ToArray();
            return ids.Length == 0 ? [null] : ids.Cast<string?>().ToArray();
        }
        return [command.TargetId];
    }

    // The card the reaction being played or listed now is about: the open window's target, else the command's own.
    private string? ReactionAffectedId(GameCommand command) => _state.ReactionWindowTargetId ?? command.TargetId;

    // The cards the deferred action would move (its MoveSpec's movers), for a reaction that names one of them.
    private IEnumerable<string> ActionMovers(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Move is not { } move) yield break;
        if (move.Movers is MoveSpec.Source or MoveSpec.SourceAndTarget && command.CardId is not null) yield return command.CardId;
        if (move.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget && command.PerformerId is not null) yield return command.PerformerId;
        if (move.Movers is MoveSpec.Target or MoveSpec.SourceAndTarget or MoveSpec.PerformerAndTarget)
            foreach (var id in ReactionWindowTargets(command).OfType<string>()) yield return id;
    }

    // Opens the window of the first of `targets` that has a possible reactor; the ones after it wait in MatchState.ReactionWindowRemaining. False when
    // none has (the action then resolves at once, exactly as an action nobody can react to always did). Called for the announcement of an action
    // (ResolveOrDeferBeforeActionReaction) and again each time a window closes (OpenNextRemainingTargetWindow).
    private bool OpenNextTargetReactionWindow(GameCommand command, IReadOnlyList<string?> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            // Judged for THIS target explicitly ("" is "no card": never the open window's, which a Reaction played inside one would otherwise inherit).
            var subject = SubjectForWindowTarget(command, target);
            bool Owns(int playerId) => OwnsBeforeActionReaction(playerId, command, target ?? "") || OwnsTargetedActionReaction(playerId, subject);
            if (!Owns(0) && !Owns(1)) continue;
            // A Reaction that is itself an action suspends the window it was played in (ReactionsAreActions.cs), window state included.
            SuspendOpenWindow();
            _state.PendingNegatableAbility = command;
            _state.ReactionWindowTargetId = target; _state.ReactionWindowRemaining = targets.Skip(i + 1).OfType<string>().ToList();
            var several = ReactionWindowTargets(command).Length > 1;
            OpenAlternatingReactionWindow(new("beforeActionReaction", command.PlayerId, several ? target : null,
                several ? $"Before this action resolves, for {Definition(Card(target!)).Name}: either player may use a reaction, or pass." : "Before this action resolves: either player may use a reaction, or pass."),
                FirstReactionOpportunity(Owns), "Before-action reaction window opened.");
            return true;
        }
        // Nothing is reset here: a Reaction played INSIDE an open window passes through this too (no target, so no window), and must not clear the
        // open window's target. The action's resolution clears it (Apply's beforeActionReaction handler).
        return false;
    }

    // Called when a window has just closed: the next target's window, if any target still to come has a possible reactor.
    private bool OpenNextRemainingTargetWindow() =>
        _state.PendingNegatableAbility is { } deferred && _state.ReactionWindowRemaining.Count > 0 && OpenNextTargetReactionWindow(deferred, _state.ReactionWindowRemaining.Cast<string?>().ToList());

    // ---- redirect and re-check on a multi-target action (R's #335 primitives, issue #336) -----------------------------------------------------

    private AbilityDefinition? SlotActionOf(ReactionSubject subject) =>
        !subject.IsAttack && subject.Command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Slots is not null ? ability : null;

    // The slot that holds `cardId` in the command's selection.
    private static TargetSlot? SlotHolding(AbilityDefinition ability, GameCommand command, string cardId) =>
        ability.Slots!.FirstOrDefault(s => !s.IsPlayer && !s.AtResolution && command.Selections?.FirstOrDefault(x => x.Slot == s.Id)?.CardIds?.Contains(cardId) == true);

    /// <summary>Would this card be a legal target in the SLOT of the window's target? Judged for that slot alone (the other slots' cards are not this
    /// window's business): the slot's category, and not a card the slot already holds. The window's own target is always the same slot's, so a re-check
    /// (Yurishi's) asks the same question as it does of a single-target action.</summary>
    private bool SlotActionAcceptsTarget(ReactionSubject subject, AbilityDefinition ability, string targetId)
    {
        if (subject.TargetId is null || SlotHolding(ability, subject.Command, subject.TargetId) is not { } slot) return false;
        var held = subject.Command.Selections!.First(x => x.Slot == slot.Id).CardIds!;
        if (targetId != subject.TargetId && held.Contains(targetId)) return false;
        try { ValidateSlotCard(subject.Command, ability, slot, targetId); return true; }
        catch (RuleViolation) { return false; }
    }

    // The same command with the window's target replaced, in its slot and in the primary TargetId the single-target readers see.
    private static GameCommand RetargetSlotAction(GameCommand command, string oldTargetId, string newTargetId)
    {
        var selections = command.Selections!.Select(s => s.CardIds is { } ids && ids.Contains(oldTargetId) ? s with { CardIds = ids.Select(id => id == oldTargetId ? newTargetId : id).ToArray() } : s).ToArray();
        return NormalizeSlotCommand(command with { TargetId = null, Selections = selections });
    }

    // A redirect names another card for the window's target: the window is now about that card (the ones still to come are unchanged).
    private void FollowRedirectInWindow(string oldTargetId, string newTargetId)
    {
        if (_state.ReactionWindowTargetId == oldTargetId) _state.ReactionWindowTargetId = newTargetId;
        if (_state.PendingChoice is { Type: "beforeActionReaction", CardId: { } named } window && named == oldTargetId)
            _state.PendingChoice = window with { CardId = newTargetId, Prompt = $"Before this action resolves, for {Definition(Card(newTargetId)).Name}: either player may use a reaction, or pass." };
    }
}
