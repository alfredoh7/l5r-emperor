namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #674, CLUSTER "ITEMS-MISC" (event-wave-32): A Fever in the Blood, Changing the Game, Purge of Fudoism, Riot in the Second City and The Second Battle of Beiden Pass, wired from the
// printed text of their scans. An Event resolves as its province is revealed in its owner's Events Phase; the printed "Limited:" is ignored (the Gates of Chaos supplement: every Event of
// Gates of Chaos, Coils of Madness and, by extension, Aftermath resolves as a plain triggered trait).
//
//   A FEVER IN THE BLOOD    "Put this Event into play. Discard it after your next turn begins. While it is in play, Personalities and Followers have -1F while attacking" (the supplement's own wording
//                           of its Most Recent Printing): it stays in play until its owner's next turn begins (Family Maneuvers' duration), and every Personality or Follower on an attacking unit
//                           (a unit assigned to or moved to a battlefield in the turn of its controller, who is the Attacker) has -1F, whoever controls it.
//   CHANGING THE GAME       an ordered search (R-011) of the owner's hand, then Fate discard pile, then Fate deck for a card titled Blood of the Preserver: it is shown, removed from the game, and
//                           its searcher gains 2 Honor (the search effect WlItemsMiscEvents.RemoveForHonor; the Honor is gained only for a card that was found).
//   PURGE OF FUDOISM        "Starting with you, each player targets (if possible) and destroys a card with the printed Fallen, Fudo, or Shadowlands keyword": the Event's owner chooses first and the
//                           card is destroyed, then the other player chooses among the cards still in play. A card is a Personality, Follower, Item, Spell or Holding of either player's in play.
//   RIOT IN THE SECOND    "Each player targets and bows an unbowed Samurai": the owner first, then the other player; the target is any unbowed Samurai in play (either player's, the printed text
//   CITY                    names no owner), and a player with none owes nothing.
//   BEIDEN PASS             "Until the game ends, this Province does not hold Dynasty cards and has +10 Province Strength": the province the Event was revealed in permanently holds no Dynasty cards
//                           (Illusory Defense's ProvinceState.HoldsNoDynastyCards) and has 10 more strength; the Event stays in play as the reminder of an effect that lasts to the end of the game.
// ======================================================================================================================================================================================

public static class WlItemsMiscEventAbilities
{
    public const string FeverEffect = "wl-fever-in-the-blood", ChangingTheGameEffect = "wl-changing-the-game", PurgeEffect = "wl-purge-of-fudoism", RiotEffect = "wl-riot-second-city", BeidenPassEffect = "wl-beiden-pass";
    /// <summary>Changing the Game's search effect on the found card: shown, removed from the game, and 2 Honor for the searcher.</summary>
    public const string RemoveForHonor = "wl-remove-for-honor";
    /// <summary>The kinds of the Events' decisions (PendingEventChoice.Kind).</summary>
    public const string PurgeChoice = "wl-purge-choice", RiotChoice = "wl-riot-choice";
    /// <summary>The Event card of The Preserver search: the title the search names.</summary>
    public const string BloodOfThePreserver = "Blood of the Preserver";
}

public static class WlItemsMiscSearches
{
    public const string ChangingTheGameId = "wl-changing-the-game";

    private static readonly SearchCriteria PreserverCard = new("card titled Blood of the Preserver", Titles: [WlItemsMiscEventAbilities.BloodOfThePreserver]);

    /// <summary>Changing the Game: "Search your hand, then your discard pile, and then your Fate deck for a card titled Blood of the Preserver. Show it. Remove it from the game to gain 2 Honor."</summary>
    public static SearchSpec ChangingTheGame { get; } = new(ChangingTheGameId, "Search your hand, then your discard pile, then your Fate deck for a card titled Blood of the Preserver",
        [new(SearchDeck.Hand, PreserverCard, WlItemsMiscEventAbilities.RemoveForHonor), new(SearchDeck.FateDiscard, PreserverCard, WlItemsMiscEventAbilities.RemoveForHonor),
            new(SearchDeck.Fate, PreserverCard, WlItemsMiscEventAbilities.RemoveForHonor)], Ordered: true);
}

