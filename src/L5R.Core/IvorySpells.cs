namespace L5R.Core;

// ======================================================================================================================================================================================
// IVORY-ERA SPELLS, A STRATEGY AND AN EVENT (issue #622, batch "spells"): Deafening Thunder (oracle-l5r-10125), Seeking the Way (6567), Servitors of Stone (6624), Walking the Way (9196), Words of
// Consecration (9378), Sanctioned Duel (6465) and Wisdom Gained (9356). Each is read against its scan, not Oracle's later wording.
//
//   * DEAFENING THUNDER: "Battle: Bow this card and target an enemy Personality or Follower: Give it a Force penalty equal to this Shugenja's Chi. If its Force is now 0 or this Shugenja is Thunder, you
//     may destroy this card; if you did, take an additional Battle action." The Chi is the Shugenja's current Chi. The "may destroy" is an Either/Or of the acting player (destroy / keep); only the
//     destroying earns the additional Battle action (Extend the Soul's Boundaries' grant shape).
//   * SEEKING THE WAY: "Battle: Bow this Shugenja, move him home, and target an attacking Personality: Move him home. Gain 2 Honor." Both the bow and the Shugenja's own move home are COSTS (Wall of
//     Honor's move-home cost); the target's move home and the Honor are effects, so the Honor is gained even when the movement is negated.
//   * SERVITORS OF STONE: "Battle: Bow this card and target a card in this Shugenja's army: Straighten it." A card in an army is a Personality at the Shugenja's battlefield on his side, or any card
//     attached to one (a unit is a Personality and everything attached to him; core rules, Units). The Shugenja himself is at the battle (the generic Spell presence rule).
//   * WALKING THE WAY: "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for a card. Put it in your hand." Introspection's search (nothing says to show it; a search's own record
//     shows it, and a searched deck is always shuffled -- Mendon's R4 ruling).
//   * WORDS OF CONSECRATION: "After this card enters play, if this Shugenja is Air: Choose a player, who gains or loses 1 Honor." (a trait modelled as a Reaction, like the Celestial Swords' entering
//     traits: TraitReactions) and "Open: Bow this Shugenja and target another player's Personality: After the next time (this turn) he assigns to attack you, gain 1 Honor, or 2 Honor if you control a
//     Temple." "He assigns" is read of the targeted Personality being assigned to attack (The Menacing Sky's reading of the same verb); the Temple is judged when the Honor is gained.
//   * SANCTIONED DUEL: "Iaijutsu. Limited: Choose your performing unbowed Personality and target another player's Personality: Your Personality challenges the target. He may refuse; if he does, dishonor
//     him and gain 2 Honor. Destroy the duel's loser." Watchful Eye Dojo's refusable challenge, with its refusal and the loser's destruction in Dueling.cs.
//   * WISDOM GAINED (an Event): "Starting with you, each player may search his discard pile and Fate deck for a Ring, show it, and put it in his hand. Each player who searched his deck shuffles it."
//     Every Ring search is an ordinary ordered search (R-011/R-012): the discard pile first, the deck only when no Ring is taken from it; "may" makes each search optional. The Event stays in its
//     province until both players have searched, then resolves like Imperial Gift (WisdomGainedState carries the second searcher).
// ======================================================================================================================================================================================

public static class IvorySpellAbilities
{
    public const string DeafeningThunderWeaken = "deafening-thunder-weaken";
    public const string SeekingTheWayMove = "seeking-the-way-move";
    public const string ServitorsOfStoneStraighten = "servitors-of-stone-straighten";
    public const string WalkingTheWaySearch = "walking-the-way-search";
    public const string WordsOfConsecrationHonor = "words-of-consecration-honor";
    public const string WordsOfConsecrationWatch = "words-of-consecration-watch";
    public const string SanctionedDuel = "sanctioned-duel";
    public const string WisdomGainedEffect = "wisdom-gained";

