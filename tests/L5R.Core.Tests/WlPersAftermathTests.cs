using L5R.Core;
using static RegressionAssertions;

// Issue #674 (cluster pers-aftermath, personality-wave-132): seventeen Aftermath Personalities wired from their Aftermath scans and printed text (src/L5R.Core/WlPersAftermath.cs).
// Driven only through QueryActions and the commands the engine offers, on explicit positions (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker,
// province 0 the current battlefield). With player 0 acting, the Province a battle action changes is player 1's (the Defender is 1 - the active player).
internal static class WlPersAftermathTests
{
    private const string Uehara = "oracle-l5r-10850", Dijuro = "oracle-l5r-10867", Wateru = "oracle-l5r-10870", Tametaka = "oracle-l5r-10839", Chiboshi = "oracle-l5r-10874",
        Endo = "oracle-l5r-10875", Mutsuken = "oracle-l5r-10885", Isidoros = "oracle-l5r-10880", Esumi = "oracle-l5r-10834", Tamao = "oracle-l5r-10852",
        Heirai = "oracle-l5r-10865", Sujikaro = "oracle-l5r-10889", Takada = "oracle-l5r-10879", BrokenMan = "oracle-l5r-10866", Shusaku = "oracle-l5r-10858",
        Usui = "oracle-l5r-10884", Jiro = "oracle-l5r-10836";
    private static readonly string[] AllIds = [Uehara, Dijuro, Wateru, Tametaka, Chiboshi, Endo, Mutsuken, Isidoros, Esumi, Tamao, Heirai, Sujikaro, Takada, BrokenMan, Shusaku, Usui, Jiro];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null) => Batch336Support.Use(e, player, ability, cardId, targetId);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string RepoRoot() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("wl-pers-aftermath", [
        ("Published as printed: stats, keywords, printed text, abilities, entry Honor loss, Mantis waiver, manifest, art and Aftermath provenance of all seventeen", PublishedShape),
        ("Akodo Uehara - exp: a Terrain from your own discard pile returns to your hand and a card is then discarded; nothing else is a target", UeharaTerrain),
        ("Bayushi Dijuro: Siege Battle, Bow: Ranged 3 Attack; if it destroyed a card the Province gets +2 or -2 (one printed choice, one use); a miss changes nothing", DijuroSiege),
        ("Shinjo Sujikaro: Battle, Bow: Melee 3 Attack destroys Force 3 or less and spares Force 4", SujikaroMelee),
        ("Bayushi Wateru: recruited in a battle her Interrupt grants an additional Battle action; recruited in the Dynasty phase it cannot be used", WateruAdditionalAction),
        ("Daidoji Tametaka - exp2: three -1F tokens on an enemy Personality only; a Mantis Clan player ignores his Honor Requirement, another Clan does not", TametakaTokensAndWaiver),
        ("Daigotsu Chiboshi: Gold: equip a Follower from hand to him paying all costs, then an additional Battle action; only a Follower is a target", ChiboshiEquip),
        ("Daigotsu Endo: Limited, Gold: if dead, Recruit him paying all costs (Destined draws); not offered while he is alive", EndoRecruit),
        ("Ide Mutsuken: Open, Bow: the ACTIVE player chooses and bows one of his own unbowed Personalities; only that player may answer", MutsukenActivePlayerBows),
        ("Isidoros: loses 2 Honor on entering play; Battle: straighten your own Follower only", IsidorosStraightens),
        ("Kaiu Esumi - exp: Battle: destroy an enemy attachment only (Hida Komatsu's ability)", EsumiDestroysAttachment),
        ("Kitsu Tamao: after you Recruit her, create a 2F/2C/3PH Lion Clan Ancestor (optional); declining creates nothing", TamaoCreatesAncestor),
        ("Shiba Heirai: while opposed, discard a card for a Force bonus equal to its Focus Value; not offered unopposed or without a card", HeiraiBoost),
        ("Susumu Takada: Invest 3 on entering play: discard a card, gain Honor equal to its Focus Value; once, only then, and a trait (Cherry Blossom does not bar it)", TakadaInvest),
        ("The Broken Man: Invest 5 on entering play gives two +1F tokens; Earth Battle bows an enemy card with strictly lower Force only", BrokenManInvestAndBow),
        ("Tsuruchi Shusaku: Destined; Repeatable Battle, 2 Gold: +1F, any number of times a turn, each paid", ShusakuRepeatable),
        ("Usui: loses 1 Honor on entering play; Earth Battle: the current Province gets +2 strength until the turn ends", UsuiProvince),
        ("Yasuki Jiro: Political Limited, 3 Gold: dishonor a target Personality once a turn; an illegal target changes nothing", JiroDishonor),
    ]);

    // ---- helpers ----

    // Gold for abilities: marketplaces at home produce it; an ability's payment is the engine's own first option for the amount.
    private static void Gold(MatchState s, int player, int marketplaces = 4) { for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", player, Zone.Home); }
    private static GameCommand Paid(MatchEngine e, int player, int cost, GameCommand command) => command with { SourceIds = e.PaymentOptions(player, cost).First().SourceIds };

    // The first province card of the economy scenario becomes the named Personality, face up, ready to be recruited in the Dynasty phase.
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

    // The Defender (player 0 when player 1 attacks) owns province 0; a province "given strength" belongs to 1 - the active player.
    private static int ProvinceStrength(MatchEngine e, int owner) => e.ProvinceStrengthForTest(owner, 0);

    private static void NoStateChange(MatchEngine e, GameCommand command)
    {
        var before = System.Text.Json.JsonSerializer.Serialize(e.State);
        var result = e.Execute(command);
        Check(!result.Accepted, $"expected a rejection of {command.AbilityId}");
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == before, $"the rejected {command.AbilityId} changed the state");
    }

    // ---- shape ----

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/personality-wave-132.txt")).Where(l => l.Length > 0).ToArray();
        Check(new HashSet<string>(listed).SetEquals(AllIds) && listed.Length == 17, "personality-wave-132's manifest is exactly its seventeen cards");
        foreach (var id in AllIds) Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");

        void Is(string id, string name, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string text, string[] abilities, int? entryLoss = null, string? waiver = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Eq(c.Text, text, $"{name}: printed text");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, "none", $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
            Eq(c.HonorRequirementWaiverClan, waiver, $"{name}: Honor Requirement waiver");
            Check(!c.ScenarioOnly && !c.CreatedOnly, $"{name} is a real, deckable card");
        }
        const string reserve = "(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)";
        Is(Uehara, "Akodo Uehara", "Lion", 2, 3, 2, 7, 6, ["Tactician", "Unique", "Experienced", "Imperial", "Imperial Explorer", "Lion Clan", "Samurai", "Scout"],
            "Limited: Put a target Terrain from your discard pile into your hand. Discard a card.", ["wlpa-uehara-terrain"]);
        Is(Dijuro, "Bayushi Dijuro", "Scorpion", 3, 3, 2, 8, null, ["Tactician", "Unique", "Samurai", "Scorpion Clan", "Siege"],
            "Siege Battle, Bow this card: Ranged 3 Attack. If this destroyed a card, give this Province +2 or -2 strength.", ["wlpa-dijuro-raise", "wlpa-dijuro-lower"]);
        Is(Wateru, "Bayushi Wateru", "Scorpion", 4, 3, 1, 8, null, ["Reserve", "Ninja", "Samurai", "Scorpion Clan", "Yojimbo"],
            reserve + "\nNinja Interrupt: After you Recruit Wateru during battle, take an additional Battle action.", ["wlpa-wateru-additional"]);
        Is(Tametaka, "Daidoji Tametaka", "Crane", 5, 3, 2, 10, 2, ["Experienced 2", "Unique", "Commander", "Crane Clan", "Iron Crane", "Samurai", "Scout"],
            "Mantis Clan players ignore Tametaka's Honor Requirement.\nBattle: Give a target enemy Personality three -1F tokens.", ["wlpa-tametaka-tokens"], waiver: "Mantis");
        Is(Chiboshi, "Daigotsu Chiboshi", "Spider", 2, 3, 0, 5, null, ["Conqueror", "Reserve", "Commander", "Samurai", "Shadowlands", "Spider Clan"],
            reserve + "\nBattle, :gstar:: Equip a Follower to Chiboshi. You may take an additional Battle action.", ["wlpa-chiboshi-equip"]);
        Is(Endo, "Daigotsu Endo", "Spider", 4, 2, 0, 8, null, ["Destined", "Nonhuman", "Samurai", "Shadowlands", "Spider Clan", "Undead"],
            "(Draw a card after your Destined card enters play.)\nLimited, :gstar:: If Endo is dead, Recruit him.", ["wlpa-endo-recruit"]);
        Is(Mutsuken, "Ide Mutsuken", "Unicorn", 2, 5, 4, 8, 4, ["Unique", "Courtier", "Imperial", "Liaison", "Unicorn Clan"],
            "Open, Bow this card: The active player must choose and bow one of his unbowed Personalities.", ["wlpa-mutsuken-bow"]);
        Is(Isidoros, "Isidoros", "Unaligned", 3, 2, 0, 5, null, ["Gaijin", "Yodotai"], "After Isidoros enters play, lose 2 Honor.\nBattle: Straighten your target Follower.", ["wlpa-isidoros-straighten"], 2);
        Is(Esumi, "Kaiu Esumi", "Crab", 3, 3, 2, 9, 5, ["Tactician", "Unique", "Crab Clan", "Experienced", "Samurai", "Siege"], "Battle: Destroy a target enemy attachment.", ["komatsu-destroy-attachment"]);
        Is(Tamao, "Kitsu Tamao", "Lion", 0, 3, 2, 5, 3, ["Lion Clan", "Shugenja", "Water"],
            "Interrupt: After you Recruit Tamao, create a 2F/2C/3PH Lion Clan • Ancestor • Samurai • Spirit Personality.", ["wlpa-tamao-create"]);
        Is(Heirai, "Shiba Heirai", "Phoenix", 3, 3, 3, 5, 7, ["Phoenix Clan", "Samurai", "Yojimbo"],
            "Battle: If Heirai is opposed, discard a card to give him a Force bonus equal to the card's Focus Value.", ["wlpa-heirai-boost"]);
        Is(Sujikaro, "Shinjo Sujikaro", "Unicorn", 4, 2, 2, 8, 4, ["Cavalry", "Samurai", "Unicorn Clan"], "Battle, Bow this card: Melee 3 Attack.", ["wlpa-sujikaro-melee"]);
        Is(Takada, "Susumu Takada", "Spider", 2, 4, 1, 6, null, ["Loyal", "Courtier", "Master of Guile", "Spider Clan"],
            "Invest :g3:: Discard a card to gain Honor equal to its Focus Value. (After this card enters play, you may also pay the Invest cost to get the effect, once.)", ["wlpa-takada-invest"]);
        Is(BrokenMan, "The Broken Man", "Phoenix", 3, 3, 2, 5, 0, ["Earth", "Henshin", "Monk", "Phoenix Clan"],
            "Invest :g5:: Give The Broken Man two +1F tokens. (After this card enters play, you may also pay the Invest cost to get the effect, once.)\nEarth Battle: Bow a target enemy card with lower Force.",
            ["wlpa-brokenman-invest", "wlpa-brokenman-bow"]);
        Is(Shusaku, "Tsuruchi Shusaku", "Mantis", 1, 2, 1, 4, null, ["Destined", "Go Master", "Mantis Clan", "Samurai", "Scout"],
            "(Draw a card after your Destined card enters play.)\nRepeatable Battle, :g2:: Give Shusaku +1F. (Repeatable actions may be used any number of times per turn.)", ["wlpa-shusaku-boost"]);
        Is(Usui, "Usui", "Unaligned", 3, 4, 0, 5, null, ["Earth", "Fudo", "Monk"], "After Usui enters play, lose 1 Honor.\nEarth Battle: Give this Province +2 strength.", ["wlpa-usui-province"], 1);
        Is(Jiro, "Yasuki Jiro", "Crab", 1, 3, 1, 5, null, ["Courtier", "Crab Clan", "Kolat", "Merchant", "Shadowlands"], "Political Limited, :g3:: Dishonor a target Personality.", ["wlpa-jiro-dishonor"]);

        // Provenance: every card is wired from the Aftermath printing's own scan.
        var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published/personality-wave-132-sources.json"))).RootElement;
        Eq(sources.GetArrayLength(), 17, "one provenance row per card");
        foreach (var row in sources.EnumerateArray())
        {
            Check(row.GetProperty("image_path").GetString()!.Contains("/oracle/aftermath/"), $"{row.GetProperty("card_id")} comes from the Aftermath scan");
            Eq(row.GetProperty("printing_set")[0].GetString(), "Aftermath", $"{row.GetProperty("card_id")} printing");
        }

        // The published abilities exist with the printed timing, costs and trait status.
        Eq(AbilityCatalog.All["wlpa-dijuro-raise"].UsageKey, AbilityCatalog.All["wlpa-dijuro-lower"].UsageKey, "Dijuro's +2 or -2 is one printed choice sharing one use");
        Check(AbilityCatalog.All["wlpa-shusaku-boost"].UsesPerTurn == AbilityUseLimit.Unlimited, "Shusaku's action is Repeatable");
        Check(TraitReactions.Is("wlpa-takada-invest") && TraitReactions.Is("wlpa-brokenman-invest") && !TraitReactions.Is("wlpa-tamao-create") && !TraitReactions.Is("wlpa-wateru-additional"),
            "Invest is a trait; an Interrupt is not");
    }

    // ---- Akodo Uehara ----

    private static void UeharaTerrain()
    {
        var s = Table(Phase.Action, 0);
        var uehara = Add(s, Uehara, 0, Zone.Home);
        var terrain = Add(s, "training-terrain", 0, Zone.FateDiscard);
        var notTerrain = Add(s, "training-focus-strategy", 0, Zone.FateDiscard);
        var enemyTerrain = Add(s, "training-terrain", 1, Zone.FateDiscard);
        var handTerrain = Add(s, "training-terrain", 0, Zone.Hand);
        var other = Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpa-uehara-terrain", uehara.Id);
        Check(option.TargetIds!.SequenceEqual([terrain.Id]), $"only the Terrain in your own discard pile is offered (got {string.Join(",", option.TargetIds!)})");
        NoStateChange(e, Use(e, 0, "wlpa-uehara-terrain", uehara.Id, notTerrain.Id));
        NoStateChange(e, Use(e, 0, "wlpa-uehara-terrain", uehara.Id, enemyTerrain.Id));
        NoStateChange(e, Use(e, 0, "wlpa-uehara-terrain", uehara.Id, handTerrain.Id));
        Accept(e, Use(e, 0, "wlpa-uehara-terrain", uehara.Id, terrain.Id));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Hand, "the Terrain is in your hand");
        Check(e.State.Players[0].Hand.Contains(terrain.Id), "listed in the hand");
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "then a card is discarded");
        Eq(e.State.PendingChoice?.PlayerId, 0, "by the player who took the action");
        Accept(e, new GameCommand("forcedDiscard", 0, e.Version, TargetId: other.Id));
        Eq(e.State.Cards[other.Id].Zone, Zone.FateDiscard, "the chosen card is discarded");
        Check(e.State.PendingChoice is null, "the discard closes the action");
        Check(!CanUse(e, 0, "wlpa-uehara-terrain", uehara.Id), "Limited: once a turn");

        // With no Terrain in the discard pile it is not offered.
        var s2 = Table(Phase.Action, 0);
        var uehara2 = Add(s2, Uehara, 0, Zone.Home);
        Add(s2, "training-focus-strategy", 0, Zone.FateDiscard);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "wlpa-uehara-terrain", uehara2.Id), "no Terrain in your discard pile: nothing to target");
    }

    // ---- Bayushi Dijuro ----

    private static void DijuroSiege()
    {
        foreach (var raise in new[] { true, false })
        {
            var s = Table(Phase.Battle, 0);
            var dijuro = Add(s, Dijuro, 0, Zone.Battlefield, province: 0);
            var weak = Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0); // Force 2
            var e = MatchEngine.FromTestState(s);
            var before = ProvinceStrength(e, 1);
            var id = raise ? "wlpa-dijuro-raise" : "wlpa-dijuro-lower";
            Check(Enabled(e, 0, id, dijuro.Id).TargetIds!.Contains(weak.Id), "an enemy unit may be targeted");
            Accept(e, Use(e, 0, id, dijuro.Id, weak.Id));
            Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "Ranged 3 destroys Force 2");
            Check(e.State.Cards[dijuro.Id].Bowed, "Bow this card is a cost");
            Eq(ProvinceStrength(e, 1), before + (raise ? 2 : -2), raise ? "the Province gets +2" : "the Province gets -2");
            Check(!CanUse(e, 0, raise ? "wlpa-dijuro-lower" : "wlpa-dijuro-raise", dijuro.Id), "the other choice is the same printed action, bowed and used");
        }

        // A miss (Force 4 survives Ranged 3) leaves the Province alone, but the cost is paid.
        var s2 = Table(Phase.Battle, 0);
        var d2 = Add(s2, Dijuro, 0, Zone.Battlefield, province: 0);
        var strong = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var strength = ProvinceStrength(e2, 1);
        Accept(e2, Use(e2, 0, "wlpa-dijuro-raise", d2.Id, strong.Id));
        Eq(e2.State.Cards[strong.Id].Zone, Zone.Battlefield, "Force 4 survives Ranged 3");
        Eq(ProvinceStrength(e2, 1), strength, "no card destroyed: the Province is unchanged");
        Check(e2.State.Cards[d2.Id].Bowed, "the bow was paid");

        // The Province change lasts until the turn ends (the rulebook default), and a bowed Dijuro cannot take the action.
        var s3 = Table(Phase.Battle, 0);
        var d3 = Add(s3, Dijuro, 0, Zone.Battlefield, province: 0);
        var weak3 = Add(s3, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        var base3 = ProvinceStrength(e3, 1);
        Accept(e3, Use(e3, 0, "wlpa-dijuro-raise", d3.Id, weak3.Id));
        Eq(Batch336Support.StrengthNextTurn(e3, 1, 0), base3, "+2 ends with the turn");
        var s4 = Table(Phase.Battle, 0);
        var bowed = Add(s4, Dijuro, 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s4, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "wlpa-dijuro-raise", bowed.Id), "a bowed Dijuro cannot pay the cost");
    }

    // ---- Shinjo Sujikaro ----

    private static void SujikaroMelee()
    {
        var s = Table(Phase.Battle, 0);
        var sujikaro = Add(s, Sujikaro, 0, Zone.Battlefield, province: 0);
        var three = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var four = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "wlpa-sujikaro-melee", sujikaro.Id, four.Id));
        Eq(e.State.Cards[four.Id].Zone, Zone.Battlefield, "Melee 3 does not reach Force 4");
        Check(e.State.Cards[sujikaro.Id].Bowed, "Bow this card is paid even on a miss");

        var s2 = Table(Phase.Battle, 0);
        var sujikaro2 = Add(s2, Sujikaro, 0, Zone.Battlefield, province: 0);
        var three2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "wlpa-sujikaro-melee", sujikaro2.Id, three2.Id));
        Eq(e2.State.Cards[three2.Id].Zone, Zone.Dead, "Melee 3 destroys Force 3");

        var s3 = Table(Phase.Battle, 0);
        var bowed = Add(s3, Sujikaro, 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s3, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "wlpa-sujikaro-melee", bowed.Id), "a bowed Sujikaro cannot use it");
    }

    // ---- Bayushi Wateru ----

    private static void WateruAdditionalAction()
    {
        // Recruited as a Reserve card in a battle: the Interrupt is offered, and grants an additional Battle action.
        var s = Table();
        s.PriorityPlayerId = 0;
        var wateruId = s.Players[0].Provinces[0].CardId!;
        s.Cards[wateruId].DefinitionId = Wateru; s.Cards[wateruId].FaceUp = true;
        Gold(s, 0, 6);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("recruitReserve", 0, e.Version, CardId: wateruId, SourceIds: e.PaymentOptions(0, 8).First().SourceIds));
        Eq(e.State.Cards[wateruId].Zone, Zone.Battlefield, "Wateru was recruited into the battle");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "her entering-play window is open");
        var grants = e.State.AdditionalActions.Count(g => g.PlayerId == 0);
        Accept(e, Use(e, 0, "wlpa-wateru-additional", wateruId));
        Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), grants + 1, "an additional Battle action was granted");

        // Recruited in the Dynasty phase there is no battle: the Interrupt is refused and nothing changes.
        var (e2, wateru2) = Recruit(Wateru, st => st.Players[0].Honor = 10);
        Accept(e2, PurchaseFromProvince(e2, 0, wateru2.Id));
        Eq(e2.State.PendingChoice?.Type, "entryReaction", "the entering-play window opens in the Dynasty phase too");
        NoStateChange(e2, Use(e2, 0, "wlpa-wateru-additional", wateru2.Id));
        Eq(e2.State.AdditionalActions.Count(g => g.PlayerId == 0), 0, "no grant outside a battle");
    }

    // ---- Daidoji Tametaka ----

    private static void TametakaTokensAndWaiver()
    {
        var s = Table(Phase.Battle, 0);
        var tametaka = Add(s, Tametaka, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpa-tametaka-tokens", tametaka.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(own.Id), "only enemy Personalities are offered");
        NoStateChange(e, Use(e, 0, "wlpa-tametaka-tokens", tametaka.Id, own.Id));
        NoStateChange(e, Use(e, 0, "wlpa-tametaka-tokens", tametaka.Id, follower.Id));
        Accept(e, Use(e, 0, "wlpa-tametaka-tokens", tametaka.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Tokens.Count, 3, "three tokens");
        Check(e.State.Cards[enemy.Id].Tokens.All(t => t.Stat == "Force" && t.Amount == -1), "each is a -1F token");
        Eq(e.CardForceForTest(enemy.Id), 1, "Force 4 - 3");
        Check(!e.State.Cards[tametaka.Id].Bowed, "no cost");

        // "Mantis Clan players ignore Tametaka's Honor Requirement" (2): a Mantis player below it recruits him; a Crane player below it cannot.
        foreach (var (clan, allowed) in new[] { ("Mantis", true), ("Crane", false) })
        {
            var (e2, card) = Recruit(Tametaka, st => { st.Players[0].Honor = 0; st.Players[0].Clan = clan; });
            var error = e2.QuotePurchase(0, card.Id).Error;
            Check(allowed ? error is null || !error.Contains("Honor", StringComparison.OrdinalIgnoreCase) : error is not null && error.Contains("Honor", StringComparison.OrdinalIgnoreCase),
                $"{clan} player below the Honor Requirement: {error ?? "no error"}");
        }
    }

    // ---- Daigotsu Chiboshi ----

    private static void ChiboshiEquip()
    {
        var s = Table(Phase.Battle, 0);
        var chiboshi = Add(s, Chiboshi, 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-recon-follower", 0, Zone.Hand); // cost 1, no Honor Requirement
        var item = Add(s, "training-item", 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpa-chiboshi-equip", chiboshi.Id);
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(item.Id), $"only a Follower from the hand is offered (got {string.Join(",", option.TargetIds!)})");
        NoStateChange(e, Paid(e, 0, 1, Use(e, 0, "wlpa-chiboshi-equip", chiboshi.Id, item.Id)));
        var grants = e.State.AdditionalActions.Count(g => g.PlayerId == 0);
        Accept(e, Paid(e, 0, 1, Use(e, 0, "wlpa-chiboshi-equip", chiboshi.Id, follower.Id)));
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower is equipped");
        Eq(e.State.Cards[follower.Id].AttachedTo, chiboshi.Id, "to Chiboshi");
        Check(!e.State.Players[0].Hand.Contains(follower.Id), "no longer in hand");
        Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), grants + 1, "an additional Battle action may be taken");
    }

    // ---- Daigotsu Endo ----

    private static void EndoRecruit()
    {
        var s = Table(Phase.Action, 0);
        var endo = Add(s, Endo, 0, Zone.Dead);
        Gold(s, 0, 6);
        s.Players[0].Honor = 10;
        var alive = Add(s, Endo, 0, Zone.Home);
        var e0 = MatchEngine.FromTestState(s);
        Check(!CanUse(e0, 0, "wlpa-endo-recruit", alive.Id), "an Endo in play cannot be recruited");

        var s2 = Table(Phase.Action, 0);
        var dead = Add(s2, Endo, 0, Zone.Dead);
        Gold(s2, 0, 6);
        var e = MatchEngine.FromTestState(s2);
        var handBefore = e.State.Players[0].Hand.Count;
        var offered = Enabled(e, 0, "wlpa-endo-recruit", dead.Id).Command!; // the offered command names the dead card as its own target
        NoStateChange(e, offered with { SourceIds = null }); // unpaid
        Accept(e, Paid(e, 0, 8, offered));
        Eq(e.State.Cards[dead.Id].Zone, Zone.Home, "Endo is back in play");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "Destined: the recruit draws a card");
        Check(!CanUse(e, 0, "wlpa-endo-recruit", dead.Id), "alive again: not offered");
    }

    // ---- Ide Mutsuken ----

    private static void MutsukenActivePlayerBows()
    {
        // Mutsuken's controller (player 0, the Defender) is not the active player: player 1 chooses among HIS unbowed Personalities.
        var s = Table(Phase.Action, 1);
        var mutsuken = Add(s, Mutsuken, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var a = Add(s, "training-lion-samurai", 1, Zone.Home);
        var b = Add(s, "training-follower-payer", 1, Zone.Home);
        var bowedAlready = Add(s, "training-follower-payer", 1, Zone.Home, bowed: true);
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "wlpa-mutsuken-bow", mutsuken.Id));
        Check(e.State.Cards[mutsuken.Id].Bowed, "Bow this card is the cost");
        Eq(e.State.PendingDeciderChoice?.DeciderPlayerId, 1, "the active player decides");
        Check(e.State.PendingDeciderChoice!.Candidates.OrderBy(x => x).SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x)), "his unbowed Personalities are the choices");
        Reject(e, new GameCommand("chooseDecider", 0, e.Version, TargetId: a.Id)); // not his to answer
        Reject(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: mine.Id)); // not one of his
        Reject(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: bowedAlready.Id));
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: b.Id)); // one card: complete as it is chosen
        Check(e.State.Cards[b.Id].Bowed && !e.State.Cards[a.Id].Bowed && !e.State.Cards[mine.Id].Bowed, "exactly the chosen Personality is bowed");
        Check(e.State.PendingDeciderChoice is null, "the choice is closed");

        // The controller is the active player: he chooses among his own unbowed Personalities (Mutsuken, bowed as the cost, is not one).
        var s2 = Table(Phase.Action, 0);
        var mutsuken2 = Add(s2, Mutsuken, 0, Zone.Home);
        var only = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "wlpa-mutsuken-bow", mutsuken2.Id));
        Check(e2.State.Cards[only.Id].Bowed, "a single choice is made for him (nothing to decide)");
        Check(e2.State.PendingDeciderChoice is null, "no decision was asked");
    }

    // ---- Isidoros ----

    private static void IsidorosStraightens()
    {
        var (e0, isidoros0) = Recruit(Isidoros);
        var honorBefore = Honor(e0, 0);
        Accept(e0, PurchaseFromProvince(e0, 0, isidoros0.Id));
        Eq(Honor(e0, 0), honorBefore - 2, "After Isidoros enters play, lose 2 Honor");

        var s = Table(Phase.Battle, 0);
        var isidoros = Add(s, Isidoros, 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: enemyHost.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpa-isidoros-straighten", isidoros.Id);
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(enemyFollower.Id) && !option.TargetIds.Contains(host.Id), "only your own Follower is offered");
        NoStateChange(e, Use(e, 0, "wlpa-isidoros-straighten", isidoros.Id, enemyFollower.Id));
        NoStateChange(e, Use(e, 0, "wlpa-isidoros-straighten", isidoros.Id, host.Id));
        Accept(e, Use(e, 0, "wlpa-isidoros-straighten", isidoros.Id, follower.Id));
        Check(!e.State.Cards[follower.Id].Bowed, "the Follower is straightened");
        Check(e.State.Cards[enemyFollower.Id].Bowed, "the enemy's is not");
    }

    // ---- Kaiu Esumi ----

    private static void EsumiDestroysAttachment()
    {
        var s = Table(Phase.Battle, 0);
        var esumi = Add(s, Esumi, 0, Zone.Battlefield, province: 0);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: enemyHost.Id);
        var ownHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ownWeapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: ownHost.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "komatsu-destroy-attachment", esumi.Id);
        Check(option.TargetIds!.Contains(weapon.Id) && !option.TargetIds.Contains(ownWeapon.Id) && !option.TargetIds.Contains(enemyHost.Id), "only an enemy attachment is offered");
        NoStateChange(e, Use(e, 0, "komatsu-destroy-attachment", esumi.Id, ownWeapon.Id));
        NoStateChange(e, Use(e, 0, "komatsu-destroy-attachment", esumi.Id, enemyHost.Id));
        Accept(e, Use(e, 0, "komatsu-destroy-attachment", esumi.Id, weapon.Id));
        Check(e.State.Cards[weapon.Id].Zone != Zone.Attached, "the enemy attachment is destroyed");
        Eq(e.State.Cards[ownWeapon.Id].Zone, Zone.Attached, "your own is not");
    }

    // ---- Kitsu Tamao ----

    private static void TamaoCreatesAncestor()
    {
        foreach (var create in new[] { true, false })
        {
            var (e, tamao) = Recruit(Tamao, st => st.Players[0].Honor = 10);
            var cardsBefore = e.State.Cards.Count;
            Accept(e, PurchaseFromProvince(e, 0, tamao.Id));
            Eq(e.State.PendingChoice?.Type, "entryReaction", "her entering-play window is open");
            if (create)
            {
                Accept(e, Use(e, 0, "wlpa-tamao-create", tamao.Id));
                var created = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9785" && c.OwnerId == 0 && c.Zone != Zone.Province);
                var d = CardCatalog.Get(created.DefinitionId);
                Check(d.Force == 2 && d.Chi == 2 && d.PersonalHonor == 3 && d.Keywords!.Contains("Ancestor") && d.Keywords.Contains("Spirit") && d.Keywords.Contains("Samurai") && d.Keywords.Contains("Lion Clan"),
                    "a 2F/2C/3PH Lion Clan Ancestor Samurai Spirit");
                Eq(created.Zone, Zone.Home, "created at home");
                Eq(e.State.Cards.Count, cardsBefore + 1, "exactly one card is created");
            }
            else
            {
                Accept(e, new GameCommand("declineReaction", 0, e.Version));
                Eq(e.State.Cards.Count, cardsBefore, "declining the optional Interrupt creates nothing");
            }
        }
        // Not usable outside her own entry window.
        var s = Table(Phase.Action, 0);
        var tamao2 = Add(s, Tamao, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, "wlpa-tamao-create", tamao2.Id), "an Interrupt after a Recruit is not an action of a Personality already in play");
    }

    // ---- Shiba Heirai ----

    private static void HeiraiBoost()
    {
        var s = Table(Phase.Battle, 0);
        var heirai = Add(s, Heirai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus Value 3
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(heirai.Id);
        Enabled(e, 0, "wlpa-heirai-boost", heirai.Id);
        Accept(e, Use(e, 0, "wlpa-heirai-boost", heirai.Id, card.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the card is discarded");
        Eq(e.CardForceForTest(heirai.Id), before + 3, "Force bonus equal to the card's Focus Value");
        Check(e.State.Cards[heirai.Id].Modifiers.All(m => !m.Permanent), "until the turn ends");

        // Unopposed: not offered (no enemy unit at his battlefield).
        var s2 = Table(Phase.Battle, 0);
        var heirai2 = Add(s2, Heirai, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-focus-strategy", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "wlpa-heirai-boost", heirai2.Id), "Heirai is not opposed");
        NoStateChange(e2, Use(e2, 0, "wlpa-heirai-boost", heirai2.Id, s2.Players[0].Hand[0]));

        // No card in hand: nothing to discard.
        var s3 = Table(Phase.Battle, 0);
        var heirai3 = Add(s3, Heirai, 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "wlpa-heirai-boost", heirai3.Id), "no card to discard");
    }

    // ---- Susumu Takada ----

    private static void TakadaInvest()
    {
        var (e, takada) = Recruit(Takada, st => { st.Players[0].Clan = "Spider"; Gold(st, 0, 8); Add(st, "training-focus-strategy", 0, Zone.Hand); });
        var card = e.State.Players[0].Hand.Last();
        Check(e.State.Cards[card].DefinitionId == "training-focus-strategy", "the hand card is the Focus 3 fixture");
        Accept(e, PurchaseFromProvince(e, 0, takada.Id));
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"the entering-play window is open ({e.State.PendingChoice?.Type})");
        var honorBefore = Honor(e, 0);
        NoStateChange(e, Use(e, 0, "wlpa-takada-invest", takada.Id, null)); // no card named
        Accept(e, Paid(e, 0, 3, Use(e, 0, "wlpa-takada-invest", takada.Id, card)));
        Eq(e.State.Cards[card].Zone, Zone.FateDiscard, "the card is discarded");
        Eq(Honor(e, 0), honorBefore + 3, "Honor equal to its Focus Value");
        Check(!CanUse(e, 0, "wlpa-takada-invest", takada.Id), "once");

        // Invest is only for the moment of entering play: a Takada in play offers none.
        var s = Table(Phase.Action, 0);
        var inPlay = Add(s, Takada, 0, Zone.Home);
        Add(s, "training-focus-strategy", 0, Zone.Hand);
        Gold(s, 0);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, "wlpa-takada-invest", inPlay.Id), "Invest is paid only as the card enters play");
    }

    // ---- The Broken Man ----

    private static void BrokenManInvestAndBow()
    {
        var (e, man) = Recruit(BrokenMan, st => Gold(st, 0, 8));
        Accept(e, PurchaseFromProvince(e, 0, man.Id));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        var base3 = e.CardForceForTest(man.Id);
        Accept(e, Paid(e, 0, 5, Use(e, 0, "wlpa-brokenman-invest", man.Id)));
        Eq(e.CardForceForTest(man.Id), base3 + 2, "two +1F tokens");
        Eq(e.State.Cards[man.Id].Tokens.Count, 2, "two tokens");

        // Earth Battle: bow an enemy card whose Force is strictly lower than his (3).
        var s = Table(Phase.Battle, 0);
        var broken = Add(s, BrokenMan, 0, Zone.Battlefield, province: 0);
        var lower = Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0); // Force 2
        var equal = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var higher = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); // Force 2
        var own = Add(s, "training-follower-payer", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s);
        var option = Enabled(e2, 0, "wlpa-brokenman-bow", broken.Id);
        Check(option.TargetIds!.Contains(lower.Id) && option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(equal.Id) && !option.TargetIds.Contains(higher.Id) && !option.TargetIds.Contains(own.Id),
            $"only enemy cards with lower Force are offered (got {string.Join(",", option.TargetIds!)})");
        NoStateChange(e2, Use(e2, 0, "wlpa-brokenman-bow", broken.Id, equal.Id));
        NoStateChange(e2, Use(e2, 0, "wlpa-brokenman-bow", broken.Id, own.Id));
        Accept(e2, Use(e2, 0, "wlpa-brokenman-bow", broken.Id, lower.Id));
        Check(e2.State.Cards[lower.Id].Bowed, "the lower-Force enemy is bowed");
        Check(!e2.State.Cards[equal.Id].Bowed && !e2.State.Cards[higher.Id].Bowed, "the others are not");
        Check(!e2.State.Cards[broken.Id].Bowed, "no cost");
    }

    // ---- Tsuruchi Shusaku ----

    private static void ShusakuRepeatable()
    {
        var (e0, shusaku0) = Recruit(Shusaku);
        var handBefore = e0.State.Players[0].Hand.Count;
        Accept(e0, PurchaseFromProvince(e0, 0, shusaku0.Id));
        Eq(e0.State.Players[0].Hand.Count, handBefore + 1, "Destined draws a card");

        var s = Table(Phase.Battle, 0);
        var shusaku = Add(s, Shusaku, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Gold(s, 0, 6);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(shusaku.Id);
        for (var n = 1; n <= 3; n++)
        {
            Accept(e, Paid(e, 0, 2, Use(e, 0, "wlpa-shusaku-boost", shusaku.Id)));
            Eq(e.CardForceForTest(shusaku.Id), force + n, $"+1F, use {n} the same turn");
            Accept(e, new GameCommand("pass", 1, e.Version)); // a Battle action hands the opponent priority; Repeatable only lifts the once-a-turn limit
        }
        // Each use is paid: with no Gold left it is refused and nothing changes.
        var broke = Table(Phase.Battle, 0);
        var shusaku2 = Add(broke, Shusaku, 0, Zone.Battlefield, province: 0);
        Add(broke, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        foreach (var id in broke.Players[0].Provinces.Where(p => p.CardId is not null).Select(p => p.CardId!).ToArray()) { }
        var e2 = MatchEngine.FromTestState(broke);
        NoStateChange(e2, Use(e2, 0, "wlpa-shusaku-boost", shusaku2.Id)); // unpaid
    }

    // ---- Usui ----

    private static void UsuiProvince()
    {
        var (e0, usui0) = Recruit(Usui);
        var honorBefore = Honor(e0, 0);
        Accept(e0, PurchaseFromProvince(e0, 0, usui0.Id));
        Eq(Honor(e0, 0), honorBefore - 1, "After Usui enters play, lose 1 Honor");

        var s = Table(Phase.Battle, 0);
        var usui = Add(s, Usui, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = ProvinceStrength(e, 1);
        Accept(e, Use(e, 0, "wlpa-usui-province", usui.Id));
        Eq(ProvinceStrength(e, 1), before + 2, "this Province has +2 strength");
        Eq(Batch336Support.StrengthNextTurn(e, 1, 0), before, "until the turn ends (the rulebook default)");
        Check(!e.State.Cards[usui.Id].Bowed, "no cost");
    }

    // ---- Yasuki Jiro ----

    private static void JiroDishonor()
    {
        var s = Table(Phase.Action, 0);
        var jiro = Add(s, Jiro, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var host = Add(s, "training-crane-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wlpa-jiro-dishonor", jiro.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(follower.Id), "a Personality (not a Follower) is a target");
        NoStateChange(e, Paid(e, 0, 3, Use(e, 0, "wlpa-jiro-dishonor", jiro.Id, follower.Id)));
        NoStateChange(e, Use(e, 0, "wlpa-jiro-dishonor", jiro.Id, enemy.Id)); // unpaid
        Accept(e, Paid(e, 0, 3, Use(e, 0, "wlpa-jiro-dishonor", jiro.Id, enemy.Id)));
        Check(e.State.Cards[enemy.Id].Dishonorable, "the target is dishonored");
        Check(!CanUse(e, 0, "wlpa-jiro-dishonor", jiro.Id), "Limited: once a turn");
    }
}
