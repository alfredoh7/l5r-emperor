namespace L5R.Core;

// ======================================================================================================================================================================================
// TWENTY FESTIVALS GEAR (issue #622, batch "tf-gear"): sixteen Followers, Items and Spells that carry a Twenty Festivals (Ivory Edition) label in the inventory but were first printed in the Emperor
// era (Aftermath, Gates of Chaos, Coils of Madness, The Shadow's Embrace, Emperor Edition). They are wired from the PRINTED text of that earliest Emperor-era scan, not from Oracle's later template.
//
//   FOLLOWERS  Band of Brothers (create a 1F Follower for each of one or two Personalities), Camel Mounts (attach to a Human, gives Cavalry, a Home Battle move), Lookout (Ninja Reaction: one Ranged Attack
//              of the announced action may target a card in the Defender's home -- No Hiding Place's record, without its -1 strength).
//   ITEMS      Lost Blade of the Maharaja, Ominous Armor (a replacement: destroy this Item instead of the Personality), Red Hunger's Fang (entry Honor loss, Poison token), Seppun's Blessed Blade (a Force
//              modifier equal to the wielder's Personal Honor), Tiger Claw (Melee 3), Versatile Blade (discard a card to straighten him / to move him to the battlefield).
//   SPELLS     Conflagration, Hitomi's Devotion, Kiyoshi's Wrath, Sailor's Warning, Steal the Candle's Flame, The Thunder Resounds, Typhoon Surge.
//
// READINGS (each is also recorded in the batch report):
//   * "Bow" in a printed cost is the bow icon: bow THIS card (the Item or Spell), as every other wired Item/Spell reads it. The action itself is performed by the wielder.
//   * A Home action may be taken from home (Coils of Madness supplement); the moves that bring a unit to the current battlefield are Absent Battle actions for either player (MovesToBattle, the standing
//     ruling), usable only while the unit WOULD BE OPPOSED there (an enemy unit is at the battlefield) and is not already there.
//   * Steal the Candle's Flame's "Interrupt" plays exactly like a Reaction to "before an action's effects resolve" (Coils of Madness supplement). The Attack it boosts has been announced and targeted, so it is
//     the Attack reaction window (Precision's shape): any Melee or Ranged Attack, either player's.
//   * Sailor's Warning names its Personality first and the Attack's target afterwards (SequencedTarget.cs, Engulfing Flames' ruling); the Spell is destroyed as the effects begin.
//   * Typhoon Surge's "Straighten his unit if you do not control him" is a sentence of its own, so it does not depend on the move having happened.
//   * Hitomi's Devotion: the Spell's unit is skipped by the battle resolution's destruction sweep; the Spell's own wielder decides "if this Shugenja is Earth" for the attacking side.
//   * Ominous Armor: "another player's card destroys this Personality" is a destruction that comes from an action (an acting player other than the Personality's controller); a battle's own casualty sweep
//     and the 0-Chi rule are not "a card" and are not replaced. The Item is destroyed through the ordinary attachment destruction (so it can be negated like any other).
// ======================================================================================================================================================================================

public static class TfGearAbilities
{
    public const string BandCreate = "tf-band-brothers-create", CamelMove = "tf-camel-mounts-move", LookoutDestroy = "tf-lookout-destroy",
        LostBladeBow = "tf-lost-blade-bow", RedHungerPoison = "tf-red-hunger-poison", TigerClawAttack = "tf-tiger-claw-attack",
        VersatileStraighten = "tf-versatile-straighten", VersatileMove = "tf-versatile-move",
        ConflagrationBoost = "tf-conflagration-boost", KiyoshiBow = "tf-kiyoshi-bow", SailorWarning = "tf-sailors-warning", SailorAttack = "tf-sailors-warning-attack",
        StealInterrupt = "tf-steal-candle-interrupt", StealAttack = "tf-steal-candle-attack", ThunderResoundsCopy = "tf-thunder-resounds-copy", TyphoonMove = "tf-typhoon-surge-move";

    /// <summary>Target categories: a Personality (either player's) with Force equal to or lower than the wielder's; an enemy Follower or Personality with 4 or lower Force; a Spell at the battlefield (or a Thunder Spell anywhere).</summary>
    public const string ForceLteWielder = "tf-personality-force-lte-wielder", EnemyForceLte4 = "tf-enemy-force-lte-4", SpellForCopy = "tf-spell-for-copy";

