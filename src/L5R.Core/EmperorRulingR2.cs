namespace L5R.Core;

// ======================================================================================================================================================================================
// THE SHARED DELAY MECHANISM (Emperor Edition ruling pass R2, issue #492, Mendon's ruling on "delay" cards).
//
// Some cards delay the effects of an action -- or of losing a duel -- until a later point of the game (Halls of the Forgotten, Spirit of the Berserker, the five "Focus Effect: you may choose
// (now) to delay the effects of losing this duel until after the next Dynasty Phase begins" Strategies, ...). Every such effect is RECORDED as it would have been applied, and APPLIED when its
// trigger point comes. Three pieces:
//
//   1. THE RECORDING LAYER (a "capture"). A card that delays effects arms a capture on one card for the running action (or, for a duel, for the duel's outcome): an entry in
//      MatchState.NegatedEffects of kind "delay:<until>|<kinds>|<abilityId>|<playerId>|<reason>" (so it travels with the held action through every pause and is dropped with the other
//      negations when the action has resolved), or an in-memory scope (_delayScopes) for the duel's outcome. While a capture is armed for a card, each card-mutating helper the engine already
//      routes an effect through asks Delay(...) FIRST and, if the effect is delayed, records it instead of applying it: StalwartNegatesBow (bow), DestroyUnits / DestroyAttachment (destroy),
//      Dishonor / Rehonor, Straighten, MoveHome (move home), TryMoveToBattlefield (move to a battlefield), ApplyModifier / AddPermanentModifier (a stat modifier), CreateToken, GrantKeyword.
//      The helper returns exactly what it returns when the effect is negated, so the calling ability sees "it did not happen (yet)" and its conditions ("if he moved ...") read correctly. An
//      action that "will not be negated or delayed" (Yoritomo Hiromi, Suitengu's Torch, Journey's End Keep) is never captured. Only effects ON THE CAPTURED CARD are recorded: a cost the
//      action's player pays, or an effect on another card, happens now. Every recorded effect is a DelayedEffect keyed by the affected card.
//
//   2. THE STORE (MatchState.DelayedEffects, persisted with the match, so it survives every phase): each DelayedEffect carries the trigger it waits for (DelayUntil), a sequence number (the
//      order in which effects were applied), the kind of effect and its details, and the battle/phase/turn it was recorded in.
//
//   3. THE APPLIERS (ApplyDelayedEffects), called at each trigger point:
//        * DelayUntil.EndOfBattle          -- FinishBattleReturnHomeAndTransition: the battle's resolution and its return home are over ("until after this battle ends").
//        * DelayUntil.EndOfPhase           -- SetPhase, as the phase (family) an effect was recorded in ends.
//        * DelayUntil.EndOfTurn            -- the top of EndTurn ("before this turn ends"; Spirit of the Berserker, In the Heart of Battle): the engine's point for "until the end of the turn", which comes
//                                             before the End Phase begins (the End Phase's own draw and discard are the turn's last steps and are not delayed effects' business).
//        * DelayUntil.NextDynastyPhase     -- SetPhase(Phase.Dynasty), as the next Dynasty Phase begins ("until after the next Dynasty Phase begins", the five duel Strategies).
//        * DelayUntil.EndPhaseBegins       -- SetPhase(Phase.End) ("until your End Phase begins").
//      Another trigger is one constant, one call from the place that point happens, and one filter. At a trigger point the due effects are applied in the ORDER THEY WERE APPLIED (the
//      sequence number; effects with the same delay never reorder, and several effects stack on one card or unit), so "dishonor then destroy" and "destroy then dishonor" differ exactly as they
//      would have: each effect is applied by the very helper that would have applied it (so a dishonorable Personality destroyed after being dishonored loses the Honor, and a destroyed one
//      cannot be dishonored afterwards -- it is out of play and the dishonoring is negated).
//
// THE CANNOT-APPLY RULE (Mendon): each effect is re-validated when its turn comes. One that cannot be applied any more is simply NEGATED (logged, dropped) -- a Personality cannot be "sent
// home" when he is already at home (the Halls of the Forgotten attacker who has since returned home), nor moved to another battlefield unless he stands at a battlefield now and the
// destination still is one. Destroy, dishonor and bow (and straighten, Force modifiers, tokens ...) may occur from any location, so they are applied wherever the card is -- as long as it is
// still in play. A card that left play meanwhile has nothing left to be affected. The helpers re-run their own negations at that time (Stalwart, Twin Forks City ... judge the effect when it is
// applied), and Hiromi-style "not negated" promises were judged when the effect was recorded.
//
// WHAT IS LEFT ALONE: effects that are not effects ON A CARD of an action (Honor, Gold, draws, the "before this battle resolves" delayed effects that the active player orders --
// BeforeResolution.cs --, Sentinel Spirit's and Eternal Victory Dojo's watches, Akodo Kobi's end-phase recruits) keep their own timing; they are the same idea but not a captured card effect.
// The delayed destruction of Spirit of the Berserker (DelayedDestruction.cs), In the Heart of Battle's and Deadly Orders' scheduled destructions are re-expressed through this store.
// ======================================================================================================================================================================================

