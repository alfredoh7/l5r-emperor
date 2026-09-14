# Playmat texture and separate-frame review — issue 218

The playable table now uses the approved Topaz artwork at **6688 × 3764**, encoded as **lossless WebP (19,905,220 bytes / 18.98 MiB)**. The user's 13376 × 7528 PNG master remains untouched outside Git. Layout, camera limits, lantern animation and colour treatment are preserved. The painted rim is still part of the playable texture; the physical frame remains a review prototype.

## Approved texture pipeline

Run `python3 tools/blender/prepare_mat_texture.py /path/to/enhanced_navy-mat-v4.png` with Pillow installed. The script validates the expected master dimensions, halves both axes using Lanczos, writes the runtime WebP and verifies that decoding it exactly reproduces the resized RGB pixels. It does not repaint, sharpen, crop or recompose the approved artwork. `texture-provenance.json` records input/output hashes, dimensions, encoding and Pillow version.

Godot imports the texture losslessly with mipmaps and no size cap or automatic 3D compression. The runtime source is 74% smaller than the master, with 75% fewer pixels. The current RGB8 texture plus a full mip chain is approximately 96 MiB before driver overhead (128 MiB if expanded to RGBA8). WebP saves file storage, not GPU memory by itself. The local Godot imported texture is approximately 26 MiB on disk including mip levels.

The active `TableArtStage` loads this WebP by default. `--mat-original-review` retains the old 1672 × 941 image for diagnostics only. Extra procedural weave stays off; `--mat-detail-preview` remains an optional material experiment because the Topaz artwork already contains surface detail.

## Separate geometry, ready for the wood-frame pass

- `navy-mat-separated.blend`: editable fabric insert and narrow beveled wooden frame; metres, 1.8 × 1.013 m outer dimensions. This geometry source still packs the original small atlas; Godot materials override it with the approved runtime texture.
- `src/L5R.Client/Art/MatFidelity/navy-mat-separated.glb`: two geometry objects, about 6 KB, independent material slots and no embedded bitmap duplication.
- `navy-mat-separated.tscn`, `navy-fabric.tres` and `wooden-frame.tres`: independent Godot meshes/materials using continuous source UVs. Newly exposed frame sides use the existing walnut texture.
- `tools/blender/build_mat_fidelity.py`: reproducible Blender geometry export. Meshy is unnecessary for this measured ring and fabric insert.
- `tools/blender/Review Playmat.command`: native side-by-side viewer; left original flat image, right approved Topaz derivative on the separate-frame prototype. Keys 1–4 change viewpoints, wheel zooms, drag pans, D toggles experimental threads, Esc exits. No catalogue installation required.
- `docs/screenshots/mat-fidelity/index.html`: source-to-game evidence and remaining limits.

The earlier `fabric-reconstruction-reference.png` is an unused imagegen experiment. It returned 1672 × 941 and changed ornaments; it was never used as an upscale or runtime replacement.

## Native validation

- Client compilation: zero warnings/errors, including the current main at 0314dcc.
- Godot 4.7.2 Compatibility on Apple M1 Max: native import completed with the full 6688 × 3764 texture. Captures contain no missing-resource or shader errors.
- Matched 1920 × 1080 asset-review views: fit, fabric close-up, corner/frame and top view. Extra procedural detail off on both sides. Native colour, UV composition and corner art inspected.
- Actual gameplay: 3440 × 1440 overview and existing maximum zoom of 2.0; 1920 × 1080 maximum zoom over gold ornament through `Play Latest.command`. Logs confirm the new runtime texture dimensions.
- Existing native camera/input checks passed: cursor-anchored wheel zoom, pan, Home reset, fixed HUD/hand, modal isolation and upright/bowed card edge clicks without match mutations.

The playable captures are independently generated engine fixtures, not identical saved matches. The paired asset viewer is the controlled artwork comparison. The upscale improves the close surface but cannot make the illustration infinitely detailed. Full motion/performance profiling and Windows graphical playtesting are not claimed.

The earlier Mac CI run (34822273287) crashed in the Godot headless importer with a worker-thread scene notification error while importing fonts; this preceded the Topaz texture change. Native import and rendering succeeded. The latest PR checks are recorded in its handoff/PR.

## Next pass

Design and approve the physical wood frame, then refine the Blender frame's width, bevels, corner joinery and worn wood material. Integrate it with the existing projected table only after visual comparison. Avoid a second painted rim by aligning the cloth/frame UV boundary. Full room lighting and the player-facing mat picker remain later work. The frame is independent geometry/materials; it does not yet replace the live table's painted rim.
