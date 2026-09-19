namespace L5R.Core;

// Reactions to an action that TARGETS a card (issue #335): Mirumoto Ezuno and Blade of Champions redirect the action to another card,
// Goju Yurishi moves away and the action's legality against him is re-checked, Yasuki Jinn-Kuen - exp2 negates the action's effects on a
// Merchant for Gold, Hida Mochitoko - exp negates a Follower's bowing and destruction twice a turn. Every rules reading is recorded, with
// the alternative, in docs/decisions/2026-09-19-redirection-and-protective-reactions.md.
//
// This is the second family of in-play "an action targets X" reactions, beside the before-action-resolves one (Rokai, Tamoko, Champion of
// Thunder, Brothers of Goemon, Kakihara). Those reactions negate or modify effects the deferred action will apply; these also change WHO the
// action affects, so they need to see and rewrite the action itself, for an Attack as well as for any other action. They use the trigger
// "targeted-by-action" and are offered in BOTH windows an action can be held in -- the beforeActionReaction window (the whole announced
// command is deferred, so none of its costs or effects have happened) and the attackReaction window (the Attack's costs are paid and its
// target is fixed; only its resolution is deferred). Everything they need is one small vocabulary, and each piece is general:
//
//   ReactionSubject         the held action, whichever window holds it: its command and the card it currently targets.
//   ActionAcceptsTarget     "would the action be accepted against THIS card?" -- the action's own target validation run again. It is the one
//                           definition of a "legal target" that redirection and re-checking share.
//   RedirectAction          make the held action target another card, if it is legal (Ezuno, Blade of Champions).
//   RecheckTarget           after a reaction changed the game, is the card still a legal target, and if not negate the action's effects on it
//                           (Goju Yurishi).
//   NegateEffects           negate any set of effect kinds an action would apply to a card -- bow, destroy, move, straighten, or "all", the
//                           blanket negation (Hida Mochitoko: bow and destroy; Yasuki Jinn-Kuen: everything; Goju Yurishi's re-check).
/// <summary>Reaction triggers introduced with the targeted-action reactions.</summary>
public static class ReactionTriggers
{
    /// <summary>"After (or when) an action targets this card, or a card of its controller's": offered while the action is held in the before-action
    /// window or, for an Attack, the attack window.</summary>
    public const string TargetedByAction = "targeted-by-action";
}

public sealed partial class MatchEngine
{
    private const string TargetedByAction = ReactionTriggers.TargetedByAction;

    // The command whose effects are currently being run (ExecuteAbilityEffects sets it around RunAbilityEffects). An Attack is announced and
    // paid for inside that call, and the window that holds it opens before the call returns, so its reactors are asked about the ACTION while
    // it is still being executed; this is where they find it. Transient: never part of MatchState, restored on every exit.
    private GameCommand? _runningCommand;

    /// <summary>The held action a targeted-by-action reaction is answering: the command as it would resolve, and the card it targets right
    /// now. IsAttack marks an Attack held in the attackReaction window, whose own target is chosen at declaration and lives on the PendingAttack
    /// (for Bayushi Kahoku - exp, whose TargetId is a separate -4F target, the Attack's target rides in CardIds).</summary>
    private sealed record ReactionSubject(GameCommand Command, string TargetId, bool IsAttack);

    // The command with ITS ACTION'S target replaced: an Attack's own target for an Attack subject, the ordinary target otherwise.
    private static GameCommand WithActionTarget(GameCommand command, string targetId, bool attackTarget) =>
        attackTarget && !TargetsThroughAttackPipeline(command.AbilityId!) ? command with { CardIds = [targetId] } : command with { TargetId = targetId };

    // A non-Attack action about to be deferred (ResolveOrDeferBeforeActionReaction). A Reaction is not "an action" (it would open a nested
    // window over the one it is played in), and an Attack-pipeline action reacts in the attack window instead, never both.
    private ReactionSubject? SubjectForAnnouncedAction(GameCommand command) =>
        command.AbilityId is { } id && command.TargetId is { } target && _state.Cards.ContainsKey(target)
        && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction" && !TargetsThroughAttackPipeline(id)
            ? new(command, target, false) : null;

