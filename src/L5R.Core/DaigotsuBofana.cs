namespace L5R.Core;

// Daigotsu Bofana (oracle-l5r-1706) and Daigotsu Bofana - exp (oracle-l5r-10315), issue #333. Every reading below is recorded, with
// the alternative, in docs/decisions/2026-09-19-daigotsu-bofana.md.
public sealed partial class MatchEngine
{
    private const string BofanaTargetedDiscardEffect = "bofana-targeted-discard";
    private const string BofanaEntryDiscardEffect = "bofana-entry-discard";

    // "After the first time each phase another player's action targets Bofana: If the action was not performed by a card with
    // (current) Force equal to or higher than Bofana's base Force, the player discards a card." No "Reaction:" label, so a
    // TRAIT: mandatory, usable while bowed, and (core rules) resolving before any Reaction to the same trigger -- so it is
    // fired here, when the action is announced and before every reaction window (AnnounceAbility). The trait's own limit is
    // once per PHASE per Bofana, replacing the default once per turn; the FIRST targeting of a phase spends it whether or not
    // the Force condition then holds (the condition is the effect's, not the trigger's). The player (the one whose action
    // it was) chooses the card. Returns true when a discard was opened; the announced action carries on once it is answered.
    //
    // The trait is asked ONCE PER ACTION per Bofana (issue #335, Mendon): retargeting is not a new action, only a change of target before the
    // effect resolves. MatchState.AnnounceTraitEvaluated lists the Bofanas the current action has been judged for (reset when the next action is
    // announced). An action redirected onto a Bofana that was not yet judged is judged when its reaction window closes (resume "action" for a
    // deferred action, "attack" for an Attack), before it resolves -- the window holds no second decision -- and the action then carries on; one
    // redirected away had already been judged at announcement and is not judged again, however many redirects there are.
    private bool OpenForcedDiscardForTargetedTrait(GameCommand command, string? resume = null)
    {
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId ?? "", out var ability) || !IsAction(ability)) return false;
        if (resume is null) _state.AnnounceTraitEvaluated.Clear();
        var targeted = ActionTargetIds(command).ToArray();
        if (targeted.Length == 0) return false;
        foreach (var bofana in _state.Cards.Values.Where(c => targeted.Contains(c.Id) && c.OwnerId != command.PlayerId && InPlay(c) && Definition(c).Effect == BofanaTargetedDiscardEffect)
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (_state.AnnounceTraitEvaluated.Contains(bofana.Id)) continue;
            _state.AnnounceTraitEvaluated.Add(bofana.Id);
            if (!TrySpendPhaseUse(bofana.AbilityUses, BofanaTargetedDiscardEffect)) continue;
            if (PerformingCardForce(command) >= Definition(bofana).Force) continue;
            if (!Player(command.PlayerId).Hand.Any(id => !CommandNamesCard(command, id))) continue;
            SuspendOpenWindow();
            _state.ForcedDiscardContinuation = command; _state.ForcedDiscardResume = resume;
            _state.PendingChoice = new("forcedDiscard", command.PlayerId, bofana.Id, $"{Definition(bofana).Name}: discard a card.");
            _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
            Emit("forcedDiscardOffered", $"{Player(command.PlayerId).Name} must discard a card ({Definition(bofana).Name}).", command.PlayerId, bofana.Id);
            return true;
        }
        return false;
    }

    // The cards an announced action targets. Targeting is command.TargetId, as for Akodo Kakihara's reaction (a group or mass
    // effect that names no single target does not "target" anything); Bayushi Kahoku - exp is the one Attack whose own target
    // rides in CardIds beside a separate -4F TargetId, and that Attack targets its card too.
    private static IEnumerable<string> ActionTargetIds(GameCommand command)
    {
        if (command.TargetId is not null) yield return command.TargetId;
        if (command.AbilityId == "kahoku-exp-attack" && command.CardIds is { Length: 1 } attackTarget) yield return attackTarget[0];
        // A multi-target action (issue #336) targets every card of every card slot; the first is TargetId, yielded above.
        var slots = command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var slotted) ? slotted.Slots : null;
        foreach (var id in (command.Selections ?? []).Where(s => slots?.FirstOrDefault(x => x.Id == s.Slot) is not ({ Performing: true } or { CostOnly: true })).SelectMany(s => s.CardIds ?? []))
            if (id != command.TargetId) yield return id;
    }

    // The current Force of the card that PERFORMED an action, 0 when nothing with a Force did (rules lead's ruling). A card
    // the ability names as its performer (a Strategy's "your performing Samurai", Yayu's Spell) performs it; otherwise the
    // card the ability is on. A Personality performs with its own Force; a Follower with ITS OWN Force (not its wielder's); an
    // Item or Spell is performed by the Personality it is attached to (that Personality's current Force). A Strategy, Ring,
    // Holding, Region, Celestial, Stronghold or player ability (the Imperial Favor) has no performing Personality: Force 0.
    private int PerformingCardForce(GameCommand command)
    {
        var ability = AbilityCatalog.All[command.AbilityId!];
        var performerId = ability.Performer is not null && command.PerformerId is not null ? command.PerformerId : command.CardId;
        if (performerId is null || !_state.Cards.TryGetValue(performerId, out var performer)) return 0;
        switch (Definition(performer).Type)
        {
            case CardType.Personality: case CardType.Follower: return CardForce(performer);
            case CardType.Item: case CardType.Spell:
                if (performer.Zone != Zone.Attached || performer.AttachedTo is not string wielderId) return 0;
                var wielder = Card(wielderId);
                return Definition(wielder).Type == CardType.Personality ? CardForce(wielder) : 0;
            default: return 0;
        }
    }

    // Daigotsu Bofana - exp: "After Bofana enters play: Lose 2 Honor and all other players must discard a card, or two cards if
    // they are Crab Clan." A mandatory entry effect like Destined's draw (ApplyMandatoryEntryEffects): the Honor loss is the card's
    // EntryHonorLoss, the discards are queued for each other player (each chooses their own card; a player with too few cards
    // discards what they have). A player is Crab Clan when their Stronghold's Clan is Crab (PlayerState.Clan, the same "is this
    // player X Clan" every other card reads, e.g. Kitsuki Minori's "any other player is Lion Clan").
    private void QueueOtherPlayersEntryDiscards(int controllerId)
    {
        foreach (var other in _state.Players.Where(p => p.Id != controllerId))
            for (var n = other.Clan == "Crab" ? 2 : 1; n > 0; n--)
                _state.PendingForcedDiscards.Add(new(other.Id, "Daigotsu Bofana"));
    }

    // "Play it for one of its Battle actions ... After it resolves, remove the card from the game" (Daigotsu Bofana - exp's
    // second ability): Bofana's action grants an Additional Action scoped to the ONE chosen discard-pile card (AdditionalActionGrant.
    // FromDiscard). That grant makes exactly that card's Battle-timed abilities usable from the discard pile -- the player picks which
    // one, each validated like any action -- and taking the action consumes it. The grant is spent or forfeited like every other
    // (a normal action or a pass ends the burst), and the card, a Strategy that sat in a discard pile, is removed from the game once
    // the action has resolved. Limited to a Battle-timed ability by default: a Battle/Open ability's Open half is not "one of its
    // Battle actions". Akodo Tezuka (issue #449) grants "one of its Battle, Limited, or Open actions" instead --
    // AdditionalActionGrant.AnyDiscardTiming -- so the same grant also admits those, each still gated by the ordinary Timing-vs-phase
    // legality (Limited: the active player only; Open/Battle: their own phase) every other use of that ability already goes through, which
    // is exactly what "as appropriate to the current phase" describes: no separate check of its own.
    private AdditionalActionGrant? DiscardPlayGrant(int playerId, CardInstance card) =>
        card.Zone is Zone.FateDiscard or Zone.DynastyDiscard
            ? _state.AdditionalActions.FirstOrDefault(g => g.FromDiscard && g.PlayerId == playerId && g.ScopeCardIds?.Contains(card.Id) == true)
            : null;
    private bool PlayableFromDiscard(AbilityDefinition ability, CardInstance source, int playerId) =>
        DisciplineFromDiscard(ability, source) || DiscardPlayGrant(playerId, source) is { } grant
            && (ability.Timing == "Battle" || grant.AnyDiscardTiming && ability.Timing is "Limited" or "Open" or "BattleOrOpen");
    // A card played as an action from a discard pile leaves the game afterward: the Discipline play and the granted play are the
    // only two ways to use a non-Reaction ability from a discard pile (ValidateAbility's zone check).
    private static bool PlayedFromDiscard(AbilityDefinition ability, CardInstance? source) =>
        source is { Zone: Zone.FateDiscard or Zone.DynastyDiscard } && ability.Timing != "Reaction";
    // Same-turn replay is refused PER ACTION (Mendon's ruling): the once-per-turn stamp lives under each ability's own UsageKey on the card
    // and survives the move to the discard pile, so an action performed this turn (from hand, in play, or earlier from the pile) cannot be
    // performed again, while another action of the same card can. The Discipline play uses the very same stamp, so both rules are one mechanism.
    // Generalized (issue #449) from a Battle-only check to any of the caller's own named Timings, so Akodo Tezuka's own target legality --
    // "choose a Bushido Virtue with a Battle, Limited or Open action still unspent" -- can ask the same question Bofana's Battle-only case does.
    private bool HasUnspentAction(CardInstance card, params string[] timings) => (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var def) && timings.Contains(def.Timing)
        && (!card.AbilityUses.TryGetValue(def.UsageKey ?? a, out var used) || used != _state.TurnNumber));

    // Proclaim first, then the entering effects (Mendon's ruling): a Proclaim-eligible entry opens the Proclaim window, and its mandatory "after
    // this enters play" effects -- an Honor loss, Destined's draw, Sensei's discard, exp Bofana's discards -- wait for the Proclaim decision, so the
    // Proclaim gain and the loss are two separate Honor changes in that order, never netted. They are applied at the first of: the Proclaim being
    // taken, a Reaction being used in the window (a trait resolves before a Reaction to the same trigger), or the window closing.
    private void ApplyPendingEntryEffects()
    {
        if (_state.PendingEntryEffectsCardId is not string id) return;
        _state.PendingEntryEffectsCardId = null;
        var card = Card(id);
        ApplyMandatoryEntryEffects(card.OwnerId, card);
    }
}
