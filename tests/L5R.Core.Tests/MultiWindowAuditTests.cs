using L5R.Core;
using static RegressionAssertions;

// Issue #830: every wired card with more than one action window (several printed actions, Battle/Open, Battle/Limited, Open + Battle,
// Reaction + Battle ...) is probed in each phase an action may be taken in. A shared "kitchen sink" table gives the card every precondition a
// generic fixture can (Gold, performers carrying every keyword the card's abilities name, enemies at the battlefield); then, per ability and
// per scenario, the engine's own QueryActions says whether the action is on offer. Printed Battle/Open must be on offer in both phases, Battle only
// in the Battle phase, Open in either player's Action phase, Limited only for the active player's Action phase. An accepted offer is also executed.
internal static partial class MultiWindowAuditTests
{
    public record Probe(string Card, string Name, string Ability, string Timing, string Scenario, bool Offered, bool Enabled, string? Reason, bool Executed, string? ExecuteError, bool Changed = false);
    private static readonly string[] Keywords = ["Shugenja", "Kensai", "Ninja", "Samurai", "Bushi", "Monk", "Courtier", "Duelist", "Ancestor", "Paragon", "Tactician", "Cavalry", "Naval", "Siege", "Magistrate", "Spy", "Sensei", "Fire", "Air", "Earth", "Water", "Void", "Elite", "Fallen", "Maho", "Kiho"];
    private static readonly string[] Windowed = ["Battle", "Open", "Limited", "BattleOrOpen", "Reaction"];

    // Single-window cards (issue #833): exactly one Battle / Open / Limited action and no Reaction, probed the same way, plus whether the executed action changed the state at all.
    public static IEnumerable<CardDefinition> SingleInventory() => CardCatalog.Cards.Values
        .Where(c => !c.CreatedOnly && (c.Abilities ?? []).Where(a => Windowed.Contains(Timing(a))).Sum(a => Timing(a) == "BattleOrOpen" ? 2 : 1) == 1 && (c.Abilities ?? []).Any(a => Timing(a) is "Battle" or "Open" or "Limited"))
        .OrderBy(c => c.Id, StringComparer.Ordinal);

    public static IEnumerable<CardDefinition> Inventory() => CardCatalog.Cards.Values
        .Where(c => !c.CreatedOnly && (c.Abilities ?? []).Where(a => Windowed.Contains(Timing(a))).Sum(a => Timing(a) == "BattleOrOpen" ? 2 : 1) >= 2)
        .OrderBy(c => c.Id, StringComparer.Ordinal);
    private static string Timing(string ability) => AbilityCatalog.All.TryGetValue(ability, out var a) ? a.Timing : "MISSING";

