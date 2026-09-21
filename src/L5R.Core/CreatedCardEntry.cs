namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Returns true when an entering-play window opened (the caller returns). A Reaction printed "After X enters play" creates inside the window it was played in.
    // beforeWindow is the rest of the action that follows the creation ("Lose 1 Honor"): it runs once the cards exist and before any window over their entry opens.
    private bool ExecuteCreate(AbilityDefinition ability, CreatedCardSpec creates, GameCommand command, PlayerState player, Action? beforeWindow = null)
    {
        var spec = CreatedCards.Chosen(creates, command.CreatedCard) ?? throw new RuleViolation("Choose which card to create.");
        var scan = CreatedCards.Resolve(spec);
        var insideEntryWindow = ability.Timing == "Reaction" && ability.Trigger == "own-enters-play";
        var printed = DerivePrinted(scan, spec, command, player);
        if (scan.Type == CardType.Personality)
        {
            var opened = false;
            for (var i = 0; i < spec.Count; i++)
            {
                var last = i == spec.Count - 1;
                opened = CreatePersonality(player.Id, scan, insideEntryWindow || !last, spec.AtBattlefield ? _state.CurrentBattleProvince : null, out var personality, printed, last ? beforeWindow : null);
                if (spec.UntilBattleEnds) _state.CreatedUntilBattleEndsIds.Add(personality.Id);
            }
            return opened;
        }
        if (!CreatedCards.IsAttachment(scan.Type)) throw new InvalidOperationException($"{scan.Name} ({scan.Id}) is a {scan.Type}: creating one is not built.");
        return CreateAttachments(player.Id, scan, printed, spec, CreateHosts(command, spec), insideEntryWindow, beforeWindow);
    }

    // The Personalities a created attachment goes to: the performer ("attach them to him"), the ones the command names (CardIds), or its single target.
    private CardInstance[] CreateHosts(GameCommand command, CreatedCardSpec spec) =>
        spec.HostIsPerformer ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? [performer] : []) : ArmyPersonalityChoice(command).Select(Card).ToArray();

    // The created attachment must be one the chosen Personality could be equipped with; the card does not exist yet, so a stand-in with its definition is judged.
    private void ValidateCreate(GameCommand command, CreatedCardSpec creates, PlayerState player)
    {
        var spec = CreatedCards.Chosen(creates, command.CreatedCard) ?? throw new RuleViolation(creates.Or is { Length: > 0 } ? "Choose which card to create." : "That is not the card this action creates.");
        var scan = CreatedCards.Resolve(spec);
        if (spec.AtBattlefield) Require(_state.CurrentBattleProvince is not null, "There is no current battlefield to create it at.");
        if (!CreatedCards.IsAttachment(scan.Type)) return;
        var hosts = CreateHosts(command, spec);
        Require(hosts.Length > 0, spec.HostIsPerformer ? "Choose the performing Personality." : "Choose a Personality to attach it to.");
        foreach (var host in hosts)
            RequireAttachable(new CardInstance { Id = "created-preview", DefinitionId = scan.Id, OwnerId = player.Id, Zone = Zone.Hand, FaceUp = true }, host, player.Id);
    }

    private CardInstance? ValueSubject(CreatedSubject from, GameCommand command)
    {
        var id = from switch
        {
            CreatedSubject.Performer => command.PerformerId,
            CreatedSubject.Source => command.CardId,
            CreatedSubject.Wielder => command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var source) ? source.AttachedTo : null,
            _ => command.TargetId
        };
        return id is not null && _state.Cards.TryGetValue(id, out var subject) ? subject : null;
    }

    // What one created card prints when that is not what the catalogue prints for its scan: its creator's Stronghold Clan, and values read from other cards. Null when the spec sets none.
    private CardDefinition? DerivePrinted(CardDefinition scan, CreatedCardSpec spec, GameCommand command, PlayerState player)
    {
        if (!spec.HasInstanceValues) return null;
        var printed = scan;
        if (spec.ControllerClan)
        {
            var stronghold = Definition(Card(player.StrongholdId));
            var clans = (stronghold.Clans ?? [stronghold.Clan]).Where(clan => clan != "Unaligned").ToArray();
            if (clans.Length > 0)
                printed = printed with { Clan = clans[0], Clans = clans.Length > 1 ? clans : null, Keywords = (printed.Keywords ?? []).Concat(clans.Select(clan => $"{clan} Clan")).Distinct(StringComparer.Ordinal).ToArray() };
        }
        foreach (var value in spec.Values ?? [])
        {
            var subject = ValueSubject(value.From, command) ?? throw new RuleViolation($"There is no {value.From.ToString().ToLowerInvariant()} to take the {value.Stat} from.");
            var definition = Definition(subject);
            if (value.Stat == CreatedStat.Name) { printed = printed with { Name = definition.Name }; continue; }
            var read = value.Read ?? value.Stat;
            var amount = Math.Max(0, (read switch { CreatedStat.GoldCost => definition.GoldCost, CreatedStat.Force => definition.Force, CreatedStat.Chi => definition.Chi, _ => definition.PersonalHonor })
                + value.Plus + (value.Keyword is not null && Keyword(subject, value.Keyword) ? value.PlusWithKeyword : 0));
            printed = value.Stat switch
            {
                CreatedStat.GoldCost => printed with { GoldCost = amount },
                CreatedStat.Force => printed with { Force = amount },
                CreatedStat.Chi => printed with { Chi = amount },
                _ => printed with { PersonalHonor = amount }
            };
        }
        return printed;
    }

    private CardInstance MintAttachedCard(CardInstance host, CardDefinition definition, CardDefinition? printed)
    {
        var card = new CardInstance { Id = $"created-{++_state.CreatedCardSequence:D4}", DefinitionId = definition.Id, Printed = printed, OwnerId = host.OwnerId, Zone = Zone.Attached, AttachedTo = host.Id, ProvinceIndex = host.ProvinceIndex, FaceUp = true };
        _state.Cards.Add(card.Id, card);
        if (definition.AttachGrantsKeyword is string granted) GrantKeyword(host, granted, card.Id, "Attachment");
        Emit("cardCreated", $"{Player(host.OwnerId).Name} created {(printed ?? definition).Name} and attached it to {Definition(host).Name}.", host.OwnerId, card.Id);
        return card;
    }

    // Every created attachment enters play; the entering-play Reaction window (an Equip's own) opens for the last, the others get the mandatory half only.
    private bool CreateAttachments(int ownerId, CardDefinition definition, CardDefinition? printed, CreatedCardSpec spec, CardInstance[] hosts, bool duringEntryReaction, Action? beforeWindow)
    {
        var placements = spec.PerHost ? hosts.SelectMany(h => Enumerable.Repeat(h, spec.Count)).ToArray() : Enumerable.Range(0, spec.Count).Select(i => hosts[i % hosts.Length]).ToArray();
        var cards = placements.Select(host => MintAttachedCard(host, definition, printed)).ToArray();
        if (spec.UntilBattleEnds) foreach (var card in cards) _state.CreatedUntilBattleEndsIds.Add(card.Id);
        beforeWindow?.Invoke();
        foreach (var card in cards[..^1]) ApplyMandatoryEntryEffects(ownerId, card);
        if (duringEntryReaction) { ApplyMandatoryEntryEffects(ownerId, cards[^1]); return false; }
        return OpenEntryReactionWindow(ownerId, cards[^1], 1 - ownerId);
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
