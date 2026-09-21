namespace L5R.Core;

// "An action targets X" reactions (issue #335): redirect the action (Ezuno, Blade of Champions), re-check a target that moved (Yurishi), negate
// the action (Jinn-Kuen) or its bowing and destruction of a Follower (Mochitoko). Offered in both windows an action can be held in: the
// before-action window for any action, the attack window for an Attack. An Attack that names no card (no target, or a province) holds no
// window: no card is targeted. Readings and alternatives: docs/decisions/2026-09-19-redirection-and-protective-reactions.md.
public static class ReactionTriggers
{
    public const string TargetedByAction = "targeted-by-action";
    /// <summary>"After a battle's resolution ends" (issue #417, AfterResolution.cs): played from a hand, a discard pile or play in the after-resolution window.</summary>
    public const string AfterBattleResolution = "after-battle-resolution";
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
    // A multi-target action holds one window per target (issue #336, TargetWindows.cs): the subject is the target the open window is for (ReactionAffectedId).
    private ReactionSubject? SubjectForAnnouncedAction(GameCommand command) => SubjectForWindowTarget(command, ReactionAffectedId(command));

    private ReactionSubject? SubjectForWindowTarget(GameCommand command, string? windowTarget)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return null;
        var costOnly = TargetsThroughAttackPipeline(id);
        var target = !costOnly && windowTarget is { } t && _state.Cards.ContainsKey(t) ? t : null;
        return target is not null || BowOrDestroyCostSubjects(command).Any() ? new(command, target, false) : null;
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
        if (SlotActionOf(subject) is { } slotAbility) return SlotActionAcceptsTarget(subject, slotAbility, targetId);
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
        var redirected = SlotActionOf(subject) is not null ? RetargetSlotAction(subject.Command, oldTargetId, newTargetId) : WithActionTarget(subject.Command, newTargetId, subject.IsAttack);
        if (subject.IsAttack)
        {
            var attack = _state.PendingAttack!;
            var attacker = Player(attack.PlayerId);
            attack.Strength += EffectiveAttackStrength(attack.AbilityId, attacker, Card(newTargetId), attack.SourceId)
                - EffectiveAttackStrength(attack.AbilityId, attacker, Card(oldTargetId), attack.SourceId);
            attack.TargetId = newTargetId;
            attack.Command = redirected;
        }
        else { _state.PendingNegatableAbility = redirected; FollowRedirectInWindow(oldTargetId, newTargetId); }
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

    // The cards an action pays a bow or destroy cost with (its chosen cost option and every declared Costs entry, AbilityCosts.cs). The rule that a negation of that
    // bowing or destruction blocks the payment is CostPaymentBlockedByNegation (AbilityCosts.cs), the one predicate.

    // The action is negated because paying one of its costs was blocked: taken (its use is spent, a Strategy played from a hand is discarded, priority passes) but
    // nothing is paid and nothing happens -- no Gold, no Honor-loss cost, no discard, no effect (ActionNegation.cs, CompleteNegatedAction).
    private void AbortActionForBlockedCost(GameCommand command, bool consumedGrant = false)
    {
        var ability = AbilityCatalog.All[command.AbilityId!];
        var player = Player(command.PlayerId);
        var source = command.CardId is null ? null : Card(command.CardId);
        var playedFromDiscard = PlayedFromDiscard(ability, source);
        Emit("actionNegated", $"{player.Name}'s {ability.Label} is negated: paying one of its costs (bowing or destroying a card as its cost) was negated, so nothing is paid and nothing resolves.", player.Id, source?.Id);
        CompleteNegatedAction(command, ability, player, source, playedFromDiscard, consumedGrant);
    }

    // In play, or in a hand: Know No Fear and To First Blood are Strategies played from a hand as a Reaction (Mendon: the window opens for a reactor holding a matching card).
    private IEnumerable<(CardInstance Card, string AbilityId)> TargetedActionReactionCandidates() =>
        _state.Cards.Values.Where(c => InPlay(c) || c.Zone == Zone.Hand).SelectMany(c => AbilityIdsOf(c)
            .Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing == "Reaction" && a.Trigger == TargetedByAction)
            .Select(id => (Card: c, AbilityId: id)));

    private bool TargetedActionReactionAvailable(CardInstance card, string abilityId, ReactionSubject subject)
    {
        var ability = AbilityCatalog.All[abilityId];
        return (!card.Bowed || abilityId == "red-crane-dojo-ninja") && !ActionLocked(card) && !AbilityUseSpent(card.AbilityUses, ability, ability.UsageKey ?? abilityId)
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
        if (!(InPlay(card) || card.Zone == Zone.Hand) || HeldActionNegated(subject)) return false;
        if (abilityId == "mochitoko-negate") return MochitokoFollowers(card, subject).Any();
        if (subject.TargetId is null || !_state.Cards.TryGetValue(subject.TargetId, out var affected) || !InPlay(affected)) return false;
        switch (abilityId)
        {
            case "ezuno-redirect":
                return YojimboRedirectApplies(card, subject, affected, "Shugenja");
            case "blade-of-champions-redirect":
                return IsBattleAction(command) && UnitHostId(affected) == UnitHostId(card) && RedirectCandidates(card, subject).Any();
            // Spirit of the Yojimbo's granted Reaction: Ezuno's shape without his Shugenja restriction -- any of the reactor's own Personalities at his location.
            case "spirit-yojimbo-redirect":
                return YojimboRedirectApplies(card, subject, affected, null);
            // Spirit of the Berserker's: another player's action targets HIM and could destroy him (an Attack always could, a Personality-destroying ability is on the list).
            case "spirit-berserker-delay":
                return command.PlayerId != card.OwnerId && affected.Id == card.Id && Definition(affected).Type == CardType.Personality && (subject.IsAttack || DestroysTarget(command.AbilityId));
            // Taming the Beast (a Spell): another player's action targets one of its controller's DEFENDING Personalities at the battle, its Shugenja is Earth, and the action could destroy him.
            case "taming-beast-negate":
                return command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
                    && AtBattle(affected) && affected.OwnerId != _state.ActivePlayerId
                    && card is { Zone: Zone.Attached, AttachedTo: { } tamedHostId } && Keyword(Card(tamedHostId), "Earth")
                    && (subject.IsAttack || DestroysTarget(command.AbilityId));
            // Know No Fear (a Strategy played from a hand): another player's action targets one of its controller's Samurai -- the "performing Samurai" is that targeted one. The first
            // half answers an action that would bow or move him; the second, any Fear action.
            case "know-no-fear-negate-move":
                return command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Samurai")
                    && (BowsTarget(command.AbilityId) || MoveOrStraightenKind(command.AbilityId) == "move");
            case "know-no-fear-negate-fear":
                return command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Samurai")
                    && ActionCarriesKeyword(acting, command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var fearSource) ? fearSource : null, "Fear");
            // To First Blood: another player's Iaijutsu action targets one of its controller's Personalities.
            case "to-first-blood-negate":
                return command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
                    && ActionCarriesKeyword(acting, command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var iaijutsuSource) ? iaijutsuSource : null, "Iaijutsu");
            // Red Crane Dojo: any action targets one of its controller's Personalities -- offered when he has Ninja or Shadowlands standing to lose.
            case "red-crane-dojo-ninja":
                return affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && (Keyword(affected, "Ninja") || HasClanAffiliation(affected, "Shadowlands"));
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
        if (!subject.IsAttack)
            foreach (var paying in BowOrDestroyCostSubjects(subject.Command))
                if (Covered(paying) && ids.All(c => c.Id != paying.Id)) ids.Add(paying);
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
