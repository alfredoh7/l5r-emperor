# Generalized Gold payment: one quote, one panel

Status: accepted (Mendon, standing authority). Lane: shared (rules engine, wire contract, client). Issue #303.

## Decision

Every action that costs Gold is priced by **one** engine function and rendered by **one** client panel.

- The engine prices an action through a `GoldCostRule` (data on the ability, `AbilityDefinition.Cost`) interpreted in exactly one place, `MatchEngine.AbilityGoldCost` (`src/L5R.Core/GoldQuotes.cs`). Validation, offered payments, the quote sent to the client and the payment itself all read that one result.
- The result crosses to the client as a `GoldQuote` on `ActionOption` (`Gold`, plus `TargetGold` for target-derived costs). The client renders the quote and never computes a cost.
- The player-chosen amount of a variable cost travels as `GameCommand.GoldAmount`.
- The client has one payment panel (`RenderGoldPanel`, `src/L5R.Client/Main.Actions.cs`) used by `purchase`, `recruitReserve`, `equip`, `playStrategy` and every costed `useAbility`.

## Why

The cost of an ability was written down four times by hand and the copies had drifted (issue #303):

1. `ValidateAbility` had an `id == "..."` chain.
2. `AbilityActions` had a second parallel per-candidate chain that pre-filled `Command.SourceIds`.
3. Each ability's own `Pay(...)` in the execution switch re-hardcoded the amount.
4. The client's `RenderTargeted` re-derived the cost (`equip || Strategy ? GoldCost : 0`), against AGENTS.md ("Gold and payments stay in Core; the UI presents engine choices").

What the drift had cost, found by pinning behavior on unmodified `origin/main` (see Evidence):

| Symptom on `origin/main` | Cause |
| --- | --- |
| Kharmic was never offered enabled | chain 2 did not know its cost, and its `own-kharmic-card` targets (hand, face-up province) were never generated as candidates |
| A non-Strategy Discipline was offered disabled ("needs 2 Gold; selected sources produce 0") | chain 2 priced only Strategy sources |
| Invest / Singh Remnants reactions were offered enabled with **no sources**, so submitting them could only be rejected | the reaction-window options never priced anything |
| Togashi Noritada's Invest and a non-Strategy Discipline validated a payment and then **never bowed the sources** | chain 3 had no `Pay` for them |
| Equip options priced at the printed cost, ignoring Katagi's -4 | the option ignored the wielder-dependent term ordinary `Equip()` applied |
| No payment panel for anything but recruit/equip/Strategy | chain 4 |

## Cost-kind taxonomy

| Kind | Meaning | Rule bases | Quote shape |
| --- | --- | --- | --- |
| `fixed` | Printed on the card or fixed in the ability: the same whichever target is chosen | `fixed` (a constant), `source-printed` (source card's printed cost; implicit for a Strategy source and for a Discipline) | `ActionOption.Gold` |
| `derived` | Computed from the chosen target, plus a modifier and a floor | `target-printed` (another card's cost), `target-unit-total` (a Personality plus everything attached, or a single card's own cost), `target-recruit` (the province Personality's recruit quote, so every recruit discount applies) | `Gold` = first legal target's quote, `TargetGold` = one quote per legal target |
| `variable` | The player chooses the amount, `MinAmount..MaxAmount` | `variable` | `Gold` with `MinAmount`, `MaxAmount`, `PaymentsByAmount` |

`amount = basis + Modifier`, raised to `Floor` when one is given, never below 0. Each quote carries `Basis`, a human-readable derivation ("Training item cost 2 − 2 (min 1)", "Training lion veteran unit total 8 + 2 + 2 = 12 − 4 (min 0)"), plus `Payments` (the engine's suggested and alternative source sets, best first) and `Error`.

## Design details

- **`GoldCostRule`** is an immutable record with static factories (`Fixed`, `SourcePrinted`, `TargetPrinted`, `TargetUnitTotal`, `TargetRecruit`, `Variable`). The catalog carries one on every explicitly costed ability; Strategy sources and Disciplines fall back to `ImplicitGoldRule` so the many published Strategies need no edit.
- **Parity is a test, not a claim.** `GoldParityTests` pins the cost of every pre-existing rule using only the pre-existing public surface, so the same file runs against the old and the new engine (both pass; see Evidence).
- **Variable amounts.** `GoldAmount` null means the minimum. Validation requires `min <= amount <= max`, then `ValidatePayment(amount, sources)`. Excess production is lost exactly as in every other payment. A `GoldAmount` on a fixed or derived cost is rejected.
- **Per-target quotes.** Changing the target changes the cost, so every legal target ships its own quote and the client swaps quotes with no round trip, re-suggesting sources from the engine's own first payment for that target.
- **Pre-filled sources are kept.** `Command.SourceIds` still carries the first suggested payment, so `L5R.AI`, bots and existing tests are unchanged. The practice bot now also uses `TargetGold` when it retargets, so it never submits sources priced for another target.
- **One payment step.** `ExecuteAbilityEffects` calls `PayForAbility` once; the per-ability `Pay(...)` calls, and the one inside `BringProvincePersonalityIntoPlay`, are gone. Purchase, reserve, equip and `playStrategy` use the same quote shapes (`ToGoldQuote`, `EquipGoldCost`, `QuoteStrategyGold`).
- **Wire.** `WireGameCommand`, `AvailableAction` and the client's outgoing `MatchSession.ToWire` carry `GoldAmount`, `Gold` and `TargetGold`. `DeckPosition` and `TargetPlayerId` were silently dropped online before this change; they are carried now, and a reflection test in the server and transport suites fails when any future `GameCommand` field is missing from the wire.

## The seven cards

Only Paid Off is wired today. The other six are **not wired by this change** (out of scope, recorded here so it is not silently expanded); each would be a catalog line plus its own effect.

| Card | Cost kind | How it plugs in |
| --- | --- | --- |
| Paid Off (oracle-l5r-5861, wired) | fixed | Strategy source: implicit `source-printed`, printed cost 4. No metadata needed; now shown in the panel with its basis. |
| Exchange of Letters | fixed | Printed cost 1 on a Strategy: implicit `source-printed`, nothing to add. |
| Urgency | fixed | "Limited: Pay 2 Gold: Draw a card" is an ability-fixed cost: `Cost: GoldCostRule.Fixed(2)` on the Limited ability. Proven by the `training-urgency-draw` fixture. |
| Virtuous Charity | variable | `Cost: GoldCostRule.Variable()`; the effect reads the amount actually paid through `PaidGoldAmount(command)`. Proven by `training-charity-honor` (and a 1..4 capped variant). **The opponent's payment is not built** (open question below). |
| At Any Cost | derived | "Recruit a dead Samurai, paying that Personality's cost": `Cost: GoldCostRule.TargetPrinted()` on a `dead-personality-any-discard` target. The recruit quote cannot be used because a dead card is not in a province. Proven by `training-at-any-cost`. |
| A Stain Cleansed | derived (grouped total) | "Target's total Gold Cost = what you paid minus 2" is, per target, a fixed payment of the target's total plus 2: `Cost: GoldCostRule.TargetUnitTotal(2)`. Proven by `training-stain-straighten`. |
| Disgraceful Conduct | derived (grouped total) | "Pay Gold equal to a target Personality or unit's total Gold cost minus 4": `Cost: GoldCostRule.TargetUnitTotal(-4, floor: 0)`. Proven by `training-disgrace-bow`. |

## Open questions (rules decisions we do not have)

1. **Virtuous Charity's opponent payment.** "You, then the player, each pay zero or more Gold" needs a prompt asking the *opponent* to pay, which needs a decision: does the second payer see the first payer's amount? Not built. The variable-cost machinery for the acting player is what it will reuse.
2. **"Total Gold cost" of a unit.** Implemented as printed costs: the Personality plus every attached card (Followers, Items, Spells). Whether it should use paid or modified costs, and whether a destroyed/bowed attachment counts, is unconfirmed.
3. **A Stain Cleansed direction.** Read as a per-target derived payment (target total + 2). If the printed intent is that the player picks an amount first and then a target whose total equals it minus 2, that is a variable cost with a target filter; the same `GoldAmount` field would carry it.
4. **At Any Cost restrictions.** Unique/Singular, Honor Requirement and Loyal checks on the dead recruit are not part of the price; they belong with that card's own entry primitive.

## Behavior changes

Intended outcomes only, each pinned by a test:

- Players get the payment panel for every costed action (previously recruit, equip and Strategy only).
- Kharmic is offered and payable. Invest / Singh Remnants reactions carry suggested sources and are disabled when unaffordable. A non-Strategy Discipline is offered.
- Togashi Noritada's Invest and a non-Strategy Discipline now actually bow the sources that pay for them.
- An ordinary equip option honors Katagi's discount when deciding whether it is affordable.
- `playStrategy` charges a Strategy's printed cost (every training Strategy is free, so nothing existing changes).
- Your Clan Needs You! is priced from the recruit quote minus 6 (as its resolution always charged); identical for every published Personality, since none carries `prosperous-discount`.
- `QueryActions` no longer throws during a battle when a Spell with a wielder-relative Battle ability (The Earth's Hunger) is still in hand.

## Coordination

Open PR #275 (stacked on #270) adds `ActionOption.GoldCost`, a scalar equip quote, and a `RenderTargeted` tweak. This change subsumes it (`Gold`/`TargetGold` are a strict superset), touches the same lines of `MatchQueries.cs` and `Main.Actions.cs`, and is based on `main`. Whichever lands second reconciles: drop `GoldCost` in favor of `Gold`/`TargetGold`.

## Evidence

- `GoldParityTests` (18 cases) passes unchanged against unmodified `origin/main` and against this branch. Asserting that the two "validated but never charged" rules do charge fails on `origin/main` (2 of 18), which is how the bugs above were pinned.
- `GoldQuoteTests` (20 cases): quotes for every kind, per-target maps, variable bounds, excess lost, partial commands, JSON round trips, the hand-Spell crash.
- Server and transport suites compare the wire records against `GameCommand` by reflection and round-trip real quotes.
- Native client, Godot 4.7.2 mono on Windows (NVIDIA GPU): `--capture-mode=gold-*` drives the real controls (`tools/check-gold-payment.sh`). Screenshots are not committed; the smoke logs and the checks above are the record.
