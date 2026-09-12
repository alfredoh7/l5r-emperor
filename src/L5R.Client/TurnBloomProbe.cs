using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native material, lifecycle and input regression probe; not a playable scenario.</summary>
public partial class TurnBloomProbe : Control
{
    private TurnBloom _bloom=null!;
    private Button _button=null!;
    private int _clicks, _step;
    private double _clock;
    private string _output="";
    private string? _pendingShot;
    private readonly float[] _times=[0,.35f,.65f,1.5f,2.9f,3.6f];
    public override void _Ready()
    {
        _output=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..];
        Directory.CreateDirectory(_output);
        var mat=new TextureRect {Texture=GD.Load<Texture2D>("res://Art/navy-mat-v4.png"), ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};
        AddChild(mat);mat.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _button=new Button {Position=Size/2-new Vector2(70,25),Size=new Vector2(140,50),Text="Input probe",Modulate=new Color(1,1,1,0)};
        _button.Pressed+=()=>_clicks++;
        AddChild(_button);
        _bloom=new TurnBloom {Size=new Vector2(Size.X*940/1280,Size.X*940/1280/3),ManualTime=true,ZIndex=2};
        _bloom.Position=(Size-_bloom.Size)/2; AddChild(_bloom);_bloom.Play();
        try { CheckTurns(); } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Require(bool ok,string why) {if(!ok)throw new InvalidOperationException(why);}
    private static void CheckTurns()
    {
        var engine=MatchEngine.CreatePublishedMatch(91);
        var view=engine.GetView(engine.State.ActivePlayerId);
        var gate=new TurnBloomGate();
        Require(gate.Observe(view,"room-a"),"First local turn must announce");
        Require(!gate.Observe(view,"room-a"),"Same snapshot must not replay");
        Require(!gate.Observe(view with {PriorityPlayerId=1-view.ViewerId,Version=view.Version+1},"room-a"),"Opponent reaction must not replay");
        Require(!gate.Observe(view with {Phase=Phase.Dynasty},"room-a"),"Phase change must not replay");
        var enemy=view with {ActivePlayerId=1-view.ViewerId,TurnNumber=view.TurnNumber+1};
        Require(!gate.Observe(enemy,"room-a"),"Opponent turn must not announce");
        Require(!gate.Observe(enemy with {PriorityPlayerId=view.ViewerId},"room-a"),"Local battle response must not announce");
        var next=view with {TurnNumber=view.TurnNumber+2};
        Require(gate.Observe(next,"room-a"),"Next local turn must announce");
        Require(!gate.Observe(next,"room-a"),"Reconnect/duplicate same turn must not replay");
        Require(!gate.Observe(next with {ViewerId=1-view.ViewerId},"room-a"),"Changing practice seat must not announce");
        Require(!gate.Observe(next,"room-a"),"Returning practice seat must not announce");
        Require(!gate.Observe(next with {WinnerId=view.ViewerId,TurnNumber=next.TurnNumber+2},"room-a"),"Victory must not announce");
        gate.Reset();Require(gate.Observe(view,"practice"),"New game reset must announce");
        Require(gate.Observe(view,"room-b"),"Different room must announce");
    }
    public override void _Process(double delta)
    {
        _clock+=delta;if(_clock<.15)return;_clock=0;
        try
        {
            if(_pendingShot is not null) { GetViewport().GetTexture().GetImage().SavePng(_pendingShot); _pendingShot=null; }
            if(_step<_times.Length)
            {
                _bloom.SetTime(_times[_step]);
                _pendingShot=Path.Combine(_output,$"frame-{_step}.png");
            }
            else if(_step==6)
            {
                Require(!_bloom.Visible && !_bloom.Playing,"Must fully clear automatically");
                _bloom.SetTime(1.5f);
                var at=Size/2;
                GetViewport().PushInput(new InputEventMouseMotion {Position=at,GlobalPosition=at},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=at,GlobalPosition=at,ButtonIndex=MouseButton.Left,Pressed=true},true);
                GetViewport().PushInput(new InputEventMouseButton {Position=at,GlobalPosition=at,ButtonIndex=MouseButton.Left,Pressed=false},true);
            }
            else if(_step==7)
            {
                Require(_clicks==1,"Announcement intercepted native pointer input");
                _button.GrabFocus();
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,PhysicalKeycode=Key.Enter,Pressed=true},true);
                GetViewport().PushInput(new InputEventKey {Keycode=Key.Enter,PhysicalKeycode=Key.Enter,Pressed=false},true);
            }
            else if(_step==8)
            {
                Require(_clicks==2,"Announcement intercepted native keyboard input");
                _bloom.ReducedMotion=true;_bloom.SetTime(1.5f);
                _pendingShot=Path.Combine(_output,"reduced.png");
            }
            else if(_step==9)
            {
                _bloom.SetTime(2.7f);Require(!_bloom.Visible,"Reduced motion failed to clear");
                _bloom.Play();_bloom.Cancel();Require(!_bloom.Visible,"Menu interruption failed to cancel");
                GD.Print("TURN_BLOOM_NATIVE_PASS: turn ownership, priority, phase, repeat, room, reset, seat, victory; native pointer/keyboard pass-through; normal/reduced clear.");GetTree().Quit();
            }
            _step++;
        }
        catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
