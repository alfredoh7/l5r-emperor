using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// SLEEP (oracle-l5r-10596, Promotional-Emperor; issue #667, cluster "duel-sleep") and the reusable BUDGETED MULTI-SELECT decision it needs.
//
// Scan: "Ritual Air Limited: Bow one or more of your target unbowed Shugenja. Discard an equal number of cards from the top of your Fate deck. Bow zero or more target Personalities with total
// Chi less than the total of the cards' Focus Values. Destroy this Spell."
//
// Mendon's confirmed ruling: "First, discard the fate cards and sum their value. Then, display a value equal to the sum of discarded fate focus values minus one. When the player selects
// personalities for bowing, subtract that from the running total displayed. The player may select any combination of personalities such that the value remaining is 0 or greater (they may not
// select additional personalities if the value would become negative). Present a 'clear selection' button and a 'confirm selection' button that, respectively, clear the selection or apply
// the effect of sleep to the targets."
//
// Sequence (all one action): the Shugenja are named with the action (a slot of 1 or more unbowed Shugenja of the acting player); on resolution they bow, an equal number of cards (a Gaijin
// Sorcerer counting as two, DuelSleepSorcerer.cs) are discarded from the top of the Fate deck, and the Focus Values are summed; then the acting player is handed a BUDGETED MULTI-SELECT:
//   * the budget is the Focus total minus one (the printed "total Chi LESS than the total"); each Personality chosen costs its Chi (current Chi), and the remaining value is shown as it falls;
//   * "budgetSelectAdd" (TargetId) adds an unselected Personality whose Chi fits the remaining value (an unaffordable one is listed disabled); "budgetSelectClear" empties the selection and
//     restores the full budget; "budgetSelectConfirm" bows every selected Personality, destroys the Spell and finishes the action. Confirming an empty selection ("zero or more") is legal;
//   * the selection lives in the engine (MatchState.PendingBudgetSelect), so a reconnecting client, the bot and the server all see the same running total. Client contract: PendingChoice.Type
//     "budgetSelect" (its Prompt carries the remaining value and the selection), the Confirm action's DecisionLabel is "Remaining value: N" and its DecisionCandidateIds are the selected cards.
//
// READINGS (not confirmed rulings): "an equal number" is counted over the Shugenja that actually bowed (a bow that is negated does not count); any Personality in play may be targeted, bowed
// or not and of either player (the scan says "target Personalities", not "unbowed"); the Spell is destroyed when the selection is confirmed (or at once when no Personality can be afforded).
// ======================================================================================================================================================================================

/// <summary>A budgeted multi-select in progress (Sleep): who chooses, what the budget was, what remains, and the Personalities chosen so far.</summary>
public sealed class PendingBudgetSelect
{
    public string Kind { get; set; } = DuelSleepAbilities.Kind;
    public string SourceCardId { get; set; } = "";
    public int PlayerId { get; set; }
    /// <summary>The sum of the discarded cards' Focus Values.</summary>
    public int Total { get; set; }
    /// <summary>The value still to spend: Total minus one minus the Chi of every Personality selected. Never below -1 (-1: nothing can be afforded).</summary>
    public int Remaining { get; set; }
    public List<string> Selected { get; set; } = [];
    public List<string> BowedBefore { get; set; } = [];
}

public sealed partial class MatchState
{
    // A decision: the acting player's open Sleep selection (see the registration in DuelSleepAbilities' hooks below).
    [JsonPropertyOrder(4290)]
    public PendingBudgetSelect? PendingBudgetSelect { get; set; }
}

public static class DuelSleepAbilities
{
    public const string SleepRitual = "sleep-ritual";
    public const string Kind = "sleep";
    public const string PendingType = "budgetSelect";
    public const string Add = "budgetSelectAdd", Clear = "budgetSelectClear", Confirm = "budgetSelectConfirm";
    public const string Shugenja = "shugenja";

