using System.Diagnostics.CodeAnalysis;

namespace L5R.Core;

// Issue #917 (docs/decisions/2026-10-13-action-resolution-area.md, VALIDATION_REPORT finding 5): the temporary resolution area of the Comprehensive Rules, step A.
// A Strategy or Ring announced from the hand is placed there -- face up, out of play, public to both seats -- the moment its announcement is validated, and stays
// until its action ends (Tidy Up discards it; Sincerity / Honesty remove it from the game; a Ring or Kata that enters play leaves it). While it is there it is not
// in Player.Hand, so every window opened by the announcement, every hand count and every effect that reads the hand sees the hand WITHOUT it.
// Phase 1 is Strategies and Rings only. A hand-played Attack and a Dueling Strategy keep the issue #320 stopgap (the card is discarded as the window opens).
public sealed partial class MatchEngine
{
    /// <summary>True for a card played from the hand whose action has begun (the area) or not yet (the hand): the origin of a hand-played card, read after announcement.
    /// Announcement-time legality deliberately keeps testing <c>Zone.Hand</c>, which is also what stops a resolving card offering a second action.</summary>
    internal static bool PlayedFromHand([NotNullWhen(true)] CardInstance? card) => card is { Zone: Zone.Hand or Zone.Resolving };

    /// <summary>Announcement-time test that the source is in its player's hand -- or is the very card whose announced action is being re-validated while it waits in the area.</summary>
    private bool SourceInHand(CardInstance source, PlayerState player) => source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || IsResolving(source);
    private bool IsResolving(CardInstance card) => card.Zone == Zone.Resolving && _state.ResolutionArea.Any(e => e.CardId == card.Id);

    // The announced action's card moves from the hand into the area. Called by UseAbility right after ValidateAbility succeeded: an illegal announcement throws before this,
    // and ExecuteOne restores the original state, so a rejected play never leaves the hand.
    private void EnterResolutionArea(GameCommand command)
    {
        if (command.CardId is null || command.AbilityId is null || !_state.Cards.TryGetValue(command.CardId, out var card)) return;
        if (card.Zone != Zone.Hand || card.OwnerId != command.PlayerId || Definition(card).Type is not (CardType.Strategy or CardType.Ring)) return;
        var player = Player(command.PlayerId);
        if (!player.Hand.Remove(card.Id)) return;
        card.Zone = Zone.Resolving; card.FaceUp = true;
        _state.ResolutionArea.Add(new(card.Id, card.OwnerId, command.AbilityId));
        Emit("cardResolving", $"{player.Name}'s {Definition(card).Name} is placed in the resolution area.", player.Id, card.Id);
    }

    /// <summary>The one exit: the card left the area (discarded, removed from the game, entered play).</summary>
    private void LeaveResolutionArea(CardInstance card)
    {
        if (_state.ResolutionArea.Count > 0) _state.ResolutionArea.RemoveAll(e => e.CardId == card.Id);
    }

    // After every command: an entry whose card has moved on by a path that did not call LeaveResolutionArea is dropped; and a card still resolving once nothing is pending is
    // discarded -- "when the action has ended, any card still in the resolution area is discarded" (rule 1914). This is also the tail discard of an action that paused for a decision
    // (a look, an Either/Or choice) and whose resume path never spent its Strategy: those used to leave it in the hand.
    private void ReconcileResolutionArea()
    {
        if (_state.ResolutionArea.Count == 0) return;
        _state.ResolutionArea.RemoveAll(e => !_state.Cards.TryGetValue(e.CardId, out var c) || c.Zone != Zone.Resolving);
        if (_state.ResolutionArea.Count == 0 || HasOpenDecisionStateExceptArea()) return;
        foreach (var entry in _state.ResolutionArea.ToArray()) DiscardFate(Player(entry.OwnerId), Card(entry.CardId));
        _state.ResolutionArea.Clear();
    }

    private ResolutionEntryView[]? BuildResolutionAreaView()
    {
        if (_state.ResolutionArea.Count == 0) return null;
        return _state.ResolutionArea.Select((e, depth) => new ResolutionEntryView(ViewCard(Card(e.CardId)), e.OwnerId, e.AbilityId,
            AbilityCatalog.All.TryGetValue(e.AbilityId, out var ability) ? ability.Label : e.AbilityId, depth)).ToArray();
    }

    /// <summary>Test invariant: every entry's card is Resolving, every Resolving card has exactly one entry, and a non-empty area implies another decision state is open.</summary>
    internal string? ResolutionAreaInvariantViolation()
    {
        foreach (var entry in _state.ResolutionArea)
            if (!_state.Cards.TryGetValue(entry.CardId, out var c) || c.Zone != Zone.Resolving) return $"Resolution area entry {entry.CardId} is not in Zone.Resolving.";
        foreach (var card in _state.Cards.Values.Where(c => c.Zone == Zone.Resolving))
            if (_state.ResolutionArea.Count(e => e.CardId == card.Id) != 1) return $"Resolving card {card.Id} has no single resolution area entry.";
        if (_state.ResolutionArea.Count > 0 && !HasOpenDecisionStateExceptArea()) return "The resolution area is non-empty with no pending decision.";
        return null;
    }
}
