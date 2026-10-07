namespace L5R.Core;

// Primitive P24 (issue #690), Attacks. See EffectPrimitives.cs for the feature and docs/decisions/2026-10-06-690-p24-effect-primitives.md for the readings.
//
// A Ranged or Melee Attack targets "a Follower or a Personality without Followers in the current enemy army" and destroys it "if its Force is <= X". AttackSpec is where an ability says
// that ITS Attack differs in one of those two respects, as data:
//
//   TargetScope     DefenderHome: the Attack "may target a card in the Defender's home" (Strength in the Earth) -- the current enemy army, and also an enemy card in the home of the
//                   battle's Defender. AnyLocation: an enemy card wherever it stands, at any home or battlefield, in a battle or outside one (an Attack an Open or Limited action
//                   makes). Only the target's location is waived, and only for that Attack: it must still be an enemy Follower, or a Personality without Followers (or with an
//                   exception), and every "may not be targeted" rule still applies. The ability's own timing, presence and performer rules are untouched.
//   CompareAgainst  PrintedForce: the strength is compared with the Force printed on the target (its "base Force"), whatever modifiers, tokens and attachments it has. With
//                   CompareOptional it is the acting player's choice, made with the action (ruling R5: "may compare" is the player's choice): the command's Choice is
//                   AttackSpec.PrintedForceChoice, or AttackSpec.ForceChoice / none for the ordinary comparison, and the engine offers one action per choice.
//
// Read at the three places every Attack already passes: RequireLegalAttackTarget (declaration), ResolveAttackNow (the hit) and AttackComparedValue (the comparison).

/// <summary>Where an Attack may find its target.</summary>
public enum AttackTargetScope
{
    /// <summary>The rulebook's: the current enemy army.</summary>
    CurrentEnemyArmy,
    /// <summary>The current enemy army, or an enemy card in the Defender's home.</summary>
    DefenderHome,
    /// <summary>An enemy card in play at any home or battlefield.</summary>
    AnyLocation
}

/// <summary>What an Attack's strength is compared with.</summary>
public enum AttackCompare
{
    /// <summary>The rulebook's: the target's current Force.</summary>
    Force,
    /// <summary>The Force printed on the target.</summary>
    PrintedForce
}

/// <summary>How one ability's Attack differs from the rulebook's (AbilityDefinition.Attack, always passed by name).</summary>
public sealed record AttackSpec(AttackTargetScope TargetScope = AttackTargetScope.CurrentEnemyArmy, AttackCompare CompareAgainst = AttackCompare.Force, bool CompareOptional = false)
{
    /// <summary>The command's Choice for an optional comparison: the ordinary one, and the printed Force.</summary>
    public const string ForceChoice = "force", PrintedForceChoice = "printed-force";
}

public sealed partial class MatchEngine
{
    // RequireLegalAttackTarget's and ResolveAttackNow's question: does this Attack's scope reach the target where it stands? (The current enemy army is the caller's own first question.)
    private bool AttackSpecReaches(AttackSpec? spec, CardInstance target) => spec?.TargetScope switch
    {
        AttackTargetScope.DefenderHome => InDefendersHome(target),
        AttackTargetScope.AnyLocation => UnitOf(target) is { Zone: Zone.Home or Zone.Battlefield },
        _ => false
    };

    private bool AttackSpecReaches(string abilityId, CardInstance target) => AbilityCatalog.All.TryGetValue(abilityId, out var ability) && AttackSpecReaches(ability.Attack, target);

    // AttackComparedValue's first question: the value this ability's spec compares, or null for the ordinary comparison (no spec, or an optional one the player did not choose).
    private int? AttackSpecComparedValue(string abilityId, CardInstance target)
    {
        if (!AbilityCatalog.All.TryGetValue(abilityId, out var ability) || ability.Attack is not { CompareAgainst: AttackCompare.PrintedForce } spec) return null;
        if (spec.CompareOptional && AttackCompareChoiceOf(abilityId) != AttackSpec.PrintedForceChoice) return null;
        return Definition(target).Force;
    }

    // AttackCompareOptions' fallback (EmperorRulingR5.cs): the two things an optional printed-Force comparison lets the player choose between.
    private static (string Key, string Label)[]? AttackSpecCompareOptions(string abilityId) =>
        AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Attack is { CompareAgainst: AttackCompare.PrintedForce, CompareOptional: true }
            ? [(AttackSpec.ForceChoice, "Force"), (AttackSpec.PrintedForceChoice, "printed Force")] : null;
}
