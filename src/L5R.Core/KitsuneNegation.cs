namespace L5R.Core;

// Kyuden Kitsune (Second City Stronghold): "Negate the first movement each battle into attacking armies at your provinces' battlefields from other players' actions."
// Every printed "move to a battlefield" effect funnels through MoveToBattlefield, which asks here. A movement of a card of the Attacker's to a battlefield fought over
// the Defender's province, while the Defender controls a card with this Effect, is negated the first time in that battle (one per turn and province) -- unless the
// Defender is the one taking the action. The card stays where it was. "Your forests are maddening" is flavor (Mendon).
public sealed partial class MatchEngine
{
    private const string KitsuneNegationEffect = "negate-first-movement-into-attacking-armies";

    private bool KitsuneNegatesMovement(CardInstance card, int province)
    {
        if (_state.Phase != Phase.Battle) return false;
        var attacker = _state.ActivePlayerId; var defender = 1 - attacker;
        if (card.OwnerId != attacker || _state.PriorityPlayerId == defender) return false;
        if (!_state.Cards.Values.Any(c => c.OwnerId == defender && InPlay(c) && Definition(c).Effect == KitsuneNegationEffect)) return false;
        var key = $"{_state.TurnNumber}:{province}";
        if (_state.KitsuneNegatedBattles.Contains(key)) return false;
        _state.KitsuneNegatedBattles.Add(key);
        Emit("moveNegated", $"{Definition(card).Name}'s movement into the attacking army was negated (Kyuden Kitsune).", card.OwnerId, card.Id);
        return true;
    }
}
