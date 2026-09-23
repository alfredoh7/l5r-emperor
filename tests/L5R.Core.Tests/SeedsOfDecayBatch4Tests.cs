using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Seeds of Decay pass (issue #453), batch 4: the cards of personality-wave-58/60, holding-wave-7, region-wave-6, event-wave-5 and follower-wave-26, driven through
// QueryActions and the commands it offers, and the primitives they introduced (StaticKeywordRules.cs, AttachmentTransfers.cs, the Action-phase additional action, the
// any-of search criterion). Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion).
internal static class SeedsOfDecayBatch4Tests
{
    private const string Aimi = "oracle-l5r-10061", Mukudori = "oracle-l5r-10092", Nagi = "oracle-l5r-10095", Zansho = "oracle-l5r-10097", Nobukatsu = "oracle-l5r-10069",
        Mahatsu = "oracle-l5r-10093", Yamato = "oracle-l5r-10085", Sawai = "oracle-l5r-10042", Katsuo = "oracle-l5r-10101", Detached = "oracle-l5r-10201", NinjaProxy = "oracle-l5r-10200",
        Temple = "oracle-l5r-10039", Hachi = "oracle-l5r-10034", IncenseMill = "oracle-l5r-10035", Palace = "oracle-l5r-10036", Port = "oracle-l5r-10037",
        TestedLand = "oracle-l5r-10104", Reverence = "oracle-l5r-10031", Detachment = "oracle-l5r-10105";

    public static void Run(TestRunner runner) => runner.Run("seeds-batch-4", [
        ("Published shape: every card of the six waves prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("Ikoma Aimi: her controller's Lion Clan Personalities in play have Courtier and not Cavalry/Commander/Deathseeker/Paragon/Scout/Tactician; not a Crane, not the opponent's Lion, not once she has left play", AimiStaticKeywords),
        ("Tsuruchi Nobukatsu - exp: his controller's Mantis Clan Samurai have Magistrate in and out of play; Seppun Nagi: other non-Loyal Magistrates have the Clan alignment in and out of play (a Clan discount on the recruit), never a Loyal one or Nagi himself; Katsuo has no Brash against a Lion", NobukatsuNagiKatsuoStaticKeywords),
        ("Zansho and the Sensei entry: recruiting him discards the other Sensei at home; his controller's Followers have +1F or their Honor Requirement, whichever is greater", ZanshoSensei),
        ("Mukudori: other Fudo cards are recruited or equipped for 1 less; the Limited bows exactly three of his controller's unbowed Fudo cards (himself included) and draws; with two it is not offered", MukudoriFudo),
        ("Nobukatsu's Battle bows a Personality at any location -- one at home included -- and is refused against one at a battlefield already resolved", NobukatsuBowAnywhere),
        ("Naka Mahatsu: before an action with an element keyword resolves (any player's, whatever it targets) his Reaction negates it in the Action phase; a plain action opens nothing; the search finds a Thunder/element card only", MahatsuNegateAndSearch),
        ("Daigotsu Yamato: entry loses 1 Honor and, against a Scorpion, Brash for good; the Open half transfers his Follower from another Personality (attaching restrictions hold) and grants an additional Action-phase action that keeps priority until spent", YamatoTransferAndActionPhaseAdditionalAction),
        ("Hiruma Sawai: transfers a Follower between two Personalities of the same player at the battlefield; a Personality of the other player is refused as the destination; the additional action only with Reconnaissance", SawaiTransfer),
        ("Shinjo Katsuo: straightens his own bowed Follower and grants an additional action; the opponent's Follower is refused", KatsuoStraighten),
        ("Cavalry Detachment: when another player's action targets its Personality, it is removed from the game, a Samurai Cavalry Personality with its controller's Clan is created at his location and his other attachments transfer to it; the held action resolves on the bare target", CavalryDetachmentSpawn),
        ("Temple to Fudo: enters losing 3 Honor, bowing it for Gold loses 1 Honor, and it stays bowed through its controller's second turn to straighten in the third", TempleToFudo),
        ("Hachi's Revenge produces 3 for a Crane player and 2 for another; its Open bows a non-Unique Holding and refuses a Unique one. Second City Port bows itself to straighten a Market or Port, never a Farm", HachiAndPort),
        ("Incense Mill negates every current Force penalty (modifiers and tokens, not bonuses) and every action lock on the target. Overlooked Palace is destroyed to create a Ninja Personality with the creator's Clan", IncenseMillAndPalace),
        ("Tested Land: revealed, it attaches to the enemy province its owner chooses among those without a Region and is controlled by that player; a Holding revealed there is discarded with it, and it returns to its owner's discard pile", TestedLandFlow),
        ("Offering Reverence: creates one Samurai Ancestor Spirit with the owner's Clan per dead Personality; with none it may be shuffled into the deck instead of discarded", OfferingReverenceFlow),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void GiveAbility(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));

    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        var aimi = CardCatalog.Get(Aimi);
        Check(aimi.Type == CardType.Personality && aimi.Clan == "Lion" && aimi.Force == 3 && aimi.Chi == 4 && aimi.PersonalHonor == 2 && aimi.GoldCost == 7 && aimi.HonorRequirement == 5
            && Keywords(Aimi) == "Courtier,Lion Clan,Loyal,Samurai,Sensei" && aimi.Effect == "aimi-lion-courtier" && aimi.Abilities!.Length == 0, "Ikoma Aimi");
        var mukudori = CardCatalog.Get(Mukudori);
        Check(mukudori.Force == 0 && mukudori.Chi == 5 && mukudori.PersonalHonor == 2 && mukudori.GoldCost == 7 && mukudori.HonorRequirement is null && Keywords(Mukudori) == "Fudo,Sensei,Unique"
            && mukudori.Effect == "mukudori-fudo-discount" && mukudori.Abilities!.SequenceEqual(["mukudori-bow-fudo-draw"]), "Mukudori");
        var nagi = CardCatalog.Get(Nagi);
        Check(nagi.Force == 3 && nagi.Chi == 4 && nagi.PersonalHonor == 2 && nagi.GoldCost == 5 && nagi.Clan == "Unaligned" && Keywords(Nagi) == "Emerald Magistrate,Imperial,Magistrate,Sensei"
            && nagi.Effect == "nagi-magistrate-clan" && nagi.Abilities!.Length == 0, "Seppun Nagi");
        var zansho = CardCatalog.Get(Zansho);
        Check(zansho.Force == 3 && zansho.Chi == 3 && zansho.PersonalHonor == 3 && zansho.GoldCost == 3 && zansho.HonorRequirement == 3 && Keywords(Zansho) == "Samurai,Sensei"
            && zansho.Effect == "zansho-follower-force" && zansho.Abilities!.Length == 0, "Zansho");
        var nobukatsu = CardCatalog.Get(Nobukatsu);
        Check(nobukatsu.Name == "Tsuruchi Nobukatsu" && nobukatsu.Clan == "Mantis" && nobukatsu.Force == 4 && nobukatsu.Chi == 4 && nobukatsu.PersonalHonor == 2 && nobukatsu.GoldCost == 9 && nobukatsu.HonorRequirement == 0
            && Keywords(Nobukatsu) == "Experienced,Magistrate,Mantis Clan,Naval,Ruby Champion,Samurai,Sensei,Unique" && nobukatsu.Effect == "nobukatsu-mantis-magistrate" && nobukatsu.Abilities!.SequenceEqual(["nobukatsu-bow"]), "Tsuruchi Nobukatsu - exp");
        var mahatsu = CardCatalog.Get(Mahatsu);
        Check(mahatsu.Force == 5 && mahatsu.Chi == 6 && mahatsu.PersonalHonor == 2 && mahatsu.GoldCost == 12 && mahatsu.HonorRequirement == 4
            && Keywords(Mahatsu) == "Air,Alchemist,Earth,Experienced Mahatsu,Fire,Grand Master of the Elements,Ronin,Shugenja,Thunder,Unique,Void,Water"
            && mahatsu.Abilities!.SequenceEqual(["mahatsu-negate", "mahatsu-search"]), "Naka Mahatsu - exp");
        Eq(AbilityCatalog.All["mahatsu-negate"].Trigger, "before-action-resolves"); Check(AbilityCatalog.All["mahatsu-search"].Search == SearchCatalog.NakaMahatsu, "the search spec");
        var yamato = CardCatalog.Get(Yamato);
        Check(yamato.Clan == "Spider" && yamato.Force == 4 && yamato.Chi == 2 && yamato.PersonalHonor == 0 && yamato.GoldCost == 6 && Keywords(Yamato) == "Brash,Commander,Conqueror,Samurai,Shadowlands,Spider Clan"
            && yamato.EntryHonorLoss == 1 && yamato.Effect == "yamato-entry-lose-brash-vs-scorpion" && yamato.Abilities!.SequenceEqual(["yamato-transfer"]), "Daigotsu Yamato");
        Eq(AbilityCatalog.All["yamato-transfer"].Timing, "BattleOrOpen");
        var sawai = CardCatalog.Get(Sawai);
        Check(sawai.Clan == "Crab" && sawai.Force == 3 && sawai.Chi == 2 && sawai.PersonalHonor == 1 && sawai.GoldCost == 6 && Keywords(Sawai) == "Crab Clan,Fudo,Samurai,Scout"
            && sawai.Abilities!.SequenceEqual(["sawai-transfer"]) && AbilityCatalog.All["sawai-transfer"].Slots!.Length == 3, "Hiruma Sawai");
        var katsuo = CardCatalog.Get(Katsuo);
        Check(katsuo.Clan == "Unicorn" && katsuo.Force == 4 && katsuo.Chi == 2 && katsuo.PersonalHonor == 2 && katsuo.GoldCost == 7 && Keywords(Katsuo) == "Brash,Cavalry,Commander,Samurai,Unicorn Clan"
            && katsuo.Effect == "loses-brash-vs-lion" && katsuo.Abilities!.SequenceEqual(["katsuo-straighten"]), "Shinjo Katsuo");
        Manifest(root, "personality-wave-58", Aimi, Mukudori, Nagi, Zansho, Nobukatsu, Mahatsu, Yamato, Sawai, Katsuo);

        var detached = CardCatalog.Get(Detached);
        Check(detached.Type == CardType.Personality && detached.CreatedOnly && detached.Force == 3 && detached.Chi == 1 && detached.PersonalHonor == 2 && Keywords(Detached) == "Cavalry,Samurai", "Detached Personality");
        var ninja = CardCatalog.Get(NinjaProxy);
        Check(ninja.Type == CardType.Personality && ninja.CreatedOnly && ninja.Force == 0 && ninja.Chi == 2 && ninja.PersonalHonor == 0 && Keywords(NinjaProxy) == "Ninja" && ninja.Clan == "Unaligned", "Ninja");
        Manifest(root, "personality-wave-60", Detached, NinjaProxy);
        Eq(CreatedCards.Verify().Length, 0, "every creator resolves its scan: " + string.Join(" | ", CreatedCards.Verify()));

        var temple = CardCatalog.Get(Temple);
        Check(temple.Type == CardType.Holding && temple.GoldCost == 0 && temple.GoldProduction == 2 && Keywords(Temple) == "Fudo,Temple" && temple.Effect == "temple-to-fudo" && temple.EntryHonorLoss == 3 && temple.Abilities!.Length == 0, "Temple to Fudo");
        var hachi = CardCatalog.Get(Hachi);
        Check(hachi.GoldCost == 4 && hachi.GoldProduction == 2 && Keywords(Hachi) == "Ship,Unique" && hachi.Effect == "crane-gold" && hachi.Abilities!.SequenceEqual(["hachis-revenge-bow"]), "Hachi's Revenge");
        var mill = CardCatalog.Get(IncenseMill);
        Check(mill.GoldCost == 2 && mill.GoldProduction == 2 && Keywords(IncenseMill) == "" && mill.Abilities!.SequenceEqual(["incense-mill-cleanse"]), "Incense Mill");
        var palace = CardCatalog.Get(Palace);
        Check(palace.GoldCost == 2 && palace.GoldProduction == 2 && Keywords(Palace) == "Ninja" && palace.Abilities!.SequenceEqual(["overlooked-palace-create"]), "Overlooked Palace");
        var port = CardCatalog.Get(Port);
        Check(port.GoldCost == 2 && port.GoldProduction == 2 && Keywords(Port) == "Port" && port.Abilities!.SequenceEqual(["second-city-port-straighten"]), "Second City Port");
        Manifest(root, "holding-wave-7", Temple, Hachi, IncenseMill, Palace, Port);

        var land = CardCatalog.Get(TestedLand);
        Check(land.Type == CardType.Region && land.GoldCost == 0 && land.Effect == "tested-land" && land.Abilities!.Length == 0, "Tested Land");
        Manifest(root, "region-wave-6", TestedLand);
        var reverence = CardCatalog.Get(Reverence);
        Check(reverence.Type == CardType.Event && Keywords(Reverence) == "Festival,Unique" && reverence.Effect == "offering-reverence" && reverence.Abilities!.Length == 0, "Offering Reverence");
        Manifest(root, "event-wave-5", Reverence);
        var detachment = CardCatalog.Get(Detachment);
        Check(detachment.Type == CardType.Follower && detachment.Force == 3 && detachment.Chi == 0 && detachment.GoldCost == 4 && detachment.Focus == 2 && detachment.HonorRequirement == 0 && Keywords(Detachment) == "Cavalry"
            && detachment.Abilities!.SequenceEqual(["cavalry-detachment-spawn"]), "Cavalry Detachment");
        var spawn = AbilityCatalog.All["cavalry-detachment-spawn"];
        Check(spawn.Timing == "Reaction" && spawn.Trigger == ReactionTriggers.TargetedByAction && spawn.Creates!.ControllerClan && spawn.Costs![0].Verb == CostVerbs.Remove, "the spawn's shape");
        Manifest(root, "follower-wave-26", Detachment);
    }

    private static void AimiStaticKeywords()
    {
        var s = Table(Phase.Action, 1);
        var aimi = Add(s, Aimi, 1, Zone.Home);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(lion, "Tactician"); GiveKeyword(lion, "Cavalry");
        var crane = Add(s, "training-crane-samurai", 1, Zone.Home); GiveKeyword(crane, "Scout");
        var enemyLion = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(enemyLion, "Scout");
        var lionInProvince = Add(s, "training-lion-samurai", 1, Zone.Province, province: 1); s.Players[1].Provinces[1].CardId = lionInProvince.Id;
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(lion.Id, "Courtier") && !e.HasKeyword(lion.Id, "Tactician") && !e.HasKeyword(lion.Id, "Cavalry") && e.HasKeyword(aimi.Id, "Samurai"), "her Lion Clan Personality in play: Courtier, no Tactician or Cavalry; everything else kept");
        Check(e.HasKeyword(aimi.Id, "Courtier") && !e.HasKeyword(aimi.Id, "Scout"), "Aimi herself is one of her Lion Clan Personalities");
        Check(!e.HasKeyword(crane.Id, "Courtier") && e.HasKeyword(crane.Id, "Scout"), "a Crane of hers is untouched");
        Check(!e.HasKeyword(enemyLion.Id, "Courtier") && e.HasKeyword(enemyLion.Id, "Scout"), "the opponent's Lion is untouched");
        Check(!e.HasKeyword(lionInProvince.Id, "Courtier"), "'(in play)': a Lion in a province is untouched");
        // Once she leaves play the rule is gone.
        var gone = Edit(e, st => { st.Cards[aimi.Id].Zone = Zone.Dead; });
        Check(!gone.HasKeyword(lion.Id, "Courtier") && gone.HasKeyword(lion.Id, "Tactician"), "with Aimi dead the Lion is himself again");
    }

    private static void NobukatsuNagiKatsuoStaticKeywords()
    {
        // Nobukatsu (player 1's): Mantis Clan Samurai in play, in a province and in the deck get Magistrate; a Mantis Shugenja and the opponent's Mantis Samurai do not.
        var s = Table(Phase.Action, 1);
        var nobukatsu = Add(s, Nobukatsu, 1, Zone.Home);
        var mantis = Add(s, "training-lion-samurai", 1, Zone.Home); GiveKeyword(mantis, "Mantis Clan"); GiveKeyword(mantis, "Samurai");
        var inDeck = Add(s, "training-lion-samurai", 1, Zone.DynastyDeck); GiveKeyword(inDeck, "Mantis Clan"); GiveKeyword(inDeck, "Samurai"); s.Players[1].DynastyDeck.Add(inDeck.Id);
        var shugenja = Add(s, "training-water-shugenja", 1, Zone.Home); GiveKeyword(shugenja, "Mantis Clan");
        var enemyMantis = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(enemyMantis, "Mantis Clan"); GiveKeyword(enemyMantis, "Samurai");
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(mantis.Id, "Magistrate") && e.HasKeyword(inDeck.Id, "Magistrate") && e.HasKeyword(nobukatsu.Id, "Magistrate"), "his Mantis Clan Samurai in and out of play are Magistrates");
        Check(!e.HasKeyword(shugenja.Id, "Magistrate") && !e.HasKeyword(enemyMantis.Id, "Magistrate"), "not a Mantis who is no Samurai, not the opponent's");

        // Nagi (player 0's, the Crane): other non-Loyal Magistrate Personalities have the Crane alignment wherever they are -- Unaligned no more, and the Clan discount on the recruit.
        var s2 = Table(Phase.Dynasty, 0);
        var nagi = Add(s2, Nagi, 0, Zone.Home);
        var magistrate = Add(s2, "training-magistrate-personality", 0, Zone.Home);
        var loyal = Add(s2, "training-magistrate-personality", 0, Zone.Home); GiveKeyword(loyal, "Loyal");
        var inProvince = Add(s2, "training-magistrate-personality", 0, Zone.Province, province: 0); s2.Players[0].Provinces[0].CardId = inProvince.Id;
        var plain = Add(s2, "training-magistrate-personality", 0, Zone.Province, province: 1); s2.Players[0].Provinces[1].CardId = plain.Id; plain.RemovedKeywords.Add("Magistrate");
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.HasKeyword(magistrate.Id, "Crane Clan") && !e2.HasKeyword(magistrate.Id, "Unaligned"), "a Magistrate of his controller's has the Crane Clan alignment");
        Check(!e2.HasKeyword(loyal.Id, "Crane Clan") && e2.HasKeyword(loyal.Id, "Unaligned"), "a Loyal one does not");
        Check(!e2.HasKeyword(nagi.Id, "Crane Clan") && e2.HasKeyword(nagi.Id, "Unaligned"), "nor Nagi himself ('other')");
        Check(e2.HasKeyword(inProvince.Id, "Crane Clan"), "out of play too: in a province");
        Check(e2.QuotePurchase(0, inProvince.Id, clanDiscount: true).Error is null, "the Clan discount is available on the recruit of a Crane Clan Personality");
        Check(e2.QuotePurchase(0, plain.Id, clanDiscount: true).Error is not null, "and not on a Personality who is no Magistrate");
        var noNagi = Edit(e2, st => { st.Cards[nagi.Id].Zone = Zone.Dead; });
        Check(!noNagi.HasKeyword(magistrate.Id, "Crane Clan") && noNagi.QuotePurchase(0, inProvince.Id, clanDiscount: true).Error is not null, "with Nagi gone the alignment is gone");

        // Katsuo: no Brash while the other player is Lion Clan (player 1 here); the Lion player's own Katsuo faces a Crane and keeps it.
        var s3 = Table(Phase.Action, 0);
        var craneKatsuo = Add(s3, Katsuo, 0, Zone.Home);
        var lionKatsuo = Add(s3, Katsuo, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!e3.HasKeyword(craneKatsuo.Id, "Brash") && e3.HasKeyword(craneKatsuo.Id, "Cavalry"), "the Crane player's Katsuo has no Brash: the other player is Lion Clan");
        Check(e3.HasKeyword(lionKatsuo.Id, "Brash"), "the Lion player's Katsuo keeps Brash: the other player is Crane");
    }

    private static void ZanshoSensei()
    {
        // Recruiting Zansho discards the other Sensei at home (the entry trait carried with his Effect).
        var s = Table(Phase.Dynasty, 0);
        var zansho = Add(s, Zansho, 0, Zone.Province, province: 0); s.Players[0].Provinces[0].CardId = zansho.Id;
        var otherSensei = Add(s, "training-sensei-entry", 0, Zone.Home);
        var notSensei = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, zansho.Id);
        Check(quote.Error is null, quote.Error ?? "");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: zansho.Id, SourceIds: quote.Payments.First().SourceIds));
        Eq(e.State.Cards[otherSensei.Id].Zone, Zone.DynastyDiscard, "the other Sensei was discarded");
        Eq(e.State.Cards[notSensei.Id].Zone, Zone.Home, "a Personality who is no Sensei stays");
        Eq(e.State.Cards[zansho.Id].Zone, Zone.Home, "Zansho himself is in play");

        // His Followers: +1F, or their Honor Requirement when greater; not the opponent's.
        var s2 = Table(Phase.Action, 0);
        var host = Add(s2, "training-lion-samurai", 0, Zone.Home);
        var low = Add(s2, "training-follower", 0, Zone.Attached, parent: host.Id);                       // Honor Requirement 1: +1
        var high = Add(s2, "training-follower", 0, Zone.Attached, parent: host.Id); high.Printed = CardCatalog.Get("training-follower") with { HonorRequirement = 3 }; // +3
        var none = Add(s2, "training-cavalry-follower", 0, Zone.Attached, parent: host.Id);              // no Honor Requirement: +1
        var enemyHost = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var enemyFollower = Add(s2, "training-follower", 1, Zone.Attached, parent: enemyHost.Id);
        var without = MatchEngine.FromTestState(s2);
        var baseline = (without.CardForceForTest(low.Id), without.CardForceForTest(high.Id), without.CardForceForTest(none.Id));
        Add(s2, Zansho, 0, Zone.Home);
        var with = MatchEngine.FromTestState(s2);
        Eq(with.CardForceForTest(low.Id), baseline.Item1 + 1, "Honor Requirement 1: +1F");
        Eq(with.CardForceForTest(high.Id), baseline.Item2 + 3, "Honor Requirement 3: +3F");
        Eq(with.CardForceForTest(none.Id), baseline.Item3 + 1, "no Honor Requirement: +1F");
        Eq(with.CardForceForTest(enemyFollower.Id), without.CardForceForTest(enemyFollower.Id), "the opponent's Follower is untouched");
    }

    private static void MukudoriFudo()
    {
        // The discount: a Fudo Personality in a province and a Fudo attachment from hand both cost 1 less with Mukudori in play; a plain card does not; Mukudori himself never.
        var s = Table(Phase.Dynasty, 0);
        var fudo = Add(s, "training-fudo-personality", 0, Zone.Province, province: 0); s.Players[0].Provinces[0].CardId = fudo.Id;
        var plain = Add(s, "training-crane-samurai", 0, Zone.Province, province: 1); s.Players[0].Provinces[1].CardId = plain.Id;
        var second = Add(s, Mukudori, 0, Zone.Province, province: 2); s.Players[0].Provinces[2].CardId = second.Id;
        var e = MatchEngine.FromTestState(s);
        Eq(e.QuotePurchase(0, fudo.Id).Cost, 3, "without Mukudori the Fudo Personality costs 3");
        Add(s, Mukudori, 0, Zone.Home);
        var withMukudori = MatchEngine.FromTestState(s);
        Eq(withMukudori.QuotePurchase(0, fudo.Id).Cost, 2, "with Mukudori in play a Fudo card enters for 1 less");
        Eq(withMukudori.QuotePurchase(0, plain.Id).Cost, 5, "a card without Fudo pays its price");
        Eq(withMukudori.QuotePurchase(0, second.Id).Cost, 6, "'other': a second Mukudori is another Fudo card, 1 less (the recruit itself is refused as Unique)");

        // The Limited: three of his controller's unbowed Fudo cards, himself included, are bowed and a card is drawn; with only two it is not offered.
        var s2 = Table(Phase.Action, 0);
        var mukudori = Add(s2, Mukudori, 0, Zone.Home);
        var f1 = Add(s2, "training-fudo-personality", 0, Zone.Home);
        var f2 = Add(s2, "training-fudo-personality", 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "mukudori-bow-fudo-draw", mukudori.Id), "two unbowed Fudo cards (Mukudori and one more): not offered");
        var f3 = Add(s2, "training-fudo-personality", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s2);
        var handBefore = e3.State.Players[0].Hand.Count;
        var command = Enabled(e3, 0, "mukudori-bow-fudo-draw", mukudori.Id).Command!;
        Accept(e3, command with { Selections = [Pick("fudo", mukudori.Id, f1.Id, f3.Id)] });
        Check(e3.State.Cards[mukudori.Id].Bowed && e3.State.Cards[f1.Id].Bowed && e3.State.Cards[f3.Id].Bowed, "the three chosen Fudo cards are bowed");
        Eq(e3.State.Players[0].Hand.Count, handBefore + 1, "a card was drawn");
        // A bowed Fudo card, or two cards only, is refused.
        var e4 = MatchEngine.FromTestState(s2);
        Reject(e4, Use(e4, 0, "mukudori-bow-fudo-draw", mukudori.Id, null, null, Pick("fudo", mukudori.Id, f1.Id, f2.Id)));
        Reject(e4, Use(e4, 0, "mukudori-bow-fudo-draw", mukudori.Id, null, null, Pick("fudo", f1.Id, f3.Id)));
    }

    private static void NobukatsuBowAnywhere()
    {
        var s = Table(Phase.Battle, 1);
        var nobukatsu = Add(s, Nobukatsu, 1, Zone.Battlefield, province: 0);
        var atBattle = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var atHome = Add(s, "training-crane-samurai", 0, Zone.Home);
        var ownAtHome = Add(s, "training-lion-samurai", 1, Zone.Home);
        var resolved = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 2); // province 2 is not unresolved this Battle phase
        var e = MatchEngine.FromTestState(s);
        var offered = Options(e, 1, "nobukatsu-bow", nobukatsu.Id).Single(a => a.Enabled).TargetIds!;
        Check(offered.Contains(atBattle.Id) && offered.Contains(atHome.Id) && offered.Contains(ownAtHome.Id) && !offered.Contains(resolved.Id), "at any location: the battlefield and either home, not a resolved battlefield: " + string.Join(",", offered));
        Accept(e, Offer(e, 1, "nobukatsu-bow", nobukatsu.Id, atHome.Id));
        Check(e.State.Cards[atHome.Id].Bowed, "the Personality at home is bowed");
        Reject(e, Use(e, 1, "nobukatsu-bow", nobukatsu.Id, resolved.Id));
    }

    private static void MahatsuNegateAndSearch()
    {
        // Player 1 (active) bows his own Personality with an Open action printed on a Water Shugenja: the action carries Water, so Mahatsu (player 0's, at home in the Action phase) may negate it.
        var s = Table(Phase.Action, 1);
        var mahatsu = Add(s, Mahatsu, 0, Zone.Home);
        var shugenja = Add(s, "training-water-shugenja", 1, Zone.Home); GiveAbility(shugenja, "training-bow-any");
        var victim = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-any", shugenja.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window opened for Mahatsu");
        Accept(e, ReactionOffer(e, 0, "mahatsu-negate", mahatsu.Id));
        ClosePassing(e);
        Check(!e.State.Cards[victim.Id].Bowed, "the Water action's effects were negated");
        Check(e.State.Cards[mahatsu.Id].AbilityUses.ContainsKey("mahatsu-negate"), "his Reaction is spent for the turn");

        // A plain action (no Thunder, no element) opens nothing for him.
        var s2 = Table(Phase.Action, 1);
        var mahatsu2 = Add(s2, Mahatsu, 0, Zone.Home);
        var plain = Add(s2, "training-bow-any-source", 1, Zone.Home);
        var victim2 = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-any", plain.Id, victim2.Id));
        Check(e2.State.PendingChoice is not { Type: "beforeActionReaction" } && e2.State.Cards[victim2.Id].Bowed, "no window, the plain action resolved");

        // In the Battle phase he must be at the current battlefield: at home, an element Battle action opens nothing; at the battlefield it does.
        var s3 = Table(Phase.Battle, 1);
        var mahatsu3 = Add(s3, Mahatsu, 0, Zone.Home);
        var defender = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var fireBower = Add(s3, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveKeyword(fireBower, "Fire");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", fireBower.Id, defender.Id));
        Check(!ReactionOffered(e3, 0, "mahatsu-negate"), "Mahatsu at home is not offered in a battle");
        ClosePassing(e3);
        var s4 = Table(Phase.Battle, 1);
        var mahatsu4 = Add(s4, Mahatsu, 0, Zone.Battlefield, province: 0);
        var fireBower4 = Add(s4, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveKeyword(fireBower4, "Fire");
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "training-bow-at-battle", fireBower4.Id, mahatsu4.Id));
        Accept(e4, ReactionOffer(e4, 0, "mahatsu-negate", mahatsu4.Id));
        ClosePassing(e4);
        Check(!e4.State.Cards[mahatsu4.Id].Bowed, "at the battlefield he negates the Fire action against himself");

        // The search: only a card with Thunder or an element keyword qualifies.
        var s5 = Table(Phase.Action, 0);
        foreach (var p in s5.Players) { foreach (var c in s5.Cards.Values.Where(c => c.Zone is Zone.FateDeck && c.OwnerId == p.Id).ToArray()) s5.Cards.Remove(c.Id); p.FateDeck.Clear(); }
        var mahatsu5 = Add(s5, Mahatsu, 0, Zone.Home);
        var weapon = Add(s5, "training-weapon", 0, Zone.FateDeck); s5.Players[0].FateDeck.Add(weapon.Id);
        var fire = Add(s5, "training-false-ring-of-air", 0, Zone.FateDeck); s5.Players[0].FateDeck.Add(fire.Id); // a Fate card printed with Air
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Enabled(e5, 0, "mahatsu-search", mahatsu5.Id).Command!);
        // His own search is an action with his element keywords: the literal text offers him his own Reaction to it (Reading 7), declined here.
        Eq(e5.State.PendingChoice?.Type, "beforeActionReaction"); Check(ReactionOffered(e5, 0, "mahatsu-negate"), "offered his own Reaction to his own element action");
        ClosePassing(e5);
        Eq(e5.State.PendingChoice?.Type, "searchSelect");
        Reject(e5, new GameCommand("selectSearchCard", 0, e5.Version, Choice: weapon.DefinitionId));
        Accept(e5, new GameCommand("selectSearchCard", 0, e5.Version, Choice: fire.DefinitionId));
        Check(e5.State.Players[0].Hand.Contains(fire.Id), "the Air card is in hand");
    }

    private static void YamatoTransferAndActionPhaseAdditionalAction()
    {
        // Entry: 1 Honor, and Brash for good against a Scorpion (the Lion here is not one: Brash stays).
        var s = Table(Phase.Dynasty, 0);
        var yamato = Add(s, Yamato, 0, Zone.Province, province: 0); s.Players[0].Provinces[0].CardId = yamato.Id;
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: yamato.Id, SourceIds: e.QuotePurchase(0, yamato.Id).Payments.First().SourceIds));
        Eq(Honor(e, 0), honor - 1, "lost 1 Honor on entry");
        Check(e.HasKeyword(yamato.Id, "Brash"), "the other player is Lion, not Scorpion: Brash stays");
        var scorpion = Table(Phase.Dynasty, 0); scorpion.Players[1].Clan = "Scorpion";
        var yamato2 = Add(scorpion, Yamato, 0, Zone.Province, province: 0); scorpion.Players[0].Provinces[0].CardId = yamato2.Id;
        var e2 = MatchEngine.FromTestState(scorpion);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: yamato2.Id, SourceIds: e2.QuotePurchase(0, yamato2.Id).Payments.First().SourceIds));
        Check(!e2.HasKeyword(yamato2.Id, "Brash") && e2.State.Cards[yamato2.Id].RemovedKeywords.Contains("Brash"), "against a Scorpion, Brash is permanently removed");

        // The Open half in the Action phase: his Follower on another Personality transfers to him (a Follower needing 1 Personal Honor cannot: Yamato has 0), then an additional action.
        var s3 = Table(Phase.Action, 0);
        var yamato3 = Add(s3, Yamato, 0, Zone.Home);
        var host = Add(s3, "training-lion-samurai", 0, Zone.Home);
        var cavalry = Add(s3, "training-cavalry-follower", 0, Zone.Attached, parent: host.Id);
        var honorable = Add(s3, "training-follower", 0, Zone.Attached, parent: host.Id);
        var enemyHost = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var enemyFollower = Add(s3, "training-cavalry-follower", 1, Zone.Attached, parent: enemyHost.Id);
        var bower = Add(s3, "training-bow-any-source", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        var targets = Options(e3, 0, "yamato-transfer", yamato3.Id).Single(a => a.Enabled).TargetIds!;
        Check(targets.SequenceEqual([cavalry.Id]), "only the Follower that could attach to him: not the one needing Personal Honor 1, not the opponent's: " + string.Join(",", targets));
        Reject(e3, Use(e3, 0, "yamato-transfer", yamato3.Id, honorable.Id));
        Reject(e3, Use(e3, 0, "yamato-transfer", yamato3.Id, enemyFollower.Id));
        Accept(e3, Offer(e3, 0, "yamato-transfer", yamato3.Id, cavalry.Id));
        Eq(e3.State.Cards[cavalry.Id].AttachedTo, yamato3.Id, "the Follower is now attached to Yamato");
        Eq(e3.State.Cards[cavalry.Id].Zone, Zone.Attached, "still in play, never re-paid");
        Eq(e3.State.AdditionalActions.Count, 1, "an additional action was granted");
        Eq(e3.State.PriorityPlayerId, 0, "and its holder keeps priority in the Action phase");
        // The next Open action is the additional one: it consumes the grant and then priority passes.
        Accept(e3, Offer(e3, 0, "training-bow-any", bower.Id, enemyHost.Id));
        Check(e3.State.Cards[enemyHost.Id].Bowed, "the additional Open action resolved");
        Eq(e3.State.AdditionalActions.Count, 0, "the grant is spent");
        Eq(e3.State.PriorityPlayerId, 1, "priority passed on after the additional action");
        // Passing instead forfeits the grant.
        var e4 = MatchEngine.FromTestState(s3);
        Accept(e4, Offer(e4, 0, "yamato-transfer", yamato3.Id, cavalry.Id));
        Accept(e4, new GameCommand("pass", 0, e4.Version));
        Eq(e4.State.AdditionalActions.Count, 0, "a pass forfeits the additional action");
    }

    private static void SawaiTransfer()
    {
        var s = Table(Phase.Battle, 1);
        var sawai = Add(s, Sawai, 1, Zone.Battlefield, province: 0);
        var from = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var to = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: from.Id);
        var ownAlly = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var slots = Enabled(e, 1, "sawai-transfer", sawai.Id).Slots!;
        Check(slots.Single(x => x.Slot == "from").CardIds.SequenceEqual([from.Id]), "the Personality with a Follower");
        Check(slots.Single(x => x.Slot == "to").CardIds.SequenceEqual([to.Id]), "the same player's other Personality, judged against the default first choice");
        Check(slots.Single(x => x.Slot == "follower").CardIds.SequenceEqual([follower.Id]), "his Follower");
        Reject(e, Use(e, 1, "sawai-transfer", sawai.Id, null, null, Pick("from", from.Id), Pick("to", ownAlly.Id), Pick("follower", follower.Id)));
        Accept(e, Use(e, 1, "sawai-transfer", sawai.Id, null, null, Pick("from", from.Id), Pick("to", to.Id), Pick("follower", follower.Id)));
        Eq(e.State.Cards[follower.Id].AttachedTo, to.Id, "the Follower moved to the other Personality");
        Eq(e.State.AdditionalActions.Count, 0, "no Reconnaissance: no additional action");
        // With Reconnaissance at the battlefield the additional action follows.
        var recon = Table(Phase.Battle, 1); recon.Players[0].Provinces[0].Reconnaissance[1] = true;
        var sawai2 = Add(recon, Sawai, 1, Zone.Battlefield, province: 0);
        var from2 = Add(recon, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var to2 = Add(recon, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower2 = Add(recon, "training-follower", 0, Zone.Attached, province: 0, parent: from2.Id);
        var e2 = MatchEngine.FromTestState(recon);
        Accept(e2, Use(e2, 1, "sawai-transfer", sawai2.Id, null, null, Pick("from", from2.Id), Pick("to", to2.Id), Pick("follower", follower2.Id)));
        Eq(e2.State.AdditionalActions.Count(g => g.PlayerId == 1), 1, "with Reconnaissance an additional action is granted");
    }

    private static void KatsuoStraighten()
    {
        var s = Table(Phase.Action, 0);
        var katsuo = Add(s, Katsuo, 0, Zone.Home);
        var own = Add(s, "training-cavalry-follower", 0, Zone.Attached, bowed: true, parent: katsuo.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Home);
        var enemy = Add(s, "training-cavalry-follower", 1, Zone.Attached, bowed: true, parent: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "katsuo-straighten", katsuo.Id).Single(a => a.Enabled).TargetIds!.SequenceEqual([own.Id]), "his own Follower only");
        Reject(e, Use(e, 0, "katsuo-straighten", katsuo.Id, enemy.Id));
        Accept(e, Offer(e, 0, "katsuo-straighten", katsuo.Id, own.Id));
        Check(!e.State.Cards[own.Id].Bowed, "the Follower is straightened");
        Eq(e.State.AdditionalActions.Count, 1, "an additional action was granted");
        Eq(e.State.PriorityPlayerId, 0, "the holder keeps priority");
    }

    private static void CavalryDetachmentSpawn()
    {
        var s = Table(Phase.Battle, 1);
        var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var detachment = Add(s, Detachment, 0, Zone.Attached, province: 0, parent: host.Id);
        var other = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: host.Id);
        var actor = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", actor.Id, host.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opened for the Personality's controller");
        Accept(e, ReactionOffer(e, 0, "cavalry-detachment-spawn", detachment.Id));
        Eq(e.State.Cards[detachment.Id].Zone, Zone.RemovedFromGame, "the Detachment is removed from the game");
        var created = e.State.Cards.Values.Single(c => c.DefinitionId == Detached);
        Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0 && created.OwnerId == 0, "the Personality is created at the target's location, the battlefield, for the target's controller");
        Check(e.HasKeyword(created.Id, "Crane Clan") && e.HasKeyword(created.Id, "Samurai") && e.HasKeyword(created.Id, "Cavalry"), "with the creator's Clan alignment");
        Check(e.State.Cards[other.Id].AttachedTo == created.Id && e.State.Cards[weapon.Id].AttachedTo == created.Id, "the target's attachments were transferred to it");
        ClosePassing(e);
        Check(e.State.Cards[host.Id].Bowed && !e.State.Cards[created.Id].Bowed, "the held action resolved on the bare target; the created Personality is untouched");
        // Its own controller's action, or an action targeting another Personality, opens nothing for it.
        var s2 = Table(Phase.Battle, 1);
        var host2 = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var detachment2 = Add(s2, Detachment, 0, Zone.Attached, province: 0, parent: host2.Id);
        var ally2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var actor2 = Add(s2, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", actor2.Id, ally2.Id));
        Check(!ReactionOffered(e2, 0, "cavalry-detachment-spawn"), "an action targeting another Personality is not its trigger");
        ClosePassing(e2);
    }

    private static void TempleToFudo()
    {
        // Entry: 3 Honor.
        var s = Table(Phase.Dynasty, 0);
        var temple = Add(s, Temple, 0, Zone.Province, province: 0); s.Players[0].Provinces[0].CardId = temple.Id;
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: temple.Id, SourceIds: e.QuotePurchase(0, temple.Id).Payments.First().SourceIds));
        Eq(Honor(e, 0), honor - 3, "lost 3 Honor on entry");
        Check(e.State.Cards[temple.Id].Zone == Zone.Home && e.State.Cards[temple.Id].Bowed, "a Holding enters play bowed");

        // Producing: bowing it for Gold loses 1 Honor.
        var s2 = Table(Phase.Dynasty, 0);
        var temple2 = Add(s2, Temple, 0, Zone.Home);
        var cheap = Add(s2, "training-bow-any-source", 0, Zone.Province, province: 0); s2.Players[0].Provinces[0].CardId = cheap.Id; // costs 1
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: cheap.Id, SourceIds: [temple2.Id]));
        Eq(Honor(e2, 0), honor2 - 1, "lost 1 Honor for the Gold it produced");
        Check(e2.State.Cards[temple2.Id].Bowed, "and it is bowed");

        // The hold: bowed at the end of its controller's first turn, it stays bowed through the second turn and straightens in the third.
        var s3 = Table(Phase.Action, 0); s3.Players[0].TurnsTaken = 1; s3.Players[1].TurnsTaken = 0;
        var temple3 = Add(s3, Temple, 0, Zone.Home, bowed: true);
        var control = Add(s3, "training-farm", 0, Zone.Home, bowed: true);
        var e3 = MatchEngine.FromTestState(s3);
        RunUntil(e3, () => e3.State.ActivePlayerId == 0 && e3.State.Phase == Phase.Action && e3.State.Players[0].TurnsTaken == 2);
        Check(e3.State.Cards[temple3.Id].Bowed && !e3.State.Cards[control.Id].Bowed, "second turn: the Temple is still bowed while the Farm straightened");
        RunUntil(e3, () => e3.State.ActivePlayerId == 0 && e3.State.Phase == Phase.Action && e3.State.Players[0].TurnsTaken == 3);
        Check(!e3.State.Cards[temple3.Id].Bowed, "third turn: it straightened");
    }

    private static void HachiAndPort()
    {
        var s = Table(Phase.Action, 0);
        var hachi = Add(s, Hachi, 0, Zone.Home);
        var lionHachi = Add(s, Hachi, 1, Zone.Home);
        var farm = Add(s, "training-farm", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(hachi.Id, 0), 3, "3 Gold for a Crane player");
        Eq(e.GoldProduced(lionHachi.Id, 1), 2, "2 Gold for another");
        var targets = Options(e, 0, "hachis-revenge-bow", hachi.Id).Single(a => a.Enabled).TargetIds!;
        Check(targets.Contains(farm.Id) && !targets.Contains(lionHachi.Id) && !targets.Contains(hachi.Id), "non-Unique Holdings only, either player's: " + string.Join(",", targets));
        Reject(e, Use(e, 0, "hachis-revenge-bow", hachi.Id, lionHachi.Id));
        Accept(e, Offer(e, 0, "hachis-revenge-bow", hachi.Id, farm.Id));
        Check(e.State.Cards[farm.Id].Bowed && !e.State.Cards[hachi.Id].Bowed, "the Farm is bowed; Hachi's Revenge itself is not (the Open names no bow cost)");

        var s2 = Table(Phase.Action, 0);
        var port = Add(s2, Port, 0, Zone.Home);
        var market = Add(s2, "training-market-holding", 1, Zone.Home, bowed: true);
        var otherPort = Add(s2, Port, 1, Zone.Home, bowed: true);
        var farm2 = Add(s2, "training-farm", 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        var portTargets = Options(e2, 0, "second-city-port-straighten", port.Id).Single(a => a.Enabled).TargetIds!;
        Check(portTargets.Contains(market.Id) && portTargets.Contains(otherPort.Id) && !portTargets.Contains(farm2.Id), "a Market or Port, either player's, never a Farm: " + string.Join(",", portTargets));
        Reject(e2, Use(e2, 0, "second-city-port-straighten", port.Id, farm2.Id));
        Accept(e2, Offer(e2, 0, "second-city-port-straighten", port.Id, market.Id));
        Check(!e2.State.Cards[market.Id].Bowed && e2.State.Cards[port.Id].Bowed, "the Market straightened and the Port bowed as the cost");
    }

    private static void IncenseMillAndPalace()
    {
        var s = Table(Phase.Action, 0);
        var mill = Add(s, IncenseMill, 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        target.Modifiers.Add(new("x", "x", "Force", -2)); target.Modifiers.Add(new("x", "x", "Force", 1)); target.Modifiers.Add(new("x", "x", "Chi", -1));
        target.Tokens.Add(new("t1", "Penalty", "x", "Force", -1)); target.Tokens.Add(new("t2", "Bonus", "x", "Force", 1));
        target.ActionLocks.Add(new("x", "Turn"));
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(target.Id);
        Accept(e, Offer(e, 0, "incense-mill-cleanse", mill.Id, target.Id));
        var after = e.State.Cards[target.Id];
        Check(after.Modifiers.Count(m => m.Stat == "Force") == 1 && after.Modifiers.Single(m => m.Stat == "Force").Amount == 1, "the Force penalty is gone, the Force bonus stays");
        Check(after.Modifiers.Any(m => m.Stat == "Chi" && m.Amount == -1), "a Chi penalty is not a Force penalty");
        Check(after.Tokens.Select(t => t.Id).SequenceEqual(["t2"]), "the -1F token is gone, the +1F token stays");
        Eq(after.ActionLocks.Count, 0, "the action lock is lifted");
        Eq(e.CardForceForTest(target.Id), before + 3, "his Force is up by the penalties removed");

        var s2 = Table(Phase.Action, 1);
        var palace = Add(s2, Palace, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 1, "overlooked-palace-create", palace.Id).Command!);
        Eq(e2.State.Cards[palace.Id].Zone, Zone.DynastyDiscard, "the Palace was destroyed as the cost");
        var ninja = e2.State.Cards.Values.Single(c => c.DefinitionId == NinjaProxy);
        Check(ninja.Zone == Zone.Home && ninja.OwnerId == 1 && e2.HasKeyword(ninja.Id, "Ninja") && e2.HasKeyword(ninja.Id, "Lion Clan") && !e2.HasKeyword(ninja.Id, "Scorpion Clan"), "a Ninja Personality with the creator's (Lion) Clan alignment at home");
        Eq(e2.PersonalityChiForTest(ninja.Id), 2, "0F/2C/0PH");
        // A bowed Palace cannot use it.
        var s3 = Table(Phase.Action, 1);
        var bowedPalace = Add(s3, Palace, 1, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s3), 1, "overlooked-palace-create", bowedPalace.Id), "not from a bowed Holding");
    }

    private static void TestedLandFlow()
    {
        // Player 1 reveals Tested Land at the start of his turn; player 0's provinces 0..3 have no Region, so he chooses; it attaches to the chosen one under player 0's control.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var land = Add(s, TestedLand, 1, Zone.Province, province: 0); land.FaceUp = false; s.Players[1].Provinces[0].CardId = land.Id;
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice?.Type == "testedLandProvince");
        Eq(e.State.PendingChoice!.PlayerId, 1, "its owner chooses");
        var offers = e.QueryActions(1).Where(a => a.Type == "testedLandProvince" && a.Enabled).ToArray();
        Eq(offers.Length, 4, "one option per enemy province without a Region");
        Check(e.QueryActions(0).All(a => a.Type != "testedLandProvince"), "the other player is offered nothing");
        Reject(e, new GameCommand("testedLandProvince", 0, e.Version, Province: 2));
        Accept(e, new GameCommand("testedLandProvince", 1, e.Version, Province: 2));
        var attached = e.State.Cards[land.Id];
        Check(attached.Zone == Zone.Region && attached.ProvinceIndex == 2 && e.State.Players[0].Provinces[2].RegionId == land.Id, "attached to the chosen enemy province");
        Check(attached.OwnerId == 0 && attached.OriginalOwnerId == 1, "controlled by that player, still owned by its owner");
        Check(e.State.Players[1].Provinces[0].CardId != land.Id && e.State.Players[1].Provinces[0].CardId is not null, "its own province was refilled");
        Eq(e.State.Phase, Phase.Action, "the reveal went on to the Action phase");
        // With exactly one eligible enemy province there is no choice.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        foreach (var index in new[] { 0, 1, 2 }) { var region = Add(s2, "training-costed-region", 0, Zone.Region, province: index); s2.Players[0].Provinces[index].RegionId = region.Id; }
        var land2 = Add(s2, TestedLand, 1, Zone.Province, province: 0); land2.FaceUp = false; s2.Players[1].Provinces[0].CardId = land2.Id;
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.ActivePlayerId == 1 && e2.State.Phase == Phase.Action);
        Check(e2.State.Cards[land2.Id].Zone == Zone.Region && e2.State.Players[0].Provinces[3].RegionId == land2.Id, "attached to the only province that can take it");

        // A Holding revealed in that province at the start of player 0's turn is discarded, and so is Tested Land -- back to player 1's discard pile.
        var holdingId = "";
        var e3 = Edit(e, st => { var h = Add(st, "training-farm", 0, Zone.Province, province: 2); h.FaceUp = false; st.Players[0].Provinces[2].CardId = h.Id; holdingId = h.Id; });
        RunUntil(e3, () => e3.State.ActivePlayerId == 0 && e3.State.Phase == Phase.Action);
        Check(e3.State.Cards[holdingId].Zone == Zone.DynastyDiscard, "the revealed Holding was discarded");
        var landAfter = e3.State.Cards[land.Id];
        Check(landAfter.Zone == Zone.DynastyDiscard && landAfter.OwnerId == 1 && landAfter.OriginalOwnerId is null && e3.State.Players[0].Provinces[2].RegionId is null, "Tested Land is discarded to its owner's pile and the province has no Region");
        Check(e3.State.Players[0].Provinces[2].CardId is not null && e3.State.Players[0].Provinces[2].CardId != holdingId, "the province was refilled");
    }

    private static void OfferingReverenceFlow()
    {
        // Two dead Personalities: two Ancestors with the Lion player's Clan.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var reverence = Add(s, Reverence, 1, Zone.Province, province: 0); reverence.FaceUp = false; s.Players[1].Provinces[0].CardId = reverence.Id;
        Add(s, "training-lion-samurai", 1, Zone.Dead); Add(s, "training-crane-samurai", 1, Zone.Dead);
        Add(s, "training-lion-samurai", 0, Zone.Dead);          // the opponent's dead do not count
        Add(s, "training-lion-samurai", 1, Zone.DynastyDiscard); // discarded, not dead
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        var ancestors = e.State.Cards.Values.Where(c => c.DefinitionId == "oracle-l5r-9785" && c.OwnerId == 1).ToArray();
        Eq(ancestors.Length, 2, "one Ancestor per dead Personality of the owner's");
        foreach (var ancestor in ancestors)
        {
            Check(ancestor.Zone == Zone.Home && !ancestor.Bowed, "created at home");
            Check(e.HasKeyword(ancestor.Id, "Ancestor") && e.HasKeyword(ancestor.Id, "Samurai") && e.HasKeyword(ancestor.Id, "Spirit") && e.HasKeyword(ancestor.Id, "Lion Clan"), "Samurai Ancestor Spirit with the Lion Clan alignment");
            Eq(e.CardForceForTest(ancestor.Id), 2, "2F"); Eq(e.PersonalityChiForTest(ancestor.Id), 2, "2C");
        }
        Eq(e.State.Cards[reverence.Id].Zone, Zone.DynastyDiscard, "the Event is discarded normally");
        // No dead Personality: the owner may shuffle it into the deck instead.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var reverence2 = Add(s2, Reverence, 1, Zone.Province, province: 0); reverence2.FaceUp = false; s2.Players[1].Provinces[0].CardId = reverence2.Id;
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.PendingChoice?.Type == "offeringReverence");
        Eq(e2.QueryActions(1).Count(a => a.Type == "offeringReverence" && a.Enabled), 2, "shuffle or discard");
        Reject(e2, new GameCommand("offeringReverence", 0, e2.Version, Choice: "shuffle"));
        var deckBefore = e2.State.Players[1].DynastyDeck.Count;
        Accept(e2, new GameCommand("offeringReverence", 1, e2.Version, Choice: "shuffle"));
        Check(e2.State.Cards[reverence2.Id].Zone == Zone.DynastyDeck && e2.State.Players[1].DynastyDeck.Contains(reverence2.Id), "shuffled into the Dynasty deck");
        Check(e2.State.Players[1].Provinces[0].CardId is not null && e2.State.Players[1].Provinces[0].CardId != reverence2.Id, "the province was refilled");
        Eq(e2.State.Phase, Phase.Action, "the reveal went on");
        Check(!e2.State.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-9785"), "no Ancestor was created");
        var e3 = MatchEngine.FromTestState(s2);
        RunUntil(e3, () => e3.State.PendingChoice?.Type == "offeringReverence");
        Accept(e3, new GameCommand("offeringReverence", 1, e3.Version, Choice: "discard"));
        Eq(e3.State.Cards[reverence2.Id].Zone, Zone.DynastyDiscard, "or discarded");
    }
}
