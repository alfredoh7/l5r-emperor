using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>An inspection row built exclusively from one public show record; never a live card lookup.</summary>
public sealed partial class ShownCardEntry : Button
{
    public ShownCardView Record { get; }
    public ShownCardEntry(ShownCardView record, string playerName, bool unread)
    {
        Record = record;
        CustomMinimumSize = new(0, 222); SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = $"{record.Definition.Name}. Shown by {playerName}. {record.Context}. Turn {record.TurnNumber}. Press to inspect.";
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            var style = state == "normal" && !unread ? SidebarSkin.Inset() : SidebarSkin.Surface(true, tint: state is "hover" or "focus" ? new Color(1.35f, 1.25f, 1.05f) : unread ? new Color(1.15f, 1.08f, .95f) : Colors.White);
            style.ContentMarginLeft = style.ContentMarginRight = 12; style.ContentMarginTop = style.ContentMarginBottom = 12;
            AddThemeStyleboxOverride(state, style);
        }
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 14);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 14); margin.AddChild(row);
        row.AddChild(CardPreview.Face(PrintedCard(record), 160, 112));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; text.AddThemeConstantOverride("separation", 5); row.AddChild(text);
        var name = Palette.Wrapped(record.Definition.Name, 21); name.MaxLinesVisible = 2; name.AddThemeFontOverride("font", SidebarSkin.Bold); text.AddChild(name);
        text.AddChild(Palette.Wrapped($"Shown by {playerName} · Turn {record.TurnNumber}", 15, Palette.PaperDim));
        var context = Palette.Wrapped(record.Context, 16, Palette.PaperDim); context.MaxLinesVisible = 4; text.AddChild(context);
        IgnoreChildren(margin);
    }
    private static void IgnoreChildren(Node node)
    { if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore; foreach (var child in node.GetChildren()) IgnoreChildren(child); }
    public static CardView PrintedCard(ShownCardView record) => new("shown-record-" + record.Sequence, record.Definition, record.ShownByPlayerId,
        Zone.Hand, true, false, null, null, record.Definition.Force, record.Definition.Chi);
}
