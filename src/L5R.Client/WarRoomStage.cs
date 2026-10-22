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
    private FortressHall _hall = null!;
    private readonly List<ShaderMaterial> _paint = [];
    private float _maximumProjectionError;
    private int _projectionSamples;
    private int _offscreenProjectionSamples;
    private bool _diagnostics;
    private bool _projectionFailed;

    public override void _Ready()
    {
        _diagnostics = OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--capture="));
        _hall = GD.Load<PackedScene>("res://FortressHall.tscn").Instantiate<FortressHall>();
        AddChild(_hall);

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
        wood.SetShaderParameter("native_candlelight", false);
        foreach (var mesh in frame.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
            mesh.MaterialOverride = wood;
        AddChild(frame); _paint.Add(wood);

        _camera = new Camera3D
        {
            Current = true,
            Near = .005f,
            Far = 30,
            KeepAspect = Camera3D.KeepAspectEnum.Height
        };
        AddChild(_camera);
        GD.Print("WAR_ROOM_LIVE: B3 fortress hall; original cloth/frame; card camera and rules unchanged. Presentation-clock firelight and separate Meshy lanterns.");
    }

    public void SetTableCamera(Vector2 screenCenter, float density, Vector2 center, float zoom,
        Vector2 boardSize, Vector2 extent, Vector2 leftLamp, Vector2 rightLamp)
    {
        // A duel moves the table out of the tree for a moment; a resize then has no viewport to aim at. The hall re-aims when its size next changes (TableArtStage).
        if (!IsInsideTree()) return;
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
        _hall.SetTableLanterns(leftLamp, rightLamp);
        if (_diagnostics) CheckProjection(screenCenter, density, center, zoom, boardSize, units);
    }

    private void CheckProjection(Vector2 screenCenter, float density, Vector2 center, float zoom, Vector2 boardSize, float units)
    {
        // Zoom can put the far-off board corners arbitrarily close to/behind
        // the eye plane. Absolute pixel error there is not a visible alignment
        // measurement. Sample a denser grid across the actual rendered surface;
        // native card-edge input probes independently check the hit mapping.
        var visible = GetViewport().GetVisibleRect().Grow(16);
        for (var row = 0; row <= 4; row++)
            for (var col = 0; col <= 4; col++)
            {
                var uv = new Vector2(col / 4f, row / 4f);
                var world = uv * boardSize;
                var p = (world - center) * zoom;
                var depth = 1 - TableSurface.Depth * p.Y;
                var expected = screenCenter + new Vector2(p.X, p.Y * TableSurface.Pitch) * density / depth;
                if (depth <= 0 || !expected.IsFinite() || !visible.HasPoint(expected))
                { _offscreenProjectionSamples++; continue; }
                var actual = _camera.UnprojectPosition(new((world.X - boardSize.X / 2) / units, 0, (world.Y - boardSize.Y / 2) / units));
                var error = expected.DistanceTo(actual);
                _maximumProjectionError = Math.Max(_maximumProjectionError, error); _projectionSamples++;
                if (error > .1f)
                {
                    _projectionFailed = true;
                    throw new InvalidOperationException($"War room and card projections differ by {error:F4} px; expected={expected}, actual={actual}, depth={1 - TableSurface.Depth * p.Y}, zoom={zoom}, viewport={GetViewport().GetVisibleRect().Size}.");
                }
            }
    }

    public void PresentLight(float time, bool animate)
    {
        _hall.PresentLight(time, animate);
        foreach (var material in _paint)
        {
            material.SetShaderParameter(ShaderParameters.CandlePhase, time);
            material.SetShaderParameter(ShaderParameters.CandleMotion, animate ? 1 : 0);
        }
    }

    public override void _ExitTree()
    {
        if (_diagnostics) GD.Print($"WAR_ROOM_PROJECTION_{(_projectionFailed ? "FAIL" : "PASS")}: {_projectionSamples} visible samples, max {_maximumProjectionError:F6} px; {_offscreenProjectionSamples} offscreen/behind-eye samples excluded.");
    }
}
