using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #803 (Mendon): when both players hold a Reaction for an alternating window they announce the Reactions they will play, the turn player orders them when the announcements are
// not all their own, and they resolve one at a time. A window only one player can react in is unchanged. Driven here through the Engage window.
internal static class ReactionOrderingTests
{
    private const string Honor = "training-engage-honor-strategy", Ability = "training-engage-gain-honor";

    public static void Run(TestRunner runner) => runner.Run("reaction-ordering", [
        ("A window both players can react in opens with the turn player announcing; nothing is played yet", AnnouncementOpens),
        ("Only the turn player announced: the Reactions resolve in the order announced and the window closes after them", TurnPlayerOnlyResolvesInAnnouncedOrder),
        ("Both announced: the turn player chooses the order, the other player's Reaction may go first, and the window closes after the last", BothAnnounceTurnPlayerOrders),
        ("Nobody announcing closes the window; a retracted announcement is not played; a Reaction that became illegal fizzles", RetractAndFizzle),
        ("A window only one player can react in is unchanged: played at once, in the order played, no announcement", OneHolderUnchanged),
        ("Only the other player announced: the turn player still orders their Reactions", OtherPlayerOnly),
        ("Pass all battles: a flagged player holds no Reaction, so no announcement is asked and their window is declined for them", FlaggedPlayerIsNotAsked),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private sealed record Fixture(MatchEngine Engine, CardInstance Mine, CardInstance Mine2, CardInstance Theirs);

    private static Fixture Open(bool attackerHolds = true, bool defenderHolds = true)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s, Honor, 0, Zone.Hand); var mine2 = Add(s, Honor, 0, Zone.Hand);
        var theirs = Add(s, Honor, 1, Zone.Hand);
        if (!attackerHolds) { s.Players[0].Hand.Clear(); s.Cards.Remove(mine.Id); s.Cards.Remove(mine2.Id); }
        if (!defenderHolds) { s.Players[1].Hand.Clear(); s.Cards.Remove(theirs.Id); }
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        return new(e, mine, mine2, theirs);
    }

    private static GameCommand Cmd(MatchEngine e, int player, string type, string? card = null, string? ability = null, string? target = null) => new(type, player, e.Version, card, AbilityId: ability, TargetId: target);
    private static ActionOption Option(MatchEngine e, int player, string type, string? card = null) => e.QueryActions(player).First(o => o.Type == type && o.Enabled && (card is null || o.CardId == card));
    private static void Announce(MatchEngine e, int player, CardInstance card) => Accept(e, Option(e, player, "reactionAnnounce", card.Id).Command!);
    private static void Done(MatchEngine e, int player) => Accept(e, Option(e, player, "reactionAnnounceDone").Command!);
    private static void Play(MatchEngine e, int player, CardInstance card) => Accept(e, Cmd(e, player, "useAbility", card.Id, Ability));

    private static void AnnouncementOpens()
    {
        var f = Open(); var e = f.Engine;
        Eq(e.State.PendingChoice?.Type, "engageReaction"); Check(e.State.PendingReactionOrder is { Stage: "Announce", Turn: 0 }, "announce stage, the turn player first");
        Eq(e.State.PriorityPlayerId, 0);
        var mine = e.QueryActions(0);
        Check(mine.Count(o => o.Type == "reactionAnnounce") == 2 && mine.Any(o => o.Type == "reactionAnnounceDone"), "both Reactions in hand may be announced, or none");
        Check(!mine.Any(o => o.Type is "useAbility" or "declineReaction"), "nothing is played from the window while announcing");
        Check(e.QueryActions(1).All(o => o.Type is "wait" or "concede"), "the other player waits");
        Reject(e, Cmd(e, 0, "useAbility", f.Mine.Id, Ability));
        Reject(e, Cmd(e, 1, "reactionAnnounceDone"));
        Eq(e.State.Players[0].Hand.Count, 2, "nothing was played");
        Check(e.GetView(0).ReactionOrder is { Stage: "Announce" }, "the procedure is in the view");
    }

    private static void TurnPlayerOnlyResolvesInAnnouncedOrder()
    {
        var f = Open(); var e = f.Engine; var honor = e.State.Players[0].Honor; var theirs = e.State.Players[1].Honor;
        Announce(e, 0, f.Mine2); Announce(e, 0, f.Mine);
        Done(e, 0);
        Eq(e.State.PendingReactionOrder?.Turn, 1, "then the other player announces");
        Done(e, 1);
        Eq(e.State.PendingReactionOrder?.Stage, "Resolve", "all announcements are the turn player's: no ordering");
        Eq(e.State.PendingReactionOrder?.Queue[0].CardId, f.Mine2.Id, "in the order announced");
        Reject(e, Cmd(e, 0, "useAbility", f.Mine.Id, Ability)); // not the next one
        Play(e, 0, f.Mine2);
        Eq(e.State.Players[0].Honor, honor + 1); Eq(e.State.Cards[f.Mine2.Id].Zone, Zone.FateDiscard);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the window waits for the second");
        Play(e, 0, f.Mine);
        Eq(e.State.Players[0].Honor, honor + 2);
        Check(e.State.PendingChoice is null && e.State.PendingReactionOrder is null, "closed after the last announced Reaction");
        Eq(e.State.Players[1].Honor, theirs, "the other player announced nothing and played nothing");
        Eq(e.State.PriorityPlayerId, 1, "no claim: the Defender has the first opportunity");
    }

