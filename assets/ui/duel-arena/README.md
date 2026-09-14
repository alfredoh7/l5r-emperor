# Duel arena, image 3

`approved-generation-3.png` is the unmodified image supplied and selected by Alfredo in the task conversation on 2026-09-14. Original supplied filename: `codex-clipboard-2241c6b0-bdbc-4793-a8c3-68bbab61f42f.png`. Generated externally by the user; the exact generation model/settings were not independently verified. See `docs/decisions/2026-09-14-duel-arena-production.md` for the accepted workflow.

This pass establishes a measurable Blender blockout and a native Godot layout review. It does not complete #205 or supply final Meshy props. The approved artwork remains the fidelity target; flat-color blockout geometry is deliberately temporary.

## Review and rebuild

- Launch `Review Duel Arena.command` on this Mac, or run `bash tools/review-duel-arena.sh` from a bootstrapped checkout.
- Tab switches between the unmodified reference and the native blockout; R switches face-down/revealed sample cards; C switches 2/6 attachments; B toggles bowing; P reserves sidebar space; mouse wheel changes camera zoom; F resets fit; Escape closes.
- Click a visible sample card to inspect its full face; click the large face to dismiss it. Decision buttons are placement samples and are deliberately disabled. No duel command is sent and no outcome is calculated.
- Blender source: `assets/table/duel-arena/duel-arena-blockout.blend`. Runtime GLB: `src/L5R.Client/Art/DuelArena/duel-arena-blockout.glb`. Measurements and named anchors: `assets/table/duel-arena/layout.json`.
- Rebuild in a separate factory process: `/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python tools/blender/build_duel_arena.py`. This never touches another open Blender document.
- The source file stays outside the Godot runtime directory. The review resolves the reference from the checkout; it is a development scene, not an exported match feature.

## Camera findings

The model uses the existing `TableSurface.Pitch` and `TableSurface.Depth`. The first native view cropped the near roller/plinth ends. The revised court sits 1.2 Blender units further from the camera, with a fit density of 0.8. It now leaves clearance around the scroll and room for the fixed hand/controls. Personality and attachment cards share the same physical face height.

Native source comparison shows that the blockout still has a stronger trapezoidal perspective than the approved illustration, simpler silhouettes, no ink artwork and no final atmospheric lighting. It is an honest composition check, not a fidelity-complete replacement. Keep the actual approved artwork visible via Tab during subsequent work.

The 1280×800 stress view fits six attachments per unit, mixed bow states and all eight revealed focused cards. The reserved sidebar substantially reduces the scale of the playing area. Recommendation: use the full available width during a duel and inspect cards on demand. This recommendation has not changed the live match UI.

## Validation, 2026-09-14

- Blender 5.2.1 background build/export passed: 37 independent meshes, 3,372 triangles, five placement anchors. No Meshy models were generated or imported.
- Pinned .NET 10.0.400 client compilation: 0 warnings, 0 errors.
- Normal main-scene headless smoke passed. The dedicated clickable review command was exercised through build, native import, capture and projected click check; passed.
- Native Godot 4.7.2, Mac OpenGL Compatibility: 1600×900 normal, 1280×800 crowded/bowed/revealed with sidebar clearance, and 1920×1080 revealed.
- Imported Blender anchors and the existing card projection agree within 0.0002 screen pixels. All fixture faces stay inside the silk and the viewport; fixed hand and controls stay inside the viewport.
- Synthetic native mouse press/release reached the projected Personality exactly once and opened the correct full face in the normal and crowded runs. These are native input checks, not human gameplay acceptance.
- Captures and machine-readable measurements: `docs/screenshots/duel-arena/`. Example reproduction after building/importing: `"$GODOT" --path src/L5R.Client --resolution 1280x800 res://DuelArenaReview.tscn -- --capture=/absolute/output.png --crowded --bowed --revealed --sidebar --check`.
- First fresh **headless editor import** crashed while importing the existing Alegreya font. A native editor import succeeded; the review launcher uses that native import route. The failed initial import and ensuing missing-resource run are not passing evidence.
- Rules, live multiplayer, privacy enforcement, real duel sequencing, native Windows gameplay and final art acceptance were not tested by this layout review.

Next work follows `NEXT_ASSETS.md`: external image generation of the flat silk artwork, human acceptance, individual Meshy assets, Blender cleanup, then native presentation integration coordinated with Mendon's mechanics work.
