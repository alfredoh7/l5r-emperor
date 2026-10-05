using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PASS (issue #653), the Reactions and delayed battle effects of the Strategies: Closing the Gap, Interrogation, Well Protected and Imminent Victory.
//
//   * CLOSING THE GAP: an after-action-resolved Reaction played from a hand (Without Mercy's window). The action must have moved a Personality to or from the current battlefield
//     (ResolvedActionFacts.Affected: a MovedKind record whose card now stands at the battlefield, or whose FromBattlefield is it). "Move him to the current battlefield, or from the current
//     battlefield to your home" is two catalogue entries sharing one use; each straightens the performer's unit if he moved.
//   * INTERROGATION: "Before another player's action resolves: Negate its effects that bow, destroy, or discard your Holdings, Regions, or Terrains in play." A before-action Reaction played from
//     a hand against an action another player takes that targets such a card of the reactor's and could bow or destroy it (the curated BowsTarget / DestroysTarget lists); it negates the bowing
//     and the destruction of that one card (NegateEffects). READING: an action that affects such cards without targeting them has no engine record to react to, and "discard" names no
//     action the engine has for those cards; both are limits recorded in the report.
//   * WELL PROTECTED: Hundred-Fold Cut's shape for an Armor -- two entries (bow it, or destroy it) sharing one use; the Armor is one attached to the targeted Personality.
//   * IMMINENT VICTORY: "Battle: If you destroy the enemy army or Province in this battle's resolution, give a +1F/+1C token to each of your Personalities in your army (at battle resolution)."
//     A watch of the battle (PromoEmperorFateState.ResolutionWatches) read where the resolution's facts are known (PromoApplyResolution, called by ContinueResolveBattle). "Destroy the enemy
//     army" is read as the engine reads it everywhere else (Seeking the Truth, Tales of Dishonor): the resolution destroyed at least one of the other side's units, or the Province.
// ======================================================================================================================================================================================

public sealed record PromoResolutionWatch(int PlayerId, int BattleNumber, string SourceId);

public sealed partial class PromoEmperorFateState
{
    /// <summary>Imminent Victory: the battle (MatchState.BattleNumber) whose resolution the player's Strategy waits for.</summary>
    public List<PromoResolutionWatch> ResolutionWatches { get; set; } = [];
}

public static partial class PromoEmperorFateAbilities
{
    public const string ClosingToBattle = "pe-closing-the-gap-to-battle", ClosingHome = "pe-closing-the-gap-home", InterrogationDraw = "pe-interrogation-draw", InterrogationNegate = "pe-interrogation-negate",
        WellProtectedBow = "pe-well-protected-bow", WellProtectedDestroy = "pe-well-protected-destroy", ImminentVictory = "pe-imminent-victory";

    /// <summary>The Armor attached to the Personality the held action targets (any bow state / unbowed only).</summary>
    public const string ArmorOfHeldTarget = "pe-armor-of-held-target", UnbowedArmorOfHeldTarget = "pe-unbowed-armor-of-held-target";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorFateReactions(this Dictionary<string, AbilityDefinition> all)
    {
        // Closing the Gap (oracle-l5r-9989): "Reaction: After an action resolves that moved a Personality to or from the current battlefield, choose your performing Personality: Move him to the current
        // battlefield, or from the current battlefield to your home. Straighten his unit if this moved him."
        all[ClosingToBattle] = new("Reaction", "After an action resolves that moved a Personality to or from the current battlefield, choose your performing Personality: Move him to the current battlefield. Straighten his unit if this moved him", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Performer: "own-personality", UsageKey: "pe-closing-the-gap", MovesToBattle: true, Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        all[ClosingHome] = new("Reaction", "After an action resolves that moved a Personality to or from the current battlefield, choose your performing Personality: Move him from the current battlefield to your home. Straighten his unit if this moved him", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Performer: "own-personality", UsageKey: "pe-closing-the-gap", Move: new(MoveSpec.Performer, MoveSpec.Home));
        // Interrogation (oracle-l5r-10239): "Open: Pay 2 Gold: Draw a card." and "Reaction: Before another player's action resolves: Negate its effects that bow, destroy, or discard your Holdings, Regions, or Terrains in play."
        all[InterrogationDraw] = new("Open", "Pay 2 Gold: Draw a card", "none", Cost: GoldCostRule.Fixed(2));
        all[InterrogationNegate] = new("Reaction", "Before another player's action resolves: Negate its effects that bow, destroy, or discard your Holdings, Regions, or Terrains in play", "none", Trigger: "before-action-resolves");
        // Well Protected (oracle-l5r-9288): "Reaction: After another player's action targets your Personality, bow or destroy his target Armor: Negate his destruction from the action's effects."
        all[WellProtectedBow] = new("Reaction", "After another player's action targets your Personality, bow his target Armor: Negate his destruction from the action's effects", UnbowedArmorOfHeldTarget,
            Trigger: ReactionTriggers.TargetedByAction, UsageKey: "pe-well-protected", Costs: ActionCost.BowTarget);
        all[WellProtectedDestroy] = new("Reaction", "After another player's action targets your Personality, destroy his target Armor: Negate his destruction from the action's effects", ArmorOfHeldTarget,
            Trigger: ReactionTriggers.TargetedByAction, UsageKey: "pe-well-protected", Costs: [new(CostVerbs.Sacrifice, CostSubject.Target)]);
        // Imminent Victory (oracle-l5r-10601): "Battle: If you destroy the enemy army or Province in this battle's resolution, give a +1F/+1C token to each of your Personalities in your army (at battle resolution)."
        all[ImminentVictory] = new("Battle", "If you destroy the enemy army or Province in this battle's resolution, give a +1F/+1C token to each of your Personalities in your army (at battle resolution)", "none");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private static bool PromoIsStrategyHeldCard(CardInstance card) => card.Zone == Zone.Hand;

    // ---- Closing the Gap ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // AfterActionResolvedApplies' arm: the window is open for the Reaction when the action moved a Personality to or from the current battlefield. Null: not this feature's Reaction.
    private bool? PromoAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId is not (PromoEmperorFateAbilities.ClosingToBattle or PromoEmperorFateAbilities.ClosingHome)) return null;
        if (source.OwnerId != playerId || _state.CurrentBattleProvince is not int battle) return false;
        return facts.Affected.Any(a => a.Kind == MovedKind && _state.Cards.TryGetValue(a.CardId, out var moved) && Definition(moved).Type == CardType.Personality
            && (a.FromBattlefield == battle || moved.Zone == Zone.Battlefield && moved.ProvinceIndex == battle));
    }

    private void ValidatePromoClosing(string id, GameCommand command, PlayerState player)
    {
        var performer = command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var found) ? found : null;
        if (performer is null) return;
        Require(_state.CurrentBattleProvince is not null, "There is no current battlefield.");
        if (id == PromoEmperorFateAbilities.ClosingToBattle) Require(InPlay(performer) && !AtBattle(performer), "He is already at the current battlefield.");
        else Require(AtBattle(performer) && performer.Zone == Zone.Battlefield, "He is not at the current battlefield.");
    }

    private bool RunPromoClosing(string id, AbilityDefinition ability, GameCommand command, PlayerState player)
    {
        var performer = Card(command.PerformerId!);
        var zoneBefore = performer.Zone; var provinceBefore = performer.ProvinceIndex;
        ExecuteMove(command, ability, null, null);
        // "Straighten his unit if this moved him": a negated movement leaves him where he was and straightens nothing.
        if (InPlay(performer) && (performer.Zone != zoneBefore || performer.ProvinceIndex != provinceBefore)) StraightenUnit(performer);
        return false;
    }

    // ---- Interrogation -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // The Holding, Region or Terrain of the reactor's that the held action targets: the card the negation protects.
    private CardInstance? PromoInterrogationProtected(CardInstance reactor, GameCommand held, string? affectedId)
    {
        if (held.AbilityId is null || affectedId is null || !_state.Cards.TryGetValue(affectedId, out var affected)) return null;
        if (held.PlayerId == reactor.OwnerId || affected.OwnerId != reactor.OwnerId || !InPlay(affected)) return null;
        var kind = Definition(affected).Type;
        var covered = kind is CardType.Holding or CardType.Region || kind == CardType.Strategy && Keyword(affected, "Terrain");
        return covered && ActionMayBowOrDestroy(held.AbilityId, false) && !TargetsThroughAttackPipeline(held.AbilityId) ? affected : null;
    }

    private bool? PromoBeforeActionApplies(CardInstance card, string abilityId, GameCommand command, string? affectedId)
    {
        if (abilityId != PromoEmperorFateAbilities.InterrogationNegate) return null;
        if (card.Zone != Zone.Hand || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        return PromoInterrogationProtected(card, command, affectedId ?? ReactionAffectedId(command)) is not null && !MovementCannotBeNegated(command);
    }

    // ---- Well Protected ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> PromoArmorsOf(CardInstance personality, bool unbowedOnly) =>
        Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Item && Keyword(a, "Armor") && (!unbowedOnly || !a.Bowed)).OrderBy(a => a.Id, StringComparer.Ordinal);

    // "After another player's action targets your Personality": an action that could destroy him (an Attack always could), against a Personality of the reactor's with an Armor to pay with.
    private bool PromoWellProtectedApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality
        && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!)) && PromoArmorsOf(affected, unbowedOnly: abilityId == PromoEmperorFateAbilities.WellProtectedBow).Any();

    private IEnumerable<(string? TargetId, string? Label)>? PromoTargetedReactionChoices(CardInstance card, string abilityId, ReactionSubject subject) =>
        abilityId is PromoEmperorFateAbilities.WellProtectedBow or PromoEmperorFateAbilities.WellProtectedDestroy
            ? PromoArmorsOf(Card(subject.TargetId!), unbowedOnly: abilityId == PromoEmperorFateAbilities.WellProtectedBow)
                .Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})"))
            : null;

    private bool PromoReactionTargetAllows(string category, CardInstance target) =>
        category is PromoEmperorFateAbilities.ArmorOfHeldTarget or PromoEmperorFateAbilities.UnbowedArmorOfHeldTarget
        && OpenReactionSubject() is { TargetId: { } heldId } && _state.Cards.TryGetValue(heldId, out var held) && Definition(held).Type == CardType.Personality
        && Definition(target).Type == CardType.Item && Keyword(target, "Armor") && target.Zone == Zone.Attached && target.AttachedTo == held.Id && (category == PromoEmperorFateAbilities.ArmorOfHeldTarget || !target.Bowed);

    // ---- Imminent Victory --------------------------------------------------------------------------------------------------------------------------------------------------------

    // ContinueResolveBattle's hook, once the resolution has destroyed what it destroys and before the winners go home: each watch of this battle whose player destroyed a unit of the other side or
    // the Province gives each of his Personalities still in his army a +1F/+1C token.
    private void PromoApplyResolution(CardInstance[] attackers, CardInstance[] defenders, int attackerId, int[] destroyedByPlayer, bool provinceDestroyed)
    {
        var watches = PromoFate.ResolutionWatches;
        if (watches.Count == 0) return;
        foreach (var watch in watches.Where(w => w.BattleNumber == _state.BattleNumber).ToArray())
        {
            watches.Remove(watch);
            var destroyedArmyOrProvince = destroyedByPlayer[watch.PlayerId] > 0 || provinceDestroyed && watch.PlayerId == attackerId;
            var name = _state.Cards.TryGetValue(watch.SourceId, out var strategy) ? Definition(strategy).Name : "Imminent Victory";
            if (!destroyedArmyOrProvince) { Emit("resolutionWatch", $"{name}: {Player(watch.PlayerId).Name} destroyed neither the enemy army nor the Province, so nothing is given.", watch.PlayerId); continue; }
            foreach (var personality in (watch.PlayerId == attackerId ? attackers : defenders).Where(c => c.Zone == Zone.Battlefield && Definition(c).Type == CardType.Personality && c.OwnerId == watch.PlayerId))
                CreateTwoStatToken(personality, "Imminent Victory", watch.SourceId, "Force", 1, "Chi", 1);
            Emit("resolutionWatch", $"{name}: each of {Player(watch.PlayerId).Name}'s Personalities in the army was given a +1F/+1C token.", watch.PlayerId);
        }
        watches.RemoveAll(w => w.BattleNumber < _state.BattleNumber);
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunPromoReactions(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoEmperorFateAbilities.ClosingToBattle:
            case PromoEmperorFateAbilities.ClosingHome:
                return RunPromoClosing(id, ability, command, player);
            case PromoEmperorFateAbilities.InterrogationDraw:
                Draw(player);
                return false;
            case PromoEmperorFateAbilities.InterrogationNegate:
            {
                var held = _state.PendingNegatableAbility ?? throw new RuleViolation("No action is open to this reaction.");
                if (PromoInterrogationProtected(source!, held, ReactionAffectedId(held)) is { } protectedCard) NegateEffects(protectedCard, "bow", "destroy");
                return false;
            }
            case PromoEmperorFateAbilities.WellProtectedBow:
            case PromoEmperorFateAbilities.WellProtectedDestroy:
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case PromoEmperorFateAbilities.ImminentVictory:
                PromoFate.ResolutionWatches.Add(new PromoResolutionWatch(player.Id, _state.BattleNumber, source!.Id));
                Emit("resolutionWatchSet", $"{Definition(source).Name}: waiting for this battle's resolution.", player.Id, source.Id);
                return false;
            default:
                return null;
        }
    }
}
