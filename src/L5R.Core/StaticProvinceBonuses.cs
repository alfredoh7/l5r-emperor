namespace L5R.Core;

// Province strength granted by a card in play for as long as its condition holds (Before the Dawn, issue #431): "Your provinces have +1 strength while another player controls a dishonorable
// Personality" (Bayushi Shibata), "Your provinces have +2 strength" (Ebisu's Honesty). Like a Region's bonus these are read live, part of the province's CURRENT strength while the card stands in play
// and simply not counted once it does not (EffectiveProvinceStrength), so nothing has to undo them. A card is named by its Effect; the province belongs to the owner it is looked up under.
public sealed partial class MatchEngine
{
    private int StaticProvinceBonus(ProvinceState province)
    {
        var ownerId = _state.Players.First(p => p.Provinces.Contains(province)).Id;
        var total = 0;
        foreach (var card in _state.Cards.Values.Where(InPlay))
        {
            var effect = Definition(card).Effect;
            if (effect.Length == 0 || effect == "none") continue;
            var mine = card.OwnerId == ownerId;
            total += P4StaticProvinceBonus(card, mine); // Drawing on the Mountain (issue #503, EmperorP4Watches.cs)
            switch (effect)
            {
                case "shibata-provinces":
                    if (mine && _state.Cards.Values.Any(c => c.OwnerId != ownerId && InPlay(c) && Definition(c).Type == CardType.Personality && MatchesTargetStatus(c, TargetStatus.IsDishonorable))) total += 1;
                    break;
                case "ebisu-provinces":
                    if (mine) total += 2;
                    break;
                // Fortifications (issue #492, group F): "Your provinces have +1 strength."
                case FortificationsEffect:
                    if (mine) total += 1;
                    break;
            }
        }
        return total;
    }
}
