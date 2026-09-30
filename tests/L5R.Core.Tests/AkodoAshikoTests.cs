using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #440: Akodo Ashiko (oracle-l5r-193) -- "Battle, [Gold]: If Ashiko is face-up in one of your
// Provinces, Recruit her reducing her Gold Cost by 2 if there are more units on the enemy side of the
// current battle than on your side" -- is the Reserve keyword's own shape (RecruitReserve: an absent
// battle action, no presence of the recruiting player's own required, only enemy presence at her own
// province) with an added conditional discount, on a card that carries no printed Reserve keyword of
// its own. MatchEngine.IsReserveRecruitable substitutes her own Effect string ("ashiko-outnumbered-
// reserve") for the keyword check; QuotePurchase reads her discount off Army(playerId, province) --
// already the engine's live, always-current unit-count primitive (e.g. barred-passage-bow's own army-
// size check) -- so nothing new needed building for "unit count at a battlefield" itself.
internal static class AkodoAshikoTests
{
    private const string Ashiko = "oracle-l5r-193";

    public static void Run(TestRunner runner) => runner.Run("akodo-ashiko", [
        ("Published shape: Akodo Ashiko is the printed Personality (F5/C4/PH0, Cost 8, Deathseeker/Hero/Lion Clan/Samurai, no printed Reserve keyword) with her own Effect and ships her art", PublishedShape),
        ("Recruited as an absent battle action from her own face-up province, at full cost when her side has at least as many units as the enemy", RecruitAtFullCost),
        ("Discounted by 2 when the enemy side has more units than hers, read live at the moment she is recruited", RecruitDiscounted),
        ("The unit-count comparison is live, not a snapshot from when the battle began: it reflects units that arrived or left after the initial assignment", DiscountIsDynamic),
        ("An ordinary Dynasty-phase Purchase of her (not a Reserve recruit) never gets the outnumbered discount", NoDiscountOutsideBattle),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // recruitReserve is a raw command type, not an AbilityCatalog id, so Batch336Support.Options/Enabled (keyed on AbilityCatalog.All[ability].Label) cannot find it.
    private static void AcceptReserve(MatchEngine e, string cardId, int cost)
    {
        var payment = e.PaymentOptions(0, cost).First();
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: cardId, SourceIds: payment.SourceIds));
    }

    // Issue #443: QueryActions' own "recruitReserve" listing now quotes with viaReserve:true (MatchQueries.cs), so the offered
    // option's own Gold.Cost must match what RecruitReserve actually charges, not just the full printed cost.
    private static void CheckOfferedCost(MatchEngine e, string cardId, int expectedCost) =>
        Check(e.QueryActions(0).Any(a => a.Type == "recruitReserve" && a.CardId == cardId && a.Command?.ClanDiscount != true && a.Command?.ClanAllegiance != true && a.Gold?.Cost == expectedCost),
            $"Offered recruitReserve quote (full-cost mode) shows {expectedCost}");

    // A battle at province 0 (player 0 defending), with Ashiko face-up in player 0's own province 0 slot -- the same shape GoldParityTests.Reserve() uses.
    private static MatchState AshikoTable()
    {
        var s = Table(); // Phase.Battle, province 0 the only unresolved battlefield, player 1 the Attacker
        s.PriorityPlayerId = 0; // player 0 (Ashiko's own controller) is the one asked whether to recruit her
        var ashikoId = s.Players[0].Provinces[0].CardId!;
        s.Cards[ashikoId].DefinitionId = Ashiko; s.Cards[ashikoId].FaceUp = true;
        return s;
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var ashiko = CardCatalog.Get(Ashiko);
        Check(ashiko.Name == "Akodo Ashiko" && ashiko.Type == CardType.Personality && ashiko.Force == 5 && ashiko.Chi == 4 && ashiko.PersonalHonor == 0 && ashiko.GoldCost == 8
            && (ashiko.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Deathseeker", "Hero", "Lion Clan", "Samurai" }.OrderBy(k => k, StringComparer.Ordinal))
            && !(ashiko.Keywords ?? []).Contains("Reserve") && ashiko.Effect == "ashiko-outnumbered-reserve" && (ashiko.Abilities ?? []).Length == 0, "Akodo Ashiko");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Ashiko + ".jpg")), "art is shipped");
        Eq(ashiko.ArtworkKey, $"res://Content/Published/{Ashiko}.jpg");
    }

    private static void RecruitAtFullCost()
    {
        var s = AshikoTable();
        var ashikoId = s.Players[0].Provinces[0].CardId!;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // player 0: 1 unit
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // player 1 (enemy presence, required): 1 unit -- tied, not "more"
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, ashikoId, viaReserve: true);
        Eq(quote.Cost, 8, "no discount when not outnumbered");
        Check(!(quote.Basis ?? "").Contains("outnumbered"), quote.Basis ?? "");
        CheckOfferedCost(e, ashikoId, 8);
        AcceptReserve(e, ashikoId, 8);
        Eq(e.State.Cards[ashikoId].Zone, Zone.Battlefield, "recruited to the battlefield");
    }

    private static void RecruitDiscounted()
    {
        var s = AshikoTable();
        var ashikoId = s.Players[0].Provinces[0].CardId!;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // player 0: 1 unit
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // player 1: 2 units -- more than player 0's
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, ashikoId, viaReserve: true);
        Eq(quote.Cost, 6, "8 − 2 (outnumbered)");
        Check((quote.Basis ?? "").Contains("− 2 (outnumbered)"), quote.Basis ?? "");
        CheckOfferedCost(e, ashikoId, 6);
        AcceptReserve(e, ashikoId, 6);
        Eq(e.State.Cards[ashikoId].Zone, Zone.Battlefield, "recruited to the battlefield");
    }

    private static void DiscountIsDynamic()
    {
        var s = AshikoTable();
        var ashikoId = s.Players[0].Provinces[0].CardId!;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); // player 0: 2 units
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // player 1: 1 unit -- fewer, no discount yet
        var e = MatchEngine.FromTestState(s);
        Eq(e.QuotePurchase(0, ashikoId, viaReserve: true).Cost, 8, "not outnumbered at the initial assignment");

        // A later action changes who has more units at the battlefield -- the same board, further along, not a fresh scenario.
        var reinforced = Edit(e, st => Add(st, "training-crane-samurai", 1, Zone.Battlefield, province: 0)); // player 1 now has 2, still tied
        Eq(reinforced.QuotePurchase(0, ashikoId, viaReserve: true).Cost, 8, "tied is not \"more\"");
        var outnumbered = Edit(reinforced, st => Add(st, "training-crane-samurai", 1, Zone.Battlefield, province: 0)); // player 1 now has 3 against player 0's 2
        Eq(outnumbered.QuotePurchase(0, ashikoId, viaReserve: true).Cost, 6, "now outnumbered: the discount reflects the CURRENT count, not the one when the battle began");
        CheckOfferedCost(outnumbered, ashikoId, 6);
        AcceptReserve(outnumbered, ashikoId, 6);
        Eq(outnumbered.State.Cards[ashikoId].Zone, Zone.Battlefield, "recruited at the discounted cost the live count now supports");
    }

    private static void NoDiscountOutsideBattle()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var ashikoId = s.Players[0].Provinces[0].CardId!;
        s.Cards[ashikoId].DefinitionId = Ashiko; s.Cards[ashikoId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, ashikoId);
        Eq(quote.Cost, 8, "an ordinary Purchase never reads the battlefield unit counts");
        Check(!(quote.Basis ?? "").Contains("outnumbered"), quote.Basis ?? "");
    }
}
