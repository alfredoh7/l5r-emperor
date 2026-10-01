using Godot;
namespace L5R.Client;

/// <summary>One catalogue filter/interaction model for Library and Deck Builder; no game legality lives here.</summary>
public partial class CatalogueBrowserBar : Control
{
    public event Action? Changed;
    public event Action<float>? CardSizeChanged;
    public LineEdit Search { get; }
    public HSlider CardSize { get; }
    public FilterChecklist Types { get; }
    public FilterChecklist Keywords { get; }
    public HashSet<string> Clans { get; } = [];
    public string Family { get; private set; }
    public OptionButton SortControl => _sort;
    public string Sort => _sort.GetItemText(_sort.Selected);
    public bool PlayableOnly { get; private set; }
    public CatalogueData.Card[] Results { get; private set; } = [];
    public CatalogueData.Card[] CardsWithoutOwnership { get; private set; } = [];
    public OwnershipFilter Ownership { get; private set; }
    public Button EditCollectionButton { get; }
    public event Action? EditCollection;
    public OptionButton CollectionControl { get; }
    public string ScopeNotice => _notice.Text;
    public Func<Rect2>? PopupBounds { get => Types.PopupBounds; set { Types.PopupBounds = value; Keywords.PopupBounds = value; } }
    public const float BarHeight = 220;
    private static readonly string[] ClanNames = ["Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Unaligned"];
    private readonly CatalogueData _data;
    private readonly Func<CatalogueData.Card, bool> _eligible;
    private readonly string _initialFamily;
    private readonly Dictionary<string, Button> _clans = [], _families = [];
    private readonly Button _selectAll, _deselectAll, _allCards, _playable, _reset, _alphabetical, _common;
    private readonly OptionButton _set, _gold, _sort;
    private readonly Label _count, _sizeLabel, _notice;
    private readonly ScrollContainer _tagScroll;
    private readonly HBoxContainer _tags;
    private bool _refreshing, _mostCommon;
    private static float _preferredCardWidth = 196;
    public CatalogueBrowserBar(CatalogueData data, string[] families, string initialFamily = "All", Func<CatalogueData.Card, bool>? eligible = null)
    {
        Name = "CatalogueBrowserBar"; Theme = CatalogueArt.Theme(); _data = data; _eligible = eligible ?? (_ => true); Family = _initialFamily = initialFamily;
        foreach (var clan in ClanNames)
        {
            var button = CatalogueArt.Button("", () => { if (!Clans.Add(clan)) Clans.Remove(clan); Refresh(); });
            button.ToggleMode = true; button.AccessibilityName = clan + " cards"; button.TooltipText = clan + " cards";
            AddChild(button); _clans[clan] = button; button.AddChild(CatalogueArt.Clan(clan));
            var label = CatalogueArt.Label(clan, 17); label.HorizontalAlignment = HorizontalAlignment.Center; button.AddChild(label);
            var mark = new TextureRect { Name = "SelectedMark", Texture = DeckSkin.Icon("unchecked"), MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered }; button.AddChild(mark);
        }
        _selectAll = AddButton("Select all", () => { Clans.UnionWith(ClanNames); Refresh(); });
        _deselectAll = AddButton("Deselect all", () => { Clans.Clear(); Refresh(); });
        CollectionControl = new OptionButton { Name = "CollectionFilter", FitToLongestItem = false, AccessibilityName = "Filter collection ownership" };
        foreach (var item in new[] { "Collection: All cards", "Collection: Owned", "Collection: Unowned" }) CollectionControl.AddItem(item);
        AddChild(CollectionControl); CollectionControl.ItemSelected += i => { Ownership = (OwnershipFilter)i; Refresh(); };
        EditCollectionButton = AddButton("Edit my collection", () => EditCollection?.Invoke());
        MyCollection.Store.Changed += CollectionChanged; TreeExiting += () => MyCollection.Store.Changed -= CollectionChanged;
        Search = new LineEdit { PlaceholderText = "Search card names, keywords or rules…", ClearButtonEnabled = true }; AddChild(Search); Search.TextChanged += _ => Refresh();
        foreach (var family in families) _families[family] = AddButton(family, () => SetFamily(family));
        Types = new FilterChecklist(this, Theme, "Types"); AddChild(Types.Trigger); Types.Changed += () => Refresh();
        var order = new HBoxContainer(); order.AddThemeConstantOverride("separation", 10);
        _alphabetical = CatalogueArt.Button("A–Z", () => { _mostCommon = false; Refresh(); }); _common = CatalogueArt.Button("Most common", () => { _mostCommon = true; Refresh(); });
        order.AddChild(_alphabetical); order.AddChild(_common);
        Keywords = new FilterChecklist(this, Theme, "Keywords", extraHeader: order); AddChild(Keywords.Trigger); Keywords.Changed += () => Refresh();
        Types.Opening += () => { if (Keywords.IsOpen) Keywords.Close(); };
        Keywords.Opening += () => { if (Types.IsOpen) Types.Close(); };
        _set = AddOption("Printing set", ["Any set", .. CatalogueData.SetOrder, "Other"]);
        _set.TooltipText = "Filter by a printing set. Unknown source labels are grouped as Other.";
        _gold = AddOption("Gold cost", ["Any Gold", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10+"]);
        _allCards = AddButton("Entire catalogue", () => { PlayableOnly = false; Refresh(); });
        _playable = AddButton("Playable now", () => { PlayableOnly = true; Refresh(); });
        _sort = AddOption("Sort cards", ["Name A–Z", "Name Z–A", "Gold cost", "Force", "Chi", "Set"]);
        _sort.TooltipText = "Set uses Mendon's presentation order; reprints sort by their earliest recognized set.";
        _tagScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tags = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _tags.AddThemeConstantOverride("separation", 8); _tagScroll.AddChild(_tags); AddChild(_tagScroll);
        _reset = AddButton("Clear filters", Reset);
        _count = CatalogueArt.Label("", 17); AddChild(_count);
        _sizeLabel = CatalogueArt.Label("Card size", 16); AddChild(_sizeLabel);
        CardSize = new HSlider { Name = "CardSize", MinValue = 120, MaxValue = 300, Step = 2, Value = _preferredCardWidth, AccessibilityName = "Card size", TooltipText = "Drag to resize full card faces. Arrow keys also adjust size." };
        var track = Palette.Box(new Color("10171a"), new Color("665332"), 2, 1); track.ContentMarginTop = track.ContentMarginBottom = 2;
        CardSize.AddThemeStyleboxOverride("slider", track);
        var fill = Palette.Box(new Color("8d7040"), CatalogueArt.Gold, 2, 1); fill.ContentMarginTop = fill.ContentMarginBottom = 2;
        CardSize.AddThemeStyleboxOverride("grabber_area", fill); CardSize.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        CardSize.AddThemeIconOverride("grabber", DeckSkin.Icon("radio")); CardSize.AddThemeIconOverride("grabber_highlight", DeckSkin.Icon("selected"));
        AddChild(CardSize); CardSize.ValueChanged += value => { _preferredCardWidth = (float)value; _sizeLabel.TooltipText = $"{value:N0} logical pixels wide"; CardSizeChanged?.Invoke((float)value); };
        _notice = CatalogueArt.Label("", 15); _notice.AddThemeColorOverride("font_color", CatalogueArt.Gold); AddChild(_notice);
        Resized += Fit;
        Refresh(false);
    }
    private Button AddButton(string text, Action action) { var button = CatalogueArt.Button(text, action); AddChild(button); return button; }
    private OptionButton AddOption(string name, string[] options)
    {
        var option = new OptionButton { Name = name.Replace(" ", ""), AccessibilityName = name, FitToLongestItem = false, ClipText = true };
        foreach (var item in options) option.AddItem(item); AddChild(option); option.ItemSelected += _ => Refresh();
        option.GetPopup().AllowSearch = true; option.GetPopup().MaxSize = new Vector2I(520, 460); return option;
    }
    public void SetFamily(string family)
    {
        if (!_families.ContainsKey(family)) return;
        Family = family; Refresh();
    }
    public void Reset()
    {
        _refreshing = true; Clans.Clear(); Types.Clear(); Keywords.Clear(); Search.Text = ""; Family = _initialFamily; PlayableOnly = false; Ownership = OwnershipFilter.All; CollectionControl.Select(0); _set.Select(0); _gold.Select(0);
        _refreshing = false; _notice.Text = ""; Refresh();
    }
    public void Refresh(bool notify = true)
    {
        if (_refreshing) return; _refreshing = true;
        try
        {
            var familyCards = _data.Filter(family: Family).Where(_eligible);
            var removed = Types.SetOptions(familyCards.SelectMany(c => c.Types).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray()).ToList();
            CardsWithoutOwnership = FilterWithoutKeywords(false, true).Where(c => Keywords.Selected.Count == 0 || Keywords.Selected.Any(c.Keywords.Contains)).ToArray();
            var scoped = FilterWithoutKeywords().ToArray();
            var counts = scoped.SelectMany(c => c.Keywords).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            var ordered = _mostCommon ? counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key) : counts.Keys.Order(StringComparer.OrdinalIgnoreCase);
            removed.AddRange(Keywords.SetOptions(ordered.ToArray(), counts));
            var selected = Keywords.Selected;
            Results = scoped.Where(c => selected.Count == 0 || selected.Any(c.Keywords.Contains)).ToArray();
            if (removed.Count > 0) _notice.Text = string.Join(", ", removed.Distinct()) + " cleared · unavailable with these filters.";
            else if (MyCollection.Store.LoadError.Length > 0) _notice.Text = "Collection unavailable; open Edit my collection for details. Your original file is preserved.";
            else if (notify) _notice.Text = "";
            _count.Text = $"{Results.Length:N0} matching cards";
            foreach (var (clan, button) in _clans)
            {
                var active = Clans.Contains(clan); button.SetPressedNoSignal(active); foreach (var state in new[] { "normal", "hover", "pressed" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
                button.GetChild<TextureRect>(0).Modulate = active ? Colors.White : new Color(.75f, .75f, .75f);
                button.GetChild<Label>(1).AddThemeColorOverride("font_color", active ? new Color("efd48d") : CatalogueArt.Ivory);
                button.GetNode<TextureRect>("SelectedMark").Texture = DeckSkin.Icon(active ? "checked" : "unchecked");
            }
            foreach (var (family, button) in _families) SelectedStyle(button, Family == family);
            SelectedStyle(_allCards, !PlayableOnly); SelectedStyle(_playable, PlayableOnly); SelectedStyle(_alphabetical, !_mostCommon); SelectedStyle(_common, _mostCommon);
            BuildTags(); Fit();
        }
        finally { _refreshing = false; }
        if (notify) Changed?.Invoke();
    }
    private void CollectionChanged() => Refresh();
    public void SetOwnership(OwnershipFilter filter) { Ownership = filter; CollectionControl.Select((int)filter); Refresh(); }
    private IEnumerable<CatalogueData.Card> FilterWithoutKeywords(bool ownership = true, bool ignoreSearch = false)
    {
        var cards = _data.Filter(ignoreSearch ? "" : Search.Text, Clans, Family, Types.Selected, null, PlayableOnly, Sort).Where(_eligible);
        if (_gold.Selected > 0) cards = cards.Where(c => c.Numeric("cost") != double.MaxValue && (_gold.Text == "10+" ? c.Numeric("cost") >= 10 : c.Numeric("cost") == _gold.Selected - 1));
        if (_set.Selected > 0)
        {
            var set = _set.GetItemText(_set.Selected);
            cards = cards.Where(c => c.Printings.Any(p => p.Set != "Game printing" && (set == "Other" ? !CatalogueData.SetOrder.Contains(p.Set) : p.Set == set)));
        }
        return ownership ? cards.Where(c => MyCollection.Store.Includes(c.Id, Ownership)) : cards;
    }
    private void BuildTags()
    {
        Palette.Clear(_tags);
        void Tag(string text, Action remove)
        {
            var b = CatalogueArt.Button(text + "  ×", remove);
            foreach (var state in new[] { "normal", "hover", "pressed" }) b.AddThemeStyleboxOverride(state, Palette.Box(new Color("e1d6c2"), new Color("8c7447"), 8, 1));
            b.AddThemeColorOverride("font_color", new Color("161d24")); b.AddThemeColorOverride("font_hover_color", new Color("161d24")); b.AccessibilityName = "Remove filter " + text; b.AddThemeFontSizeOverride("font_size", 16); _tags.AddChild(b);
        }
        foreach (var clan in Clans.Order(StringComparer.OrdinalIgnoreCase)) Tag(clan, () => { Clans.Remove(clan); Refresh(); });
        foreach (var type in Types.Selected.Order(StringComparer.OrdinalIgnoreCase)) Tag(type, () => Types.Remove(type));
        foreach (var keyword in Keywords.Selected.Order(StringComparer.OrdinalIgnoreCase)) Tag(keyword, () => Keywords.Remove(keyword));
        if (_set.Selected > 0) Tag(_set.Text, () => { _set.Select(0); Refresh(); });
        if (_gold.Selected > 0) Tag("Gold " + _gold.Text, () => { _gold.Select(0); Refresh(); });
        if (_tags.GetChildCount() == 0) { var hint = CatalogueArt.Label("Any clan · Any type · Any keyword", 16); hint.CustomMinimumSize = new Vector2(360, 30); _tags.AddChild(hint); }
    }
    private static void SelectedStyle(Button button, bool selected) =>
        button.AddThemeStyleboxOverride("normal", Palette.Box(selected ? new Color("332b1f") : new Color("10171a"), selected ? CatalogueArt.Gold : new Color("51442d"), 8, selected ? 2 : 1));
    private static void Place(Control child, float x, float y, float w, float h) { child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private void Fit()
    {
        if (Size.X < 400 || _notice is null) return;
        var w = Size.X; var cell = (w - 224) / ClanNames.Length; var i = 0;
        foreach (var button in _clans.Values)
        {
            Place(button, i++ * cell, 0, cell - 6, 72); Place(button.GetChild<Control>(0), (cell - 58) / 2, 0, 52, 52);
            Place(button.GetChild<Control>(1), 0, 48, cell - 6, 24); Place(button.GetChild<Control>(2), cell - 29, 47, 21, 22);
        }
        Place(_selectAll, w - 215, 14, 100, 38); Place(_deselectAll, w - 109, 14, 109, 38);
        var familyWidth = _families.Count == 4 ? 400 : 230;
        Place(Search, 0, 84, w - familyWidth - 435, 40);
        Place(CollectionControl, w - familyWidth - 425, 84, 220, 40); Place(EditCollectionButton, w - familyWidth - 195, 84, 183, 40);
        var x = w - familyWidth;
        foreach (var (key, button) in _families) { var width = key == "Strongholds" ? 130 : (familyWidth - (_families.ContainsKey("Strongholds") ? 130 : 0)) / (_families.Count - (_families.ContainsKey("Strongholds") ? 1 : 0)); Place(button, x, 84, width, 40); x += width; }
        Place(Types.Trigger, 0, 134, 174, 38); Place(Keywords.Trigger, 184, 134, 192, 38); Place(_set, 386, 134, 245, 38); Place(_gold, 641, 134, 140, 38);
        Place(_allCards, 799, 134, 184, 38); Place(_playable, 993, 134, 166, 38); Place(_count, 1173, 137, Math.Max(130, w - 1173), 32);
        Place(_tagScroll, 0, 182, w - 655, 36); Place(_reset, w - 644, 182, 124, 34);
        Place(_sort, w - 506, 182, 170, 34); Place(_sizeLabel, w - 320, 187, 78, 25); Place(CardSize, w - 238, 188, 230, 24);
        Place(_notice, 0, 214, w, 20);
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawLine(new Vector2(0, 77), new Vector2(Size.X, 77), new Color(.56f, .43f, .22f, .55f), 1);
    }
    public void CloseMenus() { if (Types.IsOpen) Types.Close(); if (Keywords.IsOpen) Keywords.Close(); }
}
