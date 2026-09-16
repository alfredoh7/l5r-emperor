using Godot;
namespace L5R.Client;

/// <summary>Approved matte with flow-mapped sky and depth-rendered grass; cards keep their own projection.</summary>
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
    private SubViewport _vegetationView=null!;
    private TextureRect _vegetationImage=null!;
    private DuelWindField _wind=null!;
    public bool DetailedAtmosphere {get;set;}=true;
    public bool Occluded {get;set;}
    private double _renderedTime=-1;
    private float _renderedStrength=-1;
    public double AtmosphereTime=>ReducedMotion?0:_time;
    public float WindStrength {get;set;}=1;
    public int GrassInstances=>DetailedAtmosphere?_wind.Instances:0;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;ClipContents=true;
        _atmosphere=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/cinema-atmosphere.gdshader")};
        _plate=new TextureRect{Texture=GD.Load<Texture2D>("res://Art/CrimsonDuel/cinema-clean-plate.png"),Material=_atmosphere,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore};AddChild(_plate);
        _vegetationView=new SubViewport{OwnWorld3D=true,TransparentBg=true,HandleInputLocally=false,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always,Msaa3D=Viewport.Msaa.Msaa4X};AddChild(_vegetationView);
        _vegetationView.AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.ClearColor,TonemapMode=Godot.Environment.ToneMapper.Linear}});
        _vegetationView.AddChild(new Camera3D{Current=true,Projection=Camera3D.ProjectionType.Orthogonal,Size=9.41f,Position=new Vector3(0,4.705f,20)});
        _wind=new DuelWindField();_vegetationView.AddChild(_wind);_wind.Build();
        _vegetationImage=new TextureRect{Texture=_vegetationView.GetTexture(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            Material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Art/CrimsonDuel/vegetation-composite.gdshader")},MouseFilter=MouseFilterEnum.Ignore};AddChild(_vegetationImage);
    }
    public void Resize(Vector2 size)
    {
        Size=size;ContentScale=Math.Min(size.X/Design.X,size.Y/Design.Y);ContentOffset=(size-Design*ContentScale)/2;
        _renderedTime=-1;
        _plate.Position=ContentOffset;_plate.Size=Design*ContentScale;
        _vegetationView.Size=(Vector2I)(Design*ContentScale).Round();
        _vegetationImage.Position=ContentOffset;_vegetationImage.Size=_plate.Size;
    }
    public void Advance(double delta)
    {
        if(!ReducedMotion)_time+=delta;
        if(Occluded){_vegetationView.RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled;return;}
        var strength=ReducedMotion?1:WindStrength;
        _atmosphere.SetShaderParameter("atmosphere_time",(float)AtmosphereTime);
        _atmosphere.SetShaderParameter("wind_strength",strength);
        _wind.Present(AtmosphereTime,strength);
        _vegetationImage.Visible=DetailedAtmosphere;
        var changed=_renderedTime!=AtmosphereTime||!Mathf.IsEqualApprox(_renderedStrength,strength);
        _vegetationView.RenderTargetUpdateMode=DetailedAtmosphere&&changed?SubViewport.UpdateMode.Once:SubViewport.UpdateMode.Disabled;
        _renderedTime=DetailedAtmosphere?AtmosphereTime:-1;_renderedStrength=strength;
    }
}
