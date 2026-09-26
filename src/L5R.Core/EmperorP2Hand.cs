namespace L5R.Core;

// Issue #503, pass group P2: the hand cards, The Agony of Her Gaze and A Champion in Court's Reaction (EmperorP2.cs has the group's header).
//   * HAND-DISCARD IMMUNITY FROM ANOTHER PLAYER (P2HandDiscardBlocked): Pillowbook's "Other players' cards' effects will not discard cards from your hand" and The Scorpion Wall is Finished's "other
//     players' actions will not discard cards from your hand". The engine discards a card from a hand at many places, most of them the hand's own player paying a cost or making a choice of his
//     own; the places where ANOTHER player's card makes him discard were audited and each now asks this question with who caused it and whether it is an action's effect: a forced discard queued by
//     another player's card (Times of Strife, Daigotsu Bofana, Feint & Strike, The Shadow Court's Either/Or, Moshi Ino's storm, Benefices of the Emperor), Indecision, Isawa Kimi's discard, the random
//     discards of Kitsuki Methods and of the Either/Or choices of Remember Your Ancestors' family. "Will not" means the discard simply fails (core rules Glossary).
//   * A PER-PHASE CAP ON CARDS PUT INTO A HAND (P2CardIntoHandBlocked): The Scorpion Wall is Finished's "after a player has resolved two actions that put cards in his hand in one phase, no more cards
//     will be put into his hand that phase" -- read off the per-phase count of such actions (MechD.HandActionsThisPhase, issue #501) at every place a card goes into a hand: a draw, a search, a
//     look, a return to the hand, a card taken from a discard pile. The card stays where it was.
//   * HAND SIZES (P2HandLimit): The Tapestry Perceived's "While this Shugenja is Void, your maximum hand size is increased by 1", Pillowbook's "Your maximum hand size is increased by 1. Other players'
//     maximum hand sizes are reduced by 1, to a minimum of 7."
//   * THE PERFORMERS OF AN ACTION (NoteP2Performers): recorded as its effects begin (the named performers, the card an action is on, or an attachment's Personality) and read when it is truly done:
//     The Agony of Her Gaze's "after the resolution of any Battle action, give each of your Nonhumans who performed it a +1F Frenzy token".
public sealed partial class MatchEngine
{
    public const string PillowbookEffect = "p2-pillowbook";
    public const string ScorpionWallEffect = "p2-scorpion-wall-finished";

    // ---- hand-discard immunity ----

    /// <summary>True (and logged) when <paramref name="handOwner"/>'s hand may not lose a card to <paramref name="causer"/>'s card effect: Pillowbook (any card effect of another player) or The
    /// Scorpion Wall is Finished (another player's action).</summary>
    private bool P2HandDiscardBlocked(int handOwner, int? causer, bool byAction, string reason)
    {
        if (causer is not int by || by == handOwner) return false;
        var pillowbook = _state.Cards.Values.FirstOrDefault(c => c.OwnerId == handOwner && InPlay(c) && Definition(c).Effect == PillowbookEffect);
        var wall = byAction ? _state.Cards.Values.FirstOrDefault(c => c.OwnerId == handOwner && c.Zone == Zone.EventInPlay && Definition(c).Effect == ScorpionWallEffect) : null;
        var protector = pillowbook ?? wall;
        if (protector is null) return false;
        Emit("handDiscardPrevented", $"{Definition(protector).Name}: {Player(by).Name}'s {reason} will not discard cards from {Player(handOwner).Name}'s hand.", handOwner, protector.Id);
        return true;
    }

    // ---- the cap on cards put into a hand ----

    /// <summary>True (and logged) when no more cards will be put into <paramref name="playerId"/>'s hand this phase (The Scorpion Wall is Finished, in play for any player).</summary>
    private bool P2CardIntoHandBlocked(int playerId)
    {
        if (MechD.HandActionsThisPhase[playerId] < 2) return false;
        var wall = _state.Cards.Values.FirstOrDefault(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == ScorpionWallEffect);
        if (wall is null) return false;
        Emit("cardIntoHandPrevented", $"{Definition(wall).Name}: {Player(playerId).Name} has resolved two actions that put cards in his hand this phase; no more cards will be put into his hand this phase.", playerId, wall.Id);
        return true;
    }

    // ---- hand sizes ----

