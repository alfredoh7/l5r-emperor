using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _sequenceCaptureStarted, _sequenceReviewBusy;
    private MatchEngine _sequenceFixture = null!;
    private Button[] _sequenceReviewButtons = [];
    private void ProcessBattleSequenceCapture(double delta)
    {
        _captureClock += delta;
        if (_sequenceCaptureStarted || _captureClock < 1.5) return;
        _sequenceCaptureStarted = true; _ = CaptureBattleSequence();
    }
    private async Task PrepareSequenceReview(string mode = "naval")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
        var declaration = mode == "declaration";
        var selection = mode is "naval" or "override" or "must-pass" or "empty" or "ordinary";
        state.Phase = declaration ? Phase.AttackDeclaration : selection ? Phase.BattleSelection : Phase.Battle;
        state.ActivePlayerId = 0; state.PriorityPlayerId = selection || declaration ? 0 : 1;
        state.PendingChoice = null; state.CurrentBattleProvince = selection || declaration ? null : 0;
        state.UnresolvedBattlefields = declaration ? [] : [0];
        var zone = declaration ? Zone.Home : Zone.Battlefield; int? province = declaration ? null : 0;
        if (mode != "empty")
        {
            SupportFixtureCard(state, mode is "naval" or "must-pass" ? "oracle-l5r-8840" : mode == "override" ? "oracle-l5r-1696" : mode == "order" ? "training-scout-samurai" : declaration ? "oracle-l5r-3636" : "oracle-l5r-2060", 0, zone, province);
            SupportFixtureCard(state, mode == "override" ? "oracle-l5r-1696" : mode == "order" ? "training-crane-samurai" : "oracle-l5r-3636", 1, zone, province);
        }
        if (declaration) foreach (var player in new[] { 0, 1 }) SupportFixtureCard(state, 2060, player, Zone.Home);
        if (mode == "must-pass") SupportFixtureCard(state, 6200, 1, Zone.EventInPlay);
        if (mode == "order")
        {
            SupportFixtureCard(state, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, 0);
            SupportFixtureCard(state, "training-before-resolution-send-home-terrain", 1, Zone.Battlefield, 0);
        }
        if (mode == "optional") SupportFixtureCard(state, "training-before-resolution-may-bow-terrain", 1, Zone.Battlefield, 0);
        if (mode == "reaction") SupportFixtureCard(state, "training-steady-resolve", 1, Zone.Hand);
        if (mode == "encircled")
        {
            SupportFixtureCard(state, 2332, 0, Zone.Battlefield, 0);
            SupportFixtureCard(state, 2867, 0, Zone.Battlefield, 0);
            SupportFixtureCard(state, 4664, 1, Zone.Battlefield, 0);
        }
        _sequenceFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        _resolutionMessage = null; _battleSequence = -1; _battleSequenceOpen = false;
        await LoadSupportFixture(_sequenceFixture, state.PriorityPlayerId);
        if (selection) await SequenceSubmit("selectBattle");
        if (mode is "order" or "optional" or "reaction" or "encircled") { await SequenceSubmit("pass"); await SequenceSubmit("pass"); }
        await SequenceSeat();
        if (!declaration) { FocusBattle(); await EventFrames(8); }
        if (_captureMode.Contains("review")) BuildSequenceReviewControls();
        GD.Print("BATTLE_SEQUENCE_SCENARIO: " + mode + " / " + View.BattleStep?.Step + " / " + View.PendingChoice?.Type);
    }
    private OptionButton? _sequenceReviewPicker;
    private void BuildSequenceReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("SequenceReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "SequenceReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 10);
        var scenarios = new[] { ("Declaration and maneuvers", "declaration"), ("Ordinary battle", "ordinary"), ("Naval Invasion", "naval"), ("Defender override", "override"), ("First opportunity must pass", "must-pass"), ("Encircled Terrain", "encircled"), ("Order before resolution*", "order"), ("Optional before resolution*", "optional"), ("Reaction before resolution*", "reaction"), ("Empty battlefield", "empty") };
        _sequenceReviewPicker = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(340, 44), TooltipText = "Prepared review scenarios. * includes synthetic training cards." };
        _sequenceReviewPicker.AddItem("Review another battle scenario…");
        foreach (var (label, _) in scenarios) _sequenceReviewPicker.AddItem(label);
        _sequenceReviewPicker.ItemSelected += i => { if (i > 0) _ = RunSequenceReview(scenarios[(int)i - 1].Item2); };
        row.AddChild(_sequenceReviewPicker);
        var other = SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()); other.CustomMinimumSize = new(150, 44); row.AddChild(other);
        _sequenceReviewButtons = [other];
        Place(_root, row, 350, 114, Math.Min(730, BoardWidth - 350 - 24), 46);
    }
    private async Task RunSequenceReview(string mode)
    {
        if (_sequenceReviewBusy) return;
        _sequenceReviewBusy = true; if (_sequenceReviewPicker is not null) _sequenceReviewPicker.Disabled = true; foreach (var b in _sequenceReviewButtons) b.Disabled = true;
        try { await PrepareSequenceReview(mode); }
        catch (Exception e) { SetNotice("Restart a battle scenario to prepare it again."); GD.PushWarning(e.ToString()); }
        finally { _sequenceReviewBusy = false; if (_sequenceReviewPicker is not null) _sequenceReviewPicker.Disabled = false; foreach (var b in _sequenceReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task SequenceSeat()
    { if (View.ViewerId != View.PriorityPlayerId) { _session.SwitchPracticeSeat(); await EventFrames(15); } }
    private async Task SequenceSubmit(string type, Func<ActionOption, bool>? filter = null)
    {
        await SequenceSeat();
        var action = Actions.FirstOrDefault(a => a.Enabled && a.Type == type && (filter?.Invoke(a) ?? true));
        SupportRequire(action?.Command is not null, "Missing offered " + type + ": " + string.Join(", ", Actions.Select(a => a.Type + " " + a.Reason)));
        var version = View.Version;
        await _session.Submit(action!.Command!); await WaitDecisionPresentation(); await EventFrames(18);
        SupportRequire(View.Version > version, "Engine refused offered " + type);
    }
    private async Task SequenceNativePhase(string type)
    {
        await SequenceSeat(); var action = Actions.Single(a => a.Enabled && a.Type == type);
        var button = StabilizationButtons(_phaseActions).Single(b => b.Text == action.Label);
        var version = View.Version; await SupportClick(button); await WaitDecisionPresentation(); await EventFrames(18);
        SupportRequire(View.Version > version, "Native phase button did not submit " + type);
    }
    private async Task SequenceDecision(Func<ActionOption, bool> filter)
    {
        await SequenceSeat();
        SupportRequire(GenericDecisionActive && DecisionButton("Confirm choice").Disabled, "Timing choice was not explicit");
        var action = Actions.First(a => a.Enabled && filter(a));
        await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text == action.Label));
        await SupportClick(DecisionButton("Confirm choice")); await WaitDecisionPresentation(); await EventFrames(18);
    }
    private void CheckSequenceLayout()
    {
        SupportRequire(_battleProgressStrip?.Visible == true && _battleProgressStrip.ActiveSegment == ProgressSegment, "Wrong active battle segment");
        var strip = _battleProgressStrip!.GetGlobalRect();
        SupportRequire(GetViewport().GetVisibleRect().Encloses(strip), "Strip clipped");
        SupportRequire(Math.Abs(_timeline.PlateBounds.End.X - strip.End.X) < 1, "Phase plate is not flush with the narrow battle strip");
        SupportRequire(_timeline.PlateBounds.End.Y <= strip.Position.Y + 1, "Phase plate overlaps battle strip");
        SupportRequire(!_tableViewport.GetGlobalRect().Intersects(strip) && !_hand.GetGlobalRect().Intersects(strip), "Strip overlaps table or hand");
        SupportRequire(!RulesHelp.PhaseGuide(View.Phase).Contains("match has ended"), "Active battle described as ended");
        if (View.CurrentBattleProvince is { } province)
            SupportRequire(_tiles.Where(t => t.Card.Zone == Zone.Battlefield && t.Card.ProvinceIndex == province && t.Card.Definition?.Type == CardType.Personality).All(t => _tableViewport.GetGlobalRect().HasPoint(ScreenCenter(t))), "Current battle units are offscreen");
    }
    private async Task CaptureBattleSequence()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareSequenceReview();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal)) { GD.Print("BATTLE_SEQUENCE_REVIEW_READY: prepared engine decisions; starred scenarios use synthetic fixtures."); return; }
            if (!GetWindow().HasFocus()) GD.Print("BATTLE_SEQUENCE_FOCUS_REQUIRED: activate the game window for native input and camera motion.");
            for (var i = 0; i < 18000 && !GetWindow().HasFocus(); i++) await EventFrames(1);
            SupportRequire(GetWindow().HasFocus(), "Native window did not receive focus");
            await EventFrames(60); CheckSequenceLayout(); await SupportCapture("-engage");
            var version = View.Version;
            _battleProgressStrip!.StepButton(3).GrabFocus(); await LookKey(Key.Enter);
            SupportRequire(_battleSequenceOpen && View.Version == version, "Keyboard sequence inspection changed game"); await SupportCapture("-sequence");
            await SupportClick(_battleProgressStrip.StepButton(3)); SupportRequire(!_battleSequenceOpen, "Sequence did not close");
            await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text.StartsWith("Naval Invasion:")));
            SupportRequire(_chosen?.Command?.AbilityId == "naval-invasion", "Naval action button did not prepare the real command");
            await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Confirm action")); await WaitDecisionPresentation(); await EventFrames(18);
            while (View.PendingChoice?.Type == "engageReaction") await SequenceNativePhase("declineReaction");
            await SequenceSeat(); CheckSequenceLayout();
            SupportRequire(View.BattleStep is { FirstActionReason: BattleSteps.ReasonNaval, FirstActionLimit: BattleSteps.LimitPerformerOnly }, "Naval result not displayed"); await SupportCapture("-naval");
            await SupportClick(_battleProgressStrip.StepButton(3));
            await SequenceNativePhase("pass"); await SequenceSeat();
            await EventFrames(60); CheckSequenceLayout();
            SupportRequire(!_battleSequenceOpen && View.BattleStep?.ConsecutivePasses == 1, "Incoming decision hidden or pass counter wrong"); await SupportCapture("-one-pass");
            await SequenceNativePhase("pass"); await SupportCapture("-outcome");
            await PrepareSequenceReview("declaration"); CheckSequenceLayout(); await SupportCapture("-declaration");
            await SequenceNativePhase("declareAttack");
            foreach (var phase in new[] { Phase.InfantryDeclaration, Phase.DefenseAssignment, Phase.CavalryDeclaration, Phase.CavalryDefense })
            {
                await SequenceSeat(); SupportRequire(View.Phase == phase, "Missing formal maneuver step " + phase); CheckSequenceLayout();
                await SupportClick(StabilizationButtons(_inspector).Single(b => b.Text == "Assign units  ›"));
                var chosen = _chosen; var before = View.Version;
                await SupportClick(_battleProgressStrip!.StepButton(1));
                await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Back to selected action"));
                SupportRequire(_chosen == chosen && View.Version == before, "Sequence lost assignment draft");
                if (phase == Phase.CavalryDeclaration) await SupportCapture("-cavalry");
                await SupportClick(StabilizationButtons(_decisionActions).Single(b => b.Text == "Confirm assignments")); await WaitDecisionPresentation(); await EventFrames(18);
            }
            await SequenceSeat();
            SupportRequire(View.Phase == Phase.BattleSelection && Actions.Count(a => a.Type == "selectBattle") == 4, "Empty battlefields skipped");
            await PrepareSequenceReview("declaration"); await SequenceNativePhase("pass");
            SupportRequire(View.Phase == Phase.Dynasty && !_battleProgressStrip!.Visible, "Decline did not hide battle strip");
            await PrepareSequenceReview("override");
            for (var i = 0; i < 2; i++) await SequenceSubmit("useAbility", a => a.Command?.AbilityId == "yuki-engage-recon");
            while (View.PendingChoice?.Type == "engageReaction") await SequenceNativePhase("declineReaction");
            SupportRequire(View.BattleStep?.FirstActionReason == BattleSteps.ReasonDefenderOverride, "Defender override reason missing"); await SupportCapture("-override");
            await PrepareSequenceReview("must-pass"); await SequenceSubmit("useAbility", a => a.Command?.AbilityId == "naval-invasion");
            while (View.PendingChoice?.Type == "engageReaction") await SequenceNativePhase("declineReaction");
            await SequenceSeat(); SupportRequire(View.BattleStep?.FirstActionLimit == BattleSteps.LimitMustPass, "Must-pass reason missing");
            await SupportCapture("-must-pass"); await SequenceNativePhase("pass"); SupportRequire(View.BattleStep?.ConsecutivePasses == 1, "Forced pass did not count");
            await PrepareSequenceReview("order"); CheckSequenceLayout(); SupportRequire(View.PendingChoice?.Type == "beforeBattleOrder", "Ordering fixture did not open");
            await SupportCapture("-order"); await SequenceDecision(a => a.Command?.TargetId is { } id && _sequenceFixture.State.Cards.GetValueOrDefault(id)?.DefinitionId == "training-before-resolution-boost-terrain");
            SupportRequire(View.PendingChoice is null && View.Events.Any(e => e.Type == "battleResolved" && e.Message.Contains("attacking Force 4, defending Force 0")), "Chosen before-resolution order did not resolve");
            await PrepareSequenceReview("encircled"); SupportRequire(View.PendingChoice?.Type == "encircledOrder", "Encircled ordering unavailable");
            await SupportCapture("-encircled"); await SequenceDecision(a => a.Command?.Choice == "defender");
            for (var i = 0; i < 2; i++) { SupportRequire(View.PendingChoice?.Type == "encircledSelect", "Encircled selection unavailable"); await SequenceDecision(a => a.Type == "encircledSelect"); }
            SupportRequire(View.PendingChoice is null && View.Events.Any(e => e.Type == "battleResolved"), "Encircled did not finish");
            await PrepareSequenceReview("optional"); SupportRequire(View.PendingChoice?.Type == "beforeBattleOptional", "Optional effect unavailable"); await SequenceDecision(a => a.Command?.Choice == "decline");
            SupportRequire(View.PendingChoice is null, "Optional effect did not finish");
            await PrepareSequenceReview("reaction"); SupportRequire(View.PendingChoice?.Type == "beforeBattleReaction", "Reaction fixture missing"); CheckSequenceLayout();
            await SupportCapture("-reaction"); await SequenceNativePhase("declineReaction"); SupportRequire(View.PendingChoice is null, "Reaction did not finish");
            await PrepareSequenceReview("empty"); version = View.Version; await EventFrames(90);
            SupportRequire(View.Version == version && View.Phase == Phase.Battle, "Empty battlefield advanced automatically");
            await SequenceNativePhase("pass"); await SequenceNativePhase("pass");
            SupportRequire(View.Events.Any(e => e.Type == "battleResolved" && e.Message.Contains("attacking Force 0, defending Force 0")), "Empty battle did not resolve through actual passes");
            GD.Print("BATTLE_SEQUENCE_PASS: native strip/keyboard, formal maneuvers, draft retention, decline, Naval, override, forced and ordinary passes, ordered/optional/Encircled effects, reaction ownership, empty battles and layout."); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
