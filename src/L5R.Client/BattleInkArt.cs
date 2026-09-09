using Godot;

namespace L5R.Client;

/// <summary>Approved raster pigment; no new card art or game-state arithmetic.</summary>
public static class BattleInkArt
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    public static Texture2D Extract(string path, Rect2I rect, bool medallion = false, bool borderPigment = false)
    {
        var key = path + rect + medallion + borderPigment;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        using var source = GD.Load<Texture2D>(path).GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        using var region = source.GetRegion(rect);
        var pixels = region.GetData();
        var offset = medallion || borderPigment ? 3f : 5f;
        var softness = medallion || borderPigment ? 24f : 34f;
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = (byte)(255 * Math.Clamp((pixels[i] - pixels[i + 2] - offset) / softness, 0, 1));
        if (medallion)
            for (var y = 0; y < rect.Size.Y; y++) for (var x = 0; x < rect.Size.X; x++)
                if (new Vector2(x - 24, y - 27).Length() < 22) pixels[(y * rect.Size.X + x) * 4 + 3] = 255;
        using var image = Image.CreateFromData(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8, pixels);
        image.GenerateMipmaps();
        return Cache[key] = ImageTexture.CreateFromImage(image);
    }
    public const float NeckSpan = 52;
    private static Texture2D? _invertedNeck;
    public static void Neck(Control canvas, Vector2 courtEdge, bool provinceAbove, Color tint)
    {
        // Actual B concept pixels: the rounded shoulders, narrow passage and
        // flared lips are one asset. Never substitute parallel straight lines.
        var neck = Extract("res://Art/battle-court-reference.png", new Rect2I(1478, 494, 74, 66), borderPigment: true);
        if (provinceAbove && _invertedNeck is null)
        {
            using var image = neck.GetImage(); image.ClearMipmaps(); image.FlipY(); image.GenerateMipmaps();
            _invertedNeck = ImageTexture.CreateFromImage(image);
        }
        canvas.DrawTextureRect(provinceAbove ? _invertedNeck! : neck,
            new Rect2(courtEdge.X - 37, courtEdge.Y + (provinceAbove ? -55 : -11), 74, 66), false, tint);
    }
    public static Texture2D Step => Extract("res://Art/battle-ink-reference.png", new Rect2I(375, 644, 32, 24));
    public static void Court(Control canvas, Vector2 size, float active, bool connectionAbove = false)
    {
        // The neutral B court's actual filigree and sword medallion. Only the
        // straight runs stretch; the center stays empty for real game cards.
        var art = Extract("res://Art/battle-court-reference.png", new Rect2I(1373, 268, 260, 237), borderPigment: true);
        var edge = 40f; var tint = Colors.White.Lerp(new Color(1.25f, 1.18f, 1.08f), active);
        void Piece(Rect2 destination, Rect2 source) => canvas.DrawTextureRectRegion(art, destination, source, tint);
        Piece(new Rect2(0, 0, edge, edge), new Rect2(0, 0, 40, 40));
        Piece(new Rect2(size.X-edge, 0, edge, edge), new Rect2(220, 0, 40, 40));
        Piece(new Rect2(0, size.Y-edge, edge, edge), new Rect2(0, 197, 40, 40));
        Piece(new Rect2(size.X-edge, size.Y-edge, edge, edge), new Rect2(220, 197, 40, 40));
        if (connectionAbove)
        {
            Piece(new Rect2(edge, 0, size.X/2-37-edge, 14), new Rect2(45, 0, 45, 14));
            Piece(new Rect2(size.X/2+37, 0, size.X/2-37-edge, 14), new Rect2(45, 0, 45, 14));
        }
        else
        {
            Piece(new Rect2(edge, 0, size.X/2-22-edge, 14), new Rect2(45, 0, 45, 14));
            Piece(new Rect2(size.X/2+22, 0, size.X/2-22-edge, 14), new Rect2(45, 0, 45, 14));
        }
        Piece(new Rect2(0, edge, 14, size.Y-2*edge), new Rect2(0, 50, 14, 140));
        Piece(new Rect2(size.X-14, edge, 14, size.Y-2*edge), new Rect2(246, 50, 14, 140));
        if (connectionAbove)
        {
            Piece(new Rect2(edge, size.Y-14, size.X/2-22-edge, 14), new Rect2(45, 223, 45, 14));
            Piece(new Rect2(size.X/2+22, size.Y-14, size.X/2-22-edge, 14), new Rect2(45, 223, 45, 14));
        }
        else
        {
            Piece(new Rect2(edge, size.Y-14, size.X/2-37-edge, 14), new Rect2(45, 223, 45, 14));
            Piece(new Rect2(size.X/2+37, size.Y-14, size.X/2-37-edge, 14), new Rect2(45, 223, 45, 14));
        }
        var icon = Extract("res://Art/battle-court-reference.png", new Rect2I(1476, 238, 56, 57), medallion: true);
        canvas.DrawTextureRect(icon, new Rect2(size.X/2-24, connectionAbove ? size.Y-24 : -30, 56, 57), false, tint);
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
