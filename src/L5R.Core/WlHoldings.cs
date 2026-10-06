namespace L5R.Core;

// Issue #674 ("holdings" cluster, holding-wave-53): nineteen Holdings of Torn Asunder, Coils of Madness, Gates of Chaos and Aftermath, each wired from the PRINTED text of its scan (read by eye).
//
// Existing primitives used as they are:
//   Border Keep - exp2, Damaged Port, Exotic Market     conditional Gold production (ConditionalProduction.cs): 3 or 4 Gold "if paying for a Holding" (PayingForHolding), 3 for an Item (PayingForItem).
//   Border Keep - exp2's refresh                        the province-cycle of Border Keep (CycleProvinces) limited to the first turn, usable twice (UsesPerTurn 2).
//   Productive Mine, Fudoist Advisor                    the Kharmic keyword's own repeatable action (the printed reminder is the keyword); House of Prophecy: the Destined entry draw.
//   Exquisite Silk Works, Temple of Madness, Temple of Tengen, The Breeding Ground, Hero's Memorial, Questionable Merchant, The Forgotten Bay Dojo, Green Lake Dojo, Imperial Explorer's Dojo,
//   Merchant Outpost, Recruitment Station, Fudoist Advisor     effects of their own below (the dishonorable-personality, any-card, attacking-personality and attach-from-hand primitives).
//
// New in this file: the Gold source of Bamboo Harvesters - exp2 (a payment source of its own, "<card>#bamboo"), the static Item Force of Glassworks, and three out-of-play target categories
// (a Terrain in your discard pile; a face-down card in your own / another player's province). Shared files carry one-line hooks, each commented where it stands.
public static class WlHoldingsAbilities
{
    public const string BorderKeepRefresh = "wl-border-keep-exp2-refresh", SilkWorksShame = "wl-silk-works-shame", FudoistMadness = "wl-fudoist-advisor-destroy-madness",
        GreenLakeOwnTurn = "wl-green-lake-dojo-cavalry-own-turn", GreenLakeOtherTurn = "wl-green-lake-dojo-cavalry", HerosMemorialHonor = "wl-heros-memorial-honor",
        ExplorerTerrain = "wl-imperial-explorers-dojo-terrain", OutpostOwn = "wl-merchant-outpost-own", OutpostEnemy = "wl-merchant-outpost-enemy",
        MerchantMoveHome = "wl-questionable-merchant-move", StationEquip = "wl-recruitment-station-equip", TempleMadness = "wl-temple-of-madness-token", TempleTengen = "wl-temple-of-tengen-loot",
        BreedingGround = "wl-breeding-ground-conqueror", ForgottenBayForce = "wl-forgotten-bay-dojo-force";

    /// <summary>The id of Bamboo Harvesters - exp2's Gold source (a payment token, not a catalogue action).</summary>
    public const string BambooSource = "wl-bamboo-exp2-produce";

    // Effects (data keys on the card, never prose).
    public const string BambooEffect = "wl-bamboo-exp2", BorderKeepEffect = "wl-border-keep-exp2-gold", DamagedPortEffect = "wl-damaged-port-gold", ExoticMarketEffect = "wl-exotic-market-gold",
        GlassworksEffect = "wl-glassworks";

    // Target categories (TargetAllows hook; the three out-of-play ones go through IsWlHoldingsOutOfPlayTarget).
    public const string MadnessHolder = "wl-card-with-madness-token", OwnTerrainInDiscard = "wl-own-terrain-in-discard", OwnFaceDownProvinceCard = "wl-own-face-down-province-card",
        EnemyFaceDownProvinceCard = "wl-enemy-face-down-province-card", ForgottenBayFollower = "wl-follower-with-printed-cost-1-or-more";

    public const string MadnessToken = "Madness", WealthToken = "Wealth";

