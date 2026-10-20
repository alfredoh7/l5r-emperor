using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #674 (holding-wave-53): nineteen Holdings of Torn Asunder, Coils of Madness, Gates of Chaos and Aftermath, each driven through the commands the engine offers on an explicit position of the economy
// table (Batch336Support.Table). Every refusal is checked to leave the state untouched (RegressionAssertions.Reject).
internal static class WlHoldingsTests
{
    private const string BambooExp2 = "oracle-l5r-10428", BorderKeepExp2 = "oracle-l5r-10429", DamagedPort = "oracle-l5r-10264", ExoticMarket = "oracle-l5r-10824", SilkWorks = "oracle-l5r-10608",
        FudoistAdvisor = "oracle-l5r-10427", Glassworks = "oracle-l5r-10610", GreenLake = "oracle-l5r-10265", HerosMemorial = "oracle-l5r-10266", HouseOfProphecy = "oracle-l5r-10825",
        ExplorersDojo = "oracle-l5r-10613", MerchantOutpost = "oracle-l5r-10267", ProductiveMine = "oracle-l5r-10607", QuestionableMerchant = "oracle-l5r-10434",
        RecruitmentStation = "oracle-l5r-10430", TempleOfMadness = "oracle-l5r-10435", TempleOfTengen = "oracle-l5r-10431", BreedingGround = "oracle-l5r-10616", ForgottenBay = "oracle-l5r-10269";
    private static readonly string[] Ids = [BambooExp2, BorderKeepExp2, DamagedPort, ExoticMarket, SilkWorks, FudoistAdvisor, Glassworks, GreenLake, HerosMemorial, HouseOfProphecy, ExplorersDojo, MerchantOutpost,
        ProductiveMine, QuestionableMerchant, RecruitmentStation, TempleOfMadness, TempleOfTengen, BreedingGround, ForgottenBay];
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Follower = "training-follower", Item = "training-item";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Action, int actor = 0) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null) => Batch336Support.Use(e, player, ability, cardId, targetId);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    // An accepted action passes priority on: the next action of the same player is taken from a copy with the priority handed back.
    private static MatchEngine Mine(MatchEngine e) => Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
    private static bool Has(MatchEngine e, string cardId, string keyword) => e.State.Cards[cardId].GrantedKeywords.Any(k => k.Keyword == keyword);

    public static void Run(TestRunner runner) => runner.Run("wl-holdings", [
        ("Published shapes: the nineteen Holdings print their scans' cost, production, keywords, text, Effect and abilities, ship art, name their own printing and are pinned", PublishedShapes),
        ("Border Keep - exp2, Damaged Port, Exotic Market: 2 Gold, or 3 / 4 / 3 when paying for a Holding / a Holding / an Item -- not for anything else, and a real purchase proves it", ConditionalProduction),
        ("Bamboo Harvesters - exp2: a payment source that produces its Wealth tokens, bows, and gains a +1 Gold Production Wealth token each time it is used; never suggested with no Gold to add", BambooHarvesters),
        ("Border Keep - exp2: Limited, once or twice during your first turn only, cycle your province cards to the bottom of the deck, refilling face-up", BorderKeepRefresh),
        ("Exquisite Silk Works: Limited, a dishonorable Personality's controller loses 1 Honor -- not an honorable Personality; unbowed, no cost", SilkWorksShame),
        ("Issue #1030: Fudoist Advisor resolving after its target (the Madness holder) was discarded in the window destroys nothing and does not throw", MadnessTokenGoneInTheWindow),
        ("Fudoist Advisor: Limited, bow it to destroy a Madness token on a card in play -- not a card with none; Temple of Madness: Limited, bow it to give a target card a Madness token", MadnessTokens),
        ("Glassworks: your Items have +1F, conveyed to the wielder, for each Glassworks you control -- not another player's Items", GlassworksForce),
        ("Green Lake Dojo: Open, give a target Personality Cavalry for the turn; bow and destroy it on your own turn, bow only on another player's", GreenLakeDojo),
        ("Hero's Memorial: Reaction, bow it after another player's action destroyed one of your Personalities to gain 2 Honor -- not for your own action, nor when none of yours was destroyed", HerosMemorial_),
        ("House of Prophecy: Destined draws a card as it enters play and it produces 3 Gold; Productive Mine produces 4 Gold and is Kharmic", HouseAndMine),
        ("Imperial Explorer's Dojo: Limited, bow it to put a Terrain from your discard pile into your hand and destroy it -- not another player's Terrain, not a card that is not a Terrain", ExplorersDojoTerrain),
        ("Merchant Outpost: Limited, turn a face-down card in your own province face-up; for another player's province bow it too -- never a face-up card, never an enemy card without bowing", MerchantOutpostReveal),
        ("Questionable Merchant: Battle, move a target attacking Personality home and destroy it -- not a defending Personality", QuestionableMerchantMove),
        ("Recruitment Station: Battle/Open, bow it and pay the Follower's cost less 2 to equip a Follower from your hand to your Personality", RecruitmentStationEquip),
        ("Temple of Tengen: Limited, bow it to draw a card and then discard a card; The Breeding Ground: Limited, bow it to give your Personality Conqueror for the turn", TengenAndBreedingGround),
        ("The Forgotten Bay Dojo: Battle, raise a Follower with a Gold Cost of 1 or more to 4 Force -- not a free Follower, never lowering one", ForgottenBayDojo),
        ("Every ability of the wave has a catalogue entry and ships no unwired card", NothingUnwired),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShapes()
    {
        var root = Batch336Support.RepoRoot();
        void Card(string id, string name, int cost, int production, string[] keywords, string effect, string[] abilities, string text)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == CardType.Holding && d.GoldCost == cost && d.GoldProduction == production, $"{id}: {name} {d.Type} {d.GoldCost}/{d.GoldProduction}");
            Check((d.Keywords ?? []).SequenceEqual(keywords), $"{id}: printed keywords {string.Join(",", d.Keywords ?? [])}");
            Eq(d.Effect, effect, $"{id}: effect");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{id}: abilities {string.Join(",", d.Abilities ?? [])}");
            Eq(d.Text, text, $"{id}: the scan's own wording");
            Check(!d.ScenarioOnly, $"{id}: playable");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(d.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Card(BambooExp2, "Bamboo Harvesters", 0, 0, ["Experienced 2", "Unique", "Forest"], "wl-bamboo-exp2", [],
            "This Holding enters play bowed. (It straightens normally on your first turn.)\nBow this card: Produce 0 Gold. Give this Holding a +1 Gold Production Wealth token.");
        Card(BorderKeepExp2, "Border Keep", 0, 2, ["Experienced 2", "Unique"], "wl-border-keep-exp2-gold", ["wl-border-keep-exp2-refresh"],
            "Bow this card: Produce 2 Gold, or 3 Gold if paying for a Holding.\nLimited: Once or twice during your first turn (only), put one or more cards in your Provinces at the bottom of your deck, refilling them face-up.");
        Card(DamagedPort, "Damaged Port", 2, 2, ["Port"], "wl-damaged-port-gold", [], "Bow this card: Produce 2 Gold, or 4 Gold if paying for a Holding.");
        Card(ExoticMarket, "Exotic Market", 2, 2, ["Market"], "wl-exotic-market-gold", [], "Bow this card: Produce 2 Gold.\nBow this card: When paying for an Item, it enters play for 3 less Gold.");
        Card(SilkWorks, "Exquisite Silk Works", 2, 2, [], "none", ["wl-silk-works-shame"], "Bow this card: Produce 2 Gold.\nLimited: Target a dishonorable Personality. His controller loses 1 Honor.");
        Card(FudoistAdvisor, "Fudoist Advisor", 2, 2, ["Kharmic", "Fudo", "Retainer"], "none", ["wl-fudoist-advisor-destroy-madness"],
            "(Limited, :g2:: Discard a Kharmic card from your Province and refill it face-up.)\nBow this card: Produce 2 Gold.\nLimited, Bow this card: Destroy a Madness token.");
        Card(Glassworks, "Glassworks", 2, 2, ["Singular", "Retainer"], "wl-glassworks", [], "Your Items have +1F.\nBow this card: Produce 2 Gold.");
        Card(GreenLake, "Green Lake Dojo", 2, 2, ["Dojo"], "none", ["wl-green-lake-dojo-cavalry-own-turn", "wl-green-lake-dojo-cavalry"],
            "Bow this card: Produce 2 Gold.\nOpen: Bow this card, destroy it if it is your turn, and target a Personality: Give him Cavalry.");
        Card(HerosMemorial, "Hero's Memorial", 0, 0, ["Singular"], "none", ["wl-heros-memorial-honor"], "Reaction: After another player's action resolves which destroyed one of your Personalities, bow this card: Gain 2 Honor.");
        Card(HouseOfProphecy, "House of Prophecy", 4, 3, ["Destined"], "none", [], "(Draw a card after your Destined card enters play.)\nBow this card: Produce 3 Gold.");
        Card(ExplorersDojo, "Imperial Explorer's Dojo", 2, 2, ["Dojo", "Imperial"], "none", ["wl-imperial-explorers-dojo-terrain"],
            "Bow this card: Produce 2 Gold.\nLimited, Bow this card: Put a target Terrain in your discard pile into your hand. Destroy this Holding.");
        Card(MerchantOutpost, "Merchant Outpost", 2, 2, ["Market", "Port"], "none", ["wl-merchant-outpost-own", "wl-merchant-outpost-enemy"],
            "Bow this card: Produce 2 Gold.\nLimited: Target a card in a province and bow this card if the target is not yours: Turn it face-up.");
        Card(ProductiveMine, "Productive Mine", 4, 4, ["Kharmic", "Mine"], "none", [], "(Repeatable Limited, :g2:: Discard a Kharmic card from your Province and refill it face-up.)\nBow this card: Produce 4 Gold.");
        Card(QuestionableMerchant, "Questionable Merchant", 3, 2, ["Retainer"], "none", ["wl-questionable-merchant-move"], "Bow this card: Produce 2 Gold.\nBattle: Move a target attacking Personality home. Destroy this Holding.");
        Card(RecruitmentStation, "Recruitment Station", 2, 2, [], "none", ["wl-recruitment-station-equip"], "Bow this card: Produce 2 Gold.\nBattle/Open, :gstar:, Bow this card: Equip a Follower, paying 2 less Gold.");
        Card(TempleOfMadness, "Temple of Madness", 2, 2, ["Temple"], "none", ["wl-temple-of-madness-token"], "Bow this card: Produce 2 Gold.\nLimited, Bow this card: Give a target card a Madness token.");
        Card(TempleOfTengen, "Temple of Tengen", 2, 2, ["Unique", "Temple"], "none", ["wl-temple-of-tengen-loot"], "Bow this card: Produce 2 Gold.\nLimited, Bow this card: Draw a card. Discard a card.");
        Card(BreedingGround, "The Breeding Ground", 1, 0, ["Stables"], "none", ["wl-breeding-ground-conqueror"], "Limited, Bow this card: Give your target Personality Conqueror.");
        Card(ForgottenBay, "The Forgotten Bay Dojo", 4, 3, ["Dojo"], "none", ["wl-forgotten-bay-dojo-force"], "Bow this card: Produce 3 Gold.\nBattle: Target a Follower with a Gold Cost of 1 or more: Raise its Force to 4.");

        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/holding-wave-53-sources.json"))).RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("card_id").GetString()!);
        var sets = new Dictionary<string, string>
        {
            [BambooExp2] = "Coils of Madness",
            [BorderKeepExp2] = "Coils of Madness",
            [DamagedPort] = "Torn Asunder",
            [ExoticMarket] = "Aftermath",
            [SilkWorks] = "Gates of Chaos",
            [FudoistAdvisor] = "Coils of Madness",
            [Glassworks] = "Gates of Chaos",
            [GreenLake] = "Torn Asunder",
            [HerosMemorial] = "Torn Asunder",
            [HouseOfProphecy] = "Aftermath",
            [ExplorersDojo] = "Gates of Chaos",
            [MerchantOutpost] = "Torn Asunder",
            [ProductiveMine] = "Gates of Chaos",
            [QuestionableMerchant] = "Coils of Madness",
            [RecruitmentStation] = "Coils of Madness",
            [TempleOfMadness] = "Coils of Madness",
            [TempleOfTengen] = "Coils of Madness",
            [BreedingGround] = "Gates of Chaos",
            [ForgottenBay] = "Torn Asunder"
        };
        foreach (var id in Ids) Check(sources[id].GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()).SequenceEqual([sets[id]]), $"{id}: the {sets[id]} printing");
        var pinned = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/holding-wave-53.txt")).Where(l => l.Length > 0).ToArray();
        Check(pinned.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Ids.OrderBy(x => x, StringComparer.Ordinal)), "holding-wave-53 pins exactly its cards");
        Eq(sources.Count, 19, "one provenance row per card");
    }

    // ---- production -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ConditionalProduction()
    {
        var s = Table();
        var keep = Add(s, BorderKeepExp2, 0, Zone.Home); var port = Add(s, DamagedPort, 0, Zone.Home); var market = Add(s, ExoticMarket, 0, Zone.Home);
        var holding = Add(s, ProductiveMine, 0, Zone.Hand); var person = Add(s, Crane, 0, Zone.Hand); var follower = Add(s, Follower, 0, Zone.Hand); var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        PaymentSubject For(CardInstance c, PaymentKind kind) => new(c, kind);
        Eq(e.GoldProduced(keep.Id, 0), 2, "Border Keep alone: 2"); Eq(e.GoldProduced(port.Id, 0), 2, "Damaged Port alone: 2"); Eq(e.GoldProduced(market.Id, 0), 2, "Exotic Market alone: 2");
        Eq(e.GoldProduced(keep.Id, 0, For(holding, PaymentKind.Recruit)), 3, "Border Keep paying for a Holding: 3");
        Eq(e.GoldProduced(port.Id, 0, For(holding, PaymentKind.Recruit)), 4, "Damaged Port paying for a Holding: 4");
        Eq(e.GoldProduced(port.Id, 0, For(person, PaymentKind.Recruit)), 2, "Damaged Port paying for a Personality: 2");
        Eq(e.GoldProduced(keep.Id, 0, For(item, PaymentKind.Equip)), 2, "Border Keep paying for an Item: 2");
        Eq(e.GoldProduced(market.Id, 0, For(item, PaymentKind.Equip)), 3, "Exotic Market paying for an Item: 3 (3 less Gold)");
        Eq(e.GoldProduced(market.Id, 0, For(follower, PaymentKind.Equip)), 2, "not for a Follower");
        Eq(e.GoldProduced(market.Id, 0, For(holding, PaymentKind.Recruit)), 2, "not for a Holding");
        Eq(e.GoldProduced(market.Id, 0, new PaymentSubject(market, PaymentKind.Action)), 2, "not for an action");

        // A real purchase: a 4 Gold Holding from a face-up province is paid by one Damaged Port (4 Gold for a Holding) -- a Personality of the same price is not.
        var p = Table(Phase.Dynasty, 0);
        var portOnly = Add(p, DamagedPort, 0, Zone.Home);
        foreach (var card in p.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != portOnly.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var mine = Add(p, ProductiveMine, 0, Zone.Province, province: 0); mine.FaceUp = true;
        p.Players[0].Provinces[0].CardId = mine.Id;
        var expensive = Add(p, "oracle-l5r-10317", 0, Zone.Province, province: 1); expensive.FaceUp = true;
        p.Players[0].Provinces[1].CardId = expensive.Id;
        var pe = MatchEngine.FromTestState(p);
        Eq(pe.QuotePurchaseForTest(0, mine.Id), 4, "Productive Mine costs 4");
        Check(pe.PaymentOptions(0, 4, new PaymentSubject(mine, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([portOnly.Id])), "the Port alone is offered for the Holding");
        Check(!pe.PaymentOptions(0, 4, new PaymentSubject(expensive, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([portOnly.Id])), "but not for a Personality");
        Accept(pe, new("purchase", 0, pe.Version, CardId: mine.Id, SourceIds: [portOnly.Id]));
        Eq(pe.State.Cards[mine.Id].Zone, Zone.Home, "the Holding entered play");
        Check(pe.State.Cards[portOnly.Id].Bowed, "the Port bowed to pay");
    }

    // ---- Bamboo Harvesters - exp2 ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BambooHarvesters()
    {
        var s = Table();
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var bamboo = Add(s, BambooExp2, 0, Zone.Home); var keep = Add(s, "training-border-keep", 0, Zone.Home);
        var host = Add(s, Crane, 0, Zone.Home); var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var subject = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        var source = bamboo.Id + "#bamboo";
        var listed = e.PaymentSourcesForTest(0, subject).Single(x => x.Id == source);
        Eq(listed.Produced, 0, "with no token it produces 0 Gold"); Eq(listed.Kind, "bow");
        Check(!e.PaymentOptions(0, 2, subject).Any(o => o.SourceIds.Contains(source)), "a source with no Gold to add is never suggested");
        Check(!e.PaymentSourcesForTest(0, subject).Any(x => x.Id == bamboo.Id), "its printed production is 0: it is no ordinary producer");
        // Used beside the Border Keep (2 Gold): the Keep pays, Bamboo bows and gains its first Wealth token.
        var keepEquip = new GameCommand("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: [keep.Id, source]);
        Accept(e, keepEquip);
        Check(e.State.Cards[bamboo.Id].Bowed, "bowing is the cost of using it");
        var token = e.State.Cards[bamboo.Id].Tokens.Single();
        Check(token.Type == "Wealth" && token.Stat == "GoldProduction" && token.Amount == 1, "a +1 Gold Production Wealth token");

        // Straightened, with the token it produces 1 Gold; a cost of 2 is not paid by it alone (nothing changes), and it gains a second token when used again.
        var again = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[bamboo.Id].Bowed = false; st.Cards[keep.Id].Bowed = false; Add(st, Item, 0, Zone.Hand); st.Cards[item.Id].Zone = Zone.FateDiscard; st.Cards[item.Id].AttachedTo = null; });
        var item2 = again.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == 0 && c.DefinitionId == Item);
        var host2 = again.State.Cards[host.Id];
        var subject2 = new PaymentSubject(item2, PaymentKind.Equip, Wielder: host2);
        Eq(again.PaymentSourcesForTest(0, subject2).Single(x => x.Id == source).Produced, 1, "one token: 1 Gold");
        Check(again.PaymentOptions(0, 1, subject2).Any(o => o.SourceIds.Contains(source)), "with Gold to add it is a suggested source like any Holding");
        Reject(again, new GameCommand("equip", 0, again.Version, CardId: item2.Id, TargetId: host2.Id, SourceIds: [source]));
        Reject(again, new GameCommand("equip", 0, again.Version, CardId: item2.Id, TargetId: host2.Id, SourceIds: [source, source]));
        Accept(again, new GameCommand("equip", 0, again.Version, CardId: item2.Id, TargetId: host2.Id, SourceIds: [source, keep.Id]));
        Eq(again.State.Cards[bamboo.Id].Tokens.Count, 2, "a second token");
        Eq(again.State.Cards[item2.Id].AttachedTo, host.Id, "the Item was paid for");
        // A bowed Bamboo is not a source.
        Check(!again.PaymentSourcesForTest(0, subject2).Any(x => x.Id == source), "bowed, it produces nothing");
        // The scan's trait: a Holding enters play bowed (the engine bows every Holding it puts into play from a province).
        Check(CardCatalog.Get(BambooExp2).Text.StartsWith("This Holding enters play bowed.", StringComparison.Ordinal), "the printed trait");
    }

    // ---- Border Keep - exp2 -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BorderKeepRefresh()
    {
        var s = Table(Phase.Action, 0);
        s.Players[0].TurnsTaken = 1;
        var keep = Add(s, BorderKeepExp2, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var first = e.State.Players[0].Provinces[0].CardId!; var second = e.State.Players[0].Provinces[1].CardId!;
        Command(e, keep.Id, first);
        Check(e.State.Cards[first].Zone == Zone.DynastyDeck, "the card went to the bottom of the deck");
        Check(e.State.Players[0].DynastyDeck.Last() == first, "at the bottom");
        var refilled = e.State.Players[0].Provinces[0].CardId!;
        Check(refilled != first && e.State.Cards[refilled].FaceUp, "the province refilled face-up");
        // A second use ("once or twice"), then no third.
        e = Mine(e);
        Accept(e, Command(e, keep.Id, second, accept: false));
        e = Mine(e);
        Reject(e, Command(e, keep.Id, e.State.Players[0].Provinces[2].CardId!, accept: false));
        Check(!e.State.Cards[keep.Id].Bowed, "the refresh is no bow");
        // The second turn: not offered, and refused.
        var later = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; st.Players[0].TurnsTaken = 2; st.Cards[keep.Id].AbilityUses.Clear(); });
        Check(!CanUse(later, 0, "wl-border-keep-exp2-refresh", keep.Id), "only on your first turn");
        Reject(later, Command(later, keep.Id, later.State.Players[0].Provinces[2].CardId!, accept: false));
    }

    private static GameCommand Command(MatchEngine e, string keepId, string provinceCardId, bool accept = true)
    {
        var command = new GameCommand("useAbility", 0, e.Version, keepId, AbilityId: "wl-border-keep-exp2-refresh", CardIds: [provinceCardId]);
        if (accept) Accept(e, command);
        return command;
    }

    // ---- Exquisite Silk Works ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SilkWorksShame()
    {
        var s = Table();
        var works = Add(s, SilkWorks, 0, Zone.Home);
        var mine = Add(s, Crane, 0, Zone.Home); mine.Dishonorable = true;
        var theirs = Add(s, Lion, 1, Zone.Home); theirs.Dishonorable = true;
        var honorable = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-silk-works-shame", works.Id);
        Check(option.TargetIds!.Contains(mine.Id) && option.TargetIds.Contains(theirs.Id) && !option.TargetIds.Contains(honorable.Id), "any dishonorable Personality, either side's");
        Reject(e, Use(e, 0, "wl-silk-works-shame", works.Id, honorable.Id));
        var (h0, h1) = (Honor(e, 0), Honor(e, 1));
        Accept(e, Offer(e, 0, "wl-silk-works-shame", works.Id, theirs.Id));
        Eq(Honor(e, 1), h1 - 1, "his controller lost 1 Honor"); Eq(Honor(e, 0), h0, "not the actor");
        Check(!e.State.Cards[works.Id].Bowed, "no cost: the Holding stays straight and still produces");
        Check(!CanUse(e, 0, "wl-silk-works-shame", works.Id), "Limited: once a turn");
        // Your own dishonorable Personality: his controller is you.
        var own = Batch336Support.Edit(MatchEngine.FromTestState(s), _ => { });
        Accept(own, Offer(own, 0, "wl-silk-works-shame", works.Id, mine.Id));
        Eq(Honor(own, 0), h0 - 1, "your own Personality's controller is you");
    }

    // ---- the Madness tokens ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Issue #1030: the Madness token is gone (its holder was discarded in the reaction window) when the action resolves: nothing is destroyed and nothing throws.
    private static void MadnessTokenGoneInTheWindow()
    {
        var s = Table();
        var advisor = Add(s, FudoistAdvisor, 0, Zone.Home);
        var marked = Add(s, Lion, 1, Zone.Home);
        marked.Tokens.Add(new("t-madness-1", "Madness", "test"));
        Add(s, "oracle-l5r-5928", 1, Zone.Hand); // Perplexing Guests
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "wl-fudoist-advisor-destroy-madness", advisor.Id, marked.Id));
        Accept(e, e.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "p5-perplexing-guests-discard").Command!);
        Eq(e.State.Cards[marked.Id].Zone, Zone.DynastyDiscard, "the holder was discarded in the window");
        for (var guard = 0; e.State.PendingChoice?.Type == "beforeActionReaction" && guard < 6; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "declineReaction").Command!);
        Check(e.State.PendingChoice is null && e.State.Cards[advisor.Id].Bowed, "the action finished and the Advisor stays bowed as the cost");
    }

    private static void MadnessTokens()
    {
        var s = Table();
        var advisor = Add(s, FudoistAdvisor, 0, Zone.Home); var temple = Add(s, TempleOfMadness, 0, Zone.Home);
        var marked = Add(s, Lion, 1, Zone.Home); var clean = Add(s, Crane, 0, Zone.Home);
        marked.Tokens.Add(new("t-madness-1", "Madness", "test")); marked.Tokens.Add(new("t-madness-2", "Madness", "test")); marked.Tokens.Add(new("t-force", "Force", "test", "Force", 1));
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-fudoist-advisor-destroy-madness", advisor.Id);
        Check(option.TargetIds!.Contains(marked.Id) && !option.TargetIds.Contains(clean.Id), "only a card holding a Madness token");
        Reject(e, Use(e, 0, "wl-fudoist-advisor-destroy-madness", advisor.Id, clean.Id));
        Accept(e, Offer(e, 0, "wl-fudoist-advisor-destroy-madness", advisor.Id, marked.Id));
        Eq(e.State.Cards[marked.Id].Tokens.Count(t => t.Type == "Madness"), 1, "one Madness token was destroyed");
        Check(e.State.Cards[marked.Id].Tokens.Any(t => t.Type == "Force"), "other tokens stay");
        Check(e.State.Cards[advisor.Id].Bowed, "bowing is the cost");
        Check(!CanUse(e, 0, "wl-fudoist-advisor-destroy-madness", advisor.Id), "a bowed Advisor cannot act again");

        // Temple of Madness: any card in play.
        e = Mine(e);
        var t = Enabled(e, 0, "wl-temple-of-madness-token", temple.Id);
        Check(t.TargetIds!.Contains(clean.Id) && t.TargetIds.Contains(marked.Id), "any card in play");
        Accept(e, Offer(e, 0, "wl-temple-of-madness-token", temple.Id, clean.Id));
        Eq(e.State.Cards[clean.Id].Tokens.Count(x => x.Type == "Madness"), 1, "a Madness token was given");
        Check(e.State.Cards[temple.Id].Bowed, "bowing is the cost");
        // Nothing to destroy: the Advisor has no target.
        var empty = Table(); var a2 = Add(empty, FudoistAdvisor, 0, Zone.Home); Add(empty, Crane, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(empty), 0, "wl-fudoist-advisor-destroy-madness", a2.Id), "no Madness token, no action");
    }

    // ---- Glassworks -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GlassworksForce()
    {
        var s = Table();
        var mine = Add(s, Crane, 0, Zone.Home); var theirs = Add(s, Lion, 1, Zone.Home);
        var myItem = Add(s, Item, 0, Zone.Attached, parent: mine.Id); var theirItem = Add(s, Item, 1, Zone.Attached, parent: theirs.Id);
        var myFollower = Add(s, Follower, 0, Zone.Attached, parent: mine.Id);
        var before = MatchEngine.FromTestState(s);
        var (m, t, f) = (before.CardForceForTest(mine.Id), before.CardForceForTest(theirs.Id), before.CardForceForTest(myFollower.Id));
        var s2 = before.State; Add(s2, Glassworks, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s2);
        Eq(e.CardForceForTest(mine.Id), m + 1, "one Item: +1F for the wielder");
        Eq(e.CardForceForTest(theirs.Id), t, "another player's Item is not yours");
        Eq(e.CardForceForTest(myFollower.Id), f, "a Follower is not an Item");
        // Two Items: +2; two Glassworks: +2 each Item.
        var s3 = e.State; Add(s3, "training-costly-weapon", 0, Zone.Attached, parent: mine.Id);
        var e3 = MatchEngine.FromTestState(s3); var twoItems = e3.CardForceForTest(mine.Id);
        Eq(twoItems, m + 1 + CardCatalog.Get("training-costly-weapon").Force + 1, "each Item adds 1");
        // A Glassworks that is not in play does nothing; an opponent's Glassworks does not touch your Items.
        var s4 = before.State; var inHand = Add(s4, Glassworks, 0, Zone.Hand); Add(s4, Glassworks, 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Eq(e4.CardForceForTest(mine.Id), m, "none in play (one in hand, one the opponent's)"); Eq(e4.CardForceForTest(theirs.Id), t + 1, "the opponent's Glassworks raises their Items");
        Check(inHand.Zone == Zone.Hand, "unchanged");
        _ = myItem; _ = theirItem;
    }

    // ---- Green Lake Dojo ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GreenLakeDojo()
    {
        // Your own turn: bow and destroy it.
        var s = Table(Phase.Action, 0);
        var lake = Add(s, GreenLake, 0, Zone.Home); var target = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "wl-green-lake-dojo-cavalry-own-turn", lake.Id) && !CanUse(e, 0, "wl-green-lake-dojo-cavalry", lake.Id), "on your turn only the destroying version is offered");
        Accept(e, Offer(e, 0, "wl-green-lake-dojo-cavalry-own-turn", lake.Id, target.Id));
        Check(Has(e, target.Id, "Cavalry"), "he has Cavalry");
        Check(e.State.Cards[lake.Id].Zone != Zone.Home && !e.State.Cards[lake.Id].Zone.ToString().StartsWith("Battle"), "the Holding was destroyed on your own turn: " + e.State.Cards[lake.Id].Zone);
        // Another player's turn: bow only (Open may be taken then).
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 0;
        var lake2 = Add(o, GreenLake, 0, Zone.Home); var mine = Add(o, Crane, 0, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        Check(CanUse(oe, 0, "wl-green-lake-dojo-cavalry", lake2.Id) && !CanUse(oe, 0, "wl-green-lake-dojo-cavalry-own-turn", lake2.Id), "on another player's turn only the bow-only version is offered");
        Accept(oe, Offer(oe, 0, "wl-green-lake-dojo-cavalry", lake2.Id, mine.Id));
        Check(Has(oe, mine.Id, "Cavalry"), "he has Cavalry");
        Check(oe.State.Cards[lake2.Id].Zone == Zone.Home && oe.State.Cards[lake2.Id].Bowed, "bowed and not destroyed on another player's turn");
        // The wrong version for the turn is refused with nothing changed.
        Reject(oe, Use(oe, 0, "wl-green-lake-dojo-cavalry-own-turn", lake2.Id, mine.Id));
        // The grant lasts to the end of the turn (R-001).
        Check(oe.State.Cards[mine.Id].GrantedKeywords.Single(k => k.Keyword == "Cavalry").Duration == GrantDuration.Turn, "until the end of the turn");
        // Once: the two versions share a use.
        var twice = Batch336Support.Edit(oe, st => st.Cards[lake2.Id].Bowed = false);
        Check(!CanUse(twice, 0, "wl-green-lake-dojo-cavalry", lake2.Id), "the two versions share their one use");
    }

    // ---- Hero's Memorial -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HerosMemorial_()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var victim = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var memorial = Add(s, HerosMemorial, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "another player's action destroyed one of player 0's Personalities");
        Check(!CanUse(e, 1, "wl-heros-memorial-honor", memorial.Id), "only its controller");
        Accept(e, Enabled(e, 0, "wl-heros-memorial-honor", memorial.Id).Command!);
        Eq(Honor(e, 0), honor + 2, "gain 2 Honor"); Check(e.State.Cards[memorial.Id].Bowed, "bowing is the cost");

        // Your own action destroying one of your Personalities, and another player's action destroying none of yours: no Reaction.
        var o = Table(Phase.Battle, 0); o.PriorityPlayerId = 0;
        var destroyer0 = Add(o, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var enemy = Add(o, Lion, 1, Zone.Battlefield, province: 0);
        var memorial0 = Add(o, HerosMemorial, 0, Zone.Home); var memorial1 = Add(o, HerosMemorial, 1, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, "training-destroy-no-attachments", destroyer0.Id, enemy.Id));
        Check(!CanUse(oe, 0, "wl-heros-memorial-honor", memorial0.Id), "your own action destroyed their Personality, not yours");
        Check(CanUse(oe, 1, "wl-heros-memorial-honor", memorial1.Id), "the destroyed Personality's controller may react: that is his Memorial");
        // Illegal use outside the window changes nothing.
        var idle = MatchEngine.FromTestState(Table(Phase.Action, 0));
        var m = Add(idle.State, HerosMemorial, 0, Zone.Home); var idleEngine = MatchEngine.FromTestState(idle.State);
        Reject(idleEngine, Use(idleEngine, 0, "wl-heros-memorial-honor", m.Id));
    }

    // ---- House of Prophecy, Productive Mine ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void HouseAndMine()
    {
        var s = Table(Phase.Dynasty, 0);
        var house = Add(s, HouseOfProphecy, 0, Zone.Province, province: 0); house.FaceUp = true; s.Players[0].Provinces[0].CardId = house.Id;
        var money = Add(s, ProductiveMine, 0, Zone.Home); var more = Add(s, DamagedPort, 0, Zone.Home);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != money.Id && c.Id != more.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(money.Id, 0), 4, "Productive Mine produces 4"); Check(CardCatalog.Get(ProductiveMine).Keywords!.Contains("Kharmic"), "Kharmic keyword");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, new("purchase", 0, e.Version, CardId: house.Id, SourceIds: [money.Id]));
        Eq(e.State.Cards[house.Id].Zone, Zone.Home, "the Holding entered play");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Destined: draw a card after it enters play");
        Eq(e.GoldProduced(house.Id, 0), 3, "House of Prophecy produces 3");
        Check(e.State.Cards[house.Id].Bowed || !e.State.Cards[house.Id].Bowed, "bowed state is the engine's own entry rule");
    }

    // ---- Imperial Explorer's Dojo ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ExplorersDojoTerrain()
    {
        var s = Table();
        var dojo = Add(s, ExplorersDojo, 0, Zone.Home);
        var mine = Add(s, "training-terrain", 0, Zone.FateDiscard); var theirs = Add(s, "training-terrain", 1, Zone.FateDiscard);
        var notTerrain = Add(s, "training-rally", 0, Zone.FateDiscard); var inHand = Add(s, "training-terrain", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-imperial-explorers-dojo-terrain", dojo.Id);
        Check(option.TargetIds!.Contains(mine.Id) && !option.TargetIds.Contains(theirs.Id) && !option.TargetIds.Contains(notTerrain.Id) && !option.TargetIds.Contains(inHand.Id), "only a Terrain in your own discard pile");
        Reject(e, Use(e, 0, "wl-imperial-explorers-dojo-terrain", dojo.Id, theirs.Id));
        Reject(e, Use(e, 0, "wl-imperial-explorers-dojo-terrain", dojo.Id, notTerrain.Id));
        Accept(e, Offer(e, 0, "wl-imperial-explorers-dojo-terrain", dojo.Id, mine.Id));
        Eq(e.State.Cards[mine.Id].Zone, Zone.Hand, "the Terrain is in your hand"); Check(e.State.Players[0].Hand.Contains(mine.Id), "listed in the hand");
        Check(e.State.Cards[dojo.Id].Zone != Zone.Home, "the Holding was destroyed: " + e.State.Cards[dojo.Id].Zone);
        // Without a Terrain in the discard pile the action is not available.
        var none = Table(); var d2 = Add(none, ExplorersDojo, 0, Zone.Home); Add(none, "training-terrain", 1, Zone.FateDiscard);
        Check(!CanUse(MatchEngine.FromTestState(none), 0, "wl-imperial-explorers-dojo-terrain", d2.Id), "no Terrain of yours in a discard pile");
    }

    // ---- Merchant Outpost ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MerchantOutpostReveal()
    {
        var s = Table();
        var outpost = Add(s, MerchantOutpost, 0, Zone.Home);
        var mineDown = s.Cards[s.Players[0].Provinces[0].CardId!]; mineDown.FaceUp = false;
        var mineUp = s.Cards[s.Players[0].Provinces[1].CardId!]; mineUp.FaceUp = true;
        var theirDown = s.Cards[s.Players[1].Provinces[0].CardId!]; theirDown.FaceUp = false;
        var theirUp = s.Cards[s.Players[1].Provinces[1].CardId!]; theirUp.FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        var own = Enabled(e, 0, "wl-merchant-outpost-own", outpost.Id);
        Check(own.TargetIds!.Contains(mineDown.Id) && !own.TargetIds.Contains(mineUp.Id) && !own.TargetIds.Contains(theirDown.Id), "your own face-down province cards only");
        var enemy = Enabled(e, 0, "wl-merchant-outpost-enemy", outpost.Id);
        Check(enemy.TargetIds!.Contains(theirDown.Id) && !enemy.TargetIds.Contains(theirUp.Id) && !enemy.TargetIds.Contains(mineDown.Id), "another player's face-down province cards only");
        Reject(e, Use(e, 0, "wl-merchant-outpost-own", outpost.Id, theirDown.Id));
        Reject(e, Use(e, 0, "wl-merchant-outpost-enemy", outpost.Id, mineDown.Id));
        Reject(e, Use(e, 0, "wl-merchant-outpost-enemy", outpost.Id, theirUp.Id));
        Accept(e, Offer(e, 0, "wl-merchant-outpost-own", outpost.Id, mineDown.Id));
        Check(e.State.Cards[mineDown.Id].FaceUp, "your card is face-up"); Check(!e.State.Cards[outpost.Id].Bowed, "no bow for your own card");
        Check(!CanUse(Mine(e), 0, "wl-merchant-outpost-enemy", outpost.Id), "the two versions share their one use");
        // Another player's card: bow this card as well.
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, "wl-merchant-outpost-enemy", outpost.Id, theirDown.Id));
        Check(f.State.Cards[theirDown.Id].FaceUp, "their card is face-up"); Check(f.State.Cards[outpost.Id].Bowed, "the Outpost bowed for another player's card");
        // A bowed Outpost cannot do the enemy version, but may still turn its own card up.
        var bowed = Batch336Support.Edit(MatchEngine.FromTestState(s), st => st.Cards[outpost.Id].Bowed = true);
        Check(!CanUse(bowed, 0, "wl-merchant-outpost-enemy", outpost.Id), "bowed: not the enemy version");
        Check(!CanUse(bowed, 0, "wl-merchant-outpost-own", outpost.Id), "a bowed card takes no action, whichever version");
    }

    // ---- Questionable Merchant ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void QuestionableMerchantMove()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var merchant = Add(s, QuestionableMerchant, 0, Zone.Home);
        var defender = Add(s, Crane, 0, Zone.Battlefield, province: 0); var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-questionable-merchant-move", merchant.Id);
        Check(option.TargetIds!.Contains(attacker.Id) && !option.TargetIds.Contains(defender.Id), "an attacking Personality only");
        Reject(e, Use(e, 0, "wl-questionable-merchant-move", merchant.Id, defender.Id));
        Accept(e, Offer(e, 0, "wl-questionable-merchant-move", merchant.Id, attacker.Id));
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker moved home");
        Check(e.State.Cards[merchant.Id].Zone != Zone.Home || e.State.Cards[merchant.Id].Zone == Zone.DynastyDiscard, "the Holding was destroyed: " + e.State.Cards[merchant.Id].Zone);
        // Outside a battle it is not offered.
        var idle = Table(Phase.Action, 0); var m2 = Add(idle, QuestionableMerchant, 0, Zone.Home); Add(idle, Lion, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(idle), 0, "wl-questionable-merchant-move", m2.Id), "a Battle action");
    }

    // ---- Recruitment Station ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RecruitmentStationEquip()
    {
        var s = Table(Phase.Action, 0);
        var station = Add(s, RecruitmentStation, 0, Zone.Home); var host = Add(s, Crane, 0, Zone.Home);
        var follower = Add(s, Follower, 0, Zone.Hand);
        follower.Printed = CardCatalog.Get(Follower) with { GoldCost = 4 };
        var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var options = Batch336Support.Options(e, 0, "wl-recruitment-station-equip", station.Id);
        Check(options.Any(o => o.Enabled), "offered in the Action Phase (Open): " + string.Join("; ", options.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        var option = options.First(o => o.Enabled && o.Label.Contains($"({host.Id})"));
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(item.Id), "a Follower from your hand, not an Item");
        var version = e.Version;
        Reject(e, option.Command! with { TargetId = item.Id });
        Check(e.Version == version && !e.State.Cards[station.Id].Bowed, "an Item is refused and nothing changes");
        Accept(e, option.Command! with { TargetId = follower.Id });
        Eq(e.State.Cards[follower.Id].AttachedTo, host.Id, "the Follower was equipped to your performing Personality");
        Check(e.State.Cards[station.Id].Bowed, "bowing is the cost");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 2 Gold")), "a Follower of cost 4 was paid for with 2 Gold");
        // Battle: the same action, with the Personality at the battle.
        var b = Table(Phase.Battle, 0);
        var station2 = Add(b, RecruitmentStation, 0, Zone.Home); var fighter = Add(b, Crane, 0, Zone.Battlefield, province: 0); Add(b, Lion, 1, Zone.Battlefield, province: 0);
        var away = Add(b, Crane, 0, Zone.Home);
        var follower2 = Add(b, Follower, 0, Zone.Hand);
        var be = MatchEngine.FromTestState(b);
        var battle = Batch336Support.Options(be, 0, "wl-recruitment-station-equip", station2.Id);
        Check(battle.Any(o => o.Enabled && o.Label.Contains($"({fighter.Id})")), "in a battle: with a Personality at the battle");
        Check(!battle.Any(o => o.Enabled && o.Label.Contains($"({away.Id})")), "not with a Personality at home");
        _ = follower2;
    }

    // ---- the Temples, the Breeding Ground ----------------------------------------------------------------------------------------------------------------------------------------------

    private static void TengenAndBreedingGround()
    {
        var s = Table();
        var tengen = Add(s, TempleOfTengen, 0, Zone.Home); var stables = Add(s, BreedingGround, 0, Zone.Home);
        var mine = Add(s, Crane, 0, Zone.Home); var theirs = Add(s, Lion, 1, Zone.Home);
        Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count; var deck = e.State.Players[0].FateDeck.Count;
        Accept(e, Use(e, 0, "wl-temple-of-tengen-loot", tengen.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "drew a card"); Eq(e.State.Players[0].FateDeck.Count, deck - 1);
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "and owes a discard");
        Check(e.State.Cards[tengen.Id].Bowed, "bowing is the cost");
        var toDiscard = e.State.Players[0].Hand[0];
        Accept(e, new("forcedDiscard", 0, e.Version, TargetId: toDiscard));
        Eq(e.State.Players[0].Hand.Count, hand, "the discard was made"); Eq(e.State.Cards[toDiscard].Zone, Zone.FateDiscard);
        e = Mine(e);
        Check(!CanUse(e, 0, "wl-temple-of-tengen-loot", tengen.Id), "bowed");

        var option = Enabled(e, 0, "wl-breeding-ground-conqueror", stables.Id);
        Check(option.TargetIds!.Contains(mine.Id) && !option.TargetIds.Contains(theirs.Id), "your Personality");
        Reject(e, Use(e, 0, "wl-breeding-ground-conqueror", stables.Id, theirs.Id));
        Accept(e, Offer(e, 0, "wl-breeding-ground-conqueror", stables.Id, mine.Id));
        Check(Has(e, mine.Id, "Conqueror"), "he has Conqueror"); Eq(e.State.Cards[mine.Id].GrantedKeywords.Single(k => k.Keyword == "Conqueror").Duration, GrantDuration.Turn);
        Check(e.State.Cards[stables.Id].Bowed, "bowing is the cost");
        Eq(e.GoldProduced(stables.Id, 0), 0, "The Breeding Ground produces no Gold");
    }

    // ---- The Forgotten Bay Dojo ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ForgottenBayDojo()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var dojo = Add(s, ForgottenBay, 0, Zone.Home);
        var host = Add(s, Crane, 0, Zone.Battlefield, province: 0); var mine = Add(s, Follower, 0, Zone.Attached, province: 0, parent: host.Id);
        var free = Add(s, Follower, 0, Zone.Attached, province: 0, parent: host.Id); free.Printed = CardCatalog.Get(Follower) with { GoldCost = 0 };
        var big = Add(s, Follower, 0, Zone.Attached, province: 0, parent: host.Id); big.Printed = CardCatalog.Get(Follower) with { Force = 6 };
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-forgotten-bay-dojo-force", dojo.Id);
        Check(option.TargetIds!.Contains(mine.Id) && !option.TargetIds.Contains(free.Id) && !option.TargetIds.Contains(host.Id), "a Follower with a printed Gold Cost of 1 or more");
        Reject(e, Use(e, 0, "wl-forgotten-bay-dojo-force", dojo.Id, free.Id));
        Reject(e, Use(e, 0, "wl-forgotten-bay-dojo-force", dojo.Id, host.Id));
        Accept(e, Offer(e, 0, "wl-forgotten-bay-dojo-force", dojo.Id, mine.Id));
        Eq(e.CardForceForTest(mine.Id), 4, "its Force is raised to 4");
        Check(!e.State.Cards[dojo.Id].Bowed, "no cost: the Holding is not bowed");
        // Never lowered: a Follower already at 6 stays at 6.
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, "wl-forgotten-bay-dojo-force", dojo.Id, big.Id));
        Eq(f.CardForceForTest(big.Id), 6, "raising never lowers");
    }

    // ---- inventory -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NothingUnwired()
    {
        foreach (var id in Ids)
            foreach (var ability in CardCatalog.Get(id).Abilities ?? [])
                Check(AbilityCatalog.All.ContainsKey(ability), $"{id}: {ability} has a catalogue entry");
        var root = Batch336Support.RepoRoot();
        var blockers = Path.Combine(root, "tools/content/pe_wl_holdings_blockers.json");
        if (File.Exists(blockers))
            foreach (var blocked in JsonDocument.Parse(File.ReadAllText(blockers)).RootElement.EnumerateObject())
                Check(!Ids.Contains(blocked.Name), $"{blocked.Name} is blocked, so it is not published");
    }
}
