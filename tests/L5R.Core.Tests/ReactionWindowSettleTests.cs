using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #644 (point 3, Mendon): once a Reaction has resolved, a player who holds no legal Reaction is never asked to pass. The opportunity goes on to the other player when THEY hold one;
// otherwise the window closes and play carries on normally (SettleReactionWindow). Authoritative in Core: no client-side skipping.
internal static class ReactionWindowSettleTests
{
    private const string Precision = "oracle-l5r-6047", Ruya = "oracle-l5r-7789", StormlessFury = "oracle-l5r-7512", CraneSword = "oracle-l5r-9877";

    public static void Run(TestRunner runner) => runner.Run("reaction-window-settle", [
        ("Satoshi's Dual Warfans: after its entering-play draw the window closes at once, with no Pass offered to anyone", WarfansDrawClosesWindow),
        ("Celestial Sword of the Crane: after its entering-play Honor gain the window closes at once, with no Pass offered to anyone", CraneSwordHonorClosesWindow),
        ("An Attack window: a Reaction that leaves nobody another one closes the window and the Attack resolves", AttackReactionClosesWindow),
        ("An Attack window: a legal follow-up Reaction of the other player is offered to them, not auto-passed, and the player with none is not asked to pass", AttackFollowUpGoesToOtherPlayer),
        ("An Attack window passed by the only player holding a Reaction closes after that one pass", AttackPassNeedsNoSecondPass),
        ("A bow window: the watcher's Reaction closes it", BowReactionClosesWindow),
        ("An after-action window: a resolved Reaction closes it", AfterActionReactionClosesWindow),
        ("A battle-action window: the second Reaction of the same card closes it", BattleActionReactionClosesWindow),
        ("A refused pass in a closed window changes nothing", RejectionLeavesStateAlone),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static GameCommand Pass(MatchEngine e, int player) => new("declineReaction", player, e.Version);
    private static bool OffersPass(MatchEngine e, int player) => e.QueryActions(player).Any(a => a.Type == "declineReaction" && a.Enabled);
    private static bool Closed(MatchEngine e) => e.GetView(0).Events.Any(ev => ev.Type == "reactionClosed" && ev.Message.Contains("No reaction was left to take"));
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    private static void NothingToPass(MatchEngine e, string why)
    {
        Check(e.State.PendingChoice is null, why + ": the window is closed");
        Check(!OffersPass(e, 0) && !OffersPass(e, 1), why + ": nobody is offered a Pass reaction");
        Check(Closed(e), why + ": the engine said why the window closed");
    }

    private static void WarfansDrawClosesWindow()
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0; s.Players[0].Clan = "Lion";
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var warfans = Add(s, "oracle-l5r-6481", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: warfans.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds, TargetId: host.Id));
        Eq(e.State.PendingChoice?.Type, "entryReaction");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: warfans.Id, AbilityId: "satoshi-warfans-draw"));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "the card was drawn");
        NothingToPass(e, "after the draw");
        Eq(e.State.PriorityPlayerId, 1, "play carries on normally: the opponent has the next opportunity");
    }

    private static void CraneSwordHonorClosesWindow()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home); crane.GrantedKeywords.Add(new("Crane Clan", "test-fixture", GrantDuration.Permanent)); // a fixture prints no Clan keyword
        var sword = Add(s, CraneSword, 0, Zone.Hand);
        Gold(s, 0, 4);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == sword.Id);
        var honor = Honor(e, 0);
        Check(equip.Command is not null, "equip offered: " + equip.Reason);
        Accept(e, equip.Command! with { TargetId = crane.Id });
        Eq(e.State.PendingChoice?.Type, "entryReaction");
        Accept(e, Enabled(e, 0, "celestial-sword-crane-honor").Command!);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor as it entered play");
        NothingToPass(e, "after the Honor gain");
    }

    // Player 1 attacks player 0's Personality (a Melee/Ranged Attack from a Strategy); player 0 holds Precision (one Reaction: its two halves share one use), and optionally a second Reaction fixture.
    private static MatchEngine AttackRig(int reactorOwner, out CardInstance precision)
    {
        var s = Table(Phase.Battle, 1);
        precision = Add(s, Precision, 0, Zone.Hand);
        var attacker = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        if (reactorOwner >= 0) Add(s, "training-attack-reactor", reactorOwner, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-honor-cost-attack", attacker.Id, defender.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the attack reaction window opens");
        return e;
    }

    private static void AttackReactionClosesWindow()
    {
        var e = AttackRig(-1, out var precision);
        Eq(e.State.PriorityPlayerId, 0, "the only player holding a Reaction acts first");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: precision.Id, AbilityId: "precision-boost"));
        Check(e.State.PendingAttack is null, "the Attack resolved");
        NothingToPass(e, "after Precision");
    }

    private static void AttackFollowUpGoesToOtherPlayer()
    {
        var e = AttackRig(1, out var precision);
        // The attacker holds a Reaction fixture too, and is the turn player, so the window starts with him.
        Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Pass(e, 1));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "player 0 still holds Precision, so the window stays open for him");
        Eq(e.State.PriorityPlayerId, 0);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: precision.Id, AbilityId: "precision-boost"));
        // Player 0 holds nothing more; player 1 still holds the fixture's two Reactions: the opportunity is theirs, and Pass is offered to them alone.
        Eq(e.State.PendingChoice?.Type, "attackReaction", "a legal follow-up exists for the other player: the window stays open");
        Eq(e.State.PriorityPlayerId, 1);
        Check(OffersPass(e, 1) && e.QueryActions(1).Any(a => a.Type == "useAbility" && a.Enabled), "the other player is offered his Reaction and the Pass");
        Check(!OffersPass(e, 0), "the player with nothing left is not offered a Pass");
        Accept(e, Pass(e, 1));
        NothingToPass(e, "once the player holding the last Reaction has passed");
    }

    private static void AttackPassNeedsNoSecondPass()
    {
        var e = AttackRig(-1, out _);
        Eq(e.State.PriorityPlayerId, 0);
        Accept(e, Pass(e, 0));
        Check(e.State.PendingAttack is null, "the Attack resolved");
        NothingToPass(e, "after the one pass");
    }

    private static void BowReactionClosesWindow()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var ruya = Add(s, Ruya, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var bowed = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-bow-at-battle", bower.Id, bowed.Id));
        Eq(e.State.PendingChoice?.Type, "bowReaction");
        Accept(e, Use(e, 0, "ruya-bow", ruya.Id, other.Id));
        Check(e.State.Cards[other.Id].Bowed, "her Reaction resolved");
        NothingToPass(e, "after Tamori Ruya");
    }

    private static void AfterActionReactionClosesWindow()
    {
        var s = Table(Phase.Battle, 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var watcher = Add(s, "training-after-action-watcher", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction");
        var honor = Honor(e, 1);
        Accept(e, Enabled(e, 1, "training-after-action-resolved-gain", watcher.Id).Command!);
        Eq(Honor(e, 1), honor + 3, "the reaction resolved");
        NothingToPass(e, "after the after-action Reaction");
    }

    private static void BattleActionReactionClosesWindow()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
        var first = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var second = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var fury = Add(s, StormlessFury, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: first.Id, AbilityId: "training-weapon-attack"));
        Eq(e.State.PendingChoice?.Type, "battleActionReaction");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fury.Id, AbilityId: "stormless-fury-attach"));
        NothingToPass(e, "after the attach: the card's own errata bars its second Reaction against the same action, so nothing is left to take");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: second.Id, AbilityId: "training-weapon-attack"));
        Eq(e.State.PendingChoice?.Type, "battleActionReaction", "offered again for a fresh action");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fury.Id, AbilityId: "stormless-fury-bow"));
        Check(e.State.Cards[second.Id].Bowed, "the second Reaction bowed the new action's performer");
        NothingToPass(e, "after the second Reaction");
    }

    private static void RejectionLeavesStateAlone()
    {
        var e = AttackRig(-1, out var precision);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: precision.Id, AbilityId: "precision-boost"));
        NothingToPass(e, "after Precision");
        var before = JsonSerializer.Serialize(e.State);
        foreach (var player in new[] { 0, 1 })
        {
            Check(!e.Execute(Pass(e, player)).Accepted, $"player {player} cannot pass a window that is not open");
            Check(!e.Execute(new GameCommand("useAbility", player, e.Version, CardId: precision.Id, AbilityId: "precision-reduce")).Accepted, $"player {player} cannot play the spent Reaction");
        }
        Check(JsonSerializer.Serialize(e.State) == before, "rejected commands changed nothing");
    }
}
