namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>The tutorial's table (issue #935): a prepared offline match against the practice opponent, never reachable from a room or a deck.
    /// Crane (you) and Lion (the bot) on explicit, ordered decks so each lesson meets exactly the cards it teaches with. The match is built the way
    /// <see cref="CreatePublished"/> builds one, but stops before the first turn: both Fate hands are empty and every province is still face down.
    /// The opponent has already taken its first turn, so you are the second player, which is why you start with the bowed Bamboo Harvesters. Lion starts with more Honor (7 against 6) and goes first, as it would in a real match.</summary>
    public static MatchEngine CreateTutorial()
    {
        const ulong seed = 935;
        var state = new MatchState { Players = [new() { Id = 0, Name = "You", Clan = "Crane" }, new() { Id = 1, Name = "Opponent", Clan = "Lion" }] };
        var nextId = 0; var ids = OpaqueCardIds(seed);
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = ids[nextId++], DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card); return card;
        }
        string[] Cycle(string[] top, string[] filler, int total) => top.Concat(Enumerable.Range(0, Math.Max(0, total - top.Length)).Select(i => filler[i % filler.Length])).ToArray();

        var you = state.Players[0]; var foe = state.Players[1];
        you.Honor = CardCatalog.Get(TutorialCards.YourStronghold).StartingHonor; foe.Honor = CardCatalog.Get(TutorialCards.FoeStronghold).StartingHonor;
        you.StrongholdId = Add(0, TutorialCards.YourStronghold, Zone.Stronghold, true).Id; foe.StrongholdId = Add(1, TutorialCards.FoeStronghold, Zone.Stronghold, true).Id;
        Add(0, TutorialCards.BorderKeep, Zone.Home, true); Add(1, TutorialCards.BorderKeep, Zone.Home, true);
        Add(0, TutorialCards.BambooHarvesters, Zone.Home, true, true);
        // The opponent's first turn is already behind it: one cheap Personality at home, which is the only unit that will ever defend for it in this lesson.
        Add(1, TutorialCards.FoeDefender, Zone.Home, true); foe.TurnsTaken = 1;
        foreach (var id in Cycle(TutorialCards.YourDynasty, TutorialCards.DynastyFiller, 40)) you.DynastyDeck.Add(Add(0, id, Zone.DynastyDeck).Id);
        foreach (var id in Cycle(TutorialCards.YourFate, TutorialCards.FateFiller, 40)) you.FateDeck.Add(Add(0, id, Zone.FateDeck).Id);
        foreach (var id in Cycle([], TutorialCards.FoeDynasty, 40)) foe.DynastyDeck.Add(Add(1, id, Zone.DynastyDeck).Id);
        foreach (var id in Cycle([], TutorialCards.FateFiller, 40)) foe.FateDeck.Add(Add(1, id, Zone.FateDeck).Id);
        foreach (var player in state.Players)
        {
            var strength = CardCatalog.Get(state.Cards[player.StrongholdId].DefinitionId).ProvinceStrength;
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = strength, PrintedStrength = strength }).ToList();
        }
        state.ActivePlayerId = 0; state.PriorityPlayerId = 0; state.TurnNumber = 1; // the opponent's first turn was turn 1, so your first turn is turn 2, as it would be in a real match
        OrderCardsById(state);
        var engine = new MatchEngine(state, seed, TutorialScenario);
        foreach (var player in state.Players) foreach (var province in player.Provinces) engine.Refill(player, province);
        engine.Emit("tutorialSetup", "Tutorial table: every province is face down and nobody has drawn Fate cards yet.");
        return engine;
    }

    public const string TutorialScenario = "tutorial-v1";

    /// <summary>Starts the player's turn from the tutorial's paused table: the turn's Straighten and Events phases run and the match stops on the first decision.</summary>
    internal void TutorialBeginTurn() => StartTurn();

    /// <summary>"Pretend we forgot to draw": the tutorial's own draw of the opening hand, from a deck ordered for it.</summary>
    internal void TutorialDraw(int playerId, int count) { for (var i = 0; i < count; i++) Draw(Player(playerId)); }

    /// <summary>Read-only lookups for the tutorial script (the card behind an id, where a named card is), kept on the engine so the script never copies the whole state.</summary>
    internal string TutorialDefinitionOf(string cardId) => _state.Cards[cardId].DefinitionId;
    internal string[] TutorialCardIds(int owner, string definitionId, params Zone[] zones) =>
        _state.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definitionId && (zones.Length == 0 || zones.Contains(c.Zone))).Select(c => c.Id).ToArray();
    internal string? TutorialProvinceCardId(int owner, int index) => Player(owner).Provinces[index].CardId;
    internal string TutorialStrongholdId(int owner) => Player(owner).StrongholdId;
}
