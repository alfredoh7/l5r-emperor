using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #917 (docs/decisions/2026-10-13-action-resolution-area.md): the action resolution area. Core owns it (MatchView.ResolutionArea, Zone.Resolving, oldest entry first); the client only
// draws what the view lists. A Strategy or Ring being played is public to both seats from its announcement, leaves the hand tray (the hand counts never include it) and stays here, face up,
// until its action ends -- it is discarded by Core, and Main.Motion then flies it from here to the discard pile like any other card that changes zone.
public partial class Main
{
    private const float ResolutionCardSide = 214;

    private void AddResolutionArea()
    {
        var entries = View.ResolutionArea;
        if (entries is not { Length: > 0 }) return;
        var step = ResolutionCardSide * .78f;
        var width = (entries.Length - 1) * step + ResolutionCardSide;
        // Above the hand tray at its right-hand end: the table stays readable, and the played card sits where the player's eye already is when a window opens.
        var group = Place(_hand, new Control { Name = "ResolutionArea", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 30 }, _hand.Size.X - width - 28, -ResolutionCardSide - 6, width, ResolutionCardSide);
        var caption = Palette.Text(entries.Length == 1 ? "Being resolved" : "Being resolved · oldest first", 15, Palette.Gold);
        caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        Place(group, caption, 6, -24, Math.Max(width, 220), 22);
        for (var i = 0; i < entries.Length; i++)
        {
            var tile = Tile(entries[i].Card, true, ResolutionCardSide, ResolutionCardSide);
            tile.Material = CardPaper.FixedTable;
            tile.ContextHelp = $"{(entries[i].OwnerId == View.ViewerId ? "Your" : "Opponent's")} card being resolved: {entries[i].AbilityLabel}. It is discarded when the action ends.";
            Place(group, tile, i * step, 0, ResolutionCardSide, ResolutionCardSide);
        }
    }
}
