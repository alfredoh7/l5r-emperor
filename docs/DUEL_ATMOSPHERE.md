# Crimson Standoff atmosphere prototype

This is a presentation prototype for issue #205 / PR #220 while mdornbrook reviews the duel mechanics. The existing mat, card projection, controls, introductions and authoritative resolution are preserved. This is not final AAA visual acceptance or normal-match integration.

## What changed

The approved painted landscape remains the detailed background. Two slowly moving sky bands preserve the sun, mountain silhouettes and mat. Distant grass uses a restrained wave displacement. A transparent 3D viewport adds 38 segmented photographic tufts and 150 narrow stems in two instanced draws, with anchored roots, bending, tip motion and a common traveling gust. They stay beyond the far mat edge. No foreground reeds are restored.

The gameplay and introduction scenes use the same atmosphere clock and gust strength. The introduction's public card viewport is prepared beneath the stakes presentation, avoiding resource creation at the first camera cut. Menus and card reading pause motion and sound. Reduced motion fixes the atmosphere at its initial state and keeps the existing static/skip presentation.

Procedural stereo wind, positioned paper accents and a short Strike accent support the existing sequence. Wind settles during the introduction and just before the cut, then returns gradually. **Menu → Field ambience**, **Cinematic sound** and **Detailed grass** are independent session controls. These sounds are prototype synthesis, not a final recorded soundscape. No new rules, legal decisions or result calculations are introduced. The native help probe scrolls menu controls into view before clicking; its timing is fixed for repeatability, separately from performance sampling.

## Review and reproduce

Open the normal development launcher, then **Play → Duel Demo**. Restart the seed-205 Steel on Steel position to review the opening, focus movement, Strike, source-linked consequences and held result.

The separate review scene exercises the actual `DuelScreen` and `MatchSession`; it is a development capture tool inside the same client. It adds six seconds of idle lead-in only for the recording. It submits the real challenge, a permitted blind opponent focus, native mouse selection/placement of Courtesy, and the opponent's legal Strike. It verifies the retained **8–7** result, restored priority, one opening/ending, and Continue. It does not synthesize an outcome for the film.

```sh
source tools/env.sh
dotnet build src/L5R.Client/L5R.Client.csproj
"$GODOT" --path src/L5R.Client --resolution 1920x1080 \
  res://DuelAtmosphereReview.tscn -- --benchmark --mute \
  --capture="$PWD/artifacts/duel-atmosphere/performance"
```

Omit `--benchmark` to save review PNGs. `--light` disables the extra grass viewport. `--reduced-motion` verifies the static alternative. `--scenery --time=0` / `--time=20` capture fixed atmosphere states independently of cards.

Movie capture uses Godot Movie Maker at 30 fps with audio; it is **offline rendering and not performance evidence**. The local review movie is `artifacts/duel-atmosphere/crimson-atmosphere.mp4`. Set both viewport and initial window dimensions to 1920×1080 before capture: in this setup, `--resolution` alone changes the window after the movie writer selects its dimensions and crops the resulting recording. A temporary, exclusively created `src/L5R.Client/override.cfg` supplied those four display dimensions and was removed afterwards; `project.godot` was preserved. The review window stays on top, and offline capture explicitly draws each frame so macOS window occlusion cannot freeze the recording. This forced draw is not used by the real-time benchmark or normal gameplay. See [Godot Movie Maker](https://docs.godotengine.org/en/stable/tutorials/animation/creating_movies.html) and [project-setting overrides](https://docs.godotengine.org/en/4.6/classes/class_projectsettings.html).

## Evidence and remaining work

Current measurements and native checks are recorded in [the validation report](screenshots/duel-demo/atmosphere/validation.json). They are local Mac evidence, not native Windows testing. Timing uses monotonic wall-clock intervals between completed frames from `RenderingServer.FramePostDraw`, excludes the first three seconds of startup, and records outliers as well as percentiles. Sound is muted during performance sampling. Screenshot/movie encoding is excluded by a separate benchmark run.

The sky is still a flow-mapped painted image, not volumetric cloud simulation. There is no full wind-driven field stretching into the distant terrain. The photographic tufts have limited viewing angles, and their scale, blending and response need human motion review. A final pass can replace these with authored foliage meshes/atlases and separated cloud layers while keeping the same timing and controls. The remaining occasional card/transition frame spikes should be profiled before a frame-pacing acceptance claim.

Mendon's rules review, the effective-province-strength reporting finding, normal-match activation and final grass/cloud/sound acceptance remain separate. The existing mechanics are unchanged by this prototype.
