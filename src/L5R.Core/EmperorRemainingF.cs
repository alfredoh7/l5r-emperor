namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group F: the Events, Regions and Holdings wired in event-wave-8 (EmperorRemainingFEvents.cs), region-wave-9 and holding-wave-13. The declarative
// ability data lives here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file, each naming its card. Every card's printed text (the SCAN's,
// which can differ from the Oracle text) and the reading taken are at its entry. The cards left unwired, and why, are in tools/content/ee_remF_blockers.json.
//
// New primitives:
//   * a Gold source that is a Reaction to paying for an action, usable while bowed (Luxurious Silk: Library of Rebirth's shape, PaymentWhen);
//   * a Fate deck look for a named type (Temples of Gisei Toshi) and a search bounded by the performer's Chi (Refuge of the Three Sisters: PendingSearch.FocusBelow);
//   * cost verbs that destroy a Region and remove a Personality in play from the game (Refuge); a Region bowed as a cost straightens (StraightenRegions);
//   * a static province strength bonus (Fortifications), "will not be destroyed" (Rugashi Bazaar), and "lost Honor from another player's action since your last turn ENDED" (Rugashi Bazaar);
//   * Performing target slots of a Battle/Open action, a decider who may decline (Governor's Court), an alternative bow cost;
//   * a targeted-by-action redirect to a Samurai at the same location (Chugo Seido), "before Focus Effects resolve" as a duel reaction window and a per-duel stat bonus (Falling Rain Dojo);
//   * Wealth tokens as a Gold source, removing only the tokens a payment needs, and the Gold paid for a recruit remembered as the card enters play (Moneylender);
//   * "before you lose Honor" as a decision asked before the loss: the command is abandoned, its controller asked, and the command replayed with his answer (Ten Thousand Temples: Execute).
public static class EmperorRemainingFAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingF(this Dictionary<string, AbilityDefinition> all)
    {
        // ---- Holdings (holding-wave-13) ----------------------------------------------------------------------------------------------------------------
        // Traveling Peddler (oracle-l5r-8729): "Bow this card: Produce 2 Gold." is its printed production. "Limited: Bow this card and pay 3 Gold: Draw a card."
        all["peddler-draw"] = new("Limited", "Bow this card and pay 3 Gold: Draw a card", "none", Cost: GoldCostRule.Fixed(3), Costs: ActionCost.BowSource);
        // Temples of Gisei Toshi (oracle-l5r-7898): "Limited: Bow this card: Name \"Follower\", \"Item\", or \"Spell\". Look at the top four cards of your Fate deck. You may show one of those
        // cards that is of the type you named, then put it in your hand." The naming is the choice of ability (three actions sharing one use); the rest of the four stay where they were.
        all["temples-gisei-follower"] = new("Limited", "Bow this card: Name \"Follower\". Look at the top four cards of your Fate deck. You may show one of those cards that is of the type you named, then put it in your hand", "none",
            UsageKey: "temples-gisei-look", Costs: ActionCost.BowSource, Look: EmperorRemainingFLooks.TemplesFollower);
        all["temples-gisei-item"] = new("Limited", "Bow this card: Name \"Item\". Look at the top four cards of your Fate deck. You may show one of those cards that is of the type you named, then put it in your hand", "none",
            UsageKey: "temples-gisei-look", Costs: ActionCost.BowSource, Look: EmperorRemainingFLooks.TemplesItem);
        all["temples-gisei-spell"] = new("Limited", "Bow this card: Name \"Spell\". Look at the top four cards of your Fate deck. You may show one of those cards that is of the type you named, then put it in your hand", "none",
            UsageKey: "temples-gisei-look", Costs: ActionCost.BowSource, Look: EmperorRemainingFLooks.TemplesSpell);
        // Akodo's Grave (oracle-l5r-309): "Battle: Bow and destroy this card and target a Terrain: Destroy it." (the scan's text).
        all["akodo-grave-destroy"] = new("Battle", "Bow and destroy this card and target a Terrain: Destroy it", "terrain-at-battle", Costs: [new(CostVerb.Bow), new(CostVerbs.Sacrifice)]);
        // Luxurious Silk (oracle-l5r-4787): "Reaction: Even if this card is bowed, when paying for an action: Produce 2 Gold." (the scan's text): an alternate Gold source that does not bow its
        // card and is usable while bowed (Library of Rebirth's shape), for a payment for an action (Yasuki Tanimura's reading: Equip, a Strategy play or an ability, never a recruit).
        all["luxurious-silk-gold"] = new("Reaction", "Even if this card is bowed, when paying for an action: Produce 2 Gold", "none", Trigger: "paying-gold-cost",
            PaymentProduction: 2, PaymentBows: false, PaymentWhen: EmperorRemainingFAbilities.PayingForActionWhen, PaymentEvenIfBowed: true);
        // ---- Regions (region-wave-9) -----------------------------------------------------------------------------------------------------------------
        // Blighted Region (oracle-l5r-1042): "Battle: If you are the Defender, bow this card and choose your performing Personality: Move him to the current battlefield. If he moved and is now
        // opposed, straighten him and gain 2 Honor." An action that moves a Personality to a battle is Absent for either player (MovesToBattle); its effect case is inline in RunAbilityEffects
        // (the move-to-battle scan reads that switch). "If you are the Defender" is a condition on taking the action (ValidateEmperorRemainingF).
        all["blighted-region-move"] = new("Battle", "If you are the Defender, bow this card and choose your performing Personality: Move him to the current battlefield. If he moved and is now opposed, straighten him and gain 2 Honor", "none",
            Performer: "own-personality", MovesToBattle: true, Costs: ActionCost.BowSource);
        // Refuge of the Three Sisters (oracle-l5r-6226): "Limited: Destroy this card and remove your performing unbowed Personality from the game: Search your Fate deck for a card with a lower Focus
        // Value than your Personality's Chi. Show it and put it in your hand." Both costs are paid together before the search; the Chi is his, read when the action is taken.
        all["refuge-three-sisters-search"] = new("Limited", "Destroy this card and remove your performing unbowed Personality from the game: Search your Fate deck for a card with a lower Focus Value than your Personality's Chi. Show it and put it in your hand", "none",
            Performer: "own-unbowed-personality", Costs: [new(CostVerbs.DestroyRegion), new(CostVerbs.RemoveUnit, CostSubject.Performer)], Search: EmperorRemainingFSearches.RefugeOfTheThreeSisters);
        // Chugo Seido (oracle-l5r-1373): "Reaction: When another player's action would target one of your Personalities, destroy this card: Choose one of your Samurai at the same location. The action
        // targets him instead, if legal." The Castle of Order's shape (a targeted-by-action Reaction taking one option per candidate), with the Holding destroyed as its cost.
        all["chugo-seido-redirect"] = new("Reaction", "When another player's action would target one of your Personalities, destroy this card: Choose one of your Samurai at the same location. The action targets him instead, if legal", "redirect-target-own-samurai-at-affected-location",
            Trigger: ReactionTriggers.TargetedByAction, Costs: [new(CostVerbs.Sacrifice)]);
        // Falling Rain Dojo (oracle-l5r-2455): "Reaction: Before Focus Effects resolve, if your Duelist has a lower duel stat than the Personality facing him: Give your Duelist +1 to his duel stat
        // until the duel ends." The window is the duel's own (Dueling.cs), between the Duelists' refocus and the ordering of Focus Effects; the bonus is the duel's (PendingDuel.StatBonuses).
        all["falling-rain-boost"] = new("Reaction", "Before Focus Effects resolve, if your Duelist has a lower duel stat than the Personality facing him: Give your Duelist +1 to his duel stat until the duel ends", "none", Trigger: "before-focus-effects");
        // Moneylender (oracle-l5r-5224): "After this card enters play: Put a number of Wealth tokens on it equal to the amount of Gold you paid for it." (a trait: ApplyEmperorRemainingFEntryEffects);
        // "Bow this card and remove one or more Wealth tokens from it: Produce Gold equal to the number of tokens removed." (a Gold source, MoneylenderTokens / SpendMoneylenderTokens);
        // "Limited: Bow this card and pay any amount of Gold: Put a number of Wealth tokens on this card equal to the amount of Gold you paid."
        all["moneylender-invest"] = new("Limited", "Bow this card and pay any amount of Gold: Put a number of Wealth tokens on this card equal to the amount of Gold you paid", "none",
            Cost: GoldCostRule.Variable(), Costs: ActionCost.BowSource);
        // Ten Thousand Temples (oracle-l5r-7908): "Before you lose Honor, destroy this card: Negate the Honor loss." is its Effect (TenThousandTemplesNegates: asked before an Honor loss, the command
        // replayed with the answer). "Limited: If your Family Honor is 9 or lower, bow this card: Gain 1 Honor."
        all["ten-thousand-temples-honor"] = new("Limited", "If your Family Honor is 9 or lower, bow this card: Gain 1 Honor", "none", Costs: ActionCost.BowSource);
        // Rugashi Bazaar (oracle-l5r-6396): "This card will not be destroyed." is its Effect (WillNotBeDestroyed); "Bow this card: Produce 3 Gold." its production. "Revenge Battle/Open: If you have lost
        // Honor from another player's action during the Action Phase since your last turn ended, choose one to four of your performing Personalities: Give each of them a Force bonus equal to his
        // base Personal Honor." The performers are a Performing slot (Dislodge the Foe's shape); as a Battle action each is at the battle, as an Open action anywhere.
        all["rugashi-revenge"] = new("BattleOrOpen", "Revenge Battle/Open: If you have lost Honor from another player's action during the Action Phase since your last turn ended, choose one to four of your performing Personalities: Give each of them a Force bonus equal to his base Personal Honor", "slots",
            ActionKeywords: ["Revenge"], Slots: [new("performers", "own-personality", "One to four of your performing Personalities", 1, 4, Performing: true)]);
        // Governor's Court (oracle-l5r-2878): "Political Open: Even if this card is bowed, choose your performing Courtier and bow him or this card: Choose another player with an unbowed Personality in play.
        // He may choose to target and bow one of his unbowed Personalities. If he did not choose this, gain 2 Honor." (the scan's text.) The cost is one of two bows, chosen with the command; the
        // other player's "may choose" is a decision of his (DeciderChoice.cs), with declining offered.
        all["governors-court-open"] = new("Open", "Political Open: Even if this card is bowed, choose your performing Courtier and bow him or this card: Choose another player with an unbowed Personality in play. He may choose to target and bow one of his unbowed Personalities. If he did not choose this, gain 2 Honor", "player",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Courtier"], ActionKeywords: ["Political"],
            CostOptions: [new("bow-courtier", "Bow the Courtier", CostVerb.Bow, CostSubject.Performer), new("bow-source", "Bow this card", CostVerb.Bow, CostSubject.Source)]);
        return all;
    }

    /// <summary>Luxurious Silk's payment condition: paying for an action (an Equip, a Strategy played from a hand or an ability).</summary>
    public const string PayingForActionWhen = "paying-for-action";
}

