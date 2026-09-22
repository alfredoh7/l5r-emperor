using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #415 (stage 1 of Mendon's rulings of 2026-09-21): the timed straighten hold (a designated player, a point of the turn, a count of those points), Bamboo Harvesters seeded
// from its printed trait, movement locks ("negate his (future) movement"), Hibernation's shield, and the three cards built on them: Hibernation, Visitation, Strength of the Tsunami.
internal static class StraightenHoldTests
{
    private const string JinnKuen = "oracle-l5r-9445", Hibernation = "oracle-l5r-3065", Visitation = "oracle-l5r-9172", Tsunami = "oracle-l5r-7562", Bamboo = "oracle-l5r-679", ScouringFlood = "oracle-l5r-6502";

    public static void Run(TestRunner runner) => runner.Run("straighten-holds", [
        ("Published shape: Hibernation, Visitation and Strength of the Tsunami are the printed cards with their abilities, the Tsunami's +6F Effect, and their art", PublishedShapes),
        ("Hibernation: the Nonhuman bows, is held bowed through the other player's turn, and straightens in the Straighten phase of its controller's next turn (Skip 0)", HibernationTimeline),
        ("Hibernation: only an unbowed Nonhuman Personality or Follower may perform it, and the next Battle action another player aims at it is negated whole, once -- not its own controller's, not an Open action", HibernationShield),
        ("Bamboo Harvesters' printed trait seeds a hold: bowed at the start of the game it stays bowed through its controller's first turn and straightens in the second; one that came into play later has no hold", BambooSeeded),
        ("A hold refuses every straightening, by effect as well as by the phase: it is not a candidate for a straighten-only action, a hand-built command is refused, the view shows it, and a WhileInPlay hold ends with its card", HoldRefusesEffects),
        ("The hold's points: Owner and Skip decide when it is released (turn begins, Straighten Phase ends, Events reveal ends, Action Phase ends, turn ends), and a null Owner counts whoever's turn it is", HoldPoints),
        ("Visitation: the Ancestor is destroyed as the cost, the target bows and stays bowed until after its controller's next reveal of provinces, and the Limited half pays the players who control or lack an Ancestor", VisitationFlow),
        ("Strength of the Tsunami: +6F only for its Shugenja; the first action straightens a Water host (and may move him home) even while the Spell is bowed; the second moves an enemy home and locks his future movement for the turn", TsunamiFlow),
        ("A movement lock negates the effect that would move the card (home or to a battlefield), not the rules' own return home, and it is spent with the turn", MovementLockKinds),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 200 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            var result = e.Execute(pick.Command!);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    private static CardView ViewOf(MatchEngine e, string id) => e.GetView(0).Players.SelectMany(p => p.Home.Concat(p.Army)).Single(c => c.Id == id);

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var hib = CardCatalog.Get(Hibernation); var vis = CardCatalog.Get(Visitation); var tsu = CardCatalog.Get(Tsunami);
        Check(hib.Name == "Hibernation" && hib.Type == CardType.Strategy && hib.Focus == 1 && hib.Abilities!.SequenceEqual(new[] { "hibernation-hibernate" }), "Hibernation");
        Check(vis.Name == "Visitation" && vis.Type == CardType.Strategy && vis.Focus == 2 && vis.Abilities!.SequenceEqual(new[] { "visitation-bow", "visitation-honor" }), "Visitation");
        Check(tsu.Name == "Strength of the Tsunami" && tsu.Type == CardType.Spell && tsu.GoldCost == 8 && tsu.Focus == 2 && tsu.Keywords!.Contains("Water") && tsu.Effect == "tsunami-force"
            && tsu.Abilities!.SequenceEqual(new[] { "tsunami-straighten", "tsunami-move-home" }), "Strength of the Tsunami");
        foreach (var id in new[] { Hibernation, Visitation, Tsunami })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(CardCatalog.Get(Bamboo).Effect == "skip-first-straighten", "Bamboo Harvesters keeps its printed-trait Effect: the hold is seeded from it");
    }

    // ---- Hibernation ----------------------------------------------------------------------------------------------------------------------------
    private static void HibernationTimeline()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var sleeper = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(sleeper, "Nonhuman");
        var card = Add(s, Hibernation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var turn = e.State.TurnNumber;
        Accept(e, Enabled(e, 0, "hibernation-hibernate", card.Id, sleeper.Id).Command!);
        var held = e.State.Cards[sleeper.Id];
        Check(held.Bowed, "bowed as the cost");
        Check(held.StraightenHolds.Count == 1 && held.StraightenHolds[0] is { Owner: 0, Until: StraightenPoint.TurnBegins, Skip: 0 }, "held until its controller's next turn begins");
        Check(held.TargetShield, "and shielded");
        Check(ViewOf(e, sleeper.Id).StraightenBlocked, "the view says it will not straighten");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // The other player's turn: not the controller's, so the hold stands through it, Straighten phase included.
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[sleeper.Id].Bowed && e.State.Cards[sleeper.Id].StraightenHolds.Count == 1, "still bowed and held through the other player's turn");
        // The controller's next turn: the hold is released as the turn begins, so the Straighten phase straightens it.
        Passing(e, x => x.State.TurnNumber == turn + 2 && x.State.Phase == Phase.Action);
        Check(!e.State.Cards[sleeper.Id].Bowed && e.State.Cards[sleeper.Id].StraightenHolds.Count == 0, "straightened in the Straighten phase of its controller's next turn");
        Check(e.State.Events.Any(x => x.Type == "straightenReleased" && x.CardId == sleeper.Id), "logged");
    }

