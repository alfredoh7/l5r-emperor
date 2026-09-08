using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _standardProbeDone;
    private string? _standardProbeError;
    private IEnumerable<UnitStandard> Standards(Node node) => node.GetChildren().SelectMany(n => n is UnitStandard b ? new[] { b } : Standards(n));
    private async void ProbeUnitStandardInput()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // The main-menu branch closes Options through two deferred pages.
            // Wait for the table to be interactable instead of racing that transition.
            for (var frames = 0; frames < 120 && (!_root.IsVisibleInTree() || GetChildren().OfType<Window>().Any(w => w.Visible)); frames++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetWindow().GrabFocus();
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            var b = Standards(_root).First(b => b.IsVisibleInTree() && _tableViewport.GetGlobalRect().Encloses(b.BodyRectOnViewport()));
            b.PositionAtCard(); var version = View.Version;
            var p = b.BodyRectOnViewport().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position=p });
            Input.ParseInputEvent(new InputEventMouseButton { Position=p,ButtonIndex=MouseButton.Left,Pressed=true });
            Input.ParseInputEvent(new InputEventMouseButton { Position=p,ButtonIndex=MouseButton.Left,Pressed=false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_selected?.Id != b.Card.Id || View.Version != version) throw new Exception($"Banner click failed: selected={_selected?.Id}, expected={b.Card.Id}, version={View.Version}/{version}, hover={TableHovered?.GetPath()}, point={p}, rect={b.BodyRectOnViewport()}, blocked={CameraBlocked}");
            _selected=null; RenderInspector(); Highlight();
            GetViewport().WarpMouse(new Vector2(110,135));
            Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(110,135) });
            if (_captureMode.Contains("tip", StringComparison.Ordinal))
            {
                GetWindow().GrabFocus();
                await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
                b.PositionAtCard(); p = b.BodyRectOnViewport().GetCenter();
                GetViewport().WarpMouse(p);
                GetViewport().PushInput(new InputEventMouseMotion { Position=p, GlobalPosition=p }, true);
                await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
                if (_tableHover is not UnitStandard || _tableTooltip is null || ReadingTooltip.LastText?.Contains("Click the standard") != true)
                    throw new Exception($"Native banner tooltip failed: native={GetViewport().GuiGetHoveredControl()?.GetType().Name}, table={_tableHover?.GetType().Name}, popup={_tableTooltip is not null}, pointer={_lastTableMouse}, expected={p}, text={ReadingTooltip.LastText}");
            }
            _standardProbeDone=true;
        }
        catch(Exception e) { _standardProbeError=e.ToString(); _standardProbeDone=true; }
    }
    private void ValidateUnitStandards()
    {
        var standards = Standards(_root).Where(b=>b.IsVisibleInTree()).ToArray();
        if (_expandedUnits.Count==0)
        {
            var expected=View.Players.SelectMany(p=>p.Home.Concat(p.Army)).Where(c=>c.UnitForce.HasValue).Select(c=>c.Id).Order();
            if(!expected.SequenceEqual(Standards(_root).Select(b=>b.Card.Id).Order())) throw new Exception("Missing or duplicate unit standard");
        }
        foreach(var b in standards)
        {
            b.PositionAtCard();
            if(b.Card.UnitForce!=b.CardAnchor.Card.UnitForce) throw new Exception("Banner value differs from projected view");
            var rect=b.BodyRectOnViewport();
            if(!b.Inline && _cameraFitted && !_tableViewport.GetGlobalRect().Grow(2).Encloses(rect)) throw new Exception("Banner clipped in fitted view");
            var polygons=b.ArtworkPolygonsOnViewport().ToArray();
            var face=b.CardAnchor.FaceCornersOnViewport();
            var mountDistance=Enumerable.Range(0,4).Min(i=>Geometry2D.GetClosestPointToSegment(b.MountOnViewport,face[i],face[(i+1)%4]).DistanceTo(b.MountOnViewport));
            if(mountDistance>.8f) throw new Exception("Standard brass mount detached from Personality rim");
            foreach(var c in _tiles.Where(c=>b.Inline ? _unitDetail.IsAncestorOf(c) : _world.IsAncestorOf(c)))
                if(polygons.Any(p=>Geometry2D.IntersectPolygons(p,c.FaceCornersOnViewport()).Count>0)) throw new Exception($"Banner {b.Card.Id} overlaps {c.Card.Id}");
            foreach(var other in standards.Where(o=>o!=b)) if(polygons.Any(p=>other.ArtworkPolygonsOnViewport().Any(q=>Geometry2D.IntersectPolygons(p,q).Count>0))) throw new Exception("Banners collide");
            foreach(var t in InspectableTokens.Where(t=>t.IsVisibleInTree()))
                for(var i=0;i<2;i++)
                {
                    var center=new Vector2(t.Diameter*(.5f+i*1.12f),t.Diameter/2);
                    var disc=Enumerable.Range(0,48).Select(j=>t.GetGlobalTransform()*(center+Vector2.FromAngle(j*Mathf.Tau/48)*t.Diameter/2)).ToArray();
                    if(polygons.Any(p=>Geometry2D.IntersectPolygons(p,disc).Count>0)) throw new Exception("Banner covers Force/Chi token");
                }
        }
        if(_captureMode.Contains("standards") && (!_standardProbeDone || _standardProbeError is not null)) throw new Exception(_standardProbeError ?? "Native banner input incomplete");
        GD.Print($"UNIT_STANDARD_PASS: {standards.Length} server-bound banners; no card/token occlusion, clipping or collisions; nativeClick={_standardProbeDone}.");
    }
}
