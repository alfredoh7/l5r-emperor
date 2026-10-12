using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace L5R.Core;

// Issue #732, wave batch 1: the payment and cost cards, wired on the merged P1 (payment hooks, issue #695) and P2 (cost terms, issue #682) primitives, with P3 (recruit gate) and P12
// (standing immunity) for the two cards whose text also needs them. Every price and every Gold amount is produced in Core. Ability ids are "w732-". Readings for Mendon to confirm or correct
// are in docs/decisions/2026-10-08-wave-batch-1-payment-and-cost.md. Waves personality/follower/item/spell/strategy/holding (tools/content/package_wave_732.py).
//
//   Tsujiken's Coin          ":bow: Produce 2 Gold."                                           printed GoldProduction 2 on an attached Item (P1 piece 6)
//   Opening the Veil         "Your Stronghold has +1 Gold Production for each Spirit you control."   EngineHooks.ProductionAdjustment (the Stronghold)
//   Truce                    "Reaction: When paying for a Personality with a Clan alignment different than your own: Produce 2 Gold."   a hand payment token that discards itself
//   Ever-Winter Copse        "When another Holding produces Gold to pay for a Nonhuman, bow this card: Produce 1 Gold."   a bowing token that needs another Holding among the sources
//   The Amethyst Court       ":bow: Produce 2 Gold." / ":bow: Bow your target unbowed Courtier or Artisan. Produce Gold equal to his Chi."   printed production + a token per target
//   Colonial Dojo            ":bow: When paying for a non-Unique Samurai, he enters play for 0 Gold."   ConditionalProduction with an amount function (his printed cost)
//   Suikihime's Chambers     ":bow: Produce 2 Gold, or 4 Gold if paying for a Unique Personality." / "Limited, :bow: Gain 1 Honor."   production 2, conditional 4; w732-chambers-honor
//   Trade Embargo            "You may only play one copy of Trade Embargo per turn." / "Open, 4 Gold: All non-Unique Holdings produce 1 less Gold (this turn)."   a player-level turn modifier
//   Uncovering Treasures     ":bow: Produce 2 Gold. Discard this Strategy from play." / "Limited, 2 Gold: Put this Strategy into play."   a token in the EventInPlay zone; w732-treasures-play
//   Morito Inoue, Black Riders   "Limited: Straighten another player's target Holding ... You, and not its owner, may use its Gold-producing traits (this turn) as if you controlled it."
//                            Black Riders adds "and cost-reducing" traits.   w732-inoue-lend, w732-riders-lend (LendProducer; Whispering Archive's discount reads the borrower)
//   Doji Kurohime            "Limited, :bow: Straighten a target Honor-producing Holding that has not produced Gold this turn."   w732-kurohime-straighten
//   Asukai, the Tireless     "Enters play ignoring Gold Cost if you control the Ring of Earth." / "Other players' cards will not affect your Provinces' strengths."   entry term + standing immunity
//   Sengmai, the Unbreakable "Enters play ignoring Gold Cost if you control the Ring of Fire." / "will not enter play if your Family Honor is 0 or above." / "After Sengmai enters play, put a Kiho
//                            in your discard pile into your hand."   entry term, the P3 Effect, w732-sengmai-kiho (a discard-pile search)
//   Daigotsu Kikumura        "If you are a Dragon Clan player, Kikumura's Invest is 2." / "Invest 4: Create two 1F Followers and attach them to Kikumura."   w732-kikumura-invest, one ability cost term
//
// Not wired, with a blocker note (tools/content/pe_wave_732_blockers.json): Chiyurei's Axe (a performer trait on every Melee Attack: AttackCompareOptions is a static function of the ability id) and
// Issue #835: Duty Over All Things was wired afterwards in Wave835.cs, on Mendon's rulings of 2026-10-11 (docs/decisions/2026-10-11-twelve-ruled-cards.md); the "not wired" note here is the state when this wave merged.
// Duty Over All Things (a discarded Yojimbo as the action's performer, recruited by the action: no zone support for a Dead or discarded performer).
public static class Wave732Abilities
{
    public const string ChambersHonor = "w732-chambers-honor", InoueLend = "w732-inoue-lend", RidersLend = "w732-riders-lend", KurohimeStraighten = "w732-kurohime-straighten",
        Embargo = "w732-embargo", TreasuresPlay = "w732-treasures-play", SengmaiKiho = "w732-sengmai-kiho", KikumuraInvest = "w732-kikumura-invest";

