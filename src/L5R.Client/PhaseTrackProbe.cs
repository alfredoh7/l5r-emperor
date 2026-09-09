using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native presentation contract/input fixtures; no production turn timing changes.</summary>
public partial class PhaseTrackProbe : Control
{
    private PhaseTrack _track=null!;
    private MatchView _view=null!;
    private string _output="/tmp/six-phases";
    private int _stage,_phase,_passes;
    private float _clock;
    private byte[]? _still;
    private static readonly Phase[] Ordered=[Phase.Straighten,Phase.Events,Phase.Action,Phase.AttackDeclaration,Phase.Dynasty,Phase.End];
    public override void _Ready()
    {
        foreach(var arg in OS.GetCmdlineUserArgs())if(arg.StartsWith("--output="))_output=arg[9..];
        System.IO.Directory.CreateDirectory(_output);
        Theme=Palette.MakeTheme();GetWindow().GrabFocus();_clock=-.8f;
        var screen=GetViewportRect().Size;
        AddChild(new TextureRect {Texture=GD.Load<Texture2D>("res://Art/navy-mat-v4.png"),Size=screen,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore});
        var label=Palette.Text("Approved A artwork · Native six-phase rail",26,Palette.Gold);label.Position=new Vector2(40,25);AddChild(label);
        var width=Math.Min(screen.X-80,1294);
        AddChild(new PhaseReferencePlate{Position=new Vector2((screen.X-width)/2,85),Size=new Vector2(width-34,(width-34)*154/1774),MouseFilter=MouseFilterEnum.Ignore});
        _track=new PhaseTrack{Position=new Vector2((screen.X-width)/2,270),Size=new Vector2(width,150),ReducedMotion=true,PassRequested=()=>_passes++};AddChild(_track);
        if(OS.GetCmdlineUserArgs().Contains("--scaled")) { _track.Scale=Vector2.One*.66f; _track.Size=new Vector2(width/.66f,150); }
        _view=MatchEngine.CreateStarterMatch().GetView(0);
        _track.Present(_view with{Phase=Ordered[0]});_track.PresentActions(_view,[],false);
        foreach(var phase in Enum.GetValues<Phase>())
        {
            var expected=phase switch {Phase.Straighten=>0,Phase.Events=>1,Phase.Action=>2,Phase.Dynasty or Phase.DynastyDiscard=>4,Phase.End=>5,Phase.Finished=>-1,_=>3};
            if(PhaseTrack.SegmentFor(phase)!=expected)throw new Exception("Wrong phase contract: "+phase);
        }
    }
    private void KeyEvent(Key key){Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=true});Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=false});}
    private void Save(string name){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng(_output+"/"+name+".png");}
    public override void _Process(double delta)
    {
        _clock+=(float)delta;if(_clock<.65f)return;_clock=0;
        try
        {
            if(_stage==0)
            {
                if(_track.HelpRegions.Count!=6)throw new Exception("Missing help regions");
                var area=_track.HelpRegions[1];GetWindow().GrabFocus();Input.WarpMouse(area.GetGlobalRect().GetCenter());Input.ParseInputEvent(new InputEventMouseMotion{Position=area.GetGlobalRect().GetCenter()});_stage++;return;
            }
            if(_stage==1)
            {
                if(!_track.HelpText.Contains("Events-phase"))throw new Exception("Events hover help missing: "+_track.HelpText+" hovered="+GetViewport().GuiGetHoveredControl()?.GetPath()+" area="+_track.HelpRegions[1].GetGlobalRect());
                _track.HelpRegions[1].GrabFocus();Save("hover-events");
                Input.ParseInputEvent(new InputEventMouseMotion{Position=Vector2.One*5});_track.HelpRegions[0].GrabFocus();KeyEvent(Key.Right);_stage++;return;
            }
            if(_stage==2)
            {
                if(!_track.HelpRegions[1].HasFocus()||!_track.HelpVisible)throw new Exception("Keyboard help navigation failed");
                KeyEvent(Key.Escape);_stage++;return;
            }
            if(_stage==3)
            {
                if(_track.HelpVisible||_passes!=0)throw new Exception("Dismissal failed or phase help invoked Pass");
                _track.Present(_view with{Phase=Ordered[_phase]});_track.PresentLight(0,false);_stage++;return;
            }
            if(_stage==4)
            {
                if(_track.ActiveSegment!=_phase)throw new Exception("Active phase mismatch");
                foreach(var area in _track.HelpRegions)if(!GetViewportRect().Encloses(area.GetGlobalRect()))throw new Exception("Clipped phase target");
                Save(Ordered[_phase].ToString());
                if(++_phase<6){_stage=3;return;}
                _still=GetViewport().GetTexture().GetImage().GetData();_track.PresentLight(9,false);_stage++;return;
            }
            if(_stage==5)
            {
                RenderingServer.ForceDraw();if(!_still!.SequenceEqual(GetViewport().GetTexture().GetImage().GetData()))throw new Exception("Reduced motion changed pixels");
                _track.Present(_view with{Phase=Phase.Finished});_stage++;return;
            }
            if(_track.ActiveSegment!=-1)throw new Exception("Finished match highlights a playable phase");
            GD.Print("SIX_PHASE_PASS: all enum mappings, six native states, hover Events help, keyboard arrows/Escape, no Pass dispatch, viewport fit, pixel-stable reduced motion, Finished unlit.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());Save("failure");GetTree().Quit(1);}
    }
}

public partial class PhaseReferencePlate : Control
{
    public override void _Draw()=>DrawTextureRectRegion(GD.Load<Texture2D>("res://Art/six-phase-reference.png"),new Rect2(Vector2.Zero,Size),new Rect2(0,188,1774,154));
}
