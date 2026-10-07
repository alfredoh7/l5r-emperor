using System.Text.Json;
using L5R.Core;
using static Batch336Support;

internal static class BattleValuesTests
{
    public static void Run(TestRunner runner) => runner.Run("battle-values", [
        ("Resolution totals, unit counts and province strength agree for either public seat", Resolution),
        ("Bowing preserves unit count and independently contributing Followers", BowedFollowers),
        ("Redirection and army-wide bonuses use resolution totals, never another court", Bonuses),
        ("Empty armies, open and destroyed provinces have accurate absent strength", EmptyAndOpen),
        ("Detached wire summary is public, backward compatible and hides hand identities", Wire),
        ("Conceding a live battle removes its current summary", Concession)
    ]);

    private static BattleValuesView Values(MatchEngine e, int seat = 0) => e.GetView(seat).Battlefields!.Single(b => b.Index == e.State.CurrentBattleProvince).Values!;

    private static void Resolution()
    {
        foreach (var scenario in new[] { "standard", "bowed", "crowded", "bonus" })
        {
            var e = MatchEngine.CreateBattleValuesShowcase(scenario); var totals = e.ArmyForcesForTest();
            var v = Values(e); Batch336Support.Eq(v.AttackerForce, totals.Attacker); Batch336Support.Eq(v.DefenderForce, totals.Defender);
            Batch336Support.Eq(v, Values(e, 1), "Viewing seat does not change authoritative roles or values");
            Batch336Support.Eq(v.ProvinceStrength, (int?)e.GetView(1).Players[1].Provinces[1].Strength);
            Batch336Support.Eq(v.AttackerUnits, e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality));
            Batch336Support.Eq(v.DefenderUnits, e.State.Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality));
        }
    }

    private static void BowedFollowers()
    {
        var s = Batch336Support.Table();
        var host = Batch336Support.Add(s, "training-crane-samurai", 1, Zone.Battlefield, true, 0);
        var follower = Batch336Support.Add(s, "training-follower", 1, Zone.Attached, false, 0, host.Id);
        var e = MatchEngine.FromTestState(s); var before = Values(e);
        Batch336Support.Eq(before.AttackerUnits, 1); Batch336Support.Check(before.AttackerForce > 0, "Ready Follower contributes despite bowed host");
        var later = e.State; later.Cards[follower.Id].Bowed = true; var after = Values(MatchEngine.FromTestState(later));
        Batch336Support.Eq(after.AttackerUnits, 1); Batch336Support.Eq(after.AttackerForce, 0);
        Batch336Support.Eq(Values(e), before, "Public values do not mutate old engine state");
    }

    private static void Bonuses()
    {
        var e = MatchEngine.CreateBattleValuesShowcase("standard"); var before = Values(e);
        var s = e.State; var host = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.AttachedTo is null);
        s.ForceRedirects.Add(new(host.Id, 1)); s.ArmyForceBonuses.Add(new(0, 5, s.BattleNumber));
        s.UnresolvedBattlefields = [1, 2]; Batch336Support.Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 2);
        e = MatchEngine.FromTestState(s); var totals = e.ArmyForcesForTest(); var v = Values(e);
        Batch336Support.Eq(v.AttackerForce, totals.Attacker); Batch336Support.Eq(v.DefenderForce, totals.Defender);
        Batch336Support.Check(v.DefenderForce > before.DefenderForce, "Redirected Personality Force reaches beneficiary");
        Batch336Support.Eq(v.AttackerUnits, before.AttackerUnits, "Redirection does not transfer ownership/count");
        Batch336Support.Check(e.GetView(0).Battlefields!.Single(b => b.Index == 2).Values is null, "Battle-scoped bonus cannot leak onto unselected court");
    }

    private static void EmptyAndOpen()
    {
        var empty = Values(MatchEngine.CreateBattleValuesShowcase("empty"));
        Batch336Support.Eq(empty.AttackerForce, 0); Batch336Support.Eq(empty.DefenderForce, 0); Batch336Support.Eq(empty.AttackerUnits, 0); Batch336Support.Eq(empty.DefenderUnits, 0);
        var e = MatchEngine.CreateBattleValuesShowcase("open"); Batch336Support.Eq(Values(e).ProvinceStrength, (int?)null);
        var s = MatchEngine.CreateBattleValuesShowcase("standard").State; s.Players[1].Provinces[1].Destroyed = true;
        Batch336Support.Eq(Values(MatchEngine.FromTestState(s)).ProvinceStrength, (int?)null);
        s.CurrentBattleProvince = null; s.Phase = Phase.BattleSelection;
        Batch336Support.Check(MatchEngine.FromTestState(s).GetView(0).Battlefields!.All(b => b.Values is null), "No totals predict an unengaged battle");
    }

    private static void Wire()
    {
        var e = MatchEngine.CreateBattleValuesShowcase("standard"); var first = e.GetView(0);
        var json = JsonSerializer.Serialize(first); var copy = JsonSerializer.Deserialize<MatchView>(json)!;
        Batch336Support.Eq(copy.Battlefields!.Single().Values, first.Battlefields!.Single().Values);
        var legacy = JsonSerializer.Deserialize<BattlefieldView>("{\"Index\":1,\"Province\":1}")!;
        Batch336Support.Check(legacy.Values is null, "Old snapshots have no invented totals");
        foreach (var seat in new[] { 0, 1 })
        {
            var view = e.GetView(seat);
            Batch336Support.Check(view.Players.Single(p => p.Id != seat).Hand.Length == 0, "Summary does not expose the other hand");
        }
    }

    private static void Concession()
    {
        var e = MatchEngine.CreateBattleValuesShowcase("standard");
        RegressionAssertions.Accept(e, new GameCommand("concede", 0, e.Version));
        Batch336Support.Eq(e.GetView(0).Phase, Phase.Finished);
        foreach (var seat in new[] { 0, 1 })
            Batch336Support.Check(e.GetView(seat).Battlefields!.All(b => b.Values is null), "Finished match retains no current-battle summary");
    }
}
