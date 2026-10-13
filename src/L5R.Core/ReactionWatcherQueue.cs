using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #898: Reactions to the same moment stack. The bow, move and Nikutai windows each name ONE watching card (PendingChoice.CardId): the first the engine found, with the others
// never asked ("an arbitrary but deterministic tie-break, since no real scenario needs both"). Every eligible watcher now gets its own window in turn: the opener lists them all,
// opens the first and queues the rest here; when a watcher's window closes (its Reaction played, or passed), the next one still eligible is asked, and the action's resolution
// carries on only after the last. A Reaction a watcher plays that opens a window of its own sets this queue aside with the window (SuspendedWindow.WatcherQueue).

/// <summary>One watching card's turn: the window type, the card, and what that window needs (the bowed cards it may name, the moved or bowed card), plus who the action returns to.</summary>
public sealed record WatcherTurn(string Type, string WatcherId, string Prompt, string[]? CandidateIds, string? TargetId, int ClosesTo);

public sealed partial class MatchState
{
    /// <summary>The watchers of the open single-card Reaction window still to be asked after it, in order.</summary>
    [JsonPropertyOrder(1801)]
    public List<WatcherTurn> WatcherQueue { get; set; } = [];
}

public sealed partial class MatchEngine
{
    private string? WatcherAbilityId(CardInstance card, string windowType) => windowType switch
    {
        "bowReaction" => BowedAtBattlefieldReactionAbility(card) ?? BowedNearRegionReactionAbility(card) ?? OwnActionBowedPersonalityReactionAbility(card),
        "moveReaction" => MovedEnemyInfantryReactionAbility(card),
        "nikutaiReaction" => SiblingFollowerBowedReactionAbility(card),
        "battleActionReaction" => AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "after-battle-action-resolves"),
        _ => null,
    };

    /// <summary>Opens the first watcher's window (setting aside the window a Reaction was played in) and queues the others.</summary>
    private void OpenWatcherTurns(List<WatcherTurn> turns)
    {
        SuspendOpenWindow();
        _state.WatcherQueue = turns.Skip(1).ToList();
        StartWatcherTurn(turns[0]);
    }

    private void StartWatcherTurn(WatcherTurn turn)
    {
        var watcher = Card(turn.WatcherId);
        if (turn.Type == "bowReaction") _state.BowReactionCandidateIds = turn.CandidateIds?.ToList() ?? [];
        else if (turn.Type == "moveReaction") _state.MoveReactionTargetId = turn.TargetId;
        else if (turn.Type == "nikutaiReaction") _state.NikutaiReactionTargetId = turn.TargetId;
        _state.PendingChoice = new(turn.Type, watcher.OwnerId, watcher.Id, turn.Prompt, turn.ClosesTo);
        _state.ConsecutivePasses = 0; _state.PriorityPlayerId = watcher.OwnerId;
        Emit("reactionWindow", turn.Type switch { "bowReaction" => "Bow-reaction window opened.", "moveReaction" => "Move-reaction window opened.", "battleActionReaction" => "Battle-action reaction window opened.", _ => "Nikutai reaction window opened." }, watcher.OwnerId, watcher.Id);
    }

    // A watcher asked later must still be able to react when its turn comes: a Stormless Fury attached by an earlier turn no longer sits in a hand and may not react against the action that attached it.
    private bool WatcherStillEligible(CardInstance watcher, string windowType)
    {
        if (windowType == "battleActionReaction")
            return watcher.Zone == Zone.Hand ? _state.BattleActionReactionPerformerId is not null && Definition(watcher).Abilities is not null && WatcherAbilityId(watcher, windowType) is not null
                : BattleActionWatcherInPlayEligible(watcher);
        return (InPlay(watcher) || watcher.Zone == Zone.Region) && WatcherAbilityId(watcher, windowType) is { } abilityId && Unspent(watcher, abilityId);
    }

    /// <summary>A single-card window of this type just closed: asks the next watcher still able to react. False when none is left (the action carries on).</summary>
    private bool OpenNextWatcherTurn(string windowType)
    {
        while (_state.WatcherQueue.Count > 0 && _state.WatcherQueue[0].Type == windowType)
        {
            var turn = _state.WatcherQueue[0];
            _state.WatcherQueue.RemoveAt(0);
            if (!_state.Cards.TryGetValue(turn.WatcherId, out var watcher) || !WatcherStillEligible(watcher, windowType)) continue;
            StartWatcherTurn(turn);
            return true;
        }
        return false;
    }
}