    // An Attack about to be, or already, held in the attack window.
    private ReactionSubject? SubjectForAttack(GameCommand? command, string? targetId) =>
        command?.AbilityId is { } id && targetId is not null && _state.Cards.ContainsKey(targetId)
        && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction"
            ? new(WithActionTarget(command, targetId, true), targetId, true) : null;

    // The action a reaction being resolved is answering. ValidateAbility already established that one is open.
    private ReactionSubject HeldAction() => OpenReactionSubject() ?? throw new RuleViolation("No action is open to this reaction.");

    // The action currently held open to reactions, or null when no such window is open.
    private ReactionSubject? OpenReactionSubject() => _state.PendingChoice?.Type switch
    {
        "beforeActionReaction" when _state.PendingNegatableAbility is { } deferred => SubjectForAnnouncedAction(deferred),
        "attackReaction" when _state.PendingAttack is { } attack => SubjectForAttack(attack.Command, attack.TargetId),
        _ => null
    };

    /// <summary>Would the held action be accepted against this card? The action's own target validation run again -- for an Attack the Attack
    /// target rules (in the current enemy army, no Follower to hide behind, strength against Force), for anything else the whole of
    /// ValidateAbility, which its own target category is part of. This is the single definition of a "legal target" that redirection ("if
    /// legal") and the re-check ("no longer a legal target") both use.</summary>
    private bool ActionAcceptsTarget(ReactionSubject subject, string targetId)
    {
        if (!_state.Cards.TryGetValue(targetId, out var target)) return false;
        var command = WithActionTarget(subject.Command, targetId, subject.IsAttack);
        if (!subject.IsAttack) return AbilityRejection(command) is null;
        // An Attack's costs are already paid and its source may already have been spent (a Strategy is discarded as its window opens), so the
        // whole-action validation would refuse it for reasons that have nothing to do with the target.
        var id = command.AbilityId!;
        var ability = AbilityCatalog.All[id];
        try
        {
            RequireLegalAttackTarget(id, ability, command, Player(command.PlayerId), command.CardId is null ? null : Card(command.CardId), target,
                id == "kahoku-exp-attack" ? command.TargetId : null, id == "kahoku-exp-attack" ? 4 : 0);
            return true;
        }
        catch (RuleViolation) { return false; }
    }

    /// <summary>Makes the held action target another card. It must be legal (ActionAcceptsTarget). A non-Attack action is still deferred whole, so
    /// nothing it costs has been paid and it is paid, once, for the new target when the window closes; an Attack's costs were already paid and
    /// stay paid, and its strength follows the new target (a defense bonus or conditional strength that depends on it), keeping whatever
    /// reactions already did to it. Reactions the window offers are re-evaluated against the NEW target from here on; what was already played
    /// against the old one (a bonus, a negation) stays with the old card.</summary>
    private void RedirectAction(ReactionSubject subject, string newTargetId, CardInstance reactor)
    {
        Require(ActionAcceptsTarget(subject, newTargetId), "The action cannot legally target that card.");
        var redirected = WithActionTarget(subject.Command, newTargetId, subject.IsAttack);
        if (subject.IsAttack)
        {
            var attack = _state.PendingAttack!;
            var attacker = Player(attack.PlayerId);
            attack.Strength += EffectiveAttackStrength(attack.AbilityId, attacker, Card(newTargetId), attack.SourceId)
                - EffectiveAttackStrength(attack.AbilityId, attacker, Card(subject.TargetId), attack.SourceId);
            attack.TargetId = newTargetId;
            attack.Command = redirected;
        }
        else _state.PendingNegatableAbility = redirected;
        Emit("actionRedirected", $"{Definition(reactor).Name}: the action now targets {Definition(Card(newTargetId)).Name} instead of {Definition(Card(subject.TargetId)).Name}.", reactor.OwnerId, newTargetId);
    }

