namespace L5R.Core;

/// <summary>One independent target of a multi-target action (issue #336). An ability with Slots names its targets as an
/// ordered list instead of the single AbilityDefinition.Target: each slot has its own Category -- any category the single
/// Target accepts, or "player" for a chosen player -- its own Min..Max count ("one to three attachments" is Min 1, Max 3;
/// "up to three" is Min 0), and an optional TargetMustBe status. Slots are independent: each candidate is checked on its
/// own, exactly as it would be as the ability's only target, and the selection as a whole must satisfy every slot's count.
/// Label is the words a viewer shows for the slot. Performing: the cards of this slot PERFORM the action ("one or more of your performing Tacticians", Dislodge the
/// Foe -- Mendon: N Tacticians may be designated as performers) and are not its targets: no targeted-by-action reaction is offered for them, they are not the
/// primary target, and a lock on one of them bars the action.
///
/// Two timings (Mendon's ruling, issue #336: conditional sentences are independent, and a conditional follow-up is prompted only when its
/// condition holds). An UP-FRONT slot (Resolution null) is chosen with the command, before anything happens: Yoshitada's enemy Personality, the
/// up-to-three attachments. A CONDITIONAL FOLLOW-UP slot (Resolution set) is not part of the selection at all: it names a decision the engine
/// opens when the action RESOLVES, and only if its condition holds -- Yoshitada's "choose a player who gains or loses 2 Honor" after the bow, only
/// if he is dishonorable; Fear Me!'s only if he moved. The decision reuses the pending effect choice (EffectChoice.cs): the acting player is
/// offered each of Resolution.Options as one action, and the action then finishes exactly as any action does.</summary>
public sealed record TargetSlot(string Id, string Category, string Label, int Min = 1, int Max = 1, TargetStatus? MustBe = null,
    ConditionalChoice? Resolution = null, bool Performing = false)
{
    /// <summary>The same card may be chosen more than once: each choice is one share ("attach them to one or more of your Personalities", Suiteiru no Oni: a host named twice takes two Followers; emperor proxy pass, issue #492).</summary>
    public bool Repeatable { get; init; }
    /// <summary>The slot names a card only to pay a cost with (Isawa Koiso's Shugenja to destroy, his card to discard; issue #503): not a target of the action.</summary>
    public bool CostOnly { get; init; }
    /// <summary>A later slot that reads this one's selection (Suiteiru no Oni's hosts, excluding the Personality destroyed as the cost) is offered candidates against its DEFAULT
    /// choice only -- never a probed alternate. Unlike Dislodge the Foe's performers (issue #429, the ordinary case, every earlier candidate tried so a later slot's legal
    /// candidates are never hidden behind one arbitrary default), the effect here always uses the one card actually named, so widening the later slot's offer past that single
    /// card would offer a choice (e.g. the Personality who could be destroyed) that is only ever legal against a target no one submitted.</summary>
    public bool ProbeDefaultOnlyForLaterSlots { get; init; }
    public bool IsPlayer => Category == TargetSlotKinds.Player;
    public bool AtResolution => Resolution is not null;
}

public static class TargetSlotKinds
{
    public const string Card = "card";
    public const string Player = "player";
    public const string FollowUp = "follow-up";
}

/// <summary>What the acting player chose for one slot: the cards, or -- for a "player" slot -- the player.</summary>
public sealed record SlotSelection(string Slot, string[]? CardIds = null, int? PlayerId = null);

/// <summary>One slot as offered to the acting player: its counts and every legal candidate. Kind is "card" (CardIds holds the
/// legal cards, each checked on its own), "player" (PlayerIds holds the legal players) or "follow-up". Timing "up-front" slots are answered in
/// the command's Selections; Timing "at-resolution" slots (Kind "follow-up", Min 0, Max 0) are NOT: Condition names when the engine will ask
/// (BranchCondition) and OptionLabels lists what the acting player will then be offered, so a viewer can say so beforehand.</summary>
public sealed record TargetSlotOption(string Slot, string Label, string Kind, int Min, int Max, string[] CardIds, int[] PlayerIds,
    string Timing = TargetSlotTimings.UpFront, string? Condition = null, string[]? OptionLabels = null);

public static class TargetSlotTimings
{
    public const string UpFront = "up-front";
    public const string AtResolution = "at-resolution";
}

