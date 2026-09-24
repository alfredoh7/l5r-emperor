# Live duel follow-ups — review candidate

Issue [#448](https://github.com/alfredoh7/l5r-emperor/issues/448), PR [#466](https://github.com/alfredoh7/l5r-emperor/pull/466). Alfredo approved 1A for concession and the 2B/2A/2C combination for paused Focus Effect choices. This is an unmerged review candidate.

## Implemented behavior

- The live duel menu offers the engine's **Concede match** command. A centered confirmation says the opponent wins the entire match. **Keep playing** has initial keyboard focus; it and Escape dismiss without submitting. The final click uses the current engine command and normal session guards.
- A Focus Effect decision occupies the lower tray while the duelists remain visible. The source is separate from the legal candidate cards, which retain complete period scans and explicit inspection. A quiet outline marks selection; a separate action describes the result and destination. Decline exists only when offered by the engine. Simple effect options act directly.
- The other seat gets a waiting view; its screen cannot choose for the acting player. Manual seat changes retain the opaque handoff. Crowded choices scroll horizontally.
- The engine records each public choice outcome as a source-linked beat before continuing to the next effect. Consecutive pauses retain execution order; rejected/stale commands add no beat. Draw evidence excludes hidden card identities. The existing cinematic and aftermath recap consume this evidence.
- The practice opponent now consumes an offered Focus Effect command instead of stalling at a paused choice. Targeting, optionality and outcomes remain authoritative in Core.

## Prepared reviews

On this Mac run `python3 tools/create-duel-review-launchers.py`. Apps appear in `artifacts/duel-followups/Launchers`: Return a Card, Concede, Other Seat, Crowded, Attach, Required Straighten, Destroy Attachment, Honor or Draw, Consecutive Choices, and Reduced Motion. They build/run this checkout with isolated review profiles, never fetch or switch branches. Close and relaunch to reset. The normal Latest app, prepared Concede app, and prepared Return a Card app were launched and visually verified on this Mac; Return a Card stops at a playable choice without automated submission.

The portable Windows package is specified by `previews/duel-followups.json`. Its source commit, private release, checksum and delivery validation are recorded in `previews/published/duel-followups.json` once published. Start Review enters a prepared real decision; Other previews contains each remaining case. Full Game opens the same build's normal menus. No external runtime or source checkout is needed.

## Appearance and evidence

[Approved/native comparison](screenshots/duel-followups/comparison.html) includes complete views and matching-width frames. [Native input evidence](screenshots/duel-followups/native-checks.json) records the checks for 13 scenarios.

Dedicated ImageGen derivatives use the approved navy/black surfaces and gold frame ornaments, with fixed-proportion corners. Prompts and source paths are in `assets/ui/duel-followups/production-prompts.json`. Live typography, period card scans and the existing duel button skin remain native. This preserves the existing upright duelist camera rather than reproducing the concepts' flatter camera. It is not a pixel-identical full-screen recreation. The selected action says DISCARD → HAND so the cue cannot appear to originate at another candidate.

## Validation and review boundaries

The client compiles without warnings. `bash tools/check.sh --skip-client` passed, including four new public-evidence/replay/privacy regressions and 95 AI tests (21 exercise the seven new prepared positions on three seeds), eight server and twelve transport checks. All thirteen follow-up native scenarios passed. Final layout reruns cover each choice kind, consecutive decisions, scroll-position retention, the last crowded candidate inspector, and bounds for longer explanations. The existing live-match suite passed 14/14 variants; sandbox/guidance passed 5/5 (including its full stage matrix); transition checks passed 5/5. The 1120 × 700 rerun is recorded with the native evidence.

The prepared duel transition relies on a graphical renderer. A diagnostic headless scenario reached its decision but emitted dummy-renderer image/shader errors, so it is not counted as a graphical launch check. Use the native launchers for playtesting; ordinary packaged-menu headless CI remains separate.

The additional legacy `check-crimson-duel.sh` static-art probe stops at a 0.50006104 px projection mismatch against its 0.5 px threshold. The exact failure was reproduced on unchanged main `de672f58` in a separate checkout; no relevant projection/rendering file differs. This probe is not counted as passed, and its later cases were not run. The live match, sandbox and transition suites above passed.

Windows graphical play, expert rules acceptance and final human implementation review remain open. A passing export, checksum or Windows launcher/runtime CI does not satisfy those human gates. Do not merge automatically. Other open UI preview PRs are separate from this current-main-based build.

## Concurrent work

Checked current main `de672f58` and open PR #465 (Embers of War). The Core choice-resolution integration adds only evidence capture before/after an existing decision; it preserves eligibility and resolution. Public `focusEffectRevenge` is recognized for #465 compatibility. That PR's mechanics and the interaction fixes in #464 remain separate. Recheck current heads before merging.

## Button corner review correction

Alfredo requested transparent cut-away corners throughout the UI. Shared lacquer/gold buttons, tabs and duel controls now extract the approved artwork without the opaque exterior matte; Practice button crops receive the same treatment. Keyboard focus follows the actual frame. See [native before/after and verification](screenshots/button-corners/README.md). This revision supersedes the earlier button appearance; Windows revision 3 predates this correction.
