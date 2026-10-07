namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group F: the Events of event-wave-8. An Event resolves as its province is revealed in its owner's Events Phase (core rules); these twelve
// either stay in play for a while (Zone.EventInPlay, ending when the printed duration does), or ask a player a question first (a PendingEventChoice, answered with the "eventChoice" command and
// listed by EventChoiceActions), or both. Every printed text is the SCAN's (the scans omit the "After you reveal cards in your Provinces..." lead-in the Oracle text carries).
//
// New primitives:
//   * PendingEventChoice / "eventChoice": one decision of an Event at a time -- a card of a list, a yes/no, or "none" -- opened during the reveal (the Event stays in its province until it is
//     answered, Harsh Choices' shape), at the end of a turn (Abandoning the Fortunes) or when a queued optional draw comes due (Boastful Proclamation, Glory of the Shogun: drained beside the
//     forced discards by OpenOverconfidentDrawWindow);
//   * an Event that ends "until your Events Phase N turns from now begins" (CardInstance.ExpiresAtEventsPhaseTurnsTaken) besides the ones that end as a turn begins (ExpiresAtTurnsTaken);
//   * "no players may take Reactions" (ReactionsBarred); a Limited-action target bar on a player's Personalities (Disgrace); "becomes dishonorable from an action" (Successful Bounty);
//   * gaining a province to the left or right of a player's row (GainProvince), with the province indexes of everything that names one shifted; a lock on declaring an attack until a turn
//     number; ignoring Honor Requirements and a permanent maximum hand size bonus as player states;
//   * "equipping is a Battle/Open action for all players" as a timing grant of the Event (TimingGrants' shape), and the draw after the first attach of a costly card each turn.
public sealed class PendingEventChoice
{
    /// <summary>Which decision this is: abandon, delayed-arrival, moon-sun, naoharu-show, naoharu-take, rebuilding, benefices, draw.</summary>
    public string Kind { get; set; } = "";
    public string EventCardId { get; set; } = "";
    /// <summary>The player who decides now.</summary>
    public int PlayerId { get; set; }
    /// <summary>"reveal" (the Event is still in its province: finishing it continues the Events Phase), "deferred" (a queued question: priority resumes to ClosesTo) or "end-turn" (the turn ends after it).</summary>
    public string Origin { get; set; } = "reveal";
    public int ClosesTo { get; set; }
    /// <summary>The cards a card decision offers.</summary>
    public List<string> Candidates { get; set; } = [];
    /// <summary>What has been decided so far (Naoharu's shown card, Benefices' chosen cards, one entry per player), read by the step that follows.</summary>
    public List<string> Data { get; set; } = [];
    /// <summary>How many players have been asked (a decision every player makes in turn), or how many picks remain (Abandoning the Fortunes).</summary>
    public int Step { get; set; }
    public string Prompt { get; set; } = "";
}

public sealed partial class MatchEngine
{
    internal const string AbandoningFortunesEffect = "abandoning-the-fortunes", AlterHistoryEffect = "alter-history", BeneficesEffect = "benefices-of-the-emperor",
        BoastfulProclamationEffect = "boastful-proclamation", CherryBlossomEffect = "cherry-blossom-festival", DelayedArrivalEffect = "delayed-arrival", DisgraceEffect = "disgrace",
        GloryOfTheShogunEffect = "glory-of-the-shogun", MoonAndSunEffect = "moon-and-sun", NaoharusGiftEffect = "naoharus-gift", RebuildingTheEmpireEffect = "rebuilding-the-empire",
        SuccessfulBountyEffect = "successful-bounty";
    private const string BoastProvinceKey = "boastful-proclamation:province", BoastArmedKey = "boastful-proclamation:armed", AbandonTurnKey = "abandoning-the-fortunes:turn", GloryDrawTurnKey = "glory-of-the-shogun:draw-turn:";

    private static bool IsEmperorRemainingFEvent(string effect) => effect is AbandoningFortunesEffect or AlterHistoryEffect or BeneficesEffect or BoastfulProclamationEffect or CherryBlossomEffect
        or DelayedArrivalEffect or DisgraceEffect or GloryOfTheShogunEffect or MoonAndSunEffect or NaoharusGiftEffect or RebuildingTheEmpireEffect or SuccessfulBountyEffect;

