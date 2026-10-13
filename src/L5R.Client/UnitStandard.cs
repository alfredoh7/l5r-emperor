using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A server-bound total beside the whole unit; shares token input/tooltip behavior.</summary>
public partial class UnitStandard : StatTokens
{
    public const float ReservedWidth = 26;
    private static Texture2D? _art;

    private static readonly Vector2[][] Silhouettes = [
        [new(447,119),new(482,119),new(488,124),new(488,247),new(483,254),new(473,259),new(447,264)]
    ];
    private Vector2 FromSource(Vector2 p) => _body.Position + (p - new Vector2(447, 119)) / new Vector2(41, 145) * _body.Size;
    public IEnumerable<Vector2[]> ArtworkPolygonsOnViewport() => Silhouettes.Select(shape => shape.Select(p => ToViewport(FromSource(p))).ToArray());

    private Rect2 _body;
    private Rect2? _attackSlot;
    private float _attackClearance;
    public Func<Vector2, Vector2>? Unprojection { get; set; }
    public float AttackSlidePixels { get; private set; }

    public void ReserveAttackSpace(Rect2? slot, float progress)
    {
        _attackSlot = slot; _attackClearance = progress;
        PositionAtCard();
    }

    public Vector2 MountOnViewport => ToViewport(Vector2.Zero);
    private Vector2 ToViewport(Vector2 p) => Projection?.Invoke(GetGlobalTransform() * p) ?? GetGlobalTransform() * p;
    public Vector2[] BodyCornersOnViewport() => new[] { _body.Position, new Vector2(_body.End.X, _body.Position.Y), _body.End, new Vector2(_body.Position.X, _body.End.Y) }.Select(ToViewport).ToArray();
    public Rect2 BodyRectOnViewport()
    {
        var p = BodyCornersOnViewport();
        return new Rect2(new Vector2(p.Min(v => v.X), p.Min(v => v.Y)), new Vector2(p.Max(v => v.X) - p.Min(v => v.X), p.Max(v => v.Y) - p.Min(v => v.Y)));
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
        var right = Enumerable.Range(0, 4).MaxBy(i => (face[i].X + face[(i + 1) % 4].X) / 2);
        var edge = new[] { face[right], face[(right + 1) % 4] }.OrderBy(p => p.Y).ToArray();
        var mount = edge[0];
        var h = Math.Max(12, CardAnchor.FaceHeight);
        var bodyX = mount.X + .05f;
        Position = mount;
        Rotation = (edge[1] - edge[0]).Angle() - Mathf.Pi / 2;
        // C is a short card-mounted tab; its top is flush with the card corner.
        _body = new Rect2(new Vector2(bodyX - mount.X, 0), new Vector2(h * .31f * 41 / 145, h * .31f));
        Size = new Vector2(_body.End.X, _body.Size.Y);
        AttackSlidePixels = 0;
        if (_attackSlot is { } slot && _attackClearance > 0 && Unprojection is not null)
        {
            var resting = BodyRectOnViewport();
            var clearance = Math.Max(0, slot.End.Y - resting.Position.Y);
            if (clearance > 0)
            {
                // Move along the card's actual edge, then inverse-project back into
                // the table. Cloth, spears, value and input all move as one fitting.
                var start = ToViewport(Vector2.Zero);
                var alongEdge = ToViewport(Vector2.Down * h) - start;
                var target = start + alongEdge * (clearance / Math.Max(1, alongEdge.Y));
                var travel = inverse * Unprojection(target) - inverse * Unprojection(start);
                Position += travel * _attackClearance;
                AttackSlidePixels = BodyRectOnViewport().Position.Y - resting.Position.Y;
            }
        }
        QueueRedraw();
    }

