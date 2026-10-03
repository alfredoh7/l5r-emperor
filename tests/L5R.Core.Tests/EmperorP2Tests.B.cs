using L5R.Core;
using static RegressionAssertions;

// Issue #503, pass group P2: the duel, switch, token, search, look, event and hand cards (the suite's list is in EmperorP2Tests.cs).
internal static partial class EmperorP2Tests
{
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));

    // Player 1's Mirumoto Ichizo (Chi 4, Duelist) challenges player 0's `duellist` at the battlefield; player 0 focuses `focus` and both strike. Returns at the first decision after the strike.
    private static (MatchEngine E, CardInstance Ichizo, CardInstance Duellist, CardInstance Focus) FocusDuel(string duellist, string focusDef, Action<MatchState, CardInstance>? setup = null)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, duellist, 0, Zone.Battlefield, province: 0);
        var focus = Add(s, focusDef, 0, Zone.Hand);
        setup?.Invoke(s, target);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        if (e.State.PendingDuel is { Stage: "OrderingEffects" }) Accept(e, new GameCommand("orderDuelFocusEffects", e.State.ActivePlayerId, e.Version, CardIds: [focus.Id]));
        return (e, ichizo, target, focus);
    }

    private static GameCommand[] FocusChoices(MatchEngine e) => Actions(e, 0, "p2FocusChoice");

    // ---- Killing Intent ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KillingIntentFocus()
    {
        // A 6F / 4C duellist: with Killing Intent his duel stat is his base Force, 6.
        var (e, _, duellist, _) = FocusDuel("training-lion-veteran", KillingIntent);
        Eq(e.State.PendingDuel?.Stage, MatchEngine.P2FocusChoiceStage, "the Focus Effect asks its owner before the totals");
        var yes = FocusChoices(e).Single(c => c.Choice == "yes");
        Accept(e, yes);
        Settle(e);
        Eq(e.State.LastDuel!.Result!.ChallengedStat, 6, "his base Force is his duel stat");
        // Declined: Chi.
        var (d, _, _, _) = FocusDuel("training-lion-veteran", KillingIntent);
        Accept(d, FocusChoices(d).Single(c => c.Choice == "decline"));
        Settle(d);
        Eq(d.State.LastDuel!.Result!.ChallengedStat, 4, "declined: his Chi");
        // Not at a battlefield: nothing to choose.
        var (h, _, _, _) = FocusDuel("training-lion-veteran", KillingIntent, (st, _) => { });
        Check(h.State.PendingDuel?.Stage == MatchEngine.P2FocusChoiceStage, "at the battlefield it asks");
        var home = Edit(h, st => { var id = st.PendingDuel!.ChallengedId; st.Cards[id].Zone = Zone.Home; st.Cards[id].ProvinceIndex = null; });
        Check(!FocusChoices(home).Any(c => c.Choice == "yes"), "at home the base-Force option is not offered");
    }

    private static void KillingIntentBattle()
    {
        var s = Table(Phase.Battle, 1);
        var performer = Add(s, "training-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var card = Add(s, KillingIntent, 1, Zone.Hand);
        var second = Add(s, KillingIntent, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "p2-killing-intent-straighten-home", card.Id, performerId: performer.Id));
        Settle(e);
        Check(!e.State.Cards[performer.Id].Bowed && e.State.Cards[performer.Id].Zone == Zone.Home, "straightened and moved home");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Use(f, 1, "p2-killing-intent-straighten", second.Id, performerId: performer.Id));
        Settle(f);
        Check(!f.State.Cards[performer.Id].Bowed && f.State.Cards[performer.Id].Zone == Zone.Battlefield, "straightened, stays");
        Check(Rejected(MatchEngine.FromTestState(s), Use(MatchEngine.FromTestState(s), 0, "p2-killing-intent-straighten", card.Id, performerId: performer.Id)), "not another player's performer");
    }

    // ---- A Champion in Court -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChampionInCourt()
    {
        var (e, ichizo, courtier, _) = FocusDuel("training-lion-courtier", Champion, (st, _) =>
        {
            var y = Add(st, "training-samurai", 0, Zone.Home); y.GrantedKeywords.Add(new("Yojimbo", "test-fixture", GrantDuration.Permanent)); ChiBoost(y, 20); y.Id.ToString();
        });
        var yojimbo = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.GrantedKeywords.Any(g => g.Keyword == "Yojimbo"));
        var choice = FocusChoices(e).Single(c => c.Choice == "yes");
        Eq(choice.TargetId, yojimbo.Id, "his Yojimbo is offered");
        Accept(e, choice);
        Settle(e);
        Check(Logged(e, "duelSubstituted"), "the Yojimbo takes the place");
        Check(InPlayZone(e, courtier.Id), "the Courtier is out of the duel and survives");
        Check(!InPlayZone(e, ichizo.Id), "the Yojimbo (Chi 22) won: Ichizo, the loser, is destroyed");
        // No Yojimbo: nothing to choose, the duel resolves.
        var (n, _, _, _) = FocusDuel("training-lion-courtier", Champion);
        Check(n.State.PendingDuel is null || n.State.PendingDuel.Stage != MatchEngine.P2FocusChoiceStage, "no Yojimbo: no question");
        Check(Logged(n, "focusEffectNothing"), "logged");
    }

    private static void ChampionRevenge()
    {
        MatchEngine Table2(bool courtier)
        {
            var s = Table(Phase.Battle, 1);
            var victim = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
            if (courtier) GiveKeyword(victim, "Courtier");
            var yojimbo = Add(s, "training-samurai", 0, Zone.Home); GiveKeyword(yojimbo, "Yojimbo");
            Add(s, Champion, 0, Zone.Hand);
            var kazuko = Add(s, "oracle-l5r-1734", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "kazuko-fear-melee", kazuko.Id, victim.Id));
            return e;
        }
        var e = Table2(true);
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 8; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for the Courtier's destruction");
        var revenge = Actions(e, 0, "useAbility").FirstOrDefault(c => c.AbilityId == "p2-champion-court-revenge");
        Check(revenge is not null, "A Champion in Court's Reaction is offered");
        var yojimbo = e.State.Cards[revenge!.PerformerId!];
        Accept(e, revenge);
        Eq(e.State.Cards[yojimbo.Id].Tokens.Count(t => t.Type == "Revenge" && t.Stat == "Force" && t.Amount == 1), 3, "three +1F Revenge tokens");
        var n = Table2(false);
        for (var guard = 0; n.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 8; guard++) Accept(n, new GameCommand("declineReaction", n.State.PriorityPlayerId, n.Version));
        Check(!Actions(n, 0, "useAbility").Any(c => c.AbilityId == "p2-champion-court-revenge"), "a destroyed non-Courtier, non-Shugenja: not offered");
    }

    // ---- Turning Away ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TurningAwayFocus()
    {
        var (e, ichizo, duellist, focus) = FocusDuel("training-ancestor", TurningAway);
        Accept(e, FocusChoices(e).Single(c => c.Choice == "yes"));
        Eq(e.State.Cards[duellist.Id].Zone, Zone.DynastyDiscard, "his Personality is discarded (not destroyed)");
        Check(e.State.PendingDuel is null, "the duel ended");
        Eq(e.State.LastDuel!.Result!.Kind, "Ended", "without resolution");
        Check(InPlayZone(e, ichizo.Id), "no loser was destroyed");
        Eq(e.State.Cards[focus.Id].Zone, Zone.FateDiscard, "the focused cards are spent");
        var (d, _, dDuellist, _) = FocusDuel("training-ancestor", TurningAway);
        Accept(d, FocusChoices(d).Single(c => c.Choice == "decline"));
        Settle(d);
        Check(d.State.Cards[dDuellist.Id].Zone is Zone.Dead or Zone.DynastyDiscard && d.State.LastDuel!.Result!.Kind == "Resolved", "declined: the duel resolves (the 2C Ancestor loses to Ichizo and is destroyed)");
    }

    private static void TurningAwayBattle()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var monk = Add(s, "training-monk", 0, Zone.Home, bowed: true);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home);
        Add(s, "training-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, TurningAway, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = e.ProvinceStrengthForTest(0, 0);
        var option = Options(e, 0, "p2-turning-away-lower", card.Id).FirstOrDefault(o => o.Enabled);
        Check(option is not null, "offered with no unit at the battlefield (Absent), a bowed Monk at home: " + string.Join("; ", Options(e, 0, "p2-turning-away-lower", card.Id).Select(o => o.Reason)));
        Accept(e, option!.Command! with { TargetId = ring.Id, PerformerId = monk.Id });
        Settle(e);
        Eq(e.ProvinceStrengthForTest(0, 0), Math.Max(0, before - 3), "-3 strength");
        Check(e.State.Cards[ring.Id].Bowed, "the Ring was bowed as the cost");
        var bowedRing = Edit(MatchEngine.FromTestState(s), st => st.Cards[ring.Id].Bowed = true);
        Check(!CanUse(bowedRing, 0, "p2-turning-away-raise", card.Id), "no unbowed Ring: not offered");
    }

    // ---- Spirit of the Shadows ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SpiritOfTheShadows()
    {
        var s = Table(Phase.Action, 1);
        var plain = Add(s, "training-samurai", 1, Zone.Home);
        var card = Add(s, SpiritShadows, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "p2-spirit-shadows", card.Id, performerId: plain.Id));
        Settle(e);
        Check(e.State.Cards[plain.Id].GrantedKeywords.Any(g => g.Keyword == "Ninja" && g.Duration == GrantDuration.Permanent), "a non-Ninja becomes a Ninja, permanently");
        Check(!e.AbilityIdsForTest(plain.Id).Contains("p2-shadows-switch"), "and gets no ability");
        var ninja = Edit(e, st => { var c = new CardInstance { Id = "fx-card2", DefinitionId = SpiritShadows, OwnerId = 1, Zone = Zone.Hand, FaceUp = false }; st.Cards.Add(c.Id, c); st.Players[1].Hand.Add(c.Id); st.PriorityPlayerId = 1; st.TurnNumber++; });
        Accept(ninja, Use(ninja, 1, "p2-spirit-shadows", "fx-card2", performerId: plain.Id));
        Settle(ninja);
        Check(ninja.AbilityIdsForTest(plain.Id).Contains("p2-shadows-switch"), "a Ninja gets the switch ability, permanently");
        // The switch: in a battle, two enemy Personalities at different locations swap.
        var battle = Edit(ninja, st =>
        {
            st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 1;
            st.Cards[plain.Id].Zone = Zone.Battlefield; st.Cards[plain.Id].ProvinceIndex = 0;
            st.Cards.Add("fx-a", new CardInstance { Id = "fx-a", DefinitionId = "training-ancestor", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
            st.Cards.Add("fx-b", new CardInstance { Id = "fx-b", DefinitionId = "training-ancestor", OwnerId = 0, Zone = Zone.Home, FaceUp = true });
            st.Cards.Add("fx-c", new CardInstance { Id = "fx-c", DefinitionId = "training-ancestor", OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
        });
        Check(Rejected(battle, Use(battle, 1, "p2-shadows-switch", plain.Id, null, null, Pick("first", "fx-a"), Pick("second", "fx-c"))), "two at the same location are refused");
        Check(Rejected(battle, Use(battle, 1, "p2-shadows-switch", plain.Id, null, null, Pick("first", "fx-a"), Pick("second", plain.Id))), "two of different players are refused");
        Accept(battle, Use(battle, 1, "p2-shadows-switch", plain.Id, null, null, Pick("first", "fx-a"), Pick("second", "fx-b")));
        Settle(battle);
        Eq(battle.State.Cards["fx-a"].Zone, Zone.Home, "the one at the battlefield went home");
        Check(battle.State.Cards["fx-b"].Zone == Zone.Battlefield && battle.State.Cards["fx-b"].ProvinceIndex == 0, "the one at home went to the battlefield");
    }

    // ---- Isawa Mina --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IsawaMina()
    {
        var s = Table(Phase.Battle, 1);
        var mina = Add(s, Mina, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemy.Id);
        enemy.Tokens.Add(new("tok-1", "Revenge", "test", "Force", 1));
        var homeUnit = Add(s, "training-ancestor", 0, Zone.Home);
        var homeFollower = Add(s, "training-follower", 0, Zone.Attached, parent: homeUnit.Id);
        var e = MatchEngine.FromTestState(s);
        var options = Options(e, 1, "p2-mina-destroy", mina.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Command!.Choice == MatchEngine.MinaCardChoice && o.TargetIds!.Contains(follower.Id)), "the attachment at the battlefield is offered: " + string.Join("; ", Options(e, 1, "p2-mina-destroy", mina.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason} {string.Join(",", o.TargetIds ?? [])}")));
        Check(!options.Any(o => o.TargetIds!.Contains(homeFollower.Id)), "an attachment at home is not");
        Check(options.Any(o => o.Command!.Choice == "token:tok-1" && o.TargetIds!.Contains(enemy.Id)), "the token is offered");
        var card = options.First(o => o.Command!.Choice == MatchEngine.MinaCardChoice).Command! with { TargetId = follower.Id };
        Accept(e, card);
        Settle(e);
        Check(!InPlayZone(e, follower.Id), "the Follower is destroyed");
        Eq(e.State.Cards[mina.Id].Tokens.Count(t => t.Type == "Fire" && t.Amount == 1), 2, "two +1F Fire tokens");
        var t = MatchEngine.FromTestState(s);
        Accept(t, Options(t, 1, "p2-mina-destroy", mina.Id).First(o => o.Enabled && o.Command!.Choice == "token:tok-1").Command! with { TargetId = enemy.Id });
        Settle(t);
        Check(t.State.Cards[enemy.Id].Tokens.Count == 0, "the token is destroyed");
        Eq(t.State.Cards[mina.Id].Tokens.Count(x => x.Type == "Fire"), 2, "two Fire tokens");
        Check(Rejected(MatchEngine.FromTestState(s), Use(MatchEngine.FromTestState(s), 1, "p2-mina-destroy", mina.Id, enemy.Id) with { Choice = MatchEngine.MinaCardChoice }), "a Personality is not an attachment");
    }

    // ---- Delving into History ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Delving, CardInstance Shugenja) DelvingTable(bool voidShugenja)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        if (voidShugenja) GiveKeyword(shugenja, "Void");
        var delving = Add(s, Delving, 0, Zone.Attached, parent: shugenja.Id);
        foreach (var def in new[] { "training-spell", "training-recon-spell", "training-unique-spell" })
        {
            var c = Add(s, def, 0, Zone.FateDeck); c.FaceUp = false; s.Players[0].FateDeck.Insert(0, c.Id);
        }
        GiveKeyword(shugenja, "Recon");
        return (MatchEngine.FromTestState(s), delving, shugenja);
    }

    private static void DelvingIntoHistory()
    {
        var (e, delving, shugenja) = DelvingTable(false);
        Accept(e, Use(e, 0, "p2-delving-search", delving.Id));
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja bowed");
        Eq(e.State.Cards[delving.Id].Zone, Zone.FateDiscard, "the Spell destroyed as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opens");
        Check(Rejected(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-spell")), "a Spell sharing no keyword with him is not eligible");
        Check(Rejected(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-unique-spell")), "a Unique Spell is not eligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-recon-spell"));
        Settle(e);
        Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == "training-recon-spell"), "the Recon Spell (sharing Recon) is in the hand");
        var (v, vd, _) = DelvingTable(true);
        Accept(v, Use(v, 0, "p2-delving-search", vd.Id));
        Accept(v, new GameCommand("selectSearchCard", 0, v.Version, Choice: "training-spell"));
        Settle(v);
        Check(v.State.Players[0].Hand.Any(id => v.State.Cards[id].DefinitionId == "training-spell"), "a Void performer: any non-Unique Spell");
    }

    // ---- The Tapestry Perceived --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TapestryPerceived()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var tapestry = Add(s, Tapestry, 0, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), 8, "not Void: 8");
        var voidE = Edit(e, st => st.Cards[shugenja.Id].GrantedKeywords.Add(new("Void", "test-fixture", GrantDuration.Permanent)));
        Eq(voidE.MaxHandSizeForTest(0), 9, "Void: 9");
        Eq(voidE.MaxHandSizeForTest(1), 8, "only his own hand");
        // Look at the opponent's deck, destroy the Spell, discard the card.
        var top = e.State.Players[1].FateDeck[0];
        Accept(e, Use(e, 0, "p2-tapestry-look", tapestry.Id));
        Check(e.State.Cards[tapestry.Id].Bowed, "bowed as the cost");
        Accept(e, new GameCommand("chooseLookDeck", 0, e.Version, Choice: "Fate", TargetPlayerId: 1));
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:discard"]));
        Settle(e);
        Eq(e.State.Cards[tapestry.Id].Zone, Zone.FateDiscard, "the Spell is destroyed");
        Eq(e.State.Cards[top].Zone, Zone.FateDiscard, "the card looked at is discarded");
        // Not destroying it: the card stays on top.
        var k = MatchEngine.FromTestState(s);
        var keepTop = k.State.Players[0].FateDeck[0];
        Accept(k, Use(k, 0, "p2-tapestry-look", tapestry.Id));
        Accept(k, new GameCommand("chooseLookDeck", 0, k.Version, Choice: "Fate", TargetPlayerId: 0));
        Accept(k, new GameCommand("arrangeTopCards", 0, k.Version, CardIds: ["0:top"]));
        Settle(k);
        Eq(k.State.Cards[tapestry.Id].Zone, Zone.Attached, "kept");
        Eq(k.State.Players[0].FateDeck[0], keepTop, "the card stays on top");
    }

    // ---- Events ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine RevealEvent(MatchState s, string def, int owner, out CardInstance card)
    {
        card = Add(s, def, owner, Zone.Province, province: 0);
        s.Players[owner].Provinces[0].CardId = card.Id;
        var e = MatchEngine.FromTestState(s);
        e.ResolveEventForTest(owner, card.Id);
        return e;
    }

    private static void GainingAdvantage()
    {
        var s = Table(Phase.Action, 0);
        foreach (var def in new[] { "oracle-l5r-8989", "training-engage-political-claim-strategy", Honest })
        {
            var c = Add(s, def, 0, Zone.FateDeck); c.FaceUp = false; s.Players[0].FateDeck.Insert(0, c.Id);
        }
        var keep = Add(s, "training-item", 0, Zone.Hand);
        var e = RevealEvent(s, Advantage, 0, out var ev);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the optional search opens");
        Check(Rejected(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Honest)), "a Strategy without a Political ability is not eligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-engage-political-claim-strategy"));
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "then discard a card");
        Accept(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: keep.Id));
        Eq(e.State.Cards[keep.Id].Zone, Zone.FateDiscard, "discarded");
        Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == "training-engage-political-claim-strategy"), "a Strategy whose ability is Political (not the card) qualifies");
        var s2 = Table(Phase.Action, 0);
        var c2 = Add(s2, "oracle-l5r-8989", 0, Zone.FateDeck); s2.Players[0].FateDeck.Insert(0, c2.Id);
        var p = RevealEvent(s2, Advantage, 0, out _);
        Accept(p, new GameCommand("passSearch", 0, p.Version));
        Check(!p.State.Players[0].Hand.Contains(c2.Id), "a player may decline (\"you may search\")");
    }

    private static void AgonyOfHerGaze()
    {
        var s = Table(Phase.Battle, 1);
        var e = RevealEvent(s, Agony, 1, out var agony);
        Eq(e.State.Cards[agony.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e.State.Cards[agony.Id].ExpiresAtEventsPhaseTurnsTaken, e.State.Players[1].TurnsTaken + 2, "until his Events Phase two turns from now");
        var yam = Edit(e, st =>
        {
            st.Cards.Add("fx-y", new CardInstance { Id = "fx-y", DefinitionId = Yamahatsu, OwnerId = 1, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
            st.Cards["fx-y"].GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent));
            st.Cards.Add("fx-h", new CardInstance { Id = "fx-h", DefinitionId = Yamahatsu, OwnerId = 1, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
            st.PriorityPlayerId = 1; st.PendingChoice = null;
        });
        Accept(yam, Use(yam, 1, "p2-yamahatsu-ranged", "fx-y"));
        Settle(yam);
        Eq(yam.State.Cards["fx-y"].Tokens.Count(t => t.Type == "Frenzy" && t.Stat == "Force" && t.Amount == 1), 1, "a Nonhuman who performed a Battle action: a +1F Frenzy token");
        if (yam.State.PriorityPlayerId != 1) Accept(yam, PassC(yam));
        Accept(yam, Use(yam, 1, "p2-yamahatsu-ranged", "fx-h"));
        Settle(yam);
        Check(yam.State.Cards["fx-h"].Tokens.Count == 0, "a Human performer gets nothing");
        // The other player's Nonhuman gets nothing from this player's Event.
        var other = Edit(e, st =>
        {
            st.Cards.Add("fx-o", new CardInstance { Id = "fx-o", DefinitionId = Yamahatsu, OwnerId = 0, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true });
            st.Cards["fx-o"].GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent));
            st.PriorityPlayerId = 0; st.PendingChoice = null;
        });
        Accept(other, Use(other, 0, "p2-yamahatsu-ranged", "fx-o"));
        Settle(other);
        Check(other.State.Cards["fx-o"].Tokens.Count == 0, "not the other player's Nonhuman");
    }

    private static void GrowingHostilityReveal()
    {
        var s = Table(Phase.Action, 1);
        var e = RevealEvent(s, Hostility, 1, out var card);
        Check(e.State.P2.HoldingGoldCostMinimum, "revealed: the minimum is in force");
        Eq(e.State.Cards[card.Id].Zone, Zone.DynastyDiscard, "the Event is discarded");
    }

    // ---- Pillowbook, The Scorpion Wall is Finished --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PillowbookHandSizes()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, "training-samurai", 0, Zone.Home);
        Add(s, Pillowbook, 0, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), 9, "his own: +1");
        Eq(e.MaxHandSizeForTest(1), 7, "the other player's: -1");
        var two = Edit(e, st => st.Cards.Add("fx-pb", new CardInstance { Id = "fx-pb", DefinitionId = Pillowbook, OwnerId = 0, Zone = Zone.Attached, AttachedTo = host.Id, FaceUp = true }));
        Eq(two.MaxHandSizeForTest(1), 7, "to a minimum of 7");
        Eq(two.MaxHandSizeForTest(0), 10, "each raises his own");
    }

    // Times of Strife (player 1's) makes player 0 discard as he announces a Battle action with no units at the battlefield; Pillowbook stops it.
    private static MatchEngine StrifeTable(bool pillowbook)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var strife = Add(s, "oracle-l5r-8494", 1, Zone.EventInPlay);
        strife.ExpiresAtTurnsTaken = 99;
        Add(s, "training-samurai", 1, Zone.Battlefield, province: 0);
        var host = Add(s, "training-samurai", 0, Zone.Home);
        if (pillowbook) Add(s, Pillowbook, 0, Zone.Attached, parent: host.Id);
        var ring = Add(s, "oracle-l5r-6334", 0, Zone.Hand);
        Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Options(e, 0, "ring-of-earth-raise", ring.Id).First(o => o.Enabled).Command! with { TargetPlayerId = 0, Province = 0 });
        Settle(e);
        return e;
    }

    private static void PillowbookImmunity()
    {
        var plain = StrifeTable(false);
        Check(plain.State.Events.Any(x => x.Type == "handDiscard") || plain.State.PendingChoice?.Type == "forcedDiscard", "without Pillowbook: Times of Strife makes him discard");
        var guarded = StrifeTable(true);
        Check(guarded.State.PendingChoice?.Type != "forcedDiscard" && !guarded.State.Events.Any(x => x.Type == "handDiscard"), "with Pillowbook: no discard");
        Check(Logged(guarded, "handDiscardPrevented"), "logged");
    }

    // Player 1 (active) holds two draw Strategies; player 0 holds Indecision; `wallOwner` controls The Scorpion Wall is Finished (or nobody).
    private static (MatchEngine E, CardInstance First, CardInstance Second, CardInstance Third, CardInstance Indecision) WallTable(int? wallOwner)
    {
        var s = Table(Phase.Action, 1);
        var first = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var second = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var third = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        for (var i = 0; i < 3; i++) Add(s, "training-item", 1, Zone.Hand);
        var indecision = Add(s, "oracle-l5r-3736", 0, Zone.Hand);
        if (wallOwner is int owner) Add(s, Wall, owner, Zone.EventInPlay);
        return (MatchEngine.FromTestState(s), first, second, third, indecision);
    }

    private static void ScorpionWall()
    {
        // The cap: after two actions that put cards into his hand this phase, a third draws nothing.
        var (e, first, second, third, indecision) = WallTable(0);
        Accept(e, Use(e, 1, "training-limited-draw", first.Id)); Settle(e); Accept(e, PassC(e));
        Accept(e, Use(e, 1, "training-limited-draw", second.Id));
        Eq(e.State.MechD.HandActionsThisPhase[1], 2, "two actions put cards into his hand");
        Settle(e);
        if (e.State.PriorityPlayerId != 1) Accept(e, PassC(e));
        var hand = e.State.Players[1].Hand.Count;
        Accept(e, Use(e, 1, "training-limited-draw", third.Id));
        Eq(e.State.Players[1].Hand.Count, hand - 1, "no card was put into his hand (the played Strategy left it)");
        Check(Logged(e, "cardIntoHandPrevented"), "logged");
        // No Wall: the third draw works.
        var (n, f1, f2, f3, _) = WallTable(null);
        Accept(n, Use(n, 1, "training-limited-draw", f1.Id)); Settle(n); Accept(n, PassC(n));
        Accept(n, Use(n, 1, "training-limited-draw", f2.Id)); Settle(n);
        if (n.State.PriorityPlayerId != 1) Accept(n, PassC(n));
        var nHand = n.State.Players[1].Hand.Count;
        Accept(n, Use(n, 1, "training-limited-draw", f3.Id));
        Eq(n.State.Players[1].Hand.Count, nHand, "without the Wall the card is drawn");
        // Immunity: player 1 owns the Wall, so player 0's Indecision does not discard his cards.
        var (w, w1, w2, _, wIndecision) = WallTable(1);
        Accept(w, Use(w, 1, "training-limited-draw", w1.Id)); Settle(w); Accept(w, PassC(w));
        Accept(w, Use(w, 1, "training-limited-draw", w2.Id));
        Eq(w.State.PendingChoice?.Type, "afterActionResolvedReaction", "Indecision's window");
        var wHand = w.State.Players[1].Hand.ToArray();
        Accept(w, Use(w, 0, "indecision-discard", wIndecision.Id));
        for (var guard = 0; w.State.PendingDeciderChoice is not null && guard < 4; guard++)
        {
            var pick = Actions(w, 1, "chooseDecider").FirstOrDefault(c => c.TargetId is not null) ?? Actions(w, 1, "chooseDecider").First();
            Accept(w, pick);
        }
        Eq(w.State.Players[1].Hand.Count, wHand.Length, "another player's action did not discard from his hand");
        Check(Logged(w, "handDiscardPrevented"), "logged");
    }
}

