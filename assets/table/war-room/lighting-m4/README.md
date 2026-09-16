# B3 material and static-light refinement

Alfredo reviewed the native milestone-3 room and said “looks great - please continue.” This pass keeps the accepted composition and five Meshy props. No additional Meshy jobs are used. It refines floor light spread, timber response, fixture glare and dusk colour; room animation is still deferred.

`tools/blender/bake_fortress_floor.py` opens the committed B3 Blender assembly and traces direct and indirect diffuse illumination onto a neutral floor receiver using Cycles Metal. It uses the actual room, column, chest, brazier and table geometry for occlusion, with the lantern bodies excluded from shadow/diffuse rays so generated opaque panes cannot trap the light. The bake is denoised through Blender’s OpenImageDenoise compositor node and saved as a linear half-float EXR. Its alpha channel carries a separate denoised 0.13 m contact-occlusion pass. The receipt records the source hash, light settings and sampling resolution.

The Godot floor material keeps scanned albedo, normals and roughness separate from illumination. Floor UVs keep their original grain scale. World-space UVs address the light field; its overscan bounds fade to a neutral fill. Diffuse lighting is applied once through emission, while the native lights retain a restrained specular response. The room candle now reaches its socket and has a separate charred wick; the Meshy body is unchanged. This is a custom baked floor-light field, not whole-room LightmapGI, and it has an art-directed exposure and cool fill for the approved illustration. No table/card artwork is baked into the floor.

Architecture, map and Meshy bodies still use the existing native direct/ambient lighting. Camera, mat, frame, cards and rules are unchanged. Dusk artwork retains the exact source crops, with a small cooler brightness correction and anisotropic filtering; it does not gain invented high-resolution detail.

Rebuild the light field from this checkout with:

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --python tools/blender/bake_fortress_floor.py
```

Normal launches do not require Blender. Keep the original B3 source and relative Meshy libraries alongside it for rebaking. Moving major furniture or lights requires a new bake. Future flicker needs a separate dynamic lighting contribution; the baked baseline is deliberately stationary.

An initial low-sample trial exposed grainy bounce light and was rejected. The final record and native before/after gallery distinguish technical render checks from exact-source fidelity and human acceptance.
