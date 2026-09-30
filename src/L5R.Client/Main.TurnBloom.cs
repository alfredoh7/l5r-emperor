using Godot;
namespace L5R.Client;

public partial class Main
{
    private readonly TurnBloomGate _turnBloomGate = new();
    private TurnBloom _turnBloom = null!;
    private void InitializeTurnBloom()
    {
        _turnBloom = new TurnBloom { ZIndex = 80, Observe = ObserveTurnBloom };
        _root.AddChild(_turnBloom);
    }
    private void ObserveTurnBloom()
    {
        _turnBloom.ReducedMotion = _reducedMotion;
        // Menu/dialogs remain usable. A menu interruption cancels instead of pausing the ink.
        if (_provinceReviewView is not null || _battlefieldReviewView is not null || MatchDuelOpen || MenuVisible || _session.Snapshot is null || View.WinnerId is not null ||
            _session.Online && (!_session.Connected || (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false)))
        { _turnBloom.Cancel(); return; }
        float width = Math.Min(1120, _tableViewport.Size.X * .84f);
        _turnBloom.Size = new Vector2(width, width / 3);
        _turnBloom.Position = _tableViewport.Position + (_tableViewport.Size - _turnBloom.Size) / 2;
        if (_turnBloomGate.Observe(View, _session.Snapshot.RoomCode ?? "practice")) _turnBloom.Play();
        if (View.ActivePlayerId != View.ViewerId) _turnBloom.Cancel();
    }
}
