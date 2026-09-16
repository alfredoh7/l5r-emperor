using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    public bool GuidanceEnabled {get;private set;}=true;
    public Func<bool,bool>? SaveGuidancePreference {get;set;}
    public event Action<bool>? GuidanceHelpChanged;
    private Control? _guidanceLayer;
    private Rect2 _guidanceRect;
    private readonly HashSet<int> _coachingDismissed=[];
    private bool _helpOpen,_dontShowAgain;
    private int _helpStep;
    private string? _helpContext;
    internal bool CoachingVisible {get;private set;}
    internal bool AnchoredHelpVisible=>_helpOpen&&_guidanceLayer?.Visible==true;
    internal string GuidanceTitle {get;private set;}="";
    internal string GuidanceBody {get;private set;}="";
    internal Rect2 GuidanceRect=>_guidanceRect;
    private sealed record GuidanceStep(string Title,string Body,Vector2 Target);

    public bool SetGuidanceEnabled(bool enabled,bool persist=false)
    {
        if(persist&&SaveGuidancePreference?.Invoke(enabled)==false)return false;
        GuidanceEnabled=enabled;
        if(enabled)_coachingDismissed.Clear();
        _dontShowAgain=false;RenderGuidance();return true;
    }
    public void OpenGuidance()
    {
        _helpOpen=true;_helpStep=0;_helpContext=null;_dontShowAgain=false;
        GuidanceHelpChanged?.Invoke(true);RenderGuidance();
    }
    public void CloseGuidance()
    {
        _helpOpen=false;_dontShowAgain=false;GuidanceHelpChanged?.Invoke(false);RenderGuidance();
    }
    private void ResetGuidance()
    {
        _coachingDismissed.Clear();CloseGuidance();
    }
    private void DismissGuidance(bool help)
    {
        if(_dontShowAgain&&!SetGuidanceEnabled(false,true))return;
        if(Snapshot is not null)_coachingDismissed.Add(Snapshot.View.ViewerId);
        if(help)CloseGuidance();else {_dontShowAgain=false;RenderGuidance();}
    }
    private List<GuidanceStep> GuidanceSteps()
    {
        var steps=new List<GuidanceStep>();
        if(Snapshot is null)return steps;
        var view=Snapshot.View;var actions=Legal.ToArray();
        if(view.PendingDuel?.Stage=="Focusing")
        {
            var hand=actions.FirstOrDefault(a=>a.Type=="focus"&&a.Command!.Choice is null&&a.Command.CardId is not null);
            if(hand?.Command?.CardId is {} id&&_tiles.ContainsKey(id))steps.Add(new("Choose your focus","Select a hand card, then place it in the open focus position. You can also drag it there.",CardClickPoint(id)));
            if(actions.Any(a=>a.Type=="focus"&&a.Command!.Choice=="deck"))steps.Add(new("Focus unseen","Select your deck, then place its top card without looking.",PileClickPoint(view.ViewerId,Zone.FateDeck)));
            if(actions.Any(a=>a.Type=="focus"&&a.Command!.Choice=="discard"))steps.Add(new("From the discard","Open ‘Focus from discard’ to choose one of the public cards offered for this duel.",_controlsRect.GetCenter()));
            if(actions.Any(a=>a.Type=="strike"))steps.Add(new("Make your stand","Choose Strike to end focusing and reveal the committed cards.",_controlsRect.Position+new Vector2(_controlsRect.Size.X/2,42*_scale)));
        }
        if(steps.Count==0)
        {
            var body=CanContinue?"The result is held here. Review the revealed cards or consequences, then Continue when you are ready.":Status();
            if(view.PriorityPlayerId!=view.ViewerId&&!CanContinue)body="Your opponent is deciding. You can inspect either public discard pile while you wait.";
            else if(view.PendingDuel?.Stage=="OrderingEffects")body="Open ‘Arrange Focus Effects’ and move cards earlier until the order is right. Resolve when ready.";
            else if(view.PendingDuel?.Stage=="DuelistRefocus")body="Select one of your focused cards and a hand replacement, or choose a deck replacement. You may also keep your focus.";
            if(string.IsNullOrEmpty(body))body="Select a personality or a public discard to inspect its full card face. Available decisions appear beside your hand.";
            steps.Add(new(CanContinue?"The outcome":"This moment",body,_controlsRect.GetCenter()));
        }
        return steps;
    }
    private void RenderGuidance()
    {
        if(_hud is null)return;
        if(_guidanceLayer is not null){RemoveChild(_guidanceLayer);_guidanceLayer.QueueFree();}
        _guidanceLayer=new Control{MouseFilter=MouseFilterEnum.Ignore,Size=GetViewportRect().Size};AddChild(_guidanceLayer);
        if(GodotObject.IsInstanceValid(_status))_status.Visible=true;
        CoachingVisible=false;_guidanceRect=new Rect2();GuidanceTitle="";GuidanceBody="";
        if(Snapshot is null||InputLocked||PresentationBusy||_reading is not null){_guidanceLayer.Hide();return;}
        var view=Snapshot.View;
        var automatic=GuidanceEnabled&&!_coachingDismissed.Contains(view.ViewerId)&&view.PendingDuel?.Stage=="Focusing"&&view.PriorityPlayerId==view.ViewerId;
        if(!_helpOpen&&!automatic){_guidanceLayer.Hide();return;}
        // Rebuild against the current permitted actions after a command, seat change or resize.
        var context=$"{view.ViewerId}:{view.Version}:{view.PendingDuel?.Stage}";
        if(_helpContext!=context){_helpStep=0;_helpContext=context;_dontShowAgain=false;}
        var steps=GuidanceSteps();_helpStep=Math.Clamp(_helpStep,0,steps.Count-1);
        var current=steps[_helpStep];
        var pos=_helpOpen?new Vector2(112,61):new Vector2(520,780);
        var extent=_helpOpen?new Vector2(495,220):new Vector2(612,112);
        _guidanceRect=new Rect2(_offset+pos*_scale,extent*_scale);
        var panel=new PanelContainer{Name=_helpOpen?"AnchoredGuidance":"QuietCoaching",Position=_guidanceRect.Position,Size=_guidanceRect.Size,MouseFilter=MouseFilterEnum.Stop};
        var style=DuelUi.Plaque(_scale);style.ContentMarginLeft=style.ContentMarginRight=(_helpOpen?35:18)*_scale;
        style.ContentMarginTop=style.ContentMarginBottom=(_helpOpen?18:6)*_scale;
        panel.AddThemeStyleboxOverride("panel",style);_guidanceLayer.AddChild(panel);
        panel.Resized+=()=>_guidanceRect=panel.GetGlobalRect();
        var column=new VBoxContainer();column.AddThemeConstantOverride("separation",Math.Max(0,(int)(2*_scale)));panel.AddChild(column);
        if(_helpOpen)
        {
            var top=new HBoxContainer();column.AddChild(top);
            var number=DuelUi.Text($"{_helpStep+1} of {steps.Count}",Math.Max(13,(int)(15*_scale)),Palette.PaperDim);number.SizeFlagsHorizontal=SizeFlags.ExpandFill;top.AddChild(number);
            var close=DuelUi.Button("Close",()=>DismissGuidance(true),scale:_scale,fontSize:16);close.Name="Close duel guidance";top.AddChild(close);
            GuideText(current.Title,current.Body,column,false);
        }
        else
        {
            var choices=new List<string>();
            if(Legal.Any(a=>a.Type=="focus"&&a.Command!.Choice is null))choices.Add("choose a hand card");
            if(Legal.Any(a=>a.Type=="focus"&&a.Command!.Choice=="deck"))choices.Add("focus unseen from your deck");
            if(Legal.Any(a=>a.Type=="focus"&&a.Command!.Choice=="discard"))choices.Add("choose an eligible discard");
            if(Legal.Any(a=>a.Type=="strike"))choices.Add("Strike");
            var body=choices.Count switch {0=>"Your available decisions appear beside your hand.",1=>choices[0]+".",_=>string.Join(", ",choices.Take(choices.Count-1))+" or "+choices[^1]+"."};
            body=char.ToUpperInvariant(body[0])+body[1..];GuideText("Your move",body,column,true);CoachingVisible=true;_status.Visible=false;
        }
        var footer=new HBoxContainer{Alignment=_helpOpen?BoxContainer.AlignmentMode.Begin:BoxContainer.AlignmentMode.Center};footer.AddThemeConstantOverride("separation",(int)(14*_scale));column.AddChild(footer);
        var check=new CheckBox{Name="HideDuelGuidance",Text="Do not show again",ButtonPressed=_dontShowAgain,FocusMode=FocusModeEnum.All};
        check.AddThemeFontSizeOverride("font_size",Math.Max(13,(int)(15*_scale)));check.AddThemeColorOverride("font_color",Palette.Paper);
        check.Toggled+=value=>_dontShowAgain=value;footer.AddChild(check);
        if(_helpOpen)
        {
            var space=new Control{SizeFlagsHorizontal=SizeFlags.ExpandFill};footer.AddChild(space);
            if(_helpStep>0)footer.AddChild(DuelUi.Button("Back",()=>{_helpStep--;RenderGuidance();},scale:_scale,fontSize:16));
            var next=DuelUi.Button(_helpStep==steps.Count-1?"Done":"Next",()=>{if(_helpStep==steps.Count-1)DismissGuidance(true);else{_helpStep++;RenderGuidance();}},true,_scale,26);next.Name="Next duel guidance";footer.AddChild(next);
            var leader=new DuelGuidanceLeader{From=_guidanceRect.Position+new Vector2(_guidanceRect.Size.X*.22f,_guidanceRect.Size.Y),To=current.Target,MouseFilter=MouseFilterEnum.Ignore,Size=_guidanceLayer.Size};
            _guidanceLayer.AddChild(leader);_guidanceLayer.MoveChild(leader,0);
        }
        else footer.AddChild(DuelUi.Button("Got it",()=>DismissGuidance(false),true,_scale,23));
    }
    private void GuideText(string title,string body,VBoxContainer column,bool centered)
    {
        GuidanceTitle=title;GuidanceBody=body;
        var heading=DuelUi.Text(title,Math.Max(22,(int)(43*_scale)),Palette.Paper,true);
        var text=DuelUi.Wrapped(body,Math.Max(14,(int)((centered?16:18)*_scale)),Palette.Paper);
        if(centered){heading.HorizontalAlignment=HorizontalAlignment.Center;text.HorizontalAlignment=HorizontalAlignment.Center;}
        column.AddChild(heading);column.AddChild(text);
    }
}

internal partial class DuelGuidanceLeader : Control
{
    public Vector2 From,To;
    public override void _Draw()
    {
        var delta=To-From;var direction=delta.Normalized();var end=To-direction*20;
        DrawLine(From,end,new Color(.91f,.85f,.7f,.7f),1.5f,true);
        DrawArc(To,15,0,Mathf.Tau,40,new Color(.97f,.91f,.77f,.8f),1.5f,true);
    }
}
