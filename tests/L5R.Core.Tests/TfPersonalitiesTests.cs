using L5R.Core;
using static RegressionAssertions;

// Personality wave 121 (issue #622, "tf-personalities"): fifteen Personalities with a Twenty Festivals primary printing wired from their earliest Emperor-era scan and printed text.
// Driven only through QueryActions and the commands the engine offers, on explicit positions (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker,
// province 0 the current battlefield).
internal static class TfPersonalitiesTests
{
    private const string Misora = "oracle-l5r-10861", Aibako = "oracle-l5r-10581", Mifuyu = "oracle-l5r-10869", Ryuko = "oracle-l5r-10660", Iguchi = "oracle-l5r-10575",
        Ikariya = "oracle-l5r-10862", Nairuko = "oracle-l5r-10242", Gorobei = "oracle-l5r-10835", Izumiko = "oracle-l5r-10576", Laoshe = "oracle-l5r-10663",
        Yokohime = "oracle-l5r-5290", Paikao = "oracle-l5r-10887", Kodanshi = "oracle-l5r-10082", Iabuchi = "oracle-l5r-10621", Sakiko = "oracle-l5r-10890", Ninja = "oracle-l5r-10200";
    private static readonly string[] AllIds = [Misora, Aibako, Mifuyu, Ryuko, Iguchi, Ikariya, Nairuko, Gorobei, Izumiko, Laoshe, Yokohime, Paikao, Kodanshi, Iabuchi, Sakiko];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, null, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string RepoRoot() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("tf-personalities", [
        ("Published as printed: the earliest Emperor-era stats, keywords, printed text, abilities, entry Honor loss, manifest and art of all fifteen", PublishedShape),
        ("Asako Misora: Expendable -- destroyed (not discarded) she draws her controller a card; Inquisitor and Magistrate come as printed", MisoraExpendable),
        ("Bayushi Aibako: printed with no rules text and Conqueror -- her unit does not bow after a won battle", AibakoConqueror),
        ("Hida Iguchi: printed Cavalry with no rules text -- a Cavalry unit as the maneuvers rule reads it", IguchiCavalry),
        ("Utaku Sakiko: Cavalry, Destined and Reserve as printed -- recruiting her draws a card, and she is recruitable as a Reserve card", SakikoKeywords),
        ("Bayushi Mifuyu: Battle -3F to a target enemy Personality only (not a Follower, not her own), until the turn ends", MifuyuWeakens),
        ("Daigotsu Ryuko: entering play draws (Destined) and loses 1 Honor; Battle, Bow: Melee 2 Attack destroys Force 2 or less, bows her as a cost and spares Force 3", RyukoEntryAndMelee),
        ("Toritaka Iabuchi: Destined; Battle, Bow: Ranged 2 Attack is bowed-cost, destroys Force 2 or less, and is not offered when she is bowed", IabuchiRanged),
        ("Isawa Ikariya: Ranged Attack with strength equal to the Spells in her unit (none, one, two; another Personality's Spells do not count)", IkariyaSpellAttack),
        ("Kaiu Gorobei: discard a card to bow an enemy card whose Force does not exceed the discarded card's Focus Value; illegal choices change nothing", GorobeiDiscardBow),
        ("Moto Paikao: discard a card for a Force penalty equal to its Focus Value on an enemy Personality or Follower; own cards are not targets", PaikaoDiscardPenalty),
        ("Lao-she: a Home Battle action -- usable from home with no unit at the battle, only if enemy units are there; moves him to the battlefield, bowed he cannot", LaosheHomeBattle),
        ("Moshi Yokohime: Battle: bow an enemy card without attachments (a card with attachments is not a target)", YokohimeBow),
        ("Isawa Nairuko: +2F only while she has a Spell", NairukoForce),
        ("Kakita Izumiko: wins a tied duel as a Duelist and gains 1 Honor; loses a duel and gains none; a duel won by someone else gives her nothing", IzumikoDuelHonor),
        ("Soshi Kodanshi: recruited from a province she creates a Scorpion Clan Ninja 0F/2C/0PH at home (mandatory, no window, one Ninja); recruited by Shinden Shorai the same; not when she enters another way", KodanshiCreatesNinja),
    ]);

    // ---- helpers ----

    private static CardInstance AddForce(CardInstance card, int amount) { Batch336Support.Force(card, amount); return card; }

