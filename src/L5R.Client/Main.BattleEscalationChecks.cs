using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private ActionOption? BattleCasualtyAction() => Actions.FirstOrDefault(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId == "crushing-destroy" && a.TargetIds?.Length > 0);
    private async Task ProbeActionCasualty()
    {
        var action = BattleCasualtyAction() ?? throw new Exception("No legal Crushing Strength destruction in fixture.");
        var target = action.TargetIds![0]; var old = View;
        Choose(action); SelectCard(FindCard(target)!);
        ReviewedCommitButton().EmitSignal(Button.SignalName.Pressed);
        _uiQueue.Clear(); Render();
        var actual = _session.Snapshot!.View;
        InkRequire(actual.Version == old.Version + 1 && actual.Events.Any(e => e.Sequence > (old.Events.LastOrDefault()?.Sequence ?? 0) && e.Type == "attachmentDestroyed"), "Actual ability did not destroy its attachment.");
        if (!_reducedMotion) InkRequire(PresentationBusy && _defeatBanner.Visible, "Action destruction had no immediate named announcement.");
        if (!_reducedMotion) InkRequire(_sidebarHeader.GetChildren().OfType<BattleTracker>().All(t => t.Stage == 1 && t.Status == "Applying battle action"), "Individual action death incorrectly advanced the battle tracker to resolution.");
        await SaveBattleFrame("named-action-defeat");
        await ToSignal(GetTree().CreateTimer(5), SceneTreeTimer.SignalName.Timeout);
        InkRequire(!PresentationBusy && View.Version == actual.Version, "Action announcement did not restore decisions.");
        InkRequire(_journal.Aftermath.Any(r => r.Destination == "Fate discard") && _journal.Exchanges.Any(e => e.Events.Any(x => x.Type == "attachmentDestroyed")), "Action destruction lost its destination or log entry.");
        GD.Print("BATTLE_ACTION_DEFEAT_PASS: legal Crushing Strength through selection/confirmation; named announcement, actual discard transfer, aftermath and public record.");
    }
}
