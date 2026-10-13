using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class DuelScreen
{
    private readonly Dictionary<(int, Zone), DuelFatePile> _piles = [];
    private readonly List<(Vector2 Point, string Text, float Width, Action? Open)> _tableLabels = [];
    private Button? _landing;
    // "+X instead of focusing" (Kakita Gonji's +4, a Reserve Weapon's +3): one clickable bonus per engine option, on the controller's side of the board (issue #645).
    private readonly List<Button> _bonusButtons = [];
    private Panel? _landingCue;
    private int _landingCueState = -1;
    internal bool LandingArmed { get; private set; }
    private bool _selectedDeck;
    private Dictionary<string, Zone> _previousZones = [];
    private string? _dragCard;
    private Vector2 _dragStart;
    private bool _dragging;
    private Vector2? _focusReleasePoint;
    private CardTile? _dragGhost;
    private static Vector2 FocusPoint(int side, int index) => new((side == 0 ? 332 : 1042) + (index switch { 0 => 1, 1 => 2, 2 => 0, _ => 3 }) * 106, 564);
    private static Vector2 PilePoint(int side, Zone zone) => new(side == 0 ? 155 : 1517, zone == Zone.FateDeck ? 488 : 660);
    private Vector2 PlanePoint(Vector2 design) => _table.CanvasSize / 2 + ToPlane(design);
    internal Vector2 PileClickPoint(int owner, Zone zone) => _table.ProjectCanvas(_piles[(owner, zone)].Position + _piles[(owner, zone)].TopFace.GetCenter());
    internal Vector2 FallenClickPoint(int owner) => _table.ProjectCanvas(PlanePoint(new Vector2(owner == Snapshot!.View.ViewerId ? 515 : 1195, 422)));
    internal Vector2 LandingClickPoint => _landing is null ? Vector2.Zero : _table.ProjectCanvas(_landing.Position + _landing.Size / 2);
    internal int VisibleFocusedCount => _tiles.Values.Count(t => t.Card.Zone == Zone.Focused);
    internal bool HasPrivateInspection => _reading is not null;
    internal string? SelectedInspectionId => _selectedInspection;
    internal Vector2 FocusedClickPoint(int owner) => CardClickPoint(_tiles.Values.First(t => t.Card.Zone == Zone.Focused && t.Card.OwnerId == owner).Card.Id);

    private CardTile? UprightAt(Vector2 point) => _tiles.Values.Where(t => t.HasMeta("upright"))
        .Reverse().FirstOrDefault(t => t._HasPoint(t.GetGlobalTransform().AffineInverse() * point));
    internal int UprightCount => _tiles.Values.Count(t => t.HasMeta("upright") && t.Card.Definition?.Type == CardType.Personality);
    internal int VisibleDiscardPiles => _piles.Values.Count(p => p.PileZone == Zone.FateDiscard);
    private Dictionary<int, int> _priorHandCounts = [];
    private void RenderCards(bool animate = true)
    {
        if (_plane is null) return;
        var oldTiles = _tiles.ToDictionary(p => p.Key, p => p.Value);
        var old = _tiles.ToDictionary(p => p.Key, p => (p.Value.Card, Quad: p.Value.FaceCornersOnViewport(), Point: CardClickPoint(p.Key), Height: p.Value.HasMeta("upright") ? p.Value.FaceHeight * _scale : p.Value.FaceHeight * _scale * .7f));
        var focusCounts = _tiles.Values.Where(t => t.Card.Zone == Zone.Focused).GroupBy(t => t.Card.OwnerId).ToDictionary(g => g.Key, g => g.Count());
        var deckCorners = _piles.ToDictionary(p => p.Key, p => new[] { p.Value.TopFace.Position, p.Value.TopFace.Position + Vector2.Right * p.Value.TopFace.Size.X, p.Value.TopFace.End, p.Value.TopFace.Position + Vector2.Down * p.Value.TopFace.Size.Y }.Select(v => _table.ProjectCanvas(p.Value.Position + v)).ToArray());
        var counts = _piles.ToDictionary(p => p.Key, p => p.Value.ShownCount);
        foreach (var parent in new[] { _plane, _uprights }) foreach (var c in parent.GetChildren()) { parent.RemoveChild(c); if (c is not CardTile || !oldTiles.Values.Contains(c)) c.QueueFree(); }
        _tiles.Clear(); _piles.Clear(); _tableLabels.Clear(); _landing = null; _dragGhost = null;
        if (Snapshot is null) { foreach (var tile in oldTiles.Values) tile.QueueFree(); return; }
        var view = Snapshot.View; var record = view.LastDuel;
        var priorHand = _priorHandCounts; _priorHandCounts = view.Players.ToDictionary(p => p.Id, p => p.HandCount);
        var participants = record is null ? view.Players.SelectMany(p => p.Army.Concat(p.Home)).Where(c => c.Definition?.Type == CardType.Personality && c.Definition.Id != "oracle-l5r-5095").Take(2).ToArray() : [record.Challenger, record.Challenged];
        var attachments = record?.Attachments ?? view.Players.SelectMany(p => p.Army.Concat(p.Home)).Where(c => c.Zone == Zone.Attached).ToArray();
        var discarded = view.Players.SelectMany(p => p.Discard.Concat(p.RemovedFromGame)).ToDictionary(c => c.Id);
        foreach (var player in view.Players)
        {
            var side = player.Id == view.ViewerId ? 0 : 1;
            var deck = new DuelFatePile(player, () => SelectPile(player.Id, Zone.FateDeck)) { Size = new Vector2(190, 222), Material = CardPaper.FixedTable, Selected = _selectedDeck && side == 0 };
            deck.Position = PlanePoint(PilePoint(side, Zone.FateDeck)) - new Vector2(deck.Size.X / 2, deck.Size.Y / 2 + 17);
            deck.SetMeta("flat_tooltip", deck.TooltipText); deck.TooltipText = ""; _plane.AddChild(deck); _piles[(player.Id, Zone.FateDeck)] = deck;
            if (counts.TryGetValue((player.Id, Zone.FateDeck), out var before)) deck.SettleFrom(before, ReducedMotion || !animate);
            var participant = participants.FirstOrDefault(c => c.OwnerId == player.Id);
            if (participant is not null)
            {
                var x = side == 0 ? 515 : 1195; var foot = _resultArranged ? 520 : 502;
                var attached = attachments.Where(c => c.AttachedTo == participant.Id).ToArray();
                if (!_aftermathPlayed || !discarded.ContainsKey(participant.Id))
                {
                    // Depth staircase: complete original header and artwork strips remain exposed.
                    var step = Math.Min(78, 145f / Math.Max(1, attached.Length));
                    for (var i = attached.Length - 1; i >= 0; i--) AddUpright(attached[i], new Vector2(x - 12 * (i + 1), foot - step * (i + 1)), 340);
                    AddUpright(participant, new Vector2(x, foot), 350);
                }
                else
                {
                    for (var i = attached.Length - 1; i >= 0; i--) AddFlat(attached[i] with { Bowed = false }, new Vector2(x - 10 * (i + 1), 388 - Math.Min(54, 100f / Math.Max(1, attached.Length)) * (i + 1)), 320);
                    AddFlat(discarded[participant.Id] with { Bowed = false }, new Vector2(x, 422), 330, () => ShowPile(player.Id, discarded[participant.Id].Zone));
                }
            }
            CardView[] known; int count;
            if (_resultArranged) { known = record?.Reveals.LastOrDefault()?.Cards.Where(c => c.OwnerId == player.Id).ToArray() ?? []; count = known.Length; }
            else if (_shownReveal is not null) { known = _shownReveal.Cards.Where(c => c.OwnerId == player.Id).ToArray(); count = known.Length; }
            else
            {
                known = player.Focused ?? []; count = player.FocusCount;
                if (PresentationBusy && record?.Reveals.LastOrDefault() is { } pending) { count = pending.Cards.Count(c => c.OwnerId == player.Id); known = []; }
            }
            for (var i = 0; i < count; i++)
            {
                var card = i < known.Length ? known[i] : new CardView($"anonymous-{player.Id}-{i}", null, player.Id, Zone.Focused, false, false, null, null, 0);
                AddFlat(card, _resultArranged ? ResultFocusPoint(side, i, count) : FocusPoint(side, i % 4) + Vector2.Down * (i / 4) * 82, _resultArranged ? (count == 1 ? 265 : count == 2 ? 190 : 120) : 138);
            }
        }
        var hand = view.LastDuel?.Result is not null || FocusChoicePending ? [] : view.Players[view.ViewerId].Hand; var spacing = Math.Min(180, 870f / Math.Max(1, hand.Length));
        for (var i = 0; i < hand.Length; i++) AddFlat(hand[i], new Vector2(835 + (i - (hand.Length - 1) / 2f) * spacing, 778), 225);
        foreach (var (id, tile) in oldTiles) if (!_tiles.TryGetValue(id, out var current) || current != tile) tile.QueueFree();
        _focusReleasePoint = null; _animateEntry = false; RenderLanding();
        // Exact corner interpolation preserves perspective and avoids ghost cards/fade-to-flat pops.
        if (animate && !ReducedMotion && _resultArranged)
            foreach (var (id, tile) in _tiles)
                if (old.TryGetValue(id, out var previous) && tile.Card.Definition is { } definition)
                {
                    var target = tile.FaceCornersOnViewport();
                    if (previous.Quad.Zip(target, (a, b) => a.DistanceTo(b)).Max() > 1)
                        MoveFace(tile, previous.Quad, target, CardTile.Artwork(definition), false, .95);
                }
        void MoveFace(CardTile tile, Vector2[] from, Vector2[] to, Texture2D? face, bool reveal, double duration)
        {
            if (face is null) return;
            var inverse = _scene.GetGlobalTransform().AffineInverse();
            tile.Hide(); tile.MouseFilter = MouseFilterEnum.Ignore;
            _flights.AddChild(new DuelCardTransition
            {
                From = from.Select(p => inverse * p).ToArray(),
                To = to.Select(p => inverse * p).ToArray(),
                Face = face,
                Reveal = reveal,
                Duration = duration,
                ViewScale = _scale,
                Paused = () => InputLocked || _reading is not null,
                Landed = () => { if (GodotObject.IsInstanceValid(tile) && tile.IsInsideTree()) { tile.Show(); tile.MouseFilter = MouseFilterEnum.Stop; } }
            });
        }
        void Configure(CardTile tile)
        {
            tile.Show(); tile.Modulate = Colors.White; tile.MouseFilter = MouseFilterEnum.Stop;
            tile.ReducedMotion = ReducedMotion; tile.ShowTokenRail = false; tile.Material = CardPaper.Reading;
            tile.ContextHelp = InspectionContext(tile.Card); tile.SetMeta("flat_tooltip", true); tile.TooltipText = "";
            tile.Highlighted = tile.Card.Id == _selectedHand || tile.Card.Id == _selectedFocus; _tiles[tile.Card.Id] = tile;
        }
        void AddUpright(CardView card, Vector2 foot, float height)
        {
            var shown = card with { Bowed = false };
            var tile = oldTiles.TryGetValue(card.Id, out var prior) && prior is DuelCardTile existing && existing.Card == shown && existing.Upright
                ? existing : new DuelCardTile(shown, Select) { Upright = true, Grounded = card.Definition?.Type == CardType.Personality };
            tile.Size = Vector2.One * (height + 12); tile.FaceHeight = height;
            Configure(tile); tile.SetMeta("upright", true); tile.ReducedMotion = true; tile.Position = foot - new Vector2(tile.Size.X / 2, height + 6);
            // CardTile supplies viewport coordinates already for native upright controls.
            tile.ProjectToScreen = point => point; _uprights.AddChild(tile);
        }
        void AddFlat(CardView card, Vector2 design, float height, Action? click = null)
        {
            Action<CardView> select = click is null ? Select : _ => click();
            CardTile tile = oldTiles.TryGetValue(card.Id, out var prior) && prior is DuelCardTile existing && existing.Card == card && !existing.Upright
                ? existing : new DuelCardTile(card, select) { FaceDown = card.Definition is null || card.Zone == Zone.Focused && !card.FaceUp };
            tile.Size = Vector2.One * (height + 12); tile.FaceHeight = height; var target = PlanePoint(design) - tile.Size / 2;
            Configure(tile); tile.ProjectToScreen = _table.ProjectCanvas; tile.Position = target; _plane.AddChild(tile);
            if (CinematicBusy && _cinema == Cinema.Opening && card.Zone == Zone.Hand) tile.Modulate = new Color(1, 1, 1, 0);
            if (animate && !ReducedMotion && card.Zone == Zone.Focused)
            {
                if (_revealClock > 0 && card.FaceUp && card.Definition is { } revealed && (!old.TryGetValue(card.Id, out var revealPrior) || !revealPrior.Card.FaceUp))
                {
                    var corners = tile.FaceCornersOnViewport();
                    MoveFace(tile, corners, corners, CardTile.Artwork(revealed), true, .58);
                }
                else if (!CinematicBusy && (!old.TryGetValue(card.Id, out var previous) || previous.Card.Zone != Zone.Focused) && old.Count > 0)
                {
                    // A face-down card that appears while the public hand count did not fall came from the deck (deck sizes are hidden); hidden hands remain backs.
                    var key = (card.OwnerId, Zone.FateDeck);
                    var player = view.Players.First(p => p.Id == card.OwnerId);
                    var fromDeck = !card.FaceUp && priorHand.TryGetValue(card.OwnerId, out var priorHandCount) && priorHandCount <= player.HandCount;
                    var destination = tile.FaceCornersOnViewport();
                    Vector2[] origin;
                    Texture2D? face = null;
                    if (old.TryGetValue(card.Id, out var source) && source.Card.Zone == Zone.Hand)
                    { origin = _focusReleasePoint is { } release ? destination.Select(p => p + release - destination.Aggregate(Vector2.Zero, (a, b) => a + b) / 4).ToArray() : source.Quad; face = source.Card.Definition is { } definition ? CardTile.Artwork(definition) : null; }
                    else if (fromDeck && deckCorners.TryGetValue(key, out var deck)) origin = deck;
                    else origin = destination.Select(p => p + Vector2.Up * 95 * _scale).ToArray();
                    // Do not replay existing anonymous commitments on a fresh seat view.
                    if (card.Definition is null && focusCounts.GetValueOrDefault(card.OwnerId) >= player.FocusCount) return;
                    var inverse = _scene.GetGlobalTransform().AffineInverse();
                    tile.Hide(); tile.MouseFilter = MouseFilterEnum.Ignore;
                    _focusClock = DuelFocusFlight.Duration + .10;
                    _flights.AddChild(new DuelFocusFlight
                    {
                        From = origin.Select(p => inverse * p).ToArray(),
                        To = destination.Select(p => inverse * p).ToArray(),
                        SourceFace = face,
                        ViewScale = _scale,
                        Paused = () => InputLocked || _reading is not null,
                        Landed = () => { if (GodotObject.IsInstanceValid(tile) && tile.IsInsideTree()) { tile.Show(); tile.MouseFilter = MouseFilterEnum.Stop; } }
                    });
                }
            }
        }
    }

    private void RenderTableLabels()
    {
        foreach (var (point, text, width, open) in _tableLabels)
        {
            Control label;
            if (open is not null) label = DuelUi.Button(text, () => { if (!InputLocked && !PresentationBusy) open(); }, scale: _scale, fontSize: 16);
            else
            {
                var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore }; panel.AddThemeStyleboxOverride("panel", DuelUi.Plaque(_scale));
                var words = DuelUi.Text(text, Math.Max(12, (int)(18 * _scale))); words.HorizontalAlignment = HorizontalAlignment.Center; panel.AddChild(words); label = panel;
            }
            label.Position = _offset + (point - new Vector2(width / 2, 16)) * _scale; label.Size = new Vector2(width, 32) * _scale; _hud.AddChild(label);
        }
    }
    private GameCommand? SelectedFocusCommand() => Legal.FirstOrDefault(a => a.Type == "focus" && (_selectedDeck ? a.Command!.Choice == "deck" : a.Command!.CardId == _selectedHand && a.Command.Choice is null && _selectedHand is not null))?.Command;
    private void RenderLanding()
    {
        if (_landing is not null && GodotObject.IsInstanceValid(_landing)) { _plane.RemoveChild(_landing); _landing.QueueFree(); }
        _landing = null; _landingCue = null; _landingCueState = -1; LandingArmed = false;
        RenderBonusButtons();
        if (Snapshot is null || InputLocked || PresentationBusy || SelectedFocusCommand() is not { } command) return;
        var count = Snapshot.View.Players[Snapshot.View.ViewerId].FocusCount;
        _landing = new Button { Name = _selectedDeck ? "Focus from deck" : "Focus selected card", Text = "PLACE\nFOCUS", TooltipText = _selectedDeck ? "Commit the top Fate card without looking. Enter also commits." : "Commit the selected hand card. Enter also commits.", Size = new Vector2(120, 166), ZIndex = 3, MouseDefaultCursorShape = CursorShape.PointingHand };
        _landing.Position = PlanePoint(FocusPoint(0, count % 4) + Vector2.Down * (count / 4) * 76) - _landing.Size / 2;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) _landing.AddThemeStyleboxOverride(state, DuelUi.FocusFrame(state == "hover"));
        _landing.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Paper, 1, 0));
        _landing.AddThemeFontOverride("font", DuelUi.Heading);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) _landing.AddThemeColorOverride(state, Palette.Paper);
        _landing.AddThemeFontSizeOverride("font_size", 44);
        _landing.Disabled = _submitted || _commandCooldown > 0; _landing.Pressed += () => Submit(command); _plane.AddChild(_landing);
        _landingCue = new Panel { Position = new Vector2(-6, -6), Size = _landing.Size + new Vector2(12, 12), ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore }; _landing.AddChild(_landingCue);
        UpdateLandingCue(_lastHoverPointer);
    }
    private void RenderBonusButtons()
    {
        foreach (var old in _bonusButtons) if (GodotObject.IsInstanceValid(old)) { _plane.RemoveChild(old); old.QueueFree(); }
        _bonusButtons.Clear();
        if (Snapshot is null || InputLocked || PresentationBusy) return;
        var index = 0;
        foreach (var option in Legal.Where(a => a.Type == "focus" && (a.Command!.Choice == "p2-gonji" || a.Command.Choice == "reserve")))
        {
            var command = option.Command!;
            var amount = new string(option.Label.SkipWhile(c => c != '+').Skip(1).TakeWhile(char.IsDigit).ToArray());
            var button = new Button { Name = "Bonus " + command.Choice + " " + command.CardId, Text = "+" + (amount.Length == 0 ? "?" : amount), CustomMinimumSize = new Vector2(92, 76), Size = new Vector2(92, 76), AccessibilityName = option.Label };
            button.Position = PlanePoint(new Vector2(FocusPoint(0, 0).X - 76 + index++ * 100, 690)) - button.Size / 2;
            button.TooltipText = option.Label + "\n\nYou may apply this bonus to your duel stat instead of focusing a card.";
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) button.AddThemeStyleboxOverride(state, DuelUi.FocusFrame(state == "hover"));
            button.AddThemeStyleboxOverride("focus", Palette.Box(Colors.Transparent, Palette.Paper, 1, 0));
            button.AddThemeFontOverride("font", DuelUi.Heading);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, Palette.Paper);
            button.AddThemeFontSizeOverride("font_size", 40);
            button.Disabled = _submitted || _commandCooldown > 0; button.Pressed += () => Submit(command);
            _plane.AddChild(button); _bonusButtons.Add(button);
        }
    }
    private void UpdateLandingCue(Vector2 pointer)
    {
        if (_landing is null || _landingCue is null) return;
        // Explicit inverse projection works for native hover and a captured card drag alike.
        LandingArmed = !InputLocked && !PresentationBusy && _reading is null && !_landing.Disabled && _landing.GetRect().HasPoint(_table.ScreenToCanvas(pointer));
        var hot = LandingArmed;
        if (_dragGhost is not null) _dragGhost.Modulate = new Color(1, 1, 1, hot ? .2f : .82f);
        var stateKey = (hot ? 1 : 0) + (_dragging ? 2 : 0);
        if (_landingCueState == stateKey) return;
        _landingCueState = stateKey;
        var edge = hot ? new Color(1, .91f, .66f) : new Color(.75f, .52f, .22f, .85f);
        var cue = new StyleBoxFlat
        {
            BgColor = hot ? new Color(.28f, .07f, .035f, .94f) : new Color(.08f, .035f, .025f, .85f),
            BorderColor = edge,
            BorderWidthLeft = hot ? 3 : 2,
            BorderWidthRight = hot ? 3 : 2,
            BorderWidthTop = hot ? 3 : 2,
            BorderWidthBottom = hot ? 3 : 2,
            ShadowColor = new Color(.94f, .42f, .10f, hot ? .6f : .22f),
            ShadowSize = hot ? 16 : 7
        };
        _landingCue.AddThemeStyleboxOverride("panel", cue);
        foreach (var state in new[] { "normal", "hover", "pressed" }) _landing.AddThemeStyleboxOverride(state, DuelUi.FocusFrame(hot));
        _landing.Text = hot && _dragging ? "RELEASE\nTO\nFOCUS" : "PLACE\nFOCUS";
        _landing.AddThemeFontSizeOverride("font_size", hot && _dragging ? 30 : 44);
    }
    private void SelectPile(int owner, Zone zone)
    {
        if (InputLocked || PresentationBusy || _commandCooldown > 0) return;
        if (zone == Zone.FateDeck)
        {
            if (owner != Snapshot!.View.ViewerId) return;
            if (!Legal.Any(a => a.Type == "focus" && a.Command!.Choice == "deck")) return;
            _coachingDismissed.Add(Snapshot!.View.ViewerId); _dontShowAgain = false;
            _selectedDeck = !_selectedDeck; _selectedHand = null; _selectedInspection = null;
            RefreshSelection(); RenderLanding(); RenderActions();
        }
        else ShowPile(owner, zone);
    }
    private void ShowPile(int owner, Zone zone)
    {
        var player = Snapshot!.View.Players[owner]; var cards = (zone == Zone.RemovedFromGame ? player.RemovedFromGame : player.Discard.Where(c => c.Zone == zone)).ToArray();
        var name = zone switch { Zone.FateDiscard => "Fate discard", Zone.Dead => "Dead personalities", Zone.DynastyDiscard => "Dynasty discard", _ => "Removed from game" };
        ShowCards($"{player.Name} · {name}", cards, "Public cards · click a face to inspect");
    }
    public void OpenPublicPiles()
    {
        if (InputLocked || PresentationBusy || Snapshot is null) return;
        var box = Panel("Public piles", 520);
        foreach (var player in Snapshot.View.Players)
            foreach (var zone in new[] { Zone.FateDiscard, Zone.Dead, Zone.DynastyDiscard, Zone.RemovedFromGame })
            {
                var owner = player.Id; var z = zone; var seat = owner == Snapshot.View.ViewerId ? "Your" : "Opponent";
                var label = zone switch { Zone.FateDiscard => "Fate discard", Zone.Dead => "fallen personalities", Zone.DynastyDiscard => "Dynasty discard", _ => "removed cards" };
                Button($"{seat} {label}", () => ShowPile(owner, z), box);
            }
        Button("Back", ClearReading, box);
    }
    private void ShowOtherPiles(int owner)
    {
        var box = Panel($"{Snapshot!.View.Players[owner].Name} · other piles", 370);
        foreach (var zone in new[] { Zone.DynastyDiscard, Zone.Dead, Zone.RemovedFromGame })
        { var z = zone; Button(zone switch { Zone.DynastyDiscard => "Dynasty discard", Zone.Dead => "Dead personalities", _ => "Removed from game" }, () => ShowPile(owner, z), box); }
        Button("Back", ClearReading, box);
    }
    private void ShowCards(string title, CardView[] cards, string hint)
    {
        var box = Panel(title, 620); box.AddChild(DuelUi.Wrapped(cards.Length == 0 ? "This pile is empty." : hint, 16, Palette.PaperDim));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 410), SizeFlagsVertical = SizeFlags.ExpandFill }; box.AddChild(scroll);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(grid);
        foreach (var card in cards.Where(c => c.Definition is not null))
        {
            var tile = new CardTile(card with { Bowed = false }, true, Inspect, _ => { }) { FaceHeight = 180, CustomMinimumSize = new Vector2(195, 195), ShowTokenRail = false, ReducedMotion = true, Material = CardPaper.Reading }; grid.AddChild(tile);
        }
        Button("Back", ClearReading, box);
    }
    private bool HandlePlacement(InputEventMouse mouse)
    {
        if (PresentationBusy) return false;
        if (mouse is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            _dragStart = mouse.Position; _dragging = false;
            var point = _table.ScreenToCanvas(mouse.Position);
            _dragCard = _tiles.Values.LastOrDefault(t => t.Card.Zone == Zone.Hand && t.Card.OwnerId == Snapshot!.View.ViewerId && t._HasPoint(point - t.Position))?.Card.Id;
        }
        else if (mouse is InputEventMouseMotion && _dragCard is { } id && mouse.Position.DistanceTo(_dragStart) > 10)
        {
            _dragging = true; _selectedHand = id; _selectedDeck = false;
            if (_landing is null) RenderLanding();
            if (_dragGhost is null)
            {
                var source = _tiles[id];
                _dragGhost = new CardTile(source.Card, true, _ => { }, _ => { }) { Size = source.Size, FaceHeight = source.FaceHeight, ShowTokenRail = false, ReducedMotion = true, Material = CardPaper.FixedTable, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .82f) };
                _plane.AddChild(_dragGhost);
            }
            _dragGhost.Position = _table.ScreenToCanvas(mouse.Position) - _dragGhost.Size / 2;
            UpdateLandingCue(mouse.Position);
        }
        else if (mouse is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            var dragging = _dragging; _dragging = false; _dragCard = null;
            if (_dragGhost is not null) { _plane.RemoveChild(_dragGhost); _dragGhost.QueueFree(); _dragGhost = null; }
            if (dragging && _landing is not null && _landing.GetRect().HasPoint(_table.ScreenToCanvas(mouse.Position)) && SelectedFocusCommand() is { } command)
            { _focusReleasePoint = mouse.Position; Submit(command); _focusReleasePoint = null; GetViewport().SetInputAsHandled(); return true; }
            UpdateLandingCue(mouse.Position);
        }
        return false;
    }
}
