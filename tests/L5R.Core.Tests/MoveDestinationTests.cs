using System.Text.Json;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Move to a destination the ability's own rule defines (issue #336): Sea Troll Predator moves its target AND itself to the same adjacent unresolved
// battlefield -- an enemy card sent somewhere other than home, and the source going with it, are both new. The destination is data on the ability
// (home / current battlefield / adjacent unresolved / another unresolved), the engine lists the legal battlefields in ActionOption.Provinces (the
// province indices the assignment-with-arrows flow already draws) and the command names one in Province. The other rules and mover shapes are
// proven on synthetic fixtures. Readings and alternatives: docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md.
internal static class MoveDestinationTests
{
    private const string Predator = "oracle-l5r-6522", Move = "predator-move";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Sea Troll Predator is the printed card (Force 6, Chi 4, no Honor Requirement, Cost 8, Personal Honor 0, Unaligned Shadowlands Nonhuman Troll Earth Tactician), whole -- EntryHonorLoss 3 and one Battle move on the adjacent-unresolved rule for source and target together", PublishedShape),
            ("Predator moves the Personality and himself to the same adjacent unresolved battlefield, whichever the player chooses -- both arrive with their attachments, neither is left at the battlefield they fought at, and nothing else moves", MovesBothToTheChosenBattlefield),
            ("Which battlefields are legal: adjacent to the current one along the DEFENDER's province line (destroyed provinces slide together), unresolved (not one already fought, not one nobody attacked), never the current one -- and never the attacker's own province line", LegalDestinationRules),
            ("Refused, atomically: a destination that is not listed (the current battlefield, a far one, a resolved one, none, out of range), a target that is not an enemy Personality at the current battlefield, a Predator who is not there; with no legal destination the action is offered disabled", Validation),
            ("Predator used by the Defender: the same adjacency (the defender's province line decides it, not whose action it is), and the same result", DefenderUsesPredator),
            ("If the target's movement is negated (Tamori Tamoko's effect) only the target stays: Predator still goes -- the two movements are separate effects", NegatedTargetMove),
            ("The battle state follows: the current battle resolves with what remains at it, the moved units fight the battle at their new battlefield with their Force counted there, and everyone still ends the Attack Phase home", BattleStateFollowsTheMove),
            ("Every destination rule and mover shape on fixtures: current battlefield (target from home), another unresolved battlefield (target), adjacent unresolved (source alone, or performer and target), home (source and target together)", EveryRuleAndMover),
            ("The option's Provinces (the destinations to draw) and its command's Province (the choice) survive JSON, and every offered destination validates and resolves on a fresh engine", OfferedDestinationsValidate),
        ];
        runner.Run("move-destination", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The Attack Phase with player 1 attacking and player 0 defending. The battle under way is at province `current`; `unresolved` are the battlefields still
    // to be fought (the current one included, as in the engine). Predator (player 1) and his target (player 0) stand at `current`, each with an Item.
    private sealed record Rig(MatchState S, CardInstance Predator, CardInstance Target, CardInstance PredatorItem, CardInstance TargetItem);
    private static Rig Map(int current = 1, int[]? unresolved = null, string predator = Predator, int predatorOwner = 1)
    {
        var s = Table(); s.ActivePlayerId = 1; s.PriorityPlayerId = predatorOwner; s.CurrentBattleProvince = current; s.UnresolvedBattlefields = (unresolved ?? [0, 1, 2]).ToList();
        var p = Add(s, predator, predatorOwner, Zone.Battlefield, province: current);
        var t = Add(s, "training-battle-bower", 1 - predatorOwner, Zone.Battlefield, province: current);
        var pi = Add(s, "training-item", predatorOwner, Zone.Attached, province: current, parent: p.Id);
        var ti = Add(s, "training-item", 1 - predatorOwner, Zone.Attached, province: current, parent: t.Id);
        return new(s, p, t, pi, ti);
    }

    private static int[] Destinations(MatchEngine e, Rig r, int player = 1) => Options(e, player, Move, r.Predator.Id).Single().Provinces ?? [];

    private static GameCommand MoveTo(MatchEngine e, Rig r, int province, int player = 1) => Offer(e, player, Move, r.Predator.Id, r.Target.Id) with { Province = province };

    private static void PublishedShape()
    {
        var p = CardCatalog.Get(Predator);
        Check(!p.ScenarioOnly && p.Type == CardType.Personality && p.Name == "Sea Troll Predator" && p.Clan == "Unaligned");
        Eq(p.Force, 6); Eq(p.Chi, 4); Eq(p.GoldCost, 8); Eq(p.PersonalHonor, 0); Check(p.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Tactician", "Earth", "Nonhuman", "Shadowlands", "Troll" }) Check(p.Keywords!.Contains(k), k);
        Eq(p.Text, "After Predator enters play: Lose 3 Honor.\nBattle: Target a Personality opposing Predator at the current battlefield: Move the Personality and Predator to the same adjacent unresolved battlefield.");
        Eq(p.EntryHonorLoss, 3); Eq(p.Effect, "none"); Check(p.Abilities!.SequenceEqual(new[] { Move }));
        var a = AbilityCatalog.All[Move];
        Eq(a.Timing, "Battle"); Eq(a.Target, "enemy-personality"); Eq(a.Move, new MoveSpec(MoveSpec.SourceAndTarget, MoveSpec.AdjacentUnresolved));
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Predator + ".jpg")), "the scan is shipped");
        Eq(p.ArtworkKey, $"res://Content/Published/{Predator}.jpg");
    }

    private static void MovesBothToTheChosenBattlefield()
    {
        foreach (var destination in new[] { 0, 2 })
        {
            var r = Map();
            var bystanderAtCurrent = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 1);
            var elsewhere = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: destination);
            var e = MatchEngine.FromTestState(r.S);
            var option = Enabled(e, 1, Move, r.Predator.Id);
            Check(option.Provinces!.ToHashSet().SetEquals([0, 2]), "both adjacent unresolved battlefields are offered: " + string.Join(",", option.Provinces!));
            Check(option.TargetIds!.ToHashSet().SetEquals([r.Target.Id, bystanderAtCurrent.Id]), "the Personalities opposing him at the current battlefield");
            var bowedBefore = e.State.Cards[r.Predator.Id].Bowed;
            Accept(e, MoveTo(e, r, destination));
            foreach (var moved in new[] { r.Predator, r.Target, r.PredatorItem, r.TargetItem })
            {
                Eq(e.State.Cards[moved.Id].ProvinceIndex, destination, $"{moved.Id} is at the chosen battlefield");
                Check(e.State.Cards[moved.Id].Zone == (moved.Zone == Zone.Attached ? Zone.Attached : Zone.Battlefield), $"{moved.Id} keeps its zone");
            }
            Eq(e.State.Cards[r.Target.Id].OwnerId, 0, "his controller is unchanged");
            Check(e.State.Cards[bystanderAtCurrent.Id].ProvinceIndex == 1 && e.State.Cards[elsewhere.Id].ProvinceIndex == destination, "no other card moved");
            Eq(e.State.Cards[r.Predator.Id].Bowed, bowedBefore, "moving bows nobody");
            Check(e.State.Events.Count(x => x.Type == "unitMovedToBattlefield") == 2, "both movements are in the public log");
            Eq(e.State.CurrentBattleProvince, 1, "the battle in progress is still the current one");
            Eq(e.State.PriorityPlayerId, 0, "priority passed on");
            Check(!e.GetView(0).Players[1].Army.Any(c => c.Id == r.Predator.Id && c.ProvinceIndex == 1), "and the view shows him gone from it");
        }
    }

    private static void LegalDestinationRules()
    {
        // (1) The line: 0-1-2-3 with the current battlefield at 1 gives {0, 2}, though 3 is unresolved too.
        var line = Map(unresolved: [0, 1, 2, 3]);
        Check(Destinations(MatchEngine.FromTestState(line.S), line).ToHashSet().SetEquals([0, 2]), "adjacent only: 0 and 2, not 3");
        // (2) Already fought: an adjacent battlefield that is no longer unresolved is not offered.
        var fought = Map(unresolved: [1, 2]);
        Check(Destinations(MatchEngine.FromTestState(fought.S), fought).SequenceEqual(new[] { 2 }), "province 0 was fought already");
        // (3) Nobody attacked it: an adjacent province that is not a battlefield at all is not offered.
        var nobody = Map(unresolved: [0, 1]);
        Check(Destinations(MatchEngine.FromTestState(nobody.S), nobody).SequenceEqual(new[] { 0 }), "province 2 is not a battlefield");
        // (4) A destroyed province of the DEFENDER slides its neighbours together: with province 2 gone, 1 is next to 3.
        var slid = Map(unresolved: [0, 1, 3]); slid.S.Players[0].Provinces[2].Destroyed = true;
        Check(Destinations(MatchEngine.FromTestState(slid.S), slid).ToHashSet().SetEquals([0, 3]), "0 and 3: the destroyed province is skipped");
        // (5) ...but the ATTACKER's own destroyed provinces are another line and change nothing.
        var mine = Map(unresolved: [0, 1, 2]); mine.S.Players[1].Provinces[2].Destroyed = true; mine.S.Players[1].Provinces[0].Destroyed = true;
        Check(Destinations(MatchEngine.FromTestState(mine.S), mine).ToHashSet().SetEquals([0, 2]), "the attacker's own province line is not the one that decides adjacency");
        // (6) The ends of the line have one neighbour, and a battlefield next to only resolved ones has none.
        var end = Map(current: 0, unresolved: [0, 1, 3]);
        Check(Destinations(MatchEngine.FromTestState(end.S), end).SequenceEqual(new[] { 1 }), "province 0 is next to 1 only");
        var alone = Map(unresolved: [1]);
        var e = MatchEngine.FromTestState(alone.S);
        var listed = Options(e, 1, Move, alone.Predator.Id).Single();
        Check(!listed.Enabled && listed.Provinces is { Length: 0 } && listed.Reason is not null, "no legal destination: offered disabled, with the reason and nothing to draw: " + listed.Reason);
        // (7) The current battlefield is never a destination, whatever the rest.
        Check(!Destinations(MatchEngine.FromTestState(line.S), line).Contains(1), "not the battlefield he is at");
    }

    private static void Validation()
    {
        var r = Map(unresolved: [0, 1, 2, 3]);
        var ownAtBattle = Add(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 1);
        var enemyAway = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(r.S, "training-follower", 0, Zone.Attached, province: 1, parent: r.Target.Id);
        var e = MatchEngine.FromTestState(r.S);
        GameCommand Cmd(string? target, int? province, string? source = null) => new("useAbility", 1, e.Version, source ?? r.Predator.Id, AbilityId: Move, TargetId: target, Province: province);
        Accept(MatchEngine.FromTestState(r.S), Cmd(r.Target.Id, 0) with { ExpectedVersion = MatchEngine.FromTestState(r.S).Version });
        foreach (var bad in new int?[] { null, 1, 3, -1, 4, 99 }) Reject(e, Cmd(r.Target.Id, bad));
        Reject(e, Cmd(ownAtBattle.Id, 0)); Reject(e, Cmd(enemyAway.Id, 0)); Reject(e, Cmd(enemyFollower.Id, 0)); Reject(e, Cmd(r.TargetItem.Id, 0)); Reject(e, Cmd(null, 0));
        Reject(e, Cmd(r.Predator.Id, 0));
        // A resolved battlefield: the same command is refused once province 0 has been fought.
        var fought = Map(unresolved: [1, 2]);
        Reject(MatchEngine.FromTestState(fought.S), new GameCommand("useAbility", 1, MatchEngine.FromTestState(fought.S).Version, fought.Predator.Id, AbilityId: Move, TargetId: fought.Target.Id, Province: 0));
        // Predator must be at the current battlefield to act at all.
        var away = Map(); away.S.Cards[away.Predator.Id].ProvinceIndex = 0;
        var eAway = MatchEngine.FromTestState(away.S);
        Check(Options(eAway, 1, Move, away.Predator.Id).All(o => !o.Enabled), "not offered from another battlefield");
        Reject(eAway, new GameCommand("useAbility", 1, eAway.Version, away.Predator.Id, AbilityId: Move, TargetId: away.Target.Id, Province: 2));
        // Once a turn.
        var used = Map(); var e2 = MatchEngine.FromTestState(used.S);
        Accept(e2, MoveTo(e2, used, 2));
        e2 = Edit(e2, s => { s.PriorityPlayerId = 1; s.CurrentBattleProvince = 2; });
        Check(Options(e2, 1, Move, used.Predator.Id).All(o => !o.Enabled), "one use a turn");
    }

    private static void DefenderUsesPredator()
    {
        // Predator (player 0, the Defender) and an attacking target (player 1), current battlefield 1, the defender's province 2 destroyed.
        var r = Map(current: 1, unresolved: [0, 1, 3], predatorOwner: 0); r.S.Players[0].Provinces[2].Destroyed = true;
        var e = MatchEngine.FromTestState(r.S);
        Check(Destinations(e, r, player: 0).ToHashSet().SetEquals([0, 3]), "the Defender's own province line: 2 is gone, so 3 is next to 1");
        Accept(e, MoveTo(e, r, 3, player: 0));
        Check(new[] { r.Predator, r.Target, r.PredatorItem, r.TargetItem }.All(c => e.State.Cards[c.Id].ProvinceIndex == 3), "the defending Predator and the attacking Personality arrive together");
        Eq(e.State.Cards[r.Target.Id].OwnerId, 1, "and the Attacker still controls his");
    }

    private static void NegatedTargetMove()
    {
        var r = Map();
        var e = MatchEngine.FromTestState(r.S);
        e = Edit(e, s => s.NegatedEffects.Add(new("move", r.Target.Id)));
        Accept(e, MoveTo(e, r, 2));
        Eq(e.State.Cards[r.Target.Id].ProvinceIndex, 1, "the target stayed"); Eq(e.State.Cards[r.TargetItem.Id].ProvinceIndex, 1, "and so did his Item");
        Eq(e.State.Cards[r.Predator.Id].ProvinceIndex, 2, "Predator went on his own"); Eq(e.State.Cards[r.PredatorItem.Id].ProvinceIndex, 2);
        Check(e.State.Events.Any(x => x.Type == "moveNegated"), "the negation is in the log");
    }

    private static void BattleStateFollowsTheMove()
    {
        // Province 1 (current): Predator 6 and B 4 attack; T 3 and D 2 defend. Province 0: C 1 attacks, E 1 defends. Predator takes T to province 0.
        var r = Map();
        Force(r.Target, 1); // the bower's Force 2 + 1 = 3
        var b = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 1); // Force 4
        var d = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 1); // 2
        var c = Add(r.S, "training-battle-bower", 1, Zone.Battlefield, province: 0); Force(c, -1); // 1
        var ee = Add(r.S, "training-battle-bower", 0, Zone.Battlefield, province: 0); Force(ee, -1); // 1
        // Each Item adds 1 Force to its unit (training-item: +2), which moves with the unit.
        foreach (var unit in new[] { r.PredatorItem, r.TargetItem }) r.S.Cards[unit.Id].DefinitionId = "training-zero-cost-item";
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, MoveTo(e, r, 0));
        Eq(e.GetView(1).Players[1].Army.Where(x => x.ProvinceIndex == 1 && x.Definition!.Type == CardType.Personality).Count(), 1, "only B is left of the attackers at the current battlefield");
        // Both players pass: the current battle resolves with what remains at it.
        void Pass() { Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "pass" && a.Enabled).Command!); }
        Pass(); Pass();
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message == "Province 2: attacking Force 4, defending Force 2."), "province 2 was decided by B (4) against D (2) alone: " + string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message)));
        Eq(e.State.Phase, Phase.BattleSelection);
        Check(e.State.UnresolvedBattlefields.OrderBy(x => x).SequenceEqual(new[] { 0, 2 }), "the fought battlefield is resolved; the moved units' battlefield is still to be fought");
        Check(e.State.Cards[r.Predator.Id].Zone == Zone.Battlefield && e.State.Cards[r.Predator.Id].ProvinceIndex == 0 && e.State.Cards[r.Target.Id].ProvinceIndex == 0, "and they are waiting there");
        // The battle at province 1 (index 0): Predator 6 + Item 1 + C 1 = 8 against T 3 + Item 1 + E 1 = 5.
        Accept(e, e.QueryActions(1).First(a => a.Type == "selectBattle" && a.Command!.Province == 0).Command!);
        Pass(); Pass();
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message == "Province 1: attacking Force 8, defending Force 5."), "Predator's 6 and the target's 3, with their Items (+1 each), count where they arrived: " + string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message)));
    }

    private static void EveryRuleAndMover()
    {
        // current-battlefield: your own Personality at home is brought to the battlefield (the source is there, so no Absent is needed).
        var s = Table(); s.PriorityPlayerId = 1;
        var source = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0); var homebody = Add(s, "training-crane-samurai", 1, Zone.Home); var homeItem = Add(s, "training-item", 1, Zone.Attached, parent: homebody.Id);
        var enemy = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var current = Enabled(e, 1, "training-move-current", source.Id);
        Check(current.Provinces is null && current.TargetIds!.Contains(homebody.Id) && !current.TargetIds.Contains(enemy.Id), "no battlefield to choose: nothing to draw; a Personality of yours at home is the target");
        Accept(e, current.Command! with { TargetId = homebody.Id });
        Check(e.State.Cards[homebody.Id].Zone == Zone.Battlefield && e.State.Cards[homebody.Id].ProvinceIndex == 0 && e.State.Cards[homeItem.Id].ProvinceIndex == 0, "he and his Item are at the current battlefield");
        // unresolved-battlefield: another one, chosen; an enemy target.
        var s2 = Table(); s2.PriorityPlayerId = 1; s2.CurrentBattleProvince = 1; s2.UnresolvedBattlefields = [0, 1, 3];
        var src2 = Add(s2, "training-move-source", 1, Zone.Battlefield, province: 1); var tgt2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e2 = MatchEngine.FromTestState(s2);
        var any = Enabled(e2, 1, "training-move-anywhere", src2.Id);
        Check(any.Provinces!.ToHashSet().SetEquals([0, 3]), "every OTHER unresolved battlefield, adjacent or not: " + string.Join(",", any.Provinces!));
        Reject(e2, any.Command! with { TargetId = tgt2.Id, Province = 1 });
        Accept(e2, any.Command! with { TargetId = tgt2.Id, Province = 3 });
        Check(e2.State.Cards[tgt2.Id].ProvinceIndex == 3 && e2.State.Cards[src2.Id].ProvinceIndex == 1, "only the target moved");
        // adjacent-unresolved, the source alone (no target at all).
        var s3 = Table(); s3.PriorityPlayerId = 1; s3.CurrentBattleProvince = 1; s3.UnresolvedBattlefields = [0, 1, 2];
        var src3 = Add(s3, "training-move-source", 1, Zone.Battlefield, province: 1); var stay = Add(s3, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e3 = MatchEngine.FromTestState(s3);
        var self = Enabled(e3, 1, "training-move-source-adjacent", src3.Id);
        Check(self.Provinces!.ToHashSet().SetEquals([0, 2]) && self.Command!.TargetId is null, "the source's own move needs no target");
        Accept(e3, self.Command! with { Province = 2 });
        Check(e3.State.Cards[src3.Id].ProvinceIndex == 2 && e3.State.Cards[stay.Id].ProvinceIndex == 1, "only Predator-shaped source moved");
        // home: source and target together, no battlefield named.
        var s4 = Table(); s4.PriorityPlayerId = 1;
        var src4 = Add(s4, "training-move-source", 1, Zone.Battlefield, province: 0); var tgt4 = Add(s4, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        var home = Enabled(e4, 1, "training-move-both-home", src4.Id);
        Check(home.Provinces is null, "home names no battlefield");
        Reject(e4, home.Command! with { TargetId = tgt4.Id, Province = 0 });
        Accept(e4, home.Command! with { TargetId = tgt4.Id });
        Check(e4.State.Cards[src4.Id].Zone == Zone.Home && e4.State.Cards[tgt4.Id].Zone == Zone.Home, "both went home");
        // performer-and-target: the card the ability names as its performer goes with the target -- and not the ability's own source.
        var s5 = Table(); s5.PriorityPlayerId = 1; s5.CurrentBattleProvince = 1; s5.UnresolvedBattlefields = [0, 1, 2];
        var src5 = Add(s5, "training-move-source", 1, Zone.Battlefield, province: 1); var perf5 = Add(s5, "training-crane-samurai", 1, Zone.Battlefield, province: 1);
        var tgt5 = Add(s5, "training-battle-bower", 0, Zone.Battlefield, province: 1);
        var e5 = MatchEngine.FromTestState(s5);
        var pair = Enabled(e5, 1, "training-move-performer-and-target", src5.Id, perf5.Id);
        Check(pair.Provinces!.ToHashSet().SetEquals([0, 2]), "adjacent unresolved again");
        Accept(e5, pair.Command! with { TargetId = tgt5.Id, Province = 0 });
        Check(e5.State.Cards[perf5.Id].ProvinceIndex == 0 && e5.State.Cards[tgt5.Id].ProvinceIndex == 0 && e5.State.Cards[src5.Id].ProvinceIndex == 1, "the performer and the target moved, the source stayed");
    }

    private static void OfferedDestinationsValidate()
    {
        var r = Map(unresolved: [0, 1, 2, 3]);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 1, Move, r.Predator.Id);
        var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option))!;
        Check(back.Provinces!.SequenceEqual(option.Provinces!) && back.Command!.Province == option.Command!.Province, "the destinations and the default choice survive JSON");
        Check(option.Command!.Province is int first && option.Provinces!.Contains(first), "the default command already names one of the listed battlefields");
        foreach (var province in option.Provinces!)
        {
            var fresh = MatchEngine.FromTestState(r.S);
            Accept(fresh, option.Command! with { ExpectedVersion = fresh.Version, Province = province });
            Check(fresh.State.Cards[r.Predator.Id].ProvinceIndex == province && fresh.State.Cards[r.Target.Id].ProvinceIndex == province, $"destination {province}");
        }
    }
}
