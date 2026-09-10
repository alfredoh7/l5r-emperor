using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _deselectProbeDone;
    private string? _deselectProbeError;
    private async Task SettleInspection() => await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
    private async Task ClickInspectionCard(CardView card)
    {
        GetWindow().GrabFocus();
        await SettleInspection();
        var tile = _tiles.First(t => t.Card.Id == card.Id);
        var point = tile.FaceCornersOnViewport().Aggregate(Vector2.Zero, (a,b) => a+b) / 4;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await ToSignal(GetTree().CreateTimer(.06), SceneTreeTimer.SignalName.Timeout);
        GetWindow().GrabFocus();
        point = tile.FaceCornersOnViewport().Aggregate(Vector2.Zero, (a,b) => a+b) / 4;
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false });
        await SettleInspection();
        GD.Print($"DESELECT_CLICK: {card.Definition?.Name} point={point} selected={_selected?.Definition?.Name} hovered={(TableHovered as CardTile)?.Card.Definition?.Name ?? TableHovered?.Name} chosen={_chosen?.Type} focus={GetWindow().HasFocus()} table={_tableViewport.GetGlobalRect()}");
    }
    private async void ProbeDeselection()
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
                if (_chosen != draft || !assignments.OrderBy(x => x.Key).SequenceEqual(_assignments.OrderBy(x => x.Key)))
                    throw new Exception("Repeated assignment selection discarded the active draft.");
                CancelChoice();
            }
            _selected = null; RenderInspector(); Highlight();
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
            GetWindow().GrabFocus();
            var p = close.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await SettleInspection();
            if (_selected is not null || View.Version != version) throw new Exception("Close did not deselect without issuing a game command.");
            var tracker = _sidebarHeader.GetChildren().OfType<BattleTracker>().Single();
            if (tracker.Size.Y < 120 || tracker.Size.Y > 180 || !_inspectorPanel.GetGlobalRect().Encloses(tracker.GetGlobalRect()))
                throw new Exception("Fixed battle header was not restored visibly.");
            GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(15,140), GlobalPosition = new Vector2(15,140) }, true);
            ReleaseTableFocus(); HideTableTooltip();
            await SettleInspection();
            GD.Print("DESELECT_PASS: native hand/board repeat clicks, switching inspection and Close; fixed battle header restored; match version unchanged; assignment drafts preserved.");
        }
        catch (Exception e) { _deselectProbeError = e.ToString(); }
        _deselectProbeDone = true;
    }
}
