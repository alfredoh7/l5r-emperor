using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>One private, reversible arrangement. Only Confirm submits to the engine.</summary>
public sealed partial class TopCardPanel : PanelContainer
{
    private readonly VBoxContainer _body = new();
    private readonly Control _workspace = new() { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
    private readonly Dictionary<int, string> _destinations = [];
    private readonly List<int> _order = [];
    private readonly Dictionary<int, (TopCardFace Face, Label Caption)> _cards = [];
    private readonly Dictionary<string, TopCardTray> _trays = [];
    private readonly Stack<(int[] Order, Dictionary<int, string> Destinations)> _undo = [];
    private readonly Dictionary<int, Tween> _motion = [];
    private readonly Panel _landing = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _landingText = Palette.Wrapped("", 18, Palette.Gold);
    private Label _hint = null!;
    private LookView _look = null!;
    private bool _canSubmit, _laidOut;
    private int _viewer;
    private int? _dragged, _picked;
    private (string Destination, int Index)? _preview;
    public bool ReducedMotion { get; set; }
    public Func<Vector2>? PointerPosition { get; set; }
    public Button ConfirmButton { get; private set; } = null!;
    public Button UndoButton { get; private set; } = null!;
    public IReadOnlyList<int> Order => _order;
    public IReadOnlyDictionary<int, string> Destinations => _destinations;
    public IEnumerable<string> TrayDestinations => _trays.Keys;
    public int? Picked => _picked;
    public Rect2? LandingRect => _landing.Visible ? _landing.GetGlobalRect() : null;
    public Action<string[]>? Confirmed;
    public Action<LookDeckChoiceView>? DeckChosen;
    public Action<CardView>? Inspected;
    public Button Face(int position) => _cards[position].Face;
    public Button Tray(string destination) => _trays[destination];
    public string PositionLabel(int position) => _cards[position].Caption.Text;

    public TopCardPanel()
    {
        Theme = SidebarSkin.Theme(); AddThemeStyleboxOverride("panel", TopCardSkin.Frame());
        _body.AddThemeConstantOverride("separation", 8); AddChild(_body);
        _body.AddChild(_workspace); _workspace.Hide(); _workspace.AddChild(_landing);
        _workspace.Resized += () => LayoutCards(false);
        _landing.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var landingArt = new TextureRect
        {
            Texture = TopCardSkin.Slot,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _landing.AddChild(landingArt); landingArt.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _landingText.HorizontalAlignment = HorizontalAlignment.Center; _landingText.VerticalAlignment = VerticalAlignment.Top;
        _landingText.MouseFilter = MouseFilterEnum.Ignore; _landing.AddChild(_landingText);
        _landingText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _landingText.OffsetLeft = 20; _landingText.OffsetRight = -20; _landingText.OffsetTop = 30;
    }
    private void Center(string text, int size, Color? color = null, bool title = false)
    {
        var label = Palette.Wrapped(text, size, color); label.HorizontalAlignment = HorizontalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        if (title) label.AddThemeFontOverride("font", Palette.HeadingFace);
        _body.AddChild(label);
    }
    public void Populate(LookView look, bool canSubmit, int viewer)
    {
        _look = look; _canSubmit = canSubmit; _viewer = viewer;
        if (look.Stage == "ChooseDeck")
        {
            Center("CHOOSE A DECK TO VIEW", 32, title: true); Center(look.SourceName, 23);
            Center("Only you will see the cards.", 18, Palette.Gold);
            var decks = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            decks.AddThemeConstantOverride("separation", 18); _body.AddChild(decks);
            foreach (var deck in look.DeckChoices ?? [])
            {
                var button = SidebarSkin.Button(deck.Label, () =>
                {
                    if (!_canSubmit) return;
                    _canSubmit = false; foreach (var b in decks.GetChildren().OfType<Button>()) b.Disabled = true;
                    DeckChosen?.Invoke(deck);
                });
                button.SetMeta("deck", deck.Deck); button.SetMeta("owner", deck.OwnerId);
                button.CustomMinimumSize = new(0, 100); button.Disabled = !canSubmit; decks.AddChild(button);
            }
            return;
        }
        foreach (var card in look.Cards!) { _order.Add(card.Position); _destinations[card.Position] = look.Remainder; }
        var heading = new CenterContainer(); _body.AddChild(heading);
        var title = new Control { CustomMinimumSize = new(760, 82), MouseFilter = MouseFilterEnum.Ignore }; heading.AddChild(title);
        var brush = new TextureRect
        {
            Texture = TopCardSkin.Brush,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddChild(brush); brush.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var titleText = look.SourceName.ToUpperInvariant();
        var measuredTitle = Palette.HeadingLabel(56).GetStringSize(titleText, fontSize: 56).X;
        var titleSize = Math.Min(56, (int)Math.Floor(56 * 612 / Math.Max(1, measuredTitle)));
        var name = Palette.Text(titleText, titleSize, new Color("16120d"));
        name.AddThemeFontOverride("font", Palette.HeadingLabel(titleSize));
        name.HorizontalAlignment = HorizontalAlignment.Center; name.VerticalAlignment = VerticalAlignment.Center;
        name.MouseFilter = MouseFilterEnum.Ignore; name.ClipText = true;
        title.AddChild(name); name.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); name.OffsetLeft = 66; name.OffsetRight = -66;
        Center(Instructions(), 25);
        Center($"Top {look.Cards.Length} {(look.Cards.Length == 1 ? "card" : "cards")} of {(look.OwnDeck ? "your" : "your opponent’s")} {look.Deck} deck · Only you can see these cards.", 18, Palette.Gold);
        _body.MoveChild(_workspace, -1); _workspace.Show(); _workspace.CustomMinimumSize = new(0, 460);
        var offered = (look.Rules ?? []).Where(r => r.Max > 0).Select(r => r.Disposition).Append(look.Remainder).ToHashSet();
        foreach (var destination in new[] { "top", "bottom", "hand", "show", "discard" }.Where(offered.Contains))
        {
            var tray = new TopCardTray(this, destination, Title(destination), IsReturn(destination) ? look.Deck : null);
            _workspace.AddChild(tray); _trays[destination] = tray;
        }
        _workspace.MoveChild(_landing, -1);
        foreach (var card in look.Cards)
        {
            var face = new TopCardFace(this, card.Position, DeckSearchPanel.Printed(card.Definition, viewer));
            var caption = Palette.Wrapped("", 20, Palette.Gold); caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.MouseFilter = MouseFilterEnum.Ignore;
            _workspace.AddChild(face); _workspace.AddChild(caption); _cards[card.Position] = (face, caption);
        }
        _hint = Palette.Wrapped("", 17, Palette.PaperDim); _hint.CustomMinimumSize = new(0, 26); _body.AddChild(_hint);
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 18); _body.AddChild(footer);
        var help = Palette.Text("Drag to arrange · Hover to inspect", 16, Palette.PaperDim);
        help.TooltipText = "Click or Enter to pick up and place. Left / right chooses the landing position. Escape cancels. I or right-click opens the full card.";
        help.MouseFilter = MouseFilterEnum.Stop; help.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(help);
        UndoButton = SidebarSkin.Button("Undo", Undo); UndoButton.CustomMinimumSize = new(190, 58); footer.AddChild(UndoButton);
        ConfirmButton = SidebarSkin.Button("Confirm arrangement", () =>
        {
            if (!_canSubmit || _dragged is not null || _picked is not null || !ValidDraft()) return;
            _canSubmit = false; Paint(); Confirmed?.Invoke(_order.Select(p => $"{p}:{_destinations[p]}").ToArray());
        }, true); ConfirmButton.CustomMinimumSize = new(330, 54); footer.AddChild(ConfirmButton);
        Paint(); Callable.From(() => LayoutCards(false)).CallDeferred();
    }
    private string Instructions()
    {
        var rules = (_look.Rules ?? []).Where(r => r.Max > 0).ToArray();
        if (rules.Length == 0) return _look.CanReorder ? "Drag the cards into the order you want to draw them." : "Review the cards. Their original order is fixed.";
        if (rules.Length == 1 && rules[0] is { Disposition: "hand", Min: 1, Max: 1 })
            return $"Drag 1 card into your hand. {(_look.CanReorder ? "Arrange" : "Return")} the other {_look.Cards!.Length - 1} {(_look.Remainder == "bottom" ? "at the bottom" : "on top of the deck")}.";
        return "Drag cards into the labeled trays. Confirm when the arrangement is ready.";
    }
    private static bool IsReturn(string destination) => destination is "top" or "bottom";
    private string Title(string destination) => destination switch
    {
        "top" => $"TOP OF {(_look.OwnDeck ? "YOUR" : "OPPONENT’S")} {_look.Deck!.ToUpperInvariant()} DECK",
        "bottom" => $"BOTTOM OF {(_look.OwnDeck ? "YOUR" : "OPPONENT’S")} {_look.Deck!.ToUpperInvariant()} DECK",
        "hand" => "YOUR HAND",
        "show" => "SHOW AND TAKE",
        "discard" => _look.OwnDeck ? "YOUR DISCARD PILE" : "OPPONENT’S DISCARD PILE",
        _ => destination
    };
    private int[] Group(string destination, int? omit = null) => _order.Where(p => p != omit && _destinations[p] == destination).ToArray();
    private string SlotLabel(string destination, int index, int count) => destination switch
    {
        "top" => index == 0 ? "1 · Drawn first" : $"{index + 1} · Below card {index}",
        "bottom" => count == 1 ? "1 · Very bottom\nImmediately below the deck" : index == 0 ? "1 · Immediately\nbelow the deck" : index == count - 1 ? $"{index + 1} · Very bottom" : $"{index + 1} · Below card {index}",
        "show" => "Will be shown to your opponent",
        "discard" => "Public after confirmation",
        _ => "Into your hand"
    };
    private bool ValidDraft() => (_look.Rules ?? []).All(r =>
        _destinations.Values.Count(d => d == r.Disposition) >= r.Min && _destinations.Values.Count(d => d == r.Disposition) <= r.Max) &&
        _look.Cards!.All(c => c.Dispositions.Any(d => d.Disposition == _destinations[c.Position] && d.Legal));
    public string? DropReason(int position, string destination)
    {
        if (!_canSubmit) return "Your choice is paused until the connection is ready.";
        var option = _look.Cards!.Single(c => c.Position == position).Dispositions.FirstOrDefault(d => d.Disposition == destination);
        if (option is null || !option.Legal) return option?.Reason ?? "This destination is not available for this card.";
        var rule = _look.Rules?.FirstOrDefault(r => r.Disposition == destination);
        if (_destinations[position] != destination && rule is not null && Group(destination).Length >= rule.Max)
        {
            // A single-slot tray that is already full still accepts the drop as a swap with its sole occupant,
            // bumped back to this card's own prior destination, as long as that occupant is legal there too.
            if (rule.Max == 1 && Group(destination, position) is [var occupant] &&
                _look.Cards!.Single(c => c.Position == occupant).Dispositions.Any(d => d.Disposition == _destinations[position] && d.Legal))
                return null;
            return $"This tray is full ({rule.Max}). Move a card out first.";
        }
        return null;
    }
    private void Paint()
    {
        if (_look.Stage != "Arrange") return;
        foreach (var (destination, tray) in _trays)
        {
            var rule = _look.Rules?.FirstOrDefault(r => r.Disposition == destination);
            var count = Group(destination).Length;
            tray.Count.Text = rule is null ? (_look.CanReorder ? "" : "Original order fixed") :
                rule.Min == rule.Max ? (count == rule.Max ? $"{count} of {rule.Max} · Ready" : $"Choose {rule.Max} · {count} placed") : rule.Min == 0 ? $"{count} of {rule.Max} · Optional" : $"{count} selected · Choose {rule.Min}–{rule.Max}";
            tray.Count.Modulate = rule is not null && (count < rule.Min || count > rule.Max) ? Palette.Gold : Palette.PaperDim;
            tray.Disabled = !_canSubmit;
            tray.TooltipText = (_dragged ?? _picked) is int p ? DropReason(p, destination) ?? "Place card here" : "Pick up a card, then choose this tray.";
            tray.Mark(_preview?.Destination == destination, (_dragged ?? _picked) is int current && DropReason(current, destination) is not null);
        }
        foreach (var (position, card) in _cards)
        {
            card.Face.Disabled = !_canSubmit; card.Face.SetDragged(_dragged == position);
            card.Face.Mark(_picked == position);
        }
        UndoButton.Disabled = !_canSubmit || _undo.Count == 0 || _dragged is not null;
        ConfirmButton.Disabled = !_canSubmit || !ValidDraft() || _dragged is not null || _picked is not null;
        _hint.Text = !_canSubmit ? "Your choice is paused until the connection is ready." : _picked is int pick ?
            $"{_look.Cards!.Single(c => c.Position == pick).Definition.Name} · Choose a tray and a landing position. Escape cancels." :
            ValidDraft() ? "Review the destinations and deck order, then confirm." : "Fill the required tray before confirming. Moving cards only changes your draft.";
    }
    public void Pick(int position)
    {
        if (!_canSubmit || GetViewport().GuiIsDragging()) return;
        if (_picked == position) { CancelPlacement(); return; }
        if (_picked is int held)
        {
            var destination = _destinations[position];
            Move(held, destination, Array.IndexOf(Group(destination, held), position)); return;
        }
        _picked = position; _preview = (_destinations[position], Array.IndexOf(Group(_destinations[position]), position));
        Paint(); LayoutCards(true); _trays[_destinations[position]].GrabFocus();
    }
    public void Inspect(int position) => Inspected?.Invoke(DeckSearchPanel.Printed(_look.Cards!.Single(c => c.Position == position).Definition, _viewer));
    public void CancelPlacement()
    {
        _picked = null; _preview = null; Paint(); LayoutCards(true);
    }
    private int FixedIndex(int position, string destination) => Group(destination, position).Count(p => p < position);
    public void PreviewKeyboard(string destination, int shift = 0)
    {
        if (_picked is not int position) return;
        var index = _preview?.Destination == destination ? _preview.Value.Index + shift : Group(destination, position).Length;
        _preview = (destination, _look.CanReorder || !IsReturn(destination) ? Math.Clamp(index, 0, Group(destination, position).Length) : FixedIndex(position, destination));
        Paint(); LayoutCards(true);
        if (DropReason(position, destination) is { } reason) _hint.Text = reason;
    }
    public void PlacePicked(string destination)
    {
        if (_picked is not int position) return;
        PreviewKeyboard(destination); var index = _preview!.Value.Index;
        Move(position, destination, index);
    }
    private void Move(int position, string destination, int index)
    {
        if (DropReason(position, destination) is { } reason) { _hint.Text = reason; return; }
        var prior = (_order.ToArray(), new Dictionary<int, string>(_destinations));
        var sourceDestination = _destinations[position];
        var rule = _look.Rules?.FirstOrDefault(r => r.Disposition == destination);
        if (rule is { Max: 1 } && sourceDestination != destination && Group(destination, position) is [var occupant])
            _destinations[occupant] = sourceDestination; // Swap: the displaced card takes the dragged card's old spot.
        var group = Group(destination, position);
        if (!_look.CanReorder && IsReturn(destination)) index = FixedIndex(position, destination);
        index = Math.Clamp(index, 0, group.Length);
        _order.Remove(position); _destinations[position] = destination;
        if (index < group.Length) _order.Insert(_order.IndexOf(group[index]), position);
        else if (group.Length > 0) _order.Insert(_order.IndexOf(group[^1]) + 1, position);
        else _order.Add(position);
        if (!_look.CanReorder) _order.Sort();
        if (!prior.Item1.SequenceEqual(_order) || prior.Item2.Any(pair => _destinations[pair.Key] != pair.Value)) _undo.Push(prior);
        _picked = null; _preview = null; Paint(); LayoutCards(true);
    }
    private void Undo()
    {
        if (!_canSubmit || _dragged is not null || !_undo.TryPop(out var state)) return;
        _order.Clear(); _order.AddRange(state.Order); _destinations.Clear();
        foreach (var pair in state.Destinations) _destinations[pair.Key] = pair.Value;
        CancelPlacement();
    }
    public Variant BeginDrag(int position, Control source, Vector2 atPosition)
    {
        if (!_canSubmit || (!_look.CanReorder && _trays.Count == 1)) return default;
        _picked = null; _dragged = position; _preview = null;
        var scale = source.GetGlobalTransform().Scale;
        var preview = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 200, ZAsRelative = false };
        var face = CardPreview.Face(DeckSearchPanel.Printed(_look.Cards!.Single(c => c.Position == position).Definition, _viewer), source.Size.Y * scale.Y, source.Size.X * scale.X);
        preview.AddChild(face); face.Position = -atPosition * scale; face.Size = source.Size * scale;
        if (!ReducedMotion) face.Rotation = .055f;
        source.SetDragPreview(preview); Paint(); LayoutCards(false);
        return new Godot.Collections.Dictionary { ["top_card_panel"] = GetInstanceId(), ["position"] = position };
    }
    private bool OwnDrag(Variant data, out int position)
    {
        position = -1;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var map = data.AsGodotDictionary();
        if (!map.TryGetValue("top_card_panel", out var id) || id.AsUInt64() != GetInstanceId() || !map.TryGetValue("position", out var p)) return false;
        position = p.AsInt32(); return _cards.ContainsKey(position);
    }
    private (string Destination, int Index)? TargetAt(Vector2 global, int position)
    {
        foreach (var (destination, tray) in _trays)
        {
            if (!tray.GetGlobalRect().HasPoint(global)) continue;
            var group = Group(destination, position); var local = _workspace.GetGlobalTransform().AffineInverse() * global;
            var slots = SlotRects(tray, group.Length + 1);
            var index = Enumerable.Range(0, slots.Length).MinBy(i => Math.Abs(slots[i].GetCenter().X - local.X));
            return (destination, !_look.CanReorder && IsReturn(destination) ? FixedIndex(position, destination) : index);
        }
        return null;
    }
    public bool CanDrop(Vector2 global, Variant data) => OwnDrag(data, out var position) && TargetAt(global, position) is { } target && DropReason(position, target.Destination) is null;
    public void Drop(Vector2 global, Variant data)
    {
        if (OwnDrag(data, out var position) && TargetAt(global, position) is { } target)
        {
            Move(position, target.Destination, target.Index);
        }
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => CanDrop(GetGlobalTransform() * atPosition, data);
    public override void _DropData(Vector2 atPosition, Variant data) => Drop(GetGlobalTransform() * atPosition, data);
    public void CancelPrivateDrag()
    {
        if (_dragged is not null && IsInsideTree()) GetViewport().GuiCancelDrag();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _dragged is not null)
        { _dragged = null; _preview = null; Paint(); LayoutCards(true); }
    }
    public override void _Input(InputEvent @event)
    {
        if (_look?.Stage != "Arrange" || @event is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape && (_picked is not null || _dragged is not null))
        { if (GetViewport().GuiIsDragging()) GetViewport().GuiCancelDrag(); CancelPlacement(); GetViewport().SetInputAsHandled(); }
    }
    public override void _Process(double delta)
    {
        if (_dragged is not int position) return;
        var target = TargetAt(PointerPosition?.Invoke() ?? GetGlobalMousePosition(), position);
        if (target == _preview) return;
        _preview = target; Paint(); LayoutCards(true);
        _hint.Text = target is { } t ? DropReason(position, t.Destination) ?? "Release in the highlighted slot. Escape cancels." : "Release outside a tray to cancel.";
    }
    private Rect2[] SlotRects(TopCardTray tray, int count)
    {
        count = Math.Max(1, count);
        var anchorWidth = IsReturn(tray.Destination) ? Math.Min(218, tray.Size.X * .21f) : 0;
        var area = new Rect2(tray.Position + new Vector2(24 + (tray.Destination == "bottom" ? anchorWidth : 0), 98),
            new Vector2(tray.Size.X - 48 - anchorWidth, tray.Size.Y - 176));
        var width = Math.Min((area.Size.X - (count - 1) * 22) / count, Math.Min(area.Size.Y / 1.4f, 306));
        width = Math.Max(38, width); var height = width * 1.4f;
        var start = area.Position + new Vector2((area.Size.X - count * width - (count - 1) * 22) / 2, (area.Size.Y - height) / 2);
        return Enumerable.Range(0, count).Select(i => new Rect2(start + new Vector2(i * (width + 22), 0), new Vector2(width, height))).ToArray();
    }
    public Vector2 DropPoint(string destination, int index, int position)
    {
        var slots = SlotRects(_trays[destination], Group(destination, position).Length + 1);
        return _workspace.GetGlobalTransform() * slots[Math.Clamp(index, 0, slots.Length - 1)].GetCenter();
    }
    private void LayoutCards(bool animate)
    {
        if (_look?.Stage != "Arrange" || _workspace.Size.X < 20 || _workspace.Size.Y < 20) return;
        var returns = _trays.Values.Where(t => IsReturn(t.Destination)).ToArray();
        var outgoing = _trays.Values.Where(t => !IsReturn(t.Destination)).ToArray();
        var sideWidth = outgoing.Length == 0 ? 0 : Math.Clamp(_workspace.Size.X * .235f, 290, 402);
        var mainWidth = _workspace.Size.X - (outgoing.Length == 0 ? 0 : sideWidth + 16);
        void Arrange(TopCardTray[] trays, float x, float width)
        {
            for (var i = 0; i < trays.Length; i++)
            {
                var height = (_workspace.Size.Y - (trays.Length - 1) * 12) / trays.Length;
                trays[i].Position = new(x, i * (height + 12)); trays[i].Size = new(width, height); trays[i].LayoutAnchor();
            }
        }
        if (_trays.Count <= 2 && returns.Length == 1)
        { Arrange(returns, 0, mainWidth); Arrange(outgoing, mainWidth + 16, sideWidth); }
        else
        {
            var unit = (_workspace.Size.X - (_trays.Count - 1) * 16) / (returns.Length * 1.35f + outgoing.Length);
            var x = 0f;
            foreach (var tray in returns.Concat(outgoing))
            {
                var width = unit * (IsReturn(tray.Destination) ? 1.35f : 1);
                tray.Position = new(x, 0); tray.Size = new(width, _workspace.Size.Y); tray.LayoutAnchor(); x += width + 16;
            }
        }
        _landing.Hide();
        if (_dragged is int hiddenCaption) _cards[hiddenCaption].Caption.Hide();
        var active = _dragged ?? _picked;
        foreach (var (destination, tray) in _trays)
        {
            var previewing = active is int a && _preview?.Destination == destination && DropReason(a, destination) is null;
            var contents = Group(destination, _dragged).ToList();
            if (previewing)
            {
                contents.Remove(active!.Value); contents.Insert(Math.Clamp(_preview!.Value.Index, 0, contents.Count), active.Value);
            }
            var slots = SlotRects(tray, contents.Count);
            tray.Empty.Visible = contents.Count == 0;
            tray.EmptySlot.Visible = contents.Count == 0;
            tray.EmptySlot.Position = slots[0].Position - tray.Position - new Vector2(9, 9);
            tray.EmptySlot.Size = slots[0].Size + new Vector2(18, 18);
            tray.Empty.Text = IsReturn(destination) ? "Drop cards here\nto return them" : "Drop card here";
            tray.Empty.Position = tray.EmptySlot.Position + new Vector2(22, 32);
            tray.Empty.Size = new(tray.EmptySlot.Size.X - 44, 60);
            for (var i = 0; i < contents.Count; i++)
            {
                var position = contents[i]; var card = _cards[position]; var rect = slots[i];
                if (previewing && position == active)
                {
                    _landing.Position = rect.Position - new Vector2(9, 9); _landing.Size = rect.Size + new Vector2(18, 18); _landing.Show();
                    _landingText.Text = destination == "hand" ? "Release to put in hand" : (_dragged is not null ? "Release here" : "Place here");
                }
                if (_dragged != position)
                {
                    if (_motion.Remove(position, out var previous)) previous.Kill();
                    if (animate && _laidOut && !ReducedMotion)
                    {
                        var tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                        tween.TweenProperty(card.Face, "position", rect.Position, .15); tween.TweenProperty(card.Face, "size", rect.Size, .15); _motion[position] = tween;
                    }
                    else { card.Face.Position = rect.Position; card.Face.Size = rect.Size; }
                }
                card.Caption.Show(); card.Caption.Text = SlotLabel(destination, i, contents.Count);
                var captionWidth = contents.Count == 1 ? Math.Min(tray.Size.X - 40, 280) : Math.Max(rect.Size.X + 12, Math.Min(240, (tray.Size.X - 40) / contents.Count - 12));
                card.Caption.Position = rect.Position + new Vector2((rect.Size.X - captionWidth) / 2, rect.Size.Y + 8);
                card.Caption.Size = new(captionWidth, 55);
                card.Face.AccessibilityName = _look.Cards!.Single(c => c.Position == position).Definition.Name + ", " + Title(destination) + ", " + card.Caption.Text;
            }
        }
        _laidOut = true;
    }
}

