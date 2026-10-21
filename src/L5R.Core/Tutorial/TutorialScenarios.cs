namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>The tutorial's table (issue #935): a prepared offline match against the practice opponent, never reachable from a room or a deck.
    /// Lion (you) and Crane (the bot) on the pinned profile's decks, with the cards the lesson meets put on top in the order it needs them. The match is built the way
    /// <see cref="CreatePublished"/> builds one, but stops before the first turn: both Fate hands are empty and every province is still face down.
    /// Lion starts with more Honor (7 against 6) and goes first, as it would in a real match; Crane, the second player, starts with the bowed Bamboo Harvesters.</summary>
    public static MatchEngine CreateTutorial()
    {
        const ulong seed = 935;
        var state = new MatchState { Players = [new() { Id = 0, Name = "You", Clan = "Lion" }, new() { Id = 1, Name = "Opponent", Clan = "Crane" }] };
        var nextId = 0; var ids = OpaqueCardIds(seed);
        CardInstance Add(int owner, string definition, Zone zone, bool faceUp = false, bool bowed = false)
        {
            _ = CardCatalog.Get(definition);
            var card = new CardInstance { Id = ids[nextId++], DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = faceUp, Bowed = bowed };
            state.Cards.Add(card.Id, card); return card;
        }

        var you = state.Players[0]; var foe = state.Players[1];
        you.Honor = CardCatalog.Get(TutorialCards.YourStronghold).StartingHonor; foe.Honor = CardCatalog.Get(TutorialCards.FoeStronghold).StartingHonor;
        you.StrongholdId = Add(0, TutorialCards.YourStronghold, Zone.Stronghold, true).Id; foe.StrongholdId = Add(1, TutorialCards.FoeStronghold, Zone.Stronghold, true).Id;
        Add(0, TutorialCards.BorderKeep, Zone.Home, true); Add(1, TutorialCards.BorderKeep, Zone.Home, true);
        Add(1, TutorialCards.BambooHarvesters, Zone.Home, true, true); // the second player's extra Holding: it enters play bowed and will not straighten before that player's second turn
        foreach (var id in TutorialCards.Deck(TutorialCards.YourDynasty, "Lion", dynasty: true)) you.DynastyDeck.Add(Add(0, id, Zone.DynastyDeck).Id);
        foreach (var id in TutorialCards.Deck(TutorialCards.YourFate, "Lion", dynasty: false)) you.FateDeck.Add(Add(0, id, Zone.FateDeck).Id);
        foreach (var id in TutorialCards.Deck(TutorialCards.FoeDynasty, "Crane", dynasty: true)) foe.DynastyDeck.Add(Add(1, id, Zone.DynastyDeck).Id);
        foreach (var id in TutorialCards.Deck(TutorialCards.FoeFate, "Crane", dynasty: false)) foe.FateDeck.Add(Add(1, id, Zone.FateDeck).Id);
        foreach (var player in state.Players)
        {
            var strength = CardCatalog.Get(state.Cards[player.StrongholdId].DefinitionId).ProvinceStrength;
            player.Provinces = Enumerable.Range(0, 4).Select(i => new ProvinceState { Index = i, Strength = strength, PrintedStrength = strength }).ToList();
        }
        state.ActivePlayerId = 0; state.PriorityPlayerId = 0; state.TurnNumber = 1; // your first turn is turn 1
        OrderCardsById(state);
        var engine = new MatchEngine(state, seed, TutorialScenario);
        foreach (var player in state.Players) foreach (var province in player.Provinces) engine.Refill(player, province);
        engine.Emit("tutorialSetup", "Tutorial table: every province is face down and nobody has drawn Fate cards yet.");
        return engine;
    }

    public const string TutorialScenario = "tutorial-v2";

    private readonly HashSet<(Phase Phase, int PlayerId)> _tutorialHolds = [];
    private Action? _tutorialResume;

    /// <summary>The lesson asks the engine to stop where a phase has just been reached (Straighten after its cards straightened, Events after the provinces were revealed, End before the draw),
    /// so the note can talk about that moment. Tutorial-only; no rules change. <see cref="TutorialResume"/> carries on from there.</summary>
    internal void TutorialHoldAt(Phase phase, int playerId) => _tutorialHolds.Add((phase, playerId));
    internal bool TutorialHeld => _tutorialResume is not null;
    /// <summary>The live state, for the script's own reads (<see cref="State"/> is a full copy). Never mutate it.</summary>
    internal MatchState TutorialState => _state;
    private bool TutorialHold(Phase phase, int playerId, Action resume)
    {
        if (!_tutorialHolds.Remove((phase, playerId))) return false;
        _tutorialResume = resume; return true;
    }

    /// <summary>Starts the first turn of the paused table; it stops where a hold was asked for.</summary>
    internal void TutorialBeginTurn() => StartTurn();

    /// <summary>Carries on from a hold.</summary>
    internal void TutorialResume() { var resume = _tutorialResume; _tutorialResume = null; resume?.Invoke(); }

    /// <summary>"Pretend we forgot to draw": the tutorial's own draw of the opening hand, from a deck ordered for it.</summary>
    internal void TutorialDraw(int playerId, int count) { for (var i = 0; i < count; i++) Draw(Player(playerId)); }

    /// <summary>Read-only lookups for the tutorial script (the card behind an id, where a named card is), kept on the engine so the script never copies the whole state.</summary>
    internal string TutorialDefinitionOf(string cardId) => _state.Cards[cardId].DefinitionId;
    internal string[] TutorialCardIds(int owner, string definitionId, params Zone[] zones) =>
        _state.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definitionId && (zones.Length == 0 || zones.Contains(c.Zone))).Select(c => c.Id).ToArray();
    internal string? TutorialProvinceCardId(int owner, int index) => Player(owner).Provinces[index].CardId;
    internal string TutorialStrongholdId(int owner) => Player(owner).StrongholdId;
}
