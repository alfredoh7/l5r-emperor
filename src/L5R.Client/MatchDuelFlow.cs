namespace L5R.Client;

/// <summary>Queues permitted duel snapshots, independently of animation speed or network delivery.
/// Historical results on first connection stay history; an unfinished duel resumes immediately.</summary>
public sealed class MatchDuelFlow
{
    private readonly Queue<string> _order = new();
    private readonly Dictionary<string, SessionSnapshot> _pending = new();
    private readonly HashSet<string> _finished = new();
    private string? _scope;
    private long _version = -1;
    public SessionSnapshot? Current => _order.TryPeek(out var id) ? _pending[id] : null;
    public void Observe(SessionSnapshot? snapshot)
    {
        if(snapshot is null){Reset();return;}
        var view=snapshot.View;
        var scope=$"{snapshot.RoomCode}:{view.Scenario}:{view.ViewerId}";
        if(scope!=_scope || view.Version<_version)Reset();
        var initial=_version<0;
        _scope=scope;_version=view.Version;
        if(view.LastDuel is not {} duel)return;
        if(initial && (duel.Complete||view.WinnerId is not null)){_finished.Add(duel.Id);return;}
        if(_finished.Contains(duel.Id))return;
        if(!_pending.ContainsKey(duel.Id))_order.Enqueue(duel.Id);
        _pending[duel.Id]=snapshot;
    }
    public void Complete(string id)
    {
        if(!_order.TryPeek(out var current)||current!=id)return;
        _order.Dequeue();_pending.Remove(id);_finished.Add(id);
    }
    public void Reset(){_scope=null;_version=-1;_order.Clear();_pending.Clear();_finished.Clear();}
}