    public static Dictionary<string, AbilityDefinition> AddWlHoldings(this Dictionary<string, AbilityDefinition> all)
    {
        // Border Keep - exp2 (oracle-l5r-10429): "Limited: Once or twice during your first turn (only), put one or more cards in your Provinces at the bottom of your deck, refilling them face-up."
        all[BorderKeepRefresh] = new("Limited", "Limited: Once or twice during your first turn (only), put one or more cards in your Provinces at the bottom of your deck, refilling them face-up", "provinces", UsesPerTurn: 2);
        // Exquisite Silk Works (oracle-l5r-10608): "Limited: Target a dishonorable Personality. His controller loses 1 Honor."
        all[SilkWorksShame] = new("Limited", "Limited: Target a dishonorable Personality. His controller loses 1 Honor", "dishonorable-personality");
        // Fudoist Advisor (oracle-l5r-10427): "Limited, bow: Destroy a Madness token." The token is named by choosing the card that holds it.
        all[FudoistMadness] = new("Limited", "Limited, bow this card: Destroy a Madness token", MadnessHolder, Costs: ActionCost.BowSource);
        // Green Lake Dojo (oracle-l5r-10265): "Open: Bow this card, destroy it if it is your turn, and target a Personality: Give him Cavalry." The destruction is owed only on its controller's own turn,
        // so it is two abilities that share a use: the bow and destruction on your turn, the bow alone on another player's.
        all[GreenLakeOwnTurn] = new("Open", "Open: Bow this card, destroy it as it is your turn, and target a Personality: Give him Cavalry", "personality", UsageKey: "wl-green-lake-dojo",
            Costs: [new(CostVerb.Bow), new(CostVerbs.Sacrifice)]);
        all[GreenLakeOtherTurn] = new("Open", "Open: Bow this card and target a Personality: Give him Cavalry", "personality", UsageKey: "wl-green-lake-dojo", Costs: ActionCost.BowSource);
        // Hero's Memorial (oracle-l5r-10266): "Reaction: After another player's action resolves which destroyed one of your Personalities, bow this card: Gain 2 Honor."
        all[HerosMemorialHonor] = new("Reaction", "Reaction: After another player's action resolves which destroyed one of your Personalities, bow this card: Gain 2 Honor", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Costs: ActionCost.BowSource);
        // Imperial Explorer's Dojo (oracle-l5r-10613): "Limited, bow: Put a target Terrain in your discard pile into your hand. Destroy this Holding."
        all[ExplorerTerrain] = new("Limited", "Limited, bow this card: Put a target Terrain in your discard pile into your hand. Destroy this Holding", OwnTerrainInDiscard, Costs: ActionCost.BowSource);
        // Merchant Outpost (oracle-l5r-10267): "Limited: Target a card in a province and bow this card if the target is not yours: Turn it face-up." The bow is owed only for a card that is not yours, so
        // it is two abilities that share a use.
        all[OutpostOwn] = new("Limited", "Limited: Target a card in your province: Turn it face-up", OwnFaceDownProvinceCard, UsageKey: "wl-merchant-outpost");
        all[OutpostEnemy] = new("Limited", "Limited: Bow this card and target a card in another player's province: Turn it face-up", EnemyFaceDownProvinceCard, UsageKey: "wl-merchant-outpost", Costs: ActionCost.BowSource);
        // Questionable Merchant (oracle-l5r-10434): "Battle: Move a target attacking Personality home. Destroy this Holding."
        all[MerchantMoveHome] = new("Battle", "Battle: Move a target attacking Personality home. Destroy this Holding", "attacking-personality");
        // Recruitment Station (oracle-l5r-10430): "Battle/Open, [Gold], bow: Equip a Follower, paying 2 less Gold." The Gold is the Follower's own cost (the :gstar: box); bowing is no limit beyond itself.
        all[StationEquip] = new("BattleOrOpen", "Battle/Open, Gold, bow this card: Equip a Follower, paying 2 less Gold", "attachment-in-discard", Performer: "own-personality-at-battle", UsesPerTurn: AbilityUseLimit.Unlimited,
            Cost: GoldCostRule.AttachCost(-2, 0), Costs: ActionCost.BowSource, Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Performer, [CardType.Follower]));
        // Temple of Madness (oracle-l5r-10435): "Limited, bow: Give a target card a Madness token." (Panku's "any-card" shape.)
        all[TempleMadness] = new("Limited", "Limited, bow this card: Give a target card a Madness token", "any-card", Costs: ActionCost.BowSource);
        // Temple of Tengen (oracle-l5r-10431): "Limited, bow: Draw a card. Discard a card."
        all[TempleTengen] = new("Limited", "Limited, bow this card: Draw a card. Discard a card", "none", Costs: ActionCost.BowSource);
        // The Breeding Ground (oracle-l5r-10616): "Limited, bow: Give your target Personality Conqueror." (until the end of the turn, R-001)
        all[BreedingGround] = new("Limited", "Limited, bow this card: Give your target Personality Conqueror", "own-personality", Costs: ActionCost.BowSource);
        // The Forgotten Bay Dojo (oracle-l5r-10269): "Battle: Target a Follower with a Gold Cost of 1 or more: Raise its Force to 4." (Visage of the Orochi's delta-to-a-value shape.)
        all[ForgottenBayForce] = new("Battle", "Battle: Target a Follower with a Gold Cost of 1 or more: Raise its Force to 4", ForgottenBayFollower);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWlHoldings(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case WlHoldingsAbilities.BorderKeepRefresh:
                CycleProvinces(player, command.CardIds!);
                return false;
            case WlHoldingsAbilities.SilkWorksShame:
                GainHonor(target!.OwnerId, -1, Definition(source!).Name, source);
                return false;
            case WlHoldingsAbilities.FudoistMadness:
                DestroyToken(target!, target!.Tokens.First(t => t.Type == WlHoldingsAbilities.MadnessToken));
                return false;
            case WlHoldingsAbilities.GreenLakeOwnTurn:
            case WlHoldingsAbilities.GreenLakeOtherTurn:
                GrantKeyword(target!, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case WlHoldingsAbilities.HerosMemorialHonor:
                GainHonor(player.Id, 2, Definition(source!).Name, source);
                return false;
            case WlHoldingsAbilities.ExplorerTerrain:
                ReturnToHand(target!);
                if (InPlay(source!)) DestroyUnits([source!], false, player.Id);
                return false;
            case WlHoldingsAbilities.OutpostOwn:
            case WlHoldingsAbilities.OutpostEnemy:
                target!.FaceUp = true;
                Emit("provinceRevealed", $"{player.Name} turned {Definition(target).Name} face-up in province {(target.ProvinceIndex ?? 0) + 1}.", player.Id, target.Id);
                return false;
            case WlHoldingsAbilities.MerchantMoveHome:
                MoveHome(target!, false);
                if (InPlay(source!)) DestroyUnits([source!], false, player.Id);
                return false;
            case WlHoldingsAbilities.TempleMadness:
                CreateToken(target!, WlHoldingsAbilities.MadnessToken, id);
                return false;
            case WlHoldingsAbilities.TempleTengen:
                Draw(player);
                if (player.Hand.Count > 0) _state.PendingForcedDiscards.Add(new(player.Id, Definition(source!).Name));
                return false;
            case WlHoldingsAbilities.BreedingGround:
                GrantKeyword(target!, "Conqueror", source!.Id, GrantDuration.Turn);
                return false;
            case WlHoldingsAbilities.ForgottenBayForce:
                if (CardForce(target!) < 4) ApplyModifier(target!, source!.Id, id, "Force", 4 - CardForce(target!));
                return false;
            default:
                return null;
        }
    }

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlHoldings(string id, PlayerState player)
    {
        switch (id)
        {
            case WlHoldingsAbilities.BorderKeepRefresh:
                Require(player.TurnsTaken == 1, "This Border Keep ability is only available on your first turn.");
                break;
            case WlHoldingsAbilities.GreenLakeOwnTurn:
                Require(_state.ActivePlayerId == player.Id, "This version destroys the Holding because it is your turn; on another player's turn use the other.");
                break;
            case WlHoldingsAbilities.GreenLakeOtherTurn:
                Require(_state.ActivePlayerId != player.Id, "It is your turn: this action destroys the Holding.");
                break;
        }
    }

