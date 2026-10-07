using System.Text.Json;
using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P8 (issue #712, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): the action-effect watch.
//
// An Interrupt "to what the held action does" (Moto Taigo - exp, Reverse Fate, Verdant Jungle) could only look at the action's TARGETS before it resolved: the before-action window predicted movers from the
// ability's MoveSpec and a curated list of destroying ids, and could not see a discard, a destination or a Personality the action created. The delay capture (EmperorRulingR2.cs: ArmDelayForHeldAction) is the
// working model of "do something to what the action does"; this generalises it from "delay everything" to "hear what the action actually did".
//
//   ARM        A Reaction played in the before-action window calls ArmActionEffectWatch(kind, callbackId, owner, source): (the held action, a kind, a callback id, an argument) is stored in
//              MatchState.ActionEffectWatches. It is armed against the HELD action only (a Reaction's own effects are another action's: Coils rule 4, an Interrupt refers only to the interrupted action).
//   RECORD     RecordActionEffect now also records DiscardedKind (a card discarded by the action's effects, with its Focus Value: the top-of-deck discard helper), a destination on a moved card
//              (ToBattlefield and the Side of the army it joined) and the created kind P6's Lying in Ambush already added. A watch hears a fact the moment it is recorded.
//   FIRE       Immediate watch: fires once, for the FIRST matching fact, and is spent. AtEnd watch: fires once with EVERY matching fact when the action is truly done (FinishActionEffectTracking). A watch
//              whose action finishes without a matching fact (or is negated whole: ResolveNegatedAction) is dropped. The callback is a hook (EngineHooks.ActionEffectWatchFired, keyed by callback id): it
//              either does its work at once (ActionWatchResponse.Done) or asks its owner a card question (ActionWatchResponse.Ask), queued as a PendingEventChoice that opens at the next safe point.
//   GATE       A "move-gate" watch is consulted before a unit moves to a battlefield (MovementToBattlefieldNegated): EngineHooks.ActionMoveGates answers whether to negate it (Verdant Jungle). The
//              promises "this movement cannot be negated" (MovementCannotBeNegated, Winter Sled, P12's immunity) are honoured.
//   FOCUS      FocusValueOf(card) is the one reader of "Focus Value" outside a duel: the printed value plus any "Focus" modifier on the card (Reverse Fate: "+1 or -1 FV to the first discarded card"). A duel
//              reads the printed value, so the modifier never applies inside one, as the card says.
//
// Nothing is recorded or stored unless a watch is armed: a match without one is byte-for-byte what it was (every new property is omitted from the saved state when unset).

/// <summary>The kinds of fact a watch can hear (AffectedCard.Kind) plus the gate that refuses a movement.</summary>
public static class WatchKinds
{
    public const string Moved = MatchEngine.MovedKind;
    public const string Discarded = MatchEngine.DiscardedKind;
    public const string CreatedPersonality = PerformersAmbushAbilities.CreatedKind;
    /// <summary>Not a fact: consulted before a unit moves to a battlefield (EngineHooks.ActionMoveGates).</summary>
    public const string MoveGate = "move-gate";
    public static bool IsKnown(string kind) => kind is Moved or Discarded or CreatedPersonality or MoveGate;
}

/// <summary>A watch armed against one held action: its kind, the callback that hears it, an argument for the callback, who decides, which card armed it, and whether it waits for the whole record.</summary>
public sealed record ActionEffectWatch(HeldActionKey Action, string Kind, string CallbackId, string? Arg, int OwnerPlayer, string? SourceId, bool AtEnd = false);

/// <summary>What a watch callback is told: the watch and the fact(s) it matched (one for an immediate watch, all of them for an AtEnd watch).</summary>
internal sealed record ActionWatchFact(MatchEngine Engine, ActionEffectWatch Watch, IReadOnlyList<AffectedCard> Facts)
{
    public AffectedCard First => Facts[0];
}

/// <summary>A callback's answer: <see cref="Done"/> (it did its work), or a card question for the watch's owner. <see cref="Apply"/> runs with the chosen card (null when an optional question is declined).</summary>
internal sealed record ActionWatchResponse(IReadOnlyList<string> Candidates, string Prompt, bool Optional, Action<MatchEngine, ActionWatchFact, string?> Apply)
{
    public static readonly ActionWatchResponse Done = new([], "", true, static (_, _, _) => { });
    public static ActionWatchResponse Ask(IReadOnlyList<string> candidates, string prompt, Action<MatchEngine, ActionWatchFact, string?> apply, bool optional = true) => new(candidates, prompt, optional, apply);
}

/// <summary>Consulted before a unit moves to a battlefield while a "move-gate" watch is armed against the running action.</summary>
internal sealed record ActionMoveGate(MatchEngine Engine, ActionEffectWatch Watch, CardInstance Mover, int Battlefield, bool Attacking);

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<ActionWatchFact, ActionWatchResponse?>> ActionEffectWatchFired = new();
    internal static readonly OrderedHooks<Func<ActionMoveGate, bool?>> ActionMoveGates = new();
}

