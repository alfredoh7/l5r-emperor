using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of public pile counts. No deck identities enter this control.</summary>
public partial class TablePile : TooltipButton
{
    public int OwnerId { get; }
    public Zone PileZone { get; }
    /// <summary>Decorative stack height of a deck. The real deck size is hidden information (EE-STATE-014) and never reaches the client, so every deck is drawn the same.</summary>
    public const int DeckDepth = 20;
    public int Count { get; }
    private readonly Texture2D? _face;
    public static readonly Texture2D DynastyBack = GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
    public static readonly Texture2D FateBack = GD.Load<Texture2D>("res://Art/original-fate-back.png");
    private readonly bool _fate, _discard;
    private float _shownCount;
    private float _startCount;
    private double _settle = 1;
    public float ShownCount => _shownCount;
    private const float CardThickness = 1.35f;
    private const float CardLean = .10f;
    public Rect2 TopFace => LayerFace(Math.Max(0, _shownCount));
    private Rect2 LayerFace(float level)
    {
        if (PileZone == Zone.RemovedFromGame) level = Math.Min(level, 12);
        var height = Size.Y - 18; var width = height * .7142857f;
        return new Rect2((Size.X - width) / 2 - level * CardLean, 26 - level * CardThickness, width, height);
    }
    public override bool _HasPoint(Vector2 point) => TopFace.Merge(LayerFace(0)).HasPoint(point);
    public void SettleFrom(float count, bool reducedMotion)
    {
        _startCount = Math.Clamp(count, 0, 200); _shownCount = reducedMotion ? Count : _startCount;
        _settle = reducedMotion ? 1 : 0; QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (_settle >= 1) return;
        _settle = Math.Min(1, _settle + delta / .65);
        var t = (float)_settle; t = t * t * (3 - 2 * t);
        _shownCount = Mathf.Lerp(_startCount, Count, t); QueueRedraw();
    }
    public TablePile(PlayerView player, Zone zone, Action open, CardDefinition? recentDiscard = null)
    {
        OwnerId = player.Id; PileZone = zone;
        _fate = zone is Zone.FateDeck or Zone.FateDiscard;
        _discard = zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.RemovedFromGame;
        var cards = zone == Zone.RemovedFromGame ? player.RemovedFromGame : player.Discard.Where(c => c.Zone == zone).ToArray();
        Count = zone is Zone.FateDeck or Zone.DynastyDeck ? DeckDepth : cards.Length;
        _shownCount = Count;
        Material = CardPaper.Table;
        var name = zone == Zone.RemovedFromGame ? "Removed from the Game" : (_fate ? "Fate" : "Dynasty") + (_discard ? " discard" : " deck");
        // The projection doesn't expose chronological pile order. The face is a
        // representative public card, not a claim about the top of an ordered pile.
        _face = _discard && (recentDiscard ?? cards.FirstOrDefault()?.Definition) is { } def ? CardTile.Artwork(def) : null;
        TooltipText = $"{player.Clan} · {name}" + (zone is Zone.FateDeck or Zone.DynastyDeck ? "" : $"\n{Count} cards") + (zone == Zone.FateDeck ? $" · {player.HandCount} in hand" : "") +
            (_discard ? "\nClick to inspect the pile. The face previews a public discard." : "\nDeck contents and order remain hidden.");
        Flat = true; Text = ""; MouseDefaultCursorShape = CursorShape.PointingHand;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(state, Palette.Box(Colors.Transparent, null, 0));
        Pressed += open; MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }
    private static Vector2[] FaceQuad(Rect2 r) => new[] {
        r.Position + new Vector2(0, 0),
        r.Position + new Vector2(r.Size.X, 0),
        r.End + new Vector2(0, 0),
        r.Position + new Vector2(-0, r.Size.Y)
    };
    private void PileFrame(Rect2 r, Color fill, Color rim)
    {
        var points = FaceQuad(r); DrawColoredPolygon(points, fill);
        DrawPolyline(points.Append(points[0]).ToArray(), rim, 1, true);
    }
    private void PileFace(Texture2D texture, Rect2 r, Color tint) =>
        DrawColoredPolygon(FaceQuad(r), tint, new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, texture);

    private static readonly Color CoreColor = new("141816"), RimColor = new("292b22");
    private static readonly Color[] Edges = [new("b1a58a"), new("867e69"), new("9d9278")];

