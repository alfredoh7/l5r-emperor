namespace L5R.Core;

// ======================================================================================================================================================================================
// REGRET (oracle-l5r-9923, The Shadow's Embrace) and EMBRACING VIRTUE (oracle-l5r-2313, War of Honor) -- issue #619, Mendon's readings.
//
// EMBRACING VIRTUE: "Before an End Phase begins, if this card is in your discard pile: You may put it in your hand."
//   * Asked at the top of EndTurn (BeginEmbracingVirtue), before the End Phase's draw and hand-size discard: a player who takes it back on their own turn may draw above their hand size and so be forced
//     to discard. "An End Phase" is any player's, so each End Phase asks every player with a copy in his Fate discard pile (the active player first), one Either/Or per copy. Each copy is asked once per turn.
//
// REGRET:
//   * FOCUS EFFECT. "After this duel ends, its winner's controller loses 1 Honor for each card he focused": the number of cards the winner's side focused (PendingDuel.ChallengerFocused/ChallengedFocused). A
//     Gonji-style "+4 to his duel stat instead of focusing" adds nothing to those lists, so it is not a focus and is not counted. A tie has no winner, so nothing is lost.
//   * OPEN. "Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs." Foothills Keep's attach from a discard pile (AttachAction.cs), for
//     any number of cards: one slot of the cards, one repeatable slot of Personalities (the i-th Personality takes the i-th card; name a Personality twice for two cards). The price is every card's own Equip
//     cost for its own Personality (AttachSlotsCost), paid at once. The cards are placed in order, so the legality is judged as they will really stand -- an Armor or Weapon cap, Unique and Singular count the
//     cards placed before them (ValidateRegretAttach). This is NOT Equip (Battle): no additional action, and a Spell may not be attached by it (Types are Follower and Item only). An action printed on a card
//     attached this way is usable at the player's very next action, as with any attachment: nothing here delays it.
//   * ENTERING PLAY. Each attached card then opens its own "after this enters play" window, one after the other (RegretEntryQueue): the next opens when the one before it has closed.
// ======================================================================================================================================================================================

public static class RegretEmbracingVirtueAbilities
{
    public const string RegretFocus = "regret-focus", RegretAttach = "regret-attach";
    /// <summary>Embracing Virtue's Effect: a trait with no action of its own (it is read from the discard pile at the end of a turn).</summary>
    public const string EmbracingVirtueEffect = "embracing-virtue-return";
    /// <summary>The Either/Or spec of one copy's "you may put it in your hand".</summary>
    public const string EmbracingVirtueSpec = "embracing-virtue-return";
    /// <summary>The cards to attach, and the Personalities they go to. The Personalities slot is called "personality" so the ordinary Attach legality (ValidateAttach) judges the first pair.</summary>
    public const string AttachmentsSlot = "attachments", HostsSlot = "personality";
    private const int MostAttachments = 99;

    public static Dictionary<string, AbilityDefinition> AddRegretEmbracingVirtue(this Dictionary<string, AbilityDefinition> all)
    {
        all[RegretFocus] = new("FocusEffect", "As a Focus Effect: After this duel ends, its winner's controller loses 1 Honor for each card he focused", "none");
        all[RegretAttach] = new("Open", "Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs", "slots",
            Cost: GoldCostRule.AttachSlotsCost(),
            Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.PersonalitySlot, [CardType.Follower, CardType.Item]),
            Slots: [new(AttachmentsSlot, "attachment-in-discard", "One or more of your Followers or Items in your discard pile", 1, MostAttachments),
                new(HostsSlot, "own-personality", "A Personality of yours for each card, in the same order (name one twice for two cards)", 1, MostAttachments) { Repeatable = true }]);
        return all;
    }
}

/// <summary>Regret's attach in progress: the attached cards still owed their entering-play window, and who attached them.</summary>
public sealed class RegretState
{
    public List<string> EntryQueue { get; set; } = [];
    public int PlayerId { get; set; }
}

public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1130)]
    public RegretState Regret { get; set; } = new();
}

public sealed partial class MatchEngine
{
    private const string EmbracingVirtueTurnKey = "embracing-virtue:turn";

    // ---- Embracing Virtue ------------------------------------------------------------------------------------------------------------------------------------------------------

