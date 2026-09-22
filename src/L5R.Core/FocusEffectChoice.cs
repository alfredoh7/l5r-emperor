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
public sealed partial class MatchEngine
{
    private const Zone FocusChoiceZone = Zone.FateDiscard;

    private static bool FocusEffectNeedsChoice(string abilityId) => abilityId is "disreputable-deal-focus" or "crimson-mark-focus";

    private bool HasPoliticalAbility(CardInstance card) =>
        AbilityIdsOf(card).Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && IsAction(ability) && ActionCarriesKeyword(ability, card, "Political"));

    private List<string> FocusChoiceCandidates(int playerId, string abilityId) => abilityId == "crimson-mark-focus" ? DuelAttachmentCandidates() : DiscardStrategyCandidates(playerId);

    // The attachments on the Personalities involved in the duel: the challenger and the challenged.
    private List<string> DuelAttachmentCandidates() =>
        _state.PendingDuel is { } duel
            ? new[] { duel.ChallengerId, duel.ChallengedId }.Where(id => _state.Cards.ContainsKey(id) && InPlay(Card(id))).SelectMany(id => Attachments(id)).Select(c => c.Id).Order(StringComparer.Ordinal).ToList()
            : [];

    private List<string> DiscardStrategyCandidates(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == FocusChoiceZone && Definition(c).Type == CardType.Strategy && !Keyword(c, "Unique") && HasPoliticalAbility(c))
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToList();

    /// <summary>Opens the choice for the Focus Effect at `index`. False (and nothing is asked) when nothing qualifies.</summary>
    private bool BeginFocusEffectChoice(int index, string focusCardId, string abilityId)
    {
        var focus = Card(focusCardId); var owner = focus.OwnerId;
        var candidates = FocusChoiceCandidates(owner, abilityId);
        var attachments = abilityId == "crimson-mark-focus";
        if (candidates.Count == 0)
        {
            Emit("focusEffectNothing", attachments ? $"{Definition(focus).Name}: no Personality involved in the duel has an attachment." : $"{Definition(focus).Name}: {Player(owner).Name} has no non-Unique Strategy with a Political ability in the discard pile.", owner, focusCardId);
            return false;
        }
        _state.PendingFocusChoice = new PendingFocusChoice { Index = index, FocusCardId = focusCardId, PlayerId = owner, Candidates = candidates, AbilityId = abilityId };
        _state.PendingChoice = new("focusEffectChoice", owner, focusCardId, attachments
            ? $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may destroy an attachment on a Personality involved in this duel."
            : $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may put a non-Unique Strategy with a Political ability from your discard pile into your hand.");
        _state.PriorityPlayerId = owner; _state.ConsecutivePasses = 0;
        Emit("focusEffectChoiceOffered", attachments ? $"{Player(owner).Name} may destroy an attachment ({Definition(focus).Name})." : $"{Player(owner).Name} may take a Strategy from the discard pile ({Definition(focus).Name}).", owner, focusCardId);
        return true;
    }

    private void ResolveFocusEffectChoiceCommand(GameCommand command)
    {
        var pending = _state.PendingFocusChoice ?? throw new RuleViolation("No Focus Effect is waiting for a choice.");
        Require(command.Type == "chooseFocusEffect", "Choose a card or decline.");
        Require(command.PlayerId == pending.PlayerId, "Only the Focus Effect's player may choose.");
        var focus = Card(pending.FocusCardId);
        var destroys = pending.AbilityId == "crimson-mark-focus";
        if (command.Choice == "decline") Emit("focusEffectDeclined", $"{Player(pending.PlayerId).Name} took nothing ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        else
        {
            Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), destroys ? "Choose one of the offered attachments, or decline." : "Choose one of the offered Strategies, or decline.");
            var taken = Card(command.TargetId);
            if (destroys) { DestroyAttachment(taken); Emit("focusEffectDestroyed", $"{Player(pending.PlayerId).Name} destroyed {Definition(taken).Name} ({Definition(focus).Name}).", pending.PlayerId, taken.Id); }
            else
            {
                ReturnToHand(taken);
                Emit("focusEffectTook", $"{Player(pending.PlayerId).Name} put {Definition(taken).Name} into their hand ({Definition(focus).Name}).", pending.PlayerId, taken.Id);
            }
        }
        _state.PendingChoice = null; _state.PendingFocusChoice = null;
        ContinueDuelFocusEffects(pending.Index + 1);
    }

    private IEnumerable<ActionOption> FocusChoiceActions(int playerId)
    {
        if (_state.PendingFocusChoice is not { } p || p.PlayerId != playerId) yield break;
        foreach (var id in p.Candidates)
            yield return new("chooseFocusEffect", p.AbilityId == "crimson-mark-focus" ? $"Destroy {Definition(Card(id)).Name}" : $"Put {Definition(Card(id)).Name} into your hand", true, null,
                new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, TargetId: id), id);
        yield return new("chooseFocusEffect", "Take nothing", true, null, new GameCommand("chooseFocusEffect", playerId, _state.Version, CardId: p.FocusCardId, Choice: "decline"), p.FocusCardId);
    }
}
