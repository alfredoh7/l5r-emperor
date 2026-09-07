# Fullscreen, card materials, and candlelight · 0.3.2

**Historical checkpoint:** [Version 0.3.3](QUIET_HUD.md) adds the reference-inspired phase track and clan plaques, narrows the sidebar, replaces count labels with physical piles, and removes board summaries. Dimensions and persistent labels below describe 0.3.2.

The playable table now opens in fullscreen and uses the entire display. Layout width follows the monitor's aspect ratio, with a larger inspector and larger complete card faces. F11 leaves fullscreen and presents a return-to-play screen; the match stays open and the local practice opponent pauses. Online state can still arrive while the local controls are gated. The return button restores fullscreen. Settings also provides fullscreen and quit controls.

## Cards and reading

At 1920 × 1080, home faces are 144 pixels tall, province faces 158, and hand faces 190. Battlefield cards are approximately 94 pixels tall in the crowded acceptance fixture; opening a unit spreads its attachments into a larger reading tray. The inspector occupies 410 pixels of width, with a 510-pixel-high full-card reading area. Hover previews have the same 510-pixel art height, plus readable typeset rules and context. At 3840 × 2160 these dimensions double. System fonts, detached hover previews, and dialogs also scale for that display density.

These are full scan faces, including borders, statistics, rules, and credits. Small printed rules still require the hover preview or inspector. Fullscreen cannot make all the fine print on dozens of resting cards readable simultaneously.

Crowded home and battlefield shelves page by whole units instead of shrinking every card to accommodate the row. Home Personalities appear first, followed by the Stronghold and economy cards. Bowing, attachment ownership, unit expansion, province assignment, and confirmation still use the existing live controls. Deck, discard, and opposing hand counts remain visible under the home rows. Battlefield headings name the defending Clan and province; home cards do not acquire a province association.

The card material adds restrained ivory paper grading, fine grain, darker worn edges, and a soft candle reflection. Reading views use a lighter treatment than resting cards. All grading is reversible at render time; the original scan files were not edited. The canvas shader uses fragment `COLOR` directly because Godot has already included the texture sample at that stage ([official shader reference](https://docs.godotengine.org/en/latest/tutorials/shaders/shader_reference/canvas_item_shader.html)).

## What the environment is

The table remains the selected painted illustration on a Blender-exported projection mesh. The candle, wood, cloth, and props are painted scenery, not separate 3D objects. The camera is fixed. This pass adds live shader animation: localized flame sway, a soft warm light pool on the nearby table, and subtle reflection on nearby card faces. It does not introduce real geometry-based shadows or a freely movable 3D environment.

Gameplay fills the viewport by expanding the central cloth horizontally while preserving the side props' proportions. Cards keep their own aspect ratios. This intentionally differs from the historical aspect-fit review, which showed margins. The isolated environment review still retains the original composition and static lighting; its earlier pixel-preservation result does not describe the responsive, animated gameplay render.

Reduced motion in Settings stops the flame and changing light, as well as the existing card motion. The source plate remains 1586 × 992, so the painted scenery cannot gain native 4K detail from a fullscreen layout change.

## Review evidence

- [Fullscreen from the delivered Mac app](screenshots/fullscreen-v0.3.2.png)
- [Large hover reading and expanded unit](screenshots/fullscreen-hover-v0.3.2.png)
- [Ultrawide composition](screenshots/fullscreen-ultrawide-v0.3.2.png)
- [16:10 composition](screenshots/fullscreen-16x10-v0.3.2.png)
- [Native candlelight animation](videos/candlelight-v0.3.2.mp4)

The video is a five-second sequence of actual Godot viewport renders sampled at 12 candle phases per second. It is an animation study, not a frame-rate benchmark. No offline lighting or card compositing was added.

The existing regression suite passed all 148 checks: 59 content, 36 core, 31 published-card, 13 AI, 5 server, and 4 client transport checks. Native acceptance checks exercised attack and defense assignment confirmation, ability targeting, individual attachment selection, actual mouse hover, hidden-card protection, shelf pagination, reduced motion, and fullscreen exit/re-entry. The paging probe reached every card across three crowded shelves without changing the match. Two independent candle phases produced identical frames after the real Settings reduced-motion checkbox was enabled.

Layout captures checked 1920 × 1080, 1440 × 900, 2560 × 1080, and actual fullscreen at 3840 × 2160. They assert that the presentation fills the viewport and visible card faces remain within their shelves and viewport, with correct bow states and no duplicated or omitted card nodes. Native screenshots were also visually inspected; these assertions alone do not establish visual quality. Logs, earlier iterations, and native animation frames are in `artifacts/fullscreen-polish/`.

Mac and Windows development exports were rebuilt and passed package validation. The delivered Mac app reports version 0.3.2 and also passed the native fullscreen assignment capture at 3840 × 2160. Client compilation completed with zero warnings and errors. Native visual acceptance is on macOS; Windows gameplay and GPU performance remain unverified. This pass improves the playable presentation; the scripted tutorial and further scene geometry, HUD art, and motion work remain future phases.
