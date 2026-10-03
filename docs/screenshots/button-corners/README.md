# Transparent button corners — 24 September 2026

Alfredo reported the black rectangles outside the shaped corners of Keep playing in the #448 Concede preview. The same opaque atlas crop supplied shared lacquer buttons, gold buttons/tabs and duel buttons. Practice and Fallen-gallery buttons sampled similarly opaque source art; the phase-help circle also included its source scene outside the ring.

The live extraction now removes only near-black matte connected to the crop boundary, confined to the measured frame band. Enclosed lacquer remains opaque. Alpha edges are fixed before half-size filtering, and the resulting textures are cached. The exact source artwork, borders, hit regions and actions are retained. The phase-help badge uses the same boundary extraction with a threshold matched to its darker blue scene matte. Fallen-gallery buttons also inset their center fill behind the cleaned border. Keyboard focus follows the gold silhouette instead of a surrounding rectangular box. Practice's center fill is inset behind its border so it cannot repaint transparent corners.

Deck-builder button crops sit inside their own outer silhouette; native inspection did not reproduce this issue there. They were inspected but not changed. Text-only and intentional rectangular controls were also left as designed.

## Evidence

- `concede-before.png` is the previous committed native capture; `concede-after.png` is the corrected same dialog at 1920×1080.
- `state-matrix.png` is a native Godot rendering of the actual shared styles on alternating blue/gold squares. Its bottom row shows the former opaque crop at matching border scale. Rows show lacquer states, gold samples, duel states, unchanged deck materials and practice variants. The practice labels are sample labels, not an input-state assertion.
- A temporary native witness read the actual extracted lacquer texture: all four exterior corner pixels have alpha 0, while its center remains opaque. The witness scene was removed after capture and the client rebuilt.
- `practice-after.png` and `deck-builder-checked.png` come from the existing native interaction suites.

Validation: clean client compilation (0 warnings/errors); `bash tools/check-duel-followups.sh concede discard-reduced` passed both scenarios; `bash tools/check-practice-setup.sh` passed; `bash tools/check-deck-builder.sh` passed. Additional existing native probes passed for the phase rail (hover, keyboard, no Pass dispatch, reduced motion) and Fallen gallery at 1920×1080 and 1280×720 (mouse and keyboard tabs, selection, empty refresh). These exercise real Concede mouse/keyboard cancellation and confirmation, compact/reduced-motion Focus choices, Practice navigation/selection/validation, and deck-builder menus and controls. Captures were inspected at playable size and over a contrasting background.

This is a cosmetic review correction in PR #466. It does not change rules or constitute human acceptance, Windows graphical playtesting or a merge.