/// <summary>The searches of group F (registered in SearchCatalog.All).</summary>
public static class EmperorRemainingFSearches
{
    /// <summary>Refuge of the Three Sisters: any Fate card; the Focus bound is the Personality's Chi (PendingSearch.FocusBelow).</summary>
    public static SearchSpec RefugeOfTheThreeSisters { get; } = new("refuge-three-sisters", "Search your Fate deck for a card with a lower Focus Value than the Personality's Chi",
        [new(SearchDeck.Fate, new("card with a lower Focus Value than the Personality's Chi"), SearchEffect.ToHand)]);
}

/// <summary>The looks of group F (registered in LookCatalog.All).</summary>
public static class EmperorRemainingFLooks
{
    private static LookSpec Temples(string id, string noun, CardType type) =>
        new(id, "Look at the top four cards of your Fate deck", [new("Fate")], 4, [new(LookDisposition.Show, 0, 1, new(noun, [type]))]);
    public static LookSpec TemplesFollower { get; } = Temples("temples-gisei-follower", "Follower", CardType.Follower);
    public static LookSpec TemplesItem { get; } = Temples("temples-gisei-item", "Item", CardType.Item);
    public static LookSpec TemplesSpell { get; } = Temples("temples-gisei-spell", "Spell", CardType.Spell);
    public static LookSpec[] All { get; } = [TemplesFollower, TemplesItem, TemplesSpell];
}

