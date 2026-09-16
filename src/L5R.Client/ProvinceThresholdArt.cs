using Godot;

namespace L5R.Client;

/// <summary>Approved concept pixels, sliced at runtime rather than redrawn as vector substitutes.</summary>
public static class ProvinceThresholdArt
{
    public const float Footer = 64;
    private static readonly Dictionary<string, Texture2D> Plates = new(StringComparer.Ordinal);
    private static readonly int[] NumberCenters = [543, 810, 1078, 1346];
    private static readonly Texture2D Glow = new GradientTexture2D
    {
        Width = 256, Height = 96, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1, .5f),
        Gradient = new Gradient { Colors = [new Color(1, .65f, .24f, .38f), new Color(1, .65f, .24f, .1f), new Color(1, .65f, .24f, 0)], Offsets = [0, .45f, 1] }
    };
    public static Texture2D Plate(string label, int? province)
    {
        var key = province is { } n ? $"province:{n}" : label;
        if (Plates.TryGetValue(key, out var cached)) return cached;
        var path = province is not null ? "res://Art/mat-fortress-thresholds.png" : "res://Art/mat-border-crests.png";
        var rect = province is not null ? new Rect2I(416, 351, 258, 355)
            : label == "Holdings" ? new Rect2I(73, 115, 333, 598) : new Rect2I(434, 115, 1020, 248);
        using var source = GD.Load<Texture2D>(path).GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        using var region = source.GetRegion(rect);
        if (province is { } number && number != 1)
        {
            // Reuse C's neutral first frame for every province. Its illustrated
            // fourth-frame highlight must never falsely signal live selection.
            // Only the approved ivory numeral varies; its circle/roof stay neutral.
            region.BlitRect(source, new Rect2I(NumberCenters[Math.Clamp(number - 1, 0, 3)] - 16, 651, 32, 38), new Vector2I(111, 300));
        }
        var pixels = region.GetData();
        // Gold/ivory pigment is warm; the navy reference cloth is cool. Keep
        // pigment RGB intact and remove only its background from the source atlas.
        // No recoloring, synthetic grain or extra desaturation of the approved art.
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = (byte)(255 * Math.Clamp((pixels[i] - pixels[i + 2] - 3f) / 24f, 0, 1));
        if (province is null && (label.StartsWith("Province ", StringComparison.Ordinal) || label=="Removed"))
        {
            // Battlefields retain their crossed-swords identity, not Home's house.
            for (var y = 0; y < 80; y++) for (var x = 70; x < 195; x++) pixels[(y * rect.Size.X + x) * 4 + 3] = 0;
        }
        using var image = Image.CreateFromData(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8, pixels);
        image.GenerateMipmaps();
        return Plates[key] = ImageTexture.CreateFromImage(image);
    }

    public static Rect2 IconBounds(Rect2 bounds, string label, bool connectionAbove = false)
    {
        if(label=="Removed") return new Rect2(bounds.Position+new Vector2(-6,-6),new Vector2(24,24));
        // A's icons interrupt the top stroke, near its left corner. No header row.
        if (label.StartsWith("Province ", StringComparison.Ordinal)) return new Rect2(bounds.GetCenter().X - 24, (connectionAbove ? bounds.End.Y - 20 : bounds.Position.Y - 26), 48, 48);
        var local = label == "Holdings" ? new Rect2(77 * .55f, 0, 56 * .55f, 75 * .55f)
            : new Rect2(71 * .55f, 0, 121 * .55f, 69 * .55f);
        return new Rect2(bounds.Position + local.Position, local.Size);
    }

    public static Rect2 ThresholdBounds(Rect2 bounds) => new(bounds.Position.X, bounds.End.Y - Footer, bounds.Size.X, Footer);

    public static void Draw(Control canvas, Vector2 size, string label, int? province, float active = 0, bool connectionAbove = false, bool connected = false)
    {
        if (province is null && label.StartsWith("Province ", StringComparison.Ordinal))
        { BattleInkArt.Court(canvas, size, active, connectionAbove); return; }
        var art = Plate(label, province);
        var source = art.GetSize();
        var isProvince = province is not null;
        var sx = isProvince ? 44f : label == "Holdings" ? 145f : 200f;
        var sy = isProvince ? 48f : 80f;
        var right = isProvince ? 35f : 60f;
        var bottom = isProvince ? 66f : 55f;
        var scale = isProvince ? size.X / source.X : .55f;
        float[] sourceX = [0, sx, source.X - right, source.X];
        float[] sourceY = [0, sy, source.Y - bottom, source.Y];
        var footHeight = isProvince ? bottom * size.X / source.X : bottom * scale;
        float[] destX = [0, sx * scale, size.X - right * scale, size.X];
        float[] destY = [0, sy * scale, size.Y - footHeight, size.Y];
        if (isProvince && active > .001f)
            canvas.DrawTextureRect(Glow, new Rect2(-8, size.Y - footHeight - 15, size.X + 16, footHeight + 24), false, new Color(1,1,1,active));
        var tint = Colors.White.Lerp(new Color(1.22f, 1.13f, 1.02f), active);
        for (var y = 0; y < 3; y++)
        {
            if (isProvince && y == 2)
            {
                // Keep the entire gate roof, circular number and supports in one
                // proportional image. Stretch only the plain side strokes above it.
                canvas.DrawTextureRectRegion(art, new Rect2(0, destY[2], size.X, footHeight),
                    new Rect2(0, sourceY[2], source.X, bottom), tint);
                continue;
            }
            for (var x = 0; x < 3; x++)
            {
                if (x == 1 && y == 1) continue; // transparent card space
                var dest = new Rect2(destX[x], destY[y], destX[x + 1] - destX[x], destY[y + 1] - destY[y]);
                var src = new Rect2(sourceX[x], sourceY[y], sourceX[x + 1] - sourceX[x], sourceY[y + 1] - sourceY[y]);
                if (isProvince && connected && connectionAbove && x == 1 && y == 0)
                {
                    // Open the top stroke where the concept's flared neck joins.
                    var left = size.X / 2 - 20 - dest.Position.X;
                    var rightStart = size.X / 2 + 20;
                    var ratio = src.Size.X / dest.Size.X;
                    canvas.DrawTextureRectRegion(art, new Rect2(dest.Position, new Vector2(left, dest.Size.Y)),
                        new Rect2(src.Position, new Vector2(left * ratio, src.Size.Y)), tint);
                    canvas.DrawTextureRectRegion(art, new Rect2(rightStart, dest.Position.Y, dest.End.X - rightStart, dest.Size.Y),
                        new Rect2(src.Position.X + (rightStart - dest.Position.X) * ratio, src.Position.Y, (dest.End.X - rightStart) * ratio, src.Size.Y), tint);
                }
                else canvas.DrawTextureRectRegion(art, dest, src, tint);
            }
        }
        if(label=="Removed") canvas.DrawTextureRect(RemovedPileArt.Seal,new Rect2(-6,-6,24,24),false);
    }
}
