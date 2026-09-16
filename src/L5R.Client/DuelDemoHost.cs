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
    public MenuPreferences Preferences {get;set;}=new("default");
    internal MatchSession Session {get;}=new();
    internal DuelScreen Screen {get;private set;}=null!;
    private Control _overlay=null!;
    private Label _notice=null!;
    private string _scenario="steel";
    private ulong _seed=DuelDemoScenarios.DefaultSeed;
    private int _seat;
    private bool _manual,_setup=true,_handoff,_sending,_menuOpen,_readingPause,_helpPause;
    private double _botClock;
    public override void _Ready()
    {
        Theme=DuelUi.Theme();SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Ignore;
        Screen=new DuelScreen{ReducedMotion=ReducedMotion};AddChild(Screen);
        Screen.SetGuidanceEnabled(Preferences.DuelGuidance);
        Screen.SaveGuidancePreference=SaveGuidance;
        Screen.GuidanceHelpChanged+=open=>_helpPause=open;
        _overlay=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(_overlay);_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Session.Changed+=Refresh;Session.Notice+=Notice;
        Screen.CommandRequested+=command=>_ = Submit(command);
        Screen.PresentationFinished+=()=>Prepare();
        Screen.ReadingChanged+=reading=>{_readingPause=reading;_overlay.Visible=!reading;};
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
        _setup=true;_handoff=false;_menuOpen=false;_botClock=0;Screen.ResetPresentation();
        Session.StartDuelDemo(_scenario,_seed,_seat);Screen.InputLocked=true;BuildOverlay();
    }
    private void BuildOverlay()
    {
        if(_overlay is null)return;
        foreach(var c in _overlay.GetChildren()){_overlay.RemoveChild(c);c.QueueFree();}
        var size=GetViewportRect().Size;
        _notice=DuelUi.Text("",16,Palette.Paper);_notice.Position=new Vector2(24,size.Y-30);_overlay.AddChild(_notice);
        if(_handoff)
        {
            // Opaque screen covers ALL cards and clears inspection before changing the permitted view.
            var curtain=new ColorRect{Color=new Color("101017"),Size=size};_overlay.AddChild(curtain);
            var box=DuelUi.Modal(curtain,size,new Vector2(560,245),"Pass the seat");
            box.AddChild(DuelUi.Wrapped($"Private cards are hidden. Ready for {Session.Snapshot!.View.Players[_seat].Name}?",22));
            AddButton(box,"Reveal this seat",()=>{_handoff=false;Screen.Visible=true;Screen.InputLocked=false;BuildOverlay();},true);
            return;
        }
        if(_setup)
        {
            Screen.InputLocked=true;
            var shade=new ColorRect{Color=new Color(0,0,0,.5f),Size=size};_overlay.AddChild(shade);
            var box=DuelUi.Modal(shade,size,new Vector2(640,640),"Duel Demo");
            box.AddChild(DuelUi.Text("Prepared encounter",16,Palette.PaperDim));
            var picker=new OptionButton{CustomMinimumSize=new Vector2(0,42)};
            foreach(var scenario in DuelDemoScenarios.All)picker.AddItem(scenario.Name+(scenario.TrainingFixture?" · training fixture":""));
            picker.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
            picker.Selected=DuelDemoScenarios.All.ToList().FindIndex(s=>s.Id==_scenario);box.AddChild(picker);
            picker.ItemSelected+=index=>{_scenario=DuelDemoScenarios.All[(int)index].Id;Prepare();};
            box.AddChild(DuelUi.Wrapped(DuelDemoScenarios.Get(_scenario).Description,19));
            var seat=new OptionButton{CustomMinimumSize=new Vector2(0,38)};seat.AddItem($"Play {Session.Snapshot!.View.Players[0].Name}");seat.AddItem($"Play {Session.Snapshot.View.Players[1].Name}");seat.Selected=_seat;box.AddChild(seat);
            seat.ItemSelected+=value=>{_seat=(int)value;Prepare();};
            var mode=new CheckBox{Text="Manual · control both seats",ButtonPressed=_manual};box.AddChild(mode);mode.Toggled+=value=>_manual=value;
            box.AddChild(DuelUi.Wrapped($"Seed {_seed} · restart repeats this deal\nCards with ‘Training’ in their name are synthetic fixtures. Prepared positions do not change playable decks.",16,Palette.PaperDim));
            AddButton(box,"Begin Duel",()=>_ = Begin(),true);
            AddButton(box,"New deal",()=>{NewSeed();Prepare();});
            AddButton(box,"Return to menu",()=>ReturnToMenu?.Invoke());
            return;
        }
        Screen.InputLocked=_menuOpen;
        var scale=Math.Clamp(Math.Min(size.X/1672,size.Y/941),.8f,1.15f);
        var menu=DuelUi.Button("Menu",()=>{_menuOpen=true;BuildOverlay();},scale:scale,fontSize:34);
        menu.Position=new Vector2(18,16);menu.Size=new Vector2(115,42)*scale;_overlay.AddChild(menu);
        if(!_menuOpen)return;
        var veil=new ColorRect{Color=new Color(0,0,0,.55f),Size=size};_overlay.AddChild(veil);
        var tools=DuelUi.Modal(veil,size,new Vector2(510,790),"Duel Demo");
        tools.AddChild(DuelUi.Wrapped($"{DuelDemoScenarios.Get(_scenario).Name} · seed {_seed}",16,Palette.PaperDim));
        AddButton(tools,"Resume",()=>{_menuOpen=false;BuildOverlay();},true);
        AddButton(tools,"Challenge details…",()=>{_menuOpen=false;BuildOverlay();Screen.OpenChallenge();});
        AddButton(tools,"Duel guidance…",()=>{_menuOpen=false;BuildOverlay();Screen.OpenGuidance();});
        var guidance=new CheckBox{Text="Show quiet coaching",ButtonPressed=Screen.GuidanceEnabled};tools.AddChild(guidance);
        guidance.Toggled+=value=>{if(!Screen.SetGuidanceEnabled(value,true))guidance.SetPressedNoSignal(Screen.GuidanceEnabled);};
        var sound=new CheckBox{Text="Cinematic sound",ButtonPressed=Screen.SoundEnabled};tools.AddChild(sound);sound.Toggled+=value=>Screen.SoundEnabled=value;
        var ambience=new CheckBox{Text="Field ambience",ButtonPressed=Screen.AmbienceEnabled};tools.AddChild(ambience);ambience.Toggled+=value=>Screen.AmbienceEnabled=value;
        var detailed=new CheckBox{Text="Detailed grass",ButtonPressed=Screen.DetailedAtmosphere};tools.AddChild(detailed);detailed.Toggled+=value=>Screen.DetailedAtmosphere=value;
        AddButton(tools,"Public piles…",()=>{_menuOpen=false;BuildOverlay();Screen.OpenPublicPiles();});
        AddButton(tools,"Restart",Prepare);
        AddButton(tools,"New deal",()=>{NewSeed();Prepare();});
        AddButton(tools,_manual?"Manual":"Solo",()=>{_manual=!_manual;_botClock=0;BuildOverlay();});
        AddButton(tools,"Switch seat",()=>{_menuOpen=false;SwitchSeat();});
        AddButton(tools,"Save replay",SaveReplay);
        AddButton(tools,"Return to menu",()=>ReturnToMenu?.Invoke());
    }

    private bool SaveGuidance(bool enabled)
    {
        var before=Preferences.DuelGuidance;Preferences.DuelGuidance=enabled;
        if(Preferences.Save()==Error.Ok)return true;
        Preferences.DuelGuidance=before;Notice("Could not save guidance preference. Please try again.");return false;
    }

    private static Button AddButton(Control parent,string text,Action action,bool primary=false)
    {
        var button=DuelUi.Button(text,action,primary);parent.AddChild(button);return button;
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
        if(_setup||_manual||_handoff||_sending||_menuOpen||_readingPause||_helpPause||Screen.PresentationBusy||Session.Snapshot?.View.LastDuel is not {Complete:false})return;
        _botClock+=delta;if(_botClock<(ReducedMotion?.15:.8))return;_botClock=0;
        Session.StepPracticeOpponent(DuelOpponent.Choose);
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape}){if(!_setup&&!_handoff){_menuOpen=!_menuOpen;BuildOverlay();}GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree(){Session.Changed-=Refresh;Session.Notice-=Notice;Session.Dispose();}
}
