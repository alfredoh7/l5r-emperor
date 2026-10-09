using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private IReadOnlyList<CardTile> TilesFor(string id) => _tiles.Where(t => IsInstanceValid(t) && t.Card.Id == id).ToArray();

    /// <summary>Issue #789: a sidebar control and the table cards it names highlight each other (hover, focus, and hovering the card).</summary>
    private void LinkToTable(Control control, params string?[] cardIds) => TableLink.Attach(control, TilesFor, cardIds);
    private void LinkToTable(Control control, IEnumerable<string?> cardIds) => TableLink.Attach(control, TilesFor, cardIds);

    /// <summary>The cards an action acts through or on: its source, performer and chosen target. Option lists are not included; their rows link themselves.</summary>
    private static string?[] ActionCards(ActionOption option) => [option.CardId, option.Command?.PerformerId, option.Command?.TargetId];
}
