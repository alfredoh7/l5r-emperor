namespace L5R.Core;

// "Other players may not target [this card] with actions if any other legal targets are available" (Goju Yurishi - exp, oracle-l5r-10088, issue #453 batch 3): a
// targeting restriction judged inside target legality itself. When another player's action names such a card as its target (its single target, or one candidate of
// a slot), every other card the action could name instead is tried against the very same validation; if any is accepted, the card is refused. The probe never
// nests (a card being tried as the alternative is never itself asked about alternatives), so two such cards facing each other are both legal targets only when
// nothing else is. Read by ValidateAbility for a single-target action and for each slot candidate (before the slot probe returns), so an offer never lists the
// card while another target exists and a submitted command naming it is refused.
public sealed partial class MatchEngine
{
    /// <summary>The Effect of a card that may only be targeted by another player's action when no other legal target exists.</summary>
    public const string TargetOfLastResortEffect = "target-of-last-resort";

    private bool _lastResortProbe;

    private void RequireNotTargetOfLastResort(GameCommand command, AbilityDefinition ability, AbilityDefinition? slotAbility)
    {
        if (_lastResortProbe || command.TargetId is null || ability.Slots is not null) return;
        if (!_state.Cards.TryGetValue(command.TargetId, out var target) || Definition(target).Effect != TargetOfLastResortEffect || !InPlay(target) || target.OwnerId == command.PlayerId) return;
        _lastResortProbe = true;
        try
        {
            foreach (var other in SlotCandidateCards(command.PlayerId))
            {
                if (other.Id == target.Id) continue;
                try { ValidateAbility(command with { TargetId = other.Id }, slotAbility); }
                catch (RuleViolation) { continue; }
                throw new RuleViolation($"{Definition(target).Name} may not be targeted by another player's action while another legal target ({Definition(other).Name}) is available.");
            }
        }
        finally { _lastResortProbe = false; }
    }
}
