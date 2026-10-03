using Godot;
namespace L5R.Client;

internal static class DuelDecisionArt
{
    // Dedicated ImageGen derivatives of approved 1A/2B; crop transparent exterior with UVs.
    // Only the plain center/edge runs stretch. Ornament proportions are kept by equal border scaling.
    internal static StyleBoxTexture Frame(bool tray)
    {
        var texture = GD.Load<Texture2D>(tray ? "res://Art/DuelUi/focus-tray-frame.png" : "res://Art/DuelUi/concede-frame.png");
        var region = tray ? new Rect2(32, 113, 1973, 530) : new Rect2(74, 78, 1731, 680);
        var style = new StyleBoxTexture { Texture = new AtlasTexture { Atlas = texture, Region = region } };
        var border = tray ? 170 : 175;
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetTextureMargin(side, border);
        // Native margins use the same ratio as the source's ornamental corners.
        style.SetExpandMarginAll(0);
        style.ContentMarginLeft = style.ContentMarginRight = 36;
        style.ContentMarginTop = style.ContentMarginBottom = 24;
        style.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit;
        style.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit;
        return style;
    }
    private static readonly Dictionary<bool, StyleBoxTexture> Frames = [];
    internal static void Background(Control parent, bool tray)
    {
        var panel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (!Frames.TryGetValue(tray, out var style))
        {
            style = Frame(tray);
            // Texture margins are source pixels; scale the whole texture once before nine-slicing.
            var atlas = (AtlasTexture)style.Texture;
            var image = atlas.GetImage(); image.Resize((int)(image.GetWidth() * .45f), (int)(image.GetHeight() * .45f), Image.Interpolation.Lanczos); image.GenerateMipmaps();
            style.Texture = ImageTexture.CreateFromImage(image);
            foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetTextureMargin(side, (tray ? 170 : 175) * .45f);
            Frames[tray] = style;
        }
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
}
