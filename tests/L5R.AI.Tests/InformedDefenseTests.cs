using L5R.AI;
using L5R.Core;

// Issue #963 (bot loop, round 2): the defense goes where it wins or saves a Province, and stays home where the Province stands anyway. See TrainingOpponent.Defense.cs.
internal static class InformedDefenseTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Informed defense: it defends where it wins or saves a Province, picks that battlefield over the largest army, and keeps its units home where the Province stands anyway", Decision),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    /// <summary>Own defenders (Force <paramref name="own"/>) at home; one attacking army per entry of <paramref name="attackers"/> (province index, Force).</summary>
    private static MatchView Table(int own, params (int Province, int Force)[] attackers)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var home = new CardView("defender", BattlePolicyTests.Person(), 0, Zone.Home, true, false, null, null, own);
        var army = attackers.Select((a, i) => new CardView("enemy" + i, BattlePolicyTests.Person(), 1, Zone.Battlefield, true, false, a.Province, null, a.Force)).ToArray();
        return view with
        {
            Phase = Phase.DefenseAssignment,
            PendingChoice = null,
            ActivePlayerId = 1,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Home = [home], Army = [] }, view.Players[1] with { Home = [], Army = army }]
        };
    }

    private static (string? Type, Dictionary<string, int>? Assignments) Choose(MatchView v, bool informed)
    {
        var provinces = v.Players[0].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        ActionOption defend = new("assignDefense", "Assign defenders", true, null, new("assignDefense", 0, v.Version, Assignments: []), null, ["defender"], provinces);
        var decision = TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), defend], new BotContext { UseInformedDefense = informed });
        return (decision.Command?.Type, decision.Command?.Assignments);
    }

    private static void Decision()
    {
        var strength = MatchEngine.CreateStarterMatch().GetView(0).Players[0].Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).Min();
        // Wins outright where the stronger of two armies would be beaten: attackers at 0 (Force 3) and at 1 (Force 5), defenders 4 beat only the first.
        var two = Table(4, (0, 3), (1, strength + 20));
        var picked = Choose(two, informed: true);
        Check(picked.Type == "assignDefense" && picked.Assignments!["defender"] == 0, "defenders of 4 go where they win (the army of 3), not to the army they cannot beat: " + picked.Type + " " + string.Join(",", picked.Assignments?.Values.Select(v => v.ToString()) ?? []));
        // Saves a Province: the attackers beat the defenders (Force 5 against 3) but not the defenders plus the Province's Strength.
        var save = Table(3, (0, strength + 2));
        Check(Choose(save, informed: true).Type == "assignDefense", "defenders of 3 against Strength+2 save the Province: it defends");
        // The Province stands anyway (attackers no stronger than its Strength) and the defenders could not win: they stay home with the policy on.
        var stands = Table(strength - 1 > 1 ? strength - 1 : 1, (0, strength));
        var onStands = Choose(stands, informed: true); var offStands = Choose(stands, informed: false);
        Check(onStands.Type == "pass", "the Province stands anyway and the defenders cannot win: they stay home: " + onStands.Type);
        Check(offStands.Type == "assignDefense", "policy off: unchanged behaviour, it defends: " + offStands.Type);
    }
}
