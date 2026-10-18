using System.Reflection;
using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #993: native proof of click-and-drag with real (tagged) pointer events. Every drag is a press, motion past the threshold, motion to the target
// and a release; the checks assert what the matching clicks would have produced, that nothing is submitted, that Esc or a refused drop changes nothing,
// that a press that does not travel is still a click, and that a drag from a card never pans the camera.
public partial class Main
{
    private bool _dragCaptureStarted;

    private void ProcessDragCapture(double delta)
    {
        _captureClock += delta; if (_dragCaptureStarted || _captureClock < 1.5) return;
        _dragCaptureStarted = true; _ = CheckDrag();
    }

    private void DragPush(InputEventMouse input) { input.SetMeta(CaptureMouseTag, true); GetViewport().PushInput(input, true); }
    private async Task DragBegin(Vector2 from)
    {
        DragPush(new InputEventMouseMotion { Position = from, GlobalPosition = from }); await EventFrames(3);
        DragPush(new InputEventMouseButton { Position = from, GlobalPosition = from, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }); await EventFrames(2);
    }
    private async Task DragMove(Vector2 from, Vector2 to)
    {
        var step = from + new Vector2(24, 10);
        DragPush(new InputEventMouseMotion { Position = step, GlobalPosition = step, Relative = step - from, ButtonMask = MouseButtonMask.Left }); await EventFrames(3);
        DragPush(new InputEventMouseMotion { Position = to, GlobalPosition = to, Relative = to - step, ButtonMask = MouseButtonMask.Left }); await EventFrames(6);
    }
    private async Task DragRelease(Vector2 at)
    {
        DragPush(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(10);
    }
    private async Task DragAndDrop(Vector2 from, Vector2 to) { await DragBegin(from); await DragMove(from, to); SupportRequire(_dragging && _dragGhost is not null, "The pointer moved past the threshold but no drag started"); await DragRelease(to); }

    private Vector2 DragPointOf(string cardId) => ScreenCenter(_tiles.Single(t => t.Card.Id == cardId));
    private Vector2 DragBay(int owner, int province) => WorldToScreen(_matTargets.Single(s => s.Id == $"province:{owner}:{province}").Bounds.GetCenter());
    private Vector2 DragCloth()
    {
        var rect = _tableViewport.GetGlobalRect().Grow(-16);
        var fabric = TableArtStage.FabricBounds(new Vector2(WorldWidth, _worldHeight)).Grow(-16);
        for (var y = rect.Position.Y; y < rect.End.Y; y += 24)
            for (var x = rect.Position.X; x < rect.End.X; x += 24)
            {
                var point = new Vector2(x, y);
                if (PointerOnTable(point) && !OverStatToken(point) && fabric.HasPoint(ScreenToWorld(point)) && DragDestination(point) is null && TableHovered is not CardTile) return point;
            }
        throw new Exception("No clear cloth to drop on");
    }
    private async Task DragShot(string suffix)
    {
        var path = Path.ChangeExtension(_capturePath, null) + suffix + ".png";
        SupportRequire(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "Could not save " + path);
        await Task.CompletedTask;
    }

    private async Task CheckDrag()
    {
        try
        {
            // 1. Maneuvers: assign by dragging, re-assign by dragging, send home by dragging off every battlefield; none of it submits.
            {
                var state = MatchEngine.CreatePublishedTestMatch(42).State;
                foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
                state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null; state.Phase = Phase.AttackDeclaration;
                SupportFixtureCard(state, 229, 0, Zone.Home);
                var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
                SupportRequire(engine.Execute(new GameCommand("declareAttack", 0, engine.Version)).Accepted, "Could not declare the attack");
                await LoadSupportFixture(engine, 0);
                var attacker = View.Players[0].Home.Single(c => c.Definition?.Type == CardType.Personality);
                var provinces = Actions.First(a => a.Enabled && a.Type == "attack").Provinces!;
                SupportRequire(provinces.Length >= 2, "The fixture offers fewer than two battlefields");
                var version = View.Version; var camera = _cameraCenter;
                await DragBegin(DragPointOf(attacker.Id)); await DragMove(DragPointOf(attacker.Id), DragBay(1, provinces[0]));
                await DragShot("-dragging");
                await DragRelease(DragBay(1, provinces[0]));
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var first) && first == provinces[0], "Dragging the unit onto a province did not assign it");
                SupportRequire(View.Version == version && _chosen is { Type: "attack" }, "The drag submitted or did not start the assignment");
                SupportRequire(_cameraCenter == camera && !_dragging && _dragGhost is null, "The drag panned the camera or left its ghost behind");
                await DragBegin(DragPointOf(attacker.Id)); await DragMove(DragPointOf(attacker.Id), DragBay(1, provinces[1]));
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var held) && held == provinces[0], "Picking the unit up changed its assignment");
                await DragRelease(DragBay(1, provinces[1]));
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var still) && still == provinces[1], "The drop did not reassign"); // (a click-through on pick-up would have removed it first)
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var second) && second == provinces[1], "Dragging an assigned unit onto another province did not reassign it");
                // A drop the engine does not list, and an escaped drag, change nothing.
                var other = Enumerable.Range(0, 5).FirstOrDefault(i => !provinces.Contains(i) && _matTargets.Any(s => s.Id == $"province:1:{i}"), -1);
                if (other >= 0) { await DragAndDrop(DragPointOf(attacker.Id), DragBay(1, other)); SupportRequire(_assignments[attacker.Id] == provinces[1], "A province the engine does not offer took the unit"); }
                await DragBegin(DragPointOf(attacker.Id)); await DragMove(DragPointOf(attacker.Id), DragBay(1, provinces[0]));
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true); await EventFrames(3);
                await DragRelease(DragBay(1, provinces[0]));
                SupportRequire(!_dragging && _assignments.TryGetValue(attacker.Id, out var kept) && kept == provinces[1], "Esc did not cancel the drag, or the cancelled gesture clicked the unit");
                // Off every battlefield: home. A drop on your own province is not "off the battlefield", so it changes nothing.
                await DragAndDrop(DragPointOf(attacker.Id), DragBay(0, 0));
                SupportRequire(_assignments.ContainsKey(attacker.Id), "Dropping on your own province sent the unit home");
                await DragAndDrop(DragPointOf(attacker.Id), DragCloth());
                SupportRequire(!_assignments.ContainsKey(attacker.Id), "Dropping the unit off every battlefield did not send it home");
                // A press that does not travel is a click, and the click path still assigns (select the unit, then the province).
                var tile = _tiles.Single(t => t.Card.Id == attacker.Id); var at = ScreenCenter(tile);
                DragPush(new InputEventMouseMotion { Position = at, GlobalPosition = at }); await EventFrames(3);
                DragPush(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }); await EventFrames(2);
                var nudge = at + new Vector2(3, 2); DragPush(new InputEventMouseMotion { Position = nudge, GlobalPosition = nudge, ButtonMask = MouseButtonMask.Left }); await EventFrames(2);
                DragPush(new InputEventMouseButton { Position = nudge, GlobalPosition = nudge, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(8);
                SupportRequire(!_dragging && _assignmentUnit == attacker.Id, "A press that did not travel was not an ordinary click");
                SelectProvince(1, provinces[0]); await EventFrames(4);
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var clicked) && clicked == provinces[0], "Click assignment stopped working");
                SupportRequire(View.Version == version, "A drag or click submitted before Confirm");
            }

            // 2. A move the player has started (Ninube Hajime: "Move him to a different location"): drag the target to a battlefield, or outside every battlefield for home.
            {
                var state = MatchEngine.CreatePublishedTestMatch(42).State;
                state.Phase = Phase.Battle; state.ActivePlayerId = 0; state.PriorityPlayerId = 0; state.CurrentBattleProvince = 1; state.PendingChoice = null; state.UnresolvedBattlefields = [0, 1, 2];
                var hajime = SupportFixtureCard(state, 10090, 0, Zone.Battlefield, 1);
                var enemy = SupportFixtureCard(state, 3636, 1, Zone.Battlefield, 1);
                var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
                await LoadSupportFixture(engine, 0);
                var toBattlefield = Actions.SingleOrDefault(a => a.CardId == hajime.Id && a.Movement is not null && a.Enabled);
                var toHome = Actions.SingleOrDefault(a => a.CardId == hajime.Id && a.MoveTo == MoveSpec.Home && a.Enabled);
                SupportRequire(toBattlefield is not null && toHome is not null, "Ninube Hajime does not offer both moves: " + string.Join(" | ", Actions.Where(a => a.CardId == hajime.Id).Select(a => $"{a.Label}/{a.MoveTo}/{a.Enabled}/{a.Reason}")));
                var version = View.Version;
                Choose(toBattlefield!); await EventFrames(6);
                SupportRequire(MovementActive && _chosen!.Command!.TargetId is null && _chosen.Command.Province is null, "The move did not start undecided");
                var destination = toBattlefield!.Provinces![0];
                await DragAndDrop(DragPointOf(enemy.Id), DragBay(1, destination));
                SupportRequire(_chosen!.Command!.TargetId == enemy.Id && _chosen.Command.Province == destination, "Dragging the target onto a province did not choose the target and its destination");
                SupportRequire(View.Version == version && MovementActive, "The move submitted before Confirm");
                var refused = Enumerable.Range(0, 3).First(i => !toBattlefield.Provinces.Contains(i));
                await DragAndDrop(DragPointOf(enemy.Id), DragBay(1, refused));
                SupportRequire(_chosen.Command.Province == destination, "A destination the engine does not offer was taken");
                // Home: choose that action, then drop the target outside every battlefield; a drop on a battlefield does not choose it.
                CancelChoice(); await EventFrames(4);
                toHome = Actions.Single(a => a.CardId == hajime.Id && a.MoveTo == MoveSpec.Home && a.Enabled);
                Choose(toHome); await EventFrames(6);
                SupportRequire(!MovementActive && _chosen!.Command!.TargetId is null, "The home move did not start undecided");
                SupportRequire(_battleCourts.TryGetValue(1, out var court), "The current battlefield has no box on the table");
                await DragAndDrop(DragPointOf(enemy.Id), WorldToScreen(court.GetCenter()));
                SupportRequire(_chosen!.Command!.TargetId is null, "Dropping on the battlefield chose a move home");
                await DragAndDrop(DragPointOf(enemy.Id), DragCloth());
                SupportRequire(_chosen!.Command!.TargetId == enemy.Id && View.Version == version, "Dropping the target outside the battlefield did not choose it for the move home");
            }

            GD.Print("DRAG_PASS: a unit is assigned by dragging it onto a province, re-assigned by dragging it onto another, sent home by dropping it off every battlefield, and nothing is submitted before Confirm; a refused drop, a drop on your own province and Esc change nothing; a press that does not travel is still a click; a drag never pans the camera; a started move takes its target and destination from a drag, and a move home takes its target from a drop outside the battlefield.");
            GetTree().Quit(0);
        }
        catch (Exception exception) { GD.PushError("DRAG_FAIL: " + exception); GetTree().Quit(1); }
    }
}
