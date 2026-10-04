namespace L5R.Core;

// Issue #627 (Mendon's playtest): "Do not present the option to assign Cavalry attackers/personalities if a player has no Cavalry units."
// A Cavalry Maneuvers segment (attackers' or defenders') in which the player to act has no enabled action but to pass (no Cavalry unit he
// can assign, and nothing else usable) is passed for him, exactly as the pass would: no Cavalry units are assigned and the segment ends.
// A player who has any other usable action keeps the segment, so no timing window is taken away from him.
public sealed partial class MatchEngine
{
    private void SkipIdleCavalrySegments()
    {
        // At most one skip per segment: attackers' then defenders' (and a battlefield order choice may intervene), so a small bound is plenty.
        for (var guard = 0; guard < 4; guard++)
        {
            if (_state.WinnerId is not null || _state.PendingChoice is not null || HasOpenDecisionState()) return;
            if (_state.Phase is not (Phase.CavalryDeclaration or Phase.CavalryDefense)) return;
            var player = Player(_state.PriorityPlayerId);
            if (QueryActions(player.Id).Any(a => a.Enabled && a.Type is not ("pass" or "concede"))) return;
            Emit("cavalrySkipped", $"{player.Name} has no Cavalry units to assign.", player.Id);
            var empty = new GameCommand(_state.Phase == Phase.CavalryDeclaration ? "attack" : "assignDefense", player.Id, _state.Version, Assignments: []);
            if (_state.Phase == Phase.CavalryDeclaration) AssignAttackers(empty); else AssignDefense(empty);
        }
    }
}
