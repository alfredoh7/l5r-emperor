using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #993: click-and-drag on the table, always IN ADDITION to the clicks and inspector controls that already do the same thing.
// A drag only performs the click sequence a player could make by hand (choose the unit, then its battlefield/province or target), so
// legality stays with the engine: the legal sets are the ones the engine already lists (ActionOption.TargetIds / Provinces / MoveTo).
//   * Maneuvers (Infantry and Cavalry assignment): drag a unit onto a battlefield box or a province to assign it; drag a unit that is already
//     in the draft onto another battlefield to change it, or off every battlefield to send it home. Nothing is submitted until Confirm.
//   * A move the player has started (picker): drag the mover, or the chosen target, onto a legal battlefield.
//   * A move home / to the current battlefield: drag the target outside every battlefield / onto the current one.
// Esc, or releasing over nothing legal, changes nothing. A press that does not travel DragThreshold is an ordinary click.
public partial class Main
{
    private const float DragThreshold = 10f;
    private CardView? _dragCard;
    private Vector2 _dragStart;
    private bool _dragging;
    private CardTile? _dragGhost;
    private CanvasLayer? _dragLayer;
    private bool _endingDragPress;

    // Ending the table's pending press raises the tile's own click; that click belongs to the gesture that became a drag, so it is dropped.
    private void EndTablePress(InputEventMouseButton release) { _endingDragPress = true; try { _tableSurface.ReleaseMouseOutside(release); } finally { _endingDragPress = false; } }

    private ActionOption? DragAssignment(CardView card) =>
        _chosen is { Type: "attack" or "assignDefense", Enabled: true } chosen && chosen.TargetIds?.Contains(card.Id) == true ? chosen
        : Actions.FirstOrDefault(a => a.Enabled && a.Type is "attack" or "assignDefense" && a.TargetIds?.Contains(card.Id) == true);

    private bool DragMovesViaPicker(CardView card) => MovementActive
        && (MovementMoverIds().Contains(card.Id) || _chosen!.Movement!.TargetMoves && _chosen.TargetIds?.Contains(card.Id) == true);

    private bool DragMovesToFixedDestination(CardView card) => !MovementActive && View.WinnerId is null
        && _chosen is { Type: "useAbility", Enabled: true, MoveTo: MoveSpec.Home or MoveSpec.CurrentBattlefield, Command: { } command }
        && command.PlayerId == View.ViewerId && command.ExpectedVersion == View.Version && _chosen.TargetIds?.Contains(card.Id) == true;

    private bool CanDrag(CardView card) => View.WinnerId is null && !_session.Busy && !PresentationBusy && !_playHeld
        && (DragAssignment(card) is not null || DragMovesViaPicker(card) || DragMovesToFixedDestination(card));

    // The battlefield box under a screen point, in the table's world space (the same rectangles the mat draws).
    private int? BattlefieldAt(Vector2 screen)
    {
        if (!PointerOnTable(screen)) return null;
        var world = ScreenToWorld(screen);
        foreach (var (index, bounds) in _battleCourts) if (bounds.HasPoint(world)) return index;
        return null;
    }

    // A province bay under a screen point (owner, province index).
    private (int Owner, int Index)? ProvinceAt(Vector2 screen)
    {
        if (!PointerOnTable(screen)) return null;
        var world = ScreenToWorld(screen);
        foreach (var section in _matTargets)
        {
            if (!section.Id.StartsWith("province:") || !section.Bounds.HasPoint(world)) continue;
            var part = section.Id.Split(':');
            if (part.Length == 3 && int.TryParse(part[1], out var owner) && int.TryParse(part[2], out var index)) return (owner, index);
        }
        return null;
    }

    // Where a battle drop lands: a battlefield box wins over a province bay. Battlefield is true for a box.
    private (int Owner, int Index, bool Battlefield)? DragDestination(Vector2 screen)
    {
        if (BattlefieldAt(screen) is { } battlefield) return (1 - View.ActivePlayerId, battlefield, true);
        return ProvinceAt(screen) is { } province ? (province.Owner, province.Index, false) : null;
    }

    private void BeginDragCandidate(Control? hovered, InputEventMouseButton press)
    {
        _dragCard = null;
        if (press.ButtonIndex != MouseButton.Left || !press.Pressed) return;
        for (var control = hovered; control is not null; control = control.GetParent() as Control)
            if (control is CardTile tile) { if (CanDrag(tile.Card)) { _dragCard = tile.Card; _dragStart = press.Position; } return; }
    }

    // Returns true when the motion belongs to a drag and must not reach the table.
    private bool TableDragMotion(InputEventMouseMotion motion)
    {
        if (_dragCard is null) return false;
        if (!_dragging)
        {
            if (!motion.ButtonMask.HasFlag(MouseButtonMask.Left)) { _dragCard = null; return false; }
            if (motion.Position.DistanceTo(_dragStart) <= DragThreshold) return false;
            StartDrag();
        }
        if (_dragGhost is not null) _dragGhost.Position = motion.Position - _dragGhost.Size / 2;
        UpdateDragCue(motion.Position);
        return true;
    }

