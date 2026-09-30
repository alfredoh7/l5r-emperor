using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition rulings pass R1 (issue #492): BATTLES WITHOUT A PROVINCE (Mendon's ruling, docs/rules/EMPEROR_EDITION_CORE_RULES.md). The engine model (EmperorRulingR1.cs): additional battlefields
// numbered from 100, each created with or without an associated province and without one whenever its province is destroyed; a battle at one resolves as normal except that no province is destroyed;
// no adjacent battlefields; its own Reconnaissance; a battle fought inside the Action Phase with the Attacker its card names. Cards: Surprise Attack (strategy-wave-62) and Chikushudo's Trickery
// (spell-wave-25) are published; Shinobi Assault, Two Fronts and The Perfect Moment (strategy-wave-64, issue #494: Mendon supplied their scans) are published too and exercised as the published cards.
// Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers.
internal static class EmperorRulingR1Tests
{
    private const string SurpriseAttack = "oracle-l5r-7665", Trickery = "oracle-l5r-1323", KusugiId = "oracle-l5r-7780", ShinobiAssaultId = "oracle-l5r-9927", TwoFrontsId = "oracle-l5r-8922", PerfectMomentId = "oracle-l5r-8257";
    private const int Extra = MatchEngine.FirstAdditionalBattlefield;

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static void PassTwice(MatchEngine e) { Accept(e, PassC(e)); Accept(e, PassC(e)); }
    private static int ProvincesDestroyed(MatchEngine e, int player) => e.State.Players[player].Provinces.Count(p => p.Destroyed);
    private static bool Event(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand Slotted(MatchEngine e, int player, string ability, CardInstance card, CardInstance[] performers, CardInstance target) =>
        Enabled(e, player, ability, card.Id).Command! with { TargetId = target.Id, Selections = [Pick("performers", performers.Select(p => p.Id).ToArray()), Pick("target", target.Id)] };
    // The same command built by hand, for the choices the listing would never offer.
    private static GameCommand Hand(MatchEngine e, int player, string ability, CardInstance card, CardInstance[] performers, CardInstance target) =>
        new("useAbility", player, e.Version, card.Id, AbilityId: ability, TargetId: target.Id, Selections: [Pick("performers", performers.Select(p => p.Id).ToArray()), Pick("target", target.Id)]);

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R1", [
        ("An Action Phase battle publishes only its original turn owner to both seats, survives serialization/state restoration and clears the display context on return", AsidePublicContext),
        ("An occupied resolved extra front keeps its public province association while the Attack Phase continues", ResolvedFrontPublicAssociation),
        ("Published shape: Surprise Attack (strategy-wave-62) and Chikushudo's Trickery (spell-wave-25) print their scans' stats, keywords and ability ids, ship their art and are in the manifests", PublishedShape),
        ("A battle at a battlefield without a province resolves as normal: units are destroyed, Honor is gained and units go home, but the winning Attacker destroys no province and nothing that triggers on one triggers", BattleWithoutProvinceResolves),
        ("A province destroyed before its battle engages leaves a battlefield without one, whose resolution destroys nothing further and cannot end the game twice", ProvinceDestroyedBeforeEngagement),
        ("Two Fronts primitive: the marked province gets a second battlefield in the next normal Attack Phase only; if the first battle destroys the province the second is fought without one, and if it does not the second can still destroy it", TwoFrontsSecondBattle),
        ("There are no adjacent battlefields to a battlefield without a province, and none is adjacent to one; an additional battlefield at a province is adjacent to the neighbouring provinces' battlefields", NoAdjacentBattlefields),
        ("A province-less battlefield has no province strength: effects that change its province's strength do nothing there and still work at a province; it holds its own Reconnaissance, which resets when its battle resolves", NoStrengthAndOwnReconnaissance),
        ("Surprise Attack: the performing Nonhuman Personalities and the target (there or at home) move to a new battlefield that is fought in the Attacker's battle order without a province; the Attacker and Defender stay the same", SurpriseAttackCreates),
        ("Surprise Attack: the performers must be Nonhuman Personalities at the current battlefield, the target the enemy leader's Personality there or at home; it is a Battle action; a negated movement leaves the mover behind", SurpriseAttackRefusals),
        ("Chikushudo's Trickery: from the Action Phase the target attacks and the Spirits defend at a new battlefield, movement to it is negated, the target survives the Combat Segment but not the resolution, the Spell is destroyed and its Shugenja bowed, and the Action Phase resumes", TrickeryBattle),
        ("Chikushudo's Trickery: the attacker is the target's controller whoever plays it (Open on the opponent's turn too), it needs one or two unbowed Nonhuman Spirits and an unbowed enemy Personality, and only the Action Phase", TrickeryRefusalsAndTurns),
        ("Shinobi Assault (strategy-wave-64): the Ninja attacks a bowed target at a new battlefield with the first opportunity, then 5 Honor is lost and the Action Phase resumes", ShinobiAssault),
        ("Two Fronts (strategy-wave-64): Limited, needs a player with two provinces, marks the next normal Attack Phase of the turn", TwoFrontsCard),
        ("The Perfect Moment (strategy-wave-64): the Defender declares an additional Attack Phase and is its Attacker; only his Paragons and Battle Maidens assign or move, and at most one province is destroyed in it", PerfectMoment),
        ("The Perfect Moment: during its additional Attack Phase a Paragon and a Battle Maiden may assign and move, any other Personality may neither assign nor move (each tested on its own); Retribution's one-Personality limit does not apply", PerfectMomentExceptions),
        ("Published shape: Shinobi Assault, Two Fronts and The Perfect Moment (strategy-wave-64) print their scans' stats, keywords, text and ability ids and ship their art", PublishedShapeWave64),
    ]);

    private static void AsidePublicContext()
    {
        foreach (var turn in new[] { 0, 1 })
        foreach (var actor in new[] { 0, 1 })
        {
            var engine = TrickeryTable(actor, out var spell, out _, out var spirits, out var target, out _, turnPlayer: turn);
            Check(engine.GetView(0).ActionPhaseTurnPlayerId is null, "no aside context before the battle");
            Accept(engine, Slotted(engine, actor, "chikushudo-trickery-battle", spell, spirits, target));
            foreach (var seat in new[] { 0, 1 })
            {
                var view = engine.GetView(seat);
                Eq(view.ActionPhaseTurnPlayerId, (int?)turn, "original turn, independent of attacker and viewer");
                Eq(view.ActivePlayerId, 1 - actor, "attacker semantics unchanged");
                var json = System.Text.Json.JsonSerializer.Serialize(view);
                var restoredView = System.Text.Json.JsonSerializer.Deserialize<MatchView>(json)!;
                Eq(restoredView.ActionPhaseTurnPlayerId, (int?)turn, "new display context survives wire serialization");
                Check(!json.Contains("Paused", StringComparison.Ordinal) && !json.Contains("BowedBefore", StringComparison.Ordinal), "paused private action was not projected");
            }
            var restored = Edit(engine, _ => { });
            Eq(restored.GetView(1).ActionPhaseTurnPlayerId, (int?)turn, "state restoration reconstructs the public context");
            PassTwice(restored);
            Check(restored.GetView(0).ActionPhaseTurnPlayerId is null && restored.GetView(1).ActionPhaseTurnPlayerId is null, "aside context retires after resolution");
            Eq(restored.State.Phase, Phase.Action, "return phase remains engine-owned");
        }
        var ended = TrickeryTable(0, out var endSpell, out _, out var endSpirits, out var endTarget, out _);
        Accept(ended, Slotted(ended, 0, "chikushudo-trickery-battle", endSpell, endSpirits, endTarget));
        Accept(ended, new GameCommand("concede", 0, ended.Version));
        Eq(ended.State.Phase, Phase.Finished);
        Check(ended.GetView(0).ActionPhaseTurnPlayerId is null && ended.GetView(1).ActionPhaseTurnPlayerId is null, "concession ends the displayed Action Phase battle");
        var normal = MatchEngine.FromTestState(ProvincelessBattle(20, 1));
        Check(normal.GetView(0).ActionPhaseTurnPlayerId is null, "a regular Attack Phase open battlefield is not an aside");
    }

    private static void ResolvedFrontPublicAssociation()
    {
        var state = ProvincelessBattle(1, 9, province: 2);
        state.UnresolvedBattlefields.Add(0);
        Add(state, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(state, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var engine = MatchEngine.FromTestState(state);
        PassTwice(engine);
        Check(!engine.State.UnresolvedBattlefields.Contains(Extra), "extra front resolved");
        Check(engine.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.ProvinceIndex == Extra), "defender remains at the resolved front");
        foreach (var seat in new[] { 0, 1 })
            Eq(engine.GetView(seat).Battlefields!.Single(b => b.Index == Extra).Province, (int?)2, "resolved occupied front retains its province");
        var destroyed = Edit(engine, s => s.Players[0].Provinces[2].Destroyed = true);
        Check(destroyed.GetView(0).Battlefields!.Single(b => b.Index == Extra).Province is null, "destroyed province makes the occupied front open");
        var empty = Edit(engine, s => { foreach (var c in s.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == Extra)) { c.Zone = Zone.Home; c.ProvinceIndex = null; } });
        Check(empty.GetView(0).Battlefields!.All(b => b.Index != Extra), "resolved empty front is not reintroduced");
    }

    // ---- Published shape --------------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, string id)
        {
            var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(listed.SequenceEqual([id]), $"{wave} manifest lists exactly the wired card");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var surprise = CardCatalog.Get(SurpriseAttack);
        Check(surprise.Type == CardType.Strategy && surprise.Name == "Surprise Attack" && surprise.GoldCost == 0 && surprise.Focus == 2 && (surprise.Keywords ?? []).Length == 0 && surprise.Abilities!.SequenceEqual(["surprise-attack-create"])
            && surprise.Text.StartsWith("Battle: Choose one or more of your performing Nonhuman Personalities at the current battlefield") && surprise.Text.Contains("(not associated with any province)"), "Surprise Attack's scan");
        var trickery = CardCatalog.Get(Trickery);
        Check(trickery.Type == CardType.Spell && trickery.Name == "Chikushudo's Trickery" && trickery.GoldCost == 0 && trickery.Focus == 2 && string.Join(",", (trickery.Keywords ?? []).Order(StringComparer.Ordinal)) == "Earth,Forest"
            && trickery.Abilities!.SequenceEqual(["chikushudo-trickery-battle"]) && trickery.Text.StartsWith("Open: Choose one or two of your performing unbowed Nonhuman Spirit Personalities, target another player's unbowed Personality, bow this Shugenja, and destroy this card:")
            && trickery.Text.EndsWith("Fight a battle there."), "Chikushudo's Trickery's scan");
        Manifest("strategy-wave-62", SurpriseAttack);
        Manifest("spell-wave-25", Trickery);
        Eq(string.Join(",", PublishedContent.StrategyWave62.Cards.Select(c => c.Id)), SurpriseAttack, "strategy-wave-62");
        Eq(string.Join(",", PublishedContent.SpellWave25.Cards.Select(c => c.Id)), Trickery, "spell-wave-25");
        Eq(AbilityCatalog.All["surprise-attack-create"].Timing, "Battle"); Eq(AbilityCatalog.All["chikushudo-trickery-battle"].Timing, "Open");
        Eq(AbilityCatalog.All["shinobi-assault-battle"].Timing, "Limited"); Eq(AbilityCatalog.All["two-fronts-mark"].Timing, "Limited");
    }

    // ---- The engine model -------------------------------------------------------------------------------------------------------------------------------
    // A battle at the additional battlefield 100: player 1 the Attacker, player 0 the Defender (who has priority first).
    private static MatchState ProvincelessBattle(int attackForce, int defenseForce, int? province = null)
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.R1.Additional[Extra] = province ?? -1;
        s.CurrentBattleProvince = Extra; s.UnresolvedBattlefields = [Extra];
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: Extra); Force(attacker, attackForce - 4);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: Extra); Force(defender, defenseForce - 3);
        return s;
    }

    private static void BattleWithoutProvinceResolves()
    {
        var s = ProvincelessBattle(20, 1);
        var attacker = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var defender = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 1);
        PassTwice(e);
        Check(Event(e, "battleResolved") && e.State.Events.Last(x => x.Type == "battleResolved").Message.StartsWith("A battlefield without a province"), "the resolution names it");
        Eq(e.State.Cards[defender.Id].Zone, Zone.Dead, "the outnumbered defender is destroyed as normal");
        Check(Honor(e, 1) > honor, "and the winner gains Honor for it");
        Eq(ProvincesDestroyed(e, 0), 0, "but no province is destroyed, however large the margin");
        Check(!Event(e, "provinceDestroyed"), "so nothing that triggers on a destroyed province can trigger");
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the winning Attacker goes home");
        Check(e.State.Cards[attacker.Id].Bowed, "bowed");
        Eq(e.State.Phase, Phase.Dynasty, "and it was the last battle: the Attack Phase ends");
        Check(e.State.R1.Additional.Count == 0, "the additional battlefield ceases to exist with the Attack Phase");
        // The same margin at a province destroys it.
        var t = Table(Phase.Battle, 1);
        t.PriorityPlayerId = 0;
        var a = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(a, 16);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var control = MatchEngine.FromTestState(t);
        PassTwice(control);
        Eq(ProvincesDestroyed(control, 0), 1, "at a province the same victory destroys it");
        // A defender's victory and a tie work as ever.
        var lost = MatchEngine.FromTestState(ProvincelessBattle(1, 9));
        PassTwice(lost);
        Eq(lost.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.Dead), 1, "the beaten Attacker is destroyed");
        var tie = MatchEngine.FromTestState(ProvincelessBattle(5, 5));
        PassTwice(tie);
        Eq(tie.State.Cards.Values.Count(c => c.Zone == Zone.Dead), 2, "a tie destroys both armies");
    }

    private static void ProvinceDestroyedBeforeEngagement()
    {
        // Province 0 was destroyed by some other effect before its battle engaged: the battle is at a battlefield without a province.
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.Players[0].Provinces[0].Destroyed = true;
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(attacker, 16);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GetView(0).Battlefields!.Single(b => b.Index == 0).Province, null, "the view shows the battlefield without a province");
        PassTwice(e);
        Eq(e.State.Cards[defender.Id].Zone, Zone.Dead, "the battle is resolved as normal");
        Eq(ProvincesDestroyed(e, 0), 1, "and destroys no further province (the one already gone is the only one)");
        Check(!Event(e, "provinceDestroyed") && e.State.WinnerId is null, "nothing is destroyed twice and the game goes on");
    }

    // Player 1's declaration of his next normal Attack Phase with Two Fronts' marker on province 0 (or none, when `marker` is false); two attackers at home.
    private static MatchEngine TwoFrontsDeclaration(out CardInstance first, out CardInstance second, int marker = 0)
    {
        var s = Table(Phase.AttackDeclaration, 1);
        if (marker != 0) s.R1.TwoFronts.Add(new TwoFrontsMark(1, 0, s.TurnNumber + (marker == 2 ? -1 : 0)));
        first = Add(s, "training-lion-samurai", 1, Zone.Home); Force(first, 16);
        second = Add(s, "training-lion-samurai", 1, Zone.Home); Force(second, 16);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(1).First(a => a.Type == "declareAttack" && a.Enabled).Command!);
        return e;
    }

    // Both maneuvers segments with the first attacker at battlefield 0 and the second at the additional one; the battles then wait to be chosen.
    private static void AssignTwoFronts(MatchEngine e, CardInstance first, CardInstance second)
    {
        Accept(e, new("attack", 1, e.Version, Assignments: new() { [first.Id] = 0, [second.Id] = Extra }));
        Accept(e, new("assignDefense", 0, e.Version, Assignments: new()));
        Accept(e, new("attack", 1, e.Version, Assignments: new()));
        Accept(e, new("assignDefense", 0, e.Version, Assignments: new()));
        Eq(e.State.Phase, Phase.BattleSelection);
    }

    private static void FightOthers(MatchEngine e)
    {
        foreach (var other in new[] { 1, 2, 3 }) { Accept(e, new("selectBattle", 1, e.Version, Province: other)); PassTwice(e); }
    }

    private static void TwoFrontsSecondBattle()
    {
        var e = TwoFrontsDeclaration(out var first, out var second);
        Eq(string.Join(",", e.State.UnresolvedBattlefields), "0,1,2,3", "without the marker one battlefield at each province");
        var m = TwoFrontsDeclaration(out first, out second, marker: 1);
        Eq(string.Join(",", m.State.UnresolvedBattlefields), $"0,1,2,3,{Extra}", "two battlefields at the marked province, one at each of the others");
        Eq(m.State.R1.Additional[Extra], 0, "the additional one is associated with province 0");
        Eq(m.GetView(0).Battlefields!.Single(b => b.Index == Extra).Province, 0, "and the view says so");
        AssignTwoFronts(m, first, second);
        Check(m.QueryActions(1).Any(a => a.Type == "selectBattle" && a.Label.Contains("additional battlefield at province 1")), "the second front is offered by name");
        // The first front destroys province 0: the second is fought at a battlefield without a province.
        FightOthers(m);
        Accept(m, new("selectBattle", 1, m.Version, Province: 0));
        PassTwice(m);
        Check(m.State.Players[0].Provinces[0].Destroyed, "the first front destroyed province 0");
        Eq(m.BattlefieldsForTest().Single().Province, null, "so its second battlefield has no province now");
        var destroyedBefore = ProvincesDestroyed(m, 0);
        Accept(m, new("selectBattle", 1, m.Version, Province: Extra));
        PassTwice(m);
        Eq(ProvincesDestroyed(m, 0), destroyedBefore, "and destroys no province though its Attacker won by a wide margin");
        Check(m.State.Cards[second.Id].Zone == Zone.Home && m.State.Cards[second.Id].Bowed, "its Attacker went home bowed");
        Eq(m.State.Phase, Phase.Dynasty, "the Attack Phase is over");
        Check(m.State.R1.Additional.Count == 0, "and the additional battlefield with it");
        // The other order: the additional battlefield goes first and destroys the province itself; the primary one is then fought without it.
        var f = TwoFrontsDeclaration(out first, out second, marker: 1);
        AssignTwoFronts(f, first, second);
        FightOthers(f);
        Accept(f, new("selectBattle", 1, f.Version, Province: Extra));
        PassTwice(f);
        Check(f.State.Players[0].Provinces[0].Destroyed, "an additional battlefield at a province destroys that province when its Attacker wins big enough");
        var afterFirst = ProvincesDestroyed(f, 0);
        Accept(f, new("selectBattle", 1, f.Version, Province: 0));
        PassTwice(f);
        Eq(ProvincesDestroyed(f, 0), afterFirst, "the primary battlefield of the destroyed province is then a battlefield without one");
        // A marker is for the next NORMAL Attack Phase of THAT turn only: an old marker creates nothing and is discarded.
        var stale = TwoFrontsDeclaration(out _, out _, marker: 2);
        Eq(string.Join(",", stale.State.UnresolvedBattlefields), "0,1,2,3", "a marker of an earlier turn creates no second battlefield");
        Check(stale.State.R1.TwoFronts.Count == 0, "and is discarded");
    }

    private static void NoAdjacentBattlefields()
    {
        var s = Table(Phase.Battle, 1);
        s.R1.Additional[Extra] = -1; s.R1.Additional[Extra + 1] = 2;
        s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 2, 3, Extra, Extra + 1];
        var e = MatchEngine.FromTestState(s);
        Eq(string.Join(",", e.AdjacentBattlefieldsForTest(Extra)), "", "a battlefield without a province has no adjacent battlefield");
        Eq(string.Join(",", e.AdjacentBattlefieldsForTest(1)), $"0,2,{Extra + 1}", "province 1's battlefield is adjacent to province 0's and to province 2's -- the additional battlefield at province 2 included -- but never to the one without a province");
        Eq(string.Join(",", e.AdjacentBattlefieldsForTest(Extra + 1)), "1,3", "an additional battlefield at province 2 is adjacent to the battlefields of provinces 1 and 3, not to another battlefield at its own province");
        // A destroyed province slides its neighbours together, and its own battlefield is then without a province.
        var t = Table(Phase.Battle, 1);
        t.Players[0].Provinces[1].Destroyed = true;
        t.CurrentBattleProvince = 0; t.UnresolvedBattlefields = [0, 1, 2, 3];
        var slid = MatchEngine.FromTestState(t);
        Eq(string.Join(",", slid.AdjacentBattlefieldsForTest(0)), "2", "province 0 is next to province 2 once province 1 is destroyed");
        Eq(string.Join(",", slid.AdjacentBattlefieldsForTest(1)), "", "and the battlefield of the destroyed province 1 has no adjacent battlefield");
    }

    private static void NoStrengthAndOwnReconnaissance()
    {
        // Tamori Kusugi: "raise or lower the current battlefield's province's strength by 4" -- nothing to raise without a province, and it still works at one.
        MatchEngine Kusugi(int battlefield)
        {
            var s = Table(Phase.Battle, 1);
            s.PriorityPlayerId = 0;
            if (battlefield >= Extra) s.R1.Additional[battlefield] = -1;
            s.CurrentBattleProvince = battlefield; s.UnresolvedBattlefields = [battlefield];
            Add(s, KusugiId, 0, Zone.Home);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: battlefield);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: battlefield);
            return MatchEngine.FromTestState(s);
        }
        var none = Kusugi(Extra);
        var kusugiCard = none.State.Cards.Values.Single(c => c.DefinitionId == KusugiId);
        var before = Enumerable.Range(0, 4).Select(i => none.ProvinceStrengthForTest(0, i)).ToArray();
        Accept(none, Enabled(none, 0, "kusugi-raise", kusugiCard.Id).Command!);
        Check(none.State.Cards[kusugiCard.Id].Bowed, "he is bowed as the cost");
        Check(Enumerable.Range(0, 4).Select(i => none.ProvinceStrengthForTest(0, i)).SequenceEqual(before), "and no province's strength changed");
        var at = Kusugi(0);
        var kusugiAt = at.State.Cards.Values.Single(c => c.DefinitionId == KusugiId);
        var strength = at.ProvinceStrengthForTest(0, 0);
        Accept(at, Enabled(at, 0, "kusugi-raise", kusugiAt.Id).Command!);
        Eq(at.ProvinceStrengthForTest(0, 0), strength + 4, "at a province the same action raises it by 4");
        // Reconnaissance is held by the battlefield: a province-less one holds its own and loses it when its battle resolves; no province holds it for it.
        var r = Table(Phase.Battle, 1);
        r.PriorityPlayerId = 0;
        r.R1.Additional[Extra] = -1; r.CurrentBattleProvince = Extra; r.UnresolvedBattlefields = [Extra];
        var scout = Add(r, "training-scout", 0, Zone.Battlefield, province: Extra);
        Add(r, "training-lion-samurai", 1, Zone.Battlefield, province: Extra);
        var e = MatchEngine.FromTestState(r);
        Accept(e, Use(e, 0, "training-gain-reconnaissance-here", scout.Id));
        Check(e.State.R1.AdditionalReconnaissance[Extra][0] && !e.State.R1.AdditionalReconnaissance[Extra][1], "the defender holds Reconnaissance at the battlefield without a province");
        Check(e.State.Players[0].Provinces.All(p => !p.Reconnaissance[0]) && e.State.Players[1].Provinces.All(p => !p.Reconnaissance[0]), "and no province holds it");
        Accept(e, PassC(e)); Accept(e, PassC(e)); Accept(e, PassC(e));
        Check(!e.State.R1.AdditionalReconnaissance.ContainsKey(Extra), "it resets when the battle resolves");
    }

    // ---- Surprise Attack ------------------------------------------------------------------------------------------------------------------------------------
    // Player 1 (the Attacker) holds Surprise Attack with two Nonhuman Personalities at battlefield 0; player 0 has a Personality there and one at home.
    private static MatchEngine SurpriseTable(out CardInstance card, out CardInstance[] nonhumans, out CardInstance atBattle, out CardInstance atHome)
    {
        var s = Table(Phase.Battle, 1);
        card = Add(s, SurpriseAttack, 1, Zone.Hand);
        nonhumans = [Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Nonhuman"), Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Nonhuman")];
        atBattle = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        atHome = Add(s, "training-crane-courtier", 0, Zone.Home);
        return MatchEngine.FromTestState(s);
    }

    private static void SurpriseAttackCreates()
    {
        // The target is at home: performers and target move to the new battlefield.
        var e = SurpriseTable(out var card, out var nonhumans, out var atBattle, out var atHome);
        var slots = Enabled(e, 1, "surprise-attack-create", card.Id).Slots!;
        Check(slots.Single(x => x.Slot == "performers").CardIds.OrderBy(x => x).SequenceEqual(nonhumans.Select(n => n.Id).OrderBy(x => x)), "the performers offered are the Nonhuman Personalities at the battlefield");
        Check(slots.Single(x => x.Slot == "target").CardIds.Contains(atHome.Id) && slots.Single(x => x.Slot == "target").CardIds.Contains(atBattle.Id), "the targets are the enemy leader's Personalities there and at home");
        Accept(e, Slotted(e, 1, "surprise-attack-create", card, nonhumans, atHome));
        var created = e.State.UnresolvedBattlefields.Single(b => b >= Extra);
        Eq(e.State.R1.Additional[created], -1, "a battlefield associated with no province was created");
        Check(nonhumans.All(n => e.State.Cards[n.Id].Zone == Zone.Battlefield && e.State.Cards[n.Id].ProvinceIndex == created), "every performer moved there");
        Check(e.State.Cards[atHome.Id].Zone == Zone.Battlefield && e.State.Cards[atHome.Id].ProvinceIndex == created, "and so did the target, from home");
        Eq(e.State.Cards[atBattle.Id].ProvinceIndex, 0, "the enemy Personality at the current battlefield who was not targeted stays");
        Eq(e.State.CurrentBattleProvince, 0, "the battle in progress goes on at the current battlefield");
        Eq(e.State.ActivePlayerId, 1, "no attack was declared: the Attacker is still the current Attacker");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.BattlefieldsForTest().Single().Province, null);
        Check(e.AdjacentBattlefieldsForTest(created).Length == 0, "the new battlefield has no adjacent battlefield");
        // The first battle resolves; the created battlefield is fought next, in the Attacker's order, without a province.
        PassTwice(e);
        Eq(e.State.Phase, Phase.BattleSelection, "the Attacker chooses the next battle");
        var offered = e.QueryActions(1).Where(a => a.Type == "selectBattle").ToArray();
        Check(offered.Length == 1 && offered[0].Label == "Fight at the battlefield without a province", "the created battlefield is offered by name");
        var strong = Edit(e, st => Force(st.Cards[nonhumans[0].Id], 30));
        Accept(strong, offered[0].Command! with { ExpectedVersion = strong.Version });
        Eq(strong.State.CurrentBattleProvince, created);
        Eq(strong.State.PriorityPlayerId, 0, "the Defender has the first opportunity as ever");
        var destroyedBefore = ProvincesDestroyed(strong, 0);
        PassTwice(strong);
        Eq(ProvincesDestroyed(strong, 0), destroyedBefore, "the resolution destroys no province however large the win");
        Eq(strong.State.Cards[atHome.Id].Zone, Zone.Dead, "the enemy Personality in the created battle's defending army is destroyed as normal");
        Check(nonhumans.All(n => strong.State.Cards[n.Id].Zone == Zone.Home), "the Attacker's army goes home");
        Eq(strong.State.Phase, Phase.Dynasty, "and it was the last battle");
        // The Defender can use it too: the current Attacker and Defender do not change.
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        var mine = Add(s, SurpriseAttack, 0, Zone.Hand);
        var spirit = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Nonhuman");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var d = MatchEngine.FromTestState(s);
        Accept(d, Slotted(d, 0, "surprise-attack-create", mine, [spirit], enemy));
        var made = d.State.UnresolvedBattlefields.Single(b => b >= Extra);
        Check(d.State.Cards[spirit.Id].ProvinceIndex == made && d.State.Cards[enemy.Id].ProvinceIndex == made, "the Defender's Nonhuman and the Attacker's Personality meet at the new battlefield");
        Eq(d.State.ActivePlayerId, 1, "the Attacker is still player 1, the Defender player 0, as it creates a battlefield but declares no attack");
        Check(d.State.Cards[enemy.Id].OwnerId == 1 && d.State.Cards[spirit.Id].OwnerId == 0, "each stands on his controller's side");
    }

    private static void SurpriseAttackRefusals()
    {
        var s = Table(Phase.Battle, 1);
        var c = Add(s, SurpriseAttack, 1, Zone.Hand);
        var human = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Nonhuman");
        var away = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Nonhuman");
        var foe = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 1);
        s.UnresolvedBattlefields = [0, 1];
        var x = MatchEngine.FromTestState(s);
        Check(Rejection(x, Hand(x, 1, "surprise-attack-create", c, [human], foe)).Length > 0, "a Human performer is refused");
        Check(Rejection(x, Hand(x, 1, "surprise-attack-create", c, [away], foe)).Length > 0, "a performer at home is refused: he must be at the current battlefield");
        Check(Rejection(x, Hand(x, 1, "surprise-attack-create", c, [mine], mine)).Length > 0, "the acting player's own Personality is not the enemy leader's");
        Check(Rejection(x, Hand(x, 1, "surprise-attack-create", c, [mine], elsewhere)).Length > 0, "an enemy Personality at another battlefield is neither there nor at home");
        Check(Rejection(x, Hand(x, 1, "surprise-attack-create", c, [], foe)).Length > 0, "at least one performer is needed");
        Accept(x, Hand(x, 1, "surprise-attack-create", c, [mine], foe));
        Check(x.State.Cards[mine.Id].ProvinceIndex >= Extra && x.State.Cards[foe.Id].ProvinceIndex >= Extra, "the legal choice is taken");
        // It is a Battle action: not in the Action Phase.
        var action = Table(Phase.Action, 1);
        var hand = Add(action, SurpriseAttack, 1, Zone.Hand);
        Kw(Add(action, "training-lion-samurai", 1, Zone.Home), "Nonhuman"); Add(action, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(action), 1, "surprise-attack-create", hand.Id), "Surprise Attack is a Battle action");
        // Movement is negatable one mover at a time: a target whose movement is negated stays where he is while the performers still move.
        var lockState = Table(Phase.Battle, 1);
        var lc = Add(lockState, SurpriseAttack, 1, Zone.Hand);
        var lp = Kw(Add(lockState, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Nonhuman");
        var lt = Add(lockState, "training-crane-courtier", 0, Zone.Home);
        lockState.NegatedEffects.Add(new NegatedEffect("move", lt.Id));
        var le = MatchEngine.FromTestState(lockState);
        Accept(le, Hand(le, 1, "surprise-attack-create", lc, [lp], lt));
        Check(le.State.Cards[lp.Id].ProvinceIndex >= Extra, "the performer moved");
    }

    // ---- Chikushudo's Trickery ------------------------------------------------------------------------------------------------------------------------------------
    // A Shugenja of `spellPlayer` with the Spell, `spirits` Nonhuman Spirits at home, and two Personalities of the other player at home (the second is a bystander).
    private static MatchEngine TrickeryTable(int spellPlayer, out CardInstance spell, out CardInstance shugenja, out CardInstance[] spirits, out CardInstance target, out CardInstance bystander, int spiritCount = 2, int turnPlayer = 0)
    {
        var s = Table(Phase.Action, turnPlayer);
        var foe = 1 - spellPlayer;
        var own = spellPlayer == 0 ? "training-crane-samurai" : "training-lion-samurai";
        var enemy = spellPlayer == 0 ? "training-lion-samurai" : "training-crane-samurai";
        shugenja = Kw(Add(s, own, spellPlayer, Zone.Home), "Shugenja", "Earth");
        spell = Add(s, Trickery, spellPlayer, Zone.Attached, parent: shugenja.Id);
        spirits = Enumerable.Range(0, spiritCount).Select(_ => Kw(Add(s, own, spellPlayer, Zone.Home), "Nonhuman", "Spirit")).ToArray();
        target = Add(s, enemy, foe, Zone.Home); Force(target, -2);
        bystander = Add(s, enemy, foe, Zone.Home);
        s.PriorityPlayerId = spellPlayer;
        return MatchEngine.FromTestState(s);
    }

    private static void TrickeryBattle()
    {
        var e = TrickeryTable(0, out var spell, out var shugenja, out var spirits, out var target, out var bystander);
        var slots = Enabled(e, 0, "chikushudo-trickery-battle", spell.Id).Slots!;
        Check(slots.Single(x => x.Slot == "performers").CardIds.OrderBy(x => x).SequenceEqual(spirits.Select(x => x.Id).OrderBy(x => x)), "the performers are the unbowed Nonhuman Spirit Personalities");
        Check(slots.Single(x => x.Slot == "target").CardIds.Contains(target.Id) && slots.Single(x => x.Slot == "target").CardIds.Contains(bystander.Id) && !slots.Single(x => x.Slot == "target").CardIds.Any(id => spirits.Any(sp => sp.Id == id)), "the target is another player's Personality");
        Accept(e, Slotted(e, 0, "chikushudo-trickery-battle", spell, spirits, target));
        var battlefield = e.State.CurrentBattleProvince!.Value;
        Check(battlefield >= Extra && e.BattlefieldsForTest().Single().Province is null, "a battlefield associated with no province was created");
        Eq(e.State.Phase, Phase.Battle, "and its battle is being fought");
        Eq(e.State.ActivePlayerId, 1, "the Attacker is the target's controller, though it is player 0's turn");
        Eq(string.Join(",", e.State.UnresolvedBattlefields), battlefield.ToString(), "it is the only battlefield");
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield && e.State.Cards[target.Id].ProvinceIndex == battlefield, "the target was assigned to attack there");
        Check(spirits.All(sp => e.State.Cards[sp.Id].Zone == Zone.Battlefield && e.State.Cards[sp.Id].ProvinceIndex == battlefield), "and the performing Spirits to defend there");
        Eq(e.State.Cards[bystander.Id].Zone, Zone.Home, "nobody else goes");
        Check(e.State.Cards[shugenja.Id].Bowed && e.State.Cards[shugenja.Id].Zone == Zone.Home, "the Shugenja was bowed as a cost");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "and the Spell destroyed");
        Eq(e.State.PriorityPlayerId, 0, "the Defender has the first opportunity");
        // "Negate all movement to it": a Personality at home cannot be moved there.
        string moverCardId = "", moverId = "";
        var edited = Edit(e, st => { moverCardId = Add(st, "training-move-battle-strategy", 0, Zone.Hand).Id; moverId = Add(st, "training-crane-courtier", 0, Zone.Home).Id; });
        Accept(edited, Offer(edited, 0, "training-move-to-battle", moverCardId, moverId));
        Eq(edited.State.Cards[moverId].Zone, Zone.Home, "the movement to the created battlefield was negated: he stays at home");
        Check(Event(edited, "moveNegated"), "and the log says it was negated");
        // "He will not be destroyed during the battle's Combat Segment": an action that would destroy him fails.
        Eq(edited.DestroyUnitsForTest(target.Id), 0, "an effect destroying the target in the Combat Segment does nothing");
        Eq(edited.State.Cards[target.Id].Zone, Zone.Battlefield, "he is still there");
        // The Combat Segment ends with two passes; the resolution then destroys the weaker army as ever (the Spirits are the stronger).
        PassTwice(edited);
        Eq(edited.State.Cards[target.Id].Zone, Zone.Dead, "the resolution destroys him: the shield lasted only through the Combat Segment");
        Check(spirits.All(sp => edited.State.Cards[sp.Id].Zone == Zone.Home && !edited.State.Cards[sp.Id].Bowed), "the Defender's Spirits go home unbowed");
        Eq(edited.State.Phase, Phase.Action, "the Action Phase resumes");
        Eq(edited.State.ActivePlayerId, 0, "with its own turn player");
        Eq(edited.State.PriorityPlayerId, 1, "and the other player has the next opportunity to act");
        Check(edited.State.R1.Aside is null && edited.State.R1.Additional.Count == 0 && edited.State.R1.MovementNegated.Count == 0 && edited.State.UnresolvedBattlefields.Count == 0 && edited.State.CurrentBattleProvince is null,
            "the battlefield ceased to exist with everything scoped to it");
        Eq(ProvincesDestroyed(edited, 0) + ProvincesDestroyed(edited, 1), 0, "no province was destroyed");
        // A stronger Attacker wins: the Defender's Spirits are destroyed, the Attacker bows and goes home, and the battle still destroys no province.
        var strong = TrickeryTable(0, out var spell2, out _, out var spirits2, out var target2, out _);
        var stronger = Edit(strong, st => Force(st.Cards[target2.Id], 20));
        Accept(stronger, Slotted(stronger, 0, "chikushudo-trickery-battle", spell2, spirits2, target2));
        PassTwice(stronger);
        Check(spirits2.All(sp => stronger.State.Cards[sp.Id].Zone == Zone.Dead), "an Attacker with more Force destroys the defending Spirits");
        Check(stronger.State.Cards[target2.Id].Zone == Zone.Home && stronger.State.Cards[target2.Id].Bowed, "and goes home bowed");
        Eq(ProvincesDestroyed(stronger, 1), 0, "with no province to destroy");
        Eq(stronger.State.Phase, Phase.Action);
    }

    private static void TrickeryRefusalsAndTurns()
    {
        // Played on the opponent's turn (Open): the turn player is already the Attacker, nothing is swapped.
        var e = TrickeryTable(1, out var spell, out _, out var spirits, out var target, out _, turnPlayer: 0);
        Accept(e, Slotted(e, 1, "chikushudo-trickery-battle", spell, spirits, target));
        Eq(e.State.ActivePlayerId, 0, "the target's controller is the active player and stays the Attacker");
        Check(e.State.CurrentBattleProvince >= Extra);
        Eq(e.State.PriorityPlayerId, 1, "the Defender (the Spell's player) has the first opportunity");
        PassTwice(e);
        Eq(e.State.Phase, Phase.Action, "the Action Phase resumes");
        Eq(e.State.ActivePlayerId, 0);
        Eq(e.State.PriorityPlayerId, 0, "the other player, the turn player, has the next opportunity");
        // One or two performers only, unbowed Nonhuman Spirits only, an unbowed target of another player only.
        var three = TrickeryTable(0, out var s3, out _, out var sp3, out var t3, out _, spiritCount: 3);
        Check(Rejection(three, Hand(three, 0, "chikushudo-trickery-battle", s3, sp3, t3)).Length > 0, "three performers are refused");
        var none = TrickeryTable(0, out var s0, out _, out _, out var t0, out _);
        Check(Rejection(none, Hand(none, 0, "chikushudo-trickery-battle", s0, [], t0)).Length > 0, "no performer is refused");
        var human = TrickeryTable(0, out var sh, out var shShug, out var shSpirits, out var shTarget, out _);
        var plainId = "";
        human = Edit(human, st => plainId = Add(st, "training-crane-samurai", 0, Zone.Home).Id);
        Check(Rejection(human, Hand(human, 0, "chikushudo-trickery-battle", sh, [human.State.Cards[plainId]], shTarget)).Length > 0, "a Human is not a Nonhuman Spirit");
        var bowedSpirit = Edit(human, st => st.Cards[shSpirits[0].Id].Bowed = true);
        Check(Rejection(bowedSpirit, Hand(bowedSpirit, 0, "chikushudo-trickery-battle", sh, [shSpirits[0]], shTarget)).Length > 0, "a bowed Spirit is not an unbowed one");
        var bowedTarget = Edit(human, st => st.Cards[shTarget.Id].Bowed = true);
        Check(Rejection(bowedTarget, Hand(bowedTarget, 0, "chikushudo-trickery-battle", sh, [shSpirits[0]], shTarget)).Length > 0, "the target must be unbowed");
        Check(Rejection(human, Hand(human, 0, "chikushudo-trickery-battle", sh, [shSpirits[0]], shSpirits[1])).Length > 0, "the target is another player's Personality");
        var bowedShugenja = Edit(human, st => st.Cards[shShug.Id].Bowed = true);
        Check(!CanUse(bowedShugenja, 0, "chikushudo-trickery-battle", sh.Id), "the Shugenja must be unbowed to be bowed as the cost");
        // Only in the Action Phase: an Open action is not taken during a battle.
        var battle = Table(Phase.Battle, 1);
        var bShug = Kw(Add(battle, "training-lion-samurai", 1, Zone.Home), "Shugenja", "Earth");
        var bSpell = Add(battle, Trickery, 1, Zone.Attached, parent: bShug.Id);
        Kw(Add(battle, "training-lion-samurai", 1, Zone.Home), "Nonhuman", "Spirit"); Add(battle, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(battle), 1, "chikushudo-trickery-battle", bSpell.Id), "no battle is fought from inside another");
    }

    // ---- Shinobi Assault, Two Fronts and The Perfect Moment (strategy-wave-64): the published cards on the same model -------------------------------------------------------------------------
    private static void ShinobiAssault()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, ShinobiAssaultId, 0, Zone.Hand);
        var ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ninja"); Force(ninja, 6);
        var plain = Add(s, "training-crane-courtier", 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var offer = Options(e, 0, "shinobi-assault-battle", card.Id, ninja.Id).Single(o => o.Enabled);
        Check(!Options(e, 0, "shinobi-assault-battle", card.Id, plain.Id).Any(o => o.Enabled), "only a Ninja can perform it");
        var honor = Honor(e, 0);
        Accept(e, offer.Command! with { TargetId = target.Id });
        var battlefield = e.State.CurrentBattleProvince!.Value;
        Check(battlefield >= Extra && e.BattlefieldsForTest().Single().Province is null, "a battlefield not at any province");
        Eq(e.State.ActivePlayerId, 0, "the player of Shinobi Assault is the Attacker");
        Check(e.State.Cards[ninja.Id].ProvinceIndex == battlefield && e.State.Cards[target.Id].ProvinceIndex == battlefield && e.State.Cards[target.Id].Bowed, "the Ninja attacks and the bowed target defends, even bowed");
        Eq(e.State.PriorityPlayerId, 0, "the Attacker has the first opportunity to take a Battle action or pass");
        Eq(e.State.FirstActionReason, BattleSteps.ReasonEngageGrant);
        PassTwice(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the battle is resolved: the stronger Ninja destroys the target");
        Eq(Honor(e, 0), honor + 2 - 5, "5 Honor is lost after the battle (and 2 gained for the destroyed army)");
        Eq(e.State.Phase, Phase.Action, "the Action Phase resumes");
        Eq(e.State.PriorityPlayerId, 1);
        Eq(ProvincesDestroyed(e, 1), 0, "no province is destroyed");
        // A bowed Ninja cannot perform.
        var t = Table(Phase.Action, 0);
        var c2 = Add(t, ShinobiAssaultId, 0, Zone.Hand);
        var bowedNinja = Kw(Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true), "Ninja");
        Add(t, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "shinobi-assault-battle", c2.Id, bowedNinja.Id), "the performer must be unbowed");
    }

    private static void TwoFrontsCard()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, TwoFrontsId, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 0, "two-fronts-mark", card.Id).First(o => o.Enabled);
        Accept(e, option.Command! with { Province = 2 });
        Eq(e.State.R1.TwoFronts.Single().Province, 2);
        // The next normal Attack Phase this turn: the declaration creates the second battlefield at province 2.
        while (e.State.Phase != Phase.AttackDeclaration) Accept(e, PassC(e));
        Accept(e, e.QueryActions(0).First(a => a.Type == "declareAttack" && a.Enabled).Command!);
        Eq(string.Join(",", e.State.UnresolvedBattlefields), $"0,1,2,3,{Extra}");
        Eq(e.State.R1.Additional[Extra], 2, "associated with the targeted province");
        // A province whose player has fewer than two provinces is not a legal target.
        var t = Table(Phase.Action, 0);
        var c2 = Add(t, TwoFrontsId, 0, Zone.Hand);
        foreach (var p in t.Players[1].Provinces.Take(3)) p.Destroyed = true;
        Add(t, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "two-fronts-mark", c2.Id), "a player with one province cannot be the target");
    }

    private static void PerfectMoment()
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.CurrentBattleProvince = 3; s.UnresolvedBattlefields = [3];
        var card = Add(s, PerfectMomentId, 0, Zone.Hand);
        var moveCard = Add(s, "training-move-battle-strategy", 0, Zone.Hand);
        var paragonA = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Paragon"); Force(paragonA, 17);
        var paragonB = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Battle Maiden"); Force(paragonB, 17);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home); Force(plain, 17);
        var e = MatchEngine.FromTestState(s);
        PassTwice(e); // the last battle of player 1's Attack Phase is over
        Eq(e.State.PendingChoice?.Type, "attackPhaseEndReaction", "the Defender may react to the end of the Attack Phase");
        var honor = Honor(e, 0);
        Accept(e, Options(e, 0, "perfect-moment-declare", card.Id).First(o => o.Enabled).Command!);
        Eq(e.State.ActivePlayerId, 0, "its player declared the additional Attack Phase and is its Attacker");
        Eq(e.State.Phase, Phase.InfantryDeclaration);
        Eq(string.Join(",", e.State.UnresolvedBattlefields), "0,1,2,3", "a battlefield at each of the previous Attacker's provinces");
        Check(e.State.R1.PerfectMoment is { PlayerId: 0 }, "its restrictions are in force");
        // "May not assign unless they are Paragons or Battle Maidens": a plain Personality is refused, and Retribution's one-Personality limit does not apply.
        Check(Rejection(e, new("attack", 0, e.Version, Assignments: new() { [plain.Id] = 0 })).Length > 0, "a Personality who is neither a Paragon nor a Battle Maiden may not assign");
        Accept(e, new("attack", 0, e.Version, Assignments: new() { [paragonA.Id] = 0, [paragonB.Id] = 1 }));
        Accept(e, new("assignDefense", 1, e.Version, Assignments: new()));
        Accept(e, new("attack", 0, e.Version, Assignments: new()));
        Accept(e, new("assignDefense", 1, e.Version, Assignments: new()));
        Eq(e.State.Phase, Phase.BattleSelection);
        // "Will not move unless": in the first battle a plain Personality's movement to it is negated, a Paragon's is not.
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.CurrentBattleProvince, 0);
        // (player 1, the Defender, has the first opportunity and passes)
        Accept(e, PassC(e));
        Accept(e, Offer(e, 0, "training-move-to-battle", moveCard.Id, plain.Id));
        Eq(e.State.Cards[plain.Id].Zone, Zone.Home, "the movement of a plain Personality was negated");
        Check(Event(e, "moveNegated"), "and the log says it was");
        PassTwice(e);
        Check(e.State.Players[1].Provinces[0].Destroyed, "the first battle destroyed province 0");
        // "You will not destroy more than one of the Defender's provinces": the second win destroys none.
        Accept(e, new("selectBattle", 0, e.Version, Province: 1));
        PassTwice(e);
        Check(!e.State.Players[1].Provinces[1].Destroyed, "the second province survives a victory that would have destroyed it");
        foreach (var other in new[] { 2, 3 }) { Accept(e, new("selectBattle", 0, e.Version, Province: other)); PassTwice(e); }
        Eq(e.State.Phase, Phase.Dynasty, "the additional Attack Phase is over");
        Eq(e.State.ActivePlayerId, 1, "the turn's own player is the active player again");
        Check(e.State.R1.PerfectMoment is null && e.State.Players[1].Provinces.Count(p => p.Destroyed) == 1, "the restrictions ended with it, and exactly one province was destroyed");
        Check(e.State.RemC.RetributionRoot < 0, "no additional Attack Phase is left under way");
    }

    // ---- The Perfect Moment's exception, tested per kind of Personality (issue #494: the scan's "unless they are Paragons or Battle Maidens") ----------------------------------------------------
    // The position: player 1's Attack Phase has ended, player 0 (its Defender) plays The Perfect Moment and holds an additional Attack Phase; `who` is the kind under test (null: a plain Personality).
    private static MatchEngine PerfectMomentPhase(string? who, out CardInstance tested, out CardInstance anchor, out CardInstance moveCard)
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 0;
        s.CurrentBattleProvince = 3; s.UnresolvedBattlefields = [3];
        var card = Add(s, PerfectMomentId, 0, Zone.Hand);
        moveCard = Add(s, "training-move-battle-strategy", 0, Zone.Hand);
        anchor = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Paragon"); Force(anchor, 17);
        tested = Add(s, "training-crane-samurai", 0, Zone.Home); if (who is not null) Kw(tested, who); Force(tested, 17);
        var e = MatchEngine.FromTestState(s);
        PassTwice(e);
        Accept(e, Options(e, 0, "perfect-moment-declare", card.Id).First(o => o.Enabled).Command!);
        return e;
    }

    private static void PerfectMomentExceptions()
    {
        foreach (var (who, free) in new (string?, bool)[] { ("Paragon", true), ("Battle Maiden", true), (null, false) })
        {
            var label = who ?? "a plain Personality";
            // Assigning: two Personalities may attack at once (Retribution's one-Personality limit does not apply), the tested one only if he is a Paragon or a Battle Maiden.
            var e = PerfectMomentPhase(who, out var tested, out var anchor, out _);
            Check(e.State.R1.PerfectMoment is { PlayerId: 0 }, "the restrictions are in force");
            var attack = new GameCommand("attack", 0, e.Version, Assignments: new() { [anchor.Id] = 0, [tested.Id] = 1 });
            if (free) Accept(e, attack); else { Check(Rejection(e, attack).Length > 0, $"{label} may not assign"); Accept(e, new("attack", 0, e.Version, Assignments: new() { [anchor.Id] = 0 })); }
            Eq(e.State.Cards[tested.Id].Zone == Zone.Battlefield, free, $"{label} assigned: {free}");
            // Moving: the tested Personality, at home, moves to the first battle by an effect only if he is a Paragon or a Battle Maiden.
            var m = PerfectMomentPhase(who, out tested, out anchor, out var mover);
            Accept(m, new("attack", 0, m.Version, Assignments: new() { [anchor.Id] = 0 }));
            Accept(m, new("assignDefense", 1, m.Version, Assignments: new()));
            Accept(m, new("attack", 0, m.Version, Assignments: new()));
            Accept(m, new("assignDefense", 1, m.Version, Assignments: new()));
            Accept(m, new("selectBattle", 0, m.Version, Province: 0));
            Accept(m, PassC(m));
            Accept(m, Offer(m, 0, "training-move-to-battle", mover.Id, tested.Id));
            Eq(m.State.Cards[tested.Id].Zone == Zone.Battlefield, free, $"{label} moved: {free}");
            Eq(Event(m, "moveNegated"), !free, $"{label}: the movement was negated: {!free}");
        }
    }

    private static void PublishedShapeWave64()
    {
        var root = RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-64.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.SequenceEqual([PerfectMomentId, TwoFrontsId, ShinobiAssaultId]), "strategy-wave-64's manifest lists the three cards");
        Eq(string.Join(",", PublishedContent.StrategyWave64.Cards.Select(c => c.Id).Order()), string.Join(",", listed), "strategy-wave-64");
        foreach (var id in listed) { Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped"); Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg"); }
        var shinobi = CardCatalog.Get(ShinobiAssaultId);
        Check(shinobi.Type == CardType.Strategy && shinobi.Name == "Shinobi Assault" && shinobi.GoldCost == 4 && shinobi.Focus == 1 && string.Join(",", shinobi.Keywords ?? []) == "Ninja" && shinobi.Abilities!.SequenceEqual(["shinobi-assault-battle"])
            && shinobi.Text == "Limited: Choose your performing unbowed Ninja Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. Assign the target to defend there, even if he is bowed. Fight a battle there, in which you have the first opportunity to take a Battle action or pass. Lose 5 Honor.", "Shinobi Assault's scan");
        var fronts = CardCatalog.Get(TwoFrontsId);
        Check(fronts.Type == CardType.Strategy && fronts.Name == "Two Fronts" && fronts.GoldCost == 0 && fronts.Focus == 2 && (fronts.Keywords ?? []).Length == 0 && fronts.Abilities!.SequenceEqual(["two-fronts-mark"])
            && fronts.Text == "Limited: Target a province whose player has two or more provinces: During your next normal Attack Phase (this turn), create two battlefields associated with it instead of one.", "Two Fronts' scan");
        var moment = CardCatalog.Get(PerfectMomentId);
        Check(moment.Type == CardType.Strategy && moment.Name == "The Perfect Moment" && moment.GoldCost == 0 && moment.Focus == 4 && (moment.Keywords ?? []).Length == 0 && moment.Abilities!.SequenceEqual(["perfect-moment-declare"])
            && moment.Text.StartsWith("Reaction: After the end of an Attack Phase in which you were the Defender:") && moment.Text.EndsWith("unless they are Paragons or Battle Maidens."), "The Perfect Moment's scan");
    }
}
