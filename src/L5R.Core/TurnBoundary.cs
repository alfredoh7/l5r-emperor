using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// P4 (issue #690): THE TURN BOUNDARY. Engine only; the cards that use it are wired in a later wave (docs/decisions/2026-10-06-690-p4-turn-boundary.md).
//
//   1. EngineHooks.TurnBegins -- "after your turn begins" / "after each player's turn begins" / "after another player's turn begins". Called once per turn from StartTurn, right after
//      AdvanceStraightenHolds(TurnBegins) and before the Straighten Phase. A callback returns true when it opened a pending choice: the sequence stops there, its place is kept in
//      MatchState.PendingTurnBegins (so a saved game resumes), and ResumeTurnBegins carries on with the callbacks that have not run and then the Straighten Phase.
//      A callback that needs a decision can use the generic turn-begin choice (OpenTurnBeginChoice + EngineHooks.TurnBeginChoices) and write no pending-choice plumbing at all.
//   2. TurnUntil -- one vocabulary for the durations that end, or the effects that come due, as a turn begins: "until/after your next turn begins", "your second turn from now", "the
//      next turn" (whoever's), "until the game ends". Owner and Skip mean what they mean for a StraightenHold (Owner null: the turn in progress, whoever's; Skip: how many such turn
//      beginnings go by first); they are stored as the absolute turn they name, the way TimedRemoval.AtTurnsTaken and CardInstance.ExpiresAtTurnsTaken already are, so counting twice or
//      counting the turn a duration was made in cannot happen. The same TurnUntil drives the delay store (DelayUntilTurn), a timed removal (ScheduleTimedRemovalUntil), an Event's stay in
//      play (SetEventExpiry), a player-level granted ability (TurnBoundaryGrants.cs) and a standing rule (TurnBoundaryRules.cs).
//
// ORDER as a turn begins: the durations that end "when/until that turn begins" end first and the delayed effects waiting for it are applied (ApplyTurnBoundaryExpiry), THEN the turn-begin
// callbacks run, in hook priority order. So an "until your next turn begins" rule is already over when an "after your turn begins" trait resolves.
// ======================================================================================================================================================================================

/// <summary>The turn that is beginning: whose it is, the game's turn number, and how many turns that player has now begun (this one included).</summary>
public readonly record struct TurnBeginsContext(int PlayerId, int TurnNumber, int TurnsTaken)
{
    /// <summary>Whether the card or rule of <paramref name="controllerId"/> printed with this scope fires for this turn.</summary>
    public bool Fires(TurnBeginScope scope, int controllerId) => scope switch
    {
        TurnBeginScope.Yours => controllerId == PlayerId,
        TurnBeginScope.AnotherPlayers => controllerId != PlayerId,
        _ => true
    };
}

/// <summary>Whose turn a turn-begin trait names: "after your turn begins", "after each player's turn begins", "after another player's turn begins".</summary>
public enum TurnBeginScope { Yours, EachPlayers, AnotherPlayers }

/// <summary>
/// A point a duration lasts until, or a delayed effect waits for. <see cref="Kind"/> is <see cref="TurnBegins"/> or <see cref="GameEnd"/>. For TurnBegins, <see cref="Owner"/> is the
/// player whose turn counts (null: any player's) and <see cref="At"/> the turn it names: that player's TurnsTaken value, or the game's TurnNumber when Owner is null. Build one with the
/// engine's UntilOwnersNextTurnBegins / UntilOwnersSecondTurnBegins / UntilOwnersTurnBegins / UntilTurnBegins, or <see cref="GameEnds"/>.
/// </summary>
public sealed record TurnUntil(string Kind, int? Owner = null, int At = 0)
{
    public const string TurnBegins = "turn-begins";
    public const string GameEnd = "game-end";
    /// <summary>"Until the game ends": never passes.</summary>
    public static TurnUntil GameEnds() => new(GameEnd);
}

