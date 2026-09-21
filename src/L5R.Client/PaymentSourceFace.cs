using Godot;
using L5R.Core;

namespace L5R.Client;

public sealed partial class PaymentSourceFace : Button
{
    public CardView Card { get; }
    public PaymentSourceFace(CardView card, int production, bool selected)
    {
        Card = card; CustomMinimumSize = new(104, 158); SizeFlagsHorizontal = SizeFlags.ExpandFill;
        TooltipText = card.Definition?.Name ?? "Gold source";
        AccessibilityName = $"{TooltipText}, {production} Gold. " + (card.Bowed ? "Bowed." : selected ? "Selected; press to remove." : "Press to select.");
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var style = SidebarSkin.Inset(); style.ModulateColor = card.Bowed ? new Color(.5f, .5f, .5f) : selected ? new Color(1.65f, 1.4f, .95f) : Colors.White;
            AddThemeStyleboxOverride(state, style);
        }
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 6);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 3); margin.AddChild(body);
        body.AddChild(CardPreview.Face(card, 124, 88));
        var caption = Palette.Text((card.Bowed ? "Bowed · " : selected ? "✓ " : "") + production + " Gold", 16, selected ? Palette.Gold : Palette.Paper);
        caption.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(caption);
        IgnoreInput(margin);
    }
    private static void IgnoreInput(Node node)
    { if (node is Control control) control.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) IgnoreInput(child); }
    public override Control _MakeCustomTooltip(string forText) => CardPreview.Create(Card, "Gold source", null, GetTree().Root.GetVisibleRect().Size.Y / 1080f,
        interactionHint: "Select sources, then confirm the payment separately.");
}
