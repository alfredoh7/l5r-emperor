using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Persistent marker under a province that Dark Experiments has marked: the payment-flow coin carrying the Holding Gold Production change, and the stronghold's Province Strength box carrying the
/// strength change. Each shows only while its modifier is non-zero; the board adds none for a destroyed province or one gained later. Presentation only: the values are the engine's DarkExperimentsMark.</summary>
public sealed partial class ProvinceModifierMarker : Control
{
    public const float Side = 52, Gap = 8;
    private static readonly Texture2D CoinTexture = GD.Load<Texture2D>("res://Art/Payments/plain-gold-coin.png");
    private static readonly Texture2D FrameTexture = GD.Load<Texture2D>("res://Art/ProvinceStrength/province-strength-frame.png");
    private const float FrameAspect = 113f / 104f;
    private readonly int _production, _strength;

    public ProvinceModifierMarker(int production, int strength)
    {
        _production = production; _strength = strength;
        MouseFilter = MouseFilterEnum.Stop;
        Name = "ProvinceModifierMarker";
        var parts = new List<string>();
        if (production != 0) parts.Add($"a Holding that enters play from it has {Signed(production)} Gold Production");
        if (strength != 0) parts.Add($"it has {Signed(strength)} strength");
        TooltipText = "Dark Experiments: " + string.Join("; ", parts) + ".";
        Size = new Vector2(Width(production, strength), Side);
        CustomMinimumSize = Size;
    }

    public static float Width(int production, int strength) => (production != 0 ? Side : 0) + (strength != 0 ? Side * FrameAspect : 0) + (production != 0 && strength != 0 ? Gap : 0);
    public static string Signed(int value) => value < 0 ? "−" + -value : "+" + value;

    public override void _Draw()
    {
        var x = 0f;
        if (_production != 0)
        {
            DrawTextureRect(CoinTexture, new Rect2(x, 0, Side, Side), false);
            DrawString(Palette.Body, new Vector2(x, Side / 2f + 8), Signed(_production), HorizontalAlignment.Center, Side, 22, Palette.Ink);
            x += Side + Gap;
        }
        if (_strength != 0)
        {
            // The stronghold's own Province Strength box (its numeral is white on the wood plate, as printed).
            var box = new Rect2(x, 0, Side * FrameTexture.GetWidth() / FrameTexture.GetHeight(), Side);
            DrawTextureRect(FrameTexture, box, false);
            var at = new Vector2(box.Position.X, Side / 2f + 9);
            DrawString(Palette.Body, at + new Vector2(1, 1.5f), Signed(_strength), HorizontalAlignment.Center, box.Size.X, 26, Palette.Ink);
            DrawString(Palette.Body, at, Signed(_strength), HorizontalAlignment.Center, box.Size.X, 26, Colors.White);
        }
    }
}
