using Godot;

namespace L5R.Client;

public partial class Main
{
    private Control _tableViewport = null!, _world = null!;
    private TableSurface _tableSurface = null!;
    private Control? _tableTooltip, _tableHover;
    private double _tableHoverTime;
    private Vector2 _lastTableMouse;
    private bool _syncTablePointer;
    private Control? TableFocus => _tableSurface.Canvas.GuiGetFocusOwner();
    private Control? TableHovered => _tableSurface.Canvas.GuiGetHoveredControl();
    private void ReleaseTableFocus() { GetViewport().GuiReleaseFocus(); _tableSurface.Canvas.GuiReleaseFocus(); }
    private Vector2 ScreenCenter(Control c) => _world.IsAncestorOf(c) ? _tableSurface.ProjectCanvas(c.GetGlobalRect().GetCenter()) : c.GetGlobalRect().GetCenter();
    private Vector2 ScreenToWorld(Vector2 p) => _world.GetGlobalTransform().AffineInverse() * _tableSurface.ScreenToCanvas(p);
    private Vector2 WorldToScreen(Vector2 p) => _tableSurface.ProjectCanvas(_world.GetGlobalTransform() * p);
    private Button _fitTableButton = null!, _battleCameraButton = null!;
    private bool _tableLayoutSeen;
    private Vector2 _cameraCenter = new(680, 410);
    private float _cameraZoom = 1;
    private bool _cameraFitted = true, _cameraDragging;
    private MouseButton _cameraDragButton;
    private float FitZoom => FitRegion(new Rect2(-20, -12, WorldWidth + 40, _worldHeight + 24));
    private float FitRegion(Rect2 region)
    {
        var low = .05f; var high = 2f;
        var center = region.GetCenter();
        Vector2[] corners = [region.Position, new(region.End.X, region.Position.Y), region.End, new(region.Position.X, region.End.Y)];
        var available = new Rect2(Vector2.Zero, _tableViewport.Size).Grow(-12);
        for (var i = 0; i < 18; i++)
        {
            var zoom = (low + high) / 2;
            if (corners.All(p => (p.Y - center.Y) * zoom * TableSurface.Depth < .8f && available.HasPoint(_tableViewport.Size / 2 + TableSurface.ProjectOffset((p - center) * zoom)))) low = zoom;
            else high = zoom;
        }
        return low;
    }

    private void BuildTableViewport()
    {
        _tableViewport = Place(_root, new Control { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore }, 24, 148, 1500, 702);
        _tableSurface = Place(_tableViewport, new TableSurface(), 0, 0, 1500, 702);
        _tableSurface.ResizePlane(_tableViewport.Size, 1);
        _world = Place(_tableSurface.Canvas, new Control { Theme = Palette.MakeTheme(), MouseFilter = Control.MouseFilterEnum.Ignore }, 0, 0, WorldWidth, 1100);
    }

    private void BuildCameraControls()
    {
        _fitTableButton = new TableIconButton(TableGlyph.Fit, () => { ReleaseTableFocus(); ResetTableCamera(); },
            "Fit table · Home\nArrow keys or drag empty cloth to pan. Middle-drag or Space + drag also pans. Mouse wheel zooms toward the pointer.");
        _menu.AddChild(_fitTableButton); _menu.MoveChild(_fitTableButton, 0);
        _battleCameraButton = new TableIconButton(TableGlyph.Battle, () => { ReleaseTableFocus(); FocusBattle(); }, "Frame current battlefields");
        _menu.AddChild(_battleCameraButton); _menu.MoveChild(_battleCameraButton, 1);
    }

    private void FitTableViewport()
    {
        _tableViewport.Position = new Vector2(144, 148);
        _tableViewport.Size = new Vector2(_layoutWidth - SidebarWidth - 188, 666);
        _tableSurface.ResizePlane(_tableViewport.Size, _root.Scale.X);
        ApplyTableCamera();
    }

