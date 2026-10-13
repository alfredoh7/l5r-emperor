using System.Text;

namespace L5R.Client;

/// <summary>
/// Issue #905: what a local crash report holds, kept free of Godot so it can be tested. The report is a plain text file written next to the game's own logs. Nothing is sent
/// anywhere; a player who wants a crash looked at attaches the file. It holds the exception, the last engine errors, the game position the viewer can see (phase, turn, version, the
/// public event log as that viewer saw it) and nothing hidden: no seed, no deck order, no opponent hand, and any seat token or home-directory path is scrubbed.
/// </summary>
public static class CrashBundle
{
    public const int MaxErrorLines = 60, MaxEventLines = 25, MaxCharacters = 64 * 1024;

    public static string FileName(DateTimeOffset when) => $"crash-{when.UtcDateTime:yyyyMMdd-HHmmss}.txt";

    public static string Compose(string contentVersion, string environment, Exception? exception, IEnumerable<string> recentErrors, string? position,
        IEnumerable<string>? viewerEvents, IEnumerable<string>? secrets = null, string? homeDirectory = null)
    {
        var text = new StringBuilder();
        text.AppendLine("L5R Emperor crash report (local file; nothing was sent)");
        text.AppendLine($"Content version: {contentVersion}");
        text.AppendLine($"Environment: {environment}");
        text.AppendLine();
        text.AppendLine("== Exception ==");
        text.AppendLine(exception is null ? "(none: written on request)" : exception.ToString());
        text.AppendLine();
        text.AppendLine($"== Last {MaxErrorLines} engine errors and warnings (oldest first) ==");
        foreach (var line in recentErrors.TakeLast(MaxErrorLines)) text.AppendLine(line);
        if (position is not null)
        {
            text.AppendLine();
            text.AppendLine("== Game position, as the local viewer saw it ==");
            text.AppendLine(position);
            if (viewerEvents is not null)
            {
                text.AppendLine($"== Last {MaxEventLines} public events ==");
                foreach (var line in viewerEvents.TakeLast(MaxEventLines)) text.AppendLine(line);
            }
        }
        return Scrub(Truncate(text.ToString()), secrets, homeDirectory);
    }

    public static string Truncate(string text) => text.Length <= MaxCharacters ? text : text[..MaxCharacters] + "\n... (truncated)\n";

    /// <summary>Removes each secret (a seat token) and replaces the home directory with "~". Empty or very short secrets are ignored so a stray character cannot blank the report.</summary>
    public static string Scrub(string text, IEnumerable<string>? secrets, string? homeDirectory)
    {
        foreach (var secret in secrets ?? [])
            if (!string.IsNullOrEmpty(secret) && secret.Length >= 8) text = text.Replace(secret, "[removed]", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(homeDirectory) && homeDirectory.Length > 3)
        {
            text = text.Replace(homeDirectory, "~", StringComparison.Ordinal);
            text = text.Replace(homeDirectory.Replace('\\', '/'), "~", StringComparison.Ordinal);
        }
        return text;
    }
}
