namespace L5R.Core;

// Primitive P24 (issue #690): tokens, shuffles and the straighten-triggered control return. See EffectPrimitives.cs for the feature and
// docs/decisions/2026-10-06-690-p24-effect-primitives.md for the readings.
public sealed partial class MatchEngine
{
    // ---- a token with up to three stats ---------------------------------------------------------------------------------------------------------------------------------------------------
    // "Give your target Personality a +1F/+1C/+1PH token" (A Doomseeker's Tomb). CardToken carries a third optional stat (Stat3/Amount3) beside the two it had; StatModifier reads all
    // three. As for a two-stat token (CreateTwoStatToken, EmperorP2Keirei.cs) each stat is negated or delayed on its own, a token is created with whatever is left, and a Chi left at 0
    // destroys the Personality. A delayed stat is recorded by the shared delay store as a one-stat token of its own.

    /// <summary>Creates one token carrying each (stat, amount) given -- one, two or three, each stat once. Returns the token, or null when every stat was negated or delayed.</summary>
    internal CardToken? CreateMultiStatToken(CardInstance card, string type, string source, params (string Stat, int Amount)[] stats)
    {
        if (stats.Length is < 1 or > 3 || stats.Select(s => s.Stat).Distinct(StringComparer.Ordinal).Count() != stats.Length) throw new InvalidOperationException("A token carries one to three different stats.");
        if (NegatesEffect(card, "all")) { Emit("tokenNegated", $"A {type} token on {Definition(card).Name} was negated before it resolved.", card.OwnerId, card.Id); return null; }
        bool Negated(string s, int a) =>
            s == "Force" && a < 0 && card.ForcePenaltiesNegatedTurn == _state.TurnNumber
            || !CurrentActionUnnegatable() && (ActionForceModifierNegated(card, s, a) || GiftArmorNegatesChiPenalty(card, s, a) || AkaBekoNegates(card, s, a, source));
        var kept = stats.Where(s => !Negated(s.Stat, s.Amount)).ToList();
        if (kept.Count == 0) { Emit("tokenNegated", $"A {type} token's stats on {Definition(card).Name} were negated.", card.OwnerId, card.Id); return null; }
        kept.RemoveAll(s => Delay(card, DelayedKinds.Token, stat: s.Stat, amount: s.Amount, text: type, sourceId: source));
        if (kept.Count == 0) return null;
        var token = new CardToken($"token-{++_state.TokenSequence:D4}", type, source, kept[0].Stat, kept[0].Amount,
            kept.Count > 1 ? kept[1].Stat : null, kept.Count > 1 ? kept[1].Amount : 0, kept.Count > 2 ? kept[2].Stat : null, kept.Count > 2 ? kept[2].Amount : 0);
        card.Tokens.Add(token);
        Emit("tokenCreated", $"{Definition(card).Name} gained a {type} token ({string.Join("/", kept.Select(s => $"{s.Amount:+#;-#;0}{TokenStatLabel(s.Stat)}"))}).", card.OwnerId, card.Id);
        foreach (var (stat, amount) in kept) NoteStatPenalty(card, source, type, stat, amount);
        if (kept.Any(s => s.Stat == "Chi") && Definition(card).Type == CardType.Personality && InPlay(card) && PersonalityChi(card) <= 0) DestroyUnits([card], true);
        return token;
    }

    private static string TokenStatLabel(string stat) => stat switch { "Force" => "F", "Chi" => "C", "PersonalHonor" => "PH", "GoldCost" => "GC", _ => stat };

    // ---- shuffles ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Shuffle [the chosen cards in discard piles] into your deck" (Sorrowful Prayer): each card that is still in a discard pile goes to its owner's deck of its kind, and every
    /// deck touched is shuffled (the existing ShuffleDiscardsIntoDecks, The Sun's Aspiration's). Returns how many were shuffled in.</summary>
    internal int ShuffleIntoDeck(IEnumerable<CardInstance> cards, CardInstance source)
    {
        var chosen = cards.Where(c => c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead).ToArray();
        if (chosen.Length > 0) ShuffleDiscardsIntoDecks(chosen, source);
        return chosen.Length;
    }

    /// <summary>"Each player shuffles his hand into his deck, then draws as many cards as he shuffled into his deck" (Overwhelming Chaos), for the players given, in that order. The card
    /// being played (<paramref name="played"/>, still in its player's hand while its action resolves) is not shuffled. Returns the cards each player shuffled in.</summary>
    internal Dictionary<int, int> ShuffleHandsAndRedraw(IEnumerable<int> playerIds, CardInstance? played)
    {
        var counts = new Dictionary<int, int>();
        var carried = _state.RemAShuffledCount; // Face of Ninube's own count (the shuffle below writes it): left as it was
        foreach (var playerId in playerIds.Distinct().ToArray())
        {
            var player = Player(playerId);
            RemAShuffleHandIntoDeck(player, played);
            var shuffled = _state.RemAShuffledCount;
            for (var i = 0; i < shuffled; i++) Draw(player);
            counts[playerId] = shuffled;
        }
        _state.RemAShuffledCount = carried;
        return counts;
    }

    // ---- the control return owed when its source straightens ------------------------------------------------------------------------------------------------------------------------------
    // "The target will not commit seppuku until P'an Ku straightens or leaves play. After P'an Ku does, give control of the target to his owner." A ControlReturn (EmperorMechB.cs) with
    // the trigger SourceStraightens: owed the first time the source is found unbowed after it has been seen bowed, or out of play. It is judged after every command, where a return owed
    // to a card that left play already is (ApplyControlReturnsAfterCommand), so every way a card straightens is seen, the Straighten Phase's included.

    /// <summary>Registers "give control of <paramref name="taken"/> to his owner after <paramref name="source"/> straightens or leaves play".</summary>
    internal void ReturnControlWhenSourceStraightens(CardInstance taken, CardInstance source, string reason)
    {
        MechB.ControlReturns.Add(new ControlReturn
        {
            Seq = ++MechB.ControlReturnSequence,
            CardId = taken.Id,
            Trigger = ControlReturnTriggers.SourceStraightens,
            SourceId = source.Id,
            Reason = reason,
            SourceSeenBowed = InPlay(source) && source.Bowed
        });
    }

    // ApplyControlReturnsAfterCommand's question for one owed return.
    private bool SourceStraightenedReturnDue(ControlReturn entry)
    {
        if (entry.Trigger != ControlReturnTriggers.SourceStraightens) return false;
        if (entry.SourceId is null || !_state.Cards.TryGetValue(entry.SourceId, out var source) || !InPlay(source)) return true;
        if (source.Bowed) { entry.SourceSeenBowed = true; return false; }
        return entry.SourceSeenBowed;
    }
}
