# Open battlefield implementation review — 2026-09-30

Issue #499 / PR #565. Reviewed base `1723502b5051193c77b771818a6e4d6657a614f0`, source head `06cebd399447fbc28448c5fdd3fdc07459290c1c` plus working changes. Four independent read-only Codex reviewers followed the repository governance-lite profiles; the rules reviewer subsequently checked the corrective diff. This is automated review evidence, not Mendon/Nathan acceptance.

| Role | Result |
| --- | --- |
| Rules versus core rules | Found resolved occupied extra-front association loss and Finished overridden by Action Phase. Both fixed; corrected projection and regressions re-reviewed without further findings. |
| Hidden information | No new leak found. Only original public turn-owner integer is added; private paused action and card identities remain masked. |
| Negation and edge cases | Found stale attacker-based turn label in The table menu. Fixed. No authoritative timing, cost or negation changes. |
| Client contract | Confirmed same menu-label omission, now fixed. Nullable field handled by all visible turn displays, existing network view serialization retained. |

## Contract items

- New `MatchView.ActionPhaseTurnPlayerId`: derived from persisted engine aside state, public to both seats, omitted after match result. Phase, help, plaques, turn announcement, tracker and table menu consume it. Missing field falls back to existing turn display. Attacker/priority meanings remain unchanged.
- Semantic supplement not detected as a new field: `Battlefields` now includes occupied resolved locations as well as unresolved/current locations. Association remains authoritative while defending survivors remain. Empty resolved locations are omitted. This array is presentation information; unresolved membership and offered commands still control battle selection.
- Explicit null province association means no province even for a primary index. An additional battlefield can retain an associated province.
- No new commands, choices, payment/target payloads, event meanings, saved-state fields or replay schema changes. Existing persisted aside and battlefield state regenerate the views.

## Validation

Native check8 passed with zero build warnings/errors, import and actual Godot input/render checks. It covers full scans and inspection, help/Escape, camera-only navigation, both seats, stable orphan positions/names, associated additional front, six/empty/crowded courts, caption clearance, 1440×900/ultrawide, Action Phase and original turn context, terminal-phase suppression, table-menu status and motion preferences. Check7 encountered a native input-warp/layout synchronization race; the probe now reacquires the visible target after native hover settles. Final native check8 passed.

The full `bash tools/check.sh --skip-client` passed before review corrections. Final focused `emperor-ruling-R1` passed **17/17**, including real defended extra-front resolution, both seats, subsequent province destruction, empty retirement, both aside actors/turn owners, JSON/state restoration, Action return and concession. A final broad check is recorded in the task handoff/PR.

## Limits

Prepared native scenes are public-view fixtures with commands disabled, not card-effect execution. Core regressions exercise real engine transitions. No live two-client aside-specific reconnect was manually playtested; existing full server/transport tests pass. Windows package CI does not establish graphical gameplay or expert rules acceptance. PR #564's overlapping vertical battle-stage rail stays separate until coordinated integration; no merge is authorized.

# Client-contract ledger

Compared `1723502b5051193c77b771818a6e4d6657a614f0` to `06cebd399447fbc28448c5fdd3fdc07459290c1c` plus working files.

Candidates for review; an empty ledger does not prove no client-lane impact.

## Pending choices

None detected.

## Commands

None detected.

## Decider specs

None detected.

## View fields

- added: `MatchView.ActionPhaseTurnPlayerId` — `src/L5R.Core/Models.cs:1491` (int ?)

## State fields

None detected.

## Command fields

None detected.

## Payload sites

None detected.

## Limits

- Heuristic C# inventory, not a compiler or rules/visibility verdict.
- Literal/constant constructors and named payload sites only; dynamic dispatch, value semantics, events and UI behavior require the four reviewers.
- Fields are public properties/fields reachable from MatchState, view/quote/option types and GameCommand; inheritance, custom serializers and conditional compilation require review.

