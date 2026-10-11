using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #816: Kalani's Landing (src/L5R.Core/Wave816.cs), proved on the published card itself. Player 0 is the Crane with Kalani's Landing as its Stronghold, player 1 the Lion; every position is explicit and a
// refused command must leave the state unchanged.
internal static class Wave816Tests
{
    private const string Kalani = Wave816Abilities.Kalani, Reduce = Wave816Abilities.KalaniReduce, CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    /// <summary>The Action Phase of player <paramref name="actor"/>: Kalani's Landing is player 0's Stronghold, each player has a Personality with an Open action (the training fixture), player 0 has a Personality
    /// costing 3 and one costing nothing at home, and player 1 one costing 4.</summary>
    private sealed record Fixture(MatchEngine Engine, CardInstance Kalani, CardInstance Mine, CardInstance MineB, CardInstance Foe, CardInstance Priced, CardInstance Free, CardInstance FoePriced);

    private static Fixture Table(int actor = 0, bool cheap = false)
    {
        var s = Batch336Support.Table(Phase.Action, actor);
        var kalani = Add(s, Kalani, 0, Zone.Stronghold);
        s.Players[0].StrongholdId = kalani.Id;
        CardInstance Samurai(string id, int owner, int cost, string baseId)
        {
            var card = Fx(s, baseId, id, owner, Zone.Home, d => d with { GoldCost = cheap ? 0 : cost });
            card.GrantedAbilities.Add(new("training-plain-open", "test-fixture", GrantDuration.Permanent));
            return card;
        }
        var mine = Samurai("w816-mine", 0, 0, CraneSamurai); var mineB = Samurai("w816-mine-b", 0, 0, CraneSamurai); var foe = Samurai("w816-foe", 1, 0, LionSamurai);
        var priced = Fx(s, CraneSamurai, "w816-priced", 0, Zone.Home, d => d with { GoldCost = cheap ? 0 : 3 });
        var free = Fx(s, CraneSamurai, "w816-free", 0, Zone.Home, d => d with { GoldCost = 0 });
        var foePriced = Fx(s, LionSamurai, "w816-foe-priced", 1, Zone.Home, d => d with { GoldCost = cheap ? 0 : 4 });
        if (cheap)
            foreach (var card in s.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold or Zone.Region or Zone.Celestial))
                card.Printed = (card.Printed ?? CardCatalog.Get(card.DefinitionId)) with { GoldCost = 0 };
        return new(MatchEngine.FromTestState(s), kalani, mine, mineB, foe, priced, free, foePriced);
    }

    private static GameCommand Open(MatchEngine e, int player, CardInstance card) => Batch336Support.Use(e, player, "training-plain-open", card.Id);
    private static ActionOption? KalaniOption(MatchEngine e, int player) => e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == Reduce && a.Enabled);
    private static bool WindowOpen(MatchEngine e) => e.State.PendingChoice is { Type: "announceReaction" };
    private static void Decline(MatchEngine e) => Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    /// <summary>Both players decline: the announced action goes on.</summary>
    private static void CloseWindow(MatchEngine e) { for (var guard = 0; guard < 4 && WindowOpen(e); guard++) Decline(e); Check(!WindowOpen(e), "the window closed"); }

    public static void Run(TestRunner runner) => runner.Run("wave-816", [
        ("Published as printed: Mantis Stronghold, Strength 7, production 4, Honor 2, Port, with its Reaction and Effect", PublishedShape),
        ("Gold Cost modifier: a card in play with a non-zero Gold Cost is targeted after an announced action and has -2 Gold Cost; never a card at cost 0, in a hand or in the deck", ReducesGoldCost),
        ("The reduction lasts until the end of the turn and stacks no lower than 0", LastsUntilTurnEnd),
        ("The window opens after either player's action, once Kalani's Landing is used it is not opened again until the next turn, and declining it spends nothing", WindowRules),
        ("The window does not open when there is no legal target, and a Reaction is not announced into it", NoWindow),
        ("A Strategy played and an Equip are announced into the window too, and the action resolves after it closes", StrategyAnnounced),
        ("It produces 4 Gold, but none when paying for an attachment", AttachmentProduction),
        ("After each phase begins it straightens, in either player's turn, and does nothing when it is not bowed", PhaseStraighten),
    ]);

    private static void PublishedShape()
    {
        var def = CardCatalog.Get(Kalani);
        Eq(def.Name, "Kalani's Landing"); Eq(def.Type, CardType.Stronghold); Eq(def.Clan, "Mantis");
        Eq(def.GoldProduction, 4, "production"); Eq(def.ProvinceStrength, 7, "Strength"); Eq(def.StartingHonor, 2, "starting Honor");
        Check(def.Keywords?.Contains("Port") == true, "the Port keyword");
        Eq(string.Join(",", def.Abilities ?? []), Reduce, "its Reaction"); Eq(def.Effect, Wave816Abilities.KalaniEffect, "its Effect");
        Check(def.Text.Contains("Give it -2 Gold Cost") && def.Text.Contains("After each phase begins: Straighten this card"), "the printed text is kept");
        var reaction = AbilityCatalog.All[Reduce];
        Eq(reaction.Timing, "Reaction"); Eq(reaction.Trigger, Wave816Abilities.AnnounceAnyAction); Eq(reaction.UsesPerTurn, 1, "once per turn");
    }

    private static void ReducesGoldCost()
    {
        var f = Table();
        var e = f.Engine;
        Eq(e.CardGoldCostForTest(f.Priced.Id), 3, "fixture: costs 3");
        Accept(e, Open(e, 0, f.Mine));
        Check(WindowOpen(e) && e.State.PendingChoice!.CardId == f.Kalani.Id && e.State.PendingChoice.PlayerId == 0, "the announce window opens for Kalani's Landing's controller");
        var option = KalaniOption(e, 0) ?? throw new Exception("the Reaction is offered");
        var targets = option.TargetIds ?? [];
        Check(targets.Contains(f.Priced.Id) && targets.Contains(f.FoePriced.Id), "a card in play with a cost, either player's, may be targeted");
        Check(!targets.Contains(f.Free.Id) && !targets.Contains(f.Mine.Id), "a card with no Gold Cost may not");
        // Illegal targets are refused with nothing changed.
        NoChange(e, option.Command! with { TargetId = f.Free.Id }, "a card with a Gold Cost of 0");
        NoChange(e, option.Command! with { TargetId = f.Kalani.Id }, "the Stronghold, which has no Gold Cost");
        Accept(e, option.Command! with { TargetId = f.Priced.Id });
        Eq(e.CardGoldCostForTest(f.Priced.Id), 1, "3 - 2 = 1");
        Check(e.State.Cards[f.Priced.Id].Modifiers.Any(m => m.Stat == "GoldCost" && m.Amount == -2 && !m.Permanent), "an ordinary turn-long modifier");
        Eq(e.CardGoldCostForTest(f.FoePriced.Id), 4, "nothing else changed");
        Check(e.State.Cards[f.Kalani.Id].AbilityUses.ContainsKey(Reduce), "the use is spent");
    }

    private static void LastsUntilTurnEnd()
    {
        var f = Table();
        var e = f.Engine;
        Accept(e, Open(e, 0, f.Mine));
        Accept(e, KalaniOption(e, 0)!.Command! with { TargetId = f.Priced.Id });
        Eq(e.CardGoldCostForTest(f.Priced.Id), 1, "reduced now");
        // A cost of 1 reduced by 2 is 0, never below.
        var low = Batch336Support.Edit(e, s => { s.Cards[f.Priced.Id].Printed = s.Cards[f.Priced.Id].Printed! with { GoldCost = 1 }; s.Cards[f.Priced.Id].Modifiers.Clear(); s.Cards[f.Kalani.Id].AbilityUses.Clear(); s.PendingChoice = null; s.PendingAnnouncedAction = null; });
        var modifier = new AppliedModifier(f.Kalani.Id, Reduce, "GoldCost", -2);
        var state = low.State; state.Cards[f.Priced.Id].Modifiers.Add(modifier);
        Eq(MatchEngine.FromTestState(state).CardGoldCostForTest(f.Priced.Id), 0, "never below 0");
        // The turn ends: the modifier goes with it.
        Call(e, "FinishTurn");
        Check(!e.State.Cards[f.Priced.Id].Modifiers.Any(m => m.Stat == "GoldCost"), "gone when the turn ends");
        Eq(e.CardGoldCostForTest(f.Priced.Id), 3, "the printed cost is back");
    }

    private static void WindowRules()
    {
        // Its own player's action, used: a second action the same turn opens no window.
        var f = Table();
        var e = f.Engine;
        Accept(e, Open(e, 0, f.Mine));
        Accept(e, KalaniOption(e, 0)!.Command! with { TargetId = f.Priced.Id });
        CloseWindow(e);
        var priority = e.State.PriorityPlayerId;
        Accept(e, Open(e, priority, priority == 0 ? f.MineB : f.Foe));
        Check(!WindowOpen(e), "used once: no window opens again this turn, for either player's action");
        // The next turn it opens again.
        var next = Batch336Support.Edit(e, s => { s.TurnNumber++; s.PendingChoice = null; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Cards[f.MineB.Id].AbilityUses.Clear(); });
        Accept(next, Open(next, 0, next.State.Cards[f.MineB.Id]));
        Check(WindowOpen(next), "the next turn it opens again");

        // Declining spends nothing: the next action opens it again.
        var gf = Table(); var g = gf.Engine;
        Accept(g, Open(g, 0, gf.Mine));
        Check(WindowOpen(g), "opened");
        CloseWindow(g);
        Check(!g.State.Cards[gf.Kalani.Id].AbilityUses.ContainsKey(Reduce), "declining spends nothing");
        var second = g.State.PriorityPlayerId;
        Accept(g, Open(g, second, second == 0 ? g.State.Cards[gf.MineB.Id] : g.State.Cards[gf.Foe.Id]));
        Check(WindowOpen(g), $"so the next action opens it again (priority was {second}, pending {g.State.PendingChoice?.Type}, phase {g.State.Phase}, uses {string.Join(",", g.State.Cards[gf.Kalani.Id].AbilityUses.Keys)})");

        // The opponent's action: the window opens for Kalani's Landing's controller (player 0) all the same.
        var hf = Table(actor: 1); var h = hf.Engine;
        Accept(h, Open(h, 1, hf.Foe));
        Check(WindowOpen(h) && h.State.PendingChoice!.PlayerId == 0 && h.State.PendingChoice.CardId == hf.Kalani.Id, "after the opponent's action the window is opened for its controller");
        Check(KalaniOption(h, 1) is null, "the opponent holds no such Reaction");
        Accept(h, KalaniOption(h, 0)!.Command! with { TargetId = hf.FoePriced.Id });
        Eq(h.CardGoldCostForTest(hf.FoePriced.Id), 2, "the opponent's own card is reduced too");
        CloseWindow(h);
        Check(h.State.Cards[hf.Foe.Id].AbilityUses.ContainsKey("training-plain-open"), "the announced action went on to resolve once the window closed");
    }

    private static void NoWindow()
    {
        var f = Table(cheap: true); // every card in play costs nothing
        var e = f.Engine;
        Accept(e, Open(e, 0, f.Mine));
        Check(!WindowOpen(e), "no legal target: the window never opens");
        Check(!e.State.Cards[f.Kalani.Id].AbilityUses.ContainsKey(Reduce), "and nothing is spent");
    }

    private static void StrategyAnnounced()
    {
        // Ancient Feud (oracle-l5r-397): a Terrain Strategy played from the hand at the battle.
        var s = Batch336Support.Table(Phase.Battle, 0);
        var kalani = Add(s, Kalani, 0, Zone.Stronghold); s.Players[0].StrongholdId = kalani.Id;
        Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var priced = Fx(s, CraneSamurai, "w816-priced", 0, Zone.Home, d => d with { GoldCost = 3 });
        var terrain = Add(s, "oracle-l5r-397", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: terrain.Id));
        Check(WindowOpen(e) && e.State.PendingChoice!.CardId == kalani.Id, "a Strategy played is announced into the window");
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Hand, "and waits behind it");
        Accept(e, KalaniOption(e, 0)!.Command! with { TargetId = priced.Id });
        CloseWindow(e);
        Eq(e.CardGoldCostForTest(priced.Id), 1, "the reduction applied");
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Strategy resolved when the window closed");

        // An Equip: paid by Holdings (Kalani's Landing itself produces nothing for an attachment), announced before it resolves.
        var t = GoldParityTests.Action();
        for (var i = 0; i < 4; i++) GoldParityTests.Add(t, "training-marketplace", 0, Zone.Home);
        var stronghold = Add(t, Kalani, 0, Zone.Stronghold); t.Players[0].StrongholdId = stronghold.Id;
        var follower = GoldParityTests.Add(t, "training-invest-follower", 0, Zone.Hand);
        var host = GoldParityTests.Add(t, "training-crane-samurai", 0, Zone.Home);
        var costly = Fx(t, CraneSamurai, "w816-costly", 0, Zone.Home, d => d with { GoldCost = 3 });
        var f = MatchEngine.FromTestState(t);
        var payment = f.PaymentOptions(0, 2, new PaymentSubject(f.State.Cards[follower.Id], PaymentKind.Equip, Wielder: f.State.Cards[host.Id])).First();
        Check(!payment.SourceIds.Contains(stronghold.Id), "Kalani's Landing is not offered as a source for the attachment");
        Accept(f, new GameCommand("equip", 0, f.Version, CardId: follower.Id, SourceIds: payment.SourceIds, TargetId: host.Id));
        Check(WindowOpen(f) && f.State.PendingChoice!.CardId == stronghold.Id, "an Equip is announced into the window");
        Check(f.State.Cards[follower.Id].AttachedTo is null, "and waits behind it");
        Accept(f, KalaniOption(f, 0)!.Command! with { TargetId = costly.Id });
        CloseWindow(f);
        Eq(f.State.Cards[follower.Id].AttachedTo, host.Id, "the Equip resolved when the window closed");
        Eq(f.CardGoldCostForTest(costly.Id), 1, "and the reduction applied");
    }

    private static void AttachmentProduction()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var kalani = Add(s, Kalani, 0, Zone.Stronghold); s.Players[0].StrongholdId = kalani.Id;
        var wielder = Fx(s, CraneSamurai, "w816-wielder", 0, Zone.Home);
        var item = Fx(s, "training-item", "w816-item", 0, Zone.Hand, d => d with { Type = CardType.Item, GoldCost = 3 });
        var follower = Fx(s, "training-item", "w816-follower", 0, Zone.Hand, d => d with { Type = CardType.Follower, GoldCost = 3 });
        var personality = Fx(s, CraneSamurai, "w816-recruit", 0, Zone.Hand, d => d with { GoldCost = 3 });
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(kalani.Id, 0), 4, "it produces its printed 4 Gold");
        Eq(e.GoldProduced(kalani.Id, 0, new PaymentSubject(e.State.Cards[personality.Id], PaymentKind.Recruit)), 4, "4 Gold for a Personality");
        Eq(e.GoldProduced(kalani.Id, 0, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip, Wielder: e.State.Cards[wielder.Id])), 0, "no Gold for an Item");
        Eq(e.GoldProduced(kalani.Id, 0, new PaymentSubject(e.State.Cards[follower.Id], PaymentKind.Equip, Wielder: e.State.Cards[wielder.Id])), 0, "no Gold for a Follower");
        Eq(e.GoldProduced(kalani.Id, 0, new PaymentSubject(e.State.Cards[kalani.Id], PaymentKind.Action)), 4, "4 Gold for an action");
    }

    private static void PhaseStraighten()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kalani = Add(s, Kalani, 0, Zone.Stronghold, bowed: true); s.Players[0].StrongholdId = kalani.Id;
        var other = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Call(e, "SetPhase", Phase.Dynasty, 0);
        Check(!e.State.Cards[kalani.Id].Bowed, "after a phase begins it is straightened");
        Check(e.State.Cards[other.Id].Bowed, "and nothing else is");
        // The opponent's turn is a phase too.
        var t = Batch336Support.Table(Phase.Action, 1);
        var bowed = Add(t, Kalani, 0, Zone.Stronghold, bowed: true); t.Players[0].StrongholdId = bowed.Id;
        var f = MatchEngine.FromTestState(t);
        Call(f, "SetPhase", Phase.Dynasty, 1);
        Check(!f.State.Cards[bowed.Id].Bowed, "it straightens as the opponent's phases begin");
        // A segment of the same phase is not a new phase: the Attack Phase's declaration through battle is one.
        var u = Batch336Support.Table(Phase.AttackDeclaration, 0);
        var again = Add(u, Kalani, 0, Zone.Stronghold, bowed: true); u.Players[0].StrongholdId = again.Id;
        var g = MatchEngine.FromTestState(u);
        Call(g, "SetPhase", Phase.InfantryDeclaration, 0);
        Check(g.State.Cards[again.Id].Bowed, "a segment of the same phase is not a phase beginning");
    }
}