/// <summary>Registration helpers: a feature names the callback id its Reaction arms and the hook answers only for it.</summary>
internal static class ActionWatchRegistry
{
    internal static void Fired(string hookId, int priority, string callbackId, Func<ActionWatchFact, ActionWatchResponse> callback) =>
        EngineHooks.ActionEffectWatchFired.Register(hookId, priority, fact => fact.Watch.CallbackId == callbackId ? callback(fact) : null);

    internal static void Gate(string hookId, int priority, string callbackId, Func<ActionMoveGate, bool> callback) =>
        EngineHooks.ActionMoveGates.Register(hookId, priority, gate => gate.Watch.CallbackId == callbackId ? callback(gate) : null);
}

public sealed partial class MatchState
{
    /// <summary>Watches armed against a held action (ActionEffectWatch.cs); null when none. Omitted from a saved state until used.</summary>
    [JsonPropertyOrder(2900)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ActionEffectWatch>? ActionEffectWatches { get; set; }
}

public sealed partial class MatchEngine
{
    private const string WatchChoiceKind = "p8-watch";

    // ---- arming ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Interrupt: watch what the held action does": arms a watch for the action the open before-action window (or Attack reaction) is about. Rejected when no action is held.</summary>
    internal void ArmActionEffectWatch(string kind, string callbackId, int ownerPlayer, string? sourceId, string? arg = null, bool atEnd = false)
    {
        Require(WatchKinds.IsKnown(kind), "Unknown action-effect watch kind.");
        var held = HeldAction().Command;
        var key = new HeldActionKey(held.PlayerId, held.CardId, held.AbilityId ?? "");
        (_state.ActionEffectWatches ??= []).Add(new ActionEffectWatch(key, kind, callbackId, arg, ownerPlayer, sourceId, atEnd && kind != WatchKinds.MoveGate));
        Emit("actionWatchArmed", sourceId is null ? "The held action is being watched." : $"{Definition(Card(sourceId)).Name}: the held action's effects are being watched.", ownerPlayer, sourceId);
    }

    // The watches that belong to the action being resolved right now: same ability and acting player as the delay store matches (ActiveDelay).
    private IEnumerable<ActionEffectWatch> RunningWatches()
    {
        if (_state.ActionEffectWatches is not { Count: > 0 } watches) return [];
        var running = _runningCommand ?? _attackCommandInResolution;
        return running?.AbilityId is null ? [] : watches.Where(w => w.Action.AbilityId == running.AbilityId && w.Action.PlayerId == running.PlayerId).ToArray();
    }

    private void SpendWatch(ActionEffectWatch watch)
    {
        _state.ActionEffectWatches?.Remove(watch);
        if (_state.ActionEffectWatches is { Count: 0 }) _state.ActionEffectWatches = null;
    }