    /// <summary>The visible pieces of an axis-aligned segment: what is left after the layers above it (<paramref name="above"/>) are painted over it.</summary>
    private static void AddVisible(List<Vector2> into, List<Color> colors, Color color, Vector2 a, Vector2 b, List<Rect2> above)
    {
        var horizontal = Math.Abs(b.Y - a.Y) < .0001f;
        var from = horizontal ? Math.Min(a.X, b.X) : Math.Min(a.Y, b.Y); var to = horizontal ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
        var fixedAxis = horizontal ? a.Y : a.X;
        var covered = new List<(float, float)>();
        foreach (var r in above)
        {
            var inRange = horizontal ? fixedAxis > r.Position.Y && fixedAxis < r.End.Y : fixedAxis > r.Position.X && fixedAxis < r.End.X;
            if (!inRange) continue;
            var lo = horizontal ? r.Position.X : r.Position.Y; var hi = horizontal ? r.End.X : r.End.Y;
            if (hi > from && lo < to) covered.Add((Math.Max(lo, from), Math.Min(hi, to)));
        }
        covered.Sort();
        var cursor = from;
        void Emit(float s, float e)
        {
            if (e - s < .01f) return;
            into.Add(horizontal ? new Vector2(s, fixedAxis) : new Vector2(fixedAxis, s));
            into.Add(horizontal ? new Vector2(e, fixedAxis) : new Vector2(fixedAxis, e));
            colors.Add(color); // one colour per segment
        }
        foreach (var (lo, hi) in covered) { if (lo > cursor) Emit(cursor, lo); cursor = Math.Max(cursor, hi); }
        if (cursor < to) Emit(cursor, to);
    }

    /// <summary>
    /// Paints the stacked card layers in a handful of draw calls instead of about eight per layer (issue #879). A layer only shows
    /// where the layers above it do not cover it, so the dark cores become one triangle batch and each kind of line one batched line
    /// call holding only its visible pieces; the result matches painting layer by layer.
    /// </summary>
    private void DrawLayers()
    {
        var count = (int)Math.Ceiling(PileZone == Zone.RemovedFromGame ? Math.Min(_shownCount, 12) : _shownCount);
        var rects = new List<Rect2>(count);
        for (var i = 0; i < count; i++)
        {
            var layer = LayerFace(Math.Min(i + 1, _shownCount));
            layer.Position += new Vector2((float)Math.Sin(i * 2.399) * .38f, 0);
            rects.Add(layer);
        }
        var points = new Vector2[count * 4]; var colors = new Color[count * 4]; var indices = new int[count * 6];
        for (var i = 0; i < count; i++)
        {
            var q = FaceQuad(rects[i]);
            for (var k = 0; k < 4; k++) { points[i * 4 + k] = q[k]; colors[i * 4 + k] = CoreColor; }
            int[] tri = [0, 1, 2, 0, 2, 3];
            for (var k = 0; k < 6; k++) indices[i * 6 + k] = i * 4 + tri[k];
        }
        if (count > 0) RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), indices, points, colors);
        var rim = new List<Vector2>(); var rimColors = new List<Color>();
        var bottom = new List<Vector2>(); var bottomColors = new List<Color>();
        var right = new List<Vector2>(); var rightColors = new List<Color>();
        var fibers = new List<Vector2>(); var fiberColors = new List<Color>();
        for (var i = 0; i < count; i++)
        {
            var layer = rects[i]; var above = rects.GetRange(i + 1, count - i - 1);
            var q = FaceQuad(layer);
            AddVisible(rim, rimColors, RimColor, q[0], q[1], above); AddVisible(rim, rimColors, RimColor, q[1], q[2], above);
            AddVisible(rim, rimColors, RimColor, q[3], q[2], above); AddVisible(rim, rimColors, RimColor, q[0], q[3], above);
            var edge = Edges[i % 3];
            AddVisible(bottom, bottomColors, edge, layer.Position + new Vector2(.6f, layer.Size.Y), layer.End + new Vector2(-.6f, 0), above);
            AddVisible(right, rightColors, edge.Darkened(.25f), layer.Position + new Vector2(layer.Size.X, 1), layer.End, above);
            for (var fiber = 0; fiber < 4; fiber++)
            {
                var x = layer.Position.X + 8 + (i * 17 + fiber * 37) % (int)Math.Max(1, layer.Size.X - 22);
                AddVisible(fibers, fiberColors, edge.Lightened(.12f), new Vector2(x, layer.End.Y), new Vector2(x + 3, layer.End.Y), above);
            }
        }
        if (rim.Count > 0) DrawMultilineColors(rim.ToArray(), rimColors.ToArray(), 1, true);
        if (bottom.Count > 0) DrawMultilineColors(bottom.ToArray(), bottomColors.ToArray(), .63f, true);
        if (right.Count > 0) DrawMultilineColors(right.ToArray(), rightColors.ToArray(), .52f, true);
        if (fibers.Count > 0) DrawMultilineColors(fibers.ToArray(), fiberColors.ToArray(), .48f, true);
    }

    public override void _Draw()
    {
        var frame = TopFace;
        var w = frame.Size.X; var h = frame.Size.Y;
        if (_shownCount < .01)
            PileFrame(frame, new Color(.04f, .035f, .02f, .25f), new Color(.65f, .5f, .3f, .28f));
        else
        {
            DrawStyleBox(Palette.Box(new Color(0, 0, 0, .5f), null, 0, 4), new Rect2((Size.X - w) / 2 + 2, 29, w + 3, h + 3));
            DrawLayers();
            var texture = _face ?? (_fate ? FateBack : DynastyBack);
            PileFace(texture, frame.Grow(-1), Colors.White);
        }
        if (IsHovered()) DrawRect(frame.Grow(2), Palette.Gold, false, 1, true);
    }
}
