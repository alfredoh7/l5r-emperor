namespace L5R.Core;

// Issue #503, pass group P2: the search and look cards (EmperorP2.cs has the group's header).
//   * A SEARCH CRITERION AGAINST THE PERFORMER'S KEYWORDS (Delving into History: "a non-Unique Spell that must share a keyword with the performer unless he is Void"): the performer's current keywords
//     are read when the action is taken (the Spell is destroyed as the cost, so its wielder is the Personality it was last attached to) and ride on the search (PendingSearch.SharedKeywords); a
//     candidate qualifies only if it has one of them. A Void performer sets none (no restriction).
//   * A SEARCH CRITERION ON AN ABILITY'S KEYWORD (Gaining Advantage: "a non-Unique Strategy with a Political ability"): SearchCriteria.AbilityKeyword -- one of the card's printed actions is a
//     [keyword] action: tagged with it (AbilityDefinition.ActionKeywords), or inheriting it from the card's own keyword (core rules, Abilities).
//   * A LOOK WHOSE DISCARD IS PAID FOR BY DESTROYING THE SOURCE (The Tapestry Perceived: "You may destroy this card; if you do, discard the card you looked at"): the look's Discard disposition is
//     offered, and choosing it destroys the Spell first; if the Spell was not destroyed the card goes back on top (P2BeforeLookDiscard).
public static class EmperorP2Searches
{
    public static SearchSpec DelvingIntoHistory { get; } = new("p2-delving-into-history", "Search your Fate deck for a non-Unique Spell sharing a keyword with the performer",
        [new(SearchDeck.Fate, new("non-Unique Spell", [CardType.Spell], NonUnique: true), SearchEffect.ToHand)]);

    public static SearchSpec GainingAdvantage { get; } = new("p2-gaining-advantage", "You may search your Fate deck for a non-Unique Strategy with a Political ability",
        [new(SearchDeck.Fate, new("non-Unique Strategy with a Political ability", [CardType.Strategy], NonUnique: true, AbilityKeyword: "Political"), SearchEffect.ToHandThenDiscard)], Optional: true);

    public static LookSpec TapestryPerceived { get; } = new("p2-tapestry-perceived", "Look at the top card of any Fate deck", [new("Fate"), new("Fate", Opponent: true)], 1,
        [new(LookDisposition.Discard, 0, 1)]);

    public static SearchSpec[] All { get; } = [DelvingIntoHistory, GainingAdvantage];
    public static LookSpec[] Looks { get; } = [TapestryPerceived];
}

public sealed partial class MatchEngine
{
    // Delving into History: the Spell was destroyed as the cost; the performer is the Shugenja it was attached to.
    private void BeginDelvingSearch(AbilityDefinition ability, PlayerState player, CardInstance spell)
    {
        var performerId = spell.AttachedTo ?? spell.LastHostId;
        var performer = performerId is not null && _state.Cards.TryGetValue(performerId, out var found) ? found : null;
        BeginSearch(ability.Search!, spell, player.Id, "ability");
        if (_state.PendingSearch is { } search)
            search.SharedKeywords = performer is null ? [] : Keyword(performer, "Void") ? null : CurrentKeywordList(performer);
    }

    // SearchGroups' hook: a card that shares none of the performer's keywords does not qualify.
    private (bool Eligible, string? Reason) P2SearchFilter(PendingSearch search, CardInstance card, bool eligible, string? reason)
    {
        if (!eligible || search.SharedKeywords is not { } keywords) return (eligible, reason);
        return keywords.Any(k => Keyword(card, k)) ? (true, null) : (false, "Shares no keyword with the performer");
    }

    // SearchEligibility's hook: "a Strategy with a [keyword] ability" -- one of its printed actions carries the keyword (its own tag, or the card's keyword it inherits).
    private bool HasAbilityWithKeyword(CardInstance card, string keyword) =>
        (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var ability) && IsAction(ability)
            && (ability.ActionKeywords?.Contains(keyword) == true || Keyword(card, keyword)));

    // The Tapestry Perceived (ApplyArrangement's Discard case): "You may destroy this card; if you do, discard the card you looked at" -- false when the card goes back on top instead.
    private bool P2BeforeLookDiscard(PendingLook look, CardInstance source)
    {
        if (look.LookId != EmperorP2Searches.TapestryPerceived.Id) return true;
        if (InPlay(source) && source.Zone == Zone.Attached) DestroyAttachment(source);
        if (InPlay(source)) { Emit("tapestryNotDestroyed", $"{Definition(source).Name} was not destroyed, so the card stays on top.", look.PlayerId, source.Id); return false; }
        return true;
    }

    // Gaining Advantage (an Event): "You may search your Fate deck for a non-Unique Strategy with a Political ability, show it, put it in your hand, and discard a card."
    private bool BeginGainingAdvantage(PlayerState player, CardInstance card)
    {
        BeginSearch(EmperorP2Searches.GainingAdvantage, card, player.Id, "event");
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP2Search_Rung4b1()
    {
        EngineHooks.Looks.Register("EmperorP2Searches", 300, static () => EmperorP2Searches.Looks);
        EngineHooks.Searches.Register("EmperorP2Searches", 300, static () => EmperorP2Searches.All);
    }
}