    // ---- recording -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Attacking" or "defending": the side of the army a unit joined (the Attacker is the active player).
    private string MovedSide(string cardId) => Card(cardId).OwnerId == _state.ActivePlayerId ? "attacking" : "defending";

    /// <summary>The discard helpers' record: a card discarded by an action's effect, with its Focus Value. Nothing is recorded outside an action's own resolution.</summary>
    private void NoteDiscardedByAction(CardInstance card)
    {
        if ((_runningCommand ?? _attackCommandInResolution)?.AbilityId is null) return;
        RecordActionEffect(card.Id, DiscardedKind, focus: Definition(card).Focus);
    }

    // RecordActionEffect's tail: an immediate watch of this kind, armed against the running action, hears the first matching fact.
    private void NoteWatchedFact(AffectedCard fact)
    {
        if (_state.ActionEffectWatches is null) return;
        // The same card recorded twice for one kind (a move recorded by both the helper and its caller) is one fact.
        if (_state.CurrentActionAffected.Count(a => a.CardId == fact.CardId && a.Kind == fact.Kind) > 1) return;
        foreach (var watch in RunningWatches().Where(w => !w.AtEnd && w.Kind == fact.Kind)) FireWatch(watch, [fact]);
    }

    // FinishActionEffectTracking's head: the AtEnd watches hear the whole record, and every watch of this action ends with it.
    private void FireEndWatches(List<AffectedCard> affected, int playerId, string abilityId)
    {
        if (_state.ActionEffectWatches is not { Count: > 0 } watches) return;
        foreach (var watch in watches.Where(w => w.Action.AbilityId == abilityId && w.Action.PlayerId == playerId).ToArray())
        {
            var matching = watch.AtEnd ? affected.Where(a => a.Kind == watch.Kind).DistinctBy(a => a.CardId).ToList() : [];
            if (matching.Count > 0) FireWatch(watch, matching); else SpendWatch(watch);
        }
    }

    /// <summary>An action that is negated whole never did anything: its watches are dropped.</summary>
    private void DropWatchesOf(GameCommand command)
    {
        if (_state.ActionEffectWatches is null) return;
        _state.ActionEffectWatches.RemoveAll(w => w.Action.AbilityId == command.AbilityId && w.Action.PlayerId == command.PlayerId);
        if (_state.ActionEffectWatches.Count == 0) _state.ActionEffectWatches = null;
    }

    private void FireWatch(ActionEffectWatch watch, IReadOnlyList<AffectedCard> facts)
    {
        SpendWatch(watch);
        var context = new ActionWatchFact(this, watch, facts);
        if (WatchResponse(context) is not { } response || ReferenceEquals(response, ActionWatchResponse.Done) || response.Candidates.Count == 0) return;
        var eventCardId = watch.SourceId is not null && _state.Cards.ContainsKey(watch.SourceId) ? watch.SourceId : facts[0].CardId;
        List<string> data = [watch.CallbackId, watch.Kind, watch.Arg ?? "", watch.OwnerPlayer.ToString(), watch.SourceId ?? "", watch.AtEnd ? "1" : "0", watch.Action.PlayerId.ToString(), watch.Action.CardId ?? "", watch.Action.AbilityId];
        data.AddRange(facts.Select(f => JsonSerializer.Serialize(f)));
        _state.PendingEventChoices.Add(new PendingEventChoice { Kind = WatchChoiceKind, EventCardId = eventCardId, PlayerId = watch.OwnerPlayer, Candidates = [.. response.Candidates], Data = data, Prompt = response.Prompt });
    }

    private static ActionWatchResponse? WatchResponse(ActionWatchFact context)
    {
        foreach (var hook in EngineHooks.ActionEffectWatchFired.Items)
            if (hook(context) is { } response) return response;
        return null;
    }

