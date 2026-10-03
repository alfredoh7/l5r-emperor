using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #546: GetView projected and reallocated _state.Events (the whole match's history) on every single call, an
// O(total events so far) cost repeated for the life of a match -- GetView is called several times per submitted
// command (RoomManager.CreateState/SubmitAsync call it per player, sometimes twice). MatchQueries.ViewedEvents now
// caches the masked-per-player projection and only extends it with newly appended events. The cache is gated on the
// exact List<GameEvent> object reference plus its length, not length alone, because the engine reassigns _state
// wholesale on its internal rollback paths (MatchEngine.cs:197,247,270) -- a stale reference can never be mistaken
// for the current one, so a miss just falls back to a full recompute and can never serve wrong data.
internal static class ViewEventCacheTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);

    public static void Run(TestRunner runner) => runner.Run("view-event-cache", [
        ("A no-op GetView call returns the exact same Events array instance when nothing changed since the last call (the cache-hit path actually engages)", NoOpReturnsSameArray),
        ("A later GetView call after a new event extends the cache correctly: earlier entries are unchanged by value and the new entry is appended in order", GrowthExtendsCorrectly),
        ("duelFocused events keep CardId for the acting player's own view and mask it for the other player's view, whether or not the cache already holds an entry for either viewer", MaskingUnaffectedByCache),
    ]);

    private static void NoOpReturnsSameArray()
    {
        var e = MatchEngine.CreateStarterMatch();
        var first = e.GetView(0).Events;
        var second = e.GetView(0).Events;
        Check(ReferenceEquals(first, second), "Two GetView calls with no state change between them should return the identical cached array, not recompute it");
    }

    private static void GrowthExtendsCorrectly()
    {
        var s = Table(Phase.Action, 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.GetView(0).Events;
        Accept(e, new GameCommand("pass", 0, e.Version));
        var after = e.GetView(0).Events;
        Eq(after.Length, before.Length + 1, "Exactly one new event from the pass");
        for (var i = 0; i < before.Length; i++) Eq(after[i], before[i], $"Event {i} changed after a later event was appended");
        Eq(after[^1].Type, "passed", "The new event is the pass");
    }

    private static void MaskingUnaffectedByCache()
    {
        var e = Edit(MatchEngine.CreateStarterMatch(), s => s.Events.Add(new(s.Events.Count + 1, "duelFocused", "Player 0 focused a card.", 0, "fx-focused-card")));
        // Warm the cache for both viewers before asserting, so a masking bug hidden behind the fast path would still show up.
        _ = e.GetView(0); _ = e.GetView(1);
        Eq(e.GetView(0).Events[^1].CardId, "fx-focused-card", "The acting player's own view keeps the focused card's identity");
        Eq(e.GetView(1).Events[^1].CardId, null, "The other player's view has the focused card's identity masked");
    }
}
