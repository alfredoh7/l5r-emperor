using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, pass group P2 (search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Every position is built explicitly and driven through QueryActions and the commands it
// offers (the primitives are in src/L5R.Core/EmperorP2*.cs).
internal static partial class EmperorP2Tests
{
    private const string Kazutoshi = "oracle-l5r-820", Yamahatsu = "oracle-l5r-3660", Nakano = "oracle-l5r-4127", Mina = "oracle-l5r-3861", Gonji = "oracle-l5r-4167",
        Honest = "oracle-l5r-3415", SurroundedDef = "oracle-l5r-7668", Brothers = "oracle-l5r-1164", SpiritShadows = "oracle-l5r-7427", KillingIntent = "oracle-l5r-4352",
        Champion = "oracle-l5r-6", TurningAway = "oracle-l5r-10173", Delving = "oracle-l5r-1945", Tapestry = "oracle-l5r-8371", Uruwashii = "oracle-l5r-453",
        Pillowbook = "oracle-l5r-5958", Shrine = "oracle-l5r-7227", Hostility = "oracle-l5r-2911", Advantage = "oracle-l5r-2759", Agony = "oracle-l5r-7954", Wall = "oracle-l5r-8298",
        Ichizo = "oracle-l5r-5104", Keirei = "oracle-l5r-9845";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand[] Actions(MatchEngine e, int player, string type) => e.QueryActions(player).Where(a => a.Type == type && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static bool Rejected(MatchEngine e, GameCommand c) => !e.Execute(c).Accepted;
    private static bool InPlayZone(MatchEngine e, string id) => e.State.Cards[id].Zone is Zone.Home or Zone.Battlefield or Zone.Attached;

    // Passes for whoever holds a reaction window the last command opened.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is not null && guard < 10; guard++)
        {
            var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction");
            if (pass is null) return;
            Accept(e, pass.Command!);
        }
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-p2", [
        ("Published shape: the P2 cards print their scans' stats, keywords, text and ids, ship their art and are in their manifests", PublishedShape),
        ("Honest Assessment: an unbowed Samurai bows an enemy Personality with equal or lower Personal Honor; 3 Honor at Family Honor 9 or less, else 1; a higher-Honor enemy is not a target", HonestAssessment),
        ("Armor of the Uruwashii: Melee Attacks targeting cards of its unit have -2 strength (Ranged ones do not); its Reaction negates the wielder's movement from another player's action, and is not offered for one that does not move him", ArmorOfTheUruwashii),
        ("Ikoma Yamahatsu: Ranged 4 Attack; while his controller controls a Terrain every Ranged Attack of his has +1 strength (not a Melee one, not the other player's)", IkomaYamahatsu),
        ("Surrounded!: after engaging, with an unbowed Scout at the battlefield, Reconnaissance there and +2 to the player's Ranged Attacks for this battle only; without a Scout it is not offered", Surrounded),
        ("Shrine to Nimuro: 2 Gold, or 3 with a Tactician; bowing it and a Lion Clan Tactician lowers any province's strength to 0 until the end of the turn; a non-Lion Tactician cannot perform it", ShrineToNimuro),
        ("Bayushi Kazutoshi: other players' Personalities have their Honor Requirements raised by 10 (Clan allegiance still ignores them), his own player's are not; he discards his player's other Sensei as he enters play", BayushiKazutoshi),
        ("Kakita Gonji: once per duel, instead of focusing, +4 to his player's Crane Clan duellist's stat; counted against the four-focus cap; not offered for a non-Crane duellist or a second time", KakitaGonji),
        ("Kaiu Nakano: +3F while he is in an army of Siege Personalities only (re-judged live), for the turn; a Tactical action", KaiuNakano),
        ("Brothers in Battle: two or three Samurai each have +1F and a Ranged 4 Attack only while in an army with another of them; one alone has neither", BrothersInBattle),
        ("Growing Hostility: destroys every non-Unique Holding with a base Gold Cost of 0 (not a Unique one) and from then on every Holding costs at least 1", GrowingHostility),
        ("Growing Hostility, revealed: the Event resolves from its province and is discarded", GrowingHostilityReveal),
        ("Killing Intent, Focus Effect: asked before the totals; yes makes his base Force his duel stat, declining keeps Chi; at home the option is not offered", KillingIntentFocus),
        ("Killing Intent, Battle: straighten the performer, and move him home or not (two ways sharing one use); another player's Personality cannot perform it", KillingIntentBattle),
        ("A Champion in Court, Focus Effect: a Yojimbo of his takes his Personality's place in the duel (and its outcome); with no Yojimbo nothing is asked", ChampionInCourt),
        ("A Champion in Court, Reaction: after another player's action destroyed his Courtier, a Yojimbo gets three +1F Revenge tokens; not after a non-Courtier's destruction", ChampionRevenge),
        ("Turning Away, Focus Effect: his Personality is discarded and the duel ends without resolution (no loser, focused cards spent); declined, the duel resolves", TurningAwayFocus),
        ("Turning Away, Battle: Absent, a bowed Monk at home and a bowed-as-cost Ring give the current battlefield's province -3; with no unbowed Ring it is not offered", TurningAwayBattle),
        ("Spirit of the Shadows: a non-Ninja gains Ninja, a Ninja gains the switch; two Personalities of one player at different locations swap (same location or different players refused)", SpiritOfTheShadows),
        ("Isawa Mina: destroys an attachment or a token at the battlefield and gets two +1F Fire tokens; an attachment elsewhere is not offered, a Personality is not an attachment", IsawaMina),
        ("Delving into History: bow the Shugenja and destroy it; a non-Unique Spell sharing a keyword with him (any with a Void performer); a Unique one never", DelvingIntoHistory),
        ("The Tapestry Perceived: +1 hand size while its Shugenja is Void; look at any Fate deck's top card, destroy it to discard the card, or keep both", TapestryPerceived),
        ("Gaining Advantage: may search for a non-Unique Strategy with a Political ability (its own tag or its card's keyword), then discard a card; may decline", GainingAdvantage),
        ("The Agony of Her Gaze: stays in play; after a Battle action its owner's Nonhuman performer gets a +1F Frenzy token; a Human or the other player's Nonhuman does not", AgonyOfHerGaze),
        ("Pillowbook: +1 to its player's maximum hand size, -1 to the other player's down to 7", PillowbookHandSizes),
        ("Pillowbook: another player's card effect (Times of Strife) does not discard from its player's hand", PillowbookImmunity),
        ("Shosuro Keirei: moves to any other battlefield of the Attack Phase (a fought one included); he may give a Personality at his new or previous location two -1F/-1C Poison tokens (0 Chi destroys); home is not a location offered", ShosuroKeirei),
        ("Shosuro Keirei's Poison: a two-stat token loses only its Force penalty to Incense-Mill-style cleansing and only its Chi penalty to Gift Armor-style purging", PoisonTokenStats),
        ("The Scorpion Wall is Finished: after two actions put cards in a player's hand this phase no more go in; its player's hand is safe from another player's action (Indecision)", ScorpionWall),
    ]);

    // ---- published shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal)), string.Join(",", ids.Order(StringComparer.Ordinal)), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
                foreach (var ability in CardCatalog.Get(id).Abilities ?? []) Check(AbilityCatalog.All.ContainsKey(ability), $"{id}: {ability} is catalogued");
            }
        }
        Manifest("personality-wave-100", Kazutoshi, Yamahatsu, Nakano, Mina, Gonji, Keirei);
        Manifest("strategy-wave-86", Honest, SurroundedDef, Brothers, SpiritShadows, KillingIntent, Champion, TurningAway);
        Manifest("spell-wave-30", Delving, Tapestry);
        Manifest("item-wave-22", Uruwashii, Pillowbook);
        Manifest("holding-wave-33", Shrine);
        Manifest("event-wave-13", Hostility, Advantage, Agony, Wall);
        var k = CardCatalog.Get(Kazutoshi);
        Check(k.Type == CardType.Personality && k.Clan == "Scorpion" && k.GoldCost == 7 && k.Force == 4 && k.Chi == 3 && k.PersonalHonor == 1 && k.HonorRequirement is null, "Kazutoshi's stats");
        Eq(k.Text, "After Kazutoshi enters play: Discard all your other Sensei Personalities.\nOther players' Personalities in and out of play have their Honor Requirements increased by 10.");
        var y = CardCatalog.Get(Yamahatsu);
        Check(y.Force == 4 && y.Chi == 3 && y.GoldCost == 7 && y.PersonalHonor == 2 && y.HonorRequirement == 4, "Yamahatsu's stats");
        Eq(string.Join(",", y.Abilities ?? []), "p2-yamahatsu-ranged");
        var n = CardCatalog.Get(Nakano); Check(n.HonorRequirement == 3 && (n.Keywords ?? []).Contains("Siege") && (n.Keywords ?? []).Contains("Tactician"), "Nakano");
        var m = CardCatalog.Get(Mina); Check(m.GoldCost == 8 && m.Chi == 3 && m.Force == 4, "Mina");
        var g = CardCatalog.Get(Gonji); Check(g.Force == 2 && g.Chi == 5 && g.HonorRequirement == 5 && g.PersonalHonor == 3 && (g.Keywords ?? []).Contains("Duelist"), "Gonji");
        var a = CardCatalog.Get(Uruwashii); Check(a.Type == CardType.Item && a.GoldCost == 4 && a.Force == 3 && a.Focus == 2 && a.AttackDefenseKind == "Melee" && a.AttackDefenseAmount == -2 && a.AttackDefenseScope == "Unit", "Armor of the Uruwashii");
        var shrine = CardCatalog.Get(Shrine); Check(shrine.Type == CardType.Holding && shrine.GoldCost == 3 && shrine.GoldProduction == 2 && (shrine.Keywords ?? []).Contains("Unique"), "Shrine to Nimuro");
        Eq(shrine.Text, "Bow this card: Produce 2 Gold, or 3 Gold if you control a Tactician.\nLimited: Bow this card and your performing Lion Clan Tactician, and target a province: Lower its strength to 0.");
        Eq(CardCatalog.Get(Hostility).Text, "Destroy all non-Unique Holdings with a base Gold Cost of 0. Until the game ends, Holdings in and out of play have a minimum Gold Cost of 1.");
        Eq(CardCatalog.Get(Agony).Text, "Until your Events Phase two turns from now begins, after the resolution of any Battle action, give each of your Nonhumans who performed it a +1F Frenzy token.");
        Eq(CardCatalog.Get(Champion).Focus, 3); Eq(CardCatalog.Get(KillingIntent).Focus, 2); Eq(CardCatalog.Get(TurningAway).Focus, 2); Eq(CardCatalog.Get(Honest).Focus, 4);
        Eq(AbilityCatalog.All["p2-killing-intent-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["p2-champion-court-revenge"].Timing, "Reaction");
        Eq(AbilityCatalog.All["p2-nakano-force"].Timing, "BattleOrOpen"); Check(AbilityCatalog.All["p2-nakano-force"].ActionKeywords!.Contains("Tactical"), "Nakano's action is Tactical");
        Check(AbilityCatalog.All["p2-turning-away-raise"].Absent, "Turning Away's Battle action is Absent");
    }

    // ---- Honest Assessment --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Card, CardInstance Samurai, CardInstance Low, CardInstance High) HonestTable(int honor)
    {
        var s = Table(Phase.Battle, 1);
        var samurai = Add(s, "training-samurai", 1, Zone.Battlefield, province: 0); // Personal Honor 1
        var low = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);   // Personal Honor 1
        var high = Add(s, "training-lion-courtier", 0, Zone.Battlefield, province: 0); // Personal Honor 3
        var card = Add(s, Honest, 1, Zone.Hand);
        s.Players[1].Honor = honor;
        return (MatchEngine.FromTestState(s), card, samurai, low, high);
    }

    private static void HonestAssessment()
    {
        var (e, card, samurai, low, high) = HonestTable(9);
        var option = Options(e, 1, "p2-honest-assessment-bow", card.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(low.Id) && !option.TargetIds!.Contains(high.Id), "only an enemy with equal or lower Personal Honor is a target");
        Check(!option.TargetIds!.Contains(samurai.Id), "not his own Personality");
        Accept(e, Offer(e, 1, "p2-honest-assessment-bow", card.Id, low.Id, samurai.Id));
        Settle(e);
        Check(e.State.Cards[low.Id].Bowed, "bowed");
        Eq(Honor(e, 1), 12, "Family Honor 9: 3 Honor");
        var (f, card2, samurai2, low2, _) = HonestTable(10);
        Accept(f, Offer(f, 1, "p2-honest-assessment-bow", card2.Id, low2.Id, samurai2.Id));
        Settle(f);
        Eq(Honor(f, 1), 11, "Family Honor 10: 1 Honor");
        // A bowed Samurai cannot perform it.
        var bowed = Edit(f, st => { st.Cards[samurai2.Id].Bowed = true; st.Cards[card2.Id].Zone = Zone.Hand; st.Players[1].Hand.Add(card2.Id); st.PriorityPlayerId = 1; });
        Check(!CanUse(bowed, 1, "p2-honest-assessment-bow", card2.Id), "no unbowed Samurai: not offered");
    }

    // ---- Armor of the Uruwashii ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ArmorOfTheUruwashii()
    {
        // Melee -2: Daigotsu Kazuko's Melee 3 against a 1F wielder with the Armor (+3F): 4F against 1 = survives; the same Attack without the Armor would still not reach 4 -- use strengths.
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var armor = Add(s, Uruwashii, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: wielder.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(wielder.Id), 2 + 3, "+3F from the Armor");
        // AttackDefenseModifier: a Melee Attack at the Follower (a card in the unit) is 2 weaker, a Ranged one is not.
        var s2 = Table(Phase.Battle, 1);
        var w2 = Add(s2, "training-ancestor", 0, Zone.Battlefield, province: 0);
        Add(s2, Uruwashii, 0, Zone.Attached, province: 0, parent: w2.Id);
        var f2 = Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: w2.Id); // 2F
        var kazuko = Add(s2, "oracle-l5r-1734", 1, Zone.Battlefield, province: 0); // Daigotsu Kazuko: Melee 3 Attack
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "kazuko-fear-melee", kazuko.Id, f2.Id));
        Settle(e2);
        Check(InPlayZone(e2, f2.Id), "Melee 3 - 2 = 1 does not reach the 2F Follower");
        var s3 = Table(Phase.Battle, 1);
        var w3 = Add(s3, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var f3 = Add(s3, "training-follower", 0, Zone.Attached, province: 0, parent: w3.Id);
        var k3 = Add(s3, "oracle-l5r-1734", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "kazuko-fear-melee", k3.Id, f3.Id));
        Settle(e3);
        Check(!InPlayZone(e3, f3.Id), "without the Armor the Melee 3 Attack destroys the 2F Follower");
        // Ranged: Yamahatsu's Ranged 4 at the Follower of an Armored unit is not weakened.
        var s4 = Table(Phase.Battle, 1);
        var w4 = Add(s4, "training-ancestor", 0, Zone.Battlefield, province: 0);
        Add(s4, Uruwashii, 0, Zone.Attached, province: 0, parent: w4.Id);
        var f4 = Add(s4, "training-follower", 0, Zone.Attached, province: 0, parent: w4.Id);
        var y4 = Add(s4, Yamahatsu, 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "p2-yamahatsu-ranged", y4.Id, f4.Id));
        Settle(e4);
        Check(!InPlayZone(e4, f4.Id), "a Ranged Attack is not weakened by the Armor");

        // The Reaction: player 1 moves the wielder home (Favor Battle: move an attacking enemy home is player 0's; use a training mover instead).
        var r = Table(Phase.Battle, 1);
        var target = Add(r, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var rArmor = Add(r, Uruwashii, 0, Zone.Attached, province: 0, parent: target.Id);
        var mover = Add(r, "training-move-source", 1, Zone.Battlefield, province: 0);
        var re = MatchEngine.FromTestState(r);
        Accept(re, Offer(re, 1, "training-move-both-home", mover.Id, target.Id));
        Eq(re.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window opens for the Armor");
        var reaction = Actions(re, 0, "useAbility").FirstOrDefault(c => c.AbilityId == "p2-uruwashii-negate-move");
        Check(reaction is not null, "the Armor's Reaction is offered");
        Accept(re, reaction!);
        Settle(re);
        Eq(re.State.Cards[target.Id].Zone, Zone.Battlefield, "his movement was negated");
        Check(re.State.Cards[rArmor.Id].Zone == Zone.Attached, "the Armor stays");
        // An action that moves someone else: no window for the Armor.
        var n = Table(Phase.Battle, 1);
        var other = Add(n, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var nWielder = Add(n, "training-ancestor", 0, Zone.Battlefield, province: 0);
        Add(n, Uruwashii, 0, Zone.Attached, province: 0, parent: nWielder.Id);
        var nMover = Add(n, "training-move-source", 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 1, "training-move-both-home", nMover.Id, other.Id));
        Check(!Actions(ne, 0, "useAbility").Any(c => c.AbilityId == "p2-uruwashii-negate-move"), "not offered when the action does not move the wielder");
    }

    // ---- Ikoma Yamahatsu ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IkomaYamahatsu()
    {
        var s = Table(Phase.Battle, 1);
        var yamahatsu = Add(s, Yamahatsu, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.RangedStrengthForTest("p2-yamahatsu-ranged", 1, yamahatsu.Id), 4, "no Terrain: Ranged 4");
        var terrain = Edit(e, st => { var t = new CardInstance { Id = "fx-terrain", DefinitionId = "training-terrain", OwnerId = 1, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true }; st.Cards.Add(t.Id, t); });
        Eq(terrain.RangedStrengthForTest("p2-yamahatsu-ranged", 1, yamahatsu.Id), 5, "a Terrain: +1");
        Eq(terrain.RangedStrengthForTest("kazuko-fear-melee", 1, null), 3, "a Melee Attack is not raised");
        Eq(terrain.RangedStrengthForTest("shika-ranged", 0, null), 4, "the other player's Ranged Attack is not raised");
        // A 5F target: destroyed with the Terrain, survives without it.
        var withTerrain = Edit(terrain, st => { var t = new CardInstance { Id = "fx-five", DefinitionId = "training-lion-veteran", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true }; st.Cards.Add(t.Id, t); st.Cards[t.Id].Modifiers.Add(new("t", "t", "Force", -1)); });
        Accept(withTerrain, Offer(withTerrain, 1, "p2-yamahatsu-ranged", yamahatsu.Id, "fx-five"));
        Settle(withTerrain);
        Check(!InPlayZone(withTerrain, "fx-five"), "Ranged 5 destroys a 5F Personality");
        var noTerrain = Edit(e, st => { var t = new CardInstance { Id = "fx-five", DefinitionId = "training-lion-veteran", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true }; st.Cards.Add(t.Id, t); st.Cards[t.Id].Modifiers.Add(new("t", "t", "Force", -1)); });
        Accept(noTerrain, Offer(noTerrain, 1, "p2-yamahatsu-ranged", yamahatsu.Id, "fx-five"));
        Settle(noTerrain);
        Check(InPlayZone(noTerrain, "fx-five"), "Ranged 4 does not destroy a 5F Personality");
    }

    // ---- Surrounded! ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Surrounded()
    {
        var s = Table(Phase.Battle, 1);
        var scout = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var card = Add(s, SurroundedDef, 1, Zone.Hand);
        s.PendingChoice = new("engageReaction", 1, null, "After engaging: react or pass."); s.PendingEngage = new PendingEngage { ProvinceIndex = 0, Attacker = 1, Defender = 0 };
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 1, "p2-surrounded-recon", card.Id).FirstOrDefault(o => o.Enabled);
        Check(option is not null, "offered in the engage window: " + string.Join("; ", Options(e, 1, "p2-surrounded-recon", card.Id).Select(o => o.Reason)));
        Accept(e, option!.Command! with { PerformerId = scout.Id });
        Check(Logged(e, "surroundedBonus"), "resolved");
        Eq(e.RangedStrengthForTest("shika-ranged", 1, null), 6, "+2 to his Ranged Attacks this battle");
        Eq(e.RangedStrengthForTest("shika-ranged", 0, null), 4, "not the other player's");
        Eq(e.RangedStrengthForTest("kazuko-fear-melee", 1, null), 3, "not a Melee Attack");
        var next = Edit(e, st => st.BattleNumber++);
        Eq(next.RangedStrengthForTest("shika-ranged", 1, null), 4, "another battle: no bonus");
        // No Scout: not offered.
        var n = Table(Phase.Battle, 1);
        Add(n, "training-samurai", 1, Zone.Battlefield, province: 0);
        var ncard = Add(n, SurroundedDef, 1, Zone.Hand);
        n.PendingChoice = new("engageReaction", 1, null, "After engaging: react or pass."); n.PendingEngage = new PendingEngage { ProvinceIndex = 0, Attacker = 1, Defender = 0 };
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 1, "p2-surrounded-recon", ncard.Id), "no Scout: not offered");
    }

    // ---- Shrine to Nimuro ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShrineToNimuro()
    {
        var s = Table(Phase.Action, 1);
        var shrine = Add(s, Shrine, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(shrine.Id, 1), 2, "no Tactician: 2 Gold");
        var withTactician = Edit(e, st => { var t = new CardInstance { Id = "fx-tac", DefinitionId = "training-tactician", OwnerId = 1, Zone = Zone.Home, FaceUp = true }; st.Cards.Add(t.Id, t); });
        Eq(withTactician.GoldProduced(shrine.Id, 1), 3, "a Tactician: 3 Gold");
        Check(!CanUse(withTactician, 1, "p2-shrine-nimuro-lower", shrine.Id), "a Tactician without Lion Clan cannot perform it");
        var lion = Edit(withTactician, st => st.Cards["fx-tac"].GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent)));
        // Only Tacticians in play are performers: a Lion Clan Tactician in the deck, hand or discard pile is never offered.
        var outOfPlay = Edit(lion, st =>
        {
            foreach (var (id, zone) in new[] { ("fx-deck", Zone.DynastyDeck), ("fx-hand", Zone.Hand), ("fx-discard", Zone.DynastyDiscard) })
            { var t = new CardInstance { Id = id, DefinitionId = "training-tactician", OwnerId = 1, Zone = zone }; t.GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent)); st.Cards.Add(id, t); }
        });
        Check(Options(outOfPlay, 1, "p2-shrine-nimuro-lower", shrine.Id).Select(o => o.Command?.PerformerId).Distinct().SequenceEqual(["fx-tac"]), "only the Tactician in play is offered as the performer");
        var option = Options(lion, 1, "p2-shrine-nimuro-lower", shrine.Id).First(o => o.Enabled);
        Check(option.Command!.PerformerId == "fx-tac", "the Lion Clan Tactician performs it");
        var before = lion.ProvinceStrengthForTest(0, 1);
        Check(before > 0, "the province has strength");
        Accept(lion, option.Command! with { TargetPlayerId = 0, Province = 1 });
        Eq(lion.ProvinceStrengthForTest(0, 1), 0, "its strength is 0");
        Check(lion.State.Cards[shrine.Id].Bowed && lion.State.Cards["fx-tac"].Bowed, "the Shrine and the Tactician are bowed");
        Eq(StrengthNextTurn(lion, 0, 1), before, "until the end of the turn");
    }

    // ---- Bayushi Kazutoshi -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BayushiKazutoshi()
    {
        var s = Table(Phase.Dynasty, 0);
        var kazutoshi = Add(s, Kazutoshi, 1, Zone.Home);
        var own = Add(s, "training-crane-veteran", 0, Zone.Province, province: 0); // Honor Requirement 6, Crane (player 0's Clan)
        var foreign = Add(s, "training-lion-veteran", 0, Zone.Province, province: 1); // Honor Requirement 6, Lion
        s.Players[0].Provinces[0].CardId = own.Id; s.Players[0].Provinces[1].CardId = foreign.Id;
        s.Players[0].Honor = 12;
        var e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, foreign.Id).Error is not null, "Honor 12 is below 6 + 10");
        Check(e.QuotePurchase(0, own.Id, clanAllegiance: true).Error is null, "Clan allegiance still ignores the raised requirement");
        var rich = Edit(e, st => st.Players[0].Honor = 16);
        Check(rich.QuotePurchase(0, foreign.Id).Error is null, "Honor 16 meets 6 + 10");
        // His own player's Personalities are not raised.
        var mine = Table(Phase.Dynasty, 1);
        Add(mine, Kazutoshi, 1, Zone.Home);
        var lion = Add(mine, "training-lion-veteran", 1, Zone.Province, province: 0);
        mine.Players[1].Provinces[0].CardId = lion.Id; mine.Players[1].Honor = 6;
        Check(MatchEngine.FromTestState(mine).QuotePurchase(1, lion.Id).Error is null, "his own player's Personality: Honor Requirement 6");
        // Sensei: his entry discards his player's other Sensei.
        var sensei = Table(Phase.Dynasty, 1);
        var other = Add(sensei, "training-sensei-personality", 1, Zone.Home);
        var k2 = Add(sensei, Kazutoshi, 1, Zone.Province, province: 0);
        sensei.Players[1].Provinces[0].CardId = k2.Id; sensei.Players[1].Honor = 10; sensei.Players[1].Clan = "Scorpion"; // Loyal
        for (var i = 0; i < 4; i++) Add(sensei, "training-farm", 1, Zone.Home);
        var se = MatchEngine.FromTestState(sensei);
        var quote = se.QuotePurchase(1, k2.Id);
        Check(quote.Error is null, quote.Error ?? "");
        Accept(se, new GameCommand("purchase", 1, se.Version, k2.Id, SourceIds: quote.Payments.First().SourceIds));
        Settle(se);
        Eq(se.State.Cards[other.Id].Zone, Zone.DynastyDiscard, "the other Sensei is discarded ");
    }

    // ---- Kakita Gonji ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1's Ichizo challenges player 0's duellist; player 0 controls Gonji (at home).
    private static MatchEngine GonjiDuel(string duellist)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, duellist, 0, Zone.Battlefield, province: 0);
        Add(s, Gonji, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "focusing");
        return e;
    }

    private static void KakitaGonji()
    {
        var e = GonjiDuel("training-crane-samurai");
        var gonjiFocus = e.QueryActions(0).FirstOrDefault(a => a.Command?.Choice == MatchEngine.GonjiFocusChoice);
        Check(gonjiFocus is not null, "offered to the challenged player for his Crane Clan duellist");
        var duellist = e.State.PendingDuel!.ChallengedId;
        Accept(e, gonjiFocus!.Command!);
        Eq(e.State.PendingDuel!.StatBonuses[duellist], 4, "+4 to his duel stat");
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Check(!e.QueryActions(0).Any(a => a.Command?.Choice == MatchEngine.GonjiFocusChoice), "once per duel");
        // The replaced focus counts: three more focuses reach the cap.
        for (var i = 0; i < 3; i++) { Accept(e, new GameCommand("focus", 0, e.Version, Choice: "deck")); Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck")); }
        Check(!e.QueryActions(0).Any(a => a.Type == "focus" && a.Enabled), "four (one replaced) reach the cap: only Strike");
        Check(Rejected(e, new GameCommand("focus", 0, e.Version, Choice: "deck")), "a fifth focus is refused");
        // A non-Crane duellist: not offered.
        var n = GonjiDuel("training-lion-samurai");
        Check(!n.QueryActions(0).Any(a => a.Command?.Choice == MatchEngine.GonjiFocusChoice), "not for a Lion Clan duellist");
    }

    // ---- Kaiu Nakano -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KaiuNakano()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var nakano = Add(s, Nakano, 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-ancestor", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "p2-nakano-force", nakano.Id));
        Settle(e);
        Eq(e.CardForceForTest(nakano.Id), 4, "a non-Siege Personality in his army: no bonus");
        var siege = Edit(e, st => st.Cards[ally.Id].GrantedKeywords.Add(new("Siege", "test-fixture", GrantDuration.Permanent)));
        Eq(siege.CardForceForTest(nakano.Id), 7, "all Siege: +3F (judged live)");
        var home = Edit(siege, st => { st.Cards[nakano.Id].Zone = Zone.Home; st.Cards[nakano.Id].ProvinceIndex = null; });
        Eq(home.CardForceForTest(nakano.Id), 4, "not in an army: no bonus");
        var nextTurn = Edit(siege, st => st.TurnNumber++);
        Eq(nextTurn.CardForceForTest(nakano.Id), 4, "until the end of the turn");
    }

    // ---- Brothers in Battle ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BrothersInBattle()
    {
        var s = Table(Phase.Action, 1);
        var a = Add(s, "training-samurai", 1, Zone.Home);
        var b = Add(s, "training-samurai", 1, Zone.Home);
        var card = Add(s, Brothers, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "p2-brothers-in-battle", card.Id, selections: Pick("samurai", a.Id, b.Id)));
        Settle(e);
        Eq(e.CardForceForTest(a.Id), 2, "at home: not in an army, no bonus");
        Check(!e.AbilityIdsForTest(a.Id).Contains("p2-brothers-ranged-4"), "no granted Attack at home");
        var together = Edit(e, st => { foreach (var id in new[] { a.Id, b.Id }) { st.Cards[id].Zone = Zone.Battlefield; st.Cards[id].ProvinceIndex = 0; } st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(together.CardForceForTest(a.Id), 3, "in an army with the other: +1F");
        Check(together.AbilityIdsForTest(b.Id).Contains("p2-brothers-ranged-4"), "and the Ranged 4 Attack");
        var apart = Edit(together, st => { st.Cards[b.Id].Zone = Zone.Home; st.Cards[b.Id].ProvinceIndex = null; });
        Eq(apart.CardForceForTest(a.Id), 2, "the other went home: no bonus");
        Check(!apart.AbilityIdsForTest(a.Id).Contains("p2-brothers-ranged-4"), "no Attack either");
        // The granted Attack works.
        var target = Edit(together, st => { var t = new CardInstance { Id = "fx-t", DefinitionId = "training-ancestor", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true }; st.Cards.Add(t.Id, t); st.PriorityPlayerId = 1; });
        Accept(target, Offer(target, 1, "p2-brothers-ranged-4", a.Id, "fx-t"));
        Settle(target);
        Check(!InPlayZone(target, "fx-t"), "the granted Ranged 4 Attack destroyed a 2F Personality");
        // Offered the way the client sees it: in a battle (the Samurai bowed or not), with the slot's candidates and a default command that validates.
        foreach (var bowed in new[] { false, true })
        {
            var battle = Table(Phase.Battle, 1);
            var x = Add(battle, "training-samurai", 1, Zone.Battlefield, bowed: bowed, province: 0);
            var y = Add(battle, "training-samurai", 1, Zone.Battlefield, bowed: bowed, province: 0);
            var held = Add(battle, Brothers, 1, Zone.Hand);
            var inBattle = Edit(MatchEngine.FromTestState(battle), st => { st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
            var offered = Options(inBattle, 1, "p2-brothers-in-battle", held.Id);
            var why = string.Join("; ", offered.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason} {string.Join(",", o.Slots?.Single().CardIds ?? [])} gold={o.Gold?.Kind}/{o.Gold?.Error}/{o.Gold?.Payments.Length}"));            Check(offered.Any(o => o.Enabled && o.Slots?.Single().CardIds.Contains(x.Id) == true && o.Slots.Single().CardIds.Contains(y.Id)), $"bowed={bowed} offered in battle with both Samurai: {why}");
            Check(offered.First(o => o.Enabled).Slots!.Single().NotPrimary, "a performing slot is marked, so a viewer never sends its first card as the target");
            Check(Rejected(inBattle, offered.First(o => o.Enabled).Command! with { Selections = [Pick("samurai", x.Id, y.Id)], TargetId = x.Id }), "a performer sent as the target is refused");
            var result = inBattle.Execute(offered.First(o => o.Enabled).Command! with { Selections = [Pick("samurai", x.Id, y.Id)] });
            Check(result.Accepted, $"bowed={bowed} confirming both Samurai in battle: {result.Error}");
        }
        // One Samurai is not enough.
        Check(Rejected(MatchEngine.FromTestState(s), Use(MatchEngine.FromTestState(s), 1, "p2-brothers-in-battle", card.Id, selections: Pick("samurai", a.Id))), "one Samurai is refused");
    }

    // ---- Growing Hostility --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GrowingHostility()
    {
        var s = Table(Phase.Action, 1);
        var free = Add(s, "training-castle", 0, Zone.Home);
        var e0 = MatchEngine.FromTestState(s);
        var zeroCost = e0.State.Cards.Values.Where(c => c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding && CardCatalog.Get(c.DefinitionId).GoldCost == 0
            && !(CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Unique")).Select(c => c.Id).ToArray();
        var e = Edit(e0, st => { st.P2.HoldingGoldCostMinimum = false; });
        var ev = Edit(e, st => { var c = new CardInstance { Id = "fx-ev", DefinitionId = Hostility, OwnerId = 1, Zone = Zone.Province, ProvinceIndex = 0, FaceUp = true }; st.Cards.Add(c.Id, c); });
        // Resolve the Event through the reveal path.
        var resolved = Edit(ev, st => { });
        resolved.ResolveEventForTest(1, "fx-ev");
        foreach (var id in zeroCost) Check(!InPlayZone(resolved, id), $"{id} (base Gold Cost 0) destroyed");
        Check(InPlayZone(resolved, free.Id), "a Holding with a Gold Cost stays");
        Check(resolved.State.P2.HoldingGoldCostMinimum, "the minimum is in force");
        var zero = Edit(resolved, st => { var h = new CardInstance { Id = "fx-h", DefinitionId = "training-castle", OwnerId = 0, Zone = Zone.Province, ProvinceIndex = 2, FaceUp = true }; st.Cards.Add(h.Id, h); st.Cards[h.Id].Modifiers.Add(new("t", "t", "GoldCost", -5)); });
        Eq(zero.CardGoldCostForTest("fx-h"), 1, "a Holding out of play costs at least 1");
        Eq(e.CardGoldCostForTest(free.Id), 2, "before: its printed 2");
        Check(zeroCost.Length > 0, "the scenario starts with a 0-cost Holding");
    }
}
