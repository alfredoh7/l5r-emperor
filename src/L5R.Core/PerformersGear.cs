namespace L5R.Core;

// ======================================================================================================================================================================================
// THE "PERFORMERS" ITEMS (issue #667, item-wave-46): The Blade of the Balash, The Isha's Yumi - exp, Winter Sled and Wyrmbone Katana, wired from the PRINTED text of their Promotional-Emperor scans.
//
//   * THE BLADE OF THE BALASH / THE ISHA'S YUMI - EXP: "Battle: Bow this card or, if you are a Dragon (Mantis) Clan player, discard it from your hand: Melee 5 (Ranged 6) Attack."
//     Mendon's ruling: a Dragon (Mantis) Clan player may attach the card as an Item OR discard it from his hand; discarded from the hand it is treated as a Strategy card and the card itself is the
//     card producing the Attack; attached, it is an Item and its wielder performs the Attack. Two catalogue entries share one use: the attached one (bow this card, the wielder at the battle) and the
//     hand one (discard this card from your hand; only a player of that Stronghold Clan; the Rule of Presence of any Battle action: you need a unit at the current battlefield). The hand Attack has no
//     wielder: its source is the card itself (ResolveOrDeferAttack's sourceId), so no performer-keyed bonus (Shishido, Conflagration) reaches it.
//   * WYRMBONE KATANA: "Battle: Target an enemy card with lower Force than this Personality and no attachments: Destroy it." (the wielder's Force, strictly lower; a Follower or a Personality without
//     attachments); "Battle: Move this Personality to the current battlefield if he assigned there." Mendon's ruling: "assigned there" means he was assigned to the battlefield earlier this phase and has
//     since moved away: the record Back to the Front reads (IvoryMovementState, the battlefields a Personality was assigned to this phase), and he must no longer be at the battlefield.
//   * WINTER SLED: "Reaction: After engaging, if any enemy units are at the current battlefield, bow this card: Move this Personality there." (Camel Mounts' move, an Absent Battle action's shape.)
//     The static half is PerformersSled.cs.
// ======================================================================================================================================================================================

public static class PerformersGearAbilities
{
    public const string BalashAttack = "pf-balash-attack", BalashHand = "pf-balash-attack-hand", YumiAttack = "pf-yumi-attack", YumiHand = "pf-yumi-attack-hand";
    public const string WyrmboneDestroy = "pf-wyrmbone-destroy", WyrmboneMove = "pf-wyrmbone-move", SledMove = "pf-sled-move";

    /// <summary>Target category: an enemy Follower, or an enemy Personality without attachments, whose Force is lower than the Wyrmbone Katana's wielder's.</summary>
    public const string EnemyLowerForceNoAttachments = "pf-enemy-card-lower-force-than-wielder-no-attachments";

    /// <summary>The Effect key of Winter Sled's static trait (CardDefinition.Effect).</summary>
    public const string SledEffect = "pf-winter-sled";

