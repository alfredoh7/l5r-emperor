using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PRIMITIVE P24 (issue #690, docs/decisions/2026-10-06-690-p24-effect-primitives.md): SMALL EFFECT PRIMITIVES. Engine only: no card is wired here.
//
// Eight small pieces the unwired cards of issue #668 name, each a helper a card-wiring wave calls, with one declarative field where that is cheap. Several turned out to exist already
// (the decision doc says which); those are reused, never rebuilt.
//
//   EffectPrimitivesMoves.cs    SwitchLocations(a, b, source): the glossary's Switch -- both units move or neither does; a negation of one side's movement negates the other's.
//                               AdjacentUnresolvedBattlefields / MoveToAdjacentBattlefield, beside the existing MoveSpec.AdjacentUnresolved, and the mover MoveSpec.Wielder.
//   EffectPrimitivesAttack.cs   AttackSpec on AbilityDefinition: TargetScope (the Defender's home, or any location: the Rules of Location are waived for that Attack's target only) and
//                               CompareAgainst (the target's printed Force, mandatory or as the player's "may compare" choice).
//   EffectPrimitivesCosts.cs    The cost-substitution store ("you may bow this card instead of bowing a Ring to pay a cost"), the cost-waiver channel ("he need not bow for his Spells with
//                               an odd Focus Value"), and the target categories and builders for "remove N [keyword] cards you control / a card in any discard pile from the game" and
//                               "destroy one of his Spells" as costs (the verbs themselves exist: RemoveSlotCards and Sacrifice).
//   EffectPrimitivesTokens.cs   CreateMultiStatToken (a token with up to three stats), ShuffleIntoDeck, ShuffleHandsAndRedraw, and the control return owed when its source straightens.
//
// This file: the feature's state, its fixtures (abilities whose ids start with "training-p24-": no catalogue card prints them; a test grants one to a fixture card) and its registrations.
// ======================================================================================================================================================================================

/// <summary>What the small effect primitives keep between commands (issue #690).</summary>
public sealed class EffectPrimitivesState
{
    /// <summary>Cards that may be bowed in a Ring's place to pay a cost (Harmony's permanent trait), in the order granted.</summary>
    public List<CostSubstitutionGrant> CostSubstitutions { get; set; } = [];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(22000)]
    public EffectPrimitivesState EffectPrimitives { get; set; } = new();
}

/// <summary>The fixture abilities of primitive P24. None is printed on a catalogue card.</summary>
public static class EffectPrimitiveFixtures
{
    public const string Switch = "training-p24-switch", MoveWielderAdjacent = "training-p24-move-wielder-adjacent",
        AttackHome = "training-p24-attack-home", AttackAnywhere = "training-p24-attack-anywhere", AttackPrinted = "training-p24-attack-printed", AttackMayPrinted = "training-p24-attack-may-printed",
        ShuffleDiscards = "training-p24-shuffle-discards", ShuffleRedraw = "training-p24-shuffle-redraw",
        SpellBowWielder = "training-p24-spell-bow-wielder", RingTargetBow = "training-p24-bow-target-ring",
        RemoveKoan = "training-p24-remove-koan", RemoveDiscard = "training-p24-remove-discard", DestroyOwnSpell = "training-p24-destroy-own-spell",
        ThreeStatToken = "training-p24-three-stat-token", TakeUntilStraighten = "training-p24-take-until-straighten", Judgment = "training-p24-judgment";

    /// <summary>The Effect of a fixture attachment whose wearer "need not bow for his Spells with an odd / even Focus Value".</summary>
    public const string OddFocusWaiver = "training-p24-odd-focus-waiver", EvenFocusWaiver = "training-p24-even-focus-waiver";

