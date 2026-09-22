# Approved quiet coaching + anchored help

Alfredo selected **03-quiet-coaching.png** as the default and **02-anchored-guidance.png** for optional additional help. **04-guidance-hidden.png** records the clean, suppressed state. These are exact imagegen concept outputs retained from the approved review, not native screenshots.

Runtime art: `src/L5R.Client/Art/DuelUi/ink-wash.png` and `red-slash.png`, created with the built-in imagegen tool using the approved quiet concept as reference. The original RGBA outputs are retained unchanged; Godot trims transparent bounds and resizes the blank wash for nine-slicing. The slash keeps its alpha and is slightly rotated for the action accent. Prompts and original source paths are in `runtime-prompts.json`; hashes and uses are in `provenance.json`.

Live type uses MusashiBrush.otf and Caudex-Regular.ttf. A simple native ivory checkbox and functional keyboard focus outline remain crisp. Buttons, semantic text, deck information, current legal options and results are not image text. No generated card artwork replaces the published scans.

The extra rectangular-panel imagegen attempts were rejected because they returned RGB with a baked checkerboard; those outputs are not used. The original transparent wash is the runtime surface.

Native/source comparison and actual gameplay captures: `docs/screenshots/duel-demo/quiet-guidance/index.html`. This is a visual-material and hierarchy implementation, not a pixel-identical composition: native camera perspective, real printed card faces, actual hand sizes and sparse engine-provided state differ from the illustrative concept. Existing grass/sky rendering is unchanged and has separate visual acceptance.
