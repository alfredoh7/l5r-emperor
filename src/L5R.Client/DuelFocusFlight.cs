using Godot;
namespace L5R.Client;

/// <summary>A permitted face turning into a committed back. No card identity is queried here.</summary>
public partial class DuelFocusFlight : Control
{
    public required Vector2[] From {get;init;}
    public required Vector2[] To {get;init;}
    public Texture2D? SourceFace {get;init;}
    public required Action Landed {get;init;}
    public required Func<bool> Paused {get;init;}
    public float ViewScale {get;init;}=1;
    public const double Duration=.64;
    private double _clock;
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
        var travel=t*t*(3-2*t);
        var lift=Mathf.Sin(t*Mathf.Pi)*28*ViewScale;
        var quad=From.Zip(To,(a,b)=>a.Lerp(b,travel)-Vector2.Down*lift).ToArray();
        var center=quad.Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
        // Rotate the local face once while lifting, then settle squarely onto the mat.
        var flip=SourceFace is null?1:Math.Max(.025f,Math.Abs(Mathf.Cos(Math.Min(1,t/.78f)*Mathf.Pi)));
        var roll=Mathf.Sin(t*Mathf.Pi)*.025f;
        quad=quad.Select(p=>center+new Vector2((p-center).X*flip,(p-center).Y).Rotated(roll)).ToArray();
        for(var spread=6;spread>=2;spread-=2)
            DrawColoredPolygon(quad.Select(p=>p+Vector2.Down*(3+lift*.4f)+(p-center).Normalized()*spread*ViewScale).ToArray(),new Color(0,0,0,.06f));
        DrawColoredPolygon(quad,Colors.White,new Vector2[]{Vector2.Zero,Vector2.Right,Vector2.One,Vector2.Down},SourceFace is not null&&t<.39f?SourceFace:TablePile.FateBack);
        DrawPolyline(quad.Append(quad[0]).ToArray(),new Color(.70f,.58f,.39f,.75f),ViewScale*.8f,true);
    }
}