    private static void HibernationShield()
    {
        // Who may perform it: an unbowed Nonhuman Personality or Follower of the player's own, and nothing else.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var human = Add(s, "training-crane-samurai", 0, Zone.Home);
        var nonhuman = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(nonhuman, "Nonhuman");
        var bowedNonhuman = Add(s, "training-water-personality", 0, Zone.Home, bowed: true); GiveKeyword(bowedNonhuman, "Nonhuman");
        var enemyNonhuman = Add(s, "training-crane-veteran", 1, Zone.Home); GiveKeyword(enemyNonhuman, "Nonhuman");
        var beast = Add(s, "training-farm", 0, Zone.Home); // a Holding, however keyworded, is not a Personality or Follower
        GiveKeyword(beast, "Nonhuman");
        var card = Add(s, Hibernation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offers = Options(e, 0, "hibernation-hibernate", card.Id);
        Check(offers.Any(o => o.Enabled && o.Label.Contains($"({nonhuman.Id})")), "the unbowed Nonhuman may perform it");
        Check(!offers.Any(o => o.Enabled && (o.Label.Contains($"({human.Id})") || o.Label.Contains($"({bowedNonhuman.Id})") || o.Label.Contains($"({enemyNonhuman.Id})") || o.Label.Contains($"({beast.Id})"))),
            "not a card without the keyword, a bowed one, the other player's, or a Holding: " + string.Join("; ", offers.Select(o => $"{o.Label} [{o.Enabled}]")));

        // The shield: a Battle action of the OTHER player that targets it is negated whole, once.
        var t = Table(Phase.Battle, 1);
        var sleeper = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); sleeper.TargetShield = true;
        var otherNonhuman = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var bower = Add(t, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var again = Add(t, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 1, "training-bow-at-battle", bower.Id, sleeper.Id));
        Check(!f.State.Cards[sleeper.Id].Bowed, "the action was negated: he is not bowed");
        Check(!f.State.Cards[sleeper.Id].TargetShield, "and the shield is spent");
        Check(f.State.Cards[bower.Id].AbilityUses.Count > 0, "the action was still taken (its use is spent)");
        Check(f.State.Events.Any(x => x.Type == "actionNegated" && x.Message.Contains("hibernating")), "logged");
        var next = Edit(f, st => st.PriorityPlayerId = 1);
        Accept(next, Offer(next, 1, "training-bow-at-battle", again.Id, sleeper.Id));
        Check(next.State.Cards[sleeper.Id].Bowed, "the next such action is not turned away");

        // Its own controller's action does not spend it: a Battle action of his own that targets his shielded Personality straightens him and the shield stays.
        var u = Table(Phase.Battle, 0); u.PriorityPlayerId = 0;
        var mine = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); mine.TargetShield = true;
        var ownStraightener = Add(u, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        var own = Options(g, 0, "training-straighten-at-battle", ownStraightener.Id).FirstOrDefault(o => o.Enabled && (o.TargetIds?.Contains(mine.Id) ?? false));
        Check(own is not null, "the straighten action may target the player's own Personality");
        Accept(g, own!.Command! with { TargetId = mine.Id });
        Check(!g.State.Cards[mine.Id].Bowed && g.State.Cards[mine.Id].TargetShield, "his own controller's action is not turned away and does not spend the shield");

        // An Open action of the other player is not a Battle action: it bows him, and the shield stays.
        var v = Table(Phase.Action, 1); v.PriorityPlayerId = 1;
        var sleeping = Add(v, "training-crane-samurai", 0, Zone.Home); sleeping.TargetShield = true;
        var opener = Add(v, "training-bow-any-source", 1, Zone.Home);
        var h = MatchEngine.FromTestState(v);
        Accept(h, Offer(h, 1, "training-bow-any", opener.Id, sleeping.Id));
        Check(h.State.Cards[sleeping.Id].Bowed && h.State.Cards[sleeping.Id].TargetShield, "an Open action is not a Battle action: it takes effect and the shield stays");
    }

