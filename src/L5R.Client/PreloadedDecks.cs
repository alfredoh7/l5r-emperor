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
    private sealed record Entry(string Name, string Text);

    public static void Seed(DeckFileStore store)
    {
        try
        {
            using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
            if (file is null) return;
            var decks = JsonSerializer.Deserialize<Entry[]>(file.GetAsText(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            store.Preload(decks.Select(d => (d.Name, d.Text)), System.IO.Path.Combine(System.IO.Path.GetDirectoryName(store.DirectoryPath.TrimEnd(System.IO.Path.DirectorySeparatorChar))!, "preloaded-decks.seen"), OriginallyShipped);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { GD.PushWarning("Preloaded decks could not be written: " + e.Message); }
    }
}
