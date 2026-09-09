# Left battle log removed

Alfredo withdrew the left-side Battle exchanges panel after playtesting. These current native captures supersede the earlier journal-layout screenshots; they use separate published practice fixtures rather than an identical before/after match state.

- [1920×1080 action destruction](named-defeat.png): no left panel or toggle; restored board width; named destruction notice and Battle action cause remain.
- [1280×800 reduced-motion province fall](reduced-motion.png): no left log after resolution either; surviving province positions and public history remain.
- [2560×1080 camera/input](ultrawide.png): wheel, drag, arrow pan, Home reset, fixed HUD, inspector scrolling, modal isolation and normal card selection passed without mutating the match.

Client compilation and headless Godot import passed. All three native scenarios passed and these images were inspected. Launcher was refreshed and its executable/plist/checkout target verified. No rules or engine events were changed in this follow-up. Native Windows graphical testing was not performed. The preceding pass’s full deterministic/CI results are historical; this follow-up runs its own GitHub checks.
