using Godot;
namespace L5R.Client;

public partial class DuelScreen
{
    // The host may supply one continuous table entrance instead of the demo's standalone introduction.
    public bool ExternalEntrance {get;set;}
    internal Vector2[] EntranceQuad(string id)=>_tiles[id].FaceCornersOnViewport();
    internal void SetEntranceCovered(bool covered)
    {
        _uprights.Visible=!covered;
        _hud.Visible=!covered;
        foreach(var tile in _tiles.Values.Where(t=>t.Card.Zone==L5R.Core.Zone.Hand))tile.Visible=!covered;
    }
    internal void RevealEntranceControls()
    {
        _hud.Show();
        foreach(var tile in _tiles.Values.Where(t=>t.Card.Zone==L5R.Core.Zone.Hand))tile.Show();
    }
}
