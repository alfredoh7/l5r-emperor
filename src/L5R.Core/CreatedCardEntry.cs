namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Returns true when an entering-play window opened (the caller returns). A Reaction printed "After X enters play" creates inside the window it was played in.
    // beforeWindow is the rest of the action that follows the creation ("Lose 1 Honor"): it runs once the cards exist and before any window over their entry opens.
    private bool ExecuteCreate(AbilityDefinition ability, CreatedCardSpec creates, GameCommand command, PlayerState player, Action? beforeWindow = null)
    {
        var spec = CreatedCards.Chosen(creates, command.CreatedCard) ?? throw new RuleViolation("Choose which card to create.");
        var insideEntryWindow = ability.Timing == "Reaction" && ability.Trigger is "own-enters-play" or "own-personality-enters-play" or EmperorRulingR3Abilities.EntersFromProvinceTrigger; // Shinjo Ki-Chang - exp (issue #492, proxy group Y) creates inside the window another Personality's entry opened.
        if (spec.CopyOfTarget)
        {
            var copied = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var target) ? target : throw new RuleViolation("Choose the card to copy.");
            // Issue #501 item 32: the copy is marked before any entry window opens, so every view shows the duplicate frame and the original's scan.
            return CreatePersonality(player.Id, Definition(copied), insideEntryWindow, null, out _, copied.Printed, beforeWindow, createdCopy: true, original: copied);
        }
        var scan = CreatedCards.Resolve(spec);
        var printed = DerivePrinted(scan, spec, command, player);
        // Emperor Edition proxy pass (issue #492): the count is read from the target before it was destroyed as a cost (Suiteiru no Oni); a count of 0 creates nothing.
        if (spec.CountFromTargetChi) { spec = spec with { Count = _createdCountFromTargetChi }; if (spec.Count < 1) { beforeWindow?.Invoke(); return false; } }
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
        if (scan.Type == CardType.Holding) return CreateInPlay(player.Id, scan, Zone.Home, null, spec.EntersBowed, " at home", insideEntryWindow, beforeWindow);
        if (CreatedCards.IsTerrain(scan)) return CreateInPlay(player.Id, scan, Zone.Battlefield, _state.CurrentBattleProvince ?? throw new RuleViolation(NoBattlefieldForTerrain), false, " at the battlefield", insideEntryWindow, beforeWindow);
        if (!CreatedCards.IsAttachment(scan.Type)) throw new InvalidOperationException($"{scan.Name} ({scan.Id}) is a {scan.Type}: creating one is not built.");
        var attachHosts = CreateHosts(command, spec);
        if (attachHosts.Length == 0 && spec.HostOptional) { beforeWindow?.Invoke(); return false; }
        if (spec.HostOptional)
        {
            attachHosts = attachHosts.Where(host =>
            {
                try { RequireAttachable(new CardInstance { Id = "created-preview", DefinitionId = scan.Id, OwnerId = player.Id, Zone = Zone.Hand, FaceUp = true }, host, player.Id); return true; }
                catch (RuleViolation) { return false; }
            }).ToArray();
            if (attachHosts.Length == 0) { beforeWindow?.Invoke(); return false; }
        }
        return CreateAttachments(player.Id, scan, printed, spec, attachHosts, insideEntryWindow, beforeWindow);
    }

    private const string GreatDeathEntryEffect = "great-death-entry";
    private static readonly CreatedCardSpec BirdsOfPrey = new(CardType.Follower, Force: 1, Keywords: ["Shadowlands", "Nonhuman", "Raptor", "Cavalry", "Undead"], ProxyId: "oracle-l5r-9712", Count: 3);

    // Three Birds of Prey, each attached to the Personality that entered play. Made inside the entry, so none opens an entry window of its own (CreateAttachments' duringEntryReaction).
    private void CreateGreatDeathFollowers(int playerId, CardInstance personality) =>
        CreateAttachments(playerId, CreatedCards.Resolve(BirdsOfPrey), null, BirdsOfPrey, [personality], true, null);

    // The Personalities a created attachment goes to: the performer ("attach them to him"), the ones the command names (CardIds), or its single target.
    // The source itself ("attach it to him" on the creator's own entering-play Reaction: Masayoshi, issue #453), or -- for a multi-target creator -- every card of its FIRST
    // card slot ("one or two of your Personalities": Bayushi Makubesu; Kakita Yasunori's host slot is his first too), so a later slot (Yasunori's removed card) is never a host.
    private CardInstance[] CreateHosts(GameCommand command, CreatedCardSpec spec) =>
        spec.HostSlot is not null ? SlotCards(command, spec.HostSlot).ToArray()
        : spec.HostIsEntered ? (_state.PendingChoice?.CardId is { } enteredId && _state.Cards.TryGetValue(enteredId, out var entered) ? [entered] : [])
        : spec.HostIsSource ? (command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var creator) ? [creator] : [])
        : spec.HostIsWielder ? (command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var equipped) && equipped.AttachedTo is { } wielderId && _state.Cards.TryGetValue(wielderId, out var wielder) ? [wielder] : [])
        : spec.HostIsPerformer ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? [performer] : [])
        : command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var creatorAbility) && creatorAbility.Slots?.FirstOrDefault(s => !s.IsPlayer && !s.Performing && !s.AtResolution) is { } hostSlot
            ? SlotCards(command, hostSlot.Id).ToArray()
        : ArmyPersonalityChoice(command).Select(Card).ToArray();

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
        if (CreatedCards.IsTerrain(scan))
        {
            Require(_state.CurrentBattleProvince is not null, NoBattlefieldForTerrain);
            // The Golden Plains (issue #486): its Reaction still gives Reconnaissance where a Terrain already stands; only the creation has no room then.
            Require(command.AbilityId == "golden-plains-recon" || !_state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == _state.CurrentBattleProvince && Keyword(c, "Terrain")), "This battlefield already has a Terrain in play.");
        }
        if (!CreatedCards.IsAttachment(scan.Type)) return;
        var hosts = CreateHosts(command, spec);
        if (hosts.Length == 0 && spec.HostOptional) return;
        Require(hosts.Length > 0, spec.HostIsPerformer ? "Choose the performing Personality." : "Choose a Personality to attach it to.");
        if (spec.HostOptional) return;
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
        var hasClanAlignment = spec.ControllerClan || scan.Type == CardType.Personality &&
            scan.Text.Contains("your Clan alignment", StringComparison.OrdinalIgnoreCase);
        if (!hasClanAlignment && !spec.HasInstanceValues) return null;
        var printed = scan;
        if (hasClanAlignment) printed = WithControllerClan(printed, player);
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

    private CardDefinition WithControllerClan(CardDefinition printed, PlayerState player)
    {
        var stronghold = Definition(Card(player.StrongholdId));
        var clans = (stronghold.Clans ?? [stronghold.Clan]).Where(clan => clan != "Unaligned").ToArray();
        return clans.Length == 0 ? printed : printed with
        {
            Clan = clans[0],
            Clans = clans.Length > 1 ? clans : null,
            Keywords = (printed.Keywords ?? []).Concat(clans.Select(clan => $"{clan} Clan")).Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private const string NoBattlefieldForTerrain = "A Terrain is put into play at the current battlefield, and there is none.";

    // A created Holding (at home) or Terrain (at the current battlefield): a card of its own that enters play like any other, with the entering-play window over it.
    private bool CreateInPlay(int ownerId, CardDefinition scan, Zone zone, int? province, bool bowed, string where, bool duringEntryReaction, Action? beforeWindow)
    {
        var card = new CardInstance { Id = $"created-{++_state.CreatedCardSequence:D4}", DefinitionId = scan.Id, OwnerId = ownerId, Zone = zone, ProvinceIndex = province, FaceUp = true, Bowed = bowed };
        _state.Cards.Add(card.Id, card);
        Emit("cardCreated", $"{Player(ownerId).Name} created {scan.Name}{(bowed ? " (bowed)" : "")}{where}.", ownerId, card.Id);
        beforeWindow?.Invoke();
        if (duringEntryReaction) { ApplyMandatoryEntryEffects(ownerId, card); return false; }
        return OpenEntryReactionWindow(ownerId, card, 1 - ownerId);
    }

    private CardInstance MintAttachedCard(CardInstance host, CardDefinition definition, CardDefinition? printed)
    {
        var card = new CardInstance { Id = $"created-{++_state.CreatedCardSequence:D4}", DefinitionId = definition.Id, Printed = printed, OwnerId = host.OwnerId, Zone = Zone.Attached, AttachedTo = host.Id, ProvinceIndex = host.ProvinceIndex, FaceUp = true };
        _state.Cards.Add(card.Id, card);
        if (definition.AttachGrantsKeyword is string granted) GrantKeyword(host, granted, card.Id, "Attachment");
        NoteAttached(host.OwnerId, card, "created"); // Issue #501 (EmperorMechD.cs): the attach log.
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
