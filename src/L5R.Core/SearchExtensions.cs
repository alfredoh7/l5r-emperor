namespace L5R.Core;

// ======================================================================================================================================================================================
// P20 (issue #728): SEARCH AND LOOK EXTENSIONS. No card is wired; every piece is proved with test-only fixtures (tests/L5R.Core.Tests/SearchExtensionsTests.cs, suite "search-extensions") and
// with the isolated "p20-*" review positions of SearchExtensionsShowcase.cs. docs/decisions/2026-10-07-p20-search-look-extensions.md has the API and a worked registration.
//
//   SOURCES (SearchDeckOption.Deck)
//     OwnProvinces            the searcher's own province cards, face-up OR face-down. Only the searcher sees the list; the other seat learns that a search happened and sees each card the moment it
//                             is SHOWN. Nothing is reshuffled; a card that leaves a province is replaced from the Dynasty deck, face down, as for any card that leaves one.
//     AnyPlayerFateDiscard    either player's Fate discard pile, and AnyPlayerDynastyDiscard either player's Dynasty discard pile (the dead pile included). Never reshuffled. A card of the other
//                             player's can only be put into play, attached or removed from the game (PutIntoPlay, CelestialIntoPlay, AttachToPersonality, RemoveFromGame), under the searcher's control
//                             until it leaves play (TakeControl, as Black Pearl's); never into his hand.
//     SearchSpec.ChooseOne    the searcher picks the source. Several sources already meant a choice; Ordered still forces the first (R-011) and wins over ChooseOne.
//   CRITERIA (SearchCriteria): GoldProducing (HasGoldProducingTrait) and DifferentTitles (a condition on the SET taken: no two cards with the same title).
//   SELECTION: SearchSpec.SelectCount(Min, Max): the searcher takes between Min and Max cards in ONE command (selectSearchCards, CardIds = definition ids, in the order taken). When fewer than Min can be
//     taken (the places hold fewer qualifying cards, or fewer different titles) the most that can be taken is required, so a search is never impossible; Min 0 lets the searcher take none.
//   DISPOSITIONS (SearchEffect): AttachToPersonality (a follow-up target slot: the Personality to attach to), Bottom (after the shuffle, for a deck), CelestialIntoPlay, RecruitFromProvinceIgnoringCosts,
//     PlayAsAdditionalAction (the owed-play machinery of SearchPlayGozu.cs), and the plain Discard and RemoveFromGame.
//   OTHER PLAYER CHOOSES: SearchSpec.OtherPlayerChooses(ChosenEffect): after the picks are shown, the OTHER player chooses one of the shown cards (P25's shown choice: he sees only the shown definitions,
//     named by their position in MatchView.ShownCards); that one gets ChosenEffect and the rest the option's own Effect.
// ======================================================================================================================================================================================

/// <summary>Take between <paramref name="Min"/> and <paramref name="Max"/> cards (SearchSpec.SelectCount).</summary>
public sealed record SearchSelectCount(int Min, int Max);

/// <summary>"The other player chooses one of them": the chosen card gets <paramref name="ChosenEffect"/> (Discard, RemoveFromGame, Bottom or ToHand), the others the option's own Effect.</summary>
public sealed record SearchOtherChoice(string ChosenEffect);

public sealed partial class MatchEngine
{
    internal const string SearchOtherChoiceSpecId = "p20-search-other-chooses", SearchSelectManyCommand = "selectSearchCards", SearchAttachCommand = "chooseSearchAttachTarget";

    // The dispositions a multi-card selection and an other-player choice can apply to each card (none needs a follow-up decision of the searcher's).
    private static readonly string[] SearchSimpleEffects = [SearchEffect.ToHand, SearchEffect.Bottom, SearchEffect.Discard, SearchEffect.RemoveFromGame];
    private static readonly string[] SearchProvinceEffects = [SearchEffect.RecruitFromProvinceIgnoringCosts, SearchEffect.CelestialIntoPlay, SearchEffect.Bottom, SearchEffect.Discard, SearchEffect.RemoveFromGame];
    private static readonly string[] SearchForeignEffects = [SearchEffect.PutIntoPlay, SearchEffect.CelestialIntoPlay, SearchEffect.AttachToPersonality, SearchEffect.RemoveFromGame];

