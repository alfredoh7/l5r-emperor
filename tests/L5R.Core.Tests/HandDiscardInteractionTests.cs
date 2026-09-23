using L5R.Core;
using static RegressionAssertions;

internal static class HandDiscardInteractionTests
{
    public static void Run(TestRunner runner) => runner.Run("hand-discard-interaction", new (string, Action)[]
    {
        ("Offered discards use modified limits 7, 8 and 10 for both seats; exact defaults execute", OfferedDiscards),
        ("Below/at/above-limit End Phase boundaries use the same current limit", Boundaries),
        ("Wrong count, duplicates, foreign cards, wrong seat and stale version reject atomically", InvalidSelections),
        ("A nondefault valid selection discards exactly the chosen cards; private views stay private", AlternateSelection)
    });
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static CardInstance Add(MatchState s, string definition, int owner, Zone zone)
    {
        var card = new CardInstance { Id = "discard-regression-" + s.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand };
        s.Cards.Add(card.Id, card);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(card.Id);
        if (zone == Zone.FateDeck) s.Players[owner].FateDeck.Add(card.Id);
        return card;
    }
    private static MatchEngine Prepare(int seat, int limit, int afterDraw)
    {
        var s = MatchEngine.CreateEconomyScenario(17).State;
        foreach (var c in s.Cards.Values.Where(c => c.Zone != Zone.Stronghold).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.Hand.Clear(); p.FateDeck.Clear(); p.DynastyDeck.Clear(); p.TurnsTaken = 2; foreach (var v in p.Provinces) v.CardId = null; }
        s.ActivePlayerId = s.PriorityPlayerId = seat; s.Phase = Phase.Dynasty; s.PendingChoice = null;
        if (limit == 7) Add(s, "oracle-l5r-10532", 1-seat, Zone.Home);
        if (limit == 10)
            for (var i=0; i<2; i++) Add(s, PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Asahina Tsugio").Id, 1-seat, Zone.Home);
        for (var i=0; i<afterDraw-1; i++) Add(s, "training-rally", seat, Zone.Hand);
        for (var i=0; i<3; i++) Add(s, "training-rally", 1-seat, Zone.Hand);
        for (var p=0; p<2; p++) for (var i=0; i<8; i++) Add(s, "training-rally", p, Zone.FateDeck);
        var e = MatchEngine.FromTestState(s);
        Check(e.GetView(seat).Players[seat].HandLimit!.Maximum == limit, "Fixture did not establish authoritative limit");
        Accept(e, new GameCommand("endDynasty", seat, e.Version));
        return e;
    }
    private static void OfferedDiscards()
    {
        foreach (var limit in new[] {7,8,10}) foreach (var seat in new[] {0,1})
        {
            var e=Prepare(seat,limit,limit+1);
            var action=e.QueryActions(seat).Single(a=>a.Type=="discardHand");
            Check(action.Label=="Discard 1 card(s)", $"limit {limit}, seat {seat}: wrong offer {action.Label}");
            Check(action.Command!.CardIds!.Length==1, "Engine default must match the enforced count");
            Check(action.TargetIds!.SequenceEqual(e.State.Players[seat].Hand), "Only the acting hand is selectable");
            Check(!e.QueryActions(1-seat).Any(a=>a.Type=="discardHand"), "Opponent received private discard options");
            Accept(e,action.Command!);
            Check(e.State.Players[seat].Hand.Count==limit && e.State.ActivePlayerId==1-seat && e.State.PendingChoice is null, "Discard did not finish the turn at the true limit");
        }
    }
    private static void Boundaries()
    {
        foreach (var limit in new[] {7,8,10}) foreach (var seat in new[] {0,1}) foreach (var delta in new[] {-1,0,1,3})
        {
            var e=Prepare(seat,limit,limit+delta);
            Check((e.State.PendingChoice?.Type=="discardHand")== (delta>0), "Wrong threshold for pending discard");
            if (delta>0) { var action=e.QueryActions(seat).Single(a=>a.Type=="discardHand"); Check(action.Command!.CardIds!.Length==delta, "Wrong offered count at modified boundary"); Accept(e,action.Command!); }
        }
    }
    private static void InvalidSelections()
    {
        foreach (var limit in new[] {7,10}) foreach (var seat in new[] {0,1})
        {
            var e=Prepare(seat,limit,limit+2); var hand=e.State.Players[seat].Hand.ToArray();
            var command=new GameCommand("discardHand",seat,e.Version,CardIds:hand.Take(2).ToArray());
            Reject(e,command with {CardIds=[]}); Reject(e,command with {CardIds=[hand[0]]});
            Reject(e,command with {CardIds=hand.Take(3).ToArray()}); Reject(e,command with {CardIds=[hand[0],hand[0]]});
            Reject(e,command with {CardIds=[hand[0],e.State.Players[1-seat].Hand[0]]});
            Reject(e,command with {PlayerId=1-seat}); Reject(e,command with {ExpectedVersion=e.Version-1});
            Accept(e,command); Reject(e,command);
        }
    }
    private static void AlternateSelection()
    {
        foreach (var seat in new[] {0,1})
        {
            var e=Prepare(seat,10,12); var hand=e.State.Players[seat].Hand.ToArray(); var selected=hand.TakeLast(2).ToArray();
            HiddenIdsAbsent(e.GetView(1-seat),hand);
            Accept(e,new GameCommand("discardHand",seat,e.Version,CardIds:selected));
            Check(e.State.Players[seat].Hand.SequenceEqual(hand.Except(selected)), "Different cards were discarded");
            Check(selected.All(id=>e.State.Cards[id].Zone==Zone.FateDiscard && e.State.Cards[id].FaceUp), "Chosen discards did not become public");
            HiddenIdsAbsent(e.GetView(1-seat),hand.Except(selected));
        }
    }
}
