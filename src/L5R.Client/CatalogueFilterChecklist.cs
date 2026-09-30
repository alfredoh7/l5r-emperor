using Godot;
namespace L5R.Client;

/// <summary>A "Label (n) ▾" button opening a select-all/deselect-all checkbox popover, shared by the
/// Card Library and Deck Builder so both screens filter the same additive way.</summary>
public sealed class FilterChecklist
{
    public Button Trigger { get; }
    public HashSet<string> Selected { get; } = [];
    public event Action? Changed;
    private readonly string _label;
    private readonly Func<string, Control>? _icon;
    private readonly Vector2I _popupSize;
    private readonly PopupPanel _popup;
    private readonly VBoxContainer _rows;
    private string[] _options = [];
    public FilterChecklist(Node popupParent, Theme theme, string label, Vector2I? popupSize = null, Func<string, Control>? icon = null, Control? extraHeader = null)
    {
        _label = label; _icon = icon; _popupSize = popupSize ?? new Vector2I(300, 420);
        Trigger = CatalogueArt.Button(label + "  ▾", TogglePopup);
        _popup = new PopupPanel { Theme = theme }; popupParent.AddChild(_popup);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); _popup.AddChild(box);
        box.AddChild(CatalogueArt.Label(label, 20, true));
        if (extraHeader is not null) box.AddChild(extraHeader);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 16);
        header.AddChild(CatalogueArt.Button("Select all", () => SetAll(true)));
        header.AddChild(CatalogueArt.Button("Deselect all", () => SetAll(false)));
        box.AddChild(header);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(_popupSize.X - 40, _popupSize.Y - 150) };
        _rows = new VBoxContainer(); _rows.AddThemeConstantOverride("separation", 4); scroll.AddChild(_rows); box.AddChild(scroll);
        box.AddChild(CatalogueArt.Button("Done", () => _popup.Hide()));
    }
    private void TogglePopup() { if (_popup.Visible) _popup.Hide(); else _popup.PopupCentered(_popupSize); }
    private void SetAll(bool selected)
    {
        Selected.Clear(); if (selected) foreach (var option in _options) Selected.Add(option);
        foreach (var check in _rows.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<CheckBox>())) check.SetPressedNoSignal(selected);
        UpdateTrigger(); Changed?.Invoke();
    }
    /// <summary>Replaces the offered options, in the order given (callers control A–Z vs most-common
    /// ordering). Stale selections outside the new options are dropped.</summary>
    public void SetOptions(string[] options)
    {
        if (_options.SequenceEqual(options)) return;
        _options = options;
        var before = Selected.Count;
        Selected.IntersectWith(options);
        Rebuild();
        if (Selected.Count != before) Changed?.Invoke();
    }
    public void Clear() { Selected.Clear(); Rebuild(); }
    private void Rebuild()
    {
        Palette.Clear(_rows);
        foreach (var option in _options)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            if (_icon is not null) { var icon = _icon(option); icon.CustomMinimumSize = new Vector2(28, 28); row.AddChild(icon); }
            var check = new CheckBox { Text = option, ButtonPressed = Selected.Contains(option) };
            check.AddThemeColorOverride("font_color", CatalogueArt.Ivory);
            check.Toggled += pressed => { if (pressed) Selected.Add(option); else Selected.Remove(option); UpdateTrigger(); Changed?.Invoke(); };
            row.AddChild(check);
            _rows.AddChild(row);
        }
        UpdateTrigger();
    }
    private void UpdateTrigger() => Trigger.Text = (Selected.Count == 0 ? _label : $"{_label} ({Selected.Count})") + "  ▾";
}
