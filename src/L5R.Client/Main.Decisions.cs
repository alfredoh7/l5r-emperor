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
        if(PresentationBusy || !GenericDecisionActive && _chosen?.AlternativeCost is null) return false;
        var pending=GenericDecisionActive?View.PendingChoice:null;
        var own=pending is null || pending.PlayerId==View.ViewerId && View.PriorityPlayerId==View.ViewerId;
        var options=pending is null?AlternativeCosts(_chosen!):Actions.Where(a=>a.Type is not ("concede" or "wait") && a.Type!="orderSeppuku").ToArray();
        _guide.Visible=false;
        var heading=Palette.Text(pending is null?(_reviewingAlternativeCost||_alternativeCostConfirmed?"HOW DO YOU PAY?":"CHOOSE A TARGET"):own?"YOUR CHOICE":"OPPONENT’S CHOICE",27,title:true);
        heading.HorizontalAlignment=HorizontalAlignment.Center;_sidebarHeader.AddChild(heading);
        _sidebarHeader.AddChild(new HSeparator());
        var sourceId=pending?.CardId??_chosen?.CardId;
        var source=sourceId is null?null:FindCard(sourceId);
        var prompt=pending?.Prompt;
        if(prompt?.IndexOf(" — ",StringComparison.Ordinal) is int split && split>=0) prompt=prompt[..split]+".";
        if(source is {Definition:not null}) AddDecisionSource(source,prompt);
        else if(prompt is not null) _inspector.AddChild(Palette.Wrapped(prompt,19,Palette.Paper));
        if(pending is null && _chosen?.Command is { } draft)
        {
            _inspector.AddChild(Palette.Wrapped(_reviewingAlternativeCost?"Choose one cost to pay.":"Review the performer and target.",22,Palette.Paper));
            if(draft.PerformerId is { } performer && FindCard(performer) is { } card)
                _inspector.AddChild(Palette.Wrapped("Performer: "+card.Definition?.Name,17,Palette.PaperDim));
            if(draft.TargetId is { } target && FindCard(target) is { } targetCard)
                _inspector.AddChild(Palette.Wrapped("Target: "+targetCard.Definition?.Name,17,Palette.PaperDim));
        }
        if(pending is null && !_reviewingAlternativeCost && _chosen is {Command:{ } draftCommand} draftAction)
        {
            if(_alternativeCostConfirmed)
            {
                var (required,ready)=RenderGoldPanel(draftAction);
                AddConfirm("Confirm action",required,ready,()=>draftCommand with {SourceIds=required>0?_sources.ToArray():[]},"Cancel action");return true;
            }
            var performers=Actions.Where(a=>a.Enabled&&a.CardId==draftAction.CardId&&a.AlternativeCost?.AbilityId==draftAction.AlternativeCost!.AbilityId)
                .Select(a=>a.AlternativeCost!.PerformerId).Distinct().ToArray();
            if(performers.Length>1)
            {
                var performerPicker=new OptionButton {FitToLongestItem=false};
                foreach(var id in performers)performerPicker.AddItem(id is null?"No performer":FindCard(id)?.Definition?.Name??"Performer");
                performerPicker.Selected=Array.IndexOf(performers,draftCommand.PerformerId);
                performerPicker.ItemSelected+=i=>Choose(Actions.First(a=>a.Enabled&&a.CardId==draftAction.CardId&&a.AlternativeCost?.AbilityId==draftAction.AlternativeCost!.AbilityId&&a.AlternativeCost?.PerformerId==performers[(int)i]));
                _inspector.AddChild(performerPicker);
            }
            var targets=options.SelectMany(a=>a.TargetIds??[]).Distinct().ToArray();
            if(targets.Length>0)
            {
                _inspector.AddChild(Palette.Wrapped("Choose a highlighted card on the table, or select it below.",17,Palette.PaperDim));
                var picker=new OptionButton {FitToLongestItem=false};
                foreach(var id in targets)picker.AddItem(FindCard(id)?.Definition?.Name??"Target");
                picker.Selected=Array.IndexOf(targets,draftCommand.TargetId);picker.ItemSelected+=i=>ChooseTarget(targets[(int)i]);_inspector.AddChild(picker);
            }
            var next=SidebarSkin.Button("Review cost choices",()=>{_reviewingAlternativeCost=true;_decisionSelection=null;RenderInspector();},true);
            next.Disabled=!DecisionCanSubmit;_decisionActions.AddChild(next);
            _decisionActions.AddChild(SidebarSkin.Button("Cancel action",CancelChoice));return true;
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

    private void AddDecisionSource(CardView source,string? prompt)
    {
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",14);_inspector.AddChild(row);
        row.AddChild(CardPreview.Face(source,240,168));
        var details=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};details.AddThemeConstantOverride("separation",12);row.AddChild(details);
        var name=Palette.Wrapped(source.Definition!.Name,22);name.AddThemeFontOverride("font",SidebarSkin.Bold);details.AddChild(name);
        if(prompt is not null)
        {
            var shortPrompt=prompt.StartsWith(source.Definition.Name+": ",StringComparison.Ordinal)?prompt[(source.Definition.Name.Length+2)..]:prompt;
            var text=Palette.Wrapped(shortPrompt,19,Palette.Paper);text.TooltipText=View.PendingChoice?.Prompt??prompt;details.AddChild(text);
        }
        var inspect=SidebarSkin.Button("Inspect source",()=>
        {
            var (_,body)=Dialog(source.Definition.Name,720,720);
            body.AddChild(CardPreview.Face(source,510));
            body.AddChild(Palette.Wrapped(source.Definition.Text,17,Palette.Paper));
        });
        inspect.CustomMinimumSize=new(0,44);details.AddChild(inspect);
        _inspector.AddChild(new HSeparator());
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
