using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A's attached strip; the authoritative aggregate is never recomputed by the HUD.</summary>
public partial class FallenCounter : Button
{
    public const float DesignHeight = 70;
    public required PlayerView Player { get; set; }
    public int Count => Player.DeadCount;
    public FallenCounter()
    {
        Flat = true; FocusMode = FocusModeEnum.All; TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" }) AddThemeStyleboxOverride(s, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        Resized += QueueRedraw;
    }
    public override void _Ready()
    {
        AccessibilityName = $"{Player.Clan} · Dead · {Count} dead Personalities · Open gallery";
        TooltipText = AccessibilityName;
    }
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Size / new Vector2(454, 103));
        FallenArt.Frame(this, new Rect2(0, 0, 454, 103), true);
        FallenArt.Helmet(this, new Rect2(44, 10, 90, 76));
        DrawString(Palette.Body, new Vector2(155, 49), "Dead", fontSize: 30, modulate: Palette.Paper);
        var fs = 31;
        while (fs > 15 && Palette.Body.GetStringSize(Count.ToString(), fontSize: fs).X > 78) fs--;
        DrawString(Palette.Body, new Vector2(258, 49), Count.ToString(), HorizontalAlignment.Left, 78, fs, Palette.Paper);
        DrawTextureRectRegion(FallenArt.A, new Rect2(397, 26, 24, 28), new Rect2(474, 367, 24, 28));
        DrawLine(new Vector2(156, 61), new Vector2(417, 61), new Color("715832"));
        DrawString(Palette.Body, new Vector2(221, 84), "Open gallery", fontSize: 17, modulate: Palette.PaperDim);
        if (IsHovered() || HasFocus()) DrawRect(new Rect2(6, 6, 442, 91), Palette.Gold with { A = .65f }, false, 1.6f);
        DrawSetTransformMatrix(Transform2D.Identity);
    }
    public override Control _MakeCustomTooltip(string text)
    {
        var cards = FallenArt.Cards(Player);
        return ClanPlaque.MakeStatTooltip("Dead cards", $"{Count} dead Personalities\n{cards.Count(c => !c.Dishonorable)} honorable · {cards.Count(c => c.Dishonorable)} dishonorable",
            GetTree().Root.GetVisibleRect().Size.Y / 1080f);
    }
}
