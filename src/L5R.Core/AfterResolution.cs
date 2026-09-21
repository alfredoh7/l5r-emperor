namespace L5R.Core;

/// <summary>What a battle's resolution did, for the Reactions that follow it (issue #417): who destroyed what, who lost. Held while the after-resolution window is open.</summary>
public sealed class PendingAfterResolution
{
    public int ProvinceIndex { get; set; }
    public int Attacker { get; set; }
    public bool AttackerWon { get; set; }
    /// <summary>The attacking army as it stood at the resolution (the units that return home afterwards).</summary>
    public List<string> AttackerIds { get; set; } = [];
    /// <summary>Per player: how many enemy units this player's army destroyed in the resolution.</summary>
    public int[] DestroyedEnemyUnits { get; set; } = new int[2];
    /// <summary>The player who destroyed the province in the resolution (the Attacker, or none).</summary>
    public int? ProvinceDestroyedBy { get; set; }
    /// <summary>The battle's loser or losers: the Defender or the Attacker who was beaten; BOTH players in a tie (Mendon, issue #417); none when nothing was fought.</summary>
    public List<int> Losers { get; set; } = [];
}

// The after-resolution window (issue #417, Mendon's rulings of 2026-09-21). "After a battle's resolution ends" -- after the losing units are destroyed, the province is destroyed
// and Honor gained or lost, but while the winners are still at the battlefield -- either player may play the Reactions that name this moment, in alternating turns, until both pass:
//   - a card in a hand with an "after-battle-resolution" Reaction: Redoubled Attack (as many copies as the player holds, each its own play), Tales of Dishonor;
//   - a card in a discard pile that returns itself: Never Safe ("After this battle ends, if this card is in your discard pile, you may put it into your hand"). It is offered only
//     to the player who played it in THIS battle;
//   - a card in play with such a Reaction (none is wired yet, the shape is the same).
// The window opens only when somebody actually holds such a Reaction that could be played now: a player holding no Redoubled Attack is not asked about one.
// It comes before the winners' return home (which is why Redoubled Attack can keep them from bowing) and before the Rings' recruit window that follows the battle.
public sealed partial class MatchEngine
{
    private static bool IsAfterResolutionReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger == AfterResolutionTrigger;
    internal const string AfterResolutionTrigger = ReactionTriggers.AfterBattleResolution;

    private PendingAfterResolution NewAfterResolutionFacts(int provinceIndex, int attacker, bool attackerWon, CardInstance[] attackers, int[] destroyed, bool provinceDestroyed, List<int> losers) => new()
    {
        ProvinceIndex = provinceIndex, Attacker = attacker, AttackerWon = attackerWon, AttackerIds = attackers.Select(c => c.Id).ToList(),
        DestroyedEnemyUnits = destroyed, ProvinceDestroyedBy = provinceDestroyed ? attacker : null, Losers = losers
    };

    // The Reaction plays this player could make right now, asked with the window open exactly as they will be.
    private ActionOption[] AfterResolutionOptions(int playerId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("afterResolutionReaction", playerId, null, "After this battle's resolution: use a Reaction, or pass.");
        try { return AbilityActions(playerId).Where(o => o.Enabled).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    // Opens the window when a player has something to play. False (and nothing changes) when neither does.
    private bool OpenAfterResolutionWindow(PendingAfterResolution facts)
    {
        _state.PendingAfterResolution = facts;
        bool Holds(int player) => AfterResolutionOptions(player).Length > 0;
        if (!Holds(0) && !Holds(1)) { _state.PendingAfterResolution = null; return false; }
        var first = FirstReactionOpportunity(Holds);
        OpenAlternatingReactionWindow(new("afterResolutionReaction", first, null, "After this battle's resolution: you may use a Reaction, or pass.", facts.Attacker), first, "After-resolution window opened.");
        return true;
    }

    // What "the battle's resolution" did to the acting player, for a Reaction's own condition.
    private bool ResolutionDestroyedForPlayer(int playerId) =>
        _state.PendingAfterResolution is { } facts && (facts.DestroyedEnemyUnits[playerId] > 0 || facts.ProvinceDestroyedBy == playerId);

    // Each Reaction's own condition once the window is open (the trigger check in ValidateAbility calls this).
    private bool AfterResolutionApplies(CardInstance source, string abilityId, int playerId)
    {
        if (_state.PendingAfterResolution is not { } facts || source.OwnerId != playerId) return false;
        return abilityId switch
        {
            // Never Safe: in the discard pile, played in THIS battle.
            "never-safe-return" => source.Zone is Zone.FateDiscard && source.ReturnAfterBattle == _state.BattleNumber,
            // Redoubled Attack needs a Personality of the player's still at the battlefield: its performing slot has no candidate otherwise, so it is not playable.
            // Tales of Dishonor: "if you destroyed any enemy units or a province in that resolution" and somebody lost.
            "tales-of-dishonor-lose-honor" => ResolutionDestroyedForPlayer(playerId) && facts.Losers.Count > 0,
            _ => true
        };
    }

    // The window is over: the resolution goes on with the winners' return home (and the Conqueror's choice before it).
    private void FinishAfterResolutionWindow()
    {
        var facts = _state.PendingAfterResolution!;
        _state.PendingAfterResolution = null;
        ContinueAfterResolution(facts.ProvinceIndex, facts.Attacker, facts.AttackerWon, facts.AttackerIds.Where(_state.Cards.ContainsKey).Select(Card).ToArray());
    }

    // Redoubled Attack: "They will not bow from the battle's resolution" -- named cards, as opposed to a player's whole army (Akodo Dairuko).
    private bool ResolutionWillNotBow(CardInstance card) => _state.ResolutionWillNotBowCardIds.Contains(card.Id);
}
