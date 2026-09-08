using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<string,(int Force,int Chi,string Definition)> _observedStats=[];
    private readonly Dictionary<string,(int Force,int Chi,ulong At)> _statChanges=[];
    private long _statVersion=-1;
    private void ResetStatHistory(){_observedStats.Clear();_statChanges.Clear();_statVersion=-1;}
    private void ObserveStatChanges()
    {
        if(View.Version<_statVersion)ResetStatHistory();
        if(View.Version==_statVersion)return;
        var cards=View.Players.SelectMany(p=>p.Home.Concat(p.Army)).Where(c=>c.Definition?.Type==CardType.Personality).ToArray();
        foreach(var c in cards)
        {
            if(_observedStats.TryGetValue(c.Id,out var old)&&old.Definition==c.Definition!.Id&&(old.Force!=c.Force||old.Chi!=c.Chi))
                _statChanges[c.Id]=(old.Force,old.Chi,Time.GetTicksMsec());
            _observedStats[c.Id]=(c.Force,c.Chi,c.Definition!.Id);
        }
        var visible=cards.Select(c=>c.Id).ToHashSet();
        foreach(var id in _observedStats.Keys.Where(id=>!visible.Contains(id)).ToArray()){_observedStats.Remove(id);_statChanges.Remove(id);}
        _statVersion=View.Version;
    }
    private void BindStatTransition(CardTile tile)
    {
        if(!_statChanges.TryGetValue(tile.Card.Id,out var change))return;
        tile.PreviousForce=change.Force;tile.PreviousChi=change.Chi;
        tile.StatChangeAge=(Time.GetTicksMsec()-change.At)/1000d;
    }
}
