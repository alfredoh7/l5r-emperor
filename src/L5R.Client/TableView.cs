using Godot;

namespace L5R.Client;

/// <summary>The Blender-produced table behind the native card interface. Contains no game state.</summary>
public partial class TableView : Node3D
{
    private Camera3D _camera = null!;
    private Node3D _tableModel = null!;
    private WorldEnvironment _world = null!;
    private readonly List<(MeshInstance3D Mesh,int Surface,BaseMaterial3D Material,string Role)> _materials=[];
    private int _direction;

    public override void _Ready()
    {
        Name="EmperorTable";
        var packed=GD.Load<PackedScene>("res://Art/table.glb");
        if(packed is null)
        {
            GD.PushError("The Blender table asset could not be loaded. Rebuild tools/blender/build_table.py and import Art/table.glb.");
            return;
        }
        var table=packed.Instantiate<Node3D>(); _tableModel = table;
        AddChild(table);
        CollectMaterials(table);
        _world=new WorldEnvironment
        {
            Environment=new Godot.Environment
            {
                BackgroundMode=Godot.Environment.BGMode.Color,
                BackgroundColor=new Color("151d19"),
                AmbientLightSource=Godot.Environment.AmbientSource.Color,
                AmbientLightColor=new Color("899587"),
                AmbientLightEnergy=0.24f,
                ReflectedLightSource=Godot.Environment.ReflectionSource.Disabled,
                TonemapMode=Godot.Environment.ToneMapper.Filmic,
                TonemapExposure=0.58f
            }
        };
        AddChild(_world);
        var key=new DirectionalLight3D
        {
            Name="SoftWarmKey",RotationDegrees=new Vector3(-69,-27,0),
            LightColor=new Color("e8ddc9"),LightEnergy=1.0f,ShadowEnabled=true,
            DirectionalShadowMaxDistance=24,ShadowBlur=1.8f
        };
        AddChild(key);
        var fill=new DirectionalLight3D
        {
            Name="CoolFill",RotationDegrees=new Vector3(-52,142,0),
            LightColor=new Color("aebfca"),LightEnergy=0.18f,ShadowEnabled=false
        };
        AddChild(fill);
        AddLamp(new Vector3(-5.74f,.68f,-2.60f));
        AddLamp(new Vector3(5.74f,.68f,2.60f));
        _camera=new Camera3D
        {
            Name="TableCamera",Projection=Camera3D.ProjectionType.Orthogonal,
            KeepAspect=Camera3D.KeepAspectEnum.Height,
            Position=new Vector3(0,12,1.0f),Near=.05f,Far=40,Current=true
        };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero,Vector3.Up);
        GetViewport().SizeChanged+=FitCamera;
        FitCamera();
        ApplyDirection(_direction);
    }

    public override void _ExitTree()
    {
        if(IsInstanceValid(_camera)) GetViewport().SizeChanged-=FitCamera;
    }

    /// <summary>0: lacquer/green silk, 1: paper/stone, 2: indigo/brass. May be called before Ready.</summary>
    public void ApplyDirection(int direction)
    {
        _direction=Math.Clamp(direction,0,2);
        foreach(var entry in _materials)
        {
            if(entry.Role.Contains("TableCloth",StringComparison.Ordinal))
            {
                // The GLB has a green-tinted albedo. A native shader preserves its weave while changing pigment.
                var shader=new Shader { Code="""
                    shader_type spatial;
                    render_mode diffuse_burley;
                    uniform sampler2D cloth_texture : source_color, filter_linear_mipmap;
                    uniform vec4 cloth_color : source_color = vec4(0.15, 0.20, 0.17, 1.0);
                    void fragment() {
                        vec3 original = texture(cloth_texture, UV).rgb;
                        ALBEDO = original * cloth_color.rgb;
                        ROUGHNESS = 0.94;
                    }
                    """ };
                var material=new ShaderMaterial { Shader=shader };
                material.SetShaderParameter("cloth_texture",entry.Material.AlbedoTexture);
                material.SetShaderParameter("cloth_color",_direction switch {1=>new Color("b9b39f"),2=>new Color("202e38"),_=>new Color("a6a894")});
                entry.Mesh.SetSurfaceOverrideMaterial(entry.Surface,material);
            }
            else if(entry.Role.Contains("InkInlay",StringComparison.Ordinal))
            {
                var color=_direction switch {1=>new Color("8d8b7b"),2=>new Color("283842"),_=>new Color("343e2b")};
                entry.Material.AlbedoColor=color;
            }
            else if(entry.Role.Contains("Walnut",StringComparison.Ordinal))
            {
                entry.Material.AlbedoColor=new Color(.62f,.58f,.52f);
                entry.Material.Roughness=0.80f;
                entry.Material.MetallicSpecular=0.08f;
                entry.Material.Metallic=0;
            }
            else if(entry.Role.Contains("AntiqueBrass",StringComparison.Ordinal))
            {
                entry.Material.AlbedoColor=new Color("907444");
                entry.Material.Roughness=0.65f;
                entry.Material.Metallic=0.45f;
            }
            else if(entry.Role.Contains("LampFlame",StringComparison.Ordinal))
            {
                entry.Material.AlbedoColor=new Color("d59a58");
                entry.Material.EmissionEnabled=true;
                entry.Material.Emission=new Color("bb7737");
                entry.Material.EmissionEnergyMultiplier=0.30f;
            }
        }
        if(IsInstanceValid(_world))
        {
            _world.Environment.AmbientLightEnergy=_direction==1?0.43f:0.28f;
            _world.Environment.TonemapExposure=_direction==1?0.95f:0.80f;
            _world.Environment.BackgroundColor=_direction switch {1=>new Color("4a4a40"),2=>new Color("151d25"),_=>new Color("151d19")};
        }
    }

    private void CollectMaterials(Node node)
    {
        if(node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for(var surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
            {
                if(mesh.Mesh.SurfaceGetMaterial(surface) is not BaseMaterial3D source) continue;
                var copy=(BaseMaterial3D)source.Duplicate();
                mesh.SetSurfaceOverrideMaterial(surface,copy);
                _materials.Add((mesh,surface,copy,source.ResourceName));
            }
        }
        foreach(var child in node.GetChildren()) CollectMaterials(child);
    }

    private void AddLamp(Vector3 location)
    {
        AddChild(new OmniLight3D
        {
            Name="EdgeLampGlow",Position=location,LightColor=new Color("ffc579"),
            LightEnergy=.30f,OmniRange=2.2f,OmniAttenuation=1.6f,ShadowEnabled=false
        });
    }

    private void FitCamera()
    {
        if(!IsInstanceValid(_camera)) return;
        var size=GetViewport().GetVisibleRect().Size;
        var aspect=Mathf.Max(.4f,size.X/Mathf.Max(1,size.Y));
        _camera.Size=8.08f;
        _tableModel.Scale=new Vector3(aspect * 8.08f / 12.12f, 1, 1);
    }
}
