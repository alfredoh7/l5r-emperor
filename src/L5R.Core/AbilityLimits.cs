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

    // The Fires of War (Emperor Edition remaining-cards pass, group C, issue #492): a Stronghold whose mark names this turn may use each of its abilities one more time (never an ability limited to once per game).
    private int EffectiveUsesPerTurn(Dictionary<string, int> uses, AbilityDefinition ability) =>
        ability.UsesPerTurn == AbilityUseLimit.Unlimited || ability.OncePerGame ? ability.UsesPerTurn : ability.UsesPerTurn + (uses.TryGetValue(SecondUseKey, out var marked) && marked == _state.TurnNumber ? 1 : 0) + P3AdditionalUses(uses) // Centering the Soul (issue #503): additional uses this turn
            + RegisteredExtraUsesPerTurn(uses, ability); // the registered additive channel (Mempo of the Amethyst Champion, issue #667, FavorMempo.cs)

    private int AbilityUsesThisTurn(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        if (!uses.TryGetValue(key, out var stamp) || stamp != _state.TurnNumber) return 0;
        if (EffectiveUsesPerTurn(uses, ability) <= 1) return 1;
        return uses.TryGetValue(UseCountKeyPrefix + key, out var counted) && counted / UseCountTurnScale == _state.TurnNumber ? counted % UseCountTurnScale : 1;
    }

    private bool AbilityUseSpent(Dictionary<string, int> uses, AbilityDefinition ability, string key) =>
        ability.UsesPerTurn != AbilityUseLimit.Unlimited && AbilityUsesThisTurn(uses, ability, key) >= EffectiveUsesPerTurn(uses, ability);

    private void SpendAbilityUse(Dictionary<string, int> uses, AbilityDefinition ability, string key)
    {
        var counted = EffectiveUsesPerTurn(uses, ability) > 1;
        var used = counted ? AbilityUsesThisTurn(uses, ability, key) + 1 : 1;
        uses[key] = _state.TurnNumber;
        if (counted) uses[UseCountKeyPrefix + key] = _state.TurnNumber * UseCountTurnScale + used;
    }
}