    // Target categories of this file (TargetAllows).
    public const string InoueHolding = "w732-inoue-holding", RidersHolding = "w732-riders-holding", HonorHolding = "w732-honor-holding";

    // The cards (oracle ids).
    public const string Veil = "oracle-l5r-10350", Truce = "oracle-l5r-10401", Dojo = "oracle-l5r-10432", Chambers = "oracle-l5r-10436", Asukai = "oracle-l5r-10444", Inoue = "oracle-l5r-10464",
        Sengmai = "oracle-l5r-10499", Kikumura = "oracle-l5r-10659", EmbargoCard = "oracle-l5r-10723", Treasures = "oracle-l5r-10751", Court = "oracle-l5r-10752", Kurohime = "oracle-l5r-10840",
        BlackRiders = "oracle-l5r-10892", Coin = "oracle-l5r-10911", Copse = "oracle-l5r-2391";

    /// <summary>Kikumura's Invest has no "Reaction:" designator: a trait modelled with Reaction timing, so a rule that bars Reactions does not touch it (Mendon's R4 ruling; TraitReactions.Is asks this).
    /// Sengmai's search is a printed "After Sengmai enters play" trait too.</summary>
    public static bool IsTrait(string abilityId) => abilityId is KikumuraInvest or SengmaiKiho;

    // The two Followers Kikumura's Invest creates are the published Farmlands Conscripts, attached to him.
    public static readonly CreatedCardSpec Retinue = CreatedProxies677Abilities.Conscripts with { Count = 2, HostIsSource = true };

    public static readonly SearchSpec KihoSearch = new("w732-sengmai-kiho", "Put a Kiho in your discard pile into your hand",
        [new SearchDeckOption(SearchDeck.FateDiscard, new SearchCriteria("Kiho", Keywords: ["Kiho"]), SearchEffect.ToHand)]);

    public static Dictionary<string, AbilityDefinition> AddWave732(this Dictionary<string, AbilityDefinition> all)
    {
        all[ChambersHonor] = new("Limited", "Limited, bow this card: Gain 1 Honor", "none", Costs: ActionCost.BowSource);
        all[InoueLend] = new("Limited", "Limited: Straighten another player's target Holding whose Gold Cost is lower than Inoue's Force. You, and not its owner, may use its Gold-producing traits (this turn)", InoueHolding);
        all[RidersLend] = new("Limited", "Limited: Straighten another player's target Holding with Gold Cost equal to or lower than this Follower's Force. You, and not its owner, may use its Gold-producing and cost-reducing traits (this turn)", RidersHolding);
        all[KurohimeStraighten] = new("Limited", "Limited, bow this card: Straighten a target Honor-producing Holding that has not produced Gold this turn", HonorHolding, Costs: ActionCost.BowSource);
        all[Embargo] = new("Open", "Open: All non-Unique Holdings produce 1 less Gold (this turn)", "none");
        all[TreasuresPlay] = new("Limited", "Limited: Put this Strategy into play", "none");
        all[SengmaiKiho] = new("Reaction", "After Sengmai enters play: put a Kiho in your discard pile into your hand", "none", Trigger: "own-enters-play", Search: KihoSearch);
        all[KikumuraInvest] = new("Reaction", "Invest 4 Gold: Create two 1F Followers and attach them to Kikumura", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(4), Creates: Retinue);
        return all;
    }
}

public sealed partial class MatchState
{
    // 22900-22909 is reserved for batch 1 of the wirable-card waves (issue #732). The turn on which each player last played Trade Embargo ("player:turn"), omitted until a copy is played.
    [JsonPropertyOrder(22900)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? EmbargoPlayTurns { get; set; }
}

public sealed partial class MatchEngine
{
    private static readonly Regex GainsHonorLabel = new(@"\bgain(s)?\s+\d+\s+Honor\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> Wave732InPlay(int playerId, string definitionId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.DefinitionId == definitionId && InPlay(c));

    /// <summary>"A Clan alignment different than your own": the Personality has a Clan alignment (it is not Unaligned) and none of its alignments is the paying player's Stronghold Clan.</summary>
    private bool Wave732DifferentClan(CardInstance personality, int playerId) =>
        ClanAlignments(personality).Any() && !HasClanAffiliation(personality, Player(playerId).Clan);

    /// <summary>A Holding with an ability that makes its controller gain Honor ("Gain 1 Honor"): this engine's reading of "Honor-producing", as "Gold-producing" is a Holding that produces Gold.</summary>
    private bool Wave732HonorProducing(CardInstance holding) =>
        Definition(holding).Type == CardType.Holding
        && (Definition(holding).Abilities ?? []).Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && GainsHonorLabel.IsMatch(ability.Label));

