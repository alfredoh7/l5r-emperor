using L5R.Core;
using static RegressionAssertions;

// Primitive P9 (issue #672): the stat-term registry (StatTerms.cs) and its eight demonstration cards (personality-wave-130, StatTermCards.cs), each wired from its own scan.
// Positions are explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion; in a Battle phase province 0 is the current battlefield, player 1 the Attacker). Stats are
// read through the engine's own getters; the army totals through ArmyForces and through a battle the engine resolves.
internal static class StatTermsTests
{
    private const string Tochiko = "oracle-l5r-10468", Saiga = "oracle-l5r-10490", Ogata = "oracle-l5r-10441", Sango = "oracle-l5r-10294", Manami = "oracle-l5r-10654",
        Daiken = "oracle-l5r-10673", Ogaru = "oracle-l5r-10326", Toganin = "oracle-l5r-10859";
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai", Follower = "training-follower", Weapon = "training-recon-weapon", Chikara = "oracle-l5r-1319";
    private static readonly string[] Ids = [Tochiko, Saiga, Ogata, Sango, Manami, Daiken, Ogaru, Toganin];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, 0);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, province: host.ProvinceIndex, parent: host.Id);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static int Force(MatchState s, CardInstance card) => MatchEngine.FromTestState(s).CardForceForTest(card.Id);
    private static int Chi(MatchState s, CardInstance card) => MatchEngine.FromTestState(s).PersonalityChiForTest(card.Id);
    private static (int Attacker, int Defender) Armies(MatchState s) => MatchEngine.FromTestState(s).ArmyForcesForTest();
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void Tokens(CardInstance card, string type, int count) { for (var n = 0; n < count; n++) card.Tokens.Add(new($"tk-{card.Id}-{type}-{card.Tokens.Count}", type, "test-fixture")); }
    private static void Modify(CardInstance card, string stat, int amount) => card.Modifiers.Add(new("test-source", "test", stat, amount));
    private static string Resolved(MatchEngine e) => string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message));

    public static void Run(TestRunner runner) => runner.Run("stat-terms", [
        ("Published as printed: stats, keywords, text, abilities and effects of the eight cards from their own scans, manifest, art and provenance", PublishedShape),
        ("The registry: every card Effect has its set, the P9 callbacks are registered, and a non-positive Chi term, an army Chi term or a repeated id is refused", RegistryShape),
        ("Tochiko: +2F only while opposing a Personality or Follower with lower Force -- not at home, unopposed, against equal or higher Force, or across battlefields; stacks with a modifier once", TochikoLowerForce),
        ("Tochiko against Tochiko: the term compares Force without itself, so two of them neither loop nor both claim the bonus", TochikoMirror),
        ("Toku Saiga: +1F/+1C for each of his Weapons -- not other Items, not another Personality's Weapons, live as they come and go", SaigaWeapons),
        ("Toku Saiga: a Weapon that was all that kept his Chi above 0 takes him with it when an action destroys it -- he is never left in play at 0 Chi", SaigaChiDeath),
        ("Hiruma Ogata: +1F for each Madness token on opposing cards (enemy Personalities and their attachments at his battlefield) -- not his own side's, not at home", OgataMadness),
        ("Hiruma Ogata: entering play costs 2 Honor (mandatory, no window)", OgataEntry),
        ("Matsu Sango: contributes his Force twice to the army total while attacking -- once while defending, nothing while bowed, his Follower once, his own Force unchanged", SangoTwice),
        ("Matsu Sango: the resolution the engine runs totals him twice", SangoResolution),
        ("Matsu Sango: will not attach Items; a Follower attaches", SangoItems),
        ("Bayushi Manami - exp: her army has +2F when the armies are totalled, attacking or defending, bowed or not -- not from home, and her own Force is unchanged", ManamiArmy),
        ("Bayushi Manami - exp: the resolution the engine runs is the sum of the contributions plus her +2", ManamiResolution),
        ("Moto Daiken: +1F while her controller controls an Ancestor Personality or has a dead Personality in his discard pile -- once, not per condition; not the opponent's", DaikenAncestors),
        ("Moto Ogaru: +1F while at a battlefield and the enemy leader controls an Imperial card or the Imperial Favor -- not his own side's, not at home", OgaruEnemyLeader),
        ("Yoritomo Toganin: +1F if any other player is Crane Clan -- his own controller being Crane is not another player", ToganinCrane),
        ("Yoritomo Toganin: Invest 5 Gold as he enters play gives three +1F tokens, once; declined, none; it is a trait, not a Reaction", ToganinInvest),
        ("EngineHooks.FollowerForce: a Follower carrying a term has it added to its own Force, live", FollowerTerms),
    ]);

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", "personality-wave-130.txt")).Where(l => l.Length > 0).ToArray();
        Check(new HashSet<string>(listed).SetEquals(Ids) && listed.Length == Ids.Length, "personality-wave-130's manifest is exactly its cards");
        foreach (var id in Ids) Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/personality-wave-130-sources.json"))).RootElement;
        Eq(sources.GetArrayLength(), Ids.Length, "one provenance row per card");
        var sets = new Dictionary<string, string>
        {
            [Tochiko] = "coils-of-madness",
            [Saiga] = "coils-of-madness",
            [Ogata] = "coils-of-madness",
            [Sango] = "torn-asunder",
            [Manami] = "gates-of-chaos",
            [Daiken] = "gates-of-chaos",
            [Ogaru] = "torn-asunder",
            [Toganin] = "aftermath",
        };
        foreach (var row in sources.EnumerateArray())
            Check(row.GetProperty("image_path").GetString()!.Contains($"/oracle/{sets[row.GetProperty("card_id").GetString()!]}/"), $"{row.GetProperty("card_id")} comes from its own set's scan");

        // (Force, Chi, Personal Honor, Gold, Honor Requirement, keywords, abilities, effect) read off each scan.
        void Is(string id, string name, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string[] abilities, string effect, string text, int? entryLoss = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Type}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect"); Eq(c.Text, text, $"{name}: text"); Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
            Check(AbilityCatalog.All.Keys.Intersect(abilities).Count() == abilities.Length, $"{name}: every ability id exists");
        }
        Is(Tochiko, "Tochiko, the Jagged Tusk of Death", "Mantis", 5, 1, 0, 6, null, ["Unique", "Boar Clan", "Mantis Clan", "Samurai"], [], StatTermCards.TochikoEffect,
            "Tochiko has +2F while opposing a Personality or Follower with lower Force.");
        Is(Saiga, "Toku Saiga, the Metal Storm", "Spider", 3, 2, 2, 5, null, ["Kensai", "Unique", "Monkey Clan", "Samurai", "Spider Clan"], [], StatTermCards.SaigaEffect, "Saiga has +1F/+1C for each of his Weapons.");
        Is(Ogata, "Hiruma Ogata", "Crab", 3, 3, 0, 5, null, ["Crab Clan", "Fallen", "Samurai", "Scout"], [], StatTermCards.OgataEffect,
            "After Ogata enters play, lose 2 Honor.\nOgata has +1F for each opposing Madness token.", 2);
        Is(Sango, "Matsu Sango", "Lion", 1, 3, 3, 5, 7, ["Lion Clan", "Samurai"], [], StatTermCards.SangoEffect,
            "Will not attach Items.\nDuring battle resolutions, if Sango is attacking, he contributes his Force twice.");
        Is(Manami, "Bayushi Manami", "Scorpion", 2, 3, 1, 6, null, ["Loyal", "Unique", "Experienced", "Paragon", "Samurai", "Scorpion Clan", "Yojimbo"], [], StatTermCards.ManamiEffect,
            "During battle resolution, Manami's army has +2F.");
        Is(Daiken, "Moto Daiken", "Unicorn", 3, 4, 1, 7, null, ["Cavalry", "Death Priest", "Shugenja", "Unicorn Clan", "Water"], [], StatTermCards.DaikenEffect,
            "While you control any Ancestor Personalities or have any dead Personalities in your discard pile, Daiken has +1F.");
        Is(Ogaru, "Moto Ogaru", "Unicorn", 3, 1, 2, 5, 4, ["Cavalry", "Commander", "Samurai", "Unicorn Clan"], [], StatTermCards.OgaruEffect,
            "Ogaru has +1F while the enemy leader controls an Imperial card or the Imperial Favor.");
        Is(Toganin, "Yoritomo Toganin", "Mantis", 2, 3, 0, 4, null, ["Naval", "Magistrate", "Mantis Clan", "Samurai"], [StatTermCards.ToganinInvest], StatTermCards.ToganinEffect,
            "Invest :g5:: Give Toganin three +1F tokens. (After this card enters play, you may also pay the Invest cost to get the effect, once.)\nToganin has +1F if any other player is Crane Clan.");
        var invest = AbilityCatalog.All[StatTermCards.ToganinInvest];
        Check(invest.Timing == "Reaction" && invest.Trigger == "own-enters-play" && invest.Cost == GoldCostRule.Fixed(5), "Toganin's Invest: offered as he enters play, for 5 Gold");
        Check(TraitReactions.Is(StatTermCards.ToganinInvest), "Invest is a trait, not a Reaction (ruling R4)");
    }

    private static void RegistryShape()
    {
        foreach (var id in Ids) Check(StatTermCatalog.ByEffect.ContainsKey(CardCatalog.Get(id).Effect), $"{CardCatalog.Get(id).Name}: its Effect has a stat-term set");
        Check(EngineHooks.PersonalityForce.Names.Contains("P9StatTermPersonalityForce") && EngineHooks.FollowerForce.Names.Contains("P9StatTermFollowerForce")
            && EngineHooks.PersonalityChi.Names.Contains("P9StatTermPersonalityChi") && EngineHooks.ArmyForce.Names.Contains("P9StatTermArmyForce")
            && EngineHooks.StatTerms.Names.Contains("P9StatTermCards"), "the P9 callbacks are registered on their channels");
        var sango = StatTermCatalog.ByEffect[StatTermCards.SangoEffect];
        Check(sango.Terms.Length == 0 && sango.Contribution is { Multiplier: 2 }, "Sango's set is a contribution rule, not a Force term");
        Check(StatTermCatalog.ByEffect[StatTermCards.ManamiEffect].Terms.Single() is { Scope: TermScope.Army, Stat: TermStat.Force, Amount: 2 }, "Manami's is an army term");

        static bool Refused(params StatTermSet[] sets)
        {
            try { StatTermCatalog.ValidateSets(sets); return false; }
            catch (InvalidOperationException) { return true; }
        }
        Check(!Refused(new StatTermSet("x", [new("x-chi", TermScope.Self, TermStat.Chi, 1)])), "a positive Chi term is accepted");
        Check(Refused(new StatTermSet("x", [new("x-chi", TermScope.Self, TermStat.Chi, -1)])), "a static Chi penalty is refused: it would bypass the Chi 0 rule");
        Check(Refused(new StatTermSet("x", [new("x-chi", TermScope.Self, TermStat.Chi, 0)])), "and so is a Chi term of 0");
        Check(Refused(new StatTermSet("x", [new("x-army", TermScope.Army, TermStat.Chi, 1)])), "an army term is a Force term");
        Check(Refused(new StatTermSet("x", [new("same", TermScope.Self, TermStat.Force, 1)]), new StatTermSet("y", [new("same", TermScope.Self, TermStat.Force, 1)])), "term ids are unique");
        Check(Refused(new StatTermSet("none", [])), "a set names the Effect that carries it");
        Check(Refused(new StatTermSet("x", [], new ContributionRule("x-rule", -1))), "a contribution multiplier is not negative");
    }

    private static void TochikoLowerForce()
    {
        var home = Table(); var atHome = Add(home, Tochiko, 0, Zone.Home); Bf(home, Lion, 1);
        Eq(Force(home, atHome), 5, "at home she opposes nothing");

        var alone = Table(); var unopposed = Bf(alone, Tochiko, 1);
        Eq(Force(alone, unopposed), 5, "unopposed: no bonus");

        var s = Table(); var tochiko = Bf(s, Tochiko, 0); var lion = Bf(s, Lion, 1);
        Eq(Force(s, tochiko), 7, "opposing a 4F Personality: 5F +2F");
        Eq(Force(s, lion), 4, "the opposing card is untouched");
        Modify(lion, "Force", 1);
        Eq(Force(s, tochiko), 5, "against equal Force (5): no bonus");
        Modify(lion, "Force", 3);
        Eq(Force(s, tochiko), 5, "against higher Force: no bonus");
        Attach(s, Follower, lion);
        Eq(Force(s, tochiko), 7, "a 2F Follower in the opposing unit is a lower-Force card");

        var m = Table(); var boosted = Bf(m, Tochiko, 0); Bf(m, Lion, 1); Modify(boosted, "Force", 1);
        Eq(Force(m, boosted), 8, "5F printed +1F modifier +2F term: each counted once");
        boosted.Bowed = true;
        Eq(Force(m, boosted), 8, "bowed, she still opposes and still has the bonus");

        var far = Table(); var here = Bf(far, Tochiko, 0); Add(far, Lion, 1, Zone.Battlefield, province: 1);
        Eq(Force(far, here), 5, "an enemy at another battlefield is not opposing her");
        var own = Table(); var mine = Bf(own, Tochiko, 0); Bf(own, Crane, 0);
        Eq(Force(own, mine), 5, "her own side's weaker Personality is not opposing her");
    }

    private static void TochikoMirror()
    {
        var s = Table(); var a = Bf(s, Tochiko, 0); var b = Bf(s, Tochiko, 1);
        Eq(Force(s, a), 5, "5F against 5F: neither is lower"); Eq(Force(s, b), 5, "and the same for the other");
        Modify(a, "Force", 1);
        Eq(Force(s, a), 8, "6F without the term against 5F: +2F");
        Eq(Force(s, b), 5, "the other one is not lower than her");
    }

    private static void SaigaWeapons()
    {
        var s = Table(Phase.Action, 0); var saiga = Add(s, Saiga, 0, Zone.Home);
        Eq(Force(s, saiga), 3, "no Weapon: 3F"); Eq(Chi(s, saiga), 2, "no Weapon: 2C");
        Attach(s, "training-item", saiga); // +2F, not a Weapon
        Eq(Force(s, saiga), 5, "an Item that is not a Weapon gives its own Force only"); Eq(Chi(s, saiga), 2, "and no Chi");
        var first = Attach(s, Weapon, saiga); // +1F, a Weapon
        Eq(Force(s, saiga), 7, "one Weapon: its own +1F and the term's +1F"); Eq(Chi(s, saiga), 3, "one Weapon: +1C");
        Attach(s, Weapon, saiga);
        Eq(Force(s, saiga), 9, "two Weapons (he is a Kensai): +1F each from the term"); Eq(Chi(s, saiga), 4, "two Weapons: +2C");
        var friend = Add(s, Crane, 0, Zone.Home); Attach(s, Weapon, friend);
        Eq(Force(s, saiga), 9, "another Personality's Weapon is not his"); Eq(Chi(s, friend), 3, "and gives that Personality no Chi");
        s.Cards.Remove(first.Id);
        Eq(Force(s, saiga), 7, "live: one Weapon fewer"); Eq(Chi(s, saiga), 3, "live: one Chi fewer");
    }

    private static void SaigaChiDeath()
    {
        var s = Table(Phase.Battle, 1);
        var saiga = Bf(s, Saiga, 0); var blade = Attach(s, Weapon, saiga);
        Modify(saiga, "Chi", -2);
        var chikara = Bf(s, Chikara, 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalityChiForTest(saiga.Id), 1, "2C -2C, and +1C for his Weapon: alive at 1 Chi");
        Accept(e, Batch336Support.Offer(e, 1, "komatsu-destroy-attachment", chikara.Id, blade.Id));
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PriorityPlayerId;
            Accept(e, e.QueryActions(actor).First(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass").Command!);
        }
        Check(e.State.Cards[blade.Id].Zone != Zone.Attached, "the Weapon was destroyed");
        Check(e.State.Cards[saiga.Id].Zone is not (Zone.Battlefield or Zone.Home), $"Saiga went with it at 0 Chi (he is in {e.State.Cards[saiga.Id].Zone})");
    }

    private static void OgataMadness()
    {
        var s = Table(); var ogata = Bf(s, Ogata, 0); var enemy = Bf(s, Lion, 1); var enemyFollower = Attach(s, Follower, enemy);
        Eq(Force(s, ogata), 3, "no Madness token: 3F");
        Tokens(enemy, "Madness", 2);
        Eq(Force(s, ogata), 5, "+1F for each of two Madness tokens on the opposing Personality");
        Tokens(enemyFollower, "Madness", 1);
        Eq(Force(s, ogata), 6, "a token on an opposing Follower counts too");
        Tokens(enemy, "Fire", 1);
        Eq(Force(s, ogata), 6, "another kind of token does not");
        var ally = Bf(s, Crane, 0); Tokens(ally, "Madness", 1); Tokens(ogata, "Madness", 1);
        Eq(Force(s, ogata), 6, "his own side's Madness tokens are not opposing");
        var elsewhere = Add(s, Lion, 1, Zone.Home); Tokens(elsewhere, "Madness", 3);
        Eq(Force(s, ogata), 6, "an enemy at home is not opposing him");

        var h = Table(); var atHome = Add(h, Ogata, 0, Zone.Home); var foe = Bf(h, Lion, 1); Tokens(foe, "Madness", 2);
        Eq(Force(h, atHome), 3, "at home he opposes nothing");
    }

    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[cardId]);
    }

    private static void OgataEntry()
    {
        var (e, ogata) = Recruit(Ogata);
        var before = e.State.Players[0].Honor;
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: ogata.Id, SourceIds: e.QuotePurchase(0, ogata.Id).Payments[0].SourceIds));
        Eq(e.State.Cards[ogata.Id].Zone, Zone.Home, "recruited");
        Eq(e.State.Players[0].Honor, before - 2, "After Ogata enters play, lose 2 Honor");
        Check(e.State.PendingChoice is null, "a mandatory trait opens no window");
    }

    private static void SangoTwice()
    {
        var s = Table(); var sango = Bf(s, Sango, 1); Bf(s, Lion, 0);
        Eq(Armies(s), (2, 4), "attacking: 1F counted twice");
        Eq(Force(s, sango), 1, "his Force itself is still 1");
        Modify(sango, "Force", 2);
        Eq(Armies(s), (6, 4), "attacking with +2F: 3F counted twice");
        Attach(s, Follower, sango);
        Eq(Armies(s), (8, 4), "his 2F Follower contributes once");
        sango.Bowed = true;
        Eq(Armies(s), (2, 4), "bowed he contributes nothing, twice; the Follower still does");

        var d = Table(); Bf(d, Sango, 0); Bf(d, Lion, 1);
        Eq(Armies(d), (4, 1), "defending: once");
    }

    private static void SangoResolution()
    {
        var s = Table(); var sango = Bf(s, Sango, 1); Modify(sango, "Force", 2); Bf(s, Crane, 0);
        var e = MatchEngine.FromTestState(s);
        Batch336Support.RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message.Contains("attacking Force 6, defending Force 3")), "3F twice against 3F: " + Resolved(e));
    }

    private static void SangoItems()
    {
        var s = Table(Phase.Action, 0);
        var sango = Add(s, Sango, 0, Zone.Home); var other = Add(s, Crane, 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Hand); var follower = Add(s, Follower, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var why = Batch336Support.Rejection(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, TargetId: sango.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Check(why.Length > 0, "an Item will not attach to Sango");
        Check(!e.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == item.Id && a.Enabled && (a.Command?.TargetId == sango.Id || (a.TargetIds ?? []).Contains(sango.Id))), "and he is not offered as its target");
        Check(e.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == item.Id && a.Enabled && (a.Command?.TargetId == other.Id || (a.TargetIds ?? []).Contains(other.Id))), "another Personality is");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: follower.Id, TargetId: sango.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Eq(e.State.Cards[follower.Id].AttachedTo, sango.Id, "a Follower attaches");
    }

    private static void ManamiArmy()
    {
        var s = Table(); var manami = Bf(s, Manami, 0); Bf(s, Lion, 1);
        Eq(Armies(s), (4, 4), "defending: her 2F and her army's +2F");
        Eq(Force(s, manami), 2, "her own Force is unchanged");
        manami.Bowed = true;
        Eq(Armies(s), (4, 2), "bowed she contributes no Force, but her army still has +2F");
        Bf(s, Crane, 0);
        Eq(Armies(s), (4, 5), "the bonus is the army's, once: 3F and +2F");

        var a = Table(); Bf(a, Manami, 1); Bf(a, Crane, 0);
        Eq(Armies(a), (4, 3), "attacking: the same +2F");

        var h = Table(); Add(h, Manami, 0, Zone.Home); Bf(h, Crane, 0); Bf(h, Lion, 1);
        Eq(Armies(h), (4, 3), "at home she is in no army");
    }

    private static void ManamiResolution()
    {
        var s = Table(); Bf(s, Manami, 0); Bf(s, Crane, 0); Bf(s, Lion, 1);
        var e = MatchEngine.FromTestState(s);
        Batch336Support.RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message.Contains("attacking Force 4, defending Force 7")), "2F and 3F and the army's +2F against 4F: " + Resolved(e));
    }

    private static void DaikenAncestors()
    {
        var s = Table(Phase.Action, 0); var daiken = Add(s, Daiken, 0, Zone.Home);
        Eq(Force(s, daiken), 3, "neither: 3F");
        Add(s, "training-ancestor", 1, Zone.Home); Add(s, Lion, 1, Zone.Dead);
        Eq(Force(s, daiken), 3, "the opponent's Ancestor and the opponent's dead Personality are not hers");
        Add(s, Lion, 0, Zone.DynastyDiscard); Add(s, Follower, 0, Zone.Dead);
        Eq(Force(s, daiken), 3, "a discarded (not dead) Personality and a dead card that is no Personality do not count");
        var ancestor = Add(s, "training-ancestor", 0, Zone.Home);
        Eq(Force(s, daiken), 4, "she controls an Ancestor Personality: +1F");
        var dead = Add(s, Crane, 0, Zone.Dead);
        Eq(Force(s, daiken), 4, "both conditions: still +1F, once");
        s.Cards.Remove(ancestor.Id);
        Eq(Force(s, daiken), 4, "a dead Personality in her discard pile alone: +1F");
        s.Cards.Remove(dead.Id);
        Eq(Force(s, daiken), 3, "live: gone with the last of them");
    }

    private static void OgaruEnemyLeader()
    {
        var s = Table(); var ogaru = Bf(s, Ogaru, 1); Bf(s, Crane, 0);
        Eq(Force(s, ogaru), 3, "no Favor, no Imperial card: 3F");
        s.FavorPlayerId = 1; var mine = Add(s, Lion, 1, Zone.Home); GiveKeyword(mine, "Imperial");
        Eq(Force(s, ogaru), 3, "his own controller's Favor and Imperial card are not the enemy leader's");
        s.FavorPlayerId = 0;
        Eq(Force(s, ogaru), 4, "the enemy leader has the Imperial Favor: +1F");
        var theirs = Add(s, Crane, 0, Zone.Home); GiveKeyword(theirs, "Imperial");
        Eq(Force(s, ogaru), 4, "the Favor and an Imperial card: still +1F, once");
        s.FavorPlayerId = null;
        Eq(Force(s, ogaru), 4, "the enemy leader controls an Imperial card: +1F");

        var h = Table(); var atHome = Add(h, Ogaru, 1, Zone.Home); h.FavorPlayerId = 0;
        Eq(Force(h, atHome), 3, "at home he is in no battle, so there is no enemy leader");
    }

    private static void ToganinCrane()
    {
        var s = Table(Phase.Action, 0);
        var lions = Add(s, Toganin, 1, Zone.Home); var cranes = Add(s, Toganin, 0, Zone.Home);
        Eq(s.Players[0].Clan, "Crane", "fixture: player 0 is the Crane"); Check(s.Players[1].Clan != "Crane", "fixture: player 1 is not");
        Eq(Force(s, lions), 3, "another player is Crane Clan: 2F +1F");
        Eq(Force(s, cranes), 2, "his own controller is the only Crane: no bonus");
    }

    private static void ToganinInvest()
    {
        var (e, toganin) = Recruit(Toganin);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: toganin.Id, SourceIds: e.QuotePurchase(0, toganin.Id).Payments[0].SourceIds));
        var offer = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == StatTermCards.ToganinInvest);
        Check(offer is not null, "the Invest is offered as he enters play: " + string.Join("; ", e.QueryActions(0).Select(x => $"{x.Type}:{x.Label} [{x.Enabled}] {x.Reason}")));
        Eq(offer!.Gold?.Cost, 5, "for 5 Gold");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: toganin.Id, AbilityId: StatTermCards.ToganinInvest, SourceIds: e.PaymentOptions(0, 5).First().SourceIds));
        Eq(e.State.Cards[toganin.Id].Tokens.Count(t => t.Type == "Invest" && t.Stat == "Force" && t.Amount == 1), 3, "three +1F tokens");
        Eq(e.CardForceForTest(toganin.Id), 5, "2F and three +1F tokens (his controller is the Crane, so no Clan bonus)");
        Check(!e.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == StatTermCards.ToganinInvest), "once");

        var (d, declined) = Recruit(Toganin);
        Accept(d, new GameCommand("purchase", 0, d.Version, CardId: declined.Id, SourceIds: d.QuotePurchase(0, declined.Id).Payments[0].SourceIds));
        Accept(d, d.QueryActions(0).First(a => a.Enabled && a.Type == "declineReaction").Command!);
        Eq(d.State.Cards[declined.Id].Tokens.Count, 0, "declined: no tokens");
        Eq(d.CardForceForTest(declined.Id), 2, "and his printed Force");
    }

    private static void FollowerTerms()
    {
        var s = Table(); var host = Bf(s, Crane, 0);
        var plain = Attach(s, Follower, host);
        var carrier = Attach(s, Follower, host); carrier.Printed = CardCatalog.Get(Follower) with { Effect = StatTermCards.DaikenEffect };
        Eq(Force(s, carrier), 2, "no condition holds: its printed 2F"); Eq(Force(s, plain), 2, "a Follower without a term");
        Add(s, Lion, 0, Zone.Dead);
        Eq(Force(s, carrier), 3, "the term's condition holds: +1F on the Follower"); Eq(Force(s, plain), 2, "and only on the Follower that carries it");

        var t = Table(); var unit = Bf(t, Crane, 0); var tusk = Attach(t, Follower, unit); tusk.Printed = CardCatalog.Get(Follower) with { Effect = StatTermCards.TochikoEffect };
        var foe = Bf(t, Lion, 1);
        Eq(Force(t, tusk), 2, "2F against a 4F Personality: nothing lower");
        Modify(foe, "Force", -3);
        Eq(Force(t, tusk), 4, "against a 1F Personality: +2F, read through its unit's battlefield");
        Eq(Armies(t), (1, 7), "and the army total counts the Follower's Force with the term");
    }
}
