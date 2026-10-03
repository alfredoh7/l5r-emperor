using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of the searcher's private, grouped view. Never reads a deck or derives legality.</summary>
public sealed partial class DeckSearchPanel : PanelContainer
{
    private readonly VBoxContainer _body = new(), _gallery = new(), _preview = new();
    private readonly List<(SearchEntryView Entry, Button Button)> _cards = [];
    private ScrollContainer _scroll = null!;
    private LineEdit _filter = null!;
    private Button _confirm = null!;
    private Label _selection = null!;
    private SearchView _search = null!;
    private ActionOption[] _actions = [];
    private ActionOption? _selected;
    private bool _canSubmit;
    private int _viewer;
    private string? _inspectedId;
    public Action<ActionOption>? Confirmed;
    public ActionOption? Selection => _selected;
    public LineEdit Filter => _filter;
    public ScrollContainer GalleryScroll => _scroll;
    public IReadOnlyList<(SearchEntryView Entry, Button Button)> Cards => _cards;
    public Control Preview => _preview;
    public Button ConfirmButton => _confirm;

    public DeckSearchPanel()
    {
        Theme = SidebarSkin.Theme(); AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        _body.AddThemeConstantOverride("separation", 8); AddChild(_body);
    }
    public void Populate(SearchView search, ActionOption[] actions, int viewer, bool canSubmit, CardView? source = null)
    {
        _search = search; _actions = actions; _viewer = viewer; _canSubmit = canSubmit; _selected = null; _cards.Clear();
        // These containers are reused after detaching so no previous private contents survive a new view.
        if (_gallery.GetParent() is { } gp) gp.RemoveChild(_gallery);
        if (_preview.GetParent() is { } pp) pp.RemoveChild(_preview);
        Palette.Clear(_gallery); Palette.Clear(_preview); Palette.Clear(_body);
        var title = Palette.Text("SEARCH YOUR DECK", 30, title: true); title.HorizontalAlignment = HorizontalAlignment.Center; _body.AddChild(title);
        var subtitle = Palette.Wrapped(search.SourceName + (search.Deck is null ? "" : " · " + search.Deck + " deck"), 21);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center; _body.AddChild(subtitle);
        var privateNote = Palette.Text("Only you can see this list.", 16, Palette.Gold); privateNote.HorizontalAlignment = HorizontalAlignment.Center; _body.AddChild(privateNote);
        _body.AddChild(new HSeparator());
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 20); _body.AddChild(row);
        var context = new VBoxContainer { CustomMinimumSize = new(204, 0) }; context.AddThemeConstantOverride("separation", 18); row.AddChild(context);
        context.AddChild(Palette.Wrapped(search.Deck is null ? "Choose a deck" : "Your " + search.Deck + " deck", 23, Palette.Gold));
        context.AddChild(Palette.Wrapped("Private search", 19, Palette.PaperDim)); context.AddChild(new HSeparator());
        context.AddChild(Palette.Wrapped(search.Criteria is null ? search.Prompt : "Choose: " + search.Criteria, 21));
        if (search.Deck is not null) context.AddChild(Palette.Wrapped(search.CardsRemaining + " cards remaining\nGrouped by type and name", 17, Palette.PaperDim));
        context.AddChild(Palette.Wrapped(search.Stage == SearchStage.ChooseDeck ? "Choose a deck to open its card list." : "Select one card, then confirm.", 17, Palette.PaperDim));
        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; center.AddThemeConstantOverride("separation", 12); row.AddChild(center);
        _filter = new LineEdit { PlaceholderText = "Find a card…", ClearButtonEnabled = true, CustomMinimumSize = new(0, 44), Visible = search.Stage == SearchStage.Select };
        _filter.AccessibilityName = "Filter the private card list by name"; center.AddChild(_filter);
        _filter.TextChanged += _ => PopulateGallery();
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; center.AddChild(_scroll);
        _gallery.SizeFlagsHorizontal = SizeFlags.ExpandFill; _gallery.AddThemeConstantOverride("separation", 14); _scroll.AddChild(_gallery);
        var previewFrame = new PanelContainer { CustomMinimumSize = new(336, 0) }; previewFrame.AddThemeStyleboxOverride("panel", SidebarSkin.Inset()); row.AddChild(previewFrame);
        _preview.AddThemeConstantOverride("separation", 8); previewFrame.AddChild(_preview);
        _body.AddChild(new HSeparator { Visible = search.Stage == SearchStage.Select });
        var footer = new HBoxContainer { Visible = search.Stage == SearchStage.Select }; footer.AddThemeConstantOverride("separation", 20); _body.AddChild(footer);
        _selection = Palette.Wrapped("Your selection will be shown to your opponent.", 18, Palette.PaperDim);
        _selection.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(_selection);
        _confirm = SidebarSkin.Button("Confirm selection", () =>
        { if (_canSubmit && _selected is { Enabled: true, Command: not null } chosen) Confirmed?.Invoke(chosen); }, true);
        _confirm.CustomMinimumSize = new(300, 56); _confirm.Disabled = true; footer.AddChild(_confirm);
        if (search.CanPass && actions.FirstOrDefault(a => a.Type == "passSearch" && a.Enabled && a.Command is not null) is { } pass)
        {
            _selection.Text = search.Optional ? "Taking a card is optional. Passing still shuffles the deck." : "No card qualifies. Pass to shuffle the deck and continue play.";
            var passButton = SidebarSkin.Button("Pass — take no card", () =>
            { _selected = pass; _confirm.Text = "Confirm pass"; _confirm.Disabled = !_canSubmit; _selection.Text = "The deck will still be shuffled and play will continue."; PaintSelection(); });
            passButton.CustomMinimumSize = new(236, 56); passButton.Disabled = !_canSubmit; footer.AddChild(passButton); footer.MoveChild(passButton, 1);
        }
        if (!_canSubmit) _body.AddChild(Palette.Wrapped("Your choice is paused until the connection is ready.", 17, Palette.Gold));
        if (search.Stage == SearchStage.ChooseDeck) PopulateDecks(); else PopulateGallery();
        ShowPreview(search.Entries?.FirstOrDefault(e => e.Eligible));
        if (search.Stage == SearchStage.ChooseDeck && source is { Definition: not null })
        {
            Palette.Clear(_preview); _preview.AddChild(CardPreview.Face(source, 390, 306));
            _preview.AddChild(Palette.Wrapped(source.Definition.Name, 21));
        }
    }
    private void PopulateDecks()
    {
        foreach (var deck in _search.DeckChoices ?? [])
        {
            var action = _actions.FirstOrDefault(a => a.Type == "chooseSearchDeck" && a.Command?.Choice == deck.Deck);
            var button = SidebarSkin.Button(deck.Label + "\n" + deck.Criteria + " · " + deck.CardsRemaining + " cards", () =>
            {
                if (action is not { Enabled: true, Command: not null } || !_canSubmit) return;
                _canSubmit = false;
                foreach (var b in _gallery.GetChildren().OfType<Button>()) b.Disabled = true;
                Confirmed?.Invoke(action);
            });
            button.SetMeta("deck", deck.Deck); button.CustomMinimumSize = new(0, 126); button.Disabled = !_canSubmit || action?.Enabled != true; _gallery.AddChild(button);
        }
    }
    private void PopulateGallery()
    {
        _cards.Clear(); Palette.Clear(_gallery);
        var entries = (_search.Entries ?? []).Where(e => e.Definition.Name.Contains(_filter.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var group in entries.GroupBy(e => e.Definition.Type).OrderByDescending(g => g.Any(e => e.Eligible)))
        {
            _gallery.AddChild(Palette.Text(group.Key.ToString().ToUpperInvariant(), 18, Palette.Gold));
            var flow = new HFlowContainer(); flow.AddThemeConstantOverride("h_separation", 12); flow.AddThemeConstantOverride("v_separation", 12); _gallery.AddChild(flow);
            foreach (var entry in group)
            {
                var action = _actions.FirstOrDefault(a => a.Type == "selectSearchCard" && a.Command?.Choice == entry.DefinitionId);
                var button = new Button { CustomMinimumSize = new(166, 300), ClipContents = true };
                button.AccessibilityName = entry.Definition.Name + ", " + entry.Count + " remaining. " + (entry.Eligible ? "Press to select." : "Unavailable: " + entry.Reason);
                button.TooltipText = entry.Eligible ? "Select " + entry.Definition.Name : entry.Reason ?? "Unavailable";
                var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; button.AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 8);
                var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 4); margin.AddChild(content);
                var face = CardPreview.Face(Printed(entry.Definition, _viewer), 210, 150); face.Modulate = entry.Eligible ? Colors.White : new Color(.48f, .48f, .48f); content.AddChild(face);
                var name = Palette.Wrapped(entry.Definition.Name, 16, entry.Eligible ? Palette.Paper : Palette.PaperDim); name.HorizontalAlignment = HorizontalAlignment.Center; name.MaxLinesVisible = 2; content.AddChild(name);
                var count = Palette.Text("×" + entry.Count + (entry.Eligible ? "" : " · Unavailable"), 15, entry.Eligible ? Palette.Gold : Palette.PaperDim); count.HorizontalAlignment = HorizontalAlignment.Center; content.AddChild(count);
                IgnoreInput(margin);
                var mark = Palette.Text("✓", 25, Palette.Gold); mark.Name = "SelectionMark"; mark.Position = new(132, 267); mark.ZIndex = 1; mark.MouseFilter = MouseFilterEnum.Ignore; button.AddChild(mark);
                flow.AddChild(button); _cards.Add((entry, button));
                button.MouseEntered += () => ShowPreview(entry); button.FocusEntered += () => ShowPreview(entry);
                // Ineligible cards remain inspectable by keyboard and mouse, but can never become the choice.
                button.Pressed += () =>
                {
                    ShowPreview(entry);
                    if (!entry.Eligible || !_canSubmit || action is not { Enabled: true, Command: not null }) return;
                    _selected = action; _confirm.Disabled = false; _selection.Text = "Selected: " + entry.Definition.Name + " · will be shown to your opponent."; PaintSelection();
                };
            }
        }
        if (entries.Length == 0) _gallery.AddChild(Palette.Wrapped((_search.Entries?.Length ?? 0) == 0 ? "This deck is empty." : "No cards match this name. Clear the filter to see the deck.", 21, Palette.PaperDim));
        if (!entries.Any(e => e.DefinitionId == _inspectedId)) ShowPreview(entries.FirstOrDefault(e => e.DefinitionId == _selected?.Command?.Choice) ?? entries.FirstOrDefault(e => e.Eligible));
        PaintSelection();
    }
    private void PaintSelection()
    {
        if (_selected?.Type != "passSearch") _confirm.Text = "Confirm selection";
        foreach (var (entry, button) in _cards)
        {
            var selected = _selected?.Command?.Choice == entry.DefinitionId;
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            {
                var style = selected ? SidebarSkin.Surface(true, tint: new Color(1.5f, 1.3f, .8f)) : SidebarSkin.Inset();
                if (!selected) style.ModulateColor = state is "hover" or "focus" ? new Color(1.2f, 1.15f, 1) : new Color(.8f, .8f, .8f);
                button.AddThemeStyleboxOverride(state, style);
            }
            button.GetNode<Label>("SelectionMark").Visible = selected;
        }
    }
    private void ShowPreview(SearchEntryView? entry)
    {
        _inspectedId = entry?.DefinitionId;
        Palette.Clear(_preview);
        if (entry is null)
        {
            _preview.AddChild(Palette.Wrapped("Card inspection", 22, Palette.Gold));
            _preview.AddChild(Palette.Wrapped("Hover or focus a card to read its full face.\n\nInspection does not change your selection.", 19, Palette.PaperDim)); return;
        }
        _preview.AddChild(CardPreview.Face(Printed(entry.Definition, _viewer), 390, 306));
        _preview.AddChild(Palette.Wrapped(entry.Definition.Name, 21));
        _preview.AddChild(Palette.Wrapped(entry.Eligible ? entry.Definition.Type + " · " + entry.Count + " remaining" : entry.Reason ?? "Unavailable for this search.", 17, entry.Eligible ? Palette.Gold : Palette.Error));
    }
    public static CardView Printed(CardDefinition definition, int viewer) => new("search-preview-" + definition.Id, definition, viewer, Zone.Hand, true, false, null, null, definition.Force, definition.Chi);
    private static void IgnoreInput(Node node)
    { if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) IgnoreInput(child); }
}