    // ---- the gate --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>MovementToBattlefieldNegated's term: a "move-gate" watch armed against the running action refuses this movement. "Cannot be negated" promises are honoured.</summary>
    private bool ActionWatchNegatesMove(CardInstance card, int battlefield)
    {
        if (_state.ActionEffectWatches is null) return false;
        var running = _runningCommand ?? _attackCommandInResolution;
        if (running is null || MovementCannotBeNegated(running)) return false;
        foreach (var watch in RunningWatches().Where(w => w.Kind == WatchKinds.MoveGate))
        {
            var gate = new ActionMoveGate(this, watch, card, battlefield, card.OwnerId == _state.ActivePlayerId);
            foreach (var hook in EngineHooks.ActionMoveGates.Items)
            {
                if (hook(gate) is not bool negate) continue;
                if (!negate || MovementNegationBarred(card, watch.OwnerPlayer)) break;
                Emit("moveNegated", $"{Definition(card).Name}'s movement to {BattlefieldLabel(battlefield)} was negated.", card.OwnerId, card.Id);
                return true;
            }
        }
        return false;
    }

    // ---- the watch's question --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (ActionWatchFact Context, ActionWatchResponse? Response) WatchChoiceOf(MatchEngine engine, PendingEventChoice pending)
    {
        var d = pending.Data;
        var key = new HeldActionKey(int.Parse(d[6]), d[7].Length == 0 ? null : d[7], d[8]);
        var watch = new ActionEffectWatch(key, d[1], d[0], d[2].Length == 0 ? null : d[2], int.Parse(d[3]), d[4].Length == 0 ? null : d[4], d[5] == "1");
        var facts = d.Skip(9).Select(json => JsonSerializer.Deserialize<AffectedCard>(json)!).ToList();
        var context = new ActionWatchFact(engine, watch, facts);
        return (context, WatchResponse(context));
    }

    // The answer to a watch's question (the default arm of ResolveEventChoiceCommand). True when this handled it.
    private bool ResolveWatchEventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != WatchChoiceKind) return false;
        var (context, response) = WatchChoiceOf(this, pending);
        string? chosen = null;
        if (command.Choice == "none") Require(response is null || response.Optional, "This choice is not optional.");
        else { Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered cards."); chosen = command.TargetId; }
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        response?.Apply(this, context, chosen);
        ResumeAfterDeferredWindow(pending.ClosesTo);
        return true;
    }

    private IEnumerable<ActionOption> WatchEventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != WatchChoiceKind) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        foreach (var id in pending.Candidates) yield return new("eventChoice", $"{name}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
        if (WatchChoiceOf(this, pending).Response is not { Optional: false })
            yield return new("eventChoice", $"{name}: none", true, null, Answer(choice: "none"), pending.EventCardId);
    }

    // ---- Focus Value -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Focus Value a card has outside a duel: printed, plus a "Focus" modifier, never below 0. A duel reads the printed value (Dueling.cs, EmperorFollowupsDuel.cs), so a modifier never applies inside one.</summary>
    internal int FocusValueOf(CardInstance card) => Math.Max(0, Definition(card).Focus + StatModifier(card, "Focus"));

    /// <summary>"+1 / -1 Focus Value to this card until the end of the turn" (Reverse Fate): a turn-long Focus modifier; the discard pile keeps it until FinishTurn clears modifiers.</summary>
    internal void AddFocusModifier(CardInstance card, string sourceId, string effect, int amount) => card.Modifiers.Add(new(sourceId, effect, "Focus", amount));

    // ---- test seams (L5R.Core.Tests has InternalsVisibleTo) --------------------------------------------------------------------------------------------------------------------------------

    internal int TestWatchCount => _state.ActionEffectWatches?.Count ?? 0;
    internal int FocusValueForTest(string cardId) => FocusValueOf(Card(cardId));
    internal void NegateHeldActionForTest(string negatingCardId) => NegateDeferredAction(Card(negatingCardId));
}
