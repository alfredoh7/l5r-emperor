using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Embers of War pass (issue #460, batch 6): the cards of item-wave-12, holding-wave-9, follower-wave-28 and personality-wave-64, driven through
// QueryActions and the commands it offers. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion),
// with keyword-fixture grants (GrantedKeywords, "test-fixture") standing in for keywords no training fixture already carries. The two Focus-Effect-timed
// transfer abilities (splintered-weapon-focus, flanking-unit-focus) are checked for shape only (Timing == "FocusEffect"), the same bar the Seeds of
// Decay and Before the Dawn passes already set for plain FocusEffect abilities (SeedsOfDecayBatch1Tests, DuelingFocusEffectReevaluationTests) -- their
// shared execution case (TransferAttachment) is exercised in full by Field Messenger's Battle/Open ability below instead.
internal static class EmbersOfWarBatch6Tests
{
    private const string CelestialLion = "oracle-l5r-1268", CelestialUnicorn = "oracle-l5r-1273", DazzlingAttire = "oracle-l5r-1875",
        SplinteredWeapon = "oracle-l5r-7435", AncestralSwordHantei = "oracle-l5r-7958",
        StagingGrounds = "oracle-l5r-7444", SurveillanceOutpost = "oracle-l5r-7671", TempleToElements = "oracle-l5r-7897", TerracedFarm = "oracle-l5r-7915",
        FieldMessenger = "oracle-l5r-2520", FlankingUnit = "oracle-l5r-2598",
        Guest = "oracle-l5r-9998", HarukisDeer = "oracle-l5r-9999";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-6", [
        ("Published shape: every card of the four waves prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("The three Celestial Sword siblings (Lion, Unicorn, the Spider-Clan-restricted Ancestral Sword of Hantei): +1F per controlled Clan Personality, +2 Honor on entry", CelestialSwordForceAndHonor),
        ("Celestial Sword of the Lion - exp: Battle, create a 2F/2C/3PH Lion Clan Ancestor Personality at the current battlefield", CelestialSwordLionCreate),
        ("Celestial Sword of the Unicorn - exp: Battle, bow a target enemy card, destroying it only if it is neither Cavalry nor carries an attachment", CelestialSwordUnicornBow),
        ("The Ancestral Sword of Hantei - exp: Battle, target a card in a unit, destroy it first if it is bowed or an attachment, then bow it, then straighten the wielder", AncestralSwordHanteiBow),
        ("Dazzling Attire: Political Open, bow this card, take the Imperial Favor -- no Honor requirement, unlike the universal Lobby ability", DazzlingAttireFavor),
        ("Splintered Weapon: Battle, target an enemy card without attachments, bow it, destroy it only if the wielding Personality is a Berserker", SplinteredWeaponBow),
        ("Staging Grounds: produces its printed 3 Gold ordinarily, 4 Gold restricted to that one card when paying for a card with printed Gold Cost 9 or more", StagingGroundsProduction),
        ("Surveillance Outpost: Limited, give the performing Siege Personality Conqueror -- only while the controller has fewer Provinces than their opponent, or exactly one", SurveillanceOutpostConqueror),
        ("Temple to the Elements: Open, give a target Monk or Shugenja of the controller's one of the four Elements, sharing one use across all four", TempleToElementsGive),
        ("Terraced Farm: Battle, bow this card, give every Ashigaru Personality and Follower in the controller's army at the current battlefield +1F", TerracedFarmBoost),
        ("Field Messenger: Battle/Open, transfer this Follower to a target Personality of its controller's -- re-evaluated for this pass now that AttachmentTransfers.cs exists", FieldMessengerTransfer),
        ("Flanking Unit: Battle, move a target Personality at the current battlefield home, or to an adjacent battlefield (a player's choice, sharing one use); its Focus Effect transfer shares Splintered Weapon's shape (checked for Timing only)", FlankingUnitMove),
        ("Guest and Haruki's Deer: two CreatedOnly Proxy tokens, published with their true Personality stats/keywords; Haruki's Deer's own Limited Honor gain is conditional on another player having higher Family Honor", ProxyPersonalities),
    ]);

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

        var lion = CardCatalog.Get(CelestialLion);
        Check(lion.Type == CardType.Item && lion.Force == 1 && lion.Chi == 1 && lion.GoldCost == 8 && lion.Effect == "celestial-sword-force-lion"
            && lion.AttachRequiresKeyword == "Lion Clan" && lion.Abilities!.SequenceEqual(["celestial-sword-lion-honor", "celestial-sword-lion-create"]), "Celestial Sword of the Lion - exp");
        var unicorn = CardCatalog.Get(CelestialUnicorn);
        Check(unicorn.Type == CardType.Item && unicorn.Force == 1 && unicorn.Chi == 1 && unicorn.Effect == "celestial-sword-force-unicorn"
            && unicorn.AttachRequiresKeyword == "Unicorn Clan" && unicorn.Abilities!.SequenceEqual(["celestial-sword-unicorn-honor", "celestial-sword-unicorn-bow"]), "Celestial Sword of the Unicorn - exp");
        var dazzling = CardCatalog.Get(DazzlingAttire);
        Check(dazzling.Type == CardType.Item && dazzling.Abilities!.SequenceEqual(["dazzling-attire-favor"]), "Dazzling Attire");
        var splintered = CardCatalog.Get(SplinteredWeapon);
        Check(splintered.Type == CardType.Item && Keywords(SplinteredWeapon).Contains("Weapon") && splintered.Force == 3
            && splintered.Abilities!.SequenceEqual(["splintered-weapon-focus", "splintered-weapon-bow"]), "Splintered Weapon");
        var hantei = CardCatalog.Get(AncestralSwordHantei);
        Check(hantei.Type == CardType.Item && hantei.Effect == "celestial-sword-force-spider" && hantei.AttachRequiresKeyword == "Spider Clan"
            && hantei.Abilities!.SequenceEqual(["ancestral-sword-hantei-honor", "ancestral-sword-hantei-bow"]), "The Ancestral Sword of Hantei - exp");
        Manifest(root, "item-wave-12", CelestialLion, CelestialUnicorn, DazzlingAttire, SplinteredWeapon, AncestralSwordHantei);

        var staging = CardCatalog.Get(StagingGrounds);
        Check(staging.Type == CardType.Holding && staging.GoldProduction == 3 && staging.Effect == "staging-grounds-gold" && staging.Abilities!.Length == 0, "Staging Grounds");
        var outpost = CardCatalog.Get(SurveillanceOutpost);
        Check(outpost.Type == CardType.Holding && outpost.GoldProduction == 2 && outpost.Abilities!.SequenceEqual(["surveillance-outpost-conqueror"]), "Surveillance Outpost");
        var temple = CardCatalog.Get(TempleToElements);
        Check(temple.Type == CardType.Holding && temple.GoldProduction == 5
            && temple.Abilities!.SequenceEqual(["temple-elements-air", "temple-elements-earth", "temple-elements-fire", "temple-elements-water"]), "Temple to the Elements");
        var farm = CardCatalog.Get(TerracedFarm);
        Check(farm.Type == CardType.Holding && farm.GoldProduction == 2 && farm.Abilities!.SequenceEqual(["terraced-farm-boost"]), "Terraced Farm");
        Manifest(root, "holding-wave-9", StagingGrounds, SurveillanceOutpost, TempleToElements, TerracedFarm);

        var messenger = CardCatalog.Get(FieldMessenger);
        Check(messenger.Type == CardType.Follower && messenger.Force == 2 && Keywords(FieldMessenger).Contains("Cavalry") && messenger.Abilities!.SequenceEqual(["field-messenger-transfer"]), "Field Messenger");
        var flanking = CardCatalog.Get(FlankingUnit);
        Check(flanking.Type == CardType.Follower && flanking.Force == 4
            && flanking.Abilities!.SequenceEqual(["flanking-unit-focus", "flanking-unit-move-home", "flanking-unit-move-adjacent"]), "Flanking Unit");
        Manifest(root, "follower-wave-28", FieldMessenger, FlankingUnit);

        var guest = CardCatalog.Get(Guest);
        Check(guest.Type == CardType.Personality && guest.CreatedOnly && guest.Force == 1 && guest.Chi == 1 && guest.PersonalHonor == 1
            && Keywords(Guest) == "Monk" && guest.Abilities!.Length == 0, "Guest");
        var deer = CardCatalog.Get(HarukisDeer);
        Check(deer.Type == CardType.Personality && deer.CreatedOnly && deer.Clan == "Mantis" && deer.Force == 2 && deer.Chi == 2 && deer.PersonalHonor == 3
            && Keywords(HarukisDeer) == "Deer,Mantis Clan,Nonhuman,Spirit" && deer.Abilities!.SequenceEqual(["haruki-deer-honor"]), "Haruki's Deer");
        Manifest(root, "personality-wave-64", Guest, HarukisDeer);
    }

    private static void CelestialSwordForceAndHonor()
    {
        // Each sword's own Clan restriction needs a genuinely Clan-aligned wielder: ClanAlignments() reads a card's printed Clan field (and any
        // static grant), never a plain GrantedKeywords entry, so a real fixture of the matching Clan is used rather than a granted "X Clan" keyword.
        foreach (var (sword, wielderFixture, wielderClan) in new[] { (CelestialLion, "training-lion-samurai", "Lion"), (CelestialUnicorn, "training-battle-maiden", "Unicorn"), (AncestralSwordHantei, "training-spider-honor-loss", "Spider") })
        {
            var s = Table(Phase.Battle, 0);
            var wielder = Add(s, wielderFixture, 0, Zone.Battlefield, province: 0);
            Add(s, wielderFixture, 0, Zone.Home);
            Add(s, sword, 0, Zone.Attached, province: 0, parent: wielder.Id);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var force = e.GetView(0).Players[0].Army.Single(c => c.Id == wielder.Id).Force;
            var baseForce = CardCatalog.Get(wielderFixture).Force;
            Eq(force, baseForce + CardCatalog.Get(sword).Force + 2, $"{sword}: the wielder's own base Force, plus the sword's own printed Force modifier, plus +1F for each of the two {wielderClan} Clan Personalities controlled");
        }
    }

    private static void CelestialSwordLionCreate()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        wielder.GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent));
        var sword = Add(s, CelestialLion, 0, Zone.Attached, province: 0, parent: wielder.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, Use(e, 0, "celestial-sword-lion-create", sword.Id));
        var created = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0 && created.OwnerId == 0, "created at the current battlefield, controlled by the sword's controller");
        var definition = CardCatalog.Get(created.DefinitionId);
        Check(definition.Force == 2 && definition.Chi == 2 && definition.PersonalHonor == 3
            && new[] { "Lion Clan", "Samurai", "Ancestor", "Spirit" }.All(k => definition.Keywords!.Contains(k)), "2F/2C/3PH Lion Clan - Samurai - Ancestor - Spirit");
    }

    private static void CelestialSwordUnicornBow()
    {
        // Destroyed: not Cavalry, no attachments.
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var sword = Add(s, CelestialUnicorn, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "celestial-sword-unicorn-bow", sword.Id, bare.Id));
        Eq(e.State.Cards[bare.Id].Zone, Zone.Dead, "destroyed: neither Cavalry nor carrying an attachment");

        // Bowed but survives: Cavalry.
        var s2 = Table(Phase.Battle, 0);
        var wielder2 = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var sword2 = Add(s2, CelestialUnicorn, 0, Zone.Attached, province: 0, parent: wielder2.Id);
        var cavalry = Add(s2, "training-cavalry", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "celestial-sword-unicorn-bow", sword2.Id, cavalry.Id));
        Check(e2.State.Cards[cavalry.Id].Bowed && e2.State.Cards[cavalry.Id].Zone != Zone.Dead, "bowed but survives: Cavalry");
    }

    private static void AncestralSwordHanteiBow()
    {
        // Already bowed: destroyed, then the (redundant) bow, then the wielder straightens.
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var sword = Add(s, AncestralSwordHantei, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var bowedTarget = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "ancestral-sword-hantei-bow", sword.Id, bowedTarget.Id));
        Eq(e.State.Cards[bowedTarget.Id].Zone, Zone.Dead, "destroyed: the target was already bowed");
        Check(!e.State.Cards[wielder.Id].Bowed, "the wielder straightened even though he acted while bowed (Even if bowed)");

        // Not bowed, not an attachment: survives, is bowed, and the wielder still straightens.
        var s2 = Table(Phase.Battle, 0);
        var wielder2 = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var sword2 = Add(s2, AncestralSwordHantei, 0, Zone.Attached, province: 0, parent: wielder2.Id);
        var freshTarget = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "ancestral-sword-hantei-bow", sword2.Id, freshTarget.Id));
        Check(e2.State.Cards[freshTarget.Id].Zone != Zone.Dead && e2.State.Cards[freshTarget.Id].Bowed, "survives, now bowed by this action");
        Check(!e2.State.Cards[wielder2.Id].Bowed, "the wielder straightened");
    }

    private static void DazzlingAttireFavor()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, "training-lion-samurai", 0, Zone.Home);
        var attire = Add(s, DazzlingAttire, 0, Zone.Attached, parent: wielder.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "dazzling-attire-favor", attire.Id));
        Eq(e.State.FavorPlayerId, 0, "took the Imperial Favor with no Honor requirement of its own");
        Check(e.State.Cards[attire.Id].Bowed, "bowed as its own cost");
    }

    private static void SplinteredWeaponBow()
    {
        // Not a Berserker: bowed, survives.
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, SplinteredWeapon, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "splintered-weapon-bow", weapon.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed && e.State.Cards[enemy.Id].Zone != Zone.Dead, "bowed but survives: the wielder is not a Berserker");

        // A Berserker wielder: destroyed.
        var s2 = Table(Phase.Battle, 0);
        var berserker = Add(s2, "training-berserker", 0, Zone.Battlefield, province: 0);
        var weapon2 = Add(s2, SplinteredWeapon, 0, Zone.Attached, province: 0, parent: berserker.Id);
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "splintered-weapon-bow", weapon2.Id, enemy2.Id));
        Eq(e2.State.Cards[enemy2.Id].Zone, Zone.Dead, "destroyed: the wielder is a Berserker");
        Check(!CanUse(e2, 0, "splintered-weapon-bow", cardId: weapon2.Id, performerId: enemy2.Id), "an already-attached enemy card is not a legal target");
        Eq(AbilityCatalog.All["splintered-weapon-focus"].Timing, "FocusEffect", "the Focus Effect half shares the FocusEffect timing every other duel-transfer ability of this pass and the previous one uses");
    }

    private static void StagingGroundsProduction()
    {
        var s = Table(Phase.Action, 0);
        var grounds = Add(s, StagingGrounds, 0, Zone.Home);
        var cheap = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.GoldCost is >= 1 and < 9);
        var expensive = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.GoldCost >= 9);
        var cheapCard = Add(s, cheap.Id, 0, Zone.Hand);
        var expensiveCard = Add(s, expensive.Id, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(grounds.Id, 0), 3, "its printed base production, with no payment named");
        Eq(e.GoldProduced(grounds.Id, 0, new PaymentSubject(cheapCard, PaymentKind.Play)), 3, "still its base production for a card under 9 printed Gold Cost");
        Eq(e.GoldProduced(grounds.Id, 0, new PaymentSubject(expensiveCard, PaymentKind.Play)), 4, "4 Gold, restricted, for paying for a card with printed Gold Cost 9 or more");
    }

    private static void SurveillanceOutpostConqueror()
    {
        // The controller has exactly one Province: legal even though their opponent has more.
        var s = Table(Phase.Action, 0);
        var outpost = Add(s, SurveillanceOutpost, 0, Zone.Home);
        var siege = Add(s, "training-lion-samurai", 0, Zone.Home);
        siege.GrantedKeywords.Add(new("Siege", "test-fixture", GrantDuration.Permanent));
        s.Players[0].Provinces = [s.Players[0].Provinces[0]];
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "surveillance-outpost-conqueror", outpost.Id, performerId: siege.Id));
        Check(e.HasKeyword(siege.Id, "Conqueror"), "the performing Siege Personality was given Conqueror");

        // Neither fewer Provinces nor exactly one: refused.
        var s2 = Table(Phase.Action, 0);
        var outpost2 = Add(s2, SurveillanceOutpost, 0, Zone.Home);
        var siege2 = Add(s2, "training-lion-samurai", 0, Zone.Home);
        siege2.GrantedKeywords.Add(new("Siege", "test-fixture", GrantDuration.Permanent));
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "surveillance-outpost-conqueror", outpost2.Id, siege2.Id), "equal Province counts, neither side reduced to one, is refused");

        // Not a Siege Personality: refused.
        var nonSiege = Add(s, "training-lion-samurai", 0, Zone.Home);
        var eStill1 = MatchEngine.FromTestState(s);
        Check(!CanUse(eStill1, 0, "surveillance-outpost-conqueror", outpost.Id, nonSiege.Id), "a non-Siege Personality cannot perform");
    }

    private static void TempleToElementsGive()
    {
        var s = Table(Phase.Action, 0);
        var temple = Add(s, TempleToElements, 0, Zone.Home);
        var monk = Add(s, "training-monk", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "temple-elements-fire", temple.Id, monk.Id));
        Check(e.HasKeyword(monk.Id, "Fire"), "given Fire");
        Check(!CanUse(e, 0, "temple-elements-air", temple.Id, monk.Id), "the four choices share one use");

        var nonMonkOrShugenja = Add(s, "training-lion-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Check(!CanUse(e2, 0, "temple-elements-water", temple.Id, nonMonkOrShugenja.Id), "neither a Monk nor a Shugenja is a legal target");
    }

    private static void TerracedFarmBoost()
    {
        var s = Table(Phase.Battle, 0);
        var farm = Add(s, TerracedFarm, 0, Zone.Battlefield, province: 0);
        var ashigaruPersonality = Add(s, "training-ashigaru-personality", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var ashigaruFollower = Add(s, "training-ashigaru-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "terraced-farm-boost", farm.Id));
        var army = e.GetView(0).Players[0].Army;
        Eq(army.Single(c => c.Id == ashigaruPersonality.Id).Force, 3, "the Ashigaru Personality: +1F");
        Eq(army.Single(c => c.Id == host.Id).Force, CardCatalog.Get("training-lion-samurai").Force, "the non-Ashigaru host Personality: untouched");
        Check(e.State.Cards[farm.Id].Bowed, "the Holding itself bowed as its own cost");
    }

    private static void FieldMessengerTransfer()
    {
        var s = Table(Phase.Battle, 0);
        var host1 = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var host2 = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var messenger = Add(s, FieldMessenger, 0, Zone.Attached, province: 0, parent: host1.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "field-messenger-transfer", messenger.Id, host2.Id));
        Eq(e.State.Cards[messenger.Id].AttachedTo, host2.Id, "transferred to the target Personality");
    }

    private static void FlankingUnitMove()
    {
        // Home.
        var s = Table(Phase.Battle, 0);
        var flanking = Add(s, FlankingUnit, 0, Zone.Attached, province: 0, parent: Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0).Id);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "flanking-unit-move-home", flanking.Id, target.Id));
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "moved home");
        Eq(AbilityCatalog.All["flanking-unit-focus"].Timing, "FocusEffect", "the Focus Effect half shares Splintered Weapon's transfer shape, checked for Timing only");
    }

    private static void ProxyPersonalities()
    {
        var s = Table(Phase.Action, 0);
        var deer = Add(s, HarukisDeer, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "haruki-deer-honor", deer.Id), "no other player has higher Family Honor yet");
        var behind = Edit(e, st => st.Players[1].Honor = st.Players[0].Honor + 5);
        var honorBefore = behind.State.Players[0].Honor;
        Accept(behind, Use(behind, 0, "haruki-deer-honor", deer.Id));
        Eq(behind.State.Players[0].Honor, honorBefore + 2, "gains 2 Honor once another player's Family Honor is higher");
    }
}
