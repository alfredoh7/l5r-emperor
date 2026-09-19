namespace L5R.Core;

// Whole-action negation (issue #338, Yoritomo Sakuma: "negate the action's (remaining) effects"). This is the minimal, isolated form of
// the general "negate an action's effects" a reaction to a before-action window can ask for; the narrower, per-card negations
// (Champion of Thunder's "all" on one card, Rokai's destroy, Tamoko's move) live in MatchState.NegatedEffects and are unchanged.
//
// The deferred action (MatchState.PendingNegatableAbility) has announced but not resolved. Negating it does not stop it from having been
// TAKEN: what it costs is still paid, its use is still spent (once per turn), a Strategy still leaves the hand, an Additional Action grant
// is still consumed or forfeited, and priority still passes -- only its effects do not happen, whichever they were and whoever they reached
// (bow, move, destroy, Force, Honor, a draw, a choice for another player...). Everything the engine charges GENERICALLY is paid: the Gold cost
// (PayForAbility) and the Imperial Favor discard the Favor actions print as their cost. A cost that an ability's own execution case pays
// itself -- bowing its source or performer, discarding a hand card -- is NOT paid, because those live inside the effects the negation skips:
// see the decision document for the recorded limit and the alternative reading (every cost is paid, which needs a cost/effect split of every
// ability).
public sealed partial class MatchEngine
{
    // Marks the deferred action as negated; it is resolved that way when its reaction window closes (the "beforeActionReaction" handler in
    // Apply). Protection (issue #212): an action performed by a Protected card cannot be negated, the same "still legal to play, its own
    // effect just does nothing" shape StalwartNegatesBow and effortless-counterattack-negate already use.
    private void NegateDeferredAction(CardInstance negatingCard)
    {
        if (_state.PendingNegatableAbility is not { } deferred) return;
        if (Protected(ActionPerformer(deferred)))
        {
            Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", negatingCard.OwnerId, negatingCard.Id);
            return;
        }
        _state.DeferredActionNegated = true;
        Emit("actionNegated", $"{Definition(negatingCard).Name} negates {Player(deferred.PlayerId).Name}'s action ({AbilityCatalog.All[deferred.AbilityId!].Label}).", negatingCard.OwnerId, negatingCard.Id);
    }

    // The deferred action, resolved negated: the taking of the action without its effects (see the file comment). costBlocked (issue #352) is the
    // reason a cost of the action was itself blocked by a negation (CostPaymentBlockedByNegation): the whole action is then negated with none of its
    // costs paid -- "costs of the action that were not yet paid are not paid" -- because the blocked cost stops it before anything is charged. A
    // negated action whose costs can all be paid still keeps them, including the non-Gold cost option (PayCostOption).
    private void ResolveNegatedAction(GameCommand command, string? costBlocked = null)
    {
        var id = command.AbilityId!; var ability = AbilityCatalog.All[id];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        (source?.AbilityUses ?? player.AbilityUses)[ability.UsageKey ?? id] = _state.TurnNumber;
        var playedFromDiscard = PlayedFromDiscard(ability, source);
        costBlocked ??= CostPaymentBlockedByNegation(ability, command, source);
        if (costBlocked is null)
        {
            // A cost the action's own execution pays is not paid; the ones the engine charges generically are, and the non-Gold cost option is one.
            // A blocked cost is not (see ResolveNegatedAction's comment). The order -- Gold first -- is the engine's, not a printed one.
            PayForAbility(command, player);
            PayCostOption(ability, command, player, source);
            if (id is "shunya-honor" or "favor-cycle" or "favor-battle" or "jakuei-bow-favor")
            {
                _state.FavorPlayerId = null;
                Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
            }
        }
        else Emit("costBlocked", $"{player.Name}: {costBlocked}, so the action's costs are not paid.", player.Id, source?.Id);
        Emit("abilityUsed", $"{player.Name}: {(source is null ? "Imperial Favor" : Definition(source).Name)} — {ability.Label} (negated).", player.Id, source?.Id);
        SpendHandPlayedCard(player, source);
        if (playedFromDiscard) RemoveFromGame(source!);
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id);
        if (OpenOverconfidentDrawWindow(1 - player.Id)) return;
        NextAction(player.Id);
    }
}
