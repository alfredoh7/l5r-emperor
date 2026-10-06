namespace L5R.Core;

// ======================================================================================================================================================================================
// THE TOPAZ ARMOR - EXP (oracle-l5r-9980): "Attaches to a Topaz Champion ignoring costs. After this card enters play: Choose two keywords; Courtier, Samurai, Archer, Artisan, Cavalry, Duelist,
// Elite, Historian, Kensai, Magistrate, Stalwart. This Personality has the two keywords last chosen from the previous trait."
//
// Mendon: "The Topaz Armor should create a list of keywords and apply them like the 'Spirit of...' cards. When it leaves play (discarded, returned to hand or deck) it loses the selected keywords."
//   * The choice is a TRAIT ("After this card enters play", no designator), so it is mandatory: it is queued by the entry (ApplyMandatoryEntryEffects) and asked as soon as the entry windows have
//     closed, the way the optional draws and the entry searches are (PendingEventChoices): the Armor's controller picks the first keyword of the eleven, then the second of the ten that remain
//     ("two keywords"). Both decisions are the shared Event decision ("eventChoice", one engine-listed answer per keyword, which every client and the bot already answer).
//   * The two keywords are granted to the wielder as the Spirit cards grant theirs (GrantKeyword) with the duration "Attachment": they end when the Armor leaves play for any reason (discarded,
//     destroyed, returned to the hand or a deck, removed from the game, its wielder gone) and follow it when it is transferred (AttachmentTransfers). A keyword the wielder already has is not
//     granted twice, so what he printed is never lost with the Armor. The pair last chosen is remembered (MatchState.SearchPlayTopazKeywords); entering play again chooses a fresh pair.
//   * "Attaches to a Topaz Champion ignoring costs" is AttachRequiresKeyword "Topaz Champion" and a zero Gold price (SearchPlayAttachesFree, EquipGoldCost's term).
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    internal const string TopazKeywordsKind = "sp-topaz-keywords";
    internal static readonly string[] TopazKeywordList = ["Courtier", "Samurai", "Archer", "Artisan", "Cavalry", "Duelist", "Elite", "Historian", "Kensai", "Magistrate", "Stalwart"];

    // ApplyMandatoryEntryEffects' hook: "After this card enters play: Choose two keywords".
    private void ApplySearchPlayEntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != SearchPlayAbilities.TopazEffect) return;
        _state.SearchPlayTopazKeywords[card.Id] = []; // entering play again chooses a fresh pair
        _state.PendingEventChoices.Add(new PendingEventChoice
        {
            Kind = TopazKeywordsKind,
            EventCardId = card.Id,
            PlayerId = playerId,
            Candidates = [.. TopazKeywordList],
            Step = 0,
            Prompt = $"{Definition(card).Name}: choose the first of two keywords for this Personality ({string.Join(", ", TopazKeywordList)})."
        });
        Emit("entryChoiceQueued", $"{Definition(card).Name}: {Player(playerId).Name} will choose two keywords once it has entered play.", playerId, card.Id);
    }

    // ResolveEventChoiceCommand's default branch: the answer. False when it is not this trait's decision.
    private bool SearchPlayEventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind != TopazKeywordsKind) return false;
        Require(command.Choice is { } picked && pending.Candidates.Contains(picked), "Choose one of the offered keywords.");
        var armor = Card(pending.EventCardId);
        var chosen = pending.Data.Append(command.Choice!).ToList();
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        Emit("eventChose", $"{Definition(armor).Name}: {Player(pending.PlayerId).Name} chooses {command.Choice}.", pending.PlayerId, armor.Id);
        if (chosen.Count < 2)
        {
            OpenEventChoice(new PendingEventChoice
            {
                Kind = TopazKeywordsKind,
                EventCardId = armor.Id,
                PlayerId = pending.PlayerId,
                Origin = pending.Origin,
                ClosesTo = pending.ClosesTo,
                Step = chosen.Count,
                Data = chosen,
                Candidates = pending.Candidates.Where(k => k != command.Choice).ToList(),
                Prompt = $"{Definition(armor).Name}: choose the second of two keywords for this Personality."
            });
            return true;
        }
        _state.SearchPlayTopazKeywords[armor.Id] = chosen;
        if (armor.Zone == Zone.Attached && armor.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host))
        {
            foreach (var keyword in chosen) GrantKeyword(host, keyword, armor.Id, GrantDuration.Attachment);
            Emit("abilityGranted", $"{Definition(armor).Name}: {Definition(host).Name} has {string.Join(" and ", chosen)} while it is attached.", host.OwnerId, host.Id);
        }
        ResumeAfterDeferredWindow(pending.ClosesTo);
        return true;
    }

    // EventChoiceActions' default branch: one answer per keyword still to choose from.
    private IEnumerable<ActionOption> SearchPlayEventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != TopazKeywordsKind) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        foreach (var keyword in pending.Candidates)
            yield return new("eventChoice", $"{name}: {keyword}", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, Choice: keyword), pending.EventCardId);
    }

    // TransferAttachment's hook: the pair chosen for the Armor follows it to its new wielder.
    private void SearchPlayTransferGrants(CardInstance attachment, CardInstance host)
    {
        if (Definition(attachment).Effect != SearchPlayAbilities.TopazEffect || !_state.SearchPlayTopazKeywords.TryGetValue(attachment.Id, out var chosen)) return;
        foreach (var keyword in chosen) GrantKeyword(host, keyword, attachment.Id, GrantDuration.Attachment);
    }
}
