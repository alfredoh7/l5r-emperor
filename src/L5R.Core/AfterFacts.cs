using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P6 (issue #705, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): after-event records and windows.
//
// "After a Personality commits seppuku", "after you Proclaim", "after a Personality is assigned to attack", "after a card turns face up in a province", "after a Terrain is destroyed", "after a Celestial
// enters play", "after X leaves play", "after you announce an action" had no moment a card could watch: only an ACTION's resolution (LastResolvedAction, ActionEffectTracking.cs) opened the
// after-action window. A FACT is the same thing for an event that is not an action's effect:
//
//   RECORD        RecordFact(fact) at the one choke point of each event (CommitSeppuku / OrderSeppuku, FinishProclaim, OpenAssignedReactionWindow, DiscardTerrain, EnterCelestialPlay, and the post-command
//                 sweep for a card turning face up in a province or leaving play). It only queues the fact: nothing opens in the middle of an effect. A fact is queued only when somebody could care
//                 (a registered trait for its kind, or a card in the match whose Reaction names it), so a match that uses none of this is byte-for-byte what it was.
//   DRAIN         OpenQueuedFactWindow(closesTo), asked at the safe points every other queued window already uses (the top of OpenOverconfidentDrawWindow) and after every accepted command: the
//                 fact's registered TRAITS run first (mandatory, no window), then the existing afterActionResolvedReaction window opens when -- and only when -- some player holds a Reaction whose
//                 Trigger names the fact and whose condition holds. No legal Reaction means no window and no pass button (the #639 / #664 rule). The window is not re-opened from inside itself.
//   REACTIONS     A Reaction names a fact with Trigger = FactTriggers.For(FactKinds.X); its condition beyond "the right kind of fact, held by its owner" is a FactRegistry.Condition callback.
//
// A Celestial's entry is also a place a choice is made (Natsu-Togumara's "choose a Personality", Shrine to the Heavens' "choose a Celestial it does not discard"): CelestialEntryRegistry registers such
// a choice; EnterCelestialPlay queues it as an Event decision (PendingEventChoice kind "p6-celestial"), the Events Phase reveal pauses on it and resumes, and the discard of the other Celestials waits
// for the answers when one of them spares a card. "After you announce an action" with a performer is AnnounceRegistry: the announce window (P3, EmperorP3Priority.cs) asks any registered watcher.

/// <summary>The kinds of fact a Reaction or a trait can watch.</summary>
public static class FactKinds
{
    /// <summary>A Personality committed seppuku (CardId; PlayerId is the player who committed it).</summary>
    public const string Seppuku = "seppuku";
    /// <summary>A player Proclaimed a Personality (CardId is the Proclaimed one; PlayerId the Proclaimer).</summary>
    public const string Proclaimed = "proclaimed";
    /// <summary>A Personality was assigned to attack (CardId; Province the battlefield; PlayerId the attacker).</summary>
    public const string AssignedToAttack = "assigned-to-attack";
    /// <summary>A card in a province turned face up, or entered one face up (CardId; Province; PlayerId the province's owner).</summary>
    public const string TurnedFaceUp = "turned-face-up";
    /// <summary>A Terrain was destroyed (CardId; Province the battlefield; PlayerId the Terrain's controller).</summary>
    public const string TerrainDestroyed = "terrain-destroyed";
    /// <summary>A Celestial entered play (CardId; PlayerId its controller).</summary>
    public const string CelestialEntered = "celestial-entered";
    /// <summary>A card left play (CardId; PlayerId its controller; Arg the zone it went to).</summary>
    public const string LeftPlay = "left-play";
}

/// <summary>The Reaction triggers that name a fact, and the sentinel ability id a fact's record carries.</summary>
public static class FactTriggers
{
    public const string Prefix = "p6-fact:";
    public const string SentinelPrefix = "__p6-fact:";
    public static string For(string kind) => Prefix + kind;
    public static bool IsFact(string? trigger) => trigger is not null && trigger.StartsWith(Prefix, StringComparison.Ordinal);
    public static string KindOf(string trigger) => trigger[Prefix.Length..];
    public static string Sentinel(string kind) => SentinelPrefix + kind + "__";
    public static bool IsSentinel(string? abilityId) => abilityId is not null && abilityId.StartsWith(SentinelPrefix, StringComparison.Ordinal);
}

/// <summary>The trigger of a Reaction taken in the announce window of an action whose performer a registered filter accepts (Magistrate's Stipend).</summary>
public static class AnnounceTriggers
{
    public const string WithPerformer = "p6-announce-with-performer";
}

/// <summary>One thing that happened: its kind, who it concerns, which card, and (as the kind says) the battlefield or a free-form argument.</summary>
public sealed record FactRecord(string Kind, int PlayerId, string? CardId, string? SourceId = null, int? Province = null, string? Arg = null);

/// <summary>A card's own condition for its fact Reaction: the fact, the card holding the Reaction, who holds it, and which Reaction.</summary>
internal sealed record FactConditionContext(MatchEngine Engine, FactRecord Fact, CardInstance Source, int PlayerId, string AbilityId);

/// <summary>The announced action a registered watcher may react to.</summary>
internal sealed record AnnounceContext(MatchEngine Engine, GameCommand Command, AbilityDefinition Ability, CardInstance? Source, string? PerformerId, string WatcherAbilityId);

/// <summary>One decision a Celestial's entry asks its controller: a card among Candidates (or none when Optional), applied by Apply; SparesFromDiscard makes the chosen card exempt from the discard of
/// the other Celestials the entry causes. Recomputed from its hook whenever it is asked or answered, so it is never stored.</summary>
internal sealed record CelestialEntryChoice(string Prompt, IReadOnlyList<string> Candidates, Action<MatchEngine, CelestialEntryContext, string?> Apply, bool Optional = true, bool SparesFromDiscard = false);

internal sealed record CelestialEntryContext(PlayerState Player, CardInstance Celestial);

/// <summary>The Celestials whose discard of the player's other Celestials waits for the entry choices still queued; Spared are the ones a choice kept.</summary>
public sealed class CelestialDiscardDebt
{
    public string CelestialId { get; set; } = "";
    public int PlayerId { get; set; }
    public List<string> Spared { get; set; } = [];
}

/// <summary>A modifier an action put on a card that ends when the action does (Magistrate's Stipend's "+1C until the action ends").</summary>
public sealed record ActionScopedModifier(string CardId, string Source, string Effect, string? ActionKey = null);

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Action<MatchEngine, FactRecord>> FactTraits = new();
    internal static readonly OrderedHooks<Func<FactConditionContext, bool?>> FactConditions = new();
    internal static readonly OrderedHooks<Func<MatchEngine, CelestialEntryContext, CelestialEntryChoice?>> CelestialEntryChoices = new();
    internal static readonly OrderedHooks<Func<AnnounceContext, bool?>> AnnounceRegistrants = new();
}

