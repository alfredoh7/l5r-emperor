using Godot;

namespace L5R.Client;

/// <summary>
/// The fixed-plane Gold payment readout beside the hand: a coin for the Gold paid so far, a slash and a coin for the cost
/// ("paid / cost"). A cost the player chooses has no denominator, so it shows the single coin. Presentation only: the
/// numbers come from the engine's payment quote and the player's selected sources.
/// </summary>
public sealed partial class GoldPaymentMeter : Control
{
    private const int Coin = 56, Gap = 44;
    private static readonly Texture2D CoinTexture = GD.Load<Texture2D>("res://Art/Payments/plain-gold-coin.png");
    private int _paid; private int? _cost;

    public GoldPaymentMeter()
    {
        MouseFilter = MouseFilterEnum.Ignore; Visible = false; Name = "GoldPaymentMeter";
        CustomMinimumSize = new(Coin * 2 + Gap, Coin + 8);
    }

    public int Paid => _paid;
    public int? Cost => _cost;

    public void Present(int paid, int? cost)
    {
        _paid = paid; _cost = cost; Visible = true;
        AccessibilityName = cost is { } c ? $"Gold paid {paid} of {c}" : $"Gold paid {paid}";
        TooltipText = AccessibilityName;
        QueueRedraw();
    }

    public void Dismiss() { Visible = false; _cost = null; _paid = 0; }

    public override void _Draw()
    {
        DrawNumberedCoin(new Vector2(0, 4), _paid, Palette.Ink);
        if (_cost is not { } cost) return;
        DrawString(Palette.HeadingFace, new Vector2(Coin + 4, Coin / 2 + 18), "/", HorizontalAlignment.Center, Gap - 8, 46, Palette.Gold);
        DrawNumberedCoin(new Vector2(Coin + Gap, 4), cost, Palette.Ink);
    }

    private void DrawNumberedCoin(Vector2 at, int value, Color ink)
    {
        DrawTextureRect(CoinTexture, new Rect2(at, new Vector2(Coin, Coin)), false);
        var text = value.ToString();
        DrawString(Palette.Body, at + new Vector2(0, Coin / 2f + 10), text, HorizontalAlignment.Center, Coin, text.Length > 2 ? 22 : 30, ink);
    }
}
