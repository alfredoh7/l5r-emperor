using Godot;
using L5R.Core;
namespace L5R.Client;
public partial class Main
{
    // Explicit visual fixture: copies of already-public cards. Never submitted to a match.
    // Core Discipline tests cover actual removal, visibility and terminal legality.
    private static IEnumerable<Node> Descendants(Node node) => node.GetChildren().SelectMany(c=>new[]{c}.Concat(Descendants(c)));
    private void PrepareRemovedCapture()
    {
        var original=_session.Snapshot!;
        _reducedMotion=true;
        var definitions=original.View.Players.SelectMany(p=>p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.Hand).Concat(p.Provinces.Select(v=>v.Card).OfType<CardView>())).Where(c=>c.Definition is not null).ToArray();
        var dynasty=definitions.First(c=>c.Definition!.Type==CardType.Personality);
        var fate=definitions.First(c=>c.Definition!.Type==CardType.Strategy);
        var players=original.View.Players.Select(p=>p with {RemovedFromGame=Enumerable.Range(0,p.Id==original.View.ViewerId?4:2).Select(i=>(i%2==0?dynasty:fate) with {Id=$"removed-fixture-{p.Id}-{i}",OwnerId=p.Id,Zone=Zone.RemovedFromGame,Bowed=false,AttachedTo=null}).ToArray()}).ToArray();
        _presentationSnapshot=original with {View=original.View with {Players=players},Actions=[]};
        RenderBoard();ResetTableCamera();
        if(_removedBounds.Count!=2 || _pileHud.GetChildren().OfType<TablePile>().Count(p=>p.PileZone==Zone.RemovedFromGame)!=2)throw new Exception("Removed pile fixture missing owner pile.");
        var occupied=WorldWidth;
        _presentationSnapshot=original with {View=original.View with {Players=players.Select(p=>p with {RemovedFromGame=[]}).ToArray()},Actions=[]};
        RenderBoard();
        if(_removedBounds.Count!=0 || _matTargets.Any(t=>t.Id.StartsWith("removed:")))throw new Exception("Empty removed piles left a placeholder.");
        _presentationSnapshot=original with {View=original.View with {Players=players},Actions=[]};RenderBoard();ResetTableCamera();
        if(WorldWidth!=occupied)throw new Exception("Removed layout is not stable.");
        if(_captureMode.Contains("gallery"))
        {
            ShowRemovedGallery(View.ViewerId);
            var dialog=GetChildren().OfType<AcceptDialog>().Last();
            var tab=dialog.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith(players.Single(p=>p.Id!=View.ViewerId).Clan+" ("));
            tab.EmitSignal(Button.SignalName.Pressed);
            if(Descendants(dialog).OfType<CardTile>().Count()!=2)throw new Exception("Owner tab did not switch removed cards.");
            var local=dialog.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith(Self.Clan+" ("));local.EmitSignal(Button.SignalName.Pressed);
        }
        if(_session.Snapshot!=original)throw new Exception("Inspection mutated session.");
        GD.Print("REMOVED_UI_PASS: two public mixed piles; no empty placeholders; stable layout; read-only owner tabs. Visual fixture, not a simulated game command.");
    }
}
