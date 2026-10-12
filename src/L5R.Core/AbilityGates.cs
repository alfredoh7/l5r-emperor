namespace L5R.Core;

// Recurring per-ability preconditions as data (issue #847). Each used to be a hand-written `if (id == "...") Require(...)` line in a validation switch; the same four conditions
// recurred a dozen times with slightly different wording. A gate names the condition once, and ValidateAbility checks every gate an ability carries in one place.
[Flags]
public enum AbilityGate
{
    None = 0,
    /// <summary>Only the Attacker (the active player) may take the action.</summary>
    AttackerOnly = 1,
    /// <summary>Only the Defender (not the active player) may take the action.</summary>
    DefenderOnly = 2,
    /// <summary>The action is taken by putting the source card into play from its owner's hand (a Kata, Ring, Terrain...).</summary>
    SourceInHand = 4,
    /// <summary>The action is taken only while the source card is in play at home.</summary>
    SourceInPlay = 8,
    /// <summary>The source card must itself be at the current battlefield.</summary>
    SourceAtBattle = 16,
}

public sealed partial class MatchEngine
{
    private static readonly Dictionary<string, AbilityGate> AbilityGates = new(StringComparer.Ordinal)
    {
        ["earth-flows-move"] = AbilityGate.DefenderOnly,
        ["return-to-action-move"] = AbilityGate.DefenderOnly,
        ["blighted-region-move"] = AbilityGate.DefenderOnly,
        [EmperorP5HiddenAbilities.SearchingLook] = AbilityGate.DefenderOnly,
        ["cavalry-flankers-attack"] = AbilityGate.AttackerOnly,
        [EmperorP5HiddenAbilities.ScoutingPass] = AbilityGate.AttackerOnly,

        ["ring-of-air-recruit"] = AbilityGate.SourceInHand,
        ["ring-of-water-recruit"] = AbilityGate.SourceInHand,
        ["ring-of-earth-recruit"] = AbilityGate.SourceInHand,
        ["ring-of-fire-recruit"] = AbilityGate.SourceInHand,
        ["ring-of-void-recruit"] = AbilityGate.SourceInHand,
        ["false-ring-of-air-recruit"] = AbilityGate.SourceInHand,
        ["false-ring-of-earth-recruit"] = AbilityGate.SourceInHand,
        ["false-ring-of-water-recruit"] = AbilityGate.SourceInHand,
        ["skipping-the-puddle-enter"] = AbilityGate.SourceInHand,
        ["sound-the-retreat-enter"] = AbilityGate.SourceInHand,
        ["serenity-air-enter"] = AbilityGate.SourceInHand,
        ["unstable-ground-destroy"] = AbilityGate.SourceInHand,
        [EmperorP3PriorityAbilities.DestructiveKata] = AbilityGate.SourceInHand,
        [EmperorP3Abilities.DeepSnowsEnter] = AbilityGate.SourceInHand,
        [EmperorP5Abilities.BiteEnter] = AbilityGate.SourceInHand,
        [EmperorP5Abilities.EconEnter] = AbilityGate.SourceInHand,
        [PromoPaymentSourceAbilities.ProfitsEnter] = AbilityGate.SourceInHand,
        [Wave739Abilities.MaskReanimate] = AbilityGate.SourceInHand,
        [Wave740Abilities.KataEnter] = AbilityGate.SourceInHand,

        ["irezu-move"] = AbilityGate.SourceInPlay,
        ["skipping-the-puddle-move"] = AbilityGate.SourceInPlay,
        ["sound-the-retreat-move"] = AbilityGate.SourceInPlay,
        ["serenity-air-straighten"] = AbilityGate.SourceInPlay,
        ["serenity-air-straighten-open"] = AbilityGate.SourceInPlay,
        [EmperorP3PriorityAbilities.DestructiveReaction] = AbilityGate.SourceInPlay,

        ["dairuko-rally"] = AbilityGate.SourceAtBattle,
        ["tsuruchi-yakusho-immunity"] = AbilityGate.SourceAtBattle,
        ["hawado-exp-move-home"] = AbilityGate.SourceAtBattle,
        ["hawado-exp-move-battlefield"] = AbilityGate.SourceAtBattle,
    };

    internal static IReadOnlyDictionary<string, AbilityGate> AbilityGateTable => AbilityGates;

    private void RequireAbilityGates(string id, PlayerState player, CardInstance? source)
    {
        if (!AbilityGates.TryGetValue(id, out var gates)) return;
        if (gates.HasFlag(AbilityGate.AttackerOnly)) Require(player.Id == _state.ActivePlayerId, "Only the Attacker may take this action.");
        if (gates.HasFlag(AbilityGate.DefenderOnly)) Require(player.Id != _state.ActivePlayerId, "Only the Defender may take this action.");
        if (source is null) return;
        if (gates.HasFlag(AbilityGate.SourceInHand)) Require(source.Zone == Zone.Hand && player.Hand.Contains(source.Id), "This card is put into play from your hand.");
        if (gates.HasFlag(AbilityGate.SourceInPlay)) Require(source.Zone == Zone.Home, "This card must be in play.");
        if (gates.HasFlag(AbilityGate.SourceAtBattle)) Require(AtBattle(source), "This card must be at the current battlefield.");
    }
}
