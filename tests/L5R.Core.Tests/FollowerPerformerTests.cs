using static RegressionAssertions;
using static Support337;
using L5R.Core;

// The Follower Performer (issue #337) and Hiruma Tensin (oracle-l5r-3332), its first card: "Battle: If you have Reconnaissance: Ranged 4 Attack. Battle: Bow your
// performing Follower in Tensin's unit: Ranged 4 Attack." Mendon: the second ability "is as simple as being available if he has an unbowed Follower; he may target
// that Follower and it performs the Ranged 4 Attack." The first is exactly the published Hiruma Sniper's hiruma-sniper-attack. The Follower's bow is its cost option
// (AlternativeCosts.cs: a single "bow" option). Every reading is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 acts and attacks.
internal static class FollowerPerformerTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Hiruma Tensin is the printed card (Force 3, Chi 3, Cost 7, Personal Honor 2, no Honor Requirement, Crab Samurai Scout), whole -- both Battle abilities, the first the published Sniper's own, the second performed by a Follower of his unit and paid by bowing it", PublishedShape),
            ("First ability: Ranged 4 Attack only with Reconnaissance at the current battlefield (refused with the requirement otherwise), no cost, and Tensin himself performs", ReconnaissanceAttack),
            ("The Follower performer: one offered action per UNBOWED Follower attached to Tensin himself -- not a bowed one, not another unit's, not an enemy's, not Tensin -- and a command naming any of those is refused", OneOfferPerUnbowedFollowerInHisUnit),
            ("The performing Follower is bowed as the cost and then makes the Ranged 4 Attack: a legal target is destroyed, Tensin stays unbowed, the Attack's performer is the Follower, and priority passes", FollowerBowsThenAttacks),
            ("Ranged 4 Attack targeting: a Follower, or a Personality without Followers -- a stronger card is a legal target that survives, not a followed Personality, not your own, not one away from the battlefield", AttackTargeting),
            ("The two abilities are independent: each has its own once-per-turn use, so Tensin may take both in one turn (with Reconnaissance), and a bowed Tensin can take neither", TwoIndependentAbilities),
            ("The Attack opens the same reaction window as any Attack, with the performing Follower as its performer", AttackReactionWindow),
            ("Every offered Tensin command validates on a fresh engine, with every alternate target", OfferedTensinCommandsValidate),
        ];
        runner.Run("follower-performer", tests);
    }

    private const string Recon = "hiruma-sniper-attack", ViaFollower = "tensin-follower-attack";

    private sealed record Rig(MatchState S, CardInstance Tensin, CardInstance Follower, CardInstance Target);

    // Tensin (player 0) at the battlefield with one unbowed Follower; a Force-4 enemy Personality (no Followers) to shoot at.
    private static Rig Battle(bool recon = false)
    {
        var s = Table(Phase.Battle);
        var tensin = Put(s, Support337.Tensin, 0, Zone.Battlefield, province: 0);
        var follower = Put(s, "training-follower", 0, Zone.Attached, province: 0, parent: tensin.Id);
        var target = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
        return new(s, tensin, follower, target);
    }

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Support337.Tensin);
        Expect(!d.ScenarioOnly && d.Type == CardType.Personality && d.Name == "Hiruma Tensin" && d.Clan == "Crab");
        // Verified against the scan: Force 3, Chi 3, Honor Requirement 0, Gold Cost 7, Personal Honor 2.
        Eq(d.Force, 3); Eq(d.Chi, 3); Eq(d.GoldCost, 7); Eq(d.PersonalHonor, 2); Eq(d.HonorRequirement, 0);
        foreach (var k in new[] { "Crab Clan", "Samurai", "Scout" }) Expect(d.Keywords!.Contains(k), k);
        Eq(d.Text, "Battle: If you have Reconnaissance: Ranged 4 Attack.\nBattle: Bow your performing Follower in Tensin's unit: Ranged 4 Attack.");
        Expect(d.Abilities!.SequenceEqual([Recon, ViaFollower]), "the Sniper's own ability, then the Follower-performed one"); Eq(d.Effect, "none");
        var sniper = CardCatalog.Get("oracle-l5r-3325");
        Expect(sniper.Abilities!.Contains(Recon), "the published Hiruma Sniper carries the very same ability");
        var a = AbilityCatalog.All[ViaFollower];
        Eq(a.Timing, "Battle"); Eq(a.Target, "attack"); Eq(a.Performer, "own-unbowed-follower-in-source-unit");
        Expect(a.CostOptions is { Length: 1 } && a.CostOptions[0].Verb == CostVerb.Bow, "the Follower's bow is its cost");
        Expect(ArtShipped(Support337.Tensin), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Support337.Tensin}.jpg");
    }

    private static void ReconnaissanceAttack()
    {
        var r = Battle(recon: false);
        var e = Engine(r.S);
        var refused = Offers(e, 0, Recon).Single();
        Expect(!refused.Enabled && refused.Reason!.Contains("Reconnaissance"), $"without Reconnaissance: {refused.Reason}");
        var with = Battle(recon: true);
        var we = Engine(with.S);
        var command = Offer(we, 0, Recon, with.Tensin.Id, with.Target.Id);
        Expect(command.PerformerId is null, "Tensin performs himself: no separate performer");
        Accept(we, command);
        Eq(we.State.Cards[with.Target.Id].Zone, Zone.Dead, "Ranged 4 vs Force 4");
        Expect(!we.State.Cards[with.Tensin.Id].Bowed && !we.State.Cards[with.Follower.Id].Bowed, "it costs nothing");
        Eq(we.State.PriorityPlayerId, 1);
    }

    private static void OneOfferPerUnbowedFollowerInHisUnit()
    {
        var r = Battle();
        var second = Put(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Tensin.Id);
        var bowed = Put(r.S, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: r.Tensin.Id);
        var other = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var othersFollower = Put(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: other.Id);
        var enemyHost = Put(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: enemyHost.Id);
        var e = Engine(r.S);
        var offers = Offers(e, 0, ViaFollower).Where(o => o.Enabled).ToArray();
        Expect(offers.Select(o => o.Command!.PerformerId).OrderBy(x => x).SequenceEqual(new[] { r.Follower.Id, second.Id }.OrderBy(x => x)), "one per unbowed Follower of Tensin's unit");
        Expect(offers.All(o => o.CardId == r.Tensin.Id && o.Label.Contains("Training ashigaru")), "each names the Follower that would perform");
        var command = Offer(e, 0, ViaFollower, r.Tensin.Id, r.Target.Id, r.Follower.Id);
        foreach (var bad in new[] { bowed.Id, othersFollower.Id, enemyFollower.Id, r.Tensin.Id, other.Id })
            Reject(e, command with { PerformerId = bad });
        Reject(e, command with { PerformerId = null });
    }

    private static void FollowerBowsThenAttacks()
    {
        var r = Battle();
        var e = Engine(r.S);
        var honor = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, ViaFollower, r.Tensin.Id, r.Target.Id, r.Follower.Id));
        var s = e.State;
        Expect(s.Cards[r.Follower.Id].Bowed, "the performing Follower is bowed: that is the cost");
        Expect(!s.Cards[r.Tensin.Id].Bowed, "Tensin himself is not");
        Eq(s.Cards[r.Target.Id].Zone, Zone.Dead, "Ranged 4 Attack destroyed the Force-4 target");
        Eq(s.Players[0].Honor, honor); Eq(s.PriorityPlayerId, 1, "priority passed");
        Expect(s.Cards[r.Follower.Id].Zone == Zone.Attached && s.Cards[r.Follower.Id].AttachedTo == r.Tensin.Id, "the Follower stays attached");
        // The bow is only a cost while the Follower can pay it: a bowed one is refused (see the offers test), and a Follower whose bow is negated cannot exist here
        // (a cost is never "an enemy's effect"): a Stalwart Follower still pays.
        var stalwart = Battle(); stalwart.Follower.GrantedKeywords.Add(new("Stalwart", "test", "Permanent"));
        var se = Engine(stalwart.S);
        Accept(se, Offer(se, 0, ViaFollower, stalwart.Tensin.Id, stalwart.Target.Id, stalwart.Follower.Id));
        Expect(se.State.Cards[stalwart.Follower.Id].Bowed, "Stalwart never applies to a cost the Follower's own controller pays");
    }

    private static void AttackTargeting()
    {
        var r = Battle();
        var strong = Put(r.S, "training-crane-veteran", 1, Zone.Battlefield, province: 0);             // Force 5: above the Attack's 4
        var followed = Put(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 0);           // Force 3, but has a Follower attached
        var followedFollower = Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);   // Force 2
        var home = Put(r.S, "training-lion-courtier", 1, Zone.Home);
        var own = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        var targets = Offers(e, 0, ViaFollower).First(o => o.Enabled).TargetIds!;
        Expect(targets.ToHashSet().SetEquals([r.Target.Id, followedFollower.Id, strong.Id]), $"a Force-4 Personality without Followers, a Follower and (a stronger card is still a legal target) the Force-5 one; got {string.Join(", ", targets)}");
        var command = Offer(e, 0, ViaFollower, r.Tensin.Id, r.Target.Id, r.Follower.Id);
        foreach (var bad in new[] { followed.Id, home.Id, own.Id })
            Reject(e, command with { TargetId = bad });
        // A target whose Force is above the Attack's strength is still legal (core rules): the Attack just fails to destroy it.
        var above = Battle();
        var aboveStrong = Put(above.S, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var ae = Engine(above.S);
        Accept(ae, Offer(ae, 0, ViaFollower, above.Tensin.Id, aboveStrong.Id, above.Follower.Id));
        Eq(ae.State.Cards[aboveStrong.Id].Zone, Zone.Battlefield, "the Force-5 target survives the Ranged 4 Attack");
        Expect(ae.State.Cards[above.Follower.Id].Bowed, "the cost is still paid");
        // No target at all is legal for an Attack (chosen later, "not required up front").
        Accept(e, command with { TargetId = null });
        Expect(e.State.Cards[r.Follower.Id].Bowed, "the cost is still paid");
    }

    private static void TwoIndependentAbilities()
    {
        var r = Battle(recon: true);
        var strong = Put(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, Recon, r.Tensin.Id, r.Target.Id));
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Expect(Offers(again, 0, Recon).All(o => !o.Enabled), "the first ability is spent");
        Accept(again, Offer(again, 0, ViaFollower, r.Tensin.Id, strong.Id, r.Follower.Id));
        Expect(again.State.Cards[strong.Id].Zone == Zone.Dead && again.State.Cards[r.Target.Id].Zone == Zone.Dead, "both Attacks resolved in one turn");
        var bowedTensin = Battle(recon: true); bowedTensin.Tensin.Bowed = true;
        var be = Engine(bowedTensin.S);
        Expect(Offers(be, 0, Recon).All(o => !o.Enabled) && Offers(be, 0, ViaFollower).All(o => !o.Enabled), "abilities on a bowed card cannot be used");
    }

    private static void AttackReactionWindow()
    {
        var r = Battle();
        var reactor = Put(r.S, "training-attack-reactor", 1, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, ViaFollower, r.Tensin.Id, r.Target.Id, r.Follower.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "an enemy reactor opens the ordinary Attack window");
        Eq(e.State.PendingAttack?.SourceId, r.Follower.Id, "the performing Follower is the Attack's performer");
        Eq(e.State.PendingAttack?.Strength, 4); Eq(e.State.PendingAttack?.Kind, "Ranged"); Eq(e.State.PendingAttack?.TargetId, r.Target.Id);
        Expect(e.State.Cards[r.Follower.Id].Bowed, "the cost was paid at announcement");
        Expect(reactor.Id.Length > 0);
    }

    private static void OfferedTensinCommandsValidate()
    {
        var r = Battle(recon: true);
        Put(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Tensin.Id);
        Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Target.Id);
        var e = Engine(r.S);
        OfferedCommandsValidate(e, () => Engine(r.S), alternateTargets: true);
    }
}
