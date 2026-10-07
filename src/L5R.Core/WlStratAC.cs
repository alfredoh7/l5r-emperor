using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// STRATEGY WAVE 117 (issue #674, cluster "strat-aftermath-coils"): twenty-two Aftermath / Coils of Madness Strategies wired from their OWN scans with the PRINTED wording. Every card is built from
// mechanisms that already exist (the Slots multi-target shape, the Either/Or decision, Kata, Terrain, the attack pipeline, the Engage window, the attach spec); nothing here is a new primitive.
//
//   * A SMALL FAVOR (10926): "Battle, 3: Bow a target Personality. Lose 1 Honor." The 3 is the card's own Gold cost (an implicit Strategy cost).
//   * CONCEALED RESERVES (10932): the target is a face-up Personality in one of your provinces (Two Steady Breaths' target); he is given Reserve (this turn), which the engine's own Reserve recruit
//     (IsReserveRecruitable) already honours as an Absent Battle action.
//   * DELIBERATIONS (10548): "Ranged Attack with strength equal to the amount you paid minus two" -- a Variable Gold cost (read back as PaidGoldAmount) and the Celestial Sword of the Mantis's
//     override of the attack strength. READING: the Gold chosen is at least 3 (a payment of 0 to 2 would make an attack of strength 0 or less).
//   * DESIRE (10934): two Slots (your unbowed Human, another player's unbowed Human); the first is bowed, then the SECOND's controller decides (Either/Or): bow him and gain 1 Honor, or both targets
//     are dishonored and both controllers lose 3 Honor.
//   * DYING REMONSTRATION (10936): your unbowed Human commits seppuku (CommitSeppuku), then the chosen player loses Honor equal to his Personal Honor (read before he dies), at most 4.
//   * EXPANDING THE GARDENS (10937): one or two Gold-producing Holdings each get a +1 GoldProduction modifier (this turn), the Moon's Imperative / Sun's Aspiration modifier.
//   * FIND YOUR CENTER (10939) / UNCHECKED FURY (10962): Kata (KataCards.cs); Find Your Center straightens a Ring in play, Unchecked Fury Equips a Spell from the hand to your unbowed, opposed
//     Shugenja (Ikoma Shinohara's attach with the hand pile, priced as an Equip) and grants an additional action. Unchecked Fury discards only the Kata with a DIFFERENT title.
//   * LONELY BATTLEFIELD (10943): a Terrain; its static reads live through the Personality Force hook (-2F for a Personality at its battlefield without a Follower, +1F for a Commander) and its
//     Battle play replaces a Terrain already there (Hold the Walls' shape).
//   * NO WAY OUT (10546): a Terrain played through its own Battle ability (it names targets, Unstable Ground's shape); the targets are remembered and destroyed after the battle resolves, if the
//     battlefield's Province was not destroyed (a watch in the match state).
//   * LOOMING DANGER (10551): two Slots; your unbowed Personality is bowed, an enemy unit with lower total Force than his current Force is destroyed.
//   * LOST EQUIPMENT (10944): an Engage action (an "after engaging" Reaction, Steal an Advantage's shape) that bows an enemy attachment at the battlefield.
//   * MOMENTS OF DESTINY (10560) / THE WAY OF THE WORLD (10959): move your Personality at home to the current battlefield if he would be opposed there (an action that moves a Personality to a
//     battle needs no presence: MovesToBattle); a Yojimbo gains Expendable (this turn) / a Monk gets +2F (the sentences are independent of the move).
//   * MOVE THE TROOPS (10948): the Open action is taken FROM the discard pile (2 Gold) and puts the card into your hand. READING: "before this phase ends" is a deadline, so the card goes to the
//     hand when the action resolves, and it is not removed from the game afterward.
//   * PLANTED EVIDENCE (10950): "Ninja Limited, 10": destroy a Personality with 3 or lower Chi (4 with a Ninja Personality of yours in play), then lose Honor equal to your Stronghold's Starting
//     Family Honor.
//   * PROVOKED VIOLENCE (10951) / SUFFER THE CONSEQUENCES (10957): the Gold is read as the LEAST amount that reaches the chosen target (a cost derived from the target, so every target shows its
//     price); the Yojimbo's +3 is a second entry of Provoked Violence. Suffer the Consequences is also Kharmic (the engine's own keyword action).
//   * SLEIGHT OF HAND (10954): Seeking the Truth's optional discard-pile Strategy slot beside your opposed Personality; a Magistrate gives an additional action.
//   * THE LEGION'S MIGHT (10958): Ranged 1 Attack, and the Favor Political Battle action that discards the Imperial Favor for a Ranged 3 Attack.
//   * THOUGHTLESS SACRIFICE (10960): your unbowed Personality and an enemy unit whose total Force is at most his Force (plus his Personal Honor while he is defending); both destroyed.
//   * UNPLEASANT TRUTHS (10964): a Force penalty equal to the target's PRINTED Personal Honor, and he is bowed if dishonorable.
//
// Benediction (10930) is in strategy-wave-120 (issue #699, WlBenediction.cs).
// ======================================================================================================================================================================================

