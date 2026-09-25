using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: Boastful Proclamation, Delayed Arrival and Glory of the Shogun (a part of EmperorRemainingFTests).
internal static partial class EmperorRemainingFTests
{
    private const string BoastKey = "boastful-proclamation:armed";

    // ---- Boastful Proclamation --------------------------------------------------------------------------------------------------------------------------------

    private static void BoastfulProclamationTests()
    {
        // Armed: another player controls a Personality when it resolves. At the start of its owner's next turn the province (his fourth) permanently has +3 strength and he gains 3 Honor.
        var e = Reveal(BoastfulProclamation, out var boast, s => Add(s, "training-crane-samurai", 0, Zone.Home));
        var card = e.State.Cards[boast.Id];
        Eq(card.Zone, Zone.EventInPlay, "it stays in play");
        Eq(card.ExpiresAtTurnsTaken, e.State.Players[1].TurnsTaken + 1, "until its owner's next turn begins");
        Eq(card.AbilityUses[BoastKey], 1, "armed: another player controls a Personality");
        var strength = e.State.Players[1].Provinces[3].Strength; var honor = Honor(e, 1);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == card.ExpiresAtTurnsTaken);
        Eq(e.State.Players[1].Provinces[3].Strength, strength + 3, "the province permanently has +3 strength");
        Check(Honor(e, 1) >= honor + 3, "and its owner gained 3 Honor");
        Eq(e.State.Cards[boast.Id].Zone, Zone.DynastyDiscard, "the Event is over");

