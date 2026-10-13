using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    /// <summary>What CrashReports may write about the match: the position and public event log the local viewer sees, and the seat token to scrub. Nothing hidden (issue #905).</summary>
    private (string Position, string[] Events, string[] Secrets)? CrashContext()
    {
        if (_session.Snapshot is not { } snapshot) return null;
        var view = snapshot.View;
        var position = $"phase {view.Phase}, turn {view.TurnNumber}, active seat {view.ActivePlayerId}, priority seat {view.PriorityPlayerId}, version {view.Version}, scenario {view.Scenario}, viewer seat {view.ViewerId}, online {_session.Online}";
        return (position, view.Events.Select(e => $"#{e.Sequence} {e.Message}").ToArray(), [_session.Seat?.SeatToken ?? ""]);
    }
}
