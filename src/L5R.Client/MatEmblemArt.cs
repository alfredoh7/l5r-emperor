using Godot;

namespace L5R.Client;

/// <summary>Generated gold-on-black production art, extracted once as pigment alpha.</summary>
public static class MatEmblemArt
{
    private static Texture2D[]? _symbols;
    private static Texture2D? _seal;
    public static Texture2D Seal => _seal ??= Pigment(GD.Load<Texture2D>("res://Art/province-seal.png").GetImage());
    public static Texture2D Symbol(string label)
    {
        if (_symbols is null)
        {
            using var source = GD.Load<Texture2D>("res://Art/mat-section-emblems.png").GetImage();
            var width = source.GetWidth() / 4;
            _symbols = Enumerable.Range(0, 4).Select(i => Pigment(source.GetRegion(new Rect2I(i * width, 0, width, source.GetHeight())))).ToArray();
        }
        return _symbols[label switch { "Holdings" => 0, "Home" => 1, "Provinces" => 2, _ => 3 }];
    }

    private static Texture2D Pigment(Image source)
    {
        using (source)
        {
            source.Convert(Image.Format.Rgba8);
            var bytes = source.GetData();
            for (var i = 0; i < bytes.Length; i += 4)
            {
                // Un-premultiply generated gold against its deliberately black matte.
                // A tiny black floor removes compression grain without losing patina.
                var alpha = Math.Max(bytes[i], Math.Max(bytes[i + 1], bytes[i + 2]));
                if (alpha < 10) { bytes[i + 3] = 0; continue; }
                for (var channel = 0; channel < 3; channel++) bytes[i + channel] = (byte)Math.Min(255, bytes[i + channel] * 255 / alpha);
                bytes[i + 3] = (byte)alpha;
            }
            using var pigment = Image.CreateFromData(source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8, bytes);
            using var cropped = pigment.GetRegion(pigment.GetUsedRect());
            cropped.GenerateMipmaps();
            return ImageTexture.CreateFromImage(cropped);
        }
    }

    public static void DrawSeal(Control canvas, Rect2 bounds, string number, float opacity = 1)
    {
        canvas.DrawTextureRect(Seal, bounds, false, new Color(1, 1, 1, opacity));
        var fontSize = Math.Max(11, (int)Math.Round(bounds.Size.Y * .57f));
        var baseline = bounds.GetCenter() + new Vector2(-bounds.Size.X / 2,
            (Palette.Title.GetAscent(fontSize) - Palette.Title.GetDescent(fontSize)) / 2 - bounds.Size.Y * .01f);
        canvas.DrawStringOutline(Palette.Title, baseline, number, HorizontalAlignment.Center, bounds.Size.X, fontSize, 2, new Color(.025f, .035f, .045f, opacity));
        canvas.DrawString(Palette.Title, baseline, number, HorizontalAlignment.Center, bounds.Size.X, fontSize, new Color(.93f, .88f, .76f, opacity));
    }
}
