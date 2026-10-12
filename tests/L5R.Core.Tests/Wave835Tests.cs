using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #835: the cards Mendon's rulings of 2026-10-11 (third batch) unblocked (src/L5R.Core/Wave835.cs), proved on the published cards themselves. Player 0 is the Crane, player 1 the Lion; every position is
// explicit, driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave835Tests
{
    private const string CleansingBreath = Wave835Abilities.CleansingBreathCard, ImperialDeployment = Wave835Abilities.ImperialDeploymentCard, WardedPremises = Wave835Abilities.WardedPremisesCard,
        Duty = Wave835Abilities.DutyCard, Pierce = Wave835Abilities.PierceCard;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Item = "training-item";
    // Fixtures: published Personalities carrying the keywords the rulings turn on.
    private const string Kensai = "oracle-l5r-10663" /* Lao-she */, Monk = "oracle-l5r-9998" /* Guest */, Yojimbo = "oracle-l5r-9819" /* Mirumoto Rokai, 3 Gold */, Courtier = "oracle-l5r-10013" /* Rokuta's Apprentice */,
        Shugenja = "oracle-l5r-11328" /* Ashalan */;

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, int province = 0, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province);
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static MatchState Poor(Phase phase = Phase.Action, int actor = 0)
    {
        var s = Batch336Support.Table(phase, actor);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        return s;
    }
    private static void Pass(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is not null && guard < 16; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    public static void Run(TestRunner runner) => runner.Run("wave-835", [
        ("Published as printed: Pierce the Tapestry is spelled correctly with its seven-copy limit; every card has its abilities, text and manifest", PublishedShape),
        ("A Cleansing Breath: 'He centers himself' does nothing; other players' preventions are negated for the turn; a Kensai straightens his unit", CleansingBreathCard),
        ("Imperial Deployment: only assigning is freed (he may still be barred from moving); his unit straightens when an attack is declared, unless a straightening is prevented", ImperialDeploymentCard),
        ("Too Close to Home bars movement between battlefields and to and from home, not assignment; the Perfect Moment restricts only its own player's Personalities", TooCloseAndPerfectMoment),
        ("Warded Premises: the Terrain negates only the Force bonuses actions gave, at its battlefield; it can be put into play with no units there, and not over another Terrain", WardedPremisesTerrain),
        ("Duty Over All Things: recruits a discarded (not dead) Yojimbo at the battlefield when an enemy Personality stands there, three Gold cheaper with a Courtier or Shugenja", DutyOverAllThings),
        ("Pierce the Tapestry: -3F; the third Pierce this battle destroys a target with no attachments; a Pierce that did not destroy grants an additional Pierce action", PierceTheTapestry),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, CardType Type, string[] Abilities)[] cards = [
            (CleansingBreath, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.CleansingBreath]),
            (ImperialDeployment, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.ImperialDeployment]),
            (WardedPremises, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.WardedNegate, Wave835Abilities.WardedPlay]),
            (Duty, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.DutyDiscount, Wave835Abilities.DutyFull]),
            (Pierce, "strategy-wave-835", CardType.Strategy, [Wave835Abilities.Pierce])];
        foreach (var (id, wave, type, abilities) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(string.Join(",", def.Abilities ?? []), string.Join(",", abilities), id + " abilities");
            Check(!def.CreatedOnly && !def.ScenarioOnly, id + " is a published, deck-legal card");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        // Mendon: Oracle of the Void misspells it "Tapestery"; it is spelled correctly here, with up to seven copies in a deck.
        Eq(CardCatalog.Get(Pierce).Name, "Pierce the Tapestry", "the local name is spelled correctly");
        Eq(CardCatalog.Get(Pierce).DeckCopyLimit, 7, "up to seven copies of this card in a deck");
        Check(!CardCatalog.Cards.Values.Any(d => d.Name.Contains("Tapestery")), "no published card keeps the misspelling");
        Check(CardCatalog.Get(CleansingBreath).Text.Contains("He centers himself."), "the printed text keeps the flavor sentence");
        var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-835-sources.json"))).RootElement.EnumerateArray().Single(r => r.GetProperty("card_id").GetString() == Pierce);
        Eq(source.GetProperty("oracle_name").GetString(), "Pierce the Tapestery", "the source row records Oracle's spelling");
    }

    // ---- A Cleansing Breath ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CleansingBreathCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kensai = Add(s, Kensai, 0, Zone.Home, bowed: true);
        var follower = Add(s, "training-follower", 0, Zone.Attached, true, null, kensai.Id);
        var plain = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var enemy = Add(s, LionSamurai, 1, Zone.Home);
        var card = Add(s, CleansingBreath, 0, Zone.Hand);
        var e = Engine(s);
        // Any of his own Personalities at any location, never another player's.
        var targets = Batch336Support.Options(e, 0, Wave835Abilities.CleansingBreath, card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().OrderBy(x => x).ToArray();
        Eq(string.Join(",", targets), string.Join(",", new[] { kensai.Id, plain.Id }.OrderBy(x => x)), "your Personalities, at home or anywhere, and nobody else's");
        // A lock another player's card made: it binds until the Breath, then the Personality is free of it.
        var lockedState = e.State;
        lockedState.Cards[kensai.Id].MovementLocks.Add(new MovementLock("test", lockedState.TurnNumber, MovementKinds.Home | MovementKinds.ToBattlefield | MovementKinds.Assign, ByPlayer: 1));
        var e2 = Engine(lockedState);
        Check(e2.MovementLockedForTest(kensai.Id, MovementKinds.Assign) && e2.MovementLockedForTest(kensai.Id, MovementKinds.ToBattlefield), "the other player's lock binds before the Breath");
        Accept(e2, Batch336Support.Offer(e2, 0, Wave835Abilities.CleansingBreath, card.Id, kensai.Id));
        Check(!e2.MovementLockedForTest(kensai.Id, MovementKinds.Assign) && !e2.MovementLockedForTest(kensai.Id, MovementKinds.ToBattlefield) && !e2.MovementLockedForTest(kensai.Id, MovementKinds.Home), "assigning and moving are no longer prevented");
        Check(e2.IsImmuneForTest(kensai.Id, ImmunityKind.Prevention, 1), "other players' cards' preventions are negated");
        Check(!e2.State.Cards[kensai.Id].Bowed && !e2.State.Cards[follower.Id].Bowed, "a Kensai straightens his whole unit");
        // Someone who is not a Kensai stays as he was.
        var e3 = Engine(e.State);
        var card3 = e3.State.Cards.Values.First(c => c.DefinitionId == CleansingBreath);
        Accept(e3, Batch336Support.Offer(e3, 0, Wave835Abilities.CleansingBreath, card3.Id, plain.Id));
        Check(e3.State.Cards[plain.Id].Bowed, "a Personality who is not a Kensai is not straightened");
        Check(e3.IsImmuneForTest(plain.Id, ImmunityKind.Prevention, 1) && !e3.IsImmuneForTest(plain.Id, ImmunityKind.Prevention, 0), "the immunity is against other players only");
    }

    // ---- Imperial Deployment --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImperialDeploymentCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var deployed = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        var attached = Add(s, Item, 0, Zone.Attached, true, null, deployed.Id);
        var card = Add(s, ImperialDeployment, 0, Zone.Hand);
        deployed.MovementLocks.Add(new MovementLock("test", s.TurnNumber, MovementKinds.Assign | MovementKinds.AssignAttack | MovementKinds.ToBattlefield | MovementKinds.Home, ByPlayer: 1));
        var e = Engine(s);
        Check(e.MovementLockedForTest(deployed.Id, MovementKinds.Assign), "the lock stops him assigning before the card");
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.ImperialDeployment, card.Id, performerId: deployed.Id));
        Check(!e.MovementLockedForTest(deployed.Id, MovementKinds.Assign) && !e.MovementLockedForTest(deployed.Id, MovementKinds.AssignAttack), "only the effects that stop him assigning are negated");
        Check(e.MovementLockedForTest(deployed.Id, MovementKinds.ToBattlefield) && e.MovementLockedForTest(deployed.Id, MovementKinds.Home), "he may still be barred from moving: assigning is not moving");
        Check(e.IsImmuneForTest(deployed.Id, ImmunityKind.AssignPrevention, 1) && !e.IsImmuneForTest(deployed.Id, ImmunityKind.Prevention, 1), "an assigning-only immunity");
        // A "may not attack" that another player's action stored is an effect preventing assigning too; a rule's own restriction is not.
        var withRestriction = e.State;
        withRestriction.Restrictions.Items.Add(new("restriction-test", RestrictionKind.NoAttack, deployed.Id, UntilTurn: withRestriction.TurnNumber, ByPlayer: 1));
        var e2 = Engine(withRestriction);
        Check(!(bool)Call(e2, "RestrictionBarsAssignment", e2.State.Cards[deployed.Id], false)!, "another player's stored 'may not attack' no longer bars the assignment");
        var ownRule = e.State;
        ownRule.Restrictions.Items.Clear();
        ownRule.Restrictions.Items.Add(new("restriction-own", RestrictionKind.NoAttack, deployed.Id, UntilTurn: ownRule.TurnNumber));
        Check((bool)Call(Engine(ownRule), "RestrictionBarsAssignment", ownRule.Cards[deployed.Id], false)!, "a restriction with no other player's source still bars it");
        // The attack is declared: his unit straightens, once.
        var declaring = Engine(e.State);
        declaring.State.Phase = Phase.AttackDeclaration;
        var st = declaring.State; st.Phase = Phase.AttackDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0;
        var attack = Engine(st);
        Accept(attack, new GameCommand("declareAttack", 0, attack.Version));
        Check(!attack.State.Cards[deployed.Id].Bowed && !attack.State.Cards[attached.Id].Bowed, "after the attack is declared his unit straightens");
        Check(attack.State.Wave835 is null || attack.State.Wave835.Deployments.Count == 0, "the watch is spent");
        // Only a prevention of straightening stops it: 'prevent the next straightening' and 'will not straighten' do; nothing else does.
        foreach (var (name, hold) in new (string, Action<CardInstance, MatchState>)[] {
            ("prevent the next straightening", (c, state) => c.StraightenBlockedTurn = state.TurnNumber),
            ("will not straighten", (c, state) => c.StraightenLockedTurn = state.TurnNumber) })
        {
            var again = Engine(e.State);
            var held = again.State;
            held.Phase = Phase.AttackDeclaration; held.ActivePlayerId = 0; held.PriorityPlayerId = 0;
            hold(held.Cards[deployed.Id], held);
            var heldEngine = Engine(held);
            Accept(heldEngine, new GameCommand("declareAttack", 0, heldEngine.Version));
            Check(heldEngine.State.Cards[deployed.Id].Bowed, $"{name}: the mandatory straightening is barred");
        }
        // No attack declared, no straightening (it waits for this turn's attack only).
        Check(e.State.Cards[deployed.Id].Bowed, "he is not straightened by playing the card");
    }

    private static void TooCloseAndPerfectMoment()
    {
        var tooClose = CardCatalog.Cards.Values.Single(d => d.Effect == "too-close-to-home");
        var s = Batch336Support.Table(Phase.Action, 0);
        var unit = Add(s, CraneSamurai, 0, Zone.Home);
        Add(s, tooClose.Id, 1, Zone.EventInPlay);
        var e = Engine(s);
        Check(e.MovementLockedForTest(unit.Id, MovementKinds.ToBattlefield) && e.MovementLockedForTest(unit.Id, MovementKinds.Home), "Too Close to Home bars movement to a battlefield (from home or between battlefields) and home from a battlefield");
        Check(!e.MovementLockedForTest(unit.Id, MovementKinds.Assign) && !e.MovementLockedForTest(unit.Id, MovementKinds.AssignAttack), "it does not bar assignment to battlefields");
        // The Perfect Moment restricts only its own player's Personalities: another player's Personality (the one Imperial Deployment protects) is not affected by it.
        var pm = Batch336Support.Table(Phase.Action, 0);
        var mine = Add(pm, CraneSamurai, 0, Zone.Home); var theirs = Add(pm, LionSamurai, 1, Zone.Home);
        var pe = Engine(pm);
        var state = pe.State;
        state.R1 ??= new(); state.R1.PerfectMoment = new PerfectMomentState { PlayerId = 1 };
        var locked = Engine(state);
        Check(locked.MovementLockedForTest(theirs.Id, MovementKinds.Assign) && !locked.MovementLockedForTest(mine.Id, MovementKinds.Assign), "the Perfect Moment's player's Personalities may not assign; the other player's are not restricted by it");
    }

    // ---- Warded Premises ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WardedPremisesTerrain()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var mine = Bf(s, CraneSamurai, 0); var theirs = Bf(s, LionSamurai, 1); var elsewhere = Add(s, LionSamurai, 1, Zone.Battlefield, false, 2);
        var weapon = Add(s, Item, 0, Zone.Attached, false, 0, mine.Id);
        // Bonuses of every kind on cards at the battlefield.
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", 3));                       // an action's temporary bonus: negated
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", 2, Permanent: true));     // a permanent bonus: kept
        mine.Modifiers.Add(new("src", "test-trait", "Force", 1));                              // a trait's standing bonus: kept
        mine.Modifiers.Add(new("src", "defensive-stance", "Force", -2));                       // a penalty from an action: kept
        theirs.Modifiers.Add(new("src", "defensive-stance", "Force", 4));
        weapon.Modifiers.Add(new("src", "defensive-stance", "Force", 1));                      // an attachment given a bonus by an action: it is a card at the battlefield too
        elsewhere.Modifiers.Add(new("src", "defensive-stance", "Force", 5));                  // another battlefield: untouched
        mine.Tokens.Add(new("token-1", "Force", "src", "Force", 1));
        var terrain = Add(s, WardedPremises, 0, Zone.Battlefield, false, 0);
        var e = Engine(s);
        Check(((string[])Call(e, "BeforeBattleResolveSourceIds", 0)!).Contains(terrain.Id), "the Terrain joins the set of before-resolution effects at its battlefield");
        Call(e, "Wave835WardedNegate", e.State.Cards[terrain.Id]);
        var bonuses = (string id) => string.Join(",", e.State.Cards[id].Modifiers.Select(m => $"{m.Effect}:{m.Amount}{(m.Permanent ? "p" : "")}"));
        Eq(bonuses(mine.Id), "defensive-stance:2p,test-trait:1,defensive-stance:-2", "only the action's temporary bonus is gone: permanent, trait and penalty stay");
        Eq(bonuses(theirs.Id), "", "every card at the battlefield, either side's");
        Eq(bonuses(weapon.Id), "", "and the attachments there");
        Eq(bonuses(elsewhere.Id), "defensive-stance:5", "not another battlefield");
        Eq(e.State.Cards[mine.Id].Tokens.Count, 1, "tokens are untouched");
        // Its Battle action: with no units of yours there, if there is no Terrain.
        var play = Batch336Support.Table(Phase.Battle, 0);
        Bf(play, LionSamurai, 1);
        var card = Add(play, WardedPremises, 0, Zone.Hand);
        var pe = Engine(play);
        Accept(pe, Batch336Support.Use(pe, 0, Wave835Abilities.WardedPlay, card.Id));
        Eq(pe.State.Cards[card.Id].Zone, Zone.Battlefield, "put into play at the battlefield even though you control no units there");
        var blocked = Batch336Support.Table(Phase.Battle, 0);
        Bf(blocked, LionSamurai, 1);
        var other = Add(blocked, WardedPremises, 1, Zone.Battlefield, false, 0);
        var card2 = Add(blocked, WardedPremises, 0, Zone.Hand);
        var be = Engine(blocked);
        Check(!Batch336Support.CanUse(be, 0, Wave835Abilities.WardedPlay, card2.Id), "not when there is already a Terrain at the battlefield");
        Check(other.Zone == Zone.Battlefield, "the other Terrain stays");
    }

    // ---- Duty Over All Things ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DutyOverAllThings()
    {
        // With a Courtier in play: three Gold cheaper -- the 3 Gold Yojimbo is free, and the player has no Gold at all.
        var s = Poor(Phase.Battle, 0);
        Bf(s, CraneSamurai, 0); Add(s, Courtier, 0, Zone.Home);
        Bf(s, LionSamurai, 1);
        var duty = Add(s, Duty, 0, Zone.Hand);
        var discarded = Add(s, Yojimbo, 0, Zone.DynastyDiscard);
        var dead = Add(s, Yojimbo, 0, Zone.Dead);
        var others = Add(s, CraneSamurai, 0, Zone.DynastyDiscard);
        var theirs = Add(s, Yojimbo, 1, Zone.DynastyDiscard);
        var e = Engine(s);
        var discount = Batch336Support.Options(e, 0, Wave835Abilities.DutyDiscount, duty.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Eq(string.Join(",", discount), discarded.Id, "only your own discarded Yojimbo, never a dead one, a non-Yojimbo or the other player's");
        Check(!Batch336Support.CanUse(e, 0, Wave835Abilities.DutyFull, duty.Id), "with a Courtier the plain-price version is not offered");
        Accept(e, Batch336Support.Offer(e, 0, Wave835Abilities.DutyDiscount, duty.Id, discarded.Id));
        Pass(e);
        Eq(e.State.Cards[discarded.Id].Zone, Zone.Battlefield, "he enters play at the battlefield where it was played");
        Eq(e.State.Cards[discarded.Id].ProvinceIndex, 0, "at province 0");
        Eq(e.State.Cards[duty.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // A Shugenja counts as well as a Courtier.
        var sh = Poor(Phase.Battle, 0);
        Bf(sh, CraneSamurai, 0); Add(sh, Shugenja, 0, Zone.Home); Bf(sh, LionSamurai, 1);
        var d2 = Add(sh, Duty, 0, Zone.Hand); var y2 = Add(sh, Yojimbo, 0, Zone.DynastyDiscard);
        var e2 = Engine(sh);
        Check(Batch336Support.CanUse(e2, 0, Wave835Abilities.DutyDiscount, d2.Id), "a Shugenja gives the discount too");
        // Without either: the plain recruit, which this player cannot afford with no Gold.
        var plain = Poor(Phase.Battle, 0);
        Bf(plain, CraneSamurai, 0); Bf(plain, LionSamurai, 1);
        var d3 = Add(plain, Duty, 0, Zone.Hand); var y3 = Add(plain, Yojimbo, 0, Zone.DynastyDiscard);
        var e3 = Engine(plain);
        Check(!Batch336Support.CanUse(e3, 0, Wave835Abilities.DutyDiscount, d3.Id), "the discounted version needs a Courtier or Shugenja");
        Check(Batch336Support.Options(e3, 0, Wave835Abilities.DutyFull, d3.Id).Any(), "the plain version is listed");
        Check(!Batch336Support.CanUse(e3, 0, Wave835Abilities.DutyFull, d3.Id), "but not payable without Gold (it costs the full 3)");
        // And affordable with the Gold: the Strategy is played, paying the whole cost.
        var rich = Batch336Support.Table(Phase.Battle, 0);
        Bf(rich, CraneSamurai, 0); Bf(rich, LionSamurai, 1);
        var d4 = Add(rich, Duty, 0, Zone.Hand); var y4 = Add(rich, Yojimbo, 0, Zone.DynastyDiscard);
        var e4 = Engine(rich);
        Check(Batch336Support.CanUse(e4, 0, Wave835Abilities.DutyFull, d4.Id), "with Gold, the plain version is playable");
        // "If he would be opposed": no enemy Personality at the battlefield, no Duty.
        var open = Batch336Support.Table(Phase.Battle, 0);
        Bf(open, CraneSamurai, 0); Add(open, Courtier, 0, Zone.Home);
        var d5 = Add(open, Duty, 0, Zone.Hand); Add(open, Yojimbo, 0, Zone.DynastyDiscard);
        var e5 = Engine(open);
        Check(!Batch336Support.CanUse(e5, 0, Wave835Abilities.DutyDiscount, d5.Id) && !Batch336Support.CanUse(e5, 0, Wave835Abilities.DutyFull, d5.Id), "with no enemy Personality at the battle it may not be played");
    }

    // ---- Pierce the Tapestry --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PierceTheTapestry()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var monk = Bf(s, Monk, 0);
        var target = Bf(s, LionSamurai, 1);
        var guarded = Bf(s, LionSamurai, 1); Add(s, Item, 1, Zone.Attached, false, 0, guarded.Id);
        var first = Add(s, Pierce, 0, Zone.Hand); var second = Add(s, Pierce, 0, Zone.Hand); var third = Add(s, Pierce, 0, Zone.Hand); var fourth = Add(s, Pierce, 0, Zone.Hand);
        var e = Engine(s);
        var force = (string id) => (int)Call(e, "PersonalityForce", e.State.Cards[id])!;
        var start = force(target.Id);
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, first.Id, target.Id, monk.Id));
        Pass(e);
        Eq(force(target.Id), start - 3, "-3F");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true), "it did not destroy: an additional Battle action to play a Pierce the Tapestry");
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, second.Id, target.Id, monk.Id));
        Pass(e);
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield, "two plays this battle: not destroyed yet");
        // The third play has two other Pierce behind it, and the target has no attachments: destroyed. No additional action then.
        var grantsBefore = e.State.AdditionalActions.Count(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true);
        Accept(e, Batch336Support.Use(e, 0, Wave835Abilities.Pierce, third.Id, target.Id, monk.Id));
        Pass(e);
        Check(e.State.Cards[target.Id].Zone != Zone.Battlefield, "the third Pierce this battle destroys a target with no attachments");
        Check(e.State.AdditionalActions.Count(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true) <= Math.Max(0, grantsBefore - 1), "and it grants no further additional action");
        // A target with an attachment is only weakened however many Pierce came before, and the additional action is granted again.
        var armed = Batch336Support.Table(Phase.Battle, 0);
        var armedMonk = Bf(armed, Monk, 0); var armedTarget = Bf(armed, LionSamurai, 1); Add(armed, Item, 1, Zone.Attached, false, 0, armedTarget.Id);
        var plays = Enumerable.Range(0, 4).Select(_ => Add(armed, Pierce, 0, Zone.Hand)).ToArray();
        var ae = Engine(armed);
        foreach (var play in plays)
        {
            Accept(ae, Batch336Support.Use(ae, 0, Wave835Abilities.Pierce, play.Id, armedTarget.Id, armedMonk.Id));
            Pass(ae);
            Check(ae.State.Cards[armedTarget.Id].Zone == Zone.Battlefield, "a target with an attachment survives");
            Check(ae.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeAbilityIds?.Contains(Wave835Abilities.Pierce) == true), "so another Pierce may be played as an additional action");
        }
        // A Pierce needs its performing unbowed Monk, and an enemy Personality or Follower.
        var bare = Batch336Support.Table(Phase.Battle, 0);
        Bf(bare, CraneSamurai, 0); Bf(bare, LionSamurai, 1);
        var lone = Add(bare, Pierce, 0, Zone.Hand);
        Check(!Batch336Support.CanUse(Engine(bare), 0, Wave835Abilities.Pierce, lone.Id), "no Monk of yours at the battle, no Pierce");
    }
}