/// <summary>The trigger points a delayed effect may wait for.</summary>
public static class DelayUntil
{
    public const string EndOfBattle = "battle-end";
    public const string EndOfPhase = "phase-end";
    public const string EndOfTurn = "turn-end";
    public const string NextDynastyPhase = "dynasty-phase-begins";
    public const string EndPhaseBegins = "end-phase-begins";
    /// <summary>P4 (TurnBoundary.cs): "until the game ends" -- never applied. A turn-begin point is <see cref="TurnBeginsPrefix"/> + owner + ":" + turn, built by the engine's DelayUntilTurn.</summary>
    public const string GameEnd = "game-end";
    public const string TurnBeginsPrefix = "turn-begins:";

    public static string Describe(string until) => until switch
    {
        EndOfBattle => "after this battle ends",
        EndOfPhase => "the end of the phase",
        EndOfTurn => "before this turn ends",
        NextDynastyPhase => "after the next Dynasty Phase begins",
        EndPhaseBegins => "after the End Phase begins",
        _ => TurnBoundary.DescribeDelayUntil(until) ?? until
    };
}

/// <summary>The kinds of effect the recording layer captures.</summary>
public static class DelayedKinds
{
    public const string Bow = "bow";
    public const string Straighten = "straighten";
    public const string Dishonor = "dishonor";
    public const string Rehonor = "rehonor";
    public const string Destroy = "destroy";
    public const string MoveHome = "move-home";
    public const string MoveToBattlefield = "move-battlefield";
    public const string Modifier = "modifier";
    public const string PermanentModifier = "permanent-modifier";
    public const string Token = "token";
    public const string Keyword = "keyword";
    public const string Ability = "ability";
    /// <summary>A card drawn by a delayed action (Kurohito's Perfection, issue #496): an effect on a PLAYER, not on a card. Its DelayedEffect has no card; PlayerId is who draws.</summary>
    public const string Draw = "draw";
}

/// <summary>One effect an action applied to a card that a card delayed (MatchState.DelayedEffects). <paramref name="Seq"/> is the order in which effects were applied.</summary>
public sealed class DelayedEffect
{
    public int Seq { get; set; }
    public string Kind { get; set; } = "";
    public string Until { get; set; } = DelayUntil.EndOfTurn;
    public string CardId { get; set; } = "";
    /// <summary>The player whose action applied it (null for an effect nobody's action applied: an instruction to destroy a card before the turn ends).</summary>
    public int? PlayerId { get; set; }
    public string? SourceId { get; set; }
    public string Reason { get; set; } = "";
    public string? Stat { get; set; }
    public int Amount { get; set; }
    /// <summary>Kind-specific: the destination battlefield (a province index) of a move, the keyword and its duration of a grant, "combat" for a destruction from a battle's resolution, "bow" of a move home.</summary>
    public string? Arg { get; set; }
    public string? Text { get; set; }
    public int Battle { get; set; }
    public int Turn { get; set; }
    public Phase RecordedPhase { get; set; }
}

/// <summary>The choice a player made at a "(now)" delay Focus Effect: the player whose Personalities' losing effects are delayed, until when, and by which card.</summary>
public sealed record DelayedLossChoice(int PlayerId, string Until, string SourceId, bool AnySource);

