namespace L5R.Core;

// Module initializers register metadata only. The first reader seals the registry; startup
// sorts once, then every hot-path reader receives the same array, without reflection or LINQ.
internal sealed class OrderedHooks<T> where T : Delegate
{
    private readonly List<(string Id, int Priority, T Callback)> registrations = [];
    private readonly object gate = new();
    private T[]? snapshot;
    private IReadOnlyList<string>? names;
    internal IReadOnlyList<string> Names { get { _ = Items; return names!; } }
    internal void Register(string id, int priority, T callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(callback);
        lock (gate)
        {
            if (snapshot is not null) throw new InvalidOperationException($"Hook registry is already sealed: {id}");
            if (registrations.Exists(entry => entry.Id == id)) throw new InvalidOperationException($"Duplicate hook registration: {id}");
            registrations.Add((id, priority, callback));
        }
    }
    internal T[] Items
    {
        get
        {
            var ready = System.Threading.Volatile.Read(ref snapshot);
            if (ready is not null) return ready;
            lock (gate)
            {
                if (snapshot is not null) return snapshot;
                registrations.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : StringComparer.Ordinal.Compare(a.Id, b.Id));
                var result = new T[registrations.Count];
                for (var i = 0; i < result.Length; i++) result[i] = registrations[i].Callback;
                names = Array.AsReadOnly(registrations.Select(entry => entry.Id).ToArray());
                System.Threading.Volatile.Write(ref snapshot, result);
                return result;
            }
        }
    }
}

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<Dictionary<string, AbilityDefinition>, Dictionary<string, AbilityDefinition>>> Abilities = new();
    internal static readonly OrderedHooks<Func<IEnumerable<LookSpec>>> Looks = new();
    internal static readonly OrderedHooks<Func<IEnumerable<SearchSpec>>> Searches = new();
    internal static Dictionary<string, AbilityDefinition> ExtendAbilities(Dictionary<string, AbilityDefinition> abilities)
    {
        foreach (var extend in Abilities.Items) abilities = extend(abilities);
        return abilities;
    }
    internal static Dictionary<string, T> Catalogue<T>(IEnumerable<T> baseline, OrderedHooks<Func<IEnumerable<T>>> extensions, Func<T, string> id)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var entry in baseline) result.Add(id(entry), entry);
        foreach (var extension in extensions.Items)
            foreach (var entry in extension()) result.Add(id(entry), entry);
        return result;
    }
}
