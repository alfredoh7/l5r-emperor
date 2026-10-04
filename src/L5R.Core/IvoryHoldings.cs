namespace L5R.Core;

// Holdings of issue #622 (holding-wave-48): the earliest Emperor-era printings of seventeen Holdings the catalogue's primary printing placed in Ivory. Every card's text is the PRINTED text of the scan.
//
// Already expressible with the shared engine (data only, in the wave's Effect fields):
//   * Copper Mine, Geisha House, Iron Mine, Kobune Port, Shinomen Marsh, Silver Mine, Stables: "Bow this card: Produce 2 Gold, or 3 Gold if you are a [Clan] Clan player." -- the "<clan>-gold" Effects
//     (ClanGold.cs generalises Gold Mine's/Marketplace's crane-gold/dragon-gold into one Effect -> Clan table that the engine, Stolen Merchandise and the training opponent all read).
//   * Temple of Harmony: "Produce 2 Gold, or 3 Gold if paying for a Monk." -- a ConditionalProduction (PayingForKeyword "Monk": a Monk Personality recruited, never an action).
//   * Farmer's Market: "Your maximum hand size is increased by 1." -- Kazehime's increase-own-hand-size Effect (HandLimitFor, which also lists the card on the public hand-limit placard).
//   * Merchant Atoll: "Your provinces have +1 strength." -- a registered ProvinceBonus (the same live read as Fortifications), "Bow this card: Produce 5 Gold" its production.
//
// New here:
//   * VAST PADDY FIELDS: "If this paid for a Farm Holding entering play from a province, refill the province face-up." Pay notes, on the Holding being recruited, that Vast Paddy Fields was among the
//     sources that paid for it (and which province slot it leaves); the Holding's entry (ApplyMandatoryEntryEffects) turns the card the province refilled with face-up. A Holding bought without it is
//     refilled face-down as ever; a province that could not refill (an empty Dynasty deck) has nothing to turn.
//   * DEEDS AND WORDS: "Reaction: Before the resolution of an action during the Action Phase, destroy this card: Choose one of the Honor gains or losses from its effects. Reduce it to 0." It is played in
//     the window before an Honor change (EmperorRulingR4Honor.cs: Mendon's R4 ruling, Rhetoric / Solid Defense / Shiro Chugo), so the Honor change "chosen" is the one the window is about; it must be the
//     Action Phase (Phase.Action). The change's key (turn, action, occurrence) is marked and GainHonor reduces exactly that change to 0 when the command is run again.
//   * FAMILY LIBRARY: Limited, destroy this card: the acting player names one of his provinces holding a FACE-UP card and a discarded (not dead) Personality of his; the card is discarded and the province
//     refilled face-up with the Personality (Scavenging Party's refill, ScavengingParty.cs). Scavenging Party's "own-discarded-personality" target is narrowed to the Dynasty/Fate discard piles here.
//   * RICE PADDY: Battle, bow and destroy this card (both costs, Akodo's Grave's shape), target an attacking Follower at one of your provinces' battlefields: bow it. "Attacking" is the Attacker's army
//     (Defensive Blockade's reading); "at one of your provinces' battlefields" makes the acting player the Defender at the current battlefield.
//   * JIRAMU'S COURT: Open, bow this card, target your Courtier: straighten him (Ageless Shrine's / Second City Port's shape).  SHRINE TO HACHIMAN: Battle/Open, target an attachment: straighten it.
//   * HEAVY INFANTRY DOJO: Battle, bow this card, choose your performing Personality: +2F (until end of turn: R-001).
public static class IvoryHoldingsAbilities
{
    public const string JiramuStraighten = "jiramus-court-straighten";
    public const string HachimanStraighten = "shrine-hachiman-straighten";
    public const string DojoBoost = "heavy-infantry-dojo-boost";
    public const string RicePaddyBow = "rice-paddy-bow";
    public const string FamilyLibraryRefill = "family-library-refill";
    public const string DeedsReduce = "deeds-words-reduce";

    /// <summary>Rice Paddy's target category: an attacking Follower (a Follower whose Personality is in the Attacker's army) at the current battlefield, the acting player being the Defender there.</summary>
    public const string AttackingFollowerAtOwnProvince = "ivory-attacking-follower-at-own-province";

    /// <summary>Effects (their cards' Effect fields).</summary>
    public const string MerchantAtollEffect = "merchant-atoll-provinces";
    public const string VastPaddyEffect = "vast-paddy-fields";

