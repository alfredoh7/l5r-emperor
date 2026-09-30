using Godot;

namespace L5R.Client;

public sealed record IntentArrow(Vector2 From, Vector2 To, Color Color, bool Assignment, Rect2 Clip, bool Attack = false, string? Label = null);

/// <summary>Only reviewed local intentions. Committed positions come from MatchView.</summary>
public partial class BoardIntentOverlay : Control
{
    public Func<IEnumerable<IntentArrow>>? Arrows { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        var badges = new List<Rect2>();
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
                    DrawSetTransform(point, (end - start + sideways * Mathf.Pi * Mathf.Cos(t * Mathf.Pi)).Angle());
                    DrawTextureRect(BattleEscalationArt.Step, new Rect2(-13, -5.5f, 26, 11), false, new Color(1, 1, 1, .94f));
                }
                // The exact derivative at t=1 keeps the tip on the final stone's axis.
                DrawSetTransform(end, AssignmentTangent(start, end, sideways).Angle());
                DrawTextureRect(BattleEscalationArt.Arrow, new Rect2(-39, -10, 39, 20), false);
                DrawSetTransform(Vector2.Zero);
                continue;
            }
            if (arrow.Attack)
            {
                AttackBrushArt.Draw(this, start, end);
                continue;
            }
            if (arrow.Label is not null)
            {
                var offset = new Vector2(-direction.Y, direction.X) * Math.Min(75, start.DistanceTo(end) * .13f);
                var points = Enumerable.Range(0, 33).Select(i => start.Lerp(end, i / 32f) + offset * Mathf.Sin(i / 32f * Mathf.Pi)).ToArray();
                DrawPolyline(points, new Color(0, 0, 0, .7f), 5, true); DrawPolyline(points, arrow.Color, 2.5f, true);
                direction = AssignmentTangent(start, end, offset);
            }
            else { DrawLine(start, end, new Color(0, 0, 0, .7f), 5, true); DrawLine(start, end, arrow.Color, 2.5f, true); }
            var normal = new Vector2(-direction.Y, direction.X);
            DrawColoredPolygon([end, end - direction * 13 + normal * 6, end - direction * 13 - normal * 6], arrow.Color);
            if (arrow.Label is { } label)
            {
                var width = Math.Max(26, Palette.Body.GetStringSize(label, fontSize: 17).X + 14);
                var center = end - direction * 32 + normal * 18;
                var radius = Math.Max(14, width / 2);
                var extent = Vector2.One * (radius + 3);
                for (var attempt = 0; attempt < 8 && badges.Any(r => r.Intersects(new Rect2(center - extent, extent * 2))); attempt++) center += normal * (radius * 2 + 8);
                badges.Add(new Rect2(center - extent, extent * 2));
                DrawCircle(center, radius + 2, new Color(0, 0, 0, .8f));
                DrawCircle(center, radius, new Color("17130e"));
                DrawArc(center, radius, 0, Mathf.Tau, 40, Palette.Gold, 2, true);
                DrawString(Palette.Body, center + new Vector2(-width / 2, 6), label, HorizontalAlignment.Center, width, 17, Palette.Gold);
            }
        }
    }
    internal static Vector2 AssignmentTangent(Vector2 start, Vector2 end, Vector2 sideways) => (end - start - sideways * Mathf.Pi).Normalized();
}
