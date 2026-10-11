namespace L5R.Core;

/// <summary>Catalogue facts supplied by the content adapter. No card effects are created here.
/// CreatedOnly (issue #257) defaults false so existing callers built before it existed keep
/// compiling unchanged. A created-only card ("May not be included in decks.") never enters a DeckWorkshop's pool.</summary>
// CopyLimit (issue #337): the copy limit the card itself prints ("You may include up to 10 copies of this card in your deck" -- Fading
// Shadows), read from CardDefinition.DeckCopyLimit; null, like CreatedOnly, for a caller or a card that has none, which keeps the usual 3 (1 if Unique).
// FateMinimumReduction (issue #453 batch 5, Hato-gurama: "Your minimum Fate deck size is decreased by one"): how much each copy of the card in the deck lowers the 40-card Fate minimum,
// read from CardDefinition.Effect == "hato-gurama" (the card's one Effect id also carries its hand-discard restriction, CardAbilities.cs); 0 for every other card.
// StrategyCopyRule / BattleStrategy (Emperor Edition Stronghold pass, issue #486 batch 1, Eternal Victory Dojo: "Your deck may include four copies each of one or two non-Unique Strategies with Battle
// abilities"): a Stronghold carrying that deck-construction text, and a Strategy that has a Battle ability; both read from the card's definition (DeckWorkshop.StrategyCopyRuleOf / BattleStrategyOf).
public sealed record DeckCardInfo(string Id, string Name, string[] Types, string[] Families, string[] Keywords, bool Implemented, bool CreatedOnly = false, int? CopyLimit = null, int FateMinimumReduction = 0,
    bool StrategyCopyRule = false, bool BattleStrategy = false, bool CountsTowardMinimum = true)
{
    public int ExperienceLevel => Experienced.Level(Keywords);
    public string DeckName => Experienced.DeckName(Name, Keywords);
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
    /// <summary>Hato-gurama (issue #453 batch 5): the Fate-minimum reduction a card's printed trait gives, for a DeckCardInfo built from its definition.</summary>
    public static int FateMinimumReductionOf(CardDefinition definition) => definition.Effect == "hato-gurama" ? 1 : 0;
    /// <summary>Stone of Remembrance (issue #829): "This Holding does not count towards your minimum deck size" -- it counts as zero cards toward the 40-card minimum (it is still a card of the deck).</summary>
    public static bool CountsTowardMinimumOf(CardDefinition definition) => definition.Effect != Wave829Abilities.StoneEffect;
    /// <summary>Eternal Victory Dojo (issue #486 batch 1): the Stronghold's printed deck-construction text lets the deck hold four copies each of one or two non-Unique Strategies with Battle abilities.</summary>
    public static bool StrategyCopyRuleOf(CardDefinition definition) => definition.Effect == "eternal-victory-dojo";
    /// <summary>A Strategy that has a Battle ability (a Battle or Battle/Open ability among its own abilities).</summary>
    public static bool BattleStrategyOf(CardDefinition definition) =>
        definition.Type == CardType.Strategy && (definition.Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing is "Battle" or "BattleOrOpen");
    /// <summary>The most titles a Stronghold with the strategy-copy rule lets the deck hold four copies of, and the limit for each.</summary>
    public const int StrategyCopyRuleTitles = 2, StrategyCopyRuleLimit = 4;
    private readonly Dictionary<string, DeckCardInfo> _pool;
    private readonly Dictionary<string, DeckCardInfo> _poolById;
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    public string Legality { get; private set; } = "Emperor";
    // Not in the pool, so it cannot be added and a deck file naming one is rejected as an unknown card: a published created card (CreatedOnly) or a raw Oracle "Proxy" scan.
    public static bool MayBeInDeck(DeckCardInfo card) => !card.CreatedOnly && !card.Types.Contains("Proxy", StringComparer.OrdinalIgnoreCase);
    public DeckWorkshop(IEnumerable<DeckCardInfo> pool)
    {
        var cards = pool.Where(MayBeInDeck).ToArray();
        _pool = cards.ToDictionary(c => c.DeckName, StringComparer.OrdinalIgnoreCase);
        _poolById = cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
    }
    private string ResolveKey(string nameOrId)
    {
        if (_pool.ContainsKey(nameOrId)) return _pool.Keys.First(k => string.Equals(k, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (_poolById.TryGetValue(nameOrId, out var byId)) return byId.DeckName;
        var byTitle = _pool.Values.Where(c => string.Equals(c.Name, nameOrId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (byTitle.Length == 1) return byTitle[0].DeckName;
        if (byTitle.Length > 1) throw new ArgumentException($"{nameOrId} has multiple Experience Levels; use its Experienced deck name or card id.");
        throw new KeyNotFoundException($"Unknown card: {nameOrId}.");
    }
    public int Count(string nameOrId)
    {
        try { return _counts.GetValueOrDefault(ResolveKey(nameOrId)); }
        catch (KeyNotFoundException) { return 0; }
    }
    public DeckCardInfo[] Cards => _counts.Where(p => p.Value > 0).Select(p => _pool[p.Key]).ToArray();
    public void Adjust(string nameOrId, int delta)
    {
        var key = ResolveKey(nameOrId);
        var card = _pool[key];
        if (card.SetupSlot.Length > 0) throw new InvalidOperationException("Use Select for setup cards.");
        var count = Math.Clamp((long)_counts.GetValueOrDefault(key) + delta, 0, 999);
        if (count == 0) _counts.Remove(key); else _counts[key] = (int)count;
    }
    public void SelectSetup(string nameOrId)
    {
        var card = _pool[ResolveKey(nameOrId)];
        if (card.SetupSlot.Length == 0) throw new InvalidOperationException("This is not a setup card.");
        foreach (var old in Cards.Where(c => c.SetupSlot == card.SetupSlot).ToArray()) _counts.Remove(old.DeckName);
        _counts[card.DeckName] = 1;
    }
    public Deck ToDeck() => new(Legality, Cards.OrderBy(c => c.SetupSlot.Length == 0 ? 1 : 0)
        .ThenBy(c => c.DeckName, StringComparer.OrdinalIgnoreCase).Select(c => new DeckEntry(c.DeckName, Count(c.Id))).ToArray());
    /// <summary>Find the unreadable lines in a deck file and the nearest catalogue card for each (the import troubleshooter). Does not change the draft.</summary>
    public DeckTroubleshooting Troubleshoot(string text)
    {
        if (text.Length > 1_000_000) throw new FormatException("Deck file exceeds the 1 MB limit.");
        return DeckTroubleshooter.Analyse(text, _pool.Values);
    }
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
            var sum = (long)incoming.GetValueOrDefault(card.DeckName) + entry.Count;
            if (entry.Count <= 0 || sum > 999) throw new FormatException($"Invalid quantity for {card.DeckName}; supported file quantities are 1–999.");
            incoming[card.DeckName] = (int)sum;
        }
        // Commit only after the entire file resolves, so a bad import cannot erase a draft.
        _counts.Clear(); foreach (var pair in incoming) _counts.Add(pair.Key, pair.Value);
        Legality = "Emperor";
    }
    public DeckReview Review()
    {
        var warnings = new List<string>();
        foreach (var slot in SetupSlots)
            if (Cards.Where(c => c.SetupSlot == slot).Sum(c => Count(c.Id)) != 1) warnings.Add($"Select exactly one {slot} version.");
        int Total(string family) => Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains(family) && c.CountsTowardMinimum).Sum(c => Count(c.Id));
        var dynasty = Total("Dynasty"); var fate = Total("Fate");
        if (dynasty < 40) warnings.Add($"Dynasty needs {40 - dynasty} more cards (40 minimum).");
        // Hato-gurama (issue #453 batch 5): each copy in the deck lowers the Fate minimum by one.
        var fateMinimum = 40 - Cards.Where(c => c.FateMinimumReduction > 0).Sum(c => Count(c.Id) * c.FateMinimumReduction);
        if (fate < fateMinimum) warnings.Add($"Fate needs {fateMinimum - fate} more cards ({fateMinimum} minimum).");
        // Eternal Victory Dojo (issue #486 batch 1): with that Stronghold, the (at most two) non-Unique Strategies with Battle abilities the deck holds most copies of may have four.
        var extraCopies = Cards.Any(c => c.SetupSlot == "Stronghold" && c.StrategyCopyRule)
            ? Cards.Where(c => c.BattleStrategy && !c.Keywords.Contains("Unique", StringComparer.Ordinal) && Count(c.Id) > 3)
                .OrderByDescending(c => Count(c.Id)).ThenBy(c => c.DeckName, StringComparer.OrdinalIgnoreCase).Take(StrategyCopyRuleTitles).Select(c => c.DeckName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var limits = DeckFormat.CheckLegality(ToDeck(), name => _pool[name].Keywords, name => extraCopies.Contains(name) ? StrategyCopyRuleLimit : _pool[name].CopyLimit);
        foreach (var v in limits.Violations) warnings.Add(extraCopies.Contains(v.CardName)
            ? $"{v.CardName}: {v.Count} exceeds the {v.Limit} copies your Stronghold allows."
            : _pool[v.CardName].CopyLimit is not null
            ? $"{v.CardName}: {v.Count} exceeds the card's own limit of {v.Limit} copies."
            : $"{v.CardName}: {v.Count} exceeds the usual {v.Limit} limit. Card-specific exceptions require review.");
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
    /// <summary>Puts the decks that ship with the game into this folder the first time it is asked (the marker file records that it has been): a deck of the same name is never replaced, and a deck
    /// the player later deletes does not come back. Returns how many were written.</summary>
    public int Preload(IEnumerable<(string Name, string Text)> decks, string markerPath)
    {
        if (File.Exists(markerPath)) return 0;
        Directory.CreateDirectory(DirectoryPath);
        var written = 0;
        foreach (var (name, text) in decks)
        {
            var target = PathFor(name);
            if (File.Exists(target)) continue;
            File.WriteAllText(target, text); written++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markerPath))!);
        File.WriteAllText(markerPath, $"{written} preloaded decks written");
        return written;
    }
    public static string Read(string path)
    {
        if (new FileInfo(path).Length > 1_000_000) throw new FormatException("Deck file exceeds the 1 MB limit.");
        return File.ReadAllText(path);
    }
    public static string Fingerprint(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Read(path))));
    /// <summary>Deletes one saved deck file; only a .txt file directly inside this store's folder may be removed.</summary>
    public void Delete(string path)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), DirectoryPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only a saved deck in the deck folder can be deleted.");
        File.Delete(full);
    }
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
