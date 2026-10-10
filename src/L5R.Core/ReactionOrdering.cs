namespace L5R.Core;

// Reaction announce-and-order (issue #803, Mendon's request). In an alternating Reaction window, when BOTH players hold a Reaction they could play, the Reactions are no longer played
// one at a time in whatever order the players happen to alternate. Instead:
//
//   1. Announce. The turn player, then the other player, announces every Reaction they intend to play in this window (the card and ability only: targets and Gold are chosen, and costs
//      paid, when that Reaction's turn comes), then says they are done. An announcement may be retracted until its player is done.
//   2. Order. If the announced Reactions are not all the turn player's, the turn player chooses the order they resolve in. (When only the turn player announced, they resolve in the
//      order announced; a single Reaction needs no ordering.)
//   3. Resolve. Each Reaction in turn is played by its owner through the ordinary command for it (a Reaction that is no longer legal fizzles; its owner may also skip it). When the last
//      one has resolved the window closes: nothing more can be added.
//
// A window where only one player holds a Reaction is unchanged: that player plays them one after another, as played. Pass all battles (issue #628) declines for a flagged player, who
// therefore never counts as holding a Reaction. A Reaction that opens a window of its own suspends this order (ParkedReactionOrders) until that window is over.

/// <summary>One Reaction announced into an ordered window. Id names it for ordering and retracting; Label is the offered action's label.</summary>
public sealed record AnnouncedReaction(string Id, int PlayerId, string? CardId, string AbilityId, string? PerformerId, string Label);

/// <summary>An alternating Reaction window running the announce / order / resolve procedure. Public: both players see every announcement.</summary>
public sealed class PendingReactionOrder
{
    public PendingChoice? Window { get; set; }
    /// <summary>Announce, Order or Resolve.</summary>
    public string Stage { get; set; } = "Announce";
    /// <summary>The player whose decision it is in the Announce stage (the turn player first); the turn player in the Order stage.</summary>
    public int Turn { get; set; }
    public bool[] Done { get; set; } = [false, false];
    /// <summary>Announced, not yet ordered (Announce and Order stages).</summary>
    public List<AnnouncedReaction> Announced { get; set; } = [];
    /// <summary>Ordered, the first resolving next (Resolve stage; during Order, the ones already placed).</summary>
    public List<AnnouncedReaction> Queue { get; set; } = [];
    public int NextId { get; set; }
    /// <summary>The queue entry the command being applied plays; removed once it has been applied.</summary>
    public string? Consuming { get; set; }
}

public sealed partial class MatchState
{
    /// <summary>The announce-and-order procedure of the open Reaction window, or null.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(1150)]
    public PendingReactionOrder? PendingReactionOrder { get; set; }

    /// <summary>Orders set aside while a Reaction's own window is open, innermost last.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(1151)]
    public List<PendingReactionOrder> ParkedReactionOrders { get; set; } = [];

    /// <summary>The window found not to need ordering (only one player held a Reaction), so it is not asked again while it stays open.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(1152)]
    public PendingChoice? ReactionOrderSkipped { get; set; }
}

/// <summary>Public view of the procedure. Announced are the Reactions not yet placed; Ordered the ones in resolving order, the first next.</summary>
public sealed record ReactionOrderView(string Stage, int TurnPlayerId, bool[] Done, ReactionOrderItemView[] Announced, ReactionOrderItemView[] Ordered);
public sealed record ReactionOrderItemView(string Id, int PlayerId, string? CardId, string CardName, string Label);

public sealed partial class MatchEngine
{
    /// <summary>The windows that alternate between the players with Reactions only. Proclaim (which also offers the Proclaim action), battle recruits (Ring grants) and the windows set up
    /// to answer a held action or a pending attack, duel or Honor change are not ordered: they have continuations of their own. The bow, move and Follower-bow windows belong to the one watching card that opened them.</summary>
    private static readonly HashSet<string> OrderedReactionWindows =
    [
        "engageReaction", "entryReaction", "duelWonReaction", "afterResolutionReaction", "afterActionResolvedReaction", "battleActionReaction",
    ];