public sealed partial class TopCardTray : Button
{
    private readonly TopCardPanel _panel;
    private readonly Label _title;
    private readonly Control? _anchor;
    private readonly Label? _anchorLabel;
    public readonly string Destination;
    public readonly Label Count = Palette.Text("", 21, Palette.PaperDim);
    public readonly Label Empty = Palette.Wrapped("Drop card here", 21, Palette.PaperDim);
    public readonly TextureRect EmptySlot = new()
    {
        Texture = TopCardSkin.Slot,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = MouseFilterEnum.Ignore,
        Modulate = new Color(1, 1, 1, .62f)
    };
    public TopCardTray(TopCardPanel panel, string destination, string title, string? deck)
    {
        _panel = panel; Destination = destination; MouseDefaultCursorShape = CursorShape.PointingHand;
        _title = Palette.Wrapped(title, 28, Palette.Paper); _title.HorizontalAlignment = HorizontalAlignment.Center;
        Count.HorizontalAlignment = HorizontalAlignment.Center; Empty.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(EmptySlot);
        foreach (var label in new[] { _title, Count, Empty }) { label.MouseFilter = MouseFilterEnum.Ignore; AddChild(label); }
        AccessibilityName = title + " · destination tray";
        if (deck is not null)
        {
            _anchor = new Control { MouseFilter = MouseFilterEnum.Ignore }; AddChild(_anchor);
            for (var i = 4; i >= 0; i--)
            {
                var back = new TextureRect
                {
                    Texture = GD.Load<Texture2D>(deck == "Fate" ? "res://Art/original-fate-back.png" : "res://Art/original-dynasty-back.png"),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Position = new(i * 3, -i * 3),
                    Size = new(158, 222)
                };
                _anchor.AddChild(back);
            }
            _anchorLabel = Palette.Wrapped("Rest of deck", 20, Palette.Gold); _anchorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _anchorLabel.MouseFilter = MouseFilterEnum.Ignore; AddChild(_anchorLabel);
        }
        Pressed += () => _panel.PlacePicked(Destination);
        FocusEntered += () => _panel.PreviewKeyboard(Destination);
        Mark(false, false);
    }
    public void LayoutAnchor()
    {
        _title.AddThemeFontSizeOverride("font_size", Size.X < 600 ? 24 : 28);
        _title.Position = new(18, 15); _title.Size = new(Size.X - 36, 52); _title.VerticalAlignment = VerticalAlignment.Center;
        Count.Position = new(18, 66); Count.Size = new(Size.X - 36, 26);
        var reserved = _anchor is null ? 0 : Math.Min(218, Size.X * .21f);
        Empty.Position = new(18 + (Destination == "bottom" ? reserved : 0), Size.Y / 2 - 24);
        Empty.Size = new(Size.X - 36 - reserved, 65);
        if (_anchor is not null)
        {
            var scale = Math.Min(1, Size.X / 1100); _anchor.Scale = Vector2.One * scale;
            _anchor.Position = new(Destination == "bottom" ? 34 : Size.X - 34 - 170 * scale, Size.Y / 2 - 95 * scale);
            _anchorLabel!.Position = _anchor.Position + new Vector2(-16, 222 * scale + 16);
            _anchorLabel.Size = new(158 * scale + 32, 52);
        }
    }
    public void Mark(bool active, bool unavailable)
    {
        var style = TopCardSkin.Frame(true); style.ModulateColor = unavailable ? new Color(.65f, .6f, .52f) : active ? new Color(1.15f, 1.08f, .9f) : Colors.White;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) AddThemeStyleboxOverride(state, style);
        AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Gold, 2, 2));
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => _panel.CanDrop(GetGlobalTransform() * atPosition, data);
    public override void _DropData(Vector2 atPosition, Variant data) => _panel.Drop(GetGlobalTransform() * atPosition, data);
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key && _panel.Picked is not null && key.Keycode is Key.Left or Key.Right)
        { _panel.PreviewKeyboard(Destination, key.Keycode == Key.Left ? -1 : 1); AcceptEvent(); }
    }
}

