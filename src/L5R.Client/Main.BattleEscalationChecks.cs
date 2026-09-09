using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private ActionOption? BattleCasualtyAction() => Actions.FirstOrDefault(a=>a.Enabled && a.Type=="useAbility" && a.Command?.AbilityId=="crushing-destroy" && a.TargetIds?.Length>0);
    private async Task ProbeBattleJournalInput()
    {
        if(!JournalAvailable) return;
        if(!JournalOpen) { _journalCollapsed=false; RenderBattleJournal(); FitTableViewport(); }
        var version=View.Version; var zoom=_cameraZoom; var center=_cameraCenter;
        var point=_journalPanel.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position=point,GlobalPosition=point });
        Input.ParseInputEvent(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.WheelDown,Pressed=true });
        Input.ParseInputEvent(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=MouseButton.WheelDown,Pressed=false });
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        InkRequire(_cameraZoom==zoom && _cameraCenter==center,"Journal scrolling hijacked camera zoom/pan.");
        InkRequire(!_journalPanel.GetGlobalRect().Intersects(_tableViewport.GetGlobalRect()) && !_journalPanel.GetGlobalRect().Intersects(_inspectorPanel.GetGlobalRect()),"Battle log overlaps interactive table or inspector.");
        async Task ClickToggle()
        {
            GetWindow().GrabFocus();
            await ToSignal(GetTree().CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);
            var p=_journalToggle.GetGlobalRect().GetCenter();
            GetViewport().WarpMouse(p);
            GetViewport().PushInput(new InputEventMouseMotion { Position=p,GlobalPosition=p },true);
            await ToSignal(GetTree().CreateTimer(.06),SceneTreeTimer.SignalName.Timeout);
            GetViewport().PushInput(new InputEventMouseButton { Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true },true);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GetViewport().PushInput(new InputEventMouseButton { Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false },true);
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            GD.Print($"JOURNAL_CLICK: point={p}, open={JournalOpen}, viewport={_tableViewport.Position}, focus={GetWindow().HasFocus()}, hover={GetViewport().GuiGetHoveredControl()?.GetPath()}, text={(GetViewport().GuiGetHoveredControl() as Button)?.Text}, target={_journalToggle.GetPath()}");
        }
        await ClickToggle(); InkRequire(!JournalOpen && _tableViewport.Position.X==144,"Native collapse did not reclaim board space.");
        await ClickToggle(); InkRequire(JournalOpen && _tableViewport.Position.X==292,"Native expand did not reserve log space.");
        ReleaseTableFocus();
        _journalPanel.ShowLatest();
        InkRequire(View.Version==version,"Reading the log submitted a game action.");
        GD.Print("BATTLE_JOURNAL_INPUT_PASS: native wheel/click routing; collapse/expand; no board/sidebar overlap or authoritative mutation.");
    }
    private async Task ProbeActionCasualty()
    {
        var action=BattleCasualtyAction()??throw new Exception("No legal Crushing Strength destruction in fixture.");
        var target=action.TargetIds![0]; var old=View;
        Choose(action); SelectCard(FindCard(target)!);
        _decisionActions.GetChildren().OfType<Button>().Single(b=>b.Text.StartsWith("Confirm action",StringComparison.Ordinal)).EmitSignal(Button.SignalName.Pressed);
        _uiQueue.Clear(); Render();
        var actual=_session.Snapshot!.View;
        InkRequire(actual.Version==old.Version+1 && actual.Events.Any(e=>e.Sequence>(old.Events.LastOrDefault()?.Sequence??0) && e.Type=="attachmentDestroyed"),"Actual ability did not destroy its attachment.");
        if(!_reducedMotion) InkRequire(PresentationBusy && _defeatBanner.Visible,"Action destruction had no immediate named announcement.");
        if(!_reducedMotion) InkRequire(_inspector.GetChildren().OfType<BattleTracker>().All(t=>t.Stage==1 && t.Status=="Applying battle action"),"Individual action death incorrectly advanced the battle tracker to resolution.");
        await SaveBattleFrame("named-action-defeat");
        await ToSignal(GetTree().CreateTimer(5),SceneTreeTimer.SignalName.Timeout);
        InkRequire(!PresentationBusy && View.Version==actual.Version,"Action announcement did not restore decisions.");
        InkRequire(_journal.Aftermath.Any(r=>r.Destination=="Fate discard") && _journal.Exchanges.Any(e=>e.Events.Any(x=>x.Type=="attachmentDestroyed")),"Action destruction lost its destination or log entry.");
        GD.Print("BATTLE_ACTION_DEFEAT_PASS: legal Crushing Strength through selection/confirmation; named announcement, actual discard transfer, aftermath and public record.");
    }
}