public sealed partial class MatchEngine
{
    private const string DelayEntryPrefix = "delay:";

    /// <summary>A capture armed for one card: what it delays (<see cref="Kinds"/>, null for every kind), until when, for which action (the ability and player) or duel outcome.</summary>
    private sealed record DelayScope(string CardId, string Until, string? Kinds, string? AbilityId, int? PlayerId, string Reason)
    {
        /// <summary>The player whose card set the delay (issue #667: Winter Sled), stamped on the entry that holds it; null: unknown.</summary>
        public int? ByPlayer { get; init; }
        public bool Covers(string kind) => Kinds is null || Kinds.Split(',').Contains(kind);
        public string Encode() => $"{DelayEntryPrefix}{Until}|{Kinds ?? "*"}|{AbilityId}|{PlayerId}|{Reason}";
        public static DelayScope? Decode(NegatedEffect entry)
        {
            if (!entry.Kind.StartsWith(DelayEntryPrefix, StringComparison.Ordinal)) return null;
            var parts = entry.Kind[DelayEntryPrefix.Length..].Split('|', 5);
            if (parts.Length < 5) return null;
            return new DelayScope(entry.CardId, parts[0], parts[1] == "*" ? null : parts[1], parts[2], int.TryParse(parts[3], out var player) ? player : null, parts[4]) { ByPlayer = entry.ByPlayer };
        }
    }

    // In-memory captures for a duel's outcome (never persisted: the outcome applies inside one command).
    private readonly List<DelayScope> _delayScopes = [];
    // True while a delayed effect is being applied: nothing is captured again.
    private bool _applyingDelayed;

    // ---- arming ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Delay [the action's] effects on him until ...": arms a capture on <paramref name="card"/> for the held action (the deferred action or Attack a Reaction has just been played against).
    /// <paramref name="kinds"/> narrows it to some kinds of effect ("destroy" for Spirit of the Berserker); null delays every kind.</summary>
    private void ArmDelayForHeldAction(CardInstance card, string until, string reason, string? kinds = null)
    {
        var held = HeldAction().Command;
        _state.NegatedEffects.Add(new(new DelayScope(card.Id, until, kinds, held.AbilityId, held.PlayerId, reason).Encode(), card.Id));
        Emit("effectsDelayed", $"{Definition(card).Name}: the action's {(kinds is null ? "effects" : kinds.Replace(",", " and "))} on him will be delayed until {DelayUntil.Describe(until)}.", card.OwnerId, card.Id);
    }

    /// <summary>Runs <paramref name="apply"/> with every kind of effect on <paramref name="cardIds"/> captured (a duel's outcome).</summary>
    private void WithDelayScope(IEnumerable<string> cardIds, string until, int? playerId, string reason, Action apply)
    {
        var added = cardIds.Select(id => new DelayScope(id, until, null, null, playerId, reason) { ByPlayer = playerId }).ToList();
        _delayScopes.AddRange(added);
        try { apply(); }
        finally { foreach (var scope in added) _delayScopes.Remove(scope); }
    }

    // ---- recording -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private DelayScope? ActiveDelay(CardInstance card, string kind)
    {
        if (_applyingDelayed) return null;
        for (var i = _delayScopes.Count - 1; i >= 0; i--)
            if (_delayScopes[i].CardId == card.Id && _delayScopes[i].Covers(kind)) return _delayScopes[i];
        if (_state.NegatedEffects.Count == 0) return null;
        var running = _runningCommand ?? _attackCommandInResolution;
        if (running?.AbilityId is null) return null;
        foreach (var entry in _state.NegatedEffects)
            if (entry.CardId == card.Id && DelayScope.Decode(entry) is { } scope && scope.AbilityId == running.AbilityId && scope.PlayerId == running.PlayerId && scope.Covers(kind)) return scope;
        return null;
    }