    private static GameCommand PurchaseFromProvince(MatchEngine e, int player, string cardId) =>
        new("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds);

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

    // ---- shape ----

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/personality-wave-121.txt")).Where(l => l.Length > 0).ToArray();
        Check(new HashSet<string>(listed).SetEquals(AllIds) && listed.Length == 15, "personality-wave-121's manifest is exactly its fifteen cards");
        foreach (var id in AllIds) Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");

        // (id, Clan, Force, Chi, PH, Gold, Honor Requirement, keywords as printed (clan keyword excluded), text, abilities, effect) read off each scan.
        void Is(string id, string name, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string text, string[] abilities, string effect, int? entryLoss = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Eq(c.Text, text, $"{name}: printed text");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
        }
        Is(Misora, "Asako Misora", "Phoenix", 1, 4, 3, 5, 6, ["Expendable", "Earth", "Inquisitor", "Magistrate", "Shugenja"], "(Draw a card after your Expendable card is destroyed.)", [], "none");
        Is(Aibako, "Bayushi Aibako", "Scorpion", 3, 3, 2, 5, null, ["Conqueror", "Bitter Lies", "Samurai"], "", [], "none");
        Is(Mifuyu, "Bayushi Mifuyu", "Scorpion", 4, 3, 1, 7, null, ["Magistrate", "Samurai"], "Battle: Give a target enemy Personality -3F.", ["tfp-mifuyu-weaken"], "none");
        Is(Ryuko, "Daigotsu Ryuko", "Spider", 3, 2, 0, 7, null, ["Destined", "Fallen", "Paragon", "Samurai", "Shadowlands"],
            "(Draw a card after your Destined card enters play.)\nAfter Ryuko enters play, lose 1 Honor.\nBattle, Bow: Melee 2 Attack.", ["tfp-ryuko-melee"], "none", 1);
        Is(Iguchi, "Hida Iguchi", "Crab", 3, 1, 1, 4, null, ["Cavalry", "Samurai"], "", [], "none");
        Is(Ikariya, "Isawa Ikariya", "Phoenix", 4, 4, 2, 9, 4, ["Reserve", "Fire", "Shugenja"],
            "(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)\nFire Battle: Ranged Attack with strength equal to the number of Spells in this unit.", ["tfp-ikariya-attack"], "none");
        Is(Nairuko, "Isawa Nairuko", "Phoenix", 2, 3, 3, 6, 2, ["Earth", "Shugenja", "Soul of Isawa Mariko"], "Nairuko has +2F while she has a Spell.", [], "force-with-spell-2");
        Is(Gorobei, "Kaiu Gorobei", "Crab", 3, 4, 3, 5, 3, ["Samurai", "Siege"], "Battle: Discard a card to bow a target enemy card with Force equal to or lower than the discarded card's Focus Value.", ["tfp-gorobei-bow"], "none");
        Is(Izumiko, "Kakita Izumiko", "Crane", 2, 3, 3, 5, 6, ["Duelist", "Samurai"], "After Izumiko wins a duel, gain 1 Honor.", [], "tfp-izumiko-duel-honor");
        Is(Laoshe, "Lao-she", "Spider", 2, 2, 1, 4, null, ["Kensai", "Monk"], "Home Battle: If Lao-she would be opposed, move him to the current battlefield. (Home actions may be taken from home.)", ["tfp-laoshe-move"], "none");
        Is(Yokohime, "Moshi Yokohime", "Mantis", 3, 3, 1, 7, null, ["Naval", "Shugenja", "Soul of Moshi Yuriko", "Thunder"], "Battle: Target an enemy card without attachments: Bow it.", ["masumi-bow"], "none");
        Is(Paikao, "Moto Paikao", "Unicorn", 3, 3, 2, 5, 3, ["Commander", "Samurai"], "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value.", ["tfp-paikao-weaken"], "none");
        Is(Kodanshi, "Soshi Kodanshi", "Scorpion", 4, 3, 0, 7, null, ["Ninja"], "After Kodanshi enters play from a province: Create a 0F/2C/0PH Scorpion Clan • Ninja Personality.", [], "tfp-kodanshi-entry-create");
        Is(Iabuchi, "Toritaka Iabuchi", "Crab", 3, 2, 2, 7, 3, ["Destined", "Samurai", "Scout"], "(Draw a card after your Destined card enters play.)\nBattle, Bow: Ranged 2 Attack.", ["tfp-iabuchi-ranged"], "none");
        Is(Sakiko, "Utaku Sakiko", "Unicorn", 3, 3, 3, 8, 4, ["Cavalry", "Destined", "Reserve", "Battle Maiden", "Samurai"],
            "(You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)\n(Draw a card after your Destined card enters play.)", [], "none");

        // Provenance: each is wired from the intended Emperor-era printing's own scan.
        var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published/personality-wave-121-sources.json"))).RootElement;
        var expected = new Dictionary<string, string>
        {
            [Misora] = "aftermath",
            [Aibako] = "promotional-emperor",
            [Mifuyu] = "aftermath",
            [Ryuko] = "gates-of-chaos",
            [Iguchi] = "promotional-emperor",
            [Ikariya] = "aftermath",
            [Nairuko] = "honor-and-treachery",
            [Gorobei] = "aftermath",
            [Izumiko] = "promotional-emperor",
            [Laoshe] = "gates-of-chaos",
            [Yokohime] = "emperor-edition",
            [Paikao] = "aftermath",
            [Kodanshi] = "seeds-of-decay",
            [Iabuchi] = "gates-of-chaos",
            [Sakiko] = "aftermath"
        };
        foreach (var row in sources.EnumerateArray())
            Check(row.GetProperty("image_path").GetString()!.Contains($"/oracle/{expected[row.GetProperty("card_id").GetString()!]}/"), $"{row.GetProperty("card_id")} comes from the reviewed printing's scan");
        Eq(sources.GetArrayLength(), 15, "one provenance row per card");

        // The published abilities exist with the printed timing, costs and slots.
        var ryuko = AbilityCatalog.All["tfp-ryuko-melee"]; Check(ryuko.Timing == "Battle" && ryuko.Costs!.Length == 1 && ryuko.Costs[0].Verb == CostVerb.Bow, "Ryuko's Battle, Bow cost");
        Check(AbilityCatalog.All["tfp-gorobei-bow"].Slots!.Select(x => x.Id).SequenceEqual(["discard", "target"]) && AbilityCatalog.All["tfp-paikao-weaken"].Slots!.Select(x => x.Id).SequenceEqual(["discard", "target"]), "discard slot first");
        Check(AbilityCatalog.All["tfp-laoshe-move"].Absent, "Lao-she's Home Battle may be taken from home");
        Check(CardCatalog.Get(Ninja).CreatedOnly, "the Ninja Kodanshi creates is the published created-only scan");
    }

