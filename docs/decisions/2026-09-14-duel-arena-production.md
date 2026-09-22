# Duel arena: approved target and production sequence

Status: accepted direction from Alfredo, 2026-09-14. Layout and finished assets remain to be reviewed. Issue: [#205](https://github.com/alfredoh7/l5r-emperor/issues/205). Lane: UI; mechanics remain with Mendon.

The visual target is `assets/ui/duel-arena/approved-generation-3.png`: the open ink-and-silk scroll without printed card slots, on a worn lacquer platform in the moonlit garden. Preserve this original unchanged. Its illustration is a target, not evidence of modeled geometry or a running game.

Alfredo requested: **external ChatGPT image generation → human approval → Blender layout → quick Godot camera check → individual Meshy assets → Blender cleanup and assembly → Godot presentation → compare and iterate**. Codex supplies prompts for the user's chosen ChatGPT image model; it must not substitute its own image-generation service. No new concept generation is needed for the already-selected overall target.

Adapt the supplied war-room workflow to this duel arena. Build the scroll, rollers, lacquer plinth, simple rail and architectural forms directly in Blender. Use individual Meshy assets for the later stone lanterns, garden rocks and pine silhouettes. No war-room doorway or unrelated room expansion is part of #205.

The first deliverable is an isolated layout review, with real card faces and the existing TableSurface perspective, not live duel integration. Validate two units, physical attachments, focused-card spaces, hand, controls and optional sidebar clearance. Preserve the actual match UI and shared engine interfaces. Detailed materials, Meshy geometry, transitions and match integration remain subsequent stages.

Reference pixels must not be passed off as 3D geometry. The blockout uses honest simple materials; compare its composition to the target separately. Final surface art will need an approved flat asset with no baked perspective, lighting or card slots. The reference's invented heraldic symbols are not approved clan emblems.

GLB is the runtime interchange; preserve editable `.blend` source and reproducible scripts. The source image, scene measurements, native captures and any discrepancies belong in the task PR. Human art acceptance, native camera checks, gameplay validation and rules acceptance are distinct.

## Accepted revision — supplied mat and lantern, no Meshy for now

Later on 2026-09-14, Alfredo supplied the flat duel mat and a single neutral three-quarter stone-lantern reference, then explicitly requested proceeding in Blender without Meshy. This supersedes the Meshy dependency for this iteration. Apply the exact supplied mat pixels, preserve their original aspect and border, and model the lantern directly as editable geometry with a real hollow chamber. Procedural stone and its Blender-baked PBR maps are allowed authoring work, not external image generation.

Keep the first blockout; save the new source as `duel-arena-v2.blend`, including a separate authoring scene with the individual lantern parts and packed reference. Compare the actual native mat and lantern beside the originals. A single-view reconstruction is approximate; no hidden-surface accuracy or photogrammetry is claimed. Human approval of the references is authorization to build this pass, not pre-approval of its resulting model or final scene. Mendon retains mechanics work, and live duel integration remains outside this asset iteration.

## Accepted revision — supplied Meshy lantern replaces the manual study

Alfredo rejected the manual lantern result and supplied `Meshy_AI_Mosslit_Stone_Lantern_0914201150_generate.blend` and `Meshy_AI__0914201140_texture.blend`. Use the textured Meshy asset for iteration 3 while retaining the approved mat and card camera. This supersedes the preceding manual-lantern direction. Preserve both source files unchanged; use Blender for scale/pivot normalization and removal of the generated solid window panels so the lamp can actually illuminate its chamber. Keep the supplied exterior UV artwork and PBR maps. The cleaned runtime mesh has 4,407 triangles (source: 4,051). The rejected manual study is retained locally for recovery, not loaded by the review. No service generation was run by Codex.

## Accepted revision — Meshy is the default detailed-prop workflow

Alfredo preferred the Meshy result and explicitly restored the original production sequence with direct API access. Future approved individual props use Meshy, with Blender responsible for layout, architecture, scale, geometry/UV cleanup and assembly. This supersedes the temporary no-Meshy route and the requirement for manual transfer of future service exports. External ChatGPT image creation and human image approval remain unchanged.

The configured project entry point is `tools/meshy.py`, using the pinned official CLI and a private macOS Keychain credential. [MESHY_WORKFLOW.md](../MESHY_WORKFLOW.md) records submission/recovery, provenance and native acceptance. API authentication and a no-request dry run were verified; no new paid generation was run during setup. Preserve the supplied lantern's existing provenance. Issue #205 and draft [PR #220](https://github.com/alfredoh7/l5r-emperor/pull/220) retain the presentation scope; no mechanics/interface changes are implied by this tooling decision.

## Accepted revision — one mature central pine

On 2026-09-15, Alfredo requested refining the pine foliage, then explicitly replaced the two matching side trees with one larger tree in the middle. Iteration 5 follows that direction: preserve the useful Meshy trunk/branches, replace coarse canopy surfaces with Blender needle clusters, and place a single mature tree behind the center of the mat. Preserve the playing area, shared card projection and lanterns. Matching mirrored/rotated tree copies are no longer the approved composition. This accepts the direction; final art acceptance remains distinct from implementation and native verification. No additional Meshy generation was requested or used for this pass.

## Accepted correction — show the complete central tree

Alfredo rejected the first central placement because the trunk/base were visibly cut off. The complete crown, trunk and roots must be visible in the default review composition. Put the tree on level ground behind the terrace, and give the scene headroom by widening the framing and lowering the optical center of the shared 3D/card canvas. Do not hide the base below the garden to fit the crown. The pine remains one mature tree; this correction does not authorize mirrored copies or new Meshy generation. Final lighting, architecture and human art acceptance remain open.

## Accepted continuation — garden, architecture and atmosphere

On 2026-09-15, Alfredo authorized proceeding through phases and subphases with native screenshot iteration toward the approved concept. Preserve the complete central tree. The plan is in `docs/DUEL_ARENA_PHASES.md`. The existing approved composition supplies the rock brief and distant sky/garden pixels; no new image-generation service is substituted. One Meshy text-to-3D rock candidate plus PBR refinement uses 30 reported credits. Straight architecture, tile geometry, ground and small blossom details are authored in Blender. Photographed CC0 timber/stone/moss maps supply surface detail, with provenance retained. The approved ornament and banner motifs are reused as decorative artwork, with no clan identity or player affiliation assigned to the reference’s invented heraldry. The distant image is explicitly a 2D backdrop behind real foreground geometry. Godot supplies final material grading, light, mist and restrained movement; reduced motion freezes these effects. Final human art acceptance and live duel integration remain separate.
