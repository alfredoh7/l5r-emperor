using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A server-bound total beside the whole unit; shares token input/tooltip behavior.</summary>
public partial class UnitStandard : StatTokens
{
    public const float ReservedWidth = 74;
    private static Texture2D? _art;

    private Rect2 _body;
    public Vector2 MountOnViewport => ToViewport(Vector2.Zero);
    private Vector2 ToViewport(Vector2 p) => Projection?.Invoke(GetGlobalTransform() * p) ?? GetGlobalTransform() * p;
    public Vector2[] BodyCornersOnViewport() => new[] { _body.Position, new Vector2(_body.End.X, _body.Position.Y), _body.End, new Vector2(_body.Position.X, _body.End.Y) }.Select(ToViewport).ToArray();
    public Rect2 BodyRectOnViewport()
    {
        var p = BodyCornersOnViewport();
        return new Rect2(new Vector2(p.Min(v=>v.X), p.Min(v=>v.Y)), new Vector2(p.Max(v=>v.X)-p.Min(v=>v.X), p.Max(v=>v.Y)-p.Min(v=>v.Y)));
    }

    public override void PositionAtCard()
    {
        if (!IsInstanceValid(CardAnchor) || !CardAnchor.IsInsideTree()) { Hide(); return; }
        Visible = Card.UnitForce.HasValue && CardAnchor.IsVisibleInTree() && (Allowed?.Invoke() ?? true);
        if (!Visible) return;
        // This control lives beside the card INSIDE the table canvas. The same
        // perspective mesh projects cloth, fittings, numerals and card faces.
        var inverse = GetParent<Control>().GetGlobalTransform().AffineInverse();
        var face = CardAnchor.FaceCornersInCanvas().Select(p => inverse * p).ToArray();
        var right = Enumerable.Range(0,4).MaxBy(i => (face[i].X + face[(i+1)%4].X)/2);
        var edge = new[] { face[right], face[(right+1)%4] }.OrderBy(p=>p.Y).ToArray();
        var mount = edge[0].Lerp(edge[1], .14f);
        var h = Math.Max(12, CardAnchor.FaceHeight);
        var bodyX = mount.X + .05f;
        Position = mount;
        Rotation = (edge[1]-edge[0]).Angle() - Mathf.Pi/2;
        // Source art includes its crossbar. Align that crossbar with the mount.
        _body = new Rect2(new Vector2(bodyX-mount.X, -h*.042f), new Vector2(h*.32f,h*.81f));
        Size = new Vector2(_body.End.X, _body.Size.Y);
        QueueRedraw();
    }

    public override bool _HasPoint(Vector2 point) => _body.HasPoint(point);
    public override void _Draw()
    {
        if (Card.UnitForce is not int total) return;
        _art ??= GD.Load<Texture2D>("res://Art/unit-standard-mounted.png");
        var art = _art;
        // The generated source painted its transparency preview. Use a tight
        // UV silhouette, including the open bracket, rather than keying away
        // the cloth or brass highlights. All hardware is one source artwork.
        void Piece(params Vector2[] uv)
        {
            var vertices=uv.Select(p=>_body.Position+(p-new Vector2(192,48))/new Vector2(586,1490)*_body.Size).ToArray();
            DrawColoredPolygon(vertices,new Color(.86f,.84f,.80f),uv.Select(p=>p/art.GetSize()).ToArray(),art);
        }
        Piece(new(198,50),new(255,49),new(273,62),new(279,83),new(276,183),new(263,205),new(198,203),new(193,190),new(194,68));
        Piece(new(275,80),new(643,78),new(643,111),new(277,113));
        Piece(new(268,144),new(633,143),new(648,154),new(646,184),new(270,183));
        Piece(new(647,64),new(673,57),new(688,65),new(696,94),new(692,177),new(681,198),new(648,195),new(637,177),new(638,85));
        var cap=Enumerable.Range(0,48).Select(i=>new Vector2(729,106)+Vector2.FromAngle(i*Mathf.Tau/48)*new Vector2(47,57)).ToArray(); Piece(cap);
        Piece(new(269,183),new(648,183),new(674,196),new(674,1513),new(667,1523),new(652,1513),new(474,1324),new(286,1517),new(274,1523),new(269,1510));
        var value = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fontSize = Math.Max(3, (int)(_body.Size.Y * .31f));
        while (fontSize > 2 && Palette.Title.GetStringSize(value, fontSize: fontSize).X > _body.Size.X * .61f) fontSize--;
        var baseline = _body.Position.Y + _body.Size.Y * .60f + (Palette.Title.GetAscent(fontSize) - Palette.Title.GetDescent(fontSize)) / 2;
        DrawStringOutline(Palette.Title, new Vector2(_body.Position.X - _body.Size.X*.02f, baseline), value, HorizontalAlignment.Center, _body.Size.X, fontSize, 2, new Color("151b21"));
        DrawString(Palette.Title, new Vector2(_body.Position.X - _body.Size.X*.02f, baseline), value, HorizontalAlignment.Center, _body.Size.X, fontSize, Numeral);
        if (IsHovered() || HasFocus()) DrawLine(_body.Position + _body.Size * new Vector2(.27f,.77f), _body.Position + _body.Size * new Vector2(.73f,.77f), Palette.Gold, 1, true);
    }

    public static string ExplainUnit(CardView card) => $"{card.Definition?.Name}\nUnit Force {card.UnitForce}\n\nThe rules engine's current Force contribution for this entire unit.\nIncludes the Personality and qualifying Followers. Items are already included in Personality Force.\nBowed cards normally contribute no Force; Elite is accounted for by the engine.\n\nOrange: Personality Force {card.Force}\nGray: Chi {card.Chi}\nSpears: Unit Force {card.UnitForce}\n\nClick the standard to select this Personality.";
}
