namespace L5R.Core;

// ======================================================================================================================================================================================
// THE SACRED ROSARY OF THE CONSTRICTORS (oracle-l5r-9982): "Will only attach to a Phoenix Clan or Naga Personality. Limited: Discard all your Celestials in play. Search your Dynasty deck or
// discard pile for a Celestial. Put it into play. If you searched your discard pile for it, remove this card from the game."
//
// Mendon: "Create a primitive: a search effect that puts a Celestial into play with no province, plus remember which of the two places the card came from to remove the Rosary."
//   * The search is the ordinary one (SearchPlaySpecs.Rosary): the searcher chooses the Dynasty deck or the Dynasty discard pile (SearchStage.ChooseDeck), must take a Celestial when the place holds
//     one (a pass is legal only when none qualifies), the card is shown, and a searched deck is reshuffled (a discard pile is not).
//   * "Put it into play" is SearchPlaySpecs.CelestialIntoPlay: the card leaves the place it was found in and enters play through the one Celestial entry (EnterCelestialPlay, which
//     ResolveCelestial's province refill also uses): it takes no province, discards no other Celestial of the controller's (they were all discarded as the action's first step, and are then in the discard
//     pile the search may read), and runs the entry traits of Celestials in play (Daigotsu's Guidance).
//   * Which place it came from is the search's own Deck (PendingSearch.Deck): found in the Dynasty discard pile, the Rosary is removed from the game as the Celestial enters play. A search that found
//     nothing (a pass) did not "search the discard pile for it" in the sense the text means -- nothing was taken from it -- and removes nothing.
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    // The action: every Celestial of the player's in play is discarded, then the search opens.
    private bool SearchPlayRosary(RunHookContext context)
    {
        var (player, source) = (context.player, context.source!);
        var hadRyoshun = ControlsCelestialEffect(player.Id, RyoshunsGuidanceEffect);
        foreach (var celestial in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Celestial).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            celestial.Zone = Zone.DynastyDiscard; celestial.FaceUp = true;
            Emit("celestialDiscarded", $"{Definition(celestial).Name} was discarded ({Definition(source).Name}).", player.Id, celestial.Id);
        }
        if (hadRyoshun) CheckChiAfterRyoshunLeaves(player.Id); // Ryoshun's Guidance: its +1C left with it
        BeginSearch(SearchPlaySpecs.Rosary, source, player.Id, "ability", context.bowedBeforeAction);
        return true;
    }

    // DeckSearch's default branch: the searched card's effect when it is not one of the shared ones. True when this cluster handled it (and finished the search).
    private bool SearchPlayApplyEffect(PendingSearch search, CardInstance card, string effect)
    {
        if (effect != SearchPlaySpecs.CelestialIntoPlay) return false;
        var player = Player(search.PlayerId);
        var fromDiscard = search.Deck == SearchDeck.DynastyDiscard;
        SearchDeckList(player, search.Deck!).Remove(card.Id); // a discard pile's list is a copy: the change of zone below is what takes the card from it
        card.ProvinceIndex = null;
        EnterCelestialPlay(player, card);
        if (fromDiscard && _state.Cards.TryGetValue(search.SourceCardId, out var rosary) && rosary.Zone == Zone.Attached)
        {
            var host = rosary.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var found) ? found : null;
            RemoveFromGame(rosary);
            if (host is not null && InPlay(host) && Definition(host).Type == CardType.Personality && PersonalityChi(host) <= 0) DestroyUnits([host], true); // a Chi it only had thanks to the Item
        }
        FinishSearch(search);
        return true;
    }
}
