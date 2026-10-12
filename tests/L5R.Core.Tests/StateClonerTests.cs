using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using L5R.Core;

// Issue #863: MatchEngine.Copy is a deep cloner (StateCloner.cs) instead of the JSON round trip. Behaviour must not change, so:
//   * differential: a reference engine running on the original JSON copy (MatchEngine.ReferenceJsonCopyForTests) and an engine on the fast copy
//     play the same seeded random-choice games and fixture scenarios in lockstep. At every step the fast copy of the reference state serialises
//     identically to the JSON round trip AND matches it member for member (private and non-serialised fields, runtime collection types and
//     comparers included), and the two engines hold the same state, show the same views, offer the same actions and return the same results;
//   * independence: the copy shares no mutable object with the original, and mutating every collection/field of the copy leaves the original intact;
//   * replay: replaying the recorded (copied) commands reproduces the live game, as it does on the JSON copy.
internal static class StateClonerTests
{
    public static void Run(TestRunner runner) => runner.Run("state-cloner", [
        ("Copy = JSON round trip at every step of seeded random games (seeds 11, 37, 83), fast and JSON engines in lockstep", () => { foreach (var s in new ulong[] { 11, 37, 83 }) Lockstep(() => MatchEngine.CreatePublishedMatch(s), s, 900, $"published seed {s}"); }),
        ("Copy = JSON round trip along every fixture scenario (economy, starter, showcases, duel demos)", Fixtures),
        ("Copy = JSON round trip for every catalogue card definition", Definitions),
        ("Copy shares nothing with the original: mutating every member of the copy leaves the original unchanged", Independence),
        ("Replay of the recorded (copied) commands equals the live game and a JSON-copy replay; an engine built from a copied State offers the same actions", ReplayMatchesLive),
    ]);

    // Runs the action with this thread's engines on the original JSON copy.
    private static T Reference<T>(Func<T> action)
    {
        var was = MatchEngine.ReferenceJsonCopyForTests;
        MatchEngine.ReferenceJsonCopyForTests = true;
        try { return action(); }
        finally { MatchEngine.ReferenceJsonCopyForTests = was; }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static void Lockstep(Func<MatchEngine> create, ulong seed, int maxCommands, string label, Action<MatchEngine, int>? each = null)
    {
        var reference = Reference(create);
        var fast = create();
        var rng = new Random((int)seed);
        var n = 0;
        while (Reference(() => reference.GetView(0)).WinnerId is null && n < maxCommands)
        {
            var where = $"{label}, step {n}";
            var state = Reference(() => reference.State);
            Same(state, where);
            if (state.LastDuel is { } duel) Same(duel, $"{where}, LastDuel");
            if (Json(fast.State) != Json(state)) throw new Exception($"The engine on the fast copy holds a different state from the JSON-copy engine ({where}).");
            for (var seat = 0; seat < 2; seat++)
                if (Json(fast.GetView(seat)) != Json(Reference(() => reference.GetView(seat)))) throw new Exception($"Seat {seat}'s view differs between the fast-copy and JSON-copy engines ({where}).");
            each?.Invoke(fast, n);
            var waiting = state.PendingChoice is { } pending ? pending.ActingPlayerId ?? pending.PlayerId : state.PriorityPlayerId;
            var all = Reference(() => reference.QueryActions(waiting));
            if (Json(fast.QueryActions(waiting)) != Json(all)) throw new Exception($"The fast-copy engine offers different actions ({where}).");
            var options = all.Where(a => a.Enabled && a.Command is not null).ToList();
            if (options.Count == 0) break;
            var active = options.Where(o => o.Type != "concede" && o.Type != "pass").ToList();
            var pool = active.Count > 0 && rng.Next(4) > 0 ? active : options.Where(o => o.Type != "concede").DefaultIfEmpty(options[0]).ToList();
            var choice = pool[rng.Next(pool.Count)];
            Same(choice.Command!, $"{where}, command {choice.Type}");
            var expected = Reference(() => reference.Execute(choice.Command!));
            if (Json(fast.Execute(choice.Command!)) != Json(expected)) throw new Exception($"Execute returned a different result on the fast copy ({where}, {choice.Type}).");
            if (!expected.Accepted) break;
            n++;
        }
    }

    private static void Same<T>(T value, string where)
    {
        var fast = StateCloner.Clone(value);
        var json = MatchEngine.CopyViaJson(value);
        if (Json(fast) != Json(json)) throw new Exception($"Copy serialises differently from the JSON round trip ({where}).");
        var diff = DeepDifference(fast, json, typeof(T).Name, 0);
        if (diff is not null) throw new Exception($"Copy differs from the JSON round trip at {diff} ({where}).");
    }

    private static IEnumerable<(string Name, Func<MatchEngine> Create)> FixtureGames()
    {
        var factories = typeof(MatchEngine).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(MatchEngine) && m.Name.StartsWith("Create", StringComparison.Ordinal) && m.GetParameters().All(p => p.HasDefaultValue))
            .OrderBy(m => m.Name, StringComparer.Ordinal);
        foreach (var f in factories) yield return (f.Name, () => (MatchEngine)f.Invoke(null, f.GetParameters().Select(p => p.DefaultValue).ToArray())!);
        foreach (var kind in new[] { "steel", "honor", "focus" })
        {
            try { MatchEngine.CreateDuelDemo(kind); }
            catch (ArgumentException) { continue; }
            yield return ($"CreateDuelDemo({kind})", () => MatchEngine.CreateDuelDemo(kind));
        }
    }

