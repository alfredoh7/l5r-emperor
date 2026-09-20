namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Returns true when an entering-play window opened (the caller returns). A Reaction printed "After X enters play" creates inside the window it was played in.
    private bool ExecuteCreate(AbilityDefinition ability, CreatedCardSpec spec, PlayerState player, CardInstance? target)
    {
        var scan = CreatedCards.Resolve(spec);
        var insideEntryWindow = ability.Timing == "Reaction" && ability.Trigger == "own-enters-play";
        if (scan.Type == CardType.Personality) return CreatePersonality(player.Id, scan, insideEntryWindow);
        if (!CreatedCards.IsAttachment(scan.Type)) throw new InvalidOperationException($"{scan.Name} ({scan.Id}) is a {scan.Type}: creating one is not built.");
        return CreateAttachment(player.Id, scan, target ?? throw new RuleViolation("Choose a Personality to attach it to."), insideEntryWindow);
    }

    // The created attachment must be one the chosen Personality could be equipped with; the card does not exist yet, so a stand-in with its definition is judged.
    private void ValidateCreate(GameCommand command, CreatedCardSpec spec, PlayerState player)
    {
        var scan = CreatedCards.Resolve(spec);
        if (!CreatedCards.IsAttachment(scan.Type)) return;
        var host = command.TargetId is null ? throw new RuleViolation("Choose a Personality to attach it to.") : Card(command.TargetId);
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

    private bool CreateAttachment(int ownerId, CardDefinition definition, CardInstance host, bool duringEntryReaction)
    {
        var card = MintAttachedCard(host, definition);
        if (duringEntryReaction) { ApplyMandatoryEntryEffects(ownerId, card); return false; }
        return OpenEntryReactionWindow(ownerId, card, 1 - ownerId);
    }

    // A created card has no pile to return to: whatever way it leaves play (a destroyed host, a return to hand, a shuffle), it is removed from the game by the end of the command.
    private void RemoveCreatedCardsLeftInPiles()
    {
        foreach (var card in _state.Cards.Values.Where(c => Definition(c).CreatedOnly && c.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead or Zone.Hand or Zone.FateDeck or Zone.DynastyDeck).ToArray())
        {
            foreach (var player in _state.Players) { player.Hand.Remove(card.Id); player.FateDeck.Remove(card.Id); player.DynastyDeck.Remove(card.Id); }
            RemoveFromGame(card);
        }
    }
}
