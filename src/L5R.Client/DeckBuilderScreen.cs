using Godot;
using L5R.Core;
namespace L5R.Client;

public static class DeckBuilderDialog
{
    public static void Show(Node parent)
    {
        var root = parent.GetTree().Root; if (root.GetNodeOrNull("DeckBuilderLayer") is not null) return;
        var layer = new CanvasLayer { Name = "DeckBuilderLayer", Layer = 85 }; root.AddChild(layer);
        try { var screen = new DeckBuilderScreen(); layer.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); screen.Closed += layer.QueueFree; }
        catch (Exception e) { layer.QueueFree(); GD.PushError(e.Message); }
    }
}

public partial class DeckBuilderScreen : Control
{
    public event Action? Closed;
    public CatalogueData Data { get; } = new();
    public CatalogueScans Scans { get; } = new();
    public DeckWorkshop Draft { get; private set; } = null!;
    public DeckFileStore Store { get; private set; } = null!;
    public string? StorageDirectory;
    public DeckSurface Mat { get; private set; } = null!;
    public DeckSurface Collection { get; private set; } = null!;
    public LineEdit Search { get; private set; } = null!;
    public bool Dirty { get; private set; }
    public string Page { get; private set; } = "decks";
    public string Family { get; private set; } = "Dynasty";
    private readonly Control _canvas = new(), _header = new(), _setupStrip = new(), _left = new(), _toolbar = new(), _setupTools = new(), _library = new(), _overlay = new(), _hover = new();
    private readonly HashSet<string> _compare = [];
    private Dictionary<string, CatalogueData.Card> _byId = [];
    private Button _dynasty = null!, _fate = null!, _compareButton = null!, _collectionTab = null!, _setupTab = null!, _save = null!;
    private Label _status = null!, _dirtyLabel = null!, _count = null!, _empty = null!;
    private LineEdit _name = null!;
    private OptionButton _clan = null!, _type = null!, _keyword = null!, _gold = null!, _support = null!, _sort = null!, _group = null!;
    private string _setupSlot = "", _setupClan = "Crane", _savedFingerprint = "", _savedName = "";
    private string? _currentPath;
    private float _w = 1672, _h = 941;
    private const float SidebarWidth = 620, MatLeft = 656;
    private bool _building, _list;
    private int _hoverTicket;
    private bool _previousEmbed;
    private Control _lines = new() { MouseFilter = MouseFilterEnum.Ignore };
    private CatalogueData.Card[] _inspectionCards = [];
    public override void _Ready()
    {
        _previousEmbed = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        Theme = DeckSkin.Theme();
        MouseFilter = MouseFilterEnum.Stop;
        _byId = Data.Cards.ToDictionary(c => c.Id); Draft = NewDraft();
        Store = new DeckFileStore(StorageDirectory ?? ProjectSettings.GlobalizePath("user://decks"));
        var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://Art/DeckBuilder/work-mat.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }; AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_lines); _lines.Draw += DrawFrames;
        AddChild(_canvas); foreach (var c in new[] { _header, _setupStrip, _left, _toolbar, _setupTools, _library }) _canvas.AddChild(c);
        Collection = new DeckSurface { Rows = true, RowHeight = 110, MakeCard = c => MakeRow(c, true) }; _canvas.AddChild(Collection);
        Mat = new DeckSurface { MakeCard = MakeTile }; _canvas.AddChild(Mat);
        _status = CatalogueArt.Label("", 17); _canvas.AddChild(_status); _count = CatalogueArt.Label("", 17); _canvas.AddChild(_count);
        _empty = CatalogueArt.Label("", 24); _empty.HorizontalAlignment = HorizontalAlignment.Center; _canvas.AddChild(_empty);
        BuildHeader(); BuildCollectionControls(); BuildToolbar();
        _canvas.AddChild(_overlay); _overlay.Visible = false; _canvas.AddChild(_hover); _hover.Visible = false; _hover.MouseFilter = MouseFilterEnum.Ignore;
        Resized += Fit; TreeExiting += Scans.Close; TreeExiting += () => GetViewport().GuiEmbedSubwindows = _previousEmbed; Fit(); ShowDecks();
    }
    private DeckWorkshop NewDraft() => new(Data.Cards.Select(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Definition?.Keywords?.ToArray() ?? c.Keywords, c.Playable, c.Definition?.CreatedOnly ?? false, c.Definition?.DeckCopyLimit, c.Definition is null ? 0 : DeckWorkshop.FateMinimumReductionOf(c.Definition), c.Definition is not null && DeckWorkshop.StrategyCopyRuleOf(c.Definition), c.Definition is not null && DeckWorkshop.BattleStrategyOf(c.Definition))));
    private static void Put(Control parent, Control child, float x, float y, float w, float h)
    { parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private static void Place(Control child, float x, float y, float w, float h) { child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private static OptionButton Option(string[] values, Action changed)
    { var o = new OptionButton { FitToLongestItem = false }; foreach (var v in values) o.AddItem(v); o.ItemSelected += _ => changed(); return o; }
    private void BuildHeader()
    {
        Put(_header, CatalogueArt.Button("‹ My decks", () => Guard(ShowDecks)), 24, 14, 140, 42);
        Put(_header, CatalogueArt.Label("Deck builder", 43, true), 198, 12, 355, 48);
        _name = new LineEdit { PlaceholderText = "Name your deck", MaxLength = 80 }; Put(_header, _name, 575, 16, 390, 42);
        _name.TextChanged += _ => { if (!_building) { Dirty = true; UpdateStatus(); } };
        _dirtyLabel = CatalogueArt.Label("", 16); _header.AddChild(_dirtyLabel);
        _save = CatalogueArt.Button("Save draft", () => SaveDraft()); _header.AddChild(_save);
        DeckSkin.Primary(_save);
        var copy = new MenuButton { Name = "Copy", Text = "", Icon = DeckSkin.Icon("more"), IconAlignment = HorizontalAlignment.Center, Flat = false, TooltipText = "More deck actions", AccessibilityName = "More deck actions" }; copy.AddThemeFontSizeOverride("font_size", 25); _header.AddChild(copy);
        var menu = copy.GetPopup(); menu.AddItem("Save a copy…", 0); menu.AddItem("Export draft…", 1); menu.IdPressed += id => { if (id == 0) SaveCopy(); else ExportFile(Draft.ToDeck(), _name.Text); };
        var brand = CatalogueArt.Label("LEGEND OF THE FIVE RINGS\nEMPEROR EDITION", 14); brand.Name = "Brand"; brand.HorizontalAlignment = HorizontalAlignment.Right; brand.AddThemeColorOverride("font_color", DeckSkin.Gold); _header.AddChild(brand);
    }
    private void BuildCollectionControls()
    {
        _collectionTab = CatalogueArt.Button("Collection", () => SetSetup("")); Put(_left, _collectionTab, 12, 6, 290, 40);
        _setupTab = CatalogueArt.Button("Setup", () => SetSetup("Stronghold")); Put(_left, _setupTab, 310, 6, 290, 40);
        Search = new LineEdit { PlaceholderText = "Search names or rules…", ClearButtonEnabled = true }; Put(_left, Search, 12, 58, 588, 42); Search.TextChanged += _ => ApplyFilters();
        _clan = Option(new[] { "All clans", "Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned" }, ApplyFilters);
        _type = Option(new[] { "All types", "Personality", "Holding", "Region", "Event", "Celestial", "Follower", "Item", "Spell", "Strategy", "Ring" }, ApplyFilters);
        _keyword = Option(new[] { "Any keyword" }.Concat(Data.Cards.SelectMany(c => c.Keywords).Distinct().Order()).ToArray(), ApplyFilters);
        _gold = Option(new[] { "Any Gold", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10+" }, ApplyFilters);
        _support = Option(new[] { "All cards", "Implemented" }, ApplyFilters);
        Put(_left, _clan, 12, 110, 188, 36); Put(_left, _type, 210, 110, 188, 36); Put(_left, _keyword, 408, 110, 192, 36); Put(_left, _gold, 12, 154, 188, 36); Put(_left, _support, 210, 154, 188, 36);
        Put(_left, CatalogueArt.Label("In deck", 15), 508, 163, 80, 22);
    }
    private void BuildToolbar()
    {
        _group = Option(new[] { "Group: Type", "Group: Gold", "Group: None" }, () => RefreshMat()); _sort = Option(new[] { "Name A–Z", "Gold cost", "Force", "Chi", "Set" }, () => { ApplyFilters(); RefreshMat(); });
        Put(_toolbar, _group, 8, 8, 162, 38); Put(_toolbar, _sort, 180, 8, 154, 38);
        var mat = CatalogueArt.Button("Mat", () => { _list = false; RefreshMat(); }); mat.Name = "MatTab"; Put(_toolbar, mat, 356, 8, 80, 38);
        var list = CatalogueArt.Button("List", () => { _list = true; RefreshMat(); }); list.Name = "ListTab"; Put(_toolbar, list, 440, 8, 80, 38);
        _compareButton = CatalogueArt.Button("Compare (0)", () => ShowComparison(_compare.Select(id => _byId[id]).ToArray())); Put(_toolbar, _compareButton, 540, 8, 156, 38);
        var label = CatalogueArt.Label("Card size", 16); label.Name = "SizeLabel"; _toolbar.AddChild(label);
        var slider = new HSlider { Name = "CardSize", MinValue = 110, MaxValue = 270, Step = 2, Value = 196, TooltipText = "Resize card faces; controls remain readable." }; slider.ValueChanged += v => { Mat.CardWidth = (float)v; HideHover(); RefreshMat(); }; _toolbar.AddChild(slider);
    }
    private void Fit()
    {
        var scale = Math.Min(Size.X / 1672f, Size.Y / 941f); if (scale <= 0) return;
        _canvas.Scale = Vector2.One * scale; _w = Size.X / scale; _h = Size.Y / scale; _canvas.Size = new Vector2(_w, _h);
        Place(_header, 0, 0, _w, 70); Place(_setupStrip, 14, 74, _w - 28, 76); Place(_left, 16, 158, SidebarWidth, 198);
        Place(Collection, 28, 362, SidebarWidth - 20, _h - 414); Collection.RowHeight = Math.Clamp((Collection.Size.Y - 4) / 5, 104, 150); Place(_toolbar, MatLeft, 158, _w - MatLeft - 18, 58); Place(Mat, MatLeft, 216, _w - MatLeft - 18, _h - 269);
        Place(_empty, MatLeft + 30, 360, _w - MatLeft - 64, 100); Place(_status, MatLeft + 16, _h - 43, _w - MatLeft - 40, 28); Place(_count, 24, _h - 40, SidebarWidth, 25);
        Place(_name, 560, 16, Math.Clamp(_w - 1280, 240, 390), 42);
        Place(_dirtyLabel, _w - 700, 22, 160, 30); Place(_save, _w - 530, 15, 180, 42); Place(_header.GetNode<Control>("Copy"), _w - 342, 15, 38, 42); Place(_header.GetNode<Control>("Brand"), _w - 290, 16, 264, 40);
        Place(_toolbar.GetNode<Control>("SizeLabel"), _toolbar.Size.X - 280, 16, 85, 25); Place(_toolbar.GetNode<Control>("CardSize"), _toolbar.Size.X - 187, 14, 172, 32);
        Place(_setupTools, MatLeft, 158, _w - MatLeft - 18, 58); Place(_library, 0, 0, _w, _h); _overlay.Size = _canvas.Size;
        if (IsInsideTree()) { BuildSetupStrip(); if (Page == "decks") BuildDeckList(); else if (_setupSlot.Length > 0) BuildSetupChoices(); }
        Collection.Relayout(); Mat.Relayout(); HideHover(); if (_overlay.Visible && _inspectionCards.Length > 0) ShowComparison(_inspectionCards); _lines.QueueRedraw();
    }
    private void DrawFrames()
    {
        if (_canvas.Scale.X <= 0 || Page == "decks") return; var s = _canvas.Scale.X;
        foreach (var y in new[] { 70f, 154f, _h - 50 }) _lines.DrawLine(new Vector2(18, y) * s, new Vector2(_w - 18, y) * s, new Color(.64f, .49f, .26f, .65f), 1);
        if (Page != "decks")
        {
            DeckSkin.Frame().Draw(_lines.GetCanvasItem(), new Rect2(13 * s, 155 * s, (SidebarWidth + 12) * s, (_h - 205) * s));
            DeckSkin.Frame().Draw(_lines.GetCanvasItem(), new Rect2((MatLeft - 10) * s, 155 * s, (_w - MatLeft - 4) * s, (_h - 205) * s));
        }
    }
    private void BuildSetupStrip()
    {
        Palette.Clear(_setupStrip); if (Draft is null) return;
        var stronghold = Draft.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold"); var clan = stronghold is null ? _setupClan : _byId[stronghold.Id].Clans.FirstOrDefault() ?? "Unaligned";
        Put(_setupStrip, CatalogueArt.Clan(clan), 9, 8, 58, 58);
        var x = 78f; foreach (var slot in DeckWorkshop.SetupSlots)
        {
            var info = Draft.Cards.FirstOrDefault(c => c.SetupSlot == slot); var card = info is null ? null : _byId[info.Id];
            if (card is not null) Put(_setupStrip, new DeckThumbnail(card, Scans), x, 9, 45, 55);
            var b = CatalogueArt.Button(slot + "\n" + (card?.Name ?? "Select…"), () => SetSetup(slot)); b.Alignment = HorizontalAlignment.Left; b.AddThemeFontSizeOverride("font_size", 16); b.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            b.ClipText = true; Put(_setupStrip, b, x + 53, 6, 163, 61); x += 221;
        }
        Put(_setupStrip, CatalogueArt.Button("Change setup", () => SetSetup("Stronghold")), x, 15, 145, 44);
        _dynasty = CatalogueArt.Button("", () => SetFamily("Dynasty")); _fate = CatalogueArt.Button("", () => SetFamily("Fate"));
        var tabW = Math.Max(185, (_w - 985) / 2); Put(_setupStrip, _dynasty, _w - 2 * tabW - 42, 7, tabW - 8, 62); Put(_setupStrip, _fate, _w - tabW - 34, 7, tabW - 8, 62);
        foreach (var tab in new[] { _dynasty, _fate }) { var icon = CatalogueArt.Clan(clan); Put(tab, icon, 12, 11, 40, 40); tab.AddThemeFontSizeOverride("font_size", 20); }
        UpdateStatus();
    }
    public void SetFamily(string family) { Family = family; SetSetup(""); ApplyFilters(); RefreshMat(true); }
    public void SetSetup(string slot)
    {
        _setupSlot = slot; HideHover(); Palette.Clear(_left); BuildCollectionControls();
        _toolbar.Visible = slot.Length == 0; _setupTools.Visible = slot.Length > 0; Collection.Visible = slot.Length == 0;
        if (slot.Length > 0) BuildSetupChoices(); else { ApplyFilters(); RefreshMat(true); }
        if (slot.Length == 0) { DeckSkin.Selected(_collectionTab, true); DeckSkin.Selected(_setupTab, false); }
        UpdateStatus();
    }
    private void BuildSetupChoices()
    {
        Palette.Clear(_left);
        Put(_left, CatalogueArt.Button("Collection", () => SetSetup("")), 12, 6, 290, 40); var setupTab = CatalogueArt.Button("Setup", () => { }); DeckSkin.Selected(setupTab, true); Put(_left, setupTab, 310, 6, 290, 40);
        var clans = Option(new[] { "Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned" }, () => { });
        clans.Select(Array.IndexOf(new[] { "Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned" }, _setupClan));
        clans.ItemSelected += i => { _setupClan = clans.GetItemText((int)i); BuildSetupChoices(); }; Put(_left, clans, 12, 62, 588, 42);
        var y = 124f; foreach (var slot in DeckWorkshop.SetupSlots)
        {
            var info = Draft.Cards.FirstOrDefault(c => c.SetupSlot == slot); var b = CatalogueArt.Button(slot + "\n" + (info?.Name ?? "Select a card"), () => SetSetup(slot)); b.Alignment = HorizontalAlignment.Left;
            var style = DeckSkin.Plate(slot == _setupSlot ? 1 : 0); style.ContentMarginLeft = 88; style.ContentMarginRight = 26; b.AddThemeStyleboxOverride("normal", style);
            foreach (var state in new[] { "hover", "pressed" }) { var h = DeckSkin.Plate(1); h.ContentMarginLeft = 88; h.ContentMarginRight = 26; b.AddThemeStyleboxOverride(state, h); }
            b.AccessibilityName = "Choose " + slot; b.AddThemeFontSizeOverride("font_size", 18); b.ClipText = true; Put(_left, b, 12, y, 588, 84);
            if (info is not null) Put(b, new DeckThumbnail(_byId[info.Id], Scans), 8, 8, 68, 68);
            var mark = new TextureRect { Texture = DeckSkin.Icon(info is null ? "radio" : "selected"), MouseFilter = MouseFilterEnum.Ignore }; Put(b, mark, 555, 32, 20, 20); y += 96;
        }
        Put(_left, Palette.Wrapped("Choose one of each.\nKept outside both decks.\n\nBorder Keep starts in play.\nBamboo Harvesters starts bowed for the second player only.", 18, CatalogueArt.Ivory), 16, y + 20, 555, 225);
        Palette.Clear(_setupTools); _setupTools.Visible = true; _toolbar.Visible = false; Put(_setupTools, CatalogueArt.Label("Choose your " + _setupSlot, 32, true), 14, 6, _toolbar.Size.X - 190, 48);
        Put(_setupTools, CatalogueArt.Button("Back to deck", () => SetSetup("")), _toolbar.Size.X - 170, 8, 150, 38);
        var infos = Data.Cards.Where(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot == _setupSlot);
        if (_setupSlot == "Stronghold") infos = infos.Where(c => c.Clans.Contains(_setupClan) || (_setupClan == "Unaligned" && c.Clans.Length == 0));
        var cards = infos.OrderByDescending(c => c.Playable).ThenBy(c => c.Name).ToArray(); _count.Text = "Setup · kept outside deck totals"; Mat.Setup = true; Mat.Rows = false; Mat.Reset(cards, true); _empty.Visible = cards.Length == 0; _empty.Text = "No setup choices for this Clan.";
    }
    public void ApplyFilters()
    {
        if (_building || Page == "decks" || _setupSlot.Length > 0) return;
        var cards = Data.Filter(Search.Text, _clan.Selected == 0 ? null : [_clan.Text], Family, _type.Selected == 0 ? null : [_type.Text], _keyword.Selected == 0 ? null : [_keyword.Text], _support.Selected == 1, _sort.Text)
            .Where(c => new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot.Length == 0);
        if (_gold.Selected > 0) cards = cards.Where(c => _gold.Text == "10+" ? c.Numeric("cost") >= 10 && c.Numeric("cost") != double.MaxValue : c.Numeric("cost") == double.Parse(_gold.Text));
        Collection.Reset(cards.ToArray(), true); _count.Text = $"{Collection.Cards.Length:N0} collection results";
    }
    public void RefreshMat(bool reset = false)
    {
        if (Page == "decks" || _setupSlot.Length > 0) return;
        var ids = Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains(Family)).Select(c => c.Id).ToHashSet();
        var cards = Data.Filter(sort: _sort.Text).Where(c => ids.Contains(c.Id)).ToArray();
        DeckSkin.Selected(_toolbar.GetNode<Button>("MatTab"), !_list); DeckSkin.Selected(_toolbar.GetNode<Button>("ListTab"), _list);
        Mat.Setup = false; Mat.Rows = _list; Mat.Group = _group.Selected switch { 1 => "Gold", 2 => "None", _ => "Type" }; Mat.Reset(cards, reset);
        _empty.Visible = cards.Length == 0; _empty.Text = "Your " + Family + " mat is empty.\nAdd cards from the collection to begin.";
    }
    public void Adjust(CatalogueData.Card card, int delta)
    { Draft.Adjust(card.Id, delta); Dirty = true; HideHover(); UpdateStatus(); Collection.Relayout(); RefreshMat(); }
    private void UpdateStatus()
    {
        if (_status is null || Draft is null) return; var r = Draft.Review();
        if (_dynasty is not null)
        {
            _dynasty.Text = $"DYNASTY  {r.Dynasty}\n40 minimum"; _fate.Text = $"FATE  {r.Fate}\n40 minimum";
            DeckSkin.Selected(_dynasty, Family == "Dynasty"); if (Family == "Dynasty") { var brass = DeckSkin.Plate(2); brass.ModulateColor = new Color(.58f, .5f, .38f, 1); _dynasty.AddThemeStyleboxOverride("normal", brass); }
            _fate.AddThemeStyleboxOverride("normal", DeckSkin.Plate(3));
            _fate.AddThemeColorOverride("font_color", Family == "Fate" ? new Color("ffebae") : DeckSkin.Text);
        }
        _status.Text = r.Warnings.Length > 0 ? $"{r.Warnings.Length} deck checks to review  ·  Draft can still be saved" : "Basic checks passed  ·  Draft can be saved";
        _status.TooltipText = string.Join("\n", r.Warnings); _status.MouseFilter = MouseFilterEnum.Stop;
        _dirtyLabel.Text = Dirty ? "Unsaved changes" : "Saved locally"; _compareButton.Text = $"Compare ({_compare.Count})"; _compareButton.Disabled = _compare.Count == 0;
    }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _overlay.Visible)
        { CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.F11) return;
            if (key.Keycode == Key.Escape) { if (_overlay.Visible) CloseOverlay(); else if (Page == "decks") Closed?.Invoke(); else Guard(ShowDecks); }
            else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode == Key.S && Page != "decks") SaveDraft();
            else if ((key.CtrlPressed || key.MetaPressed) && key.Keycode == Key.F && Page != "decks" && _setupSlot.Length == 0) Search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton) GetViewport().SetInputAsHandled();
    }
}
