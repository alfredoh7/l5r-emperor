namespace L5R.Core;

// Issue #639 (Mendon): after a Proclaim resolves, neither player is asked to pass a reaction window unless one of them can still take a reaction in it
// (a Reaction in hand, or a printed one in play, that the window offers). Left open, the window made both players pass for nothing.

public sealed partial class MatchEngine
{
    /// <summary>True when the player, given the priority in the open Proclaim window, would be offered something other than passing.</summary>
    private bool ProclaimWindowOffers(int playerId)
    {
        var priority = _state.PriorityPlayerId;
        _state.PriorityPlayerId = playerId;
        try { return QueryActions(playerId).Any(a => a.Enabled && a.Type is "useAbility" or "proclaim"); }
        finally { _state.PriorityPlayerId = priority; }
    }

    /// <summary>Closes the Proclaim or entering-play reaction window the moment an action in it has resolved and no one has a reaction left to take in it (issue #644: a
    /// Reaction used inside an entry window, Satoshi's Dual Warfans' draw or the Celestial Sword of the Crane's Honor, no longer leaves a Pass reaction box).</summary>
    private void CloseEntryWindowIfSpent()
    {
        if (_state.PendingChoice is not { Type: "proclaim" or "entryReaction" } pending || _state.PendingNegatableAbility is not null) return;
        if (ProclaimWindowOffers(0) || ProclaimWindowOffers(1)) return;
        CloseReactionWindow(pending, "No reaction was left to take; the window closed.");
    }
}

public sealed partial class MatchEngine
{
    /// <summary>The (Clan discount, Clan allegiance) variants of buying a Clan Personality, in the order they are offered; the first enabled one is what a client defaults to.
    /// Allegiance (+2 Gold, ignoring the Honor Requirement) comes first whenever the Honor Requirement is not met, so its extra cost is applied without being asked for.
    /// The first Personality a player buys in a turn defaults to the full cost with Proclaim available; every later one to the Clan-discounted cost.</summary>
    private (bool Discount, bool Allegiance)[] PurchaseModes(PlayerState player, CardInstance card)
    {
        var plain = QuotePurchase(player.Id, card.Id);
        var allegiance = QuotePurchase(player.Id, card.Id, false, true);
        var short_ = plain.Error is not null && allegiance.Error is null;
        var first = player.PersonalityPurchaseTurn != _state.TurnNumber && (short_ ? allegiance : plain).ProclaimEligible;
        return first
            ? [(false, short_), (true, short_), (false, !short_), (true, !short_)]
            : [(true, short_), (false, short_), (true, !short_), (false, !short_)];
    }
}

public sealed partial class MatchEngine
{
    /// <summary>A Celestial enters play the instant it is revealed (core rules Glossary), whichever effect turns it face up: an Events Phase reveal already sends it in, and this
    /// catches the other ways a province card is turned face up (a refill that comes up face up, a reshuffle's reveal, a card that turns a province card face up) so it never
    /// sits revealed in a province. The primary Dynasty slot only.</summary>
    private void EnterRevealedCelestials()
    {
        foreach (var player in _state.Players)
            foreach (var province in player.Provinces.ToArray())
            {
                // A refill after a Celestial leaves can itself come up face up and be a Celestial: keep going until the province holds none.
                for (var guard = 0; guard < 4 && province.CardId is string id && _state.Cards.TryGetValue(id, out var card) && card.FaceUp && card.Zone == Zone.Province && Definition(card).Type == CardType.Celestial; guard++)
                    ResolveCelestial(player, province, card);
            }
    }
}
