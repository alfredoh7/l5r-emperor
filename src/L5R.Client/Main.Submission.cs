using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _submissionInFlight, _submissionPointerKnown, _suppressSubmissionRelease;
    private long _submissionToken, _submissionGeneration = -1;
    private string? _submissionMatch;
    private int _submissionViewer = -1;
    private ulong _lastDecisionPointerAt, _submissionDoubleClickUntil;
    private Vector2 _lastDecisionPointer, _submissionPointer;

    private void ObserveDecisionSession(SessionSnapshot? snapshot)
    {
        if (_submissionMatch != _session.MatchKey || _submissionGeneration != _session.Generation ||
            snapshot is not null && _submissionViewer != snapshot.View.ViewerId)
        {
            _submissionMatch = _session.MatchKey;
            _submissionGeneration = _session.Generation;
            _submissionViewer = snapshot?.View.ViewerId ?? -1;
            _submissionToken++;
            _submissionInFlight = _submissionPointerKnown = _suppressSubmissionRelease = false;
            ResetReactionFlow();
        }
        if (snapshot is null) ResetReactionFlow();
        else ObserveReactionFlow(snapshot.View);
    }

    // A new decision can occupy the old button's position immediately. Consume the
    // second half of the same double click, while allowing a fresh deliberate click.
    // Confirmation keys never repeat game commands while a key is held down.
    private bool GuardRepeatedSubmissionInput(InputEvent input)
    {
        if (MenuVisible || DuelDemoOpen || PracticeVisible || MatchDuelOpen || _session.Snapshot is null) return false;
        if (input is InputEventKey { Pressed: true, Echo: true } key && key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
        {
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return false;
        var now = Time.GetTicksMsec();
        if (!mouse.Pressed && _suppressSubmissionRelease)
        {
            _suppressSubmissionRelease = false;
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (mouse.Pressed && mouse.DoubleClick && _submissionPointerKnown && now <= _submissionDoubleClickUntil &&
            mouse.Position.DistanceTo(_submissionPointer) <= 16)
        {
            _suppressSubmissionRelease = true;
            GetViewport().SetInputAsHandled();
            return true;
        }
        _lastDecisionPointer = mouse.Position;
        _lastDecisionPointerAt = now;
        return false;
    }

    private void RememberSubmissionInput()
    {
        var now = Time.GetTicksMsec();
        _submissionPointerKnown = now >= _lastDecisionPointerAt && now - _lastDecisionPointerAt < 100;
        if (!_submissionPointerKnown) return;
        _submissionPointer = _lastDecisionPointer;
        _submissionDoubleClickUntil = now + 700;
    }
}
