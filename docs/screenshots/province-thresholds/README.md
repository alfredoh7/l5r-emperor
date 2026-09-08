# Province threshold evidence — issue #56 / PR #57

Recorded natively on macOS with pinned Godot 4.7.2 .NET. The images are actual renderer output, not regenerated gameplay mockups.

- `reference-comparison.png`: original A/C regions beside `InkMatSections`, at equivalent art scale. This isolated art review is explicitly labeled and does not claim to be gameplay.
- `provinces-close.png`: two surviving province bays through the ordinary game camera; one locally emphasized to demonstrate selection styling. Actual practice match cards, including face-down cards, are preserved.
- `overview.png`, `small-window.png`, `player-side.png`: final native mat regressions at 1920×1080, 1280×800 and the closer player-side camera. Practice fixtures can differ because they are reached through legal training play.
- `purchase-ink.mp4`: 120 native frames, 24 fps, five seconds. Real Gold Mine purchase at frame 12, four-to-five in-play Holdings group including Stronghold; no further game decision. Companion JSON records bounds, reveal and motion; replay contains the practice setup/command history.

## Checks and review

- Client build: zero warnings/errors.
- `bash tools/check-ink-mat.sh`: all three native layouts and the real purchase film passed. It checks card membership, resting corner clearance, left-side Holdings, attachments, hover/select/deselect, named input-transparent hotspots, empty surviving province identity, no renumbering after removal, emphasis without frame movement/repainting, organic reveal after actual motion, delayed removal, retargeting and immediate reduced motion. Isolated geometry tests are distinguished from actual game events in the logs.
- `bash tools/check-table-camera.sh`: all eight native cases passed — overview, battle focus, 16:10, ultrawide, assignments, attachment expansion, pile tooltip and reduced motion. Includes projected upright/bowed edge clicks and camera/modal isolation.
- Additional `published-board-ink-provinces` close capture passed the native checks.
- Launcher regenerated; `bash 'Play Latest.command' --headless --quit-after 2` passed.
- `ffprobe`: 1920×1080, 120 frames, 24 fps, 5.000 seconds.
- Visually inspected the original A/C art, comparison, normal/small/close gameplay views, and purchase frames before movement, during apparition and after settlement.

Adversarial review caught the concept's fourth province being brighter by default; production now reuses the neutral frame and varies only the original numeral, reserving emphasis for actual interaction. The old detached marker has been removed, so geometry and labels cannot drift away from their province. Hotspots never consume card/camera clicks. The native close icon test frames an offscreen icon temporarily and restores its camera. The animation recorder now waits for deferred layout and explicitly draws, avoiding macOS occluded-window frame-signal stalls.

These checks do not constitute expert rules acceptance or native Windows graphical testing. Whole-table zoom reduces fine ornament/number size; hover names and wheel zoom remain available. Human visual acceptance and the requested shared-client review remain pending.