    private static void Fixtures()
    {
        var count = 0;
        foreach (var (name, create) in FixtureGames()) { Lockstep(create, 5, 60, name); count++; }
        if (count < 8) throw new Exception($"Only {count} fixture scenarios were exercised.");
    }

    private static void Definitions()
    {
        foreach (var def in CardCatalog.Cards.Values) Same(def, $"card {def.Id}");
    }

    private static void Independence()
    {
        var checkedStates = 0;
        void Check(MatchEngine game, int step)
        {
            if (step % 40 != 0) return;
            var original = game.State;
            var before = Json(original);
            var copy = StateCloner.Clone(original);
            var shared = SharedReference(original, copy, "MatchState", 0);
            if (shared is not null) throw new Exception($"The copy shares a mutable object with the original at {shared} (step {step}).");
            var mutated = Mutate(copy, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (mutated < 100) throw new Exception($"The mutation walk changed only {mutated} members: it is not reaching the state.");
            if (Json(original) != before) throw new Exception($"Mutating the copy changed the original (step {step}).");
            if (Json(copy) == before) throw new Exception("The mutation walk left the copy unchanged.");
            checkedStates++;
        }
        Lockstep(() => MatchEngine.CreatePublishedMatch(23), 23, 400, "independence", Check);
        foreach (var (_, create) in FixtureGames().Take(4)) Check(create(), 0);
        if (checkedStates < 5) throw new Exception($"Only {checkedStates} states were checked.");
    }

    private static void ReplayMatchesLive()
    {
        foreach (var seed in new ulong[] { 11, 59 })
        {
            Lockstep(() => MatchEngine.CreatePublishedMatch(seed), seed, 250, $"replay seed {seed}", (g, step) =>
            {
                if (step % 50 != 49) return;
                var record = g.ExportReplay();
                if (Json(record) != Json(Reference(g.ExportReplay))) throw new Exception($"The exported replay differs from the JSON-copy export (seed {seed}, step {step}).");
                var replay = MatchEngine.Replay(record);
                if (Json(replay.State) != Json(g.State)) throw new Exception($"Replayed state differs from the live game (seed {seed}, step {step}).");
                if (Json(replay.State) != Json(Reference(() => MatchEngine.Replay(record).State))) throw new Exception($"Replay differs from a replay on the JSON copy (seed {seed}, step {step}).");
                var rebuilt = MatchEngine.FromTestState(g.State); // its view names a test scenario, so compare its state and offered actions
                if (Json(rebuilt.State) != Json(g.State)) throw new Exception($"An engine built from the copied State holds a different state (seed {seed}, step {step}).");
                for (var seat = 0; seat < 2; seat++)
                {
                    if (Json(replay.GetView(seat)) != Json(g.GetView(seat))) throw new Exception($"Replay view differs for seat {seat} (seed {seed}, step {step}).");
                    if (Json(rebuilt.QueryActions(seat)) != Json(g.QueryActions(seat))) throw new Exception($"An engine built from the copied State offers different actions to seat {seat} (seed {seed}, step {step}).");
                }
            });
        }
    }

    private static bool Immutable(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(TimeSpan);

    private static IEnumerable<FieldInfo> Fields(Type t)
    {
        for (var c = t; c is not null && c != typeof(object); c = c.BaseType)
            foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) yield return f;
    }

    private static bool Framework(Type t) => t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true;

    // Member-for-member comparison: runtime types, every instance field (private and compiler-generated included), and for framework collections
    // their elements in enumeration order and their comparer. Returns the path of the first difference, or null.
    private static string? DeepDifference(object? a, object? b, string path, int depth)
    {
        if (a is null || b is null) return a is null && b is null ? null : path + " (null vs non-null)";
        if (depth > 200) return path + " (too deep)";
        var t = a.GetType();
        if (t != b.GetType()) return $"{path} (type {t.Name} vs {b.GetType().Name})";
        if (Immutable(t)) return a.Equals(b) ? null : $"{path} ({a} vs {b})";
        if (a is Delegate da) return da.Method == ((Delegate)b).Method ? null : path + " (delegate)";
        if (a is IEnumerable ea && (t.IsArray || Framework(t)))
        {
            foreach (var comparerName in new[] { "Comparer", "EqualityComparer" })
                if (t.GetProperty(comparerName, BindingFlags.Instance | BindingFlags.Public) is { GetMethod: not null } cp && cp.GetIndexParameters().Length == 0)
                {
                    var ca = cp.GetValue(a); var cb = cp.GetValue(b);
                    if (ca?.GetType() != cb?.GetType() || !Equals(ca, cb)) return path + " (comparer)";
                }
            var la = ea.Cast<object?>().ToList(); var lb = ((IEnumerable)b).Cast<object?>().ToList();
            if (la.Count != lb.Count) return $"{path} (count {la.Count} vs {lb.Count})";
            for (var i = 0; i < la.Count; i++)
                if (DeepDifference(la[i], lb[i], $"{path}[{i}]", depth + 1) is { } d) return d;
            return null;
        }
        if (Framework(t) && !t.IsGenericType) return Equals(a, b) ? null : $"{path} (framework value {t.Name})";
        foreach (var f in Fields(t))
            if (DeepDifference(f.GetValue(a), f.GetValue(b), $"{path}.{f.Name}", depth + 1) is { } d) return d;
        return null;
    }

    // Parallel walk: any mutable (non-immutable reference) object reachable from both at the same place must be two different objects.
    private static string? SharedReference(object? a, object? b, string path, int depth)
    {
        if (a is null || b is null || depth > 200) return null;
        var t = a.GetType();
        if (Immutable(t) || a is Delegate || t != b.GetType()) return null;
        if (!t.IsValueType && ReferenceEquals(a, b) && (t.IsArray || a is IEnumerable || !Framework(t))) return path;
        if (a is IEnumerable ea && (t.IsArray || Framework(t)))
        {
            var la = ea.Cast<object?>().ToList(); var lb = ((IEnumerable)b).Cast<object?>().ToList();
            for (var i = 0; i < Math.Min(la.Count, lb.Count); i++)
                if (SharedReference(la[i], lb[i], $"{path}[{i}]", depth + 1) is { } s) return s;
            return null;
        }
        if (Framework(t) && !t.IsGenericType) return null;
        foreach (var f in Fields(t))
            if (SharedReference(f.GetValue(a), f.GetValue(b), $"{path}.{f.Name}", depth + 1) is { } s) return s;
        return null;
    }

    // Mutates everything reachable from the value (children first), returns how many members it changed.
    private static int Mutate(object? value, HashSet<object> seen)
    {
        if (value is null) return 0;
        var t = value.GetType();
        if (Immutable(t) || value is Delegate || t.IsValueType || !seen.Add(value)) return 0;
        var changed = 0;
        switch (value)
        {
            case Array array:
                foreach (var item in array) changed += Mutate(item, seen);
                if (array.Length > 0 && array.Rank == 1) { array.SetValue(Changed(array.GetType().GetElementType()!, array.GetValue(0)), 0); changed++; }
                return changed;
            case IDictionary dictionary:
                foreach (var item in dictionary.Values) changed += Mutate(item, seen);
                if (dictionary.Count > 0) { dictionary.Clear(); changed++; }
                return changed;
            case IList list:
                foreach (var item in list) changed += Mutate(item, seen);
                if (list.Count > 0) { list.Clear(); changed++; }
                return changed;
            case IEnumerable enumerable when Framework(t):
                foreach (var item in enumerable.Cast<object?>().ToList()) changed += Mutate(item, seen);
                if (t.GetMethod("Clear", Type.EmptyTypes) is { } clear && enumerable.Cast<object?>().Any()) { clear.Invoke(value, null); changed++; }
                return changed;
        }
        if (Framework(t)) return 0;
        foreach (var f in Fields(t))
        {
            var current = f.GetValue(value);
            changed += Mutate(current, seen);
            f.SetValue(value, Changed(f.FieldType, current));
            changed++;
        }
        return changed;
    }

    private static object? Changed(Type type, object? current)
    {
        if (type == typeof(string)) return (current as string) + "~";
        if (type == typeof(bool)) return !(bool)current!;
        if (type == typeof(int)) return (int)current! + 1;
        if (type == typeof(long)) return (long)current! + 1;
        if (type == typeof(ulong)) return (ulong)current! + 1;
        if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt64(current) + 1);
        return type.IsValueType ? RuntimeHelpers.GetUninitializedObject(type) : null;
    }
}