public static class WlStratACAbilities
{
    public const string SmallFavor = "wlsac-small-favor", ReservesGive = "wlsac-concealed-reserves", Deliberations = "wlsac-deliberations", DesireBow = "wlsac-desire",
        Remonstration = "wlsac-dying-remonstration", GardensBoost = "wlsac-expanding-gardens", CenterEnter = "wlsac-find-center-enter", CenterStraighten = "wlsac-find-center-straighten",
        LoomingDanger = "wlsac-looming-danger", LostEquipment = "wlsac-lost-equipment", MomentsMove = "wlsac-moments-of-destiny", TroopsReturn = "wlsac-move-the-troops",
        NoWayOutPlay = "wlsac-no-way-out", PlantedEvidence = "wlsac-planted-evidence", ProvokedViolence = "wlsac-provoked-violence", ProvokedViolenceYojimbo = "wlsac-provoked-violence-yojimbo", SleightOfHand = "wlsac-sleight-of-hand",
        SufferDestroy = "wlsac-suffer-the-consequences", LegionRanged1 = "wlsac-legions-might-1", LegionRanged3 = "wlsac-legions-might-3", WayOfTheWorld = "wlsac-way-of-the-world",
        ThoughtlessSacrifice = "wlsac-thoughtless-sacrifice", FuryEnter = "wlsac-unchecked-fury-enter", FuryEquip = "wlsac-unchecked-fury-equip", UnpleasantTruths = "wlsac-unpleasant-truths";
    /// <summary>Lonely Battlefield's Effect: a Terrain read by the Personality Force hook and by PlayStrategy's Terrain replacement.</summary>
    public const string LonelyBattlefieldEffect = "wlsac-lonely-battlefield";
    // Target categories (judged by the TargetAllows hook).
    public const string OwnUnbowedHuman = "wlsac-own-unbowed-human", EnemyUnbowedHuman = "wlsac-enemy-unbowed-human", GoldProducingHolding = "wlsac-gold-producing-holding",
        EnemyUnitBelowBowed = "wlsac-enemy-unit-below-bowed", EnemyAttachmentAtBattle = "wlsac-enemy-attachment-at-battle", AttackingCard = "wlsac-attacking-card",
        OwnUnbowedCourtier = "wlsac-own-unbowed-courtier", EnemyFollowerOrWeapon = "wlsac-enemy-follower-or-weapon",
        EnemyUnitWithinSacrifice = "wlsac-enemy-unit-within-sacrifice", OwnUnbowedOpposedShugenja = "wlsac-own-unbowed-opposed-shugenja";
    /// <summary>The Either/Or Desire hands to the second target's controller.</summary>
    public const string DesireSpec = "wlsac-desire";
    // Slot ids.
    public const string OwnSlot = "own", EnemySlot = "enemy", PlayerSlot = "player", HoldingsSlot = "holdings", BowSlot = "bow", VictimSlot = "victim", TargetsSlot = "targets",
        CourtierSlot = "courtier", AttachmentSlot = "attachment", PersonalitySlot = "personality", StrategySlot = "strategy", SacrificeSlot = "sacrifice";

