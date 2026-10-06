# P2: entry and ability cost terms (issue #682)

Status: **built** (engine only; no card is wired by this change). Lane: rules. Source: primitive P2 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668, corrected in #671).

## What it is

Two registered channels and one cost verb, in `src/L5R.Core/CostTerms.cs`:

| Piece | Shape | Read by |
|---|---|---|
| `EngineHooks.EntryCostTerms` | `Func<EntryCostContext, IEnumerable<CostTerm>?>`, one call per Recruit / Reserve recruit and per attachment entering play | `QuotePurchase` (after the existing terms), `EquipGoldCost` |
| `EngineHooks.AbilityCostTerms` | `Func<AbilityCostContext, IEnumerable<CostTerm>?>`, one call per priced ability and per Strategy played with `playStrategy` | `AbilityGoldCost` (after the rule's own price), `QuoteStrategyGold` / `PlayStrategy` |
| `CostVerbs.PayGold` with `CostOption.Gold` | one ability, several ways to pay, one of them Gold ("Bow Daito or pay 4 Gold") | `BaseAbilityGoldCost` reads the chosen option's rule; `ValidateCostOption` / `PayCostOption` skip the verb (it names no card) |

`CostTerm(Amount, Label, IgnoreCost = false, WielderDependent = false)`: `Amount` is signed Gold (a discount is negative). The sum of all terms is applied and the price floored at zero. An `IgnoreCost` term makes the whole price zero whatever else applies; its label becomes the basis. A player-chosen (Variable) amount and a price still waiting for a target are never adjusted. A term never creates a price for a free option.

Helpers a term can read (`MatchEngine`, internal): `ControlsRing(player, title)`, `IsDefendingPlayer(player)`, `OpponentIsClan(player, clan)`, `PrintedGoldCost(card)`.

A feature file registers a callback from its module initializer, e.g.

```csharp
EngineHooks.EntryCostTerms.Register("MyFeatureEntry", 12000, static context =>
    context.Card.DefinitionId == MyCard && context.Engine.ControlsRing(context.PayerId, "Ring of Earth") ? [new CostTerm(0, "controls the Ring of Earth", IgnoreCost: true)] : null);
```

## Not changed

- The existing hard-coded terms (`ClanEntryDiscount`, `KeywordEntryDiscount`, `HoldingEntryDiscount`, `RoninEntryDiscount`, ...) stay where they are; they were not migrated onto the channel (no behaviour gain, a larger regression surface). New cards use the channel.
- An Experienced overlay and an Invest Reaction's fixed cost do not read the channel (Gates of Chaos supplement: Invest costs are not discounted unless the card says so).
- Kitsuki Judgment's "pay Gold equal to his unit's total Gold Cost, +8 unless ..." is P24's; `PayGold` is the verb it will use.

## Readings for Mendon to confirm or correct

- **Defender** is "a battle is under way and the player is not the Attacker (the active player)". A Reserve recruit made as an absent Battle action while defending, and an ordinary recruit made while a battle is current, both count.
- **"Controls the Ring of X"** is a Ring of that exact title, in play, owned by the player. A "False Ring of X" is a different title and does not count.
- **"Actions on your Strategies cost 1 less"** (Mendon, 2026-10-06): both playing a Strategy and any Strategy ability. Published Strategies are played through `useAbility` with the implicit printed-cost rule, so `AbilityCostTerms` reaches them; Terrains and scenario Strategies played with `playStrategy` read it through `StrategyPlayCost`.

## Tests

`tests/L5R.Core.Tests/CostTermsTests.cs` (suite `cost-terms`, test-only callbacks that are inert unless a test sets a mode): a discount shown in the basis, the floor at zero, a surcharge, a waiver, the Defender term in a battle and not in the Dynasty phase (and on a Reserve recruit), an Equip term and a recruit-only term's isolation, an ability term and its floor, a free option staying free, PayGold paid with chosen sources (nothing bowed) and its free sibling bowing its subject, and an unpayable PayGold rejected without a state change.
