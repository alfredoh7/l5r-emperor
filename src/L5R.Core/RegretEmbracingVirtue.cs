namespace L5R.Core;

// ======================================================================================================================================================================================
// REGRET (oracle-l5r-9923, The Shadow's Embrace) and EMBRACING VIRTUE (oracle-l5r-2313, War of Honor) -- issue #619, Mendon's readings.
//
// EMBRACING VIRTUE: "Before an End Phase begins, if this card is in your discard pile: You may put it in your hand."
//   * Asked at the top of EndTurn (BeginEmbracingVirtue), before the End Phase's draw and hand-size discard: a player who takes it back on their own turn may draw above their hand size and so be forced
//     to discard. "An End Phase" is any player's, so each End Phase asks every player with a copy in his Fate discard pile (the active player first), one Either/Or per copy. Each copy is asked once per turn.
//
// REGRET:
//   * FOCUS EFFECT. "After this duel ends, its winner's controller loses 1 Honor for each card he focused": the number of cards the winner's side focused (PendingDuel.ChallengerFocused/ChallengedFocused). Any
//     "instead of focusing" ability (Kakita Gonji's +4, Reserve Weapon's +3) adds nothing to those lists, so it is not a focus and is not counted (Mendon). A tie has no winner, so nothing is lost.
//   * OPEN. "Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs." Foothills Keep's attach from a discard pile (AttachAction.cs), for any number of
//     cards: one slot of the cards, one repeatable slot of Personalities (the i-th Personality takes the i-th card). The announcement checks the legality of the whole selection as the cards will stand one after
//     another (ValidateRegretAttach). Then EACH CARD IS ITS OWN EQUIP (Limited)-LIKE ACTION, in order (Mendon): its Equip cost is quoted and paid on its own (an Either/Or whose one real option is "attach, paying
//     N Gold", with the payment made for that attachment -- so a Holding that produces more Gold for an attachment, Platinum Mine, does so every time), the card attaches, its "after this enters play" window
//     opens and closes, and only then does the next card begin (AdvanceRegret). A card that is no longer legal or cannot be paid for when its turn comes stays in the pile. This is NOT Equip (Battle): no additional
//     action, and no Spell. An action printed on a card attached this way is usable at the player's very next action; nothing here delays it. Regret prints no Battle designator, so it is an Action Phase action
//     only (the engine's Open timing); an effect that lets an Open action be played in a battle would apply to it as to any Open action.
// ======================================================================================================================================================================================

public static class RegretEmbracingVirtueAbilities
{
    public const string RegretFocus = "regret-focus", RegretAttach = "regret-attach";
    /// <summary>Embracing Virtue's Effect: a trait with no action of its own (it is read from the discard pile at the end of a turn).</summary>
    public const string EmbracingVirtueEffect = "embracing-virtue-return";
    /// <summary>The Either/Or spec of one copy's "you may put it in your hand".</summary>
    public const string EmbracingVirtueSpec = "embracing-virtue-return";
    /// <summary>The Either/Or of one attachment of Regret's Open action: attach it, paying its Equip cost, or (only when it cannot be done) leave it in the pile.</summary>
    public const string RegretStepSpec = "regret-attach-card";
    /// <summary>The cards to attach, and the Personalities they go to. The Personalities slot is called "personality" so the ordinary Attach legality (ValidateAttach) judges the first pair.</summary>
    public const string AttachmentsSlot = "attachments", HostsSlot = "personality";
    private const int MostAttachments = 99;

    public static Dictionary<string, AbilityDefinition> AddRegretEmbracingVirtue(this Dictionary<string, AbilityDefinition> all)
    {
        all[RegretFocus] = new("FocusEffect", "As a Focus Effect: After this duel ends, its winner's controller loses 1 Honor for each card he focused", "none");
        all[RegretAttach] = new("Open", "Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs", "slots",
            Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.PersonalitySlot, [CardType.Follower, CardType.Item]),
            Slots: [new(AttachmentsSlot, "attachment-in-discard", "One or more of your Followers or Items in your discard pile", 1, MostAttachments),
                new(HostsSlot, "own-personality", "A Personality of yours for each card, in the same order (name one twice for two cards)", 1, MostAttachments) { Repeatable = true }]);
        return all;
    }
}

/// <summary>One card of Regret's Open action and the Personality named for it.</summary>
public sealed class RegretStep
{
    public string AttachmentId { get; set; } = "";
    public string HostId { get; set; } = "";
}

/// <summary>Regret's Open action in progress: the cards still to be attached, one at a time, the card whose entering-play window is next, and the action to finish once they are done.</summary>
public sealed class RegretState
{
    public bool Active { get; set; }
    public int PlayerId { get; set; }
    public string SourceId { get; set; } = "";
    public List<RegretStep> Steps { get; set; } = [];
    public string? JustAttachedId { get; set; }
    public PendingEffectChoice Paused { get; set; } = new();
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

