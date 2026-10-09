namespace L5R.Core;

/// <summary>Copies the decks shipped with the game into a player's deck folder, once each.
/// A ledger beside the folder records every starter name already offered, so a deck the player later
/// deleted or edited is never restored or overwritten by a newer build, while decks added in a later
/// version still arrive. Player-made decks are never touched.</summary>
public static class StarterDecks
{
    /// <summary>Seeds the missing starter decks and returns the names that were newly written.</summary>
    public static string[] Seed(DeckFileStore store, string ledgerPath, IEnumerable<(string Name, string Text)> starters)
    {
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(ledgerPath)) foreach (var line in File.ReadAllLines(ledgerPath)) if (line.Trim().Length > 0) offered.Add(line.Trim());
        var written = new List<string>(); var changed = false;
        foreach (var (name, text) in starters)
        {
            if (offered.Contains(name)) continue;
            var target = store.PathFor(name);
            if (!File.Exists(target))
            {
                Directory.CreateDirectory(store.DirectoryPath);
                var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllText(temp, text); File.Move(temp, target, false); written.Add(name); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            offered.Add(name); changed = true;
        }
        if (changed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ledgerPath))!);
            File.WriteAllLines(ledgerPath, offered.Order(StringComparer.OrdinalIgnoreCase));
        }
        return [.. written];
    }
}
