namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Isolated real-engine review positions. Never injected into decks or online rooms.</summary>
    public static MatchEngine CreateDecisionShowcase(string kind)
    {
        if(kind is not ("yumaru" or "fukui" or "fukui-bowed")) throw new ArgumentException("Unknown decision showcase.",nameof(kind));
        var state=CreatePublishedTestMatch(42).State;
        state.Phase=Phase.Battle;state.ActivePlayerId=0;state.PriorityPlayerId=0;
        state.CurrentBattleProvince=0;state.UnresolvedBattlefields=[0];state.PendingChoice=null;
        CardInstance Put(string definition,int owner,Zone zone,string? parent=null)
        {
            var card=new CardInstance {Id="decision-review-"+state.Cards.Count,DefinitionId=definition,OwnerId=owner,Zone=zone,FaceUp=true,ProvinceIndex=0,AttachedTo=parent};
            state.Cards.Add(card.Id,card);return card;
        }
        var enemy=Put("oracle-l5r-3636",1,Zone.Battlefield);
        var source=Put(kind=="yumaru"?"oracle-l5r-5181":"oracle-l5r-4375",0,Zone.Battlefield);
        if(kind=="yumaru") Put("oracle-l5r-6481",0,Zone.Attached,source.Id);
        else Put("oracle-l5r-9785",0,Zone.Battlefield).Bowed=kind=="fukui-bowed";
        var engine=FromTestState(state);
        if(kind=="yumaru")
        {
            var action=engine.QueryActions(0).Single(a=>a.Enabled && a.CardId==source.Id && a.Command?.AbilityId=="yumaru-move-home");
            var result=engine.Execute(action.Command! with {TargetId=enemy.Id});
            if(!result.Accepted || engine.State.PendingChoice?.Type!="effectChoice") throw new InvalidOperationException("Yumaru showcase failed to open its engine choice.");
        }
        return engine;
    }
}
