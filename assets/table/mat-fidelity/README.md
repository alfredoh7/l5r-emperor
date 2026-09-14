# Playmat material / frame review — issue 218

The active game uses the 1672×941 `navy-mat-v4.png` on a shader-projected flat plane. The visible narrow wooden edge is baked into that texture. Lanterns are separate. Older `TableView`/`build_table.py` wooden geometry is not the active table.

This pass creates independent Blender fabric and frame objects and a Godot review. The original artwork remains the source of truth. No game layout, camera limits, cards, menus or rules are changed. The live mat is not replaced by an unapproved generated variant.

## Fidelity strategy

- Preserve the original printed design through continuous UVs across fabric and frame. No AI redraw in the proposed asset.
- Add derivative-filtered fine textile detail separately. It resolves at close range and fades below pixel size; it does not restore missing detail in the gold illustration.
- Model a narrow, beveled wooden frame independently, with real depth and separate wood material.
- Keep the cloth material, frame material and geometry independent so later approved mats can replace the cloth without rebuilding the table.

The generated `fabric-reconstruction-reference.png` is an experiment only: it returned 1672×941 despite the 4K request, and changed ornaments. It is not an upscale. See `imagegen-prompt.md`.

## Review gate

Compare the native source and separated asset at fit, cloth close-up and frame close-up, then review actual gameplay with optional fine-detail shading. Physical room integration and a selectable-mat menu are later steps. A higher-resolution faithful gold artwork master remains desirable; fine fabric shading alone cannot solve unlimited zoom.
