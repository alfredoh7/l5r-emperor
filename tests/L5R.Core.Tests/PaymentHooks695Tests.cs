using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Primitive P1 (issue #695, PaymentHooks695.cs): the payment pipeline's hook channels and stores, proved on fixtures only (no card is wired). Every fixture is a per-instance definition (CardInstance.Printed) whose
// id starts with "training-p1-"; the test hooks below are inert for any card that is not one of those fixtures, so no other suite sees them.
internal static class PaymentHooks695Tests
{
    private const string Copse = "training-p1-copse", Truce = "training-p1-truce", BowItem = "training-p1-bow-item", Dojo = "training-p1-dojo", Chambers = "training-p1-chambers",
        Subject = "training-p1-subject", Keep = "training-border-keep", Crane = "training-crane-samurai", Lion = "training-lion-samurai";
    private static string AdjustMode = "";
    private static bool Recording;
    private static readonly List<string> Seen = [];

    public static void Run(TestRunner runner) => runner.Run("payment-hooks-695", [
        ("The moved sources (Zlkyt, Rani, Jade Mine, Slave Pits, Coin Pouch, Near Miss, Profits, Bamboo) and the three entry records are registered on the new channels", MovedRegistrations),
        ("A token that requires other sources: refused alone with no state change, accepted beside a Holding, bowed by the payment, never suggested without it", RequiresOtherSources),
        ("A bowing token is not also its card's own bow; it conflicts with its card in the quote, and a bowed card offers none", BowsOnce),
        ("An Attached card may offer a token (a bowing Item), and a token in the Hand needs the zone named: it is the payer's own hand only", AttachedAndHandZones),
        ("A hand token discards its card as it pays, is offered only for a Personality of another Clan, and the other seat never sees it", HandToken),
        ("A production condition hook decides a Custom condition and a conditional amount is a function of the payment subject (enters play for 0 Gold; 4 instead of 2 for a Unique)", ConditionAndAmount),
        ("A production adjustment applies to the Stronghold and to a Holding that enters later; a player-level turn modifier ends with the turn and honours its scope", AdjustmentAndTurnModifier),
        ("A lent Holding is a producer for the borrower only, until end of turn, priced for the borrower, bowed by the borrower's payment", LentProducers),
        ("After a payment: the hooks see the cost, produced Gold, subject and sources; the ledger counts the Gold spent each phase and the cards that produced this turn", AfterPaymentLedger),
        ("An Attached card with a production rule and an in-play Strategy with one are producers; a Strategy in hand, or one without a rule, is not", WiderProducers),
        ("Every new store survives serialization and a saved game continues exactly like the original", SaveAndReplay),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardDefinition Def(CardInstance c) => c.Printed ?? CardCatalog.Get(c.DefinitionId);

    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null)
    {
        var card = Batch336Support.Add(s, baseId, owner, zone, bowed, null, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }

    // ---- the test hooks -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.PaymentTokens.Register("TestP1Tokens", 19300, static (engine, context) => TestTokens(engine, context));
        EngineHooks.ProductionCondition.Register("TestP1Condition", 19300, static (engine, context) => TestCondition(engine, context));
        EngineHooks.ConditionalProductions.Register("TestP1ConditionalProductions", 19300, static (_, card) => TestProduction(card));
        EngineHooks.ProductionAdjustment.Register("TestP1Adjustment", 19300, static (_, context) => TestAdjustment(context));
        EngineHooks.AfterPayment.Register("TestP1After", 19300, static (_, context) =>
        {
            if (Recording) Seen.Add($"{context.PlayerId}|{context.Cost}|{context.Produced}|{context.Subject?.Card.Id}|{string.Join(",", context.SourceIds)}|{string.Join(",", context.ProducerIds)}");
        });
    }

    private static bool OtherHolding(MatchEngine e, string self, string[] ids) =>
        ids.Any(id => id != self && !id.Contains('#') && Def(e.P22CardForTest(id)).Type == CardType.Holding);

    private static IEnumerable<MatchEngine.PaymentToken> TestTokens(MatchEngine engine, PaymentTokenContext context)
    {
        foreach (var card in engine.LiveCardsForTest.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var def = Def(card);
            if (def.Effect == Copse && context.Paying is { Kind: not PaymentKind.Action } copseFor && engine.HasKeyword(copseFor.Card.Id, "Nonhuman"))
                yield return new(card.Id + "#copse", card, Copse, 3, "bow-reaction", "Test Copse: bow it, produce 3 Gold", null, Bows: true,
                    RequiresOtherSources: (e, _, ids) => OtherHolding(e, card.Id, ids), RequirementMessage: "Test Copse needs another Holding in the payment.");
            if (def.Effect == BowItem && card.Zone == Zone.Attached)
                yield return new(card.Id + "#bowitem", card, BowItem, 1, "bow-reaction", "Test Item: bow it, produce 1 Gold", null, Bows: true,
                    RequiresOtherSources: (e, _, ids) => OtherHolding(e, card.Id, ids));
            if (def.Effect == Truce && context.Paying is { Kind: PaymentKind.Recruit } truceFor && Def(truceFor.Card).Type == CardType.Personality && Def(truceFor.Card).Clan != engine.ClanOfForTest(card.OwnerId))
                yield return new(card.Id + "#truce", card, Truce, 4, "hand", "Test Truce: discard it from your hand, produce 4 Gold", null,
                    OnUse: static (e, token, player) => e.PaymentDiscard(token.Card, player), AllowedZones: [Zone.Hand]);
            if (def.Effect == Copse && card.Zone == Zone.Hand) // no AllowedZones: the Hand is never assumed
                yield return new(card.Id + "#handcopse", card, Copse, 3, "hand", "never offered", null);
        }
    }

    private static bool? TestCondition(MatchEngine engine, ProductionConditionContext context)
    {
        if (context.Production.When != ProductionCondition.Custom) return null;
        return context.Production.Keyword switch
        {
            "p1-nonunique-samurai" => context.Paying is { Kind: PaymentKind.Recruit } p && engine.HasKeyword(p.Card.Id, "Samurai") && !engine.HasKeyword(p.Card.Id, "Unique"),
            "p1-personality" => context.Paying is { Kind: PaymentKind.Recruit } p && Def(p.Card).Type == CardType.Personality,
            _ => null
        };
    }

    private static ConditionalProduction? TestProduction(CardInstance card) => Def(card).Effect switch
    {
        Dojo => new(0, ProductionCondition.Custom, "p1-nonunique-samurai", AmountOf: static (_, subject) => Def(subject.Card).GoldCost),
        Chambers => new(2, ProductionCondition.Custom, "p1-personality", AmountOf: static (e, subject) => e.HasKeyword(subject.Card.Id, "Unique") ? 4 : 2),
        _ => null
    };

    private static int TestAdjustment(ProductionAdjustmentContext context) => AdjustMode switch
    {
        "stronghold" when Def(context.Card).Type == CardType.Stronghold => 2,
        "borrower" when context.PayerId != context.Card.OwnerId => 1,
        _ => 0
    };

    // ---- rigs -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Dynasty phase, player 0 (Crane) active, every printed producer of player 0 bowed (so the only Gold is what a test adds), and province 0 holds a face-up fixture Personality.
    private static (MatchState State, CardInstance Province) Rig(Func<CardDefinition, CardDefinition>? tweak = null, string baseId = Crane)
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var province = s.Cards[s.Players[0].Provinces[0].CardId!];
        province.DefinitionId = baseId; province.FaceUp = true;
        var def = CardCatalog.Get(baseId) with { Id = Subject, Keywords = ["Samurai"] };
        province.Printed = tweak is null ? def : tweak(def);
        return (s, province);
    }

    private static GameCommand Purchase(MatchEngine e, CardInstance province, params string[] sources) => new("purchase", 0, e.Version, CardId: province.Id, SourceIds: sources);
    private static bool Entered(MatchEngine e, CardInstance province) => e.State.Cards[province.Id].Zone == Zone.Home;

    // ---- tests ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MovedRegistrations()
    {
        var names = EngineHooks.PaymentTokens.Names;
        foreach (var expected in new[] { "P1MechEPaymentTokens", "P1TfPaymentTokens", "P1PromoPaymentTokens", "P1WlPaymentTokens" }) Check(names.Contains(expected), $"{expected} is on the channel");
        var after = EngineHooks.AfterPayment.Names;
        foreach (var expected in new[] { "P1AfterPaymentMoneylender", "P1AfterPaymentVastPaddy", "P1AfterPaymentEntry", "P1PaymentLedger" }) Check(after.Contains(expected), $"{expected} is on the channel");
        // The order the sources used to be listed in is kept: Moto Rani and Zlkyt, then the two Temple Fortress kinds, then the promotional kinds, then Bamboo.
        Check(Array.IndexOf([.. names], "P1MechEPaymentTokens") < Array.IndexOf([.. names], "P1TfPaymentTokens") && Array.IndexOf([.. names], "P1TfPaymentTokens") < Array.IndexOf([.. names], "P1PromoPaymentTokens")
            && Array.IndexOf([.. names], "P1PromoPaymentTokens") < Array.IndexOf([.. names], "P1WlPaymentTokens"), "the migrated tokens keep their old order");
    }

    private static void RequiresOtherSources()
    {
        // One other Holding (2) and the Copse (3) pay a 3 Gold Nonhuman: the only payment is both.
        var (s, province) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        var keep = Batch336Support.Add(s, Keep, 0, Zone.Home);
        var copse = Fx(s, Keep, Copse, 0, Zone.Home, d => d with { Effect = Copse, GoldProduction = 0 });
        var e = MatchEngine.FromTestState(s);
        var token = copse.Id + "#copse";
        var quote = e.QuotePurchase(0, province.Id);
        Eq(quote.Cost, 3, "the price");
        Check(quote.Sources!.Any(x => x.Id == token && x.Kind == "bow-reaction" && x.Produced == 3), "the Copse is listed among the payment sources, as a bow-reaction");
        Check(quote.Payments.Length > 0 && quote.Payments.All(p => !p.SourceIds.Contains(token) || p.SourceIds.Contains(keep.Id)), "no suggestion names the Copse without another Holding");
        Check(quote.Payments.Any(p => p.SourceIds.Contains(token)), "and the suggestion that pays does name it: " + string.Join(" | ", quote.Payments.Select(p => string.Join(",", p.SourceIds))));
        Reject(e, Purchase(e, province, token));
        Check(!e.State.Cards[copse.Id].Bowed && !Entered(e, province), "the refusal changed nothing");
        Accept(e, Purchase(e, province, keep.Id, token));
        Check(Entered(e, province), "the Personality entered play");
        Check(e.State.Cards[copse.Id].Bowed && e.State.Cards[keep.Id].Bowed, "the payment bowed the Holding and the Copse");

        // With no other Holding the Copse alone is never suggested and never accepted.
        var (a, aprovince) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        var acopse = Fx(a, Keep, Copse, 0, Zone.Home, d => d with { Effect = Copse, GoldProduction = 0 });
        var alone = MatchEngine.FromTestState(a);
        Check(alone.QuotePurchase(0, aprovince.Id).Payments.Length == 0, "nothing pays: the Copse needs another Holding");
        Reject(alone, Purchase(alone, aprovince, acopse.Id + "#copse"));

        // Not for a card that is not a Nonhuman.
        var (h, hprovince) = Rig(d => d with { GoldCost = 3 });
        Batch336Support.Add(h, Keep, 0, Zone.Home); Fx(h, Keep, Copse, 0, Zone.Home, d => d with { Effect = Copse, GoldProduction = 0 });
        Check(MatchEngine.FromTestState(h).QuotePurchase(0, hprovince.Id).Sources!.All(x => !x.Id.EndsWith("#copse", StringComparison.Ordinal)), "the Copse is for Nonhumans only");
    }

    private static void BowsOnce()
    {
        // A Copse that also produces 2 Gold itself: its bow and its token are two ways to bow the same card.
        var (s, province) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        var keep = Batch336Support.Add(s, Keep, 0, Zone.Home);
        var copse = Fx(s, Keep, Copse, 0, Zone.Home, d => d with { Effect = Copse, GoldProduction = 2 });
        var e = MatchEngine.FromTestState(s);
        var token = copse.Id + "#copse";
        var source = e.QuotePurchase(0, province.Id).Sources!.Single(x => x.Id == token);
        Check(source.ConflictingSourceIds.Contains(copse.Id), "the token conflicts with its card's own bow");
        Reject(e, Purchase(e, province, copse.Id, token, keep.Id));
        Check(e.QuotePurchase(0, province.Id).Payments.All(p => !(p.SourceIds.Contains(token) && p.SourceIds.Contains(copse.Id))), "no suggestion bows the card twice");
        Accept(e, Purchase(e, province, token, keep.Id));
        Check(e.State.Cards[copse.Id].Bowed, "the token bowed its card");

        // A bowed card offers no bowing token.
        var (b, bprovince) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        Batch336Support.Add(b, Keep, 0, Zone.Home); Fx(b, Keep, Copse, 0, Zone.Home, d => d with { Effect = Copse, GoldProduction = 0 }, bowed: true);
        Check(MatchEngine.FromTestState(b).QuotePurchase(0, bprovince.Id).Sources!.All(x => !x.Id.EndsWith("#copse", StringComparison.Ordinal)), "a bowed card has no bowing token");
    }

    private static void AttachedAndHandZones()
    {
        // "When a Holding bows to produce Gold, bow this Item: +1": an attached Item beside a Holding pays the last Gold of a 3 Gold Personality.
        var (s, province) = Rig(d => d with { GoldCost = 3 });
        var keep = Batch336Support.Add(s, Keep, 0, Zone.Home);
        var host = Batch336Support.Add(s, Crane, 0, Zone.Home);
        var item = Fx(s, "training-item", BowItem, 0, Zone.Attached, d => d with { Effect = BowItem }, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        var token = item.Id + "#bowitem";
        Check(e.QuotePurchase(0, province.Id).Sources!.Any(x => x.Id == token), "an Attached card offers its token");
        Reject(e, Purchase(e, province, token));
        Accept(e, Purchase(e, province, keep.Id, token));
        Check(e.State.Cards[item.Id].Bowed && e.State.Cards[item.Id].Zone == Zone.Attached, "the Item bowed and stayed attached");

        // A card in the Hand is not offered unless its token names the Hand.
        var (h, hprovince) = Rig(d => d with { Keywords = ["Samurai", "Nonhuman"], GoldCost = 3 });
        Batch336Support.Add(h, Keep, 0, Zone.Home); Fx(h, Keep, Copse, 0, Zone.Hand, d => d with { Effect = Copse, GoldProduction = 0 });
        Check(MatchEngine.FromTestState(h).QuotePurchase(0, hprovince.Id).Sources!.All(x => !x.Id.EndsWith("#handcopse", StringComparison.Ordinal)), "a hand card is not offered without AllowedZones");
    }

    private static void HandToken()
    {
        var (s, province) = Rig(baseId: Lion);
        var truce = Fx(s, Keep, Truce, 0, Zone.Hand, d => d with { Effect = Truce, GoldProduction = 0, Type = CardType.Strategy, GoldCost = 0 });
        var keeps = new[] { Batch336Support.Add(s, Keep, 0, Zone.Home), Batch336Support.Add(s, Keep, 0, Zone.Home) };
        var e = MatchEngine.FromTestState(s);
        var token = truce.Id + "#truce";
        var quote = e.QuotePurchase(0, province.Id);
        Check(quote.Sources!.Any(x => x.Id == token && x.Kind == "hand"), "Truce is a hand source for a Personality of another Clan");
        Check(e.PaymentSourcesForTest(1, new PaymentSubject(e.State.Cards[province.Id], PaymentKind.Recruit)).All(x => !x.Id.StartsWith(truce.Id, StringComparison.Ordinal)), "the other seat is never offered your hand card");
        var rejected = new GameCommand("purchase", 1, e.Version, CardId: province.Id, SourceIds: [token]);
        Check(!e.Execute(rejected).Accepted, "the other seat cannot name it");
        var before = e.State.Players[0].Hand.Count;
        Accept(e, Purchase(e, province, token, keeps[0].Id, keeps[1].Id));
        Check(Entered(e, province), "the Personality entered play");
        Eq(e.State.Cards[truce.Id].Zone, Zone.FateDiscard, "Truce was discarded as it paid");
        Eq(e.State.Players[0].Hand.Count, before - 1, "and left the hand");

        // The same Clan: not offered, and refused with no state change.
        var (same, sprovince) = Rig();
        var truce2 = Fx(same, Keep, Truce, 0, Zone.Hand, d => d with { Effect = Truce, GoldProduction = 0, Type = CardType.Strategy, GoldCost = 0 });
        Batch336Support.Add(same, Keep, 0, Zone.Home); Batch336Support.Add(same, Keep, 0, Zone.Home);
        var ce = MatchEngine.FromTestState(same);
        Check(ce.QuotePurchase(0, sprovince.Id).Sources!.All(x => !x.Id.StartsWith(truce2.Id, StringComparison.Ordinal)), "not offered for a Personality of your own Clan");
        Reject(ce, Purchase(ce, sprovince, truce2.Id + "#truce"));
    }

    private static void ConditionAndAmount()
    {
        // Colonial Dojo's shape: a non-Unique Samurai "enters play for 0 Gold" -- the Holding produces his whole cost, and only for a recruit meeting the condition.
        var (s, province) = Rig(d => d with { GoldCost = 6 });
        var dojo = Fx(s, Keep, Dojo, 0, Zone.Home, d => d with { Effect = Dojo, GoldProduction = 0 });
        var e = MatchEngine.FromTestState(s);
        var subject = new PaymentSubject(e.State.Cards[province.Id], PaymentKind.Recruit);
        Eq(e.GoldProduced(dojo.Id, 0, subject), 6, "it produces the Personality's cost");
        Eq(e.GoldProduced(dojo.Id, 0), 0, "nothing for a payment with no subject");
        Eq(e.QuotePurchase(0, province.Id).Cost, 6, "the price is unchanged");
        Check(e.QuotePurchase(0, province.Id).Payments.Any(p => p.SourceIds.SequenceEqual([dojo.Id])), "the Dojo alone pays for him");
        Accept(e, Purchase(e, province, dojo.Id));
        Check(Entered(e, province) && e.State.Cards[dojo.Id].Bowed, "he entered play and the Dojo bowed");

        // A Unique Samurai does not meet the condition: the Dojo is not a producer for him.
        var (u, uprovince) = Rig(d => d with { GoldCost = 6, Keywords = ["Samurai", "Unique"] });
        var udojo = Fx(u, Keep, Dojo, 0, Zone.Home, d => d with { Effect = Dojo, GoldProduction = 0 });
        var ue = MatchEngine.FromTestState(u);
        Eq(ue.GoldProduced(udojo.Id, 0, new PaymentSubject(ue.State.Cards[uprovince.Id], PaymentKind.Recruit)), 0, "the condition is not met for a Unique");
        Check(ue.PaymentSourcesForTest(0, new PaymentSubject(ue.State.Cards[uprovince.Id], PaymentKind.Recruit)).All(x => x.Id != udojo.Id), "so it is not even offered");

        // Suikihime's Chambers' shape: produce 4 instead of 2 when paying for a Unique Personality.
        foreach (var (keywords, expected) in new[] { (new[] { "Samurai" }, 2), (new[] { "Samurai", "Unique" }, 4) })
        {
            var (c, cprovince) = Rig(d => d with { GoldCost = 4, Keywords = keywords });
            var chambers = Fx(c, Keep, Chambers, 0, Zone.Home, d => d with { Effect = Chambers, GoldProduction = 0 });
            var ce = MatchEngine.FromTestState(c);
            Eq(ce.GoldProduced(chambers.Id, 0, new PaymentSubject(ce.State.Cards[cprovince.Id], PaymentKind.Recruit)), expected, $"Chambers for {string.Join("/", keywords)}");
        }
    }

    private static void AdjustmentAndTurnModifier()
    {
        // The hook reaches the Stronghold, which the old adjustment sum never did for printed production.
        var (s, province) = Rig();
        var stronghold = s.Cards[s.Players[0].StrongholdId];
        stronghold.Bowed = false;
        var e = MatchEngine.FromTestState(s);
        var printed = e.GoldProduced(stronghold.Id, 0);
        AdjustMode = "stronghold";
        try { Eq(e.GoldProduced(stronghold.Id, 0), printed + 2, "a hook adjusts the Stronghold"); }
        finally { AdjustMode = ""; }
        Eq(e.GoldProduced(stronghold.Id, 0), printed, "and only while it is in force");
        Check(province.Id != stronghold.Id, "(the province card is not the Stronghold)");

        // "All non-Unique Holdings produce 1 less Gold this turn" holds for a Holding that enters play later this turn, and a Unique Holding is not touched.
        var (t, _) = Rig();
        var keep = Batch336Support.Add(t, Keep, 0, Zone.Home);
        var unique = Fx(t, Keep, "training-p1-unique-keep", 0, Zone.Home, d => d with { Keywords = ["Unique"] });
        var te = MatchEngine.FromTestState(t);
        Eq(te.GoldProduced(keep.Id, 0), 2); Eq(te.GoldProduced(unique.Id, 0), 2);
        te.AddProductionTurnModifier(0, -1, ProductionScope.NonUniqueHoldings);
        Eq(te.GoldProduced(keep.Id, 0), 1, "one less");
        Eq(te.GoldProduced(unique.Id, 0), 2, "a Unique Holding is not touched");
        var later = Batch336Support.Edit(te, st => Batch336Support.Add(st, Keep, 0, Zone.Home));
        Eq(later.GoldProduced(later.State.Cards.Values.Last(c => c.OwnerId == 0 && c.DefinitionId == Keep).Id, 0), 1, "a Holding that enters later this turn is reduced too");
        Eq(later.GoldProduced(keep.Id, 1 - 1), 1, "(still in force)");
        // It affects only that player's Holdings (here the opponent's Holding is untouched), and a later turn is untouched.
        var foe = Batch336Support.Edit(later, st => Batch336Support.Add(st, Keep, 1, Zone.Home));
        Eq(foe.GoldProduced(foe.State.Cards.Values.Last(c => c.OwnerId == 1 && c.DefinitionId == Keep).Id, 1), 2, "the other player's Holdings are untouched");
        var next = Batch336Support.Edit(later, st => st.TurnNumber++);
        Eq(next.GoldProduced(keep.Id, 0), 2, "it ends with the turn");
        // A Holding-scoped and an any-card modifier, and a Stronghold-scoped one.
        var scoped = Batch336Support.Edit(next, st => { });
        scoped.AddProductionTurnModifier(0, 3, ProductionScope.Stronghold);
        Eq(scoped.GoldProduced(stronghold.Id, 0), printed + 3, "scoped to the Stronghold");
        Eq(scoped.GoldProduced(keep.Id, 0), 2, "and not to a Holding");
    }

    private static void LentProducers()
    {
        var (s, province) = Rig(d => d with { GoldCost = 2 });
        var foreign = Batch336Support.Add(s, Keep, 1, Zone.Home);
        var own = Batch336Support.Add(s, Keep, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var foreignCard = e.P22CardForTest(foreign.Id);
        Check(e.PaymentOptions(0, 2).All(p => !p.SourceIds.Contains(foreign.Id)), "before it is lent, it is not offered to the borrower");
        Check(!e.LendProducer(e.P22CardForTest(own.Id), 1), "a bowed Holding cannot be lent");
        Check(!e.LendProducer(foreignCard, 1), "a card cannot be lent to its own owner");
        Check(e.LendProducer(foreignCard, 0), "an unbowed Holding of the other player can be lent");
        Check(e.PaymentOptions(0, 2).Any(p => p.SourceIds.SequenceEqual([foreign.Id])), "the borrower is offered it");
        Check(e.PaymentOptions(1, 2).All(p => !p.SourceIds.Contains(foreign.Id)), "the owner is not offered it while it is lent");
        Eq(e.GetView(0).Players.SelectMany(p => p.Home).Single(c => c.Id == foreign.Id).GoldProduced, 2, "the view shows its Gold");
        AdjustMode = "borrower";
        try
        {
            Eq(e.GoldProduced(foreign.Id, 0), 3, "priced for the borrower");
            Eq(e.GoldProduced(foreign.Id, 1), 2, "(and for its owner, were it asked)");
            Eq(e.GetView(0).Players.SelectMany(p => p.Home).Single(c => c.Id == foreign.Id).GoldProduced, 3, "CardView.GoldProduced is the borrower's price");
        }
        finally { AdjustMode = ""; }
        var ownerStronghold = e.State.Players[1].StrongholdId;
        Reject(e, new GameCommand("purchase", 1, e.Version, CardId: province.Id, SourceIds: [foreign.Id])); // not the owner's, and not his province card either
        Accept(e, Purchase(e, province, foreign.Id));
        Check(e.State.Cards[foreign.Id].Bowed && Entered(e, province), "the borrower's payment bowed the lent Holding");
        Eq(e.State.Cards[foreign.Id].OwnerId, 1, "nothing about control changed");
        Check(ownerStronghold is not null, "(sanity)");

        // Until end of turn.
        var (t, tprovince) = Rig(d => d with { GoldCost = 2 });
        var tf = Batch336Support.Add(t, Keep, 1, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        Check(te.LendProducer(te.P22CardForTest(tf.Id), 0), "lent");
        var next = Batch336Support.Edit(te, st => st.TurnNumber++);
        Check(next.PaymentOptions(0, 2).All(p => !p.SourceIds.Contains(tf.Id)), "no longer the borrower's the next turn");
        Check(next.PaymentOptions(1, 2).Any(p => p.SourceIds.Contains(tf.Id)), "and the owner's again");
        Check(tprovince.Id is not null, "(sanity)");
    }

    private static void AfterPaymentLedger()
    {
        var (s, province) = Rig(d => d with { GoldCost = 3 });
        var keeps = new[] { Batch336Support.Add(s, Keep, 0, Zone.Home), Batch336Support.Add(s, Keep, 0, Zone.Home) };
        var spare = Batch336Support.Add(s, Keep, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldSpentThisPhase(0), 0); Check(!e.ProducedGoldThisTurn(e.P22CardForTest(keeps[0].Id)), "nothing produced yet");
        Seen.Clear(); Recording = true;
        try { Accept(e, Purchase(e, province, keeps[0].Id, keeps[1].Id)); }
        finally { Recording = false; }
        Eq(Seen.Count, 1, "one payment, one call");
        Eq(Seen[0], $"0|3|4|{province.Id}|{keeps[0].Id},{keeps[1].Id}|{keeps[0].Id},{keeps[1].Id}", "the player, cost, produced Gold, subject and sources");
        Eq(e.GoldSpentThisPhase(0), 3, "the ledger counts the Gold paid this phase"); Eq(e.GoldSpentThisTurn(0), 3); Eq(e.GoldSpentThisPhase(1), 0, "per player");
        Eq(e.GoldSpentThisPhase(0, PaymentKind.Recruit), 3, "by kind: a Recruit"); Eq(e.GoldSpentThisPhase(0, PaymentKind.Equip), 0, "and not an Equip"); Eq(e.GoldSpentThisPhaseExceptRecruit(0), 0, "none of it is a non-Recruit payment");
        Check(e.ProducedGoldThisTurn(e.P22CardForTest(keeps[0].Id)) && e.ProducedGoldThisTurn(e.P22CardForTest(keeps[1].Id)), "the cards that produced are marked");
        Check(!e.ProducedGoldThisTurn(e.P22CardForTest(spare.Id)), "and a Holding that did not is not");
        // Per phase: the same turn in the next phase starts again, the turn total keeps counting; a new turn clears both.
        var action = Batch336Support.Edit(e, st => st.Phase = Phase.Action);
        Eq(action.GoldSpentThisPhase(0), 0, "a new phase starts at zero"); Eq(action.GoldSpentThisTurn(0), 3, "the turn total remains");
        var next = Batch336Support.Edit(e, st => st.TurnNumber++);
        Eq(next.GoldSpentThisTurn(0), 0, "a new turn starts at zero"); Check(!next.ProducedGoldThisTurn(next.P22CardForTest(keeps[0].Id)), "and nothing has produced yet");
    }

    private static void WiderProducers()
    {
        var (s, province) = Rig(d => d with { GoldCost = 2 });
        var host = Batch336Support.Add(s, Crane, 0, Zone.Home);
        var coin = Fx(s, "training-item", "training-p1-coin", 0, Zone.Attached, d => d with { GoldProduction = 2 }, parent: host.Id);
        var plain = Fx(s, "training-item", "training-p1-plain-item", 0, Zone.Attached, parent: host.Id);
        var inPlay = Fx(s, "training-duel-force", "training-p1-treasure", 0, Zone.EventInPlay, d => d with { GoldProduction = 3 });
        var noRule = Fx(s, "training-duel-force", "training-p1-no-rule", 0, Zone.EventInPlay);
        var inHand = Fx(s, "training-duel-force", "training-p1-hand-strategy", 0, Zone.Hand, d => d with { GoldProduction = 3 });
        var e = MatchEngine.FromTestState(s);
        var ids = e.QuotePurchase(0, province.Id).Sources!.Select(x => x.Id).ToHashSet();
        Check(ids.Contains(coin.Id), "an Attached card with a production rule is a producer");
        Check(ids.Contains(inPlay.Id), "an in-play Strategy with a production rule is a producer");
        Check(!ids.Contains(plain.Id) && !ids.Contains(noRule.Id), "neither is one without a production rule");
        Check(!ids.Contains(inHand.Id), "nor is a Strategy in the hand");
        Eq((e.GetView(0).Players[0].EventsInPlay ?? []).Single(c => c.Id == inPlay.Id).GoldProduced, 3, "the view shows the in-play Strategy's Gold");
        Accept(e, Purchase(e, province, coin.Id));
        Check(Entered(e, province) && e.State.Cards[coin.Id].Bowed, "the attached card paid and bowed");
    }

    private static void SaveAndReplay()
    {
        var (s, province) = Rig(d => d with { GoldCost = 2 });
        var foreign = Batch336Support.Add(s, Keep, 1, Zone.Home);
        var own = Batch336Support.Add(s, Keep, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.LendProducer(e.P22CardForTest(foreign.Id), 0), "lent");
        e.AddProductionTurnModifier(0, -1, ProductionScope.NonUniqueHoldings);
        var snapshot = JsonSerializer.Serialize(e.State);
        var restored = JsonSerializer.Deserialize<MatchState>(snapshot)!;
        Eq(restored.LentProducers.Count, 1, "lent producers"); Eq(restored.ProductionTurnModifiers.Count, 1, "turn modifiers");
        Check(restored.LentProducers[0] == e.State.LentProducers[0] && restored.ProductionTurnModifiers[0] == e.State.ProductionTurnModifiers[0], "the records are equal after the round trip");
        // A saved game continues exactly like the original: the same command from the restored copy and from the original gives the same state.
        var resumed = MatchEngine.FromTestState(restored);
        Check(resumed.PaymentOptions(0, 2).Any(p => p.SourceIds.Contains(foreign.Id)), "the restored game still has the lent producer");
        var pay = (MatchEngine engine) => Accept(engine, Purchase(engine, province, foreign.Id, own.Id));
        pay(e); pay(resumed);
        var after = JsonSerializer.Serialize(e.State);
        Eq(JsonSerializer.Serialize(resumed.State), after, "the original and the resumed game agree after the payment");
        var restored2 = JsonSerializer.Deserialize<MatchState>(after)!;
        Check(restored2.GoldSpentLedger.Count == 1 && restored2.GoldProducedTurns.Count == 2, "the ledger and the produced marks round-trip");
        Eq(MatchEngine.FromTestState(restored2).GoldSpentThisPhase(0), e.GoldSpentThisPhase(0), "and are read back");
    }
}
