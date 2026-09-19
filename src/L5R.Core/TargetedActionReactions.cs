namespace L5R.Core;

// "An action targets X" reactions (issue #335): redirect the action (Ezuno, Blade of Champions), re-check a target that moved (Yurishi), negate
// the action's effects on a card (Jinn-Kuen, Mochitoko). Offered in both windows an action can be held in: the before-action window for any
// action, the attack window for an Attack. Readings and alternatives: docs/decisions/2026-09-19-redirection-and-protective-reactions.md.
public static class ReactionTriggers
{
    public const string TargetedByAction = "targeted-by-action";
}

public sealed partial class MatchEngine
{
    private const string TargetedByAction = ReactionTriggers.TargetedByAction;

    // An Attack is announced and paid for inside RunAbilityEffects and its window opens before that returns, so its reactors find the command here.
    private GameCommand? _runningCommand;

    /// <summary>The held action: its command and the card it targets now. For an Attack that is the Attack's own target (Kahoku exp carries it in CardIds).</summary>
    private sealed record ReactionSubject(GameCommand Command, string TargetId, bool IsAttack);

    private static GameCommand WithActionTarget(GameCommand command, string targetId, bool attackTarget) =>
        attackTarget && !TargetsThroughAttackPipeline(command.AbilityId!) ? command with { CardIds = [targetId] } : command with { TargetId = targetId };

    // An Attack-pipeline action is held in the attack window instead, and a Reaction is not "an action" (no nested windows).
    private ReactionSubject? SubjectForAnnouncedAction(GameCommand command) =>
        command.AbilityId is { } id && command.TargetId is { } target && _state.Cards.ContainsKey(target)
        && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction" && !TargetsThroughAttackPipeline(id)
            ? new(command, target, false) : null;

    private ReactionSubject? SubjectForAttack(GameCommand? command, string? targetId) =>
        command?.AbilityId is { } id && targetId is not null && _state.Cards.ContainsKey(targetId)
        && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction"
            ? new(WithActionTarget(command, targetId, true), targetId, true) : null;

    private ReactionSubject HeldAction() => OpenReactionSubject() ?? throw new RuleViolation("No action is open to this reaction.");

    private ReactionSubject? OpenReactionSubject() => _state.PendingChoice?.Type switch
    {
        "beforeActionReaction" when _state.PendingNegatableAbility is { } deferred => SubjectForAnnouncedAction(deferred),
        "attackReaction" when _state.PendingAttack is { } attack => SubjectForAttack(attack.Command, attack.TargetId),
        _ => null
    };

    /// <summary>Would the held action be accepted against this card? The one definition of "legal target" for redirecting and re-checking.</summary>
    private bool ActionAcceptsTarget(ReactionSubject subject, string targetId)
    {
        if (!_state.Cards.TryGetValue(targetId, out var target)) return false;
        var command = WithActionTarget(subject.Command, targetId, subject.IsAttack);
        if (!subject.IsAttack) return AbilityRejection(command) is null;
        // Only the target rules: an Attack's costs are paid and its source may be spent already (a Strategy is discarded as the window opens).
        var id = command.AbilityId!;
        try
        {
            RequireLegalAttackTarget(id, AbilityCatalog.All[id], command, Player(command.PlayerId), command.CardId is null ? null : Card(command.CardId), target,
                id == "kahoku-exp-attack" ? command.TargetId : null, id == "kahoku-exp-attack" ? 4 : 0);
            return true;
        }
        catch (RuleViolation) { return false; }
    }

    // A non-Attack action is held whole, so its costs are paid once, at resolution, for the new target; an Attack's costs stay paid and its strength follows the target.
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

    /// <summary>Negates kinds of effect the held action would apply to a card: "bow", "destroy", "move", "straighten" or "all". Several kinds may stand at once; a kind is
    /// consumed by the first effect of it that runs, "all" is not, and every entry is dropped when the action has resolved.</summary>
    private void NegateEffects(CardInstance card, params string[] kinds)
    {
        foreach (var kind in kinds) _state.NegatedEffects.Add(new(kind, card.Id));
        Emit("effectsNegated", $"{Definition(card).Name}: the action's {(kinds.Contains("all") ? "effects" : string.Join(" and ", kinds.Select(k => k switch { "bow" => "bowing", "destroy" => "destruction", "move" => "movement", "straighten" => "straightening", _ => k })))} on it {(kinds.Length > 1 && !kinds.Contains("all") ? "are" : "is")} negated.", card.OwnerId, card.Id);
    }

    /// <summary>The reusable "negate the action's effects" (on that card): every effect kind the engine applies to a card, see decision note 11.</summary>
    private void NegateAllEffectsOn(CardInstance card) => NegateEffects(card, "all");