    // The scenarios an ability of this timing is expected to be on offer in (everything else must NOT offer it enabled).
    public static string[] Expected(string timing) => timing switch
    {
        "Battle" => ["battle-attacker", "battle-defender"],
        "Open" => ["action-active", "action-other"],
        "Limited" => ["action-active"],
        "BattleOrOpen" => ["battle-attacker", "battle-defender", "action-active", "action-other"],
        _ => [],
    };
    public static readonly string[] Scenarios = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    internal static (MatchEngine Engine, CardInstance Source, int Player) Build(CardDefinition c, string scenario, int variant = 0)
    {
        var battle = scenario.StartsWith("battle");
        var me = scenario is "battle-defender" or "action-active" ? 0 : 1; // the player whose card is probed: he holds priority
        var s = Batch336Support.Table(battle ? Phase.Battle : Phase.Action, scenario == "battle-defender" ? 1 : scenario == "action-other" ? 0 : me);
        s.PriorityPlayerId = me;
        var kw = Keywords.Concat((c.Abilities ?? []).SelectMany(a => AbilityCatalog.All.TryGetValue(a, out var d) ? d.RequiredKeywords ?? [] : [])).Distinct().ToArray();
        CardInstance Mk(string def, int owner, Zone z, int? province = null, string? parent = null)
        {
            var card = Batch336Support.Add(s, def, owner, z, province: province, parent: parent);
            if (CardCatalog.Get(def).Type == CardType.Personality) foreach (var k in kw) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent));
            return card;
        }
        int? at = battle ? 0 : null;
        var zone = battle ? Zone.Battlefield : Zone.Home;
        var own = Mk("training-crane-samurai", me, zone, at); Mk("training-crane-samurai", me, zone, at);
        var enemy = Mk("training-lion-samurai", 1 - me, zone, at); Mk("training-lion-samurai", 1 - me, zone, at);
        for (var i = 0; i < 8; i++) { Mk("training-marketplace", 0, Zone.Home); Mk("training-marketplace", 1, Zone.Home); }
        // Targets and costs a generic fixture can offer: attachments on both sides, bowed Personalities, piles to pull from, a hand to discard from.
        foreach (var side in new[] { me, 1 - me })
        {
            var host = Mk("training-crane-samurai", side, zone, at);
            Mk("training-weapon", side, Zone.Attached, at, host.Id); Mk("training-follower", side, Zone.Attached, at, host.Id);
            host.Bowed = side != me;
            Mk("training-crane-samurai", side, zone, at).Bowed = true;
            Mk("training-weapon", side, Zone.FateDiscard); Mk("training-follower", side, Zone.FateDiscard); Mk("training-spell", side, Zone.FateDiscard); Mk("training-rally", side, Zone.FateDiscard);
            Mk("training-ancestor", side, Zone.DynastyDiscard); Mk("training-border-keep", side, Zone.DynastyDiscard);
            Mk("training-crane-samurai", side, Zone.Dead);
            Mk("training-rally", side, Zone.Hand); Mk("training-weapon", side, Zone.Hand); Mk("training-spell", side, Zone.Hand);
        }
        CardInstance source;
        switch (c.Type)
        {
            case CardType.Personality: source = Mk(c.Id, me, zone, at); break;
            case CardType.Holding: source = Mk(c.Id, me, Zone.Home); break;
            case CardType.Item: case CardType.Follower: source = Mk(c.Id, me, Zone.Attached, at, own.Id); break;
            case CardType.Region: source = Mk(c.Id, me, Zone.Region, 0); break;
            case CardType.Celestial: source = Mk(c.Id, me, Zone.Celestial); break;
            case CardType.Stronghold: source = s.Cards[s.Players[me].StrongholdId]; source.DefinitionId = c.Id; break;
            case CardType.Spell when variant == 1: source = Mk(c.Id, me, Zone.Attached, at, own.Id); break;
            case CardType.Ring when variant == 1: source = Mk(c.Id, me, Zone.Home); break;
            default: source = Mk(c.Id, me, Zone.Hand); break;
        }
        return (MatchEngine.FromTestState(s), source, me);
    }

    public static List<Probe> Run(CardDefinition c)
    {
        var results = new List<Probe>();
        foreach (var scenario in Scenarios)
        {
            MatchEngine e; CardInstance source;
            for (var variant = 0; variant < (c.Type is CardType.Spell or CardType.Ring ? 2 : 1); variant++)
            {
                int me; try { (e, source, me) = Build(c, scenario, variant); }
                catch (Exception ex) { foreach (var id in c.Abilities ?? []) results.Add(new(c.Id, c.Name, id, Timing(id), scenario, false, false, "fixture: " + ex.Message, false, null)); continue; }
                foreach (var id in c.Abilities ?? [])
                {
                    var t = Timing(id);
                    if (!Windowed.Contains(t) || t == "Reaction") continue;
                    var best = new Probe(c.Id, c.Name, id, t, scenario, false, false, null, false, null);
                    foreach (var player in new[] { me })
                    {
                        ActionOption[] options;
                        try { options = Batch336Support.Options(e, player, id, source.Id); } catch (Exception ex) { best = best with { Reason = "query: " + ex.Message }; continue; }
                        if (options.Length == 0) continue;
                        var on = options.FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == id); // split Battle/Limited twins share one label: only the offer's own command identifies the half
                        if (on is null) { best = best with { Offered = true, Reason = options[0].Reason }; continue; }
                        best = best with { Offered = true, Enabled = true, Reason = null };
                        try { var clone = e.Clone(); var before = System.Text.Json.JsonSerializer.Serialize(clone.State); var r = clone.Execute(on.Command!); best = best with { Executed = r.Accepted, ExecuteError = r.Accepted ? null : r.Error, Changed = r.Accepted && System.Text.Json.JsonSerializer.Serialize(clone.State) != before }; }
                        catch (Exception ex) { best = best with { ExecuteError = "throw: " + ex.Message }; }
                    }
                    var prior = results.FindIndex(r => r.Card == c.Id && r.Ability == id && r.Scenario == scenario);
                    if (prior < 0) results.Add(best); else if (best.Enabled || !results[prior].Offered && best.Offered) results[prior] = best; // another placement of the same card: keep the better probe
                }
            }
        }
        return results;
    }

    private static void Report()
    {
        var path = Environment.GetEnvironmentVariable("L5R_AUDIT_OUT");
        var all = new List<Probe>();
        foreach (var c in Environment.GetEnvironmentVariable("L5R_AUDIT_MODE") == "single" ? SingleInventory() : Inventory()) { if (path is not null) File.WriteAllText(path + ".progress", c.Id); all.AddRange(Run(c)); }
        if (path is not null) File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(all));
        // Hard invariant: an action is never enabled in a phase its printed timing does not allow.
        var wrong = all.Where(p => p.Enabled && !Expected(p.Timing).Contains(p.Scenario)).ToList();
        Batch336Support.Check(wrong.Count == 0, "enabled outside its window: " + string.Join("; ", wrong.Select(p => $"{p.Name} {p.Ability} [{p.Timing}] in {p.Scenario}")));
    }

    // The single-window audit takes ~40 s as one case; it runs as SingleSlices interleaved cases so the shards can balance it (issue #865).
    public const int SingleSlices = 3;

    private static void ReportSingle(int slice)
    {
        var path = Environment.GetEnvironmentVariable("L5R_AUDIT_OUT_SINGLE");
        var all = new List<Probe>();
        foreach (var c in SingleInventory().Where((_, i) => i % SingleSlices == slice)) all.AddRange(Run(c));
        if (path is not null) File.WriteAllText($"{path}.{slice}", System.Text.Json.JsonSerializer.Serialize(all));
        var wrong = all.Where(p => p.Enabled && !Expected(p.Timing).Contains(p.Scenario)).ToList();
        Batch336Support.Check(wrong.Count == 0, "enabled outside its window: " + string.Join("; ", wrong.Select(p => $"{p.Name} {p.Ability} [{p.Timing}] in {p.Scenario}")));
        var refused = all.Where(p => p.Enabled && !p.Executed).ToList();
        Batch336Support.Check(refused.Count == 0, "offered enabled but refused on execution: " + string.Join("; ", refused.Select(p => $"{p.Name} {p.Ability} in {p.Scenario}: {p.ExecuteError}")));
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit", [
        ("Every multi-window card's actions are on offer only in the phases their printed timing allows (issue #830)", Report),
        .. Enumerable.Range(0, SingleSlices).Select(slice => ($"Every single-window card's action is on offer only in the phases its printed timing allows (issue #833, slice {slice + 1} of {SingleSlices})", (Action)(() => ReportSingle(slice)))),
    ]);
}
