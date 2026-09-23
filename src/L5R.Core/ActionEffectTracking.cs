namespace L5R.Core;

// Action-effect tracking (issue #437): what a resolved action did to specific cards, kept only long enough for a Reaction whose own trigger
// asks about it to check. Two pieces:
//   - Recording: DestroyUnits, DestroyAttachment, MoveHome and MoveByRule's own move-to-a-battlefield path append to MatchState.CurrentActionAffected
//     as they run (RecordActionEffect). Never for the mass casualty sweep a battle's own resolution performs directly (MatchEngine.cs's own
//     DestroyUnits(d)/DestroyUnits(a) calls, with no acting player) -- that is "the battle's own resolution," not "a Battle action," the same
//     distinction the printed cards below draw. DestroyUnits itself is only ever called with an acting player id from within an action's own
//     effects (an ability, a deferred Attack's hit, a duel's outcome), so gating recording on that parameter being present is exact, not a guess.
//   - Reading: FinishActionEffectTracking, called from the three points an action -- including its own reaction windows -- is truly done
//     (CompleteAbilityResolution, ResolveAttack, Dueling.cs's FinishDuelResolution), snapshots the accumulator into MatchState.LastResolvedAction
//     (replacing whatever was there before) and opens the after-action-resolved Reaction window below if anyone holds one that applies.
//
// What is NOT tracked: a bow. Unlike destroy/move, nothing bows a card through one shared choke point -- over a hundred call sites set
// CardInstance.Bowed directly today, none narrated individually. Reading every one of them (or building a shared Bow() helper and migrating all
// of them) is its own, much larger task; Hida Defenders (oracle-l5r-3083, "after your action bowed, destroyed, or moved an opposing Personality")
// stays blocked on that piece alone, now that the destroyed/moved half of its own condition is otherwise expressible.
public static class AfterActionReactionTriggers
{
    /// <summary>"After the resolution of [a/your] action that affected a card" (issue #437): the specific condition -- which cards, whose action, what
    /// happened to them -- is read from MatchState.LastResolvedAction by AfterActionResolvedApplies, keyed per ability id like AfterResolution.cs's
    /// own AfterResolutionApplies keys "after a battle's resolution ends."</summary>
    public const string AfterActionResolved = "after-action-resolved";
}

public sealed partial class MatchEngine
{
    public const string DestroyedKind = "destroyed";
    public const string MovedKind = "moved";

    private void RecordActionEffect(string cardId, string kind) => _state.CurrentActionAffected.Add(new(cardId, kind));

    // Snapshots what the just-finished action affected into LastResolvedAction, opens the Reaction window naming it if anyone holds one that
    // applies, and clears the accumulator either way. Called from the three points an action (its own reaction windows included) is truly done,
    // in the same chain as CompleteAbilityResolution's own OpenBowReactionWindow/OpenMoveReactionWindow/OpenOverconfidentDrawWindow (closesTo is
    // the same "who priority returns to once every window this action opens has closed" every one of those already threads through).
    private bool FinishActionEffectTracking(string abilityId, int playerId, string? sourceId, int closesTo)
    {
        var affected = _state.CurrentActionAffected;
        _state.LastResolvedAction = new ResolvedActionFacts { AbilityId = abilityId, PlayerId = playerId, SourceId = sourceId, Affected = affected };
        _state.CurrentActionAffected = [];
        // Revenge (issue #437): this action destroyed a Personality of some OTHER player's -- set that owner's flag.
        foreach (var a in affected)
        {
            var affectedCard = Card(a.CardId);
            if (a.Kind == DestroyedKind && Definition(affectedCard).Type == CardType.Personality && affectedCard.OwnerId != playerId)
                _state.EnemyDestroyedOwnPersonalitySinceLastBattleAction[affectedCard.OwnerId] = true;
        }
        // Revenge: the window resets once the acting player's OWN Battle action finishes resolving.
        if (AbilityCatalog.All[abilityId].Timing == "Battle") _state.EnemyDestroyedOwnPersonalitySinceLastBattleAction[playerId] = false;
        if (OpenAfterActionResolvedWindow(closesTo)) return true;
        // Moshi Ino's storm (issue #453, batch 3; MoshiInoStorm.cs): the same "this action is truly done" moment, asked once no Reaction window opened over it.
        return OpenInoStorm(closesTo);
    }