    // ---- payment tokens: Truce, Ever-Winter Copse, The Amethyst Court, Uncovering Treasures ----------------------------------------------------------------------------------------------------

    private IEnumerable<PaymentToken> Wave732Tokens(PaymentTokenContext context)
    {
        var player = Player(context.PlayerId);
        var subject = context.Paying;
        if (subject is not null)
        {
            // Truce: a hand Strategy, discarded as it pays, for a Recruit of a Personality aligned with another Clan. It is a printed Reaction, so Cherry Blossom Festival bars it.
            if (subject.Kind == PaymentKind.Recruit && Definition(subject.Card).Type == CardType.Personality && !ReactionsBarred() && Wave732DifferentClan(subject.Card, context.PlayerId))
                foreach (var id in player.Hand)
                    if (Card(id) is { DefinitionId: Wave732Abilities.Truce } truce)
                        yield return new(truce.Id + "#truce", truce, Wave732Abilities.Truce, 2, "hand", "Truce: discard it from your hand, produce 2 Gold", null,
                            AllowedZones: [Zone.Hand], OnUse: static (engine, token, playerId) => engine.PaymentDiscard(token.Card, playerId));
            // Ever-Winter Copse: a trait of a Holding in play. Another Holding must be among the sources paying for a Nonhuman card; the Copse bows (once) for 1 Gold.
            if (subject.Kind != PaymentKind.Action && Keyword(subject.Card, "Nonhuman"))
                foreach (var copse in Wave732InPlay(context.PlayerId, Wave732Abilities.Copse))
                {
                    var self = copse.Id;
                    yield return new(copse.Id + "#copse", copse, Wave732Abilities.Copse, 1, "bow-reaction", "Ever-Winter Copse: bow it, produce 1 Gold", null, Bows: true,
                        RequiresOtherSources: (engine, _, ids) => ids.Any(id => id != self && engine.Wave732IsOtherHolding(id)),
                        RequirementMessage: "Ever-Winter Copse produces Gold only when another Holding also produces Gold to pay for a Nonhuman.");
                }
        }
        // The Amethyst Court: "Bow your target unbowed Courtier or Artisan. Produce Gold equal to his Chi." One token per such Personality of the payer's; the Court bows with it, the target on use.
        foreach (var court in Wave732InPlay(context.PlayerId, Wave732Abilities.Court).Where(c => !c.Bowed))
            foreach (var target in _state.Cards.Values.Where(c => c.OwnerId == context.PlayerId && InPlay(c) && !c.Bowed && Definition(c).Type == CardType.Personality
                && (Keyword(c, "Courtier") || Keyword(c, "Artisan"))).OrderBy(c => c.Id, StringComparer.Ordinal))
                yield return new(court.Id + "#court:" + target.Id, court, Wave732Abilities.Court, Math.Max(0, PersonalityChi(target)), "bow-reaction",
                    $"The Amethyst Court: bow {Definition(target).Name}, produce {Math.Max(0, PersonalityChi(target))} Gold", target, Bows: true,
                    OnUse: static (engine, token, _) => { if (token.Subject is { } courtier) courtier.Bowed = true; });
        // Uncovering Treasures: once in play, bow it for 2 Gold and discard it.
        foreach (var treasures in _state.Cards.Values.Where(c => c.OwnerId == context.PlayerId && c.DefinitionId == Wave732Abilities.Treasures && c.Zone == Zone.EventInPlay && !c.Bowed))
            yield return new(treasures.Id + "#treasures", treasures, Wave732Abilities.Treasures, 2, "bow-reaction", "Uncovering Treasures: bow it, produce 2 Gold, discard it", null, Bows: true,
                OnUse: static (engine, token, playerId) => engine.PaymentDiscard(token.Card, playerId));
    }

    // Another Holding among the chosen sources: a Holding card chosen for its own production, or a payment token whose card is a Holding.
    private bool Wave732IsOtherHolding(string sourceId)
    {
        var cardId = IsPaymentToken(sourceId) ? PaymentTokenCardId(sourceId) : sourceId;
        return _state.Cards.TryGetValue(cardId, out var card) && Definition(card).Type == CardType.Holding;
    }