    private void FocusMySide()
    {
        _cameraFitted = false;
        _cameraZoom = FitRegion(new Rect2(-20, _selfHomeY - 20, WorldWidth + 40, _worldHeight - _selfHomeY + 40));
        _cameraCenter = new Vector2(WorldWidth / 2, (_selfHomeY + _worldHeight) / 2);
        ApplyTableCamera();
    }

    private void FocusBattle()
    {
        if (!BattleActive) return;
        var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
        var lanes = View.CurrentBattleProvince is int active ? new[] { active }
            : View.Players.SelectMany(p => p.Army).Where(c => c.ProvinceIndex is not null).Select(c => c.ProvinceIndex!.Value).Distinct().ToArray();
        if (lanes.Length == 0) return;
        var left = lanes.Min(p => SlotX(defender, 2 + p) - (_laneWidths[p] - CardHeight) / 2);
        var right = lanes.Max(p => SlotX(defender, 2 + p) + (_laneWidths[p] + CardHeight) / 2);
        _cameraFitted = false;
        _cameraZoom = FitRegion(new Rect2(left - 20, _battleY - 12, right - left + 40, _battleHeight + 24));
        _cameraCenter = new Vector2((left + right) / 2, _battleY + _battleHeight / 2);
        ApplyTableCamera();
    }

    private void ResetTableCamera()
    {
        _cameraFitted = true; _cameraDragging = false;
        ApplyTableCamera();
    }

    private void ApplyTableCamera()
    {
        if (_world is null) return;
        if (_battleCameraButton is not null) _battleCameraButton.Visible = _session.Snapshot is not null && BattleActive && View.Players.Any(p => p.Army.Length > 0);
        if (_cameraFitted)
        {
            _cameraZoom = FitZoom;
            _cameraCenter = new Vector2(WorldWidth / 2, _worldHeight / 2);
        }
        else
        {
            _cameraZoom = Math.Clamp(_cameraZoom, FitZoom * .72f, 2f);
            // Keep the table recoverable while allowing breathing room around every edge.
            _cameraCenter = _cameraCenter.Clamp(new Vector2(-100, -100), new Vector2(WorldWidth + 100, _worldHeight + 100));
        }
        _world.Scale = Vector2.One * _cameraZoom;
        _world.Position = _tableSurface.CanvasSize / 2 - _cameraCenter * _cameraZoom;
        _table.SetPlaneCamera(_tableViewport.GetGlobalRect().GetCenter(), _root.Scale.X, _cameraCenter, _cameraZoom, new Vector2(WorldWidth, _worldHeight));
        CardPaper.SetTableTransform(_world.Position * _tableSurface.RenderScale, _cameraZoom * _tableSurface.RenderScale, _tableSurface.Canvas.Size);
        HideTableTooltip(); _syncTablePointer = true;
        _intentOverlay?.QueueRedraw();
    }

    private void ZoomTable(float factor, Vector2 point)
    {
        var offset = TableSurface.UnprojectOffset(point - _tableViewport.Size / 2);
        var before = _cameraCenter + offset / _cameraZoom;
        _cameraFitted = false;
        _cameraZoom = Math.Clamp(_cameraZoom * factor, FitZoom * .72f, 2f);
        _cameraCenter = before - offset / _cameraZoom;
        ApplyTableCamera();
    }

    private bool CameraBlocked => !GetWindow().HasFocus() || (_fullscreenGate?.Visible ?? false)
        || GetChildren().OfType<Window>().Any(w => w.Visible)
        || _expandedUnits.Count > 0;

    private bool PointerOnTable(Vector2 viewportPoint)
    {
        if (CameraBlocked || !_tableViewport.GetGlobalRect().HasPoint(viewportPoint)) return false;
        // Fixed HUD lives outside this rectangle; modal/expanded views are
        // blocked above. Root hover state intentionally does not govern the
        // isolated viewport, whose mouse events are forwarded before root GUI.
        return true;
    }

