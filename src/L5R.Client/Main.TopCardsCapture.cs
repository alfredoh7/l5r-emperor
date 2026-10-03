using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _lookCaptureStarted, _lookReviewBusy;
    private MatchEngine _lookFixture = null!;
    private Button[] _lookReviewButtons = [];
    private void ProcessLookCapture(double delta)
    {
        _captureClock += delta; if (_lookCaptureStarted || _captureClock < 1.5) return;
        _lookCaptureStarted = true; _ = CaptureLook();
    }
    private async Task PrepareLookReview(string mode = "reorder")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = mode == "any" ? Phase.Dynasty : Phase.Action;
        state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var seeds = state.Players[0].Hand.Select(id => state.Cards[id].DefinitionId).Take(4).ToList();
        var item = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Item && CardTile.Artwork(d) is not null).Id;
        var political = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Strategy && (d.Keywords ?? []).Contains("Political") && CardTile.Artwork(d) is not null).Id;
        if (mode == "take") seeds = [item, seeds[1], item, seeds[3]];
        if (mode == "show") seeds = [seeds[0], political, seeds[2], seeds[3]];
        foreach (var player in state.Players)
        {
            foreach (var id in player.FateDeck) state.Cards.Remove(id); player.FateDeck.Clear();
            foreach (var definition in seeds.Take(mode == "short" ? 2 : mode == "empty" ? 0 : 4))
            { var card = SupportFixtureCard(state, definition, player.Id, Zone.FateDeck); player.FateDeck.Add(card.Id); }
        }
        string ability;
        if (mode == "any")
        {
            var province = state.Players[1].Provinces[0];
            if (province.CardId is { } id) { state.Cards[id].DefinitionId = "oracle-l5r-7253"; state.Cards[id].FaceUp = false; }
            else SupportFixtureCard(state, 7253, 1, Zone.Province, 0);
            ability = "";
        }
        else if (mode == "bottom")
        {
            SupportFixtureCard(state, 690, 0, Zone.Hand);
            var tactician = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Personality && (d.Keywords ?? []).Contains("Tactician"));
            SupportFixtureCard(state, tactician.Id, 0, Zone.Home); ability = "banish-all-doubt-look";
        }
        else if (mode == "top-bottom") { SupportFixtureCard(state, 3918, 0, Zone.Home); ability = "isawa-tamaki-look"; }
        else if (mode == "show") { SupportFixtureCard(state, 10479, 0, Zone.Home); ability = "kahoku-exp2-look"; }
        else if (mode is "take" or "none" or "fixed" or "mixed")
        { SupportFixtureCard(state, "training-look-source", 0, Zone.Home); ability = mode == "fixed" ? "training-look-fixed" : mode == "mixed" ? "training-look-any" : "training-look-take-item"; }
        else { SupportFixtureCard(state, 6095, 0, Zone.Home); ability = "public-records-look"; }
        _lookFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_lookFixture, mode == "any" ? 1 : 0);
        var option = _lookFixture.QueryActions(0).First(a => a.Enabled && (mode == "any" ? a.Type == "endDynasty" : a.Command?.AbilityId == ability));
        await _session.Submit(option.Command!); await WaitDecisionPresentation(); await EventFrames(15);
        if (_captureMode.Contains("review")) BuildLookReviewControls();
    }
    private void BuildLookReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("LookReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "LookReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 6);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Reorder", "reorder"), ("Take + top", "take"), ("Take + bottom", "bottom"), ("Top or bottom", "top-bottom"), ("Any Fate deck", "any"), ("Show + take", "show"), ("Fixed order", "fixed"), ("Mixed choices*", "mixed") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunLookReview(() => PrepareLookReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()));
        _lookReviewButtons = buttons.ToArray();
        foreach (var b in buttons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); b.AddThemeFontSizeOverride("font_size", 15); row.AddChild(b); }
        Place(_root, row, 350, 114, 1080, 46); FitLookReviewControls();
    }
    private void FitLookReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("LookReviewControls") is { } row)
            row.Size = new(Math.Min(1160, _layoutWidth - SidebarWidth - 22 - 350 - 18), 46);
    }
    private async Task RunLookReview(Func<Task> action)
    {
        if (_lookReviewBusy) return; _lookReviewBusy = true; foreach (var b in _lookReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("Restart a viewer scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _lookReviewBusy = false; foreach (var b in _lookReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task LookKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = false }); await EventFrames(4);
    }
    private async Task SetLookDestination(int position, string destination)
    {
        var panel = _lookPanel!; var version = View.Version;
        panel.Face(position).GrabFocus(); await LookKey(Key.Enter);
        panel.Tray(destination).GrabFocus(); await LookKey(Key.Enter); await EventFrames(15);
        SupportRequire(panel.Destinations[position] == destination && panel.Picked is null && View.Version == version,
            "Keyboard placement failed or submitted the draft");
    }
    private void LookMouse(InputEventMouse mouse)
    {
        GetWindow().GrabFocus(); GetViewport().WarpMouse(mouse.Position);
        PushCaptureMouse(mouse, buffered: true); Input.FlushBufferedEvents();
    }
    private async Task BeginLookDrag(int position)
    {
        var point = _lookPanel!.Face(position).GetGlobalRect().GetCenter();
        LookMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(2);
        LookMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        await EventFrames(2);
        var moved = point + new Vector2(26, 12);
        LookMouse(new InputEventMouseMotion { Position = moved, GlobalPosition = moved, Relative = moved - point, ButtonMask = MouseButtonMask.Left });
        await EventFrames(4);
        SupportRequire(GetViewport().GuiIsDragging(), "Native mouse gesture did not start card drag");
    }
    private async Task DragLook(int position, string destination, int index = 0, bool legal = true, string? capture = null, bool cancelOutside = false)
    {
        var panel = _lookPanel!; var order = panel.Order.ToArray(); var before = new Dictionary<int, string>(panel.Destinations); var version = View.Version;
        await BeginLookDrag(position);
        var point = cancelOutside ? panel.GetGlobalRect().Position + new Vector2(-12, 20) : panel.DropPoint(destination, index, position);
        LookMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left }); await EventFrames(16);
        SupportRequire(panel.ConfirmButton.Disabled, "Can confirm while holding a card");
        if (legal && !cancelOutside) SupportRequire(panel.LandingRect is not null, "Legal destination has no insertion preview");
        if (capture is not null)
        {
            var path = Path.ChangeExtension(_capturePath, null) + capture + ".png";
            SupportRequire(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "Drag capture failed");
        }
        LookMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(20);
        SupportRequire(!GetViewport().GuiIsDragging() && View.Version == version && panel.Picked is null, "Drop submitted or left a held card");
        if (legal && !cancelOutside) SupportRequire(panel.Destinations[position] == destination, "Native drop did not move the card");
        else SupportRequire(panel.Order.SequenceEqual(order) && before.All(pair => panel.Destinations[pair.Key] == pair.Value), "Rejected or outside drop altered draft");
        LookFits();
    }
    private void LookFits()
    {
        var panel = _lookPanel!;
        SupportRequire(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()), $"Viewer leaves the window: {panel.GetGlobalRect()}, min {panel.GetCombinedMinimumSize()}");
        if (View.PendingLook?.Stage != "Arrange") return;
        SupportRequire(panel.GetGlobalRect().Encloses(panel.ConfirmButton.GetGlobalRect()), "Confirmation is outside viewer");
        var trays = panel.TrayDestinations.Select(d => panel.Tray(d).GetGlobalRect()).ToArray();
        foreach (var caption in panel.Face(panel.Order[0]).GetParent().GetChildren().OfType<Label>())
            SupportRequire(trays.Any(t => t.Encloses(caption.GetGlobalRect())), "Deck-order caption leaves its destination tray");
        var faces = panel.Order.Select(p => panel.Face(p).GetGlobalRect()).ToArray();
        foreach (var rect in faces) SupportRequire(panel.GetGlobalRect().Encloses(rect), "Full card leaves viewer");
        for (var i = 0; i < faces.Length; i++) for (var j = i + 1; j < faces.Length; j++)
            SupportRequire(!faces[i].Intersects(faces[j]), "Card faces overlap after arrangement");
    }
    private async Task ConfirmLook()
    {
        LookFits();
        var view = View.PendingLook!; var owner = view.DeckOwnerId!.Value; var deck = _lookFixture.State.Players[owner].FateDeck.ToArray();
        var count = view.Cards!.Length; var order = _lookPanel!.Order.ToArray(); var destinations = new Dictionary<int, string>(_lookPanel.Destinations);
        var expected = order.Where(p => destinations[p] == "top").Select(p => deck[p]).Concat(deck.Skip(count))
            .Concat(order.Where(p => destinations[p] == "bottom").Select(p => deck[p])).ToArray();
        var version = View.Version; await SupportClick(_lookPanel.ConfirmButton); await WaitDecisionPresentation(); await EventFrames(15);
        SupportRequire(View.Version == version + 1 && View.PendingLook is null && _lookPanel is null, "Arrangement did not resolve exactly once");
        SupportRequire(_lookFixture.State.Players[owner].FateDeck.SequenceEqual(expected), "Engine result does not match displayed return order");
        foreach (var p in order.Where(p => destinations[p] is "hand" or "show"))
            SupportRequire(_lookFixture.State.Players[view.OwnDeck ? owner : View.ViewerId].Hand.Contains(deck[p]), "Chosen card did not enter hand");
        foreach (var p in order.Where(p => destinations[p] == "discard"))
            SupportRequire(_lookFixture.State.Cards[deck[p]].Zone == Zone.FateDiscard, "Chosen card was not discarded");
    }
    private async Task CaptureLook()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareLookReview(_captureMode.EndsWith("review", StringComparison.Ordinal) ? "bottom" : "reorder");
            if (_captureMode.EndsWith("review", StringComparison.Ordinal)) { GD.Print("TOP_CARDS_REVIEW_READY: draggable destinations and explicit deck order; real engine choices."); return; }
            GetWindow().GrabFocus(); await EventFrames(20);
            LookFits(); SupportRequire(_lookPanel!.Order.Count == 3 && !_lookPanel.ConfirmButton.Disabled, "Reorder case missing");
            var version = View.Version;
            _lookPanel.Face(0).GrabFocus(); await LookKey(Key.Enter); await LookKey(Key.Right); await LookKey(Key.Enter); await EventFrames(15);
            SupportRequire(_lookPanel.Order.SequenceEqual(new[] { 1, 0, 2 }) && View.Version == version, "Keyboard insertion failed or submitted");
            await SupportClick(_lookPanel.UndoButton);
            await DragLook(2, "top", capture: "-drag-reorder");
            SupportRequire(_lookPanel.Order.SequenceEqual(new[] { 2, 0, 1 }) && _lookPanel.PositionLabel(2).Contains("Drawn first"), $"Drag reorder or first-drawn label is wrong: {string.Join(",", _lookPanel.Order)}; label={_lookPanel.PositionLabel(2)}; drop={GetViewport().GuiIsDragSuccessful()}");
            await SupportCapture("-reorder");
            _lookPanel.Face(1).GrabFocus(); await LookKey(Key.I);
            SupportRequire(_lookInspection?.Visible == true && View.Version == version, "Full inspection failed or submitted");
            _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_lookPanel is null && _lookInspection is null && View.PendingLook is null, "Private view survived seat change");
            await SupportCapture("-waiting"); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_lookPanel!.Order.SequenceEqual(new[] { 0, 1, 2 }), "Seat change retained private draft");
            await BeginLookDrag(0); await LookKey(Key.Escape);
            SupportRequire(!GetViewport().GuiIsDragging() && _lookPanel.Order.SequenceEqual(new[] { 0, 1, 2 }), "Escape did not cancel drag");
            await BeginLookDrag(0); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(!GetViewport().GuiIsDragging() && _lookPanel is null, "Private drag preview survived seat change");
            _session.SwitchPracticeSeat(); await EventFrames(15); await ConfirmLook();
            await PrepareLookReview("take"); LookFits(); SupportRequire(_lookPanel!.ConfirmButton.Disabled, "Required choice defaulted");
            var illegal = View.PendingLook!.Cards!.First(c => c.Dispositions.Any(d => d.Disposition == "hand" && !d.Legal));
            SupportRequire(!string.IsNullOrWhiteSpace(_lookPanel.DropReason(illegal.Position, "hand")), "Ineligible engine reason missing");
            await DragLook(illegal.Position, "hand", legal: false);
            await DragLook(0, "hand", capture: "-drag-hand"); SupportRequire(!_lookPanel.ConfirmButton.Disabled, "Required count never satisfied");
            // A full single-slot tray accepts the drop as a swap with its sole occupant (720eeb91): card 2 takes the hand slot, card 0 returns to card 2's old spot.
            await DragLook(2, "hand");
            SupportRequire(_lookPanel.Destinations[2] == "hand" && _lookPanel.Destinations[0] == "top", "Drop onto the full hand tray did not swap with its occupant");
            await SupportCapture("-take");
            // Undo reverts one action at a time: the swap first (card 0 returns to the hand slot), then card 0's own placement.
            await SupportClick(_lookPanel.UndoButton); SupportRequire(_lookPanel.Destinations[0] == "hand" && _lookPanel.Destinations[2] == "top", "Undo did not revert the swap");
            await SupportClick(_lookPanel.UndoButton); SupportRequire(_lookPanel.ConfirmButton.Disabled && _lookPanel.Destinations[0] == "top", "Undo did not restore required choice");
            await SupportClick(_lookPanel.Face(0)); await SupportClick(_lookPanel.Tray("hand")); await EventFrames(15);
            SupportRequire(_lookPanel.Destinations[0] == "hand", "Click-to-place alternative failed"); await ConfirmLook();
            await PrepareLookReview("bottom"); LookFits();
            await DragLook(0, "hand"); await DragLook(3, "bottom", 0, capture: "-drag-bottom");
            SupportRequire(_lookPanel!.PositionLabel(3).Contains("Immediately") && _lookPanel.PositionLabel(2).Contains("Very bottom"), "Bottom order labels reversed");
            await DragLook(1, "hand", cancelOutside: true); await SupportCapture("-bottom"); await ConfirmLook();
            await PrepareLookReview("top-bottom"); LookFits();
            await DragLook(0, "bottom"); await SupportCapture("-top-bottom"); await ConfirmLook();
            await PrepareLookReview("any"); LookFits(); await SupportCapture("-decks");
            var opponent = View.PendingLook!.DeckChoices!.Single(d => !d.OwnDeck);
            var deckButton = StabilizationButtons(_lookPanel!).Single(b => b.HasMeta("owner") && b.GetMeta("owner").AsInt32() == opponent.OwnerId);
            await SupportClick(deckButton); await WaitDecisionPresentation(); await EventFrames(15); LookFits();
            SupportRequire(View.PendingLook is { OwnDeck: false } && StabilizationText(_lookPanel!).Contains("your opponent’s"), "Opponent deck is ambiguous");
            SupportRequire(_lookFixture.GetView(opponent.OwnerId).PendingLook is null && !_lookPanel!.TrayDestinations.Any(d => d is "hand" or "show"), "Opponent privacy or destinations incorrect");
            await DragLook(0, "discard"); await SupportCapture("-opponent-deck"); await ConfirmLook();
            await PrepareLookReview("show"); var shown = View.PendingLook!.Cards!.First(c => c.Dispositions.Any(d => d.Disposition == "show" && d.Legal));
            await SetLookDestination(shown.Position, "show"); SupportRequire(StabilizationText(_lookPanel!).Contains("Will be shown"), "Public reveal warning missing");
            await SupportCapture("-show"); await ConfirmLook(); SupportRequire(View.ShownCards!.Any(c => c.Definition.Id == shown.DefinitionId), "Shown card did not enter public history");
            await PrepareLookReview("mixed");
            var ownFate = StabilizationButtons(_lookPanel!).Single(b => b.HasMeta("owner") && b.GetMeta("owner").AsInt32() == 0 && b.GetMeta("deck").AsString() == "Fate");
            await SupportClick(ownFate); await WaitDecisionPresentation(); await EventFrames(15);
            await SetLookDestination(0, "hand"); await SetLookDestination(1, "discard"); await SetLookDestination(2, "bottom"); LookFits();
            await SupportCapture("-mixed"); await ConfirmLook();
            await PrepareLookReview("fixed"); var initialOrder = _lookPanel!.Order.ToArray();
            await DragLook(0, "top", 2); SupportRequire(_lookPanel.Order.SequenceEqual(initialOrder), "Fixed-order drag reordered cards");
            await SetLookDestination(1, "discard"); await SupportCapture("-fixed"); await ConfirmLook();
            await PrepareLookReview("none"); SupportRequire(View.PendingLook!.Rules!.Single().Max == 0 && !_lookPanel!.ConfirmButton.Disabled && !_lookPanel.TrayDestinations.Contains("hand"), "Capped zero destination offered"); await ConfirmLook();
            await PrepareLookReview("short"); SupportRequire(_lookPanel!.Order.Count == 2, "Short deck invented cards"); await ConfirmLook();
            await PrepareLookReview("empty"); SupportRequire(View.PendingLook is null && _lookPanel is null, "Empty deck trapped player");
            await PrepareLookReview(); await BeginLookDrag(0);
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(8);
            SupportRequire(!GetViewport().GuiIsDragging() && _lookPanel!.ConfirmButton.Disabled, "Busy state retained drag or submission"); await SupportCapture("-paused");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(8);
            SupportRequire(!_lookPanel!.ConfirmButton.Disabled, "Session did not resume");
            GD.Print("TOP_CARDS_PASS: native drag and insertion previews; keyboard equivalent; rejected/full/outside drops; undo; exact confirmed engine order; top/bottom/hand/show/discard; fixed/short/empty; privacy, Escape, seat and busy drag cancellation"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