    /// <summary>The Target category of Servitors of Stone: a card in the army of the Shugenja the Spell is attached to.</summary>
    public const string WielderArmyCard = "ivory-wielder-army-card";
    /// <summary>The Either/Or of Deafening Thunder's "you may destroy this card".</summary>
    public const string DeafeningThunderDestroySpec = "deafening-thunder-destroy";
    /// <summary>Words of Consecration's assignment watch (AssignmentWatches' Kind).</summary>
    public const string WordsOfConsecrationWatchKind = "words-of-consecration";
    /// <summary>The PendingSearch.Origin of a Wisdom Gained search.</summary>
    public const string WisdomGainedOrigin = "wisdom-gained";

    public static Dictionary<string, AbilityDefinition> AddIvorySpells(this Dictionary<string, AbilityDefinition> all)
    {
        all[DeafeningThunderWeaken] = new("Battle", "Bow this card and target an enemy Personality or Follower: Give it a Force penalty equal to this Shugenja's Chi. If its Force is now 0 or this Shugenja is Thunder, you may destroy this card; if you did, take an additional Battle action",
            "enemy-force", Costs: ActionCost.BowSource);
        all[SeekingTheWayMove] = new("Battle", "Bow this Shugenja, move him home, and target an attacking Personality: Move him home. Gain 2 Honor", "attacking-personality",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.MoveHome, CostSubject.Wielder)]);
        all[ServitorsOfStoneStraighten] = new("Battle", "Bow this card and target a card in this Shugenja's army: Straighten it", WielderArmyCard, Costs: ActionCost.BowSource);
        all[WalkingTheWaySearch] = new("Limited", "Bow this Shugenja and destroy this card: Search your Fate deck for a card. Put it in your hand", "none",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)], Search: SearchCatalog.Introspection);
        all[WordsOfConsecrationHonor] = new("Reaction", "After this card enters play, if this Shugenja is Air: Choose a player, who gains or loses 1 Honor", "none", Trigger: "own-enters-play",
            Branch: new(BranchCondition.WielderHas("Air"), HonorChoice.Options(1), HonorChoice.None));
        all[WordsOfConsecrationWatch] = new("Open", "Bow this Shugenja and target another player's Personality: After the next time (this turn) he assigns to attack you, gain 1 Honor, or 2 Honor if you control a Temple",
            "enemy-personality", Costs: ActionCost.BowWielder);
        all[SanctionedDuel] = new("Limited", "Choose your performing unbowed Personality and target another player's Personality: Your Personality challenges the target. He may refuse; if he does, dishonor him and gain 2 Honor. Destroy the duel's loser",
            "enemy-personality", Performer: "own-unbowed-personality", IssuesDuel: true, DuelRefusable: true, ActionKeywords: ["Iaijutsu"]);
        return all;
    }
}

public static class IvorySpellSearches
{
    private static readonly SearchCriteria RingCard = new("Ring", [CardType.Ring]);

    /// <summary>Wisdom Gained (oracle-l5r-9356): "each player may search his discard pile and Fate deck for a Ring, show it, and put it in his hand."</summary>
    public static SearchSpec WisdomGained { get; } = new("wisdom-gained", "You may search your discard pile and Fate deck for a Ring",
    [
        new(SearchDeck.FateDiscard, RingCard, SearchEffect.ToHand),
        new(SearchDeck.Fate, RingCard, SearchEffect.ToHand)
    ], Optional: true, Ordered: true);

    public static IEnumerable<SearchSpec> All => [WisdomGained];
}

/// <summary>A Wisdom Gained in the middle of its searches: the Event, the player whose Events Phase it is, and who still has to search (in turn order, after him).</summary>
public sealed class WisdomGainedState
{
    public string SourceCardId { get; set; } = "";
    public int OwnerId { get; set; }
    public List<int> Remaining { get; set; } = [];
}