    // The target categories of this group that stand in play.
    private bool WlTargetAllows(string category, CardInstance target)
    {
        switch (category)
        {
            case WlHoldingsAbilities.MadnessHolder:
                return InPlay(target) && target.Tokens.Any(t => t.Type == WlHoldingsAbilities.MadnessToken);
            case WlHoldingsAbilities.ForgottenBayFollower:
                return Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached && CardGoldCost(target) >= 1; // the scan says "a Gold Cost" (the catalogue text adds "printed"): the current Gold Cost
            default:
                return false;
        }
    }

    // ---- out-of-play targets (ValidateAbility's chain and the action listing's candidates) ------------------------------------------------------------------------------------------------

    private static bool IsWlHoldingsOutOfPlayTarget(string target) =>
        target is WlHoldingsAbilities.OwnTerrainInDiscard or WlHoldingsAbilities.OwnFaceDownProvinceCard or WlHoldingsAbilities.EnemyFaceDownProvinceCard;

    private bool WlOutOfPlayTargetAllows(string category, int playerId, CardInstance card) => category switch
    {
        // "A Terrain in your discard pile": a Terrain Strategy in the player's own Fate discard pile.
        WlHoldingsAbilities.OwnTerrainInDiscard => card.OwnerId == playerId && Definition(card).Type == CardType.Strategy && Keyword(card, "Terrain") && card.Zone == Zone.FateDiscard,
        // "A card in a province ... turn it face-up": only a face-down one has anything to turn.
        WlHoldingsAbilities.OwnFaceDownProvinceCard => card.OwnerId == playerId && card.Zone == Zone.Province && !card.FaceUp,
        WlHoldingsAbilities.EnemyFaceDownProvinceCard => card.OwnerId != playerId && card.Zone == Zone.Province && !card.FaceUp,
        _ => false
    };

