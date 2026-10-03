# Two Dynasty cards in one province

Part of issue #499; draft PR #544. Alfredo approved A's shared horizontal layout with C's temporary ordering cues. The contract is in [the decision](decisions/2026-09-29-two-dynasty-province.md).

Open **Review Two Dynasty Cards.command**, or the locally generated **L5R Two Dynasty Cards - Review.app**. This launches the exact checkout with no branch switching or fetching. The [versioned Windows review](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr544-b313804419-r2-windows) supplies Start Review.cmd. Its release title and PR checks distinguish a validation candidate from a ready review.

The scenario selector provides two visible cards, a concealed second slot, reveal order, refill order, an empty second slot and a crowded row. Other seat switches between independently prepared views. Selecting a card inspects its complete scan; the province perimeter and number are shared. In the order tray select A or B first, then confirm. The tray disappears and the preview reports the chosen order without executing a game effect.

This is a presentation review. It does not certify or enable Plains of Otosan Uchi's complete rules. The source scan is the pinned Emperor Edition catalogue printing, not generated card text. Full authoritative multi-slot play remains engine work for Mendon.

## Validation

- Client compilation passed with zero warnings/errors.
- `bash tools/check.sh --skip-client` passed: 2,214 C# PASS records across Core/AI/server/transport and 275 Python checks. New checks cover slot/serialization semantics, fresh decision/command guards and second-slot battle receipts.
- `bash tools/check-province-ui.sh` passed: native independent card/Region clicks, actual period scans, hidden inspection, first-slot selection and confirmation with a selected hand underneath, stale rejection, both seats, empty/crowded rows, 1440×900 and 2560×1080. Production tray resizing is checked with the review toolbar removed.
- Existing `published-board-courts-compact` native capture passed compact layout, court bounds, board/input smoke and projection checks; 28 cards occurred exactly once.
- Source/native comparison: [approved layout](../assets/ui/two-dynasty-province/A-shared-row.png), [native visible cards](screenshots/two-dynasty-province/01-two-cards.png), [concealed second card](screenshots/two-dynasty-province/02-face-down.png), [ordering tray](screenshots/two-dynasty-province/03-order-tray.png), [1440×900](screenshots/two-dynasty-province/07-1440x900.png). Raw local captures/logs remain in `artifacts/province-review/`.
- Windows export/runtime and human graphical review are separate checks; the versioned delivery receipt records the exact package.

Four independent governance-lite source passes reviewed the implementation. Rules boundary review found no authoritative logic or printing change. Privacy found a stale legacy-card selection path, client contract found a missing second-slot battle receipt, and client/edge passes found tray resizing and hotseat/reconnect omissions. These have been corrected, with focused regressions/native probes. These reviews do not substitute for execution evidence or human acceptance; final exact outcomes are in the task handoff.

Runtime keeps the existing approved province and lacquer artwork and full original scans. The generated image supplies the layout; native camera/light and actual cards differ, so this is not a claim of full-screen pixel identity.

## Windows distribution size

The first full export exceeded GitHub’s 2 GiB per-asset limit (2,477,749,433 bytes). The Windows preset now omits old standalone DuelArenaReview v3–v6 scenery, the unused war-room-m1/aged-column/chest/table-lantern assets and obsolete m4/m6 floor maps. These sources remain in Git and Mac development imports. Current gameplay uses CrimsonDuel and the B3 layout with column-timber-m7, chest-m7, stone-a/b-m7, dais-fit-m7, brazier/coals, room lanterns and the m7 floor map. Original card scans and the full catalogue pack remain included. Exported runtime resource/dependency checks cover the retained assets; historical source-only DuelArenaReview is not a Windows package entry point.

The revised portable ZIP is 2,086,886,320 bytes, pinned to game source `b3138044194cf159070131899a558779a62d990f`. An isolated Godot process mounted the exported PCK without a source checkout fallback: 1,676 retained resources exist, all 19 retained GLB models load with their dependencies, and 55 obsolete resources are absent. No resource/script errors occurred. The Mac `.app` launcher was opened and visually verified at the two-card province. The package receipt and hosted Windows check carry the final distribution evidence; Mendon's graphical acceptance remains separate.
