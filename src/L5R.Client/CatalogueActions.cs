using System.Text.RegularExpressions;
namespace L5R.Client;

/// <summary>Browsing-only classification of the bold actions printed on a card. It never decides what a card can do in a match.</summary>
public static class CatalogueActions
{
    public const string None = "None";
    /// <summary>Filter choices in presentation order.</summary>
    public static readonly string[] Choices = ["Limited", "Open", "Battle", "Battle/Open", "Reaction", None];
    // A printed action starts a line, optionally preceded by trait words ("Political Open:", "Fear Battle,") and ends with ":" or ", cost".
    // Parenthesised reminder text, text that grants an ability to other cards, and Interrupts do not start a line this way.
    private static readonly Regex Printed = new(@"(?:^|\n)[ \t]*(?:[A-Z][\w'’\-]*[ \t]+){0,4}?(Battle/Open|Open/Battle|Battle/Limited|Limited/Battle|Open/Limited|Limited/Open|Battle|Open|Limited|Reaction)[ \t]*[,:]", RegexOptions.CultureInvariant);
    /// <summary>The printed action labels: Battle/Open only when printed together; otherwise each separate timing.</summary>
    public static string[] Printings(string oracleText)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Printed.Matches(oracleText ?? ""))
        {
            var timing = m.Groups[1].Value;
            if (timing is "Battle/Open" or "Open/Battle") found.Add("Battle/Open"); else foreach (var part in timing.Split('/')) found.Add(part);
        }
        return found.ToArray();
    }
    /// <summary>True when a card printing these actions matches one filter choice. Battle and Open include Battle/Open; Battle/Open matches only itself; None means no printed action.</summary>
    public static bool Matches(string[] printed, string choice) => choice switch
    {
        None => printed.Length == 0,
        "Battle" or "Open" => printed.Contains(choice) || printed.Contains("Battle/Open"),
        _ => printed.Contains(choice)
    };
    /// <summary>A card appears once however many selected choices it satisfies; an empty selection matches everything.</summary>
    public static bool MatchesAny(string[] printed, IReadOnlyCollection<string> selected) => selected.Count == 0 || selected.Any(choice => Matches(printed, choice));
}
