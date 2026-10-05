namespace L5R.Core;

// Promotional-Celestial group D (reserved profiles item-wave-9401 / spell-wave-9402): All Within the World, Channeling the Fallen, Hunger of the Fire, Inexpensive Armor
// (a data-only card: AttackDefenseKind/Amount/Scope, no code here) and Colonist's Mark. Declarative abilities, hooks and execution are this file's own (a partial MatchEngine);
// the only shared-chain edit is one term in TargetedActionReactions.cs' TargetedReactionChoices default chain (Colonist's Mark's one option per card of the hand).
//
// Readings recorded here (none is a confirmed ruling; see docs/rules/RULINGS.md for those):
//   * All Within the World: "Either straighten the Ring or this Shugenja" is built as two Battle/Open entries sharing one use (the bow-or-straighten shape of Serenity in Air and
//     Togashi's Guidance): the choice is made as the action is announced, and every Ring you control that shares an element keyword with the Shugenja is a legal Ring to straighten.
//   * Hunger of the Fire: "Force lower than or equal to your Shugenja's Force or Chi" is Force <= the greater of the Shugenja's current Force and current Chi (either comparison
//     suffices); the unit Personality's Force is his current Force (CardForce), as every other Force comparison in the engine reads it.
public static class PromoCelestialDAbilities
{
    public const string AllWithinRing = "promo-d-all-within-ring", AllWithinSelf = "promo-d-all-within-self", AllWithinUsage = "promo-d-all-within";
    public const string ChannelingAttack = "promo-d-channeling-attack", ChannelingForceEffect = "promo-d-channeling-force";
    public const string HungerDestroy = "promo-d-hunger-fire-destroy", HungerTarget = "promo-d-unattached-unit-card-force-lte-performer-force-or-chi";
    public const string RingSharingTarget = "promo-d-ring-sharing-element-with-performer";
    public const string ColonistsMarkNegate = "promo-d-colonists-mark-negate-move";

    public static Dictionary<string, AbilityDefinition> AddPromoCelestialD(this Dictionary<string, AbilityDefinition> all)
    {
        // All Within the World (oracle-l5r-326, a Unique Spell): "Battle/Open: If this Shugenja shares an element keyword with a Ring you control: Either straighten the Ring or this Shugenja."
        all[AllWithinRing] = new("BattleOrOpen", "If this Shugenja shares an element keyword with a Ring you control: Straighten the Ring", RingSharingTarget,
            Performer: "own-wielder", UsageKey: AllWithinUsage);
        all[AllWithinSelf] = new("BattleOrOpen", "If this Shugenja shares an element keyword with a Ring you control: Straighten this Shugenja", "none",
            Performer: "own-wielder", UsageKey: AllWithinUsage);
        // Channeling the Fallen (oracle-l5r-1296, a Fire Spell): "This Shugenja has +5F. Battle: Bow this card: Ranged 5 Attack, or Ranged 6 Attack if this Shugenja is Fire." (Thunder's Favor's shape.)
        all[ChannelingAttack] = new("Battle", "Bow this card: Ranged 5 Attack, or Ranged 6 Attack if this Shugenja is Fire", "attack", Costs: ActionCost.BowSource);
        // Hunger of the Fire (oracle-l5r-3517, a Fire Spell): "Battle: Destroy this card, bow this Shugenja unless he is Fire, and target an enemy card without attachments in a unit whose
        // Personality has Force lower than or equal to your Shugenja's Force or Chi: Destroy the enemy card." (Earth's Hunger's shape.)
        all[HungerDestroy] = new("Battle", "Destroy this card, bow this Shugenja unless he is Fire, and target an enemy card without attachments in a unit whose Personality has Force lower than or equal to your Shugenja's Force or Chi: Destroy the enemy card",
            HungerTarget, Performer: "own-wielder", Costs: [new(CostVerb.Bow, CostSubject.Wielder, UnlessKeyword: "Fire"), new(CostVerbs.Sacrifice)]);
        // Colonist's Mark (oracle-l5r-1432, an Item): "Reaction: After an action targets this Personality, discard a card: Negate this Personality's movement from the action's effects."
        // (Hunger's shape, the discarded card being the Reaction's target.)
        all[ColonistsMarkNegate] = new("Reaction", "After an action targets this Personality, discard a card: Negate this Personality's movement from the action's effects", "hand",
            Trigger: ReactionTriggers.TargetedByAction, Costs: ActionCost.DiscardTarget);
        return all;
    }

}

public sealed partial class MatchEngine
{
    // The Rings a player controls that share an element keyword with the performing Shugenja.
    private CardInstance[] PromoDRingsSharingElement(CardInstance shugenja, int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Ring && RingElements.Any(e => Keyword(c, e) && Keyword(shugenja, e)))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();

    private CardInstance? PromoDPerformer(GameCommand command) =>
        command.PerformerId is { } id && _state.Cards.TryGetValue(id, out var performer) ? performer : null;

