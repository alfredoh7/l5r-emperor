using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #778: Bayushi Kachiko (oracle-l5r-10481; src/L5R.Core/Kachiko.cs, wave personality-wave-302), proved on the published card. Mendon's reading of "attacks" (2026-10-09): the first time this turn the
// target is assigned to an attacking army in the Maneuvers Segment, or joins an attacking army by moving or being moved to a battlefield, its controller loses 2 Honor; it expires with the turn.
internal static class KachikoTests
{
    private const string Kachiko = "oracle-l5r-10481", Lion = "training-lion-samurai", Crane = "training-crane-samurai", Follower = "training-follower";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    public static void Run(TestRunner runner) => runner.Run("kachiko", [
        ("Published as printed: Bayushi Kachiko, 0 Force, 3 Chi, 8 Gold, her Effect and Open watch, in personality-wave-302", PublishedShape),
        ("-1F: a Fallen or Nonhuman Personality or Follower, and a Personality or Follower attacking her controller; not an ordinary one, not her controller's own attackers, not while she is not in play", StaticTerm),
        ("Open: bows her and sets a watch on any Personality in play, either player's; a Personality not in play is refused and a bowed Kachiko cannot use it again", OpenTargets),
        ("Assigned to attack in the Maneuvers Segment: the target's controller loses 2 Honor once; another Personality's assignment and an earlier turn's watch lose nothing", AssignedToAttack),
        ("Joins an attacking army by moving or being moved to a battlefield: the controller loses 2 Honor once; not a Personality staying where he is, not the defending side's", JoinsByMoving),
    ]);

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // A Personality made for the test: a training Lion samurai with the printed Force and keywords given.
    private static CardInstance Made(MatchState s, string id, int owner, Zone zone, int force, int? province = null, params string[] keywords)
    {
        var card = Add(s, Lion, owner, zone, province: province);
        card.Printed = CardCatalog.Get(Lion) with { Id = id, Force = force, Keywords = keywords };
        return card;
    }

