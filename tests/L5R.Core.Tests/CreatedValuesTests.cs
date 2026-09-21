using static Support337;
using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Created-card follow-up (issue #402, Mendon's answers 6, 7 and 12): the per-instance layer under a created card (CardInstance.Printed, read through the engine's one Definition),
// the values a created card takes from another card, Personality counts, and the three pairings that need them: Venerable Master (your Clan), Akuma's Power (the performer's title and
// Gold Cost) and Nakanu Technique (two Followers on the chosen Personality).
internal static class CreatedValuesTests
{
    private const string VenerableMaster = "oracle-l5r-9131", Sensei = "oracle-l5r-10017", AkumasPower = "oracle-l5r-10027", YourNameIsMine = "oracle-l5r-10202",
        NakanuTechnique = "oracle-l5r-5506", NakanusCreations = "oracle-l5r-10011", LionAncestor = "oracle-l5r-9785", ValueMaker = "training-value-maker", ValueItem = "training-value-item";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Venerable Master (Holding, 2 Gold, produces 2), Akuma's Power (Celestial) and Nakanu Technique (Strategy) are ordinary cards; Venerated Sensei, Your Name is Mine and Nakanu's Creations are created-only scans; art, sha256 and manifests", PublishedShape),
            ("Every creator still resolves to exactly one scan, and a spec that sets a value a card cannot have, names a performer the ability lacks or creates several Unique cards is a Verify failure naming the creator", SpecsResolveAndAreChecked),
            ("The per-instance layer: an ordinary card and a created card with no instance values carry no Printed definition, one that has values does, and the rules and both players' views read it", PrintedLayer),
            ("Venerable Master: Open, bow and destroy it and pay 2 Gold to create a 3F/3C/3PH Samurai Venerated Sensei with the creator's Stronghold Clan and Clan keyword, fixed when it is created", VenerableMasterCreatesSensei),
            ("Venerable Master's costs: not offered bowed, not with the Gold missing, not to the other player; it is destroyed as a cost, and the Sensei leaves the game when it dies", VenerableMasterCosts),
            ("Akuma's Power: destroy your performing unbowed Shugenja, destroy your leftmost province and discard the Celestial to create Your Name is Mine with the performer's title and printed Gold Cost, at home, and lose 3 Honor", AkumasPowerCreatesYourNameIsMine),
            ("Akuma's Power's performer and province: an unbowed Shugenja of yours only, the leftmost province that is still standing, and the printed (not the current) Gold Cost", AkumasPowerRules),
            ("Nakanu Technique: after engaging, your performing unbowed opposed Personality at the battlefield gets two Nakanu's Creations Followers and you lose 1 Honor; the Followers are his, join his unit and are removed from the game if he dies", NakanuTechniqueEngage),
            ("Nakanu Technique's performer must be unbowed, at the battlefield and opposed, the Reaction is spent once, and it is offered to the Defender too", NakanuTechniqueRules),
            ("Values are printed values read from other cards: a Force from the target's Personal Honor (plus a bonus with a keyword), a Chi and Personal Honor taken and reduced, floored at 0; the current values and the modifiers on the subject are not read", ComputedValues),
            ("Values from the performer, the source and the wielder, and a Personality count: Count Personalities are created together, each with its own id, and one entry window opens", ValueSubjectsAndCounts),
        ];
        runner.Run("created-values", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static CardInstance[] CreatedOf(MatchEngine e, string definitionId) => e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static CardInstance Created(MatchEngine e, string definitionId) => CreatedOf(e, definitionId).Single();
    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static string StrongholdClan(MatchState s, int player) => CardCatalog.Get(s.Cards[s.Players[player].StrongholdId].DefinitionId).Clan;

    private static MatchState Selection()
    {
        var s = Table(Phase.BattleSelection);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }

    private static void Engage(MatchEngine e, int owner)
    {
        Accept(e, e.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        while (e.State.PriorityPlayerId != owner) Accept(e, Cmd(e, "declineReaction"));
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, bool CreatedOnly, CardType Type)[] cards = [
            (VenerableMaster, "holding-wave-3", false, CardType.Holding), (AkumasPower, "celestial-wave-2", false, CardType.Celestial), (NakanuTechnique, "strategy-wave-30", false, CardType.Strategy),
            (Sensei, "personality-wave-40", true, CardType.Personality), (YourNameIsMine, "personality-wave-40", true, CardType.Personality), (NakanusCreations, "follower-wave-19", true, CardType.Follower)];
        foreach (var (id, wave, createdOnly, type) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.CreatedOnly, createdOnly, id + " created-only"); Check(!def.ScenarioOnly, id + " is published");
            Check(ArtShipped(id), id + " art is shipped");
            var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().Single(x => x.GetProperty("card_id").GetString() == id);
            var receipt = Directory.GetFiles(Path.Combine(root, "assets/reference/oracle"), $"{id}-printing-*-image-1.json", SearchOption.AllDirectories).Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement).First();
            var sha = source.GetProperty("image_sha256").GetString();
            Check(sha == receipt.GetProperty("sha256").GetString() || receipt.TryGetProperty("working_copy", out var copy) && sha == copy.GetProperty("sha256").GetString(), id + ": the recorded scan is the receipt's");
            Eq(source.GetProperty("source_url").GetString(), $"https://oracleofthevoid.com/#cardid={id.Replace("oracle-l5r-", "")}");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(PublishedContent.HoldingWave3.Cards.Length, 1); Eq(PublishedContent.CelestialWave2.Cards.Length, 1); Eq(PublishedContent.StrategyWave30.Cards.Length, 1);
        Eq(PublishedContent.PersonalityWave40.Cards.Length, 2); Eq(PublishedContent.FollowerWave19.Cards.Length, 1);
        var master = CardCatalog.Get(VenerableMaster);
        Eq(master.GoldCost, 2); Eq(master.GoldProduction, 2); Eq(string.Join(",", master.Keywords!), "Retainer"); Eq(string.Join(",", master.Abilities!), "venerable-master-create");
        Check(master.Text.Contains("Open: Bow and destroy this card, and pay 2 Gold: Create a 3F/3C/3PH Samurai Personality with your Clan alignment."), "the scan's text: bow and destroy are costs");
        var power = CardCatalog.Get(AkumasPower);
        Eq(string.Join(",", power.Abilities!), "akumas-power-create"); Check(power.Keywords!.Contains("Unique") && power.Keywords.Contains("Dark Fortune") && power.Keywords.Contains("Shadowlands"));
        Check(power.Text.StartsWith("Limited: Destroy your performing unbowed Shugenja, destroy your leftmost province, and discard this card: Create a 9F/5C/0PH") && power.Text.EndsWith("Lose 3 Honor."), "the scan's text: three costs, no target");
        var technique = CardCatalog.Get(NakanuTechnique);
        Eq(technique.Focus, 3); Eq(technique.GoldCost, 0); Eq(string.Join(",", technique.Abilities!), "nakanu-technique-create");
        var sensei = CardCatalog.Get(Sensei);
        Eq(sensei.Force, 3); Eq(sensei.Chi, 3); Eq(sensei.PersonalHonor, 3); Eq(string.Join(",", sensei.Keywords!), "Samurai"); Eq(sensei.Clan, "Unaligned");
        var mine = CardCatalog.Get(YourNameIsMine);
        Eq(mine.Force, 9); Eq(mine.Chi, 5); Eq(mine.PersonalHonor, 0); Check(new HashSet<string>(mine.Keywords!).SetEquals(["Shadowlands", "Nonhuman", "Oni"]));
        var creations = CardCatalog.Get(NakanusCreations);
        Eq(creations.Force, 1); Eq(creations.Chi, 0); Check(new HashSet<string>(creations.Keywords!).SetEquals(["Shadowlands", "Undead"]));
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id is not (Sensei or YourNameIsMine or NakanusCreations)), "no created scan is playable from a deck");
    }

    private static void SpecsResolveAndAreChecked()
    {
        Eq(string.Join("; ", CreatedCards.Verify()), "");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["venerable-master-create"].Creates!).Id, Sensei);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["akumas-power-create"].Creates!).Id, YourNameIsMine);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["nakanu-technique-create"].Creates!).Id, NakanusCreations);
        var scans = CreatedCards.Scans.ToArray();
        string[] Problems(string ability, CreatedCardSpec spec) => CreatedCards.Verify([(ability, spec)], scans);
        var bad = new (string Ability, CreatedCardSpec Spec, string Expect)[] {
            ("kitsu-akai-create", new(CardType.Follower, ProxyId: "oracle-l5r-10011") { Values = [new(CreatedStat.PersonalHonor, CreatedSubject.Target)] }, "PersonalHonor"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor) { Values = [new(CreatedStat.Name, CreatedSubject.Target, Plus: 1)] }, "Name"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor) { Values = [new(CreatedStat.Force, CreatedSubject.Target, PlusWithKeyword: 2)] }, "Force"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor) { Values = [new(CreatedStat.Force, CreatedSubject.Target, Keyword: "Paragon")] }, "Force"),
            ("water-bo-bow", new(CardType.Item, ProxyId: "oracle-l5r-10026") { ControllerClan = true }, "Clan"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor) { HostIsPerformer = true }, "not an attachment"),
            ("water-bo-bow", new(CardType.Item, ProxyId: "oracle-l5r-10026") { HostIsPerformer = true }, "the ability has none"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor) { Values = [new(CreatedStat.Name, CreatedSubject.Performer)] }, "the ability has none"),
            ("nimuro-create", new(CardType.Personality, ProxyId: "oracle-l5r-10807", Count: 2), "Unique"),
            ("kitsu-akai-create", new(CardType.Personality, ProxyId: LionAncestor, Count: 0), "count"),
        };
        foreach (var (ability, spec, expect) in bad)
        {
            var problems = Problems(ability, spec);
            Check(problems.Length > 0 && problems.All(p => p.StartsWith(ability + ":")) && problems.Any(p => p.Contains(expect)), $"{spec.Describe()}: expected a problem naming {expect}; got [{string.Join(" | ", problems)}]");
        }
        Eq(string.Join("; ", Problems("training-create-computed", AbilityCatalog.All["training-create-computed"].Creates!)), "");
        Eq(string.Join("; ", Problems("akumas-power-create", AbilityCatalog.All["akumas-power-create"].Creates!)), "");
        Check(AbilityCatalog.All["nakanu-technique-create"].Creates!.Describe().Contains("attached to the performer") && AbilityCatalog.All["venerable-master-create"].Creates!.Describe().Contains("controller's Clan")
            && AbilityCatalog.All["akumas-power-create"].Creates!.Describe().Contains("Name from the performer"), "the description names the values");
    }

    private static void PrintedLayer()
    {
        var s = Table(Phase.Action);
        var master = Put(s, VenerableMaster, 0, Zone.Home);
        var ordinary = Put(s, "training-lion-samurai", 0, Zone.Home);
        Check(s.Cards.Values.All(c => c.Printed is null), "nothing carries a Printed definition until a card is created with values");
        var e = Engine(s);
        Accept(e, Offer(e, 0, "venerable-master-create", master.Id));
        var sensei = Created(e, Sensei);
        Check(sensei.Printed is not null && sensei.DefinitionId == Sensei && e.State.Cards[ordinary.Id].Printed is null, "only the created card with values has one; its DefinitionId is still its scan");
        Check(CardCatalog.Get(Sensei).Clan == "Unaligned" && CardCatalog.Get(Sensei).Keywords!.SequenceEqual(["Samurai"]), "the catalogue's scan is untouched");
        var copy = Edit(e, st => { });
        Check(copy.State.Cards[sensei.Id].Printed is { Clan: "Crane" } printed && printed.Keywords!.Contains("Crane Clan") && printed.Id == Sensei, "the definition survives the state copy every command makes");
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = e.GetView(viewer).Players[0].Home.Single(c => c.Id == sensei.Id);
            Check(view.Definition is { Id: Sensei, Clan: "Crane", CreatedOnly: true } && view.Definition.Keywords!.Contains("Crane Clan") && view.Force == 3 && view.Chi == 3, $"player {viewer} sees the created card as it is");
            Eq(view.Definition!.ArtworkKey, CardCatalog.Get(Sensei).ArtworkKey, "the art is its scan's");
        }
        Check(e.HasKeyword(sensei.Id, "Crane Clan") && e.HasKeyword(sensei.Id, "Samurai") && !e.HasKeyword(sensei.Id, "Lion Clan"), "the rules read the keywords through the per-instance definition");
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Venerated Sensei." && x.CardId == sensei.Id), "the log");
    }

    private static void VenerableMasterCreatesSensei()
    {
        Eq(StrongholdClan(Table(Phase.Action), 0), "Crane"); Eq(StrongholdClan(Table(Phase.Action), 1), "Lion");
        var s = Table(Phase.Action);
        var master = Put(s, VenerableMaster, 0, Zone.Home);
        var e = Engine(s);
        var offer = e.QueryActions(0).Single(a => a.Command?.AbilityId == "venerable-master-create");
        Check(offer.Enabled && offer.Gold?.Cost == 2 && offer.Command!.SourceIds is { Length: > 0 }, "offered with its 2 Gold: " + Describe(e, 0, "venerable-master-create"));
        Check(!offer.Command!.SourceIds!.Contains(master.Id) && offer.Gold!.Payments.All(p => !p.SourceIds.Contains(master.Id)), "the Master is bowed as a cost, so it is never a source of the Gold for the same action");
        var rng = e.State.RngState; var honor = e.State.Players[0].Honor;
        Accept(e, offer.Command!);
        var sensei = Created(e, Sensei);
        Eq(sensei.Id, "created-0001"); Eq(sensei.Zone, Zone.Home); Eq(sensei.OwnerId, 0); Check(sensei.FaceUp && !sensei.Bowed); Eq(e.State.RngState, rng); Eq(e.State.Players[0].Honor, honor, "no Honor is lost");
        Eq(e.State.Cards[master.Id].Zone, Zone.DynastyDiscard, "the Holding was destroyed as a cost");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 2 Gold")), "2 Gold was paid");
        var view = e.GetView(1).Players[0].Home.Single(c => c.Id == sensei.Id);
        Check(view.Force == 3 && view.Chi == 3 && view.Definition!.PersonalHonor == 3 && view.Definition.Clan == "Crane" && view.Definition.Keywords!.OrderBy(k => k).SequenceEqual(["Crane Clan", "Samurai"]), "3F/3C/3PH Samurai with the creator's Clan and Clan keyword");
        // Fixed when it is created: the other player's Master makes a Lion Sensei; the first stays Crane even if its creator's Stronghold changed afterwards.
        var t = Table(Phase.Action, actor: 1);
        var theirs = Put(t, VenerableMaster, 1, Zone.Home);
        var f = Engine(t);
        Accept(f, Offer(f, 1, "venerable-master-create", theirs.Id));
        var lion = Created(f, Sensei);
        Eq(lion.OwnerId, 1); Check(lion.Printed is { Clan: "Lion" } && f.HasKeyword(lion.Id, "Lion Clan") && !f.HasKeyword(lion.Id, "Crane Clan"), "the Defender's Master makes a Lion Sensei");
        var u = Table(Phase.Action);
        Put(u, VenerableMaster, 0, Zone.Home); Put(u, VenerableMaster, 0, Zone.Home);
        var g = Engine(u);
        Accept(g, Offer(g, 0, "venerable-master-create", u.Cards.Values.First(c => c.DefinitionId == VenerableMaster).Id));
        var first = Created(g, Sensei);
        var switched = Edit(g, st => { st.Cards[st.Players[0].StrongholdId].DefinitionId = st.Cards[st.Players[1].StrongholdId].DefinitionId; st.PriorityPlayerId = 0; });
        Eq(StrongholdClan(switched.State, 0), "Lion");
        Accept(switched, Offer(switched, 0, "venerable-master-create", u.Cards.Values.Last(c => c.DefinitionId == VenerableMaster).Id));
        var both = CreatedOf(switched, Sensei);
        Eq(both.Length, 2);
        Check(both[0].Printed is { Clan: "Crane" } && both[1].Printed is { Clan: "Lion" }, "the first Sensei stays Crane; the second, made under a Lion Stronghold, is Lion");
        // A Personality with the Clan keyword the Sensei has can be used as one: it meets a Crane-Clan-only Follower's restriction and a Lion Sensei does not.
        var follower = Put(Edit(switched, st => { }).State, "training-follower-crane-guard", 0, Zone.Hand);
        Check(CardCatalog.Get(follower.DefinitionId).AttachRequiresKeyword == "Crane Clan" && switched.HasKeyword(both[0].Id, "Crane Clan") && !switched.HasKeyword(both[1].Id, "Crane Clan"), "the keyword is the rule-visible alignment");
    }

    private static void VenerableMasterCosts()
    {
        // Bowing is one of its costs: a bow that is negated negates the whole action, and nothing is paid.
        var n = Table(Phase.Action);
        var bowedMaster = Put(n, VenerableMaster, 0, Zone.Home);
        var en = Engine(n);
        var command = Offer(en, 0, "venerable-master-create", bowedMaster.Id);
        en.CostBlockHook = (cost, _) => cost.Verb == CostVerb.Bow;
        Accept(en, command);
        Check(CreatedOf(en, Sensei).Length == 0 && en.State.Cards[bowedMaster.Id].Zone == Zone.Home && !en.State.Cards[bowedMaster.Id].Bowed && !en.State.Events.Any(x => x.Type == "payment"), "a negated bow negates the action: nothing is created, destroyed or paid");
        // Bowed: nothing to bow, nothing offered.
        var s = Table(Phase.Action);
        var bowed = Put(s, VenerableMaster, 0, Zone.Home, bowed: true);
        var e = Engine(s);
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "venerable-master-create" && a.Enabled), "a bowed Master cannot pay its own bow: " + Describe(e, 0, "venerable-master-create"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: bowed.Id, AbilityId: "venerable-master-create"));
        // No Gold: every producer bowed.
        var poor = Table(Phase.Action);
        var poorMaster = Put(poor, VenerableMaster, 0, Zone.Home);
        foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 && CardCatalog.Get(c.DefinitionId).Id != VenerableMaster)) c.Bowed = true;
        var ep = Engine(poor);
        Check(!Offers(ep, 0, "venerable-master-create").Any(a => a.Enabled), "no Gold, no action: " + Describe(ep, 0, "venerable-master-create"));
        Reject(ep, new GameCommand("useAbility", 0, ep.Version, CardId: poorMaster.Id, AbilityId: "venerable-master-create", SourceIds: [poorMaster.Id]));
        // Not the other player's action, and not in the Battle phase (Open is an Action-phase action).
        var t = Table(Phase.Action);
        Put(t, VenerableMaster, 0, Zone.Home);
        var et = Engine(t);
        Check(!Offers(et, 1, "venerable-master-create").Any(a => a.Enabled), "the other player has no such action");
        var battle = Table(Phase.Battle);
        Put(battle, VenerableMaster, 0, Zone.Home);
        Check(!Offers(Engine(battle), 0, "venerable-master-create").Any(a => a.Enabled), "not a Battle action");
        // Destroyed as a cost; the Sensei is a created card: destroyed it is removed from the game, not discarded.
        var u = Table(Phase.Action);
        var master = Put(u, VenerableMaster, 0, Zone.Home);
        var g = Engine(u);
        Accept(g, Offer(g, 0, "venerable-master-create", master.Id));
        Eq(g.State.Cards[master.Id].Zone, Zone.DynastyDiscard, "the Holding is in the discard pile, destroyed rather than bowed away");
        var sensei = Created(g, Sensei);
        var dying = Edit(g, st => { st.Cards[sensei.Id].Modifiers = [new("test-fixture", "test-fixture", "Force", -3)]; });
        var doomed = Edit(dying, st => { st.Cards[sensei.Id].Zone = Zone.Dead; });
        Eq(doomed.State.Cards[sensei.Id].Zone, Zone.Dead);
        var removed = Edit(doomed, st => { });
        Accept(removed, Cmd(removed, "pass"));
        Eq(removed.State.Cards[sensei.Id].Zone, Zone.RemovedFromGame, "a created card found in a discard pile or the dead pile is removed from the game");
    }

    private static MatchState Akuma(out CardInstance power, out CardInstance shugenja, bool shugenjaBowed = false)
    {
        var s = Table(Phase.Action);
        power = Put(s, AkumasPower, 0, Zone.Celestial);
        shugenja = Put(s, "training-shugenja", 0, Zone.Home, bowed: shugenjaBowed);
        return s;
    }

    private static void AkumasPowerCreatesYourNameIsMine()
    {
        var s = Akuma(out var power, out var shugenja);
        var e = Engine(s);
        var honor = e.State.Players[0].Honor;
        var command = Offer(e, 0, "akumas-power-create", power.Id, performerId: shugenja.Id);
        Accept(e, command);
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Dead, "the performing Shugenja was destroyed");
        Check(e.State.Players[0].Provinces[0].Destroyed && e.State.Players[0].Provinces.Skip(1).All(p => !p.Destroyed) && !e.State.Players[1].Provinces.Any(p => p.Destroyed), "your leftmost province, and only it, was destroyed");
        Eq(e.State.Cards[power.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded");
        Eq(e.State.Players[0].Honor, honor - 3, "Lose 3 Honor");
        var mine = Created(e, YourNameIsMine);
        Eq(mine.Zone, Zone.Home); Eq(mine.OwnerId, 0); Check(mine.FaceUp && !mine.Bowed); Eq(mine.Id, "created-0001");
        Check(mine.Printed is { Name: "Training shugenja", GoldCost: 3, Force: 9, Chi: 5, PersonalHonor: 0 } printed && printed.Id == YourNameIsMine && printed.CreatedOnly, "the performer's title and printed Gold Cost, the scan's stats");
        Eq(CardCatalog.Get(YourNameIsMine).Name, "Your Name is Mine"); Eq(CardCatalog.Get(YourNameIsMine).GoldCost, 0);
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = e.GetView(viewer).Players[0].Home.Single(c => c.Id == mine.Id);
            Check(view.Definition is { Name: "Training shugenja", GoldCost: 3, CreatedOnly: true } && view.Force == 9 && view.Chi == 5, $"player {viewer} sees the title and cost on the card");
        }
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Training shugenja." && x.CardId == mine.Id), "the log names the title");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "akumas-power-create" && a.Enabled), "the Celestial is gone: nothing is offered again");
        var st = e.State; st.Cards[mine.Id].Zone = Zone.Dead; var dead = Engine(st);
        Accept(dead, Cmd(dead, "pass"));
        Eq(dead.State.Cards[mine.Id].Zone, Zone.RemovedFromGame, "a created Personality never reaches a discard pile");
    }

    private static void AkumasPowerRules()
    {
        // Only your own unbowed Shugenja may perform: a bowed one, a non-Shugenja and the enemy's are not offered.
        var s = Akuma(out var power, out var shugenja, shugenjaBowed: true);
        var samurai = Put(s, "training-lion-samurai", 0, Zone.Home); var enemy = Put(s, "training-shugenja", 1, Zone.Home);
        var e = Engine(s);
        Check(!Offers(e, 0, "akumas-power-create").Any(a => a.Enabled), "no unbowed Shugenja of yours: " + Describe(e, 0, "akumas-power-create"));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: power.Id, AbilityId: "akumas-power-create", PerformerId: shugenja.Id));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: power.Id, AbilityId: "akumas-power-create", PerformerId: samurai.Id));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: power.Id, AbilityId: "akumas-power-create", PerformerId: enemy.Id));
        // The performer's printed Gold Cost, not his current one; the leftmost province still standing.
        var t = Akuma(out var power2, out var shugenja2);
        shugenja2.Modifiers = [new("test-fixture", "test-fixture", "GoldCost", 4)]; t.Players[0].Provinces[0].Destroyed = true;
        var f = Engine(t);
        Accept(f, Offer(f, 0, "akumas-power-create", power2.Id, performerId: shugenja2.Id));
        var mine = Created(f, YourNameIsMine);
        Eq(mine.Printed!.GoldCost, 3, "printed Gold Cost 3, not the current 7");
        Check(f.State.Players[0].Provinces[0].Destroyed && f.State.Players[0].Provinces[1].Destroyed && !f.State.Players[0].Provinces[2].Destroyed && !f.State.Players[0].Provinces[3].Destroyed, "province 1 was already destroyed, so the leftmost that stood (province 2) was");
        // A performer that is himself a created Personality with values passes his own title on.
        var u = Akuma(out var power3, out _);
        var sensei = Put(u, Sensei, 0, Zone.Home);
        sensei.Printed = CardCatalog.Get(Sensei) with { Name = "Borrowed Name", GoldCost = 6, Keywords = ["Samurai", "Shugenja"] };
        var g = Engine(u);
        Accept(g, Offer(g, 0, "akumas-power-create", power3.Id, performerId: sensei.Id));
        Check(CreatedOf(g, YourNameIsMine).Single().Printed is { Name: "Borrowed Name", GoldCost: 6 }, "the performer's own printed values, whatever they were made from");
        Eq(g.State.Cards[sensei.Id].Zone, Zone.RemovedFromGame, "a created performer is removed from the game when destroyed");
        // The last province: destroying it wins the game for the opponent, as any destruction of it would.
        var v = Akuma(out var power4, out var shugenja4);
        for (var i = 0; i < 3; i++) v.Players[0].Provinces[i].Destroyed = true;
        var h = Engine(v);
        Accept(h, Offer(h, 0, "akumas-power-create", power4.Id, performerId: shugenja4.Id));
        Check(h.State.Players[0].Provinces.All(p => p.Destroyed) && h.State.WinnerId == 1, "the fourth province fell: military victory for the other player");
    }

    private static (MatchEngine Engine, CardInstance Technique, CardInstance[] Mine, CardInstance Enemy) Nakanu(int owner = 0, bool enemyThere = true)
    {
        var s = Selection();
        var mine = new[] { Put(s, "training-lion-samurai", owner, Zone.Battlefield, province: 0), Put(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0) };
        var technique = Put(s, NakanuTechnique, owner, Zone.Hand);
        var enemy = Put(s, "training-crane-veteran", 1 - owner, Zone.Battlefield, province: 0);
        if (!enemyThere) s.Cards.Remove(enemy.Id);
        return (Engine(s), technique, mine, enemy);
    }

    private static void NakanuTechniqueEngage()
    {
        var (e, technique, mine, enemy) = Nakanu();
        Engage(e, 0);
        var offers = Offers(e, 0, "nakanu-technique-create").Where(a => a.Enabled).ToArray();
        Check(offers.Select(o => o.Command!.PerformerId).OrderBy(x => x).SequenceEqual(mine.Select(m => m.Id).OrderBy(x => x)), "one offer per unbowed Personality of the army: " + Describe(e, 0, "nakanu-technique-create"));
        var honor = e.State.Players[0].Honor; var before = e.GetView(0).Players[0].Army.Single(c => c.Id == mine[0].Id).UnitForce;
        var command = Offer(e, 0, "nakanu-technique-create", technique.Id, performerId: mine[0].Id);
        Accept(e, command);
        var followers = CreatedOf(e, NakanusCreations);
        Eq(followers.Length, 2, "two Followers");
        foreach (var f in followers) { Eq(f.Zone, Zone.Attached); Eq(f.AttachedTo, mine[0].Id); Eq(f.OwnerId, 0); Eq(f.ProvinceIndex, (int?)0); Check(f.FaceUp && !f.Bowed && f.Printed is null, "attached to the performer, no per-instance values"); }
        Check(followers[0].Id != followers[1].Id, "each has its own id");
        Check(!e.State.Cards.Values.Any(c => c.DefinitionId == NakanusCreations && c.AttachedTo == mine[1].Id), "the other Personality got none");
        Eq(e.State.Players[0].Honor, honor - 1, "Lose 1 Honor");
        Eq(e.State.Cards[technique.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == mine[0].Id).UnitForce, before + 2, "each 1F Follower joins his unit");
        Check(e.State.Events.Count(x => x.Type == "cardCreated" && x.Message == "Crane created Nakanu's Creations and attached it to Lion training samurai.") == 2, string.Join("|", e.State.Events.Where(x => x.Type == "cardCreated").Select(x => x.Message)));
        // They are attachments like any other: the host dying removes them from the game (created cards are never discarded).
        var st = e.State; st.Cards[mine[0].Id].Modifiers = [new("test-fixture", "test-fixture", "Force", -20)];
        var fight = Engine(st);
        while (fight.State.PendingChoice is not null) Accept(fight, Cmd(fight, "declineReaction"));
        while (fight.State.Phase == Phase.Battle && fight.State.CurrentBattleProvince is not null) Accept(fight, Cmd(fight, "pass"));
        Check(followers.All(f => fight.State.Cards[f.Id].Zone == Zone.RemovedFromGame || fight.State.Cards[f.Id].Zone == Zone.Attached), "no Follower ends in a discard pile");
        Check(fight.State.Cards.Values.All(c => c.DefinitionId != NakanusCreations || c.Zone is Zone.RemovedFromGame or Zone.Attached), "created Followers are never in a discard pile");
    }

    private static void NakanuTechniqueRules()
    {
        // Bowed Personalities are not performers; with every one bowed nothing is offered.
        var (e, technique, mine, _) = Nakanu();
        var bowedState = e.State; bowedState.Cards[mine[0].Id].Bowed = true;
        var b = Engine(bowedState); Engage(b, 0);
        Check(Offers(b, 0, "nakanu-technique-create").Where(a => a.Enabled).Select(a => a.Command!.PerformerId).SequenceEqual([mine[1].Id]), "only the unbowed one: " + Describe(b, 0, "nakanu-technique-create"));
        Reject(b, new GameCommand("useAbility", 0, b.Version, CardId: technique.Id, AbilityId: "nakanu-technique-create", PerformerId: mine[0].Id));
        // Not opposed: no enemy at the battlefield, so there is no performer.
        var (alone, _, _, _) = Nakanu(enemyThere: false);
        var lonely = alone.State;
        var la = Engine(lonely);
        if (la.QueryActions(0).Any(o => o.Type == "selectBattle" && o.Enabled))
        {
            Accept(la, la.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
            Check(!Offers(la, 0, "nakanu-technique-create").Any(a => a.Enabled), "an unopposed Personality cannot perform it: " + Describe(la, 0, "nakanu-technique-create"));
        }
        // Not a Personality at home, not the enemy's: only those at the battlefield, on your side.
        var s = Selection();
        var at = Put(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var home = Put(s, "training-crane-samurai", 0, Zone.Home);
        var foe = Put(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); var tech = Put(s, NakanuTechnique, 0, Zone.Hand);
        var f = Engine(s); Engage(f, 0);
        Reject(f, new GameCommand("useAbility", 0, f.Version, CardId: tech.Id, AbilityId: "nakanu-technique-create", PerformerId: home.Id));
        Reject(f, new GameCommand("useAbility", 0, f.Version, CardId: tech.Id, AbilityId: "nakanu-technique-create", PerformerId: foe.Id));
        Accept(f, Offer(f, 0, "nakanu-technique-create", tech.Id, performerId: at.Id));
        Check(!Offers(f, 0, "nakanu-technique-create").Any(a => a.Enabled), "spent: the Strategy left the hand");
        // Not in the Action phase: an "after engaging" Reaction has no other window.
        var act = Table(Phase.Action);
        Put(act, NakanuTechnique, 0, Zone.Hand); Put(act, "training-lion-samurai", 0, Zone.Home);
        Check(!Offers(Engine(act), 0, "nakanu-technique-create").Any(a => a.Enabled), "only inside the engage window");
        // The Defender's copy works for the Defender's army.
        var (d, dt, dmine, _) = Nakanu(owner: 1);
        d.State.ActivePlayerId = 0;
        Engage(d, 1);
        var dcmd = Offer(d, 1, "nakanu-technique-create", dt.Id, performerId: dmine[0].Id);
        Accept(d, dcmd);
        var made = CreatedOf(d, NakanusCreations);
        Check(made.Length == 2 && made.All(x => x.OwnerId == 1 && x.AttachedTo == dmine[0].Id), "created for the Defender, on the Defender's Personality");
    }

    private static void ComputedValues()
    {
        var s = Table(Phase.Action);
        Put(s, ValueMaker, 0, Zone.Home);
        var target = Put(s, "training-crane-dignitary", 0, Zone.Home);
        var e = Engine(s);
        var offer = e.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-create-computed" && a.TargetIds!.Contains(target.Id));
        var command = offer.Command! with { TargetId = target.Id };
        var def = CardCatalog.Get("training-crane-dignitary");
        Accept(e, command);
        var made = Created(e, LionAncestor);
        Check(made.Printed is { } p && p.Force == def.PersonalHonor && p.Chi == def.Chi && p.PersonalHonor == def.PersonalHonor - 1, $"Force = Personal Honor {def.PersonalHonor}, Chi {def.Chi}, Personal Honor {def.PersonalHonor - 1}: {made.Printed?.Force}/{made.Printed?.Chi}/{made.Printed?.PersonalHonor}");
        var view = e.GetView(1).Players[0].Home.Single(c => c.Id == made.Id);
        Eq(view.Force, def.PersonalHonor, "the view's Force is the computed one"); Eq(view.Chi, def.Chi);
        // Printed, never current: modifiers and tokens on the subject do not change what is read.
        var t = Table(Phase.Action);
        Put(t, ValueMaker, 0, Zone.Home); var target2 = Put(t, "training-crane-dignitary", 0, Zone.Home);
        target2.Modifiers = [new("test-fixture", "test-fixture", "PersonalHonor", 3), new("test-fixture", "test-fixture", "Chi", -1)];
        var f = Engine(t);
        Accept(f, f.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-create-computed" && a.TargetIds!.Contains(target2.Id)).Command! with { TargetId = target2.Id });
        Check(Created(f, LionAncestor).Printed is { } q && q.Force == def.PersonalHonor && q.Chi == def.Chi, "the subject's current values are not read");
        // A keyword adds its bonus: the same fixture on a Paragon; and the value never drops below zero.
        var u = Table(Phase.Action);
        Put(u, ValueMaker, 0, Zone.Home); var paragon = Put(u, "training-lion-samurai", 0, Zone.Home);
        paragon.GrantedKeywords = [new("Paragon", "test-fixture", "Permanent")];
        var g = Engine(u);
        Accept(g, g.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-create-computed" && a.TargetIds!.Contains(paragon.Id)).Command! with { TargetId = paragon.Id });
        var lion = CardCatalog.Get("training-lion-samurai");
        Check(Created(g, LionAncestor).Printed is { } r && r.Force == lion.PersonalHonor + 2 && r.PersonalHonor == Math.Max(0, lion.PersonalHonor - 1), $"a Paragon: Personal Honor {lion.PersonalHonor} + 2 Force; got {Created(g, LionAncestor).Printed?.Force}");
        var zero = Table(Phase.Action);
        Put(zero, ValueMaker, 0, Zone.Home); var low = Put(zero, YourNameIsMine, 0, Zone.Home);
        var h = Engine(zero);
        Accept(h, h.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-create-computed" && a.TargetIds!.Contains(low.Id)).Command! with { TargetId = low.Id });
        Check(Created(h, LionAncestor).Printed is { Force: 0, Chi: 5, PersonalHonor: 0 }, $"a Personal Honor of 0 gives Force 0, and 0 - 1 is floored at 0: {Created(h, LionAncestor).Printed?.Force}/{Created(h, LionAncestor).Printed?.PersonalHonor}");
    }

    private static void ValueSubjectsAndCounts()
    {
        // A created attachment carries its own printed values too: the Scout's Force is its host's Chi.
        var wState = Table(Phase.Action);
        Put(wState, ValueMaker, 0, Zone.Home); var carrier = Put(wState, "training-crane-dignitary", 0, Zone.Home);
        var ew = Engine(wState);
        var unitBefore = ew.GetView(0).Players[0].Home.Single(c => c.Id == carrier.Id).UnitForce;
        Accept(ew, ew.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-create-strong-scout" && a.TargetIds!.Contains(carrier.Id)).Command! with { TargetId = carrier.Id });
        var scout = Created(ew, "oracle-l5r-9714");
        var scoutChi = CardCatalog.Get("training-crane-dignitary").Chi;
        Check(scout.Printed is { Force: var scoutForce, Name: "Kyuden Hida Survivor" } && scoutForce == scoutChi && scout.AttachedTo == carrier.Id, $"the attached Scout's printed Force is its host's Chi {scoutChi}");
        Eq(ew.GetView(1).Players[0].Home.Single(c => c.Id == carrier.Id).UnitForce, unitBefore + scoutChi, "the unit's Force counts the created Follower's own Force");
        // Counts: two Personalities together, own ids, one entry window (no Reaction window here, so none opens and no priority changes).
        var s = Table(Phase.Action);
        var maker = Put(s, ValueMaker, 0, Zone.Home);
        var e = Engine(s);
        var honor = e.State.Players[0].Honor; var rng = e.State.RngState;
        Accept(e, Offer(e, 0, "training-create-pair", maker.Id));
        var pair = CreatedOf(e, LionAncestor);
        Eq(pair.Length, 2); Check(pair.All(c => c.Zone == Zone.Home && c.OwnerId == 0 && c.Printed is null && c.FaceUp), "two created at home, no per-instance values"); Check(pair[0].Id != pair[1].Id);
        Eq(e.State.Events.Count(x => x.Type == "cardCreated"), 2); Eq(e.State.RngState, rng); Eq(e.State.Players[0].Honor, honor);
        // A performer's title and the source's Gold Cost plus one.
        var t = Table(Phase.Action);
        var maker2 = Put(t, ValueMaker, 0, Zone.Home); var performer = Put(t, "training-crane-dignitary", 0, Zone.Home);
        var f = Engine(t);
        Accept(f, Offer(f, 0, "training-create-titled", maker2.Id, performerId: performer.Id));
        var titled = Created(f, YourNameIsMine);
        Check(titled.Printed is { GoldCost: 6 } p && p.Name == CardCatalog.Get("training-crane-dignitary").Name && p.Force == 9 && p.Chi == 5, $"the performer's title, the source's Gold Cost {CardCatalog.Get(ValueMaker).GoldCost} plus 1: {titled.Printed?.Name} {titled.Printed?.GoldCost}");
        // The wielder: an Item carrying the fixture ability names the Personality it is attached to.
        var u = Table(Phase.Action);
        var host = Put(u, "training-crane-dignitary", 0, Zone.Home);
        var item = Put(u, ValueItem, 0, Zone.Attached, parent: host.Id);
        var g = Engine(u);
        Accept(g, Offer(g, 0, "training-create-from-wielder", item.Id));
        var copy = Created(g, LionAncestor);
        Check(copy.Printed is { } w && w.Name == CardCatalog.Get("training-crane-dignitary").Name && w.Force == CardCatalog.Get("training-crane-dignitary").Force && w.Chi == CardCatalog.Get(LionAncestor).Chi, "the wielder's title and Force, the scan's Chi");
        // The wielder must exist: the same Item in a hand is offered disabled, and a hand-built command is refused.
        var v = Table(Phase.Action);
        var loose = Put(v, ValueItem, 0, Zone.Hand);
        var ev = Engine(v);
        Check(Offers(ev, 0, "training-create-from-wielder").All(o => !o.Enabled) && Offers(ev, 0, "training-create-from-wielder").Length > 0, "no wielder, no action: " + Describe(ev, 0, "training-create-from-wielder"));
        Reject(ev, new GameCommand("useAbility", 0, ev.Version, CardId: loose.Id, AbilityId: "training-create-from-wielder"));
        // A created card's values are public: the other player sees them.
        var seen = g.GetView(1).Players[0].Home.Single(c => c.Id == copy.Id);
        Check(seen.Definition!.Name == CardCatalog.Get("training-crane-dignitary").Name && seen.Force == CardCatalog.Get("training-crane-dignitary").Force, "public");
    }
}
