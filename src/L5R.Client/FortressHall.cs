using Godot;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Approved B3 scenery and presentation-clock firelight, with no match/state access.</summary>
public partial class FortressHall : Node3D
{
    private readonly List<Node3D> _tableLanterns = [];
    private readonly List<OmniLight3D> _tableLights = [];
    private sealed class Practical
    {
        public OmniLight3D Light = null!;
        public float BaseEnergy, Phase, Rate = 1, Strength = 1;
        public readonly List<ShaderMaterial> Flames = [], Halos = [];
        public readonly List<StandardMaterial3D> Windows = [];
    }
    private readonly List<Practical> _practicals = [];
    private ShaderMaterial? _bakedFloor;
    private readonly Vector4[] _floorSources = new Vector4[11];
    private readonly float[] _floorEnergy = new float[11];
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
                            if (kind == "brazier")
                            {
                                worn.AlbedoColor = new Color(.90f,.85f,.80f);
                                worn.Metallic = .55f; worn.MetallicSpecular = .24f;
                                // Preserve Meshy's iron artwork and normals. Its packed
                                // roughness has near-zero pits that sparkle under firelight.
                                worn.RoughnessTexture = null; worn.Roughness = .82f;
                                worn.NormalScale = .28f;
                            }
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
            // Table phases match the existing cloth, frame and card candlelight.
            var practical = new Practical { Phase = kind == "table" ? _tableLanterns.Count * 1.8f : 2.7f + _practicals.Count * 2.31f,
                Rate = kind == "ember" ? 1.27f : 1f, Strength = kind == "room" ? .68f : 1f };
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
                    AddCandle(prop, kind, practical);
                    if (kind == "table") _tableLanterns.Add(prop);
                }
            }
            if (kind == "ember") AddBrazierFire(position, practical);
            var light = new OmniLight3D
            {
                Name = "PracticalLight", Position = position + new Vector3(0,height*.49f,-height*.49f*.833f),
                LightColor = new Color("ffb264"), LightSpecular = .55f, LightEnergy = lamp.GetProperty("energy").GetSingle() * .43f,
                OmniRange = lamp.GetProperty("radius").GetSingle() * 1.25f, OmniAttenuation = 1.0f,
                ShadowEnabled = kind is "table" or "ember" || lamp.GetProperty("name").GetString()!.EndsWith("_0"),
                ShadowBias = kind == "ember" ? .002f : .008f,
                ShadowNormalBias = kind == "ember" ? .003f : .025f,
                ShadowOpacity = kind == "ember" ? .68f : 1f,
                ShadowBlur = kind == "ember" ? 2f : 1f
            };
            AddChild(light);
            practical.Light = light; practical.BaseEnergy = light.LightEnergy; _practicals.Add(practical);
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
        PresentLight(0, false);
        GD.Print("FORTRESS_HALL_B3: sparse hall; separate Meshy fixtures; source map/scenery; synchronized firelight with reduced-motion support.");
    }

    private static void AddCandle(Node3D prop, string kind, Practical practical)
    {
        // Meshy supplies the aged body; flame, halo and window emission share
        // one light signal without repainting or moving the body.
        var fire = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/static-flame.gdshader") };
        practical.Flames.Add(fire);
        var flame = new MeshInstance3D { Name = "CandleFlame",
            Mesh = new QuadMesh { Size = new Vector2(.066f, .095f) },
            MaterialOverride = fire, Position = new Vector3(0, kind == "table" ? .255f : .455f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        prop.AddChild(flame);
        var halo = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/practical-glow.gdshader") };
        practical.Halos.Add(halo);
        prop.AddChild(new MeshInstance3D { Name = "CandleHalation",
            Mesh = new QuadMesh { Size = new Vector2(1.3f, 1.3f) }, Position = new Vector3(0, .46f, 0),
            MaterialOverride = halo,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (kind == "room")
        {
            // Seat the candle in the existing Meshy socket, covering the residual
            // generated bulb stem. The wick anchors the separate flame visually.
            var wax = new StandardMaterial3D { AlbedoColor = new Color("d8b977"), Roughness = .95f, MetallicSpecular = .12f };
            prop.AddChild(new MeshInstance3D { Name="WaxCandle", Mesh=new CylinderMesh { TopRadius=.048f, BottomRadius=.054f,Height=.315f,RadialSegments=32 },
                Position=new Vector3(0,.275f,0), MaterialOverride=wax });
            prop.AddChild(new MeshInstance3D { Name="CharredWick", Mesh=new CylinderMesh { TopRadius=.003f, BottomRadius=.004f,Height=.023f,RadialSegments=8 },
                Position=new Vector3(0,.441f,0), Rotation=new Vector3(0,0,-.13f),
                MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color("1b100b"),Roughness=1 } });
            prop.AddChild(new MeshInstance3D { Name="CandleSocket", Mesh=new CylinderMesh { TopRadius=.062f, BottomRadius=.068f,Height=.018f,RadialSegments=24 },
                Position=new Vector3(0,.120f,0), MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color("594126"),Metallic=.65f,Roughness=.65f } });
        }
        var glass = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, .52f, .16f, .14f), Roughness = .32f, Metallic = .05f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, EmissionEnabled = true,
            Emission = new Color("e88523"), EmissionEnergyMultiplier = .23f };
        practical.Windows.Add(glass);
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

    public void PresentLight(float time, bool animate)
    {
        for (var i = 0; i < _practicals.Count; i++)
        {
            var p = _practicals[i];
            var phase = animate ? time * p.Rate + p.Phase : p.Phase;
            var energy = animate ? 1 + (CandleEnergy(phase) - 1) * p.Strength : 1;
            p.Light.LightEnergy = p.BaseEnergy * energy;
            foreach (var flame in p.Flames)
            {
                flame.SetShaderParameter("phase", phase);
                flame.SetShaderParameter("motion", animate ? 1f : 0f);
                flame.SetShaderParameter("light_energy", energy);
            }
            foreach (var halo in p.Halos) halo.SetShaderParameter("light_energy", energy);
            foreach (var glass in p.Windows) glass.EmissionEnergyMultiplier = .23f * energy;
            var position = p.Light.Position;
            _floorSources[i] = new Vector4(position.X, position.Z, p.Light.OmniRange, 0);
            _floorEnergy[i] = energy;
        }
        _bakedFloor?.SetShaderParameter("practical_sources", _floorSources);
        _bakedFloor?.SetShaderParameter("practical_energy", _floorEnergy);
    }

    // Same smooth signal as Art/candlelight.gdshaderinc, so actual lights follow
    // the existing mat/card treatment. Reduced motion bypasses every time input.
    private static float CandleEnergy(float t)
    {
        var cell = Mathf.Floor(t * 5.3f);
        var blend = Mathf.SmoothStep(0, 1, t * 5.3f - cell);
        static float Noise(float n) { var v = Mathf.Sin(n * 127.1f + 4.7f) * 43758.5453f; return v - Mathf.Floor(v); }
        return 1 + .13f * Mathf.Sin(t * 1.65f) + .075f * Mathf.Sin(t * 3.8f + 1.2f)
            + .035f * Mathf.Sin(t * 7.1f + .4f) + .055f * (Mathf.Lerp(Noise(cell), Noise(cell + 1), blend) - .5f);
    }

    private void Batch(Node3D room)
    {
        // Spatial partitions retain local light selection and culling; a single
        // hall-sized batch loses practical lights on Compatibility's light limit.
        var groups = new Dictionary<(Material, int, int), SurfaceTool>();
        ShaderMaterial? bakedFloor = null;
        if (ResourceLoader.Exists("res://Art/WarRoom/floor-irradiance-m6.exr") && !OS.GetCmdlineUserArgs().Contains("--room-no-baked-floor"))
        {
            bakedFloor = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/floor-lighting-m4.gdshader") };
            bakedFloor.SetShaderParameter("wood_colour", GD.Load<Texture2D>("res://Art/WarRoom/fortress-hall-b3_weathered_brown_planks-albedo.jpg"));
            bakedFloor.SetShaderParameter("wood_normal", GD.Load<Texture2D>("res://Art/WarRoom/fortress-hall-b3_weathered_brown_planks-normal.jpg"));
            bakedFloor.SetShaderParameter("wood_roughness", GD.Load<Texture2D>("res://Art/WarRoom/floor-roughness-m4.jpg"));
            bakedFloor.SetShaderParameter("irradiance", GD.Load<Texture2D>("res://Art/WarRoom/floor-irradiance-m6.exr"));
            _bakedFloor = bakedFloor;
        }
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
                    vista.EmissionEnabled = false; vista.AlbedoColor = new Color(.79f,.80f,.83f);
                    vista.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
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
                if (material.ResourceName == "Fortress polished worn floor" && bakedFloor is not null)
                    material = bakedFloor;
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
