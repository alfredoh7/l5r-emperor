using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #420 (stage 6 of Mendon's rulings of 2026-09-21): copying a whole Spell's rules permanently (Mastering the Elements), the Kata keyword (Skipping the Puddle), "may not be targeted by"
// with a Weapon-attached condition (Superior Reach) and dead Personalities counting as discarded (Scavenging Party).
internal static class CopyKataTargetingTests
{
    private const string Mastering = "oracle-l5r-4874", Puddle = "oracle-l5r-7285", Reach = "oracle-l5r-7656", Scavenging = "oracle-l5r-6491", Flood = "oracle-l5r-6502", Kaido = "oracle-l5r-4934";

    public static void Run(TestRunner runner) => runner.Run("copy-kata-targeting", [
        ("Published shape: the four cards are the printed cards with their abilities, keywords and art", PublishedShapes),
        ("Mastering the Elements: after it enters play, paying the Spell's base Gold Cost, it permanently copies the Spell's keywords, trait and base abilities (the Scouring Flood example)", MasteringCopiesEverything),
        ("Mastering the Elements is once per entry into play: it comes back when the card re-enters play, adds to what it already copied, and never copies abilities that copy abilities or entry abilities", MasteringReentry),
        ("Mastering the Elements' target is one of your own Spells in play (never an enemy's, never itself), and the Gold is the Spell's printed cost, not its current one", MasteringTargetsAndCost),
        ("Kata: Skipping the Puddle is put into play at the right of home, discarding your other Kata, and is not put into play from play", KataEntersPlay),
        ("Skipping the Puddle's Water Battle action: a Water Shugenja moves to a battlefield with enemy units and its controller may take an additional Battle action he must perform, once per turn", WaterBattleAction),
        ("Superior Reach: the enemy Personality's Force becomes 0 and, until the end of the turn and while the Kensai has a Weapon, actions that Personality performs may not target the Kensai", SuperiorReach),
        ("Scavenging Party: the Focus Effect gives the duel's winner -3F, the Open action +2 strength to each of your provinces", ScavengingFocusAndOpen),
        ("Scavenging Party's Limited action: a dead or discarded Personality of yours refills a province face-up, discarding the card there, and comes back as a fresh card", ScavengingRefill),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        var m = CardCatalog.Get(Mastering); var p = CardCatalog.Get(Puddle); var r = CardCatalog.Get(Reach); var s = CardCatalog.Get(Scavenging);
        Check(m.Name == "Mastering the Elements" && m.Type == CardType.Spell && m.Focus == 4 && m.GoldCost == 0 && m.Keywords!.SequenceEqual(new[] { "Unique" }) && m.Effect == "none"
            && m.Abilities!.SequenceEqual(new[] { "mastering-the-elements-copy" }), "Mastering the Elements");
        Check(p.Name == "Skipping the Puddle" && p.Type == CardType.Strategy && p.Focus == 3 && p.Keywords!.Contains("Kata") && p.Keywords.Contains("Water")
            && p.Abilities!.SequenceEqual(new[] { "skipping-the-puddle-enter", "skipping-the-puddle-move" }), "Skipping the Puddle");
        Check(r.Name == "Superior Reach" && r.Type == CardType.Strategy && r.Focus == 2 && r.Abilities!.SequenceEqual(new[] { "superior-reach-zero" }), "Superior Reach");
        Check(s.Name == "Scavenging Party" && s.Type == CardType.Strategy && s.Focus == 1 && s.Abilities!.SequenceEqual(new[] { "scavenging-party-focus", "scavenging-party-provinces", "scavenging-party-refill" }), "Scavenging Party");
        foreach (var id in new[] { Mastering, Puddle, Reach, Scavenging })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var copy = AbilityCatalog.All["mastering-the-elements-copy"];
        Check(copy.Trigger == "own-enters-play" && copy.OncePerGame && copy.CopiesAbilities && copy.Cost!.Basis == GoldCostBasis.TargetPrinted, "the copy Reaction: own entry, once per game, copies abilities, priced from the target's printed cost");
        Check(AbilityCatalog.All["skipping-the-puddle-move"].ActionKeywords!.Contains("Water") && AbilityCatalog.All["skipping-the-puddle-move"].MovesToBattle, "a Water action that moves to a battle");
    }

    // ---------- Mastering the Elements

    private static (MatchEngine E, CardInstance Mastering, CardInstance Flood, CardInstance Wielder, CardInstance Host) MasteringTable()
    {
        // A Spell enters play through Equip (Battle): an opposed unbowed Shugenja at the battlefield.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var wielder = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var host = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var flood = Add(s, Flood, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var mastering = Add(s, Mastering, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), mastering, flood, wielder, host);
    }

    private static GameCommand Equip(MatchEngine e, CardInstance spell, CardInstance host) => new("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: host.Id, TargetId: spell.Id, SourceIds: []);

    private static GameCommand Copy(MatchEngine e, CardInstance mastering, CardInstance target, int cost) =>
        new("useAbility", 0, e.Version, CardId: mastering.Id, AbilityId: "mastering-the-elements-copy", TargetId: target.Id, SourceIds: cost == 0 ? [] : e.PaymentOptions(0, cost).First().SourceIds);

    private static void MasteringCopiesEverything()
    {
        var (e, mastering, flood, wielder, host) = MasteringTable();
        Accept(e, Equip(e, mastering, host));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry window is open");
        var before = e.State.Cards[mastering.Id];
        Check(before.Printed is null && !e.HasKeyword(mastering.Id, "Water") && Def(mastering).Effect == "none", "it copies nothing until it is used");
        var listed = e.QueryActions(0).Where(x => x.Command?.AbilityId == "mastering-the-elements-copy" || x.Label.Contains("Once per game")).ToArray();
        var option = listed.SingleOrDefault(x => x.Enabled) ?? throw new Exception("the copy Reaction was not offered enabled: " + string.Join("; ", listed.Select(x => $"{x.Label} [{x.Enabled}] {x.Reason}")) + " | all: " + string.Join("; ", e.QueryActions(0).Select(x => x.Type)));
        Check(option.TargetIds!.SequenceEqual(new[] { flood.Id }), "the one Spell of his is the target: " + string.Join(",", option.TargetIds ?? []));
        Eq(option.TargetGold![flood.Id].Cost, 2, "Scouring Flood's base Gold Cost is asked");
        Reject(e, Copy(e, mastering, flood, 0));
        var pay = e.PaymentOptions(0, 2).First().SourceIds;
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: mastering.Id, AbilityId: "mastering-the-elements-copy", TargetId: flood.Id, SourceIds: pay));
        Check(pay.All(id => e.State.Cards[id].Bowed), "the Gold was paid from those sources");
        var m = e.State.Cards[mastering.Id];
        Check(m.Printed is not null, "its own printed text was rewritten");
        Check(e.HasKeyword(mastering.Id, "Water") && e.HasKeyword(mastering.Id, "Unique"), "the Water keyword is copied and its own kept");
        Eq(m.Printed!.Effect, "scouring-flood-force", "the trait");
        Check(m.Printed.Abilities!.SequenceEqual(new[] { "mastering-the-elements-copy", "scouring-flood-move-home" }), "its base ability was added to its own: " + string.Join(",", m.Printed.Abilities!));
        Eq(m.Printed.Name, "Mastering the Elements", "everything but the title: the title stays");
        Eq(e.CardForceForTest(host.Id), Def(host).Force + 2, "its wielder has +2F from the copied trait");
        Eq(e.CardForceForTest(wielder.Id), Def(wielder).Force + 2, "and the Spell it copied still gives its own");
        Check(!e.QueryActions(0).Any(x => x.Type == "useAbility" && x.Command?.AbilityId == "mastering-the-elements-copy" && x.Enabled), "once used it is not offered again in the same entry");
        // The copied action is live: in a battle, Mastering's Battle action moves a defending Personality home.
        var battle = Edit(e, st => { st.PendingChoice = null; st.PriorityPlayerId = 0; st.Cards[mastering.Id].Bowed = false; });
        Check(CanUse(battle, 0, "scouring-flood-move-home", mastering.Id), "Mastering the Elements now has Scouring Flood's Battle action");
        // It persists: nothing that resets a card that leaves play (grants, modifiers, uses) takes it away.
        var gone = Edit(e, st => { var c = st.Cards[mastering.Id]; c.Zone = Zone.FateDiscard; c.AttachedTo = null; });
        Check(gone.State.Cards[mastering.Id].Printed!.Effect == "scouring-flood-force", "a permanent change: the copy is part of the card for the game");
    }

