using Godot;
namespace L5R.Client;

/// <summary>Moves a permitted, original card face between its exact projected corners.</summary>
public partial class DuelCardTransition : Control
{
    public required Vector2[] From {get;init;}
    public required Vector2[] To {get;init;}
    public required Texture2D Face {get;init;}
    public required Action Landed {get;init;}
    public required Func<bool> Paused {get;init;}
    public double Duration {get;init;}=.95;
    public bool Reveal {get;init;}
    public float ViewScale {get;init;}=1;
    private double _clock;
    internal double Elapsed=>_clock;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.LinearWithMipmaps;Material=CardPaper.Reading;}
    public override void _Process(double delta)
    {
        if(Paused())return;
        _clock+=delta;
        if(_clock>=Duration){Landed();QueueFree();SetProcess(false);return;}
        QueueRedraw();
    }
    public override void _Draw()
    {
        var t=Math.Clamp((float)(_clock/Duration),0,1);
        // Quintic easing reaches rest with zero velocity and acceleration at both ends.
        var eased=t*t*t*(t*(t*6-15)+10);
        var quad=From.Zip(To,(a,b)=>a.Lerp(b,eased)).ToArray();
        var center=quad.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
        if(Reveal)
        {
            var flip=Math.Max(.018f,Math.Abs(Mathf.Cos(t*Mathf.Pi)));
            quad=quad.Select(p=>center+new Vector2((p-center).X*flip,(p-center).Y)).ToArray();
        }
        foreach(var spread in new[]{8f,5f,2f})
            DrawColoredPolygon(quad.Select(p=>p+new Vector2(2,4)*ViewScale+(p-center).Normalized()*spread*ViewScale).ToArray(),new Color(0,0,0,.08f));
        DrawColoredPolygon(quad,Colors.White,new[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},Reveal&&t<.5f?TablePile.FateBack:Face);
        DrawPolyline(quad.Append(quad[0]).ToArray(),new Color(.74f,.61f,.40f,.65f),.8f*ViewScale,true);
    }
}
