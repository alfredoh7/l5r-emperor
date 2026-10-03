using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>One public engine option. Selection is local; only the separate confirmation submits.</summary>
public sealed partial class EitherOrOptionButton : Button
{
    public EitherOrOptionView Option { get; }
    public EitherOrOptionButton(EitherOrOptionView option, bool selected, bool canChoose)
    {
        Option = option; Disabled = !canChoose; SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AccessibilityName = $"{option.Label}. {option.Detail}" + (!option.Legal ? $" Unavailable: {option.Reason}" : "");
        SetMeta("either_or_option", option.Key);
        var tint = selected ? new Color(1.35f, 1.2f, .9f) : option.Kind == EitherOrKind.Accept ? new Color(1.12f, .96f, .91f) : Colors.White;
        AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: tint));
        // Waiting seats keep public options readable; only genuinely unavailable options are dimmed.
        AddThemeStyleboxOverride("disabled", SidebarSkin.Surface(true, tint: option.Legal ? tint : new Color(.7f, .7f, .7f)));
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 14);
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; body.AddThemeConstantOverride("separation", 5); margin.AddChild(body);
        var title = Palette.Wrapped((selected ? "✓  " : "○  ") + option.Label, 21, option.Legal ? Palette.Paper : Palette.PaperDim);
        title.AddThemeFontOverride("font", SidebarSkin.Bold); body.AddChild(title);
        body.AddChild(Palette.Wrapped(option.Detail, 18, Palette.Paper));
        var kind = option.Kind == EitherOrKind.Accept ? "CONSEQUENCE" : option.Kind == EitherOrKind.Avoid ? "ALTERNATIVE" : "CHOICE";
        body.AddChild(Palette.Wrapped(option.Legal ? kind : kind + " · UNAVAILABLE — " + option.Reason, 14, option.Legal ? Palette.Gold : Palette.PaperDim));
        foreach (var child in body.GetChildren().OfType<Control>()) child.MouseFilter = MouseFilterEnum.Ignore;
        body.MinimumSizeChanged += () => SetDeferred(Control.PropertyName.CustomMinimumSize, new Vector2(0, Math.Max(104, body.GetCombinedMinimumSize().Y + 28)));
        CustomMinimumSize = new(0, 104);
    }
}
