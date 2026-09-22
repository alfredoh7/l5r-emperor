using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>A private local draft: choose outgoing cards, then review their return order.</summary>
public sealed partial class TopCardPanel : PanelContainer
{
    private readonly VBoxContainer _body = new();
    private HBoxContainer _row = null!;
    private readonly Dictionary<int, string> _destinations = [];
    private readonly List<int> _order = [];
    private readonly Dictionary<int, (VBoxContainer Box, TopCardFace Face, Dictionary<string, Button> Choices, Button Left, Label Order, Button Right)> _cards = [];
    private readonly List<(LookRuleView Rule, Label Label)> _counts = [];
    private LookView _look = null!;
    private bool _canSubmit;
    private int _viewer;
    private bool HasChoices => _look.Cards!.Any(c => c.Dispositions.Any(d => d.Disposition != _look.Remainder && d.Legal));
    public bool ReturnStep { get; private set; }
    public Button ContinueButton { get; private set; } = null!;
    public Button BackButton { get; private set; } = null!;
    public Button ConfirmButton { get; private set; } = null!;
    public Button ResetButton { get; private set; } = null!;
    public IReadOnlyList<int> Order => _order;
    public IReadOnlyDictionary<int, string> Destinations => _destinations;
    public Action<string[]>? Confirmed;
    public Action<LookDeckChoiceView>? DeckChosen;
    public Action<CardView>? Inspected;
    public Button Face(int position) => _cards[position].Face;
    public Button Choice(int position, string destination) => _cards[position].Choices[destination];
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
        _look = look; _canSubmit = canSubmit; _viewer = viewer;
        if (look.Stage == "ChooseDeck")
        {
            Center("CHOOSE A DECK TO VIEW", 30, title: true);
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
        foreach (var card in look.Cards!) { _order.Add(card.Position); _destinations[card.Position] = look.Remainder; }
        ReturnStep = !HasChoices; Build();
    }
    private void Build()
    {
        Palette.Clear(_body); _cards.Clear(); _counts.Clear();
        var take = (_look.Rules ?? []).Where(r => r.Max > 0).Select(r => r.Disposition).ToArray();
        Center(ReturnStep ? (_look.CanReorder ? "PUT THE CARDS BACK IN ORDER" : "REVIEW THE CARDS GOING BACK") :
            take.SequenceEqual(new[] { "hand" }) ? "CHOOSE CARDS TO TAKE" : take.SequenceEqual(new[] { "show" }) ? "CHOOSE CARDS TO SHOW AND TAKE" :
            take.SequenceEqual(new[] { "discard" }) ? "CHOOSE CARDS TO DISCARD" : "CHOOSE WHERE THE CARDS GO", 30, title: true);
        Center($"Top {_look.Cards!.Length} of {(_look.OwnDeck ? "your" : "your opponent’s")} {_look.Deck} deck", 23, _look.OwnDeck ? Palette.Paper : Palette.Gold);
        Center(_look.SourceName + " · Only you can see these cards.", 17, Palette.PaperDim);
        if (HasChoices) Center(ReturnStep ? "Step 2 of 2 · Return the rest" : "Step 1 of 2 · Choose cards", 16, Palette.Gold);
        if (!ReturnStep)
        {
            var rules = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; rules.AddThemeConstantOverride("separation", 26); _body.AddChild(rules);
            foreach (var rule in _look.Rules ?? [])
            {
                if (rule.Max == 0) continue;
                var label = Palette.Text("", 18); rules.AddChild(label); _counts.Add((rule, label));
            }
            var singleAction = _look.Cards.All(c => c.Dispositions.Count(d => d.Disposition != _look.Remainder) <= 1);
            Center(singleAction ? "Click a card or its button to choose it. Click again to undo. Unselected cards go back." :
                "Use the labeled buttons to choose each card’s destination. Click a selected button to undo.", 18, Palette.PaperDim);
        }
        else
        {
            var outgoing = _order.Where(p => _destinations[p] is not ("top" or "bottom")).GroupBy(p => _destinations[p]);
            foreach (var group in outgoing) Center(Outcome(group.Key) + ": " + string.Join(", ", group.Select(p => _look.Cards.Single(c => c.Position == p).Definition.Name)), 17, Palette.Gold);
            var returns = _destinations.Values.Where(d => d is "top" or "bottom").Distinct().ToArray();
            var location = returns.SequenceEqual(new[] { "bottom" }) ? "Below the rest of the deck · the rightmost card will be at the very bottom." :
                returns.SequenceEqual(new[] { "top" }) ? "On top of the deck · the leftmost card will be drawn first." : "Cards return to the top or bottom as labeled.";
            Center(location, 18, Palette.PaperDim);
            if (!_look.CanReorder) Center("The original order is fixed.", 16, Palette.PaperDim);
        }
        _row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; _row.AddThemeConstantOverride("separation", 28);
        var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddChild(_row); _body.AddChild(center);
        foreach (var card in _look.Cards)
        {
            if (ReturnStep && _destinations[card.Position] is not ("top" or "bottom")) continue;
            var position = card.Position;
            var box = new VBoxContainer { CustomMinimumSize = new(230, 0) }; box.AddThemeConstantOverride("separation", 6); _row.AddChild(box);
            var index = Palette.Text("", 18, Palette.Gold); index.HorizontalAlignment = HorizontalAlignment.Center; box.AddChild(index);
            var printed = DeckSearchPanel.Printed(card.Definition, _viewer);
            var actions = card.Dispositions.Where(d => d.Disposition != _look.Remainder).ToArray();
            var face = new TopCardFace(printed, !ReturnStep && actions.Length == 1, !ReturnStep && actions.Length > 1 ? 230 : 304); box.AddChild(face);
            face.Pressed += () => { if (!ReturnStep && actions.Length == 1) Choose(position, actions[0].Disposition); else Inspected?.Invoke(printed); };
            var choices = new Dictionary<string, Button>();
            if (!ReturnStep)
            {
                foreach (var d in actions)
                {
                    var button = SidebarSkin.Button(ActionLabel(d.Disposition, d.Label), () => Choose(position, d.Disposition));
                    button.CustomMinimumSize = new(230, 44); button.AddThemeFontSizeOverride("font_size", 17);
                    button.AccessibilityName = ActionLabel(d.Disposition, d.Label) + ": " + card.Definition.Name;
                    button.TooltipText = d.Reason ?? (d.Disposition == "show" ? "This card will be revealed to your opponent before entering your hand." : d.Label);
                    choices[d.Disposition] = button; box.AddChild(button);
                    if (!d.Legal && d.Reason is {} reason) box.AddChild(Palette.Wrapped(reason, 15, Palette.PaperDim));
                }
            }
            var moves = new HBoxContainer(); moves.AddThemeConstantOverride("separation", 8); box.AddChild(moves);
            Button Move(string caption, int direction)
            {
                var b = SidebarSkin.Button(caption, () => Shift(position, direction)); b.CustomMinimumSize = new(111, 38);
                b.AutowrapMode = TextServer.AutowrapMode.Off; b.AddThemeFontSizeOverride("font_size", 14);
                b.AccessibilityName = $"Move {card.Definition.Name} {(direction < 0 ? "earlier" : "later")}"; return b;
            }
            var left = Move("Move earlier", -1); var right = Move("Move later", 1); moves.AddChild(left); moves.AddChild(right);
            var inspect = SidebarSkin.Button("Inspect card", () => Inspected?.Invoke(printed)); inspect.CustomMinimumSize = new(230, 32); inspect.AddThemeFontSizeOverride("font_size", 15); box.AddChild(inspect);
            _cards[position] = (box, face, choices, left, index, right);
        }
        if (_cards.Count == 0) { var none = Palette.Wrapped("All viewed cards are going to the destinations above. Nothing will be returned to this deck.", 22); none.CustomMinimumSize = new(850, 0); _row.AddChild(none); }
        _body.AddChild(new HSeparator());
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; footer.AddThemeConstantOverride("separation", 14); _body.AddChild(footer);
        ResetButton = SidebarSkin.Button("Start over", () =>
        {
            if (!_canSubmit) return;
            _order.Clear(); foreach (var card in _look.Cards!) { _order.Add(card.Position); _destinations[card.Position] = _look.Remainder; }
            ReturnStep = !HasChoices; Build();
        }); ResetButton.CustomMinimumSize = new(175, 54); footer.AddChild(ResetButton);
        BackButton = SidebarSkin.Button("Change chosen cards", () => { if (!_canSubmit) return; ReturnStep = false; Build(); });
        BackButton.Visible = ReturnStep && HasChoices; BackButton.CustomMinimumSize = new(250, 54); footer.AddChild(BackButton);
        ContinueButton = SidebarSkin.Button("Next · return the rest", () => { if (!_canSubmit || !ValidDraft()) return; ReturnStep = true; Build(); }, true);
        ContinueButton.Visible = !ReturnStep; ContinueButton.CustomMinimumSize = new(320, 54); footer.AddChild(ContinueButton);
        ConfirmButton = SidebarSkin.Button("Confirm and finish", () =>
        {
            if (!_canSubmit || !ReturnStep || !ValidDraft()) return;
            _canSubmit = false; Paint(); Confirmed?.Invoke(_order.Select(p => $"{p}:{_destinations[p]}").ToArray());
        }, true); ConfirmButton.Visible = ReturnStep; ConfirmButton.CustomMinimumSize = new(300, 54); footer.AddChild(ConfirmButton);
        if (!_canSubmit) Center("Your choice is paused until the connection is ready.", 18, Palette.Gold);
        Paint();
    }
    private static string ActionLabel(string disposition, string fallback) => disposition switch
    { "hand" => "Take into hand", "show" => "Show and take", "discard" => "Discard this card", "bottom" => "Put on bottom", "top" => "Put on top", _ => fallback };
    private static string Outcome(string disposition) => disposition switch
    { "hand" => "To your hand", "show" => "Will be shown, then taken", "discard" => "To discard", "bottom" => "To bottom of deck", "top" => "To top of deck", _ => disposition };
    private void Choose(int position, string destination)
    {
        if (!_canSubmit || ReturnStep) return;
        var card = _look.Cards!.Single(c => c.Position == position);
        if (!card.Dispositions.Any(d => d.Disposition == destination && d.Legal)) return;
        _destinations[position] = _destinations[position] == destination ? _look.Remainder : destination;
        Paint();
    }
    private int[] Group(int position) => _order.Where(p => _destinations[p] == _destinations[position]).ToArray();
    private void Shift(int position, int direction)
    {
        if (!_canSubmit || !ReturnStep || !_look.CanReorder || _destinations[position] is not ("top" or "bottom")) return;
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
            var amount = rule.Min == rule.Max ? $"{rule.Max}" : rule.Min == 0 ? $"up to {rule.Max}" : $"{rule.Min}–{rule.Max}";
            label.Text = $"{rule.Label}: choose {amount} · {count} selected";
            label.Modulate = count >= rule.Min && count <= rule.Max ? Palette.Gold : Palette.Error;
        }
        var visible = _order.Where(_cards.ContainsKey).OrderBy(p => ReturnStep && _destinations[p] == "bottom" ? 1 : 0).ToArray();
        for (var i = 0; i < visible.Length; i++)
        {
            var p = visible[i]; var widget = _cards[p]; if (ReturnStep) _row.MoveChild(widget.Box, i);
            var card = _look.Cards!.Single(c => c.Position == p); var d = _destinations[p];
            foreach (var option in card.Dispositions.Where(o => widget.Choices.ContainsKey(o.Disposition)))
            {
                var button = widget.Choices[option.Disposition]; button.Disabled = !_canSubmit || !option.Legal;
                button.Text = d == option.Disposition ? "✓ Selected · Undo" : ActionLabel(option.Disposition, option.Label);
            }
            var group = Group(p); var index = Array.IndexOf(group, p);
            widget.Order.Text = ReturnStep ? (d == "top" ? index == 0 ? "TOP OF DECK · 1st" : $"{Ordinal(index + 1)} from top" : (index == group.Length - 1 ? "BOTTOM OF DECK" : $"{Ordinal(index + 1)} below remaining deck")) :
                d != _look.Remainder ? Outcome(d) : p == 0 ? "TOP CARD" : $"{Ordinal(p + 1)} card";
            widget.Left.Visible = widget.Right.Visible = ReturnStep && _look.CanReorder && group.Length > 1;
            widget.Left.Disabled = !_canSubmit || index == 0; widget.Right.Disabled = !_canSubmit || index == group.Length - 1;
            widget.Face.Mark(d != _look.Remainder);
        }
        ResetButton.Disabled = BackButton.Disabled = !_canSubmit;
        ContinueButton.Text = _destinations.Values.All(d => d == _look.Remainder) ? "Next · return all cards" : "Next · return the rest";
        ContinueButton.Disabled = ConfirmButton.Disabled = !_canSubmit || !ValidDraft();
    }
    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };
}

public sealed partial class TopCardFace : Button
{
    private readonly CardView _card;
    private readonly bool _chooses;
    public TopCardFace(CardView card, bool chooses = false, float height = 304)
    {
        _card = card; _chooses = chooses; CustomMinimumSize = new(230, height); TooltipText = card.Definition!.Name;
        AccessibilityName = (chooses ? "Choose " : "Inspect ") + card.Definition.Name;
        var face = CardPreview.Face(card, height - 16, 214); AddChild(face); face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
        GetTree().Root.GetVisibleRect().Size.Y / 1080f, interactionHint: _chooses ? "Click to choose or undo. Inspect card opens a larger view." : "Click to inspect.");
}
