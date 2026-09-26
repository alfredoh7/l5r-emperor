namespace L5R.Core;

// Retained, public evidence of one duel. Nothing from a hidden hand/deck is captured here.
// Defaults on MatchView/PlayerView keep older transport snapshots readable.
public sealed record DuelRecord(string Id, string Stat, CardView Challenger, CardView Challenged,
    CardView[] Attachments, DuelReveal[] Reveals, DuelResult? Result, bool Complete,
    DuelChallenge? Challenge = null, DuelBeat[]? Beats = null);
public sealed record DuelChallenge(CardView? Source, string AbilityId, string AbilityLabel, bool Refusable, string LossTerms, string RefusalTerms);
// Public explanations captured around actual engine operations, in their execution order.
public sealed record DuelBeat(int Index, string Kind, CardView? Source, string[] Lines, string[] AffectedCardIds, string? SourceLabel = null);
public sealed record DuelReveal(long Sequence, string Kind, CardView[] Cards);
public sealed record DuelProvince(int PlayerId, int Index, int Before, int After);
public sealed record DuelResult(string Kind, string? WinnerId, string[] LoserIds,
    int ChallengerStat, int ChallengedStat, int ChallengerTotal, int ChallengedTotal,
    string[] EffectOrder, GameEvent[] Consequences, DuelProvince[] Provinces,
    long ResolutionVersion, Phase ReturnPhase, int ReturnPriority, string? ResolutionReason = null);

public sealed partial class MatchEngine
{
    private DuelChallenge DescribeChallenge(string abilityId, CardInstance? source, bool refusable)
    {
        var loss = abilityId switch {
            "steel-on-steel-duel" or "ichizo-exp-duel" or "shikei-duel" or "makoto-duel" => "Defeat: the losing unit is destroyed.",
            "daisuke-duel" => "Defeat: the losing unit moves home.",
            "training-duel-force" or "training-duel-honor" or "training-duel-open" or "training-duel-challenge" => "Defeat: the losing Personality gets −1 Force this turn.",
            _ => "The challenge card defines the consequences."
        };
        return new(source is null ? null : ViewCard(source), abilityId, AbilityCatalog.All[abilityId].Label,
            refusable, loss, refusable ? "Refusal ends this challenge without a duel." : "This challenge cannot be refused.");
    }

    private sealed record DuelEvidence(Dictionary<string, CardView> Cards, int[][] Provinces, int[] Honor, int EventCount, int? Favor);
    private DuelEvidence CaptureDuelEvidence() => new(
        _state.Cards.Values.Where(c => c.Zone is not (Zone.Hand or Zone.FateDeck or Zone.DynastyDeck) && c.FaceUp)
            .ToDictionary(c => c.Id, c => ViewCard(c)),
        // Effective strength (Regions, Terrains, Times of Strife's ceiling): what GetView shows, never the raw field.
        _state.Players.Select(p => p.Provinces.Select(EffectiveProvinceStrength).ToArray()).ToArray(),
        _state.Players.Select(p => p.Honor).ToArray(), _state.Events.Count, _state.FavorPlayerId);

    private void ExplainDuelEffect(string kind, CardView? source, Action apply)
    {
        var before = CaptureDuelEvidence();
        apply();
        RecordDuelChanges(before, kind, source, recordNoChange: true);
    }