    private static void MasteringReentry()
    {
        var (e, mastering, flood, wielder, host) = MasteringTable();
        Accept(e, Equip(e, mastering, host));
        Accept(e, Copy(e, mastering, flood, 2));
        // The card leaves play and comes back; a second Spell (a Fire one carrying a different trait, an action that copies abilities and an entry ability) is copied into what it has.
        var second = e.State.Cards.Values.First(c => c.Id == flood.Id);
        var back = Edit(e, st =>
        {
            var c = st.Cards[mastering.Id]; c.Zone = Zone.Hand; c.AttachedTo = null; c.Bowed = false; c.AbilityUses.Clear(); c.FaceUp = false; st.Players[0].Hand.Add(c.Id);
            st.PendingChoice = null; st.PriorityPlayerId = 0;
            var spell = Add(st, "training-spell-with-ability", 0, Zone.Attached, province: 0, parent: st.Cards[wielder.Id].Id);
            spell.Printed = CardCatalog.Get("training-spell-with-ability") with { Keywords = ["Fire"], Effect = "tsunami-force", Abilities = ["training-spell-force-boost", "fukuzo-wear-skin", "training-honor-loss-entry"] };
        });
        var spell2 = back.State.Cards.Values.First(c => c.DefinitionId == "training-spell-with-ability");
        Accept(back, Equip(back, mastering, host));
        Eq(back.State.PendingChoice?.Type, "entryReaction", "it entered play again");
        Check(back.QueryActions(0).Any(x => x.Type == "useAbility" && x.Command?.AbilityId == "mastering-the-elements-copy" && x.Enabled), "and its Reaction is available again");
        Accept(back, Copy(back, mastering, spell2, 1));
        var m = back.State.Cards[mastering.Id];
        Check(back.HasKeyword(mastering.Id, "Water") && back.HasKeyword(mastering.Id, "Fire"), "the keywords add to the ones it already copied");
        Check(m.Printed!.Abilities!.SequenceEqual(new[] { "mastering-the-elements-copy", "scouring-flood-move-home", "training-spell-force-boost" }),
            "the abilities add to the existing ones, without an ability that copies abilities or one that triggers on entry: " + string.Join(",", m.Printed.Abilities!));
        Eq(m.Printed.Effect, "tsunami-force", "a card has one trait slot: the later Spell's trait replaces the earlier one");
        Eq(back.CardForceForTest(host.Id), Def(host).Force + 6, "+6F now, not +2F");
        Check(second.Id == flood.Id);
    }

