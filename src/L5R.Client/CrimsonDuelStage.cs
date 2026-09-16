using Godot;
namespace L5R.Client;

/// <summary>Reusable scenery only. Owns no cards, rules, decisions or match state.</summary>
public partial class CrimsonDuelStage : Control
{
    public const float Pitch = .566f, Depth = .00030f, Unit = 100, Density = .93f;
    public static readonly Vector2 Design = new(1672,941);
    public static readonly Vector2 StageCanvas = new(1672,1304);
    public static float CameraDistance => Mathf.Sqrt(1-Pitch*Pitch)/(Depth*Unit);
    public SubViewport World { get; private set; } = null!;
    public Camera3D Camera { get; private set; } = null!;
    public Node3D Model { get; private set; } = null!;
    public bool ReducedMotion { get; set; }
    public int Phase { get; set; } = 3;
    public double AtmosphereTime { get; private set; }
    public int GrassInstances { get; private set; }
    public Vector2 ContentOffset { get; private set; }
    public float ContentScale { get; private set; }
    public float MatLift { get; set; }
    public Vector2 TableOffset => ContentOffset + Vector2.Up * MatLift * ContentScale;
    private TextureRect _landscape=null!,_worldImage=null!;
    private ShaderMaterial _sky=null!,_grass=null!;
    private ShaderMaterial? _foliage;
    private Node3D _vegetation=null!;

    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore; ClipContents=true;
        _sky=new ShaderMaterial {Shader=GD.Load<Shader>("res://Art/CrimsonDuel/landscape.gdshader")};
        _landscape=new TextureRect {Texture=GD.Load<Texture2D>("res://Art/CrimsonDuel/landscape-clean-plate.png"),
            Material=_sky,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore};
        AddChild(_landscape);
        World=new SubViewport {OwnWorld3D=true,TransparentBg=true,HandleInputLocally=false,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always,Msaa3D=Viewport.Msaa.Msaa4X};
        AddChild(World);
        World.AddChild(new WorldEnvironment {Environment=new Godot.Environment {
            BackgroundMode=Godot.Environment.BGMode.ClearColor,TonemapMode=Godot.Environment.ToneMapper.Linear,
            AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.7f}});
        Model=GD.Load<PackedScene>("res://Art/CrimsonDuel/crimson-duel.glb").Instantiate<Node3D>();World.AddChild(Model);
        var mat=(MeshInstance3D)Model.FindChild("CampaignMat",true,false);
        var material=new ShaderMaterial {Shader=GD.Load<Shader>("res://Art/DuelArena/approved-mat.gdshader")};
        material.SetShaderParameter("artwork",GD.Load<Texture2D>("res://Art/CrimsonDuel/campaign-mat-albedo.png"));
        mat.MaterialOverride=material;
        if(MatLift>0){mat.Scale=new Vector3(1,1,1.14f);mat.Position+=new Vector3(0,0,.7f);}
        _vegetation=new Node3D {Name="WindField"};World.AddChild(_vegetation);
        _grass=new ShaderMaterial {Shader=GD.Load<Shader>("res://Art/CrimsonDuel/grass.gdshader")};
        var random=new Random(2050915);
        for(var variant=0;variant<3;variant++)
        {
            var source=(MeshInstance3D)Model.FindChild("GrassTuft"+variant,true,false);source.Visible=false;
            var count=210;
            var multi=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData=true,Mesh=source.Mesh,InstanceCount=count};
            for(var i=0;i<count;i++)
            {
                float Rand(float a,float b)=>a+(float)random.NextDouble()*(b-a);
                float x,z,height;
                if(i<120)
                { x=Rand(-12,12);z=Rand(-5.45f,-5.08f);height=Rand(.13f,.30f); }
                else
                { var side=i%2==0?-1:1;x=side*Rand(8.2f,12.4f);z=Rand(-5.1f,.7f);height=Rand(.20f,.48f); }
                // All planting lies outside the cloth; no foreground stems cross the card surface.
                var rotation=Basis.FromEuler(new Vector3(0,Rand(0,Mathf.Tau),0));
                var scale=new Vector3(height*Rand(.75f,1.25f),height,height*Rand(.75f,1.25f));
                multi.SetInstanceTransform(i,new Transform3D(rotation.Scaled(scale),new Vector3(x,-.08f,z)));
                multi.SetInstanceCustomData(i,new Color(Rand(0,1),Rand(0,1),Rand(0,1),1));
            }
            var node=new MultiMeshInstance3D {Multimesh=multi,MaterialOverride=_grass,
                LodBias=8,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,
                CustomAabb=new Aabb(new Vector3(-14,-1,-8),new Vector3(28,3,11))};
            _vegetation.AddChild(node);GrassInstances+=count;
        }
        BuildFoliageCards();
        Camera=new Camera3D {Current=true,Near=.05f,Far=100};World.AddChild(Camera);
        _worldImage=new TextureRect {Texture=World.GetTexture(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter=MouseFilterEnum.Ignore};AddChild(_worldImage);
        Resize(Size,1);
    }

    public void Resize(Vector2 viewport,float zoom)
    {
        Size=viewport;
        ContentScale=Math.Min(viewport.X/Design.X,viewport.Y/Design.Y);
        ContentOffset=(viewport-Design*ContentScale)/2;
        _landscape.Position=Vector2.Zero;_landscape.Size=viewport;
        // Keep the 16:9 art centered on other aspect ratios without stretching it.
        _landscape.StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered;
        World.Size=(Vector2I)(StageCanvas*ContentScale).Round();
        _worldImage.Position=TableOffset;_worldImage.Size=(Vector2)World.Size;
        Camera.Position=new Vector3(0,CameraDistance*Pitch,CameraDistance*Mathf.Sqrt(1-Pitch*Pitch));
        Camera.LookAt(Vector3.Zero);
        Camera.Fov=Mathf.RadToDeg(2*Mathf.Atan(StageCanvas.Y/(2*CameraDistance*Unit*Density*zoom)));
        _vegetation.Visible=Phase>=2;
    }

    public void SetTime(double time)
    {
        AtmosphereTime=time;
        var t=ReducedMotion||Phase<3?0:(float)time;
        _sky.SetShaderParameter("atmosphere_time",t);_grass.SetShaderParameter("atmosphere_time",t);
        _foliage?.SetShaderParameter("atmosphere_time",t);
    }
    public void Advance(double delta) { if(!ReducedMotion) AtmosphereTime+=delta;SetTime(AtmosphereTime); }
    public Vector2 ProjectWorld(Vector3 point)=>TableOffset+Camera.UnprojectPosition(point);

    private void BuildFoliageCards()
    {
        if(!ResourceLoader.Exists("res://Art/CrimsonDuel/golden-grass-tuft.png"))return;
        _foliage=new ShaderMaterial {Shader=GD.Load<Shader>("res://Art/CrimsonDuel/grass.gdshader")};
        _foliage.SetShaderParameter("use_foliage_art",true);
        _foliage.SetShaderParameter("foliage_art",GD.Load<Texture2D>("res://Art/CrimsonDuel/golden-grass-tuft.png"));
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
        for(var row=0;row<=8;row++)
        {
            var y=row/8f;
            vertices.Add(new Vector3(-.5f,y,0));vertices.Add(new Vector3(.5f,y,0));
            uv.Add(new Vector2(0,1-y));uv.Add(new Vector2(1,1-y));
            if(row<8){var a=row*2;indices.AddRange([a,a+2,a+1,a+1,a+2,a+3]);}
        }
        var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex]=vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal]=Enumerable.Repeat(Vector3.Back,vertices.Count).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV]=uv.ToArray();arrays[(int)Mesh.ArrayType.Index]=indices.ToArray();
        var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        var random=new Random(20517);
        // Three spatial cells keep alpha foliage culling and overdraw bounded.
        for(var cell=0;cell<3;cell++)
        {
            var count=cell==0?95:48;
            var mm=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseCustomData=true,Mesh=mesh,InstanceCount=count};
            for(var i=0;i<count;i++)
            {
                float Rand(float a,float b)=>a+(float)random.NextDouble()*(b-a);
                var x=cell==0?Rand(-12,12):(cell==1?-1:1)*Rand(8.25f,12.4f);
                var z=cell==0?Rand(-5.55f,-5.10f):Rand(-5.1f,.65f);
                var h=cell==0?Rand(.19f,.35f):Rand(.25f,.52f);
                var basis=Basis.FromEuler(new Vector3(0,Rand(-.28f,.28f),0)).Scaled(new Vector3(h*Rand(1.0f,1.45f),h,1));
                mm.SetInstanceTransform(i,new Transform3D(basis,new Vector3(x,-.09f,z)));
                mm.SetInstanceCustomData(i,new Color(Rand(0,1),Rand(0,1),0,1));
            }
            _vegetation.AddChild(new MultiMeshInstance3D{Multimesh=mm,MaterialOverride=_foliage,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
            GrassInstances+=count;
        }
    }
}
