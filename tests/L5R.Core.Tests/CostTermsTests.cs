using L5R.Core;
using static RegressionAssertions;

// Primitive P2 (issue #682, CostTerms.cs): registered entry cost terms, ability cost terms and the PayGold cost verb. The callbacks below are TEST-ONLY and inert unless a test sets Mode, so no other suite
// sees them; the cards that use the channels are wired in a later wave.
internal static class CostTermsTests
{
    private const string TestAbility = "test-p2-bow-or-pay";
    private static string Mode = "";

    public static void Run(TestRunner runner) => runner.Run("cost-terms", [
        ("An entry cost term discounts a recruit, is shown in the quote's basis, floors the price at zero and leaves a quote with no term unchanged", EntryDiscount),
        ("A surcharge term raises the price; an ignore-cost term makes it zero whatever else applies, with its label as the basis", SurchargeAndIgnore),
        ("The helpers read the live board: the Ring a player controls, the Defender, the opponent's Clan and a printed Gold Cost", Helpers),
        ("An entry cost term also prices an attachment entering play,; the recruit-only terms do not touch it", EquipTerm),
        ("A term keyed on the Defender applies in a battle the payer defends and not in the Dynasty phase", DefenderTerm),
        ("An ability cost term lowers a priced ability, never below zero, and never creates a price for a free option", AbilityTerm),
        ("PayGold: one ability with two ways to pay -- the Gold option is priced and paid with the chosen sources and bows nothing; the other option is free and bows its subject", PayGoldOption),
        ("PayGold: the Gold option cannot be paid without the Gold, and the rejected command changes nothing", PayGoldUnpayable),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.EntryCostTerms.Register("TestP2Entry", 19000, static context => context.Kind == EntryCostKind.Equip ? (Mode == "equip-discount" ? [new CostTerm(-1, "test equip discount", WielderDependent: true)] : null) : Mode switch
        {
            "discount" => [new CostTerm(-2, "test discount")],
            "big-discount" => [new CostTerm(-100, "test big discount")],
            "surcharge" => [new CostTerm(2, "test surcharge")],
            "ignore" => [new CostTerm(0, "test waiver", IgnoreCost: true), new CostTerm(-1, "also a discount")],
            "defender" => context.Engine.IsDefendingPlayer(context.PayerId) ? [new CostTerm(-4, "test Defender")] : null,
            _ => null
        });
        EngineHooks.AbilityCostTerms.Register("TestP2Ability", 19000, static context => context.AbilityId == TestAbility ? Mode switch
        {
            "ability-discount" => [new CostTerm(-1, "test ability discount")],
            "ability-big-discount" => [new CostTerm(-100, "test big discount")],
            _ => null
        } : null);
        EngineHooks.Abilities.Register("TestP2Abilities", 19000, static abilities =>
        {
            abilities.Add(TestAbility, new("Limited", "Test: bow this card or pay 4 Gold", "none",
                CostOptions: [new("bow", "Bow this card", CostVerb.Bow, CostSubject.Source), new("pay", "Pay 4 Gold", CostVerbs.PayGold, CostSubject.Source, GoldCostRule.Fixed(4))]));
            return abilities;
        });
        EngineHooks.RunEffects.Register("TestP2Run", 19000, static (engine, context) => context.id == TestAbility ? false : null);
    }

    private static T With<T>(string mode, Func<T> body)
    {
        Mode = mode;
        try { return body(); } finally { Mode = ""; }
    }

    // A face-up Personality in player 0's province 0 to quote, with the engine's own printed price.
    private static (MatchEngine Engine, string Id, int Printed) Province(Phase phase = Phase.Dynasty, int actor = 0)
    {
        var s = Batch336Support.Table(phase, actor);
        var id = s.Players[0].Provinces[0].CardId!;
        s.Cards[id].DefinitionId = "training-crane-veteran"; s.Cards[id].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        return (e, id, CardCatalog.Get("training-crane-veteran").GoldCost);
    }

    private static void EntryDiscount()
    {
        var (e, id, printed) = Province();
        Check(printed >= 3, "the fixture Personality costs enough to show a discount");
        var plain = e.QuotePurchase(0, id);
        Eq(plain.Cost, printed, "no term: the printed price");
        var discounted = With("discount", () => e.QuotePurchase(0, id));
        Eq(discounted.Cost, printed - 2, "one term of -2");
        Check((discounted.Basis ?? "").Contains("− 2 (test discount)"), discounted.Basis ?? "");
        var floored = With("big-discount", () => e.QuotePurchase(0, id));
        Eq(floored.Cost, 0, "floored at zero");
        Check((floored.Basis ?? "").Contains("(min 0)"), floored.Basis ?? "");
        Eq(e.QuotePurchase(0, id).Cost, printed, "the next quote with no term is unchanged");
    }

    private static void SurchargeAndIgnore()
    {
        var (e, id, printed) = Province();
        Eq(With("surcharge", () => e.QuotePurchase(0, id)).Cost, printed + 2, "a surcharge");
        var ignored = With("ignore", () => e.QuotePurchase(0, id));
        Eq(ignored.Cost, 0, "ignoring Gold Cost");
        Check((ignored.Basis ?? "").Contains("(test waiver)"), ignored.Basis ?? "");
        // The ignoring quote also holds when the payer asks for the Clan discount: a waiver zeroes the whole price.
        Eq(With("ignore", () => e.QuotePurchase(0, id, clanAllegiance: false)).Cost, 0, "a waiver still zero");
    }

    private static void Helpers()
    {
        var (e, id, printed) = Province(Phase.Battle, 1);
        Check(e.IsDefendingPlayer(0) && !e.IsDefendingPlayer(1), "player 0 defends the battle player 1 attacks");
        Check(e.OpponentIsClan(0, e.State.Players[1].Clan) && !e.OpponentIsClan(0, "No Such Clan"), "the opponent's Clan");
        Eq(e.PrintedGoldCost(e.State.Cards[id]), printed, "the printed Gold Cost");
        Check(!e.ControlsRing(0, "Ring of Earth"), "no Ring controlled yet");
        var withRing = Batch336Support.Edit(e, st =>
        {
            var ring = Batch336Support.Add(st, RingDefinition(), 0, Zone.Celestial);
            Check(ring is not null, "the Ring was placed");
        });
        Check(withRing.ControlsRing(0, CardCatalog.Get(RingDefinition()).Name) && !withRing.ControlsRing(1, CardCatalog.Get(RingDefinition()).Name), "the Ring the player controls, by title");
    }

    // The published Ring of Earth.
    private static string RingDefinition() => CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && !c.ScenarioOnly && c.Name == "Ring of Earth").Id;

