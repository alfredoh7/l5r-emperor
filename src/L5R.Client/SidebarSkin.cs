using Godot;
namespace L5R.Client;

/// <summary>Approved lacquer artwork with native scalable controls and live text.</summary>
public static class SidebarSkin
{
    private static Texture2D? _atlas;
    private static Texture2D Atlas
    {
        get
        {
            if (_atlas is not null) return _atlas;
            var image = GD.Load<Texture2D>("res://Art/Sidebar/lacquer-atlas.png").GetImage();
            image.Resize(768, 512, Image.Interpolation.Lanczos);
            return _atlas = ImageTexture.CreateFromImage(image);
        }
    }
    public static Font Bold => GD.Load<FontFile>("res://Art/Fonts/Caudex-Bold.ttf");
    public static Font Italic => GD.Load<FontFile>("res://Art/Fonts/Caudex-Italic.ttf");
    public static StyleBoxTexture Surface(bool button = false, bool gold = false, Color? tint = null)
    {
        var region = button ? new Rect2(718, gold ? 387 : 127, 781, 158) : new Rect2(36, 39, 631, 940);
        var box = new StyleBoxTexture { Texture = new AtlasTexture { Atlas = Atlas, Region = new Rect2(region.Position / 2, region.Size / 2) },
            ModulateColor = tint ?? Colors.White,
            ContentMarginLeft = button ? 22 : 26, ContentMarginRight = button ? 22 : 26,
            ContentMarginTop = button ? 16 : 28, ContentMarginBottom = button ? 16 : 28 };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
        { box.SetTextureMargin(side, button ? 17 : 27); box.SetExpandMargin(side, 0); }
        // Scale the asset once before nine-slicing so corners retain the concept's proportions.
        box.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch;
        box.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch;
        return box;
    }
    public static Theme Theme()
    {
        var t = Palette.MakeTheme();
        t.SetFont("font", "Button", Bold); t.SetFontSize("font_size", "Button", 19);
        t.SetStylebox("normal", "Button", Surface(true));
        t.SetStylebox("hover", "Button", Surface(true, tint: new Color(1.2f,1.15f,1.05f)));
        t.SetStylebox("pressed", "Button", Surface(true, tint: new Color(.8f,.8f,.8f)));
        t.SetStylebox("disabled", "Button", Surface(true, tint: new Color(.55f,.55f,.55f)));
        t.SetStylebox("focus", "Button", Palette.Box(Colors.Transparent, Palette.Paper, 0, 0));
        t.SetColor("font_disabled_color", "Button", new Color("a49d8d"));
        t.SetStylebox("normal", "OptionButton", Surface(true));
        t.SetStylebox("hover", "OptionButton", Surface(true));
        t.SetStylebox("panel", "PopupMenu", Surface());
        return t;
    }
    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = Palette.Button(text, action); b.CustomMinimumSize = new Vector2(0, 60);
        b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (primary)
        {
            b.AddThemeStyleboxOverride("normal", Surface(true,true));
            b.AddThemeStyleboxOverride("hover", Surface(true,true,new Color(1.1f,1.08f,1)));
            b.AddThemeStyleboxOverride("pressed", Surface(true,true,new Color(.85f,.85f,.85f)));
            b.AddThemeColorOverride("font_color", new Color("17130c"));
            b.AddThemeColorOverride("font_hover_color", new Color("17130c"));
            b.AddThemeColorOverride("font_pressed_color", new Color("17130c"));
        }
        return b;
    }
}
