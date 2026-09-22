# Duels in the running L5R match

Issue #205 · integration PR #268 · September 17, 2026.

An accepted challenge in a normal offline or private match now uses the approved table entrance, followed by the low-angle hush and both individual champion introductions. Acceptance and pre-duel reactions stay on the table. The same `DuelScreen` supplies focusing, private inspection, reactions, reveal, the retained verdict and consequences. Continue carries only surviving cards back to their actual current table positions. The match session, version, command history and engine priority are unchanged by either transition.

## Play and sandbox

- `Play Latest.command` / the generated `artifacts/L5R Emperor - Latest.app` runs this combined checkout with live routing enabled.
- **Play → Duel Demo** remains the permanent isolated sandbox: prepared positions, seed, solo/manual seats, restart and replay export. It instantiates the identical screen, so changes to layout, inspection, reveal and resolution immediately apply to both hosts. Its Continue returns to setup; the match host's Continue returns to the table. No separately maintained game or duplicated duel implementation exists.
- **Review Duel Integration.command** starts a prepared encounter on the main table. Click **Begin prepared duel** to exercise the actual live host without playing up to a challenge. This is explicitly a review fixture; normal launch never replaces a user's match with it.
- Offline duel Menu → Switch seat pauses the bot and uses an opaque private-card handoff. The online host retains the authenticated seat. Connection problems disable new commands and expose Reconnect in the same lacquer menu.

## Lifecycle and privacy

`Main.MatchDuel.cs` orchestrates the table's real projected geometry and the shared screen. `MatchDuelHost` owns only match-specific controls and passes legal commands to the existing `MatchSession`. `MatchDuelFlow` buffers each permitted snapshot, holds the completed result until Continue and queues a subsequent duel if the server moves ahead while a verdict is being read. It never queries an engine or synthesizes hidden information. A first connection ignores old completed history but resumes an unfinished duel.

A local-only session generation discards queued messages from a replaced practice/room. Reconnection to the same room keeps its identity. New view/seat scopes clear retained private snapshots. The server remains authoritative for actions, focus identities, totals, casualties and restored priority. Menu, inspection and animation pause the local opponent; online opponents remain independent.

Skip and reduced motion alter presentation only. A panned-away unit or late reconnect uses a quiet dissolve instead of inventing a card origin. Resize settles onto responsive live controls. Both backgrounds are prepared offscreen under a stable cover. The return removes outgoing card copies before rendering survivor movement, preventing double images. Already-presented casualties do not trigger a second generic battle-death animation. A concession during the duel presents the actual match outcome and returns to the finished table without inventing a duel winner.

## Dependencies and acceptance

The integration branch starts from main `9a0928d`, combines #220 at `e16593c` with #266 at `1f5a916` (including its approved UI dependencies), and integrates the newer main `eb0bfff` / #269. The complete v6 artwork pack includes that merge's two new scans; launcher coverage remains strict. Source task branches and unrelated working copies were preserved. The latest user request explicitly authorizes activation, superseding the older entrance-only decision to omit introductions and defer normal-match routing. This does not waive CI or Mendon's shared/mechanical review. Existing rule-review questions in `DUEL_MECHANICAL_REVIEW.md` remain separate; this pass introduces no rules interpretation or protocol schema change.

## Verification

Run `bash tools/check-duel-match.sh` for full native Mac round trips, compact/ultrawide layouts, reduced motion, Skip, resize, both seats, refusal, pre-duel reactions, immediate Strike, Action-phase return, aftermath and concession. The canonical path uses real card selection/placement, a covered seat handoff, private focused-card inspection, Strike and Continue. It checks the 8–7 result, destruction, restored priority, unchanged transition replay, normal table input and sandbox isolation.

`tests/L5R.Client.Transport.Tests` also opens two actual WebSocket clients against a temporary seeded private room, checks each seat's focus privacy, disconnects/reconnects, resolves 8–7 and continues the original battle. Queue tests cover immediate results, duplicate packets, historic results on reconnect, back-to-back duels and replacing a session.

Client and server must use this same combined source version for the retained duel records; this task does not deploy an internet server.

Native source captures and exact outcomes are recorded in `docs/screenshots/duel-integration/validation.json` after validation. Native Windows graphics and human cross-platform/private-match acceptance remain open. Rendered captures are visual evidence, not a performance benchmark or a claim of pixel/AAA equivalence.
