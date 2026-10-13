namespace L5R.Core;

// "Negate all current and new Force penalties on him" (Tireless Efforts, Second City). The penalties in force now are removed -- every negative Force modifier the
// card has -- and, for the rest of the turn, none is applied to it (ApplyModifier and CreateToken read ForcePenaltiesNegatedTurn). A Force BONUS is untouched, and so is
// every other kind of penalty. The duration is the turn, like every effect whose text names none (docs/decisions/2026-09-21-second-city-pass.md).
public sealed partial class MatchEngine
{
    // The shared mechanism of a temporary prevention that also covers what is in force now ("negate all current and new ..." for a duration -- Tireless Efforts, Creating Order): the modifiers
    // in force are hidden, not deleted (AppliedModifier.Suppressed). They keep their source, amount and permanence, so one whose own duration outlasts the prevention counts again when it ends, while a
    // turn's modifier is swept at the turn's end as ever, hidden or not. Returns how many were hidden. A one-shot "negate all current ..." with no duration (Incense Mill, Show of Restraint) removes instead.
    private int SuppressModifiers(CardInstance card, Func<AppliedModifier, bool> match)
    {
        var hidden = 0;
        for (var i = 0; i < card.Modifiers.Count; i++)
            if (card.Modifiers[i].SuppressedTurn != _state.TurnNumber && match(card.Modifiers[i])) { card.Modifiers[i] = card.Modifiers[i] with { SuppressedTurn = _state.TurnNumber }; hidden++; }
        return hidden;
    }

    private void NegateForcePenalties(CardInstance card)
    {
        var removed = SuppressModifiers(card, m => m.Stat == "Force" && m.Amount < 0);
        card.ForcePenaltiesNegatedTurn = _state.TurnNumber;
        Emit("forcePenaltiesNegated", $"{Definition(card).Name}: every Force penalty on him is negated for the turn{(removed > 0 ? $" ({removed} set aside until the turn ends)" : "")}.", card.OwnerId, card.Id);
    }
}
