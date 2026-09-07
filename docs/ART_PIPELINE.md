# Concept → Blender → Godot

The prototype uses a real asset pipeline. Sun and Moon code and Unity scenes are not part of the implementation.

1. **Concept studies.** `assets/concepts/` contains three generated material directions and their prompts. A (lacquer and green silk) is provisional; B (paper and stone) and C (indigo and brass) remain selectable in Settings. Final visual approval is still open.
2. **Original training illustrations.** `src/L5R.Client/Art/training-atlas.png` contains four generated illustrations. `assets/concepts/training-art.json` records the exact prompt and quadrant mapping. These illustrations are reused for synthetic fixtures, not represented as published card art.
3. **Editable geometry.** Blender MCP executed `tools/blender/build_table.py` in a dedicated Blender scene to produce `assets/table/emperor-table.blend`. The table includes a walnut frame, woven surface, metal inlays and lamps. The Blender render is a modeling review, not a game screenshot.
4. **Portable export.** The GLB in `src/L5R.Client/Art/table.glb` embeds its materials and textures. Godot loads it in `TableView`, applies game lighting, and presents native card controls above the 3D surface. Rules remain independent of presentation.
5. **Native comparison.** `bash tools/capture.sh` builds and runs the actual Godot client, exercises the purchase button, and saves screenshots at three window sizes. It requires a graphical desktop. Compare these captures with the chosen concept, then edit the Blender source, materials, typography and layout together. A generated image is never presented as evidence of a working screen.

## Blender connection on this Mac

The installed Blender MCP source is pinned to commit `c5f35d9cc54451d785ac4c00c48bf9e98a2e8db9` (package 1.9.1). Its Python environment lives in `.tools/blender-venv`. Codex has a local MCP registration named `l5r-blender`; a newly opened Codex session may be needed for its tools to appear in the tool inventory. The checked-in standard MCP client also exercises the same server directly:

```sh
# Start a dedicated factory scene. Existing Blender documents are not overwritten.
bash tools/blender/start.sh

# In another terminal, inspect the running scene through MCP.
.tools/blender-venv/bin/python tools/blender/mcp_call.py info

# Rebuild the approved project-owned table scene through MCP.
.tools/blender-venv/bin/python tools/blender/mcp_call.py run tools/blender/build_table.py
```

The connection uses localhost port 9876. Telemetry and external model/download integrations are disabled, and MCP safe mode remains enabled. The addon is loaded for this dedicated Blender process; it does not save global Blender preferences. `.tools` is a local tool cache and is excluded from Git. The supplied `.blend` and GLB remain usable without MCP or this cache.

The current Blender launcher is for this Mac's `/Applications/Blender.app`. The production script's documented `ROOT` value must be updated if the repository moves. The finished game does not require Blender, MCP, or an image-generation account.

## Remaining presentation work

The current interface is a functional prototype. Bespoke illustrations per card, richer spatial card animation, sound, controller navigation, text scaling, final color/contrast review and cross-platform performance acceptance belong to the presentation phase. Official artwork stays in the reference library until its exact card/printing mapping is verified.
