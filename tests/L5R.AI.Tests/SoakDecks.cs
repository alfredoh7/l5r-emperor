using L5R.Core;

/// <summary>Seeded random Practice decks over the cards the engine reports as implemented (issue #642). Deterministic per (seed, seat).</summary>
internal sealed class SoakPool
{
    public const int ForcedPerFamily = 12;
    public DeckCardInfo[] Dynasty { get; }
    public DeckCardInfo[] Fate { get; }
    public DeckCardInfo[] Strongholds { get; }
    public DeckCardInfo[] BorderKeeps { get; }
    public DeckCardInfo[] Bamboo { get; }
    public IEnumerable<DeckCardInfo> All => Dynasty.Concat(Fate).Concat(Strongholds).Concat(BorderKeeps).Concat(Bamboo);

    public SoakPool()
    {
        var shared = CustomPractice.Workshop(); var setup = new List<DeckCardInfo>();
        foreach (var def in CardCatalog.PlayableFromDecks.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            try { shared.Adjust(def.Id, 1); }
            catch (KeyNotFoundException) { continue; }
            catch (InvalidOperationException)
            {
                var scratch = CustomPractice.Workshop(); scratch.SelectSetup(def.Id); setup.Add(scratch.Cards.Single());
            }
        }
        var main = shared.Cards.Where(c => c.Implemented).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        setup = setup.Where(c => c.Implemented).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        Dynasty = main.Where(c => c.Families.Contains("Dynasty")).ToArray();
        Fate = main.Where(c => !c.Families.Contains("Dynasty") && c.Families.Contains("Fate")).ToArray();
        Strongholds = setup.Where(c => c.SetupSlot == "Stronghold").ToArray();
        BorderKeeps = setup.Where(c => c.SetupSlot == "Border Keep").ToArray();
        Bamboo = setup.Where(c => c.SetupSlot == "Bamboo Harvesters").ToArray();
    }

    static bool Aligned(DeckCardInfo card, string clan)
    {
        var def = CardCatalog.Cards[card.Id];
        return def.Clan is "Unaligned" or "" || def.Clan == clan || (def.Clans?.Contains(clan) ?? false);
    }

    /// <summary>Builds a deck the engine's own review accepts; <paramref name="review"/> is the review of the last attempt.</summary>
    public Deck Build(ulong seed, int seat, out PracticeDeckReview review)
    {
        var deckIndex = checked((long)seed * 2 + seat);
        Deck? deck = null; review = new(0, 0, "Unaligned", null, ["no attempt"]);
        for (var attempt = 0; attempt < 12; attempt++)
        {
            deck = Attempt(deckIndex, attempt, forceCoverage: attempt < 6); review = CustomPractice.Review(deck);
            if (review.Ready) return deck;
        }
        return deck!;
    }

    Deck Attempt(long deckIndex, int attempt, bool forceCoverage)
    {
        var rng = new Random(unchecked((int)(deckIndex * 2654435761L + attempt * 40503L + 17)));
        var stronghold = Strongholds[(int)((deckIndex + (attempt >= 6 ? rng.Next(Strongholds.Length) : 0)) % Strongholds.Length)];
        var clan = CardCatalog.Cards[stronghold.Id].Clan;
        var entries = new List<DeckEntry> { new(stronghold.DeckName, 1), new(Pick(BorderKeeps, deckIndex, rng).DeckName, 1), new(Pick(Bamboo, deckIndex, rng).DeckName, 1) };
        foreach (var family in new[] { Dynasty, Fate })
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            static int Limit(DeckCardInfo c) => c.Keywords.Contains("Unique", StringComparer.Ordinal) ? 1 : Math.Min(3, c.CopyLimit ?? 3);
            void Add(DeckCardInfo c, int n) => counts[c.DeckName] = Math.Min(Limit(c), counts.GetValueOrDefault(c.DeckName) + n);
            if (forceCoverage && family.Length > 0)
                for (var i = 0; i < ForcedPerFamily; i++) Add(family[(int)((deckIndex * ForcedPerFamily + i) % family.Length)], 1);
            var aligned = family.Where(c => Aligned(c, clan)).ToArray(); var target = 40 + rng.Next(0, 6);
            for (var guard = 0; counts.Values.Sum() < target && guard < 4000; guard++)
            {
                var pool = aligned.Length > 0 && rng.Next(10) > 0 ? aligned : family;
                Add(pool[rng.Next(pool.Length)], 1 + rng.Next(2));
            }
            entries.AddRange(counts.Select(p => new DeckEntry(p.Key, p.Value)));
        }
        return new("Emperor", entries.ToArray());
    }

    static DeckCardInfo Pick(DeckCardInfo[] options, long deckIndex, Random rng) =>
        options.FirstOrDefault(c => c.Id is "oracle-l5r-1106" or "oracle-l5r-679") is { } pinned && rng.Next(3) > 0 ? pinned : options[(int)((deckIndex + rng.Next(options.Length)) % options.Length)];
}
