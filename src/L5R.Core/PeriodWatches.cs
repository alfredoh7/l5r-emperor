using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P7 (issue #683; docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): period stamps and negation watches.
//
// 1. PERIOD STAMPS. "The first time each turn / battle / phase / game" is one stamp in the AbilityUses dictionary that every card and player already has:
//    key "period:<Period>:<key>" holding the number of the period INSTANCE it was spent in (this turn, this phase of this turn, this battle, the game). The stamp is
//    compared with the current instance whenever it is read, so the next turn / phase / battle resets it with nothing to clear, exactly as TrySpendPhaseUse (PhaseLimits.cs)
//    already does for phases. AbilityDefinition.UsesPer makes a printed ability use it: AbilityUseSpent / SpendAbilityUse (AbilityLimits.cs), which every offer and
//    ValidateAbility consult beside OncePerGame, read it. A stamp on a card is wiped when the card leaves play and merged by an overlay like every other use.
//    Counters ("the second copy played this battle") are PeriodCounters records, read with PeriodCount and bumped with AddPeriodCount.
//
// 2. NEGATION WATCHES. "Negate his next destruction", "Before the first time each turn another player's card's effect destroys one of your Loyal Personalities: negate the
//    destruction" are one store consulted at the choke points where a bow, a destruction, a movement, a straightening, a dishonoring or a Political action would happen:
//    TryConsumeWatch(kind, target, sourcePlayer). A watch is either
//      * DYNAMIC -- a NegationWatch record an action creates (AddNegationWatch): a target card or a keyword of its owner's cards, a number of negations left, an optional
//        refresh period (at most once per period), and the last turn it is good for; a watch owned by a card leaves with the card; or
//      * STANDING -- a StandingWatch rule a card's feature file registers once on EngineHooks.StandingWatches: it covers the card itself, its controller's cards that fit a
//        keyword, or the Followers attached to it, fires at most once per Period (Game: once ever) for each card that carries it, may be conditional, and may run a
//        follow-up when it negates (Bayushi Kazutoshi: move him home, bow him, dishonor him). What a standing rule has spent is MatchState.WatchSpent, so it outlives the card.
//    The effect is negated only when the SOURCE of the effect is another player's (Source OtherPlayers) or any player's (Any): a cost, the rulebook and a battle's own
//    resolution are not card effects and are never negated by a watch. Order at every choke point: the delay store, the protections and shields already there, THEN the
//    watches, then the effect. Each consumption is recorded (WatchConsumptions) so a follow-up ("if the destruction was negated") can ask.
//
// Not hooked (no choke point named by the prompt; P14 owns the Honor-change registry): HonorGain. The kind exists so a later pass can add the one line.

public enum Period { Turn, Battle, Phase, Game }

public enum WatchKind { Bow, Destroy, Move, Straighten, Dishonor, PoliticalAction, HonorGain }

public enum WatchSource
{
    /// <summary>Only an effect of a card another player controls than the card's controller (the usual "another player's card's effect").</summary>
    OtherPlayers,
    /// <summary>Any card effect, the controller's own included.</summary>
    Any,
}

public enum WatchScope
{
    /// <summary>The card that carries the rule.</summary>
    Self,
    /// <summary>Cards its controller controls that fit TargetKeyword (and TargetType, when set), the carrier included when it fits.</summary>
    OwnersCards,
    /// <summary>The Followers attached to the carrier.</summary>
    AttachedFollowers,
    /// <summary>The controller himself: only a Political action no card is named by (the player-level "negate the first Political action each turn").</summary>
    Player,
}

/// <summary>A watch an action created (state, serialized with the match). A card-owned watch ends when its owner card leaves play.</summary>
public sealed class NegationWatch
{
    public string Id { get; set; } = "";
    public string? OwnerCardId { get; set; }
    public int OwnerPlayerId { get; set; }
    public WatchKind Kind { get; set; }
    /// <summary>One card, or</summary>
    public string? TargetCardId { get; set; }
    /// <summary>every card of OwnerPlayerId with this keyword (and TargetType, when set). Neither set: the player-level watch (Political actions only).</summary>
    public string? TargetKeyword { get; set; }
    public CardType? TargetType { get; set; }
    public WatchSource Source { get; set; } = WatchSource.OtherPlayers;
    /// <summary>Negations left; a negative number never runs out.</summary>
    public int Remaining { get; set; } = 1;
    /// <summary>When set the watch negates at most once per instance of this period.</summary>
    public Period? Refresh { get; set; }
    public int RefreshStamp { get; set; } = -1;
    /// <summary>The last turn (MatchState.TurnNumber) the watch is good for; null: until it is used up.</summary>
    public int? LastTurn { get; set; }
    public string? Arg { get; set; }
}

public sealed class PeriodCount
{
    public int Instance { get; set; }
    public int Count { get; set; }
}

/// <summary>One negation a watch made, kept for the turn it happened in and the next (so "if his destruction was negated" can be asked).</summary>
public sealed class WatchConsumption
{
    public string WatchId { get; set; } = "";
    public WatchKind Kind { get; set; }
    public string? CardId { get; set; }
    public int? SourcePlayerId { get; set; }
    public int Turn { get; set; }
    public int Battle { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(1780)]
    public List<NegationWatch> NegationWatches { get; set; } = [];
    [JsonPropertyOrder(1784)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WatchSequence { get; set; }
    /// <summary>What the standing rules have spent: "ruleId|ownerCardId" -> the period instance.</summary>
    [JsonPropertyOrder(1781)]
    public Dictionary<string, int> WatchSpent { get; set; } = [];
    [JsonPropertyOrder(1782)]
    public Dictionary<string, PeriodCount> PeriodCounters { get; set; } = [];
    [JsonPropertyOrder(1783)]
    public List<WatchConsumption> WatchConsumptions { get; set; } = [];
}

/// <summary>What a choke point tells a watch about the effect that is about to happen.</summary>
internal sealed record WatchContext(WatchKind Kind, CardInstance? Target, int? SourcePlayerId, GameCommand? Command);

/// <summary>A watch a card carries (registered once, on EngineHooks.StandingWatches). Owner selects the carrier cards in play; Covers and TargetKeyword pick what it protects.</summary>
internal sealed record StandingWatch(
    string Id,
    Func<CardDefinition, bool> OwnerIs,
    WatchKind Kind,
    WatchScope Scope = WatchScope.Self,
    string? TargetKeyword = null,
    CardType? TargetType = null,
    WatchSource Source = WatchSource.OtherPlayers,
    Period Period = Period.Turn,
    Func<MatchEngine, WatchContext, CardInstance, bool>? Condition = null,
    Action<MatchEngine, WatchContext, CardInstance>? OnNegated = null)
{
    /// <summary>The rule for the cards whose catalogue Effect is `effect` (how a card names its own traits).</summary>
    public static Func<CardDefinition, bool> OnEffect(string effect) => d => d.Effect == effect;
}

internal static partial class EngineHooks
{
    /// <summary>Standing negation watches (PeriodWatches.cs). Register with a unique id and a priority of 10000 or more; the rules are read at every choke point.</summary>
    internal static readonly OrderedHooks<Func<StandingWatch>> StandingWatches = new();
}

public sealed partial class MatchEngine
{
    // ---- period stamps ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string PeriodUseKeyPrefix = "period:";

    /// <summary>The number of the current instance of a period: this turn, this phase of this turn (PrintedPhases), this battle (a battle's number; one value for "no battle"), the game.</summary>
    private int PeriodInstance(Period period) => period switch
    {
        Period.Turn => _state.TurnNumber,
        Period.Phase => PhaseInstance(),
        Period.Battle => _state.CurrentBattleProvince is null ? -1 - _state.TurnNumber : _state.BattleNumber + 1,
        _ => 1
    };

    private bool PeriodUseSpent(Dictionary<string, int> uses, string key, Period period) =>
        uses.TryGetValue(PeriodUseKeyPrefix + period + ":" + key, out var stamp) && stamp == PeriodInstance(period);

    /// <summary>Spends the use of `key` on this card or player in the current instance of `period`; false (and no change) when it was already spent in it.</summary>
    internal bool TrySpendPeriodUse(Dictionary<string, int> uses, string key, Period period)
    {
        if (PeriodUseSpent(uses, key, period)) return false;
        uses[PeriodUseKeyPrefix + period + ":" + key] = PeriodInstance(period);
        return true;
    }

    // ---- counters ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The count recorded under `key` in the current instance of `period` (0 once the period has moved on).</summary>
    internal int PeriodCountOf(string key, Period period) =>
        _state.PeriodCounters.TryGetValue(period + "|" + key, out var entry) && entry.Instance == PeriodInstance(period) ? entry.Count : 0;

    /// <summary>Adds to the counter and returns its new value; a counter of an earlier instance restarts from zero.</summary>
    internal int AddPeriodCount(string key, Period period, int by = 1)
    {
        var instance = PeriodInstance(period); var stored = period + "|" + key;
        if (!_state.PeriodCounters.TryGetValue(stored, out var entry) || entry.Instance != instance) entry = _state.PeriodCounters[stored] = new PeriodCount { Instance = instance };
        entry.Count += by;
        return entry.Count;
    }

    // ---- dynamic watches ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------



    /// <summary>A watch whose Arg starts with this and is shared with others is one negation in all: the first to negate removes every watch with the same Arg (Earth's Protection: "his next bowing or movement").</summary>
    internal const string ExclusiveWatchPrefix = "exclusive:";

    /// <summary>Adds a watch and returns it. An empty Id gets a unique one.</summary>
    internal NegationWatch AddNegationWatch(NegationWatch watch)
    {
        if (string.IsNullOrEmpty(watch.Id)) watch.Id = $"watch-{_state.TurnNumber}-{_state.NegationWatches.Count}-{++_state.WatchSequence}";
        _state.NegationWatches.Add(watch);
        return watch;
    }

    /// <summary>Whether a negation by `kind` was made for `cardId` (any card when null) this turn or, with `sinceBattle`, in this battle.</summary>
    internal bool WatchNegated(WatchKind kind, string? cardId = null, bool sinceBattle = false) =>
        _state.WatchConsumptions.Any(c => c.Kind == kind && (cardId is null || c.CardId == cardId) && c.Turn == _state.TurnNumber && (!sinceBattle || c.Battle == _state.BattleNumber));

    // ---- consumption ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The player whose card effect is running right now (a resolving action), or null when no action is: a cost, the rulebook and a battle's own resolution have none.</summary>
    private int? EffectSourcePlayer() => (_runningCommand ?? _attackCommandInResolution)?.PlayerId;

    private bool WatchSourceMatches(WatchSource source, CardInstance? target, int ownerPlayerId, int? sourcePlayerId) =>
        sourcePlayerId is int actor && (source == WatchSource.Any || actor != (target?.OwnerId ?? ownerPlayerId));

    /// <summary>
    /// The one question every choke point asks: would a watch negate this effect? True when one did (the effect must not happen); the watch is spent and the negation recorded.
    /// `sourcePlayerId` is the player whose card's effect it is (null: not a card effect, nothing is negated).
    /// </summary>
    internal bool TryConsumeWatch(WatchKind kind, CardInstance? target, int? sourcePlayerId, GameCommand? command = null)
    {
        if (sourcePlayerId is null) return false;
        if (kind is WatchKind.Bow or WatchKind.Destroy or WatchKind.Move && (HiromiUnnegatable() || CurrentActionUnnegatable())) return false; // an action whose bowing, destruction or movement will not be negated
        var context = new WatchContext(kind, target, sourcePlayerId, command);
        // Dynamic watches, oldest first.
        foreach (var watch in _state.NegationWatches.ToArray())
        {
            if (watch.Kind != kind || !WatchLive(watch) || !DynamicWatchCovers(watch, target) || !WatchSourceMatches(watch.Source, target, watch.OwnerPlayerId, sourcePlayerId)) continue;
            if (watch.Refresh is { } period && watch.RefreshStamp == PeriodInstance(period)) continue;
            if (watch.Refresh is { } refresh) watch.RefreshStamp = PeriodInstance(refresh);
            if (watch.Remaining > 0) watch.Remaining--;
            if (watch.Remaining == 0) _state.NegationWatches.Remove(watch);
            if (watch.Arg is { } linked && linked.StartsWith(ExclusiveWatchPrefix, StringComparison.Ordinal)) _state.NegationWatches.RemoveAll(w => w.Arg == linked); // linked watches ("his next bowing OR movement"): the first to fire ends them all
            RecordWatchNegation(watch.Id, context, null);
            return true;
        }
        // Standing watches: for each rule, each in-play card that carries it.
        foreach (var rule in StandingWatchRules())
        {
            if (rule.Kind != kind) continue;
            foreach (var owner in _state.Cards.Values.Where(c => InPlay(c) && rule.OwnerIs(Definition(c))).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            {
                if (!StandingWatchCovers(rule, owner, target) || !WatchSourceMatches(rule.Source, target, owner.OwnerId, sourcePlayerId)) continue;
                var key = rule.Id + "|" + owner.Id;
                if (_state.WatchSpent.TryGetValue(key, out var stamp) && stamp == PeriodInstance(rule.Period)) continue;
                if (rule.Condition is { } condition && !condition(this, context, owner)) continue;
                _state.WatchSpent[key] = PeriodInstance(rule.Period);
                RecordWatchNegation(rule.Id, context, owner);
                rule.OnNegated?.Invoke(this, context, owner);
                return true;
            }
        }
        return false;
    }

    private bool WatchLive(NegationWatch watch)
    {
        if (watch.LastTurn is int last && _state.TurnNumber > last) return false;
        return watch.OwnerCardId is null || _state.Cards.TryGetValue(watch.OwnerCardId, out var owner) && InPlay(owner);
    }

    private bool DynamicWatchCovers(NegationWatch watch, CardInstance? target)
    {
        if (target is null) return watch.TargetCardId is null && watch.TargetKeyword is null; // the player-level watch
        if (watch.TargetCardId is not null) return watch.TargetCardId == target.Id;
        return watch.TargetKeyword is not null && target.OwnerId == watch.OwnerPlayerId && InPlay(target) && Keyword(target, watch.TargetKeyword) && (watch.TargetType is null || Definition(target).Type == watch.TargetType);
    }

    private bool StandingWatchCovers(StandingWatch rule, CardInstance owner, CardInstance? target) => rule.Scope switch
    {
        WatchScope.Self => target is not null && target.Id == owner.Id,
        WatchScope.OwnersCards => target is not null && target.OwnerId == owner.OwnerId && InPlay(target)
            && (rule.TargetKeyword is null || Keyword(target, rule.TargetKeyword)) && (rule.TargetType is null || Definition(target).Type == rule.TargetType),
        WatchScope.AttachedFollowers => target is not null && target.Zone == Zone.Attached && target.AttachedTo == owner.Id && Definition(target).Type == CardType.Follower,
        _ => target is null // WatchScope.Player: a Political action that names no card
    };

    private void RecordWatchNegation(string watchId, WatchContext context, CardInstance? owner)
    {
        _state.WatchConsumptions.RemoveAll(c => c.Turn < _state.TurnNumber - 1);
        _state.WatchConsumptions.Add(new WatchConsumption { WatchId = watchId, Kind = context.Kind, CardId = context.Target?.Id, SourcePlayerId = context.SourcePlayerId, Turn = _state.TurnNumber, Battle = _state.BattleNumber });
        if (context.Target is null) return; // the player-level kind names its own event where it is applied
        var by = owner is null ? "" : $" ({Definition(owner).Name})";
        var (type, text) = context.Kind switch
        {
            WatchKind.Bow => ("bowNegated", "bowing"),
            WatchKind.Destroy => ("destructionNegated", "destruction"),
            WatchKind.Move => ("moveNegated", "movement"),
            WatchKind.Straighten => ("straightenNegated", "straightening"),
            WatchKind.Dishonor => ("dishonorNegated", "dishonoring"),
            _ => ("effectNegated", "effect")
        };
        Emit(type, $"{Definition(context.Target).Name}'s {text} was negated before it resolved{by}.", context.Target.OwnerId, context.Target.Id);
    }

    // Test seams (L5R.Core.Tests has InternalsVisibleTo): the live card, and the ability-use limit read and spent the way ValidateAbility and the resolution do.
    internal CardInstance CardForTest(string id) => Card(id);
    internal bool UseSpentForTest(Dictionary<string, int> uses, AbilityDefinition ability, string key) => AbilityUseSpent(uses, ability, key);
    internal void SpendUseForTest(Dictionary<string, int> uses, AbilityDefinition ability, string key) => SpendAbilityUse(uses, ability, key);

    private static StandingWatch[]? _standingWatchRules;
    /// <summary>Test seam (L5R.Core.Tests has InternalsVisibleTo): extra rules a test supplies, read beside the registered ones. Always null outside a test.</summary>
    internal static List<StandingWatch>? TestStandingWatches;

    private static IEnumerable<StandingWatch> StandingWatchRules()
    {
        _standingWatchRules ??= EngineHooks.StandingWatches.Items.Select(item => item()).ToArray();
        return TestStandingWatches is { Count: > 0 } extra ? _standingWatchRules.Concat(extra) : _standingWatchRules;
    }

    // ---- the Political action announce choke point ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Fired as an action is announced (CardAbilities.cs, beside Kamalakar's negation): a Political action that names a card a watch covers, or any Political action when a
    /// player-level watch holds, is negated whole (taken, its costs paid) unless Protection stops the negation. True when it was negated and is over.
    /// </summary>
    private bool WatchNegatesPoliticalAction(GameCommand command)
    {
        if (_state.NegationWatches.Count == 0 && !StandingWatchRules().Any()) return false;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !IsAction(ability)) return false;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        if (!ActionCarriesKeyword(ability, source, "Political")) return false;
        var named = ActionTargetIds(command).Where(id => _state.Cards.ContainsKey(id)).Select(Card).Where(InPlay).OrderBy(c => c.Id, StringComparer.Ordinal).Cast<CardInstance?>().Append(null);
        foreach (var target in named)
        {
            if (!TryConsumeWatch(WatchKind.PoliticalAction, target, command.PlayerId, command)) continue;
            if (ActionProtected(command))
            {
                Emit("actionProtected", $"Protection negates the negation -- this action cannot be negated.", command.PlayerId, command.CardId);
                return false;
            }
            Emit("actionNegated", $"{Player(command.PlayerId).Name}'s action ({ability.Label}) was negated before it resolved.", command.PlayerId, command.CardId);
            ResolveNegatedAction(command);
            return true;
        }
        return false;
    }

}
