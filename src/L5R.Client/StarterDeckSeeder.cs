using Godot;
using L5R.Core;

/// <summary>Resolves the player's deck folder, first copying in the decks bundled under res://Content/StarterDecks.
/// The folder lives in user://, outside the game folder, so deleting and re-extracting a package keeps the player's decks.</summary>
public static class StarterDeckSeeder
{
    private const string BundledFolder = "res://Content/StarterDecks";
    private static bool _seeded;
    public static string UserDeckDirectory()
    {
        var directory = ProjectSettings.GlobalizePath("user://decks");
        if (_seeded) return directory;
        _seeded = true;
        try { SeedInto(directory, ProjectSettings.GlobalizePath("user://starter-decks.seen")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { GD.PushWarning("Starter decks were not added: " + e.Message); }
        return directory;
    }
    /// <summary>Seeds the bundled decks into any folder; returns the names newly written.</summary>
    public static string[] SeedInto(string directory, string ledgerPath)
    {
        var starters = new List<(string, string)>();
        foreach (var file in DirAccess.GetFilesAt(BundledFolder).Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            using var handle = Godot.FileAccess.Open($"{BundledFolder}/{file}", Godot.FileAccess.ModeFlags.Read);
            if (handle is not null) starters.Add((file[..^4], handle.GetAsText()));
        }
        return StarterDecks.Seed(new DeckFileStore(directory), ledgerPath, starters);
    }
}
