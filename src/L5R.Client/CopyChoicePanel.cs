using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Either independent copy step, populated exclusively from the chooser's engine view.</summary>
public sealed partial class CopyChoicePanel : PanelContainer
{
    public Action<ActionOption>? Confirmed;
    public Action<CardView>? Inspect;
    public ActionOption? Selection { get; private set; }
    public Button ConfirmButton { get; private set; } = null!;
    public List<(CopyChoiceView Choice, Button Button)> Choices { get; } = [];
    public Label Progress { get; private set; } = null!;
    public CopyChoicePanel() { Theme = SidebarSkin.Theme(); AddThemeStyleboxOverride("panel", SidebarSkin.Surface()); }
    public void Populate(CopyView copy, ActionOption[] actions, bool canSubmit, CardView? receiver, int totalSteps)
    {
        Palette.Clear(this); Choices.Clear(); Selection = null;
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 10); AddChild(body);
        var title = Palette.Text(copy.Step == CopyStep.Keyword ? "CHOOSE A KEYWORD" : "CHOOSE AN ABILITY", 30, title: true); title.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(title);
        Progress = Palette.Text(totalSteps > 1 ? $"Step {totalSteps - copy.RemainingSteps.Length} of {totalSteps}" : "Final step", 19, Palette.Gold); Progress.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(Progress);
        body.AddChild(new HSeparator());
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 18); body.AddChild(row);
        if (CardCatalog.Cards.TryGetValue(copy.SourceDefinitionId, out var definition))
            AddPortrait(row, DeckSearchPanel.Printed(definition, 0), "COPY FROM", 300, true);
        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; center.AddThemeConstantOverride("separation", 12); row.AddChild(center);
        center.AddChild(Palette.Wrapped(copy.Prompt, 19));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill }; center.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 12); scroll.AddChild(list);
        ConfirmButton = SidebarSkin.Button(copy.Step == CopyStep.Keyword ? "Confirm keyword" : "Confirm ability", () =>
        { if (canSubmit && Selection is { Enabled: true, Command: not null } selected) Confirmed?.Invoke(selected); }, true);
        ConfirmButton.Disabled = true; ConfirmButton.CustomMinimumSize = new(300, 58);
        foreach (var choice in copy.Choices)
        {
            var action = actions.SingleOrDefault(a => a.Command?.Choice == choice.Value && a.Type == (copy.Step == CopyStep.Keyword ? "chooseCopyKeyword" : "chooseCopyAbility"));
            var label = (choice.Detail is null ? "" : choice.Detail + "\n") + choice.Label;
            var button = SidebarSkin.Button("○  " + label, () =>
            {
                if (!canSubmit || action is not { Enabled: true, Command: not null }) return;
                Selection = action; ConfirmButton.Disabled = false;
                foreach (var item in Choices)
                {
                    var chosen = item.Choice.Value == choice.Value;
                    item.Button.Text = (chosen ? "✓  " : "○  ") + (item.Choice.Detail is null ? "" : item.Choice.Detail + "\n") + item.Choice.Label;
                    item.Button.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: chosen ? new Color(1.35f, 1.2f, .9f) : Colors.White));
                }
            });
            button.AddThemeFontSizeOverride("font_size", 19); button.Alignment = HorizontalAlignment.Left;
            button.AccessibilityName = label; button.Disabled = !canSubmit || action is not { Enabled: true, Command: not null };
            button.SetMeta("copy_choice", choice.Value); list.AddChild(button); Choices.Add((choice, button));
        }
        if (receiver?.Definition is not null) AddPortrait(row, receiver, "COPY TO", 210, false);
        body.AddChild(new HSeparator());
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 16); body.AddChild(footer);
        var note = Palette.Wrapped(canSubmit ? copy.RemainingSteps.Length > 0 ? "Then choose " + (copy.RemainingSteps[0] == CopyStep.Keyword ? "a keyword." : "an ability.") : "Your selection is applied when you confirm." : "Waiting for the game to be ready.", 17, Palette.PaperDim);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(note); footer.AddChild(ConfirmButton);
        var duration = Palette.Wrapped(CardChanges.DurationLabel(copy.Duration), 17, Palette.Gold); duration.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(duration);
    }
    private void AddPortrait(HBoxContainer row, CardView card, string label, int height, bool printedOnly)
    {
        var column = new VBoxContainer { CustomMinimumSize = new(height / 1.4f, 0) }; column.AddThemeConstantOverride("separation", 10); row.AddChild(column);
        var caption = Palette.Text(label, 16, Palette.Gold); caption.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(caption);
        var name = Palette.Wrapped(card.Definition!.Name, 21); name.HorizontalAlignment = HorizontalAlignment.Center; column.AddChild(name);
        var face = new DecisionSourceFace(card, height, printedOnly); face.SizeFlagsVertical = SizeFlags.ShrinkBegin; column.AddChild(face); face.Pressed += () => Inspect?.Invoke(card);
        column.AddChild(Palette.Wrapped("Hover or click to inspect", 14, Palette.PaperDim));
    }
}
