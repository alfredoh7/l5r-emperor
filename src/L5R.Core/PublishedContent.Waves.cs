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
        const string prefix = "L5R.Published.";
        var assembly = typeof(PublishedContent).Assembly;
        var pattern = new Regex(@"^([a-z]+)-wave-([1-9][0-9]*)$", RegexOptions.CultureInvariant);
        var resources = new Dictionary<string, (string Resource, string Type, int Number)>(StringComparer.Ordinal);
        foreach(var resource in assembly.GetManifestResourceNames())
        {
            if(!resource.StartsWith(prefix, StringComparison.Ordinal) || !resource.Contains("-wave-", StringComparison.Ordinal)) continue;
            var profile = resource[prefix.Length..^5];
            var match = pattern.Match(profile);
            if(!resource.EndsWith(".json", StringComparison.Ordinal) || !match.Success)
                throw new InvalidOperationException($"Invalid published wave resource: {resource}");
            resources.Add(profile, (resource, match.Groups[1].Value, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
        }

        using var stream = assembly.GetManifestResourceStream("L5R.WaveOrder.txt")
            ?? throw new InvalidOperationException("Frozen published wave order is missing.");
        using var reader = new StreamReader(stream);
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while(reader.ReadLine() is { } line)
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
        return Array.AsReadOnly(ordered.Select(profile => Load(resources[profile].Resource, profile, requireDecks: false)).ToArray());
    }
}
