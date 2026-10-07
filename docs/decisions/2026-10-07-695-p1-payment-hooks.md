# P1: payment pipeline hooks (issue #695)

Status: **built** (engine only; no card is wired, no content wave, no scan). Lane: rules. Source: primitive P1 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668), generalising the ad hoc tokens of `2026-10-05-667-payments.md`. Branch `claude/695-p1`. All code is in `src/L5R.Core/PaymentHooks695.cs` plus the small shared edits listed at the end; tests are `tests/L5R.Core.Tests/PaymentHooks695Tests.cs` (label `payment-hooks-695`, fixtures `training-p1-*` as per-instance `CardInstance.Printed`).

## What was built

Six pieces. Every channel is an `OrderedHooks` registered from a module initializer (ids start with `P1`, priorities 16300-16399); every new store is a `MatchState` property with `JsonPropertyOrder` 22300-22303.

| # | Piece | Entry point |
|---|---|---|
| 1 | Extra payment sources | `EngineHooks.PaymentTokens` (`Func<MatchEngine, PaymentTokenContext, IEnumerable<MatchEngine.PaymentToken>?>`) |
| 2 | Production conditions and amounts as a function of the payment subject | `EngineHooks.ProductionCondition`, `ProductionCondition.Custom`, `ConditionalProduction.AmountOf`, `EngineHooks.ConditionalProductions` |
| 3 | Additive production adjustment, and a player-level turn modifier | `EngineHooks.ProductionAdjustment`, `AddProductionTurnModifier` |
| 4 | Lent producers | `LendProducer`, `LentBorrower`, `ProducerControlledBy` |
| 5 | After-payment hook, spent-Gold ledger, "produced Gold this turn" | `EngineHooks.AfterPayment`, `GoldSpentThisPhase`, `GoldSpentThisTurn`, `ProducedGoldThisTurn` |
| 6 | Wider producer zones | `Producers` (Attached, in-play Strategy), `GoldSourceProduction` |

### 1. Payment tokens

`PaymentToken` (the record in `EmperorMechE.cs`, now `internal`) gained five optional members:

| Member | Meaning |
|---|---|
| `Bows` | the engine bows `Card` when the token is used (before `OnUse`). A token that bows is dropped while its card is bowed, and is refused beside its own card's bow (`"X bows only once in a payment"`); the quote lists the card id in its `ConflictingSourceIds`. |
| `RequiresOtherSources` | `Func<MatchEngine, int playerId, string[] allChosenIds, bool>`. Checked by `ValidatePayment` and `ValidateSplitPayment` (new `RequirePaymentTokenConditions`), with `RequirementMessage` (or a default) as the refusal. The suggester honours it twice: groups holding such a token are searched last and a combination is kept only if the sources already in it satisfy the token, and a final post-filter (`PaymentTokenConditionError`) drops anything that still fails. |
| `OnUse` | `Action<MatchEngine, PaymentToken, int playerId>`, run by `UsePaymentTokens` after the bow. `engine.PaymentDiscard(card, playerId)` discards a hand card or a card in play (Fate or Dynasty pile by type) for the common cases. |
| `AllowedZones` | zones the token's card may be in. Default (`Home, Stronghold, Battlefield, Celestial, Attached, EventInPlay`): the Hand is never assumed, a token that wants it says `[Zone.Hand]`. The engine also keeps a token only when the payer is its card's owner (or its borrower), so no hook can offer one player's hand to the other. |
| `RequirementMessage` | the refusal text. |

Ids stay an opaque string grammar: `"<card id>#<kind>"`. New kinds use a new `#suffix`. `Kind` is the string the client shows; **new kind strings for the client: `"hand"` (a hand card discarded as it pays) and `"bow-reaction"` (a card bowed as a Reaction beside other sources).** Nothing else changes in `PaymentSource`.

```csharp
// "When another Holding produces Gold to pay for a Nonhuman, bow this card: produce 1" (Ever-Winter Copse)
EngineHooks.PaymentTokens.Register("MyCopse", 12000, static (engine, context) => engine.CopseTokens(context));
private IEnumerable<PaymentToken> CopseTokens(PaymentTokenContext context)
{
    if (context.Paying is not { Kind: not PaymentKind.Action } subject || !Keyword(subject.Card, "Nonhuman")) yield break;
    foreach (var card in _state.Cards.Values.Where(c => c.OwnerId == context.PlayerId && InPlay(c) && Definition(c).Effect == CopseEffect))
        yield return new(card.Id + "#copse", card, CopseAbility, 1, "bow-reaction", "Bow this card: produce 1 Gold", null, Bows: true,
            RequiresOtherSources: (e, _, ids) => ids.Any(id => id != card.Id && !IsPaymentToken(id) && Definition(Card(id)).Type == CardType.Holding));
}
// A hand Strategy that discards itself as it pays (Truce, Tsujiken's Coin shape): Kind "hand", AllowedZones: [Zone.Hand], OnUse: static (e, token, player) => e.PaymentDiscard(token.Card, player).
```

