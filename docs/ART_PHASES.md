# Focused art passes

The visual standard is `assets/concepts/D-playable-lacquer.png`. Each pass is reviewed in the engine before expanding its scope. Functional checks do not substitute for visual comparison. A faithful scenery render alone does not demonstrate a finished game interface.

**Current status:** Version 0.3.10 replaces the drawn phase frame with bitmap lacquer/brass artwork based on the supplied concept, using live Musashi Brush labels. The current table has expanded navy fabric, separate upright 2.5D lanterns, animated warm light, physical piles, complete card scans, and pan/zoom. See [the phase-bar report](PHASE_BAR_ART.md). The user intends to pause broad art passes after this review and proceed to [complete-match and multiplayer acceptance](NEXT_PHASE.md). The environment-only checkpoints below are historical.

## 1. Table and background — previous review checkpoint, September 6, 2026

Built-in Imagegen produced three environment iterations from the concept. The selected plate retains the dark walnut, olive cloth, antique brass corners, candle, brush tray, and warm painted shadows. Live cards, numbers, arrows, and controls are absent from the artwork. Exact prompts, references, and output filenames are saved in [the prompt manifest](../assets/table/phase-1/prompts.json).

This is deliberately a **pre-rendered 2.5D environment**. Blender MCP built a textured projection mesh, locked review camera, and four cloth-corner placement anchors. The scenery's depth, candle, props, and shadows are painted into the image; they are not separately modeled or dynamically illuminated. The Blender file packs the source image. Godot imports the exported GLB and presents it in a separate native review scene.

The plate is suited to a fixed camera. Moving around the table, parallax, animated candle lighting, and cast shadows from live cards would require additional work. Resolution is 1586 × 992; a larger production asset remains a later requirement if close inspection or higher display resolutions reveal softness. The cloth still has fine decorative texture; the third iteration reduces the dominant brocade and strengthens edge foliage, but does not produce perfectly plain fabric. Its readability must be checked again under actual cards.

### Review and evidence

- Launch `tools/review-table.command` on this configured Mac. Escape closes the isolated scene. It does not enter a match or replace the existing packaged game.
- Selected artwork: `assets/table/phase-1/environment-v3.png`.
- Blender source: `assets/table/phase-1/table-art-stage.blend`.
- Rebuild script: `tools/blender/build_table_art_stage.py`, run through the existing Blender MCP adapter.
- Godot scene: `src/L5R.Client/TableArtReview.tscn`.
- Native capture: [table at source resolution](screenshots/table-phase-1.png).
- Aspect check: [table at 1920 × 1080](screenshots/table-phase-1-wide.png). The entire plate remains visible with side margins; it is not stretched to fill the window.

Client compilation passed with zero warnings and errors. GLB import and both native captures completed without reported errors. The 1586 × 992 native RGB capture matches the selected PNG exactly (mean and maximum channel difference: zero). This measures **plate-to-render preservation**, not similarity to the original full-game concept. The screenshots were also visually inspected for framing, edge cropping, and loss of detail. No full gameplay regression run was needed for this isolated scenery pass.

Godot Compatibility's approximate sRGB conversion initially darkened some shadow tones. The isolated unshaded material compensates for that conversion at exposure 1. Its calculation follows Godot's [GLES3 tonemap source](https://raw.githubusercontent.com/godotengine/godot/master/drivers/gles3/shaders/tonemap_inc.glsl). Exact preservation is verified on this Mac's Godot 4.7.2 Compatibility renderer. Other rendering backends and Windows have not been visually verified for this scene. Recheck the material when changing renderer or engine version.

The playable table, HUD, card controls, and distributed v0.3.0 packages have not been replaced by this review. Integration must preserve the environment composition instead of squeezing it around the old dashboard layout.

## 2. Cards and spatial presentation

Place complete card scans on the established table. Establish readable resting size and hover inspection, believable edges and contact shadows, smooth sideways bowing, legible attachment stacks, clear province and battle assignments, and attack arrows. Compare sparse and crowded boards with the concept before adding further decoration. Keep cards and game state live; never bake them into the background. Confirm that no card scan is cropped.

## 3. Interface framing and beginner guidance

Rebuild clan banners, resources, phase controls, hand, and inspector around the same composition. Match typography, materials, proportions, and spacing. Add contextual hover explanations and next-action guidance without covering critical board relationships. Tooltips must use rules and action context that a future scripted tutorial can reuse.

## 4. Motion and integrated acceptance

Polish card movement, selection, attack intent, feedback, audio, and transitions. Review complete gameplay captures against the concept at the same resolution, plus crowded boards and alternate aspect ratios. Verify playability, beginner clarity, and performance before producing replacement game packages. Dynamic effects are separate from the painted lighting and need their own visual check.

## Later: scripted tutorial

Build guided scenarios after the visual and interaction conventions settle. Use the real rules engine and shared contextual explanations so tutorial instructions match normal play.
