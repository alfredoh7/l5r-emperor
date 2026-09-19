namespace L5R.Core;

/// <summary>One independent target of a multi-target action (issue #336). An ability with Slots names its targets as an
/// ordered list instead of the single AbilityDefinition.Target: each slot has its own Category -- any category the single
/// Target accepts, or "player" for a chosen player -- its own Min..Max count ("one to three attachments" is Min 1, Max 3;
/// "up to three" is Min 0), and an optional TargetMustBe status. Slots are independent: each candidate is checked on its
/// own, exactly as it would be as the ability's only target, and the selection as a whole must satisfy every slot's count.
/// Label is the words a viewer shows for the slot.</summary>
public sealed record TargetSlot(string Id, string Category, string Label, int Min = 1, int Max = 1, TargetStatus? MustBe = null,
    string PlayerDefault = TargetSlot.Self)
{
    public const string Self = "self", Opponent = "opponent";
    public bool IsPlayer => Category == TargetSlotKinds.Player;
}

public static class TargetSlotKinds
{
    public const string Card = "card";
    public const string Player = "player";
}

/// <summary>What the acting player chose for one slot: the cards, or -- for a "player" slot -- the player.</summary>
public sealed record SlotSelection(string Slot, string[]? CardIds = null, int? PlayerId = null);

/// <summary>One slot as offered to the acting player: its counts and every legal candidate. Kind is "card" (CardIds holds the
/// legal cards, each checked on its own) or "player" (PlayerIds holds the legal players).</summary>
public sealed record TargetSlotOption(string Slot, string Label, string Kind, int Min, int Max, string[] CardIds, int[] PlayerIds);

public sealed partial class MatchEngine
{
    // The card a normalised command carries in TargetId: the first card of the first card slot. Every reader written for a
    // single-target action (Akodo Kakihara's "targets him", the before-action window, the targeting traits) then sees the
    // primary target of a multi-target one without knowing about slots; a reader that must see all of them uses ActionTargetIds.
    private static string? PrimarySlotCardId(AbilityDefinition ability, SlotSelection[]? selections)
    {
        foreach (var slot in ability.Slots ?? [])
            if (!slot.IsPlayer && selections?.FirstOrDefault(s => s.Slot == slot.Id)?.CardIds is { Length: > 0 } ids) return ids[0];
        return null;
    }

    private static GameCommand NormalizeSlotCommand(GameCommand command)
    {
        if (command.TargetId is not null || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Slots is null) return command;
        return PrimarySlotCardId(ability, command.Selections) is { } primary ? command with { TargetId = primary } : command;
    }

    // The cards chosen for a slot, in the order chosen (empty when the slot was left empty), and the chosen player.
    private IEnumerable<CardInstance> SlotCards(GameCommand command, string slot) =>
        (command.Selections?.FirstOrDefault(s => s.Slot == slot)?.CardIds ?? []).Select(id => Card(id));
    private static int? SlotPlayer(GameCommand command, string slot) => command.Selections?.FirstOrDefault(s => s.Slot == slot)?.PlayerId;

    // Every card a slot could name: all the cards in play, the acting player's own hand (a private zone the opponent's slot
    // never lists) and every public discard pile. Whether a candidate is legal is decided by ValidateSlotCard, never here.
    private IEnumerable<CardInstance> SlotCandidateCards(int playerId) =>
        _state.Cards.Values.Where(c => InPlay(c) || c.OwnerId == playerId && c.Zone == Zone.Hand || c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead)
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ValidateAbility's arm for a multi-target ability: the whole selection, slot by slot.
    private void ValidateSlots(GameCommand command, AbilityDefinition ability)
    {
        var slots = ability.Slots!;
        var selections = command.Selections ?? [];
        Require(command.CardIds is null or { Length: 0 } && command.TargetPlayerId is null, "A multi-target action carries its targets in its selections.");
        Require(selections.Select(s => s.Slot).Distinct().Count() == selections.Length && selections.All(s => slots.Any(x => x.Id == s.Slot)), "Choose each target once, for a target this action has.");
        Require(command.TargetId is null || command.TargetId == PrimarySlotCardId(ability, selections), "This action's target must be the first card chosen.");
        foreach (var slot in slots)
        {
            var selection = selections.FirstOrDefault(s => s.Slot == slot.Id);
            if (slot.IsPlayer)
            {
                Require(selection?.CardIds is null or { Length: 0 }, $"{slot.Label}: choose a player, not cards.");
                var count = selection?.PlayerId is null ? 0 : 1;
                Require(count >= slot.Min && count <= slot.Max, $"{slot.Label}: choose a player.");
                Require(selection?.PlayerId is null or 0 or 1, $"{slot.Label}: choose a real player.");
                continue;
            }
            var ids = selection?.CardIds ?? [];
            Require(selection?.PlayerId is null, $"{slot.Label}: choose cards, not a player.");
            Require(ids.Length >= slot.Min && ids.Length <= slot.Max, slot.Min == slot.Max ? $"{slot.Label}: choose exactly {slot.Min}."
                : $"{slot.Label}: choose {slot.Min} to {slot.Max}.");
            Require(ids.Distinct().Count() == ids.Length, $"{slot.Label}: choose different cards.");
            foreach (var id in ids) ValidateSlotCard(command, ability, slot, id);
        }
    }