    public static Dictionary<string, AbilityDefinition> AddIvoryHoldings(this Dictionary<string, AbilityDefinition> all)
    {
        // Jiramu's Court (oracle-l5r-10189): "Open: Bow this card and target your Courtier: Straighten him."
        all[JiramuStraighten] = new("Open", "Bow this card and target your Courtier: Straighten him", "own-personality-with-keyword", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowSource);
        // Shrine to Hachiman (oracle-l5r-7223): "Battle/Open: Target an attachment: Straighten it." (any attachment in play)
        all[HachimanStraighten] = new("BattleOrOpen", "Target an attachment: Straighten it", "attachment");
        // Heavy Infantry Dojo (oracle-l5r-3044): "Battle: Choose your performing Personality and bow this card: Give him +2F." (Killing Intent's performer: no "unbowed" is printed)
        all[DojoBoost] = new("Battle", "Choose your performing Personality and bow this card: Give him +2F", "none", Performer: "own-personality-at-battle", Costs: ActionCost.BowSource);
        // Rice Paddy (oracle-l5r-6314): "Battle: Bow and destroy this card, and target an attacking Follower at one of your provinces' battlefields: Bow it."
        all[RicePaddyBow] = new("Battle", "Bow and destroy this card, and target an attacking Follower at one of your provinces' battlefields: Bow it", AttackingFollowerAtOwnProvince,
            Costs: [new(CostVerb.Bow), new(CostVerbs.Sacrifice)]);
        // Family Library (oracle-l5r-2463): "Limited: Destroy this card: Discard a face-up card in one of your provinces, refilling it with a discarded (not dead) Personality from your discard pile."
        all[FamilyLibraryRefill] = new("Limited", "Destroy this card: Discard a face-up card in one of your provinces, refilling it with a discarded (not dead) Personality from your discard pile",
            "own-discarded-personality", ChoosesOwnProvince: true, Costs: [new(CostVerbs.Sacrifice)]);
        // Deeds and Words (oracle-l5r-1914): "Reaction: Before the resolution of an action during the Action Phase, destroy this card: Choose one of the Honor gains or losses from its effects. Reduce it to 0."
        all[DeedsReduce] = new("Reaction", "Before the resolution of an action during the Action Phase, destroy this card: Choose one of the Honor gains or losses from its effects. Reduce it to 0", "none",
            Trigger: HonorChangeReactions.Trigger, Costs: [new(CostVerbs.Sacrifice)]);
        return all;
    }
}

/// <summary>Deeds and Words' marks: the Honor changes ("turn|action key#occurrence") it will reduce to 0 when the held command is run again.</summary>
public sealed class IvoryHoldingsState
{
    public List<string> ReducedHonorChanges { get; set; } = [];
}

public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1200)]
    public IvoryHoldingsState IvoryHoldings { get; set; } = new();
}

public sealed partial class MatchEngine
{
    private const string VastPaddyPaidKey = "vast-paddy-fields:paid";

