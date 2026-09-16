using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

static class DuelDemoTests
{
    static void Check(bool value,string message="Assertion failed") { if(!value)throw new Exception(message); }
    static void Equal<T>(T actual,T expected) => Check(EqualityComparer<T>.Default.Equals(actual,expected),$"Expected {expected}, got {actual}");
    static GameCommand Action(MatchEngine e,string type,Func<GameCommand,bool>? predicate=null) => e.QueryActions(e.State.PriorityPlayerId)
        .Where(a=>a.Enabled && a.Type==type).Select(a=>a.Command!).First(c=>predicate?.Invoke(c)??true);
    static void Send(MatchEngine e,GameCommand c)=>Check(e.Execute(c).Accepted,$"Rejected {c.Type}");
    static void Begin(MatchEngine e,string scenario="steel") => Send(e,Action(e,"useAbility",c=>c.AbilityId==DuelDemoScenarios.Get(scenario).ChallengeAbility));
    public static void Canonical()
    {
        var e=MatchEngine.CreateDuelDemo();Begin(e);
        var d=e.GetView(0).PendingDuel!;Check(d.DuelId.Length>0,"stable duel id");
        var command=Action(e,"focus",c=>c.Choice=="deck");Send(e,command);
        Check(!e.Execute(command).Accepted,"duplicate rejected");
        var lion=e.GetView(0).Players[1];Equal(lion.FocusCount,1);Equal(lion.Focused!.Length,0);
        var hidden=e.GetView(1).Players[1].Focused![0].Id;
        Check(!JsonSerializer.Serialize(e.GetView(0)).Contains(hidden),"opponent focus id not leaked in view/events");
        // Integration regression: public Event/Celestial reminders and private duel focuses coexist in one view.
        var combined=e.State;
        combined.Cards.Add("public-event",new CardInstance{Id="public-event",DefinitionId="oracle-l5r-7675",OwnerId=1,Zone=Zone.EventInPlay,FaceUp=true});
        combined.Cards.Add("public-celestial",new CardInstance{Id="public-celestial",DefinitionId="oracle-l5r-4658",OwnerId=1,Zone=Zone.Celestial,FaceUp=true});
        var combinedView=MatchEngine.FromTestState(combined).GetView(0);
        var roundTrip=JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(combinedView))!;
        Equal(roundTrip.Players[1].FocusCount,1);Equal(roundTrip.Players[1].Focused!.Length,0);
        Equal(roundTrip.Players[1].EventsInPlay!.Single().Definition!.Id,"oracle-l5r-7675");
        Equal(roundTrip.Players[1].Celestials!.Single().Definition!.Id,"oracle-l5r-4658");
        HiddenIdsAbsent(roundTrip,[hidden]);
        Send(e,Action(e,"focus",c=>c.CardId is { } id && e.GetView(0).Players[0].Hand.Any(h=>h.Id==id&&h.Definition!.Id=="oracle-l5r-7996")));
        Send(e,Action(e,"strike"));
        var view=e.GetView(0);var record=view.LastDuel!;var result=record.Result!;
        Check(record.Complete && view.PendingDuel is null,"immediate result retained");
        Equal(result.ChallengerTotal,8);Equal(result.ChallengedTotal,7);Equal(result.WinnerId,record.Challenger.Id);
        Check(view.Players[1].Discard.Any(c=>c.Id==record.Challenged.Id&&c.Zone==Zone.Dead),"loser destroyed");
        Check(result.Provinces.Where(p=>p.PlayerId==0).All(p=>p.After==p.Before+2),"Courtesy recorded");
        Equal(result.ReturnPriority,1);Equal(record.Reveals.Length,1);Equal(record.Reveals[0].Cards.Length,2);
        Equal(JsonSerializer.Serialize(e.State),JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).State));
        record.Reveals[0].Cards[0]=record.Challenger;
        Check(e.GetView(0).LastDuel!.Reveals[0].Cards.All(c=>c.Zone==Zone.Focused),"record detached");
    }
    public static void Story()
    {
        var e=MatchEngine.CreateDuelDemo();Begin(e);
        var challenge=e.GetView(0).LastDuel!.Challenge!;
        Equal(challenge.Source!.Definition!.Id,"oracle-l5r-7483");
        Check(!challenge.Refusable&&challenge.LossTerms.Contains("destroyed"),"challenge uses the real played source and Core stakes");
        Send(e,Action(e,"focus",c=>c.Choice=="deck"));
        Send(e,Action(e,"focus",c=>c.CardId is {} id&&e.State.Cards[id].DefinitionId=="oracle-l5r-7996"));
        Send(e,Action(e,"strike"));
        var record=e.GetView(0).LastDuel!;var beats=record.Beats!;
        Equal(beats.Length,2);Equal(beats[0].Kind,"Outcome");Equal(beats[1].Kind,"FocusEffect");
        Equal(beats[0].Source!.Definition!.Id,"oracle-l5r-7483");Equal(beats[1].Source!.Definition!.Id,"oracle-l5r-7996");
        Check(beats[0].AffectedCardIds.Contains(record.Challenged.Id)&&beats[0].Lines.Any(l=>l.Contains("destroyed")),"actual destroyed participant attributed to the challenge");
        Check(beats[1].Lines.Single().Contains("all provinces +2 strength"),"Courtesy delta attributed even without a legacy event");
        Equal(JsonSerializer.Serialize(e.State),JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).State));
        beats[0].Lines[0]="tampered";Check(e.GetView(0).LastDuel!.Beats![0].Lines[0]!="tampered","story arrays detached from engine state");
        var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(record))!.AsObject();node.Remove("Challenge");node.Remove("Beats");
        var legacy=JsonSerializer.Deserialize<DuelRecord>(node.ToJsonString())!;Check(legacy.Challenge is null&&legacy.Beats is null,"old presentation snapshots remain readable");
        foreach(var scenario in new[]{"duelist","aftermath"})
        {
            e=MatchEngine.CreateDuelDemo(scenario);Begin(e,scenario);
            if(scenario=="duelist")Equal(e.GetView(0).LastDuel!.Challenge!.Source!.Id,e.State.PendingDuel!.ChallengerId);
            Send(e,Action(e,"focus",c=>c.Choice=="deck"));
            Send(e,Action(e,"focus",c=>c.CardId is {} id&&e.State.Cards[id].DefinitionId=="oracle-l5r-7996"));
            Send(e,Action(e,"strike"));
            var privateDeck=e.State.Players.SelectMany(p=>p.FateDeck).ToArray();
            for(var i=0;i<20&&!e.GetView(0).LastDuel!.Complete;i++)
            {
                var actions=e.QueryActions(e.State.PriorityPlayerId).Where(a=>a.Enabled&&a.Command is not null).ToArray();
                var action=actions.FirstOrDefault(a=>a.Type=="useAbility")??actions.FirstOrDefault(a=>a.Command!.Choice=="draw")
                    ??actions.FirstOrDefault(a=>a.Type=="duelistRefocus"&&a.Command!.CardId is null)??actions.First();
                Send(e,action.Command!);
            }
            record=e.GetView(0).LastDuel!;Check(record.Complete,$"{scenario} completes");
            if(scenario=="duelist")Check(record.Beats!.Any(b=>b.Lines.Any(l=>l.Contains("moves home"))),"movement consequence captured");
            else
            {
                Check(record.Beats!.Any(b=>b.Kind=="Aftermath"&&b.Lines.Any(l=>l.Contains("draws 1 card"))),"aftermath draw count has a visible explanation");
                Check(record.Beats!.Any(b=>b.SourceLabel=="Overconfident"&&b.Lines.Any(l=>l.Contains("draws 1 card"))),"source-less Overconfident queue is identified by the actual keyword, not an invented card");
                var json=JsonSerializer.Serialize(record);
                foreach(var id in privateDeck)Check(!json.Contains(id),"reaction story must not disclose drawn or unseen deck identities");
            }
            Equal(JsonSerializer.Serialize(e.State),JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).State));
        }
    }
    public static void Decisions()
    {
        foreach(var s in DuelDemoScenarios.All)
        {
            var e=MatchEngine.CreateDuelDemo(s.Id);Begin(e,s.Id);
            Check(e.GetView(0).PendingDuel is not null,$"{s.Id} begins");
            foreach(var card in e.State.Cards.Values.Where(c=>(CardCatalog.Get(c.DefinitionId).Keywords??[]).Contains("Loyal")))
                Equal(e.State.Players[card.OwnerId].Clan,CardCatalog.Get(card.DefinitionId).Clan);
            Equal(JsonSerializer.Serialize(e.State),JsonSerializer.Serialize(MatchEngine.Replay(e.ExportReplay()).State));
        }
        var refused=MatchEngine.CreateDuelDemo("refusal");Begin(refused,"refusal");Send(refused,Action(refused,"declineDuel"));
        Check(refused.GetView(0).LastDuel is {Complete:true,Result.Kind:"Refused"},"refusal retained");
        var empty=MatchEngine.CreateDuelDemo("empty");Begin(empty,"empty");Equal(empty.QueryActions(1).Length,1);Send(empty,Action(empty,"strike"));
        var cap=MatchEngine.CreateDuelDemo("four");Begin(cap,"four");for(var i=0;i<8;i++)Send(cap,Action(cap,"focus",c=>c.Choice=="deck"));
        Equal(cap.QueryActions(1).Single().Type,"strike");Check(!cap.Execute(new("focus",1,cap.Version,Choice:"deck")).Accepted,"fifth rejected");
        var reaction=MatchEngine.CreateDuelDemo("reaction");Begin(reaction,"reaction");Send(reaction,Action(reaction,"useAbility"));
        for(var i=0;i<2;i++)Send(reaction,Action(reaction,"declineReaction"));Equal(reaction.State.PendingDuel!.Stage,"Focusing");
        var order=MatchEngine.CreateDuelDemo("effects");Begin(order,"effects");
        for(var i=0;i<2;i++)Send(order,Action(order,"focus",c=>c.CardId is {} id&&order.State.Cards[id].DefinitionId=="oracle-l5r-7996"));
        Send(order,Action(order,"strike"));var arrange=Action(order,"orderDuelFocusEffects");
        Check(!order.Execute(arrange with {CardIds=[arrange.CardIds![0],arrange.CardIds[0]]}).Accepted,"bad permutation rejected");
        Send(order,arrange with {CardIds=arrange.CardIds!.Reverse().ToArray()});Equal(order.GetView(0).LastDuel!.Result!.EffectOrder[0],arrange.CardIds[1]);
        Check(order.GetView(0).LastDuel!.Beats!.Where(b=>b.Kind=="FocusEffect").Select(b=>b.Source!.Id).SequenceEqual(arrange.CardIds.Reverse()),"explanations preserve the selected Focus Effect order");
        var discard=MatchEngine.CreateDuelDemo("discard");Begin(discard,"discard");Send(discard,Action(discard,"focus",c=>c.Choice=="deck"));
        var dc=Action(discard,"focus",c=>c.Choice=="discard");Send(discard,dc);Check(discard.GetView(1).Players[0].Focused!.Any(c=>c.Id==dc.CardId),"discard focus public");
        Send(discard,Action(discard,"strike"));while(discard.State.PendingDuel is not null)Send(discard,Action(discard,"duelistRefocus",c=>c.CardId is null));
        Check(discard.GetView(0).Players[0].RemovedFromGame.Any(c=>c.Id==dc.CardId),"discard focus removed");
    }
}