    public override void _Input(InputEvent @event)
    {
        if (_tableSurface is null || MenuVisible) return;
        if (@event is InputEventMouse location) _lastTableMouse = location.Position;
        // The token buttons live above the projected viewport; let native GUI input
        // reach them instead of forwarding their clicks into the underlying table.
        if (!_cameraDragging && @event is InputEventMouse tokenMouse && OverStatToken(tokenMouse.Position)
            && (@event is InputEventMouseMotion || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left }))
        { _tableSurface.ExitMouse(); HideTableTooltip(); return; }
        if (@event is InputEventKey { Pressed: false } released && released.Keycode is Key.Enter or Key.KpEnter or Key.Tab && !CameraBlocked && GetViewport().GuiGetFocusOwner() is null && TableFocus is not null)
            _tableSurface.Canvas.PushInput(released, true);
        if (@event is InputEventMouseButton mouse)
        {
            if (!mouse.Pressed && _cameraDragging && mouse.ButtonIndex == _cameraDragButton)
            {
                _cameraDragging = false; GetViewport().SetInputAsHandled(); return;
            }
            if (!PointerOnTable(mouse.Position))
            {
                if (!mouse.Pressed) _tableSurface.ForwardMouse(mouse);
                return;
            }
            if (!mouse.Pressed) { _tableSurface.ForwardMouse(mouse); GetViewport().SetInputAsHandled(); return; }
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var point = _tableViewport.GetGlobalTransform().AffineInverse() * mouse.Position;
                ZoomTable(Mathf.Pow(1.12f, (mouse.ButtonIndex == MouseButton.WheelUp ? 1 : -1) * Math.Max(.1f, mouse.Factor)), point);
                GetViewport().SetInputAsHandled(); return;
            }
            _tableSurface.ForwardMouse(new InputEventMouseMotion { Position = mouse.Position });
            var hovered = TableHovered;
            var emptyCloth = hovered is null || hovered == _root || hovered == _tableViewport || hovered == _world || hovered == _board || hovered == _pileHud;
            if (mouse.ButtonIndex == MouseButton.Middle || mouse.ButtonIndex == MouseButton.Left && (Input.IsPhysicalKeyPressed(Key.Space) || emptyCloth))
            {
                _cameraDragging = true; _cameraDragButton = mouse.ButtonIndex;
                ReleaseTableFocus(); HideTableTooltip(); _tableSurface.ExitMouse(); GetViewport().SetInputAsHandled();
            }
            else { _tableSurface.ForwardMouse(mouse); GetViewport().SetInputAsHandled(); }
        }
        else if (@event is InputEventMouseMotion motion && _cameraDragging)
        {
            if (!CameraBlocked)
            {
                _cameraFitted = false;
                var local = _tableViewport.GetGlobalTransform().AffineInverse() * motion.Position - _tableViewport.Size / 2;
                var previous = local - motion.Relative / _root.Scale.X;
                _cameraCenter -= (TableSurface.UnprojectOffset(local) - TableSurface.UnprojectOffset(previous)) / _cameraZoom;
                ApplyTableCamera();
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseMotion pointer)
        {
            _lastTableMouse = pointer.Position;
            if (PointerOnTable(pointer.Position)) { _tableSurface.ForwardMouse(pointer); GetViewport().SetInputAsHandled(); }
            else { _tableSurface.ExitMouse(); HideTableTooltip(); }
        }
        else if (@event is InputEventKey { Pressed: true } key && !CameraBlocked)
        {
            if (key.Keycode == Key.Space && PointerOnTable(GetViewport().GetMousePosition()))
            {
                GetViewport().SetInputAsHandled(); return;
            }
            var focus = GetViewport().GuiGetFocusOwner() ?? TableFocus;
            if (focus is LineEdit or TextEdit) return;
            if (focus is not null && _world.IsAncestorOf(focus) && key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down) ReleaseTableFocus();
            if (key.Keycode == Key.Home)
            {
                ResetTableCamera(); GetViewport().SetInputAsHandled();
            }
            // With focus on an inspector button, arrows retain native focus navigation.
            else if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down && GetViewport().GuiGetFocusOwner() is null)
                GetViewport().SetInputAsHandled();
            else if (key.Keycode is Key.Enter or Key.KpEnter or Key.Tab && GetViewport().GuiGetFocusOwner() is null && TableFocus is not null) { _tableSurface.Canvas.PushInput(key, true); GetViewport().SetInputAsHandled(); }
        }
    }

    private void ProcessTableCamera(double delta)
    {
        ProcessTableTooltip(delta);
        if (_world is null || CameraBlocked)
        {
            _cameraDragging = false; return;
        }
        if (GetViewport().GuiGetFocusOwner() is not null || TableFocus is not null) return;
        var direction = new Vector2((Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0));
        if (direction == Vector2.Zero) return;
        _cameraFitted = false;
        _cameraCenter += direction.Normalized() * new Vector2(1, 1 / TableSurface.Pitch) * (float)delta * 650 / _cameraZoom;
        ApplyTableCamera();
    }
    private void HideTableTooltip()
    {
        if (IsInstanceValid(_tableTooltip)) { _tableTooltip!.QueueFree(); _tableTooltip = null; }
        _tableHover = null; _tableHoverTime = 0;
    }
    private void PrepareTableTooltips(Node node)
    {
        if (node is Control control && !string.IsNullOrEmpty(control.TooltipText))
        {
            control.SetMeta("flat_tooltip", control.TooltipText); control.TooltipText = "";
        }
        foreach (var child in node.GetChildren()) PrepareTableTooltips(child);
    }
    private void ProcessTableTooltip(double delta)
    {
        if (_tableSurface is null) return;
        var token = GetViewport().GuiGetHoveredControl() as StatTokens;
        if (token is not null && !token.IsVisibleInTree()) token = null;
        // Token explanations use the same bounded overlay as card previews, so
        // native tooltips and the projected viewport cannot compete for the hover.
        if (token is not null) { _syncTablePointer = false; _tableSurface.ExitMouse(); }
        PrepareTableTooltips(_world);
        if (token is null && _syncTablePointer && !_cameraDragging && PointerOnTable(_lastTableMouse))
        {
            _syncTablePointer = false;
            _tableSurface.ForwardMouse(new InputEventMouseMotion { Position = _lastTableMouse });
        }
        if (_cameraDragging || (CameraBlocked && token?.Inline != true) || (token is null && !PointerOnTable(_lastTableMouse)))
        { HideTableTooltip(); return; }
        Control? hovered = token ?? TableHovered;
        if (token is null)
            while (hovered is not null && !hovered.HasMeta("flat_tooltip") && hovered is not CardTile) hovered = hovered.GetParent() as Control;
        if (hovered != _tableHover) { HideTableTooltip(); _tableHover = hovered; }
        if (hovered is null || _tableTooltip is not null || (_tableHoverTime += delta) < .5) return;
        _tableTooltip = token is not null ? token._MakeCustomTooltip(token.Explanation) :
            hovered is CardTile card ? card.CreateReadingPreview() : ReadingTooltip.Create(hovered, hovered.GetMeta("flat_tooltip").AsString());
        _root.AddChild(_tableTooltip);
        _tableTooltip.Scale = Vector2.One / _root.Scale.X;
        _tableTooltip.Size = _tableTooltip.CustomMinimumSize;
        var size = _tableTooltip.Size / _root.Scale.X;
        _tableTooltip.Position = (_lastTableMouse / _root.Scale + new Vector2(18, 22)).Clamp(new Vector2(8, 8), DesignSize - size - new Vector2(8, 8));
        _tableTooltip.ZIndex = 90;
        void Ignore(Node n) { if (n is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore; foreach (var child in n.GetChildren()) Ignore(child); }
        Ignore(_tableTooltip);
    }

}
