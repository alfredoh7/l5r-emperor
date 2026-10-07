using L5R.Core;
using static RegressionAssertions;

// Issue #690, primitive P24 (EffectPrimitivesCosts.cs): the cost-substitution store, the cost-waiver channel and the cost-card categories. Fixture abilities and per-instance printed
// values only: no catalogue card is wired by this primitive.
internal static class EffectPrimitivesCostTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    public static void Run(TestRunner runner) => runner.Run("p24-costs", new (string, Action)[]
    {
        ("Substitution: a Ring's own bow cost is offered with the unbowed holder bowed instead; the holder bows, the Ring does not, and the action resolves", SubstituteForRingsOwnCost),
        ("Substitution is offered only for an unbowed holder with the trait, and a command naming any other card is rejected unchanged", SubstituteOnlyWhenAble),
        ("Substitution: a Ring bowed for another card's cost may be replaced too; an action that bows no Ring takes no substitute", SubstituteForAnotherCardsCost),
        ("Substitution: the substitute is the card that pays -- a negation of ITS bowing blocks the cost, one of the Ring's does not", SubstituteIsTheCardThatPays),
        ("Substitution grants: permanent or for the turn, kept across a saved game, one per card", SubstitutionGrants),
        ("Waiver: 'need not bow for his Spells with an odd Focus Value' -- the Shugenja is not bowed and need not be unbowed; an even Spell still bows him (0 is even)", FocusParityWaiver),
        ("Remove N [keyword] cards you control: exactly N of the acting player's own in-play cards with the keyword, removed as a cost even when the action is negated", RemoveKeywordCards),
        ("Remove a card in any discard pile: either player's piles, a dead Personality's included, never the excepted type; removed from the game as the cost", RemoveFromAnyDiscard),
        ("Destroy one of his Spells: only an attachment of that type on the performing Personality; destroyed as the cost; a blocked destruction negates the action", DestroyOwnAttachment),
        ("Kitsuki Judgment's price needs nothing new: the unit's total Gold Cost, +8 by a cost term unless he is dishonorable", JudgmentPrice),
    });

    private static void Grant(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static void Settle(MatchEngine e) { for (var i = 0; i < 6 && e.State.PendingChoice is not null; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static CardInstance Now(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];

    // ---- substitution ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string Draw = "ring-of-void-draw";

    // Player 1's Action Phase: his Ring of the Void in play and a Monk at home who holds Harmony's trait (unless `trait` is false).
    private static (MatchEngine E, CardInstance Ring, CardInstance Monk) RingRig(bool trait = true, bool monkBowed = false, Action<MatchState>? build = null)
    {
        var s = Batch336Support.Table(Phase.Action);
        var ring = Add(s, "training-ring-of-void", 1, Zone.Home);
        var monk = Add(s, "training-monk", 1, Zone.Home, bowed: monkBowed);
        build?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        if (trait) e.GrantBowInsteadOfRing(e.CardForTest(monk.Id), "Harmony");
        return (e, ring, monk);
    }

    private static GameCommand Substituted(MatchEngine e, string ability, string cardId, CardInstance holder, string? targetId = null) =>
        Batch336Support.Use(e, 1, ability, cardId, targetId) with { CostOption = CostSubstitution.Option(holder.Id) };

    private static void SubstituteForRingsOwnCost()
    {
        var (e, ring, monk) = RingRig();
        var options = Batch336Support.Options(e, 1, Draw, ring.Id);
        Eq(options.Length, 2, "two ways to take the Ring's action: " + string.Join("; ", options.Select(o => o.Label)));
        var substitute = options.Single(o => o.Command?.CostOption == CostSubstitution.Option(monk.Id));
        Check(substitute.Enabled && substitute.Label.EndsWith("Bow Training monk instead of the Ring", StringComparison.Ordinal), "the substitution is offered, labelled: " + substitute.Label);
        Eq(substitute.AlternativeCost?.Id, CostSubstitution.Option(monk.Id), "the option says which way of paying it is");
        Check(options.Any(o => o.Enabled && o.Command?.CostOption is null), "and the ordinary payment is still offered");

        var deck = e.State.Players[1].FateDeck.Count;
        Accept(e, substitute.Command!); Settle(e);
        Check(Now(e, monk).Bowed, "the Monk was bowed");
        Check(!Now(e, ring).Bowed, "the Ring was not");
        Eq(e.State.Players[1].FateDeck.Count, deck - 1, "the action resolved: a card was drawn");
        Check(e.State.Events.Any(x => x.Type == "costSubstituted" && x.CardId == monk.Id), "the substitution is logged");

        // The ordinary payment, for comparison: the Ring bows and the Monk does not.
        var (plain, ring2, monk2) = RingRig();
        Accept(plain, Batch336Support.Options(plain, 1, Draw, ring2.Id).Single(o => o.Command?.CostOption is null).Command!); Settle(plain);
        Check(Now(plain, ring2).Bowed && !Now(plain, monk2).Bowed, "without the substitution the Ring bows");
    }

    private static void SubstituteOnlyWhenAble()
    {
        // A bowed holder: not offered, and a command naming him is rejected with nothing changed.
        var (bowed, ring, monk) = RingRig(monkBowed: true);
        Check(Batch336Support.Options(bowed, 1, Draw, ring.Id).All(o => o.Command?.CostOption is null), "a bowed holder is not offered");
        Reject(bowed, Substituted(bowed, Draw, ring.Id, monk));

        // No trait: not offered, rejected.
        var (none, ring2, monk2) = RingRig(trait: false);
        Eq(Batch336Support.Options(none, 1, Draw, ring2.Id).Length, 1, "without the trait there is one way to pay");
        Reject(none, Substituted(none, Draw, ring2.Id, monk2));

        // Another player's holder, a card that does not exist, and the Ring itself.
        CardInstance enemyMonk = null!;
        var (other, ring3, _) = RingRig(build: s => enemyMonk = Add(s, "training-monk", 0, Zone.Home));
        other.GrantBowInsteadOfRing(other.CardForTest(enemyMonk.Id), "Harmony");
        Check(Batch336Support.Options(other, 1, Draw, ring3.Id).All(o => o.Command?.CostOption != CostSubstitution.Option(enemyMonk.Id)), "another player's holder is not offered");
        Reject(other, Substituted(other, Draw, ring3.Id, enemyMonk));
        Reject(other, Batch336Support.Use(other, 1, Draw, ring3.Id) with { CostOption = CostSubstitution.Option("no-such-card") });
        Reject(other, Substituted(other, Draw, ring3.Id, ring3));

        // A bowed Ring cannot take its action at all: the trait replaces the payment, not the rule that a bowed card's ability is not used.
        var (ringBowed, ring4, monk4) = RingRig(build: s => s.Cards.Values.First(c => c.DefinitionId == "training-ring-of-void" && c.OwnerId == 1).Bowed = true);
        Reject(ringBowed, Substituted(ringBowed, Draw, ring4.Id, monk4));
    }

    private static void SubstituteForAnotherCardsCost()
    {
        CardInstance user = null!;
        var (e, ring, monk) = RingRig(build: s => { user = Add(s, "training-lion-samurai", 1, Zone.Home); Grant(user, EffectPrimitiveFixtures.RingTargetBow); });
        var options = Batch336Support.Options(e, 1, EffectPrimitiveFixtures.RingTargetBow, user.Id);
        Check(options.Any(o => o.Enabled && o.Command?.CostOption == CostSubstitution.Option(monk.Id)), "a 'bow your target Ring' cost is offered with the substitute: " + string.Join("; ", options.Select(o => o.Label)));
        var honor = Honor(e, 1);
        Accept(e, Substituted(e, EffectPrimitiveFixtures.RingTargetBow, user.Id, monk, ring.Id)); Settle(e);
        Check(Now(e, monk).Bowed && !Now(e, ring).Bowed, "the Monk bowed in the target Ring's place");
        Eq(Honor(e, 1), honor + 1, "and the action resolved");

        // An action that bows no Ring: a Spell that bows its Shugenja.
        CardInstance spell = null!;
        var (noRing, _, monk2) = RingRig(build: s =>
        {
            var shugenja = Add(s, "training-shugenja", 1, Zone.Home);
            spell = Add(s, "training-spell", 1, Zone.Attached, parent: shugenja.Id); Grant(spell, EffectPrimitiveFixtures.SpellBowWielder);
        });
        Check(Batch336Support.Options(noRing, 1, EffectPrimitiveFixtures.SpellBowWielder, spell.Id).All(o => o.Command?.CostOption is null), "no substitution is offered for a cost that bows no Ring");
        Reject(noRing, Substituted(noRing, EffectPrimitiveFixtures.SpellBowWielder, spell.Id, monk2));
    }

    private static void SubstituteIsTheCardThatPays()
    {
        // A negation of the Monk's bowing blocks the payment: the whole action is negated and nothing is paid.
        var (blocked, ring, monk) = RingRig();
        blocked.CostBlockHook = (cost, subject) => subject?.Id == monk.Id;
        var deck = blocked.State.Players[1].FateDeck.Count;
        Accept(blocked, Substituted(blocked, Draw, ring.Id, monk)); Settle(blocked);
        Check(!Now(blocked, monk).Bowed && !Now(blocked, ring).Bowed, "nothing was bowed");
        Eq(blocked.State.Players[1].FateDeck.Count, deck, "and nothing resolved");

        // A negation of the RING's bowing is beside the point: the Ring is not what is bowed.
        var (free, ring2, monk2) = RingRig();
        free.CostBlockHook = (cost, subject) => subject?.Id == ring2.Id;
        var deck2 = free.State.Players[1].FateDeck.Count;
        Accept(free, Substituted(free, Draw, ring2.Id, monk2)); Settle(free);
        Check(Now(free, monk2).Bowed && !Now(free, ring2).Bowed, "the Monk paid");
        Eq(free.State.Players[1].FateDeck.Count, deck2 - 1, "and the action resolved");
        // Without a substitute the same negation does block the Ring's own payment.
        var (ordinary, ring3, _) = RingRig();
        ordinary.CostBlockHook = (cost, subject) => subject?.Id == ring3.Id;
        var deck3 = ordinary.State.Players[1].FateDeck.Count;
        Accept(ordinary, Batch336Support.Use(ordinary, 1, Draw, ring3.Id)); Settle(ordinary);
        Eq(ordinary.State.Players[1].FateDeck.Count, deck3, "the Ring's own blocked bow negates the action");
    }

    private static void SubstitutionGrants()
    {
        var (e, ring, monk) = RingRig();
        Check(e.MayBowInsteadOfRing(e.CardForTest(monk.Id)) && !e.MayBowInsteadOfRing(e.CardForTest(ring.Id)), "the holder has the trait and no other card does");
        e.GrantBowInsteadOfRing(e.CardForTest(monk.Id), "Harmony");
        Eq(e.State.EffectPrimitives.CostSubstitutions.Count, 1, "the same grant twice is one grant");
        var reloaded = MatchEngine.FromTestState(e.State);
        Check(reloaded.MayBowInsteadOfRing(reloaded.CardForTest(monk.Id)), "the grant survives a saved game");
        var later = Batch336Support.Edit(e, st => st.TurnNumber += 3);
        Check(later.MayBowInsteadOfRing(later.CardForTest(monk.Id)), "a permanent grant lasts");

        var (turn, _, monk2) = RingRig(trait: false);
        turn.GrantBowInsteadOfRing(turn.CardForTest(monk2.Id), "test", GrantDuration.Turn);
        Check(turn.MayBowInsteadOfRing(turn.CardForTest(monk2.Id)), "a turn grant holds this turn");
        var next = Batch336Support.Edit(turn, st => st.TurnNumber++);
        Check(!next.MayBowInsteadOfRing(next.CardForTest(monk2.Id)), "and not the next");
    }

    // ---- the waiver ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1's Shugenja at home with a Spell of the given Focus Value that prints "Bow this Shugenja: gain 1 Honor", and (optionally) an Item whose wearer need not bow for odd / even Spells.
    private static (MatchEngine E, CardInstance Shugenja, CardInstance Spell) SpellRig(int focus, string? waiver, bool shugenjaBowed = false)
    {
        var s = Batch336Support.Table(Phase.Action);
        var shugenja = Add(s, "training-shugenja", 1, Zone.Home, bowed: shugenjaBowed);
        var spell = Add(s, "training-spell", 1, Zone.Attached, parent: shugenja.Id);
        spell.Printed = CardCatalog.Get("training-spell") with { Focus = focus, Abilities = [EffectPrimitiveFixtures.SpellBowWielder] };
        if (waiver is not null) Add(s, "training-item", 1, Zone.Attached, parent: shugenja.Id).Printed = CardCatalog.Get("training-item") with { Effect = waiver };
        return (MatchEngine.FromTestState(s), shugenja, spell);
    }

    private static bool Cast(MatchEngine e, CardInstance spell)
    {
        var honor = Honor(e, 1);
        var result = e.Execute(Batch336Support.Use(e, 1, EffectPrimitiveFixtures.SpellBowWielder, spell.Id));
        if (!result.Accepted) return false;
        Settle(e);
        Eq(Honor(e, 1), honor + 1, "the Spell's action resolved");
        return true;
    }

    private static void FocusParityWaiver()
    {
        var (odd, shugenja, spell) = SpellRig(3, EffectPrimitiveFixtures.OddFocusWaiver);
        Check(Cast(odd, spell) && !Now(odd, shugenja).Bowed, "an odd Spell: he is not bowed");
        var (oddBowed, _, spell2) = SpellRig(3, EffectPrimitiveFixtures.OddFocusWaiver, shugenjaBowed: true);
        Check(Cast(oddBowed, spell2), "and he need not be unbowed");

        var (even, shugenja3, spell3) = SpellRig(2, EffectPrimitiveFixtures.OddFocusWaiver);
        Check(Cast(even, spell3) && Now(even, shugenja3).Bowed, "an even Spell under the odd waiver: he bows as printed");
        var (evenBowed, _, spell4) = SpellRig(2, EffectPrimitiveFixtures.OddFocusWaiver, shugenjaBowed: true);
        Reject(evenBowed, Batch336Support.Use(evenBowed, 1, EffectPrimitiveFixtures.SpellBowWielder, spell4.Id));

        var (zero, shugenja5, spell5) = SpellRig(0, EffectPrimitiveFixtures.EvenFocusWaiver);
        Check(Cast(zero, spell5) && !Now(zero, shugenja5).Bowed, "0 is even: the even waiver covers a Focus Value of 0");
        var (zeroOdd, shugenja6, spell6) = SpellRig(0, EffectPrimitiveFixtures.OddFocusWaiver);
        Check(Cast(zeroOdd, spell6) && Now(zeroOdd, shugenja6).Bowed, "and the odd waiver does not");

        var (none, shugenja7, spell7) = SpellRig(3, null);
        Check(Cast(none, spell7) && Now(none, shugenja7).Bowed, "without the Item he bows");
        var (noneBowed, _, spell8) = SpellRig(3, null, shugenjaBowed: true);
        Reject(noneBowed, Batch336Support.Use(noneBowed, 1, EffectPrimitiveFixtures.SpellBowWielder, spell8.Id));

        // A Focus Value raised by an effect is the value judged (3 + 1 is even).
        var (raised, shugenja9, spell9) = SpellRig(3, EffectPrimitiveFixtures.OddFocusWaiver);
        var modified = Batch336Support.Edit(raised, st => st.Cards[spell9.Id].Modifiers.Add(new("test-source", "test", "Focus", 1)));
        Check(Cast(modified, spell9) && Now(modified, shugenja9).Bowed, "the Spell's current Focus Value decides");
    }

    // ---- cost cards ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Koan(MatchState s, int owner, string hostId)
    {
        var card = Add(s, "training-item", owner, Zone.Attached, parent: hostId);
        card.Printed = CardCatalog.Get("training-item") with { Keywords = ["Koan"] };
        return card;
    }

    private static void RemoveKeywordCards()
    {
        MatchEngine Rig(out CardInstance source, out CardInstance[] koans, out CardInstance enemyKoan, out CardInstance plain)
        {
            var s = Batch336Support.Table(Phase.Action);
            source = Add(s, "training-lion-samurai", 1, Zone.Home); Grant(source, EffectPrimitiveFixtures.RemoveKoan);
            var other = Add(s, "training-lion-courtier", 1, Zone.Home);
            koans = [Koan(s, 1, source.Id), Koan(s, 1, other.Id), Koan(s, 1, other.Id)];
            plain = Add(s, "training-item", 1, Zone.Attached, parent: source.Id);
            enemyKoan = Koan(s, 0, Add(s, "training-crane-samurai", 0, Zone.Home).Id);
            return MatchEngine.FromTestState(s);
        }

        var e = Rig(out var source, out var koans, out var enemyKoan, out var plain);
        var option = Batch336Support.Enabled(e, 1, EffectPrimitiveFixtures.RemoveKoan, source.Id);
        var slot = option.Slots!.Single(sl => sl.Slot == "p24-remove");
        Check(slot.Min == 2 && slot.Max == 2, "exactly two cards are asked for");
        Check(slot.CardIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(koans.Select(k => k.Id).OrderBy(x => x, StringComparer.Ordinal)), "the candidates are the acting player's own Koan cards in play: " + string.Join(",", slot.CardIds));
        var honor = Honor(e, 1);
        Accept(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.RemoveKoan, source.Id, null, null, Pick("p24-remove", koans[0].Id, koans[2].Id))); Settle(e);
        Check(Now(e, koans[0]).Zone == Zone.RemovedFromGame && Now(e, koans[2]).Zone == Zone.RemovedFromGame, "the two chosen cards were removed from the game");
        Eq(Now(e, koans[1]).Zone, Zone.Attached, "the third stays");
        Eq(Honor(e, 1), honor + 1, "the action resolved");

        var r = Rig(out var source2, out var koans2, out var enemyKoan2, out var plain2);
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.RemoveKoan, source2.Id, null, null, Pick("p24-remove", koans2[0].Id)));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.RemoveKoan, source2.Id, null, null, Pick("p24-remove", koans2[0].Id, koans2[1].Id, koans2[2].Id)));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.RemoveKoan, source2.Id, null, null, Pick("p24-remove", koans2[0].Id, enemyKoan2.Id)));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.RemoveKoan, source2.Id, null, null, Pick("p24-remove", koans2[0].Id, plain2.Id)));
        Reject(r, Batch336Support.Use(r, 1, EffectPrimitiveFixtures.RemoveKoan, source2.Id, null, null, Pick("p24-remove", koans2[0].Id, koans2[0].Id)));

        // A cost is paid even when the action's effects are negated.
        var n = Rig(out var source3, out var koans3, out _, out _);
        n.NegateEveryAction = true;
        var honor3 = Honor(n, 1);
        Accept(n, Batch336Support.Use(n, 1, EffectPrimitiveFixtures.RemoveKoan, source3.Id, null, null, Pick("p24-remove", koans3[0].Id, koans3[1].Id))); Settle(n);
        Check(Now(n, koans3[0]).Zone == Zone.RemovedFromGame && Now(n, koans3[1]).Zone == Zone.RemovedFromGame, "the cost was paid");
        Eq(Honor(n, 1), honor3, "and the negated effect did not happen");
        Check(enemyKoan.OwnerId == 0 && plain.OwnerId == 1, "fixture sanity");
    }

    private static void RemoveFromAnyDiscard()
    {
        var s = Batch336Support.Table(Phase.Action);
        var source = Add(s, "training-lion-samurai", 1, Zone.Home); Grant(source, EffectPrimitiveFixtures.RemoveDiscard);
        var enemyStrategy = Add(s, "training-rally", 0, Zone.FateDiscard);
        var ownStrategy = Add(s, "training-rally", 1, Zone.FateDiscard);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);
        var ring = Add(s, "training-ring-of-void", 1, Zone.FateDiscard);
        var inPlay = Add(s, "training-item", 1, Zone.Attached, parent: source.Id);
        var e = MatchEngine.FromTestState(s);
        var slot = Batch336Support.Enabled(e, 1, EffectPrimitiveFixtures.RemoveDiscard, source.Id).Slots!.Single(sl => sl.Slot == "p24-discard");
        Check(new[] { enemyStrategy.Id, ownStrategy.Id, dead.Id }.All(slot.CardIds.Contains), "either player's discard piles are offered, a dead Personality included: " + string.Join(",", slot.CardIds));
        Check(!slot.CardIds.Contains(ring.Id), "a Ring is not");
        Check(slot.CardIds.All(id => e.State.Cards[id].Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead), "and nothing outside a discard pile is");
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.RemoveDiscard, source.Id, null, null, Pick("p24-discard", ring.Id), Pick("target", source.Id)));
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.RemoveDiscard, source.Id, null, null, Pick("p24-discard", inPlay.Id), Pick("target", source.Id)));
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.RemoveDiscard, source.Id, null, null, Pick("target", source.Id)));
        var force = e.CardForceForTest(source.Id);
        Accept(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.RemoveDiscard, source.Id, null, null, Pick("p24-discard", enemyStrategy.Id), Pick("target", source.Id))); Settle(e);
        Eq(Now(e, enemyStrategy).Zone, Zone.RemovedFromGame, "the chosen card in the other player's discard pile was removed from the game");
        Eq(Now(e, ownStrategy).Zone, Zone.FateDiscard, "the others stay");
        Eq(e.CardForceForTest(source.Id), force + 3, "and the action resolved on its target");

        // The category's narrowing terms.
        Eq(EffectPrimitiveTargets.CardInAnyDiscard(), "p24-any-discard");
        Eq(EffectPrimitiveTargets.CardInAnyDiscard("Koan", CardType.Ring, ownOnly: true), "p24-any-discard;keyword=Koan;not-type=Ring;own");
    }

    private static void DestroyOwnAttachment()
    {
        MatchEngine Rig(out CardInstance shugenja, out CardInstance[] spells, out CardInstance follower, out CardInstance othersSpell)
        {
            var s = Batch336Support.Table(Phase.Action);
            shugenja = Add(s, "training-shugenja", 1, Zone.Home); Grant(shugenja, EffectPrimitiveFixtures.DestroyOwnSpell);
            spells = [Add(s, "training-spell", 1, Zone.Attached, parent: shugenja.Id), Add(s, "training-spell", 1, Zone.Attached, parent: shugenja.Id)];
            follower = Add(s, "training-follower", 1, Zone.Attached, parent: shugenja.Id);
            othersSpell = Add(s, "training-spell", 1, Zone.Attached, parent: Add(s, "training-shugenja", 1, Zone.Home).Id);
            return MatchEngine.FromTestState(s);
        }

        var e = Rig(out var shugenja, out var spells, out var follower, out var othersSpell);
        var slot = Batch336Support.Enabled(e, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja.Id).Slots!.Single(sl => sl.Slot == "p24-attachment");
        Check(slot.CardIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(spells.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)), "only his own Spells are offered: " + string.Join(",", slot.CardIds));
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja.Id, null, null, Pick("p24-attachment", follower.Id)));
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja.Id, null, null, Pick("p24-attachment", othersSpell.Id)));
        Reject(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja.Id));
        var force = e.CardForceForTest(shugenja.Id);
        Accept(e, Batch336Support.Use(e, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja.Id, null, null, Pick("p24-attachment", spells[1].Id))); Settle(e);
        Eq(Now(e, spells[1]).Zone, Zone.FateDiscard, "the chosen Spell was destroyed");
        Eq(Now(e, spells[0]).Zone, Zone.Attached, "the other is still attached");
        Eq(e.CardForceForTest(shugenja.Id), force + 2, "and the action resolved");

        // A destruction paid as a cost can be negated; the whole action is then negated and nothing is paid.
        var b = Rig(out var shugenja2, out var spells2, out _, out _);
        b.CostBlockHook = (cost, subject) => subject?.Id == spells2[0].Id;
        var force2 = b.CardForceForTest(shugenja2.Id);
        Accept(b, Batch336Support.Use(b, 1, EffectPrimitiveFixtures.DestroyOwnSpell, shugenja2.Id, null, null, Pick("p24-attachment", spells2[0].Id))); Settle(b);
        Eq(Now(b, spells2[0]).Zone, Zone.Attached, "the Spell whose destruction was negated stays");
        Eq(b.CardForceForTest(shugenja2.Id), force2, "and the action did not resolve");
    }

    private static void JudgmentPrice()
    {
        var s = Batch336Support.Table(Phase.Action);
        var magistrate = Add(s, "training-magistrate-personality", 1, Zone.Home); Grant(magistrate, EffectPrimitiveFixtures.Judgment);
        var honorable = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-follower", 0, Zone.Attached, parent: honorable.Id);
        var dishonorable = Add(s, "training-crane-samurai", 0, Zone.Home); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var unit = CardCatalog.Get("training-crane-samurai").GoldCost + CardCatalog.Get("training-follower").GoldCost;
        var price = e.QuoteAbilityGold(Batch336Support.Use(e, 1, EffectPrimitiveFixtures.Judgment, magistrate.Id, honorable.Id));
        Eq(price?.Cost, unit + 8, "an honorable target: his unit's total Gold Cost, +8");
        Check((price?.Basis ?? "").Contains("+ 8 (he is not dishonorable)"), "the basis names the term: " + price?.Basis);
        Eq(e.QuoteAbilityGold(Batch336Support.Use(e, 1, EffectPrimitiveFixtures.Judgment, magistrate.Id, dishonorable.Id))?.Cost, CardCatalog.Get("training-crane-samurai").GoldCost, "a dishonorable target: his unit's total Gold Cost alone");
    }
}
