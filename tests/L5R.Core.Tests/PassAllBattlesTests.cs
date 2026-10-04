using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #628: "Pass all battles". A button for either player while battles are being fought that passes their next action in every remaining battle; as
// Attacker it also chooses the battlefields, lowest first; an action by the other player in a battle ends it; both players taking it resolves all that is left.
internal static class PassAllBattlesTests
{
    public static void Run(TestRunner runner) => runner.Run("pass-all-battles", [
        ("Pass all battles is offered to the player holding the priority in a battle and to the Attacker choosing a battle -- not in the Action Phase, not while a decision is open", Offered),
        ("As Attacker: taking it chooses the lowest remaining battlefield, then the next lowest, and so on; the Defender's own passes are theirs to make", AttackerChoosesLowestFirst),
        ("As Defender: the engine passes for them in every battle, so the Attacker's pass resolves each battle and the next is chosen without asking them", DefenderPassesForThem),
        ("An action taken by the other player in a battle ends the pass: the player must choose Pass all battles again, and may", OpponentActionInterrupts),
        ("Both players taking it resolves every remaining battle and ends the Attack Phase", BothResolveEverything),
        ("The passes the engine makes are not commands of their own: the replay holds only what the players chose", ReplayHoldsOnlyChosenCommands),
        ("The flags end with the Attack Phase", FlagsEndWithTheAttackPhase),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // Player 0 attacks (BattleSelection), three battlefields, no units anywhere: every battle is a plain empty one.
    private static MatchState Table(params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0, 1, 2] : battlefields).ToList();
        return s;
    }

    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static bool Has(MatchEngine e, int player, string type) => e.QueryActions(player).Any(a => a.Type == type && a.Enabled);
    private static int Resolved(CommandResult r) => r.Events.Count(x => x.Type == "battleResolved");

