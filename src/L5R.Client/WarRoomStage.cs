using Godot;

namespace L5R.Client;

/// <summary>
/// Physical scenery matched to TableSurface's projective card plane. The room
/// receives camera/presentation data only; it never reads or changes a match.
/// </summary>
public partial class WarRoomStage : Node3D
{
    private const float MatWidth = 1.8f;
    private const float MatDepth = MatWidth * 941f / 1672f;
    private Camera3D _camera = null!;
    private readonly List<ShaderMaterial> _paint = [];
    private readonly List<(OmniLight3D Light, float Energy, float Phase)> _lights = [];
    private OmniLight3D _nearLight = null!, _farLight = null!;
    private readonly List<ShaderMaterial> _lanterns = [];
    private float _maximumProjectionError;
    private int _projectionSamples;
    private bool _diagnostics;

    public override void _Ready()
    {
        _diagnostics = OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--capture="));
        var room = GD.Load<PackedScene>("res://Art/WarRoom/war-room-m1.glb").Instantiate<Node3D>();
        AddChild(room);
        BatchArchitecture(room);

        var cloth = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/cloth.gdshader") };
        var matPath = OS.GetCmdlineUserArgs().Contains("--mat-original-review")
            ? "res://Art/navy-mat-v4.png" : "res://Art/navy-mat-topaz-4x.webp";
        var texture = GD.Load<Texture2D>(matPath);
        cloth.SetShaderParameter("plate_texture", texture);
        GD.Print($"MAT_TEXTURE: {matPath} {texture.GetWidth()}x{texture.GetHeight()}; extra weave opt-in only.");
        if (OS.GetCmdlineUserArgs().Contains("--mat-detail-preview"))
            cloth.SetShaderParameter("fabric_detail_strength", .13f);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(MatWidth, MatDepth) },
            MaterialOverride = cloth,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        _paint.Add(cloth);
        var frame = GD.Load<PackedScene>("res://Art/LacquerFrame/lacquer-frame-a.glb").Instantiate<Node3D>();
        var wood = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/LacquerFrame/lacquer-frame.gdshader") };
        wood.SetShaderParameter("source_art", GD.Load<Texture2D>("res://Art/LacquerFrame/approved-frame-a.png"));
        wood.SetShaderParameter("native_candlelight", true);
        foreach (var mesh in frame.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
            mesh.MaterialOverride = wood;
        AddChild(frame); _paint.Add(wood);

        AddLantern(0); AddLantern(1.8f);
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("100e0b"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("b0b9c6"), AmbientLightEnergy = .20f,
            TonemapMode = Godot.Environment.ToneMapper.Linear, TonemapExposure = 1
        }});
        AddLamp(new(-1.59f, .10f, -1.28f), "ffc385", .80f, 1.6f, .4f);
        AddLamp(new(1.57f, .12f, -1.46f), "ffc385", .80f, 1.6f, 2.7f);
        AddLamp(new(0, .15f, -3), "ffad57", .48f, .95f, 4.2f);
        _nearLight = AddLamp(new(-.83f, .15f, .42f), "ffac60", .70f, .70f, 0);
        _farLight = AddLamp(new(.78f, .15f, -.41f), "ffac60", .65f, .68f, 1.8f);
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new(-68, -22, 0), LightColor = new Color("b4bdcc"),
            LightEnergy = .13f, ShadowEnabled = true
        });
        _camera = new Camera3D { Current = true, Near = .005f, Far = 30,
            KeepAspect = Camera3D.KeepAspectEnum.Height };
        AddChild(_camera);
        GD.Print("WAR_ROOM_LIVE: enlarged Blender room; original cloth/frame; card camera and rules unchanged. Furniture/material study, Meshy props pending.");
    }

    private void BatchArchitecture(Node3D room)
    {
        // Keep the editable Blender source and prop anchors, but submit static
        // architecture once per material instead of one draw per lattice slat.
        var groups = new Dictionary<Material, SurfaceTool>();
        var meshes = room.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>().ToArray();
        foreach (var mesh in meshes)
        {
            for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                var material = mesh.GetActiveMaterial(i);
                if (!groups.TryGetValue(material, out var builder))
                {
                    builder = new SurfaceTool(); builder.Begin(Mesh.PrimitiveType.Triangles);
                    builder.SetMaterial(material); groups.Add(material, builder);
                }
                builder.AppendFrom(mesh.Mesh, i, room.GlobalTransform.AffineInverse() * mesh.GlobalTransform);
            }
            mesh.Visible = false;
        }
        foreach (var (material, builder) in groups)
        {
            room.AddChild(new MeshInstance3D { Name = "BatchedArchitecture", Mesh = builder.Commit(), MaterialOverride = material });
            builder.Dispose();
        }
        GD.Print($"WAR_ROOM_BATCH: {meshes.Length} source meshes -> {groups.Count} material batches; source geometry/UVs retained.");
    }

    private void AddLantern(float phase)
    {
        // Preserve the approved upright cutouts until the individual Meshy prop
        // milestone. Project their bases with the same mapping as the cards.
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/WarRoom/lantern.gdshader") };
        material.SetShaderParameter("lantern_texture", GD.Load<Texture2D>("res://Art/tavern-lantern.png"));
        material.SetShaderParameter("weathered_texture", GD.Load<Texture2D>("res://Art/tavern-lantern-weathered.png"));
        material.SetShaderParameter("phase_offset", phase);
        material.RenderPriority = 10;
        AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = material,
            ExtraCullMargin = 100, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _lanterns.Add(material); _paint.Add(material);
    }

    private OmniLight3D AddLamp(Vector3 position, string color, float energy, float radius, float phase)
    {
        var light = new OmniLight3D { Position = position, LightColor = new Color(color), LightEnergy = energy,
            OmniRange = radius, OmniAttenuation = 1, ShadowEnabled = true, ShadowBias = .025f };
        AddChild(light); _lights.Add((light, energy, phase)); return light;
    }

    public void SetTableCamera(Vector2 screenCenter, float density, Vector2 center, float zoom,
        Vector2 boardSize, Vector2 extent, Vector2 leftLamp, Vector2 rightLamp)
    {
        var size = GetViewport().GetVisibleRect().Size;
        var units = extent.X / MatWidth;
        var cosine = Mathf.Sqrt(1 - TableSurface.Pitch * TableSurface.Pitch);
        var distance = cosine / (TableSurface.Depth * units * zoom);
        var target = new Vector3((center.X - boardSize.X / 2) / units, 0, (center.Y - boardSize.Y / 2) / units);
        _camera.Position = target + new Vector3(0, distance * TableSurface.Pitch, distance * cosine);
        _camera.LookAt(target);
        // An off-axis frustum follows the board viewport (which excludes hand and
        // sidebar) without changing the user's pan, zoom, fit or input inverse.
        var focalPixels = cosine * density / TableSurface.Depth;
        var nearPerPixel = _camera.Near / focalPixels;
        _camera.SetFrustum(size.Y * nearPerPixel,
            new Vector2(size.X / 2 - screenCenter.X, screenCenter.Y - size.Y / 2) * nearPerPixel,
            _camera.Near, _camera.Far);
        foreach (var material in _paint)
        {
            material.SetShaderParameter("screen_size", size);
            material.SetShaderParameter("screen_center", screenCenter);
            material.SetShaderParameter("display_density", density);
            material.SetShaderParameter("camera_center", center);
            material.SetShaderParameter("camera_zoom", zoom);
            material.SetShaderParameter("environment_origin", boardSize / 2 - extent / 2);
            material.SetShaderParameter("environment_size", extent);
            material.SetShaderParameter("left_lamp", leftLamp);
            material.SetShaderParameter("right_lamp", rightLamp);
        }
        _lanterns[0].SetShaderParameter("source_uv", leftLamp);
        _lanterns[1].SetShaderParameter("source_uv", rightLamp);
        _nearLight.Position = new((leftLamp.X - .5f) * MatWidth, .15f, (leftLamp.Y - .5f) * MatDepth);
        _farLight.Position = new((rightLamp.X - .5f) * MatWidth, .15f, (rightLamp.Y - .5f) * MatDepth);
        if (_diagnostics) CheckProjection(screenCenter, density, center, zoom, boardSize, units);
    }

    private void CheckProjection(Vector2 screenCenter, float density, Vector2 center, float zoom, Vector2 boardSize, float units)
    {
        foreach (var uv in new[] { Vector2.Zero, Vector2.One, new Vector2(1, 0), new Vector2(0, 1), new Vector2(.5f, .5f) })
        {
            var world = uv * boardSize;
            var p = (world - center) * zoom;
            var expected = screenCenter + new Vector2(p.X, p.Y * TableSurface.Pitch) * density / (1 - TableSurface.Depth * p.Y);
            var actual = _camera.UnprojectPosition(new((world.X - boardSize.X / 2) / units, 0, (world.Y - boardSize.Y / 2) / units));
            var error = expected.DistanceTo(actual);
            _maximumProjectionError = Math.Max(_maximumProjectionError, error); _projectionSamples++;
            if (error > .1f) throw new InvalidOperationException($"War room and card projections differ by {error:F4} px.");
        }
    }

    public void PresentLight(float time, bool animate)
    {
        foreach (var material in _paint)
        {
            material.SetShaderParameter("candle_phase", time);
            material.SetShaderParameter("candle_motion", animate ? 1 : 0);
        }
        foreach (var (light, energy, phase) in _lights)
        {
            var t = time + phase;
            var flicker = .50f * Mathf.Sin(t * 2.1f) + .29f * Mathf.Sin(t * 3.7f + 1.2f) + .21f * Mathf.Sin(t * 7.6f + .5f);
            light.LightEnergy = energy * (animate ? 1 + flicker * .065f : 1);
        }
    }

    public override void _ExitTree()
    {
        if (_diagnostics) GD.Print($"WAR_ROOM_PROJECTION_PASS: {_projectionSamples} samples, max {_maximumProjectionError:F6} px.");
    }
}