    // ---- Bamboo Harvesters ----------------------------------------------------------------------------------------------------------------------
    private static void BambooSeeded()
    {
        // The start of the game: player 1 has taken no turn, its Bamboo Harvesters sits bowed.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 1; s.Players[1].TurnsTaken = 0;
        var bamboo = Add(s, Bamboo, 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var turn = e.State.TurnNumber;
        Check(ViewOf(e, bamboo.Id).StraightenBlocked, "before its controller's first turn the view says it will not straighten");
        Check(e.State.Cards[bamboo.Id].StraightenHolds.Count == 0, "and the view did not change the state");
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        var seeded = e.State.Cards[bamboo.Id];
        Check(seeded.Bowed, "still bowed through its controller's FIRST turn");
        Check(seeded.StraightenHolds.Count == 1 && seeded.StraightenHolds[0] is { Owner: 1, Until: StraightenPoint.TurnBegins, Skip: 0 }, "seeded with 1 and counted down to 0 as that turn began");
        Passing(e, x => x.State.TurnNumber == turn + 3 && x.State.Phase == Phase.Action);
        Check(!e.State.Cards[bamboo.Id].Bowed && e.State.Cards[bamboo.Id].StraightenHolds.Count == 0, "straightened on its controller's SECOND turn");

        // A Holding that came into play after its first turn has nothing to wait for.
        var late = Table(Phase.Action, 0); late.PriorityPlayerId = 0; late.Players[0].TurnsTaken = 4; late.Players[1].TurnsTaken = 4;
        var later = Add(late, Bamboo, 1, Zone.Home, bowed: true);
        var f = MatchEngine.FromTestState(late);
        Check(!ViewOf(f, later.Id).StraightenBlocked, "no hold for a card that came into play after the turns it waits for");
        var t2 = f.State.TurnNumber;
        Passing(f, x => x.State.TurnNumber == t2 + 1 && x.State.Phase == Phase.Action);
        Check(!f.State.Cards[later.Id].Bowed, "straightened on the next turn");

        // In its controller's first turn, after that turn has begun: an effect that would straighten it is refused too (the trait is a hold, not a phase rule).
        MatchEngine Jinn(int turnsTaken)
        {
            var mid = Table(Phase.Action, 1); mid.PriorityPlayerId = 1; mid.Players[1].TurnsTaken = turnsTaken;
            Add(mid, JinnKuen, 1, Zone.Home);
            Add(mid, Bamboo, 1, Zone.Home, bowed: true);
            return MatchEngine.FromTestState(mid);
        }
        var g = Jinn(1);
        var bambooId = g.State.Cards.Values.Single(c => c.DefinitionId == Bamboo).Id;
        var jinnOption = g.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "jinn-kuen-straighten");
        Check(jinnOption.Enabled && !jinnOption.TargetIds!.Contains(bambooId), "Jinn-Kuen may not straighten it in its controller's first turn");
        Check(g.State.Cards[bambooId].StraightenHolds is [{ Owner: 1, Until: StraightenPoint.TurnBegins, Skip: 0 }], "the hold stands until the second turn begins");
        var h = Jinn(2);
        var later2 = h.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "jinn-kuen-straighten");
        Check(later2.TargetIds!.Contains(h.State.Cards.Values.Single(c => c.DefinitionId == Bamboo).Id), "from the second turn it is an ordinary candidate");
    }

    // ---- the primitive ------------------------------------------------------------------------------------------------------------------------
    private static void HoldRefusesEffects()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var held = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var free = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        held.StraightenHolds.Add(new StraightenHold { Source = "test", Owner = 0, Until = StraightenPoint.TurnBegins });
        var e = MatchEngine.FromTestState(s);
        var straightener = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-straighten-at-battle" && a.Enabled);
        Check(!straightener.TargetIds!.Contains(held.Id) && straightener.TargetIds.Contains(free.Id), "a held card is not a candidate for a straighten-only action, a free one is");
        var refused = e.Execute(straightener.Command! with { TargetId = held.Id, ExpectedVersion = e.Version });
        Check(!refused.Accepted && refused.Error!.Contains("hold"), "a hand-built command is refused with the reason: " + refused.Error);
        Check(ViewOf(e, held.Id).StraightenBlocked && !ViewOf(e, free.Id).StraightenBlocked, "the view shows which card is held");

        // "While this card remains in play": the hold ends with its source.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var source = Add(t, "training-crane-veteran", 1, Zone.Home);
        var target = Add(t, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        target.StraightenHolds.Add(new StraightenHold { Source = "test", Owner = null, Until = StraightenPoint.TurnEnds, WhileInPlay = source.Id });
        var f = MatchEngine.FromTestState(t);
        Check(ViewOf(f, target.Id).StraightenBlocked, "held while its source is in play");
        var g = Edit(f, st => st.Cards[source.Id].Zone = Zone.FateDiscard);
        Check(!ViewOf(g, target.Id).StraightenBlocked, "and not once the source has left play");
        // The same through the rules path: the straighten-only action's candidates.
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 0;
        var keeper = Add(u, "training-crane-veteran", 1, Zone.Home);
        var sleeper = Add(u, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Add(u, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        Add(u, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        sleeper.StraightenHolds.Add(new StraightenHold { Source = "test", Owner = null, Until = StraightenPoint.TurnEnds, WhileInPlay = keeper.Id });
        var e2 = MatchEngine.FromTestState(u);
        Check(!e2.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-straighten-at-battle" && a.Enabled).TargetIds!.Contains(sleeper.Id), "not a candidate while the card he waits on is in play");
        var e3 = Edit(e2, st => st.Cards[keeper.Id].Zone = Zone.FateDiscard);
        Check(e3.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-straighten-at-battle" && a.Enabled).TargetIds!.Contains(sleeper.Id), "a candidate once it has left play: the hold ended with it");
    }

    private static void HoldPoints()
    {
        // One card per point, all held by the player whose turn it is or the other's; walk a turn and a half and watch each release.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 2; s.Players[1].TurnsTaken = 2;
        CardInstance Held(string def, int owner, StraightenPoint until, int? holdOwner, int skip = 0)
        {
            var c = Add(s, def, owner, Zone.Home, bowed: true);
            c.StraightenHolds.Add(new StraightenHold { Source = "test", Owner = holdOwner, Until = until, Skip = skip });
            return c;
        }
        var thisActionEnds = Held("training-crane-samurai", 0, StraightenPoint.ActionPhaseEnds, 0);
        var thisTurnEnds = Held("training-lion-samurai", 1, StraightenPoint.TurnEnds, null);
        var nextTurnBegins = Held("training-crane-veteran", 0, StraightenPoint.TurnBegins, 0);
        var skipTwo = Held("training-water-personality", 0, StraightenPoint.TurnBegins, 0, skip: 1);
        var otherPlayersTurn = Held("training-shugenja", 0, StraightenPoint.TurnBegins, 1);
        var phaseHold = Held("training-lion-samurai", 0, StraightenPoint.StraightenPhaseEnds, 0);
        var e = MatchEngine.FromTestState(s);
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.Phase == Phase.AttackDeclaration || x.State.Phase == Phase.Dynasty || x.State.Phase == Phase.End || x.State.TurnNumber != turn);
        Check(e.State.Cards[thisActionEnds.Id].StraightenHolds.Count == 0, "an Action-Phase-ends hold of the active player is released when the phase ends");
        Check(e.State.Cards[thisTurnEnds.Id].StraightenHolds.Count == 1, "a turn-ends hold is not released yet");
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[thisTurnEnds.Id].StraightenHolds.Count == 0 && !e.State.Cards[thisTurnEnds.Id].Bowed, "an Owner-less turn-ends hold counts whoever's turn it is: released as the turn ended, so the next Straighten phase straightened it");
        Check(e.State.Cards[nextTurnBegins.Id].StraightenHolds.Count == 1 && e.State.Cards[nextTurnBegins.Id].Bowed, "a hold on its own controller's next turn is not released by the other player's turn beginning");
        Check(e.State.Cards[phaseHold.Id].StraightenHolds.Count == 1, "nor is one on its controller's Straighten phase released by the other player's Straighten phase");
        Check(e.State.Cards[otherPlayersTurn.Id].StraightenHolds.Count == 0 && e.State.Cards[otherPlayersTurn.Id].Bowed, "a hold named for the other player's turn is released as THEIR turn begins (his own controller's Straighten phase is still to come)");
        Passing(e, x => x.State.TurnNumber == turn + 2 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[nextTurnBegins.Id].StraightenHolds.Count == 0 && !e.State.Cards[nextTurnBegins.Id].Bowed, "released as its controller's next turn began: straightened by that Straighten phase");
        Check(!e.State.Cards[otherPlayersTurn.Id].Bowed, "and the one whose hold the other player's turn released straightened now");
        Check(e.State.Cards[phaseHold.Id].StraightenHolds.Count == 0 && e.State.Cards[phaseHold.Id].Bowed, "a Straighten-Phase-ends hold stood through that very Straighten phase (still bowed) and is released after it");
        Check(e.State.Cards[skipTwo.Id].StraightenHolds is [{ Skip: 0 }] && e.State.Cards[skipTwo.Id].Bowed, "Skip 1: one of its controller's turns went by with the hold standing (count now 0), and it is still bowed");
        Passing(e, x => x.State.TurnNumber == turn + 4 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[skipTwo.Id].StraightenHolds.Count == 0 && !e.State.Cards[skipTwo.Id].Bowed, "released on the next one, and straightened");
        Check(!e.State.Cards[phaseHold.Id].Bowed, "and the Straighten-Phase-ends card straightens in the phase after");
    }

    // ---- Visitation -----------------------------------------------------------------------------------------------------------------------------
    private static void VisitationFlow()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var ancestor = Add(s, "training-ancestor", 0, Zone.Home);
        var target = Add(s, "training-crane-veteran", 1, Zone.Home);
        var card = Add(s, Visitation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var turn = e.State.TurnNumber;
        Accept(e, Use(e, 0, "visitation-bow", card.Id, target.Id, ancestor.Id));
        Check(e.State.Cards[ancestor.Id].Zone == Zone.Dead, "the Ancestor was destroyed as the cost");
        Check(e.State.Cards[target.Id].Bowed, "the target bowed");
        Check(e.State.Cards[target.Id].StraightenHolds is [{ Owner: 0, Until: StraightenPoint.EventsRevealEnds, Skip: 0 }], "and is held until Visitation's controller reveals her provinces");
        // The target's own controller's next turn: still bowed (Visitation's controller has not revealed yet).
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[target.Id].Bowed, "still bowed through his own controller's Straighten phase");
        // Visitation's controller's next turn: the reveal ends and the hold is released -- but his Straighten phase is his controller's, so he waits for that one.
        Passing(e, x => x.State.TurnNumber == turn + 2 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[target.Id].StraightenHolds.Count == 0, "released after Visitation's controller's reveal at the start of her next turn");
        Check(e.State.Cards[target.Id].Bowed, "he is straightened by his own controller's next Straighten phase, not before");
        Passing(e, x => x.State.TurnNumber == turn + 3 && x.State.Phase == Phase.Action);
        Check(!e.State.Cards[target.Id].Bowed, "and there he is");

        // Refused without an Ancestor to destroy.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var plain = Add(t, "training-crane-samurai", 0, Zone.Home); var c2 = Add(t, Visitation, 0, Zone.Hand); var victim = Add(t, "training-crane-veteran", 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Check(!Options(f, 0, "visitation-bow", c2.Id).Any(o => o.Enabled), "no Ancestor, no Visitation");

        // The Limited half.
        MatchEngine Honors(bool mine, bool theirs)
        {
            var h = Table(Phase.Action, 0); h.PriorityPlayerId = 0;
            if (mine) Add(h, "training-ancestor", 0, Zone.Home);
            if (theirs) Add(h, "training-ancestor", 1, Zone.Home);
            var c = Add(h, Visitation, 0, Zone.Hand);
            var g = MatchEngine.FromTestState(h);
            var before0 = Honor(g, 0); var before1 = Honor(g, 1);
            Accept(g, Enabled(g, 0, "visitation-honor", c.Id).Command!);
            Eq(Honor(g, 0) - before0, mine ? 2 : -2, $"mine={mine}: the player's Honor");
            Eq(Honor(g, 1) - before1, theirs ? 0 : -2, $"theirs={theirs}: the other player's Honor");
            return g;
        }
        Honors(true, false); Honors(true, true); Honors(false, false); Honors(false, true);
    }

    // ---- Strength of the Tsunami ---------------------------------------------------------------------------------------------------------------
    private static void TsunamiFlow()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var host = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, Tsunami, 0, Zone.Attached, province: 0, parent: host.Id);
        var enemy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var baseForce = Definition(host).Force;
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == host.Id).Force, baseForce + 6, "+6F for the Shugenja it is attached to");

        // The second action: bow the Spell, move an enemy home, and negate his future movement.
        Accept(e, Offer(e, 0, "tsunami-move-home", spell.Id, enemy.Id));
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy moved home");
        Check(e.State.Cards[enemy.Id].MovementLocks.Count == 1, "and his future movement is negated");
        // ...his controller cannot move him back to the battlefield this turn.
        CardInstance? mover = null;
        var again = Edit(e, st => { st.PriorityPlayerId = 1; mover = Add(st, "training-move-battle-strategy", 1, Zone.Hand); });
        Accept(again, Offer(again, 1, "training-move-to-battle", mover!.Id, enemy.Id));
        Eq(again.State.Cards[enemy.Id].Zone, Zone.Home, "the movement is negated: he stays home");
        Check(again.State.Events.Any(x => x.Type == "moveNegated" && x.CardId == enemy.Id), "logged");

        // Attached to a Personality who is not a Shugenja it gives no Force.
        var w0 = Table(Phase.Battle, 1); var plain = Add(w0, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(w0, Tsunami, 0, Zone.Attached, province: 0, parent: plain.Id);
        var pe = MatchEngine.FromTestState(w0);
        Eq(pe.GetView(0).Players[0].Army.Single(c => c.Id == plain.Id).Force, Definition(plain).Force, "no Force for a Personality who is not a Shugenja");
        // If his movement was negated (a lock already stands), he did not move, so no lock is added.
        var w1 = Table(Phase.Battle, 1); w1.PriorityPlayerId = 0;
        var wh = Add(w1, "training-water-shugenja", 0, Zone.Battlefield, province: 0); var ws = Add(w1, Tsunami, 0, Zone.Attached, province: 0, parent: wh.Id);
        var pre = Add(w1, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        pre.MovementLocks.Add(new("earlier", w1.TurnNumber, MovementKinds.Home));
        var we = MatchEngine.FromTestState(w1);
        Accept(we, Offer(we, 0, "tsunami-move-home", ws.Id, pre.Id));
        Check(we.State.Cards[pre.Id].Zone == Zone.Battlefield && we.State.Cards[pre.Id].MovementLocks.Count == 1, "he did not move, so \"if he moved\" is false and no lock is added");

        // The first action works while the Spell is bowed; a Water host is straightened, and the player may also move him home.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var water = Add(t, "training-water-shugenja", 0, Zone.Battlefield, province: 0, bowed: true);
        var sp = Add(t, Tsunami, 0, Zone.Attached, bowed: true, province: 0, parent: water.Id);
        Add(t, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, "tsunami-straighten", sp.Id).Command!);
        Eq(f.State.PendingChoice?.Type, "effectChoice", "the player chooses whether to move him home too");
        Accept(f, f.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "straighten-move").Command!);
        Check(!f.State.Cards[water.Id].Bowed, "the Water Shugenja was straightened");
        Eq(f.State.Cards[water.Id].Zone, Zone.Home, "and moved home");
        // Straighten only.
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 0;
        var w2 = Add(u, "training-water-shugenja", 0, Zone.Battlefield, province: 0, bowed: true); var sp2 = Add(u, Tsunami, 0, Zone.Attached, province: 0, parent: w2.Id);
        Add(u, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Enabled(g, 0, "tsunami-straighten", sp2.Id).Command!);
        Accept(g, g.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "straighten").Command!);
        Check(!g.State.Cards[w2.Id].Bowed && g.State.Cards[w2.Id].Zone == Zone.Battlefield, "straightened and left where he was");
        // A Shugenja who is not Water: nothing happens and nothing is asked.
        var v = Table(Phase.Battle, 1); v.PriorityPlayerId = 0;
        var fire = Add(v, "training-shugenja", 0, Zone.Battlefield, province: 0, bowed: true); var sp3 = Add(v, Tsunami, 0, Zone.Attached, province: 0, parent: fire.Id);
        Add(v, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(v);
        Accept(h, Enabled(h, 0, "tsunami-straighten", sp3.Id).Command!);
        Check(h.State.PendingChoice is null && h.State.Cards[fire.Id].Bowed, "a Shugenja who is not Water is neither straightened nor asked");
    }

    private static CardDefinition Definition(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    private static void MovementLockKinds()
    {
        // Home: the Attacker's Scouring Flood would move a defending Personality home; the one under a Home lock stays, the other goes.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var host = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var flood = Add(s, ScouringFlood, 1, Zone.Attached, province: 0, parent: host.Id);
        var lockedDefender = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var freeDefender = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        lockedDefender.MovementLocks.Add(new("test", s.TurnNumber, MovementKinds.Home));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "scouring-flood-move-home", flood.Id, lockedDefender.Id));
        Eq(e.State.Cards[lockedDefender.Id].Zone, Zone.Battlefield, "a Home lock negates the effect that would move him home");
        Check(e.State.Events.Any(x => x.Type == "moveNegated" && x.CardId == lockedDefender.Id), "logged");
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 1;
        var host2 = Add(t, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var flood2 = Add(t, ScouringFlood, 1, Zone.Attached, province: 0, parent: host2.Id);
        var free2 = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 1, "scouring-flood-move-home", flood2.Id, free2.Id));
        Eq(f.State.Cards[free2.Id].Zone, Zone.Home, "without one he goes home");

        // A lock of another kind does not stop it, and a lock is for its own turn only.
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1;
        var host3 = Add(u, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var flood3 = Add(u, ScouringFlood, 1, Zone.Attached, province: 0, parent: host3.Id);
        var wrongKind = Add(u, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        wrongKind.MovementLocks.Add(new("test", u.TurnNumber, MovementKinds.ToBattlefield));
        var spent = Add(u, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        spent.MovementLocks.Add(new("test", u.TurnNumber - 1, MovementKinds.Home));
        var g = MatchEngine.FromTestState(u);
        Accept(g, Offer(g, 1, "scouring-flood-move-home", flood3.Id, wrongKind.Id));
        Eq(g.State.Cards[wrongKind.Id].Zone, Zone.Home, "a lock on movement to battlefields does not stop a move home");
        var h = Edit(g, st => { st.PriorityPlayerId = 1; st.Cards[flood3.Id].Bowed = false; st.Cards[flood3.Id].AbilityUses.Clear(); });
        Accept(h, Offer(h, 1, "scouring-flood-move-home", flood3.Id, spent.Id));
        Eq(h.State.Cards[spent.Id].Zone, Zone.Home, "a lock made on an earlier turn is spent");

        // To a battlefield and assignment: each refused for the locked card, allowed for the others.
        var a = Table(Phase.Battle, 1); a.PriorityPlayerId = 1;
        Add(a, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var away = Add(a, "training-crane-veteran", 1, Zone.Home); var awayFree = Add(a, "training-crane-samurai", 1, Zone.Home);
        var m1 = Add(a, "training-move-battle-strategy", 1, Zone.Hand); var m2 = Add(a, "training-move-battle-strategy", 1, Zone.Hand);
        away.MovementLocks.Add(new("test", a.TurnNumber, MovementKinds.ToBattlefield));
        var k = MatchEngine.FromTestState(a);
        Accept(k, Offer(k, 1, "training-move-to-battle", m1.Id, away.Id));
        Eq(k.State.Cards[away.Id].Zone, Zone.Home, "a lock on movement to battlefields negates the move");
        var k2 = Edit(k, st => st.PriorityPlayerId = 1);
        Accept(k2, Offer(k2, 1, "training-move-to-battle", m2.Id, awayFree.Id));
        Eq(k2.State.Cards[awayFree.Id].Zone, Zone.Battlefield, "an unlocked card moves");

        var b = Table(Phase.InfantryDeclaration, 1); b.PriorityPlayerId = 1;
        var barred = Add(b, "training-crane-veteran", 1, Zone.Home); var allowed = Add(b, "training-crane-samurai", 1, Zone.Home);
        barred.MovementLocks.Add(new("test", b.TurnNumber, MovementKinds.Assign));
        var n = MatchEngine.FromTestState(b);
        Reject(n, new GameCommand("attack", 1, n.Version, Assignments: new() { [barred.Id] = 0 }));
        Accept(n, new GameCommand("attack", 1, n.Version, Assignments: new() { [allowed.Id] = 0 }));
        Eq(n.State.Cards[allowed.Id].Zone, Zone.Battlefield, "an unlocked unit may be assigned");
    }
}
