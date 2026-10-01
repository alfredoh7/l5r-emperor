using Godot;
namespace L5R.Client;

/// <summary>The approved open gallery and on-demand inspector, inside the existing game window.</summary>
public static class LibraryDialog
{
    public static void Show(Node parent)
    {
        if (parent.GetTree().Root.GetNodeOrNull<CanvasLayer>("CatalogueLayer") is not null) return;
        var layer = new CanvasLayer { Name = "CatalogueLayer", Layer = 80 }; parent.GetTree().Root.AddChild(layer);
        try
        {
            var screen = new CatalogueScreen(); layer.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            screen.Closed += layer.QueueFree;
        }
        catch (Exception e) { layer.QueueFree(); GD.PushError("Catalogue: " + e.Message); var alert = new AcceptDialog { Theme = SidebarSkin.Theme(), DialogText = "The catalogue could not be loaded. Please verify the game content bundle.", Title = "Card library" }; parent.AddChild(alert); alert.CloseRequested += alert.QueueFree; alert.Confirmed += alert.QueueFree; alert.Canceled += alert.QueueFree; alert.PopupCentered(); }
    }
}

public partial class CatalogueScreen : Control
{
    public event Action? Closed;
    public Action? OpenBuilder;
    public CatalogueData Data { get; } = new();
    public CatalogueScans Scans { get; } = new();
    public CatalogueGrid Grid { get; private set; } = null!;
    public LineEdit Search { get; private set; } = null!;
    public Rect2 InspectorRect => _detail.GetGlobalRect();
    public string SelectedId => _selected?.Id ?? "";
    public CatalogueBrowserBar Browser { get; private set; } = null!;
    private readonly Control _canvas = new(), _header = new(), _detail = new();
    private Control _backdrop = null!, _detailFrame = null!;
    private Button _back = null!, _closeDetail = null!;
    private Label _count = null!, _footer = null!;
    private ScrollContainer _detailScroll = null!;
    private VBoxContainer _detailBody = null!;
    private TextureRect? _face;
    private Label? _scanState;
    private CatalogueData.Card? _selected;
    private CatalogueData.Printing? _printing;
    private float _w, _h;
    private int _browseScroll;
    private bool _previousEmbed;
    public override void _Ready()
    {
        _previousEmbed = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        Theme = CatalogueArt.Theme(); MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.All;
        _backdrop = CatalogueArt.Backdrop(); AddChild(_backdrop); _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_canvas); _canvas.AddChild(_header);
        Grid = new CatalogueGrid(Scans); _canvas.AddChild(Grid); Grid.Selected = SelectCard;
        _canvas.AddChild(_detail); _detailFrame = CatalogueArt.Backdrop(); _detail.AddChild(_detailFrame); _detailFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _detail.Visible = false;
        _back = CatalogueArt.Button("‹  Back", () => Closed?.Invoke()); Put(_header, _back, 28, 17, 130, 44); _back.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        Put(_header, CatalogueArt.Label("Card library", 44, true), 202, 12, 390, 60);
        var builder = CatalogueArt.Button("Deck Builder", () =>
        {
            if (OpenBuilder is not null) OpenBuilder();
            else
            {
                var root = GetTree().Root;
                // Retire the previous overlay's viewport settings before opening the next one.
                TreeExited += () => Callable.From(() => DeckBuilderDialog.Show(root)).CallDeferred();
                Closed?.Invoke();
            }
        }); builder.Name = "OpenBuilder"; _header.AddChild(builder);
        var more = new MenuButton { Name = "LibraryActions", Icon = DeckSkin.Icon("more"), IconAlignment = HorizontalAlignment.Center, TooltipText = "Imported decks and source archive", AccessibilityName = "More library actions" }; _header.AddChild(more);
        var menu = more.GetPopup(); menu.AddItem("Imported decks and source archive", 0); menu.IdPressed += _ => LegacyLibraryDialog.Show(this);
        Browser = new CatalogueBrowserBar(Data, ["All", "Dynasty", "Fate", "Strongholds"]); _canvas.AddChild(Browser); Search = Browser.Search;
        Browser.EditCollection += () => MyCollection.Show(this, Data, Scans, Browser);
        Browser.Changed += ApplyFilter; Browser.PopupBounds = () => Grid.GetGlobalRect();
        Browser.CardSizeChanged += width => { Grid.CardWidth = width; Grid.LayoutCards(); };
        Browser.CardSize.DragStarted += Grid.BeginCardResize; Browser.CardSize.DragEnded += _ => Grid.EndCardResize();
        Grid.CardWidth = (float)Browser.CardSize.Value;
        _count = CatalogueArt.Label("", 16); _canvas.AddChild(_count);
        _footer = CatalogueArt.Label("Click a card to inspect  ·  Esc to return", 16); _footer.HorizontalAlignment = HorizontalAlignment.Center; _canvas.AddChild(_footer);
        _closeDetail = CatalogueArt.Button("×", CloseInspector); _closeDetail.TooltipText = "Close card details · Esc"; Put(_detail, _closeDetail, 0, 0, 44, 44);
        _detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _detail.AddChild(_detailScroll);
        _detailBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _detailBody.AddThemeConstantOverride("separation", 12); _detailScroll.AddChild(_detailBody);
        Resized += Fit; Scans.Changed += UpdateFace; TreeExiting += Scans.Close; TreeExiting += () => GetViewport().GuiEmbedSubwindows = _previousEmbed;
        ApplyFilter(); Fit(); GrabFocus();
    }
    private static void Put(Control parent, Control child, float x, float y, float w, float h) { parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    public void ApplyFilter()
    {
        if (Grid is null || Browser is null) return;
        Browser.Refresh(false); var results = Browser.Results;
        if (!Grid.Cards.SequenceEqual(results)) Grid.SetCards(results);
        _count.Text = $"{results.Length:N0} results · {Data.Cards.Length:N0} catalogue cards";
        _footer.Text = results.Length == 0 ? "No matching cards · Clear filters or try a shorter search" : "Click a card to inspect · Esc to return";
        if (_selected is not null && !results.Any(c => c.Id == _selected.Id)) CloseInspector();
    }
    public void ResetFilters() => Browser.Reset();
    private void Fit()
    {
        if (Grid is null || Size.X < 100) return;
        var scale = Math.Min(1f, Size.X / 1440f); scale = Math.Max(.5f, scale);
        _canvas.Scale = Vector2.One * scale; _w = Size.X / scale; _h = Size.Y / scale; _canvas.Size = new Vector2(_w, _h);
        _header.Size = new Vector2(_w, 78);
        var builder = _header.GetNode<Control>("OpenBuilder"); builder.Position = new Vector2(_w - 222, 20); builder.Size = new Vector2(180, 42);
        var more = _header.GetNode<Control>("LibraryActions"); more.Position = new Vector2(_w - 278, 20); more.Size = new Vector2(44, 42);
        Browser.Position = new Vector2(36, 86); Browser.Size = new Vector2(_w - 72, CatalogueBrowserBar.BarHeight);
        var dw = Math.Clamp(_w * .25f, 350, 430); var galleryWidth = _w - 72 - (_detail.Visible ? dw + 28 : 0);
        var gridTop = 86 + CatalogueBrowserBar.BarHeight + 18;
        Grid.Position = new Vector2(28, gridTop); Grid.Size = new Vector2(galleryWidth + 16, _h - gridTop - 52);
        _detail.Position = new Vector2(_w - dw - 28, gridTop); _detail.Size = new Vector2(dw, _h - gridTop - 52);
        _closeDetail.Position = new Vector2(dw - 62, 15); _closeDetail.Size = new Vector2(42, 42);
        _detailScroll.Position = new Vector2(22, 65); _detailScroll.Size = new Vector2(dw - 44, _detail.Size.Y - 88);
        _detailBody.CustomMinimumSize = new Vector2(dw - 64, 0);
        if (_face is not null) _face.CustomMinimumSize = new Vector2(0, Math.Min((dw - 70) * 1.4f, _detail.Size.Y * .75f));
        _count.Position = new Vector2(36, _h - 38); _count.Size = new Vector2(380, 27);
        _footer.Position = new Vector2(425, _h - 38); _footer.Size = new Vector2(_w - 461, 27);
        Grid.LayoutCards(); QueueRedraw();
    }
    public override void _Draw()
    {
        var s = _canvas.Scale.X;
        DrawLine(new Vector2(25, 78 * s), new Vector2((_w - 25) * s, 78 * s), new Color(.56f, .43f, .22f, .6f), 1);
    }
    public void SelectCard(CatalogueData.Card card)
    {
        if (_selected?.Id == card.Id) { CloseInspector(); return; }
        if (!_detail.Visible) _browseScroll = Grid.ScrollVertical;
        _selected = card; _printing = card.Printings.FirstOrDefault(); _detail.Visible = true; Grid.Select(card.Id); BuildInspector(); Fit(); Callable.From(() => { if (_selected?.Id == card.Id) Grid.EnsureCard(card.Id); }).CallDeferred();
    }
    public void CloseInspector()
    { _selected = null; _printing = null; _detail.Visible = false; Grid.Select(null); Fit(); Grid.ScrollVertical = _browseScroll; Grid.GrabFocus(); }
    private void BuildInspector()
    {
        if (_selected is null) return; var card = _selected; Palette.Clear(_detailBody);
        foreach (var label in _detail.GetChildren().OfType<Label>().ToArray()) { _detail.RemoveChild(label); label.QueueFree(); }
        Put(_detail, CatalogueArt.Label("Card details", 26, true), 22, 20, 275, 34);
        _face = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.LinearWithMipmaps }; _detailBody.AddChild(_face);
        _scanState = CatalogueArt.Label("", 15); _scanState.AutowrapMode = TextServer.AutowrapMode.WordSmart; _detailBody.AddChild(_scanState);
        var status = CatalogueArt.Label(card.Playable ? "✓  Game version playable" : "Not yet playable", 18); status.HorizontalAlignment = HorizontalAlignment.Center;
        var statusPanel = new PanelContainer(); statusPanel.AddThemeStyleboxOverride("panel", Palette.Box(card.Playable ? new Color("203327") : new Color("2e2720"), new Color("776644"), 8)); statusPanel.AddChild(status); _detailBody.AddChild(statusPanel);
        _detailBody.AddChild(CatalogueArt.Label(card.Name, 23));
        var printing = new OptionButton { FitToLongestItem = false }; foreach (var p in card.Printings) printing.AddItem(p.Label);
        if (card.Printings.Length == 0) { printing.AddItem("No scan indexed"); printing.Disabled = true; }
        printing.ItemSelected += i => { _printing = card.Printings[(int)i]; UpdateFace(); }; _detailBody.AddChild(printing);
        AddDisclosure("Rules text", () =>
        {
            var body = new VBoxContainer(); body.AddChild(Palette.Wrapped("Current Oracle reference", 14, CatalogueArt.Gold)); body.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(card.CurrentOracleText) ? "No rules text recorded in the catalogue." : CatalogueData.PlainText(card.CurrentOracleText), 18, CatalogueArt.Ivory));
            if (card.Definition is { } def) { body.AddChild(new HSeparator()); body.AddChild(Palette.Wrapped("Game printing / implemented profile", 14, CatalogueArt.Gold)); body.AddChild(Palette.Wrapped(def.Text.Length > 0 ? def.Text : "No printed rules text in the implemented definition.", 18, CatalogueArt.Ivory)); }
            body.AddChild(Palette.Wrapped("Oracle wording can include later errata. Alternate scans do not change the game's pinned printing.", 14, CatalogueArt.Muted)); return body;
        });
        AddDisclosure("Printing details", () =>
        {
            var body = new VBoxContainer(); body.AddChild(Palette.Wrapped(string.Join(" · ", card.Types.Concat(card.Clans)), 17, CatalogueArt.Ivory));
            body.AddChild(Palette.Wrapped(string.Join(" · ", card.Keywords), 16, CatalogueArt.Gold));
            body.AddChild(Palette.Wrapped(string.Join("   ", card.Stats.Select(s => s.Key + " " + string.Join(" / ", s.Value))), 16, CatalogueArt.Ivory));
            body.AddChild(Palette.Wrapped("Catalogue membership: Age of Conquest (Emperor). Scan selection is a reference, not certification of historical legality.", 14, CatalogueArt.Muted));
            var source = CatalogueArt.Button("Open Oracle source ↗", () => { if (card.SourceUrl.StartsWith("https://oracleofthevoid.com/", StringComparison.Ordinal)) OS.ShellOpen(card.SourceUrl); }); body.AddChild(source);
            var scan = CatalogueArt.Button("Open selected scan source ↗", () => { if (_printing?.SourceUrl.StartsWith("https://images.oracleofthevoid.com/", StringComparison.Ordinal) == true) OS.ShellOpen(_printing.SourceUrl); }); body.AddChild(scan);
            var retry = CatalogueArt.Button("Retry missing scan", () => { Scans.Retry(); UpdateFace(); }); body.AddChild(retry); return body;
        });
        _detailScroll.ScrollVertical = 0; UpdateFace();
    }
    private void AddDisclosure(string label, Func<Control> create)
    {
        var box = new VBoxContainer { Visible = false }; var button = CatalogueArt.Button(label + "  ›", () => { if (box.GetChildCount() == 0) box.AddChild(create()); box.Visible = !box.Visible; if (box.Visible) Callable.From(() => _detailScroll.EnsureControlVisible(box)).CallDeferred(); }); box.VisibilityChanged += () => button.Text = label + (box.Visible ? "  ▾" : "  ›"); button.Alignment = HorizontalAlignment.Left; button.CustomMinimumSize = new Vector2(0, 42); _detailBody.AddChild(button); _detailBody.AddChild(box);
    }
    private void UpdateFace() { if (_face is null || _selected is null) return; _face.Texture = Scans.Get(_printing); if (_scanState is not null) { _scanState.Visible = _face.Texture is null; _scanState.Text = Scans.State(_printing); } }
    public override void _UnhandledInput(InputEvent e)
    {
        if (GetChildren().OfType<MyCollectionEditor>().Any()) return;
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.F11) return;
            if (key.Keycode == Key.Escape) { if (_detail.Visible) CloseInspector(); else Closed?.Invoke(); }
            else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode == Key.F) Search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton) GetViewport().SetInputAsHandled();
    }
}
