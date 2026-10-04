using L5R.Core;

namespace L5R.AI;

/// <summary>
/// Issue #642: what the bot remembers between decisions of one match, using only what a player at the table could see: a card shown face-up in a
/// public zone stays seen even after it is discarded, removed or returned to a deck, so the bot can reason that a non-Unique card has two more
/// copies somewhere (the usual three-copy limit) and a Unique card has none. It never reads a hidden zone, the seed, or the engine.
///
/// One context belongs to one seat of one match. Choose() works without one (a fresh context is made per call), but then it forgets, so a caller
/// that plays a whole match should keep and pass the same instance.
/// </summary>
public sealed class BotContext
{
    /// <summary>The copy limit assumed for a card that is not Unique.</summary>
    public const int AssumedCopies = 3;

    private readonly Dictionary<string, HashSet<string>> _seenIdsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _uniqueByName = new(StringComparer.Ordinal);
    private int _lobbyTurn = -1;
    private int _favorTakenTurn = -1;

    /// <summary>The average Personal Honor of the bot's own decklist, when the caller knows it (its own deck is its own information). Null: estimated from cards seen.</summary>
    public double? DeckAveragePersonalHonor { get; init; }

    /// <summary>Records every face-up card in a public zone of either player. Safe to call on every decision.</summary>
    public void Observe(MatchView view)
    {
        foreach (var player in view.Players)
            foreach (var card in PublicCards(player))
                Note(card);
    }

    private static IEnumerable<CardView> PublicCards(PlayerView p) =>
        p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame).Concat(p.EventsInPlay ?? []).Concat(p.Celestials ?? []).Concat(p.Underneath ?? [])
            .Concat(p.Provinces.Select(x => x.Card).OfType<CardView>()).Append(p.Stronghold).Concat(p.Hand.Where(c => c.Definition is not null));

    private void Note(CardView card)
    {
        if (card.Definition is not { } def) return;
        if (!_seenIdsByName.TryGetValue(def.Name, out var ids)) _seenIdsByName[def.Name] = ids = new(StringComparer.Ordinal);
        ids.Add(card.Id);
        _uniqueByName[def.Name] = def.Keywords?.Contains("Unique") == true;
    }

    /// <summary>How many different copies of a card (by name) the bot has seen.</summary>
    public int Seen(string cardName) => _seenIdsByName.TryGetValue(cardName, out var ids) ? ids.Count : 0;

    /// <summary>Copies the bot expects could still be hidden somewhere: three minus those seen for a card that is not Unique, one minus those seen for a Unique card, never below zero.</summary>
    public int ExpectedHiddenCopies(string cardName) =>
        Math.Max(0, (_uniqueByName.GetValueOrDefault(cardName) ? 1 : AssumedCopies) - Seen(cardName));

    /// <summary>Records that the bot lobbied for the Imperial Favor on a turn (Lobby is once per turn).</summary>
    public void NoteLobby(int turn) => _lobbyTurn = turn;
    public bool LobbiedThisTurn(int turn) => _lobbyTurn == turn;
    public void NoteFavorTaken(int turn) => _favorTakenTurn = turn;
    public bool TookFavorThisTurn(int turn) => _favorTakenTurn == turn;

    /// <summary>A reproducible weight roll in [0, 1): the same match state and the same ability always roll the same, so replays and tests are deterministic.</summary>
    public static double Roll(long version, string? abilityId, string? cardId)
    {
        unchecked
        {
            var hash = 17L;
            hash = hash * 31 + version;
            foreach (var c in abilityId ?? "") hash = hash * 31 + c;
            foreach (var c in cardId ?? "") hash = hash * 31 + c;
            hash ^= hash >> 33; hash *= unchecked((long)0xff51afd7ed558ccdUL); hash ^= hash >> 33;
            return (hash & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
