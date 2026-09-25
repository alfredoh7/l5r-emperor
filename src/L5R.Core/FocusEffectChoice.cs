namespace L5R.Core;

/// <summary>A Focus Effect that stopped the duel's resolution to ask its player something (MatchState.PendingFocusChoice): which focused card it belongs to, where the duel's list of Focus
/// Effects stands, who chooses, and among what.</summary>
public sealed class PendingFocusChoice
{
    public int Index { get; set; }
    public string FocusCardId { get; set; } = "";
    public int PlayerId { get; set; }
    public List<string> Candidates { get; set; } = [];
    public string AbilityId { get; set; } = "";
    /// <summary>The "(now)" choice of a delay Focus Effect (EmperorRulingR2.cs): answered before the duel's outcome applies, not among the Focus Effects that resolve after it.</summary>
    public bool DelayDecision { get; set; }
}

// A Focus Effect that needs a decision (issue #419, Mendon's ruling on Disreputable Deal: "a primitive that pauses the resolution of focus effects to allow a player to select a card ...
// that meets the criteria"). A duel's Focus Effects resolve in the order the active player chose, one after another (Dueling.cs, ContinueDuelFocusEffects); one that has a real choice
// stops the run at its place, the player it belongs to answers, and the run goes on from the next Focus Effect and then finishes the duel as it always did.
//
//   * Disreputable Deal: "As a Focus Effect, you may put a non-Unique Strategy with a Political ability in your discard pile into your hand." The candidates are the Strategies in the player's
//     Fate discard pile that are not Unique and have a Political ability (an ability printed with the Political action keyword, or on a card that has the keyword: an ability inherits its
//     card's keywords -- so Political Battle, Limited, Open and Reaction abilities all count); they are public, the pile being face-up. The choice is "may": any one of them or none.
//     Mendon named the Fate deck as the place searched; the printed text says the discard pile, so that is what is built (the one constant, FocusChoiceZone, if he wants the deck);
//   * with nothing that qualifies nobody is asked;
//   * the player answers with "chooseFocusEffect": a card (TargetId) or Choice "decline";
//   * The Crimson Mark (Before the Dawn, issue #431): "As a Focus Effect: You may destroy an attachment on a Personality involved in this duel." The candidates are the attachments on the two
//     Personalities of the duel (the challenger and the challenged), of either player; choosing one destroys it, "decline" leaves them all.
//   * Hiromi's Vassals and Shamsir (issue #435): "As a Focus Effect: After this duel ends, you may attach this card to one of your Personalities, ignoring Gold cost." A third choice kind --
//     the candidates are the chooser's own Personalities in play that could legally receive this specific attachment (the same checks RequireAttachable applies to an ordinary Equip); choosing
//     one attaches the focus card itself (AttachFocusedCard), "decline" leaves it to go to the discard pile as usual when FinishDuelResolution spends the duel's focused cards.
public sealed partial class MatchEngine
{
    private const Zone FocusChoiceZone = Zone.FateDiscard;

