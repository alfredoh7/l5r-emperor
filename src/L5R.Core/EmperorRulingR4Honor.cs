namespace L5R.Core;

/// <summary>The Reactions played "before a player gains or loses Honor": Mendon's R4 ruling (issue #492) is that they may be played on ANY Honor gain or loss from an action's effects (PendingHonorChange).</summary>
public static class HonorChangeReactions
{
    /// <summary>The trigger of a Reaction played in the window before an Honor change.</summary>
    public const string Trigger = "before-honor-change";
    /// <summary>Rhetoric, Solid Defense (its two halves) and Shiro Chugo -- the Reactions whose trigger this is.</summary>
    public static readonly string[] Ids = ["rhetoric-redirect", "solid-defense-negate", "solid-defense-negate-favor", "chugo-honor-reduce"];
    /// <summary>Unimpeachable Name ("Before the resolution of another player's action: Reduce each Honor loss from its effects by 3") keeps its own out-of-play trigger and its own window, and may ALSO be
    /// played in this window, on any loss of its player's: the same "before an Honor loss" moment.</summary>
    public const string UnimpeachableName = "unimpeachable-name-reaction";
}

// ---- Mendon's R4 ruling: "Rhetoric may be played on any honor gain or loss, as may Solid Defense" ------------------------------------------------------------------------------------------
// Honor changes are intercepted where they are applied (GainHonor). When one is about to be applied by an action's effects (a resolving ability, the Attack or duel it started; never Honor paid as a
// COST, _payingActionCost) and a player holds a Reaction for it (HonorChangeReactions), the command being executed is set aside (nothing it did is kept, the way Ten Thousand Temples' decision sets
// one aside), a window opens on the state the command started from and both players may play Reactions in it, alternating as in every reaction window; when both have passed the command is run again
// (MatchEngine.Execute's loop) with what those Reactions did in force (the marks on the action: RemC.RedirectActionKey, RemC.NegateLossesActionKey, MatchState.HonorReducedActionKey) and this change
// is not asked about again. An action with several Honor changes gets a window for each. No label is read and no list of "actions that change Honor" is kept: the engine asks when it happens.
public sealed partial class MatchEngine
{
    private sealed class HonorChangeWindowNeeded(int playerId, int amount, string reason, string actionKey, int actionPlayerId, int occurrence) : Exception("An Honor change awaits Reactions.")
    {
        public int PlayerId { get; } = playerId; public int Amount { get; } = amount; public string Reason { get; } = reason;
        public string ActionKey { get; } = actionKey; public int ActionPlayerId { get; } = actionPlayerId; public int Occurrence { get; } = occurrence;
    }

    // Per Execute (and per replay): how many eligible Honor changes this run has passed, how many of the command's earliest ones already had their window (they are not asked again), the state a further
    // window would be opened on, and the command to run again once a window has closed.
    private int _honorChangeOccurrence, _honorWindowsDone;
    private bool _resolvingFocusEffect; // a duel's Focus Effect is being applied: not an action (Dueling.cs, FocusEffectChoice.cs)
    private MatchState? _honorReplayBase;
    private GameCommand? _honorReplayCommand;

    // An action that stopped for a decision (an Either/Or, another player's choice or payment, a search) and is finished by the answer: the answering command is not the action, but the Honor it then
    // changes is still "from the action's effects" -- the action is named here for the length of the answer (WithResumedAction). Proclaim, a Reaction that is not an ability, is named while it resolves.
    private (string Key, int Actor)? _resumedAction, _proclaimAction;

    private void WithResumedAction(Action body)
    {
        var before = _resumedAction;
        _resumedAction = PausedActionFacts() ?? before;
        try { body(); }
        finally { _resumedAction = before; }
    }

    private (string Key, int Actor)? PausedActionFacts() => _state.PendingChoice?.Type switch
    {
        // The action is the source card's owner's (Tanimura's controller takes the Reaction whose Either/Or the other player answers); ActingPlayerId names who decides it here.
        "eitherOr" when _state.PendingEitherOr is { } either => ($"{either.ActingPlayerId}|{either.SpecId}|{either.SourceCardId}", _state.Cards.TryGetValue(either.SourceCardId, out var offered) ? offered.OwnerId : either.ActingPlayerId),
        "deciderChoice" when _state.PendingDeciderChoice is { } decider => (ActionKey(decider.Paused.Command), decider.Paused.Command.PlayerId),
        "opponentPayment" when _state.PendingOpponentPayment is { } payment => (ActionKey(payment.Paused.Command), payment.Paused.Command.PlayerId),
        "effectChoice" when _state.PendingEffectChoice is { } effect => (ActionKey(effect.Command), effect.Command.PlayerId),
        { } type when IsSearchChoice(type) && _state.PendingSearch?.Command is { } searching => (ActionKey(searching), searching.PlayerId),
        _ => null
    };

    private void ResetHonorChangeReplay() { _honorChangeOccurrence = 0; _honorWindowsDone = 0; _honorReplayBase = null; _honorReplayCommand = null; }

