using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _searchCaptureStarted, _searchReviewBusy;
    private MatchEngine _searchFixture = null!;
    private Button[] _searchReviewButtons = [];
    private void ProcessSearchCapture(double delta)
    {
        _captureClock += delta; if (_searchCaptureStarted || _captureClock < 1.5) return;
        _searchCaptureStarted = true; _ = CaptureSearch();
    }
    private async Task PrepareSearchReview(string mode = "shizuka")
    {
        if (mode == "p20") // P20 (SearchExtensions.cs): the real engine's isolated review position, a private multi-card search of the viewer's own provinces
        {
            _searchFixture = MatchEngine.CreateSearchShowcase("p20-provinces");
            await LoadSupportFixture(_searchFixture, 0); await WaitDecisionPresentation(); await EventFrames(15);
            return;
        }
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = mode == "gift" ? Phase.Dynasty : Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var owner = mode == "gift" ? 1 : 0;
        SupportFixtureCard(state, 10292, owner, Zone.Home);
        if (mode is not ("none" or "empty"))
        {
            foreach (var id in new[] { "oracle-l5r-9865", "oracle-l5r-9865", "oracle-l5r-4777" })
            { var c = SupportFixtureCard(state, id, owner, Zone.DynastyDeck); state.Players[owner].DynastyDeck.Add(c.Id); }
            var terrain = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Strategy && (d.Keywords ?? []).Contains("Terrain") && CardTile.Artwork(d) is not null);
            for (var i = 0; i < 2; i++) { var c = SupportFixtureCard(state, terrain.Id, owner, Zone.FateDeck); state.Players[owner].FateDeck.Add(c.Id); }
        }
        if (mode == "gift")
        {
            var province = state.Players[owner].Provinces[0];
            if (province.CardId is { } old) { state.Cards[old].DefinitionId = "oracle-l5r-3694"; state.Cards[old].FaceUp = false; }
            else SupportFixtureCard(state, 3694, owner, Zone.Province, 0);
        }
        if (mode == "cost")
        { var c = SupportFixtureCard(state, "training-costed-region", owner, Zone.DynastyDeck); state.Players[owner].DynastyDeck.Add(c.Id); }
        if (mode == "optional") SupportFixtureCard(state, "training-search-source", owner, Zone.Home);
        if (mode == "empty")
        { foreach (var id in state.Players[owner].DynastyDeck) state.Cards.Remove(id); state.Players[owner].DynastyDeck.Clear(); }
        _searchFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_searchFixture, owner);
        var start = mode == "gift" ? _searchFixture.QueryActions(0).First(a => a.Enabled && a.Type == "endDynasty") :
            Actions.First(a => a.Enabled && a.Command?.AbilityId == (mode == "optional" ? "training-search-optional" : "shizuka-search"));
        await _session.Submit(start.Command!); await WaitDecisionPresentation();
        if (_captureMode.Contains("review")) BuildSearchReviewControls();
        await EventFrames(15);
    }
    private void BuildSearchReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("SearchReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "SearchReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 6);
        var shizuka = SidebarSkin.Button("Restart Shizuka", () => _ = RunSearchReview(() => PrepareSearchReview()));
        var gift = SidebarSkin.Button("Imperial Gift", () => _ = RunSearchReview(() => PrepareSearchReview("gift")));
        var none = SidebarSkin.Button("No eligible cards", () => _ = RunSearchReview(() => PrepareSearchReview("none")));
        var seat = SidebarSkin.Button("View other seat", () => { _session.SwitchPracticeSeat(); });
        _searchReviewButtons = [shizuka, gift, none, seat];
        foreach (var b in _searchReviewButtons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); row.AddChild(b); }
        Place(_root, row, 350, 114, 920, 46);
    }
    private async Task RunSearchReview(Func<Task> action)
    {
        if (_searchReviewBusy) return; _searchReviewBusy = true; foreach (var b in _searchReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("The demo has changed. Restart a scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _searchReviewBusy = false; foreach (var b in _searchReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task PickSearchDeck(string deck)
    {
        var version = View.Version;
        SupportRequire(!_searchPanel!.ConfirmButton.IsVisibleInTree(), "Deck choice must not show a second confirmation");
        await SupportClick(StabilizationButtons(_searchPanel!).Single(b => b.HasMeta("deck") && b.GetMeta("deck").AsString() == deck));
        await WaitDecisionPresentation(); await EventFrames(12);
        SupportRequire(View.Version == version + 1 && View.PendingSearch is { Stage: SearchStage.Select } search && search.Deck == deck,
            "One deck click must open the chosen private gallery with exactly one command");
        SupportRequire(_searchPanel!.ConfirmButton.IsVisibleInTree() && _searchPanel.ConfirmButton.Disabled,
            "Card choices must retain explicit confirmation and start without a selection");
    }
    private async Task SearchFilter(string text)
    {
        await SupportClick(_searchPanel!.Filter);
        SupportRequire(_searchPanel.Filter.HasFocus(), $"Filter click did not focus input: hovered={GetViewport().GuiGetHoveredControl()?.GetPath()}, focus={GetViewport().GuiGetFocusOwner()?.GetPath()}, bounds={_searchPanel.Filter.GetGlobalRect()}");
        _searchPanel.Filter.SelectAll();
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = false });
        foreach (var c in text)
        {
            GetViewport().PushInput(new InputEventKey { Unicode = c, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Unicode = c, Pressed = false });
        }
        await EventFrames(6);
    }
    private async Task PickSearchEntry(string? definition = null)
    {
        var option = _searchPanel!.Cards.First(c => c.Entry.Eligible && (definition is null || c.Entry.DefinitionId == definition));
        var version = View.Version; await SupportClick(option.Button);
        SupportRequire(View.Version == version && _searchPanel.Selection?.Command?.Choice == option.Entry.DefinitionId, "Select must not submit before confirm");
        SupportRequire(GetViewport().GetVisibleRect().Encloses(_searchPanel.GetGlobalRect()) && _searchPanel.GetGlobalRect().Encloses(_searchPanel.ConfirmButton.GetGlobalRect()), "Search panel or confirmation leaves the window");
        await SupportCapture("-selected-" + View.PendingSearch!.Deck + "-" + (View.PendingSearch.SourceName == "Imperial Gift" ? "gift" : option.Entry.Definition.ScenarioOnly ? "cost" : "shizuka"));
        await SupportClick(_searchPanel.ConfirmButton); await WaitDecisionPresentation(); await EventFrames(12);
    }
    private async Task FinishSearchFollowup()
    {
        var action = Actions.First(a => a.Enabled && a.Type is "chooseSearchProvince" or "chooseSearchDiscard");
        await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text == action.Label));
        await SupportCapture("-followup-" + View.PendingSearch!.Stage + (QuoteFor(action) is null ? "" : "-payment"));
        await SupportClick(StabilizationButtons(_decisionActions).First(b => b.Text.StartsWith("Confirm", StringComparison.Ordinal))); await WaitDecisionPresentation(); await EventFrames(12);
    }
    // P20: a search of the viewer's own provinces that takes one or two cards with different titles. Nothing is chosen for the player, the confirmation stays explicit, the draft honours the stated limits, and one
    // command is sent for the whole set.
    private async Task CaptureProvinceSearch()
    {
        await PrepareSearchReview("p20");
        SupportRequire(_searchPanel is not null && View.PendingSearch is { Stage: SearchStage.Select, MinSelect: 1, MaxSelect: 2, DifferentTitles: true, Place: "provinces" }, "The province search must open its private list with a one-to-two count and the titles rule");
        var panel = _searchPanel!; var entries = View.PendingSearch!.Entries!;
        SupportRequire(panel.Cards.Count == entries.Length && entries.All(e => e.Eligible) && entries.Length >= 3, "Every card of the viewer's own provinces must be listed and selectable");
        SupportRequire(panel.ConfirmButton.Disabled && panel.Selection is null, "A multi-card search must start without a selection");
        await SupportCapture("-provinces");
        var version = View.Version;
        var first = panel.Cards[0]; var second = panel.Cards.First(c => c.Entry.Definition.Name != first.Entry.Definition.Name);
        await SupportClick(first.Button);
        SupportRequire(View.Version == version && !panel.ConfirmButton.Disabled && panel.Selection?.Command?.CardIds is [var onlyOne] && onlyOne == first.Entry.DefinitionId, "One card satisfies the minimum and must not submit before confirm");
        await SupportClick(second.Button);
        SupportRequire(View.Version == version && panel.Selection?.Command?.CardIds is [var a, var b] && a == first.Entry.DefinitionId && b == second.Entry.DefinitionId, "The second pick must join the draft in the order picked");
        var sameTitle = panel.Cards.Skip(2).FirstOrDefault(c => c.Entry.Definition.Name == first.Entry.Definition.Name || c.Entry.Definition.Name == second.Entry.Definition.Name);
        if (sameTitle.Button is not null) { await SupportClick(sameTitle.Button); SupportRequire(panel.Selection?.Command?.CardIds?.Length <= 2, "A third pick must never exceed the maximum"); }
        SupportRequire(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()) && panel.GetGlobalRect().Encloses(panel.ConfirmButton.GetGlobalRect()), "The search panel or its confirmation leaves the window");
        await SupportCapture("-provinces-selected");
        _session.SwitchPracticeSeat(); await EventFrames(15);
        SupportRequire(_searchPanel is null && View.PendingSearch is null, "The private province list must not survive a seat switch");
        _session.SwitchPracticeSeat(); await EventFrames(15);
        SupportRequire(_searchPanel is not null && _searchPanel.Selection is null, "The searcher must regain a fresh private list with no stale draft");
        await SupportClick(_searchPanel!.Cards[0].Button); await SupportClick(_searchPanel.ConfirmButton);
        await WaitDecisionPresentation(); await EventFrames(12);
        SupportRequire(View.PendingSearch is null && View.ShownCards!.Length == 1 && !View.Events.Any(e => e.Type == "deckShuffled"), "The confirmed province card must be shown, and no deck is shuffled");
    }

    private async Task CaptureSearch()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareSearchReview();
            if (_captureMode.Contains("review")) { GD.Print("DECK_SEARCH_REVIEW_READY: choose Dynasty or Fate; other prepared scenarios are above the panel."); return; }
            SupportRequire(_searchPanel is not null && View.PendingSearch?.Entries is null && !_searchPanel.ConfirmButton.IsVisibleInTree(), "Deck step must not disclose candidates or show a redundant confirmation");
            await SupportCapture("-decks"); await PickSearchDeck("Dynasty");
            await SupportCapture("-gallery");
            SupportRequire(_searchPanel!.Cards.Count == View.PendingSearch!.Entries!.Length && !StabilizationButtons(_searchPanel).Any(b => b.Text.StartsWith("Pass", StringComparison.Ordinal)), "Mandatory search lost candidates or offered Pass");
            SupportRequire(_searchPanel.Cards.Single(c => c.Entry.DefinitionId == "oracle-l5r-9865").Entry.Count == 2, "Duplicate count missing");
            var scrollPoint = _searchPanel.GalleryScroll.GetGlobalRect().GetCenter(); var zoom = _cameraZoom;
            PushCaptureMouse(new InputEventMouseMotion { Position = scrollPoint, GlobalPosition = scrollPoint });
            PushCaptureMouse(new InputEventMouseButton { Position = scrollPoint, GlobalPosition = scrollPoint, ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 4 }); await EventFrames(8);
            PushCaptureMouse(new InputEventMouseButton { Position = scrollPoint, GlobalPosition = scrollPoint, ButtonIndex = MouseButton.WheelDown, Pressed = false });
            SupportRequire(_searchPanel.GalleryScroll.ScrollVertical > 0 && _cameraZoom == zoom, "Gallery wheel scrolled the table or did not scroll the cards");
            var illegal = _searchPanel.Cards.First(c => !c.Entry.Eligible); await SupportClick(illegal.Button);
            SupportRequire(_searchPanel.Selection is null && _searchPanel.ConfirmButton.Disabled && StabilizationText(_searchPanel.Preview).Contains(illegal.Entry.Reason!), "Ineligible card selected or reason missing");
            await SupportCapture("-ineligible");
            await SearchFilter("Fallow"); SupportRequire(_searchPanel.Cards.Count == 1, $"Name filter failed: text={_searchPanel.Filter.Text}, cards={_searchPanel.Cards.Count}, focus={GetViewport().GuiGetFocusOwner()?.GetPath()}");
            await SupportCapture("-filter"); await SearchFilter("");
            var eligible = _searchPanel.Cards.First(c => c.Entry.Eligible); eligible.Button.GrabFocus(); await EventFrames(6);
            SupportRequire(StabilizationText(_searchPanel.Preview).Contains(eligible.Entry.Definition.Name), "Keyboard inspection failed");
            await SupportClick(eligible.Button);
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(8);
            SupportRequire(_searchPanel!.ConfirmButton.Disabled && _searchPanel.Selection is null, "Busy session retained a submittable stale selection"); await SupportCapture("-paused");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(8);
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(_searchPanel is null && View.PendingSearch is null, "Private gallery survived opponent seat switch"); await SupportCapture("-opponent");
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(_searchPanel is not null && _searchPanel.Selection is null, "Searcher did not regain a fresh private view");
            await SearchFilter("Fallow"); await PickSearchEntry("oracle-l5r-9865");
            SupportRequire(_searchPanel is null && View.PendingSearch?.Stage == SearchStage.ChooseProvince && View.ShownCards!.Length == 1, "Region reveal/follow-up missing");
            await FinishSearchFollowup(); SupportRequire(View.PendingSearch is null && View.Events.Any(e => e.Type == "deckShuffled"), "Region search failed to finish/shuffle");
            await PrepareSearchReview(); await PickSearchDeck("Fate");
            await PickSearchEntry(); SupportRequire(View.PendingSearch?.Stage == SearchStage.ChooseDiscard, "Terrain discard follow-up missing");
            var found = Self.Hand.Single(c => c.Definition?.Id == View.PendingSearch!.Selected!.Id);
            SupportRequire(!Actions.Any(a => a.Type == "chooseSearchDiscard" && a.Command?.TargetId == found.Id), "Found Terrain incorrectly offered for discard");
            await FinishSearchFollowup(); SupportRequire(View.PendingSearch is null, "Terrain search failed to finish");
            await PrepareSearchReview("none"); await PickSearchDeck("Dynasty");
            SupportRequire(View.PendingSearch!.CanPass && _searchPanel!.Cards.All(c => !c.Entry.Eligible), "No-match fixture did not expose Pass");
            await SupportClick(StabilizationButtons(_searchPanel!).Single(b => b.Text.StartsWith("Pass", StringComparison.Ordinal))); await SupportCapture("-pass");
            await SupportClick(_searchPanel!.ConfirmButton); await WaitDecisionPresentation(); await EventFrames(12);
            SupportRequire(View.PendingSearch is null && View.ShownCards!.Length == 0 && View.Events.Any(e => e.Type == "deckShuffled"), "Pass failed to shuffle without reveal");
            await PrepareSearchReview("gift"); SupportRequire(View.PendingSearch?.SourceName == "Imperial Gift" && View.PendingSearch.Stage == SearchStage.Select, "Gift should open its list directly");
            await SupportCapture("-gift"); await PickSearchEntry(); SupportRequire(View.PendingSearch is null && View.ShownCards!.Length == 1, "Gift did not complete and show its Item");
            await PrepareSearchReview("cost"); await PickSearchDeck("Dynasty"); await SearchFilter("costed");
            await PickSearchEntry("training-costed-region"); await FinishSearchFollowup(); SupportRequire(View.PendingSearch is null && View.Events.Any(e => e.Type == "payment"), "Quoted Region payment did not finish");
            await PrepareSearchReview("empty"); await PickSearchDeck("Dynasty");
            SupportRequire(_searchPanel!.Cards.Count == 0 && View.PendingSearch!.CanPass, "Empty deck did not offer Pass"); await SupportCapture("-empty");
            await SupportClick(StabilizationButtons(_searchPanel).Single(b => b.Text.StartsWith("Pass", StringComparison.Ordinal))); await SupportClick(_searchPanel!.ConfirmButton); await WaitDecisionPresentation();
            SupportRequire(View.PendingSearch is null, "Empty deck could not finish");
            await PrepareSearchReview("optional");
            SupportRequire(View.PendingSearch!.Optional && View.PendingSearch.CanPass && _searchPanel!.Cards.Any(c => c.Entry.Eligible), "Optional fixture missing legal cards or Pass");
            await SupportClick(StabilizationButtons(_searchPanel!).Single(b => b.Text.StartsWith("Pass", StringComparison.Ordinal))); await SupportCapture("-optional");
            await SupportClick(_searchPanel!.ConfirmButton); await WaitDecisionPresentation(); SupportRequire(View.PendingSearch is null && View.ShownCards!.Length == 0, "Optional pass revealed a card or failed to finish");
            await CaptureProvinceSearch();
            GD.Print("DECK_SEARCH_PASS: single-click deck navigation, explicit card confirmation, full-card inspection, grouping, filter, ineligible reasons, both seats, Region, Terrain discard, no-match Pass, Imperial Gift, quoted payment and a multi-card province search"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
