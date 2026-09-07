using Godot;

namespace L5R.Client;

public sealed record IntentArrow(Vector2 From, Vector2 To, Color Color, bool Assignment, Rect2 Clip);

/// <summary>Only reviewed local intentions. Committed positions come from MatchView.</summary>
public partial class BoardIntentOverlay : Control
{
    public Func<IEnumerable<IntentArrow>>? Arrows { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        foreach (var arrow in Arrows?.Invoke() ?? [])
        {
            if (!arrow.Clip.HasPoint(arrow.From) || !arrow.Clip.HasPoint(arrow.To)) continue;
            var inverse = GetGlobalTransform().AffineInverse();
            var start = inverse * arrow.From;
            var end = inverse * arrow.To;
            var direction = (end - start).Normalized();
            if (start.DistanceTo(end) < 30) continue;
            start += direction * 20; end -= direction * 15;
            DrawLine(start, end, new Color(0, 0, 0, .7f), 5, true);
            if (arrow.Assignment) DrawDashedLine(start, end, arrow.Color, 2.5f, 10, true);
            else DrawLine(start, end, arrow.Color, 2.5f, true);
            var normal = new Vector2(-direction.Y, direction.X);
            DrawColoredPolygon([end, end - direction * 13 + normal * 6, end - direction * 13 - normal * 6], arrow.Color);
        }
    }
}
