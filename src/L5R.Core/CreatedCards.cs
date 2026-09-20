using System.Collections.Concurrent;
using System.Text;

namespace L5R.Core;

/// <summary>What an ability's printed text says the card it creates is; an omitted stat is not compared. Item Force/Chi are the printed "+2F/+0C" bonuses, stored unsigned.
/// Keywords is the exact set (order irrelevant); AbilityText is compared with the scan's text ignoring case and punctuation; ProxyId names the scan when the rest fits several.</summary>
public sealed record CreatedCardSpec(CardType Type, int? Force = null, int? Chi = null, int? PersonalHonor = null, string[]? Keywords = null,
    string? AbilityText = null, string? ProxyId = null)
{
    public string Describe()
    {
        var text = new StringBuilder(Type.ToString());
        if (Force is not null || Chi is not null || PersonalHonor is not null)
            text.Append(' ').Append(string.Join('/', new[] { Force is int f ? $"{f}F" : null, Chi is int c ? $"{c}C" : null, PersonalHonor is int p ? $"{p}PH" : null }.Where(s => s is not null)));
        if (Keywords is not null) text.Append(" [").Append(string.Join(", ", Keywords)).Append(']');
        if (AbilityText is not null) text.Append(" with \"").Append(AbilityText).Append('"');
        if (ProxyId is not null) text.Append(" (scan ").Append(ProxyId).Append(')');
        return text.ToString();
    }
}

/// <summary>A spec that no scan, or more than one scan, fits. Candidates are the scans that matched or, when none did, the scans of the same type.</summary>
public sealed class CreatedCardResolutionException(CreatedCardSpec spec, string reason, CardDefinition[] candidates)
    : InvalidOperationException($"Created card {spec.Describe()}: {reason}")
{
    public CreatedCardSpec Spec { get; } = spec;
    public CardDefinition[] Candidates { get; } = candidates;
}

/// <summary>Finds the Proxy scan (a published CardDefinition with CreatedOnly) that a "create a card" ability's spec describes.</summary>
public static class CreatedCards
{
    private static readonly ConcurrentDictionary<CreatedCardSpec, CardDefinition> Resolved = new(ReferenceEqualityComparer.Instance);

    public static IEnumerable<CardDefinition> Scans => CardCatalog.Cards.Values.Where(c => c.CreatedOnly).OrderBy(c => c.Id, StringComparer.Ordinal);

    public static bool Creatable(CardType type) => type is CardType.Personality or CardType.Item or CardType.Follower or CardType.Spell;

    public static bool IsAttachment(CardType type) => type is CardType.Item or CardType.Follower or CardType.Spell;

    public static CardDefinition Resolve(CreatedCardSpec spec) => Resolved.TryGetValue(spec, out var known) ? known : Resolved[spec] = Resolve(spec, Scans);

    internal static CardDefinition Resolve(CreatedCardSpec spec, IEnumerable<CardDefinition> scans)
    {
        var all = scans.Where(c => c.CreatedOnly).ToArray();
        var pool = all;
        if (spec.ProxyId is string named)
        {
            pool = all.Where(c => c.Id == named).ToArray();
            if (pool.Length == 0) throw new CreatedCardResolutionException(spec, $"{named} is not a created-only card in the catalogue", []);
        }
        var matches = pool.Where(c => Fits(spec, c)).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new CreatedCardResolutionException(spec, $"{matches.Length} scans fit it ({string.Join(", ", matches.Select(c => $"{c.Name} {c.Id}"))}); name one with ProxyId", matches);
        throw new CreatedCardResolutionException(spec, spec.ProxyId is null ? "no created-only scan fits it" : $"{spec.ProxyId} does not fit it",
            spec.ProxyId is null ? all.Where(c => c.Type == spec.Type).ToArray() : pool);
    }

    private static bool Fits(CreatedCardSpec spec, CardDefinition scan) =>
        scan.Type == spec.Type
        && (spec.Force is null || scan.Force == spec.Force) && (spec.Chi is null || scan.Chi == spec.Chi) && (spec.PersonalHonor is null || scan.PersonalHonor == spec.PersonalHonor)
        && (spec.Keywords is null || new HashSet<string>(scan.Keywords ?? [], StringComparer.OrdinalIgnoreCase).SetEquals(spec.Keywords))
        && (spec.AbilityText is null || Normalize(scan.Text).Contains(Normalize(spec.AbilityText), StringComparison.Ordinal));

    private static string Normalize(string text)
    {
        var words = new StringBuilder();
        foreach (var c in text) words.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        return string.Join(' ', words.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static IEnumerable<(string AbilityId, CreatedCardSpec Spec)> Creators =>
        AbilityCatalog.All.Where(p => p.Value.Creates is not null).Select(p => (p.Key, p.Value.Creates!));

    /// <summary>One line per creator whose spec cannot be resolved (no scan, several scans, a kind the engine cannot create yet); empty when every creator has its scan.</summary>
    public static string[] Verify() => Verify(Creators, Scans);

    internal static string[] Verify(IEnumerable<(string AbilityId, CreatedCardSpec Spec)> creators, IEnumerable<CardDefinition> scans)
    {
        var problems = new List<string>();
        var all = scans.ToArray();
        foreach (var (abilityId, spec) in creators)
        {
            try
            {
                var scan = Resolve(spec, all);
                if (!Creatable(scan.Type)) problems.Add($"{abilityId}: {scan.Name} ({scan.Id}) is a {scan.Type}, which the engine cannot create yet");
            }
            catch (CreatedCardResolutionException e) { problems.Add($"{abilityId}: {e.Message}"); }
        }
        return problems.ToArray();
    }
}
