# Table to Crimson — first native milestone

Issue #205 · draft PR #266 · UI owner alfredoh7.

Open **Review Duel Transition.command**, then **Preview duel transition**. After the entrance the real DuelScreen is interactive; the demo opponent uses only the existing permitted-action chooser. Replay returns to the same prepared encounter. This isolated preview does not replace the regular game launcher.

## What is built

- Challenge identification over the actual table, using public participant names.
- Continuous full-card/attachment lift from their projected positions, a shallow arc and subtle settling tilt, paper edges and shadows.
- A real room camera move. The table projection and the 3D room share the changing pitch; normal-game defaults are unchanged.
- A diagonal ink-edge reveal into the approved Crimson environment, followed by the real hand/decision controls. DUEL is the existing generated wordmark.
- An offscreen prepared duel avoids showing loading frames. The normal demo introduction is suppressed only for this host. Endpoint quads come from the actual playable cards.
- Space/Escape or Skip finishes presentation only; reduced motion uses a short dissolve. Resize during the entrance settles to the real responsive screen.

## Scope and dependencies

The branch started at current main `28654c5`, includes the approved UI stack through `f461b25` (#263), and pins the separate Duel Demo at `83231b1` (#220). Both dependency branches are unmerged. Additive integration preserves hand-limit/granted-effect views, custom-deck replay fields, duel privacy/retained records and both test sets. No new rules behavior is introduced by the transition files.

The host is explicitly a prepared local review encounter. Normal-match automatic routing, network interruption/reconnect reconciliation and return-to-table choreography are not activated. The separate ongoing Duel task and its working tree are untouched. Mendon's pending #220 mechanics review still applies.

## Evidence

See `screenshots/duel-transition/milestone-1/` and the local native movie. Run `bash tools/check-duel-transition.sh` for reduced-motion, Skip, compact, ultrawide and mid-entrance resize native paths. Each validates unchanged version/replay at handoff and a real pointer selection afterward; selection must not issue a game command. These captures are fixed-FPS and are not performance measurements. Combined dependency validation: `bash tools/check.sh --skip-client` passed **512 checks** (43 collaboration, 132 content, 6 Meshy tooling, 175 Core, 52 published, 9 deck format, 10 deck workshop, 13 custom Practice, 60 AI, 5 server, 7 transport). Final client compilation has zero warnings/errors.

The native frame recorder forces rendering for occluded macOS windows; otherwise an apparently passing input probe could save stale images. Preparation likewise finishes offscreen drawing explicitly. A frozen cover stays above viewport preparation. Native inspection caught and corrected source-unit duplicates after deferred table layout, and the final capture waits for the opponent's focus flight to settle before showing the decision prompt.

An existing general camera probe failed at its top-left “empty cloth” click on both the pre-transition integration and this implementation. That point now lies under the approved Fallen HUD. The probe now clicks the top-center cloth instead; it continues to verify actual panning and unchanged game state. This adjustment does not alter normal game input handling.

The review gallery includes approved 2B/3C and native images side by side. The ink mask reuses the dry-brush alpha of `Art/DuelUi/red-slash.png`, recolored in the shader; the earlier generated wordmark, real card scans and Meshy room/Crimson assets are reused. No new 3D props were created.

The approved images remain the art-direction target. Native cards intentionally retain original scans. This review milestone demonstrates motion and exact endpoints; it does not assert pixel-perfect equivalence or certify AAA quality, and native Windows testing is still separate.

## Final native review result

The 1920×1080 full entrance, reduced motion, Skip, 1440×900, 2560×1080, and resize during the entrance all passed. Each preserved match version/replay across presentation and accepted native hand-card selection afterward. The actual launcher passed its offline artwork preflight and native Skip run. The normal table camera/input probe also passed after the probe correction above. Transition room/card projection agreed within 0.000220 pixels over 7,025 visible samples.

`validation.json` records these results. The local movie is an 8.23-second, silent 1920×1080/60fps native capture (6.65 MB), including the 4.5-second entrance and the live decision state. It is deliberately excluded from Git; final stills and validation are committed.
