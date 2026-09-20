using static RegressionAssertions;
using L5R.Core;

// A Reaction window that opens only when someone could use it (AnyOutOfPlayReactionAvailable must agree with the Reaction offering, i.e. with
// ValidateAbility's zone rule). An out-of-play Reactor is offered from where its card may legally be played from -- a Strategy from HAND, a
// Reaction whose trigger says so (Singh Remnants) from hand or discard pile, a Discipline ability from a discard pile -- and NOT from anywhere
// else: a Strategy already played sits in the Fate discard pile and cannot be played again from there (Effortless Counterattack), and a card
// that is a Reactor only while IN PLAY (Akodo Kakihara) cannot react from hand. Before the fix each of those still counted, so every later
// additional action / Attack in the battle opened a window in which both players could only decline. Every position is played through the
// commands the engine offers (QueryActions -> Accept), never a hand-built one.
internal static class OutOfPlayReactionWindowTests
{
    private const string EffortlessCounterattack = "oracle-l5r-2283", Again = "oracle-l5r-127", ACloseCall = "oracle-l5r-10359", Kakihara = "oracle-l5r-223";
    private const string Negate = "effortless-counterattack-negate", DisciplineCounter = "training-discipline-counter", DisciplineNegate = "training-discipline-counter-negate";
    private const string Bow = "training-bow-at-battle";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("The reported repro, with the real cards: Again!, an additional action negated by Effortless Counterattack, then Again! and a further additional action -- the played (discarded) Counterattack opens no window and nothing is offered from it", PlayedCounterattackOpensNoLaterWindow),
            ("Additional action: Effortless Counterattack in HAND opens the window (and the same offer is enabled in it); the same card in the Fate discard pile, unplayed, does not -- the additional action just resolves; a window is never open with nothing offered", HandOpensDiscardDoesNot),
            ("Additional action: cards held by the player taking the action (a Counterattack in hand, a Discipline Reaction in their own discard pile), not their opponent, open no window", OnlyTheOpponentsCopyCounts),
            ("Attack: A Close Call in hand opens the attack window; a copy in a discard pile (as after it was played) does not, and the Attack resolves at once", DiscardedCloseCallOpensNoAttackWindow),
            ("Attack: a card that reacts only while IN PLAY (Akodo Kakihara) opens no attack window from the hand or a discard pile -- it could not be played from there -- and still opens one when he is in play and targeted", InPlayOnlyReactorInHandOpensNoWindow),
            ("Discipline: a Discipline Reaction in a discard pile (Fate or Dynasty) IS playable from there, so it opens the window, is offered enabled with its Discipline quote, and negates when paid for; from hand it is the free play", DisciplineReactionFromDiscardCounts),
        ];
        runner.Run("out-of-play-reaction-window", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static int _added;
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"oop-{s.Cards.Count}-{++_added}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // The engine's own offer for this ability from this card -- the command is never built by hand, so what is asserted is the offer itself.
    private static ActionOption? Offered(MatchEngine e, int player, string ability, CardInstance card, bool enabledOnly = true) =>
        e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && (o.Enabled || !enabledOnly) && o.Command?.AbilityId == ability && o.CardId == card.Id);
    private static void Pass(MatchEngine e, int player) => Accept(e, new("pass", player, e.Version));
    private static void Decline(MatchEngine e) => Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
    private static void CloseWindows(MatchEngine e) { while (e.State.PendingChoice is not null) Decline(e); }
    private static void BowWith(MatchEngine e, int player, CardInstance bower, CardInstance target) =>
        Accept(e, Offered(e, player, Bow, bower)!.Command! with { TargetId = target.Id });

    // The additional-action table. `holder` takes Battle actions (with `grants` unscoped additional actions outstanding, as after an action that granted
    // them) and `reactor` is the other player, the Active one, so only the burst itself can hand priority back to the holder; the reactor defends with
    // three Personalities the holder's two bowers can name. Nothing is in either hand yet.
    private sealed record Rig(MatchState S, int Holder, int Reactor, CardInstance Kensai, CardInstance Bower, CardInstance Bower2, CardInstance[] Targets);
    private static Rig AdditionalTable(int holder = 0, int grants = 2)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        var reactor = 1 - holder;
        s.Phase = Phase.Battle; s.ActivePlayerId = reactor; s.PriorityPlayerId = holder; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var kensai = Add(s, "training-kensai", holder, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", holder, Zone.Battlefield, province: 0);
        var bower2 = Add(s, "training-battle-bower", holder, Zone.Battlefield, province: 0);
        var targets = new[] { "training-crane-samurai", "training-lion-samurai", "training-lion-veteran" }.Select(d => Add(s, d, reactor, Zone.Battlefield, province: 0)).ToArray();
        for (var i = 0; i < grants; i++) s.AdditionalActions.Add(new(holder, null, false));
        return new(s, holder, reactor, kensai, bower, bower2, targets);
    }

    // ---- the reported repro ---------------------------------------------------------------------------------------------
    private static void PlayedCounterattackOpensNoLaterWindow()
    {
        var r = AdditionalTable(grants: 0);
        var again1 = Add(r.S, Again, 0, Zone.Hand); var again2 = Add(r.S, Again, 0, Zone.Hand);
        var counter = Add(r.S, EffortlessCounterattack, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);

        // Again! (an ordinary action) grants one additional action, taken with a Battle ability: the Counterattack in hand opens the window.
        Accept(e, Offered(e, 0, "again-boost", again1)!.Command!);
        Eq(e.State.AdditionalActions.Count, 1, "Again! granted an additional action");
        BowWith(e, 0, r.Bower, r.Targets[0]);
        Eq(e.State.PendingChoice?.Type, "additionalActionReaction", "the Counterattack in hand opens the window");
        Accept(e, Offered(e, 1, Negate, counter)!.Command!);
        CloseWindows(e);
        Check(!e.State.Cards[r.Targets[0].Id].Bowed, "the additional action was negated");
        Eq(e.State.Cards[counter.Id].Zone, Zone.FateDiscard, "and the Counterattack was played, so it sits in the Fate discard pile");
        Eq(e.State.AdditionalActions.Count, 0, "the burst is over");

        // Nothing is offered from the discard pile, and a later burst (a second Again!) opens no window: its additional action resolves at once.
        Check(Offered(e, 1, Negate, counter, enabledOnly: false) is null, "the played Counterattack is not offered from the discard pile, not even disabled");
        if (e.State.PriorityPlayerId == 1) Pass(e, 1);
        Eq(e.State.PriorityPlayerId, 0);
        Accept(e, Offered(e, 0, "again-boost", again2)!.Command!);
        Eq(e.State.AdditionalActions.Count, 1, "the second Again! granted another");
        BowWith(e, 0, r.Bower2, r.Targets[1]);
        Check(e.State.PendingChoice is null, $"no window: nothing could be played from the discard pile (got {e.State.PendingChoice?.Type})");
        Check(e.State.Cards[r.Targets[1].Id].Bowed, "the additional action simply resolved");
    }

    // ---- hand vs discard pile, and the window agreeing with the offer ---------------------------------------------------------
    private static void HandOpensDiscardDoesNot()
    {
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard, Zone.DynastyDiscard })
        {
            var r = AdditionalTable(grants: 1);
            var counter = Add(r.S, EffortlessCounterattack, 1, zone);
            var e = MatchEngine.FromTestState(r.S);
            BowWith(e, 0, r.Bower, r.Targets[0]);
            var window = e.State.PendingChoice?.Type == "additionalActionReaction";
            Eq(window, zone == Zone.Hand, $"a Counterattack in {zone}: the window opens exactly when it can be played");
            // A window is never open with nothing to play in it: whenever it opened, the reactor is offered the enabled Reaction.
            if (window) Check(Offered(e, 1, Negate, counter) is not null, "the window that opened offers the Reaction");
            else
            {
                Check(e.State.PendingChoice is null, "no window at all");
                Check(e.State.Cards[r.Targets[0].Id].Bowed, "the additional action resolved at once");
                Eq(e.State.Cards[counter.Id].Zone, zone, "and the card did not move");
            }
        }
    }

    // The window is the OPPONENT's to open: cards held by the player taking the additional action -- a Counterattack in hand, a Discipline Reaction
    // in their own discard pile (playable, but only by them) -- react to nothing of theirs.
    private static void OnlyTheOpponentsCopyCounts()
    {
        var r = AdditionalTable(grants: 1);
        Add(r.S, EffortlessCounterattack, r.Holder, Zone.Hand);
        Add(r.S, DisciplineCounter, r.Holder, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(r.S);
        BowWith(e, 0, r.Bower, r.Targets[0]);
        Check(e.State.PendingChoice is null, $"no window from the acting player's own cards (got {e.State.PendingChoice?.Type})");
        Check(e.State.Cards[r.Targets[0].Id].Bowed, "the additional action resolved at once");
    }

    // ---- the Attack window ---------------------------------------------------------------------------------------------------
    // Player 0 attacks with the Castle's Siege attack (the A Close Call table); player 1 holds the card under test and defends with an Armored Personality.
    private static (MatchEngine E, CardInstance Attacker, CardInstance Castle, CardInstance Target, CardInstance Card) AttackTable(string card, Zone zone, bool armor = true)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
        var attacker = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var held = Add(s, card, 1, zone, province: zone == Zone.Battlefield ? 0 : null);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        if (armor) Add(s, "training-armor", 1, Zone.Attached, province: 0, parent: target.Id);
        return (MatchEngine.FromTestState(s), attacker, castle, target, held);
    }
    private static void Siege(MatchEngine e, CardInstance castle, CardInstance target) =>
        Accept(e, new("useAbility", 0, e.Version, CardId: e.State.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: target.Id, AbilityId: "carpenter-castle-siege"));

    private static void DiscardedCloseCallOpensNoAttackWindow()
    {
        // In hand it can be played, so the attack window opens for it. (The window's own offer lists only in-play Reactors, so the hand play is not
        // asserted through QueryActions here -- see the attackReaction branch of MatchQueries; this test is about WHETHER the window opens.)
        var hand = AttackTable(ACloseCall, Zone.Hand);
        Siege(hand.E, hand.Castle, hand.Target);
        Eq(hand.E.State.PendingChoice?.Type, "attackReaction", "A Close Call in hand opens the attack window");
        CloseWindows(hand.E);
        Eq(hand.E.State.Cards[hand.Target.Id].Zone, Zone.Dead, "declined, the Attack destroyed the Armored target");

        // The same card in a discard pile (as after it was played) reacts to nothing: the Attack resolves with no window.
        foreach (var zone in new[] { Zone.FateDiscard, Zone.DynastyDiscard })
        {
            var discard = AttackTable(ACloseCall, zone);
            Siege(discard.E, discard.Castle, discard.Target);
            Check(discard.E.State.PendingChoice is null, $"no attack window for a card in {zone}, which cannot be played (got {discard.E.State.PendingChoice?.Type})");
            Eq(discard.E.State.Cards[discard.Target.Id].Zone, Zone.Dead, "the Attack resolved at once and destroyed its target");
        }
    }

    private static void InPlayOnlyReactorInHandOpensNoWindow()
    {
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard, Zone.DynastyDiscard })
        {
            // Akodo Kakihara is a Personality: his Reaction is only ever legal from play. Held elsewhere, with the Attack aimed at another card of his
            // controller, nobody could react -- so there is no window.
            var t = AttackTable(Kakihara, zone, armor: false);
            Siege(t.E, t.Castle, t.Target);
            Check(t.E.State.PendingChoice is null, $"Kakihara in {zone} opens no attack window (got {t.E.State.PendingChoice?.Type})");
        }
        // In play and targeted, he does react: the window opens and his reaction is the offer. (The control that the check above is not just always false.)
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var castle = Add(s, "training-castle", 0, Zone.Home);
        var kakihara = Add(s, Kakihara, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Siege(e, castle, kakihara);
        Eq(e.State.PendingChoice?.Type, "attackReaction", "an in-play Kakihara, targeted, opens the window");
    }

    // ---- Discipline ----------------------------------------------------------------------------------------------------------
    private static void DisciplineReactionFromDiscardCounts()
    {
        // Player 1 takes additional actions; player 0 (the one with Gold in the economy scenario) holds the Discipline Reaction.
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard, Zone.DynastyDiscard })
        {
            var r = AdditionalTable(holder: 1, grants: 1);
            var counter = Add(r.S, DisciplineCounter, 0, zone);
            var e = MatchEngine.FromTestState(r.S);
            BowWith(e, 1, r.Bower, r.Targets[0]);
            Eq(e.State.PendingChoice?.Type, "additionalActionReaction", $"a Discipline Reaction in {zone} can be played, so the window opens");
            var offer = Offered(e, 0, DisciplineNegate, counter);
            Check(offer is not null, "and it is offered enabled");
            if (zone != Zone.Hand)
            {
                Eq(offer!.Gold?.Cost, 2, $"from the {zone} pile it is the paid Discipline play, quoted at the card's 2 Gold");
                Check(offer.Gold!.Payments.Length > 0 && offer.Command!.SourceIds is { Length: > 0 }, "with a payment the engine suggests");
                Accept(e, offer.Command!);
                Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 2 Gold")), "and the 2 Gold was charged");
            }
            else
            {
                Check(offer!.Gold is null, "from hand it is the free play (no quote)");
                Accept(e, offer.Command!);
            }
            CloseWindows(e);
            Check(!e.State.Cards[r.Targets[0].Id].Bowed, $"negated from {zone}");
        }
    }
}
