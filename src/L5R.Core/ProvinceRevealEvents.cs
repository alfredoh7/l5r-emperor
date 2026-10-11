using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P5 (issue #711, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): Events that stay face up in their province.
//
//   Today an Event is removed from its province and resolved the moment it is revealed. A few Events (A Dangerous Game, Harbingers of War, Revelations, ...) print "After you reveal cards in your Provinces at
//   the start of your turn, if this Event is face-up in one of them, ...": the Event stays where it is and fires again every one of its owner's Events Phases while it is still there.
//
//   FLAG       ProvinceRevealEvents.Register(effectId, resolve, answer) marks an Event BY ITS EFFECT ID (no CardDefinition edit). ResolveEvent returns false for a flagged Event without moving it (before the
//              P3 window and every registered resolver), so a reveal, an already-face-up Event and the Otosan Uchi path all leave it in its province.
//   CHANNEL    EngineHooks.AfterProvincesRevealed: Func<MatchEngine, AfterRevealContext, bool>, called as the reveal loop ends (before the Straighten holds and the ongoing-Event pass) for the active player, with
//              the flagged Events still face up in their provinces that have not fired this turn. True means a decision opened (the Events Phase is paused). It is called AGAIN with the Events still owed once
//              a decision is answered, so a registrant must be idempotent: the built-in registrant (priority 10000) stamps each Event (ProvinceRevealResolved) before it resolves it, once per turn.
//   RESOLVE    resolve(engine, event) does the Event's text and returns null when it needed no decision, or a RevealEventAsk (a yes/no, or a card among candidates, optionally "none"), asked of the
//              Event's owner as an Event decision (PendingEventChoice kind "p5-reveal-event", Origin "reveal"). answer(engine, event, choice, targetId) applies it; the Events Phase then resumes exactly as it
//              does after a Celestial's entry decision.

/// <summary>One flagged Event still face up in its province.</summary>
internal sealed record RevealedEvent(CardInstance Card, ProvinceState Province);

/// <summary>The active player and the flagged Events still to fire this turn, in province order.</summary>
internal sealed record AfterRevealContext(PlayerState Player, IReadOnlyList<RevealedEvent> Events);

/// <summary>A decision a province-reveal Event asks its owner: Candidates empty means yes/no; otherwise one answer per candidate card and, when Optional, "none".</summary>
internal sealed record RevealEventAsk(string Prompt, IReadOnlyList<string> Candidates, bool Optional = true);

internal static class ProvinceRevealEvents
{
    internal sealed record Registration(Func<MatchEngine, RevealedEvent, RevealEventAsk?> Resolve, Action<MatchEngine, RevealedEvent, string?, string?> Answer);

    private static readonly Dictionary<string, Registration> All = new(StringComparer.Ordinal);

    /// <summary>Flags the Events with this effect id as staying face up in their province, and gives their text.</summary>
    internal static void Register(string effectId, Func<MatchEngine, RevealedEvent, RevealEventAsk?> resolve, Action<MatchEngine, RevealedEvent, string?, string?>? answer = null) =>
        All.Add(effectId, new(resolve, answer ?? NoAnswer));

    private static void NoAnswer(MatchEngine engine, RevealedEvent revealed, string? choice, string? target) { }

    internal static bool TryGet(string effectId, out Registration registration) => All.TryGetValue(effectId, out registration!);

    internal static bool AnyFlagged => All.Count > 0;

    internal static bool IsFlagged(string effectId) => All.ContainsKey(effectId);

    internal static void UnregisterForTest(string effectId) => All.Remove(effectId);
}

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, AfterRevealContext, bool>> AfterProvincesRevealed = new();
}

public sealed partial class MatchEngine
{
    internal const string P5RevealEventKind = "p5-reveal-event";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterProvinceRevealEventHooks() =>
        EngineHooks.AfterProvincesRevealed.Register("P5DispatchRevealEvents", 10000, static (engine, context) => engine.DispatchRevealEvents(context));

    /// <summary>True when this Event stays face up in its province (its effect id is flagged): it neither resolves at reveal nor leaves.</summary>
    private static bool StaysFaceUpInProvince(CardInstance card) => ProvinceRevealEvents.IsFlagged(Definition(card).Effect);

