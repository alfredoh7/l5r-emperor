using L5R.Core;

internal static class HoukenOpenActionTests
{
    public static void Run(TestRunner runner) => runner.Run("nightly-houken", [
        ("Offered Open action outside a battle straightens the unit without throwing", Open),
        ("Battle path still straightens Houken and joins an opposing army", Battle)
    ]);

    private static void Open()
    {
        for (var controller = 0; controller < 2; controller++)
        {
            var state = Batch336Support.Table(Phase.Action, controller);
            var houken = Batch336Support.Add(state, "oracle-l5r-10053", controller, Zone.Home, bowed: true);
            var game = MatchEngine.FromTestState(state);
            var command = Batch336Support.Enabled(game, controller, "houken-exp-straighten", houken.Id).Command!;
            Batch336Support.Check(game.Execute(command).Accepted);
            var after = game.State.Cards[houken.Id];
            Batch336Support.Check(!after.Bowed && after.Zone == Zone.Home);
            Batch336Support.Check(after.OwnerId == controller && after.OriginalOwnerId is null, "Houken must retain his controller and owner.");
        }
    }

    private static void Battle()
    {
        for (var controller = 0; controller < 2; controller++)
        {
            var state = Batch336Support.Table(Phase.Battle, controller);
            var houken = Batch336Support.Add(state, "oracle-l5r-10053", controller, Zone.Home, bowed: true);
            Batch336Support.Add(state, "training-crane-samurai", 1 - controller, Zone.Battlefield, province: 0);
            var game = MatchEngine.FromTestState(state);
            var command = Batch336Support.Enabled(game, controller, "houken-exp-straighten", houken.Id).Command!;
            Batch336Support.Check(game.Execute(command).Accepted);
            var after = game.State.Cards[houken.Id];
            Batch336Support.Check(!after.Bowed && after.Zone == Zone.Battlefield && after.ProvinceIndex == 0);
            Batch336Support.Check(after.OwnerId == controller && after.OriginalOwnerId is null, "Moving to an opposed battlefield must not transfer control.");
        }
    }
}
