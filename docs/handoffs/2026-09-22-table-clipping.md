# Table visibility clipping — issue #446

Cards were rendered into the camera's smaller overview-fitting rectangle while the scenery filled the window. Panning or zooming exposed that invisible mask on all four sides.

The fix keeps the original camera anchor and fit behavior but renders the table across the window. Its offscreen canvas is sized from the inverse projection of the actual display corners; input uses the same mapping. Fixed HUD controls and the hand remain above the table and keep their own input.

A new native regression samples actual rendered pixels outside each former edge, then clicks and zooms that same card. It checks fixed HUD input and unchanged match state. The baseline reproduced a zero-pixel difference at the left edge, confirming the card layer was absent there. Native camera probes now tag their mouse events, as the preview stack already does, so unrelated desktop pointer activity cannot change the scripted sequence.

No engine or game rules changed. Main has not been merged. The current-fortress review is a separate local integration on the existing review stack; the main-based PR contains only the rendering fix and its verification.

Validation and review links are recorded in the task handoff and PR #447.

## Validation

- Baseline native regression: FAIL as expected, left-edge card raster difference 0.0000.
- Updated main base `aa27db4c`: client compilation, zero warnings/errors; four-edge pixel/click/cursor-zoom and fixed-HUD picking checks PASS at 1920×1080, 1440×900 and 2560×1080.
- Final `tools/check-table-camera.sh`: all eight native cases PASS (overview, battle focus, 16:10, ultrawide, assignment, attachments, pile hover, reduced motion).
- Intermediate native runs exposed desktop focus/pointer interference and the new full-window texture intercepting clan-panel picking. The probes now wait for focus and settled motion; the texture ignores GUI input while Main continues inverse-projected routing. Final results above are after those corrections.
- Main integration brings in Mendon's #444/#445 unchanged. No rules changes originate in this fix.

Screenshots in `docs/screenshots/table-visibility/` demonstrate drawing beyond the old left/bottom cutoffs. The existing generated board fixture can differ between runs; screenshots are not a pixel-matched before/after pair. The regression compares the visible and hidden card layer within the same frame position.

## Review

- Minimal main-based fix: https://github.com/alfredoh7/l5r-emperor/pull/447
- `Review Table Visibility.command` builds and opens a prepared zoomed board; wheel zoom, drag empty cloth/middle-drag to pan, Home/Fit to restore the full table.
- Current fortress preview source is kept on `codex/446-table-visibility-review`. It includes the previously unmerged UI stack and must not be treated as approval to merge that stack.
- Local fast launcher: `artifacts/table-clipping-review/artifacts/L5R Table Visibility - Review.app` relative to Alfredo's workspace. It uses the built preview directly. Other existing review apps are unchanged.
- Human appearance acceptance and native Windows graphical validation remain open. Do not merge main without approval.
