using L5R.AI;
using L5R.Core;

// Issue #963 (bot loop, round 1): Order Seppuku only for a Dishonored Personality when family Honor is low enough for the dishonorable death to threaten the game. See TrainingOpponent.Seppuku.cs.
internal static class SeppukuTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Seppuku: with the policy on the bot orders it only at low family Honor, and passes at healthy Honor; with it off it orders it whenever offered", Decision),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchView Table(int honor, int force, int personalHonor)
    {
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force == force && d.PersonalHonor == personalHonor);
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var dishonored = new CardView("dishonored", def, 0, Zone.Home, true, false, null, null, force) with { Dishonorable = true };
        return view with
        {
            Phase = Phase.Action,
            PendingChoice = null,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Honor = honor, TurnsTaken = 6, Home = [dishonored], Army = [] }, view.Players[1] with { Home = [new CardView("wall", BattlePolicyTests.Person(), 1, Zone.Home, true, false, null, null, 12)], Army = [] }] // a defender no army of its size can beat, so it has no attack this turn that could rehonor
        };
    }

    private static void Decision()
    {
        var on = new BotContext { UseSeppukuPolicy = true };
        var off = new BotContext();
        ActionOption Seppuku(MatchView v) => new("orderSeppuku", "Order Seppuku", true, null, new("orderSeppuku", 0, v.Version, CardId: "dishonored"), "dishonored");
        string? Choose(MatchView v, BotContext c) => TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), Seppuku(v)], c).Command?.Type;
        var healthy = Table(honor: 8, force: 2, personalHonor: 3);
        Check(Choose(healthy, on) == "pass", "healthy family Honor: it keeps the Personality");
        Check(Choose(healthy, off) == "orderSeppuku", "policy off: unchanged behaviour, it orders it whenever offered");
        var mild = Table(honor: -3, force: 2, personalHonor: 3);
        Check(Choose(mild, on) == "pass", "Honor -3 is above the limit: it keeps the Personality");
        var danger = Table(honor: -12, force: 2, personalHonor: 3);
        Check(Choose(danger, on) == "orderSeppuku", "Honor -12 with a weak dishonored Personality whose death would cost 3 more: it orders Seppuku");
        var strong = Table(honor: -12, force: 6, personalHonor: 1);
        Check(Choose(strong, on) == "pass", "a strong Personality that costs little Honor to lose is kept (a victory can rehonor it)");
    }
}