    // ---- commands ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>ApplyCore: the open order handles the command (true), lets the ordinary window handler play it (false, a Reaction of the head of the queue), or refuses it.</summary>
    private bool HandleReactionOrderCommand(PendingReactionOrder order, GameCommand command)
    {
        order.Consuming = null;
        var player = command.PlayerId;
        switch (order.Stage)
        {
            case "Announce":
                Require(command.Type is "reactionAnnounce" or "reactionUnannounce" or "reactionAnnounceDone", "Announce the Reactions you will play in this window, then say you are done.");
                Require(order.Turn == player, "Wait for the other player to announce their Reactions.");
                if (command.Type == "reactionAnnounce") Announce(order, command);
                else if (command.Type == "reactionUnannounce")
                {
                    var own = order.Announced.FirstOrDefault(a => a.Id == command.TargetId && a.PlayerId == player);
                    Require(own is not null, "Choose one of your announced Reactions.");
                    order.Announced.Remove(own!);
                    Emit("reactionRetracted", $"{Player(player).Name} will not play {ReactionName(own!)} after all.", player, own!.CardId);
                }
                else FinishAnnouncing(order, player);
                return true;
            case "Order":
                Require(command.Type == "reactionOrder" && player == order.Turn, "Choose which announced Reaction resolves next.");
                var next = order.Announced.FirstOrDefault(a => a.Id == command.TargetId);
                Require(next is not null, "Choose one of the announced Reactions.");
                order.Announced.Remove(next!); order.Queue.Add(next!);
                if (order.Announced.Count == 1) { order.Queue.Add(order.Announced[0]); order.Announced.Clear(); }
                if (order.Announced.Count == 0) BeginResolving(order);
                return true;
            default:
                var head = order.Queue[0];
                if (command.Type == "declineReaction")
                {
                    Require(player == head.PlayerId, "Wait for the other player's Reaction.");
                    order.Queue.RemoveAt(0);
                    Emit("reactionSkipped", $"{Player(player).Name} does not play {ReactionName(head)} after all.", player, head.CardId);
                    return true;
                }
                Require(command.Type == "useAbility" && player == head.PlayerId && command.CardId == head.CardId && command.AbilityId == head.AbilityId
                    && (head.PerformerId is null || command.PerformerId == head.PerformerId), $"Play {ReactionName(head)} ({head.Label}) now, or skip it.");
                order.Consuming = head.Id;
                return false;
        }
    }

    private void Announce(PendingReactionOrder order, GameCommand command)
    {
        var player = command.PlayerId;
        var offered = ReactionChoicesOf(player).FirstOrDefault(o => o.Command is { } c && c.CardId == command.CardId && c.AbilityId == command.AbilityId && c.PerformerId == command.PerformerId);
        Require(offered is not null, "That Reaction cannot be played in this window.");
        Require(!order.Announced.Any(a => a.PlayerId == player && a.CardId == command.CardId && a.AbilityId == command.AbilityId && a.PerformerId == command.PerformerId), "You already announced that Reaction.");
        var item = new AnnouncedReaction($"r{order.NextId++}", player, command.CardId, command.AbilityId!, command.PerformerId, offered!.Label);
        order.Announced.Add(item);
        Emit("reactionAnnounced", $"{Player(player).Name} will play {ReactionName(item)}: {item.Label}.", player, item.CardId);
    }

    private void FinishAnnouncing(PendingReactionOrder order, int player)
    {
        order.Done[player] = true;
        Emit("reactionAnnounceDone", $"{Player(player).Name} has announced all their Reactions.", player);
        var active = _state.ActivePlayerId;
        if (!order.Done[active]) order.Turn = active;
        else if (!order.Done[1 - active]) order.Turn = 1 - active;
        else
        {
            var all = order.Announced;
            if (all.Count == 0)
            {
                _state.PendingReactionOrder = null;
                CloseReactionWindow(order.Window!, "No Reaction was announced; the window closed.");
                return;
            }
            if (all.Count == 1 || all.All(a => a.PlayerId == active)) { order.Queue.AddRange(all); all.Clear(); BeginResolving(order); return; }
            order.Stage = "Order"; order.Turn = active;
            Emit("reactionOrdering", $"{Player(active).Name} chooses the order the announced Reactions resolve in.", active);
            _state.PriorityPlayerId = active;
            return;
        }
        _state.PriorityPlayerId = order.Turn;
    }

    private void BeginResolving(PendingReactionOrder order)
    {
        order.Stage = "Resolve";
        Emit("reactionResolving", "The announced Reactions resolve in order.");
        _state.PriorityPlayerId = order.Queue[0].PlayerId;
    }

