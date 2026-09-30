using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation of existing public phases, not an additional rules state machine.</summary>
public partial class BattleTracker : Control
{
    private static Texture2D? _flowArt;
    public MatchView Snapshot { get; set; } = null!;
    public bool Compact { get; set; }
    public bool ReducedMotion { get; set; }
    public bool PresentationInProgress { get; set; }
    public bool ActionOutcome { get; set; }
    public string? ResolutionMessage { get; set; }
    public int Stage => ActionOutcome ? 1 : ResolutionMessage is not null ? 2 : Snapshot.Phase == Phase.Battle ? 1 : 0;
    public static bool InBattle(Phase phase) => phase is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(0, 124);
        TooltipText = ResolutionMessage ?? Snapshot.PendingChoice?.Prompt ?? (Snapshot.BattleStep?.Step == BattleSteps.DeclareOrDecline ? "Declare an attack or decline to Dynasty." : Snapshot.Phase == Phase.BattleSelection
            ? "The attacker chooses the next battlefield."
            : Snapshot.Phase == Phase.Battle ? "Battle actions alternate. Resolution follows two consecutive passes."
            : "Assign units to provinces. The highlighted player has the next decision.");
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public string Status => PresentationInProgress ? (ActionOutcome ? "Applying battle action" : "Resolving battle") : ResolutionMessage is not null ? "Resolved" : Snapshot.PriorityPlayerId == Snapshot.ViewerId ? (Snapshot.PendingChoice is null && Snapshot.Phase == Phase.Battle ? "You may act or pass" : "Your decision") : "Opponent may act";
    public override void _Draw()
    {
        var step = Snapshot.BattleStep;
        var title = Stage == 2 ? "BATTLE OUTCOME" : step?.Step switch {
            BattleSteps.DeclareOrDecline => "DECLARE AN ATTACK", BattleSteps.AttackerAssigns or BattleSteps.DefenderAssigns => step.Segment == BattleSteps.CavalryManeuvers ? "CAVALRY MANEUVERS" : "INFANTRY MANEUVERS",
            BattleSteps.ChooseBattle => "CHOOSE BATTLEFIELD", BattleSteps.Engage => "ENGAGE", BattleSteps.BeforeResolution => "BEFORE RESOLUTION", BattleSteps.Aftermath => "BATTLE AFTERMATH", _ => "BATTLE ACTIONS" };
        TextAt(title, new Rect2(0, 0, Size.X, 38), 25, Palette.Paper, true);
        TextAt(ProvinceTitle, new Rect2(0, 46, Size.X, 25), 19, Palette.Paper, false);
        var owner = step?.DecisionPlayerId;
        var status = PresentationInProgress ? Status : Stage == 2 ? "Result recorded" : owner is {} id ?
            (id == Snapshot.ViewerId ? "Your decision" : "Opponent’s decision") + " · " + Snapshot.Players.Single(p => p.Id == id).Clan : Status;
        TextAt(status, new Rect2(0, 80, Size.X, 24), 17, Palette.PaperDim, false);
        DrawLine(new Vector2(0, 116), new Vector2(Size.X, 116), Palette.Gold with { A = .55f }, 1, true);
    }

    private string ProvinceTitle => Snapshot.CurrentBattleProvince is int p ? BattlefieldTitle(p) : ResolutionMessage is not null ? "Battle resolved" : "Battle";
    // An additional battlefield (Emperor Edition rulings pass R1, issue #492) is named by the province it is at, or has none.
    private string BattlefieldTitle(int p) => p < MatchEngine.FirstAdditionalBattlefield ? $"Province {p + 1}"
        : Snapshot.Battlefields?.FirstOrDefault(b => b.Index == p)?.Province is int province ? $"Second battlefield · Province {province + 1}" : "No province";
    private void TextAt(string text, Rect2 box, int size, Color color, bool heading)
    {
        if (heading) text = text.ToUpperInvariant();
        var font = heading ? Palette.Title : Palette.Body;
        while (size > 9 && font.GetStringSize(text, fontSize: size).X > box.Size.X) size--;
        var baseline = box.Position.Y + (box.Size.Y + font.GetAscent(size) - font.GetDescent(size)) / 2;
        DrawStringOutline(font, new Vector2(box.Position.X, baseline), text, HorizontalAlignment.Center, box.Size.X, size, 2, new Color("141516"));
        DrawString(font, new Vector2(box.Position.X, baseline), text, HorizontalAlignment.Center, box.Size.X, size, color);
    }
}
