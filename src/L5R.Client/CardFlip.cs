using Godot;

namespace L5R.Client;

/// <summary>Visual-only: a face-down Dynasty card turned face-up where it lies on the table ("Flip Dynasty Cards"). It is created covering the card with its back (so the
/// face is not given away while earlier cards turn), and when <see cref="Begin"/> is called the card turns about its vertical centre line, lifting slightly, then lingers
/// face-up before calling <see cref="Done"/>. It only draws permitted textures; the engine has already revealed the card.</summary>
public partial class CardFlip : Control
{
    public required Vector2[] From { get; init; }
    public required Texture2D Face { get; init; }
    public required Texture2D Back { get; init; }
    public Action? Done { get; set; }
    public double Turn { get; init; } = .5;
    public double Linger { get; init; } = .45;
    private double _clock = -1; // below zero: still covering the card, not yet turning

    public void Begin() { if (_clock < 0) _clock = 0; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 45; Material = CardPaper.FixedTable;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    public override void _Process(double delta)
    {
        if (_clock < 0) return;
        _clock += delta; QueueRedraw();
        if (_clock >= Turn + Linger) { var done = Done; Done = null; done?.Invoke(); }
    }

    private Vector2 At(float u, float v)
    {
        var top = From[0].Lerp(From[1], u); var bottom = From[3].Lerp(From[2], u);
        return top.Lerp(bottom, v);
    }

    public override void _Draw()
    {
        var t = _clock < 0 ? 0f : Math.Clamp((float)(_clock / Turn), 0, 1);
        var angle = t * Mathf.Pi;
        var width = Mathf.Abs(Mathf.Cos(angle));
        var lift = Mathf.Sin(angle) * .07f;
        var corners = new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) }
            .Select(c => At(.5f + (c.Item1 - .5f) * width * (1 + lift), .5f + (c.Item2 - .5f) * (1 + lift))).ToArray();
        var shade = .55f + .45f * width; // edge-on, the card is in its own shadow
        DrawColoredPolygon(corners, new Color(shade, shade, shade, 1), new[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, t < .5f ? Back : Face);
    }
}