    // ---- the Reactions a player could play in the window underneath ----------------------------------------------------------------------------------

    /// <summary>The offered Reaction options of the window underneath the procedure, for a player holding the priority in it.</summary>
    private ActionOption[] ReactionChoicesOf(int playerId)
    {
        var order = _state.PendingReactionOrder; var priority = _state.PriorityPlayerId;
        _state.PendingReactionOrder = null; _state.PriorityPlayerId = playerId;
        try { return QueryActions(playerId).Where(a => a.Enabled && a.Type == "useAbility" && a.Command is { AbilityId: not null } && StillLegal(a)).ToArray(); }
        finally { _state.PendingReactionOrder = order; _state.PriorityPlayerId = priority; }
    }

    private bool QueueHeadPlayable(AnnouncedReaction head) =>
        ReactionChoicesOf(head.PlayerId).Any(o => o.Command!.CardId == head.CardId && o.Command.AbilityId == head.AbilityId && (head.PerformerId is null || o.Command.PerformerId == head.PerformerId));

    private string ReactionName(AnnouncedReaction item) => item.CardId is { } id && _state.Cards.TryGetValue(id, out var card) ? Definition(card).Name : AbilityCatalog.All[item.AbilityId].Label;

    private ActionOption[] ReactionOrderActions(int playerId, PendingReactionOrder order)
    {
        var result = new List<ActionOption>();
        ActionOption Option(string type, string label, GameCommand command, string? cardId = null) => new(type, label, true, null, command, cardId);
        var version = _state.Version;
        if (order.Stage == "Announce" && order.Turn == playerId)
        {
            var mine = order.Announced.Where(a => a.PlayerId == playerId).ToArray();
            foreach (var choice in ReactionChoicesOf(playerId)
                .Where(o => !mine.Any(a => a.CardId == o.Command!.CardId && a.AbilityId == o.Command.AbilityId && a.PerformerId == o.Command.PerformerId))
                .GroupBy(o => (o.Command!.CardId, o.Command.AbilityId, o.Command.PerformerId)).Select(g => g.First()))
                result.Add(Option("reactionAnnounce", "Announce: " + choice.Label, new("reactionAnnounce", playerId, version, choice.Command!.CardId, AbilityId: choice.Command.AbilityId, PerformerId: choice.Command.PerformerId), choice.Command.CardId));
            foreach (var item in mine) result.Add(Option("reactionUnannounce", "Retract: " + item.Label, new("reactionUnannounce", playerId, version, TargetId: item.Id), item.CardId));
            result.Add(Option("reactionAnnounceDone", mine.Length == 0 ? "No Reactions" : "Done announcing", new("reactionAnnounceDone", playerId, version)));
        }
        else if (order.Stage == "Order" && order.Turn == playerId)
        {
            foreach (var item in order.Announced)
                result.Add(Option("reactionOrder", $"Resolve {ReactionName(item)} next · {item.Label} ({Player(item.PlayerId).Name})", new("reactionOrder", playerId, version, TargetId: item.Id), item.CardId));
        }
        else if (order.Stage == "Resolve" && order.Queue.Count > 0 && order.Queue[0].PlayerId == playerId)
        {
            var head = order.Queue[0];
            result.AddRange(ReactionChoicesOf(playerId).Where(o => o.Command!.CardId == head.CardId && o.Command.AbilityId == head.AbilityId && (head.PerformerId is null || o.Command.PerformerId == head.PerformerId)));
            result.Add(Option("declineReaction", $"Skip {ReactionName(head)}", new("declineReaction", playerId, version), head.CardId));
        }
        else result.Add(new("wait", $"Waiting for {Player(_state.PriorityPlayerId).Name}", false, "The other player has the action opportunity."));
        result.Add(Option("concede", "Concede", new("concede", playerId, version)));
        return result.ToArray();
    }

    private ReactionOrderView? BuildReactionOrderView()
    {
        if (_state.PendingReactionOrder is not { } order) return null;
        ReactionOrderItemView View(AnnouncedReaction a) => new(a.Id, a.PlayerId, a.CardId, ReactionName(a), a.Label);
        return new(order.Stage, order.Turn, order.Done.ToArray(), order.Announced.Select(View).ToArray(), order.Queue.Select(View).ToArray());
    }

