using static RegressionAssertions;
using L5R.Core;

// Daigotsu Bofana (oracle-l5r-1706) and Daigotsu Bofana - exp (oracle-l5r-10315), issue #333, with the general pieces they are the
// first consumers of: the once-per-PHASE limit (PhaseLimits.cs), the forced discard (a queue drained by the deferred-window
// machinery, or opened at announcement with the action held), and the additional action that plays a card from a discard pile
// (AdditionalActionGrant.FromDiscard). Every reading is in docs/decisions/2026-09-19-daigotsu-bofana.md. Each test builds an explicit
// position and drives it only through QueryActions and the commands the engine offers; the table is the Battle Phase with player 1
// the Attacker (ActivePlayerId, the one acting) and player 0 the Defender who controls Bofana at the current battlefield, the
// Defender's province 0.
internal static class DaigotsuBofanaTests
{
    private const string Bofana = "oracle-l5r-1706", BofanaExp = "oracle-l5r-10315", BofanaEffect = "phase:bofana-targeted-discard";
    private const string Bow = "training-bow-at-battle";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: both cards are the printed ones (Force 7, Chi 4, no Honor Requirement, Cost 10 / 11, Personal Honor 0, Loyal Paragon Spider), whole -- the base card's trait is an Effect, the Experienced card's entry is an Effect plus EntryHonorLoss 2 and its Battle action an ability -- with the scans' art shipped and the general pieces registered", PublishedShape),
            ("Base Bofana: an enemy Strategy targets him -> the enemy discards a card of THEIR choice (offered through QueryActions, never the Strategy being played), the action then resolves as if nothing had happened, and the discard reveals nothing to his controller", StrategyTargetsBofana),
            ("Base Bofana: a Personality performer with current Force 7 or more (a Force modifier counts, the printed value alone does not) causes no discard; below 7 it does", PersonalityForceThreshold),
            ("Base Bofana: the threshold is his BASE Force (printed 7), not his current Force -- +3 on him does not let a Force 8 performer be caught, and -3 does not let a Force 5 one off", BaseNotCurrentForce),
            ("Base Bofana: an Item's or Spell's action is performed by the Personality it is attached to (that Personality's CURRENT Force is compared), whatever the attachment's own Force", AttachmentUsesWielderForce),
            ("Base Bofana: a Follower's action is performed with the Follower's OWN current Force -- below, at and above 7, and never its wielder's", FollowerUsesOwnForce),
            ("Base Bofana: a card that produces an action with no performer (Strategy, Ring, Holding, Region, Celestial) counts as Force 0 however strong the army beside it, and a Strategy that names a performing Personality is performed by him", NoPerformerCountsAsZero),
            ("Base Bofana: the first targeting of a phase spends the trigger whether or not the Force condition held or a hand existed; a second targeting the same phase does nothing; a new phase (Action -> Attack, or the same phase two turns later) fires again; each Bofana in play keeps his own", OncePerPhaseAndPerInstance),
            ("The once-per-phase limit knows the six printed phases: every sub-step of the Attack Phase (assignment, both Maneuvers, battle selection, every battle) is ONE phase, Dynasty's province discard is part of Dynasty, and each of Straighten, Events, Action, Attack, Dynasty and End is distinct within a turn and across turns", SixPrintedPhases),
            ("Base Bofana across a real turn: driven only through offered commands from the Action Phase through attack declaration, defense, battle selection and the battle, the Action Phase's first targeting fires, its second does not, the Attack Phase's first fires again and its second does not", RealTurnAcrossPhases),
            ("Base Bofana: only ANOTHER player's action counts -- his own controller's action, an action targeting a different card, a Reaction that names him and Bofana out of play never trigger it; a bowed Bofana still does (a trait works while bowed)", OnlyAnotherPlayersActions),
            ("Base Bofana: an empty hand (or one holding only the card being played) means nothing happens and the action carries straight on; with one card left the player must discard exactly it", EmptyHandNothingHappens),
            ("Base Bofana resolves before any Reaction: with a Champion of Thunder on his unit the discard is asked first, then the before-action window opens, and the action then resolves; every offered discard validates on a fresh engine", TraitResolvesBeforeReactions),
            ("Base Bofana: Bayushi Kahoku - exp's Attack, whose own target rides in CardIds beside a separate -4F target, targets Bofana through either", KahokuAttackTargetsBofana),
            ("Bofana - exp entering play by Purchase (a proclaim-eligible Spider player) or Overlay: his controller loses 2 Honor and each other player discards a card of their choice -- two if they are Crab Clan (the Clan of their Stronghold) -- or as many as they hold", ExpEntryDiscards),
            ("Proclaim-eligible entries (a Personality of the buyer's own Clan, bought without the Clan discount) get their mandatory entry effects like every other entry -- this was skipped before issue #333 -- and Proclaim comes FIRST: the Proclaim gain and an entering Honor loss are two Honor changes in that order (checked in the event log), never netted; the nine already-wired Personalities each still behave, with Proclaim taken or passed", ProclaimThenEntryEffects),
            ("Bofana - exp's play from the discard pile follows the same-turn rule PER ACTION (Mendon's ruling): the same action already performed this turn (from hand, or earlier from the pile) is refused, another action of the same card is allowed, and the next turn allows both", ExpDarkVirtueSameTurnRulePerAction),
            ("Bofana - exp's Battle action: a Dark Virtue with a Battle action in your discard pile is targetable (nothing else is), and the additional action it grants plays exactly that card for one of its Battle actions of your choice, then removes it from the game", ExpPlaysDarkVirtueFromDiscard),
            ("Bofana - exp's additional action is only ever the one: passing or taking any other action forfeits it, the card stays in the discard pile, and a card cannot be played from a discard pile without the grant", ExpGrantIsForfeitable),
        ];
        runner.Run("daigotsu-bofana", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"bo-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // MatchEngine.State is a detached copy, so a mid-test change is made on a copy and the engine rebuilt from it.
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) { var st = e.State; edit(st); return MatchEngine.FromTestState(st); }

    private static void Force(CardInstance card, int amount) => card.Modifiers.Add(new("test-source", "test", "Force", amount));

    // The economy scenario with both hands emptied, so every hand card in a test is one the test put there.
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    private sealed record Rig(MatchState S, CardInstance Bofana, CardInstance Bower);

    // Bofana (player 0) at the battlefield; player 1 has a Battle-action Personality there (printed Force 2, `force` in all
    // once modified) and `hand` plain cards in hand.
    private static Rig Battle(int force = 2, int hand = 2, string bofana = Bofana)
    {
        var s = Table();
        var b = Add(s, bofana, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        Force(bower, force - 2);
        for (var i = 0; i < hand; i++) Add(s, "training-farm", 1, Zone.Hand);
        return new(s, b, bower);
    }

    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability
            && a.Command.PerformerId == performerId && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!.Command! with { TargetId = targetId };
    }

    private static ActionOption[] DiscardOffers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "forcedDiscard").ToArray();

    private static bool Asked(MatchEngine e) => e.State.PendingChoice?.Type == "forcedDiscard";

    // Answers the open discard with `cardId`, through the offered option.
    private static void Discard(MatchEngine e, int player, string cardId)
    {
        var option = DiscardOffers(e, player).SingleOrDefault(a => a.CardId == cardId);
        Check(option is { Enabled: true, Command: not null }, $"discarding {cardId} was not offered");
        Accept(e, option!.Command!);
    }

    private static string Fired(MatchEngine e, CardInstance bofana) => e.State.Cards[bofana.Id].AbilityUses.ContainsKey(BofanaEffect) ? "spent" : "unspent";

    // Player 1 has Bofana's controller's Bower-shaped card bow him; true when that action asked for a discard.
    private static bool BowAsks(MatchEngine e, CardInstance actorCard, CardInstance bofana, string? ability = null, string? performerId = null, int player = 1)
    {
        Accept(e, Offer(e, player, ability ?? Bow, actorCard.Id, bofana.Id, performerId));
        return Asked(e);
    }

    // ---- published shape -----------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.PersonalityWave29.Cards.Length, 2, "Both Bofanas");
        var b = CardCatalog.Get(Bofana);
        Check(!b.ScenarioOnly && b.Type == CardType.Personality && b.Name == "Daigotsu Bofana" && b.Clan == "Spider");
        // Verified against the scan (Force 7 / Chi 4 / no Honor Requirement / Cost 10 / Personal Honor 0).
        Eq(b.Force, 7); Eq(b.Chi, 4); Eq(b.GoldCost, 10); Eq(b.PersonalHonor, 0); Check(b.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Spider Clan", "Shadowlands", "Samurai", "Dark Scion of Fire", "Paragon", "Loyal" }) Check(b.Keywords!.Contains(k), k);
        Check(!b.Keywords!.Contains("Unique"), "the base card is not Unique");
        Eq(b.Text, "After the first time each phase another player's action targets Bofana: If the action was not performed by a card with (current) Force equal to or higher than Bofana's base Force, the player discards a card.");
        Eq(b.Effect, "bofana-targeted-discard"); Eq(b.Abilities!.Length, 0);
        var x = CardCatalog.Get(BofanaExp);
        Check(!x.ScenarioOnly && x.Type == CardType.Personality && x.Name == "Daigotsu Bofana" && x.Clan == "Spider");
        Eq(x.Force, 7); Eq(x.Chi, 4); Eq(x.GoldCost, 11); Eq(x.PersonalHonor, 0); Check(x.HonorRequirement is null);
        foreach (var k in new[] { "Spider Clan", "Imperial", "Shadowlands", "Samurai", "Paragon", "Scout", "Experienced", "Loyal", "Unique", "Dark Scion of Fire", "Imperial Explorer" }) Check(x.Keywords!.Contains(k), k);
        Eq(x.Text, "After Bofana enters play: Lose 2 Honor and all other players must discard a card, or two cards if they are Crab Clan.\nBattle: Target a Dark Virtue in your discard pile: Take an additional action to play it for one of its Battle actions. After it resolves, remove the card from the game.");
        Eq(x.EntryHonorLoss, 2); Eq(x.Effect, "bofana-entry-discard"); Check(x.Abilities!.SequenceEqual(new[] { "bofana-exp-dark-virtue" }));
        Eq(AbilityCatalog.All["bofana-exp-dark-virtue"].Timing, "Battle"); Eq(AbilityCatalog.All["bofana-exp-dark-virtue"].Target, "own-dark-virtue-in-discard");
        foreach (var id in new[] { Bofana, BofanaExp })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    // ---- the base card ---------------------------------------------------------------------------------------------
    private static void StrategyTargetsBofana()
    {
        var s = Table();
        var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-bow-strategy", 1, Zone.Hand);
        var keep = Add(s, "training-farm", 1, Zone.Hand);
        var give = Add(s, "training-item", 1, Zone.Hand);
        var third = Add(s, "training-follower", 1, Zone.Hand);
        var controllerHand = Add(s, "training-farm", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, Bow, strategy.Id, bofana.Id));
        Eq(e.State.PendingChoice?.Type, "forcedDiscard"); Eq(e.State.PendingChoice?.PlayerId, 1, "the enemy who acted discards");
        Eq(e.State.PriorityPlayerId, 1);
        Check(!e.State.Cards[bofana.Id].Bowed && e.State.Cards[strategy.Id].Zone == Zone.Hand, "the action has not resolved yet: the trait comes first");
        // The offer: one option per OTHER hand card of the enemy, never the Strategy being played.
        var offers = DiscardOffers(e, 1);
        Check(offers.Select(a => a.CardId).ToHashSet().SetEquals([keep.Id, give.Id, third.Id]), "one offer per other hand card");
        Check(offers.All(a => a.Enabled && a.Command is { Type: "forcedDiscard", PlayerId: 1 }), "complete, enabled commands");
        // The controller is only told to wait: neither the offers nor the enemy's hand cross to him.
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "Bofana's controller holds no decision");
        var view = e.GetView(0);
        Eq(view.Players[1].Hand.Length, 0, "the enemy's hand is private"); Eq(view.Players[1].HandCount, 4);
        HiddenIdsAbsent(view, [strategy.Id, keep.Id, give.Id, third.Id]);
        Check(e.GetView(1).Players[1].Hand.Length == 4, "the discarding player sees his own hand");
        // Only the discarding player, only a hand card, only one not used by the action.
        Reject(e, new GameCommand("forcedDiscard", 0, e.Version, TargetId: controllerHand.Id));
        Reject(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: controllerHand.Id));
        Reject(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: strategy.Id));
        Reject(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: null));
        Reject(e, new GameCommand("pass", 1, e.Version));
        // The enemy chooses (not the first, not the last), and then the deferred action carries on.
        Discard(e, 1, give.Id);
        Eq(e.State.Cards[give.Id].Zone, Zone.FateDiscard, "the chosen card is discarded"); Check(e.State.Cards[give.Id].FaceUp, "to the public discard pile");
        Check(e.State.Players[1].Hand.Contains(keep.Id) && e.State.Players[1].Hand.Contains(third.Id) && !e.State.Players[1].Hand.Contains(give.Id), "only the chosen card left the hand");
        Check(e.State.PendingChoice is null, "the window is closed");
        Check(e.State.Cards[bofana.Id].Bowed, "the action resolved");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "and the Strategy was spent");
        Eq(e.State.PriorityPlayerId, 0, "priority passed to the opponent as after any action");
        Eq(e.State.Players[0].Hand.Count, 1, "the controller's hand is untouched");
        Check(e.State.Events.Any(x => x.Type == "handDiscard" && x.CardId == give.Id), "the discard is in the public log");
        Eq(Fired(e, bofana), "spent");
    }

    private static void PersonalityForceThreshold()
    {
        foreach (var (force, asks) in new[] { (6, true), (7, false), (8, false), (2, true), (12, false) })
        {
            var r = Battle(force);
            var e = MatchEngine.FromTestState(r.S);
            Eq(e.GetView(0).Players[1].Army.Single(c => c.Id == r.Bower.Id).Force, force, "the fixture's current Force");
            Eq(BowAsks(e, r.Bower, r.Bofana), asks, $"a Personality with current Force {force}");
            if (asks) Discard(e, 1, e.State.Players[1].Hand[1]);
            Check(e.State.Cards[r.Bofana.Id].Bowed && e.State.PendingChoice is null, "the action resolved either way");
            Eq(Fired(e, r.Bofana), "spent", "the trigger is spent by the first targeting, asked or not");
        }
        {
            // CURRENT Force, not printed: a printed 2 raised to 7 escapes; a printed 7 lowered to 6 does not.
            var r = Battle(2); Force(r.Bower, 5);
            var e = MatchEngine.FromTestState(r.S);
            Check(!BowAsks(e, r.Bower, r.Bofana), "printed 2 + 5 = 7 is high enough");
        }
        // A printed Force 7 is not enough by itself either: -1 on him makes it a current 6.
        foreach (var (modifier, asks) in new[] { (0, false), (-1, true) })
        {
            var r = Battle(2);
            r.Bower.DefinitionId = "training-force-seven-bower"; r.Bower.Modifiers.Clear(); Force(r.Bower, modifier);
            var e = MatchEngine.FromTestState(r.S);
            Eq(BowAsks(e, r.Bower, r.Bofana), asks, $"printed 7, modifier {modifier}");
        }
    }

    private static void BaseNotCurrentForce()
    {
        foreach (var (bofanaMod, performer, asks) in new[] { (3, 8, false), (3, 6, true), (-3, 5, true), (-3, 7, false), (0, 7, false), (-6, 6, true), (5, 7, false) })
        {
            var r = Battle(performer);
            Force(r.Bofana, bofanaMod);
            var e = MatchEngine.FromTestState(r.S);
            Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == r.Bofana.Id).Force, 7 + bofanaMod, "his current Force");
            Eq(BowAsks(e, r.Bower, r.Bofana), asks, $"Bofana current {7 + bofanaMod}, performer {performer}: the threshold is his printed 7");
        }
    }

    private static void AttachmentUsesWielderForce()
    {
        foreach (var (kind, def) in new[] { ("Item", "training-bow-item"), ("Spell", "training-bow-spell") })
            foreach (var (wielderForce, asks) in new[] { (6, true), (7, false), (9, false), (2, true) })
            {
                var s = Table();
                var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
                var wielder = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // printed Force 5
                Force(wielder, wielderForce - 5);
                var attachment = Add(s, def, 1, Zone.Attached, province: 0, parent: wielder.Id);
                Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Eq(BowAsks(e, attachment, bofana), asks, $"{kind} on a Personality with current Force {wielderForce}");
            }
        {
            // The wielder's CURRENT Force: printed 5 + 2 modifier = 7 escapes; the Item's own printed Force (+2 here) changes what "current" is.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var wielder = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            var item = Add(s, "training-bow-item", 1, Zone.Attached, province: 0, parent: wielder.Id);
            Add(s, "training-item", 1, Zone.Attached, province: 0, parent: wielder.Id); // an Item adding 2 Force to the wielder: 5 + 2 = 7
            Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, item, bofana), "the wielder's current Force includes his Items' Force: 7");
        }
    }

    private static void FollowerUsesOwnForce()
    {
        // Followers are compared by their own current Force (printed 3 here, modified), never their wielder's: the wielder is Force 9 throughout.
        foreach (var (modifier, asks) in new[] { (0, true), (2, true), (3, true), (4, false), (5, false), (-3, true) })
        {
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var wielder = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Force(wielder, 4);
            var follower = Add(s, "training-bow-follower", 1, Zone.Attached, province: 0, parent: wielder.Id); Force(follower, modifier);
            Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var own = e.GetView(0).Players[1].Army.Single(c => c.Id == follower.Id).Force;
            Eq(own, Math.Max(0, 3 + modifier), "the Follower's own current Force");
            Eq(BowAsks(e, follower, bofana), asks, $"Follower with current Force {own} on a Force 9 wielder");
        }
    }

    private static void NoPerformerCountsAsZero()
    {
        // A strong friendly army (Force 9) stands at the battlefield the whole time: it performs nothing.
        foreach (var def in new[] { "training-bow-strategy", "training-bow-ring", "training-bow-holding", "training-bow-region", "training-bow-celestial" })
        {
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var strong = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Force(strong, 4);
            var zone = def switch { "training-bow-strategy" => Zone.Hand, "training-bow-ring" => Zone.Home, "training-bow-holding" => Zone.Home, "training-bow-region" => Zone.Region, _ => Zone.Celestial };
            var card = Add(s, def, 1, zone, province: def == "training-bow-region" ? 0 : null);
            Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(BowAsks(e, card, bofana), $"{def}: no performer, Force 0, so the discard happens");
            Check(e.QueryActions(1).Count(a => a.Type == "forcedDiscard") == 1, "one offer: the only other card in hand");
        }
        {
            // A Strategy that NAMES a performing Personality (Unexpected) is performed by him: his Force decides.
            foreach (var (force, asks) in new[] { (6, true), (7, false) })
            {
                var s = Table();
                var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
                var performer = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Force(performer, force - 5);
                var strategy = Add(s, "oracle-l5r-9940", 1, Zone.Hand);
                Add(s, "training-farm", 1, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                Accept(e, Offer(e, 1, "unexpected", strategy.Id, bofana.Id, performer.Id));
                Eq(Asked(e), asks, $"Unexpected with a Force {force} performing Personality");
            }
        }
    }

    private static void OncePerPhaseAndPerInstance()
    {
        {
            // The first targeting spends the trigger even though it asked for nothing (Force 9); the second, in the same phase, asks nothing of a Force 1.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var strong = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); Force(strong, 7);
            var weak = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); Force(weak, -1);
            Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, strong, bofana), "Force 9: nothing asked");
            Eq(Fired(e, bofana), "spent", "but the first targeting used the trigger up");
            Accept(e, new GameCommand("pass", 0, e.Version)); // the Defender passes; the Attacker acts again
            Check(!BowAsks(e, weak, bofana), "the same phase: a Force 1 performer is not asked for anything");
        }
        {
            // An empty hand spends it too: a card drawn later does not bring the trigger back in the same phase.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var a = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var b = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, a, bofana), "no hand: nothing to discard");
            Eq(Fired(e, bofana), "spent");
            Accept(e, new GameCommand("pass", 0, e.Version));
            e = Edit(e, st => { Add(st, "training-farm", 1, Zone.Hand); st.Cards[bofana.Id].Bowed = false; });
            Check(!BowAsks(e, b, bofana), "a card in hand now, but the trigger was already used this phase");
            Eq(e.State.Players[1].Hand.Count, 1, "and the card is still in his hand");
        }
        {
            // Each Bofana keeps his own trigger: two in play, the same phase, both fire once.
            var s = Table();
            var one = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var two = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            var a = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var b = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var c = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            for (var i = 0; i < 4; i++) Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(BowAsks(e, a, one), "the first Bofana fires"); Discard(e, 1, s.Players[1].Hand[0]);
            Accept(e, new GameCommand("pass", 0, e.Version));
            Check(!BowAsks(e, b, one), "the same Bofana does not fire twice in the phase");
            Accept(e, new GameCommand("pass", 0, e.Version));
            Check(BowAsks(e, c, two), "the other Bofana has not fired yet: his own limit"); Discard(e, 1, e.State.Players[1].Hand[0]);
            Eq(e.State.Players[1].Hand.Count, 2, "two discards, one per Bofana");
        }
        {
            // The Action Phase's first targeting spends it; the same phase two turns later (the next time the same player is active) is a new
            // phase instance and fires again. (Action -> Attack in the same turn is driven for real by RealTurnAcrossPhases.)
            var s = Table(Phase.Action);
            var bofana = Add(s, Bofana, 0, Zone.Home);
            var source = Add(s, "training-bow-any-source", 1, Zone.Home); Force(source, 0);
            for (var i = 0; i < 6; i++) Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            void Reset(int turns = 0) => e = Edit(e, st => { st.Cards[bofana.Id].Bowed = false; st.Cards[source.Id].AbilityUses.Clear(); st.PriorityPlayerId = 1; st.TurnNumber += turns; });
            Check(BowAsks(e, source, bofana, "training-bow-any"), "Action Phase: fires"); Discard(e, 1, e.State.Players[1].Hand[0]);
            Reset();
            Check(!BowAsks(e, source, bofana, "training-bow-any"), "the same Action Phase: nothing");
            Reset(2);
            Check(BowAsks(e, source, bofana, "training-bow-any"), "the same phase on a later turn: fires again"); Discard(e, 1, e.State.Players[1].Hand[0]);
            Eq(e.State.Players[1].Hand.Count, 4, "two discards in all");
        }
    }

    private static void SixPrintedPhases()
    {
        var perPhase = new Dictionary<int, Phase[]>
        {
            [0] = [Phase.Straighten], [1] = [Phase.Events], [2] = [Phase.Action],
            [3] = [Phase.AttackDeclaration, Phase.InfantryDeclaration, Phase.DefenseAssignment, Phase.CavalryDeclaration, Phase.CavalryDefense, Phase.BattleSelection, Phase.Battle],
            [4] = [Phase.Dynasty, Phase.DynastyDiscard], [5] = [Phase.End],
        };
        foreach (var (printed, phases) in perPhase)
            foreach (var phase in phases) Eq(PrintedPhases.Of(phase), printed, $"{phase} belongs to printed phase {printed}");
        var seen = new HashSet<int>();
        for (var turn = 0; turn < 5; turn++)
            foreach (var (printed, phases) in perPhase)
            {
                var instance = PrintedPhases.Instance(turn, phases[0]);
                Check(seen.Add(instance), $"turn {turn} printed phase {printed} is its own instance");
                foreach (var phase in phases) Eq(PrintedPhases.Instance(turn, phase), instance, $"{phase} is the same instance as {phases[0]}");
            }
        Eq(seen.Count, 30, "six phases x five turns are thirty distinct instances");
        Check(!seen.Contains(PrintedPhases.Instance(0, Phase.Finished)), "a finished match is no phase");
    }

    // Drives the start of player 1's turn purely through offered commands. Player 0 controls Bofana at Home and defends with him.
    private static void RealTurnAcrossPhases()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Action; s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.TurnNumber = 1;
        var bofana = Add(s, Bofana, 0, Zone.Home);
        var source = Add(s, "training-bow-any-source", 1, Zone.Home); Force(source, -1);
        var attacker = Add(s, "training-battle-bower", 1, Zone.Home); Force(attacker, -1);
        var second = Add(s, "training-battle-bower", 1, Zone.Home); Force(second, -1);
        for (var i = 0; i < 12; i++) Add(s, "training-farm", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        int Hand() => e.State.Players[1].Hand.Count;
        void Ready(bool clearUses = false) => e = Edit(e, st =>
        {
            foreach (var c in st.Cards.Values.Where(c => c.Id == bofana.Id || c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Battlefield)) { c.Bowed = false; if (clearUses) c.AbilityUses.Remove("training-bow-any"); }
        });
        void Step(int player, string type)
        {
            var option = e.QueryActions(player).FirstOrDefault(a => a.Type == type && a.Enabled && a.Command is not null);
            Check(option is not null, $"{type} was not offered to player {player} in {e.State.Phase}");
            Accept(e, option!.Command!);
        }
        // Action Phase: the first targeting asks, a second in the same phase does not.
        Eq(e.State.Phase, Phase.Action);
        Check(BowAsks(e, source, bofana, "training-bow-any"), "Action Phase: the first targeting asks"); Discard(e, 1, e.State.Players[1].Hand[0]);
        Eq(Hand(), 11);
        Step(0, "pass"); Ready(clearUses: true);
        Check(!BowAsks(e, source, bofana, "training-bow-any"), "the same Action Phase: not again");
        Eq(Hand(), 11);
        // Both pass: the Attack Phase begins. Player 1 attacks province 0 with two units and player 0 defends with Bofana.
        Ready(); Step(0, "pass"); Step(1, "pass");
        Eq(PrintedPhases.Of(e.State.Phase), 3, "the Attack Phase");
        Step(1, "declareAttack");
        Accept(e, e.QueryActions(1).First(a => a.Type == "attack" && a.Enabled).Command! with { Assignments = new Dictionary<string, int> { [attacker.Id] = 0, [second.Id] = 0 } });
        Accept(e, e.QueryActions(0).First(a => a.Type == "assignDefense" && a.Enabled).Command! with { Assignments = new Dictionary<string, int> { [bofana.Id] = 0 } });
        while (e.State.Phase != Phase.BattleSelection) Step(e.State.PriorityPlayerId, "pass"); // Cavalry Maneuvers: nobody has Cavalry
        Step(1, "selectBattle");
        Eq(e.State.Phase, Phase.Battle); Eq(e.State.CurrentBattleProvince, 0);
        Check(e.State.Cards[attacker.Id].Zone == Zone.Battlefield && e.State.Cards[bofana.Id].Zone == Zone.Battlefield, "both sides are at the battlefield");
        Ready();
        while (e.State.PriorityPlayerId != 1) Step(e.State.PriorityPlayerId, "pass"); // the Defender has the first opportunity and passes
        // Attack Phase: a new phase, so the first targeting here asks again -- and only the first.
        Check(BowAsks(e, attacker, bofana, Bow), "the Attack Phase is a new phase: fires again"); Discard(e, 1, e.State.Players[1].Hand[0]);
        Eq(Hand(), 10);
        Step(0, "pass"); Ready();
        Check(!BowAsks(e, second, bofana, Bow), "later in the same Attack Phase: not again");
        Eq(Hand(), 10);
    }

    private static void OnlyAnotherPlayersActions()
    {
        {
            // His own controller's action (an Open action that may target either side) never triggers it.
            var s = Table(Phase.Action, actor: 0);
            var bofana = Add(s, Bofana, 0, Zone.Home);
            var source = Add(s, "training-bow-any-source", 0, Zone.Home);
            Add(s, "training-farm", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, source, bofana, "training-bow-any", player: 0), "his own controller's action targets him: nothing");
            Check(e.State.Cards[bofana.Id].Bowed, "the action simply resolved");
            Eq(Fired(e, bofana), "unspent", "and did not use up the trigger");
            // The other player's identical action does.
            CardInstance enemySource = null!;
            e = Edit(e, st => { st.Cards[bofana.Id].Bowed = false; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; enemySource = Add(st, "training-bow-any-source", 1, Zone.Home); Force(enemySource, -1); Add(st, "training-farm", 1, Zone.Hand); });
            Check(BowAsks(e, enemySource, bofana, "training-bow-any"), "the other player's action asks");
        }
        {
            // An action that targets a different card is not "targets Bofana".
            var r = Battle(2);
            var other = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(r.S);
            Check(!BowAsks(e, r.Bower, other), "a different card is targeted");
            Eq(Fired(e, r.Bofana), "unspent");
        }
        {
            // A Reaction that names him IS an action (rules lead: "Reactions count as actions"): Tamori Ruya's bow (a Reaction) at Bofana, from
            // the other player, inside a bow window. It spends his once-per-phase trigger and asks for the discard, over the window it was
            // played in, which is set aside and comes back once the action has carried on.
            var s = Table();
            var ruya = Add(s, "oracle-l5r-7789", 1, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var bowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            Add(s, "training-farm", 1, Zone.Hand);
            // Ruya reacts to HER OWN controller's action bowing a Personality: player 1 bows a Personality of player 0's, then her reaction bows Bofana.
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, Bow, bower.Id, bowed.Id));
            Eq(e.State.PendingChoice?.Type, "bowReaction", "Ruya's window");
            Eq(Fired(e, bofana), "unspent", "the first action did not target him");
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: ruya.Id, TargetId: bofana.Id, AbilityId: "ruya-bow"));
            Eq(e.State.PendingChoice?.Type, "forcedDiscard", "a Reaction is an action: targeting Bofana asks for the discard");
            Eq(e.State.SuspendedWindows.Count, 1); Eq(e.State.SuspendedWindows[0].Choice.Type, "bowReaction", "over the window it was played in");
            Eq(Fired(e, bofana), "spent", "the first targeting of the phase spends his trigger");
            Check(!e.State.Cards[bofana.Id].Bowed, "Ruya's bow carries on only once the discard is answered");
            Discard(e, 1, e.State.Players[1].Hand[0]);
            Eq(e.State.PendingChoice?.Type, "bowReaction", "the window Ruya was played in is open again"); Eq(e.State.SuspendedWindows.Count, 0);
            Check(e.State.Cards[bofana.Id].Bowed && e.State.Players[1].Hand.Count == 0, "the Reaction resolved after the discard");
        }
        {
            // Not in play: nothing to trigger.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.FateDiscard);
            var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, bower, other), "Bofana in a discard pile watches nothing");
            Eq(Fired(e, bofana), "unspent");
        }
        {
            // A trait works while bowed.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, bowed: true, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(BowAsks(e, bower, bofana), "a bowed Bofana still triggers");
        }
    }

    private static void EmptyHandNothingHappens()
    {
        {
            var r = Battle(2, hand: 0);
            var e = MatchEngine.FromTestState(r.S);
            Check(!BowAsks(e, r.Bower, r.Bofana), "an empty hand: nothing happens");
            Check(e.State.Cards[r.Bofana.Id].Bowed && e.State.PendingChoice is null, "the action carries straight on");
            Check(!e.State.Events.Any(x => x.Type is "forcedDiscardOffered" or "handDiscard"), "and no discard was ever announced");
        }
        {
            // A hand holding only the Strategy being played is an empty hand: it is in limbo, not in hand.
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-bow-strategy", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!BowAsks(e, strategy, bofana), "only the played Strategy in hand: nothing to discard");
            Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "and it was played normally");
        }
        {
            // One card left: exactly it is offered, and discarding it empties the hand.
            var r = Battle(2, hand: 1);
            var e = MatchEngine.FromTestState(r.S);
            Check(BowAsks(e, r.Bower, r.Bofana));
            var only = r.S.Players[1].Hand.Single();
            Check(DiscardOffers(e, 1).Select(a => a.CardId).SequenceEqual([only]), "exactly the one card is offered");
            Discard(e, 1, only);
            Eq(e.State.Players[1].Hand.Count, 0);
        }
    }

    private static void TraitResolvesBeforeReactions()
    {
        // Champion of Thunder (oracle-l5r-1292) on Bofana: "after another player's action targets a card in this unit: negate the action's effects on
        // this card". The trait is a trait, so it resolves first; the reaction window opens once the discard is answered.
        var s = Table();
        var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
        var champion = Add(s, "oracle-l5r-1292", 0, Zone.Attached, province: 0, parent: bofana.Id);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var a = Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, Bow, bower.Id, bofana.Id));
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "the trait first");
        // A fresh engine given each offered command accepts it (every offered discard is a complete, valid command).
        foreach (var offer in DiscardOffers(e, 1))
        {
            var fresh = MatchEngine.FromTestState(Copy(s));
            Accept(fresh, Offer(fresh, 1, Bow, bower.Id, bofana.Id));
            Accept(fresh, offer.Command!);
            Eq(fresh.State.PendingChoice?.Type, "beforeActionReaction", "then the reaction window, with the action still deferred");
            Check(!fresh.State.Cards[bofana.Id].Bowed, "the action has not resolved yet");
        }
        Discard(e, 1, a.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
        Check(!e.State.Cards[bofana.Id].Bowed);
        Accept(e, new GameCommand("declineReaction", 0, e.Version)); Accept(e, new GameCommand("declineReaction", 1, e.Version));
        Check(e.State.PendingChoice is null && e.State.Cards[bofana.Id].Bowed, "and the action finally resolves");
        Eq(champion.Zone, Zone.Attached);
    }

    private static MatchState Copy(MatchState s) => System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(s))!;

    private static void KahokuAttackTargetsBofana()
    {
        // Kahoku - exp (oracle-l5r-807): TargetId is the -4F target; the Ranged 3 Attack's own target rides in CardIds.
        foreach (var attackTargetsBofana in new[] { true, false })
        {
            var s = Table();
            var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
            Force(bofana, -5); // current Force 2: a legal target for a Ranged 3 Attack (his base Force, 7, is what the trait compares)
            var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var kahoku = Add(s, "oracle-l5r-807", 1, Zone.Battlefield, province: 0);
            Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var minus = attackTargetsBofana ? other.Id : bofana.Id;
            var attack = new GameCommand("useAbility", 1, e.Version, CardId: kahoku.Id, TargetId: minus, AbilityId: "kahoku-exp-attack", CardIds: attackTargetsBofana ? [bofana.Id] : null);
            Accept(e, attack);
            Check(Asked(e), attackTargetsBofana ? "the Attack targets Bofana (through CardIds): Kahoku's Force 5 is below 7" : "the -4F targets Bofana (through TargetId)");
            Discard(e, 1, e.State.Players[1].Hand[0]);
            Check(e.State.PendingChoice is null, "the action carried on");
        }
    }

    // ---- Bofana - exp's entry -------------------------------------------------------------------------------------------
    private static MatchEngine SpiderTable(out CardInstance exp, string opponentClan, int opponentHand, int gold = 11)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Players[0].Clan = "Spider"; s.Players[1].Clan = opponentClan;
        for (var i = 0; i < (gold + 1) / 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        exp = s.Cards[s.Players[0].Provinces[0].CardId!]; exp.DefinitionId = BofanaExp; exp.FaceUp = true;
        for (var i = 0; i < opponentHand; i++) Add(s, "training-farm", 1, Zone.Hand);
        Add(s, "training-farm", 0, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void ExpEntryDiscards()
    {
        var owedRound = 0;
        foreach (var (clan, hand, discards) in new[] { ("Lion", 3, 1), ("Crab", 3, 2), ("Crab", 1, 1), ("Crab", 0, 0), ("Lion", 0, 0), ("Crane", 2, 1) })
        {
            var e = SpiderTable(out var exp, clan, hand);
            var honor = e.State.Players[0].Honor; var oppHonor = e.State.Players[1].Honor; var ownHand = e.State.Players[0].Hand.Count;
            var buy = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == exp.Id && a.Enabled && a.Command is { ClanDiscount: false, ClanAllegiance: false });
            Accept(e, buy.Command!);
            Eq(e.State.Players[1].Honor, oppHonor, "the other player loses none");
            // Bofana is a Spider Clan Personality bought by a Spider player: Proclaim is on offer. Proclaim first, the entering effects after (Mendon).
            Eq(e.State.PendingChoice?.Type, "proclaim", "the Proclaim window is open (he is in the buyer's own Clan)");
            Eq(e.State.Players[0].Honor, honor, "nothing has happened to his controller's Honor yet: the entering effects wait for the Proclaim decision");
            Check(!Asked(e), "the discards wait for the Proclaim window: the open window is never overwritten");
            if (owedRound++ % 2 == 0)
            {
                Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
                Eq(e.State.Players[0].Honor, honor - 2, "Proclaim taken (a gain of his 0 Personal Honor), then he loses 2 Honor as he enters");
                Check(!Asked(e), "the discards still wait for the window to close");
            }
            else Check(e.QueryActions(0).Any(a => a.Type == "proclaim"), "Proclaim is offered (and passed on here)");
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.Players[0].Honor, honor - 2, "his controller loses 2 Honor as he enters (at the latest when the window closes)");
            var owed = 0;
            while (Asked(e))
            {
                owed++;
                var chooser = e.State.PendingChoice!.PlayerId;
                Eq(chooser, 1, "the OTHER player discards, and chooses");
                Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "the controller waits");
                Discard(e, 1, DiscardOffers(e, 1).Last().CardId!);
            }
            Eq(owed, discards, $"{clan} player with {hand} cards discards {discards}");
            Eq(e.State.Players[1].Hand.Count, hand - discards);
            Eq(e.State.Players[0].Hand.Count, ownHand, "his controller discards nothing");
            Eq(e.State.PriorityPlayerId, 0, "priority resumes with the buyer, who may keep buying");
            Check(e.State.PendingChoice is null);
        }
        {
            // Overlay: he enters play over base Bofana, for no Gold, and the same effects apply.
            var s = MatchEngine.CreateEconomyScenario().State;
            foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
            s.Players[0].Clan = "Spider"; s.Players[1].Clan = "Crab";
            var baseBofana = Add(s, Bofana, 0, Zone.Home);
            var exp = s.Cards[s.Players[0].Provinces[0].CardId!]; exp.DefinitionId = BofanaExp; exp.FaceUp = true;
            Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand); Add(s, "training-farm", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var honor = e.State.Players[0].Honor;
            Accept(e, e.QueryActions(0).First(a => a.Type == "overlay" && a.CardId == exp.Id && a.Enabled).Command!);
            Eq(e.State.Players[0].Honor, honor - 2);
            var owed = 0;
            while (Asked(e)) { owed++; Discard(e, 1, DiscardOffers(e, 1)[0].CardId!); }
            Eq(owed, 2, "a Crab Clan player discards two");
            Eq(e.State.Cards[baseBofana.Id].Zone, Zone.RemovedFromGame, "the base card was overlaid");
        }
    }

    // Buys `card` (player 0, given Clan, plenty of Honor) at full cost -- the Proclaim-eligible mode -- and returns the engine with the purchase made.
    private static MatchEngine BuyProclaimEligible(string card, string clan, Action<MatchState>? tweak = null)
    {
        var def = CardCatalog.Get(card);
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = clan; s.Players[0].Honor = 30;
        for (var i = 0; i < def.GoldCost / 2 + 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = card; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false } && a.Quote is { ProclaimEligible: true });
        Accept(e, option.Command!);
        Eq(e.State.PendingChoice?.Type, "proclaim", $"{card}: the Proclaim window is open");
        return e;
    }

    private static void ProclaimThenEntryEffects()
    {
        // The ordering, in the event log and not only the final number: Proclaim (+3) is one Honor change, the entering loss (-2) another, after it.
        foreach (var takeProclaim in new[] { true, false })
        {
            var e = BuyProclaimEligible("training-crane-proclaim-loss", "Crane");
            var honor = e.State.Players[0].Honor;
            Eq(e.State.Players[0].Honor, honor, "nothing yet");
            if (takeProclaim)
            {
                Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
                Eq(e.State.Players[0].Honor, honor + 3 - 2, "Proclaim +3 then the entering loss -2");
            }
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.Players[0].Honor, takeProclaim ? honor + 1 : honor - 2, takeProclaim ? "unchanged by the passes" : "no Proclaim: only the loss, applied when the window closed");
            var log = e.State.Events.Select(x => x.Message).ToArray();
            var gain = Array.FindIndex(log, m => m.Contains("+3 Honor") && m.Contains("Proclaim"));
            var loss = Array.FindIndex(log, m => m.Contains("-2 Honor") && m.Contains("entering play"));
            Check(loss >= 0, "the entering loss is in the log as its own event: " + string.Join(" | ", log.TakeLast(6)));
            if (takeProclaim) Check(gain >= 0 && gain < loss, "the Proclaim gain is its own event, BEFORE the loss");
            else Check(gain < 0, "no Proclaim event when it was passed");
        }
        // The nine already-wired Personalities that were skipping their mandatory effect on a Proclaim-eligible purchase.
        var nine = new (string Id, string Clan, string Effect)[]
        {
            ("oracle-l5r-10454", "Dragon", "loss"), ("oracle-l5r-10456", "Dragon", "loss"), ("oracle-l5r-1765", "Spider", "loss"), ("oracle-l5r-10630", "Dragon", "loss+draw"),
            ("oracle-l5r-10640", "Lion", "draw"), ("oracle-l5r-10832", "Crab", "draw"), ("oracle-l5r-10854", "Lion", "draw"),
            ("oracle-l5r-5095", "Dragon", "sensei"), ("oracle-l5r-10287", "Dragon", "sensei"),
        };
        foreach (var (id, clan, effect) in nine)
            foreach (var takeProclaim in new[] { true, false })
            {
                var def = CardCatalog.Get(id);
                var otherSensei = id == "oracle-l5r-5095" ? "oracle-l5r-10287" : "oracle-l5r-5095";
                CardInstance? sensei = null;
                var e = BuyProclaimEligible(id, clan, s => { if (effect == "sensei") sensei = Add(s, otherSensei, 0, Zone.Home); });
                var honor = e.State.Players[0].Honor; var hand = e.State.Players[0].Hand.Count;
                Eq(e.State.Players[0].Honor, honor); Eq(e.State.Players[0].Hand.Count, hand, $"{id}: nothing applied while the Proclaim window is open");
                if (takeProclaim) Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
                Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
                var gained = takeProclaim ? def.PersonalHonor : 0;
                var lost = def.EntryHonorLoss ?? 0;
                Eq(e.State.Players[0].Honor, honor + gained - lost, $"{id}: Proclaim {(takeProclaim ? "taken" : "passed")}, entering loss {lost}");
                Eq(e.State.Players[0].Hand.Count, hand + (effect.Contains("draw") ? 1 : 0), $"{id}: Destined draw");
                if (sensei is not null) Eq(e.State.Cards[sensei.Id].Zone, Zone.DynastyDiscard, $"{id}: the other Sensei is discarded");
                if (takeProclaim && gained > 0 && lost > 0)
                {
                    var log = e.State.Events.Select(x => x.Message).ToArray();
                    Check(Array.FindIndex(log, m => m.Contains("Proclaim")) is var g && g >= 0 && g < Array.FindIndex(log, m => m.Contains("entering play")), $"{id}: Proclaim gain logged before the entering loss");
                }
            }
    }

    // ---- Bofana - exp's Dark Virtue action ------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Exp, CardInstance Dv, MatchState S) DarkVirtueTable(Action<MatchState>? tweak = null)
    {
        var s = Table(actor: 0);
        var exp = Add(s, BofanaExp, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var dv = Add(s, "training-dark-virtue", 0, Zone.FateDiscard);
        tweak?.Invoke(s);
        return (MatchEngine.FromTestState(s), exp, dv, s);
    }

    private static void ExpPlaysDarkVirtueFromDiscard()
    {
        var (e, exp, dv, s) = DarkVirtueTable();
        var enemy = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var opt = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "bofana-exp-dark-virtue");
        Check(opt.Enabled && opt.TargetIds!.SequenceEqual([dv.Id]), "the Dark Virtue is the only legal target");
        // Before the action nothing in the discard pile is playable.
        Check(!e.QueryActions(0).Any(a => a.CardId == dv.Id), "the discard pile offers nothing without the grant");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dv.Id, TargetId: enemy.Id, AbilityId: Bow));
        Accept(e, opt.Command!);
        Eq(e.State.AdditionalActions.Count, 1, "one additional action granted");
        Check(e.State.AdditionalActions[0] is { PlayerId: 0, FromDiscard: true } g && g.ScopeCardIds!.SequenceEqual([dv.Id]), "scoped to exactly the chosen card, from the discard pile");
        Eq(e.State.PriorityPlayerId, 0, "the additional action keeps priority with the player");
        Eq(e.State.Cards[dv.Id].Zone, Zone.FateDiscard, "the card waits in the discard pile");
        // Its Battle actions are on offer (both, the player picks), its Open action is not.
        var offers = e.QueryActions(0).Where(a => a.CardId == dv.Id && a.Type == "useAbility").ToArray();
        Check(offers.Select(a => a.Command!.AbilityId).OrderBy(x => x).SequenceEqual(["training-bow-at-battle", "training-straighten-at-battle"]), "exactly its Battle actions");
        Check(offers.All(a => a.Enabled && a.Label.Contains("removed from the game")), "labelled as a play that removes the card");
        // The player picks the bow. It resolves as the card's own action, and the card leaves the game.
        Accept(e, Offer(e, 0, Bow, dv.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the chosen Battle action resolved");
        Eq(e.State.Cards[dv.Id].Zone, Zone.RemovedFromGame, "the card is removed from the game once it resolved");
        Eq(e.State.AdditionalActions.Count, 0, "the grant was consumed");
        Eq(e.State.PriorityPlayerId, 1, "priority then passes as after any action");
        // The other Battle action, on another table (the player's choice is real).
        CardInstance own = null!;
        var (e2, _, dv2, s2) = DarkVirtueTable(t => own = Add(t, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0));
        Accept(e2, e2.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
        Accept(e2, Offer(e2, 0, "training-straighten-at-battle", dv2.Id, own.Id));
        Check(!e2.State.Cards[own.Id].Bowed, "the straighten resolved instead");
        Eq(e2.State.Cards[dv2.Id].Zone, Zone.RemovedFromGame);
        // What can be targeted: only a Dark Virtue with a Battle action, in your own Fate discard pile.
        var (e3, _, _, _) = DarkVirtueTable(t =>
        {
            foreach (var c in t.Cards.Values.Where(c => c.DefinitionId == "training-dark-virtue").ToArray()) { c.Zone = Zone.Hand; t.Players[c.OwnerId].Hand.Add(c.Id); }
            Add(t, "training-bow-strategy", 0, Zone.FateDiscard);            // not a Dark Virtue
            Add(t, "training-dark-virtue-open", 0, Zone.FateDiscard);        // a Dark Virtue with no Battle action
            Add(t, "training-dark-virtue", 1, Zone.FateDiscard);             // the opponent's discard pile
            Add(t, "training-dark-virtue", 0, Zone.Battlefield);             // in play, not in a discard pile
        });
        var none = e3.QueryActions(0).Single(a => a.Label.StartsWith("Target a Dark Virtue"));
        Check(!none.Enabled, "no legal Dark Virtue: not offered as usable");
        // ...and a command naming each of the wrong cards directly is refused too, not merely unlisted.
        foreach (var wrong in e3.State.Cards.Values.Where(c => c.DefinitionId is "training-bow-strategy" or "training-dark-virtue-open" or "training-dark-virtue"))
            Reject(e3, new GameCommand("useAbility", 0, e3.Version, CardId: e3.State.Cards.Values.Single(c => c.DefinitionId == BofanaExp).Id, TargetId: wrong.Id, AbilityId: "bofana-exp-dark-virtue"));
        // A Bofana that already acted this turn cannot act again (an ordinary once-per-turn Battle action).
        var (e4, _, dv4, _) = DarkVirtueTable();
        Accept(e4, e4.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
        Accept(e4, Offer(e4, 0, "training-straighten-at-battle", dv4.Id, e4.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id));
        e4 = Edit(e4, st => { st.PriorityPlayerId = 0; Add(st, "training-dark-virtue", 0, Zone.FateDiscard); });
        Check(!e4.QueryActions(0).Single(a => a.Label.StartsWith("Target a Dark Virtue")).Enabled, "his own action is once per turn");
    }

    private static void ExpDarkVirtueSameTurnRulePerAction()
    {
        const string Straighten = "training-straighten-at-battle";
        // The real sequence: player 0 plays the Dark Virtue's BOW from hand (its once-per-turn use is spent, and stays on the card in the discard
        // pile), the opponent passes, and Bofana grants the play of the same card from the pile.
        var s = Table(actor: 0);
        var exp = Add(s, BofanaExp, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var dv = Add(s, "training-dark-virtue", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Bow, dv.Id, enemy.Id));
        Eq(e.State.Cards[dv.Id].Zone, Zone.FateDiscard, "played from hand: now in the discard pile");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, e.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
        var offers = e.QueryActions(0).Where(a => a.CardId == dv.Id && a.Type == "useAbility").ToArray();
        var bowOffer = offers.Single(a => a.Label.StartsWith("Training: bow a target enemy Personality"));
        var straightenOffer = offers.Single(a => a.Label.StartsWith("Training: straighten"));
        Check(!bowOffer.Enabled && bowOffer.Reason!.Contains("already been used"), "the same action, performed this turn: offered disabled with its reason: " + bowOffer.Reason);
        Check(straightenOffer.Enabled, "the other action of the same card: allowed");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dv.Id, TargetId: enemy.Id, AbilityId: Bow));
        Accept(e, Offer(e, 0, Straighten, dv.Id, enemy.Id));
        Eq(e.State.Cards[dv.Id].Zone, Zone.RemovedFromGame, "the permitted action resolved and the card left the game");
        {
            // Both actions spent this turn (one earlier from hand, the other earlier from the pile): nothing playable is left, so the Dark Virtue is no target.
            var t = Table(actor: 0);
            Add(t, BofanaExp, 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var spent = Add(t, "training-dark-virtue", 0, Zone.FateDiscard);
            spent.AbilityUses[Bow] = t.TurnNumber; spent.AbilityUses[Straighten] = t.TurnNumber;
            var x = MatchEngine.FromTestState(t);
            Check(!x.QueryActions(0).Single(a => a.Label.StartsWith("Target a Dark Virtue")).Enabled, "no unspent Battle action: not a legal target");
        }
        {
            // The next turn: the stamps are stale, and both actions are open again.
            var t = Table(actor: 0);
            Add(t, BofanaExp, 0, Zone.Battlefield, province: 0); var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var old = Add(t, "training-dark-virtue", 0, Zone.FateDiscard);
            old.AbilityUses[Bow] = t.TurnNumber - 1; old.AbilityUses[Straighten] = t.TurnNumber - 1;
            var x = MatchEngine.FromTestState(t);
            Accept(x, x.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
            Check(x.QueryActions(0).Where(a => a.CardId == old.Id && a.Type == "useAbility").All(a => a.Enabled), "both Battle actions are available on the next turn");
            Accept(x, Offer(x, 0, Bow, old.Id, foe.Id));
            Eq(x.State.Cards[old.Id].Zone, Zone.RemovedFromGame);
        }
    }

    private static void ExpGrantIsForfeitable()
    {
        {
            var (e, _, dv, _) = DarkVirtueTable();
            Accept(e, e.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
            Accept(e, new GameCommand("pass", 0, e.Version));
            Eq(e.State.AdditionalActions.Count, 0, "passing forfeits the additional action");
            Eq(e.State.Cards[dv.Id].Zone, Zone.FateDiscard, "the card stays where it was");
            Check(!e.QueryActions(0).Any(a => a.CardId == dv.Id), "and offers nothing");
        }
        {
            // Taking any other action instead also ends the burst.
            var (e, _, dv, s) = DarkVirtueTable(t => Add(t, "training-battle-bower", 0, Zone.Battlefield, province: 0));
            var enemy = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
            var bower = s.Cards.Values.Single(c => c.DefinitionId == "training-battle-bower");
            Accept(e, e.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue").Command!);
            Accept(e, Offer(e, 0, Bow, bower.Id, enemy.Id));
            Eq(e.State.AdditionalActions.Count, 0, "an ordinary action forfeits the grant");
            Eq(e.State.Cards[dv.Id].Zone, Zone.FateDiscard);
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dv.Id, TargetId: enemy.Id, AbilityId: Bow));
        }
        {
            // The grant names one card: a second Dark Virtue in the pile stays out of reach; Discipline/other rules are untouched.
            var (e, _, dv, s) = DarkVirtueTable(t => Add(t, "training-dark-virtue", 0, Zone.FateDiscard));
            var other = s.Cards.Values.Single(c => c.DefinitionId == "training-dark-virtue" && c.Id != dv.Id);
            var enemy = s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
            var choose = e.QueryActions(0).Single(a => a.Command?.AbilityId == "bofana-exp-dark-virtue");
            Check(choose.TargetIds!.ToHashSet().SetEquals([dv.Id, other.Id]), "either is a legal target");
            Accept(e, choose.Command! with { TargetId = other.Id });
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dv.Id, TargetId: enemy.Id, AbilityId: Bow));
            Accept(e, Offer(e, 0, Bow, other.Id, enemy.Id));
            Eq(e.State.Cards[other.Id].Zone, Zone.RemovedFromGame); Eq(e.State.Cards[dv.Id].Zone, Zone.FateDiscard);
        }
    }
}
