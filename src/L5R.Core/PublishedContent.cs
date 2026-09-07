using System.Text.Json;

namespace L5R.Core;

public sealed record PublishedDeck(string Clan, string StrongholdId, string[] Dynasty, string[] Fate);
public sealed record PublishedSnapshot(string Profile, CardDefinition[] Cards, PublishedDeck[] Decks);

/// <summary>A closed, reviewed card pool embedded in both server and client builds.</summary>
public static class PublishedContent
{
    public const string Scenario = "lion-crane-v1";
    public static PublishedSnapshot Snapshot { get; } = Load();
    private static PublishedSnapshot Load()
    {
        using var stream = typeof(PublishedContent).Assembly.GetManifestResourceStream("L5R.Published.json")
            ?? throw new InvalidOperationException("Published content is missing.");
        var snapshot = JsonSerializer.Deserialize<PublishedSnapshot>(stream)!;
        if(snapshot.Profile != Scenario || snapshot.Cards.Length != 40 || snapshot.Decks.Length != 2)
            throw new InvalidOperationException("Unexpected published content snapshot.");
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
