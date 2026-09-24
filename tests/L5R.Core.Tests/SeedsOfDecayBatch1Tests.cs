using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Seeds of Decay pass (issue #453), batch 1: strategy-wave-47 (twenty Strategies), stronghold-wave-12 (The Castle of Order) and celestial-wave-5 (Megumi's Guidance).
// Every card is driven through QueryActions and the commands it offers; each new primitive (the standing redirect, the destruction shield, the Force non-contribution stamp,
// assignment while bowed, the keyword Honor-loss immunity, Wide Valley's negation, the Grappling watch) has a scenario that fails without it. Default table: Battle Phase,
// player 1 the Attacker, player 0 the Defender at his province 0.
internal static class SeedsOfDecayBatch1Tests
{
    private const string Vigil = "oracle-l5r-10139", Onslaught = "oracle-l5r-10164", Metsubushi = "oracle-l5r-10158", ShiftingTerrain = "oracle-l5r-10168", Letters = "oracle-l5r-10147";
    private const string Manipulation = "oracle-l5r-10157", Blockade = "oracle-l5r-10142", Shield = "oracle-l5r-10167", Embracing = "oracle-l5r-10146", Promotion = "oracle-l5r-10159";
    private const string Obstacle = "oracle-l5r-10177", ArrowsEye = "oracle-l5r-10172", Dismissal = "oracle-l5r-10180", Sticks = "oracle-l5r-10170", TwoStings = "oracle-l5r-10176";
    private const string WideValley = "oracle-l5r-10182", Snake = "oracle-l5r-10150", Retreat = "oracle-l5r-10169", Reinforcements = "oracle-l5r-10163", Intelligence = "oracle-l5r-10135";
    private const string Castle = "oracle-l5r-10185", Megumi = "oracle-l5r-10028", BowBeforeMyWill = "oracle-l5r-1121", SkippingThePuddle = "oracle-l5r-7285";
    private const string EnemyBow = "training-bow-at-battle", Destroyer = "training-no-attachments-destroyer", Destroy = "training-destroy-no-attachments", Bower = "training-force-seven-bower";

    public static void Run(TestRunner runner) => runner.Run("seeds-batch-1", [
        ("Published shape: the twenty Strategies, the Stronghold and the Celestial are the printed cards (stats, keywords, ability ids, Effects) and every scan is shipped", PublishedShape),
        ("Ceaseless Vigil: the standing redirect sends another player's next action targeting one of your Personalities to the Yojimbo, is spent by it, and the additional action keeps priority", CeaselessVigilRedirects),
        ("Ceaseless Vigil is spent by the next matching action even when the redirect is not legal (the original target is hit), never by your own action, and its Focus Effect straightens a chosen bowed Personality", CeaselessVigilLegalityAndFocus),
        ("Ruthless Onslaught: another player's next Battle action targeting one of your cards (a Follower too) targets the performer; a base Force 7 performer grants an additional Battle action, a weaker one does not", RuthlessOnslaught),
        ("Metsubushi: Ranged 4 by the Ninja, and the enemy leader's next Battle action must target the Ninja when it legally can -- whatever it targeted, and only once", MetsubushiMustTarget),
        ("Shifting Terrain in play: another player's action targeting one of your Personalities opens the window, one option per Personality of yours at the Terrain's battlefield the action accepts, and the action lands on the chosen one", ShiftingTerrainRedirect),
        ("The Castle of Order: Honor lost from a Ninja card you own is not lost, and once per battle another player's Battle action against your Ninja is redirected to a chosen Ninja of yours at the same location", CastleOfOrder),
        ("Exchange of Letters (1 Gold): neither the performer nor the target may perform actions this phase; Martial Manipulation: neither may be assigned to battlefields this turn", LettersAndManipulation),
        ("Defensive Blockade: an attacking Personality without attachments (or Follower) contributes no Force to its army this battle; a Personality with attachments is not a legal target", DefensiveBlockade),
        ("Shield Formation gives a defending Personality and each of his Followers +3F (not an attacking one); Unexpected Obstacle gives an enemy Personality or Follower -4F", ShieldFormationAndObstacle),
        ("Embracing Battle (a Paragon) and Military Promotion (a Tactician names any of your Personalities): he gains Elite for the turn and a bowed Personality may be assigned to attack", EmbracingBattleAndPromotion),
        ("The Arrow's Eye: Ranged 3 plus one per Follower in the target's unit; Vengeful Dismissal moves an enemy Personality with lower Chi home (equal Chi is not offered)", ArrowsEyeAndDismissal),
        ("Sticks and Stones: a Ranged 1 that destroys a Follower draws a card and grants an additional Battle action, one that destroys a Personality only draws, a miss does nothing", SticksAndStones),
        ("Two Stings: from the hand, bow a performing Personality or Follower for a Ranged 4; from the discard pile the second action is offered, resolves and removes the card from the game", TwoStingsPlays),
        ("Wide Valley: an action's destruction of a card in a unit at its battlefield is negated (a Personality's and a Follower's), while without it the card is destroyed", WideValleyNegates),
        ("Grappling the Snake: after the next action targets the Monk, his controller may negate its effects on him or bow the performer's unit after it resolves; the choice is spent, and the Battle action grants an additional action", GrapplingTheSnake),
        ("Sound the Retreat: a Kata put into play (other Kata discarded); its Battle action moves the performer home and, if he moved, straightens his unit", SoundTheRetreat),
        ("Requesting Reinforcements: bow a performing Scout to bring a face-up Scout from a province into play at the battlefield paying all costs; with Reconnaissance an additional Battle action", RequestingReinforcements),
        ("Advanced Intelligence: after a Recon Strategy's action resolves, drawn from the hand for each Recon Strategy action taken this battle; one copy per battle", AdvancedIntelligence),
        ("Megumi's Guidance: discarded to shield a performing Personality from other players' destruction for the turn (not his own controller's); the opposed army has +2 total Force, +3 defending", MegumisGuidance),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static bool InWindow(MatchEngine e) => Window(e) is "beforeActionReaction" or "attackReaction";
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static bool Offered(MatchEngine e, int player, string ability) => Listed(e, player, ability).Any(a => a.Enabled);
    private static GameCommand ReactionCommand(MatchEngine e, int player, string ability, string? targetId = null)
    {
        var enabled = Listed(e, player, ability).Where(a => a.Enabled && (targetId is null || a.Command!.TargetId == targetId)).ToArray();
        Check(enabled.Length == 1, $"{ability} for player {player}{(targetId is null ? "" : " -> " + targetId)}: expected exactly one enabled offer, got {enabled.Length} of "
            + string.Join("; ", Listed(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return enabled[0].Command!;
    }
    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; InWindow(e) && guard < 12; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(!InWindow(e), "the window did not close");
    }
    // MatchEngine.State is a copy: a card added mid-test goes through Edit.
    private static CardInstance AddLive(ref MatchEngine e, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        CardInstance added = null!;
        e = Edit(e, st => added = Add(st, def, owner, zone, bowed, province, parent));
        return added;
    }
    private static void Pass(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));
    private static bool EventLogged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    // A face-up Personality in one of the player's provinces (the card there is replaced).
    private static CardInstance PutInProvince(MatchState s, string def, int owner, int province)
    {
        var slot = s.Players[owner].Provinces[province];
        if (slot.CardId is not null) { s.Cards[slot.CardId].Zone = Zone.DynastyDiscard; s.Cards[slot.CardId].ProvinceIndex = null; }
        var card = Add(s, def, owner, Zone.Province, province: province);
        slot.CardId = card.Id;
        return card;
    }

    // ---- published shape ------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        var abilities = new Dictionary<string, string[]>
        {
            [Vigil] = ["ceaseless-vigil-focus", "ceaseless-vigil-watch"], [Onslaught] = ["ruthless-onslaught-watch"], [Metsubushi] = ["metsubushi-attack"], [ShiftingTerrain] = ["shifting-terrain-redirect"],
            [Letters] = ["exchange-of-letters-lock"], [Manipulation] = ["martial-manipulation-lock"], [Blockade] = ["defensive-blockade-negate"], [Shield] = ["shield-formation-boost"],
            [Embracing] = ["embracing-battle-focus", "embracing-battle-elite"], [Promotion] = ["military-promotion-elite"], [Obstacle] = ["unexpected-obstacle-penalty"], [ArrowsEye] = ["arrows-eye-attack"],
            [Dismissal] = ["vengeful-dismissal-move"], [Sticks] = ["sticks-and-stones-attack"], [TwoStings] = ["two-stings-attack", "two-stings-attack-from-discard"], [WideValley] = [],
            [Snake] = ["grappling-the-snake-watch"], [Retreat] = ["sound-the-retreat-enter", "sound-the-retreat-move"], [Reinforcements] = ["requesting-reinforcements-recruit"], [Intelligence] = ["advanced-intelligence-draw"],
            [Castle] = ["castle-of-order-redirect"], [Megumi] = ["megumis-guidance-shield"],
        };
        Eq(PublishedContent.StrategyWave47.Cards.Length, 20, "strategy-wave-47"); Eq(PublishedContent.StrongholdWave12.Cards.Length, 1, "stronghold-wave-12"); Eq(PublishedContent.CelestialWave5.Cards.Length, 1, "celestial-wave-5");
        foreach (var (id, expected) in abilities)
        {
            var card = CardCatalog.Get(id);
            Check(!card.ScenarioOnly && (card.Abilities ?? []).SequenceEqual(expected), $"{card.Name}: abilities {string.Join(",", card.Abilities ?? [])}");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{card.Name}: art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
            foreach (var abilityId in expected) Check(AbilityCatalog.All.ContainsKey(abilityId), abilityId);
        }
        var vigil = CardCatalog.Get(Vigil); Check(vigil.Type == CardType.Strategy && vigil.GoldCost == 0 && vigil.Focus == 4 && vigil.Name == "Ceaseless Vigil", "Ceaseless Vigil");
        Check(CardCatalog.Get(Metsubushi).Keywords!.Contains("Ninja") && CardCatalog.Get(ShiftingTerrain).Keywords!.Contains("Terrain") && CardCatalog.Get(WideValley).Keywords!.Contains("Terrain"), "keywords");
        Check(CardCatalog.Get(Manipulation).Keywords!.Contains("Control") && CardCatalog.Get(Manipulation).Keywords!.Contains("Dark Virtue") && CardCatalog.Get(Snake).Keywords!.Contains("Kiho") && CardCatalog.Get(Retreat).Keywords!.Contains("Kata"), "keywords 2");
        Eq(CardCatalog.Get(Letters).GoldCost, 1, "Exchange of Letters costs 1"); Eq(CardCatalog.Get(Sticks).Focus, 0, "Sticks and Stones Focus 0"); Eq(CardCatalog.Get(TwoStings).Focus, 1);
        Eq(CardCatalog.Get(WideValley).Effect, "wide-valley");
        var castle = CardCatalog.Get(Castle);
        Check(castle.Type == CardType.Stronghold && castle.Clan == "Scorpion" && castle.ProvinceStrength == 7 && castle.GoldProduction == 4 && castle.StartingHonor == 0 && castle.Effect == "castle-of-order", "The Castle of Order");
        var megumi = CardCatalog.Get(Megumi);
        Check(megumi.Type == CardType.Celestial && megumi.Keywords!.Contains("Unique") && megumi.Keywords.Contains("Fortune") && megumi.Effect == "megumis-guidance", "Megumi's Guidance");
        Check(AbilityCatalog.All["ceaseless-vigil-focus"].Timing == "FocusEffect" && AbilityCatalog.All["embracing-battle-focus"].Timing == "FocusEffect", "Focus Effects");
        Check(AbilityCatalog.All["two-stings-attack-from-discard"].FromDiscard && AbilityCatalog.All["castle-of-order-redirect"].Trigger == ReactionTriggers.TargetedByAction, "shapes");
        Check(AbilityCatalog.All["requesting-reinforcements-recruit"].MovesToBattle, "Requesting Reinforcements moves a Personality to the battle");
    }

    // ---- Ceaseless Vigil --------------------------------------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Yojimbo, CardInstance Other, CardInstance Vigil) VigilTable()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var yojimbo = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(yojimbo, "Yojimbo");
        var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var vigil = Add(s, Vigil, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), yojimbo, other, vigil);
    }

    private static void CeaselessVigilRedirects()
    {
        var (e, yojimbo, other, vigil) = VigilTable();
        var enemyStrategy = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var bv = AddLive(ref e, "training-bushido-virtue", 1, Zone.Hand);
        // A non-Yojimbo is not offered as the performer.
        Check(!CanUse(e, 0, "ceaseless-vigil-watch", vigil.Id, other.Id), "only a Yojimbo performs");
        Accept(e, Enabled(e, 0, "ceaseless-vigil-watch", vigil.Id, yojimbo.Id).Command!);
        Eq(e.State.StandingRedirects.Count, 1, "the watch stands"); Eq(e.State.StandingRedirects[0].Kind, StandingRedirectKinds.OwnPersonalities);
        Eq(e.State.PriorityPlayerId, 0, "the additional action keeps priority with the player");
        Eq(e.State.Cards[vigil.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Pass(e, 0);
        Eq(e.State.PriorityPlayerId, 1);
        // The enemy's action targets the other Personality: it is redirected to the Yojimbo and the watch is spent.
        Accept(e, Offer(e, 1, EnemyBow, bv.Id, other.Id));
        Check(e.State.Cards[yojimbo.Id].Bowed && !e.State.Cards[other.Id].Bowed, "the Yojimbo was bowed instead of the target");
        Check(EventLogged(e, "actionRedirected"), "the log says so");
        Eq(e.State.StandingRedirects.Count, 0, "spent");
        // A second action of the enemy's is not redirected.
        var bv2 = AddLive(ref e, "training-bushido-virtue", 1, Zone.Hand); e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, EnemyBow, bv2.Id, other.Id));
        Check(e.State.Cards[other.Id].Bowed, "the watch was spent by the first action");
    }

    private static void CeaselessVigilLegalityAndFocus()
    {
        // Not legal: the enemy destroys a card without attachments, and the Yojimbo has a Weapon -- the action stays on its target, and the watch is spent anyway.
        var (e, yojimbo, other, vigil) = VigilTable();
        CardInstance destroyer = null!;
        e = Edit(e, st => { var weapon = Add(st, "training-weapon", 0, Zone.Attached, parent: yojimbo.Id); weapon.ProvinceIndex = 0; destroyer = Add(st, Destroyer, 1, Zone.Battlefield, province: 0); });
        Accept(e, Enabled(e, 0, "ceaseless-vigil-watch", vigil.Id, yojimbo.Id).Command!);
        Pass(e, 0);
        Accept(e, Offer(e, 1, Destroy, destroyer.Id, other.Id));
        Eq(e.State.Cards[other.Id].Zone, Zone.Dead, "the original target was destroyed: the redirect was not legal");
        Check(EventLogged(e, "standingRedirectIllegal"), "the log says why");
        Eq(e.State.StandingRedirects.Count, 0, "spent by the next matching action");
        // Your own action targeting your own Personality never triggers it.
        var (f, yojimbo2, other2, vigil2) = VigilTable();
        var ownBow = AddLive(ref f, "training-bushido-virtue", 0, Zone.Hand);
        Accept(f, Enabled(f, 0, "ceaseless-vigil-watch", vigil2.Id, yojimbo2.Id).Command!);
        var own = new GameCommand("useAbility", 0, f.Version, CardId: ownBow.Id, AbilityId: "training-bow-any", TargetId: other2.Id);
        Check(f.Execute(own).Accepted == false, "an Open action is not usable in the Battle phase (control)");
        Eq(f.State.StandingRedirects.Count, 1, "still standing");
        // The Focus Effect: player 0 focuses Ceaseless Vigil in a duel; after the duel a bowed Personality is chosen and straightened (mandatory: no 'take nothing').
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bowedHome = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s, Vigil, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var g = MatchEngine.FromTestState(s);
        Accept(g, Offer(g, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(g, new GameCommand("focus", 1, g.Version, Choice: "deck"));
        Accept(g, new GameCommand("focus", 0, g.Version, CardId: focused.Id));
        Accept(g, new GameCommand("strike", 1, g.Version));
        Eq(Window(g), "focusEffectChoice", "the duel stops at the Focus Effect");
        var choices = g.QueryActions(0).Where(a => a.Type == "chooseFocusEffect").ToArray();
        Check(choices.Any(a => a.Command!.TargetId == bowedHome.Id) && choices.All(a => a.Command!.Choice != "decline"), "the bowed Personality is offered, taking nothing is not");
        Reject(g, new GameCommand("chooseFocusEffect", 0, g.Version, CardId: focused.Id, Choice: "decline"));
        Accept(g, new GameCommand("chooseFocusEffect", 0, g.Version, CardId: focused.Id, TargetId: bowedHome.Id));
        Check(!g.State.Cards[bowedHome.Id].Bowed, "straightened");
        Check(g.State.PendingDuel is null, "the duel finished");
    }

    // ---- Ruthless Onslaught -----------------------------------------------------------------------------------------------------------
    private static void RuthlessOnslaught()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var big = Add(s, "training-high-force-personality", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: other.Id); follower.ProvinceIndex = 0;
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-card-bower", 1, Zone.Battlefield, province: 0);
        var onslaught = Add(s, Onslaught, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "ruthless-onslaught-watch", onslaught.Id, big.Id).Command!);
        Eq(e.State.StandingRedirects.Single().Kind, StandingRedirectKinds.OwnCardsBattleAction);
        Eq(e.State.AdditionalActions.Count, 1, "base Force 7: an additional Battle action");
        Eq(e.State.PriorityPlayerId, 0);
        Pass(e, 0);
        // The enemy bows one of the Defender's cards -- the Follower: the action targets the performer instead.
        Accept(e, Offer(e, 1, "training-bow-enemy-card-at-battle", bower.Id, follower.Id));
        Check(e.State.Cards[big.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "redirected to the performer");
        Eq(e.State.StandingRedirects.Count, 0, "spent");
        // A weaker performer grants nothing.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var small = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var onslaught2 = Add(s2, Onslaught, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Enabled(f, 0, "ruthless-onslaught-watch", onslaught2.Id, small.Id).Command!);
        Eq(f.State.AdditionalActions.Count, 0, "base Force 4: no additional action");
        Eq(f.State.PriorityPlayerId, 1, "priority passes");
    }

    // ---- Metsubushi ------------------------------------------------------------------------------------------------------------------
    private static void MetsubushiMustTarget()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var ninja = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id); enemyFollower.ProvinceIndex = 0;
        var bv = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var metsubushi = Add(s, Metsubushi, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var command = Enabled(e, 0, "metsubushi-attack", metsubushi.Id, ninja.Id).Command! with { TargetId = enemyFollower.Id };
        Accept(e, command);
        Eq(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "Ranged 4 destroyed the Force 2 Follower");
        Eq(e.State.StandingRedirects.Single().Kind, StandingRedirectKinds.MustTarget);
        Eq(e.State.PriorityPlayerId, 1);
        // The enemy leader's next Battle action aimed at the other Personality must target the Ninja.
        Accept(e, Offer(e, 1, EnemyBow, bv.Id, other.Id));
        Check(e.State.Cards[ninja.Id].Bowed && !e.State.Cards[other.Id].Bowed, "the action targeted the Ninja");
        Eq(e.State.StandingRedirects.Count, 0, "once");
        // Not legal (a bowed Ninja cannot be the target of an action that needs an unbowed one) -- the action stays where it was.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var ninja2 = Add(s2, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var other2 = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var destroyer = Add(s2, Destroyer, 1, Zone.Battlefield, province: 0);
        var weapon = Add(s2, "training-weapon", 0, Zone.Attached, parent: ninja2.Id); weapon.ProvinceIndex = 0;
        var metsubushi2 = Add(s2, Metsubushi, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Enabled(f, 0, "metsubushi-attack", metsubushi2.Id, ninja2.Id).Command! with { TargetId = null });
        Accept(f, Offer(f, 1, Destroy, destroyer.Id, other2.Id));
        Eq(f.State.Cards[other2.Id].Zone, Zone.Dead, "the Ninja with a Weapon is not a legal target, so the action kept its own");
        Eq(f.State.StandingRedirects.Count, 0, "spent all the same");
    }

    // ---- Shifting Terrain -------------------------------------------------------------------------------------------------------------
    private static void ShiftingTerrainRedirect()
    {
        var s = Table(Phase.Battle, 1);
        var terrain = Add(s, ShiftingTerrain, 0, Zone.Battlefield, province: 0);
        var a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-crane-veteran", 0, Zone.Home);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var bv = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EnemyBow, bv.Id, a.Id));
        Eq(Window(e), "beforeActionReaction", "the window opens for the Terrain's controller");
        Eq(e.State.PriorityPlayerId, 0);
        var offers = Listed(e, 0, "shifting-terrain-redirect").Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual(new[] { b.Id }), "one option: the other Personality at the battlefield (not the target, not the one at home): " + string.Join(",", offers));
        Accept(e, ReactionCommand(e, 0, "shifting-terrain-redirect", b.Id));
        ClosePassing(e);
        Check(e.State.Cards[b.Id].Bowed && !e.State.Cards[a.Id].Bowed, "the action landed on the chosen Personality");
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain stays in play");
        // The Terrain's own controller's action opens nothing.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        Add(s2, ShiftingTerrain, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var mine = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var bv2 = Add(s2, "training-bushido-virtue", 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 0, EnemyBow, bv2.Id, enemy.Id));
        Check(f.State.Cards[enemy.Id].Bowed && Window(f) is null, "no window for your own action");
    }

    // ---- The Castle of Order ----------------------------------------------------------------------------------------------------------
    private static void CastleOfOrder()
    {
        var s = Table(Phase.Battle, 1);
        var strongholdId = s.Players[0].StrongholdId; s.Cards[strongholdId].DefinitionId = Castle;
        var ninjaA = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var ninjaB = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var ninjaHome = Add(s, "training-ninja-source", 0, Zone.Home);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var bv = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var bv2 = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EnemyBow, bv.Id, ninjaA.Id));
        Eq(Window(e), "beforeActionReaction", "the window opens for the Stronghold's controller");
        var offers = Listed(e, 0, "castle-of-order-redirect").Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual(new[] { ninjaB.Id }), "the other Ninja at the same location, not the plain Personality nor the Ninja at home: " + string.Join(",", offers));
        Accept(e, ReactionCommand(e, 0, "castle-of-order-redirect", ninjaB.Id));
        ClosePassing(e);
        Check(e.State.Cards[ninjaB.Id].Bowed && !e.State.Cards[ninjaA.Id].Bowed, "redirected");
        // Once per battle: the next Battle action against a Ninja opens no window.
        e = Edit(e, st => { st.PriorityPlayerId = 1; st.Cards[ninjaB.Id].Bowed = false; });
        Accept(e, Offer(e, 1, EnemyBow, bv2.Id, ninjaA.Id));
        Check(Window(e) is null && e.State.Cards[ninjaA.Id].Bowed, "no second redirect this battle");
        // A new battle resets it.
        var st2 = e.State; st2.BattleNumber++; st2.PriorityPlayerId = 1; st2.Cards[ninjaA.Id].Bowed = false;
        var bv3 = Add(st2, "training-bushido-virtue", 1, Zone.Hand);
        var g = MatchEngine.FromTestState(st2);
        Accept(g, Offer(g, 1, EnemyBow, bv3.Id, ninjaA.Id));
        Eq(Window(g), "beforeActionReaction", "usable again in the next battle");
        // Honor: player 0 loses no Honor from a Ninja card he owns (Bow Before My Will, granted Ninja), but does from a card without it.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        s3.Cards[s3.Players[0].StrongholdId].DefinitionId = Castle;
        var paragon = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(paragon, "Paragon");
        var enemy = Add(s3, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var will = Add(s3, BowBeforeMyWill, 0, Zone.Hand); GiveKeyword(will, "Ninja");
        var h = MatchEngine.FromTestState(s3);
        var before = Honor(h, 0);
        Accept(h, Offer(h, 0, "bow-before-my-will", will.Id, enemy.Id, paragon.Id));
        Eq(Honor(h, 0), before, "no Honor lost from a Ninja card you own");
        Check(EventLogged(h, "honorLossNegated"), "the negation is logged");
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        s4.Cards[s4.Players[0].StrongholdId].DefinitionId = Castle;
        var paragon2 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(paragon2, "Paragon");
        var enemy2 = Add(s4, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var will2 = Add(s4, BowBeforeMyWill, 0, Zone.Hand);
        var i = MatchEngine.FromTestState(s4);
        var before2 = Honor(i, 0);
        Accept(i, Offer(i, 0, "bow-before-my-will", will2.Id, enemy2.Id, paragon2.Id));
        Eq(Honor(i, 0), before2 - 2, "a card without Ninja still costs the Honor");
    }

    // ---- Exchange of Letters / Martial Manipulation --------------------------------------------------------------------------------------
    private static void LettersAndManipulation()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var bowedMine = Add(s, "training-lion-samurai", 0, Zone.Home, bowed: true);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Home);
        var theirsBowed = Add(s, "training-crane-samurai", 1, Zone.Home, bowed: true);
        var letters = Add(s, Letters, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "exchange-of-letters-lock", letters.Id, bowedMine.Id), "a bowed performer is not offered");
        var option = Enabled(e, 0, "exchange-of-letters-lock", letters.Id, mine.Id);
        Check(option.TargetIds!.Contains(theirs.Id) && !option.TargetIds.Contains(theirsBowed.Id) && !option.TargetIds.Contains(mine.Id), "an unbowed enemy Personality only");
        Check(option.Gold is { Cost: 1 }, "costs 1 Gold");
        Accept(e, Offer(e, 0, "exchange-of-letters-lock", letters.Id, theirs.Id, mine.Id));
        foreach (var locked in new[] { mine, theirs })
            Check(e.State.Cards[locked.Id].ActionLocks.Count == 1 && e.State.Cards[locked.Id].ActionLocks[0].Duration.StartsWith("Phase:", StringComparison.Ordinal), $"{locked.Id} is locked for the phase");
        Check(e.State.Events.Any(x => x.Type == "goldPaid" || x.Type == "paid" || x.Message.Contains("Gold")), "Gold was paid");
        // Martial Manipulation: the Samurai performer and the enemy Personality may not be assigned this turn.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var samurai = Add(s2, "training-scout-samurai", 0, Zone.Home);
        var notSamurai = Add(s2, "training-monk", 0, Zone.Home);
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var manipulation = Add(s2, Manipulation, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Check(!CanUse(f, 0, "martial-manipulation-lock", manipulation.Id, notSamurai.Id), "a non-Samurai does not perform");
        Accept(f, Offer(f, 0, "martial-manipulation-lock", manipulation.Id, enemy.Id, samurai.Id));
        foreach (var locked in new[] { samurai, enemy })
            Check(f.State.Cards[locked.Id].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Assign) && l.Turn == f.State.TurnNumber), $"{locked.Id} may not be assigned");
        var st = f.State; st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0;
        var g = MatchEngine.FromTestState(st);
        Reject(g, new GameCommand("attack", 0, g.Version, Assignments: new Dictionary<string, int> { [samurai.Id] = 0 }));
    }

    // ---- Defensive Blockade -----------------------------------------------------------------------------------------------------------
    private static void DefensiveBlockade()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var attackerWithFollower = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: attackerWithFollower.Id); follower.ProvinceIndex = 0;
        var blockade = Add(s, Blockade, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "defensive-blockade-negate", blockade.Id);
        Check(option.TargetIds!.Contains(attacker.Id) && option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(attackerWithFollower.Id) && !option.TargetIds.Contains(defender.Id),
            "an attacking Personality without attachments or an attacking Follower: " + string.Join(",", option.TargetIds!));
        Accept(e, Offer(e, 0, "defensive-blockade-negate", blockade.Id, attacker.Id));
        Eq(e.State.Cards[attacker.Id].ForceContributionNegatedBattle, e.State.BattleNumber, "stamped for this battle");
        var view = e.GetView(1).Players[1].Army.Single(c => c.Id == attacker.Id);
        Eq(view.UnitForce, 0, "contributes no Force");
        Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == attackerWithFollower.Id).UnitForce, 6, "the other unit is untouched");
        // The battle resolves without it: 5 + 6 attacking would beat the Defender's 3; without the veteran's 5, 6 still does -- so blockade the second unit's Follower too and see the Defender hold.
        Accept(e, new GameCommand("pass", 1, e.Version));
        e = Edit(e, st => { st.PriorityPlayerId = 0; });
        var blockade2 = AddLive(ref e, Blockade, 0, Zone.Hand);
        Accept(e, Offer(e, 0, "defensive-blockade-negate", blockade2.Id, follower.Id));
        Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == attackerWithFollower.Id).UnitForce, 4, "the Follower's 2 is gone");
    }

    // ---- Shield Formation / Unexpected Obstacle ------------------------------------------------------------------------------------------
    private static void ShieldFormationAndObstacle()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var f1 = Add(s, "training-follower", 0, Zone.Attached, parent: defender.Id); f1.ProvinceIndex = 0;
        var f2 = Add(s, "training-follower", 0, Zone.Attached, parent: defender.Id); f2.ProvinceIndex = 0;
        var attacker = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var attackerFollower = Add(s, "training-follower", 1, Zone.Attached, parent: attacker.Id); attackerFollower.ProvinceIndex = 0;
        var shield = Add(s, Shield, 0, Zone.Hand);
        var obstacle = Add(s, Obstacle, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "shield-formation-boost", shield.Id);
        Check(option.TargetIds!.SequenceEqual(new[] { defender.Id }), "your defending Personality only");
        Accept(e, Offer(e, 0, "shield-formation-boost", shield.Id, defender.Id));
        Eq(e.CardForceForTest(defender.Id), 6); Eq(e.CardForceForTest(f1.Id), 5); Eq(e.CardForceForTest(f2.Id), 5);
        e = Edit(e, st => st.PriorityPlayerId = 0);
        var obstacleOption = Enabled(e, 0, "unexpected-obstacle-penalty", obstacle.Id);
        Check(obstacleOption.TargetIds!.Contains(attacker.Id) && obstacleOption.TargetIds.Contains(attackerFollower.Id) && !obstacleOption.TargetIds.Contains(defender.Id), "an enemy Personality or Follower");
        Accept(e, Offer(e, 0, "unexpected-obstacle-penalty", obstacle.Id, attackerFollower.Id));
        Eq(e.CardForceForTest(attackerFollower.Id), 0, "-4F on a Force 2 Follower floors at 0");
        // Shield Formation does not name an attacking Personality.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var shield2 = Add(s2, Shield, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Check(!CanUse(f, 0, "shield-formation-boost", shield2.Id), "the Attacker's Personality is not defending");
    }

    // ---- Embracing Battle / Military Promotion ---------------------------------------------------------------------------------------------
    private static void EmbracingBattleAndPromotion()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var paragon = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(paragon, "Paragon");
        var plain = Add(s, "training-lion-samurai", 0, Zone.Home, bowed: true);
        var embracing = Add(s, Embracing, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "embracing-battle-elite", embracing.Id);
        Check(option.TargetIds!.SequenceEqual(new[] { paragon.Id }), "your Paragon only");
        Accept(e, Offer(e, 0, "embracing-battle-elite", embracing.Id, paragon.Id));
        Check(e.State.Cards[paragon.Id].GrantedKeywords.Any(k => k.Keyword == "Elite" && k.Duration == GrantDuration.Turn), "Elite for the turn");
        Eq(e.State.Cards[paragon.Id].AssignBowedTurn, e.State.TurnNumber);
        var st = e.State; st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = 0;
        var f = MatchEngine.FromTestState(st);
        var attack = f.QueryActions(0).First(a => a.Type == "attack");
        Check(attack.TargetIds!.Contains(paragon.Id) && !attack.TargetIds.Contains(plain.Id), "the bowed Paragon is offered for assignment, the other bowed Personality is not");
        Accept(f, new GameCommand("attack", 0, f.Version, Assignments: new Dictionary<string, int> { [paragon.Id] = 0 }));
        Check(f.State.Cards[paragon.Id].Zone == Zone.Battlefield && f.State.Cards[paragon.Id].Bowed, "assigned while bowed");
        Reject(f, new GameCommand("attack", 0, f.Version, Assignments: new Dictionary<string, int> { [plain.Id] = 0 }));
        // The next turn it is over.
        var st2 = f.State; st2.TurnNumber++; st2.Phase = Phase.InfantryDeclaration; st2.PriorityPlayerId = 0; st2.Cards[paragon.Id].Zone = Zone.Home; st2.Cards[paragon.Id].ProvinceIndex = null;
        var g = MatchEngine.FromTestState(st2);
        Reject(g, new GameCommand("attack", 0, g.Version, Assignments: new Dictionary<string, int> { [paragon.Id] = 0 }));
        // Military Promotion: an Open action, a Tactician performs it for any of your Personalities.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 0;
        var tactician = Add(s3, "training-tactician", 0, Zone.Home);
        var promoted = Add(s3, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var enemy = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var promotion = Add(s3, Promotion, 0, Zone.Hand);
        var h = MatchEngine.FromTestState(s3);
        var promoOption = Enabled(h, 0, "military-promotion-elite", promotion.Id, tactician.Id);
        Check(promoOption.TargetIds!.Contains(promoted.Id) && promoOption.TargetIds.Contains(tactician.Id) && !promoOption.TargetIds.Contains(enemy.Id), "your Personality");
        Check(!CanUse(h, 0, "military-promotion-elite", promotion.Id, promoted.Id), "only a Tactician performs");
        Accept(h, Offer(h, 0, "military-promotion-elite", promotion.Id, promoted.Id, tactician.Id));
        Check(h.State.Cards[promoted.Id].GrantedKeywords.Any(k => k.Keyword == "Elite") && h.State.Cards[promoted.Id].AssignBowedTurn == h.State.TurnNumber, "Elite and may assign while bowed");
    }

    // ---- The Arrow's Eye / Vengeful Dismissal ----------------------------------------------------------------------------------------------
    private static void ArrowsEyeAndDismissal()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var host = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var strong = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id); strong.ProvinceIndex = 0; Force(strong, 3); // Force 5
        var second = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id); second.ProvinceIndex = 0;
        var lone = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(lone, 1); // Force 5, no Followers
        var arrow = Add(s, ArrowsEye, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "arrows-eye-attack", arrow.Id);
        Check(option.TargetIds!.Contains(strong.Id) && option.TargetIds.Contains(lone.Id), "both are legal targets (a target above the strength is still legal): " + string.Join(",", option.TargetIds!));
        {   // strength 3 against the lone Force 5 Personality: legal, but it survives.
            var g = MatchEngine.FromTestState(s);
            Accept(g, Offer(g, 0, "arrows-eye-attack", arrow.Id, lone.Id));
            Eq(g.State.Cards[lone.Id].Zone, Zone.Battlefield, "strength 3 cannot destroy the lone Force 5 Personality");
        }
        Accept(e, Offer(e, 0, "arrows-eye-attack", arrow.Id, strong.Id));
        Eq(e.State.Cards[strong.Id].Zone, Zone.FateDiscard, "destroyed");
        // Vengeful Dismissal.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var performer = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
        var lowChi = Add(s2, "training-bow-enemy-source", 1, Zone.Battlefield, province: 0); // Chi 0
        var equalChi = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var dismissal = Add(s2, Dismissal, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        var dOption = Enabled(f, 0, "vengeful-dismissal-move", dismissal.Id, performer.Id);
        Check(dOption.TargetIds!.SequenceEqual(new[] { lowChi.Id }), "lower Chi only: " + string.Join(",", dOption.TargetIds!));
        Accept(f, Offer(f, 0, "vengeful-dismissal-move", dismissal.Id, lowChi.Id, performer.Id));
        Eq(f.State.Cards[lowChi.Id].Zone, Zone.Home, "moved home");
        Check(!f.State.Cards[lowChi.Id].Bowed, "not bowed by the move");
    }

    // ---- Sticks and Stones ------------------------------------------------------------------------------------------------------------
    private static void SticksAndStones()
    {
        (MatchEngine E, CardInstance Sticks, CardInstance Target) Setup(string targetDef, bool follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var host = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            CardInstance target;
            if (follower) { target = Add(s, targetDef, 1, Zone.Attached, parent: host.Id); target.ProvinceIndex = 0; Force(target, -1); }
            else target = Add(s, targetDef, 1, Zone.Battlefield, province: 0);
            var sticks = Add(s, Sticks, 0, Zone.Hand);
            for (var i = 0; i < 3; i++) { var c = Add(s, "training-weapon", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, c.Id); }
            return (MatchEngine.FromTestState(s), sticks, target);
        }
        var (e, sticks, follower) = Setup("training-follower", true); // Force 1
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "sticks-and-stones-attack", sticks.Id, follower.Id));
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "destroyed");
        Eq(e.State.Players[0].Hand.Count, handBefore, "a card drawn (the Strategy itself left the hand)");
        Eq(e.State.AdditionalActions.Count, 1, "a Follower: an additional Battle action");
        Eq(e.State.PriorityPlayerId, 0, "priority stays");
        var (f, sticks2, personality) = Setup("training-bow-enemy-source", false); // Force 1 Personality
        var handBefore2 = f.State.Players[0].Hand.Count;
        Accept(f, Offer(f, 0, "sticks-and-stones-attack", sticks2.Id, personality.Id));
        Eq(f.State.Cards[personality.Id].Zone, Zone.Dead, "destroyed");
        Eq(f.State.Players[0].Hand.Count, handBefore2, "a card drawn");
        Eq(f.State.AdditionalActions.Count, 0, "not a Follower: no additional action");
        Eq(f.State.PriorityPlayerId, 1);
        var (g, sticks3, _) = Setup("training-bow-enemy-source", false);
        var handBefore3 = g.State.Players[0].Hand.Count;
        Accept(g, Enabled(g, 0, "sticks-and-stones-attack", sticks3.Id).Command! with { TargetId = null });
        Eq(g.State.Players[0].Hand.Count, handBefore3 - 1, "no target, nothing destroyed: no draw");
    }

    // ---- Two Stings -------------------------------------------------------------------------------------------------------------------
    private static void TwoStingsPlays()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var myFollower = Add(s, "training-follower", 0, Zone.Attached, parent: mine.Id); myFollower.ProvinceIndex = 0;
        var enemy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id); enemyFollower.ProvinceIndex = 0;
        var enemyFollower2 = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id); enemyFollower2.ProvinceIndex = 0;
        var stings = Add(s, TwoStings, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Offered(e, 0, "two-stings-attack-from-discard"), "in the hand, the discard-pile action is not offered");
        Accept(e, Offer(e, 0, "two-stings-attack", stings.Id, enemyFollower.Id, myFollower.Id));
        Check(e.State.Cards[myFollower.Id].Bowed && !e.State.Cards[mine.Id].Bowed, "the performing Follower bowed");
        Eq(e.State.Cards[enemyFollower.Id].Zone, Zone.FateDiscard, "Ranged 4 destroyed the Follower");
        Eq(e.State.Cards[stings.Id].Zone, Zone.FateDiscard, "in the discard pile now");
        // From the discard pile: the second action, performed by the Personality, then the card is removed from the game.
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!Offered(e, 0, "two-stings-attack"), "the hand action is not offered from the discard pile");
        var fromDiscard = Listed(e, 0, "two-stings-attack-from-discard").Where(o => o.Enabled).ToArray();
        Check(fromDiscard.Any(o => o.CardId == stings.Id), "offered from the discard pile");
        Accept(e, Offer(e, 0, "two-stings-attack-from-discard", stings.Id, enemyFollower2.Id, mine.Id));
        Check(e.State.Cards[mine.Id].Bowed, "the Personality bowed as the cost");
        Eq(e.State.Cards[enemyFollower2.Id].Zone, Zone.FateDiscard, "the second Ranged 4 hit");
        Eq(e.State.Cards[stings.Id].Zone, Zone.RemovedFromGame, "removed from the game");
    }

    // ---- Wide Valley ------------------------------------------------------------------------------------------------------------------
    private static void WideValleyNegates()
    {
        (MatchEngine E, CardInstance Personality, CardInstance Follower, CardInstance Destroyer, CardInstance Weapon) Setup(bool valley)
        {
            var s = Table(Phase.Battle, 1);
            if (valley) Add(s, WideValley, 1, Zone.Battlefield, province: 0);
            var personality = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id); follower.ProvinceIndex = 0;
            var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
            var weapon = Add(s, "training-bushido-virtue", 1, Zone.Hand);
            return (MatchEngine.FromTestState(s), personality, follower, destroyer, weapon);
        }
        var (e, personality, follower, destroyer, _) = Setup(true);
        Accept(e, Offer(e, 1, Destroy, destroyer.Id, personality.Id));
        Eq(e.State.Cards[personality.Id].Zone, Zone.Battlefield, "the Personality's destruction is negated");
        Check(EventLogged(e, "destructionNegated"), "logged");
        e = Edit(e, st => { st.PriorityPlayerId = 1; st.Cards[destroyer.Id].AbilityUses.Clear(); });
        Accept(e, Offer(e, 1, Destroy, destroyer.Id, follower.Id));
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction is negated");
        var (f, personality2, follower2, destroyer2, _) = Setup(false);
        Accept(f, Offer(f, 1, Destroy, destroyer2.Id, personality2.Id));
        Eq(f.State.Cards[personality2.Id].Zone, Zone.Dead, "without Wide Valley the Personality dies");
    }

    // ---- Grappling the Snake ----------------------------------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Monk, CardInstance Bower, CardInstance BowerFollower) SnakeTable()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var monk = Add(s, "training-monk", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, Bower, 1, Zone.Battlefield, province: 0);
        var bowerFollower = Add(s, "training-follower", 1, Zone.Attached, parent: bower.Id); bowerFollower.ProvinceIndex = 0;
        var snake = Add(s, Snake, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "grappling-the-snake-watch", snake.Id, monk.Id).Command!);
        Eq(e.State.AdditionalActions.Count, 1, "an additional Battle action");
        Check(e.State.Cards[monk.Id].GrantedAbilities.Count(g => g.AbilityId is "grappling-snake-negate" or "grappling-snake-bow") == 2, "the two choices are granted for the turn");
        Pass(e, 0);
        return (e, monk, bower, bowerFollower);
    }

    private static void GrapplingTheSnake()
    {
        // Negate: the bower's action targets the Monk, the window opens, both choices are offered, negating leaves the Monk unbowed; the choice is then spent.
        var (e, monk, bower, bowerFollower) = SnakeTable();
        Accept(e, Offer(e, 1, EnemyBow, bower.Id, monk.Id));
        Eq(Window(e), "beforeActionReaction");
        Check(Offered(e, 0, "grappling-snake-negate") && Offered(e, 0, "grappling-snake-bow"), "both choices");
        Accept(e, ReactionCommand(e, 0, "grappling-snake-negate"));
        ClosePassing(e);
        Check(!e.State.Cards[monk.Id].Bowed, "the action's effects on the Monk were negated");
        Eq(e.State.Cards[monk.Id].GrantedAbilities.Count, 0, "the choice is spent");
        // Bow: the performer's unit bows after the action resolves (the Monk was bowed by it).
        var (f, monk2, bower2, follower2) = SnakeTable();
        Accept(f, Offer(f, 1, EnemyBow, bower2.Id, monk2.Id));
        Accept(f, ReactionCommand(f, 0, "grappling-snake-bow"));
        Check(!f.State.Cards[bower2.Id].Bowed, "nothing bows until the action resolves");
        ClosePassing(f);
        Check(f.State.Cards[monk2.Id].Bowed, "the action resolved on the Monk");
        Check(f.State.Cards[bower2.Id].Bowed && f.State.Cards[follower2.Id].Bowed, "the performer's unit bowed afterwards");
        Eq(f.State.PendingGrapplingBows.Count, 0); Eq(f.State.Cards[monk2.Id].GrantedAbilities.Count, 0, "spent");
        // Declined: passing the window still spends the choice once that action resolves.
        var (g, monk3, bower3, _) = SnakeTable();
        Accept(g, Offer(g, 1, EnemyBow, bower3.Id, monk3.Id));
        ClosePassing(g);
        Check(g.State.Cards[monk3.Id].Bowed, "resolved");
        Eq(g.State.Cards[monk3.Id].GrantedAbilities.Count, 0, "the next time an action targeted him has passed");
        // A Strategy played from a hand has no performing Personality: only the negation is offered.
        var (h, monk4, _, _) = SnakeTable();
        var bv = AddLive(ref h, "training-bushido-virtue", 1, Zone.Hand); h = Edit(h, st => st.PriorityPlayerId = 1);
        Accept(h, Offer(h, 1, EnemyBow, bv.Id, monk4.Id));
        Check(Offered(h, 0, "grappling-snake-negate") && !Offered(h, 0, "grappling-snake-bow"), "no Personality performed it");
    }

    // ---- Sound the Retreat ------------------------------------------------------------------------------------------------------------
    private static void SoundTheRetreat()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var puddle = Add(s, SkippingThePuddle, 0, Zone.Home);
        var retreat = Add(s, Retreat, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "sound-the-retreat-enter", retreat.Id).Command!);
        Eq(e.State.Cards[retreat.Id].Zone, Zone.Home, "in play at home"); Eq(e.State.Cards[puddle.Id].Zone, Zone.FateDiscard, "the other Kata is discarded");
        // The Battle action from play: the performer moves home and his unit straightens.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var kata = Add(s2, Retreat, 0, Zone.Home);
        var performer = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var follower = Add(s2, "training-follower", 0, Zone.Attached, bowed: true, parent: performer.Id); follower.ProvinceIndex = 0;
        Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(s2);
        Check(!Offered(f, 0, "sound-the-retreat-enter"), "a Kata in play is not put into play again");
        Accept(f, Enabled(f, 0, "sound-the-retreat-move", kata.Id, performer.Id).Command!);
        Check(f.State.Cards[performer.Id].Zone == Zone.Home && !f.State.Cards[performer.Id].Bowed && !f.State.Cards[follower.Id].Bowed, "moved home and the unit straightened");
        // Not in play: the Battle action is not offered from the hand.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var inHand = Add(s3, Retreat, 0, Zone.Hand);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(s3);
        Check(!Offered(g, 0, "sound-the-retreat-move"), "must be in play");
    }

    // ---- Requesting Reinforcements ------------------------------------------------------------------------------------------------------
    private static void RequestingReinforcements()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var notScout = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var recruit = PutInProvince(s, "training-scout-samurai", 0, 1);
        var plainRecruit = PutInProvince(s, "training-crane-samurai", 0, 2);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var request = Add(s, Reinforcements, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "requesting-reinforcements-recruit", request.Id, notScout.Id), "a Scout performs");
        var option = Enabled(e, 0, "requesting-reinforcements-recruit", request.Id, scout.Id);
        Check(option.TargetIds!.SequenceEqual(new[] { recruit.Id }), "the face-up Scout in a province: " + string.Join(",", option.TargetIds!));
        Check(option.Gold is { Cost: 3 }, "all costs: the Scout's 3 Gold");
        Accept(e, Offer(e, 0, "requesting-reinforcements-recruit", request.Id, recruit.Id, scout.Id));
        Check(e.State.Cards[scout.Id].Bowed, "the performer bowed");
        Check(e.State.Cards[recruit.Id].Zone == Zone.Battlefield && e.State.Cards[recruit.Id].ProvinceIndex == 0, "entered at the battlefield");
        Check(e.State.Players[0].Provinces[1].CardId != recruit.Id, "the province refilled");
        Eq(e.State.AdditionalActions.Count, 0, "no Reconnaissance: no additional action");
        // With Reconnaissance at the battlefield: an additional Battle action.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var scout2 = Add(s2, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var recruit2 = PutInProvince(s2, "training-scout-samurai", 0, 1);
        Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        s2.Players[0].Provinces[0].Reconnaissance[0] = true; // the battlefield's province row is the Defender's (player 0); the Reconnaissance is player 0's
        var request2 = Add(s2, Reinforcements, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 0, "requesting-reinforcements-recruit", request2.Id, recruit2.Id, scout2.Id));
        Eq(f.State.AdditionalActions.Count, 1, "Reconnaissance: an additional Battle action");
    }

    // ---- Advanced Intelligence ----------------------------------------------------------------------------------------------------------
    private static void AdvancedIntelligence()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var actor = Add(s, "training-recon-actor", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var intelligence = Add(s, Intelligence, 0, Zone.Hand);
        var second = Add(s, Intelligence, 0, Zone.Hand);
        var recon = Add(s, "training-recon-keyword-strategy", 0, Zone.Hand); // not usable now (Open), but a Recon Strategy for the count would be
        for (var i = 0; i < 4; i++) { var c = Add(s, "training-weapon", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, c.Id); }
        var e = MatchEngine.FromTestState(s);
        // A Recon action from a PERSONALITY is not a Recon Strategy's action: no window.
        Accept(e, Enabled(e, 0, "training-recon-attack", actor.Id).Command! with { TargetId = null });
        Check(Window(e) is null, "a Personality's Recon action opens nothing");
        Eq(e.State.ReconStrategyActionsThisBattle[0], 0);
        // A Recon Strategy's action (a Strategy with the Recon keyword carrying an Attack): counted, and the window opens for the hand's Advanced Intelligence.
        var st = e.State; st.PriorityPlayerId = 0;
        var reconStrategy = Add(st, "training-recon-keyword-strategy", 0, Zone.Hand);
        st.Cards[reconStrategy.Id].GrantedAbilities.Add(new("training-recon-attack", "test-fixture", GrantDuration.Permanent));
        var f = MatchEngine.FromTestState(st);
        Accept(f, Enabled(f, 0, "training-recon-attack", reconStrategy.Id).Command! with { TargetId = null });
        Eq(f.State.ReconStrategyActionsThisBattle[0], 1, "one Recon Strategy action this battle");
        Eq(Window(f), "afterActionResolvedReaction", "the window opens");
        var handBefore = f.State.Players[0].Hand.Count;
        Accept(f, Listed(f, 0, "advanced-intelligence-draw").First(o => o.Enabled).Command!);
        Eq(f.State.Players[0].Hand.Count, handBefore - 1 + 1, "played (one card less) and one card drawn for the one Recon Strategy action");
        Eq(f.State.Cards[intelligence.Id].Zone == Zone.FateDiscard || f.State.Cards[second.Id].Zone == Zone.FateDiscard, true, "the played copy is discarded");
        // One copy per battle: with the stamp set, the second copy is refused.
        var st2 = f.State; st2.PendingChoice = new("afterActionResolvedReaction", 0, null, "test"); st2.PriorityPlayerId = 0;
        st2.LastResolvedAction = new ResolvedActionFacts { AbilityId = "training-recon-attack", PlayerId = 0, SourceId = reconStrategy.Id };
        var g = MatchEngine.FromTestState(st2);
        var remaining = g.State.Cards.Values.First(c => c.DefinitionId == Intelligence && c.Zone == Zone.Hand);
        Check(!Offered(g, 0, "advanced-intelligence-draw"), "a second copy this battle is not offered");
        Reject(g, new GameCommand("useAbility", 0, g.Version, CardId: remaining.Id, AbilityId: "advanced-intelligence-draw"));
    }

    // ---- Megumi's Guidance --------------------------------------------------------------------------------------------------------------
    private static void MegumisGuidance()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var megumi = Add(s, Megumi, 0, Zone.Celestial);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "megumis-guidance-shield", megumi.Id, mine.Id).Command!);
        Eq(e.State.Cards[megumi.Id].Zone, Zone.DynastyDiscard, "discarded as the cost");
        Eq(e.State.Cards[mine.Id].DestructionShieldTurn, e.State.TurnNumber);
        Accept(e, Offer(e, 1, Destroy, destroyer.Id, mine.Id));
        Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, "another player's destruction is negated");
        Check(EventLogged(e, "destructionNegated"), "logged");
        // The turn after, the shield is gone.
        var st = e.State; st.TurnNumber++; st.PriorityPlayerId = 1; st.Cards[destroyer.Id].AbilityUses.Clear();
        var f = MatchEngine.FromTestState(st);
        Accept(f, Offer(f, 1, Destroy, destroyer.Id, mine.Id));
        Eq(f.State.Cards[mine.Id].Zone, Zone.Dead, "the shield lasted the turn");
        // The army bonus: the Defender's 3 + 3 beats the Attacker's 5; without Megumi 3 loses to 5.
        (MatchEngine E, CardInstance Attacker, CardInstance Defender) Battle(bool withMegumi)
        {
            var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
            if (withMegumi) Add(t, Megumi, 0, Zone.Celestial);
            var defender = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var attacker = Add(t, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(t), attacker, defender);
        }
        var (g, attacker, defender) = Battle(true);
        RunUntil(g, () => g.State.Phase != Phase.Battle || g.State.Cards[attacker.Id].Zone == Zone.Dead || g.State.Cards[defender.Id].Zone == Zone.Dead);
        Check(g.State.Cards[attacker.Id].Zone == Zone.Dead && g.State.Cards[defender.Id].Zone != Zone.Dead, "the Defender's 3 + 3 beat 5");
        var (h, attacker2, defender2) = Battle(false);
        RunUntil(h, () => h.State.Phase != Phase.Battle || h.State.Cards[attacker2.Id].Zone == Zone.Dead || h.State.Cards[defender2.Id].Zone == Zone.Dead);
        Check(h.State.Cards[defender2.Id].Zone == Zone.Dead, "without it 3 loses to 5");
    }
}
