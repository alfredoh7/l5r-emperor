using static RegressionAssertions;
using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

internal static class EngineHookRegistryTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Eq<T>(T actual, T expected, string? context = null)
    {
        Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{context}: expected {expected}, got {actual}");
    }
    public static void Run(TestRunner runner) => runner.Run("engine-hooks", new (string, Action)[]
    {
        ("Priority, ordinal ties, immutable startup snapshot and duplicate/late registration checks", RegistryContract),
        ("Every migrated legacy hook retains its independently pinned relative order", LegacyOrder),
        ("A populated state saved by the old assembly round trips byte for byte", LegacyState),
        ("An old saved deck-search decision resumes to the old engine's exact state", LegacyResume),
        ("Warmed hook arrays and value contexts allocate nothing on repeated dispatch", AllocationFreeDispatch),
    });
    private static string Fixture(string name) => Path.Combine(Support337.RepoRoot(), "tests/L5R.Core.Tests/Fixtures", name);
    private static void MustReject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException e) { Check(e.Message.Contains(message, StringComparison.Ordinal), e.Message); return; }
        throw new Exception("Expected registration rejection: " + message);
    }
    private static void RegistryContract()
    {
        var registry = new OrderedHooks<Func<int, int>>();
        registry.Register("zeta", 200, static n => n + 3);
        registry.Register("beta", 100, static n => n + 2);
        registry.Register("alpha", 100, static n => n + 1);
        MustReject(() => registry.Register("alpha", 999, static n => n), "Duplicate");
        Eq(string.Join(",", registry.Names), "alpha,beta,zeta");
        Eq(string.Join(",", registry.Items.Select(hook => hook(0))), "1,2,3");
        Check(ReferenceEquals(registry.Items, registry.Items), "The startup array is reused");
        MustReject(() => registry.Register("late", 300, static n => n), "sealed");
    }
    private static void LegacyOrder()
    {
        var text = File.ReadAllText(Fixture("rung4b-legacy-hook-order.json")).Replace("\r\n", "\n");
        Eq(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))), "9C43F7EA9A4A7C93D480595977D0204468F723214C6AE4DE934972D4D1572EB8");
        using var document = JsonDocument.Parse(text);
        var rows = document.RootElement.EnumerateArray().ToArray();
        foreach (var group in rows.Where(row => row.GetProperty("channel").GetString() != "StateJSON").GroupBy(row => row.GetProperty("channel").GetString()!))
        {
            var field = typeof(EngineHooks).GetField(group.Key, BindingFlags.Static | BindingFlags.NonPublic)!;
            var registry = field.GetValue(null)!;
            var names = (IReadOnlyList<string>)registry.GetType().GetProperty("Names", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry)!;
            var expected = group.Select(row => row.GetProperty("hook").GetString()!).ToArray();
            var legacy = expected.ToHashSet(StringComparer.Ordinal);
            Eq(string.Join(",", names.Where(legacy.Contains)), string.Join(",", expected), group.Key);
            // New mechanisms may add their own registrations without editing this frozen fixture.
        }
    }
    private static void LegacyState()
    {
        var json = File.ReadAllText(Fixture("rung4b-legacy-state.json"));
        var restored = JsonSerializer.Deserialize<MatchState>(json)!;
        EqualLegacyJson(restored, json);
        Eq(restored.P1.TacticalActions["legacy|3"], 2);
        Eq(restored.P3.EventWindowCardId, "legacy-event");
    }
    private static void LegacyResume()
    {
        using var saved = File.OpenRead(Fixture("rung4b-legacy-resume.json.gz"));
        using var decompressed = new GZipStream(saved, CompressionMode.Decompress);
        using var fixture = JsonDocument.Parse(decompressed);
        var before = fixture.RootElement.GetProperty("Before").GetRawText();
        var restored = JsonSerializer.Deserialize<MatchState>(before)!;
        EqualLegacyJson(restored, before);
        Eq(restored.PendingChoice?.Type, "searchDeck");
        var engine = MatchEngine.FromTestState(restored);
        Accept(engine, fixture.RootElement.GetProperty("Command").Deserialize<GameCommand>()!);
        EqualLegacyJson(engine.State, fixture.RootElement.GetProperty("After").GetRawText());
    }
    private static void EqualLegacyJson(MatchState state, string expected)
    {
        using var old = JsonDocument.Parse(expected);
        using var current = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var legacyNames = old.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var retained = current.RootElement.EnumerateObject().Where(property => legacyNames.Contains(property.Name))
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        Eq(JsonSerializer.Serialize(retained), expected);
        // A future feature can add its own state property without redefining this historical fixture.
    }
    private static void AllocationFreeDispatch()
    {
        var engine = MatchEngine.CreatePublishedTestMatch();
        var context = new MatchEngine.AttackStrengthAfterContext("__no_hook__", engine.State.Players[0], null, null);
        var hooks = EngineHooks.AttackStrengthAfter.Items;
        for (var i = 0; i < 100; i++) foreach (var hook in hooks) hook(engine, context);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) foreach (var hook in EngineHooks.AttackStrengthAfter.Items) hook(engine, context);
        Eq(GC.GetAllocatedBytesForCurrentThread() - before, 0L);
    }
}
