using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #738, wave batch 7 (src/L5R.Core/Wave738.cs): the turn-boundary, rule-flag and Events-Phase cards, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the
// Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave738Tests
{
    private const string Tatsuki = Wave738Abilities.Tatsuki, Takazaki = Wave738Abilities.Takazaki, Dagger = Wave738Abilities.Dagger, LanternOil = Wave738Abilities.LanternOil, Drain = Wave738Abilities.DrainOfEffort,
        Marrow = Wave738Abilities.SuckTheMarrow, Temple = Wave738Abilities.TempleGrounds, Farmlands = Wave738Abilities.Farmlands, DangerousGame = Wave738Abilities.DangerousGame, Empire = Wave738Abilities.EmpireOfMadness,
        JourneysEnd = Wave738Abilities.JourneysEnd, OpenRecruitment = Wave738Abilities.OpenRecruitment, RejectedMediation = Wave738Abilities.RejectedMediation, Jade = Wave738Abilities.JadeChampionship,
        MadnessSpreads = Wave738Abilities.MadnessSpreads, Chagatai = Wave738Abilities.Chagatai;
    private const string Revelations = "oracle-l5r-10262", OnyxChampionship = "oracle-l5r-8242";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Marketplace = "training-marketplace", EngageHonor = "training-engage-honor-strategy", EngageAbility = "training-engage-gain-honor";
    private const string Incite = "incite-phantoms-bow", PoliticalStrategy = "training-political-strategy", BowAny = "training-bow-any", Terrain = "training-terrain", LimitedDraw = "training-limited-draw";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static void Madness(CardInstance card) => card.Tokens.Add(new CardToken("w738-madness-" + card.Id, "Madness", "test"));
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.Version);
    private static GameCommand UseCommand(MatchEngine e, int player, string ability, string card, string? target = null) => new("useAbility", player, e.Version, CardId: card, AbilityId: ability, TargetId: target);

    public static void Run(TestRunner runner) => runner.Run("wave-738", [
        ("Published as printed: sixteen cards in six profiles with their stats, abilities and effects; Revelations and The Onyx Championship are not published", PublishedShape),
        ("Doji Tatsuki - exp: straightens after another player's turn begins, and only then", TatsukiCard),
        ("Abundant Farmlands: straightens after each player's turn begins and produces 1 Gold", FarmlandsCard),
        ("Dagger of Crystal Wind: a +1F Obsession token each of its controller's turns; destroys the Personality once he has more Obsession tokens than printed Chi", DaggerCard),
        ("Shosuro Takazaki: Political Engage, so other players gain no Honor for destroying her controller's cards in that battle's resolution", TakazakiCard),
        ("Lantern Oil: the Attacker's Engage actions are barred at the battlefield of the Item's host, the Defender's are not, nor are other battlefields", LanternOilCard),
        ("Temple Grounds: Battle puts it into play (replacing a Terrain) and negates Battle actions by a player without units there unless its controller has a Cavalry unit; Engage puts it into play too", TempleGroundsCard),
        ("Drain of Effort: until the phase ends, each other action's player discards a card, at most twice; its second Open draws", DrainCard),
        ("Suck the Marrow: bow and destroy the performing Shugenja, Poison token, lose 3 Honor, and another token as each Events Phase begins", MarrowCard),
        ("Chagatai's Wrath: Limited, discard it: the Action Phase ends", ChagataiCard),
        ("The Madness Spreads: a Limited action on a Strategy may be taken as an Open action once a turn until the second turn from now", MadnessSpreadsCard),
        ("A Dangerous Game: discarding the hand draws four, declining keeps it, and the Event is then discarded", DangerousGameCard),
        ("An Empire of Madness: each player may make his Personality with a Madness token commit Seppuku, and a player ending his turn with 20 Madness tokens loses", EmpireCard),
        ("Journey's End Siege: Defenders may not play Terrains this turn; the first Province destroyed in battle gains a Province unless the destroyer has more than the Defender", JourneysEndCard),
        ("Open Recruitment: an unaligned Human Personality turning face up in your province permanently takes your Clan alignment, until the second turn from now", OpenRecruitmentCard),
        ("Rejected Mediation: the Imperial Favor is discarded and the first Political action each turn is negated until the second turn from now", RejectedMediationCard),
        ("The Jade Championship: the chosen Shugenja makes Shadowlands cards at his location -1F and straightens after one of his Spells' actions", JadeCard),
    ]);

    // ---- fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Table(Phase phase, int actor)
    {
        var s = Batch336Support.Table(phase, actor);
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        return s;
    }

    // Passes (and ends phases, declines, discards to the hand limit) for whoever must act until `stop` holds.
    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 500 && !stop(e); i++)
        {
            ActionOption? pick = null; var mover = 0;
            foreach (var candidate in new[] { e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.State.PriorityPlayerId }.Distinct())
            {
                var options = e.QueryActions(candidate).Where(a => a.Enabled && a.Command is not null).ToArray();
                pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type == "forcedDiscard")
                    ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase");
                if (pick is not null) { mover = candidate; break; }
            }
            if (pick is null)
                throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}, choice player {e.State.PendingChoice?.PlayerId}, priority {e.State.PriorityPlayerId}): "
                    + string.Join(" | ", new[] { 0, 1 }.Select(p => p + ": " + string.Join(", ", e.QueryActions(p).Select(a => $"{a.Type}{(a.Enabled ? "" : "(off)")}")))));
            var command = pick.Command!;
            if (command.Type == "discardHand") { var hand = e.State.Players[mover].Hand; command = command with { CardIds = hand.Take(hand.Count - 8).ToArray() }; }
            var result = e.Execute(command);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber}, pending {e.State.PendingChoice?.Type})");
        return e;
    }

    // The Action Phase of the given turn number.
    private static MatchEngine ToAction(MatchEngine e, int turn) => Passing(e, x => x.State.TurnNumber == turn && x.State.Phase == Phase.Action && x.State.PendingChoice is null);

    // Player 0 attacks (BattleSelection), both Strongholds published; the Attacker's and the Defender's armies at battlefield 0.
    private static MatchState Selection()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = [0];
        return s;
    }

    // The Events Phase's Event: placed face up in the province, resolved through the engine's own entry point.
    private static (MatchEngine Engine, CardInstance Event) EventRig(string eventId, int owner, Action<MatchState>? more = null, Phase phase = Phase.Events)
    {
        var s = Table(phase, owner);
        var ev = Add(s, eventId, owner, Zone.Province, province: 1);
        ev.FaceUp = true; s.Players[owner].Provinces[1].CardId = ev.Id;
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), ev);
    }

    private static object? Resolve(MatchEngine e, CardInstance ev)
    {
        var real = (PlayerState)Call(e, "Player", ev.OwnerId)!;
        return Call(e, "ResolveWave738Event", real, real.Provinces[1], Call(e, "Card", ev.Id));
    }

    private static void PublishStrongholds(MatchState s)
    {
        foreach (var p in s.Players) { var stronghold = s.Cards[p.StrongholdId]; stronghold.Printed = CardCatalog.Get(stronghold.DefinitionId) with { ScenarioOnly = false }; }
    }

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Tatsuki] = (CardType.Personality, [], "w738-tatsuki"),
            [Takazaki] = (CardType.Personality, ["w738-takazaki-engage"], "none"),
            [Dagger] = (CardType.Item, [], "w738-dagger"),
            [LanternOil] = (CardType.Item, [], "w738-lantern-oil"),
            [Drain] = (CardType.Strategy, ["w738-drain-watch", "w738-drain-draw"], "none"),
            [Marrow] = (CardType.Strategy, ["w738-marrow-curse"], "none"),
            [Temple] = (CardType.Strategy, ["w738-temple-battle", "w738-temple-engage"], "none"),
            [Farmlands] = (CardType.Holding, [], "w738-farmlands"),
            [DangerousGame] = (CardType.Event, [], "w738-dangerous-game"),
            [Empire] = (CardType.Event, [], "w738-empire-of-madness"),
            [JourneysEnd] = (CardType.Event, [], "w738-journeys-end"),
            [OpenRecruitment] = (CardType.Event, [], "w738-open-recruitment"),
            [RejectedMediation] = (CardType.Event, [], "w738-rejected-mediation"),
            [Jade] = (CardType.Event, [], "w738-jade-championship"),
            [MadnessSpreads] = (CardType.Event, [], "w738-madness-spreads"),
            [Chagatai] = (CardType.Celestial, ["w738-chagatai-end-phase"], "none")
        };
        Eq(expected.Count, 16, "sixteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Eq(CardCatalog.Get(Farmlands).GoldProduction, 1, "Abundant Farmlands produces its printed 1 Gold");
        foreach (var id in new[] { DangerousGame, Empire, OpenRecruitment, RejectedMediation })
            Check(!CardCatalog.Get(id).Text.Contains("After you reveal cards", StringComparison.Ordinal), $"{CardCatalog.Get(id).Name} prints the scan's text, without the catalogue's reveal clause");
        Check(CardCatalog.Get(Marrow).Text.Contains("after each Events Phase begins", StringComparison.Ordinal), "Suck the Marrow prints the scan's 'Events Phase'");
        foreach (var id in new[] { Revelations, OnyxChampionship }) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is deferred, not published");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w738-", StringComparison.Ordinal)), 8, "eight w738 abilities");
        foreach (var id in new[] { DangerousGame, Empire, OpenRecruitment, RejectedMediation }) Check(!ProvinceRevealEvents.IsFlagged(CardCatalog.Get(id).Effect), "an ordinary Event: the P5 flag is not used");
    }

    private static void TatsukiCard()
    {
        var s = Table(Phase.Action, 0);
        var tatsuki = Add(s, Tatsuki, 0, Zone.Home, bowed: true);
        var other = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        ToAction(e, start + 1); // player 1's turn begins
        Check(!e.State.Cards[tatsuki.Id].Bowed, "after another player's turn began she straightened");
        Check(e.State.Cards[other.Id].Bowed, "and nothing else of hers did: only her own Straighten Phase straightens the rest");
        // Her own turn: nothing extra (the ordinary Straighten Phase straightens her, and the trait does not fire for it).
        var t = Table(Phase.Action, 1);
        var mine = Add(t, Tatsuki, 1, Zone.Home, bowed: true);
        var f = MatchEngine.FromTestState(t);
        ToAction(f, f.State.TurnNumber + 1); // player 0's turn begins: another player's turn for player 1's Tatsuki
        Check(!f.State.Cards[mine.Id].Bowed, "her controller's opponent's turn straightens her whoever controls her");
    }

    private static void FarmlandsCard()
    {
        var s = Table(Phase.Action, 0);
        var farm = Add(s, Farmlands, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        ToAction(e, e.State.TurnNumber + 1); // player 1's turn
        Check(!e.State.Cards[farm.Id].Bowed, "it straightened after the other player's turn began");
        Eq(CardCatalog.Get(Farmlands).GoldProduction, 1, "and it still produces its 1 Gold when bowed");
        // Bowed for its own production, it straightens on the owner's turn too.
        e.State.Cards[farm.Id].Bowed = false;
        var f = Batch336Support.Edit(e, st => { st.Cards[farm.Id].Bowed = true; });
        ToAction(f, f.State.TurnNumber + 1);
        Check(!f.State.Cards[farm.Id].Bowed, "and after its owner's own turn began");
    }

    private static void DaggerCard()
    {
        var s = Table(Phase.Action, 0);
        var host = Fx(s, CraneSamurai, "w738-host", 0, Zone.Home, d => d with { Chi = 2, Force = 2 });
        var dagger = Add(s, Dagger, 0, Zone.Attached, parent: host.Id);
        var enemy = Fx(s, LionSamurai, "w738-enemy-host", 1, Zone.Home, d => d with { Chi = 2, Force = 2 });
        var enemyDagger = Add(s, Dagger, 1, Zone.Attached, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var start = e.State.TurnNumber;
        ToAction(e, start + 1); // player 1's turn: his Dagger acts, hers does not
        Eq(e.State.Cards[host.Id].Tokens.Count(t => t.Type == "Obsession"), 0, "player 0's Dagger did nothing on the other player's turn");
        Eq(e.State.Cards[enemy.Id].Tokens.Count(t => t.Type == "Obsession"), 1, "player 1's Dagger gave its Personality a token on his turn");
        ToAction(e, start + 2);
        Eq(e.State.Cards[host.Id].Tokens.Count(t => t.Type == "Obsession"), 1, "on its controller's turn it gives its Personality a token");
        var token = e.State.Cards[host.Id].Tokens.Single(t => t.Type == "Obsession");
        Check(token.Stat == "Force" && token.Amount == 1, "a +1F token");
        ToAction(e, start + 4);
        Eq(e.State.Cards[host.Id].Tokens.Count(t => t.Type == "Obsession"), 2, "two tokens: equal to his printed Chi, so he lives");
        Check(e.State.Cards[host.Id].Zone != Zone.Dead && InPlayZone(e.State.Cards[host.Id]), "he is still in play");
        ToAction(e, start + 6);
        Check(!InPlayZone(e.State.Cards[host.Id]), "with three tokens, more than his printed Chi 2, he was destroyed");
        Check(e.State.Events.Any(x => x.Type == "obsessionDestroyed"), "the destruction is logged");
        Check(!InPlayZone(e.State.Cards[enemy.Id]), "the other player's Dagger ran on his own turns and destroyed his Personality too");
        _ = dagger; _ = enemyDagger;
    }

    private static bool InPlayZone(CardInstance card) => card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached;

    private static void TakazakiCard()
    {
        (MatchEngine E, CardInstance Takazaki) Position()
        {
            var s = Selection();
            Add(s, LionSamurai, 0, Zone.Battlefield, province: 0); Add(s, LionSamurai, 0, Zone.Battlefield, province: 0);
            var takazaki = Add(s, Takazaki, 1, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(s), takazaki);
        }
        // Outside an Engage window the Reaction is not available.
        var (idle, idleTakazaki) = Position();
        Reject(idle, UseCommand(idle, 1, Wave738Abilities.TakazakiEngage, idleTakazaki.Id));
        // With her Engage action the winner gains nothing for destroying her controller's units; without it he gains Honor.
        int Gain(bool use)
        {
            var (e, takazaki) = Position();
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
            var honor = Honor(e, 0);
            if (use)
            {
                Accept(e, Decline(e)); // the Attacker has the first opportunity and passes it
                Check(Batch336Support.CanUse(e, 1, Wave738Abilities.TakazakiEngage, takazaki.Id), "the Engage action is offered to her controller");
                Accept(e, Batch336Support.Enabled(e, 1, Wave738Abilities.TakazakiEngage, takazaki.Id).Command!);
                Check(e.State.RuleFlags?.Any(f => f.Kind == RuleFlagKinds.NoDestructionHonorForOthers && f.PlayerId == 1 && f.Scope == RuleFlagScope.Battle) == true, "a battle-scope flag for her controller");
            }
            Passing(e, x => x.State.CurrentBattleProvince is null && x.State.PendingChoice is null || x.State.Phase != Phase.Battle && x.State.Phase != Phase.BattleSelection);
            Check(e.State.Cards[takazaki.Id].Zone != Zone.Battlefield, "her unit was destroyed");
            return Honor(e, 0) - honor;
        }
        var without = Gain(false);
        var with = Gain(true);
        Check(without > 0, $"without the action the winner gains Honor for the destroyed unit ({without})");
        Eq(with, 0, "with it he gains none");
    }

    private static void LanternOilCard()
    {
        MatchEngine Window(bool oil, int hostBattlefield = 0, bool attackerHolds = true)
        {
            var s = Selection();
            Add(s, LionSamurai, 0, Zone.Battlefield, province: 0);
            var defender = Add(s, CraneSamurai, 1, Zone.Battlefield, province: hostBattlefield == 0 ? 0 : 0);
            if (oil) { var item = Add(s, LanternOil, 1, Zone.Attached, parent: defender.Id); if (hostBattlefield != 0) defender.ProvinceIndex = hostBattlefield; _ = item; }
            if (attackerHolds) Add(s, EngageHonor, 0, Zone.Hand);
            Add(s, EngageHonor, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Window(oil: true);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Defender can use the window, so it opens");
        var attackerCard = e.State.Players[0].Hand.Select(id => e.State.Cards[id]).Single();
        var defenderCard = e.State.Players[1].Hand.Select(id => e.State.Cards[id]).Single();
        Check(e.EngageActionBarred(0) && !e.EngageActionBarred(1), "the bar is the Attacker's");
        Check(!Batch336Support.Options(e, 0, EngageAbility, attackerCard.Id).Any(), "the Attacker is not offered the Engage action");
        Reject(e, UseCommand(e, 0, EngageAbility, attackerCard.Id));
        Accept(e, Decline(e));
        Check(Batch336Support.Options(e, 1, EngageAbility, defenderCard.Id).Any(o => o.Enabled), "the Defender is");
        // Without the Item, the Attacker keeps his action.
        var plain = Window(oil: false);
        Accept(plain, new GameCommand("selectBattle", 0, plain.Version, Province: 0));
        Check(!plain.EngageActionBarred(0) && Batch336Support.Options(plain, 0, EngageAbility).Any(o => o.Enabled), "with no Lantern Oil the Attacker's action is on offer");
        // An Item whose host is at another battlefield bars nothing here.
        var s = Selection(); s.UnresolvedBattlefields = [0, 1];
        Add(s, LionSamurai, 0, Zone.Battlefield, province: 0);
        var away = Add(s, CraneSamurai, 1, Zone.Battlefield, province: 1);
        Add(s, LanternOil, 1, Zone.Attached, parent: away.Id);
        Add(s, EngageHonor, 0, Zone.Hand); Add(s, EngageHonor, 1, Zone.Hand);
        var elsewhere = MatchEngine.FromTestState(s);
        Accept(elsewhere, new GameCommand("selectBattle", 0, elsewhere.Version, Province: 0));
        Check(!elsewhere.EngageActionBarred(0), "the Item's battlefield is its host's, not this one");
        // The saved game of a match that never used it carries nothing new.
        Check(!JsonSerializer.Serialize(plain.State).Contains("\"RuleFlags\""), "no flag store is created: the bar is read from the Item");
    }

    private static void TempleGroundsCard()
    {
        (MatchEngine E, CardInstance Temple, CardInstance? OldTerrain, CardInstance Performer, CardInstance Defender) Position(bool cavalry = false, bool oldTerrain = false)
        {
            var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var temple = Add(s, Temple, 0, Zone.Hand);
            var defender = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            if (cavalry) defender.GrantedKeywords.Add(new GrantedKeyword("Cavalry", "test-fixture", "Permanent"));
            var old = oldTerrain ? Add(s, Terrain, 1, Zone.Battlefield, province: 0) : null;
            var performer = Add(s, LionSamurai, 1, Zone.Home);
            performer.GrantedKeywords.Add(new GrantedKeyword("Spirit", "test-fixture", "Permanent"));
            performer.GrantedAbilities.Add(new GrantedAbility(Incite, "test-fixture", GrantDuration.Permanent));
            return (MatchEngine.FromTestState(s), temple, old, performer, defender);
        }
        GameCommand Incited(MatchEngine e, CardInstance performer, CardInstance target) => new("useAbility", 1, e.Version, CardId: performer.Id, AbilityId: Incite, PerformerId: performer.Id, TargetId: target.Id);

        var (e, temple, old, performer, defender) = Position(oldTerrain: true);
        Check(Batch336Support.Options(e, 0, Wave738Abilities.TempleBattle, temple.Id).Any(o => o.Enabled), "Battle: it is offered from the hand");
        Accept(e, UseCommand(e, 0, Wave738Abilities.TempleBattle, temple.Id));
        Eq(e.State.Cards[temple.Id].Zone, Zone.Battlefield, "the Terrain is in play");
        Check(e.State.Cards[old!.Id].Zone != Zone.Battlefield, "the Terrain that was there was destroyed");
        Check(e.State.RuleFlags?.Any(f => f.Kind == RuleFlagKinds.NegateBattleActionWithoutUnits && f.PlayerId == 0 && f.Battlefield == 0 && f.WhileInPlay == temple.Id) == true, "a battlefield flag that lasts while it stays in play");
        Check(!Batch336Support.CanUse(e, 0, Wave738Abilities.TempleBattle, temple.Id), "and once in play it is not played again");
        // A player without units there takes his Battle action: its cost is paid and its effect negated.
        Accept(e, Incited(e, performer, defender));
        Check(e.State.Cards[performer.Id].Bowed, "his cost was paid");
        Check(!e.State.Cards[defender.Id].Bowed, "and the effect was negated");
        Check(e.State.Events.Any(x => x.Type == "actionNegated" && x.Message.Contains("Temple Grounds")), "the negation is logged with its source");
        // A Cavalry unit of its controller's there: nothing is negated.
        var (cav, cavTemple, _, cavPerformer, cavDefender) = Position(cavalry: true);
        Accept(cav, UseCommand(cav, 0, Wave738Abilities.TempleBattle, cavTemple.Id));
        Accept(cav, Incited(cav, cavPerformer, cavDefender));
        Check(cav.State.Cards[cavDefender.Id].Bowed, "with a Cavalry unit of its controller's there the action resolves");
        // The flag ends with the Terrain.
        var gone = Batch336Support.Edit(e, st => { });
        Call(gone, "DiscardTerrain", Call(gone, "Card", temple.Id));
        Check(gone.LiveRuleFlagCount() == 0, "destroyed, it negates nothing");
        // The Terrain must come from the hand and a battle must be open.
        var s = Batch336Support.Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var idle = Add(s, Temple, 0, Zone.Hand);
        var outside = MatchEngine.FromTestState(s);
        Check(!Batch336Support.CanUse(outside, 0, Wave738Abilities.TempleBattle, idle.Id), "not outside a battle");
        // Engage: in the Engage window the Reaction puts it into play too.
        var t = Selection();
        Add(t, LionSamurai, 0, Zone.Battlefield, province: 0); Add(t, CraneSamurai, 1, Zone.Battlefield, province: 0);
        var engageTemple = Add(t, Temple, 1, Zone.Hand);
        var engage = MatchEngine.FromTestState(t);
        Accept(engage, new GameCommand("selectBattle", 0, engage.Version, Province: 0));
        Eq(engage.State.PendingChoice?.Type, "engageReaction", "the Engage window opens for the Defender's Temple Grounds");
        Accept(engage, Decline(engage)); // the Attacker has the first opportunity and passes it
        Accept(engage, Batch336Support.Enabled(engage, 1, Wave738Abilities.TempleEngage, engageTemple.Id).Command!);
        Eq(engage.State.Cards[engageTemple.Id].Zone, Zone.Battlefield, "Engage: it is in play");
        Check(engage.State.RuleFlags?.Any(f => f.Kind == RuleFlagKinds.NegateBattleActionWithoutUnits) == true, "with its flag");
    }

    private static void DrainCard()
    {
        var s = Table(Phase.Action, 0);
        var drain = Add(s, Drain, 0, Zone.Hand);
        var draws = new[] { Add(s, Drain, 0, Zone.Hand), Add(s, Drain, 0, Zone.Hand), Add(s, Drain, 0, Zone.Hand) };
        for (var i = 0; i < 6; i++) Add(s, Marketplace, 0, Zone.Hand);
        for (var i = 0; i < 24; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Enabled(e, 0, Wave738Abilities.DrainWatch, drain.Id).Command!);
        Check(e.State.Wave738Drains is [{ Discards: 0, Turn: var turn }] && turn == e.State.TurnNumber, "the effect is recorded for this phase");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "forcedDiscard", "its own action does not make its player discard");
        // Another action resolves: its player discards a card.
        var again = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.PendingChoice = null; });
        Accept(again, Batch336Support.Enabled(again, 0, Wave738Abilities.DrainDraw, draws[0].Id).Command!);
        Passing(again, x => x.State.PendingChoice?.Type == "forcedDiscard" || x.State.Wave738Drains![0].Discards >= 1);
        Eq(again.State.Wave738Drains![0].Discards, 1, "the first discard is counted");
        Check(again.State.PendingForcedDiscards.Count == 1 || again.State.PendingChoice?.Type == "forcedDiscard", "and owed by the action's player");
        // Two discards at most: the third action costs nothing.
        var more = Batch336Support.Edit(again, st => { st.PendingChoice = null; st.PendingForcedDiscards.Clear(); st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        Accept(more, Batch336Support.Enabled(more, 0, Wave738Abilities.DrainDraw, draws[1].Id).Command!);
        Eq(more.State.Wave738Drains![0].Discards, 2, "the second discard is counted");
        var last = Batch336Support.Edit(more, st => { st.PendingChoice = null; st.PendingForcedDiscards.Clear(); st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        Accept(last, Batch336Support.Enabled(last, 0, Wave738Abilities.DrainDraw, draws[2].Id).Command!);
        Eq(last.State.Wave738Drains![0].Discards, 2, "after two discards from it the effect is over");
        Check(last.State.PendingForcedDiscards.Count == 0 && last.State.PendingChoice?.Type != "forcedDiscard", "so the third action owes nothing");
        // It ends with the phase: a record from another phase owes nothing and is dropped as the next one is made.
        var phase = Batch336Support.Edit(e, st => { st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; });
        Check(!(bool)Call(phase, "Wave738DrainLive", phase.State.Wave738Drains![0])!, "it is not live in another phase of the turn");
        // Its second Open draws a card.
        var d = Table(Phase.Action, 0);
        var drawer = Add(d, Drain, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(d, Marketplace, 0, Zone.Home);
        var draw = MatchEngine.FromTestState(d);
        var before = draw.State.Players[0].Hand.Count;
        Accept(draw, Batch336Support.Enabled(draw, 0, Wave738Abilities.DrainDraw, drawer.Id).Command!);
        Eq(draw.State.Players[0].Hand.Count, before, "the Strategy left the hand and one card was drawn");
    }

    private static void MarrowCard()
    {
        var s = Table(Phase.Action, 0);
        var strategy = Add(s, Marrow, 0, Zone.Hand);
        var shugenja = Fx(s, CraneSamurai, "w738-shugenja", 0, Zone.Home, d => d with { Keywords = ["Shugenja"] });
        var samurai = Fx(s, CraneSamurai, "w738-not-shugenja", 0, Zone.Home);
        var victim = Fx(s, LionSamurai, "w738-victim", 1, Zone.Home, d => d with { Force = 4, Chi = 4 });
        var e = MatchEngine.FromTestState(s);
        Check(Batch336Support.CanUse(e, 0, Wave738Abilities.MarrowCurse, strategy.Id), "it is offered from the hand");
        NoChange(e, Batch336Support.Use(e, 0, Wave738Abilities.MarrowCurse, strategy.Id, victim.Id, performerId: samurai.Id), "the performer is not a Shugenja");
        var honor = Honor(e, 0);
        Accept(e, Batch336Support.Use(e, 0, Wave738Abilities.MarrowCurse, strategy.Id, victim.Id, performerId: shugenja.Id));
        Check(!InPlayZone(e.State.Cards[shugenja.Id]), "the performing Shugenja was destroyed");
        Check(e.State.Cards[victim.Id].Tokens.Count(t => t.Type == "Poison") == 1, "the target has a Poison token");
        Eq(Honor(e, 0), honor - 3, "and its player lost 3 Honor");
        Check(e.State.TurnScopedRules.Any(r => r.Kind == Wave738Abilities.MarrowRule && r.CardId == victim.Id && r.Until.Owner is null), "a rule that lasts until the game ends names the target");
        // As each Events Phase begins -- whoever's -- another token.
        var start = e.State.TurnNumber;
        ToAction(e, start + 1);
        Eq(e.State.Cards[victim.Id].Tokens.Count(t => t.Type == "Poison"), 2, "a token as the other player's Events Phase began");
        ToAction(e, start + 2);
        Eq(e.State.Cards[victim.Id].Tokens.Count(t => t.Type == "Poison"), 3, "and as the next one began");
        // The Force and Chi the tokens take away.
        Eq((int)Call(e, "CardForce", e.State.Cards[victim.Id])!, 1, "-1F each: 4 down to 1");
        // A target that has left play is simply not poisoned again.
        var gone = Batch336Support.Edit(e, st => { st.Cards[victim.Id].Zone = Zone.Dead; });
        var tokens = gone.State.Cards[victim.Id].Tokens.Count;
        Call(gone, "Wave738EventsPhaseBegins");
        Eq(gone.State.Cards[victim.Id].Tokens.Count, tokens, "nothing happens to a card that is gone");
    }

    private static void ChagataiCard()
    {
        var s = Table(Phase.Action, 0);
        var wrath = Add(s, Chagatai, 0, Zone.Celestial);
        var e = MatchEngine.FromTestState(s);
        Check(Batch336Support.CanUse(e, 0, Wave738Abilities.ChagataiEnd, wrath.Id), "Limited: offered in the Action Phase");
        Accept(e, UseCommand(e, 0, Wave738Abilities.ChagataiEnd, wrath.Id));
        Check(e.State.Phase != Phase.Action, "the Action Phase ended");
        Check(e.State.Cards[wrath.Id].Zone != Zone.Celestial, "and the Celestial was discarded");
        Check(e.State.Events.Any(x => x.Type == "actionPhaseEnded"), "the end of the phase is logged");
        // Not in another phase.
        var t = Table(Phase.Battle, 0);
        var other = Add(t, Chagatai, 0, Zone.Celestial);
        var battle = MatchEngine.FromTestState(t);
        Check(!Batch336Support.CanUse(battle, 0, Wave738Abilities.ChagataiEnd, other.Id), "not offered in the Battle Phase");
    }

    private static void MadnessSpreadsCard()
    {
        var (e, ev) = EventRig(MadnessSpreads, 0, phase: Phase.Action);
        Eq(Resolve(e, ev), false, "the Event resolves and the reveal carries on");
        Check(e.State.TurnScopedRules.Any(r => r.Kind == TurnRuleKinds.LimitedAsOpen && r.PlayerId == 0 && r.Arg == "Strategy"), "P4's Limited-as-Open rule for this player, narrowed to Strategies");
        Check(e.State.Cards[ev.Id].Zone != Zone.EventInPlay && e.State.Cards[ev.Id].Zone != Zone.Province, "the Event is spent, the rule stands");
        // The rule ends as the owner's second turn from now begins.
        var rule = e.State.TurnScopedRules.Single(r => r.Kind == TurnRuleKinds.LimitedAsOpen);
        Check(!e.UntilPassed(rule.Until), "live now");
        ToAction(e, e.State.TurnNumber + 1);
        Check(!e.UntilPassed(rule.Until), "live through the other player's turn");
        ToAction(e, e.State.TurnNumber + 1);
        Check(!e.UntilPassed(rule.Until), "and his next one");
        ToAction(e, e.State.TurnNumber + 2);
        Check(e.UntilPassed(rule.Until), "ended when his second turn from now began");
    }

    private static void DangerousGameCard()
    {
        (MatchEngine E, CardInstance Event) Rig()
        {
            var (e, ev) = EventRig(DangerousGame, 0, st => { for (var i = 0; i < 3; i++) Add(st, Marketplace, 0, Zone.Hand); });
            Eq(Resolve(e, ev), true, "the Event opens its decision");
            Eq(e.State.PendingEventChoice?.Kind, Wave738Abilities.DangerousGameChoice, "a yes/no decision for its owner");
            return (e, ev);
        }
        var (yes, yesEvent) = Rig();
        var actions = yes.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Eq(actions.Length, 2, "two answers are offered");
        NoChange(yes, new GameCommand("eventChoice", 1, yes.Version, CardId: yesEvent.Id, Choice: "yes"), "the other player may not answer");
        NoChange(yes, new GameCommand("eventChoice", 0, yes.Version, CardId: yesEvent.Id, Choice: "maybe"), "not yes or no");
        var hand = yes.State.Players[0].Hand.ToArray();
        var fate = yes.State.Players[0].FateDeck.Count;
        Accept(yes, actions.Single(a => a.Command!.Choice == "yes").Command!);
        Check(hand.All(id => yes.State.Cards[id].Zone != Zone.Hand), "the hand was discarded");
        Eq(yes.State.Players[0].Hand.Count, 4, "and four cards were drawn");
        Eq(yes.State.Players[0].FateDeck.Count, fate - 4, "from the Fate deck");
        Eq(yes.State.Cards[yesEvent.Id].Zone, Zone.DynastyDiscard, "the Event is discarded");
        Check(yes.State.PendingChoice is null || yes.State.PendingChoice.Type != "eventChoice", "the decision is over");
        // Declining keeps the hand.
        var (no, noEvent) = Rig();
        var kept = no.State.Players[0].Hand.ToArray();
        Accept(no, no.QueryActions(0).Single(a => a.Type == "eventChoice" && a.Command!.Choice == "no").Command!);
        Check(no.State.Players[0].Hand.SequenceEqual(kept), "declined: the hand is as it was");
        Eq(no.State.Cards[noEvent.Id].Zone, Zone.DynastyDiscard, "and the Event is still discarded");
        // An empty hand may still discard it ("even if it is empty") and draws four.
        var (empty, emptyEvent) = Rig();
        var emptied = Batch336Support.Edit(empty, st => { st.Players[0].Hand.Clear(); });
        Accept(emptied, emptied.QueryActions(0).Single(a => a.Type == "eventChoice" && a.Command!.Choice == "yes").Command!);
        Eq(emptied.State.Players[0].Hand.Count, 4, "from an empty hand, four cards");
        _ = emptyEvent;
    }

    private static void EmpireCard()
    {
        CardInstance mad = null!, sane = null!, foe = null!;
        var (e, ev) = EventRig(Empire, 0, st =>
        {
            PublishStrongholds(st);
            mad = Add(st, CraneSamurai, 0, Zone.Home); Madness(mad);
            sane = Add(st, CraneSamurai, 0, Zone.Home);
            foe = Add(st, LionSamurai, 1, Zone.Home); Madness(foe);
        }, Phase.Action);
        Eq(Resolve(e, ev), false, "the Event resolves and is discarded");
        Check(e.PlayerAbilityGranted(0, Wave738Abilities.EmpireSeppuku) && e.PlayerAbilityGranted(1, Wave738Abilities.EmpireSeppuku), "each player has the ability");
        Check(e.State.RuleFlags?.Any(f => f.Kind == RuleFlagKinds.MadnessLoss && f.Amount == 20 && f.Scope == RuleFlagScope.Game && f.PlayerId is null) == true, "a game-long madness-loss flag at 20 for each player");
        // The ability: only his own Personality with a Madness token, an Open action of the player.
        var a = Batch336Support.Options(e, 0, Wave738Abilities.EmpireSeppuku).Single(o => o.Enabled);
        Check(a.TargetIds!.Contains(mad.Id) && !a.TargetIds.Contains(sane.Id) && !a.TargetIds.Contains(foe.Id), "only his own Personality with a Madness token is a target");
        NoChange(e, new GameCommand("useAbility", 0, e.Version, AbilityId: Wave738Abilities.EmpireSeppuku, TargetId: sane.Id), "no Madness token");
        NoChange(e, new GameCommand("useAbility", 0, e.Version, AbilityId: Wave738Abilities.EmpireSeppuku, TargetId: foe.Id), "the other player's Personality");
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: Wave738Abilities.EmpireSeppuku, TargetId: mad.Id));
        Check(!InPlayZone(e.State.Cards[mad.Id]), "he committed seppuku");
        Check(e.State.Events.Any(x => x.Type == "seppuku"), "logged as seppuku");
        // The loss: the player whose turn ends with 20 Madness tokens loses.
        var t = Table(Phase.Action, 0);
        PublishStrongholds(t);
        var holder = Fx(t, CraneSamurai, "w738-holder", 0, Zone.Home);
        for (var i = 0; i < 20; i++) holder.Tokens.Add(new CardToken("w738-madness-" + i, "Madness", "test"));
        var lose = MatchEngine.FromTestState(t);
        lose.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "An Empire of Madness", amount: 20);
        Call(lose, "EndTurn");
        Eq(lose.State.WinnerId, 1, "the player who ended his turn with 20 Madness tokens loses");
        var spared = Table(Phase.Action, 0);
        PublishStrongholds(spared);
        var nineteen = Fx(spared, CraneSamurai, "w738-nineteen", 0, Zone.Home);
        for (var i = 0; i < 19; i++) nineteen.Tokens.Add(new CardToken("w738-madness-" + i, "Madness", "test"));
        var ok = MatchEngine.FromTestState(spared);
        ok.SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, "An Empire of Madness", amount: 20);
        Call(ok, "EndTurn");
        Check(ok.State.WinnerId is null, "19 is not enough");
    }

    private static void JourneysEndCard()
    {
        // The turn's flag: the Defender may not play a Terrain; the Attacker may.
        var (flagged, ev) = EventRig(JourneysEnd, 1, st => { Add(st, Terrain, 0, Zone.Hand); Add(st, Terrain, 1, Zone.Hand); });
        Eq(Resolve(flagged, ev), false, "the Event resolves and is discarded");
        Check(flagged.RuleFlagTerrainReason(0) is not null && flagged.RuleFlagTerrainReason(1) is null, "the Defender may not play a Terrain, its owner may");
        // The siege: the first Province destroyed in battle this turn.
        (MatchEngine E, int AttackerProvinces, int DefenderProvinces) Siege(bool armed, int attackerDestroyed, Func<MatchEngine, int>? provinces = null)
        {
            var s = Batch336Support.Table(Phase.Battle, 1);
            Add(s, LionSamurai, 1, Zone.Battlefield, province: 0); Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
            Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
            s.Players[0].Provinces[0].Strength = 1;
            for (var i = 0; i < attackerDestroyed; i++) s.Players[1].Provinces[3 - i].Destroyed = true;
            var e = MatchEngine.FromTestState(s);
            if (armed) e.AddPeriodCount("w738-siege:1", Period.Turn);
            var attacker = e.State.Players[1].Provinces.Count(p => !p.Destroyed);
            Passing(e, x => x.State.CurrentBattleProvince is null && x.State.PendingChoice is null);
            return (e, attacker, e.State.Players[0].Provinces.Count(p => !p.Destroyed));
        }
        var (equal, before, defender) = Siege(armed: true, attackerDestroyed: 1);
        Eq(before, 3, "the Attacker began with three Provinces");
        Eq(defender, 3, "the Defender's Province was destroyed: three left");
        Eq(equal.State.Players[1].Provinces.Count(p => !p.Destroyed), 4, "three is not more than three, so he gained a Province");
        Check(equal.State.Events.Any(x => x.Type == "provinceGained"), "logged");
        var (more, _, _) = Siege(armed: true, attackerDestroyed: 0);
        Eq(more.State.Players[1].Provinces.Count(p => !p.Destroyed), 4, "with four against three he has more, so he gains none");
        var (unarmed, _, _) = Siege(armed: false, attackerDestroyed: 1);
        Eq(unarmed.State.Players[1].Provinces.Count(p => !p.Destroyed), 3, "without the Event this turn nothing is gained");
        // Only the first time this turn.
        var (twice, _, _) = Siege(armed: true, attackerDestroyed: 1);
        Eq(twice.PeriodCountOf("w738-siege-done:1", Period.Turn), 1, "the first time is recorded");
    }

    private static void OpenRecruitmentCard()
    {
        // The Event's rule, then a card turning face up in a province of its owner.
        (MatchEngine E, CardInstance Event, CardInstance Recruit) Rig(string keyword = "", string clan = "Unaligned")
        {
            CardInstance recruit = null!;
            var (e, ev) = EventRig(OpenRecruitment, 0, st =>
            {
                recruit = Fx(st, LionSamurai, "w738-recruit", 0, Zone.Province, d => d with { Clan = clan, Clans = null, Keywords = keyword == "" ? [] : [keyword] }, province: 2);
                recruit.FaceUp = false; st.Players[0].Provinces[2].CardId = recruit.Id;
            });
            return (e, ev, recruit);
        }
        var (e, ev, recruit) = Rig();
        Eq(Resolve(e, ev), false, "the Event resolves and is discarded");
        var rule = e.State.TurnScopedRules.Single(r => r.Kind == Wave738Abilities.OpenRecruitmentRule);
        Check(rule.PlayerId == 0 && !e.UntilPassed(rule.Until), "a rule for its owner that lasts until his second turn from now");
        e.SweepFactsForTest(); // the first look: nothing is a change
        Check(Call(e, "Keyword", e.State.Cards[recruit.Id], "Unaligned") is true, "he is unaligned before");
        var flipped = Batch336Support.Edit(e, st => st.Cards[recruit.Id].FaceUp = true);
        flipped.SweepFactsForTest();
        Check(Call(flipped, "HasClanAffiliation", flipped.State.Cards[recruit.Id], flipped.State.Players[0].Clan) is true, "after he turned face up in his province he permanently has the Clan alignment");
        Check(Call(flipped, "Keyword", flipped.State.Cards[recruit.Id], "Unaligned") is not true, "and is no longer unaligned");
        // A Nonhuman or an already aligned Personality is untouched.
        (MatchEngine, CardInstance) Flip(MatchEngine engine, CardInstance card)
        {
            engine.SweepFactsForTest();
            var up = Batch336Support.Edit(engine, st => st.Cards[card.Id].FaceUp = true);
            up.SweepFactsForTest();
            return (up, up.State.Cards[card.Id]);
        }
        var (nh, nhEvent, nhRecruit) = Rig("Nonhuman");
        Resolve(nh, nhEvent); var (nhAfter, nhCard) = Flip(nh, nhRecruit);
        Check(Call(nhAfter, "Keyword", nhCard, "Unaligned") is true, "a Nonhuman Personality is not given the alignment");
        var (aligned, alignedEvent, alignedRecruit) = Rig(clan: "Lion");
        Resolve(aligned, alignedEvent); var (alignedAfter, alignedCard) = Flip(aligned, alignedRecruit);
        Check(Call(alignedAfter, "HasClanAffiliation", alignedCard, alignedAfter.State.Players[0].Clan) is not true || alignedAfter.State.Players[0].Clan == "Lion", "an aligned Personality keeps his own");
        Check(Call(alignedAfter, "HasClanAffiliation", alignedCard, "Lion") is true, "his Lion alignment is untouched");
        // The other player's province is not "one of your provinces": the Personality turning face up in his is not aligned.
        CardInstance enemyRecruit = null!;
        var theirs = Batch336Support.Edit(e, st => { enemyRecruit = Fx(st, LionSamurai, "w738-theirs", 1, Zone.Province, d => d with { Clan = "Unaligned", Clans = null, Keywords = [] }, province: 2); enemyRecruit.FaceUp = false; st.Players[1].Provinces[2].CardId = enemyRecruit.Id; });
        var (theirsAfter, theirsCard) = Flip(theirs, enemyRecruit);
        Check(Call(theirsAfter, "Keyword", theirsCard, "Unaligned") is true, "one turning up in the other player's province is not aligned");
        // After the rule ended nothing happens.
        var endedRule = Batch336Support.Edit(e, st => { st.TurnScopedRules.Clear(); });
        var (endedAfter, endedCard) = Flip(endedRule, endedRule.State.Cards[recruit.Id]);
        Check(Call(endedAfter, "Keyword", endedCard, "Unaligned") is true, "with no rule nothing happens");
    }

    private static void RejectedMediationCard()
    {
        (MatchEngine E, CardInstance Event, CardInstance Victim, CardInstance[] Strategies) Rig(bool favor)
        {
            CardInstance victim = null!; var strategies = new List<CardInstance>();
            var (e, ev) = EventRig(RejectedMediation, 0, st =>
            {
                victim = Add(st, CraneSamurai, 0, Zone.Home);
                for (var i = 0; i < 3; i++) strategies.Add(Add(st, PoliticalStrategy, 1, Zone.Hand));
                for (var i = 0; i < 6; i++) Add(st, Marketplace, 1, Zone.Home);
                if (favor) st.FavorPlayerId = 1;
            });
            return (e, ev, victim, [.. strategies]);
        }
        var (e, ev, victim, strategies) = Rig(favor: true);
        Eq(e.State.FavorPlayerId, 1, "the other player controls the Imperial Favor");
        Eq(Resolve(e, ev), false, "the Event resolves and is discarded");
        Check(e.State.FavorPlayerId is null, "any player who controls the Imperial Favor discarded it");
        Check(e.State.NegationWatches.Count == 1 && e.State.NegationWatches[0] is { Kind: WatchKind.PoliticalAction, Source: WatchSource.Any, Remaining: -1, Refresh: Period.Turn }, "a player-level watch of any player's Political action, once each turn");
        // The first Political action taken this turn is negated whoever takes it; the next one resolves.
        var played = Batch336Support.Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        Accept(played, Batch336Support.Offer(played, 1, BowAny, strategies[0].Id, victim.Id));
        Settle(played);
        Check(!played.State.Cards[victim.Id].Bowed, "the effects of the first Political action were negated");
        Check(played.State.Cards[strategies[0].Id].Zone != Zone.Hand, "though the action was taken and its card spent");
        var next = Batch336Support.Edit(played, st => { st.PriorityPlayerId = 1; st.ActivePlayerId = 1; st.PendingChoice = null; });
        Accept(next, Batch336Support.Offer(next, 1, BowAny, strategies[1].Id, victim.Id));
        Settle(next);
        Check(next.State.Cards[victim.Id].Bowed, "the second Political action the same turn resolves");
        // The next turn the watch negates the first again, while it lasts.
        var later = Batch336Support.Edit(e, st => { st.TurnNumber += 2; st.Phase = Phase.Action; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        Accept(later, Batch336Support.Offer(later, 1, BowAny, strategies[2].Id, victim.Id));
        Settle(later);
        Check(!later.State.Cards[victim.Id].Bowed, "two turns on it still negates the first Political action of its turn");
        var over = Batch336Support.Edit(e, st => { st.TurnNumber += 4; st.Phase = Phase.Action; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        Accept(over, Batch336Support.Offer(over, 1, BowAny, strategies[2].Id, victim.Id));
        Settle(over);
        Check(over.State.Cards[victim.Id].Bowed, "once the second turn from now has begun it negates nothing");
        // With nobody holding the Favor the Event still sets its watch.
        var (plain, plainEvent, _, _) = Rig(favor: false);
        Resolve(plain, plainEvent);
        Check(plain.State.FavorPlayerId is null && plain.State.NegationWatches.Count == 1, "no Favor to discard, the watch is still set");
    }

    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version)); }

    private static void JadeCard()
    {
        (MatchEngine E, CardInstance Event, CardInstance[] Shugenja) Rig(int shugenja)
        {
            var list = new List<CardInstance>();
            var (e, ev) = EventRig(Jade, 0, st =>
            {
                for (var i = 0; i < shugenja; i++) list.Add(Fx(st, CraneSamurai, "w738-shugenja-" + i, 0, Zone.Home, d => d with { Keywords = ["Shugenja"] }));
                Fx(st, CraneSamurai, "w738-samurai", 0, Zone.Home);
            });
            return (e, ev, [.. list]);
        }
        // One Shugenja: no choice is needed.
        var (one, oneEvent, ones) = Rig(1);
        Eq(Resolve(one, oneEvent), false, "with one Shugenja there is nothing to choose");
        Eq(one.State.Wave738JadeShugenja?.Single(), ones[0].Id, "he has the traits");
        // None: the Event does nothing.
        var (none, noneEvent, _) = Rig(0);
        Eq(Resolve(none, noneEvent), false, "with none it is simply discarded");
        Check(none.State.Wave738JadeShugenja is null, "nobody has the traits");
        // Two: a choice among them.
        var (two, twoEvent, twos) = Rig(2);
        Eq(Resolve(two, twoEvent), true, "with two the owner chooses");
        var options = two.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Eq(options.Length, 2, "one answer for each Shugenja (and the samurai is not one)");
        NoChange(two, new GameCommand("eventChoice", 0, two.Version, CardId: twoEvent.Id, TargetId: two.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && !twos.Any(t => t.Id == c.Id)).Id), "a Personality that is not a Shugenja");
        Accept(two, options.Single(o => o.Command!.TargetId == twos[1].Id).Command!);
        Eq(two.State.Wave738JadeShugenja?.Single(), twos[1].Id, "the chosen one has the traits");

        // Trait one: Shadowlands Personalities and Followers have -1F at his location. A Shadowlands card of either player at his location, none elsewhere, none without Shadowlands.
        var s = Table(Phase.Action, 0);
        var wielder = Fx(s, CraneSamurai, "w738-wielder", 0, Zone.Battlefield, d => d with { Keywords = ["Shugenja"] }, province: 0);
        var here = Fx(s, LionSamurai, "w738-here", 1, Zone.Battlefield, d => d with { Force = 3, Keywords = ["Shadowlands"] }, province: 0);
        var follower = Fx(s, "training-expendable-follower", "w738-follower", 1, Zone.Attached, d => d with { Force = 2, Keywords = ["Shadowlands"] }, parent: here.Id);
        var elsewhere = Fx(s, LionSamurai, "w738-elsewhere", 1, Zone.Battlefield, d => d with { Force = 3, Keywords = ["Shadowlands"] }, province: 1);
        var plain = Fx(s, LionSamurai, "w738-plain", 1, Zone.Battlefield, d => d with { Force = 3 }, province: 0);
        var game = MatchEngine.FromTestState(s);
        var before = new[] { here, elsewhere, plain }.Select(c => (int)Call(game, "CardForce", game.State.Cards[c.Id])!).ToArray();
        var followerBefore = (int)Call(game, "CardForce", game.State.Cards[follower.Id])!;
        game = Batch336Support.Edit(game, st => st.Wave738JadeShugenja = [wielder.Id]);
        var after = new[] { here, elsewhere, plain }.Select(c => (int)Call(game, "CardForce", game.State.Cards[c.Id])!).ToArray();
        Eq(after[0], before[0] - 1, "a Shadowlands Personality at his location has -1F");
        Eq(after[1], before[1], "one elsewhere does not");
        Eq(after[2], before[2], "nor one without Shadowlands");
        Eq((int)Call(game, "CardForce", game.State.Cards[follower.Id])!, followerBefore - 1, "a Shadowlands Follower at his location (its host's) has -1F");
        // Trait two: after the resolution of an action from one of his Spells he straightens.
        var spellTable = Table(Phase.Action, 0);
        var shugenja = Fx(spellTable, CraneSamurai, "w738-caster", 0, Zone.Home, d => d with { Keywords = ["Shugenja"] }, bowed: true);
        var spell = Add(spellTable, "training-spell-with-ability", 0, Zone.Attached, parent: shugenja.Id);
        var cast = Batch336Support.Edit(MatchEngine.FromTestState(spellTable), st => st.Wave738JadeShugenja = [shugenja.Id]);
        var facts = new ResolvedActionFacts { AbilityId = "training-spell-force-boost", PlayerId = 0, SourceId = spell.Id };
        Call(cast, "Wave738AfterAction", facts);
        Check(!cast.State.Cards[shugenja.Id].Bowed, "after an action from his Spell resolved, he straightened");
        var foreign = Batch336Support.Edit(cast, st => { st.Cards[shugenja.Id].Bowed = true; });
        Call(foreign, "Wave738AfterAction", new ResolvedActionFacts { AbilityId = "training-spell-force-boost", PlayerId = 0, SourceId = "no-such-card" });
        Check(foreign.State.Cards[shugenja.Id].Bowed, "an action that is not from his Spell leaves him bowed");
        // A saved game round-trips the list.
        var json = JsonSerializer.Serialize(game.State);
        Check(JsonSerializer.Deserialize<MatchState>(json)!.Wave738JadeShugenja!.SequenceEqual([wielder.Id]), "the marked Shugenja are saved");
        Check(!JsonSerializer.Serialize(Table(Phase.Action, 0)).Contains("Wave738"), "and nothing is saved while unused");
    }
}
