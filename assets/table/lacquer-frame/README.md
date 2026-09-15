# Approved lacquer frame — issue 218

Alfredo selected Option A / CORNER JOIN on 15 September 2026. Match the lower-left corner detail in approved-concept-a.png: mitered black lacquered hardwood, exposed warm edge wear, fine gold line and small incised corner ornament. Preserve the existing Topaz cloth artwork.

Implemented in PR #232. Four independent Blender rails surround the existing Topaz cloth. The runtime frame uses the original approved image pixels for the grain, wear and corner ornament. Two UV sets and a vertex-colour transition continue the corner into mirrored straight grain, avoiding a stretched photo seam. See build-report.json for the exact source coordinates, hash and dimensions.

## Dimensions and materials

- Outer footprint: 1.8 × 1.013038 m; 30 mm rails; 30 mm total thickness; top 3 mm above the cloth.
- Rounded bevel profile, four mitered joints, 0.25 mm joint separation. Gold decoration is in the source material; it is not raised trim that can overlap cards.
- Runtime GLB: 819,332 bytes; 14,816 triangles. Import keeps both UV sets and vertex colours, disables geometry LOD and compression. The live projection can enlarge a mesh that appears small to Godot's ordinary camera; automatic LOD otherwise collapses the grain coordinates into streaks.
- Runtime image: unchanged approved 1586 × 992 PNG, lossless import, mipmaps and anisotropic filtering. Source and runtime copy have the same SHA-256.
- The 6688 × 3764 Topaz WebP is unchanged and separately loaded. Its original painted edge sits underneath the physical rail; the visible printed cloth decoration remains part of that approved mat.
- The source .blend has editable rails, an external cloth preview, review camera and area light. Only rails enter the runtime GLB. Blender material roughness is 0.56; the current game's painted renderer uses a colour-preserving unshaded material with localized candle warmth.

## Reproduction and review

From the repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/build_lacquer_frame.py
source tools/env.sh
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --editor --single-threaded-scene --path src/L5R.Client --import
bash tools/check-lacquer-frame.sh
```

Open `tools/blender/Review Lacquer Frame.command` for the native comparison. Keys 1–3 select the join, whole mat and overhead; wheel zooms and dragging pans. `docs/screenshots/lacquer-frame/index.html` displays the saved native captures. No image-generation or Meshy credentials are needed to reproduce the model.

## Native validation, 15 September 2026

C# build: zero warnings/errors. Native Godot 4.7.2 Compatibility on M1 Max: three asset views plus five gameplay captures passed without shader/script errors. Gameplay checks cover 1920 × 1080, 1440 × 900, 2560 × 1080, maximum zoom, widest zoom and reduced motion. Existing probes exercised cursor-anchored wheel zoom, pan/drag, Home reset, projected upright/bowed card clicks, fixed HUD/hand and modal isolation without match-state changes. Source-to-game images were visually inspected; early incorrect projection, UV transition, lantern occlusion and LOD flattening were corrected.

An initial cold editor import crashed inside Godot before asset import. Reusing the existing repository import cache and using single-threaded scene import succeeded. This is recorded separately from the clean final native captures.

## Fidelity boundaries

This is a separate model with real bevels and the approved ornament pixels, not an identical photograph from every camera angle. The reference macro invents finer cloth weave; the game retains Alfredo's approved Topaz cloth. The frame source includes photographed light and repeated straight grain, so it is not yet a fully relightable PBR asset for an inhabited room. Gameplay still uses its established 2.5D table projection; the shader explicitly preserves cloth and upright-lantern occlusion. Native Windows graphical testing is not claimed. No Meshy call was made and no Meshy credits were spent.
