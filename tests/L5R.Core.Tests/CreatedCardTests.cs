using static RegressionAssertions;
using static Support337;
using System.Text.Json;
using L5R.Core;

// Created cards (issue #387): Proxy scans resolved by data, Lion Ancestor and Water Bo as the examples; every position is played through QueryActions.
internal static class CreatedCardTests
{
    private const string BoOfWater = "oracle-l5r-1084", WaterBo = "oracle-l5r-10026", KitsuAkai = "oracle-l5r-4371", LionAncestor = "oracle-l5r-9785",
        Sparrow = "oracle-l5r-10023", Bear = "oracle-l5r-10025", SankakuYari = "oracle-l5r-6469", Stalwart = "training-stalwart-personality";
    private const string Create = "bo-of-water-create", Bow = "water-bo-bow";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Water Bo is a created-only Item (+2F/+0C Water Weapon, its Battle bow, art shipped, sha256 recorded) and Bo of Water the Spell that creates it (a Second City scan read for its printed text)", PublishedShape),
            ("Every creator resolves to exactly one scan: Kitsu Akai to the Lion Ancestor, Ohsuki and Tokoru to their own tokens, Bo of Water to Water Bo -- found by what the text says, never by name", EveryCreatorHasItsScan),
            ("The resolver: stats and the exact keyword set must fit, a spec no scan fits or that several fit is an error that lists them, an explicit scan must still fit, ability text is compared ignoring case and punctuation", ResolverRules),
            ("Verify names the creator whose spec cannot be resolved (no scan, several scans, a scan of a kind the engine cannot create yet); a scan that is not created-only is never used", VerifyNamesTheCreator),
            ("Lion Ancestor through the resolver: Kitsu Akai's offered Limited action creates it at home with a created-NNNN id, once per turn, public to both players, drawing nothing", LionAncestorThroughTheResolver),
            ("Bo of Water: the Limited action creates Water Bo attached to the chosen Personality (once per game, per Spell, public information, nothing drawn, no random number used) and its Force bonus joins the wielder", BoOfWaterCreatesWaterBo),
            ("Bo of Water is a Limited action of the acting player only, in the Action phase, and only on one of your own Personalities in play", BoOfWaterTiming),
            ("A created Weapon obeys the attach rules: one Weapon per Personality (two with Kensai), so a Personality that already carries one is not offered, and with none left the action is disabled with the rule as its reason", CreatedWeaponAttachRules),
            ("Water Bo is an ordinary Weapon Item in play: its Battle bow needs its wielder at the battle, an enemy card without attachments, once per turn, and Stalwart negates the bow like any other", WaterBoBattleAbility),
            ("A created Item that is destroyed (alone, or with its host in battle resolution) is removed from the game, never left in a discard pile", WaterBoLeavesPlay),
            ("Bo of Water's passive: a bowed Water Shugenja still contributes his Force to his army, a non-Water Shugenja does not, and it ends with the Spell", BoOfWaterPassive),
            ("Created-only scans never enter a deck: not in the deck workshop, not dealt by a match, not in a published deck, not counted as playable from decks, not in the deck data files", ProxiesNeverEnterDecks),
            ("A created card that ends up in a hand, a deck or a discard pile by any path is removed from the game at the end of the command, so it can never be drawn, searched for or recovered", StrayCreatedCardsAreRemoved),
        ];
        runner.Run("created-cards", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static CardInstance Created(MatchEngine e, string definitionId) => e.State.Cards.Values.Single(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal));
    private static CardView ViewOf(MatchEngine e, int viewer, string id) =>
        e.GetView(viewer).Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame)).Single(c => c.Id == id);
    private static void Water(CardInstance c) => c.GrantedKeywords = [new("Water", "test-fixture", "Permanent")];
    private static string Ids(MatchState s) => string.Join(",", s.Players.SelectMany(p => p.Hand.Concat(p.FateDeck).Concat(p.DynastyDeck)));

    private static (MatchState State, CardInstance Shugenja, CardInstance Spell, CardInstance Samurai) ActionTable()
    {
        var s = Table(Phase.Action);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Home); Water(shugenja);
        var spell = Put(s, BoOfWater, 0, Zone.Attached, parent: shugenja.Id);
        var samurai = Put(s, "training-crane-samurai", 0, Zone.Home);
        return (s, shugenja, spell, samurai);
    }

    private static (MatchEngine Engine, CardInstance Host, CardInstance WaterBo, CardInstance Enemy) BattleWithWaterBo()
    {
        var (s, _, spell, samurai) = ActionTable();
        var e = Engine(s);
        Accept(e, Offer(e, 0, Create, spell.Id, samurai.Id));
        var st = e.State;
        var bo = Created(e, WaterBo);
        st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.ActivePlayerId = 0; st.PriorityPlayerId = 0;
        var host = st.Cards[samurai.Id]; host.Zone = Zone.Battlefield; host.ProvinceIndex = 0; st.Cards[bo.Id].ProvinceIndex = 0;
        var enemy = Put(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(st), st.Cards[host.Id], st.Cards[bo.Id], enemy);
    }

    private static void PublishedShape()
    {
        var water = CardCatalog.Get(WaterBo);
        Eq(water.Type, CardType.Item); Check(water.CreatedOnly && !water.ScenarioOnly, "a published, created-only card");
        Eq(water.Force, 2); Eq(water.Chi, 0); Eq(water.GoldCost, 0); Eq(water.Focus, 0); Eq(water.Clan, "Unaligned");
        Eq(string.Join(",", water.Keywords!.OrderBy(k => k, StringComparer.Ordinal)), "Water,Weapon");
        Eq(string.Join(",", water.Abilities!), Bow); Eq(water.Effect, "none");
        Eq(water.Text, "May not be included in decks.\nBattle: Target an enemy card without attachments: Bow it");
        Check(ArtShipped(WaterBo), "art is shipped");
        var bow = AbilityCatalog.All[Bow];
        Eq(bow.Timing, "Battle"); Eq(bow.Target, "enemy-card"); Check(bow.RequireNoAttachments);
        var spell = CardCatalog.Get(BoOfWater);
        Eq(spell.Type, CardType.Spell); Eq(spell.GoldCost, 2); Eq(spell.Focus, 3); Check(!spell.CreatedOnly && !spell.ScenarioOnly);
        Eq(string.Join(",", spell.Keywords!), "Water"); Eq(string.Join(",", spell.Abilities!), Create); Eq(spell.AttachGrantsEliteEffectWhileWielderHas, "Water");
        Check(spell.Text.StartsWith("While this Shugenja is Water, he contributes Force to his army even if bowed.\nLimited: Once per game: Create a +2F/+0C Water"), "the printed text, whose action is Limited (the catalogue's current text says 'Water Limited')");
        var create = AbilityCatalog.All[Create];
        Eq(create.Timing, "Limited"); Check(create.OncePerGame); Eq(create.Target, "own-personality");
        Check(create.Creates is { Type: CardType.Item, Force: 2, Chi: 0 }, "declares the Item it creates");
        Check(ArtShipped(BoOfWater), "Bo of Water's art is shipped");
        var root = RepoRoot();
        var waterSource = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/item-wave-5-sources.json"))).RootElement[0];
        var waterReceipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets/reference/oracle/second-city/receipts/oracle-l5r-10026-printing-1-image-1.json"))).RootElement;
        Eq(waterSource.GetProperty("image_sha256").GetString(), waterReceipt.GetProperty("sha256").GetString(), "Water Bo's recorded scan is the receipt's");
        Eq(waterSource.GetProperty("source_url").GetString(), "https://oracleofthevoid.com/#cardid=10026"); Eq(waterSource.GetProperty("printing_set")[0].GetString(), "Second City");
        var spellSource = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/spell-wave-10-sources.json"))).RootElement[0];
        var spellReceipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets/reference/oracle/second-city/receipts/oracle-l5r-1084-printing-1-image-1.json"))).RootElement;
        Eq(spellSource.GetProperty("image_sha256").GetString(), spellReceipt.GetProperty("working_copy").GetProperty("sha256").GetString(), "Bo of Water's recorded scan is the receipt's working copy");
        Eq(PublishedContent.ItemWave5.Cards.Length, 1); Eq(PublishedContent.SpellWave10.Cards.Length, 1);
    }

    private static void EveryCreatorHasItsScan()
    {
        Eq(string.Join("; ", CreatedCards.Verify()), "", "every ability that creates a card has exactly one scan that fits");
        var pairs = CreatedCards.Creators.Where(c => !c.Spec.CopyOfTarget).ToDictionary(c => c.AbilityId, c => CreatedCards.Resolve(c.Spec).Id);
        Eq(pairs["kitsu-akai-create"], LionAncestor); Eq(pairs["ohsuki-create"], Sparrow); Eq(pairs["tokoru-create"], Bear); Eq(pairs[Create], WaterBo);
        foreach (var (id, spec) in CreatedCards.Creators.Where(c => !c.Spec.CopyOfTarget))
        {
            var scan = CreatedCards.Resolve(spec);
            Check(scan.CreatedOnly && (!scan.ScenarioOnly || id.StartsWith("training-", StringComparison.Ordinal)) && CreatedCards.Creatable(scan.Type), $"{id}: the scan is a published created-only card of a creatable type (a training fixture may use the synthetic Terrain)");
            Check(!AbilityCatalog.All[id].Target.Contains("proxy"), "no ability names a card in its target");
        }
        Eq(CreatedCards.Resolve(new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3, Keywords: ["Ancestor", "Lion Clan", "Samurai", "Spirit"])).Name, "Lion Ancestor");
        Check(!CreatedCards.Scans.Any(c => c.Keywords!.Contains("Ancestor") && !c.Keywords.Contains("Lion Clan")), "the catalogue holds no other-Clan Ancestor scan");
    }

    private static CardDefinition Scan(string id, CardType type, int force, int chi, string[] keywords, string text = "May not be included in decks.", bool createdOnly = true, int honor = 0) =>
        new(id, id, type, Force: force, Chi: chi, PersonalHonor: honor, Keywords: keywords, Text: text, ScenarioOnly: false, CreatedOnly: createdOnly);

    private static CreatedCardResolutionException Throws(Action action)
    {
        try { action(); } catch (CreatedCardResolutionException e) { return e; }
        throw new Exception("Expected a CreatedCardResolutionException");
    }
    private static string Failure(Action resolve) { try { resolve(); } catch (CreatedCardResolutionException e) { return e.Message; } throw new Exception("Expected the spec to fail"); }

    private static void ResolverRules()
    {
        var weapon = Scan("s-weapon", CardType.Item, 2, 0, ["Weapon", "Water"], "May not be included in decks.\nBattle: Target an enemy card without attachments: Bow it");
        var fire = Scan("s-fire", CardType.Item, 3, 1, ["Weapon", "Fire"], "May not be included in decks.\nBattle: Destroy it.");
        var follower = Scan("s-follower", CardType.Follower, 2, 0, ["Weapon", "Water"]);
        CardDefinition[] scans = [weapon, fire, follower];
        Eq(CreatedCards.Resolve(new(CardType.Item, Force: 2, Chi: 0, Keywords: ["Water", "Weapon"]), scans).Id, "s-weapon", "keyword order is irrelevant, the type separates the Follower");
        Eq(CreatedCards.Resolve(new(CardType.Item, Force: 2), scans).Id, "s-weapon", "a stat the spec omits is not compared");
        Eq(CreatedCards.Resolve(new(CardType.Item, Keywords: ["weapon", "FIRE"]), scans).Id, "s-fire", "keywords compare ignoring case");
        Eq(CreatedCards.Resolve(new(CardType.Item, Force: 2, AbilityText: "battle: TARGET an enemy card, without attachments... bow it"), scans).Id, "s-weapon", "ability text ignores case and punctuation");
        var missing = Failure(() => CreatedCards.Resolve(new(CardType.Item, Force: 2, AbilityText: "Battle: Bow a target enemy card without attachments"), scans));
        Check(missing.Contains("no created-only scan fits it"), "a different wording is not a match: " + missing);
        var extra = Failure(() => CreatedCards.Resolve(new(CardType.Item, Keywords: ["Weapon"]), scans));
        Check(extra.Contains("no created-only scan fits it"), extra);
        Eq(CreatedCards.Resolve(new(CardType.Item, Force: 3, Keywords: ["Weapon", "Fire"]), scans).Id, "s-fire");
        var ex = Throws(() => CreatedCards.Resolve(new(CardType.Item, Force: 9), scans));
        Check(ex.Message.Contains("Item 9F") && ex.Candidates.Select(c => c.Id).OrderBy(x => x).SequenceEqual(["s-fire", "s-weapon"]), ex.Message);
        Check(Failure(() => CreatedCards.Resolve(new(CardType.Holding, Force: 2), scans)).Contains("no created-only scan fits it"), "a type with no scan");
        var twin = Scan("s-twin", CardType.Item, 2, 0, ["Weapon", "Water"], "May not be included in decks.");
        var ambiguous = Throws(() => CreatedCards.Resolve(new(CardType.Item, Force: 2, Keywords: ["Weapon", "Water"]), [.. scans, twin]));
        Check(ambiguous.Message.Contains("2 scans fit it") && ambiguous.Message.Contains("s-weapon") && ambiguous.Message.Contains("s-twin") && ambiguous.Candidates.Length == 2, ambiguous.Message);
        Eq(CreatedCards.Resolve(new(CardType.Item, Force: 2, Keywords: ["Weapon", "Water"], ProxyId: "s-twin"), [.. scans, twin]).Id, "s-twin", "an explicit scan settles it");
        Check(Failure(() => CreatedCards.Resolve(new(CardType.Item, Force: 3, ProxyId: "s-twin"), [.. scans, twin])).Contains("s-twin does not fit it"), "the named scan must fit the rest of the spec");
        Check(Failure(() => CreatedCards.Resolve(new(CardType.Item, Force: 2, ProxyId: "nowhere"), scans)).Contains("nowhere is not a created-only card"), "an unknown scan");
        Check(Failure(() => CreatedCards.Resolve(new(CardType.Personality, ProxyId: KitsuAkai))).Contains("is not a created-only card"), "a recruitable card is never a created card");
    }

    private static void VerifyNamesTheCreator()
    {
        var weapon = Scan("s-weapon", CardType.Item, 2, 0, ["Weapon"]);
        var twin = Scan("s-twin", CardType.Item, 2, 0, ["Weapon"]);
        var region = new CardDefinition("s-region", "Region scan", CardType.Region, Keywords: [], Text: "May not be included in decks.", ScenarioOnly: false, CreatedOnly: true);
        var recruitable = Scan("s-recruit", CardType.Follower, 1, 0, ["Ashigaru"], createdOnly: false);
        (string, CreatedCardSpec)[] creators = [
            ("fine-create", new(CardType.Item, Force: 2, Keywords: ["Weapon"], ProxyId: "s-weapon")),
            ("ghost-create", new(CardType.Item, Force: 9)),
            ("twin-create", new(CardType.Item, Force: 2, Keywords: ["Weapon"])),
            ("region-create", new(CardType.Region)),
            ("recruit-create", new(CardType.Follower, Force: 1)),
        ];
        var problems = CreatedCards.Verify(creators, [weapon, twin, region, recruitable]);
        Eq(problems.Length, 4, string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("ghost-create: ") && p.Contains("no created-only scan fits it")), "no scan: " + string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("twin-create: ") && p.Contains("s-weapon") && p.Contains("s-twin")), "two scans: " + string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("region-create: ") && p.Contains("Region") && p.Contains("cannot create")), "a kind the engine cannot create yet: " + string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("recruit-create: ")), "a card that is not created-only is never used: " + string.Join("\n", problems));
        Check(!problems.Any(p => p.StartsWith("fine-create")), "the creator that fits is not reported");
    }

    private static void LionAncestorThroughTheResolver()
    {
        var s = Table(Phase.Action);
        var akai = Put(s, KitsuAkai, 0, Zone.Home);
        var hand = Put(s, "training-follower", 0, Zone.Hand);
        var e = Engine(s);
        var rng = e.State.RngState; var handsAndDecks = Ids(e.State);
        Accept(e, Offer(e, 0, "kitsu-akai-create", akai.Id));
        var ancestor = Created(e, LionAncestor);
        Eq(ancestor.Id, "created-0001"); Eq(ancestor.OwnerId, 0); Eq(ancestor.Zone, Zone.Home); Check(ancestor.FaceUp && !ancestor.Bowed);
        Check(ReferenceEquals(CardCatalog.Get(ancestor.DefinitionId), CreatedCards.Resolve(AbilityCatalog.All["kitsu-akai-create"].Creates!)), "the very definition the resolver names");
        Eq(e.State.RngState, rng, "creating a card uses no random number"); Eq(Ids(e.State), handsAndDecks, "nothing drawn, nothing added to a hand or a deck"); Check(e.State.Players[0].Hand.Contains(hand.Id));
        Eq(e.State.Events.Count(x => x.Type == "cardCreated"), 1); Check(e.State.Events.Single(x => x.Type == "cardCreated").Message == "Crane created Lion Ancestor.");
        var mine = ViewOf(e, 0, ancestor.Id); var theirs = ViewOf(e, 1, ancestor.Id);
        Check(mine.Definition is { Id: LionAncestor, CreatedOnly: true } && theirs.Definition is { Id: LionAncestor }, "public: both players see the card");
        Eq(mine.Force, 2); Eq(mine.Chi, 2);
        Check(!e.QueryActions(0).Any(o => o.Enabled && o.Command?.AbilityId == "kitsu-akai-create"), "Limited: once per turn");
        var later = Edit(e, st => { st.Cards[akai.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        Accept(later, Offer(later, 0, "kitsu-akai-create", akai.Id));
        Eq(string.Join(",", later.State.Cards.Values.Where(c => c.DefinitionId == LionAncestor).Select(c => c.Id).OrderBy(x => x)), "created-0001,created-0002");
    }

    private static void BoOfWaterCreatesWaterBo()
    {
        var (s, shugenja, spell, samurai) = ActionTable();
        var hand = Put(s, "training-follower", 0, Zone.Hand); Put(s, "training-follower", 1, Zone.Hand);
        var e = Engine(s);
        var offer = Offers(e, 0, Create).Single();
        Check(offer.Enabled && offer.CardId == spell.Id, Describe(e, 0, Create));
        Check(offer.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { shugenja.Id, samurai.Id }.OrderBy(x => x)), "any of your Personalities in play is a candidate");
        Check(offer.Label.Contains("Once per game"), "says so");
        Eq(Offers(e, 1, Create).Length, 0, "the opponent is offered nothing of it");
        var forceBefore = ViewOf(e, 0, samurai.Id).Force; var rng = e.State.RngState; var ids = Ids(e.State);
        var command = Offer(e, 0, Create, spell.Id, samurai.Id);
        Accept(e, command);
        var bo = Created(e, WaterBo);
        Eq(bo.Id, "created-0001"); Eq(bo.Zone, Zone.Attached); Eq(bo.AttachedTo, samurai.Id); Eq(bo.OwnerId, 0); Check(bo.FaceUp && !bo.Bowed); Eq(bo.ProvinceIndex, (int?)null);
        Check(e.State.Cards[shugenja.Id].Zone == Zone.Home && !e.State.Cards.Values.Any(c => c.AttachedTo == shugenja.Id && c.DefinitionId == WaterBo), "attached to the chosen Personality, not the Shugenja");
        Eq(ViewOf(e, 0, samurai.Id).Force, forceBefore + 2, "its +2F joins the wielder"); Eq(ViewOf(e, 0, samurai.Id).Chi, CardCatalog.Get("training-crane-samurai").Chi, "+0C");
        Eq(e.State.RngState, rng); Eq(Ids(e.State), ids, "not in any hand or deck"); Check(e.State.Players[0].Hand.Contains(hand.Id));
        var created = e.State.Events.Single(x => x.Type == "cardCreated");
        Check(created.Message == "Crane created Water Bo and attached it to Crane training samurai." && created.CardId == bo.Id, created.Message);
        var theirs = ViewOf(e, 1, bo.Id);
        Check(theirs.Definition is { Id: WaterBo, CreatedOnly: true, ArtworkKey: "res://Content/Published/oracle-l5r-10026.jpg" } && theirs.AttachedTo == samurai.Id && theirs.FaceUp, "the view carries the scan's definition and art key");
        Eq(JsonSerializer.Serialize(e.GetView(1).Events), JsonSerializer.Serialize(e.GetView(0).Events), "the log is public");
        HiddenIdsAbsent(e.GetView(1), [hand.Id]);
        Check(e.GetView(1).Players[0].Hand.Length == 0 && e.GetView(1).Players[0].HandCount == 1, "the opponent still sees only a hand count");
        var mine = Edit(e, st => st.PriorityPlayerId = 0);
        var spent = Offers(mine, 0, Create).Single();
        Check(!spent.Enabled && spent.Reason!.Contains("already been used"), Describe(mine, 0, Create));
        Reject(mine, command with { ExpectedVersion = mine.Version });
        var later = Edit(e, st => { st.TurnNumber += 6; st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        Check(!Offers(later, 0, Create).Single().Enabled, "still spent on a later turn");
        var (s2, _, spellA, samuraiA) = ActionTable();
        var shugenjaB = Put(s2, "training-shugenja", 0, Zone.Home); Water(shugenjaB);
        var spellB = Put(s2, BoOfWater, 0, Zone.Attached, parent: shugenjaB.Id);
        var two = Engine(s2);
        Accept(two, Offer(two, 0, Create, spellA.Id, samuraiA.Id));
        var afterOne = Edit(two, st => st.PriorityPlayerId = 0);
        Eq(afterOne.State.Cards.Values.Count(c => c.DefinitionId == WaterBo), 1);
        Check(!Offers(afterOne, 0, Create).Single(o => o.CardId == spellA.Id).Enabled && Offers(afterOne, 0, Create).Single(o => o.CardId == spellB.Id).Enabled, "each Spell has its own once per game");
    }

    private static void BoOfWaterTiming()
    {
        var (s, _, spell, samurai) = ActionTable();
        var e = Engine(s);
        var command = Offer(e, 0, Create, spell.Id, samurai.Id);
        var battle = Edit(e, st => { st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(Offers(battle, 0, Create).Count(o => o.Enabled), 0, "Limited: Action phase only");
        Reject(battle, command with { ExpectedVersion = battle.Version });
        var otherTurn = Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Eq(Offers(otherTurn, 0, Create).Count(o => o.Enabled), 0, "Limited: only on your own turn");
        Reject(otherTurn, command with { ExpectedVersion = otherTurn.Version });
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Home); var dead = Put(s, "training-crane-veteran", 0, Zone.Dead); var holding = Put(s, "training-marketplace", 0, Zone.Home);
        var all = Engine(s);
        var legal = Offers(all, 0, Create).Single().TargetIds!;
        Check(!legal.Contains(enemy.Id) && !legal.Contains(dead.Id) && !legal.Contains(holding.Id) && legal.Contains(samurai.Id), "candidates: " + string.Join(",", legal));
        foreach (var bad in new[] { enemy.Id, dead.Id, holding.Id, spell.Id })
            Reject(all, Offer(all, 0, Create, spell.Id, samurai.Id) with { TargetId = bad });
        Reject(all, Offer(all, 0, Create, spell.Id, samurai.Id) with { TargetId = null });
        Check(!all.State.Cards.Values.Any(c => c.DefinitionId == WaterBo), "a refused command creates nothing");
    }

    private static void CreatedWeaponAttachRules()
    {
        var (s, shugenja, spell, samurai) = ActionTable();
        Put(s, SankakuYari, 0, Zone.Attached, parent: samurai.Id);
        var e = Engine(s);
        var targets = Offers(e, 0, Create).Single().TargetIds!;
        Check(targets.SequenceEqual([shugenja.Id]), "a Personality that already carries a Weapon is not a candidate: " + string.Join(",", targets));
        var refused = e.Execute(Offer(e, 0, Create, spell.Id, shugenja.Id) with { TargetId = samurai.Id });
        Check(!refused.Accepted && refused.Error!.Contains("only one Weapon"), "refused with the rule: " + refused.Error);
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == WaterBo), 0);
        Put(s, SankakuYari, 0, Zone.Attached, parent: shugenja.Id);
        var none = Engine(s);
        var offer = Offers(none, 0, Create).Single();
        Check(!offer.Enabled && offer.Command is null, "disabled, with no command to submit: " + offer.Reason);
        var bare = none.Execute(new GameCommand("useAbility", 0, none.Version, CardId: spell.Id, TargetId: samurai.Id, AbilityId: Create));
        Check(!bare.Accepted && bare.Error!.Contains("only one Weapon"), bare.Error);
        s.Cards[samurai.Id].GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        var kensai = Engine(s);
        Check(Offers(kensai, 0, Create).Single().TargetIds!.SequenceEqual([samurai.Id]), "only the Kensai");
        Accept(kensai, Offer(kensai, 0, Create, spell.Id, samurai.Id));
        Eq(kensai.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == samurai.Id && CardCatalog.Get(c.DefinitionId).Keywords!.Contains("Weapon")), 2);
        var full = Edit(kensai, st => { st.Cards[spell.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        Check(!Offers(full, 0, Create).Single().Enabled, "the Kensai now carries two Weapons and the Shugenja one");
    }

    private static void WaterBoBattleAbility()
    {
        var (e, host, bo, enemy) = BattleWithWaterBo();
        var setup = e.State;
        var other = Put(setup, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var withFollower = Put(setup, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Put(setup, "training-follower", 1, Zone.Attached, province: 0, parent: withFollower.Id);
        e = Engine(setup);
        var offer = Offers(e, 0, Bow).Single(o => o.CardId == bo.Id);
        Check(offer.Enabled, Describe(e, 0, Bow));
        Check(offer.TargetIds!.Contains(enemy.Id) && offer.TargetIds.Contains(other.Id) && offer.TargetIds.Contains(follower.Id), "enemy cards without attachments, an attached Follower included: " + string.Join(",", offer.TargetIds!));
        Check(!offer.TargetIds.Contains(withFollower.Id) && !offer.TargetIds.Contains(host.Id) && !offer.TargetIds.Contains(bo.Id), "not a card with attachments, not your own");
        Reject(e, Offer(e, 0, Bow, bo.Id, enemy.Id) with { TargetId = withFollower.Id });
        Reject(e, Offer(e, 0, Bow, bo.Id, enemy.Id) with { TargetId = host.Id });
        var command = Offer(e, 0, Bow, bo.Id, enemy.Id);
        Accept(e, command);
        Check(e.State.Cards[enemy.Id].Bowed && !e.State.Cards[other.Id].Bowed && !e.State.Cards[host.Id].Bowed, "bows the target only, at no cost to the wielder or the Item");
        Check(!e.State.Cards[bo.Id].Bowed, "no bow cost of its own");
        Check(e.State.Events.Any(x => x.Type == "abilityUsed" && x.CardId == bo.Id), "the log names Water Bo");
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!Offers(again, 0, Bow).Single(o => o.CardId == bo.Id).Enabled, "used this turn");
        var away = Edit(e, st => { var w = st.Cards[host.Id]; w.Zone = Zone.Home; w.ProvinceIndex = null; st.Cards[bo.Id].ProvinceIndex = null; st.Cards[bo.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; Put(st, "training-crane-veteran", 0, Zone.Battlefield, province: 0); });
        var awayOffer = Offers(away, 0, Bow).Single(o => o.CardId == bo.Id);
        Check(!awayOffer.Enabled && awayOffer.Reason!.Contains("wielder must be at the current battlefield"), awayOffer.Reason);
        var (s, _, spell, samurai) = ActionTable();
        var away1 = Put(s, "training-lion-samurai", 1, Zone.Home);
        var quiet = Engine(s); Accept(quiet, Offer(quiet, 0, Create, spell.Id, samurai.Id));
        Eq(Offers(quiet, 0, Bow).Count(o => o.Enabled), 0);
        Reject(quiet, new GameCommand("useAbility", 0, quiet.Version, CardId: Created(quiet, WaterBo).Id, TargetId: away1.Id, AbilityId: Bow));
        var (e2, _, bo2, _) = BattleWithWaterBo();
        var setup2 = e2.State;
        var stalwart = Put(setup2, Stalwart, 1, Zone.Battlefield, province: 0);
        e2 = Engine(setup2);
        Accept(e2, Offer(e2, 0, Bow, bo2.Id, stalwart.Id));
        Check(!e2.State.Cards[stalwart.Id].Bowed && e2.State.Events.Any(x => x.Type == "stalwartNegated"), "Stalwart negates the bow");
        var spent = Edit(e2, st => st.PriorityPlayerId = 0);
        Check(!Offers(spent, 0, Bow).Single(o => o.CardId == bo2.Id).Enabled, "and the action is spent");
    }

    private static void WaterBoLeavesPlay()
    {
        {
            var (e, host, bo, _) = BattleWithWaterBo();
            var st = e.State;
            var bear = Put(st, Bear, 1, Zone.Battlefield, province: 0);
            st.PriorityPlayerId = 1;
            var fate = st.Players[0].FateDeck.Count; var discardBefore = e.GetView(0).Players[0].Discard.Length;
            var battle = Engine(st);
            Accept(battle, Offer(battle, 1, "tokoru-bear-bow", bear.Id, bo.Id));
            Eq(battle.State.Cards[bo.Id].Zone, Zone.RemovedFromGame, "a destroyed created Item is removed from the game");
            Eq(battle.GetView(0).Players[0].Discard.Length, discardBefore, "it is in nobody's discard pile"); Check(battle.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == bo.Id));
            Eq(battle.State.Players[0].FateDeck.Count, fate); Check(battle.State.Cards[host.Id].Zone == Zone.Battlefield, "its wielder is unharmed");
            Check(battle.State.Events.Any(x => x.Type == "attachmentDestroyed" && x.CardId == bo.Id) && battle.State.Events.Any(x => x.Type == "removedFromGame" && x.CardId == bo.Id), "the log says destroyed, then removed from the game");
        }
        {
            var (e, host, bo, enemy) = BattleWithWaterBo();
            var st = e.State;
            st.Cards[enemy.Id].Modifiers = [new("test-fixture", "test-fixture", "Force", 12)];
            var resolve = Engine(st);
            Accept(resolve, Pass(resolve)); Accept(resolve, Pass(resolve));
            Check(resolve.State.Cards[host.Id].Zone is Zone.Dead, "the host died");
            Eq(resolve.State.Cards[bo.Id].Zone, Zone.RemovedFromGame, "Water Bo left the game with him rather than going to the Fate discard pile");
            Check(!resolve.GetView(0).Players[0].Discard.Any(c => c.Id == bo.Id) && resolve.GetView(0).Players[0].Discard.Any(c => c.Id == host.Id), "the host is in the discard pile, Water Bo is not");
        }
    }

    private static void BoOfWaterPassive()
    {
        var s = Table(Phase.Battle);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0); Water(shugenja);
        var spell = Put(s, BoOfWater, 0, Zone.Attached, parent: shugenja.Id, province: 0);
        var fire = Put(s, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0); fire.GrantedKeywords = [new("Fire", "test-fixture", "Permanent")];
        var fireSpell = Put(s, BoOfWater, 0, Zone.Attached, parent: fire.Id, province: 0);
        var plain = Put(s, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0); Water(plain);
        var e = Engine(s);
        Eq(ViewOf(e, 0, shugenja.Id).UnitForce, CardCatalog.Get("training-shugenja").Force, "a bowed Water Shugenja with Bo of Water contributes his Force");
        Eq(ViewOf(e, 0, fire.Id).UnitForce, 0, "a Shugenja who is not Water does not");
        Eq(ViewOf(e, 0, plain.Id).UnitForce, 0, "and a Water Shugenja without the Spell does not");
        Check(!CardCatalog.Get("training-shugenja").Keywords!.Contains("Elite") && !e.HasKeyword(shugenja.Id, "Elite"), "it is Elite's effect, not the Elite keyword");
        var gone = Edit(e, st => { st.Cards[spell.Id].Zone = Zone.FateDiscard; st.Cards[spell.Id].AttachedTo = null; });
        Eq(ViewOf(gone, 0, shugenja.Id).UnitForce, 0, "the Spell left play");
        var notWater = Edit(e, st => { st.Cards[shugenja.Id].GrantedKeywords.Clear(); });
        Eq(ViewOf(notWater, 0, shugenja.Id).UnitForce, 0, "he is no longer Water");
        Check(fireSpell.Zone == Zone.Attached, "the Spell on the Fire Shugenja is a different card doing nothing for him");
    }

    private static void ProxiesNeverEnterDecks()
    {
        var normal = new DeckCardInfo("oracle-l5r-4371", "Kitsu Akai", ["Personality"], ["Dynasty"], ["Lion Clan"], true);
        var lionAncestor = new DeckCardInfo(LionAncestor, "Lion Ancestor", ["Personality"], ["Other"], ["Ancestor"], true, CreatedOnly: true);
        var raw = new DeckCardInfo("oracle-l5r-10201", "Detached Personality", ["Proxy"], ["Other"], ["Samurai"], false);
        var rawCreated = new DeckCardInfo("oracle-l5r-10200", "Ninja", ["Proxy"], ["Other"], ["Ninja"], false, CreatedOnly: true);
        var workshop = new DeckWorkshop([normal, lionAncestor, raw, rawCreated]);
        workshop.Adjust("Kitsu Akai", 2);
        foreach (var name in new[] { "Lion Ancestor", LionAncestor, "Detached Personality", "Ninja" })
        {
            try { workshop.Adjust(name, 1); throw new Exception($"{name} was added to a deck"); } catch (KeyNotFoundException) { }
            Eq(workshop.Count(name), 0);
            try { workshop.Load($"Legality: Emperor\n1 {name}\n"); throw new Exception($"{name} was loaded into a deck"); } catch (FormatException e) { Check(e.Message.StartsWith("Unknown card"), e.Message); }
        }
        Eq(workshop.Cards.Length, 1); Eq(workshop.Count("Kitsu Akai"), 2, "a rejected load leaves the draft as it was");
        Check(!workshop.Review().Warnings.Any(w => w.Contains("Lion Ancestor") || w.Contains("Detached")), "nothing to warn about: it cannot be there");
        Check(DeckWorkshop.MayBeInDeck(normal) && !DeckWorkshop.MayBeInDeck(lionAncestor) && !DeckWorkshop.MayBeInDeck(raw) && !DeckWorkshop.MayBeInDeck(rawCreated));
        var created = CreatedCards.Scans.Select(c => c.Id).ToHashSet();
        Check(created.IsSupersetOf([LionAncestor, Sparrow, Bear, WaterBo]) && created.Count >= 4);
        Check(!CardCatalog.PlayableFromDecks.Any(c => created.Contains(c.Id)), "never counted as playable from a deck");
        Eq(CardCatalog.PlayableFromDecks.Count() + CreatedCards.Scans.Count(c => !c.ScenarioOnly), CardCatalog.Cards.Values.Count(c => !c.ScenarioOnly), "every other published card is");
        Check(CardCatalog.PlayableFromDecks.Any(c => c.Id == KitsuAkai) && CardCatalog.PlayableFromDecks.Any(c => c.Id == BoOfWater), "the creators are ordinary cards");
        foreach (var deck in PublishedContent.Snapshot.Decks.Concat(PublishedContent.TestSnapshot.Decks))
            Check(!deck.Dynasty.Concat(deck.Fate).Any(created.Contains), "a published deck lists a created-only card");
        var real = PublishedContent.Snapshot.Decks[0];
        foreach (var bad in new[] { LionAncestor, WaterBo })
        {
            var dynasty = PublishedContent.Snapshot with { Decks = [real with { Dynasty = [bad, .. real.Dynasty.Skip(1)] }, .. PublishedContent.Snapshot.Decks.Skip(1)] };
            var fate = PublishedContent.Snapshot with { Decks = [real with { Fate = [bad, .. real.Fate.Skip(1)] }, .. PublishedContent.Snapshot.Decks.Skip(1)] };
            foreach (var snapshot in new[] { dynasty, fate })
            {
                try { MatchEngine.CreatePublished(1, PublishedContent.Scenario, snapshot); throw new Exception($"a deck with {bad} was dealt"); }
                catch (ArgumentException e) { Check(e.Message.Contains("may not be included in decks"), e.Message); }
            }
        }
        var started = MatchEngine.CreateStarterMatch();
        Check(!started.State.Cards.Values.Any(c => CardCatalog.Get(c.DefinitionId).CreatedOnly), "a new match holds no created card");
        var root = RepoRoot();
        var files = new[] { "content/deck-pool.json", "content/deck-library.json" }.Concat(Directory.GetFiles(Path.Combine(root, "content/decks"), "*.json").Select(f => Path.GetRelativePath(root, f)));
        foreach (var file in files)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            foreach (var id in created) Check(!text.Contains($"\"{id}\""), $"{file} lists the created-only card {id}");
        }
        var pool = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/deck-pool.json"))).RootElement.GetProperty("cards");
        foreach (var card in pool.EnumerateArray()) Check(!card.GetProperty("types").EnumerateArray().Any(t => t.GetString() == "Proxy"), "the deck pool holds a Proxy scan: " + card.GetProperty("id"));
    }

    private static void StrayCreatedCardsAreRemoved()
    {
        var s = Table(Phase.Action);
        var control = Put(s, "training-follower", 0, Zone.Hand); var controlDiscard = Put(s, "training-item", 0, Zone.FateDiscard);
        CardInstance Stray(string definition, Zone zone, int owner = 0)
        {
            var card = Put(s, definition, owner, zone == Zone.Hand ? Zone.Home : zone);
            card.Zone = zone; card.FaceUp = true;
            var player = s.Players[owner];
            if (zone == Zone.Hand) player.Hand.Add(card.Id);
            if (zone == Zone.FateDeck) player.FateDeck.Insert(0, card.Id);
            if (zone == Zone.DynastyDeck) player.DynastyDeck.Insert(0, card.Id);
            return card;
        }
        var strays = new[] { Stray(WaterBo, Zone.Hand), Stray(WaterBo, Zone.FateDeck), Stray(LionAncestor, Zone.DynastyDeck, 1), Stray(WaterBo, Zone.FateDiscard), Stray(LionAncestor, Zone.DynastyDiscard), Stray(LionAncestor, Zone.Dead, 1) };
        var fateDeck = s.Players[0].FateDeck.Count - 1; var dynastyDeck = s.Players[1].DynastyDeck.Count - 1; var hand = s.Players[0].Hand.Count - 1;
        var e = Engine(s);
        Accept(e, Pass(e));
        foreach (var stray in strays) Eq(e.State.Cards[stray.Id].Zone, Zone.RemovedFromGame, $"{stray.DefinitionId} left in {stray.Zone}");
        Check(!e.State.Players.Any(p => p.Hand.Concat(p.FateDeck).Concat(p.DynastyDeck).Any(id => strays.Any(x => x.Id == id))), "in no hand or deck");
        Eq(e.State.Players[0].FateDeck.Count, fateDeck); Eq(e.State.Players[1].DynastyDeck.Count, dynastyDeck); Eq(e.State.Players[0].Hand.Count, hand);
        Eq(e.State.Events.Count(x => x.Type == "removedFromGame"), strays.Length);
        Check(e.State.Cards[control.Id].Zone == Zone.Hand && e.State.Cards[controlDiscard.Id].Zone == Zone.FateDiscard, "ordinary cards in the same places are left alone");
        Check(!e.GetView(0).Players[0].Discard.Any(c => strays.Any(x => x.Id == c.Id)) && e.GetView(0).Players[0].RemovedFromGame.Count(c => strays.Any(x => x.Id == c.Id)) == 4, "removed from the game, not in a pile");
    }
}
