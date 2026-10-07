namespace L5R.Core;

// Emperor Edition remaining-cards follow-up (issue #492): Mendon's rulings, group R5. Engine additions for the rulings that need new code (the rulings that only change data, such as the
// Ninja/Clan fix, live in the packagers and published content).
//
// Ruling 3 -- substitutes in a duel (He's Mine!, Bayushi Irezu): "The substitute may only take the place of a participant in an accepted (or mandatory) duel. He's Mine! cannot be used to move
// refusal effects to a new target." Both cards are "before the duel begins" Reactions, played in the window a duel opens once it is accepted (or at once when it cannot be refused), and they swap
// one of the duel's two participants (the controller's own) for the substitute. A challenge still awaiting its answer has no such window, and once refused there is no duel.
public sealed partial class MatchEngine
{
    // The window is open: an accepted or mandatory duel, not yet focusing.
    private bool DuelSubstitutionWindowOpen() =>
        _state.PendingDuel is { Stage: "Reaction" } && _state.PendingChoice?.Type == "duelReaction";

    /// <summary>The player's own participant in the duel whose "before the duel begins" window is open (a duel is between two Personalities of different players, so there is at most one).</summary>
    private CardInstance? OwnDuelParticipant(int playerId)
    {
        // Also read while the window is being decided on (Stage "Reaction" is set before the window itself opens).
        if (_state.PendingDuel is not { Stage: "Reaction" } duel) return null;
        var id = duel.ChallengerPlayerId == playerId ? duel.ChallengerId : duel.ChallengedPlayerId == playerId ? duel.ChallengedId : null;
        return id is not null && _state.Cards.TryGetValue(id, out var card) && InPlay(card) && Definition(card).Type == CardType.Personality ? card : null;
    }

    internal bool HasClanAlignmentForTest(string cardId, string clan) => HasClanAffiliation(Card(cardId), clan);

    // He's Mine!: "your performing unbowed Samurai at the same location" as the participant he replaces.
    private bool HesMineDuelPerformerAllows(CardInstance performer) =>
        OwnDuelParticipant(performer.OwnerId) is { } participant && Definition(performer).Type == CardType.Personality && InPlay(performer) && !performer.Bowed
        && Keyword(performer, "Samurai") && performer.Id != participant.Id && SameLocation(performer, participant);

    private IEnumerable<CardInstance> HesMineDuelPerformers(int playerId) =>
        OwnDuelParticipant(playerId) is null ? [] : _state.Cards.Values.Where(c => c.OwnerId == playerId && HesMineDuelPerformerAllows(c)).OrderBy(c => c.Id, StringComparer.Ordinal);

    private IEnumerable<(GameCommand Command, string Label)> HesMineDuelOptions(CardInstance card, int playerId)
    {
        if (OwnDuelParticipant(playerId) is not { } participant) yield break;
        foreach (var samurai in HesMineDuelPerformers(playerId))
            yield return (new GameCommand("useAbility", playerId, _state.Version, CardId: card.Id, TargetId: participant.Id, AbilityId: "hes-mine-substitute", PerformerId: samurai.Id),
                $"{AbilityCatalog.All["hes-mine-substitute"].Label} · {Definition(samurai).Name} ({samurai.Id})");
    }

    // Irezu: "your Scorpion Clan Personality" is the controller's own participant, who is not Irezu, at Irezu's location; Irezu himself is bowed as the cost (the shared bowed-source rule rejects a
    // bowed Irezu).
    private bool IrezuSubstitutes(CardInstance irezu) =>
        OwnDuelParticipant(irezu.OwnerId) is { } participant && participant.Id != irezu.Id && Keyword(participant, "Scorpion Clan") && SameLocation(irezu, participant);

    // Whether a "before the duel begins" Reaction of a card could do anything now; the ones that substitute a participant are counted only when they could, so no window opens for nothing.
    private bool DuelReactionRelevant(CardInstance card, string abilityId) => abilityId switch
    {
        "hes-mine-substitute" => HesMineDuelPerformers(card.OwnerId).Any(),
        "irezu-duel-enter" => !card.Bowed && IrezuSubstitutes(card),
        _ => true
    };

    // The actions whose only effect on their target is to bow it and whose target category could name a Terrain (an "enemy card", any enemy card at the battlefield): a Terrain may not be
    // bowed, so it is not a legal target for them (issue #492, ruling R5). The categories that already name only Personalities, Followers or attachments need no entry.
    private static readonly HashSet<string> BowOnlyActionsOnAnyEnemyCard = ["tsuji-bow", "water-bo-bow", "incite-phantoms-bow", "murota-bow", "training-bow-enemy-card-at-battle", "training-bow-enemy-card-open"];

