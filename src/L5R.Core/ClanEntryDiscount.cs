using System.Text.RegularExpressions;

namespace L5R.Core;

// "Enters play paying N less Gold if you are a <Clan> Clan player" (issue #336: Daigotsu Hotako exp3 pays 2 less if you are a Lion Clan player).
// One Effect term for every card of this shape: "<clan>-clan-discount" is 1 less (Satoshi's Dual Warfans, Legion of the Bat, Champion of
// Thunder), "<clan>-clan-discount-N" is N less. Read by the ONE cost of every way a card can enter play: a Purchase, a Reserve recruit, Your
// Clan Needs You!, Shinden Shorai (QuotePurchase) and an Equip (EquipGoldCost).
public sealed partial class MatchEngine
{
    private static readonly Regex ClanDiscountEffect = new(@"^([a-z]+(?:-or-[a-z]+)*)-clan-discount(?:-(\d+))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static (int Amount, string Clan)? ClanEntryDiscount(CardDefinition def, PlayerState player)
    {
        var match = ClanDiscountEffect.Match(def.Effect);
        if (!match.Success) return null;
        // "lion-or-scorpion-clan-discount-2": any of the named Clans (Tsuruchi Yashiro).
        var clans = match.Groups[1].Value.Split("-or-").Select(c => char.ToUpperInvariant(c[0]) + c[1..]).ToArray();
        if (!clans.Contains(player.Clan, StringComparer.Ordinal)) return null;
        return (match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1, player.Clan);
    }
}
