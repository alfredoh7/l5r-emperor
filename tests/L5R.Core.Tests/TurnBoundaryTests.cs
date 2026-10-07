using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #690, primitive P4 (TurnBoundary.cs, TurnBoundaryGrants.cs, TurnBoundaryRules.cs): the turn-begin hook with a decision that pauses and resumes, the owner-relative durations
// (the delay store, timed removal, an Event in play), player-level granted abilities and the turn-scoped standing rules. No card is wired: every piece is driven by the fixture
// callbacks registered below (all keyed on "training-p4-" effect or ability ids, or on Mode, so no other suite sees them) and by cards given a per-instance definition.
internal static class TurnBoundaryTests
{
    private const string Yours = "training-p4-begin-yours", Each = "training-p4-begin-each", Other = "training-p4-begin-other", Late = "training-p4-begin-late", Chooser = "training-p4-begin-choice";
    private const string ChoiceKey = "training-p4-choice", ModeChoiceKey = "training-p4-mode-choice";
    private const string GrantedOpen = "training-p4-granted-open", LimitedDraw = "training-limited-draw";
    private static string Mode = "";
    private static readonly List<(string Effect, string CardId, int Player, int Turn, bool Bowed)> Fired = [];

    public static void Run(TestRunner runner) => runner.Run("turn-boundary", new (string, Action)[]
    {
        ("TurnBegins: \"after your turn begins\", \"after each player's turn begins\" and \"after another player's turn begins\" each fire once per qualifying turn, never for the wrong player, before the Straighten Phase, in hook order", Scopes),
        ("TurnBegins: a callback's choice stops the sequence before the Straighten Phase, is offered to the chooser only, refuses a wrong answer unchanged, asks again for the next card, and then the later callbacks and the turn go on", PauseAndResume),
        ("Save: a copy of the state taken while a turn-begin choice is pending resumes exactly as the original does", CopyResumes),
        ("Save: a replay of the commands up to a pending turn-begin choice reproduces it, and the replayed game resumes and stays identical", ReplayResumes),
        ("Durations: your next turn, your second turn from now, the next turn of anyone and the game's end name the right turn for the active and the other player, and pass exactly then", Durations),
        ("The delay store: effects delayed until a player's next turn, his second turn from now and the other player's next turn are applied as exactly those turns begin; one delayed until the game ends never is", DelayedUntilTurns),
        ("Timed removal and an Event in play end on the same durations: the Event as its owner's next turn begins, the removal as his second turn from now begins, neither for \"until the game ends\"", RemovalAndEventExpiry),
        ("A player-level granted ability is dormant until granted, offered through the action list to that player only, usable, alive through the other player's turn and gone when his next turn begins", GrantedAbilityLifecycle),
        ("\"Each player has the ability\" until the game ends stays for both players; a grant tied to a card ends when the card leaves play; grants round-trip through the saved state", GrantedAbilityEachAndCard),
        ("Standing rule, end-of-turn draw: each player draws two instead of one at the end of his turn only if no other card went into his hand that turn; a rule for one player until his next turn begins ends then", EndTurnExtraDraw),
        ("Standing rule, a Limited action as an Open action: once per turn, on a Strategy only, for its player only, until its duration passes; Plans Within Plans is asked first and spends nothing here", LimitedAsOpen),
        ("The stores round-trip through the saved state, the pending turn-begin state is a registered decision state, and the channels are sealed with the fixture callbacks in priority order", RoundTripAndRegistry),
    });

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false) => Batch336Support.Add(s, def, owner, zone, bowed);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);

    // ---- the fixture callbacks ---------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        // The three printed scopes: each fixture card that fires is logged (with whether it was still bowed, i.e. whether the Straighten Phase had come).
        EngineHooks.TurnBegins.Register("TestP4Scopes", 19000, static (engine, turn) =>
        {
            foreach (var (effect, scope) in new[] { (Yours, TurnBeginScope.Yours), (Each, TurnBeginScope.EachPlayers), (Other, TurnBeginScope.AnotherPlayers) })
                foreach (var card in engine.TurnBeginCards(effect, turn, scope)) Fired.Add((effect, card.Id, turn.PlayerId, turn.TurnNumber, card.Bowed));
            return false;
        });
        // "After your turn begins: choose ..." -- one question per fixture card (asked once a turn), or, in a replayed scenario that has no fixture card, one for the turn's player.
        EngineHooks.TurnBegins.Register("TestP4Choice", 19001, static (engine, turn) =>
        {
            if (Mode == "replay-choice")
                return engine.OpenTurnBeginChoice(ModeChoiceKey, turn.PlayerId, null, "Training: choose who gains the training ability.", ChoiceOptions(turn.PlayerId));
            foreach (var card in engine.TurnBeginCards(Chooser, turn, TurnBeginScope.Yours))
            {
                if (engine.PeriodCountOf("training-p4-asked:" + card.Id, Period.Turn) > 0) continue;
                engine.AddPeriodCount("training-p4-asked:" + card.Id, Period.Turn);
                return engine.OpenTurnBeginChoice(ChoiceKey, turn.PlayerId, card.Id, "Training: choose who gains the training ability.", ChoiceOptions(turn.PlayerId));
            }
            return false;
        });
        EngineHooks.TurnBegins.Register("TestP4Late", 19002, static (engine, turn) =>
        {
            foreach (var card in engine.TurnBeginCards(Late, turn, TurnBeginScope.EachPlayers)) Fired.Add((Late, card.Id, turn.PlayerId, turn.TurnNumber, card.Bowed));
            return false;
        });
        EngineHooks.TurnBeginChoices.Register("TestP4ChoiceResolve", 19001, static (engine, answer) =>
        {
            if (answer.Key is not (ChoiceKey or ModeChoiceKey)) return null;
            if (answer.Option.PlayerId is int who) engine.GrantPlayerAbility(who, GrantedOpen, engine.UntilOwnersNextTurnBegins(who), "Training choice");
            return answer.Key == ChoiceKey; // the fixture cards: ask about the next one; the replay mode: one question a turn
        });
        EngineHooks.Abilities.Register("TestP4Abilities", 19001, static abilities =>
        {
            abilities.Add(GrantedOpen, new("Open", "Training P4: a granted player ability", "none", PlayerAbility: true));
            return abilities;
        });
        EngineHooks.PlayerGrantAbilities.Register("TestP4GrantIds", 19001, static () => [GrantedOpen]);
        EngineHooks.RunEffects.Register("TestP4Run", 19001, static (engine, context) => context.id == GrantedOpen ? false : null);
    }

    private static TurnBeginOption[] ChoiceOptions(int player) =>
        [new("grant-me", "You gain the training ability", PlayerId: player), new("grant-other", "Your opponent gains the training ability", PlayerId: 1 - player), new("decline", "Nobody gains it")];

    private static T With<T>(string mode, Func<T> body)
    {
        Mode = mode;
        try { return body(); } finally { Mode = ""; }
    }

    // ---- driving the game ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Table(int actor = 0)
    {
        var s = Batch336Support.Table(Phase.Action, actor);
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        return s;
    }

    // A card of the fixture's own definition: a training card whose Effect (or abilities) the test supplies.
    private static CardInstance Fixture(MatchState s, string effect, int owner, bool bowed = false, string def = "training-farm")
    {
        var card = Add(s, def, owner, Zone.Home, bowed);
        card.Printed = CardCatalog.Get(def) with { Effect = effect };
        return card;
    }

    // Player abilities are enabled in the published profile only: the fixture Strongholds are made to count as published.
    private static void PublishStrongholds(MatchState s)
    {
        foreach (var p in s.Players) { var stronghold = s.Cards[p.StrongholdId]; stronghold.Printed = CardCatalog.Get(stronghold.DefinitionId) with { ScenarioOnly = false }; }
    }

    // Passes (and ends phases, declines, discards to the hand limit) for whoever must act until `stop` holds.
    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 400 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            var command = pick.Command!;
            if (command.Type == "discardHand") { var hand = e.State.Players[mover].Hand; command = command with { CardIds = hand.Take(hand.Count - 8).ToArray() }; }
            var result = e.Execute(command);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber}, pending {e.State.PendingChoice?.Type})");
        return e;
    }

    // The Action Phase of the turn `turns` after the current one.
    private static MatchEngine ToAction(MatchEngine e, int turn) => Passing(e, x => x.State.TurnNumber == turn && x.State.Phase == Phase.Action && x.State.PendingChoice is null);
    private static MatchEngine ToTurnChoice(MatchEngine e) => Passing(e, x => x.State.PendingChoice?.Type == TurnBoundary.ChoiceType);
    private static GameCommand Choose(MatchEngine e, int player, string option) =>
        e.QueryActions(player).Single(a => a.Type == TurnBoundary.ChooseCommand && a.Command!.Choice == option).Command!;
    private static bool Offered(MatchEngine e, int player, string ability, string? cardId = null) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.Command.CardId == cardId));

    // ---- 1. the hook ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Scopes()
    {
        var s = Table();
        var yours = Fixture(s, Yours, 0, bowed: true); var each = Fixture(s, Each, 0, bowed: true); var other = Fixture(s, Other, 0, bowed: true); var late = Fixture(s, Late, 0);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        Fired.Clear();
        ToAction(e, start + 4); // four turn beginnings: player 1, player 0, player 1, player 0
        var mine = Fired.Where(f => f.Turn > start && f.Turn <= start + 4).ToArray();
        (int Player, int Turn)[] Turns(string effect, string id) => mine.Where(f => f.Effect == effect && f.CardId == id).Select(f => (f.Player, f.Turn)).ToArray();
        Check(Turns(Yours, yours.Id).SequenceEqual(new[] { (0, start + 2), (0, start + 4) }), "\"your turn\": its controller's two turns, once each: " + string.Join(",", Turns(Yours, yours.Id)));
        Check(Turns(Each, each.Id).SequenceEqual(new[] { (1, start + 1), (0, start + 2), (1, start + 3), (0, start + 4) }), "\"each player's turn\": all four, once each: " + string.Join(",", Turns(Each, each.Id)));
        Check(Turns(Other, other.Id).SequenceEqual(new[] { (1, start + 1), (1, start + 3) }), "\"another player's turn\": the other player's two turns, once each: " + string.Join(",", Turns(Other, other.Id)));
        // Before the Straighten Phase: the controller's own bowed card was still bowed when its trait fired, and is straight in the Action Phase that follows.
        Check(mine.First(f => f.Effect == Yours) is { Bowed: true, Turn: var firstTurn } && firstTurn == start + 2, "the trait resolved before its controller's cards straightened");
        Check(!e.State.Cards[yours.Id].Bowed, "and the Straighten Phase then straightened it");
        // Hook order: within one turn beginning the lower priority runs first.
        var turnTwo = mine.Where(f => f.Turn == start + 2).Select(f => f.Effect).ToArray();
        Check(turnTwo.SequenceEqual(new[] { Yours, Each, Late }), "priority order within a turn: " + string.Join(",", turnTwo));
        Eq(Turns(Late, late.Id).Length, 4, "the later callback ran every turn too");
        Check(e.State.PendingTurnBegins is null, "nothing is left pending by callbacks that open no choice");
    }

    private static (MatchEngine Engine, string First, string Second, string Late, int Start) ChoiceTable()
    {
        var s = Table();
        var first = Fixture(s, Chooser, 1); var second = Fixture(s, Chooser, 1); var late = Fixture(s, Late, 1);
        Fixture(s, Chooser, 0); // the other player's card: never asked on player 1's turn
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        Fired.Clear();
        ToTurnChoice(e);
        return (e, first.Id, second.Id, late.Id, start);
    }

    private static void PauseAndResume()
    {
        var (e, first, second, late, start) = ChoiceTable();
        var st = e.State;
        Eq(st.TurnNumber, start + 1, "the choice opened as player 1's turn began");
        Eq(st.ActivePlayerId, 1);
        Check(st.Phase != Phase.Straighten && st.Phase != Phase.Action, $"the Straighten Phase has not begun ({st.Phase})");
        Check(st.PendingChoice is { PlayerId: 1 } pc && pc.CardId == first, "the first fixture card asks its controller");
        Check(st.PendingTurnBegins is { PlayerId: 1, HookId: "TestP4Choice", ChoiceKey: ChoiceKey, ChooserId: 1 } && st.PendingTurnBegins.Options.Count == 3, "the sequence's place and the open choice are in the state");
        Check(!Fired.Any(f => f.Effect == Late), "the callback after it has not run");
        Eq(e.QueryActions(1).Count(a => a.Type == TurnBoundary.ChooseCommand && a.Enabled), 3, "three answers for the chooser");
        Check(e.QueryActions(1).All(a => a.Type is TurnBoundary.ChooseCommand or "concede"), "and nothing else: " + string.Join(",", e.QueryActions(1).Select(a => a.Type)));
        Check(!e.QueryActions(0).Any(a => a.Type == TurnBoundary.ChooseCommand), "the other player is offered none of them");
        Reject(e, new GameCommand(TurnBoundary.ChooseCommand, 0, e.Version, Choice: "decline"));
        Reject(e, new GameCommand(TurnBoundary.ChooseCommand, 1, e.Version, Choice: "no-such-option"));
        Reject(e, new GameCommand("pass", 1, e.Version));

        Accept(e, Choose(e, 1, "grant-me"));
        st = e.State;
        Check(st.PendingChoice is { Type: TurnBoundary.ChoiceType, PlayerId: 1 } next && next.CardId == second, "the same callback asks about its second card");
        Check(st.PlayerAbilityGrants.Count == 1 && st.PlayerAbilityGrants[0] is { PlayerId: 1, AbilityId: GrantedOpen }, "the first answer was applied");
        Check(!Fired.Any(f => f.Effect == Late), "still before the later callback");

        Accept(e, Choose(e, 1, "decline"));
        st = e.State;
        Check(st.PendingChoice is null && st.PendingTurnBegins is null, "the sequence is over");
        Eq(Fired.Count(f => f.Effect == Late && f.CardId == late && f.Turn == start + 1), 1, "the later callback ran, once");
        Check(st.Phase == Phase.Action && st.ActivePlayerId == 1 && st.TurnNumber == start + 1, $"the turn went on to its Action Phase ({st.Phase})");
        Eq(st.PlayerAbilityGrants.Count, 1, "declining granted nothing more");

        // Player 0's turn: his own fixture card asks him; then player 1's next turn -- the grant made "until your next turn begins" is already gone when the question comes.
        ToTurnChoice(e);
        Check(e.State is { ActivePlayerId: 0 } && e.State.PendingChoice!.PlayerId == 0, "player 0 is asked on his own turn only");
        Eq(e.State.PlayerAbilityGrants.Count, 1, "player 1's grant stands through player 0's turn");
        Accept(e, Choose(e, 0, "decline"));
        ToTurnChoice(e);
        Check(e.State is { ActivePlayerId: 1 } && e.State.TurnNumber == start + 3, "player 1's next turn");
        Eq(e.State.PlayerAbilityGrants.Count, 0, "what lasted until his next turn began ended before the turn-begin callbacks ran");
    }

    // ---- save and replay ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CopyResumes()
    {
        var (e, _, second, _, start) = ChoiceTable();
        var copy = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Eq(Json(copy), Json(e), "the saved state is the state");
        foreach (var engine in new[] { e, copy })
        {
            Accept(engine, Choose(engine, 1, "grant-other"));
            Check(engine.State.PendingChoice?.CardId == second, "the second question");
            Accept(engine, Choose(engine, 1, "grant-me"));
            Check(engine.State.PendingTurnBegins is null && engine.State.Phase == Phase.Action && engine.State.TurnNumber == start + 1, "resumed into the turn");
            Eq(engine.State.PlayerAbilityGrants.Count, 2, "both answers applied");
        }
        Eq(Json(copy), Json(e), "the copy and the original went the same way");
        ToTurnChoice(copy); ToTurnChoice(e);
        Eq(Json(copy), Json(e), "and stay the same through the next turn's choice");
    }

    private static void ReplayResumes() => With("replay-choice", () =>
    {
        // The first turn of a real game already begins with the question; it is answered, and the game is played to the next turn's.
        var e = MatchEngine.CreateEconomyScenario();
        var start = e.State.TurnNumber;
        Check(e.State.PendingTurnBegins is { ChoiceKey: ModeChoiceKey } && e.State.PendingChoice?.Type == TurnBoundary.ChoiceType, "the game's first turn begins with the choice");
        Accept(e, Choose(e, e.State.PendingChoice!.PlayerId, "decline"));
        Check(e.State.PendingTurnBegins is null && e.State.Phase == Phase.Action, $"and goes on once it is answered ({e.State.Phase})");
        ToTurnChoice(e);
        Check(e.State.TurnNumber == start + 1 && e.State.PendingTurnBegins is { ChoiceKey: ModeChoiceKey, HookId: "TestP4Choice" }, $"a turn-begin choice is pending in a real game (turn {e.State.TurnNumber} from {start})");
        var replayed = MatchEngine.Replay(e.ExportReplay());
        Eq(Json(replayed), Json(e), "the replay stops at the same pending choice");
        var chooser = e.State.PendingChoice!.PlayerId;
        Eq(chooser, e.State.ActivePlayerId);
        Accept(e, Choose(e, chooser, "grant-me"));
        Accept(replayed, Choose(replayed, chooser, "grant-me"));
        Check(replayed.State.PendingTurnBegins is null && replayed.State.PendingChoice is null && replayed.State.Phase == Phase.Action, $"the replayed game resumed ({replayed.State.Phase})");
        Eq(replayed.State.PlayerAbilityGrants.Count, 1, "with the answer applied");
        Eq(Json(replayed), Json(e));
        // A save taken after the answer replays through the pause and the resumption.
        Eq(Json(MatchEngine.Replay(e.ExportReplay())), Json(e), "a replay through the answered choice");
        ToTurnChoice(e);
        Eq(Json(MatchEngine.Replay(e.ExportReplay())), Json(e), "and on to the next turn's choice");
        return true;
    });

    // ---- 2. durations --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Durations()
    {
        var e = MatchEngine.FromTestState(Table(0));
        var turn = e.State.TurnNumber;
        Check(e.UntilOwnersNextTurnBegins(0) is { Kind: TurnUntil.TurnBegins, Owner: 0, At: 4 }, "the active player's next turn is his fourth");
        Check(e.UntilOwnersSecondTurnBegins(0) is { Owner: 0, At: 5 }, "his second turn from now");
        Check(e.UntilOwnersNextTurnBegins(1) is { Owner: 1, At: 4 } && e.UntilOwnersTurnBegins(1, 2) is { Owner: 1, At: 6 }, "the other player's turns count from his own");
        Check(e.UntilTurnBegins() is { Owner: null } any && any.At == turn + 1, "the next turn, whoever's");
        var next = e.UntilOwnersNextTurnBegins(0); var second = e.UntilOwnersSecondTurnBegins(0); var anyNext = e.UntilTurnBegins();
        Check(!e.UntilPassed(next) && !e.UntilPassed(second) && !e.UntilPassed(anyNext) && !e.UntilPassed(TurnUntil.GameEnds()), "nothing has passed yet");
        Check(e.DescribeUntil(next).Contains("next turn begins") && e.DescribeUntil(second).Contains("second turn from now") && e.DescribeUntil(TurnUntil.GameEnds()) == "until the game ends", "the wording");
        var otherTurn = Edit(e, st => { st.TurnNumber++; st.ActivePlayerId = 1; st.Players[1].TurnsTaken++; });
        Check(!otherTurn.UntilPassed(next) && otherTurn.UntilPassed(anyNext), "the other player's turn: only \"the next turn\" has come");
        var ownTurn = Edit(otherTurn, st => { st.TurnNumber++; st.ActivePlayerId = 0; st.Players[0].TurnsTaken++; });
        Check(ownTurn.UntilPassed(next) && !ownTurn.UntilPassed(second), "his next turn: the first has come, the second not");
        var later = Edit(ownTurn, st => { st.TurnNumber += 2; st.Players[0].TurnsTaken++; st.Players[1].TurnsTaken++; });
        Check(later.UntilPassed(second) && !later.UntilPassed(TurnUntil.GameEnds()), "his turn after that: the second has come; the game's end never does");
        // The delay store's strings: one per (owner, turn), and they read back.
        Eq(e.DelayUntilOwnersNextTurnBegins(0), "turn-begins:0:4"); Eq(e.DelayUntilOwnersSecondTurnBegins(1), "turn-begins:1:5"); Eq(e.DelayUntilTurn(TurnUntil.GameEnds()), DelayUntil.GameEnd);
        Check(TurnBoundary.ParseDelayUntil("turn-begins:0:4") == next && TurnBoundary.ParseDelayUntil(e.DelayUntilTurn(anyNext)) == anyNext && TurnBoundary.ParseDelayUntil(DelayUntil.EndOfTurn) is null, "they parse back");
        Check(DelayUntil.Describe("turn-begins:0:4").Contains("player 1's turn 4") && DelayUntil.Describe(DelayUntil.GameEnd) == "the game ends" && DelayUntil.Describe(DelayUntil.EndOfTurn) == "before this turn ends", "and are described");
    }

    private static void DelayedUntilTurns()
    {
        var s = Table(0);
        var a = Add(s, "training-crane-samurai", 0, Zone.Home); var b = Add(s, "training-crane-samurai", 0, Zone.Home);
        var c = Add(s, "training-crane-samurai", 1, Zone.Home); var d = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        e.DelayEffectForTest(a.Id, DelayedKinds.Destroy, e.DelayUntilOwnersNextTurnBegins(0), playerId: 1);
        e.DelayEffectForTest(b.Id, DelayedKinds.Dishonor, e.DelayUntilOwnersSecondTurnBegins(0), playerId: 1);
        e.DelayEffectForTest(c.Id, DelayedKinds.Dishonor, e.DelayUntilOwnersNextTurnBegins(1), playerId: 0);
        e.DelayEffectForTest(d.Id, DelayedKinds.Dishonor, e.DelayUntilTurn(TurnUntil.GameEnds()), playerId: 1);
        Eq(e.State.DelayedEffects.Count, 4);

        ToAction(e, start + 1); // player 1's turn: only what waited for HIS next turn
        Check(e.State.Cards[c.Id].Dishonorable, "player 1's next turn: his delayed dishonoring was applied");
        Check(e.State.Cards[a.Id].Zone == Zone.Home && !e.State.Cards[b.Id].Dishonorable, "what waits for player 0's turns survives the other player's turn");
        Eq(e.State.DelayedEffects.Count, 3);
        e = MatchEngine.FromTestState(e.State); // through a saved state

        ToAction(e, start + 2); // player 0's next turn
        Check(e.State.Cards[a.Id].Zone != Zone.Home, $"player 0's next turn: the delayed destruction was applied ({e.State.Cards[a.Id].Zone})");
        Check(!e.State.Cards[b.Id].Dishonorable, "his second turn from now has not come");
        ToAction(e, start + 3);
        Check(!e.State.Cards[b.Id].Dishonorable, "nor does the other player's turn bring it");
        ToAction(e, start + 4); // player 0's second turn from then
        Check(e.State.Cards[b.Id].Dishonorable, "player 0's second turn: the delayed dishonoring was applied");
        Check(!e.State.Cards[d.Id].Dishonorable && e.State.DelayedEffects.Count == 1 && e.State.DelayedEffects[0].Until == DelayUntil.GameEnd, "what waits for the game's end is never applied");
    }

    private static void RemovalAndEventExpiry()
    {
        var s = Table(0);
        var guest = Add(s, "training-crane-samurai", 0, Zone.Home); var stays = Add(s, "training-crane-samurai", 0, Zone.Home);
        var ev = Add(s, "training-farm", 0, Zone.EventInPlay); var forever = Add(s, "training-farm", 0, Zone.EventInPlay);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        e.ScheduleTimedRemovalUntil(e.CardForTest(guest.Id), e.UntilOwnersSecondTurnBegins(0), "Training");
        e.ScheduleTimedRemovalUntil(e.CardForTest(stays.Id), TurnUntil.GameEnds(), "Training");
        e.SetEventExpiry(e.CardForTest(ev.Id), e.UntilOwnersNextTurnBegins(0));
        e.CardForTest(forever.Id).ExpiresAtTurnsTaken = 4;
        e.SetEventExpiry(e.CardForTest(forever.Id), TurnUntil.GameEnds());
        Eq(e.State.MechD.TimedRemovals.Count, 1, "\"until the game ends\" schedules no removal");
        Check(e.State.Cards[ev.Id].ExpiresAtTurnsTaken == 4 && e.State.Cards[forever.Id].ExpiresAtTurnsTaken is null, "the Event's expiry is its owner's turn; none for the game's end");
        try { e.SetEventExpiry(e.CardForTest(ev.Id), e.UntilOwnersNextTurnBegins(1)); throw new Exception("an Event may not end on another player's turn"); }
        catch (Exception refused) when (refused.Message.Contains("own owner")) { }

        e = MatchEngine.FromTestState(e.State); // through a saved state
        ToAction(e, start + 1);
        Check(e.State.Cards[ev.Id].Zone == Zone.EventInPlay && e.State.Cards[guest.Id].Zone == Zone.Home, "the other player's turn ends neither");
        ToAction(e, start + 2);
        Check(e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard, "the Event left play as its owner's next turn began");
        Check(e.State.Cards[guest.Id].Zone == Zone.Home, "the removal waits for his second turn");
        ToAction(e, start + 4);
        Check(e.State.Cards[guest.Id].Zone == Zone.RemovedFromGame, $"removed as his second turn from then began ({e.State.Cards[guest.Id].Zone})");
        Check(e.State.Cards[stays.Id].Zone == Zone.Home && e.State.Cards[forever.Id].Zone == Zone.EventInPlay, "\"until the game ends\": both are still there");
    }

    // ---- 3. player-level granted abilities ---------------------------------------------------------------------------------------------------------------------------------------

    private static GameCommand UseGranted(MatchEngine e, int player) => new("useAbility", player, e.Version, AbilityId: GrantedOpen);

    private static void GrantedAbilityLifecycle()
    {
        var s = Table(0); PublishStrongholds(s);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        Check(!Offered(e, 0, GrantedOpen), "dormant: not offered before it is granted");
        Reject(e, UseGranted(e, 0));
        Check(Batch336Support.Rejection(e, UseGranted(e, 0)).Contains("do not have that ability"), "and refused by name");

        e.GrantPlayerAbility(0, GrantedOpen, e.UntilOwnersNextTurnBegins(0), "Training");
        Check(e.PlayerAbilityGranted(0, GrantedOpen) && !e.PlayerAbilityGranted(1, GrantedOpen), "granted to player 0 only");
        Check(Offered(e, 0, GrantedOpen), "offered to him through the action list");
        var asOther = Edit(e, st => st.PriorityPlayerId = 1);
        Check(!Offered(asOther, 1, GrantedOpen), "not offered to the other player at his opportunity");
        Reject(asOther, UseGranted(asOther, 1));
        Check(e.State.Events.Any(x => x.Type == "playerAbilityGranted"), "the grant is logged");

        Accept(e, e.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == GrantedOpen).Command!);
        Check(e.State.Players[0].AbilityUses.Count > 0 || e.State.Events.Any(x => x.PlayerId == 0), "he used it");

        // Player 1's turn: the grant still stands, and (an Open ability) is offered to player 0 at his opportunity there.
        ToAction(e, start + 1);
        Check(e.PlayerAbilityGranted(0, GrantedOpen), "it lasts through the other player's turn");
        Check(Offered(Edit(e, st => st.PriorityPlayerId = 0), 0, GrantedOpen), "and is offered there");
        // His next turn begins: gone.
        ToAction(e, start + 2);
        Check(!e.PlayerAbilityGranted(0, GrantedOpen) && e.State.PlayerAbilityGrants.Count == 0, "removed as his next turn began");
        Check(!Offered(e, 0, GrantedOpen), "and no longer offered");
        Reject(e, UseGranted(e, 0));
        Check(e.State.Events.Any(x => x.Type == "playerAbilityExpired"), "the end is logged");
    }

    private static void GrantedAbilityEachAndCard()
    {
        var s = Table(0); PublishStrongholds(s);
        var bond = Add(s, "training-farm", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        e.GrantEachPlayerAbility(GrantedOpen, TurnUntil.GameEnds(), "Training Event");
        Eq(e.State.PlayerAbilityGrants.Count, 2, "one grant per player");
        Check(Offered(e, 0, GrantedOpen) && Offered(Edit(e, st => st.PriorityPlayerId = 1), 1, GrantedOpen), "each player is offered it");
        e = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        ToAction(e, start + 4);
        Eq(e.State.PlayerAbilityGrants.Count, 2, "four turns later both still hold it");
        Check(Offered(e, 0, GrantedOpen) && Offered(Edit(e, st => st.PriorityPlayerId = 1), 1, GrantedOpen), "and are still offered it");
        Check(e.RevokePlayerAbility(1, GrantedOpen) && !e.PlayerAbilityGranted(1, GrantedOpen) && e.PlayerAbilityGranted(0, GrantedOpen), "a grant is removed per player");

        // "While this card remains in play, his controller has the ability": the grant ends with the card, at once, and its record is cleared as the next turn begins.
        e.RevokePlayerAbility(0, GrantedOpen);
        e.GrantPlayerAbility(0, GrantedOpen, TurnUntil.GameEnds(), "Training bond", whileInPlay: bond.Id);
        Check(Offered(e, 0, GrantedOpen), "held while the card is in play");
        // The same source granting again replaces its own grant; another source stands beside it.
        e.GrantPlayerAbility(0, GrantedOpen, TurnUntil.GameEnds(), "Training bond", whileInPlay: bond.Id);
        Eq(e.State.PlayerAbilityGrants.Count, 1, "one source, one grant");
        var gone = Edit(e, st => st.Cards[bond.Id].Zone = Zone.DynastyDiscard);
        Check(!gone.PlayerAbilityGranted(0, GrantedOpen) && !Offered(gone, 0, GrantedOpen), "gone the moment the card leaves play");
        Reject(gone, UseGranted(gone, 0));
        ToAction(gone, gone.State.TurnNumber + 1);
        Eq(gone.State.PlayerAbilityGrants.Count, 0, "and its record is cleared as the next turn begins");
        // Only a registered player ability may be granted to a player.
        try { e.GrantPlayerAbility(0, "kharmic-discard", TurnUntil.GameEnds(), "Training"); throw new Exception("an ungranted player ability was granted"); }
        catch (Exception refused) when (refused.Message.Contains("registered as granted")) { }
    }

    // ---- 4. standing rules ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Drawer(MatchState s, int owner)
    {
        var card = Add(s, "training-farm", owner, Zone.Home);
        card.GrantedAbilities.Add(new GrantedAbility(LimitedDraw, "test-fixture", GrantDuration.Permanent));
        return card;
    }

    private static void EndTurnExtraDraw()
    {
        var s = Table(0);
        var drawer = Drawer(s, 1);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        Check(e.State.Players.All(p => p.Hand.Count == 0 && p.FateDeck.Count >= 6), "empty hands and cards to draw");
        e.AddTurnScopedRule(TurnRuleKinds.EndTurnExtraDraw, null, TurnUntil.GameEnds(), "Training Hostilities", amount: 1, counter: TurnCounters.CardsIntoHand, max: 0);
        ToAction(e, start + 1);
        Eq(e.State.Players[0].Hand.Count, 2, "no other card went into his hand: two cards instead of one");
        Check(e.State.Events.Any(x => x.Type == "turnRuleDraw" && x.PlayerId == 0), "logged as the rule's draw");
        // Player 1 draws a card with an action during his turn: one card at the end of it.
        Accept(e, Batch336Support.Enabled(e, 1, LimitedDraw, drawer.Id).Command!);
        Eq(e.State.Players[1].Hand.Count, 1);
        Eq(e.TurnCountOf(TurnCounters.CardsIntoHand, 1), 1, "the per-turn counter saw the draw");
        e = MatchEngine.FromTestState(e.State); // through a saved state: the counter and the rule persist
        ToAction(e, start + 2);
        Eq(e.State.Players[1].Hand.Count, 2, "he put another card in his hand that turn: only the End Phase's own draw");
        Eq(e.TurnCountOf(TurnCounters.CardsIntoHand, 1), 0, "the counter is the turn's");
        ToAction(e, start + 3);
        Eq(e.State.Players[0].Hand.Count, 4, "and the next undisturbed turn draws two again");
        Eq(e.State.TurnScopedRules.Count, 1, "until the game ends: the rule stands");

        // A rule for one player, unconditional, until his next turn begins.
        var one = MatchEngine.FromTestState(Table(0));
        one.AddTurnScopedRule(TurnRuleKinds.EndTurnExtraDraw, 0, one.UntilOwnersNextTurnBegins(0), "Training", amount: 2);
        ToAction(one, start + 1);
        Eq(one.State.Players[0].Hand.Count, 3, "his turn: the End Phase's card and two more");
        ToAction(one, start + 2);
        Eq(one.State.Players[1].Hand.Count, 1, "not the other player's rule");
        Eq(one.State.TurnScopedRules.Count, 0, "it ended as his next turn began");
        Check(one.State.Events.Any(x => x.Type == "turnRuleEnded"), "logged");
        ToAction(one, start + 3);
        Eq(one.State.Players[0].Hand.Count, 4, "and his next turn's end draws one");
    }

    private static CardInstance LimitedStrategy(MatchState s, int owner)
    {
        var card = Add(s, "training-duel-open", owner, Zone.Hand);
        card.Printed = CardCatalog.Get("training-duel-open") with { Abilities = [LimitedDraw] };
        return card;
    }

    private static void LimitedAsOpen()
    {
        var s = Table(1); s.PriorityPlayerId = 0; // player 1's turn, player 0's opportunity
        var first = LimitedStrategy(s, 0); var second = LimitedStrategy(s, 0); var holding = Drawer(s, 0);
        var theirs = LimitedStrategy(s, 1);
        var bare = MatchEngine.FromTestState(s);
        Check(!Offered(bare, 0, LimitedDraw), "without the rule a Limited action is not offered on another player's turn");
        Check(Batch336Support.Rejection(bare, Batch336Support.Use(bare, 0, LimitedDraw, first.Id)).Contains("Limited timing is unavailable"), "and is refused");

        var e = MatchEngine.FromTestState(s);
        var rule = e.AddTurnScopedRule(TurnRuleKinds.LimitedAsOpen, 0, e.UntilOwnersSecondTurnBegins(0), "Training Madness", amount: 1, arg: "Strategy");
        Check(Offered(e, 0, LimitedDraw, first.Id) && Offered(e, 0, LimitedDraw, second.Id), "the Limited action of each Strategy in his hand is offered as an Open action");
        Check(!Offered(e, 0, LimitedDraw, holding.Id), "\"on a Strategy\": his Holding's Limited ability is not");
        Reject(e, Batch336Support.Use(e, 0, LimitedDraw, holding.Id));
        var saved = MatchEngine.FromTestState(e.State);
        Accept(e, Batch336Support.Use(e, 0, LimitedDraw, first.Id));
        Eq(e.State.Players[0].Hand.Count(id => id != second.Id), 1, "the action resolved: he drew a card");
        Check(e.State.Events.Any(x => x.Type == "turnRuleUsed"), "the use is logged");
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!Offered(again, 0, LimitedDraw, second.Id), "once per turn: the second Strategy is not offered this turn");
        Reject(again, Batch336Support.Use(again, 0, LimitedDraw, second.Id));
        Accept(saved, Batch336Support.Use(saved, 0, LimitedDraw, second.Id)); // the saved state before the use: the rule was stored whole
        var nextTurn = Edit(again, st => st.TurnNumber++);
        Check(Offered(nextTurn, 0, LimitedDraw, second.Id), "the next turn restores it");
        var expired = Edit(nextTurn, st => st.Players[0].TurnsTaken += 2);
        Check(!Offered(expired, 0, LimitedDraw, second.Id), "its duration over, the rule allows nothing");
        // The other player's turn is his own: the rule is player 0's, and player 1 needs none for his own Limited actions.
        var forOther = Edit(e, st => { st.PriorityPlayerId = 1; st.TurnScopedRules.Clear(); });
        Check(Offered(forOther, 1, LimitedDraw, theirs.Id), "the active player's Limited actions are his as ever");
        var hisTurn = Edit(e, st => { st.ActivePlayerId = 0; st.PriorityPlayerId = 1; });
        Check(!Offered(hisTurn, 1, LimitedDraw, theirs.Id), "and the rule gives the other player nothing");

        // Plans Within Plans is asked first: the action it covers spends its own opportunity, not the rule's use.
        var plans = MatchEngine.FromTestState(s);
        plans.AddTurnScopedRule(TurnRuleKinds.LimitedAsOpen, 0, plans.UntilOwnersSecondTurnBegins(0), "Training Madness", amount: 1, arg: "Strategy");
        plans = Edit(plans, st => st.Players[0].PlansWithinPlansArmed = true);
        Check(Offered(plans, 0, LimitedDraw, holding.Id), "Plans Within Plans still allows any Limited action");
        Accept(plans, Batch336Support.Use(plans, 0, LimitedDraw, first.Id));
        Check(!plans.State.Players[0].PlansWithinPlansArmed && !plans.State.Events.Any(x => x.Type == "turnRuleUsed"), "its opportunity was spent, the rule's use was not");
        Check(Offered(Edit(plans, st => st.PriorityPlayerId = 0), 0, LimitedDraw, second.Id), "so the rule still allows the second Strategy this turn");
        Eq(rule.Kind, TurnRuleKinds.LimitedAsOpen);
    }

    // ---- the stores and the registry ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void RoundTripAndRegistry()
    {
        var e = MatchEngine.FromTestState(Table(0));
        e.GrantPlayerAbility(1, GrantedOpen, e.UntilOwnersSecondTurnBegins(1), "Training", whileInPlay: e.State.Players[1].StrongholdId);
        e.AddTurnScopedRule("training-p4-custom", null, e.UntilTurnBegins(1), "Training", amount: 3, counter: "x", max: 2, arg: "y", cardId: e.State.Players[0].StrongholdId);
        var state = e.State;
        state.PendingTurnBegins = new PendingTurnBegins { PlayerId = 1, TurnNumber = 7, HookId = "TestP4Choice", ChoiceKey = ChoiceKey, ChooserId = 1, SourceCardId = "x", Options = [.. ChoiceOptions(1)] };
        var json = JsonSerializer.Serialize(state);
        var back = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(json)!).State;
        Eq(JsonSerializer.Serialize(back), json, "MatchEngine.Copy keeps every field of the three stores");
        Check(back.PlayerAbilityGrants.Single() is { PlayerId: 1, AbilityId: GrantedOpen, Source: "Training", Until: { Kind: TurnUntil.TurnBegins, Owner: 1, At: 5 } } grant && grant.WhileInPlay == state.Players[1].StrongholdId, "the grant");
        Check(back.TurnScopedRules.Single() is { Id: 1, Kind: "training-p4-custom", PlayerId: null, Amount: 3, Counter: "x", Max: 2, Arg: "y", Until: { Owner: null } } && back.TurnScopedRuleSequence == 1, "the rule");
        Check(back.PendingTurnBegins is { HookId: "TestP4Choice", ChoiceKey: ChoiceKey } pending && pending.Options.Count == 3 && pending.Options[0] == ChoiceOptions(1)[0], "the pending sequence");
        Check(e.TurnScopedRulesFor("training-p4-custom", 0).Count() == 1 && e.TurnScopedRulesFor("training-p4-custom").Count() == 1 && !e.TurnScopedRulesFor("no-such-kind").Any(), "a feature reads its own kind");
        Check(e.RemoveTurnScopedRule(1) && e.State.TurnScopedRules.Count == 0, "and removes it");
        // A state saved before P4 has none of the properties: it loads with empty stores.
        var old = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(Table(0)).Replace("\"PlayerAbilityGrants\":[],", "").Replace("\"TurnScopedRules\":[],", ""))!;
        Check(old.PlayerAbilityGrants.Count == 0 && old.TurnScopedRules.Count == 0 && old.PendingTurnBegins is null, "an older save loads");

        Check(MatchEngine.SeppukuDecisionStateNames.Contains(nameof(MatchState.PendingTurnBegins)), "the pending sequence is a registered decision state");
        var names = EngineHooks.TurnBegins.Names;
        Check(names.SequenceEqual(names.Where(n => !n.StartsWith("TestP4", StringComparison.Ordinal)).Concat(["TestP4Scopes", "TestP4Choice", "TestP4Late"])), "the fixture callbacks come last, in priority order: " + string.Join(",", names));
        try { EngineHooks.TurnBegins.Register("TestP4TooLate", 19999, static (_, _) => false); throw new Exception("the channel accepted a registration after startup"); }
        catch (InvalidOperationException) { }
        Check(EngineHooks.PendingChoice.Names.Contains("P4HandleTurnBeginChoice") && EngineHooks.ValidateEarly.Names.Contains("P4ValidatePlayerGrant"), "the feature's own registrations");
    }
}
