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
    private static Texture2D? _plate;
    private double _clock;
    public int Stage => ActionOutcome ? 1 : ResolutionMessage is not null ? 2 : Snapshot.Phase == Phase.Battle ? 1 : 0;
    public static bool InBattle(Phase phase) => phase is Phase.AttackDeclaration or Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.BattleSelection or Phase.Battle;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        CustomMinimumSize = new Vector2(0, (Compact ? 100 : 490) + 44);
        TooltipText = ResolutionMessage ?? (Snapshot.Phase == Phase.BattleSelection
            ? "The attacker chooses the next battlefield."
            : Snapshot.Phase == Phase.Battle ? "Battle actions alternate. Resolution follows two consecutive passes."
            : "Assign units to provinces. The highlighted player has the next decision.");
    }
    public override Control _MakeCustomTooltip(string text) => ReadingTooltip.Create(this, text);
    public override void _Process(double delta) { if (!ReducedMotion) { _clock += delta; QueueRedraw(); } }
    public string Status => PresentationInProgress ? (ActionOutcome ? "Applying battle action" : "Resolving battle") : ResolutionMessage is not null ? "Resolved" : Snapshot.PriorityPlayerId == Snapshot.ViewerId ? (Snapshot.PendingChoice is null && Snapshot.Phase == Phase.Battle ? "You may act or pass" : "Your decision") : "Opponent may act";
    private Color Highlight => ResolutionMessage is not null ? Palette.Gold : Snapshot.PriorityPlayerId == Snapshot.ViewerId ? new Color("8bc9ee") : new Color("efb967");
    public override void _Draw()
    {
        _plate ??= GD.Load<Texture2D>("res://Art/battle-tracker.png");
        // Atlas excludes generated outer backdrop; all visible surface is original lacquer art.
        var source = new Rect2(63, 29, 763, 1714);
        var w = Math.Min(Size.X, Compact ? Size.X : 225);
        var h = Compact ? 100 : 490;
        var x = (Size.X - w) / 2;
        var rect = new Rect2(x, 0, w, h);
        if (Compact)
        {
            DrawTextureRectRegion(_plate, rect, new Rect2(63, 29, 763, 270));
            // Same real medallions in a compact row while the card inspector is open.
            float[] sourceY = [457, 883, 1312];
            for (var i = 0; i < 3; i++)
            {
                var c = new Vector2(x + w * (.22f + i * .28f), 28);
                var vertices = new Vector2[80]; var uv = new Vector2[80];
                for (var j = 0; j < 80; j++)
                {
                    var dir = Vector2.FromAngle(j * Mathf.Tau / 80);
                    vertices[j] = c + dir * 19;
                    uv[j] = (new Vector2(443, sourceY[i]) + dir * 146) / _plate.GetSize();
                }
                DrawColoredPolygon(vertices, Colors.White, uv, _plate);
                if (i == Stage)
                    for (var r = 7; r >= 0; r--) DrawArc(c, 19 + r * .7f, 0, Mathf.Tau, 64, Highlight with { A = .20f * (1 - r / 8f) }, 2, true);
            }
            var label = Stage == 2 ? "Resolution" : Stage == 1 ? "Battle actions" : "Assignment";
            TextAt(label, new Rect2(x + 8, 52, w - 16, 22), 20, Highlight, true);
            TextAt(Status, new Rect2(x + 8, h + 8, w - 16, 28), 17, Highlight, false);
            return;
        }
        DrawTextureRectRegion(_plate, rect, source);
        TextAt(ProvinceTitle, new Rect2(x + 12, h * .05f, w - 24, 28), 20, Palette.Gold, true);
        float[] centers = [.249f, .497f, .750f];
        float[] labels = [.337f, .585f, .845f];
        string[] names = ["Assignment", "Battle actions", "Resolution"];
        for (var i = 0; i < 3; i++)
        {
            var c = new Vector2(x + w * .5f, h * centers[i]);
            var radius = w * .19f;
            if (i == Stage)
            {
                var pulse = ReducedMotion ? 1 : .90f + .10f * (float)Math.Sin(_clock * 1.4);
                // Soft layered halo surrounds the real textured medallion; no flat replacement disc.
                for (var ring = 17; ring >= 0; ring--)
                    DrawArc(c, radius + ring * .65f, 0, Mathf.Tau, 96, Highlight with { A = (.012f + .10f * (1 - ring / 18f) * (1 - ring / 18f)) * pulse }, 2, true);
                DrawCircle(c, radius * .94f, Highlight with { A = .13f });
                DrawArc(c, radius * .99f, 0, Mathf.Tau, 96, Highlight with { A = .75f }, 1.2f, true);
            }
            TextAt(names[i], new Rect2(x + 8, h * labels[i], w - 16, 25), 20, i == Stage ? Highlight : Palette.PaperDim, true);

        }
        // A dedicated footer below the plate keeps priority text clear of
        // medallions, stage headings and the next inspector control.
        TextAt(Status, new Rect2(x + 8, h + 8, w - 16, 28), 17, Highlight, false);
    }
    private string ProvinceTitle => Snapshot.CurrentBattleProvince is int p ? $"Province {p + 1}" : ResolutionMessage is not null ? "Battle resolved" : "Battle";
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