/// <summary>One answer of a generic turn-begin choice. <see cref="CardId"/> and <see cref="PlayerId"/> are what the option names, if anything (the offered command carries them as its target).</summary>
public sealed record TurnBeginOption(string Id, string Label, string? CardId = null, int? PlayerId = null);

/// <summary>The answer handed to the resolver of a generic turn-begin choice.</summary>
public readonly record struct TurnBeginAnswer(string Key, int ChooserId, string? SourceCardId, TurnBeginOption Option, TurnBeginsContext Turn);

/// <summary>A turn-begin sequence that stopped for a decision: whose turn was beginning, which callback stopped it, and the generic choice if that is what it opened.</summary>
public sealed class PendingTurnBegins
{
    public int PlayerId { get; set; }
    public int TurnNumber { get; set; }
    /// <summary>The registration id of the callback that stopped the sequence; the sequence resumes after it (or with it again).</summary>
    public string HookId { get; set; } = "";
    /// <summary>The generic choice that is open (OpenTurnBeginChoice), or null when the callback opened a pending choice of its own.</summary>
    public string? ChoiceKey { get; set; }
    public int ChooserId { get; set; }
    public string? SourceCardId { get; set; }
    public List<TurnBeginOption> Options { get; set; } = [];
}

public sealed partial class MatchState
{
    // A decision: the turn-begin sequence waits for an answer (see the registration below).
    [JsonPropertyOrder(22100)]
    public PendingTurnBegins? PendingTurnBegins { get; set; }
}

internal static partial class EngineHooks
{
    /// <summary>"After a turn begins". True: the callback opened a pending choice and the sequence stops until ResumeTurnBegins.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, TurnBeginsContext, bool>> TurnBegins = new();
    /// <summary>The resolvers of generic turn-begin choices, first answer: null not mine; false done, go on with the next callback; true run my callback again (more of my cards to ask).</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, TurnBeginAnswer, bool?>> TurnBeginChoices = new();
}

public static class TurnBoundary
{
    /// <summary>PendingChoice.Type of the generic turn-begin choice.</summary>
    public const string ChoiceType = "turnBeginsChoice";
    /// <summary>The command (and offered option) type that answers it; Choice carries the option id.</summary>
    public const string ChooseCommand = "chooseTurnBegins";

    /// <summary>The delay store's Until string for a turn-begin point. One string per (owner, turn), so every effect due at one turn beginning is applied in the order it was recorded.</summary>
    public static string EncodeDelayUntil(TurnUntil until) => until.Kind == TurnUntil.GameEnd ? DelayUntil.GameEnd
        : $"{DelayUntil.TurnBeginsPrefix}{(until.Owner is int owner ? owner.ToString(System.Globalization.CultureInfo.InvariantCulture) : "*")}:{until.At.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static TurnUntil? ParseDelayUntil(string until)
    {
        if (until == DelayUntil.GameEnd) return TurnUntil.GameEnds();
        if (!until.StartsWith(DelayUntil.TurnBeginsPrefix, StringComparison.Ordinal)) return null;
        var parts = until[DelayUntil.TurnBeginsPrefix.Length..].Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var at)) return null;
        if (parts[0] == "*") return new(TurnUntil.TurnBegins, null, at);
        return int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var owner) ? new(TurnUntil.TurnBegins, owner, at) : null;
    }

    /// <summary>DelayUntil.Describe's text for a turn-begin point (it has no player names to hand), or null when <paramref name="until"/> is not one.</summary>
    public static string? DescribeDelayUntil(string until) => ParseDelayUntil(until) switch
    {
        null => null,
        { Kind: TurnUntil.GameEnd } => "the game ends",
        { Owner: int owner } parsed => $"after player {owner + 1}'s turn {parsed.At} begins",
        var parsed => $"after turn {parsed.At} of the game begins"
    };
}

