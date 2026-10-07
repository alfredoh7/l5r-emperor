using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #699: Benediction (strategy-wave-120), "Open: Destroy your target Holding. Give a Province +3 strength." Mendon's ruling: the Province may be any province (either player's, face-up or
// face-down); destroying the Holding is a separate effect that happens first; if its destruction is negated the strength bonus is not. The two targets are a card slot and the new
// province slot kind (TargetSlots.cs). Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion).
internal static class WlBenedictionTests
{
    private const string Benediction = "oracle-l5r-10930", Ability = "wlben-benediction";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone) => Batch336Support.Add(s, def, owner, zone);
    private static SlotSelection Province(int owner, int index) => new("province", ProvinceOwner: owner, Province: index);
    private static SlotSelection Holding(CardInstance holding) => Pick("holding", holding.Id);

    public static void Run(TestRunner runner) => runner.Run("wl-benediction", [
        ("Published shape: Benediction prints its scan's text and stats, ships its art, names the Aftermath printing and is in the strategy-wave-120 manifest", PublishedShape),
        ("Offer: the action lists your Holdings and every undestroyed province of both players (face-up or face-down) as its two slots, and its default command is legal", OfferScenario),
        ("Both effects: your Holding is destroyed and the chosen province has +3 strength until the end of the turn, for an enemy face-down province and for your own face-up one", BothEffects),
        ("Order: the Holding is destroyed first, then the province is given its strength", OrderScenario),
        ("Independent effects: a negated or failed destruction leaves the Holding in play and the strength bonus is still given", NegatedDestruction),
        ("Illegal choices are refused with nothing changed: no Holding, an enemy's Holding, a non-Holding, a missing or malformed province, a destroyed province", IllegalChoices),
        ("A province slot selection survives serialisation, and a card slot cannot carry a province or the reverse", SerialisationScenario),
    ]);

    // Player 0 with a Holding (and a second one that is the enemy's); the Benediction in player 0's hand. Phase.Action, player 0 to act (Open actions).
    private static MatchEngine Build(out CardInstance card, out CardInstance holding, out CardInstance enemyHolding, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        holding = Add(s, "training-marketplace", 0, Zone.Home); enemyHolding = Add(s, "training-marketplace", 1, Zone.Home);
        card = Add(s, Benediction, 0, Zone.Hand);
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static GameCommand Command(MatchEngine e, CardInstance card, params SlotSelection[] selections)
    {
        var offered = Enabled(e, 0, Ability, card.Id).Command!;
        return offered with { Selections = selections, TargetId = selections.FirstOrDefault(x => x.CardIds is { Length: > 0 })?.CardIds![0] };
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var c = CardCatalog.Get(Benediction);
        Check(c.Type == CardType.Strategy && c.Name == "Benediction" && c.GoldCost == 0 && c.Focus == 1 && !c.ScenarioOnly, "Benediction's stats");
        Eq(c.Text, "Open: Destroy your target Holding. Give a Province +3 strength.");
        Eq(string.Join(",", c.Abilities ?? []), Ability); Eq(c.Effect, "none");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Benediction + ".jpg")), "art is shipped");
        Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-120.txt")).Contains(Benediction), "the ID is pinned in the manifest");
        var row = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-120-sources.json"))).RootElement.EnumerateArray().Single();
        Eq(string.Join(",", row.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString())), "Aftermath");
        var ability = AbilityCatalog.All[Ability];
        Eq(ability.Timing, "Open"); Eq(ability.Slots!.Length, 2); Check(ability.Slots[0].Category == "own-holding" && ability.Slots[1].IsProvince, "a Holding slot and a province slot");
    }

    private static void OfferScenario()
    {
        var e = Build(out var card, out var holding, out _, s => { s.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Province).FaceUp = false; s.Players[1].Provinces[3].Destroyed = true; });
        var option = Enabled(e, 0, Ability, card.Id);
        var slots = option.Slots!;
        Eq(slots.Length, 2);
        Eq(slots[0].Kind, TargetSlotKinds.Card); Check(slots[0].CardIds.Contains(holding.Id) && slots[0].CardIds.All(id => e.State.Cards[id].OwnerId == 0 && CardCatalog.Get(e.State.Cards[id].DefinitionId).Type == CardType.Holding), "only your own Holdings are offered");
        Eq(slots[1].Kind, TargetSlotKinds.Province);
        var choices = slots[1].ProvinceChoices!;
        var expected = e.State.Players.SelectMany(p => p.Provinces.Where(x => !x.Destroyed).Select(x => (p.Id, x.Index))).ToArray();
        Eq(choices.Length, expected.Length, "every undestroyed province of both players");
        Check(choices.Any(x => x.Owner == 0) && choices.Any(x => x.Owner == 1), "both players' provinces are offered");
        Check(!choices.Any(x => x.Owner == 1 && x.Index == 3), "a destroyed province is not offered");
        Check(option.Command!.Selections!.Any(s => s.Slot == "province" && s.ProvinceOwner is not null), "the default command names a province");
    }

    private static void BothEffects()
    {
        // An enemy province whose card is face down.
        var e = Build(out var card, out var holding, out var enemyHolding, s => s.Cards[s.Players[1].Provinces[1].CardId!].FaceUp = false);
        Check(!e.State.Cards[e.State.Players[1].Provinces[1].CardId!].FaceUp, "the enemy's province 2 is face down");
        var before = e.ProvinceStrengthForTest(1, 1); var ownBefore = e.ProvinceStrengthForTest(0, 2);
        Accept(e, Command(e, card, Holding(holding), Province(1, 1)));
        Check(e.State.Cards[holding.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "your Holding is destroyed");
        Eq(e.State.Cards[enemyHolding.Id].Zone, Zone.Home, "the enemy's Holding stands");
        Eq(e.ProvinceStrengthForTest(1, 1), before + 3, "the face-down enemy province has +3 strength");
        Eq(e.ProvinceStrengthForTest(0, 2), ownBefore, "no other province changed");
        Eq(Batch336Support.StrengthNextTurn(e, 1, 1), before, "the strength lasts until the end of the turn");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Your own face-up province.
        var g = Build(out var card2, out var holding2, out _);
        var own = g.ProvinceStrengthForTest(0, 0);
        Accept(g, Command(g, card2, Holding(holding2), Province(0, 0)));
        Eq(g.ProvinceStrengthForTest(0, 0), own + 3, "your own province has +3 strength");
        Check(g.State.Cards[holding2.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "the Holding is destroyed");
    }

    private static void OrderScenario()
    {
        var e = Build(out var card, out var holding, out _);
        Accept(e, Command(e, card, Holding(holding), Province(1, 0)));
        var destroyed = e.State.Events.ToList().FindIndex(x => x.Type == "unitDestroyed" && x.CardId == holding.Id);
        var raised = e.State.Events.ToList().FindIndex(x => x.Type == "provinceStrengthRaised");
        Check(destroyed >= 0 && raised >= 0, "both effects were recorded");
        Check(destroyed < raised, "the Holding is destroyed before the province is given its strength");
    }

    private static void NegatedDestruction()
    {
        // The Holding's destruction is negated (a watch that also covers your own effects): it stays in play, and the province strength is not negated with it.
        var e = Build(out var card, out var holding, out _);
        e.AddNegationWatch(new NegationWatch { OwnerPlayerId = 0, Kind = WatchKind.Destroy, TargetCardId = holding.Id, Source = WatchSource.Any });
        var before = e.ProvinceStrengthForTest(1, 2);
        Accept(e, Command(e, card, Holding(holding), Province(1, 2)));
        Eq(e.State.Cards[holding.Id].Zone, Zone.Home, "the negated destruction leaves the Holding in play");
        Check(e.State.Events.Any(x => x.Type == "destructionNegated" && x.CardId == holding.Id), "the negation was recorded");
        Eq(e.ProvinceStrengthForTest(1, 2), before + 3, "the province still has +3 strength");
        // A Holding that will not be destroyed (Rugashi Bazaar's text) fails the same way: the second sentence is independent.
        var g = Build(out var card2, out var bazaar, out _, s => { });
        var s2 = g.State; s2.Cards[bazaar.Id].DefinitionId = "oracle-l5r-6396";
        var h = MatchEngine.FromTestState(s2);
        var strength = h.ProvinceStrengthForTest(0, 1);
        Accept(h, Command(h, card2, Holding(bazaar), Province(0, 1)));
        Eq(h.State.Cards[bazaar.Id].Zone, Zone.Home, "a Holding that will not be destroyed stays");
        Eq(h.ProvinceStrengthForTest(0, 1), strength + 3, "the province still has +3 strength");
    }

    private static void IllegalChoices()
    {
        void Refused(MatchEngine e, CardInstance card, string why, params SlotSelection[] selections)
        {
            var before = Json(e);
            var command = new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: Ability, TargetId: selections.FirstOrDefault(x => x.CardIds is { Length: > 0 })?.CardIds![0], Selections: selections);
            var result = e.Execute(command);
            Check(!result.Accepted, why + " (it was accepted)");
            Eq(Json(e), before, why + ": nothing changed");
        }
        var e = Build(out var card, out var holding, out var enemyHolding, s => s.Players[1].Provinces[3].Destroyed = true);
        var personality = Add(e.State, "training-crane-samurai", 0, Zone.Home);
        e = MatchEngine.FromTestState(e.State);
        Refused(e, card, "an enemy's Holding", Holding(enemyHolding), Province(1, 0));
        Refused(e, card, "a Personality instead of a Holding", Holding(personality), Province(1, 0));
        Refused(e, card, "no Holding chosen", Province(1, 0));
        Refused(e, card, "no province chosen", Holding(holding));
        Refused(e, card, "a province index that does not exist", Holding(holding), Province(1, 9));
        Refused(e, card, "a player that does not exist", Holding(holding), Province(2, 0));
        Refused(e, card, "an owner without an index", Holding(holding), new("province", ProvinceOwner: 1));
        Refused(e, card, "a destroyed province", Holding(holding), Province(1, 3));
        Refused(e, card, "cards in the province slot", Holding(holding), new("province", [holding.Id], ProvinceOwner: 1, Province: 0));
        Refused(e, card, "a province in the Holding slot", new("holding", ProvinceOwner: 1, Province: 0), Province(1, 0));
        // No Holding in play: the action is listed but cannot be taken.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var held in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding).ToArray()) s.Cards.Remove(held.Id);
        var lonely = Add(s, Benediction, 0, Zone.Hand); Add(s, "training-marketplace", 1, Zone.Home);
        var none = MatchEngine.FromTestState(s);
        var listed = Options(none, 0, Ability, lonely.Id);
        Check(listed.Length > 0 && listed.All(o => !o.Enabled), "with no Holding of yours in play the action is disabled: " + string.Join("; ", listed.Select(o => o.Reason)));
        var before = Json(none);
        Check(!none.Execute(new("useAbility", 0, none.Version, lonely.Id, AbilityId: Ability, Selections: [Province(1, 0)])).Accepted, "and cannot be forced");
        Eq(Json(none), before, "nothing changed");
    }

    private static void SerialisationScenario()
    {
        var e = Build(out var card, out var holding, out _);
        var command = Command(e, card, Holding(holding), Province(1, 2));
        var round = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(command))!;
        var province = round.Selections!.Single(x => x.Slot == "province");
        Check(province.ProvinceOwner == 1 && province.Province == 2 && province.CardIds is null, "the province selection round-trips");
        Accept(e, round);
        var state = JsonSerializer.Serialize(e.State);
        Eq(JsonSerializer.Serialize(JsonSerializer.Deserialize<MatchState>(state)!), state, "the state with the strength effect round-trips");
        var offer = Build(out var card2, out _, out _);
        var slots = Enabled(offer, 0, Ability, card2.Id).Slots!;
        var again = JsonSerializer.Deserialize<TargetSlotOption[]>(JsonSerializer.Serialize(slots))!;
        Eq(again[1].ProvinceChoices!.Length, slots[1].ProvinceChoices!.Length, "the province choices round-trip");
    }
}
