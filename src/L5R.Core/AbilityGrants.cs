namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Grant a predefined ability to this card, without changing its definition or resetting usage.
    // Keep independent sources/durations: a permanent grant must outlive an existing temporary one.
    private void GrantAbility(CardInstance receiver, string abilityId, string source, string duration = GrantDuration.Turn,
        string? copiedFrom = null, string? copiedFromName = null)
    {
        Require(AbilityCatalog.All.TryGetValue(abilityId, out var ability) && !ability.PlayerAbility,
            "Only a catalogued card ability can be granted.");
        Require(duration is GrantDuration.Turn or GrantDuration.Permanent or GrantDuration.Battle or GrantDuration.Attachment or GrantDuration.Copy,
            "Unknown ability grant duration.");
        var grant = new GrantedAbility(abilityId, source, duration, copiedFrom, copiedFromName);
        if (!receiver.GrantedAbilities.Contains(grant)) receiver.GrantedAbilities.Add(grant);
    }

    private void RemoveAttachmentGrants(CardInstance receiver, string source)
    {
        receiver.GrantedKeywords.RemoveAll(g => g.Source == source && g.Duration == GrantDuration.Attachment);
        receiver.GrantedAbilities.RemoveAll(g => g.Source == source && g.Duration == GrantDuration.Attachment);
    }

    private string GrantSourceName(GrantedAbility grant) =>
        grant.CopiedFromName ?? (_state.Cards.TryGetValue(grant.Source, out var source) ? Definition(source).Name :
        AbilityCatalog.All.TryGetValue(grant.Source, out var ability) ? ability.Label : grant.Source);

    private GrantedAbilityView[]? GrantedAbilityViews(CardInstance card)
    {
        var grants = card.GrantedAbilities.Where(g => GrantHolds(g.Duration, card)).Select(g =>
        {
            var ability = AbilityCatalog.All[g.AbilityId];
            var key = ability.UsageKey ?? g.AbilityId;
            var used = AbilityUsesThisTurn(card.AbilityUses, ability, key);
            return new GrantedAbilityView(g.AbilityId, ability.Label, DescribeTiming(ability), g.Source, g.Duration,
                g.CopiedFrom, g.CopiedFromName, GrantSourceName(g), ability.UsesPerTurn,
                ability.UsesPerTurn == AbilityUseLimit.Unlimited ? null : Math.Max(0, ability.UsesPerTurn - used));
        }).ToArray();
        return grants.Length == 0 ? null : grants;
    }
}
