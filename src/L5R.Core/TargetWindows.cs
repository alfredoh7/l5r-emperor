namespace L5R.Core;

// One before-action reaction window PER TARGET (Mendon's ruling, issue #336): an action with several targets does not react only to its first.
// Each target that has a possible reactor -- either player's, judged by BeforeActionReactionApplies for THAT target -- gets its own window, in slot
// order; a target nobody could react to opens none. Each window is exactly the alternating window of #318/#327: the player whose turn it is has the
// first opportunity when he holds a reaction for that target (otherwise the other player does), a reaction played resets the pass count and hands the
// opportunity over, and two passes in succession close it. Closing one opens the next; when the last has closed the deferred action resolves once,
// with the negations every window set (MatchState.NegatedEffects composes per card) or negated whole (a reaction that negates the action skips the
// windows still to come: there is nothing left to react to).
//
// Kept apart from CardAbilities.cs on purpose: it is the only place that knows about more than one window, so the "targeted by an action" plumbing
// (BeforeActionReactionApplies and the reactions' own effects) reads ReactionAffectedId instead of command.TargetId and nothing else changes.
public sealed partial class MatchEngine
{
    // The cards the windows are opened for, in order: every card of every up-front card slot of a multi-target action (slot order, then the order
    // chosen within a slot), otherwise the command's single target. A follow-up slot names no card and a player slot no card, so neither has a window.
    private static string[] ReactionWindowTargets(GameCommand command)
    {
        if (command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Slots is not null)
            return ability.Slots.Where(s => !s.IsPlayer && !s.AtResolution)
                .SelectMany(s => command.Selections?.FirstOrDefault(x => x.Slot == s.Id)?.CardIds ?? []).Distinct().ToArray();
        return command.TargetId is null ? [] : [command.TargetId];
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
            foreach (var id in ReactionWindowTargets(command)) yield return id;
    }

    // Opens the window of the first of `targets` that has a possible reactor; the ones after it wait in MatchState.ReactionWindowRemaining. False when
    // none has (the action then resolves at once, exactly as an action nobody can react to always did).
    private bool OpenNextTargetReactionWindow(GameCommand command, IReadOnlyList<string> targets)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (!OwnsBeforeActionReaction(0, command, target) && !OwnsBeforeActionReaction(1, command, target)) continue;
            _state.PendingNegatableAbility = command;
            _state.ReactionWindowTargetId = target; _state.ReactionWindowRemaining = targets.Skip(i + 1).ToList();
            var several = ReactionWindowTargets(command).Length > 1;
            OpenAlternatingReactionWindow(new("beforeActionReaction", command.PlayerId, several ? target : null,
                several ? $"Before this action resolves, for {Definition(Card(target)).Name}: either player may use a reaction, or pass." : "Before this action resolves: either player may use a reaction, or pass."),
                FirstReactionOpportunity(p => OwnsBeforeActionReaction(p, command, target)), "Before-action reaction window opened.");
            return true;
        }
        // Nothing is reset here: a Reaction played INSIDE an open window passes through this too (no target, so no window), and must not clear the
        // open window's target. The action's resolution clears it (Apply's beforeActionReaction handler).
        return false;
    }

    // Called when a window has just closed: the next target's window, if any target still to come has a possible reactor.
    private bool OpenNextRemainingTargetWindow() =>
        _state.PendingNegatableAbility is { } deferred && _state.ReactionWindowRemaining.Count > 0 && OpenNextTargetReactionWindow(deferred, _state.ReactionWindowRemaining.ToList());
}
