namespace L5R.Core;

// ======================================================================================================================================================================================
// FUSAKO (oracle-l5r-10232, Promotional-Emperor; issue #667, cluster "duel-sleep"). Scan: "Iaijustu Limited: Target another player's Personality: Fusako challenges the target. His controller may
// bow him; if he is now bowed, he may refuse the duel. Destroy the duel's loser."
//
// Mendon's confirmed ruling: "The challenged personality may only refuse the duel if they are bowed. If they are straightened, they may choose to bow in order to refuse the challenge."
//
// Built as a pre-accept step of the existing duel challenge (PendingDuel.Stage "AwaitingAccept"):
//   * a bowed target may refuse (declineDuel) without any further choice, or accept (acceptDuel);
//   * an unbowed target's controller is offered "bowToRefuseDuel": it bows him and keeps the challenge open, now with refusal available (he may still accept afterwards);
//   * an unbowed target whose controller does not bow him can only accept (declineDuel is neither offered nor legal).
// The loser is destroyed like every other Iaijutsu "Destroy the duel's loser" (Dueling.cs ApplyDuelOutcome; DuelPresentation; TfStrategiesB2's duel classification list).
// ======================================================================================================================================================================================

public static class DuelSleepFusakoAbilities
{
    public const string FusakoDuel = "fusako-duel";
    /// <summary>The command type of the pre-accept bow: the challenged Personality's controller bows him to gain the right to refuse.</summary>
    public const string BowToRefuse = "bowToRefuseDuel";

    public static Dictionary<string, AbilityDefinition> AddDuelSleepFusako(this Dictionary<string, AbilityDefinition> all)
    {
        all[FusakoDuel] = new("Limited", "Iaijutsu Limited: Target another player's Personality: Fusako challenges the target. His controller may bow him; if he is now bowed, he may refuse the duel. Destroy the duel's loser",
            "enemy-personality", IssuesDuel: true, DuelRefusable: true, ActionKeywords: ["Iaijutsu"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    /// <summary>The challenge's refusal is gated on the challenged Personality being bowed (Fusako).</summary>
    private static bool RefusalNeedsBowedTarget(PendingDuel duel) => duel.AbilityId == DuelSleepFusakoAbilities.FusakoDuel;

    /// <summary>Whether the challenged player may refuse right now: always for an ordinary refusable challenge; for Fusako's only while the target is bowed.</summary>
    private bool DuelRefusalOpen(PendingDuel duel) => duel.Refusable && (!RefusalNeedsBowedTarget(duel) || Card(duel.ChallengedId).Bowed) && DuelRefusalCostProblem(duel) is null; // P18: a refusal whose cost cannot be paid is not open

    /// <summary>Whether the challenged player may still bow his Personality to gain the right to refuse (Fusako, refusable, target straightened).</summary>
    private bool DuelBowToRefuseOffered(PendingDuel duel) =>
        duel.Stage == "AwaitingAccept" && duel.Refusable && RefusalNeedsBowedTarget(duel) && !Card(duel.ChallengedId).Bowed && InPlay(Card(duel.ChallengedId));

    // "His controller may bow him": the target is bowed by his own controller as a choice, so it is not an enemy effect to be negated; the challenge stays open for the answer.
    private void BowChallengedToRefuse(PendingDuel duel)
    {
        Require(DuelBowToRefuseOffered(duel), "The challenged Personality cannot be bowed to refuse this challenge.");
        var challenged = Card(duel.ChallengedId);
        challenged.Bowed = true;
        Emit("duelChallengedBowed", $"{Player(duel.ChallengedPlayerId).Name} bowed {Definition(challenged).Name}; he may now refuse the duel.", duel.ChallengedPlayerId, challenged.Id);
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterDuelSleepFusakoHooks()
    {
        EngineHooks.Abilities.Register("AddDuelSleepFusako", 12400, static abilities => abilities.AddDuelSleepFusako());
    }
}
