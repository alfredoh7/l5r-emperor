# Room polish — milestone 6

Alfredo requested a stronger match to the approved sparse hall, particularly the rough brazier, on 16 September 2026. The latest request explicitly asks for built-in imagegen plus Meshy and Blender. It supersedes the earlier external-only image-generation preference for this pass.

The existing chair-free layout, sparse furnishing inventory, original mat/frame, gameplay camera and restored lacquer menus are retained. The source attached with this request includes old foreground chairs; it is used for material/atmosphere reference, not to reverse the explicit chair removal.

## Production changes

- A dedicated large iron-basket reference replaces the earlier 105×124 source crop. One Meshy-7 standard/Ultra textured PBR job consumed 35 credits. Its raw 344,296 triangles were inspected in Blender, reduced to 120,000 with smooth normals, and compared under the same studio lighting. Original UVs and 4K base/normal textures are retained. Receipt and editable source: `../meshy/brazier-v2/`.
- The empty fire pan remains real geometry. An editable Blender coal bed, imagegen flame artwork, two independently warped native flame layers, subdued emissive fissures and seven small rising embers provide the fire. The pan casts native shadows; partial opacity accounts artistically for the broad flame source. Reduced motion freezes every time input using the existing presentation clock. The original PBR maps remain in the source asset; native iron roughness is deliberately set to 0.82 to suppress sparkling highlights in the generated roughness map while retaining base color and normal detail.
- The floor has a darker cool ambient response, stronger grain/specular definition and a fresh 128-sample Cycles floor-only irradiance/contact bake using the replacement brazier. This preserves local warm pools while reducing the old even orange fill.
- A dedicated 1776×888 dusk panorama replaces the visibly enlarged tiny scenery crops. It remains a background matte behind the same actual geometry, not a new navigable landscape. The original source map screen is preserved.

## Reproduce

Run `tools/blender/prepare_fortress_brazier.py -- --inspect` through Blender, inspect its original render, then `--prepare`. The ignored raw GLB is required; the accepted cleaned `.blend` and `.glb` are committed for ordinary builds. Run `tools/blender/build_fortress_hall.py` for room assembly, then `tools/blender/bake_fortress_floor.py -- --milestone 6 --samples 128 --size 1536` for lighting. Import in Godot only after Blender finishes exporting. The cleaned prop is also copied to `src/L5R.Client/Art/WarRoom/brazier.glb`.

`bash tools/check-war-room-live.sh` and `bash tools/check-war-room-motion.sh composition brazier table-lantern` capture this milestone without overwriting M5 evidence. Clips are deterministic native 24-fps frame sequences, not a real-time performance benchmark.

## Review

See `docs/screenshots/war-room/milestone-6/index.html` for source/native and previous/current comparisons. Native gameplay preserves card size; the wider isolated review camera is separately labeled. This milestone materially improves the brazier and atmosphere but does not claim an interactive fixed-pitch room is pixel-identical to its painted concept. Human visual acceptance, Windows graphical playtesting and sustained performance profiling remain open.