    public static Dictionary<string, AbilityDefinition> AddWlStratAC(this Dictionary<string, AbilityDefinition> all)
    {
        // A Small Favor: "Battle, 3: Bow a target Personality. Lose 1 Honor."
        all[SmallFavor] = new("Battle", "Bow a target Personality. Lose 1 Honor", "personality");
        // Concealed Reserves: "Battle: Give Reserve to your target Personality in a Province."
        all[ReservesGive] = new("Battle", "Give Reserve to your target Personality in a Province", "own-personality-in-province");
        // Deliberations: "Battle, *: Ranged Attack with strength equal to the amount you paid minus two."
        all[Deliberations] = new("Battle", "Ranged Attack with strength equal to the amount you paid minus two", "attack", Cost: GoldCostRule.Variable(3));
        // Desire: "Political Open: Bow your target unbowed Human Personality. Target another player's unbowed Human Personality. His controller may bow him and gain 1 Honor. If he did not
        // choose this, dishonor both targets, and both their controllers lose 3 Honor."
        all[DesireBow] = new("Open", "Political Open: Bow your target unbowed Human Personality. Target another player's unbowed Human Personality. His controller may bow him and gain 1 Honor. If he did not choose this, dishonor both targets, and both their controllers lose 3 Honor", "slots",
            ActionKeywords: ["Political"],
            Slots: [new(OwnSlot, OwnUnbowedHuman, "Your unbowed Human Personality to bow"), new(EnemySlot, EnemyUnbowedHuman, "Another player's unbowed Human Personality")]);
        // Dying Remonstration: "Political Open: Your target unbowed Human Personality commits seppuku. A target player loses Honor equal to the Personality's Personal Honor or 4, whichever is lower."
        all[Remonstration] = new("Open", "Political Open: Your target unbowed Human Personality commits seppuku. A target player loses Honor equal to the Personality's Personal Honor or 4, whichever is lower", "slots",
            ActionKeywords: ["Political"],
            Slots: [new(OwnSlot, OwnUnbowedHuman, "Your unbowed Human Personality, who commits seppuku"), new(PlayerSlot, "player", "A player who loses Honor")]);
        // Expanding the Gardens: "Limited: Give one or two target Gold-producing Holdings +1 Gold Production each (this turn)."
        all[GardensBoost] = new("Limited", "Give one or two target Gold-producing Holdings +1 Gold Production each (this turn)", "slots",
            Slots: [new(HoldingsSlot, GoldProducingHolding, "One or two Gold-producing Holdings", 1, 2)]);
        // Find Your Center: "Limited: Put this Kata into play. Discard all your other Kata in play." / "Battle: If this card is in play, straighten a Ring."
        all[CenterEnter] = new("Limited", "Put this Kata into play. Discard all your other Kata in play", "none");
        all[CenterStraighten] = new("Battle", "If this card is in play, straighten a Ring", "ring-in-play-or-own-discard");
        // Looming Danger: "Battle: Bow your target unbowed Personality. Destroy a target enemy unit with lower total Force than your Personality's (current) Force."
        all[LoomingDanger] = new("Battle", "Bow your target unbowed Personality. Destroy a target enemy unit with lower total Force than your Personality's (current) Force", "slots",
            Slots: [new(BowSlot, "own-unbowed-personality", "Your unbowed Personality to bow"), new(VictimSlot, EnemyUnitBelowBowed, "An enemy unit with lower total Force than that Personality's Force")]);
        // Lost Equipment: "Engage: Bow a target enemy attachment."
        all[LostEquipment] = new("Reaction", "Engage: Bow a target enemy attachment", EnemyAttachmentAtBattle, Trigger: "after-engaging");
        // Moments of Destiny: "Battle, 1: Target your Personality at home. If he would be opposed, move him to the current battlefield. If he is a Yojimbo, give him Expendable (this turn)."
        all[MomentsMove] = new("Battle", "Target your Personality at home. If he would be opposed, move him to the current battlefield. If he is a Yojimbo, give him Expendable (this turn)", "own-personality-at-home", MovesToBattle: true);
        // Move the Troops: "Open, 2: If this Strategy is in your discard pile, put it into your hand before this phase ends."
        all[TroopsReturn] = new("Open", "If this Strategy is in your discard pile, put it into your hand before this phase ends", "none", FromDiscard: true, Cost: GoldCostRule.Fixed(2));
        // No Way Out: "Battle: Destroy a Terrain (if able). Put this Terrain into play. Target zero to two attacking cards." (the trait is the watch it records).
        all[NoWayOutPlay] = new("Battle", "Destroy a Terrain (if able). Put this Terrain into play. Target zero to two attacking cards", "slots",
            Slots: [new(TargetsSlot, AttackingCard, "Zero to two attacking cards", 0, 2)]);
        // Planted Evidence: "Ninja Limited, 10: Destroy a target Personality with 3 or lower Chi, or with 4 or lower Chi if you control a Ninja Personality. Lose Honor equal to your Starting Family Honor."
        all[PlantedEvidence] = new("Limited", "Ninja Limited: Destroy a target Personality with 3 or lower Chi, or with 4 or lower Chi if you control a Ninja Personality. Lose Honor equal to your Starting Family Honor", "personality",
            ActionKeywords: ["Ninja"]);
        // Provoked Violence: "Political Open, *: Bow your target unbowed Courtier. Destroy a target attachment with Gold Cost less than or equal to the amount of Gold paid, plus 3 if you control a Yojimbo."
        // READING: the amount is the least that reaches the attachment (its Gold Cost, or 3 less with a Yojimbo): paying more can never matter, and a quote derived from the target is offered
        // beside every target, where a free amount would hide each attachment dearer than the default payment. The Yojimbo's 3 is the second entry, offered only while you control one (one use).
        // The attachment is the FIRST slot, so it is the action's primary target and prices the Gold.
        all[ProvokedViolence] = new("Open", "Political Open: Bow your target unbowed Courtier. Destroy a target attachment with Gold Cost less than or equal to the amount of Gold paid", "slots",
            ActionKeywords: ["Political"], UsageKey: "wlsac-provoked-violence", Cost: GoldCostRule.StatValue(CostStatSubject.Target, CostStat.GoldCost, false, 0, 0),
            Slots: [new(AttachmentSlot, "attachment", "An attachment (paying its Gold Cost)"), new(CourtierSlot, OwnUnbowedCourtier, "Your unbowed Courtier to bow")]);
        all[ProvokedViolenceYojimbo] = new("Open", "Political Open: Bow your target unbowed Courtier. Destroy a target attachment with Gold Cost less than or equal to the amount of Gold paid, plus 3 if you control a Yojimbo", "slots",
            ActionKeywords: ["Political"], UsageKey: "wlsac-provoked-violence", Cost: GoldCostRule.StatValue(CostStatSubject.Target, CostStat.GoldCost, false, -3, 0),
            Slots: [new(AttachmentSlot, "attachment", "An attachment (paying 3 less than its Gold Cost)"), new(CourtierSlot, OwnUnbowedCourtier, "Your unbowed Courtier to bow")]);
        // Sleight of Hand: "Battle, 4: Target your opposed Personality. Put a non-Unique Battle Strategy from your discard pile into your hand. If your Personality is a Magistrate, you may take an additional action."
        all[SleightOfHand] = new("Battle", "Target your opposed Personality. Put a non-Unique Battle Strategy from your discard pile into your hand. If your Personality is a Magistrate, you may take an additional action", "slots",
            Slots: [new(PersonalitySlot, "own-opposed", "Your opposed Personality"), new(StrategySlot, "own-nonunique-battle-strategy-in-discard", "A non-Unique Battle Strategy in your discard pile to put into your hand", 0, 1)]);
        // Suffer the Consequences: "Battle, *: Destroy a target enemy Follower or Weapon with Force less than or equal to the amount of Gold paid plus 2." READING: the amount is the least that
        // reaches the target (its Force less 2, at least 0): paying more can never matter, and the quote is derived from the target (Provoked Violence's reading).
        all[SufferDestroy] = new("Battle", "Destroy a target enemy Follower or Weapon with Force less than or equal to the amount of Gold paid plus 2", EnemyFollowerOrWeapon,
            Cost: GoldCostRule.StatValue(CostStatSubject.Target, CostStat.Force, false, -2, 0));
        // The Legion's Might: "Battle: Ranged 1 Attack." / "Favor Political Battle: Discard the Imperial Favor to make a Ranged 3 Attack."
        all[LegionRanged1] = new("Battle", "Ranged 1 Attack", "attack");
        all[LegionRanged3] = new("Battle", "Favor Political Battle: Discard the Imperial Favor to make a Ranged 3 Attack", "attack", ActionKeywords: ["Favor", "Political"], Costs: [new(CostVerbs.DiscardFavor)]);
        // The Way of the World: "Absent Battle: If he would be opposed, move your target Personality at home to the current battlefield. If he is a Monk, give him +2F."
        all[WayOfTheWorld] = new("Battle", "Absent Battle: If he would be opposed, move your target Personality at home to the current battlefield. If he is a Monk, give him +2F", "own-personality-at-home", Absent: true, MovesToBattle: true);
        // Thoughtless Sacrifice: "Battle: Target your unbowed Personality. Target an enemy unit with Force equal to or lower than your Personality's Force, or Force plus Personal Honor if he is defending. Destroy both targets."
        all[ThoughtlessSacrifice] = new("Battle", "Target your unbowed Personality. Target an enemy unit with Force equal to or lower than your Personality's Force, or Force plus Personal Honor if he is defending. Destroy both targets", "slots",
            Slots: [new(SacrificeSlot, "own-unbowed-personality", "Your unbowed Personality"), new(VictimSlot, EnemyUnitWithinSacrifice, "An enemy unit whose Force is within his Force (plus Personal Honor while defending)")]);
        // Unchecked Fury: "Limited: Put this Kata into play. Discard all your other Kata in play with different titles." / "Battle, *: If this card is in play, Equip a Spell to your target unbowed, opposed Shugenja.
        // You may take an additional action." The * is the Spell's Equip price (Ikoma Shinohara's attach, with a Spell from the hand).
        all[FuryEnter] = new("Limited", "Put this Kata into play. Discard all your other Kata in play with different titles", "none");
        all[FuryEquip] = new("Battle", "If this card is in play, Equip a Spell to your target unbowed, opposed Shugenja. You may take an additional action", "slots",
            Slots: [new(AttachmentSlot, "attachment-in-discard", "A Spell in your hand"), new(PersonalitySlot, OwnUnbowedOpposedShugenja, "Your unbowed, opposed Shugenja")],
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.PersonalitySlot, [CardType.Spell]));
        // Unpleasant Truths: "Battle: Give a target Personality a Force penalty equal to his printed Personal Honor. If he is dishonorable, bow him."
        all[UnpleasantTruths] = new("Battle", "Give a target Personality a Force penalty equal to his printed Personal Honor. If he is dishonorable, bow him", "personality");
        return all;
    }
}

/// <summary>No Way Out's watch: the Terrain, the battle and battlefield it was played in, the player who played it, the cards it targeted, and the Defender's province at the battlefield (if it has one), destroyed once the battle has resolved if the Province stands.</summary>
public sealed record WlStratACNoWayOutWatch(string TerrainId, int BattleNumber, int Battlefield, int PlayerId, string[] TargetIds, int DefenderId, int? ProvinceIndex);

public sealed class WlStratACState
{
    public List<WlStratACNoWayOutWatch> NoWayOut { get; set; } = [];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(20600)]
    public WlStratACState WlStratAC { get; set; } = new();
}
