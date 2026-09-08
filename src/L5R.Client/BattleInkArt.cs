using Godot;

namespace L5R.Client;

/// <summary>Approved raster pigment; no new card art or game-state arithmetic.</summary>
public static class BattleInkArt
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    public static Texture2D Extract(string path, Rect2I rect)
    {
        var key = path + rect;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        using var source = GD.Load<Texture2D>(path).GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        using var region = source.GetRegion(rect);
        var pixels = region.GetData();
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = (byte)(255 * Math.Clamp((pixels[i] - pixels[i + 2] - 5f) / 34f, 0, 1));
        using var image = Image.CreateFromData(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8, pixels);
        image.GenerateMipmaps();
        return Cache[key] = ImageTexture.CreateFromImage(image);
    }
    private static Texture2D? _verticalLink;
    public static void Stroke(Control canvas, Vector2 from, Vector2 to, Color tint)
    {
        if (_verticalLink is null)
        {
            using var source = ProvinceThresholdArt.Plate("Home", null).GetImage();
            using var line = source.GetRegion(new Rect2I(240, 34, 500, 6));
            line.ClearMipmaps(); line.Rotate90(ClockDirection.Clockwise); line.GenerateMipmaps();
            _verticalLink = ImageTexture.CreateFromImage(line);
        }
        canvas.DrawTextureRect(_verticalLink, new Rect2(from.X - 4, Math.Min(from.Y, to.Y), 8, Math.Abs(from.Y - to.Y)), false, tint);
    }
    public static Texture2D Step => Extract("res://Art/battle-ink-reference.png", new Rect2I(375, 644, 32, 24));
    public static void Court(Control canvas, Vector2 size, float active)
    {
        // The neutral B court's actual filigree and sword medallion. Only the
        // straight runs stretch; the center stays empty for real game cards.
        var art = Extract("res://Art/battle-court-reference.png", new Rect2I(1373, 268, 253, 237));
        var edge = 40f; var tint = Colors.White.Lerp(new Color(1.25f, 1.18f, 1.08f), active);
        void Piece(Rect2 destination, Rect2 source) => canvas.DrawTextureRectRegion(art, destination, source, tint);
        Piece(new Rect2(0, 0, edge, edge), new Rect2(0, 0, 40, 40));
        Piece(new Rect2(size.X-edge, 0, edge, edge), new Rect2(213, 0, 40, 40));
        Piece(new Rect2(0, size.Y-edge, edge, edge), new Rect2(0, 197, 40, 40));
        Piece(new Rect2(size.X-edge, size.Y-edge, edge, edge), new Rect2(213, 197, 40, 40));
        Piece(new Rect2(edge, 0, size.X-2*edge, 16), new Rect2(45, 0, 45, 14));
        Piece(new Rect2(0, edge, 16, size.Y-2*edge), new Rect2(0, 50, 14, 140));
        Piece(new Rect2(size.X-16, edge, 16, size.Y-2*edge), new Rect2(239, 50, 14, 140));
        Piece(new Rect2(edge, size.Y-16, size.X-2*edge, 16), new Rect2(45, 223, 45, 14));
        var icon = Extract("res://Art/battle-court-reference.png", new Rect2I(1476, 238, 56, 57));
        canvas.DrawTextureRect(icon, new Rect2(size.X/2-26, -24, 52, 53), false, tint);
    }
    public static void Fallen(Control canvas, Rect2 rect, int number, float progress)
    {
        // Neutral source before the crack; settled state remains a low marker,
        // never a card-sized empty bay that could be mistaken for a legal target.
        var art = ProvinceThresholdArt.Plate("Province", number);
        var foot = new Rect2(0, art.GetHeight() - 66, art.GetWidth(), 66);
        canvas.DrawTextureRectRegion(art, rect, foot, new Color(1, 1, 1, 1 - Math.Clamp(progress * 2.5f, 0, 1)));
        var broken = Extract("res://Art/fallen-threshold.png", new Rect2I(90, 175, 1805, 430));
        var destination = new Rect2(rect.Position + new Vector2(0, 5), new Vector2(rect.Size.X, rect.Size.X * 430 / 1805));
        var tint = Mathf.Lerp(1.3f, .60f, progress);
        canvas.DrawTextureRect(broken, destination, false, new Color(tint, tint, tint, progress));
        var fontSize = (int)(rect.Size.X * .125f);
        var text = number.ToString(); var extent = Palette.Title.GetStringSize(text, fontSize: fontSize);
        canvas.DrawString(Palette.Title, destination.GetCenter() + new Vector2(-extent.X / 2 - 1, extent.Y * .23f), text,
            fontSize: fontSize, modulate: new Color(.77f, .73f, .65f, progress));
        if (progress > 0 && progress < 1)
        {
            // Small pigment flecks separate along the fracture, then sink into
            // the cloth. The marker itself never shakes, scales or changes slot.
            for (var i = 0; i < 9; i++)
            {
                var drift = new Vector2(Mathf.Sin(i * 2.4f) * 15 * progress, -18 * progress - i * 2);
                canvas.DrawCircle(destination.GetCenter() + drift, .7f + i % 2 * .35f,
                    new Color(.82f, .61f, .29f, Mathf.Sin(progress * Mathf.Pi) * .4f));
            }
        }
    }
}
