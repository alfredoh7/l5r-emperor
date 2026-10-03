using L5R.Core;
using static RegressionAssertions;

// Mendon's R4 rulings (issue #492), part 2: the Honor-change windows (Rhetoric, Solid Defense, Shiro Chugo) and what is, and is not, "an action's effects".
internal static partial class EmperorRulingR4Tests
{
    private const string Kazan = "oracle-l5r-4185", CarpenterCastle = "oracle-l5r-1218";

    // A player 1 who holds Solid Defense and a Personality of 5 or higher Force, ready to answer player 0's Weak Poison (an Open action of a Strategy: its "Lose 2 Honor." is an effect).
    private static MatchEngine PoisonRig(bool solid, bool force5, bool favor, out CardInstance poison, out CardInstance target, out CardInstance solidCard, int actor = 0)
    {
        var s = Table(Phase.Action, actor);
        poison = Add(s, WeakPoison, actor, Zone.Hand);
        target = Add(s, "training-lion-samurai", 1 - actor, Zone.Home);
        var reactor = 1 - actor;
        solidCard = solid ? Add(s, SolidDefense, reactor, Zone.Hand) : poison;
        var big = Add(s, "training-lion-samurai", reactor, Zone.Home);
        var natural = CardCatalog.Get(big.DefinitionId).Force;
        Force(big, (force5 ? 5 : 4) - natural);
        if (favor) s.FavorPlayerId = reactor;
        return MatchEngine.FromTestState(s);
    }

    private static void HonorCostVersusEffect()
    {
        // A true cost: a Strategy whose Battle Attack "costs its own controller Honor" (CostVerbs.LoseHonor, the fixture of the cost semantics). Nothing is played on it, and Solid Defense does not touch it.
        var s = Table(Phase.Battle, 0);
        var costly = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
        var striker = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(enemy, -CardCatalog.Get(enemy.DefinitionId).Force);
        Add(s, SolidDefense, 1, Zone.Hand); Add(s, Rhetoric, 1, Zone.Hand);
        var big = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(big, 5 - CardCatalog.Get(big.DefinitionId).Force);
        Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        var cost = Options(e, 0, "training-honor-cost-attack", costly.Id).FirstOrDefault(o => o.Enabled);
        Check(cost is not null, "the Attack is offered: " + string.Join("; ", Options(e, 0, "training-honor-cost-attack", costly.Id).Select(o => o.Label + " " + o.Reason)));
        Accept(e, cost!.Command!);
        var sawHonorWindow = e.State.PendingChoice?.Type == "honorChangeReaction";
        for (var guard = 0; guard < 8 && e.State.PendingChoice is not null; guard++) { Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); sawHonorWindow |= e.State.PendingChoice?.Type == "honorChangeReaction"; }
        Check(!sawHonorWindow, "Honor paid as a cost is not an effect: no Reaction is offered on it");
        Eq(Honor(e, 0), honor - 2, "and it was paid in full");
        Check(striker.Id.Length > 0, "fixture");

        // Printed after the colon it is an effect: A Weak Poison's "Lose 2 Honor." opens the window (it was paid as a cost, outside Rhetoric and Solid Defense), and is negated with the action.
        var p = PoisonRig(true, true, false, out var poison, out var victim, out var solid);
        var h0 = Honor(p, 0);
        Accept(p, Offer(p, 0, "weak-poison-force-zero", poison.Id, victim.Id));
        Eq(p.State.PendingChoice?.Type, "honorChangeReaction", "A Weak Poison's Honor loss is an effect: the window opens before it");
        Check(p.State.PendingHonorChange is { Amount: -2, PlayerId: 0 }, "it is the acting player's own loss of 2");
        Eq(Honor(p, 0), h0, "not paid yet");
        PassWindow(p);
        Eq(Honor(p, 0), h0 - 2, "declined: he loses 2 Honor after the Force was reduced");
        Check(p.State.Cards[victim.Id].Modifiers.Sum(m => m.Amount) < 0, "the rest of the action applied");