    /// <summary>The Effect keys of the static traits (CardDefinition.Effect).</summary>
    public const string ConflagrationEffect = "tf-conflagration", DevotionEffect = "tf-hitomis-devotion", OminousArmorEffect = "tf-ominous-armor", SeppunEffect = "tf-seppuns-blessed-blade";

    /// <summary>The Followers an Aftermath scan creates are Farmlands Conscripts' scan (1F, no keywords), like every other "create a 1F Follower".</summary>
    public const string CreatedFollowerScan = "oracle-l5r-10004";

    public static Dictionary<string, AbilityDefinition> AddTfGear(this Dictionary<string, AbilityDefinition> all)
    {
        // Band of Brothers (oracle-l5r-10891, Aftermath): "After this Follower enters play, target one or two of your Personalities. Create and attach a 1F Follower to each of them." Makubesu's shape:
        // an entering-play window with a one-or-two Personality slot, one created Follower for each.
        all[BandCreate] = new("Reaction", "After this Follower enters play, target one or two of your Personalities: Create and attach a 1F Follower to each of them", "slots", Trigger: "own-enters-play",
            Slots: [new("hosts", "own-personality", "One or two of your Personalities", 1, 2)],
            Creates: new(CardType.Follower, Force: 1, ProxyId: CreatedFollowerScan, Count: 1, PerHost: true));
        // Camel Mounts (oracle-l5r-10682, Gates of Chaos): "Home Battle: Move this unit to the current battlefield if it would be opposed there."
        all[CamelMove] = new("Battle", "Home Battle: Move this unit to the current battlefield if it would be opposed there", "none", Performer: "own-wielder", MovesToBattle: true,
            Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        // Lookout (oracle-l5r-9870, The Shadow's Embrace): "Reaction: After you announce a Ranged Attack action, destroy this card: One Ranged Attack from that action may target a card in the Defender's home."
        all[LookoutDestroy] = new("Reaction", "After you announce a Ranged Attack action, destroy this card: One Ranged Attack from that action may target a card in the Defender's home", "none",
            Trigger: "own-ranged-action-announced", Costs: [new(CostVerbs.Sacrifice)]);
        // Lost Blade of the Maharaja (oracle-l5r-10693, Gates of Chaos): "Battle, bow: Bow a target Personality with Force equal to or lower than this Personality's."
        all[LostBladeBow] = new("Battle", "Bow this card: Bow a target Personality with Force equal to or lower than this Personality's", ForceLteWielder, Costs: ActionCost.BowSource);
        // Red Hunger's Fang (oracle-l5r-10696, Gates of Chaos): "After this Item enters play, lose 3 Honor" is its EntryHonorLoss; "Battle, bow: Give a target enemy Personality a -1F/-1C Poison token."
        all[RedHungerPoison] = new("Battle", "Bow this card: Give a target enemy Personality a -1F/-1C Poison token", "enemy-personality", Costs: ActionCost.BowSource);
        // Tiger Claw (oracle-l5r-10910, Aftermath): "Battle, bow: Melee 3 Attack."
        all[TigerClawAttack] = new("Battle", "Bow this card: Melee 3 Attack", "attack", Costs: ActionCost.BowSource);
        // Versatile Blade (oracle-l5r-10912, Aftermath): "Battle: Discard a card to straighten this Personality." / "Home Battle: If this Personality would be opposed, discard a card to move him to the current battlefield."
        all[VersatileStraighten] = new("Battle", "Discard a card to straighten this Personality", "hand", Costs: ActionCost.DiscardTarget);
        all[VersatileMove] = new("Battle", "Home Battle: If this Personality would be opposed, discard a card to move him to the current battlefield", "hand", Performer: "own-wielder",
            Costs: ActionCost.DiscardTarget, MovesToBattle: true, Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        // Conflagration (oracle-l5r-1449, Emperor Edition): "While this Shugenja is Fire, Ranged Attacks from actions he performs have +1 strength" is its Effect (TfRangedBonus); "Battle: Bow this card:
        // Give this Shugenja +3F."
        all[ConflagrationBoost] = new("Battle", "Bow this card: Give this Shugenja +3F", "none", Costs: ActionCost.BowSource);
        // Kiyoshi's Wrath (oracle-l5r-10701, Gates of Chaos): "Earth Battle, bow: Bow a target enemy Follower or Personality with 4 or lower Force."
        all[KiyoshiBow] = new("Battle", "Earth Battle, bow this card: Bow a target enemy Follower or Personality with 4 or lower Force", EnemyForceLte4, ActionKeywords: ["Earth"], Costs: ActionCost.BowSource);
        // Sailor's Warning (oracle-l5r-10920, Aftermath): "Thunder Battle, bow: Target your Personality. Ranged Attack with strength equal to his Chi. Destroy this Spell." The Personality is named as the action is
        // announced; the Attack's target is chosen after it (the second half's own id, never offered as an action of its own).
        all[SailorWarning] = new("Battle", "Thunder Battle, bow this card: Target your Personality. Ranged Attack with strength equal to his Chi. Destroy this Spell", "slots", ActionKeywords: ["Thunder"], Costs: ActionCost.BowSource,
            Slots: [new("personality", "own-personality", "Your Personality, whose Chi is the Attack's strength")]);
        all[SailorAttack] = new("Battle", "Ranged Attack with strength equal to the targeted Personality's Chi (the second half of Sailor's Warning)", "attack");
        // Steal the Candle's Flame (oracle-l5r-10704, Gates of Chaos): "Fire Interrupt, bow: Give a Melee or Ranged Attack +2 strength." / "Fire Battle: Ranged 0 Attack."
        all[StealInterrupt] = new("Reaction", "Fire Interrupt, bow this card: Give a Melee or Ranged Attack +2 strength", "none", Trigger: "before-attack-resolves", ActionKeywords: ["Fire"], Costs: ActionCost.BowSource);
        all[StealAttack] = new("Battle", "Fire Battle: Ranged 0 Attack", "attack", ActionKeywords: ["Fire"]);
        // The Thunder Resounds (oracle-l5r-10699, Gates of Chaos): "Thunder Battle: Target a Spell at this battlefield, or at any location if the target is Thunder. Copy an ability on it that does not itself copy
        // ability, to this Spell (until the turn ends)."
        all[ThunderResoundsCopy] = new("Battle", "Thunder Battle: Target a Spell at this battlefield, or at any location if the target is Thunder. Copy an ability on it that does not itself copy ability, to this Spell (until the turn ends)",
            SpellForCopy, ActionKeywords: ["Thunder"], CopiesAbilities: true);
        // Typhoon Surge (oracle-l5r-10543, Coils of Madness): "Water Battle: Move a target Personality at any location to the current battlefield. Straighten his unit if you do not control him. Destroy this Spell."
        all[TyphoonMove] = new("Battle", "Water Battle: Move a target Personality at any location to the current battlefield. Straighten his unit if you do not control him. Destroy this Spell", "personality-at-any-location",
            ActionKeywords: ["Water"], MovesToBattle: true, Move: new(MoveSpec.Target, MoveSpec.CurrentBattlefield));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------------------------

    private bool TfTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var own = target.OwnerId == player.Id;
        switch (category)
        {
            // Lost Blade: "a target Personality with Force equal to or lower than this Personality's" -- any player's, the wielder himself included.
            case TfGearAbilities.ForceLteWielder:
                return type == CardType.Personality && source is { Zone: Zone.Attached, AttachedTo: { } wielderId } && _state.Cards.TryGetValue(wielderId, out var wielder) && CardForce(target) <= CardForce(wielder);
            // Kiyoshi's Wrath: "a target enemy Follower or Personality with 4 or lower Force" (its current Force).
            case TfGearAbilities.EnemyForceLte4:
                return !own && (type == CardType.Personality || type == CardType.Follower && target.Zone == Zone.Attached) && CardForce(target) <= 4;
            // The Thunder Resounds: a Spell in play (not this one: a card never copies its own abilities) at this battlefield, or anywhere when it is Thunder.
            case TfGearAbilities.SpellForCopy:
                return type == CardType.Spell && target.Zone == Zone.Attached && source is not null && target.Id != source.Id && (AtBattle(target) || Keyword(target, "Thunder"));
            default:
                return false;
        }
    }

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateTfGear(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // "If it would be opposed there": an enemy unit is at the current battlefield, and the unit is not already there (a Home action may be taken from home).
            case TfGearAbilities.CamelMove:
            case TfGearAbilities.VersatileMove:
                {
                    Require(_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any(), "This unit would not be opposed at the current battlefield.");
                    Require(WielderOf(source!) is { } host && !AtBattle(host), "This unit is already at the current battlefield.");
                    break;
                }
            // The Interrupt boosts an Attack that is pending: a Melee or Ranged Attack.
            case TfGearAbilities.StealInterrupt:
                Require(_state.PendingAttack is { Kind: "Ranged" or "Melee" }, "There is no Melee or Ranged Attack to give strength to.");
                break;
        }
    }

    // ---- attacks ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private int? TfAttackStrength(string abilityId) => abilityId switch
    {
        TfGearAbilities.TigerClawAttack => 3,
        TfGearAbilities.StealAttack => 0,
        // "Strength equal to his Chi": the Personality named in the action's first slot (the running command is the paused action while the Attack is aimed).
        TfGearAbilities.SailorAttack => _runningCommand is { } running && running.Selections?.FirstOrDefault(s => s.Slot == "personality")?.CardIds?.FirstOrDefault() is { } chosen
            && _state.Cards.TryGetValue(chosen, out var personality) && InPlay(personality) ? PersonalityChi(personality) : 0,
        _ => null
    };

    private static string? TfAttackKind(string abilityId) => abilityId switch
    {
        TfGearAbilities.TigerClawAttack => "Melee",
        TfGearAbilities.StealAttack or TfGearAbilities.SailorAttack => "Ranged",
        _ => null
    };

    // Conflagration: "While this Shugenja is Fire, Ranged Attacks from actions he performs have +1 strength." A term of every Ranged Attack's strength (EffectiveAttackStrength, ResolveOrDeferAttack).
    private int TfRangedBonus(string abilityId, string? performerId)
    {
        if (performerId is null || !_state.Cards.TryGetValue(performerId, out var performer) || AttackKind(abilityId) != "Ranged") return 0;
        var shugenja = ActingPersonality(performer);
        if (Definition(shugenja).Type != CardType.Personality || !Keyword(shugenja, "Fire")) return 0;
        return Attachments(shugenja.Id).Count(a => Definition(a).Type == CardType.Spell && Definition(a).Effect == TfGearAbilities.ConflagrationEffect);
    }

    // ---- Force -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // Seppun's Blessed Blade: "This Item's Force modifier equals this Personality's Personal Honor." A Dishonorable wielder's Personal Honor is 0 (CardPersonalHonor).
    private int TfSeppunForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return 0;
        var blades = Attachments(card.Id).Count(a => Definition(a).Type == CardType.Item && Definition(a).Effect == TfGearAbilities.SeppunEffect);
        return blades == 0 ? 0 : blades * CardPersonalHonor(card);
    }

