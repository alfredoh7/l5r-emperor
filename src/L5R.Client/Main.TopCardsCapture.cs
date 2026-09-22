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
            if (province.CardId is {} id) { state.Cards[id].DefinitionId = "oracle-l5r-7253"; state.Cards[id].FaceUp = false; }
            else SupportFixtureCard(state, 7253, 1, Zone.Province, 0);
            ability = "";
        }
        else if (mode == "bottom")
        {
            SupportFixtureCard(state, 690, 0, Zone.Hand);
            var tactician = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Personality && (d.Keywords ?? []).Contains("Tactician"));
            SupportFixtureCard(state, tactician.Id, 0, Zone.Home); ability = "banish-all-doubt-look";
        }
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
        if (_root.GetNodeOrNull<Control>("LookReviewControls") is {} old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "LookReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 6);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Reorder", "reorder"), ("Take + top", "take"), ("Take + bottom", "bottom"), ("Any Fate deck", "any"), ("Show + take", "show"), ("Fixed order", "fixed"), ("Mixed choices*", "mixed") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunLookReview(() => PrepareLookReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()));
        _lookReviewButtons = buttons.ToArray();
        foreach (var b in buttons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); b.AddThemeFontSizeOverride("font_size", 15); row.AddChild(b); }
        Place(_root, row, 350, 114, 1080, 46); FitLookReviewControls();
    }
    private void FitLookReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("LookReviewControls") is {} row)
            row.Size = new(Math.Min(1160, _layoutWidth - SidebarWidth - 22 - 350 - 18), 46);
    }
    private async Task RunLookReview(Func<Task> action)
    {
        if (_lookReviewBusy) return; _lookReviewBusy = true; foreach (var b in _lookReviewButtons) b.Disabled = true;
        try { await action(); } catch (Exception e) { SetNotice("Restart a viewer scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _lookReviewBusy = false; foreach (var b in _lookReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task LookKey(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = false }); await EventFrames(4);
    }
    private async Task SetLookDestination(int position, string destination)
    {
        var panel = _lookPanel!;
        SupportRequire(!panel.ReturnStep, "Destination requested on return screen");
        var card = View.PendingLook!.Cards!.Single(c => c.Position == position);
        if (destination == View.PendingLook.Remainder)
        {
            var selected = panel.Destinations[position];
            if (selected == destination) return;
            await SupportClick(panel.Choice(position, selected));
        }
        else
        {
            SupportRequire(card.Dispositions.Any(d => d.Disposition == destination && d.Legal), "Harness requested a forbidden destination");
            panel.Choice(position, destination).GrabFocus(); await LookKey(Key.Enter);
        }
        SupportRequire(panel.Destinations[position] == destination, "Visible choice button did not select the requested destination");
    }
    private void LookFits()
    {
        var panel = _lookPanel!;
        SupportRequire(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()), $"Viewer leaves the window: {panel.GetGlobalRect()}, min {panel.GetCombinedMinimumSize()}");
        if (View.PendingLook?.Stage == "Arrange")
        {
            SupportRequire(panel.GetGlobalRect().Encloses(panel.ConfirmButton.GetGlobalRect()), "Confirmation is outside viewer");
            foreach (var position in panel.Order.Where(p => !panel.ReturnStep || panel.Destinations[p] is "top" or "bottom")) SupportRequire(panel.GetGlobalRect().Encloses(panel.Face(position).GetGlobalRect()), "Full card leaves viewer");
        }
    }
    private async Task ConfirmLook()
    {
        if (!_lookPanel!.ReturnStep)
        {
            var versionBeforeNext = View.Version;
            await SupportClick(_lookPanel.ContinueButton); await EventFrames(6); LookFits();
            SupportRequire(_lookPanel.ReturnStep && View.Version == versionBeforeNext, "Next resolved the effect instead of opening return review");
            await SupportCapture("-return");
        }
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
            if (_captureMode.EndsWith("review", StringComparison.Ordinal)) { GD.Print("TOP_CARDS_REVIEW_READY: real engine choices; prepared scenarios above the tray."); return; }
            LookFits(); SupportRequire(_lookPanel!.Order.Count == 3 && !_lookPanel.ConfirmButton.Disabled, "Reorder case missing");
            var version = View.Version;
            _lookPanel.MoveButton(0, true).GrabFocus(); await LookKey(Key.Enter);
            SupportRequire(_lookPanel.Order.SequenceEqual(new[] { 1, 0, 2 }) && View.Version == version, "Keyboard move submitted or failed to reorder");
            await SupportCapture("-reorder");
            var face = _lookPanel.Face(1); var point = face.GetGlobalRect().GetCenter();
            PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            for (var i = 0; i < 240 && !ContainsEitherPreview(GetTree().Root); i++) await EventFrames(1);
            SupportRequire(ContainsEitherPreview(GetTree().Root), "Full-card hover missing"); await SupportCapture("-hover");
            await SupportClick(face); SupportRequire(_lookInspection?.Visible == true && View.Version == version, "Inspection submitted a command or did not open");
            _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_lookPanel is null && _lookInspection is null && View.PendingLook is null, "Private view or inspection survived a seat change"); await SupportCapture("-waiting");
            _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_lookPanel!.Order.SequenceEqual(new[] { 0, 1, 2 }), "Seat change retained private draft");
            await SupportClick(_lookPanel.MoveButton(0, true)); await ConfirmLook();
            await PrepareLookReview("take"); LookFits(); SupportRequire(_lookPanel!.ConfirmButton.Disabled, "Required choice defaulted");
            var illegal = View.PendingLook!.Cards!.First(c => c.Dispositions.Any(d => d.Disposition == "hand" && !d.Legal));
            SupportRequire(_lookPanel.Choice(illegal.Position, "hand").Disabled && !string.IsNullOrWhiteSpace(_lookPanel.Choice(illegal.Position, "hand").TooltipText), "Engine ineligible reason missing");
            SupportRequire(!_lookPanel.ReturnStep && !_lookPanel.ConfirmButton.IsVisibleInTree(), "Selection screen shows final confirmation");
            await SupportCapture("-choose");
            await SetLookDestination(0, "hand"); SupportRequire(!_lookPanel.ConfirmButton.Disabled, "Required count never satisfied");
            await SetLookDestination(2, "hand"); SupportRequire(_lookPanel.ConfirmButton.Disabled, "Excess count can submit");
            await SetLookDestination(2, "top"); await SupportCapture("-take");
            await SupportClick(_lookPanel.ContinueButton); await EventFrames(6);
            SupportRequire(_lookPanel.ReturnStep && _lookPanel.Destinations[0] == "hand", "Next lost chosen card");
            await SupportClick(_lookPanel.BackButton); await EventFrames(6);
            SupportRequire(!_lookPanel.ReturnStep && _lookPanel.Destinations[0] == "hand", "Back lost the draft");
            await SupportClick(_lookPanel.Face(0)); SupportRequire(_lookPanel.Destinations[0] == "top", "Click selected card did not undo");
            await SupportClick(_lookPanel.Face(0)); await ConfirmLook();
            await PrepareLookReview("bottom"); LookFits(); SupportRequire(View.PendingLook!.Remainder == "bottom" && _lookPanel!.Order.Count == 4, "Banish case missing");
            await SetLookDestination(0, "hand"); await SupportClick(_lookPanel!.ContinueButton); await EventFrames(6); await SupportClick(_lookPanel.MoveButton(3, false)); await SupportCapture("-bottom"); await ConfirmLook();
            await PrepareLookReview("any"); LookFits(); await SupportCapture("-decks");
            var opponent = View.PendingLook!.DeckChoices!.Single(d => !d.OwnDeck);
            var deckButton = StabilizationButtons(_lookPanel!).Single(b => b.HasMeta("owner") && b.GetMeta("owner").AsInt32() == opponent.OwnerId);
            await SupportClick(deckButton); await WaitDecisionPresentation(); await EventFrames(15); LookFits();
            SupportRequire(View.PendingLook is { OwnDeck: false } && StabilizationText(_lookPanel!).Contains("your opponent’s"), "Opponent deck is ambiguous");
            SupportRequire(_lookFixture.GetView(opponent.OwnerId).PendingLook is null, "Deck owner can see private look");
            await SetLookDestination(0, "discard"); await SupportCapture("-opponent-deck"); await ConfirmLook();
            await PrepareLookReview("show"); var shown = View.PendingLook!.Cards!.First(c => c.Dispositions.Any(d => d.Disposition == "show" && d.Legal));
            await SetLookDestination(shown.Position, "show"); SupportRequire(StabilizationText(_lookPanel!).Contains("Will be shown"), "Public reveal warning missing");
            await SupportCapture("-show"); await ConfirmLook(); SupportRequire(View.ShownCards!.Any(c => c.Definition.Id == shown.DefinitionId), "Shown card did not enter public history");
            await PrepareLookReview("mixed");
            var ownFate = StabilizationButtons(_lookPanel!).Single(b => b.HasMeta("owner") && b.GetMeta("owner").AsInt32() == 0 && b.GetMeta("deck").AsString() == "Fate");
            await SupportClick(ownFate); await WaitDecisionPresentation(); await EventFrames(15); LookFits();
            await SetLookDestination(0, "bottom"); await SetLookDestination(1, "hand"); await SupportCapture("-mixed-choices");
            await SupportClick(_lookPanel!.ContinueButton); await EventFrames(6); LookFits();
            SupportRequire(_lookPanel.Face(2).GetGlobalRect().Position.X < _lookPanel.Face(0).GetGlobalRect().Position.X, "Top and bottom return groups are interleaved");
            await SupportCapture("-mixed-return"); await ConfirmLook();
            await PrepareLookReview("mixed");
            ownFate = StabilizationButtons(_lookPanel!).Single(b => b.HasMeta("owner") && b.GetMeta("owner").AsInt32() == 0 && b.GetMeta("deck").AsString() == "Fate");
            await SupportClick(ownFate); await WaitDecisionPresentation(); await EventFrames(15);
            await SetLookDestination(0, "hand"); await SetLookDestination(1, "discard"); await SetLookDestination(2, "bottom"); await ConfirmLook();
            await PrepareLookReview("fixed"); SupportRequire(!_lookPanel!.MoveButton(0, true).IsVisibleInTree(), "Fixed order has reorder controls");
            await SetLookDestination(1, "discard"); await SupportCapture("-fixed"); await ConfirmLook();
            await PrepareLookReview("none"); SupportRequire(View.PendingLook!.Rules!.Single().Max == 0 && !_lookPanel!.ConfirmButton.Disabled, "No-eligible case ignored capped engine bounds"); await ConfirmLook();
            await PrepareLookReview("short"); SupportRequire(_lookPanel!.Order.Count == 2, "Short deck invented cards"); await ConfirmLook();
            await PrepareLookReview("empty"); SupportRequire(View.PendingLook is null && _lookPanel is null, "Empty deck trapped player");
            await PrepareLookReview(); typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(8);
            SupportRequire(_lookPanel!.ConfirmButton.Disabled, "Busy session can submit"); await SupportCapture("-paused");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(8);
            SupportRequire(!_lookPanel!.ConfirmButton.Disabled, "Session did not resume");
            if (_captureMode.Contains("review"))
            { await SupportClick(_lookReviewButtons.Single(b => b.Text == "Take + bottom")); await EventFrames(90); SupportRequire(View.PendingLook?.Remainder == "bottom", "Prepared navigation failed"); }
            GD.Print("TOP_CARDS_PASS: private native hover/inspection, keyboard reorder/choice buttons and staged Back/Next, exact counts, engine return order, top/bottom, opponent deck, show, fixed/short/empty, seat and busy resets"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
