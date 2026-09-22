using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #435: reevaluates the 8 Before the Dawn cards left blocked on "Dueling/Focus Effects (not
// started at all)" -- a note written before Dueling (#204) and Focus Effects (issue #419's
// FocusEffectChoice.cs) existed. Three are now wirable in full: Hiromi's Vassals and Shamsir share a
// Focus Effect shape ("you may attach this card to one of your Personalities, ignoring Gold cost" --
// FocusEffectChoice.cs's third choice kind, alongside destroy-an-attachment and take-a-Strategy), and
// Home Guard's mandatory +3F Focus Effect, +3F-while-defending static and Battle bow-and-Honor-gain
// are each a direct reuse of an existing shape. The other five (Flanking Unit, Broken Cipher,
// Paralyzing Touch, Splintered Weapon, The Courage of the Mantis) stay blocked on a genuinely unbuilt
// gap -- see their refreshed BLOCKERS notes in tools/content/build_card_inventory.py.
internal static class DuelingFocusEffectReevaluationTests
{
    private const string Ichizo = "oracle-l5r-5104", HiromisVassals = "oracle-l5r-3280", HomeGuard = "oracle-l5r-3413", Shamsir = "oracle-l5r-6702";

    public static void Run(TestRunner runner) => runner.Run("dueling-focus-effect-reevaluation", [
        ("Published shapes: Hiromi's Vassals, Home Guard and Shamsir are the printed cards with their abilities, traits and art", PublishedShapes),
        ("Hiromi's Vassals/Shamsir's Focus Effect: after the duel, its owner may attach it to one of their own Personalities, ignoring Gold cost -- declining, or having no legal Personality, leaves it to the discard pile as usual", AttachSelfFocusEffect),
        ("Hiromi's Vassals: Bow this card, Ranged 6 Attack", HiromisVassalsAttack),
        ("Shamsir: target an enemy Personality or Follower, -5F", ShamsirAttack),
        ("Home Guard's Focus Effect gives its owner's Personality in the duel +3F, mandatory; its static bonus is +3F only while defending", HomeGuardFocusAndStatic),
        ("Home Guard: target an enemy card without attachments, bow it, gain 2 Honor", HomeGuardBattle),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var vassals = CardCatalog.Get(HiromisVassals); var guard = CardCatalog.Get(HomeGuard); var shamsir = CardCatalog.Get(Shamsir);
        Check(vassals.Name == "Hiromi's Vassals" && vassals.Type == CardType.Follower && (vassals.Abilities ?? []).SequenceEqual(["hiromis-vassals-focus", "hiromis-vassals-attack"]), "Hiromi's Vassals");
        Check(guard.Name == "Home Guard" && guard.Type == CardType.Follower && guard.Effect == "force-while-defending-3" && (guard.Abilities ?? []).SequenceEqual(["home-guard-focus", "home-guard-bow"]), "Home Guard");
        Check(shamsir.Name == "Shamsir" && shamsir.Type == CardType.Item && (shamsir.Abilities ?? []).SequenceEqual(["shamsir-focus", "shamsir-attack"]), "Shamsir");
        foreach (var id in new[] { HiromisVassals, HomeGuard, Shamsir })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Player 1's Mirumoto Ichizo, Experienced (Iaijutsu, "duel him, destroy the loser") challenges
    // player 0's Personality. Player 0's own hand starts empty; a test adds its own Focus Effect card.
    private static (MatchState S, CardInstance Ichizo, CardInstance Target) IaijutsuTable()
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        return (s, ichizo, target);
    }

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // Drives Focusing/DuelistRefocus only, stopping the moment anything else needs a decision (a
    // "focusEffectChoice", or the duel already having resolved) -- the caller inspects what is left.
    private static void DriveToFocusEffects(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
    }

    private static void AttachSelfFocusEffect()
    {
        foreach (var focusCard in new[] { HiromisVassals, Shamsir })
        {
            // Accept: player 0's own duelist survives (boosted Chi, so player 0 wins and nothing dies), then chooses it to receive the attachment.
            var (s, ichizo, target) = IaijutsuTable();
            ChiBoost(target, 10);
            var card = Add(s, focusCard, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
            ClosePassing(e);
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: card.Id));
            DriveToFocusEffects(e);
            Eq(e.State.PendingChoice?.Type, "focusEffectChoice", $"{focusCard}: the choice opens after the duel resolves");
            Eq(e.State.PendingChoice!.PlayerId, 0, $"{focusCard}: it belongs to the focus card's own owner");
            var offered = e.QueryActions(0).Where(a => a.Type == "chooseFocusEffect").ToArray();
            Check(offered.Any(o => o.Command!.TargetId == target.Id), $"{focusCard}: the owner's own surviving Personality is offered");
            Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, CardId: card.Id, TargetId: target.Id));
            Eq(e.State.Cards[card.Id].Zone, Zone.Attached, $"{focusCard}: attached itself");
            Eq(e.State.Cards[card.Id].AttachedTo, target.Id, $"{focusCard}: attached to the chosen Personality");
            Eq(e.State.Cards[card.Id].OwnerId, 0, $"{focusCard}: still controlled by its own owner");

