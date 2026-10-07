# P15: Fortification Holdings (issue #719)

Status: **built, engine only** (no card wired, no scan, no art pack: The Tower of Vigilance is wired in a follow-up wave and also needs a CreatedOnly scan for its Guard). Lane: rules. Source: primitive P15 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/FortificationHoldings.cs`; tests: `tests/L5R.Core.Tests/FortificationHoldingsTests.cs` (suite `fortification-holdings`).

## What it is

A Holding with the **Fortification** keyword guards one of its owner's Provinces.

| Piece | Behaviour |
|---|---|
| Entry | One sweep after every accepted command (`SweepFortifications`, called from `Execute` before the entry-turn sweep): a Fortification Holding now in `Zone.Home` with no entry turn is **straightened** and recorded in `MatchState.Fortifications` (holding id to province index). Purchase, `RecruitFromProvince` and a recruit from a discard pile all reach it without a per-path edit. |
| Province | The Province it entered from (`LastProvinceEntryCardId`/`LastProvinceEntryProvinceIndex`). Otherwise the owner chooses one of their remaining Provinces (`PendingEventChoice`, kind `p15-fortify`, answered by `eventChoice` with the province index as `TargetId`); a single remaining Province is chosen at once. |
| Destroy | `DestroyProvince` calls `DiscardFortificationsOf`: the Holding goes to its owner's Dynasty discard, its attachments to the Fate discard. It is a direct move, so no watch, immunity or negation sees it. |
| View | `ProvinceView.FortificationIds` (additive, init-only, null when none). Public: a Holding in play is face up. The client may ignore it. |
| Saved state | `MatchState.Fortifications` (property order 3100) is omitted until one is in play; a match without one keeps its serialized baseline. |

Shared lines edited: `MatchEngine.cs` (`Execute`: one call; `DestroyProvince`: one call), `EmperorRemainingFEvents.cs` (two arms for the province question), `Models.cs` (`ProvinceView.FortificationIds`), `MatchQueries.cs` (fills it). No `Purchase` edit was needed.

## Readings for Mendon to confirm or correct

- **Straightened after the command, not during it.** The Holding is straightened by the sweep that ends the command that put it into play. Nothing can use it in between; the effect is the same as entering straightened.
- **The Province is the one the Holding came from.** A stale "last province entry" record for the same card id is ignored when that Province is already destroyed; a Holding that re-enters by another route while the record still names it would be attached to the old Province (no such route exists today).
- **No Province to name** (every Province destroyed) leaves the Holding unattached and unrecorded.
- **A Fortification that leaves play** simply loses its record; it is not discarded by this primitive.
- **Province destroyed = discarded**, to the Dynasty discard (Holdings are Dynasty cards), face up and straightened. "Cannot be prevented" is read as bypassing every prevention, as the Province card and Region are.
- The question for a Holding with no Province opens as a decision at once when no other window is open, and is queued behind the open window otherwise.

## Not done

- No card wired. The Tower of Vigilance's Engage and its created Guard are the wave's.
- The client does not draw `FortificationIds`.
- The new code has only been exercised by the test suite, not in a running game; native Godot was not run.
