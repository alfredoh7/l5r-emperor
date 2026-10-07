using Godot;

namespace L5R.Client;

/// <summary>The clan placards' own chalkboard plate and brass frame, drawn for every battle placard. One size and one column grid; text stays inside the frame.</summary>
internal static class PlacardPlate
{
    public const float StackWidth = 340, StackHeight = 68;
    // The frame art is 1740x600 with ~95/60 px borders; drawn at this scale the border is 15 x 9.6 px.
    public const float FrameScale = .16f, BorderX = 15, BorderY = 10, Inset = 4;
    // Grid shared by the clan placards and the Province placard so the stack lines up column for column.
    public const float IconX = 21, IconSize = 36, TitleX = 64, TitleWidth = 104, ValueX = 172, ValueWidth = 70, SecondValueX = 246;
    private static readonly StyleBoxTexture Frame = new()
    {
        Texture = ClanPlaque.FrameTexture,
        TextureMarginLeft = 95,
        TextureMarginRight = 95,
        TextureMarginTop = 60,
        TextureMarginBottom = 60
    };

    /// <summary>The area inside the frame's border where text and icons may sit.</summary>
    public static Rect2 Inner(Vector2 size) => new(BorderX + Inset, BorderY + Inset, size.X - 2 * (BorderX + Inset), size.Y - 2 * (BorderY + Inset));

    public static void Draw(CanvasItem item, Vector2 size, bool offered, bool hovered)
    {
        item.DrawSetTransform(Vector2.Zero, 0, Vector2.One * FrameScale);
        item.DrawStyleBox(Frame, new Rect2(Vector2.Zero, size / FrameScale));
        item.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (offered || hovered) item.DrawRect(new Rect2(BorderX - 3, BorderY - 3, size.X - 2 * BorderX + 6, size.Y - 2 * BorderY + 6), offered ? Palette.Gold : new Color(Palette.Gold, .6f), false, 2);
    }

    public static Label Text(Control parent, string value, Rect2 rect, int size, Color? color, bool centered)
    {
        var label = Palette.Text(value, size, color ?? Palette.Paper);
        label.AddThemeFontOverride("font", ClanPlaque.BodyFace);
        while (size > 11 && ClanPlaque.BodyFace.GetStringSize(value, fontSize: size).X > rect.Size.X - 2) size--;
        label.AddThemeFontSizeOverride("font_size", size); label.ClipText = true;
        // A label never shrinks below its font's line height; keep it centred on the requested row so it cannot cross the frame.
        var height = Math.Max(rect.Size.Y, label.GetCombinedMinimumSize().Y);
        label.Position = new Vector2(rect.Position.X, rect.Position.Y + (rect.Size.Y - height) / 2); label.Size = new Vector2(rect.Size.X, height);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        label.MouseFilter = Control.MouseFilterEnum.Ignore; parent.AddChild(label);
        return label;
    }

    /// <summary>A small caption over a large number, the same block for Force, Units and Strength.</summary>
    public static void Block(Control parent, string caption, string number, float x, Color numberColor)
    {
        Text(parent, caption, new Rect2(x, 18, ValueWidth - 8, 14), 11, Palette.PaperDim, true);
        Text(parent, number, new Rect2(x, 24, ValueWidth - 8, 29), number.Length > 2 ? 19 : 22, numberColor, true);
    }
}

/// <summary>Province number and Strength placard. A press behaves exactly like pressing the province; the katana is the engine's own "falls if both pass" fact.</summary>
public partial class ProvincePlacard : Button
{
    public const float TableWidth = 164, TableHeight = 72, StackWidth = PlacardPlate.StackWidth, StackHeight = PlacardPlate.StackHeight;
    private static readonly Texture2D KatanaArt = GD.Load<Texture2D>("res://Art/BattleValues/province-destroyed.png");
    public int Province { get; }
    public int Strength { get; }
    public bool Falls { get; }
    public bool Offered { get; }

