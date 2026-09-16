# Approved three-stat clan placard

Alfredo approved variant A on 2026-09-16, including the worn black lacquer background and aged brass frame. The reference is `approved-a.png`.

Issue: https://github.com/alfredoh7/l5r-emperor/issues/243

Production scope: preserve the approved proportions and texture, use Meshy material production on measured Blender geometry, render the frame for native Godot, and keep all labels and counts live. Hand limit comes from the existing engine calculation. Persistent Home concepts are separate and are not implemented by this change.

## Production and fidelity

`blank-reference.png` was derived with built-in imagegen from the approved panel. Measured Blender geometry was sent to Meshy Retexture (4K PBR), task `01a0a92a-9a18-7286-b6fa-e0bc80c48343`. The service reported **10 credits**. The first material pass was too grey, so the Blender finish restores the approved face artwork by projection while retaining the generated normal/roughness detail and 12% of its base color. The native HUD uses the resulting orthographic PNG; it does not run a 3D viewport or need Meshy at launch.

The hand/fan/blank-coin atlas is an imagegen derivative; digits are live text. The existing clan medallions remain in use. Headings use the actual WeatheredQuill file and body/numerals use Caudex, with local MSDF copies for crisp scaling. These fonts and existing emblems intentionally differ from the generated illustrative lettering/emblem; the comparison is not a claim of pixel equivalence across the entire panel.

The 310 × 108 logical-pixel plaque preserves the shallow source proportions and makes room for three readable values. Following Alfredo's spacing feedback, the clan heading, turn cue and player name occupy separate rows with clear vertical gaps; the heading is slightly smaller and the stats sit lower. The phase track moves 44 logical pixels right to maintain clearance. The artwork has mipmaps, and tooltip frames preserve their corners through nine-patch scaling. `HandLimit` is optional for old snapshots: those display `—` rather than invent a limit.

## Editable sources

- [Private source archive](https://github.com/alfredoh7/l5r-emperor/releases/tag/art-source-placard-a-20260916): original Meshy GLB, packed production `.blend`, derived reference and sanitized receipt. This is not a playable release.
- `measured-frame.blend` and `tools/art/build_clan_placard.py`: measured geometry and camera.
- `tools/art/placard_render_meshy.py`, then `tools/art/placard_refine_render.py`: orthographic bake and fidelity correction. Restore the original GLB to `artifacts/meshy/placard-a-v1/download/model.glb` before running them with Blender.
- `provenance.json`: hashes, options and reported cost. `imagegen-prompts.json`: exact generation and refinement prompts.

## Review and validation

See `docs/screenshots/clan-placard/index.html` for the source/native comparison, actual integrated-room captures and three **unapproved** white Imperial Favor concepts. No persistent Home area is implemented here. Generated Home card lettering and inset arrangements remain illustrative; implementation must use the real scans and engine state.

`bash tools/check-clan-placard.sh` rebuilds the native comparison and checks clan inspection input, keyboard focus, modified limits, over-limit presentation and old snapshots. `bash tools/check.sh --skip-client` covers the shared engine/view contract and existing gameplay suites. Integrated room evidence was captured in the separate `codex/243-hand-placard-preview` branch so this PR does not absorb unrelated room/menu work. Windows CI compilation is not native Windows graphical acceptance.
