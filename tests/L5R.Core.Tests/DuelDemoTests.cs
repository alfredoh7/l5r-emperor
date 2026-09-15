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
    public static void Decisions()
    {
        foreach(var s in DuelDemoScenarios.All)
        {
            var e=MatchEngine.CreateDuelDemo(s.Id);Begin(e,s.Id);
            Check(e.GetView(0).PendingDuel is not null,$"{s.Id} begins");
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
        var discard=MatchEngine.CreateDuelDemo("discard");Begin(discard,"discard");Send(discard,Action(discard,"focus",c=>c.Choice=="deck"));
        var dc=Action(discard,"focus",c=>c.Choice=="discard");Send(discard,dc);Check(discard.GetView(1).Players[0].Focused!.Any(c=>c.Id==dc.CardId),"discard focus public");
        Send(discard,Action(discard,"strike"));while(discard.State.PendingDuel is not null)Send(discard,Action(discard,"duelistRefocus",c=>c.CardId is null));
        Check(discard.GetView(0).Players[0].RemovedFromGame.Any(c=>c.Id==dc.CardId),"discard focus removed");
    }
}
