# Custom Practice setup

Issue [#69](https://github.com/alfredoh7/l5r-emperor/issues/69), PR [#262](https://github.com/alfredoh7/l5r-emperor/pull/262). Stacked on the approved Fallen UI PR #261, preserving the consolidated lacquer UI. The separate Duel Demo #220 is excluded.

Open **Play → Practice**, or **Practice** in the table menu. Choose a saved deck or built-in preset for either player. The browser keeps both selections visible. Search and import use local deck text files; **Edit deck** opens the existing builder. Saving and returning selects the saved result. Incomplete drafts remain saveable, and their start issues are shown inline. Starting re-reads selected files, validates both decks in Core, then replaces the current match only after the existing-match confirmation. Clicking Stronghold art opens its full original face.

The match receives the selected compositions, Strongholds and starting Holdings. Same-Clan Practice works. Deck snapshots are embedded in custom replays, so later edits to deck files do not change recorded games. Private rooms retain their existing presets; custom multiplayer is separate work in #70.

## Visual implementation

[Native review gallery](screenshots/practice/index.html) includes the approved A/B/C images for comparison, plus full-card inspection, 1280×720 layout and an actual started match. Native fixtures are isolated from the player's saved decks. Frames and Clan crests are cropped from approved art. The navy background and blank gold button were derived with imagegen; exact prompts are in `assets/ui/practice`. Live typography uses Caudex and WeatheredQuill. Native original Stronghold art replaces illustrative concept castles, so the game identifies the actual selected card.

## Validation — 2026-09-16

- `bash tools/check.sh --skip-client`: **457 checks passed** (43 collaboration, 130 content/tool, 172 Core, 52 published, 9 deck format, 10 workshop, 13 custom Practice, 18 AI, 5 server, 5 client transport).
- Four custom AI match pairings (Crane/Lion, Lion/Crane, Crane/Crane, Lion/Lion) completed legally in 215–324 decisions and reproduced their final views from replays.
- `bash tools/check-practice-setup.sh`: client compilation with zero warnings/errors, Godot import without errors, native selection/search, draft edit/save return, missing-file rejection, full Stronghold inspection, resize, local seat/name and main-menu-to-match checks passed.
- Native `menu-flow`, `menu-input`, `menu-play` captures: current-match replacement/cancel, pause/resume, name ownership, preferences, keyboard navigation, fullscreen and quit checks passed.
- `python3 tools/ensure_catalogue_art.py --verify-only`: pinned local WebP pack verified. `Play Latest.command` compiled and launched this checkout and captured its Play menu without runtime errors.

Logs are in `docs/screenshots/practice`. Automated checks do not substitute for Mendon's review of the shared custom-deck launch contract, historical card-specific construction exceptions, or native Windows playtesting. Review and CI are required before merge.