    // "Will not be negated or delayed": the same promise NegatesEffect honours (Hiromi's bowing, destruction and movement, The Shadow's Lair's movement, Suitengu's Torch's whole action) plus Journey's End
    // Keep's printed "this will not be negated or delayed" for its movement.
    private bool CannotBeDelayed(string kind) => !_notNegatedButDelayable && kind is DelayedKinds.Bow or DelayedKinds.Destroy or DelayedKinds.MoveHome or DelayedKinds.MoveToBattlefield
        ? HiromiUnnegatable() || kind is DelayedKinds.MoveHome or DelayedKinds.MoveToBattlefield && JourneysEndKeepAction(_runningCommand ?? _attackCommandInResolution)
        : CurrentActionUnnegatable();

    // Journey's End Keep's action: the Stronghold's own "journeys-end-move" (the Strategy that shares the ability id carries no such promise).
    private bool JourneysEndKeepAction(GameCommand? command) =>
        command is { AbilityId: "journeys-end-move", CardId: { } cardId } && _state.Cards.TryGetValue(cardId, out var source) && Definition(source).Type == CardType.Stronghold;

    /// <summary>The recording layer's one question, asked by every card-mutating helper before it applies an effect to <paramref name="card"/>: true when the effect was delayed (recorded instead
    /// of applied), in which case the helper does nothing now.</summary>
    private bool Delay(CardInstance card, string kind, int? playerId = null, string? stat = null, int amount = 0, string? arg = null, string? text = null, string? sourceId = null)
    {
        if (_applyingDelayed || _state.NegatedEffects.Count == 0 && _delayScopes.Count == 0) return false;
        if (ActiveDelay(card, kind) is not { } scope || CannotBeDelayed(kind)) return false;
        if (kind is DelayedKinds.MoveHome or DelayedKinds.MoveToBattlefield && MovementNegationBarred(card, scope.ByPlayer)) return false; // Winter Sled (issue #667): a delay is another card's negation too
        var actor = playerId ?? _runningCommand?.PlayerId ?? _attackCommandInResolution?.PlayerId ?? scope.PlayerId;
        Record(new DelayedEffect { Kind = kind, Until = scope.Until, CardId = card.Id, PlayerId = actor, SourceId = sourceId ?? _runningCommand?.CardId, Reason = scope.Reason, Stat = stat, Amount = amount, Arg = arg, Text = text });
        var name = Definition(card).Name;
        Emit(kind == DelayedKinds.Destroy ? "destructionDelayed" : "effectDelayed", kind == DelayedKinds.Destroy
            ? $"{name}'s destruction is delayed until {DelayUntil.Describe(scope.Until)}."
            : $"{name}: {DescribeDelayedEffect(kind, stat, amount, arg, text)} is delayed until {DelayUntil.Describe(scope.Until)}.", card.OwnerId, card.Id);
        return true;
    }

    private void Record(DelayedEffect effect)
    {
        effect.Seq = ++_state.DelayedEffectSequence;
        effect.Battle = _state.BattleNumber; effect.Turn = _state.TurnNumber; effect.RecordedPhase = _state.Phase;
        _state.DelayedEffects.Add(effect);
    }

    /// <summary>An effect nobody's action delayed but that is owed later on its own ("Destroy him before this turn ends"): recorded through the same store, so it is applied in the same order.</summary>
    private void ScheduleDelayedEffect(CardInstance card, string kind, string until, int? playerId, string reason, string? sourceId = null, string? stat = null, int amount = 0, string? arg = null)
    {
        // An instruction to destroy the same card at the same point twice is one instruction.
        if (kind == DelayedKinds.Destroy && _state.DelayedEffects.Any(e => e.Kind == kind && e.Until == until && e.CardId == card.Id)) return;
        Record(new DelayedEffect { Kind = kind, Until = until, CardId = card.Id, PlayerId = playerId, SourceId = sourceId, Reason = reason, Stat = stat, Amount = amount, Arg = arg });
    }

    private static string DescribeDelayedEffect(string kind, string? stat, int amount, string? arg, string? text) => kind switch
    {
        DelayedKinds.Bow => "bowing",
        DelayedKinds.Straighten => "straightening",
        DelayedKinds.Dishonor => "dishonoring",
        DelayedKinds.Rehonor => "rehonoring",
        DelayedKinds.Destroy => "destruction",
        DelayedKinds.MoveHome => "moving home",
        DelayedKinds.MoveToBattlefield => "moving to a battlefield",
        DelayedKinds.Token => $"{text ?? "a"} token",
        DelayedKinds.Keyword => $"gaining {arg?.Split('/')[0]}",
        DelayedKinds.Ability => "gaining an ability",
        DelayedKinds.Draw => "drawing a card",
        _ => $"{(amount >= 0 ? "+" : "")}{amount} {stat}"
    };