    // ---- destruction -----------------------------------------------------------------------------------------------------------------------------------------------------

    // Ominous Armor: "Before another player's card destroys this Personality, destroy this Item instead." Called by DestroyUnits for a destruction an action's effect causes (actingPlayerId).
    private bool TfOminousArmorSubstitutes(CardInstance card, int actingPlayerId)
    {
        if (Definition(card).Type != CardType.Personality || card.OwnerId == actingPlayerId) return false;
        var armor = Attachments(card.Id).FirstOrDefault(a => Definition(a).Effect == TfGearAbilities.OminousArmorEffect);
        if (armor is null) return false;
        Emit("destructionReplaced", $"{Definition(armor).Name}: {Definition(card).Name} is not destroyed; the Armor is destroyed instead.", card.OwnerId, armor.Id);
        DestroyAttachment(armor);
        return true;
    }

    // Hitomi's Devotion: "Battle resolution will not destroy cards in this unit while defending and, if this Shugenja is Earth, while attacking." The army's units the resolution may destroy.
    private CardInstance[] TfResolutionDestroyable(CardInstance[] army, bool defending) =>
        army.Where(unit => !(Attachments(unit.Id).Any(a => Definition(a).Type == CardType.Spell && Definition(a).Effect == TfGearAbilities.DevotionEffect) && (defending || Keyword(unit, "Earth")))).ToArray();

