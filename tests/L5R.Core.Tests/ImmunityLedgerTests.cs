using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #696, primitive P12 (ImmunityLedger.cs): the immunity ledger. The store is driven directly and each kind through the choke point it guards, with real actions on fixture cards where one exists, so an
// immunity is proven to stop the matching effect from the filtered source where it would happen, and nothing else. Test-only standing rules go through MatchEngine.TestStandingImmunities and are
// removed in a finally, so no other suite ever sees them.
internal static class ImmunityLedgerTests
{
    private const string AncientFeud = "oracle-l5r-397", Shiotome = "oracle-l5r-7001";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static CardInstance Card(MatchEngine e, string id) => e.CardForTest(id);

    public static void Run(TestRunner runner) => runner.Run("immunities", new (string, Action)[]
    {
        ("Destroy: an immune card survives another player's destruction (logged), its own player's goes through unless the source is Any, and the immunity lapses with the turn and with the card", DestroyImmunity),
        ("Bow: another player's bowing is negated for an immune card while the effect's other results still apply; inside a unit bow each card asks for itself", BowImmunity),
        ("A standing rule is read live: conditional (while defending), carried by a card in play only, and covering attached Followers but not their Personality", StandingRules),
        ("DestroyInResolution: the battle resolution's own destruction skips an immune unit and nobody else", ResolutionImmunity),
        ("MoveNegation: a movement lock another player set no longer holds an immune card, a lock of its own player still does, and an unknown source still does", MoveNegationImmunity),
        ("Prevention: another player's action lock and movement (assignment) lock are ignored by an immune card; its own player's and unknown locks still bind", PreventionImmunity),
        ("ForcePenalty: an action's Force penalty is negated for an immune card and another card still takes it", ForcePenaltyImmunity),
        ("ProvinceStrength: another player's strength change is negated and its cards' contributions are not counted; the player's own still apply", ProvinceStrengthImmunity),
        ("TerrainDestroy: another player's effect, or another Terrain's, cannot discard an immune Terrain; the battle's own discard still does", TerrainImmunity),
        ("MovementAtBattlefield: an effect's movement from or to the immune battlefield is negated, another battlefield and a rule-made movement are not; a standing Battlefield rule follows its carrier", BattlefieldMovementImmunity),
        ("An immunity negates before a watch looks: an immune destruction does not consume a P7 watch", BeforeWatches),
        ("The ledger is omitted from the saved game until used, and round-trips once it is", RoundTrip),
        ("The standing-immunity channel is sealed at startup and the test seam is empty outside a test", Registry),
    });

