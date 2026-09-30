namespace L5R.Core;

// When Order Seppuku may be ordered (issue #368, Mendon's ruling). Seppuku is an OPEN action, and an Open action is taken as an action at
// the player's own turn to act: it may not interrupt another player's action, the current player's own action, or any decision or
// reaction window that is waiting for an answer. So it is available only when ALL of these hold:
//
//   (a) the phase is the Action phase (no wired card lets a Personality commit Seppuku in another phase; if one ever does, its grant
//       belongs in this one predicate, next to the phase test), and
//   (b) the player HOLDS PRIORITY -- it is their turn to act (PriorityPlayerId; the Action phase alternates opportunities), and
//   (c) nothing is pending: no PendingChoice (every reaction window, Overconfident / Brash / Harsh Choices / Times of Strife window, deck
//       search, look, copy, Either/Or choice, engage or before-resolution decision ...) and none of the other decision-holding states in
//       DecisionStates below -- a deferred action, an Attack or duel awaiting resolution, a queued draw or discard, a set-aside window.
//
// ONE predicate serves every entry point: the command handler (MatchEngine.OrderSeppuku), the offered actions (QueryActions), and through
// QueryActions the training AI and the client. The rules that are about the CARD rather than the moment (dishonorable, Samurai / Courtier /
// Shugenja, not locked from acting) and the pass owed to Unfortunate Hesitation (#351/#356) stay where they were; Hesitation is checked
// beside this predicate, never instead of it.
public sealed partial class MatchEngine
{
    private const string SeppukuPhaseReason = "Order Seppuku is only available during the Action Phase.";
    private const string SeppukuTurnReason = "Order Seppuku is an action: wait for your turn to act.";
    private const string SeppukuPendingReason = "Finish the pending decision before ordering Seppuku.";

    /// <summary>Every piece of match state that means "a decision, window or deferred action is still open". Adding a <c>Pending*</c> field to
    /// <see cref="MatchState"/> without classifying it here or in <see cref="NotDecisionStates"/> fails the guard test.</summary>
    private static readonly (string Name, Func<MatchState, bool> Open)[] DecisionStates =
    [
        (nameof(MatchState.PendingChoice), s => s.PendingChoice is not null),
        (nameof(MatchState.PendingAttack), s => s.PendingAttack is not null),
        (nameof(MatchState.PendingConqueror), s => s.PendingConqueror is not null),
        (nameof(MatchState.PendingDuel), s => s.PendingDuel is not null),
        (nameof(MatchState.PendingSearch), s => s.PendingSearch is not null),
        (nameof(MatchState.PendingLook), s => s.PendingLook is not null),
        (nameof(MatchState.PendingEffectChoice), s => s.PendingEffectChoice is not null),
        (nameof(MatchState.PendingBeforeBattleResolve), s => s.PendingBeforeBattleResolve is not null),
        (nameof(MatchState.PendingAfterResolution), s => s.PendingAfterResolution is not null),
        (nameof(MatchState.PendingShadowCourtResolution), s => s.PendingShadowCourtResolution is not null), // The Shadow Court (Emperor Edition remaining-cards pass, issue #492, group D): a resolution paused for its decision
        (nameof(MatchState.PendingEngage), s => s.PendingEngage is not null),
        (nameof(MatchState.PendingAdditionalAction), s => s.PendingAdditionalAction is not null),
        (nameof(MatchState.PendingNegatableAbility), s => s.PendingNegatableAbility is not null),
        (nameof(MatchState.PendingOutOfPlayActionReaction), s => s.PendingOutOfPlayActionReaction is not null),
        (nameof(MatchState.PendingAnnouncedAction), s => s.PendingAnnouncedAction is not null),
        (nameof(MatchState.PendingBrashDraws), s => s.PendingBrashDraws > 0),
        (nameof(MatchState.PendingOverconfidentDraws), s => s.PendingOverconfidentDraws.Count > 0),
        (nameof(MatchState.PendingForcedDiscards), s => s.PendingForcedDiscards.Count > 0),
        (nameof(MatchState.PendingHonorLossDecision), s => s.PendingHonorLossDecision is not null), // Ten Thousand Temples' open decision (issue #492, group F)
        (nameof(MatchState.PendingEntryCostDecision), s => s.PendingEntryCostDecision is not null), // En'you's additional cost of entering play (issue #501)
        (nameof(MatchState.PendingHonorChange), s => s.PendingHonorChange is not null), // an Honor change awaiting Reactions (issue #492, R4)
        (nameof(MatchState.PendingEventChoice), s => s.PendingEventChoice is not null), // an Event's open decision (issue #492, group F)
        (nameof(MatchState.PendingEventChoices), s => s.PendingEventChoices.Count > 0), // an optional draw an Event owes, queued behind the action in progress (issue #492, group F)
        (nameof(MatchState.PendingEntrySearches), s => s.PendingEntrySearches.Count > 0), // an "after this card enters play" search queued behind the entry windows (issue #498)
        (nameof(MatchState.PendingAttackTargetSelection), s => s.PendingAttackTargetSelection is not null), // the Attack target selection step after an announce window (issue #498)
        (nameof(MatchState.PendingCopy), s => s.PendingCopy is not null),
        (nameof(MatchState.PendingEitherOr), s => s.PendingEitherOr is not null),
        (nameof(MatchState.PendingOpponentPayment), s => s.PendingOpponentPayment is not null),
        (nameof(MatchState.PendingDeciderChoice), s => s.PendingDeciderChoice is not null),
        (nameof(MatchState.PendingFocusChoice), s => s.PendingFocusChoice is not null),
        (nameof(MatchState.PendingSequencedTarget), s => s.PendingSequencedTarget is not null),
        (nameof(MatchState.PendingProvinceCardChoice), s => s.PendingProvinceCardChoice is not null), // Plains of Otosan Uchi (issue #554): a province-card-targeting effect paused on two candidates
        (nameof(MatchState.ForcedDiscardContinuation), s => s.ForcedDiscardContinuation is not null),
        (nameof(MatchState.SuspendedWindows), s => s.SuspendedWindows.Count > 0),
        (nameof(MatchState.ReactionWindowRemaining), s => s.ReactionWindowRemaining.Count > 0),
    ];

