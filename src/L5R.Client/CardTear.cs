using Godot;

namespace L5R.Client;

/// <summary>Visual-only: a card torn in half before it is removed from the game (Stone of Remembrance: "remove this card from the game and then tear it in half"). The card shivers, a jagged tear runs
/// down it, and the two halves fall apart, turning, and fade. It only draws the permitted face texture where the card stood; the engine has already removed the card.</summary>
public partial class CardTear : Control
{
    public required Vector2[] From { get; init; }
    public required Texture2D Face { get; init; }
    public double Duration { get; init; } = 1.5;
    private double _clock;
    // Where the tear crosses the card from top to bottom, as an offset from the middle of its width: deterministic, so the same card always tears the same way.
    private static readonly float[] Jag = [0f, .07f, -.06f, .08f, -.05f, .06f, -.07f, .04f, 0f];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore; ZIndex = 45; Material = CardPaper.FixedTable;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_clock >= Duration) { QueueFree(); return; }
        QueueRedraw();
    }

    private Vector2 At(float u, float v)
    {
        var top = From[0].Lerp(From[1], u); var bottom = From[3].Lerp(From[2], u);
        return top.Lerp(bottom, v);
    }

    public override void _Draw()
    {
        var t = Math.Clamp((float)(_clock / Duration), 0, 1);
        var tearing = Math.Clamp(t / .38f, 0, 1);               // the tear runs down the card
        var parting = Math.Clamp((t - .38f) / .62f, 0, 1);      // then the halves fall apart
        var ease = parting * parting * (3 - 2 * parting);
        var opacity = 1 - Math.Clamp((t - .72f) / .28f, 0, 1);
        var shiver = t < .38f ? Mathf.Sin(t * 95f) * 2.2f * (1 - tearing * .4f) : 0f;
        var segments = Jag.Length - 1;
        var down = (int)Math.Ceiling(tearing * segments);
        if (tearing < 1)
        {
            // Whole card, shivering, with the tear drawn so far.
            var shake = new Vector2(shiver, 0);
            DrawColoredPolygon(From.Select(p => p + shake).ToArray(), Colors.White, new[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, Face);
            if (down > 0)
            {
                var line = Enumerable.Range(0, down + 1).Select(i => At(.5f + Jag[i], i / (float)segments) + shake).ToArray();
                DrawPolyline(line, new Color(.12f, .06f, .04f, .9f), 2.4f, true);
            }
            return;
        }
        var seam = Enumerable.Range(0, Jag.Length).Select(i => new Vector2(.5f + Jag[i], i / (float)segments)).ToArray();
        var leftUv = new[] { new Vector2(0, 0) }.Concat(seam).Append(new Vector2(0, 1)).ToArray();
        var rightUv = new[] { new Vector2(1, 0) }.Concat(seam).Append(new Vector2(1, 1)).ToArray();
        var width = From[0].DistanceTo(From[1]);
        void Half(Vector2[] uv, float direction)
        {
            var pivot = At(direction < 0 ? .25f : .75f, .5f);
            var angle = direction * (.42f * ease);
            var drift = new Vector2(direction * width * .34f * ease, width * .5f * ease * ease);
            var points = uv.Select(q =>
            {
                var p = At(q.X, q.Y);
                var rotated = pivot + (p - pivot).Rotated(angle);
                return rotated + drift;
            }).ToArray();
            DrawColoredPolygon(points, new Color(1, 1, 1, opacity), uv, Face);
            DrawPolyline(points.Append(points[0]).ToArray(), new Color(.43f, .37f, .27f, .7f * opacity), .8f, true);
        }
        Half(leftUv, -1); Half(rightUv, 1);
    }
}
