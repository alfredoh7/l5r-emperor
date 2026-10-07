using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #717, primitive P13 (RuleFlags.cs): the phase and battle rule flags. Every flag is set through the engine call a card's RunEffects would make (SetRuleFlag), by the test-only fixture abilities
// registered below or directly, and each is proven at the one choke point that reads it, with the unchanged state of the refusals and the unflagged default beside it. No card is wired: the cards this
// unlocks (Chagatai's Wrath, Lantern Oil, Temple Grounds, Shosuro Takazaki, Journey's End Siege, The Madness Spreads, An Empire of Madness, The False Path) are a later wave's.
internal static class RuleFlagsTests
{
    private const string EndPhase = "test-p13-end-phase", BattleFlag = "test-p13-battle-flag", Incite = "incite-phantoms-bow";
    private const string EngageHonor = "training-engage-honor-strategy", EngageAbility = "training-engage-gain-honor", TerrainCard = "training-terrain", LimitedDraw = "training-limited-draw";

    public static void Run(TestRunner runner) => runner.Run("rule-flags", new (string, Action)[]
    {
        ("No flag, no change: a match that never set a flag saves no new field, offers the Attacker his Engage action, pays Honor for destroyed units, allows Terrains and decides by Enlightenment as before", NoFlagNoChange),
        ("end-action-phase: a Limited action ends the Action Phase mid-command, the next phase of the turn's order begins for the active player, nobody else acts, and an illegal use changes nothing", EndActionPhaseFlag),
        ("no-engage-for-attacker: the Attacker's Engage actions at the flagged battlefield are neither offered nor accepted, the Defender's still are, another battlefield is unaffected and a window nobody can use does not open", EngageBar),
        ("negate-battle-action-without-units: a Battle action by a player without units there is taken, its cost paid and its effect negated, unless the flag's owner controls a Cavalry unit there; a player with units is untouched", NegateWithoutUnits),
        ("no-destruction-honor-for-others: the winner gains no Honor for the flag owner's destroyed units in that battle's resolution, the owner still gains for his own win, and the next battle is normal", DestructionHonor),
        ("defenders-may-not-play-terrains: the Defender's Terrain play is refused and listed as unavailable with the reason, the Attacker's is not, and it ends with the turn", DefendersTerrains),
        ("limited-as-open-once-per-turn: the flag is P4's standing LimitedAsOpen rule (stored there, not here) and a Limited action on a Strategy is offered as an Open action once per turn", LimitedAsOpenFlag),
        ("no-enlightenment-victory: the flag's owner does not win by Enlightenment at the start of his turn, the other player's flag does not stop him, and with the flag gone he wins", NoEnlightenment),
        ("madness-loss: the player who ends his turn with the threshold of Madness tokens on his Personalities loses; below it, with the flag for the other player, or after it expired, nothing happens", MadnessLoss),
        ("Scopes: turn, battle, player (until a turn begins), battlefield and game flags expire as they say, a flag tied to a card ends with it, and expired flags are dropped as the next is set", ScopeExpiry),
        ("The store is omitted from the saved game until used and round-trips once it is, with the id sequence continuing and unknown kinds and scopes refused without a trace", RoundTrip),
    });

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    // The engine's refusal of an illegal request is its private RuleViolation: the request must throw it and nothing else.
    private static void Refused(Action request, string failure)
    {
        try { request(); }
        catch (Exception refusal) when (refusal.GetType().Name == "RuleViolation") { return; }
        throw new Exception(failure);
    }
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);

    // ---- the fixture callbacks (TEST ONLY: nothing registers these ids in the shipped catalogue) -----------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.Abilities.Register("TestP13Abilities", 19600, static abilities =>
        {
            abilities.Add(EndPhase, new("Limited", "Test P13: end the Action Phase", "none"));
            abilities.Add(BattleFlag, new("Battle", "Test P13: other players gain no Honor for destroying your cards in this battle's resolution", "none"));
            return abilities;
        });
        EngineHooks.RunEffects.Register("TestP13Run", 19600, static (engine, context) => RunTest(engine, context.id, context.player, context.source));
    }

    private static bool? RunTest(MatchEngine engine, string id, PlayerState player, CardInstance? source)
    {
        var title = source is null ? "Test" : source.Id;
        switch (id)
        {
            case EndPhase: engine.SetRuleFlag(RuleFlagKinds.EndActionPhase, source: title); return false;
            case BattleFlag: engine.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, player.Id, title); return false;
            default: return null;
        }
    }

    // ---- fixtures ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static GameCommand Use(MatchEngine e, int player, string ability, string card, string? target = null) => new("useAbility", player, e.Version, CardId: card, AbilityId: ability, TargetId: target);
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
    private static bool HasFlag(MatchEngine e, string kind) => e.State.RuleFlags?.Any(f => f.Kind == kind) == true;

    // Player 0 attacks (BattleSelection), both Strongholds published; the Attacker's army and the Defender's at battlefield 0.
    private static MatchState Selection(params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0] : battlefields).ToList();
        return s;
    }

    // The Combat Segment: player 1 attacks province 0 against player 0's unit (the economy scenario Table with both hands emptied).
    private static (MatchEngine Engine, CardInstance Attacker, CardInstance Defender) Combat(int attackerUnits = 2, int defenderUnits = 1, Action<MatchState>? more = null)
    {
        CardInstance a = null!, d = null!;
        var s = Batch336Support.Table(Phase.Battle, 1);
        for (var i = 0; i < attackerUnits; i++) { var c = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); a ??= c; }
        for (var i = 0; i < defenderUnits; i++) { var c = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); d ??= c; }
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), a, d);
    }

    // Both players pass in succession in the Combat Segment: the battle resolves.
    private static void ResolveByPassing(MatchEngine e) { Accept(e, PassC(e)); Accept(e, PassC(e)); }

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 400 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            var command = pick.Command!;
            if (command.Type == "discardHand") { var hand = e.State.Players[mover].Hand; command = command with { CardIds = hand.Take(hand.Count - 8).ToArray() }; }
            var result = e.Execute(command);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    // ---- 1. no flag, no change -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NoFlagNoChange()
    {
        // The saved game has no trace of the store, and every read answers "not barred".
        var (e, _, defender) = Combat();
        var json = Json(e);
        Check(!json.Contains("\"RuleFlags\"") && !json.Contains("\"RuleFlagSequence\"") && !json.Contains("\"ActionPhaseEndedAtVersion\""), "an unused store is omitted from the saved game");
        Check(!e.EngageActionBarred(0) && !e.EngageActionBarred(1) && !e.EnlightenmentBarred(0) && e.RuleFlagTerrainReason(0) is null && e.LiveRuleFlagCount() == 0, "every read is false");
        // Honor for destroyed units: two Lion units against one Crane unit: the Attacker gains 2 per destroyed unit.
        var before = e.State.Players[1].Honor;
        ResolveByPassing(e);
        Check(e.State.Cards[defender.Id].Zone != Zone.Battlefield, "the defending unit was destroyed");
        Eq(e.State.Players[1].Honor, before + 2, "and the winner gained 2 Honor for it, as ever");
        Check(!Json(e).Contains("\"RuleFlags\""), "still no store after a whole battle");
        // The Attacker's Engage action is offered (a unit each; both hold the fixture Strategy).
        var s = Selection(); Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s, EngageHonor, 0, Zone.Hand);
        var open = MatchEngine.FromTestState(s);
        Accept(open, new GameCommand("selectBattle", 0, open.Version, Province: 0));
        Eq(open.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        Check(Batch336Support.Options(open, 0, EngageAbility, mine.Id).Any(o => o.Enabled), "and the Attacker's Engage action is on offer");
        // A Terrain play and an Enlightenment test carry no reason against them.
        var terrain = Combat(1, 1, st => Add(st, TerrainCard, 0, Zone.Hand));
        Check(terrain.Engine.QueryActions(1).All(a => a.Type != "playStrategy" || a.Reason is null || !a.Reason.Contains("defending players")), "no Terrain reason");
    }

    // ---- 2. end-action-phase ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EndActionPhaseFlag()
    {
        MatchEngine Fresh(out CardInstance source, Action<MatchState>? edit = null)
        {
            var s = Batch336Support.Table(Phase.Action, 1);
            source = Add(s, "training-farm", 1, Zone.Home);
            source.GrantedAbilities.Add(new GrantedAbility(EndPhase, "test-fixture", GrantDuration.Permanent));
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }

        var e = Fresh(out var card);
        Check(Batch336Support.Options(e, 1, EndPhase, card.Id).Any(o => o.Enabled), "the Limited action is the active player's, in the Action Phase");
        Accept(e, Batch336Support.Enabled(e, 1, EndPhase, card.Id).Command!);
        Eq(e.State.Phase, Phase.AttackDeclaration, "the Action Phase ended mid-command: the Attack Phase begins");
        Eq(e.State.PriorityPlayerId, 1, "the active player has the priority (the action's own tail did not hand it to the other player)");
        Eq(e.State.ConsecutivePasses, 0);
        Check(e.State.Events.Any(x => x.Type == "actionPhaseEnded"), "logged");
        Check(e.State.Events.Any(x => x.Type == "phaseChanged" && x.Message.Contains("AttackDeclaration")), "through the ordinary phase change");
        Check(HasFlag(e, RuleFlagKinds.EndActionPhase) == false && e.State.RuleFlags is null, "nothing is stored: the effect is immediate");
        // The attack decision is the active player's, as it would be after two passes.
        Check(e.QueryActions(1).Any(a => a.Enabled && a.Type is "declareAttack" or "declineAttack" or "pass"), "he is asked to declare or decline the attack");
        // The next command is a plain one: the stale marker never matches again.
        Passing(e, x => x.State.Phase != Phase.AttackDeclaration); // the following commands are plain ones: the stale marker never matches again
        Check(e.State.Phase != Phase.Action, "and the game moves on from the Attack decision");

        // The turn's phase order is honoured: with the Dynasty Phase first it follows the Action Phase.
        var swapped = Fresh(out var swappedCard, st => { st.PhaseOrder.Order = [PhaseOrderRules.Straighten, PhaseOrderRules.Events, PhaseOrderRules.Action, PhaseOrderRules.Dynasty, PhaseOrderRules.Attack, PhaseOrderRules.End]; st.PhaseOrder.ChangedTurn = st.TurnNumber; });
        Accept(swapped, Batch336Support.Enabled(swapped, 1, EndPhase, swappedCard.Id).Command!);
        Eq(swapped.State.Phase, Phase.Dynasty, "the Dynasty Phase comes next in a turn that runs in the other order");

        Eq(swapped.State.PriorityPlayerId, 1, "and the other player gets no Open action in between");

        // Illegal: the other player cannot take the active player's Limited action; outside the Action Phase the effect does nothing; each changes nothing.
        var idle = Fresh(out var idleCard);
        var other = Edit(idle, st => st.PriorityPlayerId = 0);
        Reject(other, Use(other, 0, EndPhase, idleCard.Id));
        var battle = Combat().Engine;
        var before = Json(battle);
        Check(!battle.EndActionPhase("Test"), "outside the Action Phase there is nothing to end");
        Eq(Json(battle), before, "and nothing changed");
    }

    // ---- 3. no-engage-for-attacker ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EngageBar()
    {
        MatchEngine Window(int flaggedBattlefield, bool attackerHolds, bool defenderHolds, bool flagged = true)
        {
            var s = Selection(0, 1);
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            if (attackerHolds) Add(s, EngageHonor, 0, Zone.Hand);
            if (defenderHolds) Add(s, EngageHonor, 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            if (flagged) e.SetRuleFlag(RuleFlagKinds.NoEngageForAttacker, RuleFlagScope.Battlefield, 1, "Lantern Oil", battlefield: flaggedBattlefield);
            return e;
        }

        // Flagged at the battlefield: the Attacker's Engage action is hidden and refused, the Defender's is on offer.
        var e = Window(0, true, true);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Defender can use the window, so it opens");
        var attackerCard = e.State.Players[0].Hand.Select(id => e.State.Cards[id]).Single();
        var defenderCard = e.State.Players[1].Hand.Select(id => e.State.Cards[id]).Single();
        Check(e.EngageActionBarred(0) && !e.EngageActionBarred(1), "the bar is the Attacker's");
        Check(!Batch336Support.Options(e, 0, EngageAbility, attackerCard.Id).Any(), "the Attacker is not offered the Engage action at all");
        Reject(e, Use(e, 0, EngageAbility, attackerCard.Id));
        Accept(e, Decline(e));
        Check(Batch336Support.Options(e, 1, EngageAbility, defenderCard.Id).Any(o => o.Enabled), "the Defender is");
        var honor = e.State.Players[1].Honor;
        Accept(e, Use(e, 1, EngageAbility, defenderCard.Id));
        Eq(e.State.Players[1].Honor, honor + 1, "and his Engage action resolves");

        // Another battlefield: the flag is for battlefield 1, the battle is at 0.
        var elsewhere = Window(1, true, true);
        Accept(elsewhere, new GameCommand("selectBattle", 0, elsewhere.Version, Province: 0));
        Check(Batch336Support.Options(elsewhere, 0, EngageAbility).Any(o => o.Enabled), "a flag at another battlefield bars nothing here");
        // A flag without a battlefield bars the Attacker everywhere (player scope, with an Until).
        var everywhere = Window(0, true, true, flagged: false);
        everywhere.SetRuleFlag(RuleFlagKinds.NoEngageForAttacker, RuleFlagScope.Player, 1, "Test", until: everywhere.UntilOwnersNextTurnBegins(1));
        Accept(everywhere, new GameCommand("selectBattle", 0, everywhere.Version, Province: 0));
        Check(!Batch336Support.Options(everywhere, 0, EngageAbility).Any(), "a flag for every battlefield bars it at this one");
        // Nobody left to use the window: it does not open (the Attacker's action is the only one in play).
        var alone = Window(0, true, false);
        Accept(alone, new GameCommand("selectBattle", 0, alone.Version, Province: 0));
        Check(alone.State.PendingChoice?.Type != "engageReaction" && alone.State.PendingEngage is null, "an Engage window nobody can use does not open");
        // The same position without the flag opens the window for the Attacker.
        var open = Window(0, true, false, flagged: false);
        Accept(open, new GameCommand("selectBattle", 0, open.Version, Province: 0));
        Eq(open.State.PendingChoice?.Type, "engageReaction");
    }

    // ---- 4. negate-battle-action-without-units ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void NegateWithoutUnits()
    {
        // Player 1 attacks with no unit at the battlefield: his Spirit Personality is at Home and takes Incite the Phantoms' Absent Battle action (bow the performer, bow the target); player 0 holds the
        // battlefield. Player 0 owns the flag (Temple Grounds).
        (MatchEngine E, CardInstance Performer, CardInstance Target) Position(bool attackerHasUnit = false, bool ownerCavalry = false, bool flagged = true)
        {
            var s = Batch336Support.Table(Phase.Battle, 1);
            var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (ownerCavalry) defender.GrantedKeywords.Add(new GrantedKeyword("Cavalry", "test-fixture", "Permanent"));
            if (attackerHasUnit) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var performer = Add(s, "training-lion-samurai", 1, Zone.Home);
            performer.GrantedKeywords.Add(new GrantedKeyword("Spirit", "test-fixture", "Permanent"));
            performer.GrantedAbilities.Add(new GrantedAbility(Incite, "test-fixture", GrantDuration.Permanent));
            var e = MatchEngine.FromTestState(s);
            if (flagged) e.SetRuleFlag(RuleFlagKinds.NegateBattleActionWithoutUnits, RuleFlagScope.Battlefield, 0, "Temple Grounds", battlefield: 0);
            return (e, performer, defender);
        }
        GameCommand Action(MatchEngine e, CardInstance performer, CardInstance target) => new("useAbility", 1, e.Version, CardId: performer.Id, AbilityId: Incite, PerformerId: performer.Id, TargetId: target.Id);

        var (e, performer, target) = Position();
        Accept(e, Action(e, performer, target));
        Check(e.State.Cards[performer.Id].Bowed, "its cost was paid (the performer is bowed)");
        Check(!e.State.Cards[target.Id].Bowed, "its effect was negated: the target is not bowed");
        Check(e.State.Events.Any(x => x.Type == "actionNegated" && x.Message.Contains("Temple Grounds")), "the negation is logged with its source");
        Eq(e.State.PriorityPlayerId, 0, "and the action was taken: priority passed to the other player");

        // The flag's owner controls a Cavalry unit there: the action is not negated.
        var (cavalry, cavalryPerformer, cavalryTarget) = Position(ownerCavalry: true);
        Accept(cavalry, Action(cavalry, cavalryPerformer, cavalryTarget));
        Check(cavalry.State.Cards[cavalryTarget.Id].Bowed && cavalry.State.Cards[cavalryPerformer.Id].Bowed, "unless the flag's owner controls a Cavalry unit there, the effect applies (and the cost is paid as ever)");
        // A player with a unit there is not "a player without units": untouched.
        var (armed, armedPerformer, armedTarget) = Position(attackerHasUnit: true);
        Accept(armed, Action(armed, armedPerformer, armedTarget));
        Check(armed.State.Cards[armedTarget.Id].Bowed, "a player with a unit there takes his action as ever");
        // No flag: nothing is negated.
        var (plain, plainPerformer, plainTarget) = Position(flagged: false);
        Accept(plain, Action(plain, plainPerformer, plainTarget));
        Check(plain.State.Cards[plainTarget.Id].Bowed && !plain.State.Events.Any(x => x.Type == "actionNegated"), "with no flag the action resolves");
        // A flag at another battlefield does not reach this one.
        var (away, awayPerformer, awayTarget) = Position(flagged: false);
        away.SetRuleFlag(RuleFlagKinds.NegateBattleActionWithoutUnits, RuleFlagScope.Battlefield, 0, "Temple Grounds", battlefield: 1);
        Accept(away, Action(away, awayPerformer, awayTarget));
        Check(away.State.Cards[awayTarget.Id].Bowed, "a Terrain at another battlefield negates nothing here");
    }

    // ---- 5. no-destruction-honor-for-others ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DestructionHonor()
    {
        // Player 0 (the Defender) holds one unit against two; he sets the flag with a Battle action (Shosuro Takazaki's shape), so the winner gains nothing for destroying it.
        var (e, _, defender) = Combat(2, 1, st =>
        {
            st.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).GrantedAbilities.Add(new GrantedAbility(BattleFlag, "test-fixture", GrantDuration.Permanent));
            st.PriorityPlayerId = 0;
        });
        var source = defender;
        Accept(e, Batch336Support.Enabled(e, 0, BattleFlag, source.Id).Command!);
        Check(HasFlag(e, RuleFlagKinds.NoDestructionHonorForOthers) && e.State.RuleFlags![0] is { Scope: RuleFlagScope.Battle, PlayerId: 0 }, "the action stored a battle-scope flag for its player");
        var honor = e.State.Players[1].Honor;
        while (e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null) Accept(e, PassC(e));
        Check(e.State.Cards[defender.Id].Zone != Zone.Battlefield, "the unit was destroyed");
        Eq(e.State.Players[1].Honor, honor, "and the winner gained no Honor for it");
        Check(e.State.Events.Any(x => x.Type == "ruleFlagHonorWithheld"), "logged");
        Eq(e.LiveRuleFlagCount(), 0, "the flag ended with the battle");

        // The flag's owner winning gains normally for the units he destroys: the flag is about the owner's own cards.
        var win = Combat(1, 2);
        win.Engine.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, 0, "Test");
        var ownerHonor = win.Engine.State.Players[0].Honor;
        ResolveByPassing(win.Engine);
        Eq(win.Engine.State.Players[0].Honor, ownerHonor + 2, "the owner gains for the units he destroyed");

        // A flag of the other player's does not protect this player's cards; a tie is judged each way.
        var otherOwner = Combat();
        otherOwner.Engine.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, 1, "Test");
        var base1 = otherOwner.Engine.State.Players[1].Honor;
        ResolveByPassing(otherOwner.Engine);
        Eq(otherOwner.Engine.State.Players[1].Honor, base1 + 2, "a flag of the winner's own does not stop his gain");
        var tie = Combat(1, 1, st =>
        {
            foreach (var c in st.Cards.Values.Where(c => c.Zone == Zone.Battlefield)) c.DefinitionId = "training-crane-samurai";
        });
        tie.Engine.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, 0, "Test");
        var t0 = tie.Engine.State.Players[0].Honor; var t1 = tie.Engine.State.Players[1].Honor;
        ResolveByPassing(tie.Engine);
        Eq(tie.Engine.State.Players[1].Honor, t1, "a tied battle: the other player gains nothing for the owner's unit");
        Eq(tie.Engine.State.Players[0].Honor, t0 + 1, "and the owner gains for the other's (a tie pays 1 per destroyed unit)");

        // Turn scope is not the battle: nothing but a Battle-scope flag needs a battle, and setting one with none open is refused without a trace.
        var idle = MatchEngine.FromTestState(Batch336Support.Table(Phase.Action, 1));
        var json = Json(idle);
        Refused(() => idle.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, 0, "Test"), "a battle flag was set outside a battle");
        Eq(Json(idle), json, "the refusal left the state alone");
    }

    // ---- 6. defenders-may-not-play-terrains ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DefendersTerrains()
    {
        // Player 1 attacks (active), player 0 defends and holds a Terrain; the Attacker holds one too.
        (MatchEngine E, CardInstance Def, CardInstance Att) Position(bool flagged = true)
        {
            CardInstance def = null!, att = null!;
            var (e, _, _) = Combat(1, 1, st => { def = Add(st, TerrainCard, 0, Zone.Hand); att = Add(st, TerrainCard, 1, Zone.Hand); });
            if (flagged) e.SetRuleFlag(RuleFlagKinds.DefendersMayNotPlayTerrains, RuleFlagScope.Turn, 1, "Journey's End Siege");
            return (e, def, att);
        }

        var (e, defTerrain, attTerrain) = Position();
        var defending = Edit(e, st => st.PriorityPlayerId = 0);
        var result = defending.Execute(new GameCommand("playStrategy", 0, defending.Version, CardId: defTerrain.Id));
        Check(!result.Accepted && result.Error!.Contains("defending players may not play Terrains"), $"the Defender's Terrain play is refused with the reason: {result.Error}");
        Reject(defending, new GameCommand("playStrategy", 0, defending.Version, CardId: defTerrain.Id));
        var listed = defending.QueryActions(0).Single(a => a.Type == "playStrategy" && a.CardId == defTerrain.Id);
        Check(!listed.Enabled && listed.Reason!.Contains("Journey's End Siege"), "listed as unavailable, with the reason");
        Eq(defending.State.Cards[defTerrain.Id].Zone, Zone.Hand, "the card stayed in his hand");
        Accept(e, new GameCommand("playStrategy", 1, e.Version, CardId: attTerrain.Id));
        Eq(e.State.Cards[attTerrain.Id].Zone, Zone.Battlefield, "the Attacker plays his Terrain as ever");

        // The flag is the turn's: next turn the Defender (now the same seat, a turn later) plays it.
        var (later, laterDef, _) = Position();
        later = Edit(later, st => { st.TurnNumber++; st.PriorityPlayerId = 0; });
        Accept(later, new GameCommand("playStrategy", 0, later.Version, CardId: laterDef.Id));
        Eq(later.State.Cards[laterDef.Id].Zone, Zone.Battlefield, "once the turn is over the Defender may play a Terrain");

        // With no flag: the Defender plays it.
        var (plain, plainDef, _) = Position(flagged: false);
        plain = Edit(plain, st => st.PriorityPlayerId = 0);
        Accept(plain, new GameCommand("playStrategy", 0, plain.Version, CardId: plainDef.Id));
        Eq(plain.State.Cards[plainDef.Id].Zone, Zone.Battlefield);
    }

    // ---- 7. limited-as-open-once-per-turn --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LimitedAsOpenFlag()
    {
        var s = Batch336Support.Table(Phase.Action, 1); s.PriorityPlayerId = 0; // player 1's turn, player 0's opportunity
        CardInstance Strategy(int owner)
        {
            var card = Add(s, "training-duel-open", owner, Zone.Hand);
            card.Printed = CardCatalog.Get("training-duel-open") with { Abilities = [LimitedDraw] };
            return card;
        }
        var first = Strategy(0); var second = Strategy(0);
        var e = MatchEngine.FromTestState(s);
        Check(!Batch336Support.Options(e, 0, LimitedDraw, first.Id).Any(o => o.Enabled), "without the flag a Limited action is not offered on another player's turn");
        var stored = e.SetRuleFlag(RuleFlagKinds.LimitedAsOpenOncePerTurn, RuleFlagScope.Player, 0, "The Madness Spreads", until: e.UntilOwnersSecondTurnBegins(0));
        Check(stored is null && e.State.RuleFlags is null, "the flag is P4's standing rule: no record in this store");
        Check(e.State.TurnScopedRules.Count == 1 && e.State.TurnScopedRules[0] is { Kind: TurnRuleKinds.LimitedAsOpen, Amount: 1, Arg: "Strategy", PlayerId: 0 }, "once per turn, on a Strategy, for its player");
        Check(Batch336Support.Options(e, 0, LimitedDraw, first.Id).Any(o => o.Enabled) && Batch336Support.Options(e, 0, LimitedDraw, second.Id).Any(o => o.Enabled), "each Strategy's Limited action is offered as an Open action");
        Accept(e, Batch336Support.Enabled(e, 0, LimitedDraw, first.Id).Command!);
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!Batch336Support.Options(again, 0, LimitedDraw, second.Id).Any(o => o.Enabled), "once per turn: the second is not offered this turn");
        Check(Batch336Support.Options(Edit(again, st => st.TurnNumber++), 0, LimitedDraw, second.Id).Any(o => o.Enabled), "and the next turn restores it");
    }

    // ---- 8. no-enlightenment-victory -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine FiveRings()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var ring in new[] { "training-ring-of-air", "training-ring-of-water", "training-ring-of-void", "training-ring-of-fire-stub", "training-ring-of-earth-stub" }) Add(s, ring, 0, Zone.Home);
        return MatchEngine.FromTestState(s);
    }

    private static void NoEnlightenment()
    {
        // Without the flag: player 0 wins as player 0's next turn begins (the reference behaviour).
        var control = FiveRings();
        var controlStart = control.State.TurnNumber;
        Passing(control, x => x.State.WinnerId is not null || x.State.TurnNumber >= controlStart + 3);
        Eq(control.State.VictoryReason, "Enlightenment victory", "the control game is won by Enlightenment");

        var barred = FiveRings();
        barred.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 0, "The False Path");
        var start = barred.State.TurnNumber;
        Passing(barred, x => x.State.WinnerId is not null || x.State.TurnNumber >= start + 4 && x.State.Phase == Phase.Action);
        Check(barred.State.WinnerId is null, "the flag's owner has five Rings and does not win by Enlightenment");
        Check(barred.EnlightenmentBarred(0) && !barred.EnlightenmentBarred(1), "the bar is the owner's");

        // The other player's flag does not stop him; and a flag that ended does not either.
        var other = FiveRings();
        other.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 1, "The False Path");
        var otherStart = other.State.TurnNumber;
        Passing(other, x => x.State.WinnerId is not null || x.State.TurnNumber >= otherStart + 3);
        Eq(other.State.VictoryReason, "Enlightenment victory", "a flag of the other player's leaves him free to win");
        var ended = FiveRings();
        var stronghold = ended.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home);
        ended.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 0, "The False Path", whileInPlay: stronghold.Id);
        Check(ended.EnlightenmentBarred(0), "while its card is in play");
        ended = Edit(ended, st => st.Cards[stronghold.Id].Zone = Zone.DynastyDiscard);
        Check(!ended.EnlightenmentBarred(0), "and not once the card has left play");
    }

    // ---- 9. madness-loss ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine WithMadness(int owner, int tokens, Action<MatchEngine>? flag = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var host = Add(s, "training-crane-samurai", owner, Zone.Home);
        for (var i = 0; i < tokens; i++) host.Tokens.Add(new CardToken($"madness-{i}", "Madness", "test-fixture"));
        var e = MatchEngine.FromTestState(s);
        flag?.Invoke(e);
        return e;
    }

    private static void MadnessLoss()
    {
        // Player 0 (active in the scenario) ends his turn with 3 tokens against a threshold of 3: he loses.
        var e = WithMadness(0, 3, x => x.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "An Empire of Madness", amount: 3));
        Eq(e.MadnessTokensOn(0), 3);
        var turn0 = e.State.TurnNumber;
        Passing(e, x => x.State.WinnerId is not null || x.State.TurnNumber > turn0);
        Eq(e.State.WinnerId, 1, "his opponent won");
        Check(e.State.VictoryReason!.Contains("Madness"), "for the madness");
        Check(e.State.Events.Any(x => x.Type == "madnessLoss"), "logged");

        // One short of the threshold: nothing.
        var below = WithMadness(0, 2, x => x.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "An Empire of Madness", amount: 3));
        Passing(below, x => x.State.WinnerId is not null || x.State.TurnNumber > turn0);
        Check(below.State.WinnerId is null, "below the threshold the game goes on");
        // A flag for the other player only: his turn's end is the check, and player 0's tokens are not his.
        var forOther = WithMadness(0, 5, x => x.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, 1, "Test", amount: 3));
        Passing(forOther, x => x.State.WinnerId is not null || x.State.TurnNumber > turn0);
        Check(forOther.State.WinnerId is null, "a flag for the other player does not apply to him");
        // Expired: a Turn-scope flag set last turn.
        var expired = WithMadness(0, 5, x => x.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Turn, null, "Test", amount: 3));
        expired = Edit(expired, st => st.TurnNumber++);
        var expiredTurn = expired.State.TurnNumber;
        Passing(expired, x => x.State.WinnerId is not null || x.State.TurnNumber > expiredTurn);
        Check(expired.State.WinnerId is null, "an expired flag decides nothing");
        // A madness-loss flag needs a threshold.
        var none = WithMadness(0, 1);
        var json = Json(none);
        Refused(() => none.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "Test"), "a flag with no threshold was set");
        Eq(Json(none), json, "refused without a trace");
    }

    // ---- 10. scopes --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ScopeExpiry()
    {
        var (e, _, _) = Combat();
        var host = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var turn = e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Turn, 0, "Turn")!;
        var battle = e.SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, 0, "Battle")!;
        var player = e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Player, 0, "Player", until: e.UntilOwnersNextTurnBegins(0))!;
        var field = e.SetRuleFlag(RuleFlagKinds.NoEngageForAttacker, RuleFlagScope.Battlefield, 0, "Battlefield")!;
        var game = e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 1, "Game")!;
        var tied = e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 1, "Card", whileInPlay: host.Id)!;
        Eq(battle.Battlefield, 0, "a battle flag defaults to the current battlefield"); Eq(field.Battlefield, 0, "and so does a battlefield flag");
        Eq(battle.Battle, e.State.BattleNumber, "the battle it was set in");
        Eq(e.LiveRuleFlagCount(), 6, "all six are live now");
        Eq(new[] { turn.Id, battle.Id, player.Id, field.Id, game.Id, tied.Id }.Distinct().Count(), 6, "each has its own id");

        // The battle ends: the battle-scope flag is over, the rest stand.
        var afterBattle = Edit(e, st => st.CurrentBattleProvince = null);
        Eq(afterBattle.LiveRuleFlagCount(), 5, "the battle flag ended with its battle");
        Check(afterBattle.State.RuleFlags!.Count == 6, "(it is dropped when the next flag is set)");
        // A later battle is not the one it was set in.
        var nextBattle = Edit(e, st => st.BattleNumber++);
        Eq(nextBattle.LiveRuleFlagCount(), 5, "another battle's flag does not carry");
        // The turn ends: the turn flag and the battlefield flag (no Until: the turn) are over; the player flag lasts until his next turn begins.
        var tomorrow = Edit(e, st => { st.TurnNumber++; st.CurrentBattleProvince = null; });
        Eq(tomorrow.LiveRuleFlagCount(), 3, "next turn: the player flag (until a turn begins), the game flag and the card's");
        Check(tomorrow.State.RuleFlags!.Where(f => tomorrow.RuleFlagIsLive(f.Id)).Select(f => f.Source).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["Card", "Game", "Player"]), "the right three");
        // His next turn begins: the player flag ends.
        var begun = Edit(tomorrow, st => st.Players[0].TurnsTaken += 1);
        Check(!begun.State.RuleFlags!.Any(f => f.Source == "Player" && begun.RuleFlagIsLive(f.Id)), "the Until passed: the player flag is over");
        // The card leaves play: its flag ends; the game flag does not.
        var left = Edit(begun, st => st.Cards[host.Id].Zone = Zone.DynastyDiscard);
        Check(!left.State.RuleFlags!.Any(f => f.Source == "Card" && left.RuleFlagIsLive(f.Id)) && left.State.RuleFlags!.Any(f => f.Source == "Game" && left.RuleFlagIsLive(f.Id)), "tied to a card, it ends with the card; the game's goes on");
        // Setting the next flag drops what has expired; removing the last empties the store.
        left.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 0, "Fresh");
        Eq(left.State.RuleFlags!.Count, 2, "expired flags are dropped as the next is set (the game's and the fresh one)");
        foreach (var id in left.State.RuleFlags!.Select(f => f.Id).ToArray()) Check(left.RemoveRuleFlag(id), "removed");
        Check(left.State.RuleFlags is null, "an emptied store is omitted again");
        // The turn-begin callback prunes too.
        var prune = MatchEngine.CreateEconomyScenario(); var pruneStart = prune.State.TurnNumber;
        prune.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Turn, 0, "Turn");
        Check(prune.State.RuleFlags is { Count: 1 }, "set");
        Passing(prune, x => x.State.TurnNumber > pruneStart);
        Check(prune.State.RuleFlags is null, "the turn that began since dropped the expired flag from the saved game");
        // Player scope needs a player.
        var json = Json(e);
        Refused(() => e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Player, null, "Test"), "a player flag with no player was set");
        Eq(Json(e), json, "refused without a trace");
    }

    // ---- 11. the saved game -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RoundTrip()
    {
        var (e, _, anchor) = Combat();
        Check(!Json(e).Contains("RuleFlag"), "unused: omitted");
        e.SetRuleFlag(RuleFlagKinds.NoEngageForAttacker, RuleFlagScope.Battlefield, 1, "Lantern Oil", battlefield: 0, whileInPlay: anchor.Id);
        e.SetRuleFlag(RuleFlagKinds.NegateBattleActionWithoutUnits, RuleFlagScope.Battle, 0, "Temple Grounds");
        e.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "An Empire of Madness", amount: 20, until: TurnUntil.GameEnds());
        e.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Player, 0, "The False Path", until: e.UntilOwnersSecondTurnBegins(0), arg: "x");
        var json = Json(e);
        var restored = JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(restored.RuleFlags!.Count, 4, "four flags");
        Check(restored.RuleFlags[0] is { Kind: RuleFlagKinds.NoEngageForAttacker, Scope: RuleFlagScope.Battlefield, PlayerId: 1, Battlefield: 0 } && restored.RuleFlags[0].WhileInPlay is not null, "battlefield flag fields");
        Check(restored.RuleFlags[1] is { Kind: RuleFlagKinds.NegateBattleActionWithoutUnits, Scope: RuleFlagScope.Battle, Battle: not null }, "battle flag fields");
        Check(restored.RuleFlags[2] is { Kind: RuleFlagKinds.MadnessLoss, Amount: 20 } && restored.RuleFlags[2].Until!.Kind == TurnUntil.GameEnd, "madness flag fields");
        Check(restored.RuleFlags[3] is { Scope: RuleFlagScope.Player, Arg: "x" } && restored.RuleFlags[3].Until!.Owner == 0, "player flag fields");
        Eq(restored.RuleFlagSequence, 4, "the id sequence");
        var resumed = MatchEngine.FromTestState(restored);
        Eq(JsonSerializer.Serialize(resumed.State), json, "an engine rebuilt from it saves identically");
        Check(resumed.EngageActionBarred(0) == e.EngageActionBarred(0), "and answers as the original does");
        Eq(resumed.SetRuleFlag(RuleFlagKinds.NoEnlightenmentVictory, RuleFlagScope.Game, 0, "More")!.Id, 5, "ids continue without a clash");

        // The mid-command marker is in the saved game only after an Action Phase was ended, and does not outlive its command.
        var s = Batch336Support.Table(Phase.Action, 1);
        var card = Add(s, "training-farm", 1, Zone.Home); card.GrantedAbilities.Add(new GrantedAbility(EndPhase, "test-fixture", GrantDuration.Permanent));
        var phase = MatchEngine.FromTestState(s);
        Check(!Json(phase).Contains("ActionPhaseEndedAtVersion"), "not before");
        Accept(phase, Batch336Support.Enabled(phase, 1, EndPhase, card.Id).Command!);
        Check(JsonSerializer.Serialize(MatchEngine.FromTestState(phase.State).State).Contains("ActionPhaseEndedAtVersion"), "kept in the saved game");
        Check(phase.State.ActionPhaseEndedAtVersion < phase.Version, "but older than the state's version once the command is over");

        // Unknown kinds are a programming error, refused without a trace.
        var before = Json(e);
        try { e.SetRuleFlag("not-a-kind", RuleFlagScope.Game, 0, "Test"); throw new Exception("an unknown kind was set"); }
        catch (ArgumentException) { }
        Eq(Json(e), before, "unknown kind: nothing changed");
        Eq(RuleFlagKinds.All.Distinct().Count(), 8, "eight kinds, each named once");
    }
}
