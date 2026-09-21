using Godot;

namespace L5R.Client;

public sealed record IntentArrow(Vector2 From, Vector2 To, Color Color, bool Assignment, Rect2 Clip, bool Attack = false);

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
            if (arrow.Assignment)
            {
                var length = start.DistanceTo(end);
                var sideways = new Vector2(-direction.Y, direction.X) * Math.Min(36, length * .08f);
                Vector2 Curve(float t) => start.Lerp(end, t) + sideways * Mathf.Sin(t * Mathf.Pi);
                var count = Math.Max(3, (int)(length / 35));
                for (var i = 1; i < count; i++)
                {
                    var t = i / (float)count; var point = Curve(t);
                    if (point.DistanceTo(end) < 38) continue;
                    DrawSetTransform(point, (end-start + sideways * Mathf.Pi * Mathf.Cos(t*Mathf.Pi)).Angle());
                    DrawTextureRect(BattleEscalationArt.Step, new Rect2(-13, -5.5f, 26, 11), false, new Color(1, 1, 1, .94f));
                }
                // The exact derivative at t=1 keeps the tip on the final stone's axis.
                DrawSetTransform(end, AssignmentTangent(start, end, sideways).Angle());
                DrawTextureRect(BattleEscalationArt.Arrow, new Rect2(-39, -10, 39, 20), false);
                DrawSetTransform(Vector2.Zero);
                continue;
            }
            if(arrow.Attack)
            {
                AttackBrushArt.Draw(this,start,end);
                continue;
            }
            DrawLine(start, end, new Color(0, 0, 0, .7f), 5, true); DrawLine(start, end, arrow.Color, 2.5f, true);
            var normal = new Vector2(-direction.Y, direction.X);
            DrawColoredPolygon([end, end - direction * 13 + normal * 6, end - direction * 13 - normal * 6], arrow.Color);
        }
    }
    internal static Vector2 AssignmentTangent(Vector2 start, Vector2 end, Vector2 sideways) => (end-start-sideways*Mathf.Pi).Normalized();
}