/// <summary>The one-line registration API of the fact windows. Call from a feature's module initializer (priority at least 10000).</summary>
internal static class FactRegistry
{
    private static readonly Dictionary<string, List<Func<MatchEngine, bool>>> TraitKinds = [];

    /// <summary>A mandatory trait: <paramref name="trait"/> runs, with no window, once for each fact of <paramref name="kind"/>, before any Reaction to it. <paramref name="active"/> (default: always) says
    /// whether the trait counts as watching the kind right now: a fact nobody watches is never recorded. <paramref name="activeIn"/> says the same of one match (a trait of a card that is not in this match
    /// watches nothing there), so a match without the card records nothing and serialises as before.</summary>
    internal static void Trait(string hookId, int priority, string kind, Action<MatchEngine, FactRecord> trait, Func<bool>? active = null, Func<MatchEngine, bool>? activeIn = null)
    {
        lock (TraitKinds) { if (!TraitKinds.TryGetValue(kind, out var list)) TraitKinds[kind] = list = []; list.Add(engine => (active?.Invoke() ?? true) && (activeIn?.Invoke(engine) ?? true)); }
        EngineHooks.FactTraits.Register(hookId, priority, (engine, fact) => { if (fact.Kind == kind && (active?.Invoke() ?? true) && (activeIn?.Invoke(engine) ?? true)) trait(engine, fact); });
    }

