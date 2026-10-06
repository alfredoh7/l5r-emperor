using Godot;
using L5R.Core;

namespace L5R.Client;

// Budgeted multi-select (issue #667, Sleep): the engine keeps the selection and the remaining value (MatchState.PendingBudgetSelect); this panel only presents what the engine offers.
// Each "budgetSelectAdd" action is one Personality (disabled, with the engine's reason, when its Chi no longer fits); "Clear selection" and "Confirm selection" are the engine's own actions.
// The Confirm action's DecisionLabel is the running value ("Remaining value: N") and its DecisionCandidateIds are the Personalities chosen so far.
public partial class Main
{
    private bool RenderBudgetSelect(ActionOption[] options)
    {
        var confirm = options.FirstOrDefault(a => a.Type == "budgetSelectConfirm");
        var clear = options.FirstOrDefault(a => a.Type == "budgetSelectClear");
        var adds = options.Where(a => a.Type == "budgetSelectAdd").ToArray();
        var remaining = Palette.Text(confirm?.DecisionLabel ?? "Remaining value", 30, Palette.Gold, title: true);
        remaining.HorizontalAlignment = HorizontalAlignment.Center; remaining.AddThemeFontSizeOverride("font_size", 30); _inspector.AddChild(remaining);
        var chosen = (confirm?.DecisionCandidateIds ?? []).Select(FindCard).Where(c => c?.Definition is not null).Cast<CardView>().ToArray();
        _inspector.AddChild(Palette.Wrapped(chosen.Length == 0 ? "No Personality chosen yet. Choosing one subtracts its Chi from the remaining value." : "Chosen: " + string.Join(", ", chosen.Select(c => c.Definition!.Name)), 17, Palette.Paper));
        _inspector.AddChild(new HSeparator());
        foreach (var option in adds)
        {
            var enabled = option.Enabled && option.Command is not null && DecisionCanSubmit;
            var button = SidebarSkin.Button(option.Label, () => { if (DecisionCanSubmit && option.Command is { } command) _ = Submit(command); });
            button.Disabled = !enabled; button.CustomMinimumSize = new(0, 52); button.SetMeta("budget_candidate", option.CardId ?? "");
            _inspector.AddChild(button);
            if (!option.Enabled && option.Reason is { } reason) _inspector.AddChild(Palette.Wrapped(reason, 14, Palette.PaperDim));
        }
        var clearButton = SidebarSkin.Button("Clear selection", () => { if (DecisionCanSubmit && clear?.Command is { } command) _ = Submit(command); });
        clearButton.Disabled = clear?.Enabled != true || clear.Command is null || !DecisionCanSubmit;
        var confirmButton = SidebarSkin.Button("Confirm selection", () => { if (DecisionCanSubmit && confirm?.Command is { } command) _ = Submit(command); }, true);
        confirmButton.Disabled = confirm?.Command is null || !DecisionCanSubmit;
        _decisionActions.AddChild(confirmButton); _decisionActions.AddChild(clearButton);
        _decisionActions.AddChild(Palette.Wrapped(DecisionCanSubmit ? "Choose Personalities on the table or in the list. Nothing is bowed until you confirm." : "Your choice is paused until the connection is ready.", 14, Palette.PaperDim));
        return true;
    }

    // A click on a Personality on the table adds it to the selection when the engine offers it (disabled ones do nothing).
    private bool SelectBudgetCandidate(CardView card)
    {
        if (View.PendingChoice?.Type != "budgetSelect") return false;
        if (Actions.FirstOrDefault(a => a.Enabled && a.Type == "budgetSelectAdd" && a.Command?.TargetId == card.Id) is { Command: { } command } && DecisionCanSubmit) _ = Submit(command);
        else SetNotice("That Personality does not fit the remaining value.");
        return true;
    }
}
