namespace L5R.Core;

// The engine half of strategy-wave-117 (WlStratAC.cs): target categories, validation, the effects, Lonely Battlefield's static, No Way Out's watch and the hooks that register them.
public sealed partial class MatchEngine
{
    // ---- Target categories (TargetAllows' hook; ValidateSlotCard judges a slot's candidate through the same chain) ---------------------------------------------------------------------------

    private bool WlStratACTargetAllows(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        switch (ability.Target)
        {
            // Desire / Dying Remonstration: "unbowed Human Personality" (Human is never printed: a Personality without Nonhuman).
            case WlStratACAbilities.OwnUnbowedHuman: return personality && own && !target.Bowed && KeywordOrHuman(target, "Human");
            case WlStratACAbilities.EnemyUnbowedHuman: return personality && !own && !target.Bowed && KeywordOrHuman(target, "Human");
            // Expanding the Gardens: a Holding in play (either player's) with a Gold-producing trait.
            case WlStratACAbilities.GoldProducingHolding: return Definition(target).Type == CardType.Holding && HasGoldProducingTrait(Definition(target));
            // Looming Danger: an enemy unit whose total Force is lower than the Force of the Personality chosen to be bowed.
            case WlStratACAbilities.EnemyUnitBelowBowed:
                return personality && !own && SlotCards(command, WlStratACAbilities.BowSlot).FirstOrDefault() is { } bowed && UnitTotalForce(target) < CardForce(bowed);
            // Lost Equipment: an enemy attachment at the current battlefield (an Engage action is a Reaction: the location is checked here).
            case WlStratACAbilities.EnemyAttachmentAtBattle:
                return !own && target.Zone == Zone.Attached && Definition(target).Type is CardType.Follower or CardType.Item or CardType.Spell && AtBattle(target);
            // No Way Out: "attacking cards" -- a Personality of the Attacker's army, or an attachment of one.
            case WlStratACAbilities.AttackingCard:
                return (personality || target.Zone == Zone.Attached && Definition(target).Type is CardType.Follower or CardType.Item or CardType.Spell)
                    && Card(UnitHostId(target)) is { } host && host.Zone == Zone.Battlefield && host.OwnerId == _state.ActivePlayerId && AtBattle(host);
            // Provoked Violence: your unbowed Courtier.
            case WlStratACAbilities.OwnUnbowedCourtier: return personality && own && !target.Bowed && Keyword(target, "Courtier");
            // Suffer the Consequences: an enemy Follower or Weapon.
            case WlStratACAbilities.EnemyFollowerOrWeapon:
                return !own && target.Zone == Zone.Attached && (Definition(target).Type == CardType.Follower || Definition(target).Type == CardType.Item && Keyword(target, "Weapon"));
            // Thoughtless Sacrifice: an enemy unit with Force at most the chosen Personality's, or his Force plus Personal Honor while he is defending.
            case WlStratACAbilities.EnemyUnitWithinSacrifice:
                return personality && !own && SlotCards(command, WlStratACAbilities.SacrificeSlot).FirstOrDefault() is { } sacrifice
                    && UnitTotalForce(target) <= CardForce(sacrifice) + (IsDefendingUnit(sacrifice) ? CardPersonalHonor(sacrifice) : 0);
            // Unchecked Fury: your unbowed Shugenja who is opposed at the current battlefield.
            case WlStratACAbilities.OwnUnbowedOpposedShugenja:
                return personality && own && !target.Bowed && Keyword(target, "Shugenja") && target.Zone == Zone.Battlefield && AtBattle(target)
                    && _state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any();
            default: return false;
        }
    }

