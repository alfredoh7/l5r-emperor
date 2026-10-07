using Godot;
using L5R.Core;

namespace L5R.Client;

// P25 (issue #723): a decider's choice among cards that were shown. The engine names each candidate by its position in MatchView.ShownCards (the option's Command.Choice) and offers one "chooseShown"
// action per candidate; this panel only draws each shown card's face beside the action that picks it. Selecting is a draft: nothing is decided until "Confirm choice".
public partial class Main
{
    private bool RenderShownChoice(ActionOption[] options)
    {
        var shown = View.ShownCards ?? [];
        var picks = options.Where(a => a.Type == "chooseShown").ToArray();
        _inspector.AddChild(Palette.Wrapped("Choose among the cards that were shown. Nothing is decided until you confirm.", 17, Palette.PaperDim));
        _inspector.AddChild(new HSeparator());
        var rows = new List<(ActionOption Option, Button Button)>();
        var confirm = SidebarSkin.Button("Confirm choice", CommitDecision, true);
        foreach (var option in picks)
        {
            var record = int.TryParse(option.Command?.Choice, out var sequence) ? shown.FirstOrDefault(s => s.Sequence == sequence) : null;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); _inspector.AddChild(row);
            if (record is not null) row.AddChild(CardPreview.Face(ShownCardEntry.PrintedCard(record), 150, 108));
            var enabled = option.Enabled && option.Command is not null && DecisionCanSubmit;
            var button = SidebarSkin.Button((ReferenceEquals(_decisionSelection, option) ? "✓  " : "") + option.Label, () =>
            {
                _decisionSelection = option;
                foreach (var other in rows) other.Button.Text = (ReferenceEquals(other.Option, option) ? "✓  " : "") + other.Option.Label;
                confirm.Disabled = !DecisionCanSubmit;
            });
            button.Disabled = !enabled; button.CustomMinimumSize = new(0, 74); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(button); rows.Add((option, button));
        }
        if (_decisionSelection is not null && !picks.Contains(_decisionSelection)) _decisionSelection = null;
        confirm.Disabled = _decisionSelection is null || !DecisionCanSubmit;
        _decisionActions.AddChild(confirm);
        if (_decisionSelection is not null) _decisionActions.AddChild(SidebarSkin.Button("Clear selection", () => { _decisionSelection = null; RenderInspector(); }));
        _decisionActions.AddChild(Palette.Wrapped(DecisionCanSubmit ? "Select one option, then confirm." : "Your choice is paused until the connection is ready.", 14, Palette.PaperDim));
        return true;
    }
}
