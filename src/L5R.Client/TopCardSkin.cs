using Godot;

namespace L5R.Client;

/// <summary>Raster furniture derived from the selected destination-tray concept.</summary>
public static class TopCardSkin
{
    private static Texture2D Atlas => GD.Load<Texture2D>("res://Art/TopCards/approved-trays-atlas.png");
    public static Texture2D Brush => new AtlasTexture { Atlas = Atlas, Region = new Rect2(38, 518, 1002, 140) };
    public static Texture2D Slot => new AtlasTexture { Atlas = Atlas, Region = new Rect2(1060, 416, 448, 578) };
    public static StyleBoxTexture Frame(bool inset = false, Color? tint = null)
    {
        var style = new StyleBoxTexture
        {
            Texture = new AtlasTexture { Atlas = Atlas, Region = new Rect2(26, 36, 1486, 378) },
            ModulateColor = tint ?? Colors.White,
            ContentMarginLeft = inset ? 14 : 30,
            ContentMarginRight = inset ? 14 : 30,
            ContentMarginTop = inset ? 14 : 24,
            ContentMarginBottom = inset ? 14 : 24
        };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetTextureMargin(side, 32);
        return style;
    }
}
