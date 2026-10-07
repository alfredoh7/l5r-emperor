using L5R.Core;
using static RegressionAssertions;

// Primitive P8 (issue #712, ActionEffectWatch.cs): the action-effect watch. The registrations below are TEST-ONLY and inert unless a test sets Mode, so no other suite sees them; the cards that use the
// primitive (Moto Taigo - exp, Reverse Fate, Verdant Jungle) are wired in a later wave. Every position is driven through the commands the engine offers; the held action is a real fixture action.
internal static class ActionEffectWatchTests
{
    private static string Mode = "";
    private static readonly List<string> Log = [];
    private const string Arm = "test-p8-arm";

    public static void Run(TestRunner runner) => runner.Run("action-effect-watch", [
        ("Nothing armed, nothing stored: a match that never arms a watch has no watch list, no new field in its saved state, and records its moves, discards and creations as before", NothingArmed),
        ("A watch can only be armed against a held action: arming with no window open is refused and stores nothing", ArmNeedsAHeldAction),
        ("A moved watch hears the first move of the held action with its destination and the army's side, fires once and is spent", MovedWatch),
        ("A watch fires only for the held action: an action that moves nothing leaves a moved watch unheard and the watch is dropped when the action finishes", OnlyTheHeldAction),
        ("A discarded watch hears the top-of-deck discard with its Focus Value; a Focus modifier on the discarded card changes FocusValueOf but not the printed value a duel reads, and ends with the turn", DiscardedWatch),
        ("An AtEnd watch hears every fact once the action is done and asks its owner a card question that opens at the next safe point, survives a save, and is applied with the chosen card", AtEndAsks),
        ("A created-Personality watch hears the copy an Open action creates, and the record carries it", CreatedWatch),
        ("A move gate refuses a movement to a battlefield for the held action only; an immune mover is not negated; the action's costs are still paid", MoveGate),
        ("A watch is dropped when its action is negated whole", NegatedActionDropsWatch),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.Abilities.Register("TestP8Abilities", 19300, static abilities =>
        {
            abilities.Add(Arm, new("Reaction", "Test: watch what the held action does", "none", Trigger: ReactionTriggers.TargetedByAction));
            return abilities;
        });
        EngineHooks.TargetedReaction.Register("TestP8Targeted", 19300, static (engine, context) => context.abilityId == Arm && Mode != "");
        EngineHooks.RunEffects.Register("TestP8Run", 19300, static (engine, context) => RunTest(engine, context.id, context.source));
        ActionWatchRegistry.Fired("TestP8Fired", 19300, "test-p8-cb", static fact => Fired(fact));
        ActionWatchRegistry.Gate("TestP8Gate", 19300, "test-p8-gate", static gate => Mode == "gate-defending" ? !gate.Attacking : Mode == "gate-attacking" && gate.Attacking);
    }

    private static bool? RunTest(MatchEngine engine, string id, CardInstance? source)
    {
        if (id != Arm || source is null) return null;
        switch (Mode)
        {
            case "moved": engine.ArmActionEffectWatch(WatchKinds.Moved, "test-p8-cb", source.OwnerId, source.Id); break;
            case "moved-miss": engine.ArmActionEffectWatch(WatchKinds.Moved, "test-p8-cb", source.OwnerId, source.Id); break;
            case "moved-end": engine.ArmActionEffectWatch(WatchKinds.Moved, "test-p8-cb", source.OwnerId, source.Id, atEnd: true); break;
            case "discard": engine.ArmActionEffectWatch(WatchKinds.Discarded, "test-p8-cb", source.OwnerId, source.Id); break;
            case "created": engine.ArmActionEffectWatch(WatchKinds.CreatedPersonality, "test-p8-cb", source.OwnerId, source.Id, atEnd: true); break;
            case "gate-defending" or "gate-attacking": engine.ArmActionEffectWatch(WatchKinds.MoveGate, "test-p8-gate", source.OwnerId, source.Id); break;
        }
        return false;
    }

    private static ActionWatchResponse Fired(ActionWatchFact fact)
    {
        Log.Add($"fired:{fact.Facts.Count}:{fact.First.Kind}:{fact.First.CardId}:to={fact.First.ToBattlefield}:{fact.First.Side}:focus={fact.First.Focus}");
        if (Mode == "discard")
        {
            var discarded = fact.Engine.State.Cards[fact.First.CardId];
            fact.Engine.AddFocusModifier(fact.Engine.CardForTest(fact.First.CardId), fact.Watch.SourceId ?? "test", "test-p8", 1);
            Log.Add("modified:" + discarded.Id);
            return ActionWatchResponse.Done;
        }
        if (Mode == "moved-end")
            return ActionWatchResponse.Ask(fact.Facts.Select(f => f.CardId).ToArray(), "Choose one of the moved units", static (engine, f, chosen) => Log.Add("asked:" + chosen));
        return ActionWatchResponse.Done;
    }

    private static void With(string mode, Action action)
    {
        var was = Mode; Mode = mode;
        try { action(); } finally { Mode = was; }
    }

    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();

    private static GameCommand ReactionOffer(MatchEngine e, int player)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == Arm).ToArray();
        Check(offers.Length == 1, $"expected one enabled {Arm} offer for player {player}, got {offers.Length}");
        return offers[0].Command!;
    }

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    // Player 1 (the Attacker) moves player 0's Personality (which holds the test Reaction) from the current battlefield 1 to battlefield 3.
    private static (MatchState S, CardInstance Source, CardInstance Target) MoveRig()
    {
        var s = Batch336Support.Table(); s.PriorityPlayerId = 1; s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 3];
        var source = Batch336Support.Add(s, "training-move-source", 1, Zone.Battlefield, province: 1);
        var target = Batch336Support.Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        target.GrantedAbilities.Add(new GrantedAbility(Arm, "test", GrantDuration.Permanent));
        return (s, source, target);
    }

    private static MatchEngine PlayMove(MatchState s, CardInstance source, CardInstance target, string mode)
    {
        var e = MatchEngine.FromTestState(s);
        var move = Batch336Support.Enabled(e, 1, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 };
        With(mode, () =>
        {
            Accept(e, move);
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the target's controller");
            Accept(e, ReactionOffer(e, 0));
            ClosePassing(e);
        });
        return e;
    }

    private static void NothingArmed()
    {
        var (s, source, target) = MoveRig();
        var e = MatchEngine.FromTestState(s);
        var move = Batch336Support.Enabled(e, 1, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 };
        Mode = "";
        Accept(e, move);
        Check(e.State.PendingChoice?.Type != "beforeActionReaction", "no reactor, no window");
        Eq(e.State.Cards[target.Id].ProvinceIndex, 3, "the move happened as ever");
        Check(e.State.LastResolvedAction!.Affected.Any(a => a.Kind == MatchEngine.MovedKind && a.CardId == target.Id && a.ToBattlefield == 3), "the record names the destination");
        Eq(e.TestWatchCount, 0, "no watch");
        Check(e.State.ActionEffectWatches is null, "no watch list");
        Check(!System.Text.Json.JsonSerializer.Serialize(e.State).Contains("ActionEffectWatches"), "the saved state carries no new field until a watch is armed");
        Check(!System.Text.Json.JsonSerializer.Serialize(new AffectedCard("a", "moved")).Contains("ToBattlefield"), "an unset destination is not written");
    }

    private static void ArmNeedsAHeldAction()
    {
        var (s, _, _) = MoveRig();
        var e = MatchEngine.FromTestState(s);
        var error = "";
        try { e.ArmActionEffectWatch(WatchKinds.Moved, "test-p8-cb", 0, null); } catch (Exception ex) { error = ex.Message; }
        Check(error.Length > 0, "arming with no held action is refused");
        Eq(e.TestWatchCount, 0, "nothing stored");
    }

    private static void MovedWatch()
    {
        var (s, source, target) = MoveRig();
        Log.Clear();
        var e = PlayMove(s, source, target, "moved");
        Eq(e.State.Cards[target.Id].ProvinceIndex, 3, "the action resolved");
        Eq(string.Join("|", Log), $"fired:1:moved:{target.Id}:to=3:defending:focus=", "heard once, with the destination and side");
        Eq(e.TestWatchCount, 0, "the watch is spent");
    }

    private static void OnlyTheHeldAction()
    {
        // Player 1's Fires of Turmoil discards a card and bows or straightens the target; it moves nothing, so a moved watch hears nothing.
        var s = Batch336Support.Table(); s.PriorityPlayerId = 1;
        var fires = Batch336Support.Add(s, DefinitionWith("fires-of-turmoil"), 1, Zone.Hand);
        var target = Batch336Support.Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        target.GrantedAbilities.Add(new GrantedAbility(Arm, "test", GrantDuration.Permanent));
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Log.Clear();
        With("moved-miss", () =>
        {
            Accept(e, Batch336Support.Offer(e, 1, "fires-of-turmoil", fires.Id, target.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens");
            Accept(e, ReactionOffer(e, 0));
            Eq(e.TestWatchCount, 1, "armed");
            ClosePassing(e);
        });
        Eq(Log.Count, 0, "nothing moved, nothing heard");
        Eq(e.TestWatchCount, 0, "the watch is dropped when the action finishes");
    }

    private static void DiscardedWatch()
    {
        var s = Batch336Support.Table(); s.PriorityPlayerId = 1;
        var fires = Batch336Support.Add(s, DefinitionWith("fires-of-turmoil"), 1, Zone.Hand);
        var target = Batch336Support.Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        target.GrantedAbilities.Add(new GrantedAbility(Arm, "test", GrantDuration.Permanent));
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var topId = s.Players[1].FateDeck[0];
        var printed = CardCatalog.Get(s.Cards[topId].DefinitionId).Focus;
        var e = MatchEngine.FromTestState(s);
        Log.Clear();
        With("discard", () =>
        {
            Accept(e, Batch336Support.Offer(e, 1, "fires-of-turmoil", fires.Id, target.Id));
            Accept(e, ReactionOffer(e, 0));
            ClosePassing(e);
        });
        Check(e.State.Cards[topId].Zone == Zone.FateDiscard, "the top card was discarded");
        Check(Log.Contains($"fired:1:discarded:{topId}:to=:focus={printed}") || Log.Any(l => l.StartsWith($"fired:1:discarded:{topId}") && l.EndsWith($"focus={printed}")), "heard with its Focus Value: " + string.Join("|", Log));
        Eq(e.FocusValueForTest(topId), printed + 1, "the modifier raises the Focus Value read outside a duel");
        Eq(CardCatalog.Get(e.State.Cards[topId].DefinitionId).Focus, printed, "the printed value a duel reads is untouched");
        Check(e.State.LastResolvedAction!.Affected.Any(a => a.Kind == MatchEngine.DiscardedKind && a.CardId == topId && a.Focus == printed), "the action's record names the discard and its Focus Value");
        // The modifier ends with the turn, like every other.
        var later = e.State; later.TurnNumber++;
        foreach (var card in later.Cards.Values) card.Modifiers.RemoveAll(m => !m.Permanent);
        Eq(MatchEngine.FromTestState(later).FocusValueForTest(topId), printed, "gone with the turn");
    }

    private static void AtEndAsks()
    {
        var (s, source, target) = MoveRig();
        Log.Clear();
        var e = PlayMove(s, source, target, "moved-end");
        Check(Log.Count == 1 && Log[0].StartsWith("fired:1:moved:"), "the AtEnd watch fired once with the whole record: " + string.Join("|", Log));
        Check(e.State.PendingEventChoice is { Kind: "p8-watch" } || e.State.PendingEventChoices.Any(c => c.Kind == "p8-watch"), "the question is queued or open");
        if (e.State.PendingEventChoice is null) { e.OpenQueuedChoicesForTest(e.State.PriorityPlayerId); }
        Check(e.State.PendingChoice is { Type: "eventChoice" } && e.State.PendingEventChoice is { Kind: "p8-watch", PlayerId: 0 }, "the watch's owner is asked");
        var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Check(options.Any(a => a.Command!.TargetId == target.Id) && options.Any(a => a.Command!.Choice == "none"), "the owner may pick the moved unit or none");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Check(json.Contains("p8-watch") && System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!.PendingEventChoice is { Kind: "p8-watch" }, "the question survives a save");
        var restored = MatchEngine.FromTestState(System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!);
        Log.Clear();
        With("moved-end", () => Accept(restored, restored.QueryActions(0).First(a => a.Type == "eventChoice" && a.Command!.TargetId == target.Id).Command!));
        Check(Log.Contains("asked:" + target.Id), "the choice was applied after the save");
        Check(restored.State.PendingChoice?.Type != "eventChoice", "answered");
        // Only the owner may answer.
        var (s2, src2, tgt2) = MoveRig();
        var e2 = PlayMove(s2, src2, tgt2, "moved-end");
        if (e2.State.PendingEventChoice is null) e2.OpenQueuedChoicesForTest(e2.State.PriorityPlayerId);
        Reject(e2, new GameCommand("eventChoice", 1, e2.Version, TargetId: tgt2.Id));
    }

    private static void CreatedWatch()
    {
        var s = Batch336Support.Table(Phase.Action, 1); s.PriorityPlayerId = 1;
        var copyist = Batch336Support.Add(s, "training-copyist", 1, Zone.Home);
        var foe = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        foe.GrantedAbilities.Add(new GrantedAbility(Arm, "test", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        Log.Clear();
        With("created", () =>
        {
            Accept(e, Batch336Support.Offer(e, 1, "training-copy-personality", copyist.Id, foe.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens");
            Accept(e, ReactionOffer(e, 0));
            ClosePassing(e);
        });
        Check(Log.Count == 1 && Log[0].StartsWith("fired:1:created:"), "the watch heard the created Personality: " + string.Join("|", Log));
        var created = Log[0].Split(':')[3];
        Check(e.State.Cards.ContainsKey(created) && e.State.Cards[created].OwnerId == 1, "it names the Personality that was created");
        Check(e.State.LastResolvedAction!.Affected.Any(a => a.Kind == WatchKinds.CreatedPersonality && a.CardId == created), "and the action's record carries it (P6's created kind, reused)");
    }

    private static void MoveGate()
    {
        // Defending mover (player 0's target): the gate for "defending" refuses the move; the move is negated, nothing is recorded, and the unit stays.
        var (s, source, target) = MoveRig();
        var e = PlayMove(s, source, target, "gate-defending");
        Eq(e.State.Cards[target.Id].ProvinceIndex, 1, "the movement was negated: he stays at the battlefield");
        Check(e.State.Events.Any(x => x.Type == "moveNegated"), "and the negation is logged");
        Check(!e.State.LastResolvedAction!.Affected.Any(a => a.Kind == MatchEngine.MovedKind), "a negated move is not on the record");
        Check(e.State.Cards[source.Id].Bowed || e.State.Cards[source.Id].AbilityUses.Count > 0 || e.State.Events.Any(x => x.Type == "abilityUsed"), "the action was taken: its cost is paid");
        // The gate for "attacking" does not refuse a defending unit's move.
        var (s2, src2, tgt2) = MoveRig();
        var e2 = PlayMove(s2, src2, tgt2, "gate-attacking");
        Eq(e2.State.Cards[tgt2.Id].ProvinceIndex, 3, "a defending mover is not refused by the attacking-army gate");
        // A watch armed against this action does not refuse another action's move.
        var (s3, src3, tgt3) = MoveRig();
        var e3 = PlayMove(s3, src3, tgt3, "gate-attacking");
        Eq(e3.TestWatchCount, 0, "the gate ended with the action");
        // An immune mover: the movement cannot be negated.
        var (s4, src4, tgt4) = MoveRig();
        var e4 = MatchEngine.FromTestState(s4);
        e4.GrantImmunity(e4.CardForTest(tgt4.Id), ImmunityKind.MoveNegation, ImmunityUntil.Turn, ImmunitySource.Any);
        var move4 = Batch336Support.Enabled(e4, 1, "training-move-anywhere", src4.Id).Command! with { TargetId = tgt4.Id, Province = 3 };
        With("gate-defending", () => { Accept(e4, move4); Accept(e4, ReactionOffer(e4, 0)); ClosePassing(e4); });
        Eq(e4.State.Cards[tgt4.Id].ProvinceIndex, 3, "a mover whose movement cannot be negated moves anyway");
    }

    private static void NegatedActionDropsWatch()
    {
        var (s, source, target) = MoveRig();
        var e = MatchEngine.FromTestState(s);
        var move = Batch336Support.Enabled(e, 1, "training-move-anywhere", source.Id).Command! with { TargetId = target.Id, Province = 3 };
        With("moved", () =>
        {
            Accept(e, move);
            Accept(e, ReactionOffer(e, 0));
            Eq(e.TestWatchCount, 1, "armed in the window");
            e.NegateHeldActionForTest(target.Id);
            ClosePassing(e);
        });
        Eq(e.TestWatchCount, 0, "a wholly negated action leaves no watch behind");
        Eq(e.State.Cards[target.Id].ProvinceIndex, 1, "and moves nothing");
    }
}
