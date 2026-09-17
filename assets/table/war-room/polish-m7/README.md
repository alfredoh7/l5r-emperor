# M7 — materials and side enclosure

Latest user direction: vary the worn floor and stone column feet through Meshy, improve the perimeter chests and steps, remove both tabletop lamps and side spear racks, and close the side walls with Japanese screens. The rear mountain bays stay open. This updates the approved B3 reference's furnishing inventory; it does not restore chairs or clutter.

## Production

Four dedicated imagegen references derive from the approved room. Exact prompts, the original reference hash, sanitized Meshy task receipts and credit accounting accompany the assets. Eight Meshy jobs consumed **155 credits**, leaving **1,465**. One image-to-3D dais was rejected because it flattened the steps into a slab. It is retained only in ignored production artifacts, never in the game.

- Three independently textured Meshy floorboards share measured Blender geometry and closely related finish. Deterministic selection, end-for-end rotation and tiny height differences vary the assembly. Runtime adds finer existing CC0 scanned grain; Meshy supplies the broad finish, grain and PBR maps.
- Two Meshy stone feet have different chips and fissures. Blender removes the old foot from the existing timber column while retaining its original pivot and UVs. Native stone is deliberately nonmetallic, matte and low in specular response.
- Meshy retextures the existing chest geometry against the new imagegen reference. Chests are moved slightly inward from the walls and clear of column feet after native close-up inspection.
- A measured Blender dais has three shallow levels, eased treads, end boards and framed risers. Meshy textures that fitted geometry. The initial flattened model is not used.
- Both side walls receive fitted Blender shoji leaves, lattice, wooden lower panels and recessed pulls. Godot adds stationary subtle paper fibres. This measured architectural assembly is not a one-shot generated room.
- Both tabletop lanterns and side spear racks are absent. The removed lanterns' mat, frame and card light pools are disabled as well. Room lanterns and the brazier remain.

Original Meshy downloads stay under ignored `artifacts/meshy/`. Prepared source files and game assets use maps capped at 2K. The floor's geometry and all original UVs are retained. Stone cleanup targets 80,000 triangles per variant; actual counts and hashes are in each `cleanup.json`. The assembled room links the separate editable Blender libraries.

## Iteration and checks

The first native pass had dark floor midtones and sharp highlights. Subsequent captures soften the finish, raise warm ambient detail and improve dais readability. Native close-ups exposed chest/column clearance problems, corrected before the final bake. The floor lighting is rebuilt in Cycles at 128 samples, with separate contact occlusion and local native firelight modulation. It is not whole-room dynamic GI.

Reproduce from the task checkout:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/build_fortress_hall.py
cp assets/table/war-room/layout-b3.json src/L5R.Client/Art/WarRoom/layout-b3.json
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/bake_fortress_floor.py -- --milestone 7 --samples 128 --size 1536
bash tools/check-war-room-live.sh
bash tools/check-war-room-motion.sh
```

See `docs/screenshots/war-room/milestone-7/index.html` and `validation.json` for actual native evidence and current check results. Historical M6 captures are explicitly labelled. The concept is a visual target, not a proven pixel-identical reconstruction: the native playable view retains the established card camera, and the rear vista remains a matte. Arbitrary free orbit, native Windows graphical acceptance and sustained performance profiling are not claimed. Pause for user visual milestone review.
