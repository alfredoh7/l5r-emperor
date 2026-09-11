using Godot;
namespace L5R.Client;

public partial class Main
{
    private void ValidateLacquerMenus()
    {
        if (!_captureMode.StartsWith("published-lacquer-", StringComparison.Ordinal) && _captureMode != "published-rules") return;
        var dialog = GetChildren().OfType<AcceptDialog>().LastOrDefault(d => !d.IsQueuedForDeletion());
        if (dialog is null) return; // Card and tooltip fixtures are non-modal.
        var version = View.Version;
        var controls = HudDescendants(dialog).OfType<Control>().ToArray();
        var close = dialog.GetOkButton();
        if (close.Visible)
        {
            var body = dialog.GetChildren().OfType<VBoxContainer>().FirstOrDefault();
            if (body is not null && body.GetGlobalRect().End.Y > close.GetGlobalRect().Position.Y + 1)
                throw new InvalidOperationException("Menu content overlaps its dismissal button.");
        }
        if (_captureMode == "published-rules")
        {
            var tabs = controls.OfType<TabContainer>().Single();
            tabs.CurrentTab = 1;
            if (tabs.CurrentTab != 1 || tabs.GetTabCount() != 2) throw new InvalidOperationException("Guide navigation failed.");
            dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
        }
        else if (_captureMode == "published-lacquer-menu")
            controls.OfType<Button>().Single(b => b.Text == "Return to game").EmitSignal(Button.SignalName.Pressed);
        else if (dialog is ConfirmationDialog)
            dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        else dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
        if (!dialog.IsQueuedForDeletion() || View.Version != version)
            throw new InvalidOperationException("Menu dismissal failed or changed the match.");
        GD.Print("MENU_CHECK_PASS: content clears dismissal, navigation/dismissal callback works, match unchanged.");
    }
}
