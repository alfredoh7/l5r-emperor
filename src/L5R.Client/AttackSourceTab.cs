using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A temporary, read-only card fitting. A persistent overlay allows it to retract
/// even when the action panel/cards are rebuilt. It never intercepts card input.</summary>
public partial class AttackSourceTab : Control
{
    public Action? Observe { get; set; }
    public Func<AttackCueView, Rect2?>? Placement { get; set; }
    public bool ReducedMotion { get; set; }
    public AttackCueView? Desired { get; set; }
    public AttackCueView? Current { get; private set; }
    public float Progress { get; private set; }
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
    { Desired = Current = null; Progress = 0; Hide(); }

    public override void _Process(double delta)
    {
        Observe?.Invoke();
        if (ReducedMotion) { Current = Desired; Progress = Current is null ? 0 : 1; }
        else
        {
            if (Current is null) Current = Desired;
            var showing = Current is not null && Current == Desired;
            Progress = Mathf.MoveToward(Progress, showing ? 1 : 0, (float)delta / .18f);
            if (Progress == 0) Current = Desired;
        }
        if (Current is null || Progress <= 0) { Hide(); return; }
        // Follow the physical card during camera movement/bowing. Keep the last mount only
        // for the short exit when a resolved attack removes its source from the board.
        if (Placement?.Invoke(Current) is { } body) _lastBody = body;
        else if (Desired is not null) { Hide(); return; }
        if (_lastBody.Size == Vector2.Zero) { Hide(); return; }
        Position = _lastBody.Position; Size = _lastBody.Size;
        _sprite.Texture = Current.Kind == "Ranged" ? _ranged : _melee;
        _sprite.Size = Size;
        var eased = Progress * Progress * (3 - 2 * Progress);
        _sprite.Position = new Vector2(-Size.X * (1 - eased), 0);
        Show();
    }
}