    public override bool _HasPoint(Vector2 point) => _body.HasPoint(point);
    private static Texture2D? _spears, _canvas;
    public override void _Draw()
    {
        if (Card.UnitForce is not int total) return;
        _art ??= GD.Load<Texture2D>("res://Art/unit-ribbon-reference.png");
        _spears ??= GD.Load<Texture2D>("res://Art/card-token-hardware.png");
        _canvas ??= GD.Load<Texture2D>("res://Art/unit-standard-mounted.png");
        foreach (var shape in Silhouettes)
            DrawColoredPolygon(shape.Select(FromSource).ToArray(), Colors.White, shape.Select(p => p / _art.GetSize()).ToArray(), _art);
        // Reuse C's actual outline and curved divider; replace only baked semantic pixels.
        Rect2 Patch(float x, float y, float w, float h) => new(FromSource(new Vector2(x, y)), new Vector2(w / 41 * _body.Size.X, h / 145 * _body.Size.Y));
        // Restore the earlier woven canvas using a full 2D cloth swatch, without
        // stretching a five-pixel row. Keep C's short silhouette and gold rim.
        Vector2[] cloth = [new(450, 122), new(480, 122), new(485, 126), new(485, 245), new(481, 251), new(471, 256), new(450, 260)];
        DrawColoredPolygon(cloth.Select(FromSource).ToArray(), new Color(.66f, .70f, .76f),
            cloth.Select(p => (new Vector2(360, 680) + (p - new Vector2(450, 122)) / new Vector2(35, 138) * new Vector2(142, 560)) / _canvas.GetSize()).ToArray(), _canvas);
        Vector2[] divider = [new(450, 197), new(484, 191), new(484, 199), new(475, 207), new(460, 209), new(450, 206)];
        DrawColoredPolygon(divider.Select(FromSource).ToArray(), new Color(.88f, .88f, .88f), divider.Select(p => p / _art.GetSize()).ToArray(), _art);
        Vector2[][] spearShapes = [
            [new(679,255),new(701,335),new(686,347),new(686,480),new(680,496),new(674,480),new(674,348),new(657,344)],
            [new(733,210),new(757,310),new(742,325),new(742,333),new(753,334),new(753,343),new(741,343),new(741,484),new(734,500),new(727,483),new(727,343),new(715,343),new(715,334),new(727,333),new(726,325),new(710,314)],
            [new(787,255),new(806,335),new(794,348),new(794,482),new(788,496),new(782,482),new(782,347),new(767,344)]
        ];
        var icon = Patch(453, 132, 29, 58);
        foreach (var shape in spearShapes)
            DrawColoredPolygon(shape.Select(p => icon.Position + (p - new Vector2(657, 210)) / new Vector2(150, 290) * icon.Size).ToArray(), Colors.White, shape.Select(p => p / _spears.GetSize()).ToArray(), _spears);
        var value = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var size = Math.Max(4, (int)(_body.Size.Y * .28f));
        while (size > 3 && Palette.Body.GetStringSize(value, fontSize: size).X > _body.Size.X * .80f) size--;
        var baseline = _body.Position.Y + _body.Size.Y * .76f + (Palette.Body.GetAscent(size) - Palette.Body.GetDescent(size)) / 2;
        DrawStringOutline(Palette.Body, new Vector2(_body.Position.X, baseline), value, HorizontalAlignment.Center, _body.Size.X, size, 1, new Color("10202d"));
        DrawString(Palette.Body, new Vector2(_body.Position.X, baseline), value, HorizontalAlignment.Center, _body.Size.X, size, Numeral);
        if (IsHovered() || HasFocus()) DrawPolyline(BodyCornersLocal().Append(BodyCornersLocal()[0]).ToArray(), Palette.Gold with { A = .7f }, 1, true);
    }
    private Vector2[] BodyCornersLocal() => [_body.Position, new(_body.End.X, _body.Position.Y), _body.End, new(_body.Position.X, _body.End.Y)];

    public static string ExplainUnit(CardView card) => $"{card.Definition?.Name}\nUnit Force {card.UnitForce}\n\nCurrent battle contribution for this entire unit.\nIncludes the Personality and qualifying Followers. Items are already included in Personality Force.\nEach card counts on its own: a bowed Personality or Follower contributes 0 unless it is Elite, and a bowed Personality does not stop his unbowed Followers from contributing.\nElite cards keep contributing while bowed.\n\nOrange: Personality Force {card.Force}\nGray: Chi {card.Chi}\nSpears: Unit Force {card.UnitForce}\n\nClick the standard to select this Personality.";
}
