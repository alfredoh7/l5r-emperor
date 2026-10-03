using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool ClarityFixtureReady()
    {
        if (!_captureMode.Contains("clarity", StringComparison.Ordinal)) return true;
        var seat = _captureMode.Contains("other", StringComparison.Ordinal) ? 1 - View.ViewerId : View.ViewerId;
        var player = View.Players.Single(p => p.Id == seat);
        if (_captureMode.Contains("hand", StringComparison.Ordinal)) return player.HandCount >= (_captureMode.Contains("neighbor", StringComparison.Ordinal) ? 2 : 1);
        if (_captureMode.Contains("expanded", StringComparison.Ordinal)) return View.Players.Any(p => p.Army.Any(c => c.AttachedTo is not null));
        return player.Home.Any(c => c.Definition?.Type == CardType.Personality && (!_captureMode.Contains("bowed", StringComparison.Ordinal) || c.Bowed));
    }
    private CardTile? _clarityCard, _clarityNeighbor;
    private long _clarityVersion;
    private string? _clarityError;
    private bool _clarityInputDone;
    private double _clarityReadyAt;
    private Vector2 _clarityPointer;
    private void MaintainClarityPointer()
    {
        if (!_clarityInputDone || _clarityError is not null) return;
        GetViewport().PushInput(new InputEventMouseMotion { Position = _clarityPointer, GlobalPosition = _clarityPointer }, true);
    }
    private async Task PrepareClarityCapture()
    {
        try
        {
            // Assignment captures leave a draft open. Explicitly cancel it before
            // testing unrelated hand inspection; the production misclick guard stays intact.
            if (_chosen is not null) CancelChoice();
            if (_captureMode.Contains("other", StringComparison.Ordinal)) { _session.SwitchPracticeSeat(); _uiQueue.Clear(); Render(); }
            if (!_captureMode.Contains("hand", StringComparison.Ordinal) && !_captureMode.Contains("expanded", StringComparison.Ordinal)) FocusMySide();
            await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            _clarityVersion = View.Version;
            var hand = _captureMode.Contains("hand", StringComparison.Ordinal);
            _clarityCard = hand ? (_captureMode.Contains("last", StringComparison.Ordinal) ? _tiles.Last(t => t.Card.Zone == Zone.Hand) : _tiles.First(t => t.Card.Zone == Zone.Hand)) :
                _captureMode.Contains("expanded", StringComparison.Ordinal) ? _tiles.First(t => _unitDetail.IsAncestorOf(t) && (_captureMode.Contains("parent", StringComparison.Ordinal) ? t.Card.AttachedTo is null : t.Card.AttachedTo is not null)) :
                _tiles.First(t => t.Card.OwnerId == Self.Id && t.Card.Zone == Zone.Home && t.Card.Definition?.Type == CardType.Personality && (!_captureMode.Contains("bowed", StringComparison.Ordinal) || t.Card.Bowed));
            GetWindow().Title = "L5R UI clarity check";
            GetWindow().GrabFocus();
            for (var attempt = 0; !GetWindow().HasFocus() && attempt < 100; attempt++)
                await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            if (_captureMode.Contains("neighbor", StringComparison.Ordinal))
            {
                _clarityNeighbor = _tiles.Where(t => t.Card.Zone == Zone.Hand).Skip(1).First();
                var neighbor = ScreenCenter(_clarityNeighbor);
                GetViewport().PushInput(new InputEventMouseMotion { Position = neighbor, GlobalPosition = neighbor }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = neighbor, GlobalPosition = neighbor, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = neighbor, GlobalPosition = neighbor, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            }
            var point = ScreenCenter(_clarityCard);
            if (_captureMode.Contains("edge", StringComparison.Ordinal))
            {
                var corners = _clarityCard.FaceCornersOnViewport();
                point = (corners[2] + corners[3]) / 2 + new Vector2(0, -2);
            }
            GetViewport().WarpMouse(point);
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = Vector2.One }, true);
            if (_captureMode.Contains("picked", StringComparison.Ordinal))
            {
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                ReleaseTableFocus();
                GetViewport().WarpMouse(new Vector2(125,140));
                GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(125,140), GlobalPosition = new Vector2(125,140) }, true);
            }
            _clarityPointer = _captureMode.Contains("picked", StringComparison.Ordinal) ? new Vector2(125,140) : point;
            _clarityInputDone = true; _clarityReadyAt = _captureClock;
            GD.Print($"CLARITY_INPUT: point={point}, target={_clarityCard.GetPath()}, tablehover={TableHovered?.GetPath()}, roothover={GetViewport().GuiGetHoveredControl()?.GetPath()}, focus={GetWindow().HasFocus()}");
        }
        catch (Exception e) { _clarityError = e.Message; _clarityInputDone = true; _clarityReadyAt = _captureClock; }
    }

    private void ValidateTurnAndClarity()
    {
        if (View.WinnerId is null)
        {
            if (_timeline.TurnCue.YourAction != (View.PriorityPlayerId == View.ViewerId)) throw new InvalidOperationException("Action cue disagrees with engine priority.");
            if (_timeline.TurnCue.TurnText != (View.ActivePlayerId == View.ViewerId ? "YOUR TURN" : "OPPONENT'S TURN")) throw new InvalidOperationException("Turn cue confuses priority with turn owner.");
            if (_clanHud.GetChildren().OfType<ClanPlaque>().Count(p => p.TurnOwner) != 1) throw new InvalidOperationException("Exactly one turn-owner plaque must glow.");
        }
        if (_timeline.TurnCue.Visible) throw new InvalidOperationException("Standalone turn banner must be hidden.");
        if (View.WinnerId is null && _clanHud.GetChildren().OfType<ClanPlaque>().Count(p => p.TurnOwner) != 1)
            throw new InvalidOperationException("Exactly one plaque must identify the turn owner.");
        var hand = _tiles.Where(t => t.Card.Zone == Zone.Hand).ToArray();
        if (hand.Any(t => t.FaceHeight / 228f is < 1.15f or > 1.20f)) throw new InvalidOperationException("Hand enlargement is outside requested 15–20%.");
        if (!_captureMode.Contains("clarity", StringComparison.Ordinal)) return;
        if (_clarityError is not null || !_clarityInputDone || _clarityCard is null) throw new InvalidOperationException(_clarityError ?? "Clarity input incomplete");
        if (_clarityCard.Card.Zone == Zone.Hand)
        {
            var bounds = _clarityCard.FaceCornersOnViewport().Aggregate(new Rect2(_clarityCard.FaceCornersOnViewport()[0], Vector2.Zero), (r,p) => r.Expand(p));
            if (!GetViewport().GetVisibleRect().Grow(1).Encloses(bounds) || bounds.Intersects(_inspectorPanel.GetGlobalRect()))
                throw new InvalidOperationException("Enlarged hand face clips the window or inspector.");
        }
        if (_clarityNeighbor is not null && (!_clarityNeighbor.IsSelected || _clarityCard.ZIndex <= _clarityNeighbor.ZIndex))
            throw new InvalidOperationException("Hovered hand card must rise above its selected/focused neighbor without losing selection.");
        if (View.Version != _clarityVersion) throw new InvalidOperationException("Inspection or selection submitted a game command.");
        if (_captureMode.Contains("picked", StringComparison.Ordinal))
        {
            if (!_clarityCard.IsSelected) throw new InvalidOperationException("Click did not persist selected feedback.");
        }
        else if (!_clarityCard.IsInspectionHover || _clarityCard.InteractionScale < (_clarityCard.Card.Zone == Zone.Hand ? 1.21 : 1.05))
            throw new InvalidOperationException($"Hover failed to enlarge its card: hover={_clarityCard.IsInspectionHover}, scale={_clarityCard.InteractionScale}, tablehover={TableHovered?.GetPath()}, roothover={GetViewport().GuiGetHoveredControl()?.GetPath()}, pointer={GetViewport().GetMousePosition()}, last={_lastTableMouse}, focus={GetWindow().HasFocus()}");
        GD.Print($"CLARITY_PASS: {_timeline.TurnCue.TurnText} / {_timeline.TurnCue.ActionText}; {_clarityCard.Card.Zone}, scale={_clarityCard.InteractionScale}, selected={_clarityCard.IsSelected}; match unchanged.");
    }
}
