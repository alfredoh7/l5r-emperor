# Next-five review revisions — 22 September 2026

PR: https://github.com/alfredoh7/l5r-emperor/pull/434
Branch: `codex/323-review-revisions`, isolated follow-up to PR #432. Current main `062e5718` integrated. No main merge authorized.

## Latest follow-up

Alfredo approved revised Multiple Targets and Battle Progress and selected **A’s draggable Top Cards layout with B’s deck-order labels**. The two-step Top Cards interface described below is superseded by [the draggable implementation handoff](2026-09-22-top-cards-drag.md). Review acceptance and Mendon notifications are recorded in [the decision](../decisions/2026-09-22-top-cards-drag-review.md). No main merge is authorized. The implementation branch now includes current main `aa27db4c` and the table-visibility fix from #447.

## Earlier revision record (superseded for Top Cards)

### Acceptance and changes

- **#377 Opponent Payment:** Alfredo says “good to go.” Hands-on UI acceptance recorded on PR #427: https://github.com/alfredoh7/l5r-emperor/pull/427#issuecomment-5774661062. Mendon review still required before merge.
- **#323 Top Cards:** separate choice and return-order steps. Visible per-card action buttons replace dropdowns; a card with one action can be clicked to select/undo. Required counts and engine eligibility reasons stay visible. Next is local only. Return review shows outgoing-card names and the full faces of returning cards, with explicit top/bottom positions and text move controls. Back retains the draft; only final confirmation submits. Mixed destinations group top and bottom returns separately. The launcher now opens Banish All Doubt; training scenarios are marked with an asterisk.
- **#345 Multiple Targets:** removed earlier/later chevrons and the redundant remove row from every up-front group. Clicking a selected card deselects it; reselection appends it to the chosen order. Numbered targets, command order and ordered reaction windows remain. No slot timing or rules changed.
- **#311 Battle Progress:** phase plate widened so its visible gold frame meets the right edge of the narrow flag/helmet/horse strip. The artwork’s mounting-tab inset is accounted for. Height is bounded above the strip; labels gain room; Rules help remains accessible.
- **#400 Created Copies:** three new ImageGen concepts in `docs/designs/2026-09-22-copy-marker/`. Recommend B’s ghost outlines with A’s compact wisps. Alfredo selected B’s overlapping ghost outlines with A’s shorter, quieter wisps during this task; a faithful production sprite now replaces the round seal. Two overlapping outlines remain crisp, with compact static wisps. Lower-left placement, full card scans and engine-controlled CreatedCopy behavior are preserved. Both native check configurations passed.

## Launchers

All paths below are under this checkout’s `artifacts/` directory:

- `L5R Top Cards - Revised Review.app` → `Review Top Cards.command`
- `L5R Multiple Targets - Revised Review.app` → `Review Multiple Targets.command`
- `L5R Battle Progress - Revised Review.app` → `Review Battle Progress.command`
- `L5R Ghost Copies - Review.app` → `Review Created Copies.command`
- `L5R Emperor - Latest.app` → `Play Latest.command`

Local gallery: http://127.0.0.1:8799/revisions-2026-09-22/
Copy comparison: http://127.0.0.1:8799/copy-ethereal/native/
Copy concepts: http://127.0.0.1:8799/copy-ethereal/

## Validation

- Client compilation: 0 errors / 0 warnings.
- `tools/check-top-cards-ui.sh`: 1920×1080 normal and 1280×800 reduced passed. Native card/button selection, hover, inspection, keyboard reorder, Back/Next draft retention, count bounds, exact engine outcomes, mixed return groups, opponent privacy, show/history, fixed/short/empty decks, seat and busy reset exercised.
- `tools/check-multi-targets-ui.sh`: both sizes passed; no chevrons, keyboard deselect/reselect order, exact engine effects, independent groups, counts, optional zero, private hand, conditional follow-ups and ordered target reactions.
- `tools/check-battle-sequence-ui.sh`: both sizes passed; visible-frame alignment and non-overlap asserted along with formal battle flow, native input and reduced motion.
- `tools/check-created-copies-ui.sh`: both sizes passed; actual creation, original distinction, native marker inspection, both seats, bounded hover, battle/bowed/attachments, actual destruction to public Removed gallery. Native source/comparison at approximately matching marker sizes inspected.
- Final `bash tools/check.sh --skip-client`: all requested checks passed after main `062e5718`; includes 8/8 server and 12/12 client transport checks.
- Native screenshots inspected and saved in `docs/screenshots/review-revisions/`. Local browser gallery rendered and concept links verified. Existing browser preferences preserved.
- Prepared Top Cards app opened and the choice → return flow exercised. Multiple Targets app opened Bonds of Coin; Battle Progress app opened the prepared Engage scenario through Finder. The Ghost Copies app was opened through Finder and its prepared Home scene verified. Desktop automation’s short-lived app selector timed out although those wrappers successfully launched; the native windows were verified separately.

## Artwork dependency

Current main adds printings beyond the previous package. Published private prerelease `catalogue-art-pr434-20260922-r2`, 3044 verified WebP85 images, 414328092 bytes, SHA-256 `2fa09124ccd0fa0e944b82d72e36e293bbed84bb6e7a95e8d970f93c56f75889`. Uploaded release digest/size match; offline installation verification passes. Archive stays out of Git. No printing provenance changed by this task.

## Remaining

At this earlier checkpoint Alfredo had not reviewed the revised demos. His subsequent #345/#311 approvals and #323 redesign are recorded above; Mendon validates rules interpretation before any main merge. PR remains a draft for this review. The earlier Windows review package is unchanged and does not contain these revisions. Native Mac validation is not Windows graphical playtesting. CI status is tracked on PR #434.
