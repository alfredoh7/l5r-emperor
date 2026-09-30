using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Presentation only. Durations come from the public snapshot; printed traits and
/// token quantities never become grants. Missing source identity is never guessed.</summary>
public static class CardChanges
{
    public sealed record Entry(string Kind, string Name, string Text, string Source, string Duration, bool Copied = false, string? SourceLabel = null);
    public static bool IsPermanent(string duration) => duration.Equals("Permanent", StringComparison.OrdinalIgnoreCase);
    public static Entry[] Entries(CardView card) => card.Definition is null ? [] :
        (card.GrantedKeywords ?? []).Select(g => new Entry("Keyword", g.Keyword, "", g.CopiedFrom ?? g.Source, g.Duration, g.CopiedFrom is not null, g.CopiedFromName))
        .Concat((card.GrantedAbilities ?? []).Select(g => new Entry("Ability", g.Timing, (g.CopiedFrom is null ? "Granted ability\n" : "") + g.Label + GrantedAbilityText.Usage(g), g.CopiedFrom ?? g.Source, g.Duration, g.CopiedFrom is not null, g.CopiedFromName ?? g.SourceName)))
        .Concat((card.GrantedEffects ?? []).Select(g => new Entry(g.Kind, g.Name, g.Text, g.Source, g.Duration)))
        // AppliedModifier currently has turn duration in the engine. Never infer a permanent
        // bonus from the difference between printed and current Force (tokens/attachments also alter it).
        .Concat((card.Modifiers ?? []).Select(m => new Entry("Stat", $"{m.Amount:+0;-0;0} {m.Stat}", "", m.Source, "Turn")))
        .ToArray();
    public static Entry[] Permanent(CardView card) => Entries(card).Where(e => IsPermanent(e.Duration)).ToArray();
    public static Entry[] Marked(CardView card) => Entries(card).Where(e => IsPermanent(e.Duration) || e.Copied).ToArray();
    public static bool HasSeal(CardView card) => card.FaceUp && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached
        && Marked(card).Length > 0;
    public static string DurationLabel(string duration) => duration switch
    {
        "Permanent" => "Permanent",
        "Turn" => "Until turn ends",
        "Attachment" => "While attached",
        "Battle" => "Until battle ends",
        "Copy" => "Until the copying ability is used again",
        _ => string.IsNullOrWhiteSpace(duration) ? "Duration not supplied" : duration
    };
    public static string SourceName(string source, Func<string, string?>? resolve)
    {
        var name = resolve?.Invoke(source);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return CardCatalog.Cards.TryGetValue(source, out var definition) ? definition.Name : "Source unavailable";
    }
    public static string Summary(CardView card, Func<string, string?>? resolve) => string.Join("\n", Entries(card)
        .Select(e => $"{(e.Copied ? "Copied " : "")}{e.Name} · {DurationLabel(e.Duration)}" + (e.Text.Length > 0 ? $"\n{e.Text}" : "") + $"\n{(e.Copied ? "Copied from" : "Source")}: {e.SourceLabel ?? SourceName(e.Source, resolve)}"));
}

/// <summary>Exact source pixels from approved A/B; values and typography are live controls.</summary>
public static class CardChangeArt
{
    public static readonly Rect2 PanelSource = new(775, 89, 610, 550);
    public const int PanelWidth = 610;
    private static Texture2D? _a, _b, _lacquer;
    public static Texture2D Lacquer => _lacquer ??= GD.Load<Texture2D>("res://Art/PermanentGrants/lacquer-v1.png");
    public static Texture2D A => _a ??= GD.Load<Texture2D>("res://Art/PermanentGrants/approved-A.png");
    public static Texture2D B => _b ??= GD.Load<Texture2D>("res://Art/PermanentGrants/approved-B.png");
    public static void Frame(CanvasItem canvas, Rect2 target)
    {
        // Blank lacquer from the same panel, then only its outer 24 px. No generated text
        // enters the live surface. Keep corners at their original size, stretch plain runs only.
        canvas.DrawTextureRect(Lacquer, target, false, new Color(.32f, .32f, .32f));
        var source = PanelSource; const float m = 24;
        float[] xs = [0, m, source.Size.X - m, source.Size.X], ys = [0, m, source.Size.Y - m, source.Size.Y];
        float[] dx = [target.Position.X, target.Position.X + m, target.End.X - m, target.End.X];
        float[] dy = [target.Position.Y, target.Position.Y + m, target.End.Y - m, target.End.Y];
        for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++)
        {
            if (x == 1 && y == 1) continue;
            canvas.DrawTextureRectRegion(B, new Rect2(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]),
                new Rect2(source.Position + new Vector2(xs[x], ys[y]), new Vector2(xs[x + 1] - xs[x], ys[y + 1] - ys[y])));
        }
    }
    public static void Icon(CanvasItem canvas, Vector2 center, float diameter, string kind)
    {
        var source = new Vector2(867, kind == "Keyword" ? 272 : kind == "Stat" ? 375 : 478);
        var points = Enumerable.Range(0, 64).Select(i => Vector2.FromAngle(i * Mathf.Tau / 64)).ToArray();
        canvas.DrawColoredPolygon(points.Select(p => center + p * diameter / 2).ToArray(), Colors.White,
            points.Select(p => (source + p * 41) / B.GetSize()).ToArray(), B);
    }
}

public partial class CardChangeMedallion : Control
{
    public string Kind { get; init; } = "Keyword";
    public override void _Draw() => CardChangeArt.Icon(this, Size / 2, Math.Min(Size.X, Size.Y), Kind);
}
