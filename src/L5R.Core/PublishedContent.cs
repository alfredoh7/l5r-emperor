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

    /// <summary>Strongholds beyond the pinned Lion/Crane pool, each with a curated, hand-verified
    /// Effect/Ability mapping (never inferred from prose) -- unlike VanillaWave1, these carry real
    /// rules text and printed abilities.</summary>
    public static PublishedSnapshot StrongholdWave1 { get; } = LoadStrongholdWave();

    private static PublishedSnapshot LoadStrongholdWave()
    {
        var snapshot = Load("L5R.Published.StrongholdWave1.json", "stronghold-wave-1", requireDecks: false);
        if(snapshot.Cards.Length != 4)
            throw new InvalidOperationException("Unexpected stronghold-wave-1 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Stronghold || card.Text.Length == 0)
                throw new InvalidOperationException($"stronghold-wave-1 card is not a textful Stronghold: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("stronghold-wave-1 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 2 of the Stronghold rollout: the first two Strongholds using the Ranged/Melee
    /// Attack primitive (deferred, not-required-up-front targeting -- core rules Glossary).</summary>
    public static PublishedSnapshot StrongholdWave2 { get; } = LoadStrongholdWave2();

    private static PublishedSnapshot LoadStrongholdWave2()
    {
        var snapshot = Load("L5R.Published.StrongholdWave2.json", "stronghold-wave-2", requireDecks: false);
        if(snapshot.Cards.Length != 2)
            throw new InvalidOperationException("Unexpected stronghold-wave-2 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Stronghold || card.Text.Length == 0)
                throw new InvalidOperationException($"stronghold-wave-2 card is not a textful Stronghold: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("stronghold-wave-2 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 3 of the Stronghold rollout: Monastery of New Thought, the first card using
    /// the Token primitive's Fire type end-to-end (grant, then destroy for a Ranged Attack cost).</summary>
    public static PublishedSnapshot StrongholdWave3 { get; } = LoadStrongholdWave3();

    private static PublishedSnapshot LoadStrongholdWave3()
    {
        var snapshot = Load("L5R.Published.StrongholdWave3.json", "stronghold-wave-3", requireDecks: false);
        if(snapshot.Cards.Length != 1)
            throw new InvalidOperationException("Unexpected stronghold-wave-3 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Stronghold || card.Text.Length == 0)
                throw new InvalidOperationException($"stronghold-wave-3 card is not a textful Stronghold: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Concat(StrongholdWave2.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("stronghold-wave-3 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 1 of ability-bearing Personalities beyond the pinned Lion/Crane pool: Togashi
    /// Meiyu, the first card exercising a Reaction-timing, entering-play-triggered ability.</summary>
    public static PublishedSnapshot PersonalityWave1 { get; } = LoadPersonalityWave1();

    private static PublishedSnapshot LoadPersonalityWave1()
    {
        var snapshot = Load("L5R.Published.PersonalityWave1.json", "personality-wave-1", requireDecks: false);
        if(snapshot.Cards.Length != 1)
            throw new InvalidOperationException("Unexpected personality-wave-1 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Personality || card.Text.Length == 0)
                throw new InvalidOperationException($"personality-wave-1 card is not a textful Personality: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Concat(StrongholdWave2.Cards).Concat(StrongholdWave3.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("personality-wave-1 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 2 of ability-bearing Personalities: Togashi Torazu, reusing the Fire-token
    /// primitives with a self-performed (Performer-less) Battle ability.</summary>
    public static PublishedSnapshot PersonalityWave2 { get; } = LoadPersonalityWave2();

    private static PublishedSnapshot LoadPersonalityWave2()
    {
        var snapshot = Load("L5R.Published.PersonalityWave2.json", "personality-wave-2", requireDecks: false);
        if(snapshot.Cards.Length != 1)
            throw new InvalidOperationException("Unexpected personality-wave-2 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Personality || card.Text.Length == 0)
                throw new InvalidOperationException($"personality-wave-2 card is not a textful Personality: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Concat(StrongholdWave2.Cards).Concat(StrongholdWave3.Cards).Concat(PersonalityWave1.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("personality-wave-2 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 3 of ability-bearing Personalities: the first wave of import wave 1 (issue #95)
    /// -- straightforward Ranged/Melee Attack cards needing nothing beyond the primitive itself.</summary>
    public static PublishedSnapshot PersonalityWave3 { get; } = LoadPersonalityWave3();

    private static PublishedSnapshot LoadPersonalityWave3()
    {
        var snapshot = Load("L5R.Published.PersonalityWave3.json", "personality-wave-3", requireDecks: false);
        if(snapshot.Cards.Length != 4)
            throw new InvalidOperationException("Unexpected personality-wave-3 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Personality || card.Text.Length == 0)
                throw new InvalidOperationException($"personality-wave-3 card is not a textful Personality: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Concat(StrongholdWave2.Cards).Concat(StrongholdWave3.Cards).Concat(PersonalityWave1.Cards).Concat(PersonalityWave2.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("personality-wave-3 duplicates a card already published elsewhere.");
        return snapshot;
    }

    /// <summary>Wave 1 of ability-bearing Followers: Stalking Tiger, the second half of import wave 1
    /// (issue #95), proving the Attack primitive needs no Personality-specific plumbing.</summary>
    public static PublishedSnapshot FollowerWave1 { get; } = LoadFollowerWave1();

    private static PublishedSnapshot LoadFollowerWave1()
    {
        var snapshot = Load("L5R.Published.FollowerWave1.json", "follower-wave-1", requireDecks: false);
        if(snapshot.Cards.Length != 1)
            throw new InvalidOperationException("Unexpected follower-wave-1 card count.");
        foreach(var card in snapshot.Cards)
            if(card.Type != CardType.Follower || card.Text.Length == 0)
                throw new InvalidOperationException($"follower-wave-1 card is not a textful Follower: {card.Id}");
        var others = Snapshot.Cards.Concat(VanillaWave1.Cards).Concat(StrongholdWave1.Cards).Concat(StrongholdWave2.Cards).Concat(StrongholdWave3.Cards).Concat(PersonalityWave1.Cards).Concat(PersonalityWave2.Cards).Concat(PersonalityWave3.Cards).Select(c=>c.Id);
        if(snapshot.Cards.Select(c=>c.Id).Intersect(others).Any())
            throw new InvalidOperationException("follower-wave-1 duplicates a card already published elsewhere.");
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
