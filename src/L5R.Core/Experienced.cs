namespace L5R.Core;

/// <summary>Rules shared by deck construction and match play for the levelled Experienced keyword.</summary>
public static class Experienced
{
    /// <summary>Returns zero without Experienced, one for Experienced, and the printed number for
    /// Experienced N. If more than one form is present, the highest level applies.</summary>
    public static int Level(IEnumerable<string>? keywords)
    {
        var level = 0;
        foreach (var raw in keywords ?? [])
        {
            var keyword = raw.Trim();
            if (keyword.Equals("Experienced", StringComparison.OrdinalIgnoreCase)) level = Math.Max(level, 1);
            else if (keyword.StartsWith("Experienced ", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(keyword[12..].Trim(), out var parsed) && parsed > 0)
                level = Math.Max(level, parsed);
        }
        return level;
    }

    /// <summary>Returns the shared in-game title after removing the version notation used by the
    /// imported catalogue and deck lists. Experience versions remain distinct construction cards,
    /// but Overlay compares this title.</summary>
    public static string BaseTitle(string title)
    {
        var trimmed = title.Trim();
        var legacy = trimmed.LastIndexOf(" - exp", StringComparison.OrdinalIgnoreCase);
        if (legacy >= 0 && trimmed[(legacy + 6)..].All(char.IsDigit)) return trimmed[..legacy].TrimEnd();

        var experienced = trimmed.LastIndexOf(" Experienced", StringComparison.OrdinalIgnoreCase);
        if (experienced >= 0)
        {
            var suffix = trimmed[(experienced + 12)..].Trim();
            if (suffix.Length == 0 || suffix.All(char.IsDigit)) return trimmed[..experienced].TrimEnd();
        }
        return trimmed;
    }

    /// <summary>Construction identity distinguishes otherwise same-titled cards by level. This also
    /// applies to non-Personalities, whose Experienced keyword has no in-match effect.</summary>
    public static string DeckName(string title, IEnumerable<string>? keywords)
    {
        var level = Level(keywords);
        // Imported legacy lists already use "- exp"/"- expN" to distinguish versions. Keep that
        // stable instead of appending a second qualifier while still canonicalizing raw titles.
        var legacy = title.LastIndexOf(" - exp", StringComparison.OrdinalIgnoreCase);
        if (legacy >= 0 && title[(legacy + 6)..].All(char.IsDigit)) return title;
        if (level == 1 && title.EndsWith(" Experienced", StringComparison.OrdinalIgnoreCase)) return title;
        if (level > 1 && title.EndsWith($" Experienced {level}", StringComparison.OrdinalIgnoreCase)) return title;
        return level switch
        {
            0 => title,
            1 => $"{title} Experienced",
            _ => $"{title} Experienced {level}"
        };
    }
}
