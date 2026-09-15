namespace L5R.Core;

// Retained, public evidence of one duel. Nothing from a hidden hand/deck is captured here.
// Defaults on MatchView/PlayerView keep older transport snapshots readable.
public sealed record DuelRecord(string Id, string Stat, CardView Challenger, CardView Challenged,
    CardView[] Attachments, DuelReveal[] Reveals, DuelResult? Result, bool Complete);
public sealed record DuelReveal(long Sequence, string Kind, CardView[] Cards);
public sealed record DuelProvince(int PlayerId, int Index, int Before, int After);
public sealed record DuelResult(string Kind, string? WinnerId, string[] LoserIds,
    int ChallengerStat, int ChallengedStat, int ChallengerTotal, int ChallengedTotal,
    string[] EffectOrder, GameEvent[] Consequences, DuelProvince[] Provinces,
    long ResolutionVersion, Phase ReturnPhase, int ReturnPriority);

public sealed partial class MatchEngine
{
    private void RecordDuelReveal(string kind)
    {
        var duel = _state.PendingDuel!;
        var record = _state.LastDuel!;
        _state.LastDuel = record with { Reveals = [..record.Reveals,
            new(_state.Events[^1].Sequence, kind, duel.ChallengerFocused.Concat(duel.ChallengedFocused)
                .Select(id => ViewCard(Card(id))).ToArray())] };
    }

    private void RefreshDuelRecord()
    {
        if (_state.LastDuel is not { Complete: false, Result: { } result } record) return;
        // Reactions belong to the same presentation, but do not change the resolved totals.
        // Retain only public duel consequences, never private draw events from aftermath reactions.
        var consequences = _state.Events.Where(e => e.Sequence >= (result.Consequences.FirstOrDefault()?.Sequence ?? long.MaxValue)
            && e.Type is not ("draw" or "duelFocused")).Select(e => e with { CardId =
                e.CardId is { } id && _state.Cards.TryGetValue(id, out var card) && card.Zone is Zone.Hand or Zone.FateDeck or Zone.DynastyDeck ? null : e.CardId }).ToArray();
        _state.LastDuel = record with { Complete = _state.PendingDuel is null && _state.PendingChoice is null,
            Result = result with { Consequences = consequences, ReturnPhase = _state.Phase, ReturnPriority = _state.PriorityPlayerId } };
    }

    private ActionOption[] QueryDuelActions(int playerId)
    {
        if (playerId != _state.PriorityPlayerId)
            return [new("wait", $"Waiting for {Player(_state.PriorityPlayerId).Name}", false, "The other player has priority.")];
        var actions = new List<ActionOption>();
        void Add(GameCommand c, string label, string[]? targets = null) => actions.Add(new(c.Type, label, true, null, c, c.CardId, targets));
        GameCommand Command(string type, string? card = null, string? target = null, string? choice = null) =>
            new(type, playerId, _state.Version, CardId: card, TargetId: target, Choice: choice);
        if (_state.PendingChoice is { Type: "overconfidentDraw" })
        {
            Add(Command("overconfidentDraw", choice: "draw"), "Draw a card");
            Add(Command("overconfidentDraw", choice: "decline"), "Pass reaction");
        }
        else if (_state.PendingChoice is { Type: "duelReaction" or "duelWonReaction" })
        {
            // All currently supported duel reactions are untargeted. ValidateAbility is the shared
            // gate for timing, owner, source zone, costs and per-card use limits.
            foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId))
                foreach (var id in Definition(card).Abilities ?? [])
                {
                    if (!AbilityCatalog.All.TryGetValue(id, out var def) || def.Timing != "Reaction" || def.Trigger is not ("before-duel-begins" or "own-won-duel")) continue;
                    var command = new GameCommand("useAbility", playerId, _state.Version, CardId: card.Id, AbilityId: id);
                    try { ValidateAbility(command); Add(command, def.Label); }
                    catch (RuleViolation) { }
                }
            Add(Command("declineReaction"), "Pass reaction");
        }
        else if (_state.PendingDuel is { } duel)
        {
            var focused = playerId == duel.ChallengerPlayerId ? duel.ChallengerFocused : duel.ChallengedFocused;
            switch (duel.Stage)
            {
                case "AwaitingAccept":
                    Add(Command("acceptDuel"), "Accept challenge");
                    if (duel.Refusable) Add(Command("declineDuel"), "Refuse challenge");
                    break;
                case "Focusing":
                    if (focused.Count < 4)
                    {
                        foreach (var id in Player(playerId).Hand) Add(Command("focus", id), $"Focus · {Definition(Card(id)).Name}");
                        if (Player(playerId).FateDeck.Count > 0) Add(Command("focus", choice: "deck"), "Focus from deck");
                        if (CanFocusDiscard(playerId, duel))
                            foreach (var c in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.FateDiscard or Zone.DynastyDiscard))
                                Add(Command("focus", c.Id, choice: "discard"), $"Focus from discard · {Definition(c).Name}");
                    }
                    Add(Command("strike"), "Strike");
                    break;
                case "DuelistRefocus":
                    Add(Command("duelistRefocus"), "Keep focused cards");
                    foreach (var id in focused)
                    {
                        foreach (var replacement in Player(playerId).Hand)
                            Add(Command("duelistRefocus", id, replacement), $"Replace {Definition(Card(id)).Name} with {Definition(Card(replacement)).Name}");
                        if (Player(playerId).FateDeck.Count > 0)
                            Add(Command("duelistRefocus", id, choice: "deck"), $"Replace {Definition(Card(id)).Name} from deck");
                    }
                    break;
                case "OrderingEffects":
                    var ids = FocusEffectCardIds(duel).ToArray();
                    Add(new("orderDuelFocusEffects", playerId, _state.Version, CardIds: ids), "Resolve Focus Effects", ids);
                    break;
            }
        }
        return actions.ToArray();
    }
}