    // HandLimitFor's hook: the increases first, then Pillowbook's reductions of another player's hand, each only while the maximum stays at 7 or more.
    private IEnumerable<HandLimitModifier> P2HandLimit(int playerId, int current)
    {
        foreach (var spell in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Attached && Definition(c).Effect == EmperorP2Abilities.TapestryEffect
            && c.AttachedTo is { } host && _state.Cards.TryGetValue(host, out var shugenja) && Keyword(shugenja, "Void")).OrderBy(c => c.Id, StringComparer.Ordinal))
        { current++; yield return new HandLimitModifier(spell.Id, Definition(spell).Name, 1); }
        foreach (var book in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == PillowbookEffect).OrderBy(c => c.Id, StringComparer.Ordinal))
        { current++; yield return new HandLimitModifier(book.Id, Definition(book).Name, 1); }
        foreach (var book in _state.Cards.Values.Where(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Effect == PillowbookEffect).OrderBy(c => c.Id, StringComparer.Ordinal))
            if (current > 7) { current--; yield return new HandLimitModifier(book.Id, Definition(book).Name, -1); }
    }

    // ---- The Agony of Her Gaze ----

    private static string PerformedKey(string abilityId, int playerId) => $"{abilityId}|{playerId}";

    // RunAbilityEffects: the cards performing the action whose effects begin to resolve.
    private void NoteP2Performers(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (command.AbilityId is null || !IsAction(ability)) return;
        var performers = new List<string>();
        if (command.PerformerId is { } performerId) performers.Add(performerId);
        if (ability.Slots is { } slots)
            foreach (var slot in slots.Where(s => s.Performing)) performers.AddRange(SlotCards(command, slot.Id).Select(c => c.Id));
        if (performers.Count == 0 && source is not null && InPlay(source))
        {
            var type = Definition(source).Type;
            if (type is CardType.Personality or CardType.Follower) performers.Add(source.Id);
            else if (type is CardType.Item or CardType.Spell && source.AttachedTo is { } wielder) performers.Add(wielder);
        }
        P2.Performers[PerformedKey(command.AbilityId, command.PlayerId)] = performers.Distinct(StringComparer.Ordinal).ToList();
    }

    // FinishActionEffectTracking: "after the resolution of any Battle action, give each of your Nonhumans who performed it a +1F Frenzy token" -- a trait of each Agony of Her Gaze in play.
    private void ApplyAgonyOfHerGaze(string abilityId, int playerId)
    {
        var key = PerformedKey(abilityId, playerId);
        if (!P2.Performers.Remove(key, out var performers) || !IsBattleAction(abilityId)) return;
        foreach (var agony in _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == EmperorP2Abilities.AgonyEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var id in performers)
                if (_state.Cards.TryGetValue(id, out var performer) && performer.OwnerId == agony.OwnerId && InPlay(performer) && Keyword(performer, "Nonhuman"))
                {
                    CreateToken(performer, "Frenzy", agony.Id, "Force", 1);
                    Emit("frenzyToken", $"{Definition(agony).Name}: {Definition(performer).Name} gets a +1F Frenzy token.", agony.OwnerId, performer.Id);
                }
    }

    // ---- A Champion in Court's Reaction ----

    // AfterActionResolvedApplies' hook: "After another player's action destroys one of your Courtier or Shugenja Personalities".
    private bool? P2AfterActionApplies(string abilityId, int playerId, ResolvedActionFacts facts) => abilityId switch
    {
        EmperorP2Abilities.ChampionRevenge => facts.PlayerId != playerId && (LastActionDestroyedOwnKeyword(playerId, "Courtier") || LastActionDestroyedOwnKeyword(playerId, "Shugenja")),
        _ => null
    };
}

public sealed partial class MatchEngine
{
    // Test probes (issue #503).
    internal int RangedStrengthForTest(string abilityId, int playerId, string? sourceId) => EffectiveAttackStrength(abilityId, Player(playerId), null, sourceId);
    internal void DrawForTest(int playerId) => Draw(Player(playerId));
}

public sealed partial class MatchEngine
{
    internal bool ResolveEventForTest(int playerId, string cardId) { var card = Card(cardId); return ResolveEvent(Player(playerId), Player(playerId).Provinces[card.ProvinceIndex!.Value], card); }
}
