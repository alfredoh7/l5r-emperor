using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _opponentPaymentCaptureStarted, _opponentPaymentReviewBusy;
    private Button[] _opponentPaymentReviewButtons = [];
    private MatchEngine _opponentPaymentFixture = null!;
    private void ProcessOpponentPaymentCapture(double delta)
    {
        _captureClock += delta;
        if (_opponentPaymentCaptureStarted || _captureClock < 1.5) return;
        _opponentPaymentCaptureStarted = true; _ = CaptureOpponentPayment();
    }
    private async Task PrepareOpponentPaymentReview(string mode = "standard")
    {
        // Published full scans on the familiar table; the real engine opens and resolves the choice.
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        SupportFixtureCard(state, 10181, 1, Zone.Hand);
        foreach (var player in state.Players)
            foreach (var definition in new[] { 2867, 4664, 4840 }) SupportFixtureCard(state, definition, player.Id, Zone.Home);
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        if (mode == "automatic")
            foreach (var card in state.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        _opponentPaymentFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_opponentPaymentFixture, 0);
        await _session.Submit(_opponentPaymentFixture.QueryActions(0).Single(a => a.Enabled && a.Type == "pass").Command!);
        await WaitDecisionPresentation();
        var action = _opponentPaymentFixture.QueryActions(1).Single(a => a.Enabled && a.Command?.AbilityId == "virtuous-charity-open");
        var amount = mode == "zero" ? 0 : 3;
        await _session.Submit(action.Command! with { GoldAmount = amount, SourceIds = action.Gold!.PaymentsByAmount![amount][0].SourceIds });
        await WaitDecisionPresentation(); await EventFrames(12);
        if (_captureMode.Contains("review")) BuildOpponentPaymentReviewControls();
    }
    private void BuildOpponentPaymentReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("OpponentPaymentReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "OpponentPaymentReviewControls", ZIndex = 91 };
        row.AddThemeConstantOverride("separation", 6);
        var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Opponent paid 3", "standard"), ("Opponent paid 0", "zero"), ("No Gold available", "automatic") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunOpponentPaymentReview(() => PrepareOpponentPaymentReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()));
        _opponentPaymentReviewButtons = buttons.ToArray();
        foreach (var button in buttons)
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.CustomMinimumSize = new(0, 44);
            button.AddThemeFontSizeOverride("font_size", 16); row.AddChild(button);
        }
        Place(_root, row, 350, 114, 920, 46); FitOpponentPaymentReviewControls();
    }
    private void FitOpponentPaymentReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("OpponentPaymentReviewControls") is { } row)
            row.Size = new(Math.Min(1080, _layoutWidth - SidebarWidth - 22 - 350 - 18), 46);
    }
    private async Task RunOpponentPaymentReview(Func<Task> action)
    {
        if (_opponentPaymentReviewBusy) return;
        _opponentPaymentReviewBusy = true; foreach (var button in _opponentPaymentReviewButtons) button.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("Restart a payment scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _opponentPaymentReviewBusy = false; foreach (var button in _opponentPaymentReviewButtons) if (IsInstanceValid(button)) button.Disabled = false; }
    }
    private Button OpponentPaymentButton(string name) => _inspector.FindChild(name, true, false) as Button ?? throw new Exception("Missing payment button: " + name);
    private async Task SetOpponentPaymentAmount(int amount)
    {
        while (_chosen!.Command!.GoldAmount != amount)
            await SupportClick(OpponentPaymentButton(_chosen.Command.GoldAmount < amount ? "OpponentPaymentMore" : "OpponentPaymentLess"));
    }
    private async Task ClickPaymentTable(CardTile tile)
    {
        // Native table picking intentionally requires OS window focus. A background macOS launch can
        // lose activation to the runner/Codex; wait for the actual game window rather than bypassing it.
        if (!GetWindow().HasFocus()) GD.Print("OPPONENT_PAYMENT_FOCUS_REQUIRED: activate this game window to continue native table input checks.");
        for (var i = 0; i < 18000 && !GetWindow().HasFocus(); i++) await EventFrames(1);
        SupportRequire(GetWindow().HasFocus(), "Native game never received OS focus");
        await SupportClick(tile);
    }
    private async Task CaptureOpponentPayment()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareOpponentPaymentReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal))
            { GD.Print("OPPONENT_PAYMENT_REVIEW_READY: real Virtuous Charity choice; use prepared cases and Other seat."); return; }
            SupportRequire(OpponentPaymentActive && _chosen?.Command?.GoldAmount == 0 && _sources.Count == 0 && _paymentConfirm is { Disabled: false }, "Decision must open at engine default zero without paying");
            var version = View.Version;
            SupportRequire(View.PendingOpponentPayment!.FirstAmount == 3, "First payment missing");
            SupportRequire(StabilizationText(_inspector).Contains("Your opponent gains 3 Honor."), "Opponent-gain outcome missing");
            await SupportCapture("-zero");
            var zeroSource = Self.Home.First(c => Production(c, Self) > 0 && !c.Bowed);
            await ClickPaymentTable(_tiles.Single(t => t.Card.Id == zeroSource.Id));
            SupportRequire(_sources.Count == 0 && View.Version == version, "Zero payment silently selected sources");
            var source = _inspector.FindChildren("*", "Button", true, false).OfType<DecisionSourceFace>().First();
            var point = source.GetGlobalRect().GetCenter(); PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            for (var i = 0; i < 240 && !ContainsEitherPreview(GetTree().Root); i++) await EventFrames(1);
            SupportRequire(ContainsEitherPreview(GetTree().Root), "Payment source has no large hover preview; hovered=" + GetViewport().GuiGetHoveredControl()?.GetPath() + "; focus=" + GetWindow().HasFocus());
            GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(_capturePath, null) + "-hover.png"); HideTableTooltip();
            // Keyboard input into the native amount button; repeated keys retain focus after rebuilding the panel.
            OpponentPaymentButton("OpponentPaymentMore").GrabFocus(); await EventFrames(4);
            for (var i = 0; i < 3; i++)
            {
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(10);
            }
            SupportRequire(_chosen!.Command!.GoldAmount == 3 && StabilizationText(_inspector).Contains("Nobody gains Honor."), "Keyboard amount did not preserve focus and update the tie outcome");
            await SupportCapture("-tie");
            await SetOpponentPaymentAmount(4);
            SupportRequire(StabilizationText(_inspector).Contains("You gain 1 Honor.") && View.Version == version, "Amount changed match or lost immediate outcome");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_paymentConfirm!.GetGlobalRect()), "Payment confirmation outside window");
            await SupportCapture("-gain");
            var tableSource = Self.Home.First(c => Production(c, Self) > 0 && !c.Bowed);
            var wasSelected = _sources.Contains(tableSource.Id);
            await ClickPaymentTable(_tiles.Single(t => t.Card.Id == tableSource.Id));
            SupportRequire(_sources.Contains(tableSource.Id) != wasSelected && View.Version == version, "Table source did not toggle the reviewed payment");
            await ClickPaymentTable(_tiles.Single(t => t.Card.Id == tableSource.Id));
            var keyboardSource = _inspector.FindChildren("*", "Button", true, false).OfType<CheckBox>().Last(b => !b.Disabled);
            var keyboardId = keyboardSource.GetMeta("payment_source_id").AsString(); var keyboardSelected = _sources.Contains(keyboardId);
            keyboardSource.GrabFocus(); await EventFrames(10);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(10);
            SupportRequire(_sources.Contains(keyboardId) != keyboardSelected, "Keyboard source did not toggle");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(10);
            SupportRequire(_sources.Contains(keyboardId) == keyboardSelected, "Keyboard source did not retain focus after rebuilding");
            var selectedSources = _sources.ToArray();
            foreach (var id in selectedSources)
                await SupportClick(_inspector.FindChildren("*", "Button", true, false).OfType<CheckBox>().Single(b => b.GetMeta("payment_source_id", "").AsString() == id));
            SupportRequire(_paymentConfirm!.Disabled, "Underfunded payment can be confirmed");
            await SupportCapture("-short");
            await SupportClick(OpponentPaymentButton("OpponentPaymentMax"));
            SupportRequire(_chosen.Command.GoldAmount == View.PendingOpponentPayment.Gold!.MaxAmount && OpponentPaymentButton("OpponentPaymentMore").Disabled && StabilizationText(_inspector).Contains("You gain 4 Honor."), "Engine maximum/capped outcome not shown");
            await SupportCapture("-cap");
            _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(View.PendingOpponentPayment is { ViewerDecides: false, Gold: null, Outcomes: null } && _chosen is null && _paymentConfirm is null, "Waiting seat leaked a private selection or quote");
            SupportRequire(!_inspector.FindChildren("*", "Button", true, false).OfType<CheckBox>().Any(), "Waiting seat rendered payer sources");
            await SupportCapture("-waiting");
            _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_chosen?.Command?.GoldAmount == 0 && _sources.Count == 0, "Seat switch retained private stale choices");
            await SetOpponentPaymentAmount(4);
            var honor = Self.Honor; var paying = _sources.ToArray();
            await SupportClick(_paymentConfirm!); await WaitDecisionPresentation();
            SupportRequire(View.PendingOpponentPayment is null && Self.Honor == honor + 1 && paying.All(id => FindCard(id)?.Bowed == true), "Chosen payment did not resolve and bow selected sources");
            await SupportCapture("-resolved");
            await PrepareOpponentPaymentReview();
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(6);
            SupportRequire(_chosen is null && _paymentConfirm is null, "Busy session retained submittable payment");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(6);
            await SupportClick(_paymentConfirm!); await WaitDecisionPresentation();
            SupportRequire(View.PendingOpponentPayment is null && View.Events.Any(e => e.Type == "opponentPaymentChosen" && e.Amount == 0), "Zero payment failed");
            await PrepareOpponentPaymentReview("automatic");
            SupportRequire(View.PendingOpponentPayment is null && _eitherNotice is not null && StabilizationText(_sidebarHeader).Contains("RESOLVED AUTOMATICALLY"), "No-Gold explanation missing");
            await SupportCapture("-automatic");
            _session.SwitchPracticeSeat(); await EventFrames(15); SupportRequire(StabilizationText(_sidebarHeader).Contains(_eitherNotice!), "Waiting actor lost automatic explanation");
            await SupportCapture("-automatic-other");
            if (_captureMode.Contains("review"))
            {
                await SupportClick(_opponentPaymentReviewButtons[1]);
                for (var i = 0; i < 180 && _opponentPaymentReviewBusy; i++) await EventFrames(1);
                SupportRequire(View.PendingOpponentPayment?.FirstAmount == 0, "Prepared-case navigation failed");
                await SupportClick(_opponentPaymentReviewButtons.Last()); await EventFrames(10);
                SupportRequire(!View.PendingOpponentPayment!.ViewerDecides, "Other-seat navigation failed");
                await SupportClick(_opponentPaymentReviewButtons.Last()); await EventFrames(10);
                SupportRequire(View.PendingOpponentPayment!.ViewerDecides, "Returning to payer failed");
            }
            GD.Print("OPPONENT_PAYMENT_PASS: native keyboard/mouse, amount bounds, engine outcomes/cap, selected sources, short payment, zero confirmation, full-card hover, waiting privacy, seat reset, busy guard, automatic explanations and prepared navigation.");
            GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
