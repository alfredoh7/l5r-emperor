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
            var b = Standards(_root).First(b => b.IsVisibleInTree() && _tableViewport.GetGlobalRect().Encloses(b.GetGlobalRect()));
            b.PositionAtCard(); var version = View.Version;
            var p = b.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position=p });
            Input.ParseInputEvent(new InputEventMouseButton { Position=p,ButtonIndex=MouseButton.Left,Pressed=true });
            Input.ParseInputEvent(new InputEventMouseButton { Position=p,ButtonIndex=MouseButton.Left,Pressed=false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_selected?.Id != b.Card.Id || View.Version != version) throw new Exception("Banner click failed or mutated the match");
            _selected=null; RenderInspector(); Highlight();
            GetViewport().WarpMouse(new Vector2(110,135));
            Input.ParseInputEvent(new InputEventMouseMotion { Position=new Vector2(110,135) });
            if (_captureMode.Contains("tip", StringComparison.Ordinal))
            {
                GetWindow().GrabFocus();
                await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
                b.PositionAtCard(); p = b.GetGlobalRect().GetCenter();
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
            if(!expected.SequenceEqual(UnitBadges.OfType<UnitStandard>().Select(b=>b.Card.Id).Order())) throw new Exception("Missing or duplicate unit standard");
        }
        foreach(var b in standards)
        {
            b.PositionAtCard();
            if(b.Card.UnitForce!=b.CardAnchor.Card.UnitForce) throw new Exception("Banner value differs from projected view");
            var rect=b.GetGlobalRect();
            if(!b.Inline && _cameraFitted && !_tableViewport.GetGlobalRect().Grow(2).Encloses(rect)) throw new Exception("Banner clipped in fitted view");
            Vector2[] polygon=[rect.Position,new(rect.End.X,rect.Position.Y),rect.End,new(rect.Position.X,rect.End.Y)];
            foreach(var c in _tiles.Where(c=>b.Inline ? _unitDetail.IsAncestorOf(c) : _world.IsAncestorOf(c)))
                if(Geometry2D.IntersectPolygons(polygon,c.FaceCornersOnViewport()).Count>0) throw new Exception($"Banner {b.Card.Id} overlaps {c.Card.Id}");
            foreach(var other in standards.Where(o=>o!=b)) if(rect.Intersects(other.GetGlobalRect())) throw new Exception("Banners collide");
            foreach(var t in InspectableTokens.Where(t=>t.IsVisibleInTree())) if(rect.Intersects(TokenVisualRect(t))) throw new Exception("Banner covers Force/Chi token");
        }
        if(_captureMode.Contains("standards") && (!_standardProbeDone || _standardProbeError is not null)) throw new Exception(_standardProbeError ?? "Native banner input incomplete");
        GD.Print($"UNIT_STANDARD_PASS: {standards.Length} server-bound banners; no card/token occlusion, clipping or collisions; nativeClick={_standardProbeDone}.");
    }
}
