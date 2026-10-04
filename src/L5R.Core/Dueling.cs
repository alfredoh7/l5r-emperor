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
    // False Ring of Earth (issue #295): "shared no keywords" -- every keyword either card effectively
    // carries (printed or granted, minus any removed), checked pairwise via the same live Keyword()
    // lookup every other keyword check in this engine already uses (so a removed/conditional keyword
    // on the OTHER side is still resolved correctly, not just string-matched against a raw list).
    private bool SharesNoKeywords(CardInstance a, CardInstance b) =>
        !(Definition(a).Keywords ?? []).Concat(a.GrantedKeywords.Select(g => g.Keyword)).Distinct(StringComparer.Ordinal)
            .Where(k => !KeywordRemoved(a, k)).Any(k => Keyword(b, k));
    // Issue #492 (ruling R5): He's Mine! and Irezu are counted only when they could substitute a participant (DuelReactionRelevant), so no window opens for nothing.
    private bool AnyDuelReactionAvailable(PendingDuel duel) =>
        AnyInPlayReactionAvailable("before-duel-begins", DuelReactionRelevant) ||
        OutOfPlayReactions(duel.ChallengerPlayerId, "before-duel-begins").Any(x => DuelReactionRelevant(x.Card, x.AbilityId)) ||
        OutOfPlayReactions(duel.ChallengedPlayerId, "before-duel-begins").Any(x => DuelReactionRelevant(x.Card, x.AbilityId));

    // Called from ExecuteAbilityEffects for any ability with IssuesDuel set, after the card's own
    // cost/discard has already happened -- everything from here on is the deferred EFFECT (the duel
    // itself), paused without ceding priority, the same shape ResolveOrDeferAttack already uses.
    private void IssueChallenge(string abilityId, int challengerPlayerId, string challengerId, string challengedId, string stat, bool refusable, CardInstance? source = null)
    {
        var challenger = Card(challengerId); var challenged = Card(challengedId);
        // Kitsuki Yataku (issue #477 batch 6): "The Samurai's controller may not choose to refuse any challenge created by the action."
        if (refusable && !ChallengeMayBeRefused(challengedId)) { refusable = false; Emit("refusalForbidden", $"{Definition(challenged).Name} may not refuse this challenge.", challenged.OwnerId, challengedId); }
        _state.PendingDuel = new PendingDuel
        {
            AbilityId = abilityId,
            ChallengerPlayerId = challengerPlayerId,
            ChallengedPlayerId = challenged.OwnerId,
            ChallengerId = challengerId,
            ChallengedId = challengedId,
            Stat = stat,
            Refusable = refusable,
            Stage = refusable ? "AwaitingAccept" : "Reaction",
            NegatedEffects = _state.NegatedEffects.ToList(),
        };
        // Kakita Ichigiku (The Shadow's Embrace pass, issue #476 batch 1): a challenge he issues to a dishonorable Personality in a Combat Segment will not be refused.
        if (IchigikuChallenge(challenger, challenged)) { _state.PendingDuel.IchigikuDestroysLoser = true; _state.PendingDuel.Refusable = false; _state.PendingDuel.Stage = "Reaction"; refusable = false; }
        Emit("duelChallenged", $"{Definition(challenger).Name} challenges {Definition(challenged).Name} to a duel.", challengerPlayerId, challengerId);
        var duelId = $"duel-{_state.Events[^1].Sequence}";
        _state.PendingDuel.DuelId = duelId;
        _state.LastDuel = new(duelId, stat, ViewCard(challenger), ViewCard(challenged),
            Attachments(challenger.Id).Concat(Attachments(challenged.Id)).Select(c => ViewCard(c)).ToArray(), [], null, false, DescribeChallenge(abilityId, source, refusable), []);
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
        ApplyDuelEntryTraits(duel); // Spiked Tetsubo (issue #622, IvoryPolitical.cs): "After this Personality enters a duel"
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
        var refusalStart = _state.Events.Count - 1;
        // Doji Shigeyuki: "He may refuse; if he does, move him home."
        if (duel.AbilityId == "shigeyuki-duel") MoveHome(Card(duel.ChallengedId), false);
        // Duel to First Blood (The Shadow's Embrace pass, issue #476 batch 3): "He may refuse; if he did, draw two cards." -- the challenger's player draws them.
        if (duel.AbilityId is "first-blood-duel-battle" or "first-blood-duel-limited") { Draw(Player(duel.ChallengerPlayerId)); Draw(Player(duel.ChallengerPlayerId)); }
        // Watchful Eye Dojo (Emperor Edition Stronghold pass, issue #486 batch 3): "He may refuse; if he did, dishonor him and gain 1 Honor."
        if (duel.AbilityId == "watchful-eye-duel") WatchfulEyeRefusal(duel);
        // Sanctioned Duel (issue #622, IvorySpells.cs): "He may refuse; if he does, dishonor him and gain 2 Honor."
        if (duel.AbilityId == IvorySpellAbilities.SanctionedDuel) SanctionedDuelRefusal(duel);
        _state.LastDuel = _state.LastDuel! with { Result = new("Refused", null, [], 0, 0, 0, 0, [], _state.Events.Skip(refusalStart).ToArray(), [], _state.Version + 1, _state.Phase, _state.PriorityPlayerId, "Challenge refused") };
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
            case P2FocusChoiceStage: ResolveP2FocusChoice(command); break; // issue #503 (EmperorP2Duel.cs)
            default: throw new RuleViolation("Unknown duel stage.");
        }
    }

    // Focus (core rules): play a card face-down from hand OR the top of the Fate deck, next to the
    // focusing personality -- not "playing" the card. FaceUp mirrors the duel's own reveal state
    // (Struck), matching a Hand card's own private-until-shown convention rather than inventing a new
    // one; command.Choice=="deck" selects the top-of-deck source, otherwise command.CardId names a
    // hand card.
    private bool CanFocusDiscard(int playerId, PendingDuel duel) =>
        !duel.HikaroDiscardFocusUsed[playerId] &&
        ClanAlignments(Card(playerId == duel.ChallengerPlayerId ? duel.ChallengerId : duel.ChallengedId)).Contains("Dragon") &&
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).GrantsDiscardPileFocus);

    private void Focus(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        // Reserve Weapon (issue #492 group G): "Before this Personality focuses: You may give him +3 to his duel stat until the duel ends instead of focusing."
        if (command.Choice == "reserve") { ReserveWeaponFocus(command); return; }
        if (command.Choice == GonjiFocusChoice) { GonjiFocus(command); return; } // Kakita Gonji (issue #503, EmperorP2Duel.cs): "instead of focusing that time"
        var isChallenger = command.PlayerId == duel.ChallengerPlayerId;
        var list = isChallenger ? duel.ChallengerFocused : duel.ChallengedFocused;
        Require(list.Count + P2FocusSubstitutions(command.PlayerId) < 4, "You have already focused four times this duel and must Strike."); // issue #503: a focus replaced "instead of focusing" counts
        string cardId; var fromDiscard = false;
        if (command.Choice == "deck")
        {
            var deck = Player(command.PlayerId).FateDeck;
            Require(deck.Count > 0, "Your Fate deck is empty.");
            cardId = deck[0]; deck.RemoveAt(0);
        }
        else if (command.Choice == "discard")
        {
            // Mirumoto Hikaro's own second trait: "before you focus in a duel involving your Dragon
            // Clan Personality, once per duel, you may focus from your discard pile instead" -- gated
            // on controlling a Hikaro-shaped card, not having used this already THIS duel, and the
            // duel's own dueling Personality on this side being Dragon Clan (not the Hikaro herself).
            Require(_state.Cards.Values.Any(c => c.OwnerId == command.PlayerId && InPlay(c) && Definition(c).GrantsDiscardPileFocus),
                "Focusing from your discard pile needs a card that grants it.");
            Require(!duel.HikaroDiscardFocusUsed[command.PlayerId], "Already focused from your discard pile once this duel.");
            Require(ClanAlignments(Card(isChallenger ? duel.ChallengerId : duel.ChallengedId)).Contains("Dragon"), "The duel must involve your own Dragon Clan Personality.");
            Require(command.CardId is not null && Card(command.CardId).OwnerId == command.PlayerId && Card(command.CardId).Zone is Zone.DynastyDiscard or Zone.FateDiscard,
                "Choose a card in your own discard pile.");
            cardId = command.CardId!; duel.HikaroDiscardFocusUsed[command.PlayerId] = true; duel.DiscardFocusedIds.Add(cardId); fromDiscard = true;
        }
        else
        {
            Require(command.CardId is not null && Player(command.PlayerId).Hand.Contains(command.CardId), "Choose a card from your own hand, or focus from the deck.");
            cardId = command.CardId!; Player(command.PlayerId).Hand.Remove(cardId);
        }
        var card = Card(cardId);
        // "Revealed to the opponent before being placed as a focus" -- a discard-pile focus is
        // already public information (it sat face up in a discard pile), unlike an ordinary
        // hand/deck focus, which stays hidden (FaceUp = duel.Struck) until a Strike reveals it.
        card.Zone = Zone.Focused; card.FaceUp = fromDiscard || duel.Struck; card.ProvinceIndex = null;
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
        duel.StrikerPlayerId = _state.PriorityPlayerId; // Bend like a Reed (Emperor Edition remaining-cards pass, group A, issue #492): who struck.
        foreach (var id in duel.ChallengerFocused) Card(id).FaceUp = true;
        foreach (var id in duel.ChallengedFocused) Card(id).FaceUp = true;
        Emit("duelStruck", "A Strike ends focusing; focused cards are revealed.", _state.PriorityPlayerId);
        RecordDuelReveal("Strike");
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
        if (order.Count == 0) { BeginOrderingEffectsAfterReactions(); return; }
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
        if (command.CardId is not null) RecordDuelReveal("Replacement");
        if (duel.PendingDuelistDecisions.Count == 0) BeginOrderingEffectsAfterReactions(); else _state.PriorityPlayerId = duel.PendingDuelistDecisions[0];
    }

    private void BeginOrderingEffects() => ContinueToOrderingEffects();

    private void ContinueToOrderingEffects()
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
        if (BeginP2PreTotalFocusEffects(0)) return; // issue #503 (EmperorP2Duel.cs): Killing Intent, A Champion in Court, Turning Away resolve before the totals
        ResolveDuel();
    }

    // The duel stat plus each side's own total focused Focus Value (revealed by the time this runs).
    // Any attachment modifying the stat applies as long as it is active right now -- CardForce/
    // CardPersonalHonor/PersonalityChi already read current, not printed, values.
    private int DuelStatValue(CardInstance card, string stat) => (P2DuelStat(card) /* issue #503: Killing Intent */ ?? NikaruDuelStat(card) ?? stat switch
    {
        "Force" => CardForce(card),
        "PersonalHonor" => CardPersonalHonor(card),
        _ => PersonalityChi(card),
    }) + RemADuelStatBonus(card) + P4DuelStatBonus(card, stat); // Secret Reserve (issue #503): +1C for each Weapon on the duelist. // Bend like a Reed (group A): "+2 to his duel stat until the duel ends"; Falling Rain Dojo (group F): +1 until the duel ends.

    private void ResolveDuel()
    {
        var duel = _state.PendingDuel!;
        var challenger = Card(duel.ChallengerId); var challenged = Card(duel.ChallengedId);
        // The Focus Effects that adjust Focus Values resolve before the totals, in the duel's Focus Effect order (issue #498, EmperorFollowupsDuel.cs): Duel to First Blood's "+1 Focus Value to each of your
        // focused cards" (The Shadow's Embrace pass, issue #476 batch 3) and Rend the Soul's "Raise this card's Focus Value to 5".
        var challengerTotal = DuelStatValue(challenger, duel.Stat) + FocusedTotal(duel, duel.ChallengerFocused);
        var challengedTotal = DuelStatValue(challenged, duel.Stat) + FocusedTotal(duel, duel.ChallengedFocused);
        string? winnerId; List<string> loserIds;
        var resolutionReason = "Higher duel total";
        if (challengerTotal > challengedTotal) { winnerId = duel.ChallengerId; loserIds = [duel.ChallengedId]; }
        else if (challengedTotal > challengerTotal) { winnerId = duel.ChallengedId; loserIds = [duel.ChallengerId]; }
        else
        {
            resolutionReason = "Duelist breaks the tie";
            // A tie: a Duelist on exactly one side wins; on neither side (or both) both sides lose.
            var challengerDuelist = Keyword(challenger, "Duelist"); var challengedDuelist = Keyword(challenged, "Duelist");
            if (challengerDuelist && !challengedDuelist) { winnerId = duel.ChallengerId; loserIds = [duel.ChallengedId]; }
            else if (challengedDuelist && !challengerDuelist) { winnerId = duel.ChallengedId; loserIds = [duel.ChallengerId]; }
            else { winnerId = null; loserIds = [duel.ChallengerId, duel.ChallengedId]; resolutionReason = "Equal totals · both lose"; }
        }
        // Rend the Soul (issue #498): "If your Personality wins this duel, he loses instead (the other Personality still also loses)" -- the duel becomes a tie (no winner, both lose), before anything
        // that asks who lost (the R2 delays, Honor Never Falls), the duel's own outcome and the other Focus Effects.
        var rendOfWinner = winnerId is null ? null : RendTheSoulOfWinner(duel, winnerId);
        if (rendOfWinner is not null) { winnerId = null; loserIds = [duel.ChallengerId, duel.ChallengedId]; resolutionReason = "Rend the Soul · the winner loses too"; }
        // The shared delay mechanism (issue #492, EmperorRulingR2Cards.cs): "You may choose (now) to delay the effects of losing this duel ..." is answered before the outcome applies.
        if (BeginDuelLossDelayDecisions(duel, loserIds)) return;
        if (TfBeginWitnessDecision(duel, winnerId)) return; // Awed Witness (issue #622, TfStrategiesA.cs): the winner's controller may create Witness -- asked once, then the duel resolves again
        var resultStart = _state.Events.Count;
        if (rendOfWinner is not null) Emit("duelWinnerLoses", $"{Definition(rendOfWinner).Name}: {Player(rendOfWinner.OwnerId).Name}'s Personality won the duel, so he loses instead; the other Personality still also loses.", rendOfWinner.OwnerId, rendOfWinner.Id);
        var strengthsBefore = _state.Players.SelectMany(p => p.Provinces.Select(v => new DuelProvince(p.Id, v.Index, EffectiveProvinceStrength(v), EffectiveProvinceStrength(v)))).ToArray();
        var challengerStat = DuelStatValue(challenger, duel.Stat);
        var challengedStat = DuelStatValue(challenged, duel.Stat);
        var winnerPlayerId = winnerId is null ? (int?)null : Card(winnerId).OwnerId;
        var loserPlayerIds = loserIds.Select(id => Card(id).OwnerId).ToList();
        Emit("duelResolved", winnerId is null ? "The duel is a tie; both personalities lose."
            : $"{Definition(Card(winnerId)).Name} wins the duel.", duel.ChallengerPlayerId);
        // The duel's own built-in outcome (each card's own printed win/loss text) applies first; any
        // Focus-Effect-driven outcome layers on top, in the active player's own chosen order -- e.g.
        // a Focus Effect that dishonors the loser, resolved before the duel's own "destroy the
        // loser," makes that death a dishonorable one instead of an honorable one.
        // The action's own negations still stand, and To First Blood's Focus Effect negates the destruction of every loser ("negate any destruction for losing this duel").
        _state.NegatedEffects = duel.NegatedEffects.ToList();
        foreach (var focusedId in duel.OrderedFocusEffects)
            if ((Definition(Card(focusedId)).Abilities ?? []).Contains("to-first-blood-focus"))
                foreach (var loserId in loserIds) _state.NegatedEffects.Add(new("destroy", loserId));
        ExplainDuelEffect("Outcome", _state.LastDuel!.Challenge?.Source,
            () => WithDuelLossDelays(duel, loserIds, () => ApplyDuelOutcome(duel.AbilityId, winnerId, loserIds, duel.ChallengerPlayerId)));
        // Kakita Ichigiku: "destroy that duel's loser in addition to other consequences of losing."
        if (duel.IchigikuDestroysLoser) WithDuelLossDelays(duel, loserIds, () => IchigikuDestroyLosers(loserIds, duel.ChallengerPlayerId));
        _state.NegatedEffects = [];
        // False Ring of Earth (oracle-l5r-10532, issue #295): "After you win a duel against a
        // Personality who shared no keywords with your Personality in the duel" -- Ring
        // recruitment's own existing shape (GrantRingRecruit, issue #194), reusing winnerId/loserIds
        // exactly as this resolution just computed them. A tie (winnerId null, or two loserIds) never
        // satisfies "you win," so both are excluded.
        if (winnerId is not null && loserIds.Count == 1)
            GrantRingRecruit(Player(Card(winnerId).OwnerId), SharesNoKeywords(Card(winnerId), Card(loserIds[0])), "false-ring-of-earth-recruit");
        duel.WinnerId = winnerId; duel.LoserIds = loserIds;
        // The retained result is completed in FinishDuelResolution, after any paused Focus Effect choice.
        duel.ResultEventStart = resultStart;
        duel.ResultDraft = new("Resolved", winnerId, loserIds.ToArray(), challengerStat, challengedStat, challengerTotal, challengedTotal,
            duel.OrderedFocusEffects.ToArray(), [], strengthsBefore, 0, _state.Phase, _state.PriorityPlayerId, resolutionReason);
        ContinueDuelFocusEffects(0);
    }

    // The duel's Focus Effects, in the order chosen, from the one at `from`: each applies, and one that needs a decision (Disreputable Deal's search) pauses the resolution here until
    // it is answered (FocusEffectChoice.cs), which then carries on from the next.
    private void ContinueDuelFocusEffects(int from)
    {
        var duel = _state.PendingDuel!;
        var winnerPlayerId = duel.WinnerId is null ? (int?)null : Card(duel.WinnerId).OwnerId;
        var loserPlayerIds = duel.LoserIds.Select(id => Card(id).OwnerId).ToList();
        for (var i = from; i < duel.OrderedFocusEffects.Count; i++)
        {
            var focusedId = duel.OrderedFocusEffects[i];
            var focusEffectAbility = (Definition(Card(focusedId)).Abilities ?? []).First(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "FocusEffect");
            if (FocusEffectNeedsChoice(focusEffectAbility) && BeginFocusEffectChoice(i, focusedId, focusEffectAbility)) return;
            ExplainDuelEffect("FocusEffect", ViewCard(Card(focusedId)),
                () => WithAnySourceLossDelays(duel, Card(focusedId).OwnerId, () => ApplyDuelFocusEffect(focusEffectAbility, Card(focusedId).OwnerId, winnerPlayerId, loserPlayerIds))); // issue #496: Honor Never Falls' "(from any source)"
        }
        FinishDuelResolution();
    }

    // Everything after the Focus Effects: the focused cards are spent and the duel's action completes.
    private void FinishDuelResolution()
    {
        var duel = _state.PendingDuel!;
        var winnerId = duel.WinnerId; var winnerPlayerId = winnerId is null ? (int?)null : Card(winnerId).OwnerId;
        // Every focused card is spent, win or lose (a card destroyed as part of the outcome above is
        // simply skipped here -- Card() would still resolve it, but it is no longer in Zone.Focused).
        // Mirumoto Hikaro's own "remove that card from the game after the duel ends" -- a
        // discard-pile focus (duel.DiscardFocusedIds) is removed instead of the ordinary FateDiscard.
        foreach (var id in duel.ChallengerFocused.Concat(duel.ChallengedFocused))
        {
            var card = Card(id);
            if (card.Zone != Zone.Focused) continue;
            if (duel.DiscardFocusedIds.Contains(id)) RemoveFromGame(card); else card.Zone = Zone.FateDiscard;
        }
        // Mirumoto Yozo, Experienced 2's own trait: "after your Iaijutsu action resolves, give any
        // Personality who performed it +2F" -- "regardless of how it resolves" (the user's own
        // clarification), so applied here unconditionally, win, lose, or tie, before anything below
        // touches priority. Not restricted to once per turn/personality -- ApplyModifier's own
        // Modifiers list stacks naturally, so a personality performing multiple Iaijutsu actions (or
        // the same one more than once) receives the bonus again each time.
        var performer = Card(duel.ChallengerId);
        if (IsIaijutsuAction(duel.AbilityId) && _state.Cards.Values.Any(c => c.OwnerId == performer.OwnerId && InPlay(c) && Definition(c).GrantsIaijutsuBonus))
            ExplainDuelEffect("Aftermath", ViewCard(_state.Cards.Values.First(c => c.OwnerId == performer.OwnerId && InPlay(c) && Definition(c).GrantsIaijutsuBonus)),
                () => ApplyModifier(performer, "yozo-exp2-iaijutsu", "yozo-exp2-iaijutsu-bonus", "Force", 2));
        var challengerPlayerId = duel.ChallengerPlayerId;
        // A duel-issuing ability resolves here, not in ExecuteAbilityEffects (which returned at IssueChallenge):
        // "after the resolution of your [keyword] action" traits fire once its whole outcome has applied.
        FireAfterOwnActionTraits(AbilityCatalog.All[duel.AbilityId], challengerPlayerId, duel.SourceCardId is null ? null : Card(duel.SourceCardId));
        FireCowedAndDefeatedActionWatches(challengerPlayerId, null, duel.ChallengedId); // The Shadow's Embrace pass (issue #476), batch 3: a challenge targets the challenged Personality.
        MiddayShadowPerformerActionEnded(performer); // Emperor Edition Stronghold pass (issue #486), batch 1: the challenger performed the duel-issuing action.
        var draft = duel.ResultDraft!;
        P3RecordDuelWinner(winnerId); // Kakita Kenta (issue #503): the duel's winner goes on the action's record
        P3RecordBowedPersonalities(duel.BowedBefore); // Hida Defenders (issue #503)
        _state.LastDuel = _state.LastDuel! with
        {
            Result = draft with
            {
                Consequences = _state.Events.Skip(duel.ResultEventStart).ToArray(),
                Provinces = draft.Provinces.Select(p => p with { After = EffectiveProvinceStrength(Player(p.PlayerId).Provinces[p.Index]) }).ToArray(),
                ResolutionVersion = _state.Version + 1,
                ReturnPhase = _state.Phase,
                ReturnPriority = _state.PriorityPlayerId
            }
        };
        FinishSwordForHireDuel(duel.AbilityId, duel.ChallengerId);
        _state.PendingDuel = null;
        if (duel.RingsBowedBefore is { } duelRings) RecordBowedRings(duelRings.ToHashSet()); // Deception Revealed (Mendon's R4 ruling): a Ring the duel's action bowed goes on its record (a Focus Effect's bows do not: ApplyDuelFocusEffect).
        ApplyHesMineBows(duel.ChallengerId, duel.ChallengedId); // He's Mine! (Emperor Edition remaining-cards pass, issue #492, group B): the substitute bows once the duel has ended.
        ApplyTfPersonalitiesDuelWin(winnerId); // Kakita Izumiko (issue #622, TfPersonalities.cs): "After Izumiko wins a duel, gain 1 Honor." -- a trait.
        ApplyTwinForksCity(winnerId); // Twin Forks City (The Shadow's Embrace pass, issue #476 batch 6): a trait, before any Reaction to the duel's end.
        // The duel itself is a single, complete action from challenge to consequences (issue #209):
        // once everything above has applied, the winning card's own controller gets a chance to
        // react to it having won -- before handing play back exactly where the challenge was issued.
        P3EndLockouts(new(challengerPlayerId, duel.SourceCardId, duel.AbilityId)); // issue #503: the duel's action has ended (a Reaction lockout ends)
        if (winnerId is not null && P3OpenDuelWonWindow(duel, winnerId, winnerPlayerId!.Value, 1 - challengerPlayerId)) return; // issue #503: the duel's after-action tracking runs when the window closes
        // Overconfident (issue #223): a chance to react to whichever side the duel's own outcome
        // destroyed (ApplyDuelOutcome/ApplyDuelFocusEffect, both above, already queued it via MarkDead).
        if (OpenOverconfidentDrawWindow(1 - challengerPlayerId)) return;
        // Action-effect tracking (issue #437): the duel is the action's resolution, the same reasoning FireAfterOwnActionTraits above already follows.
        if (FinishActionEffectTracking(duel.AbilityId, challengerPlayerId, duel.SourceCardId, challengerPlayerId, duel.ChallengerId)) return;
        NextAction(challengerPlayerId);
    }

    // "After a duel resolves during which [this card] won" (issue #209): scoped to the one specific
    // card that won, the same shape own-enters-play already uses for entering-play Reactions (see
    // OpenEntryReactionWindow) -- a tie has no single winner, so there is nothing to react with here.
    private string? DuelWonReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-won-duel");

    // Kakita Maratai (issue #299): "After a duel ends that your Personality won" -- the same window,
    // broadened from "the winning card's OWN reaction" to also reach any OTHER in-play, unbowed card of
    // the winner's own controller watching for ANY of that controller's Personalities winning (Trigger
    // "own-personality-won-duel"; Maratai herself need not be the duelist). Unbowed is required only to
    // decide whether the window is worth opening at all -- Maratai's bow is a cost, so a bowed watcher
    // could never use it anyway (the shared bowed-source Require in ValidateAbility rejects it too).
    // The winner's own controller is always the window's PendingChoice.PlayerId, so only their own
    // watchers ever match ValidateAbility's own trigger check. The "duelWonReaction" PendingChoice
    // itself is untouched (CardId is still the winning card), so every consumer of the existing own-won-
    // duel window -- Apply()'s pass/close handling included -- needs no change.
    private string? DuelWonBroadcastReactionAbility(CardInstance card) =>
        AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "Reaction" && def.Trigger == "own-personality-won-duel");

    private bool OpenDuelWonReactionWindow(string winnerId, int winnerPlayerId, int closesTo)
    {
        var card = Card(winnerId);
        var reactionId = DuelWonReactionAbility(card);
        var broadcastWatchers = _state.Cards.Values.Where(c => c.OwnerId == winnerPlayerId && InPlay(c) && !BowedBarsAbilities(c) && DuelWonBroadcastReactionAbility(c) is not null).ToArray();
        if (reactionId is null && broadcastWatchers.Length == 0) return false;
        // A fresh window per duel: clear any earlier use so the same card may react again to a later duel.
        if (reactionId is not null) card.AbilityUses.Remove(reactionId);
        foreach (var watcher in broadcastWatchers)
        {
            var watchId = DuelWonBroadcastReactionAbility(watcher)!;
            watcher.AbilityUses.Remove(AbilityCatalog.All[watchId].UsageKey ?? watchId);
        }
        _state.PendingChoice = new("duelWonReaction", winnerPlayerId, card.Id,
            reactionId is not null ? "After the duel resolved, you may use this card's own reaction, or pass."
                : "After a duel ended that your Personality won, you may use a card's own reaction, or pass.", closesTo);
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = winnerPlayerId;
        Emit("reactionWindow", "Duel-won reaction window opened.", winnerPlayerId, card.Id);
        return true;
    }

    // Mirumoto Yozo, Experienced 2 (issue #207 follow-up): "an action is an Iaijutsu action if it
    // has the keyword Iaijutsu or the action states that it is an Iaijutsu Action" -- the same small,
    // extendable per-ability-id classification shape AttackKind's own switch already uses. Every
    // currently self-performer duel-issuing ability is already documented as an "Iaijutsu duel"
    // (issue #207); extend this list as a future ability is wired with an explicit "Iaijutsu"
    // Timing prefix (e.g. "Iaijutsu Open").
    private static bool IsIaijutsuAction(string abilityId) => abilityId is "daisuke-duel" or "ichizo-exp-duel" or "shikei-duel"
        || AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.ActionKeywords?.Contains("Iaijutsu") == true;

    // Each duel-issuing ability's own printed win/loss text. loserIds has two entries only on a
    // double-loss tie; winnerId is null in that same case.
    // challengerPlayerId (Lady Matsu's Rage, the first Celestial): "a duel her controller initiated"
    // is the CHALLENGER specifically -- win or lose is irrelevant to whose action this is -- so this
    // is always duel.ChallengerPlayerId, never the winner's or a loser's own id.
    private void ApplyDuelOutcome(string abilityId, string? winnerId, List<string> loserIds, int challengerPlayerId)
    {
        switch (abilityId)
        {
            case "steel-on-steel-duel": DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId); break;
            case "sword-for-hire-create": DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId); break;
            case "training-duel-force":
            case "training-duel-honor":
            case "training-duel-open":
            case "training-duel-challenge": foreach (var id in loserIds) ApplyModifier(Card(id), id, abilityId, "Force", -1); break;
            // Dragon Clan review (issue #207): three real self-performer Iaijutsu duels.
            case "daisuke-duel": case "spirit-duelist-duel": foreach (var id in loserIds) MoveHome(Card(id), false); break;
            case "ichizo-exp-duel": case "shikei-duel": case "makoto-duel": case IvorySpellAbilities.SanctionedDuel: DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId); break;
            // Second City unwired-card audit (issue #264): "she won" is exactly "the challenger's own
            // side won" in a self-performer 1v1 duel -- there is no ally on that side to conflate her
            // with, so checking the winner's OWNER matches the printed text precisely.
            // Doji Shigeyuki: "Bow the duel's loser's unit."
            case "shigeyuki-duel":
                foreach (var loserId in loserIds) BowUnitByEffect(Card(loserId), challengerPlayerId);
                break;
            case "shiba-kudome-duel":
                foreach (var id in loserIds) if (!StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
                if (winnerId is not null && Card(winnerId).OwnerId == challengerPlayerId && ControlsKeyword(challengerPlayerId, "Shugenja"))
                    CreateToken(Card(winnerId), "Fire", abilityId, "Force", 1);
                break;
            // An Insult Answered (issue #453, batch 2): "Destroy the duel's loser. Give the duel's winner two +1F Revenge tokens."
            case "insult-answered-duel":
                DestroyUnits(loserIds.Select(Card).ToArray(), actingPlayerId: challengerPlayerId);
                if (winnerId is not null && InPlay(Card(winnerId))) { CreateToken(Card(winnerId), "Revenge", abilityId, "Force", 1); CreateToken(Card(winnerId), "Revenge", abilityId, "Force", 1); }
                break;
            // Doji Mitsuru (issue #453, batch 3): "Bow the duel's loser."
            case "mitsuru-duel":
                foreach (var id in loserIds) if (!StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
                break;
            // Reckless Duel (oracle-l5r-6209, issue #460): "Bow the duel's loser. Move him home." -- Doji Mitsuru's own bow plus Daisuke's own move-home, combined.
            // Doji Hakuseki (Forgotten Legacy pass, issue #467, batch 1; ForgottenLegacyBatch1.cs).
            case "hakuseki-duel": HakusekiDuelOutcome(winnerId, loserIds, challengerPlayerId); break;
            // Shiba Kudome - Experienced (The Shadow's Embrace pass, issue #476, batch 2; ShadowsEmbraceBatch2.cs).
            case "kudome-exp-duel": KudomeExpDuelOutcome(winnerId, loserIds, challengerPlayerId); break;
            // The Shadow's Embrace pass (issue #476), batch 1: Kakita Tadanobu (ShadowsEmbraceBatch1.cs).
            case "tadanobu-duel-battle": case "tadanobu-duel-limited": TadanobuDuelOutcome(winnerId, loserIds, challengerPlayerId); break;
            case TfStrategiesAAbilities.WitnessDuel: TfWitnessDuelOutcome(winnerId, loserIds, challengerPlayerId); break; // Awed Witness (issue #622)
            case "reckless-duel":
                foreach (var id in loserIds)
                {
                    if (!StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
                    MoveHome(Card(id), false);
                }
                break;
            // Duel of Haiku (oracle-l5r-2245, Forgotten Legacy pass issue #467 batch 3): "The winner gains 1 Honor. Bow and dishonor the loser." -- the winning
            // Personality's controller gains the Honor (a tie has no winner); every loser is bowed and dishonored.
            // Duel to First Blood (The Shadow's Embrace pass, issue #476 batch 3): "Bow the duel's loser."
            // Watchful Eye Dojo (Emperor Edition Stronghold pass, issue #486 batch 3): "Bow the duel's loser."
            case "first-blood-duel-battle":
            case "first-blood-duel-limited":
            case "watchful-eye-duel":
                foreach (var id in loserIds) if (InPlay(Card(id)) && !StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
                break;
            // The Aerie (Emperor Edition Stronghold pass, issue #486 batch 2): Duel of Haiku's outcome.
            case "aerie-duel": AerieDuelOutcome(winnerId, loserIds, challengerPlayerId); break;
            case EmperorP1Abilities.OchiaiDuel: QueueOchiaiDestruction(loserIds, challengerPlayerId); break; // Ochiai's Bravery (issue #503, EmperorP1.cs)
            case "duel-of-haiku":
                if (winnerId is not null) GainHonor(Card(winnerId).OwnerId, 1, "Duel of Haiku");
                foreach (var id in loserIds)
                {
                    if (!StalwartNegatesBow(Card(id), challengerPlayerId)) Card(id).Bowed = true;
                    Dishonor(Card(id));
                }
                break;
        }
    }

    // Each Focus-Effect-bearing focused card's own printed text, in the active player's chosen
    // order. winnerPlayerId/loserPlayerIds are null/empty-safe for a double-loss tie.
    private void ApplyDuelFocusEffect(string abilityId, int ownerId, int? winnerPlayerId, List<int> loserPlayerIds)
    {
        // Mendon (R4, issue #492): "a Focus Effect is not an action" -- what it does is not "an action's effects" (no mark on the duel's action, no Honor-change window applies to it).
        var wasInFocusEffect = _resolvingFocusEffect; _resolvingFocusEffect = true;
        var ringsBefore = BowedRingIds();
        try { ApplyDuelFocusEffectCore(abilityId, ownerId, winnerPlayerId, loserPlayerIds); }
        finally { _resolvingFocusEffect = wasInFocusEffect; ExcludeFocusEffectRingBows(ringsBefore); }
    }

    private void ApplyDuelFocusEffectCore(string abilityId, int ownerId, int? winnerPlayerId, List<int> loserPlayerIds)
    {
        switch (abilityId)
        {
            // The Sincerity of the Dragon and The Courtesy of the Crane: "After this duel ends, if you won it, give all your provinces +2 strength" -- until the turn ends (the default duration of an
            // ongoing effect; Mendon's ruling, issue #492).
            case "sincerity-dragon-focus":
                if (ownerId == winnerPlayerId) foreach (var province in Player(ownerId).Provinces.Where(p => !p.Destroyed)) GiveProvinceStrengthThisTurn(ownerId, province.Index, 2, "The Sincerity of the Dragon");
                break;
            case "courtesy-of-the-crane-focus":
                if (ownerId == winnerPlayerId) foreach (var province in Player(ownerId).Provinces.Where(p => !p.Destroyed)) GiveProvinceStrengthThisTurn(ownerId, province.Index, 2, "The Courtesy of the Crane");
                break;
            // To First Blood: "its winner's controller gains 2 Honor" (the destruction it negates was applied before the Focus Effects, in ResolveDuel).
            case "to-first-blood-focus":
                if (winnerPlayerId is int firstBloodWinner) GainHonor(firstBloodWinner, 2, "To First Blood");
                break;
            // Of One Instant: "Give your Personality in this duel Tactician." Shadow Tactics: "After this duel ends, the loser's controller draws a card." A Simple Yari: "After this duel ends, bow its winner."
            case "of-one-instant-focus":
                if (_state.PendingDuel is { } instantDuel) GrantKeyword(Card(instantDuel.ChallengerPlayerId == ownerId ? instantDuel.ChallengerId : instantDuel.ChallengedId), "Tactician", abilityId, "Turn");
                break;
            case "training-focus-bow-ring": foreach (var ring in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Ring && !c.Bowed).ToArray()) ring.Bowed = true; break;
            case "shadow-tactics-focus": foreach (var loserPlayer in loserPlayerIds.Distinct().ToArray()) Draw(Player(loserPlayer)); break;
            case "simple-yari-focus":
                if (_state.PendingDuel?.WinnerId is string yariWinnerId && InPlay(Card(yariWinnerId)) && !StalwartNegatesBow(Card(yariWinnerId), ownerId)) Card(yariWinnerId).Bowed = true;
                break;
            // Paralyzing Touch (The Shadow's Embrace pass, issue #476, batch 4): "After this duel ends, negate the effects of the next action performed by its winner" (a tie has none).
            case "paralyzing-touch-focus":
                if (_state.PendingDuel?.WinnerId is string paralyzedWinnerId && InPlay(Card(paralyzedWinnerId))) WatchParalyzingTouch(Card(paralyzedWinnerId), "Paralyzing Touch");
                break;
            // Sudden Movement (The Shadow's Embrace pass, issue #476, batch 4): "After this duel ends, give its winner +3F" (a tie has no winner).
            case "sudden-movement-focus":
            case "slow-death-focus": // The Slow Death (Emperor Edition remaining-cards pass, issue #492, group D): "After this duel ends, give its winner +3F."
                if (_state.PendingDuel?.WinnerId is string suddenWinnerId && InPlay(Card(suddenWinnerId))) ApplyModifier(Card(suddenWinnerId), suddenWinnerId, abilityId, "Force", 3);
                break;
            // Scavenging Party: "After this duel ends, give its winner -3F" (a tie has no winner).
            case "scavenging-party-focus":
                if (_state.PendingDuel?.WinnerId is string scavengedWinnerId && InPlay(Card(scavengedWinnerId))) ApplyModifier(Card(scavengedWinnerId), scavengedWinnerId, abilityId, "Force", -3);
                break;
            // Discovering a Conspiracy: "if your Personality won it, gain 2 Honor".
            case "discovering-conspiracy-focus":
                if (ownerId == winnerPlayerId) GainHonor(ownerId, 2, "Discovering a Conspiracy");
                break;
            // Home Guard (issue #435): "give your Personality in it +3F" -- mandatory (no "may"), the
            // same "your Personality in this duel" read of-one-instant-focus already established.
            case "home-guard-focus":
                if (_state.PendingDuel is { } homeGuardDuel)
                    ApplyModifier(Card(homeGuardDuel.ChallengerPlayerId == ownerId ? homeGuardDuel.ChallengerId : homeGuardDuel.ChallengedId), abilityId, abilityId, "Force", 3);
                break;
            // Seeds of Decay pass (issue #453), batch 1. Embracing Battle: "bow the winner" (A Simple Yari's). Ceaseless Vigil's "straighten a Personality" is a choice (FocusEffectChoice.cs).
            case "embracing-battle-focus":
                if (_state.PendingDuel?.WinnerId is string embracingWinnerId && InPlay(Card(embracingWinnerId)) && !StalwartNegatesBow(Card(embracingWinnerId), ownerId)) Card(embracingWinnerId).Bowed = true;
                break;
            // Final Confrontation (Emperor Edition remaining-cards pass, group A, issue #492): "After this duel ends, if your Personality won it, take the Imperial Favor."
            case "final-confrontation-focus":
                if (ownerId == winnerPlayerId) TakeImperialFavor(ownerId);
                break;
            // Feint & Strike (oracle-l5r-2506, Forgotten Legacy pass issue #467 batch 3): "After this duel's resolution, its winner's controller discards a card" -- queued
            // like Times of Strife's own forced discard and answered as the duel hands play back (FinishDuelResolution's OpenOverconfidentDrawWindow). A tie has no winner.
            case "feint-strike-focus":
                if (winnerPlayerId is int feintWinner && !P2HandDiscardBlocked(feintWinner, ownerId, false, "Feint & Strike")) _state.PendingForcedDiscards.Add(new(feintWinner, "Feint & Strike")); // issue #503: Pillowbook
                break;
            // Emperor Edition mechanics group E (issue #501): Greasing the Wheels (EmperorMechE.cs).
            default:
                ApplyRegretFocusEffect(abilityId, winnerPlayerId); // Regret (RegretEmbracingVirtue.cs)
                ApplyTfStrategiesAFocusEffect(abilityId, ownerId, winnerPlayerId); // Breaking the Rhythm (issue #622, TfStrategiesA.cs)
                ApplyMechEFocusEffect(abilityId);
                ApplyP5FocusEffect(abilityId, ownerId, winnerPlayerId); // issue #503: Searching for Answers (EmperorP5Hidden.cs)
                break;
        }
    }
}