    private static void BothAnnounceTurnPlayerOrders()
    {
        var f = Open(); var e = f.Engine; var honor1 = e.State.Players[1].Honor;
        Announce(e, 0, f.Mine); Done(e, 0);
        Announce(e, 1, f.Theirs); Done(e, 1);
        Eq(e.State.PendingReactionOrder?.Stage, "Order"); Eq(e.State.PriorityPlayerId, 0, "the turn player orders");
        var orders = e.QueryActions(0).Where(o => o.Type == "reactionOrder").ToArray();
        Eq(orders.Length, 2); Check(e.QueryActions(1).All(o => o.Type is "wait" or "concede"));
        Reject(e, Cmd(e, 1, "reactionOrder", target: orders[0].Command!.TargetId));
        Accept(e, orders.First(o => o.CardId == f.Theirs.Id).Command!); // the other player's Reaction first
        Eq(e.State.PendingReactionOrder?.Stage, "Resolve"); Eq(e.State.PriorityPlayerId, 1);
        Check(e.QueryActions(0).All(o => o.Type is "wait" or "concede"), "the turn player waits for the Reaction ahead of theirs");
        var offered = e.QueryActions(1);
        Check(offered.Any(o => o.Type == "useAbility" && o.CardId == f.Theirs.Id) && offered.Any(o => o.Type == "declineReaction"), "its owner may play it or skip it");
        Play(e, 1, f.Theirs);
        Eq(e.State.Players[1].Honor, honor1 + 1); Eq(e.State.PriorityPlayerId, 0);
        Play(e, 0, f.Mine);
        Check(e.State.PendingChoice is null && e.State.PendingReactionOrder is null, "closed");
    }

    private static void RetractAndFizzle()
    {
        var none = Open(); var e = none.Engine;
        Done(e, 0); Done(e, 1);
        Check(e.State.PendingChoice is null && e.State.PendingReactionOrder is null, "nothing announced: the window closed");
        Eq(e.State.PriorityPlayerId, 1, "no claim: the Defender acts first");

        var f = Open(); e = f.Engine;
        Announce(e, 0, f.Mine); Announce(e, 0, f.Mine2);
        Accept(e, Cmd(e, 0, "reactionUnannounce", target: e.State.PendingReactionOrder!.Announced[0].Id));
        Done(e, 0); Announce(e, 1, f.Theirs); Done(e, 1);
        Accept(e, Option(e, 0, "reactionOrder", f.Theirs.Id).Command!);
        Eq(e.State.PendingReactionOrder?.Queue.Count, 2);
        // The first one is skipped by its owner; the second is no longer in hand (discarded by another effect) and fizzles.
        var s = e.State; s.Cards[e.State.PendingReactionOrder!.Queue[1].CardId!].Zone = Zone.FateDiscard; s.Players[0].Hand.Clear();
        e = MatchEngine.FromTestState(s);
        Accept(e, Option(e, 1, "declineReaction").Command!);
        Check(e.State.PendingChoice is null && e.State.PendingReactionOrder is null, "the skipped and the fizzled Reactions leave the window closed");
        Check(e.State.Events.Any(x => x.Type == "reactionFizzled"), "the fizzle is logged");
    }

    private static void OneHolderUnchanged()
    {
        var f = Open(attackerHolds: true, defenderHolds: false); var e = f.Engine;
        Check(e.State.PendingReactionOrder is null, "no announcement when only one player can react");
        var honor = e.State.Players[0].Honor;
        Play(e, 0, f.Mine); Eq(e.State.Players[0].Honor, honor + 1, "played at once");
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the window carries on as before");
    }

    private static void OtherPlayerOnly()
    {
        var f = Open(); var e = f.Engine;
        Done(e, 0); Announce(e, 1, f.Theirs); Done(e, 1);
        Eq(e.State.PendingReactionOrder?.Stage, "Resolve", "a single Reaction needs no ordering");
        Eq(e.State.PriorityPlayerId, 1); Play(e, 1, f.Theirs);
        Check(e.State.PendingChoice is null);
    }

    private static void FlaggedPlayerIsNotAsked()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s, Honor, 0, Zone.Hand); Add(s, Honor, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("passAllBattles", 0, e.Version));
        Check(e.State.PendingReactionOrder is null, "no announcement for a window one side has declined");
        Eq(e.State.PendingChoice?.Type, "engageReaction"); Eq(e.State.PriorityPlayerId, 1, "the Attacker's Reaction was declined for them");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Hand);
    }
}
