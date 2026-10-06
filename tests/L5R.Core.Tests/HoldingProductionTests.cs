using System.Text.RegularExpressions;
using L5R.Core;

// Issue #455: a published card whose printed text says "Bow this card: Produce N Gold" must carry that N as its
// GoldProduction, or produce it through an ability. holding-wave-1's four Holdings carried 0 and no ability, so
// Producers() skipped them and they could never be bowed for Gold at all.
internal static class HoldingProductionTests
{
    // The cards that produce Gold through an ability instead of the printed field: a conditional production
    // (ConditionalProductionCatalog), a Reaction that produces while a cost is paid (Yoritomo Sakuma), or a
    // created Holding's own trait (Basic Lesson). Each is covered by its own suite. The Sacred Temple of the
    // Phoenix - inexperienced (issue #458): GoldProduction is its own printed BASE (3, the catalogue's own
    // "production" field); the printed text's "produce 5 Gold" is its conditional bonus (sacred-temple-phoenix-gold).
    private static readonly string[] ProduceThroughAnAbility =
        // oracle-l5r-6299 (Returning Home, issue #467 batch 4) prints the CREATED Holding's "Produce 3 Gold", as Basic Lesson (oracle-l5r-713) does.
        ["oracle-l5r-1190", "oracle-l5r-9624", "oracle-l5r-9437", "oracle-l5r-713", "oracle-l5r-10256", "oracle-l5r-6299",
         // Emperor Edition Personality pass (issue #477 batch 6): Seppun Washi (6620) and Yasuki Tanimura (9464) produce Gold through their Reactions (alternate Gold sources), as Yoritomo Sakuma (9624) does.
         "oracle-l5r-6620", "oracle-l5r-9464",
         // Issue #501 (mechanics group E): Yoritomo Singh - exp2 (9630) produces through his Reaction, as Tanimura does.
         "oracle-l5r-9630",
         // Issue #653: Treasure Hunter (9966) prints the CREATED Holding's "Produce 2 Gold" (the published created-only Charter, oracle-l5r-10806), as Returning Home does.
         "oracle-l5r-9966",
         // Issue #653: Kenro-ji-jin's Bounty (10229), a Celestial, prints "Reaction: When paying for a Farm: Produce 1 Gold" -- a Reaction Gold source, as Seaside Bazaar's.
         "oracle-l5r-10229",
         // Issue #653 (PromoEmperorItems.cs): Mantle of the Jade Champion (9967) produces 4 Gold through its Reaction when paying for a Spell, Library of Rebirth's reactive source on an attached card.
         "oracle-l5r-9967",
         // Issue #667 (PromoPaymentSources.cs): Near Miss (5523) is a 2 Gold payment source from the hand when paying for an attachment.
         "oracle-l5r-5523",
         // Issue #653 (Promotional-Emperor): Imperial Charter prints the CREATED Holding's "Produce 2 Gold" (the published Charter Holding, oracle-l5r-10806, carries it), as Basic Lesson does.
         "oracle-l5r-9990"];

    public static void Run(TestRunner runner) => runner.Run("holding-production", [
        ("The four holding-wave-1 Holdings produce the Gold they print: the field carries it and each one alone funds a payment of that size", PrintedProductionIsUsable),
        ("Every published card printing \"Produce N Gold\" carries N, unless it produces through an ability", NoPublishedCardSilentlyProducesNothing),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone) => Batch336Support.Add(s, def, owner, zone);

    private static int PrintedProduction(CardDefinition card) =>
        Regex.Match(card.Text ?? "", @"[Pp]roduce (\d+) Gold") is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;

    private static void PrintedProductionIsUsable()
    {
        foreach (var id in new[] { "oracle-l5r-430", "oracle-l5r-7291", "oracle-l5r-7875", "oracle-l5r-8721" })
        {
            var definition = CardCatalog.Get(id);
            var printed = PrintedProduction(definition);
            Check(printed > 0, $"{definition.Name} prints a Gold production");
            Eq(definition.GoldProduction, printed, $"{definition.Name} carries its printed production");
            var state = MatchEngine.CreateEconomyScenario().State;
            state.Phase = Phase.Action; state.ActivePlayerId = 0; state.PriorityPlayerId = 0;
            var holding = Add(state, id, 0, Zone.Home);
            var engine = MatchEngine.FromTestState(state);
            Eq(engine.GoldProduced(holding.Id, 0), printed, $"{definition.Name} produces its printed Gold");
            Check(engine.PaymentOptions(0, printed).Any(o => o.SourceIds.SequenceEqual(new[] { holding.Id }) && o.Produced == printed),
                $"{definition.Name} is offered as the source that funds a {printed} Gold payment");
        }
    }

    private static void NoPublishedCardSilentlyProducesNothing()
    {
        var wrong = CardCatalog.PlayableFromDecks
            .Where(c => !ProduceThroughAnAbility.Contains(c.Id, StringComparer.Ordinal))
            .Select(c => (Card: c, Printed: PrintedProduction(c)))
            .Where(x => x.Printed > 0 && x.Card.GoldProduction != x.Printed)
            .Select(x => $"{x.Card.Name} ({x.Card.Id}) prints {x.Printed} and carries {x.Card.GoldProduction}")
            .ToArray();
        Check(wrong.Length == 0, "Published cards whose printed Gold production is not wired: " + string.Join("; ", wrong));
    }
}
