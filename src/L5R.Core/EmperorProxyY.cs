namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), proxy group Y (personality-wave-77): Akodo Makotai, Shinjo Ki-Chang - experienced and Kitsune Gina, with the three Proxies they
// create (Infantry Follower, Dutiful Cavalry, Gina's Fox), all three previously blocked only on their Proxy's missing scan. Each Proxy is published CreatedOnly in the same wave as its
// creator (the personality-wave-64 / item-wave-6 precedent). What is new is small:
//   * a Reaction printed "After your target Personality enters play" that is a Personality's own in-play trait-of-the-table (trigger "own-personality-enters-play", the trigger The Spider's
//     Web already uses for a Stronghold), offered in the entry window of any of its controller's Personalities -- himself included -- and creating a card attached to the entering
//     Personality (CreatedCardSpec.HostIsEntered);
//   * Gina's Fox's trait, a battlefield-scoped Force term on the enemy's attacking Personalities and Followers.
public static class EmperorProxyYAbilities
{
    public const string GinasFoxEffect = "proxy-y-ginas-fox";

    public static Dictionary<string, AbilityDefinition> AddEmperorProxyY(this Dictionary<string, AbilityDefinition> all)
    {
        // Akodo Makotai (oracle-l5r-241): "Tactical Limited: Create a 2F Follower and attach it to Makotai." The Infantry Follower Proxy (oracle-l5r-9738, printing 4: 2F, no keywords), attached
        // to the printing card itself (HostIsSource). A Tactical action: only a Tactician may perform it, and Makotai is one (the ordinary once-a-turn cap on a Tactician's Tactical actions).
        all["makotai-create"] = new("Limited", "Tactical Limited: Create a 2F Follower and attach it to Makotai", "none", ActionKeywords: ["Tactical"],
            Creates: new(CardType.Follower, Force: 2, ProxyId: "oracle-l5r-9738") { HostIsSource = true });
        // Shinjo Ki-Chang - exp (oracle-l5r-6905): "Reaction: After your target Personality enters play: Create a 1F Cavalry Follower and attach it to him." Dutiful Cavalry (oracle-l5r-10003).
        // "Your target Personality" is one of the controller's Personalities as it enters play -- Ki-Chang's own entry included -- and "him" is that Personality, not Ki-Chang.
        all["kichang-exp-create"] = new("Reaction", "After your target Personality enters play: Create a 1F Cavalry Follower and attach it to him", "none", Trigger: "own-personality-enters-play",
            Creates: new(CardType.Follower, Force: 1, Keywords: ["Cavalry"], ProxyId: "oracle-l5r-10003") { HostIsEntered = true });
        // Kitsune Gina (oracle-l5r-4460): "Reaction: After Gina enters play: Create a 2F/2C/3PH Mantis Clan - Nonhuman - Fox - Spirit Personality with the trait, 'Enemy attacking Personalities
        // and Followers at this card's battlefield have -1F'." Gina's Fox (oracle-l5r-10005), whose trait is its Effect (GinasFoxForce).
        all["gina-create"] = new("Reaction", "After Gina enters play: Create a 2F/2C/3PH Mantis Clan - Nonhuman - Fox - Spirit Personality with the trait, 'Enemy attacking Personalities and Followers at this card's battlefield have -1F'", "none",
            Trigger: "own-enters-play",
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3, Keywords: ["Mantis Clan", "Nonhuman", "Fox", "Spirit"], ProxyId: "oracle-l5r-10005"));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // The in-play Personalities of the player that watch the entry of one of the player's own Personalities (Shinjo Ki-Chang - exp). The entering Personality counts as watching himself: he is
    // in play by the time his window opens.
    private IEnumerable<(CardInstance Card, string AbilityId)> InPlayEntryWatchers(int playerId, CardInstance entered)
    {
        if (Definition(entered).Type != CardType.Personality || entered.OwnerId != playerId) yield break;
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var abilityId in AbilityIdsOf(card))
                if (AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Timing == "Reaction" && ability.Trigger == "own-personality-enters-play") yield return (card, abilityId);
    }

    // Gina's Fox: "Enemy attacking Personalities and Followers at this card's battlefield have -1F." Read live for the card: every Fox in play at the battlefield of the battle this card is
    // attacking at, controlled by the card's enemy (the defender), takes one Force off. A Follower is at its host's battlefield, and only the units of the attacking army are enemy attackers.
    private int GinasFoxForce(CardInstance card)
    {
        var unit = card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : card;
        if (unit.Zone != Zone.Battlefield || unit.OwnerId != _state.ActivePlayerId || unit.ProvinceIndex is not int battlefield) return 0;
        return -_state.Cards.Values.Count(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && c.OwnerId != unit.OwnerId && Definition(c).Effect == EmperorProxyYAbilities.GinasFoxEffect);
    }
}
