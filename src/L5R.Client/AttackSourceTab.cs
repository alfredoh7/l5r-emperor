using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A temporary, read-only card fitting. A persistent overlay allows it to retract
/// even when the action panel/cards are rebuilt. It never intercepts card input.</summary>
public partial class AttackSourceTab : Control
{
    public Action? Observe { get; set; }
    public Func<AttackCueView, Rect2?>? Placement { get; set; }
    public Action<AttackCueView?, Rect2, float>? Arrange { get; set; }
    public bool ReducedMotion { get; set; }
    public AttackCueView? Desired { get; set; }
    public AttackCueView? Current { get; private set; }
    public float Progress { get; private set; }
    public float ClearanceProgress { get; private set; }
    private const float ClearanceSeconds = .16f, TabSeconds = .18f;
    private float _timeline;
    private static Texture2D? _atlas, _ranged, _melee;
    private TextureRect _sprite = null!;
    private Rect2 _lastBody;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ClipContents = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _atlas ??= GD.Load<Texture2D>("res://Art/AttackTargets/source-tabs-atlas.png");
        _ranged ??= new AtlasTexture { Atlas = _atlas, Region = new Rect2(191,305,562,396), FilterClip = true };
        _melee ??= new AtlasTexture { Atlas = _atlas, Region = new Rect2(858,305,564,396), FilterClip = true };
        _sprite = new TextureRect { MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(_sprite); Hide();
    }

    public void Cancel()
    {
        Desired = Current = null; _timeline = Progress = ClearanceProgress = 0;
        Arrange?.Invoke(null, default, 0); Hide();
    }

    public override void _Process(double delta)
    {
        Observe?.Invoke();
        if (ReducedMotion)
        { Current = Desired; _timeline = Current is null ? 0 : ClearanceSeconds + TabSeconds; }
        else
        {
            if (Current is null) Current = Desired;
            var showing = Current is not null && Current == Desired;
            // A different kind from the same card can reuse the reserved slot.
            var swapInPlace = Current is not null && Desired is not null && Current.SourceId == Desired.SourceId;
            var destination = showing ? ClearanceSeconds + TabSeconds : swapInPlace ? ClearanceSeconds : 0;
            _timeline = Mathf.MoveToward(_timeline, destination, (float)delta);
            if (!showing && _timeline == destination) Current = Desired;
        }
        ClearanceProgress = Mathf.Clamp(_timeline / ClearanceSeconds, 0, 1);
        Progress = Mathf.Clamp((_timeline - ClearanceSeconds) / TabSeconds, 0, 1);
        if (Current is null)
        { Arrange?.Invoke(null, default, 0); Hide(); return; }
        // Follow the physical card during camera movement/bowing. Keep the last mount only
        // for the short exit when a resolved attack removes its source from the board.
        if (Placement?.Invoke(Current) is { } body) _lastBody = body;
        else if (Desired is not null) { Arrange?.Invoke(null, default, 0); Hide(); return; }
        if (_lastBody.Size == Vector2.Zero) { Hide(); return; }
        Arrange?.Invoke(Current, _lastBody, Ease(ClearanceProgress));
        if (Progress <= 0) { Hide(); return; }
        Position = _lastBody.Position; Size = _lastBody.Size;
        _sprite.Texture = Current.Kind == "Ranged" ? _ranged : _melee;
        _sprite.Size = Size;
        var eased = Ease(Progress);
        _sprite.Position = new Vector2(-Size.X * (1 - eased), 0);
        Show();
    }
    private static float Ease(float value) => value * value * (3 - 2 * value);
}
