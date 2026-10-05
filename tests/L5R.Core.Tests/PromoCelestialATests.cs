using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// personality-wave-9101: six Promotional-Celestial Personalities (Jahan, Kamalakar, Masatane, Miya Masatsuko, Tashiki, Yotsu Ueda), wired from their scans' printed text. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers. The readings are in PromoCelestialA.cs.
internal static class PromoCelestialATests
{
    private const string Jahan = "oracle-l5r-4039", Kamalakar = "oracle-l5r-4253", Masatane = "oracle-l5r-4852", Masatsuko = "oracle-l5r-5198", Tashiki = "oracle-l5r-7822", Ueda = "oracle-l5r-9680";
    private const string ImperialProtection = "oracle-l5r-10152", UnstableGround = "oracle-l5r-9942";
    private static readonly string[] AllCards = [Jahan, Kamalakar, Masatane, Masatsuko, Tashiki, Ueda];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    public static void Run(TestRunner runner) => runner.Run("promo-celestial-a", [
        ("Published shape: the six cards print their scans' stats, keywords and text, ship their art, come from Promotional-Celestial and are in the wave manifest", PublishedShape),
        ("Masatane: an Iaijutsu Battle action destroys an enemy attachment; your own attachment, an enemy Personality or a card with no attachment is refused without a change", MasataneScenario),
        ("Tashiki: bowing himself straightens a Market or Port Holding (either player's); a bowed Tashiki, another Holding or a Personality is refused", TashikiScenario),
        ("Miya Masatsuko: she discards the Imperial Favor as her cost and her controller draws one additional card in this turn's End Phase; refused without the Favor, and nothing is drawn by the refusal", MasatsukoScenario),
        ("Yotsu Ueda: destroys an enemy Follower or Personality without Followers whose Force is 4 or lower; Force 5, a Personality with a Follower, or one of your own is refused", UedaScenario),
        ("Kamalakar: the first Political action each turn that targets him is negated whole (costs paid), the second the same turn is not, the next turn starts afresh, other actions and other Personalities are untouched", KamalakarTraitScenario),
        ("Kamalakar: his Battle action moves him home, or to another battlefield that has enemy units; a battlefield without enemy units or the current one is refused", KamalakarMoveScenario),
        ("Jahan: while he is at the current battlefield another player may not play a Terrain (by command, in the action list, or through its own ability); his controller may, and he elsewhere bars nothing", JahanScenario),
    ]);

    private static void Unchanged(MatchEngine e, GameCommand command, string why)
    {
        var before = e.Version;
        Check(!e.Execute(command).Accepted, why + " (should be refused)");
        Eq(e.Version, before, why + ": nothing changed");
    }