    // A payment for this attachment (an Equip of it to this Personality): what a Holding that pays more "for an attachment" reads.
    private PaymentSubject RegretPayingSubject(CardInstance attachment, CardInstance host) => new(attachment, PaymentKind.Equip, AbilityCatalog.All[RegretEmbracingVirtueAbilities.RegretAttach], host);

    // Either/Or's hook: what the payment of the card now being attached is for (null for every other decision).
    private PaymentSubject? EitherOrPayingFor(PendingEitherOr p) =>
        p.SpecId == RegretEmbracingVirtueAbilities.RegretStepSpec && _state.Regret.Steps.Count > 0 && _state.Cards.TryGetValue(_state.Regret.Steps[0].AttachmentId, out var card) && _state.Cards.TryGetValue(_state.Regret.Steps[0].HostId, out var host)
            ? RegretPayingSubject(card, host) : null;

    // The announcement, judged as one: each pair is the ordinary Equip legality (RequireAttachable), with the cards before it already standing where they will -- so two Weapons or two Armor for one Personality,
    // two copies of a Unique card, or a host that refuses are refused up front. The trial placement is undone however the check ends. (Gold is not part of it: each card is paid for when its turn comes.)
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

    // RunEffects hook. Regret is spent as it is played; the cards are then attached one at a time (AdvanceRegret). False when nothing is left pending (the generic tail finishes the action), true when a payment
    // or an entering-play window is open (the action finishes when the last card is done).
    private bool? RunRegret(string id, GameCommand command, PlayerState player, CardInstance? source, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (id != RegretEmbracingVirtueAbilities.RegretAttach) return null;
        var attachments = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.AttachmentsSlot);
        var hosts = RegretSlotCardsOrNone(command, RegretEmbracingVirtueAbilities.HostsSlot);
        var regret = _state.Regret;
        regret.Active = true; regret.PlayerId = player.Id; regret.SourceId = source!.Id; regret.JustAttachedId = null;
        regret.Steps = attachments.Zip(hosts, (a, h) => new RegretStep { AttachmentId = a.Id, HostId = h.Id }).ToList();
        regret.Paused = new() { Command = command, PlayerId = player.Id, ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard, OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList() };
        SpendHandPlayedCard(player, source);
        if (AdvanceRegret()) return true;
        regret.Active = false;
        return false;
    }

    // The next thing Regret owes: the entering-play window of the card just attached, else the next card's own payment. False when every card has been dealt with and nothing is open.
    private bool AdvanceRegret()
    {
        var regret = _state.Regret; var player = Player(regret.PlayerId);
        while (true)
        {
            if (OpenRegretEntryWindow()) return true;
            if (regret.Steps.Count == 0) return false;
            var step = regret.Steps[0];
            var attachment = Card(step.AttachmentId); var host = Card(step.HostId);
            string? why = null;
            if (attachment.Zone != Zone.FateDiscard || attachment.OwnerId != player.Id) why = "it is no longer in your discard pile";
            else try { RequireAttachable(attachment, host, player.Id); } catch (RuleViolation refused) { why = refused.Message; }
            if (why is not null)
            {
                Emit("attachSkipped", $"{Definition(attachment).Name} was not attached: {why}", player.Id, attachment.Id);
                regret.Steps.RemoveAt(0);
                continue;
            }
            // Its Equip cost is quoted and paid on its own; nothing to pay, or no way to pay it, needs no answer (the step applies itself).
            if (OpenEitherOr(RegretEmbracingVirtueAbilities.RegretStepSpec, Card(regret.SourceId), player.Id, player.Id, attachment, "action")) return true;
        }
    }

    // The card just attached enters play: its "after this enters play" effects and window, before the next card begins.
    private bool OpenRegretEntryWindow()
    {
        var regret = _state.Regret;
        if (regret.JustAttachedId is not string id) return false;
        regret.JustAttachedId = null;
        if (!_state.Cards.TryGetValue(id, out var card) || card.Zone != Zone.Attached) return false;
        _attachingFromPileId = id; // Clearing Crew: an attach from a discard pile is not an entry "from your hand"
        try
        {
            if (!OpenEntryReactionWindow(regret.PlayerId, card, 1 - regret.PlayerId)) return false;
            _state.PriorityPlayerId = regret.PlayerId;
            return true;
        }
        finally { _attachingFromPileId = null; }
    }

    // HandleRegisteredEntryReaction's hook: an entering-play window has closed, so the next card begins -- or, with none left, Regret is done (the window's own close already passed priority on).
    private void ContinueRegret()
    {
        if (!_state.Regret.Active || _state.PendingChoice is not null) return;
        if (!AdvanceRegret()) _state.Regret.Active = false;
    }

    private string RegretStepPrompt(PendingEitherOr p, string source)
    {
        var step = _state.Regret.Steps[0];
        return $"{source}: attach {Definition(Card(step.AttachmentId)).Name} to {Definition(Card(step.HostId)).Name}.";
    }

    private EitherOrOption[] RegretStepOptions(PendingEitherOr p)
    {
        var step = _state.Regret.Steps[0];
        var attachment = Card(step.AttachmentId); var host = Card(step.HostId); var name = Definition(attachment).Name; var hostName = Definition(host).Name;
        var (cost, basis, _) = EquipGoldCost(p.DeciderPlayerId, attachment, host, fromHand: false);
        string? obstacle = null;
        if (attachment.Zone != Zone.FateDiscard) obstacle = $"{name} is no longer in your discard pile.";
        else try { RequireAttachable(attachment, host, p.DeciderPlayerId); } catch (RuleViolation refused) { obstacle = refused.Message; }
        if (obstacle is null && cost > 0 && PaymentOptions(p.DeciderPlayerId, cost, RegretPayingSubject(attachment, host)).Length == 0) obstacle = $"You cannot produce the {cost} Gold {name} costs.";
        return [
            new("attach", $"Attach {name} to {hostName}", cost > 0 ? $"Pay {cost} Gold ({basis}). It enters play, then its own entry window." : "It costs nothing. It enters play, then its own entry window.", EitherOrKind.Choose, cost, obstacle is null, obstacle),
            new("skip", $"Do not attach {name}", obstacle ?? "It stays in your discard pile.", EitherOrKind.Choose, 0, obstacle is not null, "It can be attached: pay for it.")
        ];
    }

    private bool ApplyRegretStep(PendingEitherOr p, EitherOrOption option, PlayerState decider, string[]? sourceIds)
    {
        if (p.SpecId != RegretEmbracingVirtueAbilities.RegretStepSpec) return false;
        var regret = _state.Regret; var step = regret.Steps[0]; regret.Steps.RemoveAt(0);
        var attachment = Card(step.AttachmentId); var host = Card(step.HostId);
        if (option.Key == "attach")
        {
            if (option.GoldCost > 0) Pay(decider.Id, attachment, option.GoldCost, sourceIds, paying: RegretPayingSubject(attachment, host));
            Emit("eitherOrChosen", $"{decider.Name} attached {Definition(attachment).Name} to {Definition(host).Name}{(option.GoldCost > 0 ? $", paying {option.GoldCost} Gold" : "")}.", decider.Id, attachment.Id);
            PlaceAttachment(attachment, host, decider);
            regret.JustAttachedId = attachment.Id;
        }
        else Emit("attachSkipped", $"{Definition(attachment).Name} was not attached: {option.Detail}", decider.Id, attachment.Id);
        return true;
    }

    // The payment was answered and the card attached: its window, then the next card, then -- when none is left and nothing is open -- the action's own tail.
    private bool FinishRegretStep(PendingEitherOr p)
    {
        if (p.SpecId != RegretEmbracingVirtueAbilities.RegretStepSpec) return false;
        var regret = _state.Regret;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0; _state.PriorityPlayerId = regret.PlayerId;
        if (AdvanceRegret()) return true;
        regret.Active = false;
        var paused = regret.Paused;
        CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore, paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
        return true;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRegretEmbracingVirtueHooks()
    {
        EngineHooks.Abilities.Register("AddRegretEmbracingVirtue", 10300, static abilities => abilities.AddRegretEmbracingVirtue());
        EngineHooks.ValidateLate.Register("ValidateRegretAttach", 10300, static (engine, context) => engine.ValidateRegretAttach(context.id, context.command, context.player));
        EngineHooks.RunEffects.Register("RunRegret", 10300, static (engine, context) => engine.RunRegret(context.id, context.command, context.player, context.source, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.EitherOrPrompt.Register("EmbracingVirtueEitherOrPrompt", 10300, static (engine, p, source, affected) => p.SpecId == RegretEmbracingVirtueAbilities.EmbracingVirtueSpec ? engine.EmbracingVirtuePrompt(source) : p.SpecId == RegretEmbracingVirtueAbilities.RegretStepSpec ? engine.RegretStepPrompt(p, source) : null);
        EngineHooks.EitherOrOptions.Register("EmbracingVirtueEitherOrOptions", 10300, static (engine, p, affected, source, payable) => p.SpecId == RegretEmbracingVirtueAbilities.EmbracingVirtueSpec ? engine.EmbracingVirtueOptions(p) : p.SpecId == RegretEmbracingVirtueAbilities.RegretStepSpec ? engine.RegretStepOptions(p) : null);
        EngineHooks.ApplyEitherOr.Register("ApplyEmbracingVirtue", 10300, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyEmbracingVirtue(p, option, source, decider) || engine.ApplyRegretStep(p, option, decider, sourceIds));
        EngineHooks.FinishEitherOr.Register("FinishEmbracingVirtue", 10300, static (engine, p) => engine.FinishEmbracingVirtue(p) || engine.FinishRegretStep(p));
    }
}
