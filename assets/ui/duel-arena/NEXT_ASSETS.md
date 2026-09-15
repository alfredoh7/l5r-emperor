# Image 3 — asset briefs and current status

Overall target: `approved-generation-3.png`. The user generates new images externally in ChatGPT using their chosen model, returns the results to this conversation, and approves each direction before 3D conversion. The two briefs below are retained as provenance. Alfredo has now returned both images and initially requested manual Blender construction, then replaced that lantern study with supplied Meshy Blender files. `approved-duel-mat.png` is applied directly and `approved-stone-lantern.png` is the image target for the supplied Meshy model in iteration 3. The lantern was supplied manually. The subsequent pine pass verified direct Meshy submission and retrieval; see the current status below.

## Production split

| Component | Authoring route | Acceptance check |
|---|---|---|
| Scroll surface and indigo border | Supplied flat image; complete proportional UV on one mesh | No baked perspective or lighting; full playing area remains quiet |
| Rollers, lacquer plinth, rail, simple pavilion shapes | Direct Blender geometry | Accurate silhouette, useful dimensions and independent transforms |
| Stone lantern | Supplied textured Meshy file → Blender scale, pivot and aperture cleanup | Lantern opening remains a real cavity; separate flame/light element |
| Japanese pine | Supplied primary/reverse references → direct Meshy API → local high-source simplification → v4 | In review: readable at the gameplay camera; some needle clusters remain solid surfaces |
| Garden rocks | Future approved individual references → direct Meshy API → Blender cleanup | Readable silhouettes at actual camera distance; no intrusion into cards |
| Distant moonlit garden | Decide after layout review whether geometry or an approved distant backdrop is justified | Clearly distinguish any 2D backdrop from actual 3D assets |
| Light, mist, shadows and reveal transitions | Godot | Judge in native captures, including reduced motion |

One unit in the blockout corresponds to 100 existing card-plane units. Any later props must be fitted to their allocated footprints, not force the arena to expand around arbitrary generated scale. Base pivots go at ground contact. Retain source meshes and textures separately; cleanup exports use GLB with materials embedded.

## Prompt A — flat silk artwork (first after layout acceptance)

Attach image 3 as the visual reference, then paste:

> Using the attached approved duel-arena image, create only the flat ink-and-silk surface artwork that lies between its two wooden rollers. Preserve the reference's smoky ivory silk, faded monochrome mountains and pines along the upper edge, sparse corner branches, quiet unfinished ink circle at the center, and narrow indigo brocade perimeter. Produce a perfectly front-facing, orthographic top-down rectangle, approximately 2.1:1 landscape. It must work as an albedo texture on a real flat mesh: no camera perspective, contact shadows, directional illumination, ambient occlusion, highlights, curled edges or physical thickness. No rollers, table, buildings, garden scenery outside the mat, cards, slots, controls, lettering or emblems. Keep both the left and right playing areas broad, calm and equally prominent. Keep all ink illustrations outside those quiet areas and avoid making the central circle darker than the source. Show the complete rectangle with no crop. Match the approved material and brushwork closely rather than inventing a new design. Output the largest clean image available.

This is a source asset proposal, not a request to tile/repeat the whole illustration. The accepted result has a continuous painted border; iteration 2 keeps it intact rather than cutting or duplicating its ornaments. Geometry and UVs remain editable.

## Prompt B — stone lantern reference

Attach image 3 and the selected flat-silk result for palette context, then paste:

> Design one weathered Japanese stone garden lantern consistent with the small lanterns framing this approved moonlit duel arena. Show one complete freestanding object against an even neutral-gray background in a clear three-quarter product view. It has a low square foot, a compact sturdy shaft, a hollow square light chamber with real openings, and a broad restrained hipped stone cap. Dark gray stone, subtle age and lichen concentrated in crevices, dignified proportions, no ornate fantasy spikes. Use soft neutral studio illumination with minimal cast shadow so its geometry and material can be reconstructed in 3D. The chamber is empty: no flame, glow, fire, smoke, or baked light. No ground, scenery, neighboring objects, text or border. Keep every edge and the entire base visible. This is an individual game asset, so prioritize a coherent buildable shape over cinematic effects.

After selecting the object, ask for front, side and rear views of that **same** lantern. Keep each as an individual image with identical scale and neutral illumination; do not generate three unrelated redesigns. Review shape consistency before sending references to Meshy.

## Meshy / Blender handoff

Alfredo supplied both Meshy Blender files directly. They are retained in `assets/table/duel-arena/meshy/`; iteration 3 imports the textured file without a new service job. He subsequently approved Meshy as the standard route for detailed props and supplied API access. Use `python3 tools/meshy.py` for future approved assets; authentication is verified, and the private credential is stored in macOS Keychain. Follow [MESHY_WORKFLOW.md](../../../docs/MESHY_WORKFLOW.md) for direct submission, retrieval, provenance and cleanup. Manual service-export transfer is no longer required on this Mac. Keep the current supplied lantern. On 2026-09-15, the pine pass verified the first new paid asset end to end: two tasks (60 total credits), one rejected result, then a complete tree cleaned in Blender and assembled in v4. Original prompts and image hashes are in `pine-prompts.json`; task provenance, the preserved source, cleanup and private source-archive recovery are under `assets/table/duel-arena/meshy/pine-v2/`. Pine visual acceptance, garden rocks and detailed architecture remain next; no rocks were generated.

Once an asset is available: inspect base-color, normal and roughness textures; correct dimensions and orientation; clean geometry while preserving silhouette and UVs; set base pivots; and export the cleaned model through GLB. These checks follow [Meshy's cleanup workflow](https://help.meshy.ai/en/articles/16100257-meshy-to-blender-cleanup-workflow). Runtime export follows [Godot's recommended glTF pipeline](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/available_formats.html).
