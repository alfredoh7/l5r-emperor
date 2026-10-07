using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P1 (issue #695, docs/decisions/2026-10-07-695-p1-payment-hooks.md): the payment pipeline's hook channels and stores.
//
// Before this file every Gold source that was not a card's own printed production was a hard-coded branch of PaymentTokens/UsePaymentTokens (Zlkyt, Moto Rani, the Reaction tokens, then one file's
// worth of ad hoc kinds each: Jade Mine, Coin Pouch, Near Miss, Profits, Bamboo Harvesters), production was a printed number or a ConditionalProduction with a fixed amount, no card could lend its
// production to the other player, a Strategy in play or an attachment could not produce, and "after you pay" was three hard-coded record calls in Pay. This file is where a card says all of that.
//
//   1. PAYMENT TOKENS       EngineHooks.PaymentTokens: extra payment sources ("<card>#<kind>", opaque to the client) a card offers for a payment. A token may BOW its card as it is used, be in the Hand or Attached
//                           (AllowedZones), run OnUse (discard a hand Strategy as it pays, discard a card from play), and require something of the OTHER sources chosen beside it (RequiresOtherSources).
//   2. PRODUCTION CONDITION EngineHooks.ProductionCondition answers a ConditionalProduction's condition (ProductionCondition.Custom, or overrides any); ConditionalProduction.AmountOf makes the amount a
//                           function of what is being paid for; EngineHooks.ConditionalProductions registers a card's ConditionalProduction by card (the catalogue's switch stays untouched).
//   3. PRODUCTION ADJUSTMENT EngineHooks.ProductionAdjustment: an additive per-card adjustment to what a card produces, for every card (the Stronghold too); AddProductionTurnModifier stores a PLAYER-level
//                           modifier for the turn that also holds for Holdings that enter play later in it.
//   4. LENT PRODUCERS       LendProducer: another player's unbowed Holding is a Gold producer for the borrower until end of turn (Producers, ValidatePayment, CardView.GoldProduced). Gold only: no control change.
//   5. AFTER PAYMENT        EngineHooks.AfterPayment, called at the end of Pay; the three record calls Pay used to make now register on it from their own files. A per-player per-phase spent-Gold ledger and a
//                           "produced Gold this turn" mark per card are kept here as state (GoldSpentThisPhase, ProducedGoldThisTurn).
//   6. WIDER PRODUCERS      Producers also lists an Attached card (an Item with a PaymentProduction) and an in-play Strategy, but only one whose definition has a production rule.

/// <summary>What a token hook is asked: who is paying and for what (null for a payment with no subject).</summary>
internal sealed record PaymentTokenContext(int PlayerId, PaymentSubject? Paying);

/// <summary>A production condition being judged: the producing card (null when it is not known), its ConditionalProduction and what is being paid for (null for a payment with no subject).</summary>
internal sealed record ProductionConditionContext(CardInstance? Card, ConditionalProduction Production, PaymentSubject? Paying);

/// <summary>A card's production being priced: the card, the player paying (the borrower for a lent producer) and the subject.</summary>
internal sealed record ProductionAdjustmentContext(CardInstance Card, int PayerId, PaymentSubject? Paying);

/// <summary>A payment that was just made. SourceIds is everything chosen (tokens included), ProducerIds the card ids among them, Tokens the tokens chosen.</summary>
internal sealed record AfterPaymentContext(int PlayerId, int Cost, int Produced, PaymentSubject? Subject, string[] SourceIds, string[] ProducerIds, IReadOnlyList<MatchEngine.PaymentToken> Tokens);

internal static partial class EngineHooks
{
    /// <summary>Extra payment sources a card offers for a payment (see PaymentHooks695.cs).</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, PaymentTokenContext, IEnumerable<MatchEngine.PaymentToken>?>> PaymentTokens = new();
    /// <summary>Answers a conditional production's condition: null when unhandled, true or false when decided.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, ProductionConditionContext, bool?>> ProductionCondition = new();
    /// <summary>A card's ConditionalProduction, registered by a feature rather than by the catalogue's switch; null when the card has none.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, CardInstance, ConditionalProduction?>> ConditionalProductions = new();
    /// <summary>An additive Gold adjustment to one card's production (negative reduces); summed with every other.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, ProductionAdjustmentContext, int>> ProductionAdjustment = new();
    /// <summary>Called at the end of every Pay.</summary>
    internal static readonly OrderedHooks<Action<MatchEngine, AfterPaymentContext>> AfterPayment = new();
}

/// <summary>Which cards a player-level production modifier applies to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductionScope
{
    /// <summary>Every card the player has that produces Gold.</summary>
    AnyCard,
    /// <summary>The player's Holdings.</summary>
    Holdings,
    /// <summary>The player's Holdings without the Unique keyword.</summary>
    NonUniqueHoldings,
    /// <summary>The player's Stronghold.</summary>
    Stronghold
}

/// <summary>A Holding that produces Gold for BorrowerId (not its owner) during the turn Turn.</summary>
public sealed record LentProducer(string CardId, int BorrowerId, int Turn);

/// <summary>A production modifier for the turn Turn: PlayerId's cards in Scope (every player's when null) produce Amount more (negative: less) Gold. It is read live, so it holds for a Holding that enters later.</summary>
public sealed record ProductionTurnModifier(int? PlayerId, int Amount, ProductionScope Scope, int Turn, string? SourceId = null);

public sealed partial class MatchState
{
    // 22300-22309 is reserved for P1 (issue #695).
    [JsonPropertyOrder(22300)]
    public List<LentProducer> LentProducers { get; set; } = [];
    [JsonPropertyOrder(22301)]
    public List<ProductionTurnModifier> ProductionTurnModifiers { get; set; } = [];
    /// <summary>Gold paid by each player in each phase for each kind of payment: "turn:phase:player:kind" to the total (older turns are dropped as new entries are written).</summary>
    [JsonPropertyOrder(22302)]
    public Dictionary<string, int> GoldSpentLedger { get; set; } = [];
    /// <summary>The last turn on which each card (by id) produced Gold for a payment.</summary>
    [JsonPropertyOrder(22303)]
    public Dictionary<string, int> GoldProducedTurns { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // ---- 1. payment tokens ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The zones a hook token's card may be in unless the token names its own (the Hand and the discard piles are never assumed).
    private static readonly Zone[] DefaultPaymentTokenZones = [Zone.Home, Zone.Stronghold, Zone.Battlefield, Zone.Celestial, Zone.Attached, Zone.EventInPlay];

    /// <summary>Every token the registered hooks offer, kept only when its card is in an allowed zone, is the payer's (or lent to the payer) and, for a token that bows, is unbowed. A hand card is only ever
    /// the payer's own, so no hook can offer one player's hand to the other.</summary>
    private IEnumerable<PaymentToken> HookPaymentTokens(int playerId, PaymentSubject? paying)
    {
        var context = new PaymentTokenContext(playerId, paying);
        foreach (var hook in EngineHooks.PaymentTokens.Items)
            foreach (var token in hook(this, context) ?? [])
            {
                if (!(token.AllowedZones ?? DefaultPaymentTokenZones).Contains(token.Card.Zone)) continue;
                if (!ProducerControlledBy(token.Card, playerId)) continue;
                if (token.Bows && token.Card.Bowed) continue;
                yield return token;
            }
    }

    /// <summary>The first reason the chosen sources do not satisfy a token's own conditions, or null: a token that bows is not also its card's own bow, and RequiresOtherSources holds of the whole choice.</summary>
    private string? PaymentTokenConditionError(string[] ids, int playerId, IEnumerable<PaymentToken> available)
    {
        var tokens = available as IReadOnlyCollection<PaymentToken> ?? [.. available];
        foreach (var id in ids.Where(IsPaymentToken))
        {
            if (tokens.FirstOrDefault(t => t.Id == id) is not { } token) continue;
            var name = Definition(token.Card).Name;
            if (token.Bows && ids.Contains(token.Card.Id, StringComparer.Ordinal)) return $"{name} bows only once in a payment: choose one of its ways to produce.";
            if (token.RequiresOtherSources is { } requires && !requires(this, playerId, ids)) return token.RequirementMessage ?? $"{name}'s Gold cannot be used with the other sources of this payment.";
        }
        return null;
    }

    // ValidatePayment's and ValidateSplitPayment's step, after RequirePaymentTokens.
    private void RequirePaymentTokenConditions(string[] ids, int playerId, PaymentSubject? paying)
    {
        if (!ids.Any(IsPaymentToken)) return;
        var error = PaymentTokenConditionError(ids, playerId, PaymentTokens(playerId, paying));
        Require(error is null, error ?? "");
    }

    // ---- 2. production conditions ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ProductionConditionMet's first step: a registered hook that decides the condition.
    private bool? PaymentHookCondition(ConditionalProduction production, PaymentSubject? paying, CardInstance? card)
    {
        var hooks = EngineHooks.ProductionCondition.Items;
        if (hooks.Length == 0) return null;
        var context = new ProductionConditionContext(card, production, paying);
        foreach (var hook in hooks)
            if (hook(this, context) is { } answer) return answer;
        return null;
    }

    // ConditionalProductionOf's last step: a feature's own registration.
    private ConditionalProduction? PaymentHookConditionalProduction(CardInstance card)
    {
        foreach (var hook in EngineHooks.ConditionalProductions.Items)
            if (hook(this, card) is { } found) return found;
        return null;
    }

    // The amount a met conditional production gives: a function of what is being paid for when it has one, else its fixed Amount.
    private int ConditionalAmount(ConditionalProduction production, PaymentSubject? paying) =>
        production.AmountOf is { } amountOf && paying is not null ? amountOf(this, paying) : production.Amount;

    // ---- 3. production adjustment ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ProductionScopeMatches(ProductionScope scope, CardInstance card) => scope switch
    {
        ProductionScope.AnyCard => true,
        ProductionScope.Holdings => Definition(card).Type == CardType.Holding,
        ProductionScope.NonUniqueHoldings => Definition(card).Type == CardType.Holding && !Keyword(card, "Unique"),
        ProductionScope.Stronghold => Definition(card).Type == CardType.Stronghold,
        _ => false
    };

    /// <summary>The sum of the player-level modifiers for the current turn that apply to the card (the owner's, or every player's).</summary>
    internal int ProductionTurnAdjustment(CardInstance card)
    {
        var sum = 0;
        foreach (var modifier in _state.ProductionTurnModifiers)
            if (modifier.Turn == _state.TurnNumber && (modifier.PlayerId is null || modifier.PlayerId == card.OwnerId) && ProductionScopeMatches(modifier.Scope, card)) sum += modifier.Amount;
        return sum;
    }

    /// <summary>Stores a production modifier for the rest of the turn: "all non-Unique Holdings produce 1 less Gold this turn" is AddProductionTurnModifier(playerId, -1, ProductionScope.NonUniqueHoldings, source).
    /// Holdings that enter play later this turn are affected too (it is read live).</summary>
    internal void AddProductionTurnModifier(int? playerId, int amount, ProductionScope scope, CardInstance? source = null, string? note = null)
    {
        _state.ProductionTurnModifiers.RemoveAll(m => m.Turn != _state.TurnNumber);
        _state.ProductionTurnModifiers.Add(new(playerId, amount, scope, _state.TurnNumber, source?.Id));
        if (note is not null) Emit("productionModified", note, playerId ?? source?.OwnerId ?? _state.ActivePlayerId, source?.Id);
    }

    // GoldProduced's term: the turn modifiers plus every registered adjustment, for any card (a Holding, the Stronghold, an attachment).
    private int PaymentHookAdjustment(CardInstance card, int payerId, PaymentSubject? paying)
    {
        var sum = ProductionTurnAdjustment(card);
        var hooks = EngineHooks.ProductionAdjustment.Items;
        if (hooks.Length == 0) return sum;
        var context = new ProductionAdjustmentContext(card, payerId, paying);
        foreach (var hook in hooks) sum += hook(this, context);
        return sum;
    }

    // ---- 4. lent producers ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The player a card's Gold is lent to this turn, or null.</summary>
    internal int? LentBorrower(CardInstance card)
    {
        foreach (var lent in _state.LentProducers)
            if (lent.Turn == _state.TurnNumber && lent.CardId == card.Id) return lent.BorrowerId;
        return null;
    }

    /// <summary>The player who may use the card as a Gold producer: its owner, or the borrower while it is lent (and then not its owner).</summary>
    internal bool ProducerControlledBy(CardInstance card, int playerId) => LentBorrower(card) is int borrower ? borrower == playerId : card.OwnerId == playerId;

    /// <summary>Lends another player's unbowed Holding that is in play to the borrower until the end of the turn: it is a Gold producer for the borrower and no longer for its owner. Gold only; nothing about
    /// control changes. False (and nothing changed) when the card is not such a Holding.</summary>
    internal bool LendProducer(CardInstance holding, int borrowerId, string? note = null)
    {
        if (holding.OwnerId == borrowerId || holding.Bowed || !InPlay(holding) || Definition(holding).Type != CardType.Holding) return false;
        _state.LentProducers.RemoveAll(l => l.Turn != _state.TurnNumber || l.CardId == holding.Id);
        _state.LentProducers.Add(new(holding.Id, borrowerId, _state.TurnNumber));
        if (note is not null) Emit("producerLent", note, borrowerId, holding.Id);
        return true;
    }

    /// <summary>Discards a card as it pays (a hand Strategy, a card in play): the Fate discard pile, or the Dynasty discard pile for a Dynasty card. For a token's OnUse.</summary>
    internal void PaymentDiscard(CardInstance card, int playerId)
    {
        var name = Definition(card).Name;
        if (card.Zone == Zone.Hand) { DiscardFate(Player(card.OwnerId), card); return; }
        if (card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host)) { RemoveAttachmentGrants(host, card.Id); card.LastHostId = host.Id; }
        ResetCardState(card);
        card.Zone = IsDynastyCardType(Definition(card).Type) ? Zone.DynastyDiscard : Zone.FateDiscard;
        card.AttachedTo = null; card.ProvinceIndex = null; card.FaceUp = true; card.Tokens.Clear();
        Emit("cardDiscarded", $"{name} was discarded to produce Gold.", card.OwnerId, card.Id);
    }

    // Test seams (L5R.Core.Tests has InternalsVisibleTo).
    internal IEnumerable<CardInstance> LiveCardsForTest => _state.Cards.Values;
    internal string ClanOfForTest(int playerId) => Player(playerId).Clan;

    // ---- 6. wider producers ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Producers' zone test: the Home, Stronghold and Battlefield as ever; an Attached card or an in-play Strategy too (Producers still requires the definition to have a production rule).
    private static bool ProducerZone(CardInstance card, CardDefinition definition) =>
        card.Zone is Zone.Home or Zone.Stronghold or Zone.Battlefield || card.Zone == Zone.Attached || card.Zone == Zone.EventInPlay && definition.Type == CardType.Strategy;

    // ---- 5. after payment and the ledger --------------------------------------------------------------------------------------------------------------------------------------------------------

    // Pay's last step: the registered callbacks (the ledger below, and the entry records of Moneylender, Vast Paddy Fields and Easy Persuasion).
    private void AfterPaymentHooks(int playerId, int cost, int produced, PaymentSubject? paying, string[] sourceIds, string[] producerIds, IReadOnlyList<PaymentToken> tokens)
    {
        var hooks = EngineHooks.AfterPayment.Items;
        if (hooks.Length == 0) return;
        var context = new AfterPaymentContext(playerId, cost, produced, paying, sourceIds, producerIds, tokens);
        foreach (var hook in hooks) hook(this, context);
    }

    private string LedgerKey(int playerId, Phase phase, string kind) => $"{_state.TurnNumber}:{(int)phase}:{playerId}:{kind}";

    // The Gold paid in the current turn by the player, by phase (null: every phase) and by what it paid for: a Recruit, an Equip, a Play, an Action, or "None" (a payment with no subject).
    private int LedgerSum(int playerId, Phase? phase, Func<string, bool> kindMatches) =>
        _state.GoldSpentLedger.Where(e =>
        {
            var parts = e.Key.Split(':');
            return parts.Length == 4 && parts[0] == _state.TurnNumber.ToString() && parts[2] == playerId.ToString() && (phase is null || parts[1] == ((int)phase.Value).ToString()) && kindMatches(parts[3]);
        }).Sum(e => e.Value);

    /// <summary>The Gold the player has paid in this phase of this turn, for every kind of payment, or only for the given kind (a Recruit, an Equip, a Play, an Action).</summary>
    internal int GoldSpentThisPhase(int playerId, PaymentKind? kind = null) => LedgerSum(playerId, _state.Phase, k => kind is null || k == kind.Value.ToString());

    /// <summary>The Gold the player has paid in this phase of this turn for anything that is not a Recruit ("spent 5 total Gold for non-Recruit costs or payments").</summary>
    internal int GoldSpentThisPhaseExceptRecruit(int playerId) => LedgerSum(playerId, _state.Phase, k => k != nameof(PaymentKind.Recruit));

    /// <summary>The Gold the player has paid so far this turn, in every phase.</summary>
    internal int GoldSpentThisTurn(int playerId) => LedgerSum(playerId, null, _ => true);

    /// <summary>The card produced Gold for a payment this turn (its own production or one of its tokens).</summary>
    internal bool ProducedGoldThisTurn(CardInstance card) => _state.GoldProducedTurns.TryGetValue(card.Id, out var turn) && turn == _state.TurnNumber;

    private void RecordPaymentLedger(AfterPaymentContext context)
    {
        if (context.Cost > 0)
        {
            foreach (var key in _state.GoldSpentLedger.Keys.Where(k => !k.StartsWith($"{_state.TurnNumber}:", StringComparison.Ordinal)).ToArray()) _state.GoldSpentLedger.Remove(key);
            var ledgerKey = LedgerKey(context.PlayerId, _state.Phase, context.Subject?.Kind.ToString() ?? "None");
            _state.GoldSpentLedger[ledgerKey] = (_state.GoldSpentLedger.TryGetValue(ledgerKey, out var before) ? before : 0) + context.Cost;
        }
        foreach (var id in context.ProducerIds) if (_state.Cards.ContainsKey(id)) _state.GoldProducedTurns[id] = _state.TurnNumber;
        foreach (var token in context.Tokens) if (token.Amount > 0) _state.GoldProducedTurns[token.Card.Id] = _state.TurnNumber;
        foreach (var key in _state.GoldProducedTurns.Where(e => e.Value != _state.TurnNumber).Select(e => e.Key).ToArray()) _state.GoldProducedTurns.Remove(key);
    }

    // Registration only: callbacks are not invoked during module initialization. Priorities 16300-16399 are P1's.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPaymentHooks695()
    {
        // The entry record Easy Persuasion reads (EmperorP3Entries.cs, issue #503) was a call in Pay; it is registered here, not in its own file, which already has formatting debt on main.
        EngineHooks.AfterPayment.Register("P1AfterPaymentEntry", 16302, static (engine, context) => engine.P3RecordEntryPayment(context.Cost, context.Subject));
        EngineHooks.AfterPayment.Register("P1PaymentLedger", 16310, static (engine, context) => engine.RecordPaymentLedger(context));
    }
}
