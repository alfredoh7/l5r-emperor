# Tabletop HUD refinement

Task: [#34](https://github.com/alfredoh7/l5r-emperor/issues/34). Follow-up PR: [#35](https://github.com/alfredoh7/l5r-emperor/pull/35).

## Behavior

- Force/Chi metal tokens keep their approved materials, show only their current number, and use Musashi Brush. Color, relative placement, hover explanations and engine-bound values remain available.
- Fit, Menu and Rules are custom engraved icons at the top right. Fit retains Home, mouse-wheel zoom and drag/arrow panning. The existing battle-focus action is a crossed-swords icon beside them, visible during a battle. The +/- and My side buttons are removed.
- The separate turn banner is hidden. The active turn owner is identified in its Clan plaque, independently of the player currently required to act. The latter gets a stronger glowing frame and action/response wording. A turn owner waiting for a response retains a quieter outline. Reduced motion keeps the glow steady.
- Honor uses a custom folding fan. Gold uses the supplied complete koku coin with its center cutout. Counts are public values from the existing presentation path; this pass does not change resource calculations.
- All seven supplied Clan marks have atlas regions. Lion/Crane are the currently playable Clans; adding art is not adding playable decks. A runtime patina preserves the mark designs while reducing saturation and warming the surface.
- Deck/discard D/F markers are removed. Existing tooltip names/counts and public discard inspection remain. Piles have a separate dark core, worn paper lip and small deterministic edge variation per card. Height follows the existing count interpolation; no deck identities or shuffle order are used to draw a pile.

## Dependencies and build

This branch starts from main and incorporates pending clarity PR33 and token PR32. Its review comparison is stacked on PR33. Merge/rebase in dependency order; do not merge this directly while bypassing required parent reviews. The separately assembled local playable preview additionally includes menu PR9. Main and earlier launchers remain unchanged.

## Verification

Run `bash tools/check.sh`, then `bash tools/check-tabletop-hud.sh`, `bash tools/check-card-clarity.sh`, and `bash tools/check-table-camera.sh` on a native desktop. The tests do not replace visual inspection or expert rules acceptance.

- `TabletopProbe.tscn` is a clearly labeled presentation fixture, never loaded by normal match setup. It shows both deck backs at 0/1/8/20/40 cards, all seven supplied marks and response/turn glow separation. It checks fractional draw height (including 1→0) and reduced-motion settling.
- HUD captures click Fit/Menu/Rules through native input and require unchanged game state and responsive button bounds.
- Existing stat-token probes cover 0/1/3/6 attachments, upright/bowed states, zero and one/two/three-digit numerals, updates, clicks and hover explanations.
- Existing clarity/camera checks exercise actual match layouts, hand selection, enlarged-card overlap, bowing, attachments and panning/zoom.
- The tactile capture submits actual Border Keep, discard and end-phase draw commands and checks movement/count settling.

Final outcomes and screenshots are recorded in the task handoff and PR. Windows CI/export is not native Windows graphical testing.
