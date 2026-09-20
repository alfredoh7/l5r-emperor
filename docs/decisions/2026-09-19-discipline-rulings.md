# Discipline rulings: the additive cost, Lost in Transit, False Ring of Air

**Status:** accepted -- Mendon's rulings on the open questions of the Discipline/Absent PR (#332), implemented in issue #353
(rules lane). It closes the three questions in [2026-09-19-discipline-and-absent-flags.md](2026-09-19-discipline-and-absent-flags.md).

## The rulings (as relayed) and what each one changed

**1. Discipline cost.** Discipline cards **in hand** do not have to satisfy the Discipline cost to be played; all their other
(printed) costs still apply -- this confirms the free-from-hand reading for the seven cards. From the **discard pile** the
Discipline cost must be paid **before** the other costs can be satisfied, so a discard-pile play costs the Discipline cost **plus**
the action's own costs, Discipline first.

Built (`GoldQuotes.cs`, the one Gold-quote path from #307):

- A Discipline ability with an explicit `Cost` of its own (Knowledge and Power, Two Steady Breaths: "Limited, 1 Gold") is priced by
  `AbilityGoldCost`: from **hand** only that own cost; from a **discard pile** the Discipline cost (the card's published
  `GoldCost`) plus that own cost, as **one combined `GoldQuote`** (`Cost` = the total, a `Basis` reading "Discipline cost 1 (paid
  first), then ... = 2 in all"). An action with no cost of its own keeps the implicit rule from #332 (Discipline cost only, from a
  discard pile).
- **Ordering.** Gold does not carry over between payments, so the Discipline cost is a payment of its own, paid first, and the
  action's own cost is a second payment from other sources: one source that alone produces the total is **refused** (its excess over
  the Discipline cost is lost, it cannot also pay the second part). The engine validates the selected *set* of sources for a split
  into a part covering the Discipline cost and a remainder covering the rest (any order accepted; the least-excess split is used),
  and pays two `payment` events, "(Discipline cost, paid first)" then "(the action's own cost)".
- **Offered only if both parts are payable.** The suggested payments are built stage by stage (every alternative for the Discipline
  cost, each completed from the sources it leaves); with no complete payment the quote has none and the option is disabled -- the same
  way any unaffordable cost already is. A player-chosen variable cost cannot be combined (no card prints one).
- `Pay` gained an optional note for the event text; `PaymentOptions` gained a private exclusion-aware overload. Nothing else changed
  for a single-part payment: every existing cost, quote and payment event is byte-identical.

Wired on top of it (strategy-wave-23, **next free wave name for Strategy on origin/main**: 22 was the highest published; open PR #348
holds 21):

| Card | Text (scan verified, Aftermath; sha256 matches the receipt) | Wiring |
|---|---|---|
| Expensive Achievement `oracle-l5r-10938` (Focus 3, Discipline 2) | Battle: Give this Province +2 or -2 Strength. | two abilities sharing one use (Ring of Earth's shape): "this Province" is the current battlefield's, the Defender's; presence required; discard play 2 Gold |
| Knowledge and Power `oracle-l5r-10942` (Focus 2, Discipline 1) | Limited, 1 Gold: Destroy one or two of a target Personality's tokens. Give him as many +1F tokens as the number of tokens this destroyed. | new `personality-with-token` target (either side's); token ids ride on `CardIds`; existing `CreateToken`/`DestroyToken`; honors Champion of Thunder's "negate the action's effects"; from hand 1 Gold, from the discard pile 1 + 1 |
| Two Steady Breaths `oracle-l5r-10961` (Focus 4, Discipline 2) | Limited, 1 Gold: Give your target face-up Personality out of play Destined. | existing `own-personality-in-province` target; `GrantKeyword(..., "Destined", "Turn")`, so his recruit in this turn's Dynasty phase draws (the engine's existing Destined entry effect); from hand 1 Gold, from the discard pile 2 + 1 |

