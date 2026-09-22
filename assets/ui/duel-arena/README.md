# Duel arena — iteration 6

The visual target remains Alfredo’s approved `approved-generation-3.png`: the open ink-and-silk scroll without printed card slots. The supplied flat mat, lantern and primary/reverse pine images remain unchanged. Images were generated externally by Alfredo; their model/settings are not independently verified here.

## Garden, architecture and atmosphere

Iteration 6 follows the phased plan in `docs/DUEL_ARENA_PHASES.md`. One Meshy boulder candidate supplies asymmetrical rock groups; photographed CC0 timber, stone and moss maps supply surface detail. Blender builds an uneven bank, a shallow stream, stone paving, irregular lantern planting beds, timber/shoji pavilions, physical curved roof tiles, lacquer rails/fittings, a deeper lacquer plinth and small blossom details. The supplied mat, lanterns and complete central pine remain unchanged in the v5 base.

Godot grades the materials, adds cool moonlight against warm shoji/lantern practicals, sky reflections, restrained lantern halos, low drifting mist and stream ripples. The **distant sky/garden is a flat layer sampled from the approved concept**; it is not represented as modeled geometry. Its scale follows the actual camera so the sidebar does not leave an empty gap. Decorative motifs are sampled from the approved image without assigning invented symbols to player clans. No new image-generation service was used.

The new runtime file is `src/L5R.Client/Art/DuelArena/duel-arena-v6-environment.glb`, loaded alongside the preserved v5 base. The complete editable assembly is `assets/table/duel-arena/duel-arena-v6.blend`; earlier detailed component studies remain in v5. Rebuild the environment with `tools/blender/build_duel_arena_v6.py` (`-- --phase=1` for the garden checkpoint; default phase 2 includes the architecture). The final atmosphere is implemented in `DuelArenaEnvironment.cs` and the named Godot shaders.

This pass used **30 API-reported Meshy credits**: 20 for geometry and 10 for PBR textures. Original output, prompts, hashes and cleanup settings are under `assets/table/duel-arena/meshy/rock-v1/` and `garden-rock-prompts.json`. Runtime rock maps are 2048²; the downloaded source remains unchanged. Surface attribution and hashes are in `assets/table/duel-arena/surfaces/provenance.json`.

Native checkpoint captures preserve the first garden pass, architecture pass, first atmosphere and subsequent corrections under `docs/screenshots/duel-arena/v6/`. The complete central pine and practical card layout are deliberate differences from the concept; this is a native interpretation, not a claim of pixel equivalence. Live duel transitions/decisions and final human art acceptance remain separate.

## Preserved base assembly

- The complete 1817×866 mat is mapped once to a 15.65×7.458943 plane, preserving the full border and original proportions. Original artwork and runtime lossless imports with mipmaps are retained.
- The two supplied Meshy lanterns keep their v3 geometry, UVs and packed PBR maps. Their cleaned chambers have real openings. The source files and earlier cleanup remain in `assets/table/duel-arena/meshy/`.
- At Alfredo’s request, one larger pine replaces the matching pair. It is approximately 6.48 units tall, planted on level garden ground behind the terrace. The complete crown, trunk and roots remain visible in the default view. The rear surround ends at the terrace so the tree is not buried inside the old platform.
- The foliage pass retains 31,817 selected Meshy wood triangles with their coordinates, UVs and corner normals. It removes 188,183 coarse foliage triangles and creates 4,464 needle clusters containing 160,704 tapered needles. The original lower trunk is protected. Wood keeps its original PBR maps; the new needle geometry uses a matte green material with color variation.
- The new tree is 353,225 triangles; the assembly is 27 meshes / 364,413 triangles. The single tree reduces the total scene triangle count relative to v4’s two trees (451,176). This is authored refinement of a generated asset, not a botanical scan or an exact reconstruction.
- The card-plane anchors and mat/lantern geometry remain intact. A wider review framing and lower optical center provide headroom for the whole tree; the 3D stage and cards share that same canvas and retain the existing projection math. Fixture cards use original period scans and exercise focus reveal, attachments, bowing and sidebar space. There are no live duel commands in this scene.

No additional Meshy requests or credits were used for v5. The initial pine pass used 60 credits across two attempts; its original outputs, sanitized provenance and private high-source archive remain documented under `assets/table/duel-arena/meshy/pine-v2/`.

Original Meshy branch simplification remains visible close up. The garden, pavilion architecture and atmosphere are now implemented in v6. Human art acceptance, live duel transitions/integration, full performance qualification and native Windows graphical acceptance remain open.

## Open and review

- `Review Duel Arena.command` builds the client, imports natively and opens the current Godot review.
- **T** compares the refined pine with the supplied primary image; **G** switches to the reverse. Pine comparisons use neutral light and a gray background. **V** clears the cards/HUD. **L** compares the lantern, **M** the mat, and **Tab** the overall concept.
- **R** reveals sample focus cards; **C** switches 2/6 attachments; **B** changes bow states; **P** reserves sidebar space. Mouse wheel zooms, **F** resets fit, **N** toggles reduced motion locally, and **Escape** closes. The review reads the saved default reduced-motion preference; the toggle does not change saved preferences. Click a visible sample card for its full face. Decision buttons are disabled layout samples.
- `tools/blender/Review Duel Arena.command` opens `assets/table/duel-arena/duel-arena-v6.blend` in a separate Blender instance.
- The main v6 Blender scene contains the assembled environment and card-matched camera, with packed images. In the preserved v5 source, **Pine — refined foliage components** keeps wood and needles separately editable, and **Pine — original Meshy source and images** retains the original model and references. The original lantern authoring scene also remains in v5.
- Base runtime: `src/L5R.Client/Art/DuelArena/duel-arena-v5.glb`; environment: `duel-arena-v6-environment.glb`. Dimensions/refinement are in `layout-v5.json`; new environment counts and provenance are in `layout-v6.json` and `validation-v6.json`.

Rebuild the preserved pine base from the committed v4 scene with no Meshy access:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --disable-autoexec --python tools/blender/build_duel_arena_v5.py
```

The builder classifies this particular model using its retained albedo, samples the former canopy envelope deterministically and builds open paired-needle geometry. It never changes another open Blender document or the v4 source. The palette thresholds are specific to this source and require visual review if the source changes.

## Validation and evidence

Current native captures and results are under `docs/screenshots/duel-arena/v6/`; earlier pine comparisons remain under `v5/`. `pine-before.png` uses the original v4 pine in the same neutral comparison setup; `pine-front.png` and `pine-reverse.png` show the refinement. Previous iteration evidence remains in `v3/`, `v4/` and the parent directory. The review’s `--pine-before` option is for baseline comparison; ordinary launch uses the refined scene.

The pinned .NET SDK and native Mac Godot 4.7.2 Compatibility renderer are used. Camera/card checks validate placement and native mouse dispatch, not duel sequencing or hidden-state behavior. Capture checks also project every imported pine vertex and require its full silhouette to fit inside the native play viewport with a margin at default zoom. Captured FPS/primitive counters are brief observations on this Mac, not cross-platform performance certification. Exact checks and remaining limits are recorded in `docs/handoffs/tasks/codex-205-duel-arena-e1746e3b.json`. Native Windows graphical and final human art acceptance are not claimed.

Automated review flags: `--check`, `--crowded --bowed --revealed --sidebar`, `--zoom=1.1`, `--reduced-motion`, `--motion`, `--settle=4`, and `--capture=/absolute/path.png`. `--phase=1` / `--phase=2` disable the final atmosphere for inspection; they do not load historical geometry unless rebuilt at that phase.
