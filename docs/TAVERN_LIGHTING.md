# Tavern lighting and brass phase track · 0.3.9

The phase bar follows the supplied original concept: dark lacquer, fine brass rails, serif capitals, circular medallions, and feathered gold illumination. The four segments represent the engine’s Action, Battle, Dynasty and End phases. Attack declaration, defense assignment and battle selection share the Battle segment; Dynasty discard shares Dynasty. Automatic straighten/reveal processing is explained in the Action tooltip. The concept’s decorative “Emperor” label does not introduce an additional game phase.

Active illumination crossfades over 0.45 seconds and breathes gently on a 5.8-second cycle, with only 7% intensity modulation. Continuous radial gradients avoid both abrupt rectangular edges and seams between rail samples. Reduced motion freezes the lighting and switches phase emphasis immediately.

The lantern distortion came from projecting already angled props as flat mat artwork. Imagegen removed both baked lanterns from the existing expanded mat and generated a separate transparent lantern. Blender MCP rebuilt the packed environment asset. Godot now anchors upright lantern billboards to the table using uniform perspective scaling, preserving their image aspect while panning and zooming. Mipmaps prevent the ornate metal detail from sparkling at a distance.

Warm amber illumination falls across the mat and nearby card faces. Local contact shadows ground the feet; light through the paper panes, a small halo and broader light pools vary slowly by a few percent. The left and right sources have offset rhythms. Card reading previews retain their stable presentation. This is a layered 2.5D environment with shader lighting, not a fully modeled room or physically simulated light transport.

The expanded navy mat, native camera/input mapping, original card scans, card sizes and physical layout are preserved. Windowed play remains available.

## Assets

- Clean image: `assets/table/phase-8/navy-mat-v4.png`
- Transparent prop: `assets/table/phase-8/tavern-lantern.png` (original RGBA preserved)
- Exact built-in imagegen prompts: `assets/table/phase-8/prompts.md`
- Blender scene: `assets/table/phase-8/navy-table-v4.blend`
- Blender MCP builder: `tools/blender/build_navy_table_v4.py`
- Runtime: `Art/navy-table-v4.glb`, `Art/navy-mat-v4.png`, `Art/tavern-lantern.png`, `Art/navy-table.gdshader`

## Review

Native screenshots and logs are retained in `artifacts/tavern-ui/`. The camera suite covers a developed 39-card game, battle zoom, 16:10 and ultrawide windows, assignment, attachment inspection, pile hover, and reduced motion. Board corners and pile faces still have at least 88 logical units of navy clearance. Reduced-motion frames at different light times are byte-identical.

The ambient video records the native renderer at 12 samples per second. The separate transition recording exercises real Border Keep, Dynasty discard and end-phase draw commands and captures phase crossfades alongside the existing card animations. Packaging validates required art resources and managed runtimes; the Mac package receives a native visual launch. Windows export validation does not substitute for a native Windows playtest.
