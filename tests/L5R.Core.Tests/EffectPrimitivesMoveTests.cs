using System.Text.RegularExpressions;
using L5R.Core;
using static RegressionAssertions;

// Issue #690, primitive P24 (EffectPrimitivesMoves.cs): the location switch, the adjacent-battlefield destination and the wielder mover. Fixture abilities only (training-p24-*), granted
// to training cards: no catalogue card is wired by this primitive.
internal static class EffectPrimitivesMoveTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);

    public static void Run(TestRunner runner) => runner.Run("p24-moves", new (string, Action)[]
    {
        ("Switch: two units of one player at different locations swap, attachments with them; a unit named by its Follower stands for its unit", SwitchMovesBoth),
        ("Switch: a negation of either side's movement (a watch, a movement lock) negates both, spending what negated it; nothing moves", SwitchNegatedMovesNeither),
        ("Switch: units of two players swap a battlefield for a home; the same location, a card out of play and a card with itself do nothing", SwitchEdges),
        ("Switch asks every question an ordinary move asks: the list is pinned against MoveHome, TryMoveToBattlefield and MoveToBattlefield", SwitchQuestionsPinned),
        ("Adjacent battlefield: only unresolved battlefields next to the unit's own; none for a battlefield without a province (R-002); a move elsewhere is refused", AdjacentDestinations),
        ("MoveSpec.Wielder: a Spell's action moves the Shugenja it is attached to, offered only the adjacent unresolved battlefields", WielderMover),
    });

    private static void Grant(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static (Zone Zone, int? Province) At(MatchEngine e, string id) => (e.State.Cards[id].Zone, e.State.Cards[id].ProvinceIndex);

    // Player 1 (the Attacker) holds the switching card at the current battlefield (province 0); the two units to switch are player `owner`'s: one there, one at home.
    private static (MatchEngine Engine, CardInstance Source, CardInstance AtBattle, CardInstance AtHome, CardInstance Follower) Rig(int owner = 1, Action<MatchState, CardInstance, CardInstance>? build = null)
    {
        CardInstance source = null!, atBattle = null!, atHome = null!, follower = null!;
        var s = Batch336Support.Table();
        source = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Grant(source, EffectPrimitiveFixtures.Switch);
        atBattle = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
        follower = Add(s, "training-follower", owner, Zone.Attached, province: 0, parent: atBattle.Id);
        atHome = Add(s, "training-crane-courtier", owner, Zone.Home);
        build?.Invoke(s, atBattle, atHome);
        return (MatchEngine.FromTestState(s), source, atBattle, atHome, follower);
    }

    private static GameCommand SwitchCommand(MatchEngine e, CardInstance source, string first, string second) =>
        Batch336Support.Use(e, 1, EffectPrimitiveFixtures.Switch, source.Id, null, null, Pick("first", first), Pick("second", second));

    private static void SwitchMovesBoth()
    {
        var (e, source, atBattle, atHome, follower) = Rig();
        Accept(e, SwitchCommand(e, source, atBattle.Id, atHome.Id)); Settle(e);
        Eq(At(e, atBattle.Id), (Zone.Home, null), "the unit at the battlefield went home");
        Eq(e.State.Cards[follower.Id].ProvinceIndex, null, "its Follower went with it");
        Eq(At(e, atHome.Id), (Zone.Battlefield, 0), "the unit at home went to the battlefield");
        Check(e.State.Events.Any(x => x.Type == "locationsSwitched"), "the switch is logged");
        Check(!e.State.Cards[atBattle.Id].Bowed && !e.State.Cards[atHome.Id].Bowed, "a switch bows nobody");

        // A card in a unit stands for its unit: the Follower names the unit it is attached to.
        var (direct, _, b2, h2, f2) = Rig();
        Check(direct.SwitchLocations(direct.CardForTest(f2.Id), direct.CardForTest(h2.Id), "test"), "switched by the Follower's unit");
        Eq(At(direct, b2.Id), (Zone.Home, null)); Eq(At(direct, h2.Id), (Zone.Battlefield, 0));
        Eq(direct.State.Cards[f2.Id].AttachedTo, b2.Id, "the Follower is still attached to its Personality");
    }

    private static void SwitchNegatedMovesNeither()
    {
        // The Defender's two units, switched by the Attacker's action; a watch of the Defender's negates the movement of the one at home.
        var (e, source, atBattle, atHome, _) = Rig(owner: 0);
        Check(Batch336Support.CanUse(e, 1, EffectPrimitiveFixtures.Switch, source.Id), "the switch is offered against the other player's two units");
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Move, TargetCardId = atHome.Id });
        Accept(e, SwitchCommand(e, source, atBattle.Id, atHome.Id)); Settle(e);
        Eq(At(e, atBattle.Id), (Zone.Battlefield, 0), "the other side did not move either");
        Eq(At(e, atHome.Id), (Zone.Home, null), "the negated side stayed home");
        Check(e.State.Events.Any(x => x.Type == "switchNegated"), "the negated switch is logged");
        Eq(e.State.NegationWatches.Count, 0, "the watch that negated it is spent");
        Check(e.State.Cards[source.Id].AbilityUses.ContainsKey(EffectPrimitiveFixtures.Switch), "the action was still taken");

        // The same when it is the FIRST side that is negated (the watch is on the unit at the battlefield).
        var (first, source1, b1, h1, _) = Rig(owner: 0);
        first.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Move, TargetCardId = b1.Id });
        Accept(first, SwitchCommand(first, source1, b1.Id, h1.Id)); Settle(first);
        Eq(At(first, b1.Id), (Zone.Battlefield, 0)); Eq(At(first, h1.Id), (Zone.Home, null));

        // A movement lock on one side ("may not move this turn") stops both.
        var (locked, source2, b2, h2, _) = Rig(owner: 0, build: (s, battle, _) => battle.MovementLocks.Add(new MovementLock("test-lock", s.TurnNumber, MovementKinds.Home)));
        Accept(locked, SwitchCommand(locked, source2, b2.Id, h2.Id)); Settle(locked);
        Eq(At(locked, b2.Id), (Zone.Battlefield, 0), "the locked unit stayed"); Eq(At(locked, h2.Id), (Zone.Home, null), "and so did the other");

        // A lock on moving TO battlefields stops the unit at home, and so the one at the battlefield.
        var (locked2, source3, b3, h3, _) = Rig(owner: 0, build: (s, _, home) => home.MovementLocks.Add(new MovementLock("test-lock", s.TurnNumber, MovementKinds.ToBattlefield)));
        Accept(locked2, SwitchCommand(locked2, source3, b3.Id, h3.Id)); Settle(locked2);
        Eq(At(locked2, b3.Id), (Zone.Battlefield, 0)); Eq(At(locked2, h3.Id), (Zone.Home, null));

        // Without any of them the same rig switches.
        var (plain, source4, b4, h4, _) = Rig(owner: 0);
        Accept(plain, SwitchCommand(plain, source4, b4.Id, h4.Id)); Settle(plain);
        Eq(At(plain, b4.Id), (Zone.Home, null)); Eq(At(plain, h4.Id), (Zone.Battlefield, 0));
    }

    private static void SwitchEdges()
    {
        // Two players' units: the Attacker's at the battlefield and the Defender's at home trade places (each "home" is its own controller's).
        var (e, source, atBattle, atHome, _) = Rig(owner: 0);
        var mine = e.CardForTest(source.Id); var his = e.CardForTest(atHome.Id);
        Check(e.SwitchLocations(mine, his, "test"), "units of two players switch");
        Eq(At(e, source.Id), (Zone.Home, null)); Eq(At(e, atHome.Id), (Zone.Battlefield, 0));
        Eq(e.State.Cards[source.Id].OwnerId, 1, "nobody changes controller");

        // The same battlefield: nothing to switch.
        var (same, source2, b2, _, _) = Rig(owner: 0);
        var before = same.State;
        Check(!same.SwitchLocations(same.CardForTest(source2.Id), same.CardForTest(b2.Id), "test"), "two units at one battlefield do not switch");
        Eq(At(same, source2.Id), (before.Cards[source2.Id].Zone, before.Cards[source2.Id].ProvinceIndex)); Eq(At(same, b2.Id), (Zone.Battlefield, 0));
        Check(!same.SwitchLocations(same.CardForTest(b2.Id), same.CardForTest(b2.Id), "test"), "a unit does not switch with itself");

        // A card that is not in play: neither moves.
        var (gone, _, b3, h3, _) = Rig(owner: 0, build: (_, _, home) => home.Zone = Zone.Dead);
        Check(!gone.SwitchLocations(gone.CardForTest(b3.Id), gone.CardForTest(h3.Id), "test"), "a dead Personality does not switch");
        Eq(At(gone, b3.Id), (Zone.Battlefield, 0));

        // The fixture's targets are Sudden Movement's: two Personalities of one player at different locations. Two at one location are not a legal pair.
        var (pair, source4, b4, _, _) = Rig(owner: 0, build: (s, _, home) => { home.Zone = Zone.Battlefield; home.ProvinceIndex = 0; });
        var other = pair.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.Id != b4.Id && c.DefinitionId == "training-crane-courtier");
        Reject(pair, SwitchCommand(pair, source4, b4.Id, other.Id));
    }

    private static string Body(string file, string signature)
    {
        var lines = File.ReadAllLines(Path.Combine(Batch336Support.RepoRoot(), "src/L5R.Core", file));
        var start = Array.FindIndex(lines, l => l.Contains(signature));
        Check(start >= 0, $"{signature} located in {file}");
        var end = Array.FindIndex(lines, start + 1, l => l == "    }");
        return string.Join("\n", lines[start..(end + 1)]);
    }

    // The questions a move asks before it moves a unit. SwitchSideNegated (EffectPrimitivesMoves.cs) asks the same ones without moving; when a move function gains a question this list
    // changes and the test fails until the switch asks it too.
    private static void SwitchQuestionsPinned()
    {
        var asked = new Regex(@"\b(EnemyActionMovementAutoNegated|NegatesEffect|TryConsumeWatch|Delay|[A-Z]\w*Negates\w*|[A-Z]\w*Negated\w*)\(");
        string[] Questions(string body) => asked.Matches(body).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var home = Questions(Body("CardAbilities.cs", "private void MoveHome(CardInstance card, bool bow)"));
        var toBattle = Questions(Body("CardAbilities.cs", "private bool TryMoveToBattlefield(CardInstance card, int province)") + Body("MatchEngine.cs", "private bool MoveToBattlefield(CardInstance card, int province)"));
        Eq(string.Join(",", home), "BattlefieldMovementNegated,Delay,EnemyActionMovementAutoNegated,MovementLockNegates,NegatesEffect,P4ShieldNegatesMoveHome,TryConsumeWatch", "MoveHome's questions"); // P12: BattlefieldMovementNegated (ImmunityLedger.cs)
        Eq(string.Join(",", toBattle),
            "AsukaNegatesMove,Delay,EnemyActionMovementAutoNegated,HoteiNegatesMove,KitsuneNegatesMovement,MovementLockNegates,MovementToBattlefieldNegated,NegatesEffect,P4NegatesMoveToBattlefield,ShieldmanNegatesMove,StarCrossedNegatesMove,TryConsumeWatch,UguluNegatesMovement",
            "TryMoveToBattlefield's and MoveToBattlefield's questions");
        // And the switch asks each of them (Delay as SwitchSideDelayed: a delayed half cannot be honoured).
        var switchBody = Body("EffectPrimitivesMoves.cs", "private bool SwitchSideNegated(CardInstance card, int? to)");
        foreach (var question in home.Concat(toBattle).Distinct())
            Check(switchBody.Contains((question == "Delay" ? "SwitchSideDelayed" : question) + "("), $"the switch asks {question}");
    }

    // A battle at province 1 with province 2 still to fight and province 0 already resolved.
    private static MatchState MiddleBattle()
    {
        var s = Batch336Support.Table();
        s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [1, 2];
        return s;
    }

    private static void AdjacentDestinations()
    {
        var s = MiddleBattle();
        var unit = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 1, parent: unit.Id);
        var e = MatchEngine.FromTestState(s);
        Check(e.AdjacentBattlefieldsForTest(1).OrderBy(x => x).SequenceEqual([0, 2]), "province 1 is next to provinces 0 and 2");
        Check(e.AdjacentUnresolvedBattlefields(1).SequenceEqual([2]), "only the unresolved neighbour is a destination");
        Check(!e.MoveToAdjacentBattlefield(e.CardForTest(unit.Id), 0), "a resolved neighbour is refused");
        Check(!e.MoveToAdjacentBattlefield(e.CardForTest(unit.Id), 3), "a battlefield that is not adjacent is refused");
        Eq(At(e, unit.Id), (Zone.Battlefield, 1), "a refused move moves nothing");
        Check(e.MoveToAdjacentBattlefield(e.CardForTest(follower.Id), 2), "the unit moves to the adjacent unresolved battlefield (named by its Follower)");
        Eq(At(e, unit.Id), (Zone.Battlefield, 2)); Eq(e.State.Cards[follower.Id].ProvinceIndex, 2, "its Follower moved with it");

        // R-002: a battlefield without a province has no adjacent battlefield.
        var lone = MatchEngine.FromTestState(MiddleBattle());
        var extra = lone.CreateAdditionalBattlefieldForTest(null);
        Eq(lone.AdjacentUnresolvedBattlefields(extra).Length, 0, "a battlefield without a province has no adjacent battlefield");
        Check(!lone.AdjacentUnresolvedBattlefields(1).Contains(extra), "and it is adjacent to none");

        // A unit at home stands at no battlefield.
        var s3 = MiddleBattle(); var atHome = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!e3.MoveToAdjacentBattlefield(e3.CardForTest(atHome.Id), 2), "a unit at home has no adjacent battlefield");
    }

    private static void WielderMover()
    {
        var s = MiddleBattle();
        var shugenja = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 1);
        var spell = Add(s, "training-spell", 1, Zone.Attached, province: 1, parent: shugenja.Id); Grant(spell, EffectPrimitiveFixtures.MoveWielderAdjacent);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Enabled(e, 1, EffectPrimitiveFixtures.MoveWielderAdjacent, spell.Id);
        Check(option.Provinces is { } offered && offered.SequenceEqual([2]), "only the adjacent unresolved battlefield is offered: " + string.Join(",", option.Provinces ?? []));
        Check(option.Movement is { } preview && preview.FixedMoverIds.SequenceEqual([shugenja.Id]), "the preview names the Shugenja as the mover");
        Reject(e, option.Command! with { Province = 0 });
        Accept(e, option.Command! with { Province = 2 }); Settle(e);
        Eq(At(e, shugenja.Id), (Zone.Battlefield, 2), "the Shugenja moved");
        Eq(e.State.Cards[spell.Id].ProvinceIndex, 2, "his Spell went with him");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, "the Spell was bowed as the cost, not the Shugenja");

        // At a battlefield without a province there is nowhere adjacent to go.
        var s2 = MiddleBattle();
        var e2 = MatchEngine.FromTestState(s2);
        var extra = e2.CreateAdditionalBattlefieldForTest(null);
        var e3 = Batch336Support.Edit(e2, st =>
        {
            st.CurrentBattleProvince = extra;
            var sh = Add(st, "training-shugenja", 1, Zone.Battlefield, province: extra);
            Grant(Add(st, "training-spell", 1, Zone.Attached, province: extra, parent: sh.Id), EffectPrimitiveFixtures.MoveWielderAdjacent);
        });
        Check(!Batch336Support.CanUse(e3, 1, EffectPrimitiveFixtures.MoveWielderAdjacent), "no adjacent battlefield: the move is not usable");
    }
}