    /// <summary>Negates the action's effects on the card when it is no longer a legal target; returns whether it did.</summary>
    private bool RecheckTarget(ReactionSubject subject, CardInstance card)
    {
        if (ActionAcceptsTarget(subject, card.Id)) return false;
        NegateAllEffectsOn(card);
        return true;
    }

    private IEnumerable<(CardInstance Card, string AbilityId)> TargetedActionReactionCandidates() =>
        _state.Cards.Values.Where(InPlay).SelectMany(c => (Definition(c).Abilities ?? [])
            .Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == TargetedByAction)
            .Select(id => (Card: c, AbilityId: id)));

    private bool TargetedActionReactionAvailable(CardInstance card, string abilityId, ReactionSubject subject)
    {
        var ability = AbilityCatalog.All[abilityId];
        return !card.Bowed && !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId)
            && TargetedActionReactionApplies(card, abilityId, subject) && CanPayReaction(card, abilityId);
    }

    // Priced through the Gold quote, so a cost reduced to 0 counts and no window opens for a reaction its controller cannot take.
    private bool CanPayReaction(CardInstance card, string abilityId)
    {
        var quote = QuoteAbilityGold(new GameCommand("useAbility", card.OwnerId, _state.Version, CardId: card.Id, AbilityId: abilityId));
        return quote is null || quote.Error is null && (quote.Cost == 0 || quote.Payments.Length > 0);
    }

    private bool OwnsTargetedActionReaction(int playerId, ReactionSubject? subject) =>
        subject is not null && TargetedActionReactionCandidates().Any(x => x.Card.OwnerId == playerId && TargetedActionReactionAvailable(x.Card, x.AbilityId, subject));

    private bool IsBattleAction(GameCommand command) =>
        AbilityCatalog.All.TryGetValue(command.AbilityId ?? "", out var ability) && (ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle);

    // Each card's printed trigger. A reaction is applicable only when it could do something, so no window opens for nothing.
    private bool TargetedActionReactionApplies(CardInstance card, string abilityId, ReactionSubject subject)
    {
        var command = subject.Command;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || acting.Timing == "Reaction") return false;
        if (!InPlay(card) || !_state.Cards.TryGetValue(subject.TargetId, out var affected) || !InPlay(affected)) return false;
        switch (abilityId)
        {
            case "ezuno-redirect":
                return YojimboRedirectApplies(card, subject, affected, "Shugenja");
            case "blade-of-champions-redirect":
                return IsBattleAction(command) && UnitHostId(affected) == UnitHostId(card) && RedirectCandidates(card, subject).Any();
            case "yurishi-disappear":
                return command.PlayerId != card.OwnerId && affected.Id == card.Id && IsBattleAction(command) && card.Zone == Zone.Battlefield;
            case "jinn-kuen-negate":
            case "training-negate-merchant-free":
                return affected.OwnerId == card.OwnerId && Keyword(affected, "Merchant");
            case "mochitoko-negate":
                return Definition(affected).Type == CardType.Follower && affected.Zone == Zone.Attached && affected.OwnerId == card.OwnerId
                    && affected.AttachedTo is { } hostId && SameLocation(card, Card(hostId)) && ActionMayBowOrDestroy(command.AbilityId, subject.IsAttack);
            default:
                return false;
        }
    }

    // Another player's action targets one of the reactor's own [keyword] Personalities at his location, and is legal against him.
    private bool YojimboRedirectApplies(CardInstance card, ReactionSubject subject, CardInstance affected, string? affectedKeyword) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && affected.Id != card.Id
        && Definition(affected).Type == CardType.Personality && (affectedKeyword is null || Keyword(affected, affectedKeyword))
        && SameLocation(card, affected) && ActionAcceptsTarget(subject, card.Id);

    // The reactor's own other in-play cards the action is legal against; not the redirecting card itself, which is bowed as the cost first.
    private IEnumerable<CardInstance> RedirectCandidates(CardInstance card, ReactionSubject subject) =>
        _state.Cards.Values.Where(c => c.OwnerId == card.OwnerId && c.Id != subject.TargetId && c.Id != card.Id && InPlay(c) && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // Curated like BowsTarget / DestroysTarget, which it extends: a new action that bows or destroys a Follower is added here.
    private static bool ActionMayBowOrDestroy(string abilityId, bool isAttack) =>
        isAttack || BowsTarget(abilityId) || DestroysTarget(abilityId) || DestroysAttachmentTarget(abilityId);

    private static bool DestroysAttachmentTarget(string abilityId) => abilityId is "crushing-destroy" or "never-afraid" or "keppo-destroy" or "michitsu-destroy"
        or "tetsuo-destroy-attachment" or "tokoru-bear-bow" or "hiroto-bow";

    private IEnumerable<(string? TargetId, string? Label)> TargetedReactionChoices(CardInstance card, string abilityId, ReactionSubject subject) =>
        abilityId == "blade-of-champions-redirect"
            ? RedirectCandidates(card, subject).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})"))
            : [(null, null)];
}
