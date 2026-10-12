using System.Text.Json;
using L5R.Core;

// Issue #853 (stability): the post-command sweeps are registered, ordered steps (CommandSweeps.cs). The order is part of the rules -- a card must be returned to its owner before the spell copies it owed
// are swept, and a Wave sweep may rely on both -- so the lists are pinned here: adding a sweep means adding it to the expected order on purpose.
internal static class CommandSweepTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Sweeps: the registered order is pinned", OrderPinned),
            ("Sweeps: a rejected command leaves the live state untouched (no sweep mutates it before the rejection)", RejectedCommandChangesNothing),
        ];
        runner.Run("command-sweeps", tests);
    }

    private static void Same(IReadOnlyList<string> actual, params string[] expected)
    {
        if (!actual.SequenceEqual(expected)) throw new Exception($"Sweep order changed: [{string.Join(", ", actual)}] != [{string.Join(", ", expected)}]");
    }

    private static void OrderPinned()
    {
        Same(MatchEngine.SweepOrderForTest("before"), "SpellCopies");
        Same(MatchEngine.SweepOrderForTest("after"), "ReturnCardsToOwners", "SpellCopies", "Wave827", "Wave829", "Wave835", "CreatedCardsInPiles");
        Same(MatchEngine.SweepOrderForTest("accepted"), "Fortifications", "ZoneHistory", "Facts");
    }

    private static void RejectedCommandChangesNothing()
    {
        var e = MatchEngine.CreateEconomyScenario();
        var before = JsonSerializer.Serialize(e.State);
        var result = e.Execute(new GameCommand("useAbility", e.State.ActivePlayerId, e.Version) { AbilityId = "no-such-ability" });
        if (result.Accepted) throw new Exception("The bogus command was accepted.");
        if (JsonSerializer.Serialize(e.State) != before) throw new Exception("A rejected command changed the state.");
    }
}
