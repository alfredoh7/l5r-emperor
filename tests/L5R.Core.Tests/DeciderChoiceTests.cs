using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #419 (stage 5 of Mendon's rulings of 2026-09-21), first part: "choose a player who chooses" -- an action that stops when its effect is a choice another player makes, offers
// that player exactly the legal picks, and finishes when he has chosen. Know Their Minds (a chosen player turns a face-up province Personality face-down), Scandalous Rumors (the
// Attacker moves any of his Personalities home, and pays for the ones that stay) and Dragon's Breath Castle (the Attacker bows one of his Personalities; its Dynasty Phase honor trait).
internal static class DeciderChoiceTests
{
    private const string KnowTheirMinds = "oracle-l5r-4487", ScandalousRumors = "oracle-l5r-6488", Dbc = "oracle-l5r-2219";

    public static void Run(TestRunner runner) => runner.Run("decider-choice", [
        ("Published shape: Know Their Minds, Scandalous Rumors and Dragon's Breath Castle are the printed cards with their abilities, Effect and art", PublishedShapes),
        ("Know Their Minds: the chosen player picks among his face-up province Personalities (not face-down ones or other cards), the pick is turned face-down, it is revealed again in its controller's next Events phase, and a player may choose himself", KnowTheirMindsFlow),
        ("Know Their Minds asks nobody when there is nothing to choose, and announces the one forced choice", KnowTheirMindsForced),
        ("Scandalous Rumors: the Attacker (either player) chooses any of his Personalities at the battlefield to move home; each that stays costs him 1 Honor and is dishonored; a Personality whose movement failed counts as not moved", ScandalousRumorsFlow),
        ("Dragon's Breath Castle's Reaction: after engaging at its controller's province the Attacker bows one of his unbowed Personalities there -- even with the Castle bowed, and never for a battle at the Attacker's own province", DragonsBreathReaction),
        ("Dragon's Breath Castle's trait: after another player's Dynasty Phase begins, 2 Honor if he controls Personalities and assigned none to an attack this turn", DragonsBreathTrait),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            Accept(e, pick.Command!);
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var k = CardCatalog.Get(KnowTheirMinds); var r = CardCatalog.Get(ScandalousRumors); var d = CardCatalog.Get(Dbc);
        Check(k.Name == "Know Their Minds" && k.Type == CardType.Strategy && k.Focus == 2 && k.Abilities!.SequenceEqual(new[] { "know-their-minds-choose" }), "Know Their Minds");
        Check(r.Name == "Scandalous Rumors" && r.Focus == 3 && r.Keywords!.Contains("Political") && r.Abilities!.SequenceEqual(new[] { "scandalous-rumors-move" }), "Scandalous Rumors");
        Check(d.Name == "Dragon's Breath Castle" && d.Type == CardType.Stronghold && d.Clan == "Dragon" && d.GoldProduction == 4 && d.StartingHonor == 5 && d.ProvinceStrength == 7
            && d.Effect == "dbc-honor" && d.Abilities!.SequenceEqual(new[] { "dbc-attacker-bow" }), "Dragon's Breath Castle");
        foreach (var id in new[] { KnowTheirMinds, ScandalousRumors, Dbc })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Player 0 holds Know Their Minds with an unbowed Samurai; player 1's provinces: 0 and 1 face-up Personalities, 2 a face-down Personality, 3 a face-up Holding.
    private static (MatchEngine E, CardInstance Card, CardInstance Samurai, CardInstance A, CardInstance B, CardInstance Hidden, CardInstance Holding) KtmTable()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        CardInstance Province(int index, string def, bool faceUp) { var c = s.Cards[s.Players[1].Provinces[index].CardId!]; c.DefinitionId = def; c.FaceUp = faceUp; return c; }
        var a = Province(0, "training-crane-samurai", true); var b = Province(1, "training-lion-samurai", true); var hidden = Province(2, "training-crane-veteran", false); var holding = Province(3, "training-farm", true);
        var card = Add(s, KnowTheirMinds, 0, Zone.Hand); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(samurai, "Samurai");
        return (MatchEngine.FromTestState(s), card, samurai, a, b, hidden, holding);
    }

    private static GameCommand Ktm(MatchEngine e, CardInstance card, CardInstance samurai, int chosenPlayer)
    {
        var option = Enabled(e, 0, "know-their-minds-choose", card.Id, samurai.Id);
        return option.Command! with { TargetPlayerId = chosenPlayer };
    }

    private static void KnowTheirMindsFlow()
    {
        var (e, card, samurai, a, b, hidden, holding) = KtmTable();
        var turn = e.State.TurnNumber;
        Accept(e, Ktm(e, card, samurai, 1));
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the chosen player is asked");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1 decides");
        Check(e.State.Cards[samurai.Id].Bowed && e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the Samurai bowed as the cost and the Strategy is spent");
        var picks = e.QueryActions(1).Where(x => x.Type == "chooseDecider").Select(x => x.CardId).ToArray();
        Check(picks.Length == 2 && picks.Contains(a.Id) && picks.Contains(b.Id), "exactly his face-up Personalities in provinces: " + string.Join(",", picks));
        Check(!e.QueryActions(0).Any(x => x.Type == "chooseDecider"), "the acting player has nothing to choose");
        Reject(e, new GameCommand("chooseDecider", 0, e.Version, TargetId: a.Id));
        Reject(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: hidden.Id));
        Reject(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: holding.Id));
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: b.Id));
        Check(e.State.PendingChoice is null, "a choice of one card ends when it is made");
        Check(!e.State.Cards[b.Id].FaceUp && e.State.Cards[a.Id].FaceUp, "the chosen one is face-down, the other still face-up");
        Check(e.State.Events.Any(x => x.Type == "provinceTurnedFaceDown"), "logged");
        // Revealed again in its controller's next Events phase, as though never revealed.
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[b.Id].FaceUp, "revealed again when its controller's Events phase came");
        Check(e.State.Events.Any(x => x.Type == "provinceRevealed" && x.CardId == b.Id), "with a reveal event");

        // Choosing oneself: the acting player decides among his own provinces.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        CardInstance Own(int index, string def) { var c = s.Cards[s.Players[0].Provinces[index].CardId!]; c.DefinitionId = def; c.FaceUp = true; return c; }
        var mine = Own(0, "training-crane-samurai"); var mine2 = Own(1, "training-lion-samurai");
        var c2 = Add(s, KnowTheirMinds, 0, Zone.Hand); var sam2 = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(sam2, "Samurai");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Ktm(f, c2, sam2, 0));
        Eq(f.State.PendingChoice?.PlayerId, 0, "a player may choose himself");
        Accept(f, new GameCommand("chooseDecider", 0, f.Version, TargetId: mine2.Id));
        Check(!f.State.Cards[mine2.Id].FaceUp, "his own province Personality turned face-down");

        // Only a Samurai may perform it, and only while unbowed.
        var (g, card3, samurai3, _, _, _, _) = KtmTable();
        var notSamurai = Add(g.State, "training-crane-veteran", 0, Zone.Home);
        Check(!Options(g, 0, "know-their-minds-choose", card3.Id).Any(o => o.Enabled && o.Label.Contains($"({notSamurai.Id})")), "a Personality who is not a Samurai cannot perform it");
    }

    private static void KnowTheirMindsForced()
    {
        // No face-up Personality: nothing to choose, nothing asked, the action finishes.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var p in s.Players[1].Provinces) if (p.CardId is { } id) { s.Cards[id].DefinitionId = "training-farm"; s.Cards[id].FaceUp = true; }
        var card = Add(s, KnowTheirMinds, 0, Zone.Hand); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(samurai, "Samurai");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Ktm(e, card, samurai, 1));
        Check(e.State.PendingChoice is null && e.State.PendingDeciderChoice is null, "no decision was opened");
        Check(e.State.Events.Any(x => x.Type == "deciderAutoResolved" && x.Message.Contains("nothing to choose")), "and the log says why");
        Check(e.State.Cards[samurai.Id].Bowed && e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the action was taken and its cost paid");
        // Exactly one candidate: forced, announced, applied.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        for (var i = 0; i < t.Players[1].Provinces.Count; i++)
        {
            var c = t.Cards[t.Players[1].Provinces[i].CardId!]; c.FaceUp = true; c.DefinitionId = i == 0 ? "training-crane-samurai" : "training-farm";
        }
        var card2 = Add(t, KnowTheirMinds, 0, Zone.Hand); var sam2 = Add(t, "training-crane-samurai", 0, Zone.Home); GiveKeyword(sam2, "Samurai");
        var f = MatchEngine.FromTestState(t);
        Accept(f, Ktm(f, card2, sam2, 1));
        Check(f.State.PendingChoice is null, "one choice is not asked");
        Check(!f.State.Cards[t.Players[1].Provinces[0].CardId!].FaceUp && f.State.Events.Any(x => x.Type == "deciderAutoResolved" && x.Message.Contains("one choice")), "it was applied and announced");
    }

    private static void ScandalousRumorsFlow()
    {
        // Player 1 attacks (the Attacker, and the decider); player 0 defends and plays the Rumors with a Courtier at home.
        (MatchEngine E, CardInstance Card, CardInstance Courtier, CardInstance A, CardInstance B, CardInstance C) Setup(int actor = 0)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = actor;
            var a = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); var b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var c = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0);
            var courtier = Add(s, "training-shugenja", actor, Zone.Home); GiveKeyword(courtier, "Courtier");
            var card = Add(s, ScandalousRumors, actor, Zone.Hand);
            return (MatchEngine.FromTestState(s), card, courtier, a, b, c);
        }
        var (e, card, courtier, a, b, c) = Setup();
        var honor = Honor(e, 1);
        Accept(e, Enabled(e, 0, "scandalous-rumors-move", card.Id, courtier.Id).Command!);
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the Attacker is asked");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1, the Attacker, decides");
        var offered = e.QueryActions(1).Where(x => x.Type == "chooseDecider").ToArray();
        Check(offered.Count(x => x.CardId is not null && (x.CardId == a.Id || x.CardId == b.Id || x.CardId == c.Id)) == 3, "each of his Personalities at the battlefield can be moved");
        Check(offered.Any(x => x.Command!.Choice == "done") && offered.Single(x => x.Command!.Choice == "done").Label.Contains("none"), "and none may be chosen");
        // Move a and b home; c stays: he loses 1 Honor for c and c is dishonored.
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: a.Id));
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "more may be chosen");
        Reject(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: a.Id));
        Check(!e.QueryActions(1).Any(x => x.Type == "chooseDecider" && x.CardId == a.Id), "one already chosen is not offered again");
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, TargetId: b.Id));
        Accept(e, e.QueryActions(1).Single(x => x.Type == "chooseDecider" && x.Command!.Choice == "done").Command!);
        Check(e.State.PendingChoice is null, "done");
        Check(e.State.Cards[a.Id].Zone == Zone.Home && e.State.Cards[b.Id].Zone == Zone.Home && e.State.Cards[c.Id].Zone == Zone.Battlefield, "the two chosen went home, the third stayed");
        Eq(Honor(e, 1), honor - 1, "1 Honor for the one that stayed");
        Check(e.State.Cards[c.Id].Dishonorable && !e.State.Cards[a.Id].Dishonorable, "and he is dishonored, the others not");

        // None chosen: all three stay -- 3 Honor, three dishonored.
        var (f, cardF, courtierF, aF, bF, cF) = Setup();
        var honorF = Honor(f, 1);
        Accept(f, Enabled(f, 0, "scandalous-rumors-move", cardF.Id, courtierF.Id).Command!);
        Accept(f, f.QueryActions(1).Single(x => x.Type == "chooseDecider" && x.Command!.Choice == "done").Command!);
        Eq(Honor(f, 1), honorF - 3, "3 Honor for three that stayed");
        Check(new[] { aF, bF, cF }.All(x => f.State.Cards[x.Id].Dishonorable), "all dishonored");
        // All chosen: nobody stays and nobody pays.
        var (g, cardG, courtierG, aG, bG, cG) = Setup();
        var honorG = Honor(g, 1);
        Accept(g, Enabled(g, 0, "scandalous-rumors-move", cardG.Id, courtierG.Id).Command!);
        foreach (var x in new[] { aG, bG, cG }) Accept(g, new GameCommand("chooseDecider", 1, g.Version, TargetId: x.Id));
        Check(g.State.PendingChoice is null, "choosing every one of them completes it");
        Eq(Honor(g, 1), honorG, "no Honor lost when all went home");
        Check(new[] { aG, bG, cG }.All(x => g.State.Cards[x.Id].Zone == Zone.Home && !g.State.Cards[x.Id].Dishonorable), "all home, none dishonored");
        // A Personality whose movement fails did not move: it counts as staying.
        var (h, cardH, courtierH, aH, bH, cH) = Setup();
        aH.MovementLocks.Add(new("test", 0, MovementKinds.Home));
        var stateH = h.State; stateH.Cards[aH.Id].MovementLocks.Clear(); stateH.Cards[aH.Id].MovementLocks.Add(new("test", stateH.TurnNumber, MovementKinds.Home));
        var h2 = MatchEngine.FromTestState(stateH);
        var honorH = Honor(h2, 1);
        Accept(h2, Enabled(h2, 0, "scandalous-rumors-move", cardH.Id, courtierH.Id).Command!);
        Accept(h2, new GameCommand("chooseDecider", 1, h2.Version, TargetId: aH.Id));
        Accept(h2, h2.QueryActions(1).Single(x => x.Type == "chooseDecider" && x.Command!.Choice == "done").Command!);
        Check(h2.State.Cards[aH.Id].Zone == Zone.Battlefield && h2.State.Cards[aH.Id].Dishonorable, "his movement was negated: he did not move, so he is dishonored");
        Eq(Honor(h2, 1), honorH - 3, "and counts: three Personalities did not move");

        // The Attacker may be the one who plays it: he decides for himself.
        var (i, cardI, courtierI, aI, bI, cI) = Setup(actor: 1);
        Accept(i, Enabled(i, 1, "scandalous-rumors-move", cardI.Id, courtierI.Id).Command!);
        Eq(i.State.PendingChoice?.PlayerId, 1, "the Attacker who took the action still makes the choice");
    }

    private static void DragonsBreathReaction()
    {
        // Player 0 attacks province 0 of player 1, whose Stronghold is Dragon's Breath Castle.
        (MatchEngine E, CardInstance Castle, CardInstance A, CardInstance B) Engage(bool castleBowed = false, bool castleOwnedByAttacker = false)
        {
            var s = Table(Phase.BattleSelection, 0); s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            var castleOwner = castleOwnedByAttacker ? 0 : 1;
            var castle = s.Cards[s.Players[castleOwner].StrongholdId]; castle.DefinitionId = Dbc; castle.Bowed = castleBowed;
            var a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var b = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-water-personality", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-veteran", 0, Zone.Battlefield, bowed: true, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return (e, castle, a, b);
        }
        var (e, castle, a, b) = Engage(castleBowed: true);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        // The Attacker has the first opportunity in the window and passes it to the Defender.
        if (e.State.PriorityPlayerId == 0) Accept(e, e.QueryActions(0).Single(x => x.Type == "declineReaction").Command!);
        var reaction = e.QueryActions(1).Single(x => x.Type == "useAbility" && x.Enabled && x.Command?.AbilityId == "dbc-attacker-bow" && x.CardId == castle.Id).Command!;
        Accept(e, reaction);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the Attacker is asked (the Castle was bowed: Even if this card is bowed)");
        Eq(e.State.PendingChoice?.PlayerId, 0, "the Attacker decides");
        var picks = e.QueryActions(0).Where(x => x.Type == "chooseDecider").Select(x => x.CardId).ToArray();
        Check(picks.Length == 2 && picks.Contains(a.Id) && picks.Contains(b.Id), "one of his unbowed Personalities at the battlefield (the bowed one is not offered): " + string.Join(",", picks));
        Accept(e, new GameCommand("chooseDecider", 0, e.Version, TargetId: b.Id));
        Check(e.State.Cards[b.Id].Bowed && !e.State.Cards[a.Id].Bowed, "the chosen Personality was bowed");
        Eq(e.State.PendingChoice?.Type, "engageReaction", "and the Engage window it was played in is open again");

        // Not for a battle at the Attacker's own province: the Attacker's Castle is not one of the Defender's provinces.
        var (f, ownCastle, _, _) = Engage(castleOwnedByAttacker: true);
        Check(ownCastle.OwnerId == 0);
        Check(!f.QueryActions(0).Any(x => x.Type == "useAbility" && x.Enabled && x.Command?.AbilityId == "dbc-attacker-bow"), "the Attacker's own Castle does not react to his own attack");
        Check(!f.QueryActions(1).Any(x => x.Command?.AbilityId == "dbc-attacker-bow"), "and it is not the Defender's");
        // All his Personalities there already bowed: nothing to choose.
        var s2 = Table(Phase.BattleSelection, 0); s2.PriorityPlayerId = 0; s2.CurrentBattleProvince = null; s2.UnresolvedBattlefields = [0];
        s2.Cards[s2.Players[1].StrongholdId].DefinitionId = Dbc;
        var bowedOne = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); Add(s2, "training-water-personality", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(s2);
        Accept(g, new GameCommand("selectBattle", 0, g.Version, Province: 0));
        if (g.State.PriorityPlayerId == 0) Accept(g, g.QueryActions(0).Single(x => x.Type == "declineReaction").Command!);
        Accept(g, g.QueryActions(1).Single(x => x.Type == "useAbility" && x.Enabled && x.Command?.AbilityId == "dbc-attacker-bow").Command!);
        Check(g.State.PendingChoice is not { Type: "deciderChoice" }, "with every Personality already bowed nothing is asked");
    }

    private static void DragonsBreathTrait()
    {
        // Player 1 has the Castle; player 0's turn: at its Dynasty Phase player 1 gains 2 Honor if player 0 controls Personalities and assigned none to an attack.
        MatchEngine Turn(bool personalities, bool assigned)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
            s.Cards[s.Players[1].StrongholdId].DefinitionId = Dbc;
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).ToArray()) s.Cards.Remove(c.Id);
            if (personalities) { var p = Add(s, "training-crane-samurai", 0, Zone.Home); if (assigned) p.AssignedToAttackTurn = s.TurnNumber; }
            return MatchEngine.FromTestState(s);
        }
        int Gain(MatchEngine e)
        {
            var before = Honor(e, 1);
            Passing(e, x => x.State.Phase == Phase.Dynasty || x.State.TurnNumber != 1 + 0 && x.State.Phase != Phase.Action);
            return Honor(e, 1) - before;
        }
        Eq(Gain(Turn(true, false)), 2, "another player's Dynasty Phase began: he controls Personalities and assigned none to an attack: 2 Honor");
        Eq(Gain(Turn(true, true)), 0, "he assigned one to an attack: nothing");
        Eq(Gain(Turn(false, false)), 0, "he controls no Personalities: nothing");
        // Its own controller's Dynasty Phase does not count.
        var own = Turn(true, false);
        var st = own.State; st.ActivePlayerId = 1; st.PriorityPlayerId = 1;
        Add(st, "training-lion-samurai", 1, Zone.Home);
        var mine = MatchEngine.FromTestState(st);
        var h1 = Honor(mine, 1);
        Passing(mine, x => x.State.Phase == Phase.Dynasty);
        Eq(Honor(mine, 1), h1, "another player's Dynasty Phase: not the Castle's own controller's");
    }
}