    // The action whose effects are being applied, and who took it (an ability's resolution, the Attack under way, or the duel a Battle action issued).
    private bool CurrentActionFacts(out string key, out int actorId)
    {
        key = ""; actorId = -1;
        if (_resolvingFocusEffect) return false; // Mendon's R4 ruling: "a Focus Effect is not an action" -- an Honor change it makes is not from an action's effects.
        var command = _runningCommand ?? _attackCommandInResolution ?? _state.PendingAttack?.Command;
        if (command?.AbilityId is not null) { key = ActionKey(command); actorId = command.PlayerId; return true; }
        if (_resumedAction is { } resumed) { key = resumed.Key; actorId = resumed.Actor; return true; }
        if (_proclaimAction is { } proclaiming) { key = proclaiming.Key; actorId = proclaiming.Actor; return true; }
        if (_state.PendingDuel is { } duel) { key = $"{duel.ChallengerPlayerId}|{duel.AbilityId}|{duel.SourceCardId}"; actorId = duel.ChallengerPlayerId; return true; }
        key = ""; actorId = -1;
        return false;
    }

    // GainHonor's first step. Nothing happens unless a Reaction could be played on this change now.
    private void OfferHonorChangeWindow(int playerId, int amount, string reason)
    {
        _p3LastHonorChange = null;
        if (_payingActionCost || _state.PendingHonorChange is not null || _state.PendingChoice is { Type: "honorChangeReaction" }) return;
        if (!CurrentActionFacts(out var key, out var actorId) && !P3NonActionHonorChange(playerId, amount, out key, out actorId)) return; // Destructive Priorities (issue #503): a gain no action made
        var occurrence = _honorChangeOccurrence++;
        _p3LastHonorChange = (key, occurrence);
        if (occurrence < _honorWindowsDone) return;
        var change = new PendingHonorChange { ActionKey = key, ActionPlayerId = actorId, Occurrence = occurrence, PlayerId = playerId, Amount = amount, Reason = reason };
        if (!HonorChangeReactionAvailable(0, change) && !HonorChangeReactionAvailable(1, change)) return;
        throw new HonorChangeWindowNeeded(playerId, amount, reason, key, actorId, occurrence);
    }

    // The Reactions of one player's that could be played on this change: in play or a Strategy in a hand, with the trigger, and each one's own condition.
    private IEnumerable<(CardInstance Card, string AbilityId)> HonorChangeReactionsOf(int reactorId, PendingHonorChange change) =>
        _state.Cards.Values.Where(c => c.OwnerId == reactorId && (InPlay(c) || c.Zone == Zone.Hand && Definition(c).Type == CardType.Strategy))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .SelectMany(c => AbilityIdsOf(c).Select(id => (Card: c, AbilityId: id)))
            .Where(x => AbilityCatalog.All.TryGetValue(x.AbilityId, out var a) && a.Timing == "Reaction" && (a.Trigger == HonorChangeReactions.Trigger || x.AbilityId == HonorChangeReactions.UnimpeachableName)
                && HonorChangeReactionAvailable(x.Card, x.AbilityId, change))
            .ToArray();

    private bool HonorChangeReactionAvailable(int reactorId, PendingHonorChange change) => HonorChangeReactionsOf(reactorId, change).Any();

    private bool HonorChangeReactionAvailable(CardInstance card, string abilityId, PendingHonorChange change) =>
        !BowedBarsAbilities(card) && !ActionLocked(card) && !BarredByCherryBlossom(abilityId) && !P3ReactionLocked(card.OwnerId, abilityId) /* issue #503 */ && Unspent(card, abilityId) && CanPayReaction(card, abilityId)
        && HonorChangeReactionApplies(card, abilityId, change);

    // Each Reaction's own printed condition (its cost's affordability is asked by the caller).
    private bool HonorChangeReactionApplies(CardInstance card, string abilityId, PendingHonorChange change)
    {
        var reactor = card.OwnerId;
        if (P3HonorChangeApplies(card, abilityId, change) is { } p3) return p3; // Destructive Priorities (issue #503)
        switch (abilityId)
        {
            // "Before a player gains or loses Honor from an action's effects, bow your performing Courtier": any change, and an unbowed Courtier to bow.
            case "rhetoric-redirect":
            // Shiro Chugo: "Before an action's resolution, choose your performing unbowed Courtier: Reduce any Honor gain or loss from the action's effects by 2."
            case "chugo-honor-reduce":
                return BeforeActionReactionPerformers(reactor, AbilityCatalog.All[abilityId]).Any();
            // Solid Defense: "Before the resolution of another player's action ... Negate any Honor losses from the action's effects": another player's action, and a way to pay (a Personality of
            // 5 or higher Force, or the Imperial Favor).
            case "solid-defense-negate":
                return change.ActionPlayerId != reactor && ControlsPersonalityWithForce(reactor, 5);
            case "solid-defense-negate-favor":
                return change.ActionPlayerId != reactor && !ControlsPersonalityWithForce(reactor, 5) && _state.FavorPlayerId == reactor;
            // Unimpeachable Name: another player's action, an Honor LOSS of the reactor's own to reduce (a Reaction from a hand, so only a Strategy in it).
            case HonorChangeReactions.UnimpeachableName:
                return card.Zone == Zone.Hand && change.ActionPlayerId != reactor && change.PlayerId == reactor && change.Amount < 0;
            default:
                return false;
        }
    }

