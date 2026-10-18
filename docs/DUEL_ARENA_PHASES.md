# Duel arena — garden, architecture and atmosphere

Historical moonlit-garden direction. The approved Crimson Standoff concept supersedes this plan; see [CRIMSON_DUEL_PHASES.md](CRIMSON_DUEL_PHASES.md) for current implementation and evidence.

Task: [#205](https://github.com/l5r-emperor-dev/l5r-emperor/issues/205), draft [PR #220](https://github.com/l5r-emperor-dev/l5r-emperor/pull/220). Direction authorized by Alfredo on 2026-09-15: proceed in phases and subphases, compare actual in-game screenshots with `assets/ui/duel-arena/approved-generation-3.png`, and iterate. The complete central pine supersedes the reference's paired side trees. Preserve its full silhouette and the supplied mat/lantern assets. Live duel integration remains a separate stage.

## Phase 1 — garden

1.1 Establish asymmetrical boulder groups and a planted bank around the central tree; keep the root visible and the playing area clear.
1.2 Generate one Meshy garden-rock candidate from a brief derived from the approved composition, inspect its silhouette, then texture and clean it in Blender. This task authorizes proceeding from the existing concept without requiring a new externally generated reference image.
1.3 Add smaller varied stones, moss transitions and sparse petals. Inspect the actual Godot camera before committing to detail.

## Phase 2 — architecture and foreground

2.1 Replace pavilion boxes with editable timber frames, open verandas, shoji panels and steps.
2.2 Model curved roof profiles, individual tile courses, eaves and beams; refine the rail and metal fittings.
2.3 Give the lacquer, timber, stone paving, scroll rollers and foreground cloth credible surfaces. Reuse approved ornamental pixels where applicable; the reference's invented heraldry is decorative, not an approved clan identity.

## Phase 3 — atmosphere

3.1 Match cool moonlight against warm lantern/shoji light while retaining readable card artwork and the pine silhouette.
3.2 Reuse the approved distant sky/lake artwork as an explicitly documented background layer. Foreground garden, pavilions, tree, lanterns and scroll remain real geometry.
3.3 Add restrained mist, water shimmer and ember/light movement in Godot. Reduced motion freezes time-dependent effects. Keep effects out of the card reading area.

## Phase 4 — compare and iterate

4.1 Save a native capture after each major pass and compare geometry, material scale, composition, warm/cool balance and atmosphere with the approved image.
4.2 Correct visible gaps, clipping, repeated silhouettes, excessive contrast and distracting effects; retain earlier captures as evidence of iteration.
4.3 Check normal/crowded cards, bowed/revealed samples, sidebar, detail views, zoom and reduced motion. Validate full tree bounds, card projection and native card clicks. Record actual build and CI results separately from human art acceptance.

## Progress and acceptance

Phases 1–3 are implemented in the v6 Blender/Godot presentation. Phase 4 includes five preserved intermediate captures plus final native normal, crowded, clear, zoom and neutral pine comparison views in `docs/screenshots/duel-arena/v6/`. The comparison gallery shows the approved image beside the final native image without cropping either.

The first comparisons exposed an over-bright moss bank, blue roof tiles, a hard background seam, repeated-looking boulder placements and edge-on blossom glints. Subsequent passes darkened materials, varied exposed rock faces, blended the far bank into mist, added lacquer depth and approved ornaments, and tilted blossom faces toward the camera. The crowded view exposed a gap below the distant image; its framing now follows the actual camera. The stream's initial regular highlights and visible far cap were softened into the mist, and vertical planting edges use their own texture coordinates.

Final local checks:

- Pinned client compilation: 0 warnings and 0 errors. Native Mac Godot import and main-scene headless startup pass.
- Normal 1600×900 and crowded 1280×800 native captures: 14/22 card faces within the viewport and silk; bow/reveal/sidebar clearance; native projected clicks open the correct sample card exactly once. Maximum camera error is 0.000123 pixels. The same card checks pass at 110% zoom.
- Every actual pine vertex fits with a margin at default zoom, including the crown and roots. Deliberate zoom enlarges the whole presentation and can crop scenery; Fit restores the complete-tree composition.
- Reduced-motion captures at 1.5 and 4 seconds are byte-identical; animation-enabled capture advances the effects clock and changes pixels.
- Blender comparison confirms all five retained base meshes keep identical geometry, UVs and transforms, and all seven original anchors retain their locations. Twenty-one original artwork/v5 runtime files remain byte-identical to the preceding commit. Meshy source and nine CC0 surface hashes match provenance; all 15 new extracted textures are lossless with mipmaps.
- New environment: 83 mesh instances, 279,894 triangles. The original pine is loaded once from v5. Source Blender file is 94,205,563 bytes; environment GLB is 45,282,368 bytes. Meshy consumption: 30 API-reported credits.

Exact capture metadata and source checks are in `assets/table/duel-arena/validation-v6.json`. The result follows the concept's ink scroll, dark roofs, warm windows, wet stone and misty garden depth while preserving the later request for a complete central pine. It is a native interpretation rather than pixel equivalence. Final human art acceptance, live duel integration/transitions, controlled performance qualification and native Windows graphical acceptance remain open; a composition fixture does not test a match.
