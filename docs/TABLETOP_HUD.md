# Tabletop HUD refinement

Task: [#34](https://github.com/l5r-emperor-dev/l5r-emperor/issues/34). Follow-up PR: [#35](https://github.com/l5r-emperor-dev/l5r-emperor/pull/35).

## Behavior

- Force/Chi metal tokens keep their approved materials, show only their current number, and use Musashi Brush. Color, relative placement, hover explanations and engine-bound values remain available.
- Fit, Menu and Rules are custom engraved icons at the top right. Fit retains Home, mouse-wheel zoom and drag/arrow panning. The existing battle-focus action is a crossed-swords icon beside them, visible during a battle. The +/- and My side buttons are removed.
- The separate turn banner is hidden. The active turn owner is identified in its Clan plaque, independently of the player currently required to act. The latter gets a stronger glowing frame and action/response wording. A turn owner waiting for a response retains a quieter outline. Reduced motion keeps the glow steady.
- Honor uses a custom folding fan. Gold uses the supplied complete koku coin with its center cutout. Counts are public values from the existing presentation path; this pass does not change resource calculations.
- All seven supplied Clan marks have atlas regions. Lion/Crane are the currently playable Clans; adding art is not adding playable decks. A runtime patina preserves the mark designs while reducing saturation and warming the surface.
- Deck/discard D/F markers are removed. Existing tooltip names/counts and public discard inspection remain. Piles have a separate dark core, worn paper lip and small deterministic edge variation per card. Height follows the existing count interpolation; no deck identities or shuffle order are used to draw a pile.

## Dependencies and build

This branch starts from main and incorporates pending clarity PR33 and token PR32. Its review comparison is stacked on PR33. Merge/rebase in dependency order; do not merge this directly while bypassing required parent reviews. The separately assembled local playable preview additionally includes menu PR9. Main and earlier launchers remain unchanged.

The inherited PR32 handoff is preserved verbatim under `docs/handoffs/dependencies/`, so the stacked PR audit reads this task’s handoff rather than treating the inherited task as a second current owner. Run the audit with the actual comparison base: `python3 tools/collab.py audit --base origin/codex/11-turn-card-clarity --working`. Restore/synchronize historical task records with current main when the dependencies merge.

## Verification

Run `bash tools/check.sh`, then `bash tools/check-tabletop-hud.sh`, `bash tools/check-card-clarity.sh`, and `bash tools/check-table-camera.sh` on a native desktop. The tests do not replace visual inspection or expert rules acceptance.

- `TabletopProbe.tscn` is a clearly labeled presentation fixture, never loaded by normal match setup. It shows both deck backs at 0/1/8/20/40 cards, all seven supplied marks and response/turn glow separation. It checks fractional draw height (including 1→0) and reduced-motion settling.
- HUD captures click Fit/Menu/Rules through native input and require unchanged game state and responsive button bounds.
- Existing stat-token probes cover 0/1/3/6 attachments, upright/bowed states, zero and one/two/three-digit numerals, updates, clicks and hover explanations.
- Existing clarity/camera checks exercise actual match layouts, hand selection, enlarged-card overlap, bowing, attachments and panning/zoom.
- The tactile capture submits actual Border Keep, discard and end-phase draw commands and checks movement/count settling.

Final outcomes and screenshots are recorded in the task handoff and PR. Windows CI/export is not native Windows graphical testing.

## Recorded outcome

- `bash tools/check.sh`: 188 checks passed (29 collaboration, 59 content, 46 core, 32 published, 13 AI, 5 server, 4 transport), plus Godot import/headless smoke. Final runtime compilation: zero warnings/errors. Initial network-restricted package-audit warning was resolved by a successful online restore.
- All 14 clarity scenarios passed cumulatively, including the corrected ultrawide case. All eight camera scenarios passed.
- Four token fixtures passed; each checks 0/1/3/6 attachments, upright/bowed, 0/one/two/three-digit values and native input. Three responsive icon captures plus the final 720p pile-top-edge/arrow-pan check passed.
- The real-command tactile sequence passed return/draw/discard/end-phase draw, exact final counts and reduced motion.
- Combined menu preview passed launcher menu→match, pause/resume, cancel replacement, options persistence, native toolbar/pile-top input, expanded selected parent, reduced motion and final pile-count fixture.
- Visual captures were inspected. The ultrawide test initially found reduced mat padding above raised piles; the fabric headroom was expanded and the failing case rerun successfully. Final code also includes a raised-pile hit area and prevents Fit from trapping arrow-key input.
- Mac and Windows CI passed for runtime commit c805965. Final documentation-only CI is recorded on PR35. Native Windows graphical playtesting and human visual/rules acceptance remain separate.

Screenshots: `docs/screenshots/tabletop-hud/after.png`, `expanded.png`, `numeral-tokens.png`, `pile-counts.png`. `before.png` is the prior combined preview. Before/after screenshots show different practice matches; compare the chrome/art, not the card state. The pile-count and token grids are explicitly illustrative native presentation fixtures.
