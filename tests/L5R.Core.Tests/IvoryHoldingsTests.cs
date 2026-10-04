using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #622, holding-wave-48: the earliest Emperor-era printings of seventeen Holdings (IvoryHoldings.cs, ClanGold.cs), driven through QueryActions and the commands it offers.
internal static partial class IvoryHoldingsTests
{
    private const string CopperMine = "oracle-l5r-1497", GeishaHouse = "oracle-l5r-2784", IronMine = "oracle-l5r-3808", KobunePort = "oracle-l5r-4496", ShinomenMarsh = "oracle-l5r-6984",
        SilverMine = "oracle-l5r-7266", Stables = "oracle-l5r-7443", FarmersMarket = "oracle-l5r-10033", TempleOfHarmony = "oracle-l5r-7874", VastPaddyFields = "oracle-l5r-10271",
        MerchantAtoll = "oracle-l5r-5027", JiramusCourt = "oracle-l5r-10189", ShrineToHachiman = "oracle-l5r-7223", HeavyInfantryDojo = "oracle-l5r-3044", RicePaddy = "oracle-l5r-6314",
        FamilyLibrary = "oracle-l5r-2463", DeedsAndWords = "oracle-l5r-1914";
    private const string GoldMine = "oracle-l5r-2867", Marketplace = "oracle-l5r-4840", HachisRevenge = "oracle-l5r-10034", TempleOfHotei = "oracle-l5r-7875", TsujikenExp = "oracle-l5r-10045",
        ShamateKeep = "oracle-l5r-6691";