public sealed partial class MatchEngine
{
    // ---- the durations ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Until [after] your next turn begins" for <paramref name="ownerId"/> (Skip 0), "your second turn from now" (Skip 1), and so on: <paramref name="skip"/> of his turn beginnings go by first.</summary>
    internal TurnUntil UntilOwnersTurnBegins(int ownerId, int skip = 0) => new(TurnUntil.TurnBegins, ownerId, Player(ownerId).TurnsTaken + 1 + Math.Max(0, skip));
    internal TurnUntil UntilOwnersNextTurnBegins(int ownerId) => UntilOwnersTurnBegins(ownerId);
    internal TurnUntil UntilOwnersSecondTurnBegins(int ownerId) => UntilOwnersTurnBegins(ownerId, 1);
    /// <summary>"Until the next turn begins", whoever's it is (Owner null), <paramref name="skip"/> turn beginnings going by first.</summary>
    internal TurnUntil UntilTurnBegins(int skip = 0) => new(TurnUntil.TurnBegins, null, _state.TurnNumber + 1 + Math.Max(0, skip));

    /// <summary>Whether the point has come: the named turn has begun. "Until the game ends" never passes.</summary>
    internal bool UntilPassed(TurnUntil until) => until.Kind == TurnUntil.TurnBegins && (until.Owner is int owner ? Player(owner).TurnsTaken >= until.At : _state.TurnNumber >= until.At);

    internal string DescribeUntil(TurnUntil until)
    {
        if (until.Kind != TurnUntil.TurnBegins) return "until the game ends";
        var away = until.Owner is int owner ? until.At - Player(owner).TurnsTaken : until.At - _state.TurnNumber;
        var whose = until.Owner is int id ? $"{Player(id).Name}'s" : "the";
        return away switch
        {
            <= 1 => $"until {whose} next turn begins",
            2 => $"until {whose} second turn from now begins",
            _ => $"until {whose} turn {away} turns from now begins"
        };
    }

    // ---- the delay store, timed removal and Events in play, on the same durations --------------------------------------------------------------------------------------------------

    /// <summary>The delay store's Until for a turn-begin point: pass it wherever a DelayUntil constant goes (ArmDelayForHeldAction, WithDelayScope, ScheduleDelayedEffect). An effect delayed "until
    /// the game ends" is recorded and never applied.</summary>
    internal string DelayUntilTurn(TurnUntil until) => TurnBoundary.EncodeDelayUntil(until);
    internal string DelayUntilOwnersNextTurnBegins(int ownerId) => DelayUntilTurn(UntilOwnersNextTurnBegins(ownerId));
    internal string DelayUntilOwnersSecondTurnBegins(int ownerId) => DelayUntilTurn(UntilOwnersSecondTurnBegins(ownerId));

    // The turn has begun: every delayed effect waiting for a turn-begin point that has now come, each point's effects in the order they were recorded, the earlier points first.
    private void ApplyTurnBeginDelayedEffects()
    {
        if (_state.DelayedEffects.Count == 0) return;
        var due = new List<(string Until, int At)>();
        foreach (var until in _state.DelayedEffects.Select(e => e.Until).Distinct(StringComparer.Ordinal))
            if (TurnBoundary.ParseDelayUntil(until) is { } point && UntilPassed(point)) due.Add((until, point.At));
        foreach (var (until, _) in due.OrderBy(d => d.At).ThenBy(d => d.Until, StringComparer.Ordinal))
        {
            if (_state.WinnerId is not null) return;
            ApplyDelayedEffects(until);
        }
    }

    /// <summary>"Remove it from the game when [that turn] begins" through the existing timed-removal record (EmperorMechD.cs), which MechDTurnBegins carries out. The point must name an owner;
    /// "until the game ends" schedules nothing.</summary>
    internal void ScheduleTimedRemovalUntil(CardInstance card, TurnUntil until, string source)
    {
        if (until.Kind != TurnUntil.TurnBegins) return;
        Require(until.Owner is not null, "A timed removal waits for a named player's turn.");
        var owner = until.Owner!.Value;
        MechD.TimedRemovals.Add(new(card.Id, TimedRemovalWhen.PlayersNextTurnBegins, owner, until.At, source));
        Emit("timedRemovalScheduled", $"{source}: {Definition(card).Name} stays in play {DescribeUntil(until)}, and is then removed from the game.", owner, card.Id);
    }

