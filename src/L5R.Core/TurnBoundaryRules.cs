using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// P4 (issue #690): TURN-SCOPED STANDING RULES -- a rule an action or an Event sets that stands for several turns ("until your second turn from now begins ...", "until the game ends,
// each player ..."). One record per rule in MatchState.TurnScopedRules: a kind, the player it is for (null: each player), a TurnUntil, and what the kind needs. A rule ends as the turn
// its TurnUntil names begins (ApplyTurnBoundaryExpiry) or when the card it depends on leaves play.
//
// Two kinds are consulted by the engine itself, each at one existing choke point:
//   * EndTurnExtraDraw   -- EndTurn, just before the End Phase's own draw: the player whose turn is ending draws Amount more cards, if the per-turn counter named by Counter is at most
//                           Max for him at that moment (no Counter: unconditionally). "Each player draws two cards instead of one at the end of his turn if he put no other cards in his
//                           hand that turn" is Amount 1, Counter CardsIntoHand, Max 0.
//   * LimitedAsOpen      -- the Limited timing check (ValidateAbility and the offered list): Amount times per turn, the player may take a Limited action as an Open action, i.e. in an
//                           Action Phase that is not his own turn's active-player opportunity. Arg narrows it to the source card's type ("Strategy"); null: any Limited ability. The use
//                           is counted as the action is announced (UseAbility). Plans Within Plans keeps its own mechanism and is asked first: an action it covers spends no use here.
// Any other kind is a record a feature reads from its own code (TurnScopedRulesFor) -- for instance from its EngineHooks.TurnBegins callback -- with the store doing the expiry.
//
// The per-turn counters are P7's period counters (PeriodWatches.cs) under "p4:<counter>:<player>": TurnCountOf / AddTurnCount. CardsIntoHand is bumped by NoteCardIntoHand.
// ======================================================================================================================================================================================

public static class TurnRuleKinds
{
    public const string EndTurnExtraDraw = "end-turn-extra-draw";
    public const string LimitedAsOpen = "limited-as-open";
}

public static class TurnCounters
{
    /// <summary>Cards put into the player's hand this turn, by any means (every site NoteCardIntoHand covers: a draw, a search, a return to hand).</summary>
    public const string CardsIntoHand = "cards-into-hand";
}