    public static Dictionary<string, AbilityDefinition> AddDuelSleep(this Dictionary<string, AbilityDefinition> all)
    {
        all[SleepRitual] = new("Limited", "Ritual Air Limited: Bow one or more of your target unbowed Shugenja. Discard an equal number of cards from the top of your Fate deck. Bow zero or more target Personalities with total Chi less than the total of the cards' Focus Values. Destroy this Spell",
            "slots", Slots: [new(Shugenja, EmperorP6Abilities.OwnUnbowedShugenja, "Your unbowed Shugenja to bow", 1, 20)], ActionKeywords: ["Ritual"]);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Null: not Sleep. True: the budgeted selection opened and the action waits for it. False: nothing could be afforded, so the action is over.
    private bool? RunDuelSleep(string id, GameCommand command, PlayerState player, CardInstance? source, IEnumerable<string> bowedBeforeAction)
    {
        if (id != DuelSleepAbilities.SleepRitual) return null;
        var spell = source!;
        // 1. Bow the targeted Shugenja; only those that really bowed count (a negated bow is not "bowed").
        var bowed = new List<CardInstance>();
        foreach (var shugenja in SlotCards(command, DuelSleepAbilities.Shugenja).ToArray())
            if (InPlay(shugenja) && BowByEffect(shugenja, player.Id)) bowed.Add(shugenja);
        // 2. Discard an equal number of cards (a Gaijin Sorcerer counts as two Shugenja, never as double Chi) and sum their Focus Values.
        var count = RitualShugenjaCount(bowed, player.Id);
        var total = 0;
        for (var i = 0; i < count; i++) total += DiscardTopOfFateDeck(player) ?? 0;
        Emit("sleepDiscarded", $"{Definition(spell).Name}: {count} card(s) discarded from the top of {player.Name}'s Fate deck for a total Focus Value of {total}.", player.Id, spell.Id);
        // 3. The budgeted selection: Personalities with total Chi less than the total.
        var pending = new PendingBudgetSelect { SourceCardId = spell.Id, PlayerId = player.Id, Total = total, Remaining = total - 1, BowedBefore = bowedBeforeAction.ToList() };
        if (!BudgetCandidates(pending).Any(c => PersonalityChi(c) <= pending.Remaining))
        {
            Emit("budgetSelectNone", $"{Definition(spell).Name}: no Personality can be afforded with a remaining value of {Math.Max(0, pending.Remaining)}.", player.Id, spell.Id);
            if (spell.Zone == Zone.Attached) DestroyAttachment(spell);
            return false;
        }
        _state.PendingBudgetSelect = pending;
        PublishBudgetPrompt(pending);
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        Emit("budgetSelectOffered", $"{player.Name} chooses Personalities for {Definition(spell).Name} (remaining value {Math.Max(0, pending.Remaining)}).", player.Id, spell.Id);
        return true;
    }

    /// <summary>The Personalities a Sleep selection may name: every Personality in play, in a stable order.</summary>
    private IEnumerable<CardInstance> BudgetCandidates(PendingBudgetSelect pending) =>
        _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    private void PublishBudgetPrompt(PendingBudgetSelect pending)
    {
        var name = Definition(Card(pending.SourceCardId)).Name;
        var chosen = pending.Selected.Count == 0 ? "none chosen" : "chosen: " + string.Join(", ", pending.Selected.Select(id => Definition(Card(id)).Name));
        _state.PendingChoice = new(DuelSleepAbilities.PendingType, pending.PlayerId, pending.SourceCardId,
            $"{name}: remaining value {Math.Max(0, pending.Remaining)}. Choose Personalities to bow; each costs its Chi ({chosen}).", pending.PlayerId);
    }

    // ---- answering ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool HandleBudgetSelect(string type, GameCommand command)
    {
        if (type != DuelSleepAbilities.PendingType) return false;
        var pending = _state.PendingBudgetSelect ?? throw new RuleViolation("No selection is pending.");
        Require(command.PlayerId == pending.PlayerId, "Only the player choosing may answer.");
        Require(command.Type is DuelSleepAbilities.Add or DuelSleepAbilities.Clear or DuelSleepAbilities.Confirm, "Select Personalities, clear the selection, or confirm it.");
        switch (command.Type)
        {
            case DuelSleepAbilities.Add:
                {
                    Require(command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var card), "Choose a Personality.");
                    card = Card(command.TargetId!);
                    Require(BudgetCandidates(pending).Any(c => c.Id == card.Id), "That card cannot be chosen.");
                    Require(!pending.Selected.Contains(card.Id), $"{Definition(card).Name} is already chosen.");
                    var cost = PersonalityChi(card);
                    Require(cost <= pending.Remaining, $"{Definition(card).Name}'s Chi ({cost}) is more than the remaining value ({Math.Max(0, pending.Remaining)}).");
                    pending.Selected.Add(card.Id); pending.Remaining -= cost;
                    Emit("budgetSelectAdded", $"{Player(pending.PlayerId).Name} chose {Definition(card).Name} (Chi {cost}); remaining value {Math.Max(0, pending.Remaining)}.", pending.PlayerId, card.Id);
                    PublishBudgetPrompt(pending);
                    return true;
                }
            case DuelSleepAbilities.Clear:
                pending.Selected.Clear(); pending.Remaining = pending.Total - 1;
                Emit("budgetSelectCleared", $"{Player(pending.PlayerId).Name} cleared the selection; remaining value {Math.Max(0, pending.Remaining)}.", pending.PlayerId, pending.SourceCardId);
                PublishBudgetPrompt(pending);
                return true;
            default:
                ConfirmBudgetSelect(pending);
                return true;
        }
    }

