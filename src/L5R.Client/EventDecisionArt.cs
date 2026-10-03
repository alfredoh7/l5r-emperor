using Godot;
namespace L5R.Client;

/// <summary>Approved pixels for brass joinery and lacquer. Labels and card faces remain native/live.</summary>
public static class EventDecisionArt
{
    private static Texture2D? _a, _b;
    public static Texture2D A => _a ??= GD.Load<Texture2D>("res://Art/EventDecisions/A1.png");
    public static Texture2D B => _b ??= GD.Load<Texture2D>("res://Art/EventDecisions/B2.png");
    public static void Frame(CanvasItem canvas, Rect2 target, bool hand = false)
    {
        canvas.DrawTextureRectRegion(B, target, new Rect2(1340, 732, 275, 140));
        Border(canvas, target, hand ? B : A, hand ? new(213, 563, 1052, 109) : new(1287, 79, 372, 848), 16, hand);
    }
    public static void Border(CanvasItem canvas, Rect2 target, Texture2D texture, Rect2 source, float edge, bool cleanHandBottom = false)
    {
        float[] sx = [0, edge, source.Size.X - edge, source.Size.X], sy = [0, edge, source.Size.Y - edge, source.Size.Y];
        float[] dx = [0, edge, target.Size.X - edge, target.Size.X], dy = [0, edge, target.Size.Y - edge, target.Size.Y];
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) if (x != 1 || y != 1)
            canvas.DrawTextureRectRegion(texture, new(target.Position + new Vector2(dx[x], dy[y]), new Vector2(dx[x + 1] - dx[x], dy[y + 1] - dy[y])),
                cleanHandBottom && y == 2 && x == 1 ? new Rect2(250, 656, 450, 16) : new(source.Position + new Vector2(sx[x], sy[y]), new Vector2(sx[x + 1] - sx[x], sy[y + 1] - sy[y])));
    }
    public static void Button(CanvasItem canvas, Rect2 target, bool danger, bool gold)
    {
        var texture = gold ? B : A; var frame = gold ? new Rect2(965, 607, 267, 49) : danger ? new(1305, 786, 333, 69) : new(1305, 716, 333, 69);
        var fill = gold ? new Rect2(994, 614, 204, 6) : danger ? new(1335, 834, 278, 7) : new(1335, 766, 278, 6);
        canvas.DrawTextureRectRegion(texture, target, fill);
        Border(canvas, target, texture, frame, gold ? 12 : 16);
    }
}
public partial class EventArtButton : Button
{
    public bool Danger { get; init; }
    public bool Gold { get; init; }
    public EventArtButton(string caption, Action clicked)
    {
        Text = caption; AccessibilityName = caption; Flat = true; MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color" }) AddThemeColorOverride(state, Colors.Transparent);
        Pressed += clicked; MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw; Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        EventDecisionArt.Button(this, new(Vector2.Zero, Size), Danger, Gold && !Disabled);
        if (Disabled) DrawRect(new(Vector2.One * 10, Size - Vector2.One * 20), new Color(0, 0, 0, .3f));
        if (!Disabled && (IsHovered() || HasFocus())) DrawRect(new(Vector2.One * 7, Size - Vector2.One * 14), Palette.Paper with { A = .7f }, false, 1, true);
        var font = SidebarSkin.Bold; var size = 22;
        while (size > 16 && font.GetStringSize(Text, fontSize: size).X > Size.X - 42) size--;
        DrawString(font, new Vector2(21, (Size.Y + font.GetAscent(size) - font.GetDescent(size)) / 2), Text, HorizontalAlignment.Center, Size.X - 42, size,
            Disabled ? Palette.Muted : Gold ? new Color("211409") : Palette.Paper);
    }
}
