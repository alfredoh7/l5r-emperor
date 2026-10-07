using L5R.Core;
using static RegressionAssertions;

// Issue #690, primitive P24 (EffectPrimitivesAttack.cs): AttackSpec -- where an ability's Attack may find its target, and what its strength is compared with. Fixture abilities only.
internal static class EffectPrimitivesAttackTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    public static void Run(TestRunner runner) => runner.Run("p24-attacks", new (string, Action)[]
    {
        ("DefenderHome: the Attack destroys a Follower in the Defender's home, after the attack reaction window; an ordinary Attack cannot reach it", HomeAttackDestroysFollower),
        ("DefenderHome waives only the location: a followed Personality, the acting player's own home and a card outside a battle stay illegal", HomeAttackStillLegalOnly),
        ("AnyLocation: an Attack outside a battle reaches an enemy card at home; its hit is judged where the target stands", AnywhereAttack),
        ("CompareAgainst PrintedForce ignores Force modifiers both ways; the ordinary comparison does not", PrintedForceCompare),
        ("CompareOptional: the player is offered both comparisons and the one chosen with the action is the one applied", OptionalPrintedCompare),
        ("AttackSpec is data on the ability, and absent from an ability that does not declare it", SpecIsData),
    });

    private static void Grant(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static void Decline(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static bool Gone(MatchEngine e, string id) => e.State.Cards[id].Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead;
    private static string[] Targets(MatchEngine e, int player, string ability, string cardId) =>
        Batch336Support.Options(e, player, ability, cardId).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).Distinct().ToArray();

    // A battle at province 0: player 1 attacks with a Personality carrying `ability`; the Defender (player 0) has a followed Personality at home and a lone one beside him.
    private sealed record Rig(MatchEngine E, CardInstance Attacker, CardInstance HomeHost, CardInstance HomeFollower, CardInstance HomeLoner);
    private static Rig Battle(string ability, Action<MatchState>? build = null)
    {
        var s = Batch336Support.Table();
        var attacker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Grant(attacker, ability);
        var host = Add(s, "training-crane-veteran", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id); // Force 2
        var loner = Add(s, "training-crane-samurai", 0, Zone.Home);                     // Force 3
        build?.Invoke(s);
        return new(MatchEngine.FromTestState(s), attacker, host, follower, loner);
    }

    private static void HomeAttackDestroysFollower()
    {
        CardInstance reactor = null!;
        var r = Battle(EffectPrimitiveFixtures.AttackHome, s => reactor = Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0));
        var targets = Targets(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id);
        Check(targets.Contains(r.HomeFollower.Id) && targets.Contains(r.HomeLoner.Id), "a Follower and a Personality without Followers in the Defender's home are offered: " + string.Join(",", targets));
        Check(targets.Contains(reactor.Id), "and so is the current enemy army, as always");
        Accept(r.E, Batch336Support.Offer(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id, r.HomeFollower.Id));
        Eq(r.E.State.PendingChoice?.Type, "attackReaction", "the home-targeted Attack opens the attack reaction window");
        Check(!Gone(r.E, r.HomeFollower.Id), "nothing is destroyed while the window is open");
        Decline(r.E);
        Check(Gone(r.E, r.HomeFollower.Id), "the Follower in the Defender's home was destroyed (Force 2 against Melee 3)");
        Eq(r.E.State.Cards[r.HomeHost.Id].Zone, Zone.Home, "his Personality is untouched");

        // The rulebook Attack of the same strength cannot reach the home.
        var plain = Battle("training-weapon-attack", s => Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0));
        var plainTargets = Targets(plain.E, 1, "training-weapon-attack", plain.Attacker.Id);
        Check(!plainTargets.Contains(plain.HomeFollower.Id) && !plainTargets.Contains(plain.HomeLoner.Id), "an ordinary Attack is not offered a card in the Defender's home");
        Reject(plain.E, Batch336Support.Use(plain.E, 1, "training-weapon-attack", plain.Attacker.Id, plain.HomeFollower.Id));
    }

    private static void HomeAttackStillLegalOnly()
    {
        CardInstance ownAtHome = null!;
        var r = Battle(EffectPrimitiveFixtures.AttackHome, s => ownAtHome = Add(s, "training-follower", 1, Zone.Attached, parent: Add(s, "training-lion-samurai", 1, Zone.Home).Id));
        var targets = Targets(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id);
        Check(!targets.Contains(r.HomeHost.Id), "a Personality with a Follower is still not a legal target, at home as anywhere");
        Reject(r.E, Batch336Support.Use(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id, r.HomeHost.Id));
        Check(!targets.Contains(ownAtHome.Id), "the acting player's own home is not the Defender's");
        Reject(r.E, Batch336Support.Use(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id, ownAtHome.Id));
        // A hit on the lone Personality at home: Force 3 against Melee 3.
        Accept(r.E, Batch336Support.Offer(r.E, 1, EffectPrimitiveFixtures.AttackHome, r.Attacker.Id, r.HomeLoner.Id)); Decline(r.E);
        Check(Gone(r.E, r.HomeLoner.Id), "a Personality without Followers in the Defender's home was destroyed");

        // The Defender taking the same action: the Defender's home holds only his own cards, and the Attacker's home is not in scope.
        var s2 = Batch336Support.Table();
        var defender = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Grant(defender, EffectPrimitiveFixtures.AttackHome);
        Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var attackersHome = Add(s2, "training-lion-samurai", 1, Zone.Home);
        s2.PriorityPlayerId = 0;
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Targets(e2, 0, EffectPrimitiveFixtures.AttackHome, defender.Id).Contains(attackersHome.Id), "the Attacker's home is not the Defender's home");
    }

    private static void AnywhereAttack()
    {
        // The Action Phase: no battle, no current enemy army.
        var s = Batch336Support.Table(Phase.Action);
        var archer = Add(s, "training-lion-veteran", 1, Zone.Home); Grant(archer, EffectPrimitiveFixtures.AttackAnywhere);
        var host = Add(s, "training-crane-veteran", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id);
        var loner = Add(s, "training-crane-samurai", 0, Zone.Home);
        var own = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Targets(e, 1, EffectPrimitiveFixtures.AttackAnywhere, archer.Id);
        Check(targets.Contains(follower.Id) && targets.Contains(loner.Id), "enemy cards at home are offered outside a battle: " + string.Join(",", targets));
        Check(!targets.Contains(host.Id) && !targets.Contains(own.Id), "but not a followed Personality or the acting player's own card");
        Accept(e, Batch336Support.Offer(e, 1, EffectPrimitiveFixtures.AttackAnywhere, archer.Id, follower.Id)); Decline(e);
        Check(Gone(e, follower.Id), "the Follower at home was destroyed by a Ranged 3 Attack outside a battle");

        // The rulebook Attack has no target at all outside a battle.
        var s2 = Batch336Support.Table();
        var melee = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Grant(melee, "training-weapon-attack");
        var away = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, Batch336Support.Use(e2, 1, "training-weapon-attack", melee.Id, away.Id));
    }

    // Player 1's Melee 3 against player 0's Personalities at the battlefield: a Crane samurai (printed Force 3) raised to 8, and a Lion samurai (printed Force 4) lowered to 1.
    private static (MatchEngine E, CardInstance Attacker, CardInstance Raised, CardInstance Lowered) Compare(string ability)
    {
        var s = Batch336Support.Table();
        var attacker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Grant(attacker, ability);
        var raised = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Batch336Support.Force(raised, 5);
        var lowered = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Batch336Support.Force(lowered, -3);
        return (MatchEngine.FromTestState(s), attacker, raised, lowered);
    }

    private static void PrintedForceCompare()
    {
        var (e, attacker, raised, lowered) = Compare(EffectPrimitiveFixtures.AttackPrinted);
        Eq(e.CardForceForTest(raised.Id), 8, "fixture: the raised target's Force"); Eq(e.CardForceForTest(lowered.Id), 1, "fixture: the lowered target's Force");
        Accept(e, Batch336Support.Offer(e, 1, EffectPrimitiveFixtures.AttackPrinted, attacker.Id, raised.Id)); Decline(e);
        Check(Gone(e, raised.Id), "printed Force 3 against Melee 3: destroyed, whatever his Force is now");

        var (e2, attacker2, _, lowered2) = Compare(EffectPrimitiveFixtures.AttackPrinted);
        Accept(e2, Batch336Support.Offer(e2, 1, EffectPrimitiveFixtures.AttackPrinted, attacker2.Id, lowered2.Id)); Decline(e2);
        Check(!Gone(e2, lowered2.Id), "printed Force 4 against Melee 3: not destroyed, although his Force is 1 now");

        // The ordinary comparison reads the current Force: the reverse of both.
        var (e3, attacker3, raised3, _) = Compare("training-p24-attack-home");
        Accept(e3, Batch336Support.Offer(e3, 1, "training-p24-attack-home", attacker3.Id, raised3.Id)); Decline(e3);
        Check(!Gone(e3, raised3.Id), "current Force 8 against Melee 3: not destroyed");
        var (e4, attacker4, _, lowered4) = Compare("training-p24-attack-home");
        Accept(e4, Batch336Support.Offer(e4, 1, "training-p24-attack-home", attacker4.Id, lowered4.Id)); Decline(e4);
        Check(Gone(e4, lowered4.Id), "current Force 1 against Melee 3: destroyed");
    }

    private static void OptionalPrintedCompare()
    {
        var (e, attacker, raised, _) = Compare(EffectPrimitiveFixtures.AttackMayPrinted);
        var choices = Batch336Support.Options(e, 1, EffectPrimitiveFixtures.AttackMayPrinted, attacker.Id).Select(a => a.Command?.Choice).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(choices.SequenceEqual([AttackSpec.ForceChoice, AttackSpec.PrintedForceChoice]), "one offered action per comparison: " + string.Join(",", choices));
        Accept(e, Batch336Support.OfferChoice(e, 1, EffectPrimitiveFixtures.AttackMayPrinted, attacker.Id, raised.Id, AttackSpec.PrintedForceChoice)); Decline(e);
        Check(Gone(e, raised.Id), "compared against printed Force 3: destroyed");

        var (e2, attacker2, raised2, _) = Compare(EffectPrimitiveFixtures.AttackMayPrinted);
        Accept(e2, Batch336Support.OfferChoice(e2, 1, EffectPrimitiveFixtures.AttackMayPrinted, attacker2.Id, raised2.Id, AttackSpec.ForceChoice)); Decline(e2);
        Check(!Gone(e2, raised2.Id), "compared against current Force 8: not destroyed");

        // No choice named is the ordinary comparison; a choice that is not one of the two is rejected.
        var (e3, attacker3, raised3, _) = Compare(EffectPrimitiveFixtures.AttackMayPrinted);
        Reject(e3, Batch336Support.Use(e3, 1, EffectPrimitiveFixtures.AttackMayPrinted, attacker3.Id, raised3.Id) with { Choice = "chi" });
        Accept(e3, Batch336Support.Use(e3, 1, EffectPrimitiveFixtures.AttackMayPrinted, attacker3.Id, raised3.Id)); Decline(e3);
        Check(!Gone(e3, raised3.Id), "no choice: the current Force is compared");
        // A mandatory printed comparison takes no choice.
        var (e4, attacker4, raised4, _) = Compare(EffectPrimitiveFixtures.AttackPrinted);
        Reject(e4, Batch336Support.Use(e4, 1, EffectPrimitiveFixtures.AttackPrinted, attacker4.Id, raised4.Id) with { Choice = AttackSpec.PrintedForceChoice });
    }

    private static void SpecIsData()
    {
        Eq(AbilityCatalog.All[EffectPrimitiveFixtures.AttackHome].Attack, new AttackSpec(TargetScope: AttackTargetScope.DefenderHome));
        Eq(AbilityCatalog.All[EffectPrimitiveFixtures.AttackAnywhere].Attack, new AttackSpec(TargetScope: AttackTargetScope.AnyLocation));
        Eq(AbilityCatalog.All[EffectPrimitiveFixtures.AttackMayPrinted].Attack, new AttackSpec(CompareAgainst: AttackCompare.PrintedForce, CompareOptional: true));
        Check(AbilityCatalog.All["training-weapon-attack"].Attack is null && AbilityCatalog.All["nobunori-attack"].Attack is null, "an ability that declares none has none");
    }
}
