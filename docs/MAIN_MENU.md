# Moonlit main menu — implementation and acceptance

2026-09-07 · issue #8 · PR #9 · based on the user's approved Moonlit Study attachment.

## Result

Normal startup shows the moonlit menu without creating a match or allowing hidden practice-opponent turns. Play chooses Crane or Lion using the existing published-match engine. Private Match and Card Library open their existing flows. A failed private-server validation keeps the player in the menu; successful private entry waits for an authoritative connected snapshot before showing the board.

Escape or the table menu opens the main menu. Local practice pauses and can be resumed via Play → Return to table. A private match continues on the server, explicitly stated in the menu. Replacing practice and quitting require confirmation; local practice is not saved across application launches. Replay export, coverage and concession remain available from the table menu.

Options apply and persist window mode, VSync, reduced motion and menu atmosphere strength. F11 works from either menu or board. Preferences are device-local and separate from private-room credentials. Native test captures use unique isolated settings profiles. No rules, payment choices, legal-action definitions, transport protocol or hidden-state authority changed.

## Art and motion

The built-in image generator removed only painted navigation from the approved image while preserving the title/logo and scene. Live controls use the supplied Musashi Brush font; its glyphs differ from generated concept lettering. Background composition, title artwork, navy cloth, decks, castle and lanterns were visually compared to the approved image. This is close visual matching, not a claim of pixel identity to the original generation.

The 2.5D shader restricts cloud drift to the sky, protects the moon disk, moves subtle valley mist, and varies lantern spill, moon halo and individual distant window lights. Architecture, decks and lantern geometry stay rigid. Reduced motion freezes atmospheric animation and makes card transfers immediate. Aspect-preserving framing uses dark margins at non-16:9 ratios instead of stretching/cropping the scene.

Runtime assets: `src/L5R.Client/Art/moonlit-menu.png` and `moonlit-menu.gdshader`. Source reference and exact imagegen prompt: `assets/ui/main-menu/`. Native captures: [main menu](screenshots/moonlit-main-menu.png), [options](screenshots/moonlit-menu-options.png). [Ten-second native atmospheric preview](videos/moonlit-main-menu.mp4), sampled at 8 fps; this is a review recording, not a runtime frame-rate benchmark.

## Validation

- Client builds completed with zero warnings/errors; Godot import and native rendering succeeded on this Mac.
- Native menu checks: no startup match; Clan selection; unchanged practice while in menu; resume; replacement confirmation/cancel; shared reduced-motion setting; fresh preference-instance persistence.
- Actual Godot input events: Enter activates Play, Escape navigates back, mouse coordinates activate Options/toggle motion, F11 enters/exits fullscreen, and quitting shows confirmation.
- Private-server invalid-input path remains on menu; Card Library opens from startup.
- Native captures inspected at 1672×942 (the OS rounded requested height), 1280×800 and 2560×1080. Refined live heading size, options contrast and focus lifecycle after review.
- Two independent reduced-motion captures at animation clocks 0 and 22 were pixel-identical. Animated comparisons changed sky/lantern/window regions; the title region had zero difference. Card-region light changes are illumination, not geometric displacement.
- Existing native board-assignment and published Border Keep refresh captures passed, including actual board controls.
- 29 collaboration tests and all 148 existing content/rules/AI/server/transport checks passed across the recorded runs. Initial server tests were blocked by sandbox localhost binding; those five server tests plus four transport checks passed when rerun with local socket access. Logs: ignored `artifacts/checks/main-menu-*.log` and `final-menu-*.log`.

Run `bash tools/check-main-menu.sh` for menu acceptance captures and `bash tools/run.sh` to launch this checkout. The prior downloadable release is unchanged; this PR contains source/runtime assets and awaits review. Native Windows graphical acceptance and a live external private-match playtest remain separate product gates.
