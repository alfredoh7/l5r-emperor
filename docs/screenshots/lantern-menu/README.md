# B6 native main menu — #470 / PR #471

The native menu uses the approved B6 source unchanged, with the slow approved lantern shader at 85%, full-frame fitting, native mouse/keyboard navigation and functional submenus. `index.html` displays source/native views at matching scale and provides a comparison slider. The source PNG and ImageGen provenance are retained in the repository.

## Actual validation

- C# client build: zero warnings and errors; Godot import passed. The macOS CI job also passed the rules, AI, server, transport, content and native-client foundation checks at source `82bf8f65` ([run](https://github.com/alfredoh7/l5r-emperor/actions/runs/35992694193/job/107611009328)). Its first bootstrap attempt timed out downloading Godot; the isolated job retry passed.
- `tools/check-main-menu.sh`: native main/later, options, play, name, name input, selection, steady-light/later, practice flow, input, overlay return, private-dialog validation and library captures; 1280×800 and 2560×1080 fitting. Full run plus focused reruns after the name-heading and Escape/focus corrections passed (16 cases).
- Source/native image analysis: all 1,573,352 pixels in the steady-light native artwork area have zero RGB difference from the approved PNG. The native window is 1672×942 due to Retina rounding; its one-pixel bottom letterbox row is excluded. `pixel-validation.json` records dimensions, hash, region and results.
- Reduced-motion captures at times 0 and 22 are identical. Animated captures differ across the lantern, screen/sigils and floor; the timing and strength come directly from the approved shader.
- Native input checks cover Up/Down wrap and matching selection, Enter, mouse Options hit area, reduced motion, F11 fullscreen/windowed, Escape submenu return and Quit confirmation. Options strength persists independently of the legacy atmosphere value. Practice setup, start, pause, resume and cancellation retain existing behavior.
- Native overlay regressions cover Library and Deck Builder opening from the real menu, Escape closing them without opening Quit behind them, and keyboard focus returning to the invoking entry.
- One F11 transition assertion failed while a second live Godot instance was open; the isolated rerun passed. No unresolved input assertion remains.
- The local Mac review app was opened through the native UI and launched this checkout. Mouse navigation into Card Library and Private Match, Library Escape return, and visible room illumination were inspected. The private dialog's Close button was checked; no remote connection was attempted.

## Boundaries

Static pixel identity applies to the default main-page artwork at original scale with lighting held steady. Other aspect ratios contain the whole scene; submenus and active selection are functional native states. No assertion of identical GPU animation pixels across operating systems is made.

This branch starts at main `1e494ebe`; other unmerged UI previews remain separate. Main has not been merged or changed. Windows graphical acceptance remains open. Hosted CI currently cannot start because GitHub reports failed account payments or a spending limit; this is separate from native Mac verification. Windows review publication must retain the repository's validation gate. The [versioned Windows candidate](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr471-82bf8f659b-r1-windows) is staged with its GitHub asset size and SHA-256 verified. Local strict export/package validation passed. `previews/published/lantern-main-menu.json` pins its source, size and checksum. It remains **Validation pending**; Windows runtime CI, the ready-for-review notification and native Windows graphical acceptance are outstanding.
