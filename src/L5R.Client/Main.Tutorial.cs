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
        ResetTutorialTable();
        _enteringPrivate = false; _tableLayoutSeen = false; _cameraFitted = true;
        _session.StartTutorial();
        EnterTable();
    }

    /// <summary>Forgets everything the table was showing, so a lesson (or the scene before this one) starts clean.</summary>
    private void ResetTutorialTable()
    {
        _turnBloomGate.Reset(); _turnBloom.Cancel(); ResetStatHistory();
        _presentationSnapshot = null; _battlePresentationStage = 0; _motionView = null; _cameraGlide = false;
        _selected = null; _selectedHomeReminder = null; _chosen = null; _expandedUnits.Clear(); _sources.Clear(); _assignments.Clear();
        _tutorialDeal = int.MaxValue; _tutorialDealPending = false; _tutorialDealAt.Clear();
        SyncTutorialGlow([]); SyncTutorialShow(null);
    }

    /// <summary>The coach's Back button: undoes the last step by putting the table back as it was before it.</summary>
    private void TutorialBack()
    {
        if (_session.Tutorial is null || PresentationBusy || PlayShowcaseBusy) return;
        ResetTutorialTable();
        _session.TutorialBack();
    }

    /// <summary>
    /// The fitted camera normally centres the whole mat, which keeps room at the right for Celestials, Rings and removed piles. The lesson's table starts empty on that side,
    /// so with the coach column taking the left the cards looked pushed off the cloth. While a lesson is showing, centre the cards and piles that are actually on the table instead.
    /// </summary>
    private Vector2? TutorialContentCenter()
    {
        if (!TutorialActive || _world is null) return null;
        var toWorld = _world.GetGlobalTransform().AffineInverse();
        Rect2? bounds = null;
        foreach (var tile in _tiles)
        {
            if (!IsInstanceValid(tile) || !tile.IsVisibleInTree() || tile.Size.X < 1 || tile.Card.Zone is Zone.Hand or Zone.Resolving) continue; // the hand is drawn flat on the screen, not on the table
            var transform = tile.GetGlobalTransform();
            foreach (var corner in new[] { Vector2.Zero, new Vector2(tile.Size.X, 0), tile.Size, new Vector2(0, tile.Size.Y) })
            {
                var point = toWorld * (transform * corner);
                bounds = bounds is { } b ? b.Expand(point) : new Rect2(point, Vector2.Zero);
            }
        }
        foreach (var pile in TablePiles) // the decks and discard piles are drawn by the pile layer, not as card tiles; without them your Fate deck ran under the side panel
        {
            var rect = new Rect2(pile.Position, pile.Size);
            bounds = bounds is { } b ? b.Merge(rect) : rect;
        }
        return bounds?.GetCenter();
    }

    /// <summary>Cards the lesson is talking about right now; the table draws a soft pulse on them.</summary>
    private HashSet<string> TutorialPointed() => _session.Tutorial is { Finished: false } lesson ? [.. lesson.Coach.HighlightCardIds] : [];

    /// <summary>Shows, updates or removes the coach card to match the lesson after every render.</summary>
    private void SyncTutorialCoach()
    {
        if (_session.Tutorial is not { } lesson || MenuVisible)
        {
            _coachPanel?.Hide(); SyncTutorialGlow([]); SyncTutorialShow(null); return;
        }
        if (_coachPanel is null)
        {
            _coachPanel = new TutorialCoachPanel { Continue = TutorialContinue, Back = TutorialBack };
            _root.AddChild(_coachPanel);
        }
        var coach = lesson.Coach;
        SyncTutorialGlow(coach.Glow ?? []); SyncTutorialShow(coach.ShowDefinitionId); SyncTutorialDeal(coach);
        if (_coachPanel.StepId != coach.StepId || !_coachPanel.Visible) _coachPanel.Present(coach);
        _coachPanel.Show(); LayoutTutorialCoach();
    }

    /// <summary>The Gold payment meter sits above the left end of the hand; while the coach column is showing it moves clear of it, so the paid amount stays in view.</summary>
    private void PlaceGoldMeter() =>
        _goldMeter.Position = new Vector2(Math.Max(_hand.Position.X + 8, _coachPanel is { Visible: true } ? 16 + TutorialColumnWidth + 16 : 0), _hand.Position.Y - 74);

    private void LayoutTutorialCoach()
    {
        PlaceGoldMeter();
        if (_coachPanel is not { Visible: true } panel) return;
        panel.SetReady(!PresentationBusy && !PlayShowcaseBusy && !_session.Busy && !TutorialDealing);
        var height = Math.Max(300, GetViewport().GetVisibleRect().Size.Y / _root.Scale.Y - TutorialColumnTop - 196);
        panel.CustomMinimumSize = new Vector2(TutorialColumnWidth, 0); panel.Size = new Vector2(TutorialColumnWidth, height);
        panel.Position = new Vector2(16, TutorialColumnTop);
    }

    /// <summary>Leaves the lesson at any point (the table menu's Concede while a lesson is running) and returns to the menu.</summary>
    private void LeaveTutorial()
    {
        _session.LeaveTutorial(); _coachPanel?.Hide(); SyncTutorialGlow([]); SyncTutorialShow(null);
        _selected = null; _chosen = null; _sources.Clear(); _assignments.Clear();
        ShowMainMenu();
    }

    private void TutorialContinue()
    {
        _session.TutorialContinue();
        if (_session.Tutorial is { Finished: true }) { _coachPanel?.Hide(); ShowMainMenu(); }
    }

    private void PulseTutorialPointers(double delta)
    {
        if (!TutorialActive) return;
        ProcessTutorialDeal(delta); UpdateTutorialDealFades(); AnimateTutorialHonor(delta);
        if (_reducedMotion) return;
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
        await WaitTutorialRest();
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

    /// <summary>One thing a player does at the table, through the real controls: select:&lt;card&gt;, selectprovince:&lt;owner&gt;/&lt;index&gt;, click:&lt;button text&gt;, resize:&lt;width&gt;x&lt;height&gt;, wait:&lt;seconds&gt;.</summary>
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
        else if (verb == "option") // the purchase review's mode picker (Clan discount, full cost, ...)
        {
            var picker = _inspector.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault() ?? throw new InvalidOperationException("no option picker for " + arg);
            var index = Enumerable.Range(0, picker.ItemCount).First(i => picker.GetItemText(i).Contains(arg, StringComparison.OrdinalIgnoreCase));
            picker.Select(index); picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
        }
        else if (verb == "menuleave") // the table's hamburger menu, its "Leave tutorial…" entry, and the confirmation
        {
            ShowTableMenu(); for (var i = 0; i < 4; i++) await CoverageFrames();
            var leave = GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "Leave tutorial…") ?? throw new InvalidOperationException("The table menu has no Leave tutorial entry.");
            leave.EmitSignal(BaseButton.SignalName.Pressed); await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var dialog = GetChildren().OfType<ConfirmationDialog>().FirstOrDefault(d => d.Title == "Leave the tutorial?") ?? throw new InvalidOperationException("Leaving did not ask for confirmation.");
            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        }
        else if (verb == "back") // the coach's Back button, then the step it should land on
        {
            var button = _coachPanel?.FindChild("TutorialBack", true, false) as Button ?? throw new InvalidOperationException("no Back button on the coach");
            for (var wait = 0; wait < 80 && !button.IsVisibleInTree(); wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            for (var i = 0; i < 8; i++) await CoverageFrames();
            if (_session.Tutorial?.Coach.StepId != arg) throw new InvalidOperationException($"Back landed on {_session.Tutorial?.Coach.StepId}, not {arg}.");
            await WaitTutorialRest();
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
        else if (verb == "resize") // the window resized by its owner, e.g. resize:2000x1300
        {
            var size = arg.Split('x').Select(int.Parse).ToArray();
            GetWindow().Size = new Vector2I(size[0], size[1]);
            for (var i = 0; i < 20; i++) await CoverageFrames();
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

    /// <summary>A player waits for a card or battle presentation to finish before the table takes a click; so do the lab's scripts.</summary>
    private async Task WaitTutorialRest()
    {
        for (var wait = 0; wait < 160 && (PresentationBusy || PlayShowcaseBusy || _session.Busy || TutorialDealing); wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        for (var i = 0; i < 4; i++) await CoverageFrames();
    }

    /// <summary>What a player clicks at each action step of lesson 1. A change to the table's controls that breaks one of these breaks the check below.</summary>
    private static readonly Dictionary<string, string> TutorialUiScripts = new()
    {
        ["pass-action"] = "click:Pass action",
        ["decline-attack"] = "click:Decline to attack",
        ["recruit-hachiro"] = "select:Matsu Hachiro;option:full cost;select:Halls of Memory;select:Border Keep;click:Recruit",
        ["end-dynasty"] = "click:End Dynasty Phase",
        ["pass-open"] = "click:Pass action",
        ["equip-armor"] = "select:Light Armor;click:Matsu Hachiro;select:Halls of Memory;click:Attach",
        ["equip-skirmishers"] = "select:Veteran Skirmishers;click:Matsu Hachiro;select:Border Keep;click:Attach",
        ["prepared-for-death"] = "select:Prepared for Death;click:Matsu Hachiro;click:Play Prepared for Death",
        ["pass-to-attack"] = "click:Pass action",
        ["declare-attack"] = "click:Declare an attack",
        ["assign-attacker"] = "select:Matsu Hachiro;selectprovince:1/0;click:Confirm assignments",
        ["engage"] = "click:Fight at province 1",
        ["incapacitated"] = "select:Incapacitated;click:Daidoji Kenshi;click:Play Incapacitated",
        ["pass-battle"] = "click:Pass battle action",
    };


    /// <summary>Plays lesson 1 start to finish with the controls a player uses: the coach's button for notes, the table's own buttons and cards for every move.</summary>
    private async Task CheckTutorialUi()
    {
        try
        {
            var seen = new List<string>();
            for (var guard = 0; guard < 400 && _session.Tutorial is { Finished: false } lesson; guard++)
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
                await WaitTutorialRest();
                foreach (var token in script.Split(';')) await RunTutorialUiToken(token);
                for (var wait = 0; wait < 60 && _session.Tutorial is { Finished: false } still && still.Coach.StepId == coach.StepId; wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                if (_session.Tutorial is { Finished: false } after && after.Coach.StepId == coach.StepId)
                    throw new InvalidOperationException($"{coach.StepId}: the step did not advance. {TutorialControls()} notice={_status.Text}");
            }
            if (_session.Tutorial is { Finished: false }) throw new InvalidOperationException("The lesson did not finish.");
            var honor = View.Players[0].Honor;
            // Back: a note's Back returns to the note before it, and a move's Back to the scene before the move (the move is undone).
            StartTutorial(); for (var i = 0; i < 6; i++) await CoverageFrames();
            await PlayTutorialTo("pass-open"); await WaitTutorialRest();
            var turnOneHonor = View.Players[0].Honor;
            await RunTutorialUiToken("back:crane-passed");
            await PlayTutorialTo("equip-armor"); await WaitTutorialRest();
            await RunTutorialUiToken("back:housekeeping");
            if (View.Players[0].Honor != turnOneHonor || View.ActivePlayerId != 0) throw new InvalidOperationException("Going back did not restore the table.");
            // Leaving at any moment: start again, get part way in, and leave through the table menu.
            StartTutorial(); for (var i = 0; i < 6; i++) await CoverageFrames();
            await PlayTutorialTo("recruit-hachiro");
            await RunTutorialUiToken("menuleave"); for (var i = 0; i < 6; i++) await CoverageFrames();
            if (_session.Tutorial is not null || !MenuVisible) throw new InvalidOperationException("Leaving the tutorial from the table menu did not return to the main menu.");
            GD.Print($"TUTORIAL_UI_CHECK: completed {seen.Count} steps; Honor {honor}; left mid-lesson through the table menu");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("TUTORIAL_UI_CHECK failed: " + e.Message); GetTree().Quit(1); }
    }

    /// <summary>Plays the lesson as a player would, through the table's own cards and buttons, until the named step is showing.</summary>
    private async Task<bool> PlayTutorialTo(string stepId)
    {
        for (var guard = 0; guard < 400 && _session.Tutorial is { Finished: false } lesson; guard++)
        {
            if (lesson.Coach.StepId == stepId) return true;
            if (lesson.Coach.Button is not null) { _session.TutorialContinue(); await CoverageFrames(); continue; }
            if (!TutorialUiScripts.TryGetValue(lesson.Coach.StepId, out var script)) return false;
            var before = lesson.Coach.StepId;
            await WaitTutorialRest();
            foreach (var token in script.Split(';')) await RunTutorialUiToken(token);
            for (var wait = 0; wait < 60 && _session.Tutorial is { Finished: false } still && still.Coach.StepId == before; wait++) await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
            if (_session.Tutorial is { Finished: false } after && after.Coach.StepId == before) { GD.Print($"TUTORIAL_UI the step {before} did not advance. {TutorialControls()} notice={_status.Text}"); return false; }
        }
        return _session.Tutorial?.Coach.StepId == stepId;
    }
}
