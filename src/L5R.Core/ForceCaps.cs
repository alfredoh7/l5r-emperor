namespace L5R.Core;

// Issue #768: the Force cap ("has a maximum Force of 3"), the stat cap primitive P9 left out (docs/decisions/2026-10-06-672-p9-stat-terms.md). Asako Kaitoko - exp (Kaitoko.cs) is the first card.
//
// A cap is a LIVE rule, like a stat term: a registrant on EngineHooks.ForceCaps yields the caps that bind a Personality as the engine asks for its Force, from the state as it is then. Nothing is
// stored on the Personality, so when the source leaves the battlefield (moves home, is destroyed, the battle ends) or the Personality does, the next read is the uncapped Force again.
//
// A cap only LOWERS: the Personality's Force is min(its Force, the lowest cap). A Personality at or under the maximum is untouched, and Force is never raised to it.
//
// A cap is a Force penalty, so whatever negates Force penalties negates it, and only for the Personality that has the protection:
//   * "Negate all current and new Force penalties on him" (Tireless Efforts, Incense Mill, Deep Roots, ...): CardInstance.ForcePenaltiesNegatedTurn, a per-card mark for the turn;
//   * "Negate all current Force penalties on him" without the "new" (Incense Mill): a one-shot cleanse, so the caps in force when it resolves are recorded for the turn (ForceCapCleanse), by source;
//   * "Negate all current effects on it from other players' cards" (Peace of the Kami): the same one-shot record, for the caps whose source another player controls (Mendon, 2026-10-09). The cap is a
//     trait, not an action, so Creating Order ("no Force bonus or penalty from an action") does not touch it;
//   * a P12 Force-penalty immunity (ImmunityKind.ForcePenalty), stored on one card, on a player's cards (a keyword or type filter) or standing on a card while it is in play: asked as
//     IsImmune(card, ForcePenalty, the cap's source controller, the source card), so an immunity to "other players' cards" does not stop a cap from the Personality's own player's card.
// Both are read where the Force is asked, not when the cap begins, so a protection given after the cap began lifts it at once and one that ends restores it.
public sealed record ForceCap(int Max, string SourceCardId, string Label);

/// <summary>A cap Incense Mill's "negate all current Force penalties" cleansed: this source's cap on this Personality is negated for the turn.</summary>
public sealed record ForceCapCleanse(string CardId, string SourceCardId, int Turn);

internal sealed record ForceCapContext(MatchEngine Engine, CardInstance Card);

internal static partial class EngineHooks
{
    /// <summary>The Force caps that bind a Personality now (ForceCaps); empty when a registrant has none for the card.</summary>
    internal static readonly OrderedHooks<Func<ForceCapContext, IEnumerable<ForceCap>>> ForceCaps = new();
}

public sealed partial class MatchState
{
    /// <summary>The caps cleansed by a one-shot "negate all current Force penalties" this turn. Omitted until one is.</summary>
    [System.Text.Json.Serialization.JsonPropertyOrder(23200)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<ForceCapCleanse>? ForceCapCleanses { get; set; }
}

public sealed partial class MatchEngine
{
    /// <summary>Test-only caps, read beside the registered ones; null outside a test.</summary>
    internal static Func<MatchEngine, CardInstance, IEnumerable<ForceCap>>? TestForceCaps;

    /// <summary>The Force caps that bind a Personality now and are not negated for him: the registered rules, minus those a Force-penalty negation or immunity covers.</summary>
    internal IEnumerable<ForceCap> ForceCapsOn(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) yield break;
        var context = new ForceCapContext(this, card);
        var caps = EngineHooks.ForceCaps.Items.SelectMany(hook => hook(context));
        if (TestForceCaps is { } test) caps = caps.Concat(test(this, card));
        foreach (var cap in caps)
        {
            if (card.ForcePenaltiesNegatedTurn == _state.TurnNumber) continue; // Tireless Efforts and every other "negate all Force penalties on him"
            _state.Cards.TryGetValue(cap.SourceCardId, out var source);
            if (_state.ForceCapCleanses is { } cleansed && cleansed.Any(c => c.CardId == card.Id && c.SourceCardId == cap.SourceCardId && c.Turn == _state.TurnNumber)) continue; // Incense Mill
            if (IsImmune(card, ImmunityKind.ForcePenalty, source?.OwnerId, source)) continue; // P12: his own immunity, or one a card gives his player's cards
            yield return cap;
        }
    }

    /// <summary>A Personality's Force with every cap that binds him applied: lowered to the lowest maximum, never raised.</summary>
    private int ApplyForceCaps(CardInstance card, int force)
    {
        if (EngineHooks.ForceCaps.Items.Length == 0 && TestForceCaps is null) return force;
        foreach (var cap in ForceCapsOn(card)) force = Math.Min(force, Math.Max(0, cap.Max));
        return force;
    }

    /// <summary>"Negate all current Force penalties on him" (Incense Mill), or with otherPlayersOnly "negate all current effects on it from other players' cards" (Peace of the Kami): the caps binding him now
    /// (those whose source another player controls, when otherPlayersOnly) are cleansed for the turn.</summary>
    private void CleanseForceCaps(CardInstance card, bool otherPlayersOnly = false)
    {
        var caps = ForceCapsOn(card).Where(cap => !otherPlayersOnly || !_state.Cards.TryGetValue(cap.SourceCardId, out var source) || source.OwnerId != card.OwnerId).ToArray();
        if (caps.Length == 0) return;
        var list = _state.ForceCapCleanses ??= [];
        list.RemoveAll(c => c.Turn != _state.TurnNumber);
        foreach (var cap in caps) list.Add(new(card.Id, cap.SourceCardId, _state.TurnNumber));
    }

    internal void NegateOtherPlayersEffectsForTest(string cardId) => NegateOtherPlayersEffects(Card(cardId));
    internal void NegateForcePenaltiesForTest(string cardId) => NegateForcePenalties(Card(cardId));
    internal int ForceUncappedForTest(string cardId) => PersonalityForceUncapped(Card(cardId));
}
