# Card presentation pass · 0.3.1

**Historical checkpoint:** Version 0.3.2 replaces the aspect-fit layout with fullscreen presentation, larger cards and reading panels, paper materials, and animated candlelight. See [the current presentation report](FULLSCREEN_PRESENTATION.md). The dimensions and static-environment descriptions below apply to 0.3.1.

The preferred floral table is now the background of the playable client. The game and environment share one centered 1586 × 992 design space and preserve its aspect ratio at other window sizes. Live cards sit on the cloth, the hand sits on the foreground wood, and the inspector occupies the right perimeter. The old table is no longer stretched underneath dashboard containers.

## Artwork and alignment

The user selected `assets/table/phase-1/environment-v2.png`. A first alignment edit changed the perspective too much and was rejected. Two further built-in Imagegen edits retained the broad foreground and floral treatment while reducing the lower frame's slope. The selected asset is `assets/table/phase-1/environment-v5-level.png`. Sampled bright-trim heights across 950 horizontal pixels vary by approximately 3 pixels, down from 10 in the original. The painted border has small irregularities; this is not a claim of a mathematically perfect geometric rectangle. Floral materials and ornaments remain, although generated edits do not preserve every source pixel.

Blender MCP rebuilt the packed projection scene and GLB. The isolated Godot capture matches the selected corrected image exactly in RGB at 1586 × 992. This verifies preservation through the rendering pipeline; it is not a 1:1 similarity claim for the entire interface against the original concept.

The new hidden Dynasty back is `assets/cards/dynasty-back-v1.png`, with a corresponding runtime copy at `src/L5R.Client/Art/dynasty-back.png`. It is a new generic floral back, not a historical card scan. All visible published card faces remain their actual complete scans. [Exact prompts and selected outputs](../assets/cards/ui-pass-prompts.json) record the built-in Imagegen workflow.

## Live card behavior

- Complete printed faces retain their original aspect ratios, borders, rules, statistics, and credits. Bowing rotates the whole card; it does not recolor or crop the face.
- Mipmaps and linear filtering reduce shimmering fine print at small sizes. Layered contact shadows and a thin paper edge separate cards from the painted cloth. Hover gives a restrained lift; reduced motion disables it.
- Mouse hit regions follow rotated card faces, so a Personality's transparent margin no longer intercepts its attachment's exposed edge.
- Collapsed attachments expose individual strips behind their Personality. Opening a unit presents its actual live tiles in a large tray, with parent name, location, individual bow states, and a Fold control. The tray does not create duplicate actionable cards. Large units no longer shrink into unreadable strips inside one battlefield lane.
- Shelves fit whole units; exceptionally crowded shelves use previous/next controls instead of cutting a card in half at a scroll edge. The separate hand has a shallow fan.
- Home areas are explicitly identified. Home Personalities do not belong to provinces. Battlefield units remain grouped by the defending province, with separate owner labels and Force totals. Draft arrows and confirmation continue to use the authoritative action flow.
- Hover previews preserve hidden information and pending decisions. Existing rule explanations remain available; the scripted tutorial is a future phase.

## Native evidence

- [Unit tray and complete cards](screenshots/cards-phase-2-unit-tray.png)
- [Attack assignments at 1920 × 1080](screenshots/cards-phase-2-assignments.png)
- [Actual hover preview](screenshots/cards-phase-2-hover.png)
- [Corrected environment without interface](screenshots/table-phase-1-level.png)

The native captures were visually inspected. Initial passes exposed undersampling, partial cards at shelf edges, a crowded expansion, and overlapping home/province labels; those were corrected before this delivery.

The existing regression suite passed all 148 checks: 59 content, 36 core, 31 published-card, 13 AI, 5 server, and 4 client transport checks. The client builds with zero warnings and errors. Twelve native acceptance captures passed (including the exported Mac app), covering draft/cancel/confirmed attacks, confirmed defense, targeted ability confirmation, individual attachments, actual mouse hover and hidden-card protection, province refresh, opponent Honor choice, Gold payment/Proclaim, and multiple window sizes. Board captures also verify bow angles, unit ownership, battlefield membership, unique card nodes, and visible card-face bounds within shelves and the viewport. Logs and the numerical image comparison are in `artifacts/card-polish/`.

## Scope and remaining work

This is a playable environment and card presentation pass. HUD decoration, clan banners, more expressive transitions, and a full concept comparison across the final interface remain separate passes. The environment is static painted scenery with a locked camera; card shadows are drawn in the live presentation, not ray-traced scene lighting. Tiny resting cards still require hover or the inspector to read their rules. Extreme unit sizes and broad hardware coverage need further playtesting.

The Mac and Windows development packages were rebuilt successfully with `tools/export.sh all`; the exported Mac app also passed a native assignment capture. Native visual verification in this pass was performed on macOS; Windows visual acceptance is still outstanding.
