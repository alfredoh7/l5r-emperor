using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards follow-up (issue #492), Mendon's rulings, group R5: clans (Ninja is a keyword, never a Clan), Shinjo Ki-Chang's trigger (tested in EmperorProxyYTests), He's Mine! and
// Irezu's substitution in an accepted duel, whole-unit bowing and straightening, Sasumata and Terrain, and the player's choice in an Attack's "may compare" clauses.
internal static class EmperorRulingR5Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    private const string HesMine = "oracle-l5r-3008", Irezu = "oracle-l5r-793", Fortitude = "oracle-l5r-2671", BarredPassage = "oracle-l5r-708", TrapIsSprung = "oracle-l5r-8394", SlowDeath = "oracle-l5r-8340",
        HardPressed = "oracle-l5r-2972", Sasumata = "oracle-l5r-6478", UnseenAssailant = "oracle-l5r-9011", TsuruchiGuard = "oracle-l5r-8837", Nobunori = "oracle-l5r-572";

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R5", [
        ("Clans: no published card has the Clan \"Ninja\" (there is no Ninja clan); every card that had it keeps Ninja as a keyword and has the Clan of its \"<Clan> Clan\" keyword (Goju, Ninube: Spider; Shosuro, Bayushi: Scorpion)", NinjaIsNotAClan),
        ("Clans: a Spider-clan Ninja such as Goju Oyoto has the Spider clan alignment and the Ninja keyword, and is not aligned with any Ninja clan", NinjaClanAlignment),
        ("He's Mine!: a refusable challenge that has not been accepted opens no substitution window (a crafted command is rejected); once accepted the Samurai may take the place; a refused challenge is no duel", HesMineRefusable),
        ("He's Mine!: the reactor may also replace the challenger of an accepted duel of his own", HesMineChallengerAndIrezu),
        ("Irezu: like He's Mine!, he enters only an accepted or mandatory duel -- not a refusable challenge still awaiting its answer", IrezuAcceptedDuelOnly),
        ("Units: Fortitude bows every card of the target unit (Personality, Follower, Spell) on its own -- a card whose bowing is prevented (Stalwart) stays unbowed, an already bowed card is left alone and prevention is not spent on it", FortitudeUnit),
        ("Units: Barred Passage's attachments bow one by one (a Stalwart Follower is not bowed); The Trap is Sprung! for a Crane player bows the unit card by card; The Slow Death straightens card by card (a locked Follower stays bowed)", UnitBowAndStraighten),
        ("Units: Hard Pressed's optional bow and Block Supply's bowed move home bow every card of the moved unit, each unless its own bowing is prevented", HardPressedAndBlockSupply),
        ("Conqueror: the chosen Conqueror's whole unit is straightened (each card unless its own straightening is prevented) and returns home unbowed; another unit returns home bowed", ConquerorStraightensWholeUnit),
        ("Terrain: a Terrain is never bowed (the property refuses it whichever path tries) and never straightened; a bow-only action cannot target one; other enemy cards remain targets", TerrainNeverBowed),
        ("Sasumata: at the battlefield only -- attachments themselves (Weapon, Armor, Follower, Spell) and Personalities without a Weapon or Armor; not a Personality carrying one, not a card away from the battlefield, not a Terrain or Holding", SasumataTargets),
        ("Attack may-compare: the choice rides on the command -- an invalid choice or one the target does not allow is refused, a command without one compares Force, and the choice survives an Attack held for reactions", CompareChoiceRules),
    ]);

    // ---- Clans ----------------------------------------------------------------------------------------------------------------------------------------
    private static void NinjaIsNotAClan()
    {
        var ninjaClan = CardCatalog.Cards.Values.Where(c => c.Clan == "Ninja").Select(c => c.Id).ToArray();
        Check(ninjaClan.Length == 0, "cards whose Clan is Ninja: " + string.Join(", ", ninjaClan));
        Check(CardCatalog.Cards.Values.All(c => (c.Clans ?? []).All(clan => clan != "Ninja")), "no card has a Ninja clan among its Clans");
        var expected = new (string Id, string Clan)[]
        {
            ("oracle-l5r-2855", "Spider"), ("oracle-l5r-5590", "Spider"), ("oracle-l5r-5591", "Spider"), ("oracle-l5r-2858", "Spider"), ("oracle-l5r-9711", "Spider"), ("oracle-l5r-2849", "Spider"), ("oracle-l5r-9850", "Spider"),
            ("oracle-l5r-7121", "Scorpion"), ("oracle-l5r-7179", "Scorpion"), ("oracle-l5r-10250", "Scorpion"), ("oracle-l5r-10226", "Scorpion"), ("oracle-l5r-790", "Scorpion"), ("oracle-l5r-840", "Scorpion"),
            ("oracle-l5r-850", "Scorpion"), ("oracle-l5r-9846", "Scorpion"), ("oracle-l5r-10653", "Scorpion"),
            ("oracle-l5r-10008", "Unaligned") // the Ninja Follower Proxy has no Clan of its own
        };
        foreach (var (id, clan) in expected)
        {
            var card = CardCatalog.Get(id);
            Eq(card.Clan, clan, $"{card.Name}'s Clan");
            if (id != "oracle-l5r-10008") Check((card.Keywords ?? []).Contains("Ninja") && (card.Keywords ?? []).Contains($"{clan} Clan"), $"{card.Name} keeps Ninja as a keyword beside {clan} Clan");
        }
        // Every published card keyword "<Clan> Clan" on a Personality agrees with its Clan field when that field is not Unaligned (the audit behind the fix).
        foreach (var card in CardCatalog.Cards.Values.Where(c => c.Type == CardType.Personality && c.Clan != "Unaligned" && (c.Keywords ?? []).Any(k => k.EndsWith(" Clan", StringComparison.Ordinal) && k != "Clan Champion")))
            Check((card.Keywords ?? []).Contains($"{card.Clan} Clan") || (card.Clans ?? []).Any(clan => (card.Keywords ?? []).Contains($"{clan} Clan")), $"{card.Name}: Clan {card.Clan} matches a Clan keyword");
    }

    private static void NinjaClanAlignment()
    {
        var s = Table(Phase.Action, 0);
        var oyoto = Add(s, "oracle-l5r-2855", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(oyoto.Id, "Ninja") && e.HasKeyword(oyoto.Id, "Spider Clan"), "Goju Oyoto is a Ninja and a Spider Clan Personality");
        Eq(CardCatalog.Get("oracle-l5r-2855").Clan, "Spider", "his Clan field");
        Check(e.HasClanAlignmentForTest(oyoto.Id, "Spider") && !e.HasClanAlignmentForTest(oyoto.Id, "Ninja"), "aligned with the Spider Clan, never with a Ninja clan");
        var hawado = Add(s, "oracle-l5r-10226", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Check(e2.HasClanAlignmentForTest(hawado.Id, "Scorpion") && !e2.HasClanAlignmentForTest(hawado.Id, "Ninja"), "Shosuro Hawado is Scorpion");
    }

    // ---- He's Mine! / Irezu ---------------------------------------------------------------------------------------------------------------------------
    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingDuel is not null; guard++)
        {
            var mover = e.State.PriorityPlayerId;
            var focus = e.QueryActions(mover).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type == "focus" && a.Command.Choice == "deck" || a.Type == "strike" || a.Type == "declineReaction"));
            Check(focus is not null, $"nothing to do for player {mover} ({e.State.PendingChoice?.Type} choice for {e.State.PendingChoice?.PlayerId}, priority {e.State.PriorityPlayerId}, stage {e.State.PendingDuel?.Stage}): " + string.Join("; ", e.QueryActions(mover).Select(a => $"{a.Type}[{a.Enabled}]")));
            Accept(e, focus!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel did not finish");
    }

    private static (MatchEngine E, CardInstance Card, CardInstance Victim, CardInstance Samurai, CardInstance Duelist, CardInstance Challenge) RefusableSetup()
    {
        var s = Table(Phase.Battle, 1);
        var victim = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
        var duelist = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var card = Add(s, HesMine, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-duel-challenge", challenge.Id, victim.Id, performerId: duelist.Id));
        return (e, card, victim, samurai, duelist, challenge);
    }

    private static void HesMineRefusable()
    {
        // Awaiting the answer: the challenged player may only accept or refuse; He's Mine! is not on offer and a crafted command is refused (refusal cannot be moved to a new target).
        var (e, card, victim, samurai, _, _) = RefusableSetup();
        Eq(e.State.PendingDuel?.Stage, "AwaitingAccept", "the refusable challenge awaits its answer");
        Check(e.State.PendingChoice is null, "no reaction window is open yet");
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility"), "no ability is offered while the challenge awaits its answer");
        var rejection = Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, TargetId: victim.Id, AbilityId: "hes-mine-substitute", PerformerId: samurai.Id));
        Check(rejection.Length > 0, "a crafted He's Mine! is refused before the challenge is accepted: " + rejection);
        Check(!e.State.Cards[victim.Id].Bowed && e.State.Cards[card.Id].Zone == Zone.Hand, "nothing was paid");
        Eq(e.State.PendingDuel?.ChallengedId, victim.Id, "the challenged Personality is unchanged, so only he may refuse");

        // Accepted: the before-the-duel window opens and the Samurai may take his place.
        Accept(e, e.QueryActions(0).First(a => a.Type == "acceptDuel").Command!);
        Eq(e.State.PendingChoice?.Type, "duelReaction", "accepting opens the window before the duel begins");
        var reaction = Enabled(e, 0, "hes-mine-substitute", card.Id, samurai.Id);
        Accept(e, reaction.Command!);
        Check(e.State.PendingDuel is { } duel && duel.ChallengedId == samurai.Id && e.State.Cards[victim.Id].Bowed, "the Samurai is in the duel and the Personality bowed");
        Check(e.State.PendingDuel!.Stage != "AwaitingAccept", "the duel is already accepted: nobody may refuse it any more");
        FinishDuel(e);
        Check(e.State.Cards[samurai.Id].Bowed, "the substitute bows after the duel ends");

        // Refused: no duel, no window, and He's Mine! stays in the hand.
        var (r, rcard, _, _, _, _) = RefusableSetup();
        Accept(r, r.QueryActions(0).First(a => a.Type == "declineDuel").Command!);
        Check(r.State.PendingDuel is null && r.State.PendingChoice is null, "a refused challenge is no duel");
        Eq(r.State.Cards[rcard.Id].Zone, Zone.Hand, "He's Mine! was not used");
        Check(!CanUse(r, 0, "hes-mine-substitute", rcard.Id), "it cannot be used after the refusal either");
    }

    private static void HesMineChallengerAndIrezu()
    {
        // The reactor's own duel: player 0 challenges player 1 with an unrefusable duel; player 0 (the challenger) may replace his own participant when the window reaches him.
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Samurai");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-force", 0, Zone.Hand);
        var card = Add(s, HesMine, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-duel-force", challenge.Id, enemy.Id, performerId: mine.Id));
        Eq(e.State.PendingChoice?.Type, "duelReaction", "the mandatory duel opens the window");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the challenged player has the first opportunity");
        Accept(e, new("declineReaction", 1, e.Version));
        Check(CanUse(e, 0, "hes-mine-substitute", card.Id, samurai.Id), "then the challenger may replace his own participant");
        Accept(e, Enabled(e, 0, "hes-mine-substitute", card.Id, samurai.Id).Command!);
        Check(e.State.PendingDuel is { } duel && duel.ChallengerId == samurai.Id && duel.ChallengedId == enemy.Id, "the Samurai now challenges");
        Check(e.State.Cards[mine.Id].Bowed, "the replaced Personality is bowed as the cost");

        // Never the other player's participant: the Lion player cannot use it on the Crane's duelist (he has none), and a Samurai of the wrong side is not a performer.
        var s2 = Table(Phase.Battle, 1);
        var v = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Kw(Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Samurai");
        var d = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ch = Add(s2, "training-duel-force", 1, Zone.Hand);
        var mine2 = Add(s2, HesMine, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 1, "training-duel-force", ch.Id, v.Id, performerId: d.Id));
        Accept(e2, new("declineReaction", 0, e2.Version));
        Check(CanUse(e2, 1, "hes-mine-substitute", mine2.Id), "the challenger's controller may use his own copy on his own participant");
        Check(!e2.QueryActions(0).Any(a => a.Label.StartsWith("Before your target Personality enters a duel", StringComparison.Ordinal)), "the other player is offered no He's Mine! of the first player's");
    }
    // ---- Units: whole-unit bow / straighten (ruling 4) ----------------------------------------------------------------------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Card, CardInstance Berserker, CardInstance Target, CardInstance Follower, CardInstance Spell) FortitudeSetup(Action<CardInstance, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Fortitude, 0, Zone.Hand);
        var berserker = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Berserker"); // base Force 5
        var target = Add(s, "training-crane-courtier", 1, Zone.Battlefield, province: 0);                 // Force 2
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id);   // +2: the unit's total Force is 4, below 5
        var spell = Add(s, "training-spell", 1, Zone.Attached, province: 0, parent: target.Id);
        tweak?.Invoke(follower, spell);
        return (MatchEngine.FromTestState(s), card, berserker, target, follower, spell);
    }

    private static void FortitudeUnit()
    {
        var (e, card, berserker, target, follower, spell) = FortitudeSetup();
        Accept(e, Offer(e, 0, "fortitude-bow", card.Id, target.Id, berserker.Id));
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[follower.Id].Bowed && e.State.Cards[spell.Id].Bowed, "the whole unit bowed: the Personality, his Follower and his Spell");

        // The Follower's own bowing is prevented (Stalwart): only that card stays unbowed, and the rest of the unit bows.
        var (e2, card2, berserker2, target2, follower2, spell2) = FortitudeSetup((f, _) => Kw(f, "Stalwart"));
        Accept(e2, Offer(e2, 0, "fortitude-bow", card2.Id, target2.Id, berserker2.Id));
        Check(e2.State.Cards[target2.Id].Bowed && e2.State.Cards[spell2.Id].Bowed && !e2.State.Cards[follower2.Id].Bowed, "the Stalwart Follower was not bowed, the others were");

        // An already bowed card is not "bowed" again: a prevention it carries is not spent on it.
        var (e3, card3, berserker3, target3, follower3, spell3) = FortitudeSetup((f, _) => { Kw(f, "Stalwart"); f.Bowed = true; });
        Accept(e3, Offer(e3, 0, "fortitude-bow", card3.Id, target3.Id, berserker3.Id));
        Check(e3.State.Cards[target3.Id].Bowed && e3.State.Cards[spell3.Id].Bowed && e3.State.Cards[follower3.Id].Bowed, "the unit is bowed, the already bowed Follower stays bowed");
        Check(!e3.State.Cards[follower3.Id].AbilityUses.ContainsKey("stalwart-negate"), "its Stalwart was not spent on a bowing that could not happen");
    }

    private static void UnitBowAndStraighten()
    {
        // Barred Passage: "Bow the Personality and every card attached to him" -- every card on its own, prevention honored per card.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var hero = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Hero");
        var card = Add(s, BarredPassage, 0, Zone.Hand);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: target.Id);
        var stalwart = Kw(Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id), "Stalwart");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "barred-passage-bow", card.Id, target.Id, hero.Id));
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[item.Id].Bowed, "the Personality and his Item bowed");
        Check(!e.State.Cards[stalwart.Id].Bowed, "the Stalwart Follower's bowing was prevented (before this ruling every attachment bowed unconditionally)");

        // The Trap is Sprung!, a Crane Clan player: the target and then the rest of its unit, card by card.
        var t = Table(Phase.Battle, 0);
        var trap = Add(t, TrapIsSprung, 0, Zone.Hand);
        var scout = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Scout");
        var enemy = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var followerA = Kw(Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id), "Stalwart");
        var spellB = Add(t, "training-spell", 1, Zone.Attached, province: 0, parent: enemy.Id);
        t.Players[1].Provinces[0].Reconnaissance[0] = true;
        var et = MatchEngine.FromTestState(t);
        Accept(et, Offer(et, 0, "trap-sprung-bow", trap.Id, enemy.Id, scout.Id));
        Check(et.State.Cards[enemy.Id].Bowed && et.State.Cards[spellB.Id].Bowed && !et.State.Cards[followerA.Id].Bowed, "the Personality and his Spell bowed, the Stalwart Follower did not");
        // Targeting the Stalwart Follower itself: its bowing is prevented and it is not bowed again through "its unit"; the rest of the unit bows.
        var t2 = Table(Phase.Battle, 0);
        var trap2 = Add(t2, TrapIsSprung, 0, Zone.Hand);
        var scout2 = Kw(Add(t2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Scout");
        var enemy2 = Add(t2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var followerC = Kw(Add(t2, "training-follower", 1, Zone.Attached, province: 0, parent: enemy2.Id), "Stalwart");
        t2.Players[1].Provinces[0].Reconnaissance[0] = true;
        var et2 = MatchEngine.FromTestState(t2);
        Accept(et2, Offer(et2, 0, "trap-sprung-bow", trap2.Id, followerC.Id, scout2.Id));
        Check(!et2.State.Cards[followerC.Id].Bowed && et2.State.Cards[enemy2.Id].Bowed, "the targeted Stalwart Follower stays unbowed (its first bowing is negated once), his Personality bows with the unit");

        // The Slow Death: straighten card by card; a Follower that may not be straightened this turn stays bowed.
        var u = Table(Phase.Battle, 0);
        var slow = Add(u, SlowDeath, 0, Zone.Hand);
        var mine = Add(u, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var mineFollower = Add(u, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: mine.Id);
        var mineSpell = Add(u, "training-spell", 0, Zone.Attached, bowed: true, province: 0, parent: mine.Id);
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        mineFollower.StraightenLockedTurn = u.TurnNumber;
        var eu = MatchEngine.FromTestState(u);
        Accept(eu, Offer(eu, 0, "slow-death-straighten", slow.Id, mine.Id));
        Check(!eu.State.Cards[mine.Id].Bowed && !eu.State.Cards[mineSpell.Id].Bowed, "the Personality and his Spell straightened");
        Check(eu.State.Cards[mineFollower.Id].Bowed, "the Follower that may not be straightened this turn stayed bowed");
    }

    private static void HardPressedAndBlockSupply()
    {
        // Hard Pressed: "Move a unit home. You may bow it" -- the unit (an enemy one, so a Stalwart card can prevent its own bowing).
        var s = Table(Phase.Battle, 1);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var lion = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var lionSpell = Add(s, "training-spell", 1, Zone.Attached, province: 0, parent: lion.Id);
        var lionFollower = Kw(Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: lion.Id), "Stalwart");
        Add(s, HardPressed, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Batch336Support.RunUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
        var bowOption = e.QueryActions(0).Single(o => o.Type == "beforeBattleOptional" && o.Command!.TargetId == lion.Id && o.Command.CardIds is [var same] && same == lion.Id);
        Accept(e, bowOption.Command!);
        Check(e.State.Cards[lion.Id].Zone == Zone.Home && e.State.Cards[lion.Id].Bowed && e.State.Cards[lionSpell.Id].Bowed, "the unit moved home and its Personality and Spell bowed");
        Check(!e.State.Cards[lionFollower.Id].Bowed, "the Follower whose bowing is prevented did not (Stalwart)");
        _ = crane;

        // Block Supply: "Move an attacking unit home bowed".
        var t = Table(Phase.Battle, 0);
        t.PriorityPlayerId = 1;
        var block = Add(t, "oracle-l5r-1045", 1, Zone.Hand);
        var attacker = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attackerSpell = Add(t, "training-spell", 0, Zone.Attached, province: 0, parent: attacker.Id);
        var attackerFollower = Kw(Add(t, "training-follower", 0, Zone.Attached, province: 0, parent: attacker.Id), "Stalwart");
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var et = MatchEngine.FromTestState(t);
        Accept(et, Batch336Support.Use(et, 1, "block-supply", block.Id, attacker.Id));
        Check(et.State.Cards[attacker.Id].Zone == Zone.Home && et.State.Cards[attacker.Id].Bowed && et.State.Cards[attackerSpell.Id].Bowed, "the attacking unit moved home bowed, card by card");
        Check(!et.State.Cards[attackerFollower.Id].Bowed, "the Stalwart Follower's bowing was prevented (it used to bow with the unit unconditionally)");
    }

    // ---- Terrain (ruling 5) -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TerrainNeverBowed()
    {
        var s = Table(Phase.Battle, 0);
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        terrain.Bowed = true;
        Check(!terrain.Bowed, "a Terrain refuses to be bowed, whichever path sets it");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.Cards[terrain.Id].Bowed && e.StraightenForTest(terrain.Id), "a copied state has it unbowed, and straightening it changes nothing");
        // A state that was saved bowed cannot bring a bowed Terrain back either (deserialising goes through the same property).
        var raw = e.State.Cards[terrain.Id];
        Check(!raw.Bowed, "the copy is unbowed");
        // A bow-only action naming "an enemy card" does not offer the Terrain, but the enemy Personality stays a target; a crafted command is refused.
        var ability = "training-bow-enemy-card-at-battle";
        var puppet = Add(s, "training-card-bower", 0, Zone.Battlefield, province: 0);
        _ = puppet; _ = mine;
        var e2 = MatchEngine.FromTestState(s);
        var options = Batch336Support.Options(e2, 0, ability).Where(o => o.Enabled).ToArray();
        var targets = options.SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(enemy.Id), "the enemy Personality is a target: " + string.Join(",", targets));
        Check(!targets.Contains(terrain.Id), "the Terrain is not: it may not be bowed");
        var source = options.First().CardId!;
        var rejection = Batch336Support.Rejection(e2, new GameCommand("useAbility", 0, e2.Version, source, TargetId: terrain.Id, AbilityId: ability));
        Check(rejection.Contains("Terrain"), "a crafted command against the Terrain is refused: " + rejection);
    }

    // ---- Sasumata (ruling 5) ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void SasumataTargets()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var pole = Add(s, Sasumata, 0, Zone.Attached, province: 0, parent: host.Id);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var armored = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var armor = Add(s, "training-armor", 1, Zone.Attached, province: 0, parent: armored.Id);
        var spelled = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var spell = Add(s, "training-spell", 1, Zone.Attached, province: 0, parent: spelled.Id);
        var followed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
        var home = Add(s, "training-lion-samurai", 1, Zone.Home); var homeItem = Add(s, "training-item", 1, Zone.Attached, parent: home.Id);
        var terrain = Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "sasumata-bow", pole.Id).TargetIds!.ToHashSet();
        Check(targets.SetEquals(new[] { bare.Id, weapon.Id, armor.Id, spell.Id, follower.Id, followed.Id, spelled.Id }), "every enemy card at the battlefield that is not a Personality carrying a Weapon or Armor: " + string.Join(",", targets));
        Check(!targets.Contains(armed.Id) && !targets.Contains(armored.Id), "a Personality with a Weapon or Armor is not a target, but the Weapon and the Armor themselves are");
        Check(!targets.Contains(home.Id) && !targets.Contains(homeItem.Id), "nothing away from the battlefield: a Battle action targets only cards there");
        Check(!targets.Contains(terrain.Id) && !targets.Contains(host.Id) && !targets.Contains(pole.Id), "not a Terrain, and not the player's own cards");
        Accept(e, Offer(e, 0, "sasumata-bow", pole.Id, weapon.Id));
        Check(e.State.Cards[weapon.Id].Bowed && e.State.Cards[pole.Id].Bowed && !e.State.Cards[armed.Id].Bowed, "the enemy Weapon itself bowed (its Personality did not) and the Sasumata bowed as the cost");
    }

    // ---- "may compare" choices (ruling 6) -----------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void CompareChoiceRules()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, UnseenAssailant, 0, Zone.Hand);
        var ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
        var big = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        big.Modifiers.Add(new("test-source", "test", "Force", 5)); // Force 9
        big.Modifiers.Add(new("test-source", "test", "Chi", -1));  // Chi 2
        var vet = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: vet.Id);
        var e = MatchEngine.FromTestState(s);
        var force = Batch336Support.Options(e, 0, "unseen-assailant-attack", card.Id, ninja.Id).Where(o => o.Enabled).Select(o => o.Command!.Choice).ToArray();
        Check(force.SequenceEqual(new string?[] { "force", "chi" }), "one listed option per comparison: " + string.Join(",", force));
        Check(Batch336Support.Options(e, 0, "unseen-assailant-attack", card.Id, ninja.Id).All(o => o.Label.Contains("compare against")), "each option names what it compares against");
        Check(Batch336Support.Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "unseen-assailant-attack", PerformerId: ninja.Id, TargetId: big.Id, Choice: "banana")).Length > 0, "an unknown comparison is refused");
        Check(Batch336Support.Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "unseen-assailant-attack", PerformerId: ninja.Id, TargetId: follower.Id, Choice: "chi")).Contains("Personality"), "Chi is for a Personality target only");
        Check(Batch336Support.Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "unseen-assailant-attack", PerformerId: ninja.Id, Choice: "chi")).Contains("Personality"), "and not for an Attack with no target");
        // No choice at all: Force, as every Attack does (9 is above 3: he survives).
        var plain = MatchEngine.FromTestState(s);
        Accept(plain, Batch336Support.Use(plain, 0, "unseen-assailant-attack", card.Id, big.Id, ninja.Id));
        Check(plain.State.Cards[big.Id].Zone == Zone.Battlefield, "a command without a choice compares Force");
        // Chi (2) is within 3.
        var chi = MatchEngine.FromTestState(s);
        Accept(chi, Batch336Support.OfferChoice(chi, 0, "unseen-assailant-attack", card.Id, big.Id, "chi", ninja.Id));
        Check(chi.State.Cards[big.Id].Zone == Zone.Dead, "compared against Chi 2");

        // The choice survives an Attack held for reactions: the enemy holds a reactor, passes, and the Attack resolves with the choice made at the announcement.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, UnseenAssailant, 0, Zone.Hand);
        var ninja2 = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
        var big2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        big2.Modifiers.Add(new("test-source", "test", "Force", 5)); big2.Modifiers.Add(new("test-source", "test", "Chi", -1));
        Add(t, "training-attack-reactor", 1, Zone.Battlefield, province: 0);
        var held = MatchEngine.FromTestState(t);
        Accept(held, Batch336Support.OfferChoice(held, 0, "unseen-assailant-attack", card2.Id, big2.Id, "chi", ninja2.Id));
        Eq(held.State.PendingChoice?.Type, "attackReaction", "the Attack is held for reactions");
        Check(held.State.PendingAttack?.Command?.Choice == "chi", "the pending Attack carries the choice");
        while (held.State.PendingChoice?.Type == "attackReaction") Accept(held, new("declineReaction", held.State.PriorityPlayerId, held.Version));
        Check(held.State.Cards[big2.Id].Zone == Zone.Dead, "it resolved comparing Chi");

        // Tsuruchi House Guard: Gold Cost needs a target; Nobunori: base Force only against a target with no attachments.
        var g = Table(Phase.Battle, 0);
        var host = Add(g, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var guard = Add(g, TsuruchiGuard, 0, Zone.Attached, province: 0, parent: host.Id);
        var eg = MatchEngine.FromTestState(g);
        Check(Batch336Support.Rejection(eg, new GameCommand("useAbility", 0, eg.Version, guard.Id, AbilityId: "tsuruchi-house-guard-attack", Choice: "gold")).Contains("Gold Cost"), "Gold Cost needs a target card");
        Check(Batch336Support.Rejection(eg, new GameCommand("useAbility", 0, eg.Version, guard.Id, AbilityId: "tsuruchi-house-guard-attack", Choice: "chi")).Length > 0, "Chi is not one of its choices");
        var n = Table(Phase.Battle, 0);
        var nob = Add(n, Nobunori, 0, Zone.Battlefield, province: 0);
        var armedEnemy = Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(n, "training-item", 1, Zone.Attached, province: 0, parent: armedEnemy.Id);
        var en = MatchEngine.FromTestState(n);
        Check(Batch336Support.Rejection(en, new GameCommand("useAbility", 0, en.Version, nob.Id, AbilityId: "nobunori-attack", TargetId: armedEnemy.Id, Choice: "base")).Contains("attachments"), "base Force is refused against a target with an attachment");
    }
    private static void IrezuAcceptedDuelOnly()
    {
        (MatchEngine E, CardInstance Irezu, CardInstance Scorpion) Setup()
        {
            var s = Table(Phase.Battle, 1);
            var irezu = Add(s, Irezu, 0, Zone.Battlefield, province: 0);
            var scorpion = Kw(Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0), "Scorpion Clan");
            var duelist = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 1, "training-duel-challenge", challenge.Id, scorpion.Id, performerId: duelist.Id));
            return (e, irezu, scorpion);
        }
        var (e, irezuCard, scorpion) = Setup();
        Eq(e.State.PendingDuel?.Stage, "AwaitingAccept", "the refusable challenge awaits its answer");
        Check(!CanUse(e, 0, "irezu-duel-enter", irezuCard.Id), "Irezu cannot enter a challenge that has not been accepted");
        Check(Rejection(e, new GameCommand("useAbility", 0, e.Version, irezuCard.Id, AbilityId: "irezu-duel-enter")).Length > 0, "a crafted command is refused");
        Check(!e.State.Cards[irezuCard.Id].Bowed && e.State.PendingDuel!.ChallengedId == scorpion.Id, "nothing was paid or changed");
        Accept(e, e.QueryActions(0).First(a => a.Type == "acceptDuel").Command!);
        Accept(e, Enabled(e, 0, "irezu-duel-enter", irezuCard.Id).Command!);
        Check(e.State.PendingDuel!.ChallengedId == irezuCard.Id && e.State.Cards[irezuCard.Id].Bowed, "once accepted, Irezu enters the duel instead, bowed as the cost");
    }
    private static void ConquerorStraightensWholeUnit()
    {
        var s = Table(Phase.Battle, 0);
        var conqueror = Add(s, "training-conqueror", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: conqueror.Id);
        var locked = Add(s, "training-spell", 0, Zone.Attached, bowed: true, province: 0, parent: conqueror.Id);
        locked.StraightenLockedTurn = s.TurnNumber;
        var other = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Batch336Support.RunUntil(e, () => e.State.Cards[conqueror.Id].Zone == Zone.Home);
        Check(!e.State.Cards[conqueror.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the Conqueror's unit straightened: his bowed Follower is no longer bowed");
        Check(e.State.Cards[locked.Id].Bowed, "the card that may not be straightened this turn stayed bowed");
        Check(e.State.Cards[other.Id].Zone == Zone.Home && e.State.Cards[other.Id].Bowed, "another unit returned home bowed");
    }
}
