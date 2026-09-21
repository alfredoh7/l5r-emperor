namespace L5R.Core;

// Province strength granted by a card in play for as long as its condition holds (Before the Dawn, issue #431): "Your provinces have +1 strength while another player controls a dishonorable
// Personality" (Bayushi Shibata), "While this Shugenja is Earth, your provinces have +1 strength" (Drawing on the Mountain), "While this Shugenja is Water, other players' provinces have -1
// strength" (Erosion), "Your provinces have +2 strength" (Ebisu's Honesty). Like a Region's bonus these are read live, part of the province's CURRENT strength while the card stands in play
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
            switch (effect)
            {
                case "shibata-provinces":
                    if (mine && _state.Cards.Values.Any(c => c.OwnerId != ownerId && InPlay(c) && Definition(c).Type == CardType.Personality && MatchesTargetStatus(c, TargetStatus.IsDishonorable))) total += 1;
                    break;
                case "drawing-on-the-mountain-provinces":
                    if (mine && WielderHasKeyword(card, "Earth")) total += 1;
                    break;
                case "erosion-provinces":
                    if (!mine && WielderHasKeyword(card, "Water")) total -= 1;
                    break;
                case "ebisu-provinces":
                    if (mine) total += 2;
                    break;
            }
        }
        return total;
    }

    // "While this Shugenja is [Keyword]": the Personality a Spell is attached to has the keyword.
    private bool WielderHasKeyword(CardInstance attachment, string keyword) =>
        attachment.Zone == Zone.Attached && attachment.AttachedTo is string hostId && _state.Cards.TryGetValue(hostId, out var host) && Keyword(host, keyword);
}