    private static bool IsAfterActionResolvedReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger == AfterActionReactionTriggers.AfterActionResolved;

    // Whether the last resolved action's own effects destroyed a Personality of `playerId`'s.
    private bool LastActionDestroyedOwnPersonality(int playerId) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId).OwnerId == playerId && Definition(Card(a.CardId)).Type == CardType.Personality);

    // As above, narrowed to a Personality carrying `keyword` -- the shape Measure of Devotion ("one of your Deathseekers") and a future card of
    // this same family would read.
    private bool LastActionDestroyedOwnKeyword(int playerId, string keyword) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId).OwnerId == playerId && Keyword(Card(a.CardId), keyword));

    // Each Reaction's own condition once the window is open (the trigger check in ValidateAbility calls this).
    private bool AfterActionResolvedApplies(CardInstance source, string abilityId, int playerId)
    {
        if (_state.LastResolvedAction is not { } facts || source.OwnerId != playerId) return false;
        return abilityId switch
        {
            // Measure of Devotion (oracle-l5r-5013): "After the resolution of a Battle action that destroyed one of your Deathseekers."
            "measure-of-devotion-create" => AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && LastActionDestroyedOwnKeyword(playerId, "Deathseeker"),
            // Akodo Kobi - exp (oracle-l5r-232): "After another player's Battle action resolves which destroyed one of your Lion Clan Personalities."
            "kobi-recruit" => facts.PlayerId != playerId && AbilityCatalog.All[facts.AbilityId].Timing == "Battle" && LastActionDestroyedOwnKeyword(playerId, "Lion Clan"),
            // Matsu Rika (oracle-l5r-9827): "After your Bushido Virtue action resolves" -- her own action (facts.PlayerId == playerId, already implied by
            // source.OwnerId == playerId above only gating who HOLDS the Reaction, not whose action it was), classified through ActionCarriesKeyword the
            // same way Ebizo/Kobi's Recon/Tactical Traits already read a card's own keyword when the acting ability prints no explicit designator.
            "rika-bushido-virtue-bonus" => facts.PlayerId == playerId
                && ActionCarriesKeyword(AbilityCatalog.All[facts.AbilityId], facts.SourceId is null ? null : Card(facts.SourceId), "Bushido Virtue"),
            // Synthetic fixture (issue #437): "after the resolution of an action that destroyed one of your Personalities, gain 3 Honor" -- no
            // Timing/keyword narrowing, exercising AfterActionResolvedApplies/the window itself directly rather than through a real card's own
            // extra conditions.
            "training-after-action-resolved-gain" => LastActionDestroyedOwnPersonality(playerId),
            // Asahina Akikusa (oracle-l5r-10046, issue #453 batch 3): "After an action on your Air Spell resolves" -- the resolved action's source card is a Spell with the
            // Air keyword attached to a Personality her controller controls (the Spell's action is her controller's own, so facts.PlayerId matches too).
            "akikusa-honor" => facts.SourceId is not null && _state.Cards.TryGetValue(facts.SourceId, out var akikusaSpell) && Definition(akikusaSpell).Type == CardType.Spell
                && Keyword(akikusaSpell, "Air") && akikusaSpell.OwnerId == playerId && facts.PlayerId == playerId,
            _ => false
        };
    }

    // The Reaction plays this player could make right now, asked with the window open exactly as they will be (AfterResolutionOptions' own shape).
    private ActionOption[] AfterActionResolvedOptions(int playerId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("afterActionResolvedReaction", playerId, null, "After that action's resolution: use a Reaction, or pass.");
        try { return AbilityActions(playerId).Where(o => o.Enabled).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    // Opens the window when a player has something to play. False (and nothing changes) when neither does.
    private bool OpenAfterActionResolvedWindow(int closesTo)
    {
        bool Holds(int player) => AfterActionResolvedOptions(player).Length > 0;
        if (!Holds(0) && !Holds(1)) return false;
        var first = FirstReactionOpportunity(Holds);
        OpenAlternatingReactionWindow(new("afterActionResolvedReaction", first, null, "After that action's resolution: you may use a Reaction, or pass.", closesTo), first, "After-action-resolved window opened.");
        return true;
    }
}
