using Godot;
namespace L5R.Client;

/// <summary>An anchored, immediately applied checklist shared by both catalogue workspaces.</summary>
public sealed class FilterChecklist
{
    public Button Trigger { get; }
    public HashSet<string> Selected { get; } = [];
    public event Action? Changed;
    public event Action? Opening;
    public LineEdit Search { get; }
    public Rect2 PopupRect => new(_popup.Position, _popup.Size);
    public bool IsOpen => _popup.Visible;
    public Func<Rect2>? PopupBounds { get; set; }
    private readonly string _label;
    private readonly PopupPanel _popup;
    private readonly VBoxContainer _rows;
    private readonly ScrollContainer _scroll;
    private readonly Label _empty;
    private readonly Vector2I _popupSize;
    private readonly Func<string, Control>? _icon;
    private readonly Dictionary<string, CheckBox> _checks = [];
    private IReadOnlyDictionary<string, int>? _counts;
    private string[] _options = [];
    public FilterChecklist(Node popupParent, Theme theme, string label, Vector2I? popupSize = null, Func<string, Control>? icon = null, Control? extraHeader = null)
    {
        _label = label; _icon = icon; _popupSize = popupSize ?? new Vector2I(350, 440);
        Trigger = CatalogueArt.Button(label + " · Any  ▾", TogglePopup);
        Trigger.AccessibilityName = "Filter " + label.ToLowerInvariant();
        _popup = new PopupPanel { Theme = theme, Name = label + "Checklist" }; popupParent.AddChild(_popup);
        _popup.AddThemeStyleboxOverride("panel", Palette.Box(new Color("0e171d"), CatalogueArt.Gold, 18, 1));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); _popup.AddChild(box);
        var title = new HBoxContainer(); var heading = CatalogueArt.Label(label, 23, true); heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; title.AddChild(heading);
        var close = CatalogueArt.Button("×", Close); close.AccessibilityName = "Close " + label.ToLowerInvariant() + " filter"; title.AddChild(close); box.AddChild(title);
        if (label == "Keywords") box.AddChild(CatalogueArt.Label("Within current results", 15));
        Search = new LineEdit { PlaceholderText = "Find " + label.ToLowerInvariant() + "…", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(0, 38) };
        Search.TextChanged += _ => Rebuild(); box.AddChild(Search);
        if (extraHeader is not null) box.AddChild(extraHeader);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 16);
        actions.AddChild(CatalogueArt.Button("Select all", () => SetAll(true)));
        actions.AddChild(CatalogueArt.Button("Deselect all", () => SetAll(false))); box.AddChild(actions);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(_popupSize.X - 40, 180) };
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _rows.AddThemeConstantOverride("separation", 8); _scroll.AddChild(_rows); box.AddChild(_scroll);
        _empty = CatalogueArt.Label("No matching options", 17); box.AddChild(_empty); _empty.Hide();
        UpdateTrigger();
    }
    public void Open() { if (!_popup.Visible) TogglePopup(); }
    public void Close() { _popup.Hide(); Trigger.GrabFocus(); }
    private void TogglePopup()
    {
        if (_popup.Visible) { Close(); return; }
        Opening?.Invoke();
        var viewport = Trigger.GetViewport().GetVisibleRect();
        var bounds = (PopupBounds?.Invoke() ?? viewport).Intersection(viewport);
        var trigger = Trigger.GetGlobalRect();
        var size = new Vector2I(Math.Min(_popupSize.X, (int)bounds.Size.X - 8), Math.Min(_popupSize.Y, (int)bounds.Size.Y - 8));
        if (size.X < 100 || size.Y < 100) return;
        var x = Math.Clamp((int)trigger.Position.X, (int)bounds.Position.X + 4, (int)bounds.End.X - size.X - 4);
        var y = Math.Clamp((int)trigger.End.Y + 6, (int)bounds.Position.Y + 4, (int)bounds.End.Y - size.Y - 4);
        _scroll.CustomMinimumSize = new Vector2(size.X - 40, Math.Max(80, size.Y - (_label == "Keywords" ? 240 : 182)));
        _popup.Popup(new Rect2I(x, y, size.X, size.Y)); Search.GrabFocus();
    }
    private void SetAll(bool selected)
    {
        Selected.Clear(); if (selected) Selected.UnionWith(_options);
        foreach (var (option, check) in _checks) check.SetPressedNoSignal(Selected.Contains(option));
        UpdateTrigger(); Changed?.Invoke();
    }
    /// <summary>Returns cleared selections without reentering the caller's filtering pass.</summary>
    public string[] SetOptions(string[] options, IReadOnlyDictionary<string, int>? counts = null)
    {
        var removed = Selected.Except(options).ToArray();
        var different = !_options.SequenceEqual(options) || !SameCounts(_counts, counts);
        _options = options; _counts = counts; Selected.IntersectWith(options);
        if (different || removed.Length > 0) Rebuild();
        UpdateTrigger(); return removed;
    }
    private static bool SameCounts(IReadOnlyDictionary<string, int>? a, IReadOnlyDictionary<string, int>? b) =>
        a is null ? b is null : b is not null && a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var n) && n == kv.Value);
    public void Clear() { Selected.Clear(); Search.Text = ""; Rebuild(); }
    public void Remove(string option) { if (Selected.Remove(option)) { Rebuild(); Changed?.Invoke(); } }
    public void Free() => _popup.QueueFree();
    private void Rebuild()
    {
        var focused = _checks.FirstOrDefault(kv => kv.Value.HasFocus()).Key;
        var before = _scroll.ScrollVertical;
        Palette.Clear(_rows); _checks.Clear();
        foreach (var option in _options.Where(o => o.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            if (_icon is not null) { var icon = _icon(option); icon.CustomMinimumSize = new Vector2(28, 28); row.AddChild(icon); }
            var check = new CheckBox { Text = option, ButtonPressed = Selected.Contains(option), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 32) };
            check.AddThemeColorOverride("font_color", CatalogueArt.Ivory);
            check.AddThemeFontSizeOverride("font_size", 18);
            check.AddThemeIconOverride("checked", DeckSkin.Icon("checked"));
            check.AddThemeIconOverride("unchecked", DeckSkin.Icon("unchecked"));
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                check.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            check.Toggled += pressed => { if (pressed) Selected.Add(option); else Selected.Remove(option); UpdateTrigger(); Changed?.Invoke(); };
            row.AddChild(check); _checks[option] = check;
            if (_counts?.TryGetValue(option, out var count) == true) { var label = CatalogueArt.Label(count.ToString("N0"), 16); label.CustomMinimumSize = new Vector2(48, 32); label.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(label); }
            _rows.AddChild(row);
        }
        _empty.Visible = _checks.Count == 0;
        if (_popup.IsInsideTree()) Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(_popup) || !_popup.IsInsideTree()) return;
            _scroll.ScrollVertical = before;
            if (focused is not null && _popup.Visible && _checks.TryGetValue(focused, out var check)) check.GrabFocus();
        }).CallDeferred();
        UpdateTrigger();
    }
    private void UpdateTrigger() => Trigger.Text = (Selected.Count == 0 ? _label + " · Any" : $"{_label} ({Selected.Count})") + "  ▾";
}
