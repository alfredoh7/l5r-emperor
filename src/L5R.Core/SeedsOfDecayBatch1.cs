namespace L5R.Core;

// Seeds of Decay pass (issue #453), batch 1: the helpers behind the batch's cards. The standing redirects are StandingRedirects.cs; everything else small is here.
public sealed partial class MatchEngine
{
    // ---- Defensive Blockade: "It does not contribute Force to its army's total Force this battle" ---------------------------------------------------------------
    private bool ForceContributionNegated(CardInstance card) => card.ForceContributionNegatedBattle == _state.BattleNumber && _state.Phase == Phase.Battle;
    private void NegateForceContribution(CardInstance card, string sourceName)
    {
        card.ForceContributionNegatedBattle = _state.BattleNumber;
        Emit("forceContributionNegated", $"{sourceName}: {Definition(card).Name} contributes no Force to its army's total this battle.", card.OwnerId, card.Id);
    }

    // ---- Embracing Battle / Military Promotion: "He may assign even if bowed. Give him Elite" ---------------------------------------------------------------------
    private bool MayAssignBowed(CardInstance card) => card.AssignBowedTurn == _state.TurnNumber;
    private void GrantAssignWhileBowedAndElite(CardInstance personality, CardInstance source)
    {
        personality.AssignBowedTurn = _state.TurnNumber;
        GrantKeyword(personality, "Elite", source.Id, GrantDuration.Turn);
        Emit("assignWhileBowed", $"{Definition(source).Name}: {Definition(personality).Name} may assign even if bowed this turn, and has Elite.", personality.OwnerId, personality.Id);
    }

