using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Seeds of Decay pass (issue #453), batch 3: the batch's Personalities (personality-wave-55), Merihiko's Swarm (personality-wave-57) and Masayoshi's Blade
// (item-wave-10). Every position is built explicitly and driven through QueryActions and the commands it offers. The readings are in the batch report.
internal static class SeedsOfDecayBatch3Tests
{
    private const string Chukage = "oracle-l5r-10072", Jukko = "oracle-l5r-10073", Rikate = "oracle-l5r-10075", Makubesu = "oracle-l5r-10078", Seki = "oracle-l5r-10091",
        Kitahime = "oracle-l5r-10048", Mitsuru = "oracle-l5r-10049", Kaiji = "oracle-l5r-10040", Nitani = "oracle-l5r-10041", Okaru = "oracle-l5r-10043", Merihiko = "oracle-l5r-10065",
        Masayoshi = "oracle-l5r-10089", Koyama = "oracle-l5r-10064", Aoi = "oracle-l5r-10098", Hajime = "oracle-l5r-10090", Iaimiko = "oracle-l5r-10077", Hawado = "oracle-l5r-10081",
        Tanemono = "oracle-l5r-10096", Kohrogi = "oracle-l5r-10068", Hirononri = "oracle-l5r-10103", Heijin = "oracle-l5r-10083", Akukiho = "oracle-l5r-10070", Iwashi = "oracle-l5r-10071",
        Yurishi = "oracle-l5r-10088", Ino = "oracle-l5r-10066", Akikusa = "oracle-l5r-10046", Swarm = "oracle-l5r-10199", Blade = "oracle-l5r-10204", Conscripts = "oracle-l5r-10004";

    public static void Run(TestRunner runner) => runner.Run("seeds-batch-3", [
        ("Published shape: the 26 Personalities, Merihiko's Swarm and Masayoshi's Blade are the printed cards with the abilities and Effects named for them, ship their art, and their waves pin exactly them", PublishedShapes),
        ("Asako Chukage - exp: bows one of his controller's own unbowed Rings to draw a card (a bowed Ring or the opponent's is refused); bowed at home he reinforces an opposed battle and straightens, and may then send an enemy unit home only while the enemy outnumbers his side", Chukage_),
        ("Asako Jukko and Tanemono: an Exchange swaps the target's current Force and Chi (a Personality with 0 Force is refused), Jukko may switch his own too, and Tanemono switches himself; a Phoenix Clan player may not recruit Tanemono", Exchanges),
        ("Asako Rikate - exp, Kaiu Okaru, Moto Aoi, Matsu Koyama - exp: Force to 0 and -2C (a Chi of 2 destroys him), -3F with an additional Battle action, bow-for-+4F even from home, and each other Lion in the army gains his own Personal Honor", ForceEffects),
        ("Bayushi Makubesu, Kitsune Merihiko, Masayoshi and Chuda Seki entering play: two Conscripts dealt to one or two of his Personalities, the Swarm with its own bow-for-Ranged-4, the Blade attached to Masayoshi, and a -1C Plague token after Seki's own 3 Honor loss", EntryCreations),
        ("Daidoji Kitahime, Tsuruchi Kohrogi, Hiruma Nitani - exp: Ranged 4 bowing her without Reconnaissance (not with it), Ranged 2, Ranged 6, and Nitani destroys an enemy card with the printed Shadowlands keyword only (not a granted one)", RangedAndNitani),
        ("Hida Kaiji: another player's action moves him only from the second time each turn on; his Battle action bows an enemy card of lower Force and destroys it when it has no attachments", Kaiji_),
        ("Doji Mitsuru duels an enemy Personality and the loser is bowed", Mitsuru_),
        ("Ninube Hajime moves an enemy Personality at the battlefield home or to another unresolved battlefield (one use for both), and loses 2 Honor entering play", Hajime_),
        ("Shiba Iaimiko: -4F, and only with a Shugenja controlled and the target's Force now 0 may a card without attachments in its unit be destroyed", Iaimiko_),
        ("Shosuro Hawado - exp reacts after a Battle action resolves, from the battlefield only, moving its performer home or to another unresolved battlefield", HawadoExp),
        ("Utaku Hirononri bows to straighten a Stables or Cavalry card, either side's, nothing else", Hirononri_),
        ("Yogo Heijin brings his Courtier and an enemy Personality from anywhere to the current battlefield and straightens the enemy's unit", Heijin_),
        ("Yoritomo Akukiho attaches a Weapon from his hand paying its cost and takes an additional Battle action; a Follower in hand is refused", Akukiho_),
        ("Yoritomo Iwashi: winning a battle as an attacker stamps him; the Limited bow is offered on his controller's next turn and not once that turn has passed", Iwashi_),
        ("Goju Yurishi - exp may not be targeted by another player's action while another legal target exists, and may when he is the only one; his own controller may target him freely", Yurishi_),
        ("Moshi Ino's storm: the next action of a non-Thunder performer makes its player discard a card or bow the performer (a Thunder performer passes untouched), and she takes an additional Battle action", InoStorm),
        ("Asahina Akikusa gains 1 Honor after an action on her controller's Air Spell resolves, not after one on a Spell without Air", Akikusa_),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static MatchState Action() { var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3; return s; }
    private static ActionOption[] Offers(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static bool Offered(MatchEngine e, int player, string ability) => Offers(e, player, ability).Any(a => a.Enabled);
    private static void Decline(MatchEngine e) => Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));

    // Recruits a card sitting face-up in player 0's first province, the economy scenario's own purchase flow: the entry window (if any) is then open.
    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? arrange = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        arrange?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var expected = new (string Id, string Name, string[] Abilities, string Effect, int? EntryLoss)[]
        {
            (Chukage, "Asako Chukage", ["chukage-draw", "chukage-move"], "none", null), (Jukko, "Asako Jukko", ["jukko-switch"], "none", null), (Rikate, "Asako Rikate", ["rikate-exp-weaken"], "none", null),
            (Makubesu, "Bayushi Makubesu", ["makubesu-create"], "none", null), (Seki, "Chuda Seki", ["seki-plague"], "none", 3), (Kitahime, "Daidoji Kitahime", ["kitahime-attack"], "none", null),
            (Mitsuru, "Doji Mitsuru", ["mitsuru-duel"], "none", null), (Kaiji, "Hida Kaiji", ["kaiji-bow"], MatchEngine.NegateFirstEnemyMoveEachTurnEffect, null),
            (Nitani, "Hiruma Nitani", ["nitani-exp-attack", "nitani-exp-destroy"], "none", null), (Okaru, "Kaiu Okaru", ["okaru-weaken"], "none", null),
            (Merihiko, "Kitsune Merihiko", ["merihiko-create"], "none", null), (Masayoshi, "Masayoshi", ["masayoshi-create"], "none", null), (Koyama, "Matsu Koyama", ["koyama-exp-boost"], "none", null),
            (Aoi, "Moto Aoi", ["aoi-boost"], "none", null), (Hajime, "Ninube Hajime", ["hajime-move-home", "hajime-move-battlefield"], "none", 2), (Iaimiko, "Shiba Iaimiko", ["iaimiko-weaken"], "none", null),
            (Hawado, "Shosuro Hawado", ["hawado-exp-move-home", "hawado-exp-move-battlefield"], "none", null), (Tanemono, "Tanemono", ["tanemono-switch"], MatchEngine.TanemonoNoPhoenixControlEffect, null),
            (Kohrogi, "Tsuruchi Kohrogi", ["kohrogi-attack"], "none", null), (Hirononri, "Utaku Hirononri", ["hirononri-straighten"], "none", null), (Heijin, "Yogo Heijin", ["heijin-move"], "none", null),
            (Akukiho, "Yoritomo Akukiho", ["akukiho-attach"], "none", null), (Iwashi, "Yoritomo Iwashi", ["iwashi-bow"], "none", null), (Yurishi, "Goju Yurishi", [], MatchEngine.TargetOfLastResortEffect, 3),
            (Ino, "Moshi Ino", ["ino-storm"], "none", null), (Akikusa, "Asahina Akikusa", ["akikusa-honor"], "none", null),
        };
        foreach (var (id, name, abilities, effect, entryLoss) in expected)
        {
            var card = CardCatalog.Get(id);
            Check(card.Name == name && card.Type == CardType.Personality && !card.CreatedOnly && (card.Abilities ?? []).SequenceEqual(abilities) && card.Effect == effect && card.EntryHonorLoss == entryLoss,
                $"{name}: {card.Name} {string.Join(",", card.Abilities ?? [])} {card.Effect} {card.EntryHonorLoss}");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{name}: art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-55.txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-55.txt")).Count(l => l.Length > 0), 26, "wave 55 pins exactly the 26");
        var chukage = CardCatalog.Get(Chukage);
        Check(chukage.Force == 6 && chukage.Chi == 5 && chukage.GoldCost == 12 && chukage.PersonalHonor == 3 && chukage.HonorRequirement == 6 && chukage.Keywords!.Contains("Loyal") && chukage.Keywords.Contains("Unique"), "Chukage's printed stats");
        var kaiji = CardCatalog.Get(Kaiji);
        Check(kaiji.Force == 9 && kaiji.Chi == 2 && kaiji.GoldCost == 12 && kaiji.PersonalHonor == 0 && kaiji.HonorRequirement is null, "Kaiji's printed stats");
        var swarm = CardCatalog.Get(Swarm);
        Check(swarm.Type == CardType.Personality && swarm.CreatedOnly && swarm.Force == 0 && swarm.Chi == 2 && swarm.PersonalHonor == 3 && swarm.Clan == "Mantis"
            && new HashSet<string>(swarm.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Bee", "Cavalry", "Spirit", "Swarm"]) && swarm.Abilities!.SequenceEqual(["merihikos-swarm-attack"]), "Merihiko's Swarm");
        var blade = CardCatalog.Get(Blade);
        Check(blade.Type == CardType.Item && blade.CreatedOnly && blade.Force == 4 && blade.Chi == 1 && blade.Keywords!.SequenceEqual(["Weapon"]) && (blade.Abilities ?? []).Length == 0, "Masayoshi's Blade");
        foreach (var (id, wave) in new[] { (Swarm, "personality-wave-57"), (Blade, "item-wave-10") })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).SequenceEqual([id]), $"{wave} pins exactly {id}");
        }
        Eq(CreatedCards.Resolve(AbilityCatalog.All["merihiko-create"].Creates!).Id, Swarm, "Merihiko creates his Swarm, found by its stats, keywords and text");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["masayoshi-create"].Creates!).Id, Blade, "Masayoshi creates his Blade");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["makubesu-create"].Creates!).Id, Conscripts, "Makubesu creates Farmlands Conscripts");
        Check(CreatedCards.Verify().Length == 0, "every creator resolves: " + string.Join("; ", CreatedCards.Verify()));
    }

    private static void Chukage_()
    {
        // Limited: bow one of his own unbowed Rings to draw a card.
        var s = Action(); var chukage = Add(s, Chukage, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home); var bowedRing = Add(s, "training-ring-of-water", 0, Zone.Home, bowed: true); var theirs = Add(s, "training-ring-of-void", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "chukage-draw", chukage.Id).TargetIds!;
        Check(targets.Contains(ring.Id) && !targets.Contains(bowedRing.Id) && !targets.Contains(theirs.Id), "his own unbowed Ring only: " + string.Join(",", targets));
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Check(e.State.Cards[ring.Id].Bowed && e.State.Players[0].Hand.Count == hand + 1, "the Ring is bowed and a card drawn");
        // Battle: bowed at home, an enemy at the battlefield: he moves in and his unit straightens; with two enemies against his lone self he may then send one home.
        var b = Battle(); var c = Add(b, Chukage, 0, Zone.Home, bowed: true); var follower = Add(b, "training-follower", 0, Zone.Attached, bowed: true, parent: c.Id);
        var enemy1 = Bf(b, "training-lion-samurai", 1); var enemy2 = Bf(b, "training-lion-veteran", 1);
        var be = MatchEngine.FromTestState(b);
        var offer = Enabled(be, 0, "chukage-move", c.Id);
        var enemySlot = offer.Slots!.Single(x => x.Slot == "enemy");
        Check(enemySlot.CardIds.Contains(enemy1.Id) && enemySlot.CardIds.Contains(enemy2.Id), "an enemy unit at the battlefield may be named");
        Accept(be, offer.Command! with { TargetId = null, Selections = [Pick("enemy", enemy1.Id)] });
        Check(be.State.Cards[c.Id].Zone == Zone.Battlefield && !be.State.Cards[c.Id].Bowed && !be.State.Cards[follower.Id].Bowed, "he reinforced the battle and his unit straightened");
        Check(be.State.Cards[enemy1.Id].Zone == Zone.Home && be.State.Cards[enemy2.Id].Zone == Zone.Battlefield, "the named enemy unit went home");
        // One enemy against his own arrival: the enemy would not outnumber him, so naming one is refused (the move itself still stands).
        var one = Battle(); var c2 = Add(one, Chukage, 0, Zone.Home, bowed: true); var lone = Bf(one, "training-lion-samurai", 1);
        var oe = MatchEngine.FromTestState(one);
        Reject(oe, Enabled(oe, 0, "chukage-move", c2.Id).Command! with { TargetId = null, Selections = [Pick("enemy", lone.Id)] });
        Accept(oe, Enabled(oe, 0, "chukage-move", c2.Id).Command! with { Selections = [] });
        Check(oe.State.Cards[c2.Id].Zone == Zone.Battlefield && oe.State.Cards[lone.Id].Zone == Zone.Battlefield, "he moved; the lone enemy stayed");
        // Not opposed: he does not move (and is not straightened).
        var u = Battle(); var c3 = Add(u, Chukage, 0, Zone.Home, bowed: true); Bf(u, "training-crane-samurai", 0);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Enabled(ue, 0, "chukage-move", c3.Id).Command! with { Selections = [] });
        Check(ue.State.Cards[c3.Id].Zone == Zone.Home && ue.State.Cards[c3.Id].Bowed, "unopposed: no move, no straighten");
    }

    private static void Exchanges()
    {
        var s = Action(); var jukko = Add(s, Jukko, 0, Zone.Home); var target = Add(s, "training-lion-samurai", 1, Zone.Home); var zero = Add(s, "training-crane-samurai", 1, Zone.Home);
        Force(zero, -CardCatalog.Get("training-crane-samurai").Force);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "jukko-switch", jukko.Id);
        var targets = offer.Slots!.Single(x => x.Slot == "target").CardIds; var self = offer.Slots!.Single(x => x.Slot == "self").CardIds;
        Check(targets.Contains(target.Id) && !targets.Contains(zero.Id), "a Personality with 1 Force or greater: " + string.Join(",", targets));
        Check(self.SequenceEqual([jukko.Id]), "the optional second switch names only Jukko");
        var (f, c) = (e.CardForceForTest(target.Id), e.PersonalityChiForTest(target.Id)); var (jf, jc) = (e.CardForceForTest(jukko.Id), e.PersonalityChiForTest(jukko.Id));
        Check(f != c && jf != jc, "precondition: distinct stats");
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("target", target.Id), Pick("self", jukko.Id)] });
        Check(e.CardForceForTest(target.Id) == c && e.PersonalityChiForTest(target.Id) == f, "the target's Force and Chi are switched");
        Check(e.CardForceForTest(jukko.Id) == jc && e.PersonalityChiForTest(jukko.Id) == jf, "and Jukko's too");
        // Tanemono switches himself; a Phoenix Clan player may not recruit him.
        var t = Action(); var tanemono = Add(t, Tanemono, 0, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Enabled(te, 0, "tanemono-switch", tanemono.Id).Command!);
        Check(te.CardForceForTest(tanemono.Id) == 6 && te.PersonalityChiForTest(tanemono.Id) == 1, "1F/6C became 6F/1C");
        foreach (var (clan, allowed) in new[] { ("Phoenix", false), ("Crane", true) })
        {
            var r = MatchEngine.CreateEconomyScenario().State; r.Players[0].Clan = clan;
            for (var i = 0; i < 4; i++) Add(r, "training-marketplace", 0, Zone.Home);
            var cardId = r.Players[0].Provinces[0].CardId!; r.Cards[cardId].DefinitionId = Tanemono; r.Cards[cardId].FaceUp = true;
            var re = MatchEngine.FromTestState(r);
            var quote = re.QuotePurchase(0, cardId);
            Eq(quote.Error is null, allowed, $"{clan}: {quote.Error}");
        }
    }

    private static void ForceEffects()
    {
        // Rikate - exp: Force to 0 and -2C; a Personality with Chi 2 is destroyed.
        var s = Battle(); var rikate = Bf(s, Rikate, 0); var enemy = Bf(s, "training-lion-samurai", 1); var frail = Bf(s, "training-crane-samurai", 1); frail.Modifiers.Add(new("test", "test", "Chi", -1));
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalityChiForTest(frail.Id), 2, "precondition");
        Accept(e, Offer(e, 0, "rikate-exp-weaken", rikate.Id, enemy.Id));
        Check(e.CardForceForTest(enemy.Id) == 0 && e.PersonalityChiForTest(enemy.Id) == CardCatalog.Get("training-lion-samurai").Chi - 2, "Force 0, Chi -2");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "rikate-exp-weaken", rikate.Id, frail.Id));
        Check(e2.State.Cards[frail.Id].Zone == Zone.Dead, "a Chi reduced to 0 destroys him");
        // Okaru: -3F and an additional Battle action.
        var o = Battle(); var okaru = Bf(o, Okaru, 0); var foe = Bf(o, "training-lion-samurai", 1); var follower = Add(o, "training-follower", 1, Zone.Attached, province: 0, parent: foe.Id);
        var oe = MatchEngine.FromTestState(o);
        Check(Enabled(oe, 0, "okaru-weaken", okaru.Id).TargetIds!.Contains(follower.Id), "a Follower is a target too");
        var force = oe.CardForceForTest(foe.Id);
        Accept(oe, Offer(oe, 0, "okaru-weaken", okaru.Id, foe.Id));
        Check(oe.CardForceForTest(foe.Id) == force - 3 && oe.State.AdditionalActions.Any(g => g.PlayerId == 0) && oe.State.PriorityPlayerId == 0, "-3F and the additional Battle action");
        // Aoi: from home, in the Battle phase, bows for +4F on a Personality at the battlefield.
        var a = Battle(); var aoi = Add(a, Aoi, 0, Zone.Home); var ally = Bf(a, "training-crane-samurai", 0); Bf(a, "training-lion-samurai", 1);
        var ae = MatchEngine.FromTestState(a);
        var allyForce = ae.CardForceForTest(ally.Id);
        Accept(ae, Offer(ae, 0, "aoi-boost", aoi.Id, ally.Id));
        Check(ae.State.Cards[aoi.Id].Bowed && ae.CardForceForTest(ally.Id) == allyForce + 4, "Aoi bowed at home; +4F");
        Check(!Offered(ae, 0, "aoi-boost"), "bowed: not again");
        // Koyama - exp: each OTHER Lion in his army gains his own Personal Honor; not himself, not a non-Lion, not one at home.
        var k = Battle(); var koyama = Bf(k, Koyama, 0); var lion = Bf(k, "training-lion-samurai", 0); var veteran = Bf(k, "training-lion-veteran", 0); var crane = Bf(k, "training-crane-samurai", 0); var home = Add(k, "training-lion-samurai", 0, Zone.Home); Bf(k, "training-lion-samurai", 1);
        var ke = MatchEngine.FromTestState(k);
        var before = new[] { koyama, lion, veteran, crane, home }.ToDictionary(x => x.Id, x => ke.CardForceForTest(x.Id));
        Accept(ke, Enabled(ke, 0, "koyama-exp-boost", koyama.Id).Command!);
        Eq(ke.CardForceForTest(lion.Id), before[lion.Id] + CardCatalog.Get("training-lion-samurai").PersonalHonor, "the Lion samurai gains his own Personal Honor");
        Eq(ke.CardForceForTest(veteran.Id), before[veteran.Id] + CardCatalog.Get("training-lion-veteran").PersonalHonor, "the Lion veteran gains his own");
        Check(ke.CardForceForTest(koyama.Id) == before[koyama.Id] && ke.CardForceForTest(crane.Id) == before[crane.Id] && ke.CardForceForTest(home.Id) == before[home.Id], "not Koyama, not the Crane, not the Lion at home");
    }

    private static void EntryCreations()
    {
        // Makubesu: two Conscripts dealt to one or two of his controller's Personalities (himself included).
        CardInstance ally = null!;
        var (m, makubesuId) = Recruit(Makubesu, s => { ally = Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); });
        Eq(m.State.PendingChoice?.Type, "entryReaction", "his entry window is open");
        var offer = Enabled(m, 0, "makubesu-create", makubesuId);
        var hosts = offer.Slots!.Single(x => x.Slot == "hosts");
        Check(hosts.CardIds.Contains(makubesuId) && hosts.CardIds.Contains(ally.Id) && hosts.CardIds.Length == 2 && hosts.Min == 1 && hosts.Max == 2, "one or two of his controller's own Personalities: " + string.Join(",", hosts.CardIds));
        Accept(m, offer.Command! with { TargetId = null, Selections = [Pick("hosts", makubesuId, ally.Id)] });
        var created = m.State.Cards.Values.Where(c => c.DefinitionId == Conscripts).ToArray();
        Check(created.Length == 2 && created.Any(c => c.AttachedTo == makubesuId) && created.Any(c => c.AttachedTo == ally.Id), "two Conscripts, one on each");
        var (m2, makubesu2) = Recruit(Makubesu);
        Accept(m2, Enabled(m2, 0, "makubesu-create", makubesu2).Command! with { TargetId = null, Selections = [Pick("hosts", makubesu2)] });
        Check(m2.State.Cards.Values.Count(c => c.DefinitionId == Conscripts && c.AttachedTo == makubesu2) == 2, "or both on the one chosen Personality");
        // Merihiko: the Swarm, with its own bow-for-Ranged-4.
        var (r, merihikoId) = Recruit(Merihiko);
        Accept(r, Enabled(r, 0, "merihiko-create", merihikoId).Command!);
        var swarm = r.State.Cards.Values.Single(c => c.DefinitionId == Swarm);
        Check(swarm.OwnerId == 0 && swarm.Zone == Zone.Home && !swarm.Bowed, "the Swarm is created at home");
        var b = Battle(); var bee = Bf(b, Swarm, 0); var prey = Bf(b, "training-lion-samurai", 1);
        var be = MatchEngine.FromTestState(b);
        Eq(be.AttackStrengthForTest("merihikos-swarm-attack", bee.Id), 4, "Ranged 4");
        Accept(be, Offer(be, 0, "merihikos-swarm-attack", bee.Id, prey.Id));
        Check(be.State.Cards[bee.Id].Bowed, "it bows as its cost");
        Check(be.State.Cards[prey.Id].Zone == Zone.Dead || CardCatalog.Get("training-lion-samurai").Force > 4, "Ranged 4 destroyed a Force of at most 4");
        // Masayoshi: the Blade attached to himself.
        var (y, masayoshiId) = Recruit(Masayoshi);
        Accept(y, Enabled(y, 0, "masayoshi-create", masayoshiId).Command!);
        var blade = y.State.Cards.Values.Single(c => c.DefinitionId == Blade);
        Check(blade.AttachedTo == masayoshiId && y.CardForceForTest(masayoshiId) == CardCatalog.Get(Masayoshi).Force + 4 && y.PersonalityChiForTest(masayoshiId) == CardCatalog.Get(Masayoshi).Chi + 1, "+4F/+1C on Masayoshi");
        // Seki: 3 Honor lost on entry (mandatory), then his Reaction's -1C Plague token; a Personality with Chi 1 is destroyed by it.
        CardInstance frail = null!, sturdy = null!;
        var (k, sekiId) = Recruit(Seki, s => { frail = Add(s, "training-crane-samurai", 1, Zone.Home); frail.Modifiers.Add(new("test", "test", "Chi", -2)); sturdy = Add(s, "training-lion-samurai", 1, Zone.Home); });
        Eq(Honor(k, 0), 17, "20 - 3 Honor entering play");
        Eq(k.State.PendingChoice?.Type, "entryReaction");
        var targets = Enabled(k, 0, "seki-plague", sekiId).TargetIds!;
        Check(targets.Contains(sturdy.Id) && targets.Contains(sekiId), "any Personality, his own side included");
        var chi = k.PersonalityChiForTest(sturdy.Id);
        Accept(k, Offer(k, 0, "seki-plague", sekiId, sturdy.Id));
        Check(k.State.Cards[sturdy.Id].Tokens.Any(t => t.Type == "Plague" && t.Stat == "Chi" && t.Amount == -1) && k.PersonalityChiForTest(sturdy.Id) == chi - 1, "a -1C Plague token");
        var (k2, seki2) = Recruit(Seki, s => { frail = Add(s, "training-crane-samurai", 1, Zone.Home); frail.Modifiers.Add(new("test", "test", "Chi", -2)); });
        Accept(k2, Offer(k2, 0, "seki-plague", seki2, frail.Id));
        Eq(k2.State.Cards[frail.Id].Zone, Zone.Dead, "Chi 1 to 0: destroyed");
    }

    private static void RangedAndNitani()
    {
        var s = Battle(); var kitahime = Bf(s, Kitahime, 0); var t = Bf(s, "training-lion-samurai", 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("kitahime-attack", kitahime.Id), 4, "Ranged 4");
        Accept(e, Offer(e, 0, "kitahime-attack", kitahime.Id, t.Id));
        Check(e.State.Cards[kitahime.Id].Bowed, "no Reconnaissance: bowed");
        var r = Battle(); var k2 = Bf(r, Kitahime, 0); Bf(r, "training-lion-samurai", 1); r.Players[1].Provinces[0].Reconnaissance[0] = true;
        var re = MatchEngine.FromTestState(r);
        Accept(re, Enabled(re, 0, "kitahime-attack", k2.Id).Command! with { TargetId = null });
        Check(!re.State.Cards[k2.Id].Bowed, "with Reconnaissance she stays unbowed");
        var c = Battle(); var kohrogi = Bf(c, Kohrogi, 0); var nitani = Bf(c, Nitani, 0);
        var ce = MatchEngine.FromTestState(c);
        Eq(ce.AttackStrengthForTest("kohrogi-attack", kohrogi.Id), 2, "Kohrogi: Ranged 2");
        Eq(ce.AttackStrengthForTest("nitani-exp-attack", nitani.Id), 6, "Nitani: Ranged 6");
        // Nitani's destroy: the printed Shadowlands keyword, on a Personality (his unit dies) or an attachment; a granted keyword does not count.
        var n = Battle(); var n1 = Bf(n, Nitani, 0);
        var oni = Bf(n, "training-lion-samurai", 1); GiveKeyword(oni, "Shadowlands");
        var printed = Bf(n, Kaiji, 1); // Crab, no Shadowlands
        var ne = MatchEngine.FromTestState(n);
        Check(!Offered(ne, 0, "nitani-exp-destroy"), "no enemy card with the printed keyword: not offered (" + string.Join(";", Offers(ne, 0, "nitani-exp-destroy").Select(o => o.Reason)) + ")");
        var p = Battle(); var n2 = Bf(p, Nitani, 0); var yurishi = Bf(p, Yurishi, 1); var plain = Bf(p, "training-lion-samurai", 1);
        var pe = MatchEngine.FromTestState(p);
        var targets = Enabled(pe, 0, "nitani-exp-destroy", n2.Id).TargetIds!;
        Check(targets.SequenceEqual([yurishi.Id]), "Yurishi prints Shadowlands; the samurai does not: " + string.Join(",", targets));
        Accept(pe, Offer(pe, 0, "nitani-exp-destroy", n2.Id, yurishi.Id));
        Eq(pe.State.Cards[yurishi.Id].Zone, Zone.Dead, "destroyed");
        Check(printed.Id != oni.Id && plain.Id != n1.Id);
    }

    private static void Kaiji_()
    {
        // Another player's action moves him: the first time each turn is negated, the second is not; his own controller's action moves him freely.
        var s = Battle(1); s.PriorityPlayerId = 1; var kaiji = Bf(s, Kaiji, 0);
        var mover1 = Bf(s, "training-move-source", 1); var mover2 = Bf(s, "training-move-source", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-move-both-home", mover1.Id, kaiji.Id));
        Check(e.State.Cards[kaiji.Id].Zone == Zone.Battlefield && e.State.Cards[mover1.Id].Zone == Zone.Home, "Kaiji's movement was negated; the mover still went home");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "moveNegated" && ev.CardId == kaiji.Id), "announced");
        Accept(e, new GameCommand("pass", 0, e.Version));
        Accept(e, Offer(e, 1, "training-move-both-home", mover2.Id, kaiji.Id));
        Eq(e.State.Cards[kaiji.Id].Zone, Zone.Home, "the second time this turn he moves");
        // Battle action: an enemy card with lower Force is bowed, and destroyed when it has no attachments.
        var b = Battle(); var kj = Bf(b, Kaiji, 0); var weak = Bf(b, "training-lion-samurai", 1); var armed = Bf(b, "training-crane-samurai", 1); var follower = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var strong = Bf(b, "training-lion-veteran", 1); Force(strong, 10);
        var be = MatchEngine.FromTestState(b);
        var targets = Enabled(be, 0, "kaiji-bow", kj.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(armed.Id) && targets.Contains(follower.Id) && !targets.Contains(strong.Id), "lower Force only: " + string.Join(",", targets));
        Accept(be, Offer(be, 0, "kaiji-bow", kj.Id, armed.Id));
        Check(be.State.Cards[armed.Id].Bowed && be.State.Cards[armed.Id].Zone == Zone.Battlefield, "with an attachment: bowed, not destroyed");
        var be2 = MatchEngine.FromTestState(b);
        Accept(be2, Offer(be2, 0, "kaiji-bow", kj.Id, weak.Id));
        Eq(be2.State.Cards[weak.Id].Zone, Zone.Dead, "without attachments: destroyed");
        var be3 = MatchEngine.FromTestState(b);
        Accept(be3, Offer(be3, 0, "kaiji-bow", kj.Id, follower.Id));
        Check(be3.State.Cards[follower.Id].Zone == Zone.FateDiscard && be3.State.Cards[armed.Id].Zone == Zone.Battlefield, "a Follower is destroyed alone");
    }

    private static void Mitsuru_()
    {
        var s = Battle(); var mitsuru = Bf(s, Mitsuru, 0); mitsuru.Modifiers.Add(new("test", "test", "Chi", 10)); var loser = Bf(s, "training-lion-samurai", 1); var mine = Bf(s, "training-crane-samurai", 0);
        var e = MatchEngine.FromTestState(s);
        Check(!Enabled(e, 0, "mitsuru-duel", mitsuru.Id).TargetIds!.Contains(mine.Id), "an enemy Personality only");
        Accept(e, Offer(e, 0, "mitsuru-duel", mitsuru.Id, loser.Id));
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 8; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", duel.TurnPlayerId, e.Version));
        Check(e.State.Cards[loser.Id].Bowed && !e.State.Cards[mitsuru.Id].Bowed, "the loser is bowed, the winner is not");
    }

    private static void Hajime_()
    {
        var s = Battle(); s.UnresolvedBattlefields = [0, 1]; var hajime = Bf(s, Hajime, 0); var enemy = Bf(s, "training-lion-samurai", 1); var away = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        Check(!Enabled(e, 0, "hajime-move-home", hajime.Id).TargetIds!.Contains(away.Id), "an enemy Personality at the current battlefield only");
        Accept(e, Offer(e, 0, "hajime-move-home", hajime.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home, "moved home");
        Check(!Offered(e, 0, "hajime-move-battlefield"), "one use for both destinations");
        var e2 = MatchEngine.FromTestState(s);
        var option = Options(e2, 0, "hajime-move-battlefield", hajime.Id).First(o => o.Enabled);
        Check(option.Provinces!.SequenceEqual([1]), "the other unresolved battlefield is the only destination: " + string.Join(",", option.Provinces!));
        Accept(e2, option.Command! with { TargetId = enemy.Id, Province = 1 });
        Check(e2.State.Cards[enemy.Id].Zone == Zone.Battlefield && e2.State.Cards[enemy.Id].ProvinceIndex == 1, "moved to the other battlefield");
        Check(AbilityCatalog.All["hajime-move-home"].ActionKeywords!.Contains("Ninja"), "a Ninja action");
        var (r, _) = Recruit(Hajime);
        Eq(Honor(r, 0), 18, "20 - 2 Honor entering play");
    }

    private static void Iaimiko_()
    {
        var s = Battle(); var iaimiko = Bf(s, Iaimiko, 0); Bf(s, "training-shugenja", 0);
        var target = Bf(s, "training-lion-samurai", 1); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id);
        var other = Bf(s, "training-crane-samurai", 1);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "iaimiko-weaken", iaimiko.Id);
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("target", target.Id), Pick("destroy", follower.Id)] });
        Check(e.CardForceForTest(target.Id) == 0 && e.State.Cards[follower.Id].Zone == Zone.FateDiscard, "-4F to 0, and the Follower in his unit destroyed");
        // A card outside the target's unit is refused; without a Shugenja, or with a Force above 4, no second target may be named.
        Reject(e, Use(e, 0, "iaimiko-weaken", iaimiko.Id, selections: [Pick("target", target.Id), Pick("destroy", other.Id)]));
        var n = Battle(); var i2 = Bf(n, Iaimiko, 0); var t2 = Bf(n, "training-lion-samurai", 1);
        var ne = MatchEngine.FromTestState(n);
        Check(Enabled(ne, 0, "iaimiko-weaken", i2.Id).Slots!.Single(x => x.Slot == "destroy").CardIds.Length == 0, "no Shugenja: nothing to destroy");
        Reject(ne, Use(ne, 0, "iaimiko-weaken", i2.Id, selections: [Pick("target", t2.Id), Pick("destroy", t2.Id)]));
        var h = Battle(); var i3 = Bf(h, Iaimiko, 0); Bf(h, "training-shugenja", 0); var t3 = Bf(h, "training-lion-veteran", 1); Force(t3, 2); // Force 8
        var he = MatchEngine.FromTestState(h);
        Check(Enabled(he, 0, "iaimiko-weaken", i3.Id).Slots!.Single(x => x.Slot == "destroy").CardIds.Length == 0, "Force above 4: it would not be 0");
        Accept(he, Enabled(he, 0, "iaimiko-weaken", i3.Id).Command! with { TargetId = null, Selections = [Pick("target", t3.Id)] });
        Eq(he.CardForceForTest(t3.Id), 4, "-4F");
    }

    private static void HawadoExp()
    {
        // Player 1's Battle action resolves; Hawado (player 0) at the battlefield reacts: the performer goes home.
        var s = Battle(1); s.PriorityPlayerId = 1; s.UnresolvedBattlefields = [0, 1];
        var hawado = Bf(s, Hawado, 0); var victim = Bf(s, "training-crane-samurai", 0); var performer = Bf(s, "training-force-seven-bower", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", performer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "battleActionReaction", "the after-Battle-action window opens for Hawado");
        Check(Offered(e, 0, "hawado-exp-move-home") && Offered(e, 0, "hawado-exp-move-battlefield"), "both destinations are offered");
        Accept(e, Enabled(e, 0, "hawado-exp-move-home", hawado.Id).Command!);
        Eq(e.State.Cards[performer.Id].Zone, Zone.Home, "the performer moved home");
        // The other unresolved battlefield.
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", performer.Id, victim.Id));
        var option = Options(e2, 0, "hawado-exp-move-battlefield", hawado.Id).First(o => o.Enabled);
        Check(option.Provinces!.SequenceEqual([1]), "another unresolved battlefield: " + string.Join(",", option.Provinces!));
        Accept(e2, option.Command! with { Province = 1 });
        Check(e2.State.Cards[performer.Id].Zone == Zone.Battlefield && e2.State.Cards[performer.Id].ProvinceIndex == 1, "moved to battlefield 1");
        // Hawado at home: he is not at the current battlefield, so he cannot react.
        var h = Battle(1); h.PriorityPlayerId = 1; var away = Add(h, Hawado, 0, Zone.Home); var v = Bf(h, "training-crane-samurai", 0); var p = Bf(h, "training-force-seven-bower", 1);
        var he = MatchEngine.FromTestState(h);
        Accept(he, Offer(he, 1, "training-bow-at-battle", p.Id, v.Id));
        if (he.State.PendingChoice?.Type == "battleActionReaction") Reject(he, new GameCommand("useAbility", 0, he.Version, CardId: away.Id, AbilityId: "hawado-exp-move-home"));
        Eq(he.State.Cards[p.Id].Zone, Zone.Battlefield, "the performer stayed");
    }

    private static void Hirononri_()
    {
        var s = Action(); var hirononri = Add(s, Hirononri, 0, Zone.Home);
        var stables = Add(s, "training-marketplace", 0, Zone.Home, bowed: true); GiveKeyword(stables, "Stables");
        var cavalry = Add(s, "training-cavalry", 1, Zone.Home, bowed: true); var plain = Add(s, "training-lion-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hirononri-straighten", hirononri.Id).TargetIds!;
        Check(targets.Contains(stables.Id) && targets.Contains(cavalry.Id) && !targets.Contains(plain.Id), "a Stables or Cavalry card, either side's: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "hirononri-straighten", hirononri.Id, cavalry.Id));
        Check(e.State.Cards[hirononri.Id].Bowed && !e.State.Cards[cavalry.Id].Bowed, "Hirononri bowed; the Cavalry Personality straightened");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "hirononri-straighten", hirononri.Id, stables.Id));
        Check(!e2.State.Cards[stables.Id].Bowed, "a Stables Holding straightened");
    }

    private static void Heijin_()
    {
        var s = Battle(); s.UnresolvedBattlefields = [0, 1]; var heijin = Bf(s, Heijin, 0);
        var courtier = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(courtier, "Courtier"); var notCourtier = Add(s, "training-lion-samurai", 0, Zone.Home);
        var enemyHome = Add(s, "training-lion-veteran", 1, Zone.Home, bowed: true); var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, parent: enemyHome.Id);
        var enemyAway = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "heijin-move", heijin.Id);
        var courtiers = offer.Slots!.Single(x => x.Slot == "courtier").CardIds; var enemies = offer.Slots!.Single(x => x.Slot == "enemy").CardIds;
        Check(courtiers.SequenceEqual([courtier.Id]), "his Courtier only: " + string.Join(",", courtiers));
        Check(enemies.Contains(enemyHome.Id) && enemies.Contains(enemyAway.Id) && !enemies.Contains(notCourtier.Id), "an enemy Personality anywhere: " + string.Join(",", enemies));
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("courtier", courtier.Id), Pick("enemy", enemyHome.Id)] });
        Check(e.State.Cards[courtier.Id].Zone == Zone.Battlefield && e.State.Cards[courtier.Id].ProvinceIndex == 0, "the Courtier is at the battlefield");
        Check(e.State.Cards[enemyHome.Id].Zone == Zone.Battlefield && e.State.Cards[enemyHome.Id].ProvinceIndex == 0 && !e.State.Cards[enemyHome.Id].Bowed && !e.State.Cards[enemyFollower.Id].Bowed, "the enemy is there too, his unit straightened");
        Reject(e, Use(e, 0, "heijin-move", heijin.Id, selections: [Pick("courtier", notCourtier.Id), Pick("enemy", enemyAway.Id)]));
    }

    private static void Akukiho_()
    {
        var s = Battle(); s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var akukiho = Bf(s, Akukiho, 0); Bf(s, "training-lion-samurai", 1);
        var weapon = Add(s, "training-weapon", 0, Zone.Hand); var follower = Add(s, "training-follower", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "akukiho-attach", akukiho.Id);
        Check(offer.TargetIds!.SequenceEqual([weapon.Id]), "a Weapon in his hand, not a Follower: " + string.Join(",", offer.TargetIds!));
        Check(offer.Gold is { Cost: 1 }, "priced at the Weapon's own cost");
        Accept(e, Offer(e, 0, "akukiho-attach", akukiho.Id, weapon.Id));
        Check(e.State.Cards[weapon.Id].Zone == Zone.Attached && e.State.Cards[weapon.Id].AttachedTo == akukiho.Id, "attached");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action");
        Check(follower.Id != weapon.Id);
    }

    private static void Iwashi_()
    {
        // Player 0 attacks province 0 with Iwashi against no defender and wins: he is stamped.
        var s = Battle(); var iwashi = Bf(s, Iwashi, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("pass", 0, e.Version)); Accept(e, new GameCommand("pass", 1, e.Version));
        Check(e.GetView(0).Events.Any(ev => ev.Type == "battleResolved"), "the battle resolved");
        var won = e.State;
        Eq(won.Cards[iwashi.Id].Zone, Zone.Home, "he returned home");
        // On his controller's next turn (two turns on) the Limited bow is offered; two more turns on it is not.
        foreach (var (turnsOn, expected) in new[] { (2, true), (4, false) })
        {
            var next = MatchEngine.FromTestState(won).State;
            next.Phase = Phase.Action; next.ActivePlayerId = 0; next.PriorityPlayerId = 0; next.CurrentBattleProvince = null; next.UnresolvedBattlefields = []; next.PendingChoice = null; next.TurnNumber += turnsOn;
            next.Cards[iwashi.Id].Bowed = false;
            var target = Add(next, "training-lion-samurai", 1, Zone.Home);
            var ne = MatchEngine.FromTestState(next);
            Eq(Offered(ne, 0, "iwashi-bow"), expected, $"{turnsOn} turns on");
            if (expected) { Accept(ne, Offer(ne, 0, "iwashi-bow", iwashi.Id, target.Id)); Check(ne.State.Cards[target.Id].Bowed, "bowed"); }
        }
        // Never in a winning attacking army: not offered.
        var a = Action(); var fresh = Add(a, Iwashi, 0, Zone.Home); Add(a, "training-lion-samurai", 1, Zone.Home);
        Check(!Offered(MatchEngine.FromTestState(a), 0, "iwashi-bow"), "no victory: refused");
    }

    private static void Yurishi_()
    {
        // Player 1's Battle action targets an enemy Personality: with another target available Yurishi is not offered and is refused; alone, he is.
        var s = Battle(1); s.PriorityPlayerId = 1; var yurishi = Bf(s, Yurishi, 0); var other = Bf(s, "training-crane-samurai", 0); var bower = Bf(s, "training-force-seven-bower", 1);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 1, "training-bow-at-battle", bower.Id).TargetIds!;
        Check(targets.SequenceEqual([other.Id]), "not Yurishi while another legal target exists: " + string.Join(",", targets));
        Reject(e, Use(e, 1, "training-bow-at-battle", bower.Id, yurishi.Id));
        var alone = Battle(1); alone.PriorityPlayerId = 1; var y2 = Bf(alone, Yurishi, 0); var b2 = Bf(alone, "training-force-seven-bower", 1);
        var ae = MatchEngine.FromTestState(alone);
        Check(Enabled(ae, 1, "training-bow-at-battle", b2.Id).TargetIds!.SequenceEqual([y2.Id]), "the only legal target: he may be targeted");
        Accept(ae, Offer(ae, 1, "training-bow-at-battle", b2.Id, y2.Id));
        Check(ae.State.Cards[y2.Id].Bowed, "bowed");
        // An Attack too: with another legal Attack target he is refused.
        var r = Battle(1); r.PriorityPlayerId = 1; var y3 = Bf(r, Yurishi, 0); var o3 = Bf(r, "training-crane-samurai", 0); var archer = Bf(r, Nitani, 1);
        var re = MatchEngine.FromTestState(r);
        var attackTargets = Enabled(re, 1, "nitani-exp-attack", archer.Id).TargetIds ?? [];
        Check(!attackTargets.Contains(y3.Id) && attackTargets.Contains(o3.Id), "a Ranged Attack may not name him either: " + string.Join(",", attackTargets));
        // His own controller targets him freely.
        var own = Action(); var y4 = Add(own, Yurishi, 0, Zone.Home); Add(own, "training-crane-samurai", 0, Zone.Home); var donor = Add(own, "training-skin-donor", 0, Zone.Home);
        var oe = MatchEngine.FromTestState(own);
        Check(Enabled(oe, 0, "training-bow-any", donor.Id).TargetIds!.Contains(y4.Id), "his controller's own action may target him");
        // A Spider player recruits him (he is Loyal); the economy scenario's own Stronghold negates a Spider card's Honor loss, so a published one stands in.
        var (recruited, _) = Recruit(Yurishi, s => { s.Players[0].Clan = "Spider"; s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; });
        // No Reaction of his own: the entry opens the Proclaim window, and the mandatory loss lands when it closes.
        for (var guard = 0; guard < 4 && recruited.State.PendingChoice is { Type: "proclaim" }; guard++) Decline(recruited);
        Eq(Honor(recruited, 0), 17, "20 - 3 Honor entering play");
    }

    private static void InoStorm()
    {
        // Ino (player 0) summons the storm and keeps priority (the additional Battle action); she passes; player 1's non-Thunder performer acts: player 1 must choose.
        var s = Battle(); var ino = Bf(s, Ino, 0); var mine = Bf(s, "training-crane-samurai", 0); var performer = Bf(s, "training-force-seven-bower", 1);
        var card1 = Add(s, "training-weapon", 1, Zone.Hand); var card2 = Add(s, "training-weapon", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "ino-storm", ino.Id).Command!);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0) && e.State.PriorityPlayerId == 0, "an additional Battle action");
        Accept(e, new GameCommand("pass", 0, e.Version));
        Accept(e, Offer(e, 1, "training-bow-at-battle", performer.Id, mine.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the storm breaks: the player who took the action chooses");
        var answers = e.QueryActions(1).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(answers.Length == 2 && answers.All(a => a.Enabled), "discard a card, or bow the performer: " + string.Join(";", answers.Select(a => a.Label)));
        Check(e.QueryActions(0).All(a => a.Type != "chooseEitherOr"), "Ino's controller has no say");
        Accept(e, answers.Single(a => a.Command!.Choice == "bow").Command!);
        Check(e.State.Cards[performer.Id].Bowed && e.State.PendingChoice is null, "the performer is bowed and play goes on");
        Check(!e.State.Cards[ino.Id].AbilityUses.ContainsKey("trait:ino-storm-battle"), "the storm is spent");
        // Discarding instead: a forced discard of player 1's choosing.
        var d = MatchEngine.FromTestState(s);
        Accept(d, Enabled(d, 0, "ino-storm", ino.Id).Command!); Accept(d, new GameCommand("pass", 0, d.Version));
        Accept(d, Offer(d, 1, "training-bow-at-battle", performer.Id, mine.Id));
        Accept(d, d.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "discard").Command!);
        Eq(d.State.PendingChoice?.Type, "forcedDiscard", "he now discards");
        Accept(d, new GameCommand("forcedDiscard", 1, d.Version, TargetId: card1.Id));
        Check(d.State.Cards[card1.Id].Zone == Zone.FateDiscard && d.State.Players[1].Hand.Contains(card2.Id) && !d.State.Cards[performer.Id].Bowed, "discarded; the performer stays unbowed");
        // A Thunder Personality's action passes untouched and the storm stays armed for the next one.
        var t = Battle(); var ino2 = Bf(t, Ino, 0); var m2 = Bf(t, "training-crane-samurai", 0); var thunder = Bf(t, "training-thunder-shugenja", 1); var later = Bf(t, "training-force-seven-bower", 1); Add(t, "training-weapon", 1, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Enabled(te, 0, "ino-storm", ino2.Id).Command!); Accept(te, new GameCommand("pass", 0, te.Version));
        Accept(te, Enabled(te, 1, "training-weapon-attack", thunder.Id).Command! with { TargetId = null });
        for (var guard = 0; guard < 6 && te.State.PendingChoice is { Type: "attackReaction" }; guard++) Decline(te);
        Check(te.State.PendingChoice is null, "a Thunder performer: no choice asked (" + te.State.PendingChoice?.Type + ")");
        Accept(te, new GameCommand("pass", 0, te.Version));
        Accept(te, Offer(te, 1, "training-bow-at-battle", later.Id, m2.Id));
        Eq(te.State.PendingChoice?.Type, "eitherOr", "the storm was still armed for the next non-Thunder action");
    }

    private static void Akikusa_()
    {
        var s = Battle(); var akikusa = Bf(s, Akikusa, 0); var shugenja = Bf(s, "training-shugenja", 0); Bf(s, "training-lion-samurai", 1);
        var spell = Add(s, "training-spell-with-ability", 0, Zone.Attached, province: 0, parent: shugenja.Id); GiveKeyword(spell, "Air");
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "training-spell-force-boost", spell.Id, shugenja.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for her");
        Accept(e, Enabled(e, 0, "akikusa-honor", akikusa.Id).Command!);
        Eq(Honor(e, 0), honor + 1, "+1 Honor");
        // A Spell without Air: no window for her.
        var n = Battle(); Bf(n, Akikusa, 0); var sh = Bf(n, "training-shugenja", 0); Bf(n, "training-lion-samurai", 1);
        var plain = Add(n, "training-spell-with-ability", 0, Zone.Attached, province: 0, parent: sh.Id);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 0, "training-spell-force-boost", plain.Id, sh.Id));
        Check(ne.State.PendingChoice?.Type != "afterActionResolvedReaction", "not an Air Spell: nothing to react to");
    }
}
