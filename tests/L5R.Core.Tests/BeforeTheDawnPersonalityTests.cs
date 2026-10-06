using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #431: the Before the Dawn Personalities wired on primitives the engine already has (personality-wave-43). One suite; each test names the cards it covers.
internal static class BeforeTheDawnPersonalityTests
{
    private const string Heiwa = "oracle-l5r-537", Izuna = "oracle-l5r-546", Ebara = "oracle-l5r-763", Shibata = "oracle-l5r-879", Suwabe = "oracle-l5r-901", Kiuchi = "oracle-l5r-1356",
        Masafuni = "oracle-l5r-1653", Shigeyuki = "oracle-l5r-2152", Akio = "oracle-l5r-3287", Kaname = "oracle-l5r-3844", Kota = "oracle-l5r-3994", Yupadi = "oracle-l5r-4010",
        Iemasa = "oracle-l5r-4119", Kunji = "oracle-l5r-4604", Umiko = "oracle-l5r-5287", Hailung = "oracle-l5r-5322", Nijugun = "oracle-l5r-5560", Omigawa = "oracle-l5r-5695",
        Ryuba = "oracle-l5r-6799", Tsuji = "oracle-l5r-7179", Komiko = "oracle-l5r-7355", Taishuu = "oracle-l5r-7733", Yashiro = "oracle-l5r-8896", EunJu = "oracle-l5r-9061",
        WrathfulDead = "oracle-l5r-9391", Tarao = "oracle-l5r-9642", Zinan = "oracle-l5r-9659";