    internal static bool TraitWatches(string kind, MatchEngine engine)
    {
        lock (TraitKinds) return TraitKinds.TryGetValue(kind, out var list) && list.Any(active => active(engine));
    }

    /// <summary>The condition of the Reaction <paramref name="abilityId"/> beyond "a fact of its kind, held by its owner": true when it may be used for this fact.</summary>
    internal static void Condition(string hookId, int priority, string abilityId, Func<FactConditionContext, bool> condition) =>
        EngineHooks.FactConditions.Register(hookId, priority, context => context.AbilityId == abilityId ? condition(context) : null);
}

/// <summary>Registers a decision made when a Celestial enters play.</summary>
internal static class CelestialEntryRegistry
{
    internal static void Register(string hookId, int priority, Func<MatchEngine, CelestialEntryContext, CelestialEntryChoice?> choice) =>
        EngineHooks.CelestialEntryChoices.Register(hookId, priority, choice);
}

/// <summary>Registers the announce window's watchers: the Reaction <paramref name="abilityId"/> (Trigger = AnnounceTriggers.WithPerformer) may be taken after its owner announces an action the filter accepts.</summary>
internal static class AnnounceRegistry
{
    internal static void Register(string hookId, int priority, string abilityId, Func<AnnounceContext, bool> filter) =>
        EngineHooks.AnnounceRegistrants.Register(hookId, priority, context => context.WatcherAbilityId == abilityId ? filter(context) : null);
}

