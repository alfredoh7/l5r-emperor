namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #501, MECHANICS GROUP D: records, transfer and timed removal (Mendon's specification items 28-31, and the engine half of item 32). Cards wired here: Daidoji Narizane and Daigotsu
// Yajinden - exp3 (personality-wave-92), Broken Cipher, Indecision and Amazing Feat (strategy-wave-78). Each was read against its scan.
//
// New primitives (each reads the core rules; none conflicts with them):
//   * A PER-PLAYER PER-TURN RECON-ACTION COUNTER (item 28): every Recon action a player takes (ActionCarriesKeyword: the ability's own "Recon" tag or its card's keyword) counts as its effects
//     begin to resolve (RunAbilityEffects). Mendon's ruling: a partially negated action counts, a wholly negated one does not. "Wholly negated" is the engine's whole-action negation path --
//     ResolveNegatedAction (the held action's negation, Burning Dreams, Paralyzing Touch, Bad Kharma ...) and AbortActionForBlockedCost -- which pays the costs but never enters
//     RunAbilityEffects; "partially negated" is an action that does resolve with some of its effects stopped per card (MatchState.NegatedEffects: a negated bow, move, destruction ...).
//     Both players' counters return to 0 as each End Phase finishes (FinishTurn: every player's End Phase is the end of a turn, so the count is always "this turn").
//     The same rule holds for the other records by construction: the hand-action count is taken in FinishActionEffectTracking, which a wholly negated action never reaches (CompleteNegatedAction
//     opens no after-action window), and the attach log and cards-into-hand record note what actually happened, which a wholly negated action never does.
//   * "BOW UP TO N DIFFERENT CARDS OF ONE UNIT", N READ LIVE (Narizane): N is read when the action is taken (UseAbility, after validation; stored on MechDState so a paused action keeps it) and the acting
//     player chooses 0..N different unbowed cards of the target's unit as the action resolves (a DeciderChoice of the acting player's, spec "narizane-bow").
//   * THE ATTACH LOG (item 29): every card a player attaches -- an Equip (Limited or Battle), an ability's attach from a hand or a discard pile, a Focus Effect's attach, a created attachment --
//     is recorded (turn, phase, battle, player, card, host, card type). A transfer is not an attach: it is not recorded (Mendon: "it is not an equip action"). "You have attached a Spell this turn"
//     reads the log; it is kept for the current turn only.
//   * "CARDS PUT INTO YOUR HAND THIS PHASE" and "ACTIONS WHICH PUT CARDS INTO HIS HAND THIS PHASE" (item 30): the first is set wherever a card goes into a hand (NoteCardIntoHand, which every hand
//     entry already calls, the draw included); the second counts, per player, each resolved action whose own record says it put a card into that player's hand (FinishActionEffectTracking's
//     HandRecipients). Both reset when the phase changes (the Attack Phase's declaration-to-resolution stretch is one phase, PhaseFamily).
//   * TIMED REMOVAL FROM THE GAME (item 31): MechDState.TimedRemovals, each "when the turn ends" (Yajinden: applied as the turn ends, FinishTurn) or "when its player's next turn begins"
//     (Amazing Feat: applied as that player's next turn begins, StartTurn). Not optional. A card no longer in play is not removed (the effect has lost its card: the R2 convention, and the
//     Celestial Sword of the Phoenix precedent); the whole unit leaves play the same way (core rules, Units).
//   * A CREATED COPY IN THE VIEW (item 32, engine half): group B's view fields (EmperorMechBCopies.cs, commit "Expose copied cards in the view", cherry-picked onto this branch unchanged):
//     Amazing Feat's copy is created with CreatePersonality's `original` (MarkAsCopy: IsCopy, CopyOfCardId, CopyOfDefinitionId) and its timed removal is added as a CopyConditions sentence.
//     A card that is not a copy (Yajinden's raised Personality) has no view field for its pending removal; the "timedRemovalScheduled" event records it.
// ======================================================================================================================================================================================
public static class EmperorMechDAbilities
{
    public const string NarizaneBow = "narizane-bow";
    public const string BrokenCipherFocus = "broken-cipher-focus";
    public const string BrokenCipherDraw = "broken-cipher-draw";
    public const string IndecisionDiscard = "indecision-discard";
    public const string IndecisionMove = "indecision-move";
    public const string YajindenRaise = "yajinden-raise";
    public const string AmazingFeatCreate = "amazing-feat-create";

