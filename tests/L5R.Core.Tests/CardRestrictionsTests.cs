using System.Reflection;
using L5R.Core;

// Primitive P11 (issue #684): the card-restriction store and the generalised target bars. Engine-level tests on already-published fixture cards; the cards that use it are wired in a later wave.
// A static restriction registered here is keyed on a fixture definition id and cleared by the test that registered it, so it cannot reach another suite.
internal static class CardRestrictionsTests
{
    private const string Samurai = "training-crane-samurai", Veteran = "training-lion-veteran", Lion = "training-lion-samurai", Follower = "training-follower", Item = "training-item";

    public static void Run(TestRunner runner) => runner.Run("card-restrictions", [
        ("May not attack / may not defend: refused in Assign, absent from the offered units, gone next turn; others unaffected", NoAttackNoDefend),
        ("A pair restriction refuses assigning the two to one battlefield (together or one after the other) and allows separate battlefields; the family variant pairs a family name", PairRestriction),
        ("May assign even if bowed (stored and static): a bowed Personality is assignable only while it holds", AssignWhileBowed),
        ("Will not attach: a stored or static restriction by type or keyword refuses Equip and the offered targets; other attachments still attach", NoAttach),
        ("Will not become dishonorable and will not challenge: the effect fails and the state is otherwise unchanged", DishonorAndChallenge),
        ("Province entry lock: Recruit is quoted an error and refused, the other province and the next turn are free; a Celestial's entry is delayed to the next turn and the province refills face-down", ProvinceLock),
        ("May act while bowed: the performer category is judged unbowed, a bow-the-performer cost still blocks him", ActWhileBowed),
        ("Generalised bars: controller-only, others-only, keyword-scoped, unit-of and a phase duration; static bars read live while the card is in play", TargetBars),
        ("Forced target: a matching actor must target the forced card when it is legal, and may name another card when it is not", ForcedTarget),
        ("Spell limit counts a performer's Spell actions and refuses the one beyond the limit", SpellLimit),
    ]);

    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);
    private static MatchState Maneuvers(Phase phase, int active)
    {
        var s = MatchEngine.CreateStarterMatch().State;
        s.Phase = phase; s.ActivePlayerId = active; s.PriorityPlayerId = active;
        s.UnresolvedBattlefields = s.Players[1 - active].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToList();
        return s;
    }
    private static MatchState Live(MatchEngine e) => (MatchState)typeof(MatchEngine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(e)!;
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException inner) { throw inner.InnerException!; }
    }
    private static bool Throws(Action action) { try { action(); return false; } catch (Exception e) when (e.GetType().Name == "RuleViolation") { return true; } }
    private static CardRestriction Stored(MatchState s, RestrictionKind kind, string cardId, string? arg = null, string? other = null, RestrictionUntil until = RestrictionUntil.Turn)
    {
        var r = new CardRestriction("test-" + s.Restrictions.Items.Count, kind, cardId, null, null, arg, other, until, s.TurnNumber);
        s.Restrictions.Items.Add(r);
        return r;
    }
    private static GameCommand Attack(MatchEngine e, params (string Id, int Province)[] units) =>
        new("attack", e.State.ActivePlayerId, e.Version, Assignments: units.ToDictionary(u => u.Id, u => u.Province));
    private static void Rejected(MatchEngine e, GameCommand command, string message)
    {
        var before = e.Version;
        var result = e.Execute(command);
        Check(!result.Accepted && e.Version == before, message + " (accepted or advanced)");
    }

    private static void NoAttackNoDefend()
    {
        var s = Maneuvers(Phase.InfantryDeclaration, 0);
        var barred = Batch336Support.Add(s, Samurai, 0, Zone.Home); var free = Batch336Support.Add(s, Samurai, 0, Zone.Home);
        Stored(s, RestrictionKind.NoAttack, barred.Id);
        var e = MatchEngine.FromTestState(s);
        var offered = e.QueryActions(0).Where(a => a.Type == "attack" && a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(!offered.Contains(barred.Id) && offered.Contains(free.Id), "the barred unit is not offered to attack, the free one is");
        Rejected(e, Attack(e, (barred.Id, 0)), "assigning a Personality that may not attack");
        Check(e.State.Cards[barred.Id].Zone == Zone.Home, "the barred unit stayed home");
        Check(e.Execute(Attack(e, (free.Id, 0))).Accepted, "another Personality attacks");

        // It lasts the turn: the next turn it is gone.
        var later = Maneuvers(Phase.InfantryDeclaration, 0); var again = Batch336Support.Add(later, Samurai, 0, Zone.Home); Stored(later, RestrictionKind.NoAttack, again.Id); later.TurnNumber++;
        Check(MatchEngine.FromTestState(later).Execute(Attack(MatchEngine.FromTestState(later), (again.Id, 0))).Accepted, "an expired restriction no longer holds");

        // May not defend: the defender's own assignment, and only the defend side.
        var d = Maneuvers(Phase.DefenseAssignment, 0);
        var defender = Batch336Support.Add(d, Samurai, 1, Zone.Home); var other = Batch336Support.Add(d, Samurai, 1, Zone.Home);
        Stored(d, RestrictionKind.NoDefend, defender.Id);
        d.PriorityPlayerId = 1;
        var de = MatchEngine.FromTestState(d);
        var defending = de.QueryActions(1).Where(a => a.Type == "assignDefense" && a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(!defending.Contains(defender.Id) && defending.Contains(other.Id), "may not defend: not offered to defend");
        Rejected(de, new("assignDefense", 1, de.Version, Assignments: new() { [defender.Id] = 0 }), "defending with a Personality that may not defend");
        var attackSide = Maneuvers(Phase.InfantryDeclaration, 1); var defendOnly = Batch336Support.Add(attackSide, Samurai, 1, Zone.Home); Stored(attackSide, RestrictionKind.NoDefend, defendOnly.Id);
        var ae = MatchEngine.FromTestState(attackSide);
        Check(ae.Execute(Attack(ae, (defendOnly.Id, 0))).Accepted, "may not defend does not stop him attacking");
    }

    private static void PairRestriction()
    {
        var s = Maneuvers(Phase.InfantryDeclaration, 0);
        var a = Batch336Support.Add(s, Samurai, 0, Zone.Home); var b = Batch336Support.Add(s, Samurai, 0, Zone.Home); var c = Batch336Support.Add(s, Samurai, 0, Zone.Home);
        Stored(s, RestrictionKind.NoPair, a.Id, null, b.Id);
        var e = MatchEngine.FromTestState(s);
        Rejected(e, Attack(e, (a.Id, 0), (b.Id, 0)), "the pair assigned together to one battlefield");
        Check(e.Execute(Attack(e, (a.Id, 0), (b.Id, 1), (c.Id, 0))).Accepted, "the pair on different battlefields, a third beside one of them");
        // One after the other (Infantry, then Cavalry): the second is refused where the first already stands.
        var seq = Maneuvers(Phase.InfantryDeclaration, 0);
        var first = Batch336Support.Add(seq, Samurai, 0, Zone.Home); var second = Batch336Support.Add(seq, Samurai, 0, Zone.Battlefield, province: 0);
        Stored(seq, RestrictionKind.NoPair, first.Id, null, second.Id);
        var se = MatchEngine.FromTestState(seq);
        Rejected(se, Attack(se, (first.Id, 0)), "joining the card it may not stand beside");
        Check(se.Execute(Attack(se, (first.Id, 1))).Accepted, "another battlefield is fine");

        // The family variant: two Personalities of one family name may not share a battlefield. The helper judges the family the Torn Asunder way.
        var fam = Maneuvers(Phase.InfantryDeclaration, 0);
        var doji1 = FamilyMember(fam, 0, "Doji"); var doji2 = FamilyMember(fam, 0, "Doji"); var kakita = FamilyMember(fam, 0, "Kakita");
        Stored(fam, RestrictionKind.NoPair, doji1.Id, "family");
        var fe = MatchEngine.FromTestState(fam);
        Check((bool)Call(fe, "SameFamilyName", fe.State.Cards[doji1.Id], fe.State.Cards[doji2.Id])! && !(bool)Call(fe, "SameFamilyName", fe.State.Cards[doji1.Id], fe.State.Cards[kakita.Id])!, "SameFamilyName pairs the family");
        Rejected(fe, Attack(fe, (doji1.Id, 0), (doji2.Id, 0)), "two of one family together");
        Check(fe.Execute(Attack(fe, (doji1.Id, 0), (kakita.Id, 0))).Accepted, "a different family is no pair");
    }

    private static CardInstance FamilyMember(MatchState s, int owner, string family)
    {
        var def = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && !c.CreatedOnly && c.Type == CardType.Personality && c.Name.StartsWith(family + " ", StringComparison.Ordinal) && c.Name.Split(' ').Length == 2
            && (c.Keywords ?? []).Any(k => k.EndsWith(" Clan", StringComparison.Ordinal)) && !c.Text.Contains("may not assign", StringComparison.OrdinalIgnoreCase));
        return Batch336Support.Add(s, def.Id, owner, Zone.Home);
    }

    private static void AssignWhileBowed()
    {
        var s = Maneuvers(Phase.InfantryDeclaration, 0);
        var bowed = Batch336Support.Add(s, Samurai, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Rejected(e, Attack(e, (bowed.Id, 0)), "a bowed Personality without the restriction");
        var s2 = Maneuvers(Phase.InfantryDeclaration, 0);
        var b2 = Batch336Support.Add(s2, Samurai, 0, Zone.Home, bowed: true);
        Stored(s2, RestrictionKind.AssignWhileBowed, b2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.QueryActions(0).Any(a => a.Type == "attack" && a.Enabled && a.TargetIds!.Contains(b2.Id)), "the bowed Personality is offered");
        Check(e2.Execute(Attack(e2, (b2.Id, 0))).Accepted, "a stored restriction lets him assign while bowed");
        CardRestrictionCatalog.Register(Veteran, new StaticRestriction(RestrictionKind.AssignWhileBowed));
        try
        {
            var s3 = Maneuvers(Phase.InfantryDeclaration, 0); var b3 = Batch336Support.Add(s3, Veteran, 0, Zone.Home, bowed: true);
            var e3 = MatchEngine.FromTestState(s3);
            Check(e3.Execute(Attack(e3, (b3.Id, 0))).Accepted, "a static restriction does too while he is in play");
        }
        finally { CardRestrictionCatalog.Clear(Veteran); }
    }

    private static MatchState ActionTable() { var s = Batch336Support.Table(Phase.Action, 0); s.PriorityPlayerId = 0; return s; }

    private static void NoAttach()
    {
        // A static "will not attach Followers or Items" on a host (Natsumi - exp's shape).
        CardRestrictionCatalog.Register(Lion, new StaticRestriction(RestrictionKind.NoAttach, "type:Follower|Item"));
        try
        {
            var s = ActionTable();
            var host = Batch336Support.Add(s, Lion, 0, Zone.Home); var item = Batch336Support.Add(s, Item, 0, Zone.Hand); var follower = Batch336Support.Add(s, Follower, 0, Zone.Hand);
            host.Modifiers.Add(new("test", "test", "PersonalHonor", 1));
            var e = MatchEngine.FromTestState(s);
            var sources = e.PaymentOptions(0, 2).First().SourceIds;
            Rejected(e, new("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: sources), "an Item to the host that will not attach Items");
            Rejected(e, new("equip", 0, e.Version, CardId: follower.Id, TargetId: host.Id, SourceIds: sources), "a Follower to the host that will not attach Followers");
        }
        finally { CardRestrictionCatalog.Clear(Lion); }

        // A stored one by keyword: Armor only. The Follower still attaches.
        var t = ActionTable();
        var h = Batch336Support.Add(t, Lion, 0, Zone.Home); var follow = Batch336Support.Add(t, Follower, 0, Zone.Hand);
        h.Modifiers.Add(new("test", "test", "PersonalHonor", 1));
        Stored(t, RestrictionKind.NoAttach, h.Id, "keyword:Armor");
        var te = MatchEngine.FromTestState(t);
        Check(te.Execute(new("equip", 0, te.Version, CardId: follow.Id, TargetId: h.Id, SourceIds: te.PaymentOptions(0, 2).First().SourceIds)).Accepted, "a Follower still attaches to a host that refuses Armor");
        Check((bool)typeof(MatchEngine).GetMethod("AttachRestrictionMatches", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, ["any", CardCatalog.Get(Follower)])!, "'any' refuses every attachment");
    }

    private static void DishonorAndChallenge()
    {
        var s = ActionTable();
        var protectedCard = Batch336Support.Add(s, Samurai, 0, Zone.Home); var plain = Batch336Support.Add(s, Samurai, 0, Zone.Home); var challenged = Batch336Support.Add(s, Lion, 1, Zone.Home);
        Stored(s, RestrictionKind.NoDishonor, protectedCard.Id); Stored(s, RestrictionKind.NoChallenge, protectedCard.Id);
        var e = MatchEngine.FromTestState(s);
        var live = Live(e);
        Call(e, "Dishonor", live.Cards[protectedCard.Id], null);
        Check(!live.Cards[protectedCard.Id].Dishonorable, "will not become dishonorable");
        Call(e, "Dishonor", live.Cards[plain.Id], null);
        Check(live.Cards[plain.Id].Dishonorable, "another Personality is dishonored as usual");
        Call(e, "IssueChallenge", "training-duel-force", 0, protectedCard.Id, challenged.Id, "Force", false, null);
        Check(live.PendingDuel is null && live.Events.Any(x => x.Type == "challengeRefused"), "a Personality that will not challenge issues no duel");
        Call(e, "IssueChallenge", "training-duel-force", 0, plain.Id, challenged.Id, "Force", false, null);
        Check(live.PendingDuel is not null, "another Personality challenges");
    }

    private static void ProvinceLock()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
        var inLocked = Batch336Support.Add(s, Samurai, 0, Zone.Province, province: 0); inLocked.FaceUp = true; s.Players[0].Provinces[0].CardId = inLocked.Id;
        var inOpen = Batch336Support.Add(s, Samurai, 0, Zone.Province, province: 1); inOpen.FaceUp = true; s.Players[0].Provinces[1].CardId = inOpen.Id;
        s.Restrictions.Items.Add(new("lock-1", RestrictionKind.NoEntryFromProvince, null, 0, 0, null, null, RestrictionUntil.Turn, s.TurnNumber));
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, inLocked.Id);
        Check(quote.Error is not null && quote.Error.Contains("will not enter play", StringComparison.Ordinal), "the locked province's card is quoted an error: " + quote.Error);
        Check(e.QuotePurchase(0, inOpen.Id).Error?.Contains("will not enter play", StringComparison.Ordinal) != true, "the other province is not locked");
        Check(!e.QueryActions(0).Any(a => a.Type == "purchase" && a.Enabled && a.CardId == inLocked.Id), "no enabled purchase is offered for the locked province");
        Rejected(e, new("purchase", 0, e.Version, CardId: inLocked.Id, SourceIds: []), "buying from the locked province");
        var next = e.State; next.TurnNumber++;
        Check(MatchEngine.FromTestState(next).QuotePurchase(0, inLocked.Id).Error?.Contains("will not enter play", StringComparison.Ordinal) != true, "the lock is gone next turn");

        // API: the lock added by an effect.
        var made = MatchEngine.FromTestState(Batch336Support.Table(Phase.Dynasty, 0));
        made.AddProvinceEntryLock(1, 3);
        Check(made.ProvinceEntryLocked(1, 3) && !made.ProvinceEntryLocked(1, 2) && !made.ProvinceEntryLocked(0, 3), "AddProvinceEntryLock locks that province only");

        // A Celestial revealed from a locked province waits for the next turn, then enters and the province refills face-down.
        var c = Batch336Support.Table(Phase.Events, 0);
        var celestial = CardCatalog.Cards.Values.Where(d => !d.ScenarioOnly && !d.CreatedOnly && d.Type == CardType.Celestial).OrderBy(d => d.Id, StringComparer.Ordinal).First();
        var card = Batch336Support.Add(c, celestial.Id, 0, Zone.Province, province: 2); card.FaceUp = true; c.Players[0].Provinces[2].CardId = card.Id;
        c.Restrictions.Items.Add(new("lock-2", RestrictionKind.NoEntryFromProvince, null, 2, 0, null, null, RestrictionUntil.Turn, c.TurnNumber));
        var ce = MatchEngine.FromTestState(c); var live = Live(ce);
        Call(ce, "ResolveCelestial", live.Players[0], live.Players[0].Provinces[2], live.Cards[card.Id]);
        Check(live.Cards[card.Id].Zone == Zone.Province && live.Restrictions.DelayedCelestials.Count == 1, "the Celestial did not enter: its entry is delayed");
        live.TurnNumber++;
        Call(ce, "RestrictionsTurnBegin");
        Check(live.Cards[card.Id].Zone == Zone.Celestial && live.Restrictions.DelayedCelestials.Count == 0, "it enters when the next turn begins");
        var refill = live.Players[0].Provinces[2].CardId;
        Check(refill is not null && refill != card.Id && !live.Cards[refill].FaceUp, "the province refilled face-down");
        Check(live.Restrictions.Items.All(r => r.UntilTurn >= live.TurnNumber), "the expired lock was dropped");
    }

    private static CardDefinition CardWithAbility(string ability) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && !c.CreatedOnly && (c.Abilities ?? []).Contains(ability)).OrderBy(c => c.Id, StringComparer.Ordinal).First();

    private static void ActWhileBowed()
    {
        var thoughts = CardWithAbility("thoughts-of-wind-cavalry");
        MatchEngine Build(bool restricted, bool bowed)
        {
            var s = ActionTable();
            Batch336Support.Add(s, thoughts.Id, 0, Zone.Hand);
            var monk = Batch336Support.Add(s, Samurai, 0, Zone.Home, bowed: bowed); monk.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
            if (restricted) Stored(s, RestrictionKind.ActWhileBowed, monk.Id);
            return MatchEngine.FromTestState(s);
        }
        string StrategyId(MatchEngine e) => e.State.Cards.Values.First(c => c.DefinitionId == thoughts.Id).Id;
        var plain = Build(false, true);
        Check(!Batch336Support.CanUse(plain, 0, "thoughts-of-wind-cavalry", StrategyId(plain)), "a bowed Monk cannot perform an action that needs an unbowed performer");
        var e = Build(true, true);
        Check(Batch336Support.CanUse(e, 0, "thoughts-of-wind-cavalry", StrategyId(e)), "a bowed Monk that may act while bowed is offered as the performer");
        var option = Batch336Support.Enabled(e, 0, "thoughts-of-wind-cavalry", StrategyId(e));
        Check(e.Execute(option.Command!).Accepted, "and the action resolves");
        var unbowed = Build(true, false);
        Check(Batch336Support.CanUse(unbowed, 0, "thoughts-of-wind-cavalry", StrategyId(unbowed)), "an unbowed one is unaffected");

        // The exemption waives only the 'unbowed' requirement: a cost that bows the performer still needs him unbowed (Mendon's ruling for Kojinrue).
        var world = CardWithAbility("world-disappears-bow");
        var kiho = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && !c.CreatedOnly && c.Type == CardType.Strategy && (c.Keywords ?? []).Contains("Kiho"));
        MatchEngine BuildWorld(bool bowed)
        {
            var s = ActionTable();
            Batch336Support.Add(s, world.Id, 0, Zone.Hand); Batch336Support.Add(s, kiho.Id, 0, Zone.FateDiscard);
            var monk = Batch336Support.Add(s, Samurai, 0, Zone.Home, bowed: bowed); monk.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
            Stored(s, RestrictionKind.ActWhileBowed, monk.Id);
            return MatchEngine.FromTestState(s);
        }
        var ready = BuildWorld(false);
        Check(Batch336Support.CanUse(ready, 0, "world-disappears-bow", ready.State.Cards.Values.First(c => c.DefinitionId == world.Id).Id), "control: an unbowed Monk can pay 'bow your performing Monk'");
        var bowedMonk = BuildWorld(true);
        Check(!Batch336Support.CanUse(bowedMonk, 0, "world-disappears-bow", bowedMonk.State.Cards.Values.First(c => c.DefinitionId == world.Id).Id), "a bowed Monk cannot pay a cost that bows him, restriction or not");
    }

    private static AbilityDefinition Political() => new("Open", "Political test action", "personality", ActionKeywords: ["Political"]);
    private static AbilityDefinition Plain() => new("Open", "Plain test action", "personality");
    private static GameCommand Targeting(MatchEngine e, int player, string sourceId, string targetId) => new("useAbility", player, e.Version, sourceId, AbilityId: "test-ability", TargetId: targetId);
    private static bool Bars(MatchEngine e, AbilityDefinition ability, GameCommand command) => Throws(() => Call(e, "RequireRestrictionRulesAllow", command, ability));

    private static void TargetBars()
    {
        // Static, controller-only Political bar ("You may not target The Lost Colossus with Political actions"): Samurai in play carries it.
        CardRestrictionCatalog.RegisterTargetBars(Samurai, new StaticTargetBar(new ActorSpec("controller", "Political")));
        try
        {
            var s = ActionTable();
            var carrier = Batch336Support.Add(s, Samurai, 0, Zone.Home); var mine = Batch336Support.Add(s, Lion, 0, Zone.Home); var theirs = Batch336Support.Add(s, Lion, 1, Zone.Home);
            var inHand = Batch336Support.Add(s, Samurai, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(Bars(e, Political(), Targeting(e, 0, mine.Id, carrier.Id)), "its controller's Political action may not target it");
            Check(!Bars(e, Plain(), Targeting(e, 0, mine.Id, carrier.Id)), "a non-Political action of the controller may");
            Check(!Bars(e, Political(), Targeting(e, 1, theirs.Id, carrier.Id)), "another player's Political action may");
            Check(!Bars(e, Political(), Targeting(e, 0, mine.Id, inHand.Id)), "the static bar is read only while the card is in play");
        }
        finally { CardRestrictionCatalog.Clear(Samurai); }

        // Stored, others-only, Political, this phase ("Other players' actions may not target him this phase" shape, keyword-scoped).
        var t = ActionTable();
        var target = Batch336Support.Add(t, Samurai, 0, Zone.Home); var friend = Batch336Support.Add(t, Lion, 0, Zone.Home); var foe = Batch336Support.Add(t, Lion, 1, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        te.AddTargetBarRule(te.State.Cards[target.Id], new ActorSpec("others", "Political"), until: RestrictionUntil.Phase);
        Check(Bars(te, Political(), Targeting(te, 1, foe.Id, target.Id)), "another player's Political action may not target him");
        Check(!Bars(te, Political(), Targeting(te, 0, friend.Id, target.Id)) && !Bars(te, Plain(), Targeting(te, 1, foe.Id, target.Id)), "his controller's, and a non-Political one, may");
        var nextPhase = te.State; nextPhase.Phase = Phase.AttackDeclaration;
        Check(!Bars(MatchEngine.FromTestState(nextPhase), Political(), Targeting(te, 1, foe.Id, target.Id)), "a phase-long bar ends with the phase");
        var nextTurn = te.State; nextTurn.TurnNumber++;
        Check(!Bars(MatchEngine.FromTestState(nextTurn), Political(), Targeting(te, 1, foe.Id, target.Id)), "and with the turn");

        // Unit scope: actions from cards in a named Personality's unit may not target cards in this unit.
        var u = ActionTable();
        var head = Batch336Support.Add(u, Samurai, 0, Zone.Home); var attached = Batch336Support.Add(u, Follower, 0, Zone.Attached, parent: head.Id);
        var enemy = Batch336Support.Add(u, Lion, 1, Zone.Home); var enemyFollower = Batch336Support.Add(u, Follower, 1, Zone.Attached, parent: enemy.Id); var bystander = Batch336Support.Add(u, Veteran, 1, Zone.Home);
        var ue = MatchEngine.FromTestState(u);
        ue.AddTargetBarRule(ue.State.Cards[head.Id], new ActorSpec(UnitOfCardId: enemy.Id), targetUnit: true);
        Check(Bars(ue, Plain(), Targeting(ue, 1, enemy.Id, head.Id)) && Bars(ue, Plain(), Targeting(ue, 1, enemy.Id, attached.Id)), "the named unit's Personality may not target the head or his attachment");
        Check(Bars(ue, Plain(), Targeting(ue, 1, enemyFollower.Id, attached.Id)), "nor may a card attached to him");
        Check(!Bars(ue, Plain(), Targeting(ue, 1, bystander.Id, head.Id)), "a card outside that unit may");
        // Cards in other players' units only (an unattached Strategy is not "a card in a unit").
        ue.AddTargetBarRule(ue.State.Cards[head.Id], new ActorSpec("others", UnitCardsOnly: true));
        Check(Bars(ue, Plain(), Targeting(ue, 1, bystander.Id, head.Id)), "a Personality of another player may not target him once the unit-cards bar is added");
    }

    private static void ForcedTarget()
    {
        // Chiisai's Fear Battle names any enemy Personality with 5 or lower Force at the battlefield; two of them, one forced.
        var s = Batch336Support.Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var chiisai = Batch336Support.Add(s, "oracle-l5r-9803", 0, Zone.Battlefield, province: 0);
        var forced = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0); var other = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Batch336Support.Enabled(e, 0, "chiisai-charge", chiisai.Id).TargetIds!;
        Check(targets.Contains(forced.Id) && targets.Contains(other.Id), "control: both are legal targets");
        e.AddTargetBarRule(e.State.Cards[forced.Id], new ActorSpec(UnitOfCardId: chiisai.Id), forced: true);
        var narrowed = Batch336Support.Enabled(e, 0, "chiisai-charge", chiisai.Id).TargetIds!;
        Check(narrowed.Contains(forced.Id) && !narrowed.Contains(other.Id), "the offered targets narrow to the forced card");
        Batch336Support.Rejection(e, Batch336Support.Use(e, 0, "chiisai-charge", chiisai.Id, other.Id));
        Check(e.Execute(Batch336Support.Offer(e, 0, "chiisai-charge", chiisai.Id, forced.Id)).Accepted, "the forced card is a legal target");

        // When the forced card is not a legal target for the action (6 Force), another card may be named.
        var s2 = Batch336Support.Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var chiisai2 = Batch336Support.Add(s2, "oracle-l5r-9803", 0, Zone.Battlefield, province: 0);
        var strong = Batch336Support.Add(s2, Veteran, 1, Zone.Battlefield, province: 0); var weak = Batch336Support.Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        e2.AddTargetBarRule(e2.State.Cards[strong.Id], new ActorSpec(UnitOfCardId: chiisai2.Id), forced: true);
        var legal = Batch336Support.Enabled(e2, 0, "chiisai-charge", chiisai2.Id).TargetIds!;
        Check(legal.Contains(weak.Id) && !legal.Contains(strong.Id), "the forced card is not a legal target here");
        Check(e2.Execute(Batch336Support.Offer(e2, 0, "chiisai-charge", chiisai2.Id, weak.Id)).Accepted, "so the action may name the other card");
    }

    private static void SpellLimit()
    {
        var spellDef = CardCatalog.Cards.Values.Where(c => c.Type == CardType.Spell && !c.ScenarioOnly && !c.CreatedOnly && c.AttachRequiresKeyword is null && c.AttachExcludesKeyword is null && c.AttachRequiresName is null)
            .OrderBy(c => c.Id, StringComparer.Ordinal).First();
        var s = Batch336Support.Table(Phase.Battle, 1);
        var wielder = Batch336Support.Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var spell = Batch336Support.Add(s, spellDef.Id, 0, Zone.Attached, province: 0, parent: wielder.Id);
        s.Restrictions.Items.Add(new("limit-1", RestrictionKind.SpellLimit, wielder.Id, null, null, null, null, RestrictionUntil.Turn, s.TurnNumber, null, 1));
        var e = MatchEngine.FromTestState(s);
        var ability = new AbilityDefinition("Battle", "Spell test action", "none");
        var command = new GameCommand("useAbility", 0, e.Version, spell.Id, AbilityId: "test-ability");
        Check(!Throws(() => Call(e, "RequireSpellLimitAllows", command, ability)), "the first Spell action is allowed");
        Call(e, "NoteRestrictionAction", ability, command);
        Check(Throws(() => Call(e, "RequireSpellLimitAllows", command, ability)), "the second is refused at a limit of one");
        var notSpell = new GameCommand("useAbility", 0, e.Version, wielder.Id, AbilityId: "test-ability");
        Check(!Throws(() => Call(e, "RequireSpellLimitAllows", notSpell, ability)), "an action that is not a Spell action is not counted");
    }
}
