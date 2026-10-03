using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private static IEnumerable<StatTokens> DescendantTokens(Node node) => node.GetChildren().SelectMany(child=>child is StatTokens token && token is not UnitStandard ? new[]{token}:DescendantTokens(child));
    private IEnumerable<StatTokens> InspectableTokens => TokenPairs.Concat(DescendantTokens(_unitDetail));
    private IEnumerable<CardTile> InlaidCards => _tiles.Where(t=>t.ShowsStatInlays);
    private bool _tokenClickChecked;
    private string? _tokenProbeError;
    private async Task ProbeStatTokenInput()
    {
        try
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            // While a unit stack is expanded the table is deliberately inert (CameraBlocked); the clickable cards are the ones in the detail overlay.
            var clickable=_expandedUnits.Count>0?InlaidCards.Where(t=>_unitDetail.IsAncestorOf(t)):InlaidCards;
            var tile=clickable.First(t=>_tableViewport.GetGlobalRect().HasPoint(t.InlayCornersOnViewport(0).Aggregate(Vector2.Zero,(a,b)=>a+b)/4));
            var version=View.Version; var point=tile.InlayCornersOnViewport(0).Aggregate(Vector2.Zero,(a,b)=>a+b)/4;
            Input.ParseInputEvent(new InputEventMouseMotion {Position=point});
            Input.ParseInputEvent(new InputEventMouseButton {Position=point,ButtonIndex=MouseButton.Left,Pressed=true});
            Input.ParseInputEvent(new InputEventMouseButton {Position=point,ButtonIndex=MouseButton.Left,Pressed=false});
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(_selected?.Id!=tile.Card.Id||View.Version!=version)throw new Exception("Inlay click did not select the correct card without submitting a command.");
            _tokenClickChecked=true;
            if(_captureMode.Contains("tokentip",StringComparison.Ordinal)) GetViewport().PushInput(new InputEventMouseMotion {Position=point,GlobalPosition=point},true);
        }
        catch(Exception e){_tokenProbeError=e.ToString();}
    }
    private void ValidateStatTokens()
    {
        if(InspectableTokens.Any())throw new Exception("External Force/Chi coins still occupy table or inspection space.");
        if(_expandedUnits.Count==0)
        {
            var expected=View.Players.SelectMany(p=>p.Home.Concat(p.Army)).Where(c=>c.Definition is {Type:CardType.Personality,ScenarioOnly:false} && c.AttachedTo is null).Select(c=>c.Id).Order();
            if(!expected.SequenceEqual(InlaidCards.Select(t=>t.Card.Id).Order()))throw new Exception("Missing or duplicate Personality stat inlays.");
        }
        foreach(var tile in InlaidCards)
            foreach(var i in new[]{0,1})
                if(tile.InlayCornersOnViewport(i).Any(p=>!Geometry2D.IsPointInPolygon(p,tile.FaceCornersOnViewport())))throw new Exception("Stat strip extends beyond card face.");
        if(_captureMode.Contains("tokens",StringComparison.Ordinal)&&(!_tokenClickChecked||_tokenProbeError is not null))throw new Exception(_tokenProbeError??"Native stat inlay input probe incomplete");
        GD.Print($"STAT_INLAY_PASS: {InlaidCards.Count()} engine-bound Personality pairs; inlays contained by full faces; no external discs; nativeClick={_tokenClickChecked}.");
    }
}
