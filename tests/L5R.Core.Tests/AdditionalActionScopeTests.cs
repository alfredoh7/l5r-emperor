using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Primitive P17 (issue #721, docs/decisions/2026-10-07-p17-additional-action-scope.md): an additional-action grant scoped to named abilities, and an Action-Phase Equip that spends or forfeits grants.
// Player 0 attacks and holds the grant; the abilities are the engine's training fixtures (a Battle bow, and a Battle action that creates a grant).
internal static class AdditionalActionScopeTests
{
    private const string Creator = "training-grant-bower", Follower = "training-battle-bower", GrantBow = "training-grant-bow", BowAtBattle = "training-bow-at-battle", Hotako = "oracle-l5r-1726";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Scope: a grant naming an ability is spent by that ability and by nothing else", GrantIsSpentOnlyByItsAbility),
            ("Scope: an action outside the scope is rejected while only scoped grants are held, and the state does not change", OutOfScopeActionIsRejectedWithoutChange),
            ("Scope: an unscoped grant is not narrowed, and a scoped grant is preferred over an unscoped one for the ability it names", UnscopedGrantIsUnchanged),
            ("Scope: a Battle-bound grant is not spent by an action outside its battle", BattleBoundGrantIsNotSpentOutsideItsBattle),
            ("Scope: an Equip in a battle is rejected while only scoped grants are held", BattleEquipIsRejected),
            ("Scope: an Action-Phase Equip is rejected while only scoped grants are held, spends an unscoped grant, and forfeits the rest otherwise", ActionPhaseEquip),
            ("Scope: a mid-burst snapshot of a scoped grant round-trips, and a match without one saves exactly as before", SnapshotRoundTrips),
        ];
        runner.Run("additional-action-scope", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, int? province = null) => Batch336Support.Add(s, def, owner, zone, false, province, null);

    private sealed record Rig(MatchState S, CardInstance Creator, CardInstance Follow, CardInstance First, CardInstance Second);

    // Player 0 attacks with a grant-creating card and a second bower; player 1 defends with two Personalities. Player 0 to act, in a battle.
    private static Rig Table()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var creator = Add(s, Creator, 0, Zone.Battlefield, 0);
        var follow = Add(s, Follower, 0, Zone.Battlefield, 0);
        var first = Add(s, "training-crane-samurai", 1, Zone.Battlefield, 0);
        var second = Add(s, "training-lion-samurai", 1, Zone.Battlefield, 0);
        return new(s, creator, follow, first, second);
    }

    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static int Grants(MatchEngine e) => e.State.AdditionalActions.Count(g => g.PlayerId == 0);
    private static void Use(MatchEngine e, int player, string ability, CardInstance card, string? target = null) =>
        Accept(e, new("useAbility", player, e.Version, CardId: card.Id, AbilityId: ability, TargetId: target));
    private static AdditionalActionGrant Scoped(params string[] abilities) => new(0, null, ScopeAbilityIds: abilities);

    private static void GrantIsSpentOnlyByItsAbility()
    {
        var r = Table();
        r.S.AdditionalActions.Add(Scoped(BowAtBattle));
        var e = MatchEngine.FromTestState(r.S);
        Check(e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == BowAtBattle && o.CardId == r.Follow.Id), "the named ability is offered");
        Use(e, 0, BowAtBattle, r.Follow, r.First.Id);
        Check(e.State.Cards[r.First.Id].Bowed, "and resolves");
        Eq(Grants(e), 0, "it spent the grant");
        Eq(e.State.PriorityPlayerId, 1, "with the last grant spent, priority passes on");
    }

    private static void OutOfScopeActionIsRejectedWithoutChange()
    {
        var r = Table();
        r.S.AdditionalActions.Add(Scoped(BowAtBattle));
        var e = MatchEngine.FromTestState(r.S);
        var before = JsonSerializer.Serialize(e.State);
        var message = Rejection(e, new("useAbility", 0, e.Version, CardId: r.Creator.Id, AbilityId: GrantBow, TargetId: r.First.Id));
        Check(message.Contains("additional action must use"), "refused with the scope's reason: " + message);
        Eq(JsonSerializer.Serialize(e.State), before, "a rejected action changes nothing");
        Eq(Grants(e), 1, "the grant is still held");
        Check(e.QueryActions(0).Any(o => o.Type == "pass"), "and passing is still offered");
    }

    private static void UnscopedGrantIsUnchanged()
    {
        var r = Table();
        r.S.AdditionalActions.Add(new(0, null));
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, GrantBow, r.Creator, r.First.Id);
        Eq(Grants(e), 1, "an unscoped grant admits any Battle action (the action took the held grant and created a new one)");

        // Both held: the ability the scoped grant names spends it, not the unscoped one.
        var both = Table();
        both.S.AdditionalActions.Add(new(0, null)); both.S.AdditionalActions.Add(Scoped(BowAtBattle));
        var e2 = MatchEngine.FromTestState(both.S);
        Use(e2, 0, BowAtBattle, both.Follow, both.First.Id);
        Eq(Grants(e2), 1, "one grant is left");
        Check(e2.State.AdditionalActions.Single().ScopeAbilityIds is null, "the scoped one was spent in preference to the unscoped one");
    }

    private static void BattleBoundGrantIsNotSpentOutsideItsBattle()
    {
        var r = Table();
        r.S.AdditionalActions.Add(new(0, null, Battle: r.S.BattleNumber + 5, ScopeAbilityIds: [BowAtBattle]));
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, BowAtBattle, r.Follow, r.First.Id);
        Eq(Grants(e), 1, "a grant of another battle is not spent by this one's action");
    }

    private static void BattleEquipIsRejected()
    {
        var r = Table();
        var item = Add(r.S, "training-champion-item", 0, Zone.Hand);
        r.S.AdditionalActions.Add(Scoped(BowAtBattle));
        var e = MatchEngine.FromTestState(r.S);
        var before = JsonSerializer.Serialize(e.State);
        Rejection(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [], TargetId: r.Follow.Id));
        Eq(JsonSerializer.Serialize(e.State), before, "nothing changed");
    }

    private static MatchState ActionTable(Action<MatchState, CardInstance, CardInstance>? arrange = null)
    {
        var s = GoldParityTests.Action();
        for (var i = 0; i < 4; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
        var host = GoldParityTests.Add(s, Hotako, 0, Zone.Home);
        var item = GoldParityTests.Add(s, "training-champion-item", 0, Zone.Hand);
        arrange?.Invoke(s, host, item);
        return s;
    }

    private static GameCommand EquipCommand(MatchEngine e, CardInstance item, CardInstance host) =>
        new("equip", 0, e.Version, CardId: item.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds, TargetId: host.Id);

    private static void ActionPhaseEquip()
    {
        // Only a grant scoped to abilities: the Equip is not one of them.
        var scoped = ActionTable((s, _, _) => s.AdditionalActions.Add(Scoped(BowAtBattle)));
        var e = MatchEngine.FromTestState(scoped);
        var host = scoped.Cards.Values.First(c => c.DefinitionId == Hotako); var item = scoped.Cards.Values.First(c => c.DefinitionId == "training-champion-item");
        var before = JsonSerializer.Serialize(e.State);
        Check(Rejection(e, EquipCommand(e, item, host)).Contains("additional action must use"), "refused with the scope's reason");
        Eq(JsonSerializer.Serialize(e.State), before, "nothing changed");

        // An unscoped grant is spent by the Equip, and its holder keeps priority while another is held.
        var open = ActionTable((s, _, _) => { s.AdditionalActions.Add(new(0, null)); s.AdditionalActions.Add(new(0, null)); });
        var e2 = MatchEngine.FromTestState(open);
        var host2 = open.Cards.Values.First(c => c.DefinitionId == Hotako); var item2 = open.Cards.Values.First(c => c.DefinitionId == "training-champion-item");
        Accept(e2, EquipCommand(e2, item2, host2));
        Eq(e2.State.Cards[item2.Id].AttachedTo, host2.Id, "attached");
        Eq(Grants(e2), 1, "the Equip spent one grant");
        Eq(e2.State.PriorityPlayerId, 0, "the holder keeps priority while another grant is held");

        // A grant scoped to the equipped card admits it.
        var cardScoped = ActionTable((s, _, i) => s.AdditionalActions.Add(new(0, [i.Id])));
        var e3 = MatchEngine.FromTestState(cardScoped);
        Accept(e3, EquipCommand(e3, cardScoped.Cards.Values.First(c => c.DefinitionId == "training-champion-item"), cardScoped.Cards.Values.First(c => c.DefinitionId == Hotako)));
        Eq(Grants(e3), 0, "spent by the card it names");

        // A grant scoped to some other card is not spent: the Equip is an ordinary action and forfeits the burst.
        var other = ActionTable((s, h, _) => s.AdditionalActions.Add(new(0, [h.Id])));
        var e4 = MatchEngine.FromTestState(other);
        Accept(e4, EquipCommand(e4, other.Cards.Values.First(c => c.DefinitionId == "training-champion-item"), other.Cards.Values.First(c => c.DefinitionId == Hotako)));
        Eq(Grants(e4), 0, "an ordinary action forfeits what it did not spend");
        Eq(e4.State.PriorityPlayerId, 1, "and priority passes on");

        // With no grant held nothing is different.
        var none = ActionTable();
        var e5 = MatchEngine.FromTestState(none);
        Accept(e5, EquipCommand(e5, none.Cards.Values.First(c => c.DefinitionId == "training-champion-item"), none.Cards.Values.First(c => c.DefinitionId == Hotako)));
        Eq(e5.State.PriorityPlayerId, 1, "an ordinary Equip passes priority");
    }

    private static void SnapshotRoundTrips()
    {
        var r = Table();
        r.S.AdditionalActions.Add(Scoped(BowAtBattle));
        var json = JsonSerializer.Serialize(r.S);
        Check(json.Contains("ScopeAbilityIds"), "the scope is saved");
        var restored = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(json)!);
        Eq(JsonSerializer.Serialize(JsonSerializer.Deserialize<MatchState>(json)!), json, "round-trips");
        Eq(restored.State.AdditionalActions.Single().ScopeAbilityIds?.Single(), BowAtBattle, "and the restored engine holds the scope");
        Use(restored, 0, BowAtBattle, restored.State.Cards[r.Follow.Id], r.First.Id);
        Eq(Grants(restored), 0, "and the restored grant is spent by the ability it names");

        var plain = Table();
        plain.S.AdditionalActions.Add(new(0, null));
        Check(!JsonSerializer.Serialize(plain.S).Contains("ScopeAbilityIds"), "an unscoped grant saves exactly as before: the scope is omitted when unset");
    }
}
