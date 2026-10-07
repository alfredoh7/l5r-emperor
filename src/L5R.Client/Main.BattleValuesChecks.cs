using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private async Task CheckBattleValuesReview(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            foreach (var scenario in Enumerable.Range(0, ValuesScenarios.Length))
                foreach (var seat in new[] { 0, 1 })
                {
                    _valuesReviewScenario = scenario; PrepareBattleValuesReview();
                    if (seat == 1) _session.SwitchPracticeSeat();
                    await CoverageFrames(); FocusBattleValues(); await CoverageFrames();
                    // Sample settled UI, with texture resources surviving a collection.
                    _turnBloom.Cancel(); GC.Collect(); GC.WaitForPendingFinalizers(); await CoverageFrames();
                    VerifyBattleValuesGeometry();
                    var field = View.CurrentBattleProvince!.Value; var v = BattleValues(field)!;
                    RequireCamera(_valueRails[(field, v.AttackerId)].Force == v.AttackerForce && _valueRails[(field, v.DefenderId)].Units == v.DefenderUnits, "Displayed values differ from engine snapshot");
                    RequireCamera(_strengthBadges.Count == (v.ProvinceStrength is null ? 0 : 1), "Open court invented a province strength");
                    using var image = GetViewport().GetTexture().GetImage();
                    RequireCamera(image.SavePng(System.IO.Path.Combine(directory, $"{ValuesScenarios[scenario]}-seat{seat}.png")) == Error.Ok, "Capture failed");
                }
            foreach (var size in new[] { new Vector2I(1280, 800), new Vector2I(2560, 1080) })
            {
                GetWindow().Size = size; _valuesReviewScenario = 0; _reducedMotion = true;
                PrepareBattleValuesReview(); await CoverageFrames(); FocusBattleValues(); await CoverageFrames(); VerifyBattleValuesGeometry();
                using var image = GetViewport().GetTexture().GetImage(); image.SavePng(System.IO.Path.Combine(directory, $"reduced-{size.X}x{size.Y}.png"));
            }
            GetWindow().Size = new Vector2I(1920, 1080); _valuesReviewScenario = 0; _reducedMotion = false;
            PrepareBattleValuesReview(); await CoverageFrames(); FocusBattleValues(); await CoverageFrames();
            await CoverageFocus();
            var otherSeat = _valuesReviewBar!.GetChildren().OfType<Button>().Single(b => b.Text == "Other seat");
            var seatPoint = otherSeat.GetGlobalRect().GetCenter(); MoveProbeMouse(seatPoint);
            CameraMouse(MouseButton.Left, true, seatPoint); CameraMouse(MouseButton.Left, false, seatPoint); await CoverageFrames();
            RequireCamera(View.ViewerId == 1, "Review's Other seat button did not receive native input");
            CameraMouse(MouseButton.Left, true, seatPoint); CameraMouse(MouseButton.Left, false, seatPoint); await CoverageFrames();
            RequireCamera(View.ViewerId == 0, "Review seat button could not return to attacker view");
            var pickerPoint = _valuesReviewPicker!.GetGlobalRect().GetCenter(); MoveProbeMouse(pickerPoint);
            CameraMouse(MouseButton.Left, true, pickerPoint); CameraMouse(MouseButton.Left, false, pickerPoint); await CoverageFrames();
            var popup = _valuesReviewPicker.GetPopup();
            RequireCamera(popup.Visible, "Review scenario picker did not open");
            RequireCamera(popup.ItemCount == ValuesScenarios.Length, "Review scenarios missing from native picker");
            // OS-native popup selection is verified with computer-use; synthetic root-viewport
            // keys do not activate the separate macOS PopupMenu window.
            popup.Hide(); await CoverageFrames(); await CoverageFocus();
            _valuesReviewScenario = 0; PrepareBattleValuesReview(); await CoverageFrames(); _turnBloom.Cancel();
            var zoomPoint = ScreenCenter(_valueRails[(View.CurrentBattleProvince!.Value, View.ViewerId)]);
            var zoomBefore = _cameraZoom; MoveProbeMouse(zoomPoint);
            for (var n = 0; n < 3; n++) { CameraMouse(MouseButton.WheelUp, true, zoomPoint); CameraMouse(MouseButton.WheelUp, false, zoomPoint); }
            await CoverageFrames(); RequireCamera(_cameraZoom > zoomBefore, "Review cannot zoom to inspect its rails");
            VerifyBattleValuesGeometry();
            using (var closeup = GetViewport().GetTexture().GetImage()) closeup.SavePng(System.IO.Path.Combine(directory, "closeup-seat0.png"));
            FocusBattleValues(); await CoverageFrames();
            // Native input must still reach a full scan beneath the inset rails.
            var tile = _tiles.First(t => t.Card.Zone == Zone.Battlefield && t.Card.AttachedTo is null);
            await CoverageFocus(); var point = ScreenCenter(tile); MoveProbeMouse(point); await CoverageFrames();
            CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point); await CoverageFrames();
            RequireCamera(_selected?.Id == tile.Card.Id, "Rail blocked card inspection");
            var forceBefore = BattleValues(View.CurrentBattleProvince!.Value)!.AttackerForce;
            var boost = Actions.FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "crushing-force");
            RequireCamera(boost?.Command is not null && boost.TargetIds?.Length > 0, "Prepared Force-changing action unavailable");
            await _session.Submit(boost!.Command! with { TargetId = boost.TargetIds![0] }); await CoverageFrames();
            // The real action may open reaction windows; decline the offered optional windows from each seat.
            for (var i = 0; i < 10 && View.PendingChoice is not null; i++)
            {
                _session.FollowPracticePriority();
                var decline = Actions.FirstOrDefault(a => a.Enabled && a.Command?.Type == "declineReaction");
                RequireCamera(decline?.Command is not null, "Unexpected prepared reaction window");
                await _session.Submit(decline!.Command!); await CoverageFrames();
            }
            RequireCamera(BattleValues(View.CurrentBattleProvince!.Value)!.AttackerForce == forceBefore + 3, "Live Force action did not update engine total");
            VerifyBattleValuesGeometry();
            PrepareBattleValuesReview(); await CoverageFrames();
            var pass = Actions.FirstOrDefault(a => a.Enabled && a.Command?.Type == "pass");
            RequireCamera(pass?.Command is not null, "Prepared review is not an interactive engine battle");
            await _session.Submit(pass!.Command!); await CoverageFrames();
            RequireCamera(View.PriorityPlayerId == 1, "Live engine action did not refresh the seat opportunity");
            VerifyBattleValuesGeometry();
            await _session.Submit(new GameCommand("concede", View.ViewerId, View.Version)); await CoverageFrames();
            RequireCamera(_valueRails.Count == 0 && _strengthBadges.Count == 0, "Finished match retained battle values");
            GD.Print("BATTLE_VALUES_PASS: six real-engine scenarios, both seats, inset/card clearance, native review controls/zoom/card input, live Force action/concession, two resize/reduced-motion cases.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void VerifyBattleValuesGeometry()
    {
        foreach (var ((field, owner), rail) in _valueRails)
        {
            var court = _battleCourts[field]; var rect = new Rect2(court.Position + rail.Position, rail.Size);
            // Pigment and ornamental fittings occupy the outer 22 world units.
            RequireCamera(court.Grow(-22).Encloses(rect), "Rail crosses the blood border");
            foreach (var child in rail.GetChildren().OfType<Control>())
                RequireCamera(new Rect2(Vector2.Zero, rail.Size).Encloses(new Rect2(child.Position, child.Size)), "Rail child expands beyond its intended bounds");
            RequireCamera((owner == Self.Id) == (rect.GetCenter().Y > court.GetCenter().Y), "Army rail belongs to the wrong seat");
            foreach (var player in View.Players)
                foreach (var unit in player.Army.Where(c => c.AttachedTo is null && c.ProvinceIndex == field))
                    RequireCamera(!rect.Intersects(new Rect2(_battlePlacements[unit.Id], new Vector2(UnitWidth(unit, player), StackHeight(unit, player)))), "Rail obscures a card, bowed unit, attachment or standard");
        }
    }
}