    /// <summary>An Event staying in play (Zone.EventInPlay) leaves it as that turn of its owner begins, through CardInstance.ExpiresAtTurnsTaken (StartTurn's expiry). "Until the game ends": it stays.</summary>
    internal void SetEventExpiry(CardInstance eventCard, TurnUntil until)
    {
        if (until.Kind != TurnUntil.TurnBegins) { eventCard.ExpiresAtTurnsTaken = null; return; }
        Require(until.Owner == eventCard.OwnerId, "An Event in play ends as a turn of its own owner begins.");
        eventCard.ExpiresAtTurnsTaken = until.At;
    }

    // ---- the turn-begin sequence -----------------------------------------------------------------------------------------------------------------------------------------------------

    // StartTurn, after AdvanceStraightenHolds(TurnBegins): true when the sequence stopped (a decision is pending, or the game is over) and the Straighten Phase must wait.
    private bool RunTurnBegins(PlayerState player)
    {
        _state.PendingTurnBegins = null;
        ApplyTurnBoundaryExpiry();
        if (_state.WinnerId is not null) return true;
        return ContinueTurnBegins(player, 0);
    }

    // What ends, and what comes due, as the turn begins -- before any "after the turn begins" callback.
    private void ApplyTurnBoundaryExpiry()
    {
        ExpirePlayerAbilityGrants();
        ExpireTurnScopedRules();
        ApplyTurnBeginDelayedEffects();
    }

    private bool ContinueTurnBegins(PlayerState player, int from)
    {
        var hooks = EngineHooks.TurnBegins.Items;
        var context = new TurnBeginsContext(player.Id, _state.TurnNumber, player.TurnsTaken);
        for (var i = from; i < hooks.Length; i++)
        {
            var paused = hooks[i](this, context);
            if (_state.WinnerId is not null) { _state.PendingTurnBegins = null; return true; }
            if (!paused) continue;
            if (_state.PendingChoice is null) throw new InvalidOperationException($"Turn-begin callback {EngineHooks.TurnBegins.Names[i]} reported a pending choice but opened none.");
            var pending = _state.PendingTurnBegins ??= new PendingTurnBegins();
            pending.PlayerId = player.Id; pending.TurnNumber = _state.TurnNumber; pending.HookId = EngineHooks.TurnBegins.Names[i];
            return true;
        }
        _state.PendingTurnBegins = null;
        return false;
    }

    /// <summary>
    /// The decision a turn-begin callback opened has been answered (its PendingChoice is cleared): run the callbacks that have not run yet, then begin the Straighten Phase.
    /// <paramref name="repeat"/>: run the callback that stopped the sequence again first (it has more of its own cards to ask about). Nothing happens while another choice is still open.
    /// </summary>
    internal void ResumeTurnBegins(bool repeat = false)
    {
        var paused = _state.PendingTurnBegins ?? throw new InvalidOperationException("No turn-begin sequence is waiting.");
        if (_state.WinnerId is not null) { _state.PendingTurnBegins = null; return; }
        if (_state.PendingChoice is not null) return;
        var names = EngineHooks.TurnBegins.Names;
        var index = -1;
        for (var i = 0; i < names.Count; i++) if (names[i] == paused.HookId) { index = i; break; }
        // A callback that no longer exists (a save from another build): nothing of the sequence is run twice; the turn goes on.
        var next = index < 0 ? names.Count : repeat ? index : index + 1;
        var player = Player(paused.PlayerId);
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        if (ContinueTurnBegins(player, next)) return;
        StraightenPhase(player);
    }

    /// <summary>The cards in play carrying <paramref name="effect"/> whose turn-begin trait of that scope fires for this turn, in a stable order.</summary>
    internal CardInstance[] TurnBeginCards(string effect, TurnBeginsContext context, TurnBeginScope scope) =>
        _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == effect && context.Fires(scope, c.OwnerId)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();

