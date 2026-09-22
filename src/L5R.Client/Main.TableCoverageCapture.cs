using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private async Task CoverageFrames()
    {
        for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
    }

    private async Task CoverageFocus()
    {
        for (var i = 0; i < 60 && !GetWindow().HasFocus(); i++)
        {
            GetWindow().GrabFocus(); await CoverageFrames();
        }
        RequireCamera(GetWindow().HasFocus(), "native coverage window could not regain focus");
    }

    private async Task CaptureTableCoverage()
    {
        try
        {
            ResetTableCamera();
            if (_captureMode.EndsWith("-review", StringComparison.Ordinal))
            {
                _cameraFitted = false; _cameraZoom = FitZoom * 1.4f; ApplyTableCamera();
                _capturePath = null; _cameraProbeDone = true;
                GD.Print("TABLE_VISIBILITY_REVIEW_READY: prepared zoom; wheel or drag to inspect the edges, Home to fit the table.");
                return;
            }
            // Card transfers must settle before sampling a stationary face.
            for (var i = 0; i < 120 && !TableMotionsSettled(); i++) await CoverageFrames();
            RequireCamera(TableMotionsSettled(), "initial card motion did not settle");
            var version = View.Version;
            var fitted = _tableViewport.GetGlobalRect();
            var tile = _tiles.Where(t => _board.IsAncestorOf(t) && !t.Card.Bowed && t.Card.Definition is not null && t.Card.AttachedTo is null)
                .OrderBy(t => Math.Abs(ScreenToWorld(ScreenCenter(t)).Y - _worldHeight * .4f)).First();
            var anchor = ScreenToWorld(ScreenCenter(tile));
            var density = _root.Scale.X;
            var points = new (string Edge, Vector2 Point)[]
            {
                ("left", new(fitted.Position.X - 12 * density, fitted.GetCenter().Y)),
                ("right", new(fitted.End.X + 8 * density, fitted.GetCenter().Y)),
                ("top", new(fitted.GetCenter().X, fitted.Position.Y - 12 * density)),
                ("bottom", new(fitted.GetCenter().X, fitted.End.Y + 12 * density))
            };
            foreach (var (edge, point) in points)
            {
                _cameraFitted = false; _cameraZoom = 1.1f;
                var local = _tableViewport.GetGlobalTransform().AffineInverse() * point;
                _cameraCenter = anchor - TableSurface.UnprojectOffset(local - _tableViewport.Size / 2) / _cameraZoom;
                ApplyTableCamera();
                RequireCamera(ScreenCenter(tile).DistanceTo(point) < 1, $"{edge}: fixture could not reach boundary");
                RequireCamera(!fitted.HasPoint(point), $"{edge}: regression sample is inside old crop");
                MoveProbeMouse(_menu.GetGlobalRect().GetCenter());
                await CoverageFrames();
                using var rendered = GetViewport().GetTexture().GetImage();
                var stem = System.IO.Path.ChangeExtension(_capturePath, null);
                rendered.SavePng($"{stem}-{edge}.png");
                _tableSurface.Hide(); await CoverageFrames();
                using var background = GetViewport().GetTexture().GetImage();
                _tableSurface.Show(); await CoverageFrames();
                float difference = 0; int count = 0;
                for (var y = -3; y <= 3; y++) for (var x = -3; x <= 3; x++)
                {
                    var p = (Vector2I)point + new Vector2I(x, y);
                    var a = rendered.GetPixelv(p); var b = background.GetPixelv(p);
                    difference += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B); count++;
                }
                RequireCamera(difference / count > .08f, $"{edge}: card is visually clipped outside fitting rectangle (pixel difference {difference / count:F4})");
                await CoverageFocus();
                if (_selected?.Id == tile.Card.Id) DeselectCard();
                MoveProbeMouse(point); CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point);
                RequireCamera(_selected?.Id == tile.Card.Id, $"{edge}: visible card cannot be inspected (focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, pointer={PointerOnTable(point)}, hud={FixedHudCovers(point)}, selected={_selected?.Id}, expected={tile.Card.Id}, hovered={TableHovered?.Name})");
                var before = ScreenToWorld(point); var zoom = _cameraZoom;
                CameraMouse(MouseButton.WheelUp, true, point); CameraMouse(MouseButton.WheelUp, false, point);
                RequireCamera(_cameraZoom > zoom && ScreenToWorld(point).DistanceTo(before) < .1f, $"{edge}: wheel loses pointer anchor");
                GD.Print($"TABLE_COVERAGE_EDGE_PASS: {edge}; visible pixels, native click and cursor-anchored zoom.");
            }
            // Keep fixed controls in front of the newly extended card layer.
            foreach (var hud in new[] { _inspectorPanel, _hand, _timeline, _menu }.Concat(_clanHud.GetChildren().OfType<Control>()).Where(c => c.Visible))
            {
                await CoverageFocus();
                var point = hud.GetGlobalRect().GetCenter();
                RequireCamera(!PointerOnTable(point), $"fixed HUD admits table input: {hud.Name}");
                var zoom = _cameraZoom; MoveProbeMouse(point);
                CameraMouse(MouseButton.WheelDown, true, point); CameraMouse(MouseButton.WheelDown, false, point);
                RequireCamera(_cameraZoom == zoom, $"wheel over {hud.Name} moved table");
            }
            foreach (var button in HudDescendants(_clanHud).OfType<BaseButton>().Append(_fitTableButton))
            {
                await CoverageFocus();
                MoveProbeMouse(button.GetGlobalRect().GetCenter()); await CoverageFrames();
                var hovered = GetViewport().GuiGetHoveredControl();
                RequireCamera(hovered == button || hovered is not null && button.IsAncestorOf(hovered),
                    $"table surface intercepts fixed HUD control {button.Name} (hovered={hovered?.Name})");
            }
            RequireCamera(View.Version == version, "coverage inspection changed match state");
            DeselectCard(); ReleaseTableFocus(); ResetTableCamera(); await CoverageFrames();
            GD.Print("TABLE_COVERAGE_PASS: four old clip boundaries render and receive input; fixed HUD protected; no match changes.");
            _cameraProbeDone = true;
        }
        catch (Exception error)
        {
            _tableSurface.Show(); GD.PushError(error.ToString()); _cameraProbeDone = true; _capturePath = null; GetTree().Quit(1);
        }
    }
}