    private void ValidateEmperorRulingR5(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        ValidateAttackCompareChoice(id, command);
        if (BowOnlyActionsOnAnyEnemyCard.Contains(id) && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var bowTarget))
            Require(!IsTerrainCard(bowTarget), "A Terrain may not be bowed or straightened.");
        switch (id)
        {
            case "irezu-duel-enter":
                Require(DuelSubstitutionWindowOpen(), "Irezu may enter only a duel that has been accepted or cannot be refused, before it begins.");
                Require(IrezuSubstitutes(source!), "Irezu needs your Scorpion Clan Personality, at his location, to be in the duel.");
                break;
            case "hes-mine-substitute":
                Require(DuelSubstitutionWindowOpen(), "He's Mine! may be played only in a duel that has been accepted or cannot be refused, before it begins.");
                break;
        }
    }

    /// <summary>The substitute takes the replaced participant's place in the pending duel (before it begins).</summary>
    private void SubstituteDuelParticipant(CardInstance replaced, CardInstance substitute, CardInstance reactor)
    {
        Require(DuelSubstitutionWindowOpen(), "There is no accepted duel to enter.");
        var duel = _state.PendingDuel!;
        Require(substitute.OwnerId == replaced.OwnerId && substitute.Id != replaced.Id && InPlay(substitute) && Definition(substitute).Type == CardType.Personality, "That Personality cannot enter the duel.");
        if (duel.ChallengerId == replaced.Id) duel.ChallengerId = substitute.Id;
        else if (duel.ChallengedId == replaced.Id) duel.ChallengedId = substitute.Id;
        else throw new RuleViolation("That Personality is not in the duel.");
        var challenger = Card(duel.ChallengerId); var challenged = Card(duel.ChallengedId);
        if (_state.LastDuel is { } record)
            _state.LastDuel = record with
            {
                Challenger = ViewCard(challenger),
                Challenged = ViewCard(challenged),
                Attachments = Attachments(challenger.Id).Concat(Attachments(challenged.Id)).Select(c => ViewCard(c)).ToArray()
            };
        Emit("duelSubstituted", $"{Definition(substitute).Name} enters the duel instead of {Definition(replaced).Name} ({Definition(reactor).Name}).", reactor.OwnerId, substitute.Id);
    }
    // ---- Ruling 4: whole-unit bow / straighten; ruling 5: a Terrain is never bowed or straightened -------------------------------------------------------------------------------------------------
    // Mendon: "Whole unit bow/straighten bow/straighten every card in the unit (unless bowing/straightening is somehow prevented for one or more cards, in which case, those cards are not
    // straightened/bowed)." A unit is the Personality and every card attached to him (Followers, Items, Spells; core rules, Cards). "Bow it / the target / the targets" on a unit therefore bows each
    // card of it on its own, honoring the prevention that applies to that card (a negation of its bowing, Stalwart, a Torch, ...); StraightenUnit straightens card by card the same way.

    /// <summary>A Terrain (a Strategy with the Terrain keyword): never bowed or straightened.</summary>
    private static bool IsTerrainCard(CardInstance card) => Definition(card).Type == CardType.Strategy && (Definition(card).Keywords ?? []).Contains("Terrain");

    /// <summary>Bows one card as the effect of an action; false when it was already bowed, is a Terrain, or its bowing was prevented (the prevention that fires only for a card that would really bow).</summary>
    private bool BowByEffect(CardInstance card, int actingPlayerId)
    {
        if (card.Bowed || IsTerrainCard(card) || StalwartNegatesBow(card, actingPlayerId)) return false;
        card.Bowed = true;
        return true;
    }

    /// <summary>Bows every card of the card's unit -- the Personality and everything attached to him -- each on its own. Returns how many bowed. `except` names a card already dealt with.</summary>
    private int BowUnitByEffect(CardInstance card, int actingPlayerId, string? except = null)
    {
        var bowed = 0;
        foreach (var member in UnitMembers(card).Where(m => m.Id != except).ToArray())
            if (InPlay(member) && BowByEffect(member, actingPlayerId)) bowed++;
        return bowed;
    }
    // ---- Ruling 6: an Attack's "may compare" clause is the player's choice ---------------------------------------------------------------------------------------------------------------------------
    // Mendon: "The player chooses." Unseen Assailant (Force or Chi, against a Personality), Tsuruchi House Guard (Force or Gold Cost) and Asako Nobunori (current Force or, against a target without
    // attachments, base Force) print "you may compare against ...". The choice is made with the action: one offered action per choice (QueryActions, the way Kaiu Esumi's +2 / -2 are two listed
    // actions), carried as GameCommand.Choice; a command without one compares Force as every Attack does. Tsuruchi Samuru's "that you compare against Gold Cost" has no choice.

    /// <summary>The things an Attack may be compared against, first the ordinary one (Force); null for an ability with no "may compare" clause.</summary>
    private static (string Key, string Label)[]? AttackCompareOptions(string abilityId) => abilityId switch
    {
        "unseen-assailant-attack" => [("force", "Force"), ("chi", "Chi")],
        "tsuruchi-house-guard-attack" => [("force", "Force"), ("gold", "Gold Cost")],
        "nobunori-attack" => [("force", "Force"), ("base", "base Force")],
        _ => AttackSpecCompareOptions(abilityId) // issue #690 (P24, EffectPrimitivesAttack.cs): AttackSpec.CompareOptional
    };

    /// <summary>The compare choice of the Attack now resolving (its own command's), or null.</summary>
    private string? AttackCompareChoiceOf(string abilityId)
    {
        var command = _attackCommandInResolution ?? _runningCommand;
        return command is not null && command.AbilityId == abilityId ? command.Choice : null;
    }

    private void ValidateAttackCompareChoice(string id, GameCommand command)
    {
        if (AttackCompareOptions(id) is not { } options) return;
        Require(command.Choice is null || options.Any(o => o.Key == command.Choice), "Choose what the Attack is compared against.");
        var target = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var card) ? card : null;
        // "If the Ranged Attack targeted a Personality, you may compare it against his Chi."
        if (command.Choice == "chi") Require(target is not null && Definition(target).Type == CardType.Personality, "The Attack can be compared against Chi only when it targets a Personality.");
        // "...against base Force if the target has no attachments."
        if (command.Choice == "base") Require(target is not null && !HasAttachments(target), "The Attack can be compared against base Force only when its target has no attachments.");
        if (command.Choice == "gold") Require(target is not null, "The Attack can be compared against Gold Cost only when it targets a card.");
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRulingR5Hooks()
    {
        EngineHooks.ValidateEarly.Register("ValidateEmperorRulingR5", 1100, static (engine, context) => engine.ValidateEmperorRulingR5(context.id, context.ability, context.command, context.player, context.source));
    }
}
