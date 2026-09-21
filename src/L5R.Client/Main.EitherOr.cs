using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private string? _eitherOrKey,_eitherNoticeMatch,_eitherNotice;
    private long _eitherNoticeSequence=-1,_eitherNoticeVersion=-1;
    private bool EitherOrActive=>View.WinnerId is null&&View.PendingChoice?.Type=="eitherOr"&&View.PendingEitherOr is not null;

    private void RenderEitherOrNotice()
    {
        var sequence=View.Events.LastOrDefault()?.Sequence??0;
        if(_eitherNoticeMatch!=_session.MatchKey)
        {_eitherNoticeMatch=_session.MatchKey;_eitherNoticeSequence=sequence;_eitherNotice=null;}
        if(_eitherNoticeVersion!=View.Version)_eitherNotice=null;
        var automatic=View.Events.LastOrDefault(e=>e.Sequence>_eitherNoticeSequence&&e.Type is "eitherOrAutoResolved" or "opponentPaymentAutoResolved");
        if(automatic is not null)_eitherNotice=automatic.Message;
        _eitherNoticeSequence=sequence;_eitherNoticeVersion=View.Version;
        if(_eitherNotice is null)return;
        var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",SidebarSkin.Inset());_sidebarHeader.AddChild(panel);
        var text=new VBoxContainer();panel.AddChild(text);text.AddChild(Palette.Text("RESOLVED AUTOMATICALLY",18,Palette.Gold));
        text.AddChild(Palette.Wrapped(_eitherNotice,18));
    }

    private bool RenderEitherOr()
    {
        var key=EitherOrActive?$"{_session.MatchKey}/{View.ViewerId}/{View.Version}/{View.PendingEitherOr!.ChoiceId}/{DecisionCanSubmit}":null;
        if(key!=_eitherOrKey)
        {
            if(_chosen?.Type=="chooseEitherOr"){_chosen=null;_sources.Clear();}
            _eitherOrKey=key;
        }
        if(!EitherOrActive||PresentationBusy)return false;
        var choice=View.PendingEitherOr!;
        var own=choice.ViewerDecides&&choice.DeciderPlayerId==View.ViewerId&&View.PriorityPlayerId==View.ViewerId;
        var options=Actions.Where(a=>a.Type=="chooseEitherOr").ToArray();
        var selected=own&&_chosen is {Type:"chooseEitherOr"} chosen&&options.Contains(chosen)?chosen:null;
        var paying=selected is not null&&QuoteFor(selected) is not null;
        _guide.Visible=false;
        var heading=Palette.Text(own?"YOUR CHOICE":"OPPONENT’S CHOICE",27,title:true);heading.HorizontalAlignment=HorizontalAlignment.Center;_sidebarHeader.AddChild(heading);
        var actor=View.Players.Single(p=>p.Id==choice.ActingPlayerId).Name;
        var decider=View.Players.Single(p=>p.Id==choice.DeciderPlayerId).Name;
        _sidebarHeader.AddChild(Palette.Wrapped($"Action: {actor} · Decision: {decider}",16,Palette.PaperDim));
        _sidebarHeader.AddChild(new HSeparator());
        var source=FindCard(choice.SourceCardId);
        // SourceDefinitionId is explicitly public even when the source has left the visible zones.
        if(source?.Definition is null&&CardCatalog.Cards.TryGetValue(choice.SourceDefinitionId,out var definition))
            source=DeckSearchPanel.Printed(definition,choice.ActingPlayerId);
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",14);_inspector.AddChild(row);
        if(source?.Definition is not null)
        {
            var face=new DecisionSourceFace(source,paying?112:184);row.AddChild(face);
            face.Pressed+=()=>{var (_,body)=Dialog(choice.SourceName,720,720);body.AddChild(CardPreview.Face(source,510));body.AddChild(Palette.Wrapped(source.Definition.Text,17));};
        }
        var context=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};context.AddThemeConstantOverride("separation",8);row.AddChild(context);
        var name=Palette.Wrapped(choice.SourceName,21);name.AddThemeFontOverride("font",SidebarSkin.Bold);context.AddChild(name);
        var prompt=choice.Prompt.StartsWith(choice.SourceName+": ",StringComparison.Ordinal)?choice.Prompt[(choice.SourceName.Length+2)..]:choice.Prompt;
        if(!paying)context.AddChild(Palette.Wrapped(prompt,18));
        if(choice.AffectedName is {} affected)context.AddChild(Palette.Wrapped("Affected: "+affected,16,Palette.Gold));
        context.AddChild(Palette.Wrapped("Hover or click the card to inspect",13,Palette.PaperDim));
        if(!own)_inspector.AddChild(Palette.Wrapped($"Waiting for {decider}. These options address that player.",17,Palette.Crane));
        _inspector.AddChild(new HSeparator());
        foreach(var option in choice.Options)
        {
            if(paying&&selected!.Command?.Choice!=option.Key)continue;
            var action=options.SingleOrDefault(a=>a.Command?.Choice==option.Key);
            var button=new EitherOrOptionButton(option,selected?.Command?.Choice==option.Key,
                own&&DecisionCanSubmit&&option.Legal&&action is {Enabled:true,Command:not null});
            button.Pressed+=()=>
            {
                if(!DecisionCanSubmit||_eitherOrKey!=key||action?.Command is null||!Actions.Contains(action))return;
                Choose(action);
                _paymentConfirm?.CallDeferred(Control.MethodName.GrabFocus);
            };
            _inspector.AddChild(button);
        }
        if(!own)
        {
            if(!_session.Online&&!_autoOpponent)_decisionActions.AddChild(SidebarSkin.Button("Pass device to other player",SwitchSeat));
            return true;
        }
        var (required,ready)=selected is null?(0,false):RenderGoldPanel(selected);
        _paymentConfirm=SidebarSkin.Button(selected is null?"Choose an option":required>0?$"Confirm · Pay {required} Gold":"Confirm choice",()=>
        {
            if(!DecisionCanSubmit||_eitherOrKey!=key||!choice.ViewerDecides||selected is not {Enabled:true,Command:{ } command}||!Actions.Contains(selected)||!ready)return;
            Submit(command with {SourceIds=required>0?_sources.ToArray():command.SourceIds});
        },true);
        _paymentConfirm.Disabled=selected is null||!ready||!DecisionCanSubmit;_decisionActions.AddChild(_paymentConfirm);
        if(selected is not null)_decisionActions.AddChild(SidebarSkin.Button("Back to options",()=>{_chosen=null;_sources.Clear();RenderInspector();RenderPhaseActions();Highlight();}));
        _decisionActions.AddChild(Palette.Wrapped(DecisionCanSubmit?"Nothing happens until you confirm.":"Your choice is paused until the connection is ready.",14,Palette.PaperDim));
        return true;
    }
}
