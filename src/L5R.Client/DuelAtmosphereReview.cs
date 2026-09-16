using Godot;
using L5R.Core;
using System.Text.Json;
using System.Globalization;
namespace L5R.Client;

/// <summary>Native review of the production duel components, driven only by legal session commands.</summary>
public partial class DuelAtmosphereReview : Control
{
    private readonly MatchSession _session=new();
    private DuelScreen _screen=null!;
    private DuelCinematicStage? _scenery;
    private bool _reduced,_light,_benchmark,_sceneOnly,_mute;
    private string _output="",_phase="";
    private double _elapsed,_actionClock,_fixedTime;
    private ulong _stamp;
    private int _step;
    private readonly Dictionary<string,List<double>> _frames=[];
    private readonly List<object> _timeline=[];
    private readonly List<object> _slowFrames=[];
    private readonly HashSet<string> _captures=[];
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public override void _Ready()
    {
        foreach(var a in OS.GetCmdlineUserArgs())
        {
            if(a.StartsWith("--capture="))_output=a[10..];
            if(a.StartsWith("--time="))_fixedTime=double.Parse(a[7..],CultureInfo.InvariantCulture);
            _reduced|=a=="--reduced-motion";_light|=a=="--light";_benchmark|=a=="--benchmark";_sceneOnly|=a=="--scenery";_mute|=a=="--mute";
        }
        if(string.IsNullOrEmpty(_output))_output=ProjectSettings.GlobalizePath("res://../../artifacts/duel-atmosphere/review");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_output)!);
        Theme=DuelUi.Theme();GetWindow().Unresizable=true;GetWindow().AlwaysOnTop=true;GetWindow().GrabFocus();
        RenderingServer.FramePostDraw+=RecordRenderedFrame;
        if(_sceneOnly)
        {
            _scenery=new DuelCinematicStage{ReducedMotion=_reduced};AddChild(_scenery);_scenery.DetailedAtmosphere=!_light;
            _scenery.Resize(GetViewportRect().Size);_scenery.Advance(_fixedTime);return;
        }
        _screen=new DuelScreen{ReducedMotion=_reduced,OpeningLeadIn=_reduced?2:6,SoundEnabled=!_mute,AmbienceEnabled=!_mute};AddChild(_screen);
        _screen.DetailedAtmosphere=!_light;_screen.SetGuidanceEnabled(false);
        _screen.CommandRequested+=command=>_ = Submit(command);
        _session.Changed+=()=>_screen.Present(_session.Snapshot!);
        _session.Notice+=message=>throw new InvalidOperationException(message);
        _session.StartDuelDemo("steel",205);_ = _session.BeginDuelDemo();
        GD.Print("DUEL_ATMOSPHERE: native production screen; permitted views and commands; seed 205.");
    }
    private async Task Submit(GameCommand command){await _session.Submit(command);_screen.SubmissionFinished();}
    private void Native(Vector2 point)
    {
        GetViewport().WarpMouse(point);Input.ParseInputEvent(new InputEventMouseMotion{Position=point,GlobalPosition=point});
        Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true});
        Input.ParseInputEvent(new InputEventMouseButton{Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false});
    }
    private void Capture(string name)
    {
        if(_benchmark||!_captures.Add(name))return;
        var path=_output+"-"+name+".png";
        if(GetViewport().GetTexture().GetImage().SavePng(path)!=Error.Ok)throw new Exception("Capture failed: "+path);
    }
    public override void _Process(double delta)
    {
        try
        {
            // Movie capture must keep drawing even when macOS occludes its desktop window.
            if(OS.HasFeature("movie")||_sceneOnly)RenderingServer.ForceDraw(false,delta);
            Advance(delta);
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);SetProcess(false);}
    }
    private void RecordRenderedFrame()
    {
        var now=Time.GetTicksUsec();
        if(_stamp>0&&_elapsed>3)
        {
            var ms=(now-_stamp)/1000d;
            if(!_frames.TryGetValue(_phase,out var samples))_frames[_phase]=samples=[];samples.Add(ms);
            if(ms>33.34)_slowFrames.Add(new{Time=_elapsed,Phase=_phase,Milliseconds=ms,Step=_step});
        }
        _stamp=now;
    }
    private void Advance(double delta)
    {
        _elapsed+=delta;
        if(_sceneOnly)
        {
            if(_elapsed>1.2){Capture("scenery");System.IO.File.WriteAllText(_output+".json",JsonSerializer.Serialize(new{Passed=true,ReducedMotion=_reduced,Time=_scenery!.AtmosphereTime,GrassInstances=_scenery.GrassInstances},Json));GetTree().Quit();SetProcess(false);}return;
        }
        var phase=_screen.CinematicBusy?(_screen.CinematicTime<0?"idle":_screen.CinematicPhase):_step<4?"focusing":"verdict";
        if(phase!=_phase){_phase=phase;_timeline.Add(new{Time=Math.Round(_elapsed,3),Phase=phase,Version=_session.Snapshot!.View.Version});GD.Print($"ATMOSPHERE_PHASE: {_elapsed:F2} {phase}");}
        if(_screen.CinematicTime<0&&_elapsed>4)Capture("idle");
        if(phase=="opening-challenger"&&_screen.CinematicTime>4.6)Capture("introduction");
        if(_screen.StoryText.Contains("all provinces +2"))Capture("courtesy");
        if(_elapsed>70)throw new Exception("Atmosphere native sequence stalled.");
        if(_screen.PresentationBusy){_actionClock=0;return;}
        _actionClock+=delta;
        switch(_step)
        {
            case 0 when _actionClock>1.8:
                if(!_session.StepPracticeOpponent((view,actions)=>actions.Single(a=>a.Enabled&&a.Command?.Type=="focus"&&a.Command.Choice=="deck").Command))throw new Exception("Opponent deck focus failed.");
                Capture("deck-focus");Next();break;
            case 1 when _actionClock>2:
                var card=_session.Snapshot!.View.Players[0].Hand.Single(c=>c.Definition?.Id=="oracle-l5r-7996");
                Native(_screen.CardClickPoint(card.Id));Next();break;
            case 2 when _actionClock>1:
                Capture("selected-focus");Native(_screen.LandingClickPoint);Next();break;
            case 3 when _actionClock>4:
                if(_session.Snapshot!.View.Version!=3)throw new Exception("Local focus was not submitted exactly once.");
                Capture("standoff");
                GetViewport().WarpMouse(new Vector2(20,20));
                Input.ParseInputEvent(new InputEventMouseMotion{Position=new Vector2(20,20),GlobalPosition=new Vector2(20,20)});
                if(!_session.StepPracticeOpponent((view,actions)=>actions.Single(a=>a.Enabled&&a.Command?.Type=="strike").Command))throw new Exception("Opponent Strike failed.");
                Next();break;
            case 4 when _actionClock>6:
                var record=_session.Snapshot!.View.LastDuel!;
                if(!_screen.CanContinue||record.Result is not {ChallengerTotal:8,ChallengedTotal:7,ReturnPriority:1}||_screen.OpeningRuns!=1||_screen.EndingRuns!=1)throw new Exception("Canonical duel/one-shot presentation failed.");
                if(_reduced&&_screen.AtmosphereTime!=0)throw new Exception("Reduced motion advanced ambient time.");
                Capture("result");SaveReport();GetTree().Quit();SetProcess(false);break;
        }
        void Next(){_step++;_actionClock=0;}
    }
    private void SaveReport()
    {
        object Metrics(IEnumerable<double> frames)
        {var v=frames.Order().ToArray();double P(double p)=>v.Length==0?0:v[Math.Min(v.Length-1,(int)(v.Length*p))];return new{Samples=v.Length,P50Ms=P(.50),P95Ms=P(.95),P99Ms=P(.99),MaxMs=P(1),Over33Ms=v.Count(x=>x>33.34)};}
        var report=new{Passed=true,ActualSessionCommands=_session.Snapshot!.View.Version,CanonicalResult="8–7",OpeningRuns=_screen.OpeningRuns,EndingRuns=_screen.EndingRuns,
            ReducedMotion=_reduced,DetailedGrass=!_light,GrassInstances=_screen.GrassInstances,Resolution=GetViewportRect().Size.ToString(),DurationSeconds=_elapsed,
            Mode=OS.HasFeature("movie")?"offline movie; not a performance benchmark":_benchmark?"real-time native benchmark; completed rendered frames; monotonic wall clock":"native review",
            Frames=Metrics(_frames.Values.SelectMany(v=>v)),ByPhase=_frames.ToDictionary(k=>k.Key,k=>Metrics(k.Value)),SlowFrames=_slowFrames,Timeline=_timeline,
            DrawCalls=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),Gpu=RenderingServer.GetVideoAdapterName(),
            Notes="Uses the production screen and MatchSession, a blind legal opponent focus, native local selection/placement, and authoritative resolution. Procedural prototype sound; native Windows/AAA human acceptance not implied."};
        System.IO.File.WriteAllText(_output+".json",JsonSerializer.Serialize(report,Json));
        System.IO.File.WriteAllText(_output+"-replay.json",_session.ExportPracticeReplay());
    }
    public override void _ExitTree(){RenderingServer.FramePostDraw-=RecordRenderedFrame;_session.Dispose();}
}