    /// <summary>Negates the named kinds of effect the held action would apply to a card: "bow", "destroy", "move", "straighten", or "all" -- the
    /// blanket negation of everything it does to that card (bow, straighten, move, destroy, Force/Chi/Personal Honor modifiers, dishonor, tokens
    /// placed on it). Any number of kinds, on any number of cards, may stand together on one action (NegatedEffect: one entry per card and
    /// kind); a kind is consumed by the first effect of it that runs, "all" is not, and every entry is discarded once the action has resolved.
    /// A reaction that must negate more than one kind (Mochitoko: bowing AND destruction) names them together, and an effect that is both -- bows,
    /// then destroys if it was bowed -- finds each of its parts negated separately.</summary>
    private void NegateEffects(CardInstance card, params string[] kinds)
    {
        foreach (var kind in kinds) _state.NegatedEffects.Add(new(kind, card.Id));
        Emit("effectsNegated", $"{Definition(card).Name}: the action's {(kinds.Contains("all") ? "effects" : string.Join(" and ", kinds.Select(k => k switch { "bow" => "bowing", "destroy" => "destruction", "move" => "movement", "straighten" => "straightening", _ => k })))} on it {(kinds.Length > 1 && !kinds.Contains("all") ? "are" : "is")} negated.", card.OwnerId, card.Id);
    }

    /// <summary>Blanket negation of the held action's effects on a card (NegateEffects "all"): the reusable "negate the action's effects" for any
    /// reaction that says so.</summary>
    private void NegateAllEffectsOn(CardInstance card) => NegateEffects(card, "all");

    /// <summary>After a reaction changed the game (Yurishi moved), is this card still a legal target of the held action? When it is not, the action's
    /// effects on it are negated (NegateAllEffectsOn). Returns whether it was negated.</summary>
    private bool RecheckTarget(ReactionSubject subject, CardInstance card)
    {
        if (ActionAcceptsTarget(subject, card.Id)) return false;
        NegateAllEffectsOn(card);
        return true;
    }

    // ---- who reacts, and when -----------------------------------------------------------------------------------------------------

