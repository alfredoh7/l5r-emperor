using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #771 (src/L5R.Core/Wave771.cs): Journey's End Keep - exp and the Imperial Clan, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane,
// player 1 the Lion; here player 0's Stronghold is swapped for Journey's End Keep) and a refused command must leave the state unchanged.
internal static class Wave771Tests
{
    private const string Jek = "oracle-l5r-10406", Uehara = "oracle-l5r-10850", Makubesu = "oracle-l5r-10078", Narizane = "oracle-l5r-1663", Mai = "oracle-l5r-9086",
        Washi = "oracle-l5r-6620", Suikihime = "oracle-l5r-10094", Demiyah = "oracle-l5r-5812", Marketplace = "training-marketplace";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null) => Batch336Support.Add(s, def, owner, zone, bowed, province);

    public static void Run(TestRunner runner) => runner.Run("wave-771", [
        ("Published as printed: Journey's End Keep - exp (stronghold-wave-56), listed as the Imperial Clan with its scan text", PublishedShape),
        ("Imperial is a Clan alignment for every Personality with the Imperial keyword while a Journey's End Keep is a Stronghold in the match, and not otherwise", ImperialAlignment),
        ("The Journey's End Keep player may Proclaim an Imperial Personality at full price; the Clan discount is for those with another alignment (Uehara, Makubesu, Narizane, Mai) and not for Washi, Suikihime, Demiyah", RecruitTerms),
        ("Clan allegiance: an Imperial Personality is bought past his Honor Requirement for 2 more Gold, by the Journey's End Keep player only", Allegiance),
        ("Setup: a Journey's End Keep player begins with the Imperial Favor unless the other player's Stronghold has the Imperial keyword; both setup paths call it", FavorAtSetup),
        ("Gold lock: JEK paying a 3 Gold Holding spends all 5 of its Gold, so Personalities may be recruited at once", LockLiftedByExcess),
        ("Gold lock: after JEK pays for a Personality no other can be recruited until the phase ends or 5 Gold is spent on non-Recruit costs", LockHeldUntilSpent),
        ("Gold lock: it lapses with the phase, never binds the other player, and a player without JEK never opens one", LockLapses),
    ]);

    // ---- helpers -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState JekTable(Phase phase = Phase.Dynasty, int actor = 0)
    {
        var s = Batch336Support.Table(phase, actor);
        var stronghold = s.Cards[s.Players[0].StrongholdId];
        stronghold.DefinitionId = Jek; stronghold.Printed = null; s.Players[0].Clan = "Imperial"; s.Players[0].Honor = 30;
        s.Players[1].Honor = 30;
        return s;
    }

    private static CardInstance Province(MatchState s, int owner, int index, string def, Func<CardDefinition, CardDefinition>? shape = null)
    {
        var id = s.Players[owner].Provinces[index].CardId!;
        var card = s.Cards[id]; card.DefinitionId = def; card.FaceUp = true; card.Zone = Zone.Province; card.ProvinceIndex = index;
        if (shape is not null) card.Printed = shape(CardCatalog.Get(def));
        return card;
    }

    private static CardDefinition ThreeGoldHolding(CardDefinition d) => d with { GoldCost = 3, Keywords = [], Effect = "none", Abilities = [] };
    private static CardDefinition FiveGoldHolding(CardDefinition d) => d with { GoldCost = 5, Keywords = [], Effect = "none", Abilities = [] };

    private static PaymentOption JekOnly(MatchEngine e, int cost, string strongholdId) =>
        e.PaymentOptions(0, cost).First(o => o.SourceIds.Length == 1 && o.SourceIds[0] == strongholdId);

    private static void Accept(MatchEngine e, GameCommand command)
    {
        var result = e.Execute(command);
        Check(result.Accepted, $"{command.Type} accepted: {result.Error}");
        // An Imperial Personality is Proclaim-eligible: decline the window the recruit opens, which is not Gold spent.
        if (e.State.PendingChoice?.Type == "proclaim") Check(e.Execute(new GameCommand("pass", command.PlayerId, e.Version)).Accepted, "the Proclaim window is declined");
    }

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var jek = CardCatalog.Get(Jek);
        Check(jek.Type == CardType.Stronghold && jek.Clan == "Imperial" && jek.Effect == Wave771Abilities.Effect && jek.GoldProduction == 5 && jek.StartingHonor == 8 && jek.ProvinceStrength == 7, "a 5 Gold Stronghold listed as Imperial");
        Check((jek.Keywords ?? []).SequenceEqual(["Experienced", "Imperial"]), "its keywords are Experienced and Imperial");
        Check(jek.Text.Contains("\"Imperial\" is a Clan Alignment", StringComparison.Ordinal) && jek.Text.Contains("Imperial Favor", StringComparison.Ordinal), "it prints its text");
        Check(!jek.ScenarioOnly && !jek.CreatedOnly, "it is playable");
    }

    private static void ImperialAlignment()
    {
        var s = JekTable();
        var washi = Add(s, Washi, 0, Zone.Home); var uehara = Add(s, Uehara, 0, Zone.Home); var theirs = Add(s, Washi, 1, Zone.Home); var theirUehara = Add(s, Uehara, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(string.Join(",", e.ClanAlignmentsForTest(washi.Id)), "Imperial", "an Imperial-only Personality is aligned to Imperial");
        Eq(string.Join(",", e.ClanAlignmentsForTest(uehara.Id)), "Lion,Imperial", "Uehara is a Lion who is also Imperial");
        Eq(string.Join(",", e.ClanAlignmentsForTest(theirs.Id)), "Imperial", "Imperial is a Clan of the match, so the other player's Washi has it too");
        Eq(string.Join(",", e.ClanAlignmentsForTest(theirUehara.Id)), "Lion,Imperial", "and his Uehara");
        var plain = Batch336Support.Table(Phase.Dynasty, 0);
        var p1 = Add(plain, Washi, 0, Zone.Home); var p2 = Add(plain, Uehara, 0, Zone.Home);
        var none = MatchEngine.FromTestState(plain);
        Eq(none.ClanAlignmentsForTest(p1.Id).Length, 0, "without a Journey's End Keep Washi is Unaligned, as before");
        Eq(string.Join(",", none.ClanAlignmentsForTest(p2.Id)), "Lion", "and Uehara is only a Lion");
    }

    private static void RecruitTerms()
    {
        var defs = new[] { Uehara, Makubesu, Narizane, Mai, Washi, Suikihime, Demiyah };
        // One province holds one card at a time: quote each card from its own table.
        foreach (var def in defs)
        {
            var table = JekTable();
            var card = Province(table, 0, 0, def);
            var e = MatchEngine.FromTestState(table);
            var name = CardCatalog.Get(def).Name;
            var printedGold = CardCatalog.Get(def).GoldCost;
            var plain = e.QuotePurchase(0, card.Id);
            Check(plain.Error is null, $"{name}: an ordinary recruit by the Journey's End Keep player is legal: {plain.Error}");
            Check(plain.ProclaimEligible, $"{name} may be Proclaimed (Imperial is his Clan, so at full price)");
            Eq(plain.Cost, printedGold, $"{name} costs his printed Gold");
            var discount = e.QuotePurchase(0, card.Id, clanDiscount: true);
            var otherAlignments = new[] { Uehara, Makubesu, Narizane, Mai }.Contains(def);
            if (otherAlignments)
            {
                Check(discount.Error is null, $"{name} has another Clan alignment, so the Clan discount stands: {discount.Error}");
                Eq(discount.Cost, Math.Max(0, printedGold - 2), $"{name} with the Clan discount costs 2 less");
                Check(!discount.ProclaimEligible, $"{name} bought with the Clan discount is not Proclaimed");
            }
            else
            {
                Check(discount.Error is not null && discount.Error.Contains("Journey's End Keep", StringComparison.Ordinal), $"{name}'s only alignment is Imperial: no Clan discount ({discount.Error})");
            }
            // The other player (a Lion) gains nothing from Imperial: no Proclaim for the Imperial-only ones, and the Lion Uehara is his own Clan's as ever.
            var theirTable = JekTable(actor: 1);
            var theirs = Province(theirTable, 1, 0, def);
            var they = MatchEngine.FromTestState(theirTable);
            var theirQuote = they.QuotePurchase(1, theirs.Id);
            Eq(theirQuote.ProclaimEligible, def == Uehara, $"{name}: only a Lion's own Clan makes the Lion eligible");
        }
    }

    private static void Allegiance()
    {
        var table = JekTable();
        var washi = Province(table, 0, 0, Washi, d => d with { HonorRequirement = 20 });
        table.Players[0].Honor = 5;
        var e = MatchEngine.FromTestState(table);
        Check(e.QuotePurchase(0, washi.Id).Error is not null, "below the Honor Requirement an ordinary recruit is refused");
        var allegiance = e.QuotePurchase(0, washi.Id, clanAllegiance: true);
        Check(allegiance.Error is null, "Clan allegiance is allowed: Imperial is his Clan: " + allegiance.Error);
        Eq(allegiance.Cost, CardCatalog.Get(Washi).GoldCost + 2, "for 2 more Gold");
        var theirs = JekTable(actor: 1); var theirCard = Province(theirs, 1, 0, Washi, d => d with { HonorRequirement = 20 }); theirs.Players[1].Honor = 5;
        Check(MatchEngine.FromTestState(theirs).QuotePurchase(1, theirCard.Id, clanAllegiance: true).Error is not null, "the Lion has no allegiance to Imperial");
    }

    private static void FavorAtSetup()
    {
        var s = JekTable();
        MatchEngine.ApplyImperialSetup(s);
        Eq(s.FavorPlayerId, 0, "the Journey's End Keep player begins with the Imperial Favor");
        var seat1 = Batch336Support.Table(Phase.Dynasty, 0);
        var sh1 = seat1.Cards[seat1.Players[1].StrongholdId]; sh1.DefinitionId = Jek; sh1.Printed = null; seat1.Players[1].Clan = "Imperial";
        MatchEngine.ApplyImperialSetup(seat1);
        Eq(seat1.FavorPlayerId, 1, "in either seat");
        var both = JekTable();
        var sh = both.Cards[both.Players[1].StrongholdId]; sh.DefinitionId = Jek; sh.Printed = null; both.Players[1].Clan = "Imperial";
        MatchEngine.ApplyImperialSetup(both);
        Check(both.FavorPlayerId is null, "if the other player has an Imperial Stronghold too, neither begins with the Favor");
        var neither = Batch336Support.Table(Phase.Dynasty, 0);
        MatchEngine.ApplyImperialSetup(neither);
        Check(neither.FavorPlayerId is null, "without a Journey's End Keep nobody is given the Favor");
        // The two real setups (the published match and custom practice) call it; the rule is not a test fixture's.
        var root = Batch336Support.RepoRoot();
        foreach (var file in new[] { "PublishedMatch.cs", "CustomPractice.cs" })
            Check(File.ReadAllText(Path.Combine(root, "src/L5R.Core", file)).Contains("ApplyImperialSetup(state)", StringComparison.Ordinal), file + " applies Journey's End Keep's setup");
    }

    private static MatchEngine LockTable(out string stronghold, out CardInstance holding, out CardInstance first, out CardInstance second)
    {
        var s = JekTable();
        stronghold = s.Players[0].StrongholdId;
        holding = Province(s, 0, 0, "training-mine-holding", ThreeGoldHolding);
        first = Province(s, 0, 1, Washi);
        second = Province(s, 0, 2, Suikihime);
        for (var i = 0; i < 6; i++) Add(s, Marketplace, 0, Zone.Home);
        return MatchEngine.FromTestState(s);
    }

    private static void LockLiftedByExcess()
    {
        var e = LockTable(out var jek, out var holding, out var first, out _);
        Check(e.QuotePurchase(0, first.Id).Error is null, "before JEK produces Gold nothing is locked");
        var option = JekOnly(e, 3, jek);
        Eq(option.Produced, 5, "JEK produces 5 Gold for the 3 Gold Holding");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: holding.Id, SourceIds: option.SourceIds));
        Eq(e.State.Cards[holding.Id].Zone, Zone.Home, "the Holding is in play");
        Check(e.State.Cards[jek].Bowed, "JEK bowed to produce");
        Eq(e.State.Wave771Locks![0][2], 5, "all 5 Gold counts as spent: the 3 for the Holding and the 2 of excess");
        var quote = e.QuotePurchase(0, first.Id);
        Check(quote.Error is null, "Personalities may be recruited thereafter: " + quote.Error);
    }

    private static void LockHeldUntilSpent()
    {
        var s = JekTable();
        var jek = s.Players[0].StrongholdId;
        var first = Province(s, 0, 1, Demiyah); var second = Province(s, 0, 2, Suikihime); var holding = Province(s, 0, 0, "training-mine-holding", FiveGoldHolding); // Demiyah costs 4: JEK's 5 Gold can pay it alone
        for (var i = 0; i < 6; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var price = e.QuotePurchase(0, first.Id).Cost;
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: first.Id, SourceIds: JekOnly(e, price, jek).SourceIds));
        Eq(e.State.Cards[first.Id].Zone, Zone.Home, "JEK may pay for the Personality it produced for");
        Eq(e.State.Wave771Locks![0][2], 0, "the recruit itself is not Gold spent on non-Recruit costs");
        var locked = e.QuotePurchase(0, second.Id);
        Check(locked.Error is not null && locked.Error.Contains("Journey's End Keep", StringComparison.Ordinal), "no other Personality may be recruited: " + locked.Error);
        var before = JsonSerializer.Serialize(e.State);
        var refused = e.Execute(new GameCommand("purchase", 0, e.Version, CardId: second.Id, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
        Check(!refused.Accepted && JsonSerializer.Serialize(e.State) == before, "a refused recruit changes nothing");
        // Spend 5 Gold elsewhere: the Holding is a non-Recruit payment.
        var payment = e.PaymentOptions(0, 5).First(o => !o.SourceIds.Contains(jek));
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: holding.Id, SourceIds: payment.SourceIds));
        Check(e.State.Wave771Locks![0][2] >= Wave771Abilities.LockGold, "5 Gold has now been spent on a non-Recruit cost");
        Check(e.QuotePurchase(0, second.Id).Error is null, "the lock is lifted");
    }

    private static void LockLapses()
    {
        var s = JekTable();
        var jek = s.Players[0].StrongholdId;
        var first = Province(s, 0, 1, Demiyah); var second = Province(s, 0, 2, Suikihime);
        var theirs = Province(s, 1, 0, Washi);
        for (var i = 0; i < 6; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: first.Id, SourceIds: JekOnly(e, e.QuotePurchase(0, first.Id).Cost, jek).SourceIds));
        Check(e.QuotePurchase(0, second.Id).Error is not null, "locked this phase");
        // The other player is never bound by it.
        var theirEngine = MatchEngine.FromTestState(Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; }));
        Check(theirEngine.QuotePurchase(1, theirs.Id).Error is null, "the other player's recruits are not locked");
        // The lock is for the phase: a later phase, and a later turn, are free.
        var later = MatchEngine.FromTestState(Edit(e, st => { st.Phase = Phase.DynastyDiscard; }));
        Check(later.QuotePurchase(0, second.Id).Error is null, "a later phase is free");
        var nextTurn = MatchEngine.FromTestState(Edit(e, st => st.TurnNumber++));
        Check(nextTurn.QuotePurchase(0, second.Id).Error is null, "a later turn is free");
        // Without a Journey's End Keep no lock is ever opened.
        var plain = Batch336Support.Table(Phase.Dynasty, 0);
        var card = Province(plain, 0, 1, Demiyah); var other = Province(plain, 0, 2, Suikihime);
        for (var i = 0; i < 6; i++) Add(plain, Marketplace, 0, Zone.Home);
        var control = MatchEngine.FromTestState(plain);
        Accept(control, new GameCommand("purchase", 0, control.Version, CardId: card.Id, SourceIds: control.PaymentOptions(0, control.QuotePurchase(0, card.Id).Cost).First().SourceIds));
        Check(control.State.Wave771Locks is null, "no lock record without a Journey's End Keep");
        Check(control.QuotePurchase(0, other.Id).Error is null, "and nothing is refused");
    }

    private static MatchState Edit(MatchEngine e, Action<MatchState> edit) { var state = e.State; edit(state); return state; }
}
