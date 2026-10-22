using Godot;

namespace L5R.Client;

/// <summary>
/// Issue #789: a sidebar control and the table cards it stands for light each other. Hovering or focusing the control draws its cards as hovered;
/// hovering one of those cards on the table lights the control. That is how a player tells apart two cards that share a title (two Ikoma Shika)
/// when one sidebar choice would act through, or on, only one of them. Presentation only: the engine decides what is legal.
/// </summary>
internal static class TableLink
{
    private static readonly Color Lit = new(1.3f, 1.2f, .85f);

    /// <summary>Links <paramref name="control"/> to the table tiles of <paramref name="cardIds"/> (nulls and blanks are ignored). Handlers are removed with the control.</summary>
    public static void Attach(Control control, Func<string, IReadOnlyList<CardTile>> tilesFor, IEnumerable<string?> cardIds)
    {
        var ids = cardIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return;
        void Hover(bool on) { foreach (var id in ids) foreach (var tile in tilesFor(id)) if (GodotObject.IsInstanceValid(tile)) tile.ForcedHover = on; }
        control.MouseEntered += () => Hover(true);
        control.MouseExited += () => Hover(false);
        control.FocusEntered += () => Hover(true);
        control.FocusExited += () => Hover(false);
        control.TreeExiting += () => Hover(false);
        // The other direction: the card on the table lights this control. Handlers leave with the control so a rebuilt sidebar keeps none behind.
        foreach (var id in ids)
            foreach (var tile in tilesFor(id))
            {
                var captured = tile;
                Action enter = () => Light(control, true), exit = () => Light(control, false);
                // TreeExiting also fires when the control is only moved (a duel reparents the table), so unlink once and link again if it comes back.
                var linked = false;
                void Link(bool on)
                {
                    if (on == linked || !GodotObject.IsInstanceValid(captured)) return;
                    if (on) { captured.MouseEntered += enter; captured.MouseExited += exit; }
                    else { captured.MouseEntered -= enter; captured.MouseExited -= exit; }
                    linked = on;
                }
                Link(true);
                control.TreeExiting += () => Link(false);
                control.TreeEntered += () => Link(true);
            }
    }

    private static void Light(Control control, bool on)
    {
        if (GodotObject.IsInstanceValid(control)) control.Modulate = on ? Lit : Colors.White;
    }
}
