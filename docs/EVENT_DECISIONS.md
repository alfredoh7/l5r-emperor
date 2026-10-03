# Event decision presentation

Related issue: #259. Approved combination: Harsh Choices A1 sidebar + Times of Strife B2 hand-adjacent control bar.

## Behavior

- Harsh Choices shows the revealed card and its actual province. Keep and Destroy submit the engine-offered commands directly. Final-province destruction is explicitly labelled defeat and cannot continue revealing Events after the match has ended.
- Times of Strife highlights one selected local hand card without changing game state. Clear resets the draft. The named Discard button commits the exact legal command; no generic OK, Pass or Cancel bypass is added. Empty-hand queue entries are skipped by the engine.
- Overconfident's deferred optional draw exposes both Draw and Decline, so a queued offer after a discard cannot leave the player stuck.
- A new version or change of viewing seat clears the private selection. Opponents receive neither hand-card actions nor the local selection. Discards become visible in the public Fate discard pile only after the authoritative command succeeds.
- The practice opponent consumes these same offered commands. It conservatively keeps provinces, discards an offered lower-value hand card and accepts optional draws. This never chooses for the human.

## Architecture

`MatchQueries.cs` supplies commands and the final-province warning. `Main.EventDecisions.cs` presents permitted snapshots and submits those commands. `EventDecisionUI.cs` and `EventDecisionArt.cs` implement the accepted appearance. The hand selection uses the original `CardTile` face/hit target; the bar is excluded from table mouse routing.

The native fixture uses a diagnostic setup solely under `--capture-mode=published-events-*`, then asks the real engine to reveal Harsh Choices / announce the triggering Battle action. Subsequent clicks use ordinary GUI input and session command submission. It is fixture-driven integration evidence, not a claim of a complete human match.

## Verification

Run `bash tools/check.sh --skip-client` and `bash tools/check-event-decisions.sh`. Native results and reviewed screenshots live under `docs/screenshots/event-decisions/`. Native Windows play and expert rules acceptance remain separate gates.

2026-09-17: `bash tools/check.sh --skip-client` passed all 466 checks: 43 collaboration, 130 content, 180 Core, 52 published, 9 deck format, 10 deck workshop, 13 custom Practice, 19 AI, 5 server and 5 client transport. The client build passed with zero warnings/errors; Godot import passed. All eight native Event cases passed: Harsh keep/destroy, Times of Strife, 1280×800 discard, 1280×720 last province, both opponent views, and 1920×1080 motion enabled. These use native viewport mouse input through `_Input`/GUI, not emitted Pressed signals.

The initial delivery left two older inspection/camera probes unresolved. The [follow-up investigation](CAMERA_INSPECTION_INVESTIGATION.md) confirmed a stale camera target underneath the Fallen counter and an obsolete inspection-header height assertion. Focused hand/Holding selection, deselection, switching and Close worked with unchanged production input code. An expanded defense fixture also needed to preserve committed Infantry arrows when cancelling a Cavalry draft. Additional traces confirmed unrelated live scroll events entering the automated camera sequence, so these capture modes now isolate scripted mouse input. The original isolated missed Holding click was not reproduced; observing focus loss did not establish its cause. The final rerun passed all 13 native inspection/target/camera scenarios, with a clean client build and Godot import. Native Windows and a full human match remain unperformed.

The approved/native comparison is [here](screenshots/event-decisions/index.html). Review fixtures can be opened with `Review Harsh Choices.command` or `Review Times of Strife.command`; they create isolated diagnostic matches and retain the ordinary controls. `Play Latest.command` opens the normal game.

The combined UI/main checkout needs the updated `catalogue-art-webp-v7` private artwork pack (2,779 images, WebP quality 85). Its 379,558,804-byte upload was verified against SHA-256 `fac304155d07b225e02426f9d30a5b52c364d0cab3612778f8ecba7b0a0a1f70`. This prevents the stale publisher-pack error on fresh installs; no binaries or publisher cache are committed.

## Follow-up audit (outside this bounded approved UI pass)

Current main also lacks explicit query branches for `brashDraw`, `bowReaction`, and `beforeActionReaction`. Duel-related `duelReaction`/`duelWonReaction` are part of the separately paused duel integration. These must not be treated as implemented by this PR. Times of Strife’s Reserve-trigger interpretation remains separate rules work; this pass preserves the current engine trigger. This pass deliberately does not rewrite those effects or redesign the duel UI.
