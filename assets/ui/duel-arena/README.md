# Duel arena — iteration 5

The visual target remains Alfredo’s approved `approved-generation-3.png`: the open ink-and-silk scroll without printed card slots. The supplied flat mat, lantern and primary/reverse pine images remain unchanged. Images were generated externally by Alfredo; their model/settings are not independently verified here.

## Current assembly

- The complete 1817×866 mat is mapped once to a 15.65×7.458943 plane, preserving the full border and original proportions. Original artwork and runtime lossless imports with mipmaps are retained.
- The two supplied Meshy lanterns keep their v3 geometry, UVs and packed PBR maps. Their cleaned chambers have real openings. The source files and earlier cleanup remain in `assets/table/duel-arena/meshy/`.
- At Alfredo’s request, one larger pine replaces the matching pair. It is approximately 6.48 units tall, planted in a lower garden behind the elevated terrace, with its crown centered behind the mat. The rear surround ends at the terrace so the tree is not buried inside the old platform.
- The foliage pass retains 31,817 selected Meshy wood triangles with their coordinates, UVs and corner normals. It removes 188,183 coarse foliage triangles and creates 4,464 needle clusters containing 160,704 tapered needles. The original lower trunk is protected. Wood keeps its original PBR maps; the new needle geometry uses a matte green material with color variation.
- The new tree is 353,225 triangles; the assembly is 27 meshes / 364,413 triangles. The single tree reduces the total scene triangle count relative to v4’s two trees (451,176). This is authored refinement of a generated asset, not a botanical scan or an exact reconstruction.
- The card-plane anchors, mat/lantern geometry and perspective remain intact. Fixture cards use original period scans and exercise focus reveal, attachments, bowing and sidebar space. There are no live duel commands in this scene.

No additional Meshy requests or credits were used for v5. The initial pine pass used 60 credits across two attempts; its original outputs, sanitized provenance and private high-source archive remain documented under `assets/table/duel-arena/meshy/pine-v2/`.

Original Meshy branch simplification remains visible close up. Human art acceptance, remaining garden detail, pavilion architecture, final atmosphere/transitions, full performance qualification and live duel integration remain open. This iteration completes the needle refinement and single-tree composition requested by Alfredo; it does not finish the entire arena concept.

## Open and review

- `Review Duel Arena.command` builds the client, imports natively and opens the current Godot review.
- **T** compares the refined pine with the supplied primary image; **G** switches to the reverse. Pine comparisons use neutral light and a gray background. **V** clears the cards/HUD. **L** compares the lantern, **M** the mat, and **Tab** the overall concept.
- **R** reveals sample focus cards; **C** switches 2/6 attachments; **B** changes bow states; **P** reserves sidebar space. Mouse wheel zooms, **F** resets fit, and **Escape** closes. Click a visible sample card for its full face. Decision buttons are disabled layout samples.
- `tools/blender/Review Duel Arena.command` opens `assets/table/duel-arena/duel-arena-v5.blend` in a separate Blender instance.
- The main Blender scene contains the assembly and card-matched camera. **Pine — refined foliage components** keeps wood and needles separately editable at the original authoring scale. **Pine — original Meshy source and images** retains the source model and image references. The original lantern authoring scene remains available. Images are packed.
- Runtime: `src/L5R.Client/Art/DuelArena/duel-arena-v5.glb`. Dimensions, placement and refinement settings: `assets/table/duel-arena/layout-v5.json`.

Rebuild from the committed v4 scene with no Meshy access:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --disable-autoexec --python tools/blender/build_duel_arena_v5.py
```

The builder classifies this particular model using its retained albedo, samples the former canopy envelope deterministically and builds open paired-needle geometry. It never changes another open Blender document or the v4 source. The palette thresholds are specific to this source and require visual review if the source changes.

## Validation and evidence

Native captures and results are under `docs/screenshots/duel-arena/v5/`. `pine-before.png` uses the original v4 pine in the same neutral comparison setup; `pine-front.png` and `pine-reverse.png` show the refinement. Previous iteration evidence remains in `v3/`, `v4/` and the parent directory. The review’s `--pine-before` option is for baseline comparison; ordinary launch uses the refined scene.

The pinned .NET SDK and native Mac Godot 4.7.2 Compatibility renderer are used. Camera/card checks validate placement and native mouse dispatch, not duel sequencing or hidden-state behavior. Captured FPS/primitive counters are brief observations on this Mac, not cross-platform performance certification. Exact checks and remaining limits are recorded in `docs/handoffs/tasks/codex-205-duel-arena-e1746e3b.json`. Native Windows graphical and final human art acceptance are not claimed.
