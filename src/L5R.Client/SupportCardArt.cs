using Godot;

namespace L5R.Client;

/// <summary>Production frame derived from approved B; thin province ornament sampled from approved A.
/// Card text and numerals remain live. No illustrated mockup card is used as a playing card.</summary>
public static class SupportCardArt
{
    private static Texture2D? _celestial, _province;
    public static Texture2D CelestialFrame => _celestial ??= GD.Load<Texture2D>("res://Art/Support/celestial-frame.png");
    public static void DrawCelestial(Control canvas, Vector2[] face)
    {
        // The transparent aperture is (150,190)-(930,1285) in the generated asset.
        // Map that aperture to the full original card; never crop or cover its printed border.
        Vector2 Map(float x, float y) => face[0] + (face[1] - face[0]) * ((x - 150) / 780) + (face[3] - face[0]) * ((y - 190) / 1095);
        Vector2[] corners = [Map(0, 0), Map(1080, 0), Map(1080, 1456), Map(0, 1456)];
        canvas.DrawColoredPolygon(corners, Colors.White, new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, CelestialFrame);
    }

    private static Texture2D ProvinceOrnament
    {
        get
        {
            if (_province is not null) return _province;
            using var image = GD.Load<Texture2D>("res://Art/Support/approved-a.png").GetImage();
            if (image.IsCompressed()) image.Decompress();
            image.Convert(Image.Format.Rgba8);
            // The first province supplies a neutral thin outline. Isolate only the warm ink,
            // clear its card/number interior, and reuse the original ornamental corner pixels.
            using var crop = image.GetRegion(new Rect2I(289, 402, 155, 113));
            var pixels = crop.GetData();
            for (var y = 0; y < 113; y++) for (var x = 0; x < 155; x++)
            {
                var at = (y * 155 + x) * 4;
                pixels[at + 3] = (byte)(255 * Math.Clamp((pixels[at] - pixels[at + 2] - 10f) / 35f, 0, 1));
                if (x > 20 && x < 130 && y > 14 && y < 100 || y > 101) pixels[at + 3] = 0;
            }
            using var plate = Image.CreateFromData(155, 113, false, Image.Format.Rgba8, pixels);
            plate.GenerateMipmaps(); return _province = ImageTexture.CreateFromImage(plate);
        }
    }

    public static void DrawProvincePair(Control canvas, Vector2 size, int number, float active)
    {
        // Corners stay fixed at card scale; only the unornamented runs stretch across the pair.
        var art = ProvinceOrnament;
        float[] sx = [0, 24, 130, 155], sy = [0, 17, 96, 113];
        float[] dx = [0, 30, size.X - 30, size.X], dy = [0, 24, size.Y - 70, size.Y - 48];
        var tint = Colors.White.Lerp(new Color(1.22f, 1.13f, 1.02f), active);
        for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
        {
            if (x == 1 && y == 1) continue;
            canvas.DrawTextureRectRegion(art, new Rect2(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]), new Rect2(sx[x], sy[y], sx[x + 1] - sx[x], sy[y + 1] - sy[y]), tint);
        }
        // Retain the exact approved small numbered seal, without widening it with the pair.
        var seal = ProvinceThresholdArt.Plate("Province", number);
        canvas.DrawTextureRectRegion(seal, new Rect2(size.X / 2 - 26, size.Y - 53, 52, 45), new Rect2(97, 294, 64, 55), tint);
    }
}