    private void RecordDuelChanges(DuelEvidence before, string kind, CardView? source, bool recordNoChange = false, string? sourceLabel = null)
    {
        if (_state.LastDuel is not {} record) return;
        var lines = new List<string>(); var affected = new List<string>();
        foreach (var (id, prior) in before.Cards)
        {
            if (!_state.Cards.TryGetValue(id, out var current)) continue;
            if (prior.Zone == current.Zone) continue;
            var destination = current.Zone switch {
                Zone.Dead => "is destroyed", Zone.FateDiscard or Zone.DynastyDiscard => "is discarded",
                Zone.Home => "moves home", Zone.RemovedFromGame => "is removed from the game", _ => null };
            if (destination is not null) { lines.Add($"{prior.Definition!.Name} {destination}."); affected.Add(id); }
        }
        foreach (var player in _state.Players)
        {
            var changes = player.Provinces.Select((p,i) => EffectiveProvinceStrength(p)-before.Provinces[player.Id][i]).ToArray();
            if (changes.Length>0 && changes.All(d => d==changes[0]) && changes[0]!=0)
                lines.Add($"{player.Name}: all provinces {changes[0]:+0;−0} strength.");
            else for (var i=0;i<changes.Length;i++) if (changes[i]!=0)
                lines.Add($"{player.Name}: province {i+1} {changes[i]:+0;−0} strength.");
            var honor=player.Honor-before.Honor[player.Id];
            if (honor!=0) lines.Add($"{player.Name}: {honor:+0;−0} Honor.");
            var draws=_state.Events.Skip(before.EventCount).Count(e=>e.Type=="cardDrawn"&&e.PlayerId==player.Id);
            if (draws>0) lines.Add($"{player.Name} draws {draws} {(draws==1?"card":"cards")}.");
        }
        if (before.Favor!=_state.FavorPlayerId)
            lines.Add(_state.FavorPlayerId is {} owner ? $"{Player(owner).Name} takes the Imperial Favor." : "The Imperial Favor is relinquished.");
        // Public modifiers may change without moving a card (training penalties / Iaijutsu bonus).
        foreach (var (id, prior) in before.Cards)
        {
            if (!_state.Cards.TryGetValue(id, out var card) || card.Zone is Zone.Hand or Zone.FateDeck or Zone.DynastyDeck) continue;
            var now=ViewCard(card);
            if (now.Zone==prior.Zone && now.Force!=prior.Force) { lines.Add($"{prior.Definition!.Name}: Force {prior.Force} → {now.Force}."); affected.Add(id); }
        }
        if (lines.Count==0 && !recordNoChange) return;
        if (lines.Count==0) lines.Add("No change from this effect.");
        var beats=record.Beats??[];
        _state.LastDuel=record with { Beats=[..beats,new(beats.Length,kind,source,lines.ToArray(),affected.Distinct().ToArray(),sourceLabel)] };
    }

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
        var actions = new List<ActionOption>();
        void Add(GameCommand c, string label, string[]? targets = null) => actions.Add(new(c.Type, label, true, null, c, c.CardId, targets));
        GameCommand Command(string type, string? card = null, string? target = null, string? choice = null) =>
            new(type, playerId, _state.Version, CardId: card, TargetId: target, Choice: choice);
        // Conceding is always available, mid-duel included, exactly as QueryActions offers it elsewhere.
        if (playerId != _state.PriorityPlayerId)
            return [new("wait", $"Waiting for {Player(_state.PriorityPlayerId).Name}", false, "The other player has priority."),
                new("concede", "Concede", true, null, Command("concede"))];
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
                    if (!AbilityCatalog.All.TryGetValue(id, out var def) || def.Timing != "Reaction" || def.Trigger is not ("before-duel-begins" or "own-won-duel" or "own-personality-won-duel" or EmperorRemainingAAbilities.BeforeFocusEffects)) continue;
                    // He's Mine! (issue #492, ruling R5): one option per Samurai who may take the participant's place; the target is the participant.
                    if (id == "hes-mine-substitute")
                    {
                        foreach (var (substituteCommand, substituteLabel) in HesMineDuelOptions(card, playerId))
                        {
                            try { ValidateAbility(substituteCommand); Add(substituteCommand, substituteLabel); }
                            catch (RuleViolation) { }
                        }
                        continue;
                    }
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
                    if (focused.Count + P2FocusSubstitutions(playerId) < 4) // issue #503: Kakita Gonji's replaced focus counts against the cap
                    {
                        foreach (var gonji in GonjiFocusSources(playerId, duel)) Add(Command("focus", gonji.Id, choice: GonjiFocusChoice), $"Kakita Gonji · +4 duel stat instead of focusing ({Definition(gonji).Name})");
                        foreach (var id in Player(playerId).Hand) Add(Command("focus", id), $"Focus · {Definition(Card(id)).Name}");
                        if (Player(playerId).FateDeck.Count > 0) Add(Command("focus", choice: "deck"), "Focus from deck");
                        if (CanFocusDiscard(playerId, duel))
                            foreach (var c in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.FateDiscard or Zone.DynastyDiscard))
                                Add(Command("focus", c.Id, choice: "discard"), $"Focus from discard · {Definition(c).Name}");
                    }
                    foreach (var weapon in ReserveWeaponsFor(playerId, duel))
                        Add(Command("focus", weapon.Id, choice: "reserve"), $"Reserve Weapon · +3 duel stat instead of focusing ({Definition(weapon).Name})");
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
                case P2FocusChoiceStage: // issue #503 (EmperorP2Duel.cs)
                    foreach (var (p2Command, p2Label) in P2FocusChoiceActions(playerId)) Add(p2Command, p2Label);
                    break;
                case "OrderingEffects":
                    var ids = FocusEffectCardIds(duel).ToArray();
                    Add(new("orderDuelFocusEffects", playerId, _state.Version, CardIds: ids), "Resolve Focus Effects", ids);
                    break;
            }
        }
        Add(Command("concede"), "Concede");
        return actions.ToArray();
    }
}
