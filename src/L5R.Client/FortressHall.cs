using Godot;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Approved B3 scenery. Static presentation only, with no match/state access.</summary>
public partial class FortressHall : Node3D
{
    private readonly List<Node3D> _tableLanterns = [];
    private readonly List<OmniLight3D> _tableLights = [];
    public override void _Ready()
    {
        var room = GD.Load<PackedScene>("res://Art/WarRoom/fortress-hall-b3.glb").Instantiate<Node3D>();
        AddChild(room);
        Batch(room);
        using var layout = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Art/WarRoom/layout-b3.json"));
        if (layout.RootElement.TryGetProperty("furnishings", out var furnishings))
            foreach (var placement in furnishings.EnumerateArray())
            {
                var kind = placement.GetProperty("kind").GetString()!;
                var prop = GD.Load<PackedScene>($"res://Art/WarRoom/{kind}.glb").Instantiate<Node3D>();
                var body = prop.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>().First();
                var dimensions = body.GetAabb().Size;
                var p = placement.GetProperty("position").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                var h = placement.GetProperty("height").GetSingle();
                prop.Position = new Vector3(p[0],p[2],-p[1]);
                prop.Basis = new Basis(new Vector3(placement.GetProperty("width").GetSingle()/dimensions.X,0,0),
                    new Vector3(0,h,-h*.833f),new Vector3(0,0,placement.GetProperty("depth").GetSingle()/dimensions.Z));
                AddChild(prop);
                foreach (var mesh in prop.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
                    for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                        if (mesh.GetActiveMaterial(i) is StandardMaterial3D source)
                        {
                            var worn = (StandardMaterial3D)source.Duplicate();
                            worn.AlbedoColor = kind == "chest" ? new Color(.65f,.50f,.35f) : new Color(.35f,.30f,.25f);
                            worn.Metallic = kind == "chest" ? .40f : .75f;
                            worn.Roughness = .80f; worn.NormalScale = .65f;
                            mesh.SetSurfaceOverrideMaterial(i,worn);
                        }
            }
        if (layout.RootElement.TryGetProperty("columns", out var columns))
            foreach (var placement in columns.EnumerateArray())
            {
                var column = GD.Load<PackedScene>("res://Art/WarRoom/aged-column.glb").Instantiate<Node3D>();
                var body = column.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>().First();
                var dimensions = body.GetAabb().Size;
                var p = placement.GetProperty("position").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                var w = placement.GetProperty("width").GetSingle(); var h = placement.GetProperty("height").GetSingle();
                column.Position = new Vector3(p[0], p[2], -p[1]);
                column.Basis = new Basis(new Vector3(w/dimensions.X,0,0),new Vector3(0,h,-h*.833f),new Vector3(0,0,w/dimensions.Z));
                AddChild(column);
                foreach (var mesh in column.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
                    for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                        if (mesh.GetActiveMaterial(i) is StandardMaterial3D source)
                        {
                            var timber = (StandardMaterial3D)source.Duplicate();
                            timber.Metallic = .08f; timber.MetallicSpecular = .22f;
                            timber.Roughness = .88f; timber.NormalScale = .55f;
                            mesh.SetSurfaceOverrideMaterial(i, timber);
                        }
            }
        foreach (var lamp in layout.RootElement.GetProperty("lamps").EnumerateArray())
        {
            var p = lamp.GetProperty("position").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            var position = new Vector3(p[0], p[2], -p[1]);
            var kind = lamp.GetProperty("kind").GetString()!;
            var height = lamp.GetProperty("height").GetSingle();
            if (kind != "ember")
            {
                var path = $"res://Art/WarRoom/{kind}-lantern.glb";
                if (ResourceLoader.Exists(path))
                {
                    var prop = GD.Load<PackedScene>(path).Instantiate<Node3D>();
                    prop.Name = lamp.GetProperty("name").GetString()!;
                    prop.Position = position; prop.Scale = Vector3.One * height;
                    prop.Basis = new Basis(new Vector3(height,0,0), new Vector3(0,height,-height*.833f), new Vector3(0,0,height));
                    // GLB +Y is up. The original is normalized to a ground-contact pivot.
                    AddChild(prop);
                    foreach (var mesh in prop.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
                    {
                        // The generated amber panes are opaque to shadow maps.
                        // Avoid trapping the candle light inside its own fixture.
                        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                            if (mesh.GetActiveMaterial(surface) is StandardMaterial3D source)
                            {
                                var aged = (StandardMaterial3D)source.Duplicate();
                                aged.AlbedoColor = kind == "table" ? new Color(.60f,.54f,.45f) : new Color(.29f,.24f,.18f);
                                aged.Metallic = .40f; aged.MetallicSpecular = .30f; aged.Roughness = 1.0f; aged.NormalScale = .6f;
                                mesh.SetSurfaceOverrideMaterial(surface, aged);
                            }
                    }
                    AddCandle(prop, kind);
                    if (kind == "table") _tableLanterns.Add(prop);
                }
            }
            if (kind == "ember")
                for (var i=0; i<5; i++)
                {
                    var fire = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/static-flame.gdshader") };
                    fire.SetShaderParameter("seed", i*1.7f);
                    AddChild(new MeshInstance3D { Name="StationaryBrazierFlame", Mesh=new QuadMesh { Size=new Vector2(.045f,.075f+i%3*.017f) },
                        Position=position+new Vector3((i-2)*.017f,.015f,-i%2*.012f), MaterialOverride=fire,
                        CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
                }
            var light = new OmniLight3D
            {
                Name = "PracticalLight", Position = position + new Vector3(0,height*.49f,-height*.49f*.833f),
                LightColor = new Color("ffb264"), LightEnergy = lamp.GetProperty("energy").GetSingle() * .43f,
                OmniRange = lamp.GetProperty("radius").GetSingle() * 1.25f, OmniAttenuation = 1.0f,
                ShadowEnabled = kind == "table" || lamp.GetProperty("name").GetString()!.EndsWith("_0"),
                ShadowBias = .008f, ShadowNormalBias = .025f
            };
            AddChild(light);
            if (kind == "table") _tableLights.Add(light);
        }
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("17131a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bcc1d1"), AmbientLightEnergy = .27f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.0f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new(-38, -155, 0),
            LightColor = new Color("9ca9cc"), LightEnergy = .12f, ShadowEnabled = false });
        GD.Print("FORTRESS_HALL_B3: static sparse hall; separate Meshy fixtures; source map/scenery; no new room animation.");
    }

    private static void AddCandle(Node3D prop, string kind)
    {
        // Meshy supplies the aged body. These separate optical surfaces can later
        // animate without repainting the body or regenerating the whole fixture.
        var fire = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/static-flame.gdshader") };
        var flame = new MeshInstance3D { Name = "StationaryCandleFlame",
            Mesh = new QuadMesh { Size = new Vector2(.066f, .095f) },
            MaterialOverride = fire, Position = new Vector3(0, kind == "table" ? .255f : .455f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        prop.AddChild(flame);
        prop.AddChild(new MeshInstance3D { Name = "StaticCandleHalation",
            Mesh = new QuadMesh { Size = new Vector2(1.3f, 1.3f) }, Position = new Vector3(0, .46f, 0),
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/practical-glow.gdshader") },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (kind == "room")
            prop.AddChild(new MeshInstance3D { Name="WaxCandle", Mesh=new CylinderMesh { TopRadius=.048f, BottomRadius=.05f,Height=.23f,RadialSegments=16 },
                Position=new Vector3(0,.30f,0), MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color("d8b977"),Roughness=.9f } });
        var glass = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, .52f, .16f, .14f), Roughness = .32f, Metallic = .05f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true,
            Emission = new Color("e88523"), EmissionEnergyMultiplier = .23f };
        prop.AddChild(new MeshInstance3D { Name = "AmberInnerWindows",
            Mesh = new CylinderMesh { TopRadius = kind == "room" ? .15f : .20f,
                BottomRadius = kind == "room" ? .15f : .23f, Height = .44f,
                RadialSegments = kind == "table" ? 6 : 4, Rings = 1, CapTop = false, CapBottom = false },
            Position = new Vector3(0, .46f, 0), Rotation = new Vector3(0, kind == "room" ? Mathf.Pi / 4 : 0, 0),
            MaterialOverride = glass,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public void SetTableLanterns(Vector2 left, Vector2 right)
    {
        var uvs = new[] { left, right };
        for (var i = 0; i < uvs.Length; i++)
        {
            var p = new Vector3((uvs[i].X - .5f) * 1.8f, .003f, (uvs[i].Y - .5f) * 1.8f * 941 / 1672);
            if (i < _tableLanterns.Count) _tableLanterns[i].Position = p;
            if (i < _tableLights.Count) _tableLights[i].Position = p + new Vector3(0, .04116f, -.034286f);
        }
    }

    private static void Batch(Node3D room)
    {
        // Spatial partitions retain local light selection and culling; a single
        // hall-sized batch loses practical lights on Compatibility's light limit.
        var groups = new Dictionary<(Material, int, int), SurfaceTool>();
        foreach (var mesh in room.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>().ToArray())
        {
            if (mesh.Name.ToString().Replace("_", "").Replace(" ", "").StartsWith("Staticember"))
            { mesh.Visible = false; continue; }
            for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                var material = mesh.GetActiveMaterial(i);
                if (material is StandardMaterial3D vista && material.ResourceName == "Approved distant dusk scenery")
                {
                    vista.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    vista.EmissionEnabled = false; vista.AlbedoColor = new Color(.72f,.72f,.72f);
                }
                if (material is StandardMaterial3D standard && material.ResourceName.StartsWith("Fortress"))
                {
                    standard.AlbedoColor = material.ResourceName.Contains("floor")
                        ? new Color(.78f, .73f, .66f) : new Color(.42f, .36f, .29f);
                    standard.VertexColorUseAsAlbedo = true;
                    standard.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                    standard.Roughness = material.ResourceName.Contains("floor") ? .30f : .68f;
                    standard.NormalScale = .48f;
                }
                if (material.ResourceName == "Earthen plaster")
                    material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/earthen-plaster.gdshader") };
                var batchCentre = mesh.Transform * mesh.GetAabb().GetCenter();
                var key = (material, Mathf.FloorToInt(batchCentre.X / .75f), Mathf.FloorToInt(batchCentre.Z / .75f));
                if (!groups.TryGetValue(key, out var builder))
                {
                    builder = new SurfaceTool(); builder.Begin(Mesh.PrimitiveType.Triangles); builder.SetMaterial(material); groups.Add(key, builder);
                }
                builder.AppendFrom(mesh.Mesh, i, room.GlobalTransform.AffineInverse() * mesh.GlobalTransform);
            }
            mesh.Visible = false;
        }
        foreach (var (key, builder) in groups)
        {
            room.AddChild(new MeshInstance3D { Name = "StaticHallPartition", Mesh = builder.Commit(), MaterialOverride = key.Item1 });
            builder.Dispose();
        }
        GD.Print($"FORTRESS_BATCHES: {groups.Count}");
    }
}
