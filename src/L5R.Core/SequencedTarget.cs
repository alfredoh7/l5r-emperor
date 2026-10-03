namespace L5R.Core;

/// <summary>The second target of an action that names its targets one after another (MatchState.PendingSequencedTarget): the action is paused for its player to choose it, and
/// carries on with its second effect. Paused is the action, kept whole so it finishes exactly as it would have.</summary>
public sealed class PendingSequencedTarget
{
    public string SourceCardId { get; set; } = "";
    public string AttackAbilityId { get; set; } = "";
    public string PerformerId { get; set; } = "";
    public int PlayerId { get; set; }
    /// <summary>The cards the Attack may legally target now, after the first effect has resolved.</summary>
    public List<string> Candidates { get; set; } = [];
    public PendingEffectChoice Paused { get; set; } = new();
}

// Sequenced targets and effects (issue #419, Mendon's ruling on Engulfing Flames: "a primitive that tracks target 1 and effect 1, resolves these, then allows for target 2 and effect 2").
// It is ONE action, not two: the first effect's target is named as the action is announced (an optional slot), the action's costs are paid once, a reaction that negates the action
// negates both parts, and a negation of the first effect's destruction leaves the second effect to happen, since the printed text does not make it depend on the first.
// What is sequenced is the second TARGET: it is chosen only after the first effect has resolved, because what the first effect did can change what the second may target (destroying the
// Follower that shielded a Personality from a Ranged Attack makes him a target). Engulfing Flames: "You may target and destroy an enemy attachment. If your Monk has any Fire tokens,
// Ranged 4 Attack." The second half is an Attack in every respect -- its strength and kind, the same target rules, the same reaction window -- named for its own ability id
// ("engulfing-flames-attack"), which is never offered as an action of its own.
//
//   * the acting player answers with "chooseSequencedTarget": a card of those the Attack may legally target now (TargetId), or Choice "none" (an Attack may always be taken with no target);
//   * the choice is public, as the action was announced in the open.
public sealed partial class MatchEngine
{
    /// <summary>Called from the ability's execution case after its first effect: hands the second target to the acting player. Returns true when the choice is open (the caller returns
    /// without finishing the action).</summary>
    private bool BeginSequencedAttackTarget(string attackAbilityId, GameCommand command, CardInstance performer, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore,
        HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var source = Card(command.CardId);
        var actor = Player(command.PlayerId);
        var ability = AbilityCatalog.All[attackAbilityId];
        var probe = new GameCommand("useAbility", actor.Id, _state.Version, source.Id, AbilityId: attackAbilityId, PerformerId: performer.Id);
        var candidates = _state.Cards.Values.Where(c => InPlay(c) && c.OwnerId != actor.Id && LegalAttackTarget(attackAbilityId, ability, probe, actor, performer, c)).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        var pending = new PendingSequencedTarget
        {
            SourceCardId = source.Id, AttackAbilityId = attackAbilityId, PerformerId = performer.Id, PlayerId = actor.Id, Candidates = candidates,
            Paused = new()
            {
                Command = command, PlayerId = actor.Id, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
                OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList()
            }
        };
        // The played Strategy is spent as it is played: it is in the discard pile, in the open, while its second target is chosen.
        SpendHandPlayedCard(actor, source);
        _state.PendingSequencedTarget = pending;
        SuspendOpenWindow();
        _state.PendingChoice = new("sequencedTarget", actor.Id, source.Id,
            $"{Definition(source).Name}: choose the Attack's target (Ranged {AttackStrengthOf(attackAbilityId)}), or none.", actor.Id);
        _state.PriorityPlayerId = actor.Id; _state.ConsecutivePasses = 0;
        Emit("sequencedTargetOffered", $"{actor.Name} chooses the second target of {Definition(source).Name}.", actor.Id, source.Id);
        return true;
    }

    private bool LegalAttackTarget(string id, AbilityDefinition ability, GameCommand probe, PlayerState player, CardInstance performer, CardInstance target)
    {
        try { RequireLegalAttackTarget(id, ability, probe, player, performer, target); return true; }
        catch (RuleViolation) { return false; }
    }

    private int AttackStrengthOf(string attackAbilityId) => EffectiveAttackStrength(attackAbilityId, Player(_state.ActivePlayerId), null, null);

    private void ResolveSequencedTargetCommand(GameCommand command)
    {
        var pending = _state.PendingSequencedTarget ?? throw new RuleViolation("No second target is waiting.");
        Require(command.Type == "chooseSequencedTarget", "Choose the second target.");
        Require(command.PlayerId == pending.PlayerId, "Only the acting player chooses it.");
        string? targetId = null;
        if (command.Choice != "none") { Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered cards, or none."); targetId = command.TargetId; }
        _state.PendingChoice = null; _state.PendingSequencedTarget = null;
        var paused = pending.Paused; var attacker = Player(pending.PlayerId); var performer = Card(pending.PerformerId);
        Emit("sequencedTargetChosen", targetId is not null ? $"{attacker.Name} chose {Definition(Card(targetId)).Name}." : $"{attacker.Name} chose no target.", attacker.Id, targetId);
        // The second effect: the Attack, through the ordinary pipeline. Its own reaction window (if any) finishes the action; otherwise the action finishes here.
        var running = _runningCommand;
        _runningCommand = paused.Command;
        _state.NegatedEffects = paused.Negated.ToList();
        var deferred = ResolveOrDeferAttack(pending.AttackAbilityId, attacker, targetId, performer.Id, paused.ConsumedGrant);
        _runningCommand = running;
        if (deferred) return;
        _state.NegatedEffects.Clear();
        CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore,
            paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
    }

    private IEnumerable<ActionOption> SequencedTargetActions(int playerId)
    {
        if (_state.PendingSequencedTarget is not { } p || p.PlayerId != playerId) yield break;
        foreach (var id in p.Candidates)
            yield return new("chooseSequencedTarget", $"Attack · {Definition(Card(id)).Name}", true, null, new GameCommand("chooseSequencedTarget", playerId, _state.Version, CardId: p.SourceCardId, TargetId: id), id);
        yield return new("chooseSequencedTarget", "Attack no target", true, null, new GameCommand("chooseSequencedTarget", playerId, _state.Version, CardId: p.SourceCardId, Choice: "none"), p.SourceCardId);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredSequencedTarget(string type, GameCommand command)
    {
        if (type != "sequencedTarget") return false;
        ResolveSequencedTargetCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSequencedTargetHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredSequencedTarget", 900, static (engine, type, command) => engine.HandleRegisteredSequencedTarget(type, command));
    }
}
