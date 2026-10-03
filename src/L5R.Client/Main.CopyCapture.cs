using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _copyCaptureStarted, _copyReviewBusy;
    private MatchEngine _copyFixture = null!;
    private string _copyReceiverId = "", _copyDonorId = "";
    private Button[] _copyReviewButtons = [];
    private void ProcessCopyCapture(double delta)
    { _captureClock += delta; if (_copyCaptureStarted || _captureClock < 1.5) return; _copyCaptureStarted = true; _ = CaptureCopy(); }
    private async Task PrepareCopyReview(string mode = "fukuzo")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var single = mode is "keyword" or "ability" or "turn";
        var receiver = SupportFixtureCard(state, single ? "training-skin-receiver" : "oracle-l5r-2728", 0, Zone.Home);
        var donor = SupportFixtureCard(state, mode == "fukuzo" ? "oracle-l5r-10056" : mode == "automatic" ? "training-battle-bower" : mode == "nothing" ? "training-lion-samurai" : "training-skin-donor", 1, Zone.DynastyDiscard);
        if (mode == "automatic") donor.GrantedKeywords = [new("Kensai", "copy-review", "Permanent")];
        if (mode == "ability") SupportFixtureCard(state, "training-lion-samurai", 1, Zone.Home);
        _copyReceiverId = receiver.Id; _copyDonorId = donor.Id;
        _copyFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_copyFixture, 0);
        var ability = mode == "keyword" ? "training-copy-keyword" : mode == "ability" ? "training-copy-ability" : mode == "turn" ? "training-copy-ability-turn" : "fukuzo-wear-skin";
        var action = Actions.Single(a => a.Enabled && a.CardId == receiver.Id && a.Command?.AbilityId == ability);
        await _session.Submit(action.Command! with { TargetId = donor.Id }); await WaitDecisionPresentation();
        if (_captureMode.Contains("review")) BuildCopyReviewControls(); await EventFrames(15);
    }
    private void BuildCopyReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("CopyReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "CopyReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 5); var buttons = new List<Button>();
        foreach (var (label, mode) in new[] { ("Fukuzo", "fukuzo"), ("Keyword only", "keyword"), ("Ability only", "ability"), ("This turn", "turn"), ("Automatic", "automatic"), ("Nothing", "nothing") })
            buttons.Add(SidebarSkin.Button(label, () => _ = RunCopyReview(() => PrepareCopyReview(mode))));
        buttons.Add(SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat())); _copyReviewButtons = buttons.ToArray();
        foreach (var b in buttons) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.CustomMinimumSize = new(0, 44); b.AddThemeFontSizeOverride("font_size", 15); row.AddChild(b); }
        Place(_root, row, 344, 114, 1000, 46); FitCopyReviewControls();
    }
    private void FitCopyReviewControls()
    { if (_root.GetNodeOrNull<Control>("CopyReviewControls") is { } row) row.Size = new(Math.Min(1120, _layoutWidth - SidebarWidth - 22 - 344 - 18), 46); }
    private async Task RunCopyReview(Func<Task> action)
    {
        if (_copyReviewBusy) return; _copyReviewBusy = true; foreach (var b in _copyReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("Restart a copy example to prepare its decision again."); GD.PushWarning(e.ToString()); }
        finally { _copyReviewBusy = false; foreach (var b in _copyReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task ChooseCopy(string value, bool keyboard = false)
    {
        var version = View.Version; var button = _copyPanel!.Choices.Single(c => c.Choice.Value == value).Button;
        if (keyboard)
        {
            button.GrabFocus(); await EventFrames(5); GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await EventFrames(8);
        }
        else await SupportClick(button);
        SupportRequire(View.Version == version && _copyPanel.Selection?.Command?.Choice == value && !_copyPanel.ConfirmButton.Disabled, "Copy selection must wait for confirmation");
        SupportRequire(GetViewport().GetVisibleRect().Encloses(_copyPanel.GetGlobalRect()) && _copyPanel.GetGlobalRect().Encloses(_copyPanel.ConfirmButton.GetGlobalRect()), "Copy tray/confirmation leaves its viewport bounds");
    }
    private async Task ConfirmCopy()
    { await SupportClick(_copyPanel!.ConfirmButton); await WaitDecisionPresentation(); await EventFrames(12); }
    private CardView CopyReceiver() => FindCard(_copyReceiverId)!;
    private async Task CaptureCopy()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false); await PrepareCopyReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal)) { GD.Print("COPY_REVIEW_READY: Fukuzo and six prepared review controls; choices use the real engine."); return; }
            if (_captureMode.Contains("review"))
            {
                var row = _root.GetNode<Control>("CopyReviewControls");
                SupportRequire(!row.GetGlobalRect().Intersects(_inspectorPanel.GetGlobalRect()) && !row.GetGlobalRect().Intersects(_copyPanel!.GetGlobalRect()), "Review navigation overlaps the decision UI");
                await SupportClick(_copyReviewButtons.Last()); SupportRequire(View.PendingCopy is null && _copyPanel is null, "Review seat switch retained chooser controls");
                await SupportClick(_copyReviewButtons.Last()); SupportRequire(View.PendingCopy is not null && _copyPanel is not null, "Review seat switch failed to restore chooser");
            }
            SupportRequire(_copyPanel is not null && _copyPanel.ConfirmButton.Disabled && _copyPanel.Progress.Text == "Step 1 of 2", "Keyword step missing, preselected or wrong progress");
            SupportRequire(_copyFixture.State.Cards[_copyDonorId].Zone == Zone.RemovedFromGame, "Source removal cost was not paid before the wizard");
            await SupportCapture("-keyword");
            var face = StabilizationButtons(_copyPanel!).OfType<DecisionSourceFace>().First(); var point = face.GetGlobalRect().GetCenter();
            PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(80);
            SupportRequire(ContainsEitherPreview(GetTree().Root), "Copy source full-card hover missing");
            GetViewport().GetTexture().GetImage().SavePng(Path.ChangeExtension(_capturePath, null) + "-hover.png");
            var keyword = View.PendingCopy!.Choices.First(c => c.Value == "Earth").Value; await ChooseCopy(keyword, true);
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, true); Render(); await EventFrames(8);
            SupportRequire(_copyPanel!.Selection is null && _copyPanel.ConfirmButton.Disabled, "Busy copy retained selection");
            typeof(MatchSession).GetProperty(nameof(MatchSession.Busy))!.SetValue(_session, false); Render(); await EventFrames(8);
            await ChooseCopy(keyword); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_copyPanel is null && View.PendingCopy is null && !StabilizationText(_inspector).Contains("Confirm keyword"), "Waiting seat retained private copy controls");
            await SupportCapture("-waiting"); _session.SwitchPracticeSeat(); await EventFrames(15);
            SupportRequire(_copyPanel is not null && _copyPanel.Selection is null, "Returning to chooser retained stale selection");
            await ChooseCopy(keyword); await ConfirmCopy();
            SupportRequire(View.PendingCopy?.Step == CopyStep.Ability && _copyPanel!.Progress.Text == "Step 2 of 2" && CopyReceiver().GrantedKeywords!.Any(g => g.Keyword == keyword && g.CopiedFromName == "Tamori Tamoko"), "Fukuzo did not compose the independent second step");
            SupportRequire(StabilizationText(_inspector).Contains("CURRENTLY COPIED") && StabilizationText(_inspector).Contains(keyword), "Confirmed keyword missing from compact result summary");
            await SupportCapture("-ability"); await ChooseCopy("tamoko-negate", true); await SupportCapture("-selected"); await ConfirmCopy();
            SupportRequire(View.PendingCopy is null && _copyPanel is null && CopyReceiver().GrantedAbilities!.Single().AbilityId == "tamoko-negate", "Copied Reaction was not stored");
            SupportRequire(CardChanges.Marked(CopyReceiver()).Length == 2 && _tiles.Single(t => t.Card.Id == _copyReceiverId).TokenRail?.GrantSeal is not null, "Copies lack an on-card marker");
            SupportRequire(StabilizationText(_inspector).Contains("Copied from: Tamori Tamoko") && StabilizationText(_inspector).Contains("Until the copying ability is used again"), "Copy provenance/duration absent in inspector");
            await SupportCapture("-result");
            var tile = _tiles.Single(t => t.Card.Id == _copyReceiverId); var seal = tile.TokenRail!.GrantSeal!;
            seal.GrabFocus(); await EventFrames(10); SupportRequire(seal.HasFocusPanel && CardChangePanel.LastCardId == _copyReceiverId, "Copy marker keyboard inspection missing"); await SupportCapture("-marker"); seal.ReleaseFocus();
            await PrepareCopyReview("keyword"); SupportRequire(View.PendingCopy!.RemainingSteps.Length == 0 && View.PendingCopy.Step == CopyStep.Keyword, "Keyword-only fixture wrong");
            await ChooseCopy("Berserker"); await ConfirmCopy(); SupportRequire(View.PendingCopy is null && (CopyReceiver().GrantedAbilities ?? []).Length == 0, "Keyword-only opened an ability step");
            await PrepareCopyReview("ability"); SupportRequire(View.PendingCopy!.RemainingSteps.Length == 0 && View.PendingCopy.Step == CopyStep.Ability, "Ability-only fixture wrong");
            await ChooseCopy("training-bow-any", true); await ConfirmCopy();
            SupportRequire((CopyReceiver().GrantedKeywords ?? []).Length == 0, "Ability-only copied a keyword");
            await _session.Submit(_copyFixture.QueryActions(1).First(a => a.Enabled && a.Type == "pass").Command!); await WaitDecisionPresentation();
            if (_selected?.Id != _copyReceiverId) SelectCard(CopyReceiver()); await EventFrames(12);
            var copiedAction = Actions.Single(a => a.Enabled && a.CardId == _copyReceiverId && a.Command?.AbilityId == "training-bow-any");
            SupportRequire(copiedAction.Label.Contains("copied from"), "Copied ability lost ordinary action provenance");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text == copiedAction.Label));
            var opposingTarget = _tiles.Single(t => t.Card.OwnerId == 1 && t.Card.Definition?.Id == "training-lion-samurai");
            await SupportClick(opposingTarget);
            var target = _chosen!.Command!.TargetId!; await SupportCapture("-activation");
            await SupportClick(StabilizationButtons(_decisionActions).First(b => b.Text.StartsWith("Confirm", StringComparison.Ordinal))); await WaitDecisionPresentation();
            SupportRequire(_copyFixture.State.Cards[target].Bowed, "Copied action could not execute through ordinary UI");
            await _session.Submit(_copyFixture.QueryActions(1).First(a => a.Enabled && a.Type == "pass").Command!); await WaitDecisionPresentation();
            if (_selected?.Id != _copyReceiverId) SelectCard(CopyReceiver()); await EventFrames(8);
            SupportRequire(Actions.Any(a => !a.Enabled && a.CardId == _copyReceiverId && a.Label.Contains("copied from") && a.Reason!.Contains("already been used")), "Spent copied action lost engine reason");
            await PrepareCopyReview("turn"); await SupportCapture("-turn"); await ChooseCopy("training-create-token"); await ConfirmCopy();
            SupportRequire(StabilizationText(_inspector).Contains("Until turn ends"), "Turn copy duration is misleading");
            await PrepareCopyReview("automatic"); SupportRequire(_copyPanel is null && View.PendingCopy is null && _copyNotice is not null && CardChanges.Marked(CopyReceiver()).Length == 2, "Single candidates did not auto-resolve visibly"); await SupportCapture("-automatic");
            await PrepareCopyReview("nothing"); SupportRequire(_copyPanel is null && _copyNotice!.Contains("no keyword") && _copyNotice.Contains("no ability"), "Skipped copy steps were not announced"); await SupportCapture("-nothing");
            GD.Print("COPY_PASS: native independent/composed steps, source hover, privacy, keyboard, confirmation, result provenance/duration, marker, activation/spent reason and automatic/skipped events"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
