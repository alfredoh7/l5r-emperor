using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _movementCaptureStarted, _movementReviewBusy;
    private MatchEngine _movementFixture = null!;
    private string _movementSource = "", _movementTarget = "", _movementSecondTarget = "", _movementCost = "", _movementSourceItem = "", _movementTargetItem = "";
    private Button[] _movementReviewButtons = [];
    private void ProcessMovementCapture(double delta)
    { _captureClock += delta; if (_movementCaptureStarted || _captureClock < 1.5) return; _movementCaptureStarted = true; _ = CaptureMovement(); }
    private async Task PrepareMovementReview(string mode = "predator")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Battle; state.ActivePlayerId = mode == "defender" ? 1 : 0; state.PriorityPlayerId = 0; state.CurrentBattleProvince = 1;
        state.PendingChoice = null; state.UnresolvedBattlefields = mode == "none" ? [1] : mode == "gap" ? [0, 1, 3] : [0, 1, 2];
        if (mode == "gap") state.Players[1].Provinces[2].Destroyed = true;
        var hawado = mode == "hawado";
        var source = SupportFixtureCard(state, hawado ? 7103 : 6522, 0, hawado ? Zone.Home : Zone.Battlefield, hawado ? null : 1, hawado);
        var target = SupportFixtureCard(state, 3636, 1, Zone.Battlefield, 1);
        var second = SupportFixtureCard(state, 2060, 1, Zone.Battlefield, 1);
        var item = SupportFixtureCard(state, 10118, 0, Zone.Attached, hawado ? null : 1); item.AttachedTo = source.Id;
        var otherItem = SupportFixtureCard(state, 10118, 1, Zone.Attached, 1); otherItem.AttachedTo = target.Id;
        _movementSource = source.Id; _movementTarget = target.Id; _movementSecondTarget = second.Id;
        _movementSourceItem = item.Id; _movementTargetItem = otherItem.Id; _movementCost = "";
        if (hawado)
        {
            // Only those two unresolved battlefields contain enemies; the removed Dynasty card is a cost, never a mover.
            target.ProvinceIndex = 0; otherItem.ProvinceIndex = 0; second.ProvinceIndex = 2;
            _movementCost = SupportFixtureCard(state, 3636, 1, Zone.DynastyDiscard).Id;
        }
        if (mode == "partial") SupportFixtureCard(state, 10056, 1, Zone.Home);
        _movementFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_movementFixture, 0);
        var option = Actions.Single(a => a.CardId == source.Id && a.Movement is not null);
        if (option.Enabled) Choose(option); else { SelectCard(FindCard(source.Id)!); SetNotice(option.Reason!); }
        if (_captureMode.Contains("review")) BuildMovementReviewControls();
        await EventFrames(20);
        if (mode == "partial" && _captureMode.EndsWith("-review", StringComparison.Ordinal))
        { ChooseMovementProvince(option.Movement!.DefenderId, 2); await ConfirmMovement(); await NegateMovementTarget(); }
    }
    private void BuildMovementReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("MovementReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "MovementReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 5);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Predator", "predator"), ("Defender", "defender"), ("Hawado", "hawado"), ("Destroyed gap", "gap"), ("No destination", "none"), ("One stopped", "partial") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunMovementReview(() => PrepareMovementReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat())); _movementReviewButtons = buttons.ToArray();
        foreach (var button in buttons) { button.CustomMinimumSize = new(0, 44); button.AddThemeFontSizeOverride("font_size", 14); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(button); }
        Place(_root, row, 344, 114, 1000, 46); FitMovementReviewControls();
    }
    private void FitMovementReviewControls()
    { if (_root.GetNodeOrNull<Control>("MovementReviewControls") is { } row) row.Size = new(Math.Min(1120, _layoutWidth - SidebarWidth - 22 - 344 - 18), 46); }
    private async Task RunMovementReview(Func<Task> action)
    {
        if (_movementReviewBusy) return; _movementReviewBusy = true; foreach (var b in _movementReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("Restart a movement example to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _movementReviewBusy = false; foreach (var b in _movementReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private Button MovementButton(int province) => StabilizationButtons(_inspector).Single(b => b.HasMeta("movement_province") && b.GetMeta("movement_province").AsInt32() == province);
    private async Task PickMovement(int province, bool keyboard = false)
    {
        // Issue #581: no target is chosen for the player. When the action names legal targets, pick the first through the real picker, as a player would, before the destination.
        if (_chosen is { TargetIds.Length: > 0, Command.TargetId: null } && _inspector.FindChild("MovementTargetPicker", true, false) is OptionButton targetPicker)
        { targetPicker.Selected = 0; targetPicker.EmitSignal(OptionButton.SignalName.ItemSelected, 0L); await EventFrames(3); }
        var version = View.Version; var button = MovementButton(province);
        if (keyboard)
        { button.GrabFocus(); await EventFrames(3); GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }); GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(12); }
        else await SupportClick(button);
        SupportRequire(View.Version == version && _chosen?.Command?.Province == province && _paymentConfirm is { Disabled: false }, "Destination choice must only draft movement and enable confirmation");
        SupportRequire(GetViewport().GetVisibleRect().Encloses(_paymentConfirm!.GetGlobalRect()), "Movement confirmation leaves the window");
    }
    private async Task ConfirmMovement()
    { await SupportClick(_paymentConfirm!); await WaitDecisionPresentation(); for (var i = 0; i < 240 && !TableMotionsSettled(); i++) await EventFrames(1); await EventFrames(12); }
    private async Task NegateMovementTarget()
    {
        // Tamoko's engine command has no separate target: it uses the suspended action's target.
        SupportRequire(_movementFixture.State.PendingNegatableAbility?.TargetId == _movementTarget, "Negation window is not for the proposed target");
        var reaction = _movementFixture.QueryActions(1).Single(a => a.Enabled && a.Command?.AbilityId == "tamoko-negate");
        await _session.Submit(reaction.Command!); await WaitDecisionPresentation();
        for (var i = 0; i < 12 && _movementFixture.State.PendingChoice is not null; i++)
        {
            var actor = _movementFixture.State.PriorityPlayerId;
            await _session.Submit(_movementFixture.QueryActions(actor).First(a => a.Enabled && a.Type == "declineReaction").Command!); await WaitDecisionPresentation();
        }
        SupportRequire(_movementFixture.State.PendingChoice is null, "Movement reaction windows did not close");
        await EventFrames(35);
    }
    private void CheckMovementDraft(int destination, int expectedMovers)
    {
        SupportRequire(_movementPlacements.Count == expectedMovers && IntentArrows().Count(a => a.Assignment) == expectedMovers, "Not every proposed mover has its own placement and assignment arrow");
        SupportRequire(_chosen!.Command!.Assignments is null, "Movement draft used attack assignments");
        foreach (var (id, position) in _movementPlacements)
        {
            var card = FindCard(id)!; var owner = View.Players.Single(p => p.Id == card.OwnerId);
            SupportRequire(_battleCourts[destination].Encloses(new Rect2(position, new Vector2(UnitWidth(card, owner), StackHeight(card, owner)))), "Proposed stack leaves its destination court");
        }
        var clip = _tableViewport.GetGlobalRect();
        SupportRequire(IntentArrows().Where(a => a.Assignment).All(a => clip.HasPoint(a.From) && clip.HasPoint(a.To)), "Movement arrows are clipped out of view");
    }
    private async Task CaptureMovement()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false); await PrepareMovementReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal)) { GD.Print("MOVEMENT_REVIEW_READY: real Predator choice and prepared cases; nothing moves until confirmation."); return; }
            if (_captureMode.Contains("render")) { await CaptureMovementRenders(); return; }
            SupportRequire(MovementActive && _chosen!.Command!.Province is null && _paymentConfirm!.Disabled && !IntentArrows().Any(), "Movement started preselected or without confirmation gating");
            if (_captureMode.Contains("review")) SupportRequire(!_root.GetNode<Control>("MovementReviewControls").GetGlobalRect().Intersects(_inspectorPanel.GetGlobalRect()), "Review navigation overlaps the sidebar");
            await SupportCapture("-initial"); await PickMovement(0, true); CheckMovementDraft(0, 2);
            SupportRequire(_movementFixture.State.Cards[_movementSource].ProvinceIndex == 1 && _movementFixture.State.Cards[_movementTarget].ProvinceIndex == 1, "Preview changed live positions");
            await SupportCapture("-proposed");
            var defender = _chosen!.Movement!.DefenderId;
            await SupportClick(_tiles.Single(t => t.Card.OwnerId == defender && t.Card.Zone == Zone.Province && t.Card.ProvinceIndex == 2));
            SupportRequire(_chosen.Command!.Province == 2, "Clicking the destination province did not select it"); CheckMovementDraft(2, 2);
            await SupportClick(_tiles.Single(t => t.Card.Id == _movementSecondTarget));
            SupportRequire(_chosen.Command.TargetId == _movementSecondTarget && _movementPlacements.ContainsKey(_movementSecondTarget) && !_movementPlacements.ContainsKey(_movementTarget), "Retargeting left an arrow on the previous target");
            await SupportCapture("-retargeted");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Cancel movement"));
            SupportRequire(_chosen is null && _movementPlacements.Count == 0 && !IntentArrows().Any() && View.Version == 0, "Cancel changed the match or left proposed arrows");
            await PrepareMovementReview(); await PickMovement(2); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(!MovementActive && _chosen is null && !IntentArrows().Any(), "A changed seat retained a private movement draft");
            await SupportCapture("-waiting"); _session.SwitchPracticeSeat(); await EventFrames(15);
            Choose(Actions.Single(a => a.CardId == _movementSource && a.Movement is not null)); await EventFrames(10); await PickMovement(2); await ConfirmMovement();
            SupportRequire(new[] { _movementSource, _movementTarget, _movementSourceItem, _movementTargetItem }.All(id => _movementFixture.State.Cards[id].ProvinceIndex == 2), "Confirmed destination did not carry both units and attachments");
            SupportRequire(_movementPlacements.Count == 0 && !IntentArrows().Any(), "Resolved movement retained proposed ghosts"); await SupportCapture("-arrived");
            await PrepareMovementReview("hawado"); SupportRequire(_chosen!.Provinces!.SequenceEqual([0, 2]), "Hawado did not expose both engine destinations");
            await PickMovement(2); CheckMovementDraft(2, 1); SupportRequire(!_movementPlacements.ContainsKey(_movementCost), "Hawado's cost card is drawn as a mover"); await SupportCapture("-hawado"); await ConfirmMovement();
            SupportRequire(_movementFixture.State.Cards[_movementSource].ProvinceIndex == 2 && !_movementFixture.State.Cards[_movementSource].Bowed && _movementFixture.State.Cards[_movementCost].Zone == Zone.RemovedFromGame, "Hawado movement/straightening or removal cost failed");
            await PrepareMovementReview("gap"); SupportRequire(_chosen!.Provinces!.SequenceEqual([0, 3]), "UI replaced the engine's destroyed-province adjacency"); await PickMovement(3); CheckMovementDraft(3, 2); await SupportCapture("-gap");
            await PrepareMovementReview("defender"); SupportRequire(_chosen!.Movement!.DefenderId == 0, "Wrong destination owner for defending actor"); await PickMovement(0); CheckMovementDraft(0, 2); await SupportCapture("-defender");
            await PrepareMovementReview("none"); SupportRequire(!MovementActive && Actions.Any(a => a.Movement is not null && !a.Enabled && a.Reason is not null && a.Provinces is { Length: 0 }), "No-destination action is not explained/disabled"); await SupportCapture("-none");
            await PrepareMovementReview("partial"); await PickMovement(2); await ConfirmMovement(); await NegateMovementTarget();
            SupportRequire(_movementFixture.State.Cards[_movementSource].ProvinceIndex == 2 && _movementFixture.State.Cards[_movementTarget].ProvinceIndex == 1 && _movementFixture.State.Cards[_movementTargetItem].ProvinceIndex == 1, "Partial negation incorrectly moved both units");
            SupportRequire(View.Events.Count(e => e.Type == "unitMovedToBattlefield") == 1 && View.Events.Any(e => e.Type == "moveNegated") && _movementPlacements.Count == 0, "Partial result lost the authoritative movement/negation events"); await SupportCapture("-partial");
            GD.Print("MOVEMENT_PASS: native destination keyboard/table selection, proposed stack arrows, retarget/cancel/seat reset, attachments, Hawado cost distinction, destroyed-gap/defender destinations and partial negation"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    // Renderer/real-engine checks can run while the desktop is locked. They deliberately do NOT claim mouse/focus acceptance.
    private async Task CaptureMovementRenders()
    {
        foreach (var (mode, destination, count) in new[] { ("predator", 2, 2), ("hawado", 2, 1), ("gap", 3, 2), ("defender", 0, 2) })
        {
            await PrepareMovementReview(mode); ChooseMovementProvince(_chosen!.Movement!.DefenderId, destination); await EventFrames(100);
            CheckMovementDraft(destination, count); await SupportCapture("-" + mode);
            var before = _movementFixture.State;
            SupportRequire(before.Cards[_movementSource].ProvinceIndex == (mode == "hawado" ? null : 1), "Render preview changed live positions");
            await _session.Submit(_chosen.Command!); await WaitDecisionPresentation(); await EventFrames(45);
            SupportRequire(_movementFixture.State.Cards[_movementSource].ProvinceIndex == destination, "Rendered command failed to resolve");
        }
        await PrepareMovementReview("none"); await EventFrames(80); await SupportCapture("-none");
        await PrepareMovementReview("partial"); ChooseMovementProvince(_chosen!.Movement!.DefenderId, 2);
        await _session.Submit(_chosen.Command!); await WaitDecisionPresentation(); await NegateMovementTarget(); await EventFrames(90);
        SupportRequire(_movementFixture.State.Cards[_movementSource].ProvinceIndex == 2 && _movementFixture.State.Cards[_movementTarget].ProvinceIndex == 1, "Partial negation render assumed both arrived");
        SupportRequire(View.Events.Count(e => e.Type == "unitMovedToBattlefield") == 1 && View.Events.Any(e => e.Type == "moveNegated"), "Partial movement events missing");
        await SupportCapture("-partial");
        GD.Print("MOVEMENT_RENDER_PASS: renderer and direct real-engine commands only; native desktop mouse/focus acceptance remains pending."); GetTree().Quit();
    }
}
