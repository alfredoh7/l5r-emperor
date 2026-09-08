using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class CardTile : Button
{
    public CardView Card { get; private set; } = null!;
    public bool Highlighted { get; set; }
    public bool LegalTarget { get; set; }
    public bool ChosenTarget { get; set; }
    public bool? PreviousBowed { get; set; }
    private float _bowAngle;
    private float _bowDestination;
    public float VisualBowAngle => _bowAngle;
    public bool SelectedSource { get; set; }
    public bool Compact { get; set; }
    public float FaceHeight { get; set; }
    public float RestAngle { get; set; }
    public Func<Vector2, Vector2>? ProjectToScreen { get; set; }
    public bool ReducedMotion { get; set; }
    public int? ContextualGoldProduction { get; set; }
    public string ContextHelp { get; set; } = "";
    private static Texture2D? _atlas;
    private static Texture2D? _dynastyBack;
    private bool _hover;
    private float _lift;
    private float _hoverScale = 1;
    public float InteractionScale => _hoverScale;
    public bool IsInspectionHover => _hover || HasFocus();
    public bool IsSelected => Highlighted || SelectedSource || ChosenTarget;
    public static readonly Color HoverColor = new("ffda89");
    public static readonly Color SelectionColor = new("9ceafa");
    private static readonly Dictionary<string, Texture2D> PublishedArt = [];
    public static Texture2D? Artwork(CardDefinition definition)
    {
        if (definition.ScenarioOnly) return Atlas;
        if (definition.ArtworkKey is not string path) return null;
        if (!PublishedArt.TryGetValue(path, out var texture) && ResourceLoader.Exists(path)) PublishedArt[path] = texture = GD.Load<Texture2D>(path);
        return texture;
    }

    public CardTile() { }
    public CardTile(CardView card, bool compact, Action<CardView> clicked, Action<CardView> inspected)
    {
        Card = card;
        Compact = compact;
        Flat = true;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Material = CardPaper.Table;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = card.Definition is null ? "Face-down card. Its identity is hidden." : $"{card.Definition.Name}\n{card.Definition.Type} · {card.Definition.GoldCost} Gold\n{card.Definition.Text}";
        Pressed += () => clicked(Card);
        MouseEntered += () => { _hover = true; QueueRedraw(); inspected(Card); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
        FocusEntered += () => { QueueRedraw(); inspected(Card); };
        FocusExited += QueueRedraw;
        Resized += QueueRedraw;
    }

    public override void _Ready()
    {
        _bowDestination = Card.Bowed ? Mathf.Pi / 2 : 0;
        _bowAngle = (PreviousBowed ?? Card.Bowed) ? Mathf.Pi / 2 : 0;
        if (ReducedMotion) _bowAngle = _bowDestination;
    }

    public override void _Process(double delta)
    {
        AdvanceStatFeedback(delta);
        var inspecting = IsInspectionHover;
        var targetLift = inspecting ? Card.Zone == Zone.Hand ? 40f : ProjectToScreen is null ? 4f : 0f : 0f;
        var targetScale = inspecting ? Card.Zone == Zone.Hand ? 1.22f : 1.06f : 1f;
        ZIndex = _hover ? 30 : HasFocus() ? 20 : IsSelected ? 10 : 0;
        if (Mathf.IsEqualApprox(_bowAngle, _bowDestination) && Mathf.IsEqualApprox(_lift, targetLift) && Mathf.IsEqualApprox(_hoverScale, targetScale)) return;
        _bowAngle = ReducedMotion ? _bowDestination : Mathf.MoveToward(_bowAngle, _bowDestination, (float)delta * Mathf.Pi * 3);
        // Reduced motion preserves visible feedback and makes its transition immediate.
        _lift = ReducedMotion ? targetLift : Mathf.MoveToward(_lift, targetLift, (float)delta * 300);
        _hoverScale = ReducedMotion ? targetScale : Mathf.MoveToward(_hoverScale, targetScale, (float)delta * 1.65f);
        QueueRedraw();
    }

    private Vector2 FaceSize()
    {
        var height = FaceHeight > 0 ? FaceHeight : Math.Min(Size.X, Size.Y) - 12;
        var art = Card?.Definition is { ScenarioOnly: false } printed ? Artwork(printed) : null;
        return new Vector2(height * (art is null ? .7142857f : art.GetWidth() / (float)art.GetHeight()), height);
    }

    // The same projected quadrilateral drives drawing, hit testing and arrows.
    // Bow rotation takes place in the table plane before camera foreshortening.
    public bool AlignUnitRightEdge { get; set; }

    private Vector2 ProjectFacePoint(Vector2 point, Vector2 face)
    {
        var p = ((point - face / 2) * _hoverScale).Rotated(_bowAngle + RestAngle);
        var center = Size / 2;
        if (AlignUnitRightEdge)
        {
            var angle = _bowAngle + RestAngle;
            var extent = (Math.Abs(Mathf.Cos(angle)) * face.X + Math.Abs(Mathf.Sin(angle)) * face.Y) * _hoverScale / 2;
            center.X = Size.X - 6 - extent;
        }
        return center + new Vector2(0, -_lift) + p;
    }

    private Vector2[] LocalFaceCorners(float grow = 0)
    {
        var face = FaceSize();
        return new[] { new Vector2(-grow, -grow), new Vector2(face.X + grow, -grow), face + Vector2.One * grow, new Vector2(-grow, face.Y + grow) }
            .Select(p => ProjectFacePoint(p, face)).ToArray();
    }

    public override bool _HasPoint(Vector2 point)
    {
        if (Geometry2D.IsPointInPolygon(point, LocalFaceCorners(2))) return true;
        if (!_hover || Card.Zone != Zone.Hand) return false;
        // Keep the original pick area while lifting. Otherwise a pointer on the
        // bottom edge repeatedly enters/exits as the card moves away from it.
        return ContainsRestingFace(point);
    }

    internal bool ContainsRestingFace(Vector2 point)
    {
        var resting = (point - Size / 2).Rotated(-_bowAngle - RestAngle) + FaceSize() / 2;
        return new Rect2(Vector2.Zero, FaceSize()).Grow(2).HasPoint(resting);
    }

    public Vector2[] FaceCornersInCanvas() => LocalFaceCorners().Select(p => GetGlobalTransform() * p).ToArray();

    public Vector2[] FaceCornersOnViewport() => LocalFaceCorners().Select(p => ProjectToScreen?.Invoke(GetGlobalTransform() * p) ?? GetGlobalTransform() * p).ToArray();

    private void DrawInteractionHalo()
    {
        if (!IsSelected && !IsInspectionHover && !LegalTarget) return;
        var color = IsSelected ? SelectionColor : HoverColor;
        var strength = IsSelected ? 1f : IsInspectionHover ? .82f : .28f;
        var edge = LocalFaceCorners(2);
        var closed = edge.Append(edge[0]).ToArray();
        // Broad soft light with a bright core; no solid fill over the original scan.
        var perspectiveWeight = ProjectToScreen is null ? 1f : 2.2f;
        foreach (var (width, alpha) in new[] { (28f, .07f), (20f, .12f), (13f, .22f), (8f, .40f) })
            DrawPolyline(closed, color with { A = alpha * strength }, width * perspectiveWeight, true);
    }

    private void DrawInteractionEdge()
    {
        var active = IsSelected || IsInspectionHover || LegalTarget;
        var color = IsSelected ? SelectionColor : IsInspectionHover || LegalTarget ? HoverColor : new Color("8e866b");
        var edge = LocalFaceCorners(active ? 1.6f : 1);
        DrawPolyline(edge.Append(edge[0]).ToArray(), color with { A = active ? 1 : .58f }, active ? (IsSelected ? 3.2f : 2.3f) * (ProjectToScreen is null ? 1 : 2) : .9f, true);
    }

    private void DrawPrintedFace(Texture2D texture)
    {
        var corners = LocalFaceCorners();
        DrawInteractionHalo();
        for (var spread = 3; spread >= 1; spread--)
            DrawColoredPolygon(LocalFaceCorners(spread).Select(p => p + new Vector2(.6f, .8f + _lift * .5f)).ToArray(), new Color(.008f, .006f, .004f, .055f));
        DrawColoredPolygon(corners.Select(p => p + new Vector2(0, .7f)).ToArray(), new Color("a29174"));
        DrawColoredPolygon(corners, Colors.White, new Vector2[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down }, texture);
        DrawInteractionEdge();
        DrawStatInlays();
        DrawPublicStatus();
    }

    public string[] PublicStatusLabels => Card.Definition is null || Card.Zone is not (Zone.Home or Zone.Battlefield or Zone.Attached) ? [] :
        (Card.Dishonorable ? new[]{ "Dishonorable" } : Array.Empty<string>())
        .Concat((Card.GrantedKeywords ?? []).Select(k => "+ " + k.Keyword).Distinct()).ToArray();
    private void DrawPublicStatus()
    {
        var labels = PublicStatusLabels;
        if (labels.Length == 0) return;
        var face=FaceSize();
        var origin=ProjectFacePoint(Vector2.Zero,face);
        var x=(ProjectFacePoint(Vector2.Right,face)-origin);
        var y=(ProjectFacePoint(Vector2.Down,face)-origin);
        DrawSetTransformMatrix(new Transform2D(x,y,origin));
        // Two slim overlay bands across the lower artwork; never cover printed statistics/name/rules.
        var shown = labels.Take(2).ToArray();
        if(labels.Length>2) shown[1]+=$" · {labels.Length-2} more";
        for(var i=0;i<shown.Length;i++)
        {
            var rect=new Rect2(face.X*.035f,face.Y*.395f+i*face.Y*.08f,face.X*.93f,face.Y*.074f);
            var dishonorable=shown[i]=="Dishonorable";
            DrawStyleBox(Palette.Box(dishonorable?new Color("492924"):new Color("202f33"),dishonorable?new Color("b37c67"):Palette.Gold,0,1),rect);
            var size=Math.Max(11,(int)(face.Y*.06f));
            while(size>8 && Palette.Title.GetStringSize(shown[i],fontSize:size).X>rect.Size.X-6) size--;
            DrawString(Palette.Title,rect.Position+new Vector2(3,rect.Size.Y*.80f),shown[i],HorizontalAlignment.Center,rect.Size.X-6,size,Palette.Paper);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
    public static Texture2D? Atlas
    {
        get
        {
            if (_atlas is null && ResourceLoader.Exists("res://Art/training-atlas.png")) _atlas = GD.Load<Texture2D>("res://Art/training-atlas.png");
            return _atlas;
        }
    }

    public static Rect2 ArtRegion(CardDefinition def, Texture2D atlas)
    {
        if (!def.ScenarioOnly) return new Rect2(Vector2.Zero, atlas.GetSize());
        var quadrant = def.Type == CardType.Personality ? (def.Clan == "Lion" ? 1 : 0) : def.Type == CardType.Holding ? 2 : 3;
        var half = atlas.GetSize() / 2;
        return new Rect2(new Vector2(quadrant % 2 * half.X, quadrant / 2 * half.Y), half);
    }

    public static string UnitLabel(CardView card) => "Unit " + card.Id.Replace("card-", "").TrimStart('0');

    internal static string? LastPreviewCardId;
    internal static Control? LastPreviewControl;
    public override Control _MakeCustomTooltip(string forText) => HasMeta("flat_tooltip") ? null! : CreateReadingPreview();

    public Control CreateReadingPreview()
    {
        LastPreviewCardId = Card.Id;
        return LastPreviewControl = CardPreview.Create(Card, ContextHelp, ContextualGoldProduction, GetTree().Root.GetVisibleRect().Size.Y / 1080f);
    }

    public override void _Draw()
    {
        if (Card is null || Size.X <= 1 || Size.Y <= 1) return;
        // Reserve the same square footprint in either orientation, including the transition.
        var height = FaceHeight > 0 ? FaceHeight : Math.Min(Size.X, Size.Y) - 12;
        var original = Card.Definition is { ScenarioOnly: false } printed ? Artwork(printed) : null;
        if (original is not null) { DrawPrintedFace(original); return; }
        if (Card.Definition is null)
        {
            _dynastyBack ??= GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
            if (_dynastyBack is not null) { DrawPrintedFace(_dynastyBack); return; }
        }
        var ratio = original is null ? .7142857f : original.GetWidth() / (float)original.GetHeight();
        var faceSize = new Vector2(height * ratio, height);
        DrawInteractionHalo();
        DrawInteractionEdge();
        DrawSetTransformMatrix(new Transform2D(Vector2.Right, Vector2.Down, Size / 2 + new Vector2(0, -_lift)) * Transform2D.Identity.Rotated(_bowAngle + RestAngle).ScaledLocal(Vector2.One * _hoverScale).TranslatedLocal(-faceSize / 2));
        var r = new Rect2(Vector2.Zero, faceSize);
        var border = SelectedSource || ChosenTarget ? Palette.Crane : Highlighted || LegalTarget || _hover || HasFocus() ? Palette.Gold : new Color("8e866b");
        // Broad penumbra, close contact shadow, and a thin paper edge. The original
        // scan keeps its printed content; paper grading is a reversible material.
        for (var spread = 3; spread >= 1; spread--)
            DrawStyleBox(Palette.Box(new Color(.01f, .009f, .006f, .045f), null, 0, 3), new Rect2(new Vector2(2 + _lift * .3f, 3 + _lift * .6f), faceSize).Grow(spread));
        DrawStyleBox(Palette.Box(new Color(.012f, .01f, .006f, .48f), null, 0, 2), new Rect2(new Vector2(1.5f, 2.5f), faceSize));
        DrawRect(new Rect2(new Vector2(0, 1.4f), faceSize), new Color("b0a185"));
        if (Card.Definition is null)
        {
            _dynastyBack ??= GD.Load<Texture2D>("res://Art/original-dynasty-back.png");
            if (_dynastyBack is not null)
            {
                DrawTextureRect(_dynastyBack, r, false);
                if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || _hover || HasFocus()) DrawRect(r.Grow(2), border, false, 1.5f, true);
                return;
            }
            DrawStyleBox(Palette.Box(Palette.Ink, border, 0), r);
            DrawRect(new Rect2(7, 7, faceSize.X - 14, faceSize.Y - 14), new Color("4a624c"), false, 1);
            var center = faceSize / 2;
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.Tau / 5;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * faceSize.X * .11f;
                DrawArc(point, faceSize.X * .15f, 0, Mathf.Tau, 48, new Color("94885e"), 1.4f, true);
            }
            DrawString(Palette.Body, new Vector2(0, faceSize.Y - 14), "DYNASTY", HorizontalAlignment.Center, faceSize.X, height < 110 ? 8 : 11, Palette.PaperDim);
            return;
        }
        var d = Card.Definition;
        if (original is not null)
        {
            // Printed borders, statistics and rules are part of the original card.
            // Never crop, tint for bowing, or paint replacement text over the scan.
            DrawTextureRect(original, r, false);
            DrawRect(r.Grow(1), border with { A = .65f }, false, 1, true);
            if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || _hover || HasFocus())
            {
                DrawRect(r.Grow(3), border with { A = .20f }, false, 5, true);
                DrawRect(r.Grow(2), border, false, 1.6f, true);
            }
            return;
        }
        DrawStyleBox(Palette.Box(Palette.Paper, border, 0), r);
        var artHeight = height < 110 ? faceSize.Y * .34f : Compact ? faceSize.Y * .42f : faceSize.Y * .48f;
        var art = new Rect2(6, 6, faceSize.X - 12, artHeight);
        if (Artwork(d) is { } texture)
            DrawTextureRectRegion(texture, art, ArtRegion(d, texture), Card.Bowed ? new Color(.68f, .7f, .66f, 1) : Colors.White);
        else
        {
            DrawRect(art, new Color("6b7963"));
            for (var i = 0; i < 5; i++) DrawArc(art.GetCenter(), (i + 1) * 7, .2f, 5.8f, 44, Palette.PaperDim, 1, true);
        }
        if (height < 110)
        {
            var wordsSmall = d.Name.Split(' '); var smallLines = new List<string>(); var current = "";
            foreach (var word in wordsSmall)
            {
                var next = current.Length == 0 ? word : current + " " + word;
                if (Palette.Title.GetStringSize(next, HorizontalAlignment.Left, -1, 10).X > faceSize.X - 10 && current.Length > 0) { smallLines.Add(current); current = word; }
                else current = next;
            }
            if (current.Length > 0) smallLines.Add(current);
            for (var i = 0; i < Math.Min(2, smallLines.Count); i++) DrawString(Palette.Title, new Vector2(5, art.End.Y + 11 + i * 10), smallLines[i], HorizontalAlignment.Left, faceSize.X - 10, 10, Palette.Ink);
            if (d.GoldProduction > 0) DrawString(Palette.Body, new Vector2(5, faceSize.Y - 4), $"{ContextualGoldProduction ?? d.GoldProduction} GOLD", HorizontalAlignment.Left, faceSize.X - 10, 9, Palette.Ink);
            if (Card.Bowed) { DrawRect(new Rect2(3, 4, 36, 12), Palette.Ink); DrawString(Palette.Body, new Vector2(5, 13), "BOWED", HorizontalAlignment.Left, 34, 8, Palette.Paper); }
            if (Card.Zone is Zone.Hand or Zone.Province && d.GoldCost > 0)
            {
                DrawCircle(new Vector2(faceSize.X - 10, 12), 8, Palette.Gold);
                DrawString(Palette.Body, new Vector2(faceSize.X - 17, 16), d.GoldCost.ToString(), HorizontalAlignment.Center, 14, 10, Palette.Ink);
            }
            if (SelectedSource || ChosenTarget) { DrawCircle(new Vector2(faceSize.X - 9, 11), 7, Palette.Crane); DrawString(Palette.Body, new Vector2(faceSize.X - 14, 15), "✓", HorizontalAlignment.Center, 10, 10, Palette.Ink); }
            if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || HasFocus()) DrawRect(new Rect2(1, 1, faceSize.X - 2, faceSize.Y - 2), border, false, 2);
            return;
        }
        var nameSize = height < 110 ? 10 : Compact ? 12 : 15;
        var nameY = art.End.Y + (height < 110 ? 11 : Compact ? 16 : 20);
        var words = d.Name.Split(' ');
        var lines = new List<string>();
        var line = "";
        foreach (var word in words)
        {
            var proposed = line.Length == 0 ? word : line + " " + word;
            if (Palette.Title.GetStringSize(proposed, HorizontalAlignment.Left, -1, nameSize).X > faceSize.X - 14 && line.Length > 0) { lines.Add(line); line = word; }
            else line = proposed;
        }
        if (line.Length > 0) lines.Add(line);
        foreach (var text in lines.Take(2))
        {
            DrawString(Palette.Title, new Vector2(7, nameY), text, HorizontalAlignment.Left, faceSize.X - 14, nameSize, Palette.Ink);
            nameY += nameSize + 1;
        }
        if (!Compact)
            DrawString(Palette.Body, new Vector2(7, faceSize.Y - 40), d.Type.ToString().ToUpperInvariant(), HorizontalAlignment.Left, faceSize.X - 14, 10, new Color("69715f"));
        var statText = d.Type switch
        {
            CardType.Personality => $"{Card.Force} F   {Card.Chi} C   {d.PersonalHonor} H",
            CardType.Stronghold => $"{ContextualGoldProduction ?? d.GoldProduction} GOLD",
            CardType.Holding => $"{ContextualGoldProduction ?? d.GoldProduction} GOLD",
            _ => d.Type.ToString().ToUpperInvariant()
        };
        DrawLine(new Vector2(7, faceSize.Y - 27), new Vector2(faceSize.X - 7, faceSize.Y - 27), new Color("aea58a"), 1);
        DrawString(Palette.Body, new Vector2(7, faceSize.Y - 10), statText, HorizontalAlignment.Left, faceSize.X - 14, height < 110 ? 9 : Compact ? 11 : 13, Palette.Ink);
        if (Card.Zone is Zone.Province or Zone.Hand && d.GoldCost > 0)
        {
            DrawCircle(new Vector2(faceSize.X - 18, 18), 13, Palette.Gold);
            DrawString(Palette.Body, new Vector2(faceSize.X - 28, 23), d.GoldCost.ToString(), HorizontalAlignment.Center, 20, 14, Palette.Ink);
        }
        if (Card.Bowed)
        {
            DrawStyleBox(Palette.Box(Palette.Ink, null, 0), new Rect2(4, 8, Math.Min(62, faceSize.X - 8), 20));
            DrawString(Palette.Body, new Vector2(7, 22), "BOWED", HorizontalAlignment.Left, faceSize.X - 14, 11, Palette.Paper);
        }
        if (SelectedSource)
        {
            DrawCircle(new Vector2(faceSize.X - 15, 17), 11, Palette.Crane);
            DrawString(Palette.Body, new Vector2(faceSize.X - 24, 22), "✓", HorizontalAlignment.Center, 18, 14, Palette.Ink);
        }
        if (d.Type == CardType.Personality)
        {
            var label = UnitLabel(Card);
            DrawStyleBox(Palette.Box(Palette.Ink, null, 0), new Rect2(5, art.End.Y - 17, 63, 17));
            DrawString(Palette.Body, new Vector2(8, art.End.Y - 5), label, HorizontalAlignment.Left, 61, 10, Palette.Paper);
        }
        if (Highlighted || LegalTarget || ChosenTarget || SelectedSource || HasFocus()) DrawRect(new Rect2(1, 1, faceSize.X - 2, faceSize.Y - 2), border, false, 2);
    }
}