    // ---- applying --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Applies every delayed effect waiting for <paramref name="until"/> (and accepted by <paramref name="due"/>), in the order the effects were applied. An effect that cannot be applied any
    /// more is negated. Returns whether any was due.</summary>
    private bool ApplyDelayedEffects(string until, Func<DelayedEffect, bool>? due = null)
    {
        if (_state.DelayedEffects.Count == 0) return false;
        var list = _state.DelayedEffects.Where(e => e.Until == until && (due is null || due(e))).OrderBy(e => e.Seq).ToArray();
        if (list.Length == 0) return false;
        foreach (var effect in list) _state.DelayedEffects.Remove(effect);
        // What an action's own record (CurrentActionAffected ...) holds belongs to the action in progress, not to an effect owed from an earlier one.
        var affected = _state.CurrentActionAffected; var losers = _state.CurrentActionHonorLosers; var recipients = _state.CurrentActionHandRecipients; var takers = _state.CurrentActionFavorTakers;
        _state.CurrentActionAffected = []; _state.CurrentActionHonorLosers = []; _state.CurrentActionHandRecipients = []; _state.CurrentActionFavorTakers = [];
        var was = _applyingDelayed;
        _applyingDelayed = true;
        try
        {
            foreach (var effect in list)
            {
                if (_state.WinnerId is not null) break;
                ApplyDelayedEffect(effect);
            }
        }
        finally
        {
            _applyingDelayed = was;
            _state.CurrentActionAffected = affected; _state.CurrentActionHonorLosers = losers; _state.CurrentActionHandRecipients = recipients; _state.CurrentActionFavorTakers = takers;
        }
        return true;
    }

    private void NegateDelayedEffect(DelayedEffect effect, CardInstance card, string why) =>
        Emit("delayedEffectNegated", $"{Definition(card).Name}: the delayed {DescribeDelayedEffect(effect.Kind, effect.Stat, effect.Amount, effect.Arg, effect.Text)} ({effect.Reason}) can no longer be applied ({why}) and is negated.", card.OwnerId, card.Id);