    // ---- keyword-only cards ----

    private static void MisoraExpendable()
    {
        var s = Table(Phase.Battle, 0);
        var misora = Add(s, Misora, 1, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: destroyer.Id, TargetId: misora.Id, AbilityId: "training-destroy-no-attachments"));
        Eq(e.State.Cards[misora.Id].Zone, Zone.Dead, "Misora is destroyed");
        Eq(e.State.Players[1].Hand.Count, handBefore + 1, "her controller draws a card for the destruction (Expendable)");
        Check(e.State.Players[0].Hand.Count == 0, "the destroyer's controller does not");
        Check(CardCatalog.Get(Misora).Keywords!.Contains("Inquisitor") && CardCatalog.Get(Misora).Keywords!.Contains("Magistrate"), "Inquisitor and Magistrate are printed");
    }

    private static void AibakoConqueror()
    {
        var s = Table(Phase.Battle, 0);
        var aibako = Add(s, Aibako, 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        s.PriorityPlayerId = 1; // the Defender acts first; two passes resolve the battle
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(e.State.PendingChoice is null, "a single Conqueror needs no choice");
        Eq(e.State.Cards[aibako.Id].Zone, Zone.Home, "Aibako goes home after the battle");
        Check(!e.State.Cards[aibako.Id].Bowed, "a Conqueror's unit does not bow after battle");
        Check(e.State.Cards[other.Id].Bowed, "a non-Conqueror attacker bows");
    }

    private static void IguchiCavalry()
    {
        // Cavalry Maneuvers: a unit is Cavalry when the Personality and every attached Follower is. Iguchi (printed Cavalry, no rules text) assigns there; his Infantry-Follower twin does not.
        var s = Table(Phase.Action, 0);
        var iguchi = Add(s, Iguchi, 0, Zone.Home);
        Add(s, "training-cavalry-follower", 0, Zone.Attached, parent: iguchi.Id);
        var other = Add(s, Iguchi, 0, Zone.Home);
        Add(s, "training-follower", 0, Zone.Attached, parent: other.Id);
        var e = MatchEngine.FromTestState(s);
        var maneuvers = Batch336Support.Edit(e, st => { st.Phase = Phase.CavalryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.PendingChoice = null; });
        var attack = maneuvers.QueryActions(0).First(a => a.Type == "attack");
        Check(attack.TargetIds!.Contains(iguchi.Id) && !attack.TargetIds.Contains(other.Id), "Iguchi's Cavalry unit assigns in Cavalry Maneuvers; the unit with an Infantry Follower does not");
        var infantry = Batch336Support.Edit(maneuvers, st => st.Phase = Phase.InfantryDeclaration);
        var infantryAttack = infantry.QueryActions(0).First(a => a.Type == "attack");
        Check(infantryAttack.TargetIds!.Contains(iguchi.Id) && infantryAttack.TargetIds.Contains(other.Id), "in Infantry Maneuvers both may assign");
        Eq(CardCatalog.Get(Iguchi).Text, "", "no rules text is printed");
    }

    private static void SakikoKeywords()
    {
        var (e, sakiko) = Recruit(Sakiko);
        var handBefore = e.State.Players[0].Hand.Count;
        var honorBefore = Honor(e, 0);
        Accept(e, PurchaseFromProvince(e, 0, sakiko.Id));
        Eq(e.State.Cards[sakiko.Id].Zone, Zone.Home, "recruited");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "Destined: the entering card draws one");
        Eq(Honor(e, 0), honorBefore, "Sakiko has no Honor loss");
        var k = CardCatalog.Get(Sakiko).Keywords!;
        Check(k.Contains("Reserve") && k.Contains("Cavalry") && k.Contains("Destined") && k.Contains("Battle Maiden"), "Cavalry, Destined, Reserve and Battle Maiden are printed");
    }

    // ---- Mifuyu ----

    private static void MifuyuWeakens()
    {
        var s = Table(Phase.Battle, 0);
        var mifuyu = Add(s, Mifuyu, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "tfp-mifuyu-weaken", mifuyu.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(own.Id) && !option.TargetIds.Contains(mifuyu.Id), "only enemy Personalities are offered");
        Batch336Support.Rejection(e, Use(e, 0, "tfp-mifuyu-weaken", mifuyu.Id, follower.Id));
        Batch336Support.Rejection(e, Use(e, 0, "tfp-mifuyu-weaken", mifuyu.Id, own.Id));
        Eq(e.CardForceForTest(enemy.Id), 4, "rejections changed nothing");
        Accept(e, Use(e, 0, "tfp-mifuyu-weaken", mifuyu.Id, enemy.Id));
        Eq(e.CardForceForTest(enemy.Id), 1, "-3F");
        Check(!e.State.Cards[mifuyu.Id].Bowed, "no cost");
        Check(e.State.Cards[enemy.Id].Modifiers.Count == 1 && !e.State.Cards[enemy.Id].Modifiers[0].Permanent, "an until-end-of-turn modifier, not a permanent one (ruling R-001)");
    }

    // ---- Ryuko ----

    private static void RyukoEntryAndMelee()
    {
        var (e0, ryuko0) = Recruit(Ryuko);
        var handBefore = e0.State.Players[0].Hand.Count; var honorBefore = Honor(e0, 0);
        Accept(e0, PurchaseFromProvince(e0, 0, ryuko0.Id));
        Eq(e0.State.Players[0].Hand.Count, handBefore + 1, "Destined draws a card");
        Eq(Honor(e0, 0), honorBefore - 1, "After Ryuko enters play, lose 1 Honor");

        var s = Table(Phase.Battle, 0);
        var ryuko = Add(s, Ryuko, 0, Zone.Battlefield, province: 0);
        var strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "tfp-ryuko-melee", ryuko.Id).TargetIds!.Contains(strong.Id), "any enemy unit may be targeted");
        Accept(e, Use(e, 0, "tfp-ryuko-melee", ryuko.Id, strong.Id));
        Check(e.State.Cards[ryuko.Id].Bowed, "Bow is a cost");
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "Melee 2 does not reach Force 4");

        var s2 = Table(Phase.Battle, 0);
        var ryuko2 = Add(s2, Ryuko, 0, Zone.Battlefield, province: 0);
        var weak2 = Add(s2, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "tfp-ryuko-melee", ryuko2.Id, weak2.Id));
        Eq(e2.State.Cards[weak2.Id].Zone, Zone.Dead, "Melee 2 destroys Force 2");
        Check(e2.State.Cards[ryuko2.Id].Bowed, "bowed by the cost");

        var s3 = Table(Phase.Battle, 0);
        var bowed = Add(s3, Ryuko, 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s3, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "tfp-ryuko-melee", bowed.Id), "a bowed Ryuko cannot pay the cost");
    }

    // ---- Iabuchi ----

    private static void IabuchiRanged()
    {
        var (e0, iabuchi0) = Recruit(Iabuchi, s => s.Players[0].Honor = 10);
        var handBefore = e0.State.Players[0].Hand.Count;
        Accept(e0, PurchaseFromProvince(e0, 0, iabuchi0.Id));
        Eq(e0.State.Players[0].Hand.Count, handBefore + 1, "Destined draws a card");

        var s = Table(Phase.Battle, 0);
        var iabuchi = Add(s, Iabuchi, 0, Zone.Battlefield, province: 0);
        var strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "tfp-iabuchi-ranged", iabuchi.Id, strong.Id));
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "Ranged 2 does not reach Force 4");
        Check(e.State.Cards[iabuchi.Id].Bowed, "bowed as the cost");

        var s3 = Table(Phase.Battle, 0);
        var iabuchi3 = Add(s3, Iabuchi, 0, Zone.Battlefield, province: 0);
        var weak3 = Add(s3, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "tfp-iabuchi-ranged", iabuchi3.Id, weak3.Id));
        Eq(e3.State.Cards[weak3.Id].Zone, Zone.Dead, "Ranged 2 destroys Force 2");

        var s4 = Table(Phase.Battle, 0);
        var bowed = Add(s4, Iabuchi, 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s4, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "tfp-iabuchi-ranged", bowed.Id), "a bowed Iabuchi cannot use it");
    }

    // ---- Ikariya ----

    private static void IkariyaSpellAttack()
    {
        // Two Spells in her unit: Ranged 2 destroys a Force 2 Personality.
        var s = Table(Phase.Battle, 0);
        var ikariya = Add(s, Ikariya, 0, Zone.Battlefield, province: 0);
        Add(s, "training-spell", 0, Zone.Attached, province: 0, parent: ikariya.Id);
        Add(s, "training-spell", 0, Zone.Attached, province: 0, parent: ikariya.Id);
        var target = Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "tfp-ikariya-attack", ikariya.Id, target.Id));
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "strength 2 (two Spells) destroys Force 2");
        Check(!e.State.Cards[ikariya.Id].Bowed, "no bow cost is printed");

        // One Spell: strength 1 does not reach Force 2 but reaches Force 1.
        var s1 = Table(Phase.Battle, 0);
        var ik1 = Add(s1, Ikariya, 0, Zone.Battlefield, province: 0);
        Add(s1, "training-spell", 0, Zone.Attached, province: 0, parent: ik1.Id);
        var t2 = Add(s1, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var t1 = AddForce(Add(s1, "training-follower-payer", 1, Zone.Battlefield, province: 0), -1);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Use(e1, 0, "tfp-ikariya-attack", ik1.Id, t2.Id));
        Eq(e1.State.Cards[t2.Id].Zone, Zone.Battlefield, "strength 1 does not reach Force 2");
        var e1b = MatchEngine.FromTestState(s1);
        Accept(e1b, Use(e1b, 0, "tfp-ikariya-attack", ik1.Id, t1.Id));
        Eq(e1b.State.Cards[t1.Id].Zone, Zone.Dead, "strength 1 reaches Force 1");

        // No Spell in her unit (a Spell on another Personality does not count): strength 0 destroys nothing, even a Force 0 target.
        var s0 = Table(Phase.Battle, 0);
        var ik0 = Add(s0, Ikariya, 0, Zone.Battlefield, province: 0);
        var friend = Add(s0, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s0, "training-spell", 0, Zone.Attached, province: 0, parent: friend.Id);
        var zero = AddForce(Add(s0, "training-follower-payer", 1, Zone.Battlefield, province: 0), -2);
        var e0 = MatchEngine.FromTestState(s0);
        Accept(e0, Use(e0, 0, "tfp-ikariya-attack", ik0.Id, zero.Id));
        Eq(e0.State.Cards[zero.Id].Zone, Zone.Battlefield, "no Spells in her unit: strength 0 destroys nothing");
    }

    // ---- Gorobei ----

    private static void GorobeiDiscardBow()
    {
        var s = Table(Phase.Battle, 0);
        var gorobei = Add(s, Gorobei, 0, Zone.Battlefield, province: 0);
        var focus3 = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus Value 3
        var focus1 = Add(s, "training-stat-cost-low-strategy", 0, Zone.Hand); // Focus Value 1
        var three = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var four = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var two = Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0); // Force 2
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Enabled(e, 0, "tfp-gorobei-bow", gorobei.Id);
        // Force 4 is above Focus 3; a Force 2 above Focus 1; an own card and a missing discard are illegal. Each rejection leaves the table untouched.
        Batch336Support.Rejection(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, Pick("discard", focus3.Id), Pick("target", four.Id)));
        Batch336Support.Rejection(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, Pick("discard", focus1.Id), Pick("target", two.Id)));
        Batch336Support.Rejection(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, Pick("discard", focus3.Id), Pick("target", own.Id)));
        Batch336Support.Rejection(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, Pick("target", three.Id)));
        Check(e.State.Cards[focus3.Id].Zone == Zone.Hand && e.State.Cards[focus1.Id].Zone == Zone.Hand && !e.State.Cards[three.Id].Bowed && !e.State.Cards[four.Id].Bowed, "nothing was discarded or bowed");
        Accept(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, Pick("discard", focus3.Id), Pick("target", three.Id)));
        Check(e.State.Cards[three.Id].Bowed, "the Force 3 target is bowed (Focus 3)");
        Eq(e.State.Cards[focus3.Id].Zone, Zone.FateDiscard, "the discarded card is discarded as the cost");
        Check(!e.State.Cards[four.Id].Bowed, "nothing else bows");

        // Focus 1 reaches only Force 1 or lower.
        var s2 = Table(Phase.Battle, 0);
        var g2 = Add(s2, Gorobei, 0, Zone.Battlefield, province: 0);
        var low = Add(s2, "training-stat-cost-low-strategy", 0, Zone.Hand);
        var tiny = AddForce(Add(s2, "training-follower-payer", 1, Zone.Battlefield, province: 0), -1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "tfp-gorobei-bow", g2.Id, null, Pick("discard", low.Id), Pick("target", tiny.Id)));
        Check(e2.State.Cards[tiny.Id].Bowed, "Focus 1 bows a Force 1 target");

        // Nothing in hand: the action is not offered enabled.
        var s3 = Table(Phase.Battle, 0);
        var g3 = Add(s3, Gorobei, 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "tfp-gorobei-bow", g3.Id), "without a card to discard there is nothing to do");
    }

    // ---- Paikao ----

    private static void PaikaoDiscardPenalty()
    {
        var s = Table(Phase.Battle, 0);
        var paikao = Add(s, Paikao, 0, Zone.Battlefield, province: 0);
        var card = Add(s, "training-focus-strategy", 0, Zone.Hand); // Focus Value 3
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Enabled(e, 0, "tfp-paikao-weaken", paikao.Id);
        Batch336Support.Rejection(e, Use(e, 0, "tfp-paikao-weaken", paikao.Id, null, Pick("discard", card.Id), Pick("target", own.Id)));
        Batch336Support.Rejection(e, Use(e, 0, "tfp-paikao-weaken", paikao.Id, null, Pick("target", enemy.Id)));
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "a rejected action discards nothing");
        Eq(e.CardForceForTest(enemy.Id), 4);
        Accept(e, Use(e, 0, "tfp-paikao-weaken", paikao.Id, null, Pick("discard", card.Id), Pick("target", enemy.Id)));
        Eq(e.CardForceForTest(enemy.Id), 1, "a Force penalty equal to the discarded card's Focus Value (3)");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the card is discarded");

        var s2 = Table(Phase.Battle, 0);
        var p2 = Add(s2, Paikao, 0, Zone.Battlefield, province: 0);
        var card2 = Add(s2, "training-stat-cost-low-strategy", 0, Zone.Hand); // Focus Value 1
        var host2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower2 = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: host2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "tfp-paikao-weaken", p2.Id, null, Pick("discard", card2.Id), Pick("target", follower2.Id)));
        Eq(e2.CardForceForTest(follower2.Id), 1, "a Follower is a legal target; Focus 1 gives -1F (Force 2 becomes 1)");
    }

    // ---- Lao-she ----

    private static void LaosheHomeBattle()
    {
        // At home, no unit of his own at the battle, enemy units present: offered, and moves him to the battlefield.
        var s = Table(Phase.Battle, 0);
        var laoshe = Add(s, Laoshe, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "tfp-laoshe-move", laoshe.Id).Command!);
        Eq(e.State.Cards[laoshe.Id].Zone, Zone.Battlefield, "he is moved to the current battlefield");
        Eq(e.State.Cards[laoshe.Id].ProvinceIndex, 0);
        Check(!e.State.Cards[laoshe.Id].Bowed, "no bow is printed");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield);

        // No enemy units at the battlefield: he would not be opposed; the action is refused (and unchanged).
        var s2 = Table(Phase.Battle, 0);
        var l2 = Add(s2, Laoshe, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "tfp-laoshe-move", l2.Id), "not opposed: not offered");
        Batch336Support.Rejection(e2, new GameCommand("useAbility", 0, e2.Version, CardId: l2.Id, AbilityId: "tfp-laoshe-move"));
        Eq(e2.State.Cards[l2.Id].Zone, Zone.Home);

        // Bowed: his abilities are unavailable.
        var s3 = Table(Phase.Battle, 0);
        var l3 = Add(s3, Laoshe, 0, Zone.Home, bowed: true);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "tfp-laoshe-move", l3.Id), "bowed: not offered");

        // Outside a battle it is not a Home Battle action at all.
        var s4 = Table(Phase.Action, 0);
        var l4 = Add(s4, Laoshe, 0, Zone.Home);
        Add(s4, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "tfp-laoshe-move", l4.Id), "a Battle action: only in a battle");
    }

    // ---- Yokohime ----

    private static void YokohimeBow()
    {
        var s = Table(Phase.Battle, 0);
        var yokohime = Add(s, Yokohime, 0, Zone.Battlefield, province: 0);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "masumi-bow", yokohime.Id);
        Check(option.TargetIds!.Contains(bare.Id) && !option.TargetIds.Contains(armed.Id), "only an enemy card without attachments is offered");
        Batch336Support.Rejection(e, Use(e, 0, "masumi-bow", yokohime.Id, armed.Id));
        Accept(e, Use(e, 0, "masumi-bow", yokohime.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Bowed && !e.State.Cards[armed.Id].Bowed, "the targeted card bows");
        Check(!e.State.Cards[yokohime.Id].Bowed, "no cost is printed");
    }

    // ---- Nairuko ----

    private static void NairukoForce()
    {
        var s = Table(Phase.Action, 0);
        var nairuko = Add(s, Nairuko, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(nairuko.Id), 2, "printed 2F without a Spell");
        var s2 = Table(Phase.Action, 0);
        var n2 = Add(s2, Nairuko, 0, Zone.Home);
        Add(s2, "training-spell", 0, Zone.Attached, parent: n2.Id);
        Eq(MatchEngine.FromTestState(s2).CardForceForTest(n2.Id), 4, "+2F while she has a Spell");
        var s3 = Table(Phase.Action, 0);
        var n3 = Add(s3, Nairuko, 0, Zone.Home);
        var friend = Add(s3, "training-crane-samurai", 0, Zone.Home);
        Add(s3, "training-spell", 0, Zone.Attached, parent: friend.Id);
        Eq(MatchEngine.FromTestState(s3).CardForceForTest(n3.Id), 2, "a Spell on another Personality does not count");
    }

    // ---- Izumiko ----

    private static (MatchEngine Engine, CardInstance Izumiko, CardInstance Enemy) DuelTable(bool izumikoChallenges, int enemyChiBonus)
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 1; s.ActivePlayerId = 1; // player 1 issues the challenge either way
        var izumiko = Add(s, Izumiko, izumikoChallenges ? 1 : 0, Zone.Battlefield, province: 0); // Chi 3, Duelist
        var enemy = Add(s, "training-crane-samurai", izumikoChallenges ? 0 : 1, Zone.Battlefield, province: 0); // Chi 3, no Duelist
        if (enemyChiBonus != 0) enemy.Modifiers.Add(new("test-source", "test", "Chi", enemyChiBonus));
        Add(s, "training-duel-challenge", 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), izumiko, enemy);
    }

    private static void RunDuel(MatchEngine e, CardInstance challenger, CardInstance challenged)
    {
        var challenge = e.State.Cards.Values.Single(c => c.DefinitionId == "training-duel-challenge");
        Accept(e, new GameCommand("useAbility", challenger.OwnerId, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", challenged.OwnerId, e.Version));
        Accept(e, new GameCommand("strike", challenged.OwnerId, e.Version));
        // A Duelist may re-focus before the Focus Effects: decline it.
        while (e.State.PendingDuel is not null)
        {
            var actor = e.State.PriorityPlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command?.Type is "duelistRefocus" or "strike");
            Check(option is not null, "the duel's remaining step is offered");
            Accept(e, option!.Command!);
        }
    }

    private static void IzumikoDuelHonor()
    {
        // Izumiko challenges a Chi-equal non-Duelist: the tie is hers (Duelist) and she gains 1 Honor.
        var (e, izumiko, enemy) = DuelTable(true, 0);
        var before = (Honor(e, 0), Honor(e, 1));
        RunDuel(e, izumiko, enemy);
        Eq(e.State.Cards[izumiko.Id].Zone, Zone.Battlefield, "she wins the tied duel");
        Eq(Honor(e, 1), before.Item2 + 1, "After Izumiko wins a duel, gain 1 Honor");
        Eq(Honor(e, 0), before.Item1, "her opponent's Honor is unchanged");

        // She is challenged and wins: still her win.
        var (e2, izumiko2, enemy2) = DuelTable(false, 0);
        var b2 = Honor(e2, 0);
        RunDuel(e2, enemy2, izumiko2);
        Eq(Honor(e2, 0), b2 + 1, "winning as the challenged player counts too");

        // She loses (the enemy has +3 Chi): no Honor.
        var (e3, izumiko3, enemy3) = DuelTable(true, 3);
        var b3 = Honor(e3, 1);
        RunDuel(e3, izumiko3, enemy3);
        Eq(Honor(e3, 1), b3, "a lost duel gains nothing");

        // Another Duelist-less Personality of her controller winning a duel gives her nothing.
        var s4 = Table(Phase.Battle, 1);
        s4.PriorityPlayerId = 1; s4.ActivePlayerId = 1;
        Add(s4, Izumiko, 1, Zone.Battlefield, province: 0);
        var other = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var foe = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        foe.Modifiers.Add(new("test-source", "test", "Chi", -2));
        Add(s4, "training-duel-challenge", 1, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        var b4 = Honor(e4, 1);
        RunDuel(e4, other, foe);
        Eq(Honor(e4, 1), b4, "only Izumiko's own win gains Honor");
    }

    // ---- Kodanshi ----

    private static void KodanshiCreatesNinja()
    {
        var (e, kodanshi) = Recruit(Kodanshi);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, PurchaseFromProvince(e, 0, kodanshi.Id));
        Eq(e.State.Cards[kodanshi.Id].Zone, Zone.Home, "Kodanshi entered play");
        var ninjas = e.State.Cards.Values.Where(c => c.DefinitionId == Ninja).ToArray();
        Eq(ninjas.Length, 1, "exactly one Ninja is created");
        var ninja = ninjas[0];
        Check(ninja.OwnerId == 0 && ninja.Zone == Zone.Home && ninja.FaceUp, "created for her controller, in play at home");
        var def = ninja.Printed ?? CardCatalog.Get(ninja.DefinitionId);
        Check(def.Force == 0 && def.Chi == 2 && def.PersonalHonor == 0 && def.Type == CardType.Personality, "0F/2C/0PH Personality");
        Check(def.Keywords!.Contains("Scorpion Clan") && def.Keywords.Contains("Ninja") && def.Clan == "Scorpion", "a Scorpion Clan Ninja, as the created card's scan prints");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "entryReaction", "mandatory: no optional Reaction window is opened for it");
        Eq(e.State.Players[0].Hand.Count, handBefore, "Kodanshi has no Destined draw");

        // Recruited by Shinden Shorai (an ability that brings a face-up province Personality into play) she also enters play from a province.
        var (shorai, command) = GoldParityTests.ShindenShorai(Kodanshi);
        var provinceCard = shorai.State.Players[0].Provinces[0].CardId!;
        Accept(shorai, command(shorai.QuotePurchase(0, provinceCard, ignoreHonorRequirementIfPersonalityClan: "Lion").Payments[0].SourceIds));
        Eq(shorai.State.Cards[provinceCard].Zone, Zone.Home, "Kodanshi was recruited by the ability");
        Eq(shorai.State.Cards.Values.Count(c => c.DefinitionId == Ninja && c.OwnerId == 0 && c.Zone == Zone.Home), 1, "and created her Ninja");
        // A Kodanshi that did not enter from a province (placed in play directly) creates nothing and no entry effect fires.
        var s = Table(Phase.Action, 0);
        Add(s, Kodanshi, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s).State.Cards.Values.Count(c => c.DefinitionId == Ninja), 0, "no Ninja unless she enters from a province");
    }
}
