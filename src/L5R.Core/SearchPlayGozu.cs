namespace L5R.Core;

// ======================================================================================================================================================================================
// GOZU (oracle-l5r-2887): "Battle: Target a Terrain: Destroy it. You may discard a card. If you did, search your Fate deck for a Terrain, show it, then take an additional Battle action to play it,
// if legal; if not, discard it."
//
// Mendon's ruling: "Once the optional action of discarding a card is selected, the terrain play is mandatory and the player may not pass; if there is at least one terrain, they must select a
// terrain and then it is played."  The flow:
//   1. Destroy the Terrain (Control the Board's destruction). The discard is the second way of taking the action (sp-gozu-destroy-discard): the card rides on the command, as Ikoma Shika - exp's does.
//   2. The search (SearchPlaySpecs.Gozu): the searcher must take a Terrain when the Fate deck holds one; Pass is only legal when none qualifies. The Terrain is shown (the one search flow) and goes to the
//      hand (the card is "in the deck" only until it is found), and the deck is shuffled as every search ends.
//   3. FinishSearch's hook (SearchPlayFinishSearch): the engine asks the one source of "what may this player do now" -- QueryActions -- whether the found Terrain could be played at this moment. If
//      so, a MANDATORY additional action scoped to it is granted and the play is OWED (MatchState.SearchPlayOwes). If not (a Terrain already at the battlefield, no unit, the Gold, Jahan, a lock
//      on additional actions ...) the card is discarded: "if not, discard it".
//   4. While the play is owed, at an ordinary action opportunity of that player, only the play (a playStrategy or the card's own action) is legal: the pass, "Pass all battles", the decline of the
//      additional action and every other action are refused and listed disabled with the reason (EnforceSearchPlay / ApplySearchPlayToOptions). If the card can no longer be played by then (the
//      opponent changed the battle in between), the debt lapses and the card is discarded, so the player is never left without a legal action.
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    private bool _searchPlayProbe;

    // Gozu's "Destroy it" (Control the Board's own: the Terrain leaves play, a destroyed-a-Terrain record is kept for Ring of Water).
    private void SearchPlayDestroyTerrain(PlayerState player, CardInstance terrain)
    {
        DiscardTerrain(terrain);
        if (terrain.Zone != Zone.Battlefield) player.TerrainDestroyedThisBattle = true;
    }

    // ---- the found Terrain ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // FinishSearch's hook: the Terrain Gozu's search found is in the hand and is played now, or discarded.
    private void SearchPlayFinishSearch(PendingSearch search)
    {
        if (search.SearchId != SearchPlaySpecs.GozuId || search.SelectedCardId is not { } foundId || !_state.Cards.TryGetValue(foundId, out var found) || found.Zone != Zone.Hand) return;
        var player = Player(search.PlayerId);
        if (!SearchPlayCardPlayable(player.Id, found.Id))
        {
            Emit("searchPlayDiscarded", $"{Definition(found).Name} cannot be played now, so {player.Name} discards it ({Definition(Card(search.SourceCardId)).Name}).", player.Id, found.Id);
            DiscardFate(player, found);
            return;
        }
        var held = _state.AdditionalActions.Count;
        GrantAdditionalAction(player.Id, [found.Id], mandatory: true, id: SearchPlayAbilities.GozuGrantId);
        if (_state.AdditionalActions.Count == held) // an effect forbids new additional actions this battle: the action cannot be taken
        {
            Emit("searchPlayDiscarded", $"{player.Name} may not take an additional action to play {Definition(found).Name}, so it is discarded ({Definition(Card(search.SourceCardId)).Name}).", player.Id, found.Id);
            DiscardFate(player, found);
            return;
        }
        _state.SearchPlayOwes.Add(new(player.Id, found.Id, Definition(Card(search.SourceCardId)).Name));
        Emit("searchPlayOwed", $"{player.Name} must take an additional Battle action to play {Definition(found).Name} ({Definition(Card(search.SourceCardId)).Name}).", player.Id, found.Id);
    }

    // Whether the card in the player's hand could be played by him now: the engine's own listing of his actions, as it would stand once the search has ended (no decision open), holds an enabled
    // way to play it. The probe nests no further (the listing reads the filter below, which a probe leaves alone).
    private bool SearchPlayCardPlayable(int playerId, string cardId)
    {
        if (_searchPlayProbe) return true;
        var choice = _state.PendingChoice;
        _searchPlayProbe = true; _state.PendingChoice = null;
        try { return QueryActions(playerId).Any(o => o.Enabled && o.CardId == cardId && o.Type is "playStrategy" or "useAbility"); }
        finally { _state.PendingChoice = choice; _searchPlayProbe = false; }
    }

    // FinishDeferredAction's hook: the player who was just granted the owed play keeps his priority to take it.
    private bool SearchPlayKeepsPriority(int playerId) => _state.AdditionalActions.Any(g => g.PlayerId == playerId && g.Id == SearchPlayAbilities.GozuGrantId);

    // ---- the debt -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private SearchPlayOwed? SearchPlayOwedBy(int playerId) =>
        _state.SearchPlayOwes.FirstOrDefault(o => o.PlayerId == playerId && _state.Cards.TryGetValue(o.CardId, out var card) && card.Zone == Zone.Hand && card.OwnerId == playerId);

    // A debt whose card has left the hand (played, or discarded by something else) is paid or void: it and its grant go.
    private void PruneSearchPlay()
    {
        if (_state.SearchPlayOwes.Count == 0) return;
        foreach (var gone in _state.SearchPlayOwes.Where(o => !_state.Cards.TryGetValue(o.CardId, out var card) || card.Zone != Zone.Hand).ToArray())
        {
            _state.SearchPlayOwes.Remove(gone);
            _state.AdditionalActions.RemoveAll(g => g.PlayerId == gone.PlayerId && g.Id == SearchPlayAbilities.GozuGrantId && (g.ScopeCardIds?.Contains(gone.CardId) ?? false));
        }
    }

    private string SearchPlayReason(SearchPlayOwed owed) =>
        $"You must take an additional Battle action to play {(_state.Cards.TryGetValue(owed.CardId, out var card) ? Definition(card).Name : "the card you found")} ({owed.SourceName}); you may not pass.";

    // Apply()'s check, on the command of a player who owes the play at an ordinary action opportunity: everything but the play is refused. A play that can no longer be made releases him (the card is
    // discarded, "if not, discard it").
    private void EnforceSearchPlay(GameCommand command)
    {
        PruneSearchPlay();
        if (_state.SearchPlayOwes.Count == 0 || !AtOrdinaryActionOpportunity() || SearchPlayOwedBy(command.PlayerId) is not { } owed) return;
        if (command.Type is "playStrategy" or "useAbility" && command.CardId == owed.CardId) return;
        if (command.Type is not ("pass" or "declineAdditionalAction" or "passAllBattles" or "playStrategy" or "useAbility" or "recruitReserve" or "equip")) return;
        if (!SearchPlayCardPlayable(command.PlayerId, owed.CardId))
        {
            var player = Player(command.PlayerId);
            Emit("searchPlayDiscarded", $"{Definition(Card(owed.CardId)).Name} can no longer be played, so {player.Name} discards it ({owed.SourceName}).", player.Id, owed.CardId);
            DiscardFate(player, Card(owed.CardId));
            PruneSearchPlay();
            return;
        }
        throw new RuleViolation(SearchPlayReason(owed));
    }

    // The same rule as the player's offered actions: what he could not do while the play is owed is listed disabled, with the reason (the play itself stays enabled).
    private ActionOption[] ApplySearchPlayToOptions(int playerId, ActionOption[] options)
    {
        if (_searchPlayProbe || _state.SearchPlayOwes.Count == 0 || !AtOrdinaryActionOpportunity() || SearchPlayOwedBy(playerId) is not { } owed) return options;
        bool IsPlay(ActionOption o) => o.CardId == owed.CardId && o.Type is "playStrategy" or "useAbility";
        if (!options.Any(o => o.Enabled && IsPlay(o))) return options; // it can no longer be played: nothing is held back (the first command settles the debt)
        var reason = SearchPlayReason(owed);
        return options.Select(o => o.Type == "concede" || IsPlay(o) ? o : o with { Enabled = false, Reason = reason }).ToArray();
    }
}
