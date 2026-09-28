namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P3 (continued): Easy Persuasion -- an entry seen by either player, and what was paid for it.
//   * WHAT WAS PAID FOR AN ENTRY: every Recruit payment of a Personality records its Gold on the card (Pay's hook, Moneylender's record generalised); an entry nothing was paid for (a
//     created Personality, a Personality put into play by an effect "ignoring costs") paid 0. An Experienced overlay is not an entry.
//   * A WINDOW AFTER A PERSONALITY ENTERS PLAY FOR EITHER PLAYER ("p3EntryReaction"): once the entry's own windows (Proclaim, the entering player's entry Reactions) have closed and the
//     command leaves nothing open, an alternating window opens for the entered Personality when either player holds a Reaction to it.
// ======================================================================================================================================================================================
public static class EmperorP3EntryAbilities
{
    public const string PersuasionReaction = "easy-persuasion-honor";
    public const string PersuasionMove = "easy-persuasion-move";
    public const string EntryTrigger = "p3-personality-entered-play";

    public static Dictionary<string, AbilityDefinition> AddEmperorP3Entries(this Dictionary<string, AbilityDefinition> all)
    {
        // Easy Persuasion (oracle-l5r-9905, The Shadow's Embrace): "Reaction: After a Personality enters play, if his controller paid less than his base Gold Cost for him, bow your performing
        // Merchant: The Personality's controller loses Honor equal to his base Personal Honor, or 3, whichever is lower." and "Battle: Pay Gold equal to a target unit's total Gold Cost minus 5:
        // Move it home. Its controller loses 2 Honor."
        all[PersuasionReaction] = new("Reaction", "After a Personality enters play, if his controller paid less than his base Gold Cost for him, bow your performing Merchant: The Personality's controller loses Honor equal to his base Personal Honor, or 3, whichever is lower",
            "none", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Merchant"], Trigger: EntryTrigger, Costs: ActionCost.BowPerformer);
        all[PersuasionMove] = new("Battle", "Pay Gold equal to a target unit's total Gold Cost minus 5: Move it home. Its controller loses 2 Honor", "personality",
            Cost: GoldCostRule.TargetUnitTotal(-5, floor: 0));
        return all;
    }
}

/// <summary>A Personality that entered play and the Gold paid for him, waiting for the window after its entry (Easy Persuasion).</summary>
public sealed record P3Entry(string CardId, int Paid);

public sealed class EmperorP3EntryState
{
    /// <summary>The Gold paid for each Personality being recruited (Pay's Recruit payments), read as he enters play.</summary>
    public Dictionary<string, int> PaidForEntry { get; set; } = [];
    public string? Overlaying { get; set; }
    public List<P3Entry> Awaiting { get; set; } = [];
    public P3Entry? Open { get; set; }
}

public sealed partial class MatchEngine
{
    private EmperorP3EntryState P3E => _state.P3.Entries;

    /// <summary>Pay's hook (RecordGoldPaidForEntry's neighbour): the Gold paid to recruit a Personality.</summary>
    private void P3RecordEntryPayment(int cost, PaymentSubject? paying)
    {
        if (paying is { Kind: PaymentKind.Recruit } && Definition(paying.Card).Type == CardType.Personality) P3E.PaidForEntry[paying.Card.Id] = cost;
    }

    /// <summary>Overlay's hook: the next entry effects are an overlay's, not an entry.</summary>
    private void P3NoteOverlay(CardInstance card) => P3E.Overlaying = card.Id;

    /// <summary>ApplyMandatoryEntryEffects' hook: a Personality entered play (an overlay excepted), with what was paid for him.</summary>
    private void P3NoteEntry(CardInstance card)
    {
        var paid = P3E.PaidForEntry.Remove(card.Id, out var amount) ? amount : 0;
        if (P3E.Overlaying == card.Id) { P3E.Overlaying = null; return; }
        if (Definition(card).Type != CardType.Personality) return;
        P3E.Awaiting.Add(new(card.Id, paid));
    }

    private ActionOption[] EntryWindowOptions(int playerId, string enteredId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("p3EntryReaction", playerId, enteredId, "After this Personality entered play: use a Reaction, or pass.");
        try { return AbilityActions(playerId).Where(o => o.Enabled).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    /// <summary>Apply's hook once a command leaves nothing open: the window after an entry, when a player holds a Reaction to it.</summary>
    private void OpenP3EntryWindow()
    {
        if (_state.PendingChoice is not null || _state.PendingDuel is not null || _state.PendingAttack is not null || _state.WinnerId is not null || P3E.Open is not null) return;
        while (P3E.Awaiting.Count > 0)
        {
            var entry = P3E.Awaiting[0];
            P3E.Awaiting.RemoveAt(0);
            if (!_state.Cards.TryGetValue(entry.CardId, out var entered) || !InPlay(entered)) continue;
            P3E.Open = entry;
            bool Holds(int p) => EntryWindowOptions(p, entry.CardId).Length > 0;
            if (!Holds(0) && !Holds(1)) { P3E.Open = null; continue; }
            var first = FirstReactionOpportunity(Holds);
            OpenAlternatingReactionWindow(new("p3EntryReaction", first, entry.CardId, $"After {Definition(entered).Name} entered play: either player may use a Reaction, or pass.", _state.PriorityPlayerId), first, "Entry reaction window opened.");
            return;
        }
    }

    private void ResolveP3EntryWindowCommand(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction to the entry, or pass.");
        var window = _state.PendingChoice!;
        if (command.Type == "useAbility")
        {
            UseAbility(command);
            if (_state.PendingChoice is null && P3E.Open is not null) { _state.PendingChoice = window with { PlayerId = _state.PriorityPlayerId }; _state.ConsecutivePasses = 0; }
            return;
        }
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        if (++_state.ConsecutivePasses < 2) { _state.PriorityPlayerId = 1 - _state.PriorityPlayerId; return; }
        Emit("reactionClosed", "Both players passed; the reaction window closed.");
        P3E.Open = null;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = window.ActingPlayerId ?? _state.PriorityPlayerId;
    }

    /// <summary>The trigger check (ValidateAbility): the window after an entry, the entered Personality paid for with less than his base Gold Cost.</summary>
    private bool P3EntryWindowApplies(GameCommand command) =>
        _state.PendingChoice?.Type == "p3EntryReaction" && P3E.Open is { } entry && _state.Cards.TryGetValue(entry.CardId, out var entered) && InPlay(entered)
        && entry.Paid < Definition(entered).GoldCost;

    private bool? RunEmperorP3Entries(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case EmperorP3EntryAbilities.PersuasionReaction:
                if (P3E.Open is { } entry && _state.Cards.TryGetValue(entry.CardId, out var entered))
                    GainHonor(entered.OwnerId, -Math.Min(Definition(entered).PersonalHonor, 3), Definition(source!).Name, source);
                return false;
            case EmperorP3EntryAbilities.PersuasionMove:
            {
                var loser = target!.OwnerId;
                MoveHome(target, false);
                GainHonor(loser, -2, Definition(source!).Name, source);
                return false;
            }
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP3Entries_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorP3Entries", 3400, static abilities => abilities.AddEmperorP3Entries());
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredP3EntryReaction(string type, GameCommand command)
    {
        if (type != "p3EntryReaction") return false;
        ResolveP3EntryWindowCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP3Entries_Rung4b4()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredP3EntryReaction", 2300, static (engine, type, command) => engine.HandleRegisteredP3EntryReaction(type, command));
    }
}