    public static Dictionary<string, AbilityDefinition> AddEmperorMechD(this Dictionary<string, AbilityDefinition> all)
    {
        // Daidoji Narizane (oracle-l5r-1663): "Battle: Target an enemy Personality: Bow zero or more different cards in his unit up to the number of Recon actions you have taken this turn."
        all[NarizaneBow] = new("Battle", "Target an enemy Personality: Bow zero or more different cards in his unit up to the number of Recon actions you have taken this turn", "enemy-personality");
        // Broken Cipher (oracle-l5r-1151): "As a Focus Effect: You may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location." and "Battle: If you have
        // not put cards into your hand this phase and you have attached a Spell this turn: Draw two cards." The conditions sit before the colon: they gate taking the action.
        all[BrokenCipherFocus] = new("FocusEffect", "As a Focus Effect: You may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location", "none");
        all[BrokenCipherDraw] = new("Battle", "If you have not put cards into your hand this phase and you have attached a Spell this turn: Draw two cards", "none");
        // Indecision (oracle-l5r-3736): "Reaction: After an action resolves, if its player has resolved two or more actions which put cards into his hand this phase: He discards two cards." -- a
        // Strategy played from a hand in the after-action-resolved window; "its player" is the player of the action that resolved (either player), who chooses the two cards he discards.
        // "Battle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved." -- Hidden Defenses' Reaction shape as a Battle
        // action: the performer is any of the player's Personalities wherever he is (he is moving to the battle), so the action moves a Personality to the battle (MovesToBattle).
        all[IndecisionDiscard] = new("Reaction", "After an action resolves, if its player has resolved two or more actions which put cards into his hand this phase: He discards two cards", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        all[IndecisionMove] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved", "none",
            Performer: "own-personality", Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Daigotsu Yajinden - exp3 (oracle-l5r-1783): "After Yajinden enters play: Lose 4 Honor." (EntryHonorLoss) and "Limited: Target a dead Personality: Yajinden raises him from the dead. Bring him
        // into play (under your control) ignoring costs and Honor Requirements. Remove him from the game when the turn ends." -- any player's dead Personality; "raises him from the dead" is flavour.
        all[YajindenRaise] = new("Limited", "Target a dead Personality: Yajinden raises him from the dead. Bring him into play (under your control) ignoring costs and Honor Requirements. Remove him from the game when the turn ends",
            "dead-personality-any-discard");
        // Amazing Feat (oracle-l5r-340): "Limited: Bow your performing Artisan Personality and target a non-Unique Personality: Create a Personality with the same base Force, Chi, Personal Honor and
        // keywords as the targeted Personality, who permanently copies all his base abilities except those that copy abilities. Remove the created Personality from the game when your next turn begins."
        all[AmazingFeatCreate] = new("Limited", "Bow your performing Artisan Personality and target a non-Unique Personality: Create a Personality with the same base Force, Chi, Personal Honor and keywords as the targeted Personality, who permanently copies all his base abilities except those that copy abilities. Remove the created Personality from the game when your next turn begins",
            "non-unique-personality", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Artisan"], Costs: ActionCost.BowPerformer, CopiesAbilities: true);
        return all;
    }
}

public static class EmperorMechDSpecs
{
    /// <summary>Daidoji Narizane: the acting player chooses zero to N different unbowed cards of the target's unit to bow (N: his Recon actions this turn when the action was taken).</summary>
    public const string NarizaneBow = "narizane-bow";
    /// <summary>Indecision: the player of the action that resolved chooses and discards two cards from his hand.</summary>
    public const string IndecisionDiscard = "indecision-discard";

    public static bool IsDeciderSpec(string spec) => spec is NarizaneBow or IndecisionDiscard;
}

/// <summary>When a <see cref="TimedRemoval"/> is carried out.</summary>
public static class TimedRemovalWhen
{
    /// <summary>"Remove him from the game when the turn ends" (Yajinden).</summary>
    public const string TurnEnds = "turn-ends";
    /// <summary>"Remove ... from the game when your next turn begins" (Amazing Feat): when PlayerId's TurnsTaken reaches AtTurnsTaken.</summary>
    public const string PlayersNextTurnBegins = "players-next-turn-begins";
}

/// <summary>A card to be removed from the game at a fixed moment, whatever happens meanwhile (issue #501, item 31). Not optional. Source names the card whose effect it is.</summary>
public sealed record TimedRemoval(string CardId, string When, int PlayerId, int AtTurnsTaken, string Source);

/// <summary>One card a player attached (issue #501, item 29): an Equip or any other action or effect that attaches a card. Transfers are not attaches and are not recorded.</summary>
public sealed record AttachRecord(int Turn, Phase Phase, int BattleNumber, int PlayerId, string CardId, string? HostId, CardType CardType, string Via);

/// <summary>The records of mechanics group D (issue #501), kept together on MatchState.MechD.</summary>
public sealed class MechDState
{
    /// <summary>Recon actions each player has taken this turn (reset as each End Phase finishes).</summary>
    public int[] ReconActionsThisTurn { get; set; } = new int[2];
    /// <summary>Whether a card has gone into each player's hand this phase.</summary>
    public bool[] CardsIntoHandThisPhase { get; set; } = new bool[2];
    /// <summary>How many resolved actions of each player have put cards into his own hand this phase.</summary>
    public int[] HandActionsThisPhase { get; set; } = new int[2];
    /// <summary>The attach log of the current turn.</summary>
    public List<AttachRecord> AttachLog { get; set; } = [];
    public List<TimedRemoval> TimedRemovals { get; set; } = [];
    /// <summary>Narizane's N, read when the action was taken, keyed by the source card id.</summary>
    public Dictionary<string, int> NarizaneLimits { get; set; } = [];
    /// <summary>Indecision's "its player", fixed when the Reaction is played, keyed by the source card id.</summary>
    public Dictionary<string, int> IndecisionPlayers { get; set; } = [];
}

public sealed partial class MatchEngine
{
    private MechDState MechD => _state.MechD;

    // ---- item 28: the Recon-action counter --------------------------------------------------------------------------------------------------------------------------------------------------

    // Called from RunAbilityEffects only: an action whose effects resolve, wholly or in part. A wholly negated action never gets here.
    private void CountReconActionTaken(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (!IsAction(ability) || TraitReactions.Is(command.AbilityId ?? "") || !ActionCarriesKeyword(ability, source, "Recon")) return;
        MechD.ReconActionsThisTurn[command.PlayerId]++;
    }

    /// <summary>Recon actions <paramref name="playerId"/> has taken this turn.</summary>
    internal int ReconActionsTakenThisTurn(int playerId) => MechD.ReconActionsThisTurn[playerId];

    // UseAbility, right after validation: the action is being taken (announced). A before-action window's Reactions come after this, so they do not change N.
    private void CaptureAtAnnounceForMechD(GameCommand command)
    {
        if (command.AbilityId == EmperorMechDAbilities.NarizaneBow && command.CardId is { } narizane) MechD.NarizaneLimits[narizane] = MechD.ReconActionsThisTurn[command.PlayerId];
        // Indecision: "its player" is the player of the action that resolved, fixed as the Reaction is played (an action resolving in its own before-action window must not change him).
        if (command.AbilityId == EmperorMechDAbilities.IndecisionDiscard && command.CardId is { } indecision && _state.LastResolvedAction is { } facts) MechD.IndecisionPlayers[indecision] = facts.PlayerId;
    }

    // ---- item 29: the attach log ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Called at every place a card is attached (Equip, an ability's attach from a hand or a discard pile, a Focus Effect's attach, a created attachment). The host is recorded when it is already
    // known (a hand attach is noted as the card leaves the hand, before it is placed).
    private void NoteAttached(int playerId, CardInstance attachment, string via)
    {
        MechD.AttachLog.Add(new(_state.TurnNumber, _state.Phase, _state.BattleNumber, playerId, attachment.Id, attachment.AttachedTo, Definition(attachment).Type, via));
    }

    private int SpellsAttachedThisTurn(int playerId) => MechD.AttachLog.Count(r => r.Turn == _state.TurnNumber && r.PlayerId == playerId && r.CardType == CardType.Spell);

    // ---- item 30: cards into hand ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private void NoteCardIntoHandThisPhase(int playerId) => MechD.CardsIntoHandThisPhase[playerId] = true;

    // FinishActionEffectTracking: the action that just resolved put a card into its own player's hand.
    private void CountHandActionResolved(int playerId, List<int> handRecipients)
    {
        if (handRecipients.Contains(playerId)) MechD.HandActionsThisPhase[playerId]++;
    }

    // SetPhase: a new phase begins (the Attack Phase's segments are one phase).
    private void ResetMechDPhaseRecords(Phase from, Phase to)
    {
        if (PhaseFamily(from) == PhaseFamily(to)) return;
        MechD.CardsIntoHandThisPhase = new bool[2];
        MechD.HandActionsThisPhase = new int[2];
    }

    // ---- item 31: timed removal -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ScheduleTimedRemoval(CardInstance card, string when, int playerId, string source)
    {
        var at = when == TimedRemovalWhen.PlayersNextTurnBegins ? Player(playerId).TurnsTaken + 1 : 0;
        MechD.TimedRemovals.Add(new(card.Id, when, playerId, at, source));
        Emit("timedRemovalScheduled", $"{source}: {Definition(card).Name} will be removed from the game {DescribeRemovalMoment(when, playerId)}.", playerId, card.Id);
    }

    private string DescribeRemovalMoment(string when, int playerId) =>
        when == TimedRemovalWhen.TurnEnds ? "when the turn ends" : $"when {Player(playerId).Name}'s next turn begins";

    // Carries out every removal due at this moment. A card that has left play meanwhile is not removed (its record is dropped all the same).
    private void ApplyTimedRemovals(Func<TimedRemoval, bool> due)
    {
        foreach (var removal in MechD.TimedRemovals.Where(due).ToArray())
        {
            MechD.TimedRemovals.Remove(removal);
            if (!_state.Cards.TryGetValue(removal.CardId, out var card) || !InPlay(card)) continue;
            if (Definition(card).Type == CardType.Personality) RemoveUnitFromGame(card, removal.Source);
            else RemoveFromGame(card);
            Emit("timedRemoval", $"{removal.Source}: {Definition(card).Name} is removed from the game {DescribeRemovalMoment(removal.When, removal.PlayerId)}.", removal.PlayerId, card.Id);
        }
    }

    // FinishTurn: the turn ends -- "when the turn ends" removals, the Recon counters and the attach log of the turn.
    private void MechDTurnEnds()
    {
        ApplyTimedRemovals(r => r.When == TimedRemovalWhen.TurnEnds);
        MechD.ReconActionsThisTurn = new int[2];
        MechD.NarizaneLimits.Clear();
        MechD.IndecisionPlayers.Clear();
        MechD.AttachLog.Clear();
    }

    // StartTurn: the player's turn begins (TurnsTaken has just advanced).
    private void MechDTurnBegins(PlayerState player) =>
        ApplyTimedRemovals(r => r.When == TimedRemovalWhen.PlayersNextTurnBegins && r.PlayerId == player.Id && player.TurnsTaken >= r.AtTurnsTaken);

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateEmperorMechD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case EmperorMechDAbilities.BrokenCipherDraw:
                Require(!MechD.CardsIntoHandThisPhase[player.Id], "You have put cards into your hand this phase.");
                Require(SpellsAttachedThisTurn(player.Id) > 0, "You have not attached a Spell this turn.");
                break;
            case EmperorMechDAbilities.IndecisionMove:
                Require(_state.CurrentBattleProvince is int battle && Army(1 - player.Id, battle).Any(), "There are no enemy units at the current battlefield.");
                break;
        }
    }