public sealed partial class MatchState
{
    /// <summary>Facts waiting for the next safe point (AfterFacts.cs); null when none. Omitted from a saved state until used.</summary>
    [JsonPropertyOrder(2600)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FactRecord>? QueuedFacts { get; set; }

    /// <summary>Which cards sat in provinces, and whether face up, after the last command (the "turned face up" diff); null while nobody watches that fact.</summary>
    [JsonPropertyOrder(2601)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, bool>? ProvinceFaceSnapshot { get; set; }

    [JsonPropertyOrder(2602)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CelestialDiscardDebt>? CelestialDiscardDebts { get; set; }

    [JsonPropertyOrder(2603)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ActionScopedModifier>? ActionScopedModifiers { get; set; }
}

public sealed partial class MatchEngine
{
    private bool _drainingFacts;

    // ---- interest: nothing is recorded unless somebody could react -----------------------------------------------------------------------------------------------------------------------

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> FactKindsByDefinition = new(StringComparer.Ordinal);

    // The fact kinds the Reactions of one card definition name.
    private static string[] FactKindsOf(string definitionId, CardDefinition definition) =>
        FactKindsByDefinition.GetOrAdd(definitionId, _ => (definition.Abilities ?? []).Select(id => AbilityCatalog.All.TryGetValue(id, out var ability) ? ability.Trigger : null)
            .Where(FactTriggers.IsFact).Select(t => FactTriggers.KindOf(t!)).Distinct().ToArray());

    private bool FactHasInterest(string kind)
    {
        if (FactRegistry.TraitWatches(kind, this)) return true;
        var trigger = FactTriggers.For(kind);
        foreach (var card in _state.Cards.Values)
        {
            if (FactKindsOf(card.DefinitionId, Definition(card)).Contains(kind)) return true;
            if (card.GrantedAbilities.Any(g => AbilityCatalog.All.TryGetValue(g.AbilityId, out var granted) && granted.Trigger == trigger)) return true;
        }
        return false;
    }

    /// <summary>Queues a fact for the next safe point; does nothing when no trait and no Reaction names its kind.</summary>
    internal void RecordFact(string kind, int playerId, string? cardId, string? sourceId = null, int? province = null, string? arg = null)
    {
        if (!FactHasInterest(kind)) return;
        (_state.QueuedFacts ??= []).Add(new FactRecord(kind, playerId, cardId, sourceId, province, arg));
    }

    // ---- the drain --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void RunFactTraits(FactRecord fact)
    {
        foreach (var trait in EngineHooks.FactTraits.Items) trait(this, fact);
    }

    /// <summary>The queued facts: for each, its traits run, then the after-action window opens if anybody holds a Reaction to it. True when a window opened (the rest wait for it to close).</summary>
    private bool OpenQueuedFactWindow(int closesTo)
    {
        if (_drainingFacts || _state.QueuedFacts is not { Count: > 0 }) return false;
        _drainingFacts = true;
        try
        {
            while (_state.QueuedFacts is { Count: > 0 } queue)
            {
                var fact = queue[0]; queue.RemoveAt(0);
                if (queue.Count == 0) _state.QueuedFacts = null;
                RunFactTraits(fact);
                if (_state.WinnerId is not null || _state.PendingChoice is not null) return _state.PendingChoice is not null;
                var previous = _state.LastResolvedAction;
                _state.LastResolvedAction = new ResolvedActionFacts
                {
                    AbilityId = FactTriggers.Sentinel(fact.Kind),
                    PlayerId = fact.PlayerId,
                    SourceId = fact.SourceId ?? fact.CardId,
                    Affected = fact.CardId is null ? [] : [new AffectedCard(fact.CardId, fact.Kind)],
                    TargetIds = fact.CardId is null ? [] : [fact.CardId],
                    Fact = fact
                };
                if (OpenAfterActionResolvedWindow(closesTo)) return true;
                _state.LastResolvedAction = previous;
            }
            return false;
        }
        finally { _drainingFacts = false; }
    }

    /// <summary>The ordinary check of a fact Reaction (ValidateAbility's trigger check and the window's listing): the window of this fact is open, the Reaction names its kind, and its condition holds.
    /// Null when the Reaction is not a fact Reaction and the open window is not a fact's (the ordinary after-action path decides).</summary>
    private bool? FactApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        var inFactWindow = FactTriggers.IsSentinel(facts.AbilityId);
        var trigger = AbilityCatalog.All.TryGetValue(abilityId, out var ability) ? ability.Trigger : null;
        var isFactReaction = FactTriggers.IsFact(trigger);
        if (!inFactWindow && !isFactReaction) return null;
        if (!inFactWindow || !isFactReaction || facts.Fact is not { } fact || FactTriggers.For(fact.Kind) != trigger || source.OwnerId != playerId) return false;
        var context = new FactConditionContext(this, fact, source, playerId, abilityId);
        foreach (var condition in EngineHooks.FactConditions.Items)
            if (condition(context) is bool decided) return decided;
        return true;
    }

    /// <summary>After every accepted command: the facts only a before/after comparison can see (a card turned face up in a province), then the drain when nothing else is open.</summary>
    private void SweepFactsAfterCommand()
    {
        SweepProvinceFaces();
        if (_state.PendingChoice is not null || _state.PendingDuel is not null || _state.WinnerId is not null) return;
        var closesTo = _state.PriorityPlayerId;
        if (_state.PendingEventChoices.Count > 0 && _state.PendingEventChoices[0].Kind == P6CelestialKind) { OpenQueuedEventChoice(closesTo); return; }
        OpenQueuedFactWindow(closesTo);
    }

    // "Turned face up": a card now face up in a province that was face down there, or was not in a province at all, at the last sweep. Kept only while somebody watches the fact.
    private void SweepProvinceFaces()
    {
        if (!FactHasInterest(FactKinds.TurnedFaceUp)) { _state.ProvinceFaceSnapshot = null; return; }
        var previous = _state.ProvinceFaceSnapshot;
        var now = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var owner in _state.Players)
            foreach (var province in owner.Provinces.Where(p => !p.Destroyed))
                foreach (var id in new[] { province.CardId, province.CardId2 })
                {
                    if (id is null || !_state.Cards.TryGetValue(id, out var card) || card.Zone != Zone.Province) continue;
                    now[id] = card.FaceUp;
                    if (previous is not null && card.FaceUp && !(previous.TryGetValue(id, out var wasUp) && wasUp))
                        (_state.QueuedFacts ??= []).Add(new FactRecord(FactKinds.TurnedFaceUp, owner.Id, id, null, province.Index));
                }
        _state.ProvinceFaceSnapshot = now;
    }

    // ---- a card leaving play (from the zone-history sweep) -----------------------------------------------------------------------------------------------------------------------

    private void NoteLeftPlay(CardInstance card) => RecordFact(FactKinds.LeftPlay, card.OwnerId, card.Id, null, card.ProvinceIndex, card.Zone.ToString());

    /// <summary>What the action in progress has recorded (the cards it affected, the Honor it cost, the hands it filled, the Favor it moved) is dropped: used by an action that never reaches FinishActionEffectTracking.</summary>
    private void ClearActionAccumulators() { _state.CurrentActionAffected = []; _state.CurrentActionHonorLosers = []; _state.CurrentActionHandRecipients = []; _state.CurrentActionFavorTakers = []; }

    // ---- "until the action ends" modifiers ------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A modifier that ends when the action it belongs to does. Applied from a Reaction in an announce window it belongs to the ANNOUNCED action ("+1C until the action ends"), so the Reaction's own
    /// tail does not end it; applied anywhere else it belongs to the action now resolving. Cleared by FinishActionEffectTracking, by a negated action's completion, and at the turn's end.</summary>
    internal void ApplyUntilActionEnds(CardInstance card, string source, string effect, string stat, int amount)
    {
        ApplyModifier(card, source, effect, stat, amount);
        var key = _state.PendingChoice is { Type: "announceReaction" } && _state.PendingAnnouncedAction is { } announced ? ActionKeyPrefix(announced.PlayerId, announced.CardId, announced.AbilityId ?? announced.Type) : null;
        (_state.ActionScopedModifiers ??= []).Add(new ActionScopedModifier(card.Id, source, effect, key));
    }

    private static string ActionKeyPrefix(int playerId, string? cardId, string? abilityId) => playerId + "|" + cardId + "|" + abilityId + "|";

    // The action that just finished (its key prefix) ends the modifiers that belong to it and every one that belongs to no announced action; null ends them all (the turn's end).
    private void ClearActionScopedModifiers(string? finishedActionKey = null)
    {
        if (_state.ActionScopedModifiers is not { } scoped) return;
        var ending = scoped.Where(m => finishedActionKey is null || m.ActionKey is null || finishedActionKey.StartsWith(m.ActionKey, StringComparison.Ordinal)).ToArray();
        if (ending.Length == 0) return;
        _state.ActionScopedModifiers = scoped.Except(ending).ToList() is { Count: > 0 } rest ? rest : null;
        foreach (var modifier in ending)
            if (_state.Cards.TryGetValue(modifier.CardId, out var card)) card.Modifiers.RemoveAll(m => !m.Permanent && m.Source == modifier.Source && m.Effect == modifier.Effect);
    }

    // ---- the announce window's registered watchers ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The next card of the announcing player that holds a registered announce Reaction the announced action satisfies and that can be taken now; not yet asked for this announcement.</summary>
    private CardInstance? RegisteredAnnounceWatcher(GameCommand command)
    {
        if (command.Type != "useAbility" || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var announced) || announced.Timing == "Reaction") return null;
        var source = command.CardId is null ? null : Card(command.CardId);
        var performerId = command.PerformerId ?? ActionPerformerIdOrNull(announced, command, source);
        var record = AnnounceRecord(command);
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == command.PlayerId && InPlay(c) && !record.Asked.Contains(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal))
            foreach (var abilityId in AbilityIdsOf(card))
            {
                if (!AbilityCatalog.All.TryGetValue(abilityId, out var reaction) || reaction.Trigger != AnnounceTriggers.WithPerformer || ActionLocked(card) || !Unspent(card, abilityId)) continue;
                var context = new AnnounceContext(this, command, announced, source, performerId, abilityId);
                if (!EngineHooks.AnnounceRegistrants.Items.Any(hook => hook(context) == true) || !AnnounceReactionUsable(command, card)) continue;
                record.Asked.Add(card.Id);
                return card;
            }
        return null;
    }

