namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group F: the Events, Regions and Holdings wired in event-wave-8, region-wave-9 and holding-wave-13. The declarative ability data lives here
// (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file, each naming its card. Every card's printed text (the SCAN's, which can
// differ from the Oracle text) and the reading taken are at its entry. The cards left unwired, and why, are in tools/content/ee_remF_blockers.json.
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

    // The ability ids whose effects RunEmperorRemainingFEffect runs (RunAbilityEffects' guard case).
    private static bool IsEmperorRemainingFAbility(string id) => id is
        "peddler-draw" or "temples-gisei-follower" or "temples-gisei-item" or "temples-gisei-spell" or "akodo-grave-destroy" or "refuge-three-sisters-search";

    // Per-id conditions of group F's abilities (ValidateAbility's hook).
    private void ValidateEmperorRemainingF(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Blighted Region: "If you are the Defender" (a condition on taking the action at all).
        if (id == "blighted-region-move") Require(player.Id != _state.ActivePlayerId, "Blighted Region: only the Defender may use this.");
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
        EmperorRemainingFAbilities.PayingForActionWhen => paying.Kind is PaymentKind.Action or PaymentKind.Play or PaymentKind.Equip,
        _ => false
    };
}