**Moved onto the channel with zero behaviour change** (their own files, ids `P1MechEPaymentTokens` 16300, `P1TfPaymentTokens` 16301, `P1PromoPaymentTokens` 16302, `P1WlPaymentTokens` 16303; the hard-coded branches in `PaymentTokens` and `UsePaymentTokens` are deleted and each token carries its `OnUse`): Zlkyt's destroy token and Moto Rani's discount token (`EmperorMechE.cs`), Jade Mine and Slave Pits (`TfHoldings.cs`), Coin Pouch, Near Miss, Profits reduce and gold (`PromoPaymentSources.cs`), Bamboo Harvesters (`WlHoldings.cs`). The Reaction tokens (`"<card>#reaction"`) are the base of `PaymentTokens` and are not moved (they are the engine's own data-driven source, not a feature's).

### 2. Production conditions

- `ProductionCondition.Custom` is decided only by an `EngineHooks.ProductionCondition` callback (`Func<MatchEngine, ProductionConditionContext(Card, Production, Paying), bool?>`); a hook may answer any condition (`null` is unhandled) and runs before the built-in switch. `ProductionConditionMet` now receives the producing card (optional).
- `ConditionalProduction(Amount, When, Keyword, Threshold, AmountOf)`: `AmountOf` is `Func<MatchEngine, PaymentSubject, int>`; used instead of `Amount` for a payment that has a subject. It must not quote a price (the quote asks for this production), so read the **printed** cost (`engine.PrintedGoldCost(subject.Card)`).
- `EngineHooks.ConditionalProductions` (`Func<MatchEngine, CardInstance, ConditionalProduction?>`) is the registration path for a card's `ConditionalProduction`, so the catalogue's switch is not edited; it is the last link of `ConditionalProductionOf`.

```csharp
EngineHooks.ConditionalProductions.Register("MyDojo", 12000, static (_, card) => card.DefinitionId == Dojo ? new(0, ProductionCondition.Custom, "my-dojo", AmountOf: static (e, s) => e.PrintedGoldCost(s.Card)) : null);
EngineHooks.ProductionCondition.Register("MyDojoCondition", 12000, static (e, c) => c.Production.Keyword == "my-dojo" ? c.Paying is { Kind: PaymentKind.Recruit } p && e.Keyword(p.Card, "Samurai") && !e.Keyword(p.Card, "Unique") : null);
```

### 3. Production adjustment

`EngineHooks.ProductionAdjustment` (`Func<MatchEngine, ProductionAdjustmentContext(Card, PayerId, Paying), int>`) is summed with the existing terms at the adjustment line of `GoldProduced`, for **every** card (the Stronghold, a Holding, an attachment); the result is floored at 0 as before. `AddProductionTurnModifier(playerId?, amount, ProductionScope, source?, note?)` stores a `ProductionTurnModifier` for the current turn (scopes `AnyCard`, `Holdings`, `NonUniqueHoldings`, `Stronghold`; a null player means every player); it is read live, so a Holding that enters play later this turn is affected, and it lapses when the turn number changes. `AddProductionTurnModifier(1, -1, ProductionScope.NonUniqueHoldings, source)` is Trade Embargo's whole effect.

### 4. Lent producers

`LendProducer(holding, borrowerId)` makes another player's unbowed Holding in play a Gold producer for the borrower until end of turn (`MatchState.LentProducers`, keyed by turn). `Producers` (and so `ValidatePayment`'s ownership check, `PaymentOptions` and the quote) lists it for the borrower and not for its owner while lent; `GoldProduced(card, borrower)` already prices from the payer's side (an adjustment hook sees `PayerId`); `CardView.GoldProduced` is the borrower's price. Gold only: nothing about control changes, the lender's other effects on the card are untouched, and the borrower's payment bows the card. Returns false (nothing changed) for a bowed card, a card the borrower owns, or anything that is not a Holding in play.

### 5. After payment, ledger and marks

`EngineHooks.AfterPayment` (`Action<MatchEngine, AfterPaymentContext(PlayerId, Cost, Produced, Subject, SourceIds, ProducerIds, Tokens)>`) is called at the end of `Pay`, after the bows, the tokens and the Honor loss. The three record calls `Pay` used to make now register on it (`P1AfterPaymentMoneylender` 16300 in `EmperorRemainingF.cs`, `P1AfterPaymentVastPaddy` 16301 in `IvoryHoldings.cs`, `P1AfterPaymentEntry` 16302 in `PaymentHooks695.cs`: `EmperorP3Entries.cs` already fails the changed-file format check on main, so it was left untouched), so the Moneylender, Vast Paddy Fields and Easy Persuasion tests are the regression for them. The feature's own ledger (`P1PaymentLedger`, 16310) keeps `MatchState.GoldSpentLedger` ("turn:phase:player:kind" to Gold paid, the kind being the subject's `PaymentKind` or `None`; older turns are dropped as new entries are written) and `MatchState.GoldProducedTurns` (card id to the last turn it produced for a payment; a card produced when it was a chosen source or when a chosen token of it added Gold). Read with `GoldSpentThisPhase(player, kind?)`, `GoldSpentThisPhaseExceptRecruit(player)`, `GoldSpentThisTurn(player)`, `ProducedGoldThisTurn(card)`.

