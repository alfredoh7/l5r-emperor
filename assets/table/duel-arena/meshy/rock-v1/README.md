# Duel garden rock

One Meshy candidate generated from a brief derived from the approved moonlit garden, then textured with dark granite and restrained moss/lichen. Geometry cost 20 credits; PBR refinement cost 10, for 30 total API-reported credits. No additional candidate or paid remesh was submitted.

`rock-source.glb` is the unmodified downloaded original. `provenance.json` records the tasks, hashes and source triangle count; prompts/settings are in `assets/ui/duel-arena/garden-rock-prompts.json`. The v6 builder normalizes the ground pivot, fits a 2.8-unit footprint, and prepares 2048² runtime maps while retaining the source. The full editable assembly is `duel-arena-v6.blend`; the game loads the separate `duel-arena-v6-environment.glb` alongside the unchanged v5 assets.

Runtime review uses varied orientations and scales so repeated geometry does not present identical exposed faces. Small scattered stones and the garden bank are authored separately in Blender. Visual acceptance is based on the native camera, not the service preview alone.
