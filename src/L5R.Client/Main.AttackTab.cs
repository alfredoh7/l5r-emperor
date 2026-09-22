using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private AttackSourceTab _attackTab = null!;
    private (string Room, int Viewer)? _attackTabContext;
    private long _attackTabVersion;
    private (string Source, string Unit)? _attackTabUnit;
    private bool _attackStandardsReserved;

    private void ObserveAttackTab()
    {
        _attackTab.ReducedMotion = _reducedMotion;
        if (MenuVisible || _session.Snapshot is null || View.WinnerId is not null ||
            _session.Online && (!_session.Connected || (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false)))
        { _attackTab.Cancel(); _attackTabContext = null; return; }
        var context = (_session.Snapshot.RoomCode ?? _session.Snapshot.Mode, View.ViewerId);
        if (_attackTabContext != context || View.Version < _attackTabVersion) _attackTab.Cancel();
        _attackTabContext = context; _attackTabVersion = View.Version;
        // Busy may precede the server's reaction view. Keep the selected cue until that
        // authoritative response arrives, rather than flickering at confirmation.
        _attackTab.Desired = AttackTargetActive ? _chosen!.Attack : View.PendingAttack ??
            (_session.Busy ? _attackTab.Desired : null);
    }

    private Rect2? AttackTabPlacement(AttackCueView cue)
    {
        var tile = _tiles.FirstOrDefault(t => t.Card.Id == cue.SourceId && TileVisible(t));
        if (tile is null || tile.Card.Zone == Zone.Hand) return null;
        var inverse = _root.GetGlobalTransform().AffineInverse();
        var face = tile.FaceCornersOnViewport().Select(p => inverse * p).ToArray();
        var edge = Enumerable.Range(0,4).MaxBy(i => (face[i].X + face[(i+1)%4].X)/2);
        var ends = new[] { face[edge],face[(edge+1)%4] }.OrderBy(p => p.Y).ToArray();
        var width = Mathf.Clamp((face.Max(p => p.X)-face.Min(p => p.X))*.24f,32,52);
        var height = width*396/562;
        var mount = ends[0].Lerp(ends[1],.07f);
        var body = new Rect2(mount,new Vector2(width,height));
        var clip = _tableSurface.GetGlobalRect();
        var globalBody = new Rect2(_root.GetGlobalTransform()*body.Position,body.Size*_root.Scale);
        return clip.Encloses(globalBody) ? body : null;
    }

    private void ArrangeAttackTab(AttackCueView? cue, Rect2 body, float clearance)
    {
        if (cue is null && !_attackStandardsReserved) return;
        var source = cue is null ? null : _tiles.FirstOrDefault(t => t.Card.Id == cue.SourceId && TileVisible(t));
        var standards = Standards(_root).ToArray();
        if (cue is null || _attackTabUnit?.Source != cue.SourceId) _attackTabUnit = null;
        if (source is not null && standards.FirstOrDefault(b => b.CardAnchor == source || b.Companions.Contains(source)) is { } host)
            _attackTabUnit = (cue!.SourceId, host.Card.Id);
        var slot = new Rect2(_root.GetGlobalTransform() * body.Position,
            (body.Size + new Vector2(0, 5)) * _root.Scale);
        // Standards live inside each projected card stack, not in the screen-space
        // Force/Chi token layer. Keep the host's whole ribbon clear for attachments too.
        foreach (var standard in standards)
        {
            var sameUnit = _attackTabUnit?.Unit == standard.Card.Id;
            standard.ReserveAttackSpace(sameUnit ? slot : null, sameUnit ? clearance : 0);
        }
        _attackStandardsReserved = cue is not null;
    }
}
