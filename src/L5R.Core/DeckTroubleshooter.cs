using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace L5R.Core;

/// <summary>A catalogue card offered as the likely intended match for an unreadable deck line. Score is 0..1.</summary>
public sealed record DeckSuggestion(string DeckName, double Score);

/// <summary>A deck-list line whose card name is not in the catalogue, with the nearest catalogue matches (best first).</summary>
public sealed record DeckProblem(int LineNumber, string Text, int Count, DeckSuggestion[] Suggestions);

/// <summary>Result of reading a deck list leniently. Nothing is loaded here: DeckWorkshop.Load stays strict and atomic,
/// and the repaired text from <see cref="Rebuild"/> goes through it like any other deck file.</summary>
public sealed class DeckTroubleshooting
{
    internal sealed record Line(string Raw, int Count, string Name, DeckProblem? Problem);
    private readonly List<Line> _lines;
    internal DeckTroubleshooting(string? legality, List<Line> lines) { Legality = legality; _lines = lines; }
    /// <summary>The list's own `Legality:` value, or null when the header is missing.</summary>
    public string? Legality { get; }
    public DeckProblem[] Problems => _lines.Where(l => l.Problem is not null).Select(l => l.Problem!).ToArray();
    public int RecognisedLines => _lines.Count(l => l.Problem is null);
    /// <summary>Whether the header would be rewritten (missing, or a legality this builder cannot load).</summary>
    public bool LegalityNeedsFixing => !string.Equals(Legality, "Emperor", StringComparison.OrdinalIgnoreCase);

    /// <summary>Deck text with every problem line replaced by <paramref name="choose"/>'s card name, or dropped when it returns null.
    /// Recognised lines are rewritten to their catalogue names unchanged otherwise. The header is always `Legality: Emperor`.</summary>
    public string Rebuild(Func<DeckProblem, string?> choose)
    {
        var text = new StringBuilder("Legality: Emperor\n\n");
        foreach (var line in _lines)
        {
            var name = line.Problem is null ? line.Name : choose(line.Problem);
            if (name is null) continue;
            text.Append(line.Count == 1 ? name : $"{line.Count} {name}").Append('\n');
        }
        return text.ToString();
    }
}

public static class DeckTroubleshooter
{
    private const double MinimumScore = 0.45;
    private const int MaxSuggestions = 5;
    private static readonly Regex CountPrefix = new(@"^(\d+)\s*x?\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // "Taishuu - exp", "Fukuzo - expFukuzo", "Daigotsu Aya - expExperienced", "Kanpeki - exp2", "Aya Experienced 2"
    private static readonly Regex ExperienceNote = new(@"\s*(?:-\s*exp(?:erienced)?|\bexperienced)\s*(\d*)\s*[A-Za-z]*\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Normalise(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var clean = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            clean.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', clean.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static (string Title, int Level) SplitExperience(string name)
    {
        var m = ExperienceNote.Match(name);
        if (!m.Success || m.Index == 0) return (name.Trim(), 0);
        return (name[..m.Index].Trim(), m.Groups[1].Length > 0 && int.TryParse(m.Groups[1].Value, out var n) ? n : 1);
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0]; row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }

    private static double Similarity(string query, string candidate)
    {
        if (query.Length == 0 || candidate.Length == 0) return 0;
        if (query == candidate) return 1;
        var edit = 1.0 - (double)Distance(query, candidate) / Math.Max(query.Length, candidate.Length);
        var q = query.Split(' '); var c = candidate.Split(' ');
        var shared = q.Count(t => c.Any(u => u == t || (t.Length > 3 && u.Length > 3 && Distance(t, u) <= 1)));
        var dice = 2.0 * shared / (q.Length + c.Length);
        // A query that is a whole-word part of the title (a surname, "Kanpeki") is a real clue, but weaker than a full match.
        var contained = (" " + candidate + " ").Contains(" " + query + " ") ? 0.6 + 0.3 * query.Length / candidate.Length : 0;
        return Math.Max(Math.Max(edit, dice), contained);
    }

    internal static DeckSuggestion[] Suggest(string entry, IEnumerable<DeckCardInfo> pool)
    {
        var (title, hint) = SplitExperience(entry);
        var normal = Normalise(title);
        var full = Normalise(entry);
        var best = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in pool)
        {
            var baseTitle = Normalise(Experienced.BaseTitle(card.Name));
            var score = Math.Max(Similarity(normal, baseTitle), Similarity(full, Normalise(card.DeckName)));
            // The list's own experience marker is a tie-breaker between versions of one title, never a reason to match a different card.
            if (hint > 0 && card.ExperienceLevel == hint) score += 0.04;
            else if (hint > 0 && card.ExperienceLevel > 0) score += 0.02;
            else if (hint == 0 && card.ExperienceLevel == 0) score += 0.02;
            if (score < MinimumScore) continue;
            if (!best.TryGetValue(card.DeckName, out var old) || score > old) best[card.DeckName] = score;
        }
        return best.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Take(MaxSuggestions)
            .Select(p => new DeckSuggestion(p.Key, Math.Round(Math.Min(1.0, p.Value), 3))).ToArray();
    }

    /// <summary>Read a deck list line by line without stopping at the first unknown card.</summary>
    public static DeckTroubleshooting Analyse(string text, IReadOnlyCollection<DeckCardInfo> pool)
    {
        var byName = pool.ToDictionary(c => c.DeckName, StringComparer.OrdinalIgnoreCase);
        string? legality = null;
        var lines = new List<DeckTroubleshooting.Line>();
        var number = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//")) continue;
            if (line.StartsWith("Legality:", StringComparison.OrdinalIgnoreCase)) { legality = line[(line.IndexOf(':') + 1)..].Trim(); continue; }
            var count = 1; var name = line;
            var m = CountPrefix.Match(line);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0) { count = n; name = m.Groups[2].Value.Trim(); }
            if (byName.TryGetValue(name, out var exact)) { lines.Add(new(raw, count, exact.DeckName, null)); continue; }
            var byTitle = byName.Values.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (byTitle.Length == 1) { lines.Add(new(raw, count, byTitle[0].DeckName, null)); continue; }
            lines.Add(new(raw, count, name, new DeckProblem(number, name, count, Suggest(name, byName.Values))));
        }
        return new(legality, lines);
    }
}
