using Godot;

namespace L5R.Client;

/// <summary>Extracts approved button pixels without the sheet's opaque exterior matte.</summary>
internal static class ButtonArtwork
{
    internal static Texture2D Extract(Texture2D sheet, Rect2I region, int rim, float scale = 1)
    {
        var image = sheet.GetImage().GetRegion(region);
        image.Convert(Image.Format.Rgba8);
        var width = image.GetWidth();
        var height = image.GetHeight();
        var visited = new bool[width * height];
        var exterior = new Queue<Vector2I>();
        for (var x = 0; x < width; x++) { Visit(x, 0); Visit(x, height - 1); }
        for (var y = 1; y < height - 1; y++) { Visit(0, y); Visit(width - 1, y); }
        while (exterior.TryDequeue(out var point))
        {
            image.SetPixelv(point, Colors.Transparent);
            Visit(point.X - 1, point.Y); Visit(point.X + 1, point.Y);
            Visit(point.X, point.Y - 1); Visit(point.X, point.Y + 1);
        }
        // Only boundary-connected matte is removed. Never key out dark lacquer
        // enclosed by the gold frame, or traverse beyond the asset's border band.
        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height || visited[y * width + x]) return;
            visited[y * width + x] = true;
            if (x >= rim && x < width - rim && y >= rim && y < height - rim) return;
            var color = image.GetPixel(x, y);
            if (Math.Max(color.R, Math.Max(color.G, color.B)) < .15f || color.A == 0)
                exterior.Enqueue(new Vector2I(x, y));
        }
        // Extend edge RGB into transparent pixels before filtering so scaling
        // the nine-slice does not mix the black sheet back into its gold edge.
        image.FixAlphaEdges();
        if (scale != 1)
            image.Resize((int)Math.Round(width * scale), (int)Math.Round(height * scale), Image.Interpolation.Lanczos);
        return ImageTexture.CreateFromImage(image);
    }
}