public sealed partial class MatchEngine
{
    // ---- Effect keys --------------------------------------------------------------------------------------------------------------------------------
    internal const string FortificationsEffect = "fortifications-provinces";
    internal const string RugashiBazaarEffect = "will-not-be-destroyed";

    // Chugo Seido: the reactor's own Samurai at the targeted Personality's location, other than it, that the held action accepts.
    private IEnumerable<CardInstance> ChugoSeidoCandidates(CardInstance holding, ReactionSubject subject, CardInstance affected) =>
        _state.Cards.Values.Where(c => c.OwnerId == holding.OwnerId && c.Id != affected.Id && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Samurai")
                && SameLocation(c, affected) && ActionAcceptsTarget(subject, c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- Ten Thousand Temples ---------------------------------------------------------------------------------------------------------------------------------
    internal const string TenThousandTemplesEffect = "ten-thousand-temples";

    private sealed class HonorLossDecisionNeeded(int playerId, string templeId, int amount, string reason) : Exception("An Honor loss awaits a decision.")
    {
        public int PlayerId { get; } = playerId; public string TempleId { get; } = templeId; public int Amount { get; } = amount; public string Reason { get; } = reason;
    }

    // The decisions made so far for this command's Honor losses the Temple could negate, in the order they arise, and how many have arisen in this run.
    private List<bool> _honorLossDecisions = [];
    private int _honorLossOccurrence;

    private CardInstance? TenThousandTemplesOf(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Region && Definition(c).Effect == TenThousandTemplesEffect).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    // "Before you lose Honor, destroy this card: Negate the Honor loss." A loss the player's Ten Thousand Temples could negate is put to him first: with no answer for it yet the command is abandoned and
    // asked about (Execute), and replayed with the answer; a "yes" destroys the Region and the loss does not happen.
    private bool TenThousandTemplesNegates(int playerId, int amount, string reason)
    {
        var temple = TenThousandTemplesOf(playerId);
        if (temple is null) return false;
        var index = _honorLossOccurrence++;
        if (index >= _honorLossDecisions.Count) throw new HonorLossDecisionNeeded(playerId, temple.Id, amount, reason);
        if (!_honorLossDecisions[index]) return false;
        DestroyRegion(temple);
        Emit("honorLossNegated", $"{Player(playerId).Name}'s Honor loss from {reason} was negated: Ten Thousand Temples was destroyed.", playerId, temple.Id);
        return true;
    }

    private IEnumerable<ActionOption> HonorLossDecisionActions(int playerId)
    {
        if (_state.PendingHonorLossDecision is not { } pending || pending.PlayerId != playerId) yield break;
        yield return new("honorLossChoice", $"Destroy Ten Thousand Temples: negate the loss of {-pending.Amount} Honor ({pending.Reason})", true, null,
            new GameCommand("honorLossChoice", playerId, _state.Version, CardId: pending.TempleId, Choice: "yes"), pending.TempleId);
        yield return new("honorLossChoice", $"Lose {-pending.Amount} Honor ({pending.Reason})", true, null,
            new GameCommand("honorLossChoice", playerId, _state.Version, CardId: pending.TempleId, Choice: "no"), pending.TempleId);
    }

    // ---- Moneylender ------------------------------------------------------------------------------------------------------------------------------------
    internal const string MoneylenderEffect = "moneylender", MoneylenderTokenType = "Wealth", EntryGoldPaidKey = "entry-gold-paid";

    private static int MoneylenderTokens(CardInstance card) => card.Tokens.Count(t => t.Type == MoneylenderTokenType);

    // "The amount of Gold you paid for it": recorded on the card as it is recruited (Pay's Recruit payment), read once when it enters play.
    private void RecordGoldPaidForEntry(int cost, PaymentSubject? paying)
    {
        if (paying is { Kind: PaymentKind.Recruit } && Definition(paying.Card).Effect == MoneylenderEffect) paying.Card.AbilityUses[EntryGoldPaidKey] = cost;
    }

    private void ApplyEmperorRemainingFEntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect != MoneylenderEffect) return;
        var paid = card.AbilityUses.Remove(EntryGoldPaidKey, out var amount) ? amount : 0;
        for (var i = 0; i < paid; i++) CreateToken(card, MoneylenderTokenType, "entering play");
    }

