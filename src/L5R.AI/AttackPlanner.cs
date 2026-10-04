namespace L5R.AI;

/// <summary>
/// Issue #642 (Mendon's spec): "If a deck has enough force to overcome the entire enemy army, it should divide its army into as many sub armies as
/// satisfy this restriction and attack multiple provinces such that the minimum force of each army is greater than the province strength of the
/// province and the unbowed units that could defend it."
///
/// A sub-army sent at a province must have more Force than that province's Strength plus the Force of every unbowed enemy unit that could defend it
/// (any unbowed enemy home unit can defend any province, so all of them are counted against each army: the safe reading). The planner picks the
/// largest number of the weakest provinces it can man this way and spreads the units across them; units left over join the army facing the strongest
/// of the chosen provinces. With only one army possible it is the old "everything at the weakest province" attack. If not even one army clears its
/// threshold the answer is null (do not attack). Pure and deterministic; only public numbers go in.
/// </summary>
public static class AttackPlanner
{
    public sealed record Unit(string Id, int Force);
    public sealed record Target(int Index, int Strength);

    public static Dictionary<string, int>? Plan(IReadOnlyList<Unit> units, IReadOnlyList<Target> provinces, int defenderForce)
    {
        var ready = units.Where(u => u.Force > 0).OrderByDescending(u => u.Force).ThenBy(u => u.Id, StringComparer.Ordinal).ToArray();
        var targets = provinces.OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        if (ready.Length == 0 || targets.Length == 0) return null;
        for (var k = Math.Min(targets.Length, ready.Length); k >= 1; k--)
        {
            var chosen = targets.Take(k).ToArray();
            if (TryPartition(ready, chosen, defenderForce) is { } plan) return plan;
        }
        return null;
    }

    private static Dictionary<string, int>? TryPartition(Unit[] units, Target[] chosen, int defenderForce)
    {
        // Each army needs strictly more than Strength + defenders.
        var need = chosen.Select(t => t.Strength + defenderForce + 1).ToArray();
        var force = new int[chosen.Length];
        var groups = chosen.Select(_ => new List<Unit>()).ToArray();
        foreach (var unit in units)
        {
            // Largest remaining deficit first; once every army is satisfied, the rest go where the margin is thinnest (the strongest province).
            var deficits = need.Select((n, i) => n - force[i]).ToArray();
            var i = Array.IndexOf(deficits, deficits.Max());
            if (deficits[i] <= 0) i = Array.IndexOf(need, need.Max());
            groups[i].Add(unit); force[i] += unit.Force;
        }
        if (force.Where((f, i) => f < need[i]).Any()) return null;
        var plan = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < chosen.Length; i++) foreach (var unit in groups[i]) plan[unit.Id] = chosen[i].Index;
        return plan;
    }
}