    public static Dictionary<string, AbilityDefinition> AddPerformersGear(this Dictionary<string, AbilityDefinition> all)
    {
        // The Blade of the Balash (oracle-l5r-9975): "Battle: Bow this card or, if you are a Dragon Clan player, discard it from your hand: Melee 5 Attack."
        all[BalashAttack] = new("Battle", "Bow this card: Melee 5 Attack", "attack", UsageKey: "pf-balash", Costs: ActionCost.BowSource);
        all[BalashHand] = new("Battle", "If you are a Dragon Clan player, discard this card from your hand: Melee 5 Attack", "attack", UsageKey: "pf-balash", Costs: [new(CostVerbs.Discard, CostSubject.Source)]);
        // The Isha's Yumi - exp (oracle-l5r-9979): "Battle: Bow this card or, if you are a Mantis Clan player, discard it from your hand: Ranged 6 Attack."
        all[YumiAttack] = new("Battle", "Bow this card: Ranged 6 Attack", "attack", UsageKey: "pf-yumi", Costs: ActionCost.BowSource);
        all[YumiHand] = new("Battle", "If you are a Mantis Clan player, discard this card from your hand: Ranged 6 Attack", "attack", UsageKey: "pf-yumi", Costs: [new(CostVerbs.Discard, CostSubject.Source)]);
        // Wyrmbone Katana (oracle-l5r-9414): "Battle: Target an enemy card with lower Force than this Personality and no attachments: Destroy it." / "Battle: Move this Personality to the current battlefield if he assigned there."
        all[WyrmboneDestroy] = new("Battle", "Target an enemy card with lower Force than this Personality and no attachments: Destroy it", EnemyLowerForceNoAttachments);
        all[WyrmboneMove] = new("Battle", "Move this Personality to the current battlefield if he assigned there", "none", Performer: "own-wielder", MovesToBattle: true,
            Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        // Winter Sled (oracle-l5r-9350): "Reaction: After engaging, if any enemy units are at the current battlefield, bow this card: Move this Personality there."
        all[SledMove] = new("Reaction", "After engaging, if any enemy units are at the current battlefield, bow this card: Move this Personality there", "none", Trigger: "after-engaging",
            Performer: "own-wielder", Costs: ActionCost.BowSource, MovesToBattle: true, Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // AbilitySourceZoneAllows' hook: the hand halves of the Blade and the Yumi are used from their player's hand. Null for every other ability.
    private bool? PerformersSourceZoneAllows(AbilityDefinition ability, CardInstance source, PlayerState player) =>
        ReferenceEquals(ability, AbilityCatalog.All[PerformersGearAbilities.BalashHand]) || ReferenceEquals(ability, AbilityCatalog.All[PerformersGearAbilities.YumiHand])
            ? source.Zone == Zone.Hand && player.Hand.Contains(source.Id) : null;

    private bool PerformersTargetAllows(string category, CardInstance? source, PlayerState player, CardInstance target)
    {
        if (category != PerformersGearAbilities.EnemyLowerForceNoAttachments) return false;
        // "An enemy card ... with no attachments": a Follower, or a Personality without attachments; lower than the wielder's own Force.
        var type = Definition(target).Type;
        return target.OwnerId != player.Id && (type == CardType.Follower && target.Zone == Zone.Attached || type == CardType.Personality && InPlay(target) && !HasAttachments(target))
            && source is not null && WielderOf(source) is { } wielder && CardForce(target) < CardForce(wielder);
    }

    private void ValidatePerformersGear(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PerformersGearAbilities.BalashHand:
                Require(player.Clan == "Dragon", "Only a Dragon Clan player may discard The Blade of the Balash from his hand for its Attack.");
                break;
            case PerformersGearAbilities.YumiHand:
                Require(player.Clan == "Mantis", "Only a Mantis Clan player may discard The Isha's Yumi from his hand for its Attack.");
                break;
            // "Move this Personality to the current battlefield if he assigned there": he was assigned to this battlefield earlier this phase and is no longer there.
            case PerformersGearAbilities.WyrmboneMove:
                {
                    Require(_state.CurrentBattleProvince is not null, "There is no current battlefield.");
                    var wielder = WielderOf(source!) ?? throw new RuleViolation("This card is not attached to a Personality.");
                    Require(!AtBattle(wielder), $"{Definition(wielder).Name} is already at the current battlefield.");
                    Require(IvoryAssignedBattlefields(wielder).Contains(_state.CurrentBattleProvince!.Value), $"{Definition(wielder).Name} was not assigned to the current battlefield this phase.");
                    break;
                }
            // "If any enemy units are at the current battlefield": he is moved there (not already there).
            case PerformersGearAbilities.SledMove:
                {
                    Require(_state.CurrentBattleProvince is int battle && Army(1 - player.Id, battle).Any(), "There are no enemy units at the current battlefield.");
                    var wielder = WielderOf(source!) ?? throw new RuleViolation("This card is not attached to a Personality.");
                    Require(!AtBattle(wielder), $"{Definition(wielder).Name} is already at the current battlefield.");
                    break;
                }
        }
    }

    private static int? PerformersFixedStrength(string abilityId) => abilityId switch
    {
        PerformersGearAbilities.BalashAttack or PerformersGearAbilities.BalashHand => 5,
        PerformersGearAbilities.YumiAttack or PerformersGearAbilities.YumiHand => 6,
        _ => null
    };

    private static string? PerformersAttackKind(string abilityId) => abilityId switch
    {
        PerformersGearAbilities.BalashAttack or PerformersGearAbilities.BalashHand => "Melee",
        PerformersGearAbilities.YumiAttack or PerformersGearAbilities.YumiHand => "Ranged",
        _ => null
    };

    // null: not this feature's. false: the effects are done. true: a window opened (an Attack's reaction window).
    private bool? RunPerformersGear(RunHookContext context)
    {
        var (id, ability, command, player, source, target, consumedGrant) = (context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant);
        switch (id)
        {
            // Attached: an Item's action, performed by its wielder.
            case PerformersGearAbilities.BalashAttack:
            case PerformersGearAbilities.YumiAttack:
                return ResolveOrDeferAttack(id, player, command.TargetId, ActionPerformer(ability, command, source).Id, consumedGrant);
            // From the hand: the card is treated as a Strategy and is itself the card producing the Attack; it has been discarded as the cost.
            case PerformersGearAbilities.BalashHand:
            case PerformersGearAbilities.YumiHand:
                return ResolveOrDeferAttack(id, player, command.TargetId, source!.Id, consumedGrant);
            case PerformersGearAbilities.WyrmboneDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            case PerformersGearAbilities.WyrmboneMove:
            case PerformersGearAbilities.SledMove:
                ExecuteMove(command, ability, source, null);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPerformersGearHooks()
    {
        EngineHooks.Abilities.Register("AddPerformersGear", 14600, static abilities => abilities.AddPerformersGear());
        EngineHooks.TargetAllows.Register("PerformersGearTargetAllows", 14600, static (engine, context) => engine.PerformersTargetAllows(context.ability.Target, context.source, context.player, context.target));
        EngineHooks.ValidateMiddle.Register("ValidatePerformersGear", 14600, static (engine, context) => engine.ValidatePerformersGear(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunPerformersGear", 14600, static (engine, context) => engine.RunPerformersGear(context));
        EngineHooks.AttackStrengthAfter.Register("PerformersGearAttackStrength", 14600, static (engine, context) => PerformersFixedStrength(context.abilityId));
        EngineHooks.AttackKind.Register("PerformersGearAttackKind", 14600, static context => PerformersAttackKind(context.abilityId));
        EngineHooks.DestroysTarget.Register("PerformersGearDestroysTarget", 14600, static context => context.abilityId == PerformersGearAbilities.WyrmboneDestroy);
    }
}
