using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// WINTER SLED'S STATIC TRAIT (issue #667, item-wave-46): "While you control a Winter card, other players' cards' effects will not negate this Personality's movement."
//
// The engine has several independent sources that stop a Personality's movement, and none of them used to know WHOSE card is doing the negating. Every one that is another card's effect now asks
// ONE question, MovementNegationBarred(mover, negatingPlayerId): the mover carries a Winter Sled whose controller controls a Winter card, and the negating card is controlled by some player other than the
// Sled's controller. The sources, and the player each names:
//
//   * Negations a Reaction sets on the held action (NegatesEffect: Tamori Tamoko, Champion of Thunder's "all", The Pearl-Encrusted Staff, the Earth's Strength ...) and Request Authorization's move
//     record: NegatedEffect.ByPlayer, stamped with the Reaction's player as its effects finish (StampNegatedBy, ExecuteAbilityEffects). The delay store (Delay) reads the same stamp.
//   * Movement locks (A Quiet Transaction, Strength of the Tsunami ... MovementLockNegates): MovementLock.ByPlayer, the player of the action that set it (the card that holds a lock while it stands at a
//     battlefield, Moksha Patam, names its own controller); The Too Close to Home Event names its controller.
//   * Chikushudo's Trickery ("negate all movement to it") and Ambush ("other Personalities will not move there"): the player who played them (PerformersBattlefieldNegators; the Ambush's action).
//   * Kyuden Kitsune's negation (the Defender's card) and Ugulu no Oni (his controller).
//   * Sources that are the mover's OWN controller's cards -- Blessings of the Shi-Tien Yen-Wang's and Show of Restraint's shields, Twin Forks City, Hida Kaiji's trait, The Snow Has Teeth's, Shieldman,
//     Star-Crossed Lovers, Goju Asuka, Hotei's Contentment, Fudo's Charging tokens, Suikihime -- are not "other players' cards", so the Sled leaves them alone, as printed.
//
// READING (Mendon's "Correct. Any effect from another player's card that would move the personality is negated." reads like the opposite of the printed text; the PRINTED text is built and the
// ambiguity is recorded in docs/decisions/2026-10-05-667-performers.md). "A Winter card" is any card carrying the Winter keyword that its controller controls in play (a Holding, a Terrain, a face-up
// Event in play, the Stronghold ...). A negation the Sled bars is spent: a "move" entry that would have negated it is consumed, an "all" entry stays for the action's other effects.
// ======================================================================================================================================================================================

public sealed partial class MatchState
{
    /// <summary>Chikushudo's Trickery: for each battlefield whose movement is negated (EmperorRulingR1State.MovementNegated), the player who played it (Winter Sled asks whose card negates).</summary>
    [JsonPropertyOrder(4601)]
    public Dictionary<int, int> PerformersBattlefieldNegators { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // The Sled that protects this Personality now: attached to him, and its controller controls a Winter card.
    private CardInstance? SledOf(CardInstance mover)
    {
        if (Definition(mover).Type != CardType.Personality || !InPlay(mover)) return null;
        return Attachments(mover.Id).FirstOrDefault(a => Definition(a).Effect == PerformersGearAbilities.SledEffect && ControlsWinterCard(a.OwnerId));
    }

    private bool ControlsWinterCard(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && (InPlay(c) || c.Zone == Zone.EventInPlay) && Keyword(c, "Winter"));

    /// <summary>THE question: is a card controlled by <paramref name="negatingPlayerId"/> barred from negating <paramref name="mover"/>'s movement? An unknown negator is never barred.</summary>
    private bool MovementNegationBarred(CardInstance mover, int? negatingPlayerId)
    {
        if (SledOf(mover) is not { } sled) return false; // Mendon: no card's effect negates it -- the controller's own cards included; the player is kept for the log only
        Emit("movementNotNegated", $"{Definition(mover).Name}'s movement will not be negated ({Definition(sled).Name}).", mover.OwnerId, sled.Id);
        return true;
    }

    // ExecuteAbilityEffects' tail: every negation the action's effects just set is the acting player's.
    private void StampNegatedBy(int playerId)
    {
        for (var i = 0; i < _state.NegatedEffects.Count; i++)
            if (_state.NegatedEffects[i].ByPlayer is null) _state.NegatedEffects[i] = _state.NegatedEffects[i] with { ByPlayer = playerId };
    }

    // MovementLockNegates' term: every lock that holds on the card is another player's card's (null players, such as an unknown lock, hold it).
    private bool MovementLockBarredBySled(CardInstance card, MovementKinds kind)
    {
        if (SledOf(card) is null) return false;
        var negators = card.MovementLocks.Where(l => l.Turn == _state.TurnNumber && (l.Kinds & kind) != 0 && MovementLockHolds(l)).Select(l => l.ByPlayer).ToList();
        if (PerfectMomentLocks(card) && R1.PerfectMoment is { } moment) negators.Add(moment.PlayerId);
        if (P4GlobalMovementLock(kind)) negators.AddRange(EventsInPlay(EmperorP4WatchAbilities.TooCloseToHomeEffect).Select(e => (int?)e.OwnerId));
        if (SuikihimeMovementLocked(card, kind) || PromoMovementLocked(card, kind)) negators.Add(card.OwnerId);
        return negators.Count > 0 && negators.All(n => MovementNegationBarred(card, n));
    }

    // MovementToBattlefieldNegated's term: Chikushudo's Trickery on the battlefield and the Ambush fought there are the only sources; they bar the move when every one that holds is another player's.
    private bool SledBarsBattlefieldNegation(CardInstance card, int battlefield)
    {
        if (SledOf(card) is null) return false;
        var trickery = R1.MovementNegated.Contains(battlefield);
        var ambush = IvoryAmbushNegatesMove(card, battlefield);
        if (!trickery && !ambush) return false;
        if (trickery && !(_state.PerformersBattlefieldNegators.TryGetValue(battlefield, out var trickeryPlayer) && MovementNegationBarred(card, trickeryPlayer))) return false;
        if (ambush && !(R1.Aside?.Paused.Command is { } ambushCommand && MovementNegationBarred(card, ambushCommand.PlayerId))) return false;
        return true;
    }

    private void PerformersNoteBattlefieldNegator(int battlefield, int playerId) => _state.PerformersBattlefieldNegators[battlefield] = playerId;
}