    // Applies Sleep to the selection: each chosen Personality bows (a negated bow is skipped), the Spell is destroyed, and the action finishes as a search or look finishes one.
    private void ConfirmBudgetSelect(PendingBudgetSelect pending)
    {
        var spell = Card(pending.SourceCardId);
        _state.PendingBudgetSelect = null; _state.PendingChoice = null;
        foreach (var id in pending.Selected)
            if (Card(id) is { } card && InPlay(card) && BowByEffect(card, pending.PlayerId))
                Emit("sleepBowed", $"{Definition(spell).Name}: {Definition(card).Name} falls asleep and bows.", pending.PlayerId, card.Id);
        if (spell.Zone == Zone.Attached) DestroyAttachment(spell);
        Emit("budgetSelectConfirmed", $"{Player(pending.PlayerId).Name} confirmed {pending.Selected.Count} Personality(ies) for {Definition(spell).Name}.", pending.PlayerId, spell.Id);
        FinishDeferredAction(pending.PlayerId, "ability", pending.SourceCardId, pending.BowedBefore);
    }

    // ---- what the chooser is offered ---------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<ActionOption> BudgetSelectActions(int playerId)
    {
        if (_state.PendingBudgetSelect is not { } pending || pending.PlayerId != playerId) yield break;
        foreach (var card in BudgetCandidates(pending).Where(c => !pending.Selected.Contains(c.Id)))
        {
            var cost = PersonalityChi(card);
            var fits = cost <= pending.Remaining;
            yield return new(DuelSleepAbilities.Add, $"Bow {Definition(card).Name} · Chi {cost}", fits, fits ? null : $"Chi {cost} is more than the remaining value ({Math.Max(0, pending.Remaining)}).",
                new GameCommand(DuelSleepAbilities.Add, playerId, _state.Version, TargetId: card.Id), card.Id);
        }
        yield return new(DuelSleepAbilities.Clear, "Clear selection", pending.Selected.Count > 0, pending.Selected.Count > 0 ? null : "Nothing is selected.",
            new GameCommand(DuelSleepAbilities.Clear, playerId, _state.Version), pending.SourceCardId);
        yield return new(DuelSleepAbilities.Confirm, "Confirm selection", true, null, new GameCommand(DuelSleepAbilities.Confirm, playerId, _state.Version), pending.SourceCardId,
            DecisionCandidateIds: pending.Selected.ToArray(), DecisionLabel: $"Remaining value: {Math.Max(0, pending.Remaining)}");
    }

    // ---- registration ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterDuelSleepHooks()
    {
        EngineHooks.Abilities.Register("AddDuelSleep", 12401, static abilities => abilities.AddDuelSleep());
        EngineHooks.RunEffects.Register("RunDuelSleep", 12401, static (engine, context) => engine.RunDuelSleep(context.id, context.command, context.player, context.source, context.bowedBeforeAction));
        EngineHooks.PendingChoice.Register("HandleBudgetSelect", 12401, static (engine, type, command) => engine.HandleBudgetSelect(type, command));
        EngineHooks.DecisionState.Register(nameof(MatchState.PendingBudgetSelect), 12401, static state => state.PendingBudgetSelect is not null);
    }
}
