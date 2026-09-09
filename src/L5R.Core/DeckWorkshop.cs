namespace L5R.Core;

/// <summary>Catalogue facts supplied by the content adapter. No card effects are created here.</summary>
public sealed record DeckCardInfo(string Id, string Name, string[] Types, string[] Families, string[] Keywords, bool Implemented)
{
    public string SetupSlot => Types.Contains("Stronghold") ? "Stronghold" :
        Name == "Border Keep" || Name.StartsWith("Border Keep - ", StringComparison.Ordinal) ? "Border Keep" :
        Name == "Bamboo Harvesters" || Name.StartsWith("Bamboo Harvesters - ", StringComparison.Ordinal) ? "Bamboo Harvesters" : "";
}

public sealed record DeckReview(int Dynasty, int Fate, string[] Warnings, string[] OverLimit, int Unimplemented)
{
    // These are basic construction/implementation checks, not historical tournament certification.
    public bool ChecksPassed => Warnings.Length == 0;
}

/// <summary>Editable draft using the existing name-based text format. Rules checks stay in Core.</summary>
public sealed class DeckWorkshop
{
    public static readonly string[] SetupSlots = ["Stronghold", "Border Keep", "Bamboo Harvesters"];
    private readonly Dictionary<string, DeckCardInfo> _pool;
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    public string Legality { get; private set; } = "Emperor";
    public DeckWorkshop(IEnumerable<DeckCardInfo> pool) => _pool = pool.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    public int Count(string name) => _counts.GetValueOrDefault(name);
    public DeckCardInfo[] Cards => _counts.Where(p => p.Value > 0).Select(p => _pool[p.Key]).ToArray();
    public void Adjust(string name, int delta)
    {
        var card = _pool[name];
        if (card.SetupSlot.Length > 0) throw new InvalidOperationException("Use Select for setup cards.");
        var count = Math.Clamp((long)Count(name) + delta, 0, 999);
        if (count == 0) _counts.Remove(name); else _counts[name] = (int)count;
    }
    public void SelectSetup(string name)
    {
        var card = _pool[name];
        if (card.SetupSlot.Length == 0) throw new InvalidOperationException("This is not a setup card.");
        foreach (var old in Cards.Where(c => c.SetupSlot == card.SetupSlot).ToArray()) _counts.Remove(old.Name);
        _counts[card.Name] = 1;
    }
    public Deck ToDeck() => new(Legality, Cards.OrderBy(c => c.SetupSlot.Length == 0 ? 1 : 0)
        .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c => new DeckEntry(c.Name, Count(c.Name))).ToArray());
    public void Load(string text)
    {
        if (text.Length > 1_000_000) throw new FormatException("Deck file exceeds the 1 MB limit.");
        var deck = DeckFormat.Parse(text);
        if (!string.Equals(deck.Legality, "Emperor", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("This builder supports Legality: Emperor. The open draft has not changed.");
        var incoming = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in deck.Entries)
        {
            if (!_pool.TryGetValue(entry.Name, out var card)) throw new FormatException($"Unknown card: {entry.Name}. Use its catalogue name (including any Experienced suffix).");
            var sum = (long)incoming.GetValueOrDefault(card.Name) + entry.Count;
            if (entry.Count <= 0 || sum > 999) throw new FormatException($"Invalid quantity for {card.Name}; supported file quantities are 1–999.");
            incoming[card.Name] = (int)sum;
        }
        // Commit only after the entire file resolves, so a bad import cannot erase a draft.
        _counts.Clear(); foreach (var pair in incoming) _counts.Add(pair.Key, pair.Value);
        Legality = "Emperor";
    }
    public DeckReview Review()
    {
        var warnings = new List<string>();
        foreach (var slot in SetupSlots)
            if (Cards.Where(c => c.SetupSlot == slot).Sum(c => Count(c.Name)) != 1) warnings.Add($"Select exactly one {slot} version.");
        int Total(string family) => Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains(family)).Sum(c => Count(c.Name));
        var dynasty = Total("Dynasty"); var fate = Total("Fate");
        if (dynasty < 40) warnings.Add($"Dynasty needs {40 - dynasty} more cards (40 minimum).");
        if (fate < 40) warnings.Add($"Fate needs {40 - fate} more cards (40 minimum).");
        var limits = DeckFormat.CheckLegality(ToDeck(), name => _pool[name].Keywords);
        foreach (var v in limits.Violations) warnings.Add($"{v.CardName}: {v.Count} exceeds the usual {v.Limit} limit. Card-specific exceptions require review.");
        var unsupported = Cards.Count(c => !c.Implemented);
        if (unsupported > 0) warnings.Add($"{unsupported} selected card types are not yet implemented; this draft cannot be treated as playable.");
        return new(dynasty, fate, warnings.ToArray(), limits.Violations.Select(v => v.CardName).ToArray(), unsupported);
    }
}

/// <summary>Local plain-text deck storage, usable by desktop clients and tested without Godot.</summary>
public sealed class DeckFileStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string[] Files() => Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "*.txt").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : [];
    public string PathFor(string name)
    {
        var clean = name.Trim();
        if (clean.Length is < 1 or > 80 || clean.Any(c => !char.IsLetterOrDigit(c) && c is not (' ' or '-' or '_' or '—')))
            throw new ArgumentException("Use a deck name of 1–80 letters, numbers, spaces, dashes or underscores.");
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(clean, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Choose another deck name; this one is reserved by Windows.");
        return Path.Combine(DirectoryPath, clean + ".txt");
    }
    public static string Read(string path)
    {
        if (new FileInfo(path).Length > 1_000_000) throw new FormatException("Deck file exceeds the 1 MB limit.");
        return File.ReadAllText(path);
    }
    public static string Fingerprint(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Read(path))));
    public string Save(string name, Deck deck, bool overwrite = false)
    {
        var target = PathFor(name); Directory.CreateDirectory(DirectoryPath);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, DeckFormat.Write(deck));
            File.Move(temp, target, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return target;
    }
}
