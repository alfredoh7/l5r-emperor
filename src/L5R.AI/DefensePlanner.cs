namespace L5R.AI;

/// <summary>
/// Issue #946 (Mendon's defense guidance, 2026-10-14). The attacker committed no infantry but has Cavalry at home, so the defender's own infantry must be assigned now or not at all.
/// The attacker's total possible Cavalry army is the threat. The defender's Force divided by it is the ratio; below 1, each hand card and each printed Battle action on a defending unit
/// adds 0.1 until it reaches 1. If it cannot, the bot does not defend. Otherwise the first army defends the most valuable province, and every further sub-army that reaches 1 by Force
/// alone defends the next most valuable. Units that cannot form another army stay home, unless they print a Battle action, in which case they join the first army.
/// Cavalry defenders are always held back: the attacker still has unassigned Cavalry, so they are assigned in the Cavalry Maneuvers Segment, when the real attack is visible. Their Force
/// counts toward the ratio, because they will defend then.
/// Pure and deterministic; only public numbers go in.
/// </summary>
public static class DefensePlanner
{
    /// <param name="Infantry">Not Cavalry: it can only be assigned in this segment. Cavalry is never assigned here; it waits for the Cavalry Maneuvers Segment.</param>
    public sealed record Unit(string Id, int Force, bool Infantry, bool HasBattleAction);

    /// <param name="threat">The attacker's total possible Cavalry army Force.</param>
    /// <param name="handCards">Cards in the defender's hand (each adds 0.1 to the ratio of the first army).</param>
    /// <param name="provinces">Provinces in order of value, most valuable first.</param>
    public static Dictionary<string, int>? Plan(IReadOnlyList<Unit> units, IReadOnlyList<int> provinces, int threat, int handCards)
    {
        var ready = units.Where(u => u.Force > 0 || u.HasBattleAction).ToArray();
        if (threat <= 0 || ready.Length == 0 || provinces.Count == 0 || ready.All(u => !u.Infantry)) return null;
        var total = ready.Sum(u => u.Force);
        var cards = handCards + ready.Count(u => u.HasBattleAction);
        if (!Reaches(total, cards, threat)) return null;
        var pool = ready.Where(u => u.Infantry).OrderByDescending(u => u.Force).ThenBy(u => u.Id, StringComparer.Ordinal).ToList();
        var plan = new Dictionary<string, int>(StringComparer.Ordinal);
        var armies = 0;
        while (armies < provinces.Count && pool.Count > 0)
        {
            var army = new List<Unit>();
            var force = 0;
            var bonus = armies == 0 ? cards : 0; // the hand and the Battle actions back the first army only
            while (pool.Count > 0 && (army.Count == 0 || !Reaches(force, bonus, threat))) { army.Add(pool[0]); force += pool[0].Force; pool.RemoveAt(0); }
            if (army.Count == 0 || !Reaches(force, bonus, threat))
            {
                // All the infantry together cannot make an army; with the held-back Cavalry to join it later it still goes to the most valuable province.
                if (armies == 0) foreach (var unit in army) plan[unit.Id] = provinces[0];
                else pool.InsertRange(0, army);
                break;
            }
            foreach (var unit in army) plan[unit.Id] = provinces[armies];
            armies++;
        }
        if (plan.Count == 0) return null;
        // Fractional leftovers: kept home, or committed to the first army when they have a Battle action to take.
        foreach (var unit in pool.Where(u => u.HasBattleAction)) plan[unit.Id] = provinces[0];
        return plan;
    }

    /// <summary>Force / threat + 0.1 per card >= 1, in integers.</summary>
    private static bool Reaches(int force, int cards, int threat) => 10 * force + cards * threat >= 10 * threat;
}