public sealed partial class MatchState
{
    // Feature state, not a decision of its own: the open decision is the search (PendingSearch/PendingChoice), which the Seppuku-timing classification already covers.
    [System.Text.Json.Serialization.JsonPropertyOrder(1260)]
    public WisdomGainedState? WisdomGained { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateIvorySpells(string id, GameCommand command, CardInstance? source)
    {
        switch (id)
        {
            // Seeking the Way: the Shugenja's own move home is a cost, so it must be a move he may make.
            case IvorySpellAbilities.SeekingTheWayMove when source is not null && WielderOf(source) is { } mover:
                Require(!MovementLocked(mover, MovementKinds.Home), $"{Definition(mover).Name} may not move this turn.");
                break;
            // Words of Consecration: "if this Shugenja is Air".
            case IvorySpellAbilities.WordsOfConsecrationHonor:
                Require(source is not null && WielderOf(source) is { } wielder && Keyword(wielder, "Air"), "This Shugenja must be Air.");
                break;
        }
    }

    // ---- Target categories -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // Servitors of Stone: "a card in this Shugenja's army" -- a Personality of the Shugenja's side at his battlefield, or a card attached to one.
    private bool IvoryTargetAllows(string category, CardInstance? source, CardInstance target)
    {
        if (category != IvorySpellAbilities.WielderArmyCard) return false;
        if (source is not { Zone: Zone.Attached } || WielderOf(source) is not { Zone: Zone.Battlefield, ProvinceIndex: int battlefield } shugenja) return false;
        var leader = Definition(target).Type == CardType.Personality ? target : target.Zone == Zone.Attached && target.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : null;
        return leader is { Zone: Zone.Battlefield } && Definition(leader).Type == CardType.Personality && leader.OwnerId == shugenja.OwnerId && leader.ProvinceIndex == battlefield;
    }

    // ---- execution -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunIvorySpells(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case IvorySpellAbilities.DeafeningThunderWeaken:
                {
                    var shugenja = WielderOf(source!)!;
                    ApplyModifier(target!, source!.Id, id, "Force", -PersonalityChi(shugenja));
                    if (!InPlay(target!) || !(CardForce(target!) == 0 || Keyword(shugenja, "Thunder")) || source.Zone != Zone.Attached) return false;
                    // "You may destroy this card; if you did, take an additional Battle action": the acting player decides (Extend the Soul's Boundaries' shape for the grant).
                    if (!consumedGrant && ForfeitsAdditionalActions(ability)) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                    var stay = _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g));
                    return OpenEitherOr(IvorySpellAbilities.DeafeningThunderDestroySpec, source, player.Id, player.Id, target, "action", bowedBeforeAction, stay);
                }
            case IvorySpellAbilities.SeekingTheWayMove:
                MoveHome(target!, false);
                GainHonor(player.Id, 2, Definition(source!).Name, source);
                return false;
            case IvorySpellAbilities.ServitorsOfStoneStraighten:
                Straighten(target!);
                return false;
            case IvorySpellAbilities.WalkingTheWaySearch:
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case IvorySpellAbilities.WordsOfConsecrationWatch:
                if (target is null || !InPlay(target)) return false;
                _state.AssignmentWatches.Add(new(target.Id, IvorySpellAbilities.WordsOfConsecrationWatchKind, player.Id, _state.TurnNumber, Definition(source!).Name));
                Emit("watchSet", $"{Definition(source!).Name}: after the next time this turn {Definition(target).Name} is assigned to attack {player.Name}, {player.Name} gains Honor.", player.Id, target.Id);
                return false;
            default:
                return null;
        }
    }

    // AssignmentWatch's hook (ResolveAssignmentWatch): the watched Personality was assigned to attack, so its watcher gains 1 Honor, or 2 if he controls a Temple.
    private void ResolveWordsOfConsecrationWatch(AssignmentWatch watch, CardInstance card)
    {
        var amount = ControlsKeyword(watch.BeneficiaryPlayerId, "Temple") ? 2 : 1;
        Emit("watchFired", $"{watch.Source}: {Definition(card).Name} was assigned to attack.", watch.BeneficiaryPlayerId, card.Id);
        GainHonor(watch.BeneficiaryPlayerId, amount, watch.Source);
    }

    // ---- Deafening Thunder's Either/Or -------------------------------------------------------------------------------------------------------------------------------------------------

    private string? IvoryEitherOrPrompt(PendingEitherOr p, string source) =>
        p.SpecId == IvorySpellAbilities.DeafeningThunderDestroySpec ? $"{source}: destroy it to take an additional Battle action?" : null;

    private EitherOrOption[]? IvoryEitherOrOptions(PendingEitherOr p)
    {
        if (p.SpecId != IvorySpellAbilities.DeafeningThunderDestroySpec) return null;
        var spell = Card(p.SourceCardId);
        return [
            new("destroy", $"Destroy {Definition(spell).Name}", "It is destroyed, and you take an additional Battle action.", EitherOrKind.Choose, 0, spell.Zone == Zone.Attached, $"{Definition(spell).Name} is no longer in play."),
            new("keep", "Do not destroy it", "Nothing further happens.", EitherOrKind.Choose, 0, true, null)
        ];
    }

    private bool ApplyIvoryEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != IvorySpellAbilities.DeafeningThunderDestroySpec) return false;
        if (option.Key == "destroy" && source.Zone == Zone.Attached)
        {
            Emit("eitherOrChosen", $"{decider.Name} destroyed {Definition(source).Name}.", decider.Id, source.Id);
            DestroyAttachment(source);
            GrantAdditionalAction(p.ActingPlayerId, null);
            p.StayWithActor = _state.AdditionalActions.Any(g => g.PlayerId == p.ActingPlayerId && Forfeitable(g));
        }
        else Emit("eitherOrChosen", $"{decider.Name} did not destroy {Definition(source).Name}.", decider.Id, source.Id);
        return true;
    }

    // ---- Wisdom Gained -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ResolveEvent's hook. "Starting with you": the Event's controller searches first, then the other player. The Event waits in its province, like Imperial Gift, until both searches are over.
    private bool ResolveWisdomGained(PlayerState player, CardInstance card)
    {
        _state.WisdomGained = new() { SourceCardId = card.Id, OwnerId = player.Id, Remaining = [1 - player.Id] };
        BeginSearch(IvorySpellSearches.WisdomGained, card, player.Id, IvorySpellAbilities.WisdomGainedOrigin);
        return true;
    }

    // FinishSearch's hook, after a Wisdom Gained search has shuffled: the other player searches, or the Event is done. True when the search that ended was one of this card's.
    private bool ContinueWisdomGained(PendingSearch search)
    {
        if (search.Origin != IvorySpellAbilities.WisdomGainedOrigin || _state.WisdomGained is not { } chain) return false;
        if (chain.Remaining.Count > 0)
        {
            var next = chain.Remaining[0]; chain.Remaining.RemoveAt(0);
            BeginSearch(IvorySpellSearches.WisdomGained, Card(chain.SourceCardId), next, IvorySpellAbilities.WisdomGainedOrigin);
            return true;
        }
        _state.WisdomGained = null;
        FinishDeferredAction(chain.OwnerId, "event", chain.SourceCardId, null);
        return true;
    }

    // ---- Sanctioned Duel -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "He may refuse; if he does, dishonor him and gain 2 Honor": the refuser is dishonored and the challenger's player gains the Honor (Watchful Eye Dojo's, with 2).
    private void SanctionedDuelRefusal(PendingDuel duel)
    {
        Dishonor(Card(duel.ChallengedId), duel.ChallengerPlayerId);
        GainHonor(duel.ChallengerPlayerId, 2, "Sanctioned Duel");
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterIvorySpellHooks()
    {
        EngineHooks.Abilities.Register("AddIvorySpells", 11300, static abilities => abilities.AddIvorySpells());
        EngineHooks.Searches.Register("IvorySpellSearches", 11300, static () => IvorySpellSearches.All);
        EngineHooks.ValidateLate.Register("ValidateIvorySpells", 11300, static (engine, context) => engine.ValidateIvorySpells(context.id, context.command, context.source));
        EngineHooks.TargetAllows.Register("IvoryTargetAllows", 11300, static (engine, context) => engine.IvoryTargetAllows(context.ability.Target, context.source, context.target));
        EngineHooks.RunEffects.Register("RunIvorySpells", 11300, static (engine, context) => engine.RunIvorySpells(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.EitherOrPrompt.Register("IvoryEitherOrPrompt", 11300, static (engine, p, source, affected) => engine.IvoryEitherOrPrompt(p, source));
        EngineHooks.EitherOrOptions.Register("IvoryEitherOrOptions", 11300, static (engine, p, affected, source, payable) => engine.IvoryEitherOrOptions(p));
        EngineHooks.ApplyEitherOr.Register("ApplyIvoryEitherOr", 11300, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyIvoryEitherOr(p, option, source, decider));
    }
}
