using L5R.Core;
using static RegressionAssertions;

// Promotional-Emperor Strategies and Spells, part two (issue #653): the Reactions, the recruits from a pile and the small Battle/Open actions. Same fixtures and helpers as PromoEmperorFateTests.cs.
internal static partial class PromoEmperorFateTests
{
    private const string Fallen = "oracle-l5r-10598", ItWasACat = "oracle-l5r-10415", Sinkhole = "oracle-l5r-9986", ClosingTheGap = "oracle-l5r-9989", Interrogation = "oracle-l5r-10239",
        WellProtected = "oracle-l5r-9288", ImminentVictory = "oracle-l5r-10601", Resurrection = "oracle-l5r-6281", Renewal = "oracle-l5r-6261", GovernTheLand = "oracle-l5r-2877",
        ReFortification = "oracle-l5r-6192", Peace = "oracle-l5r-5895", ImperialCharter = "oracle-l5r-9990", TremblingEarth = "oracle-l5r-10595",
        IndomitableHome = "oracle-l5r-3737", Temples = "oracle-l5r-7898", TigerClaw = "oracle-l5r-10910", FloodedPass = "oracle-l5r-2613", Quagmire = "oracle-l5r-6123", Brawl = "oracle-l5r-1127",
        TyphoonSurge = "oracle-l5r-10543", Merchant = "training-merchant";

    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static bool Offered(MatchEngine e, int player, string ability) => Listed(e, player, ability).Any(a => a.Enabled);
    private static GameCommand ReactionCommand(MatchEngine e, int player, string ability, string? targetId = null)
    {
        var enabled = Listed(e, player, ability).Where(a => a.Enabled && (targetId is null || a.Command!.TargetId == targetId)).ToArray();
        Check(enabled.Length >= 1, $"{ability} for player {player}: expected an enabled offer, got " + string.Join("; ", Listed(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return enabled[0].Command!;
    }
    private static void Hold(MatchEngine e)
    {
        for (var guard = 0; Window(e) is "beforeActionReaction" or "attackReaction" && guard < 12; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static void Region(MatchState s, int owner, int province, string def = Quagmire)
    {
        var region = Add(s, def, owner, Zone.Region, province: province); s.Players[owner].Provinces[province].RegionId = region.Id;
    }

    // ---- Fallen ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FallenScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Fallen, 0, Zone.Hand); Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var fallen = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Fallen");
        var plain = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var away = Kw(Add(s, "training-crane-veteran", 1, Zone.Home), "Fallen");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.FallenBow, card.Id).TargetIds!;
        Check(targets.Contains(fallen.Id) && !targets.Contains(plain.Id) && !targets.Contains(away.Id), "only a Fallen card at the battlefield: " + string.Join(",", targets));
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.FallenBow, card.Id, plain.Id));
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.FallenBow, card.Id, fallen.Id));
        Check(e.State.Cards[fallen.Id].Bowed && !e.State.Cards[plain.Id].Bowed, "the Fallen card is bowed");
        // Open: a Fallen card at home, in the Action Phase. The Kharmic reminder is no ability of the card.
        var o = Table(Phase.Action, 0);
        var oc = Add(o, Fallen, 0, Zone.Hand); var home = Kw(Add(o, "training-crane-veteran", 1, Zone.Home), "Fallen");
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, PromoEmperorFateAbilities.FallenBow, oc.Id, home.Id));
        Check(oe.State.Cards[home.Id].Bowed, "Open: bowed at home");
        Check(CardCatalog.Get(Fallen).Keywords!.SequenceEqual(["Kharmic"]) && CardCatalog.Get(Fallen).Abilities!.SequenceEqual([PromoEmperorFateAbilities.FallenBow]), "Kharmic, and no ability for its reminder text");
    }

    // ---- It Was a Cat ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ItWasACatScenario()
    {
        foreach (var ninja in new[] { false, true })
        {
            var s = Table(Phase.Action, 0);
            var card = Add(s, ItWasACat, 0, Zone.Hand);
            var performer = Add(s, Crane, 0, Zone.Home); if (ninja) Kw(performer, "Ninja");
            var spare = Add(s, Lion, 0, Zone.Home);
            s.Players[0].Honor = 10;
            var e = MatchEngine.FromTestState(s);
            var offer = Enabled(e, 0, PromoEmperorFateAbilities.CatLook, card.Id, performer.Id);
            Accept(e, offer.Command!);
            Check(e.State.Cards[performer.Id].Bowed && !e.State.Cards[spare.Id].Bowed, "the performer is bowed");
            Eq(e.State.Players[0].Honor, 9, "1 Honor is lost");
            Eq(Window(e), "lookDeck", "the deck is chosen");
            Check(e.QueryActions(0).Count(a => a.Type == "chooseLookDeck") == 4, "any of the four decks");
            var viewed = e.State.Players[1].DynastyDeck.Take(ninja ? 4 : 3).ToArray();
            Accept(e, new GameCommand("chooseLookDeck", 0, e.Version, Choice: "Dynasty", TargetPlayerId: 1));
            Eq(e.State.PendingLook!.Count, ninja ? 4 : 3, ninja ? "four cards for a Ninja performer" : "three cards");
            var arrangement = Enumerable.Range(0, ninja ? 4 : 3).Select(i => i == 0 ? "0:discard" : $"{i}:top").ToArray();
            Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: arrangement));
            Eq(e.State.Cards[viewed[0]].Zone, Zone.DynastyDiscard, "the discarded card goes to its owner's Dynasty discard pile");
            Check(e.State.Players[1].DynastyDeck.Take(viewed.Length - 1).SequenceEqual(viewed.Skip(1)), "the rest stay on top in the order chosen");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
        // A bowed Personality cannot perform; a Follower can (any unbowed Personality or Follower of the player's).
        var b = Table(Phase.Action, 0);
        var bc = Add(b, ItWasACat, 0, Zone.Hand); var tired = Add(b, Crane, 0, Zone.Home, bowed: true); var host = Add(b, Lion, 0, Zone.Home, bowed: true); var follower = Attach(b, "oracle-l5r-10682", host);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, PromoEmperorFateAbilities.CatLook, bc.Id, tired.Id), "a bowed Personality cannot perform");
        Check(CanUse(be, 0, PromoEmperorFateAbilities.CatLook, bc.Id, follower.Id), "an unbowed Follower can");
    }

    // ---- Sinkhole --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SinkholeScenario()
    {
        foreach (var choice in new[] { "bow", "home" })
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, Sinkhole, 0, Zone.Hand); Add(s, Crane, 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0); var terrain = Add(s, FloodedPass, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, PromoEmperorFateAbilities.SinkholeRegion, card.Id), "no Region at the battlefield: the Region entry is not offered");
            Accept(e, Offer(e, 0, PromoEmperorFateAbilities.SinkholeTerrain, card.Id, enemy.Id));
            Eq(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed");
            Eq(Window(e), "effectChoice", "bow or move home is the acting player's choice");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == choice).Command!);
            if (choice == "bow") Check(e.State.Cards[enemy.Id].Bowed && e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "he is bowed");
            else Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "he moved home");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
        // The Region of the province under attack (any player's), through the other entry.
        var r = Table(Phase.Battle, 0);
        var rc = Add(r, Sinkhole, 0, Zone.Hand); Add(r, Crane, 0, Zone.Battlefield, province: 0); var foe = Add(r, Lion, 1, Zone.Battlefield, province: 0);
        Region(r, 1, 0); var other = Quagmire; Region(r, 1, 1, "oracle-l5r-9865");
        var re = MatchEngine.FromTestState(r);
        Check(!CanUse(re, 0, PromoEmperorFateAbilities.SinkholeTerrain, rc.Id), "no Terrain: the Terrain entry is not offered");
        var regionAtBattle = re.State.Players[1].Provinces[0].RegionId!; var elsewhere = re.State.Players[1].Provinces[1].RegionId!;
        Accept(re, Offer(re, 0, PromoEmperorFateAbilities.SinkholeRegion, rc.Id, foe.Id));
        Eq(re.State.Cards[regionAtBattle].Zone, Zone.DynastyDiscard, "the Region at the battlefield was destroyed"); Eq(re.State.Cards[elsewhere].Zone, Zone.Region, "the one at another province stays");
        Check(re.State.Players[1].Provinces[0].RegionId is null, "the province has no Region now"); _ = other;
        Eq(Window(re), "effectChoice", "then the choice");
        // A target away from the battle is refused, and so is the action with nothing to destroy.
        var n = Table(Phase.Battle, 0);
        var nc = Add(n, Sinkhole, 0, Zone.Hand); Add(n, Crane, 0, Zone.Battlefield, province: 0); Add(n, Lion, 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, PromoEmperorFateAbilities.SinkholeTerrain, nc.Id) && !CanUse(ne, 0, PromoEmperorFateAbilities.SinkholeRegion, nc.Id), "nothing to destroy: not offered");
    }

    // ---- Closing the Gap -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card, CardInstance Moved, CardInstance AtBattle, CardInstance Reserve) ClosingTable()
    {
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, Shugenja, 1, Zone.Battlefield, province: 0); var spell = Attach(s, TyphoonSurge, attacker);
        var atBattle = Add(s, Crane, 0, Zone.Battlefield, province: 0); var moved = Add(s, "training-crane-veteran", 0, Zone.Home); var reserve = Add(s, "training-merchant", 0, Zone.Home);
        var card = Add(s, ClosingTheGap, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "tf-typhoon-surge-move", spell.Id, moved.Id));
        return (e, card, moved, atBattle, reserve);
    }

    private static void ClosingTheGapScenario()
    {
        var (e, card, moved, atBattle, reserve) = ClosingTable();
        Eq(Window(e), "afterActionResolvedReaction", "the action moved a Personality to the battlefield");
        Eq(e.State.Cards[moved.Id].Zone, Zone.Battlefield, "he is at the battlefield now");
        Check(Offered(e, 0, PromoEmperorFateAbilities.ClosingToBattle) && Offered(e, 0, PromoEmperorFateAbilities.ClosingHome), "both ways are offered to the holder of the Strategy");
        Check(!Offered(e, 1, PromoEmperorFateAbilities.ClosingToBattle), "the other player holds none");
        // Move him to the current battlefield (a Personality that is not there), straightening his unit.
        var s2 = e.State; s2.Cards[reserve.Id].Bowed = true;
        var e2 = MatchEngine.FromTestState(s2);
        Check(CanUse(e2, 0, PromoEmperorFateAbilities.ClosingToBattle, card.Id, reserve.Id), "a Personality at home may be moved to the battlefield");
        Check(!CanUse(e2, 0, PromoEmperorFateAbilities.ClosingToBattle, card.Id, atBattle.Id), "not one already there");
        Accept(e2, Use(e2, 0, PromoEmperorFateAbilities.ClosingToBattle, card.Id, null, reserve.Id));
        Eq(e2.State.Cards[reserve.Id].Zone, Zone.Battlefield, "he is at the battlefield"); Check(!e2.State.Cards[reserve.Id].Bowed, "and his unit is straightened");
        Eq(e2.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // From the current battlefield to his own home, straightening his unit.
        var (e3, card3, moved3, atBattle3, _) = ClosingTable();
        var s3 = e3.State; s3.Cards[atBattle3.Id].Bowed = true; var e4 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e4, 0, PromoEmperorFateAbilities.ClosingHome, card3.Id, e4.State.Cards.Values.First(c => c.Zone == Zone.Home && c.OwnerId == 0).Id), "a Personality at home cannot be moved home");
        Accept(e4, Use(e4, 0, PromoEmperorFateAbilities.ClosingHome, card3.Id, null, atBattle3.Id));
        Eq(e4.State.Cards[atBattle3.Id].Zone, Zone.Home, "he went home"); Check(!e4.State.Cards[atBattle3.Id].Bowed, "straightened");
        // One use: the other way is no longer offered.
        Check(!Offered(e4, 0, PromoEmperorFateAbilities.ClosingToBattle), "the Strategy is spent");
        // An action that moved nobody to or from the battlefield offers no Reaction.
        var q = Table(Phase.Battle, 1);
        var sh = Add(q, Shugenja, 1, Zone.Battlefield, province: 0); var kw = Attach(q, "oracle-l5r-10701", sh);
        Add(q, Crane, 0, Zone.Battlefield, province: 0); Add(q, ClosingTheGap, 0, Zone.Hand); var victim = Add(q, Crane, 0, Zone.Battlefield, province: 0);
        var qe = MatchEngine.FromTestState(q);
        Accept(qe, Offer(qe, 1, "tf-kiyoshi-bow", kw.Id, victim.Id));
        Check(Window(qe) != "afterActionResolvedReaction" || !Offered(qe, 0, PromoEmperorFateAbilities.ClosingHome), "a bow moves nobody: Closing the Gap is not offered");
    }

    // ---- Interrogation ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void InterrogationScenario()
    {
        // Open: pay 2 Gold, draw a card.
        var s = Table(Phase.Action, 0);
        var card = Add(s, Interrogation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        var offer = Enabled(e, 0, PromoEmperorFateAbilities.InterrogationDraw, card.Id);
        Check(offer.Gold is { Cost: 2 }, "it costs 2 Gold");
        Accept(e, offer.Command!);
        Eq(e.State.Players[0].Hand.Count, handBefore, "the Strategy left the hand and one card was drawn");
        Check(e.State.Players[0].Hand.All(id => id != card.Id), "the Strategy is not in the hand");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // Reaction: another player's Brawl destroys the reactor's Region -- negated; without it the Region falls.
        foreach (var reacts in new[] { false, true })
        {
            var t = Table(Phase.Action, 1);
            var brawl = Add(t, Brawl, 1, Zone.Hand); var guard = Add(t, Interrogation, 0, Zone.Hand); Region(t, 0, 0);
            var te = MatchEngine.FromTestState(t);
            var regionId = te.State.Players[0].Provinces[0].RegionId!;
            Accept(te, Offer(te, 1, "brawl-destroy", brawl.Id, regionId));
            Eq(Window(te), "beforeActionReaction", "the action is held");
            if (reacts) { Accept(te, ReactionCommand(te, 0, PromoEmperorFateAbilities.InterrogationNegate)); Eq(te.State.Cards[guard.Id].Zone, Zone.FateDiscard, "the Strategy was played"); }
            else Check(Offered(te, 0, PromoEmperorFateAbilities.InterrogationNegate), "offered while holding it");
            Hold(te);
            Eq(te.State.Cards[regionId].Zone, reacts ? Zone.Region : Zone.DynastyDiscard, reacts ? "its destruction was negated" : "control: the Region was destroyed");
            Check(te.State.NegatedEffects.Count == 0, "no negation outlives the action");
        }
        // A Terrain too, and a Holding bowed by Set the Fields Aflame.
        var u = Table(Phase.Action, 1);
        var sh = Add(u, Shugenja, 1, Zone.Home); var fields = Attach(u, "oracle-l5r-9888", sh);
        var holding = Add(u, IndomitableHome, 0, Zone.Home); Add(u, Interrogation, 0, Zone.Hand);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Offer(ue, 1, "set-fields-holding", fields.Id, holding.Id)); Eq(Window(ue), "beforeActionReaction");
        Accept(ue, ReactionCommand(ue, 0, PromoEmperorFateAbilities.InterrogationNegate)); Hold(ue);
        Check(!ue.State.Cards[holding.Id].Bowed, "the Holding is not bowed");
        var ve = MatchEngine.FromTestState(u); Accept(ve, Offer(ve, 1, "set-fields-holding", ve.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9888").Id, holding.Id)); Hold(ve);
        Check(ve.State.Cards[holding.Id].Bowed, "control: without the Reaction it is bowed");
        // Not offered against an action on a Personality, on the player's own cards or by the player himself.
        var n = Table(Phase.Action, 1);
        var nb = Add(n, Brawl, 1, Zone.Hand); var own = Add(n, Interrogation, 1, Zone.Hand); Region(n, 1, 0); var foe = Add(n, Lion, 0, Zone.Home); Add(n, "oracle-l5r-1127", 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 1, "brawl-destroy", nb.Id, ne.State.Players[1].Provinces[0].RegionId!));
        Check(Window(ne) != "beforeActionReaction" || !Offered(ne, 1, PromoEmperorFateAbilities.InterrogationNegate), "it never protects the acting player's own card"); _ = own; _ = foe;
    }

    // ---- Well Protected --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WellProtectedScenario()
    {
        foreach (var (ability, reacts) in new[] { ("", false), (PromoEmperorFateAbilities.WellProtectedBow, true), (PromoEmperorFateAbilities.WellProtectedDestroy, true) })
        {
            var s = Table(Phase.Battle, 1);
            var wielder = Add(s, Lion, 1, Zone.Battlefield, province: 0); var weapon = Attach(s, "training-weapon-with-attack", wielder);
            var victim = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var armor = Attach(s, "training-armor", victim);
            var card = Add(s, WellProtected, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, victim.Id)); Eq(Window(e), "attackReaction", "the Attack is held");
            if (reacts)
            {
                var options = Listed(e, 0, ability).Where(a => a.Enabled).ToArray();
                Check(options.Length == 1 && options[0].Command!.TargetId == armor.Id, "one offer per Armor: " + string.Join(";", Listed(e, 0, ability).Select(a => a.Label)));
                Accept(e, options[0].Command!);
                Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
                if (ability == PromoEmperorFateAbilities.WellProtectedBow) Check(e.State.Cards[armor.Id].Bowed && e.State.Cards[armor.Id].Zone == Zone.Attached, "the Armor was bowed");
                else Check(e.State.Cards[armor.Id].Zone != Zone.Attached, "the Armor was destroyed");
                Check(!Offered(e, 0, PromoEmperorFateAbilities.WellProtectedBow) && !Offered(e, 0, PromoEmperorFateAbilities.WellProtectedDestroy), "one use");
            }
            for (var guard = 0; Window(e) == "attackReaction" && guard < 12; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.Cards[victim.Id].Zone == Zone.Battlefield, reacts, reacts ? "his destruction was negated" : "control: the Attack destroyed him");
        }
        // Not offered without an Armor, against the player's own action, for an unbowed-only bow with a bowed Armor, or when the action could not destroy him.
        var n = Table(Phase.Battle, 1);
        var nw = Add(n, Lion, 1, Zone.Battlefield, province: 0); var nweapon = Attach(n, "training-weapon-with-attack", nw);
        var bare = Add(n, Shugenja, 0, Zone.Battlefield, province: 0); Add(n, WellProtected, 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 1, "training-weapon-attack", nweapon.Id, bare.Id));
        Check(!Offered(ne, 0, PromoEmperorFateAbilities.WellProtectedBow) && !Offered(ne, 0, PromoEmperorFateAbilities.WellProtectedDestroy), "no Armor: nothing to pay with");
        var m = Table(Phase.Battle, 1);
        var mw = Add(m, Lion, 1, Zone.Battlefield, province: 0); var mweapon = Attach(m, "training-weapon-with-attack", mw);
        var armored = Add(m, Shugenja, 0, Zone.Battlefield, province: 0); Attach(m, "training-armor", armored, bowed: true); Add(m, WellProtected, 0, Zone.Hand);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Offer(me, 1, "training-weapon-attack", mweapon.Id, armored.Id));
        Check(!Offered(me, 0, PromoEmperorFateAbilities.WellProtectedBow) && Offered(me, 0, PromoEmperorFateAbilities.WellProtectedDestroy), "a bowed Armor can be destroyed, not bowed");
    }

    // ---- Imminent Victory ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImminentVictoryScenario()
    {
        foreach (var attackerForce in new[] { 6, 1 })
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, ImminentVictory, 0, Zone.Hand);
            var a1 = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); var a2 = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            var d1 = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            if (attackerForce == 1) { Batch336Support.Force(a1, -5); Batch336Support.Force(a2, -2); }
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, PromoEmperorFateAbilities.ImminentVictory, card.Id).Command!);
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent when played");
            Check(e.State.PromoFate.ResolutionWatches.Count == 1, "its watch of this battle is set");
            Batch336Support.RunUntil(e, () => e.State.CurrentBattleProvince is null);
            var tokens = new[] { a1, a2 }.Select(c => e.State.Cards[c.Id].Tokens.Count(t => t.Type == "Imminent Victory")).ToArray();
            if (attackerForce == 6)
            {
                Check(tokens.All(t => t == 1), "each Personality of the winning army got a token: " + string.Join(",", tokens));
                var token = e.State.Cards[a2.Id].Tokens.Single(t => t.Type == "Imminent Victory");
                Check(token.Stat == "Force" && token.Amount == 1 && token.Stat2 == "Chi" && token.Amount2 == 1, "a +1F/+1C token");
                Eq(e.CardForceForTest(a2.Id), 3 + 1, "and it counts");
            }
            else Check(tokens.All(t => t == 0), "the army destroyed neither the enemy army nor the Province: no token");
            Check(e.State.PromoFate.ResolutionWatches.Count == 0, "the watch is spent either way");
        }
        // Only the player who played it: the other side's winning army gets nothing.
        var o = Table(Phase.Battle, 0);
        var oc = Add(o, ImminentVictory, 0, Zone.Hand); var weak = Add(o, Crane, 0, Zone.Battlefield, province: 0); Batch336Support.Force(weak, -2);
        var strong = Add(o, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Enabled(oe, 0, PromoEmperorFateAbilities.ImminentVictory, oc.Id).Command!);
        Batch336Support.RunUntil(oe, () => oe.State.CurrentBattleProvince is null);
        Check(oe.State.Cards[strong.Id].Tokens.Count == 0 && oe.State.Cards[weak.Id].Tokens.Count == 0, "the defending winner gets none");
    }

    // ---- Resurrection and Renewal ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void ResurrectionScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Resurrection, 0, Zone.Hand); var fodder = Add(s, Lion, 0, Zone.Hand);
        var dead = Add(s, Merchant, 0, Zone.Dead); var discarded = Add(s, Crane, 0, Zone.DynastyDiscard); var foreign = Add(s, Merchant, 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        var slots = Enabled(e, 0, PromoEmperorFateAbilities.ResurrectionRecruit, card.Id).Slots!;
        Check(slots.Single(x => x.Slot == "target").CardIds.SequenceEqual([dead.Id]), "only one of the player's own dead Personalities: " + string.Join(",", slots.Single(x => x.Slot == "target").CardIds));
        Check(slots.Single(x => x.Slot == "discard").CardIds.SequenceEqual([fodder.Id]), "the card to discard is another card in hand, never the Strategy itself");
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.ResurrectionRecruit, card.Id, null, null, Pick("target", discarded.Id), Pick("discard", fodder.Id)));
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.ResurrectionRecruit, card.Id, null, null, Pick("target", foreign.Id), Pick("discard", fodder.Id)));
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.ResurrectionRecruit, card.Id, null, null, Pick("target", dead.Id), Pick("discard", card.Id)));
        var goldBefore = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, Enabled(e, 0, PromoEmperorFateAbilities.ResurrectionRecruit, card.Id).Command!);
        Eq(e.State.Cards[dead.Id].Zone, Zone.Home, "he is in play at home");
        Eq(e.State.Cards[fodder.Id].Zone, Zone.FateDiscard, "the discard was paid"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed) > goldBefore, "and his price was paid in Gold (producers bowed)");
        // With nothing else in hand there is no card to discard: the action is not available.
        var t = Table(Phase.Action, 0);
        var lone = Add(t, Resurrection, 0, Zone.Hand); Add(t, Merchant, 0, Zone.Dead);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, PromoEmperorFateAbilities.ResurrectionRecruit, lone.Id), "no other card to discard: not offered");
        // A negated action still discards its card (the cost) and recruits nobody.
        var n = Table(Phase.Action, 0);
        var nc = Add(n, Resurrection, 0, Zone.Hand); var nf = Add(n, Lion, 0, Zone.Hand); var nd = Add(n, Merchant, 0, Zone.Dead);
        var ne = MatchEngine.FromTestState(n); ne.NegateEveryAction = true;
        Accept(ne, Enabled(ne, 0, PromoEmperorFateAbilities.ResurrectionRecruit, nc.Id).Command!);
        Eq(ne.State.Cards[nf.Id].Zone, Zone.FateDiscard, "the cost was paid"); Eq(ne.State.Cards[nd.Id].Zone, Zone.Dead, "nobody came back");
    }

    private static void RenewalScenario()
    {
        MatchEngine Table0(int activeTurns, int active = 0)
        {
            var s = Table(Phase.Action, active);
            s.Players[active].TurnsTaken = activeTurns;
            var card = Add(s, Renewal, 0, Zone.Hand); Add(s, Lion, 0, Zone.Hand); Add(s, Merchant, 0, Zone.DynastyDiscard);
            return MatchEngine.FromTestState(s);
        }
        var early = Table0(2);
        Check(!CanUse(early, 0, PromoEmperorFateAbilities.RenewalRecruit), "the active player's second turn: refused");
        var first = Table0(1);
        Check(!CanUse(first, 0, PromoEmperorFateAbilities.RenewalRecruit), "the active player's first turn: refused");
        var ok = Table0(3);
        var card = ok.State.Cards.Values.Single(c => c.DefinitionId == Renewal);
        var slots = Enabled(ok, 0, PromoEmperorFateAbilities.RenewalRecruit, card.Id).Slots!;
        Check(slots.Single(x => x.Slot == "target").CardIds.Length == 1, "a discarded Personality: " + string.Join(",", slots.Single(x => x.Slot == "target").CardIds));
        Accept(ok, Enabled(ok, 0, PromoEmperorFateAbilities.RenewalRecruit, card.Id).Command!);
        var recruited = ok.State.Cards.Values.Single(c => c.DefinitionId == Merchant);
        Eq(recruited.Zone, Zone.Home, "he is brought into play at home");
        // A dead Personality is a discarded one too; the rule reads the ACTIVE player's turn, whoever plays it (the opponent's own turns do not count).
        var d = Table(Phase.Action, 1);
        d.Players[1].TurnsTaken = 4; d.Players[0].TurnsTaken = 1;
        var dc = Add(d, Renewal, 0, Zone.Hand); Add(d, Lion, 0, Zone.Hand); var dead = Add(d, Merchant, 0, Zone.Dead);
        var de = MatchEngine.FromTestState(d); de.State.PriorityPlayerId = 0;
        var dd = de.State; dd.PriorityPlayerId = 0; var de2 = MatchEngine.FromTestState(dd);
        Check(de2.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoEmperorFateAbilities.RenewalRecruit && a.Enabled), "on the opponent's later turn, from the Dead pile too (the active player's turn count is the one that matters)");
        _ = dc; _ = dead;
        var x = Table(Phase.Action, 1);
        x.Players[1].TurnsTaken = 2; x.Players[0].TurnsTaken = 9;
        Add(x, Renewal, 0, Zone.Hand); Add(x, Lion, 0, Zone.Hand); Add(x, Merchant, 0, Zone.Dead); x.PriorityPlayerId = 0;
        var xe = MatchEngine.FromTestState(x);
        Check(!xe.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoEmperorFateAbilities.RenewalRecruit && a.Enabled), "the opponent's second turn: refused, though it is the player's own ninth");
    }

    // ---- Govern the Land and Re-fortification ---------------------------------------------------------------------------------------------------------------------------------

    private static void GovernTheLandScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, GovernTheLand, 0, Zone.Hand);
        var holding = Add(s, IndomitableHome, 0, Zone.DynastyDiscard); var foreign = Add(s, IndomitableHome, 1, Zone.DynastyDiscard); var personality = Add(s, Merchant, 0, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.GovernHolding, card.Id).TargetIds!;
        Check(targets.Contains(holding.Id) && !targets.Contains(foreign.Id) && !targets.Contains(personality.Id), "a Holding in the player's own discard pile: " + string.Join(",", targets));
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.GovernHolding, card.Id, personality.Id));
        var quote = Enabled(e, 0, PromoEmperorFateAbilities.GovernHolding, card.Id).Gold;
        Check(quote is { Cost: 2 }, "its whole recruit price is paid: " + quote?.Cost);
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.GovernHolding, card.Id, holding.Id));
        Eq(e.State.Cards[holding.Id].Zone, Zone.Home, "the Holding is in play"); Check(e.State.Cards[holding.Id].Bowed, "bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // The second action: a Follower or Item from the discard pile to the performing Personality, paying its Equip cost. Open at home, and in a battle.
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
        {
            var a = Table(phase, 0);
            var c = Add(a, GovernTheLand, 0, Zone.Hand); var claw = Add(a, TigerClaw, 0, Zone.FateDiscard); var who = Add(a, Crane, 0, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
            if (phase == Phase.Battle) Add(a, Lion, 1, Zone.Battlefield, province: 0);
            var ae = MatchEngine.FromTestState(a);
            var offer = Enabled(ae, 0, PromoEmperorFateAbilities.GovernAttach, c.Id, who.Id);
            Check(offer.TargetIds!.Contains(claw.Id), "the Item in the discard pile is a target");
            Accept(ae, Offer(ae, 0, PromoEmperorFateAbilities.GovernAttach, c.Id, claw.Id, who.Id));
            Eq(ae.State.Cards[claw.Id].Zone, Zone.Attached, $"{phase}: attached"); Eq(ae.State.Cards[claw.Id].AttachedTo, who.Id, $"{phase}: to the performer");
        }
    }

    private static void ReFortificationScenario()
    {
        // Limited: a Castle Holding from the discard pile, bowed, paying its whole price.
        var s = Table(Phase.Action, 0);
        var card = Add(s, ReFortification, 0, Zone.Hand);
        var castle = Add(s, IndomitableHome, 0, Zone.DynastyDiscard); var plain = Add(s, Temples, 0, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.FortHolding, card.Id).TargetIds!;
        Check(targets.Contains(castle.Id) && !targets.Contains(plain.Id), "a Castle Holding only: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.FortHolding, card.Id, castle.Id));
        Eq(e.State.Cards[castle.Id].Zone, Zone.Home, "in play"); Check(e.State.Cards[castle.Id].Bowed, "bowed");
        // Battle: even with no unit at the battlefield, bow an unbowed Castle and move an enemy Personality home.
        var b = Table(Phase.Battle, 0);
        var bc = Add(b, ReFortification, 0, Zone.Hand); var keep = Add(b, IndomitableHome, 0, Zone.Home); var tired = Add(b, "oracle-l5r-7444", 0, Zone.Home, bowed: true);
        var enemy = Add(b, Lion, 1, Zone.Battlefield, province: 0); var homeEnemy = Add(b, "training-lion-veteran", 1, Zone.Home);
        var be = MatchEngine.FromTestState(b);
        var offer = Enabled(be, 0, PromoEmperorFateAbilities.FortMove, bc.Id, keep.Id);
        Check(offer.TargetIds!.Contains(enemy.Id) && !offer.TargetIds.Contains(homeEnemy.Id), "an enemy Personality at the battlefield");
        Check(!CanUse(be, 0, PromoEmperorFateAbilities.FortMove, bc.Id, tired.Id), "a bowed Castle cannot be bowed");
        Accept(be, Offer(be, 0, PromoEmperorFateAbilities.FortMove, bc.Id, enemy.Id, keep.Id));
        Check(be.State.Cards[keep.Id].Bowed, "the Castle is bowed"); Eq(be.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
        Eq(be.State.Cards[bc.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
    }

    // ---- Peace -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PeaceScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Peace, 0, Zone.Hand);
        var plain = Add(s, IndomitableHome, 0, Zone.DynastyDeck); var unique = Add(s, Temples, 0, Zone.DynastyDeck); var personality = Add(s, Merchant, 0, Zone.DynastyDeck);
        s.Players[0].DynastyDeck.Clear(); s.Players[0].DynastyDeck.AddRange([unique.Id, personality.Id, plain.Id]);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, PromoEmperorFateAbilities.PeaceAbility, card.Id).Command!);
        Eq(Window(e), "searchSelect", "the Dynasty deck is searched");
        var takes = e.QueryActions(0).Where(a => a.Type == "selectSearchCard" && a.Enabled).Select(a => a.Command!.Choice).ToArray();
        Check(takes.SequenceEqual([IndomitableHome]), "only the non-Unique Holding qualifies: " + string.Join(",", takes));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "selectSearchCard" && a.Enabled).Command!);
        Eq(Window(e), "searchRecruitPay", "its whole price is asked for");
        Check(e.State.PendingChoice!.Prompt.Contains("bowed") && !e.State.PendingChoice.Prompt.Contains("unbowed"), "the prompt says bowed: " + e.State.PendingChoice.Prompt);
        var pay = e.QueryActions(0).First(a => a.Type == "chooseSearchPayment" && a.Enabled && a.Command!.Choice is null);
        Check(pay.Label.Contains("bowed") && !pay.Label.Contains("unbowed"), "and so does the option: " + pay.Label);
        Accept(e, pay.Command!);
        Eq(e.State.Cards[plain.Id].Zone, Zone.Home, "the Holding is in play"); Check(e.State.Cards[plain.Id].Bowed, "bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded after the search");
        Check(e.State.Players[0].DynastyDeck.Count == 2 && !e.State.Players[0].DynastyDeck.Contains(plain.Id), "the deck was shuffled without it");
        Check(SearchCatalog.All.ContainsKey("pe-peace") && SearchCatalog.All["versatile-farm"].Decks[0].Effect == SearchEffect.RecruitUnbowed, "Peace's search is catalogued beside Versatile Farm's");
    }

    // ---- Imperial Charter ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImperialCharterScenario()
    {
        foreach (var (first, second) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            var s = Table(Phase.Action, 0); s.Players[0].TurnsTaken = 3;
            var card = Add(s, ImperialCharter, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, PromoEmperorFateAbilities.CharterCreate, card.Id).Command!);
            Eq(Window(e), "deciderChoice", "the acting player decides first"); Eq(e.State.PendingChoice!.PlayerId, 0, "the acting player");
            Check(e.QueryActions(1).All(a => a.Type != "chooseDecider"), "the other player has nothing to choose yet");
            Accept(e, first ? e.QueryActions(0).First(a => a.Type == "chooseDecider" && a.Command!.Choice != "done").Command! : e.QueryActions(0).Single(a => a.Type == "chooseDecider" && a.Command!.Choice == "done").Command!);
            Eq(Window(e), "deciderChoice", "then the other player"); Eq(e.State.PendingChoice!.PlayerId, 1, "player 1 decides");
            Accept(e, second ? e.QueryActions(1).First(a => a.Type == "chooseDecider" && a.Command!.Choice != "done").Command! : e.QueryActions(1).Single(a => a.Type == "chooseDecider" && a.Command!.Choice == "done").Command!);
            Check(Window(e) is null, "the action is complete");
            var charters = e.State.Cards.Values.Where(c => c.DefinitionId == PromoEmperorFateAbilities.CharterScan).ToArray();
            Eq(charters.Count(c => c.OwnerId == 0 && c.Zone == Zone.Home && !c.Bowed), first ? 1 : 0, "the acting player's Holding, unbowed");
            Eq(charters.Count(c => c.OwnerId == 1 && c.Zone == Zone.Home && !c.Bowed), second ? 1 : 0, "the other player's Holding, unbowed");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        }
        // Not on the player's first turn.
        var f = Table(Phase.Action, 0); f.Players[0].TurnsTaken = 1;
        var fc = Add(f, ImperialCharter, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(f), 0, PromoEmperorFateAbilities.CharterCreate, fc.Id), "first turn: refused");
        // The created Holding produces 2 Gold when bowed.
        var g = Table(Phase.Action, 0); g.Players[0].TurnsTaken = 3;
        var gc = Add(g, ImperialCharter, 0, Zone.Hand);
        var ge = MatchEngine.FromTestState(g);
        Accept(ge, Enabled(ge, 0, PromoEmperorFateAbilities.CharterCreate, gc.Id).Command!);
        Accept(ge, ge.QueryActions(0).First(a => a.Type == "chooseDecider" && a.Command!.Choice != "done").Command!);
        Accept(ge, ge.QueryActions(1).Single(a => a.Type == "chooseDecider" && a.Command!.Choice == "done").Command!);
        var charter = ge.State.Cards.Values.Single(c => c.DefinitionId == PromoEmperorFateAbilities.CharterScan);
        Eq(ge.GoldProduced(charter.Id, 0), 2, "Bow this card: Produce 2 Gold");
    }

    // ---- Trembling Earth -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TremblingEarthScenario()
    {
        var s = Table(Phase.Battle, 0);
        var spell = Add(s, TremblingEarth, 0, Zone.Hand); Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var earth = Kw(Add(s, Shugenja, 0, Zone.Home), "Earth"); var water = Kw(Add(s, Shugenja, 0, Zone.Home), "Water"); var samurai = Kw(Add(s, Crane, 0, Zone.Home), "Earth");
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, PromoEmperorFateAbilities.TremblingEquip, spell.Id, earth.Id), "an Earth Shugenja at home (any location) may be equipped");
        Check(!CanUse(e, 0, PromoEmperorFateAbilities.TremblingEquip, spell.Id, water.Id) && !CanUse(e, 0, PromoEmperorFateAbilities.TremblingEquip, spell.Id, samurai.Id), "not a non-Earth Shugenja, nor an Earth Personality who is no Shugenja");
        Accept(e, Use(e, 0, PromoEmperorFateAbilities.TremblingEquip, spell.Id, null, earth.Id) with { SourceIds = Offered0(e, spell, earth) });
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "attached"); Eq(e.State.Cards[spell.Id].AttachedTo, earth.Id, "to the Shugenja");
        Check(e.State.AdditionalActions.Count == 0, "Equip as printed grants no additional action (not the rulebook Equip (Battle))");
        // Home Earth Battle: bow a target enemy card, destroy this Spell -- from home, and from the battlefield.
        foreach (var at in new[] { Zone.Home, Zone.Battlefield })
        {
            var t = Table(Phase.Battle, 0);
            var host = Kw(Add(t, Shugenja, 0, at, province: at == Zone.Battlefield ? 0 : null), "Earth"); var sp = Attach(t, TremblingEarth, host);
            var foe = Add(t, Lion, 1, Zone.Battlefield, province: 0); if (at == Zone.Home) Add(t, Crane, 0, Zone.Battlefield, province: 0);
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 0, PromoEmperorFateAbilities.TremblingBow, sp.Id, foe.Id));
            Check(te.State.Cards[foe.Id].Bowed, $"{at}: the enemy card is bowed"); Eq(te.State.Cards[sp.Id].Zone, Zone.FateDiscard, $"{at}: the Spell is destroyed");
        }
        // The Shugenja away from home and the battlefield cannot.
        var u = Table(Phase.Battle, 0);
        var far = Kw(Add(u, Shugenja, 0, Zone.Battlefield, province: 1), "Earth"); var fs = Attach(u, TremblingEarth, far); Add(u, Lion, 1, Zone.Battlefield, province: 0); Add(u, Crane, 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, PromoEmperorFateAbilities.TremblingBow, fs.Id), "a Shugenja at another battlefield is neither home nor here");
    }

    private static string[]? Offered0(MatchEngine e, CardInstance spell, CardInstance performer) => Enabled(e, 0, PromoEmperorFateAbilities.TremblingEquip, spell.Id, performer.Id).Command!.SourceIds;
}
