using L5R.Client;

// Issue #905: the local crash report holds what a bug report needs and nothing hidden.
internal static class CrashBundleTests
{
    public static void Run()
    {
        var token = "0123456789abcdef0123456789abcdef";
        var home = "/Users/someone";
        var text = CrashBundle.Compose("emperor-test", "macOS 15, Godot 4.7", new InvalidOperationException($"boom at {home}/L5R/file.cs with {token}"),
            Enumerable.Range(1, 200).Select(i => $"ERROR line {i}"), "phase Battle, turn 3, version 42",
            Enumerable.Range(1, 80).Select(i => $"#{i} event {i}"), [token, "", "short"], home);
        Check(text.Contains("Content version: emperor-test") && text.Contains("macOS 15, Godot 4.7"), "versions and environment are in the report");
        Check(text.Contains("boom at ~/L5R/file.cs"), "the home directory is replaced by ~");
        Check(!text.Contains(token) && text.Contains("[removed]"), "the seat token is scrubbed");
        Check(text.Contains("ERROR line 200") && !text.Contains("ERROR line 140\n") && text.Split('\n').Count(l => l.StartsWith("ERROR line", StringComparison.Ordinal)) == CrashBundle.MaxErrorLines,
            "only the last errors are kept, newest last");
        Check(text.Contains("#80 event 80") && !text.Contains("#50 event 50\n") && text.Split('\n').Count(l => l.StartsWith('#')) == CrashBundle.MaxEventLines, "only the last public events are kept");
        Check(text.Contains("phase Battle, turn 3, version 42"), "the viewer's position is there");
        Check(!text.Contains("seed", StringComparison.OrdinalIgnoreCase) && !text.Contains("deck order", StringComparison.OrdinalIgnoreCase), "nothing hidden is added by the composer itself");

        var bare = CrashBundle.Compose("v", "env", null, [], null, null);
        Check(bare.Contains("(none: written on request)") && !bare.Contains("Game position"), "a report without a match or an exception is still well formed");

        Check(CrashBundle.Truncate(new string('x', CrashBundle.MaxCharacters + 500)).Length < CrashBundle.MaxCharacters + 40, "a huge report is cut");
        Check(CrashBundle.Scrub("abc short abc", ["abc"], "/x") == "abc short abc", "a secret shorter than 8 characters is ignored, so a stray character cannot blank the report");
        Check(CrashBundle.FileName(new DateTimeOffset(2026, 10, 13, 4, 5, 6, TimeSpan.Zero)) == "crash-20261013-040506.txt", "file names sort by time");
        Console.WriteLine("  crash bundle: composition, scrubbing and truncation verified");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
