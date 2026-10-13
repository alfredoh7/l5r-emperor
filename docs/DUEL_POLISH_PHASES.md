# Duel presentation polish — 2026-09-17

Issue #205 / PR #220. Alfredo requested phases 1–5 sequentially. Preserve the accepted close mat, original card artwork, Musashi headings, main-game inspection, silence, grass-free introductions and engine-owned decisions. No new permanent placards or stat counters.

1. **Physical cards:** contact shadows, fine paper edges, layered Fate decks and attachment separation. Inspect actual native table and close-up.
2. **Focus gesture:** travel from the permitted source, restrained lift/settle, exact destination and a brief readable commitment. Respect reduced motion, privacy, pause and duplicate-input protection.
3. **Inspection:** reuse CardPreview; choose its position by protected regions rather than covering the confrontation. Retain readable size, immediate dismissal, click/deselect and seat privacy.
4. **Atmosphere:** replace isolated tufts with layered field density and coherent gusts; preserve the painted composition and fixed sun/mountains/mat while improving sky motion. Introductions remain free of 3D grass.
5. **Frame pacing:** profile actual completed frames; reduce unnecessary viewport/card reconstruction and first-use costs. Re-run native normal/compact/reduced scenarios, inspect recordings, report measured limits.

Native captures, meaningful input checks and separate real-time measurements are evidence. AAA quality is the art acceptance target; automated checks do not certify it. The checkpoints below record implemented changes and remaining visual acceptance limits.

## Phase 1 native checkpoint

Added duel-only card edge/contact rendering while retaining CardTile’s complete original face, stats, hover and hit polygon. Fate pile depth uses the actual public count and individually shaded edges. Attachment cards cast a restrained shadow onto the next card. The 1920×1080 native idle view was inspected; card positions, mat and printed content are preserved. Evidence: `artifacts/duel-polish/phase1-verified-idle.png`.

## Phase 2 native checkpoint

Focus now travels from the visible permitted hand card or public deck position, turns into its original Fate back, and settles over 640 ms with a short 100 ms hold. The demo host already respects presentation busy time; pause and reduced motion preserve ordinary controls. Opponent hand identities are never requested. A 38.6-second native recording completed four real commands and the canonical 8–7 result once. Evidence: `artifacts/duel-polish/phase2.avi` and its JSON report.

## Phase 3 native checkpoint

The unchanged shared CardPreview now evaluates bounded candidate positions against the source card, champions, focused cards, turn prompt and controls. The 20-step normal native flow passed, including both seats, hover/pinned inspection, deselection, dragging, duplicate clicks and canonical resolution. The sequence exposed and corrected a focus animation replay during private seat switching; snapshots for a changed seat now render without transfers. Destination cards are hidden until landing instead of relying on modulation. Evidence: `artifacts/duel-polish/phase3-check*.png` and JSON.

## Phase 4 composition checkpoint

The gameplay field now uses 384 photographic tufts and 576 fine stems, interleaved through three depth bands in two instanced draws. Root fading, distance opacity and warmer rim values blend into the approved painted field. A shared world-space gust drives both distant field displacement and nearby segmented geometry; clouds use separate slow upper/lower advection with fixed sun/horizon masks. Native 1080p scenery was inspected and the far mat-edge mask tightened. No new grass is instantiated during introductions. The fixed-state and reduced-motion comparisons are recorded with final native verification below.

## Phase 5 frame-pacing checkpoint

The duel's fixed camera now opts into a tighter table raster: 2534×1976 instead of approximately 3917×4852 at 1080p, retaining the same pixel density. The normal pannable board keeps its exact previous default extent. Unchanged card nodes are reused, covered table/grass viewports suspend during introductions, and paused/reduced grass renders only when its static state changes. UI ink/frame assets prepare before interaction. Only engine-revealed artwork is requested asynchronously before the reveal presentation; unknown hand/deck identities are never preloaded.

Separate real-time native runs on the Apple M1 Max, 1920×1080, completed rendered frames after the first three seconds:

| Run | Median | P95 | P99 | Maximum | Frames over 33.34 ms |
|---|---:|---:|---:|---:|---:|
| Before phase 5 | 8.99 ms | 13.48 ms | 13.79 ms | 66.89 ms | 3 |
| Final | 9.05 ms | 13.66 ms | 13.77 ms | 28.96 ms | 0 |

The improvement is the removal of the repeatable opening, first focus-selection and reveal stalls in this run. Steady-state frame time is similar. Screenshot/movie captures are deliberately excluded from this comparison. These are local observations, not a cross-platform performance guarantee.

## Final native verification

`bash tools/check-duel-demo.sh` passed its five paths: normal 20-step flow at 1920×1080; compact reduced 20-step flow at 1120×700; thirteen curated scenarios; normal and reduced 43-step guidance/source-inspection flows. The final run has no runtime errors or warnings. The normal guidance window expands vertically to fit its content (reported 1920×1872); it is not an additional fixed-1080p layout claim. These fixed-FPS input runs include screenshots, so their frame statistics are not performance evidence.

The final 38.63-second native movie contains 1,159 frames at 1920×1080/30fps and completes four real commands, the canonical 8–7 result, one opening/ending, Courtesy and restored priority. Sampled introduction, focus-flight and result frames were inspected. Raw AVI audio remains silent; the local MP4 contains video only. Reduced-motion scenery at 0 and 20 seconds matches pixel-for-pixel. Normal-motion sampled sun, mountain and mat regions remain unchanged while the sky and field change. A normal-board camera capture verifies the unchanged shared table default; the refreshed launcher passes offline artwork preflight and headless startup.

[Validation](archive/duel-demo/screenshots/presentation-polish/validation.json) · [Standoff](archive/duel-demo/screenshots/presentation-polish/standoff.png) · [Inspector](archive/duel-demo/screenshots/presentation-polish/inspection.png) · [Introduction](archive/duel-demo/screenshots/presentation-polish/introduction.png) · [Result](archive/duel-demo/screenshots/presentation-polish/result.png) · [Final timing](archive/duel-demo/screenshots/presentation-polish/performance-final.json). The local moving review is `artifacts/duel-polish/final.mp4`.

## Remaining acceptance

The five implementation passes preserve the approved composition and actual published card scans. The sky remains an animated painted plate, and the close grass uses fixed-camera photographic ribbons plus fine segmented stems. This is a deliberate hybrid presentation, not volumetric weather or a fully modeled distant field. Final subjective grass/cloud and cinematic acceptance still needs Alfredo's review of the moving result. Mendon's mechanical/shared-interface review, the effective-province-strength reporting finding, normal-match activation and native Windows graphical acceptance remain separate from this UI increment.
