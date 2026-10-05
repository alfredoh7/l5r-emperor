namespace L5R.Core;

// Issue #642 (found by the bot soak): QueryActions offers a pending choice only to the player who holds priority, and the handlers of the choices below accept an answer only from
// the player the choice belongs to (PendingChoice.PlayerId). Several of them were opened, or re-opened after another player's step, while priority was still with the OTHER
// player: that player was offered the choice and refused it, and the player it belonged to saw only "waiting", so the whole match stalled (Harsh Choices, Offering Reverence,
// The Price of Honor's second question, the province reveal order). Whatever opens or hands back one of these choices, it is answered by its owner, so after every command the
// owner holds priority.
public sealed partial class MatchEngine
{
    private static readonly HashSet<string> OwnerAnsweredChoices = new(StringComparer.Ordinal) { "harshChoices", "provinceOrder", "offeringReverence", "eitherOr", "testedLandProvince" };

    internal void NormalizeOwnerAnsweredPriority()
    {
        if (_state.PendingChoice is { } pending && OwnerAnsweredChoices.Contains(pending.Type) && pending.PlayerId != _state.PriorityPlayerId)
            _state.PriorityPlayerId = pending.PlayerId;
    }
}
