namespace L5R.Core;

// P20 (issue #728): isolated review positions for the new search shapes, for the native deck-search probe (tools/check-deck-search-ui.sh) and the bot-policy test. Like the other showcases
// (DecisionShowcase.cs, DecisionInfraShowcase.cs) they exist only through MatchEngine.CreateSearchShowcase: the specs below are named by no card, deck or room.
public sealed partial class MatchEngine
{
    internal const string ShowcaseProvincesSpec = "p20-showcase-provinces", ShowcaseOtherSpec = "p20-showcase-other", ShowcaseAttachSpec = "p20-showcase-attach";

    /// <summary>Kinds: "p20-provinces" (seat 0 searches its own provinces for one or two cards with different titles, any of them face down), "p20-other" (seat 1 is searching its Fate deck for two Items with different titles
    /// and seat 0 chooses which one is discarded), "p20-attach" (seat 0 found an Item and chooses which of its Personalities carries it).</summary>
    public static MatchEngine CreateSearchShowcase(string kind)
    {
        if (kind is not ("p20-provinces" or "p20-other" or "p20-attach")) throw new ArgumentException("Unknown search showcase.", nameof(kind));
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null; state.CurrentBattleProvince = null;
        foreach (var id in state.Players[0].Hand.ToArray()) state.Cards.Remove(id);
        state.Players[0].Hand.Clear();
        var searcher = kind == "p20-other" ? 1 : 0;
        CardInstance Put(string definition, int owner, Zone zone)
        {
            var card = new CardInstance { Id = "p20-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.FateDeck or Zone.DynastyDeck) };
            state.Cards.Add(card.Id, card);
            if (zone == Zone.FateDeck) state.Players[owner].FateDeck.Add(card.Id);
            return card;
        }
        var source = Put("oracle-l5r-3636", searcher, Zone.Home);
        if (kind == "p20-provinces")
        {
            var faceDown = false;
            foreach (var province in state.Players[0].Provinces.Where(p => p.CardId is not null)) { state.Cards[province.CardId!].FaceUp = faceDown; faceDown = !faceDown; } // alternate face-down and face-up
        }
        else
        {
            foreach (var item in CardCatalog.Cards.Values.Where(d => !d.ScenarioOnly && d.Type == CardType.Item && d.AttachRequiresKeyword is null && d.AttachRequiresName is null && d.AttachExcludesKeyword is null && d.HonorRequirement is null).GroupBy(d => d.Name).Select(g => g.First()).Take(3)) Put(item.Id, searcher, Zone.FateDeck);
            if (kind == "p20-attach") { Put("training-lion-samurai", 0, Zone.Home); Put("training-lion-samurai", 0, Zone.Home); }
        }
        var engine = FromTestState(state);
        var specId = kind switch { "p20-provinces" => ShowcaseProvincesSpec, "p20-other" => ShowcaseOtherSpec, _ => ShowcaseAttachSpec };
        engine.BeginSearch(SearchCatalog.All[specId], engine.Card(source.Id), searcher, "ability");
        if (kind == "p20-attach") engine.ResolveSearchCommand(new("selectSearchCard", 0, engine.Version, Choice: engine.SearchGroups(engine._state.PendingSearch!).First(g => g.Eligible).DefinitionId));
        return engine;
    }

    // Registration only: callbacks are not invoked during module initialization.
    private static void RegisterSearchExtensionsShowcase()
    {
        EngineHooks.Searches.Register("P20ShowcaseSearches", 22801, static () =>
        [
            new SearchSpec(ShowcaseProvincesSpec, "Search your provinces for up to two cards with different titles",
                [new(SearchDeck.OwnProvinces, new("card", DifferentTitles: true), SearchEffect.Bottom)], SelectCount: new(1, 2)),
            new SearchSpec(ShowcaseOtherSpec, "Search your Fate deck for two Items with different titles",
                [new(SearchDeck.Fate, new("Item", [CardType.Item], DifferentTitles: true), SearchEffect.ToHand)], SelectCount: new(2, 2), OtherPlayerChooses: new(SearchEffect.Discard)),
            new SearchSpec(ShowcaseAttachSpec, "Search your Fate deck for an Item and attach it to a Personality",
                [new(SearchDeck.Fate, new("Item", [CardType.Item]), SearchEffect.AttachToPersonality)])
        ]);
    }
}