    private static void Offered()
    {
        var e = MatchEngine.FromTestState(Table());
        Check(Has(e, 0, "passAllBattles"), "the Attacker choosing a battle");
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.Phase, Phase.Battle);
        Eq(e.State.PriorityPlayerId, 1, "the Defender acts first");
        Check(Has(e, 1, "passAllBattles"), "the Defender holding the priority in a battle");
        Check(Has(e, 1, "pass"), "and the ordinary pass is still there");
        var action = Table(); action.Phase = Phase.Action;
        Check(!Has(MatchEngine.FromTestState(action), 0, "passAllBattles"), "not in the Action Phase");
        var declaration = Table(); declaration.Phase = Phase.AttackDeclaration;
        Check(!Has(MatchEngine.FromTestState(declaration), 0, "passAllBattles"), "not while declaring an attack");
        var pending = Table(); pending.Phase = Phase.Battle; pending.CurrentBattleProvince = 0; pending.UnresolvedBattlefields = [0]; pending.PendingBrashDraws = 1; pending.PendingChoice = new("brashDraw", 0, null, "Brash: you may draw a card.");
        Check(!Has(MatchEngine.FromTestState(pending), 0, "passAllBattles"), "not while a decision is open");
        Reject(MatchEngine.FromTestState(action), new("passAllBattles", 0, 0));
    }

    private static void AttackerChoosesLowestFirst()
    {
        var e = MatchEngine.FromTestState(Table(2, 0, 1));
        Accept(e, Cmd(e, "passAllBattles"));
        Eq(e.State.CurrentBattleProvince, 0, "the lowest remaining battlefield is chosen");
        Eq(e.State.Phase, Phase.Battle);
        Eq(e.State.PriorityPlayerId, 1, "the Defender holds the first opportunity and is not passed for");
        Check(e.State.PassAllBattles[0] && !e.State.PassAllBattles[1], "only the Attacker's flag is raised");
        var order = new List<int> { 0 };
        for (var i = 0; i < 3 && e.State.Phase == Phase.Battle; i++)
        {
            var before = e.State.UnresolvedBattlefields.Count;
            var result = e.Execute(Cmd(e, "pass")); // the Defender passes; the Attacker's pass is made for them, and the battle resolves
            Check(result.Accepted, result.Error ?? "");
            Eq(Resolved(result), 1, "one battle resolved");
            Eq(e.State.UnresolvedBattlefields.Count, before - 1);
            if (e.State.Phase == Phase.Battle) order.Add(e.State.CurrentBattleProvince!.Value);
        }
        Eq(string.Join(",", order), "0,1,2", "lowest to highest");
        Check(e.State.Phase != Phase.Battle && e.State.Phase != Phase.BattleSelection, "the last battle ended the Attack Phase");
    }

    private static void DefenderPassesForThem()
    {
        var e = MatchEngine.FromTestState(Table());
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Accept(e, Cmd(e, "passAllBattles")); // the Defender: passes now and in every battle
        Eq(e.State.PriorityPlayerId, 0, "their pass went to the Attacker");
        Check(e.State.PassAllBattles[1]);
        var result = e.Execute(Cmd(e, "pass")); // the Attacker passes: the battle resolves
        Check(result.Accepted, result.Error ?? "");
        Eq(Resolved(result), 1);
        Eq(e.State.Phase, Phase.BattleSelection);
        Check(e.State.PassAllBattles[1], "the pass stands for the next battle");
        Accept(e, new("selectBattle", 0, e.Version, Province: 1));
        // The Defender's first opportunity was passed for them: the Attacker holds the priority at once.
        Eq(e.State.Phase, Phase.Battle);
        Eq(e.State.PriorityPlayerId, 0, "the Defender's opportunity was passed for them");
        Eq(e.State.ConsecutivePasses, 1);
        result = e.Execute(Cmd(e, "pass"));
        Check(result.Accepted, result.Error ?? "");
        Eq(Resolved(result), 1);
        Eq(e.State.Phase, Phase.BattleSelection);
        Accept(e, new("selectBattle", 0, e.Version, Province: 2));
        Accept(e, Cmd(e, "pass"));
        Check(e.State.Phase is not (Phase.Battle or Phase.BattleSelection), "the third battle ended the Attack Phase");
        Check(!e.State.PassAllBattles[1], "and the flag with it");
    }

    private static void OpponentActionInterrupts()
    {
        var s = Table(0, 1);
        var unit = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-performer-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Settle(e);
        Eq(e.State.Phase, Phase.Battle);
        Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Cmd(e, "passAllBattles", 1));
        Eq(e.State.PriorityPlayerId, 0);
        Check(e.State.PassAllBattles[1]);
        Accept(e, Enabled(e, 0, "training-performer-boost", strategy.Id, unit.Id).Command!);
        Check(!e.State.PassAllBattles[1], "the Attacker's action ended the Defender's pass");
        Eq(e.State.PriorityPlayerId, 1, "the Defender decides for themself");
        Check(e.State.Events.Any(x => x.Type == "passAllBattlesInterrupted"), "it is said");
        Check(Has(e, 1, "passAllBattles"), "and may be chosen again");
        Accept(e, Cmd(e, "passAllBattles", 1));
        Check(e.State.PassAllBattles[1]);
    }

    private static void BothResolveEverything()
    {
        var e = MatchEngine.FromTestState(Table());
        Accept(e, Cmd(e, "passAllBattles")); // Attacker: battle 0 chosen; the Defender to act
        Eq(e.State.CurrentBattleProvince, 0);
        var result = e.Execute(Cmd(e, "passAllBattles")); // Defender takes it too
        Check(result.Accepted, result.Error ?? "");
        Eq(Resolved(result), 3, "every remaining battle resolved");
        Check(e.State.Phase is not (Phase.Battle or Phase.BattleSelection), "the Attack Phase ended");
        Eq(e.State.UnresolvedBattlefields.Count, 0);
        Check(!e.State.PassAllBattles[0] && !e.State.PassAllBattles[1], "no flag outlives the phase");
    }

    private static void ReplayHoldsOnlyChosenCommands()
    {
        var e = MatchEngine.FromTestState(Table());
        var before = e.ExportReplay().Commands.Length;
        Accept(e, Cmd(e, "passAllBattles"));
        Accept(e, Cmd(e, "passAllBattles"));
        var commands = e.ExportReplay().Commands.Skip(before).ToArray();
        Eq(commands.Length, 2, "two chosen commands");
        Check(commands.All(c => c.Type == "passAllBattles"), "and nothing the engine did for them");
    }

    private static void FlagsEndWithTheAttackPhase()
    {
        var e = MatchEngine.FromTestState(Table(0));
        Accept(e, Cmd(e, "passAllBattles"));
        Accept(e, Cmd(e, "pass")); // the Defender passes; the one battle resolves
        Check(e.State.Phase is not (Phase.Battle or Phase.BattleSelection));
        Check(!e.State.PassAllBattles[0] && !e.State.PassAllBattles[1]);
    }

    private static void Settle(MatchEngine e)
    {
        // An Engage window opens only when something could be played in it; none here, but be tolerant of one.
        for (var i = 0; i < 4 && e.State.PendingChoice is { Type: "engageReaction" }; i++) Accept(e, Cmd(e, "declineReaction"));
    }
}
