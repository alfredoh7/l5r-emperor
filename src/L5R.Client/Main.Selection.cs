using Godot;

namespace L5R.Client;

public partial class Main
{
    private void DeselectCard()
    {
        // Inspection is local presentation. Active game choices retain their
        // explicit Cancel action, so this cannot silently discard a draft.
        if (_chosen is not null || _session.Busy) return;
        _playPrompt = null;
        _selected = null;
        _selectedHomeReminder = null;
        HighlightHomeReminders();
        _assignmentUnit = null;
        _sources.Clear();
        ReleaseTableFocus();
        HideTableTooltip();
        RenderInspector();
        RenderPhaseActions();
        Highlight();
    }

    private void AddInspectionCaption(string caption, VBoxContainer? target = null)
    {
        var row = new HBoxContainer();
        var label = Palette.Text(caption, 18, Palette.PaperDim);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(label);
        if (_chosen is null)
        {
            var close = Palette.Button("×", DeselectCard);
            close.Name = "CloseInspection";
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
                close.AddThemeStyleboxOverride(state, Palette.Box(Colors.Transparent, null, 0));
            close.CustomMinimumSize = new Vector2(32, 32);
            close.AddThemeFontSizeOverride("font_size", 25);
            close.TooltipText = "Deselect this card and return to the battle or phase overview. You can also click the selected card again.";
            row.AddChild(close);
        }
        (target ?? _inspector).AddChild(row);
    }
}
