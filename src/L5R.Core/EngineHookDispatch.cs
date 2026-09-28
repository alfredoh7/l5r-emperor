namespace L5R.Core;

public sealed partial class MatchEngine
{
    internal readonly record struct AttackStrengthBeforeContext(string abilityId, PlayerState player, CardInstance? target, string? sourceId);
    private int? RegisteredAttackStrengthBefore(string abilityId, PlayerState player, CardInstance? target, string? sourceId)
    {
        var context = new AttackStrengthBeforeContext(abilityId, player, target, sourceId);

        foreach (var hook in EngineHooks.AttackStrengthBefore.Items)
            if (hook(this, context) is { } result) return result;
        return null;
    }
    internal readonly record struct AttackStrengthAfterContext(string abilityId, PlayerState player, CardInstance? target, string? sourceId);
    private int? RegisteredAttackStrengthAfter(string abilityId, PlayerState player, CardInstance? target, string? sourceId)
    {
        var context = new AttackStrengthAfterContext(abilityId, player, target, sourceId);

        foreach (var hook in EngineHooks.AttackStrengthAfter.Items)
            if (hook(this, context) is { } result) return result;
        return null;
    }
    internal readonly record struct AttackKindContext(string abilityId);
    private static string? RegisteredAttackKind(string abilityId)
    {
        var context = new AttackKindContext(abilityId);

        foreach (var hook in EngineHooks.AttackKind.Items)
            if (hook(context) is { } result) return result;
        return null;
    }
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    internal readonly record struct PerformerAllowsContext(AbilityDefinition ability, CardInstance performer, bool actingAsBattle);
    private bool RegisteredPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle)
    {
        var context = new PerformerAllowsContext(ability, performer, actingAsBattle);

        foreach (var hook in EngineHooks.PerformerAllows.Items)
            if (hook(this, context)) return true;
        return false;
    }
    internal readonly record struct TargetAllowsContext(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance target);
    private bool RegisteredTargetAllows(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var context = new TargetAllowsContext(ability, command, player, source, target);

        foreach (var hook in EngineHooks.TargetAllows.Items)
            if (hook(this, context)) return true;
        return false;
    }
    internal readonly record struct PerformerCandidatesContext(AbilityDefinition ability, int playerId, bool actingAsBattle);
    private string?[]? RegisteredPerformerCandidates(AbilityDefinition ability, int playerId, bool actingAsBattle)
    {
        var context = new PerformerCandidatesContext(ability, playerId, actingAsBattle);

        foreach (var hook in EngineHooks.PerformerCandidates.Items)
            if (hook(this, context) is { } result) return result;
        return null;
    }
    internal readonly record struct DestroysTargetContext(string abilityId);
    private static bool RegisteredDestroysTarget(string abilityId)
    {
        var context = new DestroysTargetContext(abilityId);

        foreach (var hook in EngineHooks.DestroysTarget.Items)
            if (hook(context)) return true;
        return false;
    }
    internal readonly record struct BowsTargetContext(string abilityId);
    private static bool RegisteredBowsTarget(string abilityId)
    {
        var context = new BowsTargetContext(abilityId);

        foreach (var hook in EngineHooks.BowsTarget.Items)
            if (hook(context)) return true;
        return false;
    }
    internal readonly record struct PersonalityForceContext(CardInstance card);
    private int RegisteredPersonalityForce(CardInstance card)
    {
        var context = new PersonalityForceContext(card);

        var total = 0;
        foreach (var hook in EngineHooks.PersonalityForce.Items)
            total += hook(this, context);
        return total;
    }
    internal readonly record struct ProvinceBonusContext(CardInstance card, bool mine);
    private int RegisteredProvinceBonus(CardInstance card, bool mine)
    {
        var context = new ProvinceBonusContext(card, mine);

        var total = 0;
        foreach (var hook in EngineHooks.ProvinceBonus.Items)
            total += hook(this, context);
        return total;
    }
    internal readonly record struct TargetedReactionContext(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected);
    private bool RegisteredTargetedReaction(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var context = new TargetedReactionContext(card, abilityId, subject, affected);

        foreach (var hook in EngineHooks.TargetedReaction.Items)
            if (hook(this, context)) return true;
        return false;
    }
}
