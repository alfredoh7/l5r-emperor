namespace L5R.Core;

// "An action targets X" reactions (issue #335): redirect the action (Ezuno, Blade of Champions), re-check a target that moved (Yurishi), negate
// the action (Jinn-Kuen) or its bowing and destruction of a Follower (Mochitoko). Offered in both windows an action can be held in: the
// before-action window for any action, the attack window for an Attack. An Attack that names no card (no target, or a province) holds no
// window: no card is targeted. Readings and alternatives: docs/decisions/2026-09-19-redirection-and-protective-reactions.md.
public static class ReactionTriggers
{
    public const string TargetedByAction = "targeted-by-action";
}

public sealed partial class MatchEngine
{
    private const string TargetedByAction = ReactionTriggers.TargetedByAction;

    // An Attack is announced and paid for inside RunAbilityEffects and its window opens before that returns, so its reactors find the command here.
    private GameCommand? _runningCommand;

    /// <summary>The held action: its command and the card it targets now (null for a non-Attack action that targets no card, whose cost may still name
    /// a Follower of Mochitoko's). For an Attack that is the Attack's own target (Kahoku exp carries it in CardIds).</summary>
    private sealed record ReactionSubject(GameCommand Command, string? TargetId, bool IsAttack);

    private static GameCommand WithActionTarget(GameCommand command, string targetId, bool attackTarget) =>
        attackTarget && !TargetsThroughAttackPipeline(command.AbilityId!) ? command with { CardIds = [targetId] } : command with { TargetId = targetId };

