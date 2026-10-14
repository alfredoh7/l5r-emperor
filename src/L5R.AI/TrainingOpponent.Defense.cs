using L5R.Core;

namespace L5R.AI;

// Issue #946 (Mendon's defense guidance, 2026-10-14): the Infantry Maneuvers defense when the attacker assigned nobody. See DefensePlanner for the arithmetic.
public static partial class TrainingOpponent
{
    /// <summary>A unit is Cavalry only when the Personality and every attached Follower carry the keyword (core rules, Attack Phase).</summary>
    private static bool IsCavalryUnit(PlayerView player, CardView personality) =>
        personality.Definition?.Keywords?.Contains("Cavalry") == true
        && player.Home.Where(c => c.AttachedTo == personality.Id && c.Definition?.Type == CardType.Follower).All(c => c.Definition?.Keywords?.Contains("Cavalry") == true);

    /// <summary>The Force of everything the attacker could send in the Cavalry Maneuvers Segment: its unbowed Cavalry units with their attachments.</summary>
    private static int PossibleCavalryForce(PlayerView attacker) =>
        attacker.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && IsCavalryUnit(attacker, c))
            .Sum(c => ArmyForce(attacker.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)));

    /// <summary>Province order for a defense: face-up Personalities by Gold cost, most expensive first, then provinces whose card is face down, then the rest.</summary>
    private static int[] ProvincesByValue(PlayerView self, IEnumerable<int> offered)
    {
        int Cost(ProvinceView p) => (p.DynastySlots?.Select(s => s.Card) ?? [p.Card]).OfType<CardView>().Where(c => c.FaceUp && c.Definition?.Type == CardType.Personality).Select(c => c.Definition!.GoldCost).DefaultIfEmpty(-1).Max();
        bool FaceDown(ProvinceView p) => (p.DynastySlots?.Select(s => s.Card) ?? [p.Card]).OfType<CardView>().Any(c => !c.FaceUp || c.Definition is null);
        return self.Provinces.Where(p => !p.Destroyed && offered.Contains(p.Index))
            .OrderBy(p => Cost(p) >= 0 ? 0 : FaceDown(p) ? 1 : 2).ThenByDescending(Cost).ThenBy(p => p.Index).Select(p => p.Index).ToArray();
    }

    /// <summary>The assignment that commits the infantry to the provinces it can hold against the attacker's possible Cavalry, or null: nobody is attacking yet and there is no Cavalry at home, or the defense cannot reach parity.</summary>
    private static Dictionary<string, int>? CavalryDefensePlan(Ctx x, ActionOption action, CardView[]? offeredUnits)
    {
        if (!x.Memory.UseCavalryDefense || x.View.Phase != Phase.DefenseAssignment || x.Opponent.Army.Length > 0) return null;
        var threat = PossibleCavalryForce(x.Opponent);
        if (threat == 0) return null;
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var units = (offeredUnits ?? x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id))).ToArray())
            .Select(c =>
            {
                var cards = x.Self.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id).ToArray();
                return new DefensePlanner.Unit(c.Id, ArmyForce(cards), !IsCavalryUnit(x.Self, c), cards.Any(HasBattleAction));
            }).ToArray();
        var provinces = ProvincesByValue(x.Self, action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index));
        return DefensePlanner.Plan(units, provinces, threat, x.Self.HandCount);
    }

    private static bool HasBattleAction(CardView card) => !card.Bowed && !card.ActionLocked && BattlePolicy.Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen");

    // ---- Attacking: Cavalry waits for its own segment (issue #946) --------------------------------------------------------------------------------------------------------

    /// <summary>The Personalities the bot already sent in this Attack Phase's Infantry Maneuvers Segment; the Cavalry joins them.</summary>
    private static CardView[] AlreadyAssigned(Ctx x) =>
        x.Memory.UseCavalryAttack && x.View.Phase == Phase.CavalryDeclaration ? x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality && c.ProvinceIndex is not null).ToArray() : [];

    /// <summary>The part of a whole-army plan that this segment may assign: the units it offers.</summary>
    private static Dictionary<string, int> OfferedOnly(Dictionary<string, int> plan, CardView[] offered) =>
        plan.Where(p => offered.Any(c => c.Id == p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>In the Infantry Maneuvers Segment the bot assigns only units that are not Cavalry; the Cavalry is assigned in the Cavalry Maneuvers Segment, when the defense is visible.</summary>
    private static Dictionary<string, int> HoldCavalry(Ctx x, Dictionary<string, int> assignments) =>
        !x.Memory.UseCavalryAttack || x.View.Phase != Phase.InfantryDeclaration ? assignments
            : assignments.Where(p => x.Self.Home.FirstOrDefault(c => c.Id == p.Key) is not { } unit || !IsCavalryUnit(x.Self, unit)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>The attack as the scores read it: the whole army, not only this segment's part. In the Infantry segment, the plan the infantry assignment is the part of; in the Cavalry segment, the infantry already sent plus these Cavalry.</summary>
    private static Dictionary<string, int> WholeAttack(Ctx x, ActionOption action, Dictionary<string, int>? assignments)
    {
        assignments ??= [];
        if (!x.Memory.UseCavalryAttack) return assignments;
        if (x.View.Phase == Phase.CavalryDeclaration)
        {
            var merged = new Dictionary<string, int>(assignments, StringComparer.Ordinal);
            foreach (var unit in AlreadyAssigned(x)) if (unit.ProvinceIndex is int province) merged.TryAdd(unit.Id, province);
            return merged;
        }
        if (x.View.Phase != Phase.InfantryDeclaration) return assignments;
        var (units, provinces) = AttackInputs(x, action);
        return PlanAttack(x, units, provinces) is { } whole && HoldCavalry(x, whole) is var part && part.Count == assignments.Count && part.All(p => assignments.TryGetValue(p.Key, out var sent) && sent == p.Value) ? whole : assignments;
    }
}
