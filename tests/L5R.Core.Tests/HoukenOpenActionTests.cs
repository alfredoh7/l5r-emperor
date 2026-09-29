using L5R.Core;

internal static class HoukenOpenActionTests
{
    public static void Run(TestRunner runner) => runner.Run("nightly-houken", [
        ("Offered Open action outside a battle straightens the unit without throwing", Open),
        ("Battle path still straightens Houken and joins an opposing army", Battle)
    ]);

    private static void Open()
    {
        var state = Batch336Support.Table(Phase.Action, 0);
        var houken = Batch336Support.Add(state, "oracle-l5r-10053", 0, Zone.Home, bowed: true);
        var game = MatchEngine.FromTestState(state);
        var command = Batch336Support.Enabled(game, 0, "houken-exp-straighten", houken.Id).Command!;
        Batch336Support.Check(game.Execute(command).Accepted);
        Batch336Support.Check(!game.State.Cards[houken.Id].Bowed && game.State.Cards[houken.Id].Zone == Zone.Home);
    }

    private static void Battle()
    {
        var state = Batch336Support.Table(Phase.Battle, 0);
        var houken = Batch336Support.Add(state, "oracle-l5r-10053", 0, Zone.Home, bowed: true);
        Batch336Support.Add(state, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var game = MatchEngine.FromTestState(state);
        var command = Batch336Support.Enabled(game, 0, "houken-exp-straighten", houken.Id).Command!;
        Batch336Support.Check(game.Execute(command).Accepted);
        Batch336Support.Check(!game.State.Cards[houken.Id].Bowed && game.State.Cards[houken.Id].Zone == Zone.Battlefield);
    }
}
