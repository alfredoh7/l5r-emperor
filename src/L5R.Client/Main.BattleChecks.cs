using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _battleProbeDone;
    private string? _battleProbeError;
    private async Task SaveBattleFrame(string suffix)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(true);
        using var image = GetViewport().GetTexture().GetImage();
        var path = System.IO.Path.ChangeExtension(_capturePath!, null) + "-" + suffix + ".png";
        if (image.SavePng(path) != Error.Ok) throw new Exception("Could not capture " + path);
    }
    private async void ProbeBattleCourts()
    {
        try
        {
            await ToSignal(GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);
            RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            InkRequire(_battleDecor.Size.DistanceTo(_world.Size) < .01f && _battleDecor.DrawCalls > 0,
                $"Battle decoration layer has no drawable bounds; decor={_battleDecor.Size}, world={_world.Size}, draws={_battleDecor.DrawCalls}.");
            ValidateInkBoardTopology();
            ValidateInkCardClearance();
            var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
            foreach (var (index, court) in _battleCourts)
            {
                var bay = _matTargets.Single(s => s.Id == $"province:{defender.Id}:{index}");
                InkRequire(Math.Abs(court.GetCenter().X - bay.Bounds.GetCenter().X) < .02, "Court disconnected from its own province.");
                var rows = View.Players.Where(p => CourtUnits(p, index).Any()).ToArray();
                if (rows.Length == 2)
                {
                    var top = rows.Single(p => p.Id != View.ViewerId); var bottom = rows.Single(p => p.Id == View.ViewerId);
                    InkRequire(CourtUnits(top, index).Max(c => _battlePlacements[c.Id].Y + StackHeight(c, top)) <=
                        CourtUnits(bottom, index).Min(c => _battlePlacements[c.Id].Y), "Opposing units overlap or are on the wrong side.");
                }
                foreach (var player in rows)
                foreach (var unit in CourtUnits(player, index))
                    InkRequire(court.Encloses(new Rect2(_battlePlacements[unit.Id], new Vector2(UnitWidth(unit, player), StackHeight(unit, player)))), "Stack does not fit its court.");
            }
            foreach (var player in View.Players)
            {
                var home = _homeBounds[player.Id];
                var provinceY = ProvinceRowY(player);
                InkRequire(player.Id == Self.Id ? home.End.Y <= provinceY - MatHeader : home.Position.Y >= provinceY + PlaneHeight(CardHeight),
                    "Home Personalities crossed to the wrong side of their provinces.");
            }
            if (_captureMode.Contains("compact", StringComparison.Ordinal))
            {
                InkRequire(!BattleActive && _battleCourts.Count == 0 && _battleHeight == 0,
                    "A non-battle board kept empty battlefield space.");
                InkRequire(_laneWidths.All(w => Math.Abs(w - 218) < .01f), "Provinces retained battle-expanded lane widths.");
                GD.Print("COMPACT_BOARD_PASS: original 218-unit province pitch, no retained court band, Home on table-facing side from both seats.");
            }
            GD.Print($"BATTLE_COURT_PASS: {_battleCourts.Count} connected courts; both seats nearest their provinces; real stacks fit disjoint bounds.");
            await ProbeBattleJournalInput();
            if (_assignments.Count > 0)
            {
                var version = View.Version; var plans = new Dictionary<string, int>(_assignments);
                var previousStarts=new Dictionary<int,double>(_bloodStarts);
                if(Self.Id==View.ActivePlayerId) InkRequire(plans.Values.All(p=>!_bloodStarts.ContainsKey(p)),"Draft attack prematurely activated blood.");
                InkRequire(plans.All(p => FindCard(p.Key)?.Zone == Zone.Home && _battlePlacements.ContainsKey(p.Key)), "Preview moved a real unit.");
                InkRequire(IntentArrows().Count(a => a.Assignment) == plans.Count, "Missing proposed ink path.");
                var sourceRegion = _homeBounds[Self.Id];
                var region = _battleCourts.Values.Aggregate(sourceRegion, (a, b) => a.Merge(b)).Grow(24);
                if(_captureMode.Contains("blood-film",StringComparison.Ordinal))
                    region=_battleCourts.Values.Aggregate((a,b)=>a.Merge(b)).Grow(110);
                _cameraCenter = region.GetCenter(); _cameraZoom = FitRegion(region); _cameraFitted = false; ApplyTableCamera();
                await SaveBattleFrame("planned");
                GD.Print($"BLOOD_MOVIE_START: frame {Engine.GetProcessFrames()}.");
                _decisionActions.GetChildren().OfType<Button>().Single(b => b.Text == "Confirm assignments").EmitSignal(Button.SignalName.Pressed);
                _uiQueue.Clear(); Render();
                InkRequire(View.Version == version + 1 && _assignments.Count == 0 && !IntentArrows().Any(a => a.Assignment), "Confirmation failed to replace previews.");
                foreach (var (id, province) in plans)
                {
                    var root = FindCard(id)!;
                    InkRequire(root.Zone == Zone.Battlefield && root.ProvinceIndex == province, "Confirmed unit has wrong engine destination.");
                    InkRequire(Self.Army.Where(c => c.AttachedTo == id).All(c => c.ProvinceIndex == province), "Attachments left their unit.");
                }
                if(Self.Id==View.ActivePlayerId) InkRequire(plans.Values.All(_bloodStarts.ContainsKey),"Committed attack did not activate blood.");
                else InkRequire(previousStarts.All(p=>_bloodStarts.GetValueOrDefault(p.Key)==p.Value),"Defender assignment restarted blood.");
                await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                await SaveBattleFrame("blood-first");
                await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
                await SaveBattleFrame("blood-soaking");
                var starts=new Dictionary<int,double>(_bloodStarts);
                RenderBoard(); Highlight();
                InkRequire(starts.All(p=>_bloodStarts.GetValueOrDefault(p.Key)==p.Value),"Rerender or highlight replayed blood.");
                await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
                InkRequire(TableMotionsSettled() && !_matSections.IsAnimating, "Confirmation motion did not settle.");
                FocusBattle();
                GD.Print($"BLOOD_MOVIE_END: frame {Engine.GetProcessFrames()}.");
                GD.Print("BATTLE_ASSIGNMENT_PASS: actual tile/province signals, cancel and confirmation; preview leaves authority unchanged; all attachments follow root; ink settles after cards.");
            }
            if(_captureMode.Contains("casualty", StringComparison.Ordinal)) await ProbeActionCasualty();
            if (_captureMode.Contains("fall", StringComparison.Ordinal) || _captureMode.Contains("deaths",StringComparison.Ordinal))
            {
                FocusBattle();
                var old = View;
                var oldBays = _matTargets.Where(s => s.ProvinceNumber.HasValue).ToDictionary(s => s.Id, s => s.Bounds);
                await SaveBattleFrame("before");
                GD.Print($"BATTLE_MOVIE_START: frame {Engine.GetProcessFrames()}; fixed capture can use --write-movie --fixed-fps 24.");
                var step = 0;
                while (_session.Snapshot!.View.Phase == Phase.Battle && !PresentationBusy && step++ < 3)
                {
                    var live = _session.Snapshot.View;
                    await _session.Submit(new GameCommand("pass", live.PriorityPlayerId, live.Version));
                    _uiQueue.Clear(); Render();
                }
                var actual = _session.Snapshot!.View;
                var expected=_captureMode.Contains("deaths",StringComparison.Ordinal)?"unitDestroyed":"provinceDestroyed";
                InkRequire(actual.Events.Any(e => e.Sequence > (old.Events.LastOrDefault()?.Sequence ?? 0) && e.Type == expected), "Fixture did not produce a real "+expected);
                if (!_reducedMotion)
                {
                    InkRequire(PresentationBusy && _battlePresentationStage == 1 && View.Version < actual.Version && Actions.Length == 0, "Outcome beat did not hold the permitted old view with actions unavailable.");
                    InkRequire(_inspector.GetChildren().OfType<BattleTracker>().All(t => t.Stage == 2 && t.Status == "Resolving battle"), "Resolution still invites a battle action.");
                    await SaveBattleFrame("outcome");
                    await ToSignal(GetTree().CreateTimer(_outcomeHold+.08), SceneTreeTimer.SignalName.Timeout);
                    await SaveBattleFrame("transfers");
                    await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
                    await SaveBattleFrame("fracture");
                }
                await ToSignal(GetTree().CreateTimer(3.2), SceneTreeTimer.SignalName.Timeout);
                InkRequire(!PresentationBusy && View.Version == actual.Version && TableMotionsSettled(), "Presentation did not return to authoritative state.");
                InkRequire(_journal.Outcome.Length>0 && _journal.Exchanges.Any(e=>e.Events.Any(x=>x.Type==expected)),"Result missing from battle exchanges.");
                if(expected=="unitDestroyed") InkRequire(_journal.Aftermath.Any(r=>r.Destination=="Dead"),"Named death has no destination in aftermath.");
                foreach (var player in View.Players)
                foreach (var province in player.Provinces)
                {
                    var id = $"province:{player.Id}:{province.Index}";
                    if (province.Destroyed)
                    {
                        var world = new Vector2(SlotX(player, 2+province.Index)+CardHeight/2, ProvinceRowY(player)+PlaneHeight(CardHeight)+MatPadding+30);
                        GD.Print($"FALL_MARKER: {id} time={_fallTimes.GetValueOrDefault((player.Id,province.Index))} clock={_battleVisualTime} screen={WorldToScreen(world)} draw={_battleDecor.DrawCalls}");
                        InkRequire(!_provinceButtons.ContainsKey((player.Id, province.Index)) && _matTargets.All(s => s.Id != id), "Destroyed province retained a playable bay.");
                    }
                    else if (oldBays.TryGetValue(id, out var bounds))
                        InkRequire(InkSame(bounds, _matTargets.Single(s => s.Id == id).Bounds), $"Surviving province {id} changed from {bounds} to {_matTargets.Single(s => s.Id == id).Bounds} during resolution.");
                }
                GD.Print($"BATTLE_MOVIE_END: frame {Engine.GetProcessFrames()}.");
                if (_session.ExportPracticeReplay() is { } replay) System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(_capturePath!, "replay.json"), replay);
                ShowHistory();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                foreach (var window in GetChildren().OfType<Window>()) window.QueueFree();
                GD.Print($"BATTLE_FALL_PASS: real battleResolved/provinceDestroyed events; staged outcome, actual transfers, fractured/extinguished marker; original slots stable; no target remains; public history opens; reduced={_reducedMotion}.");
            }
            await SaveBattleFrame("review");
        }
        catch (Exception error) { _battleProbeError = error.ToString(); GD.PushError(_battleProbeError); }
        finally { _battleProbeDone = true; }
    }
}
