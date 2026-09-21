namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Returns true when an entering-play window opened (the caller returns). A Reaction printed "After X enters play" creates inside the window it was played in.
    private bool ExecuteCreate(AbilityDefinition ability, CreatedCardSpec creates, GameCommand command, PlayerState player)
    {
        var spec = CreatedCards.Chosen(creates, command.CreatedCard) ?? throw new RuleViolation("Choose which card to create.");
        var insideEntryWindow = ability.Timing == "Reaction" && ability.Trigger == "own-enters-play";
        if (spec.CopyOfTarget)
        {
            var copied = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var target) ? target : throw new RuleViolation("Choose the card to copy.");
            return CreatePersonality(player.Id, Definition(copied), insideEntryWindow, null, out _, createdCopy: true);
        }
        var scan = CreatedCards.Resolve(spec);
        if (scan.Type == CardType.Personality)
        {
            var created = CreatePersonality(player.Id, scan, insideEntryWindow, spec.AtBattlefield ? _state.CurrentBattleProvince : null, out var personality);
            if (spec.UntilBattleEnds) _state.CreatedUntilBattleEndsIds.Add(personality.Id);
            return created;
        }
        if (!CreatedCards.IsAttachment(scan.Type)) throw new InvalidOperationException($"{scan.Name} ({scan.Id}) is a {scan.Type}: creating one is not built.");
        return CreateAttachments(player.Id, scan, spec, CreateHosts(command), insideEntryWindow);
    }

    // The Personalities a created attachment goes to: the ones the command names (CardIds), or its single target.
    private CardInstance[] CreateHosts(GameCommand command) => ArmyPersonalityChoice(command).Select(Card).ToArray();

    // The created attachment must be one the chosen Personality could be equipped with; the card does not exist yet, so a stand-in with its definition is judged.
    private void ValidateCreate(GameCommand command, CreatedCardSpec creates, PlayerState player)
    {
        var spec = CreatedCards.Chosen(creates, command.CreatedCard) ?? throw new RuleViolation(creates.Or is { Length: > 0 } ? "Choose which card to create." : "That is not the card this action creates.");
        if (spec.CopyOfTarget)
        {
            Require(command.TargetId is not null && Definition(Card(command.TargetId)).Type == spec.Type && InPlay(Card(command.TargetId)), $"Choose a {spec.Type} in play to copy.");
            return;
        }
        var scan = CreatedCards.Resolve(spec);
        if (spec.AtBattlefield) Require(_state.CurrentBattleProvince is not null, "There is no current battlefield to create it at.");
        if (!CreatedCards.IsAttachment(scan.Type)) return;
        var hosts = CreateHosts(command);
        Require(hosts.Length > 0, "Choose a Personality to attach it to.");
        foreach (var host in hosts)
            RequireAttachable(new CardInstance { Id = "created-preview", DefinitionId = scan.Id, OwnerId = player.Id, Zone = Zone.Hand, FaceUp = true }, host, player.Id);
    }

    private CardInstance MintAttachedCard(CardInstance host, CardDefinition definition)
    {
        var card = new CardInstance { Id = $"created-{++_state.CreatedCardSequence:D4}", DefinitionId = definition.Id, OwnerId = host.OwnerId, Zone = Zone.Attached, AttachedTo = host.Id, ProvinceIndex = host.ProvinceIndex, FaceUp = true };
        _state.Cards.Add(card.Id, card);
        if (definition.AttachGrantsKeyword is string granted) GrantKeyword(host, granted, card.Id, "Attachment");
        Emit("cardCreated", $"{Player(host.OwnerId).Name} created {definition.Name} and attached it to {Definition(host).Name}.", host.OwnerId, card.Id);
        return card;
    }

    // Every created attachment enters play; the entering-play Reaction window (an Equip's own) opens for the last, the others get the mandatory half only.
    private bool CreateAttachments(int ownerId, CardDefinition definition, CreatedCardSpec spec, CardInstance[] hosts, bool duringEntryReaction)
    {
        var placements = spec.PerHost ? hosts.SelectMany(h => Enumerable.Repeat(h, spec.Count)).ToArray() : Enumerable.Range(0, spec.Count).Select(i => hosts[i % hosts.Length]).ToArray();
        var cards = placements.Select(host => MintAttachedCard(host, definition)).ToArray();
        if (spec.UntilBattleEnds) foreach (var card in cards) _state.CreatedUntilBattleEndsIds.Add(card.Id);
        foreach (var card in cards[..^1]) ApplyMandatoryEntryEffects(ownerId, card);
        if (duringEntryReaction) { ApplyMandatoryEntryEffects(ownerId, cards[^1]); return false; }
        return OpenEntryReactionWindow(ownerId, cards[^1], 1 - ownerId);
    }

    // A created card: a CreatedOnly scan, or a created copy of another card (CardInstance.CreatedCopy). It has no physical card, so it is removed from the game rather than discarded.
    private static bool IsCreatedCard(CardInstance card) => card.CreatedCopy || Definition(card).CreatedOnly;

    // A created card has no pile to return to: whatever way it leaves play (a destroyed host, a return to hand, a shuffle), it is removed from the game by the end of the command.
    private void RemoveCreatedCardsLeftInPiles()
    {
        foreach (var card in _state.Cards.Values.Where(c => IsCreatedCard(c) && c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead or Zone.Hand or Zone.FateDeck or Zone.DynastyDeck).ToArray())
        {
            foreach (var player in _state.Players) { player.Hand.Remove(card.Id); player.FateDeck.Remove(card.Id); player.DynastyDeck.Remove(card.Id); }
            RemoveFromGame(card);
        }
    }
}
