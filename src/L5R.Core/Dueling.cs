namespace L5R.Core;

// Dueling (core rules; issue #204): a mini-game that pauses the outer turn structure. A challenge-
// issuing ability (AbilityDefinition.IssuesDuel) resolves generically through IssueChallenge instead
// of the per-id switch in ExecuteAbilityEffects; the same ability id is invoked again later, once a
// winner/loser is known, through ApplyDuelOutcome, to apply its own printed win/loss text. The state
// machine (PendingDuel.Stage) is: "AwaitingAccept" (Refusable challenges only) -> "Reaction" (a real
// PendingChoice("duelReaction", ...) window, same shape as attackReaction) -> "Focusing" (challenged
// goes first; either side Striking ends it for both; four focuses is the cap) -> "DuelistRefocus"
// (each Duelist-controlling side, in turn, may discard one focused card and focus once more) ->
// "OrderingEffects" (the OUTER game's ActivePlayerId orders any Focus-Effect-bearing focused cards)
// -> resolved (PendingDuel set back to null, priority returns to the challenger exactly as an
// ordinary ability's own NextAction call would have left it).
public sealed partial class MatchEngine
{
    private bool AnyDuelReactionAvailable(PendingDuel duel) =>
        AnyInPlayReactionAvailable("before-duel-begins") ||
        AnyOutOfPlayReactionAvailable(duel.ChallengerPlayerId, "before-duel-begins") ||
        AnyOutOfPlayReactionAvailable(duel.ChallengedPlayerId, "before-duel-begins");

    // Called from ExecuteAbilityEffects for any ability with IssuesDuel set, after the card's own
    // cost/discard has already happened -- everything from here on is the deferred EFFECT (the duel
    // itself), paused without ceding priority, the same shape ResolveOrDeferAttack already uses.
    private void IssueChallenge(string abilityId, int challengerPlayerId, string challengerId, string challengedId, string stat, bool refusable)
    {
        var challenger = Card(challengerId); var challenged = Card(challengedId);
        _state.PendingDuel = new PendingDuel
        {
            AbilityId = abilityId, ChallengerPlayerId = challengerPlayerId, ChallengedPlayerId = challenged.OwnerId,
            ChallengerId = challengerId, ChallengedId = challengedId, Stat = stat, Refusable = refusable,
            Stage = refusable ? "AwaitingAccept" : "Reaction",
        };
        Emit("duelChallenged", $"{Definition(challenger).Name} challenges {Definition(challenged).Name} to a duel.", challengerPlayerId, challengerId);
        if (refusable) { _state.PriorityPlayerId = challenged.OwnerId; _state.ConsecutivePasses = 0; }
        else OpenDuelReactionWindowOrBeginFocusing();
    }

