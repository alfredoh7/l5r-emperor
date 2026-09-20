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
        (nameof(MatchState.PendingEngage), s => s.PendingEngage is not null),
        (nameof(MatchState.PendingAdditionalAction), s => s.PendingAdditionalAction is not null),
        (nameof(MatchState.PendingNegatableAbility), s => s.PendingNegatableAbility is not null),
        (nameof(MatchState.PendingOutOfPlayActionReaction), s => s.PendingOutOfPlayActionReaction is not null),
        (nameof(MatchState.PendingAnnouncedAction), s => s.PendingAnnouncedAction is not null),
        (nameof(MatchState.PendingBrashDraws), s => s.PendingBrashDraws > 0),
        (nameof(MatchState.PendingOverconfidentDraws), s => s.PendingOverconfidentDraws.Count > 0),
        (nameof(MatchState.PendingForcedDiscards), s => s.PendingForcedDiscards.Count > 0),
        (nameof(MatchState.PendingCopy), s => s.PendingCopy is not null),
        (nameof(MatchState.PendingEitherOr), s => s.PendingEitherOr is not null),
        (nameof(MatchState.PendingOpponentPayment), s => s.PendingOpponentPayment is not null),
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
    ];

    /// <summary>The names the guard test compares with every <c>Pending*</c> property of <see cref="MatchState"/>.</summary>
    public static IReadOnlyList<string> SeppukuDecisionStateNames { get; } = DecisionStates.Select(d => d.Name).ToArray();
    public static IReadOnlyList<string> SeppukuNonDecisionStateNames => NotDecisionStates;

    /// <summary>The one rule for the MOMENT of Order Seppuku: null when <paramref name="playerId"/> may order it now, otherwise the reason it
    /// is not their time. Cards are judged separately (dishonorable, keyword, locked) and Unfortunate Hesitation's owed pass is checked beside it.</summary>
    public string? SeppukuTimingReason(int playerId)
    {
        if (_state.Phase != Phase.Action) return SeppukuPhaseReason;
        if (_state.PriorityPlayerId != playerId) return SeppukuTurnReason;
        if (DecisionStates.Any(d => d.Open(_state))) return SeppukuPendingReason;
        return null;
    }
}