/// <summary>A standing rule that lasts for turns. <see cref="PlayerId"/> null: it applies to each player.</summary>
public sealed class TurnScopedRule
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int? PlayerId { get; set; }
    public TurnUntil Until { get; set; } = TurnUntil.GameEnds();
    /// <summary>The card (id or title) whose effect the rule is, for the log.</summary>
    public string Source { get; set; } = "";
    /// <summary>A card id the rule ends with ("while this card remains in play"), or null.</summary>
    public string? WhileInPlay { get; set; }
    public int Amount { get; set; }
    public string? Counter { get; set; }
    public int Max { get; set; }
    public string? Arg { get; set; }
    /// <summary>A card the rule names (its target), for a kind that needs one.</summary>
    public string? CardId { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(22102)]
    public List<TurnScopedRule> TurnScopedRules { get; set; } = [];
    [JsonPropertyOrder(22103)]
    public int TurnScopedRuleSequence { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- per-turn counters -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string TurnCounterKey(string counter, int playerId) => $"p4:{counter}:{playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    /// <summary>The player's count of <paramref name="counter"/> this turn (zero once the turn has ended).</summary>
    internal int TurnCountOf(string counter, int playerId) => PeriodCountOf(TurnCounterKey(counter, playerId), Period.Turn);
    /// <summary>Counts for the turn in progress. Nothing is counted before the first turn begins: the opening hands are not cards a player put in his hand "that turn".</summary>
    internal int AddTurnCount(string counter, int playerId, int by = 1) =>
        Array.TrueForAll(_state.Players, p => p.TurnsTaken == 0) ? 0 : AddPeriodCount(TurnCounterKey(counter, playerId), Period.Turn, by);

    // ---- the store -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Sets a standing rule. <paramref name="playerId"/> null: each player.</summary>
    internal TurnScopedRule AddTurnScopedRule(string kind, int? playerId, TurnUntil until, string source, int amount = 0, string? counter = null, int max = 0, string? arg = null,
        string? cardId = null, string? whileInPlay = null)
    {
        var rule = new TurnScopedRule { Id = ++_state.TurnScopedRuleSequence, Kind = kind, PlayerId = playerId, Until = until, Source = source, WhileInPlay = whileInPlay, Amount = amount, Counter = counter, Max = max, Arg = arg, CardId = cardId };
        _state.TurnScopedRules.Add(rule);
        Emit("turnRuleSet", $"{source}: a rule stands {DescribeUntil(until)}{(playerId is int who ? $" for {Player(who).Name}" : " for each player")}.", playerId, whileInPlay);
        return rule;
    }

    internal bool RemoveTurnScopedRule(int id) => _state.TurnScopedRules.RemoveAll(r => r.Id == id) > 0;

    private bool TurnRuleLive(TurnScopedRule rule) =>
        !UntilPassed(rule.Until) && (rule.WhileInPlay is not { } cardId || _state.Cards.TryGetValue(cardId, out var card) && InPlay(card));

    /// <summary>The live rules of a kind that apply to <paramref name="playerId"/> (his own and the each-player ones), oldest first. Null player: every live rule of the kind.</summary>
    internal IEnumerable<TurnScopedRule> TurnScopedRulesFor(string kind, int? playerId = null) =>
        _state.TurnScopedRules.Count == 0 ? [] : _state.TurnScopedRules.Where(r => r.Kind == kind && (playerId is null || r.PlayerId is null || r.PlayerId == playerId) && TurnRuleLive(r));

    // A turn has begun: the rules whose duration has passed, or whose card has left play, are removed.
    private void ExpireTurnScopedRules()
    {
        if (_state.TurnScopedRules.Count == 0) return;
        foreach (var rule in _state.TurnScopedRules.Where(r => !TurnRuleLive(r)).ToArray())
        {
            _state.TurnScopedRules.Remove(rule);
            Emit("turnRuleEnded", $"{rule.Source}: its rule has ended.", rule.PlayerId);
        }
    }

    // ---- EndTurnExtraDraw: EndTurn, before the End Phase's own draw ---------------------------------------------------------------------------------------------------------------

    private void DrawTurnRuleEndTurnCards(PlayerState player)
    {
        if (_state.TurnScopedRules.Count == 0) return;
        // Every rule is judged on the turn as it stands now: the cards one rule draws do not unmake another's condition.
        var owed = TurnScopedRulesFor(TurnRuleKinds.EndTurnExtraDraw, player.Id).Where(r => r.Amount > 0 && (r.Counter is null || TurnCountOf(r.Counter, player.Id) <= r.Max)).ToArray();
        foreach (var rule in owed)
        {
            Emit("turnRuleDraw", $"{rule.Source}: {player.Name} draws {rule.Amount} more card{(rule.Amount == 1 ? "" : "s")} at the end of the turn.", player.Id);
            for (var i = 0; i < rule.Amount && _state.WinnerId is null; i++) Draw(player);
        }
    }

    // ---- LimitedAsOpen: the Limited timing check, and the announcement -----------------------------------------------------------------------------------------------------------------

    private static string TurnRuleUseKey(TurnScopedRule rule, int playerId) => $"p4:rule-use:{rule.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private TurnScopedRule? LimitedAsOpenRule(int playerId, string? sourceCardId)
    {
        if (_state.TurnScopedRules.Count == 0 || _state.Phase != Phase.Action || _state.PendingChoice is not null) return null;
        var source = sourceCardId is not null && _state.Cards.TryGetValue(sourceCardId, out var card) ? card : null;
        foreach (var rule in TurnScopedRulesFor(TurnRuleKinds.LimitedAsOpen, playerId))
        {
            if (rule.Arg is { } type && (source is null || !string.Equals(Definition(source).Type.ToString(), type, StringComparison.Ordinal))) continue;
            if (PeriodCountOf(TurnRuleUseKey(rule, playerId), Period.Turn) < Math.Max(1, rule.Amount)) return rule;
        }
        return null;
    }

    // The Limited timing check (ValidateAbility, AbilityActions): a standing rule lets this player take this Limited action as an Open action now.
    private bool TurnRuleLimitedAsOpen(int playerId, string? sourceCardId) => LimitedAsOpenRule(playerId, sourceCardId) is not null;

    // UseAbility, as the action is announced: a Limited action only a standing rule allowed spends one of that rule's uses this turn.
    private void NoteTurnRuleAnnouncement(GameCommand command)
    {
        if (_state.TurnScopedRules.Count == 0 || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Timing != "Limited") return;
        if (_state.Phase != Phase.Action || command.PlayerId == _state.ActivePlayerId || LimitedInsteadOfOpen(command.PlayerId)) return;
        if (LimitedAsOpenRule(command.PlayerId, command.CardId) is not { } rule) return;
        AddPeriodCount(TurnRuleUseKey(rule, command.PlayerId), Period.Turn);
        Emit("turnRuleUsed", $"{rule.Source}: {Player(command.PlayerId).Name} takes a Limited action as an Open action.", command.PlayerId, command.CardId);
    }
}
