using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Additional Actions and Reactions (rules lead, 2026-09-19; docs/decisions/2026-09-19-battle-rulings-round-2.md, ruling 2): every action of an
// additional-action burst is atomic and independent, so a Reaction played meanwhile only interrupts the burst -- it continues, and the next
// additional action is offered -- unless the Reaction negates the action that CREATED the additional actions, when they are never created. Each
// position is played through the commands the engine offers. Player 0 attacks and holds the burst; player 1 defends.
internal static class AdditionalActionBurstTests
{
    private const string Grant = "training-grant-bow", Ruya = "oracle-l5r-7789", Hizumi = "oracle-l5r-8521", EffortlessCounterattack = "oracle-l5r-2283";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Burst: the creating action grants one additional action and the actor keeps priority (the baseline)", CreatingActionKeepsPriority),
            ("Burst: the burst holder's OWN non-negating Reaction, played in an after-action window, does not end it -- the grant survives, the window closes to the holder, and the next additional action is offered and taken", HoldersReactionDoesNotEndTheBurst),
            ("Burst: the OPPONENT's non-negating Reaction, played in an after-action window, does not end it -- and control returns to the holder, not to the opponent", OpponentsReactionDoesNotEndTheBurst),
            ("Burst: a Reaction that opens a window of its own over the one it was played in (SuspendedWindows) leaves the burst intact through every window and hands back to the holder at the end", NestedWindowsKeepTheBurst),
            ("Burst: a Reaction that negates the CREATING action ends it -- no additional action is ever created, and priority passes as after any action; declined, the same action creates it", NegatedCreatingActionEndsTheBurst),
            ("Burst: a negated ADDITIONAL action loses only itself (and what it would have created) -- the rest of the burst goes on and is the holder's to take", NegatedAdditionalActionLeavesTheRest),
            ("Burst: an ordinary action that is not one of the burst's additional actions still ends it, and so does passing (the rules that were not changed)", OrdinaryActionAndPassStillEndTheBurst),
            ("Burst: a negated Reaction (a Reaction is an action) does not forfeit the holder's additional actions", NegatedReactionDoesNotForfeit),
            ("Burst: a snapshot taken mid-burst, behind a nested window, round-trips and continues identically", SnapshotMidBurst),
        ];
        runner.Run("additional-action-bursts", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static int _added;
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"ab-{s.Cards.Count}-{++_added}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private sealed record Rig(MatchState S, CardInstance Creator, CardInstance Follow, CardInstance Ruya, CardInstance First, CardInstance Second, CardInstance Third);

    // Player 0 attacks with the creating action's card (training-grant-bow), a second bower for the additional action, and Ruya (the after-action
    // window for the creator's own bow); player 1 defends with three Personalities. Player 0 to act.
    private static Rig Table(Action<Rig>? arrange = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var creator = Add(s, "training-grant-bower", 0, Zone.Battlefield, province: 0);
        var follow = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var ruya = Add(s, Ruya, 0, Zone.Battlefield, province: 0);
        var first = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var second = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var third = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var rig = new Rig(s, creator, follow, ruya, first, second, third);
        arrange?.Invoke(rig);
        return rig;
    }

    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static void Decline(MatchEngine e) => Accept(e, Cmd(e, "declineReaction"));
    private static void Use(MatchEngine e, int player, string ability, CardInstance card, string? target = null, string? performer = null) =>
        Accept(e, new("useAbility", player, e.Version, CardId: card.Id, AbilityId: ability, TargetId: target, PerformerId: performer));
    // The command the engine offers for this ability from this card (never a hand-built one) -- so the offer itself is what is asserted.
    private static ActionOption? Offered(MatchEngine e, int player, string ability, CardInstance card) =>
        e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == ability && o.CardId == card.Id);
    private static int Grants(MatchEngine e, int player = 0) => e.State.AdditionalActions.Count(g => g.PlayerId == player);
    private static void ClearWindow(MatchEngine e) { while (e.State.PendingChoice is not null && e.State.PendingChoice.Type != "additionalActionReaction") Decline(e); }

    // ---- the baseline ---------------------------------------------------------------------------------------------
    private static void CreatingActionKeepsPriority()
    {
        var r = Table(t => t.S.Cards.Remove(t.Ruya.Id));
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Check(e.State.Cards[r.First.Id].Bowed, "the creating action resolved");
        Eq(Grants(e), 1, "and created its additional action");
        Eq(e.State.PriorityPlayerId, 0, "the holder keeps priority");
        Check(Offered(e, 0, "training-bow-at-battle", r.Follow) is not null, "the additional action is offered");
        Check(e.QueryActions(0).Any(o => o.Type == "pass"), "and passing still is");
    }

    // ---- a non-negating Reaction of the holder's own ----------------------------------------------------------------------
    private static void HoldersReactionDoesNotEndTheBurst()
    {
        var r = Table();
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Eq(e.State.PendingChoice?.Type, "bowReaction", "Ruya's window: her controller's own action bowed a Personality");
        Eq(Grants(e), 1, "the burst exists");
        Use(e, 0, "ruya-bow", r.Ruya, r.Second.Id);
        Check(e.State.Cards[r.Second.Id].Bowed, "her Reaction resolved");
        Eq(Grants(e), 1, "an ordinary action that was not an additional action would have forfeited it; a Reaction does not");
        // Issue #644: the opponent holds nothing to react with, so nobody is asked to pass and the window closes at once.
        Check(e.State.PendingChoice is null, "the window closed");
        Eq(e.State.PriorityPlayerId, 0, "and the holder has priority again, mid-burst");
        Eq(Grants(e), 1);
        var next = Offered(e, 0, "training-bow-at-battle", r.Follow);
        Check(next is not null, "the next additional action is offered");
        Accept(e, next!.Command! with { TargetId = r.Third.Id });
        Check(e.State.Cards[r.Third.Id].Bowed, "and taken");
        Eq(Grants(e), 0, "the additional action was spent");
        Eq(e.State.PriorityPlayerId, 1, "with the burst over, priority passes on");
    }

    // ---- a non-negating Reaction of the opponent's ----------------------------------------------------------------------
    private static void OpponentsReactionDoesNotEndTheBurst()
    {
        var r = Table(t => { t.S.Cards.Remove(t.Ruya.Id); Add(t.S, Hizumi, 1, Zone.Battlefield, province: 0); });
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Eq(e.State.PendingChoice?.Type, "bowReaction", "Hizumi watches this battlefield: his window for the bow");
        Eq(e.State.PriorityPlayerId, 1);
        var hizumi = r.S.Cards.Values.First(c => c.DefinitionId == Hizumi);
        Use(e, 1, "hizumi-negate-straighten", hizumi, r.First.Id);
        Eq(Grants(e), 1, "the opponent's Reaction leaves the burst alone");
        Check(e.State.PendingChoice is null, "the window closed (issue #644: nobody holds another reaction, so nobody is asked to pass)");
        Eq(e.State.PriorityPlayerId, 0, "control returns to the burst's holder, not to the opponent whose window it was");
        Check(Offered(e, 0, "training-bow-at-battle", r.Follow) is not null, "the next additional action is offered");
    }

    // ---- SuspendedWindows -----------------------------------------------------------------------------------------------
    private static void NestedWindowsKeepTheBurst()
    {
        var r = Table(t => Add(t.S, Hizumi, 1, Zone.Battlefield, province: 0));
        var hizumi = r.S.Cards.Values.First(c => c.DefinitionId == Hizumi);
        hizumi.AbilityUses["hizumi-negate-straighten"] = r.S.TurnNumber; // spent: only Ruya's window opens for the creator's own bow
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Eq(e.State.PendingChoice?.Type, "bowReaction"); Eq(e.State.PendingChoice?.CardId, r.Ruya.Id);
        var ruyasWindow = e.State.PendingChoice;
        Eq(Grants(e), 1);
        var state = e.State; state.Cards[hizumi.Id].AbilityUses.Clear(); // available again by the time Ruya's Reaction bows
        e = MatchEngine.FromTestState(state);
        Use(e, 0, "ruya-bow", r.Ruya, r.Second.Id);
        Eq(e.State.PendingChoice?.CardId, hizumi.Id, "Ruya's Reaction bowed a Personality: Hizumi's window opens over hers");
        Eq(e.State.SuspendedWindows.Count, 1); Eq(e.State.SuspendedWindows[0].Choice, ruyasWindow);
        Eq(Grants(e), 1, "the burst holds while a window waits behind another");
        Decline(e); // Hizumi's controller passes: the attacker holds nothing to react with, so his window closes
        // Issue #644: Ruya's window is given back (SuspendedWindows is empty again) but her Reaction is spent, so it has nothing left to offer and closes too.
        Eq(e.State.SuspendedWindows.Count, 0);
        Eq(Grants(e), 1);
        Check(e.State.PendingChoice is null, "every window closed");
        Eq(e.State.PriorityPlayerId, 0, "the holder has priority again");
        Eq(Grants(e), 1, "with the additional action intact");
        Check(Offered(e, 0, "training-bow-at-battle", r.Follow) is not null);
    }

    // ---- the creating action negated -----------------------------------------------------------------------------------------
    private static void NegatedCreatingActionEndsTheBurst()
    {
        foreach (var negate in new[] { true, false })
        {
            var r = Table(t => { t.S.Cards.Remove(t.Ruya.Id); Add(t.S, "training-merchant-free-guard", 1, Zone.Battlefield, province: 0); });
            var guard = r.S.Cards.Values.First(c => c.DefinitionId == "training-merchant-free-guard");
            var e = MatchEngine.FromTestState(r.S);
            Use(e, 0, Grant, r.Creator, guard.Id);
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the Merchant's controller may react to the action that targets him");
            Eq(Grants(e), 0, "nothing exists yet: the action has not resolved");
            if (negate)
            {
                var offer = e.QueryActions(1).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "training-negate-merchant-free");
                Accept(e, offer.Command!);
            }
            while (e.State.PendingChoice?.Type == "beforeActionReaction") Decline(e);
            Check(e.State.PendingChoice is null, "the window closed");
            if (negate)
            {
                Check(!e.State.Cards[guard.Id].Bowed, "negated: the action's effects did not happen");
                Eq(Grants(e), 0, "and the additional action it would have created never was");
                Eq(e.State.PriorityPlayerId, 1, "so nobody holds a burst: priority passes as after any action");
            }
            else
            {
                Check(e.State.Cards[guard.Id].Bowed, "declined: the action resolved");
                Eq(Grants(e), 1, "and created its additional action");
                Eq(e.State.PriorityPlayerId, 0, "which its actor keeps priority for");
            }
        }
    }

    // ---- an additional action negated ---------------------------------------------------------------------------------------
    private static void NegatedAdditionalActionLeavesTheRest()
    {
        // Player 0 is the Defender here (player 1 is Active), so only the burst itself can hand priority back to him.
        var r = Table(t => { t.S.Cards.Remove(t.Ruya.Id); Add(t.S, EffortlessCounterattack, 1, Zone.Hand); t.S.ActivePlayerId = 1; });
        var counter = r.S.Cards.Values.First(c => c.DefinitionId == EffortlessCounterattack);
        // Two additional actions are outstanding (as after an action that granted two); the holder takes one, which would create a third.
        r.S.AdditionalActions.Add(new(0, null, false)); r.S.AdditionalActions.Add(new(0, null, false));
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Eq(e.State.PendingChoice?.Type, "additionalActionReaction", "an additional action is deferred behind its Reaction window");
        Eq(Grants(e), 1, "one of the two was consumed by taking it");
        Accept(e, e.QueryActions(1).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "effortless-counterattack-negate").Command!);
        Decline(e); Decline(e);
        Check(e.State.PendingChoice is null, "the window closed");
        Check(!e.State.Cards[r.First.Id].Bowed, "the negated action did nothing");
        Eq(Grants(e), 1, "the negation lost that action and what it would have created (no third), not the other one that was outstanding");
        Eq(e.State.PriorityPlayerId, 0, "the burst is still the holder's to take");
        var next = Offered(e, 0, "training-bow-at-battle", r.Follow);
        Check(next is not null, "the next additional action is offered");
        Accept(e, next!.Command! with { TargetId = r.Second.Id });
        Check(e.State.PendingChoice is null, "the played Counterattack sits in the discard pile and cannot be played from there: the next additional action opens no window");
        Check(e.State.Cards[r.Second.Id].Bowed, "the additional action resolved"); Eq(Grants(e), 0);
    }

    // ---- what did not change ---------------------------------------------------------------------------------------------
    private static void OrdinaryActionAndPassStillEndTheBurst()
    {
        // Passing is an ordinary action too: it forfeits whatever was left.
        var passed = Table(t => t.S.Cards.Remove(t.Ruya.Id));
        var e = MatchEngine.FromTestState(passed.S);
        Use(e, 0, Grant, passed.Creator, passed.First.Id);
        Eq(Grants(e), 1);
        Accept(e, Cmd(e, "pass"));
        Eq(Grants(e), 0, "passing forfeits the unspent additional actions");

        // A grant scoped to one card (Two Hands' shape): a Battle action from another card is an ordinary action outside the scope and forfeits what was left.
        var scoped = Table(t => t.S.Cards.Remove(t.Ruya.Id));
        scoped.S.AdditionalActions.Add(new(0, [scoped.Creator.Id], false));
        var e2 = MatchEngine.FromTestState(scoped.S);
        Use(e2, 0, "training-bow-at-battle", scoped.Follow, scoped.Second.Id);
        Eq(Grants(e2), 0, "an ordinary action outside the grant's scope ends the burst");
    }

    private static void NegatedReactionDoesNotForfeit()
    {
        // Ruya's Reaction names a Merchant: the Merchant's own Reaction negates it. That Reaction is an action taken and negated -- the holder's
        // additional action is not forfeited by it.
        var r = Table(t => Add(t.S, "training-merchant-free-guard", 1, Zone.Battlefield, province: 0));
        var guard = r.S.Cards.Values.First(c => c.DefinitionId == "training-merchant-free-guard");
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        Eq(e.State.PendingChoice?.Type, "bowReaction"); Eq(Grants(e), 1);
        Use(e, 0, "ruya-bow", r.Ruya, guard.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the Reaction names his Merchant: he may react to it");
        Accept(e, e.QueryActions(1).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "training-negate-merchant-free").Command!);
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Decline(e);
        Check(!e.State.Cards[guard.Id].Bowed, "the Reaction's bow was negated");
        Eq(Grants(e), 1, "the holder's additional action survived the negated Reaction");
    }

    private static void SnapshotMidBurst()
    {
        var r = Table(t => Add(t.S, Hizumi, 1, Zone.Battlefield, province: 0));
        var hizumi = r.S.Cards.Values.First(c => c.DefinitionId == Hizumi);
        hizumi.AbilityUses["hizumi-negate-straighten"] = r.S.TurnNumber;
        var e = MatchEngine.FromTestState(r.S);
        Use(e, 0, Grant, r.Creator, r.First.Id);
        var state = e.State; state.Cards[hizumi.Id].AbilityUses.Clear();
        e = MatchEngine.FromTestState(state);
        Use(e, 0, "ruya-bow", r.Ruya, r.Second.Id);
        Eq(e.State.SuspendedWindows.Count, 1);
        var snapshot = JsonSerializer.Serialize(e.State);
        var restored = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(snapshot)!);
        Eq(JsonSerializer.Serialize(restored.State), snapshot, "round-trips");
        foreach (var engine in new[] { e, restored })
        {
            Decline(engine); // Hizumi's controller passes; both windows close (issue #644)
            Eq(engine.State.PriorityPlayerId, 0); Eq(Grants(engine), 1);
            Accept(engine, engine.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "training-bow-at-battle").Command! with { TargetId = r.Third.Id });
        }
        Eq(JsonSerializer.Serialize(e.State), JsonSerializer.Serialize(restored.State), "a snapshot taken mid-burst continues identically");
    }
}
