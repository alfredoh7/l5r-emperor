namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION FOLLOW-UPS (issue #498): THE RING SEARCH PRIMITIVE and the three cards it unblocks -- Enslaved Djinn (Gates of Chaos, a Follower), Celestial Sword of the Dragon - exp
// (Seeds of Decay, an Item) and Asako Chukage (Forgotten Legacy, a Personality). Each was read letter by letter against its scan (the scans are the authoritative text).
//
//   * SEARCHING FOR A RING is an ordinary search (DeckSearch.cs): its criteria key off the Ring CARD TYPE (SearchCriteria.Types [Ring]), optionally a title ("Ring of Water"). Ordered searches (Mendon's R4
//     ruling) apply unchanged: a qualifying card must be taken from the FIRST place that holds one, the searcher chooses among that place's qualifying cards, later places are not shown, and a searched deck is
//     always reshuffled.
//   * THE HAND AS A SEARCHABLE PLACE (SearchDeck.Hand): listed like a discard pile (the searcher's own cards), never shuffled; a card taken from it leaves the hand by changing zone (put into play) or simply
//     stays there (put into the hand). Asako Chukage searches "your discard pile, then hand, then deck".
//   * "PUT IT INTO PLAY OR YOUR HAND" (SearchEffect.RingIntoPlayOrHand): once the Ring is shown the searcher chooses (SearchStage.ChooseDestination, PendingChoice "searchDestination", command
//     "chooseSearchDestination" with Choice "play" or "hand"). Into play it goes through the Ring entry path Togashi's Guidance uses (home, face up, the turn noted for Brotherhood Schism, "a Ring entered play"
//     recorded when an action is resolving) and carries the Enlightenment exclusion mark (NoEnlightenmentKey): while it remains in play it does not count towards an Enlightenment Victory (Mendon's ruling).
//   * "AFTER THIS CARD ENTERS PLAY: SEARCH ..." TRAITS (no designator: mandatory). A search is a decision, so it cannot open inside the entering sequence (the card's own entry windows would overwrite it): it
//     is queued (MatchState.PendingEntrySearches) and opened as soon as the entry windows are done -- drained where the queued entry decisions already are (OpenOverconfidentDrawWindow, NextAction). Its
//     search has the origin "entry" and, when it ends, hands priority back where the entry left it (PendingSearch.ResumeTo).
//   * Asako Chukage's Reaction: "Even if Chukage is bowed, after the resolution of an action that moved your Personality: Move Chukage to the Personality's location. Straighten Chukage's unit if he moved."
//     -- the after-action-resolved window (ActionEffectTracking.cs) reading the action's recorded moves.
// ======================================================================================================================================================================================

/// <summary>An "After this card enters play: search ..." trait waiting for the entry windows to close (MatchState.PendingEntrySearches).</summary>
public sealed class PendingEntrySearch
{
    public int PlayerId { get; set; }
    public string SourceCardId { get; set; } = "";
    public string SearchId { get; set; } = "";
}

public static class EmperorFollowupsSearches
{
    private static readonly SearchCriteria EnslavedDjinnCard = new("card titled Enslaved Djinn", Titles: ["Enslaved Djinn"]);
    private static readonly SearchCriteria RingCard = new("Ring", [CardType.Ring]);
    private static readonly SearchCriteria RingOfWaterCard = new("Ring of Water", [CardType.Ring], Titles: ["Ring of Water"]);

    /// <summary>Enslaved Djinn (oracle-l5r-10680): "After this Follower enters play, search your discard pile, then deck, for another Enslaved Djinn. Show it. Put it into your hand."</summary>
    public static SearchSpec EnslavedDjinn { get; } = new("enslaved-djinn", "Search your discard pile, then deck, for another Enslaved Djinn",
    [
        new(SearchDeck.FateDiscard, EnslavedDjinnCard, SearchEffect.ToHand),
        new(SearchDeck.Fate, EnslavedDjinnCard, SearchEffect.ToHand)
    ], Ordered: true);

    /// <summary>Celestial Sword of the Dragon - exp (oracle-l5r-10112): "After this card enters play: Search your discard pile, then deck, for a Ring. Show it. Put it into play or your hand."</summary>
    public static SearchSpec DragonSwordRing { get; } = new("celestial-sword-dragon-ring", "Search your discard pile, then deck, for a Ring",
    [
        new(SearchDeck.FateDiscard, RingCard, SearchEffect.RingIntoPlayOrHand),
        new(SearchDeck.Fate, RingCard, SearchEffect.RingIntoPlayOrHand)
    ], Ordered: true);

    /// <summary>Asako Chukage (oracle-l5r-533): "After Chukage enters play: Search your discard pile, then hand, then deck, for a Ring of Water. Put it into play or in your hand."</summary>
    public static SearchSpec ChukageRingOfWater { get; } = new("chukage-ring-of-water", "Search your discard pile, then hand, then deck, for a Ring of Water",
    [
        new(SearchDeck.FateDiscard, RingOfWaterCard, SearchEffect.RingIntoPlayOrHand),
        new(SearchDeck.Hand, RingOfWaterCard, SearchEffect.RingIntoPlayOrHand),
        new(SearchDeck.Fate, RingOfWaterCard, SearchEffect.RingIntoPlayOrHand)
    ], Ordered: true);

    public static IEnumerable<SearchSpec> All => [EnslavedDjinn, DragonSwordRing, ChukageRingOfWater];
}

public sealed partial class MatchEngine
{
    // The Effect keys of the three cards (their published Effect field).
    internal const string EnslavedDjinnEffect = "enslaved-djinn-search";
    internal const string DragonSwordEffect = "celestial-sword-force-dragon";
    internal const string ChukageEffect = "chukage-ring-search";
    internal const string ChukageFollowId = "chukage-follow";
    internal const string ChukageFollowTarget = "followups-own-personality-moved-by-last-action";
    /// <summary>The PendingSearch.Origin of an "After this card enters play: search ..." trait's search.</summary>
    internal const string EntrySearchOrigin = "entry";
    private const string DestinationPlay = "play";
    private const string DestinationHand = "hand";

    // ---- "After this card enters play": the mandatory traits (ApplyMandatoryEntryEffects' hook) ----

    private void ApplyEmperorFollowupsEntryEffects(int playerId, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case EnslavedDjinnEffect:
                QueueEntrySearch(playerId, card, EmperorFollowupsSearches.EnslavedDjinn);
                break;
            // "After this card enters play: Gain 2 Honor." and "After this card enters play: Search ..." -- two traits, in their printed order.
            case DragonSwordEffect:
                GainHonor(playerId, 2, Definition(card).Name, card);
                QueueEntrySearch(playerId, card, EmperorFollowupsSearches.DragonSwordRing);
                break;
            case ChukageEffect:
                QueueEntrySearch(playerId, card, EmperorFollowupsSearches.ChukageRingOfWater);
                break;
        }
    }

    private void QueueEntrySearch(int playerId, CardInstance card, SearchSpec spec)
    {
        _state.PendingEntrySearches.Add(new PendingEntrySearch { PlayerId = playerId, SourceCardId = card.Id, SearchId = spec.Id });
        Emit("entrySearchQueued", $"{Definition(card).Name}: {Player(playerId).Name} will search once it has entered play ({spec.Label}).", playerId, card.Id);
    }

    /// <summary>Opens the next queued entry search, if any, handing priority to <paramref name="resumeTo"/> once it ends. True when a search opened.</summary>
    private bool OpenQueuedEntrySearch(int resumeTo)
    {
        if (_state.PendingEntrySearches.Count == 0 || _state.PendingSearch is not null || _state.WinnerId is not null) return false;
        var next = _state.PendingEntrySearches[0];
        _state.PendingEntrySearches.RemoveAt(0);
        if (!_state.Cards.TryGetValue(next.SourceCardId, out var source) || !SearchCatalog.All.TryGetValue(next.SearchId, out var spec)) return OpenQueuedEntrySearch(resumeTo);
        var running = _runningCommand; _runningCommand = null; // a trait of the card that entered, not an effect of whatever action is (or was) resolving
        try { BeginSearch(spec, source, next.PlayerId, EntrySearchOrigin); }
        finally { _runningCommand = running; }
        _state.PendingSearch!.ResumeTo = resumeTo;
        return true;
    }

    // ---- "Put it into play or your hand" ----

    private void BeginSearchDestination(PendingSearch search, CardInstance ring)
    {
        search.Stage = SearchStage.ChooseDestination;
        _state.PendingChoice = new("searchDestination", search.PlayerId, search.SourceCardId, $"{Definition(Card(search.SourceCardId)).Name}: put {Definition(ring).Name} into play or into your hand.");
    }

    private void ChooseSearchDestination(PendingSearch search, GameCommand command)
    {
        Require(command.Type == "chooseSearchDestination" && command.Choice is DestinationPlay or DestinationHand, "Put the card into play or into your hand.");
        var ring = Card(search.SelectedCardId);
        if (command.Choice == DestinationHand) MoveSearchedCardToHand(search, ring);
        else PutSearchedRingIntoPlay(search, ring);
        FinishSearch(search);
    }

    private IEnumerable<ActionOption> SearchDestinationActions(PendingSearch search, int playerId)
    {
        var ring = Card(search.SelectedCardId);
        yield return new("chooseSearchDestination", $"Put {Definition(ring).Name} into play (it will not count towards an Enlightenment Victory)", true, null,
            new GameCommand("chooseSearchDestination", playerId, _state.Version, Choice: DestinationPlay), ring.Id);
        yield return new("chooseSearchDestination", $"Put {Definition(ring).Name} into your hand", true, null,
            new GameCommand("chooseSearchDestination", playerId, _state.Version, Choice: DestinationHand), ring.Id);
    }

    // The Ring entry path (Togashi's Guidance's): the Ring leaves the searched place, enters play at home face up, and carries the Enlightenment exclusion while it remains in play.
    private void PutSearchedRingIntoPlay(PendingSearch search, CardInstance ring)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(ring.Id);
        player.Hand.Remove(ring.Id); // a Ring found in the hand leaves it by changing zone
        ring.Zone = Zone.Home; ring.ProvinceIndex = null; ring.FaceUp = true; ring.Bowed = false;
        ring.AbilityUses[NoEnlightenmentKey] = 1;
        player.RingPutIntoPlayTurn = _state.TurnNumber;
        Emit("cardEnteredPlay", $"{player.Name} put {Definition(ring).Name} into play ({Definition(Card(search.SourceCardId)).Name}); while it remains in play it does not count towards an Enlightenment Victory.", player.Id, ring.Id);
        if (_runningCommand is not null) RecordActionEffect(ring.Id, RingEnteredKind); // "after one of your Rings enters play" reads the resolving action's record
    }

    // ---- Asako Chukage's Reaction ----

    // "Your Personality" the last resolved action moved: one of Chukage's controller's Personalities in play (not Chukage himself: he cannot be moved to his own location) the action recorded as moved.
    private bool ChukageFollowTargetAllows(GameCommand command, PlayerState player, CardInstance? source, CardInstance target) =>
        Definition(target).Type == CardType.Personality && target.OwnerId == player.Id && InPlay(target) && source is not null && target.Id != source.Id
        && _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == MovedKind && a.CardId == target.Id);

    private bool ChukageFollowApplies(CardInstance source, int playerId) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == MovedKind && a.CardId != source.Id && _state.Cards.TryGetValue(a.CardId, out var moved)
            && moved.OwnerId == playerId && InPlay(moved) && Definition(moved).Type == CardType.Personality);

    // "Move Chukage to the Personality's location. Straighten Chukage's unit if he moved."
    private void ChukageFollow(CardInstance chukage, CardInstance personality)
    {
        var moved = false;
        if (personality.Zone == Zone.Home && chukage.Zone != Zone.Home)
        {
            MoveHome(chukage, false);
            moved = chukage.Zone == Zone.Home;
        }
        else if (personality.Zone == Zone.Battlefield && personality.ProvinceIndex is int province && !(chukage.Zone == Zone.Battlefield && chukage.ProvinceIndex == province))
            moved = TryMoveToBattlefield(chukage, province);
        if (!moved) { Emit("chukageStayed", $"{Definition(chukage).Name} did not move, so his unit is not straightened.", chukage.OwnerId, chukage.Id); return; }
        StraightenUnit(chukage);
    }
}
