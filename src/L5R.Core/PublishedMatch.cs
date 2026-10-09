namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Shared by the live app scenario (PublishedContent.Snapshot) and the frozen test scenario
    // (PublishedContent.TestSnapshot) -- only which deck lists to draw from differs; card behavior for
    // any given id always resolves through the one shared CardCatalog either way.
    internal static MatchEngine CreatePublished(ulong seed, string scenario, PublishedSnapshot snapshot)
    {
        var state = new MatchState { Players = [new() { Id = 0, Name = "Crane", Clan = "Crane" }, new() { Id = 1, Name = "Lion", Clan = "Lion" }] };
        var random = new StableRandom(seed); var nextId = 0; var ids = OpaqueCardIds(seed);
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = ids[nextId++], DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card); return card;
        }
        foreach (var player in state.Players)
        {
            var deck = snapshot.Decks.Single(d => d.Clan == player.Clan);
            var stronghold = CardCatalog.Get(deck.StrongholdId);
            player.Honor = stronghold.StartingHonor;
            player.StrongholdId = Add(player.Id, stronghold.Id, Zone.Stronghold, true).Id;
            Add(player.Id, "oracle-l5r-1106", Zone.Home, true);
            // A created-only Proxy scan may not be included in decks.
            foreach (var id in deck.Dynasty.Concat(deck.Fate))
                if (CardCatalog.Get(id).CreatedOnly) throw new ArgumentException($"{CardCatalog.Get(id).Name} ({id}) may not be included in decks.");
            foreach (var id in deck.Dynasty) player.DynastyDeck.Add(Add(player.Id, id, Zone.DynastyDeck).Id);
            foreach (var id in deck.Fate) player.FateDeck.Add(Add(player.Id, id, Zone.FateDeck).Id);
            random.Shuffle(player.DynastyDeck); random.Shuffle(player.FateDeck);
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = stronghold.ProvinceStrength, PrintedStrength = stronghold.ProvinceStrength }).ToList();
        }
        state.RngState = random.State; // Deck search (issue #312): later shuffles continue this exact sequence.
        state.ActivePlayerId = FirstPlayerAtSetup(state, 0); ApplyFootholdStartHonor(state); ApplyImperialSetup(state); // Foothold of the Mad (issue #766, Foothold.cs); Journey's End Keep's Favor (issue #771, Wave771.cs)
        Add(1 - state.ActivePlayerId, "oracle-l5r-679", Zone.Home, true, true);
        OrderCardsById(state);
        var engine = new MatchEngine(state, seed, scenario);
        foreach (var player in state.Players)
        {
            foreach (var province in player.Provinces) engine.Refill(player, province);
            for (var i = 0; i < 6; i++) engine.Draw(player);
        }
        engine.Emit("publishedSetup", "Lion goes first (7 starting Honor); Crane starts at 6 Honor with bowed Bamboo Harvesters. Each player uses 40 Dynasty and 40 Fate cards.");
        engine.StartTurn(); return engine;
    }
}