Interpretations to keep in mind (small, none blocks):
- **Knowledge and Power's token choice.** Which one or two tokens are destroyed is named on `CardIds`; with none named the command
  destroys his first token, so the offered command is always legal. **There is no client picker for the choice**, so a player using
  the UI gets the default (one token); a token picker is a client follow-up. The engine accepts and validates any one or two of the
  target's tokens.
- **Expensive Achievement's duration.** Province Strength changes are applied to the province's own `Strength` directly, exactly as
  Ring of Earth and Improved Defenses already do (the engine has no per-turn reset of them). Same convention, same limitation.
- **Two Steady Breaths' "out of play".** Read as the face-up Personality in one of your provinces, the only out-of-play Personality
  that can be recruited.
- **Mercantile Conflict `oracle-l5r-10947` stays unwired**: it needs an Honor-loss-increase primitive ("the next two Honor losses
  from your cards (this turn) are increased by 1 each"). Its `BLOCKERS` entry now says so.

**2. Lost in Transit.** Presence at the battlefield is required **only when it is used as a Battle action**; it is **not** required
when played during the Action phase.

No behavior change was needed, only proof: `Battle/Open` is `BattleOrOpen`, whose Open half (Action phase) has never carried a
presence or battlefield-target rule and whose Battle half (the Battle phase) has carried both since #267. #332 kept the Action-phase
play exactly as before the Discipline timing was removed (the pre-#332 Discipline timing allowed the same two phases with no
presence at all, so the Action phase is identical); only the Battle-phase play gained the presence rule the printed verb always
implied. `LostInTransitTwoModes` now pins both modes, in hand and from the discard pile: Action phase with no presence and a target
not at any battlefield is accepted; in a battle it is rejected without the acting player's presence, rejected when the target
attachment is not at the battlefield, and accepted with both. (In a battle the target must be at the battlefield, the standard
Battle-action target rule.)

**3. False Ring of Air.** A true Absent Battle action (no presence required, as built) that also requires bowing the Ring as a cost.

Verified the wiring: the Ring was bowed in the execution case, but only `if (source.Zone != Zone.Hand)`, so it could be used **from
hand without bowing anything** -- the generic "you may discard this Ring from your hand to use its ability" is printed on the real
Rings, not on a False Ring. Fixed: it has no hand play (its recruit action is separate and unchanged), it must be in play and
unbowed (a bowed Ring is refused by the shared "abilities on bowed cards cannot be used" rule), and it is bowed as the action starts,
before the effect. Tested in play, bowed, and in hand, plus Absent (no unit of your own needed) unchanged.

*Noticed, not changed (out of the ruling):* **False Ring of Water's** "Bow this card: Ranged 3 Attack" has the same shape (its
execution case bows only when not in hand), so it too can be used from hand for free. A one-line follow-up of the same kind.