    // ---- Megumi's Guidance: a shield that outlives an action, "negate his destruction from other players' cards' effects (until the turn ends)" -------------------
    private void ShieldFromDestruction(CardInstance personality, string sourceName)
    {
        personality.DestructionShieldTurn = _state.TurnNumber;
        Emit("destructionShielded", $"{sourceName}: {Definition(personality).Name}'s destruction from other players' cards' effects is negated until the turn ends.", personality.OwnerId, personality.Id);
    }
    // Read where an action's effect destroys a card (DestroyUnits with an acting player; DestroyAttackTarget): another player's effect only. A battle's own resolution is not a card's effect.
    private bool DestructionShielded(CardInstance card, int actingPlayerId)
    {
        if (card.DestructionShieldTurn != _state.TurnNumber || actingPlayerId == card.OwnerId) return false;
        Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated (Megumi's Guidance).", card.OwnerId, card.Id);
        return true;
    }
    // "While your army is opposed, it has +2 total Force, or +3 if you are the Defender": read live at resolution (ArmyForces), like Togashi Noboru's redirect.
    private int CelestialArmyBonus(int playerId, CardInstance[] own, CardInstance[] opposing, bool defending) =>
        own.Length > 0 && opposing.Length > 0 && ControlsCelestialEffect(playerId, "megumis-guidance") ? (defending ? 3 : 2) : 0;

    // ---- Wide Valley (a Terrain): "Before an action's effect destroys a card in a unit: Negate the destruction" ---------------------------------------------------
    // A trait, so mandatory and for either player's cards; it reaches the units at its own battlefield (a Terrain's effects apply where it was played). DestroyUnits asks it only for an
    // action's effect (an acting player is set exactly then); an attachment's destruction is always an action's effect (ActionEffectTracking.cs), so DestroyAttachment asks unconditionally.
    private bool WideValleyNegatesDestruction(CardInstance card)
    {
        if (Definition(card).Type is not (CardType.Personality or CardType.Follower)) return false;
        var host = Card(UnitHostId(card));
        if (host.Zone != Zone.Battlefield || host.ProvinceIndex is not int battlefield || !TerrainsAtBattlefield(battlefield).Any(t => Definition(t).Effect == "wide-valley")) return false;
        Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated by Wide Valley.", card.OwnerId, card.Id);
        return true;
    }

    // ---- The Castle of Order -------------------------------------------------------------------------------------------------------------------------------------
    // "You do not lose Honor from Ninja cards you own": the Stronghold's Effect names the keyword; HonorLossNegated (MatchEngine.cs) already limits it to the losing player's own cards.
    private static readonly Dictionary<string, string> HonorLossKeywordImmunity = new(StringComparer.Ordinal) { ["castle-of-order"] = "Ninja" };
    private bool HonorLossNegatedByKeyword(string strongholdEffect, CardInstance? source) =>
        source is not null && HonorLossKeywordImmunity.TryGetValue(strongholdEffect, out var keyword) && Keyword(source, keyword);
    // "Once per battle": a stamp of the battle number on the Stronghold's own uses (a card's per-turn stamps live under the ability id; this key cannot collide).
    private const string CastleOfOrderBattleKey = "castle-of-order-redirect:battle";
    private bool CastleOfOrderUsedThisBattle(CardInstance stronghold) => stronghold.AbilityUses.TryGetValue(CastleOfOrderBattleKey, out var battle) && battle == _state.BattleNumber;
    private void MarkCastleOfOrderUsed(CardInstance stronghold) => stronghold.AbilityUses[CastleOfOrderBattleKey] = _state.BattleNumber;
    // The reactor's own Ninja cards at the targeted Ninja's location, other than it, that the held action accepts.
    private IEnumerable<CardInstance> CastleOfOrderCandidates(CardInstance stronghold, ReactionSubject subject, CardInstance affected) =>
        _state.Cards.Values.Where(c => c.OwnerId == stronghold.OwnerId && c.Id != affected.Id && InPlay(c) && Keyword(c, "Ninja") && SameLocation(c, affected) && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Shifting Terrain: the controller's Personalities at the Terrain's battlefield, other than the held action's target, that the action accepts -------------------
    private IEnumerable<CardInstance> ShiftingTerrainCandidates(CardInstance terrain, ReactionSubject subject) =>
        terrain.Zone != Zone.Battlefield ? []
        : _state.Cards.Values.Where(c => c.OwnerId == terrain.OwnerId && c.Id != subject.TargetId && Definition(c).Type == CardType.Personality && c.Zone == Zone.Battlefield
                && c.ProvinceIndex == terrain.ProvinceIndex && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Sticks and Stones: "If the Ranged Attack destroyed a card, draw a card; if it destroyed a Follower, you may take an additional Battle action" ---------------
    // Asked at the end of ResolveAttackNow: the target was in play before the Attack and is not after it (a negated or shielded destruction leaves it in play).
    private void AfterSticksAndStonesAttack(string abilityId, int playerId, string? targetId, bool targetInPlayBefore)
    {
        if (abilityId != "sticks-and-stones-attack" || targetId is null || !targetInPlayBefore) return;
        var target = Card(targetId);
        if (InPlay(target)) return;
        Draw(Player(playerId));
        if (Definition(target).Type == CardType.Follower) GrantAdditionalAction(playerId, null);
    }

    // ---- Advanced Intelligence -----------------------------------------------------------------------------------------------------------------------------------
    // "You may only play one copy of Advanced Intelligence per battle": a stamp of the battle number on the player's uses (a copy played leaves the hand, so the card cannot carry it).
    private const string AdvancedIntelligenceBattleKey = "advanced-intelligence:battle";
    private bool AdvancedIntelligencePlayedThisBattle(PlayerState player) => player.AbilityUses.TryGetValue(AdvancedIntelligenceBattleKey, out var battle) && battle == _state.BattleNumber;
    private void MarkAdvancedIntelligencePlayed(PlayerState player) => player.AbilityUses[AdvancedIntelligenceBattleKey] = _state.BattleNumber;
    // "Each action from a Recon Strategy you have taken this battle": counted as each action finishes resolving (FinishActionEffectTracking), the Strategy's keyword read the way every
    // "[keyword] action" classification is (ActionCarriesKeyword). Outside a battle nothing is counted.
    private void CountReconStrategyAction(string abilityId, int playerId, string? sourceId)
    {
        if (_state.Phase != Phase.Battle || sourceId is null || !_state.Cards.TryGetValue(sourceId, out var source) || Definition(source).Type != CardType.Strategy) return;
        if (AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ActionCarriesKeyword(ability, source, "Recon")) _state.ReconStrategyActionsThisBattle[playerId]++;
    }

    // ---- Grappling the Snake -------------------------------------------------------------------------------------------------------------------------------------
    // "After the next time (this turn) an action targets him, you may choose either to negate its effects on your Monk, or to bow the unit of one Personality that performed it after the
    // action resolves." The Battle action grants the Monk two Reactions for the turn (one per choice, sharing one use) that answer any action targeting him in the targeted-by-action
    // window. The choice is spent by the next action that targets him -- used or not -- when that action finishes resolving (FinishGrapplingWatches, called from the points an action
    // is truly done: CompleteAbilityResolution and a deferred Attack's ResolveAttack); the bow it may ask for is applied at that same moment.
    private static readonly string[] GrapplingReactionIds = ["grappling-snake-negate", "grappling-snake-bow"];
    private void GrantGrapplingWatch(CardInstance monk, CardInstance source)
    {
        foreach (var id in GrapplingReactionIds) GrantAbility(monk, id, source.Id, GrantDuration.Turn);
        Emit("grapplingWatch", $"{Definition(source).Name}: after the next time this turn an action targets {Definition(monk).Name}, its controller may negate its effects on him or bow a performer's unit after it resolves.", monk.OwnerId, monk.Id);
    }
    private void SpendGrapplingWatch(CardInstance monk) => monk.GrantedAbilities.RemoveAll(g => GrapplingReactionIds.Contains(g.AbilityId));
    // The Personality who performed an action: the chosen performer when the ability names one, else the source card itself or (an attachment's action) its wielder.
    private CardInstance? ActionPerformingPersonality(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return null;
        CardInstance? actor = ability.Performer is not null && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen
            : command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var source) ? source : null;
        if (actor is { Zone: Zone.Attached, AttachedTo: { } hostId }) actor = Card(hostId);
        return actor is not null && Definition(actor).Type == CardType.Personality && InPlay(actor) ? actor : null;
    }
    private void UseGrapplingWatch(CardInstance monk, bool negate)
    {
        var held = HeldAction();
        if (negate) NegateAllEffectsOn(monk);
        else
        {
            var performer = ActionPerformingPersonality(held.Command) ?? throw new RuleViolation("No Personality performed that action.");
            _state.PendingGrapplingBows.Add(new PendingGrapplingBow(monk.Id, performer.Id, "Grappling the Snake"));
            Emit("grapplingBowPending", $"Grappling the Snake: {Definition(performer).Name}'s unit will bow after the action resolves.", monk.OwnerId, performer.Id);
        }
        SpendGrapplingWatch(monk);
    }
    // The action that targeted a watched Monk has resolved: its pending bow applies, and the Monk's choice is spent whether or not it was used.
    private void FinishGrapplingWatches(GameCommand? command)
    {
        if (command is null) return;
        foreach (var targetId in ReactionWindowTargets(command).OfType<string>().Distinct())
        {
            if (!_state.Cards.TryGetValue(targetId, out var monk)) continue;
            foreach (var pending in _state.PendingGrapplingBows.Where(p => p.MonkId == monk.Id).ToArray())
            {
                _state.PendingGrapplingBows.Remove(pending);
                if (!_state.Cards.TryGetValue(pending.PerformerId, out var performer) || !InPlay(performer)) continue;
                foreach (var member in Attachments(performer.Id).Prepend(performer)) if (!StalwartNegatesBow(member, monk.OwnerId)) member.Bowed = true;
                Emit("unitBowed", $"{pending.SourceName}: {Definition(performer).Name}'s unit bowed after the action resolved.", monk.OwnerId, performer.Id);
            }
            SpendGrapplingWatch(monk);
        }
    }

    // ---- Ceaseless Vigil's Focus Effect: "straighten a Personality" -- a mandatory choice among the bowed Personalities in play (FocusEffectChoice.cs) ----------------
    private static bool IsStraightenFocusEffect(string abilityId) => abilityId == "ceaseless-vigil-focus";
    private List<string> StraightenFocusCandidates() =>
        _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && c.Bowed).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
}
