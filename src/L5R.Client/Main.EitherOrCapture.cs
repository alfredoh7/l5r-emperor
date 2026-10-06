using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _eitherCaptureStarted, _eitherReviewBusy;
    private MatchEngine _eitherFixture = null!;
    private Button[] _eitherReviewButtons = [];
    private void ProcessEitherCapture(double delta)
    {
        _captureClock += delta; if (_eitherCaptureStarted || _captureClock < 1.5) return;
        _eitherCaptureStarted = true; _ = CaptureEitherOr();
    }
    private async Task PrepareEitherReview(string mode = "sakuma")
    {
        // Same published cards/choices as the engine showcase, on the familiar full-scan Lion/Crane table.
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        SupportFixtureCard(state, 9624, 0, Zone.Home); SupportFixtureCard(state, 7151, 0, Zone.Home);
        SupportFixtureCard(state, 9941, 0, Zone.Hand); SupportFixtureCard(state, 9485, 1, Zone.Home);
        if (mode is not ("sakuma" or "hesitation")) SupportFixtureCard(state, "training-either-or-source", 1, Zone.Home);
        var actor = mode == "hesitation" ? 0 : 1;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = actor; state.PendingChoice = null;
        if (mode is "disabled" or "automatic")
        {
            foreach (var card in state.Cards.Values.Where(c => c.OwnerId == 0 && (CardCatalog.Get(c.DefinitionId).GoldProduction > 0 || c.DefinitionId == "oracle-l5r-9624"))) card.Bowed = true;
            if (mode == "automatic") state.Players[0].Honor = 0;
        }
        _eitherFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_eitherFixture, mode is "sakuma" or "hesitation" ? 1 : 0);
        if (mode == "sakuma")
        {
            var target = state.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "oracle-l5r-7151").Id;
            await TakeEitherFixture(1, "yogo-adi-choice", target);
            await TakeEitherFixture(0, "sakuma-political-negate");
        }
        else if (mode == "hesitation")
            await TakeEitherFixture(0, "unfortunate-hesitation", state.Cards.Values.First(c => c.DefinitionId == "oracle-l5r-9485").Id);
        else await TakeEitherFixture(1, "training-either-or-three");
        if (_captureMode.Contains("review")) BuildEitherReviewControls();
        await EventFrames(15);
    }
    private async Task TakeEitherFixture(int player, string ability, string? target = null)
    {
        var action = _eitherFixture.QueryActions(player).First(a => a.Enabled && a.Command?.AbilityId == ability && (target is null || a.TargetIds?.Contains(target) == true));
        await _session.Submit(action.Command! with { TargetId = target }); await WaitDecisionPresentation();
    }
    private void BuildEitherReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("EitherReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "EitherReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 6);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Sakuma", "sakuma"), ("Hesitation", "hesitation"), ("Three options", "three"), ("Unavailable", "disabled"), ("Automatic", "automatic") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunEitherReview(() => PrepareEitherReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()));
        _eitherReviewButtons = buttons.ToArray();
        foreach (var b in buttons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); b.AddThemeFontSizeOverride("font_size", 16); row.AddChild(b); }
        Place(_root, row, 350, 114, 920, 46); FitEitherReviewControls();
    }
    private void FitEitherReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("EitherReviewControls") is { } row)
            row.Size = new(Math.Min(1080, _layoutWidth - SidebarWidth - 22 - 350 - 18), 46);
    }
    private async Task RunEitherReview(Func<Task> action)
    {
        if (_eitherReviewBusy) return; _eitherReviewBusy = true; foreach (var b in _eitherReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("The demo has changed. Restart a scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _eitherReviewBusy = false; foreach (var b in _eitherReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private EitherOrOptionButton EitherButton(string key) => _inspector.GetChildren().OfType<EitherOrOptionButton>().Single(b => b.Option.Key == key);
    private async Task PickEither(string key, bool keyboard = false)
    {
        var version = View.Version; var button = EitherButton(key);
        if (keyboard)
        {
            button.GrabFocus(); await EventFrames(5);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(10);
        }
        else await SupportClick(button);
        SupportRequire(View.Version == version && _chosen?.Command?.Choice == key, "Selecting an option must not submit it");
        SupportRequire(_paymentConfirm is { Disabled: false } && GetViewport().GetVisibleRect().Encloses(_paymentConfirm.GetGlobalRect()), "Confirmation unavailable or outside the window");
    }
    private async Task ConfirmEither()
    { await SupportClick(_paymentConfirm!); await WaitDecisionPresentation(); await EventFrames(12); }
    // The engine's CURRENT province strength, as the player sees it (ProvinceView.Strength), not the raw base field: strength given for the turn is a modifier on top of it.
    private int EitherProvinceStrength() => View.Players.Single(p => p.Id == 1).Provinces.Single(p => p.Index == 0).Strength;
    private async Task FinishEitherReaction()
    {
        // Issue #644: a player who holds no Reaction is not asked to pass, so how many passes the windows need depends on who holds one; pass for whoever is asked until they close.
        for (var i = 0; i < 4 && _eitherFixture.State.PendingChoice is not null; i++)
        {
            var pass = _eitherFixture.QueryActions(_eitherFixture.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "declineReaction");
            await _session.Submit(pass.Command!); await WaitDecisionPresentation();
        }
        SupportRequire(View.PendingChoice is null, "Suspended reaction did not resume and finish");
    }
    private async Task CaptureEitherOr()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareEitherReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal)) { GD.Print("EITHER_OR_REVIEW_READY: prepared Sakuma choice; use the review controls for all five cases and either seat."); return; }
            if (_captureMode.Contains("review"))
            {
                var row = _root.GetNode<Control>("EitherReviewControls");
                SupportRequire(!row.GetGlobalRect().Intersects(_inspectorPanel.GetGlobalRect()), "Demo navigation overlaps the decision panel");
                await SupportClick(_eitherReviewButtons.Last()); SupportRequire(!View.PendingEitherOr!.ViewerDecides, "Demo seat button did not switch to waiting player");
                await SupportClick(_eitherReviewButtons.Last()); SupportRequire(View.PendingEitherOr!.ViewerDecides, "Demo seat button did not return to decider");
            }
            SupportRequire(View.PendingEitherOr?.ChoiceId == EitherOrSpecs.SakumaPayOrNegate && _paymentConfirm!.Disabled && _chosen is null, "Sakuma missing or preselected");
            var prompt = View.PendingEitherOr!;
            foreach (var o in prompt.Options) SupportRequire(StabilizationText(_inspector).Contains(o.Detail), "An option lost its consequence");
            await SupportCapture("-sakuma");
            var source = StabilizationButtons(_inspector).OfType<DecisionSourceFace>().Single();
            var point = source.GetGlobalRect().GetCenter(); PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(80);
            await EventFrames(3); GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(_capturePath, null) + "-hover.png"); HideTableTooltip();
            SupportRequire(ContainsEitherPreview(GetTree().Root), "Source hover failed to show large card; hovered=" + GetViewport().GuiGetHoveredControl()?.GetPath());
            await PickEither("pay", true); await SupportCapture("-payment");
            _sources.Clear(); RenderInspector(); await EventFrames(6); SupportRequire(_paymentConfirm!.Disabled, "Short payment can be confirmed");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Back to options")); await PickEither("pay");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(6);
            SupportRequire(_chosen is null && _paymentConfirm!.Disabled, "Busy session kept a submittable stale choice");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(6);
            await PickEither("pay"); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_chosen is null && View.PendingEitherOr!.Options.All(o => o.Gold is null) && _paymentConfirm is null, "Waiting player received selection or payment");
            SupportRequire(_inspector.GetChildren().OfType<EitherOrOptionButton>().Count() == 2 && _inspector.GetChildren().OfType<EitherOrOptionButton>().All(b => b.Disabled), "Waiting options must remain visible and read-only");
            foreach (var o in prompt.Options) SupportRequire(StabilizationText(_inspector).Contains(o.Detail), "Waiting player lost public consequence");
            await SupportCapture("-waiting"); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_chosen is null && _paymentConfirm!.Disabled, "Seat switch restored a stale selection");
            var strength = EitherProvinceStrength();
            await PickEither("pay"); await ConfirmEither();
            SupportRequire(View.PendingEitherOr is null && View.PendingChoice?.Type == "beforeActionReaction" && View.Events.Any(e => e.Type == "payment"), "Payment did not restore reaction window");
            await FinishEitherReaction(); SupportRequire(EitherProvinceStrength() == strength + 2, "Paid action did not resolve");
            await PrepareEitherReview(); strength = EitherProvinceStrength(); await PickEither("negate"); await SupportCapture("-consequence"); await ConfirmEither(); await FinishEitherReaction();
            SupportRequire(EitherProvinceStrength() == strength, "Negated action still applied its effect");
            await PrepareEitherReview("three"); SupportRequire(_inspector.GetChildren().OfType<EitherOrOptionButton>().Count() == 3, "Three-option decision was truncated"); await SupportCapture("-three");
            var honor = Self.Honor; await PickEither("honor", true); await ConfirmEither(); SupportRequire(Self.Honor == honor - 2, "Honor option failed");
            await PrepareEitherReview("disabled"); var disabled = EitherButton("pay");
            SupportRequire(disabled.Disabled && StabilizationText(disabled).Contains(disabled.Option.Reason!), "Disabled option lost reason");
            var version = View.Version; await SupportClick(disabled); SupportRequire(View.Version == version && _chosen is null, "Disabled option submitted or selected");
            await SupportCapture("-disabled"); await PickEither("accept"); await ConfirmEither(); SupportRequire(View.PendingEitherOr is null, "Accept consequence did not finish");
            await PrepareEitherReview("hesitation"); await SupportCapture("-hesitation"); var affected = View.PendingEitherOr!.AffectedCardId!;
            await PickEither("bow"); await ConfirmEither(); SupportRequire(_eitherFixture.State.Cards[affected].Bowed, "Bow option did not bow affected card");
            await PrepareEitherReview("hesitation"); await PickEither("pass"); await ConfirmEither();
            SupportRequire(_eitherFixture.QueryActions(1).Any(a => a.Enabled && a.Type == "pass"), "Forced-pass branch did not resume");
            await PrepareEitherReview("automatic");
            SupportRequire(View.PendingEitherOr is null && _eitherNotice is not null && StabilizationText(_sidebarHeader).Contains("RESOLVED AUTOMATICALLY"), "Automatic resolution explanation was lost behind later events");
            await SupportCapture("-automatic"); _session.SwitchPracticeSeat(); await EventFrames(12);
            SupportRequire(StabilizationText(_sidebarHeader).Contains(_eitherNotice!), "Other seat lost automatic resolution"); await SupportCapture("-automatic-other");
            GD.Print("EITHER_OR_PASS: native Gold/negation, reaction resume, three choices, disabled reasons, waiting privacy, keyboard, bow/pass, auto-resolution, selection reset and source hover"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static bool ContainsEitherPreview(Node node) => node.HasMeta("card_preview") || node.GetChildren(true).Any(ContainsEitherPreview);
}