    /// <summary>ContinueEventsReveal's call, as the reveal loop ends. True when a decision opened.</summary>
    private bool RunAfterProvincesRevealed(PlayerState player)
    {
        if (!ProvinceRevealEvents.AnyFlagged) return false;
        if (Wave829WeddingOpen()) return true; // Cross-Clan Wedding's second effect waits for the other player; the Events Phase resumes when it is answered
        var owed = player.Provinces
            .Where(p => !p.Destroyed && p.CardId is not null)
            .Select(p => (Province: p, Card: Card(p.CardId!)))
            .Where(x => x.Card.Zone == Zone.Province && x.Card.FaceUp && x.Card.OwnerId == player.Id && Definition(x.Card).Type == CardType.Event && StaysFaceUpInProvince(x.Card)
                && _state.ProvinceRevealResolved?.GetValueOrDefault(x.Card.Id) != _state.TurnNumber)
            .Select(x => new RevealedEvent(x.Card, x.Province)).ToList();
        if (owed.Count == 0) return false;
        var context = new AfterRevealContext(player, owed);
        foreach (var hook in EngineHooks.AfterProvincesRevealed.Items)
            if (hook(this, context)) return true;
        return false;
    }

    private bool DispatchRevealEvents(AfterRevealContext context)
    {
        foreach (var revealed in context.Events)
        {
            var stamps = _state.ProvinceRevealResolved ??= new(StringComparer.Ordinal);
            if (stamps.GetValueOrDefault(revealed.Card.Id) == _state.TurnNumber || !ProvinceRevealEvents.TryGet(Definition(revealed.Card).Effect, out var registration)) continue;
            stamps[revealed.Card.Id] = _state.TurnNumber;
            Emit("provinceRevealEvent", $"{Definition(revealed.Card).Name} resolves: it is face up in province {revealed.Province.Index + 1}.", context.Player.Id, revealed.Card.Id);
            if (registration.Resolve(this, revealed) is not { } ask) continue;
            OpenEventChoice(new PendingEventChoice { Kind = P5RevealEventKind, EventCardId = revealed.Card.Id, PlayerId = context.Player.Id, Origin = "reveal", Candidates = [.. ask.Candidates], Step = ask.Optional ? 1 : 0, Prompt = ask.Prompt });
            return true;
        }
        return false;
    }

    // The answer to a province-reveal Event's decision (the default arm of ResolveEventChoiceCommand). True when this handled it.
    private bool ResolveP5EventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != P5RevealEventKind) return false;
        var card = Card(pending.EventCardId); var player = Player(pending.PlayerId);
        if (pending.Candidates.Count == 0) Require(command.Choice is "yes" or "no", "Choose yes or no.");
        else if (command.Choice == "none") Require(pending.Step == 1, "This choice is not optional.");
        else Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered cards.");
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        if (ProvinceRevealEvents.TryGet(Definition(card).Effect, out var registration) && player.Provinces.FirstOrDefault(p => p.CardId == card.Id) is { } province)
            registration.Answer(this, new RevealedEvent(card, province), command.Choice, command.TargetId);
        ContinueEventsReveal(Player(card.OwnerId));
        return true;
    }

    private IEnumerable<ActionOption> P5EventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != P5RevealEventKind) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        if (pending.Candidates.Count == 0)
        {
            yield return new("eventChoice", $"{name}: yes", true, null, Answer(choice: "yes"), pending.EventCardId);
            yield return new("eventChoice", $"{name}: no", true, null, Answer(choice: "no"), pending.EventCardId);
            yield break;
        }
        foreach (var id in pending.Candidates) yield return new("eventChoice", $"{name}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
        if (pending.Step == 1) yield return new("eventChoice", $"{name}: none", true, null, Answer(choice: "none"), pending.EventCardId);
    }

    internal bool ResolveRevealEventsForTest(int playerId) => RunAfterProvincesRevealed(Player(playerId));
}

public sealed partial class MatchState
{
    /// <summary>P5 (ProvinceRevealEvents.cs): the turn number a province-reveal Event last resolved on, by card id (once per turn). Omitted until one has.</summary>
    [JsonPropertyOrder(2800)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? ProvinceRevealResolved { get; set; }
}