    // AfterActionResolvedApplies: Indecision's condition -- the player of the action that resolved has resolved two or more actions this phase that put cards into his own hand.
    private bool? MechDAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts) => abilityId switch
    {
        EmperorMechDAbilities.IndecisionDiscard => MechD.HandActionsThisPhase[facts.PlayerId] >= 2,
        _ => null
    };

    // ---- execution: null when the id is not this group's; false when its effects are done; true when a decision opened and the action waits -----------------------------------------------------

    private bool? RunEmperorMechD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorMechDAbilities.NarizaneBow:
            {
                var limit = MechD.NarizaneLimits.TryGetValue(source!.Id, out var captured) ? captured : MechD.ReconActionsThisTurn[player.Id];
                if (limit == 0 || NarizaneCandidates(target!).Count == 0)
                {
                    Emit("narizaneNothing", limit == 0 ? $"{Definition(source).Name}: {player.Name} has taken no Recon actions this turn, so nothing is bowed."
                        : $"{Definition(source).Name}: no card in {Definition(target!).Name}'s unit is unbowed.", player.Id, source.Id);
                    return false;
                }
                return BeginDeciderChoice(EmperorMechDSpecs.NarizaneBow, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            }
            case EmperorMechDAbilities.BrokenCipherDraw:
                Draw(player); Draw(player);
                return false;
            case EmperorMechDAbilities.IndecisionDiscard:
                return BeginDeciderChoice(EmperorMechDSpecs.IndecisionDiscard, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            case EmperorMechDAbilities.IndecisionMove:
            {
                // The move is the shared "to the current battlefield" rule (MoveDestinations.cs: a negated or blocked move leaves him where he was); he straightens only if he moved.
                var mover = Card(command.PerformerId!);
                var wasThere = AtBattle(mover);
                ExecuteMove(command, ability, source, target);
                if (!wasThere && AtBattle(mover)) Straighten(mover);
                return false;
            }
            case EmperorMechDAbilities.YajindenRaise:
                return RaiseWithYajinden(player, source!, target!);
            case EmperorMechDAbilities.AmazingFeatCreate:
                return CreateAmazingFeatCopy(player, source!, target!);
            default:
                return null;
        }
    }

    // ---- Narizane -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The unbowed cards of the target's unit: the Personality and every card attached to him (or to one of his attachments). A bowed card is not offered: bowing it would do nothing.
    private List<string> NarizaneCandidates(CardInstance personality)
    {
        var unit = new List<CardInstance> { personality };
        for (var i = 0; i < unit.Count; i++) unit.AddRange(Attachments(unit[i].Id));
        return unit.Where(c => !c.Bowed && Definition(c).Type != CardType.Ring).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
    }

    // ---- Yajinden ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The dead Personality enters play under the acting player's control (a card of another player's pile is taken control of first), ignoring costs and Honor Requirements, and is removed from
    // the game when the turn ends. A Loyal Personality who will not be controlled by the player, or a Unique/Singular one whose title the player already controls, cannot be brought into play.
    private bool RaiseWithYajinden(PlayerState player, CardInstance source, CardInstance target)
    {
        var name = Definition(target).Name;
        if (target.Zone != Zone.Dead) { Emit("raiseFailed", $"{Definition(source).Name}: {name} is no longer dead.", player.Id, target.Id); return false; }
        if (target.OwnerId != player.Id && !LoyaltyAllowsControl(target, player.Clan, player.Id))
        {
            Emit("controlRefused", $"{Definition(source).Name}: {name} is Loyal and will not be controlled by {player.Name}; he stays dead.", player.Id, target.Id);
            return false;
        }
        if ((Keyword(target, "Unique") || Keyword(target, "Singular")) && ControlsCopyOf(player.Id, name))
        {
            Emit("controlRefused", $"{Definition(source).Name}: {player.Name} already controls a {name}; he stays dead.", player.Id, target.Id);
            return false;
        }
        TakeControl(target, player.Id);
        return BringDeadPersonalityIntoPlay(player, target, Definition(source).Name, null, raised => ScheduleTimedRemoval(raised, TimedRemovalWhen.TurnEnds, player.Id, Definition(source).Name));
    }

    // ---- Amazing Feat -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The created Personality: the target's base Force, Chi, Personal Honor and keywords (its Clan alignment with them), every base ability of his except those that copy abilities, and nothing
    // else -- no trait (a trait is not an ability, core rules, Reading the Cards), Gold Cost 0 and no Honor Requirement (core rules, Created cards). It shows the target's scan and title as a copy.
    private bool CreateAmazingFeatCopy(PlayerState player, CardInstance source, CardInstance target)
    {
        var copied = Definition(target);
        var abilities = (copied.Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var def) && !def.CopiesAbilities && !def.NotCopyable && !TraitReactions.Is(a)).ToArray();
        var printed = new CardDefinition(copied.Id, copied.Name, CardType.Personality, copied.Clan, 0, 0, copied.Force, copied.Chi, copied.PersonalHonor, null, 0, 0, "none",
            $"Created by Amazing Feat: {copied.Force}F/{copied.Chi}C/{copied.PersonalHonor}PH, the keywords of {copied.Name}, and his base abilities except those that copy abilities.",
            false, copied.ArtworkKey, (copied.Keywords ?? []).ToArray(), abilities, 0, copied.Clans);
        var sourceName = Definition(source).Name;
        return CreatePersonality(player.Id, CardCatalog.Get(target.DefinitionId), false, null, out _, printed, () =>
        {
            var created = Card($"created-{_state.CreatedCardSequence:D4}"); // the card CreatePersonality has just minted (already marked as a copy of the target)
            ScheduleTimedRemoval(created, TimedRemovalWhen.PlayersNextTurnBegins, player.Id, sourceName);
            AddCopyCondition(created, $"Removed from the game when {player.Name}'s next turn begins ({sourceName}).");
        }, createdCopy: true, original: target);
    }

    // ---- decider choices (DeciderChoice.cs hooks) -------------------------------------------------------------------------------------------------------------------------------------------

    private int MechDDeciderId(string specId, GameCommand command) =>
        specId == EmperorMechDSpecs.IndecisionDiscard
            ? command.CardId is { } source && MechD.IndecisionPlayers.TryGetValue(source, out var fixedPlayer) ? fixedPlayer : _state.LastResolvedAction?.PlayerId ?? 1 - command.PlayerId
            : command.PlayerId;

    private (List<string> Candidates, int Min, int Max) MechDDeciderOptions(string specId, int deciderId, GameCommand command)
    {
        if (specId == EmperorMechDSpecs.IndecisionDiscard)
        {
            var hand = Player(deciderId).Hand.Order(StringComparer.Ordinal).ToList();
            var count = Math.Min(2, hand.Count);
            return (hand, count, count);
        }
        var target = Card(command.TargetId!);
        var limit = command.CardId is { } sourceId && MechD.NarizaneLimits.TryGetValue(sourceId, out var captured) ? captured : MechD.ReconActionsThisTurn[command.PlayerId];
        return (NarizaneCandidates(target), 0, limit);
    }

    private string MechDDeciderPrompt(PendingDeciderChoice p) => p.SpecId == EmperorMechDSpecs.IndecisionDiscard
        ? $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose {p.Max} card{(p.Max == 1 ? "" : "s")} from your hand to discard."
        : $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose up to {p.Max} different card{(p.Max == 1 ? "" : "s")} of the target's unit to bow.";

    private static string MechDDeciderVerb(string specId) => specId == EmperorMechDSpecs.IndecisionDiscard ? "Discard" : "Bow";

    private void ApplyMechDDeciderChoice(PendingDeciderChoice p)
    {
        foreach (var id in p.Chosen)
        {
            var card = Card(id);
            if (p.SpecId == EmperorMechDSpecs.IndecisionDiscard) { if (card.Zone == Zone.Hand && !P2HandDiscardBlocked(card.OwnerId, Card(p.SourceCardId).OwnerId, true, "Indecision")) DiscardFate(Player(card.OwnerId), card); } // issue #503: Pillowbook, The Scorpion Wall is Finished
            else if (InPlay(card) && !StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
        }
    }

    // ---- Broken Cipher's Focus Effect (FocusEffectChoice.cs hooks) ----------------------------------------------------------------------------------------------------------------------------

    private static bool IsMechDFocusChoice(string abilityId) => abilityId == EmperorMechDAbilities.BrokenCipherFocus;
    private const char TransferSeparator = '>';

    // Every legal transfer, as "spell>shugenja": a Spell attached to the owner's Personality in this duel, to another of his Shugenja at that Personality's location who could legally hold it.
    private List<string> BrokenCipherTransfers(int ownerId)
    {
        if (_state.PendingDuel is not { } duel) return [];
        var dueller = Card(duel.ChallengerPlayerId == ownerId ? duel.ChallengerId : duel.ChallengedId);
        if (!InPlay(dueller) || dueller.OwnerId != ownerId) return [];
        var spells = Attachments(dueller.Id).Where(a => Definition(a).Type == CardType.Spell).ToArray();
        var hosts = _state.Cards.Values.Where(c => c.Id != dueller.Id && c.OwnerId == ownerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Shugenja") && SameLocation(c, dueller)).ToArray();
        return spells.SelectMany(s => hosts.Where(h => CanTransferTo(s, h)).Select(h => s.Id + TransferSeparator + h.Id)).Order(StringComparer.Ordinal).ToList();
    }

    private bool BeginMechDFocusChoice(int index, string focusCardId, string abilityId)
    {
        var focus = Card(focusCardId); var owner = focus.OwnerId;
        var candidates = BrokenCipherTransfers(owner);
        if (candidates.Count == 0)
        {
            Emit("focusEffectNothing", $"{Definition(focus).Name}: {Player(owner).Name} has no Spell on the Personality in this duel that another of his Shugenja there could take.", owner, focusCardId);
            return false;
        }
        _state.PendingFocusChoice = new PendingFocusChoice { Index = index, FocusCardId = focusCardId, PlayerId = owner, Candidates = candidates, AbilityId = abilityId };
        _state.PendingChoice = new("focusEffectChoice", owner, focusCardId, $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location.");
        _state.PriorityPlayerId = owner; _state.ConsecutivePasses = 0;
        Emit("focusEffectChoiceOffered", $"{Player(owner).Name} may transfer a Spell ({Definition(focus).Name}).", owner, focusCardId);
        return true;
    }

    private void ResolveMechDFocusChoice(GameCommand command, PendingFocusChoice pending)
    {
        var focus = Card(pending.FocusCardId);
        if (command.Choice == "decline") Emit("focusEffectDeclined", $"{Player(pending.PlayerId).Name} transferred nothing ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        else
        {
            Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered transfers, or decline.");
            var parts = command.TargetId!.Split(TransferSeparator);
            TransferAttachment(Card(parts[0]), Card(parts[1]), Definition(focus).Name);
        }
        _state.PendingChoice = null; _state.PendingFocusChoice = null;
        ContinueDuelFocusEffects(pending.Index + 1);
    }

    private IEnumerable<ActionOption> MechDFocusChoiceActions(PendingFocusChoice p, int playerId)
    {
        foreach (var key in p.Candidates)
        {
            var parts = key.Split(TransferSeparator);
            yield return new("chooseFocusEffect", $"Transfer {Definition(Card(parts[0])).Name} to {Definition(Card(parts[1])).Name}", true, null,
                new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, TargetId: key), parts[0]);
        }
        yield return new("chooseFocusEffect", "Transfer nothing", true, null, new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, Choice: "decline"), p.FocusCardId);
    }
}