    /// <summary>The <c>Pending*</c> fields that are NOT an open decision, and why: each is either queued work that always sits behind an open
    /// PendingChoice (so the PendingChoice test already covers it) or a delayed check that lives for the whole turn and is not waiting for anyone.</summary>
    private static readonly string[] NotDecisionStates =
    [
        nameof(MatchState.PendingEntryEffectsCardId),      // waits for its open Proclaim window (PendingChoice)
        nameof(MatchState.PendingMoveReactionCardId),      // queued for the move-reaction window (PendingChoice)
        nameof(MatchState.PendingTamokoHonorChecks),       // delayed honor checks drained at end of turn; no one is deciding anything
        nameof(MatchState.PendingLadyMatsusRageWatches),   // reactive watches that last the turn; no one is deciding anything
        nameof(MatchState.PendingDelayedDestructions),     // destructions put off until the turn ends; no one is deciding anything
        nameof(MatchState.PendingEndPhaseRecruits),        // Akodo Kobi's delayed recruits, resolved at the next End Phase; no one is deciding anything
        nameof(MatchState.PendingGrapplingBows),           // Grappling the Snake's bows applied once the action resolves (Seeds of Decay, issue #453); no one is deciding anything
        nameof(MatchState.PendingEndPhaseDraws),           // Eternal Victory Dojo's delayed draws, taken as the next End Phase begins (issue #486); no one is deciding anything
        nameof(MatchState.PendingBanditGangs),             // Bandit Gang's queued pay-or-destroy traits (issue #492 group G); each waits behind its open Either/Or (PendingChoice)
        nameof(MatchState.PendingOutOfPlayNegations),      // what the before-action window set, carried through the out-of-play window that follows it (issue #492, R4); that window is a PendingChoice
        nameof(MatchState.PendingOutOfPlayConsumedGrant),  // the same window's flag that the held action is an additional one (issue #492, R4)
    ];

    /// <summary>The names the guard test compares with every <c>Pending*</c> property of <see cref="MatchState"/>.</summary>
    private static readonly Lazy<IReadOnlyList<string>> DecisionStateNames = new(() => Array.AsReadOnly(DecisionStates.Select(d => d.Name).Concat(EngineHooks.DecisionState.Names).ToArray()));
    public static IReadOnlyList<string> SeppukuDecisionStateNames => DecisionStateNames.Value;
    private static readonly Lazy<IReadOnlyList<string>> NonDecisionStateNames = new(() => Array.AsReadOnly(NotDecisionStates.Concat(EngineHooks.NonDecisionState.Names).ToArray()));
    public static IReadOnlyList<string> SeppukuNonDecisionStateNames => NonDecisionStateNames.Value;

    private bool HasOpenDecisionState()
    {
        foreach (var decision in DecisionStates) if (decision.Open(_state)) return true;
        foreach (var decision in EngineHooks.DecisionState.Items) if (decision(_state)) return true;
        return false;
    }

    /// <summary>The one rule for the MOMENT of Order Seppuku: null when <paramref name="playerId"/> may order it now, otherwise the reason it
    /// is not their time. Cards are judged separately (dishonorable, keyword, locked) and Unfortunate Hesitation's owed pass is checked beside it.</summary>
    public string? SeppukuTimingReason(int playerId)
    {
        if (_state.Phase != Phase.Action) return SeppukuPhaseReason;
        if (_state.PriorityPlayerId != playerId) return SeppukuTurnReason;
        if (HasOpenDecisionState()) return SeppukuPendingReason;
        return null;
    }
}