**4. Deck search (#328).** A found Region that cannot be attached or paid for is **discarded** (as built). Confirmed; no code change.

## Also closed

The three open questions of the previous record are marked CLOSED there: the free-from-hand reading (1), Lost in Transit's and
False Ring of Air's behavior (2, above), and the "not modeled" additive cost (built).

## What the additive cost unblocks

Only cards that print a Gold cost of their own on a Discipline action can use it: those are exactly Knowledge and Power and Two
Steady Breaths, both wired here. No other card is unblocked by it (Mercantile Conflict's action has no cost of its own; it is
blocked on the Honor-loss primitive).

## Reconciled with the merged rulings (#363, #359, #361, #365)

- **Absent / `MovesToBattle` (#363).** The hand-kept id list of #332 is gone; `ValidateAbility` reads `ability.Absent || ability.MovesToBattle`.
  Return to Action carries both (`Absent: true` because it prints Absent Battle, `MovesToBattle: true` because its effect moves a
  Personality to the battlefield; either alone waives presence, and `BattleAbsentMovesTests` scans its `TryMoveToBattlefield` case).
  **False Ring of Air stays an explicit `Absent`** (it prints Absent Battle and moves nothing, so it is not a mover); the bow-as-a-cost
  fix above is independent of both flags. **Lost in Transit** is neither (Battle/Open: presence only as a Battle action, as ruled).
  None of the three needed a change; the flag-inventory test now pins `Absent` = the merged list (with `munefusa-move`) and
  `Discipline` = the eight Discipline abilities.
- **Published count (#361).** This branch no longer touches any count: the pinned total is derived from `content/published/*.json`, and
  `strategy-wave-23` is pinned by its own manifest `tests/L5R.Core.Tests/PublishedWaves/strategy-wave-23.txt` (no collision: main has
  strategy waves 17, 18, 22 and 27). Suites are found by reflection, so `DisciplineRulingsTests` needs no registration.

## Reconciled with atomic costs (#356), alternative costs (#357) and out-of-play windows (#367)

- **Declared costs (#356).** The non-Gold costs of an action are a declarative `Costs` list paid at one point (before the effects, and also for a
  NEGATED action), and a cost blocked by a negation aborts the whole action. This work has exactly one non-Gold cost, and it is declared:
  **False Ring of Air** is `Costs: ActionCost.BowSource` (main had `BowSourceUnlessInHand`, which kept the free hand play; the ruling wants a real
  bow, so the "unless in hand" is gone and `ValidateAbility` still requires the Ring in play). The three new abilities and the Discipline payment
  have no other non-Gold cost (Knowledge and Power and Two Steady Breaths' own cost is Gold; Expensive Achievement has none), so nothing is left in
  an effect case that a negation would skip. The effect case no longer bows the Ring.
- **Order under atomic payment.** `PayForAbility` (all Gold: the Discipline cost first, then the action's own Gold cost, as the two payments of the
  combined quote) runs before `PayCostOption` and the declared `Costs`, both in a normal resolution and in `ResolveNegatedAction`, so "Discipline
  first" holds. A **negated Discipline play** pays the Discipline cost and then the own cost and is still removed from the game
  (`CompleteNegatedAction`); a play aborted because a bow/destroy cost was blocked pays nothing but is still spent and, from a discard pile,
  removed (`AbortActionForBlockedCost`). Pinned in `NegatedPlaysPayAtomically` (Knowledge and Power from the discard pile and from hand under a
  whole-action negation; False Ring of Air negated, blocked and plain).
- **#357, #367.** No overlap: this adds no alternative cost and no Reaction. The discard-pile Discipline plays (Return to Action, Banzai!, Lost in
  Transit and the three new cards) are non-Reaction abilities, so the out-of-play Reaction-window rule does not touch them; they are still offered
  and validated exactly as before (the whole `discipline-absent`/`discipline-rulings` and `out-of-play-reaction-window` suites pass on the merged code).
  Main's `training-discipline-counter-negate` (a Discipline Reaction fixture) joined the pinned Discipline-flag inventory.

## Validation and merge notes

- `tools/check.sh --skip-client` and the client compile, see the PR; new tests in `tests/L5R.Core.Tests/DisciplineRulingsTests.cs`
  (10), mutation-checked (15 mutations, each killed by a named test; listed in the PR).
- Expect a trivial sync with #341, #348 and the other pending PRs: this touches `CardAbilities.cs` (new abilities and cases, one
  target category, the CardIds allow-list, three small edits), `GoldQuotes.cs` (the wrapper and the split-payment helpers) and a few
  lines of `MatchEngine.cs` (`Pay`/`PaymentOptions`).

**Affected files:** `src/L5R.Core/CardAbilities.cs`, `GoldQuotes.cs`, `MatchEngine.cs`, `PublishedContent.cs`, `CardCatalog.cs`,
`L5R.Core.csproj`; `tools/content/package_strategy_wave_23.py`, `build_card_inventory.py`; `content/published/strategy-wave-23*.json`;
`src/L5R.Client/Content/Published/oracle-l5r-10938.jpg`, `-10942.jpg`, `-10961.jpg`; `tests/L5R.Core.Tests`; `docs/CARD_INVENTORY.xlsx`.
**Issue:** #353.
