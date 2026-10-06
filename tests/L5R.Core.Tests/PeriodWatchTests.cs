using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #683, primitive P7 (PeriodWatches.cs): period stamps and negation watches. The store and the stamps are driven directly (their internal API), and each choke point
// through a real action on a fixture card, so a watch is proven to negate the effect where the effect would happen -- and only then.
internal static class PeriodWatchTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);

    public static void Run(TestRunner runner) => runner.Run("period-watches", new (string, Action)[]
    {
        ("Period stamps: once per turn, per printed phase, per battle and per game reset exactly when their period does; UsesPer drives AbilityUseSpent", PeriodStamps),
        ("Period counters count within an instance and restart in the next", PeriodCounters),
        ("A dynamic watch negates the next destruction from another player's action, once, and the next destruction after it happens", DynamicDestroy),
        ("A refreshing watch negates once per period instance; a watch owned by a card ends when the card leaves play; LastTurn expires it", RefreshAndOwner),
        ("A keyword watch covers its owner's matching cards only, and only another player's effects (not a cost, not the rulebook, not its own player's)", KeywordAndSource),
        ("A standing rule: the first destruction each turn of one of its controller's Samurai from another player's effect is negated, and its follow-up runs", StandingRule),
        ("A standing Game rule is spent for the game: the card leaving play and the turns passing do not restore it", StandingGame),
        ("Each choke point: a watch negates a bowing, a move home, a straightening and a dishonoring from an action, and the effect does not happen", EachChokePoint),
        ("A Political action: a watch on the targeted card, or the player's own, negates the whole action (its costs still paid), once", PoliticalAction),
        ("Watch state round-trips through the saved game", RoundTrip),
        ("The standing-watch channel is sealed at startup and the test seam is empty outside a test", Registry),
    });

    private static MatchEngine WithBattle(Action<MatchState> build, Phase phase = Phase.Battle, int actor = 1)
    {
        var s = Table(phase, actor); build(s); return MatchEngine.FromTestState(s);
    }

    // After an action priority passes to the other player; the next action of the same player is taken from a rebuilt engine that gives it back.
    private static MatchEngine Again(MatchEngine e) => Edit(e, st => { st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
    private static CardInstance Granted(MatchEngine e, int owner) => e.State.Cards.Values.First(c => c.OwnerId == owner && c.GrantedAbilities.Count > 0);

    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version)); }

    // ---- stamps and counters ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PeriodStamps()
    {
        var e = WithBattle(_ => { });
        var uses = new Dictionary<string, int>();
        Check(e.TrySpendPeriodUse(uses, "k", Period.Turn), "first use this turn");
        Check(!e.TrySpendPeriodUse(uses, "k", Period.Turn), "second use this turn is refused, with no change");
        Check(e.TrySpendPeriodUse(uses, "other-key", Period.Turn), "another key is independent");
        var nextTurn = Edit(e, st => st.TurnNumber++);
        Check(nextTurn.TrySpendPeriodUse(uses, "k", Period.Turn), "the next turn restores it");

        // Phases: the six PRINTED phases; the Attack Phase's sub-steps are one phase.
        var phases = new Dictionary<string, int>();
        var inBattle = WithBattle(_ => { });
        Check(inBattle.TrySpendPeriodUse(phases, "p", Period.Phase), "first use in the Attack Phase");
        var sameAttackPhase = Edit(inBattle, st => { st.Phase = Phase.BattleSelection; });
        Check(!sameAttackPhase.TrySpendPeriodUse(phases, "p", Period.Phase), "a sub-step of the same Attack Phase shares it");
        var nextPhase = Edit(inBattle, st => { st.Phase = Phase.Dynasty; st.CurrentBattleProvince = null; });
        Check(nextPhase.TrySpendPeriodUse(phases, "p", Period.Phase), "the Dynasty Phase is a new phase");
        var nextTurnSamePhase = Edit(inBattle, st => st.TurnNumber++);
        Check(nextTurnSamePhase.TrySpendPeriodUse(phases, "p", Period.Phase), "the same phase next turn is a new instance");

        // Battles: each battle is its own instance, and "no battle" is one more.
        var battles = new Dictionary<string, int>();
        var b1 = WithBattle(st => st.BattleNumber = 3);
        Check(b1.TrySpendPeriodUse(battles, "b", Period.Battle), "first use in a battle");
        Check(!b1.TrySpendPeriodUse(battles, "b", Period.Battle), "second use in the same battle");
        var b2 = Edit(b1, st => st.BattleNumber = 4);
        Check(b2.TrySpendPeriodUse(battles, "b", Period.Battle), "the next battle restores it");
        var between = Edit(b2, st => st.CurrentBattleProvince = null);
        Check(between.TrySpendPeriodUse(battles, "b", Period.Battle), "outside a battle is its own instance");
        Check(!between.TrySpendPeriodUse(battles, "b", Period.Battle), "and spends once");

        // The game: never resets.
        var game = new Dictionary<string, int>();
        Check(e.TrySpendPeriodUse(game, "g", Period.Game), "first use this game");
        var later = Edit(e, st => { st.TurnNumber += 9; st.BattleNumber += 9; st.Phase = Phase.Dynasty; });
        Check(!later.TrySpendPeriodUse(game, "g", Period.Game), "no later turn restores a game use");

        // The ability field: UsesPer replaces the per-turn count, even for an ability with no per-turn limit.
        var perBattle = new AbilityDefinition("Battle", "test", "none", UsesPerTurn: AbilityUseLimit.Unlimited, UsesPer: Period.Battle);
        var abilityUses = new Dictionary<string, int>();
        Check(!b1.UseSpentForTest(abilityUses, perBattle, "x"), "unspent");
        b1.SpendUseForTest(abilityUses, perBattle, "x");
        Check(b1.UseSpentForTest(abilityUses, perBattle, "x"), "spent for this battle");
        Check(!b2.UseSpentForTest(abilityUses, perBattle, "x"), "and free in the next battle");
        var plain = new AbilityDefinition("Battle", "test", "none");
        var plainUses = new Dictionary<string, int>();
        b1.SpendUseForTest(plainUses, plain, "y");
        Check(b1.UseSpentForTest(plainUses, plain, "y") && !abilityUses.ContainsKey("y"), "an ability without UsesPer keeps its per-turn stamp");
    }

    private static void PeriodCounters()
    {
        var e = WithBattle(st => st.BattleNumber = 5);
        Eq(e.PeriodCountOf("copies", Period.Battle), 0, "starts at zero");
        Eq(e.AddPeriodCount("copies", Period.Battle), 1, "first");
        Eq(e.AddPeriodCount("copies", Period.Battle, 2), 3, "adds");
        Eq(e.PeriodCountOf("copies", Period.Turn), 0, "a different period is a different counter");
        var next = Edit(e, st => st.BattleNumber = 6);
        Eq(next.PeriodCountOf("copies", Period.Battle), 0, "the next battle reads zero");
        Eq(next.AddPeriodCount("copies", Period.Battle), 1, "and restarts from zero");
        var turnly = Edit(e, st => st.TurnNumber++);
        Eq(turnly.PeriodCountOf("copies", Period.Battle), 3, "a turn counter of another period is untouched by the turn changing alone");
        Eq(e.AddPeriodCount("per-turn", Period.Turn), 1, "turn counter");
        Eq(turnly.PeriodCountOf("per-turn", Period.Turn), 0, "turn counter restarts next turn");
    }

    // ---- fixtures: the Defender (player 0) holds a Personality; the Attacker (player 1) has Samurai with the fixture destroy ----------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Target, CardInstance[] Killers) Duel(Action<MatchState, CardInstance>? build = null, int killers = 2)
    {
        CardInstance target = null!; var made = new List<CardInstance>();
        var e = WithBattle(s =>
        {
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); target.GrantedKeywords.Add(new("Samurai", "test-fixture", GrantDuration.Permanent));
            for (var i = 0; i < killers; i++)
            {
                var killer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                killer.GrantedAbilities.Add(new("kuronada-destroy", "test-fixture", GrantDuration.Permanent)); made.Add(killer);
            }
            build?.Invoke(s, target);
        });
        return (e, target, made.ToArray());
    }

    private static void Destroy(MatchEngine e, CardInstance killer, CardInstance target) { Accept(e, Offer(e, 1, "kuronada-destroy", killer.Id, target.Id)); Settle(e); }

    private static void DynamicDestroy()
    {
        var (e, target, killers) = Duel();
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetCardId = target.Id });
        Destroy(e, killers[0], target);
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "the destruction from the action was negated: he is still at the battlefield");
        Check(e.State.Events.Any(x => x.Type == "destructionNegated" && x.CardId == target.Id), "the negation is logged");
        Check(e.State.Cards[killers[0].Id].AbilityUses.ContainsKey("kuronada-destroy"), "the negated action was still taken (its use is spent)");
        Eq(e.State.NegationWatches.Count, 0, "a one-shot watch is used up");
        Check(e.WatchNegated(WatchKind.Destroy, target.Id), "the negation is recorded this turn for a follow-up to ask about");
        var again = Again(e); Destroy(again, killers[1], target);
        Check(again.State.Cards[target.Id].Zone != Zone.Battlefield, "the next destruction happens");
        Check(!again.WatchNegated(WatchKind.Destroy, "nobody"), "nothing recorded for another card");
    }

    private static void RefreshAndOwner()
    {
        var (e, target, _) = Duel();
        var other = e.State.Cards.Values.First(c => c.OwnerId == 1);
        var watch = e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetCardId = target.Id, Remaining = -1, Refresh = Period.Turn });
        Check(e.TryConsumeWatch(WatchKind.Destroy, Card(e, target.Id), 1), "first this turn");
        Check(!e.TryConsumeWatch(WatchKind.Destroy, Card(e, target.Id), 1), "not twice in the turn");
        Eq(e.State.NegationWatches.Count, 1, "a refreshing watch stays");
        var next = Edit(e, st => st.TurnNumber++);
        Check(next.TryConsumeWatch(WatchKind.Destroy, Card(next, target.Id), 1), "again next turn");

        // Owned by a card: it leaves with the card.
        var owned = Edit(e, st => { st.NegationWatches.Clear(); });
        owned.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, OwnerCardId = target.Id, Kind = WatchKind.Destroy, TargetCardId = target.Id });
        var gone = Edit(owned, st => st.Cards[target.Id].Zone = Zone.FateDiscard);
        Check(!gone.TryConsumeWatch(WatchKind.Destroy, Card(gone, target.Id), 1), "the owner card left play: the watch does nothing");
        var home = Edit(owned, st => { });
        Check(home.TryConsumeWatch(WatchKind.Destroy, Card(home, target.Id), 1), "the owner card in play: it does");

        // Expiry: good through LastTurn.
        var timed = Edit(e, st => st.NegationWatches.Clear());
        timed.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetCardId = target.Id, LastTurn = timed.State.TurnNumber });
        var tomorrow = Edit(timed, st => st.TurnNumber++);
        Check(!tomorrow.TryConsumeWatch(WatchKind.Destroy, Card(tomorrow, target.Id), 1), "the turn it was good for is over");
        Check(timed.TryConsumeWatch(WatchKind.Destroy, Card(timed, target.Id), 1), "and on the turn it was good for it works");
        Check(other.OwnerId == 1, "fixture sanity");
    }

    private static CardInstance Card(MatchEngine e, string id) => e.CardForTest(id);

    private static void KeywordAndSource()
    {
        var (e, target, _) = Duel((s, t) => { Add(s, "training-lion-veteran", 0, Zone.Home); });
        var mine = e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone != Zone.Hand).ToArray();
        var enemy = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Bow, TargetKeyword = "Samurai", Remaining = -1, Refresh = Period.Turn });
        Check(Card(e, target.Id) is { } t && e.HasKeyword(t.Id, "Samurai"), "fixture: the target is a Samurai");
        Check(!e.TryConsumeWatch(WatchKind.Bow, Card(e, enemy.Id), 0), "another player's Samurai is not covered by a watch of player 0");
        Check(!e.TryConsumeWatch(WatchKind.Bow, Card(e, target.Id), null), "no effect source (a cost, the rulebook): not negated");
        Check(!e.TryConsumeWatch(WatchKind.Bow, Card(e, target.Id), 0), "its own player's effect: not negated by an OtherPlayers watch");
        Check(!e.TryConsumeWatch(WatchKind.Destroy, Card(e, target.Id), 1), "another kind of effect: not negated");
        Check(e.TryConsumeWatch(WatchKind.Bow, Card(e, target.Id), 1), "another player's effect on his Samurai: negated");
        var any = Edit(e, st => { st.NegationWatches.Clear(); });
        any.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Bow, TargetCardId = target.Id, Source = WatchSource.Any });
        Check(any.TryConsumeWatch(WatchKind.Bow, Card(any, target.Id), 0), "an Any watch also negates its own player's effect");
        Check(mine.Length > 0, "fixture sanity");
    }

    // A rule written the way a card's feature file writes it (here from the test assembly: the seam list is read beside the registered ones).
    private static StandingWatch KazutoshiShape(Func<CardDefinition, bool> carrier, Action<MatchEngine, WatchContext, CardInstance>? follow = null) => new(
        "p7-test-loyal-destroy", carrier, WatchKind.Destroy, WatchScope.OwnersCards, TargetKeyword: "Samurai", TargetType: CardType.Personality, Source: WatchSource.OtherPlayers, Period: Period.Turn, OnNegated: follow);

    private static void StandingRule()
    {
        var carrierId = "training-lion-veteran";
        var (e, target, killers) = Duel((s, _) => Add(s, carrierId, 0, Zone.Home), killers: 3);
        var negated = new List<string>();
        MatchEngine.TestStandingWatches = [KazutoshiShape(d => d.Id == carrierId, (engine, context, owner) => { negated.Add(context.Target!.Id + "<-" + owner.Id); context.Target.Bowed = true; })];
        try
        {
            Destroy(e, killers[0], target);
            Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "the first destruction this turn is negated");
            Check(e.State.Cards[target.Id].Bowed && negated.Count == 1, "the follow-up ran once, with the carrier");
            var second = Again(e); Destroy(second, killers[1], target);
            Check(second.State.Cards[target.Id].Zone != Zone.Battlefield, "the second destruction the same turn happens");
            // Without the carrier in play nothing protects him.
            var (bare, target2, killers2) = Duel(killers: 1);
            Destroy(bare, killers2[0], target2);
            Check(bare.State.Cards[target2.Id].Zone != Zone.Battlefield, "no carrier in play: no watch");
        }
        finally { MatchEngine.TestStandingWatches = null; }
    }

    private static void StandingGame()
    {
        var carrierId = "training-lion-veteran"; CardInstance carrier = null!;
        var (e, target, _) = Duel((s, _) => carrier = Add(s, carrierId, 0, Zone.Home));
        MatchEngine.TestStandingWatches = [new("p7-test-first-each-game", d => d.Id == carrierId, WatchKind.Destroy, WatchScope.Self, Period: Period.Game)];
        try
        {
            Check(!e.TryConsumeWatch(WatchKind.Destroy, Card(e, target.Id), 1), "the rule covers its carrier only, not another card");
            Check(e.TryConsumeWatch(WatchKind.Destroy, Card(e, carrier.Id), 1), "the first destruction of the carrier is negated");
            Check(!e.TryConsumeWatch(WatchKind.Destroy, Card(e, carrier.Id), 1), "not a second");
            var later = Edit(e, st => { st.TurnNumber += 5; st.Phase = Phase.Dynasty; st.CurrentBattleProvince = null; });
            Check(!later.TryConsumeWatch(WatchKind.Destroy, Card(later, carrier.Id), 1), "no later turn restores it");
            var left = Edit(e, st => st.Cards[carrier.Id].Zone = Zone.Home); // back in play after being away: still spent
            Check(!left.TryConsumeWatch(WatchKind.Destroy, Card(left, carrier.Id), 1), "the spent mark outlives the carrier's time out of play");
            Check(e.State.WatchSpent.Count == 1, "one spent mark");
        }
        finally { MatchEngine.TestStandingWatches = null; }
    }

    // ---- the choke points -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EachChokePoint()
    {
        // Dishonor: the fixture dishonor action, from player 1 to a Defender.
        {
            var e = WithBattle(s =>
            {
                var him = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                var killer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); killer.GrantedAbilities.Add(new("training-dishonor-target", "test-fixture", GrantDuration.Permanent));
            });
            var him2 = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield); var by = Granted(e, 1);
            e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Dishonor, TargetCardId = him2.Id });
            Accept(e, Offer(e, 1, "training-dishonor-target", by.Id, him2.Id)); Settle(e);
            Check(!e.State.Cards[him2.Id].Dishonorable, "the dishonoring was negated");
            Check(e.State.Events.Any(x => x.Type == "dishonorNegated" && x.CardId == him2.Id), "and logged");
        }
        // Move home: the Attacker sends a defending Personality home.
        {
            var e = WithBattle(s =>
            {
                Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                var mover = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); mover.GrantedAbilities.Add(new("incapacitated", "test-fixture", GrantDuration.Permanent));
            });
            var defender = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield); var by = Granted(e, 1);
            e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Move, TargetCardId = defender.Id });
            Accept(e, Offer(e, 1, "incapacitated", by.Id, defender.Id)); Settle(e);
            Check(e.State.Cards[defender.Id].Zone == Zone.Battlefield, "the movement home was negated: he stays at the battlefield");
            Check(e.State.Events.Any(x => x.Type == "moveNegated" && x.CardId == defender.Id), "and logged");
        }
        // Bow: a Political action that bows a Personality.
        {
            var e = WithBattle(s =>
            {
                Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                var bower = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); bower.GrantedAbilities.Add(new("costly-feud", "test-fixture", GrantDuration.Permanent));
            });
            var victim = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield); var by = Granted(e, 1);
            e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Bow, TargetCardId = victim.Id });
            Accept(e, Offer(e, 1, "costly-feud", by.Id, victim.Id)); Settle(e);
            Check(!e.State.Cards[victim.Id].Bowed, "the bowing was negated");
            Check(e.State.Cards[victim.Id].Modifiers.Count > 0, "its other effect (the Force penalty) still applied: only the bowing is negated");
        }
        // Straighten: an Air Personality straightened by the other player's action.
        {
            CardInstance air = null!;
            var e = WithBattle(s =>
            {
                air = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); air.GrantedKeywords.Add(new("Air", "test-fixture", GrantDuration.Permanent));
                var src = Add(s, "training-lion-samurai", 1, Zone.Home); src.GrantedAbilities.Add(new("air-straighten", "test-fixture", GrantDuration.Permanent));
            }, Phase.Action);
            var by = Granted(e, 1);
            e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Straighten, TargetCardId = air.Id });
            Accept(e, Offer(e, 1, "air-straighten", by.Id, air.Id)); Settle(e);
            Check(e.State.Cards[air.Id].Bowed, "the straightening was negated: he is still bowed");
            Check(e.State.Events.Any(x => x.Type == "straightenNegated" && x.CardId == air.Id), "and logged");
        }
    }

    private static void PoliticalAction()
    {
        CardInstance victim = null!, other = null!, strategy = null!, second = null!;
        MatchEngine Build() => WithBattle(s =>
        {
            victim = Add(s, "training-crane-samurai", 0, Zone.Home); other = Add(s, "training-crane-samurai", 0, Zone.Home);
            strategy = Add(s, "training-political-strategy", 1, Zone.Hand); second = Add(s, "training-political-strategy", 1, Zone.Hand);
            for (var i = 0; i < 4; i++) { Add(s, "training-marketplace", 1, Zone.Home); }
        }, Phase.Action);
        // A watch on the card the action names.
        var e = Build();
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.PoliticalAction, TargetCardId = victim.Id });
        Accept(e, Offer(e, 1, "training-bow-any", strategy.Id, victim.Id)); Settle(e);
        Check(!e.State.Cards[victim.Id].Bowed, "the Political action that named him was negated whole");
        Check(e.State.Cards[strategy.Id].Zone != Zone.Hand, "the Strategy was still played: the negated action is taken and its card spent");
        Check(e.State.Events.Any(x => x.Type == "actionNegated"), "the negation is logged");
        Eq(e.State.NegationWatches.Count, 0, "a one-shot is used up");
        var e1b = Again(e); Accept(e1b, Offer(e1b, 1, "training-bow-any", second.Id, victim.Id)); Settle(e1b);
        Check(e1b.State.Cards[victim.Id].Bowed, "the next Political action resolves");

        // A watch on a card the action does not name does nothing.
        var e2 = Build();
        e2.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.PoliticalAction, TargetCardId = other.Id });
        Accept(e2, Offer(e2, 1, "training-bow-any", strategy.Id, victim.Id)); Settle(e2);
        Check(e2.State.Cards[victim.Id].Bowed, "a watch on another card does not negate it");

        // The player-level watch: the first Political action each turn taken against this player.
        var e3 = Build();
        e3.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.PoliticalAction, Remaining = -1, Refresh = Period.Turn });
        Accept(e3, Offer(e3, 1, "training-bow-any", strategy.Id, victim.Id)); Settle(e3);
        Check(!e3.State.Cards[victim.Id].Bowed, "the player-level watch negated the first Political action of the turn");
        var e3b = Again(e3); Accept(e3b, Offer(e3b, 1, "training-bow-any", second.Id, victim.Id)); Settle(e3b);
        Check(e3b.State.Cards[victim.Id].Bowed, "and not the second the same turn");
    }

    private static void RoundTrip()
    {
        var (e, target, _) = Duel();
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetKeyword = "Samurai", TargetType = CardType.Personality, Refresh = Period.Battle, Remaining = -1, LastTurn = 4, Arg = "x" });
        e.AddPeriodCount("c", Period.Turn, 2);
        Check(e.TryConsumeWatch(WatchKind.Destroy, Card(e, target.Id), 1), "consumed once so the record has a stamp and a consumption");
        var json = JsonSerializer.Serialize(e.State);
        var restored = JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(restored.NegationWatches.Count, 1, "watches");
        Check(restored.NegationWatches[0] is { Kind: WatchKind.Destroy, TargetKeyword: "Samurai", TargetType: CardType.Personality, Refresh: Period.Battle, Remaining: -1, LastTurn: 4, Arg: "x" }, "watch fields");
        Eq(restored.PeriodCounters.Values.Single().Count, 2, "counters");
        Eq(restored.WatchConsumptions.Count, 1, "consumptions");
        var resumed = MatchEngine.FromTestState(restored);
        Check(!resumed.TryConsumeWatch(WatchKind.Destroy, Card(resumed, target.Id), 1), "the refresh stamp survived: still spent in this battle");
    }

    private static void Registry()
    {
        Check(ReferenceEquals(EngineHooks.StandingWatches.Items, EngineHooks.StandingWatches.Items), "the channel is read at startup and its snapshot is stable");
        Check(MatchEngine.TestStandingWatches is null or { Count: 0 }, "the test seam is empty outside a test");
    }
}
