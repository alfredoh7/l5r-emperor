using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #674, cluster strat-gates: strategy-wave-119, eighteen Gates of Chaos Strategies (src/L5R.Core/WlStratGates.cs). Every scenario is an explicit position on the economy table (player 0 the
// Crane, player 1 the Lion), driven only through QueryActions and the commands it offers; a refused command must leave the whole state unchanged.
internal static class WlStratGatesTests
{
    private const string Allied = "oracle-l5r-10738", Boiling = "oracle-l5r-10717", Chaos = "oracle-l5r-10716", Ruins = "oracle-l5r-10743", Formation = "oracle-l5r-10721", Farmlands = "oracle-l5r-10727",
        Friends = "oracle-l5r-10740", Rage = "oracle-l5r-10725", Dojo = "oracle-l5r-10722", Ivinda = "oracle-l5r-10729", Caravan = "oracle-l5r-10737", Mukudori = "oracle-l5r-10728",
        Nubane = "oracle-l5r-10726", Recovering = "oracle-l5r-10719", Setback = "oracle-l5r-10747", FalseRoute = "oracle-l5r-10744", FinalLesson = "oracle-l5r-10749", Tomb = "oracle-l5r-10750";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", CraneVeteran = "training-crane-veteran", LionVeteran = "training-lion-veteran";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void Strong(CardInstance card) { foreach (var stat in new[] { "Force", "Chi", "PersonalHonor" }) card.Modifiers.Add(new("test-source", "test", stat, 20)); }
    private static int Force(MatchEngine e, CardInstance card) => e.CardForceForTest(card.Id);
    private static int Mod(MatchEngine e, CardInstance card, string stat) => e.State.Cards[card.Id].Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);
    private static string Snapshot(MatchEngine e) => JsonSerializer.Serialize(e.State);

    // A refused command: the engine says no and the position is exactly as it was.
    private static void Reject(MatchEngine e, GameCommand command, string why)
    {
        var before = Snapshot(e);
        var result = e.Execute(command);
        Check(!result.Accepted, $"refused: {why}");
        Eq(Snapshot(e), before, $"a refused command changes nothing: {why}");
    }

    // The option the engine lists (with its suggested payment), retargeted or re-selected the way the client does.
    private static GameCommand Take(MatchEngine e, int player, string ability, CardInstance card, string? target = null, string? performer = null, params SlotSelection[] selections)
    {
        var option = Enabled(e, player, ability, card.Id, performer);
        var command = option.Command!;
        if (selections.Length > 0) return command with { Selections = selections, TargetId = selections[0].CardIds?.FirstOrDefault() };
        return target is null ? command : command with { TargetId = target };
    }

    // Player 0 attacks province 0 (priority 0) unless defender is set: player 1 attacks and player 0, the defender, has priority.
    private static MatchState Battle(bool defender = false)
    {
        var s = Table(Phase.Battle, defender ? 1 : 0);
        s.PriorityPlayerId = 0;
        return s;
    }

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-119.txt"));
        var expected = new (string Id, string Name, int Gold, int Focus, string[] Keywords, string[] Abilities)[]
        {
            (Allied, "Allied Efforts", 0, 3, [], ["spitting-llama-force"]),
            (Boiling, "Boiling Point", 0, 4, [], ["wl-sg-boiling-focus", "wl-sg-boiling-bow-straighten"]),
            (Chaos, "Chaos of Battle", 0, 2, [], ["wl-sg-chaos-of-battle"]),
            (Ruins, "Claiming the Ruins", 2, 2, [], ["wl-sg-claiming-ruins"]),
            (Formation, "Defensive Formation", 0, 3, [], ["wl-sg-defensive-formation"]),
            (Farmlands, "Exotic Farmlands", 0, 1, [], ["wl-sg-exotic-farmlands"]),
            (Friends, "Friends in the Jungle", 1, 1, [], ["wl-sg-friends-jungle"]),
            (Rage, "Honor Replaces Rage", 0, 3, [], ["wl-sg-honor-replaces-rage"]),
            (Dojo, "Iaijutsu Dojo", 0, 3, ["Iaijutsu"], ["wl-sg-dojo-duel"]),
            (Ivinda, "Ivinda Village", 0, 4, ["Kharmic"], ["wl-sg-ivinda-cavalry"]),
            (Caravan, "Lost Caravan", 3, 2, [], ["wl-sg-lost-caravan"]),
            (Mukudori, "Mukudori's Wisdom", 0, 3, ["Kharmic"], ["wl-sg-mukudori-straighten", "wl-sg-mukudori-bow"]),
            (Nubane, "Nubane's Defense", 0, 2, [], ["wl-sg-nubanes-defense"]),
            (Recovering, "Recovering What Was Lost", 0, 4, [], ["wl-sg-recovering-lost"]),
            (Setback, "Tactical Setback", 0, 2, [], ["wl-sg-tactical-setback"]),
            (FalseRoute, "The False Route", 0, 2, [], ["wl-sg-false-route"]),
            (FinalLesson, "The Final Lesson", 0, 4, [], ["wl-sg-final-lesson"]),
            (Tomb, "The Tomb of Sun Tao", 0, 4, ["Kharmic"], ["wl-sg-tomb-sun-tao"]),
        };
        Eq(listed.Length, 18, "the manifest pins eighteen cards");
        foreach (var (id, name, gold, focus, keywords, abilities) in expected)
        {
            Check(listed.Contains(id), $"{name} is in the manifest");
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{name} ships art");
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Strategy && c.Name == name && c.GoldCost == gold && c.Focus == focus && !c.ScenarioOnly, $"{name}: printed stats");
            Check((c.Keywords ?? []).SequenceEqual(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Check(c.Abilities!.SequenceEqual(abilities), $"{name}: abilities");
            Check(c.Text.Length > 0 && c.Effect == "none", $"{name}: printed text, no effect key");
            foreach (var ability in abilities) Check(AbilityCatalog.All.ContainsKey(ability), $"{ability} is catalogued");
        }
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published/strategy-wave-119-sources.json"))).RootElement.EnumerateArray()
            .Select(row => row.GetProperty("printing_set")[0].GetString()).ToArray();
        Check(sources.Length == 18 && sources.All(set => set == "Gates of Chaos"), "every printing is Gates of Chaos");
        var focusGrid = new[] { ("wl-sg-boiling-focus", "FocusEffect"), ("wl-sg-ivinda-cavalry", "Open"), ("wl-sg-recovering-lost", "Reaction"), ("wl-sg-mukudori-bow", "BattleOrOpen") };
        foreach (var (id, timing) in focusGrid) Eq(AbilityCatalog.All[id].Timing, timing, $"{id} timing");
    }

    // ---- Allied Efforts, Tactical Setback ----------------------------------------------------------------------------------------------------------------------------------

    private static void AlliedAndSetback()
    {
        var s = Battle();
        var mine = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); var theirs = Kw(Add(s, LionSamurai, 1, Zone.Battlefield, province: 0), "Test");
        Strong(theirs);
        var allied = Add(s, Allied, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Force(e, theirs);
        Check(!Options(e, 0, "spitting-llama-force", allied.Id).Any(o => o.TargetIds?.Contains(mine.Id) ?? false), "one of my own is not a target");
        Reject(e, Use(e, 0, "spitting-llama-force", allied.Id, mine.Id), "Allied Efforts on my own Personality");
        Accept(e, Offer(e, 0, "spitting-llama-force", allied.Id, theirs.Id));
        Eq(Force(e, theirs), before - 3, "Allied Efforts gives the enemy -3F");
        Eq(e.State.Cards[allied.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");

        // Absent Battle: a player with no unit at the battlefield may take it, and may not take a plain Battle action.
        var t = Battle(defender: true);
        var attacker = Strong2(Add(t, LionSamurai, 1, Zone.Battlefield, province: 0));
        var home = Add(t, CraneSamurai, 0, Zone.Home);
        var setback = Add(t, Setback, 0, Zone.Hand); var allied2 = Add(t, Allied, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var attackerForce = Force(f, attacker);
        Check(!CanUse(f, 0, "spitting-llama-force", allied2.Id), "a plain Battle action needs presence at the battlefield");
        Check(!Options(f, 0, "wl-sg-tactical-setback", setback.Id).Any(o => o.TargetIds?.Contains(home.Id) ?? false), "my own Personality is no target");
        Accept(f, Offer(f, 0, "wl-sg-tactical-setback", setback.Id, attacker.Id));
        Eq(Force(f, attacker), attackerForce - 2, "Tactical Setback gives the enemy -2F without presence");
        Eq(f.State.Cards[setback.Id].Zone, Zone.FateDiscard, "discarded");
    }

    private static CardInstance Strong2(CardInstance card) { Strong(card); return card; }

    // ---- Defensive Formation -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void DefensiveFormation()
    {
        var s = Battle(defender: true);
        var d1 = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); var d2 = Add(s, CraneVeteran, 0, Zone.Battlefield, province: 0); var d3 = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var d4 = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card = Add(s, Formation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var forces = new[] { d1, d2, d3, d4 }.Select(c => Force(e, c)).ToArray();
        Reject(e, Take(e, 0, "wl-sg-defensive-formation", card, selections: Pick("targets", d1.Id, d2.Id, d3.Id, d4.Id)), "four targets");
        Reject(e, Take(e, 0, "wl-sg-defensive-formation", card, selections: Pick("targets", d1.Id, enemy.Id)), "an attacker");
        Reject(e, Take(e, 0, "wl-sg-defensive-formation", card, selections: Pick("targets")), "no target");
        Accept(e, Take(e, 0, "wl-sg-defensive-formation", card, selections: Pick("targets", d1.Id, d2.Id, d3.Id)));
        Eq(Force(e, d1), forces[0] + 2, "first +2F"); Eq(Force(e, d2), forces[1] + 2, "second +2F"); Eq(Force(e, d3), forces[2] + 2, "third +2F");
        Eq(Force(e, d4), forces[3], "the fourth is untouched");

        // The attacker has no defending Personality: the action is not available to him.
        var t = Battle();
        Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var attackerCard = Add(t, Formation, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, "wl-sg-defensive-formation", attackerCard.Id), "an attacking player has no defending Personality to target");
    }

    // ---- Nubane's Defense, Chaos of Battle ---------------------------------------------------------------------------------------------------------------------------------

    private static void NubaneAndChaos()
    {
        var s = Battle();
        var a = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); var b = Add(s, CraneVeteran, 0, Zone.Battlefield, province: 0); var c3 = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        Add(s, "training-ring-of-air", 0, Zone.Home); Add(s, "training-ring-of-water", 0, Zone.Home); Add(s, "training-ring-of-void", 1, Zone.Home); // two of mine, one of his
        var card = Add(s, Nubane, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var fa = Force(e, a); var fb = Force(e, b); var fc = Force(e, c3);
        Reject(e, Take(e, 0, "wl-sg-nubanes-defense", card, selections: Pick("targets", a.Id, b.Id, c3.Id)), "three targets");
        Accept(e, Take(e, 0, "wl-sg-nubanes-defense", card, selections: Pick("targets", a.Id, b.Id)));
        Eq(Force(e, a), fa + 2, "+1F for each of my two Rings"); Eq(Force(e, b), fb + 2, "both targets"); Eq(Force(e, c3), fc, "a Personality not chosen is untouched");

        // No Ring: the action still resolves, for +0F.
        var t = Battle();
        var x = Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card2 = Add(t, Nubane, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var fx = Force(f, x);
        Accept(f, Take(f, 0, "wl-sg-nubanes-defense", card2, selections: Pick("targets", x.Id)));
        Eq(Force(f, x), fx, "no Rings, no bonus");

        // Chaos of Battle: Crane and Lion at the battlefield are two different Clan alignments; each of one or two opposed Personalities gets +2F.
        var u = Battle();
        var mine = Add(u, CraneSamurai, 0, Zone.Battlefield, province: 0); var theirs = Add(u, LionSamurai, 1, Zone.Battlefield, province: 0); var mine2 = Add(u, CraneVeteran, 0, Zone.Battlefield, province: 0);
        var chaos = Add(u, Chaos, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        var fm = Force(g, mine); var ft = Force(g, theirs); var fm2 = Force(g, mine2);
        Reject(g, Take(g, 0, "wl-sg-chaos-of-battle", chaos, selections: Pick("targets", mine.Id, theirs.Id, mine2.Id)), "three targets");
        Accept(g, Take(g, 0, "wl-sg-chaos-of-battle", chaos, selections: Pick("targets", mine.Id, theirs.Id)));
        Eq(Force(g, mine), fm + 2, "my Personality: two alignments"); Eq(Force(g, theirs), ft + 2, "the enemy Personality too"); Eq(Force(g, mine2), fm2, "not chosen");

        // Unopposed Personalities are not targets.
        var v = Battle();
        var lone = Add(v, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var chaos2 = Add(v, Chaos, 0, Zone.Hand);
        var h = MatchEngine.FromTestState(v);
        Check(!CanUse(h, 0, "wl-sg-chaos-of-battle", chaos2.Id), "nobody is opposed: no target");
        _ = lone;
        // One alignment only: the bonus is +1.
        var w = Battle();
        var p = Add(w, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(w, CraneVeteran, 1, Zone.Battlefield, province: 0);
        var chaos3 = Add(w, Chaos, 0, Zone.Hand);
        var k = MatchEngine.FromTestState(w);
        var fp = Force(k, p);
        Accept(k, Take(k, 0, "wl-sg-chaos-of-battle", chaos3, selections: Pick("targets", p.Id)));
        Eq(Force(k, p), fp + 1, "both are Crane: one alignment");
    }

    // ---- Friends in the Jungle ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void FriendsInTheJungle()
    {
        var s = Battle();
        var beast = Kw(Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0), "Nonhuman"); var plain = Add(s, CraneVeteran, 0, Zone.Battlefield, province: 0);
        Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card = Add(s, Friends, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var force = Force(e, beast); var chi = Mod(e, beast, "Chi");
        Check(!Options(e, 0, "wl-sg-friends-jungle", card.Id).Any(o => o.TargetIds?.Contains(plain.Id) ?? false), "a Human is not a target");
        Reject(e, Use(e, 0, "wl-sg-friends-jungle", card.Id, plain.Id), "a Human Personality");
        var command = Offer(e, 0, "wl-sg-friends-jungle", card.Id, beast.Id);
        Check(command.SourceIds is { Length: > 0 }, "it costs 1 Gold, paid from a source");
        Accept(e, command);
        Eq(Force(e, beast), force + 2, "+2F"); Eq(Mod(e, beast, "Chi"), chi + 2, "+2C");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");

        // The Open half: in the Action Phase, a Nonhuman Personality at home.
        var t = Table(Phase.Action, 0);
        var home = Kw(Add(t, CraneSamurai, 0, Zone.Home), "Nonhuman");
        var card2 = Add(t, Friends, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var hf = Force(f, home);
        Accept(f, Offer(f, 0, "wl-sg-friends-jungle", card2.Id, home.Id));
        Eq(Force(f, home), hf + 2, "the Open action works at home");
    }

    // ---- Boiling Point, Iaijutsu Dojo --------------------------------------------------------------------------------------------------------------------------------------

    private static void BoilingPointBattle()
    {
        var s = Battle();
        var present = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var home = Add(s, CraneVeteran, 0, Zone.Home);
        var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0, bowed: true);
        var card = Add(s, Boiling, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Take(e, 0, "wl-sg-boiling-bow-straighten", card, selections: [Pick("bow", enemy.Id), Pick("straighten", present.Id)]), "bowing an enemy");
        Reject(e, Take(e, 0, "wl-sg-boiling-bow-straighten", card, selections: [Pick("bow", home.Id), Pick("straighten", home.Id)]), "the same Personality for both");
        Accept(e, Take(e, 0, "wl-sg-boiling-bow-straighten", card, selections: [Pick("bow", home.Id), Pick("straighten", enemy.Id)]));
        Check(e.State.Cards[home.Id].Bowed, "my Personality at home (any location) was bowed");
        Check(!e.State.Cards[enemy.Id].Bowed, "the other Personality was straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");

        // An already bowed Personality may not be the one to bow.
        var t = Battle();
        var bowedOne = Add(t, CraneVeteran, 0, Zone.Home, bowed: true); Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0); var other = Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card2 = Add(t, Boiling, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Reject(f, Take(f, 0, "wl-sg-boiling-bow-straighten", card2, selections: [Pick("bow", bowedOne.Id), Pick("straighten", other.Id)]), "bowing a bowed Personality");
    }

    private static (MatchEngine E, CardInstance Mine, CardInstance Theirs, CardInstance Yojimbo, CardInstance FarYojimbo, CardInstance Boil) DojoDuel(bool mineWins)
    {
        var s = Battle();
        var mine = Add(s, CraneVeteran, 0, Zone.Battlefield, province: 0); var theirs = Add(s, LionVeteran, 1, Zone.Battlefield, province: 0);
        var near = Kw(Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0), "Yojimbo"); var far = Kw(Add(s, CraneSamurai, 0, Zone.Home), "Yojimbo");
        var dojo = Add(s, Dojo, 0, Zone.Hand); var boil = Add(s, Boiling, 0, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var n = 0; n < 4; n++) { var top = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, top.Id); }
        Strong(mineWins ? mine : theirs);
        var e = MatchEngine.FromTestState(s);
        // The challenge is not refusable: the scan prints "challenges" without a refusal option.
        Accept(e, Offer(e, 0, "wl-sg-dojo-duel", dojo.Id, theirs.Id, mine.Id));
        Check(e.State.PendingDuel is not null && e.State.PendingDuel.Stage != "AwaitingAccept", "the challenge cannot be refused");
        return (e, mine, theirs, near, far, boil);
    }

    private static void Finish(MatchEngine e)
    {
        Accept(e, new GameCommand("strike", 1, e.Version));
        for (var guard = 0; guard < 20 && e.State.PendingDuel is not null; guard++)
        {
            var actor = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var action = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type is "orderDuelFocusEffects" or "declineReaction" or "declineDuelReaction" or "pass" || a is { Type: "duelistRefocus", Command.CardId: null }));
            Check(action is not null, $"the duel is stuck at {e.State.PendingDuel?.Stage}");
            Accept(e, action!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel finished");
    }

    private static void DojoAndBoilingFocus()
    {
        // I win: the loser is dishonored and bowed (not destroyed); my Yojimbo where my Personality stands gets +1F, the one at home does not.
        var (e, mine, theirs, near, far, boil) = DojoDuel(true);
        var nearForce = Force(e, near); var farForce = Force(e, far);
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: boil.Id));
        Finish(e);
        Check(e.State.Cards[theirs.Id].Dishonorable && e.State.Cards[theirs.Id].Bowed && e.State.Cards[theirs.Id].Zone == Zone.Battlefield, "the loser is dishonored and bowed, and still in play");
        Check(!e.State.Cards[mine.Id].Dishonorable && !e.State.Cards[mine.Id].Bowed, "the winner is not");
        Eq(Force(e, near), nearForce + 1, "my Yojimbo at the same location: +1F");
        Eq(Force(e, far), farForce, "my Yojimbo at home: nothing");

        // I lose: the Focus Effect is unconditional, and it is my Personality that is dishonored and bowed.
        var (f, mine2, theirs2, near2, far2, boil2) = DojoDuel(false);
        var near2Force = Force(f, near2);
        Accept(f, new GameCommand("focus", 1, f.Version, Choice: "deck"));
        Accept(f, new GameCommand("focus", 0, f.Version, CardId: boil2.Id));
        Finish(f);
        Check(f.State.Cards[mine2.Id].Dishonorable && f.State.Cards[mine2.Id].Bowed, "the loser (mine) is dishonored and bowed");
        Check(!f.State.Cards[theirs2.Id].Dishonorable, "the winner is not");
        Eq(Force(f, near2), near2Force + 1, "the Focus Effect does not depend on winning");
        Eq(Force(f, far2), Force(f, far2), "far Yojimbo stays");
        Check(!f.State.Cards[far2.Id].Modifiers.Any(m => m.Stat == "Force"), "no bonus to the Yojimbo at home");

        // An enemy Personality is no challenger of mine; a bowed one cannot challenge.
        var s = Battle();
        var bowedMine = Add(s, CraneVeteran, 0, Zone.Battlefield, bowed: true, province: 0); var enemy = Add(s, LionVeteran, 1, Zone.Battlefield, province: 0);
        var dojo = Add(s, Dojo, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s);
        Check(!CanUse(g, 0, "wl-sg-dojo-duel", dojo.Id), "no unbowed Personality of mine to challenge");
        _ = bowedMine; _ = enemy;
    }

    // ---- Honor Replaces Rage -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void HonorReplacesRage()
    {
        var s = Table(Phase.Action, 0);
        var lion = Add(s, LionSamurai, 1, Zone.Home); Strong(lion);
        Add(s, CraneSamurai, 0, Zone.Home);
        var card = Add(s, Rage, 0, Zone.Hand); var card2 = Add(s, Rage, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "wl-sg-honor-replaces-rage", card.Id, lion.Id));
        Eq(Force(e, lion), Force(MatchEngine.FromTestState(s), lion), "not attacking: nothing yet");
        // Attacking: the same Personality at the battlefield on his own turn has -2F.
        var attacking = Batch336Support.Edit(e, st => { var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 1; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        var plain = Batch336Support.Edit(e, st => { st.WlStratGates.AttackPenalties.Clear(); var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 1; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(Force(attacking, lion), Force(plain, lion) - 2, "-2F while attacking this turn");
        // Defending (the other player's turn): no penalty.
        var defending = Batch336Support.Edit(e, st => { var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 0; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        var defendingPlain = Batch336Support.Edit(e, st => { st.WlStratGates.AttackPenalties.Clear(); var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 0; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(Force(defending, lion), Force(defendingPlain, lion), "no penalty while defending");
        // A later turn: the marker has lapsed.
        var later = Batch336Support.Edit(e, st => { st.TurnNumber += 2; var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 1; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(Force(later, lion), Force(plain, lion), "the effect lasts this turn only");
        // Played twice: -4F while attacking.
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, "wl-sg-honor-replaces-rage", card2.Id, lion.Id));
        var twice = Batch336Support.Edit(e, st => { var c = st.Cards[lion.Id]; c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; st.ActivePlayerId = 1; st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Eq(Force(twice, lion), Force(plain, lion) - 4, "two plays stack");
        // The marker survives a saved game.
        Eq(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!.WlStratGates.AttackPenalties.Count, 2, "the markers are in the saved state");
    }

    // ---- Claiming the Ruins ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ClaimingTheRuins()
    {
        var item = CardCatalog.PlayableFromDecks.First(c => c.Type == CardType.Item && !(c.Keywords ?? []).Contains("Unique")).Id;
        var unique = CardCatalog.PlayableFromDecks.First(c => c.Type == CardType.Item && (c.Keywords ?? []).Contains("Unique")).Id;
        var s = Table(Phase.Action, 0);
        var card = Add(s, Ruins, 0, Zone.Hand);
        var plain = Add(s, item, 0, Zone.FateDiscard); var uniq = Add(s, unique, 0, Zone.FateDiscard); var theirs = Add(s, item, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "wl-sg-claiming-ruins", card.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the discard pile search opened");
        var picks = e.QueryActions(0).Where(a => a.Type == "selectSearchCard" && a.Enabled).ToArray();
        Check(picks.Length > 0, "a card may be selected");
        Check(picks.All(a => a.Command!.Choice != unique || unique == item), "a Unique Item is not offered");
        var chosen = picks.First(a => a.Command!.Choice == plain.Id || a.Command!.Choice == item).Command!;
        Accept(e, chosen);
        Eq(e.State.Cards[plain.Id].Zone, Zone.Hand, "the non-Unique Item is in my hand");
        Eq(e.State.Cards[uniq.Id].Zone, Zone.FateDiscard, "the Unique Item stays in the discard pile");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.FateDiscard, "the opponent's discard pile is not mine to search");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy itself is discarded");
    }

    // ---- Exotic Farmlands, Ivinda Village ----------------------------------------------------------------------------------------------------------------------------------

    private static void ExoticFarmlands()
    {
        foreach (var (paid, followers) in new[] { (6, 2), (7, 2), (3, 1), (2, 0) })
        {
            var s = Battle();
            var host = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            var card = Add(s, Farmlands, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, "wl-sg-exotic-farmlands", card.Id);
            var sources = e.PaymentOptions(0, paid).FirstOrDefault()?.SourceIds;
            if (sources is null) { Check(paid > 6, $"{paid} Gold can be paid"); continue; }
            var command = option.Command! with { TargetId = host.Id, GoldAmount = paid, SourceIds = sources };
            Accept(e, command);
            var made = e.State.Cards.Values.Where(c => c.AttachedTo == host.Id && Definition(c).Type == CardType.Follower).ToArray();
            Eq(made.Length, followers, $"{paid} Gold: {followers} Follower(s)");
            Check(made.All(c => c.Zone == Zone.Attached && Definition(c).Force == 1 && (Definition(c).Keywords ?? []).Contains("Ashigaru")), "each is a 1F Ashigaru Follower attached to him");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
        // The target must be my own Personality.
        var t = Battle();
        Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0); var enemy = Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card2 = Add(t, Farmlands, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var opt = Enabled(f, 0, "wl-sg-exotic-farmlands", card2.Id);
        Reject(f, opt.Command! with { TargetId = enemy.Id, GoldAmount = 3, SourceIds = f.PaymentOptions(0, 3).First().SourceIds }, "an enemy host");
        Reject(f, opt.Command! with { TargetId = opt.Command.TargetId, GoldAmount = 3, SourceIds = [] }, "no payment for 3 Gold");
        static CardDefinition Definition(CardInstance c) => CardCatalog.Get(c.DefinitionId);
    }

    private static void IvindaVillage()
    {
        var s = Table(Phase.Action, 0);
        var a = Add(s, CraneSamurai, 0, Zone.Home); var b = Add(s, CraneVeteran, 0, Zone.Home); var c3 = Add(s, LionSamurai, 1, Zone.Home);
        var card = Add(s, Ivinda, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "wl-sg-ivinda-cavalry", card.Id);
        var two = e.PaymentOptions(0, 2).First().SourceIds;
        // The number of targets must equal the Gold paid.
        Reject(e, option.Command! with { GoldAmount = 2, SourceIds = two, Selections = [Pick("cards", a.Id)], TargetId = a.Id }, "one target for two Gold");
        Reject(e, option.Command! with { GoldAmount = 1, SourceIds = e.PaymentOptions(0, 1).First().SourceIds, Selections = [Pick("cards", a.Id, b.Id)], TargetId = a.Id }, "two targets for one Gold");
        Reject(e, option.Command! with { GoldAmount = 0, SourceIds = [], Selections = [Pick("cards")], TargetId = null }, "no Gold, no target");
        Accept(e, option.Command! with { GoldAmount = 2, SourceIds = two, Selections = [Pick("cards", a.Id, c3.Id)], TargetId = a.Id });
        Check(e.State.Cards[a.Id].GrantedKeywords.Any(g => g.Keyword == "Cavalry" && g.Duration == GrantDuration.Turn), "my Personality has Cavalry this turn");
        Check(e.State.Cards[c3.Id].GrantedKeywords.Any(g => g.Keyword == "Cavalry"), "so has the enemy card I chose: any card in play");
        Check(!e.State.Cards[b.Id].GrantedKeywords.Any(g => g.Keyword == "Cavalry"), "the one I did not choose has not");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");
    }

    // ---- Lost Caravan, Mukudori's Wisdom, The Tomb of Sun Tao ---------------------------------------------------------------------------------------------------------------------

    private static void LostCaravan()
    {
        var s = Table(Phase.Action, 0);
        var stronghold = s.Cards[s.Players[0].StrongholdId];
        var holding = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var enemyStronghold = s.Cards[s.Players[1].StrongholdId]; enemyStronghold.Bowed = true;
        var card = Add(s, Caravan, 0, Zone.Hand); var card2 = Add(s, Caravan, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "wl-sg-lost-caravan", card.Id).Any(o => o.TargetIds?.Contains(enemyStronghold.Id) ?? false), "an enemy Stronghold is not a target");
        Reject(e, Use(e, 0, "wl-sg-lost-caravan", card.Id, enemyStronghold.Id), "the enemy Stronghold");
        Accept(e, Take(e, 0, "wl-sg-lost-caravan", card, holding.Id));
        Check(!e.State.Cards[holding.Id].Bowed, "my Holding is straightened");
        Check(e.State.Cards[enemyStronghold.Id].Bowed, "his is untouched");
        e = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[stronghold.Id].Bowed = true; st.Cards[holding.Id].Bowed = false; });
        Accept(e, Take(e, 0, "wl-sg-lost-caravan", card2, stronghold.Id));
        Check(!e.State.Cards[stronghold.Id].Bowed, "my Stronghold is straightened");
    }

    private static CardInstance RingTable(out MatchState s, out CardInstance mine, out CardInstance theirs, Phase phase = Phase.Action)
    {
        s = Table(phase, 0);
        mine = Add(s, "training-ring-of-air", 0, Zone.Home); theirs = Add(s, "training-ring-of-water", 1, Zone.Home);
        return Add(s, Mukudori, 0, Zone.Hand);
    }

    private static void MukudorisWisdom()
    {
        var card = RingTable(out var s, out var mine, out var theirs);
        mine.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        // Straighten a Ring (mine) or bow one (his): both halves are the same card's, one use.
        Accept(e, Offer(e, 0, "wl-sg-mukudori-straighten", card.Id, mine.Id));
        Check(!e.State.Cards[mine.Id].Bowed, "my bowed Ring is straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");

        var t = RingTable(out var s2, out var mine2, out var theirs2);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 0, "wl-sg-mukudori-bow", t.Id, theirs2.Id));
        Check(f.State.Cards[theirs2.Id].Bowed && !f.State.Cards[mine2.Id].Bowed, "an enemy Ring may be bowed: any Ring in play");

        // A Ring in a discard pile is not a Ring in play.
        var t3 = RingTable(out var s3, out var mine3, out _);
        mine3.Zone = Zone.FateDiscard;
        var g = MatchEngine.FromTestState(s3);
        Reject(g, Use(g, 0, "wl-sg-mukudori-bow", t3.Id, mine3.Id), "a Ring in the discard pile");

        // Battle timing: a Ring is never in a unit, so the Battle half needs no Ring at the battlefield.
        var t4 = RingTable(out var s4, out var mine4, out _, Phase.Battle);
        Add(s4, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(s4, LionSamurai, 1, Zone.Battlefield, province: 0);
        s4.PriorityPlayerId = 0;
        var h = MatchEngine.FromTestState(s4);
        Accept(h, Offer(h, 0, "wl-sg-mukudori-bow", t4.Id, mine4.Id));
        Check(h.State.Cards[mine4.Id].Bowed, "bowed in a battle");
    }

    private static void TombOfSunTao()
    {
        var s = Table(Phase.Action, 0);
        var mine = Add(s, "training-ring-of-air", 0, Zone.Home, bowed: true); var theirs = Add(s, "training-ring-of-water", 1, Zone.Home);
        var card = Add(s, Tomb, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "wl-sg-tomb-sun-tao", card.Id, theirs.Id));
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Hand, "the Ring went to its OWNER's hand");
        Check(e.State.Players[1].Hand.Contains(theirs.Id) && !e.State.Players[0].Hand.Contains(theirs.Id), "in his hand, not mine");
        Check(!e.State.Cards[theirs.Id].Bowed, "and is fresh");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home, "my Ring is untouched");
        // Mine, bowed: returned unbowed to my hand.
        var t = Table(Phase.Action, 0);
        var ring = Add(t, "training-ring-of-air", 0, Zone.Home, bowed: true);
        var card2 = Add(t, Tomb, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 0, "wl-sg-tomb-sun-tao", card2.Id, ring.Id));
        Check(f.State.Cards[ring.Id].Zone == Zone.Hand && !f.State.Cards[ring.Id].Bowed && f.State.Players[0].Hand.Contains(ring.Id), "my bowed Ring is back in my hand, straight");
        // A Personality is no Ring.
        var u = Table(Phase.Action, 0);
        var samurai = Add(u, CraneSamurai, 0, Zone.Home);
        var card3 = Add(u, Tomb, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        Reject(g, Use(g, 0, "wl-sg-tomb-sun-tao", card3.Id, samurai.Id), "a Personality");
    }

    // ---- Recovering What Was Lost ------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine EngageTable(Func<MatchState, CardInstance, CardInstance> arrange, out CardInstance card, out CardInstance target, bool opposed = true)
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        card = Add(s, Recovering, 0, Zone.Hand);
        Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        if (opposed) Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        target = arrange(s, card);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return e;
    }

    private static void RecoveringWhatWasLost()
    {
        // A Tactician at home is brought to an opposed battle with +2F.
        var e = EngageTable((s, _) => Kw(Add(s, CraneVeteran, 0, Zone.Home), "Tactician"), out var card, out var tactician);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        Check(!CanUse(e, 0, "wl-sg-recovering-lost", card.Id) == false, "offered in the Engage window");
        var force = Force(e, tactician);
        Accept(e, Offer(e, 0, "wl-sg-recovering-lost", card.Id, tactician.Id));
        Eq(e.State.Cards[tactician.Id].Zone, Zone.Battlefield, "he moved to the battlefield"); Eq(e.State.Cards[tactician.Id].ProvinceIndex, 0, "the current one");
        Check(Force(e, tactician) == force + 2 || Mod(e, tactician, "Force") == 2, "+2F as a Tactician");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");

        // Not a Tactician: moved, no bonus.
        var f = EngageTable((s, _) => Add(s, CraneVeteran, 0, Zone.Home), out var card2, out var plain);
        Accept(f, Offer(f, 0, "wl-sg-recovering-lost", card2.Id, plain.Id));
        Eq(f.State.Cards[plain.Id].Zone, Zone.Battlefield, "moved");
        Eq(Mod(f, plain, "Force"), 0, "no bonus without Tactician");

        // Not opposed: nobody stands against him, so the action is refused and nothing moves.
        var g = EngageTable((s, _) => Add(s, CraneVeteran, 0, Zone.Home), out var card3, out var home, opposed: false);
        Check(!CanUse(g, 0, "wl-sg-recovering-lost", card3.Id), "not offered when he would not be opposed");
        Reject(g, Use(g, 0, "wl-sg-recovering-lost", card3.Id, home.Id), "he would not be opposed");

        // Only a Personality at home: one already at the battlefield is not a target.
        var h = EngageTable((s, _) => Add(s, CraneVeteran, 0, Zone.Home), out var card4, out var homeOne);
        var present = h.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Reject(h, Use(h, 0, "wl-sg-recovering-lost", card4.Id, present.Id), "a Personality already at the battlefield");
        _ = homeOne;
        // Not in the Battle phase's action window: no Engage window, no action.
        var t = Battle();
        var homeT = Add(t, CraneVeteran, 0, Zone.Home); Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0); Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card5 = Add(t, Recovering, 0, Zone.Hand);
        var k = MatchEngine.FromTestState(t);
        Check(!CanUse(k, 0, "wl-sg-recovering-lost", card5.Id), "after the Engage window it is not offered");
        _ = homeT;
    }

    // ---- The False Route ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TheFalseRoute()
    {
        var s = Battle();
        var scout = Kw(Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0), "Scout"); var follower = Add(s, "training-expendable-follower", 0, Zone.Attached, parent: scout.Id);
        scout.Bowed = true; follower.Bowed = true;
        var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); var enemy2 = Add(s, LionVeteran, 1, Zone.Battlefield, province: 0);
        var bystander = Add(s, CraneVeteran, 0, Zone.Battlefield, province: 0);
        var card = Add(s, FalseRoute, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Take(e, 0, "wl-sg-false-route", card, selections: [Pick("own", enemy.Id), Pick("enemy", enemy2.Id)]), "an enemy as my Personality");
        Reject(e, Take(e, 0, "wl-sg-false-route", card, selections: [Pick("own", scout.Id), Pick("enemy", bystander.Id)]), "my own as the enemy");
        var battlefields = e.State.UnresolvedBattlefields.ToArray();
        Accept(e, Take(e, 0, "wl-sg-false-route", card, selections: [Pick("own", scout.Id), Pick("enemy", enemy.Id)]));
        var created = e.State.UnresolvedBattlefields.Except(battlefields).ToArray();
        Eq(created.Length, 1, "one battlefield was created"); Check(created[0] >= MatchEngine.FirstAdditionalBattlefield, "it is not one of the Defender's provinces");
        Eq(e.State.Cards[scout.Id].ProvinceIndex, created[0], "my Personality moved there"); Eq(e.State.Cards[enemy.Id].ProvinceIndex, created[0], "so did the enemy Personality");
        Eq(e.State.Cards[enemy2.Id].ProvinceIndex, 0, "the other enemy stays"); Eq(e.State.Cards[bystander.Id].ProvinceIndex, 0, "so does my other Personality");
        Check(!e.State.Cards[scout.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the Scout's unit is straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded");

        // Not a Scout: moved, still bowed.
        var t = Battle();
        var plain = Add(t, CraneSamurai, 0, Zone.Battlefield, bowed: true, province: 0); var foe = Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var card2 = Add(t, FalseRoute, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Take(f, 0, "wl-sg-false-route", card2, selections: [Pick("own", plain.Id), Pick("enemy", foe.Id)]));
        Check(f.State.Cards[plain.Id].ProvinceIndex >= MatchEngine.FirstAdditionalBattlefield && f.State.Cards[plain.Id].Bowed, "moved, and not straightened without Scout");
    }

    // ---- The Final Lesson --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TheFinalLesson()
    {
        var s = Table(Phase.Action, 0);
        var a = Add(s, CraneSamurai, 0, Zone.Home); var b = Add(s, CraneVeteran, 0, Zone.Home); var bowed = Add(s, CraneSamurai, 0, Zone.Home, bowed: true); var enemy = Add(s, LionSamurai, 1, Zone.Home);
        var card = Add(s, FinalLesson, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Take(e, 0, "wl-sg-final-lesson", card, selections: Pick("targets", a.Id, b.Id, enemy.Id)), "three targets");
        Reject(e, Take(e, 0, "wl-sg-final-lesson", card, selections: Pick("targets", bowed.Id)), "a bowed Personality");
        Reject(e, Take(e, 0, "wl-sg-final-lesson", card, selections: Pick("targets", enemy.Id)), "an enemy Personality");
        Accept(e, Take(e, 0, "wl-sg-final-lesson", card, selections: Pick("targets", a.Id, b.Id)));
        foreach (var each in new[] { a, b }) Check(e.State.Cards[each.Id].GrantedKeywords.Any(g => g.Keyword == "Expendable" && g.Duration == GrantDuration.Permanent), "permanently Expendable");
        Check(!e.State.Cards[bowed.Id].GrantedKeywords.Any(g => g.Keyword == "Expendable"), "not the one I did not choose");
        // Permanent: it survives the end of the turn, and a destroyed Expendable Personality draws a card.
        var later = Batch336Support.Edit(e, st => st.TurnNumber += 3);
        Check(later.State.Cards[a.Id].GrantedKeywords.Any(g => g.Keyword == "Expendable"), "still Expendable on a later turn");
    }

    public static void Run(TestRunner runner) => runner.Run("wl-strat-gates", [
        ("Published shape: eighteen Gates of Chaos Strategies with their printed stats, keywords, abilities, art and manifest", PublishedShape),
        ("Allied Efforts gives an enemy -3F (not my own); Tactical Setback is Absent: -2F with no unit at the battlefield", AlliedAndSetback),
        ("Defensive Formation: one to three of my defending Personalities +2F; four, an attacker or none is refused; an attacker cannot play it", DefensiveFormation),
        ("Nubane's Defense: +1F per Ring I control to one or two Personalities; Chaos of Battle: opposed Personalities get +N for N different Clan alignments", NubaneAndChaos),
        ("Friends in the Jungle: +2F/+2C to a Nonhuman Personality for 1 Gold, in a battle or at home", FriendsInTheJungle),
        ("Boiling Point's Battle/Open: bow my unbowed Personality at any location and straighten another", BoilingPointBattle),
        ("Iaijutsu Dojo: an unrefusable challenge, the loser dishonored and bowed; Boiling Point's Focus Effect: +1F to my Yojimbo where my Personality stands", DojoAndBoilingFocus),
        ("Honor Replaces Rage: -2F while attacking, this turn only, stacking", HonorReplacesRage),
        ("Claiming the Ruins: a non-Unique Item from my own discard pile into my hand", ClaimingTheRuins),
        ("Exotic Farmlands: one 1F Ashigaru Follower per 3 Gold paid, rounded down, on my Personality", ExoticFarmlands),
        ("Ivinda Village: Cavalry for the turn to as many target cards as Gold paid", IvindaVillage),
        ("Lost Caravan: straighten my Stronghold or Holding", LostCaravan),
        ("Mukudori's Wisdom: straighten or bow a Ring in play", MukudorisWisdom),
        ("The Tomb of Sun Tao: a Ring in play goes to its owner's hand", TombOfSunTao),
        ("Recovering What Was Lost: an Engage action bringing a Personality from home to an opposed battle, +2F for a Tactician", RecoveringWhatWasLost),
        ("The False Route: a battlefield without a Province, both Personalities moved there, a Scout's unit straightened", TheFalseRoute),
        ("The Final Lesson: one or two of my unbowed Personalities become permanently Expendable", TheFinalLesson),
    ]);
}
