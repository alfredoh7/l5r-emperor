# Playable lacquer UI · 0.3.0

The 0.3.0 presentation pass preserves the published rules engine and the existing bow, stack, assignment and confirmation behavior. All published cards use their complete original scans, including printed borders, statistics, rules and credits. The client never tints a published scan to indicate bowing: the card physically rotates. Current Force, attachment counts and selection outlines are drawn outside its printed face. Training-only illustrations retain their separate fallback renderer.

## Concept to runtime

The selected visual target is `assets/concepts/D-playable-lacquer.png`, developed from the earlier lacquer/ink concept, a real gameplay capture and an original Ikoma Shika scan. Built-in Imagegen also produced olive silk and walnut material textures. Exact prompts and reference roles are recorded in `assets/concepts/polish-prompts.json`; no CLI/API-key fallback was used.

The project’s local Blender MCP connection executed `tools/blender/build_table.py` inside the dedicated production scene. It embeds those textures, maps the complete silk surface, models the table, brass fittings, rings, candles and decorative counters, and exports `assets/table/emperor-table-v2.blend` and `src/L5R.Client/Art/table.glb`. The earlier .blend and concept variants remain available. Native Godot controls and original card scans render over the genuinely modeled table; the concept screenshot is not a runtime background.

Native screenshot reviews corrected an overbright silk surface, excessive lacquer specular glare, and a tooltip that initially measured too tall before layout. The resulting interface follows the concept's materials, restrained brass, full cards, slim header, four lanes and centered gently fanned hand. It is **not pixel-identical** to the generated composition: actual match state determines the cards, labels and empty lanes; the generated clan banners and large deck props are absent; guidance sits beside confirmation controls; the table uses a flatter camera for card placement. Original printed card colors remain intact rather than receiving the concept's aged color treatment.

## Help and interaction

- Hover over a visible card for its complete face, current statistics, bow state and relationship to its unit/home. Unknown cards reveal neither an image nor a definition.
- Click to keep a card in the larger scrollable inspector. Hover does not replace a pending target, assignment or payment.
- `Your next move` remains visible beside confirmation controls, including while inspecting a card. It adapts to priority, pending choices, payments, assignments and the current phase.
- Phase labels, province labels, ready Gold/Honor, unit Force, keywords and action buttons explain their meaning on hover. Disabled actions retain their reason.
- `? Rules` opens a two-tab table guide. Longer explanations use readable text and scrolling.
- The shared `RulesHelp` topics have stable keys and no engine mutations, so a future scripted tutorial can reuse the explanations. A scripted tutorial is not included in this pass.

## Verification

Native interaction captures exercise actual card/province/button handlers, source selection, attack/defense drafts, confirmation, individual attachments and ability targeting. Hover acceptance sends mouse input through the viewport, checks the bounded live tooltip, verifies that the pending command and authoritative version stay unchanged, and checks that unknown-card previews contain no image. Screenshot review is required in addition to passing those assertions.

See `docs/VALIDATION.md` for the final run evidence and package checks. Final native comparison images are in `docs/screenshots/polish-*.png`.
