# Ink mat implementation and review

Issue #52 / PR #53 — 2026-09-08 — native macOS Godot 4.7.2 .NET.

The approved C direction is implemented as textured, animated mat sections. Holdings are grouped on screen-left, other Home cards remain separate, and actual battles use province-specific fields. Existing full card scans, stat inlays, unit standards, rules authority and manual camera controls remain in use.

## Reviewed native views

These are unaltered game captures from real practice states, not generated game screenshots. Practice shuffles vary, so the three pictures are different positions in different matches.

![Whole table](screenshots/ink-mat/overview.png)

![1280 × 800 table](screenshots/ink-mat/small.png)

![Player-side camera framing](screenshots/ink-mat/focus.png)

The focused view intentionally crops the opponent's side; Fit Table returns to the complete layout. The material follows C's worn ink and cloud motifs, with a brighter pigment and inset ornaments adapted to gameplay. It is not a literal pixel copy of the concept sheet.

## Validation

- `bash tools/check.sh --skip-client`: passed. 31 collaboration, 85 content, 56 foundation, 32 published, 8 deck-format, 13 AI, 5 server and 4 client-transport checks.
- Client compilation: zero warnings/errors. Native Godot import passed.
- `bash tools/check-ink-mat.sh`: three native board fixtures at 1920 × 1080 and 1280 × 800, plus a 72-frame purchase film. Verifies disjoint labeled regions, left holdings, exact card membership, section clearance, hover/select/deselect invariance, actual attachment expand/inspect/fold, and empty surviving province markers. Final fixtures include Home stacks of one and three attachments and battlefield stacks of two and four attachments.
- Isolated native renderer checks: expansion, contraction, intermediate geometry, interruption/retargeting without jumping, unchanged-sync stability, removal fade, reduced motion and fresh-match reset. These are explicitly representative rectangles rather than fabricated gameplay events.
- Purchase film: a real engine-offered Holding purchase changes the displayed in-play group from four to five cards including the Stronghold. The border grows from 474 to 694 logical units wide; cards and piles reflow in their real table plane; camera remains fixed. Replay and per-frame geometry are written beside the capture. Capture waits for `FramePostDraw` so new controls have actually rendered.
- Existing board matrix: all 17 assignment, defense, target, purchase, refresh, choice, rules and aspect-ratio cases passed. The Shinden setup originally ended its random practice without encountering the choice; setup now searches another real match before the UI assertion. The affected choice case and remaining rules case were rerun successfully. No failing UI assertion is retried.
- `bash tools/check-table-camera.sh`: eight cases passed, including native pointer/key routing, projected upright/bowed edge clicks, modal isolation, attachments, pile tooltip, reduced motion, 16:10 and ultrawide views. Its modal probe now opens the real Rules dialog because Options is a main-menu page; repeated edge clicks account for the approved deselect toggle.
- `bash tools/check-card-deselection.sh`: battle, attack assignment, defense assignment and target-confirmation cases passed.

Ignored local evidence lives under `artifacts/checks`, `artifacts/table-camera`, and `artifacts/screenshots`. Run the ink script to regenerate the 24 fps film frames and replay. The movie is an authored-time native capture, not a real-time frame-rate benchmark.

## Adversarial review and iteration

Independent source and visual review identified: raw-scan reflow losing stat overlays/flags; hover creating false reflow; empty provinces losing identifiers; null-snapshot refresh; very narrow labels; attachment-tray metadata; hand/border clearance; stale camera tests. These were corrected. Live card/pile interpolation now preserves actual controls and their overlays; hovering and opening trays do not resize sections. The review also rejected barely visible initial ink and undersized labels before the present material was selected.

No authoritative rules, server protocol or card data changed. Mendon's review remains appropriate for the battlefield grouping and shared client file. These checks are not expert L5R rules acceptance or native Windows graphical playtesting. Assignment previews and cinematic resolution/destruction remain later passes.
