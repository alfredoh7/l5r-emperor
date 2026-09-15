using Godot;
using L5R.Core;
using L5R.AI;
using System.Security.Cryptography;

namespace L5R.Client;

/// <summary>Local demo lifecycle only. The duel screen can also consume the normal match session.</summary>
public partial class DuelDemoHost : Control
{
    public event Action? ReturnToMenu;
    public bool ReducedMotion {get;set;}
    internal MatchSession Session {get;}=new();
    internal DuelScreen Screen {get;private set;}=null!;
    private Control _overlay=null!;
    private Label _notice=null!;
    private string _scenario="steel";
    private ulong _seed=DuelDemoScenarios.DefaultSeed;
    private int _seat;
    private bool _manual,_setup=true,_handoff,_sending;
    private double _botClock;
    public override void _Ready()
    {
        Theme=Palette.MakeTheme();SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Ignore;
        Screen=new DuelScreen{ReducedMotion=ReducedMotion};AddChild(Screen);
        _overlay=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(_overlay);_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Session.Changed+=Refresh;Session.Notice+=Notice;
        Screen.CommandRequested+=command=>_ = Submit(command);
        Screen.PresentationFinished+=()=>Prepare();
        Resized+=BuildOverlay;Prepare();
    }
    private void Notice(string message){if(_notice is not null)_notice.Text=message;}
    private void Refresh(){if(Session.Snapshot is {} snapshot)Screen.Present(snapshot);}
    private async Task Submit(GameCommand command)
    {
        if(_sending||_handoff||_setup)return;
        _sending=true;try{await Session.Submit(command);}finally{_sending=false;Screen.SubmissionFinished();}
    }
    private void Prepare()
    {
        _setup=true;_handoff=false;_botClock=0;Screen.ResetPresentation();
        Session.StartDuelDemo(_scenario,_seed,_seat);Screen.InputLocked=true;BuildOverlay();
    }
    private void BuildOverlay()
    {
        if(_overlay is null)return;
        foreach(var c in _overlay.GetChildren()){_overlay.RemoveChild(c);c.QueueFree();}
        var size=GetViewportRect().Size;
        _notice=Palette.Text("",16,Palette.Paper);_notice.Position=new Vector2(24,size.Y-30);_overlay.AddChild(_notice);
        if(_handoff)
        {
            // Opaque screen covers ALL cards and clears inspection before changing the permitted view.
            var curtain=new ColorRect{Color=new Color("101017"),Size=size};_overlay.AddChild(curtain);
            var box=Box(curtain,460);box.AddChild(Palette.Text("Pass the seat",32,Palette.Gold,true));
            box.AddChild(Palette.Wrapped($"Private cards are hidden. Ready for {Session.Snapshot!.View.Players[_seat].Name}?",22));
            AddButton(box,"Reveal this seat",()=>{_handoff=false;Screen.Visible=true;Screen.InputLocked=false;BuildOverlay();});
            return;
        }
        if(_setup)
        {
            Screen.InputLocked=true;
            var shade=new ColorRect{Color=new Color(0,0,0,.5f),Size=size};_overlay.AddChild(shade);
            var box=Box(shade,580);
            box.AddChild(Palette.Text("Duel Demo",38,Palette.Gold,true));
            var picker=new OptionButton{CustomMinimumSize=new Vector2(0,42)};
            foreach(var scenario in DuelDemoScenarios.All)picker.AddItem(scenario.Name+(scenario.TrainingFixture?" · training fixture":""));
            picker.Selected=DuelDemoScenarios.All.ToList().FindIndex(s=>s.Id==_scenario);box.AddChild(picker);
            picker.ItemSelected+=index=>{_scenario=DuelDemoScenarios.All[(int)index].Id;Prepare();};
            box.AddChild(Palette.Wrapped(DuelDemoScenarios.Get(_scenario).Description,19));
            var seat=new OptionButton{CustomMinimumSize=new Vector2(0,38)};seat.AddItem($"Play {Session.Snapshot!.View.Players[0].Name}");seat.AddItem($"Play {Session.Snapshot.View.Players[1].Name}");seat.Selected=_seat;box.AddChild(seat);
            seat.ItemSelected+=value=>{_seat=(int)value;Prepare();};
            var mode=new CheckBox{Text="Manual · control both seats",ButtonPressed=_manual};box.AddChild(mode);mode.Toggled+=value=>_manual=value;
            box.AddChild(Palette.Wrapped($"Seed {_seed} · restart repeats this deal\nCards with ‘Training’ in their name are synthetic fixtures. Prepared positions do not change playable decks.",16,Palette.PaperDim));
            AddButton(box,"Begin Duel",()=>_ = Begin());
            AddButton(box,"New deal",()=>{NewSeed();Prepare();});
            AddButton(box,"Return to menu",()=>ReturnToMenu?.Invoke());
            return;
        }
        Screen.InputLocked=false;
        var bar=new HBoxContainer{Position=new Vector2(20,18)};bar.AddThemeConstantOverride("separation",10);_overlay.AddChild(bar);
        AddButton(bar,"Menu",()=>ReturnToMenu?.Invoke());
        AddButton(bar,"Restart",Prepare);
        AddButton(bar,"New deal",()=>{NewSeed();Prepare();});
        AddButton(bar,_manual?"Manual":"Solo",()=>{_manual=!_manual;_botClock=0;BuildOverlay();});
        AddButton(bar,"Switch seat",SwitchSeat);
        AddButton(bar,"Save replay",SaveReplay);
        var seed=Palette.Text($"{DuelDemoScenarios.Get(_scenario).Name} · seed {_seed}",15,Palette.Paper);seed.Position=new Vector2(24,65);_overlay.AddChild(seed);
    }
    private VBoxContainer Box(Control parent,float width)
    {
        var size=GetViewportRect().Size;var panel=new PanelContainer{Position=new Vector2((size.X-width)/2,Math.Max(85,(size.Y-580)/2)),Size=new Vector2(width,0)};
        panel.AddThemeStyleboxOverride("panel",Palette.Box(new Color("15131b"),Palette.Gold,22));parent.AddChild(panel);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",12);panel.AddChild(box);return box;
    }
    private static Button AddButton(Control parent,string text,Action action)
    {
        var button=new Button{Text=text,CustomMinimumSize=new Vector2(0,36)};parent.AddChild(button);button.Pressed+=action;return button;
    }
    private async Task Begin()
    {
        if(_sending||!_setup)return;
        _sending=true;
        try{await Session.BeginDuelDemo();if(Session.Snapshot?.View.LastDuel is not null){_setup=false;BuildOverlay();}}
        finally{_sending=false;}
    }
    private void SwitchSeat()
    {
        if(_sending||Screen.PresentationBusy)return;
        _manual=true;_handoff=true;Screen.ClearReading();Screen.Visible=false;Screen.InputLocked=true;
        Session.SwitchPracticeSeat();_seat=Session.Snapshot!.View.ViewerId;_botClock=0;BuildOverlay();
    }
    private void NewSeed()=>_seed=BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
    private void SaveReplay()
    {
        var json=Session.ExportPracticeReplay();if(json is null)return;
        var dir=ProjectSettings.GlobalizePath("user://duel-replays");System.IO.Directory.CreateDirectory(dir);
        var path=System.IO.Path.Combine(dir,$"{_scenario}-{_seed}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");System.IO.File.WriteAllText(path,json);Notice($"Replay saved: {path}");
    }
    public override void _Process(double delta)
    {
        if(_setup||_manual||_handoff||_sending||Screen.PresentationBusy||Session.Snapshot?.View.LastDuel is not {Complete:false})return;
        _botClock+=delta;if(_botClock<(ReducedMotion?.15:.8))return;_botClock=0;
        Session.StepPracticeOpponent(DuelOpponent.Choose);
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape}){ReturnToMenu?.Invoke();GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree(){Session.Changed-=Refresh;Session.Notice-=Notice;Session.Dispose();}
}
