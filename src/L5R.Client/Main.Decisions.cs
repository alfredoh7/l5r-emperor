using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private ActionOption? _decisionSelection;
    private long _decisionVersion = -1;
    private int _decisionViewer = -1;
    private bool _reviewingAlternativeCost;
    private bool _alternativeCostConfirmed;
    private bool GenericDecisionActive => View.WinnerId is null && !EventDecisionActive &&
        View.PendingChoice?.Type is "effectChoice" or "preservingHonor" or "rinShame" or "conqueror" or "declineStraighten" or "forcedDiscard";
    private bool DecisionCanSubmit => !_session.Busy && !PresentationBusy && (!_session.Online ||
        _session.Connected && !(_session.Snapshot?.PlayersConnected?.Any(p=>!p)??false));

    private ActionOption[] AlternativeCosts(ActionOption action) => action.AlternativeCost is not { } cost ? [] :
        Actions.Where(a=>a.CardId==action.CardId && a.AlternativeCost is { } other &&
            other.AbilityId==cost.AbilityId && other.PerformerId==cost.PerformerId).ToArray();

    private void ResetDecisionSelection()
    {
        _decisionSelection=null;
        _reviewingAlternativeCost=false;_alternativeCostConfirmed=false;
        _decisionVersion=View.Version; _decisionViewer=View.ViewerId;
    }

    private bool RenderDecisionChooser()
    {
        if(_decisionVersion!=View.Version || _decisionViewer!=View.ViewerId) ResetDecisionSelection();
        if(PresentationBusy || !GenericDecisionActive && !(_reviewingAlternativeCost && _chosen?.AlternativeCost is not null)) return false;
        var pending=GenericDecisionActive?View.PendingChoice:null;
        var own=pending is null || pending.PlayerId==View.ViewerId && View.PriorityPlayerId==View.ViewerId;
        var options=pending is null?AlternativeCosts(_chosen!):Actions.Where(a=>a.Type is not ("concede" or "wait") && a.Type!="orderSeppuku").ToArray();
        _guide.Visible=false;
        var heading=Palette.Text(pending is null?"HOW DO YOU PAY?":own?"YOUR CHOICE":"OPPONENT’S CHOICE",27,title:true);
        heading.HorizontalAlignment=HorizontalAlignment.Center;_sidebarHeader.AddChild(heading);
        _sidebarHeader.AddChild(new HSeparator());
        var sourceId=pending?.CardId??_chosen?.CardId;
        if(sourceId is not null && FindCard(sourceId) is {Definition:not null} source) AddDecisionSource(source);
        if(pending is not null)
            _inspector.AddChild(Palette.Wrapped(pending.Prompt,19,Palette.Paper));
        else if(_chosen?.Command is { } draft)
        {
            _inspector.AddChild(Palette.Wrapped("Choose one cost to pay.",22,Palette.Paper));
            if(draft.PerformerId is { } performer && FindCard(performer) is { } card)
                _inspector.AddChild(Palette.Wrapped("Performer: "+card.Definition?.Name,17,Palette.PaperDim));
            if(draft.TargetId is { } target && FindCard(target) is { } targetCard)
                _inspector.AddChild(Palette.Wrapped("Target: "+targetCard.Definition?.Name,17,Palette.PaperDim));
        }
        if(!own)
        {
            _inspector.AddChild(Palette.Wrapped("Waiting for "+View.Players.Single(p=>p.Id==pending!.PlayerId).Name+" to choose.",20,Palette.Crane));
            if(!_session.Online&&!_autoOpponent) _decisionActions.AddChild(SidebarSkin.Button("Pass device to other player",SwitchSeat));
            return true;
        }
        var rows=new List<(ActionOption Option,Button Button)>();
        var confirm=SidebarSkin.Button("Confirm choice",CommitDecision,true);
        foreach(var option in options)
        {
            // A cost option is usable only for the draft's engine-listed target. Never fabricate a disabled command.
            var matchesTarget=pending is not null || _chosen?.Command?.TargetId is not { } target || option.TargetIds?.Contains(target)==true;
            var enabled=option.Enabled && option.Command is not null && matchesTarget;
            Button? button=null;
            button=SidebarSkin.Button(option.AlternativeCost?.Label??option.Label,()=>
            {
                _decisionSelection=option;
                foreach(var row in rows)
                {
                    var selected=ReferenceEquals(row.Option,option);
                    row.Button.Text=(selected?"✓  ":"")+(row.Option.AlternativeCost?.Label??row.Option.Label);
                    row.Button.AddThemeStyleboxOverride("normal",SidebarSkin.Surface(true,tint:selected?new Color(1.3f,1.2f,.9f):Colors.White));
                }
                confirm.Disabled=!DecisionCanSubmit;Highlight();
            });
            button.Disabled=!enabled || !DecisionCanSubmit;
            button.CustomMinimumSize=new(0,74);
            rows.Add((option,button));_inspector.AddChild(button);
            if(!enabled) _inspector.AddChild(Palette.Wrapped(option.Reason??"Unavailable for the selected target.",15,Palette.PaperDim));
            if(ReferenceEquals(_decisionSelection,option)) button.Text="✓  "+button.Text;
        }
        if(_decisionSelection is not null && !options.Contains(_decisionSelection)) _decisionSelection=null;
        confirm.Disabled=_decisionSelection is null || !DecisionCanSubmit;
        _decisionActions.AddChild(confirm);
        if(pending is null) _decisionActions.AddChild(SidebarSkin.Button("Back to target",()=>{_reviewingAlternativeCost=false;_decisionSelection=null;RenderInspector();}));
        else if(_decisionSelection is not null) _decisionActions.AddChild(SidebarSkin.Button("Clear selection",()=>{_decisionSelection=null;RenderInspector();Highlight();}));
        _decisionActions.AddChild(Palette.Wrapped(DecisionCanSubmit?"Select one option, then confirm.":"Your choice is paused until the connection is ready.",14,Palette.PaperDim));
        return true;
    }

    private void AddDecisionSource(CardView source)
    {
        var name=Palette.Wrapped(source.Definition!.Name,22);name.AddThemeFontOverride("font",SidebarSkin.Bold);_inspector.AddChild(name);
        _inspector.AddChild(CardPreview.Face(source,240));
        var inspect=SidebarSkin.Button("Inspect source card",()=>
        {
            var (_,body)=Dialog(source.Definition.Name,720,720);
            body.AddChild(CardPreview.Face(source,510));
            body.AddChild(Palette.Wrapped(source.Definition.Text,17,Palette.Paper));
        });
        inspect.CustomMinimumSize=new(0,44);_inspector.AddChild(inspect);
    }

    private void CommitDecision()
    {
        if(!DecisionCanSubmit || _decisionVersion!=View.Version || _decisionViewer!=View.ViewerId ||
           _decisionSelection is not {Enabled:true,Command:{ } command} selected || !Actions.Contains(selected)) return;
        if(GenericDecisionActive)
        {
            if(View.PendingChoice!.PlayerId!=View.ViewerId || View.PriorityPlayerId!=View.ViewerId) return;
            Submit(command); // Pending answers are already complete, including any CardIds or Choice.
        }
        else if(_reviewingAlternativeCost && _chosen?.Command is { } draft)
        {
            if(draft.TargetId is { } target && selected.TargetIds?.Contains(target)!=true) return;
            var reviewed=selected with {Command=command with {TargetId=draft.TargetId}};
            // Gold still goes through the existing engine-quoted panel when the ability has both kinds of cost.
            if(QuoteFor(reviewed) is not null) { _reviewingAlternativeCost=false;Choose(reviewed);_alternativeCostConfirmed=true;RenderInspector();return; }
            Submit(reviewed.Command!);
        }
    }
}
