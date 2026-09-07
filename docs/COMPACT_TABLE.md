# Compact navy table · 0.3.5

This pass addresses oversized spacing, an overly distant starting view, the rejected olive background, and cards appearing upright above the table.

## Layout and camera

The Sun and Moon tournament match was visually inspected in the in-app browser at approximately 1:40: https://www.youtube.com/watch?v=RfKc5ZcnDNc&t=100s. Its tight neighboring cards and clear unit groups inform this layout. The Emperor tutorial screenshots/transcript remain the reference for provinces, decks, home cards and attachments. No Sun and Moon implementation is used.

Province columns start at 170 logical units instead of 440. Home cards are packed by their actual upright/bowed footprint, with 10 units between controls. Developed economies expand sideways before wrapping; occupied battlefield columns grow only as their units need room. Attachments retain exposed title strips and individual bow states. The separate home/province rows, mirrored piles, hidden-information boundaries and rules remain intact.

The starting camera fits this substantially smaller layout. Existing drag, arrow, wheel, Home, My side and Battle controls remain available. Once the board grows, it does not continually shrink the player's view. An explicitly requested whole-table fit in a developed game still produces smaller cards; focused views and full-card inspection remain necessary for reading dense printed rules.

## Surface and card presentation

Built-in imagegen edited the supplied reference into an empty [navy table plate](../assets/table/phase-5/navy-lantern-table-v1.png), preserving dark blue cloth, gold cloud embroidery, walnut fittings and warm lanterns. The [exact prompt](../assets/table/phase-5/prompt.md) and input reference are retained beside it. The generated source is 1672 × 941 pixels.

The pinned Blender MCP connection built [an editable scene](../assets/table/phase-5/navy-table-v1.blend) and exported `Art/navy-table-v1.glb` through `tools/blender/build_navy_table.py`. The runtime uses this same generated image, not an approximation assembled from generic textures. The full scene follows table pan/zoom; its surrounding wood is decorative and does not constrain navigation. Outside the image, the edge fades into dark timber. The previous olive assets are preserved but no longer used for gameplay.

This remains a 2.5D projection scene, not a fully modeled room. Localized animated lantern light is anchored to the surface; reduced motion stops the fluctuation. Card scans are projected into a slightly foreshortened quadrilateral (14% vertical compression and a subtle narrower far edge), with contact shadows and paper grading. Bow rotation happens before projection. The same quadrilateral drives hit testing, so the visible tilted edge and clickable edge agree. Full scan UVs are preserved. The fixed hand and reading/expanded-unit views stay flat for legibility; deck/discard piles receive the tabletop tilt too.

## Acceptance

Native checks exercise cursor-anchored wheel zoom, blank/middle/Space drags, arrows, Home reset, fixed hand/HUD, inspector and modal isolation, ordinary card clicks, mirrored pile order, province identities, attachment grouping, full card bounds and input on both sides of every projected edge. The capture harness explicitly focuses its native window before sending input; production focus safeguards remain unchanged.

Reproduce with `tools/check-table-camera.sh` and `tools/check-table-followup.sh`. Logs for this iteration are in `artifacts/compact-table/`; native scenario screenshots/logs are in `artifacts/table-camera/`. Delivered-app screenshots are linked below after export verification. Windows package validation is distinct from native Windows gameplay acceptance.

All 18 native scenario runs passed. The exported Mac app passed its 3840 × 2160 input/layout check; the initial table and focused battle screenshots were visually reviewed. Both packages passed resource/runtime validation. This presentation-only pass did not re-run the unchanged rules/AI/server suite.

- [Delivered fullscreen starting table](screenshots/compact-table-initial-v0.3.5.png)
- [Delivered fullscreen focused battle](screenshots/compact-table-focus-v0.3.5.png)
- Acceptance record: `artifacts/compact-table/acceptance.json`