### 6. Wider producers

`Producers` and `GoldSourceProduction` use `ProducerZone`: Home, Stronghold and Battlefield as before, plus an Attached card and a Strategy in the EventInPlay zone. The existing rule still gates all of them: the definition must have a printed production, a Moneylender-style amount, an alternate (`PaymentProduction`) source or a conditional production. A Strategy in the hand or in play with no production rule is not a producer.

## Which unwired cards each piece is for

| Card | Pieces |
|---|---|
| Truce | 1 (a `"hand"` token for a Personality of another Clan, `OnUse` = `PaymentDiscard`) |
| Ever-Winter Copse | 1 (`Bows`, `RequiresOtherSources`: another Holding among the sources, Nonhuman subject) |
| The Amethyst Court | 1 (its second ability is a token whose `Subject` is the Courtier or Artisan it bows, `OnUse` bows him; its first, ":bow:: Produce 2 Gold", is printed production) |
| Colonial Dojo | 2 (`ConditionalProductions` + `AmountOf`) |
| Suikihime's Chambers | 2 (`AmountOf`: 4 for a Unique, else 2) |
| Tsujiken's Coin | 6 (an attached Item with a printed or `PaymentProduction` rule) |
| Uncovering Treasures | 1 and 6 (it must discard itself as it pays: a token in the EventInPlay zone with `OnUse` = `PaymentDiscard`; piece 6 lists an in-play Strategy as a plain producer when nothing needs to happen as it pays) |
| Trade Embargo | 3 (`AddProductionTurnModifier`) |
| Opening the Veil | 3 (hook for the Stronghold) |
| Morito Inoue, Black Riders | 4 (`LendProducer`; the target category "another player's Holding with Gold Cost lower than ..." is `TargetAllows`) |
| Doji Kurohime | 5 (`ProducedGoldThisTurn`) |
| Journey's End Keep - exp | 5 (`GoldSpentThisPhaseExceptRecruit`), with P3 (the recruit gate) |

## Left out, and why

- **The Coin Pouch precondition stays where it was.** `RequirePromoPaymentSources` / `PromoPaymentOptionValid` (a Holding bowed in the same payment) still enforce the Pouch with their own wording; the Pouch token was not rewritten to `RequiresOtherSources`, because the existing tests pin the refusal text and the suggestion order. New cards use `RequiresOtherSources`.
- **The base Reaction tokens (`#reaction`) are not on the channel**; they are data-driven by `PaymentProduction`/`PaymentWhen`.
- **A token that needs other sources in a Discipline (two-part) payment** is validated against each half on its own by `Pay` (the existing Coin Pouch rule has the same limit); `ValidateSplitPayment` checks the whole choice.
- **No client edit.** The client renders `PaymentSource.Kind` generically; it should label `"hand"` ("discard from hand") and `"bow-reaction"` ("bow") if it does not already.
- **A lent Holding's `LoseHonorForProducers`/Temple effects** run for the paying player, as `Pay` always does (no card needs otherwise).
- Interactions with P3 (recruitment gates, the error chain of `QuotePurchase`) and P10 (keyword rules) are not touched.

## Readings for Mendon to confirm or correct

1. **"Produced Gold this turn" (Doji Kurohime)** is read as: the card was a chosen source of a payment this turn, or one of its tokens that added Gold was. Gold produced outside a payment (none exists today) would not count.
2. **A lent Holding is lent only while unbowed, for the whole turn, and is priced for the borrower** (an adjustment sees the borrower as payer). The owner cannot use it for his own payments while it is lent.
3. **The player-level turn modifier lapses at the turn number changing**, so it is "this turn" in the R-001 sense (end of the turn), not "this phase".
4. **`AmountOf` reads the subject's printed cost** (the only price that cannot recurse). "Enters play for 0 Gold" therefore produces the printed cost; a discounted or surcharged price still pays in full or loses the excess.
5. **A token that bows is not also its card's own bow** (two ways to bow one card in one payment are refused), as Jade Mine and Slave Pits already do.
6. **Per-phase ledger** counts the Gold of the cost of each payment (not the Gold produced), by the phase at the moment of payment. "Non-Recruit costs or payments" (Journey's End Keep) is read as every payment whose subject is not a Recruit, including payments with no subject and ability costs.
