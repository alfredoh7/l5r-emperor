# Godot client profile, October 2026 (issue #875)

Measured on an Apple M5 Mac, macOS OpenGL 4.1 (Metal), `gl_compatibility` renderer, 1440x900 window.
Method: the scenarios in `Main.Perf.cs` (`--perf=menu|table|play`, `--perf-seconds=N`, optional `--perf-toggle=shadows|lights|hall|noshadowcast`),
one `PERF` line per second plus `PERF_NODES`, `PERF_MESH` and `PERF_TEX` summaries at the end; `dotnet-trace` attached to the running Godot process for the managed profile.
Run for example `"$GODOT" --path src/L5R.Client -- --perf=table --perf-seconds=15`.

## What the numbers say

| Scenario | Measure | Before | After this change |
|---|---|---|---|
| Idle table | frame time (average / p95) | 11.0 / 15.5 ms | 9.3 ms average, p95 about 10 to 13 ms |
| Idle table | managed allocation | 68 MB/s, 8 gen-0 GCs/s | 1.1 MB/s, 0.2 GCs/s |
| Played game | managed allocation | 48 MB/s | 22 MB/s (the rest is `Render()` rebuilding the board after each command) |
| Title menu | resident memory / texture memory | 2.27 GB / 948 MB | unchanged |
| Table | resident memory / texture memory / draw calls | 2.5 GB / 1029 MB / 1144 | unchanged (see held items) |

### Managed CPU (before)
- 67% of the main thread's managed time was `MatchEngine.State` (a full deep copy of the match) called from `MatchSession.StepPracticeReaction` and `StepPracticeOpponent`, which run every frame and read `State` 5 times per frame to check three scalar fields. They now read scalars directly.
- `PrepareTableTooltips` walked every control in the table each frame (23% of main-thread samples). It now runs when the tree changes and 5 times a second.
- `InkIconOverlay._Process` recomputed every hotspot rectangle each frame (15%). It now skips when nothing moved.
- `FortressHall.PresentLight` re-created StringNames and re-sent constant shader parameters each frame (12%). Names are cached and constant parameters are sent only when they change.
- `PhaseTrack` redrew its plate every frame for a shader-only glow pulse (11%). It redraws when a label, weight or focus changes.

### GPU and memory (not changed here)
- The 3D war room dominates frame cost. With vsync off: whole scene 8.94 ms, with shadows off 5.59 ms, with the hall hidden 3.91 ms. On older GPUs this is the first thing to break.
- The hall is 1306 `MeshInstance3D` nodes (2.5 M vertices, 1263 casting shadows) and 9 omni lights, several with shadows. The 2D UI adds about 970 canvas draw calls on its own.
- The title menu loads about 640 MB of war-room assets (models and textures) even though the hall is hidden behind the menu.
- Every War Room texture is imported lossless, so each 4096x4096 map is 64 MB of GPU memory as RGBA8 with mipmaps. 46 textures loaded at launch come to about 909 MB raw against 252 MB if block-compressed.
- Held on branch `claude/875-vram-textures-held`: importing the War Room JPG/PNG textures as VRAM-compressed BC7 (high quality) cuts texture memory from 1029 MB to 560 MB. A table screenshot compared before and after, with the difference amplified 6x, shows no visible difference in the hall; the differences are confined to the randomly dealt hand. This changes the shipped pixels in principle, so it needs your call.

## Recommended next steps (not done)
1. Build the war room when the table is first shown instead of at launch (menu footprint and startup).
2. Merge the 1306 static hall meshes by material (same pixels, far fewer draw calls).
3. Cut shadow cost: fewer shadow-casting lights or a smaller shadow atlas, judged against screenshots.
4. Decide on the BC7 texture import above.
5. Find where the 2D UI spends its 970 draw calls.
6. Measure all of this on the oldest real GPU you intend to support; a Mac M-series GPU hides most of these costs.