    private void ValidateWlHoldingsOutOfPlayTarget(string category, PlayerState player, CardInstance card) =>
        Require(WlOutOfPlayTargetAllows(category, player.Id, card), category switch
        {
            WlHoldingsAbilities.OwnTerrainInDiscard => "Choose a Terrain in your discard pile.",
            WlHoldingsAbilities.OwnFaceDownProvinceCard => "Choose a face-down card in one of your provinces.",
            _ => "Choose a face-down card in another player's province."
        });

    private string?[]? WlHoldingsCandidates(string category, int playerId) => IsWlHoldingsOutOfPlayTarget(category)
        ? _state.Cards.Values.Where(c => WlOutOfPlayTargetAllows(category, playerId, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
        : null;

    // ---- static: Glassworks ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Your Items have +1F." An Item's Force conveys to its wielder (PersonalityForce), so each Item attached to a Personality adds 1 for every Glassworks its controller has in play.
    private int WlGlassworksForce(CardInstance card)
    {
        if (!InPlay(card) || Definition(card).Type != CardType.Personality) return 0;
        var total = 0;
        foreach (var item in Attachments(card.Id).Where(a => Definition(a).Type == CardType.Item))
            total += _state.Cards.Values.Count(c => c.OwnerId == item.OwnerId && InPlay(c) && Definition(c).Type == CardType.Holding && Definition(c).Effect == WlHoldingsAbilities.GlassworksEffect);
        return total;
    }

    // ---- Bamboo Harvesters - exp2's Gold -----------------------------------------------------------------------------------------------------------------------------------------------------

    // "Bow this card: Produce 0 Gold. Give this Holding a +1 Gold Production Wealth token." Its printed production is 0, so the ordinary producers never list it: it is a payment source of its own
    // ("<card>#bamboo"), producing the Gold its tokens add, whose use bows it and gives it the next token. PaymentTokens' extension.
    private IEnumerable<PaymentToken> WlPaymentTokens(int playerId)
    {
        foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield && !c.Bowed && Definition(c).Effect == WlHoldingsAbilities.BambooEffect)
            .OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var amount = Math.Max(0, Definition(card).GoldProduction + ShadowsEmbraceBatch6Production(card, playerId));
            yield return new(card.Id + PaymentTokenMark + "bamboo", card, WlHoldingsAbilities.BambooSource, amount, "bow",
                $"Bow this card: {amount} Gold. It gains a +1 Gold Production Wealth token", null);
        }
    }

    // A source that adds no Gold is never suggested: it is chosen to feed the Holding.
    private static bool WlNeverSuggested(PaymentToken token) => token.AbilityId == WlHoldingsAbilities.BambooSource && token.Amount == 0;

    // UsePaymentTokens' step; true when the token was this file's.
    private bool WlUsePaymentToken(PaymentToken token, int playerId)
    {
        if (token.AbilityId != WlHoldingsAbilities.BambooSource) return false;
        var card = token.Card;
        card.Bowed = true;
        Emit("goldSourceUsed", $"{Definition(card).Name} bowed to produce {token.Amount} Gold.", playerId, card.Id);
        CreateToken(card, WlHoldingsAbilities.WealthToken, Definition(card).Name, "GoldProduction", 1);
        return true;
    }

    // ---- Hero's Memorial's trigger ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // AfterActionResolvedApplies' hook: "After another player's action resolves which destroyed one of your Personalities."
    private bool? WlAfterActionApplies(string abilityId, int playerId, ResolvedActionFacts facts) =>
        abilityId == WlHoldingsAbilities.HerosMemorialHonor ? facts.PlayerId != playerId && LastActionDestroyedOwnPersonality(playerId) : null;

    // Registration only: callbacks are not invoked during module initialization. Priorities 14900-14999 are this group's.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlHoldingsHooks()
    {
        EngineHooks.Abilities.Register("AddWlHoldings", 14900, static abilities => abilities.AddWlHoldings());
        EngineHooks.RunEffects.Register("RunWlHoldings", 14901, static (engine, context) => engine.RunWlHoldings(context.id, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidateWlHoldings", 14902, static (engine, context) => engine.ValidateWlHoldings(context.id, context.player));
        EngineHooks.TargetAllows.Register("WlTargetAllows", 14903, static (engine, context) => engine.WlTargetAllows(context.ability.Target, context.target));
        EngineHooks.PersonalityForce.Register("WlGlassworksForce", 14904, static (engine, context) => engine.WlGlassworksForce(context.card));
    }
}
