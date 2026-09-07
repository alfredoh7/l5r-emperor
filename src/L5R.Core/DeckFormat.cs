using System.Text;

namespace L5R.Core;

public sealed record DeckEntry(string Name, int Count);

/// <summary>A player-built deck list, resolved by card name rather than id so a deck shared
/// between two local installs stays loadable even if their content versions differ slightly.</summary>
public sealed record Deck(string Name, string Clan, string Stronghold, string BambooHarvesters,
    string BorderKeep, DeckEntry[] Dynasty, DeckEntry[] Fate);

public sealed record DeckLegalityViolation(string CardName, int Count, int Limit);
public sealed record DeckLegality(bool IsLegal, DeckLegalityViolation[] Violations);

/// <summary>Reads and writes the plain-text deck-list format: a small `Key: Value` header
/// followed by `## Dynasty Deck` / `## Fate Deck` sections of `COUNT CARD NAME` lines. Deliberately
/// human-writable and diffable so a deck can be hand-edited or dropped into another player's
/// local install. This class never resolves a card name against content — callers do that with
/// whatever card pool they have (e.g. the deck-builder's larger reference pool), which is why
/// CheckLegality takes a keyword lookup rather than loading one itself.</summary>
public static class DeckFormat
{
    public static Deck Parse(string text)
    {
        string? name = null, clan = null, stronghold = null, bamboo = null, borderKeep = null;
        var dynasty = new List<DeckEntry>();
        var fate = new List<DeckEntry>();
        List<DeckEntry>? current = null;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || (line.StartsWith('#') && !line.StartsWith("##"))) continue;
            if (line.StartsWith("##"))
            {
                var heading = line[2..].Trim();
                current = heading switch
                {
                    "Dynasty Deck" => dynasty,
                    "Fate Deck" => fate,
                    _ => throw new FormatException($"Unknown deck section: {heading}"),
                };
                continue;
            }
            if (current is null)
            {
                var colon = line.IndexOf(':');
                if (colon < 0) throw new FormatException($"Expected 'Key: Value' in the deck header: {line}");
                var key = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                switch (key)
                {
                    case "Name": name = value; break;
                    case "Clan": clan = value; break;
                    case "Stronghold": stronghold = value; break;
                    case "Bamboo Harvesters": bamboo = value; break;
                    case "Border Keep": borderKeep = value; break;
                    default: throw new FormatException($"Unknown deck header field: {key}");
                }
                continue;
            }
            var space = line.IndexOf(' ');
            if (space < 0 || !int.TryParse(line[..space], out var count) || count <= 0)
                throw new FormatException($"Expected 'COUNT CARD NAME': {line}");
            var cardName = line[(space + 1)..].Trim();
            if (cardName.Length == 0) throw new FormatException($"Missing card name: {line}");
            current.Add(new(cardName, count));
        }
        if (name is null || clan is null || stronghold is null)
            throw new FormatException("Deck header must include Name, Clan and Stronghold.");
        return new(name, clan, stronghold, bamboo ?? "Bamboo Harvesters", borderKeep ?? "Border Keep", dynasty.ToArray(), fate.ToArray());
    }

    public static string Write(Deck deck)
    {
        var text = new StringBuilder();
        text.AppendLine("# L5R Emperor Deck");
        text.AppendLine($"Name: {deck.Name}");
        text.AppendLine($"Clan: {deck.Clan}");
        text.AppendLine($"Stronghold: {deck.Stronghold}");
        text.AppendLine($"Bamboo Harvesters: {deck.BambooHarvesters}");
        text.AppendLine($"Border Keep: {deck.BorderKeep}");
        text.AppendLine();
        text.AppendLine("## Dynasty Deck");
        foreach (var entry in deck.Dynasty) text.AppendLine($"{entry.Count} {entry.Name}");
        text.AppendLine();
        text.AppendLine("## Fate Deck");
        foreach (var entry in deck.Fate) text.AppendLine($"{entry.Count} {entry.Name}");
        return text.ToString();
    }

    /// <summary>Soft check only: a deck may exceed these limits and still be built/saved/loaded
    /// (some Strongholds and cards are meant to break them; some players build illegal decks for
    /// fun). Stronghold/Bamboo Harvesters/Border Keep are never counted — callers pass a lookup for
    /// just the Dynasty/Fate entries actually in the deck.</summary>
    public static DeckLegality CheckLegality(Deck deck, Func<string, IReadOnlyList<string>> keywordsFor)
    {
        var violations = new List<DeckLegalityViolation>();
        foreach (var entry in deck.Dynasty.Concat(deck.Fate))
        {
            var limit = keywordsFor(entry.Name).Contains("Unique", StringComparer.Ordinal) ? 1 : 3;
            if (entry.Count > limit) violations.Add(new(entry.Name, entry.Count, limit));
        }
        return new(violations.Count == 0, violations.ToArray());
    }
}