    private void ValidatePromoCelestialD(string id, GameCommand command, PlayerState player)
    {
        if (id != PromoCelestialDAbilities.AllWithinSelf || PromoDPerformer(command) is not { } shugenja) return;
        Require(PromoDRingsSharingElement(shugenja, player.Id).Length > 0, "This Shugenja shares no element keyword with a Ring you control.");
    }

    // Target categories (ValidateAbility's default chain; offered targets are judged through it too).
    private bool PromoCelestialDTargetAllows(string category, GameCommand command, PlayerState player, CardInstance target)
    {
        switch (category)
        {
            case PromoCelestialDAbilities.RingSharingTarget:
                return PromoDPerformer(command) is { } shugenja && PromoDRingsSharingElement(shugenja, player.Id).Any(r => r.Id == target.Id);
            case PromoCelestialDAbilities.HungerTarget:
                if (PromoDPerformer(command) is not { } performer || target.OwnerId == player.Id || !IsUnitCardWithoutAttachments(target)) return false;
                return CardForce(Card(UnitHostId(target))) <= Math.Max(CardForce(performer), PersonalityChi(performer));
            default:
                return false;
        }
    }

    private bool? RunPromoCelestialD(string id, CardInstance? source, CardInstance? target, PlayerState player, AbilityDefinition ability, GameCommand command, bool consumedGrant)
    {
        switch (id)
        {
            case PromoCelestialDAbilities.AllWithinRing:
                Straighten(target!);
                return false;
            case PromoCelestialDAbilities.AllWithinSelf:
                Straighten(Card(command.PerformerId!));
                return false;
            case PromoCelestialDAbilities.ChannelingAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant) ? true : false;
            case PromoCelestialDAbilities.HungerDestroy:
                DestroyUnits([target!], false, player.Id);
                return false;
            case PromoCelestialDAbilities.ColonistsMarkNegate:
                if (source?.AttachedTo is { } wielderId) NegateEffects(Card(wielderId), "move");
                return false;
            default:
                return null;
        }
    }

    // Channeling the Fallen: Ranged 5, or Ranged 6 if the performing Shugenja is Fire (sourceId is the performer).
    private static int? PromoDAttackStrength(MatchEngine engine, string abilityId, string? sourceId) =>
        abilityId == PromoCelestialDAbilities.ChannelingAttack ? 5 + (sourceId is not null && engine.Keyword(engine.Card(sourceId), "Fire") ? 1 : 0) : null;

    // Channeling the Fallen: "This Shugenja has +5F."
    private int PromoDPersonalityForce(CardInstance card) =>
        Keyword(card, "Shugenja") ? Attachments(card.Id).Count(c => Definition(c).Type == CardType.Spell && Definition(c).Effect == PromoCelestialDAbilities.ChannelingForceEffect) * 5 : 0;

    // Colonist's Mark: "After an action targets this Personality" -- its wielder, by any player's action.
    private bool PromoDTargetedReactionApplies(CardInstance card, string abilityId, CardInstance affected) =>
        abilityId == PromoCelestialDAbilities.ColonistsMarkNegate && card.Zone == Zone.Attached && affected.Id == card.AttachedTo;

    // One offered Reaction per card of the hand that may be discarded.
    private IEnumerable<(string? TargetId, string? Label)>? PromoDTargetedReactionChoices(CardInstance card, string abilityId) =>
        abilityId == PromoCelestialDAbilities.ColonistsMarkNegate
            ? _state.Cards.Values.Where(c => c.OwnerId == card.OwnerId && c.Zone == Zone.Hand).OrderBy(c => c.Id, StringComparer.Ordinal)
                .Select(c => ((string?)c.Id, (string?)$"{AbilityCatalog.All[abilityId].Label} · {Definition(c).Name} ({c.Id})")).ToArray()
            : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoCelestialDHooks()
    {
        EngineHooks.Abilities.Register("AddPromoCelestialD", 94010, static abilities => abilities.AddPromoCelestialD());
        EngineHooks.ValidateMiddle.Register("ValidatePromoCelestialD", 94010, static (engine, context) => engine.ValidatePromoCelestialD(context.id, context.command, context.player));
        EngineHooks.TargetAllows.Register("PromoCelestialDTargetAllows", 94010, static (engine, context) => engine.PromoCelestialDTargetAllows(context.ability.Target, context.command, context.player, context.target));
        EngineHooks.RunEffects.Register("RunPromoCelestialD", 94010, static (engine, context) => engine.RunPromoCelestialD(context.id, context.source, context.target, context.player, context.ability, context.command, context.consumedGrant));
        EngineHooks.AttackStrengthBefore.Register("PromoCelestialDAttackStrength", 94010, static (engine, context) => PromoDAttackStrength(engine, context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("PromoCelestialDAttackKind", 94010, static context => context.abilityId == PromoCelestialDAbilities.ChannelingAttack ? "Ranged" : null);
        EngineHooks.PersonalityForce.Register("PromoCelestialDPersonalityForce", 94010, static (engine, context) => engine.PromoDPersonalityForce(context.card));
        EngineHooks.TargetedReaction.Register("PromoCelestialDTargetedReaction", 94010, static (engine, context) => engine.PromoDTargetedReactionApplies(context.card, context.abilityId, context.affected));
    }
}