    public static Dictionary<string, AbilityDefinition> AddEffectPrimitiveFixtures(this Dictionary<string, AbilityDefinition> all)
    {
        // Shinjo Kinto - exp2's shape: "Target two Personalities at different locations controlled by the same player. Switch their locations." (Sudden Movement's two slots.)
        all[Switch] = new("Battle", "Training: target two Personalities of one player at different locations: switch their locations", "slots",
            Slots: [new("first", "personality-with-switch-partner", "A Personality"), new("second", "personality-same-controller-as-first-elsewhere", "Another Personality of the same player's, at a different location")]);
        // Befriend the Tides' shape, on a Spell: "Bow this card: Move this Shugenja from the current battlefield to an adjacent battlefield."
        all[MoveWielderAdjacent] = new("Battle", "Training: bow this card: move this Shugenja to an adjacent unresolved battlefield", "none", Costs: ActionCost.BowSource,
            Move: new(MoveSpec.Wielder, MoveSpec.AdjacentUnresolved));
        // Strength in the Earth's shape: "Melee Attack ..., which may target a card in the Defender's home."
        all[AttackHome] = new("Battle", "Training: Melee 3 Attack, which may target a card in the Defender's home", "attack", Attack: new(TargetScope: AttackTargetScope.DefenderHome));
        // An Attack outside a battle, at a card wherever it stands (Ikoma Satoru - exp's "targeting a Follower or Personality without Followers in his home").
        all[AttackAnywhere] = new("Open", "Training: Ranged 3 Attack, which may target an enemy card at any location", "attack", Attack: new(TargetScope: AttackTargetScope.AnyLocation));
        all[AttackPrinted] = new("Battle", "Training: Melee 3 Attack compared against the target's printed Force", "attack", Attack: new(CompareAgainst: AttackCompare.PrintedForce));
        // "... may be compared against the target's base Force": the player's choice (ruling R5), carried on the command.
        all[AttackMayPrinted] = new("Battle", "Training: Melee 3 Attack that you may compare against the target's printed Force", "attack",
            Attack: new(CompareAgainst: AttackCompare.PrintedForce, CompareOptional: true));
        // Sorrowful Prayer's shape: "Shuffle one to three target non-Unique Strategies in your discard pile into your deck."
        all[ShuffleDiscards] = new("Limited", "Training: shuffle one to three target non-Unique Strategies in your discard pile into your deck", "slots",
            Slots: [new("cards", "own-nonunique-strategy-in-discard", "One to three non-Unique Strategies in your discard pile", 1, 3)]);
        // Overwhelming Chaos' shape: "Each player shuffles his hand into his deck, then draws as many cards as he shuffled into his deck."
        all[ShuffleRedraw] = new("Open", "Training: each player shuffles his hand into his deck, then draws as many cards", "none");
        // A Spell that prints "Bow this Shugenja" (the cost Koan's Jingasa and Koan's Robes waive).
        all[SpellBowWielder] = new("Open", "Training: bow this Shugenja: gain 1 Honor", "none", Costs: ActionCost.BowWielder);
        // "Bow your target unbowed Ring": a Ring bowed to pay another card's cost.
        all[RingTargetBow] = new("Open", "Training: bow your target unbowed Ring: gain 1 Honor", "own-unbowed-ring", Costs: ActionCost.BowTarget);
        // Koan's Whisper's shape, with two cards in place of five: "remove N Koan cards you control from the game".
        var (koanSlot, koanCost) = EffectPrimitiveCosts.RemoveOwnCardsWithKeyword("Koan", 2);
        all[RemoveKoan] = new("Limited", "Training: remove two Koan cards you control from the game: gain 1 Honor", "slots", Slots: [koanSlot], Costs: [koanCost]);
        // Scorching Lash's shape: "Remove a non-Ring card in any discard pile from the game, and target a Personality: Give him +3F."
        var (fuelSlot, fuelCost) = EffectPrimitiveCosts.RemoveFromAnyDiscard(exceptType: CardType.Ring);
        all[RemoveDiscard] = new("Open", "Training: remove a non-Ring card in any discard pile from the game, and target a Personality: give him +3F", "slots",
            Slots: [fuelSlot, new("target", "personality", "A Personality")], Costs: [fuelCost]);
        // Tamori Tsuchiya's shape: "destroy one of his Spells: ...".
        var (spellSlot, spellCost) = EffectPrimitiveCosts.DestroyOwnAttachment(CardType.Spell);
        all[DestroyOwnSpell] = new("Open", "Training: destroy one of this Personality's Spells: give him +2F", "slots", Slots: [spellSlot], Costs: [spellCost]);
        // A Doomseeker's Tomb's shape: "Give your target Personality a +1F/+1C/+1PH token."
        all[ThreeStatToken] = new("Limited", "Training: give your target Personality a +1F/+1C/+1PH token", "own-personality");
        // P'an Ku - exp's shape: "Bow this card: Take control of a target Personality. After this card straightens or leaves play, give control of the target to his owner."
        all[TakeUntilStraighten] = new("Limited", "Training: bow this card: take control of a target enemy Personality until this card straightens or leaves play", "enemy-personality", Costs: ActionCost.BowSource);
        // Kitsuki Judgment's price: "pay Gold equal to his unit's total Gold Cost, +8 unless he is dishonorable" -- an existing rule (TargetUnitTotal) plus a P2 cost term.
        all[Judgment] = new("Limited", "Training: target a Personality and pay Gold equal to his unit's total Gold Cost, +8 unless he is dishonorable: gain 1 Honor", "personality", Cost: GoldCostRule.TargetUnitTotal());
        return all;
    }
}

public sealed partial class MatchEngine
{
    private EffectPrimitivesState EffectPrimitives => _state.EffectPrimitives;

    private static string? EffectPrimitiveAttackKind(string abilityId) => abilityId switch
    {
        EffectPrimitiveFixtures.AttackHome or EffectPrimitiveFixtures.AttackPrinted or EffectPrimitiveFixtures.AttackMayPrinted => "Melee",
        EffectPrimitiveFixtures.AttackAnywhere => "Ranged",
        _ => null
    };