    // Whether the card has a Reaction it can actually take in the announce window of this command: asked exactly as the window will list it, so a window with nothing to play never opens.
    private bool AnnounceReactionUsable(GameCommand command, CardInstance watcher)
    {
        var savedChoice = _state.PendingChoice; var savedAnnounced = _state.PendingAnnouncedAction;
        _state.PendingAnnouncedAction = command;
        _state.PendingChoice = new("announceReaction", watcher.OwnerId, watcher.Id, "After you announce this action: you may use this card's own reaction, or pass.");
        try { return QueryActions(watcher.OwnerId).Any(o => o.Type == "useAbility" && o.Enabled && o.CardId == watcher.Id); }
        finally { _state.PendingChoice = savedChoice; _state.PendingAnnouncedAction = savedAnnounced; }
    }

    /// <summary>ValidateAbility's check for AnnounceTriggers.WithPerformer: the announce window of this card is open for its player's own announcement and the registered filter accepts it.</summary>
    private bool AnnounceWithPerformerOpen(string abilityId, GameCommand command)
    {
        if (_state.PendingChoice is not { Type: "announceReaction" } pending || pending.CardId != command.CardId || command.PlayerId != pending.PlayerId
            || _state.PendingAnnouncedAction is not { } announced || announced.PlayerId != command.PlayerId || announced.AbilityId is null || !AbilityCatalog.All.TryGetValue(announced.AbilityId, out var ability)) return false;
        var source = announced.CardId is null ? null : Card(announced.CardId);
        var context = new AnnounceContext(this, announced, ability, source, announced.PerformerId ?? ActionPerformerIdOrNull(ability, announced, source), abilityId);
        return EngineHooks.AnnounceRegistrants.Items.Any(hook => hook(context) == true);
    }

