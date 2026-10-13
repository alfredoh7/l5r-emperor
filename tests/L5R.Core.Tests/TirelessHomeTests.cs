using L5R.Core;
using static RegressionAssertions;

// Issue #672, primitive P16 (TirelessHome.cs): Tireless and Home as ability data, a granted Home, and the six demonstration cards of personality-wave-129, spell-wave-54 and
// item-wave-47. Driven through QueryActions and the commands the engine offers, on explicit positions (Batch336Support.Table: province 0 is the current battlefield).
internal static class TirelessHomeTests
{
    private const string Obayashi = "oracle-l5r-10877", Sasako = "oracle-l5r-10643", Kinto = "oracle-l5r-10674", Kanpeki = "oracle-l5r-10876", Steed = "oracle-l5r-10705", Satchel = "oracle-l5r-10526";
    // Published Spells of other waves, used as subjects of the granted Home: Conflagration ("Battle: Bow this card: Give this Shugenja +3F") and Kiyoshi's Wrath.
    private const string Conflagration = "oracle-l5r-1449", KiyoshisWrath = "oracle-l5r-10701";
    private const string ConflagrationBoost = "tf-conflagration-boost", KiyoshiBow = "tf-kiyoshi-bow";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 0) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, null, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static string Reasons(MatchEngine e, int player, string ability, string? cardId = null) =>
        string.Join("; ", Batch336Support.Options(e, player, ability, cardId).Select(o => $"[{o.Enabled}] {o.Reason}"));

    public static void Run(TestRunner runner) => runner.Run("tireless-home", [
        ("Published as printed: stats, keywords, printed text, abilities, entry Honor loss, manifests and art of the six cards; the flags are on their definitions", PublishedShape),
        ("Tireless is data: the flag is offered while its card is bowed, an unflagged action on a bowed card is not, and the flag waives nothing else", TirelessIsTheFlag),
        ("Tireless does not pay a bow cost: a flagged action with 'bow this card' pays the bow when unbowed and is refused, state unchanged, when bowed", TirelessDoesNotPayABowCost),
        ("The older 'even if bowed' abilities still validate while bowed, and none of them was converted to the flag", OlderTirelessIdsStillValidate),
        ("Goju Obayashi: entering play loses 2 Honor; Ninja Tireless Battle moves him home even while bowed, once a turn, and only from the battlefield", ObayashiMovesHome),
        ("Moshi Sasako - exp: Tireless Battle straightens her while bowed at the battlefield; not from home, and a negated action straightens nothing", SasakoStraightens),
        ("Shinjo Kinto - exp: a Home action is offered from home with presence and not without; 'if he would be opposed' moves him in with no presence; one use covers both destinations", KintoHomeBattle),
        ("Shinjo Kinto - exp: Home is 'at home', not 'anywhere' -- from another battlefield the move home is refused; bowed he may take neither", KintoHomeIsNotAnywhere),
        ("Daigotsu Kanpeki - exp2: Tireless Open takes the Imperial Favor while he is bowed, once a turn, in the Action phase only", KanpekiTakesTheFavor),
        ("My Ancestor's Steed: from home, with no unit at the battle, bow it to move your own Spirit Personality from any location in; enemy Spirits and non-Spirits are not targets", SteedMovesASpirit),
        ("My Ancestor's Steed: the bow is a cost -- a bowed Steed cannot be used, and a negated action still bows it and moves nobody", SteedCostAndNegation),
        ("Koan's Scroll Satchel: gives Home to Spells in its own unit, up to the number of Koan cards there; too many, or a Spell of another unit, is refused unchanged", SatchelGivesHome),
        ("A granted Home lets the Spell act from home (with presence, not without), leaves the other Spell alone, and lapses when the turn ends", GrantedHomeActsAndLapses),
    ]);

    // ---- helpers ----

    private static GameCommand PurchaseFromProvince(MatchEngine e, int player, string cardId) =>
        new("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds);

    private static CardInstance Keyworded(CardInstance card, string keyword) { card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent)); return card; }

    private static string Shugenja() =>
        CardCatalog.Cards.Values.Where(c => c.Type == CardType.Personality && !c.ScenarioOnly && (c.Abilities ?? []).Length == 0 && c.Effect == "none" && c.Keywords?.Contains("Shugenja") == true)
            .OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;

    private static bool HasHome(MatchEngine e, string cardId) => e.State.Cards[cardId].GrantedKeywords.Any(g => g.Keyword == "Home");

    // ---- published shape ----

    private static void PublishedShape()
    {
        foreach (var (wave, ids) in new[] { ("personality-wave-129", new[] { Obayashi, Sasako, Kinto, Kanpeki }), ("spell-wave-54", new[] { Steed }), ("item-wave-47", new[] { Satchel }) })
        {
            var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(new HashSet<string>(listed).SetEquals(ids) && listed.Length == ids.Length, $"{wave}'s manifest is exactly its cards");
            foreach (var id in ids) Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        }

        void Is(string id, CardType type, string name, string clan, int force, int chi, int ph, int gold, int focus, string[] keywords, string text, string[] abilities, int? entryLoss = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == type && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Type}, {c.Name}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.Focus == focus && c.HonorRequirement is null, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} focus {c.Focus} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Eq(c.Text, text, $"{name}: printed text");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, "none", $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
        }
        Is(Obayashi, CardType.Personality, "Goju Obayashi", "Spider", 4, 1, 0, 6, 0, ["Spider Clan", "Ninja", "Shadowlands"],
            "After Obayashi enters play, lose 2 Honor.\nNinja Tireless Battle: Move Obayashi home. (You may use Tireless abilities even if bowed.)", ["obayashi-move-home"], 2);
        Is(Sasako, CardType.Personality, "Moshi Sasako", "Mantis", 4, 4, 1, 10, 0, ["Mantis Clan", "Thunder", "Conqueror", "Experienced", "Naval", "Shugenja", "Unique"],
            "Tireless Battle: Straighten Sasako. (Tireless actions may be taken while bowed.)", ["sasako-exp-straighten"]);
        Is(Kinto, CardType.Personality, "Shinjo Kinto", "Unicorn", 4, 3, 2, 9, 0, ["Unicorn Clan", "Commander of the 9th Legion", "Commander", "Imperial", "Samurai", "Scout", "Cavalry", "Experienced", "Unique"],
            "Home Battle: Move Kinto home or, if he would be opposed, to the current battlefield. (Home actions may be taken from home.)", ["kinto-exp-move-home", "kinto-exp-move-battle"]);
        Is(Kanpeki, CardType.Personality, "Daigotsu Kanpeki", "Spider", 6, 5, 0, 14, 0, ["Spider Clan", "Clan Champion", "Monk", "Paragon", "Samurai", "Conqueror", "Experienced 2", "Kensai", "Loyal", "Unique"],
            "Tireless Open: Take the Imperial Favor. (You may use Tireless abilities even if bowed.)", ["kanpeki-exp2-favor"]);
        Is(Steed, CardType.Spell, "My Ancestor's Steed", CardCatalog.Get(Steed).Clan, 0, 0, 0, 2, 1, ["Water"],
            "Home Water Battle, Bow: Move your target Spirit Personality at any location to the current battlefield. (Home actions may be taken from home.)", ["ancestors-steed-move"]);
        Is(Satchel, CardType.Item, "Koan's Scroll Satchel", CardCatalog.Get(Satchel).Clan, 0, 0, 0, 4, 3, ["Koan", "Unique"],
            "Open, Bow: Give Home to one or more Spells in this unit, up to the number of Koan cards in this unit. (Home actions may be used from home.)", ["koan-satchel-home"]);

        // The flags are data on the definitions, as printed.
        var all = AbilityCatalog.All;
        Check(all["obayashi-move-home"] is { Timing: "Battle", Tireless: true, Home: false, MovesToBattle: false } && all["obayashi-move-home"].ActionKeywords!.SequenceEqual(["Ninja"]), "Obayashi: Ninja Tireless Battle");
        Check(all["sasako-exp-straighten"] is { Timing: "Battle", Tireless: true, Home: false }, "Sasako: Tireless Battle");
        Check(all["kinto-exp-move-home"] is { Timing: "Battle", Home: true, Tireless: false, MovesToBattle: false, UsageKey: "kinto-exp-move" }, "Kinto (home): Home Battle");
        Check(all["kinto-exp-move-battle"] is { Timing: "Battle", Home: true, Tireless: false, MovesToBattle: true, UsageKey: "kinto-exp-move" }, "Kinto (battlefield): Home Battle, a move to the battle");
        Check(all["kanpeki-exp2-favor"] is { Timing: "Open", Tireless: true, Home: false }, "Kanpeki: Tireless Open");
        Check(all["ancestors-steed-move"] is { Timing: "Battle", Home: true, Tireless: false, MovesToBattle: true } && all["ancestors-steed-move"].ActionKeywords!.SequenceEqual(["Water"]), "My Ancestor's Steed: Home Water Battle");
        Check(all["koan-satchel-home"] is { Timing: "Open", Home: false, Tireless: false }, "Koan's Scroll Satchel: Open");
    }

    // ---- Tireless ----

    private static void TirelessIsTheFlag()
    {
        // The same position twice: a bowed Personality at the battlefield. The flagged action is offered; the unflagged Home action of a bowed Kinto is not.
        var s = Table();
        var sasako = Add(s, Sasako, 0, Zone.Battlefield, bowed: true, province: 0);
        var kinto = Add(s, Kinto, 0, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "sasako-exp-straighten", sasako.Id), "a Tireless action is offered while its card is bowed: " + Reasons(e, 0, "sasako-exp-straighten", sasako.Id));
        Check(!CanUse(e, 0, "kinto-exp-move-home", kinto.Id), "an action without Tireless is not offered while its card is bowed");
        Check(Rejection(e, Use(e, 0, "kinto-exp-move-home", kinto.Id)).Contains("bowed"), "and the reason is the bowed card");
        Check(e.State.Cards[kinto.Id] is { Zone: Zone.Battlefield, Bowed: true }, "the refused action changed nothing");

        // Tireless is not Home: bowed at home, a Tireless Battle action is still not taken away from the battlefield.
        var s2 = Table();
        var home = Add(s2, Sasako, 0, Zone.Home, bowed: true);
        Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "sasako-exp-straighten", home.Id), "Tireless does not waive the battlefield");
        Check(Rejection(e2, Use(e2, 0, "sasako-exp-straighten", home.Id)).Contains("battlefield"), "the reason is her location");

        // Tireless is not a timing: outside the Battle phase a Tireless Battle action is not offered.
        var s3 = Table(Phase.Action);
        var idle = Add(s3, Sasako, 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "sasako-exp-straighten", idle.Id), "a Battle action is taken in the Battle phase");
    }

    private static void TirelessDoesNotPayABowCost()
    {
        const string fixture = TirelessHomeAbilities.TrainingTirelessBow;
        MatchEngine Rig(bool bowed, out CardInstance card)
        {
            var s = Table();
            card = Add(s, "training-crane-veteran", 0, Zone.Battlefield, bowed: bowed, province: 0);
            card.GrantedAbilities.Add(new(fixture, "test-fixture", GrantDuration.Permanent));
            return MatchEngine.FromTestState(s);
        }
        Check(AbilityCatalog.All[fixture] is { Tireless: true } && AbilityCatalog.All[fixture].Costs!.Single() is { Verb: CostVerb.Bow, Subject: CostSubject.Source }, "the fixture is Tireless with a 'bow this card' cost");

        var e = Rig(bowed: false, out var ready);
        var force = e.CardForceForTest(ready.Id);
        Accept(e, Enabled(e, 0, fixture, ready.Id).Command!);
        Check(e.State.Cards[ready.Id].Bowed, "unbowed: the printed bow cost is paid");
        Eq(e.CardForceForTest(ready.Id), force + 1, "and the effect happened");

        var e2 = Rig(bowed: true, out var spent);
        Check(!CanUse(e2, 0, fixture, spent.Id), "bowed: a bowed card cannot pay a bow cost, Tireless or not");
        var version = e2.Version;
        Check(Rejection(e2, Use(e2, 0, fixture, spent.Id)).Contains("cannot be bowed to pay"), "the reason is the cost");
        Eq(e2.Version, version, "a refused command leaves the state alone");
        Check(e2.State.Cards[spent.Id].Modifiers.Count == 0 && e2.State.Cards[spent.Id].AbilityUses.Count == 0, "no effect, no use spent");
    }

    private static void OlderTirelessIdsStillValidate()
    {
        // The id list of ValidateAbility's bowed-source rule is left in place beside the flag: every id it named before issue #672 is still named there...
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/L5R.Core/CardAbilities.cs"));
        var start = source.IndexOf("if (!(!source.Bowed || id is \"shinden-choice\"", StringComparison.Ordinal); // ValidateAbilityResult returns the refusal (issue #890)
        var end = source.IndexOf("\"Abilities on bowed cards cannot be used.\"", start, StringComparison.Ordinal);
        Check(start > 0 && end > start, "the bowed-source rule located");
        var rule = source[start..end];
        string[] frozen = ["shinden-choice", "tsudoken-exp-bow", "tsudoken-exp-straighten", "tsudoken-exp2-straighten", "houken-exp-straighten", "michi-move", "rikyou-move", "fubatsu-plate-straighten",
            "fubatsu-plate-move", "nikutai-straighten", "hawado-move-home", "hawado-move-battlefield", "dairuko-rally", "tsunami-straighten", "red-crane-dojo-ninja", "chukage-move", "dbc-attacker-bow",
            "utoro-straighten", "chikata-bow", "chikata-straighten", "tsubo-straighten", "rokku-move", "vengeful-straighten", "osawa-bow", "osawa-straighten", "celestial-sword-crab-straighten",
            "natsumi-move", "small-time-bully-dishonor", "kimi-exp2-discard", "palace-dawn-boost", "temple-purity-weaken", "aerie-duel"];
        foreach (var id in frozen)
        {
            Check(rule.Contains($"\"{id}\"", StringComparison.Ordinal), $"{id} is still named by the bowed-source rule");
            Check(AbilityCatalog.All.TryGetValue(id, out var ability) && !ability.Tireless, $"{id} exists and was not converted to the flag");
        }
        foreach (var helper in new[] { "EmperorStrongholdBatch3BowedOk(id)", "StrongholdBatch1EvenIfBowed(id)", "EmperorRemainingFBowedOk(id)", "FollowupsEvenIfBowed(id)", "MechAEvenIfBowed(id)", "MechEEvenIfBowed(id)",
            "IvoryPoliticalEvenIfBowed(id)", "PromoEmperorEvenIfBowed(id)", "PromoItemsEvenIfBowed(id)", "TirelessStronghold(source)", "UnsafePassageWaives(command, source)", "ability.Tireless" })
            Check(rule.Contains(helper, StringComparison.Ordinal), $"{helper} is still read by the bowed-source rule");

        // ...and still behaves: Kakita Michi ("Even if bowed: if opposed, move Michi to the current battlefield and straighten his unit") moves in while bowed,
        var michi = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.Abilities?.Contains("michi-move") == true).Id;
        var s = Table();
        var bowedMichi = Add(s, michi, 0, Zone.Home, bowed: true);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "michi-move", bowedMichi.Id), "michi-move is offered while he is bowed: " + Reasons(e, 0, "michi-move", bowedMichi.Id));
        Accept(e, Enabled(e, 0, "michi-move", bowedMichi.Id).Command!);
        Check(e.State.Cards[bowedMichi.Id] is { Zone: Zone.Battlefield, Bowed: false }, "he moved in and straightened");

        // and Akodo Tsudoken - exp2 ("Tireless: Straighten your target Lion Clan or Fallen Personality") straightens a Lion while bowed.
        var tsudoken = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.Abilities?.Contains("tsudoken-exp2-straighten") == true).Id;
        var s2 = Table(Phase.Action);
        var bowedTsudoken = Add(s2, tsudoken, 0, Zone.Home, bowed: true);
        var lion = Add(s2, "training-lion-samurai", 0, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Batch336Support.Offer(e2, 0, "tsudoken-exp2-straighten", bowedTsudoken.Id, lion.Id));
        Check(!e2.State.Cards[lion.Id].Bowed && e2.State.Cards[bowedTsudoken.Id].Bowed, "the Lion is straightened; Tsudoken stays bowed");
    }

    // ---- Goju Obayashi ----

    private static void ObayashiMovesHome()
    {
        // Entering play from a province: lose 2 Honor.
        var entry = MatchEngine.CreateEconomyScenario().State;
        var provinceCard = entry.Players[0].Provinces[0].CardId!;
        entry.Cards[provinceCard].DefinitionId = Obayashi; entry.Cards[provinceCard].FaceUp = true;
        var e0 = MatchEngine.FromTestState(entry);
        var honor = e0.State.Players[0].Honor;
        Accept(e0, PurchaseFromProvince(e0, 0, provinceCard));
        Eq(e0.State.Players[0].Honor, honor - 2, "After Obayashi enters play, lose 2 Honor");

        // Bowed at the battlefield: the Tireless action is offered, moves him home (still bowed), and is spent for the turn.
        var s = Table();
        var obayashi = Add(s, Obayashi, 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "obayashi-move-home", obayashi.Id).Command!);
        Check(e.State.Cards[obayashi.Id] is { Zone: Zone.Home, Bowed: true }, "he is moved home, and stays bowed (no straighten is printed)");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "unitMovedHome" && ev.CardId == obayashi.Id), "the move is reported");
        // Once per turn: back at the battlefield with priority, the action is spent.
        var again = Batch336Support.Edit(e, st => { st.Cards[obayashi.Id].Zone = Zone.Battlefield; st.Cards[obayashi.Id].ProvinceIndex = 0; st.PriorityPlayerId = 0; });
        Check(!CanUse(again, 0, "obayashi-move-home", obayashi.Id), "once per turn");
        Check(Rejection(again, Use(again, 0, "obayashi-move-home", obayashi.Id)).Contains("already been used"), "the reason is the limit");
        Eq(again.State.Cards[obayashi.Id].Zone, Zone.Battlefield, "the refused action moved nobody");

        // Unbowed it works the same; from home it is not a Home action.
        var s2 = Table();
        var ready = Add(s2, Obayashi, 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "obayashi-move-home", ready.Id).Command!);
        Check(e2.State.Cards[ready.Id] is { Zone: Zone.Home, Bowed: false }, "unbowed: moved home unbowed");

        var s3 = Table();
        var atHome = Add(s3, Obayashi, 0, Zone.Home, bowed: true);
        Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "obayashi-move-home", atHome.Id), "Tireless is not Home: at home it is not offered");

        // A negated action moves nobody; the use is spent.
        var s4 = Table();
        var held = Add(s4, Obayashi, 0, Zone.Battlefield, bowed: true, province: 0);
        var e4 = MatchEngine.FromTestState(s4); e4.NegateEveryAction = true;
        Accept(e4, Use(e4, 0, "obayashi-move-home", held.Id));
        Eq(e4.State.Cards[held.Id].Zone, Zone.Battlefield, "negated: he does not move");
    }

    // ---- Moshi Sasako - exp ----

    private static void SasakoStraightens()
    {
        var s = Table();
        var sasako = Add(s, Sasako, 0, Zone.Battlefield, bowed: true, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "sasako-exp-straighten", sasako.Id);
        Check(option.TargetIds is null or { Length: 0 }, "it names no target");
        Accept(e, option.Command!);
        Check(!e.State.Cards[sasako.Id].Bowed, "Straighten Sasako");
        Eq(e.State.Cards[sasako.Id].Zone, Zone.Battlefield, "she stays at the battlefield");
        // Once per turn: bowed again, the action is spent.
        var again = Batch336Support.Edit(e, st => { st.Cards[sasako.Id].Bowed = true; st.PriorityPlayerId = 0; });
        Check(!CanUse(again, 0, "sasako-exp-straighten", sasako.Id), "once per turn");
        Check(Rejection(again, Use(again, 0, "sasako-exp-straighten", sasako.Id)).Contains("already been used"), "the reason is the limit");
        Check(again.State.Cards[sasako.Id].Bowed, "the refused action straightened nothing");

        // Negated: nothing is straightened.
        var s2 = Table();
        var held = Add(s2, Sasako, 0, Zone.Battlefield, bowed: true, province: 0);
        var e2 = MatchEngine.FromTestState(s2); e2.NegateEveryAction = true;
        Accept(e2, Use(e2, 0, "sasako-exp-straighten", held.Id));
        Check(e2.State.Cards[held.Id].Bowed, "negated: she stays bowed");
    }

    // ---- Shinjo Kinto - exp ----

    private static void KintoHomeBattle()
    {
        // At home, another unit of his player's at the battlefield (presence), no enemy there: "move home" is a Home action he may take from home; he would not be opposed.
        var s = Table();
        var kinto = Add(s, Kinto, 0, Zone.Home);
        Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "kinto-exp-move-home", kinto.Id), "a Home action is offered from home during a battle, with presence: " + Reasons(e, 0, "kinto-exp-move-home", kinto.Id));
        Check(!CanUse(e, 0, "kinto-exp-move-battle", kinto.Id), "no enemy unit: he would not be opposed");
        Check(Rejection(e, Use(e, 0, "kinto-exp-move-battle", kinto.Id)).Contains("would not be opposed"), "the reason is the printed condition");
        Eq(e.State.Cards[kinto.Id].Zone, Zone.Home);
        Accept(e, Enabled(e, 0, "kinto-exp-move-home", kinto.Id).Command!);
        Eq(e.State.Cards[kinto.Id].Zone, Zone.Home, "already home: nothing moves");
        Check(!e.GetView(0).Events.Any(ev => ev.Type == "unitMovedHome" && ev.CardId == kinto.Id), "and no movement is reported");

        // At home, NO unit of his player's at the battlefield: Home does not waive presence, so the move home is not offered...
        var s2 = Table();
        var alone = Add(s2, Kinto, 0, Zone.Home);
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "kinto-exp-move-home", alone.Id), "a Home action is not offered without presence");
        Check(Rejection(e2, Use(e2, 0, "kinto-exp-move-home", alone.Id)).Contains("unit at the current battlefield"), "the reason is the missing unit");
        // ...but the other half moves him to the battle, which needs none (a move to the battle), and he would be opposed.
        Accept(e2, Enabled(e2, 0, "kinto-exp-move-battle", alone.Id).Command!);
        Check(e2.State.Cards[alone.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 0, Bowed: false }, "he is moved to the current battlefield");
        Eq(e2.State.Cards[enemy.Id].Zone, Zone.Battlefield);
        // One printed action: the use covers both destinations.
        var later = Batch336Support.Edit(e2, st => st.PriorityPlayerId = 0);
        Check(!CanUse(later, 0, "kinto-exp-move-home", alone.Id) && !CanUse(later, 0, "kinto-exp-move-battle", alone.Id), "one use per turn covers both destinations");
        Check(Rejection(later, Use(later, 0, "kinto-exp-move-home", alone.Id)).Contains("already been used"), "the reason is the limit");
        Eq(later.State.Cards[alone.Id].Zone, Zone.Battlefield);

        // At the battlefield: he moves home.
        var s3 = Table();
        var there = Add(s3, Kinto, 0, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "kinto-exp-move-home", there.Id).Command!);
        Check(e3.State.Cards[there.Id] is { Zone: Zone.Home, Bowed: false }, "from the battlefield he is moved home");

        // Outside a battle neither half is on offer.
        var s4 = Table(Phase.Action);
        var idle = Add(s4, Kinto, 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "kinto-exp-move-home", idle.Id) && !CanUse(e4, 0, "kinto-exp-move-battle", idle.Id), "a Home Battle action is a Battle action");
    }

    private static void KintoHomeIsNotAnywhere()
    {
        // At ANOTHER battlefield, with a unit of his player's at the current one: he is neither at the battlefield nor at home.
        var s = Table();
        s.UnresolvedBattlefields = [0, 1];
        var kinto = Add(s, Kinto, 0, Zone.Battlefield, province: 1);
        Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "kinto-exp-move-home", kinto.Id), "Home is 'at home', not 'anywhere'");
        Check(Rejection(e, Use(e, 0, "kinto-exp-move-home", kinto.Id)).Contains("battlefield"), "the reason is his location");
        Check(e.State.Cards[kinto.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 1 }, "unchanged");

        // Bowed: Home is not Tireless.
        var s2 = Table();
        var bowed = Add(s2, Kinto, 0, Zone.Home, bowed: true);
        Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "kinto-exp-move-home", bowed.Id) && !CanUse(e2, 0, "kinto-exp-move-battle", bowed.Id), "bowed: neither half is offered");

        // Negated: he does not move.
        var s3 = Table();
        var held = Add(s3, Kinto, 0, Zone.Home);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3); e3.NegateEveryAction = true;
        Accept(e3, Use(e3, 0, "kinto-exp-move-battle", held.Id));
        Eq(e3.State.Cards[held.Id].Zone, Zone.Home, "negated: he stays home");
    }

    // ---- Daigotsu Kanpeki - exp2 ----

    private static void KanpekiTakesTheFavor()
    {
        var s = Table(Phase.Action);
        s.FavorPlayerId = 1;
        var kanpeki = Add(s, Kanpeki, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "kanpeki-exp2-favor", kanpeki.Id).Command!);
        Eq(e.State.FavorPlayerId, (int?)0, "Take the Imperial Favor");
        Check(e.State.Cards[kanpeki.Id].Bowed, "he stays bowed");
        Check(e.GetView(1).Events.Any(ev => ev.Type == "favorTaken"), "both players are told");
        var again = Batch336Support.Edit(e, st => { st.FavorPlayerId = 1; st.PriorityPlayerId = 0; });
        Check(!CanUse(again, 0, "kanpeki-exp2-favor", kanpeki.Id), "once per turn");
        Rejection(again, Use(again, 0, "kanpeki-exp2-favor", kanpeki.Id));
        Eq(again.State.FavorPlayerId, (int?)1, "the refused action took nothing");

        // An Open action: not in the Battle phase.
        var s2 = Table();
        var inBattle = Add(s2, Kanpeki, 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "kanpeki-exp2-favor", inBattle.Id), "Open: not during a battle");

        // Negated: the Favor stays where it is.
        var s3 = Table(Phase.Action);
        var held = Add(s3, Kanpeki, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3); e3.NegateEveryAction = true;
        Accept(e3, Use(e3, 0, "kanpeki-exp2-favor", held.Id));
        Eq(e3.State.FavorPlayerId, (int?)null, "negated: nobody took the Favor");
    }

    // ---- My Ancestor's Steed ----

    private static void SteedMovesASpirit()
    {
        var s = Table();
        var caster = Add(s, Shugenja(), 0, Zone.Home);
        var steed = Add(s, Steed, 0, Zone.Attached, parent: caster.Id);
        var spirit = Keyworded(Add(s, "training-crane-veteran", 0, Zone.Home), "Spirit");
        var mortal = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemySpirit = Keyworded(Add(s, "training-lion-samurai", 1, Zone.Home), "Spirit");
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), 0, "precondition: no unit of the acting player's at the battlefield");
        var option = Enabled(e, 0, "ancestors-steed-move", steed.Id);
        Check(option.TargetIds!.Contains(spirit.Id), "your Spirit Personality at home is a target (any location)");
        Check(!option.TargetIds.Contains(mortal.Id), "a Personality who is not a Spirit is not");
        Check(!option.TargetIds.Contains(enemySpirit.Id), "another player's Spirit is not ('your')");
        Rejection(e, Use(e, 0, "ancestors-steed-move", steed.Id, enemySpirit.Id));
        Rejection(e, Use(e, 0, "ancestors-steed-move", steed.Id, mortal.Id));
        Check(!e.State.Cards[steed.Id].Bowed && e.State.Cards[enemySpirit.Id].Zone == Zone.Home && e.State.Cards[mortal.Id].Zone == Zone.Home, "the refused commands changed nothing");
        Accept(e, option.Command! with { TargetId = spirit.Id });
        Check(e.State.Cards[spirit.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "the Spirit is moved to the current battlefield");
        Check(e.State.Cards[steed.Id].Bowed, "the Steed is bowed as the cost");
        Check(e.State.Cards[caster.Id] is { Zone: Zone.Home, Bowed: false }, "the Shugenja stays home, unbowed (no such cost is printed)");

        // With the Shugenja at the battlefield it is an ordinary Battle action, and a Spirit at home is still "at any location".
        var s2 = Table();
        var caster2 = Add(s2, Shugenja(), 0, Zone.Battlefield, province: 0);
        var steed2 = Add(s2, Steed, 0, Zone.Attached, parent: caster2.Id);
        var spirit2 = Keyworded(Add(s2, "training-crane-veteran", 0, Zone.Home), "Spirit");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Batch336Support.Offer(e2, 0, "ancestors-steed-move", steed2.Id, spirit2.Id));
        Eq(e2.State.Cards[spirit2.Id].Zone, Zone.Battlefield);

        // No Spirit of his own: there is no target, so it cannot be taken.
        var s3 = Table();
        var caster3 = Add(s3, Shugenja(), 0, Zone.Home);
        var steed3 = Add(s3, Steed, 0, Zone.Attached, parent: caster3.Id);
        Keyworded(Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Spirit");
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "ancestors-steed-move", steed3.Id), "no Spirit Personality of his own: not offered");
    }

    private static void SteedCostAndNegation()
    {
        // A bowed Steed cannot pay its bow cost (it is not Tireless).
        var s = Table();
        var caster = Add(s, Shugenja(), 0, Zone.Home);
        var steed = Add(s, Steed, 0, Zone.Attached, bowed: true, parent: caster.Id);
        var spirit = Keyworded(Add(s, "training-crane-veteran", 0, Zone.Home), "Spirit");
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "ancestors-steed-move", steed.Id), "a bowed Steed is not offered");
        Check(Rejection(e, Use(e, 0, "ancestors-steed-move", steed.Id, spirit.Id)).Contains("bowed"), "the reason is the bowed card");
        Eq(e.State.Cards[spirit.Id].Zone, Zone.Home);

        // Negated: the cost is paid, the Spirit does not move.
        var s2 = Table();
        var caster2 = Add(s2, Shugenja(), 0, Zone.Home);
        var steed2 = Add(s2, Steed, 0, Zone.Attached, parent: caster2.Id);
        var spirit2 = Keyworded(Add(s2, "training-crane-veteran", 0, Zone.Home), "Spirit");
        var e2 = MatchEngine.FromTestState(s2); e2.NegateEveryAction = true;
        Accept(e2, Use(e2, 0, "ancestors-steed-move", steed2.Id, spirit2.Id));
        Check(e2.State.Cards[steed2.Id].Bowed, "negated: the bow cost is still paid");
        Eq(e2.State.Cards[spirit2.Id].Zone, Zone.Home, "negated: the Spirit does not move");
    }

    // ---- Koan's Scroll Satchel and the granted Home ----

    // A Shugenja at home with the Satchel and two Spells (Conflagration, Kiyoshi's Wrath); another Shugenja of the same player with a Spell of his own.
    private static (MatchState S, CardInstance Caster, CardInstance Satchel, CardInstance Flame, CardInstance Wrath, CardInstance Elsewhere) SatchelRig()
    {
        var s = Table(Phase.Action);
        var caster = Add(s, Shugenja(), 0, Zone.Home);
        var satchel = Add(s, Satchel, 0, Zone.Attached, parent: caster.Id);
        var flame = Add(s, Conflagration, 0, Zone.Attached, parent: caster.Id);
        var wrath = Add(s, KiyoshisWrath, 0, Zone.Attached, parent: caster.Id);
        var other = Add(s, Shugenja(), 0, Zone.Home);
        var elsewhere = Add(s, Conflagration, 0, Zone.Attached, parent: other.Id);
        return (s, caster, satchel, flame, wrath, elsewhere);
    }

    private static void SatchelGivesHome()
    {
        var (s, caster, satchel, flame, wrath, elsewhere) = SatchelRig();
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "koan-satchel-home", satchel.Id);
        var slot = option.Slots!.Single();
        Check(new HashSet<string>(slot.CardIds).SetEquals([flame.Id, wrath.Id]), "the Spells in this unit are the candidates (not the Satchel, not another unit's Spell): " + string.Join(",", slot.CardIds));
        Check(slot.Min == 1, "one or more");

        // One Koan card in the unit (the Satchel itself): two Spells are one too many.
        var version = e.Version;
        Check(Rejection(e, Use(e, 0, "koan-satchel-home", satchel.Id, null, Pick("spells", flame.Id, wrath.Id))).Contains("at most 1"), "up to the number of Koan cards in this unit");
        Rejection(e, Use(e, 0, "koan-satchel-home", satchel.Id, null, Pick("spells", elsewhere.Id)));
        Rejection(e, Use(e, 0, "koan-satchel-home", satchel.Id));
        Eq(e.Version, version, "refused commands leave the state alone");
        Check(!e.State.Cards[satchel.Id].Bowed && !HasHome(e, flame.Id) && !HasHome(e, elsewhere.Id), "nothing was bowed or given");

        Accept(e, Use(e, 0, "koan-satchel-home", satchel.Id, null, Pick("spells", flame.Id)));
        Check(e.State.Cards[satchel.Id].Bowed, "the Satchel is bowed as the cost");
        Check(HasHome(e, flame.Id) && !HasHome(e, wrath.Id), "the chosen Spell has Home; the other does not");
        Eq(e.State.Cards[flame.Id].GrantedKeywords.Single(g => g.Keyword == "Home").Duration, GrantDuration.Turn, "until the turn ends");

        // Two Koan cards in the unit (another card of the unit is given the keyword): two Spells may be chosen.
        var (s2, _, satchel2, flame2, wrath2, _) = SatchelRig();
        Keyworded(wrath2, "Koan");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "koan-satchel-home", satchel2.Id, null, Pick("spells", flame2.Id, wrath2.Id)));
        Check(HasHome(e2, flame2.Id) && HasHome(e2, wrath2.Id), "two Koan cards: two Spells are given Home");

        // A bowed Satchel cannot pay; and a negated action pays the bow and gives nothing.
        var (s3, _, satchel3, flame3, _, _) = SatchelRig();
        satchel3.Bowed = true;
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "koan-satchel-home", satchel3.Id), "a bowed Satchel is not offered");
        var (s4, _, satchel4, flame4, _, _) = SatchelRig();
        var e4 = MatchEngine.FromTestState(s4); e4.NegateEveryAction = true;
        Accept(e4, Use(e4, 0, "koan-satchel-home", satchel4.Id, null, Pick("spells", flame4.Id)));
        Check(e4.State.Cards[satchel4.Id].Bowed && !HasHome(e4, flame4.Id), "negated: the bow is paid, no Home is given");
        _ = caster; _ = flame3;
    }

    private static MatchEngine InBattle(MatchEngine e, bool presence)
    {
        return Batch336Support.Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0];
            Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (presence) Add(st, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        });
    }

    private static void GrantedHomeActsAndLapses()
    {
        // Before the Satchel: a Spell of a Shugenja at home is not usable in a battle, presence or not.
        var (s0, _, _, flame0, _, _) = SatchelRig();
        var before = InBattle(MatchEngine.FromTestState(s0), presence: true);
        Check(!CanUse(before, 0, ConflagrationBoost, flame0.Id), "without Home a Spell at home is not offered");
        Check(Rejection(before, Use(before, 0, ConflagrationBoost, flame0.Id)).Contains("battlefield"), "the reason is its wielder's location");

        var (s, caster, satchel, flame, wrath, elsewhere) = SatchelRig();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "koan-satchel-home", satchel.Id, null, Pick("spells", flame.Id)));

        // In a battle that turn, with a unit of his at the battlefield: the Spell with Home acts from home; the others do not.
        var battle = InBattle(e, presence: true);
        var force = battle.CardForceForTest(caster.Id);
        Check(CanUse(battle, 0, ConflagrationBoost, flame.Id), "a granted Home is read by the same check: " + Reasons(battle, 0, ConflagrationBoost, flame.Id));
        Check(!CanUse(battle, 0, KiyoshiBow, wrath.Id), "the Spell that was not given Home is not offered from home");
        Check(!CanUse(battle, 0, ConflagrationBoost, elsewhere.Id), "nor is another unit's Spell");
        Accept(battle, Enabled(battle, 0, ConflagrationBoost, flame.Id).Command!);
        Check(battle.State.Cards[flame.Id].Bowed, "its own bow cost is paid");
        Eq(battle.CardForceForTest(caster.Id), force + 3, "Give this Shugenja +3F, taken from home");
        Eq(battle.State.Cards[caster.Id].Zone, Zone.Home, "the Shugenja never left home");

        // Without a unit at the battlefield: Home is not Absent.
        var noPresence = InBattle(e, presence: false);
        Check(!CanUse(noPresence, 0, ConflagrationBoost, flame.Id), "Home does not waive presence");
        Check(Rejection(noPresence, Use(noPresence, 0, ConflagrationBoost, flame.Id)).Contains("unit at the current battlefield"), "the reason is the missing unit");
        Check(!noPresence.State.Cards[flame.Id].Bowed, "unchanged");

        // The grant lasts until the turn ends: play on until the next turn begins, then rig the same battle.
        var turn = e.State.TurnNumber;
        Batch336Support.RunUntil(e, () => e.State.TurnNumber > turn);
        Check(!HasHome(e, flame.Id), "the granted Home lapsed when the turn ended");
        var nextTurn = InBattle(Batch336Support.Edit(e, st => { st.Cards[flame.Id].Bowed = false; st.Cards[caster.Id].Zone = Zone.Home; st.Cards[caster.Id].ProvinceIndex = null; st.PendingChoice = null; }), presence: true);
        Check(!CanUse(nextTurn, 0, ConflagrationBoost, flame.Id), "next turn the Spell is not offered from home");
    }
}
