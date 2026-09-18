using static RegressionAssertions;
using L5R.Core;

// Generalized Gold payment (issue #303): PARITY. One case per rule the engine priced by hand before the
// refactor (ValidateAbility's cost chain, AbilityActions' parallel chain, every ability's own Pay call,
// Purchase/Reserve/Equip). Written against the pre-existing public surface only (commands, PaymentOptions,
// the "payment" event), so this exact file runs unchanged against the old engine and the new one: it pins
// that every rule still costs what it cost. Each case checks that a payment one step short of the cost is
// rejected atomically, that the exact minimal payment is accepted, and (for rules the old engine actually
// charged) that precisely the offered sources bowed and the payment event names exactly that cost.
internal static class GoldParityTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Kharmic discard costs 2", () => Pin(Kharmic, 2)),
            ("Training Invest costs 3", () => Pin(TrainingInvest, 3)),
            ("Clay Kshatriya's Invest costs 5", () => Pin(ClayInvest, 5)),
            ("Togashi Noritada's Invest costs 3 (validated; the old engine never charged it)", () => Pin(NoritadaInvest, 3, charged: false)),
            ("Singh Remnants' shape costs the watcher card's own printed cost", () => Pin(EntryWatcher, 2)),
            ("A Strategy source costs its own printed cost (Paid Off, 4)", () => Pin(PaidOff, 4)),
            ("A Discipline Strategy costs its own printed cost (Banzai!, 2)", () => Pin(Banzai, 2)),
            ("A non-Strategy Discipline costs its source's printed cost (2; the old engine validated but never charged it)", () => Pin(DisciplineItem, 2, charged: false)),
            ("Equip (Battle) costs the targeted Spell's own cost", () => { Pin(() => EquipBattle("training-spell-with-ability"), 1); Pin(() => EquipBattle("oracle-l5r-9344"), 2); }),
            ("Soshi Neiru's equip costs the targeted Spell's own cost", () => Pin(Neiru, 2)),
            ("Low Market pays 2 less, to a minimum of 1", () => { Pin(() => LowMarket("training-follower-crane-guard"), 2); Pin(() => LowMarket("training-item"), 1); }),
            ("Your Clan Needs You! pays the recruit's cost minus 6, to a minimum of 0", () => { Pin(() => YourClanNeedsYou("training-nonhuman-personality"), 0); Pin(() => YourClanNeedsYou("training-crane-veteran"), 2); }),
            ("Shinden Shorai pays the recruit's full cost", () => Pin(() => ShindenShorai("training-lion-samurai"), 5)),
            ("Ordinary purchase: printed cost, Clan discount -2 and Clan allegiance +2", () => { Pin(() => Purchase(false, false), 5); Pin(() => Purchase(true, false), 3); Pin(() => Purchase(false, true), 7); }),
            ("recruitReserve pays the recruit's own cost", () => Pin(Reserve, 3)),
            ("Ordinary equip pays the attachment's printed cost", () => Pin(() => Equip("training-item", "training-crane-samurai"), 2)),
            ("Equip discounts: Katagi's non-Unique Weapon -4, Satoshi's Lion -1, Legion of the Bat's Crane -1", () =>
            {
                Pin(() => Equip("training-weapon", "training-crane-samurai"), 1);
                Pin(() => Equip("training-weapon", "oracle-l5r-5112"), 0);
                Pin(() => Equip("oracle-l5r-4700", "training-crane-samurai"), 4);
            }),
            ("A free ability accepts no Gold sources and pays nothing", FreeAbilityTakesNoSources),
        ];
        runner.Run("gold-parity", tests);
    }

    internal static CardInstance Add(MatchState s, string def, int owner = 0, Zone zone = Zone.Home, bool bowed = false, int? province = null, string? attachedTo = null)
    {
        var c = new CardInstance { Id = $"test-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = attachedTo };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }

    internal static MatchState Action()
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        return s;
    }

    internal static MatchState Battle(string strongholdDefinitionId)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = strongholdDefinitionId;
        s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        return s;
    }

    // The source set producing the most Gold while still strictly less than `cost`, or null when the cost is
    // 0 (nothing is short of it). Brute force over the player's unbowed producers.
    private static string[]? Under(MatchEngine e, int cost)
    {
        if (cost <= 0) return null;
        var ids = e.State.Cards.Values.Where(c => c.OwnerId == 0 && !c.Bowed && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)
            .Select(c => c.Id).Take(14).ToArray();
        string[]? best = null; var bestProduced = -1;
        for (var mask = 0; mask < 1 << ids.Length; mask++)
        {
            var chosen = ids.Where((_, i) => (mask >> i & 1) == 1).ToArray();
            var produced = chosen.Sum(id => e.GoldProduced(id, 0));
            if (produced < cost && produced > bestProduced) { best = chosen; bestProduced = produced; }
        }
        return best;
    }

    private static void Pin(Func<(MatchEngine E, Func<string[], GameCommand> Command)> setup, int cost, bool charged = true)
    {
        var (e, command) = setup();
        var under = Under(e, cost);
        if (under is not null) Reject(e, command(under));
        var pay = e.PaymentOptions(0, cost).First();
        Accept(e, command(pay.SourceIds));
        if (!charged) return;
        Check(pay.SourceIds.All(id => e.State.Cards[id].Bowed), "Exactly the offered sources bow");
        var payment = e.State.Events.Last(x => x.Type == "payment");
        Check(payment.Message.Contains($"paid {cost} Gold"), $"The payment event names the cost: {payment.Message}");
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Kharmic()
    {
        var s = Action();
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-kharmic-dynasty"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, TargetId: cardId, AbilityId: "kharmic-discard", SourceIds: src));
    }

    // Equip a Follower, opening its own entering-play reaction window; the returned command is the reaction.
    internal static (MatchEngine, Func<string[], GameCommand>) InvestAfterEquip(string followerDefinition, string abilityId, int equipCost)
    {
        var s = Action();
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var follower = Add(s, followerDefinition, 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: follower.Id, SourceIds: e.PaymentOptions(0, equipCost).First().SourceIds, TargetId: host.Id));
        Check(e.State.PendingChoice?.Type == "entryReaction", "The entering-play reaction window is open");
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: follower.Id, AbilityId: abilityId, SourceIds: src));
    }
    internal static (MatchEngine, Func<string[], GameCommand>) TrainingInvest() => InvestAfterEquip("training-invest-follower", "training-invest", 2);
    internal static (MatchEngine, Func<string[], GameCommand>) ClayInvest() => InvestAfterEquip("oracle-l5r-10678", "clay-kshatriya-invest", 2);

    internal static (MatchEngine, Func<string[], GameCommand>) NoritadaInvest()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "oracle-l5r-10634"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 4).First().SourceIds));
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: cardId, AbilityId: "noritada-invest", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) EntryWatcher()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var enteringId = s.Players[0].Provinces[0].CardId!;
        s.Cards[enteringId].DefinitionId = "training-crane-clan-samurai"; s.Cards[enteringId].FaceUp = true;
        var watcher = Add(s, "training-personality-entry-watcher", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: enteringId, SourceIds: e.PaymentOptions(0, 5).First().SourceIds));
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: watcher.Id, AbilityId: "training-personality-entry-watch", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) PaidOff()
    {
        var s = Action();
        var card = Add(s, "oracle-l5r-5861", 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: card.Id, TargetId: enemy.Id, AbilityId: "paid-bow", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Banzai()
    {
        var s = Battle("oracle-l5r-1218");
        var strategy = Add(s, "oracle-l5r-10929", 0, Zone.FateDiscard);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, TargetId: target.Id, AbilityId: "banzai", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) DisciplineItem()
    {
        var s = Action();
        var card = Add(s, "training-discipline-item", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: card.Id, AbilityId: "training-discipline-ability", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) EquipBattle(string spellDefinition)
    {
        var s = Battle("oracle-l5r-1218");
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, spellDefinition, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Neiru()
    {
        var s = Action();
        var neiru = Add(s, "oracle-l5r-7360", 0, Zone.Home);
        var air = Add(s, "oracle-l5r-9344", 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: neiru.Id, TargetId: air.Id, AbilityId: "neiru-equip", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) LowMarket(string attachmentDefinition)
    {
        var s = Action();
        var lowMarket = Add(s, "oracle-l5r-4777", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = lowMarket.Id;
        var performer = Add(s, "training-crane-clan-samurai", 0, Zone.Home);
        var attachment = Add(s, attachmentDefinition, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: lowMarket.Id, SourceIds: src, TargetId: attachment.Id, AbilityId: "low-market-equip", PerformerId: performer.Id));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) YourClanNeedsYou(string recruitDefinition)
    {
        var s = Battle("oracle-l5r-1218");
        var strategy = Add(s, "oracle-l5r-9687", 0, Zone.Hand);
        var recruit = Add(s, recruitDefinition, 0, Zone.Province, province: 1);
        s.Players[0].Provinces[1].CardId = recruit.Id;
        Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: strategy.Id, TargetId: recruit.Id, AbilityId: "your-clan-needs-you", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) ShindenShorai(string recruitDefinition)
    {
        var s = Action(); s.TurnNumber = 1;
        s.Players[0].Clan = "Crane"; s.Players[0].Honor = 3; s.Players[0].TurnsTaken = 3;
        var region = Add(s, "oracle-l5r-6857", 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = region.Id;
        var provinceCard = s.Cards[s.Players[0].Provinces[0].CardId!]; provinceCard.DefinitionId = recruitDefinition; provinceCard.FaceUp = true;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("useAbility", 0, e.Version, CardId: region.Id, TargetId: provinceCard.Id, AbilityId: "shinden-shorai-recruit", SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Purchase(bool discount, bool allegiance)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = "training-crane-samurai"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: src, ClanDiscount: discount, ClanAllegiance: allegiance));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Reserve()
    {
        var s = Battle("oracle-l5r-1218"); s.ActivePlayerId = 1;
        var reserveId = s.Players[0].Provinces[0].CardId!;
        s.Cards[reserveId].DefinitionId = "training-reserve-personality"; s.Cards[reserveId].FaceUp = true;
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("recruitReserve", 0, e.Version, CardId: reserveId, SourceIds: src));
    }

    internal static (MatchEngine, Func<string[], GameCommand>) Equip(string attachmentDefinition, string hostDefinition)
    {
        var s = Action();
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var attachment = Add(s, attachmentDefinition, 0, Zone.Hand);
        var host = Add(s, hostDefinition, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, src => new GameCommand("equip", 0, e.Version, CardId: attachment.Id, SourceIds: src, TargetId: host.Id));
    }

    private static void FreeAbilityTakesNoSources()
    {
        MatchEngine Table()
        {
            var s = Action(); s.FavorPlayerId = 0;
            Add(s, "training-honor-personality", 0, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Table();
        var honor = e.State.Cards.Values.Single(c => c.DefinitionId == "training-honor-personality");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: honor.Id, AbilityId: "shunya-honor", SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        var e2 = Table();
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: honor.Id, AbilityId: "shunya-honor", SourceIds: []));
        Check(!e2.State.Events.Any(x => x.Type == "payment"), "A free ability leaves no payment event");
    }
}
