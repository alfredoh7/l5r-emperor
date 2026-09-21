namespace L5R.Core;

// Kata (issue #420, Mendon's ruling on Skipping the Puddle): a Kata is a keyword, on a Strategy that is put into play instead of being played and discarded. It sits at the right of its controller's
// home like a Ring (Zone.Home, in play), stays there until it is discarded or destroyed, and while it is there its controller has the action printed on it ("You have the ability, ..."), usable once
// per turn like any action. A player has one Kata at a time: putting one into play discards all his other Kata.
public sealed partial class MatchEngine
{
    private void EnterPlayAsKata(PlayerState player, CardInstance kata)
    {
        foreach (var other in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Home && c.Id != kata.Id && Definition(c).Type == CardType.Strategy && Keyword(c, "Kata")).ToArray())
        {
            ResetCardState(other);
            DiscardFate(player, other);
        }
        player.Hand.Remove(kata.Id); kata.Zone = Zone.Home; kata.FaceUp = true;
        Emit("kataEnteredPlay", $"{player.Name} puts {Definition(kata).Name} into play.", player.Id, kata.Id);
    }
}
