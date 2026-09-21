using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private AttackSourceTab _attackTab = null!;
    private (string Room, int Viewer)? _attackTabContext;
    private long _attackTabVersion;

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
        // A live Force ribbon may belong to the host rather than this attachment.
        // Leave all live numerals visible, including on bowed/overlapping stacks.
        foreach (var badge in UnitBadges.OfType<UnitStandard>().Where(b => b.IsVisibleInTree()))
        {
            var r = badge.BodyRectOnViewport();
            var local = new Rect2(inverse*r.Position,r.Size/_root.Scale);
            if (body.Grow(2).Intersects(local)) body.Position = new Vector2(body.Position.X,local.End.Y+3);
        }
        var clip = _tableViewport.GetGlobalRect();
        var globalBody = new Rect2(_root.GetGlobalTransform()*body.Position,body.Size*_root.Scale);
        return clip.Encloses(globalBody) ? body : null;
    }
}
