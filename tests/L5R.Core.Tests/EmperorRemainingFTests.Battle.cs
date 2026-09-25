using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: Alter History and Boastful Proclamation through a real battle resolution (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    private static void EventsThroughARealBattle()
    {
        // Player 1 attacks player 0's province 0 with a Force 4 Personality against no defender and a province of strength 1: it is destroyed by the battle's resolution.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var history = Add(s, AlterHistory, 1, Zone.EventInPlay);
        var boast = Add(s, BoastfulProclamation, 0, Zone.EventInPlay); boast.AbilityUses["boastful-proclamation:armed"] = 1; boast.AbilityUses["boastful-proclamation:province"] = 0;
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        s.Players[0].Provinces[0].Strength = 1;
        var start = StartingHonorOf(s, 0);
        s.Players[0].Honor = start + 1;
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(attacker.Id);
        var handBefore = e.State.Players[1].Hand.Count;
        // Both players pass: the battle resolves.
        for (var guard = 0; guard < 12 && !e.State.Players[0].Provinces[0].Destroyed && e.State.PendingChoice is null; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(o => o.Type == "pass" && o.Enabled).Command!);
        Check(e.State.Players[0].Provinces[0].Destroyed, "the battle destroyed the province");
        Eq(Honor(e, 0), start + 1 - 5, "Alter History: the Defender lost 5 Honor");
        Check(e.State.Players[0].IgnoresHonorRequirements, "and, below his starting Family Honor, ignores Honor Requirements");
        Eq(e.State.Cards[history.Id].Zone, Zone.DynastyDiscard, "Alter History is spent");
        Eq(e.CardForceForTest(attacker.Id), force + 1, "Boastful Proclamation: a +1F token on each Personality of the attacking army");
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: not "eventChoice" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "eventChoice", "and the Attacker is asked whether to draw");
        Eq(e.State.PriorityPlayerId, 1, "the Attacker decides");
        AnswerEvent(e, choice: "yes");
        Eq(e.State.Players[1].Hand.Count, handBefore + 1, "he drew a card");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "eventChoice", "and the battle carried on");
    }
}