    // ---- Validation (ValidateLate's hook) ---------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlStratAC(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case WlStratACAbilities.CenterEnter:
            case WlStratACAbilities.FuryEnter:
                Require(source is { Zone: Zone.Hand } && player.Hand.Contains(source.Id), "A Kata is put into play from your hand.");
                break;
            case WlStratACAbilities.CenterStraighten:
                Require(source is { Zone: Zone.Home }, "This Kata must be in play.");
                Require(command.TargetId is { } ring && _state.Cards.TryGetValue(ring, out var ringCard) && InPlay(ringCard), "Choose a Ring in play.");
                break;
            case WlStratACAbilities.FuryEquip:
                Require(source is { Zone: Zone.Home }, "This Kata must be in play.");
                break;
            case WlStratACAbilities.ProvokedViolenceYojimbo:
                Require(ControlsKeyword(player.Id, "Yojimbo"), "You control no Yojimbo.");
                break;
            case WlStratACAbilities.PlantedEvidence:
                Require(command.TargetId is { } victim && _state.Cards.TryGetValue(victim, out var victimCard) && PersonalityChi(victimCard) <= (ControlsNinjaPersonality(player.Id) ? 4 : 3),
                    "Choose a Personality with 3 or lower Chi (4 or lower if you control a Ninja Personality).");
                break;
            case WlStratACAbilities.TroopsReturn:
                Require(source is { Zone: Zone.FateDiscard } && source.OwnerId == player.Id, "Move the Troops is taken from your discard pile.");
                break;
        }
    }

    private bool ControlsNinjaPersonality(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Ninja"));

    // ---- Effects (RunEffects' hook) -----------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this wave's. false: the effects are done. true: a window opened (an attack's reaction window, an Either/Or, an attach's entering-play window).
    private bool? RunWlStratAC(RunHookContext context)
    {
        var (id, ability, command, player, source, target) = (context.id, context.ability, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case WlStratACAbilities.SmallFavor:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(player.Id, -1, "A Small Favor", source);
                return false;
            case WlStratACAbilities.ReservesGive:
                GrantKeyword(target!, "Reserve", source!.Id, GrantDuration.Turn);
                Emit("reserveGiven", $"{Definition(target!).Name} has Reserve this turn ({Definition(source).Name}).", player.Id, target!.Id);
                return false;
            case WlStratACAbilities.Deliberations:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, context.consumedGrant,
                    strengthOverride: Math.Max(0, PaidGoldAmount(command) - 2) + (target is null ? 0 : AttackDefenseModifier(target, "Ranged")));
            case WlStratACAbilities.LegionRanged1:
            case WlStratACAbilities.LegionRanged3:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, context.consumedGrant);
            case WlStratACAbilities.DesireBow:
                {
                    var mine = SlotCards(command, WlStratACAbilities.OwnSlot).First(); var theirs = SlotCards(command, WlStratACAbilities.EnemySlot).First();
                    if (!StalwartNegatesBow(mine, player.Id)) mine.Bowed = true;
                    SpendHandPlayedCard(player, source);
                    // "His controller may bow him and gain 1 Honor": the decision is the second target's controller's; the first target rides along as the performer.
                    return OpenEitherOr(WlStratACAbilities.DesireSpec, source!, player.Id, theirs.OwnerId, theirs, "action", context.bowedBeforeAction, performerId: mine.Id);
                }
            case WlStratACAbilities.Remonstration:
                {
                    var victim = SlotCards(command, WlStratACAbilities.OwnSlot).First();
                    // The loss reads his Personal Honor as he stands, before he dies; a negative Personal Honor loses nothing.
                    var loss = Math.Clamp(CardPersonalHonor(victim), 0, 4);
                    CommitSeppuku(victim, player.Id);
                    if (SlotPlayer(command, WlStratACAbilities.PlayerSlot) is int loser) GainHonor(loser, -loss, "Dying Remonstration", source);
                    return false;
                }
            case WlStratACAbilities.GardensBoost:
                foreach (var holding in SlotCards(command, WlStratACAbilities.HoldingsSlot).ToArray())
                {
                    ApplyModifier(holding, source!.Id, id, "GoldProduction", 1);
                    Emit("productionChanged", $"{Definition(holding).Name} produces 1 more Gold this turn ({Definition(source).Name}).", player.Id, holding.Id);
                }
                return false;
            case WlStratACAbilities.CenterEnter:
                EnterPlayAsKata(player, source!);
                return false;
            case WlStratACAbilities.FuryEnter:
                WlEnterKataKeepingSameTitle(player, source!);
                return false;
            case WlStratACAbilities.CenterStraighten:
                if (InPlay(target!)) Straighten(target!);
                return false;
            case WlStratACAbilities.LoomingDanger:
                {
                    var bowed = SlotCards(command, WlStratACAbilities.BowSlot).First(); var victim = SlotCards(command, WlStratACAbilities.VictimSlot).First();
                    if (!StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                    if (InPlay(victim)) DestroyUnits([victim], false, player.Id);
                    return false;
                }
            case WlStratACAbilities.LostEquipment:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case WlStratACAbilities.MomentsMove:
            case WlStratACAbilities.WayOfTheWorld:
                {
                    // "If he would be opposed": an enemy unit stands at the current battlefield. A movement that is negated moves nothing, and the other sentence is independent of it.
                    if (_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any()) TryMoveToBattlefield(target!, battlefield);
                    if (id == WlStratACAbilities.MomentsMove && InPlay(target!) && Keyword(target!, "Yojimbo")) GrantKeyword(target!, "Expendable", source!.Id, GrantDuration.Turn);
                    if (id == WlStratACAbilities.WayOfTheWorld && InPlay(target!) && Keyword(target!, "Monk")) ApplyModifier(target!, source!.Id, id, "Force", 2);
                    return false;
                }
            case WlStratACAbilities.TroopsReturn:
                if (source is { Zone: Zone.FateDiscard }) ReturnToHand(source);
                return false;
            case WlStratACAbilities.NoWayOutPlay:
                {
                    if (_state.CurrentBattleProvince is int battlefield && source!.Zone == Zone.Hand)
                    {
                        foreach (var existing in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Keyword(c, "Terrain") && c.Id != source.Id).OrderBy(c => c.Id, StringComparer.Ordinal).Take(1).ToArray())
                            DiscardTerrain(existing);
                        PutTerrainIntoPlay(player, source);
                        _state.WlStratAC.NoWayOut.RemoveAll(w => w.TerrainId == source.Id);
                        _state.WlStratAC.NoWayOut.Add(new(source.Id, _state.BattleNumber, battlefield, player.Id, SlotCards(command, WlStratACAbilities.TargetsSlot).Select(c => c.Id).ToArray(), 1 - _state.ActivePlayerId, CurrentProvinceIndex()));
                    }
                    return false;
                }
            case WlStratACAbilities.PlantedEvidence:
                {
                    var starting = Definition(Card(player.StrongholdId)).StartingHonor;
                    DestroyUnits([target!], false, player.Id);
                    GainHonor(player.Id, -starting, "Planted Evidence", source);
                    return false;
                }
            case WlStratACAbilities.ProvokedViolence:
            case WlStratACAbilities.ProvokedViolenceYojimbo:
                {
                    var courtier = SlotCards(command, WlStratACAbilities.CourtierSlot).First(); var attachment = SlotCards(command, WlStratACAbilities.AttachmentSlot).First();
                    if (!StalwartNegatesBow(courtier, player.Id)) courtier.Bowed = true;
                    if (InPlay(attachment) && attachment.Zone == Zone.Attached) DestroyAttachment(attachment);
                    return false;
                }
            case WlStratACAbilities.SleightOfHand:
                {
                    var personality = SlotCards(command, WlStratACAbilities.PersonalitySlot).First();
                    foreach (var strategy in SlotCards(command, WlStratACAbilities.StrategySlot).ToArray())
                    {
                        if (strategy.Zone != Zone.FateDiscard) continue;
                        ReturnToHand(strategy);
                        Emit("cardReturnedToHand", $"{player.Name} put {Definition(strategy).Name} from the discard pile into their hand.", player.Id, strategy.Id);
                    }
                    if (Keyword(personality, "Magistrate")) GrantAdditionalAction(player.Id, null);
                    return false;
                }
            case WlStratACAbilities.SufferDestroy:
                DestroyAttachment(target!);
                return false;
            case WlStratACAbilities.ThoughtlessSacrifice:
                {
                    var sacrificed = SlotCards(command, WlStratACAbilities.SacrificeSlot).First(); var victim = SlotCards(command, WlStratACAbilities.VictimSlot).First();
                    DestroyUnits([sacrificed, victim], false, player.Id);
                    return false;
                }
            case WlStratACAbilities.FuryEquip:
                // "You may take an additional action": granted first, so the attach's own entering-play window cannot hide it.
                GrantAdditionalAction(player.Id, null);
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            case WlStratACAbilities.UnpleasantTruths:
                ApplyModifier(target!, source!.Id, id, "Force", -Definition(target!).PersonalHonor);
                if (InPlay(target!) && target!.Dishonorable && !StalwartNegatesBow(target, player.Id)) target.Bowed = true;
                return false;
            default:
                return null;
        }
    }

    // Unchecked Fury: "Discard all your other Kata in play with different titles" -- a Kata of the same title stays.
    private void WlEnterKataKeepingSameTitle(PlayerState player, CardInstance kata)
    {
        foreach (var other in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Home && c.Id != kata.Id && Definition(c).Type == CardType.Strategy && Keyword(c, "Kata")
                     && Definition(c).Name != Definition(kata).Name).ToArray())
        {
            ResetCardState(other);
            DiscardFate(player, other);
        }
        player.Hand.Remove(kata.Id); kata.Zone = Zone.Home; kata.FaceUp = true;
        Emit("kataEnteredPlay", $"{player.Name} puts {Definition(kata).Name} into play.", player.Id, kata.Id);
    }

    // ---- Attack kind and strength ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string? WlStratACAttackKind(string abilityId) =>
        abilityId is WlStratACAbilities.Deliberations or WlStratACAbilities.LegionRanged1 or WlStratACAbilities.LegionRanged3 ? "Ranged" : null;

    // Deliberations' strength is priced at resolution from the Gold paid (the override in its case); the Legion's Might's are printed.
    private static int? WlStratACAttackStrength(string abilityId) => abilityId switch
    {
        WlStratACAbilities.Deliberations => 0,
        WlStratACAbilities.LegionRanged1 => 1,
        WlStratACAbilities.LegionRanged3 => 3,
        _ => null
    };

    // ---- Lonely Battlefield (PersonalityForce's hook) -------------------------------------------------------------------------------------------------------------------------------------

    // "Personalities without Followers have -2F. Commanders have +1F." -- for a Personality standing at a battlefield where it is in play, read live, once per such Terrain.
    private int WlStratACLonelyBattlefieldForce(CardInstance card)
    {
        if (card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield || Definition(card).Type != CardType.Personality) return 0;
        var lonely = TerrainsAtBattlefield(battlefield).Count(t => Definition(t).Effect == WlStratACAbilities.LonelyBattlefieldEffect);
        if (lonely == 0) return 0;
        var total = 0;
        if (!Attachments(card.Id).Any(a => Definition(a).Type == CardType.Follower)) total -= 2 * lonely;
        if (Keyword(card, "Commander")) total += lonely;
        return total;
    }

    private static bool WlStratACTerrainReplaces(CardDefinition definition) => definition.Effect == WlStratACAbilities.LonelyBattlefieldEffect;
    // PlayedFromDiscard: Move the Troops is taken from the discard pile but is not removed from the game afterward; it goes to the hand.
    private static bool WlStratACKeepsCardAfterDiscardPlay(AbilityDefinition ability) => ability.FromDiscard && AbilityCatalog.All.TryGetValue(WlStratACAbilities.TroopsReturn, out var troops) && ability == troops;
    private static bool WlStratACTerrainThroughAbility(CardInstance card) => (Definition(card).Abilities ?? []).Contains(WlStratACAbilities.NoWayOutPlay);

    // ---- No Way Out ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "After this battle resolves, if its Province was not destroyed, destroy the targets of this card's action." Called as the battle's after-resolution effects run.
    private void ResolveWlStratACNoWayOut(int? provinceIndex)
    {
        if (_state.WlStratAC.NoWayOut.Count == 0) return;
        var due = _state.WlStratAC.NoWayOut.Where(w => w.BattleNumber == _state.BattleNumber && (provinceIndex is null || w.Battlefield == provinceIndex)).ToArray();
        _state.WlStratAC.NoWayOut.RemoveAll(w => w.BattleNumber != _state.BattleNumber || due.Contains(w));
        foreach (var watch in due)
        {
            if (watch.ProvinceIndex is int index && Player(watch.DefenderId).Provinces[index].Destroyed) continue; // "if its Province was not destroyed"
            var targets = watch.TargetIds.Where(t => _state.Cards.TryGetValue(t, out var c) && InPlay(c)).Select(Card).ToArray();
            foreach (var victim in targets)
            {
                if (!InPlay(victim)) continue;
                DestroyTargetCard(victim, watch.PlayerId);
                Emit("noWayOut", $"No Way Out: {Definition(victim).Name} is destroyed.", watch.PlayerId, victim.Id);
            }
        }
    }

    // ---- Desire's Either/Or ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private string? WlStratACEitherOrPrompt(PendingEitherOr p, string source) =>
        p.SpecId == WlStratACAbilities.DesireSpec ? $"{source}: bow {Definition(Card(p.AffectedCardId!)).Name} and gain 1 Honor, or both targets are dishonored and both their controllers lose 3 Honor." : null;

    private EitherOrOption[]? WlStratACEitherOrOptions(PendingEitherOr p)
    {
        if (p.SpecId != WlStratACAbilities.DesireSpec) return null;
        var affected = Card(p.AffectedCardId!);
        var name = Definition(affected).Name;
        return [
            new("bow", $"Bow {name} and gain 1 Honor", $"{name} is bowed and you gain 1 Honor. Nothing further happens.", EitherOrKind.Avoid, 0, InPlay(affected) && !affected.Bowed, $"{name} cannot be bowed."),
            new("refuse", "Do not bow him", "Both targets are dishonored and both their controllers lose 3 Honor.", EitherOrKind.Accept, 0, true, null)
        ];
    }

    private bool WlStratACApplyEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != WlStratACAbilities.DesireSpec) return false;
        var affected = Card(p.AffectedCardId!);
        var mine = p.PerformerId is { } performer ? Card(performer) : null;
        if (option.Key == "bow")
        {
            affected.Bowed = true;
            Emit("eitherOrChosen", $"{decider.Name} chose to bow {Definition(affected).Name} and gain 1 Honor ({Definition(source).Name}).", decider.Id, affected.Id);
            GainHonor(decider.Id, 1, Definition(source).Name, source);
            return true;
        }
        Emit("eitherOrChosen", $"{decider.Name} did not bow {Definition(affected).Name} ({Definition(source).Name}): both targets are dishonored.", decider.Id, source.Id);
        if (InPlay(affected)) Dishonor(affected, p.ActingPlayerId);
        if (mine is not null && InPlay(mine)) Dishonor(mine, p.ActingPlayerId);
        GainHonor(affected.OwnerId, -3, Definition(source).Name, source);
        if (mine is not null) GainHonor(mine.OwnerId, -3, Definition(source).Name, source);
        return true;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlStratACHooks()
    {
        EngineHooks.Abilities.Register("AddWlStratAC", 14600, static abilities => abilities.AddWlStratAC());
        EngineHooks.TargetAllows.Register("WlStratACTargetAllows", 14601, static (engine, context) => engine.WlStratACTargetAllows(context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidateWlStratAC", 14602, static (engine, context) => engine.ValidateWlStratAC(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunWlStratAC", 14603, static (engine, context) => engine.RunWlStratAC(context));
        EngineHooks.AttackKind.Register("WlStratACAttackKind", 14604, static context => WlStratACAttackKind(context.abilityId));
        EngineHooks.AttackStrengthBefore.Register("WlStratACAttackStrength", 14605, static (engine, context) => WlStratACAttackStrength(context.abilityId));
        EngineHooks.PersonalityForce.Register("WlStratACLonelyBattlefield", 14606, static (engine, context) => engine.WlStratACLonelyBattlefieldForce(context.card));
        EngineHooks.EitherOrPrompt.Register("WlStratACEitherOrPrompt", 14607, static (engine, p, source, affected) => engine.WlStratACEitherOrPrompt(p, source));
        EngineHooks.EitherOrOptions.Register("WlStratACEitherOrOptions", 14608, static (engine, p, affected, source, payable) => engine.WlStratACEitherOrOptions(p));
        EngineHooks.ApplyEitherOr.Register("WlStratACApplyEitherOr", 14609, static (engine, p, option, source, decider, affected, sourceIds) => engine.WlStratACApplyEitherOr(p, option, source, decider));
    }
}
