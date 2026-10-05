using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Promotional-Celestial group C (holding-wave-9301, region-wave-9302, follower-wave-9303, personality-wave-9304): eight cards wired from the printed text of their scans.
internal static class PromoCelestialCTests
{
    private const string Tanuki = "oracle-l5r-1081", Deadlands = "oracle-l5r-1879", FudoCultist = "oracle-l5r-2722", Officer = "oracle-l5r-6218", Treasury = "oracle-l5r-8150",
        IvoryCoast = "oracle-l5r-8161", WarRavaged = "oracle-l5r-9224", YotsuChosen = "oracle-l5r-9681", FreshRecruit = "oracle-l5r-9997";
    private const string HirumaSawai = "oracle-l5r-10042"; // a Fudo Personality

    public static void Run(TestRunner runner) => runner.Run("promo-celestial-c", [
        ("Published shapes: the eight cards print their scan's text, stats, keywords, abilities and Effect, ship art, and the four waves pin exactly them; Fresh Recruit is a created-only Personality", PublishedShapes),
        ("Blue Tanuki Bar: bow to give a target Personality a +1F Sake token; a non-Personality target is refused with nothing changed; its Gold production is 1", BlueTanukiBar),
        ("Deadlands: entering play costs 4 Honor; its Limited discards it and the province's card and refills the province face-up with an Undead discarded Personality; illegal uses change nothing", DeadlandsRefill),
        ("The Ivory Coast: bow the Region to move your performing Personality home and straighten his unit; only a Personality at the battle, and a bowed Region offers nothing", IvoryCoastHome),
        ("War-ravaged Plain: discard it to destroy another target Region of either player; it may not target itself; with no other Region it is not offered", WarRavagedPlain),
        ("Recruitment Officer: after engaging where you control a unit, bow it to create a 2F/2C/2PH Ronin Samurai there, removed when the turn ends; refused with no unit of yours; it produces 3 Gold", RecruitmentOfficer),
        ("The Imperial Treasury: produces 1 plus 1 per non-Unique Holding you control (not itself or other Unique Holdings); costs 1 less for a Mantis Clan player only", ImperialTreasury),
        ("Fudo Cultist: free to attach to a Nonhuman Fudo Personality, full price to any other; +1F for each other Fudo card you control (not its own, not an enemy's)", FudoCultist_),
        ("Yotsu Ueda's Chosen: full price to any other wielder; bow or straighten a target attachment of either player, once as one action", YotsuChosen_),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The engine's own offered command for an ability that names no target (the Officer's Reaction).
    private static GameCommand OfferNoTarget(MatchEngine e, int player, string ability, string cardId)
    {
        var option = Options(e, player, ability, cardId).FirstOrDefault(a => a.Enabled);
        Check(option is not null, $"{ability} on {cardId} was not offered to player {player}: " + string.Join("; ", Options(e, player, ability, cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!.Command!;
    }

    private static MatchState Action(int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Action; s.ActivePlayerId = actor; s.PriorityPlayerId = actor; s.TurnNumber = 3;
        return s;
    }

    private static CardInstance AttachRegion(MatchState s, string definition, int owner, int province)
    {
        var region = Add(s, definition, owner, Zone.Region, province: province);
        s.Players[owner].Provinces[province].RegionId = region.Id;
        return region;
    }

    private static void DeclineWindows(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // ---- shapes -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShapes()
    {
        var expected = new (string Id, string Name, CardType Type, int Cost, int Production, string[] Keywords, string Effect, string[] Abilities)[]
        {
            (Tanuki, "Blue Tanuki Bar", CardType.Holding, 1, 1, ["Sake Works"], "none", ["pcc-tanuki-sake"]),
            (Officer, "Recruitment Officer", CardType.Holding, 4, 3, ["Retainer"], "none", ["pcc-recruitment-officer"]),
            (Treasury, "The Imperial Treasury", CardType.Holding, 4, 1, ["Imperial", "Unique"], "mantis-clan-discount", []),
            (Deadlands, "Deadlands", CardType.Region, 0, 0, ["Shadowlands"], "none", ["pcc-deadlands-refill"]),
            (IvoryCoast, "The Ivory Coast", CardType.Region, 0, 0, ["Singular"], "none", ["pcc-ivory-coast-home"]),
            (WarRavaged, "War-ravaged Plain", CardType.Region, 0, 0, [], "none", ["pcc-war-ravaged-destroy"]),
            (FudoCultist, "Fudo Cultist", CardType.Follower, 3, 0, ["Fudo"], "pcc-fudo-cultist", []),
            (YotsuChosen, "Yotsu Ueda's Chosen", CardType.Follower, 4, 0, [], "pcc-yotsu-chosen", ["pcc-yotsu-bow", "pcc-yotsu-straighten"]),
            (FreshRecruit, "Fresh Recruit", CardType.Personality, 0, 0, ["Ronin", "Samurai"], "none", []),
        };
        var root = RepoRoot();
        foreach (var (id, name, type, cost, production, keywords, effect, abilities) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == type && d.GoldCost == cost && d.GoldProduction == production, $"{id}: {name} {d.Type} {d.GoldCost}/{d.GoldProduction}");
            Check((d.Keywords ?? []).SequenceEqual(keywords), $"{id}: printed keywords {string.Join(",", d.Keywords ?? [])}");
            Eq(d.Effect, effect, $"{id}: effect"); Check((d.Abilities ?? []).SequenceEqual(abilities), $"{id}: abilities");
            Check(!d.ScenarioOnly && d.CreatedOnly == (id == FreshRecruit), $"{id}: playable/created-only flags");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(d.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(CardCatalog.Get(Deadlands).EntryHonorLoss, 4, "Deadlands' entry Honor loss");
        var fresh = CardCatalog.Get(FreshRecruit);
        Check(fresh.Force == 2 && fresh.Chi == 2 && fresh.PersonalHonor == 2, "Fresh Recruit is 2F/2C/2PH");
        Eq(CardCatalog.Get(Treasury).Text, "Enters play paying 1 less Gold if you are a Mantis Clan player.\nBow this card: Produce 1 Gold, plus 1 additional Gold for each non-Unique Holding you control.");
        Eq(CardCatalog.Get(WarRavaged).Text, "Limited: Discard this card and target a Region: Destroy it.");
        Eq(CardCatalog.Get(FudoCultist).Text, "Attaches to a Nonhuman Fudo Personality ignoring Gold cost.\nThis card has +1F for each other Fudo card you control.");
        Eq(CardCatalog.Get(YotsuChosen).Text, "Attaches to Yotsu Ueda paying 2 less Gold.\nBattle: Target an attachment: Bow or straighten it.");
        // The creating ability finds Fresh Recruit's scan.
        Eq(CreatedCards.Resolve(AbilityCatalog.All["pcc-recruitment-officer"].Creates!).Id, FreshRecruit);
        Check(CreatedCards.Verify().Length == 0, string.Join("; ", CreatedCards.Verify()));
        // The manifests pin exactly these cards, and the sources name the reviewed printings.
        var waves = new Dictionary<string, string[]>
        {
            ["holding-wave-9301"] = [Tanuki, Officer, Treasury],
            ["region-wave-9302"] = [Deadlands, IvoryCoast, WarRavaged],
            ["follower-wave-9303"] = [FudoCultist, YotsuChosen],
            ["personality-wave-9304"] = [FreshRecruit],
        };
        foreach (var (wave, ids) in waves)
        {
            var manifest = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(new HashSet<string>(manifest).SetEquals(ids) && manifest.Length == ids.Length, $"{wave} manifest");
            var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("card_id").GetString()!);
            foreach (var id in ids)
            {
                var set = sources[id].GetProperty("printing_set")[0].GetString();
                Eq(set, id == FreshRecruit ? "Embers of War" : "Promotional&ndash;Celestial", $"{id}: reviewed printing");
            }
        }
    }

    // ---- Blue Tanuki Bar ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BlueTanukiBar()
    {
        var s = Action();
        var bar = Add(s, Tanuki, 0, Zone.Home); var own = Add(s, "training-crane-samurai", 0, Zone.Home); var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var market = Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(bar.Id, 0), 1, "it produces 1 Gold");
        var option = Enabled(e, 0, "pcc-tanuki-sake", bar.Id);
        Check(option.TargetIds!.Contains(own.Id) && option.TargetIds.Contains(enemy.Id), "either player's Personality is a target");
        Check(!option.TargetIds.Contains(market.Id), "a Holding is not");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pcc-tanuki-sake", bar.Id, market.Id));
        Check(!e.State.Cards[bar.Id].Bowed && e.State.Cards[market.Id].Tokens.Count == 0 && e.Version == version, "an illegal target changes nothing");
        Accept(e, Offer(e, 0, "pcc-tanuki-sake", bar.Id, enemy.Id));
        Check(e.State.Cards[bar.Id].Bowed, "bowing is the cost");
        var token = e.State.Cards[enemy.Id].Tokens.Single();
        Eq(token.Type, "Sake"); Eq(token.Stat, "Force"); Eq(token.Amount, 1);
        Check(!CanUse(e, 0, "pcc-tanuki-sake", bar.Id), "the bowed bar offers nothing");
    }

    // ---- Deadlands -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DeadlandsRefill()
    {
        // Entering play: the Events Phase reveal attaches it and costs its player 4 Honor.
        var s0 = Action(); s0.Phase = Phase.Events;
        var revealed = Add(s0, Deadlands, 0, Zone.Province, province: 1); s0.Players[0].Provinces[1].CardId = revealed.Id;
        var e0 = MatchEngine.FromTestState(s0);
        var honor0 = Honor(e0, 0);
        e0.ResolveRegionForTest(0, revealed.Id);
        Eq(e0.State.Players[0].Provinces[1].RegionId, revealed.Id, "it attached to its province"); Eq(Honor(e0, 0), honor0 - 4, "and cost 4 Honor");

        MatchState Setup(out CardInstance region, out CardInstance inProvince, out CardInstance undead, bool undeadKeyword = true, bool cardInProvince = true)
        {
            var s = Action();
            region = AttachRegion(s, Deadlands, 0, 1);
            inProvince = Add(s, "training-crane-samurai", 0, Zone.Province, province: 1); inProvince.FaceUp = false;
            if (cardInProvince) s.Players[0].Provinces[1].CardId = inProvince.Id; else { s.Cards.Remove(inProvince.Id); s.Players[0].Provinces[1].CardId = null; }
            undead = Add(s, "training-lion-samurai", 0, Zone.DynastyDiscard);
            if (undeadKeyword) undead.GrantedKeywords.Add(new("Undead", "test-fixture", GrantDuration.Permanent));
            return s;
        }
        var s1 = Setup(out var region1, out var old1, out var undead1);
        var e1 = MatchEngine.FromTestState(s1);
        var honor1 = Honor(e1, 0);
        Accept(e1, Offer(e1, 0, "pcc-deadlands-refill", region1.Id, undead1.Id));
        var province = e1.State.Players[0].Provinces[1];
        Eq(province.CardId, undead1.Id, "the Undead Personality refilled the province"); Check(e1.State.Cards[undead1.Id].FaceUp && e1.State.Cards[undead1.Id].Zone == Zone.Province, "face-up in the province");
        Eq(e1.State.Cards[old1.Id].Zone, Zone.DynastyDiscard, "the province's card was discarded");
        Eq(e1.State.Cards[region1.Id].Zone, Zone.DynastyDiscard, "the Region was discarded as the cost"); Check(province.RegionId is null, "and left its province");
        Eq(Honor(e1, 0), honor1, "no Honor cost for the action");
        // A discarded Personality without Undead is not a target.
        var s2 = Setup(out var region2, out _, out var plain, undeadKeyword: false);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "pcc-deadlands-refill", region2.Id), "no Undead Personality in your discard pile: not offered");
        var version = e2.Version;
        Rejection(e2, Use(e2, 0, "pcc-deadlands-refill", region2.Id, plain.Id));
        Check(e2.State.Cards[region2.Id].Zone == Zone.Region && e2.State.Players[0].Provinces[1].RegionId == region2.Id && e2.Version == version, "a refused use changes nothing");
        // An empty province: "if there are any cards in this province".
        var s3 = Setup(out var region3, out _, out var undead3, cardInProvince: false);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "pcc-deadlands-refill", region3.Id), "no card in the province: not offered");
        Rejection(e3, Use(e3, 0, "pcc-deadlands-refill", region3.Id, undead3.Id));
        Check(e3.State.Cards[region3.Id].Zone == Zone.Region && e3.State.Cards[undead3.Id].Zone == Zone.DynastyDiscard, "and nothing moved");
    }

