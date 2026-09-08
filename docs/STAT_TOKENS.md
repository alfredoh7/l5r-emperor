# Automatic Personality stat tokens

Implemented for the approved Force/Chi concept, related to [issue #18](https://github.com/alfredoh7/l5r-emperor/issues/18) and [PR #32](https://github.com/alfredoh7/l5r-emperor/pull/32).

## Behavior and appearance

Each in-play Personality has amber Force and pewter Chi bitmap discs with pale gray live numerals. The metal texture derives from Alfredo's approved concept in `assets/ui/stat-tokens/approved-concept.png`; the runtime asset is `src/L5R.Client/Art/stat-token-sheet.png`. Card faces remain intact. The numerals and labels are rendered separately for arbitrary live values and readability at different zoom levels.

The discs remain upright through bowing and follow the lowest visible card in a unit stack, including independently bowed attachments. Rows reserve space for the readouts. Tokens move with the camera, hide when their owner moves offscreen, and appear beneath the Personality in the expanded attachment tray. At distant zoom levels labels shorten to F/C. Hovering shows current/printed values, visible Item bonuses, applied modifiers and granted keywords; clicking selects the Personality without issuing a game command.

Values come directly from `CardView.Force` and `CardView.Chi`. The client does not calculate authoritative totals. These are Personality values, not the unit's total Force or its current battle contribution. The tooltip explicitly distinguishes those concepts and notes that engine-applied conditional effects are included even when not individually available as modifier records. No private card identities are inferred.

This completes the approved token presentation, not every acceptance criterion in issue #18. A separate unit-total display, persistent granted-keyword display and a fully source-resolved conditional-effect breakdown remain possible follow-up work with the rules lead. Do not close #18 based on this PR alone.

## Verification

- `bash tools/check.sh`: 188 checks passed (29 collaboration, 59 content, 46 foundation, 32 published, 13 AI, 5 server, 4 transport), plus client build, import and headless startup. NuGet vulnerability metadata lookup produced NU1900 warnings; compilation succeeded.
- `bash tools/check-table-camera.sh`: eight native Mac cases, including wheel zoom, middle/empty-cloth drag, arrow keys, cursor anchoring, 16:10, ultrawide, assignment, attachment tray, pile tooltip and reduced motion.
- `bash tools/check-stat-tokens.sh`: native fixture and real engine-board captures. Fixtures cover 0/1/3/6 attachments, independent bow states, live value replacement, zero/two/three-digit numbers and native token clicks. Board cases cover battle, assignment, focus, the expanded tray and a real mouse-hover explanation. Three additional shuffled focused battle layouts passed. Geometry checks reject card occlusion, token collisions, fitted-view clipping and a click that changes the match version.
- Native screenshots are reviewed separately from those assertions. Committed examples are under `docs/screenshots/stat-tokens/`. Fixture screenshots explicitly label illustrative values; they are not evidence of legal card combinations or rules acceptance.

No native Windows playtest is claimed. The original concept is a visual reference, not a pixel-for-pixel runtime screenshot.
