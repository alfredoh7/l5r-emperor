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

## Phase 2 (issue #877)

Same machine and method, same session, vsync off, 1440x900. Before is main at 931d2c1f.

| Measure | Before | After |
|---|---|---|
| Title menu resident / texture memory | 2288 MB / 948 MB | 414 MB / 116 MB |
| Table resident memory | about 2520 MB | about 2340 MB |
| Table texture memory | 1032 MB | 1032 MB (BC7 held, see below) |
| Table nodes | 1603 | 687 |
| Idle table frame time (average / p95) | 8.43 / 10.18 ms | 3.85 / 4.17 ms |
| Played game frame time (average) | 7.6 ms | 6.0 to 6.8 ms (varies with the game) |

What changed:
- **Lazy hall.** The war room is built when the table is first shown, not at launch. Choosing Practice, a match or a private room starts a background prefetch. Building it costs about 3.1 s on this Mac, paid when the table first appears.
- **BC7 textures: held.** Importing as BC7 cut table texture memory to 576 MB, but the Windows CI client job's Godot import stalled for 30 minutes on a 4096x4096 texture (each texture is encoded twice, BPTC and ASTC). Kept on branch `claude/877-bc7-held` until import time is solved (for example, drop the ASTC encode).
- **Freed source meshes.** The hall batches its 1306 source meshes into 288 partitions but kept the originals hidden. They are now freed after batching. The original draw-call count was already low, so merging further would not help and would risk the Compatibility renderer's per-mesh light limit.
- **Hall viewport.** The hall renders into its own viewport that redraws on camera, window or light changes, and at 30 Hz while candles flicker (reduced motion draws it once). It no longer redraws at display rate.

Pixel checks use `--perf-shot=path` (the 3D hall alone, static light, 2D hidden). Freed meshes: identical pixels. Hall viewport: 1.4% of pixels differ, all in the floor's shadow-acne dither pattern at the left edge and dais edges; the lighting and shapes match.

Tried and dropped: switching off shadow casting for floor and vista meshes gave no measurable gain (interleaved A/B), so it was not kept.

Still open: 2D draw calls (about 970), shadow cost on lights (about 1.5 ms), old GPU hardware testing, BC7 needs a GPU with BPTC or the ETC2/ASTC fallback.

## Phase 3 (issue #879)

Same machine and method, vsync off, 1440x900. Before is main at 6c4ce998 (after phase 2).

| Measure | Before | After |
|---|---|---|
| Table draw calls | 968 | 277 |
| Idle table frame time | 4.3 ms (this session) | 3.0 ms |
| Table texture memory | 1042 MB | 592 MB |
| Table footprint (macOS `footprint`) | 2363 MB | about 1470 MB resident |

**Piles.** Four deck and discard piles produced 724 of the 968 draw calls and 61% of the idle frame, because each card layer was about eight separate shapes. They now paint in a handful of batched calls: the dark cores as one triangle batch and each kind of line as one multi-line call holding only its visible pieces (a layer only shows where the layers above do not cover it). In the pile areas under 1% of pixels differ by more than 8 of 255 (maximum 43), hairline edge pixels only; `check-board-ui`, `perspective-table`, `interactions`, `battle-presentation`, `main-menu` pass.

**Where table memory goes** (macOS footprint of the running table, 2363 MB): GPU allocations 1317 MB, large CPU allocations 323 MB, small allocations 216 MB, other virtual allocations 232 MB. The title menu is 414 MB, so the table adds about 1.9 GB, over half of it textures. Textures by size: the cloth `navy-mat-topaz-4x.webp` was 6688x3764 (96 MB, 128 with mips); the brazier and the room lantern each carried two 4096x4096 maps (about 85 MB each with mips) for objects only a few dozen pixels tall; about 28 more 2048x2048 maps are 21 MB each.

**What changed.** Godot import `process/size_limit` (a lossless resize, no encode time): brazier, room lantern and chest maps to 1024, stone maps to 1024, the cloth to 3344 (still twice the 1672 px source it was upscaled from). At the game camera and at near-maximum zoom the hall differs from before in at most a few hundred pixels by at most 8 of 255.

**BC7 was dropped.** BPTC encoding takes 11m38s locally (47 CPU minutes) and stalled the Windows CI import at 30 minutes, with or without the ASTC encode. DXT is quick but changes 21% of hall pixels. Size caps give a larger saving without any of that.

**Remaining.** The hall viewport and table canvas redraw rules, 2D draw calls beyond the piles (about 100 for hand and province cards), the remaining 2048 maps (columns, dais, floors, which fill large areas and were left alone), and old-GPU testing.

## Phase 4 (issue #886)

**Column and dais maps.** Capped at 1024 through import `process/size_limit`: table texture memory 598 to 526 MB, resident footprint about 1480 to 1380 MB; 1.0% of hall pixels differ by more than 2 of 255 (maximum 26), the crops show slightly smoother scratch highlights on the columns and no visible change on the dais. The three floor map sets were tried at 1024 too (415 MB, 1229 MB resident) but the wood grain is visibly softer (18% of hall pixels differ, maximum 48), so the floors stay at 2048.

**Ink mat and province UI checks were real stale checks, not environment problems.** Earlier reports of these as "hover or focus" failures were wrong.
- `check-ink-mat` failed with "Holding hover signal did not activate the existing card feedback". Since #780 a card takes hover only when the pointer actually moves over it (`CardTile` records a bare `MouseEntered` as pending until a mouse-motion `GuiInput` arrives). The probe emitted only `MouseEntered`. It now also sends the motion event, as a real pointer would.
- `check-province-ui` failed with `selected=preview-0-1-a` when clicking the Region `preview-region-0-1`. Since #780 the Region sits partly under its Dynasty card, so the Region's centre selects the card on top. The check now clicks an uncovered point on the tile.

Both fail on any machine from #780 onward, not just here; neither is run by CI.