    private static int Force(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(Kachiko);
        Check(card.Type == CardType.Personality && card.Force == 0 && card.Chi == 3 && card.GoldCost == 8 && card.Effect == KachikoAbilities.Effect && card.Abilities!.SequenceEqual([KachikoAbilities.Watch]), "printed stats, Effect and ability");
        Check(card.Keywords.Contains("Unique") && card.Keywords.Contains("Courtier") && card.Keywords.Contains("Empress") && card.Keywords.Contains("ExperiencedCoM") && card.Keywords.Contains("Seductress"), "printed keywords");
        Check(card.Text.Contains("Fallen, Nonhuman, or attacking you") && card.Text.Contains("loses 2 Honor"), "both clauses are in the published text");
        var ability = AbilityCatalog.All[KachikoAbilities.Watch];
        Check(ability.Timing == "Open" && ability.ActionKeywords!.Contains("Political") && ability.Costs!.SequenceEqual(ActionCost.BowSource), "a Political Open that bows her");
        Check(File.ReadAllText(Path.Combine(Batch336Support.RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/personality-wave-302.txt")).Contains(Kachiko), "pinned in the wave manifest");
    }

    private static void StaticTerm()
    {
        // Battle at province 0: player 1 attacks, player 0 defends. Kachiko is player 0's, at home.
        var s = Batch336Support.Table(Phase.Battle, 1);
        Add(s, Kachiko, 0, Zone.Home);
        var attacker = Made(s, "attacker", 1, Zone.Battlefield, 4, 0, "Samurai");
        var follower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: attacker.Id);
        var fallenHome = Made(s, "fallen-home", 1, Zone.Home, 4, null, "Fallen");
        var nonhumanHome = Made(s, "nonhuman-home", 0, Zone.Home, 4, null, "Nonhuman");
        var ordinaryHome = Made(s, "ordinary-home", 1, Zone.Home, 4, null, "Samurai");
        var defender = Made(s, "defender", 0, Zone.Battlefield, 4, 0, "Samurai");
        var e = MatchEngine.FromTestState(s);

        // The same table without Kachiko is the baseline.
        var b = Batch336Support.Table(Phase.Battle, 1);
        var bAttacker = Made(b, "attacker", 1, Zone.Battlefield, 4, 0, "Samurai");
        var bFollower = Add(b, Follower, 1, Zone.Attached, province: 0, parent: bAttacker.Id);
        var baseline = MatchEngine.FromTestState(b);

        Eq(Force(e, attacker), 3, "an attacker of the player she opposes has -1F");
        Eq(Force(e, follower), Force(baseline, bFollower) - 1, "so does his Follower");
        Eq(Force(e, fallenHome), 3, "a Fallen Personality has -1F at home, either player's");
        Eq(Force(e, nonhumanHome), 3, "so does a Nonhuman one of her own controller");
        Eq(Force(e, ordinaryHome), 4, "an ordinary Personality at home is untouched");
        Eq(Force(e, defender), 4, "a defending Personality is untouched");

        // Her controller's attackers are not "attacking you", but a Fallen one still has -1F.
        var own = Batch336Support.Table(Phase.Battle, 1);
        Add(own, Kachiko, 1, Zone.Home);
        var ownAttacker = Made(own, "own-attacker", 1, Zone.Battlefield, 4, 0, "Samurai");
        var ownFallen = Made(own, "own-fallen", 1, Zone.Battlefield, 4, 0, "Fallen");
        var oe = MatchEngine.FromTestState(own);
        Eq(Force(oe, ownAttacker), 4, "her controller's attackers are untouched");
        Eq(Force(oe, ownFallen), 3, "but a Fallen one still has -1F");

        // A Fallen Follower is reduced wherever it stands.
        var f = Batch336Support.Table(Phase.Action, 1);
        Add(f, Kachiko, 0, Zone.Home);
        var host = Made(f, "host", 1, Zone.Home, 2, null, "Samurai");
        var fallenFollower = Add(f, Follower, 1, Zone.Attached, parent: host.Id);
        fallenFollower.Printed = CardCatalog.Get(Follower) with { Id = "fallen-follower", Keywords = [.. (CardCatalog.Get(Follower).Keywords ?? []), "Fallen"] };
        var plainFollower = Add(f, Follower, 1, Zone.Attached, parent: host.Id);
        var fe = MatchEngine.FromTestState(f);
        Eq(Force(fe, fallenFollower), Force(fe, plainFollower) - 1, "a Fallen Follower has -1F, an ordinary one at home none");

        // She must be in play: at the dynasty discard, or not on the table, she changes nothing.
        var gone = Batch336Support.Table(Phase.Battle, 1);
        Add(gone, Kachiko, 0, Zone.Dead);
        var goneAttacker = Made(gone, "gone-attacker", 1, Zone.Battlefield, 4, 0, "Fallen");
        Eq(Force(MatchEngine.FromTestState(gone), goneAttacker), 4, "a Kachiko who is not in play has no effect");
    }

    private static void OpenTargets()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kachiko = Add(s, Kachiko, 0, Zone.Home);
        var enemy = Made(s, "enemy", 1, Zone.Home, 3, null, "Samurai");
        var own = Add(s, Crane, 0, Zone.Home);
        var dead = Made(s, "dead", 1, Zone.Dead, 3);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Enabled(e, 0, KachikoAbilities.Watch, kachiko.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(own.Id), "any Personality in play is a target, either player's");
        Check(!option.TargetIds.Contains(dead.Id), "a Personality not in play is not");
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(new GameCommand("useAbility", 0, e.Version, CardId: kachiko.Id, TargetId: dead.Id, AbilityId: KachikoAbilities.Watch)).Accepted, "a Personality not in play is refused");
        Check(JsonSerializer.Serialize(e.State) == before, "a refused command changes nothing");
        Accept(e, Batch336Support.Offer(e, 0, KachikoAbilities.Watch, kachiko.Id, enemy.Id));
        Check(e.State.Cards[kachiko.Id].Bowed, "Kachiko bowed as the cost");
        Eq(e.State.AssignmentWatches.Count(w => w.CardId == enemy.Id && w.Kind == KachikoAbilities.WatchKind && w.Turn == e.State.TurnNumber), 1, "a watch is set on the target");
        Check(Batch336Support.Options(e, 0, KachikoAbilities.Watch, kachiko.Id).All(o => !o.Enabled), "bowed, she cannot use it again");
    }