    // ---- a Celestial's entry choices -------------------------------------------------------------------------------------------------------------------------------------------------------

    internal const string P6CelestialKind = "p6-celestial";

    // The choices this Celestial's entry asks: one per registered hook that has something to offer.
    private List<(string HookName, CelestialEntryChoice Choice)> CelestialEntryAsks(PlayerState player, CardInstance celestial)
    {
        var asks = new List<(string, CelestialEntryChoice)>();
        var context = new CelestialEntryContext(player, celestial);
        var names = EngineHooks.CelestialEntryChoices.Names;
        for (var i = 0; i < EngineHooks.CelestialEntryChoices.Items.Length; i++)
            if (EngineHooks.CelestialEntryChoices.Items[i](this, context) is { } choice && choice.Candidates.Count > 0) asks.Add((names[i], choice));
        return asks;
    }

    private CelestialEntryChoice? CelestialAskNamed(string hookName, PlayerState player, CardInstance celestial)
    {
        var index = EngineHooks.CelestialEntryChoices.Names.ToList().IndexOf(hookName);
        return index < 0 ? null : EngineHooks.CelestialEntryChoices.Items[index](this, new CelestialEntryContext(player, celestial));
    }

    /// <summary>EnterCelestialPlay's tail: queues one decision per ask (and the discard of the other Celestials waits for them when one spares a card). True when the Events Phase reveal must pause on the first.</summary>
    private bool QueueCelestialEntryAsks(PlayerState player, CardInstance celestial, List<(string HookName, CelestialEntryChoice Choice)> asks, bool pauseReveal)
    {
        foreach (var (hookName, choice) in asks)
            _state.PendingEventChoices.Add(new PendingEventChoice { Kind = P6CelestialKind, EventCardId = celestial.Id, PlayerId = player.Id, Candidates = [.. choice.Candidates], Data = [hookName], Prompt = choice.Prompt });
        if (!pauseReveal) return false;
        var first = _state.PendingEventChoices.First(c => c.Kind == P6CelestialKind && c.EventCardId == celestial.Id);
        _state.PendingEventChoices.Remove(first);
        first.Origin = "reveal";
        OpenEventChoice(first);
        return true;
    }

