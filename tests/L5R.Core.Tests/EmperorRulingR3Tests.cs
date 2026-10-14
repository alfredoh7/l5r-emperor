using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), Mendon's rulings, group R3: (1) province strength lasts until end of turn unless a card says "permanently" (every modifier is audited by the
// suites of the cards themselves, each of which now also reads the strength once the turn has ended -- Batch336Support.StrengthNextTurn -- and by the audit scan here); (2) The Sincerity of the
// Dragon follows the corrected scan; (3) Kaiu Village (region-wave-10) and the Kaiu Weapon Proxy it creates (item-wave-16, local id oracle-l5r-10006).
internal static class EmperorRulingR3Tests
{
    private const string KaiuVillage = "oracle-l5r-4148", KaiuWeapon = "oracle-l5r-10006", KaiuArmor = "oracle-l5r-10007", Sincerity = "oracle-l5r-8338", Courtesy = "oracle-l5r-7996",
        Nimuro = "oracle-l5r-10463", WillingSpirits = "oracle-l5r-9337", Hizumi = "oracle-l5r-8521";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static CardInstance Deck(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R3", [
        ("Published shape: region-wave-10 is Kaiu Village and item-wave-16 the CreatedOnly Kaiu Weapon (+1F/+1C, printed corners, local id); the corrected Sincerity of the Dragon prints the Dragon Clan player condition and the removal", PublishedShape),
        ("Province strength audit: no printed card's strength change is a raw permanent change but the three that say 'permanently' (Willing Spirits, Boastful Proclamation, Debt to the Kaiu); the timed record ends with the turn and the Region's is 'while attached'", ProvinceStrengthAudit),
        ("The Sincerity of the Dragon: the Focus Effect's +2 (and Courtesy of the Crane's) lasts until end of turn; the Open half needs a Dragon Clan player, and the card is removed from the game, not discarded", SincerityScenario),
        ("Kaiu Village: after the player's Personality enters play from a province, paying 2 Gold creates the +1F/+1C Weapon (Kaiu Weapon) or the +1F Armor (Kaiu Armor) attached to him; both may be used; each once", KaiuVillageScenario),
        ("Togashi Hizumi: his Battle half gives the current battlefield's province +4 or -4 strength (one use between the two) until end of turn, and only while he is at the battle", HizumiScenario),
        ("Kaiu Village edge cases: not for an entry that is not from a province, not for the other player's Personality or Region, not without 2 Gold, not when a Personality could not carry the Item", KaiuVillageEdges),
    ]);

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var village = CardCatalog.Get(KaiuVillage);
        Check(village.Type == CardType.Region && village.Name == "Kaiu Village" && village.GoldCost == 0 && string.Join(",", village.Keywords ?? []) == "Unique", "Kaiu Village: a Unique Region");
        Check(village.Abilities!.SequenceEqual(["kaiu-village-weapon", "kaiu-village-armor"]) && village.Effect == "none", "both Reactions are wired");
        Check(village.Text == "Reaction: After your performing Personality enters play from a province, pay 2 Gold: Create a +1F/+1C Weapon Item and attach it to him.\nReaction: After your performing Personality enters play from a province, pay 2 Gold: Create a +1F Armor Item and attach it to him.", "the scan's text");
        var weapon = CardCatalog.Get(KaiuWeapon);
        Check(weapon.Type == CardType.Item && weapon.Name == "Kaiu Weapon" && weapon.CreatedOnly && weapon.GoldCost == 0 && weapon.Force == 1 && weapon.Chi == 1 && string.Join(",", weapon.Keywords ?? []) == "Weapon"
            && weapon.Text == "May not be included in decks." && (weapon.Abilities ?? []).Length == 0, "Kaiu Weapon: a CreatedOnly Weapon Item, +1F/+1C, cost 0");
        var armor = CardCatalog.Get(KaiuArmor);
        Check(armor.CreatedOnly && armor.Force == 1 && armor.Chi == 0 && (armor.Keywords ?? []).Contains("Armor"), "Kaiu Armor is the +1F Armor");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["kaiu-village-weapon"].Creates!).Id, KaiuWeapon, "the Weapon Reaction creates Kaiu Weapon");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["kaiu-village-armor"].Creates!).Id, KaiuArmor, "the Armor Reaction creates Kaiu Armor");
        Check(CreatedCards.Verify().Length == 0, "the created-card pairings all resolve");
        foreach (var (wave, id) in new[] { ("region-wave-10", KaiuVillage), ("item-wave-16", KaiuWeapon) })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(listed.SequenceEqual([id]), $"{wave}'s manifest lists exactly {id}");
        }
        Eq(PublishedContent.RegionWave10.Cards.Length, 1, "region-wave-10 holds exactly Kaiu Village");
        Eq(PublishedContent.ItemWave16.Cards.Length, 1, "item-wave-16 holds exactly Kaiu Weapon");
        var sincerity = CardCatalog.Get(Sincerity);
        Check(sincerity.Text == "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2 strength.\nBattle/Open: If you are a Dragon Clan player, choose your performing unbowed Dragon Clan Personality: Name a number. After the next resolution this turn of your action from another Strategy or Ring with a base Focus Value equal to the number, draw a card, and choose a player, who discards a card. Remove this card from the game.",
            "The Sincerity of the Dragon prints the corrected scan's text");
        Check(AbilityCatalog.All["sincerity-dragon-name"].Label.StartsWith("If you are a Dragon Clan player,") && AbilityCatalog.All["sincerity-dragon-name"].Label.EndsWith("Remove this card from the game"), "and its ability says so");
    }

    // Every raw province Strength write in the engine is a "permanently" one: the audit is the source scan of those writes, kept as an explicit list so a new one must be justified here.
    private static void ProvinceStrengthAudit()
    {
        var core = Path.Combine(Batch336Support.RepoRoot(), "src/L5R.Core");
        var writers = new List<string>();
        foreach (var file in Directory.GetFiles(core, "*.cs"))
            foreach (var line in File.ReadAllLines(file))
            {
                var code = line.Split("//")[0];
                if (System.Text.RegularExpressions.Regex.IsMatch(code, @"[Pp]rovinces?(\[[^\]]*\])?\.Strength\s*[-+]=|province\.Strength\s*[-+]=|\.Strength\s*=\s*Math\.Max\(0, province")) writers.Add(Path.GetFileName(file));
            }
        // Boastful Proclamation (EmperorRemainingFEvents.cs), Willing Spirits (ForgottenLegacyBatch5.cs) and Debt to the Kaiu (SeedsOfDecayBatch2.cs) print "permanently".
        // Issue #503: Dark Experiments (EmperorP6.cs) prints "Permanently give it -2 strength", so its file joins the list.
        // Issue #674: The Second Battle of Beiden Pass (WlItemsMiscEvents.cs) prints "Until the game ends, ... has +10 Province Strength", a permanent change too.
        Eq(string.Join(",", writers.OrderBy(w => w, StringComparer.Ordinal)), "EmperorP6.cs,EmperorRemainingFEvents.cs,ForgottenLegacyBatch5.cs,SeedsOfDecayBatch2.cs,WlItemsMiscEvents.cs", "only the cards that say 'permanently' change a province's own strength");

        // Willing Spirits' permanence versus a timed change: a permanent +2 is still there after the turn, the timed record is not.
        var s = Table(Phase.Action, 0);
        var printed = MatchEngine.FromTestState(s).ProvinceStrengthForTest(0, 0);
        s.Players[0].Provinces[0].Strength += 2; // what a "permanently" card does
        var e = MatchEngine.FromTestState(s);
        Eq(e.ProvinceStrengthForTest(0, 0), printed + 2, "a permanent change is part of the province's strength");
        Eq(Batch336Support.StrengthNextTurn(e, 0, 0), printed + 2, "and survives the turn");
        Check(CardCatalog.Get(WillingSpirits).Text.Contains("permanently"), "Willing Spirits says permanently");
        // A Region's strength is "while attached": Fallow Lands is read live off the province's Region and goes with it (Program.cs' Fallow Lands suite pins the removal).
        Check(CardCatalog.Get("oracle-l5r-9865").Text.StartsWith("This province has +2 strength."), "Fallow Lands is a passive Region strength");

        // The view shows every one of those changes as the province's net modifier, so the board can mark it (Willing Spirits' +2 was never shown).
        var view = e.GetView(0).Players[0].Provinces;
        Check(printed > 0, "the table records a printed strength");
        Eq(view[0].StrengthModifier, 2, "a permanent change is in the view as a modifier");
        Check(view.Skip(1).All(p => p.StrengthModifier == 0 && p.StrengthNotes is null), "an unchanged province shows none");
        e.GiveProvinceStrengthForTest(0, 0, -5, null, "Ring of Earth");
        e.GiveProvinceStrengthForTest(0, 1, 3, null, "Ring of Earth");
        view = e.GetView(1).Players[0].Provinces;
        Eq(view[0].StrengthModifier, -3, "permanent and timed changes add up, for either seat");
        Eq(view[1].StrengthModifier, 3, "a timed raise is shown while it lasts");
        Check(view[1].StrengthNotes!.SequenceEqual(["Ring of Earth +3"]), "and names its source");
        e.GiveProvinceStrengthForTest(0, 2, -printed - 4, null, "Burn the Towers");
        Eq(e.GetView(0).Players[0].Provinces[2].StrengthModifier, -printed, "strength floors at zero, so the modifier cannot show more than the province lost");
    }

    private static void SincerityScenario()
    {
        // The Focus Effect (and Courtesy of the Crane's, the same words): the winner's provinces +2 -- until end of turn.
        foreach (var (card, name) in new[] { (Sincerity, "The Sincerity of the Dragon"), (Courtesy, "The Courtesy of the Crane") })
        {
            var s = Table(Phase.Battle, 0);
            var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); mine.Modifiers.Add(new("test-source", "test", "Chi", 9));
            var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var duelCard = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
            var focused = Add(s, card, 0, Zone.Hand);
            Deck(s, 1);
            var e = MatchEngine.FromTestState(s);
            var before = Enumerable.Range(0, 4).Select(i => e.ProvinceStrengthForTest(0, i)).ToArray();
            Accept(e, Batch336Support.Offer(e, 0, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            for (var i = 0; i < 4; i++)
            {
                Eq(e.ProvinceStrengthForTest(0, i), before[i] + 2, $"{name}: province {i + 1} has +2 strength");
                Eq(Batch336Support.StrengthNextTurn(e, 0, i), before[i], $"{name}: province {i + 1} is back once the turn ends (issue #492 ruling R3)");
            }
            Eq(e.ProvinceStrengthForTest(1, 0), e.State.Players[1].Provinces[0].Strength, $"{name}: the other player's provinces are untouched");
            // The raw printed value was never changed.
            Check(e.State.Players[0].Provinces.All(p => p.Strength == before[p.Index]), $"{name}: no permanent change");
        }
        // A duel its owner lost gives nothing.
        {
            var l = Table(Phase.Battle, 0);
            var weak = Add(l, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var strong = Add(l, "training-lion-samurai", 1, Zone.Battlefield, province: 0); strong.Modifiers.Add(new("test-source", "test", "Chi", 9));
            var duelCard = Add(l, "training-plain-duel-strategy", 0, Zone.Hand);
            var focused = Add(l, Sincerity, 0, Zone.Hand);
            Deck(l, 1);
            var le = MatchEngine.FromTestState(l);
            var beforeLoss = Enumerable.Range(0, 4).Select(i => le.ProvinceStrengthForTest(0, i)).ToArray();
            Accept(le, Batch336Support.Offer(le, 0, "steel-on-steel-duel", duelCard.Id, strong.Id, weak.Id));
            Accept(le, new GameCommand("focus", 1, le.Version, Choice: "deck"));
            Accept(le, new GameCommand("focus", 0, le.Version, CardId: focused.Id));
            Accept(le, new GameCommand("strike", 1, le.Version));
            Check(Enumerable.Range(0, 4).Select(i => le.ProvinceStrengthForTest(0, i)).SequenceEqual(beforeLoss), "a duel its owner lost gives no strength");
        }

        // The Open half: Dragon Clan player only; the card leaves the game instead of the discard pile.
        var dragonId = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Clan == "Dragon").Id;
        MatchEngine Rig(string clan, out CardInstance sincerity, out CardInstance dragon, bool bowed = false)
        {
            var a = Table(Phase.Action, 0);
            a.Players[0].Clan = clan;
            sincerity = Add(a, Sincerity, 0, Zone.Hand);
            dragon = Add(a, dragonId, 0, Zone.Home, bowed: bowed);
            Deck(a, 0);
            return MatchEngine.FromTestState(a);
        }
        var crane = Rig("Crane", out var craneCard, out var craneDragon);
        Check(!CanUse(crane, 0, "sincerity-dragon-name", craneCard.Id, craneDragon.Id), "a Crane Clan player cannot: 'If you are a Dragon Clan player'");
        Check(crane.State.Cards[craneCard.Id].Zone == Zone.Hand, "the card stays in hand");
        var dragon = Rig("Dragon", out var card2, out var performer);
        Accept(dragon, Enabled(dragon, 0, "sincerity-dragon-name", card2.Id, performer.Id).Command!);
        Accept(dragon, dragon.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == "name-3").Command!);
        Eq(dragon.State.SincerityWatches.Single().Number, 3, "the number is named");
        Eq(dragon.State.Cards[card2.Id].Zone, Zone.RemovedFromGame, "'Remove this card from the game': it is not in the discard pile");
        Check(!dragon.State.Players[0].Hand.Contains(card2.Id), "and no longer in the hand");
        // Negated: "If Sincerity's base action is negated it is still removed from the game" (Mendon, issue #492).
        foreach (var negated in new[] { false, true })
        {
            var n = Table(Phase.Action, 0); n.Players[0].Clan = "Dragon";
            var negatedCard = Add(n, Sincerity, 0, Zone.Hand);
            var negatedDragon = Add(n, dragonId, 0, Zone.Home);
            Deck(n, 0);
            var ne = MatchEngine.FromTestState(n);
            ne.NegateEveryAction = negated;
            Accept(ne, Enabled(ne, 0, "sincerity-dragon-name", negatedCard.Id, negatedDragon.Id).Command!);
            if (!negated) Accept(ne, ne.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == "name-1").Command!);
            Eq(ne.State.Cards[negatedCard.Id].Zone, Zone.RemovedFromGame, negated ? "negated: still removed from the game" : "not negated: removed from the game");
            Check(!ne.State.Players[0].Hand.Contains(negatedCard.Id), "and not in the hand");
            Eq(ne.State.SincerityWatches.Count, negated ? 0 : 1, negated ? "a negated action sets no watch" : "the watch is set");
        }
        // A bowed Dragon Personality cannot perform (unchanged): the performer must be unbowed.
        var bowed = Rig("Dragon", out var card3, out var tired, bowed: true);
        Check(!CanUse(bowed, 0, "sincerity-dragon-name", card3.Id, tired.Id), "the performing Dragon Clan Personality must be unbowed");
    }

    private static void HizumiScenario()
    {
        foreach (var (ability, delta) in new[] { ("hizumi-province-raise", 4), ("hizumi-province-lower", -4) })
        {
            var s = Table(Phase.Battle, 0);
            var hizumi = Add(s, Hizumi, 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var before = e.ProvinceStrengthForTest(1, 0);
            Accept(e, Enabled(e, 0, ability, hizumi.Id).Command!);
            Eq(e.ProvinceStrengthForTest(1, 0), before + delta, $"{ability}: the Defender's battlefield province {delta:+#;-#} strength");
            Eq(e.ProvinceStrengthForTest(0, 0), before, $"{ability}: not the Attacker's own province of that index");
            Eq(Batch336Support.StrengthNextTurn(e, 1, 0), before, $"{ability}: until end of turn");
            Check(!CanUse(e, 0, "hizumi-province-raise", hizumi.Id) && !CanUse(e, 0, "hizumi-province-lower", hizumi.Id), "the raise and the lower share one use");
        }
        // Not from home: no 'even if', so a Hizumi who is not at the battle cannot.
        var t = Table(Phase.Battle, 0);
        var home = Add(t, Hizumi, 0, Zone.Home);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "hizumi-province-raise", home.Id), "a Hizumi at home cannot use his Battle action");
        Check(CardCatalog.Get(Hizumi).Abilities!.SequenceEqual(["hizumi-negate-straighten", "hizumi-province-raise", "hizumi-province-lower"]), "both halves of his card are published");
    }

    // A purchase of a Personality from a province: the Kaiu Village's window (or another card's).
    private static (MatchEngine Engine, string CardId) Recruit(Action<MatchState>? setup = null, int marketplaces = 12, string definition = "training-crane-samurai")
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        setup?.Invoke(s);
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definition).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static CardInstance Village(MatchState s, int owner)
    {
        var region = Add(s, KaiuVillage, owner, Zone.Region, province: 3);
        s.Players[owner].Provinces[3].RegionId = region.Id;
        return region;
    }

    private static CardInstance[] Attached(MatchEngine e, string hostId) => e.State.Cards.Values.Where(c => c.Zone == Zone.Attached && c.AttachedTo == hostId).ToArray();

    private static void KaiuVillageScenario()
    {
        string regionId = "";
        var (e, host) = Recruit(s => regionId = Village(s, 0).Id);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", "the entry window is open");
        var force = e.CardForceForTest(host);
        var options = Batch336Support.Options(e, 0, "kaiu-village-weapon").Concat(Batch336Support.Options(e, 0, "kaiu-village-armor")).ToArray();
        Eq(options.Length, 2, "both Reactions are listed");
        Check(options.All(o => o.Enabled && o.Gold is not null && o.Gold.Cost == 2), "each costs 2 Gold");
        var known = e.State.Cards.Keys.ToHashSet();
        Accept(e, Enabled(e, 0, "kaiu-village-weapon", regionId).Command!);
        var created = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Eq(created.DefinitionId, KaiuWeapon, "the Kaiu Weapon Proxy");
        Check(created.Zone == Zone.Attached && created.AttachedTo == host && created.OwnerId == 0, "attached to the Personality who entered, owned by the Village's controller");
        Eq(e.CardForceForTest(host), force + 1, "+1F on the host");
        Check(Attached(e, host).Length == 1, "one Item attached");
        Check(!CanUse(e, 0, "kaiu-village-weapon", regionId), "the Weapon Reaction is spent");
        Eq(e.State.PriorityPlayerId, 0, "the other player holds nothing to react with and is not asked to pass (issue #644): the window is back with its owner");
        // The Armor Reaction is the other printed Reaction: still available, and a Personality may carry an Armor and a Weapon.
        Check(CanUse(e, 0, "kaiu-village-armor", regionId), "the Armor Reaction is still open");
        known = e.State.Cards.Keys.ToHashSet();
        Accept(e, Enabled(e, 0, "kaiu-village-armor", regionId).Command!);
        var armor = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Eq(armor.DefinitionId, KaiuArmor, "the Kaiu Armor Proxy");
        Check(armor.Zone == Zone.Attached && armor.AttachedTo == host, "attached to the same Personality");
        Eq(e.CardForceForTest(host), force + 2, "+1F more");
        Check(!CanUse(e, 0, "kaiu-village-armor", regionId), "the Armor Reaction is spent too");

        // Armor alone: only the Armor is created.
        var (a, aHost) = Recruit(s => regionId = Village(s, 0).Id);
        var aForce = a.CardForceForTest(aHost);
        Accept(a, Enabled(a, 0, "kaiu-village-armor", regionId).Command!);
        Eq(Attached(a, aHost).Single().DefinitionId, KaiuArmor, "only the Armor");
        Eq(a.State.PriorityPlayerId, 0, "the other player holds nothing to react with and is not asked to pass (issue #644)");
        Eq(a.CardForceForTest(aHost), aForce + 1, "+1F");
        Check(CanUse(a, 0, "kaiu-village-weapon", regionId), "the Weapon is still available to a Personality with only an Armor");
    }

    private static void KaiuVillageEdges()
    {
        // No window at all when the entry is not from a province: Matsu Nimuro creates The False Nimuro at home, which is a Personality entering play but not from a province.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var village = Village(s, 0);
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var nimuro = Add(s, Nimuro, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "nimuro-create", nimuro.Id).Command!);
        Check(e.State.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-10807"), "The False Nimuro entered play");
        Check(!CanUse(e, 0, "kaiu-village-weapon", village.Id) && !CanUse(e, 0, "kaiu-village-armor", village.Id), "a Personality created at home did not enter play from a province");

        // The other player's Village does not watch this player's entry, and this player's is not the other's to use.
        var (theirs, _) = Recruit(st => Village(st, 1));
        Check(!CanUse(theirs, 0, "kaiu-village-weapon") && !CanUse(theirs, 1, "kaiu-village-weapon") && !CanUse(theirs, 0, "kaiu-village-armor") && !CanUse(theirs, 1, "kaiu-village-armor"),
            "another player's Kaiu Village is not offered");

        // Not without 2 Gold left over: pay for the Personality with every Gold there is.
        string regionId = "";
        var (poor, poorHost) = Recruit(st =>
        {
            regionId = Village(st, 0).Id;
            // Two Marketplaces (6 Gold for a Crane) pay the 5 Gold Personality; every other Gold source is bowed.
            var keep = st.Cards.Values.Where(c => c.OwnerId == 0 && c.DefinitionId == "training-marketplace" && c.Zone == Zone.Home).Take(2).Select(c => c.Id).ToHashSet();
            foreach (var producer in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 && !keep.Contains(c.Id))) producer.Bowed = true;
        }, marketplaces: 2, definition: "training-crane-samurai");
        var poorOptions = Batch336Support.Options(poor, 0, "kaiu-village-weapon").Concat(Batch336Support.Options(poor, 0, "kaiu-village-armor")).ToArray();
        Check(poorOptions.Length == 0 || poorOptions.All(o => !o.Enabled), "no Gold left to pay 2: the Reactions are not enabled");
        Check(Attached(poor, poorHost).Length == 0, "nothing was created");

        // Nothing offered outside an entry window.
        var idle = Table(Phase.Action, 0); idle.PriorityPlayerId = 0;
        var idleVillage = Village(idle, 0);
        for (var i = 0; i < 6; i++) Add(idle, "training-marketplace", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(idle), 0, "kaiu-village-weapon", idleVillage.Id), "not offered outside an entry window");

        // A Village that has left play (its Region discarded) offers nothing.
        var (gone, _) = Recruit(st => { var v = Village(st, 0); v.Zone = Zone.DynastyDiscard; v.ProvinceIndex = null; st.Players[0].Provinces[3].RegionId = null; });
        Check(!CanUse(gone, 0, "kaiu-village-weapon") && !CanUse(gone, 0, "kaiu-village-armor"), "a Region that is no longer attached does not react");
    }
}
