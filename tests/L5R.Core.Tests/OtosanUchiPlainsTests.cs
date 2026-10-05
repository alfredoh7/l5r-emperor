using L5R.Core;
using static RegressionAssertions;

// Plains of Otosan Uchi (oracle-l5r-5979, issue #554, OtosanUchiPlains.cs): a two-Dynasty-card province built and driven
// only through QueryActions and the commands the engine offers, using the synthetic training-otosan-uchi fixture
// (CardCatalog.cs) so no published scan is needed to exercise the mechanic.
internal static class OtosanUchiPlainsTests
{
    private const string RegionDef = "training-otosan-uchi", Toson = "oracle-l5r-7177";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);

    public static void Run(TestRunner runner) => runner.Run("otosan-uchi-plains", [
        ("Published shape: region-wave-21 prints the scan's stats, keywords and text, carries no Abilities (Effect-keyed instead), ships its art and is in its manifest", PublishedShape),
        ("Revealing the Region attaches it and creates an empty second slot, refilled face down; not revealed this turn", AttachCreatesSecondSlot),
        ("Both slots face down: the controller chooses which to reveal first, and it resolves in that order", RevealOrderChoice),
        ("Discarding a card from a province's SECOND slot and refilling face up (Moon's Imperative, Hawado) works on the right slot instead of crashing (bot soak, issue #642)", DiscardSecondSlotRefillsFaceUp),
        ("The reveal-order choice goes to the player it belongs to even when the other player held priority; the other player is never offered it (bot soak, issue #642)", RevealOrderTakesPriority),
        ("Destroying the province loses 10 Honor and discards both slots and the Region", DestructionHonorLoss),
        ("Purchasing the card in the second slot refills the second slot, not the first", PurchaseFromSecondSlot),
        ("Shosuro Toson: two candidates ask which to discard, each face-up card's own title replacing Left/Right", TosonDisambiguation),
        ("Errata: destroying the Region alone leaves the second slot's card in place, but it is not refilled again", RegionDestroyedAloneErrata),
    ]);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));

    private static void PublishedShape()
    {
        const string id = "oracle-l5r-5979";
        var c = CardCatalog.Get(id);
        Check(c.Type == CardType.Region && c.Name == "Plains of Otosan Uchi" && c.Clan == "Unaligned", $"{id}'s type/name/clan");
        Check(c.GoldCost == 0 && c.GoldProduction == 0 && c.ProvinceStrength == 0, $"{id}'s stats");
        Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), "Imperial,Unique", $"{id}'s keywords");
        Eq(c.Effect, OtosanUchiPlains.Effect, $"{id}'s effect");
        Eq((c.Abilities ?? []).Length, 0, $"{id} has no activatable ability (its effects are Effect-keyed engine hooks)");
        Eq(c.Text, "This Province holds two Dynasty cards; do not reveal the second card the same turn this Region enters play.\nBefore this Province is destroyed, lose 10 Honor.", $"{id} prints the scan's text");
        Check(!c.ScenarioOnly, $"{id} is playable");
        Check(ArtShipped(id), $"{id} ships art");
        var manifest = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/region-wave-21.txt"));
        Check(new HashSet<string>(manifest).SetEquals([id]), "region-wave-21 manifest");
    }

    // A fresh economy scenario with both hands and both Dynasty/Fate decks emptied, so every card in a test is one the test put there.
    private static MatchState Start()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.DynastyDeck or Zone.FateDeck).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.DynastyDeck.Clear(); p.FateDeck.Clear(); }
        return s;
    }

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone, bool bowed = false, int? province = null)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"otosan-{n}")) n++;
        var c = new CardInstance { Id = $"otosan-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck), Bowed = bowed, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }
    private static void Deck(MatchState s, int owner, Zone zone, params string[] definitions) { foreach (var d in definitions) Put(s, d, owner, zone); }
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);

    // Player 0 ends their (otherwise empty) turn; player 1's own Events Phase reveals whatever sits in their province 0.
    private static MatchEngine EndTurnIntoEvents(MatchState s)
    {
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var e = Engine(s);
        Accept(e, new("endDynasty", 0, e.Version));
        if (e.State.PendingChoice?.Type == "discardHand") Accept(e, new("discardHand", 0, e.Version, CardIds: e.State.Players[0].Hand.Take(e.State.Players[0].Hand.Count - 8).ToArray()));
        return e;
    }

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the stop was not reached");
    }

    private static void AttachCreatesSecondSlot()
    {
        var s = Start();
        var province = s.Players[1].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 1, Zone.Province, province: 0); region.FaceUp = false; province.CardId = region.Id;
        Deck(s, 1, Zone.DynastyDeck, "training-lion-samurai", "training-lion-courtier");
        var e = EndTurnIntoEvents(s);
        var after = e.State.Players[1].Provinces[0];
        Check(after.RegionId == region.Id, "the Region attached");
        // AttachRegionToProvince (attaching the second slot) runs before ResolveRegion's own Refill of the primary slot,
        // so the second slot draws first -- which physical slot gets which random card is otherwise immaterial.
        Check(after.CardId is not null, "the primary slot refilled");
        Eq(e.State.Cards[after.CardId!].DefinitionId, "training-lion-courtier", "the primary slot's fresh card");
        Check(after.CardId2 is not null, "the second slot was created and refilled");
        Eq(e.State.Cards[after.CardId2!].DefinitionId, "training-lion-samurai", "the second slot's fresh card");
        Check(!e.State.Cards[after.CardId2!].FaceUp, "the second slot's card is NOT revealed the turn the Region enters play");
        Eq(e.State.Phase, Phase.Action, "the Events Phase finished without getting stuck");
    }

    private static void DiscardSecondSlotRefillsFaceUp()
    {
        var s = Start();
        var province = s.Players[0].Provinces[0];
        var region = Put(s, RegionDef, 0, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-lion-samurai", 0, Zone.Province, province: 0); cardA.FaceUp = true; province.CardId = cardA.Id;
        var cardB = Put(s, "training-lion-courtier", 0, Zone.Province, province: 0); cardB.FaceUp = true; province.CardId2 = cardB.Id;
        var deckCard = Put(s, "training-lion-samurai", 0, Zone.DynastyDeck); s.Players[0].DynastyDeck.Insert(0, deckCard.Id);
        var source = Put(s, "training-lion-samurai", 1, Zone.Home);
        var e = Engine(s);
        var discard = typeof(MatchEngine).GetMethod("DiscardProvinceCardAndRefillFaceUp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var card = typeof(MatchEngine).GetMethod("Card", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(string)])!;
        discard.Invoke(e, [card.Invoke(e, [cardB.Id])!, card.Invoke(e, [source.Id])!]); // the engine's own card objects (State is a snapshot)
        var after = e.State.Players[0].Provinces[0];
        Eq(after.CardId, cardA.Id, "the first slot is untouched");
        Eq(after.CardId2, deckCard.Id, "the second slot was refilled from the deck");
        Check(e.State.Cards[deckCard.Id].FaceUp, "face up");
        Eq(e.State.Cards[cardB.Id].Zone, Zone.DynastyDiscard, "the discarded card went to the Dynasty discard pile");
    }

    private static void RevealOrderTakesPriority()
    {
        var s = Start();
        var province = s.Players[0].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 0, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-lion-samurai", 0, Zone.Province, province: 0); cardA.FaceUp = false; province.CardId = cardA.Id;
        var cardB = Put(s, "training-lion-courtier", 0, Zone.Province, province: 0); cardB.FaceUp = false; province.CardId2 = cardB.Id;
        s.ActivePlayerId = 0; s.PriorityPlayerId = 1; // the other player answered something just before: priority was left with him
        var e = Engine(s);
        var open = typeof(MatchEngine).GetMethod("BeginProvinceRevealOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Check((bool)open.Invoke(e, [e.State.Players[0], e.State.Players[0].Provinces[0]])!, "the choice opens");
        Eq(e.State.PendingChoice?.Type, "provinceOrder", "asks which to reveal first");
        Eq(e.State.PriorityPlayerId, 0, "the player it belongs to now holds the action opportunity");
        Check(e.QueryActions(0).Count(a => a.Type == "provinceOrder" && a.Enabled) == 2, "he is offered Left and Right, enabled");
        Check(e.QueryActions(1).All(a => a.Type != "provinceOrder"), "the other player is not offered the choice");
    }

    private static void RevealOrderChoice()
    {
        var s = Start();
        var province = s.Players[1].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 1, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-lion-samurai", 1, Zone.Province, province: 0); cardA.FaceUp = false; province.CardId = cardA.Id;
        var cardB = Put(s, "training-lion-courtier", 1, Zone.Province, province: 0); cardB.FaceUp = false; province.CardId2 = cardB.Id;
        var e = EndTurnIntoEvents(s);
        Eq(e.State.PendingChoice?.Type, "provinceOrder", "asks which to reveal first");
        var options = e.QueryActions(1).Where(a => a.Type == "provinceOrder").ToArray();
        Eq(options.Length, 2, "both Left and Right are offered");
        Check(options.All(a => a.Enabled), "both are enabled");
        Eq(e.State.PriorityPlayerId, 1, "the chooser holds the action opportunity (bot soak, issue #642)");
        Check(e.QueryActions(0).All(a => a.Type != "provinceOrder"), "the other player is not offered the choice");
        var right = options.Single(a => a.Label == "Right");
        Accept(e, right.Command!);
        // The economy scenario's other three provinces reveal their own (unrelated) cards in this same Events Phase too; filter to these two.
        var revealed = e.State.Events.Where(ev => ev.Type == "provinceRevealed" && (ev.CardId == cardA.Id || ev.CardId == cardB.Id)).Select(ev => ev.CardId).ToArray();
        Eq(revealed.Length, 2, "both cards revealed");
        Eq(revealed[0], cardB.Id, "the second slot (Right) revealed first");
        Eq(revealed[1], cardA.Id, "then the first slot");
        Check(e.State.Cards[cardA.Id].FaceUp && e.State.Cards[cardB.Id].FaceUp, "both are now face up");
        Check(e.State.PendingChoice is null, "the choice is settled");
        Eq(e.State.Phase, Phase.Action, "the Events Phase finished cleanly");
    }

    private static void DestructionHonorLoss()
    {
        var s = Start();
        var province = s.Players[0].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 0, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-lion-samurai", 0, Zone.Province, province: 0); cardA.FaceUp = false; province.CardId = cardA.Id;
        var cardB = Put(s, "training-lion-courtier", 0, Zone.Province, province: 0); cardB.FaceUp = false; province.CardId2 = cardB.Id;
        var e = Engine(s);
        var honorBefore = e.State.Players[0].Honor;
        e.DestroyProvinceForTest(0, 0);
        Eq(e.State.Players[0].Honor, honorBefore - 10, "loses 10 Honor when the province is destroyed");
        var after = e.State.Players[0].Provinces[0];
        Check(after.Destroyed, "the province is destroyed");
        Check(after.CardId is null && after.CardId2 is null && after.RegionId is null, "both slots and the Region are cleared");
        Eq(e.State.Cards[cardA.Id].Zone, Zone.DynastyDiscard, "slot 1's card discarded");
        Eq(e.State.Cards[cardB.Id].Zone, Zone.DynastyDiscard, "slot 2's card discarded");
        Eq(e.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "the Region discarded");
    }

    private static void PurchaseFromSecondSlot()
    {
        var s = Start();
        var province = s.Players[0].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 0, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-farm", 0, Zone.Province, province: 0); cardA.FaceUp = true; province.CardId = cardA.Id;
        var recruit = Put(s, "training-mitsu", 0, Zone.Province, province: 0); recruit.FaceUp = true; province.CardId2 = recruit.Id; // GoldCost 0: no payment setup needed
        Deck(s, 0, Zone.DynastyDeck, "training-lion-courtier");
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var e = Engine(s);
        var quote = e.QuotePurchase(0, recruit.Id, false, false);
        Check(quote.Error is null, quote.Error ?? "must be purchasable");
        Accept(e, new("purchase", 0, e.Version, CardId: recruit.Id, SourceIds: quote.Payments.FirstOrDefault()?.SourceIds));
        var after = e.State.Players[0].Provinces[0];
        Eq(after.CardId, cardA.Id, "slot 1 untouched");
        Check(after.CardId2 is not null, "slot 2 refilled");
        Eq(e.State.Cards[after.CardId2!].DefinitionId, "training-lion-courtier", "slot 2's fresh card");
        Eq(e.State.Cards[recruit.Id].Zone, Zone.Home, "the recruited card entered play");
    }

    private static void TosonDisambiguation()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var toson = Add(s, Toson, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var province = s.Players[1].Provinces[0];
        var region = Add(s, RegionDef, 1, Zone.Region, province: 0); province.RegionId = region.Id;
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        var cardA = Add(s, "training-marketplace", 1, Zone.Province, province: 0); province.CardId = cardA.Id; // face up, matching TosonTable's own convention
        var cardB = Add(s, "training-lion-courtier", 1, Zone.Province, province: 0); province.CardId2 = cardB.Id;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Accept(e, Enabled(e, 0, "toson-exp2-engage", toson.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "provinceCardChoice", "two candidates: asks which to discard");
        var options = e.QueryActions(0).Where(a => a.Type == "provinceCardChoice").ToArray();
        Eq(options.Length, 2, "Left and Right offered");
        Check(options.Any(a => a.Label == CardCatalog.Get(cardA.DefinitionId).Name), "a face-up candidate's own title replaces its plain label");
        Check(options.Any(a => a.Label == CardCatalog.Get(cardB.DefinitionId).Name), "so does the other");
        var chooseB = options.Single(a => a.Label == CardCatalog.Get(cardB.DefinitionId).Name);
        Accept(e, chooseB.Command!);
        Eq(e.State.Cards[cardB.Id].Zone, Zone.DynastyDiscard, "the chosen card was discarded");
        Eq(e.State.Cards[cardA.Id].Zone, Zone.Province, "the other candidate untouched");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.FirstActionPlayerId, 0, "a Personality was discarded: Toson's controller holds the first opportunity");
        Eq(e.State.FirstActionRestriction?.PerformerId, toson.Id, "which Toson must perform");
    }

    private static void RegionDestroyedAloneErrata()
    {
        var s = Start();
        var province = s.Players[0].Provinces[0];
        if (province.CardId is string old) s.Cards.Remove(old);
        var region = Put(s, RegionDef, 0, Zone.Region, province: 0); province.RegionId = region.Id;
        var cardA = Put(s, "training-farm", 0, Zone.Province, province: 0); cardA.FaceUp = true; province.CardId = cardA.Id;
        var recruit = Put(s, "training-mitsu", 0, Zone.Province, province: 0); recruit.FaceUp = true; province.CardId2 = recruit.Id;
        Deck(s, 0, Zone.DynastyDeck, "training-lion-courtier"); // would refill slot 2 if the errata did not apply
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var e = Engine(s);
        e.DestroyRegionForTest(region.Id);
        var afterDestroy = e.State.Players[0].Provinces[0];
        Check(afterDestroy.RegionId is null, "the Region left play on its own");
        Eq(afterDestroy.CardId2, recruit.Id, "the second slot's card stays (MRP errata), not auto-discarded");
        var quote = e.QuotePurchase(0, recruit.Id, false, false);
        Check(quote.Error is null, quote.Error ?? "must still be purchasable");
        Accept(e, new("purchase", 0, e.Version, CardId: recruit.Id, SourceIds: quote.Payments.FirstOrDefault()?.SourceIds));
        Check(e.State.Players[0].Provinces[0].CardId2 is null, "the errata: once empty, the second slot is not refilled again");
    }
}