    // Opens the window on `_state` (the state the abandoned command started from): who has the first opportunity is decided as in every reaction window. Returns the event count to report from.
    private int OpenHonorChangeWindow(HonorChangeWindowNeeded need, GameCommand abandoned)
    {
        var change = new PendingHonorChange
        {
            Command = abandoned, ActionKey = need.ActionKey, ActionPlayerId = need.ActionPlayerId, Occurrence = need.Occurrence, PlayerId = need.PlayerId, Amount = need.Amount, Reason = need.Reason,
            WindowsDone = need.Occurrence + 1, TempleDecisions = _honorLossDecisions.ToList(),
            PriorChoice = _state.PendingChoice, PriorPriority = _state.PriorityPlayerId, PriorPasses = _state.ConsecutivePasses,
        };
        _state.PendingHonorChange = change;
        var first = FirstReactionOpportunity(p => HonorChangeReactionAvailable(p, change));
        var events = _state.Events.Count;
        var kind = need.Amount > 0 ? "gain" : "lose";
        OpenAlternatingReactionWindow(new("honorChangeReaction", first, null, $"Before {Player(need.PlayerId).Name} would {kind} {Math.Abs(need.Amount)} Honor ({need.Reason}): either player may play a Reaction, or pass."),
            first, $"{Player(need.PlayerId).Name} would {kind} {Math.Abs(need.Amount)} Honor from {need.Reason}: reaction window opened.");
        return events;
    }

    // ---- Mendon's R4 ruling: Bad Kharma and Footsteps of Madness "may be played at any time on top of other modifiers to cancel an effect" ------------------------------------------------------------
    // Their window (the out-of-play Reaction window, opened for the opponent of the acting player) used to open only when nothing else had been opened for the action: an action with a before-action
    // window (a Reaction of anyone's in play, Rhetoric's...) or an additional-action window skipped it. It now FOLLOWS them: once the other Reactions and modifiers have been played, the hand
    // Reactions may still be played on top and cancel the action's effects.

    // Opens the out-of-play window for the held action if its reactor could play one now, carrying what the windows before it set. False when there is nothing to offer.
    private bool ChainOutOfPlayReactionWindow(GameCommand pending, bool consumedGrant = false)
    {
        var negations = _state.NegatedEffects.ToList();
        if (!ResolveOrDeferUnimpeachableName(pending, pending.AbilityId)) return false;
        _state.PendingOutOfPlayNegations = negations; _state.PendingOutOfPlayConsumedGrant = consumedGrant;
        return true;
    }

    // The deferred action once its before-action window has closed: the out-of-play window still to come, unless the action was negated (nothing left to react to), then its resolution.
    private void ResolveDeferredActionAfterItsWindows(GameCommand pending)
    {
        if (!_state.DeferredActionNegated && ChainOutOfPlayReactionWindow(pending)) return;
        ResolveDeferredActionInItsWindow(pending);
    }

    // A command inside the window: a Reaction (played like any other, it suspends the window while it resolves and gives it back), or a pass; two passes in succession close it, and the command that
    // was set aside is run again from the state the window leaves.
    private void ResolveHonorChangeWindowCommand(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction to the Honor change, or pass.");
        if (command.Type == "useAbility") { UseAbility(command); return; }
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        if (++_state.ConsecutivePasses < 2) { _state.PriorityPlayerId = 1 - _state.PriorityPlayerId; return; }
        var window = _state.PendingHonorChange!;
        _state.PendingHonorChange = null;
        Emit("reactionClosed", "Both players passed; the reaction window closed.");
        _state.PendingChoice = window.PriorChoice; _state.PriorityPlayerId = window.PriorPriority; _state.ConsecutivePasses = window.PriorPasses;
        _honorWindowsDone = window.WindowsDone; _honorChangeOccurrence = 0;
        _honorLossDecisions = window.TempleDecisions.ToList(); _honorLossOccurrence = 0;
        _honorReplayBase = Copy(_state);
        _honorReplayCommand = window.Command;
    }

    // The window is judged live: what is on offer to `playerId` right now (QueryActions lists these, validated as quoted).
    // (Unimpeachable Name's own option, with its optional rehonor target, is listed by the ability listing: QueryActions.)
    private IEnumerable<(CardInstance Card, string AbilityId)> HonorChangeWindowOffers(int playerId) =>
        _state.PendingHonorChange is { } change ? HonorChangeReactionsOf(playerId, change) : [];
}
