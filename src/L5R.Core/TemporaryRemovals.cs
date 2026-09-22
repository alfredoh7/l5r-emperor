namespace L5R.Core;

/// <summary>Keywords and Clan alignments taken off a card for a while (Red Crane Dojo: "remove Ninja and Shadowlands from him until the action ends"), with exactly what was
/// taken so the same is given back. <see cref="Until"/> is <see cref="TemporaryRemovals.Action"/> or <see cref="TemporaryRemovals.Turn"/>.</summary>
public sealed class TemporaryRemoval
{
    public string CardId { get; set; } = "";
    public List<string> Keywords { get; set; } = [];
    public List<string> Clans { get; set; } = [];
    public string Until { get; set; } = TemporaryRemovals.Action;
}

public static class TemporaryRemovals
{
    public const string Action = "action";
    public const string Turn = "turn";
}

// Temporary removal of keywords and Clan alignments (issue #416, Mendon's ruling on Red Crane Dojo). Removed for the rest of the ACTION in progress -- applied after the action has
// targeted the card, so an effect that is conditional on the target being Ninja or Shadowlands ("if the target is Shadowlands, destroy it") finds it is not -- and given back when
// the action has resolved. An action that never resolves (it was negated, or paused for a choice that is never made) gives them back at the end of the turn at the latest.
// Only what the card actually had is removed and only that is given back, so a permanent removal made meanwhile is never undone.
public sealed partial class MatchEngine
{
    private void RemoveKeywordsAndClans(CardInstance card, string until, string[] keywords, string[] clans)
    {
        var record = new TemporaryRemoval { CardId = card.Id, Until = until };
        foreach (var keyword in keywords)
            if (Keyword(card, keyword)) { card.RemovedKeywords.Add(keyword); record.Keywords.Add(keyword); }
        foreach (var clan in clans)
        {
            if (!HasClanAffiliation(card, clan)) continue;
            card.RemovedClanAffiliations.Add(clan); record.Clans.Add(clan);
            var clanKeyword = $"{clan} Clan";
            if (!card.RemovedKeywords.Contains(clanKeyword, StringComparer.Ordinal)) { card.RemovedKeywords.Add(clanKeyword); record.Keywords.Add(clanKeyword); }
        }
        if (record.Keywords.Count + record.Clans.Count == 0) return;
        _state.TemporaryRemovals.Add(record);
        Emit("keywordsRemoved", $"{Definition(card).Name} loses {string.Join(" and ", record.Keywords.Where(k => !k.EndsWith(" Clan", StringComparison.Ordinal)).Concat(record.Clans))} until the {until} ends.", card.OwnerId, card.Id);
    }

    // The action in progress has ended (or the turn has): what it took off comes back. Not while the action is still paused for a choice or a duel -- it has not ended.
    private void RestoreTemporaryRemovals(string until)
    {
        if (_state.TemporaryRemovals.Count == 0) return;
        if (until == TemporaryRemovals.Action && (_state.PendingEffectChoice is not null || _state.PendingDuel is not null)) return;
        foreach (var record in _state.TemporaryRemovals.Where(r => until == TemporaryRemovals.Turn || r.Until == TemporaryRemovals.Action).ToArray())
        {
            _state.TemporaryRemovals.Remove(record);
            if (!_state.Cards.TryGetValue(record.CardId, out var card)) continue;
            foreach (var keyword in record.Keywords) card.RemovedKeywords.Remove(keyword);
            foreach (var clan in record.Clans) card.RemovedClanAffiliations.Remove(clan);
            Emit("keywordsRestored", $"{Definition(card).Name} has his keywords again.", card.OwnerId, card.Id);
        }
    }
}
