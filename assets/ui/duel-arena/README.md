# Duel arena — iteration 3

Alfredo supplied `approved-duel-mat.png` and `approved-stone-lantern.png` on 2026-09-14. He subsequently supplied Meshy geometry and textured Blender files and requested replacing the manual lantern study. Both original images are retained unchanged. The rejected manual study is archived locally under ignored `artifacts/duel-review-v2/manual-study/`; the review now loads the Meshy version. The overall target remains `approved-generation-3.png` (the open scroll without printed card slots). Generation happened externally through the user; no model/settings claim is independently verified here.

## What is implemented

- The complete 1817×866 mat image, including the navy brocade border, is UV-mapped once to a 15.65×7.458943 plane. The artwork is neither stretched to the previous blockout ratio nor redrawn. The mat PNG and all three original Meshy map files survive GLB export byte for byte; Godot's extracted PNG has identical decoded pixels. Runtime imports use lossless compression and mipmaps. Perspective and filtering still affect its on-screen appearance.
- The lantern uses the user-supplied textured Meshy asset: original geometry, UV artwork and three packed 2048² PBR maps. The source contains 2,126 vertices / 4,051 triangles. Both supplied `.blend` files remain unchanged in `assets/table/duel-arena/meshy/`, with original filenames and hashes in `provenance.json`.
- Blender cleanup normalizes the height to 2.574 units and places the pivot at ground contact. Meshy filled the window apertures with solid panels; two narrow crossed cuts open the chamber while retaining the outer stone artwork, roof, posts and sill. Cut interiors use a quiet stone material. The cleaned lantern has 2,335 vertices / 4,407 triangles. Center rays through both window axes are unobstructed.
- Two linked copies frame the mat with separate light anchors. They share mesh and materials. The assembled runtime scene is 31 meshes / 11,296 triangles. This is the supplied Meshy reconstruction with cleanup, not a scan or a claim of exact photographic geometry.
- The original card-plane anchors and perspective are preserved. Fixture cards use original period scans, with focus reveal, attachment, bow and sidebar stress views. There are no live duel actions or gameplay changes.

The garden, pavilion silhouettes, rail, rollers and plinth remain simple layout geometry. The new mat and lanterns do not mean the entire approved concept has been reproduced. Background detail, final lighting and transitions, live match integration, and human visual acceptance remain subsequent work.

Alfredo has restored Meshy as the default route for detailed props, with direct API access through `tools/meshy.py`. Follow the [production workflow](../../../docs/MESHY_WORKFLOW.md) for future approved assets; the current lantern remains the user-supplied source. Connection setup made no new generation request.

## Open and review

- Click `Review Duel Arena.command` in this task checkout. It builds the current source, imports natively, then opens `DuelArenaReview.tscn`.
- **V** clears the cards/HUD to inspect the assembled scene. **L** compares the native lantern with its source under neutral light. **M** compares the native mat top view with the source at matching scale. **Tab** shows the original overall concept.
- **R** reveals focused sample cards; **C** switches 2/6 attachments; **B** changes bow states; **P** reserves sidebar space. Mouse wheel zooms, **F** resets fit, **Escape** closes. Click a visible sample card for its full face; click the large face to dismiss it. Decision buttons are disabled layout samples.
- Click `tools/blender/Review Duel Arena.command` to open the new source in a separate Blender instance on this Mac.
- Blender source: `assets/table/duel-arena/duel-arena-v3.blend`. The main scene contains the assembly and gameplay camera. Switch to **Meshy lantern — supplied source and reference** for the original imported object and image target. The source and cleaned meshes remain editable. Images are packed into the `.blend`.
- Runtime: `src/L5R.Client/Art/DuelArena/duel-arena-v3.glb`. Measurements, source hashes and limits: `assets/table/duel-arena/layout-v3.json`.
- Rebuild from the retained first blockout in a separate process: `/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --disable-autoexec --python tools/blender/build_duel_arena_v3.py`. It never changes another open Blender document. The v1 builder and `.blend` remain available.

## Validation

This is a development review scene, not an exported match feature. It reads source references from the checkout. Native evidence lives under `docs/screenshots/duel-arena/v3/`; accompanying JSON records camera agreement, fixture counts and input results. The original blockout captures remain in the parent folder as historical evidence.

The review uses native Mac Godot 4.7.2 Compatibility and the pinned .NET 10.0.400 SDK. Tests validate projection/card placement and native mouse dispatch, not engine duel sequencing, privacy or human gameplay acceptance. Native Windows play and final visual acceptance are not claimed. `docs/handoffs/tasks/codex-205-duel-arena-e1746e3b.json` records the actual final checks for this iteration.

A prior first headless editor import crashed on the existing Alegreya font; the launcher uses the native editor import route, which succeeds on this Mac.

Final iteration-3 checks on 2026-09-14: Blender 5.2.1 build/export and Cycles render passed; aperture rays passed. Pinned client build: 0 warnings / 0 errors. Native Mac captures passed at 1600×900 (14 fixture faces, one correct projected click), 1280×800 (22 faces, six attachments per unit, bowed/revealed/sidebar, one correct projected click), and 1920×1080 (revealed and clear views), plus mat/lantern comparisons. Maximum camera difference was 0.000137 pixels. Normal-main headless smoke passed. Both the Godot review launcher and separate Blender launcher were exercised successfully. Source-file and embedded-texture hash checks passed. No live duel, multiplayer, Windows graphical or human final-art acceptance is claimed.