    private void ApplyDelayedEffect(DelayedEffect effect)
    {
        // A delayed draw (Kurohito's Perfection, issue #496) is an effect on a player: nothing to re-validate but that the game is still on.
        if (effect.Kind == DelayedKinds.Draw && effect.PlayerId is int drawer)
        {
            Emit("delayedEffectApplied", $"{Player(drawer).Name} draws a card, as delayed ({effect.Reason}).", drawer);
            Draw(Player(drawer));
            return;
        }
        if (!_state.Cards.TryGetValue(effect.CardId, out var card)) return;
        if (!InPlay(card)) { NegateDelayedEffect(effect, card, "it left play"); return; }
        var name = Definition(card).Name;
        var actor = effect.PlayerId ?? card.OwnerId;
        switch (effect.Kind)
        {
            case DelayedKinds.Destroy:
                Emit("delayedDestruction", $"{name} is destroyed, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                if (Definition(card).Type == CardType.Personality) DestroyUnits([card], effect.Arg == "combat" || effect.PlayerId is null, effect.PlayerId);
                else DestroyAttachment(card);
                break;
            case DelayedKinds.Bow:
                Emit("delayedEffectApplied", $"{name} is bowed, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                if (!StalwartNegatesBow(card, actor)) card.Bowed = true;
                break;
            case DelayedKinds.Straighten:
                Emit("delayedEffectApplied", $"{name} is straightened, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                Straighten(card);
                break;
            case DelayedKinds.Dishonor:
                if (Definition(card).Type != CardType.Personality) { NegateDelayedEffect(effect, card, "it is not a Personality"); break; }
                Emit("delayedEffectApplied", $"{name} is dishonored, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                Dishonor(card, actor);
                break;
            case DelayedKinds.Rehonor:
                Emit("delayedEffectApplied", $"{name} is rehonored, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                Rehonor(card);
                break;
            // A location-bound effect: a Personality who is home cannot be sent home, and one who is not at a battlefield cannot be moved to another (Mendon's ruling).
            case DelayedKinds.MoveHome:
                if (card.Zone != Zone.Battlefield) { NegateDelayedEffect(effect, card, "he is already home"); break; }
                Emit("delayedEffectApplied", $"{name} is moved home, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                MoveHome(card, effect.Arg == "bow");
                break;
            case DelayedKinds.MoveToBattlefield:
                {
                    var destination = int.Parse(effect.Arg!);
                    if (card.Zone != Zone.Battlefield) { NegateDelayedEffect(effect, card, "he is not at a battlefield"); break; }
                    if (card.ProvinceIndex == destination) { NegateDelayedEffect(effect, card, "he is there already"); break; }
                    if (_state.CurrentBattleProvince != destination && !_state.UnresolvedBattlefields.Contains(destination)) { NegateDelayedEffect(effect, card, "that battlefield is no longer open"); break; }
                    Emit("delayedEffectApplied", $"{name} is moved to the battlefield, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                    TryMoveToBattlefield(card, destination);
                    break;
                }
            case DelayedKinds.Modifier:
                Emit("delayedEffectApplied", $"{name} gets {(effect.Amount >= 0 ? "+" : "")}{effect.Amount} {effect.Stat}, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                ApplyModifier(card, effect.SourceId ?? "delayed-effect", effect.Text ?? "delayed-effect", effect.Stat!, effect.Amount);
                break;
            case DelayedKinds.PermanentModifier:
                Emit("delayedEffectApplied", $"{name} permanently gets {(effect.Amount >= 0 ? "+" : "")}{effect.Amount} {effect.Stat}, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                AddPermanentModifier(card, effect.SourceId ?? "delayed-effect", effect.Text ?? "delayed-effect", effect.Stat!, effect.Amount);
                break;
            case DelayedKinds.Token:
                Emit("delayedEffectApplied", $"{name} gets a {effect.Text} token, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                CreateToken(card, effect.Text ?? "Delayed", effect.SourceId ?? "delayed-effect", effect.Stat, effect.Amount);
                break;
            case DelayedKinds.Keyword:
                {
                    var parts = effect.Arg!.Split('/', 2);
                    Emit("delayedEffectApplied", $"{name} gains {parts[0]}, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                    GrantKeyword(card, parts[0], effect.SourceId ?? "delayed-effect", parts.Length > 1 ? parts[1] : GrantDuration.Turn);
                    break;
                }
            case DelayedKinds.Ability:
                {
                    var parts = effect.Arg!.Split('/', 2);
                    Emit("delayedEffectApplied", $"{name} gains an ability, as delayed ({effect.Reason}).", card.OwnerId, card.Id);
                    GrantAbility(card, parts[0], effect.SourceId ?? "delayed-effect", parts.Length > 1 ? parts[1] : GrantDuration.Turn);
                    break;
                }
        }
    }

    // Test seams (issue #492): record a delayed effect exactly as the recording layer does, and apply a trigger point's effects. Never used outside tests.
    internal void DelayEffectForTest(string cardId, string kind, string until, int? playerId = null, string? stat = null, int amount = 0, string? arg = null, string reason = "test")
    {
        Record(new DelayedEffect { Kind = kind, Until = until, CardId = Card(cardId).Id, PlayerId = playerId, Reason = reason, Stat = stat, Amount = amount, Arg = arg });
    }
    internal bool ApplyDelayedEffectsForTest(string until) => ApplyDelayedEffects(until);

    // The trigger points that are one phase-bound (SetPhase): EndOfPhase effects of the phase family that is ending, then those that wait for the next Dynasty / End Phase to begin.
    private void ApplyDelayedEffectsAtPhaseEnd(Phase leaving)
    {
        var family = PhaseFamily(leaving);
        ApplyDelayedEffects(DelayUntil.EndOfPhase, e => PhaseFamily(e.RecordedPhase) == family);
    }
}
