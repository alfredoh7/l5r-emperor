using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class MatchDuelHost
{
    private bool _confirmConcede;
    private ActionOption? ConcedeOption => Session.Snapshot?.View.WinnerId is null
        ? Session.Snapshot?.Actions.FirstOrDefault(a => a.Type == "concede" && a.Enabled && a.Command is not null) : null;
    private void OpenConcede()
    {
        if (ConcedeOption is null || Unavailable || _sending) return;
        _confirmConcede = true; BuildOverlay();
    }
    private void CancelConcede() => SetMenu(false);
    private void BuildConcede()
    {
        var size = GetViewportRect().Size;
        var veil = new ColorRect { Name = "Concede confirmation", Color = new Color(0, 0, 0, .55f), Size = size }; _overlay.AddChild(veil);
        var design = new Vector2(850, 370); var scale = Math.Min(1.15f, Math.Min((size.X - 48) / design.X, (size.Y - 48) / design.Y));
        var shell = new Control { Position = (size - design * scale) / 2, Size = design, Scale = Vector2.One * scale }; veil.AddChild(shell);
        DuelDecisionArt.Background(shell, false);
        Label Line(string text, int font, float y, float height, bool title = false)
        {
            var line = DuelUi.Text(text, font, Palette.Paper, title); line.Position = new Vector2(45, y); line.Size = new Vector2(760, height);
            line.HorizontalAlignment = HorizontalAlignment.Center; shell.AddChild(line); return line;
        }
        Line("CONCEDE MATCH?", 58, 53, 75, true);
        Line("Your opponent will win the entire match.", 27, 151, 38);
        Line("Cancelling returns you to this duel.", 20, 200, 30);
        var keep = DuelUi.Button("Keep playing", CancelConcede, fontSize: 25); keep.Position = new Vector2(62, 273); keep.Size = new Vector2(346, 56); shell.AddChild(keep);
        var concede = DuelUi.Button("Concede match", () => _ = ConfirmConcede(), fontSize: 25); concede.Position = new Vector2(443, 273); concede.Size = new Vector2(346, 56); shell.AddChild(concede);
        foreach (var state in new[] { "normal", "hover", "pressed" })
        {
            var style = (StyleBoxTexture)concede.GetThemeStylebox(state).Duplicate();
            style.ModulateColor = new Color(.85f, .30f, .23f); concede.AddThemeStyleboxOverride(state, style);
        }
        concede.Disabled = ConcedeOption is null || Unavailable || _sending;
        keep.GrabFocus();
    }
    private async Task ConfirmConcede()
    {
        if (!_confirmConcede || Unavailable || _sending || ConcedeOption?.Command is not { } command) return;
        SetMenu(false);
        await Submit(command);
    }
}
