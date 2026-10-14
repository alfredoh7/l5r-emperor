using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private TutorialCoachPanel? _coachPanel;
    private bool TutorialActive => _session.Tutorial is not null;
    private const float TutorialColumnTop = 178, TutorialColumnWidth = 296;
    /// <summary>The coach card owns a column at the left of the table while a lesson is showing; the table is fitted to the room that is left, so nothing is covered.</summary>
    private float TutorialReserve => _coachPanel is { Visible: true } ? 176 : 0;

    private void StartTutorial()
    {
        _turnBloomGate.Reset(); _turnBloom.Cancel(); ResetStatHistory();
        _presentationSnapshot = null; _battlePresentationStage = 0; _motionView = null; _cameraGlide = false;
        _enteringPrivate = false; _tableLayoutSeen = false; _cameraFitted = true;
        _selected = null; _selectedHomeReminder = null; _chosen = null; _expandedUnits.Clear(); _sources.Clear(); _assignments.Clear();
        _session.StartTutorial();
        EnterTable();
    }

    /// <summary>Cards the lesson is talking about right now; the table draws a soft pulse on them.</summary>
    private HashSet<string> TutorialPointed() => _session.Tutorial is { Finished: false } lesson ? [.. lesson.Coach.HighlightCardIds] : [];

    /// <summary>Shows, updates or removes the coach card to match the lesson after every render.</summary>
    private void SyncTutorialCoach()
    {
        if (_session.Tutorial is not { } lesson || MenuVisible)
        {
            _coachPanel?.Hide(); return;
        }
        if (_coachPanel is null)
        {
            _coachPanel = new TutorialCoachPanel { Continue = TutorialContinue };
            _root.AddChild(_coachPanel);
        }
        var coach = lesson.Coach;
        if (_coachPanel.StepId != coach.StepId || !_coachPanel.Visible) _coachPanel.Present(coach);
        _coachPanel.Show(); LayoutTutorialCoach();
    }

    private void LayoutTutorialCoach()
    {
        if (_coachPanel is not { Visible: true } panel) return;
        panel.SetReady(!PresentationBusy && !PlayShowcaseBusy && !_session.Busy);
        var height = Math.Max(300, GetViewport().GetVisibleRect().Size.Y / _root.Scale.Y - TutorialColumnTop - 196);
        panel.CustomMinimumSize = new Vector2(TutorialColumnWidth, 0); panel.Size = new Vector2(TutorialColumnWidth, height);
        panel.Position = new Vector2(16, TutorialColumnTop);
    }

    /// <summary>Leaves the lesson at any point (the table menu's Concede while a lesson is running) and returns to the menu.</summary>
    private void LeaveTutorial()
    {
        _session.LeaveTutorial(); _coachPanel?.Hide();
        _selected = null; _chosen = null; _sources.Clear(); _assignments.Clear();
        ShowMainMenu();
    }

    private void TutorialContinue()
    {
        _session.TutorialContinue();
        if (_session.Tutorial is { Finished: true }) { _coachPanel?.Hide(); ShowMainMenu(); }
    }

    private void PulseTutorialPointers()
    {
        if (!TutorialActive || _reducedMotion) return;
        foreach (var tile in _tiles) if (tile.Pointed && IsInstanceValid(tile)) tile.QueueRedraw();
    }

    // ---- The tutorial lab (issue #935) -------------------------------------------------------------------------------------------------------------------------------------
    // `--tutorial-review` opens the tutorial directly; `--tutorial-step=<id>` plays the lesson through the real table to that step first; `--capture=<png>` saves the view there and quits.

    private void StartTutorialReview()
    {
        var capture = _capturePath; _capturePath = null;
        var step = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--tutorial-step="))?["--tutorial-step=".Length..];
        StartTutorial();
        Callable.From(() => { _ = RunTutorialReview(step, capture); }).CallDeferred();
        GD.Print("TUTORIAL_REVIEW_READY: lesson 1 on the real engine.");
    }

    private async Task RunTutorialReview(string? step, string? capture)
    {
        for (var i = 0; i < 6; i++) await CoverageFrames();
        if (OS.GetCmdlineUserArgs().Contains("--tutorial-ui-check")) { await CheckTutorialUi(); return; }
        if (step is not null && !await PlayTutorialTo(step)) { GD.PushError($"TUTORIAL_REVIEW: step '{step}' was not reached."); GetTree().Quit(1); return; }
        for (var i = 0; i < 12; i++) await CoverageFrames();
        if (OS.GetCmdlineUserArgs().Contains("--tutorial-dump") || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--tutorial-script=")))
        {
            GD.Print($"TUTORIAL_DUMP at {_session.Tutorial?.Coach.StepId}: {TutorialControls()}");
            foreach (var token in (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--tutorial-script="))?["--tutorial-script=".Length..] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                await RunTutorialUiToken(token);
                GD.Print($"TUTORIAL_DUMP after {token}: step={_session.Tutorial?.Coach.StepId} {TutorialControls()} notice={_status.Text}");
            }
            if (capture is null) { GetTree().Quit(); return; }
        }
        if (capture is null) return;
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(capture);
        GD.Print("TUTORIAL_REVIEW: captured step " + (_session.Tutorial?.Coach.StepId ?? "done")); GetTree().Quit();
    }

    private string TutorialControls()
    {
        string Buttons(Node root) => string.Join(" | ", root.FindChildren("*", "Button", true, false).OfType<Button>().Select(b => (b.Disabled ? "[x]" : "") + b.Text));
        return $"phase=[{Buttons(_phaseActions)}] decision=[{Buttons(_decisionActions)}] inspector=[{Buttons(_inspector)}]";
    }

    /// <summary>One thing a player does at the table, through the real controls: select:&lt;card&gt;, selectprovince:&lt;owner&gt;/&lt;index&gt;, click:&lt;button text&gt;, wait:&lt;seconds&gt;.</summary>
    private async Task RunTutorialUiToken(string token)
    {
        var (verb, arg) = token.Split(':', 2) is [var v, var a] ? (v, a) : (token, "");
        if (verb == "select")
        {
            var tile = _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Definition?.Name == arg && t.Card.OwnerId == View.ViewerId) ?? _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Definition?.Name == arg);
            if (tile is null) { GD.Print("TUTORIAL_UI no tile for " + arg); return; }
            SelectCard(tile.Card);
        }
        else if (verb == "selectprovince")
        {
            var tile = _tiles.FirstOrDefault(t => IsInstanceValid(t) && t.Card.Zone == Zone.Province && t.Card.OwnerId == int.Parse(arg.Split('/')[0]) && t.Card.ProvinceIndex == int.Parse(arg.Split('/')[1]));
            if (tile is null) { GD.Print("TUTORIAL_UI no province tile " + arg); return; }
            SelectCard(tile.Card);
        }
        else if (verb == "menuleave") // the table's hamburger menu, its "Leave tutorial…" entry, and the confirmation
        {
            ShowTableMenu(); for (var i = 0; i < 4; i++) await CoverageFrames();
            var leave = GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "Leave tutorial…") ?? throw new InvalidOperationException("The table menu has no Leave tutorial entry.");
            leave.EmitSignal(BaseButton.SignalName.Pressed); await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var dialog = GetChildren().OfType<ConfirmationDialog>().FirstOrDefault(d => d.Title == "Leave the tutorial?") ?? throw new InvalidOperationException("Leaving did not ask for confirmation.");
            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        }
        else if (verb == "mouse") // a real pointer click at the centre of the coach's button, through the GUI input path
        {
            var button = _coachPanel?.FindChild("TutorialContinue", true, false) as Button ?? throw new InvalidOperationException("no coach button");
            for (var wait = 0; wait < 80 && !button.IsVisibleInTree(); wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout); // the button waits out card and battle presentations
            var at = button.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            foreach (var pressed in new[] { true, false })
                GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Left : 0 });
        }
        else if (verb == "wait") await ToSignal(GetTree().CreateTimer(double.Parse(arg)), SceneTreeTimer.SignalName.Timeout);
        else if (verb == "click")
        {
            var button = new[] { _decisionActions, _inspector, _phaseActions }.SelectMany(n => n.FindChildren("*", "Button", true, false).OfType<Button>()).FirstOrDefault(b => !b.Disabled && b.Text.Contains(arg, StringComparison.OrdinalIgnoreCase));
            if (button is null) { GD.Print("TUTORIAL_UI no enabled button " + arg); return; }
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        for (var i = 0; i < 8; i++) await CoverageFrames();
    }

    /// <summary>What a player clicks at each action step of lesson 1. A change to the table's controls that breaks one of these breaks the check below.</summary>
    private static readonly Dictionary<string, string> TutorialUiScripts = new()
    {
        ["border-refresh"] = "select:Border Keep;click:First-turn province refresh;selectprovince:0/1;selectprovince:0/2;selectprovince:0/3;click:Confirm",
        ["pass-action"] = "click:Pass action",
        ["decline-attack"] = "click:Decline to attack",
        ["buy-farm"] = "select:Small Farm;click:Recruit",
        ["buy-uehara"] = "select:Akodo Uehara;select:Halls of Memory;click:Recruit",
        ["end-dynasty"] = "click:End Dynasty Phase",
        ["tell-tale"] = "select:Tell the Tale;click:Confirm play",
        ["equip-follower"] = "select:Veteran Skirmishers;click:Confirm play;click:Akodo Uehara;select:Border Keep;click:Attach",
        ["equip-item"] = "select:Light Armor;click:Confirm play;click:Akodo Uehara;select:Halls of Memory;click:Attach",
        ["pass-turn-two"] = "click:Pass action",
        ["declare-attack"] = "click:Declare an attack",
        ["assign-attacker"] = "select:Akodo Uehara;selectprovince:1/0;click:Confirm assignments",
        ["pick-battle"] = "click:Fight at province 1",
        ["battle-action"] = "select:Crushing Strength;click:Confirm play;click:Akodo Uehara;click:Play Crushing Strength",
        ["pass-battle"] = "click:Pass battle action",
        ["ring-reaction"] = "wait:6;click:Skip Reaction",
    };

    /// <summary>Plays lesson 1 start to finish with the controls a player uses: the coach's button for notes, the table's own buttons and cards for every move.</summary>
    private async Task CheckTutorialUi()
    {
        try
        {
            var seen = new List<string>();
            for (var guard = 0; guard < 120 && _session.Tutorial is { Finished: false } lesson; guard++)
            {
                var coach = lesson.Coach; seen.Add(coach.StepId);
                if (coach.Button is not null)
                {
                    var before = coach.StepId;
                    await RunTutorialUiToken("mouse:coach"); await CoverageFrames(); await CoverageFrames();
                    if (_session.Tutorial is { Finished: false } moved && moved.Coach.StepId == before) throw new InvalidOperationException($"{before}: a real click on the coach's button did nothing.");
                    if (coach.StepId == "done") break;
                    continue;
                }
                if (!TutorialUiScripts.TryGetValue(coach.StepId, out var script)) throw new InvalidOperationException($"{coach.StepId}: no UI script for this step.");
                foreach (var token in script.Split(';')) await RunTutorialUiToken(token);
                for (var wait = 0; wait < 60 && _session.Tutorial is { Finished: false } still && still.Coach.StepId == coach.StepId; wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                if (_session.Tutorial is { Finished: false } after && after.Coach.StepId == coach.StepId)
                    throw new InvalidOperationException($"{coach.StepId}: the step did not advance. {TutorialControls()} notice={_status.Text}");
            }
            if (_session.Tutorial is { Finished: false }) throw new InvalidOperationException("The lesson did not finish.");
            var honor = View.Players[0].Honor;
            // Leaving at any moment: start again, get part way in, and leave through the table menu.
            StartTutorial(); for (var i = 0; i < 6; i++) await CoverageFrames();
            await PlayTutorialTo("border-refresh");
            await RunTutorialUiToken("menuleave"); for (var i = 0; i < 6; i++) await CoverageFrames();
            if (_session.Tutorial is not null || !MenuVisible) throw new InvalidOperationException("Leaving the tutorial from the table menu did not return to the main menu.");
            GD.Print($"TUTORIAL_UI_CHECK: completed {seen.Count} steps; Honor {honor}; left mid-lesson through the table menu");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("TUTORIAL_UI_CHECK failed: " + e.Message); GetTree().Quit(1); }
    }

    /// <summary>Plays the lesson as a player would, through the session's own submit path, until the named step is showing.</summary>
    private async Task<bool> PlayTutorialTo(string stepId)
    {
        for (var guard = 0; guard < 300 && _session.Tutorial is { Finished: false } lesson; guard++)
        {
            if (lesson.Coach.StepId == stepId) return true;
            if (lesson.Coach.Button is not null) { _session.TutorialContinue(); await CoverageFrames(); continue; }
            var offered = Actions.FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type != "concede");
            if (offered?.Command is not { } command) return false;
            if (lesson.Coach.StepId == "border-refresh")
                command = command with { CardIds = new[] { 1, 2, 3 }.Select(i => Self.Provinces[i].Card!.Id).ToArray() };
            await Submit(command); await CoverageFrames();
        }
        return _session.Tutorial?.Coach.StepId == stepId;
    }
}
