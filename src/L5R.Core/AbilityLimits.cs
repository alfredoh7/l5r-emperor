namespace L5R.Core;

public static class AbilityUseLimit
{
    /// <summary>"Any number of times per turn": no per-turn limit on this ability.</summary>
    public const int Unlimited = 0;
}

public sealed partial class MatchEngine
{
    // AbilityUses[key] is the turn of the latest use, for every ability. A limit above one also keeps "count:<key>" = turn * scale + uses that
    // turn; both live in the same dictionary, so they are wiped and overlay-merged (larger value wins) together.
    private const string UseCountKeyPrefix = "count:";
    private const int UseCountTurnScale = 1024;

    private int AbilityUsesThisTurn(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        if (!uses.TryGetValue(key, out var stamp) || stamp != _state.TurnNumber) return 0;
        if (ability.UsesPerTurn <= 1) return 1;
        return uses.TryGetValue(UseCountKeyPrefix + key, out var counted) && counted / UseCountTurnScale == _state.TurnNumber ? counted % UseCountTurnScale : 1;
    }

    private bool AbilityUseSpent(Dictionary<string, int> uses, AbilityDefinition ability, string key) =>
        ability.UsesPerTurn != AbilityUseLimit.Unlimited && AbilityUsesThisTurn(uses, ability, key) >= ability.UsesPerTurn;

    private void SpendAbilityUse(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        var used = ability.UsesPerTurn > 1 ? AbilityUsesThisTurn(uses, ability, key) + 1 : 1;
        uses[key] = _state.TurnNumber;
        if (ability.UsesPerTurn > 1) uses[UseCountKeyPrefix + key] = _state.TurnNumber * UseCountTurnScale + used;
    }
}
