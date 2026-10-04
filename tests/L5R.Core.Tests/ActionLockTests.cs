using System.Text.RegularExpressions;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Daigotsu Hotako, Experienced 3 (oracle-l5r-1726, issue #336) and the general "may not perform actions" status his Fear Open is the
// first user of, plus his Lion-Clan entry discount through the Gold quote path and the ordinary "X Champion" keyword. Every reading is in
// docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md. Each test builds an explicit position on the economy scenario (player 0 the
// Crane, player 1 the Lion) and drives it only through QueryActions and the commands the engine offers.
internal static class ActionLockTests
{
    private const string Hotako = "oracle-l5r-1726", Bow = "training-bow-at-battle";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Hotako exp3 is the printed card (Force 7, Chi 4, no Honor Requirement, Cost 11, Personal Honor 0, Spider Naval Unique Obsidian Champion), whole -- Effect lion-clan-discount-2, EntryHonorLoss 2 and one Fear Open ability tagged Fear -- with the scan's art shipped", PublishedShape),
            ("Fear Open: a locked Personality cannot perform ANY action -- its own, one on its Item, Spell or Follower, or a Strategy naming it as the performer -- each offered before the lock, refused (with the reason) after, while every other Personality still acts", LockBlocksEveryKindOfAction),
            ("A locked Personality can still be TARGETED: an enemy's action bows it and the lock is unaffected; the lock is public (CardView.ActionLocked, on the card and its attachments) and shows to both players", LockedCanBeTargeted),
            ("A Reaction is an action too: a Follower entering play under a locked Personality is not offered its entering-play Reaction, and is offered it once the lock is gone", LockBlocksReactions),
            ("The lock lasts this turn only: applied in the Action Phase, it holds through the Attack Phase and the whole End Phase (where no action exists to take) and is gone when the next turn begins -- the same reset whichever of \"this turn\" or \"the End Phase\" is read", LockLastsUntilTurnEnd),
            ("The lock is \"this turn\" even from the other player's Open action: used on the opponent's turn it ends with THAT turn, not on its controller's next one; and two locks on one card are still just a lock", LockedOnTheOpponentsTurn),
            ("Fear Open is a plain Open action: either player may take it, once a turn per card, on any Personality in play (its own controller's included), and never on a Follower, an attachment or a card out of play", FearOpenTargeting),
            ("Innate actions are barred too (Mendon's ruling): a locked dishonorable Samurai is listed for Order Seppuku disabled with the reason and refused when sent anyway, while an unlocked one still may -- and it is available again once the lock ends", InnateActionSeppukuIsBarred),
            ("Every innate action the engine offers is accounted for: the offered action types are pinned, and exactly two are performed by a Personality (Order Seppuku and an ability on a card) -- the rest are the player's (buying, equipping, passing, choosing) and none of them is barred by a lock on a Personality", InnateActionInventory),
            ("Assigning to attack or defend is not an action (Mendon's ruling): a locked Personality is offered in the attack and the defense assignments, is assigned, fights the battle with his Force counted, and is still locked and unable to act in it; he can also be moved by an enemy's action", AssignmentIsNotAnAction),
            ("Reactions ARE actions (#359, IsAction): a locked Personality cannot perform a Reaction in any window -- a curated before-action Reaction (Akodo Kakihara), a \"targeted by an action\" Reaction (a Merchant's negation, #335) and a before-resolution Reaction (Akodo Dairuko's shape) -- and no window opens for a Reaction nobody may play; unlocked, each window opens as before", LockCoversReactionsInEveryWindow),
            ("An action barred by the lock is refused, not negated (#356): a Personality whose ability declares a cost (bow a Farm) is listed disabled and refused when locked, and nothing is paid and its use is not spent, negated or not; an action announced BEFORE the lock is still taken; Order Seppuku is barred by the lock and by an owed Hesitation pass, each on its own", LockedActionsPayNothing),
            ("A copied ability is barred too (Fukuzo's copy primitive): an ability a locked Personality copied is listed disabled with the reason and refused, its \"copied from\" label intact, and is offered as usual without the lock", CopiedAbilityIsBarred),
            ("\"X Champion\" keywords are ordinary keywords: an Item that attaches only to an Obsidian Champion goes onto Hotako and onto no other Personality, and a Strategy naming a performing Obsidian Champion offers only Hotako -- no special rule", ChampionKeywordIsOrdinary),
            ("Hotako's entry discount goes through the ONE purchase quote: a Lion Clan player pays 9 (11 - 2, term-by-term basis), a Crane player 11; the Clan discount and allegiance still stack; and the exact payment enters him play, costs the Lion 2 Honor with no Reaction offered", LionClanDiscountThroughQuotePurchase),
            ("The Clan entry discount is one general term: \"<clan>-clan-discount\" is 1 less and \"<clan>-clan-discount-N\" N less for a Purchase and for an Equip alike, only for that Clan's player, never below 0", ClanDiscountIsGeneral),
        ];
        runner.Run("action-lock", tests);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------
    // Program.cs's top-level Check and Add would otherwise win the name lookup in this class (CS8801).
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private sealed record Rig(MatchState S, CardInstance Hotako, CardInstance Locked, CardInstance Item, CardInstance Spell, CardInstance Follower, CardInstance Free, CardInstance Attacker);

    // Player 1 (the active player, whose Open action it is) has Hotako at home. Player 0's `Locked` Personality has an Item, a Spell and a Follower
    // (each with a Battle action bowing an enemy Personality) and stands at province 0 beside a second, unaffected Personality of theirs; player 1's
    // Attacker is there too. The position is the Action Phase; Battle() turns it into the Battle Phase, player 0 the Defender to act.
    private static Rig ActionPhase()
    {
        var s = Table(Phase.Action, actor: 1);
        var hotako = Add(s, Hotako, 1, Zone.Home);
        var locked = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var item = Add(s, "training-bow-item", 0, Zone.Attached, province: 0, parent: locked.Id);
        var spell = Add(s, "training-bow-spell", 0, Zone.Attached, province: 0, parent: locked.Id);
        var follower = Add(s, "training-bow-follower", 0, Zone.Attached, province: 0, parent: locked.Id);
        var free = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return new(s, hotako, locked, item, spell, follower, free, attacker);
    }

    private static MatchEngine Battle(MatchEngine e, int priority = 0) => Edit(e, st =>
    {
        st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.ActivePlayerId = 1; st.PriorityPlayerId = priority; st.ConsecutivePasses = 0;
    });

    private static MatchEngine Lock(MatchEngine e, CardInstance hotako, CardInstance target)
    {
        Accept(e, Offer(e, 1, "hotako-fear-lock", hotako.Id, target.Id));
        return e;
    }

    // ---- published shape -------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.PersonalityWave32.Cards.Length, 4, "Hotako, Yoshitada, Predator and Liu-Xeung");
        var h = CardCatalog.Get(Hotako);
        Check(!h.ScenarioOnly && h.Type == CardType.Personality && h.Name == "Daigotsu Hotako" && h.Clan == "Spider");
        Eq(h.Force, 7); Eq(h.Chi, 4); Eq(h.GoldCost, 11); Eq(h.PersonalHonor, 0); Check(h.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Experienced 3", "Naval", "Unique", "Obsidian Champion", "Paragon", "Samurai", "Shadowlands", "Spider Clan" }) Check(h.Keywords!.Contains(k), k);
        Check(!h.Keywords!.Contains("Loyal"), "Hotako is not Loyal, so a Lion Clan player may recruit him");
        Eq(h.Text, "Enters play paying 2 less Gold if you are a Lion Clan player.\nAfter Hotako enters play: Lose 2 Honor.\nFear Open: Target a Personality: He may not perform actions this turn.");
        Eq(h.EntryHonorLoss, 2); Eq(h.Effect, "lion-clan-discount-2"); Check(h.Abilities!.SequenceEqual(new[] { "hotako-fear-lock" }));
        var fear = AbilityCatalog.All["hotako-fear-lock"];
        Eq(fear.Timing, "Open"); Eq(fear.Target, "personality"); Check(fear.ActionKeywords?.SequenceEqual(new[] { "Fear" }) == true, "the ability is a Fear action");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Hotako + ".jpg")), "the scan is shipped");
        Eq(h.ArtworkKey, $"res://Content/Published/{Hotako}.jpg");
    }

    // ---- the lock ----------------------------------------------------------------------------------------------------------
    private static void LockBlocksEveryKindOfAction()
    {
        var r = ActionPhase();
        var strategy = Add(r.S, "training-performer-strategy", 0, Zone.Hand);
        var e0 = Battle(MatchEngine.FromTestState(r.S));
        // Control: before the lock every one of them is offered to player 0.
        foreach (var (name, card) in new[] { ("its own action", r.Locked), ("its Item's action", r.Item), ("its Spell's action", r.Spell), ("its Follower's action", r.Follower), ("the other Personality's action", r.Free) })
            Check(CanUse(e0, 0, Bow, card.Id), $"before the lock: {name} is offered");
        Check(CanUse(e0, 0, "training-performer-boost", strategy.Id, r.Locked.Id) && CanUse(e0, 0, "training-performer-boost", strategy.Id, r.Free.Id), "before the lock: the Strategy may name either Personality");
        // Hotako locks the Personality in the Action Phase; the Attack Phase position is then rebuilt from the same engine.
        var e = ActionPhaseEngine(r);
        Lock(e, r.Hotako, r.Locked);
        Eq(e.State.Cards[r.Locked.Id].ActionLocks.Count, 1, "one lock, from Hotako");
        e = Battle(e);
        foreach (var (name, card) in new[] { ("its own action", r.Locked), ("its Item's action", r.Item), ("its Spell's action", r.Spell), ("its Follower's action", r.Follower) })
        {
            var options = Options(e, 0, Bow, card.Id);
            Check(options.Length == 1 && !options[0].Enabled && options[0].Reason!.Contains("may not perform actions"), $"after the lock: {name} is listed disabled with the reason ({string.Join(";", options.Select(o => o.Reason))})");
            Check(Rejection(e, new GameCommand("useAbility", 0, e.Version, card.Id, TargetId: r.Attacker.Id, AbilityId: Bow)).Contains("may not perform actions"), $"{name} is refused when sent anyway");
        }
        Check(CanUse(e, 0, Bow, r.Free.Id), "the other Personality still acts");
        var named = Options(e, 0, "training-performer-boost", strategy.Id);
        Check(named.Count(o => o.Enabled) == 1 && named.Single(o => o.Enabled).Label.Contains($"({r.Free.Id})") && named.Single(o => !o.Enabled).Reason!.Contains("may not perform actions"),
            "the Strategy that names its performer no longer accepts the locked Personality, and still accepts the other");
        Check(Rejection(e, Use(e, 0, "training-performer-boost", strategy.Id, performerId: r.Locked.Id)).Contains("may not perform actions"), "naming him as the performer is refused when sent anyway");
        Accept(e, Use(e, 0, "training-performer-boost", strategy.Id, performerId: r.Free.Id));
        Check(e.State.Cards[r.Free.Id].Modifiers.Any(m => m.Effect == "training-performer-boost"), "and the other Personality performs it");
    }

    private static MatchEngine ActionPhaseEngine(Rig r) => MatchEngine.FromTestState(r.S);

    private static void LockedCanBeTargeted()
    {
        var r = ActionPhase();
        var e = ActionPhaseEngine(r);
        Lock(e, r.Hotako, r.Locked);
        // Public to both players, on the card and on what is attached to it, and on nothing else.
        foreach (var viewer in new[] { 0, 1 })
        {
            var army = e.GetView(viewer).Players[0].Army;
            Check(army.Single(c => c.Id == r.Locked.Id).ActionLocked && army.Single(c => c.Id == r.Item.Id).ActionLocked && army.Single(c => c.Id == r.Follower.Id).ActionLocked, $"player {viewer} sees the lock on him and his attachments");
            Check(!army.Single(c => c.Id == r.Free.Id).ActionLocked, $"player {viewer} sees no lock on the other Personality");
        }
        e = Battle(e, priority: 1);
        Check(CanUse(e, 1, Bow, r.Attacker.Id), "the enemy still acts");
        Accept(e, Offer(e, 1, Bow, r.Attacker.Id, r.Locked.Id));
        Check(e.State.Cards[r.Locked.Id].Bowed, "an action targeting the locked Personality resolves on him");
        Eq(e.State.Cards[r.Locked.Id].ActionLocks.Count, 1, "and the lock is not touched by it");
    }

    private static void LockBlocksReactions()
    {
        foreach (var locked in new[] { true, false })
        {
            var s = GoldParityTests.Action();
            for (var i = 0; i < 4; i++) GoldParityTests.Add(s, "training-marketplace", 0, Zone.Home);
            var follower = GoldParityTests.Add(s, "training-invest-follower", 0, Zone.Hand);
            var host = GoldParityTests.Add(s, "training-crane-samurai", 0, Zone.Home);
            if (locked) host.ActionLocks.Add(new("test", "Turn"));
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("equip", 0, e.Version, CardId: follower.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds, TargetId: host.Id));
            Eq(e.State.PendingChoice?.Type, "entryReaction");
            var reaction = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == follower.Id).ToArray();
            Check(reaction.Length == 1, "the reaction is listed either way");
            if (locked) Check(!reaction[0].Enabled && reaction[0].Reason!.Contains("may not perform actions"), "the Follower's Reaction is not available under a locked Personality: " + reaction[0].Reason);
            else Check(reaction[0].Enabled, "the same Reaction is offered without the lock: " + reaction[0].Reason);
        }
    }

    private static void LockLastsUntilTurnEnd()
    {
        var s = Table(Phase.Action, actor: 1);
        var hotako = Add(s, Hotako, 1, Zone.Home);
        var locked = Add(s, "training-battle-bower", 0, Zone.Home); Force(locked, 15); // holds province 0 against everything player 1 can send
        var attacker = Add(s, "training-battle-bower", 1, Zone.Home);
        for (var i = 0; i < 9; i++) Add(s, "training-farm", 1, Zone.Hand); // more than the hand limit: the End Phase stops at the discard
        var e = MatchEngine.FromTestState(s);
        bool Locked() => (e.GetView(0).Players[0].Home.Concat(e.GetView(0).Players[0].Army).SingleOrDefault(c => c.Id == locked.Id) ?? throw new Exception($"the locked Personality is {e.State.Cards[locked.Id].Zone} in {e.State.Phase}")).ActionLocked;
        Lock(e, hotako, locked);
        Check(Locked(), "locked in the Action Phase");
        var phases = new HashSet<Phase>();
        // Through the whole Attack Phase -- a real battle over province 0, the locked Personality defending it -- to the End Phase's discard.
        RunUntil(e, () => e.State.Phase == Phase.End && e.State.PendingChoice is not null, () =>
        {
            phases.Add(e.State.Phase);
            if (e.State.Phase != Phase.End) Check(Locked(), $"still locked in {e.State.Phase}");
            if (e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince == 0)
            {
                Check(e.State.Cards[locked.Id].Zone == Zone.Battlefield, "he is defending");
                Check(Options(e, 0, Bow, locked.Id).All(o => !o.Enabled), "and cannot act in the battle");
            }
        });
        Check(phases.Contains(Phase.Battle), "a real battle was fought");
        Eq(e.State.Phase, Phase.End); Eq(e.State.PendingChoice?.Type, "discardHand");
        Check(Locked(), "still locked in the End Phase");
        Check(e.QueryActions(0).Concat(e.QueryActions(1)).All(a => a.Type != "useAbility"), "the End Phase offers no action to either player, so the lock has nothing left to block there");
        Eq(e.State.TurnNumber, 1);
        Accept(e, e.QueryActions(1).First(a => a.Type == "discardHand" && a.Enabled).Command! with { CardIds = e.State.Players[1].Hand.Take(e.State.Players[1].Hand.Count - 8).ToArray() });
        Eq(e.State.TurnNumber, 2, "the next turn has begun"); Check(!Locked(), "and the lock is gone");
        Eq(e.State.Cards[locked.Id].ActionLocks.Count, 0, "the state agrees");
    }

    private static void LockedOnTheOpponentsTurn()
    {
        // Player 0's Hotako locks player 1's Personality during player 1's own turn: it ends with THAT turn, so player 1 acts freely on his next one.
        var s = Table(Phase.Action, actor: 1); s.PriorityPlayerId = 0; // player 1 has acted: the other player holds the opportunity
        var hotako = Add(s, Hotako, 0, Zone.Home);
        var victim = Add(s, "training-battle-bower", 1, Zone.Home);
        Add(s, "training-farm", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "hotako-fear-lock", hotako.Id, victim.Id));
        Eq(e.State.Cards[victim.Id].ActionLocks.Count, 1);
        e = Edit(e, st => st.Cards[victim.Id].ActionLocks.Add(new("second-source", "Turn")));
        Eq(e.State.Cards[victim.Id].ActionLocks.Count, 2, "a second lock is just added");
        Check(e.GetView(1).Players[1].Home.Single(c => c.Id == victim.Id).ActionLocked, "locked");
        RunUntil(e, () => e.State.TurnNumber == 2);
        Check(e.State.Cards[victim.Id].ActionLocks.Count == 0 && !e.GetView(1).Players[1].Home.Concat(e.GetView(1).Players[1].Army).Concat(e.GetView(1).Players[1].Discard).Single(c => c.Id == victim.Id).ActionLocked, "both locks are gone when the turn they were placed in ends (turn 2 is player 0's)");
        Eq(e.State.ActivePlayerId, 0);
        RunUntil(e, () => e.State.TurnNumber == 3 && e.State.Phase == Phase.Action);
        Eq(e.State.ActivePlayerId, 1); Check(!e.State.Cards[victim.Id].ActionLocks.Any(), "and player 1's own next turn is free too");
    }

    private static void FearOpenTargeting()
    {
        var s = Table(Phase.Action, actor: 0);
        var hotako = Add(s, Hotako, 0, Zone.Home);
        var ownOther = Add(s, "training-battle-bower", 0, Zone.Home);
        var enemy = Add(s, "training-battle-bower", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id);
        var inProvince = Add(s, "training-battle-bower", 1, Zone.Province);
        var holding = Add(s, "training-farm", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hotako-fear-lock", hotako.Id).TargetIds!;
        Check(targets.ToHashSet().SetEquals([hotako.Id, ownOther.Id, enemy.Id]), "any Personality in play, his own controller's and Hotako himself included: " + string.Join(",", targets));
        Check(!targets.Contains(follower.Id) && !targets.Contains(holding.Id) && !targets.Contains(inProvince.Id), "never a Follower, a Holding or a card out of play");
        Accept(e, Offer(e, 0, "hotako-fear-lock", hotako.Id, hotako.Id));
        Check(e.State.Cards[hotako.Id].ActionLocks.Count == 1, "he may lock himself");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, 0, "hotako-fear-lock", hotako.Id), "once a turn per card");
        Check(Rejection(e, new GameCommand("useAbility", 0, e.Version, hotako.Id, TargetId: enemy.Id, AbilityId: "hotako-fear-lock")).Contains("already been used"), "a second use is refused");
    }

    // ---- innate actions (Mendon's ruling, issue #336) -----------------------------------------------------------------------------
    // "No actions means even innate actions may not be performed by a Personality who is barred from taking actions. The Personality may still be
    // assigned to attack or defend, as those are not actions."
    private static void InnateActionSeppukuIsBarred()
    {
        var s = Table(Phase.Action, actor: 0);
        var locked = Add(s, "training-dishonored-samurai", 0, Zone.Home); locked.Dishonorable = true; locked.ActionLocks.Add(new("test", "Turn"));
        var free = Add(s, "training-dishonored-samurai", 0, Zone.Home); free.Dishonorable = true;
        var honorable = Add(s, "training-dishonored-samurai", 0, Zone.Home); honorable.ActionLocks.Add(new("test", "Turn")); // locked but not dishonorable: Seppuku was never his
        var e = MatchEngine.FromTestState(s);
        var seppuku = e.QueryActions(0).Where(a => a.Type == "orderSeppuku").ToArray();
        Eq(seppuku.Length, 2, "one listing per dishonorable Samurai, the locked one included");
        var lockedOption = seppuku.Single(a => a.CardId == locked.Id);
        Check(!lockedOption.Enabled && lockedOption.Reason is not null && lockedOption.Reason.Contains("may not perform actions"), "the locked one is listed disabled with the reason: " + lockedOption.Reason);
        Check(seppuku.Single(a => a.CardId == free.Id).Enabled, "the unlocked one is offered as before");
        Check(Rejection(e, new GameCommand("orderSeppuku", 0, e.Version, locked.Id)).Contains("may not perform actions"), "Order Seppuku by the locked Personality is refused when sent anyway");
        Check(e.State.Cards[locked.Id].Zone == Zone.Home, "and nothing happened to him");
        // The same for the other player's view of the position: a lock is public and either player's listing shows only its own cards.
        Check(e.QueryActions(1).All(a => a.Type != "orderSeppuku"), "the other player is never offered Seppuku for a Personality that is not theirs");
        Accept(e, new GameCommand("orderSeppuku", 0, e.Version, free.Id));
        Eq(e.State.Cards[free.Id].Zone, Zone.Dead, "an unlocked dishonorable Samurai still performs it");
        // Lifted: available again. (Ordering Seppuku is an action, so the opportunity passed to the other player when the free one died -- issue #368;
        // it is handed back to player 0 here, since the point is the lock, not whose turn it is.)
        Eq(e.State.PriorityPlayerId, 1, "the action passed the opportunity on");
        var lifted = Edit(e, st => { st.Cards[locked.Id].ActionLocks.Clear(); st.PriorityPlayerId = 0; });
        Check(lifted.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == locked.Id).Enabled, "without the lock he may order it");
        Accept(lifted, new GameCommand("orderSeppuku", 0, lifted.Version, locked.Id));
        Eq(lifted.State.Cards[locked.Id].Zone, Zone.Dead);
        // Locked by Hotako's real Fear Open on a dishonorable Samurai, end to end.
        var s2 = Table(Phase.Action, actor: 0);
        var hotako = Add(s2, Hotako, 0, Zone.Home); var victim = Add(s2, "training-dishonored-samurai", 0, Zone.Home); victim.Dishonorable = true;
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.QueryActions(0).Single(a => a.Type == "orderSeppuku").Enabled, "before Hotako's action");
        Accept(e2, Offer(e2, 0, "hotako-fear-lock", hotako.Id, victim.Id));
        e2 = Edit(e2, st => st.PriorityPlayerId = 0);
        var after = e2.QueryActions(0).Single(a => a.Type == "orderSeppuku");
        Check(!after.Enabled && after.Reason!.Contains("may not perform actions"), "after it: " + after.Reason);
        Rejection(e2, new GameCommand("orderSeppuku", 0, e2.Version, victim.Id));
    }

    // The inventory: every action type MatchQueries offers, classified. A new one fails here until it is classified (and, if a Personality performs it,
    // barred by the lock) -- the point of the test.
    private static readonly string[] PerformedByAPersonality = ["orderSeppuku", "useAbility"];
    private static readonly string[] PlayersOwnAndNotBarred =
    [
        "beforeBattleOptional", "beforeBattleOrder", "chooseConqueror", "choosePreservingHonor", "chooseRikateDetach", "chooseRinShame", "chooseShinden", "harshChoices",
        "chooseYourClanNeedsYouProvince", "concede", "declareAttack", "declineReaction", "declineStraighten", "discardHand", "discardProvince", "encircledOrder", "encircledSelect",
        "endDynasty", "equip", "forcedDiscard", "overlay", "pass", "playStrategy", "proclaim", "purchase", "recruitReserve", "selectBattle", "wait",
        // Seeds of Decay pass (issue #453), batch 4: two Events Phase decisions of the revealing player's (Offering Reverence's shuffle-or-discard, Tested Land's enemy province).
        "offeringReverence", "testedLandProvince",
        // Issue #501 (Mendon's item 2): declining an optional additional action is the player's decision, not an action a Personality performs.
        "declineAdditionalAction",
        // Plains of Otosan Uchi (issue #554): the reveal-order choice and the province-card-targeting disambiguation (Shosuro Toson) are both the province's own controller's decisions.
        "provinceCardChoice", "provinceOrder",
        // Pass all battles (issue #628): the player's own standing pass, not an action a Personality performs.
        "passAllBattles",
    ];

    private static void InnateActionInventory()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/L5R.Core/MatchQueries.cs"));
        // The player-level timing grant (#352, TimingGrants.cs) offers the equip action from its own file.
        var granted = File.ReadAllText(Path.Combine(RepoRoot(), "src/L5R.Core/TimingGrants.cs"));
        var offered = Regex.Matches(source, "\\bAdd\\(\"(\\w+)\"").Concat(Regex.Matches(granted, "\\bnew\\(\"(equip)\"")).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var known = PerformedByAPersonality.Concat(PlayersOwnAndNotBarred).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(offered.SequenceEqual(known), "the offered action types changed -- classify the new one (a Personality performs it: bar it under a lock; else list it as the player's): "
            + "new [" + string.Join(", ", offered.Except(known)) + "], gone [" + string.Join(", ", known.Except(offered)) + "]");
        // The two assignment types are built from a variable, not a literal: they are assignments, not actions (the next test).
        Check(source.Contains("var actionType = attack ? \"attack\" : \"assignDefense\""), "attack and assignDefense are still the one assignment construction");
        // Behaviour, for the one of the two that is not an ability: Order Seppuku is listed disabled for a locked Personality and every ability on him too;
        // and a Personality's lock changes none of the player's own actions in the Dynasty and Action Phases (buying, passing).
        var s = Table(Phase.Action, actor: 0);
        var locked = Add(s, "training-battle-bower", 0, Zone.Home); locked.ActionLocks.Add(new("test", "Turn"));
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == locked.Id).All(a => !a.Enabled && a.Reason!.Contains("may not perform actions")), "every ability listed on him is disabled with the reason");
        Check(e.QueryActions(0).Any(a => a.Type == "pass" && a.Enabled), "the player still passes");
        var s2 = Table(Phase.Dynasty, actor: 0); Add(s2, "training-battle-bower", 0, Zone.Home).ActionLocks.Add(new("test", "Turn"));
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.QueryActions(0).Any(a => a.Type == "endDynasty" && a.Enabled), "and ends the Dynasty Phase");
    }

    private static void AssignmentIsNotAnAction()
    {
        // Player 1 attacks province 1 with a locked Personality (Force 12) and player 0 defends with one (Force 7). Hotako, who did the locking, is bowed.
        var s = Table(Phase.Action, actor: 1);
        var hotako = Add(s, Hotako, 1, Zone.Home);
        var attacker = Add(s, "training-battle-bower", 1, Zone.Home); Force(attacker, 10);
        var defender = Add(s, "training-battle-bower", 0, Zone.Home); Force(defender, 5); defender.ActionLocks.Add(new("test", "Turn"));
        var e = MatchEngine.FromTestState(s);
        Lock(e, hotako, attacker);
        e = Edit(e, st => { st.Cards[hotako.Id].Bowed = true; st.PriorityPlayerId = 1; });
        Check(e.GetView(1).Players[1].Home.Single(c => c.Id == attacker.Id).ActionLocked && e.GetView(0).Players[0].Home.Single(c => c.Id == defender.Id).ActionLocked, "both are locked");
        bool sawAttack = false, sawDefense = false;
        RunUntil(e, () => e.State.Phase == Phase.Battle, () =>
        {
            if (e.State.Phase == Phase.InfantryDeclaration) // the Attacker's Infantry Maneuvers (the Declaration Segment before it only declares)
            {
                var attack = e.QueryActions(1).Where(a => a.Type == "attack" && a.Enabled).ToArray();
                sawAttack |= attack.Length > 0 && attack.All(a => a.TargetIds!.Contains(attacker.Id));
            }
            if (e.State.Phase == Phase.DefenseAssignment)
            {
                var defend = e.QueryActions(0).Where(a => a.Type == "assignDefense" && a.Enabled).ToArray();
                sawDefense |= defend.Length > 0 && defend.All(a => a.TargetIds!.Contains(defender.Id));
            }
        });
        Check(sawAttack, "the locked attacker is offered in the attack assignment, enabled"); Check(sawDefense, "the locked defender is offered in the defense assignment, enabled");
        Check(e.State.Cards[attacker.Id].Zone == Zone.Battlefield && e.State.Cards[attacker.Id].ProvinceIndex == 0, "he was assigned and moved to the battlefield");
        Check(e.State.Cards[defender.Id].Zone == Zone.Battlefield && e.State.Cards[defender.Id].ProvinceIndex == 0, "and so was the locked defender");
        Check(e.State.Cards[attacker.Id].ActionLocks.Count == 1 && e.State.Cards[defender.Id].ActionLocks.Count == 1, "neither lock was touched by assigning");
        Check(Options(e, 0, Bow, defender.Id).All(o => !o.Enabled) && Options(e, 1, Bow, attacker.Id).All(o => !o.Enabled), "in the battle neither can act");
        RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));
        Check(e.State.Events.Any(x => x.Type == "battleResolved" && x.Message.Contains("attacking Force 12, defending Force 7")), "both fought with their Force counted: " + string.Join(" | ", e.State.Events.Where(x => x.Type == "battleResolved").Select(x => x.Message)));
        // Moved by an enemy's action: still a legal target, still locked afterwards.
        var t = Table(); t.ActivePlayerId = 1; t.PriorityPlayerId = 1; t.CurrentBattleProvince = 1; t.UnresolvedBattlefields = [0, 1, 2];
        var predator = Add(t, "oracle-l5r-6522", 1, Zone.Battlefield, province: 1);
        var moved = Add(t, "training-battle-bower", 0, Zone.Battlefield, province: 1); moved.ActionLocks.Add(new("test", "Turn"));
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, "predator-move", predator.Id, moved.Id) with { Province = 2 });
        Check(e2.State.Cards[moved.Id].ProvinceIndex == 2 && e2.State.Cards[moved.Id].ActionLocks.Count == 1, "an enemy's action moved the locked Personality, and the lock stands");
    }

    // ---- Reactions are actions (#359: IsAction) ------------------------------------------------------------------------------------------------
    private static void LockCoversReactionsInEveryWindow()
    {
        // (1) A curated before-action Reaction: Akodo Kakihara's, for an action that targets HIM. Player 0 bows him with a Battle action.
        foreach (var locked in new[] { false, true })
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            var kakihara = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0);
            if (locked) kakihara.ActionLocks.Add(new("test", "Turn"));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, Bow, bower.Id, kakihara.Id));
            if (locked)
            {
                Check(e.State.PendingChoice is null && e.State.Cards[kakihara.Id].Bowed, "locked Kakihara: no window opens for a Reaction nobody may play, and the bow resolves");
                Check(!e.State.Events.Any(x => x.Type == "reactionWindow" && x.Message.StartsWith("Before-action")), "no window was announced");
            }
            else
            {
                Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "unlocked: his window opens");
                Check(e.QueryActions(1).Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "kakihara-force-bonus"), "and his Reaction is offered");
            }
        }
        // (2) A "targeted by an action" Reaction (#335): a Merchant's negation, paid at no cost. Player 1 bows the Merchant with a Battle action.
        foreach (var locked in new[] { false, true })
        {
            var s = Table();
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var guard = Add(s, "training-merchant-free-guard", 0, Zone.Battlefield, province: 0);
            if (locked) guard.ActionLocks.Add(new("test", "Turn"));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, guard.Id));
            if (locked) Check(e.State.PendingChoice is null && e.State.Cards[guard.Id].Bowed, "locked Merchant: no window, the bow resolves on him");
            else
            {
                Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "unlocked: the window opens");
                Check(e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-merchant-free"), "and the negation is offered");
            }
        }
        // (2b) A Reaction listed inside a window that is open anyway (an unlocked Merchant guards a second card): the locked one's Reaction is listed disabled
        // with the reason, and refused when sent.
        {
            var s = Table();
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var open = Add(s, "training-merchant-free-guard", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, open.Id));
            var st = e.State; st.Cards[open.Id].ActionLocks.Add(new("test", "Turn")); // locked while its window is open
            e = MatchEngine.FromTestState(st);
            var listed = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-negate-merchant-free").ToArray();
            Check(listed.Length == 0 || listed.All(a => !a.Enabled && a.Reason!.Contains("may not perform actions")), "a Reaction of a Personality locked while its window is open is not playable: " + string.Join(";", listed.Select(a => a.Reason)));
            Reject(e, new GameCommand("useAbility", 0, e.Version, open.Id, AbilityId: "training-negate-merchant-free"));
        }
        // (3) A before-resolution Reaction (Akodo Dairuko's shape, #363): "Before the resolution of a battle at her battlefield". Both players pass, and the
        // window for it opens -- unless its Personality is locked, then the battle resolves.
        foreach (var locked in new[] { false, true })
        {
            var s = Table();
            var commander = Add(s, "training-rallying-commander", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            if (locked) commander.ActionLocks.Add(new("test", "Turn"));
            var e = MatchEngine.FromTestState(s);
            for (var i = 0; i < 2; i++) Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "pass" && a.Enabled).Command!);
            if (locked) Check(e.State.PendingChoice is null && e.State.Events.Any(x => x.Type == "battleResolved"), "locked: the battle resolves without asking for his Reaction");
            else
            {
                Eq(e.State.PendingChoice?.Type, "beforeBattleReaction", "unlocked: the before-resolution Reaction is asked"); Eq(e.State.PendingChoice?.CardId, commander.Id);
            }
        }
    }

    // ---- the lock and the atomic payment of costs (#356) -----------------------------------------------------------------------------------------
    // An action a locked Personality "may not perform" is REFUSED, not negated: it is never announced, so nothing of it is paid (no bow, no Gold, no Honor) and its
    // use is not spent -- the payment machinery of #356 (a negated action pays all its declared costs; a blocked cost aborts atomically) never sees it. An action
    // that WAS announced is taken: the lock does not reach back into a window it waits in.
    private static void LockedActionsPayNothing()
    {
        (MatchEngine E, CardInstance Source, CardInstance Farm) Rig(bool locked, bool negated)
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var source = Add(s, "training-ashigaru-boost-source", 0, Zone.Battlefield, province: 0); var farm = Add(s, "training-farm", 0, Zone.Home);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (locked) source.ActionLocks.Add(new("test", "Turn"));
            var e = MatchEngine.FromTestState(s); e.NegateEveryAction = negated;
            return (e, source, farm);
        }
        const string Boost = "training-ashigaru-boost";
        // (1) Unlocked, negated: the declared cost (bow the Farm) is paid, the effect is not -- the baseline the locked case must differ from.
        var (e1, s1, f1) = Rig(false, true);
        Accept(e1, Offer(e1, 0, Boost, s1.Id, f1.Id));
        Check(e1.State.Cards[f1.Id].Bowed && !e1.State.Cards[s1.Id].Modifiers.Any(m => m.Effect == Boost), "unlocked and negated: the Farm was bowed as the cost, no effect");
        // (2) Locked: listed disabled with the reason, refused when sent, and NOTHING was paid -- negated or not.
        foreach (var negated in new[] { false, true })
        {
            var (e, source, farm) = Rig(true, negated);
            var options = Options(e, 0, Boost, source.Id);
            Check(options.Length == 1 && !options[0].Enabled && options[0].Reason!.Contains("may not perform actions"), $"negated={negated}: listed disabled with the reason");
            Check(Rejection(e, new GameCommand("useAbility", 0, e.Version, source.Id, TargetId: farm.Id, AbilityId: Boost)).Contains("may not perform actions"), "refused when sent");
            Check(!e.State.Cards[farm.Id].Bowed && !e.State.Cards[source.Id].AbilityUses.ContainsKey(Boost), "the Farm was not bowed and the use was not spent");
            Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 0, "nothing happened: priority did not pass");
        }
        // (3) Announced first, locked while the action waits in a before-action window: the action was taken, so it resolves (and pays) as announced.
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var source = Add(s, "training-ashigaru-boost-source", 0, Zone.Battlefield, province: 0); var farm = Add(s, "training-farm", 0, Zone.Home);
            var kakihara = Add(s, "oracle-l5r-223", 1, Zone.Battlefield, province: 0); var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, Bow, bower.Id, kakihara.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
            var st = e.State; st.Cards[bower.Id].ActionLocks.Add(new("test", "Turn")); e = MatchEngine.FromTestState(st);
            while (e.State.PendingChoice?.Type == "beforeActionReaction") Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
            Check(e.State.Cards[kakihara.Id].Bowed, "the announced action resolved: it had been taken before the lock");
            Check(Options(e, 0, Bow, bower.Id).All(o => !o.Enabled), "but the locked Personality performs nothing further");
        }
        // (4) Order Seppuku is barred by the lock AND, when a Hesitation pass is owed (#356), by that: the pass is the reason shown, and either alone bars it.
        {
            var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
            var locked = Add(s, "training-dishonored-samurai", 0, Zone.Home); locked.Dishonorable = true; locked.ActionLocks.Add(new("test", "Turn"));
            var free = Add(s, "training-dishonored-samurai", 0, Zone.Home); free.Dishonorable = true;
            var e = MatchEngine.FromTestState(s);
            Check(!e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == locked.Id).Enabled && e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == free.Id).Enabled, "the lock bars one Samurai; the other still may");
            var owed = Edit(e, st => st.ForcedPasses.Add(new ForcedPass(0, PrintedPhases.Instance(st.TurnNumber, st.Phase), "test")));
            Check(owed.QueryActions(0).Where(a => a.Type == "orderSeppuku").All(a => !a.Enabled), "with a pass owed neither may");
        }
    }

    private static void CopiedAbilityIsBarred()
    {
        // Fukuzo holds "copied from" the donor a Battle ability (F's copy primitive: a GrantedAbility with CopiedFrom); the lock is on Fukuzo.
        foreach (var locked in new[] { true, false })
        {
            var s = Table();
            var fukuzo = Add(s, "oracle-l5r-2728", 0, Zone.Battlefield, province: 0);
            fukuzo.GrantedAbilities.Add(new(Bow, "fukuzo-wear-skin", "Copy", CopiedFrom: "training-battle-bower", CopiedFromName: "Training Battle bower"));
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (locked) fukuzo.ActionLocks.Add(new("test", "Turn"));
            s.PriorityPlayerId = 0;
            var e = MatchEngine.FromTestState(s);
            var copied = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == fukuzo.Id && a.Label.EndsWith(" · copied from Training Battle bower")).ToArray();
            Eq(copied.Length, 1, "the copied ability is listed");
            var command = new GameCommand("useAbility", 0, e.Version, fukuzo.Id, TargetId: enemy.Id, AbilityId: Bow);
            if (locked)
            {
                Check(!copied[0].Enabled && copied[0].Reason!.Contains("may not perform actions"), "listed disabled with the reason: " + copied[0].Reason);
                Check(Rejection(e, command).Contains("may not perform actions"), "and refused when sent anyway");
                Check(!e.State.Cards[enemy.Id].Bowed, "nothing resolved");
            }
            else
            {
                Check(copied[0].Enabled, "without the lock it is offered: " + copied[0].Reason);
                Accept(e, command); Check(e.State.Cards[enemy.Id].Bowed, "and it resolves");
            }
        }
    }

    // ---- "X Champion" is an ordinary keyword ----------------------------------------------------------------------------------
    private static void ChampionKeywordIsOrdinary()
    {
        var s = Table(Phase.Action, actor: 0);
        var hotako = Add(s, Hotako, 0, Zone.Home);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-champion-item", 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var pay = e.PaymentOptions(0, 1).First().SourceIds;
        Rejection(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: pay, TargetId: plain.Id));
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: pay, TargetId: hotako.Id));
        Eq(e.State.Cards[item.Id].AttachedTo, hotako.Id, "attached to the Obsidian Champion");
        // A Strategy whose performer must be an Obsidian Champion: offered for Hotako alone.
        var s2 = Table(); var h2 = Add(s2, Hotako, 0, Zone.Battlefield, province: 0); var p2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s2, "training-champion-strategy", 0, Zone.Hand); s2.PriorityPlayerId = 0;
        var e2 = MatchEngine.FromTestState(s2);
        var options = Options(e2, 0, "training-champion-boost", strategy.Id);
        Check(options.Count(o => o.Enabled) == 1 && options.Single(o => o.Enabled).Label.Contains($"({h2.Id})"), "Hotako is the one performer offered: " + string.Join("; ", options.Select(o => o.Label)));
        Rejection(e2, Use(e2, 0, "training-champion-boost", strategy.Id, performerId: p2.Id));
        Accept(e2, Use(e2, 0, "training-champion-boost", strategy.Id, performerId: h2.Id));
        Check(e2.State.Cards[h2.Id].Modifiers.Any(m => m.Effect == "training-champion-boost"), "and he performs it");
    }

    // ---- the Lion-Clan entry discount --------------------------------------------------------------------------------------
    private static void LionClanDiscountThroughQuotePurchase()
    {
        MatchEngine Fresh(out string cardId, int owner)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            cardId = s.Players[owner].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = Hotako; s.Cards[cardId].FaceUp = true;
            for (var i = 0; i < 6; i++) Add(s, "training-marketplace", owner, Zone.Home);
            s.Phase = Phase.Dynasty; s.ActivePlayerId = owner; s.PriorityPlayerId = owner;
            return MatchEngine.FromTestState(s);
        }
        var lion = Fresh(out var lionCard, 1);
        var crane = Fresh(out var craneCard, 0);
        var lionQuote = lion.QuotePurchase(1, lionCard); var craneQuote = crane.QuotePurchase(0, craneCard);
        Eq(lionQuote.Cost, 9, "a Lion Clan player pays 2 less"); Eq(craneQuote.Cost, 11, "any other player pays the printed cost");
        Check(lionQuote.Basis!.Contains("Daigotsu Hotako cost 11") && lionQuote.Basis.Contains("− 2 (Lion Clan)"), "the basis names the term: " + lionQuote.Basis);
        Check(!craneQuote.Basis!.Contains("Lion Clan"), "and only for the Lion");
        // The Clan choices are for a Personality of the buyer's own Clan: he is a Spider, so neither applies here.
        Check(lion.QuotePurchase(1, lionCard, clanDiscount: true).Error is not null, "a Spider is not the Lion's own Clan, so the Clan discount is refused");
        // The offered purchase carries the same quote, and the exact payment brings him into play.
        var offered = lion.QueryActions(1).Single(a => a.Type == "purchase" && a.CardId == lionCard);
        Eq(offered.Gold!.Cost, 9, "the option's Gold quote agrees"); Eq(offered.Quote!.Cost, 9);
        var payments = lion.PaymentOptions(1, 9);
        var under = lion.PaymentOptions(1, 8).First();
        Check(under.Produced < 9, "the fixture has a payment that is exactly one Gold short");
        Rejection(lion, new GameCommand("purchase", 1, lion.Version, CardId: lionCard, SourceIds: under.SourceIds));
        var honorBefore = Honor(lion, 1);
        Accept(lion, new GameCommand("purchase", 1, lion.Version, CardId: lionCard, SourceIds: payments.First().SourceIds));
        Check(lion.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 9 Gold"), "9 Gold was charged");
        Eq(lion.State.Cards[lionCard].Zone, Zone.Home, "in play");
        Eq(Honor(lion, 1), honorBefore - 2, "After Hotako enters play: lose 2 Honor, automatically");
        Check(lion.State.PendingChoice is null || lion.State.PendingChoice.Type != "entryReaction", "no Reaction is offered for a mandatory trait");
        Rejection(crane, new GameCommand("purchase", 0, crane.Version, CardId: craneCard, SourceIds: crane.PaymentOptions(0, 9).First().SourceIds));
        Accept(crane, new GameCommand("purchase", 0, crane.Version, CardId: craneCard, SourceIds: crane.PaymentOptions(0, 11).First().SourceIds));
        Check(crane.State.Events.Last(x => x.Type == "payment").Message.Contains("paid 11 Gold"), "11 Gold was charged to the Crane");
    }

    private static void ClanDiscountIsGeneral()
    {
        // "<clan>-clan-discount-N" for an Equip (the Purchase side is Hotako's, above): N less for that Clan's player only, never below 0 -- and the older
        // one-less terms (Satoshi's Dual Warfans, Legion of the Bat; GoldParityTests) are the N = 1 case of the same term.
        var s = GoldParityTests.Action();
        var bigItem = Add(s, "training-clan-discount-item", 0, Zone.Hand); var cheapItem = Add(s, "training-clan-discount-cheap-item", 0, Zone.Hand);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        var lionItem = Add(s, "training-lion-clan-discount-item", 0, Zone.Hand);
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        ActionOption Equip(CardInstance item) => e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == item.Id);
        Eq(Equip(bigItem).Gold!.Cost, 2, "5 - 3 for the Crane player"); Check(Equip(bigItem).Gold!.Basis.Contains("− 3 (Crane Clan)"), Equip(bigItem).Gold!.Basis);
        Eq(Equip(cheapItem).Gold!.Cost, 0, "2 - 3 stops at 0"); Check(Equip(cheapItem).Gold!.Basis.Contains("(min 0)"), Equip(cheapItem).Gold!.Basis);
        Eq(Equip(lionItem).Gold!.Cost, 4, "a Lion Clan term does nothing for the Crane player"); Check(!Equip(lionItem).Gold!.Basis.Contains("−"), Equip(lionItem).Gold!.Basis);
        var s2 = GoldParityTests.Action(); s2.ActivePlayerId = 1; s2.PriorityPlayerId = 1;
        var lionPlayersItem = Add(s2, "training-lion-clan-discount-item", 1, Zone.Hand); var w2 = Add(s2, "training-lion-samurai", 1, Zone.Home);
        for (var i = 0; i < 6; i++) Add(s2, "training-marketplace", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var quote = e2.QueryActions(1).First(a => a.Type == "equip" && a.CardId == lionPlayersItem.Id).Gold!;
        Eq(quote.Cost, 2, "4 - 2 for the Lion player"); Check(quote.Basis.Contains("− 2 (Lion Clan)"), quote.Basis);
    }
}