public sealed partial class TopCardFace : Button
{
    private readonly TopCardPanel _panel;
    private readonly int _position;
    private readonly CardView _card;
    private readonly Control _face;
    public TopCardFace(TopCardPanel panel, int position, CardView card)
    {
        _panel = panel; _position = position; _card = card; TooltipText = card.Definition!.Name;
        MouseDefaultCursorShape = CursorShape.Drag;
        _face = CardPreview.Face(card, 1000); _face.CustomMinimumSize = Vector2.Zero; AddChild(_face);
        _face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _face.OffsetLeft = _face.OffsetTop = 6; _face.OffsetRight = _face.OffsetBottom = -6;
        Pressed += () => _panel.Pick(_position); Mark(false);
    }
    public void SetDragged(bool dragged)
    {
        // Hide only the original face and frame, preserving normal descendant
        // modulation for Godot’s separate drag presentation.
        SelfModulate = dragged ? new Color(1, 1, 1, 0) : Colors.White; _face.Visible = !dragged;
    }
    public void Mark(bool selected)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var style = SidebarSkin.Inset(); style.ModulateColor = selected ? new Color(1.6f, 1.35f, .8f) : state is "hover" or "focus" ? new Color(1.2f, 1.15f, 1) : Colors.White;
            AddThemeStyleboxOverride(state, style);
        }
    }
    public override Variant _GetDragData(Vector2 atPosition) => _panel.BeginDrag(_position, this, atPosition);
    public override bool _CanDropData(Vector2 atPosition, Variant data) => _panel.CanDrop(GetGlobalTransform() * atPosition, data);
    public override void _DropData(Vector2 atPosition, Variant data) => _panel.Drop(GetGlobalTransform() * atPosition, data);
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } || @event is InputEventKey { Pressed: true, Keycode: Key.I })
        { _panel.Inspect(_position); AcceptEvent(); }
    }
    public override Control _MakeCustomTooltip(string forText) => CardPreview.Create(_card, "Private deck view", null,
        GetTree().Root.GetVisibleRect().Size.Y / 1080f, interactionHint: "Drag to move. Click to pick up. I or right-click to inspect.");
}
