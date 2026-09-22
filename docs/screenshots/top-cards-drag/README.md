# Top Cards native acceptance — 22 September 2026

[Source/native comparison](index.html). Original source images and provenance are in [designs](../../designs/2026-09-22-top-cards-drag/README.md). These captures use the actual Godot renderer and original card faces.

Client build: 0 warnings, 0 errors. `bash tools/check-top-cards-ui.sh` passes at 1920×1080 normal motion and 1280×800 reduced motion. Native mouse drag/reorder, slot previews, click/keyboard equivalents, invalid/full/outside drops, undo, Escape, full inspection, exact engine order, top/bottom/hand/show/discard, opponent privacy, fixed/short/empty decks and seat/busy drag cancellation are exercised. Label and face containment are asserted.

Visual comparison checked at full-table scale and readable image scale. Main differences from the concept are deliberate reuse of existing heading/font/frame assets and authentic scans. B’s wording is integrated into A’s horizontal tray layout. Mixed effects use neighboring full-height destinations. No pixel-equivalence claim; no rules or Windows human acceptance claim.

Native 1920×1080 table camera/input and full-window coverage regressions also pass after the drag-release routing fix. Prepared app launched from Finder and reached its engine-backed review-ready checkpoint.