    private void OpenDuelReactionWindowOrBeginFocusing()
    {
        var duel = _state.PendingDuel!;
        duel.Stage = "Reaction";
        if (!AnyDuelReactionAvailable(duel)) { BeginFocusing(); return; }
        _state.PendingChoice = new("duelReaction", duel.ChallengedPlayerId, null, "A duel is pending: react or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = duel.ChallengedPlayerId;
        Emit("reactionWindow", "Duel reaction window opened.", duel.ChallengedPlayerId);
    }

    private void BeginFocusing()
    {
        var duel = _state.PendingDuel!;
        duel.Stage = "Focusing";
        duel.TurnPlayerId = duel.ChallengedPlayerId; // the challenged player has the first option, always
        _state.PriorityPlayerId = duel.TurnPlayerId;
        _state.ConsecutivePasses = 0;
        Emit("duelFocusingBegan", "Focusing begins; the challenged player has the first option.", duel.TurnPlayerId);
    }

    // Refusing is only ever reachable when Refusable -- Steel on Steel's own unrefusable phrasing
    // ("Your performer duels the target") never opens this path. No card refusable this pass defines
    // its own refusal consequence yet (a future one would name it here, matching ApplyDuelOutcome's
    // own per-ability dispatch); refusing simply ends the challenge with no duel at all.
    private void ResolveDuelRefused()
    {
        var duel = _state.PendingDuel!;
        Emit("duelRefused", $"{Definition(Card(duel.ChallengedId)).Name} refuses the duel.", duel.ChallengedPlayerId, duel.ChallengedId);
        var challengerPlayerId = duel.ChallengerPlayerId;
        _state.PendingDuel = null;
        NextAction(challengerPlayerId);
    }

    private void ResolveDuelCommand(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        switch (duel.Stage)
        {
            case "AwaitingAccept":
                Require(duel.Refusable, "This duel cannot be refused.");
                Require(command.Type is "acceptDuel" or "declineDuel", "Accept or decline the duel challenge first.");
                Require(command.PlayerId == duel.ChallengedPlayerId, "Only the challenged player may respond to the challenge.");
                if (command.Type == "declineDuel") ResolveDuelRefused();
                else OpenDuelReactionWindowOrBeginFocusing();
                break;
            case "Focusing":
                Require(command.Type is "focus" or "strike", "Focus or Strike.");
                Require(command.PlayerId == duel.TurnPlayerId, "Wait for your turn to Focus or Strike in this duel.");
                if (command.Type == "strike") Strike(); else Focus(command);
                break;
            case "DuelistRefocus":
                Require(command.Type == "duelistRefocus", "Resolve the Duelist re-focus decision first.");
                Require(duel.PendingDuelistDecisions.Count > 0 && command.PlayerId == duel.PendingDuelistDecisions[0], "It is not your Duelist decision.");
                DuelistRefocus(command);
                break;
            case "OrderingEffects":
                Require(command.Type == "orderDuelFocusEffects", "Order the duel's own Focus Effects first.");
                Require(command.PlayerId == _state.ActivePlayerId, "Only the active player orders Focus Effects.");
                OrderDuelFocusEffects(command);
                break;
            default: throw new RuleViolation("Unknown duel stage.");
        }
    }

    // Focus (core rules): play a card face-down from hand OR the top of the Fate deck, next to the
    // focusing personality -- not "playing" the card. FaceUp mirrors the duel's own reveal state
    // (Struck), matching a Hand card's own private-until-shown convention rather than inventing a new
    // one; command.Choice=="deck" selects the top-of-deck source, otherwise command.CardId names a
    // hand card.
    private void Focus(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var isChallenger = command.PlayerId == duel.ChallengerPlayerId;
        var list = isChallenger ? duel.ChallengerFocused : duel.ChallengedFocused;
        Require(list.Count < 4, "You have already focused four times this duel and must Strike.");
        string cardId;
        if (command.Choice == "deck")
        {
            var deck = Player(command.PlayerId).FateDeck;
            Require(deck.Count > 0, "Your Fate deck is empty.");
            cardId = deck[0]; deck.RemoveAt(0);
        }
        else
        {
            Require(command.CardId is not null && Player(command.PlayerId).Hand.Contains(command.CardId), "Choose a card from your own hand, or focus from the deck.");
            cardId = command.CardId!; Player(command.PlayerId).Hand.Remove(cardId);
        }
        var card = Card(cardId);
        card.Zone = Zone.Focused; card.FaceUp = duel.Struck; card.ProvinceIndex = null;
        list.Add(cardId);
        Emit("duelFocused", $"{Player(command.PlayerId).Name} focused a card.", command.PlayerId, cardId);
        duel.TurnPlayerId = 1 - command.PlayerId;
        _state.PriorityPlayerId = duel.TurnPlayerId;
    }

    // Either player's Strike ends focusing for BOTH sides at once and reveals every focused card.
    private void Strike()
    {
        var duel = _state.PendingDuel!;
        duel.Struck = true;
        foreach (var id in duel.ChallengerFocused) Card(id).FaceUp = true;
        foreach (var id in duel.ChallengedFocused) Card(id).FaceUp = true;
        Emit("duelStruck", "A Strike ends focusing; focused cards are revealed.", _state.PriorityPlayerId);
        BeginDuelistRefocus();
    }

    // "Before Focus Effects resolve" (core rules): each side controlling a Duelist in the duel may
    // discard one focused card and focus once more. If both sides have Duelist, the OUTER game's own
    // active player decides first.
    private void BeginDuelistRefocus()
    {
        var duel = _state.PendingDuel!;
        duel.Stage = "DuelistRefocus";
        var challengerDuelist = Keyword(Card(duel.ChallengerId), "Duelist");
        var challengedDuelist = Keyword(Card(duel.ChallengedId), "Duelist");
        List<int> order = challengerDuelist && challengedDuelist
            ? [_state.ActivePlayerId, 1 - _state.ActivePlayerId]
            : challengerDuelist ? [duel.ChallengerPlayerId] : challengedDuelist ? [duel.ChallengedPlayerId] : [];
        duel.PendingDuelistDecisions = order;
        if (order.Count == 0) { BeginOrderingEffects(); return; }
        _state.PriorityPlayerId = order[0];
        _state.ConsecutivePasses = 0;
    }

    // command.CardId null declines. Otherwise: command.CardId names the already-focused card to
    // discard; the replacement focus source is command.Choice=="deck" or command.TargetId (a hand
    // card) -- distinct fields from the discard target, since this is one combined action, not two.
    private void DuelistRefocus(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var playerId = duel.PendingDuelistDecisions[0];
        duel.PendingDuelistDecisions.RemoveAt(0);
        if (command.CardId is not null)
        {
            var list = playerId == duel.ChallengerPlayerId ? duel.ChallengerFocused : duel.ChallengedFocused;
            Require(list.Remove(command.CardId), "Choose one of your own focused cards to discard.");
            var discarded = Card(command.CardId);
            discarded.Zone = Zone.FateDiscard;
            string newId;
            if (command.Choice == "deck")
            {
                var deck = Player(playerId).FateDeck;
                Require(deck.Count > 0, "Your Fate deck is empty.");
                newId = deck[0]; deck.RemoveAt(0);
            }
            else
            {
                Require(command.TargetId is not null && Player(playerId).Hand.Contains(command.TargetId), "Choose a card from your own hand, or focus from the deck.");
                newId = command.TargetId!; Player(playerId).Hand.Remove(newId);
            }
            var newCard = Card(newId);
            newCard.Zone = Zone.Focused; newCard.FaceUp = true; newCard.ProvinceIndex = null; // already revealed -- the duel has Struck
            list.Add(newId);
            Emit("duelistRefocused", $"{Player(playerId).Name} discarded a focused card to focus again.", playerId, newId);
        }
        if (duel.PendingDuelistDecisions.Count == 0) BeginOrderingEffects(); else _state.PriorityPlayerId = duel.PendingDuelistDecisions[0];
    }

    private void BeginOrderingEffects()
    {
        var duel = _state.PendingDuel!;
        duel.Stage = "OrderingEffects";
        var focusEffectCards = FocusEffectCardIds(duel);
        if (focusEffectCards.Count <= 1) { ResolveDuelFocusEffectsInOrder(focusEffectCards); return; }
        _state.PriorityPlayerId = _state.ActivePlayerId;
    }

    private List<string> FocusEffectCardIds(PendingDuel duel) =>
        duel.ChallengerFocused.Concat(duel.ChallengedFocused)
            .Where(id => (Definition(Card(id)).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "FocusEffect"))
            .ToList();

    private void OrderDuelFocusEffects(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var focusEffectCards = FocusEffectCardIds(duel);
        Require(command.CardIds is not null && command.CardIds.Length == focusEffectCards.Count &&
            command.CardIds.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(focusEffectCards.OrderBy(x => x, StringComparer.Ordinal)),
            "Order every Focus-Effect-bearing focused card exactly once.");
        ResolveDuelFocusEffectsInOrder(command.CardIds!);
    }

    private void ResolveDuelFocusEffectsInOrder(IEnumerable<string> orderedFocusEffectIds)
    {
        _state.PendingDuel!.OrderedFocusEffects = orderedFocusEffectIds.ToList();
        ResolveDuel();
    }

    // The duel stat plus each side's own total focused Focus Value (revealed by the time this runs).
    // Any attachment modifying the stat applies as long as it is active right now -- CardForce/
    // CardPersonalHonor/PersonalityChi already read current, not printed, values.
    private int DuelStatValue(CardInstance card, string stat) => stat switch
    {
        "Force" => CardForce(card),
        "PersonalHonor" => CardPersonalHonor(card),
        _ => PersonalityChi(card),
    };

    private void ResolveDuel()
    {
        var duel = _state.PendingDuel!;
        var challenger = Card(duel.ChallengerId); var challenged = Card(duel.ChallengedId);
        var challengerTotal = DuelStatValue(challenger, duel.Stat) + duel.ChallengerFocused.Sum(id => Definition(Card(id)).Focus);
        var challengedTotal = DuelStatValue(challenged, duel.Stat) + duel.ChallengedFocused.Sum(id => Definition(Card(id)).Focus);
        string? winnerId; List<string> loserIds;
        if (challengerTotal > challengedTotal) { winnerId = duel.ChallengerId; loserIds = [duel.ChallengedId]; }
        else if (challengedTotal > challengerTotal) { winnerId = duel.ChallengedId; loserIds = [duel.ChallengerId]; }
        else
        {
            // A tie: a Duelist on exactly one side wins; on neither side (or both) both sides lose.
            var challengerDuelist = Keyword(challenger, "Duelist"); var challengedDuelist = Keyword(challenged, "Duelist");
            if (challengerDuelist && !challengedDuelist) { winnerId = duel.ChallengerId; loserIds = [duel.ChallengedId]; }
            else if (challengedDuelist && !challengerDuelist) { winnerId = duel.ChallengedId; loserIds = [duel.ChallengerId]; }
            else { winnerId = null; loserIds = [duel.ChallengerId, duel.ChallengedId]; }
        }
        var winnerPlayerId = winnerId is null ? (int?)null : Card(winnerId).OwnerId;
        var loserPlayerIds = loserIds.Select(id => Card(id).OwnerId).ToList();
        Emit("duelResolved", winnerId is null ? "The duel is a tie; both personalities lose."
            : $"{Definition(Card(winnerId)).Name} wins the duel.", duel.ChallengerPlayerId);
        // The duel's own built-in outcome (each card's own printed win/loss text) applies first; any
        // Focus-Effect-driven outcome layers on top, in the active player's own chosen order -- e.g.
        // a Focus Effect that dishonors the loser, resolved before the duel's own "destroy the
        // loser," makes that death a dishonorable one instead of an honorable one.
        ApplyDuelOutcome(duel.AbilityId, winnerId, loserIds);
        foreach (var focusedId in duel.OrderedFocusEffects)
        {
            var focusEffectAbility = (Definition(Card(focusedId)).Abilities ?? []).First(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "FocusEffect");
            ApplyDuelFocusEffect(focusEffectAbility, Card(focusedId).OwnerId, winnerPlayerId, loserPlayerIds);
        }
        // Every focused card is spent, win or lose (a card destroyed as part of the outcome above is
        // simply skipped here -- Card() would still resolve it, but it is no longer in Zone.Focused).
        foreach (var id in duel.ChallengerFocused.Concat(duel.ChallengedFocused))
        {
            var card = Card(id);
            if (card.Zone == Zone.Focused) card.Zone = Zone.FateDiscard;
        }
        var challengerPlayerId = duel.ChallengerPlayerId;
        _state.PendingDuel = null;
        // The duel itself is a single, complete action from challenge to consequences (issue #209):
        // once everything above has applied, the winning card's own controller gets a chance to
        // react to it having won -- before handing play back exactly where the challenge was issued.
        if (winnerId is not null && OpenDuelWonReactionWindow(winnerId, winnerPlayerId!.Value, 1 - challengerPlayerId)) return;
        // Overconfident (issue #223): a chance to react to whichever side the duel's own outcome
        // destroyed (ApplyDuelOutcome/ApplyDuelFocusEffect, both above, already queued it via MarkDead).
        if (OpenOverconfidentDrawWindow(1 - challengerPlayerId)) return;
        NextAction(challengerPlayerId);
    }

    // "After a duel resolves during which [this card] won" (issue #209): scoped to the one specific
    // card that won, the same shape own-enters-play already uses for entering-play Reactions (see
    // OpenEntryReactionWindow) -- a tie has no single winner, so there is nothing to react with here.
    private static string? DuelWonReactionAbility(CardInstance card) =>
        (Definition(card).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-won-duel");

    private bool OpenDuelWonReactionWindow(string winnerId, int winnerPlayerId, int closesTo)
    {
        var card = Card(winnerId);
        var reactionId = DuelWonReactionAbility(card);
        if (reactionId is null) return false;
        card.AbilityUses.Remove(reactionId);
        _state.PendingChoice = new("duelWonReaction", winnerPlayerId, card.Id, "After the duel resolved, you may use this card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = winnerPlayerId;
        Emit("reactionWindow", "Duel-won reaction window opened.", winnerPlayerId, card.Id);
        return true;
    }

    // Each duel-issuing ability's own printed win/loss text. loserIds has two entries only on a
    // double-loss tie; winnerId is null in that same case.
    private void ApplyDuelOutcome(string abilityId, string? winnerId, List<string> loserIds)
    {
        switch (abilityId)
        {
            case "steel-on-steel-duel": DestroyUnits(loserIds.Select(Card).ToArray()); break;
            case "training-duel-challenge": foreach (var id in loserIds) ApplyModifier(Card(id), id, abilityId, "Force", -1); break;
            // Dragon Clan review (issue #207): three real self-performer Iaijutsu duels.
            case "daisuke-duel": foreach (var id in loserIds) MoveHome(Card(id), false); break;
            case "ichizo-exp-duel": case "shikei-duel": DestroyUnits(loserIds.Select(Card).ToArray()); break;
        }
    }

    // Each Focus-Effect-bearing focused card's own printed text, in the active player's chosen
    // order. winnerPlayerId/loserPlayerIds are null/empty-safe for a double-loss tie.
    private void ApplyDuelFocusEffect(string abilityId, int ownerId, int? winnerPlayerId, List<int> loserPlayerIds)
    {
        switch (abilityId)
        {
            case "courtesy-of-the-crane-focus":
                if (ownerId == winnerPlayerId) foreach (var province in Player(ownerId).Provinces) province.Strength += 2;
                break;
        }
    }
}