    private static void EquipTerm()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        for (var i = 0; i < 2; i++) Batch336Support.Add(s, "training-marketplace", 0, Zone.Home);
        var item = Batch336Support.Add(s, "training-item", 0, Zone.Hand);
        Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        int? Quote() => e.QueryActions(0).FirstOrDefault(a => a.Type == "equip" && a.CardId == item.Id && a.Gold is not null)?.Gold?.Cost;
        var printed = CardCatalog.Get("training-item").GoldCost;
        Eq(Quote(), printed, "no term: the printed price");
        Eq(With("equip-discount", Quote), Math.Max(0, printed - 1), "an Equip term of -1");
        Eq(With("discount", Quote), printed, "a recruit-only term leaves the attachment's price alone");
        Eq(With("ignore", Quote), printed, "so does a recruit waiver");
    }

    private static void DefenderTerm()
    {
        var (dynasty, dynastyId, printed) = Province(Phase.Dynasty, 0);
        Eq(With("defender", () => dynasty.QuotePurchase(0, dynastyId)).Cost, printed, "not defending in the Dynasty phase");
        var (battle, battleId, _) = Province(Phase.Battle, 1);
        Eq(With("defender", () => battle.QuotePurchase(0, battleId)).Cost, Math.Max(0, printed - 4), "defending: 4 less");
        Eq(With("defender", () => battle.QuotePurchase(0, battleId, viaReserve: true)).Cost, Math.Max(0, printed - 4), "a Reserve recruit while defending: the same term");
    }

    // The source card of the test ability: any card of player 0's at Home with the ability granted.
    private static (MatchEngine Engine, string Id) AbilityTable()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var card = Batch336Support.Add(s, "training-crane-veteran", 0, Zone.Home);
        card.GrantedAbilities.Add(new(TestAbility, "test-fixture", GrantDuration.Permanent));
        return (MatchEngine.FromTestState(s), card.Id);
    }

    private static GameCommand TestCommand(MatchEngine e, string id, string option, string[]? sources = null) =>
        new("useAbility", 0, e.Version, CardId: id, AbilityId: TestAbility, CostOption: option, SourceIds: sources);

    private static void AbilityTerm()
    {
        var (e, id) = AbilityTable();
        Eq(e.QuoteAbilityGold(TestCommand(e, id, "pay"))?.Cost, 4, "the option's own price");
        Eq(With("ability-discount", () => e.QuoteAbilityGold(TestCommand(e, id, "pay")))?.Cost, 3, "the ability term takes 1 off");
        Check((With("ability-discount", () => e.QuoteAbilityGold(TestCommand(e, id, "pay")))?.Basis ?? "").Contains("− 1 (test ability discount)"), "the basis names the term");
        Eq(With("ability-big-discount", () => e.QuoteAbilityGold(TestCommand(e, id, "pay")))?.Cost, 0, "floored at zero");
        Check(With("ability-discount", () => e.QuoteAbilityGold(TestCommand(e, id, "bow"))) is null, "the free option stays free: a term never creates a price");
    }

    private static void PayGoldOption()
    {
        var (e, id) = AbilityTable();
        var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == TestAbility).ToArray();
        Check(offered.Length == 2 && offered.All(a => a.Enabled) && offered.Any(a => a.Command!.CostOption == "pay") && offered.Any(a => a.Command!.CostOption == "bow"), "one offered action per way to pay");
        var payOption = offered.Single(a => a.Command!.CostOption == "pay");
        Eq(payOption.Gold?.Cost, 4, "the Gold option ships its quote");
        Check(offered.Single(a => a.Command!.CostOption == "bow").Gold is null, "the bow option ships no quote");
        var sources = e.PaymentOptions(0, 4).First().SourceIds;
        Check(sources.Length > 0, "four Gold can be produced");
        Accept(e, TestCommand(e, id, "pay", sources));
        Check(!e.State.Cards[id].Bowed, "paying Gold bows nothing of the source");
        Check(sources.All(s => e.State.Cards[s].Bowed), "the chosen sources were bowed for the Gold");
        // The other way to pay: bow the source, no Gold.
        var (e2, id2) = AbilityTable();
        Accept(e2, TestCommand(e2, id2, "bow"));
        Check(e2.State.Cards[id2].Bowed, "the bow option bows its subject");
        Check(e2.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != id2).All(c => !c.Bowed), "and bows no Gold source");
    }

    private static void PayGoldUnpayable()
    {
        var (e, id) = AbilityTable();
        var before = e.State.Version;
        Check(Batch336Support.Rejection(e, TestCommand(e, id, "pay", [])).Contains("Gold", StringComparison.OrdinalIgnoreCase), "paying with no sources is rejected for the Gold");
        Eq(e.State.Version, before, "a rejected command changes nothing");
        Check(!e.State.Cards[id].Bowed, "nothing was bowed");
    }
}
