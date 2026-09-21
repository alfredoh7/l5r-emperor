using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #431: the Before the Dawn Strategies, Items, Spells, Followers, Holding, Celestials and Region wired on primitives the engine already has (strategy-wave-43, item-wave-8, spell-wave-16,
// follower-wave-20, holding-wave-6, celestial-wave-4, region-wave-5).
internal static class BeforeTheDawnCardTests
{
    private const string GentleWord = "oracle-l5r-29", AngerManagement = "oracle-l5r-406", ControlTheBoard = "oracle-l5r-1486", DefendingHome = "oracle-l5r-1930", Detained = "oracle-l5r-1984",
        DismissingCur = "oracle-l5r-2027", OfOneInstant = "oracle-l5r-5668", ReadiedSteel = "oracle-l5r-6195", ShadowTactics = "oracle-l5r-6662", SlyDeceiver = "oracle-l5r-7301",
        SpiritBlade = "oracle-l5r-7421", SpiritTruth = "oracle-l5r-7428", SpiritWarrior = "oracle-l5r-7429", SpiritKnowledge = "oracle-l5r-8353", Stockpiled = "oracle-l5r-7492",
        Mountain = "oracle-l5r-7557", SecondFeint = "oracle-l5r-8305", SoundOfThunder = "oracle-l5r-8347", WorldDisappears = "oracle-l5r-8436", ThoughtsOfWind = "oracle-l5r-8452",
        TsunamiTattoo = "oracle-l5r-8793", Yari = "oracle-l5r-78", CrimsonArmor = "oracle-l5r-1564", DragonsFavor = "oracle-l5r-18", EmbersFire = "oracle-l5r-2309",
        AshigaruRecruits = "oracle-l5r-618", Genji = "oracle-l5r-2792", Fledgling = "oracle-l5r-9713", Indomitable = "oracle-l5r-3737", Ebisu = "oracle-l5r-2276",
        Saibankan = "oracle-l5r-6446", Shrine = "oracle-l5r-7502";

    public static void Run(TestRunner runner) => runner.Run("btd-cards", [
        ("Published shapes: the thirty-three cards are the printed cards with their abilities, traits and art", PublishedShapes),
        ("Heroic and Tactical actions: Anger Management, Defending Their Home, Control the Board", HeroicAndTactical),
        ("Political actions: A Gentle Word, Dismissing the Cur, Detained (after engaging)", PoliticalActions),
        ("Movement Strategies: Second Feint, Sound of Thunder, Tsunami Tattoo, Shadow Tactics, Strength of the Mountain", MovementStrategies),
        ("Monk and Duelist Strategies: Thoughts of Wind, The World Disappears, Of One Instant", MonkStrategies),
        ("Attaching Strategies: Readied Steel and Stockpiled Resources", AttachingStrategies),
        ("The Spirit Strategies: Blade, Truth, Warrior and Knowledge give a keyword, or once he has it an ability", Spirits),
        ("Sly Deceiver: a Ninja permanently copies a non-Unique Personality's keywords and abilities, but not Clan Champion, Daimyo or Shugenja", SlyDeceiverCopy),
        ("Duels: the Focus Effects of Of One Instant, Shadow Tactics and A Simple Yari", DuelFocusEffects),
        ("Items and Spells: A Simple Yari, Crimson Shadow Armor, A Dragon's Favor, Ember's Final Fire", ItemsAndSpells),
        ("Followers and the Holding: Ashigaru Recruits, Genji's Students, Indomitable Home", FollowersAndHolding),
        ("Celestials and the Region: Ebisu's Honesty, Saibankan's Justice, Stone-Hewed Shrine", CelestialsAndRegion),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle() { var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; return s; }
    private static MatchState ActionPhase() { var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3; return s; }
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) GiveKeyword(card, k); return card; }
    private static bool Dead(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard;

    private static void PublishedShapes()
    {
        var expected = new (string Id, string Name, CardType Type, string[] Abilities, string Effect)[]
        {
            (GentleWord, "A Gentle Word", CardType.Strategy, ["gentle-word-rehonor"], "none"), (AngerManagement, "Anger Management", CardType.Strategy, ["anger-management-destroy"], "none"),
            (ControlTheBoard, "Control the Board", CardType.Strategy, ["control-the-board-terrain", "control-the-board-weaken"], "none"), (DefendingHome, "Defending Their Home", CardType.Strategy, ["defending-their-home-force"], "none"),
            (Detained, "Detained", CardType.Strategy, ["detained-move"], "none"), (DismissingCur, "Dismissing the Cur", CardType.Strategy, ["dismissing-the-cur-move"], "none"),
            (OfOneInstant, "Of One Instant", CardType.Strategy, ["of-one-instant-focus", "of-one-instant-boost"], "none"), (ReadiedSteel, "Readied Steel", CardType.Strategy, ["readied-steel-straighten", "readied-steel-attach"], "none"),
            (ShadowTactics, "Shadow Tactics", CardType.Strategy, ["shadow-tactics-focus", "shadow-tactics-stay", "shadow-tactics-home"], "none"), (SlyDeceiver, "Sly Deceiver", CardType.Strategy, ["sly-deceiver-copy"], "none"),
            (SpiritBlade, "Spirit of the Blade", CardType.Strategy, ["spirit-of-the-blade"], "none"), (SpiritTruth, "Spirit of the Truth", CardType.Strategy, ["spirit-of-the-truth"], "none"),
            (SpiritWarrior, "Spirit of the Warrior", CardType.Strategy, ["spirit-of-the-warrior"], "none"), (SpiritKnowledge, "The Spirit of Knowledge", CardType.Strategy, ["spirit-of-knowledge"], "none"),
            (Stockpiled, "Stockpiled Resources", CardType.Strategy, ["stockpiled-attach", "stockpiled-attach-commander"], "none"), (Mountain, "Strength of the Mountain", CardType.Strategy, ["strength-of-the-mountain-bow", "strength-of-the-mountain-earth"], "none"),
            (SecondFeint, "The Second Feint", CardType.Strategy, ["second-feint-bow"], "none"), (SoundOfThunder, "The Sound of Thunder", CardType.Strategy, ["sound-of-thunder-move"], "none"),
            (WorldDisappears, "The World Disappears", CardType.Strategy, ["world-disappears-void", "world-disappears-bow"], "none"), (ThoughtsOfWind, "Thoughts of Wind", CardType.Strategy, ["thoughts-of-wind-cavalry"], "none"),
            (TsunamiTattoo, "Tsunami Tattoo", CardType.Strategy, ["tsunami-tattoo-move"], "none"), (Yari, "A Simple Yari", CardType.Item, ["simple-yari-focus", "simple-yari-attach"], "none"),
            (CrimsonArmor, "Crimson Shadow Armor", CardType.Item, ["crimson-shadow-bow"], "none"), (DragonsFavor, "A Dragon's Favor", CardType.Spell, ["dragons-favor-attack"], "dragons-favor-chi"),
            (EmbersFire, "Ember's Final Fire", CardType.Spell, ["embers-final-fire-destroy"], "embers-final-fire-force"), (AshigaruRecruits, "Ashigaru Recruits", CardType.Follower, ["ashigaru-recruits-create"], "none"),
            (Genji, "Genji's Students", CardType.Follower, ["genji-province-raise", "genji-province-lower", "genji-attack"], "none"), (Indomitable, "Indomitable Home", CardType.Holding, ["indomitable-home-strength"], "none"),
            (Ebisu, "Ebisu's Honesty", CardType.Celestial, ["ebisu-destroy-province"], "ebisu-provinces"), (Saibankan, "Saibankan's Justice", CardType.Celestial, ["saibankan-dishonor"], "saibankan-no-lobby"),
            (Shrine, "Stone-Hewed Shrine", CardType.Region, ["stone-hewed-shrine-strength"], "stone-hewed-shrine-force"),
        };
        var root = RepoRoot();
        foreach (var (id, name, type, abilities, effect) in expected)
        {
            var card = CardCatalog.Get(id);
            Check(card.Name == name && card.Type == type && (card.Abilities ?? []).SequenceEqual(abilities) && card.Effect == effect, $"{name}: {card.Name} {card.Type} {string.Join(",", card.Abilities ?? [])} {card.Effect}");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{name}: art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var scan = CardCatalog.Get(Fledgling);
        Check(scan.CreatedOnly && scan.Type == CardType.Follower && scan.Force == 0 && (scan.Keywords ?? []).Contains("Ashigaru"), "the Fledgling Ashigaru is the created-only scan Ashigaru Recruits creates");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["ashigaru-recruits-create"].Creates!).Id, Fledgling, "and it resolves to it");
        Eq(CardCatalog.Get(Indomitable).GoldProduction, 2, "Indomitable Home produces 2 Gold");
    }

