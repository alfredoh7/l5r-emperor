using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _createdCaptureStarted, _createdReviewBusy;
    private MatchEngine _createdFixture = null!;
    private string _createdId = "", _createdOriginal = "";
    private void ProcessCreatedCopyCapture(double delta)
    {
        _captureClock += delta;
        if (_createdCaptureStarted || _captureClock < 1.5) return;
        _createdCaptureStarted = true; _ = CaptureCreatedCopy();
    }
    private static MatchEngine CreatedFixtureEngine(MatchState state) => (MatchEngine)typeof(MatchEngine)
        .GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
    private async Task PrepareCreatedReview(string mode = "home")
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var ordinary = mode is "ordinary" or "removed";
        var original = SupportFixtureCard(state, ordinary ? 2060 : 9785, 0, Zone.Home); _createdOriginal = original.Id;
        string creatorId, ability;
        if (ordinary)
        {
            creatorId = SupportFixtureCard(state, "training-copyist", 0, Zone.Home).Id; ability = "training-copy-personality";
        }
        else
        {
            var shugenja = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Personality && d.Keywords?.Contains("Shugenja") == true);
            var caster = SupportFixtureCard(state, shugenja.Id, 0, Zone.Home);
            var spell = SupportFixtureCard(state, 10707, 0, Zone.Attached); spell.AttachedTo = caster.Id;
            creatorId = spell.Id; ability = "gates-of-chikushudo-copy";
        }
        var creation = CreatedFixtureEngine(state);
        var offered = creation.QueryActions(0).Single(a => a.Enabled && a.CardId == creatorId && a.Command?.AbilityId == ability);
        var before = creation.Version;
        creation.Execute(offered.Command! with { TargetId = original.Id });
        SupportRequire(creation.Version > before, "Actual copy creation failed");
        state = creation.State;
        var copy = state.Cards.Values.Single(c => c.CreatedCopy); _createdId = copy.Id;
        var battle = mode is "battle" or "removed";
        if (battle)
        {
            state.Phase = Phase.Battle; state.ActivePlayerId = 1; state.PriorityPlayerId = 0;
            state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0];
            foreach (var id in new[] { _createdId, _createdOriginal }) { state.Cards[id].Zone = Zone.Battlefield; state.Cards[id].ProvinceIndex = 0; }
            SupportFixtureCard(state, 3636, 1, Zone.Battlefield, 0).Modifiers = [new("review-fixture", "review-fixture", "Force", 20)];
        }
        if (mode == "crowded")
        {
            copy.Bowed = true; copy.Dishonorable = true;
            copy.Tokens = [new("created-review-training", "Training", "review-fixture", "Force", 1)];
            copy.GrantedAbilities = [new("water-bo-bow", "review-fixture", GrantDuration.Permanent)];
            SupportFixtureCard(state, 10118, 0, Zone.Attached).AttachedTo = copy.Id;
            for (var i = 0; i < 6; i++) SupportFixtureCard(state, i % 2 == 0 ? 2060 : 3636, i % 2, Zone.Home, bowed: i % 3 == 0);
        }
        _createdFixture = CreatedFixtureEngine(state);
        _resolutionMessage = null;
        await LoadSupportFixture(_createdFixture, 0);
        SelectCard(FindCard(_createdId)!); HideTableTooltip(); ReleaseTableFocus();
        if (battle) { FocusBattle(); await EventFrames(30); }
        if (mode == "removed")
        {
            for (var n = 0; n < 2; n++)
            {
                if (View.ViewerId != View.PriorityPlayerId) { _session.SwitchPracticeSeat(); await EventFrames(10); }
                await _session.Submit(Actions.Single(a => a.Enabled && a.Type == "pass").Command!); await WaitDecisionPresentation();
            }
            SupportRequire(_createdFixture.State.Cards[_createdId].Zone == Zone.RemovedFromGame && _createdFixture.State.Cards[_createdOriginal].Zone == Zone.Dead, "Copy did not leave the game separately from ordinary death");
            if (View.ViewerId != 0) { _session.SwitchPracticeSeat(); await EventFrames(15); }
            ShowRemovedGallery(0); await EventFrames(15);
        }
        if (_captureMode.EndsWith("-review")) BuildCreatedReviewControls();
        await EventFrames(15);
        GD.Print("CREATED_COPY_SCENARIO: " + mode);
    }
    private void BuildCreatedReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("CreatedCopyReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "CreatedCopyReviewControls", ZIndex = 91 }; row.AddThemeConstantOverride("separation", 10);
        var choices = new[] { ("Gates of Chikushudo · Home", "home"), ("Copy in battle", "battle"), ("Bowed attachment stack*", "crowded"), ("Ordinary Personality copy*", "ordinary"), ("Destroyed copy · Removed gallery*", "removed") };
        var picker = new OptionButton { FitToLongestItem = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(370, 44), TooltipText = "Prepared positions; * includes synthetic training effects." };
        picker.AddItem("Review another created-copy scenario…"); foreach (var (label, _) in choices) picker.AddItem(label);
        picker.ItemSelected += i => { if (i > 0) _ = RunCreatedReview(choices[(int)i - 1].Item2); }; row.AddChild(picker);
        var other = SidebarSkin.Button("Other seat", () => _session.SwitchPracticeSeat()); other.CustomMinimumSize = new(150, 44); row.AddChild(other);
        Place(_root, row, 350, 114, Math.Min(740, BoardWidth - 374), 46);
    }
    private async Task RunCreatedReview(string mode)
    {
        if (_createdReviewBusy) return; _createdReviewBusy = true;
        try { await PrepareCreatedReview(mode); }
        catch (Exception e) { SetNotice("Choose a scenario to restart the prepared demo."); GD.PushWarning(e.ToString()); }
        finally { _createdReviewBusy = false; }
    }
    private async Task WaitCreatedFocus()
    {
        var announced = false;
        for (var i = 0; i < 18000; i++)
        {
            if (GetWindow().HasFocus())
            {
                await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
                if (GetWindow().HasFocus()) return;
            }
            if (!announced) { GD.Print("CREATED_COPY_FOCUS_REQUIRED: activate the game window."); announced = true; }
            await EventFrames(1);
        }
        throw new Exception("Native window requires OS focus");
    }
    private async Task ClickCreatedSeal()
    {
        await WaitCreatedFocus();
        var seal = _tiles.Single(t => t.Card.Id == _createdId).CopySeal!;
        var point = seal.CenterOnViewport();
        PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(8);
        GD.Print($"CREATED_SEAL_INPUT: {point}; hover={TableHovered?.GetType().Name}; focus={GetWindow().HasFocus()}; blocked={CameraBlocked}");
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(12);
    }
    private void CheckCreatedMarkers()
    {
        SupportRequire(_tiles.All(t => (t.CopySeal is not null) == CreatedCopyStatus.VisibleOn(t.Card)), "Created-copy flag and marker differ");
        var tile = _tiles.Single(t => t.Card.Id == _createdId);
        var seal = tile.CopySeal!;
        SupportRequire(!Geometry2D.IsPointInPolygon(seal.CenterOnViewport(), tile.FaceCornersOnViewport()), "Seal covers the printed face");
        SupportRequire(_tiles.Single(t => t.Card.Id == _createdOriginal).CopySeal is null, "Original received a copy seal");
        if (tile.TokenRail is { } rail) SupportRequire(!Geometry2D.IsPointInPolygon(seal.CenterOnViewport(), rail.CornersOnViewport()), "Copy seal overlaps token rail");
    }
    private async Task CaptureCreatedCopy()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareCreatedReview();
            if (_captureMode.EndsWith("-review")) { GD.Print("CREATED_COPY_REVIEW_READY: Gates created Lion Ancestor; scenario picker includes clearly marked synthetic cases."); return; }
            await WaitCreatedFocus(); await EventFrames(40);
            CheckCreatedMarkers(); var version = View.Version; DeselectCard(); await ClickCreatedSeal();
            SupportRequire(_selected?.Id == _createdId && View.Version == version && StabilizationText(_inspector).Contains("Created copy"), "Seal did not inspect without changing state");
            await SupportCapture("-home");
            DeselectCard(); var tile = _tiles.Single(t => t.Card.Id == _createdId); var point = ScreenCenter(tile);
            PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout); await EventFrames(4);
            SupportRequire(_tableTooltip is not null && StabilizationText(_tableTooltip).Contains("Removed from the game when it leaves play."), $"Hover meaning missing: type={_tableHover?.GetType().Name}, actual={TableHovered?.GetType().Name}, time={_tableHoverTime}, last={_lastTableMouse}, point={point}, blocked={CameraBlocked}");
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_tableTooltip!.GetGlobalRect()), "Hover clipped"); await SaveShownCapture("-hover"); HideTableTooltip();
            _session.SwitchPracticeSeat(); await EventFrames(30); CheckCreatedMarkers(); await ClickCreatedSeal();
            SupportRequire(_selected?.Id == _createdId, "Opponent cannot inspect public seal"); await SupportCapture("-opponent");
            await PrepareCreatedReview("battle"); CheckCreatedMarkers(); await SupportCapture("-battle");
            await PrepareCreatedReview("crowded"); CheckCreatedMarkers(); await SupportCapture("-bowed");
            _expandedUnits.Add(_createdId); RenderBoard(); await EventFrames(20); CheckCreatedMarkers(); await SupportCapture("-stack");
            await PrepareCreatedReview("ordinary"); CheckCreatedMarkers();
            SupportRequire(FindCard(_createdId) is { CreatedCopy: true, Definition.CreatedOnly: false }, "Ordinary copied Personality not covered");
            SupportRequire(!CreatedCopyStatus.VisibleOn(FindCard(_createdId)! with { FaceUp = false, Definition = null }), "Hidden identity exposes a marker"); await SupportCapture("-ordinary");
            await PrepareCreatedReview("removed");
            var dialog = GetChildren().OfType<AcceptDialog>().Last();
            var removed = Descendants(dialog).OfType<CardTile>().Single(t => t.Card.Id == _createdId);
            SupportRequire(removed.CopySeal is not null && StabilizationText(dialog).Contains("Created copy"), "Removed gallery lost marker or caption");
            SupportRequire(!View.Players.Any(p => p.Discard.Any(c => c.Id == _createdId)), "Copy appeared in discard"); await SupportCapture("-removed");
            GD.Print("CREATED_COPY_PASS: actual creation; original/ordinary distinction; native seal inspection; both seats; bounded hover; battle/bowed/attachments; actual destruction to public Removed gallery."); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
