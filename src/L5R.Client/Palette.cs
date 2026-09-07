using Godot;

namespace L5R.Client;

public static class Palette
{
    public static int Direction { get; set; }
    public static Color Ink => new("191d18");
    public static Color Paper => new("e7dfc9");
    public static Color PaperDim => new("c6bda4");
    public static Color Gold => new("c5a66e");
    public static Color Muted => new("9eaa9c");
    public static Color Crane => new("98bfca");
    public static Color Lion => new("c7a664");
    public static Color Error => new("e9a79b");
    public static Color Cloth => Direction switch { 1 => new("b9b39f"), 2 => new("253640"), _ => new("34493e") };
    public static Color Background => new("11140f");
    public static Font Body { get; } = GD.Load<FontFile>("res://Art/Fonts/Alegreya.ttf");
    public static Font Title { get; } = GD.Load<FontFile>("res://Art/Fonts/MusashiBrush.otf");

    public static StyleBoxFlat Box(Color background, Color? border = null, int padding = 12, int radius = 3)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background, BorderColor = border ?? new Color(0, 0, 0, 0),
            BorderWidthLeft = border.HasValue ? 1 : 0, BorderWidthTop = border.HasValue ? 1 : 0,
            BorderWidthRight = border.HasValue ? 1 : 0, BorderWidthBottom = border.HasValue ? 1 : 0,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding
        };
        return box;
    }

    public static StyleBoxFlat RuledPanel(bool active, int padding)
    {
        var box = Box(new Color(.08f, .09f, .055f, active ? .38f : .12f), active ? Gold : new Color(.61f, .50f, .30f, .55f), padding, 0);
        box.BorderWidthLeft = 0;
        box.BorderWidthBottom = active ? 1 : 0;
        return box;
    }

    public static Theme MakeTheme()
    {
        var t = new Theme { DefaultFont = Body, DefaultFontSize = 16 };
        t.SetColor("font_color", "Label", Paper);
        t.SetColor("default_color", "RichTextLabel", Paper);
        t.SetConstant("separation", "VBoxContainer", 8);
        t.SetConstant("separation", "HBoxContainer", 12);
        t.SetStylebox("normal", "Button", Box(new Color("23271e"), new Color("736343")));
        t.SetStylebox("hover", "Button", Box(new Color("3a3e2b"), Gold));
        t.SetStylebox("pressed", "Button", Box(new Color("494933"), Gold));
        t.SetStylebox("disabled", "Button", Box(new Color("25322c"), new Color("455045")));
        t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Crane, 0));
        t.SetColor("font_color", "Button", Paper);
        t.SetColor("font_hover_color", "Button", new Color("faf1d9"));
        t.SetColor("font_disabled_color", "Button", new Color("849084"));
        t.SetStylebox("normal", "LineEdit", Box(new Color("182b22"), new Color("736343")));
        t.SetStylebox("focus", "LineEdit", Box(new Color("243b2f"), Gold));
        t.SetColor("font_color", "LineEdit", Paper);
        t.SetStylebox("panel", "PopupPanel", Box(Ink, Gold, 20));
        t.SetStylebox("panel", "AcceptDialog", Box(new Color("171b15"), Gold, 16));
        var windowBorder = Box(new Color("171b15"), new Color("736343"), 0);
        windowBorder.BorderWidthTop = 30;
        t.SetStylebox("embedded_border", "Window", windowBorder);
        t.SetColor("title_color", "Window", Paper);
        t.SetStylebox("panel", "TabContainer", Box(new Color("171b15"), null, 8));
        t.SetStylebox("tab_selected", "TabContainer", Box(new Color("353728"), Gold, 9));
        t.SetStylebox("tab_unselected", "TabContainer", Box(new Color("21261e"), null, 9));
        t.SetStylebox("panel", "TooltipPanel", Box(new Color("171a16"), Gold, 14));
        t.SetColor("font_color", "TooltipLabel", Paper);
        t.SetFontSize("font_size", "TooltipLabel", 15);
        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = new Color("6b5b3e"), Thickness = 1 });
        return t;
    }

    public static Label Text(string text, int size = 16, Color? color = null, bool title = false)
    {
        var l = new TooltipLabel { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeColorOverride("font_color", color ?? Paper);
        l.AddThemeFontSizeOverride("font_size", title ? (int)(size * 1.25f) : size);
        l.AddThemeFontOverride("font", title ? Title : Body);
        return l;
    }

    public static Label Wrapped(string text, int size = 16, Color? color = null)
    {
        var l = Text(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = new TooltipButton { Text = text, CustomMinimumSize = new Vector2(0, 40), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        if (primary)
        {
            b.AddThemeStyleboxOverride("normal", Box(Gold, new Color("dbbf8c")));
            b.AddThemeStyleboxOverride("hover", Box(new Color("ddc394"), Paper));
            b.AddThemeStyleboxOverride("pressed", Box(new Color("b3955f"), Paper));
            b.AddThemeColorOverride("font_color", Ink);
            b.AddThemeColorOverride("font_hover_color", Ink);
            b.AddThemeColorOverride("font_pressed_color", Ink);
        }
        b.Pressed += action;
        return b;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }
}
