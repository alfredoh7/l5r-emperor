# Archived project-board guide

Retired on 2026-10-17. Historical instructions below are not an active workflow.
The original guide was at `docs/PROJECT_BOARD.md`; relative links in the preserved
text refer to that original directory.

---

# L5R Project Board

After the repository moved to `l5r-emperor-dev/l5r-emperor` (see [REPO_MOVED.md](REPO_MOVED.md)), the board's GitHub App installation, webhook target and any repository setting in the Netlify environment must name the new repository. On 2026-10-17 the production URL returned HTTP 503 and this could not be verified. The separate private application lives in [alfredoh7/l5r-board](https://github.com/alfredoh7/l5r-board). Production URL: https://l5r-project-board.netlify.app. The Netlify database and dedicated GitHub App are connected; Alfredo’s sign-in and webhook synchronization have been verified. Each person should verify their own sign-in and core workflow before adoption.

GitHub owns issue descriptions, assignees, PRs, checks, and code. The board owns workflow stage, executor claims, scope snapshots, dependency links, and human acceptance. Repository policy remains authoritative for review requirements. Mendon's cross-area authority remains unchanged.

## Connect an agent once

Sign in to the board with your own GitHub account, open **Agent access**, and create a device token. Store it outside the repository in your environment as `L5R_BOARD_TOKEN`. `L5R_BOARD_URL` defaults to the production URL. Never paste tokens into issues, handoffs, committed files, or agent output. Tokens expire after 90 days and can be revoked in the board.

`python3 tools/collab.py context` includes board context when configured. `python3 tools/board.py context` is a compact independent read. The board returns the authenticated human; a claimed executor name is not an identity credential.

## Start, resume, and finish

1. Read the issue, current scope, dependencies, claims, open PRs and current repository policy. A task must be Ready, synchronized, and assigned to your human before claiming.
2. Claim before implementation: `python3 tools/board.py claim --issue NUMBER --executor SESSION_NAME --branch BRANCH`. Use a unique session name. Resume only your same executor and branch using `resume` with those arguments. A rejected claim is not permission to continue.
3. Keep one In-progress task per person and one expansion task across active stages. An overlapping subsystem needs a recorded coordination decision; it does not restrict Mendon to a lane. Parent features contain bounded child tasks and are not claimed themselves.
4. Freeze scope at claim. New requirements become linked tasks, or the owner records a scope revision and reason. Old evidence does not prove a revised scope.
5. Link implementation with `link-pr --issue NUMBER --executor SESSION_NAME --number PR_NUMBER`. Record exact validation with `evidence --issue NUMBER --executor SESSION_NAME --kind KIND --summary TEXT --url HTTPS_URL --commit SHA --result passed|failed|"not run"`. Distinguish automated checks, native Mac/Windows, multiplayer, and full-match acceptance.
6. Continue the existing repository `collab.py handoff` and audit process. That command remains local-only and never unexpectedly publishes. After publishing the task record, explicitly run `board.py handoff --issue NUMBER --executor SESSION_NAME --remaining TEXT --stage Review` (or Ready to pause). It releases the executor and records the next action. Tokens cannot provide human acceptance.

A human accepts the bounded outcome in the board. A merged PR, successful build, or closed issue never automatically makes the board task Done. Required CI/reviews still control merges under repository policy; board human acceptance controls the product completion record.

## Interruption and outage

Stale claims remain owned until explicitly handed off; they never expire into another agent's work. If the board is unavailable, preserve already-claimed work and the ordinary local handoff. Starting unclaimed work requires an explicit claim/coordination record in GitHub after checking open work; never interpret failed authentication or a timeout as an empty board. Reconcile the board once connectivity returns. Do not switch/reset/stash unrelated work.

Blocked and Needs decision are flags on the actual stage. Cancellation and duplication are archive resolutions, not completed features. Packaging status remains separate from acceptance.

## Current task map

- #65 is the bounded six-phase UI follow-up of #10. The engine foundation remains separate.
- #66 is the battle-stage display follow-up of #11; it depends on the #20/#15 shared sequencing/reaction contract.
- #67/#68 split deck browse/edit and save/load under #25.
- #69/#70 split engine/Practice fixture setup and multiplayer negotiation under #29. The engine fixture task does not depend on the builder.
- #71 covers complete Lion/Crane matches; #72 covers native Windows; #73 covers external private Mac–Windows multiplayer. Their source is `docs/NEXT_PHASE.md`, and they contain no invented test results.
- #74 holds later tutorial specification outside the current commitment.

Existing implementations in #18/#34/#37/#41/#43/#45/#47/#54 require acceptance reconciliation, not duplicated implementation. #56 remains in Review with PR #57. #15/#20 and #21 still need their explicit scope/design decisions. Newly discovered work is imported as Backlog for deliberate classification; synchronization never silently claims another session’s work.