public sealed partial class MatchEngine
{
    // The card a normalised command carries in TargetId: the first card of the first card slot. Every reader written for a
    // single-target action (Akodo Kakihara's "targets him", the before-action window, the targeting traits) then sees the
    // primary target of a multi-target one without knowing about slots; a reader that must see all of them uses ActionTargetIds.
    private static string? PrimarySlotCardId(AbilityDefinition ability, SlotSelection[]? selections)
    {
        foreach (var slot in ability.Slots ?? [])
            if (!slot.IsPlayer && !slot.Performing && !slot.CostOnly && selections?.FirstOrDefault(s => s.Slot == slot.Id)?.CardIds is { Length: > 0 } ids) return ids[0];
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
    // A face-up province card is public too (The Moon's Imperative, The Shadow's Embrace pass issue #476 batch 6: "face-up Personalities in provinces").
    private IEnumerable<CardInstance> SlotCandidateCards(int playerId) =>
        _state.Cards.Values.Where(c => InPlay(c) || c.OwnerId == playerId && c.Zone == Zone.Hand || c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead || c.Zone == Zone.Province && c.FaceUp || c.Zone == Zone.Underneath && (c.FaceUp || c.OwnerId == playerId) /* issue #503: The New Foundries' Spells, public; P21: a face-down under-card is private to its owner */)
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
            // A conditional follow-up is decided at resolution, never in the command.
            if (slot.AtResolution) { Require(selection is null, $"{slot.Label}: this is decided when the action resolves, not chosen now."); continue; }
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
            Require(slot.Repeatable || ids.Distinct().Count() == ids.Length, $"{slot.Label}: choose different cards.");
            foreach (var id in ids) ValidateSlotCard(command, ability, slot, id);
        }
        // Dislodge the Foe: every performing Tactician stands at the SAME battlefield, and the one Personality they oppose stands there too.
        if (ability is { Move.Movers: MoveSpec.PerformingSlot }) ValidatePerformersTogether(command, ability);
    }

    // The cards of the ability's performing slot(s), in the order chosen.
    private IEnumerable<CardInstance> PerformingSlotCards(GameCommand command, AbilityDefinition ability) =>
        (ability.Slots ?? []).Where(s => s.Performing).SelectMany(s => SlotCards(command, s.Id));

    private void ValidatePerformersTogether(GameCommand command, AbilityDefinition ability)
    {
        var performers = PerformingSlotCards(command, ability).ToArray();
        if (performers.Length == 0) return;
        Require(performers.All(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == performers[0].ProvinceIndex), "The performing Personalities must all stand at the same battlefield.");
        foreach (var target in (ability.Slots ?? []).Where(s => !s.Performing && !s.IsPlayer && !s.AtResolution).SelectMany(s => SlotCards(command, s.Id)))
            Require(target.Zone == Zone.Battlefield && target.ProvinceIndex == performers[0].ProvinceIndex, "The opposing Personality must stand where the performing Personalities do.");
    }

    // One candidate of one slot, judged exactly as the ability's single Target of that category would be: the same
    // ValidateAbility, run for a copy of the ability whose Target is the slot's category. Its target check is the only new
    // work; the payment step is skipped (the whole command is priced once, by the caller).
    private void ValidateSlotCard(GameCommand command, AbilityDefinition ability, TargetSlot slot, string cardId)
    {
        Require(_state.Cards.ContainsKey(cardId), "Unknown card.");
        // The selections stay on the command: a category that compares a candidate with the cards another slot holds (Dislodge the Foe's opposing Personality) reads them.
        ValidateAbility(command with { TargetId = cardId },
            ability with { Target = slot.Category, Slots = null, TargetMustBe = slot.MustBe, RequireNoAttachments = false });
    }

    // The slots of a multi-target ability as the acting player is offered them, and the command that takes the first legal
    // choice everywhere (up to each slot's Min) -- null when some slot has too few legal candidates, with the first reason a
    // candidate was refused (or the slot's own shortfall) in Error.
    private (TargetSlotOption[] Options, GameCommand? Default, string? Error) BuildSlotOffer(GameCommand prototype, AbilityDefinition ability)
    {
        var options = new List<TargetSlotOption>(); var chosen = new List<SlotSelection>(); var complete = true;
        // Each earlier card slot's own legal candidates, for the probes below.
        var offered = new List<(string Slot, string[] Candidates)>();
        // Why candidates were refused, most common first: a refusal every candidate shares (the action's timing, its source
        // or its performer) is the reason the whole action is unavailable, and outnumbers any one candidate's own.
        var refusals = new Dictionary<string, int>();
        foreach (var slot in ability.Slots!)
        {
            if (slot.AtResolution)
            {
                options.Add(new(slot.Id, slot.Label, TargetSlotKinds.FollowUp, 0, 0, [], [], TargetSlotTimings.AtResolution, slot.Resolution!.Condition, slot.Resolution.Options.Select(o => o.Label).ToArray()));
                continue;
            }
            if (slot.IsPlayer)
            {
                options.Add(new(slot.Id, slot.Label, TargetSlotKinds.Player, slot.Min, slot.Max, [], [0, 1]));
                if (slot.Min > 0) chosen.Add(new(slot.Id, PlayerId: prototype.PlayerId));
                continue;
            }
            var legal = new List<string>();
            // A category that compares a candidate with another slot's cards (Dislodge the Foe's opposing Personality, judged
            // against where the chosen Tacticians stand) would otherwise be judged against the DEFAULT earlier selection only,
            // hiding every candidate that is legal beside a different one. Each earlier choice is probed, so a candidate is
            // offered when some legal earlier selection admits it; the combination itself is still validated on submission.
            var probes = SlotProbes(prototype, chosen, offered);
            foreach (var candidate in SlotCandidateCards(prototype.PlayerId))
            {
                RuleViolation? refused = null;
                foreach (var probe in probes)
                {
                    try { ValidateSlotCard(probe, ability, slot, candidate.Id); refused = null; break; }
                    catch (RuleViolation e) { refused ??= e; }
                }
                if (refused is null) legal.Add(candidate.Id);
                else refusals[refused.Message] = refusals.GetValueOrDefault(refused.Message) + 1;
            }
            options.Add(new(slot.Id, slot.Label, TargetSlotKinds.Card, slot.Min, slot.Max, legal.ToArray(), []));
            if (!slot.ProbeDefaultOnlyForLaterSlots) offered.Add((slot.Id, legal.ToArray()));
            if (legal.Count < slot.Min) complete = false;
            else if (slot.Min > 0)
            {
                // Two independent slots drawing from the same candidate pool (Sudden Movement's "first"/"second", each any
                // Personality with a partner) must not default to the SAME card for both: that default would fail the
                // ability's own distinctness rule even though other, genuinely legal combinations exist. Prefer a candidate
                // no earlier up-front slot already picked; only repeat one when the slot has no other legal candidate left
                // (Repeatable slots, e.g. Suiteiru no Oni's hosts, still take the plain first-legal default).
                var alreadyChosen = chosen.SelectMany(c => c.CardIds ?? []).ToHashSet(StringComparer.Ordinal);
                var distinct = legal.Where(id => !alreadyChosen.Contains(id)).ToArray();
                chosen.Add(new(slot.Id, distinct.Length >= slot.Min ? distinct.Take(slot.Min).ToArray() : legal.Take(slot.Min).ToArray()));
            }
        }
        var error = refusals.Count == 0 ? null : refusals.OrderByDescending(r => r.Value).First().Key;
        if (!complete) return (options.ToArray(), null, error ?? "No legal target.");
        return (options.ToArray(), NormalizeSlotCommand(prototype with { Selections = chosen.ToArray() }), null);
    }

    // The default selection, plus one variant per alternative single choice of each earlier card slot. Bounded: a table with many
    // candidates is probed against a capped number of variants, the default always among them.
    private static GameCommand[] SlotProbes(GameCommand prototype, List<SlotSelection> chosen, List<(string Slot, string[] Candidates)> offered)
    {
        const int limit = 48;
        var probes = new List<GameCommand> { prototype with { Selections = chosen.ToArray() } };
        foreach (var (slotId, candidates) in offered)
        {
            var current = chosen.FirstOrDefault(c => c.Slot == slotId);
            if (current?.CardIds is null) continue;
            foreach (var candidate in candidates)
            {
                if (current.CardIds.Contains(candidate, StringComparer.Ordinal)) continue;
                if (probes.Count >= limit) return probes.ToArray();
                probes.Add(prototype with { Selections = chosen.Select(c => c.Slot == slotId ? c with { CardIds = [candidate] } : c).ToArray() });
            }
        }
        return probes.ToArray();
    }

    // AbilityActions' entry for a multi-target ability: one option whose Slots carry every legal candidate and whose Command
    // is the default selection (so a viewer that has no slot picker still submits a complete, legal command).
    private ActionOption SlotAbilityOption(GameCommand prototype, AbilityDefinition ability, string label, string? sourceId)
    {
        var (options, defaultCommand, error) = BuildSlotOffer(prototype, ability);
        if (defaultCommand is null) return new("useAbility", label, false, error ?? "No legal target.", prototype, sourceId, Slots: options);
        // Mutual Support's default is a pair with different Clans (issue #622, TfStrategiesB.cs); issue #503: an aggregate condition's default (EmperorP6.cs).
        defaultCommand = TfStrategiesBDefaultSelection(defaultCommand, ability, options) ?? defaultCommand;
        defaultCommand = EmperorP6DefaultSelection(defaultCommand, ability, options) ?? defaultCommand;
        var quote = QuoteAbilityGold(defaultCommand);
        defaultCommand = defaultCommand with { SourceIds = quote is null ? [] : quote.Payments.FirstOrDefault()?.SourceIds };
        try { ValidateAbility(defaultCommand); }
        catch (RuleViolation e) { return new("useAbility", label, false, e.Message, defaultCommand, sourceId, Gold: quote, Slots: options); }
        return new("useAbility", label, true, null, defaultCommand, sourceId, Gold: quote, Slots: options);
    }
}
