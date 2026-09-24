# B6 Lantern Study main menu

Alfredo approved the B6 folding-screen concept with the slow lantern animation at 85% on 2026-09-24, then requested native implementation and source/native iteration. UI task: #470, alfredoh7. This approval does not authorize merging.

Retain the approved pixels: textured indigo folding screen, faded five-ring composition, Japanese pine-and-cloud border, cropped bonsai and lantern at the left. The six main entries sit on the right. Preserve working native navigation, confirmation, keyboard access, full-frame aspect fitting and reduced motion. Port the approved slow shader rather than changing its timing. Keep new menu illumination settings separate from existing saved atmosphere preferences.

Source image: `src/L5R.Client/Art/lantern-study.png`; immutable approval, concept prompt and browser shader reference: `assets/ui/main-menu/lantern-study/`. Implementation and native comparison evidence are recorded on [PR #471](https://github.com/alfredoh7/l5r-emperor/pull/471) and in `docs/screenshots/lantern-menu/`. No mechanics or pinned profile changes.
