using System.Text.Json;

namespace L5R.Core;

public sealed record PracticeDeckReview(int Dynasty, int Fate, string Clan, string? StrongholdId, string[] Issues)
{
    public bool Ready => Issues.Length == 0;
}

/// <summary>Authoritative setup checks; the client supplies deck intentions, never support flags.</summary>
public static class CustomPractice
{
    public const string Scenario = "custom-practice-v1";
    private sealed record PoolCard(string Id, string Name, string[] Types, string[] DeckTypes, string[] Keywords);
    private sealed record Pool(PoolCard[] Cards);
    private static readonly Lazy<DeckCardInfo[]> PoolCards = new(() =>
    {
        using var stream = typeof(CustomPractice).Assembly.GetManifestResourceStream("L5R.DeckPool.json")!;
        var pool = JsonSerializer.Deserialize<Pool>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return pool.Cards.Select(c =>
        {
            var def = CardCatalog.Cards.GetValueOrDefault(c.Id);
            return new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, def?.Keywords?.ToArray() ?? c.Keywords,
                def is { ScenarioOnly: false } && c.Id.StartsWith("oracle-l5r-", StringComparison.Ordinal), def?.CreatedOnly ?? false,
                FateMinimumReduction: def is null ? 0 : DeckWorkshop.FateMinimumReductionOf(def));
        }).ToArray();
    });
    public static DeckWorkshop Workshop() => new(PoolCards.Value);
    public static Deck Preset(string clan)
    {
        var preset = PublishedContent.Snapshot.Decks.Single(d => d.Clan == clan);
        var ids = new[] { preset.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(preset.Dynasty).Concat(preset.Fate);
        return new("Emperor", ids.GroupBy(id => id).Select(g =>
        {
            var c = CardCatalog.Get(g.Key); return new DeckEntry(Experienced.DeckName(c.Name, c.Keywords ?? []), g.Count());
        }).ToArray());
    }
    public static PracticeDeckReview Review(Deck deck)
    {
        try
        {
            var draft = Resolve(deck);
            var basic = draft.Review();
            var issues = basic.Warnings.ToList();
            if (basic.Unimplemented > 0)
            {
                issues.RemoveAll(x => x.Contains("selected card types are not yet implemented", StringComparison.Ordinal));
                issues.Add("Not implemented: " + string.Join(", ", draft.Cards.Where(c => !c.Implemented).Select(c => c.DeckName)) + ".");
            }
            foreach (var c in draft.Cards.Where(c => c.SetupSlot.Length == 0))
                if (c.Families.Count(f => f is "Dynasty" or "Fate") != 1) issues.Add($"{c.DeckName}: deck placement is not supported.");
            // Resource bound, not an assertion about tournament deck-size rules.
            if ((long)basic.Dynasty + basic.Fate > 10000) issues.Add("This build supports at most 10,000 cards in a Practice deck.");
            var stronghold = draft.Cards.FirstOrDefault(c => c.SetupSlot == "Stronghold");
            var definition = stronghold is null ? null : CardCatalog.Cards.GetValueOrDefault(stronghold.Id);
            return new(basic.Dynasty, basic.Fate, definition?.Clan ?? "Unaligned", stronghold?.Id, issues.ToArray());
        }
        catch (Exception e) when (e is ArgumentException or FormatException or KeyNotFoundException or OverflowException)
        { return new(0, 0, "Unaligned", null, [e.Message]); }
    }
    internal static DeckWorkshop Resolve(Deck deck)
    {
        if (deck is null || deck.Entries is null || deck.Entries.Length > 10000 || deck.Entries.Any(e => e is null || string.IsNullOrWhiteSpace(e.Name) || e.Count is <= 0 or > 999))
            throw new ArgumentException("Invalid deck entries or quantities.");
        var draft = Workshop(); draft.Load(DeckFormat.Write(deck)); return draft;
    }
    internal static Deck CopyDeck(Deck deck) => new(deck.Legality, deck.Entries.Select(e => e with { }).ToArray());
}

public sealed partial class MatchEngine
{
    private Deck[]? _customDecks;
    public static MatchEngine CreateCustomMatch(ulong seed, Deck yourDeck, Deck opponentDeck)
    {
        Deck[] supplied = [yourDeck, opponentDeck];
        for (var i = 0; i < 2; i++)
        {
            var review = CustomPractice.Review(supplied[i]);
            if (!review.Ready) throw new ArgumentException($"{(i == 0 ? "Your deck" : "Opponent's deck")}: {string.Join("\n", review.Issues)}");
        }
        var decks = supplied.Select(CustomPractice.CopyDeck).ToArray();
        var drafts = decks.Select(CustomPractice.Resolve).ToArray();
        var state = new MatchState { Players = [new() { Id = 0 }, new() { Id = 1 }] };
        var random = new StableRandom(seed); var nextId = 0;
        CardInstance Add(int owner, string id, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(id);
            var card = new CardInstance { Id = $"card-{++nextId:D4}", DefinitionId = id, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card); return card;
        }
        foreach (var player in state.Players)
        {
            var draft = drafts[player.Id];
            var stronghold = CardCatalog.Get(draft.Cards.Single(c => c.SetupSlot == "Stronghold").Id);
            player.Clan = stronghold.Clan; player.Name = stronghold.Clan; player.Honor = stronghold.StartingHonor;
            player.StrongholdId = Add(player.Id, stronghold.Id, Zone.Stronghold, true).Id;
            Add(player.Id, draft.Cards.Single(c => c.SetupSlot == "Border Keep").Id, Zone.Home, true);
            foreach (var c in draft.Cards.Where(c => c.SetupSlot.Length == 0).OrderBy(c => c.Id, StringComparer.Ordinal))
                for (var n = 0; n < draft.Count(c.Id); n++)
                {
                    var dynasty = c.Families.Contains("Dynasty");
                    var id = Add(player.Id, c.Id, dynasty ? Zone.DynastyDeck : Zone.FateDeck).Id;
                    (dynasty ? player.DynastyDeck : player.FateDeck).Add(id);
                }
            random.Shuffle(player.DynastyDeck); random.Shuffle(player.FateDeck);
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = stronghold.ProvinceStrength, PrintedStrength = stronghold.ProvinceStrength }).ToList();
        }
        // Same starting-Honor ordering as the published setup; seeded tie break.
        state.ActivePlayerId = state.Players[0].Honor == state.Players[1].Honor ? (int)(seed % 2) : state.Players.OrderByDescending(p => p.Honor).First().Id;
        var second = 1 - state.ActivePlayerId;
        Add(second, drafts[second].Cards.Single(c => c.SetupSlot == "Bamboo Harvesters").Id, Zone.Home, true, true);
        var engine = new MatchEngine(state, seed, CustomPractice.Scenario) { _customDecks = decks };
        foreach (var player in state.Players)
        {
            foreach (var province in player.Provinces) engine.Refill(player, province);
            for (var n = 0; n < 6; n++) engine.Draw(player);
        }
        engine.Emit("customSetup", $"{state.Players[state.ActivePlayerId].Clan} goes first; {state.Players[second].Clan} starts with bowed Bamboo Harvesters.");
        engine.StartTurn(); return engine;
    }
}
