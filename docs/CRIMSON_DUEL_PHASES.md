# Crimson duel implementation

Approved target: `assets/ui/duel-arena/crimson/approved-crimson-standoff.png`. Issue #205 / draft PR #220.

## 1. Playing surface and composition

1.1 Retain approved artwork and derive a clean mat texture with no cards or controls.
1.2 Build editable cloth geometry in Blender and a matching native Godot/card camera.
1.3 Place original personality scans flat and prominent, attachment stacks, focus cards, local hand and sparse controls; compare to the target.

## 2. Hybrid landscape

2.1 Derive a detailed clean landscape plate with no mat or game elements.
2.2 Keep distant mountains and sun fixed, mask cloud movement to the sky, and blend geometry into the field.
2.3 Use instanced grass at the mat perimeter with no foreground reeds obscuring the play area.

## 3. Atmosphere and animation

3.1 Coherent traveling wind, root anchoring, varied stiffness/phase and finer leaf motion.
3.2 Slow cloud displacement at different rates, with protected sun/mountain silhouettes.
3.3 Reduced motion freezes all ambient effects; card readability and input remain stable.

## 4. Native iteration and acceptance

4.1 Capture composition checkpoints and compare actual game-rendered images with the full target.
4.2 Check normal/crowded/zoom/other-seat/reveal views and actual projected card clicks.
4.3 Record native motion and sustained frame-time distributions, inspect temporal stability, and report visual/performance limits without calling a still screenshot AAA acceptance.

The generation prompts are in `assets/ui/duel-arena/crimson/asset-prompts.json`. The scene is a presentation fixture; live duel decisions remain a subsequent integration stage.

## Completed native pass — 2026-09-15

Phases 1–3 are implemented in `CrimsonDuelStage.cs`, with a native public-card review in `CrimsonDuelReview.cs`. Blender exports an editable shallow cloth and three geometry tuft variants; Godot adds segmented textured foliage. The default review launcher now opens Crimson Standoff. Historical garden assets remain available but are not loaded by this scene.

Five retained native checkpoints document actual corrections: excessive grass height/density, flipped glTF root coordinates, square focus-back proportions, bright foliage transparency, and final mipmapped/graded foliage. Original card scans replace the concept's generated faces. The mat uses dedicated derived artwork with the approved crest, border proportions and aged blood; it is not an exact pixel copy of the perspective concept.

The environment combines a detailed landscape plate with actual foreground geometry. Cloud movement is bounded, masked image displacement; it does not simulate volumetric weather. The sun and mountain silhouette remain fixed. Grass has world-space traveling gusts, root anchoring, arc bending, stiffness variation and smaller leaf movement. There are 821 grass instances and 7 reported world draw calls in the current view.

## Validation and evidence

- Client compilation: 0 warnings and 0 errors; Blender export, Godot resource import and main headless startup passed.
- All 9 `tools/check-crimson-duel.sh` native views passed: normal, crowded/bowed/revealed/sidebar, other seat, 1080p, scenery close-up, zoom, two reduced-motion times and later animated time.
- Normal, crowded and other-seat probes each opened the correct personality and hand card through native projected input. Maximum measured 3D/card projection error was 0.17575 pixels across these captures. Default-fit card containment passed.
- All 8 original `tools/check-table-camera.sh` scenarios passed with the unchanged default board projection.
- Reduced-motion images at times 2 and 25 were byte-identical. Enabled-motion region checks found changing clouds and grass while the sun and interior mat/cards stayed identical.
- The actual `Review Duel Arena.command` launcher built/imported/launched successfully and passed both native card clicks. `tools/create-dev-launcher.py` refreshed the separate main-game app, which does not yet enter this scene from a match.
- Native 1920×1080 animation benchmark on Apple M1 Max, Godot Compatibility: 2,641 monotonic wall-clock frame samples after a 3-second warmup in a 25-second run. P50 **7.6535 ms**, P95 **13.536084 ms**, P99 **13.93725 ms**. This is a local presentation observation, not full-match or cross-platform qualification.
- A 15-second, 450-frame native movie at 1672×942/30fps is available locally in `artifacts/crimson-motion/crimson-duel-locked.mp4`. Capture locks input; the first take was superseded after desktop scrolling changed its zoom. The interior mat/cards are identical across all 450 final source frames. See `motion-continuity.json` for final frame analysis.

Open [the comparison gallery](screenshots/duel-arena/crimson/index.html) for the approved target, native result, movie and stress views. JSON capture metadata accompanies the images. The local movie and frame sequence are intentionally excluded from Git.

## Remaining phases

4.4 Alfredo reviews the running motion and composition. The benchmark and frame checks do not certify AAA visual quality. Remaining tuning should respond to visible artifacts in motion, especially silhouette transitions where 3D foliage meets the painted field.

5.1 Coordinate live duel entry/exit, actor/attachment views and private focused-card presentation against Mendon's engine contract.
5.2 Connect the sparse controls only to legal engine choices; implement reveal, strike and outcome transitions without local rules authority.
5.3 Validate full-match behavior, native Windows rendering/input and wider hardware performance. The present review uses public fixture cards and disabled decision buttons; it is not a playable duel.
