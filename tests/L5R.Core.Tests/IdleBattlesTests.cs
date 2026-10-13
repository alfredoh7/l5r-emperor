using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #894 (Mendon): "Frequently, battles are fought with no personalities on either side." The engine makes the decisions that are not decisions: an empty battle in which
// the player holding the priority has nothing to do but pass is passed for them, and the one battlefield left to choose is chosen. Engines built from a test state leave this
// off (their scripts place units and answer every window), so each test here switches it on.
internal static class IdleBattlesTests
{
    public static void Run(TestRunner runner) => runner.Run("idle-battles", [
        ("An empty battle with nothing to play is resolved by the Attacker's selection alone: no passes are asked for", EmptyBattleResolvesFromTheSelection),
        ("The last battlefield is chosen for the player choosing, so the Attack Phase ends with the selection before it", LastBattlefieldIsChosen),
        ("A player with something to play in an empty battle keeps it: the battle waits for them, and resolves when they pass", PlayableActionKeepsTheBattleWaiting),
        ("A battle with a unit on the battlefield stays manual", UnitsKeepTheBattleManual),
        ("A decision or window that is open is never answered by the engine", OpenDecisionIsNotAnswered),
        ("An engine built from a test state does nothing unless the test asks", OffForTestStates),
        ("Between two human players the pass in an empty battle is the player's own: the last battlefield is still chosen, but nobody is passed for", HumansPassForThemselves),
        ("A human who has taken Pass all battles is passed for in an empty battle, with or without the no-human-opponent flag", FlaggedHumanIsPassedFor),
        ("The no-human-opponent flag is part of the replay, so a replay reaches the same state", FlagIsReplayed),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // Player 0 attacks (BattleSelection), no units anywhere, no cards in hand.
    private static MatchState Table(bool idle, params int[] battlefields) => Table(idle, idle, battlefields);

    // `passes`: a match with no human opponent (IdleBattlePasses); `idle` alone is the human-vs-human case.
    private static MatchState Table(bool idle, bool passes, int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0, 1, 2] : battlefields).ToList();
        if (idle) s.IdleBattleSkips = true;
        if (passes) s.IdleBattlePasses = true;
        return s;
    }

    private static int Resolved(CommandResult r) => r.Events.Count(x => x.Type == "battleResolved");
    private static bool HasUsable(MatchEngine e, int player) => e.QueryActions(player).Any(a => a.Enabled && a.Type is not ("pass" or "passAllBattles" or "declineReaction" or "wait" or "concede" or "selectBattle"));

