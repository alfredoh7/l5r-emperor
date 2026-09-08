using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A server-bound total beside the whole unit; shares token input/tooltip behavior.</summary>
public partial class UnitStandard : StatTokens
{
    public const float ReservedWidth = 74;
    private static Texture2D? _art;

    public override void PositionAtCard()
    {
        if (!IsInstanceValid(CardAnchor) || !CardAnchor.IsInsideTree()) { Hide(); return; }
        Visible = Card.UnitForce.HasValue && CardAnchor.IsVisibleInTree() && (Allowed?.Invoke() ?? true);
        if (!Visible) return;
        var inverse = GetParent<Control>().GetGlobalTransform().AffineInverse();
        var face = CardAnchor.FaceCornersOnViewport().Select(p => inverse * p).ToArray();
        var all = Companions.Where(IsInstanceValid).Append(CardAnchor)
            .SelectMany(c => c.FaceCornersOnViewport()).Select(p => inverse * p).ToArray();
        var h = Math.Max(12, face.Max(p => p.Y) - face.Min(p => p.Y));
        Size = new Vector2(h * .40f, h * .82f);
        // The farthest right attachment determines the edge, including bowed attachments.
        Position = new Vector2(all.Max(p => p.X) + 2, face.Min(p => p.Y) + h * .06f);
        QueueRedraw();
    }

    public override bool _HasPoint(Vector2 point) => new Rect2(Vector2.Zero, Size).HasPoint(point);
    public override void _Draw()
    {
        if (Card.UnitForce is not int total) return;
        _art ??= GD.Load<Texture2D>("res://Art/unit-standard.png");
        DrawTextureRect(_art, new Rect2(Vector2.Zero, Size), false);
        var value = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fontSize = Math.Max(3, (int)(Size.Y * .31f));
        while (fontSize > 2 && Palette.Title.GetStringSize(value, fontSize: fontSize).X > Size.X * .50f) fontSize--;
        var baseline = Size.Y * .59f + (Palette.Title.GetAscent(fontSize) - Palette.Title.GetDescent(fontSize)) / 2;
        DrawStringOutline(Palette.Title, new Vector2(0, baseline), value, HorizontalAlignment.Center, Size.X, fontSize, 2, new Color("151b21"));
        DrawString(Palette.Title, new Vector2(0, baseline), value, HorizontalAlignment.Center, Size.X, fontSize, Numeral);
        if (IsHovered() || HasFocus()) DrawLine(new Vector2(Size.X * .27f, Size.Y * .77f), new Vector2(Size.X * .73f, Size.Y * .77f), Palette.Gold, 1, true);
    }

    public static string ExplainUnit(CardView card) => $"{card.Definition?.Name}\nUnit Force {card.UnitForce}\n\nThe rules engine's current Force contribution for this entire unit.\nIncludes the Personality and qualifying Followers. Items are already included in Personality Force.\nBowed cards normally contribute no Force; Elite is accounted for by the engine.\n\nOrange: Personality Force {card.Force}\nGray: Chi {card.Chi}\nSpears: Unit Force {card.UnitForce}\n\nClick the standard to select this Personality.";
}
