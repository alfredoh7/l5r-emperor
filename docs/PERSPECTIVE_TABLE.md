# Shared table perspective and lantern light · 0.3.6

The previous pass skewed individual card corners while leaving the overall board near top-down. Its 2D triangles also interpolated artwork unevenly. This version removes that independent distortion: cards, piles and the environment follow one perspective projection, with a view equivalent to approximately 40 degrees above the table. Sun and Moon's tournament match at about 1:40 was visually reviewed again as the camera/layout reference.

## Rendering and input

`TableSurface` renders the native board controls flat into an isolated transparent SubViewport. A perspective-correct inverse mapping samples that surface into the game viewport. Card scans keep rectangular full-image UVs, bow rotation happens within the flat table plane, and resting board cards no longer lift when hovered. Tighter contact shadows help them sit flush. The fixed hand, inspector, dialogs, and readable hover previews remain outside this projection.

Mouse events use the inverse of the same projection to reach the native controls. Camera panning and cursor-anchored zoom use table-plane coordinates. Assignment arrows use projected screen positions. Native checks click the centers and all four near-edge positions of upright and bowed cards, verify selection, and confirm that inspection/navigation never changes match state. Expanded unit inspection remains flat for reading. Keyboard events for table controls are isolated from HUD focus; F11 and Escape remain application shortcuts.

The offscreen render target follows screen density, with a texture-size ceiling. It covers the full visible inverse-projection area, including the far side while panning; it is not a low-resolution fixed screenshot. This remains a 2.5D presentation, not a fully modeled room.

## Environment and light

The approved navy image and Blender projection asset remain the source art. The environment expands around the board at the image's native aspect ratio. Its outer pixels fade *inside* the image boundary into a uniform near-black gray; no last-row/last-column smearing extends the scene. The image is placed so its left lantern remains visible in the normal starting composition. Panning beyond the environment reveals the same quiet near-black field.

Two localized lantern effects add emissive cores, broad warm halos, and soft light spill onto the textile and card material. Their breathing intensities use slow, offset oscillations. Light sources share the table coordinates and therefore stay attached to the scene during pan/zoom. Reduced motion retains steady illumination and disables the oscillation. These are runtime shader lighting effects; lantern geometry and environmental shadows are still painted into the approved background.

## Review

- `tools/check-table-camera.sh`: perspective input, edge clicks, card/pile layout, standard/16:10/ultrawide views, attachments, pile hover, reduced motion.
- `tools/check-perspective-table.sh`: readable hover and hidden-card protection, attack/defense confirmation, targeting, payments, purchases, choices, fullscreen input, and 72 native light-phase frames.
- Source logs and review captures: `artifacts/perspective-table/` and `artifacts/table-camera/`.

The lighting movie is an encoding of actual Godot frames sampled at 12 light phases per second. No offline glow or scenery compositing is added; it is an animation study, not a performance benchmark. Package validation and the delivered Mac fullscreen/input review are recorded in the acceptance file after export. Native Windows gameplay acceptance remains separate from its package validation.

All 19 development scenarios passed, including the separate zoomed-out boundary check. The exported Mac app passed its 3840 × 2160 camera/input and projected-edge click checks. Both packages passed resource/runtime validation. Final delivered-app captures were visually inspected. The unchanged rules/AI/server suites were not repeated for this presentation pass.

The 72-frame light analysis measured a 2.42-level mean RGB variation at the lantern core and 0.88 on nearby cloth, versus 0.05 on distant cloth and exactly zero in the HUD and black surround. Reduced-motion phases rendered identically.

- [Delivered fullscreen starting view](screenshots/perspective-table-initial-v0.3.6.png)
- [Delivered fullscreen focused battle](screenshots/perspective-table-focus-v0.3.6.png)
- [Native lantern-light movie](videos/lantern-light-v0.3.6.mp4)
- Acceptance record: `artifacts/perspective-table/acceptance.json`
