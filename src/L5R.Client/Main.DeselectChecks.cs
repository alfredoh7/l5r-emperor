using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _deselectProbeDone;
    private string? _deselectProbeError;
    private async Task SettleInspection() => await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
    private async Task FocusInspectionProbe()
    {
        GetWindow().GrabFocus();
        for (var attempt = 0; attempt < 40 && !GetWindow().HasFocus(); attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        if (!GetWindow().HasFocus()) throw new Exception("Inspection probe could not focus the native window; no click submitted.");
    }
    private async Task ClickInspectionCard(CardView card)
    {
        await FocusInspectionProbe();
        await SettleInspection();
        var tile = _tiles.First(t => t.Card.Id == card.Id);
        var point = tile.FaceCornersOnViewport().Aggregate(Vector2.Zero, (a, b) => a + b) / 4;
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }, buffered: true);
        await ToSignal(GetTree().CreateTimer(.06), SceneTreeTimer.SignalName.Timeout);
        await FocusInspectionProbe();
        point = tile.FaceCornersOnViewport().Aggregate(Vector2.Zero, (a, b) => a + b) / 4;
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, buffered: true);
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, buffered: true);
        await SettleInspection();
        GD.Print($"DESELECT_CLICK: {card.Definition?.Name} point={point} selected={_selected?.Definition?.Name} hovered={(TableHovered as CardTile)?.Card.Definition?.Name ?? TableHovered?.Name} chosen={_chosen?.Type} focus={GetWindow().HasFocus()} table={_tableViewport.GetGlobalRect()}");
    }
    private async Task ProbeDeselection()
    {
        try
        {
            GetWindow().GrabFocus();
            if (_chosen is not null)
            {
                var draft = _chosen;
                var assignments = new Dictionary<string, int>(_assignments);
                var card = FindCard(draft.TargetIds!.First())!;
                SelectCard(card); SelectCard(card);
                // Issue #973: a second click on an assigned unit sends that unit home, so only the clicked unit's own assignment may change; the draft and every other assignment stay.
                var others = assignments.Where(x => x.Key != card.Id).OrderBy(x => x.Key);
                if (_chosen != draft || !others.SequenceEqual(_assignments.Where(x => x.Key != card.Id).OrderBy(x => x.Key)))
                    throw new Exception("Repeated assignment selection discarded the active draft.");
                CancelChoice();
            }
            _selected = null; RenderInspector(); Highlight();
            await SettleInspection();
            var originalHeader = _sidebarHeader.GetChildren().OfType<BattleTracker>().Single().GetGlobalRect();
            var version = View.Version;
            var hand = Self.Hand.First(c => c.Definition?.Type == CardType.Strategy);
            await ClickInspectionCard(hand);
            if (_selected?.Id != hand.Id) throw new Exception("Native hand click did not inspect the card.");
            var headerRect = _sidebarHeader.GetGlobalRect();
            var footerRect = _phaseActions.GetGlobalRect();
            if (!_inspectorPanel.GetGlobalRect().Encloses(headerRect) || !_inspectorPanel.GetGlobalRect().Encloses(footerRect) || headerRect.End.Y > footerRect.Position.Y)
                throw new Exception("Inspection obscured the fixed header or decision footer.");
            RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_capturePath + ".selected.png");
            await ClickInspectionCard(hand);
            if (_selected is not null || _chosen is not null) throw new Exception("Second click did not deselect the inspected hand card.");
            var board = _tiles.First(t => t.Card.Zone == Zone.Home && t.Card.Definition?.Type == CardType.Holding).Card;
            await ClickInspectionCard(board);
            if (_selected?.Id != board.Id) throw new Exception("Native board click did not inspect the Holding.");
            await ClickInspectionCard(board);
            if (_selected is not null) throw new Exception("Second click did not deselect the Holding.");
            await ClickInspectionCard(hand); await ClickInspectionCard(board);
            if (_selected?.Id != board.Id) throw new Exception("Selecting another card did not replace inspection.");
            var close = _inspector.FindChild("CloseInspection", true, false) as Button ?? throw new Exception("Visible Close control missing.");
            await FocusInspectionProbe();
            var p = close.GetGlobalRect().GetCenter();
            PushCaptureMouse(new InputEventMouseMotion { Position = p, GlobalPosition = p });
            PushCaptureMouse(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true });
            PushCaptureMouse(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false });
            await SettleInspection();
            if (_selected is not null || View.Version != version) throw new Exception("Close did not deselect without issuing a game command.");
            var tracker = _sidebarHeader.GetChildren().OfType<BattleTracker>().Single();
            var restoredHeader = tracker.GetGlobalRect();
            // Compare with the actual unselected layout. The approved progress
            // track is taller than the obsolete 120–180 px test expectation.
            if (!tracker.IsVisibleInTree() || tracker.Size.Y < tracker.GetCombinedMinimumSize().Y
                || restoredHeader.Position.DistanceTo(originalHeader.Position) > .5f
                || restoredHeader.Size.DistanceTo(originalHeader.Size) > .5f
                || !_inspectorPanel.GetGlobalRect().Encloses(restoredHeader)
                || restoredHeader.End.Y > _phaseActions.GetGlobalRect().Position.Y)
                throw new Exception($"Fixed battle header was not restored visibly: before={originalHeader}, after={restoredHeader}.");
            GD.Print($"INSPECTION_HEADER: restored={restoredHeader}; minimumHeight={tracker.GetCombinedMinimumSize().Y}; focus={GetWindow().HasFocus()}");
            PushCaptureMouse(new InputEventMouseMotion { Position = new Vector2(15, 140), GlobalPosition = new Vector2(15, 140) });
            ReleaseTableFocus(); HideTableTooltip();
            await SettleInspection();
            GD.Print("DESELECT_PASS: native hand/board repeat clicks, switching inspection and Close; fixed battle header restored; match version unchanged; assignment drafts preserved.");
        }
        catch (Exception e) { _deselectProbeError = e.ToString(); }
        _deselectProbeDone = true;
    }
}
