namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), Mendon's rulings on the open questions, group R3.
//
//   * PROVINCE STRENGTH DURATION. "Effects are only permanent if they state that they are permanent, otherwise, the effects last until the end of turn ... Remember that regions that are
//     attached to a province may increase or decrease a province's strength passively. These are 'while attached' effects." Every province strength change of a printed card that does not
//     say "permanently" (Ring of Earth, Tamori Kusugi, Burn the Towers, Besieged, Yoritomo's Guidance, Improved Defenses, Courtesy of the Crane, ...) is a timed province strength
//     (GiveProvinceStrengthThisTurn: SeedsOfDecayBatch2.cs), read by EffectiveProvinceStrength and gone when the turn ends; only Willing Spirits, Boastful Proclamation and Debt to the
//     Kaiu ("permanently") still change the province's own strength. A Region's strength (Fallow Lands) is read live off ProvinceState.RegionId, so it ends with the Region.
//   * THE SINCERITY OF THE DRAGON follows the corrected scan (EmperorRemainingD.cs): a Dragon Clan player condition and "Remove this card from the game".
//   * KAIU VILLAGE (region-wave-10, oracle-l5r-4148): two Reactions of a Region attached to one of the player's provinces, each watching the entry of the player's own Personality FROM A
//     PROVINCE (trigger "own-personality-enters-from-province": Ki-Chang - exp's watcher shape, extended from a Personality to a Region and gated by MatchState.LastProvinceEntryCardId,
//     the marker Yogo Ugimori and Agasha Ryo already read). Each pays 2 Gold and creates an Item attached to the entering Personality: the +1F/+1C Weapon Item (Kaiu Weapon, a Proxy
//     scan Mendon supplied, local id oracle-l5r-10006) or the +1F Armor Item (Kaiu Armor, oracle-l5r-10007).
public static class EmperorRulingR3Abilities
{
    public const string EntersFromProvinceTrigger = "own-personality-enters-from-province";

    public static Dictionary<string, AbilityDefinition> AddEmperorRulingR3(this Dictionary<string, AbilityDefinition> all)
    {
        all["kaiu-village-weapon"] = new("Reaction", "After your performing Personality enters play from a province, pay 2 Gold: Create a +1F/+1C Weapon Item and attach it to him", "none",
            Trigger: EntersFromProvinceTrigger, Cost: GoldCostRule.Fixed(2),
            Creates: new(CardType.Item, Force: 1, Chi: 1, Keywords: ["Weapon"], ProxyId: "oracle-l5r-10006") { HostIsEntered = true });
        all["kaiu-village-armor"] = new("Reaction", "After your performing Personality enters play from a province, pay 2 Gold: Create a +1F Armor Item and attach it to him", "none",
            Trigger: EntersFromProvinceTrigger, Cost: GoldCostRule.Fixed(2),
            Creates: new(CardType.Item, Force: 1, Chi: 0, Keywords: ["Armor"], ProxyId: "oracle-l5r-10007") { HostIsEntered = true });
        // Togashi Hizumi (oracle-l5r-8521, personality-wave-12): "Battle: Give the current battlefield's province +4 or -4 strength." The Battle half of his card, which the wave-12 pass left unwired
        // (its comment said it reuses The Courtesy of the Crane's precedent): the printed choice is two abilities sharing one use, like Kusugi's, performed by Hizumi at the battle, and -- as every
        // province strength change that does not say "permanently" -- it lasts until end of turn.
        all["hizumi-province-raise"] = new("Battle", "Give the current battlefield's province +4 strength", "none", UsageKey: "hizumi-province");
        all["hizumi-province-lower"] = new("Battle", "Give the current battlefield's province -4 strength", "none", UsageKey: "hizumi-province");
        return all;
    }
}

public sealed partial class MatchEngine
{
    // "Enters play from a province": the Personality was the one last brought into play from a province (Purchase, RecruitReserve, an overlay recruit).
    private bool EnteredFromProvince(CardInstance card) => _state.LastProvinceEntryCardId == card.Id;

    // Whether an in-play card's Reaction with this trigger watches the entry of this Personality (InPlayEntryWatchers).
    private bool EntryWatchTriggerApplies(AbilityDefinition ability, CardInstance entered, CardInstance watcher) =>
        ability.Trigger == "own-personality-enters-play" && watcher.Zone != Zone.Region
        || ability.Trigger == EmperorRulingR3Abilities.EntersFromProvinceTrigger && EnteredFromProvince(entered);
}