    // An Attack's own target is judged in the attack window, so before its cost is paid it is held here only for a bow or destroy cost it would pay with a card
    // that may be protected (Mochitoko). A Reaction is an action too (IsAction), so it is held like any other.
    private ReactionSubject? SubjectForAnnouncedAction(GameCommand command)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return null;
        var costOnly = TargetsThroughAttackPipeline(id);
        var target = !costOnly && command.TargetId is { } t && _state.Cards.ContainsKey(t) ? t : null;
        return target is not null || CostSubjectOf(command) is not null ? new(command, target, false) : null;
    }

    // An Attack that names no card (no target, or a province) targets no card, so nothing card-targeted reacts to it.
    private ReactionSubject? SubjectForAttack(GameCommand? command, string? targetId) =>
        command?.AbilityId is { } id && targetId is not null && _state.Cards.ContainsKey(targetId)
        && AbilityCatalog.All.TryGetValue(id, out var ability) && IsAction(ability)
            ? new(WithActionTarget(command, targetId, true), targetId, true) : null;

    private ReactionSubject HeldAction() => OpenReactionSubject() ?? throw new RuleViolation("No action is open to this reaction.");

    private ReactionSubject? OpenReactionSubject() => _state.PendingChoice?.Type switch
    {
        "beforeActionReaction" when _state.PendingNegatableAbility is { } deferred => SubjectForAnnouncedAction(deferred),
        "attackReaction" when _state.PendingAttack is { } attack => SubjectForAttack(attack.Command, attack.TargetId),
        _ => null
    };

    private static bool WithReactorBowed(CardInstance reactor, Func<bool> judge)
    {
        var was = reactor.Bowed;
        reactor.Bowed = true;
        try { return judge(); }
        finally { reactor.Bowed = was; }
    }

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

    // The redirecting card is bowed as its own cost before the new target is chosen, so legality is judged on that position -- for the card itself too.
    private bool RedirectTargetAccepted(ReactionSubject subject, string targetId, CardInstance reactor) =>
        WithReactorBowed(reactor, () => ActionAcceptsTarget(subject, targetId));

    // A non-Attack action is held whole, so its costs are paid once, at resolution, for the new target; an Attack's costs stay paid and its strength follows the target.
    private void RedirectAction(ReactionSubject subject, string newTargetId, CardInstance reactor)
    {
        var oldTargetId = subject.TargetId ?? throw new RuleViolation("The action targets no card.");
        Require(ActionAcceptsTarget(subject, newTargetId), "The action cannot legally target that card.");
        var redirected = WithActionTarget(subject.Command, newTargetId, subject.IsAttack);
        if (subject.IsAttack)
        {
            var attack = _state.PendingAttack!;
            var attacker = Player(attack.PlayerId);
            attack.Strength += EffectiveAttackStrength(attack.AbilityId, attacker, Card(newTargetId), attack.SourceId)
                - EffectiveAttackStrength(attack.AbilityId, attacker, Card(oldTargetId), attack.SourceId);
            attack.TargetId = newTargetId;
            attack.Command = redirected;
        }
        else _state.PendingNegatableAbility = redirected;
        Emit("actionRedirected", $"{Definition(reactor).Name}: the action now targets {Definition(Card(newTargetId)).Name} instead of {Definition(Card(oldTargetId)).Name}.", reactor.OwnerId, newTargetId);
    }

    /// <summary>Per-card negation: kinds of effect the held action would apply to ONE card ("bow", "destroy", "move", "straighten" or "all"). Several kinds may stand at
    /// once; a kind is consumed by the first effect of it that runs, "all" is not, and every entry is dropped when the action has resolved. An "all" entry also stops an
    /// Honor loss the action inflicts on that card's controller (GainHonor). Whole-action negation is NegateHeldAction.</summary>
    private void NegateEffects(CardInstance card, params string[] kinds)
    {
        foreach (var kind in kinds) _state.NegatedEffects.Add(new(kind, card.Id));
        Emit("effectsNegated", $"{Definition(card).Name}: the action's {(kinds.Contains("all") ? "effects" : string.Join(" and ", kinds.Select(k => k switch { "bow" => "bowing", "destroy" => "destruction", "move" => "movement", "straighten" => "straightening", _ => k })))} on it {(kinds.Length > 1 && !kinds.Contains("all") ? "are" : "is")} negated.", card.OwnerId, card.Id);
    }

    /// <summary>"Negate its effects on him": every effect kind the engine applies to that card, and Honor lost by its controller to the action.</summary>
    private void NegateAllEffectsOn(CardInstance card) => NegateEffects(card, "all");

    /// <summary>"Negate the action's effects": the whole held action, whichever cards it reaches. A deferred action goes through ActionNegation.cs (issue #338, Sakuma);
    /// an Attack is negated here as a whole, its cost already paid. Protection (issue #212) stops either.</summary>
    private void NegateHeldAction(CardInstance negator)
    {
        if (_state.PendingChoice?.Type == "attackReaction" && _state.PendingAttack is { } attack)
        {
            if (attack.SourceId is not null && Protected(ActingPersonality(Card(attack.SourceId))))
            {
                Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", negator.OwnerId, negator.Id);
                return;
            }
            attack.Negated = true;
            Emit("actionNegated", $"{Definition(negator).Name} negates {Player(attack.PlayerId).Name}'s Attack.", negator.OwnerId, negator.Id);
        }
        else NegateDeferredAction(negator);
    }

    // An action a reaction has already negated as a whole (Sakuma's, Jinn-Kuen's) will not resolve: redirecting it or negating it again would be an empty reaction, so
    // none is offered and no window opens for one.
    private bool HeldActionNegated(ReactionSubject subject) =>
        subject.IsAttack ? _state.PendingAttack?.Negated == true : _state.DeferredActionNegated;

    /// <summary>Negates the action's effects on the card when it is no longer a legal target; returns whether it did.</summary>
    private bool RecheckTarget(ReactionSubject subject, CardInstance card)
    {
        if (ActionAcceptsTarget(subject, card.Id)) return false;
        NegateAllEffectsOn(card);
        return true;
    }

    // Honor a negated card's controller would lose to another player's action is lost no more (decision note 3): only what the ACTION inflicts, never a cost the
    // acting player pays himself.
    private bool HonorLossNegatedWithEffects(int playerId) =>
        _runningCommand is { } running && running.PlayerId != playerId
        && _state.NegatedEffects.Any(n => n.Kind == "all" && _state.Cards.TryGetValue(n.CardId, out var negated) && negated.OwnerId == playerId);

    // ---- a cost paid by a Follower a negation protects (Hida Mochitoko, decision note 2) ------------------------------------------------------

    // The card an action pays a bow or destroy cost with (its chosen cost option's subject) and the verb it is paid with, if it names one.
    private (CardInstance Subject, string Verb)? CostOf(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.CostOptions is null) return null;
        try
        {
            var option = ChosenCostOption(ability, command);
            var subject = CostSubjectCard(option, ability, command, command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null);
            return subject is not null && InPlay(subject) ? (subject, option.Verb) : null;
        }
        catch (RuleViolation) { return null; }
    }

    private CardInstance? CostSubjectOf(GameCommand command) => CostOf(command)?.Subject;

    /// <summary>Is this action's cost payment blocked by a negation? True when the card it would bow or destroy as its cost has had that negated. This is the ONE
    /// place the rule is asked: the action then does not resolve and pays nothing (AbortActionForBlockedCost), so a cost cannot be dodged while the effects are
    /// kept. A cost-payment function that pays atomically calls this first and passes its verdict on.</summary>
    private bool CostPaymentBlockedByNegation(GameCommand command) =>
        _state.NegatedEffects.Count > 0 && CostOf(command) is { } cost
        && _state.NegatedEffects.Any(n => n.CardId == cost.Subject.Id && (n.Kind == "all" || n.Kind == cost.Verb)); // a bow is blocked by a negated bow, a destruction by a negated destruction

    // The action is negated: taken (its use is spent, priority passes) but nothing is paid and nothing happens -- no Gold, no Honor-loss cost, no discard, no effect.
    private void AbortActionForBlockedCost(GameCommand command)
    {
        var ability = AbilityCatalog.All[command.AbilityId!];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        SpendAbilityUse(source?.AbilityUses ?? player.AbilityUses, ability, ability.UsageKey ?? command.AbilityId!);
        Emit("actionNegated", $"{player.Name}'s {ability.Label} is negated: {Definition(CostSubjectOf(command)!).Name}'s bowing or destruction as its cost was negated, so nothing is paid and nothing resolves.", player.Id, source?.Id);
        if (ForfeitsAdditionalActions(ability)) _state.AdditionalActions.RemoveAll(g => g.PlayerId == player.Id);
        if (OpenOverconfidentDrawWindow(1 - player.Id)) return;
        NextAction(player.Id);
    }

    private IEnumerable<(CardInstance Card, string AbilityId)> TargetedActionReactionCandidates() =>
        _state.Cards.Values.Where(InPlay).SelectMany(c => AbilityIdsOf(c)
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
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        if (!InPlay(card) || HeldActionNegated(subject)) return false;
        if (abilityId == "mochitoko-negate") return MochitokoFollowers(card, subject).Any();
        if (subject.TargetId is null || !_state.Cards.TryGetValue(subject.TargetId, out var affected) || !InPlay(affected)) return false;
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
            default:
                return false;
        }
    }

    // Another player's action targets one of the reactor's own [keyword] Personalities at his location, and is legal against him.
    private bool YojimboRedirectApplies(CardInstance card, ReactionSubject subject, CardInstance affected, string? affectedKeyword) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && affected.Id != card.Id
        && Definition(affected).Type == CardType.Personality && (affectedKeyword is null || Keyword(affected, affectedKeyword))
        && SameLocation(card, affected) && ActionAcceptsTarget(subject, card.Id);

    // The reactor's own other in-play cards the action is legal against -- the reactor itself included, judged after it is bowed as the cost.
    private IEnumerable<CardInstance> RedirectCandidates(CardInstance card, ReactionSubject subject) =>
        _state.Cards.Values.Where(c => c.OwnerId == card.OwnerId && c.Id != subject.TargetId && InPlay(c) && RedirectTargetAccepted(subject, c.Id, card))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // Mochitoko's Followers: his controller's, whose host stands where he does, that the held action would bow or destroy -- as its target (an Attack always could),
    // or as the cost it pays (a bow or destroy cost option), asked before that cost is paid. An Attack is asked twice: its cost here, its target in the attack window.
    private IEnumerable<CardInstance> MochitokoFollowers(CardInstance card, ReactionSubject subject)
    {
        bool Covered(CardInstance follower) =>
            Definition(follower).Type == CardType.Follower && follower.Zone == Zone.Attached && follower.OwnerId == card.OwnerId && InPlay(follower)
            && follower.AttachedTo is { } hostId && SameLocation(card, Card(hostId));
        var ids = new List<CardInstance>();
        if (subject.TargetId is not null && _state.Cards.TryGetValue(subject.TargetId, out var target) && Covered(target) && ActionMayBowOrDestroy(subject.Command.AbilityId!, subject.IsAttack))
            ids.Add(target);
        if (!subject.IsAttack && CostSubjectOf(subject.Command) is { } paying && Covered(paying) && ids.All(c => c.Id != paying.Id)) ids.Add(paying);
        return ids;
    }

    // Curated like BowsTarget / DestroysTarget, which it extends: a new action that bows or destroys a Follower is added here.
    private static bool ActionMayBowOrDestroy(string abilityId, bool isAttack) =>
        isAttack || BowsTarget(abilityId) || DestroysTarget(abilityId) || DestroysAttachmentTarget(abilityId);

    private static bool DestroysAttachmentTarget(string abilityId) => abilityId is "crushing-destroy" or "never-afraid" or "keppo-destroy" or "michitsu-destroy"
        or "tetsuo-destroy-attachment" or "tokoru-bear-bow" or "hiroto-bow";

    private IEnumerable<(string? TargetId, string? Label)> TargetedReactionChoices(CardInstance card, string abilityId, ReactionSubject subject) =>
        abilityId switch
        {
            "blade-of-champions-redirect" => RedirectCandidates(card, subject).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})")),
            "mochitoko-negate" => MochitokoFollowers(card, subject).Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})")),
            _ => [(null, null)]
        };
}
