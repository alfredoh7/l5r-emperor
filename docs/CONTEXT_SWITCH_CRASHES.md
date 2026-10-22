# Rapid context switching: test plan, findings and proposed fixes

Status: fixes implemented on `claude/1103-context-switch` (issue #1103), approved by Mendon on 2026-10-22. Base: `origin/main` at `51186ab8`.

Report: the game "crashes" when switching contexts quickly, e.g. start a game with any deck → quit → start a new game, or game → deck builder → new game.

## Summary

| # | Finding | Reproduced | Effect seen by the player |
|---|---------|-----------|---------------------------|
| F1 | `MatchSession.StartCustomPractice` does not advance `Generation`, so the table never learns that a new match replaced the old one | **Yes, 4/4**; one-line fix verified 3/3 | Leave a duel to the menu, start a new Practice: the old duel reopens over the new game and waits forever ("Opponent's move", no way back). Indistinguishable from a hang; a player force-quits. |
| F2 | The private-match "Enter" handler is `async void` and touches its dialog after the await without checking it still exists | **Yes, 2/2** (logged error) | Press Enter, close the dialog, start Practice: an `ObjectDisposedException` is logged. The game survives. Worse variant (code evidence only): a slow server answers after Practice started and `Connect` wipes the new game. |
| F3 | `Render()` with no snapshot frees tiles but keeps them in `_tiles`/`_flights` | No (code evidence) | Errors every frame for ~1 s if the snapshot is cleared while the table is visible (only reachable through F2's variant). |
| F4 | Other start paths do not reset the same per-match fields (`_autoOpponent`, `_autoProclaimCardId`, tutorial deal state) | No (code evidence) | Settings and an auto-proclaim intent can carry into the next match. |

Not reproduced in 540 random switches: any native crash, any `.NET` exception from menu / Practice / deck builder / library / tutorial switching, or any hang other than F1. The deck builder and library open/close paths were clean in every run.

Two macOS crash reports exist for this machine's Godot (`~/Library/Logs/DiagnosticReports/Godot-2026-10-18-085847.ips`, `…-10-20-221442.ips`): both are null-pointer aborts inside the engine binary with no symbols, one during app launch (Apple-event open handler) and one shallow call from `main`. Neither has a C# frame, and they cannot be tied to this report without the user's own logs. **We should ask the reporter for `user://crash-reports/` and `user://logs/godot.log`** (macOS: `~/Library/Application Support/Godot/app_userdata/L5R · Emperor/`), and whether the window closed or froze.

Note: `CrashReports` writes nothing for F1 or F2. A hang never raises an exception, and Godot's C# bridge catches the `async void` exception and only logs it. So the absence of crash reports does not mean the absence of a problem.

## Test plan

### Harness

Run it with `bash tools/check-context-switch.sh`. It starts a local server for the private-match case and fails on a crash, a hang (no step for 90 s), a failed assertion or any logged error. `L5R_CONTEXT_STEPS` sets the length of the random run (default 40).

`src/L5R.Client/ContextStressProbe.cs` / `.tscn` loads the real `Main.tscn`, with the bot running exactly as in play, and drives it through the same methods the menu buttons call:

- A step is chosen from: `practice` (random shipped deck or preset vs random), `menu`, `resume`, `deckbuilder`, `library`, `table-library` (Library → Deck Builder over a live table), `practice-edit` (deck editor from Practice setup, then Start), `tutorial`, `leave-tutorial`, plus targeted `duel-leave-practice` and `slow-online-then-practice`.
- Between steps it waits a random dwell: same frame, 1 frame, 0.05, 0.15, 0.4, 1, 2.5, 6, 15 or 30 s.
- The local seat is played by the training bot through `Main.Submit`, so presentations, card flights and duels run while switches happen.
- Every step is logged with `Generation`, version, phase and turn; a seed replays a run exactly (`-- --seed=N --steps=N [--only=a,b] [--dwell=s]`).

Verdicts:

- **crash**: a non-zero or abnormal exit code;
- **hang**: no step logged for 90 s (the script sends SIGABRT so the OS writes a report, then kills the game);
- **error**: any `ERROR` or `Unhandled exception` in the log;
- **targeted assertions**: after leaving a duel and starting Practice, no duel is open and the new game progresses; after a private-match connect races a new Practice, the Practice is still the match being played.

### Cases and results

| Case | Runs | Result |
|------|------|--------|
| Random mix, menu-level only (bot idle) | seed 1 × 40 steps | clean |
| Random mix, both seats playing, short dwells | seeds 2–4 × 60 | clean |
| Random mix including 15/30 s dwells (games to turn 5) | seeds 6–9 × 80 | clean |
| Duel → Leave to menu → new Practice | seed 10 × 4, seed 11 × 1 (20 s hold + screenshot) | **F1, 5/5** |
| Same, with `Generation++` added to `StartCustomPractice` | seed 12 × 3 | clean; new game reaches v24 in 20 s |
| Private match Enter → close dialog → Practice (unreachable address) | seed 13 × 1 | clean (request fails before `Connect`) |
| Same against a local server (`tools/run-server.sh`) | seed 14 × 2 | **F2, 2/2** logged `ObjectDisposedException` |

The investigation runs above used the earlier version of this probe (seeds as listed). The check script keeps seeds 10 (duel), 14 (online) and 6 (random).

### Still to cover

- **F2 slow-server variant:** the check starts Practice in the same frame as Enter, so the reply always arrives after it, and the Practice is kept. A delaying proxy would test replies that take seconds rather than a frame.
- **Windows:** the report may come from Windows. Run the same probe there through `tools/env.sh`.
- **Normal motion and reduced motion:** run both.
- **Window resize and fullscreen toggles** mixed into the random steps.

## Root cause and fixes

### F1 (primary)

`MatchSession.cs:153-159`: `StartPractice`, `StartTutorial`, `StartDuelDemo`, `StartPreparedReview`, `LeaveTutorial` and `Connect` all do `Generation++`; `StartCustomPractice` (the path behind Play → Practice → Start, i.e. every game "with any deck") and `StartShowcase` do not. Three things rely on that counter:

- `RouteMatchDuel` resets the duel flow only when the generation changes (`Main.MatchDuel.cs:25`).
- `_uiQueue` drops updates from an old match by comparing generations (`Main.cs:94-101`).
- The pass-device dialog does the same check (`Main.Dialogs.cs`, `SwitchSeat`).

`ConfirmLeaveMatchDuel` deliberately keeps the unfinished duel so Resume can return to it. Because the generation never changes, the next Practice inherits that duel.

Fix:
1. `Generation++` at the point where `StartCustomPractice` and `StartShowcase` replace the engine. This happens after validation, so a rejected deck still leaves the running game untouched.
2. One `Main.ResetMatchPresentation()` used by `StartFromMenu`, Practice `Begin` and `StartTutorial`, replacing the three hand-copied reset lists. It also restores and disposes a duel that is still open, and clears `_assignmentUnit` and `_autoProclaimCardId`. `_autoOpponent` stays where it was set (Practice turns the bot on; probes that call `StartFromMenu` keep it off).
3. Regression: the `duel-leave-practice` case in `tools/check-context-switch.sh`. It fails without the fix (6 assertion failures over 3 runs) and passes with it.

### F2

`Main.Dialogs.cs:56-66` (Enter) and `:67` (Reconnect):

- After `await`, the handlers touch the dialog only if `IsInstanceValid(dialog)`; a closed dialog's failure becomes a table notice instead.
- `CreateOrJoin` remembers `Generation` before the HTTP call. If a newer match started by the time the server answers, it saves the seat (so Reconnect still works) and gives up instead of calling `Connect`, which would wipe that match.
- `ReconnectMatchDuel` needs no change: `Resume` connects without an earlier await, and it already null-checks the duel.
- Regression: the `online-then-practice` case logged `ObjectDisposedException` twice without the fix, which fails the check.

### F3 and F4

- **F3:** `Render()`'s no-snapshot branch now finishes `_flights` and clears `_tiles` along with the freed board children.
- **F4:** folded into the shared reset in F1.2.

### Scope and risk

- All changes are client-side (`L5R.Client`). No rules, network-protocol or engine changes.
- The duel-resume behaviour within one match is unchanged, because Resume does not start a new match.
- Checks run (see the PR for results): the new stress check, `check-main-menu.sh`, `check-practice-setup.sh`, `check-duel-match.sh`, `check-duel-followups.sh`, `check-tutorial.sh`, `check-deck-builder.sh`, and `bash tools/check.sh --skip-client`.

## Windows validation handoff

For a second session on Windows: validate these findings there and fix anything Windows-only, without competing with the macOS fixes.

### Ownership while #1103 is open

The macOS session (branch `claude/1103-context-switch`, PR linked from #1103) owns these files until that PR merges:

- `src/L5R.Client/MatchSession.cs` (`StartCustomPractice`, `StartShowcase`, `CreateOrJoin`)
- `src/L5R.Client/Main.Frontend.cs` (`StartFromMenu`, `ResetMatchPresentation`)
- `src/L5R.Client/Main.Practice.cs`, `Main.Tutorial.cs` (`StartTutorial`), `Main.Dialogs.cs` (`ShowOnline`), `Main.cs` (`Render` no-snapshot branch)
- `src/L5R.Client/ContextStressProbe.cs`, `tools/check-context-switch.sh`, and this document

Do not edit those. If one of them needs a change on Windows, describe it as a comment on #1103 (file, line, failing log), and the macOS session will make it. Everything else is open.

### Setup

1. `git fetch origin` and create your own branch from the macOS branch, not from `main`: `git switch -c claude/1103-context-switch-windows origin/claude/1103-context-switch`. Use a separate worktree if the checkout has other work.
2. Follow `docs/PITFALLS.md`: use the shared Godot through `tools/env.sh` (`~/tools/godot` or `L5R_GODOT`); do not install a per-worktree Godot. Run scripts from Git Bash.
3. Install the art if needed (`python tools/ensure_catalogue_art.py`), then `bash tools/check-context-switch.sh`.

### What to validate

1. **The fix holds on Windows:** `bash tools/check-context-switch.sh` passes. It runs the duel → menu → new Practice case, the private-match race against a local server, and 40 random switches.
2. **The bug reproduces on Windows without the fix:** check out `origin/main` in a scratch worktree, copy in only `ContextStressProbe.cs`, `ContextStressProbe.tscn` and `tools/check-context-switch.sh`, then run the script. Expected: `duel-leave-practice` fails with "the previous match's duel reopened over the new Practice". If it passes on main, report that, since the bug would then be macOS-only.
3. **Longer random runs:** `L5R_CONTEXT_STEPS=150 bash tools/check-context-switch.sh` a few times. Change the seed by running the probe directly, e.g. `"$GODOT" --path src/L5R.Client res://ContextStressProbe.tscn -- --seed=21 --steps=150`.
4. **Things macOS could not test:**
   - an actual app crash on Windows (whether the window closes);
   - fullscreen and window resizing during switches;
   - Windows Event Viewer / WER crash dumps for `Godot*.exe`;
   - `user://crash-reports/` (`%APPDATA%\Godot\app_userdata\L5R · Emperor\crash-reports`) and `logs\godot.log` after a failure.
5. If the original reporter is on Windows, try their exact sequences by hand from the launcher: a game with any deck → Menu → Main menu → Practice → Start, repeated quickly; and game → Card Library → Deck Builder → back → Practice → Start.

### Script portability to watch

`tools/check-context-switch.sh` uses `kill -0`, `kill -ABRT` (hang watchdog), `curl`, `rg` and `$RANDOM`. If Git Bash cannot signal the Godot process, the watchdog falls through to `kill -9`. Fix portability in a Windows-only wrapper or ask on #1103; do not rewrite the script on your branch.

### Reporting

- **Validation results:** comment on #1103 with the exact commands, exit codes, seeds and log excerpts (`artifacts/checks/context-switch/*.log`). State what was run on Windows, and that it was automated and not a human playtest.
- **A Windows-only defect in files outside the list above:** fix it on `claude/1103-context-switch-windows` with a regression, write a handoff with `python tools/collab.py handoff --lane ui ...`, and open a PR **into `claude/1103-context-switch`** (or into `main` once #1103 has merged). Do not force-push or rebase the macOS branch.
- **A new crash unrelated to #1103:** open a separate issue and branch.
