namespace L5R.Core;

// Hibernation (issue #415): "After the next time this game another player's Battle action targets it, negate the action's effects." A mandatory trait -- no Reaction, no choice --
// that fires when the action is announced and its target is chosen, before any reaction window opens. The action is negated whole (ResolveNegatedAction: taken, its costs paid,
// nothing else happens), and the card's shield is spent, so only the next such action is turned away. An action aimed at the card by its own controller does not spend it.
public sealed partial class MatchEngine
{
    private bool TargetShieldNegates(GameCommand command)
    {
        if (!IsBattleAction(command)) return false;
        var shielded = ActionTargetIds(command).Where(id => _state.Cards.ContainsKey(id)).Select(Card)
            .FirstOrDefault(c => c.TargetShield && c.OwnerId != command.PlayerId && InPlay(c));
        if (shielded is null) return false;
        shielded.TargetShield = false;
        Emit("actionNegated", $"{Definition(shielded).Name} is hibernating: {Player(command.PlayerId).Name}'s action ({AbilityCatalog.All[command.AbilityId!].Label}) is negated.", shielded.OwnerId, shielded.Id);
        ResolveNegatedAction(command);
        return true;
    }
}
