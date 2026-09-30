# Draggable Top Cards and recorded approvals — 22 September 2026

PR: https://github.com/alfredoh7/l5r-emperor/pull/434
Branch: `codex/323-review-revisions`, stacked on #432. Includes main `aa27db4c` and the current fortress/table-visibility integration `b16d40ad`. Main has not been merged or modified.

## Result

The rejected choice/return screens are replaced by **A’s one-screen destination trays with B’s explicit deck-order labels**. Original full faces move between only the destinations offered by `PendingLook`. The held face, landing outline, animated neighboring cards and positional captions expose the draft before release. Reduced motion places cards immediately.

A single Confirm submits the arrangement. Undo, Escape, outside-drop cancellation, click/keyboard placement and full-card inspection work without early submission. Maximums and per-card restrictions use engine-provided values/reasons; no silent replacement of a filled hand slot. Top, bottom, show-and-take and discard remain distinct, and opponent deck ownership is named. Fixed-order effects preserve viewed relative order. Seat and busy changes destroy private drag previews.

The table input handler now leaves an active root-UI drag release with the root viewport. Forwarding that release into the underlying table SubViewport could end the shared Godot drag before the tray received it. Preview positioning follows the same accepted mouse events as other client input. The native probe also keeps Godot’s physical pointer and input events synchronized because Godot reads the physical pointer again when completing a drag.

## Approvals and coordination

[Decision and notification links](../decisions/2026-09-22-top-cards-drag-review.md). Alfredo approved revised Multiple Targets (#345 / #429) and Battle Progress (#311 / #430). Mendon was explicitly notified on those PRs and #434 and asked to record requested UI/QoL or rules changes. This is not Mendon acceptance.

Collaboration context was refreshed. Git refs were current; `collab.py` GitHub reads failed authentication, so the authenticated GitHub wrapper supplied current issue/PR/review context. No board token is configured; explicit ownership was recorded on [issue #323](https://github.com/alfredoh7/l5r-emperor/issues/323#issuecomment-5780064867). No competing changes to the Top Cards implementation were found.

## Evidence

See [native captures and source comparison](../screenshots/top-cards-drag/README.md) and [approved source/provenance](../designs/2026-09-22-top-cards-drag/README.md). Existing lacquer/brass, heading font and authentic scans are retained; generated card text and decorative parchment are not substituted into the game. This is the approved layout/label combination, not full-screen pixel equivalence.

Final client build: 0 warnings/errors. Top Cards native checks passed at 1920×1080 normal and 1280×800 reduced motion. The 1920×1080 battle camera probe passed real edge clicks, zoom, pan, HUD/hand and modal isolation; full-window coverage passed all four old clipping boundaries with native input and HUD protection. Finder launched the prepared app, confirmed by its exact profile process and `TOP_CARDS_REVIEW_READY` log. The CUA app selector resolves another Godot instance, so native visual verification uses the automated renderer captures. Exact validation is also recorded in this branch’s JSON task handoff. Earlier unrelated native and deterministic check runs remain separately recorded in the previous revision and table-visibility handoffs; they are not claimed as fresh checks for this turn.

## Review entry

- `Review Top Cards.command` builds the checkout and opens Banish All Doubt directly.
- Local prepared `artifacts/L5R Top Cards - Drag Review.app` opens the already-built client directly, avoiding an unnecessary rebuild for this review.
- `artifacts/L5R Emperor - Latest.app` refreshed using `tools/create-dev-launcher.py`.
- Local gallery: http://127.0.0.1:8799/top-cards-drag/native/ (Alfredo’s Mac only).

The prepared Top Cards screen has scenario buttons for Reorder, Take + top, Take + bottom, Top or bottom, Any Fate deck, Show + take, Fixed order, Mixed choices and Other seat.

## Remaining

Alfredo’s hands-on review of this newly implemented Top Cards result; Mendon review; PR CI. No main merge authorized. The previous Windows package does not include this revision; Windows graphical playtesting is not claimed. The Mac launcher and localhost gallery are not Windows deliverables.
