using Godot;
using L5R.Core;
namespace L5R.Client;

public sealed partial class MultiTargetFace : Button
{
    public CardView Card { get; }
    public MultiTargetFace(CardView card, int? order)
    {
        Card = card; CustomMinimumSize = new(164, 235); SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = card.Definition!.Name + (order is {} n ? $". Selected number {n}. Press to remove." : ". Press to select.");
        TooltipText = card.Definition.Name;
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        { var style = SidebarSkin.Inset(); style.ModulateColor = order.HasValue ? new Color(1.5f, 1.3f, .8f) : state == "disabled" ? new Color(.65f, .65f, .65f) : Colors.White; AddThemeStyleboxOverride(state, style); }
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 8);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 3); margin.AddChild(body);
        body.AddChild(CardPreview.Face(card, 188, 142));
        var label = Palette.Wrapped((order.HasValue ? order + " · " : "") + card.Definition.Name, 15, order.HasValue ? Palette.Gold : Palette.Paper);
        label.HorizontalAlignment = HorizontalAlignment.Center; label.MaxLinesVisible = 2; body.AddChild(label); Ignore(margin);
    }
    private static void Ignore(Node node) { if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) Ignore(child); }
    public override Control _MakeCustomTooltip(string forText) => CardPreview.Create(Card, "Offered target", null,
        GetTree().Root.GetVisibleRect().Size.Y / 1080f, interactionHint: "Select or remove this card. Confirm all targets separately.");
}
