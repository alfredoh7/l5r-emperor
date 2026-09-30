using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>An offered public target, with its full scan. Selection never submits an action.</summary>
public sealed partial class AttackTargetFace : Button
{
    public CardView Card { get; }
    public AttackTargetFace(CardView card, bool selected, string? price)
    {
        Card = card; CustomMinimumSize = new(160, 272); SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = card.Definition?.Name + ". " + (selected ? "Selected. " : "") + "Press to choose this attack target.";
        TooltipText = card.Definition?.Name ?? "Target";
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            var style = selected ? SidebarSkin.Surface(true, tint: new Color(1.4f, 1.25f, .85f)) : SidebarSkin.Inset();
            if (!selected && state is "hover" or "focus") style.ModulateColor = new(1.25f, 1.18f, 1);
            AddThemeStyleboxOverride(state, style);
        }
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 8);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 4); margin.AddChild(body);
        body.AddChild(CardPreview.Face(card, 200, 142));
        var title = Palette.Wrapped((selected ? "✓ " : "") + card.Definition?.Name, 16, selected ? Palette.Gold : Palette.Paper);
        title.MaxLinesVisible = 2; title.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(title);
        var detail = Palette.Text(card.Definition?.Type + (price ?? ""), 14, Palette.PaperDim); detail.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(detail);
        IgnoreInput(margin);
    }
    private static void IgnoreInput(Node node)
    { if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) IgnoreInput(child); }
    public override Control _MakeCustomTooltip(string forText) => CardPreview.Create(Card, "Legal attack target", null, GetTree().Root.GetVisibleRect().Size.Y / 1080f,
        interactionHint: "Click to select, then confirm separately.\nInspect selected card keeps the full scan open.");
}
