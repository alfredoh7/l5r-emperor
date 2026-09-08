using System.Text.Json;

namespace L5R.Core;

public sealed record PublishedDeck(string Clan, string StrongholdId, string[] Dynasty, string[] Fate);
public sealed record PublishedSnapshot(string Profile, CardDefinition[] Cards, PublishedDeck[] Decks);

/// <summary>A closed, reviewed card pool embedded in both server and client builds.</summary>
public static class PublishedContent
{
    public const string Scenario = "lion-crane-v1";
    public static PublishedSnapshot Snapshot { get; } = Load("L5R.Published.json", Scenario, requireDecks: true);

    /// <summary>Wave 1 of the wider catalogue: every ability-free card, reviewed the same way as the
    /// pinned Lion/Crane pool but not part of any constructed deck yet -- browsing/custom-deck content only.</summary>
    public static PublishedSnapshot VanillaWave1 { get; } = LoadVanillaWave();

    private static PublishedSnapshot LoadVanillaWave()
    {
        var snapshot = Load("L5R.Published.VanillaWave1.json", "vanilla-wave-1", requireDecks: false);
        if(snapshot.Cards.Length != 61)
            throw new InvalidOperationException("Unexpected vanilla-wave-1 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Effect != "none" || card.Text.Length != 0 || card.Abilities is null || card.Abilities.Length != 0)
                throw new InvalidOperationException($"vanilla-wave-1 card is not ability-free: {card.Id}");
        if(snapshot.Cards.Select(c=>c.Id).Intersect(Snapshot.Cards.Select(c=>c.Id)).Any())
            throw new InvalidOperationException("vanilla-wave-1 duplicates a card already in the pinned Lion/Crane pool.");
        return snapshot;
    }

    private static PublishedSnapshot Load(string resourceName, string profile, bool requireDecks)
    {
        using var stream = typeof(PublishedContent).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Published content is missing: {resourceName}");
        var snapshot = JsonSerializer.Deserialize<PublishedSnapshot>(stream)!;
        if(snapshot.Profile != profile || (requireDecks && snapshot.Decks.Length != 2))
            throw new InvalidOperationException($"Unexpected published content snapshot: {resourceName}");
        var cards=snapshot.Cards.ToDictionary(c=>c.Id);
        foreach(var card in snapshot.Cards)
        {
            if(card.ScenarioOnly || card.Abilities is null || card.Keywords is null ||
                card.Abilities.Any(id=>!AbilityCatalog.All.ContainsKey(id)))
                throw new InvalidOperationException($"Unimplemented published definition: {card.Id}");
        }
        foreach(var deck in snapshot.Decks)
            if(deck.Dynasty.Length!=40 || deck.Fate.Length!=40 ||
               deck.Dynasty.Concat(deck.Fate).Append(deck.StrongholdId).Any(id=>!cards.ContainsKey(id)))
                throw new InvalidOperationException("Published deck is incomplete.");
        return snapshot;
    }
}
