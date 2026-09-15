# Sparse fortress hall — approved B3

Alfredo approved the chair-free, sparse fortress hall on 2026-09-15 and requested implementation through Meshy, Blender and Godot, with particular attention to lighting. This **supersedes the M1/M2 command-chamber design**. The older source, authoring file and captures are retained as history; they are not the current art target. Issue #233 / draft PR #234 owns this work.

The approval and exact source hash are in `approval-b3.json`. `approved-fortress-hall-b3.png` is the unmodified approved image. No chairs, carpet, armour display, banners, rear table, loose scroll piles, foreground side cabinets or new furnishing categories are added.

## Current implementation

- Editable low table support, hall floor, columns, beams, open bays and veranda rails, empty stepped dais, map screen, sparse perimeter chests, weapon racks and left brazier.
- The original Topaz mat and approved lacquer frame remain separate unchanged artwork/geometry. Card layout, full faces, rules and input camera are unchanged.
- Actual approved pixels provide the campaign map and distant scenery. Scanned, licensed CC0 timber provides floor/beam texture and normals; provenance is in `materials/provenance.json`. No cards or UI are projected across the room.
- Meshy generated two distinct fixtures: the bronze tabletop lantern and rectangular room lantern. Three more separate jobs provide the aged structural column, closed perimeter chest and iron brazier. These replace the corresponding simple geometry; they add no new furnishing categories. Each has an original receipt, cleaned GLB and editable `prepared.blend` under `meshy/`.
- Static practical lights, amber inner window surfaces, candle interiors, subtle halation and a stationary brazier flame are native Godot elements. New room animation is deliberately deferred. Existing cloth/card candle presentation and reduced-motion behavior remain intact.
- `FortressHall` owns scenery and light only. It receives no match or hidden state. `WarRoomStage` matches the existing `TableSurface` projection and relocates the two physical table fixtures using the established lantern positions.

The full room composition and the normal gameplay camera are different views. The normal view retains card size and UI space; it does not shrink the game solely to reproduce the concept's wider framing. The comparison gallery labels this explicitly. It also includes native close-ups of both Meshy lanterns.

## Authoring and reproduction

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/build_fortress_hall.py
bash 'tools/blender/Review War Room.command'
bash tools/check-war-room-live.sh
bash 'Play Latest.command'
```

`fortress-hall-b3.blend` is the editable assembly. Repeated Meshy fixtures link to the separate `meshy/*/prepared.blend` libraries. Their editable transforms live in the room assembly, while geometry and packed textures remain in the individual source files. Keep this directory structure when moving the authoring files. Meshy source downloads are retained in ignored `artifacts/meshy/`; immutable hashes, task IDs, exact texture prompts and returned credit charges are in the checked-in receipts. Normal builds and play do not need Meshy access. The connected credential remains in Keychain and is not part of this repository.

Each cleaned prop is normalized to a unit height and grounded pivot. Original UVs and 4K colour/PBR images are preserved. Excess triangulation is reduced offline to approximately 60,000 triangles per prop. The room fixture's generated electric bulb was removed; native wax and flame replace it. The brazier retained low coal-like interior forms despite the request for an empty bowl; fire/light are still separate native elements. No paid remesh, texture enhancement or animation passes were submitted. The five tasks report 30 credits each: 150 total, balance 1,805 before and 1,655 after.

The source illustration uses corrected architectural verticals while the existing table camera has a fixed .64 pitch. The assembled hall therefore uses an explicit optical shear above the floor, recorded in `layout-b3.json`, to preserve those verticals under the game's existing pan/zoom. Individual prop source files remain upright. This is an art-directed fixed-pitch stage, not a room prepared for arbitrary free camera orbit. The standalone close-up cameras use the same pitch so they test the actual presentation.

## Validation and acceptance boundaries

See `validation-b3.json` and `docs/screenshots/war-room/milestone-3/index.html` for the actual tested revision and captures. Native visual checks supplement camera/input assertions; passing them does not prove a pixel-perfect visual match or human approval.

The current pass is a static room/prop review milestone. The room has real modeled geometry and relightable props, but the landscape is cropped background artwork, with finite resolution and deliberate reframing to fill the open bays. The existing native renderer uses Compatibility; the current lighting is a set of direct practical lights and manual ambient/fill, not baked global illumination. The source's precise material finish, distant vista detail and illumination remain visual acceptance items; do not describe this as a proven 1:1 full-screen reconstruction. New room animation, native Windows graphical acceptance and sustained performance profiling remain separate. Keep PR #234 draft until visual acceptance and relevant CI/review are complete.
