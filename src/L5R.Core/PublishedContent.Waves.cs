using System.Globalization;
using System.Text.RegularExpressions;

namespace L5R.Core;

public static partial class PublishedContent
{
    // Lazy evaluation happens after the legacy properties and their validators initialize.
    private static readonly Lazy<IReadOnlyList<PublishedSnapshot>> DiscoveredWaves = new(LoadWaves);

    /// <summary>All embedded waves, in catalogue order. Adding a wave needs no C# registration.</summary>
    public static IReadOnlyList<PublishedSnapshot> Waves => DiscoveredWaves.Value;

    private static IReadOnlyList<PublishedSnapshot> LoadWaves()
    {
        var assembly = typeof(PublishedContent).Assembly;
        using var stream = assembly.GetManifestResourceStream("L5R.WaveOrder.txt")
            ?? throw new InvalidOperationException("Frozen published wave order is missing.");
        using var reader = new StreamReader(stream);
        var ordered = OrderWaveProfiles(assembly.GetManifestResourceNames(), reader.ReadToEnd().Split('\n'));
        return Array.AsReadOnly(ordered.Select(profile => Load($"L5R.Published.{profile}.json", profile, requireDecks: false)).ToArray());
    }

    // The startup loader and focused failure tests share the same parsing/ordering logic.
    internal static string[] OrderWaveProfiles(IEnumerable<string> resourceNames, IEnumerable<string> legacyOrder)
    {
        const string prefix = "L5R.Published.";
        var pattern = new Regex(@"^([a-z]+)-wave-([1-9][0-9]*)$", RegexOptions.CultureInvariant);
        var resources = new Dictionary<string, (string Type, int Number)>(StringComparer.Ordinal);
        foreach(var resource in resourceNames)
        {
            if(!resource.StartsWith(prefix, StringComparison.Ordinal) || !resource.Contains("-wave-", StringComparison.Ordinal)) continue;
            if(!resource.EndsWith(".json", StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid published wave resource: {resource}");
            var profile = resource[prefix.Length..^5];
            var match = pattern.Match(profile);
            if(!match.Success || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new InvalidOperationException($"Invalid published wave resource: {resource}");
            resources.Add(profile, (match.Groups[1].Value, number));
        }

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var line in legacyOrder)
        {
            var profile = line.Trim();
            if(profile.Length == 0 || profile.StartsWith('#')) continue;
            if(!seen.Add(profile) || !resources.ContainsKey(profile))
                throw new InvalidOperationException($"Duplicate or missing legacy published wave: {profile}");
            ordered.Add(profile);
        }
        ordered.AddRange(resources.Where(pair => !seen.Contains(pair.Key))
            .OrderBy(pair => pair.Value.Type, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value.Number)
            .Select(pair => pair.Key));
        return ordered.ToArray();
    }
}
