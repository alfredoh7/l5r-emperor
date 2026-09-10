using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of existing public phases, not an additional rules state machine.</summary>
public partial class BattleTracker : Control
{
    public MatchView Snapshot { get; set; } = null!;
    public bool Compact { get; set; }
    public bool ReducedMotion { get; set; }
    public bool PresentationInProgress { get; set; }
    public bool ActionOutcome { get; set; }
    public string? ResolutionMessage { get; set; }
    public int Stage => ActionOutcome ? 1 : ResolutionMessage is not null ? 2 : Snapshot.Phase == Phase.Battle ? 1 : 0;
    public static bool InBattle(Phase phase) => phase is Phase.AttackDeclaration or Phase.DefenseAssignment or Phase.BattleSelection or Phase.Battle;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(0, 138);
        TooltipText = ResolutionMessage ?? (Snapshot.Phase == Phase.BattleSelection
            ? "The attacker chooses the next battlefield."
            : Snapshot.Phase == Phase.Battle ? "Battle actions alternate. Resolution follows two consecutive passes."
            : "Assign units to provinces. The highlighted player has the next decision.");
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public string Status => PresentationInProgress ? (ActionOutcome ? "Applying battle action" : "Resolving battle") : ResolutionMessage is not null ? "Resolved" : Snapshot.PriorityPlayerId == Snapshot.ViewerId ? (Snapshot.PendingChoice is null && Snapshot.Phase == Phase.Battle ? "You may act or pass" : "Your decision") : "Opponent may act";
    public override void _Draw()
    {
        var title = Stage == 2 ? "RESOLUTION" : Stage == 1 ? "BATTLE ACTIONS" : "ASSIGNMENT";
        TextAt(title, new Rect2(0, 4, Size.X, 38), 28, Palette.Paper, true);
        TextAt(ProvinceTitle, new Rect2(0, 49, Size.X, 28), 21, Palette.Paper, false);
        TextAt(Status, new Rect2(0, 82, Size.X, 28), 18, Palette.Paper, false);
        var y = 128f; var cx = Size.X / 2;
        DrawLine(new Vector2(8,y),new Vector2(cx-10,y),Palette.Gold with { A=.65f },1,true);
        DrawLine(new Vector2(cx+10,y),new Vector2(Size.X-8,y),Palette.Gold with { A=.65f },1,true);
        DrawPolyline(new[]{new Vector2(cx,y-5),new Vector2(cx+5,y),new Vector2(cx,y+5),new Vector2(cx-5,y),new Vector2(cx,y-5)},Palette.Gold,1,true);
    }
    private string ProvinceTitle => Snapshot.CurrentBattleProvince is int p ? $"Province {p + 1}" : ResolutionMessage is not null ? "Battle resolved" : "Battle";
    private void TextAt(string text, Rect2 box, int size, Color color, bool heading)
    {
        var font = heading ? Palette.Title : Palette.Body;
        while (size > 9 && font.GetStringSize(text, fontSize: size).X > box.Size.X) size--;
        var baseline = box.Position.Y + (box.Size.Y + font.GetAscent(size) - font.GetDescent(size)) / 2;
        DrawStringOutline(font, new Vector2(box.Position.X, baseline), text, HorizontalAlignment.Center, box.Size.X, size, 2, new Color("141516"));
        DrawString(font, new Vector2(box.Position.X, baseline), text, HorizontalAlignment.Center, box.Size.X, size, color);
    }
}
