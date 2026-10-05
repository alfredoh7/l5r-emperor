using System.Text.RegularExpressions;
namespace L5R.Client;

/// <summary>Presentation of an engine action label in the Battle pane: the performing Personality's title, then the action text, never a card id.</summary>
public static class BattleActionText
{
    private static readonly Regex CardId = new(@"\s*\((?:card|created)-[\w-]+\)", RegexOptions.CultureInvariant);
    /// <summary>The label without any engine card identifier.</summary>
    public static string WithoutIds(string label) => CardId.Replace(label ?? "", "").Trim();
    /// <summary>"Ability text · Card Title" becomes "Card Title · Ability text": the card that may be chosen is always named first. Unchanged when no known card name ends the label.</summary>
    public static string TitleFirst(string label, IEnumerable<string> cardNames)
    {
        var text = WithoutIds(label);
        var at = text.LastIndexOf(" · ", StringComparison.Ordinal);
        if (at <= 0) return text;
        var tail = text[(at + 3)..];
        return cardNames.Contains(tail, StringComparer.Ordinal) ? tail + " · " + text[..at] : text;
    }
    /// <summary>Title and text for one action. A performing Personality becomes the title and leaves the text; otherwise the source card is the title.</summary>
    public static (string? Title, string Text) Present(string label, string? performerName, string? sourceName)
    {
        var text = WithoutIds(label);
        if (performerName is null) return (sourceName, text);
        var suffix = " · " + performerName;
        var at = text.IndexOf(suffix, StringComparison.Ordinal);
        if (at >= 0) text = text.Remove(at, suffix.Length);
        return (performerName, text);
    }
}
