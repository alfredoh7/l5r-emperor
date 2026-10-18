using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>B's card list and reading panel. Navigation has no gameplay command callback.</summary>
public partial class FallenGallery : Control
{
    public static readonly Vector2 DesignSize = new(1077, 590);
    public int OwnerId { get; private set; }
    public string? SelectedCardId { get; private set; }
    public IReadOnlyList<FallenRow> Rows => _rows;
    public IReadOnlyList<FallenButton> Tabs => _tabs;
    public CardView? SelectedCard => _players.Single(p => p.Id == OwnerId).Discard.FirstOrDefault(c => c.Id == SelectedCardId && FallenArt.IsFallen(c));
    public ScrollContainer ListScroll => _scroll;
    public Control Design => _design;
    public Button CloseButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public event Action? Close;
    /// <summary>Cards the player may choose now (issue #965): their rows are marked and a click chooses the card instead of only showing it.</summary>
    public Func<string, bool>? Actionable { get; set; }
    public event Action<string>? Chosen;
    private PlayerView[] _players;
    private readonly Control _design = new() { Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<FallenRow> _rows = [];
    private readonly List<FallenButton> _tabs = [];
    private readonly ScrollContainer _scroll;
    private readonly VBoxContainer _list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly TextureRect _face;
    private readonly FallenSurface _surface = new() { Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore };
    private readonly FallenMedallion _medallion = new() { Position = new Vector2(840, 127), Size = new Vector2(152, 152), MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _status, _name, _identity, _empty, _hint;
    public FallenGallery(PlayerView[] players, int owner)
    {
        _players = players; OwnerId = owner; Theme = SidebarSkin.Theme(); MouseFilter = MouseFilterEnum.Stop;
        AddChild(_design);
        _design.AddChild(_surface);
        PutLabel("FALLEN CARDS", new Rect2(28, 18, 780, 49), 34, true);
        CloseButton = Button("×", new Rect2(1009, 19, 42, 36), () => Close?.Invoke(), 26);
        CloseButton.AccessibilityName = "Close Dead cards";
        for (var i = 0; i < players.Length; i++)
        {
            var id = players[i].Id;
            var tab = new FallenButton { Position = new Vector2(27 + i * 192, 66), Size = new Vector2(180, 49), Tab = true, FontSize = 21 };
            tab.Pressed += () => SelectPlayer(id); _design.AddChild(tab); _tabs.Add(tab);
        }
        _scroll = new ScrollContainer
        {
            Position = new Vector2(28, 125),
            Size = new Vector2(408, 417),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _list.AddThemeConstantOverride("separation", 6); _scroll.AddChild(_list); _design.AddChild(_scroll);
        _face = new TextureRect
        {
            Position = new Vector2(495, 105),
            Size = new Vector2(307, 409),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = CardPaper.Reading
        };
        _design.AddChild(_face); _design.AddChild(_medallion);
        _status = PutLabel("", new Rect2(826, 276, 210, 94), 31);
        _name = PutLabel("", new Rect2(826, 374, 209, 73), 23);
        _name.MouseFilter = MouseFilterEnum.Pass;
        _identity = PutLabel("", new Rect2(826, 445, 209, 65), 19);
        _empty = PutLabel("", new Rect2(496, 230, 515, 115), 26);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _hint = PutLabel("Select a card to inspect", new Rect2(501, 545, 299, 30), 19);
        BackButton = Button("Back to table", new Rect2(824, 526, 214, 43), () => Close?.Invoke());
        Resized += Fit; Populate();
    }
    public override void _Ready() => Fit();
    private void Fit()
    {
        var s = Math.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _design.Scale = Vector2.One * s; _design.Position = (Size - DesignSize * s) / 2;
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Close?.Invoke(); GetViewport().SetInputAsHandled(); }
    }
    public void Refresh(PlayerView[] players)
    {
        if (ReferenceEquals(players, _players)) return;
        _players = players; if (!players.Any(p => p.Id == OwnerId)) OwnerId = players[0].Id; Populate();
    }
    public void SelectPlayer(int owner)
    {
        if (owner == OwnerId) return;
        OwnerId = owner; SelectedCardId = null; _scroll.ScrollVertical = 0; Populate();
    }
    private void Populate()
    {
        foreach (var row in _rows) { _list.RemoveChild(row); row.QueueFree(); }
        _rows.Clear();
        var player = _players.Single(p => p.Id == OwnerId);
        for (var i = 0; i < _tabs.Count; i++)
        {
            var p = _players[i]; _tabs[i].Caption = $"{p.Clan}  {p.DeadCount}"; _tabs[i].Selected = p.Id == OwnerId;
            _tabs[i].AccessibilityName = $"{p.Clan} · {p.DeadCount} Dead"; _tabs[i].QueueRedraw();
        }
        var cards = FallenArt.Cards(player);
        if (!cards.Any(c => c.Id == SelectedCardId)) SelectedCardId = cards.FirstOrDefault()?.Id;
        foreach (var card in cards)
        {
            var row = new FallenRow(card, () => { if (Actionable?.Invoke(card.Id) == true) Chosen?.Invoke(card.Id); else SelectCard(card.Id); }) { Legal = Actionable?.Invoke(card.Id) == true };
            _rows.Add(row); _list.AddChild(row);
        }
        ShowSelected();
    }
    public void SelectCard(string id)
    {
        if (!FallenArt.Cards(_players.Single(p => p.Id == OwnerId)).Any(c => c.Id == id)) return;
        SelectedCardId = id; ShowSelected();
    }
    private void ShowSelected()
    {
        var card = SelectedCard; var player = _players.Single(p => p.Id == OwnerId);
        foreach (var row in _rows) { row.Selected = row.Card.Id == SelectedCardId; row.QueueRedraw(); }
        _face.Texture = card?.Definition is { } def ? CardTile.Artwork(def) : null;
        _face.Visible = _medallion.Visible = _status.Visible = _name.Visible = _identity.Visible = card is not null;
        _surface.HasSelection = _hint.Visible = card is not null; _surface.QueueRedraw();
        _empty.Visible = card is null || _face.Texture is null;
        _empty.Text = card is not null ? "Card image unavailable" : player.DeadCount == 0 ? "No fallen Personalities." : "No public card faces available.";
        if (card is null) return;
        _medallion.Dishonorable = card.Dishonorable; _medallion.QueueRedraw();
        _status.Text = FallenArt.DeathLabel(card);
        FitName(_name, card.Definition!.Name, new Vector2(209, 73));
        _identity.Text = player.Clan + " · Dead\nPersonality";
    }
    private Label PutLabel(string text, Rect2 bounds, int size, bool heading = false)
    {
        var label = Palette.Wrapped(text, size, Palette.Paper); if (heading) label.AddThemeFontOverride("font", Palette.HeadingLabel(size));
        label.Position = bounds.Position; label.Size = bounds.Size; label.MouseFilter = MouseFilterEnum.Ignore;
        label.ClipText = true; _design.AddChild(label); return label;
    }
    internal static void FitName(Label label, string text, Vector2 bounds)
    {
        var size = 23;
        while (size > 15 && Palette.Body.GetMultilineStringSize(text, width: bounds.X, fontSize: size).Y > bounds.Y - 6) size--;
        label.AddThemeFontSizeOverride("font_size", size); label.Text = text;
        label.Size = bounds; label.ClipText = true; label.MaxLinesVisible = 3;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.TooltipText = text;
    }
    private FallenButton Button(string label, Rect2 bounds, Action action, int size = 20)
    {
        var b = new FallenButton { Caption = label, Position = bounds.Position, Size = bounds.Size, FontSize = size, AccessibilityName = label };
        b.Pressed += action; _design.AddChild(b); return b;
    }
    private partial class FallenSurface : Control
    {
        public bool HasSelection { get; set; }
        public override void _Draw()
        {
            FallenArt.Frame(this, new Rect2(Vector2.Zero, Size));
            DrawLine(new Vector2(26, 61), new Vector2(1050, 61), new Color("7d633c"), 1);
            DrawLine(new Vector2(462, 101), new Vector2(462, 528), new Color("917647"), 1);
            DrawTextureRectRegion(FallenArt.B, new Rect2(454, 302, 16, 16), new Rect2(1022, 396, 16, 16));
            if (HasSelection)
            {
                foreach (var y in new[] { 371f, 444f }) DrawLine(new Vector2(826, y), new Vector2(1030, y), new Color("7d633c"), 1);
                DrawTextureRectRegion(FallenArt.B, new Rect2(920, 436, 16, 16), new Rect2(1482, 496, 16, 16));
            }
        }
    }
}

public partial class FallenRow : Button
{
    public CardView Card { get; }
    public bool Selected { get; set; }
    public bool Legal { get; set; }
    private readonly TextureRect _face;
    private readonly Label _name, _status;
    private readonly FallenMedallion _icon;
    public FallenRow(CardView card, Action selected)
    {
        Card = card; Flat = true; FocusMode = FocusModeEnum.All; MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(0, 133); SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = card.Definition!.Name + " · " + FallenArt.DeathLabel(card);
        TooltipText = card.Definition.Name;
        foreach (var s in new[] { "normal", "pressed", "hover", "focus" }) AddThemeStyleboxOverride(s, new StyleBoxEmpty());
        _face = new TextureRect
        {
            Texture = CardTile.Artwork(card.Definition),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(12, 6),
            Size = new Vector2(86, 121),
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Material = CardPaper.Reading
        };
        AddChild(_face);
        _name = Palette.Wrapped(card.Definition.Name, 23); _name.Position = new Vector2(112, 15); _name.MouseFilter = MouseFilterEnum.Ignore; AddChild(_name);
        _icon = new FallenMedallion { Small = true, Dishonorable = card.Dishonorable, Position = new Vector2(112, 82), Size = new Vector2(41, 34), MouseFilter = MouseFilterEnum.Ignore }; AddChild(_icon);
        _status = Palette.Wrapped(FallenArt.DeathLabel(card), 19); _status.Position = new Vector2(160, 82); _status.MouseFilter = MouseFilterEnum.Ignore; AddChild(_status);
        Pressed += selected; MouseEntered += QueueRedraw; MouseExited += QueueRedraw; FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        Resized += () => { FallenGallery.FitName(_name, card.Definition.Name, new Vector2(Math.Max(50, Size.X - 129), 64)); _status.Size = new Vector2(Math.Max(50, Size.X - 169), 47); QueueRedraw(); };
    }
    public override void _Draw()
    {
        if (!Selected && !IsHovered() && !HasFocus() && !Legal) return;
        var color = Selected && Card.Dishonorable ? FallenArt.Dishonor : Palette.Gold;
        DrawRect(new Rect2(2, 2, Size.X - 4, Size.Y - 4), color with { A = .15f }, false, 5, true);
        DrawRect(new Rect2(2, 2, Size.X - 4, Size.Y - 4), color, false, 1.2f, true);
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
}