    private IEnumerable<CardInstance> EventsInPlay(string effect) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == effect).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- resolving an Event as it is revealed ---------------------------------------------------------------------------------------------------------------

    // Returns true when the reveal paused for a decision (ContinueEventsReveal stops; the answer resumes it).
    private bool ResolveEmperorRemainingFEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            // "Until the game ends, before each player's turn ends ..." / "Until the game ends, equipping is a Battle/Open action ...": in play for good.
            case AbandoningFortunesEffect:
            case GloryOfTheShogunEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until the game ends.");
                return false;
            // "After the next time this game you destroy a province as the Attacker ...": waits in play for that.
            case AlterHistoryEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until the next time {player.Name} destroys a province as the Attacker.");
                return false;
            // "No players may take Reactions until your next Events Phase begins."
            case CherryBlossomEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play: no players may take Reactions until {player.Name}'s next Events Phase begins.", eventsPhaseIn: 1);
                return false;
            // "Until your Events Phase two turns from now begins, other players may not target your Personalities with Limited actions."
            case DisgraceEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s Events Phase two turns from now begins.", eventsPhaseIn: 2);
                return false;
            // "Until your Events Phase three turns from now begins, after each time a Personality opposing a Magistrate or Courtier becomes dishonorable from an action, ..."
            case SuccessfulBountyEffect:
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s Events Phase three turns from now begins.", eventsPhaseIn: 3);
                return false;
            // "If any other player controls a Personality, then at the start of your next turn, if this province still exists, permanently give it +3 strength and gain 3 Honor. Until your next
            // turn begins, after battle resolution destroys this province, ..." The condition is read as it resolves; the province is remembered by its index.
            case BoastfulProclamationEffect:
                card.AbilityUses[BoastProvinceKey] = province.Index;
                card.AbilityUses[BoastArmedKey] = _state.Cards.Values.Any(c => c.OwnerId != player.Id && InPlay(c) && Definition(c).Type == CardType.Personality) ? 1 : 0;
                KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins.", turnBeginsIn: 1);
                return false;
            case DelayedArrivalEffect:
                return BeginRevealChoice("delayed-arrival", player, card, _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(),
                    $"{Definition(card).Name}: choose a Personality in play. He may not be assigned to attack {player.Name} until {player.Name}'s next turn begins.");
            case MoonAndSunEffect:
                return AskMoonAndSun(player, card, 0) || DiscardResolvedEvent(player, province, card);
            case NaoharusGiftEffect:
                return BeginRevealChoice("naoharu-show", player, card, player.Hand.ToList(), $"{Definition(card).Name}: choose a card in your hand to show to the other player and offer as a gift.");
            case RebuildingTheEmpireEffect:
                return AskRebuilding(player, card, 0) || DiscardResolvedEvent(player, province, card);
            case BeneficesEffect:
                return AskBenefices(player, card, 0, []) || DiscardResolvedEvent(player, province, card);
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition group F Event '{Definition(card).Effect}'.");
        }
    }

    // The Event leaves its province (which refills) and stays in play, ending as its owner's turn begins (turnBeginsIn) or as his Events Phase begins (eventsPhaseIn) that many turns on.
    private void KeepEventInPlay(PlayerState player, ProvinceState province, CardInstance card, string message, int? turnBeginsIn = null, int? eventsPhaseIn = null)
    {
        province.CardId = null; card.ProvinceIndex = null;
        card.Zone = Zone.EventInPlay;
        if (turnBeginsIn is int begins) card.ExpiresAtTurnsTaken = player.TurnsTaken + begins;
        if (eventsPhaseIn is int phases) card.ExpiresAtEventsPhaseTurnsTaken = player.TurnsTaken + phases;
        Emit("eventEnteredPlay", message, player.Id, card.Id);
        Refill(player, province);
    }

    // An Event that had nothing to ask is over: it is discarded and its province refills (always false: the reveal carries on).
    private bool DiscardResolvedEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
        Emit("eventResolved", $"{Definition(card).Name}: nothing to decide, so it resolved and was discarded.", player.Id, card.Id);
        Refill(player, province);
        return false;
    }

    // The Event's province, wherever its row has moved it to: the one that still holds it.
    private ProvinceState EventProvince(CardInstance card) => Player(card.OwnerId).Provinces.Single(p => p.CardId == card.Id);

    // An Event that asked a question is done: it leaves its province (discarded, or in play), the province refills, and the Events Phase carries on.
    private void FinishRevealedEvent(CardInstance card, bool keep = false, int? turnBeginsIn = null, string? message = null)
    {
        var player = Player(card.OwnerId);
        var province = EventProvince(card);
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        if (keep) KeepEventInPlay(player, province, card, message ?? $"{Definition(card).Name} stays in play.", turnBeginsIn);
        else
        {
            province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
            Emit("eventResolved", message ?? $"{Definition(card).Name} resolved and was discarded.", player.Id, card.Id);
            Refill(player, province);
        }
        ContinueEventsReveal(player);
    }

    // Opens a decision of an Event during its reveal; with nothing to choose the Event is simply over (returns false: the caller's reveal loop carries on).
    private bool BeginRevealChoice(string kind, PlayerState chooser, CardInstance card, List<string> candidates, string prompt, int step = 0, List<string>? data = null)
    {
        if (candidates.Count == 0 && kind is "delayed-arrival" or "naoharu-show")
        {
            var province = EventProvince(card);
            province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard;
            Emit("eventResolved", $"{Definition(card).Name}: nothing to choose, so it resolved and was discarded.", card.OwnerId, card.Id);
            Refill(Player(card.OwnerId), province);
            return false;
        }
        OpenEventChoice(new PendingEventChoice { Kind = kind, EventCardId = card.Id, PlayerId = chooser.Id, Origin = "reveal", Candidates = candidates, Step = step, Data = data ?? [], Prompt = prompt });
        return true;
    }

    private void OpenEventChoice(PendingEventChoice pending)
    {
        _state.PendingEventChoice = pending;
        _state.PendingChoice = new("eventChoice", pending.PlayerId, pending.EventCardId, pending.Prompt, pending.Origin == "deferred" ? pending.ClosesTo : null);
        _state.PriorityPlayerId = pending.PlayerId; _state.ConsecutivePasses = 0;
        Emit("eventChoice", $"{Player(pending.PlayerId).Name} must decide ({Definition(Card(pending.EventCardId)).Name}).", pending.PlayerId, pending.EventCardId);
    }

    // A question queued for when the action that owes it is over (the draw Boastful Proclamation and Glory of the Shogun offer).
    private bool OpenQueuedEventChoice(int closesTo)
    {
        if (_state.PendingEventChoices.Count == 0) return false;
        var next = _state.PendingEventChoices[0]; _state.PendingEventChoices.RemoveAt(0);
        next.Origin = "deferred"; next.ClosesTo = closesTo;
        OpenEventChoice(next);
        return true;
    }

    private void QueueOptionalDraw(int playerId, CardInstance eventCard, string prompt) =>
        _state.PendingEventChoices.Add(new PendingEventChoice { Kind = "draw", EventCardId = eventCard.Id, PlayerId = playerId, Prompt = prompt });

    // ---- answering ------------------------------------------------------------------------------------------------------------------------------------------

    private void ResolveEventChoiceCommand(GameCommand command)
    {
        var pending = _state.PendingEventChoice ?? throw new RuleViolation("No Event decision is pending.");
        Require(command.Type == "eventChoice" && command.PlayerId == pending.PlayerId, "Answer the Event's decision first.");
        var card = Card(pending.EventCardId); var player = Player(pending.PlayerId);
        switch (pending.Kind)
        {
            case "delayed-arrival":
                {
                    Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose a Personality in play.");
                    var chosen = Card(command.TargetId!);
                    _state.EventTargets[card.Id] = chosen.Id;
                    Emit("eventChose", $"{Definition(card).Name}: {Definition(chosen).Name} may not be assigned to attack {Player(card.OwnerId).Name} until {Player(card.OwnerId).Name}'s next turn begins.", card.OwnerId, chosen.Id);
                    FinishRevealedEvent(card, keep: true, turnBeginsIn: 1, message: $"{Definition(card).Name} stays in play until {Player(card.OwnerId).Name}'s next turn begins.");
                    return;
                }
            case "moon-sun":
                {
                    Require(command.Choice == "none" || command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose an Event in your discard pile, or none.");
                    if (command.TargetId is not null && command.Choice != "none")
                    {
                        var event_ = Card(command.TargetId);
                        event_.Zone = Zone.DynastyDeck; event_.FaceUp = false;
                        player.DynastyDeck.Add(event_.Id);
                        ShuffleDeck(player, SearchDeck.Dynasty);
                        Emit("eventResolved", $"{Definition(card).Name}: {player.Name} shuffled {Definition(event_).Name} into their Dynasty deck.", player.Id, event_.Id);
                    }
                    else Emit("eventChose", $"{Definition(card).Name}: {player.Name} shuffled nothing.", player.Id, card.Id);
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    if (AskMoonAndSun(Player(card.OwnerId), card, pending.Step + 1)) return;
                    FinishRevealedEvent(card);
                    return;
                }
            case "naoharu-show":
                {
                    Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId) && player.Hand.Contains(command.TargetId), "Choose a card in your hand.");
                    var shown = Card(command.TargetId!);
                    RecordShown(player.Id, shown, card, "Hand", "event");
                    Emit("eventChose", $"{Definition(card).Name}: {player.Name} shows {Definition(shown).Name} and offers it as a gift.", player.Id, shown.Id);
                    var other = Player(1 - player.Id);
                    OpenEventChoice(new PendingEventChoice
                    {
                        Kind = "naoharu-take",
                        EventCardId = card.Id,
                        PlayerId = other.Id,
                        Origin = "reveal",
                        Data = [shown.Id],
                        Prompt = $"{Definition(card).Name}: {player.Name} offers you {Definition(shown).Name} as a gift. Put it in your hand?"
                    });
                    return;
                }
            case "naoharu-take":
                {
                    Require(command.Choice is "yes" or "no", "Choose whether to put the card in your hand.");
                    var giver = Player(card.OwnerId); var shown = Card(pending.Data[0]);
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    if (command.Choice == "yes" && P2CardIntoHandBlocked(player.Id)) { } // issue #503: The Scorpion Wall is Finished -- the gift is not put into his hand and stays with its giver
                    else if (command.Choice == "yes")
                    {
                        giver.Hand.Remove(shown.Id); player.Hand.Add(shown.Id); shown.OriginalOwnerId ??= giver.Id; shown.OwnerId = player.Id; // his to play; it returns to its owner's pile when it leaves his hand
                        Emit("eventResolved", $"{Definition(card).Name}: {player.Name} put the gift in their hand.", player.Id, shown.Id);
                        GainHonor(giver.Id, Definition(shown).Focus, Definition(card).Name, card);
                    }
                    else
                    {
                        giver.Hand.Remove(shown.Id); shown.Zone = Zone.FateDiscard; shown.FaceUp = true;
                        Emit("eventResolved", $"{Definition(card).Name}: {player.Name} refused the gift; {giver.Name} discards {Definition(shown).Name} and draws two cards.", giver.Id, shown.Id);
                        Draw(giver); Draw(giver);
                    }
                    FinishRevealedEvent(card);
                    return;
                }
            case "rebuilding":
                {
                    Require(command.Choice is "yes" or "no", "Choose whether to gain a province.");
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    if (command.Choice == "yes") RebuildingGains(player, card);
                    if (AskRebuilding(Player(card.OwnerId), card, pending.Step + 1)) return;
                    FinishRevealedEvent(card);
                    return;
                }
            case "benefices":
                {
                    Require(command.Choice == "none" || command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose a card in your hand, or none.");
                    var data = pending.Data.Append(command.Choice == "none" ? "" : $"{player.Id}|{command.TargetId}").ToList();
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    if (AskBenefices(Player(card.OwnerId), card, pending.Step + 1, data)) return;
                    ResolveBenefices(card, data);
                    return;
                }
            case "abandon":
                {
                    Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of your Personalities or Holdings to destroy.");
                    var victim = Card(command.TargetId!);
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    Emit("eventChose", $"{Definition(card).Name}: {player.Name} destroys {Definition(victim).Name}.", player.Id, victim.Id);
                    DestroyUnits([victim]);
                    ContinueAbandoning(card, player.Id, pending.Step - 1);
                    return;
                }
            case "draw":
                {
                    Require(command.Choice is "yes" or "no", "Choose whether to draw a card.");
                    _state.PendingChoice = null; _state.PendingEventChoice = null;
                    if (command.Choice == "yes") { Draw(player); Emit("eventResolved", $"{Definition(card).Name}: {player.Name} drew a card.", player.Id, card.Id); }
                    ResumeAfterDeferredWindow(pending.ClosesTo);
                    return;
                }
            default:
                if (ResolveP4EventChoice(command, pending)) return; // Too Close to Home (issue #503, EmperorP4Watches.cs)
                if (PromoEventChoice(pending, command)) return; // issue #653 (PromoEmperorEvents.cs)
                if (WlItemsMiscEventChoice(pending, command)) return; // issue #674 (WlItemsMiscEvents.cs): Purge of Fudoism, Riot in the Second City
                if (SearchPlayEventChoice(pending, command)) return; // issue #667 (SearchPlayTopaz.cs): The Topaz Armor's two keywords
                if (ResolveEmperorP6Choice(pending, command)) return; // issue #503 (EmperorP6.cs): Dark Experiments, the face-up refills, Ietsuno's switch
                if (ResolveP6EventChoice(pending, command)) return; // primitive P6 (issue #705, AfterFacts.cs): a Celestial's entry decision
                if (ResolveP5EventChoice(pending, command)) return; // primitive P5 (issue #711, ProvinceRevealEvents.cs): a province-reveal Event's decision
                if (ResolveFortifyChoice(pending, command)) return; // primitive P15 (issue #719, FortificationHoldings.cs): which province a Fortification guards
                if (ResolveWatchEventChoice(pending, command)) return; // primitive P8 (issue #712, ActionEffectWatch.cs): a watch's choice
                throw new RuleViolation("Unknown Event decision.");
        }
    }

    // The decider's legal answers: one per candidate card, "none" where the decision is optional, yes/no where it is a question.
    private IEnumerable<ActionOption> EventChoiceActions(int playerId)
    {
        if (_state.PendingEventChoice is not { } pending || pending.PlayerId != playerId) yield break;
        var eventName = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        switch (pending.Kind)
        {
            case "p4-too-close": // Too Close to Home (issue #503, EmperorP4Watches.cs)
                foreach (var option in P4EventChoiceActions(pending, playerId)) yield return option;
                break;
            case "moon-sun":
            case "benefices":
                foreach (var id in pending.Candidates)
                    yield return new("eventChoice", $"{eventName}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
                yield return new("eventChoice", $"{eventName}: none", true, null, Answer(choice: "none"), pending.EventCardId);
                break;
            case "delayed-arrival":
            case "naoharu-show":
            case "abandon":
                foreach (var id in pending.Candidates)
                    yield return new("eventChoice", $"{eventName}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
                break;
            case "naoharu-take":
                yield return new("eventChoice", $"{eventName}: put {Definition(Card(pending.Data[0])).Name} in your hand", true, null, Answer(choice: "yes"), pending.EventCardId);
                yield return new("eventChoice", $"{eventName}: refuse the gift", true, null, Answer(choice: "no"), pending.EventCardId);
                break;
            case "rebuilding":
                yield return new("eventChoice", $"{eventName}: gain a province", true, null, Answer(choice: "yes"), pending.EventCardId);
                yield return new("eventChoice", $"{eventName}: decline", true, null, Answer(choice: "no"), pending.EventCardId);
                break;
            case "draw":
                yield return new("eventChoice", $"{eventName}: draw a card", true, null, Answer(choice: "yes"), pending.EventCardId);
                yield return new("eventChoice", $"{eventName}: do not draw", true, null, Answer(choice: "no"), pending.EventCardId);
                break;
            default:
                foreach (var option in PromoEventChoiceActions(pending, playerId)) yield return option; // issue #653
                foreach (var option in WlItemsMiscEventChoiceActions(pending, playerId)) yield return option; // issue #674 (WlItemsMiscEvents.cs)
                foreach (var option in SearchPlayEventChoiceActions(pending, playerId)) yield return option; // issue #667 (SearchPlayTopaz.cs)
                foreach (var option in EmperorP6ChoiceActions(pending, playerId)) yield return option; // issue #503 (EmperorP6.cs)
                foreach (var option in P6EventChoiceActions(pending, playerId)) yield return option; // primitive P6 (issue #705, AfterFacts.cs)
                foreach (var option in P5EventChoiceActions(pending, playerId)) yield return option; // primitive P5 (issue #711, ProvinceRevealEvents.cs)
                foreach (var option in FortifyChoiceActions(pending, playerId)) yield return option; // primitive P15 (issue #719, FortificationHoldings.cs)
                foreach (var option in WatchEventChoiceActions(pending, playerId)) yield return option; // primitive P8 (issue #712, ActionEffectWatch.cs)
                break;
        }
    }

    // ---- Abandoning the Fortunes ----------------------------------------------------------------------------------------------------------------------------

    // "Until the game ends, before each player's turn ends, if his Dynasty deck has no cards, he chooses and destroys one Personality or Holding he controls for each of his provinces without a
    // Dynasty card in it." Asked at the start of EndTurn for the player whose turn is ending; with no more candidates than picks they are destroyed without a choice. Returns true when it paused.
    private bool BeginAbandoningTheFortunes()
    {
        var player = Player(_state.ActivePlayerId);
        if (player.DynastyDeck.Count > 0) return false;
        foreach (var card in EventsInPlay(AbandoningFortunesEffect))
        {
            if (card.AbilityUses.TryGetValue(AbandonTurnKey, out var stamped) && stamped == _state.TurnNumber) continue;
            card.AbilityUses[AbandonTurnKey] = _state.TurnNumber;
            var empty = player.Provinces.Count(p => !p.Destroyed && p.CardId is null);
            if (empty == 0) continue;
            if (ContinueAbandoning(card, player.Id, empty, fromEndTurn: true)) return true;
        }
        return false;
    }

    // Destroys the player's Personalities and Holdings (one at a time, by his choice) until `remaining` are gone or none is left; true when it opened a choice.
    private bool ContinueAbandoning(CardInstance card, int playerId, int remaining, bool fromEndTurn = false)
    {
        var candidates = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Holding).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        if (remaining > 0 && candidates.Count > 0 && candidates.Count <= remaining)
        {
            foreach (var id in candidates.ToArray()) { Emit("eventResolved", $"{Definition(card).Name}: {Definition(Card(id)).Name} is destroyed.", playerId, id); DestroyUnits([Card(id)]); }
            remaining = 0;
        }
        if (remaining > 0 && candidates.Count > 0)
        {
            OpenEventChoice(new PendingEventChoice
            {
                Kind = "abandon",
                EventCardId = card.Id,
                PlayerId = playerId,
                Origin = "end-turn",
                Candidates = candidates,
                Step = remaining,
                Prompt = $"{Definition(card).Name}: choose one of your Personalities or Holdings to destroy ({remaining} to go)."
            });
            return true;
        }
        if (fromEndTurn) return false;
        // The last pick was answered: the turn ends now (this Event's stamp keeps it from asking again).
        EndTurn();
        return true;
    }

    // ---- Moon and Sun ---------------------------------------------------------------------------------------------------------------------------------------

    // "Each player may shuffle an Event in his discard pile into his deck." -- the revealing player first, then the other; a player with no Event there is not asked.
    private bool AskMoonAndSun(PlayerState owner, CardInstance card, int step)
    {
        for (var i = step; i < 2; i++)
        {
            var who = Player(i == 0 ? owner.Id : 1 - owner.Id);
            var events = _state.Cards.Values.Where(c => c.OwnerId == who.Id && c.Zone == Zone.DynastyDiscard && Definition(c).Type == CardType.Event).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
            if (events.Count == 0) continue;
            OpenEventChoice(new PendingEventChoice
            {
                Kind = "moon-sun",
                EventCardId = card.Id,
                PlayerId = who.Id,
                Origin = "reveal",
                Candidates = events,
                Step = i,
                Prompt = $"{Definition(card).Name}: you may shuffle an Event in your discard pile into your Dynasty deck."
            });
            return true;
        }
        return false;
    }

    // ---- Rebuilding the Empire ------------------------------------------------------------------------------------------------------------------------------

    // "Any player with three or fewer provinces may gain a province to the right of his rightmost province." Each such player is asked in turn, the revealing player first.
    private bool AskRebuilding(PlayerState owner, CardInstance card, int step)
    {
        for (var i = step; i < 2; i++)
        {
            var who = Player(i == 0 ? owner.Id : 1 - owner.Id);
            if (who.Provinces.Count(p => !p.Destroyed) > 3) continue;
            OpenEventChoice(new PendingEventChoice
            {
                Kind = "rebuilding",
                EventCardId = card.Id,
                PlayerId = who.Id,
                Origin = "reveal",
                Step = i,
                Prompt = $"{Definition(card).Name}: you have {who.Provinces.Count(p => !p.Destroyed)} provinces. Gain a province to the right of your rightmost province? Your Family Honor is lowered to its starting value and you may not declare an attack until {owner.Name}'s next turn begins."
            });
            return true;
        }
        return false;
    }

    private void RebuildingGains(PlayerState who, CardInstance card)
    {
        GainProvince(who, left: false);
        var start = Definition(Card(who.StrongholdId)).StartingHonor;
        if (who.Honor > start)
        {
            // "This Honor loss will not be negated or given to another player instead": the Honor is set, not routed through GainHonor's negations.
            var lost = who.Honor - start;
            who.Honor = start; RecordHonorLoss(who.Id);
            Emit("honorChanged", $"{who.Name}: -{lost} Honor ({Definition(card).Name}: lowered to its starting value).", who.Id, Amount: -lost);
            CheckFormalApology(who.Id);
        }
        // "He may not declare an attack until your next turn begins": through this turn and the next one (the revealing player's next turn is two turns on).
        who.AttackLockedThroughTurn = _state.TurnNumber + 1;
        Emit("attackLocked", $"{who.Name} may not declare an attack until {Player(card.OwnerId).Name}'s next turn begins.", who.Id);
    }

    private bool AttackLockedByEvent(int playerId) => _state.TurnNumber <= Player(playerId).AttackLockedThroughTurn;

    // ---- gaining a province -----------------------------------------------------------------------------------------------------------------------------------

    // A new province at the left or right end of the row, at the Stronghold's base strength, filled face-down from the Dynasty deck. A province to the LEFT renumbers the row: every province,
    // and every card that names one of this player's provinces by its index, moves up by one (Events resolve in the Events Phase, when no battlefield exists).
    private ProvinceState GainProvince(PlayerState player, bool left, int? baseStrength = null, bool holdsNoDynastyCards = false)
    {
        // Issue #503 (EmperorP6.cs): Kuni Renyu's "a province with a base strength of 4" and Illusory Defense's "which permanently does not hold Dynasty cards".
        var strength = baseStrength ?? Definition(Card(player.StrongholdId)).ProvinceStrength;
        var province = new ProvinceState { Strength = strength, PrintedStrength = strength, HoldsNoDynastyCards = holdsNoDynastyCards };
        if (left)
        {
            foreach (var existing in player.Provinces) existing.Index++;
            foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.ProvinceIndex is not null && c.Zone is Zone.Province or Zone.Region)) card.ProvinceIndex++;
            foreach (var evt in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.EventInPlay && c.AbilityUses.ContainsKey(BoastProvinceKey))) evt.AbilityUses[BoastProvinceKey]++;
            _state.ProvinceSights = _state.ProvinceSights.Select(s => s.OwnerId == player.Id ? s with { Province = s.Province + 1 } : s).ToList();
            if (_state.Phase == Phase.Events && _state.ActivePlayerId == player.Id) _state.EventsRevealNext++;
            ShiftProvinceKeysForGainedLeftProvince(player); // Issue #503 (EmperorP6.cs): every other record naming one of his provinces by index (a battle at them included).
            province.Index = 0;
            player.Provinces.Insert(0, province);
        }
        else
        {
            province.Index = player.Provinces.Count;
            player.Provinces.Add(province);
        }
        Refill(player, province);
        Emit("provinceGained", $"{player.Name} gained a province to the {(left ? "left of their leftmost" : "right of their rightmost")} province.", player.Id);
        return province;
    }

    // ---- Benefices of the Emperor ----------------------------------------------------------------------------------------------------------------------------

    // "Each player in turn order starting with you may choose a card in his hand." Asked of each player in turn (a player with an empty hand is not); the choices stay hidden until all are made.
    private bool AskBenefices(PlayerState owner, CardInstance card, int step, List<string> data)
    {
        for (var i = step; i < 2; i++)
        {
            var who = Player(i == 0 ? owner.Id : 1 - owner.Id);
            if (who.Hand.Count == 0) continue;
            OpenEventChoice(new PendingEventChoice
            {
                Kind = "benefices",
                EventCardId = card.Id,
                PlayerId = who.Id,
                Origin = "reveal",
                Candidates = who.Hand.ToList(),
                Step = i,
                Data = data,
                Prompt = $"{Definition(card).Name}: you may choose a card in your hand. All chosen cards are discarded; the lowest and highest Focus Values win."
            });
            return true;
        }
        return false;
    }

    // Discard every chosen card, then: exactly one player's card with the lowest Focus Value of them all gains a province to the left of his leftmost province; exactly one player's with the highest
    // gets +3 maximum hand size permanently and draws three cards. (data holds "player|card" for each card chosen; "" for a player who chose none.)
    private void ResolveBenefices(CardInstance card, List<string> data)
    {
        var chosen = new List<(PlayerState Player, CardInstance Card)>();
        foreach (var entry in data.Where(d => d.Length > 0))
        {
            var parts = entry.Split('|');
            chosen.Add((Player(int.Parse(parts[0])), Card(parts[1])));
        }
        foreach (var (who, discarded) in chosen)
        {
            who.Hand.Remove(discarded.Id); discarded.Zone = Zone.FateDiscard; discarded.FaceUp = true;
            Emit("handDiscard", $"{who.Name} discarded {Definition(discarded).Name} ({Definition(card).Name}).", who.Id, discarded.Id);
        }
        if (chosen.Count > 0)
        {
            var lowest = chosen.Min(c => Definition(c.Card).Focus); var highest = chosen.Max(c => Definition(c.Card).Focus);
            var lows = chosen.Where(c => Definition(c.Card).Focus == lowest).ToArray(); var highs = chosen.Where(c => Definition(c.Card).Focus == highest).ToArray();
            if (lows.Length == 1) { Emit("eventResolved", $"{Definition(card).Name}: {lows[0].Player.Name}'s card has the lowest Focus Value.", lows[0].Player.Id, card.Id); GainProvince(lows[0].Player, left: true); }
            if (highs.Length == 1)
            {
                var winner = highs[0].Player;
                winner.MaxHandBonus += 3;
                Emit("eventResolved", $"{Definition(card).Name}: {winner.Name}'s card has the highest Focus Value: +3 maximum hand size, and three cards drawn.", winner.Id, card.Id);
                Draw(winner); Draw(winner); Draw(winner);
            }
        }
        FinishRevealedEvent(card);
    }

    // ---- ending, and what an Event in play does ---------------------------------------------------------------------------------------------------------------

    // Cherry Blossom Festival, Disgrace, Successful Bounty leave play as their owner's Events Phase begins.
    private void ExpireEmperorRemainingFEventsAtEventsPhase(PlayerState player)
    {
        foreach (var expired in _state.Cards.Values.Where(c => c.OwnerId == player.Id && c.Zone == Zone.EventInPlay && c.ExpiresAtEventsPhaseTurnsTaken == player.TurnsTaken).ToArray())
        {
            expired.Zone = Zone.DynastyDiscard;
            Emit("eventExpired", $"{Definition(expired).Name}'s effect ended.", expired.OwnerId, expired.Id);
        }
    }

    // Boastful Proclamation's "at the start of your next turn, if this province still exists, permanently give it +3 strength and gain 3 Honor" (when its condition held as it resolved), and Delayed
    // Arrival's named Personality, are settled as their Event's turn-begins duration ends.
    private void OnEmperorRemainingFEventExpired(CardInstance expired)
    {
        _state.EventTargets.Remove(expired.Id);
        if (Definition(expired).Effect != BoastfulProclamationEffect || !expired.AbilityUses.TryGetValue(BoastArmedKey, out var armed) || armed != 1) return;
        var owner = Player(expired.OwnerId);
        if (!expired.AbilityUses.TryGetValue(BoastProvinceKey, out var index) || index < 0 || index >= owner.Provinces.Count || owner.Provinces[index].Destroyed) return;
        owner.Provinces[index].Strength += 3;
        Emit("provinceStrength", $"{Definition(expired).Name}: {owner.Name}'s province {index + 1} permanently has +3 strength.", owner.Id, expired.Id);
        GainHonor(owner.Id, 3, Definition(expired).Name, expired);
    }

    // After battle resolution destroys a province: Alter History ("the Defender loses 5 Honor ... "), and Boastful Proclamation ("give each Personality in the attacking army a +1F token and the
    // Attacker may draw a card").
    private void AfterProvinceDestroyedByResolution(int attacker, int defender, ProvinceState province, CardInstance[] attackingArmy)
    {
        WlPersGates1AfterProvinceDestroyed(attacker, attackingArmy); // Boyoh Mercenary (issue #674, WlPersGates1.cs): "After Boyoh Mercenary's army destroys a Province, draw a card."
        foreach (var boast in EventsInPlay(BoastfulProclamationEffect).Where(e => e.OwnerId == defender && e.AbilityUses.TryGetValue(BoastProvinceKey, out var i) && i == province.Index).ToArray())
        {
            foreach (var personality in attackingArmy.Where(c => c.Zone == Zone.Battlefield && Definition(c).Type == CardType.Personality))
                CreateToken(personality, "Boast", boast.Id, "Force", 1);
            QueueOptionalDraw(attacker, boast, $"{Definition(boast).Name}: {Player(attacker).Name} may draw a card.");
        }
        foreach (var history in EventsInPlay(AlterHistoryEffect).Where(e => e.OwnerId == attacker).ToArray())
        {
            var loser = Player(defender); var start = Definition(Card(loser.StrongholdId)).StartingHonor; var before = loser.Honor;
            history.Zone = Zone.DynastyDiscard;
            Emit("eventResolved", $"{Definition(history).Name}: {loser.Name} loses 5 Honor.", loser.Id, history.Id);
            GainHonor(defender, -5, Definition(history).Name, history);
            if (before >= start && loser.Honor < start)
            {
                loser.IgnoresHonorRequirements = true;
                Emit("eventResolved", $"{Definition(history).Name}: {loser.Name} may ignore Honor Requirements when bringing Personalities into play until the game ends.", loser.Id, history.Id);
            }
        }
    }

    // Test seam: a province destroyed by a battle's resolution, with what that triggers (the real resolution is exercised in the battle scenarios).
    internal void ProvinceDestroyedForTest(int attacker, int defender, int provinceIndex, string[] attackingArmyIds)
    {
        var province = Player(defender).Provinces[provinceIndex];
        DestroyProvince(defender, province);
        AfterProvinceDestroyedByResolution(attacker, defender, province, attackingArmyIds.Select(Card).ToArray());
        OpenOverconfidentDrawWindow(attacker);
    }

    // ---- Cherry Blossom Festival -------------------------------------------------------------------------------------------------------------------------------

    // "No players may take Reactions until your next Events Phase begins."
    private bool ReactionsBarred() => _state.Cards.Values.Any(c => c.Zone == Zone.EventInPlay && Definition(c).Effect == CherryBlossomEffect);

    // ---- Disgrace ---------------------------------------------------------------------------------------------------------------------------------------------------

    // "Other players may not target your Personalities with Limited actions."
    private void RequireDisgraceAllowsTargets(AbilityDefinition ability, GameCommand command, PlayerState player)
    {
        if (ability.Timing != "Limited") return;
        foreach (var disgrace in EventsInPlay(DisgraceEffect).Where(e => e.OwnerId != player.Id))
            foreach (var targetId in ActionTargetIds(command))
                if (_state.Cards.TryGetValue(targetId, out var target) && InPlay(target) && Definition(target).Type == CardType.Personality && target.OwnerId == disgrace.OwnerId)
                    throw new RuleViolation($"{Definition(target).Name} may not be targeted by Limited actions of other players (Disgrace).");
    }

    // ---- Delayed Arrival --------------------------------------------------------------------------------------------------------------------------------------------

    // "He may not be assigned to attack you until your next turn begins."
    private void RequireDelayedArrivalMayAssign(CardInstance personality, bool isDefender)
    {
        if (isDefender) return;
        foreach (var arrival in EventsInPlay(DelayedArrivalEffect))
            Require(!DelayedArrivalBars(arrival, personality),
                $"Delayed Arrival: {Definition(personality).Name} may not be assigned to attack {Player(arrival.OwnerId).Name} until their next turn begins.");
    }

    // The named Personality of another player than the Event's owner (an attack on "you" is what is barred).
    private bool DelayedArrivalBars(CardInstance arrival, CardInstance personality) =>
        _state.EventTargets.TryGetValue(arrival.Id, out var targetId) && targetId == personality.Id && arrival.OwnerId != personality.OwnerId;

    // The Personalities of the attacking player the assignment offer leaves out (QueryActions): those Delayed Arrival bars.
    private bool DelayedArrivalBarsAttacker(CardInstance personality, bool attacking) =>
        attacking && EventsInPlay(DelayedArrivalEffect).Any(arrival => DelayedArrivalBars(arrival, personality));

    // ---- Successful Bounty ------------------------------------------------------------------------------------------------------------------------------------------

    // "After each time a Personality opposing a Magistrate or Courtier becomes dishonorable from an action, the Personality's controller loses 2 Honor." Opposing: at the same battlefield, on the
    // other side (core rules Glossary).
    private void AfterPersonalityDishonored(CardInstance personality, int? byPlayerId)
    {
        if (Definition(personality).Type != CardType.Personality || personality.Zone != Zone.Battlefield) return;
        var fromAction = byPlayerId is not null || _runningCommand?.AbilityId is not null || _state.PendingDuel is not null || _state.PendingAttack is not null;
        if (!fromAction) return;
        foreach (var bounty in EventsInPlay(SuccessfulBountyEffect).ToArray())
            if (_state.Cards.Values.Any(c => c.OwnerId != personality.OwnerId && c.Zone == Zone.Battlefield && c.ProvinceIndex == personality.ProvinceIndex && Definition(c).Type == CardType.Personality
                && (Keyword(c, "Magistrate") || Keyword(c, "Courtier"))))
                GainHonor(personality.OwnerId, -2, Definition(bounty).Name, bounty);
    }

    // ---- Glory of the Shogun ----------------------------------------------------------------------------------------------------------------------------------------

    // "Equipping is a Battle/Open action (for all players)": the Event grants every player the timing for every equip (TimingGrants' shape). Empty while none is in play.
    private IEnumerable<(CardInstance Source, TimingGrant Grant)> GloryOfTheShogunGrants() =>
        EventsInPlay(GloryOfTheShogunEffect).Select(c => (c, new TimingGrant(ActionClass.EquipAny, "BattleOrOpen")));

    // "After the first time each turn each player attaches a card with a base Gold Cost of 5 or more from his hand, that player may draw a card." Called wherever a card leaves a hand to attach.
    private void NoteAttachedFromHandForGlory(int playerId, CardInstance attachment)
    {
        NoteAttached(playerId, attachment, "hand"); // Issue #501 (EmperorMechD.cs): every attach from a hand is in the attach log.
        if (Definition(attachment).GoldCost < 5) return;
        foreach (var glory in EventsInPlay(GloryOfTheShogunEffect))
        {
            var key = GloryDrawTurnKey + playerId;
            if (glory.AbilityUses.TryGetValue(key, out var turn) && turn == _state.TurnNumber) continue;
            glory.AbilityUses[key] = _state.TurnNumber;
            QueueOptionalDraw(playerId, glory, $"{Definition(glory).Name}: {Player(playerId).Name} may draw a card.");
        }
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredEventChoice(string type, GameCommand command)
    {
        if (type != "eventChoice") return false;
        ResolveEventChoiceCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingFEventsHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredEventChoice", 3800, static (engine, type, command) => engine.HandleRegisteredEventChoice(type, command));
    }
}