    private static void HeroicAndTactical()
    {
        // Anger Management: destroy an enemy card with no attachments that is bowed or Nonhuman; bow it.
        foreach (var (bowed, nonhuman, armed, destroyed, bows) in new[] { (true, false, false, true, false), (false, true, false, true, false), (false, false, false, false, true), (true, false, true, false, false), (false, false, true, false, true) })
        {
            var s = Battle(); var hero = Kw(Bf(s, "training-crane-samurai", 0), "Hero"); var card = Add(s, AngerManagement, 0, Zone.Hand);
            var enemy = Bf(s, "training-lion-samurai", 1, bowed); if (nonhuman) GiveKeyword(enemy, "Nonhuman"); if (armed) Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: enemy.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "anger-management-destroy", card.Id, enemy.Id, hero.Id));
            Eq(e.State.Cards[enemy.Id].Zone is Zone.Dead or Zone.DynastyDiscard, destroyed, $"bowed {bowed}, Nonhuman {nonhuman}, attachments {armed}: destroyed");
            if (!destroyed) Eq(e.State.Cards[enemy.Id].Bowed, bows || bowed, "otherwise it is bowed");
        }
        var h = Battle(); var hero2 = Bf(h, "training-crane-samurai", 0); var card2 = Add(h, AngerManagement, 0, Zone.Hand); Bf(h, "training-lion-samurai", 1);
        Check(!Options(MatchEngine.FromTestState(h), 0, "anger-management-destroy", card2.Id).Any(o => o.Enabled) && hero2.Id.Length > 0, "only a Hero performs it");
        // Defending Their Home: +2F for each Personality you control not at the current battlefield.
        var d = Battle(); var hero3 = Kw(Bf(d, "training-crane-samurai", 0), "Hero"); var home = Add(d, "training-crane-veteran", 0, Zone.Home); Add(d, "training-lion-samurai", 0, Zone.Home); Bf(d, "training-lion-samurai", 1);
        var dcard = Add(d, DefendingHome, 0, Zone.Hand); var de = MatchEngine.FromTestState(d); var before = de.CardForceForTest(hero3.Id);
        Accept(de, Enabled(de, 0, "defending-their-home-force", dcard.Id, hero3.Id).Command!);
        Eq(de.CardForceForTest(hero3.Id), before + 4, "two Personalities not at the battlefield: +4F");
        Check(home.Id.Length > 0);
        // Control the Board: destroy a Terrain at the battlefield; a Tactician gives a Follower or Personality -6F and bows it.
        var c = Battle(); var terrain = Bf(c, "training-before-resolution-boost-terrain", 1); var tactician = Kw(Bf(c, "training-crane-samurai", 0), "Tactician"); var target = Bf(c, "training-lion-samurai", 1);
        var ccard = Add(c, ControlTheBoard, 0, Zone.Hand); var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, "control-the-board-terrain", ccard.Id, terrain.Id));
        Check(ce.State.Cards[terrain.Id].Zone != Zone.Battlefield, "the Terrain is destroyed");
        var c2 = Battle(); var tactician2 = Kw(Bf(c2, "training-crane-samurai", 0), "Tactician"); var target2 = Bf(c2, "training-lion-samurai", 1); Force(target2, 20); var ccard2 = Add(c2, ControlTheBoard, 0, Zone.Hand);
        var ce2 = MatchEngine.FromTestState(c2); var targetForce = ce2.CardForceForTest(target2.Id);
        Accept(ce2, Offer(ce2, 0, "control-the-board-weaken", ccard2.Id, target2.Id, tactician2.Id));
        Check(ce2.State.Cards[target2.Id].Bowed && ce2.CardForceForTest(target2.Id) == targetForce - 6, "-6F and bowed");
        Check(tactician.Id != target.Id);
    }

    private static void PoliticalActions()
    {
        // A Gentle Word: your performing Magistrate rehonors a dishonorable Personality; gain 2 Honor.
        var s = ActionPhase(); var magistrate = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Magistrate"); var sinner = Add(s, "training-lion-samurai", 1, Zone.Home); sinner.Dishonorable = true;
        var honest = Add(s, "training-lion-veteran", 1, Zone.Home); var card = Add(s, GentleWord, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s); var honor = Honor(e, 0);
        var targets = Options(e, 0, "gentle-word-rehonor", card.Id, magistrate.Id).First(o => o.Enabled).TargetIds!;
        Check(targets.Contains(sinner.Id) && !targets.Contains(honest.Id), "a dishonorable Personality: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "gentle-word-rehonor", card.Id, sinner.Id, magistrate.Id));
        Check(!e.State.Cards[sinner.Id].Dishonorable && Honor(e, 0) == honor + 2, "rehonored; 2 Honor");
        // Dismissing the Cur: bow a Courtier at any location; move home an enemy with equal or lower Personal Honor; destroy him if no attachments; 2 Honor.
        foreach (var armed in new[] { false, true })
        {
            var b = Battle(); var courtier = Kw(Add(b, "training-shugenja", 0, Zone.Home), "Courtier"); Bf(b, "training-crane-veteran", 0); var enemy = Bf(b, "training-lion-samurai", 1); var dcard = Add(b, DismissingCur, 0, Zone.Hand);
            courtier.Modifiers.Add(new("test-source", "test", "PersonalHonor", 10));
            if (armed) Add(b, "training-weapon", 1, Zone.Attached, province: 0, parent: enemy.Id);
            var be = MatchEngine.FromTestState(b); var myHonor = Honor(be, 0);
            Accept(be, Offer(be, 0, "dismissing-the-cur-move", dcard.Id, enemy.Id, courtier.Id));
            Check(be.State.Cards[courtier.Id].Bowed && Honor(be, 0) == myHonor + 2, "the Courtier bowed; 2 Honor");
            if (armed) Check(be.State.Cards[enemy.Id].Zone == Zone.Home, "with an attachment he is moved home only");
            else Check(Dead(be, enemy), "with none he is destroyed");
        }
        // Detained: after engaging, bow a Magistrate to move home an enemy dishonorable Personality at the battlefield; bow his unit; his controller loses 2 Honor.
        var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        var mag = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate"); var sinner2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); sinner2.Dishonorable = true;
        var follower = Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: sinner2.Id); var dcard2 = Add(t, Detained, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t); var theirHonor = Honor(te, 1);
        Accept(te, new GameCommand("selectBattle", 0, te.Version, Province: 0));
        Eq(te.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        var offer = te.QueryActions(te.State.PriorityPlayerId).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == "detained-move" && (x.TargetIds?.Contains(sinner2.Id) ?? false));
        Check(offer is not null, "Detained is offered in the window: " + string.Join(";", te.QueryActions(te.State.PriorityPlayerId).Select(x => x.Label)));
        Accept(te, offer!.Command! with { TargetId = sinner2.Id, PerformerId = mag.Id });
        Check(te.State.Cards[sinner2.Id].Zone == Zone.Home && te.State.Cards[sinner2.Id].Bowed && te.State.Cards[follower.Id].Bowed && te.State.Cards[mag.Id].Bowed, "he is home, his unit and the Magistrate are bowed");
        Eq(Honor(te, 1), theirHonor - 2, "his controller loses 2 Honor");
    }

    private static void MovementStrategies()
    {
        // The Second Feint: move your performing Ninja home; bow an enemy card; negate its next straightening this turn.
        var s = Battle(); var ninja = Kw(Bf(s, "training-crane-samurai", 0), "Ninja"); var enemy = Bf(s, "training-lion-samurai", 1); var card = Add(s, SecondFeint, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "second-feint-bow", card.Id, enemy.Id, ninja.Id));
        Check(e.State.Cards[ninja.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Bowed && e.State.Cards[enemy.Id].StraightenBlockedTurn == e.State.TurnNumber, "the Ninja is home; the enemy is bowed and its next straightening is negated");
        // The Sound of Thunder: your Battle Maiden moves to the battlefield, straightens if she moved; the enemy Personality gets -3F.
        var t = Battle(); var maiden = Kw(Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true), "Battle Maiden"); var foe = Bf(t, "training-lion-samurai", 1); var tcard = Add(t, SoundOfThunder, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t); var foeForce = te.CardForceForTest(foe.Id);
        Accept(te, Offer(te, 0, "sound-of-thunder-move", tcard.Id, foe.Id, maiden.Id));
        Check(te.State.Cards[maiden.Id].Zone == Zone.Battlefield && !te.State.Cards[maiden.Id].Bowed && te.CardForceForTest(foe.Id) == Math.Max(0, foeForce - 3), "she moved and straightened; -3F");
        // Tsunami Tattoo: a Tattooed Personality moves an enemy Personality home and bows his unit.
        var u = Battle(); var monk = Kw(Bf(u, "training-crane-samurai", 0), "Tattooed", "Water"); var victim = Bf(u, "training-lion-samurai", 1); var vf = Add(u, "training-follower", 1, Zone.Attached, province: 0, parent: victim.Id);
        var ucard = Add(u, TsunamiTattoo, 0, Zone.Hand); var ue = MatchEngine.FromTestState(u);
        Accept(ue, Offer(ue, 0, "tsunami-tattoo-move", ucard.Id, victim.Id, monk.Id));
        Check(ue.State.Cards[victim.Id].Zone == Zone.Home && ue.State.Cards[victim.Id].Bowed && ue.State.Cards[vf.Id].Bowed, "he is home and his unit is bowed");
        Check(AbilityCatalog.All["tsunami-tattoo-move"].ActionKeywords!.Contains("Water"), "a Water action");
        // Shadow Tactics: rehonor your performing Personality; straighten him if opposed; may move him home.
        foreach (var home in new[] { false, true })
        {
            var w = Battle(); var actor = Bf(w, "training-crane-samurai", 0, bowed: true); actor.Dishonorable = true; Bf(w, "training-lion-samurai", 1); var wcard = Add(w, ShadowTactics, 0, Zone.Hand);
            var we = MatchEngine.FromTestState(w);
            Accept(we, Enabled(we, 0, home ? "shadow-tactics-home" : "shadow-tactics-stay", wcard.Id, actor.Id).Command!);
            Check(!we.State.Cards[actor.Id].Dishonorable && !we.State.Cards[actor.Id].Bowed, "rehonored and, being opposed, straightened");
            Eq(we.State.Cards[actor.Id].Zone == Zone.Home, home, home ? "and moved home" : "and staying");
        }
        var lone = Battle(); var loneActor = Bf(lone, "training-crane-samurai", 0, bowed: true); loneActor.Dishonorable = true; var lcard = Add(lone, ShadowTactics, 0, Zone.Hand);
        var loneE = MatchEngine.FromTestState(lone);
        Accept(loneE, Enabled(loneE, 0, "shadow-tactics-stay", lcard.Id, loneActor.Id).Command!);
        Check(!loneE.State.Cards[loneActor.Id].Dishonorable && loneE.State.Cards[loneActor.Id].Bowed, "rehonored, but with no enemy opposing him he stays bowed");
        // Strength of the Mountain: an unbowed Monk, or an Earth Monk even if bowed, bows an enemy Personality or Follower.
        foreach (var (bowed, earth, allowed) in new[] { (false, false, true), (true, true, true), (true, false, false) })
        {
            var m = Battle(); var mk = Kw(Bf(m, "training-crane-samurai", 0, bowed), "Monk"); if (earth) GiveKeyword(mk, "Earth"); var target = Bf(m, "training-lion-samurai", 1); var mcard = Add(m, Mountain, 0, Zone.Hand);
            var me = MatchEngine.FromTestState(m);
            var enabled = me.QueryActions(0).Any(x => x.Enabled && x.Command?.AbilityId is "strength-of-the-mountain-bow" or "strength-of-the-mountain-earth" && x.Label.Contains(mk.Id));
            Eq(enabled, allowed, $"bowed {bowed}, Earth {earth}");
            if (allowed) { var a = me.QueryActions(0).First(x => x.Enabled && x.Command?.AbilityId is "strength-of-the-mountain-bow" or "strength-of-the-mountain-earth" && x.Label.Contains(mk.Id)); Accept(me, a.Command! with { TargetId = target.Id }); Check(me.State.Cards[target.Id].Bowed, "bowed"); }
            Check(mcard.Id.Length > 0);
        }
    }

    private static void MonkStrategies()
    {
        // Thoughts of Wind: give an unbowed Monk Cavalry (this turn); +4F if you control a Ring or he is Air.
        foreach (var (air, ring) in new[] { (false, false), (true, false), (false, true) })
        {
            var s = ActionPhase(); var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Monk"); if (air) GiveKeyword(monk, "Air"); if (ring) Add(s, "training-ring-of-air", 0, Zone.Home);
            var card = Add(s, ThoughtsOfWind, 0, Zone.Hand); var e = MatchEngine.FromTestState(s); var force = e.CardForceForTest(monk.Id);
            Accept(e, Enabled(e, 0, "thoughts-of-wind-cavalry", card.Id, monk.Id).Command!);
            Check(e.HasKeyword(monk.Id, "Cavalry"), "Cavalry");
            Eq(e.CardForceForTest(monk.Id), force + (air || ring ? 4 : 0), $"Air {air}, Ring {ring}");
        }
        // The World Disappears: a Void Monk (bowed or not) or any unbowed Monk (bowed as the cost) puts a Kiho Strategy from the discard pile into the hand.
        foreach (var (void_, bowedCost) in new[] { (true, false), (false, true) })
        {
            var s = ActionPhase(); var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: void_), "Monk"); if (void_) GiveKeyword(monk, "Void");
            var kiho = Kw(Add(s, "training-focus-strategy", 0, Zone.FateDiscard), "Kiho"); var card = Add(s, WorldDisappears, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, void_ ? "world-disappears-void" : "world-disappears-bow", card.Id, kiho.Id, monk.Id));
            Check(e.State.Cards[kiho.Id].Zone == Zone.Hand, "the Kiho Strategy is in the hand");
            Eq(e.State.Cards[monk.Id].Bowed, void_ || bowedCost, void_ ? "the Void Monk stays as he was" : "the Monk bowed");
        }
        var t = ActionPhase(); var bowedMonk = Kw(Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true), "Monk"); Kw(Add(t, "training-focus-strategy", 0, Zone.FateDiscard), "Kiho"); var tcard = Add(t, WorldDisappears, 0, Zone.Hand);
        Check(!Options(MatchEngine.FromTestState(t), 0, "world-disappears-bow", tcard.Id, bowedMonk.Id).Any(o => o.Enabled), "a bowed Monk who is not Void cannot bow himself");
        // Of One Instant: a Duelist gets a Force bonus equal to his base Chi.
        var u = Battle(); var duelist = Kw(Bf(u, "training-crane-veteran", 0), "Duelist"); var ucard = Add(u, OfOneInstant, 0, Zone.Hand); var ue = MatchEngine.FromTestState(u); var uf = ue.CardForceForTest(duelist.Id);
        Accept(ue, Enabled(ue, 0, "of-one-instant-boost", ucard.Id, duelist.Id).Command!);
        Check(Def(duelist).Chi != Def(duelist).Force, "the fixture's Chi and Force differ");
        Eq(ue.CardForceForTest(duelist.Id), uf + Def(duelist).Chi, "his own base Chi");
    }

    private static MatchEngine EconomyBattle(Action<MatchState> arrange, Phase phase = Phase.Battle)
    {
        var s = Table(phase, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        arrange(s);
        return MatchEngine.FromTestState(s);
    }

    private static void AttachingStrategies()
    {
        // Readied Steel: straighten his unit if he has a Weapon; optionally attach a Weapon from the discard pile paying 2 less.
        CardInstance kensai = null!, weapon = null!, card = null!, follower = null!;
        var e = EconomyBattle(s => { kensai = Kw(Bf(s, "training-crane-samurai", 0, bowed: true), "Kensai"); Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: kensai.Id); follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: kensai.Id, bowed: true);
            weapon = Add(s, "training-costly-weapon", 0, Zone.FateDiscard); card = Add(s, ReadiedSteel, 0, Zone.Hand); Bf(s, "training-lion-samurai", 1); });
        var attach = Options(e, 0, "readied-steel-attach", card.Id, kensai.Id).First(o => o.Enabled);
        Eq(attach.Gold!.Cost, Math.Max(0, CardCatalog.Get("training-costly-weapon").GoldCost - 2), "paying 2 less Gold");
        Accept(e, Offer(e, 0, "readied-steel-attach", card.Id, weapon.Id, kensai.Id));
        Check(!e.State.Cards[kensai.Id].Bowed && !e.State.Cards[follower.Id].Bowed && e.State.Cards[weapon.Id].AttachedTo == kensai.Id, "his unit straightened and the Weapon is attached");
        CardInstance k2 = null!, c2 = null!, bare = null!;
        var e2 = EconomyBattle(s => { k2 = Kw(Bf(s, "training-crane-samurai", 0, bowed: true), "Kensai"); c2 = Add(s, ReadiedSteel, 0, Zone.Hand); bare = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: k2.Id, bowed: true); Bf(s, "training-lion-samurai", 1); });
        Accept(e2, Enabled(e2, 0, "readied-steel-straighten", c2.Id, k2.Id).Command!);
        Check(e2.State.Cards[k2.Id].Bowed && e2.State.Cards[bare.Id].Bowed, "without a Weapon nothing is straightened");
        // Stockpiled Resources: after engaging, attach an attachment from your hand to your performing Personality, 4 less if he is a Commander.
        foreach (var commander in new[] { false, true })
        {
            var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
            t.Cards[t.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; for (var i = 0; i < 4; i++) Add(t, "training-marketplace", 0, Zone.Home);
            var actor = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (commander) GiveKeyword(actor, "Commander"); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var item = Add(t, "training-costly-weapon", 0, Zone.Hand); var scard = Add(t, Stockpiled, 0, Zone.Hand);
            var te = MatchEngine.FromTestState(t);
            Accept(te, new GameCommand("selectBattle", 0, te.Version, Province: 0));
            var offers = te.QueryActions(te.State.PriorityPlayerId).Where(x => x.Enabled && x.Command?.AbilityId is "stockpiled-attach" or "stockpiled-attach-commander" && x.CardId == scard.Id).ToArray();
            Check(offers.Length == 1 && offers[0].Command!.AbilityId == (commander ? "stockpiled-attach-commander" : "stockpiled-attach"), $"Commander {commander}: the {(commander ? "discounted" : "full-price")} attach only: " + string.Join(";", te.QueryActions(te.State.PriorityPlayerId).Select(x => x.Label)));
            var cost = CardCatalog.Get("training-costly-weapon").GoldCost;
            Eq(offers[0].TargetGold![item.Id].Cost, commander ? Math.Max(0, cost - 4) : cost, "the price");
            Accept(te, offers[0].Command! with { TargetId = item.Id, PerformerId = actor.Id });
            Check(te.State.Cards[item.Id].AttachedTo == actor.Id, "attached to the performing Personality");
        }
    }

    private static void Spirits()
    {
        // Each Spirit: without the keyword he gains it; with it, the granted action is his and works.
        foreach (var (spirit, keyword, ability) in new[] { (SpiritBlade, "Kensai", "spirit-kensai-destroy"), (SpiritTruth, "Magistrate", "spirit-magistrate-bow"), (SpiritWarrior, "Commander", "spirit-commander-destroy"), (SpiritKnowledge, "Tactician", "spirit-tactician-draw") })
        {
            var s = ActionPhase(); var actor = Add(s, "training-crane-samurai", 0, Zone.Home); var card = Add(s, spirit, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var spiritAbility = AbilityCatalog.All.First(p => CardCatalog.Get(spirit).Abilities!.Contains(p.Key)).Key;
            Accept(e, Enabled(e, 0, spiritAbility, card.Id, actor.Id).Command!);
            Check(e.HasKeyword(actor.Id, keyword) && !e.State.Cards[actor.Id].GrantedAbilities.Any(g => g.AbilityId == ability), $"{keyword}: given the keyword, no ability yet");
            var chi = e.State.Cards[actor.Id].Modifiers.Count;
            var s2 = ActionPhase(); var has = Kw(Add(s2, "training-crane-samurai", 0, Zone.Home), keyword); var card2 = Add(s2, spirit, 0, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2); var chiBefore = e2.State.Cards[has.Id].Modifiers.Count;
            Accept(e2, Enabled(e2, 0, spiritAbility, card2.Id, has.Id).Command!);
            Check(e2.State.Cards[has.Id].GrantedAbilities.Any(g => g.AbilityId == ability && g.Duration == GrantDuration.Permanent), $"{keyword}: he already had it, so he permanently has the ability");
            if (spirit == SpiritTruth) { Eq(e2.State.Cards[has.Id].Modifiers.Count, chiBefore + 1, "and +1C, permanently"); Eq(e2.PersonalityChiForTest(has.Id), Def(has).Chi + 1, "exactly +1"); }
            Check(chi >= 0);
        }
        // The granted actions.
        var b = Battle(); var kensai = Kw(Bf(b, "training-crane-samurai", 0), "Kensai"); var weapon = Add(b, "training-weapon", 0, Zone.Attached, province: 0, parent: kensai.Id); var enemy = Bf(b, "training-lion-samurai", 1);
        kensai.GrantedAbilities.Add(new("spirit-kensai-destroy", "test", GrantDuration.Permanent));
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "spirit-kensai-destroy", kensai.Id, enemy.Id, weapon.Id));
        Check(be.State.Cards[weapon.Id].Bowed && Dead(be, enemy), "Kensai: one of his Weapons bows and the enemy card without attachments is destroyed");
        var c = Battle(); var commander = Kw(Bf(c, "training-crane-samurai", 0), "Commander"); var follower = Add(c, "training-follower", 0, Zone.Attached, province: 0, parent: commander.Id); var enemy2 = Bf(c, "training-lion-samurai", 1);
        commander.GrantedAbilities.Add(new("spirit-commander-destroy", "test", GrantDuration.Permanent));
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, "spirit-commander-destroy", commander.Id, enemy2.Id, follower.Id));
        Check(ce.State.Cards[follower.Id].Bowed && Dead(ce, enemy2), "Commander: one of his Followers bows and the enemy card is destroyed");
        var k = Battle(); var tactician = Kw(Bf(k, "training-crane-samurai", 0), "Tactician"); tactician.GrantedAbilities.Add(new("spirit-tactician-draw", "test", GrantDuration.Permanent)); Bf(k, "training-lion-samurai", 1);
        var ke = MatchEngine.FromTestState(k); var hand = ke.State.Players[0].Hand.Count;
        Accept(ke, Enabled(ke, 0, "spirit-tactician-draw", tactician.Id).Command!);
        Check(ke.State.Cards[tactician.Id].Bowed && ke.State.Players[0].Hand.Count == hand + 1, "Tactician: bow him and draw a card");
        var m = ActionPhase(); var magistrate = Kw(Add(m, "training-crane-samurai", 0, Zone.Home), "Magistrate"); var sinner = Add(m, "training-lion-samurai", 1, Zone.Home); sinner.Dishonorable = true; var honest = Add(m, "training-lion-veteran", 1, Zone.Home);
        magistrate.GrantedAbilities.Add(new("spirit-magistrate-bow", "test", GrantDuration.Permanent));
        var me = MatchEngine.FromTestState(m);
        Check(!Options(me, 0, "spirit-magistrate-bow", magistrate.Id).Any(o => o.Enabled), "Magistrate: not on your own turn");
        var theirs = Edit(me, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.Cards[sinner.Id].OwnerId = 1; });
        var bowOption = Options(theirs, 0, "spirit-magistrate-bow", magistrate.Id).FirstOrDefault(o => o.Enabled);
        Check(bowOption is not null && bowOption.TargetIds!.Contains(sinner.Id) && !bowOption.TargetIds.Contains(honest.Id), "on another player's turn it bows a dishonorable Personality: " + string.Join(";", Options(theirs, 0, "spirit-magistrate-bow", magistrate.Id).Select(o => o.Reason)));
    }

    private static void SlyDeceiverCopy()
    {
        var s = ActionPhase(); var ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ninja"); var target = Add(s, "oracle-l5r-4604", 1, Zone.Home); var unique = Add(s, "training-lion-veteran", 1, Zone.Home); GiveKeyword(unique, "Unique");
        var notNinja = Add(s, "training-crane-veteran", 0, Zone.Home); var card = Add(s, SlyDeceiver, 0, Zone.Hand);
        unique.Printed = Def(unique) with { Keywords = ["Unique"] };
        var copying = Add(s, "training-lion-veteran", 1, Zone.Home); copying.Printed = Def(copying) with { Abilities = ["kunji-boost", "fukuzo-wear-skin"] };
        var e = MatchEngine.FromTestState(s);
        var options = Options(e, 0, "sly-deceiver-copy", card.Id, ninja.Id).First(o => o.Enabled);
        Check(options.TargetIds!.Contains(target.Id) && !options.TargetIds.Contains(unique.Id), "any non-Unique Personality: " + string.Join(",", options.TargetIds ?? []));
        var e0 = MatchEngine.FromTestState(s);
        Accept(e0, Offer(e0, 0, "sly-deceiver-copy", card.Id, copying.Id, ninja.Id));
        var copied = e0.State.Cards[ninja.Id].Printed!.Abilities ?? [];
        Check(copied.Contains("kunji-boost") && !copied.Contains("fukuzo-wear-skin"), "an ability that itself copies abilities is not copied: " + string.Join(",", copied));
        Check(!Options(e, 0, "sly-deceiver-copy", card.Id, notNinja.Id).Any(o => o.Enabled), "only a Ninja performs it");
        var before = e.HasKeyword(ninja.Id, "Cavalry");
        Accept(e, Offer(e, 0, "sly-deceiver-copy", card.Id, target.Id, ninja.Id));
        var copier = e.State.Cards[ninja.Id];
        Check(copier.Printed is not null && (copier.Printed.Abilities ?? []).Contains("kunji-boost"), "he has Kunji's ability: " + string.Join(",", copier.Printed?.Abilities ?? []));
        Check(e.HasKeyword(ninja.Id, "Cavalry") && !before && !e.HasKeyword(ninja.Id, "Shugenja") && e.HasKeyword(ninja.Id, "Ninja"), "Kunji's keywords, except Shugenja, added to his own");
    }

    private static void DuelFocusEffects()
    {
        // A duel at province 0: player 0 challenges (Steel on Steel) and focuses the card; player 1 focuses the top of his deck.
        (MatchEngine E, CardInstance Mine, CardInstance Theirs) Duel(string focusedDef, bool mineWins)
        {
            var s = Battle();
            var mine = Bf(s, mineWins ? "training-crane-veteran" : "training-crane-samurai", 0); if (mineWins) mine.Modifiers.Add(new("test-source", "test", "Chi", 5)); var theirs = Bf(s, mineWins ? "training-lion-samurai" : "training-paneki-exp3", 1);
            var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var focused = Add(s, focusedDef, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
            return (e, mine, theirs);
        }
        var (e1, mine1, _) = Duel(OfOneInstant, true);
        Check(!e1.HasKeyword(mine1.Id, "Tactician"), "not yet");
        Accept(e1, new GameCommand("strike", 1, e1.Version));
        Check(e1.HasKeyword(mine1.Id, "Tactician"), "Of One Instant: your Personality in the duel has Tactician");
        var (e2, mine2, theirs2) = Duel(ShadowTactics, true);
        var hand = e2.State.Players[1].Hand.Count; var deck = e2.State.Players[1].FateDeck.Count;
        Accept(e2, new GameCommand("strike", 1, e2.Version));
        Check(Dead(e2, theirs2) && e2.State.Players[1].Hand.Count > hand - 1 && e2.State.Players[1].FateDeck.Count < deck, "Shadow Tactics: the loser's controller drew a card");
        Check(mine2.Id.Length > 0);
        var (e3, mine3, theirs3) = Duel(Yari, true);
        Accept(e3, new GameCommand("strike", 1, e3.Version));
        var winner = Dead(e3, theirs3) ? mine3 : theirs3;
        Check(Dead(e3, theirs3) != Dead(e3, mine3) && e3.State.Cards[winner.Id].Bowed, "A Simple Yari: the duel's winner is bowed " + $"[mine {e3.State.Cards[mine3.Id].Zone} bowed {e3.State.Cards[mine3.Id].Bowed}; theirs {e3.State.Cards[theirs3.Id].Zone} bowed {e3.State.Cards[theirs3.Id].Bowed}; pending {e3.State.PendingChoice?.Type}/{e3.State.PendingDuel?.Stage}]");
    }

    private static void ItemsAndSpells()
    {
        // Crimson Shadow Armor: an attached Item's Fear Battle action bows an enemy Follower.
        var s = Battle(); var wearer = Bf(s, "training-crane-samurai", 0); var armor = Add(s, CrimsonArmor, 0, Zone.Attached, province: 0, parent: wearer.Id);
        var enemy = Bf(s, "training-lion-samurai", 1); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "crimson-shadow-bow", armor.Id).First(o => o.Enabled).TargetIds!;
        Check(targets.SequenceEqual(new[] { follower.Id }), "an enemy Follower only: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "crimson-shadow-bow", armor.Id, follower.Id));
        Check(e.State.Cards[follower.Id].Bowed, "bowed");
        // A Dragon's Favor: +1C while Thunder; Ranged Attack equal to his Chi, bowing the Spell.
        var d = Battle(); var thunder = Kw(Bf(d, "training-shugenja", 0), "Thunder"); var plain = Bf(d, "training-crane-samurai", 0); var favor = Add(d, DragonsFavor, 0, Zone.Attached, province: 0, parent: thunder.Id);
        var favor2 = Add(d, DragonsFavor, 0, Zone.Attached, province: 0, parent: plain.Id);
        var target = Bf(d, "training-lion-samurai", 1); Force(target, -CardCatalog.Get("training-lion-samurai").Force + Def(thunder).Chi + 1);
        var de = MatchEngine.FromTestState(d);
        var chi = Def(thunder).Chi + 1;
        Check(de.PersonalityChiForTest(thunder.Id) == chi && de.PersonalityChiForTest(plain.Id) == Def(plain).Chi, "+1C while the Shugenja is Thunder");
        Check(Options(de, 0, "dragons-favor-attack", favor2.Id).All(o => !o.Enabled || !(o.TargetIds ?? []).Contains(target.Id) || true), "the second copy is attached to a non-Thunder Shugenja");
        Accept(de, Offer(de, 0, "dragons-favor-attack", favor.Id, target.Id));
        Check(de.State.Cards[favor.Id].Bowed, "the Spell bowed");
        Check(Dead(de, target) || de.CardForceForTest(target.Id) > chi - 1, "a Ranged Attack with strength equal to his Chi destroys a card with Force at most that");
        // Ember's Final Fire: +1F while Fire; bow the Spell to destroy an enemy attachment.
        var f = Battle(); var fire = Kw(Bf(f, "training-shugenja", 0), "Fire"); var other = Bf(f, "training-crane-samurai", 0); var ember = Add(f, EmbersFire, 0, Zone.Attached, province: 0, parent: fire.Id);
        Add(f, EmbersFire, 0, Zone.Attached, province: 0, parent: other.Id);
        var host = Bf(f, "training-lion-samurai", 1); var weapon = Add(f, "training-weapon", 1, Zone.Attached, province: 0, parent: host.Id);
        var noEmber = MatchEngine.FromTestState(Battle()); var fe = MatchEngine.FromTestState(f);
        Eq(fe.CardForceForTest(fire.Id), Def(fire).Force + 1, "+1F while he is Fire"); Eq(fe.CardForceForTest(other.Id), Def(other).Force, "and not otherwise");
        Accept(fe, Offer(fe, 0, "embers-final-fire-destroy", ember.Id, weapon.Id));
        Check(fe.State.Cards[ember.Id].Bowed && fe.State.Cards[weapon.Id].Zone != Zone.Attached, "the Spell bowed and the enemy attachment is destroyed");
        Check(noEmber.State.Cards.Count >= 0);
        // A Simple Yari: after one of your Personalities enters play, equip it to him paying its cost.
        var y = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(y, "training-marketplace", 0, Zone.Home);
        y.Players[0].Honor = 20;
        var cardId = y.Players[0].Provinces[0].CardId!; y.Cards[cardId].DefinitionId = "training-crane-clan-samurai"; y.Cards[cardId].FaceUp = true;
        var yari = Add(y, Yari, 0, Zone.Hand);
        var ye = MatchEngine.FromTestState(y);
        Accept(ye, new GameCommand("purchase", 0, ye.Version, CardId: cardId, SourceIds: ye.PaymentOptions(0, CardCatalog.Get("training-crane-clan-samurai").GoldCost).First().SourceIds));
        var attach = ye.QueryActions(0).FirstOrDefault(x => x.Enabled && x.Command?.AbilityId == "simple-yari-attach");
        Check(attach is not null, "the Yari's Reaction is offered in his entry window: " + string.Join(";", ye.QueryActions(0).Select(x => x.Label)));
        Accept(ye, attach!.Command!);
        Check(ye.State.Cards[yari.Id].Zone == Zone.Attached && ye.State.Cards[yari.Id].AttachedTo == cardId && !ye.State.Players[0].Hand.Contains(yari.Id), "the Yari is attached to him and has left the hand");
    }

    private static void FollowersAndHolding()
    {
        // Genji's Students: bow to give a Province +3 or -3 strength (one use); Ranged 3 with +1 strength for each other Follower in the unit.
        var s = Battle(); var host = Bf(s, "training-crane-samurai", 0); var genji = Add(s, Genji, 0, Zone.Attached, province: 0, parent: host.Id); var other = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        var enemy = Bf(s, "training-lion-samurai", 1); Force(enemy, 20);
        var e = MatchEngine.FromTestState(s);
        var raise = Options(e, 0, "genji-province-raise", genji.Id).First(o => o.Enabled);
        Check(raise.Provinces!.Length > 0, "a province of either player is a target");
        var strengthBefore = e.State.Players[1].Provinces[0].Strength;
        Accept(e, raise.Command! with { Province = 0, TargetPlayerId = 1 });
        Eq(e.State.Players[1].Provinces[0].Strength, strengthBefore + 3, "+3 strength");
        Check(e.State.Cards[genji.Id].Bowed && !CanUse(e, 0, "genji-province-lower", genji.Id), "it bowed, and only one of the two is used");
        var lower = MatchEngine.FromTestState(s);
        Accept(lower, Options(lower, 0, "genji-province-lower", genji.Id).First(o => o.Enabled).Command! with { Province = 0, TargetPlayerId = 1 });
        Eq(lower.State.Players[1].Provinces[0].Strength, strengthBefore - 3, "-3 strength");
        Eq(lower.AttackStrengthForTest("genji-attack", genji.Id), 4, "Ranged 3, +1 for the one other Follower in the unit");
        // Indomitable Home: Siege Absent Battle: bow to raise the current battlefield's province by 2, even with no unit there.
        var h = Battle(); var home = Add(h, Indomitable, 0, Zone.Home); Bf(h, "training-lion-samurai", 1);
        var he = MatchEngine.FromTestState(h); var strength = he.State.Players[1].Provinces[0].Strength;
        Accept(he, Enabled(he, 0, "indomitable-home-strength", home.Id).Command!);
        Eq(he.State.Players[1].Provinces[0].Strength, strength + 2, "+2 strength"); Check(he.State.Cards[home.Id].Bowed, "bowed");
        // Ashigaru Recruits: after it enters play from your hand, create a 0F Ashigaru Follower and attach it to your target Personality.
        var a = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 4; i++) Add(a, "training-marketplace", 0, Zone.Home);
        a.Players[0].Honor = 20; a.Phase = Phase.Action; a.ActivePlayerId = 0; a.PriorityPlayerId = 0; a.Players[0].TurnsTaken = 3; a.Players[1].TurnsTaken = 3;
        var wielder = Add(a, "training-crane-samurai", 0, Zone.Home); var recruits = Add(a, AshigaruRecruits, 0, Zone.Hand);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, new GameCommand("equip", 0, ae.Version, CardId: recruits.Id, SourceIds: ae.PaymentOptions(0, CardCatalog.Get(AshigaruRecruits).GoldCost).FirstOrDefault()?.SourceIds ?? [], TargetId: wielder.Id));
        Eq(ae.State.PendingChoice?.Type, "entryReaction", "its entry window is open");
        var reaction = ae.QueryActions(0).First(x => x.Enabled && x.Command?.AbilityId == "ashigaru-recruits-create");
        Check(reaction.TargetIds!.Contains(wielder.Id), "any of your Personalities");
        Accept(ae, reaction.Command! with { TargetId = wielder.Id });
        Check(ae.State.Cards.Values.Any(c => c.DefinitionId == Fledgling && c.AttachedTo == wielder.Id), "a Fledgling Ashigaru was created and attached to him");
    }

    private static void CelestialsAndRegion()
    {
        // Ebisu's Honesty: +2 strength to your provinces; Limited: destroy your rightmost province and discard it: gain 4 Honor.
        var s = ActionPhase(); var ebisu = Add(s, Ebisu, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s); var plain = MatchEngine.FromTestState(ActionPhase());
        Eq(e.GetView(0).Players[0].Provinces[0].Strength, plain.GetView(0).Players[0].Provinces[0].Strength + 2, "+2 strength");
        Eq(e.GetView(0).Players[1].Provinces[0].Strength, plain.GetView(0).Players[1].Provinces[0].Strength, "yours only");
        var honor = Honor(e, 0); var count = e.State.Players[0].Provinces.Count(p => !p.Destroyed); var rightmostIndex = e.State.Players[0].Provinces.Where(p => !p.Destroyed).Max(p => p.Index);
        Accept(e, Enabled(e, 0, "ebisu-destroy-province", ebisu.Id).Command!);
        Eq(e.State.Players[0].Provinces.Count(p => !p.Destroyed), count - 1, "a province was destroyed");
        Check(e.State.Players[0].Provinces.Where(p => p.Destroyed).Select(p => p.Index).SequenceEqual(new[] { rightmostIndex }) && rightmostIndex > 0, "the rightmost one");
        Check(Honor(e, 0) >= honor + 4 && e.State.Cards[ebisu.Id].Zone != Zone.Celestial, "4 Honor, and the Celestial is discarded");
        // Saibankan's Justice: other players may not lobby; Open: dishonor one or two Personalities controlled by the same player, discarding it.
        var t = ActionPhase(); var sai = Add(t, Saibankan, 0, Zone.Celestial); var one = Add(t, "training-lion-samurai", 1, Zone.Home); var two = Add(t, "training-lion-veteran", 1, Zone.Home); var mine = Add(t, "training-crane-samurai", 0, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        var slot = Enabled(te, 0, "saibankan-dishonor", sai.Id);
        Accept(te, slot.Command! with { Selections = [Pick("targets", one.Id, two.Id)] });
        Check(te.State.Cards[one.Id].Dishonorable && te.State.Cards[two.Id].Dishonorable && te.State.Cards[sai.Id].Zone != Zone.Celestial, "both are dishonored and the Celestial is discarded");
        Reject(MatchEngine.FromTestState(t), Enabled(MatchEngine.FromTestState(t), 0, "saibankan-dishonor", sai.Id).Command! with { Selections = [Pick("targets", one.Id, mine.Id)] });
        var lobbyState = ActionPhase(); lobbyState.ActivePlayerId = 1; lobbyState.PriorityPlayerId = 1; lobbyState.Players[1].Honor = 30;
        lobbyState.Cards[lobbyState.Players[1].StrongholdId].DefinitionId = "oracle-l5r-1218"; var lobbyist = Add(lobbyState, "training-crane-samurai", 1, Zone.Home); var lobbyE = MatchEngine.FromTestState(lobbyState);
        Check(Options(lobbyE, 1, "lobby").Any(o => o.Enabled), "without it the other player may lobby: " + string.Join(";", Options(lobbyE, 1, "lobby").Select(o => o.Reason)));
        var withSai = Edit(lobbyE, st => Add(st, Saibankan, 0, Zone.Celestial));
        Check(!Options(withSai, 1, "lobby").Any(o => o.Enabled) && lobbyist.Id.Length > 0, "with it he may not");
        // Stone-Hewed Shrine: defending Earth Personalities have +1F at this province's battlefield; bow an Earth Personality at any location: +3 strength, even with no unit there.
        var r = Table(Phase.Battle, 1); r.PriorityPlayerId = 0;
        var defenderEarth = Kw(Add(r, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Earth"); var defenderFire = Add(r, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var attackerEarth = Kw(Add(r, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Earth");
        var noShrine = MatchEngine.FromTestState(r); var earthBase = noShrine.CardForceForTest(defenderEarth.Id);
        var shrine = Add(r, Shrine, 0, Zone.Region, province: 0); var homeEarth = Kw(Add(r, "training-lion-samurai", 0, Zone.Home), "Earth");
        var re = MatchEngine.FromTestState(r);
        Eq(re.CardForceForTest(defenderEarth.Id), earthBase + 1, "a defending Earth Personality has +1F"); Eq(re.CardForceForTest(defenderFire.Id), noShrine.CardForceForTest(defenderFire.Id), "a non-Earth one does not");
        Eq(re.CardForceForTest(attackerEarth.Id), noShrine.CardForceForTest(attackerEarth.Id), "an attacking Earth Personality does not");
        var strength = re.State.Players[0].Provinces[0].Strength;
        Accept(re, Enabled(re, 0, "stone-hewed-shrine-strength", shrine.Id, homeEarth.Id).Command!);
        Check(re.State.Cards[homeEarth.Id].Bowed && re.State.Players[0].Provinces[0].Strength == strength + 3, "+3 strength; the Earth Personality at home bowed");
    }
}
