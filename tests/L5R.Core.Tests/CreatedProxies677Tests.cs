using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #677: the created-card proxies published under Mendon's ruling of 2026-10-06 ("Use the ivory frame for the cards, no other frame exists. Prefer the oracle's scans that are exact matches over
// my scans that are manufactured.") and the creators they unblock (src/L5R.Core/CreatedProxies677.cs; waves personality-wave-145, follower-wave-58, item-wave-58, spell-wave-65, strategy-wave-129,
// holding-wave-63). Every position is explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion, province 0 the current battlefield) and driven through QueryActions and the commands
// the engine offers; a refused command must leave the whole state unchanged.
internal static class CreatedProxies677Tests
{
    // Creators.
    private const string Abdollah = "oracle-l5r-10667", Koru = "oracle-l5r-10833", Renyu = "oracle-l5r-10440", Yamazaru = "oracle-l5r-10856", KichiExp = "oracle-l5r-10883", Miyano = "oracle-l5r-10507",
        Hashibei = "oracle-l5r-10316", Tselu = "oracle-l5r-10675", GojuKaxt = "oracle-l5r-10336", Shard = "oracle-l5r-10697", YojimboOfEarth = "oracle-l5r-10924",
        AshalanAmbush = "oracle-l5r-10715", CavalryTactics = "oracle-l5r-10724", NewPerspective = "oracle-l5r-10712", RecruitmentEffort = "oracle-l5r-10390", OnyxCircle = "oracle-l5r-10757",
        NeverWithoutWeapon = "oracle-l5r-10949", WindsChampion = "oracle-l5r-10923";
    // Proxies published here.
    private const string Ashalan = "oracle-l5r-11328", Bat = "oracle-l5r-10809", MinorIllusion = "oracle-l5r-11341", TowerGuard = "oracle-l5r-11345", Kaxt = "oracle-l5r-10813",
        ShadowBadger = "oracle-l5r-11335", OnyxOni = "oracle-l5r-11343", TsumaroIllusion = "oracle-l5r-11346", MajorIllusion = "oracle-l5r-11338", Monkey = "oracle-l5r-11327",
        Butterflies = "oracle-l5r-10808", Zombie = "oracle-l5r-11349", ClayYojimbo = "oracle-l5r-11332", Elephant = "oracle-l5r-11337", Dog = "oracle-l5r-10815", Retinue = "oracle-l5r-9745",
        UnexpectedWeapon = "oracle-l5r-11348";
    // Proxies published by earlier waves.
    private const string Conscripts = "oracle-l5r-10004", DutifulCavalry = "oracle-l5r-10003", BirdOfPrey = "oracle-l5r-9712", HiredSamurai = "oracle-l5r-10811", Colonists = "user-scan-colonists";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Shugenja = "training-shugenja", NinjaSource = "training-ninja-source",
        Destroyer = "training-no-attachments-destroyer", KaiuEsumi = "oracle-l5r-10834", FocusThree = "training-focus-strategy", Marketplace = "training-marketplace", TrainingFollower = "training-follower";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 0) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, null, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static CardDefinition Printed(CardInstance c) => c.Printed ?? CardCatalog.Get(c.DefinitionId);

    public static void Run(TestRunner runner) => runner.Run("created-proxies-677", [
        ("Published as printed: every creator and proxy with its stats, keywords, text, abilities and manifest; the proxies are created-only; provenance names the reviewed printing", PublishedShape),
        ("Every creator's printed card resolves to exactly its proxy scan, the six pairings with earlier proxies included, and no creator fails verification", EveryCreatorResolves),
        ("A proxy whose creator is not wired is created by nothing, and the creators left unwired are not published", UnwiredGuard),
        ("Abdollah: entering play loses 2 Honor and creates a Zombie at home (mandatory, one, no window); the Zombie is removed from the game when destroyed", AbdollahCreatesZombie),
        ("Kitsune Kichi - exp: entering play loses 3 Honor and creates the Shadow Badger; its Battle, Bow: Melee 4 Attack destroys Force 4, spares Force 5 and bows it as a cost", KichiExpAndBadger),
        ("Kuni Renyu - exp2: entering play attaches Renyu's Dog to him; Earth Battle bows an enemy card with Shadowlands or without attachments only", RenyuDogAndBow),
        ("Hiruma Koru: Invest 1 on entering play attaches a 0F Follower to him; optional, once, only then, and a trait", KoruInvest),
        ("Kitsune Yamazaru: after you Recruit him the Interrupt creates the Monkey (optional); the Monkey discards a card for a Force penalty equal to its Focus Value", YamazaruAndMonkey),
        ("Komori Miyano: Air Limited creates a Bat at home, once a turn; it is removed from the game when her controller's next turn begins", MiyanoBat),
        ("Daigotsu Hashibei: Fear Open, bowing him, attaches a 1F Follower to your target Personality; bowed he cannot; an enemy is no target", HashibeiFollower),
        ("Shinjo Tselu - exp2: Limited, 2 Gold attaches a 1F Cavalry Follower to your target Personality; unpaid it is refused", TseluCavalry),
        ("Goju Kaxt: other Ninja in his army have +1F (not himself, not a non-Ninja, not at home, not the enemy's)", KaxtNinjaForce),
        ("Goju Kaxt: destroyed by an action, alone or with his Personality, he creates Kaxt with his controller's Clan at home or at the battlefield, once; a later action that destroys his old host offers nothing", KaxtCreatesKaxt),
        ("Shard of the Great Death's Bones: entering play loses 2 Honor; three Birds of Prey are dealt to one to three of your Personalities; optional as a trait", ShardBirds),
        ("Yojimbo of Earth: Earth Limited, bowing the Spell, attaches a Clay Yojimbo to your Shugenja without Followers only", YojimboOfEarthFollower),
        ("Ashalan Ambush: Limited, 8 Gold creates the Ashalan at home and the Strategy is discarded; unpaid it is refused", AshalanAmbushCreates),
        ("New Cavalry Tactics: an Engage action for 3 Gold; two Little Elephants on your target Personality, three on a Commander", CavalryTacticsElephants),
        ("A New Perspective: Battle, 2 Gold; a 1F Follower on each of your opposed Personalities at the current battlefield, none elsewhere, none unopposed", NewPerspectiveFollowers),
        ("Recruitment Effort: Open, destroying your non-Unique Holding as a cost, creates a 3F/3C/2PH Samurai; a Unique or an enemy Holding is no target", RecruitmentEffortSamurai),
        ("The Onyx Dueling Circle: produces 2 Gold; Limited, Bow destroys your Shugenja, creates the Onyx Oni, destroys the Holding and loses 4 Honor", OnyxCircleOni),
        ("Never Without a Weapon: Battle, 5 Gold, discarding a card, attaches a Weapon whose Force Modifier is the discarded card's Focus Value; the one-Weapon rule and the cost hold", NeverWithoutAWeapon),
        ("The Wind's Champion: Air Open, bowing the Spell and its Shugenja, creates an Illusion with Force equal to his current Chi, plus 1 if he is Air; the Spell is destroyed", WindsChampionIllusion),
    ]);

    // ---- helpers ----

    private static void Gold(MatchState s, int player, int marketplaces = 4) { for (var i = 0; i < marketplaces; i++) Add(s, Marketplace, player, Zone.Home); }
    private static GameCommand Paid(MatchEngine e, int player, int cost, GameCommand command) => command with { SourceIds = e.PaymentOptions(player, cost).First().SourceIds };
    private static CardInstance[] Of(MatchEngine e, string definition) => e.State.Cards.Values.Where(c => c.DefinitionId == definition && c.Zone != Zone.Province && c.Zone is not (Zone.DynastyDeck or Zone.FateDeck)).ToArray();
    private static CardInstance[] AttachedTo(MatchEngine e, CardInstance host, string definition) => e.State.Cards.Values.Where(c => c.DefinitionId == definition && c.Zone == Zone.Attached && c.AttachedTo == host.Id).ToArray();

    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        var result = e.Execute(command);
        Check(!result.Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }

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

    private static void Purchase(MatchEngine e, int player, string cardId) =>
        Accept(e, new GameCommand("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds));

    // A Personality of the player's own Clan may be proclaimed: the Proclaim window and the card's own entering-play window are then one window, which a single pass closes.
    private static void DeclineProclaim(MatchEngine e)
    {
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", e.State.PendingChoice.PlayerId, e.Version));
    }

    private static void EntryWindowOpen(MatchEngine e) => Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"the entering-play window is open ({e.State.PendingChoice?.Type})");

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            var command = pick.Command!;
            if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[mover].Hand.Take(e.State.Players[mover].Hand.Count - 8).ToArray() };
            Accept(e, command);
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    // ---- shape ----

    private static readonly Dictionary<string, string[]> Waves = new()
    {
        ["personality-wave-145"] = [Abdollah, Koru, Renyu, Yamazaru, KichiExp, Miyano, Hashibei, Tselu, Ashalan, Bat, MinorIllusion, TowerGuard, Kaxt, ShadowBadger, OnyxOni, TsumaroIllusion,
            MajorIllusion, Monkey, Butterflies, Zombie],
        ["follower-wave-58"] = [GojuKaxt, ClayYojimbo, Elephant, Dog, Retinue],
        ["item-wave-58"] = [Shard, UnexpectedWeapon],
        ["spell-wave-65"] = [WindsChampion, YojimboOfEarth],
        ["strategy-wave-129"] = [AshalanAmbush, CavalryTactics, NewPerspective, RecruitmentEffort, NeverWithoutWeapon],
        ["holding-wave-63"] = [OnyxCircle],
    };

    private const string IvoryNote = "(This is a proxy for a created card. It is not considered to have a title and cannot be included in decks.)";

    private static void PublishedShape()
    {
        foreach (var (wave, ids) in Waves)
        {
            var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(new HashSet<string>(listed).SetEquals(ids) && listed.Length == ids.Length, $"{wave}'s manifest is exactly its cards");
            foreach (var id in ids) Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        }

        void Is(string id, string name, CardType type, string clan, int force, int chi, int ph, int gold, int? hr, int focus, string[] keywords, string text, string[] abilities, string effect = "none",
            int? entryLoss = null, bool created = false, int production = 0)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == type && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Type}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr && c.Focus == focus && c.GoldProduction == production,
                $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement} focus {c.Focus} production {c.GoldProduction}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Eq(c.Text, text, $"{name}: printed text");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
            Eq(c.CreatedOnly, created, $"{name}: created-only");
            Check(!c.ScenarioOnly, $"{name}: a published card");
        }
        const CardType P = CardType.Personality, F = CardType.Follower;
        // Creators, read off their own scans.
        Is(Abdollah, "Abdollah", P, "Unaligned", 1, 2, 0, 6, null, 0, ["Gaijin", "Jackal", "Shugenja"],
            "After Abdollah enters play, lose 2 Honor and create a 2F/2C/0PH Nonhuman • Shadowlands • Undead Personality.", [], "proxy677-abdollah-entry", 2);
        Is(Koru, "Hiruma Koru", P, "Crab", 3, 4, 1, 5, null, 0, ["Crab Clan", "Samurai", "Scout"],
            "Invest :g1:: Create a 0F Follower and attach it to Koru. (After this card enters play, you may also pay the Invest cost to get the effect, once.)", ["proxy677-koru-invest"]);
        Is(Renyu, "Kuni Renyu", P, "Crab", 5, 5, 3, 10, null, 0, ["Experienced 2", "Loyal", "Unique", "Crab Clan", "Daimyo", "Earth", "Jade", "Magistrate", "Shugenja", "Witch Hunter"],
            "After Renyu enters play, create a 1F Dog • Nonhuman Follower and attach it to him.\nEarth Battle: Bow a target enemy card with Shadowlands or without attachments.",
            ["proxy677-renyu-bow"], "proxy677-renyu-entry");
        Is(Yamazaru, "Kitsune Yamazaru", P, "Mantis", 0, 3, 3, 6, 2, 0, ["Earth", "Mantis Clan", "Shugenja"],
            "Interrupt: After you Recruit Yamazaru, create a 3F/2C/3PH Mantis Clan • Monkey • Nonhuman • Spirit Personality with the ability, \"Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value.\"",
            ["proxy677-yamazaru-create"]);
        Is(KichiExp, "Kitsune Kichi", P, "Unaligned", 0, 4, 0, 8, null, 0, ["Unique", "Earth", "Experienced", "Kitsune", "Nonhuman", "Onyx Champion", "Shadowlands", "Shugenja"],
            "After Kichi enters play, lose 3 Honor and create a 4F/2C/0PH Badger • Nonhuman • Shadowlands • Spirit Personality with the ability, \"Battle, Bow: Melee 4 Attack.\"",
            [], "proxy677-kichi-exp-entry", 3);
        Is(Miyano, "Komori Miyano, the Shadow Wing", P, "Unicorn", 1, 3, 1, 4, null, 0, ["Unique", "Air", "Bat Clan", "Shugenja", "Unicorn Clan"],
            "Air Limited: Create a 0F/2C/0PH Bat • Nonhuman • Cavalry Personality. Remove it from the game after your next turn begins.", ["proxy677-miyano-bat"]);
        Is(Hashibei, "Daigotsu Hashibei", P, "Spider", 1, 3, 0, 5, null, 0, ["Commander", "Recruitment Officer", "Samurai", "Spider Clan"],
            "Fear Open: Bow Hashibei and target your Personality: Create a 1F Follower and attach it to him.", ["proxy677-hashibei-create"]);
        Is(Tselu, "Shinjo Tselu", P, "Unicorn", 5, 5, 3, 10, 10, 0, ["Cavalry", "Experienced 2", "Unique", "Commander", "Imperial", "Ivory Champion", "Magistrate", "Samurai", "Unicorn Clan"],
            "Limited, :g2:: Create a 1F Cavalry Follower and attach it to your target Personality.", ["proxy677-tselu-create"]);
        Is(GojuKaxt, "Goju Kaxt", F, "Unaligned", 3, 0, 0, 6, 0, 3, ["Ninja"],
            "Other Ninja in this army have +1F.\nAfter an action resolves during which this card was destroyed: Create a 4F/3C/0PH Ninja Personality with your Clan alignment at any location.",
            ["proxy677-kaxt-create-home", "proxy677-kaxt-create-battle"], "proxy677-kaxt-ninja-force");
        Is(Shard, "Shard of the Great Death's Bones", CardType.Item, "Unaligned", 0, 0, 0, 3, null, 3, ["Shadowlands"],
            "After this Item enters play, lose 2 Honor, create three 1F Nonhuman • Raptor • Shadowlands • Undead • Cavalry Followers, and attach them to your Personalities.",
            ["proxy677-shard-create"], "none", 2);
        Is(YojimboOfEarth, "Yojimbo of Earth", CardType.Spell, "Unaligned", 0, 0, 0, 2, null, 3, ["Earth"],
            "Earth Limited, Bow this card: Create a 1F Clay Soldier • Nonhuman • Yojimbo Follower and attach it to your target Shugenja without Followers.", ["proxy677-yojimbo-earth"]);
        Is(AshalanAmbush, "Ashalan Ambush", CardType.Strategy, "Unaligned", 0, 0, 0, 8, null, 2, [], "Limited, :g8:: Create a 4F/4C/0PH Ashalan • Nonhuman • Shugenja Personality.", ["proxy677-ashalan-create"]);
        Is(CavalryTactics, "New Cavalry Tactics", CardType.Strategy, "Unaligned", 0, 0, 0, 3, null, 2, [],
            "Engage, :g3:: Target your Personality. Create and attach two, or three if the target is a Commander, 1F Elephant • Nonhuman • Cavalry Followers to him.", ["proxy677-cavalry-tactics"]);
        Is(NewPerspective, "A New Perspective", CardType.Strategy, "Unaligned", 0, 0, 0, 2, null, 2, [],
            "Battle, :g2:: Create and attach a 1F Follower to each of your opposed Personalities at the current battlefield.", ["proxy677-new-perspective"]);
        Is(RecruitmentEffort, "Recruitment Effort", CardType.Strategy, "Unaligned", 0, 0, 0, 3, null, 2, [], "Open: Destroy your target non-Unique Holding: Create a 3F/3C/2PH Samurai Personality.",
            ["proxy677-recruitment-effort"]);
        Is(NeverWithoutWeapon, "Never Without a Weapon", CardType.Strategy, "Unaligned", 0, 0, 0, 0, null, 4, [],
            "Battle, :g5:: Discard a card to create a Weapon Item with a Force Modifier equal to the discarded card's Focus Value and attach it to your target Personality.", ["proxy677-never-without-weapon"]);
        Is(WindsChampion, "The Wind's Champion", CardType.Spell, "Unaligned", 0, 0, 0, 7, null, 3, [],
            "Air Open, Bow this card: Bow this Shugenja to create a 2C/0PH Illusion • Samurai • Spirit Personality with Force equal to this Shugenja's Chi, plus 1 if he is Air. Destroy this Spell.",
            ["proxy677-winds-champion"]);
        Is(OnyxCircle, "The Onyx Dueling Circle", CardType.Holding, "Unaligned", 0, 0, 0, 3, null, 0, ["Unique"],
            "Bow this card: Produce 2 Gold.\nLimited, Bow this card: Destroy your target Shugenja. Create a 5F/2C/0PH Nonhuman • Oni • Shadowlands Personality. Destroy this Holding. Lose 4 Honor.",
            ["proxy677-onyx-circle"], production: 2);
        // Proxies, read off their own scans: the real type is the printed frame's; Gold Cost 0, no Honor Requirement.
        Is(Ashalan, "Ashalan", P, "Unaligned", 4, 4, 0, 0, null, 0, ["Ashalan", "Nonhuman", "Shugenja"], IvoryNote, [], created: true);
        Is(Bat, "Bat", P, "Unaligned", 0, 2, 0, 0, null, 0, ["Nonhuman", "Bat", "Cavalry"], "", [], created: true);
        Is(MinorIllusion, "Minor Illusion", P, "Unaligned", 0, 0, 0, 0, null, 0, ["Illusion", "Nonhuman", "Cavalry", "Shugenja"],
            "This Personality's Chi and Personal Honor are equal to the Chi and Personal Honor of the caster, respectively.\n" + IvoryNote, [], created: true);
        Is(TowerGuard, "Tower Guard", P, "Unaligned", 0, 2, 2, 0, null, 0, ["Guard", "Samurai"], IvoryNote, [], created: true);
        Is(Kaxt, "Kaxt", P, "Unaligned", 4, 3, 0, 0, null, 0, ["Ninja"], "May not be included in decks.\nThis card has your Clan alignment keyword.", [], created: true);
        Is(ShadowBadger, "Kichi's Shadow Badger", P, "Unaligned", 4, 2, 0, 0, null, 0, ["Badger", "Nonhuman", "Shadowlands", "Spirit"], "Battle, Bow: Melee 4 Attack.\n" + IvoryNote,
            ["proxy677-badger-melee"], created: true);
        Is(OnyxOni, "Onyx Oni", P, "Unaligned", 5, 2, 0, 0, null, 0, ["Nonhuman", "Oni", "Shadowlands"], IvoryNote, [], created: true);
        Is(TsumaroIllusion, "Tsumaro's Illusion", P, "Unaligned", 0, 3, 0, 0, null, 0, ["Air", "Illusion", "Shugenja"], IvoryNote, [], created: true);
        Is(MajorIllusion, "Major Illusion", P, "Unaligned", 0, 2, 0, 0, null, 0, ["Illusion", "Samurai", "Spirit"],
            "This Personality's Force is equal to the Chi of the caster, plus 1 if he was Air.\n" + IvoryNote, [], created: true);
        Is(Monkey, "Yamazaru's Monkey", P, "Mantis", 3, 2, 3, 0, null, 0, ["Mantis Clan", "Monkey", "Nonhuman", "Spirit"],
            "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value.\n" + IvoryNote, ["proxy677-monkey-weaken"], created: true);
        Is(Butterflies, "Yuribara's Butterflies", P, "Mantis", 0, 2, 3, 0, null, 0, ["Mantis Clan", "Nonhuman", "Butterfly", "Cavalry", "Spirit"],
            "May not be included in decks.\nOther player's Battle actions must target a Butterfly Personality at this battlefield, if legal.\nAfter another player's Battle action targets this card: Destroy this card.",
            [], created: true);
        Is(Zombie, "Zombie", P, "Unaligned", 2, 2, 0, 0, null, 0, ["Nonhuman", "Shadowlands", "Undead"], IvoryNote, [], created: true);
        Is(ClayYojimbo, "Clay Yojimbo", F, "Unaligned", 1, 0, 0, 0, null, 0, ["Clay Soldier", "Nonhuman", "Yojimbo"], IvoryNote, [], created: true);
        Is(Elephant, "Little Elephant", F, "Unaligned", 1, 0, 0, 0, null, 0, ["Elephant", "Nonhuman", "Cavalry"], IvoryNote, [], created: true);
        Is(Dog, "Renyu's Dog", F, "Unaligned", 1, 0, 0, 0, null, 0, ["Nonhuman", "Dog"], "", [], created: true);
        Is(Retinue, "Disposable Troops", F, "Unaligned", 0, 0, 0, 0, 0, 0, [], "May not be included in decks.", [], created: true);
        Is(UnexpectedWeapon, "Unexpected Weapon", CardType.Item, "Unaligned", 0, 0, 0, 0, null, 0, ["Weapon"],
            "This Item's Force Modifier is equal to the discarded card's Focus Value.\n" + IvoryNote, [], created: true);

        // Provenance: each Oracle proxy from the reviewed printing (an Emperor-era one where it exists, else Ivory Edition; Disposable Troops from its Celestial-frame printing), each with a committed receipt.
        var expected = new Dictionary<string, string>
        {
            [Ashalan] = "ivory-edition",
            [MinorIllusion] = "ivory-edition",
            [TowerGuard] = "ivory-edition",
            [ShadowBadger] = "ivory-edition",
            [OnyxOni] = "ivory-edition",
            [TsumaroIllusion] = "ivory-edition",
            [MajorIllusion] = "ivory-edition",
            [Monkey] = "ivory-edition",
            [Zombie] = "ivory-edition",
            [ClayYojimbo] = "ivory-edition",
            [Elephant] = "ivory-edition",
            [UnexpectedWeapon] = "ivory-edition",
            [Retinue] = "promotional-celestial",
            [Bat] = "coils-of-madness",
            [Dog] = "coils-of-madness",
            [Kaxt] = "torn-asunder",
            [Butterflies] = "torn-asunder",
            [Abdollah] = "gates-of-chaos",
            [Koru] = "aftermath",
            [Renyu] = "coils-of-madness",
            [Yamazaru] = "aftermath",
            [KichiExp] = "aftermath",
            [Miyano] = "coils-of-madness",
            [Hashibei] = "torn-asunder",
            [Tselu] = "gates-of-chaos",
            [GojuKaxt] = "torn-asunder",
            [Shard] = "gates-of-chaos",
            [YojimboOfEarth] = "aftermath",
            [AshalanAmbush] = "gates-of-chaos",
            [CavalryTactics] = "gates-of-chaos",
            [NewPerspective] = "gates-of-chaos",
            [RecruitmentEffort] = "torn-asunder",
            [NeverWithoutWeapon] = "aftermath",
            [WindsChampion] = "aftermath",
            [OnyxCircle] = "promotional-emperor"
        };
        var rows = 0;
        foreach (var wave in Waves.Keys)
            foreach (var row in JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray())
            {
                rows++;
                var id = row.GetProperty("card_id").GetString()!;
                var path = row.GetProperty("image_path").GetString()!;
                Check(path.Contains($"/oracle/{expected[id]}/"), $"{id} comes from the reviewed {expected[id]} scan ({path})");
                Check(Directory.GetFiles(Path.Combine(RepoRoot(), "assets/reference/oracle", expected[id], "receipts"), $"{id}-printing-*-image-1.json").Length == 1, $"{id} has a committed receipt");
            }
        Eq(rows, Waves.Values.Sum(ids => ids.Length), "one provenance row per card");

        // The abilities exist with the printed timing, costs and tags.
        var all = AbilityCatalog.All;
        Check(all[CreatedProxies677Abilities.KoruInvest].Cost == GoldCostRule.Fixed(1) && all[CreatedProxies677Abilities.TseluCreate].Cost == GoldCostRule.Fixed(2)
            && all[CreatedProxies677Abilities.NeverWithoutWeapon].Cost == GoldCostRule.Fixed(5), "the printed Gold costs");
        Check(all[CreatedProxies677Abilities.MiyanoBat].ActionKeywords!.SequenceEqual(["Air"]) && all[CreatedProxies677Abilities.RenyuBow].ActionKeywords!.SequenceEqual(["Earth"])
            && all[CreatedProxies677Abilities.YojimboEarth].ActionKeywords!.SequenceEqual(["Earth"]) && all[CreatedProxies677Abilities.HashibeiCreate].ActionKeywords!.SequenceEqual(["Fear"])
            && all[CreatedProxies677Abilities.WindsChampion].ActionKeywords!.SequenceEqual(["Air"]), "the printed action keywords");
        Check(all[CreatedProxies677Abilities.CavalryTactics] is { Timing: "Reaction", Trigger: "after-engaging" } && all[CreatedProxies677Abilities.YamazaruCreate] is { Timing: "Reaction", Trigger: "own-enters-play" },
            "an Engage and an Interrupt after a Recruit are Reactions");
        foreach (var trait in new[] { CreatedProxies677Abilities.KoruInvest, CreatedProxies677Abilities.KaxtCreateHome, CreatedProxies677Abilities.KaxtCreateBattle, CreatedProxies677Abilities.ShardCreate })
            Check(TraitReactions.Is(trait), $"{trait} is a trait, not a Reaction");
        Check(!TraitReactions.Is(CreatedProxies677Abilities.YamazaruCreate) && !TraitReactions.Is(CreatedProxies677Abilities.CavalryTactics), "a printed Interrupt or Engage is not a trait");
        Check(all[CreatedProxies677Abilities.BadgerMelee].Costs!.Single().Verb == CostVerb.Bow && all[CreatedProxies677Abilities.MonkeyWeaken].Slots!.Select(x => x.Id).SequenceEqual(["discard", "target"]),
            "the Badger bows as a cost; the Monkey's discard slot comes first");
    }

    private static void EveryCreatorResolves()
    {
        Eq(string.Join(" | ", CreatedCards.Verify()), "", "every creator's spec resolves to one scan the engine can create");
        void Resolves(CreatedCardSpec spec, string scan, string creator) => Eq(CreatedCards.Resolve(spec with { ProxyId = null }).Id, scan, $"{creator}: the printed card fits exactly one scan, without naming it");
        Resolves(CreatedProxies677Abilities.Zombie, Zombie, "Abdollah");
        Resolves(CreatedProxies677Abilities.ShadowBadger, ShadowBadger, "Kitsune Kichi - exp");
        Resolves(CreatedProxies677Abilities.Dog, Dog, "Kuni Renyu - exp2");
        Resolves(CreatedProxies677Abilities.Retinue, Retinue, "Hiruma Koru");
        Resolves(CreatedProxies677Abilities.Monkey, Monkey, "Kitsune Yamazaru");
        Resolves(CreatedProxies677Abilities.Bat, Bat, "Komori Miyano");
        Resolves(CreatedProxies677Abilities.Kaxt, Kaxt, "Goju Kaxt");
        Resolves(CreatedProxies677Abilities.ClayYojimbo, ClayYojimbo, "Yojimbo of Earth");
        Resolves(CreatedProxies677Abilities.Ashalan, Ashalan, "Ashalan Ambush");
        Resolves(CreatedProxies677Abilities.Elephants, Elephant, "New Cavalry Tactics");
        Resolves(CreatedProxies677Abilities.OnyxOni, OnyxOni, "The Onyx Dueling Circle");
        Resolves(CreatedProxies677Abilities.UnexpectedWeapon, UnexpectedWeapon, "Never Without a Weapon");
        Resolves(CreatedProxies677Abilities.MajorIllusion, MajorIllusion, "The Wind's Champion");
        // The six creators served by a proxy an earlier wave published: the same resolver, the same exact comparison.
        Resolves(CreatedProxies677Abilities.Conscripts, Conscripts, "A New Perspective, Daigotsu Hashibei and (unwired) Daigotsu Kikumura: a 1F Follower with no keyword");
        Resolves(CreatedProxies677Abilities.DutifulCavalry, DutifulCavalry, "Shinjo Tselu - exp2");
        Resolves(CreatedProxies677Abilities.BirdsOfPrey, BirdOfPrey, "Shard of the Great Death's Bones");
        Resolves(CreatedProxies677Abilities.HiredSamurai, HiredSamurai, "Recruitment Effort");
        Resolves(WlStratGatesAbilities.Colonists, Colonists, "Exotic Farmlands (wired by strategy-wave-119)");
        // What each creating ability declares is what its card prints.
        var all = AbilityCatalog.All;
        Eq(CreatedCards.Resolve(all[CreatedProxies677Abilities.KoruInvest].Creates!).Id, Retinue, "Koru's Invest creates Disposable Troops, the Oracle scan");
        Check(!CardCatalog.Cards.ContainsKey("user-scan-korus-retinue"), "the manufactured Koru's Retinue scan is not published: an exact Oracle scan is preferred");
        Check(all[CreatedProxies677Abilities.ShardCreate].Creates is { Count: 3, PerHost: false }, "the Shard: three in all");
        Check(all[CreatedProxies677Abilities.KaxtCreateBattle].Creates is { AtBattlefield: true } && all[CreatedProxies677Abilities.KaxtCreateHome].Creates is { AtBattlefield: false }, "Kaxt: at the battlefield, or at home");
        // A near miss is not accepted: the same card with one keyword more or fewer fits nothing.
        foreach (var nearMiss in new[] { CreatedProxies677Abilities.Zombie with { ProxyId = null, Keywords = ["Shadowlands", "Undead", "Nonhuman", "Oni"] },
            CreatedProxies677Abilities.Elephants with { ProxyId = null, Keywords = ["Elephant", "Cavalry"] }, CreatedProxies677Abilities.Retinue with { ProxyId = null, Force = 3 } })
        {
            var refused = false;
            try { CreatedCards.Resolve(nearMiss); } catch (CreatedCardResolutionException) { refused = true; }
            Check(refused, $"no scan fits {nearMiss.Describe()}");
        }
    }

    private static void UnwiredGuard()
    {
        // These proxies are published for their art and data; nothing creates them until their creator is wired. Yuribara's Butterflies prints two traits the engine does not have: a creator
        // that resolved to it would put a card into play with printed text that does nothing.
        var unwiredProxies = Array.Empty<string>(); // Tsumaro's Illusion is created by Isawa Tsumaro since issue #821 // Yuribara's Butterflies, Minor Illusion and Tower Guard are created by their creators since wave batch 10 (issue #741)
        foreach (var (abilityId, spec) in CreatedCards.Creators)
            foreach (var alternative in spec.Alternatives.Where(a => !a.CopyOfTarget))
                Check(!unwiredProxies.Contains(CreatedCards.Resolve(alternative).Id), $"{abilityId} creates {CreatedCards.Resolve(alternative).Name}, whose creator is not wired");
        // Isawa Tsumaro: left unwired, so not published. (Constructing the Self, The Tower of Vigilance and Kitsune Yuribara were wired by wave batch 10, issue #741.) (Daigotsu Kikumura was wired by wave batch 1, issue #732.)
        foreach (var id in Array.Empty<string>()) // Isawa Tsumaro (oracle-l5r-10863) was wired by issue #821
            Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is not published");
        // No created-only card is in any deck pool or deck (the general guard is in CreatedCardTests); the new ones are created-only.
        foreach (var id in Waves.Values.SelectMany(x => x).Where(id => CardCatalog.Get(id).CreatedOnly)) Check(CardCatalog.PlayableFromDecks.All(c => c.Id != id), $"{id} is not playable from a deck");
    }

    // ---- Abdollah ----

    private static void AbdollahCreatesZombie()
    {
        var (e, abdollah) = Recruit(Abdollah);
        var honor = Honor(e, 0);
        var cards = e.State.Cards.Count;
        Purchase(e, 0, abdollah.Id);
        Eq(e.State.Cards[abdollah.Id].Zone, Zone.Home, "Abdollah entered play");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor");
        var zombie = Of(e, Zombie).Single();
        Check(zombie.OwnerId == 0 && zombie.Zone == Zone.Home && zombie.FaceUp && !zombie.Bowed, "one Zombie, created for his controller at home, unbowed");
        var d = Printed(zombie);
        Check(d.Type == CardType.Personality && d.Force == 2 && d.Chi == 2 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Nonhuman", "Shadowlands", "Undead"]), "a 2F/2C/0PH Nonhuman Shadowlands Undead Personality");
        Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
        Check(e.State.PendingChoice?.Type != "entryReaction", "a mandatory trait: no optional window");
        // A created card leaves the game, never a pile.
        e.DestroyUnitsForTest(zombie.Id);
        Eq(e.State.Cards[zombie.Id].Zone, Zone.RemovedFromGame, "the destroyed Zombie is removed from the game");
        // An Abdollah put into play directly did not enter play: nothing is created.
        var s = Table(Phase.Action);
        Add(s, Abdollah, 0, Zone.Home);
        Eq(Of(MatchEngine.FromTestState(s), Zombie).Length, 0, "no Zombie without an entry");
    }

    // ---- Kitsune Kichi - exp and the Shadow Badger ----

    private static void KichiExpAndBadger()
    {
        var (e, kichi) = Recruit(KichiExp, st => Gold(st, 0, 6));
        var honor = Honor(e, 0);
        Purchase(e, 0, kichi.Id);
        Eq(e.State.Cards[kichi.Id].Zone, Zone.Home, "Kichi entered play");
        Eq(Honor(e, 0), honor - 3, "lose 3 Honor");
        var badger = Of(e, ShadowBadger).Single();
        Check(badger.OwnerId == 0 && badger.Zone == Zone.Home, "one Shadow Badger at home");
        var d = Printed(badger);
        Check(d.Force == 4 && d.Chi == 2 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Badger", "Nonhuman", "Shadowlands", "Spirit"]) && d.Abilities!.SequenceEqual(["proxy677-badger-melee"]),
            "a 4F/2C/0PH Badger Nonhuman Shadowlands Spirit with its Battle action");

        // Battle, Bow: Melee 4 Attack.
        foreach (var (extraForce, dies) in new[] { (0, true), (1, false) })
        {
            var s = Table();
            var mine = Add(s, ShadowBadger, 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); // Force 4
            Batch336Support.Force(enemy, extraForce);
            var b = MatchEngine.FromTestState(s);
            Eq(b.CardForceForTest(enemy.Id), 4 + extraForce, "the enemy's Force");
            Accept(b, Batch336Support.Offer(b, 0, CreatedProxies677Abilities.BadgerMelee, mine.Id, enemy.Id));
            Check(b.State.Cards[mine.Id].Bowed, "the Badger bows as the cost");
            Eq(b.State.Cards[enemy.Id].Zone == Zone.Dead, dies, $"a Melee 4 Attack against Force {4 + extraForce}");
            Check(!CanUse(b, 0, CreatedProxies677Abilities.BadgerMelee, mine.Id), "bowed, it cannot attack again");
        }
        var t = Table();
        var bowed = Add(t, ShadowBadger, 0, Zone.Battlefield, bowed: true, province: 0);
        var target = Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, CreatedProxies677Abilities.BadgerMelee, bowed.Id), "a bowed Badger cannot pay the bow");
        NoChange(f, Use(f, 0, CreatedProxies677Abilities.BadgerMelee, bowed.Id, target.Id), "a bowed Badger");
    }

    // ---- Kuni Renyu - exp2 ----

    private static void RenyuDogAndBow()
    {
        var (e, renyu) = Recruit(Renyu, st => { st.Players[0].Clan = "Crab"; Gold(st, 0, 8); });
        var cards = e.State.Cards.Count;
        Purchase(e, 0, renyu.Id);
        DeclineProclaim(e);
        Eq(e.State.Cards[renyu.Id].Zone, Zone.Home, "Renyu entered play");
        var dog = AttachedTo(e, e.State.Cards[renyu.Id], Dog).Single();
        var d = Printed(dog);
        Check(d.Type == CardType.Follower && d.Force == 1 && new HashSet<string>(d.Keywords!).SetEquals(["Dog", "Nonhuman"]) && dog.OwnerId == 0, "a 1F Dog Nonhuman Follower attached to him");
        Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
        Check(e.State.PendingChoice?.Type != "entryReaction", "a mandatory trait: no optional window");

        // Earth Battle: Bow a target enemy card with Shadowlands or without attachments.
        var s = Table();
        var mine = Add(s, Renyu, 0, Zone.Battlefield, province: 0);
        var bare = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var host = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var follower = Add(s, TrainingFollower, 1, Zone.Attached, province: 0, parent: host.Id);
        var shadowHost = Add(s, KichiExp, 1, Zone.Battlefield, province: 0); // a Personality with the Shadowlands keyword, with an attachment
        Add(s, TrainingFollower, 1, Zone.Attached, province: 0, parent: shadowHost.Id);
        var own = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var option = Enabled(b, 0, CreatedProxies677Abilities.RenyuBow, mine.Id);
        Check(option.TargetIds!.Contains(bare.Id) && option.TargetIds.Contains(follower.Id) && option.TargetIds.Contains(shadowHost.Id) && !option.TargetIds.Contains(host.Id) && !option.TargetIds.Contains(own.Id),
            $"an enemy card with Shadowlands, or without attachments (got {string.Join(",", option.TargetIds!)})");
        NoChange(b, Use(b, 0, CreatedProxies677Abilities.RenyuBow, mine.Id, host.Id), "an enemy Personality with an attachment and no Shadowlands");
        NoChange(b, Use(b, 0, CreatedProxies677Abilities.RenyuBow, mine.Id, own.Id), "his own side's card");
        Accept(b, Use(b, 0, CreatedProxies677Abilities.RenyuBow, mine.Id, shadowHost.Id));
        Check(b.State.Cards[shadowHost.Id].Bowed, "the Shadowlands Personality is bowed although he has an attachment");
        Check(!b.State.Cards[mine.Id].Bowed && !b.State.Cards[bare.Id].Bowed, "no cost, and no one else is bowed");
        Check(!CanUse(b, 0, CreatedProxies677Abilities.RenyuBow, mine.Id), "once a turn");
    }

    // ---- Hiruma Koru ----

    private static void KoruInvest()
    {
        foreach (var invest in new[] { true, false })
        {
            var (e, koru) = Recruit(Koru, st => { st.Players[0].Clan = "Crab"; Gold(st, 0, 6); });
            Purchase(e, 0, koru.Id);
            EntryWindowOpen(e);
            var cards = e.State.Cards.Count;
            if (invest)
            {
                NoChange(e, Use(e, 0, CreatedProxies677Abilities.KoruInvest, koru.Id), "the Invest unpaid");
                Accept(e, Paid(e, 0, 1, Use(e, 0, CreatedProxies677Abilities.KoruInvest, koru.Id)));
                var retinue = AttachedTo(e, e.State.Cards[koru.Id], Retinue).Single();
                var d = Printed(retinue);
                Check(d.Type == CardType.Follower && d.Force == 0 && (d.Keywords ?? []).Length == 0 && d.Name == "Disposable Troops", "a 0F Follower with no keyword, attached to Koru");
                Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
                Check(!CanUse(e, 0, CreatedProxies677Abilities.KoruInvest, koru.Id), "once");
            }
            else
            {
                Accept(e, new GameCommand("declineReaction", 0, e.Version));
                Eq(e.State.Cards.Count, cards, "declining the Invest creates nothing");
            }
        }
        // Invest is only for the moment of entering play.
        var s = Table(Phase.Action);
        var inPlay = Add(s, Koru, 0, Zone.Home);
        Gold(s, 0);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, CreatedProxies677Abilities.KoruInvest, inPlay.Id), "Invest is paid only as the card enters play");
    }

    // ---- Kitsune Yamazaru and the Monkey ----

    private static void YamazaruAndMonkey()
    {
        foreach (var create in new[] { true, false })
        {
            var (e, yamazaru) = Recruit(Yamazaru, st => { st.Players[0].Honor = 10; Gold(st, 0, 4); });
            var cards = e.State.Cards.Count;
            Purchase(e, 0, yamazaru.Id);
            EntryWindowOpen(e);
            if (create)
            {
                Accept(e, Use(e, 0, CreatedProxies677Abilities.YamazaruCreate, yamazaru.Id));
                var monkey = Of(e, Monkey).Single();
                var d = Printed(monkey);
                Check(monkey.Zone == Zone.Home && monkey.OwnerId == 0 && d.Force == 3 && d.Chi == 2 && d.PersonalHonor == 3 && new HashSet<string>(d.Keywords!).SetEquals(["Mantis Clan", "Monkey", "Nonhuman", "Spirit"]),
                    "a 3F/2C/3PH Mantis Clan Monkey Nonhuman Spirit at home");
                Check(d.Abilities!.SequenceEqual(["proxy677-monkey-weaken"]), "with its Battle action");
                Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
            }
            else
            {
                Accept(e, new GameCommand("declineReaction", 0, e.Version));
                Eq(e.State.Cards.Count, cards, "declining the Interrupt creates nothing");
            }
        }
        var t = Table(Phase.Action);
        var inPlay = Add(t, Yamazaru, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, CreatedProxies677Abilities.YamazaruCreate, inPlay.Id), "not an action of a Yamazaru already in play");

        // The Monkey: "Battle: Discard a card to give a target enemy Personality or Follower a Force penalty equal to the card's Focus Value."
        var s = Table();
        var mine = Add(s, Monkey, 0, Zone.Battlefield, province: 0);
        var card = Add(s, FocusThree, 0, Zone.Hand); // Focus Value 3
        var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); // Force 4
        var own = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Enabled(b, 0, CreatedProxies677Abilities.MonkeyWeaken, mine.Id);
        NoChange(b, Use(b, 0, CreatedProxies677Abilities.MonkeyWeaken, mine.Id, null, Pick("discard", card.Id), Pick("target", own.Id)), "his own side's card");
        NoChange(b, Use(b, 0, CreatedProxies677Abilities.MonkeyWeaken, mine.Id, null, Pick("target", enemy.Id)), "no card to discard");
        Accept(b, Use(b, 0, CreatedProxies677Abilities.MonkeyWeaken, mine.Id, null, Pick("discard", card.Id), Pick("target", enemy.Id)));
        Eq(b.State.Cards[card.Id].Zone, Zone.FateDiscard, "the card is discarded");
        Eq(b.CardForceForTest(enemy.Id), 1, "Force 4 less the Focus Value 3");
        Check(!b.State.Cards[mine.Id].Bowed, "the Monkey does not bow");
    }

    // ---- Komori Miyano ----

    private static void MiyanoBat()
    {
        var s = Table(Phase.Action, 1);
        var miyano = Add(s, Miyano, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var cards = e.State.Cards.Count;
        Accept(e, Use(e, 1, CreatedProxies677Abilities.MiyanoBat, miyano.Id));
        var bat = Of(e, Bat).Single();
        var d = Printed(bat);
        Check(bat.Zone == Zone.Home && bat.OwnerId == 1 && d.Force == 0 && d.Chi == 2 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Bat", "Nonhuman", "Cavalry"]),
            "a 0F/2C/0PH Bat Nonhuman Cavalry Personality at home");
        Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
        Check(!e.State.Cards[miyano.Id].Bowed, "no cost");
        Check(e.State.Events.Any(x => x.Type == "timedRemovalScheduled" && x.CardId == bat.Id), "the pending removal is announced");
        // The other player's turn: still in play. Her controller's next turn: removed.
        var start = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber == start + 1 && x.State.Phase == Phase.Action);
        Eq(e.State.Cards[bat.Id].Zone, Zone.Home, "still in play during the other player's turn");
        Passing(e, x => x.State.TurnNumber == start + 2);
        Eq(e.State.Cards[bat.Id].Zone, Zone.RemovedFromGame, "removed from the game when her controller's next turn began");

        // Once a turn; a Limited action, so not in a battle and not on the other player's turn.
        var s2 = Table(Phase.Action, 1);
        var again = Add(s2, Miyano, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 1, CreatedProxies677Abilities.MiyanoBat, again.Id));
        Check(!CanUse(e2, 1, CreatedProxies677Abilities.MiyanoBat, again.Id), "once a turn");
        var s3 = Table(Phase.Battle, 1);
        var inBattle = Add(s3, Miyano, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 1, CreatedProxies677Abilities.MiyanoBat, inBattle.Id), "a Limited action is not taken in a battle");
    }

    // ---- Daigotsu Hashibei ----

    private static void HashibeiFollower()
    {
        var s = Table(Phase.Action);
        var hashibei = Add(s, Hashibei, 0, Zone.Home);
        var mine = Add(s, CraneSamurai, 0, Zone.Home);
        var enemy = Add(s, LionSamurai, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, CreatedProxies677Abilities.HashibeiCreate, hashibei.Id);
        Check(option.TargetIds!.Contains(mine.Id) && option.TargetIds.Contains(hashibei.Id) && !option.TargetIds.Contains(enemy.Id), "your Personality, Hashibei himself included; never an enemy");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.HashibeiCreate, hashibei.Id, enemy.Id), "an enemy Personality");
        Accept(e, Use(e, 0, CreatedProxies677Abilities.HashibeiCreate, hashibei.Id, mine.Id));
        Check(e.State.Cards[hashibei.Id].Bowed, "Hashibei bows as the cost");
        var follower = AttachedTo(e, e.State.Cards[mine.Id], Conscripts).Single();
        Check(Printed(follower).Force == 1 && (Printed(follower).Keywords ?? []).Length == 0, "a 1F Follower with no keyword on the target");
        Check(!CanUse(e, 0, CreatedProxies677Abilities.HashibeiCreate, hashibei.Id), "bowed (and used), he cannot again");

        var s2 = Table(Phase.Action);
        var bowed = Add(s2, Hashibei, 0, Zone.Home, bowed: true);
        var target = Add(s2, CraneSamurai, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, CreatedProxies677Abilities.HashibeiCreate, bowed.Id), "a bowed Hashibei cannot pay the bow");
        NoChange(e2, Use(e2, 0, CreatedProxies677Abilities.HashibeiCreate, bowed.Id, target.Id), "a bowed Hashibei");
    }

    // ---- Shinjo Tselu - exp2 ----

    private static void TseluCavalry()
    {
        var s = Table(Phase.Action);
        var tselu = Add(s, Tselu, 0, Zone.Home);
        var mine = Add(s, CraneSamurai, 0, Zone.Home);
        var enemy = Add(s, LionSamurai, 1, Zone.Home);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        Eq(Enabled(e, 0, CreatedProxies677Abilities.TseluCreate, tselu.Id).Gold?.Cost, 2, "2 Gold");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.TseluCreate, tselu.Id, mine.Id), "unpaid");
        NoChange(e, Paid(e, 0, 2, Use(e, 0, CreatedProxies677Abilities.TseluCreate, tselu.Id, enemy.Id)), "an enemy Personality");
        Accept(e, Paid(e, 0, 2, Use(e, 0, CreatedProxies677Abilities.TseluCreate, tselu.Id, mine.Id)));
        var follower = AttachedTo(e, e.State.Cards[mine.Id], DutifulCavalry).Single();
        Check(Printed(follower).Force == 1 && new HashSet<string>(Printed(follower).Keywords!).SetEquals(["Cavalry"]), "a 1F Cavalry Follower on the target");
        Check(!e.State.Cards[tselu.Id].Bowed, "he does not bow");
        Check(!CanUse(e, 0, CreatedProxies677Abilities.TseluCreate, tselu.Id), "once a turn");
    }

    // ---- Goju Kaxt ----

    private static void KaxtNinjaForce()
    {
        MatchEngine Build(bool withKaxt, bool second, out CardInstance a, out CardInstance b, out CardInstance c, out CardInstance home, out CardInstance enemy, out CardInstance? kaxt, out CardInstance? kaxt2)
        {
            var s = Table();
            a = Add(s, NinjaSource, 0, Zone.Battlefield, province: 0); // a Ninja Personality, Force 2
            b = Add(s, NinjaSource, 0, Zone.Battlefield, province: 0);
            c = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); // not a Ninja
            home = Add(s, NinjaSource, 0, Zone.Home);
            enemy = Add(s, NinjaSource, 1, Zone.Battlefield, province: 0);
            kaxt = withKaxt ? Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: a.Id) : null;
            kaxt2 = second ? Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: b.Id) : null;
            return MatchEngine.FromTestState(s);
        }
        var plain = Build(false, false, out var a0, out var b0, out var c0, out var h0, out var x0, out _, out _);
        var one = Build(true, false, out var a1, out var b1, out var c1, out var h1, out var x1, out var k1, out _);
        Eq(one.CardForceForTest(k1!.Id), 3, "Kaxt's own Force is his printed 3: he is not another Ninja");
        Eq(one.CardForceForTest(a1.Id), plain.CardForceForTest(a0.Id) + 1, "his own Personality, a Ninja in the army, has +1F");
        Eq(one.CardForceForTest(b1.Id), plain.CardForceForTest(b0.Id) + 1, "another Ninja Personality in the army has +1F");
        Eq(one.CardForceForTest(c1.Id), plain.CardForceForTest(c0.Id), "a Personality who is not a Ninja has none");
        Eq(one.CardForceForTest(h1.Id), plain.CardForceForTest(h0.Id), "a Ninja at home is not in this army");
        Eq(one.CardForceForTest(x1.Id), plain.CardForceForTest(x0.Id), "the enemy's Ninja is not in this army");
        var two = Build(true, true, out var a2, out _, out _, out _, out _, out var k2a, out var k2b);
        Eq(two.CardForceForTest(k2a!.Id), 4, "a second Goju Kaxt gives the first +1F");
        Eq(two.CardForceForTest(k2b!.Id), 4, "and the first gives the second +1F");
        Eq(two.CardForceForTest(a2.Id), plain.CardForceForTest(a0.Id) + 2, "a Ninja Personality has +1F from each");
        // At home there is no army.
        var s = Table(Phase.Action);
        var host = Add(s, NinjaSource, 0, Zone.Home);
        Add(s, GojuKaxt, 0, Zone.Attached, parent: host.Id);
        var other = Add(s, NinjaSource, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(other.Id), plain.CardForceForTest(h0.Id), "no bonus at home");
    }

    private static void KaxtCreatesKaxt()
    {
        // Destroyed alone by another player's Battle action: the trait is offered once the action has resolved, at home or at the battlefield.
        foreach (var atBattle in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1);
            var host = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            var kaxt = Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: host.Id);
            var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, kaxt.Id));
            Eq(e.State.Cards[kaxt.Id].Zone, Zone.FateDiscard, "Goju Kaxt was destroyed");
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window is open");
            Check(CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id) && CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "both places are offered");
            var cards = e.State.Cards.Count;
            Accept(e, Use(e, 0, atBattle ? CreatedProxies677Abilities.KaxtCreateBattle : CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id));
            var created = Of(e, Kaxt).Single();
            var d = Printed(created);
            Check(created.OwnerId == 0 && d.Type == CardType.Personality && d.Force == 4 && d.Chi == 3 && d.PersonalHonor == 0 && d.Keywords!.Contains("Ninja"), "a 4F/3C/0PH Ninja Personality");
            Check(d.Clan == "Crane" && d.Keywords!.Contains("Crane Clan"), $"with his controller's Clan alignment ({d.Clan}: {string.Join(",", d.Keywords!)})");
            if (atBattle) Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0, "at the current battlefield");
            else Eq(created.Zone, Zone.Home, "at home");
            Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
            Eq(e.State.Cards[kaxt.Id].Zone, Zone.FateDiscard, "Goju Kaxt stays in the discard pile");
            Check(!CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id) && !CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "once: one Kaxt for one destruction");
        }

        // Destroyed with his Personality by his own controller's action (The Onyx Dueling Circle): also "destroyed during the action". No battle, so only his home is offered.
        {
            var s = Table(Phase.Action);
            var circle = Add(s, OnyxCircle, 0, Zone.Home);
            var host = Add(s, Shugenja, 0, Zone.Home);
            var kaxt = Add(s, GojuKaxt, 0, Zone.Attached, parent: host.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, CreatedProxies677Abilities.OnyxCircle, circle.Id, host.Id));
            Check(e.State.Cards[host.Id].Zone == Zone.Dead && e.State.Cards[kaxt.Id].Zone == Zone.FateDiscard, "the Shugenja and Goju Kaxt were destroyed");
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window is open");
            Check(CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id) && !CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "home only: there is no battlefield");
            NoChange(e, Use(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "no current battlefield");
            Accept(e, Use(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id));
            Eq(Of(e, Kaxt).Single().Zone, Zone.Home, "Kaxt is created at home");
        }

        // Declined, the trait is not owed later: an action that destroys his old Personality while he lies in the discard pile is not an action during which he was destroyed.
        {
            var s = Table(Phase.Battle, 1);
            var host = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            var kaxt = Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: host.Id);
            var first = Add(s, KaiuEsumi, 1, Zone.Battlefield, province: 0); // "Battle: Destroy a target enemy attachment."
            var second = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Offer(e, 1, "komatsu-destroy-attachment", first.Id, kaxt.Id));
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window is open");
            Check(CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id), "destroyed as an attachment, he is offered his trait");
            Eq(e.State.Cards[kaxt.Id].LastHostId, host.Id, "the engine remembers the Personality he was attached to");
            Passing(e, x => x.State.PendingChoice is null && x.State.PriorityPlayerId == 1);
            Eq(Of(e, Kaxt).Length, 0, "declined: no Kaxt");
            Accept(e, Batch336Support.Offer(e, 1, "training-destroy-no-attachments", second.Id, host.Id));
            Eq(e.State.Cards[host.Id].Zone, Zone.Dead, "his old Personality is destroyed by a later action");
            Check(!CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id) && !CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "nothing is offered for the later action");
        }

        // In play he has no such action.
        {
            var s = Table();
            var host = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            var kaxt = Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: host.Id);
            Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateHome, kaxt.Id) && !CanUse(e, 0, CreatedProxies677Abilities.KaxtCreateBattle, kaxt.Id), "not while he is in play");
        }
    }

    // ---- Shard of the Great Death's Bones ----

    private static void ShardBirds()
    {
        foreach (var hosts in new[] { 2, 1, 3, 0 })
        {
            var s = Table(Phase.Action);
            var a = Add(s, CraneSamurai, 0, Zone.Home);
            var b = Add(s, CraneSamurai, 0, Zone.Home);
            var c = Add(s, CraneSamurai, 0, Zone.Home);
            var enemy = Add(s, LionSamurai, 1, Zone.Home);
            var shard = Add(s, Shard, 0, Zone.Hand);
            Gold(s, 0);
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, new GameCommand("equip", 0, e.Version, CardId: shard.Id, SourceIds: e.PaymentOptions(0, 3).First().SourceIds, TargetId: a.Id));
            Eq(e.State.Cards[shard.Id].AttachedTo, a.Id, "the Item is attached");
            Eq(Honor(e, 0), honor - 2, "lose 2 Honor, whatever is chosen next");
            Eq(e.State.PendingChoice?.Type, "entryReaction", "its entering-play window is open");
            if (hosts == 0)
            {
                Accept(e, new GameCommand("declineReaction", 0, e.Version));
                Eq(Of(e, BirdOfPrey).Length, 0, "declined: no Followers");
                continue;
            }
            NoChange(e, Use(e, 0, CreatedProxies677Abilities.ShardCreate, shard.Id, null, Pick("hosts", enemy.Id)), "an enemy Personality");
            var chosen = new[] { a.Id, b.Id, c.Id }.Take(hosts).ToArray();
            Accept(e, Use(e, 0, CreatedProxies677Abilities.ShardCreate, shard.Id, null, Pick("hosts", chosen)));
            var birds = Of(e, BirdOfPrey);
            Eq(birds.Length, 3, "three Followers are created");
            Check(birds.All(x => x.Zone == Zone.Attached && Printed(x).Force == 1 && new HashSet<string>(Printed(x).Keywords!).SetEquals(["Nonhuman", "Raptor", "Shadowlands", "Undead", "Cavalry"])),
                "each a 1F Nonhuman Raptor Shadowlands Undead Cavalry Follower");
            var counts = new[] { a, b, c }.Select(p => birds.Count(x => x.AttachedTo == p.Id)).ToArray();
            Eq(string.Join(",", counts), hosts switch { 1 => "3,0,0", 2 => "2,1,0", _ => "1,1,1" }, "dealt to the chosen Personalities in turn");
            Check(!CanUse(e, 0, CreatedProxies677Abilities.ShardCreate, shard.Id), "once");
        }
    }

    // ---- Yojimbo of Earth ----

    private static void YojimboOfEarthFollower()
    {
        var s = Table(Phase.Action);
        var caster = Add(s, Shugenja, 0, Zone.Home);
        var spell = Add(s, YojimboOfEarth, 0, Zone.Attached, parent: caster.Id);
        var other = Add(s, Shugenja, 0, Zone.Home);
        var guarded = Add(s, Shugenja, 0, Zone.Home);
        Add(s, TrainingFollower, 0, Zone.Attached, parent: guarded.Id);
        var samurai = Add(s, CraneSamurai, 0, Zone.Home);
        var enemy = Add(s, Shugenja, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id);
        Check(option.TargetIds!.Contains(caster.Id) && option.TargetIds.Contains(other.Id) && !option.TargetIds.Contains(guarded.Id) && !option.TargetIds.Contains(samurai.Id) && !option.TargetIds.Contains(enemy.Id),
            $"your Shugenja without Followers only (got {string.Join(",", option.TargetIds!)})");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id, guarded.Id), "a Shugenja with a Follower");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id, samurai.Id), "not a Shugenja");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id, enemy.Id), "an enemy Shugenja");
        Accept(e, Use(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id, other.Id));
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[caster.Id].Bowed, "the Spell bows as the cost; the Shugenja does not");
        var follower = AttachedTo(e, e.State.Cards[other.Id], ClayYojimbo).Single();
        Check(Printed(follower).Force == 1 && new HashSet<string>(Printed(follower).Keywords!).SetEquals(["Clay Soldier", "Nonhuman", "Yojimbo"]), "a 1F Clay Soldier Nonhuman Yojimbo Follower on the target");
        Check(!CanUse(e, 0, CreatedProxies677Abilities.YojimboEarth, spell.Id), "bowed, it cannot be used again");
    }

    // ---- Ashalan Ambush ----

    private static void AshalanAmbushCreates()
    {
        var s = Table(Phase.Action);
        var card = Add(s, AshalanAmbush, 0, Zone.Hand);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, CreatedProxies677Abilities.AshalanCreate, card.Id);
        Eq(option.Gold?.Cost, 8, "8 Gold");
        NoChange(e, option.Command! with { SourceIds = [] }, "unpaid");
        Accept(e, option.Command! with { SourceIds = e.PaymentOptions(0, 8).First().SourceIds });
        var created = Of(e, Ashalan).Single();
        var d = Printed(created);
        Check(created.Zone == Zone.Home && created.OwnerId == 0 && d.Force == 4 && d.Chi == 4 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Ashalan", "Nonhuman", "Shugenja"]),
            "a 4F/4C/0PH Ashalan Nonhuman Shugenja Personality at home");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        var battle = Table();
        var held = Add(battle, AshalanAmbush, 0, Zone.Hand);
        Add(battle, CraneSamurai, 0, Zone.Battlefield, province: 0);
        Gold(battle, 0);
        Check(!CanUse(MatchEngine.FromTestState(battle), 0, CreatedProxies677Abilities.AshalanCreate, held.Id), "a Limited action is not taken in a battle");
    }

    // ---- New Cavalry Tactics ----

    private static void CavalryTacticsElephants()
    {
        foreach (var commander in new[] { false, true })
        {
            var s = Table(Phase.BattleSelection); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            var card = Add(s, CavalryTactics, 0, Zone.Hand);
            var target = Add(s, commander ? Hashibei : CraneSamurai, 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            Gold(s, 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, CreatedProxies677Abilities.CavalryTactics, card.Id), "not before the battle is engaged");
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
            var option = Enabled(e, 0, CreatedProxies677Abilities.CavalryTactics, card.Id);
            Eq(option.Gold?.Cost, 3, "3 Gold");
            Check(!option.TargetIds!.Contains(enemy.Id), "an enemy Personality is no target");
            NoChange(e, option.Command! with { TargetId = enemy.Id, SourceIds = e.PaymentOptions(0, 3).First().SourceIds }, "an enemy Personality");
            Accept(e, option.Command! with { TargetId = target.Id, SourceIds = e.PaymentOptions(0, 3).First().SourceIds });
            var elephants = AttachedTo(e, e.State.Cards[target.Id], Elephant);
            Eq(elephants.Length, commander ? 3 : 2, commander ? "three for a Commander" : "two");
            Check(elephants.All(x => Printed(x).Force == 1 && new HashSet<string>(Printed(x).Keywords!).SetEquals(["Elephant", "Nonhuman", "Cavalry"])), "each a 1F Elephant Nonhuman Cavalry Follower");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
    }

    // ---- A New Perspective ----

    private static void NewPerspectiveFollowers()
    {
        foreach (var opposed in new[] { true, false })
        {
            var s = Table();
            var a = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            var b = Add(s, CraneSamurai, 0, Zone.Battlefield, bowed: true, province: 0);
            var home = Add(s, CraneSamurai, 0, Zone.Home);
            if (opposed) Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            var card = Add(s, NewPerspective, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, CreatedProxies677Abilities.NewPerspective, card.Id);
            Eq(option.Gold?.Cost, 2, "2 Gold");
            NoChange(e, option.Command! with { SourceIds = [] }, "unpaid");
            Accept(e, option.Command! with { SourceIds = e.PaymentOptions(0, 2).First().SourceIds });
            Eq(AttachedTo(e, e.State.Cards[a.Id], Conscripts).Length, opposed ? 1 : 0, "one on an opposed Personality at the battlefield");
            Eq(AttachedTo(e, e.State.Cards[b.Id], Conscripts).Length, opposed ? 1 : 0, "one on each, bowed or not");
            Eq(AttachedTo(e, e.State.Cards[home.Id], Conscripts).Length, 0, "none on a Personality at home");
            Eq(Of(e, Conscripts).Length, opposed ? 2 : 0, opposed ? "two in all" : "no enemy unit: no Personality is opposed, and nothing is created");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
    }

    // ---- Recruitment Effort ----

    private static void RecruitmentEffortSamurai()
    {
        var s = Table(Phase.Action);
        var card = Add(s, RecruitmentEffort, 0, Zone.Hand);
        var holding = Add(s, Marketplace, 0, Zone.Home);
        var unique = Add(s, OnyxCircle, 0, Zone.Home);
        var enemy = Add(s, Marketplace, 1, Zone.Home);
        Gold(s, 0, 3);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, CreatedProxies677Abilities.RecruitmentEffort, card.Id);
        Eq(option.Gold?.Cost, 3, "its printed Gold Cost is 3");
        Check(option.TargetIds!.Contains(holding.Id) && !option.TargetIds.Contains(unique.Id) && !option.TargetIds.Contains(enemy.Id), "your non-Unique Holding only");
        var sources = e.PaymentOptions(0, 3).First(p => !p.SourceIds.Contains(holding.Id)).SourceIds;
        NoChange(e, option.Command! with { TargetId = unique.Id, SourceIds = sources }, "a Unique Holding");
        NoChange(e, option.Command! with { TargetId = enemy.Id, SourceIds = sources }, "another player's Holding");
        Accept(e, option.Command! with { TargetId = holding.Id, SourceIds = sources });
        Check(e.State.Cards[holding.Id].Zone is not (Zone.Home or Zone.Attached), $"the Holding is destroyed as the cost ({e.State.Cards[holding.Id].Zone})");
        var created = Of(e, HiredSamurai).Single();
        var d = Printed(created);
        Check(created.Zone == Zone.Home && created.OwnerId == 0 && d.Force == 3 && d.Chi == 3 && d.PersonalHonor == 2 && new HashSet<string>(d.Keywords!).SetEquals(["Samurai"]), "a 3F/3C/2PH Samurai Personality at home");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
    }

    // ---- The Onyx Dueling Circle ----

    private static void OnyxCircleOni()
    {
        var s = Table(Phase.Action);
        var circle = Add(s, OnyxCircle, 0, Zone.Home);
        var shugenja = Add(s, Shugenja, 0, Zone.Home);
        var samurai = Add(s, CraneSamurai, 0, Zone.Home);
        var enemy = Add(s, Shugenja, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.PaymentOptions(0, 2).Any(p => p.SourceIds.SequenceEqual([circle.Id])), "it bows to produce 2 Gold");
        var honor = Honor(e, 0);
        var option = Enabled(e, 0, CreatedProxies677Abilities.OnyxCircle, circle.Id);
        Check(option.TargetIds!.Contains(shugenja.Id) && !option.TargetIds.Contains(samurai.Id) && !option.TargetIds.Contains(enemy.Id), "your Shugenja only");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.OnyxCircle, circle.Id, samurai.Id), "not a Shugenja");
        NoChange(e, Use(e, 0, CreatedProxies677Abilities.OnyxCircle, circle.Id, enemy.Id), "an enemy Shugenja");
        Accept(e, Use(e, 0, CreatedProxies677Abilities.OnyxCircle, circle.Id, shugenja.Id));
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Dead, "the Shugenja is destroyed");
        var oni = Of(e, OnyxOni).Single();
        var d = Printed(oni);
        Check(oni.Zone == Zone.Home && oni.OwnerId == 0 && d.Force == 5 && d.Chi == 2 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Nonhuman", "Oni", "Shadowlands"]),
            "a 5F/2C/0PH Nonhuman Oni Shadowlands Personality at home");
        Check(e.State.Cards[circle.Id].Zone is not (Zone.Home or Zone.Attached), $"the Holding is destroyed ({e.State.Cards[circle.Id].Zone})");
        Eq(Honor(e, 0), honor - 4, "lose 4 Honor");

        // Bowed (it produced Gold), the Holding cannot pay the bow.
        var s2 = Table(Phase.Action);
        var bowed = Add(s2, OnyxCircle, 0, Zone.Home, bowed: true);
        var mine = Add(s2, Shugenja, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, CreatedProxies677Abilities.OnyxCircle, bowed.Id), "a bowed Holding cannot pay the bow");
        NoChange(e2, Use(e2, 0, CreatedProxies677Abilities.OnyxCircle, bowed.Id, mine.Id), "a bowed Holding");
    }

    // ---- Never Without a Weapon ----

    private static void NeverWithoutAWeapon()
    {
        MatchState Position(out CardInstance card, out CardInstance focus, out CardInstance mine, out CardInstance enemy)
        {
            var s = Table();
            card = Add(s, NeverWithoutWeapon, 0, Zone.Hand);
            focus = Add(s, FocusThree, 0, Zone.Hand); // Focus Value 3
            mine = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            Gold(s, 0);
            return s;
        }
        var e = MatchEngine.FromTestState(Position(out var card, out var focus, out var mine, out var enemy));
        var option = Enabled(e, 0, CreatedProxies677Abilities.NeverWithoutWeapon, card.Id);
        Eq(option.Gold?.Cost, 5, "5 Gold");
        var pay = e.PaymentOptions(0, 5).First().SourceIds;
        GameCommand Play(string discard, string target, string[]? sources) => option.Command! with { TargetId = null, Selections = [Pick("discard", discard), Pick("target", target)], SourceIds = sources };
        NoChange(e, Play(focus.Id, mine.Id, []), "unpaid");
        NoChange(e, Play(card.Id, mine.Id, pay), "the Strategy being played is not a card to discard");
        NoChange(e, Play(focus.Id, enemy.Id, pay), "an enemy Personality");
        var force = e.CardForceForTest(mine.Id);
        Accept(e, Play(focus.Id, mine.Id, pay));
        Eq(e.State.Cards[focus.Id].Zone, Zone.FateDiscard, "the card is discarded");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        var weapon = AttachedTo(e, e.State.Cards[mine.Id], UnexpectedWeapon).Single();
        var d = Printed(weapon);
        Check(d.Type == CardType.Item && d.Force == 3 && d.Chi == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Weapon"]), $"a Weapon Item whose Force Modifier is the Focus Value 3 (+{d.Force}F/+{d.Chi}C)");
        Eq(e.CardForceForTest(mine.Id), force + 3, "its Personality has +3F");
        Eq(CardCatalog.Get(UnexpectedWeapon).Force, 0, "the catalogue's scan is unchanged: the value is this card's own");

        // A Personality carries one Weapon: a second one is refused before anything is paid or discarded.
        var s2 = Position(out var card2, out var focus2, out var armed, out _);
        Add(s2, UnexpectedWeapon, 0, Zone.Attached, province: 0, parent: armed.Id);
        var e2 = MatchEngine.FromTestState(s2);
        NoChange(e2, Batch336Support.Use(e2, 0, CreatedProxies677Abilities.NeverWithoutWeapon, card2.Id, null, null, Pick("discard", focus2.Id), Pick("target", armed.Id)) with { SourceIds = e2.PaymentOptions(0, 5).First().SourceIds },
            "a Personality who already carries a Weapon");

        // No other card in hand: nothing to discard.
        var s3 = Table();
        var alone = Add(s3, NeverWithoutWeapon, 0, Zone.Hand);
        Add(s3, CraneSamurai, 0, Zone.Battlefield, province: 0);
        Add(s3, LionSamurai, 1, Zone.Battlefield, province: 0);
        Gold(s3, 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, CreatedProxies677Abilities.NeverWithoutWeapon, alone.Id), "no card to discard");
    }

    // ---- The Wind's Champion ----

    private static void WindsChampionIllusion()
    {
        // (the Shugenja, a Chi modifier on him, the Illusion's Force): Chi 3 and Air gives 4; Chi 3 without Air gives 3; his current Chi counts, not his printed one.
        foreach (var (shugenjaId, chiModifier, expected) in new[] { ("training-seeds5-air-shugenja", 0, 4), (Shugenja, 0, 3), ("training-seeds5-air-shugenja", 2, 6) })
        {
            var s = Table(Phase.Action);
            var caster = Add(s, shugenjaId, 0, Zone.Home);
            if (chiModifier != 0) caster.Modifiers.Add(new("test-source", "test", "Chi", chiModifier));
            var spell = Add(s, WindsChampion, 0, Zone.Attached, parent: caster.Id);
            var e = MatchEngine.FromTestState(s);
            Eq(e.PersonalityChiForTest(caster.Id), 3 + chiModifier, "the Shugenja's Chi");
            var cards = e.State.Cards.Count;
            Accept(e, Use(e, 0, CreatedProxies677Abilities.WindsChampion, spell.Id));
            Check(e.State.Cards[caster.Id].Bowed, "the Shugenja bows as a cost");
            Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell is destroyed");
            var illusion = Of(e, MajorIllusion).Single();
            var d = Printed(illusion);
            Check(illusion.Zone == Zone.Home && illusion.OwnerId == 0 && d.Chi == 2 && d.PersonalHonor == 0 && new HashSet<string>(d.Keywords!).SetEquals(["Illusion", "Samurai", "Spirit"]),
                "a 2C/0PH Illusion Samurai Spirit Personality at home");
            Eq(d.Force, expected, "Force equal to his Chi, plus 1 if he is Air");
            Eq(e.CardForceForTest(illusion.Id), expected, "and that is the Force it has");
            Eq(e.State.Cards.Count, cards + 1, "exactly one card is created");
        }
        Eq(CardCatalog.Get(MajorIllusion).Force, 0, "the catalogue's scan is unchanged: the value is the created card's own");

        // A bowed Shugenja cannot be bowed to pay, and a bowed Spell cannot either.
        foreach (var (casterBowed, spellBowed) in new[] { (true, false), (false, true) })
        {
            var s = Table(Phase.Action);
            var caster = Add(s, Shugenja, 0, Zone.Home, bowed: casterBowed);
            var spell = Add(s, WindsChampion, 0, Zone.Attached, bowed: spellBowed, parent: caster.Id);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, CreatedProxies677Abilities.WindsChampion, spell.Id), casterBowed ? "a bowed Shugenja cannot pay" : "a bowed Spell cannot pay");
            NoChange(e, Use(e, 0, CreatedProxies677Abilities.WindsChampion, spell.Id), "a cost that cannot be paid");
        }
    }
}
