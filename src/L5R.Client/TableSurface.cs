using Godot;

namespace L5R.Client;

/// <summary>One perspective-correct table plane. Native controls render flat into
/// an isolated viewport; the same inverse projection routes mouse input to them.</summary>
public partial class TableSurface : TextureRect
{
    public const float Pitch = .64f, Depth = .00065f;
    // Optional presentation camera. The normal game table keeps its original defaults.
    public float PlanePitch { get; set; } = Pitch;
    public float PlaneDepth { get; set; } = Depth;
    // Fixed presentation scenes may bound their content more tightly than the pannable board.
    public Vector2 CanvasExtent { get; set; } = new(1.7f, 2.7f);
    public SubViewport Canvas { get; private set; } = null!;
    public Vector2 CanvasSize { get; private set; }
    public float RenderScale { get; private set; } = 1;
    private ShaderMaterial _projection = null!;
    private bool _mouseInside;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Linear;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        Canvas = new SubViewport { TransparentBg = true, Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = true, GuiSnapControlsToPixels = false };
        AddChild(Canvas);
        Texture = Canvas.GetTexture();
        Material = _projection = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/table-perspective.gdshader") };
    }
    public void ResizePlane(Vector2 size, float density)
    {
        Size = size; CanvasSize = size * CanvasExtent;
        RenderScale = Math.Min(density, 8192 / Math.Max(CanvasSize.X, CanvasSize.Y));
        Canvas.Size = (Vector2I)(CanvasSize * RenderScale).Ceil();
        CanvasSize = (Vector2)Canvas.Size / RenderScale;
        Canvas.CanvasTransform = Transform2D.Identity.Scaled(Vector2.One * RenderScale);
        _projection.SetShaderParameter("display_size", Size);
        _projection.SetShaderParameter("canvas_size", CanvasSize);
        _projection.SetShaderParameter("pitch", PlanePitch);
        _projection.SetShaderParameter("depth", PlaneDepth);
    }
    public static Vector2 ProjectOffset(Vector2 plane) => ProjectOffset(plane, Pitch, Depth);
    public static Vector2 ProjectOffset(Vector2 plane, float pitch, float depth)
    {
        var divisor = 1 - plane.Y * depth;
        return new Vector2(plane.X, plane.Y * pitch) / divisor;
    }
    public static Vector2 UnprojectOffset(Vector2 screen) => UnprojectOffset(screen, Pitch, Depth);
    public static Vector2 UnprojectOffset(Vector2 screen, float pitch, float depth)
    {
        var y = screen.Y / (pitch + depth * screen.Y);
        return new Vector2(screen.X * (1 - depth * y), y);
    }
    public Vector2 ProjectCanvas(Vector2 plane) => GetGlobalTransform() * (Size / 2 + ProjectOffset(plane - CanvasSize / 2, PlanePitch, PlaneDepth));
    public Vector2 ScreenToCanvas(Vector2 screen) => CanvasSize / 2 + UnprojectOffset(GetGlobalTransform().AffineInverse() * screen - Size / 2, PlanePitch, PlaneDepth);
    public void ForwardMouse(InputEventMouse mouse)
    {
        var forwarded = (InputEventMouse)mouse.Duplicate();
        var point = ScreenToCanvas(mouse.Position) * RenderScale;
        forwarded.Position = point; forwarded.GlobalPosition = point;
        if (forwarded is InputEventMouseMotion motion && mouse is InputEventMouseMotion original)
            motion.Relative = point - ScreenToCanvas(mouse.Position - original.Relative) * RenderScale;
        if (!_mouseInside) { Canvas.NotifyMouseEntered(); _mouseInside = true; }
        Canvas.PushInput(forwarded, true);
    }
    public void ExitMouse()
    {
        if (!_mouseInside) return;
        Canvas.NotifyMouseExited(); _mouseInside = false;
    }

    public void ReleaseMouseOutside(InputEventMouseButton mouse)
    {
        // Finish a table press released over fixed HUD/outside the table without
        // re-entering the projection and activating a covered card or button.
        ExitMouse();
        var released = (InputEventMouseButton)mouse.Duplicate();
        released.Position = released.GlobalPosition = new Vector2(-1, -1);
        Canvas.PushInput(released, true);
    }

}
