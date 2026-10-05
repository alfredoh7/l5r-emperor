using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #644 (Mendon's playtest): Steel on Steel needs its performer at the battlefield; a Celestial turned face up enters play at once; an entering-play Reaction that has been
// used (or that nobody can take) leaves no Pass reaction box.
internal static class PlaytestFixTests
{
    public static void Run(TestRunner runner) => runner.Run("playtest-fixes-644", [
        ("Steel on Steel: the performer must be at the battlefield, and one there is accepted", SteelOnSteelPerformer),
        ("A Celestial that is face up in a province enters play at once, whichever effect turned it", CelestialEntersAtOnce),
        ("An entry window with nothing to take closes after one pass, and after a used Reaction", EntryWindowClosesWhenSpent),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static void SteelOnSteelPerformer()
    {
        var s = Table(Phase.Battle, 0);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home); // not at the battlefield
        var strategy = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var enemy = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, AbilityId: "steel-on-steel-duel", PerformerId: home.Id, TargetId: enemy.Id));
        var s2 = Table(Phase.Battle, 0);
        var there = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy2 = Add(s2, "training-plain-duel-strategy", 0, Zone.Hand);
        var enemy2 = Add(s2, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: strategy2.Id, AbilityId: "steel-on-steel-duel", PerformerId: there.Id, TargetId: enemy2.Id));
        Check(e2.State.PendingDuel is not null, "the duel is under way");
    }

    private static void CelestialEntersAtOnce()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var celestial = s.Cards[s.Players[1].Provinces[0].CardId!]; celestial.DefinitionId = "training-bow-celestial"; celestial.Printed = CardCatalog.Get("training-bow-celestial"); celestial.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("endDynasty", 0, e.Version));
        Eq(e.State.Cards[celestial.Id].Zone, Zone.Celestial, "it entered play instead of sitting revealed in the province");
        Check(e.State.Players[1].Provinces[0].CardId != celestial.Id, "and its province was refilled");
    }

    private static void EntryWindowClosesWhenSpent()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Spider"; s.Players[0].Honor = 10;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var card = s.Cards[s.Players[0].Provinces[0].CardId!]; card.DefinitionId = "training-spider-honor-loss"; card.Printed = CardCatalog.Get("training-spider-honor-loss"); card.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var buy = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == card.Id && a.Enabled && a.Command!.ClanDiscount);
        Accept(e, buy.Command!);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "bought with the discount: only the card's own Reaction is on offer");
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Check(e.State.PendingChoice is null, "the opponent has nothing to react with, so passing closed the window at once");
        Eq(e.State.PriorityPlayerId, 0, "and the buyer carries on purchasing");
    }
}