    // A payment that includes Moneylenders removes from each only as many Wealth tokens as the cost still needs after the other sources (at least one, at most all it has): the player's
    // "one or more" is never a waste. Returns the Gold the payment counted for tokens it did not remove.
    private int SpendMoneylenderTokens(string[] ids, int cost, int playerId, PaymentSubject? paying)
    {
        var lenders = ids.Select(Card).Where(c => Definition(c).Effect == MoneylenderEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (lenders.Length == 0) return 0;
        var remaining = cost - ids.Where(id => lenders.All(l => l.Id != id)).Sum(id => GoldProduced(id, playerId, paying));
        var unused = 0;
        foreach (var lender in lenders)
        {
            var tokens = lender.Tokens.Where(t => t.Type == MoneylenderTokenType).ToArray();
            var take = Math.Clamp(remaining, 1, tokens.Length);
            for (var i = 0; i < take; i++) DestroyToken(lender, tokens[i]);
            Emit("goldSourceUsed", $"{Definition(lender).Name} removed {take} Wealth token{(take == 1 ? "" : "s")} and produced {take} Gold.", playerId, lender.Id);
            unused += tokens.Length - take;
            remaining -= take;
        }
        return unused;
    }

    // ---- Falling Rain Dojo -------------------------------------------------------------------------------------------------------------------------------
    internal const string BeforeFocusEffectsStage = "FocusEffectsReaction"; // the one stage of the window before the Focus Effects (Bend like a Reed, group A, and Falling Rain Dojo, group F share it)

    // The player's Duelist in the duel in progress when his duel stat is lower than the Personality facing him; null otherwise.
    private CardInstance? FallingRainDuelist(int playerId)
    {
        if (_state.PendingDuel is not { Stage: BeforeFocusEffectsStage } duel) return null;
        var (mineId, theirsId) = playerId == duel.ChallengerPlayerId ? (duel.ChallengerId, duel.ChallengedId) : playerId == duel.ChallengedPlayerId ? (duel.ChallengedId, duel.ChallengerId) : (null, null);
        if (mineId is null || theirsId is null) return null;
        var mine = Card(mineId); var theirs = Card(theirsId);
        return mine.OwnerId == playerId && Keyword(mine, "Duelist") && DuelStatValue(mine, duel.Stat) < DuelStatValue(theirs, duel.Stat) ? mine : null;
    }

    // True when a Reaction in play could be played in the "before Focus Effects resolve" window (the stage must already be set: Falling Rain Dojo reads it).
    private bool InPlayBeforeFocusReactionAvailable() =>
        _state.Cards.Values.Where(c => InPlay(c)).Any(c => (Definition(c).Abilities ?? []).Any(id =>
        {
            if (!AbilityCatalog.All.TryGetValue(id, out var ability) || ability.Timing != "Reaction" || ability.Trigger != "before-focus-effects") return false;
            try { ValidateAbility(new GameCommand("useAbility", c.OwnerId, _state.Version, CardId: c.Id, AbilityId: id)); return true; }
            catch (RuleViolation) { return false; }
        }));

    // Test seam: the Gold a recruit of the card would cost the player now.
    internal int QuotePurchaseForTest(int playerId, string cardId) => QuotePurchase(playerId, cardId).Cost;

    // Test seam: apply a destruction to one card the way an action's effect does.
    internal int DestroyUnitsForTest(string cardId) => DestroyUnits([Card(cardId)], false, _state.ActivePlayerId);

    // "This card will not be destroyed" (Rugashi Bazaar): read where a destruction is applied (DestroyUnits).
    private bool WillNotBeDestroyed(CardInstance card) => InPlay(card) && Definition(card).Effect == RugashiBazaarEffect || CombatSegmentShielded(card); // Chikushudo's Trickery (issue #492, rulings pass R1)

    // "Even if this card is bowed" for group F's abilities (ValidateAbility's list).
    private static bool EmperorRemainingFBowedOk(string id) => id is "governors-court-open";

    // The ability ids whose effects RunEmperorRemainingFEffect runs (RunAbilityEffects' guard case).
    private static bool IsEmperorRemainingFAbility(string id) => id is
        "ten-thousand-temples-honor" or "moneylender-invest" or "falling-rain-boost" or "chugo-seido-redirect" or "rugashi-revenge" or "governors-court-open" or "peddler-draw" or "temples-gisei-follower" or "temples-gisei-item" or "temples-gisei-spell" or "akodo-grave-destroy" or "refuge-three-sisters-search";

    // Per-id conditions of group F's abilities (ValidateAbility's hook).
    private void ValidateEmperorRemainingF(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (id == "ten-thousand-temples-honor") Require(player.Honor <= 9, "Your Family Honor must be 9 or lower.");
        // Cherry Blossom Festival: "No players may take Reactions". Disgrace: "other players may not target your Personalities with Limited actions".
        if (ability.Timing == "Reaction") Require(!BarredByCherryBlossom(id), "Cherry Blossom Festival: no players may take Reactions.");
        RequireDisgraceAllowsTargets(ability, command, player);
        // Blighted Region: "If you are the Defender" (a condition on taking the action at all).
        if (id == "blighted-region-move") Require(player.Id != _state.ActivePlayerId, "Blighted Region: only the Defender may use this.");
        // Falling Rain Dojo: "if your Duelist has a lower duel stat than the Personality facing him".
        if (id == "falling-rain-boost") Require(FallingRainDuelist(player.Id) is not null, "Your Duelist in the duel must have a lower duel stat than the Personality facing him.");
        // Rugashi Bazaar's Revenge: the condition before the colon.
        if (id == "rugashi-revenge") Require(_state.HonorLostToOtherActionSinceTurnEnded[player.Id], "You have not lost Honor from another player's action during the Action Phase since your last turn ended.");
        // Governor's Court: "another player with an unbowed Personality in play".
        if (id == "governors-court-open")
            Require(command.TargetPlayerId is int chosen && chosen != player.Id && _state.Cards.Values.Any(c => c.OwnerId == chosen && InPlay(c) && Definition(c).Type == CardType.Personality && !c.Bowed),
                "Choose another player with an unbowed Personality in play.");
    }

    // The Chi of Refuge of the Three Sisters' performing Personality, read before he is removed from the game as a cost.
    private int _capturedPerformerChi;
    private void CaptureBeforeCostsRemainingF(string id, GameCommand command)
    {
        if (id == "refuge-three-sisters-search" && command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)) _capturedPerformerChi = PersonalityChi(performer);
    }

