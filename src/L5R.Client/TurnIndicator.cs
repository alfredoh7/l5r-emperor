using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Turn ownership and action priority are separate facts supplied by the engine.</summary>
public partial class TurnIndicator : Control
{
    public string TurnText { get; private set; } = "";
    public string ActionText { get; private set; } = "";
    public bool YourAction { get; private set; }
    public bool ReducedMotion { get; set; }
    private Color _accent = Palette.Gold;
    private float _time;
    public TurnIndicator() { MouseFilter = MouseFilterEnum.Ignore; }

    public void Present(MatchView view)
    {
        if (view.WinnerId is int winner)
        {
            TurnText = winner == view.ViewerId ? "VICTORY" : "MATCH ENDED";
            ActionText = view.VictoryReason ?? ""; YourAction = false; QueueRedraw(); return;
        }
        var yourTurn = (view.ActionPhaseTurnPlayerId ?? view.ActivePlayerId) == view.ViewerId;
        YourAction = view.PriorityPlayerId == view.ViewerId;
        var actor = view.Players.Single(p => p.Id == view.PriorityPlayerId);
        var name = view.Players.Select(p => p.Clan).Distinct().Count() == 1 ? actor.Name : actor.Clan;
        TurnText = yourTurn ? "YOUR TURN" : "OPPONENT'S TURN";
        ActionText = YourAction ? "Your decision" :
            (yourTurn ? $"Waiting for {name}'s response" : $"{name} is deciding");
        _accent = actor.Clan == "Lion" ? new Color("f1c976") : new Color("9bd4e4");
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (ReducedMotion) return;
        _time += (float)delta; QueueRedraw();
    }

    public override void _Draw()
    {
        if (string.IsNullOrEmpty(TurnText)) return;
        var pulse = ReducedMotion ? 1 : 1 + .06f * Mathf.Sin(_time * 2);
        var rect = new Rect2(0, 0, Size.X, 40);
        DrawStyleBox(Palette.Box(new Color(.045f, .045f, .035f, .95f), _accent with { A = .6f }, 9, 3), rect);
        DrawLine(new Vector2(10, 39), new Vector2(Size.X - 10, 39), _accent with { A = .12f * pulse }, 8, true);
        var titleWidth = Math.Min(Size.X * .48f, 270);
        var titleSize = 24;
        while (titleSize > 16 && Palette.Title.GetStringSize(TurnText, fontSize: titleSize).X > titleWidth - 12) titleSize--;
        DrawString(Palette.Title, new Vector2(12, 28), TurnText, HorizontalAlignment.Left, titleWidth - 12, titleSize, _accent);
        DrawLine(new Vector2(titleWidth, 9), new Vector2(titleWidth, 31), _accent with { A = .35f }, 1);
        var bodySize = 20;
        var width = Size.X - titleWidth - 22;
        while (bodySize > 13 && Palette.Body.GetStringSize(ActionText, fontSize: bodySize).X > width) bodySize--;
        DrawString(Palette.Body, new Vector2(titleWidth + 12, 27), ActionText, HorizontalAlignment.Left, width, bodySize, Palette.Paper);
    }
}