        // Not armed: no other player controls a Personality.
        var e2 = Reveal(BoastfulProclamation, out var boast2, s => { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).ToArray()) s.Cards.Remove(c.Id); });
        Eq(e2.State.Cards[boast2.Id].AbilityUses[BoastKey], 0, "not armed");
        var strength2 = e2.State.Players[1].Provinces[3].Strength;
        RunUntil(e2, () => e2.State.ActivePlayerId == 1 && e2.State.Phase == Phase.Action && e2.State.Players[1].TurnsTaken == e2.State.Cards[boast2.Id].ExpiresAtTurnsTaken);
        Eq(e2.State.Players[1].Provinces[3].Strength, strength2, "no strength bonus");

        // Destroyed while it lasts (after battle resolution destroys the province): each Personality in the attacking army gets a +1F token and the Attacker may draw a card.
        MatchState Setup(out CardInstance evt, out CardInstance a1, out CardInstance a2)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            evt = Add(s, BoastfulProclamation, 1, Zone.EventInPlay); evt.AbilityUses[BoastKey] = 1; evt.AbilityUses["boastful-proclamation:province"] = 2;
            a1 = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 2);
            a2 = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 2);
            return s;
        }
        var s3 = Setup(out _, out var a1, out var a2);
        var e3 = MatchEngine.FromTestState(s3);
        var handBefore = e3.State.Players[0].Hand.Count;
        var force = e3.CardForceForTest(a1.Id);
        e3.ProvinceDestroyedForTest(0, 1, 2, [a1.Id, a2.Id]);
        Eq(e3.CardForceForTest(a1.Id), force + 1, "+1F token on each Personality of the attacking army");
        Eq(e3.CardForceForTest(a2.Id), force + 1, "on every one");
        Eq(e3.State.PendingChoice?.Type, "eventChoice", "the Attacker is asked whether to draw");
        Check(EventOptions(e3, 0).Any(l => l.Contains("draw a card")) && EventOptions(e3, 0).Any(l => l.Contains("do not draw")), "he may draw or not");
        AnswerEvent(e3, choice: "yes");
        Eq(e3.State.Players[0].Hand.Count, handBefore + 1, "he drew a card");
        var s4 = Setup(out _, out var b1, out _);
        var e4 = MatchEngine.FromTestState(s4);
        e4.ProvinceDestroyedForTest(0, 1, 2, [b1.Id]);
        AnswerEvent(e4, choice: "no");
        Eq(e4.State.Players[0].Hand.Count, handBefore, "declining draws nothing");
        // Another province of its owner's destroyed: nothing.
        var s5 = Setup(out _, out var c1, out _);
        var e5 = MatchEngine.FromTestState(s5);
        var force5 = e5.CardForceForTest(c1.Id);
        e5.ProvinceDestroyedForTest(0, 1, 0, [c1.Id]);
        Check(e5.State.PendingChoice is null && e5.CardForceForTest(c1.Id) == force5, "only its own province counts");
    }

    // ---- Delayed Arrival --------------------------------------------------------------------------------------------------------------------------------------

    private static void DelayedArrivalTests()
    {
        CardInstance? enemy = null, own = null;
        var e = Reveal(DelayedArrival, out var arrival, s => { enemy = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); own = Add(s, "training-lion-samurai", 1, Zone.Home); });
        Eq(e.State.PendingChoice?.Type, "eventChoice", "its owner chooses a Personality in play");
        var options = EventOptions(e, 1);
        Eq(options.Length, e.State.Cards.Values.Count(c => c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality), "every Personality in play is offered: " + string.Join("; ", options));
        AnswerEvent(e, target: enemy!.Id);
        Eq(e.State.Cards[arrival.Id].Zone, Zone.EventInPlay, "it stays in play until its owner's next turn begins");
        Eq(e.State.EventTargets[arrival.Id], enemy.Id, "naming him");
        // The named Personality (player 0's) may not be assigned to attack player 1; another Personality of player 0's may.
        var st = e.State;
        st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null;
        var other = Add(st, "training-lion-samurai", 0, Zone.Home);
        foreach (var id in new[] { enemy.Id, other.Id }) { st.Cards[id].Bowed = false; st.Cards[id].Zone = Zone.Home; st.Cards[id].ProvinceIndex = null; }
        var open = st.Players[1].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var ea = MatchEngine.FromTestState(st);
        var rejection = Rejection(ea, new GameCommand("attack", 0, ea.Version, Assignments: new() { [enemy.Id] = open[0] }));
        Check(rejection.Contains("Delayed Arrival"), "he may not be assigned to attack: " + rejection);
        Accept(ea, new GameCommand("attack", 0, ea.Version, Assignments: new() { [other.Id] = open[0] }));
        Eq(ea.State.Cards[other.Id].ProvinceIndex, open[0], "another Personality may");
        // It lasts until its owner's next turn begins, and then the naming is gone.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == e.State.Cards[arrival.Id].ExpiresAtTurnsTaken);
        Eq(e.State.Cards[arrival.Id].Zone, Zone.DynastyDiscard, "over as his next turn began");
        Check(!e.State.EventTargets.ContainsKey(arrival.Id), "and forgotten");
        // One of its owner's own Personalities: he may still attack the other player (an attack on "you" is what is barred).
        var s2 = Table(Phase.InfantryDeclaration, 1); s2.PriorityPlayerId = 1;
        var evt2 = Add(s2, DelayedArrival, 1, Zone.EventInPlay);
        var mine = Add(s2, "training-lion-samurai", 1, Zone.Home);
        s2.EventTargets[evt2.Id] = mine.Id;
        var e2 = MatchEngine.FromTestState(s2);
        var open2 = e2.State.Players[0].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        Accept(e2, new GameCommand("attack", 1, e2.Version, Assignments: new() { [mine.Id] = open2[0] }));
        Eq(e2.State.Cards[mine.Id].ProvinceIndex, open2[0], "his own Personality attacks the other player freely");
        // With nobody in play to name, the Event is simply over.
        var e3 = Reveal(DelayedArrival, out var arrival3, s => { foreach (var c in s.Cards.Values.Where(c => CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Zone is Zone.Home or Zone.Battlefield).ToArray()) s.Cards.Remove(c.Id); });
        Check(e3.State.PendingChoice is null && e3.State.Cards[arrival3.Id].Zone == Zone.DynastyDiscard, "no Personality in play: nothing to choose");
    }

    // ---- Glory of the Shogun ----------------------------------------------------------------------------------------------------------------------------------

    private static void GloryOfTheShogunTests()
    {
        var e = Reveal(GloryOfTheShogun, out var glory);
        Eq(e.State.Cards[glory.Id].Zone, Zone.EventInPlay, "in play until the game ends");
        Check(e.State.Cards[glory.Id].ExpiresAtTurnsTaken is null && e.State.Cards[glory.Id].ExpiresAtEventsPhaseTurnsTaken is null, "with no end");
        // Equipping is a Battle/Open action for all players: in the Action Phase the player who does not hold the turn may equip (Open), and in a battle a player with a unit there may (Battle).
        MatchState OpenTable(bool withGlory, out CardInstance item, out CardInstance wielder)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 1;
            if (withGlory) Add(s, GloryOfTheShogun, 1, Zone.EventInPlay);
            wielder = Add(s, "training-lion-samurai", 1, Zone.Home);
            item = Add(s, "training-item", 1, Zone.Hand);
            return s;
        }
        var open = MatchEngine.FromTestState(OpenTable(true, out var item1, out var wielder1));
        var equip = open.QueryActions(1).FirstOrDefault(o => o.Type == "equip" && o.CardId == item1.Id);
        Check(equip is { Enabled: true }, "Open: the other player may equip in the Action Phase: " + string.Join("; ", open.QueryActions(1).Select(o => $"{o.Type} {o.Enabled} {o.Reason}")));
        Accept(open, equip!.Command!);
        Eq(open.State.Cards[item1.Id].AttachedTo, wielder1.Id, "the Item was attached");
        var closed = MatchEngine.FromTestState(OpenTable(false, out var item0, out _));
        Check(!closed.QueryActions(1).Any(o => o.Type == "equip" && o.CardId == item0.Id && o.Enabled), "without the Event only the turn's player may equip");
        // Battle: a defender with a unit at the current battlefield equips there.
        MatchState BattleTable(bool withGlory, out CardInstance item, out CardInstance wielder)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            if (withGlory) Add(s, GloryOfTheShogun, 1, Zone.EventInPlay);
            wielder = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            item = Add(s, "training-item", 0, Zone.Hand);
            return s;
        }
        var battle = MatchEngine.FromTestState(BattleTable(true, out var item2, out var wielder2));
        var battleEquip = battle.QueryActions(0).FirstOrDefault(o => o.Type == "equip" && o.CardId == item2.Id);
        Check(battleEquip is { Enabled: true }, "Battle: a player with a unit at the battlefield may equip");
        Accept(battle, battleEquip!.Command!);
        Eq(battle.State.Cards[item2.Id].AttachedTo, wielder2.Id, "attached during the battle");
        var noGlory = MatchEngine.FromTestState(BattleTable(false, out var item3, out _));
        Check(!noGlory.QueryActions(0).Any(o => o.Type == "equip" && o.CardId == item3.Id && o.Enabled), "not without the Event");

        // The draw: after the first time each turn a player attaches a card with base Gold Cost 5 or more from his hand, he may draw a card.
        MatchState DrawTable(out CardInstance costly, out CardInstance cheap, out CardInstance costly2, out CardInstance wielder)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            Add(s, GloryOfTheShogun, 1, Zone.EventInPlay);
            wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
            costly = Add(s, "training-clan-discount-item", 0, Zone.Hand); // base Gold Cost 5
            costly2 = Add(s, "training-clan-discount-item", 0, Zone.Hand);
            cheap = Add(s, "training-item", 0, Zone.Hand);
            for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
            return s;
        }
        var cheapOnly = MatchEngine.FromTestState(DrawTable(out _, out var cheapItem, out _, out _));
        Accept(cheapOnly, cheapOnly.QueryActions(0).First(o => o.Type == "equip" && o.CardId == cheapItem.Id && o.Enabled).Command!);
        Check(cheapOnly.State.PendingChoice?.Type != "eventChoice", "a card with a base Gold Cost under 5 offers no draw");
        var drawer = MatchEngine.FromTestState(DrawTable(out var c1, out _, out var c2, out _));
        var hand = drawer.State.Players[0].Hand.Count;
        Accept(drawer, drawer.QueryActions(0).First(o => o.Type == "equip" && o.CardId == c1.Id && o.Enabled).Command!);
        Eq(drawer.State.PendingChoice?.Type, "eventChoice", "the first costly attach offers a draw");
        AnswerEvent(drawer, choice: "yes");
        Eq(drawer.State.Players[0].Hand.Count, hand - 1 + 1, "he drew a card");
        // Only the first time each turn: a second costly attach the same turn (when the engine lets him take it) offers nothing more.
        for (var guard = 0; drawer.State.PriorityPlayerId != 0 && drawer.State.PendingChoice is null && guard < 3; guard++) Pass(drawer);
        var second = drawer.QueryActions(0).FirstOrDefault(o => o.Type == "equip" && o.CardId == c2.Id && o.Enabled);
        if (second is not null)
        {
            Accept(drawer, second.Command!);
            Check(drawer.State.PendingChoice?.Type != "eventChoice", "only the first time each turn");
        }
        // He may decline the draw.
        var decliner = MatchEngine.FromTestState(DrawTable(out var d1, out _, out _, out _));
        var handD = decliner.State.Players[0].Hand.Count;
        Accept(decliner, decliner.QueryActions(0).First(o => o.Type == "equip" && o.CardId == d1.Id && o.Enabled).Command!);
        AnswerEvent(decliner, choice: "no");
        Eq(decliner.State.Players[0].Hand.Count, handD - 1, "declined: no card drawn");
    }
}