    // The answer to a celestial entry decision (the default arm of ResolveEventChoiceCommand). True when this handled it.
    private bool ResolveP6EventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != P6CelestialKind) return false;
        var player = Player(pending.PlayerId); var celestial = Card(pending.EventCardId);
        var choice = CelestialAskNamed(pending.Data[0], player, celestial);
        string? chosen = null;
        if (command.Choice == "none") Require(choice is null || choice.Optional, "This choice is not optional.");
        else { Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered cards."); chosen = command.TargetId; }
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        if (choice is not null && InPlay(celestial))
        {
            choice.Apply(this, new CelestialEntryContext(player, celestial), chosen);
            if (chosen is not null && choice.SparesFromDiscard && _state.CelestialDiscardDebts?.FirstOrDefault(d => d.CelestialId == celestial.Id) is { } debt) debt.Spared.Add(chosen);
        }
        // The next decision of the same entry, opened the way this one was; otherwise the entry is over.
        if (_state.PendingEventChoices.FirstOrDefault(c => c.Kind == P6CelestialKind && c.EventCardId == celestial.Id) is { } next)
        {
            _state.PendingEventChoices.Remove(next);
            next.Origin = pending.Origin; next.ClosesTo = pending.ClosesTo;
            OpenEventChoice(next);
            return true;
        }
        SettleCelestialDiscardDebt(celestial.Id);
        if (pending.Origin == "reveal") ContinueEventsReveal(Player(celestial.OwnerId)); else ResumeAfterDeferredWindow(pending.ClosesTo);
        return true;
    }

    private void SettleCelestialDiscardDebt(string celestialId)
    {
        if (_state.CelestialDiscardDebts?.FirstOrDefault(d => d.CelestialId == celestialId) is not { } debt) return;
        _state.CelestialDiscardDebts!.Remove(debt);
        if (_state.CelestialDiscardDebts.Count == 0) _state.CelestialDiscardDebts = null;
        if (!_state.Cards.TryGetValue(celestialId, out var celestial) || celestial.Zone != Zone.Celestial) return;
        DiscardOtherCelestials(Player(debt.PlayerId), celestial, debt.Spared);
    }

    private void DiscardOtherCelestials(PlayerState player, CardInstance entering, IReadOnlyCollection<string>? spared = null)
    {
        foreach (var other in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Celestial && c.Id != entering.Id && spared?.Contains(c.Id) != true).ToArray())
        {
            other.Zone = Zone.DynastyDiscard;
            Emit("celestialDiscarded", $"{Definition(other).Name} was discarded: {player.Name} may control only one Celestial in play at a time.", player.Id, other.Id);
        }
    }

    // ---- test seams (L5R.Core.Tests has InternalsVisibleTo) ---------------------------------------------------------------------------------------------------------------------------

    internal string[] TestPersonalities(int playerId) => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    internal string[] TestOtherCelestials(int playerId, string enteringId) => _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Celestial && c.Id != enteringId).Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    internal void SweepFactsForTest() { SweepZoneHistory(_state.TurnNumber); SweepFactsAfterCommand(); }
    internal void CelestialEntryForTest(int playerId, int provinceIndex, string celestialId, bool pauseReveal) => ResolveCelestialCore(Player(playerId), Player(playerId).Provinces[provinceIndex], Card(celestialId), pauseReveal);
    internal bool OpenQueuedChoicesForTest(int closesTo) => OpenQueuedEventChoice(closesTo);
    internal void EventsPhaseForTest(int playerId) => EventsPhase(Player(playerId));

    /// <summary>The decider's legal answers to a celestial entry decision: one per candidate, and "none" where the choice is optional.</summary>
    private IEnumerable<ActionOption> P6EventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != P6CelestialKind) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        foreach (var id in pending.Candidates) yield return new("eventChoice", $"{name}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
        if (CelestialAskNamed(pending.Data[0], Player(pending.PlayerId), Card(pending.EventCardId)) is not { Optional: false })
            yield return new("eventChoice", $"{name}: none", true, null, Answer(choice: "none"), pending.EventCardId);
    }
}
