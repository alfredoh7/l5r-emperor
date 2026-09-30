# Japanese pine — Meshy source and cleanup

Alfredo returned the two approved pine references on 2026-09-15. They and the original prompts are preserved under `assets/ui/duel-arena/`. The reverse is a generated reference, not a measured second photograph of the tree.

The first multi-image task lost most of the tree and was rejected. A second task using the primary image alone produced the complete pine. Each task reports 30 consumed credits: 60 total. No further paid remesh or texture task was used. Exact task IDs, settings and hashes are in `provenance.json`.

- `pine-source.glb`: unchanged Meshy textured output, 59,974 triangles with original 4K base-color, normal and packed metallic/roughness maps.
- `pine-cleaned.glb`: the high-detail source reduced locally to 220,000 triangles, with UVs transferred from the textured output and the same original PBR maps. `cleanup.json` records the executed operation and hashes.
- `../../duel-arena-v4.blend`: editable assembly with two linked copies, a root-contact pivot and uniform height of 3.05 units. A separate authoring scene retains the original textured model and both reference images. The mat, lantern geometry and card camera are preserved from v3.

The 60K output made the foliage visibly coarse. The 220K reconstruction improves its outline, but some needle clusters remain solid leaf-like surfaces. UV transfer uses nearest source triangles and is approximate; unchanged texture bytes do not imply identical surface mapping. Compare front/reverse close-ups and the gameplay camera before human art acceptance. This is a review asset, not final foliage or a scan.

## Recover the large geometry source

The original pre-remesh GLB is 374,311,972 bytes and 7,641,180 triangles. It has no UVs or textures. It is retained unchanged in a compressed archive on the repository's [private draft source Release](https://github.com/alfredoh7/l5r-emperor/releases/tag/untagged-69b6f44ff90b9719289e), outside Git. Normal game builds and arena assembly use the committed cleaned asset and need no download or Meshy access.

From an authenticated repository checkout, recover it only when rebuilding the cleanup:

```sh
mkdir -p artifacts/meshy/duel-pine-v2/download
gh release download art-source-205-pine-20260915 \
  --pattern l5r-duel-pine-v2-high-source.zip \
  --dir artifacts/meshy/duel-pine-v2
unzip -n artifacts/meshy/duel-pine-v2/l5r-duel-pine-v2-high-source.zip \
  -d artifacts/meshy/duel-pine-v2/download
```

Archive SHA-256: `da01eba8ed4a8ef0607d125577a1a1da9af0fb457150bf397d254ace93d94a75`.

Raw GLB SHA-256: `81c821e81645f3f41bb10fa93b6b24a8daceebff32d9fead67b71b9f81a46614`.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --disable-autoexec --python tools/blender/prepare_duel_pine.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup \
  --disable-autoexec --python tools/blender/build_duel_arena_v4.py
```

The cleanup uses NumPy bundled with Blender and was exercised on Blender 5.2.1. It exact-welds positions, reduces geometry, and transfers each destination triangle's UVs through a nearest-face lookup with clamped barycentric interpolation. This avoids Blender's expensive UV-island Data Transfer search on the large source. The original downloads and rejected candidate remain in ignored local `artifacts/meshy/` folders.