    public static void Run(TestRunner runner) => runner.Run("btd-personalities", [
        ("Published shapes: the twenty-seven Personalities are the printed cards with their abilities, traits and art", PublishedShapes),
        ("Force bonuses: Heiwa (bonus or penalty equal to his Chi, once), Suwabe (his performing Courtier's Chi), Kunji (+2F)", ForceActions),
        ("Bows and moves: Tsuji, Komiko (Political, honor loss for a dishonorable target), Eun-ju, Iemasa, Zinan, Wrathful Dead", BowAndDestroyActions),
        ("Moving home: Ryuba (straightens if he moved), Masafuni (bows unless Reconnaissance), Nijugun (negates further movement), Omigawa (bows if she has a Weapon)", MoveActions),
        ("Kaname and Umiko: Ranged 1 any number of times, +2 (or -2) to a Ranged Attack; Yashiro's Ranged 4; Akio's Ranged 5 and his Followers", RangedActions),
        ("Static traits: Shibata's provinces, Kota's and Umiko's +2F with a Spell, Taishuu's hand size, Yashiro's Clan discount", StaticTraits),
        ("Reactions to a targeting action: Kota negates the movement, Taishuu negates a Political action", TargetedReactions),
        ("Attaching: Ebara (a Weapon from the hand, draws) and Hailung (a Follower from the hand, discount while defending or Crab)", AttachActions),
        ("Izuna (dishonor and a permanent keyword), Yupadi (Naval; bow her or a Spell), Iemasa's Limited", EntryAndGrants),
        ("The Red Hunger: destroys one or two attachments with a total Force lower than his, or a Personality without attachments with lower Force", RedHunger),
        ("Tarao: the chosen player bows a card of his or discards one from his hand; Kiuchi: equalizes two Forces; Shigeyuki: a refusable Personal Honor duel", DecidersAndDuels),
        ("Boundaries: equal values are refused or allowed exactly as each card's text says, and every static trait and reaction stays inside its own scope", Boundaries),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle() { var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; return s; }

    private static void PublishedShapes()
    {
        var expected = new (string Id, string Name, string[] Abilities, string Effect)[]
        {
            (Heiwa, "Asako Heiwa", ["heiwa-bonus", "heiwa-penalty"], "none"), (Izuna, "Asako Izuna", ["izuna-kolat", "izuna-ninja", "izuna-shadowlands"], "none"), (Ebara, "Bayushi Ebara", ["ebara-equip"], "none"),
            (Shibata, "Bayushi Shibata", [], "shibata-provinces"), (Suwabe, "Bayushi Suwabe", ["suwabe-force"], "none"), (Kiuchi, "Chuda Kiuchi", ["kiuchi-equalize"], "none"),
            (Masafuni, "Daidoji Masafuni", ["masafuni-move"], "none"), (Shigeyuki, "Doji Shigeyuki", ["shigeyuki-duel"], "none"), (Akio, "Hiruma Akio", ["akio-attack"], "akio-follower-force"),
            (Kaname, "Isawa Kaname", ["kaname-attack-boost", "kaname-ranged-attack"], "none"), (Kota, "Iuchi Kota", ["kota-negate-move"], "force-with-spell-2"), (Yupadi, "Iuchi Yupadi", ["yupadi-naval"], "none"),
            (Iemasa, "Kaiu Iemasa", ["iemasa-rebuild", "iemasa-bow"], "none"), (Kunji, "Kunji", ["kunji-boost"], "none"), (Umiko, "Moshi Umiko", ["umiko-attack-boost", "umiko-attack-reduce"], "force-with-spell-2"),
            (Hailung, "Moto Hailung", ["hailung-attach", "hailung-attach-discount"], "none"), (Nijugun, "Nijugun", ["nijugun-move"], "none"), (Omigawa, "Omigawa", ["omigawa-move"], "none"),
            (Ryuba, "Shiba Ryuba", ["ryuba-move"], "none"), (Tsuji, "Shosuro Tsuji", ["tsuji-bow"], "none"), (Komiko, "Soshi Komiko", ["komiko-bow"], "none"),
            (Taishuu, "Taishuu", ["taishuu-political-negate"], "decrease-other-hand-size"), (Yashiro, "Tsuruchi Yashiro", ["yashiro-attack"], "lion-or-scorpion-clan-discount-2"),
            (EunJu, "Utaku Eun-ju", ["eun-ju-bow"], "none"), (WrathfulDead, "Wrathful Dead", ["wrathful-dead-destroy"], "none"), (Tarao, "Yoritomo Tarao", ["tarao-sacrifice"], "none"), (Zinan, "Yoritomo Zinan", ["zinan-return"], "none"),
        };
        var root = RepoRoot();
        foreach (var (id, name, abilities, effect) in expected)
        {
            var card = CardCatalog.Get(id);
            Check(card.Name == name && card.Type == CardType.Personality && (card.Abilities ?? []).SequenceEqual(abilities) && card.Effect == effect, $"{name}: {card.Name} {string.Join(",", card.Abilities ?? [])} {card.Effect}");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{name}: art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(CardCatalog.Get(Kiuchi).EntryHonorLoss, 1, "Kiuchi loses 1 Honor entering play");
    }

    private static void ForceActions()
    {
        var s = Battle();
        var heiwa = Bf(s, Heiwa, 0); var ally = Bf(s, "training-crane-samurai", 0); var enemy = Bf(s, "training-lion-samurai", 1);
        var courtierHome = Add(s, "training-shugenja", 0, Zone.Home); GiveKeyword(courtierHome, "Courtier");
        var suwabe = Bf(s, Suwabe, 0); var kunji = Bf(s, Kunji, 0);
        var e = MatchEngine.FromTestState(s);
        var chi = Def(heiwa).Chi; var allyForce = e.CardForceForTest(ally.Id);
        var options = Options(e, 0, "heiwa-bonus", heiwa.Id).First(o => o.Enabled);
        Check(options.TargetIds!.Contains(ally.Id) && options.TargetIds.Contains(enemy.Id) && !options.TargetIds.Contains(heiwa.Id), "another Personality or a Follower, either side, never Heiwa himself");
        Accept(e, Offer(e, 0, "heiwa-bonus", heiwa.Id, ally.Id));
        Eq(e.CardForceForTest(ally.Id), allyForce + chi, "a Force bonus equal to Heiwa's Chi");
        Check(!CanUse(e, 0, "heiwa-penalty", heiwa.Id), "the bonus and the penalty share one use");
        var e2 = Edit(MatchEngine.FromTestState(s), _ => { });
        Accept(e2, Offer(e2, 0, "heiwa-penalty", heiwa.Id, enemy.Id));
        Eq(e2.CardForceForTest(enemy.Id), MatchEngine.FromTestState(s).CardForceForTest(enemy.Id) - chi, "or a penalty");
        // Suwabe: his performing Courtier's Chi, the Courtier anywhere.
        var e3 = MatchEngine.FromTestState(s); var suwabeForce = e3.CardForceForTest(suwabe.Id);
        Accept(e3, Enabled(e3, 0, "suwabe-force", suwabe.Id, courtierHome.Id).Command!);
        Eq(e3.CardForceForTest(suwabe.Id), suwabeForce + Def(courtierHome).Chi, "Suwabe gets the Courtier's Chi as a Force bonus");
        Check(!Options(e3, 0, "suwabe-force", suwabe.Id, ally.Id).Any(o => o.Enabled), "only a Courtier performs it");
        // Kunji: +2F to a Personality.
        var e4 = MatchEngine.FromTestState(s); var enemyForce = e4.CardForceForTest(enemy.Id);
        Accept(e4, Offer(e4, 0, "kunji-boost", kunji.Id, enemy.Id));
        Eq(e4.CardForceForTest(enemy.Id), enemyForce + 2, "+2F");
        Check(kunji.Id != suwabe.Id);
    }

    private static void BowAndDestroyActions()
    {
        // Tsuji: bow an enemy card.
        var s = Battle(); var tsuji = Bf(s, Tsuji, 0); var enemy = Bf(s, "training-lion-samurai", 1); var mine = Bf(s, "training-crane-samurai", 0);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "tsuji-bow", tsuji.Id).First(o => o.Enabled).TargetIds!.Contains(mine.Id), "an enemy card only");
        Accept(e, Offer(e, 0, "tsuji-bow", tsuji.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "bowed");
        // Komiko: Personal Honor lower than or equal to her Chi; dishonorable: his controller loses 2 Honor.
        var k = Battle(); var komiko = Bf(k, Komiko, 0); var low = Bf(k, "training-lion-samurai", 1); var high = Bf(k, "training-crane-veteran", 1);
        GiveKeyword(low, "Marker"); low.Dishonorable = true;
        var ke = MatchEngine.FromTestState(k); var chi = Def(komiko).Chi;
        var komikoTargets = Options(ke, 0, "komiko-bow", komiko.Id).First(o => o.Enabled).TargetIds!;
        Check(komikoTargets.Contains(low.Id) == (Definition(ke, low) <= chi) && komikoTargets.Contains(high.Id) == (Definition(ke, high) <= chi), "Personal Honor lower than or equal to her Chi: " + string.Join(",", komikoTargets));
        if (komikoTargets.Contains(low.Id))
        {
            var honor = Honor(ke, 1);
            Accept(ke, Offer(ke, 0, "komiko-bow", komiko.Id, low.Id));
            Check(ke.State.Cards[low.Id].Bowed && Honor(ke, 1) == honor - 2, "bowed, and dishonorable: his controller loses 2 Honor");
        }
        // Eun-ju: an enemy Personality with lower Personal Honor; gain 2 Honor.
        var u = Battle(); var eunju = Bf(u, EunJu, 0); var lower = Bf(u, "training-lion-samurai", 1);
        var ue = MatchEngine.FromTestState(u); var myHonor = Honor(ue, 0);
        var eunjuTargets = Options(ue, 0, "eun-ju-bow", eunju.Id).FirstOrDefault(o => o.Enabled)?.TargetIds ?? [];
        Check(eunjuTargets.Contains(lower.Id) == (Definition(ue, lower) < Definition(ue, eunju)), "lower Personal Honor only");
        if (eunjuTargets.Contains(lower.Id))
        {
            Accept(ue, Offer(ue, 0, "eun-ju-bow", eunju.Id, lower.Id));
            Check(ue.State.Cards[lower.Id].Bowed && Honor(ue, 0) == myHonor + 2, "bowed; 2 Honor");
        }
        // Iemasa: Force lower than or equal to the province's strength.
        var m = Battle(); var iemasa = Bf(m, Iemasa, 0); var weak = Bf(m, "training-lion-samurai", 1); var strong = Bf(m, "training-lion-veteran", 1); Force(strong, 40);
        var me = MatchEngine.FromTestState(m);
        var iemasaTargets = Options(me, 0, "iemasa-bow", iemasa.Id).First(o => o.Enabled).TargetIds!;
        Check(iemasaTargets.Contains(weak.Id) && !iemasaTargets.Contains(strong.Id), "an enemy card with Force at most the province's strength");
        Accept(me, Offer(me, 0, "iemasa-bow", iemasa.Id, weak.Id));
        Check(me.State.Cards[weak.Id].Bowed, "bowed");
        // Zinan: an attachment to its owner's hand.
        var z = Battle(); var zinan = Bf(z, Zinan, 0); var host = Bf(z, "training-lion-samurai", 1); var weapon = Add(z, "training-weapon", 1, Zone.Attached, province: 0, parent: host.Id);
        var ze = MatchEngine.FromTestState(z);
        Check(Options(ze, 0, "zinan-return", zinan.Id).First(o => o.Enabled).TargetIds!.SequenceEqual(new[] { weapon.Id }), "attachments only");
        Accept(ze, Offer(ze, 0, "zinan-return", zinan.Id, weapon.Id));
        Check(ze.State.Cards[weapon.Id].Zone == Zone.Hand && ze.State.Players[1].Hand.Contains(weapon.Id), "back in its owner's hand");
        // Wrathful Dead: a card without attachments in a unit led by a dishonorable Personality.
        var w = Battle(); var dead = Bf(w, WrathfulDead, 0); var leader = Bf(w, "training-lion-samurai", 1); leader.Dishonorable = true;
        var follower = Add(w, "training-follower", 1, Zone.Attached, province: 0, parent: leader.Id); var honorable = Bf(w, "training-lion-veteran", 1);
        var we = MatchEngine.FromTestState(w);
        var deadTargets = Options(we, 0, "wrathful-dead-destroy", dead.Id).First(o => o.Enabled).TargetIds!;
        Check(deadTargets.Contains(follower.Id) && !deadTargets.Contains(leader.Id) && !deadTargets.Contains(honorable.Id), "the Follower (no attachments) of the dishonorable leader; the leader has one, the other unit is honorable: " + string.Join(",", deadTargets));
        Accept(we, Offer(we, 0, "wrathful-dead-destroy", dead.Id, follower.Id));
        Check(we.State.Cards[follower.Id].Zone != Zone.Attached, "destroyed");
    }

    private static int Definition(MatchEngine e, CardInstance card) => CardCatalog.Get(card.DefinitionId).PersonalHonor;

    private static void MoveActions()
    {
        // Ryuba: move home; straighten if he moved.
        var s = Battle(); var ryuba = Bf(s, Ryuba, 0); var ally = Bf(s, "training-crane-samurai", 0, bowed: true); var enemy = Bf(s, "training-lion-samurai", 1);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "ryuba-move", ryuba.Id).First(o => o.Enabled).TargetIds!.SequenceEqual(new[] { ryuba.Id, ally.Id }.OrderBy(x => x, StringComparer.Ordinal)) || !Options(e, 0, "ryuba-move", ryuba.Id).First(o => o.Enabled).TargetIds!.Contains(enemy.Id), "your own Personalities");
        Accept(e, Offer(e, 0, "ryuba-move", ryuba.Id, ally.Id));
        Check(e.State.Cards[ally.Id].Zone == Zone.Home && !e.State.Cards[ally.Id].Bowed, "he went home and, having moved, straightened");
        // Masafuni: bow him unless Reconnaissance.
        var m = Battle(); var masafuni = Bf(m, Masafuni, 0); var foe = Bf(m, "training-lion-samurai", 1);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Offer(me, 0, "masafuni-move", masafuni.Id, foe.Id));
        Check(me.State.Cards[foe.Id].Zone == Zone.Home && me.State.Cards[masafuni.Id].Bowed, "no Reconnaissance: Masafuni bowed, the Personality is home");
        var m2 = Battle(); var masafuni2 = Bf(m2, Masafuni, 0); var foe2 = Bf(m2, "training-lion-samurai", 1); m2.Players[1].Provinces[0].Reconnaissance[0] = true;
        var me2 = MatchEngine.FromTestState(m2);
        Accept(me2, Offer(me2, 0, "masafuni-move", masafuni2.Id, foe2.Id));
        Check(me2.State.Cards[foe2.Id].Zone == Zone.Home && !me2.State.Cards[masafuni2.Id].Bowed, "with Reconnaissance he does not bow");
        // Nijugun: bows a performing Follower, moves an enemy home; with Reconnaissance or as a Unicorn player negates his further movement.
        foreach (var (recon, unicorn, expectLock) in new[] { (false, false, false), (true, false, true), (false, true, true) })
        {
            var n = Battle(); var nijugun = Bf(n, Nijugun, 0); var follower = Add(n, "training-follower", 0, Zone.Attached, province: 0, parent: nijugun.Id); var enemy2 = Bf(n, "training-lion-samurai", 1);
            n.Players[1].Provinces[0].Reconnaissance[0] = recon; n.Players[0].Clan = unicorn ? "Unicorn" : "Crane";
            var ne = MatchEngine.FromTestState(n);
            Accept(ne, Offer(ne, 0, "nijugun-move", nijugun.Id, enemy2.Id, follower.Id));
            Check(ne.State.Cards[follower.Id].Bowed && ne.State.Cards[enemy2.Id].Zone == Zone.Home, "the Follower bowed and the enemy went home");
            Eq(ne.State.Cards[enemy2.Id].MovementLocks.Count > 0, expectLock, $"Reconnaissance {recon}, Unicorn {unicorn}: his further movement is negated");
        }
        // Nijugun moves a Personality at the battlefield: one already home is not a target.
        var nh = Battle(); var nij = Bf(nh, Nijugun, 0); var nhFollower = Add(nh, "training-follower", 0, Zone.Attached, province: 0, parent: nij.Id); var nhHome = Add(nh, "training-lion-samurai", 1, Zone.Home); Bf(nh, "training-lion-veteran", 1);
        nh.Players[1].Provinces[0].Reconnaissance[0] = true;
        var nhe = MatchEngine.FromTestState(nh);
        Check(!(Options(nhe, 0, "nijugun-move", nij.Id).FirstOrDefault(o => o.Enabled)?.TargetIds ?? []).Contains(nhHome.Id), "a Personality already at home is not a target");
        Check(nhFollower.Id.Length > 0);
        // Omigawa: lower Force or no Weapon; bow him if he moved and she has a Weapon.
        foreach (var armed in new[] { false, true })
        {
            var o = Battle(); var omigawa = Bf(o, Omigawa, 0); var target = Bf(o, "training-lion-samurai", 1);
            if (armed) Add(o, "training-weapon", 0, Zone.Attached, province: 0, parent: omigawa.Id);
            var oe = MatchEngine.FromTestState(o);
            Accept(oe, Offer(oe, 0, "omigawa-move", omigawa.Id, target.Id));
            Check(oe.State.Cards[target.Id].Zone == Zone.Home, "moved home");
            Eq(oe.State.Cards[target.Id].Bowed, armed, armed ? "she has a Weapon: he is bowed" : "no Weapon: he is not bowed");
        }
        var p = Battle(); var omi = Bf(p, Omigawa, 0); var strong = Bf(p, "training-lion-veteran", 1); Force(strong, 40); Add(p, "training-weapon", 1, Zone.Attached, province: 0, parent: strong.Id);
        var pe = MatchEngine.FromTestState(p);
        Check(!(Options(pe, 0, "omigawa-move", omi.Id).FirstOrDefault(o => o.Enabled)?.TargetIds ?? []).Contains(strong.Id), "an armed Personality with higher Force is not a target");
    }

    // Kaname's own +2 Reaction makes every Ranged Attack open its window: both players pass it.
    private static void PassAttackWindow(MatchEngine e)
    {
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: "attackReaction" }; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(x => x.Type == "declineReaction").Command!);
    }

    private static void RangedActions()
    {
        // Kaname: Ranged 1 Attack any number of times per turn.
        var s = Battle(); var kaname = Bf(s, Kaname, 0); var weak = Bf(s, "training-lion-samurai", 1); Force(weak, -CardCatalog.Get("training-lion-samurai").Force + 1);
        var weak2 = Bf(s, "training-lion-samurai", 1); Force(weak2, -CardCatalog.Get("training-lion-samurai").Force + 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "kaname-ranged-attack", kaname.Id, weak.Id));
        PassAttackWindow(e);
        Check(e.State.Cards[weak.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "Ranged 1 destroyed a Force-1 Personality");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(CanUse(e, 0, "kaname-ranged-attack", kaname.Id), "and it may be used again this turn");
        // The +2 Reaction (Kaname's own) raises the pending Attack's strength from 1 to 3.
        foreach (var react in new[] { false, true })
        {
            var k = Battle(); var kaname2 = Bf(k, Kaname, 0); var target1 = Bf(k, "training-lion-samurai", 1); Force(target1, -CardCatalog.Get("training-lion-samurai").Force + 1);
            Bf(k, "training-attack-reactor", 0); // a second Reaction keeps the window open once Kaname's is played, so the strength can be read (issue #644)
            var ke = MatchEngine.FromTestState(k);
            Accept(ke, Offer(ke, 0, "kaname-ranged-attack", kaname2.Id, target1.Id));
            Eq(ke.State.PendingChoice?.Type, "attackReaction", "the Ranged Attack opens its window");
            Eq(ke.State.PendingAttack!.Strength, 1, "Ranged 1");
            if (react) { Accept(ke, ke.QueryActions(ke.State.PriorityPlayerId).First(x => x.Enabled && x.Command?.AbilityId == "kaname-attack-boost").Command!); Eq(ke.State.PendingAttack!.Strength, 3, "+2 strength"); }
            PassAttackWindow(ke);
        }
        // Umiko: +2 or -2, once: an enemy Ranged 4 Attack is turned to 2, or to 6.
        foreach (var (ability, expected) in new[] { ("umiko-attack-reduce", 2), ("umiko-attack-boost", 6) })
        {
            var u = Battle(); u.ActivePlayerId = 1; u.PriorityPlayerId = 1;
            var shooter = Bf(u, Yashiro, 1); var umiko = Bf(u, Umiko, 0); var victim = Bf(u, "training-crane-samurai", 0); Force(victim, -CardCatalog.Get("training-crane-samurai").Force + 4);
            u.Players[0].Provinces[0].Reconnaissance[1] = true;
            Bf(u, "training-attack-reactor", 0); // a second Reaction keeps the window open once Umiko's is played, so the strength can be read (issue #644)
            var ue = MatchEngine.FromTestState(u);
            Accept(ue, Offer(ue, 1, "yashiro-attack", shooter.Id, victim.Id));
            var seen = -1;
            for (var guard = 0; guard < 6 && ue.State.PendingChoice is { Type: "attackReaction" }; guard++)
            {
                var who = ue.State.PriorityPlayerId;
                var use = ue.QueryActions(who).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == ability);
                if (use is not null) { Accept(ue, use.Command!); seen = ue.State.PendingAttack!.Strength; Check(!ue.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId is "umiko-attack-boost" or "umiko-attack-reduce"), "one use covers both"); }
                Accept(ue, ue.QueryActions(ue.State.PriorityPlayerId).First(x => x.Type == "declineReaction").Command!);
            }
            Eq(seen, expected, $"{ability}: the Attack's strength");
            Check(umiko.Id.Length > 0);
        }
        // Yashiro: bow unless Reconnaissance; Ranged 4.
        var y = Battle(); var yashiro = Bf(y, Yashiro, 0); var t = Bf(y, "training-lion-samurai", 1);
        var ye = MatchEngine.FromTestState(y);
        Accept(ye, Offer(ye, 0, "yashiro-attack", yashiro.Id, t.Id));
        Check(ye.State.Cards[yashiro.Id].Bowed, "no Reconnaissance: Yashiro is bowed");
        Check(ye.State.Cards[t.Id].Zone is Zone.Dead or Zone.DynastyDiscard || CardCatalog.Get("training-lion-samurai").Force > 4, "Ranged 4 destroyed a Force of at most 4");
        // Akio: bow a performing Personality or Follower for Ranged 5; his Followers +1F with Reconnaissance.
        var a = Battle(); var akio = Bf(a, Akio, 0); var follower = Add(a, "training-follower", 0, Zone.Attached, province: 0, parent: akio.Id); var enemy = Bf(a, "training-lion-samurai", 1);
        var ae = MatchEngine.FromTestState(a);
        var performers = Options(ae, 0, "akio-attack", akio.Id).Where(o => o.Enabled).ToArray();
        Check(performers.Any(o => o.Label.Contains(akio.Id)) && performers.Any(o => o.Label.Contains(follower.Id)), "the Personality or a Follower performs it: " + string.Join(";", performers.Select(o => o.Label)));
        Accept(ae, Offer(ae, 0, "akio-attack", akio.Id, enemy.Id, follower.Id));
        Check(ae.State.Cards[follower.Id].Bowed && !ae.State.Cards[akio.Id].Bowed, "the performing Follower bowed");
        var b = Battle(); var akio2 = Bf(b, Akio, 0); var f2 = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: akio2.Id); var ally = Bf(b, "training-crane-samurai", 0); var f3 = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: ally.Id);
        var enemyF = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: Bf(b, "training-lion-samurai", 1).Id);
        var be = MatchEngine.FromTestState(b); var before = be.CardForceForTest(f3.Id); var enemyBefore = be.CardForceForTest(enemyF.Id);
        b.Players[1].Provinces[0].Reconnaissance[0] = true;
        var recon = MatchEngine.FromTestState(b);
        Eq(recon.CardForceForTest(f3.Id), before + 1, "his Followers at his location have +1F while its controller has Reconnaissance");
        Eq(recon.CardForceForTest(enemyF.Id), enemyBefore, "an enemy Follower is untouched");
        var awayHost = Add(b, "training-crane-veteran", 0, Zone.Home); var awayFollower = Add(b, "training-follower", 0, Zone.Attached, parent: awayHost.Id);
        var awayBefore = MatchEngine.FromTestState(b).CardForceForTest(awayFollower.Id);
        b.Players[1].Provinces[0].Reconnaissance[0] = false;
        var noRecon = MatchEngine.FromTestState(b).CardForceForTest(awayFollower.Id);
        b.Players[1].Provinces[0].Reconnaissance[0] = true;
        Eq(awayBefore, noRecon, "a Follower of a Personality at home is not at Akio's location: no bonus");
        Check(f2.Id != f3.Id);
    }

    private static void StaticTraits()
    {
        // Shibata: +1 strength to the controller's provinces while another player controls a dishonorable Personality.
        var s = Table(Phase.Action, 0); var shibata = Add(s, Shibata, 0, Zone.Home); var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var baseStrength = e.GetView(0).Players[0].Provinces[0].Strength;
        var dishonored = Edit(e, st => st.Cards[foe.Id].Dishonorable = true);
        Eq(dishonored.GetView(0).Players[0].Provinces[0].Strength, baseStrength + 1, "+1 strength while another player controls a dishonorable Personality");
        Eq(dishonored.GetView(0).Players[1].Provinces[0].Strength, e.GetView(0).Players[1].Provinces[0].Strength, "the other player's provinces are not raised");
        Check(shibata.Id != foe.Id);
        // Kota and Umiko: +2F while they have a Spell.
        var k = Table(Phase.Action, 0); var kota = Add(k, Kota, 0, Zone.Home); var umiko = Add(k, Umiko, 0, Zone.Home);
        var ke = MatchEngine.FromTestState(k); var kotaBase = ke.CardForceForTest(kota.Id); var umikoBase = ke.CardForceForTest(umiko.Id);
        Add(k, "training-spell", 0, Zone.Attached, parent: kota.Id); Add(k, "training-spell", 0, Zone.Attached, parent: umiko.Id);
        var ke2 = MatchEngine.FromTestState(k);
        Eq(ke2.CardForceForTest(kota.Id), kotaBase + 2, "Kota +2F with a Spell"); Eq(ke2.CardForceForTest(umiko.Id), umikoBase + 2, "Umiko +2F with a Spell");
        // Taishuu: other players' maximum hand size is 7.
        var t = Table(Phase.Action, 0); Add(t, Taishuu, 0, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        var t2 = Table(Phase.Action, 0);
        var plain = MatchEngine.FromTestState(t2);
        Check(te.MaxHandSizeForTest(1) == plain.MaxHandSizeForTest(1) - 1 && te.MaxHandSizeForTest(0) == plain.MaxHandSizeForTest(0), "other players' maximum hand sizes are reduced by 1");
        // Yashiro: 2 less Gold if you are a Lion or Scorpion Clan player.
        var yCost = new List<int>();
        foreach (var clan in new[] { "Lion", "Scorpion", "Crane" })
        {
            var y = MatchEngine.CreateEconomyScenario().State;
            y.Players[0].Clan = clan;
            y.Cards[y.Players[0].Provinces[0].CardId!].DefinitionId = Yashiro; y.Cards[y.Players[0].Provinces[0].CardId!].FaceUp = true;
            var ye = MatchEngine.FromTestState(y);
            yCost.Add(ye.QuotePurchase(0, y.Players[0].Provinces[0].CardId!).Cost);
        }
        Check(yCost[0] == yCost[2] - 2 && yCost[1] == yCost[2] - 2, "2 less Gold for a Lion or Scorpion Clan player only: " + string.Join(",", yCost));
    }

    // Plays the reaction if the player holding priority in the open window has it, else passes, until the window is closed.
    private static void React(MatchEngine e, string reactionAbility)
    {
        for (var guard = 0; guard < 8 && e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" }; guard++)
        {
            var who = e.State.PriorityPlayerId;
            var mine = e.QueryActions(who).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == reactionAbility);
            Accept(e, mine?.Command ?? e.QueryActions(who).First(x => x.Type == "declineReaction").Command!);
        }
    }

    private static void TargetedReactions()
    {
        // Kota: an enemy action moves one of your Personalities at his battlefield; the movement is negated.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var kota = Bf(s, Kota, 0); var ally = Bf(s, "training-crane-samurai", 0); var masafuni = Bf(s, Masafuni, 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "masafuni-move", masafuni.Id, ally.Id));
        Check(e.State.PendingChoice is { Type: "beforeActionReaction" }, "the action is held: " + e.State.PendingChoice?.Type);
        React(e, "kota-negate-move");
        Check(e.State.Cards[ally.Id].Zone == Zone.Battlefield, "his movement was negated: he is still at the battlefield");
        Check(kota.Id != ally.Id);
        // Without Kota's reaction the same action moves him home.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var ally2 = Bf(s2, "training-crane-samurai", 0); var masafuni2 = Bf(s2, Masafuni, 1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "masafuni-move", masafuni2.Id, ally2.Id));
        Check(e2.State.Cards[ally2.Id].Zone == Zone.Home, "no Kota: he goes home");
        // Taishuu: another player's Political action targets one of your Personalities; its effects are negated.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 1;
        var taishuu = Bf(t, Taishuu, 0); var mine = Bf(t, "training-crane-samurai", 0); var komiko = Bf(t, Komiko, 1);
        mine.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-crane-samurai").PersonalHonor));
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 1, "komiko-bow", komiko.Id, mine.Id));
        Check(te.State.PendingChoice is { Type: "beforeActionReaction" }, "the Political action is held");
        React(te, "taishuu-political-negate");
        Check(!te.State.Cards[mine.Id].Bowed && taishuu.Id != mine.Id, "its effects were negated: he is not bowed");
        var t2 = Table(Phase.Battle, 1); t2.PriorityPlayerId = 1;
        var mine2 = Bf(t2, "training-crane-samurai", 0); var komiko2 = Bf(t2, Komiko, 1);
        mine2.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-crane-samurai").PersonalHonor));
        var te2 = MatchEngine.FromTestState(t2);
        Accept(te2, Offer(te2, 1, "komiko-bow", komiko2.Id, mine2.Id));
        Check(te2.State.Cards[mine2.Id].Bowed, "no Taishuu: the same action bows him");
    }

    private static MatchEngine EconomyBattle(Action<MatchState> arrange, Phase phase = Phase.Battle, int actor = 0)
    {
        var s = Table(phase, actor); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        arrange(s);
        return MatchEngine.FromTestState(s);
    }

    private static void AttachActions()
    {
        // Ebara: a Weapon in the hand (not one in a discard pile), paying all costs; then draw a card.
        CardInstance ebara = null!, weapon = null!, discarded = null!;
        var e = EconomyBattle(s => { ebara = Bf(s, Ebara, 0); weapon = Add(s, "training-weapon", 0, Zone.Hand); discarded = Add(s, "training-weapon", 0, Zone.FateDiscard); Bf(s, "training-lion-samurai", 1); });
        var targets = Options(e, 0, "ebara-equip", ebara.Id).First(o => o.Enabled).TargetIds!;
        Check(targets.SequenceEqual(new[] { weapon.Id }), "only a Weapon in your hand: " + string.Join(",", targets));
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "ebara-equip", ebara.Id, weapon.Id));
        Check(e.State.Cards[weapon.Id].Zone == Zone.Attached && e.State.Cards[weapon.Id].AttachedTo == ebara.Id, "attached to Ebara");
        Eq(e.State.Players[0].Hand.Count, handBefore, "the Weapon left the hand and a card was drawn");
        Check(discarded.Zone == Zone.FateDiscard);
        // Hailung: the full price attacking as a non-Crab player; 4 less while defending or as a Crab Clan player.
        foreach (var (attacker, clan, discounted) in new[] { (0, "Lion", false), (1, "Lion", true), (0, "Crab", true) })
        {
            CardInstance hailung = null!, follower = null!;
            var h = EconomyBattle(s => { s.Players[0].Clan = clan; hailung = Bf(s, Hailung, 0); follower = Add(s, "training-follower", 0, Zone.Hand); Bf(s, "training-lion-samurai", 1); }, Phase.Battle, attacker);
            var full = Options(h, 0, "hailung-attach", hailung.Id).FirstOrDefault(o => o.Enabled); var less = Options(h, 0, "hailung-attach-discount", hailung.Id).FirstOrDefault(o => o.Enabled);
            Check(discounted ? full is null && less is not null : full is not null && less is null, $"{clan} {(attacker == 0 ? "attacker" : "defender")}: the {(discounted ? "discounted" : "full-price")} attach only");
            var cost = CardCatalog.Get("training-follower").GoldCost;
            Eq((discounted ? less : full)!.Gold!.Cost, discounted ? Math.Max(0, cost - 4) : cost, "the price");
            Accept(h, (discounted ? less : full)!.Command!);
            Check(h.State.Cards[follower.Id].AttachedTo == hailung.Id, "the Follower is attached");
        }
    }

    private static void EntryAndGrants()
    {
        // Izuna: after she enters play, dishonor an enemy Personality with Personal Honor at most her Chi and permanently give him Kolat, Ninja or Shadowlands.
        foreach (var (ability, keyword) in new[] { ("izuna-kolat", "Kolat"), ("izuna-ninja", "Ninja"), ("izuna-shadowlands", "Shadowlands") })
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
            s.Players[0].Honor = 20;
            var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = Izuna; s.Cards[cardId].FaceUp = true;
            var victim = Add(s, "training-lion-samurai", 1, Zone.Home); victim.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-lion-samurai").PersonalHonor));
            var proud = Add(s, "training-lion-veteran", 1, Zone.Home); proud.Modifiers.Add(new("test-source", "test", "PersonalHonor", 10));
            var own = Add(s, "training-crane-samurai", 0, Zone.Home);
            var edge = Add(s, "training-lion-veteran", 1, Zone.Home); edge.Modifiers.Add(new("test-source", "test", "PersonalHonor", CardCatalog.Get(Izuna).Chi - CardCatalog.Get("training-lion-veteran").PersonalHonor));
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(Izuna).GoldCost).First().SourceIds));
            Eq(e.State.PendingChoice?.Type, "entryReaction", "her entry window is open");
            var offered = e.QueryActions(0).Where(x => x.Type == "useAbility" && x.Command?.AbilityId is "izuna-kolat" or "izuna-ninja" or "izuna-shadowlands").ToArray();
            Eq(offered.Length, 3, "one option per keyword she may give");
            Check(offered.All(o => o.Enabled && o.TargetIds!.Contains(victim.Id) && o.TargetIds.Contains(edge.Id) && !o.TargetIds.Contains(proud.Id) && !o.TargetIds.Contains(own.Id)), "another player's Personality with Personal Honor at most her Chi (equal is allowed)");
            Accept(e, Offer(e, 0, ability, cardId, victim.Id));
            Check(e.State.Cards[victim.Id].Dishonorable && e.HasKeyword(victim.Id, keyword), $"dishonored and permanently {keyword}");
            Check(e.State.Cards[victim.Id].GrantedKeywords.Any(g => g.Keyword == keyword && g.Duration == GrantDuration.Permanent), "permanently");
            Check(!e.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId is "izuna-kolat" or "izuna-ninja" or "izuna-shadowlands"), "the three share one use");
        }
        // Yupadi: bow Yupadi or one of her Spells, and give a Personality Naval (this turn).
        var y = Table(Phase.Action, 0); y.PriorityPlayerId = 0; y.Players[0].TurnsTaken = 3; y.Players[1].TurnsTaken = 3;
        var yupadi = Add(y, Yupadi, 0, Zone.Home); var spell = Add(y, "training-spell", 0, Zone.Attached, parent: yupadi.Id); var ally = Add(y, "training-crane-samurai", 0, Zone.Home);
        var ye = MatchEngine.FromTestState(y);
        Accept(ye, Offer(ye, 0, "yupadi-naval", yupadi.Id, ally.Id, spell.Id));
        Check(ye.HasKeyword(ally.Id, "Naval") && ye.State.Cards[spell.Id].Bowed && !ye.State.Cards[yupadi.Id].Bowed, "Naval given; the Spell paid the bow");
        var y2 = MatchEngine.FromTestState(y);
        Accept(y2, Offer(y2, 0, "yupadi-naval", yupadi.Id, ally.Id, yupadi.Id));
        Check(y2.State.Cards[yupadi.Id].Bowed, "or Yupadi herself bows");
        // Iemasa's Limited: gain 3 Honor and may not declare an attack.
        var lim = Table(Phase.Action, 0); lim.PriorityPlayerId = 0; lim.Players[0].TurnsTaken = 3; lim.Players[1].TurnsTaken = 3;
        var iemasa = Add(lim, Iemasa, 0, Zone.Home);
        var ie = MatchEngine.FromTestState(lim); var honor = Honor(ie, 0);
        Accept(ie, Enabled(ie, 0, "iemasa-rebuild", iemasa.Id).Command!);
        Check(Honor(ie, 0) >= honor + 3 && ie.State.Players[0].MayNotAttackThisTurn, "3 Honor; no attack this turn");
    }

    private static void RedHunger()
    {
        var hunger = CardCatalog.Get("oracle-l5r-8283");
        Check(hunger.Name == "The Red Hunger" && hunger.Abilities!.SequenceEqual(new[] { "red-hunger-attachments", "red-hunger-personality" }), "the printed card");
        var s = Battle(); var rh = Bf(s, "oracle-l5r-8283", 0); var host = Bf(s, "training-lion-samurai", 1); var weapon = Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: host.Id);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); var bare = Bf(s, "training-crane-samurai", 1); var strong = Bf(s, "training-lion-veteran", 1); Force(strong, 50);
        var e = MatchEngine.FromTestState(s); var force = e.CardForceForTest(rh.Id);
        var personalityTargets = Options(e, 0, "red-hunger-personality", rh.Id).First(o => o.Enabled).TargetIds!;
        Check(personalityTargets.Contains(bare.Id) && !personalityTargets.Contains(host.Id) && !personalityTargets.Contains(strong.Id), "a Personality without attachments and with lower Force: " + string.Join(",", personalityTargets));
        Accept(e, Offer(e, 0, "red-hunger-personality", rh.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "he swallows him whole");
        Check(!CanUse(e, 0, "red-hunger-attachments", rh.Id), "the two shapes share one use");
        var f = MatchEngine.FromTestState(s);
        var slot = Enabled(f, 0, "red-hunger-attachments", rh.Id);
        Check(slot.Slots!.Single().CardIds.Contains(weapon.Id) && slot.Slots!.Single().CardIds.Contains(follower.Id), "attachments of either side");
        Accept(f, slot.Command! with { Selections = [Pick("attachments", weapon.Id, follower.Id)] });
        Check(f.State.Cards[weapon.Id].Zone != Zone.Attached && f.State.Cards[follower.Id].Zone != Zone.Attached, "both are destroyed");
        // A total Force that is not lower than his own is refused.
        var t = Battle(); var rh2 = Bf(t, "oracle-l5r-8283", 0); var host2 = Bf(t, "training-lion-samurai", 1); var big = Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: host2.Id); big.Modifiers.Add(new("test-source", "test", "Force", 50));
        var te = MatchEngine.FromTestState(t);
        Check(!CanUse(te, 0, "red-hunger-attachments", rh2.Id), "not offered when the only attachments outweigh him");
        Reject(te, new GameCommand("useAbility", 0, te.Version, CardId: rh2.Id, AbilityId: "red-hunger-attachments", Selections: [Pick("attachments", big.Id)]));
        Check(force > 0);
    }

    private static void DecidersAndDuels()
    {
        // Tarao: pay 2 Gold; the chosen player bows an unbowed Personality or Holding of his, or discards a card from his hand.
        foreach (var fromHand in new[] { false, true })
        {
            CardInstance tarao = null!, theirs = null!, holding = null!, handCard = null!, bowedOne = null!;
            var e = EconomyBattle(s => { tarao = Add(s, Tarao, 0, Zone.Home); theirs = Add(s, "training-lion-samurai", 1, Zone.Home); holding = Add(s, "training-marketplace", 1, Zone.Home); handCard = Add(s, "training-weapon", 1, Zone.Hand); bowedOne = Add(s, "training-lion-veteran", 1, Zone.Home, bowed: true); s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3; }, Phase.Action);
            var chooseThem = Options(e, 0, "tarao-sacrifice", tarao.Id).First(o => o.Enabled && o.Label.Contains("opponent"));
            Accept(e, chooseThem.Command!);
            Eq(e.State.PendingChoice?.Type, "deciderChoice", "the chosen player decides"); Eq(e.State.PendingChoice?.PlayerId, 1);
            var picks = e.QueryActions(1).Where(x => x.Type == "chooseDecider").Select(x => x.CardId).ToArray();
            Check(picks.Contains(theirs.Id) && picks.Contains(holding.Id) && picks.Contains(handCard.Id), "his Personality, his Holding and a card in his hand: " + string.Join(",", picks));
            Check(!picks.Contains(bowedOne.Id), "a bowed Personality is not among them");
            Check(!e.QueryActions(0).Any(x => x.Type == "chooseDecider"), "the acting player has nothing to choose");
            var chosen = fromHand ? handCard : theirs;
            Accept(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: chosen.Id));
            if (fromHand) Check(e.State.Cards[handCard.Id].Zone == Zone.FateDiscard && !e.State.Players[1].Hand.Contains(handCard.Id), "a card in his hand is discarded");
            else Check(e.State.Cards[theirs.Id].Bowed, "any other card is bowed");
        }
        // Kiuchi: two performing Personalities, one Nonhuman or Undead, take the higher printed Force.
        CardInstance kiuchi = null!, plain = null!, beast = null!;
        var k = EconomyBattle(s => { kiuchi = Add(s, Kiuchi, 0, Zone.Home); plain = Add(s, "training-crane-samurai", 0, Zone.Home); beast = Add(s, "training-crane-veteran", 0, Zone.Home); GiveKeyword(beast, "Nonhuman"); s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3; }, Phase.Action);
        var slot = Enabled(k, 0, "kiuchi-equalize", kiuchi.Id);
        var second = slot.Slots!.Single(x => x.Slot == "second");
        Check(second.CardIds.Contains(beast.Id) && !second.CardIds.Contains(plain.Id), "the second is a Nonhuman or Undead Personality: " + string.Join(",", second.CardIds));
        var higher = Math.Max(Def(plain).Force, Def(beast).Force);
        Accept(k, slot.Command! with { Selections = [Pick("first", plain.Id), Pick("second", beast.Id)] });
        Eq(k.CardForceForTest(plain.Id), higher, "both have the higher printed Force"); Eq(k.CardForceForTest(beast.Id), higher);
        // Shigeyuki: a refusable challenge with Personal Honor as the stat; a refusal moves him home.
        var d = Battle(); var shigeyuki = Bf(d, Shigeyuki, 0); var foe = Bf(d, "training-lion-samurai", 1);
        foe.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-lion-samurai").PersonalHonor));
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, "shigeyuki-duel", shigeyuki.Id, foe.Id));
        Check(de.State.PendingDuel is { Refusable: true, Stat: "PersonalHonor", Stage: "AwaitingAccept" }, "a refusable Personal Honor challenge");
        Accept(de, new GameCommand("declineDuel", 1, de.Version));
        Check(de.State.Cards[foe.Id].Zone == Zone.Home && de.State.PendingDuel is null, "he refused and was moved home");
        // Accepted, and Shigeyuki wins: the loser's unit (he and his attachments) is bowed.
        var w = Battle(); var shig = Bf(w, Shigeyuki, 0); shig.Modifiers.Add(new("test-source", "test", "PersonalHonor", 10)); var loser = Bf(w, "training-lion-samurai", 1);
        loser.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-lion-samurai").PersonalHonor));
        var loserFollower = Add(w, "training-follower", 1, Zone.Attached, province: 0, parent: loser.Id);
        var we = MatchEngine.FromTestState(w);
        Accept(we, Offer(we, 0, "shigeyuki-duel", shig.Id, loser.Id));
        Accept(we, new GameCommand("acceptDuel", 1, we.Version));
        for (var guard = 0; we.State.PendingDuel is { } duel && guard < 6; guard++)
            Accept(we, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", we.State.PriorityPlayerId, we.Version) : new GameCommand("strike", duel.TurnPlayerId, we.Version));
        Check(we.State.Cards[loser.Id].Bowed && we.State.Cards[loserFollower.Id].Bowed && !we.State.Cards[shig.Id].Bowed, "the loser and his Follower are bowed, the winner is not");
    }

    // Values at the edge of each "lower than" / "at most" wording, and the scope of the static traits and reactions.
    private static void Boundaries()
    {
        // The Red Hunger: the attachments' total Force must be LOWER than his; so must a Personality's.
        var s = Battle(); var rh = Bf(s, "oracle-l5r-8283", 0); var host = Bf(s, "training-lion-samurai", 1); var att = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var probe = MatchEngine.FromTestState(s); var rhForce = probe.CardForceForTest(rh.Id); var attForce = probe.CardForceForTest(att.Id);
        Force(att, rhForce - attForce);
        var equalTotal = MatchEngine.FromTestState(s);
        Check(!CanUse(equalTotal, 0, "red-hunger-attachments", rh.Id), "a total equal to his Force is not lower");
        Reject(equalTotal, new GameCommand("useAbility", 0, equalTotal.Version, CardId: rh.Id, AbilityId: "red-hunger-attachments", Selections: [Pick("attachments", att.Id)]));
        Force(att, -1);
        Check(CanUse(MatchEngine.FromTestState(s), 0, "red-hunger-attachments", rh.Id), "one less is lower");
        var p = Battle(); var rh2 = Bf(p, "oracle-l5r-8283", 0); var equalForce = Bf(p, "training-lion-samurai", 1); var lowerForce = Bf(p, "training-crane-samurai", 1);
        var pe0 = MatchEngine.FromTestState(p); Force(equalForce, pe0.CardForceForTest(rh2.Id) - pe0.CardForceForTest(equalForce.Id)); Force(lowerForce, pe0.CardForceForTest(rh2.Id) - 1 - pe0.CardForceForTest(lowerForce.Id));
        var targets = Options(MatchEngine.FromTestState(p), 0, "red-hunger-personality", rh2.Id).First(o => o.Enabled).TargetIds!;
        Check(!targets.Contains(equalForce.Id) && targets.Contains(lowerForce.Id), "a Personality with Force equal to his is not a target, one lower is: " + string.Join(",", targets));
        // Komiko: Personal Honor at most her Chi (equal is allowed). Eun-ju: Personal Honor LOWER than hers (equal is not).
        var k = Battle(); var komiko = Bf(k, Komiko, 0); var atChi = Bf(k, "training-lion-samurai", 1); var overChi = Bf(k, "training-lion-veteran", 1);
        var chi = Def(komiko).Chi;
        atChi.Modifiers.Add(new("test-source", "test", "PersonalHonor", chi - Def(atChi).PersonalHonor)); overChi.Modifiers.Add(new("test-source", "test", "PersonalHonor", chi + 1 - Def(overChi).PersonalHonor));
        var komikoTargets = Options(MatchEngine.FromTestState(k), 0, "komiko-bow", komiko.Id).First(o => o.Enabled).TargetIds!;
        Check(komikoTargets.Contains(atChi.Id) && !komikoTargets.Contains(overChi.Id), "Personal Honor equal to her Chi is allowed, above it is not: " + string.Join(",", komikoTargets));
        var u = Battle(); var eunju = Bf(u, EunJu, 0); var same = Bf(u, "training-lion-samurai", 1); var under = Bf(u, "training-crane-samurai", 1);
        same.Modifiers.Add(new("test-source", "test", "PersonalHonor", Def(eunju).PersonalHonor - Def(same).PersonalHonor)); under.Modifiers.Add(new("test-source", "test", "PersonalHonor", Def(eunju).PersonalHonor - 1 - Def(under).PersonalHonor));
        var eunjuTargets = Options(MatchEngine.FromTestState(u), 0, "eun-ju-bow", eunju.Id).First(o => o.Enabled).TargetIds!;
        Check(!eunjuTargets.Contains(same.Id) && eunjuTargets.Contains(under.Id), "equal Personal Honor is not lower, one less is: " + string.Join(",", eunjuTargets));
        // Zinan: any attachment -- a Spell, too.
        var z = Battle(); var zinan = Bf(z, Zinan, 0); var zhost = Bf(z, "training-lion-samurai", 1); var spell = Add(z, "training-spell", 1, Zone.Attached, province: 0, parent: zhost.Id);
        Check(Options(MatchEngine.FromTestState(z), 0, "zinan-return", zinan.Id).First(o => o.Enabled).TargetIds!.Contains(spell.Id), "a Spell attachment may be returned");
        // Iemasa: an enemy card only.
        var m = Battle(); var iemasa = Bf(m, Iemasa, 0); var ownWeak = Bf(m, "training-crane-samurai", 0); Bf(m, "training-lion-samurai", 1);
        Check(!Options(MatchEngine.FromTestState(m), 0, "iemasa-bow", iemasa.Id).First(o => o.Enabled).TargetIds!.Contains(ownWeak.Id), "one of your own cards is not a target");
        // Shibata: the bonus is its controller's, and needs ANOTHER player's dishonorable Personality; his own gives nothing to anyone.
        var sh = Table(Phase.Action, 0); Add(sh, Shibata, 0, Zone.Home); var ownSinner = Add(sh, "training-crane-samurai", 0, Zone.Home); ownSinner.Dishonorable = true;
        var she = MatchEngine.FromTestState(sh); var shPlain = MatchEngine.FromTestState(Table(Phase.Action, 0));
        Eq(she.GetView(0).Players[1].Provinces[0].Strength, shPlain.GetView(0).Players[1].Provinces[0].Strength, "the other player's provinces are not raised");
        Eq(she.GetView(0).Players[0].Provinces[0].Strength, shPlain.GetView(0).Players[0].Provinces[0].Strength, "and his own are not raised by his own dishonorable Personality");
        // Kota's reaction is for a Personality at HIS battlefield; Taishuu's answers Political actions only.
        var kt = Table(Phase.Battle, 1); kt.PriorityPlayerId = 1;
        var kota = Add(kt, Kota, 0, Zone.Battlefield, province: 1); var here = Bf(kt, "training-crane-samurai", 0); var masafuni = Bf(kt, Masafuni, 1);
        var kte = MatchEngine.FromTestState(kt);
        Accept(kte, Offer(kte, 1, "masafuni-move", masafuni.Id, here.Id));
        Check(!kte.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == "kota-negate-move"), "Kota answers for a Personality at his battlefield only");
        Check(kota.Id.Length > 0);
        var tt = Table(Phase.Battle, 1); tt.PriorityPlayerId = 1;
        var taishuu = Bf(tt, Taishuu, 0); var mine = Bf(tt, "training-crane-samurai", 0); var tsuji = Bf(tt, Tsuji, 1);
        mine.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-crane-samurai").PersonalHonor));
        var tte = MatchEngine.FromTestState(tt);
        Accept(tte, Offer(tte, 1, "tsuji-bow", tsuji.Id, mine.Id));
        Check(!tte.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId == "taishuu-political-negate"), "Tsuji's bow is not a Political action: Taishuu has nothing to answer");
        Check(taishuu.Id.Length > 0);
    }
}