    // ---- the spec check ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // A spec that combines things the flow cannot do is an authoring error: refused before anything changes (so a test sees it, and no half-open search is left).
    private static void ValidateSearchSpec(SearchSpec spec)
    {
        foreach (var option in spec.Decks)
        {
            if (option.Deck is SearchDeck.AnyPlayerFateDiscard or SearchDeck.AnyPlayerDynastyDiscard && !SearchForeignEffects.Contains(option.Effect))
                throw new InvalidOperationException($"Search {spec.Id}: a card from either player's discard pile can only be put into play, attached or removed from the game.");
            if (option.Deck == SearchDeck.OwnProvinces && !SearchProvinceEffects.Contains(option.Effect))
                throw new InvalidOperationException($"Search {spec.Id}: a province card cannot take the {option.Effect} disposition.");
            if (option.Effect == SearchEffect.RecruitFromProvinceIgnoringCosts && option.Deck != SearchDeck.OwnProvinces)
                throw new InvalidOperationException($"Search {spec.Id}: a card is recruited from a province only when the province is searched.");
            if (spec.SelectCount is { Max: > 1 } && !SearchSimpleEffects.Contains(option.Effect))
                throw new InvalidOperationException($"Search {spec.Id}: a selection of several cards applies only a plain disposition.");
        }
        if (spec.SelectCount is { } count && (count.Min < 0 || count.Max < Math.Max(1, count.Min)))
            throw new InvalidOperationException($"Search {spec.Id}: the selection count is not valid.");
        if (spec.OtherPlayerChooses is { } other && (spec.SelectCount is not { Max: > 1 } || !SearchSimpleEffects.Contains(other.ChosenEffect)))
            throw new InvalidOperationException($"Search {spec.Id}: the other player chooses among at least two cards and the chosen one takes a plain disposition.");
    }

    private static bool SearchMayTakeNone(SearchSpec spec) => spec.SelectCount?.Min == 0;

    // ---- places ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string? SearchExtPlace(string deck) => deck switch
    {
        SearchDeck.OwnProvinces => "provinces",
        SearchDeck.AnyPlayerFateDiscard => "Fate discard piles",
        SearchDeck.AnyPlayerDynastyDiscard => "Dynasty discard piles",
        _ => null
    };

