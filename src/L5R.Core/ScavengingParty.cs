namespace L5R.Core;

// Scavenging Party (issue #420, Mendon's ruling): "Target your discarded Personality: Discard a card from one of your provinces and refill it face-up with the Personality." A dead Personality is also
// a discarded one (he is in the same discard pile), so the target is any of the player's Personalities in his Dynasty discard pile, dead or merely discarded. He comes back as a fresh card: every
// permanent change he carried is cleared (ResetCardState), exactly as for a Personality that goes back into a province by any other route.
public sealed partial class MatchEngine
{
    private static bool IsDiscardedPersonality(CardInstance card) => Definition(card).Type == CardType.Personality && card.Zone is Zone.DynastyDiscard or Zone.FateDiscard or Zone.Dead;

    private void RefillProvinceWithDiscardedPersonality(PlayerState player, CardInstance personality, int provinceIndex, string sourceName)
    {
        var province = player.Provinces.Single(p => p.Index == provinceIndex);
        if (province.CardId is string existingId)
        {
            var existing = Card(existingId);
            existing.Zone = Zone.DynastyDiscard; existing.ProvinceIndex = null; existing.FaceUp = true;
            Emit("provinceDiscard", $"{player.Name} discarded {Definition(existing).Name} from a province ({sourceName}).", player.Id, existingId);
        }
        ResetCardState(personality);
        personality.Zone = Zone.Province; personality.ProvinceIndex = province.Index; personality.FaceUp = true;
        province.CardId = personality.Id;
        Emit("cardPlacedInProvince", $"{Definition(personality).Name} was placed face-up into a province ({sourceName}).", player.Id, personality.Id);
    }
}