    private IEnumerable<(CardInstance Card, string AbilityId)> TargetedActionReactionCandidates() =>
        _state.Cards.Values.Where(InPlay).SelectMany(c => (Definition(c).Abilities ?? [])
            .Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == TargetedByAction)
            .Select(id => (Card: c, AbilityId: id)));

    // Usable right now: applicable, the card unbowed (ValidateAbility forbids any ability from a bowed source), its per-turn limit not spent, and
    // its Gold cost payable -- priced through the same quote every costed action uses (a cost reduced to 0 counts), so a window is never opened for
    // a reaction its controller could not take.
    private bool TargetedActionReactionAvailable(CardInstance card, string abilityId, ReactionSubject subject)
    {
        var ability = AbilityCatalog.All[abilityId];
        return !card.Bowed && !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId)
            && TargetedActionReactionApplies(card, abilityId, subject) && CanPayReaction(card, abilityId);
    }

    private bool CanPayReaction(CardInstance card, string abilityId)
    {
        var quote = QuoteAbilityGold(new GameCommand("useAbility", card.OwnerId, _state.Version, CardId: card.Id, AbilityId: abilityId));
        return quote is null || quote.Error is null && (quote.Cost == 0 || quote.Payments.Length > 0);
    }

    private bool OwnsTargetedActionReaction(int playerId, ReactionSubject? subject) =>
        subject is not null && TargetedActionReactionCandidates().Any(x => x.Card.OwnerId == playerId && TargetedActionReactionAvailable(x.Card, x.AbilityId, subject));

    // "A Battle action": an ability whose timing is Battle, or Battle/Open used while a battle is on (its Open half is not a Battle action).
    private bool IsBattleAction(GameCommand command) =>
        AbilityCatalog.All.TryGetValue(command.AbilityId ?? "", out var ability) && (ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle);

    // The one place each card's own printed trigger lives (BeforeActionReactionApplies's counterpart). Whether the reaction is worth offering AT ALL
    // is decided here, before any window opens, so a legal redirect must exist for a redirecting card and an action that could bow or destroy the
    // Follower must be the one targeting it for Mochitoko.
    private bool TargetedActionReactionApplies(CardInstance card, string abilityId, ReactionSubject subject)
    {
        var command = subject.Command;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || acting.Timing == "Reaction") return false;
        if (!InPlay(card) || !_state.Cards.TryGetValue(subject.TargetId, out var affected) || !InPlay(affected)) return false;
        switch (abilityId)
        {
            // "When another player's action would target one of your Shugenja at Ezuno's location, it targets Ezuno instead, if legal." The
            // redirect ability common to Yojimbo (Ezuno restricts it to Shugenja): the affected card is one of the reactor's own Personalities of the
            // right kind at the reactor's location -- Home is a location of a player, so only that player's own Home counts, a battlefield is shared.
            case "ezuno-redirect":
                return YojimboRedirectApplies(card, subject, affected, "Shugenja");
            // "When a Battle action would target a card in this unit, bow this card: The action targets another of your cards instead, if legal."
            // Any Battle action (its printed text names no acting player), against any card of the Blade's unit -- its wielder or one of his
            // attachments, itself included. Offered only while one of the controller's other cards is a legal new target.
            case "blade-of-champions-redirect":
                return IsBattleAction(command) && UnitHostId(affected) == UnitHostId(card) && RedirectCandidates(card, subject).Any();
            // "After another player's Battle action targets Yurishi: He disappears into the shadows. Move him home." He must be at a battlefield for
            // moving home to mean anything.
            case "yurishi-disappear":
                return command.PlayerId != card.OwnerId && affected.Id == card.Id && IsBattleAction(command) && card.Zone == Zone.Battlefield;
            // "After an action targets your Merchant, pay 2 Gold: Negate the action's effects." Any action (the printed text does not say "another
            // player's"), against any card its controller controls that has the Merchant keyword.
            case "jinn-kuen-negate":
            case "training-negate-merchant-free":
                return affected.OwnerId == card.OwnerId && Keyword(affected, "Merchant");
            // "Twice per turn, after an action targets your Follower at Mochitoko's location: Negate its bowing and destruction from that action's
            // effects." A Follower of his controller's whose host is at his location, targeted by an action that could bow or destroy it (an Attack
            // always could) -- the same "never offer a reaction the action could not need" rule Brothers of Goemon follows.
            case "mochitoko-negate":
                return Definition(affected).Type == CardType.Follower && affected.Zone == Zone.Attached && affected.OwnerId == card.OwnerId
                    && affected.AttachedTo is { } hostId && SameLocation(card, Card(hostId)) && ActionMayBowOrDestroy(command.AbilityId, subject.IsAttack);
            default:
                return false;
        }
    }

    // "Another player's action targets one of your [keyword] Personalities at this Personality's location: it targets this Personality instead, if
    // legal." The affected card is one of the reactor's own Personalities (never the reactor himself) sharing his location, and the action must be
    // legal against the reactor.
    private bool YojimboRedirectApplies(CardInstance card, ReactionSubject subject, CardInstance affected, string? affectedKeyword) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && affected.Id != card.Id
        && Definition(affected).Type == CardType.Personality && (affectedKeyword is null || Keyword(affected, affectedKeyword))
        && SameLocation(card, affected) && ActionAcceptsTarget(subject, card.Id);

    // The controller's cards the held action may be redirected to from a redirecting card: another card of theirs than the one targeted and than the
    // redirecting card itself (which is bowed as the cost, so what is legal against it cannot be judged before paying), against which the action is
    // legal.
    private IEnumerable<CardInstance> RedirectCandidates(CardInstance card, ReactionSubject subject) =>
        _state.Cards.Values.Where(c => c.OwnerId == card.OwnerId && c.Id != subject.TargetId && c.Id != card.Id && InPlay(c) && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // Could this action bow or destroy the card it targets? The curated, extendable classification Brothers of Goemon and Rokai already use (BowsTarget,
    // DestroysTarget) plus the actions that destroy a targeted attachment. An Attack destroys whatever it hits.
    private static bool ActionMayBowOrDestroy(string abilityId, bool isAttack) =>
        isAttack || BowsTarget(abilityId) || DestroysTarget(abilityId) || DestroysAttachmentTarget(abilityId);

    private static bool DestroysAttachmentTarget(string abilityId) => abilityId is "crushing-destroy" or "never-afraid" or "keppo-destroy" or "michitsu-destroy"
        or "tetsuo-destroy-attachment" or "tokoru-bear-bow" or "hiroto-bow";

    // What a reaction that offers a CHOICE of card offers: Blade of Champions one option per legal new target, every other one a single option.
    private IEnumerable<(string? TargetId, string? Label)> TargetedReactionChoices(CardInstance card, string abilityId, ReactionSubject subject) =>
        abilityId == "blade-of-champions-redirect"
            ? RedirectCandidates(card, subject).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})"))
            : [(null, null)];
}
