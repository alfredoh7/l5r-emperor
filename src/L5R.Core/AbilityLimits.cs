namespace L5R.Core;

/// <summary>The values of AbilityDefinition.UsesPerTurn other than an ordinary count.</summary>
public static class AbilityUseLimit
{
    /// <summary>"Any number of times per turn", "Repeatable", "tireless": the once-per-turn limit is off for this ability, and
    /// each use is limited only by its own cost and requirements.</summary>
    public const int Unlimited = 0;
}

public sealed partial class MatchEngine
{
    // The per-turn use limit (Hida Mochitoko, "Twice per turn", is the first N > 1; the once-per-turn default and the "any number of
    // times" exemptions are the same mechanism with a limit of 1 and of Unlimited).
    //
    // What is stored. AbilityUses[key] has always held the TURN NUMBER of the ability's latest use (key = its UsageKey, else its id),
    // and a use counts while that stamp equals the current turn; the turn number advances at the end of EVERY player's turn, so
    // "per turn" resets there. That stamp is unchanged for every ability, whatever its limit -- code and tests that read it keep
    // working. An ability whose limit is above one adds a second entry, "count:<key>", holding turn * UseCountTurnScale + uses that
    // turn. Both live in the same dictionary, so they are wiped together whenever the card's uses are (leaving play, returning to hand)
    // and carried together by an Experienced overlay (which keeps the larger value of each key: the composite grows with the turn and
    // then with the count, so the larger one is always the later use).
    private const string UseCountKeyPrefix = "count:";
    private const int UseCountTurnScale = 1024;

    /// <summary>How many times this ability has been used on this card (or by this player) in the current turn.</summary>
    private int AbilityUsesThisTurn(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        if (!uses.TryGetValue(key, out var stamp) || stamp != _state.TurnNumber) return 0;
        if (ability.UsesPerTurn <= 1) return 1;
        return uses.TryGetValue(UseCountKeyPrefix + key, out var counted) && counted / UseCountTurnScale == _state.TurnNumber ? counted % UseCountTurnScale : 1;
    }

    /// <summary>True once the ability has been used as many times this turn as its limit allows.</summary>
    private bool AbilityUseSpent(Dictionary<string, int> uses, AbilityDefinition ability, string key) =>
        ability.UsesPerTurn != AbilityUseLimit.Unlimited && AbilityUsesThisTurn(uses, ability, key) >= ability.UsesPerTurn;

    /// <summary>Records one use of the ability this turn.</summary>
    private void SpendAbilityUse(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        var used = ability.UsesPerTurn > 1 ? AbilityUsesThisTurn(uses, ability, key) + 1 : 1;
        uses[key] = _state.TurnNumber;
        if (ability.UsesPerTurn > 1) uses[UseCountKeyPrefix + key] = _state.TurnNumber * UseCountTurnScale + used;
    }
}
