# Candlelit command chamber — milestone 1

Alfredo approved `approved-command-chamber.png` on 15 September 2026. This task builds the supporting table and room architecture in Blender, preserving the approved Topaz cloth and lacquer frame. The first review milestone covers proportions, composition, structural silhouettes and preliminary lighting.

Detailed props, live gameplay integration and final material/lighting fidelity remain later passes. Pause for Alfredo’s feedback at each milestone. No Meshy submission was made in this architecture pass. Related issue: #233 / draft PR #234.

## User steering during milestone 1

- Use the connected Meshy API for individual furnishings/props to retain their aged look. The cabinets here establish placement; their final geometry, textures and patina come through the prop pipeline. Do not treat the geometric placeholders as approved final art.
- The first room felt claustrophobic. The revision widens the room to approximately 4.3 m and moves the rear wall back. Main-room depth is 4.35 m, plus a 1.1 m vestibule. Table size remains 1.8 × 1.013 m. The preview lens widens from 34 mm to 30.5 mm to show the extra space; live camera settings are untouched.
- Arrange furnishings using the reference and judgement: armour/storage on the left, scroll/storage on the right, low perimeter work surfaces for map/tea and scrolls. Detailed prop anchors are named empties in the Blender file.

## Model and review

`command-chamber-m1.blend` contains separate architecture, joinery, furniture proxies, approved surface, light and review-camera collections. Cloth height is 0, floor is −0.58 m. The front wall and ceiling are omitted for the cutaway review. The exported GLB includes only new room meshes and placement anchors; Godot loads the existing mat/frame separately.

The approved image is unchanged; its SHA-256 is in `approval.json` and `layout-m1.json`. The original mat and frame geometry/UVs are reused from PR #232. Room wood is a temporary procedural material study, not source-derived worn PBR. It must not be presented as a 1:1 final room or weathering result. The room screenshot is not projected onto scenery geometry. No cards or HUD are baked into the model.

Run from the checkout root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/build_war_room.py
bash 'tools/blender/Review War Room.command'
bash tools/blender/check_war_room.sh
```

In the native review, 1 selects the composition, 2 the whole room, 3 table/joinery. R toggles the source, wheel zooms, drag pans, Home resets and Escape closes. Captures ignore interactive input and hide the reference before writing. The HTML comparison lives in `docs/screenshots/war-room/milestone-1/index.html`.

## Milestone boundaries

This is an isolated native Godot architecture viewer. It does not change the game’s main scene, board projection, card layout, rules, camera controls or menus. Live match integration and full camera/input regression testing are the next milestone after user feedback. Final Meshy props, room material maps, relighting, candle animation and performance profiling remain later. Native Windows visual acceptance is not claimed.
