using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Local arrangement of the engine's private top-N view. No deck access or eligibility inference.</summary>
public sealed partial class TopCardPanel : PanelContainer
{
    private readonly VBoxContainer _body = new();
    private HBoxContainer _row = null!;
    private readonly Dictionary<int, string> _destinations = [];
    private readonly List<int> _order = [];
    private readonly Dictionary<int, (VBoxContainer Box, TopCardFace Face, OptionButton Choice, Button Left, Label Order, Button Right)> _cards = [];
    private readonly List<(LookRuleView Rule, Label Label)> _counts = [];
    private LookView _look = null!;
    private bool _canSubmit;
    public Button ConfirmButton { get; private set; } = null!;
    public Button ResetButton { get; private set; } = null!;
    public IReadOnlyList<int> Order => _order;
    public IReadOnlyDictionary<int, string> Destinations => _destinations;
    public Action<string[]>? Confirmed;
    public Action<LookDeckChoiceView>? DeckChosen;
    public Action<CardView>? Inspected;
    public Button Face(int position) => _cards[position].Face;
    public OptionButton Choice(int position) => _cards[position].Choice;
    public Button MoveButton(int position, bool right) => right ? _cards[position].Right : _cards[position].Left;

    public TopCardPanel()
    {
        Theme = SidebarSkin.Theme(); AddThemeStyleboxOverride("panel", SidebarSkin.Surface());
        _body.AddThemeConstantOverride("separation", 8); AddChild(_body);
    }
    private void Center(string text, int size, Color? color = null, bool title = false)
    {
        var label = Palette.Wrapped(text, size, color); label.HorizontalAlignment = HorizontalAlignment.Center;
        if (title) label.AddThemeFontOverride("font", Palette.HeadingFace);
        _body.AddChild(label);
    }
    public void Populate(LookView look, bool canSubmit, int viewer)
    {
        _look = look; _canSubmit = canSubmit;
        Center(look.Stage == "ChooseDeck" ? "CHOOSE A DECK TO VIEW" : "ARRANGE VIEWED CARDS", 30, title: true);
        if (look.Stage == "ChooseDeck")
        {
            Center(look.SourceName, 22); Center("Only you will see the cards.", 17, Palette.Gold);
            var decks = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            decks.AddThemeConstantOverride("separation", 18); _body.AddChild(decks);
            foreach (var deck in look.DeckChoices ?? [])
            {
                var button = SidebarSkin.Button(deck.Label + "\n" + deck.CardsRemaining + " cards remaining", () =>
                {
                    if (!_canSubmit) return;
                    _canSubmit = false; foreach (var b in decks.GetChildren().OfType<Button>()) b.Disabled = true;
                    DeckChosen?.Invoke(deck);
                });
                button.SetMeta("deck", deck.Deck); button.SetMeta("owner", deck.OwnerId);
                button.CustomMinimumSize = new(0, 100); button.Disabled = !canSubmit; decks.AddChild(button);
            }
            if (!canSubmit) Center("Your choice is paused until the connection is ready.", 18, Palette.Gold);
            return;
        }
        Center($"Top {look.Cards!.Length} of {(look.OwnDeck ? "your" : "your opponent’s")} {look.Deck} deck", 24, look.OwnDeck ? Palette.Paper : Palette.Gold);
        Center(look.SourceName + " · Only you can see these cards.", 17, Palette.PaperDim);
        var rules = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; rules.AddThemeConstantOverride("separation", 26); if ((look.Rules ?? []).Any(r => r.Max > 0)) _body.AddChild(rules); else rules.Free();
        foreach (var rule in look.Rules ?? [])
        {
            if (rule.Max == 0) continue;
            var label = Palette.Text("", 18); rules.AddChild(label); _counts.Add((rule, label));
        }
        Center((look.Remainder == "bottom" ? "Return the rest to the bottom" : "Return the rest to the top") +
            (look.CanReorder ? " · arrange left to right." : " · keep their original order."), 18, Palette.PaperDim);
        _row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _row.AddThemeConstantOverride("separation", 28);
        var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddChild(_row); _body.AddChild(center);
        foreach (var card in look.Cards)
        {
            _order.Add(card.Position); _destinations[card.Position] = look.Remainder;
            var box = new VBoxContainer { CustomMinimumSize = new(230, 0) }; box.AddThemeConstantOverride("separation", 8); _row.AddChild(box);
            var printed = DeckSearchPanel.Printed(card.Definition, viewer);
            var face = new TopCardFace(printed); face.Pressed += () => Inspected?.Invoke(printed); box.AddChild(face);
            var choice = new OptionButton { CustomMinimumSize = new(230, 44), FitToLongestItem = false, ClipText = true };
            choice.AccessibilityName = "Destination for " + card.Definition.Name;
            for (var i = 0; i < card.Dispositions.Length; i++)
            {
                var d = card.Dispositions[i]; choice.AddItem(ShortDisposition(d));
                choice.SetItemDisabled(i, !d.Legal); choice.SetItemTooltip(i, d.Reason ?? (d.Disposition == "show" ? "This card will be revealed to your opponent before entering your hand." : d.Label));
                if (d.Disposition == look.Remainder) choice.Selected = i;
            }
            choice.Disabled = !canSubmit || card.Dispositions.Length == 1;
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            {
                var style = SidebarSkin.Surface(true); style.ContentMarginLeft = 12; style.ContentMarginRight = 22;
                style.ContentMarginTop = style.ContentMarginBottom = 7; choice.AddThemeStyleboxOverride(state, style);
            }
            choice.AddThemeFontSizeOverride("font_size", 17); box.AddChild(choice);
            choice.ItemSelected += i =>
            {
                if (!_canSubmit || !card.Dispositions[(int)i].Legal) return;
                _destinations[card.Position] = card.Dispositions[(int)i].Disposition; Paint();
            };
            var order = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; order.AddThemeConstantOverride("separation", 7); box.AddChild(order);
            Button Move(string caption, int direction)
            {
                var b = SidebarSkin.Button(caption, () => Shift(card.Position, direction)); b.CustomMinimumSize = new(42, 36);
                b.AutowrapMode = TextServer.AutowrapMode.Off; b.AddThemeFontSizeOverride("font_size", 24);
                b.AccessibilityName = $"Move {card.Definition.Name} {(direction < 0 ? "earlier" : "later")} in its return order";
                foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
                { var style = Palette.Box(new Color("171a1b"), state == "disabled" ? new Color("534b3a") : Palette.Gold, 4, 4); b.AddThemeStyleboxOverride(state, style); }
                return b;
            }
            var left = Move("‹", -1); var index = Palette.Text("", 18, Palette.Gold); index.CustomMinimumSize = new(118, 36); index.HorizontalAlignment = HorizontalAlignment.Center; index.VerticalAlignment = VerticalAlignment.Center;
            var right = Move("›", 1); order.AddChild(left); order.AddChild(index); order.AddChild(right);
            _cards[card.Position] = (box, face, choice, left, index, right);
        }
        _body.AddChild(new HSeparator());
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; footer.AddThemeConstantOverride("separation", 14); _body.AddChild(footer);
        ResetButton = SidebarSkin.Button("Reset arrangement", () =>
        {
            if (!_canSubmit) return;
            _order.Clear(); foreach (var card in _look.Cards!) { _order.Add(card.Position); _destinations[card.Position] = _look.Remainder; }
            Paint();
        }); ResetButton.CustomMinimumSize = new(240, 54); ResetButton.AutowrapMode = TextServer.AutowrapMode.Off; ResetButton.Disabled = !canSubmit; footer.AddChild(ResetButton);
        ConfirmButton = SidebarSkin.Button("Confirm arrangement", () =>
        {
            if (!_canSubmit || !ValidDraft()) return;
            _canSubmit = false; Paint(); Confirmed?.Invoke(_order.Select(p => $"{p}:{_destinations[p]}").ToArray());
        }, true); ConfirmButton.CustomMinimumSize = new(300, 54); ConfirmButton.AutowrapMode = TextServer.AutowrapMode.Off; footer.AddChild(ConfirmButton);
        if (!canSubmit) Center("Your choice is paused until the connection is ready.", 18, Palette.Gold);
        Paint();
    }
    private static string ShortDisposition(LookDispositionView value) => value.Disposition switch
    { "top" => "Keep on top", "bottom" => "To bottom", "hand" => "To hand", "show" => "Show + take", "discard" => "Discard", _ => value.Label };
    private int[] Group(int position) => _order.Where(p => _destinations[p] == _destinations[position]).ToArray();
    private void Shift(int position, int direction)
    {
        if (!_canSubmit || !_look.CanReorder || _destinations[position] is not ("top" or "bottom")) return;
        var group = Group(position); var next = Array.IndexOf(group, position) + direction;
        if (next < 0 || next >= group.Length) return;
        var a = _order.IndexOf(position); var b = _order.IndexOf(group[next]); (_order[a], _order[b]) = (_order[b], _order[a]);
        Paint(); (direction < 0 ? _cards[position].Left : _cards[position].Right).CallDeferred(Control.MethodName.GrabFocus);
    }
    private bool ValidDraft() => (_look.Rules ?? []).All(r =>
        _destinations.Values.Count(d => d == r.Disposition) >= r.Min && _destinations.Values.Count(d => d == r.Disposition) <= r.Max) &&
        _look.Cards!.All(c => c.Dispositions.Any(d => d.Disposition == _destinations[c.Position] && d.Legal));
    private void Paint()
    {
        foreach (var (rule, label) in _counts)
        {
            var count = _destinations.Values.Count(d => d == rule.Disposition);
            label.Text = $"{rule.Label}: {count} / {(rule.Min == rule.Max ? rule.Max.ToString() : $"{rule.Min}–{rule.Max}")}";
            label.Modulate = count >= rule.Min && count <= rule.Max ? Palette.Gold : Palette.Error;
        }
        for (var i = 0; i < _order.Count; i++)
        {
            var p = _order[i]; var widget = _cards[p]; _row.MoveChild(widget.Box, i);
            var card = _look.Cards!.Single(c => c.Position == p); var d = _destinations[p];
            widget.Choice.Selected = Array.FindIndex(card.Dispositions, o => o.Disposition == d); widget.Choice.Disabled = !_canSubmit || card.Dispositions.Length == 1;
            var group = Group(p); var index = Array.IndexOf(group, p); var returns = d is "top" or "bottom";
            widget.Order.Text = returns ? (d == "top" ? "Top " : "Bottom ") + (index + 1) : d == "show" ? "Will be shown" : d == "hand" ? "To your hand" : "To discard";
            widget.Left.Visible = widget.Right.Visible = returns && _look.CanReorder;
            widget.Left.Disabled = !_canSubmit || index == 0; widget.Right.Disabled = !_canSubmit || index == group.Length - 1;
            widget.Face.Mark(d != _look.Remainder);
        }
        ResetButton.Disabled = !_canSubmit; ConfirmButton.Disabled = !_canSubmit || !ValidDraft();
    }
}

public sealed partial class TopCardFace : Button
{
    private readonly CardView _card;
    public TopCardFace(CardView card)
    {
        _card = card; CustomMinimumSize = new(230, 304); TooltipText = card.Definition!.Name;
        AccessibilityName = "Inspect " + card.Definition.Name;
        var face = CardPreview.Face(card, 288, 214); AddChild(face); face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        face.OffsetLeft = face.OffsetTop = 8; face.OffsetRight = face.OffsetBottom = -8;
        Ignore(face); Mark(false);
    }
    private static void Ignore(Node node) { if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) Ignore(child); }
    public void Mark(bool selected)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            var style = SidebarSkin.Inset(); style.ModulateColor = selected ? new Color(1.6f, 1.35f, .8f) : state is "hover" or "focus" ? new Color(1.2f, 1.15f, 1) : Colors.White;
            AddThemeStyleboxOverride(state, style);
        }
    }
    public override Control _MakeCustomTooltip(string forText) => CardPreview.Create(_card, "Private deck view", null,
        GetTree().Root.GetVisibleRect().Size.Y / 1080f, interactionHint: "Click to inspect. Use the controls beneath the card to arrange it.");
}
