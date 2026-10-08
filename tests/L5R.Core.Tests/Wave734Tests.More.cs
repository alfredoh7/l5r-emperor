using L5R.Core;
using static RegressionAssertions;

// Issue #734, wave batch 3: the second half of the suite (see Wave734Tests.cs).
internal static partial class Wave734Tests
{
    private static MatchState Selection()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }

    private static int Madness(MatchEngine e, CardInstance card) => e.State.Cards[card.Id].Tokens.Count(x => x.Type == "Madness");

    private static void ArmorTerms()
    {
        var s = Battle(); s.PriorityPlayerId = 0;
        var wielder = Bf(s, Crane, 0);
        var armor = Add(s, Armor, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var enemyA = Bf(s, Lion, 1); var enemyB = Bf(s, Lion, 1); var enemyC = Bf(s, Lion, 1);
        var e = Engine(s);
        Eq(e.PersonalHonorForTest(wielder.Id), 0, "a maximum Personal Honor of 0");
        var bare = Battle(); var free = Bf(bare, Crane, 0);
        Eq(Engine(bare).PersonalHonorForTest(free.Id), CardCatalog.Get(Crane).PersonalHonor, "without the Armor, the printed Personal Honor");
        Check(CardCatalog.Get(Armor).AttachGrantsKeyword == "Fallen", "the Armor gives Fallen");
        // Three targets are refused with nothing changed; two take a token each.
        var three = Batch336Support.Use(e, 0, "w734-armor-madness", armor.Id, null, null, Batch336Support.Pick("targets", enemyA.Id, enemyB.Id, enemyC.Id));
        var before = System.Text.Json.JsonSerializer.Serialize(e.State);
        Check(!e.Execute(three).Accepted, "three targets are refused");
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == before, "and nothing changes");
        Accept(e, Batch336Support.Use(e, 0, "w734-armor-madness", armor.Id, null, null, Batch336Support.Pick("targets", enemyA.Id, enemyB.Id)));
        Check(e.State.Cards[armor.Id].Bowed, "the Armor bowed to pay");
        Eq(Madness(e, enemyA), 1, "a Madness token on the first");
        Eq(Madness(e, enemyB), 1, "and the second");
        Eq(Madness(e, enemyC), 0, "but not the third");
        // One target is enough.
        var one = Battle(); one.PriorityPlayerId = 0;
        var w1 = Bf(one, Crane, 0); var a1 = Add(one, Armor, 0, Zone.Attached, province: 0, parent: w1.Id); var t1 = Bf(one, Lion, 1);
        var oe = Engine(one);
        Accept(oe, Batch336Support.Use(oe, 0, "w734-armor-madness", a1.Id, null, null, Batch336Support.Pick("targets", t1.Id)));
        Eq(Madness(oe, t1), 1, "one target takes one token");
        // Only enemy cards.
        var own = Battle(); own.PriorityPlayerId = 0;
        var w2 = Bf(own, Crane, 0); var a2 = Add(own, Armor, 0, Zone.Attached, province: 0, parent: w2.Id); var friend = Bf(own, Crane, 0);
        var ownEngine = Engine(own);
        Check(!ownEngine.Execute(Batch336Support.Use(ownEngine, 0, "w734-armor-madness", a2.Id, null, null, Batch336Support.Pick("targets", friend.Id))).Accepted, "an own card is not a target");
    }

    private static void JiYunTerms()
    {
        var s = Battle();
        var paragon = Bf(s, Crane, 0);
        paragon.Printed = CardCatalog.Get(Crane) with { Id = "w734-paragon", Keywords = ["Paragon"] };
        paragon.Dishonorable = true;
        var plain = Bf(s, Crane, 0);
        plain.Dishonorable = true;
        var enemyParagon = Bf(s, Lion, 1);
        enemyParagon.Printed = CardCatalog.Get(Lion) with { Id = "w734-paragon2", Keywords = ["Paragon"] };
        enemyParagon.Dishonorable = true;
        Bf(s, JiYun, 0, 2);
        var e = Engine(s);
        Eq(e.PersonalHonorForTest(paragon.Id), CardCatalog.Get(Crane).PersonalHonor, "a dishonorable Paragon of hers keeps his Personal Honor");
        Eq(e.PersonalHonorForTest(plain.Id), 0, "a dishonorable Personality who is not a Paragon has 0");
        Eq(e.PersonalHonorForTest(enemyParagon.Id), 0, "and the other player's Paragon too");
        var without = Battle(); var p2 = Bf(without, Crane, 0); p2.Printed = CardCatalog.Get(Crane) with { Id = "w734-paragon3", Keywords = ["Paragon"] }; p2.Dishonorable = true;
        Eq(Engine(without).PersonalHonorForTest(p2.Id), 0, "without Ji-Yun, 0");
        // After engaging at her battlefield, an opposing Personality may not use his abilities (here: perform actions) this turn.
        var g = Selection();
        var jiyun = Add(g, JiYun, 0, Zone.Battlefield, province: 0);
        var foe = Add(g, Lion, 1, Zone.Battlefield, province: 0);
        var other = Add(g, Lion, 1, Zone.Battlefield, province: 1);
        var ge = Engine(g);
        Accept(ge, ge.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Eq(ge.State.PendingChoice?.Type, "engageReaction", "the Engage window");
        var offers = Batch336Support.Options(ge, 0, "w734-jiyun-lock", jiyun.Id).Where(o => o.Enabled).ToArray();
        Check(offers.Length == 1 && offers[0].TargetIds!.Contains(foe.Id) && !offers[0].TargetIds!.Contains(other.Id) && !offers[0].TargetIds!.Contains(jiyun.Id), "the opposing Personality, and only he, may be targeted");
        Accept(ge, Batch336Support.Offer(ge, 0, "w734-jiyun-lock", jiyun.Id, foe.Id));
        Check(ge.State.Cards[foe.Id].ActionLocks.Count == 1, "he may not use his abilities");
    }

    private static void NitoshiEntry()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = s.Cards[s.Players[0].Provinces[0].CardId!]; card.DefinitionId = Nitoshi; card.FaceUp = true;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = Engine(s);
        var honor0 = e.State.Players[0].Honor; var honor1 = e.State.Players[1].Honor;
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        Eq(e.State.Players[0].Honor, honor0 - 3, "the controller lost 3 Honor");
        Eq(e.State.Players[1].Honor, honor1 - 3, "and so did the other player");
    }

    private static void HanteiTerms()
    {
        // The Honor Requirement (10) is ignored while the Favor is controlled.
        foreach (var (favor, allowed) in new[] { (true, true), (false, false) })
        {
            var s = Batch336Support.Table(Phase.Dynasty, 0);
            var province = s.Cards[s.Players[0].Provinces[0].CardId!]; province.DefinitionId = Hantei; province.FaceUp = true;
            s.Players[0].Honor = 5; s.FavorPlayerId = favor ? 0 : 1;
            var quote = Engine(s).QuotePurchase(0, province.Id);
            Check(allowed ? quote.Error is null : quote.Error is not null, $"Favor {favor}: {(allowed ? "may be recruited" : "refused")} ({quote.Error})");
        }
        // After he enters play: search the discard pile or Fate deck for a non-Unique Political Strategy.
        var g = MatchEngine.CreateEconomyScenario().State;
        var card = g.Cards[g.Players[0].Provinces[0].CardId!]; card.DefinitionId = Hantei; card.FaceUp = true; g.FavorPlayerId = 0; g.Players[0].Honor = 5;
        for (var i = 0; i < 6; i++) Add(g, "training-marketplace", 0, Zone.Home);
        var political = Add(g, Alliance, 0, Zone.FateDiscard);
        var e = Engine(g);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Check(e.State.PendingChoice?.Type is "entryReaction", $"the entering-play window is open ({e.State.PendingChoice?.Type})");
        Accept(e, Batch336Support.Use(e, 0, "w734-hantei-search", card.Id));
        Check(e.State.PendingSearch is not null, "the search opens");
        for (var guard = 0; guard < 4 && e.State.PendingSearch is not null; guard++)
        {
            var types = string.Join(", ", e.QueryActions(0).Where(o => o.Enabled).Select(o => o.Type + ":" + o.Command?.Choice));
            var pick = e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "chooseSearchDeck", Choice: "FateDiscard" })
                ?? e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "selectSearchCard" });
            Check(pick is not null, "a search choice is offered: " + types);
            Accept(e, pick!.Command!.Type == "selectSearchCard" ? pick.Command with { Choice = Alliance } : pick.Command);
        }
        Check(e.State.Cards[political.Id].Zone == Zone.Hand, "the Political Strategy is in the hand");
    }

    private static void ChizuruTerms()
    {
        foreach (var honorBonus in new[] { false, true })
        {
            var s = Batch336Support.Table(Phase.Action, 0);
            var chizuru = Bf(s, Chizuru, 0);
            var target = Bf(s, Crane, 0);
            var discard = Add(s, Alliance, 0, Zone.Hand); // Focus Value 3
            var focus = CardCatalog.Get(Alliance).Focus;
            var e = Engine(s);
            var ability = honorBonus ? "w734-chizuru-honor" : "w734-chizuru-force";
            var baseForce = Force(e, target); var baseHonor = e.PersonalHonorForTest(target.Id);
            Accept(e, Batch336Support.Use(e, 0, ability, chizuru.Id, null, null, Batch336Support.Pick("target", target.Id), Batch336Support.Pick("discard", discard.Id)));
            Check(e.State.Cards[discard.Id].Zone == Zone.FateDiscard, "the card was discarded as the cost");
            if (!honorBonus) Eq(Force(e, target), baseForce + focus, "Force bonus equal to the Focus Value");
            else
            {
                Eq(e.PersonalHonorForTest(target.Id), Math.Min(4, baseHonor + focus), "Personal Honor bonus, capped at 4");
                Eq(Force(e, target), baseForce, "Force unchanged");
            }
        }
        var big = Batch336Support.Table(Phase.Action, 0);
        var c2 = Bf(big, Chizuru, 0); var t2 = Bf(big, Crane, 0);
        t2.Modifiers.Add(new("test-source", "test", "PersonalHonor", 6));
        var d2 = Add(big, Alliance, 0, Zone.Hand);
        var be = Engine(big);
        Eq(be.PersonalHonorForTest(t2.Id), CardCatalog.Get(Crane).PersonalHonor + 6, "before: uncapped");
        Accept(be, Batch336Support.Use(be, 0, "w734-chizuru-force", c2.Id, null, null, Batch336Support.Pick("target", t2.Id), Batch336Support.Pick("discard", d2.Id)));
        Eq(be.PersonalHonorForTest(t2.Id), 4, "after: a maximum Personal Honor of 4");
        var later = be.State; later.TurnNumber++;
        Eq(MatchEngine.FromTestState(later).PersonalHonorForTest(t2.Id), CardCatalog.Get(Crane).PersonalHonor + 6, "and the ceiling lapses with the turn");
    }

    private static MatchEngine ToBattle(MatchEngine e)
    {
        var st = e.State;
        st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0];
        return MatchEngine.FromTestState(st);
    }

    private static void ReadyTerms()
    {
        foreach (var tactician in new[] { true, false })
        {
            var s = Batch336Support.Table(Phase.Action, 0);
            var performer = Add(s, Crane, 0, Zone.Home);
            performer.Printed = CardCatalog.Get(Crane) with { Id = "w734-tactician", Keywords = tactician ? ["Tactician"] : [] };
            var mine = Bf(s, Crane, 0); var theirs = Bf(s, Lion, 1);
            var theirsFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: theirs.Id);
            var elsewhere = Bf(s, Lion, 1, 1);
            var ready = Add(s, Ready, 0, Zone.Hand);
            var e = Engine(s);
            var options = Batch336Support.Options(e, 0, "w734-ready-defenses", ready.Id);
            var option = options.FirstOrDefault(x => x.Enabled && x.Label.Contains(performer.Id));
            Check(option is not null, "offered: " + string.Join("; ", options.Select(x => $"{x.Label} [{x.Enabled}] {x.Reason}")) + " / " + string.Join("; ", e.QueryActions(0).Select(x => x.Label)));
            // The province named is the Defender's (player 1 is the Defender once the Battle phase comes; here player 0's own province 0 is the one that will be defended).
            var before = ToBattle(Engine(e.State));
            var m = Force(before, mine); var t = Force(before, theirs); var f = Force(before, theirsFollower); var o = Force(before, elsewhere);
            Accept(e, option!.Command! with { TargetPlayerId = 0, Province = 0 });
            var battle = e.State; battle.Phase = Phase.Battle; battle.ActivePlayerId = 1; battle.PriorityPlayerId = 1; battle.CurrentBattleProvince = 0; battle.UnresolvedBattlefields = [0];
            var after = MatchEngine.FromTestState(battle);
            Eq(Force(after, theirs), t - 1, "the other player's Personality -1F");
            Eq(Force(after, theirsFollower), f - 1, "and his Follower");
            Eq(Force(after, mine), m + (tactician ? 1 : 0), tactician ? "the Tactician's player +1F" : "no Tactician: no bonus");
            Eq(Force(after, elsewhere), o, "another battlefield is untouched");
        }
    }

    private static void AllianceTerms()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var courtier = Add(s, Crane, 0, Zone.Home);
        courtier.Printed = CardCatalog.Get(Crane) with { Id = "w734-courtier", Keywords = ["Courtier"] };
        var notCourtier = Add(s, Crane, 0, Zone.Home);
        var a = Bf(s, Crane, 0);
        var lion1 = Bf(s, Lion, 1); var lion2 = Bf(s, Lion, 1);
        var home = Add(s, Lion, 1, Zone.Home);
        var alliance = Add(s, Alliance, 0, Zone.Hand);
        var e = Engine(s);
        var options = Batch336Support.Options(e, 0, "w734-alliance-bow", alliance.Id).Where(o => o.Enabled).ToArray();
        Check(options.Length > 0 && options.All(o => o.TargetIds!.Contains(courtier.Id) && !o.TargetIds!.Contains(notCourtier.Id)),
            "only an unbowed Courtier may be targeted: " + string.Join("; ", Batch336Support.Options(e, 0, "w734-alliance-bow", alliance.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason} {string.Join(",", o.TargetIds ?? [])}")));
        var before = ToBattle(Engine(e.State));
        var forceA = Force(before, a); var forceL = Force(before, lion1); var forceH = Force(before, home);
        Accept(e, Batch336Support.Offer(e, 0, "w734-alliance-bow", alliance.Id, courtier.Id));
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed");
        var after = ToBattle(e);
        Eq(Force(after, a), forceA - 1, "the Crane army: one alignment, -1F");
        Eq(Force(after, lion1), forceL - 1, "the Lion army: one alignment, -1F");
        Eq(Force(after, lion2), CardCatalog.Get(Lion).Force - 1, "and each of its Personalities");
        Eq(Force(after, home), forceH, "a Personality at home is in no army");
    }

    private static bool Refusable(MatchEngine e, int player) => e.QueryActions(player).Any(o => o.Enabled && o.Type == "declineDuel");

    private static void HoturiDuel()
    {
        // Honorable target: refusable by dishonoring him and moving him home.
        var s = Battle(); s.PriorityPlayerId = 0;
        var hoturi = Bf(s, Hoturi, 0);
        var target = Bf(s, Lion, 1);
        var e = Engine(s);
        Accept(e, Batch336Support.Offer(e, 0, "w734-hoturi-duel", hoturi.Id, target.Id));
        Check(e.State.PendingDuel is not null, "a duel is issued");
        var refuse = e.QueryActions(1).FirstOrDefault(o => o.Enabled && o.Type == "declineDuel");
        Check(refuse is not null, "the honorable target may refuse (" + string.Join(", ", e.QueryActions(1).Select(o => o.Type)) + ")");
        Accept(e, refuse!.Command!);
        Check(e.State.Cards[target.Id].Dishonorable, "refusing dishonored him");
        Check(e.State.Cards[target.Id].Zone == Zone.Home, "and moved him home");
        Check(e.State.PendingDuel is null, "no duel");
        // A dishonorable target may not refuse.
        var d = Battle(); d.PriorityPlayerId = 0;
        var h2 = Bf(d, Hoturi, 0); var dishonored = Bf(d, Lion, 1); dishonored.Dishonorable = true;
        var de = Engine(d);
        Accept(de, Batch336Support.Offer(de, 0, "w734-hoturi-duel", h2.Id, dishonored.Id));
        Check(!Refusable(de, 1), "a dishonorable target cannot refuse");
    }

    private static void TsukeDuel()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var tsuke = Bf(s, Tsuke, 0);
        var target = Bf(s, Lion, 1);
        var e = Engine(s);
        Accept(e, Batch336Support.Offer(e, 0, "w734-tsuke-duel", tsuke.Id, target.Id));
        Check(e.State.Cards[tsuke.Id].Bowed, "Tsuke bowed to pay");
        Check(e.State.PendingDuel?.Rules?.MinFocusEach == 1, "both sides owe one focus");
        Check(!Refusable(e, 1), "a Fire duel of Tsuke's is not refusable");
    }

    private static void KarasuSwitch()
    {
        var s = Battle(); s.PriorityPlayerId = 0;
        var karasu = Add(s, Karasu, 0, Zone.Home);
        var away = Bf(s, Crane, 0, bowed: true);
        var home = Add(s, Crane, 0, Zone.Home, bowed: true);
        var e = Engine(s);
        Accept(e, Batch336Support.Use(e, 0, "w734-karasu-switch", karasu.Id, null, null, Batch336Support.Pick("first", away.Id), Batch336Support.Pick("second", home.Id)));
        Eq(e.State.Cards[away.Id].Zone, Zone.Home, "the Personality at the battlefield went home");
        Eq(e.State.Cards[home.Id].Zone, Zone.Battlefield, "and the one at home went to the battlefield");
        Check(!e.State.Cards[away.Id].Bowed && !e.State.Cards[home.Id].Bowed, "both straightened");
        // A Fallen Personality is switched but not straightened.
        var f = Battle(); f.PriorityPlayerId = 0;
        var k2 = Add(f, Karasu, 0, Zone.Home); var a2 = Bf(f, Crane, 0, bowed: true); var fl = Add(f, Crane, 0, Zone.Home, bowed: true);
        fl.Printed = CardCatalog.Get(Crane) with { Id = "w734-fallen4", Keywords = ["Fallen"] };
        var fe = Engine(f);
        Accept(fe, Batch336Support.Use(fe, 0, "w734-karasu-switch", k2.Id, null, null, Batch336Support.Pick("first", a2.Id), Batch336Support.Pick("second", fl.Id)));
        Check(!fe.State.Cards[a2.Id].Bowed && fe.State.Cards[fl.Id].Bowed, "only the one who is not Fallen is straightened");
    }

    private static void BarrierTerms()
    {
        foreach (var air in new[] { true, false })
        {
            var s = Battle(); s.PriorityPlayerId = 0;
            var shugenja = Bf(s, "training-shugenja", 0);
            shugenja.Printed = CardCatalog.Get("training-shugenja") with { Id = air ? "w734-air" : "w734-water", Keywords = air ? ["Shugenja", "Air"] : ["Shugenja", "Water"] };
            var spell = Add(s, Barrier, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            var attacker = Bf(s, Lion, 1);
            var e = Engine(s);
            var honor = Batch336Support.Honor(e, 0);
            Accept(e, Batch336Support.Offer(e, 0, "w734-barrier", spell.Id, attacker.Id));
            Check(e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Spell was destroyed to pay");
            Check(e.KeywordForTest(attacker.Id, "Elite"), "he has Elite");
            Eq(Batch336Support.Honor(e, 0), honor + (air ? 2 : 0), air ? "Air: gain 2 Honor" : "not Air: no Honor");
        }
    }
}
