using Godot;
using L5R.Core;
using System.Text.Json;

namespace L5R.Client;

/// <summary>The decks that ship with the game (Content/preloaded-decks.json): written into the player's My Decks when the deck folder is opened, once each: a new install has them on launch and an older install gains decks added later.</summary>
public static class PreloadedDecks
{
    private const string Path = "res://Content/preloaded-decks.json";
    /// <summary>The nine decks that shipped before the marker listed names; an older marker means exactly these were offered.</summary>
    private static readonly string[] OriginallyShipped = ["Crab Control", "Crane Dominance", "Dragon Destruction", "Mantis Manipulation", "Phoenix Talons", "Scorpion Lethargy", "Scottish Lion Deck", "Spider Conquer", "Unicorn Maidens"];
    private sealed record Entry(string Name, string Text, string? Seed = null);
    private static string? _directory;

    /// <summary>The decks that ship with the game, read from the read-only copy embedded in the package. The tutorial and Restore use these; the editable .txt files in the deck folder are only ever copies of them.</summary>
    public static (string Name, string Text)[] Embedded() => Entries().Select(d => (d.Name, d.Text)).ToArray();

    /// <summary>The embedded decks that are also written to the deck folder as editable .txt files. The others (the pinned Lion and Crane the tutorial and the pinned profile rely on) stay embedded only.</summary>
    public static (string Name, string Text)[] Shipped() => Entries().Where(d => d.Seed != "false").Select(d => (d.Name, d.Text)).ToArray();

    private static Entry[] Entries()
    {
        try
        {
            using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
            if (file is null) return [];
            return JsonSerializer.Deserialize<Entry[]>(file.GetAsText(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException) { GD.PushWarning("Embedded decks could not be read: " + e.Message); return []; }
    }

    /// <summary>The folder of editable practice decks: Documents\L5R Emperor\Decks (or L5R_DECKS_DIR), visible to the player and usable by other tools. Decks kept in the old hidden user://decks folder are copied across once (never overwritten);
    /// when Documents cannot be written the old folder stays in use.</summary>
    public static string DeckDirectory()
    {
        if (_directory is not null) return _directory;
        var legacy = ProjectSettings.GlobalizePath("user://decks");
        // L5R_DECKS_DIR puts the folder anywhere the player wants (a development checkout keeps it out of a synced Documents folder); nothing is copied into it.
        if (System.Environment.GetEnvironmentVariable("L5R_DECKS_DIR") is { Length: > 0 } custom)
        {
            try { Directory.CreateDirectory(custom); return _directory = System.IO.Path.GetFullPath(custom); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { GD.PushWarning("L5R_DECKS_DIR could not be used: " + e.Message); }
        }
        try
        {
            var documents = OS.GetSystemDir(OS.SystemDir.Documents);
            if (string.IsNullOrEmpty(documents)) return _directory = legacy;
            var target = System.IO.Path.Combine(documents, "L5R Emperor", "Decks");
            Directory.CreateDirectory(target);
            if (Directory.Exists(legacy) && !string.Equals(System.IO.Path.GetFullPath(legacy), System.IO.Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                foreach (var old in Directory.GetFiles(legacy, "*.txt")) { var copy = System.IO.Path.Combine(target, System.IO.Path.GetFileName(old)); if (!File.Exists(copy)) File.Copy(old, copy); }
            return _directory = target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { GD.PushWarning("The Documents deck folder could not be used: " + e.Message); return _directory = legacy; }
    }

    public static DeckFileStore OpenStore() { var store = new DeckFileStore(DeckDirectory()); Seed(store); return store; }

    /// <summary>The shipped decks whose .txt is missing or no longer matches the embedded original.</summary>
    public static string[] Restorable(DeckFileStore store) =>
        Shipped().Where(d => { var file = store.PathFor(d.Name); return !File.Exists(file) || File.ReadAllText(file) != d.Text; }).Select(d => d.Name).ToArray();

    /// <summary>Rewrites the named shipped decks from the embedded originals; returns how many were written.</summary>
    public static int Restore(DeckFileStore store, IEnumerable<string> names)
    {
        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase); var written = 0;
        Directory.CreateDirectory(store.DirectoryPath);
        foreach (var (name, text) in Shipped()) if (wanted.Contains(name)) { File.WriteAllText(store.PathFor(name), text); written++; }
        return written;
    }

    public static void Seed(DeckFileStore store)
    {
        try
        {
            store.Preload(Shipped(), ProjectSettings.GlobalizePath("user://preloaded-decks.seen"), OriginallyShipped);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { GD.PushWarning("Preloaded decks could not be written: " + e.Message); }
    }
}
