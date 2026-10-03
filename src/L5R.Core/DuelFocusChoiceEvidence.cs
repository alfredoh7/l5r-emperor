namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Record only explicit public choice outcomes, not private draw events or candidate lists.
    // This runs before the next Focus Effect, so sequential pauses retain engine execution order.
    private void RecordFocusChoiceBeat(int eventStart, CardView source)
    {
        if (_state.LastDuel is not { } record) return;
        var outcomes = _state.Events.Skip(eventStart).Where(e => e.Type is
            "focusEffectDeclined" or "focusEffectChosen" or "focusEffectPenalty" or
            "focusEffectRevenge" or "focusEffectDestroyed" or "focusEffectStraightened" or "focusEffectAttached" or "focusEffectTook").ToArray();
        if (outcomes.Length == 0) return;
        var affected = outcomes.Select(e => e.CardId).OfType<string>()
            .Where(id => _state.Cards.TryGetValue(id, out var card) && card.FaceUp &&
                card.Zone is not (Zone.Hand or Zone.FateDeck or Zone.DynastyDeck)).Distinct().ToArray();
        var beats = record.Beats ?? [];
        _state.LastDuel = record with
        {
            Beats = [..beats,
            new(beats.Length, "FocusEffect", source, outcomes.Select(e => e.Message).ToArray(), affected)]
        };
    }
}
