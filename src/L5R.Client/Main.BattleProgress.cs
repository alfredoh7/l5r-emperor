using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private long _battleSequence = -1;
    private string? _resolutionMessage;
    private ulong _resolutionUntil;
    private void ObserveBattleResolution()
    {
        if (View.Version < _displayedVersion) { _battleSequence = -1; _resolutionMessage = null; }
        var latest = View.Events.LastOrDefault()?.Sequence ?? 0;
        if (latest < _battleSequence) _resolutionMessage = null;
        // On a fresh connection, historical results must not replay as new events.
        if (_battleSequence >= 0 && latest >= _battleSequence)
        {
            var resolved = View.Events.LastOrDefault(e => e.Sequence > _battleSequence && e.Type == "battleResolved");
            if (resolved is not null) { _resolutionMessage = resolved.Message; _resolutionUntil = Time.GetTicksMsec() + 4500; }
        }
        _battleSequence = latest;
        // A new battle or assignment always takes precedence over the previous result.
        if (View.Phase is not (Phase.Dynasty or Phase.BattleSelection) || View.WinnerId is not null) _resolutionMessage = null;
    }
    private void ExpireBattleResolution()
    {
        if (_resolutionMessage is null || Time.GetTicksMsec() < _resolutionUntil) return;
        _resolutionMessage = null;
        if (_session.Snapshot is not null) RenderInspector();
    }
}