internal static partial class EmperorP2Tests
{
    private static (MatchEngine E, CardInstance Keirei, CardInstance Here, CardInstance There, CardInstance Home) KeireiTable()
    {
        var s = Table(Phase.Battle, 1);
        s.UnresolvedBattlefields = [0, 1];
        var keirei = Add(s, Keirei, 1, Zone.Battlefield, province: 0);
        var here = Add(s, "training-samurai", 0, Zone.Battlefield, province: 0);   // 2F/2C
        var there = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 1); // 6F/4C
        var home = Add(s, "training-ancestor", 0, Zone.Home);
        return (MatchEngine.FromTestState(s), keirei, here, there, home);
    }

    private static void ShosuroKeirei()
    {
        var (e, keirei, here, there, home) = KeireiTable();
        var option = Options(e, 1, "p2-keirei-move", keirei.Id).First(o => o.Enabled);
        Eq(string.Join(",", option.Provinces ?? []), "1", "the other battlefield is offered, not the current one");
        var fought = Edit(e, st => { st.P2.FoughtPhaseKey = $"{st.TurnNumber}|{st.ActivePlayerId}"; st.P2.FoughtBattlefields = [2]; });
        Eq(string.Join(",", Options(fought, 1, "p2-keirei-move", keirei.Id).First(o => o.Enabled).Provinces ?? []), "1,2", "a battlefield already fought this Attack Phase is offered too");
        Accept(e, option.Command! with { Province = 1 });
        Settle(e);
        Check(e.State.Cards[keirei.Id].Zone == Zone.Battlefield && e.State.Cards[keirei.Id].ProvinceIndex == 1, "Keirei moved");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "he may target a Personality");
        var picks = Actions(e, 1, "chooseDecider").Where(c => c.TargetId is not null).Select(c => c.TargetId!).ToArray();
        Check(picks.Contains(here.Id) && picks.Contains(there.Id) && picks.Contains(keirei.Id), "Personalities at his previous and new locations are offered");
        Check(!picks.Contains(home.Id), "not one at home");
        Accept(e, Actions(e, 1, "chooseDecider").First(c => c.TargetId == there.Id));
        Settle(e);
        var tokens = e.State.Cards[there.Id].Tokens.Where(t => t.Type == "Poison").ToArray();
        Eq(tokens.Length, 2, "two Poison tokens");
        Check(tokens.All(t => t.Stat == "Force" && t.Amount == -1 && t.Stat2 == "Chi" && t.Amount2 == -1), "each -1F/-1C");
        Eq(e.CardForceForTest(there.Id), 4, "-2F");
        Eq(e.PersonalityChiForTest(there.Id), 2, "-2C");
        // A 2C Personality at the previous location dies of it.
        var (d, dk, dHere, _, _) = KeireiTable();
        Accept(d, Options(d, 1, "p2-keirei-move", dk.Id).First(o => o.Enabled).Command! with { Province = 1 });
        Accept(d, Actions(d, 1, "chooseDecider").First(c => c.TargetId == dHere.Id));
        Settle(d);
        Check(d.State.Cards[dHere.Id].Zone == Zone.Dead, "0 Chi: destroyed");
        // "May": zero is a legal answer.
        var (z, zk, _, zThere, _) = KeireiTable();
        Accept(z, Options(z, 1, "p2-keirei-move", zk.Id).First(o => o.Enabled).Command! with { Province = 1 });
        Accept(z, Actions(z, 1, "chooseDecider").First(c => c.Choice == "done"));
        Check(z.State.Cards[zThere.Id].Tokens.Count == 0 && z.State.PendingDeciderChoice is null, "no target: no tokens");
        // No other battlefield: not offered.
        var only = Edit(KeireiTable().E, st => st.UnresolvedBattlefields = [0]);
        Check(!CanUse(only, 1, "p2-keirei-move"), "no different battlefield: not offered");
    }

    private static void PoisonTokenStats()
    {
        var (e, keirei, _, there, _) = KeireiTable();
        Accept(e, Options(e, 1, "p2-keirei-move", keirei.Id).First(o => o.Enabled).Command! with { Province = 1 });
        Accept(e, Actions(e, 1, "chooseDecider").First(c => c.TargetId == there.Id));
        Settle(e);
        // Gift Armor purges Chi penalties: attach one and the tokens keep their Force penalty only.
        var gift = CardCatalog.Cards.Values.First(c => c.Effect == "gift-armor");
        var armored = Edit(e, st => st.Cards.Add("fx-gift", new CardInstance { Id = "fx-gift", DefinitionId = gift.Id, OwnerId = 0, Zone = Zone.Attached, AttachedTo = there.Id, ProvinceIndex = 1, FaceUp = true }));
        armored.PurgeChiPenaltiesForTest(there.Id);
        Check(armored.State.Cards[there.Id].Tokens.Where(t => t.Type == "Poison").All(t => t.Stat == "Force" && t.Stat2 is null), "only the Force penalty stays");
        Eq(armored.PersonalityChiForTest(there.Id), 4, "Chi back to 4");
        var cleansed = Edit(e, st => { });
        cleansed.CleanseForcePenaltiesForTest(there.Id);
        Check(cleansed.State.Cards[there.Id].Tokens.Where(t => t.Type == "Poison").All(t => t.Stat == "Chi" && t.Amount == -1 && t.Stat2 is null), "only the Chi penalty stays");
        Eq(cleansed.CardForceForTest(there.Id), 6, "Force back to 6");
    }
}
