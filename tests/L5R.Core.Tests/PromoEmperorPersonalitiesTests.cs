using L5R.Core;
using static RegressionAssertions;

// Promotional-Emperor pass (issue #653): fifteen Personalities (personality-wave-123) and three Followers (follower-wave-46) wired from their own scans and printed text.
// Driven only through QueryActions and the commands the engine offers, on explicit positions (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker,
// province 0 the current battlefield). The cards that reuse existing surface are checked for the printed behavior they now carry; the rest for the new rules and their rejections.
internal static class PromoEmperorPersonalitiesTests
{
    private const string ArmedRiceFarmer = "oracle-l5r-10584", Chikara = "oracle-l5r-1319", Hirose = "oracle-l5r-10582", Fudo = "oracle-l5r-2721", Gakku = "oracle-l5r-2765",
        Hikarou = "oracle-l5r-10580", Kataoka = "oracle-l5r-10583", Mahatsu = "oracle-l5r-4796", Barru = "oracle-l5r-10233", Ichigo = "oracle-l5r-9957", Ritisharu = "oracle-l5r-9958",
        Tamahime = "oracle-l5r-10194", TamahimeExpId = "oracle-l5r-10193", Tesskss = "oracle-l5r-9959", Nishima = "oracle-l5r-10579",
        Horse = "oracle-l5r-10588", RiceFarmer = "oracle-l5r-10587", TreasureHunter = "oracle-l5r-9966", Charter = "oracle-l5r-10806", Furugaro = "oracle-l5r-10236";
    private static readonly string[] PersonalityIds = [ArmedRiceFarmer, Chikara, Hirose, Fudo, Gakku, Hikarou, Kataoka, Mahatsu, Barru, Ichigo, Ritisharu, Tamahime, TamahimeExpId, Tesskss, Nishima];
    private static readonly string[] FollowerIds = [Horse, RiceFarmer, TreasureHunter];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, null);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void Tokens(CardInstance card, int count) { for (var n = 0; n < count; n++) card.Tokens.Add(new($"tk-{card.Tokens.Count}-{n}", "Charging", "test-fixture")); }
    private static int TokenCount(MatchEngine e, string id) => e.State.Cards[id].Tokens.Count(t => t.Type == "Charging");

    public static void Run(TestRunner runner) => runner.Run("promo-emperor-personalities", [
        ("Published as printed: stats, keywords, text, abilities and effects of all eighteen cards from their own Promotional-Emperor scans, manifests, art and provenance", PublishedShape),
        ("Armed Rice Farmer: Expendable -- destroyed (not discarded) he draws his controller a card", ArmedRiceFarmerExpendable),
        ("Rice Farmer: the Kharmic keyword's own action discards it from the hand for 2 Gold and draws a card (a Fate card)", RiceFarmerKharmic),
        ("Daigotsu Hirose: entering play costs 1 Honor (mandatory, no window), printed Cavalry", HiroseEntry),
        ("Tess'kss: Battle: bow a target enemy card -- any enemy card, never her own side's", TesskssBow),
        ("Chikara: Battle: destroy a target enemy attachment -- not an own attachment, not a Personality", ChikaraDestroysAttachment),
        ("Gakku: pays 1 less as a Spider Clan player only, loses 3 Honor on entering, Battle: Melee 7 Attack destroys Force 7 or less", GakkuEntryAndMelee),
        ("Seppun Barru: Battle: your Followers in his army get +1F -- his own and other units' at the battlefield, never an enemy's or one at home; not offered away from the battle", BarruFollowers),
        ("Mahatsu: Open: gives himself one of Air, Earth, Fire or Water, once -- the four actions share one use, and the keyword is a plain keyword (Fire counts for Fire cards)", MahatsuElements),
        ("Isawa Hikarou: +1F for each Fire Spell attached to him -- not other Spells, not another Personality's, live as they come and go", HikarouFireSpells),
        ("Seppun Ichigo: +1F for each Dojo you control -- not an opponent's, not Holdings without the keyword", IchigoDojos),
        ("Seppun Ritisharu: your Holdings that bow for Gold produce 1 more -- not the opponent's, not a Holding that produces nothing, not once he leaves play; two of them stack with a payment", RitisharuProduction),
        ("Kataoka: enters play for 1 less Gold for each Farm another player controls -- not his own Farms, never below 0", KataokaFarmDiscount),
        ("Tamahime: after entering she discards her controller's other Sensei Personalities; her other Ronin Personalities have Tactician while she is in play", TamahimeBase),
        ("Tamahime - exp: Sensei discard on entering and on overlaying, Tactician to other Ronin, other Ronin cards enter 1 Gold cheaper, Battle: destroy an enemy card without attachments", TamahimeExp),
        ("Tsuruchi Nishima: gains Naval (for the turn) when he assigns in Infantry Maneuvers, either side -- not in Cavalry Maneuvers, and Naval is nothing printed", NishimaNaval),
        ("Fudo: may not assign, and will not move, to battlefields below 5 Charging tokens; assigned, he loses two; Limited: two Charging tokens, once a turn", FudoTokens),
        ("Fudo: Reaction, twice per turn after an action targets him, removes a Charging token and negates its effects on him -- not offered without a token, a third time, or when he is not the target", FudoNegates),
        ("Fudo: Battle: destroy a target enemy unit -- the Personality and its attachments; own units are not targets", FudoDestroysUnit),
        ("Horse: attaches to Kataoka ignoring costs (0 Gold) and to any other Human for its 3 Gold, never to a Nonhuman; the host has Cavalry while it is attached", HorseAttach),
        ("Treasure Hunter: after a battle's resolution in which its Attacker's army destroyed a province, destroy it to create a bowed Charter (2 Gold); not as a Defender, not when no province fell", TreasureHunterRetires),
        ("Every enabled command of the new actions validates on a fresh engine", OfferedCommandsValidate),
    ]);

    // ---- helpers ----

    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[cardId]);
    }

    private static GameCommand PurchaseFromProvince(MatchEngine e, int player, string cardId) =>
        new("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds);

    private static void Richer(MatchState s, int holdings = 6) { for (var i = 0; i < holdings; i++) Add(s, "training-marketplace", 0, Zone.Home); }

    // Declines every open window and passes until nothing is pending (an action's before-action window, an after-action window).
    private static void Settle(MatchEngine e, int limit = 12)
    {
        for (var guard = 0; guard < limit && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PriorityPlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass");
            Check(option is not null, $"nothing to decline for player {actor} in {e.State.PendingChoice!.Type}: " + string.Join("; ", e.QueryActions(actor).Select(x => $"{x.Type}:{x.Label} [{x.Enabled}] {x.Reason}")));
            Accept(e, option!.Command!);
        }
    }

    // ---- shape ----

    private static void PublishedShape()
    {
        var root = RepoRoot();
        foreach (var (wave, ids) in new[] { ("personality-wave-123", PersonalityIds), ("follower-wave-46", FollowerIds) })
        {
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(new HashSet<string>(listed).SetEquals(ids) && listed.Length == ids.Length, $"{wave}'s manifest is exactly its cards");
            foreach (var id in ids) Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
            var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json"))).RootElement;
            Eq(sources.GetArrayLength(), ids.Length, $"{wave}: one provenance row per card");
            foreach (var row in sources.EnumerateArray())
                Check(row.GetProperty("image_path").GetString()!.Contains("/oracle/promotional-emperor/"), $"{row.GetProperty("card_id")} comes from the Promotional-Emperor scan");
        }

        // (Force, Chi, Personal Honor, Gold, Honor Requirement, keywords, abilities, effect) read off each scan, not the catalogue record.
        void Is(string id, string name, CardType type, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string[] abilities, string effect, int? entryLoss = null, int focus = 0)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == type && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Type}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr && c.Focus == focus,
                $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement} focus {c.Focus}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
            Check(AbilityCatalog.All.Keys.Intersect(abilities).Count() == abilities.Length, $"{name}: every ability id exists");
        }
        const CardType P = CardType.Personality;
        Is(ArmedRiceFarmer, "Armed Rice Farmer", P, "Unaligned", 1, 1, 0, 2, null, ["Expendable", "Ashigaru"], [], "none");
        Is(Chikara, "Chikara", P, "Unaligned", 5, 4, 2, 7, null, ["Duelist", "Experienced 2", "Kensai", "Unique", "Ronin", "Samurai"], ["komatsu-destroy-attachment"], "none");
        Is(Hirose, "Daigotsu Hirose", P, "Spider", 3, 2, 0, 4, null, ["Cavalry", "Samurai", "Shadowlands", "Spider Clan"], [], "none", 1);
        Is(Fudo, "Fudo", P, "Unaligned", 12, 6, 0, 7, null, ["Conqueror", "Unique", "Experienced", "Fudo", "Nonhuman"], ["promo-fudo-charge", "promo-fudo-negate", "promo-fudo-destroy"], "promo-fudo");
        Is(Gakku, "Gakku", P, "Unaligned", 7, 2, 0, 9, null, ["Unique", "Devourer of Gods", "Experienced", "Goblin", "Nonhuman", "Scout", "Shadowlands"], ["promo-gakku-melee"], "spider-clan-discount", 3);
        Is(Hikarou, "Isawa Hikarou", P, "Phoenix", 2, 3, 2, 5, 2, ["Fire", "Phoenix Clan", "Shugenja"], [], "promo-hikarou-fire-spells");
        Is(Kataoka, "Kataoka", P, "Unaligned", 3, 1, 1, 5, null, ["Cavalry", "Commander", "Ronin", "Samurai"], [], "promo-kataoka-farm-discount");
        Is(Mahatsu, "Mahatsu", P, "Unaligned", 3, 3, 1, 3, null, ["Alchemist", "Ronin", "Shugenja"], ["promo-mahatsu-air", "promo-mahatsu-earth", "promo-mahatsu-fire", "promo-mahatsu-water"], "none");
        Is(Barru, "Seppun Barru", P, "Unaligned", 3, 3, 2, 4, 4, ["Commander", "Imperial", "Samurai"], ["promo-barru-followers"], "none");
        Is(Ichigo, "Seppun Ichigo", P, "Unaligned", 2, 2, 2, 3, 0, ["Imperial", "Samurai"], [], "promo-ichigo-dojo-force");
        Is(Ritisharu, "Seppun Ritisharu", P, "Unaligned", 0, 3, 3, 2, 0, ["Unique", "Imperial", "Imperial Treasurer"], [], "promo-ritisharu-holdings");
        Is(Tamahime, "Tamahime", P, "Unaligned", 3, 3, 2, 6, 0, ["Tactician", "Ronin", "Samurai", "Sensei"], [], "discard-other-sensei");
        Is(TamahimeExpId, "Tamahime", P, "Unaligned", 5, 3, 3, 7, 2, ["Tactician", "Unique", "Experienced", "Ronin", "Samurai", "Sensei"], ["kuronada-destroy"], "discard-other-sensei");
        Is(Tesskss, "Tess'kss", P, "Unaligned", 3, 3, 0, 4, null, ["Ningyo", "Nonhuman", "Pearl", "Shugenja"], ["tsuji-bow"], "none");
        // The scan prints no Naval keyword (the catalogue record lists one): he is given it by his own trait.
        Is(Nishima, "Tsuruchi Nishima", P, "Mantis", 4, 3, 1, 7, null, ["Cavalry", "Mantis Clan", "Samurai", "Thunder"], [], "promo-nishima-naval");
        const CardType F = CardType.Follower;
        Is(Horse, "Horse", F, "Unaligned", 1, 0, 0, 3, null, ["Cavalry", "Horse", "Nonhuman"], [], "promo-horse-kataoka-free", focus: 1);
        Is(RiceFarmer, "Rice Farmer", F, "Unaligned", 2, 0, 0, 2, null, ["Kharmic", "Ashigaru"], [], "none", focus: 2);
        Is(TreasureHunter, "Treasure Hunter", F, "Unaligned", 2, 0, 0, 2, 0, ["Scout"], ["promo-treasure-hunter-retire"], "none", focus: 1);
        Eq(CardCatalog.Get(Horse).AttachRequiresKeyword, "Human"); Eq(CardCatalog.Get(Horse).AttachGrantsKeyword, "Cavalry");
        Check(CardCatalog.Get(Charter).CreatedOnly && CardCatalog.Get(Charter).GoldProduction == 2, "the Holding Treasure Hunter creates is the published created-only Charter, producing 2 Gold");
        var fudoNegate = AbilityCatalog.All["promo-fudo-negate"];
        Check(fudoNegate.UsesPerTurn == 2 && fudoNegate.Trigger == ReactionTriggers.TargetedByAction && fudoNegate.Costs![0].Verb == CostVerbs.DestroyToken && fudoNegate.Costs[0].Token == "Charging", "Fudo's Reaction: twice per turn, a Charging token as the cost");
        Check(AbilityCatalog.All["promo-treasure-hunter-retire"].Costs![0].Verb == CostVerbs.Sacrifice, "Treasure Hunter is destroyed as a cost");
        Check(AbilityCatalog.All["promo-mahatsu-air"].UsageKey == AbilityCatalog.All["promo-mahatsu-water"].UsageKey, "Mahatsu's four actions share a use");
    }

    // ---- cards carried by existing surface ----

    private static void ArmedRiceFarmerExpendable()
    {
        var s = Table(Phase.Battle, 0);
        var farmer = Add(s, ArmedRiceFarmer, 1, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: farmer.Id, AbilityId: "training-destroy-no-attachments"));
        Eq(e.State.Cards[farmer.Id].Zone, Zone.Dead, "the Farmer is destroyed");
        Eq(e.State.Players[1].Hand.Count, handBefore + 1, "his controller draws a card (Expendable)");
        Check(e.State.Players[0].Hand.Count == 0, "the destroyer's controller does not");
    }

    private static void RiceFarmerKharmic()
    {
        var s = Table(Phase.Action, 0);
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities need a published Stronghold
        var farmer = Add(s, RiceFarmer, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: farmer.Id, AbilityId: "kharmic-discard", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Eq(e.State.Cards[farmer.Id].Zone, Zone.FateDiscard, "discarded");
        Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 1, "it left the hand and a card was drawn");
        var plain = Table(Phase.Action, 0);
        plain.Cards[plain.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        var other = Add(plain, "training-follower", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(plain);
        Reject(e2, new GameCommand("useAbility", 0, e2.Version, TargetId: other.Id, AbilityId: "kharmic-discard", SourceIds: e2.PaymentOptions(0, 2).First().SourceIds));
    }

    private static void HiroseEntry()
    {
        var (e, hirose) = Recruit(Hirose, s => Richer(s));
        var honorBefore = Honor(e, 0);
        Accept(e, PurchaseFromProvince(e, 0, hirose.Id));
        Eq(e.State.Cards[hirose.Id].Zone, Zone.Home, "recruited");
        Eq(Honor(e, 0), honorBefore - 1, "After Hirose enters play, lose 1 Honor");
        Check(e.State.PendingChoice is null, "a mandatory trait opens no window");
        Check(e.HasKeyword(hirose.Id, "Cavalry"), "printed Cavalry");
    }

    private static void TesskssBow()
    {
        var s = Table(Phase.Battle, 0);
        var tesskss = Add(s, Tesskss, 0, Zone.Battlefield, province: 0);
        var friend = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "tsuji-bow", tesskss.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(follower.Id), "any enemy card, attachments and Personalities alike");
        Check(!option.TargetIds.Contains(friend.Id) && !option.TargetIds.Contains(tesskss.Id), "no card of her own side");
        Accept(e, Offer(e, 0, "tsuji-bow", tesskss.Id, enemy.Id));
        Settle(e);
        Check(e.State.Cards[enemy.Id].Bowed, "the enemy card is bowed");
        Check(!e.State.Cards[tesskss.Id].Bowed, "bowing is no cost to her");
    }

    private static void ChikaraDestroysAttachment()
    {
        var s = Table(Phase.Battle, 0);
        var chikara = Add(s, Chikara, 0, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: chikara.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attachment = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "komatsu-destroy-attachment", chikara.Id);
        Check(option.TargetIds!.Contains(attachment.Id), "an enemy attachment is a target");
        Check(!option.TargetIds.Contains(mine.Id) && !option.TargetIds.Contains(enemy.Id), "neither her own attachment nor a Personality");
        Accept(e, Offer(e, 0, "komatsu-destroy-attachment", chikara.Id, attachment.Id));
        Settle(e);
        Check(e.State.Cards[attachment.Id].Zone != Zone.Attached, "the attachment is destroyed");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "the Personality stays");
    }

    // ---- Gakku ----

    private static void GakkuEntryAndMelee()
    {
        var (spider, gakku) = Recruit(Gakku, s => { Richer(s, 8); s.Players[0].Clan = "Spider"; });
        Eq(spider.QuotePurchase(0, gakku.Id).Cost, 8, "a Spider Clan player pays 1 less (9 -> 8)");
        var honorBefore = Honor(spider, 0);
        Accept(spider, PurchaseFromProvince(spider, 0, gakku.Id));
        Eq(Honor(spider, 0), honorBefore - 3, "After Gakku enters play: Lose 3 Honor");
        var (crane, gakkuCrane) = Recruit(Gakku, s => Richer(s, 8));
        Eq(crane.QuotePurchase(0, gakkuCrane.Id).Cost, 9, "any other Clan pays the printed 9");

        var t = Table(Phase.Battle, 0);
        var g = Add(t, Gakku, 0, Zone.Battlefield, province: 0);
        var seven = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Batch336Support.Force(seven, 3); // Force 4 + 3 = 7
        var eight = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6 + 2 = 8
        Batch336Support.Force(eight, 2);
        var e = MatchEngine.FromTestState(t);
        Accept(e, Use(e, 0, "promo-gakku-melee", g.Id, eight.Id));
        Settle(e);
        Eq(e.State.Cards[eight.Id].Zone, Zone.Battlefield, "Melee 7 does not reach Force 8");
        var t2 = Table(Phase.Battle, 0);
        var g2 = Add(t2, Gakku, 0, Zone.Battlefield, province: 0);
        var seven2 = Add(t2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Batch336Support.Force(seven2, 3);
        var e2 = MatchEngine.FromTestState(t2);
        Accept(e2, Use(e2, 0, "promo-gakku-melee", g2.Id, seven2.Id));
        Settle(e2);
        Eq(e2.State.Cards[seven2.Id].Zone, Zone.Dead, "Melee 7 destroys Force 7");
        Check(!e2.State.Cards[g2.Id].Bowed, "no cost is printed");
        var away = Table(Phase.Battle, 0);
        var home = Add(away, Gakku, 0, Zone.Home);
        Add(away, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, "promo-gakku-melee", home.Id), "a Battle action needs him at the battlefield");
    }

    // ---- Barru ----

    private static void BarruFollowers()
    {
        var s = Table(Phase.Battle, 0);
        var barru = Add(s, Barru, 0, Zone.Battlefield, province: 0);
        var own = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: barru.Id);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var allyFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: ally.Id);
        var homePersonality = Add(s, "training-crane-samurai", 0, Zone.Home);
        var homeFollower = Add(s, "training-follower", 0, Zone.Attached, parent: homePersonality.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        int[] before = [e.CardForceForTest(own.Id), e.CardForceForTest(allyFollower.Id), e.CardForceForTest(homeFollower.Id), e.CardForceForTest(enemyFollower.Id), e.CardForceForTest(barru.Id), e.CardForceForTest(ally.Id)];
        Accept(e, Use(e, 0, "promo-barru-followers", barru.Id));
        Settle(e);
        Eq(e.CardForceForTest(own.Id), before[0] + 1, "his own Follower");
        Eq(e.CardForceForTest(allyFollower.Id), before[1] + 1, "a Follower in the same army");
        Eq(e.CardForceForTest(homeFollower.Id), before[2], "a Follower at home is not in his army");
        Eq(e.CardForceForTest(enemyFollower.Id), before[3], "an enemy Follower is not yours");
        Eq(e.CardForceForTest(barru.Id), before[4], "Personalities are not Followers");
        Eq(e.CardForceForTest(ally.Id), before[5], "Personalities are not Followers (the ally)");
        Check(!CanUse(MatchEngine.FromTestState(Table(Phase.Action, 0)), 0, "promo-barru-followers", barru.Id), "only the Battle phase");
        var away = Table(Phase.Battle, 0);
        var homeBarru = Add(away, Barru, 0, Zone.Home);
        Add(away, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, "promo-barru-followers", homeBarru.Id), "a Battle action needs him at the battlefield");
    }

    // ---- Mahatsu ----

    private static void MahatsuElements()
    {
        foreach (var (ability, keyword) in new[] { ("promo-mahatsu-air", "Air"), ("promo-mahatsu-earth", "Earth"), ("promo-mahatsu-fire", "Fire"), ("promo-mahatsu-water", "Water") })
        {
            var s = Table(Phase.Action, 0);
            var mahatsu = Add(s, Mahatsu, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!e.HasKeyword(mahatsu.Id, keyword), $"{keyword} is not printed");
            Accept(e, Use(e, 0, ability, mahatsu.Id));
            Settle(e);
            Check(e.HasKeyword(mahatsu.Id, keyword), $"he has {keyword}");
            foreach (var other in new[] { "promo-mahatsu-air", "promo-mahatsu-earth", "promo-mahatsu-fire", "promo-mahatsu-water" })
                Check(!CanUse(e, 0, other, mahatsu.Id), $"{other} shares the one use");
            Check(e.State.Cards[mahatsu.Id].GrantedKeywords.Any(g => g.Keyword == keyword && g.Duration == GrantDuration.Turn), "the grant lasts the turn (R-001)");
        }
        var t = Table(Phase.Action, 0);
        var m = Add(t, Mahatsu, 0, Zone.Home);
        Add(t, "training-lion-samurai", 1, Zone.Home);
        Check((Enabled(MatchEngine.FromTestState(t), 0, "promo-mahatsu-fire", m.Id).TargetIds ?? []).Length == 0, "it names no target: it gives the keyword to Mahatsu himself");
    }

    // ---- Hikarou / Ichigo ----

    private static void HikarouFireSpells()
    {
        var s = Table(Phase.Action, 0);
        var hikarou = Add(s, Hikarou, 0, Zone.Home);
        var fire1 = Add(s, "training-spell", 0, Zone.Attached, parent: hikarou.Id); GiveKeyword(fire1, "Fire");
        var air = Add(s, "training-spell", 0, Zone.Attached, parent: hikarou.Id); GiveKeyword(air, "Air");
        var friend = Add(s, "training-crane-samurai", 0, Zone.Home);
        var fire2 = Add(s, "training-spell", 0, Zone.Attached, parent: friend.Id); GiveKeyword(fire2, "Fire");
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(hikarou.Id), 3, "2F printed, +1F for his one Fire Spell (not the Air Spell, not another Personality's)");
        var s2 = Table(Phase.Action, 0);
        var h2 = Add(s2, Hikarou, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s2).CardForceForTest(h2.Id), 2, "no Spell, no bonus");
        var s3 = Table(Phase.Action, 0);
        var h3 = Add(s3, Hikarou, 0, Zone.Home);
        for (var i = 0; i < 2; i++) { var spell = Add(s3, "training-spell", 0, Zone.Attached, parent: h3.Id); GiveKeyword(spell, "Fire"); }
        var e3 = MatchEngine.FromTestState(s3);
        Eq(e3.CardForceForTest(h3.Id), 4, "+1F for each of two Fire Spells");
        var gone = Batch336Support.Edit(e3, st => st.Cards.Remove(st.Cards.Values.First(c => c.AttachedTo == h3.Id).Id));
        Eq(gone.CardForceForTest(h3.Id), 3, "live: one Spell fewer, one point fewer");
    }

    private static void IchigoDojos()
    {
        var s = Table(Phase.Action, 0);
        var ichigo = Add(s, Ichigo, 0, Zone.Home);
        Add(s, "training-dojo", 0, Zone.Home);
        Add(s, "oracle-l5r-1190", 0, Zone.Home); // Bushi Dojo
        Add(s, "training-farm", 0, Zone.Home);
        Add(s, "training-dojo", 1, Zone.Home); // another player's
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(ichigo.Id), 4, "2F printed, +1F for each of the two Dojos he controls (not the Farm, not the opponent's Dojo)");
        var s2 = Table(Phase.Action, 0);
        var i2 = Add(s2, Ichigo, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s2).CardForceForTest(i2.Id), 2, "no Dojo, no bonus");
    }

    // ---- Ritisharu ----

    private static void RitisharuProduction()
    {
        var s = Table(Phase.Action, 0);
        var ritisharu = Add(s, Ritisharu, 0, Zone.Home);
        var farm = Add(s, "training-farm", 0, Zone.Home);
        var dojo = Add(s, "training-dojo", 0, Zone.Home); // produces no Gold
        var theirs = Add(s, "training-farm", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(farm.Id, 0), 3, "a Holding that bows for 2 Gold produces 3");
        Eq(e.GoldProduced(dojo.Id, 0), 0, "a Holding that produces no Gold has nothing to add to");
        Eq(e.GoldProduced(theirs.Id, 1), 2, "the opponent's Holdings are not yours");
        var without = Table(Phase.Action, 0);
        var farm2 = Add(without, "training-farm", 0, Zone.Home);
        Eq(MatchEngine.FromTestState(without).GoldProduced(farm2.Id, 0), 2, "without him: the printed 2");
        var gone = Batch336Support.Edit(e, st => st.Cards.Remove(ritisharu.Id));
        Eq(gone.GoldProduced(farm.Id, 0), 2, "live: back to 2 once he leaves play");
        // The extra Gold is real: a Gold cost of 3 is paid by one Farm.
        var pay = Table(Phase.Action, 0);
        Add(pay, Ritisharu, 0, Zone.Home);
        var onlyFarm = Add(pay, "training-farm", 0, Zone.Home);
        foreach (var c in pay.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && c.Id != onlyFarm.Id && Definition(c).GoldProduction > 0).ToArray()) c.Bowed = true;
        var ep = MatchEngine.FromTestState(pay);
        Check(ep.PaymentOptions(0, 3).Any(o => o.SourceIds.Contains(onlyFarm.Id) && o.SourceIds.Length == 1 && o.Produced >= 3), "one Farm now pays a Gold cost of 3");
    }

    private static CardDefinition Definition(CardInstance c) => CardCatalog.Get(c.DefinitionId);

    // ---- Kataoka ----

    private static void KataokaFarmDiscount()
    {
        var (none, k0) = Recruit(Kataoka);
        Eq(none.QuotePurchase(0, k0.Id).Cost, 5, "no Farms: the printed 5");
        var (two, k2) = Recruit(Kataoka, s => { Add(s, "training-farm", 1, Zone.Home); Add(s, "training-farm", 1, Zone.Home); });
        Eq(two.QuotePurchase(0, k2.Id).Cost, 3, "1 less for each of two Farms another player controls");
        var (own, kOwn) = Recruit(Kataoka, s => { Add(s, "training-farm", 0, Zone.Home); Add(s, "training-farm", 1, Zone.Home); });
        Eq(own.QuotePurchase(0, kOwn.Id).Cost, 4, "his own Farm does not count");
        var (many, kMany) = Recruit(Kataoka, s => { for (var i = 0; i < 7; i++) Add(s, "training-farm", 1, Zone.Home); });
        Eq(many.QuotePurchase(0, kMany.Id).Cost, 0, "never below 0");
        var (nonFarm, kNon) = Recruit(Kataoka, s => Add(s, "training-dojo", 1, Zone.Home));
        Eq(nonFarm.QuotePurchase(0, kNon.Id).Cost, 5, "only Farms count");
        Check(two.QuotePurchase(0, k2.Id).Basis?.Contains("Farms other players control") == true, "the price shows its reason: " + two.QuotePurchase(0, k2.Id).Basis);
    }

    // ---- Tamahime ----

    private static void TamahimeBase()
    {
        var (e, tamahime) = Recruit(Tamahime, s =>
        {
            Richer(s);
            var sensei = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(sensei, "Sensei");
            var enemySensei = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(enemySensei, "Sensei");
            var ronin = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(ronin, "Ronin");
            var enemyRonin = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(enemyRonin, "Ronin");
        });
        var senseis = e.State.Cards.Values.Where(c => c.DefinitionId == "training-lion-samurai" && c.GrantedKeywords.Any(g => g.Keyword == "Sensei")).ToArray();
        var roninOwn = e.State.Cards.Values.First(c => c.DefinitionId == "training-crane-samurai" && c.GrantedKeywords.Any(g => g.Keyword == "Ronin"));
        var roninEnemy = e.State.Cards.Values.First(c => c.DefinitionId == "training-lion-samurai" && c.GrantedKeywords.Any(g => g.Keyword == "Ronin"));
        Check(!e.HasKeyword(roninOwn.Id, "Tactician"), "before she enters play: no Tactician");
        Accept(e, PurchaseFromProvince(e, 0, tamahime.Id));
        Eq(e.State.Cards[tamahime.Id].Zone, Zone.Home, "recruited");
        var ownSensei = senseis.First(c => c.OwnerId == 0); var enemySenseiCard = senseis.First(c => c.OwnerId == 1);
        Check(e.State.Cards[ownSensei.Id].Zone != Zone.Home, "her controller's other Sensei Personality is discarded");
        Eq(e.State.Cards[enemySenseiCard.Id].Zone, Zone.Home, "another player's Sensei stays");
        Check(e.HasKeyword(roninOwn.Id, "Tactician"), "her other Ronin Personalities have Tactician");
        Check(!e.HasKeyword(roninEnemy.Id, "Tactician"), "another player's Ronin do not");
        Check(e.HasKeyword(tamahime.Id, "Tactician"), "she is printed Tactician");
        var nonRonin = Batch336Support.Edit(e, st => st.Cards.Remove(tamahime.Id));
        Check(!nonRonin.HasKeyword(roninOwn.Id, "Tactician"), "live: it ends with her");
        // A Personality that is not Ronin gets nothing.
        var s = Table(Phase.Action, 0);
        Add(s, Tamahime, 0, Zone.Home);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        Check(!MatchEngine.FromTestState(s).HasKeyword(samurai.Id, "Tactician"), "only Ronin");
    }

    private static void TamahimeExp()
    {
        // Entering play: Sensei discard, a Ronin cheaper, the printed Tactician grant.
        var (e, exp) = Recruit(TamahimeExpId, s =>
        {
            Richer(s); s.Players[0].Honor = 10;
            var sensei = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(sensei, "Sensei");
        });
        Accept(e, PurchaseFromProvince(e, 0, exp.Id));
        Check(e.State.Cards.Values.Where(c => c.DefinitionId == "training-lion-samurai" && c.OwnerId == 0).All(c => c.Zone != Zone.Home), "the other Sensei is discarded on entering");

        // Ronin cards enter paying 1 less: Kataoka (Ronin, 5) costs 4 with Tamahime - exp in play, 5 without, and Tamahime herself is not discounted by her own text.
        var s2 = MatchEngine.CreateEconomyScenario().State;
        var kataokaId = s2.Players[0].Provinces[0].CardId!;
        s2.Cards[kataokaId].DefinitionId = Kataoka; s2.Cards[kataokaId].FaceUp = true;
        Add(s2, TamahimeExpId, 0, Zone.Home);
        var with = MatchEngine.FromTestState(s2);
        Eq(with.QuotePurchase(0, kataokaId).Cost, 4, "another Ronin card enters play paying 1 less");
        Check(with.QuotePurchase(0, kataokaId).Basis?.Contains("Tamahime") == true, "the price names her: " + with.QuotePurchase(0, kataokaId).Basis);
        var s3 = MatchEngine.CreateEconomyScenario().State;
        var k3 = s3.Players[0].Provinces[0].CardId!;
        s3.Cards[k3].DefinitionId = Kataoka; s3.Cards[k3].FaceUp = true;
        Eq(MatchEngine.FromTestState(s3).QuotePurchase(0, k3).Cost, 5, "without her: the printed price");
        var s4 = MatchEngine.CreateEconomyScenario().State;
        var nonRonin = s4.Players[0].Provinces[0].CardId!;
        s4.Cards[nonRonin].DefinitionId = "training-crane-samurai"; s4.Cards[nonRonin].FaceUp = true;
        Add(s4, TamahimeExpId, 0, Zone.Home);
        var plain = MatchEngine.FromTestState(s4);
        Eq(plain.QuotePurchase(0, nonRonin).Cost, 5, "a card that is not Ronin is not discounted");
        var s5 = MatchEngine.CreateEconomyScenario().State;
        var k5 = s5.Players[0].Provinces[0].CardId!;
        s5.Cards[k5].DefinitionId = Kataoka; s5.Cards[k5].FaceUp = true;
        Add(s5, TamahimeExpId, 1, Zone.Home);
        Eq(MatchEngine.FromTestState(s5).QuotePurchase(0, k5).Cost, 5, "another player's Tamahime gives you nothing");

        // Overlaying: the lower Tamahime is replaced, and the entry trait runs again ("enters play or overlays").
        var o = MatchEngine.CreateEconomyScenario().State;
        var overId = o.Players[0].Provinces[0].CardId!;
        o.Cards[overId].DefinitionId = TamahimeExpId; o.Cards[overId].FaceUp = true;
        var basic = Add(o, Tamahime, 0, Zone.Home);
        var other = Add(o, "training-lion-samurai", 0, Zone.Home); GiveKeyword(other, "Sensei");
        o.Phase = Phase.Dynasty; o.ActivePlayerId = 0; o.PriorityPlayerId = 0;
        var eo = MatchEngine.FromTestState(o);
        Accept(eo, new GameCommand("overlay", 0, eo.Version, CardId: overId, TargetId: basic.Id));
        Eq(eo.State.Cards[overId].Zone, Zone.Home, "overlaid");
        Check(eo.State.Cards[other.Id].Zone != Zone.Home, "the overlay discards the other Sensei too");

        // Battle: destroy an enemy card without attachments.
        var b = Table(Phase.Battle, 0);
        var t = Add(b, TamahimeExpId, 0, Zone.Battlefield, province: 0);
        var bare = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var eb = MatchEngine.FromTestState(b);
        var option = Enabled(eb, 0, "kuronada-destroy", t.Id);
        Check(option.TargetIds!.Contains(bare.Id) && !option.TargetIds.Contains(armed.Id) && !option.TargetIds.Contains(t.Id), "an enemy card with no attachments only");
        Accept(eb, Offer(eb, 0, "kuronada-destroy", t.Id, bare.Id));
        Settle(eb);
        Eq(eb.State.Cards[bare.Id].Zone, Zone.Dead, "destroyed");
    }

    // ---- Nishima ----

    private static void NishimaNaval()
    {
        // Attacker, Infantry Maneuvers.
        var s = Table(Phase.Action, 1);
        var nishima = Add(s, Nishima, 1, Zone.Home);
        var e = Batch336Support.Edit(MatchEngine.FromTestState(s), st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
        Check(!e.HasKeyword(nishima.Id, "Naval"), "no Naval before");
        Accept(e, e.QueryActions(1).First(a => a.Type == "attack").Command! with { Assignments = new() { [nishima.Id] = 0 } });
        Check(e.HasKeyword(nishima.Id, "Naval"), "assigned in Infantry Maneuvers: Naval");
        Check(e.State.Cards[nishima.Id].GrantedKeywords.Any(g => g.Keyword == "Naval" && g.Duration == GrantDuration.Turn), "for the turn (R-001)");

        // Cavalry Maneuvers: nothing.
        var c = Table(Phase.Action, 1);
        var cav = Add(c, Nishima, 1, Zone.Home);
        var ec = Batch336Support.Edit(MatchEngine.FromTestState(c), st => { st.Phase = Phase.CavalryDeclaration; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
        Accept(ec, ec.QueryActions(1).First(a => a.Type == "attack").Command! with { Assignments = new() { [cav.Id] = 0 } });
        Check(!ec.HasKeyword(cav.Id, "Naval"), "assigned in Cavalry Maneuvers: no Naval");

        // The Defender assigns in Infantry Maneuvers too.
        var d = Table(Phase.Action, 1);
        var def = Add(d, Nishima, 0, Zone.Home);
        var ed = Batch336Support.Edit(MatchEngine.FromTestState(d), st => { st.Phase = Phase.DefenseAssignment; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.PendingChoice = null; st.UnresolvedBattlefields = [0]; });
        var defend = ed.QueryActions(0).FirstOrDefault(a => a.Type == "assignDefense" && a.Enabled);
        Check(defend is not null, "the Defender may assign him");
        Accept(ed, defend!.Command! with { Assignments = new() { [def.Id] = 0 } });
        Check(ed.HasKeyword(def.Id, "Naval"), "a Defender's assignment in Infantry Maneuvers counts");

        // Another Personality assigning gets nothing.
        var o = Table(Phase.Action, 1);
        var plain = Add(o, "training-lion-samurai", 1, Zone.Home);
        var eo = Batch336Support.Edit(MatchEngine.FromTestState(o), st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
        Accept(eo, eo.QueryActions(1).First(a => a.Type == "attack").Command! with { Assignments = new() { [plain.Id] = 0 } });
        Check(!eo.HasKeyword(plain.Id, "Naval"), "only Nishima");
    }

    // ---- Fudo ----

    private static void FudoTokens()
    {
        // Infantry assignment: not offered below five tokens, offered at five, and two tokens come off.
        foreach (var (count, offered) in new[] { (0, false), (4, false), (5, true), (6, true) })
        {
            var s = Table(Phase.Action, 1);
            var fudo = Add(s, Fudo, 1, Zone.Home); Tokens(fudo, count);
            var e = Batch336Support.Edit(MatchEngine.FromTestState(s), st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
            var attack = e.QueryActions(1).FirstOrDefault(a => a.Type == "attack");
            Eq(attack?.TargetIds?.Contains(fudo.Id) ?? false, offered, $"{count} tokens: assignable");
            if (!offered)
            {
                Reject(e, new GameCommand("attack", 1, e.Version, Assignments: new() { [fudo.Id] = 0 }));
                continue;
            }
            Accept(e, attack!.Command! with { Assignments = new() { [fudo.Id] = 0 } });
            Eq(TokenCount(e, fudo.Id), count - 2, "After Fudo is assigned: remove two of its Charging tokens");
        }
        // Defending needs the tokens too.
        var d = Table(Phase.Action, 1);
        var defender = Add(d, Fudo, 0, Zone.Home); Tokens(defender, 4);
        var ed = Batch336Support.Edit(MatchEngine.FromTestState(d), st => { st.Phase = Phase.DefenseAssignment; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.PendingChoice = null; st.UnresolvedBattlefields = [0]; });
        var assign = ed.QueryActions(0).FirstOrDefault(a => a.Type == "assignDefense");
        Check(assign is null || !(assign.TargetIds?.Contains(defender.Id) ?? false), "four tokens: he does not defend either");

        // Movement: he will not move to a battlefield below five tokens.
        var m = Table(Phase.Battle, 0);
        var fudoAway = Add(m, Fudo, 0, Zone.Home); Tokens(fudoAway, 3);
        Add(m, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var em = MatchEngine.FromTestState(m);
        Check(!em.MoveToBattlefieldForTest(fudoAway.Id, 0), "three tokens: he will not move");
        var m2 = Table(Phase.Battle, 0);
        var fudoReady = Add(m2, Fudo, 0, Zone.Home); Tokens(fudoReady, 5);
        Add(m2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(MatchEngine.FromTestState(m2).MoveToBattlefieldForTest(fudoReady.Id, 0), "five tokens: he moves");

        // Limited: two Charging tokens, once a turn.
        var l = Table(Phase.Action, 0);
        var f = Add(l, Fudo, 0, Zone.Home);
        var el = MatchEngine.FromTestState(l);
        Accept(el, Use(el, 0, "promo-fudo-charge", f.Id));
        Settle(el);
        Eq(TokenCount(el, f.Id), 2, "two Charging tokens");
        Check(!CanUse(el, 0, "promo-fudo-charge", f.Id), "Limited: once a turn");
        var furugaro = Add(l, Furugaro, 0, Zone.Attached, parent: f.Id);
        Check(CardCatalog.Get(Furugaro).Abilities!.Contains("furugaro-charge"), "Furugaro's tokens go to the same Fudo (keyword Fudo)");
    }

    private static (MatchEngine E, CardInstance Fudo, CardInstance Tess) FudoTargetedTable(int tokens)
    {
        var s = Table(Phase.Battle, 1);
        var fudo = Add(s, Fudo, 0, Zone.Battlefield, province: 0); Tokens(fudo, tokens);
        var tess = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), fudo, tess);
    }

    private static void FudoNegates()
    {
        var (e, fudo, tess) = FudoTargetedTable(3);
        Accept(e, Offer(e, 1, "tsuji-bow", tess.Id, fudo.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "an action targeting Fudo opens the before-action window");
        Accept(e, Enabled(e, 0, "promo-fudo-negate", fudo.Id).Command!);
        Eq(TokenCount(e, fudo.Id), 2, "one Charging token was the cost");
        Settle(e);
        Check(!e.State.Cards[fudo.Id].Bowed, "the action's effects on him were negated");

        // Without the reaction he is bowed.
        var (plain, f2, t2) = FudoTargetedTable(3);
        Accept(plain, Offer(plain, 1, "tsuji-bow", t2.Id, f2.Id));
        Settle(plain);
        Check(plain.State.Cards[f2.Id].Bowed, "declined: the bow lands");
        Eq(TokenCount(plain, f2.Id), 3, "and no token was spent");

        // No token: the Reaction is not offered.
        var (empty, f3, t3) = FudoTargetedTable(0);
        Accept(empty, Offer(empty, 1, "tsuji-bow", t3.Id, f3.Id));
        Check(!CanUse(empty, 0, "promo-fudo-negate", f3.Id), "no Charging token, no Reaction");
        Settle(empty);
        Check(empty.State.Cards[f3.Id].Bowed, "so the bow lands");

        // Twice per turn: the third time is not offered. A negation does not consume the attacker's own use of Tess'kss -- another Personality performs the third.
        var s = Table(Phase.Battle, 1);
        var fudo4 = Add(s, Fudo, 0, Zone.Battlefield, province: 0); Tokens(fudo4, 5);
        var a = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        var b = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        var c = Add(s, Tesskss, 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s);
        foreach (var attacker in new[] { a, b })
        {
            Accept(e4, Offer(e4, 1, "tsuji-bow", attacker.Id, fudo4.Id));
            Accept(e4, Enabled(e4, 0, "promo-fudo-negate", fudo4.Id).Command!);
            Settle(e4);
            if (e4.State.PriorityPlayerId == 0) Accept(e4, new GameCommand("pass", 0, e4.Version)); // the Defender passes; the Attacker acts again
        }
        Eq(TokenCount(e4, fudo4.Id), 3, "two uses, two tokens");
        Accept(e4, Offer(e4, 1, "tsuji-bow", c.Id, fudo4.Id));
        Check(!CanUse(e4, 0, "promo-fudo-negate", fudo4.Id), "a third time this turn is not offered");
        Settle(e4);
        Check(e4.State.Cards[fudo4.Id].Bowed, "so the third bow lands");

        // Not for another Personality: an action that targets a friend of his does not offer his Reaction.
        var o = Table(Phase.Battle, 1);
        var fudo5 = Add(o, Fudo, 0, Zone.Battlefield, province: 0); Tokens(fudo5, 3);
        var friend = Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var tess5 = Add(o, Tesskss, 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(o);
        Accept(e5, Offer(e5, 1, "tsuji-bow", tess5.Id, friend.Id));
        Check(!CanUse(e5, 0, "promo-fudo-negate", fudo5.Id), "he is not the target");
    }

    private static void FudoDestroysUnit()
    {
        var s = Table(Phase.Battle, 0);
        var fudo = Add(s, Fudo, 0, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "promo-fudo-destroy", fudo.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(mine.Id) && !option.TargetIds.Contains(fudo.Id), "an enemy unit, never his own side");
        Accept(e, Offer(e, 0, "promo-fudo-destroy", fudo.Id, enemy.Id));
        Settle(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Personality is destroyed");
        Check(e.State.Cards[follower.Id].Zone != Zone.Attached, "and its attachment with it");
        Check(!e.State.Cards[fudo.Id].Bowed, "no cost is printed");
    }

    // ---- Horse ----

    private static void HorseAttach()
    {
        MatchEngine Setup(out CardInstance horse, out CardInstance kataoka, out CardInstance human, out CardInstance beast)
        {
            var s = Table(Phase.Action, 0);
            horse = Add(s, Horse, 0, Zone.Hand);
            kataoka = Add(s, Kataoka, 0, Zone.Home);
            human = Add(s, "training-crane-samurai", 0, Zone.Home);
            beast = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(beast, "Nonhuman");
            return MatchEngine.FromTestState(s);
        }
        var e = Setup(out var horse, out var kataoka, out _, out _);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: horse.Id, SourceIds: [], TargetId: kataoka.Id));
        Eq(e.State.Cards[horse.Id].Zone, Zone.Attached, "attached to Kataoka"); Eq(e.State.Cards[horse.Id].AttachedTo, kataoka.Id, "to Kataoka");
        Check(e.HasKeyword(kataoka.Id, "Cavalry"), "This Personality has Cavalry");

        // Another Human: the printed 3 Gold, and nothing without it.
        var e2 = Setup(out var horse2, out _, out var human, out _);
        Reject(e2, new GameCommand("equip", 0, e2.Version, CardId: horse2.Id, SourceIds: [], TargetId: human.Id));
        Accept(e2, new GameCommand("equip", 0, e2.Version, CardId: horse2.Id, SourceIds: e2.PaymentOptions(0, 3).First().SourceIds, TargetId: human.Id));
        Eq(e2.State.Cards[horse2.Id].AttachedTo, human.Id, "attached to another Human for 3 Gold");

        // Will only attach to a Human.
        var e3 = Setup(out var horse3, out _, out _, out var beast);
        Reject(e3, new GameCommand("equip", 0, e3.Version, CardId: horse3.Id, SourceIds: e3.PaymentOptions(0, 3).First().SourceIds, TargetId: beast.Id));
        var quoteTargets = e3.QueryActions(0).Where(a => a.Type == "equip" && a.CardId == horse3.Id).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(!quoteTargets.Contains(beast.Id), "a Nonhuman is not offered as a host");
    }

    // ---- Treasure Hunter ----

    private static MatchEngine AttackerWins(int attackerForce, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Batch336Support.Force(attacker, attackerForce);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hunter = Add(s, TreasureHunter, 1, Zone.Attached, province: 0, parent: attacker.Id);
        tweak?.Invoke(s, hunter);
        var e = MatchEngine.FromTestState(s);
        for (var guard = 0; guard < 8 && e.State.PendingChoice?.Type != "afterResolutionReaction" && e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null; guard++)
            Accept(e, new GameCommand("pass", e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.Version));
        return e;
    }

    private static void TreasureHunterRetires()
    {
        var e = AttackerWins(30);
        Eq(e.State.PendingChoice?.Type, "afterResolutionReaction", "the after-resolution window opens");
        Check(e.State.PendingAfterResolution!.ProvinceDestroyedBy == 1, "precondition: the Attacker destroyed the province");
        var hunter = e.State.Cards.Values.First(c => c.DefinitionId == TreasureHunter);
        var holdings = e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.DefinitionId == Charter);
        Accept(e, Enabled(e, 1, "promo-treasure-hunter-retire", hunter.Id).Command!);
        Check(e.State.Cards[hunter.Id].Zone is Zone.FateDiscard or Zone.Dead or Zone.RemovedFromGame, "the Follower was destroyed as the cost: " + e.State.Cards[hunter.Id].Zone);
        var created = e.State.Cards.Values.Where(c => c.OwnerId == 1 && c.DefinitionId == Charter && c.Zone == Zone.Home).ToArray();
        Eq(created.Length, holdings + 1, "a Holding was created at home");
        Check(created.All(h => h.Bowed), "it entered play bowed");
        Eq(e.GoldProduced(created[0].Id, 1), 0 + CardCatalog.Get(Charter).GoldProduction, "it produces 2 Gold once straightened and bowed again");
        Settle(e);

        // Declined: nothing happens.
        var declined = AttackerWins(30);
        var h2 = declined.State.Cards.Values.First(c => c.DefinitionId == TreasureHunter);
        Settle(declined);
        Check(declined.State.Cards[h2.Id].Zone == Zone.Attached, "a Reaction is optional");

        // The Attacker destroyed the army but not the province (the strength of province 0 is above his Force): not offered.
        var noProvince = AttackerWins(0, (s, h) => { s.Players[0].Provinces[0].Strength = 99; });
        Check(noProvince.State.PendingChoice?.Type != "afterResolutionReaction", "no province destroyed: no window opens for the Follower");
        var h3 = noProvince.State.Cards.Values.First(c => c.DefinitionId == TreasureHunter);
        Eq(h3.Zone, Zone.Attached, "and nothing was spent");
        Check(!CanUse(noProvince, 1, "promo-treasure-hunter-retire", h3.Id), "no province destroyed, no Reaction");

        // A Defender's Treasure Hunter: the Attacker's army destroyed the province, not his.
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Batch336Support.Force(attacker, 30);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var defHunter = Add(s, TreasureHunter, 0, Zone.Attached, province: 0, parent: defender.Id);
        var ed = MatchEngine.FromTestState(s);
        for (var guard = 0; guard < 8 && ed.State.PendingChoice?.Type != "afterResolutionReaction" && ed.State.CurrentBattleProvince is not null; guard++)
            Accept(ed, new GameCommand("pass", ed.State.PendingChoice?.PlayerId ?? ed.State.PriorityPlayerId, ed.Version));
        Check(!ed.State.Cards.ContainsKey(defHunter.Id) || !CanUse(ed, 0, "promo-treasure-hunter-retire", defHunter.Id), "a Defender's Follower is not offered it");

        // In hand or at home with no battle: not offered.
        var idle = Table(Phase.Action, 1);
        var host = Add(idle, "training-lion-samurai", 1, Zone.Home);
        var idleHunter = Add(idle, TreasureHunter, 1, Zone.Attached, parent: host.Id);
        Check(!CanUse(MatchEngine.FromTestState(idle), 1, "promo-treasure-hunter-retire", idleHunter.Id), "outside a battle's resolution it is not offered");
    }

    // ---- every new command validates ----

    private static void OfferedCommandsValidate()
    {
        MatchEngine Fresh()
        {
            var s = Table(Phase.Battle, 0);
            var fudo = Add(s, Fudo, 0, Zone.Battlefield, province: 0); Tokens(fudo, 5);
            Add(s, Gakku, 0, Zone.Battlefield, province: 0);
            var barru = Add(s, Barru, 0, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: barru.Id);
            Add(s, Tesskss, 0, Zone.Battlefield, province: 0);
            Add(s, Chikara, 0, Zone.Battlefield, province: 0);
            Add(s, TamahimeExpId, 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
            return MatchEngine.FromTestState(s);
        }
        var source = Fresh();
        var options = source.QueryActions(0).Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is { } id
            && (id.StartsWith("promo-", StringComparison.Ordinal) || id is "komatsu-destroy-attachment" or "tsuji-bow" or "kuronada-destroy")).ToArray();
        Check(options.Length >= 6, $"the position offers the new actions ({options.Length})");
        foreach (var option in options)
        {
            Accept(Fresh(), option.Command!);
            if (option.Command!.CardIds is null)
                foreach (var target in option.TargetIds ?? []) Accept(Fresh(), option.Command! with { TargetId = target });
        }
    }
}
