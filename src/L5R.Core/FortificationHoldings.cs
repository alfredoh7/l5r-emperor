using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P15 (issue #719, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): Fortification Holdings.
//
//   A Holding with the Fortification keyword stands guard over one of its owner's Provinces: it enters play straightened (an ordinary Holding enters bowed), it is attached to the Province it came from, and it is
//   discarded the moment that Province is destroyed (a discard nothing can prevent: it is a direct move, like the Province card and Region that go with the Province).
//
//   ENTRY      SweepFortifications runs after every accepted command (Execute), before the entry-turn sweep: a Fortification Holding now in play with no entry turn yet is straightened and recorded in
//              MatchState.Fortifications (holding id -> province index). Purchase, a province recruit and a recruit from a discard pile all reach it with no per-path edit.
//   NO PROVINCE  A Holding that did not enter from a Province (LastProvinceEntryCardId is not this card) asks its owner which of their remaining Provinces it guards (a PendingEventChoice, kind
//              "p15-fortify", answered by choosing a province index); one remaining Province is chosen at once.
//   DESTROY    DestroyProvince calls DiscardFortificationsOf: every Fortification attached to that Province goes to its owner's Dynasty discard, its attachments to the Fate discard.
//   VIEW       ProvinceView.FortificationIds lists the Holdings attached to a Province (public: a Holding in play is face up). Null when none.

public sealed partial class MatchEngine
{
    internal const string FortificationKeyword = "Fortification";
    internal const string FortifyChoiceKind = "p15-fortify";

    /// <summary>The one sweep after every accepted command: drops records of Holdings that left play, then straightens and attaches each Fortification Holding that has just entered play.</summary>
    private void SweepFortifications()
    {
        if (_state.Fortifications is { } store)
        {
            foreach (var id in store.Keys.ToArray())
                if (!_state.Cards.TryGetValue(id, out var holding) || !InPlay(holding)) store.Remove(id);
            if (store.Count == 0) _state.Fortifications = null;
        }
        foreach (var card in _state.Cards.Values.Where(c => c.EnteredPlayTurn is null && c.Zone == Zone.Home && Definition(c).Type == CardType.Holding && Keyword(c, FortificationKeyword)
                     && _state.Fortifications?.ContainsKey(c.Id) != true).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            Fortify(card);
    }

    private void Fortify(CardInstance card)
    {
        var owner = Player(card.OwnerId);
        card.Bowed = false;
        var open = owner.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToList();
        int? index = _state.LastProvinceEntryCardId == card.Id && _state.LastProvinceEntryProvinceIndex is int entered && open.Contains(entered) ? entered : open.Count == 1 ? open[0] : null;
        if (index is int attached)
        {
            (_state.Fortifications ??= new(StringComparer.Ordinal))[card.Id] = attached;
            Emit("fortified", $"{Definition(card).Name} enters play straightened, guarding {owner.Name}'s province {attached + 1}.", owner.Id, card.Id);
            return;
        }
        if (open.Count == 0) return;
        (_state.Fortifications ??= new(StringComparer.Ordinal))[card.Id] = -1; // awaiting the owner's choice
        var pending = new PendingEventChoice { Kind = FortifyChoiceKind, EventCardId = card.Id, PlayerId = owner.Id, Origin = "deferred", ClosesTo = owner.Id, Candidates = [.. open.Select(i => i.ToString())], Prompt = $"{Definition(card).Name}: choose the province it guards." };
        if (_state.PendingChoice is null) OpenEventChoice(pending);
        else _state.PendingEventChoices.Add(pending);
    }

    /// <summary>DestroyProvince's call: every Fortification attached to this Province is discarded (to its owner's Dynasty discard, its attachments to the Fate discard). Nothing prevents it.</summary>
    private void DiscardFortificationsOf(int owner, int provinceIndex)
    {
        if (_state.Fortifications is not { } store) return;
        foreach (var id in store.Where(kv => kv.Value == provinceIndex && _state.Cards.TryGetValue(kv.Key, out var h) && h.OwnerId == owner).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray())
        {
            var holding = Card(id);
            foreach (var attachment in Attachments(id).ToArray()) { attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.Bowed = false; }
            holding.Zone = Zone.DynastyDiscard; holding.ProvinceIndex = null; holding.Bowed = false; holding.FaceUp = true;
            store.Remove(id);
            Emit("fortificationDiscarded", $"{Definition(holding).Name} was discarded with {Player(owner).Name}'s province {provinceIndex + 1}.", owner, id);
        }
        if (store.Count == 0) _state.Fortifications = null;
    }

    /// <summary>The Holdings that guard a Province, for the view (null when none).</summary>
    private string[]? FortificationsOf(int owner, int provinceIndex) =>
        _state.Fortifications is { } store && store.Where(kv => kv.Value == provinceIndex && _state.Cards.TryGetValue(kv.Key, out var h) && h.OwnerId == owner).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray() is { Length: > 0 } ids ? ids : null;

    // The answer to a Fortification's province question (an arm of ResolveEventChoiceCommand's default). True when this handled it.
    private bool ResolveFortifyChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != FortifyChoiceKind) return false;
        Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered provinces.");
        var index = int.Parse(command.TargetId!);
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        if (_state.Fortifications?.ContainsKey(pending.EventCardId) == true && Player(pending.PlayerId).Provinces[index] is { Destroyed: false })
        {
            _state.Fortifications[pending.EventCardId] = index;
            Emit("fortified", $"{Definition(Card(pending.EventCardId)).Name} guards {Player(pending.PlayerId).Name}'s province {index + 1}.", pending.PlayerId, pending.EventCardId);
        }
        ResumeAfterDeferredWindow(pending.ClosesTo);
        return true;
    }

    private IEnumerable<ActionOption> FortifyChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != FortifyChoiceKind) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        foreach (var index in pending.Candidates)
            yield return new("eventChoice", $"{name}: province {int.Parse(index) + 1}", true, null, new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: index), pending.EventCardId);
    }

    internal void SweepFortificationsForTest() => SweepFortifications();
    internal void DestroyProvinceForTestP15(int owner, int index) => DestroyProvince(owner, Player(owner).Provinces[index]);
    internal int? FortificationProvinceForTest(string holdingId) => _state.Fortifications is { } s && s.TryGetValue(holdingId, out var i) ? i : null;
}

public sealed partial class MatchState
{
    /// <summary>P15 (FortificationHoldings.cs): Fortification Holding id -> the index of the Province it guards (-1 while its owner has still to choose). Omitted from a saved state until one is in play.</summary>
    [JsonPropertyOrder(3100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Fortifications { get; set; }
}
