using Godot;

namespace L5R.Client;

/// <summary>Concept A's actual lacquer and wave ornaments, with C's number hierarchy and live public values.</summary>
public partial class BattleValueRail : Control
{
    public const float Height = 64;
    public int Force { get; }
    public int Units { get; }
    public int OwnerId { get; }
    public string Role { get; }
    // Draw commands retain texture RIDs; keep their resources alive across frames and GC.
    private static readonly Texture2D LeftArt = GD.Load<Texture2D>("res://Art/BattleValues/rail-left.png");
    private static readonly Texture2D RunArt = GD.Load<Texture2D>("res://Art/BattleValues/rail-run.png");
    private static readonly Texture2D RightArt = GD.Load<Texture2D>("res://Art/BattleValues/rail-right.png");
    private static readonly Texture2D CoinArt = GD.Load<Texture2D>("res://Art/Payments/plain-gold-coin.png");

    public BattleValueRail(string clan, int owner, string role, int force, int units, float width)
    {
        Force = force; Units = units; OwnerId = owner; Role = role;
        MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(width, Height);
        var emblem = ClanPlaque.Emblem(clan, new Vector2(40, 40));
        emblem.Position = new Vector2(70, 12); AddChild(emblem);
        Text(clan.ToUpperInvariant(), new Rect2(118, 8, width - 422, 26), 20);
        Text(role.ToUpperInvariant(), new Rect2(118, 32, width - 422, 21), 13, Palette.PaperDim);
        var fx = width - 302;
        Text("FORCE", new Rect2(fx, 22, 62, 20), 13, Palette.PaperDim);
        Text(force.ToString(), new Rect2(fx + 64, 4, 48, 56), force.ToString().Length > 2 ? 25 : 34, new Color("21170d"), true);
        Text("UNITS", new Rect2(width - 156, 10, 46, 17), 12, Palette.PaperDim, true);
        Text(units.ToString(), new Rect2(width - 156, 28, 46, 29), 23, null, true);
    }

    private void Text(string value, Rect2 rect, int size, Color? color = null, bool centered = false)
    {
        var label = Palette.Text(value, size, color ?? Palette.Paper);
        label.AddThemeFontOverride("font", ClanPlaque.BodyFace);
        while (size > 12 && ClanPlaque.BodyFace.GetStringSize(value, fontSize: size).X > rect.Size.X - 4) size--;
        label.AddThemeFontSizeOverride("font_size", size); label.ClipText = true;
        label.Position = rect.Position; label.Size = rect.Size;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        label.MouseFilter = MouseFilterEnum.Ignore; AddChild(label);
    }

    public override void _Draw()
    {
        // Preserve end-cap proportions. Only the blank center run stretches.
        var cap = Height * 58 / 45;
        DrawTextureRect(LeftArt, new Rect2(0, 0, cap, Height), false);
        DrawTextureRect(RunArt, new Rect2(cap, 0, Size.X - 2 * cap, Height), false);
        DrawTextureRect(RightArt, new Rect2(Size.X - cap, 0, cap, Height), false);
        DrawTextureRect(CoinArt,
            new Rect2(Size.X - 242, 4, 56, 56), false, new Color(.74f, .65f, .51f));
        DrawLine(new Vector2(Size.X - 169, 13), new Vector2(Size.X - 169, 51), new Color("947b4c"), 1);
    }
}