public sealed partial class MatchEngine
{
    // ---- the reveal ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ResolveEvent's registered hook: null when the Event is not this cluster's; false when it is resolved; true when the reveal paused for a decision or a search.
    private bool? ResolveWlItemsMiscEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case WlItemsMiscEventAbilities.FeverEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: Personalities and Followers have -1F while attacking.", turnBeginsIn: 1);
                return false;
            case WlItemsMiscEventAbilities.ChangingTheGameEffect:
                BeginSearch(WlItemsMiscSearches.ChangingTheGame, card, player.Id, "event");
                return true;
            case WlItemsMiscEventAbilities.PurgeEffect:
                return WlBeginEventStep(player, province, card, WlItemsMiscEventAbilities.PurgeChoice, 0);
            case WlItemsMiscEventAbilities.RiotEffect:
                return WlBeginEventStep(player, province, card, WlItemsMiscEventAbilities.RiotChoice, 0);
            case WlItemsMiscEventAbilities.BeidenPassEffect:
                // The province is flagged first, so the refill KeepEventInPlay does after the Event leaves it finds a province that holds no Dynasty cards.
                province.HoldsNoDynastyCards = true;
                province.Strength += 10;
                Emit("provinceHoldsNoDynastyCards", $"{Definition(card).Name}: {player.Name}'s province {province.Index + 1} does not hold Dynasty cards and has +10 Province Strength until the game ends.", player.Id, card.Id);
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until the game ends: province {province.Index + 1} holds no Dynasty cards and has +10 Province Strength.");
                return false;
            default:
                return null;
        }
    }

    // ---- A Fever in the Blood ---------------------------------------------------------------------------------------------------------------------------------------------------

    private int WlItemsMiscFevers => _state.Cards.Values.Count(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == WlItemsMiscEventAbilities.FeverEffect);

    // PersonalityForce's hook: -1F for each Fever in play while the Personality is attacking (a unit at a battlefield in the turn of the player who owns it).
    private int WlItemsMiscPersonalityForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || !IsAttacking(card)) return 0;
        return -WlItemsMiscFevers;
    }

    // FollowerForce's term: the same for a Follower of an attacking Personality.
    private int WlItemsMiscFollowerForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Follower || card.Zone != Zone.Attached || card.AttachedTo is not { } hostId || !_state.Cards.TryGetValue(hostId, out var host) || !IsAttacking(host)) return 0;
        return -WlItemsMiscFevers;
    }

    // ---- Changing the Game: the search effect ---------------------------------------------------------------------------------------------------------------------------------------

    // DeckSearch's default arm: the found card is removed from the game and the searcher gains 2 Honor. True when it was this cluster's effect (and the search is finished).
    private bool WlItemsMiscApplySearchEffect(PendingSearch search, CardInstance card, string effect)
    {
        if (effect != WlItemsMiscEventAbilities.RemoveForHonor) return false;
        var player = Player(search.PlayerId);
        SearchDeckList(player, search.Deck!).Remove(card.Id); // a deck's own list; a pile's or the hand's is a copy
        player.Hand.Remove(card.Id);
        RemoveFromGame(card);
        var source = Card(search.SourceCardId);
        GainHonor(player.Id, 2, Definition(source).Name, source);
        FinishSearch(search);
        return true;
    }

    // ---- Purge of Fudoism and Riot in the Second City: each player in turn -------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> WlEventCandidates(string kind) => kind switch
    {
        WlItemsMiscEventAbilities.RiotChoice => _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && InPlay(c) && !c.Bowed && Keyword(c, "Samurai")),
        _ => _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell or CardType.Holding
            && (Definition(c).Keywords ?? []).Any(k => k is "Fallen" or "Fudo" or "Shadowlands"))
    };

    // The owner chooses first (step 0), then the other player (step 1). A player with nothing to choose among owes nothing and the next step begins. Returns true when a decision was opened;
    // with none left to ask the Event is over: discarded when this is its reveal (false: the reveal carries on), finished by the caller otherwise.
    private bool WlBeginEventStep(PlayerState owner, ProvinceState? province, CardInstance card, string kind, int step)
    {
        for (; step <= 1; step++)
        {
            var chooser = step == 0 ? owner : Player(1 - owner.Id);
            var candidates = WlEventCandidates(kind).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
            if (candidates.Count == 0)
            {
                Emit("eventResolved", $"{Definition(card).Name}: {chooser.Name} has nothing to choose.", chooser.Id, card.Id);
                continue;
            }
            var prompt = kind == WlItemsMiscEventAbilities.RiotChoice ? $"{Definition(card).Name}: choose an unbowed Samurai. It is bowed."
                : $"{Definition(card).Name}: choose a card with the printed Fallen, Fudo or Shadowlands keyword. It is destroyed.";
            OpenEventChoice(new PendingEventChoice { Kind = kind, EventCardId = card.Id, PlayerId = chooser.Id, Origin = "reveal", Candidates = candidates, Step = step, Prompt = prompt });
            return true;
        }
        if (province is not null) return DiscardResolvedEvent(owner, province, card);
        FinishRevealedEvent(card);
        return true;
    }

    // EventChoice's default branch: the answer. False when it is not this cluster's decision.
    private bool WlItemsMiscEventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind is not (WlItemsMiscEventAbilities.PurgeChoice or WlItemsMiscEventAbilities.RiotChoice)) return false;
        Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered cards.");
        var card = Card(pending.EventCardId); var chosen = Card(command.TargetId!); var owner = Player(card.OwnerId);
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        Emit("eventChose", $"{Definition(card).Name}: {Player(pending.PlayerId).Name} chooses {Definition(chosen).Name}.", pending.PlayerId, chosen.Id);
        if (pending.Kind == WlItemsMiscEventAbilities.RiotChoice)
        {
            if (!StalwartNegatesBow(chosen, owner.Id)) chosen.Bowed = true;
        }
        else DestroyTargetCard(chosen, owner.Id);
        // The next player's turn to choose (a player with nothing to choose among finishes the Event), or the end of the Event: its province refills and the Events Phase carries on.
        if (pending.Step == 0) WlBeginEventStep(owner, null, card, pending.Kind, 1);
        else FinishRevealedEvent(card);
        return true;
    }

    private IEnumerable<ActionOption> WlItemsMiscEventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind is not (WlItemsMiscEventAbilities.PurgeChoice or WlItemsMiscEventAbilities.RiotChoice)) yield break;
        var eventName = Definition(Card(pending.EventCardId)).Name;
        foreach (var id in pending.Candidates)
            yield return new("eventChoice", $"{eventName}: {Definition(Card(id)).Name}", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: id), id);
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlItemsMiscEventHooks()
    {
        EngineHooks.Searches.Register("WlItemsMiscSearches", 15120, static () => [WlItemsMiscSearches.ChangingTheGame]);
        EngineHooks.ResolveEvent.Register("ResolveWlItemsMiscEvent", 15120, static (engine, context) => engine.ResolveWlItemsMiscEvent(context.Player, context.Province, context.Card));
        EngineHooks.PersonalityForce.Register("WlItemsMiscPersonalityForce", 15120, static (engine, context) => engine.WlItemsMiscPersonalityForce(context.card));
    }
}
