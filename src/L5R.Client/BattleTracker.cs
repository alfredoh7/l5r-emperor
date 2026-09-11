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
    public static bool InBattle(Phase phase) => phase is Phase.AttackDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense or Phase.BattleSelection or Phase.Battle;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(0, 188);
        TooltipText = ResolutionMessage ?? (Snapshot.Phase == Phase.BattleSelection
            ? "The attacker chooses the next battlefield."
            : Snapshot.Phase == Phase.Battle ? "Battle actions alternate. Resolution follows two consecutive passes."
            : "Assign units to provinces. The highlighted player has the next decision.");
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public string Status => PresentationInProgress ? (ActionOutcome ? "Applying battle action" : "Resolving battle") : ResolutionMessage is not null ? "Resolved" : Snapshot.PriorityPlayerId == Snapshot.ViewerId ? (Snapshot.PendingChoice is null && Snapshot.Phase == Phase.Battle ? "You may act or pass" : "Your decision") : "Opponent may act";
    public override void _Draw()
    {
        var title = Stage == 2 ? "BATTLE OUTCOME" : Snapshot.PendingChoice is not null ? "YOUR CHOICE" : Snapshot.Phase switch {
            Phase.Battle => "BATTLE ACTIONS", Phase.BattleSelection => "CHOOSE THE BATTLE",
            Phase.CavalryDeclaration or Phase.CavalryDefense => "CAVALRY MANEUVERS", _ => "INFANTRY MANEUVERS" };
        TextAt(title, new Rect2(0, 0, Size.X-30, 38), 25, Palette.Paper, true);
        DrawTextureRectRegion(_flowArt ??= GD.Load<Texture2D>("res://Art/battle-flow-reference.png"), new Rect2(Size.X-33,2,32,36),new Rect2(320,121,47,53));
        var progress = Stage == 2 ? 3 : Snapshot.Phase == Phase.Battle ? 2 : Snapshot.Phase == Phase.BattleSelection ? 1 : 0;
        string[] steps = ["Maneuvers", "Engage", "Actions", "Outcome"];
        var stepWidth = Size.X/4;
        for(var i=0;i<4;i++) {
            var center = new Vector2(stepWidth*(i+.5f),58);
            if(i<3) DrawLine(center,center+new Vector2(stepWidth,0),new Color("5e5648"),1,true);
            DrawCircle(center,5,new Color("24221d"));
            DrawArc(center,5,0,Mathf.Tau,24,i==progress?Palette.Gold:new Color("736955"),1,true);
            if(i==progress) { DrawCircle(center,3,Palette.Paper); DrawArc(center,8,0,Mathf.Tau,32,Palette.Gold with {A=.28f},3,true); }
            TextAt(steps[i],new Rect2(i*stepWidth,70,stepWidth,20),13,i==progress?Palette.Paper:Palette.PaperDim,false);
        }
        TextAt(ProvinceTitle,new Rect2(0,104,Size.X,25),20,Palette.Paper,false);
        var role = Snapshot.Players.Single(p=>p.Id==Snapshot.ActivePlayerId).Clan+" turn · "+
            (PresentationInProgress?Status:Stage==2?"Result recorded":Snapshot.PriorityPlayerId==Snapshot.ViewerId?"You may act":"Opponent may act");
        TextAt(role,new Rect2(0,139,Size.X,24),16,Palette.PaperDim,false);
        DrawLine(new Vector2(0,178),new Vector2(Size.X,178),Palette.Gold with { A=.55f },1,true);
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