    private static readonly string[] AllIds = [CopperMine, GeishaHouse, IronMine, KobunePort, ShinomenMarsh, SilverMine, Stables, FarmersMarket, TempleOfHarmony, VastPaddyFields, MerchantAtoll,
        JiramusCourt, ShrineToHachiman, HeavyInfantryDojo, RicePaddy, FamilyLibrary, DeedsAndWords];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    public static void Run(TestRunner runner) => runner.Run("ivory-holdings", [
        ("Published shape: the seventeen Holdings print the scans' text, stats, keywords, Effect and ability ids, ship their art, name their Emperor-era printing and are in holding-wave-48's manifest", PublishedShape),
        ("Clan Gold: Copper Mine, Geisha House, Iron Mine, Kobune Port, Shinomen Marsh, Silver Mine and Stables produce 3 Gold for their Clan and 2 for any other; Gold Mine, Marketplace, Hachi's Revenge and the training Marketplace are unchanged", ClanGold),
        ("Temple of Harmony: produces 3 Gold when paying to recruit a Monk, otherwise 2 -- never for an action of a Monk or a card that is not a Monk", TempleOfHarmony_),
        ("Farmer's Market: its controller's maximum hand size is +1 while it is in play (bowed or not), stacking per copy; not the opponent's, not out of play", FarmersMarketHandSize),
        ("Merchant Atoll: its controller's provinces have +1 strength while it is in play, stacking per copy; not the opponent's, not out of play; produces 5 Gold", MerchantAtollProvinces),
        ("Vast Paddy Fields: a Farm Holding it helped pay for refills its province face-up -- not when other Holdings paid, not for a Holding that is not a Farm, and an empty Dynasty deck refills nothing", VastPaddyRefill),
        ("Jiramu's Court: Open, bow it and target your Courtier to straighten him -- not another player's, not a non-Courtier, not while bowed, not a Courtier that may not be straightened", JiramusCourtStraighten),
        ("Shrine to Hachiman: Battle/Open, target an attachment (either player's) to straighten it, once per turn -- not a Personality, and it does not bow the Shrine", HachimanStraighten),
        ("Heavy Infantry Dojo: Battle, bow it and choose your performing Personality to give him +2F -- not outside a battle, not while bowed, not another player's Personality", DojoBoost),
        ("Rice Paddy: Battle, bow and destroy it to bow an attacking Follower at one of your provinces' battlefields -- only the Defender, not a defending or non-battle Follower, not while bowed", RicePaddyBow),
        ("Family Library: Limited, destroy it to discard a face-up card in one of your provinces and refill it face-up with one of your discarded (not dead) Personalities", FamilyLibraryRefill),
        ("Deeds and Words: before an Honor gain or loss from an action's effects during the Action Phase, destroy it to reduce that change to 0 -- not in another phase, not unplayed, not while bowed, not for an action with no Honor change", DeedsAndWordsReduce),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Holding(string id, string name, int cost, int production, string effect, string[] abilities, string text, params string[] keywords)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Holding && c.Name == name && c.GoldCost == cost && c.GoldProduction == production, $"{name}: type={c.Type} cost={c.GoldCost} production={c.GoldProduction}");
            Eq(c.Effect, effect, $"{name}'s effect");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), string.Join(",", keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}'s keywords");
            Eq(c.Text, text, $"{name} prints the scan's text");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg", $"{name}'s art key");
        }
        Holding(CopperMine, "Copper Mine", 2, 2, "lion-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Lion Clan player.", "Mine");
        Holding(GeishaHouse, "Geisha House", 2, 2, "scorpion-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Scorpion Clan player.", "Geisha House");
        Holding(IronMine, "Iron Mine", 2, 2, "crab-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Crab Clan player.", "Mine");
        Holding(KobunePort, "Kobune Port", 2, 2, "mantis-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Mantis Clan player.", "Port");
        Holding(ShinomenMarsh, "Shinomen Marsh", 2, 2, "spider-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Spider Clan player.", "Swamp");
        Holding(SilverMine, "Silver Mine", 2, 2, "phoenix-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Phoenix Clan player.", "Mine");
        Holding(Stables, "Stables", 2, 2, "unicorn-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if you are a Unicorn Clan player.", "Stables");
        Holding(FarmersMarket, "Farmer's Market", 2, 2, "increase-own-hand-size", [], "Your maximum hand size is increased by 1.\nBow this card: Produce 2 Gold.", "Farm", "Market");
        Holding(TempleOfHarmony, "Temple of Harmony", 2, 2, "temple-of-harmony-gold", [], "Bow this card: Produce 2 Gold, or 3 Gold if paying for a Monk.", "Temple");
        Holding(VastPaddyFields, "Vast Paddy Fields", 2, 2, "vast-paddy-fields", [], "Bow this card: Produce 2 Gold. If this paid for a Farm Holding entering play from a province, refill the province face-up.", "Farm");
        Holding(MerchantAtoll, "Merchant Atoll", 6, 5, "merchant-atoll-provinces", [], "Your provinces have +1 strength.\nBow this card: Produce 5 Gold.", "Castle", "Port");
        Holding(JiramusCourt, "Jiramu's Court", 2, 2, "none", ["jiramus-court-straighten"], "Bow this card: Produce 2 Gold.\nOpen: Bow this card and target your Courtier: Straighten him.");
        Holding(ShrineToHachiman, "Shrine to Hachiman", 2, 2, "none", ["shrine-hachiman-straighten"], "Bow this card: Produce 2 Gold.\nBattle/Open: Target an attachment: Straighten it.", "Temple");
        Holding(HeavyInfantryDojo, "Heavy Infantry Dojo", 4, 3, "none", ["heavy-infantry-dojo-boost"], "Bow this card: Produce 3 Gold.\nBattle: Choose your performing Personality and bow this card: Give him +2F.", "Dojo");
        Holding(RicePaddy, "Rice Paddy", 2, 2, "none", ["rice-paddy-bow"], "Bow this card: Produce 2 Gold.\nBattle: Bow and destroy this card, and target an attacking Follower at one of your provinces' battlefields: Bow it.", "Farm");
        Holding(FamilyLibrary, "Family Library", 2, 2, "none", ["family-library-refill"],
            "Bow this card: Produce 2 Gold.\nLimited: Destroy this card: Discard a face-up card in one of your provinces, refilling it with a discarded (not dead) Personality from your discard pile.", "Library");
        Holding(DeedsAndWords, "Deeds and Words", 2, 2, "none", ["deeds-words-reduce"],
            "Bow this card: Produce 2 Gold.\nReaction: Before the resolution of an action during the Action Phase, destroy this card: Choose one of the Honor gains or losses from its effects. Reduce it to 0.");

        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/holding-wave-48.txt"));
        Eq(listed.Length, 17, "the manifest lists seventeen cards");
        foreach (var id in AllIds)
        {
            Check(listed.Contains(id), $"{id} is in the holding-wave-48 manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
        }
        // Each card names the Emperor-era printing its scan was read from (never a later reprint), and the receipt's hash is recorded.
        var sources = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "content/published/holding-wave-48-sources.json")));
        var sets = sources.EnumerateArray().ToDictionary(r => r.GetProperty("card_id").GetString()!, r => string.Join(",", r.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()!.Replace("&ndash;", "-"))));
        var expected = new Dictionary<string, string>
        {
            [CopperMine] = "War of Honor",
            [GeishaHouse] = "War of Honor",
            [IronMine] = "Emperor Edition",
            [KobunePort] = "Emperor Edition",
            [ShinomenMarsh] = "Emperor Edition",
            [SilverMine] = "War of Honor",
            [Stables] = "Emperor Edition",
            [FarmersMarket] = "Seeds of Decay",
            [TempleOfHarmony] = "War of Honor",
            [VastPaddyFields] = "Torn Asunder",
            [MerchantAtoll] = "Promotional-Celestial",
            [JiramusCourt] = "Promotional-Emperor",
            [ShrineToHachiman] = "Emperor Edition",
            [HeavyInfantryDojo] = "Promotional-Celestial",
            [RicePaddy] = "Honor and Treachery",
            [FamilyLibrary] = "War of Honor",
            [DeedsAndWords] = "Emperor Edition",
        };
        foreach (var (id, set) in expected) Eq(sets[id], set, $"{id}'s printing");
        foreach (var ability in new[] { "jiramus-court-straighten", "shrine-hachiman-straighten", "heavy-infantry-dojo-boost", "rice-paddy-bow", "family-library-refill", "deeds-words-reduce" })
            Check(AbilityCatalog.All.ContainsKey(ability), $"{ability} is registered");
    }

    // ---- Clan Gold ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly string[] Clans = ["Crane", "Crab", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn"];

    private static void ClanGold()
    {
        var cards = new (string Id, string Clan)[] { (CopperMine, "Lion"), (GeishaHouse, "Scorpion"), (IronMine, "Crab"), (KobunePort, "Mantis"), (ShinomenMarsh, "Spider"), (SilverMine, "Phoenix"), (Stables, "Unicorn"),
            (GoldMine, "Dragon"), (Marketplace, "Crane"), (HachisRevenge, "Crane"), ("training-marketplace", "Crane") };
        foreach (var (id, clan) in cards)
        {
            foreach (var playerClan in Clans)
            {
                var s = Table(Phase.Action, 0);
                s.Players[0].Clan = playerClan;
                var holding = Add(s, id, 0, Zone.Home);
                var e = MatchEngine.FromTestState(s);
                Eq(e.GoldProduced(holding.Id, 0), playerClan == clan ? 3 : 2, $"{CardCatalog.Get(id).Name} for a {playerClan} Clan player");
            }
        }
        // The one table the engine, Stolen Merchandise and the training opponent share.
        Eq(ClanGoldEffects.ClanOf("lion-gold"), "Lion", "lion-gold names the Lion"); Eq(ClanGoldEffects.ClanOf("none"), null, "no Clan for another Effect");
        Eq(ClanGoldEffects.Bonus("crab-gold", "Crab"), 1, "the Clan's bonus"); Eq(ClanGoldEffects.Bonus("crab-gold", "Crane"), 0, "no bonus for another Clan");
        // A payment really uses the bonus: three Gold from a Spider player's Shinomen Marsh alone.
        var s2 = Table(Phase.Action, 0);
        s2.Players[0].Clan = "Spider";
        foreach (var c in s2.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        var marsh = Add(s2, ShinomenMarsh, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.PaymentOptions(0, 3).Any(o => o.SourceIds.SequenceEqual([marsh.Id]) && o.Produced == 3), "the Marsh alone pays 3 Gold for a Spider");
        s2.Players[0].Clan = "Lion";
        Check(!MatchEngine.FromTestState(s2).PaymentOptions(0, 3).Any(o => o.SourceIds.SequenceEqual([marsh.Id])), "but not for a Lion");
    }

    // ---- Temple of Harmony -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TempleOfHarmony_()
    {
        var monkDefinition = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && (d.Keywords ?? []).Contains("Monk"));
        var s = Table(Phase.Dynasty, 0);
        var temple = Add(s, TempleOfHarmony, 0, Zone.Home);
        var monk = Add(s, monkDefinition.Id, 0, Zone.Province, province: 1);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Province, province: 2);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(temple.Id, 0), 2, "unspecified: the printed 2 Gold");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(e.State.Cards[monk.Id], PaymentKind.Recruit)), 3, "3 Gold paying to recruit a Monk");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(e.State.Cards[samurai.Id], PaymentKind.Recruit)), 2, "2 Gold paying to recruit a Personality that is not a Monk");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(e.State.Cards[monk.Id], PaymentKind.Action)), 2, "2 Gold for an action of a Monk: it is not paying for him");
        // The offered recruit payment uses the extra Gold: a recruit of the Monk is payable by the Temple plus Gold summing to his cost.
        var options = e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[monk.Id], PaymentKind.Recruit));
        Check(options.Any(o => o.SourceIds.SequenceEqual([temple.Id]) && o.Produced == 3), "the Temple alone pays 3 for a Monk");
        Check(!e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[samurai.Id], PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([temple.Id])), "but not 3 for another Personality");
    }

    // ---- Farmer's Market ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FarmersMarketHandSize()
    {
        var s = Table(Phase.Action, 0);
        var baseline = MatchEngine.FromTestState(s).MaxHandSizeForTest(0);
        var market = Add(s, FarmersMarket, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), baseline + 1, "+1 maximum hand size");
        Eq(e.MaxHandSizeForTest(1), baseline, "not the opponent's");
        s.Cards[market.Id].Bowed = true;
        Eq(MatchEngine.FromTestState(s).MaxHandSizeForTest(0), baseline + 1, "bowed or not");
        Add(s, FarmersMarket, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s).MaxHandSizeForTest(0), baseline + 2, "each copy adds 1");
        Add(s, FarmersMarket, 1, Zone.Home);
        var both = MatchEngine.FromTestState(s);
        Eq(both.MaxHandSizeForTest(1), baseline + 1, "the opponent's own Market is his");
        s.Cards[market.Id].Zone = Zone.DynastyDiscard;
        Eq(MatchEngine.FromTestState(s).MaxHandSizeForTest(0), baseline + 1, "a Market out of play gives nothing");
    }

    // ---- Merchant Atoll ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MerchantAtollProvinces()
    {
        var s = Table(Phase.Action, 0);
        var plain = MatchEngine.FromTestState(s);
        var own = s.Players[0].Provinces.Select(p => plain.ProvinceStrengthForTest(0, p.Index)).ToArray();
        var theirs = s.Players[1].Provinces.Select(p => plain.ProvinceStrengthForTest(1, p.Index)).ToArray();
        var atoll = Add(s, MerchantAtoll, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        for (var i = 0; i < own.Length; i++) Eq(e.ProvinceStrengthForTest(0, s.Players[0].Provinces[i].Index), own[i] + 1, $"province {i + 1} has +1 strength");
        for (var i = 0; i < theirs.Length; i++) Eq(e.ProvinceStrengthForTest(1, s.Players[1].Provinces[i].Index), theirs[i], $"the opponent's province {i + 1} is unchanged");
        Eq(e.GoldProduced(atoll.Id, 0), 5, "it produces 5 Gold");
        Add(s, MerchantAtoll, 0, Zone.Home, bowed: true);
        Eq(MatchEngine.FromTestState(s).ProvinceStrengthForTest(0, 0), own[0] + 2, "each Atoll adds 1, bowed or not");
        s.Cards[atoll.Id].Zone = Zone.DynastyDiscard;
        Eq(MatchEngine.FromTestState(s).ProvinceStrengthForTest(0, 0), own[0] + 1, "a destroyed Atoll no longer counts");
    }

    // ---- Vast Paddy Fields ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine DynastyRig(string provinceDefinition, out string provinceCardId, out CardInstance paddy, out CardInstance other, bool emptyDeck = false)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        provinceCardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[provinceCardId].DefinitionId = provinceDefinition; s.Cards[provinceCardId].FaceUp = true;
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        paddy = Add(s, VastPaddyFields, 0, Zone.Home);
        other = Add(s, JiramusCourt, 0, Zone.Home); // a plain 2-Gold Holding
        if (emptyDeck) { foreach (var id in s.Players[0].DynastyDeck.ToArray()) s.Cards.Remove(id); s.Players[0].DynastyDeck.Clear(); }
        return MatchEngine.FromTestState(s);
    }

    private static void VastPaddyRefill()
    {
        // A Farm Holding paid for with Vast Paddy Fields: the province refills face-up.
        var e = DynastyRig(RicePaddy, out var farmId, out var paddy, out var other);
        var deckTop = e.State.Players[0].DynastyDeck[0];
        Eq(e.QuotePurchase(0, farmId).Cost, 2, "Rice Paddy costs 2");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: farmId, SourceIds: [paddy.Id]));
        Eq(e.State.Cards[farmId].Zone, Zone.Home, "Rice Paddy entered play");
        var refill = e.State.Players[0].Provinces[0].CardId;
        Eq(refill, deckTop, "the province refilled from the top of the Dynasty deck");
        Check(e.State.Cards[refill!].FaceUp, "face-up, since Vast Paddy Fields paid for the Farm");
        Check(e.State.Events.Any(x => x.Type == "provinceRefilled" && x.Message.StartsWith("Vast Paddy Fields")), "the log says why");
        Check(!e.State.Cards[farmId].AbilityUses.ContainsKey("vast-paddy-fields:paid"), "the note is spent");

        // Paid for by another Holding: the ordinary face-down refill.
        var e2 = DynastyRig(RicePaddy, out var farm2, out _, out var other2);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: farm2, SourceIds: [other2.Id]));
        Check(!e2.State.Cards[e2.State.Players[0].Provinces[0].CardId!].FaceUp, "another Holding paid: refilled face-down");

        // Not a Farm: Vast Paddy Fields paid, but Jiramu's Court is no Farm.
        var e3 = DynastyRig(JiramusCourt, out var court3, out var paddy3, out _);
        Accept(e3, new GameCommand("purchase", 0, e3.Version, CardId: court3, SourceIds: [paddy3.Id]));
        Check(!e3.State.Cards[e3.State.Players[0].Provinces[0].CardId!].FaceUp, "not a Farm Holding: refilled face-down");

        // Not a Holding: a Personality it helped pay for.
        var e4 = DynastyRig("training-crane-samurai", out var samurai4, out var paddy4, out var other4);
        CardInstance third = null!;
        e4 = Edit(e4, st => third = Add(st, "training-marketplace", 0, Zone.Home));
        var cost4 = e4.QuotePurchase(0, samurai4).Cost;
        Check(cost4 > 2 && cost4 <= 6, $"the Personality costs {cost4}, payable by three 2-Gold Holdings");
        Accept(e4, new GameCommand("purchase", 0, e4.Version, CardId: samurai4, SourceIds: [paddy4.Id, other4.Id, third.Id]));
        Eq(e4.State.Cards[samurai4].Zone, Zone.Home, "the Personality entered play");
        Check(!e4.State.Cards[e4.State.Players[0].Provinces[0].CardId!].FaceUp, "a Personality: refilled face-down");

        // An empty Dynasty deck: nothing to refill, nothing to turn.
        var e5 = DynastyRig(RicePaddy, out var farm5, out var paddy5, out _, emptyDeck: true);
        Accept(e5, new GameCommand("purchase", 0, e5.Version, CardId: farm5, SourceIds: [paddy5.Id]));
        Check(e5.State.Players[0].Provinces[0].CardId is null, "no card to refill the province with");

        // A Vast Paddy Fields that is not in play cannot have paid: a purchase with a source the engine rejects changes nothing.
        var e6 = DynastyRig(RicePaddy, out var farm6, out var paddy6, out _);
        var before = e6.State.Players[0].Provinces[0].CardId;
        var bowed = Edit(e6, st => st.Cards[paddy6.Id].Bowed = true);
        Rejection(bowed, new GameCommand("purchase", 0, bowed.Version, CardId: farm6, SourceIds: [paddy6.Id]));
        Eq(bowed.State.Players[0].Provinces[0].CardId, before, "a refused purchase leaves the province alone");
    }

    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    // ---- Jiramu's Court ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JiramusCourtStraighten()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var court = Add(s, JiramusCourt, 0, Zone.Home);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var other = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var enemy = Kw(Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true), "Courtier");
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "jiramus-court-straighten", court.Id);
        Check(option.TargetIds!.Contains(courtier.Id) && !option.TargetIds.Contains(other.Id) && !option.TargetIds.Contains(enemy.Id), "only your own Courtier is a target: " + string.Join(",", option.TargetIds!));
        Check(Rejection(e, Use(e, 0, "jiramus-court-straighten", court.Id, enemy.Id)).Length > 0, "another player's Courtier is refused");
        Check(Rejection(e, Use(e, 0, "jiramus-court-straighten", court.Id, other.Id)).Length > 0, "a non-Courtier is refused");
        Check(e.State.Cards[enemy.Id].Bowed && e.State.Cards[other.Id].Bowed && !e.State.Cards[court.Id].Bowed, "refused actions changed nothing");
        Accept(e, Offer(e, 0, "jiramus-court-straighten", court.Id, courtier.Id));
        Check(!e.State.Cards[courtier.Id].Bowed, "the Courtier straightened");
        Check(e.State.Cards[court.Id].Bowed, "bowing the Court is the cost");
        Check(!CanUse(e, 0, "jiramus-court-straighten", court.Id), "a bowed Court cannot be used again");

        // A Courtier that may not be straightened this turn (Basic Lesson): the action is refused outright.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var court2 = Add(s2, JiramusCourt, 0, Zone.Home);
        var locked = Kw(Add(s2, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        locked.StraightenLockedTurn = s2.TurnNumber;
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "jiramus-court-straighten", court2.Id), "no legal target: the Courtier may not be straightened this turn");
        Check(!e2.State.Cards[court2.Id].Bowed, "and the Court is not bowed");
        // It is an Open action: the other player may take it too, on his own Court.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 1;
        var court3 = Add(s3, JiramusCourt, 1, Zone.Home);
        var courtier3 = Kw(Add(s3, "training-lion-courtier", 1, Zone.Home, bowed: true), "Courtier");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "jiramus-court-straighten", court3.Id, courtier3.Id));
        Check(!e3.State.Cards[courtier3.Id].Bowed, "an Open action: the non-active player straightened his Courtier");
    }

    // ---- Shrine to Hachiman ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HachimanStraighten()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shrine = Add(s, ShrineToHachiman, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: host.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Home);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, parent: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "shrine-hachiman-straighten", shrine.Id);
        Check(option.TargetIds!.Contains(follower.Id) && option.TargetIds.Contains(enemyFollower.Id), "any attachment is a target");
        Check(!option.TargetIds.Contains(host.Id) && !option.TargetIds.Contains(enemyHost.Id), "a Personality is not");
        Check(Rejection(e, Use(e, 0, "shrine-hachiman-straighten", shrine.Id, host.Id)).Length > 0, "a Personality is refused");
        Accept(e, Offer(e, 0, "shrine-hachiman-straighten", shrine.Id, enemyFollower.Id));
        Check(!e.State.Cards[enemyFollower.Id].Bowed, "the attachment straightened");
        Check(!e.State.Cards[shrine.Id].Bowed, "the Shrine pays no bow");
        Check(!CanUse(e, 0, "shrine-hachiman-straighten", shrine.Id), "once per turn");
        // A bowed attachment that may not be straightened this turn makes the action illegal.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var shrine2 = Add(s2, ShrineToHachiman, 0, Zone.Home);
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var locked = Add(s2, "training-follower", 0, Zone.Attached, bowed: true, parent: host2.Id);
        locked.StraightenLockedTurn = s2.TurnNumber;
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "shrine-hachiman-straighten", shrine2.Id), "an attachment that may not be straightened is no target");
        // A Battle action too.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        var shrine3 = Add(s3, ShrineToHachiman, 0, Zone.Home);
        var host3 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower3 = Add(s3, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host3.Id);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "shrine-hachiman-straighten", shrine3.Id, follower3.Id));
        Check(!e3.State.Cards[follower3.Id].Bowed, "straightened in a battle");
    }

    // ---- Heavy Infantry Dojo -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DojoBoost()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var dojo = Add(s, HeavyInfantryDojo, 0, Zone.Home);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var away = Add(s, "training-crane-courtier", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(performer.Id);
        Check(!Options(e, 0, "heavy-infantry-dojo-boost", dojo.Id, away.Id).Any(o => o.Enabled), "a Personality at home is not at the battle");
        Check(!Options(e, 0, "heavy-infantry-dojo-boost", dojo.Id, enemy.Id).Any(o => o.Enabled), "another player's Personality is not yours to choose");
        Accept(e, Enabled(e, 0, "heavy-infantry-dojo-boost", dojo.Id, performer.Id).Command!);
        Eq(e.CardForceForTest(performer.Id), force + 2, "+2F");
        Check(e.State.Cards[dojo.Id].Bowed, "the Dojo bowed as the cost");
        Eq(e.CardForceForTest(enemy.Id), MatchEngine.FromTestState(s).CardForceForTest(enemy.Id), "nobody else changed");
        Check(!CanUse(e, 0, "heavy-infantry-dojo-boost", dojo.Id, performer.Id), "a bowed Dojo cannot be used again");
        // Not outside a battle.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var dojo2 = Add(s2, HeavyInfantryDojo, 0, Zone.Home);
        Add(s2, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "heavy-infantry-dojo-boost", dojo2.Id), "a Battle action: not in the Action Phase");
    }

    // ---- Rice Paddy --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RicePaddyBow()
    {
        // Player 1 attacks province 0; player 0, the Defender, owns the Rice Paddy.
        MatchState Setup(out CardInstance paddy, out CardInstance attackerFollower, out CardInstance defenderFollower, out CardInstance awayFollower, bool bowedPaddy = false)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            paddy = Add(s, RicePaddy, 0, Zone.Home, bowed: bowedPaddy);
            var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            attackerFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: attacker.Id);
            defenderFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: defender.Id);
            var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Home);
            awayFollower = Add(s, "training-follower", 1, Zone.Attached, parent: elsewhere.Id);
            return s;
        }
        var s1 = Setup(out var paddy, out var attackerFollower, out var defenderFollower, out var awayFollower);
        var e = MatchEngine.FromTestState(s1);
        var option = Enabled(e, 0, "rice-paddy-bow", paddy.Id);
        Eq(string.Join(",", option.TargetIds!), attackerFollower.Id, "only the attacking Follower at the battlefield is a target");
        Check(Rejection(e, Use(e, 0, "rice-paddy-bow", paddy.Id, defenderFollower.Id)).Length > 0, "a defending Follower is refused");
        Check(Rejection(e, Use(e, 0, "rice-paddy-bow", paddy.Id, awayFollower.Id)).Length > 0, "a Follower not at the battle is refused");
        Check(!e.State.Cards[paddy.Id].Bowed && e.State.Cards[paddy.Id].Zone == Zone.Home && !e.State.Cards[attackerFollower.Id].Bowed, "refused actions changed nothing");
        Accept(e, Offer(e, 0, "rice-paddy-bow", paddy.Id, attackerFollower.Id));
        Check(e.State.Cards[attackerFollower.Id].Bowed, "the attacking Follower bowed");
        Check(e.State.Cards[paddy.Id].Zone is not (Zone.Home or Zone.Stronghold), "Rice Paddy was destroyed as the cost");

        // The Attacker's own Rice Paddy has no target: its Follower is not at one of his provinces' battlefields.
        var s2 = Setup(out var paddy2, out _, out _, out _);
        paddy2.OwnerId = 1; s2.PriorityPlayerId = 1;
        Check(!CanUse(MatchEngine.FromTestState(s2), 1, "rice-paddy-bow", paddy2.Id), "only the Defender may use it");
        // A bowed Rice Paddy cannot pay the bow.
        var s3 = Setup(out var paddy3, out _, out _, out _, bowedPaddy: true);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "rice-paddy-bow", paddy3.Id), "a bowed Rice Paddy cannot be used");
        // Not in the Action Phase.
        var s4 = Setup(out var paddy4, out _, out _, out _);
        s4.Phase = Phase.Action; s4.CurrentBattleProvince = null; s4.UnresolvedBattlefields = [];
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "rice-paddy-bow", paddy4.Id), "a Battle action: not outside a battle");
    }

    // ---- Family Library ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FamilyLibraryRefill()
    {
        MatchState Setup(out CardInstance library, out CardInstance discarded, out CardInstance dead, out string provinceCard)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            library = Add(s, FamilyLibrary, 0, Zone.Home);
            discarded = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
            dead = Add(s, "training-crane-courtier", 0, Zone.Dead);
            foreach (var province in s.Players[0].Provinces.Where(p => p.CardId is not null)) s.Cards[province.CardId!].FaceUp = false;
            provinceCard = s.Players[0].Provinces[1].CardId!;
            s.Cards[provinceCard].FaceUp = true;
            return s;
        }
        var s1 = Setup(out var library, out var discarded, out var dead, out var provinceCard);
        var e = MatchEngine.FromTestState(s1);
        var option = Enabled(e, 0, "family-library-refill", library.Id);
        Check(option.TargetIds!.Contains(discarded.Id) && !option.TargetIds.Contains(dead.Id), "the discarded Personality is a target, the dead one is not: " + string.Join(",", option.TargetIds!));
        Check(option.Provinces!.Contains(1), "the province holding a face-up card is offered");
        Check(!option.Provinces.Contains(0), "a province holding a face-down card is not");
        var command = Offer(e, 0, "family-library-refill", library.Id, discarded.Id) with { Province = 1 };
        Check(Rejection(e, command with { TargetId = dead.Id }).Length > 0, "a dead Personality is refused");
        Check(Rejection(e, command with { Province = 0 }).Length > 0, "a province with a face-down card is refused");
        Check(e.State.Cards[library.Id].Zone == Zone.Home && e.State.Cards[discarded.Id].Zone == Zone.DynastyDiscard && e.State.Cards[provinceCard].Zone == Zone.Province, "refused actions changed nothing");
        Accept(e, command);
        Eq(e.State.Players[0].Provinces[1].CardId, discarded.Id, "the province was refilled with the Personality");
        Check(e.State.Cards[discarded.Id].Zone == Zone.Province && e.State.Cards[discarded.Id].FaceUp, "face-up");
        Eq(e.State.Cards[provinceCard].Zone, Zone.DynastyDiscard, "the face-up card was discarded");
        Check(e.State.Cards[library.Id].Zone is not (Zone.Home or Zone.Stronghold), "Family Library was destroyed as the cost");
        Eq(e.State.Cards[dead.Id].Zone, Zone.Dead, "the dead Personality stayed dead");
        // No discarded (not dead) Personality: no action.
        var s2 = Setup(out var library2, out var discarded2, out _, out _);
        s2.Cards.Remove(discarded2.Id);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "family-library-refill", library2.Id), "only a dead Personality: nothing to refill with");
        // No face-up card in any province.
        var s3 = Setup(out var library3, out _, out _, out var provinceCard3);
        s3.Cards[provinceCard3].FaceUp = false;
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "family-library-refill", library3.Id), "no face-up province card: nothing to discard");
        // Limited: the active player's own turn only.
        var s4 = Setup(out var library4, out _, out _, out _);
        s4.ActivePlayerId = 1; s4.PriorityPlayerId = 0;
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "family-library-refill", library4.Id), "a Limited action: not on another player's turn");
    }

    // ---- Deeds and Words ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine DishonorRig(out CardInstance deeds, out CardInstance tsujiken, out CardInstance victim, Phase phase = Phase.Action, int deedsOwner = 0)
    {
        // Player 1's Tsujiken - exp dishonors player 0's Personality; the Personality's controller would lose 1 Honor.
        var s = Table(phase, 1); s.PriorityPlayerId = 1;
        deeds = Add(s, DeedsAndWords, deedsOwner, Zone.Home);
        tsujiken = Add(s, TsujikenExp, 1, Zone.Home);
        victim = Add(s, "training-crane-samurai", 0, Zone.Home);
        s.Players[0].Honor = 12; s.Players[1].Honor = 12;
        return MatchEngine.FromTestState(s);
    }

    private static void DeedsAndWordsReduce()
    {
        // The loss, offered and played: reduced to 0 -- everything else of the action still resolves.
        var e = DishonorRig(out var deeds, out var tsujiken, out var victim);
        Accept(e, Offer(e, 1, "tsujiken-exp-dishonor", tsujiken.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the window opens before the Honor loss");
        Check(e.State.PendingHonorChange is { Amount: -1, PlayerId: 0 }, "it names the loss: 1 Honor for player 0");
        Eq(e.State.PriorityPlayerId, 0, "the player with a Reaction to play has the first opportunity");
        Eq(Honor(e, 0), 12, "nothing is applied yet");
        Check(Options(e, 0, "deeds-words-reduce", deeds.Id).Any(o => o.Enabled), "Deeds and Words is on offer");
        Check(!e.QueryActions(1).Any(o => o.Command?.AbilityId == "deeds-words-reduce"), "and only to its controller");
        Accept(e, Enabled(e, 0, "deeds-words-reduce", deeds.Id).Command!);
        Check(e.State.Cards[deeds.Id].Zone is not (Zone.Home or Zone.Stronghold), "Deeds and Words was destroyed as the cost");
        PassWindow(e);
        Eq(Honor(e, 0), 12, "the Honor loss was reduced to 0");
        Check(e.State.Cards[victim.Id].Dishonorable && e.State.Cards[tsujiken.Id].Bowed, "the rest of the action resolved");
        Check(e.State.IvoryHoldings.ReducedHonorChanges.Count == 0, "the mark is spent");
        Check(e.State.Events.Any(x => x.Type == "honorChangeReduced"), "the log says why");

        // Declined: both pass, the loss happens and Deeds and Words stays in play.
        var d = DishonorRig(out var deeds2, out var tsujiken2, out var victim2);
        Accept(d, Offer(d, 1, "tsujiken-exp-dishonor", tsujiken2.Id, victim2.Id));
        PassWindow(d);
        Eq(Honor(d, 0), 11, "declined: the Honor was lost");
        Eq(d.State.Cards[deeds2.Id].Zone, Zone.Home, "and Deeds and Words was not spent");

        // Another player's Honor: it may reduce a change of anyone's -- here the opponent's own gain from Temple of Hotei.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var temple = Add(s3, TempleOfHotei, 0, Zone.Home);
        var shugenja = Add(s3, "training-shugenja", 0, Zone.Home);
        var deeds3 = Add(s3, DeedsAndWords, 1, Zone.Home);
        s3.Players[0].Honor = 10;
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "temple-of-hotei-honor", temple.Id, shugenja.Id).Command!);
        Eq(e3.State.PendingChoice?.Type, "honorChangeReaction", "the opponent's Honor gain opens the window too");
        Check(e3.State.PendingHonorChange is { Amount: 1, PlayerId: 0 }, "a gain of 1 for player 0");
        Accept(e3, Enabled(e3, 1, "deeds-words-reduce", deeds3.Id).Command!);
        PassWindow(e3);
        Eq(Honor(e3, 0), 10, "the gain was reduced to 0");
        Check(e3.State.Cards[shugenja.Id].Bowed, "the cost of the action was still paid");

        // Not during the Action Phase: a Battle-phase action's Honor change offers nothing.
        var s4 = Table(Phase.Battle, 0);
        var keep = s4.Cards[s4.Players[0].StrongholdId]; keep.DefinitionId = ShamateKeep;
        var performer = Add(s4, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var target = Add(s4, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var deeds4 = Add(s4, DeedsAndWords, 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        var honor4 = Honor(e4, 0);
        Accept(e4, Offer(e4, 0, "shamate-move", keep.Id, target.Id, performer.Id));
        Check(e4.State.PendingChoice?.Type != "honorChangeReaction", "no window: it is not the Action Phase");
        Eq(Honor(e4, 0), honor4 + 2, "the gain was applied at once");
        Eq(e4.State.Cards[deeds4.Id].Zone, Zone.Home, "and Deeds and Words is still there");

        // No Honor change, no window: Tsujiken against nothing of the kind -- a plain Open action.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0;
        var shrine = Add(s5, ShrineToHachiman, 0, Zone.Home);
        var host = Add(s5, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s5, "training-follower", 0, Zone.Attached, bowed: true, parent: host.Id);
        Add(s5, DeedsAndWords, 1, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "shrine-hachiman-straighten", shrine.Id, follower.Id));
        Check(e5.State.PendingChoice is null, "an action that changes no Honor opens no window");

        // A bowed Deeds and Words cannot be played (no "even if bowed" is printed).
        var b = DishonorRig(out var deeds6, out var tsujiken6, out var victim6);
        b = Edit(b, st => st.Cards[deeds6.Id].Bowed = true);
        Accept(b, Offer(b, 1, "tsujiken-exp-dishonor", tsujiken6.Id, victim6.Id));
        Check(b.State.PendingChoice?.Type != "honorChangeReaction", "a bowed Deeds and Words offers no Reaction");
        Eq(Honor(b, 0), 11, "so the loss happened");
    }
}