    public ProvincePlacard(int province, int strength, bool falls, bool offered, float width, float height, bool stacked)
    {
        Province = province; Strength = strength; Falls = falls; Offered = offered;
        Flat = true; FocusMode = FocusModeEnum.None; Text = ""; Size = new Vector2(width, height);
        TooltipText = $"Province {province + 1} · Strength {strength}" + (falls ? " · the attacker's Force is enough to destroy it if both players pass" : "");
        if (stacked)
        {
            PlacardPlate.Text(this, "PROVINCE", new Rect2(PlacardPlate.TitleX, 15, PlacardPlate.TitleWidth, 22), 19, null, false);
            PlacardPlate.Text(this, $"NO. {province + 1}", new Rect2(PlacardPlate.TitleX, 33, PlacardPlate.TitleWidth, 16), 12, Palette.PaperDim, false);
            PlacardPlate.Block(this, "STRENGTH", strength.ToString(), PlacardPlate.ValueX, Palette.Gold);
            if (falls) AddIcon(new Vector2(PlacardPlate.SecondValueX + (PlacardPlate.ValueWidth - 8 - 40) / 2f, (height - 40) / 2), 40); // aligned with the Units column of the placards above and below
            return;
        }
        var inner = PlacardPlate.Inner(Size);
        PlacardPlate.Text(this, $"PROVINCE {province + 1}", new Rect2(inner.Position.X, inner.Position.Y, 90, 22), 12, Palette.PaperDim, false);
        PlacardPlate.Text(this, "STRENGTH", new Rect2(inner.Position.X, inner.Position.Y + 22, 62, 22), 12, Palette.PaperDim, false);
        PlacardPlate.Text(this, strength.ToString(), new Rect2(inner.Position.X + 62, inner.Position.Y + 15, 32, 29), 20, Palette.Gold, true);
        if (falls) AddIcon(new Vector2(inner.End.X - 30, inner.Position.Y + (inner.Size.Y - 30) / 2), 30);
    }

    private void AddIcon(Vector2 position, float side) => AddChild(new TextureRect
    {
        Texture = KatanaArt,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore,
        Name = "ProvinceDestroyedIcon",
        Position = position,
        Size = new Vector2(side, side)
    });

    public override void _Draw() => PlacardPlate.Draw(this, Size, Offered, IsHovered());

    public override void _Notification(int what)
    {
        if (what is (int)NotificationMouseEnter or (int)NotificationMouseExit) QueueRedraw();
    }
}

/// <summary>Corner placard for the engaged battle: clan, role, live Force and unit count on the shared plate.</summary>
public partial class BattleSeal : Control
{
    public const float Width = PlacardPlate.StackWidth, Height = PlacardPlate.StackHeight;
    public int OwnerId { get; }
    public int Force { get; }
    public int Units { get; }
    public string Role { get; }

    public BattleSeal(string clan, int owner, string role, int force, int units)
    {
        OwnerId = owner; Role = role; Force = force; Units = units;
        MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(Width, Height);
        var emblem = ClanPlaque.Emblem(clan, new Vector2(PlacardPlate.IconSize, PlacardPlate.IconSize));
        emblem.Position = new Vector2(PlacardPlate.IconX, (Height - PlacardPlate.IconSize) / 2); AddChild(emblem);
        PlacardPlate.Text(this, clan.ToUpperInvariant(), new Rect2(PlacardPlate.TitleX, 15, PlacardPlate.TitleWidth, 22), 19, null, false);
        PlacardPlate.Text(this, role.ToUpperInvariant(), new Rect2(PlacardPlate.TitleX, 33, PlacardPlate.TitleWidth, 16), 12, Palette.PaperDim, false);
        PlacardPlate.Block(this, "FORCE", force.ToString(), PlacardPlate.ValueX, Palette.Gold);
        PlacardPlate.Block(this, "UNITS", units.ToString(), PlacardPlate.SecondValueX, Palette.Paper);
    }

    public override void _Draw() => PlacardPlate.Draw(this, Size, false, false);
}