        // Negated with the action: the effect is not applied, so nothing is lost (the cost reading paid it even for a negated action).
        var n = Table(Phase.Action, 0);
        var poison2 = Add(n, WeakPoison, 0, Zone.Hand); var victim2 = Add(n, "training-lion-samurai", 1, Zone.Home);
        var negator = Add(n, "training-untargeted-negator", 1, Zone.Home);
        var ne = MatchEngine.FromTestState(n);
        var nh = Honor(ne, 0);
        Accept(ne, Offer(ne, 0, "weak-poison-force-zero", poison2.Id, victim2.Id));
        Accept(ne, Enabled(ne, 1, "training-negate-untargeted-action", negator.Id).Command!);
        PassWindow(ne);
        Eq(Honor(ne, 0), nh, "a negated action loses no Honor: it was an effect");
        Eq(ne.State.Cards[poison2.Id].Zone, Zone.FateDiscard, "the action was still taken");

        // Exploited Advantage: "Lose 2 Honor." is unconditional and an effect too.
        var b = Table(Phase.Battle, 0);
        var advantage = Add(b, ExploitedAdvantage, 0, Zone.Hand);
        var paragon = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
        var foe = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var sd = Add(b, SolidDefense, 1, Zone.Hand); var wall = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(wall, 5 - CardCatalog.Get(wall.DefinitionId).Force);
        var be = MatchEngine.FromTestState(b);
        var bh = Honor(be, 0);
        Accept(be, Offer(be, 0, "exploited-advantage-move", advantage.Id, foe.Id, paragon.Id));
        Eq(be.State.PendingChoice?.Type, "honorChangeReaction", "Exploited Advantage's Honor loss opens the window");
        Accept(be, Enabled(be, 1, "solid-defense-negate", sd.Id).Command!);
        PassWindow(be);
        Eq(Honor(be, 0), bh, "Solid Defense negated the loss");
        Eq(be.State.Cards[foe.Id].Zone, Zone.Home, "the rest of the action happened: he moved home");
        Check(wall.Id.Length > 0 && paragon.Id.Length > 0, "fixture");
    }

    private static void SolidDefenseAnyLoss()
    {
        // With a Personality of 5 or higher Force: free.
        var e = PoisonRig(true, true, false, out var poison, out var target, out var solid);
        var h0 = Honor(e, 0);
        Accept(e, Offer(e, 0, "weak-poison-force-zero", poison.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the loss is offered for negation where it would be applied");
        Eq(e.State.PriorityPlayerId, 1, "player 1 holds the Reaction and has the first opportunity");
        Check(Listed(e, 1, "solid-defense-negate").Any(o => o.Enabled) && !Listed(e, 1, "solid-defense-negate-favor").Any(o => o.Enabled), "the free half only");
        Accept(e, Enabled(e, 1, "solid-defense-negate", solid.Id).Command!);
        PassWindow(e);
        Eq(Honor(e, 0), h0, "the loss was negated");
        Eq(e.State.Cards[solid.Id].Zone, Zone.FateDiscard, "Solid Defense was played");
        Check(e.State.Cards[target.Id].Modifiers.Sum(m => m.Amount) < 0, "the action's other effect stands");

        // Without one, the Imperial Favor is discarded.
        var f = PoisonRig(true, false, true, out var poison2, out var target2, out var solid2);
        var f0 = Honor(f, 0);
        Accept(f, Offer(f, 0, "weak-poison-force-zero", poison2.Id, target2.Id));
        Check(!Listed(f, 1, "solid-defense-negate").Any(o => o.Enabled) && Listed(f, 1, "solid-defense-negate-favor").Any(o => o.Enabled), "the Favor half only");
        Accept(f, Enabled(f, 1, "solid-defense-negate-favor", solid2.Id).Command!);
        PassWindow(f);
        Eq(Honor(f, 0), f0, "negated");
        Eq(f.State.FavorPlayerId, (int?)null, "the Imperial Favor was discarded");

        // Nothing to pay with: no window.
        var g = PoisonRig(true, false, false, out var poison3, out var target3, out _);
        var g0 = Honor(g, 0);
        Accept(g, Offer(g, 0, "weak-poison-force-zero", poison3.Id, target3.Id));
        Check(g.State.PendingChoice is null, "no 5-Force Personality and no Favor: no window");
        Eq(Honor(g, 0), g0 - 2, "the loss stands");

        // Solid Defense is for ANOTHER player's action: the acting player's own action gives it no window (player 0 holds it and acts).
        var s = Table(Phase.Action, 0);
        var poison4 = Add(s, WeakPoison, 0, Zone.Hand); var target4 = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, SolidDefense, 0, Zone.Hand); var big = Add(s, "training-crane-samurai", 0, Zone.Home); Force(big, 5 - CardCatalog.Get(big.DefinitionId).Force);
        var own = MatchEngine.FromTestState(s);
        var o0 = Honor(own, 0);
        Accept(own, Offer(own, 0, "weak-poison-force-zero", poison4.Id, target4.Id));
        Check(own.State.PendingChoice is null, "his own action: no window for his Solid Defense");
        Eq(Honor(own, 0), o0 - 2, "he lost the Honor");
    }

    private static void WindowsPerChangeAndFocusEffects()
    {
        // Two Honor changes in one action: Kakita Kazan bows an enemy dishonorable Personality (its controller loses 2 Honor) and his player gains 2. Each gets its own window; Rhetoric played before the
        // second applies to the second only.
        MatchEngine Kaz(out CardInstance kazan, out CardInstance victim, out CardInstance rhetoric)
        {
            var s = Table(Phase.Battle, 0);
            kazan = Add(s, Kazan, 0, Zone.Battlefield, province: 0);
            victim = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); victim.Dishonorable = true;
            rhetoric = Add(s, Rhetoric, 1, Zone.Hand);
            Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
            return MatchEngine.FromTestState(s);
        }
        var e = Kaz(out var kazan, out var victim, out var rhetoric);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Offer(e, 0, "kazan-shame", kazan.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the first change (the enemy's loss) opens a window");
        Check(e.State.PendingHonorChange is { Occurrence: 0, Amount: -2, PlayerId: 1 }, "occurrence 0: player 1 loses 2");
        Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the loss stands, and the second change (his own gain) opens its own window");
        Check(e.State.PendingHonorChange is { Occurrence: 1, Amount: 2, PlayerId: 0 }, "occurrence 1: player 0 gains 2");
        Accept(e, Listed(e, 1, "rhetoric-redirect").First(o => o.Enabled && o.Command!.TargetPlayerId == 1).Command!);
        PassWindow(e);
        Eq(Honor(e, 1), h1 - 2 + 2, "player 1 lost 2 (first change) and took the gain of the second instead of player 0");
        Eq(Honor(e, 0), h0, "player 0 gained nothing");
        Check(e.State.Cards[victim.Id].Bowed, "the bow happened once, before the losses");

        // A Focus Effect is not an action (Mendon): Discovering a Conspiracy's Focus Effect gains 2 Honor after the duel, and no Reaction may be played on it though player 1 holds Rhetoric with a Courtier.
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
            var conspiracy = Add(s, Conspiracy, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            Add(s, Rhetoric, 1, Zone.Hand); Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
            var d = MatchEngine.FromTestState(s);
            var honor = Honor(d, 0);
            Accept(d, Offer(d, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(d, new GameCommand("focus", 1, d.Version, Choice: "deck"));
            Accept(d, new GameCommand("focus", 0, d.Version, CardId: conspiracy.Id));
            Accept(d, new GameCommand("strike", 1, d.Version));
            Check(d.State.PendingChoice?.Type != "honorChangeReaction", "the Focus Effect's Honor gain opens no window: it is not an action's effect");
            Eq(Honor(d, 0), honor + 2, "and it was gained in full");
        }

        // A duel's own outcome is the action's effect: The Aerie's "The winner's controller gains 1 Honor" opens the window at the outcome, and Rhetoric redirects it.
        {
            var s = Table(Phase.Battle, 1);
            var aerie = StrongholdAs(s, 1, TheAerie);
            var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var samurai = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0); samurai.Modifiers.Add(new("test", "test", "Chi", 8));
            Add(s, Rhetoric, 0, Zone.Hand); Kw(Add(s, "training-lion-courtier", 0, Zone.Home), "Courtier");
            var d = MatchEngine.FromTestState(s);
            var h = Honor(d, 1); var hMine = Honor(d, 0);
            Accept(d, Offer(d, 1, "aerie-duel", aerie.Id, mine.Id, samurai.Id));
            var opened = false;
            for (var guard = 0; guard < 14 && (d.State.PendingDuel is not null || d.State.PendingChoice is { Type: "honorChangeReaction" }); guard++)
            {
                if (d.State.PendingChoice is { Type: "honorChangeReaction" })
                {
                    opened = true;
                    Check(d.State.PendingHonorChange is { Amount: 1, PlayerId: 1 }, "the winner's controller would gain 1");
                    Accept(d, Listed(d, 0, "rhetoric-redirect").First(o => o.Enabled && o.Command!.TargetPlayerId == 0).Command!);
                    PassWindow(d);
                    continue;
                }
                var pending = d.State.PendingDuel!;
                Accept(d, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", d.State.PriorityPlayerId, d.Version) : new GameCommand("strike", pending.TurnPlayerId, d.Version));
            }
            Check(opened, "the duel's outcome opened the window");
            Eq(Honor(d, 1), h, "the winner's controller did not gain");
            Eq(Honor(d, 0), hMine + 1, "the chosen player took the gain");
            Check(d.State.Cards[mine.Id].Bowed && d.State.Cards[mine.Id].Dishonorable, "the rest of the duel's outcome applied");
        }

        // An Attack's own targeting effect (Carpenter Castle: gain 2 Honor when it targets a card): the window opens from the Attack's resolution, and the Attack goes on after it.
        {
            var s = Table(Phase.Battle, 0);
            var castle = StrongholdAs(s, 0, CarpenterCastle);
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var wall = Add(s, "training-castle", 0, Zone.Home);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, Rhetoric, 1, Zone.Hand); Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
            var a = MatchEngine.FromTestState(s);
            var h = Honor(a, 0); var h1b = Honor(a, 1);
            Accept(a, new GameCommand("useAbility", 0, a.Version, CardId: castle.Id, PerformerId: wall.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
            Eq(a.State.PendingChoice?.Type, "honorChangeReaction", "the Attack's targeting gain opens the window");
            Accept(a, Listed(a, 1, "rhetoric-redirect").First(o => o.Enabled && o.Command!.TargetPlayerId == 1).Command!);
            PassWindow(a);
            Eq(Honor(a, 0), h, "the redirected gain");
            Eq(Honor(a, 1), h1b + 2, "went to the chosen player");
            Eq(a.State.Cards[enemy.Id].Zone, Zone.Dead, "and the Attack destroyed its target as it always does");
        }
    }

    private static void PausedActionsAndProclaim()
    {
        // An Either/Or answer: Yasuki Tanimura's "the player may pay 4 Gold. If he does not pay the Gold, he loses 3 Honor" -- the loss is applied by the ANSWER, and is still from Tanimura's action:
        // player 1's Solid Defense (another player's action) negates it where it would be applied.
        var s = Table(Phase.Battle, 1);
        var tanimura = Add(s, "oracle-l5r-9464", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var sd = Add(s, SolidDefense, 1, Zone.Hand);
        var wall = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(wall, 5 - CardCatalog.Get(wall.DefinitionId).Force);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, tanimura.Id));
        Accept(e, Enabled(e, 0, "tanimura-political-pay", tanimura.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the acting player chooses");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!);
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "his answer applies an Honor loss from Tanimura's action: the window opens");
        Check(e.State.PendingHonorChange is { Amount: -3, PlayerId: 1 }, "3 Honor lost by player 1");
        Accept(e, Enabled(e, 1, "solid-defense-negate", sd.Id).Command!);
        PassWindow(e);
        Eq(Honor(e, 1), honor, "negated");

        // Proclaim is a Reaction (an action): its gain is from an action's effects, so Rhetoric may be played on it.
        var t = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(t, "training-marketplace", 0, Zone.Home);
        t.Players[0].Honor = 20;
        var cardId = t.Players[0].Provinces[0].CardId!; t.Cards[cardId].DefinitionId = "training-crane-samurai"; t.Cards[cardId].FaceUp = true;
        Add(t, Rhetoric, 1, Zone.Hand); Kw(Add(t, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var p = MatchEngine.FromTestState(t);
        Accept(p, new GameCommand("purchase", 0, p.Version, CardId: cardId, SourceIds: p.PaymentOptions(0, 5).First().SourceIds));
        var h0 = Honor(p, 0); var h1 = Honor(p, 1);
        Accept(p, p.QueryActions(0).First(o => o.Type == "proclaim" && o.Enabled).Command!);
        Eq(p.State.PendingChoice?.Type, "honorChangeReaction", "Proclaim's gain opens the window");
        Accept(p, Listed(p, 1, "rhetoric-redirect").First(o => o.Enabled && o.Command!.TargetPlayerId == 1).Command!);
        PassWindow(p);
        Eq(Honor(p, 0), h0, "the Proclaimer gained nothing");
        Check(Honor(p, 1) > h1, "the chosen player took the gain instead");
    }

    // Unimpeachable Name ("Before the resolution of another player's action: Reduce each Honor loss from its effects by 3"): the same moment as Solid Defense. Astonishing Accusation is not on the curated
    // list of actions that lose Honor and the target is not dishonorable, so the old pre-action window never opened for it; the loss still may be answered where it is applied.
    private static void UnimpeachableNameOnALoss()
    {
        MatchEngine Rig(out CardInstance accusation, out CardInstance target, out CardInstance name)
        {
            var s = Table(Phase.Action, 0);
            accusation = Add(s, "oracle-l5r-637", 0, Zone.Hand);
            target = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Kolat");
            name = Add(s, "oracle-l5r-8989", 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var accusation, out var target, out var name);
        var h1 = Honor(e, 1);
        Accept(e, Offer(e, 0, "accusation-honor-loss", accusation.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the loss opens the window (the old window's guess never saw it)");
        var offer = Listed(e, 1, "unimpeachable-name-reaction").FirstOrDefault(o => o.Enabled);
        Check(offer is not null, "Unimpeachable Name is offered on his own loss: " + string.Join("; ", e.QueryActions(1).Select(o => o.Label + " " + o.Reason)));
        Accept(e, offer!.Command!);
        PassWindow(e);
        Eq(Honor(e, 1), h1, "the loss was reduced by 3 to nothing");
        Eq(e.State.Cards[name.Id].Zone, Zone.FateDiscard, "and Unimpeachable Name was played");
        // Declined: the loss stands.
        var d = Rig(out var acc2, out var target2, out var name2);
        var d1 = Honor(d, 1);
        Accept(d, Offer(d, 0, "accusation-honor-loss", acc2.Id, target2.Id));
        PassWindow(d);
        Check(Honor(d, 1) < d1, "declined: player 1 lost the Honor");
        Eq(d.State.Cards[name2.Id].Zone, Zone.Hand, "and kept the Strategy");
        // Another player's loss, or the acting player's own action: nothing to reduce for the holder.
        var s3 = Table(Phase.Action, 0);
        var poison = Add(s3, WeakPoison, 0, Zone.Hand); var victim = Add(s3, "training-lion-samurai", 1, Zone.Home); Add(s3, "oracle-l5r-8989", 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "weak-poison-force-zero", poison.Id, victim.Id));
        var sawWindow = false;
        for (var guard = 0; guard < 8 && e3.State.PendingChoice is not null; guard++) { sawWindow |= e3.State.PendingChoice.Type == "honorChangeReaction"; Accept(e3, new("declineReaction", e3.State.PriorityPlayerId, e3.Version)); }
        Check(!sawWindow, "it is the acting player's own loss: nothing for player 1 to reduce, so no window before it (the old pre-action window may still open, for any action on its list)");
    }
}
