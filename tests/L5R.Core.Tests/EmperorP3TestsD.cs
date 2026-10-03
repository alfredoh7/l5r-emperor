using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition group P3 (issue #503), fourth part: Easy Persuasion (EmperorP3Entries.cs).
internal static partial class EmperorP3Tests
{
    private const string EasyPersuasion = "oracle-l5r-9905";

    public static (string, Action)[] PartD => [
        ("Published shape (fourth part): Easy Persuasion (strategy-wave-90)", PublishedShapeD),
        ("Easy Persuasion, Reaction: after a Personality enters play for less than his base Gold Cost, the other player's Merchant bows and the Personality's controller loses his base Personal Honor (at most 3)", PersuasionReaction),
        ("Easy Persuasion, Reaction edges: nothing when the full Gold Cost was paid, or without an unbowed Merchant; an overlay is not an entry", PersuasionEdges),
        ("Easy Persuasion, Battle: pay the target unit's total Gold Cost minus 5 (at least 0) to move it home; its controller loses 2 Honor", PersuasionMove),
    ];

    private static void PublishedShapeD()
    {
        var c = CardCatalog.Get(EasyPersuasion);
        Check(c.Type == CardType.Strategy && c.Name == "Easy Persuasion" && c.Focus == 2 && c.GoldCost == 0 && (c.Keywords ?? []).Length == 0, "Easy Persuasion's stats");
        Eq(string.Join(",", c.Abilities ?? []), "easy-persuasion-honor,easy-persuasion-move");
        Eq(c.Text, "Reaction: After a Personality enters play, if his controller paid less than his base Gold Cost for him, bow your performing Merchant: The Personality's controller loses Honor equal to his base Personal Honor, or 3, whichever is lower.\nBattle: Pay Gold equal to a target unit's total Gold Cost minus 5: Move it home. Its controller loses 2 Honor.");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", EasyPersuasion + ".jpg")), "art is shipped");
    }

    private static MatchEngine PersuasionPurchase(bool discount, bool merchant, out CardInstance entering, out CardInstance card, out CardInstance trader)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        entering = s.Cards[s.Players[0].Provinces[0].CardId!];
        entering.DefinitionId = "training-crane-samurai"; entering.FaceUp = true; // Crane, Gold Cost 5, Personal Honor 2
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        card = Add(s, EasyPersuasion, 1, Zone.Hand);
        trader = Add(s, "training-lion-courtier", 1, Zone.Home);
        if (merchant) GiveKeyword(trader, "Merchant");
        var e = MatchEngine.FromTestState(s);
        var cost = e.QuotePurchase(0, entering.Id, clanDiscount: discount).Cost;
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: entering.Id, ClanDiscount: discount, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: "proclaim" or "entryReaction" }; guard++) Accept(e, Pass(e));
        return e;
    }

    private static void PersuasionReaction()
    {
        var e = PersuasionPurchase(true, true, out var entering, out var card, out var trader);
        Eq(e.State.Cards[entering.Id].Zone, Zone.Home, "he entered play for 3 Gold (the Clan discount)");
        Eq(e.State.PendingChoice?.Type, "p3EntryReaction", "the window after his entry, for the other player");
        Eq(e.State.PriorityPlayerId, 1, "who holds the Reaction");
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 1, "easy-persuasion-honor", card.Id, trader.Id).Command!);
        Eq(Honor(e, 0), honor - 2, "his controller lost his base Personal Honor (2)");
        Check(e.State.Cards[trader.Id].Bowed, "the Merchant bowed as the cost");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase goes on");
    }

    private static void PersuasionEdges()
    {
        var full = PersuasionPurchase(false, true, out _, out _, out _);
        Check(full.State.PendingChoice?.Type != "p3EntryReaction", "the full Gold Cost was paid: no window");
        var noMerchant = PersuasionPurchase(true, false, out _, out _, out _);
        Check(noMerchant.State.PendingChoice?.Type != "p3EntryReaction", "no Merchant: no window");
    }

    private static void PersuasionMove()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, EasyPersuasion, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Gold Cost 5
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);   // + 2 = 7
        var cheap = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);  // 5: pays 0
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 0, "easy-persuasion-move", card.Id).First(o => o.Enabled);
        Eq(option.TargetGold![enemy.Id].Cost, 2, "the unit's total Gold Cost minus 5");
        Eq(option.TargetGold![cheap.Id].Cost, 0, "never below 0");
        var honor = Honor(e, 1);
        var command = option.Command! with { TargetId = enemy.Id, SourceIds = option.TargetGold[enemy.Id].Payments.First().SourceIds };
        Accept(e, command);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the unit moved home");
        Eq(Honor(e, 1), honor - 2, "its controller lost 2 Honor");
    }
}
