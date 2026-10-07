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
            var zoomPoint = WorldToScreen(_battleCourts[View.CurrentBattleProvince!.Value].GetCenter());
            var zoomBefore = _cameraZoom; MoveProbeMouse(zoomPoint);
            for (var n = 0; n < 3; n++) { CameraMouse(MouseButton.WheelUp, true, zoomPoint); CameraMouse(MouseButton.WheelUp, false, zoomPoint); }
            await CoverageFrames(); RequireCamera(_cameraZoom > zoomBefore, "Review cannot zoom to inspect its battle");
            VerifyBattleValuesGeometry();
            using (var closeup = GetViewport().GetTexture().GetImage()) closeup.SavePng(System.IO.Path.Combine(directory, "closeup-seat0.png"));
            FocusBattleValues(); await CoverageFrames();
            // Native input must still reach a full scan beneath the placards.
            var tile = _tiles.First(t => t.Card.Zone == Zone.Battlefield && t.Card.AttachedTo is null);
            await CoverageFocus(); var point = ScreenCenter(tile); MoveProbeMouse(point); await CoverageFrames();
            CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point); await CoverageFrames();
            RequireCamera(_selected?.Id == tile.Card.Id, "Placard blocked card inspection");
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
            await CheckPlacardLayout();
            PrepareBattleValuesReview(); await CoverageFrames();
            var pass = Actions.FirstOrDefault(a => a.Enabled && a.Command?.Type == "pass");
            RequireCamera(pass?.Command is not null, "Prepared review is not an interactive engine battle");
            await _session.Submit(pass!.Command!); await CoverageFrames();
            RequireCamera(View.PriorityPlayerId == 1, "Live engine action did not refresh the seat opportunity");
            VerifyBattleValuesGeometry();
            await _session.Submit(new GameCommand("concede", View.ViewerId, View.Version)); await CoverageFrames();
            RequireCamera(_placardStack.Count == 0 && _provincePlacards.Count == 0, "Finished match retained battle values");
            GD.Print("BATTLE_VALUES_PASS: eight real-engine scenarios, both seats, placard stack/katana/click/frame clearance, Light Armor toggle, native review controls/zoom/card input, live Force action/concession, two resize/reduced-motion cases.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckPlacardLayout()
    {
        foreach (var (scenario, lethal) in new[] { (0, false), (6, true), (7, false), (8, true) })
            foreach (var seat in new[] { 0, 1 })
            {
                _valuesReviewScenario = scenario; PrepareBattleValuesReview();
                if (seat == 1) _session.SwitchPracticeSeat();
                await CoverageFrames(); FocusBattleValues(); await CoverageFrames();
                var field = View.CurrentBattleProvince!.Value;
                RequireCamera(BattleValues(field)!.ProvinceFalls == lethal, "Engine flag differs from the scenario");
                VerifyBattleValuesGeometry();
                var tablePlacard = _provincePlacards.Single(p => p.Key.Province == BattlefieldProvince(field)).Value;
                _selected = null; tablePlacard.EmitSignal(BaseButton.SignalName.Pressed);
                RequireCamera(_selected is not null, "Pressing a table placard did not act like pressing its province");
            }
        // The Toggle armor button attaches / removes Light Armor; Force changes and the katana follows it.
        _valuesReviewScenario = 7; PrepareBattleValuesReview(); await CoverageFrames();
        var before = BattleValues(View.CurrentBattleProvince!.Value)!;
        RequireCamera(!before.ProvinceFalls && _placardStack.OfType<ProvincePlacard>().Single().GetNodeOrNull("ProvinceDestroyedIcon") is null, "Bare army already shows the katana");
        ToggleValuesArmor(); await CoverageFrames();
        var armed = BattleValues(View.CurrentBattleProvince!.Value)!;
        RequireCamera(armed.AttackerForce > before.AttackerForce && armed.ProvinceFalls && _placardStack.OfType<ProvincePlacard>().Single().GetNodeOrNull("ProvinceDestroyedIcon") is not null, "Attaching armor did not make the katana appear");
        ToggleValuesArmor(); await CoverageFrames();
        RequireCamera(!BattleValues(View.CurrentBattleProvince!.Value)!.ProvinceFalls && _placardStack.OfType<ProvincePlacard>().Single().GetNodeOrNull("ProvinceDestroyedIcon") is null, "Removing armor did not remove the katana");
        _valuesReviewScenario = 0;
    }

    /// <summary>Stack, order, alignment, engine agreement and frame clearance of every battle placard on screen.</summary>
    private void VerifyBattleValuesGeometry()
    {
        var field = View.CurrentBattleProvince!.Value; var v = BattleValues(field)!;
        var hasProvince = BattlefieldProvince(field) is not null && v.ProvinceStrength is not null;
        var seals = _placardStack.OfType<BattleSeal>().ToArray(); var plates = _placardStack.OfType<ProvincePlacard>().ToArray();
        RequireCamera(seals.Length == 2 && plates.Length == (hasProvince ? 1 : 0) && _placardStack.Count == 2 + plates.Length, "Stack is not opponent, province (when there is one), own");
        RequireCamera(_placardStack[0] == seals.Single(x => x.OwnerId != View.ViewerId) && _placardStack[^1] == seals.Single(x => x.OwnerId == View.ViewerId), "Stack order is wrong");
        RequireCamera(_placardStack.Zip(_placardStack.Skip(1)).All(pair => pair.First.Position.Y < pair.Second.Position.Y), "Stack is not top to bottom");
        RequireCamera(_placardStack.All(c => c.Size.IsEqualApprox(_placardStack[0].Size) && c.Scale.IsEqualApprox(_placardStack[0].Scale) && Mathf.IsEqualApprox(c.Position.X, _placardStack[0].Position.X)), "Stack placards differ in size or alignment");
        var attacker = seals.Single(x => x.OwnerId == v.AttackerId); var defender = seals.Single(x => x.OwnerId == v.DefenderId);
        RequireCamera(attacker.Role == "Attacker" && defender.Role == "Defender" && attacker.Force == v.AttackerForce && attacker.Units == v.AttackerUnits && defender.Force == v.DefenderForce && defender.Units == v.DefenderUnits, "Placard differs from engine");
        RequireCamera(plates.All(p => p.Strength == v.ProvinceStrength && p.Falls == v.ProvinceFalls && (p.GetNodeOrNull("ProvinceDestroyedIcon") is not null) == v.ProvinceFalls), "Province placard or katana differs from engine");
        var middle = _placardStack.Select(c => c.Position.Y + c.Size.Y * PlacardStackScale / 2).Average();
        RequireCamera(Math.Abs(middle - 540) < 2 && _placardStack.All(c => c.Position.X == 16), "Stack is not centred on the left edge");
        var crossing = _placardStack.Concat(_provincePlacards.Values).SelectMany(part => part.GetChildren().OfType<Control>().Where(child => !PlacardPlate.Inner(part.Size).Grow(.5f).Encloses(new Rect2(child.Position, child.Size)))
            .Select(child => $"{(child as Label)?.Text ?? child.Name} {new Rect2(child.Position, child.Size)} in {PlacardPlate.Inner(part.Size)}")).ToArray();
        RequireCamera(crossing.Length == 0, "Placard content crosses the frame border: " + string.Join("; ", crossing));
        RequireCamera(_provincePlacards.Count == View.Players.Single(p => p.Id != View.ActivePlayerId).Provinces.Count(p => !p.Destroyed), "Each undestroyed defending province needs a placard");
        RequireCamera(_provincePlacards.Keys.All(k => k.Owner != View.ActivePlayerId), "Attacker's own provinces carry a placard");
        if (hasProvince) RequireCamera(_provincePlacards.Single(p => p.Key.Province == BattlefieldProvince(field)).Value is { } table && table.Falls == v.ProvinceFalls && table.Strength == v.ProvinceStrength, "Table placard differs from engine");
    }
}