    // EndTurn's first look: one question per copy in a discard pile not yet asked this turn. True when it paused for the answer.
    private bool BeginEmbracingVirtue()
    {
        foreach (var playerId in new[] { _state.ActivePlayerId, 1 - _state.ActivePlayerId })
            foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.FateDiscard && Definition(c).Effect == RegretEmbracingVirtueAbilities.EmbracingVirtueEffect)
                .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            {
                if (card.AbilityUses.TryGetValue(EmbracingVirtueTurnKey, out var asked) && asked == _state.TurnNumber) continue;
                card.AbilityUses[EmbracingVirtueTurnKey] = _state.TurnNumber;
                if (OpenEitherOr(RegretEmbracingVirtueAbilities.EmbracingVirtueSpec, card, playerId, playerId, null, "end-turn")) return true;
            }
        return false;
    }

    private string EmbracingVirtuePrompt(string source) => $"{source}: put it in your hand before the End Phase begins?";

    private EitherOrOption[] EmbracingVirtueOptions(PendingEitherOr p)
    {
        var card = Card(p.SourceCardId);
        return [
            new("return", $"Put {Definition(card).Name} in your hand", "It leaves your discard pile for your hand, before the End Phase's draw and hand-size discard.", EitherOrKind.Choose, 0,
                card.Zone == Zone.FateDiscard && !P2CardIntoHandBlocked(p.DeciderPlayerId), "It is not in your discard pile, or cards may not be put into your hand."),
            new("decline", "Leave it in your discard pile", "Nothing happens.", EitherOrKind.Choose, 0, true, null)
        ];
    }

    private bool ApplyEmbracingVirtue(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != RegretEmbracingVirtueAbilities.EmbracingVirtueSpec) return false;
        Emit("eitherOrChosen", option.Key == "return" ? $"{decider.Name} put {Definition(source).Name} in their hand." : $"{decider.Name} left {Definition(source).Name} in their discard pile.", decider.Id, source.Id);
        if (option.Key == "return" && source.Zone == Zone.FateDiscard)
        {
            decider.Hand.Add(source.Id); source.Zone = Zone.Hand; source.FaceUp = true; NoteCardIntoHand(decider.Id);
        }
        return true;
    }

    // The question was answered: the turn goes on ending, and asks the next copy, if any (each copy's stamp keeps it from being asked twice).
    private bool FinishEmbracingVirtue(PendingEitherOr p)
    {
        if (p.SpecId != RegretEmbracingVirtueAbilities.EmbracingVirtueSpec) return false;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = _state.ActivePlayerId;
        EndTurn();
        return true;
    }

    // ---- Regret: the Focus Effect ----------------------------------------------------------------------------------------------------------------------------------------------

    // ApplyDuelFocusEffectCore's hook. The winner's controller loses Honor equal to the cards his side focused, counted now (a Duelist's refocus left the list as it ended).
    private void ApplyRegretFocusEffect(string abilityId, int? winnerPlayerId)
    {
        if (abilityId != RegretEmbracingVirtueAbilities.RegretFocus || winnerPlayerId is not int winner || _state.PendingDuel is not { } duel) return;
        var focused = duel.ChallengerPlayerId == winner ? duel.ChallengerFocused : duel.ChallengedFocused;
        if (focused.Count > 0) GainHonor(winner, -focused.Count, "Regret");
    }

    // ---- Regret: the Open attach ------------------------------------------------------------------------------------------------------------------------------------------------

    private CardInstance[] RegretSlotCardsOrNone(GameCommand command, string slot) =>
        (command.Selections?.FirstOrDefault(s => s.Slot == slot)?.CardIds ?? []).Select(id => _state.Cards.TryGetValue(id, out var card) ? card : null).OfType<CardInstance>().ToArray();

    private CardInstance? RegretFirstHost(GameCommand command) => RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.HostsSlot).FirstOrDefault();

    // GoldCostBasis.AttachSlotsCost: every attachment's own Equip cost for the Personality it goes to (a discard pile earns no "from your hand" discount), added up.
    private AbilityCost RegretAttachSlotsCost(GameCommand command, GoldCostRule rule)
    {
        var attachments = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.AttachmentsSlot);
        var hosts = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.HostsSlot);
        if (attachments.Length == 0 || hosts.Length == 0)
            return new(rule, 0, "Choose the cards to attach and the Personalities they go to, to see this cost.", null, true, null);
        if (attachments.Length != hosts.Length)
            return new(rule, 0, "Name one of your Personalities for each card you attach.", "Name one of your Personalities for each card you attach.", false, null);
        var parts = new List<string>(); var total = 0;
        for (var i = 0; i < attachments.Length; i++)
        {
            var (cost, basis, _) = EquipGoldCost(command.PlayerId, attachments[i], hosts[i], fromHand: false);
            total += cost; parts.Add(basis);
        }
        return new(rule, total, parts.Count == 1 ? parts[0] : $"{string.Join(" + ", parts)} = {total}", null, false, attachments[0]);
    }

    // The attach, judged as one: each pair is the ordinary Equip legality (RequireAttachable), with the cards before it already standing where they will -- so two Weapons for one Personality,
    // two Armor, or two copies of a Unique card are refused as a whole. The trial placement is undone however the check ends.
    private void ValidateRegretAttach(string id, GameCommand command, PlayerState player)
    {
        if (id != RegretEmbracingVirtueAbilities.RegretAttach) return;
        var attachments = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.AttachmentsSlot);
        var hosts = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.HostsSlot);
        Require(attachments.Length == hosts.Length, "Name one of your Personalities for each card you attach, in the same order.");
        var placed = new List<(CardInstance Card, Zone Zone, string? Host)>();
        try
        {
            for (var i = 0; i < attachments.Length; i++)
            {
                RequireAttachable(attachments[i], hosts[i], player.Id);
                placed.Add((attachments[i], attachments[i].Zone, attachments[i].AttachedTo));
                attachments[i].Zone = Zone.Attached; attachments[i].AttachedTo = hosts[i].Id;
            }
        }
        finally { foreach (var (card, zone, host) in placed) { card.Zone = zone; card.AttachedTo = host; } }
    }

    // RunEffects hook. The Gold was paid. Every pair that is still legal is attached in order, then the entering-play windows open one at a time. A Strategy played from hand is spent here when a
    // window opens (the generic tail does not run), as Inspirational Address's attach does.
    private bool? RunRegret(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (id != RegretEmbracingVirtueAbilities.RegretAttach) return null;
        var attachments = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.AttachmentsSlot);
        var hosts = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.HostsSlot);
        var queue = new List<string>();
        for (var i = 0; i < attachments.Length && i < hosts.Length; i++)
        {
            var attachment = attachments[i]; var host = hosts[i];
            try
            {
                Require(attachment.Zone == Zone.FateDiscard && attachment.OwnerId == player.Id, $"{Definition(attachment).Name} is no longer in your discard pile.");
                RequireAttachable(attachment, host, player.Id);
            }
            catch (RuleViolation refused)
            {
                Emit("attachSkipped", $"{Definition(attachment).Name} was not attached: {refused.Message}", player.Id, attachment.Id);
                continue;
            }
            PlaceAttachment(attachment, host, player);
            queue.Add(attachment.Id);
        }
        _state.Regret.EntryQueue = queue; _state.Regret.PlayerId = player.Id;
        if (!AdvanceRegretEntryQueue()) return false;
        SpendHandPlayedCard(player, source);
        return true;
    }

    // Opens the next attached card's entering-play window (its mandatory entry effects apply either way); true when one is open now.
    private bool AdvanceRegretEntryQueue()
    {
        var regret = _state.Regret;
        while (regret.EntryQueue.Count > 0)
        {
            var id = regret.EntryQueue[0]; regret.EntryQueue.RemoveAt(0);
            if (!_state.Cards.TryGetValue(id, out var card) || card.Zone != Zone.Attached) continue;
            _attachingFromPileId = id; // Clearing Crew: an attach from a discard pile is not an entry "from your hand"
            try
            {
                if (!OpenEntryReactionWindow(regret.PlayerId, card, 1 - regret.PlayerId)) continue;
                _state.PriorityPlayerId = regret.PlayerId;
                return true;
            }
            finally { _attachingFromPileId = null; }
        }
        return false;
    }

    // HandleRegisteredEntryReaction's hook: the window that was open has closed, so the next card's may open.
    private void ContinueRegretEntryQueue()
    {
        if (_state.PendingChoice is not null || _state.Regret.EntryQueue.Count == 0) return;
        AdvanceRegretEntryQueue();
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRegretEmbracingVirtueHooks()
    {
        EngineHooks.Abilities.Register("AddRegretEmbracingVirtue", 10300, static abilities => abilities.AddRegretEmbracingVirtue());
        EngineHooks.ValidateLate.Register("ValidateRegretAttach", 10300, static (engine, context) => engine.ValidateRegretAttach(context.id, context.command, context.player));
        EngineHooks.RunEffects.Register("RunRegret", 10300, static (engine, context) => engine.RunRegret(context.id, context.command, context.player, context.source));
        EngineHooks.EitherOrPrompt.Register("EmbracingVirtueEitherOrPrompt", 10300, static (engine, p, source, affected) => p.SpecId == RegretEmbracingVirtueAbilities.EmbracingVirtueSpec ? engine.EmbracingVirtuePrompt(source) : null);
        EngineHooks.EitherOrOptions.Register("EmbracingVirtueEitherOrOptions", 10300, static (engine, p, affected, source, payable) => p.SpecId == RegretEmbracingVirtueAbilities.EmbracingVirtueSpec ? engine.EmbracingVirtueOptions(p) : null);
        EngineHooks.ApplyEitherOr.Register("ApplyEmbracingVirtue", 10300, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyEmbracingVirtue(p, option, source, decider));
        EngineHooks.FinishEitherOr.Register("FinishEmbracingVirtue", 10300, static (engine, p) => engine.FinishEmbracingVirtue(p));
    }
}