    // ---- the generic turn-begin choice -----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Opens a choice for <paramref name="chooserId"/> from inside a turn-begin callback (or from the resolver of an earlier one) and returns true, the value the callback returns. With no
    /// option there is nothing to choose and nothing is opened (false). The answer goes to the EngineHooks.TurnBeginChoices resolver that recognises <paramref name="key"/>.
    /// The prompt and the labels are shown to the chooser only; they must not name a card he may not see.
    /// </summary>
    internal bool OpenTurnBeginChoice(string key, int chooserId, string? sourceCardId, string prompt, IEnumerable<TurnBeginOption> options)
    {
        var list = options.ToList();
        if (list.Count == 0) return false;
        var pending = _state.PendingTurnBegins ??= new PendingTurnBegins { PlayerId = _state.ActivePlayerId, TurnNumber = _state.TurnNumber };
        pending.ChoiceKey = key; pending.ChooserId = chooserId; pending.SourceCardId = sourceCardId; pending.Options = list;
        _state.PendingChoice = new(TurnBoundary.ChoiceType, chooserId, sourceCardId, prompt);
        _state.PriorityPlayerId = chooserId; _state.ConsecutivePasses = 0;
        return true;
    }

    private bool HandleTurnBeginChoice(string type, GameCommand command)
    {
        if (type != TurnBoundary.ChoiceType) return false;
        if (_state.PendingTurnBegins is not { ChoiceKey: not null } pending) throw new RuleViolation("No turn-begin choice is waiting.");
        Require(command.Type == TurnBoundary.ChooseCommand && command.PlayerId == pending.ChooserId, "Choose one of the offered options.");
        var option = pending.Options.FirstOrDefault(o => o.Id == command.Choice);
        Require(option is not null, "That is not one of the offered options.");
        var answer = new TurnBeginAnswer(pending.ChoiceKey, pending.ChooserId, pending.SourceCardId, option!, new TurnBeginsContext(pending.PlayerId, pending.TurnNumber, Player(pending.PlayerId).TurnsTaken));
        _state.PendingChoice = null;
        pending.ChoiceKey = null; pending.SourceCardId = null; pending.Options = [];
        bool? repeat = null;
        foreach (var resolve in EngineHooks.TurnBeginChoices.Items)
            if (resolve(this, answer) is { } handled) { repeat = handled; break; }
        if (repeat is null) throw new InvalidOperationException($"No resolver is registered for the turn-begin choice '{answer.Key}'.");
        if (_state.WinnerId is not null) { _state.PendingTurnBegins = null; return true; }
        // The resolver opened a further choice: the sequence stays where it is.
        if (_state.PendingChoice is not null) return true;
        ResumeTurnBegins(repeat.Value);
        return true;
    }

    // QueryActions: one option per answer, for the chooser only (the other player holds no priority and is told to wait).
    private IEnumerable<ActionOption> TurnBeginChoiceActions(int playerId)
    {
        if (_state.PendingTurnBegins is not { ChoiceKey: not null } pending || pending.ChooserId != playerId) yield break;
        foreach (var option in pending.Options)
            yield return new(TurnBoundary.ChooseCommand, option.Label, true, null,
                new GameCommand(TurnBoundary.ChooseCommand, playerId, _state.Version, TargetId: option.CardId, Choice: option.Id, TargetPlayerId: option.PlayerId), option.CardId ?? pending.SourceCardId);
    }

    // ---- registration ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTurnBoundaryHooks()
    {
        EngineHooks.PendingChoice.Register("P4HandleTurnBeginChoice", 16100, static (engine, type, command) => engine.HandleTurnBeginChoice(type, command));
        EngineHooks.DecisionState.Register(nameof(MatchState.PendingTurnBegins), 16100, static state => state.PendingTurnBegins is not null);
        EngineHooks.ValidateEarly.Register("P4ValidatePlayerGrant", 16100, static (engine, context) => engine.ValidatePlayerGrant(context.id, context.ability, context.player));
    }
}
