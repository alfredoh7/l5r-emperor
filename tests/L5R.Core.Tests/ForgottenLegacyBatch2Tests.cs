using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Forgotten Legacy pass (issue #467), batch 2: the 15 Personalities of personality-wave-66, the Follower of follower-wave-29 and the Stronghold of stronghold-wave-15,
// driven through QueryActions and the commands it offers. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion). The other
// three candidates (Shosuro Aroru, The Dark Naga, Tsuruchi Nobukatsu) stay unwired and unpublished (see BLOCKERS in tools/content/build_card_inventory.py).
internal static class ForgottenLegacyBatch2Tests
{
    private const string Vengeful = "oracle-l5r-8409", Koyama = "oracle-l5r-4944", Sasako = "oracle-l5r-5279", Xiao = "oracle-l5r-5395", Shiho = "oracle-l5r-5591",
        Sawaken = "oracle-l5r-6804", Junpei = "oracle-l5r-6900", Makiko = "oracle-l5r-7126", Shakash = "oracle-l5r-8320", Sleepless = "oracle-l5r-8339",
        Osawa = "oracle-l5r-8565", Chokichi = "oracle-l5r-8644", JiYun = "oracle-l5r-9071", Yamazaki = "oracle-l5r-9430", Daiki = "oracle-l5r-9436",
        Haruhiko = "oracle-l5r-9556", Temple = "oracle-l5r-8094";
    private const string Yoshihara = "oracle-l5r-7373", Makoto = "oracle-l5r-9449", Tojikana = "oracle-l5r-4145";
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai", Follower = "training-follower";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static MatchEngine Priority(MatchEngine e, int player) => Edit(e, st => { st.PriorityPlayerId = player; st.ConsecutivePasses = 0; });
    private static int Force(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);

    public static void Run(TestRunner runner) => runner.Run("forgotten-legacy-batch-2", [
        ("Published shape: exactly the 17 wired cards are published in personality-wave-66, follower-wave-29 and stronghold-wave-15, with their catalogue stats, Effects and ability ids, art and manifests", PublishedShape),
        ("The Vengeful: its own Ranged 7 Attack; even while bowed it straightens its unit, but only while its Personality is Nonhuman", VengefulAttackAndStraighten),
        ("Matsu Koyama: reduces every Personality and Follower in the target unit by his Personal Honor and bows those at 0 Force", KoyamaWeaken),
        ("Moshi Sasako: the battle resolution returns his unit home unbowed while the rest of the attacking army bows", SasakoNoResolutionBow),
        ("Moto Xiao: Followers in his army have +1F (not at home, not the enemy's); destroys a target enemy card without attachments", XiaoBoostAndDestroy),
        ("Ninube Shiho: loses 1 Honor on recruit; goes to the top of his Dynasty deck and sends the target unit's cards without attachments to the bottom of their decks", ShihoRecruitAndReturn),
        ("Shiba Sawaken: an Iaijutsu -5F on an enemy Personality or Follower", SawakenWeaken),
        ("Shinjo Junpei: from home, bows a target enemy card without attachments; a Personality's next movement is negated once, the one after it is not", JunpeiBowAndNextMovement),
        ("Shosuro Makiko: bows to take the Imperial Favor", MakikoFavor),
        ("The Shakash: pays 1 less per Nonhuman controlled, capped at 3; bow OR move home a target enemy Personality, one shared use", ShakashDiscountAndBattle),
        ("The Sleepless One: pays 1 less; twice per turn, bowing a Forest or Pearl gives each of his controller's Nonhuman cards at the battlefield +1F", SleeplessDiscountAndBoost),
        ("Togashi Osawa: even while bowed, bows or straightens a Personality at his own location (his controller's home in the Action phase, his battlefield in a battle)", OsawaAtHisLocation),
        ("Toritaka Chokichi: bows his controller's Follower for a Ranged 4 Attack that may target a Personality with Followers only with Reconnaissance", ChokichiAttack),
        ("Utaku Ji-Yun: another player's card cannot dishonor her (an action or an Either/Or), her own controller's card can", JiYunDishonorImmunity),
        ("Yamazaki: straightens his controller's target Holding", YamazakiStraighten),
        ("Yasuki Daiki: the target's controller pays 4 Gold, loses 2 Honor or does neither -- then, on his own turn, the Personality may not be assigned or move to battlefields", DaikiDemand),
        ("Yoritomo Haruhiko: Ranged 3 Attack, 5 with Reconnaissance", HaruhikoAttack),
        ("The Forgotten Temple: no Honor gain, no loss from own cards, every other loss reduced to 1, no Political actions; refills a province face-up with a discarded Nonhuman", ForgottenTemple),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Eq(string.Join(",", listed.Order(StringComparer.Ordinal)), string.Join(",", ids.Order(StringComparer.Ordinal)), $"the {wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Ids(PublishedSnapshot wave) => string.Join(",", wave.Cards.Select(c => c.Id).Order(StringComparer.Ordinal));
        string[] personalities = [Koyama, Sasako, Xiao, Shiho, Sawaken, Junpei, Makiko, Shakash, Sleepless, Osawa, Chokichi, JiYun, Yamazaki, Daiki, Haruhiko];
        Eq(Ids(PublishedContent.PersonalityWave66), string.Join(",", personalities.Order(StringComparer.Ordinal)), "personality-wave-66 publishes exactly the wired Personalities");
        Eq(Ids(PublishedContent.FollowerWave29), Vengeful, "follower-wave-29 publishes exactly The Vengeful");
        Eq(Ids(PublishedContent.StrongholdWave15), Temple, "stronghold-wave-15 publishes exactly The Forgotten Temple");
        foreach (var unwired in new[] { "oracle-l5r-7089", "oracle-l5r-8861" }) // The Dark Naga (oracle-l5r-8012) is published in personality-wave-86 (issue #501)
            Check(!CardCatalog.Cards.ContainsKey(unwired), $"{unwired} stays unpublished");

        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string clan, int force, int chi, int cost, string keywords, string[] abilities, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && Keywords(id) == keywords && c.Effect == effect,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} keywords={Keywords(id)} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }
        Stats(Koyama, "Lion", 4, 3, 7, "Lion Clan,Paragon,Samurai", ["koyama-weaken"]);
        Stats(Sasako, "Mantis", 4, 3, 7, "Mantis Clan,Naval,Shugenja,Thunder", [], "sasako-unit-no-resolution-bow");
        Stats(Xiao, "Unicorn", 6, 4, 10, "Cavalry,Commander,Experienced,Samurai,Unicorn Clan,Unique", ["xiao-destroy"], "xiao-army-follower-force");
        Stats(Shiho, "Spider", /* issue #492, ruling R5 */ 3, 3, 4, "Ninja,Shadowlands,Shugenja,Spider Clan,Unmaker", ["shiho-return"]);
        Eq(CardCatalog.Get(Shiho).EntryHonorLoss, 1, "Shiho's recruit Honor loss");
        Stats(Sawaken, "Phoenix", 4, 3, 7, "Duelist,Phoenix Clan,Samurai,Yojimbo", ["sawaken-weaken"]);
        Stats(Junpei, "Unicorn", 3, 4, 7, "Samurai,Tactician,Unicorn Clan", ["junpei-base-bow"]);
        Stats(Makiko, "Scorpion", 2, 4, 6, "Courtier,Loyal,Scorpion Clan,Seductress", ["makiko-favor"]);
        Stats(Shakash, "Unaligned", 4, 2, 7, "Commander,Conqueror,Naga,Nonhuman", ["shakash-bow", "shakash-move-home"], "shakash-nonhuman-discount");
        Stats(Sleepless, "Unaligned", 4, 2, 7, "Abomination,Naga,Nonhuman,Pearl,Shugenja", ["sleepless-boost"], "sleepless-discount");
        Stats(Osawa, "Dragon", 5, 4, 9, "Air,Dragon Clan,Experienced,Monk,Tattooed,Unique", ["osawa-bow", "osawa-straighten"]);
        Stats(Chokichi, "Crab", 4, 3, 7, "Crab Clan,Samurai,Scout", ["chokichi-attack"]);
        Stats(JiYun, "Unicorn", 5, 3, 8, "Battle Maiden,Cavalry,Paragon,Samurai,Unicorn Clan", [], "jiyun-dishonor-immunity");
        Stats(Yamazaki, "Spider", 4, 3, 7, "Kensai,Monk,Order of Venom,Spider Clan", ["yamazaki-straighten"]);
        Stats(Daiki, "Crab", 2, 4, 7, "Courtier,Crab Clan,Merchant", ["daiki-demand"]);
        Stats(Haruhiko, "Mantis", 4, 3, 8, "Mantis Clan,Naval,Samurai,Scout", ["haruhiko-attack"]);
        var vengeful = CardCatalog.Get(Vengeful);
        Check(vengeful.Type == CardType.Follower && vengeful.Force == 6 && vengeful.GoldCost == 8 && vengeful.Focus == 4 && Keywords(Vengeful) == "Naga,Nonhuman,Unique", "The Vengeful's stats");
        Eq(string.Join(",", vengeful.Abilities ?? []), "vengeful-attack,vengeful-straighten");
        var temple = CardCatalog.Get(Temple);
        Check(temple.Type == CardType.Stronghold && temple.GoldProduction == 4 && temple.ProvinceStrength == 7 && temple.StartingHonor == -1 && temple.Effect == "forgotten-temple"
            && Keywords(Temple) == "Forest,Temple", "The Forgotten Temple's stats");
        Eq(string.Join(",", temple.Abilities ?? []), "forgotten-temple-refill");

        Manifest(root, "personality-wave-66", personalities);
        Manifest(root, "follower-wave-29", Vengeful);
        Manifest(root, "stronghold-wave-15", Temple);
    }

    private static void VengefulAttackAndStraighten()
    {
        var s = Table(Phase.Battle, 0);
        var shakash = Add(s, Shakash, 0, Zone.Battlefield, province: 0);
        var vengeful = Add(s, Vengeful, 0, Zone.Attached, province: 0, parent: shakash.Id);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "vengeful-attack", vengeful.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 7 destroyed the 4F enemy Personality");
        Check(!e.State.Cards[vengeful.Id].Bowed, "the Attack has no bow cost");

        // Bowed Follower, bowed Nonhuman Personality: the whole unit straightens.
        var bowed = Priority(Edit(e, st => { st.Cards[shakash.Id].Bowed = true; st.Cards[vengeful.Id].Bowed = true; }), 0);
        Check(CanUse(bowed, 0, "vengeful-straighten", vengeful.Id), "usable even while The Vengeful is bowed");
        Check(!CanUse(bowed, 0, "vengeful-attack", vengeful.Id), "its other ability is not (and it is spent anyway)");
        Accept(bowed, Use(bowed, 0, "vengeful-straighten", vengeful.Id));
        Check(!bowed.State.Cards[shakash.Id].Bowed && !bowed.State.Cards[vengeful.Id].Bowed, "Nonhuman Personality: his unit straightened");

        // A human Personality: the action may be taken, and nothing straightens.
        var s2 = Table(Phase.Battle, 0);
        var human = Add(s2, Crane, 0, Zone.Battlefield, bowed: true, province: 0);
        var vengeful2 = Add(s2, Vengeful, 0, Zone.Attached, bowed: true, province: 0, parent: human.Id);
        Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "vengeful-straighten", vengeful2.Id));
        Check(e2.State.Cards[human.Id].Bowed && e2.State.Cards[vengeful2.Id].Bowed, "a human Personality: nothing straightens");
    }

    private static void KoyamaWeaken()
    {
        var s = Table(Phase.Battle, 0);
        var koyama = Add(s, Koyama, 0, Zone.Battlefield, province: 0); // Personal Honor 4
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);     // 4F
        var follower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: enemy.Id); // 2F
        Batch336Support.Force(enemy, 1);                                // 5F: stays above 0
        var bystander = Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "koyama-weaken", koyama.Id, enemy.Id));
        Eq(Force(e, enemy), 1, "the Personality lost 4 Force (his Personal Honor)");
        Eq(Force(e, follower), 0, "and so did the Follower in his unit");
        Check(!e.State.Cards[enemy.Id].Bowed && e.State.Cards[follower.Id].Bowed, "only the card whose Force is now 0 bowed");
        Eq(Force(e, bystander), 3, "another unit is untouched");
        Check(!e.State.Cards[bystander.Id].Bowed);
    }

    private static void SasakoNoResolutionBow()
    {
        var s = Table(Phase.Battle, 0);
        s.PriorityPlayerId = 1; // the Combat Segment starts with the Defender
        var sasako = Add(s, Sasako, 0, Zone.Battlefield, province: 0);
        var follower = Add(s, Follower, 0, Zone.Attached, province: 0, parent: sasako.Id);
        var other = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // 2F: the attackers win
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("pass", 0, e.Version));
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { } open; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[sasako.Id].Zone, Zone.Home, "Sasako's unit returned home");
        Check(!e.State.Cards[sasako.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the resolution did not bow Sasako's unit");
        Check(e.State.Cards[other.Id].Zone == Zone.Home && e.State.Cards[other.Id].Bowed, "the rest of the attacking army bowed as usual");
    }

    private static void XiaoBoostAndDestroy()
    {
        var s = Table(Phase.Battle, 0);
        var xiao = Add(s, Xiao, 0, Zone.Battlefield, province: 0);
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var armyFollower = Add(s, Follower, 0, Zone.Attached, province: 0, parent: ally.Id);
        var homeHost = Add(s, Crane, 0, Zone.Home);
        var homeFollower = Add(s, Follower, 0, Zone.Attached, parent: homeHost.Id);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: enemy.Id);
        var lone = Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, armyFollower), 3, "a Follower in Xiao's army has +1F");
        Eq(Force(e, homeFollower), 2, "not one at home");
        Eq(Force(e, enemyFollower), 2, "not one in the enemy army");
        Eq(Force(e, xiao), 6, "Xiao himself is not a Follower");
        var option = Options(e, 0, "xiao-destroy", xiao.Id).First(o => o.Enabled);
        Check(!(option.TargetIds ?? []).Contains(enemy.Id), "an enemy Personality with a Follower is not a legal target");
        Accept(e, Offer(e, 0, "xiao-destroy", xiao.Id, lone.Id));
        Eq(e.State.Cards[lone.Id].Zone, Zone.Dead, "the target enemy card without attachments is destroyed");
    }

    private static void ShihoRecruitAndReturn()
    {
        // "After you Recruit Shiho, lose 1 Honor."
        var s0 = MatchEngine.CreateEconomyScenario().State;
        var shihoInProvince = s0.Players[0].Provinces[0].CardId!;
        s0.Cards[shihoInProvince].DefinitionId = Shiho; s0.Cards[shihoInProvince].FaceUp = true;
        for (var i = 0; i < 4; i++) Add(s0, "training-marketplace", 0, Zone.Home);
        s0.Phase = Phase.Dynasty; s0.ActivePlayerId = 0; s0.PriorityPlayerId = 0;
        var recruit = MatchEngine.FromTestState(s0);
        var honorBefore = Honor(recruit, 0);
        Accept(recruit, new GameCommand("purchase", 0, recruit.Version, CardId: shihoInProvince, SourceIds: recruit.PaymentOptions(0, recruit.QuotePurchase(0, shihoInProvince).Cost).First().SourceIds));
        Eq(recruit.State.Cards[shihoInProvince].Zone, Zone.Home, "recruited");
        Eq(Honor(recruit, 0), honorBefore - 1, "lost 1 Honor");

        var s = Table(Phase.Battle, 0);
        var shiho = Add(s, Shiho, 0, Zone.Battlefield, province: 0);
        var shihoItem = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: shiho.Id);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "shiho-return", shiho.Id, enemy.Id));
        Eq(e.State.Cards[shiho.Id].Zone, Zone.DynastyDeck, "Shiho is in his Dynasty deck");
        Eq(e.State.Players[0].DynastyDeck[0], shiho.Id, "on top");
        Eq(e.State.Cards[shihoItem.Id].Zone, Zone.FateDeck, "his own attachment left play with him (top of its owner's Fate deck)");
        Eq(e.State.Players[1].FateDeck[^1], enemyFollower.Id, "the target's Follower (no attachments) went to the bottom of its Fate deck");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "the target himself had an attachment, so he stays");

        var s2 = Table(Phase.Battle, 0);
        var shiho2 = Add(s2, Shiho, 0, Zone.Battlefield, province: 0);
        var loneEnemy = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "shiho-return", shiho2.Id, loneEnemy.Id));
        Eq(e2.State.Cards[loneEnemy.Id].Zone, Zone.DynastyDeck, "a Personality without attachments goes to the bottom of his Dynasty deck");
        Eq(e2.State.Players[1].DynastyDeck[^1], loneEnemy.Id);
    }

    private static void SawakenWeaken()
    {
        var s = Table(Phase.Battle, 0);
        var sawaken = Add(s, Sawaken, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        Batch336Support.Force(enemy, 3); // 7F
        var e = MatchEngine.FromTestState(s);
        Check(AbilityCatalog.All["sawaken-weaken"].ActionKeywords!.Contains("Iaijutsu"), "an Iaijutsu action");
        Accept(e, Offer(e, 0, "sawaken-weaken", sawaken.Id, enemy.Id));
        Eq(Force(e, enemy), 2, "7F - 5F");
    }

    private static void JunpeiBowAndNextMovement()
    {
        var s = Table(Phase.Battle, 0);
        var junpei = Add(s, Junpei, 0, Zone.Home);
        var mover = Add(s, Shakash, 0, Zone.Battlefield, province: 0);
        var secondMover = Add(s, Shakash, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "junpei-base-bow", junpei.Id), "usable while Junpei is at home");
        Accept(e, Offer(e, 0, "junpei-base-bow", junpei.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the target bowed");
        var first = Priority(e, 0);
        Accept(first, Offer(first, 0, "shakash-move-home", mover.Id, enemy.Id));
        Eq(first.State.Cards[enemy.Id].Zone, Zone.Battlefield, "his next movement was negated");
        var second = Priority(first, 0);
        Accept(second, Offer(second, 0, "shakash-move-home", secondMover.Id, enemy.Id));
        Eq(second.State.Cards[enemy.Id].Zone, Zone.Home, "only the next one: the movement after it happens");
    }

    private static void MakikoFavor()
    {
        var s = Table(Phase.Action, 0);
        var makiko = Add(s, Makiko, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "makiko-favor", makiko.Id));
        Eq(e.State.FavorPlayerId, 0, "took the Imperial Favor");
        Check(e.State.Cards[makiko.Id].Bowed, "Makiko bowed as the cost");
    }

    private static void ShakashDiscountAndBattle()
    {
        MatchEngine Quote(string def, int nonhumans, out string cardId)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            cardId = s.Players[0].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = def; s.Cards[cardId].FaceUp = true;
            for (var i = 0; i < nonhumans; i++) Add(s, Sleepless, 0, Zone.Home);
            Add(s, Sleepless, 1, Zone.Home); // another player's Nonhuman does not count
            s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        var two = Quote(Shakash, 2, out var shakashId);
        Eq(two.QuotePurchase(0, shakashId).Cost, 5, "two Nonhuman: 7 - 2");
        var four = Quote(Shakash, 4, out shakashId);
        Eq(four.QuotePurchase(0, shakashId).Cost, 4, "four Nonhuman: at most 3 less");
        var none = Quote(Shakash, 0, out shakashId);
        Eq(none.QuotePurchase(0, shakashId).Cost, 7, "none: the printed cost");

        var s2 = Table(Phase.Battle, 0);
        var shakash = Add(s2, Shakash, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "shakash-bow", shakash.Id, enemy.Id));
        Check(e2.State.Cards[enemy.Id].Bowed, "bowed");
        Check(!CanUse(Priority(e2, 0), 0, "shakash-move-home", shakash.Id), "bow OR move home: one shared use");
    }

    private static void SleeplessDiscountAndBoost()
    {
        var s0 = MatchEngine.CreateEconomyScenario().State;
        var inProvince = s0.Players[0].Provinces[0].CardId!;
        s0.Cards[inProvince].DefinitionId = Sleepless; s0.Cards[inProvince].FaceUp = true;
        s0.Phase = Phase.Dynasty; s0.ActivePlayerId = 0; s0.PriorityPlayerId = 0;
        Eq(MatchEngine.FromTestState(s0).QuotePurchase(0, inProvince).Cost, 6, "7 - 1");

        var s = Table(Phase.Battle, 0);
        var sleepless = Add(s, Sleepless, 0, Zone.Battlefield, province: 0);
        var nonhuman = Add(s, Shakash, 0, Zone.Battlefield, province: 0);
        var human = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var homeNonhuman = Add(s, Shakash, 0, Zone.Home);
        var pearl = Add(s, Sleepless, 0, Zone.Home);
        var forest = s.Cards[s.Players[0].StrongholdId]; forest.DefinitionId = Temple; // a Forest
        var spare = Add(s, Sleepless, 0, Zone.Home);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "sleepless-boost", sleepless.Id, performerId: pearl.Id));
        Check(e.State.Cards[pearl.Id].Bowed, "the Pearl bowed as the cost");
        Eq(Force(e, nonhuman), 5, "a Nonhuman at the battlefield +1F");
        Eq(Force(e, sleepless), 5, "himself too");
        Eq(Force(e, human), 3, "not a human");
        Eq(Force(e, homeNonhuman), 4, "not a Nonhuman at home");
        var again = Priority(e, 0);
        Accept(again, Use(again, 0, "sleepless-boost", sleepless.Id, performerId: forest.Id));
        Check(again.State.Cards[forest.Id].Bowed, "a Forest bowed as the cost");
        Eq(Force(again, nonhuman), 6, "twice");
        var third = Priority(again, 0);
        Check(!CanUse(third, 0, "sleepless-boost", sleepless.Id, spare.Id), "only twice per turn");
    }

    private static void OsawaAtHisLocation()
    {
        var s = Table(Phase.Action, 0);
        var osawa = Add(s, Osawa, 0, Zone.Home, bowed: true);
        var own = Add(s, Crane, 0, Zone.Home);
        var enemyAtHome = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 0, "osawa-bow", osawa.Id).First(o => o.Enabled);
        Check((option.TargetIds ?? []).Contains(own.Id) && !(option.TargetIds ?? []).Contains(enemyAtHome.Id), "Osawa's location is his controller's home, not the other player's");
        Accept(e, Offer(e, 0, "osawa-bow", osawa.Id, own.Id));
        Check(e.State.Cards[own.Id].Bowed, "bowed, even though Osawa was bowed");
        Check(!CanUse(Priority(e, 0), 0, "osawa-straighten", osawa.Id), "bow OR straighten: one shared use");

        var s2 = Table(Phase.Battle, 0);
        var osawa2 = Add(s2, Osawa, 0, Zone.Battlefield, bowed: true, province: 0);
        var enemy = Add(s2, Lion, 1, Zone.Battlefield, bowed: true, province: 0);
        var away = Add(s2, Crane, 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        var battleOption = Options(e2, 0, "osawa-straighten", osawa2.Id).First(o => o.Enabled);
        Check(!(battleOption.TargetIds ?? []).Contains(away.Id), "a Personality at home is not at his battlefield");
        Accept(e2, Offer(e2, 0, "osawa-straighten", osawa2.Id, enemy.Id));
        Check(!e2.State.Cards[enemy.Id].Bowed, "straightened at his battlefield");
    }

    private static void ChokichiAttack()
    {
        var s = Table(Phase.Battle, 0);
        var chokichi = Add(s, Chokichi, 0, Zone.Battlefield, province: 0);
        var follower = Add(s, Follower, 0, Zone.Attached, province: 0, parent: chokichi.Id);
        var enemy = Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // 2F
        var enemyFollower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 0, "chokichi-attack", chokichi.Id).First(o => o.Enabled);
        Check((option.TargetIds ?? []).Contains(enemyFollower.Id) && !(option.TargetIds ?? []).Contains(enemy.Id), "without Reconnaissance a Personality with Followers is no target");
        var recon = Edit(e, st => st.Players[1].Provinces[0].Reconnaissance[0] = true);
        Accept(recon, Offer(recon, 0, "chokichi-attack", chokichi.Id, enemy.Id, follower.Id));
        Check(recon.State.Cards[follower.Id].Bowed, "the performing Follower bowed as the cost");
        Eq(recon.State.Cards[enemy.Id].Zone, Zone.Dead, "with Reconnaissance: Ranged 4 destroyed the Personality with a Follower");
    }

    private static void JiYunDishonorImmunity()
    {
        // Another player's action.
        var s = Table(Phase.Action, 1);
        var jiyun = Add(s, JiYun, 0, Zone.Home);
        var yoshihara = Add(s, Yoshihara, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "yoshihara-dishonor", yoshihara.Id, jiyun.Id));
        Check(!e.State.Cards[jiyun.Id].Dishonorable, "another player's card cannot dishonor her");
        // Her own controller's card can.
        var s2 = Table(Phase.Action, 0);
        var jiyun2 = Add(s2, JiYun, 0, Zone.Home);
        var ownYoshihara = Add(s2, Yoshihara, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "yoshihara-dishonor", ownYoshihara.Id, jiyun2.Id));
        Check(e2.State.Cards[jiyun2.Id].Dishonorable, "her own controller's card dishonors her");
        // Another player's card through an Either/Or answered by her own controller (Yasuki Makoto): still that player's card's effect.
        var s3 = Table(Phase.Battle, 1);
        var makoto = Add(s3, Makoto, 1, Zone.Home);
        var jiyun3 = Add(s3, JiYun, 0, Zone.Battlefield, province: 0);
        Add(s3, Lion, 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "makoto-lock", makoto.Id, jiyun3.Id));
        Accept(e3, new GameCommand("chooseEitherOr", 0, e3.Version, Choice: "refuse"));
        Check(!e3.State.Cards[jiyun3.Id].Dishonorable && e3.State.Cards[jiyun3.Id].Zone == Zone.Home, "the dishonoring is negated; the move home is not");
    }

    private static void YamazakiStraighten()
    {
        var s = Table(Phase.Action, 0);
        var yamazaki = Add(s, Yamazaki, 0, Zone.Home);
        var own = Add(s, "training-marketplace", 0, Zone.Home, bowed: true);
        var theirs = Add(s, "training-marketplace", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 0, "yamazaki-straighten", yamazaki.Id).First(o => o.Enabled);
        Check(!(option.TargetIds ?? []).Contains(theirs.Id), "only his controller's Holding");
        Accept(e, Offer(e, 0, "yamazaki-straighten", yamazaki.Id, own.Id));
        Check(!e.State.Cards[own.Id].Bowed, "straightened");
    }

    private static void DaikiDemand()
    {
        // Taken on the other player's turn: the target is one of the Attacker's own Personalities this turn.
        MatchEngine Build(int active, out CardInstance target)
        {
            var s = Table(Phase.Action, active);
            s.PriorityPlayerId = 0;
            var daiki = Add(s, Daiki, 0, Zone.Home);
            target = Add(s, Lion, 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "daiki-demand", daiki.Id, target.Id));
            Eq(e.State.PendingEitherOr?.SpecId, EitherOrSpecs.DaikiPayLoseOrLock);
            Eq(e.State.PendingEitherOr?.DeciderPlayerId, 1, "the target's controller decides");
            return e;
        }
        var neither = Build(1, out var target);
        Accept(neither, new GameCommand("chooseEitherOr", 1, neither.Version, Choice: "neither"));
        Check(neither.State.Cards[target.Id].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Assign) && l.Kinds.HasFlag(MovementKinds.ToBattlefield)), "neither: he may not be assigned or move to the attack");
        var honor = Build(1, out target);
        var before = Honor(honor, 1);
        Accept(honor, new GameCommand("chooseEitherOr", 1, honor.Version, Choice: "honor"));
        Eq(Honor(honor, 1), before - 2, "lost 2 Honor");
        Check(honor.State.Cards[target.Id].MovementLocks.Count == 0, "and nothing else");
        // On Daiki's own turn the target defends: nothing he could do puts him in an attacking army.
        var ownTurn = Build(0, out target);
        Accept(ownTurn, new GameCommand("chooseEitherOr", 1, ownTurn.Version, Choice: "neither"));
        Check(ownTurn.State.Cards[target.Id].MovementLocks.Count == 0, "no lock on a Personality that cannot attack this turn");
    }

    private static void HaruhikoAttack()
    {
        var s = Table(Phase.Battle, 0);
        var haruhiko = Add(s, Haruhiko, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0); // 4F
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("haruhiko-attack", haruhiko.Id), 3);
        var recon = Edit(e, st => st.Players[1].Provinces[0].Reconnaissance[0] = true);
        Eq(recon.AttackStrengthForTest("haruhiko-attack", haruhiko.Id), 5, "+2 with Reconnaissance");
        Check(Options(e, 0, "haruhiko-attack", haruhiko.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(enemy.Id)), "a 4F target is still a legal target of Ranged 3");
        Accept(e, Offer(e, 0, "haruhiko-attack", haruhiko.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "but Ranged 3 does not destroy it");
        Accept(recon, Offer(recon, 0, "haruhiko-attack", haruhiko.Id, enemy.Id));
        Eq(recon.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 5 does");
    }

    private static void ForgottenTemple()
    {
        MatchState WithTemple(Phase phase, int active)
        {
            var s = Table(phase, active);
            s.Cards[s.Players[0].StrongholdId].DefinitionId = Temple;
            return s;
        }
        // No Political actions.
        var s = WithTemple(Phase.Action, 0);
        var makiko = Add(s, Makiko, 0, Zone.Home);
        var tojikana = Add(s, Tojikana, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "makiko-favor", makiko.Id), "a Political action is not offered");
        Check(Rejection(e, Use(e, 0, "makiko-favor", makiko.Id)).Contains("Political"), "and is refused");
        Check(!CanUse(e, 0, "lobby"), "nor is Lobby");
        // No Honor gain.
        var before = Honor(e, 0);
        Accept(e, Use(e, 0, "tojikana-honor", tojikana.Id));
        Eq(Honor(e, 0), before, "You do not gain Honor");

        // Another player's card: the loss is reduced to 1.
        var s2 = WithTemple(Phase.Action, 0);
        s2.PriorityPlayerId = 1;
        var daiki = Add(s2, Daiki, 1, Zone.Home);
        var mine = Add(s2, Crane, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "daiki-demand", daiki.Id, mine.Id));
        var before2 = Honor(e2, 0);
        Accept(e2, new GameCommand("chooseEitherOr", 0, e2.Version, Choice: "honor"));
        Eq(Honor(e2, 0), before2 - 1, "a 2-Honor loss from another player's card is reduced to 1");

        // A card of his own: no loss at all (Ninube Shiho's recruit loss).
        var s3 = MatchEngine.CreateEconomyScenario().State;
        s3.Cards[s3.Players[0].StrongholdId].DefinitionId = Temple;
        var shiho = s3.Players[0].Provinces[0].CardId!;
        s3.Cards[shiho].DefinitionId = Shiho; s3.Cards[shiho].FaceUp = true;
        for (var i = 0; i < 4; i++) Add(s3, "training-marketplace", 0, Zone.Home);
        s3.Phase = Phase.Dynasty; s3.ActivePlayerId = 0; s3.PriorityPlayerId = 0;
        var e3 = MatchEngine.FromTestState(s3);
        var before3 = Honor(e3, 0);
        Accept(e3, new GameCommand("purchase", 0, e3.Version, CardId: shiho, SourceIds: e3.PaymentOptions(0, e3.QuotePurchase(0, shiho).Cost).First().SourceIds));
        Eq(Honor(e3, 0), before3, "no Honor lost from a card he owns");

        // Limited: refill a province face-up with a discarded Nonhuman Personality, discarding the card there.
        var s4 = WithTemple(Phase.Action, 0);
        var nonhuman = Add(s4, Shakash, 0, Zone.DynastyDiscard);
        var human = Add(s4, Crane, 0, Zone.DynastyDiscard);
        var templeId = s4.Players[0].StrongholdId;
        var displaced = s4.Players[0].Provinces[1].CardId!;
        var e4 = MatchEngine.FromTestState(s4);
        var refill = Options(e4, 0, "forgotten-temple-refill", templeId).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(refill.Contains(nonhuman.Id) && !refill.Contains(human.Id), "only a Nonhuman Personality: " + string.Join(",", refill));
        Accept(e4, new GameCommand("useAbility", 0, e4.Version, templeId, AbilityId: "forgotten-temple-refill", TargetId: nonhuman.Id, Province: 1));
        Eq(e4.State.Players[0].Provinces[1].CardId, nonhuman.Id, "the province holds the Personality");
        Check(e4.State.Cards[nonhuman.Id].Zone == Zone.Province && e4.State.Cards[nonhuman.Id].FaceUp, "face-up");
        Eq(e4.State.Cards[displaced].Zone, Zone.DynastyDiscard, "the card there was discarded");
    }
}
