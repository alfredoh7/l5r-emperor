using Godot;
namespace L5R.Client;

public partial class DuelScreen
{
    // Defer the standalone opening until the table entrance has completed.
    public bool ExternalEntrance {get;set;}
    internal void CompleteExternalEntrance(bool introduce)
    {
        ExternalEntrance=false;
        if(!introduce){_openingSeen=true;return;}
        if(Snapshot?.View.PendingDuel?.Stage=="Focusing" && Snapshot.View.LastDuel?.Result is null)
        {
            StartOpening();
            // The entrance already named the challenge. Continue with the low-angle hush and portraits.
            _cinemaTime=StakesDuration;
            RenderActions();
        }
    }
    internal void PrepareReturnBackground()
    {
        InputLocked=true;SetProcess(false);
        foreach(var child in GetChildren().OfType<CanvasItem>().Where(c=>c!=_scene))child.Hide();
        _uprights.Hide();_flights.Hide();
        foreach(var tile in _tiles.Values)tile.Hide();
    }
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
