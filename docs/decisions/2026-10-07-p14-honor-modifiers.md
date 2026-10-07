# P14: Honor-change modifier registry (issue #713)

Status: **built, engine only** (no card wired, no scan, no art pack). Lane: rules. Source: primitive P14 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/HonorModifiers.cs`; tests: `tests/L5R.Core.Tests/HonorModifiersTests.cs` (suite `honor-modifiers`).

## What it is

| Piece | API | Where it reads |
|---|---|---|
| Channel | `EngineHooks.HonorChange: Func<HonorChangeContext, HonorChangeResult?>` | `GainHonor`, at two stages |
| Context | `Stage, PlayerId, Amount, Reason, ActionKey, SourceCardId, SourcePlayerId, CausedByOwnCard, IsProclaim, TargetedCardIds` | built by `ApplyHonorChangeHooks` |
| Result | `NewAmount, RedirectToPlayer?, ConsumedModifierId?` | chained: each registrant sees the amount the last one left |
| Store | `MatchState.HonorModifiers` (order 3000), `AddHonorModifier(HonorModifier)`, `HonorModifierList` | the `HonorModifierStore` registrant (STANDING stage) |
| Kinds | `next-change`, `next-proclaim`, `each-gain`, `no-gain`, `no-loss` | |
| Filters | `own-cards`, `not-proclaim`, `action-effects`, `action-phase-only`, `unless-controls:<Keyword>`, `targeted-by-other:<cardId>`; an unknown filter never matches | |
| Size | `Delta` changes the size (a negative one shrinks, floor 0); `SetTo` replaces it (Kimura's "the discarded card's Focus Value") | |
| Expiry | `UntilTurn` (the modifier lapses after that turn number); null lasts until consumed | pruned on every read/add |

Stages. **STANDING** runs before the Honor-change Reaction window (`OfferHonorChangeWindow`), so the window opens with the amount as modified. When the window opens, the command is abandoned and replayed from the state it started in, so a modifier consumed before the window is consumed again on the replay exactly once. **APPLIED** runs where Shiro Chugo's reduction always ran (after the window's Reactions and every other reduction), so ordering with Yamazaki's "exactly 1" and the other later steps is unchanged.

The saved state is unchanged for a match that never adds a modifier (both new properties are omitted when unset).

## First registrants

- Shiro Chugo's reduction moved onto the channel (APPLIED stage), same function, same position: the existing Chugo suites pass unchanged.
- **Rhetoric's redirect was not moved.** It lives inside `EmperorRemainingCHonorChange`, in one function with Solid Defense's negation and Rumors Travel's reduction, in a fixed order, and runs after the window (it is played in it). Splitting it would reorder those three. The channel supports a redirect (`RedirectToPlayer`) for new registrants; moving Rhetoric is a follow-up if wanted.

## Readings for Mendon to confirm or correct

- **A standing modifier applies before the Honor-change window; a Reaction played in the window sees the modified amount.** The prompt asks for this. Chugo's reduction (played in the window) stays after it.
- **"From your cards"** means the change's source card is owned by the affected player. A caller that names no source falls back to the card of the running command (`_runningCommand.CardId`); a rulebook change has no card, so `own-cards` excludes it. Not every `GainHonor` caller passes a source; where none does and no command card exists, `own-cards` will not match. The wave that wires a card must check its own call sites.
- **Proclaim is an Honor gain with no source card** unless the caller names it (`_proclaimAction` marks it). `not-proclaim` excludes it; `next-proclaim` requires it.
- **"Increase a loss by 1"** raises the size of the loss (a loss of 2 becomes 3); a negative `Delta` on a loss shrinks it. `each-gain` ignores losses.
- **A `next-*` modifier is consumed by the first change it applies to**, and the walk stops there (a later modifier does not also act on that change); `each-gain`, `no-gain`, `no-loss` stack in the order added.
- **Honor paid as a cost** goes through `GainHonor` too; the channel sees it. A card that means "from effects only" adds the `action-effects` filter.

## Not done

- No card wired (Court Scribe, Kimura, Foothold of the Mad, The False Path, Marriage of the Emerald Champion, Invocation, The Heavens' Blessing, Merchant Patronage are the wave's; Kimura needs P2's Ring waiver and has no local scan, Marriage needs P11 and P4).
- Rhetoric's redirect and the other hard-wired Honor modifiers stay where they are.