    // "Remove [a Personality in play] from the game": his attachments cannot stay attached, so they are discarded (a token leaves with its card).
    private void RemoveUnitFromGame(CardInstance card)
    {
        foreach (var attachment in Attachments(card.Id).ToArray())
        {
            RemoveAttachmentGrants(card, attachment.Id);
            attachment.Zone = Zone.FateDiscard; attachment.AttachedTo = null; attachment.ProvinceIndex = null; attachment.Tokens.Clear();
            Emit("attachmentDiscarded", $"{Definition(attachment).Name} was discarded: {Definition(card).Name} left the game.", attachment.OwnerId, attachment.Id);
        }
        RemoveFromGame(card);
    }

    // Straighten Phase: a Region belongs to its province's owner and straightens with the rest of his cards.
    private void StraightenRegions(PlayerState player)
    {
        foreach (var province in player.Provinces.ToArray())
            if (province.RegionId is { } regionId && _state.Cards.TryGetValue(regionId, out var region) && region.Zone == Zone.Region && region.Bowed) Straighten(region);
    }

    // True when the action paused (a choice, a look) and finishes from there.
    private bool RunEmperorRemainingFEffect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case "ten-thousand-temples-honor":
                GainHonor(player.Id, 1, "Ten Thousand Temples", source);
                return false;
            case "moneylender-invest":
                for (var i = 0; i < PaidGoldAmount(command); i++) CreateToken(source!, MoneylenderTokenType, id);
                return false;
            case "falling-rain-boost":
            {
                var duelist = FallingRainDuelist(player.Id)!;
                _state.PendingDuel!.StatBonuses[duelist.Id] = _state.PendingDuel.StatBonuses.GetValueOrDefault(duelist.Id) + 1;
                Emit("duelStatBonus", $"{Definition(source!).Name}: {Definition(duelist).Name} has +1 to his duel stat until the duel ends.", player.Id, duelist.Id);
                return false;
            }
            case "chugo-seido-redirect":
                RedirectAction(HeldAction(), target!.Id, source!);
                return false;
            case "rugashi-revenge":
                foreach (var performer in SlotCards(command, "performers").ToArray())
                    if (Definition(performer).PersonalHonor is > 0 and var bonus) ApplyModifier(performer, source!.Id, id, "Force", bonus);
                return false;
            case "governors-court-open":
                return BeginDeciderChoice(DeciderSpecs.GovernorsCourt, command, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case "peddler-draw":
                Draw(player);
                return false;
            case "temples-gisei-follower": case "temples-gisei-item": case "temples-gisei-spell":
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBefore);
                return true;
            case "refuge-three-sisters-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBefore);
                _state.PendingSearch!.FocusBelow = _capturedPerformerChi;
                return true;
            case "akodo-grave-destroy":
                DiscardTerrain(target!);
                player.TerrainDestroyedThisBattle = true;
                return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition group F ability '{id}'.");
        }
    }

    // ---- Fortifications (holding-wave-13): "Your provinces have +1 strength." (StaticProvinceBonus's arm; a static trait read live, so gone when the Holding is.) ----------------------

    // ---- Luxurious Silk: paying for an action ---------------------------------------------------------------------------------------------------------------------
    private bool EmperorRemainingFPaymentConditionMet(string when, PaymentSubject paying) => when switch
    {
        EmperorRemainingFAbilities.PayingForActionWhen => PaymentIsForAnAction(paying),
        _ => MechEPaymentConditionMet(when, paying) // Seaside Bazaar (issue #501, EmperorMechE.cs): any Gold Cost
    };

    // "When paying for an action" (Luxurious Silk, Yasuki Tanimura). Mendon's ruling (issue #492, R4): "This does not reduce the cost of a personality recruited during the dynasty phase or if
    // the recruit action requires a player to pay all costs normally. However, if a personality's gold cost is the cost of an action and the effect is to recruit them, then Luxurious Silk may be
    // used to reduce its cost." So an Equip, a Strategy played from a hand and an ability's own cost are payments for an action; an ordinary recruit (a Dynasty purchase, a search that recruits, any
    // recruit that carries no ability) is not; and a recruit BY AN ABILITY whose Gold cost is the Personality's Gold Cost is one -- unless the ability's printed text says he is brought into play
    // "paying all costs" (the recruit's own costs, paid as for any recruit), which it is not.
    internal static bool PaymentIsForAnAction(PaymentSubject? paying) => paying is not null && paying.Kind switch
    {
        PaymentKind.Action or PaymentKind.Play or PaymentKind.Equip => true,
        PaymentKind.Recruit => paying.Ability is { Cost.Basis: GoldCostBasis.TargetRecruit } recruiting && !RecruitPaysAllCostsNormally(recruiting),
        _ => false
    };

    /// <summary>Whether the recruit an ability makes is one that "pays all costs" as any recruit does (the printed phrase "paying all costs", modified or not).</summary>
    internal static bool RecruitPaysAllCostsNormally(AbilityDefinition ability) => ability.Label.Contains("paying all costs", StringComparison.OrdinalIgnoreCase);
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingF_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingF", 900, static abilities => abilities.AddEmperorRemainingF());
        EngineHooks.Looks.Register("EmperorRemainingFLooks", 100, static () => EmperorRemainingFLooks.All);
    }
}
