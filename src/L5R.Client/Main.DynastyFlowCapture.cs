using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #659 native probe (mode published-supports-dynasty-flow): the table camera snaps back to the fitted view once per turn when the Dynasty Phase
// begins, and End Dynasty asks for confirmation only when nothing was bought, overlaid or discarded. Real engine, real button clicks.
public partial class Main
{
    private void PanTableForProbe()
    {
        _cameraFitted = false; _cameraZoom = FitZoom * 1.5f; _cameraCenter += new Vector2(220, 140); ApplyTableCamera();
    }

    private ConfirmationDialog? OpenConfirmDialog() => GetChildren().OfType<ConfirmationDialog>().FirstOrDefault(d => d.Visible && !d.IsQueuedForDeletion());

    private async Task CheckDynastyFlow()
    {
        MatchEngine Dynasty(bool actionTaken)
        {
            var state = SupportFixture().State; state.Phase = Phase.Dynasty; state.ActivePlayerId = state.PriorityPlayerId = 0;
            state.Players[0].DynastyActionTaken = actionTaken;
            return (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [state])!;
        }
        // 1. Camera: the first render of a turn's Dynasty Phase resets it; later renders leave the player's own pan/zoom alone; a busy presentation defers it.
        var engine = Dynasty(false); await LoadSupportFixture(engine, 0);
        _framedDynasty = null; PanTableForProbe(); SupportRequire(!_cameraFitted, "probe could not pan the table");
        _battlePresentationStage = 2; FrameDynasty();
        SupportRequire(!_cameraFitted && _framedDynasty is null, "camera must not reset while a battle presentation is still running");
        _battlePresentationStage = 0; Render();
        SupportRequire(_cameraFitted && _framedDynasty is not null, "camera did not reset on entering Dynasty");
        PanTableForProbe(); Render();
        SupportRequire(!_cameraFitted, "camera reset again within the same Dynasty Phase and fought the player's pan/zoom");
        GD.Print("DYNASTY_FLOW: camera resets once per turn on entering Dynasty, waits for presentation");

        // 2. Nothing bought or discarded: End Dynasty asks first; Cancel keeps the phase, Confirm ends it.
        var end = Actions.Single(a => a.Type == "endDynasty" && a.Enabled);
        var version = engine.State.Version;
        await SupportClick(StabilizationButtons(_root).First(b => b.IsVisibleInTree() && b.Text == end.Label)); await EventFrames(10);
        var dialog = OpenConfirmDialog(); SupportRequire(dialog is not null, "End Dynasty with nothing bought or discarded must ask for confirmation");
        SupportRequire(engine.State.Version == version && engine.State.Phase == Phase.Dynasty, "the command must wait for the answer");
        dialog!.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(10);
        SupportRequire(engine.State.Version == version && engine.State.Phase == Phase.Dynasty, "Cancel must keep the Dynasty Phase");
        await SupportClick(StabilizationButtons(_root).First(b => b.IsVisibleInTree() && b.Text == end.Label)); await EventFrames(10);
        dialog = OpenConfirmDialog(); SupportRequire(dialog is not null, "the confirmation must appear again after cancelling");
        dialog!.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(30);
        SupportRequire(engine.State.Version > version && engine.State.Phase != Phase.Dynasty, "Confirm must end the Dynasty Phase");
        GD.Print("DYNASTY_FLOW: empty Dynasty asks first; cancel keeps the phase; confirm ends it");

        // 3. Something bought or discarded already (flag set): no prompt.
        engine = Dynasty(true); await LoadSupportFixture(engine, 0); version = engine.State.Version;
        end = Actions.Single(a => a.Type == "endDynasty" && a.Enabled);
        await SupportClick(StabilizationButtons(_root).First(b => b.IsVisibleInTree() && b.Text == end.Label)); await EventFrames(30);
        SupportRequire(OpenConfirmDialog() is null && engine.State.Version > version && engine.State.Phase != Phase.Dynasty, "End Dynasty after a purchase or discard must not ask");
        GD.Print("DYNASTY_FLOW: no prompt once something was bought or discarded");
    }
}
