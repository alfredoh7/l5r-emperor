namespace L5R.Core;

// Issue #639 (Mendon): after a Proclaim resolves, neither player is asked to pass a reaction window unless one of them can still take a reaction in it
// (a Reaction in hand, or a printed one in play, that the window offers). Left open, the window made both players pass for nothing.

public sealed partial class MatchEngine
{
    // Issue #644 (point 3, Mendon): a player who holds no legal Reaction in an alternating reaction window is never asked to pass it. These are the windows that
    // alternate between the two players; the others (before-action, engage, honor-change, ...) have their own continuations and keep their own prompts.
    private static readonly HashSet<string> SettledReactionWindows =
    [
        "proclaim", "entryReaction", "bowReaction", "moveReaction", "nikutaiReaction", "attackReaction", "duelReaction", "duelWonReaction",
        "afterResolutionReaction", "afterActionResolvedReaction", "battleRecruit", "battleActionReaction",
    ];

    /// <summary>True when the player, given the priority in the open reaction window, would be offered something other than passing (a Reaction, a Proclaim, a Ring to recruit...).</summary>
    private bool ReactionWindowOffers(int playerId)
    {
        var priority = _state.PriorityPlayerId;
        _state.PriorityPlayerId = playerId;
        try { return QueryActions(playerId).Any(a => a.Enabled && a.Type is not ("declineReaction" or "pass" or "concede" or "wait") && StillLegal(a)); }
        finally { _state.PriorityPlayerId = priority; }
    }

    // Some windows list a Reaction without checking it (a spent one stays "enabled"): a plain ability option counts as an offer only if the engine would still accept it.
    // Options that need a choice the listing cannot make for the player (targets, slots, destinations) are trusted as listed.
    private bool StillLegal(ActionOption option) =>
        option is not { Type: "useAbility", Command: { } command, TargetIds: null, Slots: null, Provinces: null } || AbilityRejection(command) is null;

    /// <summary>Called once an action in an alternating reaction window has resolved (or the other player has passed): a player who has nothing to react with is never asked to pass.
    /// The opportunity goes on to the other player if they hold a Reaction, and the window closes (play carries on normally) when neither does.
    /// The timing of the window closing tells the opponent that the other side held (or did not hold) a Reaction; this is the small information leak #639/#644 accept.</summary>
    private void SettleReactionWindow()
    {
        if (_state.PendingChoice is not { } pending || !SettledReactionWindows.Contains(pending.Type) || _state.PendingNegatableAbility is not null || ReactionOrderHolds()) return;
        var priority = _state.PriorityPlayerId;
        if (ReactionWindowOffers(priority)) return;
        if (ReactionWindowOffers(1 - priority)) { _state.PriorityPlayerId = 1 - priority; return; }
        CloseReactionWindow(pending, "No reaction was left to take; the window closed.");
    }

    /// <summary>A Reaction played in an open window: afterwards the window is settled, unless the Reaction opened a window of its own (that one was opened with its own rules).</summary>
    private void UseAbilityInWindow(GameCommand command)
    {
        var window = _state.PendingChoice;
        UseAbility(command);
        if (window is not null && Equals(_state.PendingChoice, window)) SettleReactionWindow();
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