    // ---- conditional production: Colonial Dojo, Suikihime's Chambers --------------------------------------------------------------------------------------------------------------------------

    private ConditionalProduction? Wave732ConditionalProduction(CardInstance card) => card.DefinitionId switch
    {
        // "When paying for a non-Unique Samurai, he enters play for 0 Gold": the card's printed Gold Cost (the price it would otherwise have is never read: it would recurse).
        Wave732Abilities.Dojo => new(0, ProductionCondition.Custom, "w732-dojo", AmountOf: static (engine, subject) => engine.PrintedGoldCost(subject.Card)),
        // "Produce 2 Gold, or 4 Gold if paying for a Unique Personality": the printed 2 otherwise.
        Wave732Abilities.Chambers => new(0, ProductionCondition.Custom, "w732-chambers", AmountOf: static (_, _) => 4),
        _ => null
    };

    private bool? Wave732ProductionCondition(ProductionConditionContext context)
    {
        if (context.Production.Keyword is not ("w732-dojo" or "w732-chambers")) return null;
        if (context.Paying is not { Kind: PaymentKind.Recruit } subject || Definition(subject.Card).Type != CardType.Personality) return false;
        return context.Production.Keyword == "w732-dojo" ? Keyword(subject.Card, "Samurai") && !Keyword(subject.Card, "Unique") : Keyword(subject.Card, "Unique");
    }

    // ---- production adjustment: Opening the Veil ----------------------------------------------------------------------------------------------------------------------------------------------------

    // "Your Stronghold has +1 Gold Production for each Spirit you control": +1 for each Spirit Personality or Follower of the Stronghold's owner in play, for each Opening the Veil of his in play.
    private int Wave732VeilProduction(ProductionAdjustmentContext context)
    {
        if (context.Card.Zone != Zone.Stronghold || Definition(context.Card).Type != CardType.Stronghold) return 0;
        var veils = Wave732InPlay(context.Card.OwnerId, Wave732Abilities.Veil).Count();
        if (veils == 0) return 0;
        var spirits = _state.Cards.Values.Count(c => c.OwnerId == context.Card.OwnerId && InPlay(c) && Definition(c).Type is CardType.Personality or CardType.Follower && Keyword(c, "Spirit"));
        return veils * spirits;
    }

    // ---- entry terms: Asukai, Sengmai, Chiyurei is not wired ---------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave732Entry(EntryCostContext context)
    {
        if (context.Kind is not (EntryCostKind.Recruit or EntryCostKind.Reserve)) yield break;
        var ring = context.Card.DefinitionId switch { Wave732Abilities.Asukai => "Ring of Earth", Wave732Abilities.Sengmai => "Ring of Fire", _ => null };
        if (ring is not null && ControlsRing(context.PayerId, ring)) yield return new(0, ring, IgnoreCost: true);
    }

    // ---- ability cost terms: Kikumura's Invest ---------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CostTerm> Wave732AbilityCost(AbilityCostContext context)
    {
        // "If you are a Dragon Clan player, Kikumura's Invest is 2": 4 less 2. Invest costs are not discounted by any other card (Gates of Chaos supplement), so only this ability asks.
        if (context.AbilityId == Wave732Abilities.KikumuraInvest && Player(context.PayerId).Clan == "Dragon") yield return new(-2, "Dragon Clan player");
    }

    // ---- target legality --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave732TargetAllows(TargetAllowsContext context)
    {
        var target = context.target;
        var isHolding = Definition(target).Type == CardType.Holding && InPlay(target);
        switch (context.ability.Target)
        {
            // "another player's target Holding whose Gold Cost is lower than Inoue's Force" / "with Gold Cost equal to or lower than this Follower's Force": the printed Gold Cost.
            case Wave732Abilities.InoueHolding:
                return isHolding && target.OwnerId != context.player.Id && context.source is { } inoue && PrintedGoldCost(target) < CardForce(inoue);
            case Wave732Abilities.RidersHolding:
                return isHolding && target.OwnerId != context.player.Id && context.source is { } riders && PrintedGoldCost(target) <= CardForce(riders);
            case Wave732Abilities.HonorHolding:
                return isHolding && Wave732HonorProducing(target) && !ProducedGoldThisTurn(target);
            default:
                return false;
        }
    }

