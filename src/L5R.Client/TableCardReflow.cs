using Godot;

namespace L5R.Client;

/// <summary>Slides a live card or pile in its table plane, keeping its native visuals intact.</summary>
public partial class TableCardReflow : Node
{
    public required Control Card { get; init; }
    public required Vector2 From { get; init; }
    public required Vector2 To { get; init; }
    public Func<bool> ReducedMotion { get; init; } = () => false;
    private float _progress;
    public override void _Ready() => Card.Position = From;
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Card)) { QueueFree(); return; }
        _progress = ReducedMotion() ? 1 : Math.Min(1, _progress + (float)Math.Min(delta, .1) / .76f);
        var t = _progress;
        var ease = t * t * t * (t * (t * 6 - 15) + 10);
        Card.Position = From.Lerp(To, ease);
        if (t >= 1) QueueFree();
    }
}