    // One candidate of one slot, judged exactly as the ability's single Target of that category would be: the same
    // ValidateAbility, run for a copy of the ability whose Target is the slot's category. Its target check is the only new
    // work; the payment step is skipped (the whole command is priced once, by the caller).
    private void ValidateSlotCard(GameCommand command, AbilityDefinition ability, TargetSlot slot, string cardId)
    {
        Require(_state.Cards.ContainsKey(cardId), "Unknown card.");
        ValidateAbility(command with { TargetId = cardId, Selections = null },
            ability with { Target = slot.Category, Slots = null, TargetMustBe = slot.MustBe, RequireNoAttachments = false });
    }

    // The slots of a multi-target ability as the acting player is offered them, and the command that takes the first legal
    // choice everywhere (up to each slot's Min) -- null when some slot has too few legal candidates, with the first reason a
    // candidate was refused (or the slot's own shortfall) in Error.
    private (TargetSlotOption[] Options, GameCommand? Default, string? Error) BuildSlotOffer(GameCommand prototype, AbilityDefinition ability)
    {
        var options = new List<TargetSlotOption>(); var chosen = new List<SlotSelection>(); var complete = true;
        // Why candidates were refused, most common first: a refusal every candidate shares (the action's timing, its source
        // or its performer) is the reason the whole action is unavailable, and outnumbers any one candidate's own.
        var refusals = new Dictionary<string, int>();
        foreach (var slot in ability.Slots!)
        {
            if (slot.IsPlayer)
            {
                options.Add(new(slot.Id, slot.Label, TargetSlotKinds.Player, slot.Min, slot.Max, [], [0, 1]));
                if (slot.Min > 0) chosen.Add(new(slot.Id, PlayerId: slot.PlayerDefault == TargetSlot.Opponent ? 1 - prototype.PlayerId : prototype.PlayerId));
                continue;
            }
            var legal = new List<string>();
            foreach (var candidate in SlotCandidateCards(prototype.PlayerId))
            {
                try { ValidateSlotCard(prototype, ability, slot, candidate.Id); legal.Add(candidate.Id); }
                catch (RuleViolation e) { refusals[e.Message] = refusals.GetValueOrDefault(e.Message) + 1; }
            }
            options.Add(new(slot.Id, slot.Label, TargetSlotKinds.Card, slot.Min, slot.Max, legal.ToArray(), []));
            if (legal.Count < slot.Min) complete = false;
            else if (slot.Min > 0) chosen.Add(new(slot.Id, legal.Take(slot.Min).ToArray()));
        }
        var error = refusals.Count == 0 ? null : refusals.OrderByDescending(r => r.Value).First().Key;
        if (!complete) return (options.ToArray(), null, error ?? "No legal target.");
        return (options.ToArray(), NormalizeSlotCommand(prototype with { Selections = chosen.ToArray() }), null);
    }

    // AbilityActions' entry for a multi-target ability: one option whose Slots carry every legal candidate and whose Command
    // is the default selection (so a viewer that has no slot picker still submits a complete, legal command).
    private ActionOption SlotAbilityOption(GameCommand prototype, AbilityDefinition ability, string label, string? sourceId)
    {
        var (options, defaultCommand, error) = BuildSlotOffer(prototype, ability);
        if (defaultCommand is null) return new("useAbility", label, false, error ?? "No legal target.", prototype, sourceId, Slots: options);
        var quote = QuoteAbilityGold(defaultCommand);
        defaultCommand = defaultCommand with { SourceIds = quote is null ? [] : quote.Payments.FirstOrDefault()?.SourceIds };
        try { ValidateAbility(defaultCommand); }
        catch (RuleViolation e) { return new("useAbility", label, false, e.Message, defaultCommand, sourceId, Gold: quote, Slots: options); }
        return new("useAbility", label, true, null, defaultCommand, sourceId, Gold: quote, Slots: options);
    }
}
