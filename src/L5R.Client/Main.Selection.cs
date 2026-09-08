using Godot;

namespace L5R.Client;

public partial class Main
{
    private void DeselectCard()
    {
        // Inspection is local presentation. Active game choices retain their
        // explicit Cancel action, so this cannot silently discard a draft.
        if (_chosen is not null || _session.Busy) return;
        _selected = null;
        _assignmentUnit = null;
        _sources.Clear();
        ReleaseTableFocus();
        HideTableTooltip();
        RenderInspector();
        RenderPhaseActions();
        Highlight();
    }

    private void AddInspectionCaption(string caption)
    {
        var row = new HBoxContainer();
        var label = Palette.Text(caption, 11, Palette.Gold);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(label);
        if (_chosen is null)
        {
            var close = Palette.Button("Close", DeselectCard);
            close.Name = "CloseInspection";
            close.CustomMinimumSize = new Vector2(54, 30);
            close.AddThemeFontSizeOverride("font_size", 14);
            close.TooltipText = "Deselect this card and return to the battle or phase overview. You can also click the selected card again.";
            row.AddChild(close);
        }
        _inspector.AddChild(row);
    }
}
