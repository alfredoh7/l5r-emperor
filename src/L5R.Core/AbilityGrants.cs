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
        if (copiedFrom is null && Delay(receiver, DelayedKinds.Ability, arg: $"{abilityId}/{duration}", sourceId: source)) return; // The shared delay mechanism (issue #492, EmperorRulingR2.cs).
        var grant = new GrantedAbility(abilityId, source, duration, copiedFrom, copiedFromName);
        if (!receiver.GrantedAbilities.Contains(grant)) receiver.GrantedAbilities.Add(grant);
    }

    // The Spirit Strategies (Second City): "Choose your performing unbowed Personality: If he is a [keyword], permanently give him [the ability]; otherwise, permanently
    // give him [keyword]." Returns whether he already had the keyword, i.e. whether the ability was the branch taken.
    private bool GrantSpirit(CardInstance performer, string keyword, string abilityId, string sourceId, int playerId)
    {
        if (!Keyword(performer, keyword))
        {
            GrantKeyword(performer, keyword, sourceId, GrantDuration.Permanent);
            return false;
        }
        GrantAbility(performer, abilityId, sourceId, GrantDuration.Permanent);
        Emit("abilityGranted", $"{Definition(performer).Name} permanently gains {AbilityCatalog.All[abilityId].Timing}: {AbilityCatalog.All[abilityId].Label}", playerId, performer.Id);
        return true;
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