    // A fresh list of the place's cards (taking one is its change of zone, or its province slot being cleared).
    private List<string> SearchExtList(PlayerState player, string deck) => deck switch
    {
        SearchDeck.OwnProvinces => _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.Province && c.ProvinceIndex is not null)
            .OrderBy(c => c.ProvinceIndex).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList(),
        SearchDeck.AnyPlayerFateDiscard => _state.Cards.Values.Where(c => c.Zone == Zone.FateDiscard).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList(),
        _ => _state.Cards.Values.Where(c => c.Zone is Zone.DynastyDiscard or Zone.Dead).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList()
    };

    // What the disposition can do with a card found in this place, as an eligibility reason on the card.
    private (bool Eligible, string? Reason) SearchExtGate(PendingSearch search, SearchDeckOption option, CardInstance card, bool eligible, string? reason)
    {
        if (!eligible) return (eligible, reason);
        var def = Definition(card);
        switch (option.Effect)
        {
            case SearchEffect.AttachToPersonality when SearchAttachHosts(search.PlayerId, card).Length == 0:
                return (false, "No Personality you control can have it attached");
            case SearchEffect.RecruitFromProvinceIgnoringCosts:
                if (def.Type is not (CardType.Personality or CardType.Holding)) return (false, "Only a Personality or a Holding can be recruited");
                if ((Keyword(card, "Unique") || Keyword(card, "Singular")) && ControlsCopyOf(search.PlayerId, def.Name)) return (false, "You already control this Unique card");
                if (ProvinceEntryLockError(card) is { } locked) return (false, locked);
                break;
        }
        return (true, null);
    }

    // Removes the card from the place it was found and returns the province it leaves (to be refilled once the card has gone where it is going), if any.
    private ProvinceState? SearchExtTake(PendingSearch search, CardInstance card)
    {
        var player = Player(search.PlayerId);
        if (search.Deck == SearchDeck.OwnProvinces)
        {
            var province = player.Provinces.FirstOrDefault(p => p.CardId == card.Id);
            if (province is not null) province.CardId = null;
            else if (player.Provinces.FirstOrDefault(p => p.CardId2 == card.Id) is { } second) second.CardId2 = null; // a two-slot province's second card: that slot is not refilled
            card.ProvinceIndex = null;
            return province;
        }
        SearchDeckList(player, search.Deck!).Remove(card.Id); // a discard pile's list is a copy: the change of zone is what takes the card from it
        return null;
    }

    private void SearchExtRefill(PlayerState player, ProvinceState? province)
    {
        if (province is not null) Refill(player, province);
    }

    // ---- the dispositions --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // DeckSearch's default branch for a single card. True when this feature handled the effect (and finished or advanced the search).
    private bool SearchExtApplyEffect(PendingSearch search, CardInstance card, string effect)
    {
        var player = Player(search.PlayerId);
        switch (effect)
        {
            case SearchEffect.Bottom or SearchEffect.Discard or SearchEffect.RemoveFromGame:
                ApplySimpleSearchEffect(search, card, effect);
                FinishSearch(search);
                return true;
            case SearchEffect.PlayAsAdditionalAction:
                MoveSearchedCardToHand(search, card); // the owed play itself is FinishSearch's hook (SearchPlayFinishSearch)
                FinishSearch(search);
                return true;
            case SearchEffect.CelestialIntoPlay:
                var province = SearchExtTake(search, card);
                if (card.OwnerId != player.Id) TakeControl(card, player.Id);
                card.ProvinceIndex = null;
                EnterCelestialPlay(player, card); // no province refill, the controller's other Celestials are discarded
                SearchExtRefill(player, province);
                FinishSearch(search);
                return true;
            case SearchEffect.RecruitFromProvinceIgnoringCosts:
                var from = SearchExtTake(search, card);
                if (from is not null) { _state.LastProvinceEntryCardId = card.Id; _state.LastProvinceEntryProvinceIndex = from.Index; }
                EnterPlayFromRecruit(player, card, null, Definition(Card(search.SourceCardId)).Name); // no Gold, no Honor Requirement
                SearchExtRefill(player, from);
                search.EnteredCardId = card.Id; // the entering-play windows open as the search ends (FinishSearch)
                FinishSearch(search);
                return true;
            case SearchEffect.AttachToPersonality:
                BeginSearchAttach(search, card);
                return true;
            default:
                return false;
        }
    }

    // The plain dispositions a selection of several cards (and the other player's choice) applies to each card.
    private void ApplySimpleSearchEffect(PendingSearch search, CardInstance card, string effect)
    {
        var player = Player(search.PlayerId);
        switch (effect)
        {
            case SearchEffect.ToHand:
                MoveSearchedCardToHand(search, card);
                break;
            case SearchEffect.Bottom:
                if (search.Deck is SearchDeck.Dynasty or SearchDeck.Fate) (search.PutOnBottomIds ??= []).Add(card.Id); // stays in the deck; moved after the shuffle
                else
                {
                    var province = SearchExtTake(search, card);
                    var dynasty = !IsFateCardType(Definition(card).Type);
                    card.Zone = dynasty ? Zone.DynastyDeck : Zone.FateDeck; card.FaceUp = false; card.ProvinceIndex = null;
                    (dynasty ? player.DynastyDeck : player.FateDeck).Add(card.Id);
                    Emit("toDeck", $"{Definition(card).Name} was placed on the bottom of {player.Name}'s {(dynasty ? "Dynasty" : "Fate")} deck.", player.Id, card.Id);
                    SearchExtRefill(player, province);
                }
                break;
            case SearchEffect.Discard:
                var discardedFrom = SearchExtTake(search, card);
                card.Zone = IsFateCardType(Definition(card).Type) ? Zone.FateDiscard : Zone.DynastyDiscard; card.FaceUp = true; card.ProvinceIndex = null;
                Emit("searchDiscarded", $"{Definition(card).Name} was discarded.", card.OwnerId, card.Id);
                SearchExtRefill(player, discardedFrom);
                break;
            case SearchEffect.RemoveFromGame:
                var removedFrom = SearchExtTake(search, card);
                RemoveFromGame(card);
                SearchExtRefill(player, removedFrom);
                break;
            default:
                throw new RuleViolation("Unknown search effect.");
        }
    }

    // FinishSearch, after the shuffle: the cards that go to the bottom of the searched deck.
    private void SearchExtPutOnBottom(PendingSearch search)
    {
        if (search.PutOnBottomIds is not { Count: > 0 } ids) return;
        var player = Player(search.PlayerId); var deck = SearchDeckList(player, search.Deck!);
        foreach (var id in ids)
            if (deck.Remove(id)) deck.Add(id);
        Emit("searchedCardOnBottom", $"{player.Name} put {ids.Count} found card{(ids.Count == 1 ? "" : "s")} on the bottom of their {SearchPlace(search.Deck!)} after shuffling.", search.PlayerId);
    }

    // The disposition that plays the found card: Gozu's owed-play machinery also serves a spec whose effect is PlayAsAdditionalAction (SearchPlayGozu.cs reads this).
    private bool SearchExtPlaysAfter(PendingSearch search) =>
        search.Deck is not null && SearchCatalog.All.TryGetValue(search.SearchId, out var spec) && spec.Decks.FirstOrDefault(d => d.Deck == search.Deck)?.Effect == SearchEffect.PlayAsAdditionalAction;

    // ---- AttachToPersonality: a follow-up target slot ---------------------------------------------------------------------------------------------------------------------------------

    private CardInstance[] SearchAttachHosts(int playerId, CardInstance attachment)
    {
        var def = Definition(attachment);
        if ((Keyword(attachment, "Unique") || Keyword(attachment, "Singular")) && ControlsCopyOf(playerId, def.Name)) return [];
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && AttachmentLegalTarget(def, c) && AttachmentAllowed(attachment, c))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    }

    // The attachment goes onto a Personality of the searcher's choice, with no Gold paid (the printed text says "attach", not "pay"); one candidate is no choice.
    private void BeginSearchAttach(PendingSearch search, CardInstance card)
    {
        var hosts = SearchAttachHosts(search.PlayerId, card);
        if (hosts.Length == 0)
        {
            Emit("searchFailed", $"{Definition(card).Name} cannot be attached to any Personality, so it stays where it was found.", search.PlayerId, card.Id);
            FinishSearch(search);
            return;
        }
        if (hosts.Length == 1) { AttachSearchedCard(search, card, hosts[0]); return; }
        search.Stage = SearchStage.ChooseAttachTarget;
        _state.PendingChoice = new("searchAttachTarget", search.PlayerId, search.SourceCardId, $"{Definition(Card(search.SourceCardId)).Name}: choose the Personality to attach {Definition(card).Name} to.");
    }

    private void ChooseSearchAttachTarget(PendingSearch search, GameCommand command)
    {
        Require(command.Type == SearchAttachCommand && command.TargetId is not null, "Choose a Personality to attach the card to.");
        var card = Card(search.SelectedCardId);
        var host = SearchAttachHosts(search.PlayerId, card).FirstOrDefault(h => h.Id == command.TargetId);
        Require(host is not null, "Choose one of the Personalities this card can be attached to.");
        AttachSearchedCard(search, card, host!);
    }

    private void AttachSearchedCard(PendingSearch search, CardInstance card, CardInstance host)
    {
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id);
        PlaceAttachment(card, host, player);
        search.EnteredCardId = card.Id; // the attachment's entering-play window opens as the search ends
        FinishSearch(search);
    }

    private IEnumerable<ActionOption> SearchAttachActions(PendingSearch search, int playerId)
    {
        var card = Card(search.SelectedCardId);
        foreach (var host in SearchAttachHosts(playerId, card))
            yield return new(SearchAttachCommand, $"Attach {Definition(card).Name} to {Definition(host).Name}", true, null, new GameCommand(SearchAttachCommand, playerId, _state.Version, TargetId: host.Id), host.Id);
    }

    // ---- a selection of several cards ---------------------------------------------------------------------------------------------------------------------------------------------------

    // The most cards that can be taken: every qualifying copy, or one per title when no two may share one.
    private static int SearchFeasibleCount(SearchDeckOption option, List<SearchGroup> groups)
    {
        var eligible = groups.Where(g => g.Eligible).ToList();
        return option.Criteria.DifferentTitles ? eligible.Select(g => Definition(g.Card).Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() : eligible.Sum(g => g.Count);
    }

    private string? SearchSelectionError(SearchSpec spec, SearchDeckOption option, List<SearchGroup> groups, string[] ids)
    {
        var count = spec.SelectCount!;
        foreach (var id in ids)
        {
            var group = groups.FirstOrDefault(g => g.DefinitionId == id);
            if (group is null) return "That card is not in the place being searched.";
            if (!group.Eligible) return group.Reason ?? "That card does not meet this search's criteria.";
            if (ids.Count(i => i == id) > group.Count) return $"Only {group.Count} of {Definition(group.Card).Name} {(group.Count == 1 ? "is" : "are")} there.";
        }
        if (option.Criteria.DifferentTitles && ids.Select(id => Definition(groups.First(g => g.DefinitionId == id).Card).Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
            return "Each card must have a different title.";
        var required = spec.Optional ? 0 : Math.Min(count.Min, SearchFeasibleCount(option, groups));
        if (ids.Length < required) return required == 1 ? "Take a card." : $"Take at least {required} cards.";
        if (ids.Length > count.Max) return count.Max == 1 ? "Take one card." : $"Take at most {count.Max} cards.";
        return null;
    }

    // The greedy legal selection: eligible entries in the viewer's order, up to Max, never twice the same title when titles must differ.
    private List<string> SearchDefaultSelection(SearchSpec spec, SearchDeckOption option, List<SearchGroup> groups)
    {
        var picks = new List<string>(); var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Where(g => g.Eligible))
            for (var i = 0; i < group.Count && picks.Count < spec.SelectCount!.Max; i++)
            {
                if (option.Criteria.DifferentTitles && !titles.Add(Definition(group.Card).Name)) break;
                picks.Add(group.DefinitionId);
            }
        return picks;
    }

    private void SelectManySearch(PendingSearch search, SearchSpec spec, SearchDeckOption option, List<SearchGroup> groups, GameCommand command)
    {
        Require(command.Type is SearchSelectManyCommand or "selectSearchCard", "Choose the cards to take from the list.");
        var ids = command.Type == "selectSearchCard" ? (command.Choice is null ? null : new[] { command.Choice }) : command.CardIds;
        Require(ids is { Length: > 0 }, "Choose the cards to take from the list, or pass.");
        var error = SearchSelectionError(spec, option, groups, ids!);
        Require(error is null, error ?? "");
        var deck = SearchDeckList(Player(search.PlayerId), search.Deck!).Select(Card).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var picked = new List<CardInstance>();
        foreach (var id in ids!) picked.Add(deck.First(c => c.DefinitionId == id && !picked.Contains(c)));
        search.PickedCardIds = picked.Select(c => c.Id).ToList(); search.SelectedCardId = picked[0].Id; search.ShownSequences = [];
        foreach (var card in picked) { ShowSearchedCard(search, card); search.ShownSequences.Add(_state.ShownCards.Count); }
        if (spec.OtherPlayerChooses is not null)
        {
            search.Stage = SearchStage.OtherPlayerChooses;
            _state.PendingChoice = null; // the search's own decision is over: the shown choice must not suspend it as an open window to resume
            var decider = 1 - search.PlayerId;
            OpenShownChoice(SearchOtherChoiceSpecId, Card(search.SourceCardId), search.PlayerId, decider, search.ShownSequences, 1, 1,
                $"{Definition(Card(search.SourceCardId)).Name}: choose one of the cards {Player(search.PlayerId).Name} found.");
            return; // ApplySearchOtherChoice finishes the search
        }
        foreach (var card in picked) ApplySimpleSearchEffect(search, card, option.Effect);
        FinishSearch(search);
    }

    // The other player's answer: the chosen card takes ChosenEffect, every other picked card the option's Effect, then the search ends as any search does.
    private bool ApplySearchOtherChoice(PendingShownChoice choice)
    {
        if (_state.PendingSearch is not { Stage: SearchStage.OtherPlayerChooses } search || search.PickedCardIds is null || search.ShownSequences is null) return true;
        var spec = SearchSpecOf(search); var option = SearchOptionOf(search);
        var chosenIndex = choice.Chosen.Count == 0 ? -1 : search.ShownSequences.IndexOf(choice.Chosen[0]);
        for (var i = 0; i < search.PickedCardIds.Count; i++)
            ApplySimpleSearchEffect(search, Card(search.PickedCardIds[i]), i == chosenIndex ? spec.OtherPlayerChooses!.ChosenEffect : option.Effect);
        FinishSearch(search);
        return true;
    }

    // ---- the searcher's legal actions and view ---------------------------------------------------------------------------------------------------------------------------------------

    // Select stage of a multi-card search: the default legal selection, and Pass when nothing qualifies (or taking none is allowed).
    private IEnumerable<ActionOption> SearchSelectManyActions(PendingSearch search, SearchSpec spec, List<SearchGroup> groups, int playerId)
    {
        var option = SearchOptionOf(search);
        var picks = SearchDefaultSelection(spec, option, groups);
        if (picks.Count > 0 && SearchSelectionError(spec, option, groups, picks.ToArray()) is null)
        {
            var names = string.Join(", ", picks.Select(id => Definition(groups.First(g => g.DefinitionId == id).Card).Name));
            yield return new(SearchSelectManyCommand, $"Take {names}", true, null, new GameCommand(SearchSelectManyCommand, playerId, _state.Version, CardIds: picks.ToArray()));
        }
        var anyEligible = groups.Any(g => g.Eligible);
        if (!anyEligible || spec.Optional || SearchMayTakeNone(spec))
            yield return new("passSearch", anyEligible ? "Take no card (the deck is still shuffled)" : "No card qualifies: pass (the deck is still shuffled)", true, null, new GameCommand("passSearch", playerId, _state.Version));
    }

    // The deck viewer's extra facts: how many to take, whether titles must differ, and the name of a new place. Null/absent for a classic search, so its view is unchanged.
    private static SearchView SearchExtView(SearchSpec spec, SearchDeckOption option, SearchView view) =>
        spec.SelectCount is null && SearchExtPlace(option.Deck) is null ? view
            : view with { MinSelect = spec.SelectCount?.Min, MaxSelect = spec.SelectCount?.Max, DifferentTitles = option.Criteria.DifferentTitles ? true : null, Place = SearchExtPlace(option.Deck) };

    // ---- the other player's choice, and the hooks ---------------------------------------------------------------------------------------------------------------------------------

    internal void BeginSearchForTest(string searchId, string sourceId, int playerId) => BeginSearch(SearchCatalog.All[searchId], Card(sourceId), playerId, "ability");
    internal void BeginLookForTest(string lookId, string sourceId, int playerId) => BeginLook(LookCatalog.All[lookId], Card(sourceId), playerId, "ability");

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterSearchExtensionHooks()
    {
        EngineHooks.ShownChoiceApply.Register("P20SearchOtherChoice", 22800, static (engine, choice) => choice.SpecId == SearchOtherChoiceSpecId && engine.ApplySearchOtherChoice(choice));
        RegisterSearchExtensionsShowcase();
    }
}