    // ---- The Ivory Coast -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IvoryCoastHome()
    {
        MatchState Setup(out CardInstance region, out CardInstance mover, out CardInstance unitMate, bool atBattle = true)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            region = AttachRegion(s, IvoryCoast, 0, 1);
            mover = Add(s, "training-crane-samurai", 0, atBattle ? Zone.Battlefield : Zone.Home, bowed: true, province: atBattle ? 0 : null);
            unitMate = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: mover.Id);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var s1 = Setup(out var region1, out var mover1, out var mate1);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Enabled(e1, 0, "pcc-ivory-coast-home", region1.Id, mover1.Id).Command!);
        DeclineWindows(e1);
        Check(e1.State.Cards[mover1.Id].Zone == Zone.Home, "he moved home");
        Check(!e1.State.Cards[mover1.Id].Bowed && !e1.State.Cards[mate1.Id].Bowed, "and his unit straightened");
        Check(e1.State.Cards[region1.Id].Bowed, "the Region bowed as the cost");
        Check(!CanUse(e1, 0, "pcc-ivory-coast-home", region1.Id), "a bowed Region offers nothing");
        // Not at the battle: no performer.
        var s2 = Setup(out var region2, out var home2, out _, atBattle: false);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "pcc-ivory-coast-home", region2.Id, home2.Id), "a Personality at home is not a performer");
    }

    // ---- War-ravaged Plain ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WarRavagedPlain()
    {
        var s = Action();
        var plain = AttachRegion(s, WarRavaged, 0, 0);
        var enemyRegion = AttachRegion(s, IvoryCoast, 1, 1); var ownRegion = AttachRegion(s, Deadlands, 0, 2);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pcc-war-ravaged-destroy", plain.Id);
        Check(option.TargetIds!.Contains(enemyRegion.Id) && option.TargetIds.Contains(ownRegion.Id), "either player's Region is a target");
        Check(!option.TargetIds.Contains(host.Id), "a Personality is not");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pcc-war-ravaged-destroy", plain.Id, plain.Id));
        Check(e.State.Cards[plain.Id].Zone == Zone.Region && e.Version == version, "it may not target itself");
        Rejection(e, Use(e, 0, "pcc-war-ravaged-destroy", plain.Id, host.Id));
        Check(e.State.Cards[plain.Id].Zone == Zone.Region && e.Version == version, "nor a Personality");
        Accept(e, Offer(e, 0, "pcc-war-ravaged-destroy", plain.Id, enemyRegion.Id));
        Eq(e.State.Cards[enemyRegion.Id].Zone, Zone.DynastyDiscard, "the target Region was destroyed"); Check(e.State.Players[1].Provinces[1].RegionId is null, "and left its province");
        Eq(e.State.Cards[plain.Id].Zone, Zone.DynastyDiscard, "the Plain was discarded as the cost"); Check(e.State.Players[0].Provinces[0].RegionId is null, "and left its province");
        Eq(e.State.Cards[ownRegion.Id].Zone, Zone.Region, "another Region is untouched");
        // Alone, there is no target.
        var alone = Action(); var only = AttachRegion(alone, WarRavaged, 0, 0);
        var eAlone = MatchEngine.FromTestState(alone);
        Check(!CanUse(eAlone, 0, "pcc-war-ravaged-destroy", only.Id), "with no other Region it is not offered");
    }

    // ---- Recruitment Officer ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Engage(MatchEngine e, int player)
    {
        Accept(e, e.QueryActions(0).First(a => a.Type == "selectBattle" && a.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        while (e.State.PriorityPlayerId != player)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void RecruitmentOfficer()
    {
        MatchState Setup(bool ownUnit, out CardInstance officer)
        {
            var s = Table(Phase.BattleSelection, 0);
            s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            if (ownUnit) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            officer = Add(s, Officer, 0, Zone.Home);
            return s;
        }
        var s1 = Setup(true, out var officer1);
        var e1 = MatchEngine.FromTestState(s1);
        Eq(e1.GoldProduced(officer1.Id, 0), 3, "it produces 3 Gold");
        Engage(e1, 0);
        Accept(e1, OfferNoTarget(e1, 0, "pcc-recruitment-officer", officer1.Id));
        Check(e1.State.Cards[officer1.Id].Bowed, "bowing is the cost");
        var recruit = e1.State.Cards.Values.Single(c => c.DefinitionId == FreshRecruit);
        Check(recruit.Zone == Zone.Battlefield && recruit.ProvinceIndex == 0 && recruit.OwnerId == 0, "created at the battlefield on its player's side");
        Check(e1.CardForceForTest(recruit.Id) == 2, "2F");
        Check(e1.State.MechD.TimedRemovals.Any(r => r.CardId == recruit.Id && r.When == TimedRemovalWhen.TurnEnds), "removed from the game when this turn ends");
        Check(!CanUse(e1, 0, "pcc-recruitment-officer", officer1.Id), "a bowed Officer offers nothing more");
        // No unit of yours at the battlefield: refused with nothing changed.
        var s2 = Setup(false, out var officer2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "selectBattle" && a.Enabled).Command!);
        Check(!Options(e2, 0, "pcc-recruitment-officer", officer2.Id).Any(o => o.Enabled), "no unit of yours there: not offered");
        Check(e2.State.Cards.Values.All(c => c.DefinitionId != FreshRecruit) && !e2.State.Cards[officer2.Id].Bowed, "nothing was created");
    }

    // ---- The Imperial Treasury -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImperialTreasury()
    {
        var s = Action();
        var treasury = Add(s, Treasury, 0, Zone.Home);
        var farm1 = Add(s, "training-marketplace", 0, Zone.Home); var farm2 = Add(s, "training-marketplace", 0, Zone.Home); Add(s, "training-marketplace", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var nonUnique = s.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding && !(CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Unique"));
        Check(nonUnique >= 2, "fixture has non-Unique Holdings");
        Eq(e.GoldProduced(treasury.Id, 0), 1 + nonUnique, "1 plus 1 per non-Unique Holding you control (it is Unique; the enemy's Holding does not count)");
        // Cost: 1 less for a Mantis Clan player only.
        MatchEngine Buy(string clan)
        {
            var b = Action(); b.Players[0].Clan = clan;
            var card = Add(b, Treasury, 0, Zone.Province, province: 0); card.FaceUp = true; b.Players[0].Provinces[0].CardId = card.Id;
            return MatchEngine.FromTestState(b);
        }
        var mantis = Buy("Mantis"); var crane = Buy("Crane");
        var treasuryId = mantis.State.Cards.Values.Single(c => c.DefinitionId == Treasury).Id;
        Eq(crane.QuotePurchaseForTest(0, crane.State.Cards.Values.Single(c => c.DefinitionId == Treasury).Id), 4, "a Crane player pays the printed 4");
        Eq(mantis.QuotePurchaseForTest(0, treasuryId), 3, "a Mantis Clan player pays 1 less");
    }

    // ---- Fudo Cultist --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int EquipCost(MatchEngine e, CardInstance follower, CardInstance wielder)
    {
        var option = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == follower.Id);
        return option.TargetGold is { } byTarget && byTarget.TryGetValue(wielder.Id, out var quote) ? quote.Cost : option.Gold!.Cost;
    }

    private static void FudoCultist_()
    {
        var s = GoldParityTests.Action();
        for (var i = 0; i < 6; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
        var cultist = GoldParityTests.Add(s, FudoCultist, 0, Zone.Hand);
        var nonhumanFudo = GoldParityTests.Add(s, "training-crane-samurai", 0, Zone.Home);
        nonhumanFudo.GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent)); nonhumanFudo.GrantedKeywords.Add(new("Fudo", "test-fixture", GrantDuration.Permanent));
        var humanFudo = GoldParityTests.Add(s, HirumaSawai, 0, Zone.Home);
        var nonhumanOnly = GoldParityTests.Add(s, "training-lion-samurai", 0, Zone.Home);
        nonhumanOnly.GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        Eq(EquipCost(e, cultist, nonhumanFudo), 0, "ignoring Gold cost on a Nonhuman Fudo Personality");
        Eq(EquipCost(e, cultist, humanFudo), 3, "a human Fudo Personality pays the printed 3");
        Eq(EquipCost(e, cultist, nonhumanOnly), 3, "a Nonhuman who is not Fudo pays the printed 3");
        var option = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == cultist.Id);
        Check(option.TargetGold![nonhumanFudo.Id].Basis.Contains("Nonhuman Fudo"), option.TargetGold[nonhumanFudo.Id].Basis);
        // Attached for free: no Gold is spent.
        var gold = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        var equip = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == cultist.Id);
        Accept(e, equip.Command! with { TargetId = nonhumanFudo.Id, SourceIds = equip.TargetGold![nonhumanFudo.Id].Payments[0].SourceIds });
        Eq(e.State.Cards[cultist.Id].AttachedTo, nonhumanFudo.Id, "it attached");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed), gold, "nothing was bowed to pay");
        // +1F for each other Fudo card you control: the wielder is itself Fudo (+1), Hiruma Sawai (+1); the enemy's Fudo and the Cultist itself do not count.
        var s2 = GoldParityTests.Action();
        var host = GoldParityTests.Add(s2, "training-crane-samurai", 0, Zone.Home);
        var cult = GoldParityTests.Add(s2, FudoCultist, 0, Zone.Attached, attachedTo: host.Id);
        var e2 = MatchEngine.FromTestState(s2);
        var baseForce = e2.CardForceForTest(cult.Id);
        Eq(baseForce, 2, "printed 2F with no other Fudo card");
        GoldParityTests.Add(s2, HirumaSawai, 0, Zone.Home); GoldParityTests.Add(s2, HirumaSawai, 1, Zone.Home);
        var second = GoldParityTests.Add(s2, FudoCultist, 0, Zone.Home); // another Cultist (a Fudo card) in play counts too
        var e3 = MatchEngine.FromTestState(s2);
        Eq(e3.CardForceForTest(cult.Id), 4, "+1F for Hiruma Sawai and +1F for the other Cultist, not for the enemy's Fudo or itself");
    }

    // ---- Yotsu Ueda's Chosen -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void YotsuChosen_()
    {
        // The 2-less price is keyed on the wielder's name, Yotsu Ueda (oracle-l5r-9680), who is not a published card yet: every other wielder pays the printed 4.
        var s = GoldParityTests.Action();
        for (var i = 0; i < 6; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
        var chosen = GoldParityTests.Add(s, YotsuChosen, 0, Zone.Hand);
        var wielder = GoldParityTests.Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(EquipCost(e, chosen, wielder), 4, "any other wielder pays the printed 4");

        // Battle: target an attachment (either player's): bow it, or straighten it.
        var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 0;
        var host = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var enemyHost = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var carrier = Add(b, YotsuChosen, 0, Zone.Attached, province: 0, parent: host.Id);
        var enemyItem = Add(b, "training-item", 1, Zone.Attached, province: 0, parent: enemyHost.Id); var ownItem = Add(b, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host.Id);
        var eb = MatchEngine.FromTestState(b);
        var bow = Enabled(eb, 0, "pcc-yotsu-bow", carrier.Id);
        Check(bow.TargetIds!.Contains(enemyItem.Id) && bow.TargetIds.Contains(ownItem.Id), "either player's attachment is a target");
        Check(!bow.TargetIds.Contains(enemyHost.Id), "a Personality is not");
        var version = eb.Version;
        Rejection(eb, Use(eb, 0, "pcc-yotsu-bow", carrier.Id, enemyHost.Id));
        Check(!eb.State.Cards[enemyHost.Id].Bowed && eb.Version == version, "an illegal target changes nothing");
        Accept(eb, Offer(eb, 0, "pcc-yotsu-bow", carrier.Id, enemyItem.Id));
        DeclineWindows(eb);
        Check(eb.State.Cards[enemyItem.Id].Bowed, "the target attachment bowed");
        // One printed action: the straighten half shares the use.
        Check(!CanUse(eb, 0, "pcc-yotsu-straighten", carrier.Id), "bow and straighten share one use");
        var e2 = MatchEngine.FromTestState(b);
        Accept(e2, Offer(e2, 0, "pcc-yotsu-straighten", carrier.Id, ownItem.Id));
        DeclineWindows(e2);
        Check(!e2.State.Cards[ownItem.Id].Bowed, "the other half straightens");
    }
}
