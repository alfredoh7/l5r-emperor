using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Issue #660: native probe for the "Paused Animation" setting. Real engine events (Hotako's lock, used by the opponent) drive the showcase; extra beats are
// queued directly to cover the second/third play of a match. Modes: published-paused-animation-normal / -reduced.
public partial class Main
{
    private bool _pausedCaptureStarted;
    private string _pausedHotako = "", _pausedHero = "";
    private void ProcessPausedAnimationCapture(double delta)
    {
        _captureClock += delta; if (_pausedCaptureStarted || _captureClock < 1.5) return;
        _pausedCaptureStarted = true; _ = CapturePausedAnimation();
    }
    private async Task PreparePausedFixture(bool opponentPlays)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var hotako = SupportFixtureCard(state, 1726, 1, Zone.Home); _pausedHotako = hotako.Id;
        var hero = SupportFixtureCard(state, 10292, 0, Zone.Home); _pausedHero = hero.Id;
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        if (!opponentPlays) return;
        await _session.Submit(Actions.Single(a => a.Enabled && a.Type == "pass").Command!); await WaitDecisionPresentation();
        var ability = engine.QueryActions(1).Single(a => a.Enabled && a.Command?.AbilityId == "hotako-fear-lock" && a.CardId == hotako.Id);
        await _session.Submit(ability.Command! with { TargetId = hero.Id });
    }
    private Button? ProceedButton => _playCurrent is { } stage && IsInstanceValid(stage) ? stage.GetNodeOrNull<Button>("ProceedButton") : null;
    private Label? ProceedHint => _playCurrent is { } stage && IsInstanceValid(stage) ? stage.GetNodeOrNull<Label>("ProceedHint") : null;
    private async Task WaitFor(Func<bool> condition, string failure, double seconds = 8)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 20) { if (condition()) return; await EventFrames(3); }
        throw new Exception(failure);
    }
    private static void PausedKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }
    private async Task PausedMouse(Vector2 point, bool click)
    {
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(4);
        if (!click) return;
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false });
    }
    private void PausedBeat(string caption) { _playQueue.Enqueue(new PlayBeat(FindCard(_pausedHotako)!, caption, false)); if (_playCurrent is null || !IsInstanceValid(_playCurrent)) StartNextCardPlay(); }
    private async Task CapturePausedAnimation()
    {
        try
        {
            var reduced = _captureMode.Contains("reduced");
            _preferences.ReducedMotion = reduced; _preferences.PausedAnimation = false; ApplyPreferences(false);
            // Off: the original timing, no button, nothing held.
            await PreparePausedFixture(true);
            await WaitFor(() => _playCurrent is not null, "Opponent's ability was not showcased");
            SupportRequire(!_playHeld && ProceedButton is null && Actions.Length > 0, "Paused Animation Off held the card or blocked input");
            await WaitFor(() => !PlayShowcaseBusy, "Paused Animation Off did not finish by itself");
            SupportRequire(ProceedButton is null && !_playHeld, "Paused Animation Off left a Proceed button");

            // On: the same play stops fully shown with Proceed below the card, and the local player cannot act.
            _preferences.PausedAnimation = true;
            await PreparePausedFixture(true);
            await WaitFor(() => ProceedButton is not null, "Paused Animation On showed no Proceed button");
            await EventFrames(10);
            var button = ProceedButton!; var stage = _playCurrent!;
            var face = stage.GetChildren().OfType<Control>().First(c => c is not Label && c.Name != "ProceedButton");
            SupportRequire(_playHeld && stage.Modulate.A > .99f, "Card was not fully displayed while held");
            SupportRequire(button.GetGlobalRect().Position.Y >= face.GetGlobalRect().End.Y - 2 && GetViewport().GetVisibleRect().Encloses(button.GetGlobalRect()), "Proceed is not below the card inside the window");
            SupportRequire(button.HasFocus(), "Proceed did not take keyboard focus");
            SupportRequire(Actions.Length == 0 && !DecisionCanSubmit, "Local actions were not blocked while held");
            var before = View.Version; SelectCard(FindCard(_pausedHero)!); SupportRequire(_selected is null, "A card could be selected while held");
            await Submit(new GameCommand("pass", 0, View.Version)); SupportRequire(View.Version == before, "A command was submitted while held");
            await EventFrames(reduced ? 150 : 250); // far longer than the original hold + outro
            SupportRequire(ProceedButton == button && _playHeld, "Held card was dismissed by the clock");
            await SupportCapture("-held");
            // First hover in the match shows the reminder; it never blocks the click.
            SupportRequire(!ProceedHint!.Visible && !_proceedHintShown, "Reminder shown before any hover");
            await PausedMouse(button.GetGlobalRect().GetCenter(), false);
            SupportRequire(ProceedHint!.Visible && _proceedHintShown && _notice.Text.Contains("Settings"), "First hover did not remind the player about Settings");
            await SupportCapture("-hint");
            button.GrabFocus(); // the capture helper releases focus to move the pointer away
            // Enter proceeds, once, and the turn resumes.
            PausedKey(Key.Enter);
            await WaitFor(() => !_playHeld, "Enter did not dismiss the held card");
            await WaitFor(() => !PlayShowcaseBusy, "Animation did not finish after Proceed");
            await EventFrames(6);
            SupportRequire(Actions.Length > 0 && DecisionCanSubmit && View.Version == before, "Turn did not resume after Proceed");

            // Same match, second play: the reminder does not return. Click Proceed with the mouse.
            PausedBeat("Second play");
            await WaitFor(() => ProceedButton is not null, "Second beat was not held");
            await EventFrames(10);
            await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), false);
            SupportRequire(!ProceedHint!.Visible, "Reminder repeated within the same match");
            await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), true);
            await WaitFor(() => !_playHeld, "Clicking Proceed did not release input");
            await WaitFor(() => !PlayShowcaseBusy, "Animation did not finish after the click");

            // Queued plays are held one at a time; input stays blocked between them. Esc also proceeds.
            PausedBeat("First of two"); PausedBeat("Second of two");
            await WaitFor(() => ProceedButton is not null, "Queued beat was not held");
            await EventFrames(10);
            PausedKey(Key.Escape);
            await EventFrames(3); SupportRequire(_playHeld && !MenuVisible, "Esc released input with another card still waiting, or opened the menu");
            await WaitFor(() => ProceedButton is { Visible: true } && _playQueue.Count == 0, "Second queued beat was not held");
            await EventFrames(10);
            PausedKey(Key.Enter);
            await WaitFor(() => !_playHeld && !PlayShowcaseBusy, "Queued plays did not finish");

            // A new match arms the reminder again.
            await PreparePausedFixture(false);
            SupportRequire(!_proceedHintShown, "Reminder was not re-armed for a new match");
            PausedBeat("New match"); await WaitFor(() => ProceedButton is not null, "New match beat was not held"); await EventFrames(10);
            await PausedMouse(ProceedButton!.GetGlobalRect().GetCenter(), false);
            SupportRequire(ProceedHint!.Visible, "Reminder did not return in a new match");
            PausedKey(Key.Enter); await WaitFor(() => !PlayShowcaseBusy, "New match beat did not finish");
            // Leaving the match clears a held card.
            PausedBeat("Left behind"); await WaitFor(() => ProceedButton is not null, "Beat was not held"); await EventFrames(10);
            ClearCardPlays(); await EventFrames(4);
            SupportRequire(!_playHeld && !PlayShowcaseBusy && _playProceed is null, "Clearing the match left a held card");
            _preferences.PausedAnimation = false;
            GD.Print($"PAUSED_ANIMATION_PASS: Off keeps the original timing; On holds the played card with Proceed, blocks local actions, Enter/click/Esc proceed once, queued plays held in turn, reminder once per match; reduced={reduced}."); GetTree().Quit();
        }
        catch (Exception e) { try { await SupportCapture("-failure"); } catch { } GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
