# P8: action-effect watch (issue #712)

Status: **built, engine only** (no card wired, no scan, no art pack: Moto Taigo - exp, Reverse Fate and Verdant Jungle are wired in a follow-up wave). Lane: rules. Source: primitive P8 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/ActionEffectWatch.cs`; tests: `tests/L5R.Core.Tests/ActionEffectWatchTests.cs` (suite `action-effect-watch`, 9 tests).

## What it is

| Piece | API | Where it reads |
|---|---|---|
| Arm | `ArmActionEffectWatch(kind, callbackId, ownerPlayer, sourceId, arg, atEnd)`: stores an `ActionEffectWatch(HeldActionKey, Kind, CallbackId, Arg, OwnerPlayer, SourceId, AtEnd)` in `MatchState.ActionEffectWatches` (order 2900) | called by a Reaction's `RunEffects` in the before-action / Attack-reaction window; refused (rule violation) when no action is held |
| Facts | `RecordActionEffect` gains `toBattlefield` and `focus`; `AffectedCard` gains `ToBattlefield`, `Side` (`attacking`/`defending`), `Focus` (omitted when unset); new `DiscardedKind` (from `DiscardTopOfFateDeck`, `NoteDiscardedByAction`); the created kind is P6's (`PerformersAmbushAbilities.CreatedKind`), reused | `MatchEngine.MoveToBattlefield`, `TryMoveToBattlefield`, `SwitchRelocate`, the discard helper |
| Fire | `EngineHooks.ActionEffectWatchFired: Func<ActionWatchFact, ActionWatchResponse?>` keyed by callback id (`ActionWatchRegistry.Fired`). An immediate watch fires for the first matching fact and is spent; an `AtEnd` watch fires once with every matching fact when the action is truly done (`FinishActionEffectTracking`) | `NoteWatchedFact`, `FireEndWatches` |
| Answer | `ActionWatchResponse.Done`, or `.Ask(candidates, prompt, apply, optional)`: queued as a `PendingEventChoice` of kind `p8-watch`, opened by the existing queued-event-choice drain; the hook is re-asked on the answer so a saved game resumes | `ResolveWatchEventChoice`, `WatchEventChoiceActions` (one line each in `EmperorRemainingFEvents.cs`) |
| Gate | a `move-gate` watch is consulted by `MovementToBattlefieldNegated` through `EngineHooks.ActionMoveGates: Func<ActionMoveGate, bool?>` (`ActionWatchRegistry.Gate`); `MovementCannotBeNegated`, Winter Sled's bar and P12's `MoveNegation` immunity are honoured | `ActionWatchNegatesMove` |
| Focus Value | `FocusValueOf(card)` = printed + `Focus` modifier (never below 0), `AddFocusModifier` | the action-effect reads of a discarded card's Focus Value (Akodo Regulars, Tactical Advantage, Obote, Susumu Takada, the Training/TF proxy cards); a duel still reads the printed value |

Watches end with their action: when `FinishActionEffectTracking` runs, or when the action is negated whole (`ResolveNegatedAction` drops them). A match that never arms a watch has no watch list and an unchanged saved state.

Shared lines edited: `Models.cs` (`AffectedCard`, three optional properties with `JsonIgnore(WhenWritingNull)`), `ActionEffectTracking.cs` (`RecordActionEffect`, one call in `FinishActionEffectTracking`), `ActionNegation.cs` (one line), `EmperorRulingR1.cs` (one term in `MovementToBattlefieldNegated`), `EmperorRemainingFEvents.cs` (two lines), `EngineHookStartup.cs` (two lines), `MatchEngine.cs`/`CardAbilities.cs`/`EffectPrimitivesMoves.cs` (the destination on three move records; the discard record), and the Focus-Value reads listed above.

## Readings for Mendon to confirm or correct

- **The held action only.** A watch matches the running action by ability id and acting player (the delay store's rule), so a Reaction's own effects never trigger the watch of the action it interrupts (Coils rule 4).
- **First fact only (immediate watch).** Reverse Fate's "the first discarded card" is the first `discarded` fact of the action; a card that wants every fact uses `AtEnd`.
- **A discard counts when an action's effect discards the top card of a deck** (`DiscardTopOfFateDeck`). Discards that are costs (Focus, "discard a card" slots), the End Phase hand-size discard and Event-driven discards are not recorded: the prompt says "effects", and a cost is paid whether or not the effect happens.
- **The side of a moved unit** is the owner of the moved card against the active player (the Attacker). "An attacking army at its province's battlefield" is therefore `Attacking == true` at the destination.
- **A moved card is recorded once per watch** even though two helpers each record a move (the existing duplicate record is unchanged; the watch dedupes by card).
- **A Focus modifier lasts until the end of the turn** like every modifier (`FinishTurn` clears non-permanent ones), including on a card in the discard pile.
- **Honor facts** (the prompt's fourth kind) are not built: no demonstration card needs them and the Honor window (`honorChangeReaction`, P14) already has its own channel.

## Not done

- No card wired. Moto Taigo - exp: arm an `AtEnd` `moved` watch whose callback asks for "a card without attachments in one of the moved units" and destroys it. Reverse Fate: arm a `discarded` watch whose callback adds ±1 with `AddFocusModifier` (the owner's choice of sign is the card's own prompt). Verdant Jungle: arm a `move-gate` watch whose gate returns `Attacking`.
- `TargetWindows.cs` is untouched: the window still opens only when a reactor exists for a target or cost subject; an untargeted action with a registered watcher would need that file.
- Hand discards by an effect have no shared helper in the engine, so none is recorded yet.