    // ---- Lookout ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // The record on the announced action: one Ranged Attack from it may target a card in the Defender's home (No Hiding Place's permission, with no strength change).
    private void TfLookoutReaction(PlayerState player, CardInstance source)
    {
        var announced = _state.PendingAnnouncedAction ?? throw new RuleViolation("No action was announced.");
        _state.AnnouncedAttackModifiers.RemoveAll(m => m.Battle != _state.BattleNumber);
        _state.AnnouncedAttackModifiers.Add(new AnnouncedAttackModifier
        {
            PlayerId = announced.PlayerId,
            ActionCardId = announced.CardId,
            ActionAbilityId = announced.AbilityId,
            StrengthDelta = 0,
            MayTargetDefenderHome = true,
            ModifierSourceId = source.Id,
            Battle = _state.BattleNumber
        });
        Emit("announcedAttackModified", $"{Definition(source).Name}: one Ranged Attack from {player.Name}'s action may target a card in the Defender's home.", player.Id, source.Id);
    }

    // ---- execution -------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this batch's. false: the effects are done. true: a window opened (an Attack's reaction window, a sequenced target, a copy choice).
    private bool? RunTfGear(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case TfGearAbilities.CamelMove:
            case TfGearAbilities.VersatileMove:
                ExecuteMove(command, ability, source, target);
                return false;
            case TfGearAbilities.LookoutDestroy:
                TfLookoutReaction(player, source!);
                return false;
            case TfGearAbilities.LostBladeBow:
            case TfGearAbilities.KiyoshiBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case TfGearAbilities.RedHungerPoison:
                CreateTwoStatToken(target!, "Poison", source!.Id, "Force", -1, "Chi", -1);
                return false;
            case TfGearAbilities.TigerClawAttack:
            case TfGearAbilities.StealAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case TfGearAbilities.VersatileStraighten:
                if (WielderOf(source!) is { } wielder) Straighten(wielder);
                return false;
            case TfGearAbilities.ConflagrationBoost:
                if (WielderOf(source!) is { } shugenja) ApplyModifier(shugenja, source!.Id, id, "Force", 3);
                return false;
            case TfGearAbilities.SailorWarning:
                {
                    // "Destroy this Spell" as the effects begin; the Personality's Chi is read when the Attack is aimed.
                    var performer = ActionPerformer(ability, command, source);
                    if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                    return BeginSequencedAttackTarget(TfGearAbilities.SailorAttack, command, performer, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case TfGearAbilities.StealInterrupt:
                _state.PendingAttack!.Strength += 2;
                Emit("attackStrengthModified", $"{Definition(source!).Name}: this {_state.PendingAttack.Kind} Attack has +2 strength.", player.Id, source!.Id);
                return false;
            case TfGearAbilities.ThunderResoundsCopy:
                // The Spell copies one ability (not a copying one) of the target for this turn; with one candidate it is taken, with several the player chooses (CardCopy.cs).
                return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", bowedBeforeAction, GrantDuration.Turn);
            case TfGearAbilities.TyphoonMove:
                ExecuteMove(command, ability, source, target);
                if (InPlay(target!) && target!.OwnerId != player.Id) StraightenUnit(target);
                if (source is { Zone: Zone.Attached }) DestroyAttachment(source);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfGearHooks()
    {
        EngineHooks.Abilities.Register("AddTfGear", 11600, static abilities => abilities.AddTfGear());
        EngineHooks.TargetAllows.Register("TfGearTargetAllows", 11600, static (engine, context) => engine.TfTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidateTfGear", 11600, static (engine, context) => engine.ValidateTfGear(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunTfGear", 11600, static (engine, context) => engine.RunTfGear(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant,
            context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.AttackStrengthAfter.Register("TfGearAttackStrength", 11600, static (engine, context) => engine.TfAttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("TfGearAttackKind", 11600, static context => TfAttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("TfGearBowsTarget", 11600, static context => context.abilityId is TfGearAbilities.LostBladeBow or TfGearAbilities.KiyoshiBow);
        EngineHooks.PersonalityForce.Register("TfGearSeppunForce", 11600, static (engine, context) => engine.TfSeppunForce(context.card));
    }
}
