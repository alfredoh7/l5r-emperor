using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #639 (Mendon): the Proclaim window closes the moment a Proclaim has resolved and nobody has a reaction left to take; the purchase options are ordered so the first
// Personality bought in a turn defaults to full cost with Proclaim, every later one to the Clan discount, and one whose Honor Requirement is not met to Clan allegiance (+2).
internal static class ProclaimFlowTests
{
    public static void Run(TestRunner runner) => runner.Run("proclaim-flow", [
        ("After a Proclaim nobody is prompted when no reaction is left to take", NoPromptWhenNothingLeft),
        ("The window stays open after a Proclaim while the entering card's own Reaction is still unused", WindowStaysForAnUnusedReaction),
        ("The first Personality bought in a turn defaults to full cost with Proclaim, the next to the Clan discount", FirstPurchaseDefaultsToProclaim),
        ("A Personality whose Honor Requirement is not met defaults to Clan allegiance, with the discount on top once the first is bought", HonorRequirementAddsAllegiance),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // Player 0 (Crane or Spider) in the Dynasty Phase with plenty of Gold; the first provinces hold `cards`, face up.
    private static (MatchEngine Engine, CardInstance[] Cards) Shop(string clan, int honor, params string[] cards)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = clan; s.Players[0].Honor = honor;
        for (var i = 0; i < 12; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var shown = new List<CardInstance>();
        for (var i = 0; i < cards.Length; i++)
        {
            var card = s.Cards[s.Players[0].Provinces[i].CardId!]; card.DefinitionId = cards[i]; card.FaceUp = true; shown.Add(card);
        }
        return (MatchEngine.FromTestState(s), shown.ToArray());
    }

    private static ActionOption[] Buys(MatchEngine e, CardInstance card) => e.QueryActions(0).Where(a => a.Type == "purchase" && a.CardId == card.Id).ToArray();
    private static ActionOption DefaultBuy(MatchEngine e, CardInstance card) => Buys(e, card).First(a => a.Enabled);

    private static void NoPromptWhenNothingLeft()
    {
        var (e, cards) = Shop("Crane", 10, "training-crane-samurai");
        Accept(e, DefaultBuy(e, cards[0]).Command!);
        Eq(e.State.PendingChoice?.Type, "proclaim", "the Proclaim window opens on the purchase");
        var honor = e.State.Players[0].Honor;
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Check(e.State.PendingChoice is null, "no reaction is left to take, so the window is closed");
        Eq(e.State.Players[0].Honor, honor + 2, "the Proclaim resolved first");
        Eq(e.State.PriorityPlayerId, 0, "the buyer goes on purchasing");
        Check(e.State.Events.Any(x => x.Type == "reactionClosed"), "and the closing is in the log");
    }

    private static void WindowStaysForAnUnusedReaction()
    {
        var (e, cards) = Shop("Spider", 10, "training-spider-honor-loss");
        Accept(e, DefaultBuy(e, cards[0]).Command!);
        Eq(e.State.PendingChoice?.Type, "proclaim");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Eq(e.State.PendingChoice?.Type, "proclaim", "the entering card's own Reaction is still unused, so the window stays");
        Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the opponent has nothing and passes
        Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled), "and the Reaction is still offered to its owner");
    }

    private static void FirstPurchaseDefaultsToProclaim()
    {
        var (e, cards) = Shop("Crane", 10, "training-crane-samurai", "training-crane-samurai");
        var first = DefaultBuy(e, cards[0]);
        Check(first.Command is { ClanDiscount: false, ClanAllegiance: false } && first.Quote is { ProclaimEligible: true }, "the first Personality defaults to full cost with Proclaim");
        Check(Buys(e, cards[0]).Any(a => a.Command is { ClanDiscount: true } && a.Enabled), "the Clan discount is still offered");
        Accept(e, first.Command!);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        var next = DefaultBuy(e, cards[1]);
        Check(next.Command is { ClanDiscount: true, ClanAllegiance: false }, "the next Personality defaults to the Clan-discounted cost");
        Eq(next.Quote!.Cost, 3, "5 Gold less the discount of 2");
        // Whether or not the first was proclaimed: buying with the discount counts as the first purchase too.
        var (e2, cards2) = Shop("Crane", 10, "training-crane-samurai", "training-crane-samurai");
        Accept(e2, Buys(e2, cards2[0]).First(a => a.Enabled && a.Command is { ClanDiscount: true, ClanAllegiance: false }).Command!);
        Check(DefaultBuy(e2, cards2[1]).Command is { ClanDiscount: true }, "after a first purchase that was not proclaimed, the discount is the default");
    }

    private static void HonorRequirementAddsAllegiance()
    {
        var (e, cards) = Shop("Crane", 0, "training-crane-samurai", "training-crane-samurai"); // Honor Requirement 4, Family Honor 0
        var plain = Buys(e, cards[0]).Single(a => a.Quote is { ClanDiscount: false, ClanAllegiance: false });
        Check(!plain.Enabled, "without allegiance the purchase is barred");
        var first = DefaultBuy(e, cards[0]);
        Check(first.Command is { ClanDiscount: false, ClanAllegiance: true }, "the first Personality defaults to full cost plus the +2 allegiance cost, applied without being asked for");
        Eq(first.Quote!.Cost, 7, "5 Gold + 2");
        Check(first.Quote.ProclaimEligible, "and it can still be proclaimed");
        Accept(e, first.Command!);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        var next = DefaultBuy(e, cards[1]);
        Check(next.Command is { ClanDiscount: true, ClanAllegiance: true }, "the next defaults to the discount, with allegiance still applied");
        Eq(next.Quote!.Cost, 5, "5 Gold - 2 + 2");
    }
}