    private static bool FocusEffectNeedsChoice(string abilityId) => abilityId is "disreputable-deal-focus" or "crimson-mark-focus" or "hiromis-vassals-focus" or "shamsir-focus"
        // Ceaseless Vigil (Seeds of Decay pass, issue #453, batch 1): "straighten a Personality" -- a mandatory choice among the bowed Personalities in play (SeedsOfDecayBatch1.cs).
        or "ceaseless-vigil-focus"
        or "sandalwood-incense-focus" // issue #453, batch 5
        or "cycle-of-vengeance-focus" // issue #460, batch 4
        or "swell-of-strength-focus" // issue #460, batch 5: Hiromi's Vassals/Shamsir's own attach-self shape, narrowed to Water Shugenja by AttachRequiresKeyword.
        || abilityId == "fair-warning-focus" // issue #453, batch 2
        || abilityId == "superior-opponent-focus"; // Emperor Edition remaining-cards pass, group C (issue #492)
    // Fair Warning (issue #453, batch 2): "if your Personality won it, you may give a Personality -4F" -- any Personality in play, only when the focus card's player's side won.
    private static bool IsFairWarningFocus(string abilityId) => abilityId == "fair-warning-focus";
    private List<string> FairWarningCandidates(int playerId) =>
        _state.PendingDuel?.WinnerId is string winnerId && Card(winnerId).OwnerId == playerId
            ? _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [];
    private static bool IsAttachSelfFocusEffect(string abilityId) => abilityId is "hiromis-vassals-focus" or "shamsir-focus" or "swell-of-strength-focus";
    // The Cycle of Vengeance (oracle-l5r-8004, issue #460 batch 4): "After this duel ends, if your Personality lost it, you may give one of your
    // Personalities two +1F Revenge tokens" -- Fair Warning's own shape (candidates = own Personalities in play), asked on a LOSS instead of a win: the
    // focus player's Personality is one of the duel's LoserIds, not its WinnerId.
    private static bool IsCycleOfVengeanceFocus(string abilityId) => abilityId == "cycle-of-vengeance-focus";
    private List<string> CycleOfVengeanceCandidates(int playerId) =>
        _state.PendingDuel?.LoserIds.Any(id => Card(id).OwnerId == playerId) == true
            ? _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && c.OwnerId == playerId).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [];
    // Sandalwood Incense (oracle-l5r-10123, issue #453 batch 5): "After this duel ends, if your Personality won it, gain 2 Honor or draw a card" -- a choice between two EFFECTS, not
    // cards: the candidates are the two effect keys below, offered only when the focusing player's Personality won (a tie or a loss asks nothing).
    private const string SandalwoodGainHonor = "gain-honor";
    private const string SandalwoodDrawCard = "draw-card";
    private static bool IsEffectKeyFocusEffect(string abilityId) => abilityId is "sandalwood-incense-focus";
    private List<string> SandalwoodCandidates(int playerId) =>
        _state.PendingDuel is { WinnerId: { } winnerId } && Card(winnerId).OwnerId == playerId ? [SandalwoodGainHonor, SandalwoodDrawCard] : [];

    private bool HasPoliticalAbility(CardInstance card) =>
        AbilityIdsOf(card).Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && IsAction(ability) && ActionCarriesKeyword(ability, card, "Political"));

    private List<string> FocusChoiceCandidates(int playerId, string abilityId, string focusCardId) => abilityId == "crimson-mark-focus" ? DuelAttachmentCandidates()
        : IsFairWarningFocus(abilityId) ? FairWarningCandidates(playerId)
        : IsEffectKeyFocusEffect(abilityId) ? SandalwoodCandidates(playerId)
        : IsAttachSelfFocusEffect(abilityId) ? AttachWielderCandidates(playerId, focusCardId)
        : IsCycleOfVengeanceFocus(abilityId) ? CycleOfVengeanceCandidates(playerId)
        : abilityId == "superior-opponent-focus" ? SuperiorOpponentCandidates(playerId)
        : IsStraightenFocusEffect(abilityId) ? StraightenFocusCandidates() : DiscardStrategyCandidates(playerId);

    // The attachments on the Personalities involved in the duel: the challenger and the challenged.
    private List<string> DuelAttachmentCandidates() =>
        _state.PendingDuel is { } duel
            ? new[] { duel.ChallengerId, duel.ChallengedId }.SelectMany(id => Attachments(id)).Select(c => c.Id).Order(StringComparer.Ordinal).ToList()
            : [];

    private List<string> DiscardStrategyCandidates(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == FocusChoiceZone && Definition(c).Type == CardType.Strategy && !Keyword(c, "Unique") && HasPoliticalAbility(c))
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToList();

    // The chooser's own Personalities in play that could legally receive this focused card as an attachment -- the same checks RequireAttachable applies to an ordinary Equip
    // (in play, the attachment's own "will/not attach to", the Armor/Weapon caps, and Unique/Singular), just gathered as a candidate list instead of thrown against one chosen wielder.
    private List<string> AttachWielderCandidates(int playerId, string focusCardId)
    {
        var attachment = Card(focusCardId); var def = Definition(attachment);
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality
                && AttachmentLegalTarget(def, c) && AttachmentAllowed(attachment, c)
                && (!(Keyword(attachment, "Unique") || Keyword(attachment, "Singular")) || !ControlsCopyOf(playerId, def.Name)))
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Opens the choice for the Focus Effect at `index`. False (and nothing is asked) when nothing qualifies.</summary>
    private bool BeginFocusEffectChoice(int index, string focusCardId, string abilityId)
    {
        var focus = Card(focusCardId); var owner = focus.OwnerId;
        var candidates = FocusChoiceCandidates(owner, abilityId, focusCardId);
        var attachments = abilityId is "crimson-mark-focus" or "superior-opponent-focus";
        var attachSelf = IsAttachSelfFocusEffect(abilityId);
        if (candidates.Count == 0 && IsEffectKeyFocusEffect(abilityId))
        {
            Emit("focusEffectNothing", $"{Definition(focus).Name}: {Player(owner).Name}'s Personality did not win the duel, so nothing is gained.", owner, focusCardId);
            return false;
        }
        if (IsEffectKeyFocusEffect(abilityId))
        {
            _state.PendingFocusChoice = new PendingFocusChoice { Index = index, FocusCardId = focusCardId, PlayerId = owner, Candidates = candidates, AbilityId = abilityId };
            _state.PendingChoice = new("focusEffectChoice", owner, focusCardId, $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, gain 2 Honor or draw a card.");
            _state.PriorityPlayerId = owner; _state.ConsecutivePasses = 0;
            Emit("focusEffectChoiceOffered", $"{Player(owner).Name} may gain 2 Honor or draw a card ({Definition(focus).Name}).", owner, focusCardId);
            return true;
        }
        if (candidates.Count == 0)
        {
            Emit("focusEffectNothing", IsFairWarningFocus(abilityId) ? $"{Definition(focus).Name}: {Player(owner).Name}'s Personality did not win the duel."
                : IsCycleOfVengeanceFocus(abilityId) ? $"{Definition(focus).Name}: {Player(owner).Name}'s Personality did not lose the duel."
                : abilityId == "superior-opponent-focus" ? $"{Definition(focus).Name}: {Player(owner).Name}'s Personality is not at the current battlefield, or no attachment is there."
                : attachments ? $"{Definition(focus).Name}: no Personality involved in the duel has an attachment."
                : IsStraightenFocusEffect(abilityId) ? $"{Definition(focus).Name}: no Personality in play is bowed."
                : attachSelf ? $"{Definition(focus).Name}: {Player(owner).Name} controls no Personality it may attach to."
                : $"{Definition(focus).Name}: {Player(owner).Name} has no non-Unique Strategy with a Political ability in the discard pile.", owner, focusCardId);
            return false;
        }
        _state.PendingFocusChoice = new PendingFocusChoice { Index = index, FocusCardId = focusCardId, PlayerId = owner, Candidates = candidates, AbilityId = abilityId };
        _state.PendingChoice = new("focusEffectChoice", owner, focusCardId, IsFairWarningFocus(abilityId)
            ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may give a Personality -4F."
            : IsCycleOfVengeanceFocus(abilityId) ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may give one of your Personalities two +1F Revenge tokens."
            : abilityId == "superior-opponent-focus" ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may destroy an attachment at the current battlefield."
            : attachments
            ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may destroy an attachment on a Personality involved in this duel."
            : IsStraightenFocusEffect(abilityId) ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, straighten a Personality."
            : attachSelf ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may attach this card to one of your Personalities, ignoring Gold cost."
            : $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may put a non-Unique Strategy with a Political ability from your discard pile into your hand.");
        _state.PriorityPlayerId = owner; _state.ConsecutivePasses = 0;
        Emit("focusEffectChoiceOffered", attachments ? $"{Player(owner).Name} may destroy an attachment ({Definition(focus).Name})."
            : IsCycleOfVengeanceFocus(abilityId) ? $"{Player(owner).Name} may give a Personality two Revenge tokens ({Definition(focus).Name})."
            : attachSelf ? $"{Player(owner).Name} may attach {Definition(focus).Name} to a Personality."
            : $"{Player(owner).Name} may take a Strategy from the discard pile ({Definition(focus).Name}).", owner, focusCardId);
        return true;
    }

    private void ResolveFocusEffectChoiceCommand(GameCommand command)
    {
        var pending = _state.PendingFocusChoice ?? throw new RuleViolation("No Focus Effect is waiting for a choice.");
        Require(command.Type == "chooseFocusEffect", "Choose a card or decline.");
        Require(command.PlayerId == pending.PlayerId, "Only the Focus Effect's player may choose.");
        if (pending.DelayDecision) { ResolveDelayDecision(command, pending); return; } // The delay Focus Effects' "(now)" choice (EmperorRulingR2Cards.cs).
        var focus = Card(pending.FocusCardId);
        var destroys = pending.AbilityId is "crimson-mark-focus" or "superior-opponent-focus";
        var attachSelf = IsAttachSelfFocusEffect(pending.AbilityId);
        Require(command.Choice != "decline" || !IsStraightenFocusEffect(pending.AbilityId), "This Focus Effect is not optional: choose a Personality to straighten.");
        if (command.Choice == "decline") Emit("focusEffectDeclined", $"{Player(pending.PlayerId).Name} took nothing ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        // Sandalwood Incense (issue #453 batch 5): the chosen effect key, not a card.
        else if (IsEffectKeyFocusEffect(pending.AbilityId))
        {
            Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose to gain 2 Honor or to draw a card, or decline.");
            if (command.TargetId == SandalwoodGainHonor) GainHonor(pending.PlayerId, 2, Definition(focus).Name, focus);
            else Draw(Player(pending.PlayerId));
            Emit("focusEffectChosen", $"{Player(pending.PlayerId).Name} chose to {(command.TargetId == SandalwoodGainHonor ? "gain 2 Honor" : "draw a card")} ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        }
        else
        {
            Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId),
                destroys ? "Choose one of the offered attachments, or decline."
                : attachSelf ? "Choose one of the offered Personalities, or decline."
                : IsCycleOfVengeanceFocus(pending.AbilityId) ? "Choose one of your Personalities to give two Revenge tokens, or decline."
                : "Choose one of the offered Strategies, or decline.");
            var taken = Card(command.TargetId);
            if (IsFairWarningFocus(pending.AbilityId)) { ApplyModifier(taken, focus.Id, pending.AbilityId, "Force", -4); Emit("focusEffectPenalty", $"{Player(pending.PlayerId).Name} gave {Definition(taken).Name} -4F ({Definition(focus).Name}).", pending.PlayerId, taken.Id); }
            else if (IsCycleOfVengeanceFocus(pending.AbilityId))
            {
                // An Insult Answered's own token shape (issue #453 batch 2, Dueling.cs): two independent +1F Revenge tokens, not a single +2F one.
                CreateToken(taken, "Revenge", pending.AbilityId, "Force", 1); CreateToken(taken, "Revenge", pending.AbilityId, "Force", 1);
                Emit("focusEffectRevenge", $"{Player(pending.PlayerId).Name} gave {Definition(taken).Name} two +1F Revenge tokens ({Definition(focus).Name}).", pending.PlayerId, taken.Id);
            }
            else if (destroys) { DestroyAttachment(taken); Emit("focusEffectDestroyed", $"{Player(pending.PlayerId).Name} destroyed {Definition(taken).Name} ({Definition(focus).Name}).", pending.PlayerId, taken.Id); }
            else if (IsStraightenFocusEffect(pending.AbilityId)) { Straighten(taken); Emit("focusEffectStraightened", $"{Player(pending.PlayerId).Name} straightened {Definition(taken).Name} ({Definition(focus).Name}).", pending.PlayerId, taken.Id); }
            else if (attachSelf)
            {
                AttachFocusedCard(focus, taken, pending.PlayerId);
                Emit("focusEffectAttached", $"{Player(pending.PlayerId).Name} attached {Definition(focus).Name} to {Definition(taken).Name}.", pending.PlayerId, focus.Id);
            }
            else
            {
                ReturnToHand(taken);
                Emit("focusEffectTook", $"{Player(pending.PlayerId).Name} put {Definition(taken).Name} into their hand ({Definition(focus).Name}).", pending.PlayerId, taken.Id);
            }
        }
        _state.PendingChoice = null; _state.PendingFocusChoice = null;
        ContinueDuelFocusEffects(pending.Index + 1);
    }

    // Hiromi's Vassals/Shamsir's own chosen attach: the focus card was revealed by the duel's own Strike, not drawn from a hand or discard pile, so this bypasses ExecuteAttach (which assumes
    // one of those two sources) and Gold is never quoted or paid -- "ignoring Gold cost" is simply not charging it. Neither printed card carries its own entering-play Reaction, so this does not
    // open one (ExecuteAttach's own OpenEntryReactionWindow call); a future Focus-Effect-attach card that does would need that pause-and-resume wiring added then.
    private void AttachFocusedCard(CardInstance attachment, CardInstance wielder, int playerId)
    {
        var def = Definition(attachment);
        TakeControl(attachment, wielder.OwnerId);
        attachment.Zone = Zone.Attached; attachment.AttachedTo = wielder.Id; attachment.FaceUp = true; attachment.ProvinceIndex = wielder.ProvinceIndex;
        if (def.AttachGrantsKeyword is string granted) GrantKeyword(wielder, granted, attachment.Id, "Attachment");
        Emit("equipped", $"{def.Name} attached to {Definition(wielder).Name} as a Focus Effect.", playerId, attachment.Id);
    }

    private IEnumerable<ActionOption> FocusChoiceActions(int playerId)
    {
        if (_state.PendingFocusChoice is not { } p || p.PlayerId != playerId) yield break;
        if (p.DelayDecision) { foreach (var option in DelayDecisionActions(p)) yield return option; yield break; }
        foreach (var id in p.Candidates)
            yield return new("chooseFocusEffect", IsFairWarningFocus(p.AbilityId) ? $"Give {Definition(Card(id)).Name} -4F"
                : IsEffectKeyFocusEffect(p.AbilityId) ? (id == SandalwoodGainHonor ? "Gain 2 Honor" : "Draw a card")
                : p.AbilityId is "crimson-mark-focus" or "superior-opponent-focus" ? $"Destroy {Definition(Card(id)).Name}"
                : IsAttachSelfFocusEffect(p.AbilityId) ? $"Attach to {Definition(Card(id)).Name}"
                : IsCycleOfVengeanceFocus(p.AbilityId) ? $"Give {Definition(Card(id)).Name} two Revenge tokens"
                : IsStraightenFocusEffect(p.AbilityId) ? $"Straighten {Definition(Card(id)).Name}" : $"Put {Definition(Card(id)).Name} into your hand", true, null,
                new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, TargetId: id), id);
        if (IsStraightenFocusEffect(p.AbilityId)) yield break; // mandatory: no "take nothing"
        yield return new("chooseFocusEffect", "Take nothing", true, null, new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, Choice: "decline"), p.FocusCardId);
    }
}