    private static void EmptyBattleResolvesFromTheSelection()
    {
        var e = MatchEngine.FromTestState(Table(true));
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 0));
        Check(result.Accepted, result.Error ?? "");
        Eq(Resolved(result), 1, "the battle was fought, not skipped");
        Eq(e.State.Phase, Phase.BattleSelection, "and the Attacker is choosing the next one");
        Check(e.State.UnresolvedBattlefields.OrderBy(x => x).SequenceEqual([1, 2]), "battlefield 0 is done");
        Eq(e.State.PriorityPlayerId, 0, "the Attacker chooses");
        Check(result.Events.Count(x => x.Type == "pass") >= 2 || e.State.Events.Count(x => x.Message.Contains("pass", StringComparison.OrdinalIgnoreCase)) >= 2, "both players' passes were made for them");
    }

    private static void LastBattlefieldIsChosen()
    {
        var e = MatchEngine.FromTestState(Table(true, 1, 2));
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 1));
        Check(result.Accepted, result.Error ?? "");
        Eq(Resolved(result), 2, "the chosen battle and the last one, which was chosen for them");
        Check(e.State.Phase is not (Phase.Battle or Phase.BattleSelection), $"the Attack Phase is over ({e.State.Phase})");
        Eq(e.State.UnresolvedBattlefields.Count, 0);
    }

    private static void PlayableActionKeepsTheBattleWaiting()
    {
        // A move-to-battle action can be played at a battlefield with no unit there (BattleAbsentMovesTests): the Defender, first to act, has something to do, so the battle waits for them.
        var s = Table(true, 0, 1);
        Add(s, "training-crane-samurai", 1, Zone.Home); Add(s, "training-move-battle-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 0));
        Check(result.Accepted, result.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "the battle waits: the Defender has a move to play");
        Eq(e.State.PriorityPlayerId, 1, "the Defender holds the first opportunity and is waited for");
        Check(HasUsable(e, 1), "because they have something to play");
        Eq(Resolved(result), 0, "nothing resolved yet");
        var pass = e.Execute(new("pass", 1, e.Version));
        Check(pass.Accepted, pass.Error ?? "");
        Eq(Resolved(pass), 1, "their pass was the last thing the battle waited for: the Attacker had nothing and was passed for");
    }

    private static void UnitsKeepTheBattleManual()
    {
        var s = Table(true, 0, 1);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 0));
        Check(result.Accepted, result.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "a battle with a unit waits for its players");
        Eq(Resolved(result), 0);
        Eq(e.State.PriorityPlayerId, 1, "the Defender acts first");
    }

    private static void OpenDecisionIsNotAnswered()
    {
        var s = Table(true, 0);
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 1;
        s.PendingBrashDraws = 1; s.PendingChoice = new("brashDraw", 1, null, "Draw a card for Brash?");
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(1).Any(a => a.Enabled && a.Type != "concede"), "the player has the choice to make");
        var result = e.Execute(new("pass", 0, e.Version)); // not the player holding the priority: refused, and nothing is passed for anyone
        Check(!result.Accepted, "an out-of-turn command is still refused");
        Eq(e.State.Phase, Phase.Battle);
        Check(e.State.PendingChoice is not null, "the open decision is untouched");
    }

    private static void OffForTestStates()
    {
        var e = MatchEngine.FromTestState(Table(false));
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 0));
        Check(result.Accepted, result.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "left as a script wrote it");
        Eq(Resolved(result), 0);
    }

    private static void HumansPassForThemselves()
    {
        var e = MatchEngine.FromTestState(Table(true, passes: false, [0, 1, 2]));
        var result = e.Execute(new("selectBattle", 0, e.Version, Province: 0));
        Check(result.Accepted, result.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "the battle waits for the Defender's own pass");
        Eq(e.State.PriorityPlayerId, 1);
        Eq(Resolved(result), 0);
        var pass = e.Execute(new("pass", 1, e.Version)); Check(pass.Accepted, pass.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "and for the Attacker's");
        Eq(e.State.PriorityPlayerId, 0);
        var last = e.Execute(new("pass", 0, e.Version)); Check(last.Accepted, last.Error ?? "");
        Eq(Resolved(last), 1, "two passes resolve it");
        // Two battlefields left: the Attacker chooses; one left after that: chosen for them.
        var again = MatchEngine.FromTestState(Table(true, passes: false, [1, 2]));
        var second = again.Execute(new("selectBattle", 0, again.Version, Province: 1)); Check(second.Accepted, second.Error ?? "");
        Eq(again.State.Phase, Phase.Battle);
        var lone = MatchEngine.FromTestState(Table(true, passes: false, [2]));
        Eq(lone.State.Phase, Phase.BattleSelection);
        var chosen = lone.Execute(new("selectBattle", 0, lone.Version, Province: 2)); Check(chosen.Accepted, chosen.Error ?? "");
        Eq(lone.State.Phase, Phase.Battle, "a human still takes their own passes in the last battle");
    }

    private static void FlaggedHumanIsPassedFor()
    {
        var e = MatchEngine.FromTestState(Table(true, passes: false, [0, 1]));
        var flag = e.Execute(new("passAllBattles", 0, e.Version)); Check(flag.Accepted, flag.Error ?? "");
        Eq(e.State.Phase, Phase.Battle, "the Attacker's flag chose the first battle; the Defender is not flagged and passes for themselves");
        var defenderPasses = 0;
        while (e.State.Phase == Phase.Battle && defenderPasses < 5)
        {
            Eq(e.State.PriorityPlayerId, 1, "only the unflagged Defender is ever asked");
            var pass = e.Execute(new("pass", 1, e.Version)); Check(pass.Accepted, pass.Error ?? ""); defenderPasses++;
        }
        Eq(defenderPasses, 2, "one pass per battle, the Attacker's own passes and the choosing of the second battle made for them");
        Check(e.State.Phase is not (Phase.Battle or Phase.BattleSelection), "the flagged Attacker was passed for in every battle");
    }

    private static void FlagIsReplayed()
    {
        var e = MatchEngine.CreateStarterMatch(31); e.AllowIdleBattlePasses();
        var replay = e.ExportReplay();
        Check(replay.IdleBattlePasses, "the flag is exported");
        var json = System.Text.Json.JsonSerializer.Deserialize<ReplayRecord>(System.Text.Json.JsonSerializer.Serialize(replay))!;
        Check(json.IdleBattlePasses, "and survives the journal");
        var back = MatchEngine.Replay(json);
        Eq(System.Text.Json.JsonSerializer.Serialize(back.State), System.Text.Json.JsonSerializer.Serialize(e.State), "the replay reaches the same state");
        Check(!MatchEngine.CreateStarterMatch(31).ExportReplay().IdleBattlePasses, "a match that did not ask exports false");
    }
}
