using L5R.AI;
using L5R.Core;

// Issue #963: the archetype flags (S1-S4), split defense (hypothesis A) and military trades (hypothesis B). Each flag is default off; these prove the one decision each changes.
internal static class ArchetypePlayTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Split defense: two defenders that each beat a different attacking army go to different Provinces, and with the flag off both go to one", Split),
        ("Archetype switch: an honor-route deck at 28 Honor chooses to gain it, a dishonor deck presses the opponent, and with the flag off the Honor tier decides", Switch),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchView Table(int[] own, params (int Province, int Force)[] attackers)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var home = own.Select((f, i) => new CardView("defender" + i, BattlePolicyTests.Person(), 0, Zone.Home, true, false, null, null, f)).ToArray();
        var army = attackers.Select((a, i) => new CardView("enemy" + i, BattlePolicyTests.Person(), 1, Zone.Battlefield, true, false, a.Province, null, a.Force)).ToArray();
        return view with
        {
            Phase = Phase.DefenseAssignment,
            PendingChoice = null,
            ActivePlayerId = 1,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Home = home, Army = [] }, view.Players[1] with { Home = [], Army = army }]
        };
    }

    private static Dictionary<string, int>? Defend(MatchView v, bool split)
    {
        var provinces = v.Players[0].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var ids = v.Players[0].Home.Select(c => c.Id).ToArray();
        ActionOption defend = new("assignDefense", "Assign defenders", true, null, new("assignDefense", 0, v.Version, Assignments: []), null, ids, provinces);
        return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), defend], new BotContext { UseInformedDefense = true, UseSplitDefense = split }).Command?.Assignments;
    }

    private static void Split()
    {
        // Attackers of 3 at Province 0 and 4 at Province 1; defenders of 4 and 5 each beat one army, and together (9) beat either.
        var table = Table([4, 5], (0, 3), (1, 4));
        var on = Defend(table, split: true);
        Check(on is { Count: 2 } && on["defender0"] == 0 && on["defender1"] == 1, "defender of 4 beats the 3 at Province 0, defender of 5 beats the 4 at Province 1: " + (on is null ? "none" : string.Join(",", on)));
        var off = Defend(table, split: false);
        Check(off is { Count: > 0 } && off.Values.Distinct().Count() == 1, "flag off: every defender goes to one battlefield: " + (off is null ? "none" : string.Join(",", off)));
    }

    private static void Switch()
    {
        // The switch reads the bot's own Honor and the opponent's, both in the public view. Compare a high-Honor honor-route deck with the same table switched off.
        static bool Gains(bool flag, WinRoute primary, WinRoute? secondary, int own, int opponent)
        {
            var view = MatchEngine.CreateStarterMatch().GetView(0);
            view = view with { Phase = Phase.Action, PendingChoice = null, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [view.Players[0] with { Honor = own }, view.Players[1] with { Honor = opponent }] };
            var memory = new BotContext { UseArchetypeSwitch = flag, Archetype = new(primary, secondary, 0, 0, 0) };
            return TrainingOpponent.WantsOwnGainForTest(view, memory);
        }
        Check(Gains(true, WinRoute.Military, WinRoute.Honor, own: 30, opponent: 10), "an honor-route deck at 30 Honor gains it");
        Check(!Gains(true, WinRoute.Dishonor, WinRoute.Military, own: 5, opponent: 0), "a dishonor deck in no danger presses the opponent");
        Check(Gains(true, WinRoute.Dishonor, WinRoute.Military, own: -3, opponent: 0), "a dishonor deck below 0 Honor looks after itself");
        Check(Gains(false, WinRoute.Dishonor, WinRoute.Military, own: -3, opponent: 0) == Gains(false, WinRoute.Honor, null, own: -3, opponent: 0), "flag off: the archetype is ignored");
    }
}
