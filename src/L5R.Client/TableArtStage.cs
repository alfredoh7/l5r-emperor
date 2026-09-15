using Godot;

namespace L5R.Client;

/// <summary>Presentation-only table environment. Gameplay uses the physical war
/// room with the established card projection; isolated and legacy art reviews
/// remain available. No scene object owns match state.</summary>
public partial class TableArtStage : Node3D
{
    public bool ReviewMode { get; set; } = true;
    private const float PlateWidth = 1586f / 992f * 10f;
    private const float PlateHeight = 10;
    private Camera3D _camera = null!;
    private WarRoomStage? _warRoom;
    private string? _capture;
    private double _elapsed;
    private float _lightTime;
    public bool ReducedMotion { get; set; }
    private Node3D _environment = null!;
    private readonly List<ShaderMaterial> _paintedMaterials = [];


    public override void _Ready()
    {
        if (!ReviewMode && !OS.GetCmdlineUserArgs().Contains("--without-war-room"))
        {
            _warRoom = new WarRoomStage();
            AddChild(_warRoom);
            return;
        }
        foreach (var arg in ReviewMode ? OS.GetCmdlineUserArgs() : [])
            if (arg.StartsWith("--capture=")) _capture = arg[10..];
        var packed = GD.Load<PackedScene>(ReviewMode ? "res://Art/table-art-stage.glb" : "res://Art/navy-table-v4.glb");
        if (packed is null) { _capture = null; GetTree().Quit(1); return; }
        var environment = packed.Instantiate<Node3D>(); _environment = environment; AddChild(environment);
        ApplyPaintedMaterials(environment);
        if (!ReviewMode) AddLacquerFrame();
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("090806"),
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            TonemapExposure = 1,
            AmbientLightEnergy = 0
        }});
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Position = new Vector3(0, 10, 0), Current = true, Near = .05f, Far = 30
        };
        AddChild(_camera); _camera.LookAt(Vector3.Zero, new Vector3(0, 0, -1));
        GetViewport().SizeChanged += FitCamera;
        FitCamera();
        GD.Print($"TABLE_ART_STAGE: Blender projection mesh, complete environment plate, {(ReviewMode ? "original aspect" : "navy lantern table, table-anchored light and pan/zoom") }.");
    }

    private void ApplyPaintedMaterials(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
            for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(i) is not BaseMaterial3D source) continue;
                var texture = source.EmissionTexture ?? source.AlbedoTexture;
                if (texture is null) throw new InvalidOperationException("The Blender environment is missing its packed plate texture.");
                var shader = new Shader { Code = """
                    shader_type spatial;
                    render_mode unshaded, cull_disabled, fog_disabled;
                    uniform sampler2D plate_texture : filter_linear;
                    uniform float surface_aspect = 1.598790323;
                    uniform float candle_phase = 0.0;
                    uniform float candle_motion = 0.0;
                    // Preserve side props at their original size; widen the cloth
                    // between them instead of stretching the candle and corner trim.
                    vec2 plate_uv(vec2 uv) {
                        float expansion = surface_aspect / 1.598790323;
                        float left = 0.145 / expansion;
                        float right = 0.22 / expansion;
                        if (uv.x < left) uv.x *= expansion;
                        else if (uv.x > 1.0-right) uv.x = 1.0-(1.0-uv.x)*expansion;
                        else uv.x = 0.145 + (uv.x-left)/(1.0-left-right)*0.635;
                        return uv;
                    }
                    // Compatibility performs an approximate sRGB round trip even for
                    // unshaded meshes. Invert that known polynomial for painted scenery.
                    // See Godot drivers/gles3/shaders/tonemap_inc.glsl; exposure must be 1.
                    vec3 preserve_painted_color(vec3 c) {
                        vec3 linear_target = pow((c + vec3(0.055)) / 1.055, vec3(2.4));
                        if (!OUTPUT_IS_SRGB) {
                            return mix(c / 12.92, linear_target, step(vec3(0.04045), c));
                        }
                        vec3 x = max(c, vec3(0.03));
                        for (int i = 0; i < 6; i++) {
                            vec3 value = x * (x * (x * 0.305306011 + 0.682171111) + 0.012522878);
                            vec3 derivative = x * (x * 0.915918033 + 1.364342222) + 0.012522878;
                            x -= (value - linear_target) / derivative;
                        }
                        return x;
                    }
                    void fragment() {
                        vec2 uv = plate_uv(UV);
                        float flicker = 0.50*sin(candle_phase*2.1) + 0.29*sin(candle_phase*3.7+1.2) + 0.21*sin(candle_phase*7.6+0.5);
                        vec2 flame_distance = (uv-vec2(0.024, 0.179))/vec2(0.011, 0.024);
                        float flame = exp(-dot(flame_distance, flame_distance)*2.0);
                        uv.x += sin(candle_phase*3.1) * 0.0011 * flame * candle_motion;
                        vec3 painted = texture(plate_texture, uv).rgb;
                        vec2 distance = (uv-vec2(0.055, 0.23))/vec2(0.29, 0.38);
                        float pool = exp(-dot(distance, distance)*1.6);
                        painted += vec3(0.056, 0.026, 0.004) * pool * (0.5+flicker*0.5) * candle_motion;
                        painted += vec3(0.055, 0.036, 0.012) * flame * flicker * candle_motion;
                        ALBEDO = preserve_painted_color(clamp(painted, vec3(0.0), vec3(1.0)));
                    }
                    """ };
                var material = new ShaderMaterial { Shader = ReviewMode ? shader : GD.Load<Shader>("res://Art/navy-table.gdshader") };
                if (!ReviewMode)
                {
                    // Retain the original only for matched native fidelity captures.
                    var matPath = OS.GetCmdlineUserArgs().Contains("--mat-original-review")
                        ? "res://Art/navy-mat-v4.png" : "res://Art/navy-mat-topaz-4x.webp";
                    var mat = GD.Load<Texture2D>(matPath);
                    if (mat is null) throw new InvalidOperationException($"The playable mat texture is missing: {matPath}");
                    material.SetShaderParameter("plate_texture", mat);
                    GD.Print($"MAT_TEXTURE: {matPath} {mat.GetWidth()}x{mat.GetHeight()}; extra weave opt-in only.");
                    if (OS.GetCmdlineUserArgs().Contains("--mat-detail-preview"))
                        material.SetShaderParameter("fabric_detail_strength", .13f);
                    material.SetShaderParameter("lantern_texture", GD.Load<Texture2D>("res://Art/tavern-lantern.png"));
                    material.SetShaderParameter("weathered_texture", GD.Load<Texture2D>("res://Art/tavern-lantern-weathered.png"));
                }
                if (ReviewMode) material.SetShaderParameter("plate_texture", texture);
                mesh.SetSurfaceOverrideMaterial(i, material);
                _paintedMaterials.Add(material);
            }
        foreach (var child in node.GetChildren()) ApplyPaintedMaterials(child);
    }

    private void AddLacquerFrame()
    {
        var frame = GD.Load<PackedScene>("res://Art/LacquerFrame/lacquer-frame-a.glb").Instantiate<Node3D>();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/LacquerFrame/lacquer-frame.gdshader") };
        material.SetShaderParameter("source_art", GD.Load<Texture2D>("res://Art/LacquerFrame/approved-frame-a.png"));
        material.SetShaderParameter("lantern_texture", GD.Load<Texture2D>("res://Art/tavern-lantern.png"));
        material.SetShaderParameter("project_to_table", true);
        foreach (var mesh in frame.FindChildren("*", "MeshInstance3D").OfType<MeshInstance3D>())
            mesh.MaterialOverride = material;
        AddChild(frame);
        _paintedMaterials.Add(material);
        GD.Print("LACQUER_FRAME: approved Option A corner join; independent Blender rails, shared table projection.");
    }

    private void FitCamera()
    {
        var size = GetViewport().GetVisibleRect().Size;
        // The review preserves original proportions. Gameplay fills the screen by
        // expanding the central cloth while protecting the painted side props.
        var aspect = size.X / Math.Max(1, size.Y);
        _camera.Size = ReviewMode ? Math.Max(PlateHeight, PlateWidth / aspect) : PlateHeight;
        _environment.Scale = new Vector3(ReviewMode ? 1 : aspect / (PlateWidth / PlateHeight), 1, 1);
        foreach (var material in _paintedMaterials) material.SetShaderParameter("surface_aspect", ReviewMode ? PlateWidth / PlateHeight : aspect);
    }

    // The new plate has uninterrupted fabric inside this conservative UV inset.
    // Keep a real margin around the board as its home/army rows expand.
    private static readonly Vector2 RightLamp = new(.91f, .16f);
    private const float FabricInset = .04f;
    public const float FabricPadding = 90;
    private static Vector2 EnvironmentExtent(Vector2 boardSize)
    {
        const float aspect = 1672f / 941f;
        const float coverage = 1 - 2 * FabricInset;
        const float pileHeadroom = 32; // Raised 40-card stacks extend above the far province row.
        var width = Math.Max(Math.Max(boardSize.X * 1.30f, boardSize.Y * 1.22f * aspect),
            Math.Max((boardSize.X + 2 * FabricPadding) / coverage, (boardSize.Y + 2 * (FabricPadding + pileHeadroom)) / coverage * aspect));
        return new Vector2(width, width / aspect);
    }
    public static Rect2 FabricBounds(Vector2 boardSize)
    {
        var extent = EnvironmentExtent(boardSize);
        return new Rect2(boardSize / 2 - extent / 2 + extent * FabricInset, extent * (1 - 2 * FabricInset));
    }

    public void SetPlaneCamera(Vector2 screenCenter, float density, Vector2 center, float zoom, Vector2 boardSize, float fitZoom)
    {
        // Expand the environment around the playable area, keeping its native
        // aspect instead of stretching edge pixels into an infinite surface.
        var extent = EnvironmentExtent(boardSize);
        var origin = boardSize / 2 - extent / 2;
        // Reserve an actual gutter outside the card plane, rather than a fixed
        // fraction that can put the lamp inside Holdings on compact tables.
        var leftWorld = new Vector2(Math.Min(-160, origin.X + extent.X * .08f), origin.Y + extent.Y * .88f);
        // At the fitted view, keep the whole sprite away from the window edge.
        // Derive this from the fit camera, not the live camera: manual pan/zoom
        // must never make a lamp chase the screen edge or detach from its light.
        var fitPerspective = 1 / (1 - TableSurface.Depth * (leftWorld.Y - boardSize.Y / 2) * fitZoom);
        var fitPixelsPerWorld = fitZoom * fitPerspective * density;
        var minimumBase = 32 * density + 108 * fitPixelsPerWorld;
        var fittedX = screenCenter.X + (leftWorld.X - boardSize.X / 2) * fitPixelsPerWorld;
        if (fittedX < minimumBase) leftWorld.X += (minimumBase - fittedX) / fitPixelsPerWorld;
        var leftLamp = (leftWorld - origin) / extent;
        foreach (var material in _paintedMaterials)
        {
            material.SetShaderParameter("screen_size", GetViewport().GetVisibleRect().Size);
            material.SetShaderParameter("screen_center", screenCenter);
            material.SetShaderParameter("display_density", density);
            material.SetShaderParameter("camera_center", center);
            material.SetShaderParameter("camera_zoom", zoom);
            material.SetShaderParameter("plane_pitch", TableSurface.Pitch);
            material.SetShaderParameter("plane_depth", TableSurface.Depth);
            material.SetShaderParameter("environment_origin", origin);
            material.SetShaderParameter("environment_size", extent);
            material.SetShaderParameter("left_lamp", leftLamp);
            material.SetShaderParameter("right_lamp", RightLamp);
        }
        _warRoom?.SetTableCamera(screenCenter, density, center, zoom, boardSize, extent, leftLamp, RightLamp);
        CardPaper.Table.SetShaderParameter("light_radius", extent * new Vector2(.24f, .31f));
        CardPaper.Table.SetShaderParameter("light_origin", leftWorld);
        CardPaper.Table.SetShaderParameter("light_origin_right", origin + extent * RightLamp);
    }

    public override void _Process(double delta)
    {
        _lightTime += (float)delta;
        PresentLight(_lightTime, !ReviewMode && !ReducedMotion);
        if (_capture is null) return;
        _elapsed += delta;
        if (_elapsed < 3) return;
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
        var rendered = GetViewport().GetTexture().GetImage();
        var error = rendered.SavePng(_capture);
        GD.Print($"TABLE_ART_CAPTURE: {error}, {rendered.GetWidth()}x{rendered.GetHeight()}, {_capture}");
        _capture = null; GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void PresentLight(float time, bool animate)
    {
        foreach (var material in _paintedMaterials)
        {
            material.SetShaderParameter("candle_phase", time);
            material.SetShaderParameter("candle_motion", animate ? 1 : 0);
        }
        _warRoom?.PresentLight(time, animate);
        CardPaper.SetLight(time, animate);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (ReviewMode && @event is InputEventKey { Pressed: true, Keycode: Key.Escape }) GetTree().Quit();
    }
}