    private void StartDrag()
    {
        _dragging = true;
        _tableSurface.ExitMouse(); HideTableTooltip();
        var source = _tiles.FirstOrDefault(t => t.Card.Id == _dragCard!.Id);
        var height = 110f; var width = source is null || source.Size.Y < 1 ? 100f : height * source.Size.X / source.Size.Y;
        _dragGhost = new CardTile(_dragCard!, true, _ => { }, _ => { })
        {
            Size = new Vector2(width, height),
            FaceHeight = height,
            ShowTokenRail = false,
            ReducedMotion = true,
            Material = CardPaper.FixedTable,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, .85f),
            Name = "DragGhost"
        };
        _dragLayer = new CanvasLayer { Layer = 60 }; AddChild(_dragLayer); _dragLayer.AddChild(_dragGhost);
        Input.SetDefaultCursorShape(Input.CursorShape.Drag);
    }

    // The battlefield or province a drop would use is lit while it is legal; nothing is lit over a place that would refuse the drop.
    private void UpdateDragCue(Vector2 screen)
    {
        string? lit = null;
        if (_dragCard is { } card && DragDestination(screen) is { } place)
        {
            var legal = DragAssignment(card) is { } action ? place.Owner == 1 - View.ActivePlayerId && action.Provinces?.Contains(place.Index) == true
                : DragMovesViaPicker(card) && MovementDestination(place.Owner, place.Index);
            if (legal) lit = place.Battlefield ? $"battle:{place.Owner}:{place.Index}" : $"province:{place.Owner}:{place.Index}";
        }
        _matSections.Emphasize(lit, hover: true);
    }

    private void EndDragVisuals()
    {
        _dragging = false;
        _dragGhost = null;
        if (_dragLayer is not null && IsInstanceValid(_dragLayer)) _dragLayer.QueueFree();
        _dragLayer = null;
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        Highlight();
    }

    private void CancelDrag()
    {
        var wasDragging = _dragging; _dragCard = null;
        if (!wasDragging) return;
        EndDragVisuals();
        EndTablePress(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
    }

    // True when the release ended a drag (the table must not also see it as a click).
    private bool FinishDrag(InputEventMouseButton release)
    {
        var card = _dragCard; _dragCard = null;
        if (!_dragging) return false;
        EndDragVisuals();
        EndTablePress(release);
        if (card is not null) DropCard(card, release.Position);
        return true;
    }

    private void DropCard(CardView card, Vector2 screen)
    {
        var place = DragDestination(screen);
        if (!CanDrag(card)) return;
        if (DragAssignment(card) is { } assignment)
        {
            var defender = 1 - View.ActivePlayerId;
            if (place is { } target && target.Owner == defender)
            {
                if (assignment.Provinces?.Contains(target.Index) != true) { SetNotice($"{ShortName(card)} cannot be assigned to {BattlefieldName(target.Index)} right now."); return; }
                if (_chosen is not { Type: var type } || type != assignment.Type) Choose(assignment);
                _selected = card; _assignmentUnit = card.Id;
                SelectProvince(defender, target.Index);
                return;
            }
            if (place is null && _assignments.Remove(card.Id))
            {
                _selected = card; _assignmentUnit = card.Id;
                SetNotice($"{ShortName(card)} stays home. Confirm assignments when ready.");
                RenderBoard(); RenderInspector(); Highlight();
            }
            return;
        }
        if (DragMovesViaPicker(card))
        {
            if (place is { } destination && MovementDestination(destination.Owner, destination.Index))
            {
                if (_chosen!.Movement!.TargetMoves && _chosen.TargetIds?.Contains(card.Id) == true && _chosen.Command!.TargetId != card.Id) ChooseTarget(card.Id);
                ChooseMovementProvince(destination.Owner, destination.Index);
            }
            else SetNotice("Drop it on a highlighted battlefield, or press Esc to keep it where it is.");
            return;
        }
        if (DragMovesToFixedDestination(card))
        {
            var battlefield = BattlefieldAt(screen);
            if (_chosen!.MoveTo == MoveSpec.Home ? battlefield is null : battlefield is { } at && at == View.CurrentBattleProvince)
            {
                if (_chosen.Command!.TargetId != card.Id) ChooseTarget(card.Id);
                SetNotice($"{ShortName(card)} → {(_chosen.MoveTo == MoveSpec.Home ? "home" : "the current battlefield")}. Confirm when ready.");
            }
            else SetNotice(_chosen.MoveTo == MoveSpec.Home ? $"Drop {ShortName(card)} outside the battlefield to send it home." : $"Drop {ShortName(card)} on the current battlefield.");
        }
    }
}
