using L5R.Core;
using static RegressionAssertions;

// Issue #848: The Courtesy of the Crane's Open action (src/L5R.Core/Wave848.cs), proved on the published card. Player 0 is the Crane, player 1 the Lion.
internal static class Wave848Tests
{
    private const string Courtesy = "oracle-l5r-7996", CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai";

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false) => Batch336Support.Add(s, def, owner, zone, bowed);
    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message) => Batch336Support.Eq(actual, expected, message);

    public static void Run(TestRunner runner) => runner.Run("wave-848", [
        ("The Courtesy of the Crane: the other player may draw a card, then you draw two; only an unbowed Crane Clan Personality can perform it", CourtesyOpen),
        ("The Courtesy of the Crane under The Heavens' Blessing: your two cards cost 2 Gold each, the other player's card is free", CourtesyUnderBlessing),
    ]);

    private static (MatchEngine E, CardInstance Card, CardInstance Crane) Table(bool blessing = false)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        if (blessing)
        {
            // Player abilities (the payment window's) exist only in a published-profile match: its hands emptied, a Holding producing 2 Gold per card owed.
            s = MatchEngine.CreatePublishedMatch().State;
            foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
            s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.PendingChoice = null;
            for (var i = 0; i < 2; i++) Add(s, Wave829Abilities.Shrine, 0, Zone.Home);
        }
        var crane = Add(s, CraneSamurai, 0, Zone.Home);
        crane.GrantedKeywords.Add(new("Crane Clan", "fixture", GrantDuration.Permanent));
        var card = Add(s, Courtesy, 0, Zone.Hand);
        if (blessing) s.Wave835 = new Wave835State { HeavensActive = true };
        return (MatchEngine.FromTestState(s), card, crane);
    }

    private static void CourtesyOpen()
    {
        Check(AbilityCatalog.All.ContainsKey(Wave848Abilities.CourtesyOpen) && (CardCatalog.Get(Courtesy).Abilities ?? []).Contains(Wave848Abilities.CourtesyOpen), "the card carries the Open action");
        var (e, card, crane) = Table();
        var mine = e.State.Players[0].Hand.Count; var theirs = e.State.Players[1].Hand.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave848Abilities.CourtesyOpen, card.Id, performerId: crane.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the other player decides");
        Eq(e.State.PendingChoice?.PlayerId, 1, "it is theirs to decide");
        Accept(e, e.QueryActions(1).First(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "draw").Command!);
        Eq(e.State.Players[1].Hand.Count, theirs + 1, "he drew a card");
        Eq(e.State.Players[0].Hand.Count, mine - 1 + 2, "the Strategy left the hand and two cards were drawn");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // Declined: he draws nothing, the player still draws two.
        var (d, dcard, dcrane) = Table();
        var dmine = d.State.Players[0].Hand.Count; var dtheirs = d.State.Players[1].Hand.Count;
        Accept(d, Batch336Support.Use(d, 0, Wave848Abilities.CourtesyOpen, dcard.Id, performerId: dcrane.Id));
        Accept(d, d.QueryActions(1).First(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "decline").Command!);
        Eq(d.State.Players[1].Hand.Count, dtheirs, "he did not draw");
        Eq(d.State.Players[0].Hand.Count, dmine + 1, "the player drew two");
        // A bowed Crane, or a Personality of another Clan, cannot perform it.
        var s = Batch336Support.Table(Phase.Action, 0);
        var bowed = Add(s, CraneSamurai, 0, Zone.Home, bowed: true); bowed.GrantedKeywords.Add(new("Crane Clan", "fixture", GrantDuration.Permanent));
        Add(s, LionSamurai, 0, Zone.Home);
        var c3 = Add(s, Courtesy, 0, Zone.Hand);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(s), 0, Wave848Abilities.CourtesyOpen, c3.Id), "no unbowed Crane Clan Personality, no action");
    }

    private static void CourtesyUnderBlessing()
    {
        var (e, card, crane) = Table(blessing: true);
        var theirs = e.State.Players[1].Hand.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave848Abilities.CourtesyOpen, card.Id, performerId: crane.Id));
        Accept(e, e.QueryActions(1).First(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "draw").Command!);
        Eq(e.State.Players[1].Hand.Count, theirs + 1, "the other player's card is not drawn from his own Strategy: free");
        Eq(e.State.Players[0].Hand.Count, 0, "the player's two cards are not drawn yet");
        Eq(e.State.Wave835!.HeavensOwed.GetValueOrDefault(0), 2, "two cards owed, each a 2 Gold payment");
        Eq(e.State.PendingChoice?.Type, Wave835Abilities.HeavensWindow, "and the payment window is open");
        Accept(e, Batch336Support.Enabled(e, 0, Wave835Abilities.HeavensPay).Command!);
        Accept(e, Batch336Support.Enabled(e, 0, Wave835Abilities.HeavensPay).Command!);
        Eq(e.State.Players[0].Hand.Count, 2, "both cards paid for and drawn");
    }
}
