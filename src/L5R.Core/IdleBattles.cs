namespace L5R.Core;

// Issue #894 (Mendon): "Frequently, battles are fought with no personalities on either side." Every undestroyed province of the defender is a battlefield, so a battle is
// fought there whether or not anyone stands on it, and each empty one asked for a selectBattle and two passes. Two decisions that are not decisions are now made by the engine,
// from the state alone (so a replay reaches them the same way, as it does for "Pass all battles", PassAllBattles.cs):
//
//   * An empty battlefield, nothing pending, and the player holding the priority has nothing enabled but a pass: the engine passes for them. Whatever a player could play
//     (a Battle action or Strategy, a move from home, an Engage action or Reaction that opens a window) stays on offer and keeps the battle waiting for them, so nobody
//     loses an opportunity and "intending to move a Personality there" is simply having an action to do it with.
//   * Exactly one battlefield left to choose: the player choosing has no choice, so it is selected.
//
// The flag-driven "Pass all battles" still covers a player who has playable actions and chooses not to use them.
public sealed partial class MatchState
{
    /// <summary>Whether the engine makes the idle-battle decisions below. Null (the default) means yes for every real match and no for an engine built straight from a test state
    /// (scripts that place their own units and answer every window); a test of the feature sets it true.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(1141)]
    public bool? IdleBattleSkips { get; set; }
}

public sealed partial class MatchEngine
{
    private bool IdleBattlesOn => _state.IdleBattleSkips ?? _scenario != "test-only";

    /// <summary>No Personality, follower, item or Terrain stands on the battlefield, for either player.</summary>
    private bool BattlefieldEmpty(int battlefield) => !_state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield);

    /// <summary>Whether the player has anything to do but pass: an enabled action other than the pass, the pass-all button, a decline or a concede.</summary>
    private bool HasUsableAction(int playerId) =>
        QueryActions(playerId).Any(a => a.Enabled && a.Type is not ("pass" or "passAllBattles" or "declineReaction" or "wait" or "concede" or "selectBattle"));

    /// <summary>The command the engine makes for nobody to be asked, or null: see the file's header.</summary>
    private GameCommand? NextIdleBattleCommand()
    {
        if (!IdleBattlesOn || _state.WinnerId is not null || _state.PendingChoice is not null || R1.Aside is not null) return null;
        var player = _state.PriorityPlayerId;
        if (player is not (0 or 1)) return null;
        if (_state.Phase == Phase.BattleSelection)
            return _state.UnresolvedBattlefields.Count == 1 ? new("selectBattle", player, _state.Version, Province: _state.UnresolvedBattlefields[0]) : null;
        if (_state.Phase != Phase.Battle || _state.PendingDuel is not null || _state.CurrentBattleProvince is not int battlefield || !BattlefieldEmpty(battlefield)) return null;
        return HasUsableAction(player) ? null : new("pass", player, _state.Version);
    }
}