    // ---- validation and effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave732Validate(ValidationHookContext context)
    {
        switch (context.id)
        {
            case Wave732Abilities.TreasuresPlay:
                Require(context.source is { Zone: Zone.Hand }, "Put Uncovering Treasures into play from your hand.");
                break;
            case Wave732Abilities.Embargo:
                Require(!EmbargoPlayedThisTurn(context.player.Id), "You may only play one copy of Trade Embargo per turn.");
                break;
        }
    }

    private bool EmbargoPlayedThisTurn(int playerId) => _state.EmbargoPlayTurns is { } plays && plays.TryGetValue(playerId, out var turn) && turn == _state.TurnNumber;

    private bool? RunWave732(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            case Wave732Abilities.ChambersHonor:
                GainHonor(player.Id, 1, Definition(source!).Name, source);
                return false;
            case Wave732Abilities.InoueLend or Wave732Abilities.RidersLend:
                // Straighten first (the Holding must be unbowed to be lent); then it is the borrower's Gold producer, with its cost-reducing traits, for the turn.
                Straighten(target!);
                LendProducer(target!, player.Id, $"{player.Name} uses {Definition(target!).Name}'s Gold-producing{(id == Wave732Abilities.RidersLend ? " and cost-reducing" : "")} traits this turn.");
                return false;
            case Wave732Abilities.KurohimeStraighten:
                Straighten(target!);
                return false;
            case Wave732Abilities.Embargo:
                SpendHandPlayedCard(player, source);
                (_state.EmbargoPlayTurns ??= [])[player.Id] = _state.TurnNumber;
                AddProductionTurnModifier(null, -1, ProductionScope.NonUniqueHoldings, source, "Trade Embargo");
                return false;
            case Wave732Abilities.SengmaiKiho:
                BeginSearch(c.ability.Search!, source!, player.Id, "ability", c.bowedBeforeAction);
                return true;
            case Wave732Abilities.TreasuresPlay:
                player.Hand.Remove(source!.Id); source.Zone = Zone.EventInPlay; source.FaceUp = true;
                Emit("strategyEnteredPlay", $"{player.Name} puts {Definition(source).Name} into play.", player.Id, source.Id);
                return false;
            default:
                return null;
        }
    }

    // Black Riders' "cost-reducing traits": Whispering Archive's static discount reads the borrower of a lent Holding (Wave732Abilities is registered below; ShadowsEmbraceBatch6 calls this).
    internal bool Wave732ControlsForTraits(CardInstance holding, int playerId) => ProducerControlledBy(holding, playerId);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave732Hooks()
    {
        EngineHooks.Abilities.Register("Wave732Abilities", 13300, static abilities => abilities.AddWave732());
        EngineHooks.PaymentTokens.Register("Wave732PaymentTokens", 13300, static (engine, context) => engine.Wave732Tokens(context));
        EngineHooks.Searches.Register("Wave732Searches", 13300, static () => [Wave732Abilities.KihoSearch]);
        EngineHooks.ConditionalProductions.Register("Wave732ConditionalProductions", 13300, static (engine, card) => engine.Wave732ConditionalProduction(card));
        EngineHooks.ProductionCondition.Register("Wave732ProductionCondition", 13300, static (engine, context) => engine.Wave732ProductionCondition(context));
        EngineHooks.ProductionAdjustment.Register("Wave732VeilProduction", 13300, static (engine, context) => engine.Wave732VeilProduction(context));
        EngineHooks.EntryCostTerms.Register("Wave732Entry", 13300, static context => context.Engine.Wave732Entry(context));
        EngineHooks.AbilityCostTerms.Register("Wave732AbilityCost", 13300, static context => context.Engine.Wave732AbilityCost(context));
        EngineHooks.TargetAllows.Register("Wave732TargetAllows", 13300, static (engine, context) => engine.Wave732TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave732Validate", 13300, static (engine, context) => engine.Wave732Validate(context));
        EngineHooks.RunEffects.Register("Wave732RunEffects", 13300, static (engine, context) => engine.RunWave732(context));
        // Asukai: "Other players' cards will not affect your Provinces' strengths."
        EngineHooks.StandingImmunities.Register("Wave732AsukaiProvinces", 13300, static () => new StandingImmunity(
            Id: "w732-asukai-provinces", OwnerIs: static d => d.Id == Wave732Abilities.Asukai, Kind: ImmunityKind.ProvinceStrength, Scope: ImmunityScope.Player, Source: ImmunitySource.OtherPlayers));
    }
}
