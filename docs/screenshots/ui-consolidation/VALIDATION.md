# Consolidated UI validation — 2026-09-16

Combined branch: `codex/247-ui-consolidation`, based on main `5f48af8`. Source merges: `47af35c`, `031f59c`, `888fb32`; integration fixes and these captures are committed with this record. Source inventory and exclusions: [UI_CONSOLIDATION.md](../../UI_CONSOLIDATION.md).

## Deterministic checks

`bash tools/check.sh` passed on the initial combined build, including client compilation, Godot import and headless smoke. After the defender-query correction, `bash tools/check.sh --skip-client` passed again:

| Suite | Passed |
| --- | ---: |
| Collaboration tooling | 43 |
| Content tooling | 130 |
| Core | 154 |
| Published cards | 52 |
| Deck format | 9 |
| Deck workshop | 10 |
| AI | 14 |
| Server integration | 5 |
| Client transport | 4 |

The final client build passed with zero warnings/errors. Godot import succeeded. `Play Latest.command --headless --quit-after 2 --profile=consolidation-launch-smoke` successfully installed/verified the mandatory WebP pack, built and started the current checkout. The Mac application launcher was regenerated with `tools/create-dev-launcher.py`.

## Native Mac checks

Run with `tools/check-ui-consolidation.sh`, using its `L5R_UI_CASES` subset for focused reruns after failures; Home coverage additionally uses `tools/check-persistent-home.sh`.

- 20 full-game capture/input modes at 1672 × 941: menu, help, history, coverage, private match, confirmation, tooltip, card inspection, battle target/waiting/outcome, removed gallery, attacker and defender confirmation, expanded armies, target confirmation, payment, purchase, menu flow and native keyboard/mouse navigation. macOS renders the requested odd-height window at 942 pixels.
- Four compact modes at 1280 × 800: menu, help, battle target and removed gallery. Visual inspection confirmed controls and dismissal fit.
- Catalogue: native selection/deselection, filters/search, virtual rows, Escape, compact layout, bounded textures, local verified WebP loading, safe old-JPEG cache retirement and corrupt-member rejection without HTTP fallback.
- Deck builder: real native setup/quantity changes, search, rules warnings, list/mat toggles, comparison and long-rule scrolling, exact save/reopen, bad import preserving draft, unsaved/external-edit guards, density, 1280 × 800 and 1920 × 1080 layouts, main-menu entry.
- Placard: approved-source/native capture, known/modified/over-limit/unknown hand states, clan click and hand focus.
- Phase rail at 1672 × 941 and scaled 1280 × 720: all engine enum mappings (including Cavalry), six visual states, hover help, keyboard arrows/Escape, no phase-bar Pass dispatch, pixel-stable reduced motion and Finished unlit.
- Home at 1920 × 1080 reduced motion and 1440 × 900 normal motion: real Lobby/Favor gain/spend/transfer, opponent inspection, battle Favor action, Shunya presence/effect loss, visible Reserve recruitment button/engine command, Air Ring bow/action, required decision priority, new-game reset, actual Suspicions reveal/Honor/untargetable reminder, both seats, crowded/zoomed Home and mixed removed piles without overlap. Capacity duplicates are explicitly staged layout fixtures.

## Integration defects found and resolved

- Battle flow intercepted selected Favor/reminder inspection. Inspection now takes precedence except while a required choice is pending.
- A fixed three-type battle-action filter hid current-main Reserve recruitment. The list uses the engine's enabled actions, preserving footer actions and the separately confirmed Concede flow.
- Defender query destinations included unattacked provinces; confirming such a choice was rejected by the existing engine. Both Infantry and Cavalry queries now match `AssignDefense`'s existing battlefield restriction. The new Core regression executes every offered defender command.
- Cavalry phases were missing from the rail's Attack mapping and displayed an unstyled disabled help control. Mapping and disabled artwork are corrected.
- The menu-input probe used an old child index for Options after Deck Builder was added. It now identifies the intended button by text. Assignment probes verify the committed session snapshot, which can precede animation's displayed snapshot. The phase probe now moves both the real and injected cursor away after hover so OS motion cannot reopen help during its later assertions.

An initial Home click and an initial menu Escape assertion did not reproduce on subsequent native runs; no retry loop or production behavior change was used to suppress those assertions. Native focus/timing is not claimed as a proven cause.

## Evidence and limits

The [gallery](index.html) contains fresh native captures selected from these successful runs. It does not use historical screenshots as merged-build evidence. Raw logs/captures remain locally under `artifacts/ui-consolidation-checks`, `artifacts/checks/persistent-home-*` and `/tmp/l5r-consolidation-*.log`.

All seven source UI PR heads and the explicit Suspicions dependency are ancestors of this branch. The Duel Demo head is not; no demo-specific paths are included. Original worktrees/branches are preserved.

CI and Mendon's shared review are tracked in [PR #248](https://github.com/alfredoh7/l5r-emperor/pull/248). The initial PR head passed Mac and Windows CI, but automated review routing failed because GitHub reported zero changed files while its paginated file API returned 588. The guard was not bypassed. Native Windows graphical testing, a two-machine match and expert rules acceptance are not claimed by this validation.
