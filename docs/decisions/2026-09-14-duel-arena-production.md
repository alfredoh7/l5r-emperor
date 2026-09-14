# Duel arena: approved target and production sequence

Status: accepted direction from Alfredo, 2026-09-14. Layout and finished assets remain to be reviewed. Issue: [#205](https://github.com/alfredoh7/l5r-emperor/issues/205). Lane: UI; mechanics remain with Mendon.

The visual target is `assets/ui/duel-arena/approved-generation-3.png`: the open ink-and-silk scroll without printed card slots, on a worn lacquer platform in the moonlit garden. Preserve this original unchanged. Its illustration is a target, not evidence of modeled geometry or a running game.

Alfredo requested: **external ChatGPT image generation → human approval → Blender layout → quick Godot camera check → individual Meshy assets → Blender cleanup and assembly → Godot presentation → compare and iterate**. Codex supplies prompts for the user's chosen ChatGPT image model; it must not substitute its own image-generation service. No new concept generation is needed for the already-selected overall target.

Adapt the supplied war-room workflow to this duel arena. Build the scroll, rollers, lacquer plinth, simple rail and architectural forms directly in Blender. Use individual Meshy assets for the later stone lanterns, garden rocks and pine silhouettes. No war-room doorway or unrelated room expansion is part of #205.

The first deliverable is an isolated layout review, with real card faces and the existing TableSurface perspective, not live duel integration. Validate two units, physical attachments, focused-card spaces, hand, controls and optional sidebar clearance. Preserve the actual match UI and shared engine interfaces. Detailed materials, Meshy geometry, transitions and match integration remain subsequent stages.

Reference pixels must not be passed off as 3D geometry. The blockout uses honest simple materials; compare its composition to the target separately. Final surface art will need an approved flat asset with no baked perspective, lighting or card slots. The reference's invented heraldic symbols are not approved clan emblems.

GLB is the runtime interchange; preserve editable `.blend` source and reproducible scripts. The source image, scene measurements, native captures and any discrepancies belong in the task PR. Human art acceptance, native camera checks, gameplay validation and rules acceptance are distinct.
