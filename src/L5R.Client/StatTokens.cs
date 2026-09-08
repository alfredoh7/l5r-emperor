using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Public Personality values, never a calculation of legality or army strength.</summary>
public partial class StatTokens : Button
{
    public const float ReservedHeight = 112;
    public CardTile CardAnchor { get; set; } = null!;
    public CardTile[] Companions { get; set; } = [];
    public CardView Card { get; set; } = null!;
    public bool Inline { get; set; }
    public string Explanation { get; set; } = "";
    public Func<bool>? Allowed { get; set; }
    public Func<Vector2, Vector2>? Projection { get; set; }
    private static Texture2D? _sheet;
    private float _diameter = 58;
    public float Diameter => _diameter;
    public static readonly Color Numeral = new("deded7");

    public StatTokens()
    {
        Flat = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _Process(double delta) => PositionAtCard();

    public void PositionAtCard()
    {
        if (!IsInstanceValid(CardAnchor) || !CardAnchor.IsInsideTree()) { Hide(); return; }
        Visible = CardAnchor.IsVisibleInTree() && (Allowed?.Invoke() ?? true);
        if (!Visible || Inline) return;
        var inverse = GetParent<Control>().GetGlobalTransform().AffineInverse();
        var center = CardAnchor.GetGlobalRect().GetCenter();
        var sample = CardAnchor.GetGlobalTransform() * (CardAnchor.Size / 2 + Vector2.Right * 58);
        var screenWidth = Projection is null ? 58 :
            (inverse * Projection(sample)).DistanceTo(inverse * Projection(center));
        // Screen-space discs keep circular silhouettes and upright type through bowing.
        var verticalSample = CardAnchor.GetGlobalTransform() * (CardAnchor.Size / 2 + Vector2.Down * 80);
        var screenClearance = Projection is null ? 58 : Math.Abs(
            (inverse * Projection(verticalSample)).Y - (inverse * Projection(center)).Y);
        // Far rows compress more vertically than horizontally. Respect the reserved
        // depth as well as card width instead of allowing a circular badge to cover
        // the next row at a steep camera angle.
        _diameter = Math.Max(4, Math.Min(86, Math.Min(screenWidth, screenClearance)));
        Size = new Vector2(_diameter * 2.12f, _diameter + 4);
        var stackFaces = Companions.Where(IsInstanceValid).Append(CardAnchor)
            .Select(c => c.FaceCornersOnViewport().Select(p => inverse * p).ToArray());
        var lowestFace = stackFaces.MaxBy(face => face.Max(p => p.Y))!;
        // Anchor at the near edge rather than the bounding-box center: perspective
        // shifts the bottom of cards sideways near the table's outer columns.
        var nearEdge = lowestFace.OrderByDescending(p => p.Y).Take(2).ToArray();
        Position = new Vector2(nearEdge.Average(p => p.X) - Size.X / 2,
            lowestFace.Max(p => p.Y) + 4);
        QueueRedraw();
    }

    public void SetInlineSize(float diameter)
    {
        Inline = true; _diameter = diameter;
        CustomMinimumSize = new Vector2(diameter * 2.12f, diameter + 4);
    }

    public override bool _HasPoint(Vector2 point) => Enumerable.Range(0, 2).Any(i => point.DistanceTo(new Vector2(_diameter * (.5f + i * 1.12f), _diameter / 2)) <= _diameter / 2);
    public override Control _MakeCustomTooltip(string forText)
    {
        var shell = ReadingTooltip.Create(this, forText);
        var panel = shell.GetChild<Control>(0);
        // Long effect lists must reserve the label's real line height, so the
        // native popup positions the whole explanation inside the viewport.
        void MeasureContent()
        {
            var actual = panel.GetCombinedMinimumSize() * panel.Scale;
            shell.CustomMinimumSize = new Vector2(Math.Max(shell.CustomMinimumSize.X, actual.X),
                Math.Max(shell.CustomMinimumSize.Y, actual.Y));
        }
        shell.Ready += MeasureContent;
        panel.MinimumSizeChanged += MeasureContent;
        return shell;
    }

    public override void _Draw()
    {
        if (Card?.Definition is null) return;
        _sheet ??= GD.Load<Texture2D>("res://Art/stat-token-sheet.png");
        for (var i = 0; i < 2; i++)
        {
            var center = new Vector2(_diameter * (.5f + i * 1.12f), _diameter / 2);
            DrawCircle(center + new Vector2(0, 2), _diameter * .505f, new Color(0, 0, 0, .42f));
            // The generator's background is not alpha: sample only the real metal disc,
            // with a circular mesh. No checkerboard can enter the runtime silhouette.
            var sourceCenter = new Vector2(i == 0 ? 414 : 1123, 488);
            var vertices = new Vector2[96]; var uv = new Vector2[96];
            for (var j = 0; j < vertices.Length; j++)
            {
                var direction = Vector2.FromAngle(j * Mathf.Tau / vertices.Length);
                vertices[j] = center + direction * _diameter / 2;
                uv[j] = (sourceCenter + direction * 345) / _sheet.GetSize();
            }
            DrawColoredPolygon(vertices, Colors.White, uv, _sheet);
            if (IsHovered() || HasFocus()) DrawArc(center, _diameter * .48f, 0, Mathf.Tau, 80, Numeral with { A = .45f }, 1, true);
            var label = _diameter >= 34 ? (i == 0 ? "FORCE" : "CHI") : (i == 0 ? "F" : "C");
            var labelSize = Math.Max(6, (int)(_diameter * .16f));
            var value = (i == 0 ? Card.Force : Card.Chi).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var fontSize = (int)(_diameter * .56f);
            while (fontSize > 2 && Palette.Body.GetStringSize(value, fontSize: fontSize).X > _diameter * .7f) fontSize--;
            var left = center.X - _diameter / 2;
            var baseline = center.Y + _diameter * .30f;
            DrawStringOutline(Palette.Body, new Vector2(left, baseline), value, HorizontalAlignment.Center, _diameter, fontSize, Math.Max(2, (int)(_diameter * .035f)), new Color("24201c"));
            DrawStringOutline(Palette.Body, new Vector2(left, baseline), value, HorizontalAlignment.Center, _diameter, fontSize, 1, Numeral);
            DrawString(Palette.Body, new Vector2(left, baseline), value, HorizontalAlignment.Center, _diameter, fontSize, Numeral);
            if (_diameter >= 18)
            {
                DrawStringOutline(Palette.Body, new Vector2(left, center.Y - _diameter * .20f), label, HorizontalAlignment.Center, _diameter, labelSize, 2, new Color("24201c"));
                DrawString(Palette.Body, new Vector2(left, center.Y - _diameter * .20f), label, HorizontalAlignment.Center, _diameter, labelSize, Numeral);
            }
        }
    }

    public static string Explain(CardView card, IEnumerable<CardView> visibleCards)
    {
        if (card.Definition is not { } def) return "Face-down card. Identity hidden.";
        var cards = visibleCards.ToArray();
        var lines = new List<string> { def.Name, $"Current Force {card.Force}  ·  Chi {card.Chi}", $"Printed Force {def.Force}  ·  Chi {def.Chi}", "" };
        foreach (var item in cards.Where(c => c.AttachedTo == card.Id && c.Definition?.Type == CardType.Item))
            lines.Add($"{item.Definition!.Name}: {item.Definition.Force:+0;-0;0} Force, {item.Definition.Chi:+0;-0;0} Chi");
        foreach (var modifier in card.Modifiers ?? [])
        {
            var source = cards.FirstOrDefault(c => c.Id == modifier.Source)?.Definition?.Name ?? "Card ability";
            lines.Add($"{source}: {modifier.Amount:+0;-0;0} {modifier.Stat}");
        }
        foreach (var keyword in card.GrantedKeywords ?? []) lines.Add($"{keyword.Keyword} · {keyword.Duration}");
        lines.Add("");
        lines.Add("These are the Personality's current values.");
        lines.Add("Followers contribute separately to its unit.");
        if (card.Bowed) lines.Add("Bowed: Force value and battle contribution are different.");
        lines.Add("Conditional card effects are included in the current values.");
        lines.Add("Click either token to select this Personality.");
        return string.Join("\n", lines);
    }
}