            // Decline: the focused card is simply discarded, as any unattached focused card is.
            var (s2, ichizo2, target2) = IaijutsuTable();
            ChiBoost(target2, 10);
            var card2 = Add(s2, focusCard, 0, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Offer(e2, 1, "ichizo-exp-duel", ichizo2.Id, target2.Id));
            ClosePassing(e2);
            Accept(e2, new GameCommand("focus", 0, e2.Version, CardId: card2.Id));
            DriveToFocusEffects(e2);
            Eq(e2.State.PendingChoice?.Type, "focusEffectChoice", $"{focusCard}: the choice opens (decline case)");
            Accept(e2, new GameCommand("chooseFocusEffect", 0, e2.Version, CardId: card2.Id, Choice: "decline"));
            Eq(e2.State.Cards[card2.Id].Zone, Zone.FateDiscard, $"{focusCard}: declined, discarded as usual");

            // No legal Personality of the owner's own: player 0's duelist is this duel's OWN loser and is destroyed by it, leaving player 0 with nothing to attach to.
            var (s3, ichizo3, target3) = IaijutsuTable();
            ChiBoost(ichizo3, 20); // wins regardless of target's Chi 3 plus whichever card's own Focus Value is focused
            var card3 = Add(s3, focusCard, 0, Zone.Hand);
            var e3 = MatchEngine.FromTestState(s3);
            Accept(e3, Offer(e3, 1, "ichizo-exp-duel", ichizo3.Id, target3.Id));
            ClosePassing(e3);
            Accept(e3, new GameCommand("focus", 0, e3.Version, CardId: card3.Id));
            DriveToFocusEffects(e3);
            Eq(e3.State.Cards[target3.Id].Zone, Zone.Dead, $"{focusCard}: control -- player 0 lost the duel, their own Personality is destroyed");
            Check(e3.State.PendingChoice is not { Type: "focusEffectChoice" }, $"{focusCard}: nothing to attach to, so nothing is asked");
            Eq(e3.State.Cards[card3.Id].Zone, Zone.FateDiscard, $"{focusCard}: discarded, having had nothing to attach to");
            Eq(e3.State.PendingDuel, null, $"{focusCard}: the duel finished on its own");
        }
    }

    private static void HiromisVassalsAttack()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var vassals = Add(s, HiromisVassals, 0, Zone.Attached, province: 0, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("hiromis-vassals-attack", vassals.Id), 6, "Ranged 6 Attack");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: vassals.Id, AbilityId: "hiromis-vassals-attack"));
        Check(e.State.Cards[vassals.Id].Bowed, "bows itself as its own cost");
    }

    private static void ShamsirAttack()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shamsir = Add(s, Shamsir, 0, Zone.Attached, province: 0, parent: host.Id);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(target, 6); // base Force 3 + 6 = 9, so the -5F is visible without being clamped at 0
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "shamsir-attack", shamsir.Id, target.Id));
        Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == target.Id).Force, 4, "the target's Force is reduced by 5 (base 3 + 6 boost)");
    }

    private static void HomeGuardFocusAndStatic()
    {
        // Mandatory Focus Effect: player 0's own duelist (surviving, boosted Chi) gets +3F, whether or not it wins.
        var (s, ichizo, target) = IaijutsuTable();
        ChiBoost(target, 10);
        var card = Add(s, HomeGuard, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var baseForce = e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force;
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        ClosePassing(e);
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: card.Id));
        DriveToFocusEffects(e);
        Eq(e.State.PendingDuel, null, "Home Guard's Focus Effect needs no choice: the duel finishes on its own");
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force, baseForce + 3, "the duel's own Personality on Home Guard's owner's side gained +3F");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");

        // Static: +3F only while defending (Table's own convention: player 1 attacks, player 0 defends).
        var s2 = Table();
        var defenderHost = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var defendingGuard = Add(s2, HomeGuard, 0, Zone.Attached, province: 0, parent: defenderHost.Id);
        var attackerHost = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var attackingGuard = Add(s2, HomeGuard, 1, Zone.Attached, province: 0, parent: attackerHost.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.GetView(0).Players[0].Army.Single(c => c.Id == defendingGuard.Id).Force, 4, "defending: base Force 1 + 3");
        Eq(e2.GetView(1).Players[1].Army.Single(c => c.Id == attackingGuard.Id).Force, 1, "attacking: no bonus");
    }

    private static void HomeGuardBattle()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var guard = Add(s, HomeGuard, 0, Zone.Attached, province: 0, parent: host.Id);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Offer(e, 0, "home-guard-bow", guard.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "the target is bowed");
        Eq(Honor(e, 0), before + 2, "the acting player gains 2 Honor");
        // A Personality carrying an attachment is not itself a legal target (though its own Follower still is).
        var s2 = Table(Phase.Battle, 0);
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var guard2 = Add(s2, HomeGuard, 0, Zone.Attached, province: 0, parent: host2.Id);
        var target2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var follower2 = Add(s2, "training-follower-force-effect", 1, Zone.Attached, province: 0, parent: target2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        var offered2 = Options(e2, 0, "home-guard-bow", guard2.Id).FirstOrDefault(a => a.Enabled);
        Check(offered2 is not null && (offered2.TargetIds?.Contains(follower2.Id) ?? false) && !(offered2.TargetIds?.Contains(target2.Id) ?? false),
            "the Personality carrying an attachment is excluded; its own attached Follower is still a legal target");
    }
}