    private static int? EffectPrimitiveAttackStrength(string abilityId) => EffectPrimitiveAttackKind(abilityId) is null ? null : 3;

    // The fixtures' effects: null when the id is not one of them; false when its effects are done; true when a window opened.
    private bool? RunEffectPrimitiveFixture(RunHookContext context)
    {
        var (id, ability, command, player, source, target) = (context.id, context.ability, context.command, context.player, context.source, context.target);
        var name = source is null ? "An action" : Definition(source).Name;
        switch (id)
        {
            case EffectPrimitiveFixtures.Switch:
                SwitchLocations(SlotCards(command, "first").Single(), SlotCards(command, "second").Single(), name);
                return false;
            case EffectPrimitiveFixtures.MoveWielderAdjacent:
                ExecuteMove(command, ability, source, target);
                return false;
            case EffectPrimitiveFixtures.AttackHome:
            case EffectPrimitiveFixtures.AttackAnywhere:
            case EffectPrimitiveFixtures.AttackPrinted:
            case EffectPrimitiveFixtures.AttackMayPrinted:
                return ResolveOrDeferAttack(id, player, target?.Id, source is null ? null : ActionPerformer(ability, command, source).Id, context.consumedGrant);
            case EffectPrimitiveFixtures.ShuffleDiscards:
                ShuffleIntoDeck(SlotCards(command, "cards"), source!);
                return false;
            case EffectPrimitiveFixtures.ShuffleRedraw:
                ShuffleHandsAndRedraw([player.Id, 1 - player.Id], source);
                return false;
            case EffectPrimitiveFixtures.SpellBowWielder:
            case EffectPrimitiveFixtures.RingTargetBow:
            case EffectPrimitiveFixtures.RemoveKoan:
            case EffectPrimitiveFixtures.Judgment:
                GainHonor(player.Id, 1, name, source);
                return false;
            case EffectPrimitiveFixtures.RemoveDiscard:
                ApplyModifier(SlotCards(command, "target").Single(), source!.Id, id, "Force", 3);
                return false;
            case EffectPrimitiveFixtures.DestroyOwnSpell:
                ApplyModifier(source!, source!.Id, id, "Force", 2);
                return false;
            case EffectPrimitiveFixtures.ThreeStatToken:
                CreateMultiStatToken(target!, "Tomb", source!.Id, ("Force", 1), ("Chi", 1), ("PersonalHonor", 1));
                return false;
            case EffectPrimitiveFixtures.TakeUntilStraighten:
                if (TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true)) ReturnControlWhenSourceStraightens(target!, source!, $"{name}: after it straightens or leaves play");
                return false;
            default:
                return null;
        }
    }

    // Kitsuki Judgment's "+8 unless he is dishonorable, Kolat, Ninja, or Shadowlands", as the fixture prices it: a P2 ability cost term (CostTerms.cs) read off the command's target.
    private IEnumerable<CostTerm>? EffectPrimitiveFixtureCostTerms(AbilityCostContext context) =>
        context.AbilityId == EffectPrimitiveFixtures.Judgment && context.Command?.TargetId is { } targetId && _state.Cards.TryGetValue(targetId, out var target) && !target.Dishonorable
            ? [new CostTerm(8, "he is not dishonorable")] : null;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEffectPrimitiveHooks()
    {
        EngineHooks.Abilities.Register("P24AddEffectPrimitiveFixtures", 16000, static abilities => abilities.AddEffectPrimitiveFixtures());
        EngineHooks.TargetAllows.Register("P24EffectPrimitiveTargetAllows", 16000, static (engine, context) => engine.EffectPrimitiveTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.AttackKind.Register("P24EffectPrimitiveAttackKind", 16000, static context => EffectPrimitiveAttackKind(context.abilityId));
        EngineHooks.AttackStrengthBefore.Register("P24EffectPrimitiveAttackStrength", 16000, static (engine, context) => EffectPrimitiveAttackStrength(context.abilityId));
        EngineHooks.RunEffects.Register("P24RunEffectPrimitiveFixture", 16000, static (engine, context) => engine.RunEffectPrimitiveFixture(context));
        EngineHooks.AbilityCostTerms.Register("P24EffectPrimitiveFixtureCostTerms", 16000, static context => context.Engine.EffectPrimitiveFixtureCostTerms(context));
        EngineHooks.CostWaivers.Register("P24EffectPrimitiveFixtureWaivers", 16000, static context =>
            context.Engine.SpellFocusParityWaives(context, EffectPrimitiveFixtures.OddFocusWaiver, odd: true) || context.Engine.SpellFocusParityWaives(context, EffectPrimitiveFixtures.EvenFocusWaiver, odd: false));
    }
}
