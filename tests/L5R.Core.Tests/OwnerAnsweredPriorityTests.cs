using L5R.Core;

// Issue #642 (found by the bot soak, seeds 1139, 1222, 1328, 1397 and, earlier, 15): a choice only its owner can answer must give its owner priority, or the owner sees "waiting"
// while the other player is offered a choice the engine then refuses, and the match stalls. Reaction windows are different (priority alternates inside them) and are left alone.
internal static class OwnerAnsweredPriorityTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run(TestRunner runner) => runner.Run("owner-answered-priority", [
        ("A choice answered by its owner (Harsh Choices, the province reveal order, Offering Reverence, an Either/Or) hands its owner priority; a Reaction window keeps its alternating priority", Scenarios),
    ]);

    private static MatchEngine Engine(string type, int owner, int priority)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.ActivePlayerId = priority; s.PriorityPlayerId = priority;
        s.PendingChoice = new(type, owner, null, "test choice");
        return MatchEngine.FromTestState(s);
    }

    private static void Scenarios()
    {
        var normalize = typeof(MatchEngine).GetMethod("NormalizeOwnerAnsweredPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var type in new[] { "harshChoices", "provinceOrder", "offeringReverence", "eitherOr", "testedLandProvince" })
        {
            var e = Engine(type, owner: 0, priority: 1);
            normalize.Invoke(e, []);
            Check(e.State.PriorityPlayerId == 0, $"{type}: its owner holds priority");
            Check(e.QueryActions(1).All(a => a.Type != type), $"{type}: the other player is not offered it");
            var already = Engine(type, owner: 1, priority: 1);
            normalize.Invoke(already, []);
            Check(already.State.PriorityPlayerId == 1, $"{type}: priority already with the owner is left alone");
        }
        var window = Engine("proclaim", owner: 0, priority: 1); // a Reaction window: the pending player is the one who acted, priority alternates
        normalize.Invoke(window, []);
        Check(window.State.PriorityPlayerId == 1, "a Reaction window keeps its alternating priority");
        var none = MatchEngine.CreateEconomyScenario(); normalize.Invoke(none, []);
        Check(none.State.PendingChoice is null, "nothing pending: nothing to do");
    }
}
