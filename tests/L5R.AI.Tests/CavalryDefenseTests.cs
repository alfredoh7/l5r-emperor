using L5R.AI;
using L5R.Core;

// Issue #946: Mendon's defense guidance for an attacker that commits no infantry but has Cavalry at home.
internal static class CavalryDefenseTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("DefensePlanner: parity needs Force / threat + 0.1 per hand card and Battle action to reach 1; one army per sub-army that reaches 1 alone; leftovers stay home unless they print a Battle action", Planner),
        ("Bot: with no infantry assigned against it, it commits its infantry to the most valuable province when it can match the attacker's possible Cavalry, and passes when it cannot or there is none", BotDefends),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static DefensePlanner.Unit Foot(string id, int force, bool action = false) => new(id, force, true, action);
    private static DefensePlanner.Unit Horse(string id, int force, bool action = false) => new(id, force, false, action);

    private static void Planner()
    {
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 0) is null, "8 against 10 with nothing in hand: 0.8 never reaches 1");
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 1) is null, "one card makes 0.9");
        Check(DefensePlanner.Plan([Foot("a", 8)], [0, 1], 10, 2) is { } two && two["a"] == 0, "two cards make exactly 1.0: defend");
        Check(DefensePlanner.Plan([Foot("a", 8, true)], [0, 1], 10, 1) is not null, "a printed Battle action on a defending unit counts like a card");
        Check(DefensePlanner.Plan([Foot("a", 10)], [0], 10, 0) is not null, "equal Force is parity");
        Check(DefensePlanner.Plan([Horse("a", 12)], [0], 10, 0) is null, "only Cavalry at home: it is held back for the Cavalry Maneuvers Segment, so nothing is committed now");
        Check(DefensePlanner.Plan([Foot("a", 12)], [0], 0, 0) is null, "no possible Cavalry army, no defense");

        // Three units of 6 against 6: three sub-armies, one per province, in value order.
        var three = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 6)], [2, 0, 1], 6, 0)!;
        Check(three["a"] == 2 && three["b"] == 0 && three["c"] == 1, "each unit is its own army at the next most valuable province");
        // Only two provinces: the third unit is a leftover and stays home...
        var two2 = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 6)], [2, 0], 6, 0)!;
        Check(two2.Count == 2 && !two2.ContainsKey("c"), "no province left for a third army: it stays home");
        // ...and a Battle action takes it into the first army.
        var action = DefensePlanner.Plan([Foot("a", 6), Foot("b", 6), Foot("c", 3, true)], [2, 0], 6, 0)!;
        Check(action["c"] == 2 && action["a"] == 2 && action["b"] == 0, "a leftover with a Battle action joins the first army (got " + string.Join(",", action.Select(p => p.Key + ">" + p.Value)) + ")");
        // The hand backs the first army only: 4 + 4 against 6 with one card each.. the first reaches 0.67+0.1*... never, the second needs 6 alone.
        var hand = DefensePlanner.Plan([Foot("a", 5), Foot("b", 4)], [0, 1], 6, 1)!;
        Check(hand.Count == 2 && hand["a"] == 0 && hand["b"] == 0, "5 + 4 fall short of a second army of 6 on their own: both defend the first province (got " + string.Join(",", hand.Select(p => p.Key + ">" + p.Value)) + ")");
        // Cavalry defenders are held back for the Cavalry Maneuvers Segment, however strong, but their Force counts toward parity.
        var mixed = DefensePlanner.Plan([Horse("h", 9), Foot("f", 6)], [0, 1], 6, 0)!;
        Check(mixed.Count == 1 && mixed["f"] == 0, "the Cavalry is not assigned now");
        var joined = DefensePlanner.Plan([Horse("h", 6), Foot("f", 4)], [0, 1], 10, 0)!;
        Check(joined.Count == 1 && joined["f"] == 0, "4 infantry alone are short of 10, but with the held-back Cavalry the defense is on par: the infantry holds the most valuable province");
        Check(DefensePlanner.Plan([Horse("h", 3), Foot("f", 4)], [0, 1], 10, 0) is null, "even with the Cavalry it cannot reach parity: nobody is committed");
    }

    private static CardDefinition Infantry => CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force >= 4 && d.Keywords?.Contains("Cavalry") != true);
    private static CardView Unit(string id, int owner, int force, CardDefinition def) => new(id, def, owner, Zone.Home, true, false, null, null, force);

    private static void BotDefends()
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var cheap = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost == 3);
        var dear = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.GoldCost >= 7);
        var cavalry = CardCatalog.Get("training-cavalry");
        ProvinceView[] Provinces() =>
        [
            new(0, 5, false, null, null), new(1, 5, false, new CardView("p1", cheap, 0, Zone.Province, true, false, 1, null, cheap.Force), null),
            new(2, 5, false, new CardView("p2", dear, 0, Zone.Province, true, false, 2, null, dear.Force), null),
            new(3, 5, false, new CardView("hidden-p3", null, 0, Zone.Province, false, false, 3, null, 0), null)
        ];
        MatchView Table(int ownForce, int horses, int horseForce, int hand = 0) => view with
        {
            Phase = Phase.DefenseAssignment,
            ActivePlayerId = 1,
            PriorityPlayerId = 0,
            PendingChoice = null,
            Players = [view.Players[0] with { Home = [Unit("own-1", 0, ownForce, Infantry)], Army = [], Provinces = Provinces(), HandCount = hand, Hand = [] },
                view.Players[1] with { Home = Enumerable.Range(0, horses).Select(i => Unit("h-" + i, 1, horseForce, cavalry)).ToArray(), Army = [] }]
        };
        ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
        ActionOption Defend(MatchView v) => new("assignDefense", "Defend", true, null, null, TargetIds: ["own-1"], Provinces: [0, 1, 2, 3]);

        var even = Table(8, 2, 4);
        var chosen = TrainingOpponent.Choose(even, [Pass(even), Defend(even)]);
        Check(chosen.Command?.Type == "assignDefense" && chosen.Command.Assignments?["own-1"] == 2, "8 against a possible 8 Cavalry: it defends the province holding the 7-Gold Personality (got " + chosen.Command?.Type + ")");

        var weak = Table(5, 2, 4);
        Check(TrainingOpponent.Choose(weak, [Pass(weak), Defend(weak)]).Command?.Type == "pass", "5 against 8 with an empty hand cannot reach 1: it keeps its units home");
        var tricks = Table(5, 2, 4, hand: 4);
        Check(TrainingOpponent.Choose(tricks, [Pass(tricks), Defend(tricks)]).Command?.Type == "assignDefense", "5 / 8 = 0.625 + 0.4 from four cards reaches 1: it defends");
        var none = Table(8, 0, 0);
        Check(TrainingOpponent.Choose(none, [Pass(none), Defend(none)]).Command?.Type == "pass", "no Cavalry at home and nobody attacking: nothing to defend against");
        Check(TrainingOpponent.Choose(even, [Pass(even), Defend(even)], new BotContext { UseCavalryDefense = false }).Command?.Type == "pass", "switched off, it plays as before");
    }
}
