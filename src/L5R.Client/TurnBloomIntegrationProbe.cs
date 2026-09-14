using Godot;
using System.Reflection;
namespace L5R.Client;

/// <summary>Exercises the real Main scene, menu entry and live render lifecycle.</summary>
public partial class TurnBloomIntegrationProbe : Node
{
    private Main _main=null!;
    private TurnBloom _bloom=null!;
    private MatchSession _session=null!;
    private string _output="";
    private int _step, _plays, _settleFrames;
    private double _clock;
    private long _version;
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private T Field<T>(string name)=>(T)typeof(Main).GetField(name,Private)!.GetValue(_main)!;
    private void Set(string name,object value)=>typeof(Main).GetField(name,Private)!.SetValue(_main,value);
    private void Call(string name,params object[] args)=>typeof(Main).GetMethod(name,Private)!.Invoke(_main,args);
    private static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public override void _Ready()
    {
        _output=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--output="))[9..];Directory.CreateDirectory(_output);
        _main=new Main();Set("_profile","turn-bloom-probe");Set("_autoOpponent",false);AddChild(_main);
        _bloom=Field<TurnBloom>("_turnBloom");_session=Field<MatchSession>("_session");
    }
    public override void _Process(double delta)
    {
        _clock+=delta;if(++_settleFrames<4 || _clock<.5)return;_clock=0;_settleFrames=0;
        try
        {
            switch(_step)
            {
                case 0:
                    Require(_session.Snapshot is null && !_bloom.Visible,"Startup must stay in menu with no announcement");
                    Call("StartFromMenu",0);
                    if(_session.Snapshot!.View.ActivePlayerId!=0)Call("StartFromMenu",_session.Snapshot.View.ActivePlayerId);
                    break;
                case 1:
                    Require(_bloom.PlayCount==1,"New game must show one local turn announcement");
                    _bloom.ManualTime=true;_bloom.SetTime(1.5f);_plays=_bloom.PlayCount;
                    break;
                case 2:
                    Require(_bloom.IsVisibleInTree() && _bloom.Playing,"Ink disappeared before readable hold");
                    GD.Print($"TURN_BLOOM_FRAME: {_bloom.GetGlobalRect()} elapsed={_bloom.Elapsed} children={_bloom.GetChildCount()} reduced={_bloom.ReducedMotion}");
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output,"board.png"));
                    _version=_session.Snapshot!.View.Version;
                    var pass=_session.Snapshot.Actions.First(a=>a.Enabled && a.Type=="pass");
                    _session.Submit(pass.Command!).GetAwaiter().GetResult();
                    break;
                case 3:
                    Require(_session.Snapshot!.View.Version>_version,"Action was blocked during announcement");
                    Require(_bloom.PlayCount==_plays,"Priority change replayed announcement");
                    Call("ShowMainMenu");break;
                case 4:
                    Require(!_bloom.Visible,"Menu failed to cancel announcement");Call("EnterTable");break;
                case 5:
                    Require(!_bloom.Visible && _bloom.PlayCount==_plays,"Menu resume replayed same turn");
                    Call("StartFromMenu",_session.Snapshot!.View.ActivePlayerId);break;
                case 6:
                    Require(_bloom.PlayCount==_plays+1,"Replacement game failed to reset turn history");
                    Set("_reducedMotion",true);_bloom.SetTime(1.5f);break;
                case 7:
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output,"board-reduced.png"));
                    _bloom.ManualTime=false;break;
                case 11:
                    Require(!_bloom.Visible,"Real process did not auto-clear reduced motion");
                    GD.Print("TURN_BLOOM_INTEGRATION_PASS: startup, game entry, live action, priority, menu interruption/resume, new game, reduced motion auto-clear");GetTree().Quit();break;
            }
            _step++;
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