    // ---- the tail of every command ---------------------------------------------------------------------------------------------------------------

    /// <summary>A player who took Pass all battles holds no Reaction as far as the procedure goes: the engine declines their window for them.</summary>
    private bool ReactionOrderOffers(int playerId) => !(InBattles() && PassAllBattlesOn(playerId)) && ReactionWindowOffers(playerId);

    private static bool OrderedWindowOpen(PendingChoice? choice) => choice is not null && OrderedReactionWindows.Contains(choice.Type);

    /// <summary>True while the window under the procedure is open (or set aside behind a window of a Reaction's own): the ordinary "nothing left to react with" closing must wait.</summary>
    private bool ReactionOrderHolds() =>
        _state.PendingReactionOrder is not null || _state.PendingChoice is not null && _state.ParkedReactionOrders.Any(p => Equals(p.Window, _state.PendingChoice));

    /// <summary>Apply's tail: carries the open order on after a command, sets one aside or back, opens one for a window where both players could react.</summary>
    private void ReactionOrderTail()
    {
        if (_state.WinnerId is not null) { _state.PendingReactionOrder = null; _state.ParkedReactionOrders.Clear(); _state.ReactionOrderSkipped = null; return; }
        var current = _state.PendingChoice;
        if (_state.PendingReactionOrder is { } order)
        {
            if (order.Consuming is not null) { order.Queue.RemoveAll(i => i.Id == order.Consuming); order.Consuming = null; }
            if (!Equals(current, order.Window))
            {
                // The window is gone (closed by the Reaction), or a window of the Reaction's own has opened above it.
                _state.PendingReactionOrder = null;
                if (current is not null && _state.SuspendedWindows.Any(w => Equals(w.Choice, order.Window))) _state.ParkedReactionOrders.Add(order);
            }
        }
        if (_state.PendingReactionOrder is null && _state.ParkedReactionOrders.Count > 0)
        {
            if (current is not null && Equals(_state.ParkedReactionOrders[^1].Window, current))
            {
                _state.PendingReactionOrder = _state.ParkedReactionOrders[^1]; _state.ParkedReactionOrders.RemoveAt(_state.ParkedReactionOrders.Count - 1);
            }
            else if (current is null && _state.SuspendedWindows.Count == 0) _state.ParkedReactionOrders.Clear();
        }
        if (!Equals(current, _state.ReactionOrderSkipped)) _state.ReactionOrderSkipped = null;
        if (_state.PendingReactionOrder is { Stage: "Resolve" } resolving && Equals(current, resolving.Window))
        {
            while (resolving.Queue.Count > 0 && !QueueHeadPlayable(resolving.Queue[0]))
            {
                var fizzled = resolving.Queue[0]; resolving.Queue.RemoveAt(0);
                Emit("reactionFizzled", $"{ReactionName(fizzled)} can no longer be played.", fizzled.PlayerId, fizzled.CardId);
            }
            if (resolving.Queue.Count == 0)
            {
                _state.PendingReactionOrder = null;
                CloseReactionWindow(resolving.Window!, "The announced Reactions have all resolved; the window closed.");
                return;
            }
            _state.PriorityPlayerId = resolving.Queue[0].PlayerId; _state.ConsecutivePasses = 0;
            return;
        }
        if (_state.PendingReactionOrder is { Stage: "Announce" or "Order" } deciding && Equals(current, deciding.Window)) { _state.PriorityPlayerId = deciding.Turn; _state.ConsecutivePasses = 0; return; }
        if (_state.PendingReactionOrder is not null || !OrderedWindowOpen(current) || Equals(current, _state.ReactionOrderSkipped) || _state.PendingNegatableAbility is not null) return;
        // A window opened by this command: ordered only when both players could play a Reaction in it.
        var active = _state.ActivePlayerId;
        if (!ReactionOrderOffers(active) || !ReactionOrderOffers(1 - active)) { _state.ReactionOrderSkipped = current; return; }
        _state.PendingReactionOrder = new PendingReactionOrder { Window = current, Turn = active };
        _state.PriorityPlayerId = active; _state.ConsecutivePasses = 0;
        Emit("reactionAnnouncing", "Both players hold a Reaction for this window: announce the Reactions you will play.");
    }
}
