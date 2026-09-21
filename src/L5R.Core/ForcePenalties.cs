namespace L5R.Core;

// "Negate all current and new Force penalties on him" (Tireless Efforts, Second City). The penalties in force now are removed -- every negative Force modifier the
// card has -- and, for the rest of the turn, none is applied to it (ApplyModifier and CreateToken read ForcePenaltiesNegatedTurn). A Force BONUS is untouched, and so is
// every other kind of penalty. The duration is the turn, like every effect whose text names none (docs/decisions/2026-09-21-second-city-pass.md).
public sealed partial class MatchEngine
{
    private void NegateForcePenalties(CardInstance card)
    {
        var removed = card.Modifiers.RemoveAll(m => m.Stat == "Force" && m.Amount < 0);
        card.ForcePenaltiesNegatedTurn = _state.TurnNumber;
        Emit("forcePenaltiesNegated", $"{Definition(card).Name}: every Force penalty on him is negated for the turn{(removed > 0 ? $" ({removed} removed)" : "")}.", card.OwnerId, card.Id);
    }
}
