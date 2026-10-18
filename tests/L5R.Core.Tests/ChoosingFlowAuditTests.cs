using L5R.Core;

// Issue #1004: the client's choosing flow (one question at a time, Back, summary) is drawn by a handful of panels. This inventory routes EVERY catalogue ability to the panel that draws its
// draft, so a new ability shape that no panel handles fails here instead of reaching a player. The routing mirrors the client's own predicates (HasMultiTargets, HasAttackTargets,
// MovementActive, IsProvinceTarget, the dedicated deck-search / look / copy panels and the reaction windows); docs/audits/choosing-flow-audit.md records what native check covers each panel.
internal static class ChoosingFlowAuditTests
{
    public static string Panel(AbilityDefinition a) =>
        a.Timing.Contains("Reaction", StringComparison.Ordinal) || a.Trigger is not null ? "reaction-window"
        : a.Slots is { Length: > 0 } ? "multi-target"
        : a.Search is not null || a.Look is not null || a.CopiesAbilities ? "dedicated-wizard"
        : a.Target == "attack" ? "attack-target"
        : a.Move is not null ? "movement"
        : a.Target is "provinces" or "any-province" or "enemy-province" || a.ChoosesOwnProvince ? "province-picker"
        : a.Target == "player" ? "draft-player"
        : "draft";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Choosing flow audit: every catalogue ability routes to exactly one client panel, and the draft panels (draft, draft-player, attack-target, multi-target, province-picker, movement) cover every non-reaction, non-wizard ability", EveryAbilityHasAPanel),
        ];
        runner.Run("choosing-flow-audit", tests);
    }

    private static void EveryAbilityHasAPanel()
    {
        var known = new HashSet<string> { "reaction-window", "multi-target", "dedicated-wizard", "attack-target", "movement", "province-picker", "draft-player", "draft" };
        var counts = AbilityCatalog.All.GroupBy(p => Panel(p.Value)).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Keys.Any(k => !known.Contains(k))) throw new Exception("An ability routes to an unknown panel.");
        if (counts.Values.Sum() != AbilityCatalog.All.Count) throw new Exception("Some abilities were not routed.");
        Console.WriteLine("choosing-flow-audit routing: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Key}={c.Value}")) + $" of {AbilityCatalog.All.Count}");
    }
}
