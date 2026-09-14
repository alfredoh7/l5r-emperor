# Playmat material / frame review — issue 218

The active game uses the 1672×941 `navy-mat-v4.png` on a shader-projected flat plane. The visible narrow wooden edge is baked into that texture. Lanterns are separate. Older `TableView`/`build_table.py` wooden geometry is not the active table.

This pass creates independent Blender fabric and frame objects and a Godot review. The original artwork remains the source of truth. No game layout, camera limits, cards, menus or rules are changed. The live mat is not replaced by an unapproved generated variant.

## Fidelity strategy

- Preserve the original printed design through continuous UVs across fabric and frame. No AI redraw in the proposed asset.
- Add derivative-filtered fine textile detail separately. It resolves at close range and fades below pixel size; it does not restore missing detail in the gold illustration.
- Model a narrow, beveled wooden frame independently, with real depth and separate wood material.
- Keep the cloth material, frame material and geometry independent so later approved mats can replace the cloth without rebuilding the table.

The generated `fabric-reconstruction-reference.png` is an experiment only: it returned 1672×941 despite the 4K request, and changed ornaments. It is not an upscale. See `imagegen-prompt.md`.

## Deliverables

- `navy-mat-separated.blend`: editable fabric insert and narrow beveled wooden frame. Metres, 1.8 × 1.013 m outer dimensions; dimensions are a starting scale, not a new gameplay layout.
- `src/L5R.Client/Art/MatFidelity/navy-mat-separated.glb`: two geometry objects, about 6 KB, independent material slots. The GLB does not embed duplicate texture pixels.
- `src/L5R.Client/Art/MatFidelity/navy-mat-separated.tscn`: Godot-ready scene with separate `navy-fabric.tres` and `wooden-frame.tres` resources. It references the existing artwork and the previously generated walnut texture for the newly exposed sides.
- `tools/blender/build_mat_fidelity.py`: reproducible Blender export. No Meshy generation was needed for this simple measured geometry.
- `tools/blender/Review Playmat.command`: native side-by-side viewer. Keys 1–4 change viewpoints, wheel zooms, drag pans, D toggles detail, Esc exits. No catalogue installation required.
- `docs/screenshots/mat-fidelity/index.html`: native comparison captures; left original, right proposed asset.

Godot shaders use mipmapped anisotropic sampling for the separate asset, with derivative-filtered textile frequencies. The original runtime shader remains at its existing filtering and zero extra detail by default. `Play Latest.command -- --mat-detail-preview` opts into fine threads on the existing playable plane; it does **not** integrate the physical frame. The `--` separator is required.

## Validation / remaining limits

Blender 5.2.1 exported successfully. Godot 4.7.2 Compatibility rendered fit, fabric close-up, frame close-up and top views at 1920×1080. Native client compilation passed with zero warnings/errors. The real playable preview passed existing native camera/input checks (cursor zoom, pan, normal card clicks, fixed HUD and modal isolation).

The first headless editor import crashed while importing a font. A native editor import completed; the final captures contain no resource or shader errors. Import-generated changes to unrelated settings/assets were excluded.

The original artwork remains 1672×941. Fine yarn detail improves the close surface but cannot reconstruct sharp gold motifs at arbitrary magnification. This pass is a faithful **asset structure / material prototype**, not a completed high-resolution master. Side-by-side views use identical asset-review cameras; gameplay evidence uses the actual projected table. The review is not a full scene pixel-equivalence claim.

The top artwork is currently unshaded to retain the approved colours. Full room light response, physical-frame integration with the existing projected card plane, a higher-resolution faithful illustration, and the player-facing mat picker remain later work after review. Mat and frame still sample different UV regions of one approved artwork atlas; they are independent meshes/materials, not newly reconstructed independent 4K image files.

## Review gate

Compare the native source and separated asset at fit, cloth close-up and frame close-up, then review actual gameplay with optional fine-detail shading. Physical room integration and a selectable-mat menu are later steps. A higher-resolution faithful gold artwork master remains desirable; fine fabric shading alone cannot solve unlimited zoom.
