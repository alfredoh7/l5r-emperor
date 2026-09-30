using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private async Task CheckOpenBattlefieldReview(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            async Task Capture(string name)
            {
                MoveProbeMouse(new Vector2(8, GetViewport().GetVisibleRect().Size.Y / 2)); HideTableTooltip();
                await CoverageFrames();
                foreach (var caption in _battlefieldCaptions.Values.Where(c => c.IsVisibleInTree()))
                    foreach (var tile in _tiles.Where(t => t.Card.Zone == Zone.Battlefield))
                    {
                        var corners = tile.FaceCornersOnViewport();
                        var face = new Rect2(corners[0], Vector2.Zero);
                        foreach (var point in corners) face = face.Expand(point);
                        RequireCamera(!caption.GetGlobalRect().Intersects(face), "battlefield caption obscures a card face");
                    }
                using var image = GetViewport().GetTexture().GetImage();
                RequireCamera(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "battlefield capture failed");
            }
            async Task Click(Control control, bool table = false)
            {
                await CoverageFocus();
                var point = table ? ScreenCenter(control) : control.GetGlobalRect().GetCenter();
                MoveProbeMouse(point);
                await CoverageFrames();
                CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point);
                await CoverageFrames();
            }
            await CoverageFrames(); await CoverageFocus();
            _reducedMotion = true; PrepareOpenBattlefieldReview(); await CoverageFrames();
            RequireCamera(BattlefieldProvince(100) is null && BattlefieldName(100) == "Open battlefield", "open battlefield identity");
            RequireCamera(_matTargets.Single(s => s.Id == "battle:1:100").ClosedCourt && _bloodCourts[100].Closed, "standalone court is not closed");
            RequireCamera(!_matTargets.Any(s => s.Id == "province:1:100"), "invented province bay");
            await Capture("01-open-battlefield");
            var version = View.Version; var underlying = _session.Snapshot!.View.Version;
            foreach (var id in new[] { "open-review-0-100", "open-review-1-100" })
            {
                var tile = _tiles.Single(t => t.Card.Id == id);
                RequireCamera(tile.Card.Definition is { } definition && CardTile.Artwork(definition) is not null, "missing full card scan");
                await Click(tile, true);
                RequireCamera(_selected?.Id == id, "open battlefield card could not be inspected: " + id);
            }
            DeselectCard(); FocusReviewBattlefields(); await CoverageFrames();
            var help = HudDescendants(_battlefieldHeaders!).OfType<Button>().Single(b => b.HasMeta("battlefield_help"));
            await Click(help, true);
            RequireCamera(_battlefieldHelp is { Visible: true }, $"native help click failed (help={help.GetGlobalRect()}, parent={((Control)help.GetParent()).GetGlobalRect()}, visible={help.IsVisibleInTree()}, focus={GetWindow().HasFocus()}, hovered={GetViewport().GuiGetHoveredControl()?.Name}, fixed={FixedHudCovers(ScreenCenter(help))}, blocked={CameraBlocked})");
            await Capture("02-rules-help");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
            await CoverageFrames();
            RequireCamera(_battlefieldHelp is null, "Escape did not dismiss help");
            await Submit(new GameCommand("concede", View.ViewerId, View.Version));
            RequireCamera(View.Version == version && _session.Snapshot!.View.Version == underlying, "review input changed game state");

            _battlefieldReviewScenario = 1; PrepareOpenBattlefieldReview(); await CoverageFrames();
            RequireCamera(_battlefieldNavigation.Count == 2 && CourtConnectsToProvince(1) && !CourtConnectsToProvince(100), "mixed court associations");
            version = View.Version;
            await Click(_battlefieldNavigation[1]);
            RequireCamera(_viewedBattlefield == 1 && View.CurrentBattleProvince == 100 && View.Version == version && _session.Snapshot!.View.Version == underlying, "navigation chose a battle or submitted a command");
            await Capture("03-camera-navigation");
            FocusReviewBattlefields(); await Capture("04-multiple-courts");
            _battlefieldReviewSeat = 1; PrepareOpenBattlefieldReview(); await Capture("05-other-seat");
            RequireCamera(_matTargets.Single(s => s.Id == "battle:1:100").ClosedCourt, "opposite seat reopened neck");

            _battlefieldReviewScenario = 4; _battlefieldReviewSeat = 0; PrepareOpenBattlefieldReview(); await CoverageFrames();
            RequireCamera(BattlefieldProvince(100) == 1 && BattlefieldName(100).Contains("extra front"), "additional associated front mislabeled as open");
            var bounds = _battleCourts.ToDictionary();
            RemoveReviewProvince(); await CoverageFrames();
            RequireCamera(_battleCourts.All(c => c.Value.IsEqualApprox(bounds[c.Key])), "province removal moved courts");
            RequireCamera(_bloodCourts.Values.All(c => c.Closed) && BattlefieldName(1) == "Open battlefield A" && BattlefieldName(100) == "Open battlefield B", "orphan courts retained connection or ambiguous name");
            await Capture("06-province-removed");
            _battlefieldReviewView = View with { Battlefields = [new BattlefieldView(100, null)], UnresolvedBattlefields = [100], Players = View.Players.Select(p => p with { Army = p.Army.Where(c => c.ProvinceIndex == 100).ToArray() }).ToArray() };
            Render(); await CoverageFrames();
            RequireCamera(BattlefieldName(100) == "Open battlefield B", "remaining court was renamed during battle");

            _battlefieldReviewScenario = 5; PrepareOpenBattlefieldReview(); await CoverageFrames();
            RequireCamera(_battleCourts.Count == 6 && _battlefieldNavigation.Count == 6, "additional courts truncated");
            foreach (var pair in _battleCourts)
                RequireCamera(_battleCourts.Where(c => c.Key != pair.Key).All(c => !pair.Value.Intersects(c.Value)), "extra courts overlap");
            await Capture("07-six-courts");
            _battlefieldReviewScenario = 6; PrepareOpenBattlefieldReview();
            _battlefieldReviewView = View with { Phase = Phase.BattleSelection, CurrentBattleProvince = null };
            Render(); await CoverageFrames();
            RequireCamera(_battleCourts.Count == 1 && _battleHeight >= 270 && !_bloodCourts.ContainsKey(100), "empty unresolved court omitted or painted as an attack");
            await Capture("08-empty-court");

            _battlefieldReviewScenario = 7; PrepareOpenBattlefieldReview(); await CoverageFrames();
            foreach (var player in View.Players)
                foreach (var unit in player.Army.Where(c => c.AttachedTo is null))
                {
                    var footprint = new Rect2(_battlePlacements[unit.Id], new Vector2(UnitWidth(unit, player), StackHeight(unit, player)));
                    RequireCamera(_battleCourts[100].Encloses(footprint), "crowded unit or attachment falls outside court");
                }
            await Capture("09-crowded");
            _battlefieldReviewScenario = 0; PrepareOpenBattlefieldReview();
            GetWindow().Size = new Vector2I(1440, 900); await CoverageFrames(); FocusReviewBattlefields(); await Capture("10-1440x900");
            GetWindow().Size = new Vector2I(2560, 1080); await CoverageFrames(); FocusReviewBattlefields(); await Capture("11-ultrawide");
            _reducedMotion = false; _battlefieldReviewScenario = 1; PrepareOpenBattlefieldReview(); await CoverageFrames();
            await CoverageFocus();
            ViewBattlefield(1);
            RequireCamera(_cameraGlide && View.CurrentBattleProvince == 100, "normal-motion navigation did not glide independently");
            for (var i = 0; i < 90 && _cameraGlide; i++) await CoverageFrames();
            RequireCamera(!_cameraGlide, $"camera glide did not settle (blocked={CameraBlocked}, focused={GetWindow().HasFocus()}, time={_cameraGlideTime})");
            GD.Print("OPEN_BATTLEFIELD_REVIEW_PASS: full scans/native card inspection; help/Escape; camera-only navigation; both seats; associated extra front; stable orphan court positions/names; six courts; empty selection; bowed/attached units; 1440x900/ultrawide; reduced motion and smooth camera.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