    // The Attacker's first declaration: player 1's Personalities are at home, the watch is already set on the target.
    private static MatchEngine Maneuvers(out CardInstance target, out CardInstance other, int watchTurnOffset = 0)
    {
        var s = Batch336Support.Table(Phase.InfantryDeclaration, 1); s.PriorityPlayerId = 1;
        Add(s, Kachiko, 0, Zone.Home);
        target = Made(s, "target", 1, Zone.Home, 3, null, "Samurai"); other = Made(s, "other", 1, Zone.Home, 3, null, "Samurai");
        s.AssignmentWatches.Add(new(target.Id, KachikoAbilities.WatchKind, 0, s.TurnNumber + watchTurnOffset, "Bayushi Kachiko"));
        return MatchEngine.FromTestState(s);
    }

    private static void AssignedToAttack()
    {
        var e = Maneuvers(out _, out var other);
        var honor = Batch336Support.Honor(e, 1);
        Accept(e, new GameCommand("attack", 1, e.Version, Assignments: new Dictionary<string, int> { [other.Id] = 0 }));
        Eq(Batch336Support.Honor(e, 1), honor, "another Personality's assignment loses nothing");
        Check(e.State.AssignmentWatches.Any(w => w.Kind == KachikoAbilities.WatchKind), "and the watch stays");

        var again = Maneuvers(out var target, out other);
        var h = Batch336Support.Honor(again, 1);
        Accept(again, new GameCommand("attack", 1, again.Version, Assignments: new Dictionary<string, int> { [target.Id] = 0, [other.Id] = 0 }));
        Eq(Batch336Support.Honor(again, 1), h - 2, "the target's controller loses 2 Honor when he is assigned to attack");
        Check(!again.State.AssignmentWatches.Any(w => w.Kind == KachikoAbilities.WatchKind), "the watch is spent by the first time");

        var stale = Maneuvers(out target, out _, watchTurnOffset: -1);
        var sh = Batch336Support.Honor(stale, 1);
        Accept(stale, new GameCommand("attack", 1, stale.Version, Assignments: new Dictionary<string, int> { [target.Id] = 0 }));
        Eq(Batch336Support.Honor(stale, 1), sh, "a watch of an earlier turn loses nothing");
    }

    private static void JoinsByMoving()
    {
        MatchEngine Battle(out CardInstance target, int owner = 1, Zone zone = Zone.Home)
        {
            var s = Batch336Support.Table(Phase.Battle, 1);
            Add(s, Kachiko, 0, Zone.Home);
            target = Made(s, "target", owner, zone, 3, zone == Zone.Battlefield ? 1 : null, "Samurai");
            s.AssignmentWatches.Add(new(target.Id, KachikoAbilities.WatchKind, 0, s.TurnNumber, "Bayushi Kachiko"));
            return MatchEngine.FromTestState(s);
        }
        static void Move(MatchEngine e, CardInstance c, int province) =>
            typeof(MatchEngine).GetMethod("MoveToBattlefield", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, [c, province]);

        var e = Battle(out var target);
        var honor = Batch336Support.Honor(e, 1);
        Move(e, target, 0);
        Eq(Batch336Support.Honor(e, 1), honor - 2, "moved to a battlefield from home, he joins an attacking army: his controller loses 2 Honor");
        Check(!e.State.AssignmentWatches.Any(w => w.Kind == KachikoAbilities.WatchKind), "the watch is spent");
        Move(e, e.State.Cards[target.Id], 2);
        Eq(Batch336Support.Honor(e, 1), honor - 2, "moving again loses nothing more");

        var from = Battle(out var other, zone: Zone.Battlefield);
        var fh = Batch336Support.Honor(from, 1);
        Move(from, other, 0);
        Eq(Batch336Support.Honor(from, 1), fh - 2, "moved from one battlefield to another counts as joining the army there");

        var same = Battle(out var stay, zone: Zone.Battlefield);
        var sh = Batch336Support.Honor(same, 1);
        Move(same, stay, 1);
        Eq(Batch336Support.Honor(same, 1), sh, "staying at his own battlefield joins nothing");

        // The defending side's Personality moving to a battlefield is not joining an attacking army.
        var defend = Battle(out var defender, owner: 0);
        var dh = Batch336Support.Honor(defend, 0);
        Move(defend, defender, 0);
        Eq(Batch336Support.Honor(defend, 0), dh, "a Defender's Personality joining the defence loses nothing");
    }
}