    // ---- execution -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunIvoryHoldings(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case IvoryHoldingsAbilities.JiramuStraighten:
            case IvoryHoldingsAbilities.HachimanStraighten:
                Straighten(target!);
                return false;
            case IvoryHoldingsAbilities.DojoBoost:
                ApplyModifier(ActionPerformer(ability, command, source), source!.Id, id, "Force", 2);
                return false;
            case IvoryHoldingsAbilities.RicePaddyBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case IvoryHoldingsAbilities.FamilyLibraryRefill:
                RefillProvinceWithDiscardedPersonality(player, target!, command.Province!.Value, Definition(source!).Name);
                return false;
            case IvoryHoldingsAbilities.DeedsReduce:
                if (_state.PendingHonorChange is { } held)
                {
                    var marks = _state.IvoryHoldings.ReducedHonorChanges;
                    marks.RemoveAll(m => !m.StartsWith($"{_state.TurnNumber}|", StringComparison.Ordinal)); // a mark of an earlier turn is stale
                    marks.Add(DeedsMark(held.ActionKey, held.Occurrence));
                    Emit("honorChangeChosen", $"{Definition(source!).Name}: the Honor {(held.Amount > 0 ? "gain" : "loss")} of {Math.Abs(held.Amount)} for {Player(held.PlayerId).Name} ({held.Reason}) will be reduced to 0.", player.Id, source!.Id);
                }
                return false;
            default:
                return null;
        }
    }

    // ---- validation -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateIvoryHoldings(string id, GameCommand command, PlayerState player)
    {
        if (id != IvoryHoldingsAbilities.FamilyLibraryRefill) return;
        // "a discarded (not dead) Personality from your discard pile": Scavenging Party's target is any discarded Personality, a dead one included.
        if (command.TargetId is { } targetId && _state.Cards.TryGetValue(targetId, out var personality))
            Require(personality.Zone != Zone.Dead, "Choose a discarded Personality that is not dead.");
        // "a face-up card in one of your provinces": ChoosesOwnProvince only asks that the province hold a card.
        if (command.Province is int index && player.Provinces.FirstOrDefault(p => p.Index == index && !p.Destroyed) is { CardId: { } provinceCardId } province)
        {
            Require(Card(provinceCardId).FaceUp, "Choose one of your provinces holding a face-up card.");
            Require(!province.HoldsNoDynastyCards, "That province holds no Dynasty cards.");
        }
    }

    // Rice Paddy: an attacking Follower (a Follower whose Personality is in the Attacker's army) at the current battlefield, with the acting player the Defender there.
    private bool IvoryHoldingsTargetAllows(string category, PlayerState player, CardInstance target) =>
        category == IvoryHoldingsAbilities.AttackingFollowerAtOwnProvince
        && Definition(target).Type == CardType.Follower && target.Zone == Zone.Attached && target.AttachedTo is { } hostId
        && Card(hostId).OwnerId == _state.ActivePlayerId && player.Id != _state.ActivePlayerId && AtBattle(target);

    // ---- static: Merchant Atoll -----------------------------------------------------------------------------------------------------------------------------------------------

    private int IvoryHoldingsProvinceBonus(CardInstance card, bool mine) =>
        mine && Definition(card).Effect == IvoryHoldingsAbilities.MerchantAtollEffect ? 1 : 0;

    // ---- Vast Paddy Fields ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // Pay's note: a Farm Holding recruited from a province, with a Vast Paddy Fields among the sources that paid for it. The province and slot it leaves ride on the recruited card.
    private void RecordVastPaddyPayment(string[] sourceIds, PaymentSubject? paying)
    {
        if (paying is not { Kind: PaymentKind.Recruit } || Definition(paying.Card).Type != CardType.Holding || paying.Card.Zone != Zone.Province || !Keyword(paying.Card, "Farm")) return;
        if (!sourceIds.Any(id => _state.Cards.TryGetValue(id, out var source) && InPlay(source) && Definition(source).Effect == IvoryHoldingsAbilities.VastPaddyEffect)) return;
        var (province, slot) = FindProvinceSlot(Player(paying.Card.OwnerId), paying.Card.Id);
        paying.Card.AbilityUses[VastPaddyPaidKey] = province.Index * 2 + slot + 1;
    }

    // ApplyMandatoryEntryEffects' hook: the province the Farm Holding was recruited from has refilled by now (face-down); the card it holds is turned face-up.
    private void ApplyVastPaddyEntry(int playerId, CardInstance card)
    {
        if (!card.AbilityUses.Remove(VastPaddyPaidKey, out var packed) || _state.LastProvinceEntryCardId != card.Id) return;
        var owner = Player(playerId);
        var province = owner.Provinces.FirstOrDefault(p => p.Index == (packed - 1) / 2);
        if (province is null || province.Destroyed) return;
        var refillId = (packed - 1) % 2 == 0 ? province.CardId : province.CardId2;
        if (refillId is null || !_state.Cards.TryGetValue(refillId, out var refill) || refill.Zone != Zone.Province || refill.FaceUp) return;
        refill.FaceUp = true;
        Emit("provinceRefilled", $"Vast Paddy Fields: {owner.Name}'s province {province.Index + 1} was refilled face up with {Definition(refill).Name}.", owner.Id, refill.Id);
    }

    // ---- Deeds and Words ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private string DeedsMark(string actionKey, int occurrence) => $"{_state.TurnNumber}|{actionKey}#{occurrence}";

    // HonorChangeReactionApplies' hook: null for every other ability. "During the Action Phase": Phase.Action (Limited and Open actions, Strategies played then).
    private bool? IvoryHoldingsHonorChangeApplies(CardInstance card, string abilityId, PendingHonorChange change) =>
        abilityId == IvoryHoldingsAbilities.DeedsReduce ? _state.Phase == Phase.Action && change.Amount != 0 && InPlay(card) : null;

    // GainHonor's hook: the change just offered (OfferHonorChangeWindow's record) is the one Deeds and Words was played on; its mark is spent as it is reduced.
    private bool DeedsAndWordsReduces(int playerId, int amount, string reason)
    {
        if (_p3LastHonorChange is not { } change || _state.IvoryHoldings.ReducedHonorChanges.Count == 0) return false;
        if (!_state.IvoryHoldings.ReducedHonorChanges.Remove(DeedsMark(change.Key, change.Occurrence))) return false;
        Emit("honorChangeReduced", $"{Player(playerId).Name}'s Honor {(amount > 0 ? "gain" : "loss")} of {Math.Abs(amount)} from {reason} was reduced to 0 (Deeds and Words).", playerId);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization. Priorities 11000-11099 are this batch's band.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterIvoryHoldingsHooks()
    {
        EngineHooks.Abilities.Register("AddIvoryHoldings", 11000, static abilities => abilities.AddIvoryHoldings());
        EngineHooks.RunEffects.Register("RunIvoryHoldings", 11000, static (engine, context) => engine.RunIvoryHoldings(context.id, context.ability, context.command, context.player, context.source, context.target));
        EngineHooks.ValidateLate.Register("ValidateIvoryHoldings", 11000, static (engine, context) => engine.ValidateIvoryHoldings(context.id, context.command, context.player));
        EngineHooks.TargetAllows.Register("IvoryHoldingsTargetAllows", 11000, static (engine, context) => engine.IvoryHoldingsTargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.ProvinceBonus.Register("IvoryHoldingsProvinceBonus", 11000, static (engine, context) => engine.IvoryHoldingsProvinceBonus(context.card, context.mine));
        EngineHooks.BowsTarget.Register("IvoryHoldingsBowsTarget", 11000, static context => context.abilityId == IvoryHoldingsAbilities.RicePaddyBow);
    }
}
