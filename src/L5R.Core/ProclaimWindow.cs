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

    /// <summary>Closes the Proclaim window the moment a Proclaim has resolved and no one has a reaction left to take in it.</summary>
    private void CloseProclaimWindowIfSpent()
    {
        if (_state.PendingChoice is not { Type: "proclaim" } pending || _state.PendingNegatableAbility is not null) return;
        if (ProclaimWindowOffers(0) || ProclaimWindowOffers(1)) return;
        CloseReactionWindow(pending, "The Proclaim resolved and no reaction was available; the window closed.");
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
