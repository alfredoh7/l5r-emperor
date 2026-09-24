namespace L5R.Core;

// "Delay his destruction from the action's effects until before this turn ends" (Spirit of the Berserker, Second City). It is a per-card negation kind of its
// own (NegateEffects): where the action would destroy the card, the destruction does not happen then -- it is put off and carried out at the top of EndTurn,
// before the turn ends, if the card is still in play. Only the destruction is delayed; every other effect of the action applies as usual. Like the other kinds
// it is consumed by the first destruction it meets and dropped when the action has resolved. A Personality is destroyed with his unit, as any destruction.
public sealed partial class MatchEngine
{
    private const string DelayDestroyKind = "delay-destroy";

    // The reaction's effect: from now until the held action has resolved, the first destruction it would inflict on this card is put off.
    private void DelayDestructionFromAction(CardInstance card)
    {
        _state.NegatedEffects.Add(new(DelayDestroyKind, card.Id));
        Emit("destructionDelayReady", $"{Definition(card).Name}: the action's destruction of him will be delayed until before this turn ends.", card.OwnerId, card.Id);
    }

    // Called where a destruction is about to happen (DestroyUnits, DestroyAttachment): true when it is put off instead.
    private bool DestructionDelayed(CardInstance card)
    {
        if (HiromiUnnegatable()) return false; // Yoritomo Hiromi (issue #477): the destruction from her Battle actions will not be delayed.
        var entry = _state.NegatedEffects.FirstOrDefault(n => n.CardId == card.Id && n.Kind == DelayDestroyKind);
        if (entry is null) return false;
        _state.NegatedEffects.Remove(entry);
        if (!_state.PendingDelayedDestructions.Contains(card.Id)) _state.PendingDelayedDestructions.Add(card.Id);
        Emit("destructionDelayed", $"{Definition(card).Name}'s destruction is delayed until before this turn ends.", card.OwnerId, card.Id);
        return true;
    }

    // The top of EndTurn: each delayed card that is still in play is destroyed now; one that left play meanwhile (or came back from the dead) is simply skipped.
    private void ResolveDelayedDestructions()
    {
        var due = _state.PendingDelayedDestructions.ToArray();
        _state.PendingDelayedDestructions.Clear();
        foreach (var id in due)
        {
            if (!_state.Cards.TryGetValue(id, out var card) || !InPlay(card)) continue;
            Emit("delayedDestruction", $"{Definition(card).Name} is destroyed, as delayed.", card.OwnerId, card.Id);
            if (Definition(card).Type == CardType.Personality) DestroyUnits([card], true);
            else DestroyAttachment(card);
        }
    }
}
