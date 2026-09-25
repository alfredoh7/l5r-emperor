using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group A: the third part of the scenarios (see EmperorRemainingATests.cs).
internal static partial class EmperorRemainingATests
{
    private const string AsakoChukage = "oracle-l5r-10072";

    // Passes for whoever must act until `stop` holds.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    // ---- Deadly Orders --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DeadlyOrdersScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, DeadlyOrders, 0, Zone.Hand);
        var commander = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, bowed: true, province: 0), "Commander"); // Force 5; no "unbowed" is printed
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: plain.Id); // Force 2
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "deadly-orders-boost", card.Id, plain.Id), "a Personality who is not a Commander may not perform");
        var option = Batch336Support.Options(e, 0, "deadly-orders-boost", card.Id, commander.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(enemyFollower.Id) && !option.TargetIds.Contains(plain.Id), "only one of its player's Followers is a target");
        Accept(e, option.Command! with { TargetId = follower.Id });
        Eq(ForceMod(e, follower.Id), 4, "a Force bonus of twice its current Force (2)");
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "it is not destroyed yet");
        // The battle resolves (the Attacker wins with the boosted Follower), and after its resolution the Follower is destroyed.
        PassUntil(e, () => e.State.CurrentBattleProvince is null && e.State.Phase != Phase.Battle || e.State.Cards[follower.Id].Zone != Zone.Attached);
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "after the battle's resolution the Follower was destroyed");
        Check(e.State.Cards[plain.Id].Zone is Zone.Home or Zone.Battlefield, "its Personality lives");
        Eq(e.State.RemADeadlyOrders.Count, 0, "the mark is spent");
    }

    // ---- Deception Revealed ------------------------------------------------------------------------------------------------------------------------------------------

    private static void DeceptionScenario()
    {
        // The Reaction: player 0's Limited action bows a Ring (Asako Chukage's), and Deception Revealed straightens it.
        var s = Table(Phase.Action, 0);
        var chukage = Add(s, AsakoChukage, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home);
        var card = Add(s, DeceptionRevealed, 0, Zone.Hand);
        Deck(s, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "an action that bowed a Ring opens the window");
        Check(e.State.Cards[ring.Id].Bowed, "the Ring is bowed");
        Accept(e, Offer(e, 0, "deception-revealed-straighten", card.Id, ring.Id));
        Check(!e.State.Cards[ring.Id].Bowed, "it was straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was discarded");
        // An action that bowed no Ring: no window.
        var t = Table(Phase.Action, 0);
        var strategy = Add(t, Forefather, 0, Zone.Hand);
        Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Ancestor");
        Add(t, "training-ring-of-air", 0, Zone.Home);
        Add(t, DeceptionRevealed, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Enabled(e2, 0, "forefather-honor", strategy.Id).Command!);
        Check(e2.State.PendingChoice is null, "no Ring was bowed: no window");

        // The Battle action: -4F, and it bows if its player controls a Ring.
        MatchEngine Battle(bool ring, out CardInstance card2, out CardInstance target, out CardInstance follower)
        {
            var b = Table(Phase.Battle, 0);
            card2 = Add(b, DeceptionRevealed, 0, Zone.Hand);
            Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            target = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            follower = Add(b, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id);
            if (ring) Add(b, "training-ring-of-air", 0, Zone.Home);
            return MatchEngine.FromTestState(b);
        }
        {
            var b = Battle(true, out var card2, out var target, out var follower);
            var option = Enabled(b, 0, "deception-revealed-weaken", card2.Id);
            Check(option.TargetIds!.Contains(target.Id) && option.TargetIds.Contains(follower.Id), "a Personality or a Follower");
            Accept(b, option.Command! with { TargetId = target.Id });
            Eq(ForceMod(b, target.Id), -4, "-4F");
            Check(b.State.Cards[target.Id].Bowed, "bowed: its player controls a Ring");
        }
        {
            var b = Battle(false, out var card2, out _, out var follower);
            Accept(b, Offer(b, 0, "deception-revealed-weaken", card2.Id, follower.Id));
            Eq(ForceMod(b, follower.Id), -4, "a Follower gets -4F too");
            Check(!b.State.Cards[follower.Id].Bowed, "with no Ring it is not bowed");
        }
    }

    // ---- Duel of Serpents ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DuelSerpentsScenario()
    {
        // The Open half: a Nonhuman or Spirit bows to gain 3 Honor, only if another player has higher Family Honor.
        MatchEngine Open(int myHonor, out CardInstance card, out CardInstance nonhuman, out CardInstance spiritFollower, out CardInstance plain)
        {
            var s = Table(Phase.Action, 0); s.Players[0].Honor = myHonor; s.Players[1].Honor = 5;
            card = Add(s, DuelSerpents, 0, Zone.Hand);
            nonhuman = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Nonhuman");
            plain = Add(s, "training-crane-veteran", 0, Zone.Home);
            spiritFollower = Kw(Add(s, "training-follower", 0, Zone.Attached, parent: plain.Id), "Spirit");
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Open(2, out var card, out var nonhuman, out var spirit, out var plain);
            Check(!CanUse(e, 0, "duel-serpents-honor", card.Id, plain.Id), "a Personality who is neither Nonhuman nor Spirit may not perform");
            Accept(e, Enabled(e, 0, "duel-serpents-honor", card.Id, nonhuman.Id).Command!);
            Eq(Honor(e, 0), 5, "gain 3 Honor");
            Check(e.State.Cards[nonhuman.Id].Bowed, "the performer bowed as the cost");
        }
        {
            var e = Open(2, out var card, out _, out var spirit, out _);
            Accept(e, Enabled(e, 0, "duel-serpents-honor", card.Id, spirit.Id).Command!);
            Eq(Honor(e, 0), 5, "a Spirit Follower may perform too");
            Check(e.State.Cards[spirit.Id].Bowed, "and bows");
        }
        {
            var e = Open(9, out var card, out var nonhuman, out _, out _);
            Check(!CanUse(e, 0, "duel-serpents-honor", card.Id, nonhuman.Id), "with the highest Family Honor it is not playable");
        }
        // The Battle half: an unbowed Nonhuman or Spirit bows an enemy card without attachments.
        var b = Table(Phase.Battle, 0);
        var strategy = Add(b, DuelSerpents, 0, Zone.Hand);
        var spirit2 = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Spirit");
        var bowedSpirit = Kw(Add(b, "training-crane-veteran", 0, Zone.Battlefield, bowed: true, province: 0), "Spirit");
        var bare = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(b, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, "duel-serpents-bow", strategy.Id, bowedSpirit.Id), "a bowed Spirit may not perform");
        var option = Batch336Support.Options(be, 0, "duel-serpents-bow", strategy.Id, spirit2.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(bare.Id) && !option.TargetIds.Contains(armed.Id), "an enemy card without attachments");
        Accept(be, option.Command! with { TargetId = bare.Id });
        Check(be.State.Cards[bare.Id].Bowed, "bowed");
        Check(!be.State.Cards[spirit2.Id].Bowed, "the performer is not bowed by the Battle half");
    }

    // ---- Extended Maneuvers ----------------------------------------------------------------------------------------------------------------------------------------------

    private static void ExtendedManeuversScenario()
    {
        // Player 0 (the Attacker) controls the Terrain. The defenders keep their state through the resolution (only attackers bow afterwards), so they are the ones read.
        MatchEngine Build(out CardInstance mineSamurai, out CardInstance mineRonin, out CardInstance theirSamurai, out CardInstance theirRonin)
        {
            var s = Table(Phase.Battle, 0);
            Add(s, ExtendedManeuvers, 0, Zone.Battlefield, province: 0);
            mineSamurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Samurai");
            mineRonin = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            theirSamurai = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0), "Samurai");
            Batch336Support.Force(theirSamurai, 10);
            theirRonin = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(out var mineSamurai, out var mineRonin, out var theirSamurai, out var theirRonin);
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Eq(e.State.PendingChoice!.PlayerId, 0, "the Terrain's controller chooses");
            var options = e.QueryActions(0).Where(a => a.Type == "beforeBattleOptional").ToArray();
            Check(options.Any(a => a.Command!.Choice == "decline"), "he may do neither");
            Check(options.Any(a => a.Command!.Choice == "use" && a.Command.TargetId == theirSamurai.Id && a.Command.CardIds is null), "he may straighten a Samurai alone (either side's)");
            Check(options.Any(a => a.Command!.Choice == "use" && a.Command.TargetId is null && a.Command.CardIds?.Single() == mineRonin.Id), "he may bow a non-Samurai alone (either side's)");
            Check(!options.Any(a => a.Command!.Choice == "use" && (a.Command.TargetId == mineRonin.Id || a.Command.TargetId == theirRonin.Id)), "a non-Samurai is not a Samurai to straighten");
            var both = options.Single(a => a.Command!.TargetId == theirSamurai.Id && a.Command.CardIds?.Single() == theirRonin.Id);
            Accept(e, both.Command!);
            Check(!e.State.Cards[theirSamurai.Id].Bowed, "the Samurai was straightened");
            Check(e.State.Cards[theirRonin.Id].Bowed, "the non-Samurai was bowed");
            Check(e.State.PendingChoice?.Type != "beforeBattleOptional", "the choice is done and the battle resolves");
        }
        {
            var e = Build(out _, out _, out var theirSamurai, out var theirRonin);
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "beforeBattleOptional" && a.Command!.Choice == "decline").Command!);
            Check(e.State.Cards[theirSamurai.Id].Bowed && !e.State.Cards[theirRonin.Id].Bowed, "declining does neither");
        }
        {
            // A wrong choice is refused: an unbowed non-Samurai to straighten, or a Samurai to bow.
            var e = Build(out var mineSamurai, out _, out _, out var theirRonin);
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, TargetId: theirRonin.Id, Choice: "use"));
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, CardIds: [mineSamurai.Id], Choice: "use"));
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "use"));
        }
        {
            // The Terrain is played like any Terrain (a Battle action, none already there), from the hand.
            var s = Table(Phase.Battle, 0);
            var hand = Add(s, ExtendedManeuvers, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: hand.Id));
            Eq(e.State.Cards[hand.Id].Zone, Zone.Battlefield, "put into play at the battlefield");
        }
    }

    // ---- Face of Ninube ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FaceNinubeScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, FaceNinube, 0, Zone.Hand);
        var hand = Enumerable.Range(0, 3).Select(_ => Add(s, "training-focus-strategy", 0, Zone.Hand).Id).ToArray();
        for (var i = 0; i < 5; i++) Deck(s, 0);
        var e = MatchEngine.FromTestState(s);
        var deckBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, Enabled(e, 0, "face-ninube-draw", card.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, 3, "three cards were shuffled in and three drawn");
        Eq(e.State.Players[0].FateDeck.Count, deckBefore, "the deck holds as many as before");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is not part of the hand it shuffles and was discarded");
        Check(e.State.Events.Any(ev => ev.Type == "handShuffled"), "the shuffle is announced");
        // A hand of only the Strategy: nothing shuffled, nothing drawn.
        var t = Table(Phase.Action, 0);
        var lone = Add(t, FaceNinube, 0, Zone.Hand);
        Deck(t, 0);
        var e2 = MatchEngine.FromTestState(t);
        var deckLone = e2.State.Players[0].FateDeck.Count;
        Accept(e2, Enabled(e2, 0, "face-ninube-draw", lone.Id).Command!);
        Eq(e2.State.Players[0].Hand.Count, 0, "no card in hand to shuffle in, none drawn");
        Eq(e2.State.Players[0].FateDeck.Count, deckLone, "the deck is untouched");
    }

    // ---- Falling Leaf Strike -------------------------------------------------------------------------------------------------------------------------------------------

    private static void FallingLeafScenario()
    {
        MatchEngine Build(int enemyForce, out CardInstance card, out CardInstance monk, out CardInstance enemy, out CardInstance follower, out CardInstance home)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, FallingLeaf, 0, Zone.Hand);
            monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Monk"); // at home
            Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            Batch336Support.Force(enemy, enemyForce - 4);
            follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id); // Force 2
            home = Add(s, "training-lion-veteran", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(4, out var card, out var monk, out var enemy, out var follower, out var home);
            var option = Batch336Support.Options(e, 0, "falling-leaf-strike", card.Id, monk.Id).First(o => o.Enabled);
            Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(follower.Id) && !option.TargetIds.Contains(home.Id), "an enemy Personality or Follower at the battlefield");
            Accept(e, option.Command! with { TargetId = enemy.Id });
            Check(e.State.Cards[monk.Id].Zone == Zone.Battlefield && e.State.Cards[monk.Id].ProvinceIndex == 0, "the Monk moved to the current battlefield");
            Eq(ForceMod(e, enemy.Id), -3, "-3F");
            Check(!e.State.Cards[enemy.Id].Bowed, "Force 1 is not 0: not bowed");
        }
        {
            var e = Build(3, out var card, out var monk, out var enemy, out _, out _);
            Accept(e, Offer(e, 0, "falling-leaf-strike", card.Id, enemy.Id, monk.Id));
            Check(e.State.Cards[enemy.Id].Bowed, "Force 0 now: bowed");
        }
        {
            var e = Build(4, out var card, out var monk, out _, out var follower, out _);
            Accept(e, Offer(e, 0, "falling-leaf-strike", card.Id, follower.Id, monk.Id));
            Eq(ForceMod(e, follower.Id), -3, "a Follower may be the target");
            Check(e.State.Cards[follower.Id].Bowed, "a Force-2 Follower is at Force 0 now: bowed");
        }
        {
            // A bowed Monk may not perform.
            var s = Table(Phase.Battle, 0);
            var card = Add(s, FallingLeaf, 0, Zone.Hand);
            var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Monk");
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Check(!CanUse(MatchEngine.FromTestState(s), 0, "falling-leaf-strike", card.Id, monk.Id), "a bowed Monk may not perform");
        }
    }

    // ---- Final Confrontation ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void FinalConfrontationScenario()
    {
        // The Focus Effect: taking the Imperial Favor when the player's Personality won the duel, nothing when it lost.
        foreach (var (bonus, expected) in new[] { (5, (int?)0), (-2, (int?)null) })
        {
            var s = Table(Phase.Battle, 0);
            var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            mine.Modifiers.Add(new("test-source", "test", "Chi", bonus));
            var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var duelCard = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
            var focused = Add(s, FinalConfrontation, 0, Zone.Hand);
            Deck(s, 1);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            Eq(e.State.FavorPlayerId, expected, bonus > 0 ? "the winner took the Imperial Favor" : "the loser's Focus Effect gave nothing");
        }
        // The Battle action: one or two Personalities whose total Chi is at most the Duelist's.
        var b = Table(Phase.Battle, 0);
        var card = Add(b, FinalConfrontation, 0, Zone.Hand);
        var duelist = Kw(Add(b, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Duelist"); // Chi 4
        var bowedDuelist = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Duelist");
        var notDuelist = Add(b, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var chi3 = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var chi2a = Add(b, "training-scout-samurai", 1, Zone.Battlefield, province: 0); // Chi 2
        var chi2b = Add(b, "training-scout-samurai", 1, Zone.Battlefield, province: 0); // Chi 2
        var chi4 = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Chi 4
        var e2 = MatchEngine.FromTestState(b);
        Check(!CanUse(e2, 0, "final-confrontation-bow", card.Id, notDuelist.Id) && !CanUse(e2, 0, "final-confrontation-bow", card.Id, bowedDuelist.Id), "only an unbowed Duelist may perform");
        var command = Enabled(e2, 0, "final-confrontation-bow", card.Id, duelist.Id).Command!;
        Reject(e2, command with { Selections = [Pick("targets", chi3.Id, chi2a.Id)], TargetId = null }); // 5 > 4
        Reject(e2, command with { Selections = [Pick("targets", chi2a.Id, chi2b.Id, chi3.Id)], TargetId = null }); // three
        var ok = MatchEngine.FromTestState(e2.State);
        Accept(ok, command with { Selections = [Pick("targets", chi2a.Id, chi2b.Id)], ExpectedVersion = ok.Version, TargetId = null }); // 4 <= 4
        Check(ok.State.Cards[chi2a.Id].Bowed && ok.State.Cards[chi2b.Id].Bowed, "both were bowed");
        var single = MatchEngine.FromTestState(e2.State);
        Accept(single, command with { Selections = [Pick("targets", chi4.Id)], ExpectedVersion = single.Version, TargetId = null });
        Check(single.State.Cards[chi4.Id].Bowed, "one Personality with Chi equal to the Duelist's");
    }

    // ---- Flooded Pass ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FloodedPassScenario()
    {
        // Played with no unit at the battlefield; an ordinary Terrain may not be.
        var s = Table(Phase.Battle, 0);
        var flooded = Add(s, FloodedPass, 0, Zone.Hand);
        var maneuvers = Add(s, ExtendedManeuvers, 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.Enabled && a.CardId == flooded.Id), "Flooded Pass is offered with no unit at the battlefield");
        Check(!e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.Enabled && a.CardId == maneuvers.Id), "an ordinary Terrain is not");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: flooded.Id));
        Eq(e.State.Cards[flooded.Id].Zone, Zone.Battlefield, "put into play at the battlefield");
        // Not where a Terrain already is.
        var t = Table(Phase.Battle, 0);
        Add(t, FloodedPass, 0, Zone.Hand);
        Add(t, ExtendedManeuvers, 1, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Check(!e2.QueryActions(0).Any(a => a.Type == "playStrategy" && a.Enabled), "not where there is a Terrain already");

        // The province has +2 strength for each unit in the attacking army, while the Terrain is at its battlefield.
        var u = Table(Phase.Battle, 0);
        Add(u, FloodedPass, 1, Zone.Battlefield, province: 0);
        Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(u, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(u);
        var printed = e3.State.Players[1].Provinces[0].Strength;
        Eq(e3.GetView(0).Players[1].Provinces.Single(p => p.Index == 0).Strength, printed + 4, "two attacking units: +4 strength");
        Eq(e3.GetView(0).Players[1].Provinces.Single(p => p.Index == 1).Strength, e3.State.Players[1].Provinces[1].Strength, "another province is untouched");
        var v = Batch336Support.Edit(e3, st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Take(1).ToArray()) c.Zone = Zone.Home; });
        Eq(v.GetView(0).Players[1].Provinces.Single(p => p.Index == 0).Strength, printed + 2, "one attacking unit: +2 strength");
        var w = Batch336Support.Edit(e3, st => st.Cards.Values.First(c => Definition(c) == FloodedPass).Zone = Zone.FateDiscard);
        Eq(w.GetView(0).Players[1].Provinces.Single(p => p.Index == 0).Strength, printed, "gone with the Terrain");
    }

    private static string Definition(CardInstance card) => card.DefinitionId;

    // ---- Fortitude ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FortitudeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Fortitude, 0, Zone.Hand);
        var berserker = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Berserker"); // base Force 5
        berserker.Modifiers.Add(new("test-source", "test", "Force", -2)); // a current Force penalty
        var bowed = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Berserker");
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var boosted = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: boosted.Id); // unit total 4 + 2 = 6
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "fortitude-bow", card.Id, bowed.Id), "a bowed Berserker may not perform");
        var option = Batch336Support.Options(e, 0, "fortitude-bow", card.Id, berserker.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(weak.Id) && !option.TargetIds.Contains(strong.Id) && !option.TargetIds.Contains(boosted.Id), "only an enemy unit with total Force below the Berserker's BASE Force (5), not his current 3 or a stronger unit");
        Accept(e, option.Command! with { TargetId = weak.Id });
        Check(e.State.Cards[weak.Id].Bowed, "the target was bowed");
        Eq(ForceMod(e, berserker.Id), 0, "his current Force penalty was negated");
        Eq(e.State.Cards[berserker.Id].ForcePenaltiesNegatedTurn, e.State.TurnNumber, "and new ones are for the turn");
    }
}
