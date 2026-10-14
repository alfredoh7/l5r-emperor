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
}
