namespace L5R.Core;

// Whole-action negation (issue #338, Yoritomo Sakuma: "negate the action's (remaining) effects"). This is the minimal, isolated form of
// the general "negate an action's effects" a reaction to a before-action window can ask for; the narrower, per-card negations
// (Champion of Thunder's "all" on one card, Rokai's destroy, Tamoko's move) live in MatchState.NegatedEffects and are unchanged.
//
// The deferred action (MatchState.PendingNegatableAbility) has announced but not resolved. Negating it does not stop it from having been
// TAKEN: what it costs is still paid (Mendon's ruling, issue #351: a negated action pays ALL its costs unless the card says otherwise), its use is
// still spent (once per turn), a Strategy still leaves the hand, an Additional Action grant is still consumed or forfeited, and priority still
// passes -- only its effects do not happen, whichever they were and whoever they reached (bow, move, destroy, Force, Honor, a draw, a choice for
// another player...). Every cost is paid here, in ONE place: the Gold cost (PayForAbility), a chosen CostOption, the Imperial Favor discard, and
// the ability's declared Costs (AbilityCosts.cs: bow, discard, remove from the game, sacrifice, destroy a token, return to a deck, lose Honor) --
// the same list RunAbilityEffects pays for an action that is not negated. A cost an ability still pays inside its own effects (the few the
// decision document lists by id, whose cost is conditional or feeds its effect) is not paid when negated. Payment is ATOMIC: see below.
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

    // The deferred action, resolved negated: the taking of the action without its effects (see the file comment). Costs are paid in full and ATOMICALLY
    // (Mendon, AbilityCosts.cs): when any cost payment is blocked by a negation (CostPaymentBlockedByNegation) the ENTIRE action is negated -- nothing is
    // paid, not even an Honor-loss cost -- and otherwise every cost is paid, an Honor-loss cost included, before the (skipped) effects. `costsBlocked` is
    // true when the caller has already found a blocked cost (RunAbilityEffects); `consumedGrant`: the action was taken as an Additional Action.
    private void ResolveNegatedAction(GameCommand command, bool costsBlocked = false, bool consumedGrant = false)
    {
        var id = command.AbilityId!; var ability = AbilityCatalog.All[id];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        (source?.AbilityUses ?? player.AbilityUses)[ability.UsageKey ?? id] = _state.TurnNumber;
        var playedFromDiscard = PlayedFromDiscard(ability, source);
        costsBlocked = costsBlocked || AnyCostBlocked(ability, command, source);
        if (!costsBlocked)
        {
            PayForAbility(command, player);
            PayCostOption(ability, command, player, source);
            if (id is "shunya-honor" or "favor-cycle" or "favor-battle" or "jakuei-bow-favor")
            {
                _state.FavorPlayerId = null;
                Emit("favorDiscarded", $"{player.Name} discarded the Imperial Favor.", player.Id);
            }
            PayAbilityCosts(ability, command, player, source);
        }
        Emit("abilityUsed", $"{player.Name}: {(source is null ? "Imperial Favor" : Definition(source).Name)} — {ability.Label} ({(costsBlocked ? "negated: a cost could not be paid, so nothing was paid" : "negated")}).", player.Id, source?.Id);
        SpendHandPlayedCard(player, source);
        if (playedFromDiscard) RemoveFromGame(source!);
        if (!consumedGrant) _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id);
        if (OpenOverconfidentDrawWindow(1 - player.Id)) return;
        NextAction(player.Id, _state.AdditionalActions.Any(g => g.PlayerId == player.Id));
    }
}
