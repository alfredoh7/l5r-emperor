using L5R.Core;
using static RegressionAssertions;

// Issue #653: the second half of the Holdings and the five Regions (PromoEmperorHoldingsTests.cs has the suite's header).
internal static partial class PromoEmperorHoldingsTests
{
    private const string GoldenPlains = "oracle-l5r-8105"; // a Stronghold with Gold Production 3
    private const string Ronin = "oracle-l5r-6850";         // Shikaro, Ronin

    private static CardInstance PutRegion(MatchState s, string def, int owner, int province)
    {
        var region = Add(s, def, owner, Zone.Region, province: province);
        s.Players[owner].Provinces[province].RegionId = region.Id;
        return region;
    }

    // ---- Second City Dojo, Forgotten Outpost ------------------------------------------------------------------------------------------------------------------------------------

    private static void SecondCityDojoAndOutpost()
    {
        var s = Action(0);
        var dojo = Add(s, SecondCityDojo, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(dojo.Id, 0), 2, "Produce 2 Gold");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "pe-second-city-dojo-draw", dojo.Id).Command!);
        Check(e.State.Cards[dojo.Id].Zone is not (Zone.Home or Zone.Stronghold), "the Holding is destroyed as the cost");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "a card is drawn");
        // A Limited action: only the active player's turn.
        var s2 = Action(1); s2.PriorityPlayerId = 0; var d2 = Add(s2, SecondCityDojo, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-second-city-dojo-draw", d2.Id), "Limited: not on another player's turn");

        // Forgotten Outpost: only while another player has higher Family Honor.
        var o = Action(0); var outpost = Add(o, Outpost, 0, Zone.Home);
        o.Players[1].Honor = o.Players[0].Honor + 1;
        var oe = MatchEngine.FromTestState(o);
        Eq(oe.GoldProduced(outpost.Id, 0), 5, "Produce 5 Gold");
        var honor = Honor(oe, 0);
        Accept(oe, Enabled(oe, 0, "pe-forgotten-outpost-honor", outpost.Id).Command!);
        Eq(Honor(oe, 0), honor + 1, "gain 1 Honor");
        Check(!e.State.Cards[dojo.Id].Bowed && !oe.State.Cards[outpost.Id].Bowed, "no bow is printed for the Limited action");
        foreach (var delta in new[] { 0, -1 })
        {
            var eq = Action(0); var out2 = Add(eq, Outpost, 0, Zone.Home);
            eq.Players[1].Honor = eq.Players[0].Honor + delta;
            var ee = MatchEngine.FromTestState(eq);
            Check(!CanUse(ee, 0, "pe-forgotten-outpost-honor", out2.Id), delta == 0 ? "equal Family Honor is not higher" : "lower Family Honor is not higher");
            Check(Options(ee, 0, "pe-forgotten-outpost-honor", out2.Id).Any(x => x.Reason?.Contains("higher Family Honor") == true), "and says why");
            var version = ee.Version;
            Rejection(ee, Use(ee, 0, "pe-forgotten-outpost-honor", out2.Id));
            Check(ee.Version == version && Honor(ee, 0) == eq.Players[0].Honor, "a forged use changes nothing");
        }
        // "Another player" is the other player: the Outpost's controller being the richer one is no reason.
        var reverse = Action(1); reverse.PriorityPlayerId = 1; var ro = Add(reverse, Outpost, 1, Zone.Home); reverse.Players[1].Honor = reverse.Players[0].Honor + 3;
        Check(!CanUse(MatchEngine.FromTestState(reverse), 1, "pe-forgotten-outpost-honor", ro.Id), "your own higher Honor does not qualify");
    }

    // ---- Public Room -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublicRoomSake()
    {
        var s = Action(0);
        var room = Add(s, PublicRoom, 0, Zone.Home);
        var a = Add(s, "training-crane-samurai", 0, Zone.Home); var b = Add(s, "training-crane-courtier", 0, Zone.Home);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        a.Tokens.Add(new("token-s1", "Sake", "test", "Force", 1)); a.Tokens.Add(new("token-s2", "Sake", "test", "Force", 1));
        b.Tokens.Add(new("token-s3", "Sake", "test", "Force", 1)); b.Tokens.Add(new("token-f1", "Fire", "test", "Force", 1));
        foe.Tokens.Add(new("token-s4", "Sake", "test", "Force", 1));
        var e = MatchEngine.FromTestState(s);
        var forces = (a: e.CardForceForTest(a.Id), b: e.CardForceForTest(b.Id), foe: e.CardForceForTest(foe.Id));
        Eq(e.GoldProduced(room.Id, 0), 0, "Public Room produces no Gold");
        Accept(e, Enabled(e, 0, "pe-public-room-sake", room.Id).Command!);
        Eq(e.CardForceForTest(a.Id), forces.a + 2, "each of his two Sake tokens has +1F");
        Eq(e.CardForceForTest(b.Id), forces.b + 1, "one Sake token, and not the Fire token");
        Eq(e.CardForceForTest(foe.Id), forces.foe, "another player's Sake token is not yours");
        Check(e.State.Cards[room.Id].Bowed, "bowing is the cost");
        // A modifier of the turn (R-001): gone with it.
        var later = Edit(e, st => { st.TurnNumber++; foreach (var c in st.Cards.Values) c.Modifiers.RemoveAll(m => !m.Permanent); });
        Eq(later.CardForceForTest(a.Id), forces.a, "until end of turn");
        // With no Sake token at all the action is still legal (it simply gives nothing).
        var s2 = Action(0); var r2 = Add(s2, PublicRoom, 0, Zone.Home);
        Check(CanUse(MatchEngine.FromTestState(s2), 0, "pe-public-room-sake", r2.Id), "legal with no Sake token");
    }

    // ---- Jungle Stockade ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JungleStockade()
    {
        var s = Action(0);
        var stockade = Add(s, Stockade, 0, Zone.Home);
        var item = Add(s, "training-costly-weapon", 0, Zone.Hand); var follower = Add(s, "training-follower", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var recruit = Add(s, "training-crane-samurai", 0, Zone.Province);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(stockade.Id, 0), 3, "3 Gold in general");
        Eq(e.GoldProduced(stockade.Id, 0, new PaymentSubject(recruit, PaymentKind.Recruit)), 3, "3 for a Personality");
        Eq(e.GoldProduced(stockade.Id, 0, new PaymentSubject(item, PaymentKind.Action)), 3, "3 for an action");
        Eq(e.GoldProduced(stockade.Id, 0, new PaymentSubject(item, PaymentKind.Equip, null, host)), 4, "4 for an Item attached");
        Eq(e.GoldProduced(stockade.Id, 0, new PaymentSubject(follower, PaymentKind.Equip, null, host)), 4, "4 for a Follower attached");
        Check(e.PaymentOptions(0, 4, new PaymentSubject(follower, PaymentKind.Equip, null, host)).Any(o => o.SourceIds.SequenceEqual([stockade.Id])), "it alone pays 4 for an attachment");
        Check(!e.PaymentOptions(0, 4, new PaymentSubject(recruit, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([stockade.Id])), "but not 4 for a Personality");

        // The Open action: two performing Personalities and an attachment on one of them.
        var t = Action(0);
        var yard = Add(t, Stockade, 0, Zone.Home);
        var one = Add(t, "training-crane-samurai", 0, Zone.Home); var two = Add(t, "training-crane-courtier", 0, Zone.Home); var three = Add(t, "training-crane-samurai", 0, Zone.Home);
        var weapon = Add(t, "training-follower", 0, Zone.Attached, parent: one.Id); var stray = Add(t, "training-follower", 0, Zone.Attached, parent: three.Id);
        var foe = Add(t, "training-lion-samurai", 1, Zone.Home); var foeFollower = Add(t, "training-follower", 1, Zone.Attached, parent: foe.Id);
        var te = MatchEngine.FromTestState(t);
        Check(CanUse(te, 0, "pe-jungle-stockade-transfer", yard.Id), "offered with two Personalities and an attachment");
        var version = te.Version;
        Rejection(te, Use(te, 0, "pe-jungle-stockade-transfer", yard.Id, selections: [Pick("performers", one.Id, two.Id), Pick("attachment", stray.Id)]));
        Rejection(te, Use(te, 0, "pe-jungle-stockade-transfer", yard.Id, selections: [Pick("performers", one.Id), Pick("attachment", weapon.Id)]));
        Rejection(te, Use(te, 0, "pe-jungle-stockade-transfer", yard.Id, selections: [Pick("performers", one.Id, foe.Id), Pick("attachment", weapon.Id)]));
        Rejection(te, Use(te, 0, "pe-jungle-stockade-transfer", yard.Id, selections: [Pick("performers", one.Id, two.Id), Pick("attachment", foeFollower.Id)]));
        Check(te.Version == version && te.State.Cards[weapon.Id].AttachedTo == one.Id && te.State.Cards[stray.Id].AttachedTo == three.Id, "an attachment not on a performer, one performer, an enemy performer and an enemy attachment change nothing");
        Accept(te, Use(te, 0, "pe-jungle-stockade-transfer", yard.Id, selections: [Pick("performers", one.Id, two.Id), Pick("attachment", weapon.Id)]));
        Eq(te.State.Cards[weapon.Id].AttachedTo, two.Id, "transferred to the other Personality");
        Check(!te.State.Cards[yard.Id].Bowed, "no bow is printed for the Open action");
        Check(!CanUse(te, 0, "pe-jungle-stockade-transfer", yard.Id), "once per turn");
        // It works in either direction.
        var u = Action(0); var y2 = Add(u, Stockade, 0, Zone.Home); var left = Add(u, "training-crane-samurai", 0, Zone.Home); var right = Add(u, "training-crane-courtier", 0, Zone.Home);
        var onRight = Add(u, "training-follower", 0, Zone.Attached, parent: right.Id);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Use(ue, 0, "pe-jungle-stockade-transfer", y2.Id, selections: [Pick("performers", left.Id, right.Id), Pick("attachment", onRight.Id)]));
        Eq(ue.State.Cards[onRight.Id].AttachedTo, left.Id, "from the second performer to the first");
    }

    // ---- Bandit's Sanctuary ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BanditsSanctuary()
    {
        var s = Action(0);
        var sanctuary = Add(s, Bandits, 0, Zone.Home);
        var ronin = Add(s, Ronin, 0, Zone.Province); var plain = Add(s, "training-crane-samurai", 0, Zone.Province);
        var item = Add(s, "training-costly-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(sanctuary.Id, 0), 2, "2 Gold in general");
        Eq(e.GoldProduced(sanctuary.Id, 0, new PaymentSubject(ronin, PaymentKind.Recruit)), 3, "3 Gold when paying for a Ronin");
        Eq(e.GoldProduced(sanctuary.Id, 0, new PaymentSubject(plain, PaymentKind.Recruit)), 2, "2 for a Personality who is not a Ronin");
        Eq(e.GoldProduced(sanctuary.Id, 0, new PaymentSubject(item, PaymentKind.Equip)), 2, "2 for an Item");
        Eq(e.GoldProduced(sanctuary.Id, 0, new PaymentSubject(ronin, PaymentKind.Action)), 2, "never for an action of a Ronin");
        Check(e.PaymentOptions(0, 3, new PaymentSubject(ronin, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([sanctuary.Id])), "it alone pays 3 for a Ronin");
        Check(!e.PaymentOptions(0, 3, new PaymentSubject(plain, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([sanctuary.Id])), "but not 3 for another Personality");

        // Entering play: a player with a Clan alignment loses 3 Honor; an unaligned one does not.
        foreach (var (clan, loses) in new[] { ("Crane", true), ("Unaligned", false) })
        {
            var d = Action(0); d.Phase = Phase.Dynasty; d.Players[0].Clan = clan;
            for (var i = 0; i < 2; i++) Add(d, "training-marketplace", 0, Zone.Home);
            var province = d.Players[0].Provinces[0]; var card = d.Cards[province.CardId!]; card.DefinitionId = Bandits; card.FaceUp = true;
            var de = MatchEngine.FromTestState(d);
            var honor = Honor(de, 0);
            Accept(de, new GameCommand("purchase", 0, de.Version, CardId: card.Id, SourceIds: de.PaymentOptions(0, 2, new PaymentSubject(de.State.Cards[card.Id], PaymentKind.Recruit)).First().SourceIds));
            Eq(de.State.Cards[card.Id].Zone, Zone.Home, "in play");
            Eq(Honor(de, 0), honor - (loses ? 3 : 0), loses ? "a Clan player loses 3 Honor" : "an unaligned player loses nothing");
        }
        // The deck-construction line: up to five copies.
        Eq(CardCatalog.Get(Bandits).DeckCopyLimit, 5, "the printed limit of copies");
    }

    // ---- Plantation --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PlantationStronghold()
    {
        MatchEngine Position(int plantations, out CardInstance stronghold, out PaymentSubject item, out PaymentSubject personality, out PaymentSubject action, out PaymentSubject holding)
        {
            var s = Action(0);
            s.Cards[s.Players[0].StrongholdId].DefinitionId = GoldenPlains;
            for (var i = 0; i < plantations; i++) Add(s, Plantation, 0, Zone.Home);
            var host = Add(s, "training-crane-samurai", 0, Zone.Home);
            var weapon = Add(s, "training-costly-weapon", 0, Zone.Hand); var recruit = Add(s, "training-crane-samurai", 0, Zone.Province);
            var farm = Add(s, "training-farm", 0, Zone.Province);
            var e = MatchEngine.FromTestState(s);
            stronghold = e.State.Cards[s.Players[0].StrongholdId];
            item = new PaymentSubject(weapon, PaymentKind.Equip, null, host); personality = new PaymentSubject(recruit, PaymentKind.Recruit);
            action = new PaymentSubject(weapon, PaymentKind.Action); holding = new PaymentSubject(farm, PaymentKind.Recruit);
            return e;
        }
        var e0 = Position(0, out var sh0, out var i0, out _, out _, out _);
        var printed = CardCatalog.Get(GoldenPlains).GoldProduction;
        Eq(e0.GoldProduced(sh0.Id, 0, i0), printed, "without a Plantation: the printed production");
        var e1 = Position(1, out var sh, out var item, out var personality, out var action, out var holding);
        Eq(e1.GoldProduced(sh.Id, 0, item), printed + 1, "one more Gold for an attachment");
        Eq(e1.GoldProduced(sh.Id, 0, holding), printed + 1, "and for a Holding");
        Eq(e1.GoldProduced(sh.Id, 0, personality), printed, "never for a Personality");
        Eq(e1.GoldProduced(sh.Id, 0, action), printed, "never for an action of a card in play");
        Eq(e1.GoldProduced(sh.Id, 0), printed, "nor when no payment is named");
        var e2 = Position(2, out var sh2, out var item2, out _, out _, out _);
        Eq(e2.GoldProduced(sh2.Id, 0, item2), Math.Min(5, printed + 2), "two Plantations: one more each");
        var e3 = Position(4, out var sh3, out var item3, out _, out _, out _);
        Eq(e3.GoldProduced(sh3.Id, 0, item3), 5, "to a maximum of five");
        // A Stronghold already at five or more gains nothing.
        var s4 = Action(0); s4.Cards[s4.Players[0].StrongholdId].DefinitionId = "training-crane-stronghold"; Add(s4, Plantation, 0, Zone.Home);
        var host4 = Add(s4, "training-crane-samurai", 0, Zone.Home); var w4 = Add(s4, "training-costly-weapon", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Eq(e4.GoldProduced(s4.Players[0].StrongholdId, 0, new PaymentSubject(w4, PaymentKind.Equip, null, host4)), Math.Min(5, 4 + 1), "a Stronghold of four reaches five");
        // Another player's Plantation does not help you; a Plantation not in play does not either.
        var s5 = Action(0); s5.Cards[s5.Players[0].StrongholdId].DefinitionId = GoldenPlains; Add(s5, Plantation, 1, Zone.Home); Add(s5, Plantation, 0, Zone.FateDiscard);
        var host5 = Add(s5, "training-crane-samurai", 0, Zone.Home); var w5 = Add(s5, "training-costly-weapon", 0, Zone.Hand);
        Eq(MatchEngine.FromTestState(s5).GoldProduced(s5.Players[0].StrongholdId, 0, new PaymentSubject(w5, PaymentKind.Equip, null, host5)), printed, "only your own Plantations in play");
        // The real payment: with a Plantation the Stronghold alone pays for a 4 Gold attachment.
        Check(e1.PaymentOptions(0, printed + 1, item).Any(o => o.SourceIds.SequenceEqual([sh.Id])), "the Stronghold alone covers one more Gold");
        // Plantation's own production is the ordinary 2.
        var plantation = e1.State.Cards.Values.First(c => c.DefinitionId == Plantation);
        Eq(e1.GoldProduced(plantation.Id, 0), 2, "Produce 2 Gold");
    }

    // ---- Sacred Temples, Den of Iniquity ------------------------------------------------------------------------------------------------------------------------------------------

    private static void ProtectiveHoldings()
    {
        // Sacred Temples: another player's ACTION does not discard from your hand; a card effect that is not an action does (Pillowbook is the broader card), and your own discards are your own.
        var s = Action(0); var temples = Add(s, SacredTemples, 0, Zone.Home); Add(s, SacredTemples, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(temples.Id, 0), 2, "Produce 2 Gold");
        Check(e.HandDiscardBlockedForTest(0, 1, byAction: true), "another player's action will not discard from your hand");
        Check(!e.HandDiscardBlockedForTest(0, 1, byAction: false), "a card effect that is not an action still can");
        Check(!e.HandDiscardBlockedForTest(0, 0, byAction: true), "your own action is not 'another player's'");
        Check(!e.HandDiscardBlockedForTest(1, 0, byAction: true), "the Temples protect their own controller's hand only");
        Check(e.State.Events.Any(x => x.Type == "handDiscardPrevented"), "the prevention is logged");
        var bowed = Edit(e, st => st.Cards[temples.Id].Bowed = true);
        Check(bowed.HandDiscardBlockedForTest(0, 1, byAction: true), "bowed or not, the trait holds");
        var gone = Edit(e, st => st.Cards[temples.Id].Zone = Zone.DynastyDiscard);
        Check(!gone.HandDiscardBlockedForTest(0, 1, byAction: true), "out of play it does nothing");

        // Den of Iniquity: it will not be destroyed.
        var d = Action(0); var den = Add(d, Den, 0, Zone.Home); var other = Add(d, "training-marketplace", 0, Zone.Home);
        var de = MatchEngine.FromTestState(d);
        Eq(de.GoldProduced(den.Id, 0), 2, "Produce 2 Gold");
        Eq(de.DestroyUnitsForTest(den.Id), 0, "its destruction fails");
        Eq(de.State.Cards[den.Id].Zone, Zone.Home, "it is still in play");
        Check(de.State.Events.Any(x => x.Type == "destructionFailed"), "and says so");
        Eq(de.DestroyUnitsForTest(other.Id), 1, "another Holding is destroyed as usual");
        // ...and shields from Honor loss from its controller's Spells: "will not lose", no negation window.
        var h = Action(0); Add(h, Den, 0, Zone.Home);
        var host = Add(h, "training-crane-samurai", 0, Zone.Home); var mySpell = Add(h, "training-spell", 0, Zone.Attached, parent: host.Id);
        var foeHost = Add(h, "training-lion-samurai", 1, Zone.Home); var foeSpell = Add(h, "training-spell", 1, Zone.Attached, parent: foeHost.Id);
        var item = Add(h, "training-costly-weapon", 0, Zone.Attached, parent: host.Id);
        var he = MatchEngine.FromTestState(h);
        var foeHonor = Honor(he, 1); var myHonor = Honor(he, 0);
        he.P4LoseHonorFromForTest(1, 2, mySpell.Id);
        Eq(Honor(he, 1), foeHonor, "another player does not lose Honor from your Spell");
        Check(he.State.Events.Any(x => x.Type == "honorLossPrevented"), "logged");
        he.P4LoseHonorFromForTest(1, 2, item.Id);
        Eq(Honor(he, 1), foeHonor - 2, "but does from your Item");
        he.P4LoseHonorFromForTest(0, 2, foeSpell.Id);
        Eq(Honor(he, 0), myHonor - 2, "you lose Honor from his Spell");
        he.P4LoseHonorFromForTest(0, 2, mySpell.Id);
        Eq(Honor(he, 0), myHonor - 4, "and from your own Spell: only 'other players' are shielded");
        var noDen = Action(0); var nh = Add(noDen, "training-crane-samurai", 0, Zone.Home); var ns = Add(noDen, "training-spell", 0, Zone.Attached, parent: nh.Id);
        var ne = MatchEngine.FromTestState(noDen); var nHonor = Honor(ne, 1);
        ne.P4LoseHonorFromForTest(1, 2, ns.Id);
        Eq(Honor(ne, 1), nHonor - 2, "without a Den the loss happens");

        // Political Limited: bow it and target another player's dishonorable Personality.
        var p = Action(0); var parlor = Add(p, Den, 0, Zone.Home);
        var lowHonor = Add(p, "training-lion-samurai", 1, Zone.Home); lowHonor.Dishonorable = true;           // base Personal Honor 2
        var highHonor = Add(p, "training-lion-dignitary", 1, Zone.Home); highHonor.Dishonorable = true;       // base Personal Honor 4
        var honorable = Add(p, "training-lion-courtier", 1, Zone.Home);
        var mine = Add(p, "training-crane-samurai", 0, Zone.Home); mine.Dishonorable = true;
        var pe = MatchEngine.FromTestState(p);
        var option = Enabled(pe, 0, "pe-den-of-iniquity-shame", parlor.Id);
        Check(option.TargetIds!.Contains(lowHonor.Id) && option.TargetIds.Contains(highHonor.Id), "another player's dishonorable Personalities");
        Check(!option.TargetIds.Contains(honorable.Id) && !option.TargetIds.Contains(mine.Id), "not an honorable one, not your own");
        var version = pe.Version;
        Rejection(pe, Use(pe, 0, "pe-den-of-iniquity-shame", parlor.Id, honorable.Id));
        Rejection(pe, Use(pe, 0, "pe-den-of-iniquity-shame", parlor.Id, mine.Id));
        Check(pe.Version == version && !pe.State.Cards[parlor.Id].Bowed && !pe.State.Players[0].MayNotAttackThisTurn, "illegal targets change nothing");
        var before = Honor(pe, 1);
        Accept(pe, Offer(pe, 0, "pe-den-of-iniquity-shame", parlor.Id, highHonor.Id));
        Eq(Honor(pe, 1), before - 3, "Honor equal to his base Personal Honor (4), or 3, whichever is lower");
        Check(pe.State.Cards[parlor.Id].Bowed, "bowing is the cost");
        Check(pe.State.Players[0].MayNotAttackThisTurn, "you may not declare an attack this turn");
        var q = Action(0); var parlor2 = Add(q, Den, 0, Zone.Home); var low2 = Add(q, "training-lion-samurai", 1, Zone.Home); low2.Dishonorable = true;
        var qe = MatchEngine.FromTestState(q); var qBefore = Honor(qe, 1);
        Accept(qe, Offer(qe, 0, "pe-den-of-iniquity-shame", parlor2.Id, low2.Id));
        Eq(Honor(qe, 1), qBefore - 2, "a base Personal Honor of 2 costs 2");
    }

    // ---- Ambush Pass -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AmbushPassForce()
    {
        var s = Action(0);
        var mineRonin = Add(s, Ronin, 0, Zone.Home); var other = Add(s, "training-crane-samurai", 0, Zone.Home); var foeRonin = Add(s, Ronin, 1, Zone.Home);
        var e0 = MatchEngine.FromTestState(s);
        var force = (mine: e0.CardForceForTest(mineRonin.Id), other: e0.CardForceForTest(other.Id), foe: e0.CardForceForTest(foeRonin.Id));
        var region = PutRegion(s, AmbushPass, 0, 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(mineRonin.Id), force.mine + 1, "your Ronin have +1F");
        Eq(e.CardForceForTest(other.Id), force.other, "a Personality who is not a Ronin does not");
        Eq(e.CardForceForTest(foeRonin.Id), force.foe, "your opponent's Ronin do not");
        // It stays while the Region does: destroyed, the bonus is gone.
        var gone = Edit(e, st => { st.Cards[region.Id].Zone = Zone.DynastyDiscard; st.Players[0].Provinces[1].RegionId = null; });
        Eq(gone.CardForceForTest(mineRonin.Id), force.mine, "no Region, no bonus");
        // A second copy cannot enter (Singular) -- the engine's own entry rule for Singular cards.
        Check(CardCatalog.Get(AmbushPass).Keywords!.Contains("Singular"), "printed Singular");
    }

    // ---- Border Market -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BorderMarketAttach_()
    {
        var s = Action(0);
        var market = PutRegion(s, BorderMarket, 0, 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.FateDiscard); var item = Add(s, "training-costly-weapon", 0, Zone.FateDiscard);
        var unique = Add(s, "training-unique-spell", 0, Zone.FateDiscard); // a Spell: not a Follower or Item
        var theirs = Add(s, "training-follower", 1, Zone.FateDiscard);
        var uniqueItem = Add(s, "training-costly-weapon", 0, Zone.FateDiscard); Kw(uniqueItem, "Unique");
        var markets = new[] { Add(s, "training-marketplace", 0, Zone.Home), Add(s, "training-marketplace", 0, Zone.Home) };
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-border-market-attach", market.Id, performer.Id);
        Check(option.TargetIds!.Contains(follower.Id) && option.TargetIds.Contains(item.Id), "a Follower or Item in your discard pile");
        Check(!option.TargetIds.Contains(unique.Id) && !option.TargetIds.Contains(theirs.Id) && !option.TargetIds.Contains(uniqueItem.Id), "not a Spell, not another player's pile, not a Unique card");
        var cost = e.PaymentOptions(0, 2).First().SourceIds;
        var command = Offer(e, 0, "pe-border-market-attach", market.Id, follower.Id, performer.Id) with { SourceIds = cost };
        var version = e.Version;
        Rejection(e, command with { TargetId = theirs.Id }); Rejection(e, command with { TargetId = uniqueItem.Id });
        Rejection(e, command with { SourceIds = [] });
        Check(e.Version == version && !e.State.Cards[market.Id].Bowed && e.State.Cards[follower.Id].Zone == Zone.FateDiscard, "refused, nothing changes: unpaid, another player's, Unique");
        Accept(e, command);
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower is attached"); Eq(e.State.Cards[follower.Id].AttachedTo, performer.Id);
        Check(e.State.Cards[market.Id].Bowed, "bowing the Region is a cost");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.CardId == follower.Id), "all costs were paid through the Gold flow");
        Check(cost.Length > 0 && cost.All(id => e.State.Cards[id].Bowed), "by bowing the Gold sources it named: " + string.Join(",", cost));
        _ = markets;
        Check(!CanUse(e, 0, "pe-border-market-attach", market.Id, performer.Id), "a bowed Region cannot be used again");
        // An Open action: not in the Dynasty Phase.
        var s2 = Action(0); s2.Phase = Phase.Dynasty; var m2 = PutRegion(s2, BorderMarket, 0, 0); Add(s2, "training-crane-samurai", 0, Zone.Home); Add(s2, "training-follower", 0, Zone.FateDiscard);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-border-market-attach", m2.Id), "Open timing is the Action Phase");
    }

    // ---- Interesting Plains ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState DynastyPosition(string provinceCardDef, out CardInstance entering, int provinceIndex = 0)
    {
        var s = Action(0); s.Phase = Phase.Dynasty;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        entering = s.Cards[s.Players[0].Provinces[provinceIndex].CardId!]; entering.DefinitionId = provinceCardDef; entering.FaceUp = true;
        return s;
    }

    private static void Buy(MatchEngine e, CardInstance card) =>
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(0, e.QuotePurchaseForTest(0, card.Id), new PaymentSubject(e.State.Cards[card.Id], PaymentKind.Recruit)).First().SourceIds));

    private static void InterestingPlainsRefill()
    {
        var s = DynastyPosition("training-crane-samurai", out var entering);
        var plains = PutRegion(s, InterestingPlains, 0, 0);
        var e = MatchEngine.FromTestState(s);
        Buy(e, entering);
        Check(e.State.PendingChoice?.Type is "proclaim" or "entryReaction", "a window opened for the Personality who entered");
        var province = e.State.Players[0].Provinces[0];
        Check(province.CardId is not null && !e.State.Cards[province.CardId].FaceUp, "the province refilled face-down");
        var refilled = province.CardId!;
        Accept(e, Enabled(e, 0, "pe-interesting-plains-refill", plains.Id).Command!);
        Check(e.State.Cards[refilled].FaceUp, "after the Reaction the province's card is face-up");
        Check(e.State.Events.Any(x => x.Type == "provinceRefilled" && x.Message.Contains("face up")), "announced");
        // Optional: the player may pass.
        var s2 = DynastyPosition("training-crane-samurai", out var entering2); PutRegion(s2, InterestingPlains, 0, 0);
        var e2 = MatchEngine.FromTestState(s2); Buy(e2, entering2);
        var refilled2 = e2.State.Players[0].Provinces[0].CardId!;
        Accept(e2, new GameCommand("declineReaction", 0, e2.Version));
        for (var guard = 0; guard < 6 && e2.State.PendingChoice is not null; guard++) Accept(e2, new GameCommand("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Check(!e2.State.Cards[refilled2].FaceUp, "passed: the province stays face-down");
        // A Personality that entered from another province does not qualify (the Region is on province 0).
        var s3 = DynastyPosition("training-crane-samurai", out var entering3, provinceIndex: 1); var plains3 = PutRegion(s3, InterestingPlains, 0, 0);
        var e3 = MatchEngine.FromTestState(s3); Buy(e3, entering3);
        Check(!CanUse(e3, 0, "pe-interesting-plains-refill", plains3.Id), "not from this province: no reaction");
        Check(Options(e3, 0, "pe-interesting-plains-refill", plains3.Id).All(o => o.Reason?.Contains("did not enter play from this province") == true), "and says why");
        // A Holding entering play is not a Personality.
        var s4 = DynastyPosition("training-marketplace", out var holding); var plains4 = PutRegion(s4, InterestingPlains, 0, 0);
        var e4 = MatchEngine.FromTestState(s4); Buy(e4, holding);
        Check(!CanUse(e4, 0, "pe-interesting-plains-refill", plains4.Id), "a Holding is not a Personality");
    }

    // ---- Quicksand ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void QuicksandDestroy_()
    {
        MatchState Position(out CardInstance quicksand, out CardInstance bare, out CardInstance withFollower, out CardInstance follower, out CardInstance away, int regionProvince = 0)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1; s.TurnNumber = 3;
            quicksand = PutRegion(s, Quicksand, 1, regionProvince); // the Defender's province
            var defender = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            withFollower = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: withFollower.Id);
            away = Add(s, "training-crane-samurai", 0, Zone.Home);
            _ = defender;
            return s;
        }
        var s1 = Position(out var sand, out var bare, out var loaded, out var follower, out var away);
        var e = MatchEngine.FromTestState(s1);
        var option = Enabled(e, 1, "pe-quicksand-destroy", sand.Id);
        Check(option.TargetIds!.Contains(bare.Id) && option.TargetIds.Contains(follower.Id), "a Personality without Followers and a Follower at the battlefield");
        Check(!option.TargetIds.Contains(loaded.Id) && !option.TargetIds.Contains(away.Id), "not a Personality with Followers, not one elsewhere");
        var version = e.Version;
        Rejection(e, Use(e, 1, "pe-quicksand-destroy", sand.Id, loaded.Id)); Rejection(e, Use(e, 1, "pe-quicksand-destroy", sand.Id, away.Id));
        Check(e.Version == version && e.State.Cards[sand.Id].Zone == Zone.Region, "illegal targets change nothing: the Region is not destroyed");
        Accept(e, Offer(e, 1, "pe-quicksand-destroy", sand.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Zone == Zone.Dead, "the Personality is destroyed");
        Check(e.State.Cards[sand.Id].Zone is not Zone.Region && e.State.Players[1].Provinces[0].RegionId is null, "the Region is destroyed as the cost");
        // A Follower is destroyed alone; its Personality lives.
        var s2 = Position(out var sand2, out _, out var host2, out var follower2, out _);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "pe-quicksand-destroy", sand2.Id, follower2.Id));
        Check(e2.State.Cards[follower2.Id].Zone == Zone.FateDiscard && e2.State.Cards[host2.Id].Zone == Zone.Battlefield, "the Follower alone");
        // The Region is on another province: this battlefield is not its province's.
        var s3 = Position(out var sand3, out _, out _, out _, out _, regionProvince: 2);
        Check(!CanUse(MatchEngine.FromTestState(s3), 1, "pe-quicksand-destroy", sand3.Id), "this is not the Region's province's battlefield");
        // A Battle action: not in the Action Phase.
        var s4 = Action(1); var sand4 = PutRegion(s4, Quicksand, 1, 0); Add(s4, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s4), 1, "pe-quicksand-destroy", sand4.Id), "not outside a battle");
    }

    // ---- Renewed Lands -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RenewedLandsFarm()
    {
        var s = DynastyPosition("training-farm", out var farm);
        PutRegion(s, RenewedLands, 0, 0);
        var e = MatchEngine.FromTestState(s);
        var baseProduction = e.QuotePurchaseForTest(0, farm.Id) >= 0 ? CardCatalog.Get("training-farm").GoldProduction : 0;
        Buy(e, farm);
        var entered = e.State.Cards[farm.Id];
        Eq(entered.Zone, Zone.Home, "the Farm entered play");
        var token = entered.Tokens.Single();
        Check(token.Type == "Gold Production" && token.Stat == "GoldProduction" && token.Amount == 1, "a +1 Gold Production token");
        Eq(e.GoldProduced(farm.Id, 0), baseProduction + 1, "it produces one more Gold");
        var refilled = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(refilled.Id != farm.Id && refilled.Zone == Zone.Province && refilled.FaceUp, "the province refilled face-up");
        // A Holding that is not a Farm: nothing, and an ordinary face-down refill.
        var s2 = DynastyPosition("training-marketplace", out var market); PutRegion(s2, RenewedLands, 0, 0);
        var e2 = MatchEngine.FromTestState(s2); Buy(e2, market);
        Check(e2.State.Cards[market.Id].Tokens.Count == 0 && !e2.State.Cards[e2.State.Players[0].Provinces[0].CardId!].FaceUp, "another Holding gets no token and refills face-down");
        // A Farm from a province without the Region.
        var s3 = DynastyPosition("training-farm", out var farm3, provinceIndex: 1); PutRegion(s3, RenewedLands, 0, 0);
        var e3 = MatchEngine.FromTestState(s3); Buy(e3, farm3);
        Check(e3.State.Cards[farm3.Id].Tokens.Count == 0 && !e3.State.Cards[e3.State.Players[0].Provinces[1].CardId!].FaceUp, "not from this province: nothing");
        // No Region at all.
        var s4 = DynastyPosition("training-farm", out var farm4);
        var e4 = MatchEngine.FromTestState(s4); Buy(e4, farm4);
        Check(e4.State.Cards[farm4.Id].Tokens.Count == 0, "without the Region nothing happens");
    }
}