    private static void Decline(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------------------
    private static void Stats(string id, string name, int gold, int force, int chi, int honor, int? requirement, string[] keywords, string[] abilities, string effect, string text)
    {
        var d = CardCatalog.Get(id);
        Check(d.Type == CardType.Personality && d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.PersonalHonor == honor && d.HonorRequirement == requirement && d.Clan == "Unaligned" && !d.ScenarioOnly, $"{name}: stats");
        Check((d.Keywords ?? []).Order(StringComparer.Ordinal).SequenceEqual(keywords.Order(StringComparer.Ordinal)), $"{name}: keywords {string.Join(",", d.Keywords ?? [])}");
        Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", d.Abilities ?? [])}");
        Eq(d.Effect, effect, $"{name}: effect");
        Eq(d.Text, text, $"{name}: text");
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        Stats(Jahan, "Jahan", 3, 3, 2, 2, null, ["Gaijin", "Kshatriya", "Scout"], [], "promo-jahan-no-terrain", "While Jahan is at the current battlefield, other players may not play Terrains.");
        Stats(Kamalakar, "Kamalakar", 10, 4, 4, 3, 0, ["Cavalry", "Tactician", "Unique", "Courtier", "Gaijin", "Kshatriya"], ["kamalakar-move-home", "kamalakar-move-battlefield"], "promo-kamalakar-political-shield",
            "After the first time each turn a Political action targets Kamalakar: Negate its effects.\nBattle: Kamalakar confounds the enemy. Move Kamalakar home or to a battlefield with enemy units.");
        Stats(Masatane, "Masatane", 5, 4, 3, 0, null, ["Duelist", "Kensai", "Ronin", "Samurai"], ["masatane-destroy"], "none", "Iaijutsu Battle: Target an enemy attachment: Destroy it.");
        Stats(Masatsuko, "Miya Masatsuko", 5, 0, 3, 3, 1, ["Singular", "Courtier", "Herald", "Imperial"], ["masatsuko-draw"], "none",
            "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase.");
        Stats(Tashiki, "Tashiki", 3, 0, 3, 1, null, ["Merchant", "Settler"], ["tashiki-straighten"], "none", "Open: Bow Tashiki and target a Market or Port Holding: Straighten it.");
        Stats(Ueda, "Yotsu Ueda", 7, 5, 3, 2, 0, ["Kensai", "Commander", "Gunso", "Samurai"], ["ueda-destroy"], "none",
            "Battle: Target an enemy Follower or Personality without Followers with Force equal to or lower than 4: Destroy it.");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", "personality-wave-9101.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual(AllCards.Order(StringComparer.Ordinal)), "the wave manifest lists exactly the six cards");
        foreach (var id in AllCards)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var rows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", "personality-wave-9101-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(rows.Length, 6, "six provenance rows");
        foreach (var row in rows) Eq(row.GetProperty("printing_set")[0].GetString(), "Promotional&ndash;Celestial", $"{row.GetProperty("card_id")} printing");
        var script = File.ReadAllText(Path.Combine(root, "tools/content/package_personality_wave_9101.py"));
        Check(script.Contains("PROFILE = \"personality-wave-9101\"") && script.Contains("prefer_slug=SET_SLUG"), "the rerunnable packager of the wave");
        // The abilities carry the printed designators, costs and action keywords.
        Check(AbilityCatalog.All["masatane-destroy"].ActionKeywords!.Contains("Iaijutsu"), "Masatane's action is an Iaijutsu one");
        Check(AbilityCatalog.All["masatsuko-draw"].Costs!.Any(c => c.Verb == CostVerbs.DiscardFavor) && AbilityCatalog.All["masatsuko-draw"].ActionKeywords!.SequenceEqual(["Political"]), "Masatsuko: Political, the Favor discard a cost");
        Check(AbilityCatalog.All["tashiki-straighten"].Costs!.Any(c => c.Verb == CostVerb.Bow), "Tashiki bows himself as the cost");
        Eq(AbilityCatalog.All["kamalakar-move-home"].Move!.Destination, MoveSpec.Home); Eq(AbilityCatalog.All["kamalakar-move-battlefield"].Move!.Destination, MoveSpec.AnyUnresolved);
    }

    // ---- Masatane -------------------------------------------------------------------------------------------------------------------------------------------------
    private static void MasataneScenario()
    {
        var s = Table(Phase.Battle, 0);
        var m = Add(s, Masatane, 0, Zone.Battlefield, province: 0);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyAttachment = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: foe.Id);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: m.Id);
        var e = MatchEngine.FromTestState(s);
        Unchanged(e, Use(e, 0, "masatane-destroy", m.Id, mine.Id), "your own attachment is refused");
        Unchanged(e, Use(e, 0, "masatane-destroy", m.Id, foe.Id), "an enemy Personality is not an attachment");
        Unchanged(e, Use(e, 0, "masatane-destroy", m.Id, bare.Id), "a card with no attachment is refused");
        Accept(e, Offer(e, 0, "masatane-destroy", m.Id, enemyAttachment.Id)); Decline(e);
        Eq(e.State.Cards[enemyAttachment.Id].Zone, Zone.FateDiscard, "the enemy attachment was destroyed");
        Eq(e.State.Cards[foe.Id].Zone, Zone.Battlefield, "its host lives");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Attached, "my own attachment is untouched");
    }

    // ---- Tashiki --------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TashikiScenario()
    {
        var s = Table(Phase.Action, 0);
        var t = Add(s, Tashiki, 0, Zone.Home);
        var market = Add(s, "training-market-holding", 0, Zone.Home, bowed: true);
        var enemyMarket = Add(s, "training-market-holding", 1, Zone.Home, bowed: true);
        var other = Add(s, "training-marketplace", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Unchanged(e, Use(e, 0, "tashiki-straighten", t.Id, other.Id), "a Holding that is neither Market nor Port is refused");
        Unchanged(e, Use(e, 0, "tashiki-straighten", t.Id, t.Id), "a Personality is not a Holding");
        Accept(e, Offer(e, 0, "tashiki-straighten", t.Id, market.Id)); Decline(e);
        Check(!e.State.Cards[market.Id].Bowed, "the Market was straightened");
        Check(e.State.Cards[t.Id].Bowed, "Tashiki bowed as the cost");
        Check(e.State.Cards[other.Id].Bowed, "the other Holding is untouched");
        Check(!CanUse(e, 0, "tashiki-straighten", t.Id), "a bowed Tashiki may not act again");

        // Either player's Market works.
        var u = Table(Phase.Action, 0); var t2 = Add(u, Tashiki, 0, Zone.Home); var em = Add(u, "training-market-holding", 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(u);
        Accept(e2, Offer(e2, 0, "tashiki-straighten", t2.Id, em.Id)); Decline(e2);
        Check(!e2.State.Cards[em.Id].Bowed, "an enemy Market was straightened");
        _ = enemyMarket;

        // A bowed Tashiki cannot pay the bow.
        var v = Table(Phase.Action, 0); var t3 = Add(v, Tashiki, 0, Zone.Home, bowed: true); Add(v, "training-market-holding", 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(v), 0, "tashiki-straighten", t3.Id), "a bowed Tashiki is not offered the action");
    }

    // ---- Miya Masatsuko -------------------------------------------------------------------------------------------------------------------------------------------
    private static void MasatsukoScenario()
    {
        var s = Table(Phase.Action, 0); s.FavorPlayerId = 0;
        var m = Add(s, Masatsuko, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.ExtraEndPhaseDraws[0], 0, "no extra draw owed yet");
        Accept(e, Enabled(e, 0, "masatsuko-draw", m.Id).Command!); Decline(e);
        Check(e.State.FavorPlayerId != 0, "the Imperial Favor was discarded as the cost");
        Eq(e.State.ExtraEndPhaseDraws[0], 1, "one additional card is owed in the End Phase");
        Eq(e.State.ExtraEndPhaseDraws[1], 0, "and the opponent owes none");

        // Without the Favor (the opponent holds it, or nobody) the action is refused without a change.
        foreach (var holder in new int?[] { 1, null })
        {
            var t = Table(Phase.Action, 0); t.FavorPlayerId = holder; var m2 = Add(t, Masatsuko, 0, Zone.Home);
            var te = MatchEngine.FromTestState(t);
            Check(!CanUse(te, 0, "masatsuko-draw", m2.Id), $"not offered without the Favor (holder {holder?.ToString() ?? "nobody"})");
            Unchanged(te, Use(te, 0, "masatsuko-draw", m2.Id), "refused without the Favor");
            Eq(te.State.ExtraEndPhaseDraws[0], 0, "nothing was queued");
        }
    }

    // ---- Yotsu Ueda -----------------------------------------------------------------------------------------------------------------------------------------------
    private static void UedaScenario()
    {
        var s = Table(Phase.Battle, 0);
        var u = Add(s, Ueda, 0, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(strong, 10);
        var guarded = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: guarded.Id);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var weakForce = e.CardForceForTest(weak.Id);
        Check(weakForce <= 4 && e.CardForceForTest(strong.Id) > 4 && e.CardForceForTest(guarded.Id) <= 4, "fixture Forces: weak and guarded at most 4, strong above");
        Unchanged(e, Use(e, 0, "ueda-destroy", u.Id, strong.Id), "a Personality with Force above 4 is refused");
        Unchanged(e, Use(e, 0, "ueda-destroy", u.Id, guarded.Id), "a Personality with a Follower is refused");
        Unchanged(e, Use(e, 0, "ueda-destroy", u.Id, mine.Id), "one of your own is refused");
        Accept(e, Offer(e, 0, "ueda-destroy", u.Id, weak.Id)); Decline(e);
        Check(e.State.Cards[weak.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the weak enemy Personality was destroyed");

        // A Follower of Force 4 or lower is a legal target; the Force cap reads current Force (a +5F boost makes it illegal).
        var t = Table(Phase.Battle, 0); var u2 = Add(t, Ueda, 0, Zone.Battlefield, province: 0);
        var host = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var f = Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var te = MatchEngine.FromTestState(t);
        Check(te.CardForceForTest(f.Id) <= 4, "the fixture Follower is within the cap");
        Accept(te, Offer(te, 0, "ueda-destroy", u2.Id, f.Id)); Decline(te);
        Eq(te.State.Cards[f.Id].Zone, Zone.FateDiscard, "the enemy Follower was destroyed");
        // The boundary: Force exactly 4 is legal, exactly 5 is not (the current Force is what counts).
        foreach (var (target, legal) in new[] { (4, true), (5, false) })
        {
            var w = Table(Phase.Battle, 0); var u3 = Add(w, Ueda, 0, Zone.Battlefield, province: 0); var edge = Add(w, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var we = MatchEngine.FromTestState(w); var delta = target - we.CardForceForTest(edge.Id);
            we = Batch336Support.Edit(we, st => st.Cards[edge.Id].Modifiers.Add(new("test-source", "test", "Force", delta)));
            Eq(we.CardForceForTest(edge.Id), target, "fixture Force");
            if (legal) { Accept(we, Offer(we, 0, "ueda-destroy", u3.Id, edge.Id)); Decline(we); Check(we.State.Cards[edge.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "Force 4 was destroyed"); }
            else Unchanged(we, Use(we, 0, "ueda-destroy", u3.Id, edge.Id), "Force 5 is refused");
        }
    }

    // ---- Kamalakar: the trait -------------------------------------------------------------------------------------------------------------------------------------
    private static void KamalakarTraitScenario()
    {
        var s = Table(Phase.Action, 0);
        var k = Add(s, Kamalakar, 1, Zone.Home);
        var other = Add(s, "training-lion-samurai", 1, Zone.Home);
        var p1 = Add(s, ImperialProtection, 0, Zone.Hand); var p2 = Add(s, ImperialProtection, 0, Zone.Hand); var p3 = Add(s, ImperialProtection, 0, Zone.Hand); var p4 = Add(s, ImperialProtection, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // A Political action against a Personality without the trait is untouched.
        Accept(e, Offer(e, 0, "imperial-protection-watch", p1.Id, other.Id)); Decline(e);
        Check(e.State.AssignmentWatches.Any(w => w.CardId == other.Id), "a Personality without the trait is targeted normally");
        Check(!e.State.Events.Any(x => x.Type == "actionNegated"), "nothing negated yet");

        // The first Political action of the turn that targets Kamalakar is negated whole; the card is still played (its costs are paid).
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, "imperial-protection-watch", p2.Id, k.Id)); Decline(e);
        Check(!e.State.AssignmentWatches.Any(w => w.CardId == k.Id), "its effects were negated: no watch on him");
        Check(e.State.Cards[p2.Id].Zone != Zone.Hand, "the Strategy was still played");
        Check(e.State.Events.Any(x => x.Type == "actionNegated" && x.Message.Contains("Kamalakar")), "Kamalakar's negation is on the record");
        // The second one the same turn goes through.
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, "imperial-protection-watch", p3.Id, k.Id)); Decline(e);
        Check(e.State.AssignmentWatches.Any(w => w.CardId == k.Id), "the second Political action this turn resolved");
        // The next turn starts afresh.
        var later = Batch336Support.Edit(e, st => { st.TurnNumber++; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        var watches = later.State.AssignmentWatches.Count;
        Accept(later, Offer(later, 0, "imperial-protection-watch", p4.Id, k.Id)); Decline(later);
        Eq(later.State.AssignmentWatches.Count, watches, "negated again the next turn: no new watch");
        Check(later.State.Events.Count(x => x.Type == "actionNegated" && x.Message.Contains("Kamalakar")) == 2, "twice in all");

        // A non-Political action does not spend the turn's negation, and his own controller's Political action is negated too (the text prints no "another player's").
        var t = Table(Phase.Battle, 0); var tk = Add(t, Kamalakar, 1, Zone.Battlefield, province: 0);
        var src = Add(t, "training-penalty-source", 0, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(t); var force = te.CardForceForTest(tk.Id);
        Accept(te, Offer(te, 0, "training-penalty-force", src.Id, tk.Id)); Decline(te);
        Eq(te.CardForceForTest(tk.Id), force - 2, "a non-Political action resolves as always");
        Check(!te.State.Events.Any(x => x.Type == "actionNegated"), "and Kamalakar did not act");
        var own = Table(Phase.Action, 0); var ok = Add(own, Kamalakar, 0, Zone.Home); var op = Add(own, ImperialProtection, 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(own);
        Accept(oe, Offer(oe, 0, "imperial-protection-watch", op.Id, ok.Id)); Decline(oe);
        Check(!oe.State.AssignmentWatches.Any(w => w.CardId == ok.Id), "his own controller's Political action is negated as well");
    }

    // ---- Kamalakar: his Battle action -----------------------------------------------------------------------------------------------------------------------------
    private static void KamalakarMoveScenario()
    {
        var s = Table(Phase.Battle, 0);
        s.UnresolvedBattlefields = [0, 1, 2];
        var k = Add(s, Kamalakar, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        // Home.
        var home = Enabled(e, 0, "kamalakar-move-home", k.Id);
        Accept(e, home.Command!);
        Eq(e.State.Cards[k.Id].Zone, Zone.Home, "he moved home");

        // To a battlefield with enemy units: only the one that has them is offered; one without, and the current one, are refused.
        var t = Table(Phase.Battle, 0); t.UnresolvedBattlefields = [0, 1, 2];
        var k2 = Add(t, Kamalakar, 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var te = MatchEngine.FromTestState(t);
        var offered = Options(te, 0, "kamalakar-move-battlefield", k2.Id).Where(a => a.Enabled).SelectMany(a => a.Provinces ?? []).Distinct().ToArray();
        Check(offered.SequenceEqual([1]), "only the battlefield with enemy units is offered: " + string.Join(",", offered));
        var option = Options(te, 0, "kamalakar-move-battlefield", k2.Id).First(a => a.Enabled);
        Unchanged(te, option.Command! with { Province = 2 }, "a battlefield without enemy units is refused");
        Unchanged(te, option.Command! with { Province = 0 }, "the current battlefield is refused");
        Unchanged(te, option.Command! with { Province = null }, "a destination must be named");
        Accept(te, option.Command! with { Province = 1 });
        Eq(te.State.Cards[k2.Id].Zone, Zone.Battlefield, "he is still at a battlefield"); Eq(te.State.Cards[k2.Id].ProvinceIndex, 1, "now at the one with enemy units");
    }

    // ---- Jahan ----------------------------------------------------------------------------------------------------------------------------------------------------
    private static void JahanScenario()
    {
        // A Terrain in the attacker's hand, Jahan (the defender's) at the current battlefield: refused, listed with the reason, nothing paid.
        var s = Table(Phase.Battle, 1);
        Add(s, Jahan, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, "training-terrain", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var play = e.QueryActions(1).Where(a => a.Type == "playStrategy" && a.CardId == terrain.Id).ToArray();
        Check(play.Length == 1 && !play[0].Enabled && (play[0].Reason ?? "").Contains("Jahan"), "the Terrain is listed disabled for Jahan: " + string.Join(";", play.Select(a => a.Reason)));
        Unchanged(e, new GameCommand("playStrategy", 1, e.Version, terrain.Id), "playing the Terrain is refused");
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Hand, "the Terrain stays in the hand");

        // Jahan elsewhere (home) bars nothing.
        var t = Table(Phase.Battle, 1); Add(t, Jahan, 0, Zone.Home); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain2 = Add(t, "training-terrain", 1, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, new GameCommand("playStrategy", 1, te.Version, terrain2.Id));
        Eq(te.State.Cards[terrain2.Id].Zone, Zone.Battlefield, "Jahan at home: the Terrain was played");

        // His own controller may play a Terrain with Jahan at the battlefield.
        var u = Table(Phase.Battle, 0); Add(u, Jahan, 0, Zone.Battlefield, province: 0);
        var terrain3 = Add(u, "training-terrain", 0, Zone.Hand);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, new GameCommand("playStrategy", 0, ue.Version, terrain3.Id));
        Eq(ue.State.Cards[terrain3.Id].Zone, Zone.Battlefield, "Jahan's controller plays a Terrain");

        // A Terrain played through its own ability from the hand (Unstable Ground) is a Terrain play too.
        foreach (var jahanThere in new[] { true, false })
        {
            var v = Table(Phase.Battle, 1);
            if (jahanThere) Add(v, Jahan, 0, Zone.Battlefield, province: 0);
            var scout = Kw(Add(v, "training-crane-samurai", 1, Zone.Battlefield, province: 0), "Scout");
            var victim = Add(v, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var ground = Add(v, UnstableGround, 1, Zone.Hand);
            var ve = MatchEngine.FromTestState(v);
            var options = Options(ve, 1, "unstable-ground-destroy", ground.Id).Where(a => a.Enabled && (a.TargetIds?.Contains(victim.Id) ?? false)).ToArray();
            if (jahanThere)
            {
                Check(options.Length == 0, "Unstable Ground is not offered while Jahan is at the battlefield");
                var forced = Use(ve, 1, "unstable-ground-destroy", ground.Id, victim.Id, scout.Id);
                Unchanged(ve, forced, "Unstable Ground is refused while Jahan is at the battlefield");
                Eq(ve.State.Cards[ground.Id].Zone, Zone.Hand, "it stays in the hand");
            }
            else
            {
                Check(options.Length > 0, "without Jahan the same position offers Unstable Ground (so the refusal above is his)");
            }
        }
    }
}