    private static void MasteringTargetsAndCost()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var mine = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
        var myFlood = Add(s, Flood, 0, Zone.Attached, province: 0, parent: mine.Id); var theirFlood = Add(s, Flood, 1, Zone.Attached, province: 0, parent: theirs.Id);
        myFlood.Modifiers.Add(new("test-source", "test", "GoldCost", 3));
        var handSpell = Add(s, Flood, 0, Zone.Hand);
        var mastering = Add(s, Mastering, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Equip(e, mastering, mine));
        var option = e.QueryActions(0).Single(x => x.Type == "useAbility" && x.Command?.AbilityId == "mastering-the-elements-copy" && x.Enabled);
        Check(option.TargetIds!.SequenceEqual(new[] { myFlood.Id }), "only his own Spell in play: not the enemy's, not the one in his hand, not itself: " + string.Join(",", option.TargetIds ?? []));
        Eq(option.TargetGold![myFlood.Id].Cost, 2, "the BASE Gold Cost (2), though its current cost is 5");
        Reject(e, Copy(e, mastering, theirFlood, 2));
        Reject(e, Copy(e, mastering, handSpell, 2));
        Reject(e, Copy(e, mastering, mastering, 0));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mastering.Id, AbilityId: "mastering-the-elements-copy", TargetId: myFlood.Id, SourceIds: []));
        Check(handSpell.Id != theirFlood.Id);
    }

    // ---------- Kata

    private static void KataEntersPlay()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var old = Add(s, Puddle, 0, Zone.Home); var other = Add(s, "training-focus-strategy", 0, Zone.Hand); var enemyKata = Add(s, Puddle, 1, Zone.Home);
        var kata = Add(s, Puddle, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "skipping-the-puddle-enter", old.Id).Any(o => o.Enabled), "a Kata already in play is not put into play again");
        Accept(e, Enabled(e, 0, "skipping-the-puddle-enter", kata.Id).Command!);
        var k = e.State.Cards[kata.Id];
        Check(k.Zone == Zone.Home && k.FaceUp && !e.State.Players[0].Hand.Contains(kata.Id), "it is in play at home, face up, not in the hand");
        Eq(e.State.Cards[old.Id].Zone, Zone.FateDiscard, "his other Kata was discarded");
        Eq(e.State.Cards[enemyKata.Id].Zone, Zone.Home, "the other player's Kata stays");
        Eq(e.State.Cards[other.Id].Zone, Zone.Hand, "a card that is not a Kata stays");
        Check(k.Zone == Zone.Home && CardCatalog.Get(kata.DefinitionId).Type == CardType.Strategy, "it stays in play as a Strategy: not discarded after resolving");
    }

    private static (MatchEngine E, CardInstance Kata, CardInstance Water, CardInstance Fire, CardInstance Enemy) PuddleTable(bool enemyThere = true)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var kata = Add(s, Puddle, 0, Zone.Home);
        var water = Add(s, "training-shugenja", 0, Zone.Home); GiveKeyword(water, "Water");
        var fire = Add(s, "training-shugenja", 0, Zone.Home); GiveKeyword(fire, "Fire");
        CardInstance enemy = null!;
        if (enemyThere) enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), kata, water, fire, enemy);
    }

    private static void WaterBattleAction()
    {
        var (e, kata, water, fire, _) = PuddleTable();
        Check(!Options(e, 0, "skipping-the-puddle-move", kata.Id, fire.Id).Any(o => o.Enabled), "a Shugenja who is not Water may not perform it");
        Accept(e, Enabled(e, 0, "skipping-the-puddle-move", kata.Id, water.Id).Command!);
        var w = e.State.Cards[water.Id];
        Check(w.Zone == Zone.Battlefield && w.ProvinceIndex == 0, "the Water Shugenja moved to the current battlefield");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { Length: 1 } ids && ids[0] == water.Id), "and its controller may take an additional Battle action that he must perform");
        Check(!CanUse(e, 0, "skipping-the-puddle-move", kata.Id, water.Id), "once per turn");
        // No enemy units at the battlefield: not usable at all.
        var (none, kata2, water2, _, _) = PuddleTable(enemyThere: false);
        Check(!Options(none, 0, "skipping-the-puddle-move", kata2.Id, water2.Id).Any(o => o.Enabled), "with no enemy units at the current battlefield it may not be taken");
        // A Kata still in hand gives no action; a Kata's action is its owner's.
        var (h, kata3, water3, _, _) = PuddleTable();
        var inHand = Edit(h, st => { var c = st.Cards[kata3.Id]; c.Zone = Zone.Hand; st.Players[0].Hand.Add(c.Id); });
        Check(!Options(inHand, 0, "skipping-the-puddle-move", kata3.Id, water3.Id).Any(o => o.Enabled), "the action belongs to a Kata in play, not one in the hand");
        Check(!Options(h, 1, "skipping-the-puddle-move", kata3.Id).Any(o => o.Enabled), "the other player has no such action");
    }

    // ---------- Superior Reach

    private static (MatchEngine E, CardInstance Card, CardInstance Kensai, CardInstance Weapon, CardInstance Target, CardInstance Other) ReachTable()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var kensai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(kensai, "Kensai");
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: kensai.Id);
        var target = Add(s, Kaido, 1, Zone.Battlefield, province: 0); var other = Add(s, Kaido, 1, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var card = Add(s, Reach, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, kensai, weapon, target, other);
    }

    private static void SuperiorReach()
    {
        var (e, card, kensai, weapon, target, other) = ReachTable();
        var offered = Options(e, 0, "superior-reach-zero", card.Id, kensai.Id).First(o => o.Enabled);
        var reachTargets = offered.TargetIds ?? [];
        Check(reachTargets.Contains(target.Id) && reachTargets.Length == 2, "the enemy Personalities without Weapons are the targets (the armed one is not): " + string.Join(",", reachTargets));
        var force = e.CardForceForTest(target.Id);
        Check(force > 0, "the target has Force to lose");
        Accept(e, Offer(e, 0, "superior-reach-zero", card.Id, target.Id, kensai.Id));
        Eq(e.CardForceForTest(target.Id), 0, "his Force is 0");
        Check(e.CardForceForTest(other.Id) > 0, "the other enemy Personality is untouched");
        // While the Kensai has a Weapon, the reduced Personality's actions may not target him; another Personality's may.
        Check(!CanUse(e, 1, "kaido-force-zero", target.Id), "the Personality's own action is not offered against the Kensai");
        var rejection = Rejection(e, new GameCommand("useAbility", 1, e.Version, target.Id, AbilityId: "kaido-force-zero", TargetId: kensai.Id));
        Check(rejection.Contains("may not be targeted"), "and is refused with the reason: " + rejection);
        Check(CanUse(e, 1, "kaido-force-zero", other.Id), "another Personality's action may target him");
        // The bar depends on the Weapon live: with it gone the Personality may target him again; back again, the bar returns.
        var unarmed = Edit(e, st => { st.Cards[weapon.Id].Zone = Zone.FateDiscard; st.Cards[weapon.Id].AttachedTo = null; });
        Check(CanUse(unarmed, 1, "kaido-force-zero", target.Id), "without a Weapon the Kensai may be targeted");
        var rearmed = Edit(unarmed, st => { st.Cards[weapon.Id].Zone = Zone.Attached; st.Cards[weapon.Id].AttachedTo = kensai.Id; });
        Check(!CanUse(rearmed, 1, "kaido-force-zero", target.Id), "with a Weapon again it may not");
        // Until the end of the turn: the next turn it is over.
        var nextTurn = Edit(e, st => st.TurnNumber++);
        Check(CanUse(nextTurn, 1, "kaido-force-zero", target.Id), "after the turn ends the bar is over");
        // It applies to the acting Personality only when the action is that Personality's: a bar leaves the Kensai targetable by the rest of the game's actions.
        Check(e.State.Cards[kensai.Id].TargetBars.Count == 1 && e.State.Cards[kensai.Id].TargetBars[0].ActorCardId == target.Id, "the bar sits on the Kensai, naming the Personality");
    }

    // ---------- Scavenging Party

    private static void ScavengingFocusAndOpen()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var focused = Add(s, Scavenging, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var e = MatchEngine.FromTestState(s);
        var forceMine = e.CardForceForTest(mine.Id); var forceTheirs = e.CardForceForTest(theirs.Id);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Check(e.State.PendingDuel is null, "the duel finished");
        var (winner, winnerForce, loser) = e.State.Cards[theirs.Id].Zone == Zone.Dead ? (mine, forceMine, theirs) : (theirs, forceTheirs, mine);
        Eq(e.State.Cards[loser.Id].Zone, Zone.Dead, "one side lost and was destroyed");
        Eq(e.CardForceForTest(winner.Id), winnerForce - 3, "the winner has -3F, whichever side that is");
        // Open: each of your provinces +2 strength (a destroyed one gets nothing).
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var party = Add(t, Scavenging, 0, Zone.Hand);
        t.Players[0].Provinces[3].Destroyed = true;
        var before = t.Players[0].Provinces.Select(p => p.Strength).ToArray(); var enemyBefore = t.Players[1].Provinces.Select(p => p.Strength).ToArray();
        var f = MatchEngine.FromTestState(t);
        Accept(f, Enabled(f, 0, "scavenging-party-provinces", party.Id).Command!);
        var after = f.State.Players[0].Provinces.Select(p => p.Strength).ToArray();
        for (var i = 0; i < 3; i++) Eq(after[i], before[i] + 2, $"province {i + 1} +2");
        Eq(after[3], before[3], "a destroyed province gets nothing");
        Check(f.State.Players[1].Provinces.Select(p => p.Strength).SequenceEqual(enemyBefore), "the other player's provinces are untouched");
    }

    private static void ScavengingRefill()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var card = Add(s, Scavenging, 0, Zone.Hand);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead); GiveKeyword(dead, "Berserker"); dead.Tokens.Add(new CardToken("t1", "Fire", "test", "Force", 1)); dead.Bowed = true; dead.Dishonorable = true;
        var discarded = Add(s, "training-lion-samurai", 0, Zone.DynastyDiscard);
        var enemyDead = Add(s, "training-lion-veteran", 1, Zone.Dead);
        var inPlay = Add(s, "training-crane-veteran", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var occupant = e.State.Players[0].Provinces[1].CardId!;
        var offers = Options(e, 0, "scavenging-party-refill", card.Id).Where(o => o.Enabled).ToArray();
        var targets = offers.SelectMany(o => o.TargetIds ?? []).Distinct().OrderBy(x => x).ToArray();
        Check(targets.SequenceEqual(new[] { dead.Id, discarded.Id }.OrderBy(x => x)), "his dead and his discarded Personalities, not the other player's and not one in play: " + string.Join(",", targets));
        var provinces = offers.SelectMany(o => o.Provinces ?? []).Distinct().OrderBy(x => x).ToArray();
        Check(provinces.SequenceEqual(new[] { 0, 1, 2, 3 }), "any of his four provinces holding a card is a choice: " + string.Join(",", provinces));
        // Refill province 2 with the dead Personality.
        var command = Offer(e, 0, "scavenging-party-refill", card.Id, dead.Id) with { Province = 1 };
        Reject(e, command with { Province = null });
        Reject(e, Offer(e, 0, "scavenging-party-refill", card.Id, discarded.Id) with { Province = 7 });
        Accept(e, command);
        var d = e.State.Cards[dead.Id];
        Check(d.Zone == Zone.Province && d.ProvinceIndex == 1 && d.FaceUp && e.State.Players[0].Provinces[1].CardId == dead.Id, "he is in the province, face up");
        Eq(e.State.Cards[occupant].Zone, Zone.DynastyDiscard, "the card that was there was discarded");
        Check(d.GrantedKeywords.Count == 0 && d.Tokens.Count == 0 && !d.Bowed && !d.Dishonorable, "he comes back as a fresh card: permanent changes, tokens, bowed and dishonored are gone");
        Check(!e.State.Players[0].Hand.Contains(card.Id) && e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the Strategy was spent");
        // A destroyed province or an empty one is not a choice.
        var g = Edit(e, st => { st.Players[0].Provinces[2].Destroyed = true; st.Players[0].Provinces[3].CardId = null; st.Cards[card.Id].Zone = Zone.Hand; st.Cards[card.Id].AbilityUses.Clear(); st.Players[0].Hand.Add(card.Id); st.Players[0].Provinces[0].CardId = st.Players[0].Provinces[0].CardId; st.PriorityPlayerId = 0; });
        var narrowed = Options(g, 0, "scavenging-party-refill", card.Id).Where(o => o.Enabled).SelectMany(o => o.Provinces ?? []).Distinct().OrderBy(x => x).ToArray();
        Check(narrowed.SequenceEqual(new[] { 0, 1 }), "only provinces that hold a card and are not destroyed: " + string.Join(",", narrowed));
        Check(enemyDead.Id != inPlay.Id);
    }
}
