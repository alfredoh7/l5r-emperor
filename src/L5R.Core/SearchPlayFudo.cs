namespace L5R.Core;

// ======================================================================================================================================================================================
// HEART OF FUDO - EXP2 (oracle-l5r-10523) and HYOBUKO (oracle-l5r-3534).
//
//   Heart of Fudo - exp2: "Limited: If this Personality is unbowed, search your Fate deck for a card and put it into your hand. Bow this Personality. Destroy this Item."  The condition governs the
//   search only (the printed text is three sentences): the Item may be used by a bowed wielder, who then only bows (already) and destroys it. The wielder is read as the action resolves, the bow
//   and the destruction are applied, and the search then opens (the order of the three does not change what any of them does).
//
//   Hyobuko: "Attaches Heart of Fudo ignoring Gold cost." is the attach price of a Heart of Fudo (either version: the title is compared) that goes to Hyobuko himself (Quelsa's "attaches Spells
//   paying 2 less Gold" shape: EquipGoldCost's term). "Limited: Discard Hyobuko: He searches for an ancient relic. Search your discard pile, then Fate deck, for a card titled Heart of Fudo. Show it
//   and put it in your hand." -- discarding him is the cost (a Personality discarded from play: his unit leaves with him, his attachments are discarded, and he goes to the Dynasty discard pile, not
//   the dead one), and the search is an ordered one (R-011): the discard pile first, the Fate deck only when it holds none, a searched deck reshuffled. "Singular" is only a keyword.
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    // ---- Heart of Fudo - exp2 -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool SearchPlayFudoFetch(RunHookContext context)
    {
        var (player, source) = (context.player, context.source!);
        var wielder = WielderOf(source);
        var unbowed = wielder is not null && !wielder.Bowed;
        if (wielder is not null && InPlay(wielder) && !StalwartNegatesBow(wielder, player.Id)) wielder.Bowed = true;
        DestroyAttachment(source);
        if (!unbowed)
        {
            Emit("searchPlayFudoBowed", $"{Definition(source).Name}: the Personality was bowed, so {player.Name} does not search.", player.Id, source.Id);
            return false;
        }
        BeginSearch(SearchPlaySpecs.Fudo, source, player.Id, "ability", context.bowedBeforeAction);
        return true;
    }

    // ---- Hyobuko ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // EquipGoldCost's term: a Heart of Fudo attached to Hyobuko costs nothing in Gold.
    private bool SearchPlayAttachesFree(CardInstance attachment, CardInstance wielder) =>
        Definition(wielder).Effect == SearchPlayAbilities.HyobukoEffect && Experienced.BaseTitle(Definition(attachment).Name) == "Heart of Fudo"
        || Definition(attachment).Effect == SearchPlayAbilities.TopazEffect && Definition(attachment).AttachRequiresKeyword is { } required && KeywordOrHuman(wielder, required);

    // The DiscardFromPlay cost's hook: a Personality discarded from play (Hyobuko's "Discard Hyobuko"). His unit leaves play with him -- the attachments go to their owners' Fate discard piles, as
    // when he leaves the game or the table (RemoveUnitFromGame) -- and he goes to his owner's Dynasty discard pile, face up, with everything he carried reset. Not a destruction: no dead pile, no
    // Reaction to a Personality being destroyed, no Honor loss for a dishonorable death.
    private bool SearchPlayDiscardPersonalityFromPlay(CardInstance personality)
    {
        if (Definition(personality).Type != CardType.Personality || !InPlay(personality)) return false;
        foreach (var attachment in Attachments(personality.Id).ToArray())
        {
            RemoveAttachmentGrants(personality, attachment.Id);
            attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.FaceUp = true; attachment.Bowed = false; attachment.Tokens.Clear();
            Emit("attachmentDiscarded", $"{Definition(attachment).Name} was discarded: {Definition(personality).Name} left play.", attachment.OwnerId, attachment.Id);
        }
        QueueOverconfidentDraw(personality); // "leaves play" for the Overconfident keyword
        personality.Zone = Zone.DynastyDiscard; personality.ProvinceIndex = null; personality.FaceUp = true;
        ResetCardState(personality); // a card that has left play carries nothing of what it had in play (a fresh card when it enters play again)
        _state.TsujikenWatches.RemoveAll(w => w.TsujikenId == personality.Id);
        Emit("discardedFromPlay", $"{Definition(personality).Name} was discarded.", personality.OwnerId, personality.Id);
        return true;
    }
}
