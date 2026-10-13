namespace L5R.Core;

// "Pass all battles" (issue #628, Mendon's request): a button for either player while battles are being fought. It passes the player's next action
// and keeps passing it for every remaining battle of the Attack Phase, so a player with nothing to play does not click through each battle.
//
//   * Taking it is a pass (the ordinary Pass, with its forced-pass, first-action and Killing Grounds handling) and sets a per-player flag.
//   * While a flagged player holds the priority and nothing is waiting for an answer, the engine passes for them (RunPassAllBattles, after each command).
//     As Attacker the same applies to choosing the next battle: the lowest remaining battlefield is selected, then the next lowest.
//   * The Engage window and the "before this battle resolves" Reaction prompt are the exceptions (issue #803): a flagged player has chosen to take no
//     action in the battles, so the engine declines those Reactions for them. A window the other player can still use stays open for the other player.
//   * Any other decision or window that needs the flagged player (a reaction, an Either/Or, a duel ...) is never answered for them; the engine waits, and
//     carries on once it is answered.
//   * An action (an ability, a Strategy, a Reserve recruit, an equip) taken by the OTHER player in a battle ends the pass: the flag is cleared and the
//     player must choose Pass all battles again.
//   * If both players take it, every remaining battle is resolved without further actions and the Attack Phase ends.
//   * The flags last only while battles are being fought: they are cleared the moment the Phase leaves Battle / BattleSelection.
//
// The passes the engine makes are not recorded as commands of their own: they follow deterministically from the command that raised the flag and the
// ones after it, so a replay of the recorded commands reaches the same state.

public sealed partial class MatchState
{
    /// <summary>Per player: has taken "Pass all battles" and not been interrupted. Public information.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(1140)]
    public bool[] PassAllBattles { get; set; } = [false, false];
}

public sealed partial class MatchEngine
{
    private const int PassAllBattlesStepLimit = 256;

    private bool PassAllBattlesOn(int playerId) => _state.PassAllBattles[playerId];

    private bool InBattles() => _state.Phase is Phase.Battle or Phase.BattleSelection;

    /// <summary>ApplyCore's handling of the command: raises the flag, and in a battle returns the Pass it stands for (in the battle selection there is nothing to pass:
    /// the next battle is chosen by <see cref="RunPassAllBattles"/>).</summary>
    private GameCommand BeginPassAllBattles(GameCommand command)
    {
        Require(InBattles(), "Pass all battles is available while battles are being fought.");
        Require(!HasOpenDecisionState(), "Finish the pending decision first.");
        _state.PassAllBattles[command.PlayerId] = true;
        Emit("passAllBattles", $"{Player(command.PlayerId).Name} will pass in every remaining battle.", command.PlayerId);
        return _state.Phase == Phase.Battle ? command with { Type = "pass" } : command;
    }

    /// <summary>ApplyCore's check before a command runs: an action taken by a player in a battle ends the other player's pass.</summary>
    private void InterruptPassAllBattles(GameCommand command)
    {
        if (_state.Phase != Phase.Battle || command.PlayerId is not (0 or 1) || command.Type is not ("playStrategy" or "recruitReserve" or "equip" or "useAbility")) return;
        var other = 1 - command.PlayerId;
        if (!_state.PassAllBattles[other]) return;
        _state.PassAllBattles[other] = false;
        Emit("passAllBattlesInterrupted", $"{Player(command.PlayerId).Name} took an action: {Player(other).Name}'s pass of all battles ends.", other);
    }

    /// <summary>Apply's tail: the flags last only while battles are being fought.</summary>
    private void PassAllBattlesTail()
    {
        if (!InBattles() && (_state.PassAllBattles[0] || _state.PassAllBattles[1])) _state.PassAllBattles = [false, false];
    }

    /// <summary>What the engine does next for the player holding the priority, or null when nothing is owed (no flag, a decision open, or no battle to choose).</summary>
    private GameCommand? NextPassAllBattlesCommand()
    {
        if (_state.WinnerId is not null || !InBattles()) return null;
        var player = _state.PriorityPlayerId;
        if (HasOpenDecisionState()) return NextPassAllReactionDecline();
        if (NextIdleBattleCommand() is { } idle) return idle; // issue #894 (IdleBattles.cs): an empty battle with nothing to play, or the one battlefield left
        if (player is not (0 or 1) || !PassAllBattlesOn(player)) return null;
        if (_state.Phase == Phase.Battle) return new("pass", player, _state.Version);
        return player == _state.ActivePlayerId && _state.UnresolvedBattlefields.Count > 0
            ? new("selectBattle", player, _state.Version, Province: _state.UnresolvedBattlefields.Min())
            : null;
    }

    /// <summary>The Reaction the engine declines for a player who has taken Pass all battles: the Engage window, or one
    /// "before this battle resolves" Reaction, and nothing else open. Null when the window is something else, or the player holding it is not flagged.</summary>
    private GameCommand? NextPassAllReactionDecline()
    {
        if (_state.PendingChoice is not { Type: "engageReaction" or "beforeBattleReaction" } pending) return null;
        foreach (var decision in DecisionStates)
            if (decision.Name is not (nameof(MatchState.PendingChoice) or nameof(MatchState.PendingEngage) or nameof(MatchState.PendingBeforeBattleResolve)) && decision.Open(_state)) return null;
        foreach (var decision in EngineHooks.DecisionState.Items) if (decision(_state)) return null;
        var holder = pending.Type == "engageReaction" ? _state.PriorityPlayerId : pending.PlayerId;
        if (holder is not (0 or 1) || !PassAllBattlesOn(holder)) return null;
        return new("declineReaction", holder, _state.Version);
    }

    /// <summary>Execute's tail: carries out the passes and battle selections the raised flags owe, each as an ordinary command of its own.</summary>
    private CommandResult RunPassAllBattles(CommandResult result)
    {
        if (NextPassAllBattlesCommand() is null) return result;
        var events = new List<GameEvent>(result.Events);
        for (var step = 0; step < PassAllBattlesStepLimit && NextPassAllBattlesCommand() is { } next; step++)
        {
            var recorded = _commands.Count;
            var done = ExecuteOne(next);
            _commands.RemoveRange(recorded, _commands.Count - recorded);
            events.AddRange(done.Events);
            if (done.Accepted) continue;
            if (!PassAllBattlesOn(next.PlayerId)) break; // an idle-battle command (IdleBattles.cs) the rules refused: the player decides for themselves, no flag is involved
            // The engine could not pass for the player (a rule stands in the way): the pass ends, and the player decides for themselves.
            var first = _state.Events.Count;
            _state.PassAllBattles[next.PlayerId] = false;
            Emit("passAllBattlesInterrupted", $"{Player(next.PlayerId).Name}'s pass of all battles ends: {done.Error}", next.PlayerId);
            _state.Version++;
            events.AddRange(_state.Events.Skip(first));
            break;
        }
        return result with { Version = _state.Version, Events = events.ToArray() };
    }
}