    // ---- fixtures ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine WithBattle(Action<MatchState> build, Phase phase = Phase.Battle, int actor = 1)
    {
        var s = Table(phase, actor); build(s); return MatchEngine.FromTestState(s);
    }

    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version)); }

    // The Defender (player 0) holds a Samurai at battlefield 0; the Attacker (player 1) has fixture Samurai with a destroy action.
    private static (MatchEngine Engine, CardInstance Target, CardInstance[] Killers) Duel(int killers = 2)
    {
        CardInstance target = null!; var made = new List<CardInstance>();
        var e = WithBattle(s =>
        {
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            for (var i = 0; i < killers; i++)
            {
                var killer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                killer.GrantedAbilities.Add(new("kuronada-destroy", "test-fixture", GrantDuration.Permanent)); made.Add(killer);
            }
        });
        return (e, target, made.ToArray());
    }

    private static void Destroy(MatchEngine e, CardInstance killer, CardInstance target) { Accept(e, Offer(e, 1, "kuronada-destroy", killer.Id, target.Id)); Settle(e); }

    private static T WithRule<T>(StandingImmunity rule, Func<T> body)
    {
        MatchEngine.TestStandingImmunities = [rule];
        try { return body(); } finally { MatchEngine.TestStandingImmunities = null; }
    }

    // ---- destroy and bow ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DestroyImmunity()
    {
        var (e, target, killers) = Duel();
        Check(!e.IsImmuneForTest(target.Id, ImmunityKind.Destroy, 1), "no immunity yet");
        e.GrantImmunity(Card(e, target.Id), ImmunityKind.Destroy);
        Check(e.IsImmuneForTest(target.Id, ImmunityKind.Destroy, 1), "another player's effect: immune");
        Check(!e.IsImmuneForTest(target.Id, ImmunityKind.Destroy, 0), "the controller's own effect: not covered by OtherPlayers");
        Check(!e.IsImmuneForTest(target.Id, ImmunityKind.Destroy, null), "a cost or the rulebook has no source player: never stopped");
        Check(!e.IsImmuneForTest(target.Id, ImmunityKind.Bow, 1), "another kind is untouched");
        Destroy(e, killers[0], target);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the destruction from the action was negated: he is still at the battlefield");
        Check(e.State.Events.Any(x => x.Type == "immunityNegated" && x.CardId == target.Id), "the negation is logged");
        Check(e.State.Cards[killers[0].Id].AbilityUses.ContainsKey("kuronada-destroy"), "the negated action was still taken (its use is spent)");
        Eq(e.DestroyByEffectForTest([target.Id], 0), 0 + 1, "the controller's own destruction goes through");
        Check(e.State.Cards[target.Id].Zone != Zone.Battlefield, "and he is gone");

        // Source Any: the controller's own effect too.
        var (any, anyTarget, _) = Duel();
        any.GrantImmunity(Card(any, anyTarget.Id), ImmunityKind.Destroy, source: ImmunitySource.Any);
        Eq(any.DestroyByEffectForTest([anyTarget.Id], 0), 0, "Any covers the controller's own effect");
        Eq(any.State.Cards[anyTarget.Id].Zone, Zone.Battlefield, "he stays");

        // Lapses with the turn, and with the card.
        var (timed, timedTarget, _) = Duel();
        timed.GrantImmunity(Card(timed, timedTarget.Id), ImmunityKind.Destroy);
        var tomorrow = Edit(timed, st => st.TurnNumber++);
        Check(!tomorrow.IsImmuneForTest(timedTarget.Id, ImmunityKind.Destroy, 1), "the turn it was good for is over");
        var (gone, goneTarget, _) = Duel();
        gone.GrantImmunity(Card(gone, goneTarget.Id), ImmunityKind.Destroy, ImmunityUntil.Permanent);
        Check(gone.IsImmuneForTest(goneTarget.Id, ImmunityKind.Destroy, 1), "permanent while in play");
        var left = Edit(gone, st => st.Cards[goneTarget.Id].Zone = Zone.FateDiscard);
        Check(!left.IsImmuneForTest(goneTarget.Id, ImmunityKind.Destroy, 1), "an immunity leaves with the card");
        // Owner-card duration: the granting card leaving play ends it.
        var (owned, ownedTarget, ownedKillers) = Duel();
        owned.GrantImmunity(Card(owned, ownedTarget.Id), ImmunityKind.Destroy, ImmunityUntil.WhileInPlay, ownerCardId: ownedKillers[0].Id);
        Check(owned.IsImmuneForTest(ownedTarget.Id, ImmunityKind.Destroy, 1), "while the granting card is in play");
        var ownerLeft = Edit(owned, st => st.Cards[ownedKillers[0].Id].Zone = Zone.FateDiscard);
        Check(!ownerLeft.IsImmuneForTest(ownedTarget.Id, ImmunityKind.Destroy, 1), "the granting card left play: it ends");
    }

    private static void BowImmunity()
    {
        var e = WithBattle(s =>
        {
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); bower.GrantedAbilities.Add(new("costly-feud", "test-fixture", GrantDuration.Permanent));
        });
        var victim = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield); var by = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.GrantedAbilities.Count > 0);
        e.GrantImmunity(Card(e, victim.Id), ImmunityKind.Bow);
        Accept(e, Offer(e, 1, "costly-feud", by.Id, victim.Id)); Settle(e);
        Check(!e.State.Cards[victim.Id].Bowed, "the bowing was negated");
        Check(e.State.Cards[victim.Id].Modifiers.Count > 0, "its other effect (the Force penalty) still applied: only the bowing is immune");

        var d = WithBattle(s => Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0));
        var card = d.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Check(d.BowByEffectForTest(card.Id, 1), "without an immunity another player bows him");
        d.State.Cards[card.Id].Bowed = false;
        d.GrantImmunity(Card(d, card.Id), ImmunityKind.Bow);
        Check(!d.BowByEffectForTest(card.Id, 1), "with it, not");
        Check(d.BowByEffectForTest(card.Id, 0), "his own player's bowing is not another player's");
    }

    private static void StandingRules()
    {
        // "While defending, other players' cards will not bow this Shugenja": Self, conditional.
        var (e, target, _) = Duel();
        var rule = new StandingImmunity("test-p12-defending", d => d.Id == "training-crane-samurai", ImmunityKind.Bow, ImmunityScope.Self,
            Condition: static (engine, _, carrier) => engine.IsDefendingUnitForTest(carrier.Id));
        Check(WithRule(rule, () => e.IsImmuneForTest(target.Id, ImmunityKind.Bow, 1)), "defending: immune");
        var attacker = Edit(e, st => st.ActivePlayerId = 0);
        Check(!WithRule(rule, () => attacker.IsImmuneForTest(target.Id, ImmunityKind.Bow, 1)), "attacking: the condition fails");
        Check(!e.IsImmuneForTest(target.Id, ImmunityKind.Bow, 1), "outside the helper the rule is gone");
        // Carried by a card in play only.
        var out_ = Edit(e, st => st.Cards[target.Id].Zone = Zone.FateDiscard);
        Check(!WithRule(rule, () => out_.IsImmuneForTest(target.Id, ImmunityKind.Bow, 1)), "the carrier left play: no immunity");

        // Attached Followers (a unit bow asks card by card, R-017): the Followers are immune, their Personality is not.
        CardInstance host = null!, follower = null!;
        var u = WithBattle(s =>
        {
            host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id);
        });
        var followers = new StandingImmunity("test-p12-followers", d => d.Id == "training-crane-samurai", ImmunityKind.Bow, ImmunityScope.AttachedFollowers);
        Check(WithRule(followers, () => u.BowByEffectForTest(follower.Id, 1)) == false, "the Follower is not bowed");
        Check(WithRule(followers, () => u.BowByEffectForTest(host.Id, 1)), "his Personality is");
        // Cards of the controller that fit a keyword.
        var keyword = new StandingImmunity("test-p12-keyword", d => d.Id == "training-crane-samurai", ImmunityKind.Destroy, ImmunityScope.OwnersCards, TargetKeyword: "Samurai", TargetType: CardType.Personality);
        var (built, kTarget, _) = Duel();
        var k = Edit(built, st =>
        {
            st.Cards[kTarget.Id].GrantedKeywords.Add(new("Samurai", "test-fixture", GrantDuration.Permanent));
            foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield)) c.GrantedKeywords.Add(new("Samurai", "test-fixture", GrantDuration.Permanent));
        });
        Check(WithRule(keyword, () => k.IsImmuneForTest(kTarget.Id, ImmunityKind.Destroy, 1)), "the controller's cards that fit");
        var otherSide = k.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Check(!WithRule(keyword, () => k.IsImmuneForTest(otherSide.Id, ImmunityKind.Destroy, 0)), "not the other player's");
    }

    private static void ResolutionImmunity()
    {
        var e = WithBattle(s =>
        {
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        });
        var army = e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Eq(e.ResolutionDestroyableForTest(army, true).Length, 2, "both units are destroyable");
        e.GrantImmunity(Card(e, army[0].Id), ImmunityKind.DestroyInResolution);
        var left = e.ResolutionDestroyableForTest(army, true);
        Eq(left.Length, 1, "the immune unit is skipped");
        Eq(left[0].Id, army[1].Id, "and only that one");
        Check(e.State.Events.Any(x => x.Type == "immunityNegated" && x.CardId == army[0].Id), "logged");
        Check(!e.IsImmuneForTest(army[0].Id, ImmunityKind.Destroy, 1), "an ordinary destruction by an action is a different kind");
    }

    // ---- movement and prevention -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card) Mover(MovementLock? @lock = null, Action<MatchState>? more = null)
    {
        CardInstance card = null!;
        var e = WithBattle(s =>
        {
            card = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (@lock is not null) card.MovementLocks.Add(@lock);
            more?.Invoke(s);
        });
        return (e, card);
    }

    private static void MoveNegationImmunity()
    {
        var (e, card) = Mover(new MovementLock("fixture", 1, MovementKinds.Home, ByPlayer: 1));
        Check(!e.MoveHomeByEffectForTest(card.Id, 1), "another player's lock holds a card with no immunity");
        var (immune, immuneCard) = Mover(new MovementLock("fixture", 1, MovementKinds.Home, ByPlayer: 1));
        immune.GrantImmunity(Card(immune, immuneCard.Id), ImmunityKind.MoveNegation);
        Check(immune.MoveHomeByEffectForTest(immuneCard.Id, 1), "the immune card moves: the lock was another player's, so it cannot negate");
        var (own, ownCard) = Mover(new MovementLock("fixture", 1, MovementKinds.Home, ByPlayer: 0));
        own.GrantImmunity(Card(own, ownCard.Id), ImmunityKind.MoveNegation);
        Check(!own.MoveHomeByEffectForTest(ownCard.Id, 1), "a lock of his own player still holds");
        var (unknown, unknownCard) = Mover(new MovementLock("fixture", 1, MovementKinds.Home));
        unknown.GrantImmunity(Card(unknown, unknownCard.Id), ImmunityKind.MoveNegation);
        Check(!unknown.MoveHomeByEffectForTest(unknownCard.Id, 1), "a lock whose source is unknown still holds");
    }

    private static void PreventionImmunity()
    {
        // Action locks: the lock's source card is another player's.
        CardInstance locker = null!, locked = null!;
        var e = WithBattle(s =>
        {
            locker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            locked = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            locked.ActionLocks.Add(new(locker.Id, "Turn"));
        });
        Check(e.ActionLockedForTest(locked.Id), "locked");
        e.GrantImmunity(Card(e, locked.Id), ImmunityKind.Prevention);
        Check(!e.ActionLockedForTest(locked.Id), "another player's lock no longer applies");
        CardInstance own = null!, ownLocked = null!;
        var o = WithBattle(s =>
        {
            own = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            ownLocked = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            ownLocked.ActionLocks.Add(new(own.Id, "Turn"));
        });
        o.GrantImmunity(Card(o, ownLocked.Id), ImmunityKind.Prevention);
        Check(o.ActionLockedForTest(ownLocked.Id), "his own player's lock still binds");

        // Movement locks, which also refuse assigning.
        var (m, card) = Mover(new MovementLock("fixture", 1, MovementKinds.Assign, ByPlayer: 1));
        Check(m.MovementLockedForTest(card.Id, MovementKinds.Assign), "locked from assigning");
        m.GrantImmunity(Card(m, card.Id), ImmunityKind.Prevention);
        Check(!m.MovementLockedForTest(card.Id, MovementKinds.Assign), "another player's lock is ignored: he may be assigned again");
        var (u, unknown) = Mover(new MovementLock("fixture", 1, MovementKinds.Assign));
        u.GrantImmunity(Card(u, unknown.Id), ImmunityKind.Prevention);
        Check(u.MovementLockedForTest(unknown.Id, MovementKinds.Assign), "an unknown source still binds");
        var (a, mine) = Mover(new MovementLock("fixture", 1, MovementKinds.Assign, ByPlayer: 0));
        a.GrantImmunity(Card(a, mine.Id), ImmunityKind.Prevention);
        Check(a.MovementLockedForTest(mine.Id, MovementKinds.Assign), "his own player's binds");
    }

    private static void ForcePenaltyImmunity()
    {
        CardInstance first = null!, second = null!, by = null!;
        var e = WithBattle(s =>
        {
            first = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            second = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            by = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); by.GrantedAbilities.Add(new("costly-feud", "test-fixture", GrantDuration.Permanent));
        });
        e.GrantImmunity(Card(e, first.Id), ImmunityKind.ForcePenalty);
        Accept(e, Offer(e, 1, "costly-feud", by.Id, first.Id)); Settle(e);
        Eq(e.State.Cards[first.Id].Modifiers.Count(m => m.Stat == "Force"), 0, "the immune card took no Force penalty");
        Check(e.State.Events.Any(x => x.Type == "immunityNegated" && x.CardId == first.Id), "logged");
        var again = Edit(e, st => { st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; st.Cards[by.Id].AbilityUses.Clear(); });
        Accept(again, Offer(again, 1, "costly-feud", by.Id, second.Id)); Settle(again);
        Check(again.State.Cards[second.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount < 0), "another card still takes it");
    }

    // ---- provinces, Terrains, battlefields ------------------------------------------------------------------------------------------------------------------------------------------

    private static void ProvinceStrengthImmunity()
    {
        var e = WithBattle(_ => { });
        var before = e.EffectiveProvinceStrengthForTest(0, 0);
        e.GiveProvinceStrengthForTest(0, 0, -2, 1, "fixture");
        Eq(e.EffectiveProvinceStrengthForTest(0, 0), Math.Max(0, before - 2), "no immunity: the penalty applies");

        var imm = WithBattle(_ => { });
        imm.GrantPlayerImmunity(0, ImmunityKind.ProvinceStrength);
        imm.GiveProvinceStrengthForTest(0, 0, -2, 1, "fixture");
        Eq(imm.EffectiveProvinceStrengthForTest(0, 0), before, "another player's change is negated");
        Check(imm.State.Events.Any(x => x.Type == "provinceStrengthNegated"), "logged");
        imm.GiveProvinceStrengthForTest(0, 0, 2, 0, "fixture");
        Eq(imm.EffectiveProvinceStrengthForTest(0, 0), before + 2, "his own player's change applies");
        imm.GiveProvinceStrengthForTest(1, 0, -1, 1, "fixture");
        Check(imm.EffectiveProvinceStrengthForTest(1, 0) <= imm.State.Players[1].Provinces[0].Strength, "the other player's province is not covered");

        // Read side: another player's card in play that gives the province strength is not counted.
        CardInstance source = null!;
        var built = WithBattle(s => source = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0));
        var r = Edit(built, st =>
        {
            st.Players[0].Provinces[0].SourceBonuses.Add(new ProvinceBonus(source.Id, 3));
            st.Players[1].Provinces[0].SourceBonuses.Add(new ProvinceBonus(source.Id, 3));
        });
        var raised = r.EffectiveProvinceStrengthForTest(0, 0);
        var otherRaised = r.EffectiveProvinceStrengthForTest(1, 0);
        r.GrantPlayerImmunity(0, ImmunityKind.ProvinceStrength);
        Eq(r.EffectiveProvinceStrengthForTest(0, 0), raised - 3, "its bonus is not counted");
        Eq(r.EffectiveProvinceStrengthForTest(1, 0), otherRaised, "the other player's own province still counts its own card");
    }

    private static void TerrainImmunity()
    {
        CardInstance terrain = null!, other = null!, creature = null!;
        MatchEngine Build() => WithBattle(s =>
        {
            terrain = Add(s, AncientFeud, 0, Zone.Battlefield, province: 0);
            other = Add(s, Shiotome, 1, Zone.Battlefield, province: 0);
            creature = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        });
        var e = Build();
        Check(e.DiscardTerrainByEffectForTest(terrain.Id, 1, other.Id), "no immunity: another player's effect discards it");
        var a = Build();
        a.GrantImmunity(Card(a, terrain.Id), ImmunityKind.TerrainDestroy);
        Check(!a.DiscardTerrainByEffectForTest(terrain.Id, 1, other.Id), "immune to other players' effects");
        Check(a.DiscardTerrainByEffectForTest(terrain.Id, null, null), "the battle's own discard has no source player and still happens");
        var b = Build();
        b.GrantImmunity(Card(b, terrain.Id), ImmunityKind.TerrainDestroy, source: ImmunitySource.OtherTerrains);
        Check(!b.DiscardTerrainByEffectForTest(terrain.Id, 1, other.Id), "immune to another Terrain's effect");
        var c = Build();
        c.GrantImmunity(Card(c, terrain.Id), ImmunityKind.TerrainDestroy, source: ImmunitySource.OtherTerrains);
        Check(c.DiscardTerrainByEffectForTest(terrain.Id, 1, creature.Id), "but a card that is not a Terrain can discard it");
    }

    private static void BattlefieldMovementImmunity()
    {
        CardInstance defender = null!, attacker = null!, mover = null!;
        MatchEngine Build() => WithBattle(s =>
        {
            s.UnresolvedBattlefields = [0, 1, 2, 3];
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            mover = Add(s, "training-crane-veteran", 0, Zone.Home);
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        });
        var free = Build();
        Check(free.MoveHomeByEffectForTest(defender.Id, 1), "no immunity: an effect sends him home");
        var e = Build();
        e.GrantBattlefieldImmunity(0, ImmunityKind.MovementAtBattlefield);
        Check(!e.MoveHomeByEffectForTest(defender.Id, 1), "movement from the immune battlefield is negated");
        Eq(e.State.Cards[defender.Id].Zone, Zone.Battlefield, "he stays");
        Check(!e.MoveToBattlefieldByEffectForTest(mover.Id, 0, 1), "movement to it is negated");
        Check(e.MoveToBattlefieldByEffectForTest(mover.Id, 1, 1), "movement to another battlefield is not");
        Check(e.MoveHomeByEffectForTest(attacker.Id, 1), "nor from another battlefield");
        var rules = Build();
        rules.GrantBattlefieldImmunity(0, ImmunityKind.MovementAtBattlefield);
        Check(rules.MoveHomeByEffectForTest(defender.Id, null), "a movement no card effect makes (the battle's own) is not negated");

        // A standing rule follows its carrier: a Terrain at the battlefield.
        var carrierRule = new StandingImmunity("test-p12-harbor", d => d.Id == Shiotome, ImmunityKind.MovementAtBattlefield, ImmunityScope.Battlefield);
        var withCarrier = WithBattle(s =>
        {
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, Shiotome, 1, Zone.Battlefield, province: 0);
        });
        Check(!WithRule(carrierRule, () => withCarrier.MoveHomeByEffectForTest(defender.Id, 1)), "while the carrier stands there: negated");
        var carrierGone = Edit(withCarrier, st => { foreach (var c in st.Cards.Values.Where(c => c.DefinitionId == Shiotome)) c.Zone = Zone.FateDiscard; });
        Check(WithRule(carrierRule, () => carrierGone.MoveHomeByEffectForTest(defender.Id, 1)), "the carrier left: his movement is not negated");
    }

    private static void BeforeWatches()
    {
        var (e, target, killers) = Duel();
        e.GrantImmunity(Card(e, target.Id), ImmunityKind.Destroy);
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetCardId = target.Id });
        Destroy(e, killers[0], target);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "negated");
        Eq(e.State.NegationWatches.Count, 1, "the watch was not consumed: the immunity negated first");
        Check(!e.WatchNegated(WatchKind.Destroy, target.Id), "and no watch negation is recorded");
    }

    private static void RoundTrip()
    {
        var (e, target, _) = Duel();
        var fresh = JsonSerializer.Serialize(e.State);
        Check(!fresh.Contains("\"Immunities\"") && !fresh.Contains("\"ImmunitySequence\""), "an unused ledger is omitted from the saved game");
        e.GrantImmunity(Card(e, target.Id), ImmunityKind.Prevention, ImmunityUntil.Phase, ImmunitySource.Any, ownerCardId: target.Id);
        e.GrantPlayerImmunity(0, ImmunityKind.ProvinceStrength, ImmunityUntil.TurnBoundary, boundary: e.UntilOwnersNextTurnBegins(0), targetKeyword: "Samurai", targetType: CardType.Personality);
        e.GrantBattlefieldImmunity(0, ImmunityKind.MovementAtBattlefield, ImmunityUntil.Permanent);
        var json = JsonSerializer.Serialize(e.State);
        var restored = JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(restored.Immunities!.Count, 3, "immunities");
        Check(restored.Immunities[0] is { Kind: ImmunityKind.Prevention, Until: ImmunityUntil.Phase, Source: ImmunitySource.Any } && restored.Immunities[0].UntilPhase == e.State.Phase, "card immunity fields");
        Check(restored.Immunities[1] is { Kind: ImmunityKind.ProvinceStrength, PlayerId: 0, TargetKeyword: "Samurai", Until: ImmunityUntil.TurnBoundary } && restored.Immunities[1].Boundary is not null, "player immunity fields");
        Check(restored.Immunities[2] is { Kind: ImmunityKind.MovementAtBattlefield, Battlefield: 0 }, "battlefield immunity fields");
        Eq(restored.ImmunitySequence, 3, "the id sequence");
        var resumed = MatchEngine.FromTestState(restored);
        Check(resumed.IsImmuneForTest(target.Id, ImmunityKind.Prevention, 0), "the restored ledger still answers");
        Eq(resumed.GrantImmunity(Card(resumed, target.Id), ImmunityKind.Bow).Id, "immunity-4", "and ids continue without a clash");
    }

    private static void Registry()
    {
        Check(ReferenceEquals(EngineHooks.StandingImmunities.Items, EngineHooks.StandingImmunities.Items), "the channel is read at startup and its snapshot is stable");
        Check(MatchEngine.TestStandingImmunities is null or { Count: 0 }, "the test seam is empty outside a test");
    }
}
