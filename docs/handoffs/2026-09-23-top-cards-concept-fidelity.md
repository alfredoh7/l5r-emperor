# Top Cards concept-fidelity correction — 23 September 2026

Issue #323 / PR #434, continuing `codex/323-review-revisions` after fast-forwarding to Mendon’s integrated 9f8ef9f5. Alfredo rejected the earlier appearance as insufficiently faithful to the selected A layout and B labels. The screenshot in his correction displays B, so an optional clarification was offered; the existing explicit A+B selection was retained absent a changed direction.

The revised panel restores the brush title with live heading text, larger full card faces, a more substantial original deck-back stack, visible empty Hand slot, glowing insertion slot, positional captions, and right-aligned Undo/Confirm. The existing engine contract, counts, legality, private draft, keyboard/click placement, cancellation and privacy behavior remain unchanged. Long source names fit inside the brush strip.

The built-in ImageGen tool derived the lacquer, brush and slot furniture from approved concept A. Runtime asset: `src/L5R.Client/Art/TopCards/approved-trays-atlas.png`. Exact prompt and provenance: `assets/ui/top-cards/provenance.json`. Original card scans/deck backs and existing heading font are retained; this is not a full-screen pixel-equivalence or human acceptance claim.

Current main’s clipping fix (#447 / 78573475) was merged into this task branch. Conflicts retain the preview branch’s modal/review/drag routing, attack tab, battle-progress HUD exclusion, and richer native probe support. `TableSurface` takes main’s instance `PlanePitch`/`PlaneDepth` projection and minimum `CanvasExtent`, preserving the existing duel camera configuration. No changes were merged into main.

## Validation

Final validation is recorded in the branch JSON handoff. Source/native captures and comparison: `docs/screenshots/top-cards-drag/`. Build has no errors; the initial full build reports the inherited unused `BattleTracker._flowArt` CS0169 warning.

## Review

Prepared Mac launcher: `artifacts/L5R Top Cards - Approved Layout.app`; fast entry: `artifacts/Review Top Cards - Approved Layout.command`. They use this checkout’s already-built client and open the Banish All Doubt scenario. `Review Top Cards.command` remains the portable build-and-run entry. `artifacts/L5R Emperor - Latest.app` was refreshed for the checkout.

The local gallery at http://127.0.0.1:8799/top-cards-drag/native/ is updated with the revised native captures. Alfredo’s hands-on acceptance is pending. The Windows release `preview-pr434-9f8ef9f5a5-r1-windows` is immutable and predates this correction; do not treat it as the corrected appearance. No Windows re-export or Windows graphical playtest is claimed for this task.
