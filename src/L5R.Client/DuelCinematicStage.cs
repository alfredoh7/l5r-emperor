using Godot;
namespace L5R.Client;

/// <summary>Approved composition as an animated matte; live cards use their own projection.</summary>
public partial class DuelCinematicStage : Control
{
    public const float Pitch=.64f,Depth=.00026f,Density=1.05f,MatLift=55;
    public static readonly Vector2 Design=new(1672,941),StageCanvas=new(1672,1304);
    public float ContentScale {get;private set;}
    public Vector2 ContentOffset {get;private set;}
    public Vector2 TableOffset=>ContentOffset+Vector2.Up*MatLift*ContentScale;
    public bool ReducedMotion {get;set;}
    private TextureRect _plate=null!;
    private ShaderMaterial _atmosphere=null!;
    private double _time;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        _atmosphere=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/cinema-atmosphere.gdshader")};
        _plate=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/CrimsonDuel/cinema-clean-plate.png"),Material=_atmosphere,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore};AddChild(_plate);
    }
    public void Resize(Vector2 size)
    {
        Size=size;ContentScale=Math.Min(size.X/Design.X,size.Y/Design.Y);ContentOffset=(size-Design*ContentScale)/2;
        _plate.Position=ContentOffset;_plate.Size=Design*ContentScale;
    }
    public void Advance(double delta)
    {
        if(!ReducedMotion)_time+=delta;
        _atmosphere.SetShaderParameter("atmosphere_time",ReducedMotion?0:(float)_time);
    }
}
