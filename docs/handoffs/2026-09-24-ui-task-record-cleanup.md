# UI task-record cleanup — 24 September 2026

Issue [#461](https://github.com/alfredoh7/l5r-emperor/issues/461). Alfredo authorized this documentation/administrative pass while preserving Mendon's workflow and open tasks. This is a dated audit, not a new approval policy or a game update.

## Current review map

| Record | Verified state and meaning |
|---|---|
| [#268](https://github.com/alfredoh7/l5r-emperor/pull/268) | Merged 22 September as `86de986d931b2d16758c1945e2d45e463c8a20aa`; includes the older UI/duel transition baseline. |
| [#266](https://github.com/alfredoh7/l5r-emperor/pull/266) | Closed as superseded in this pass, not merged again. Branch `codex/205-table-crimson-transition` remains at `1f5a916a9e23cf01b9c7227145d08e7411b7d8dd`. |
| [#434](https://github.com/alfredoh7/l5r-emperor/pull/434) | Current unmerged UI review at `659e015f0e3562bd4b83f5fa445efed1d3fd2892`. Existing review requests and stacked base remain unchanged. |
| [#454](https://github.com/alfredoh7/l5r-emperor/pull/454) | Delivery completed at `ab284dbd230eb07b2deab5db1e058a8b861b4796`; PR remains open. It does not merge the game candidate or supply human acceptance. |

[Mendon's integration record](https://github.com/alfredoh7/l5r-emperor/pull/434#issuecomment-5783680384) explicitly confirms #266 landed in #268 and identifies #434 as the single landing candidate for the newer stack. The already-superseded #270, #392, #393, #395, #396, #401, #403, #404, #410, #427, #428, #429, #430 and #432 remain closed; their history is retained. No PR targets #266's head branch at closure.

[Revision 2 Windows review](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr434-659e015f0e-r2-windows) contains the corrected Top Cards, ghost copies, multiple targets, battle progress and clipping revisions at the exact game source above. [Final delivery evidence](https://github.com/alfredoh7/l5r-emperor/pull/454#issuecomment-5803848934) records the successful package checks and [one verified notification](https://github.com/alfredoh7/l5r-emperor/pull/434#issuecomment-5803833083). There is no duplicate preview notification in this cleanup. The preparation handoff's publication-pending note is historical.

## Issue reconciliation

All 38 pre-existing open issues assigned to Alfredo were inspected. All remain open. Original issue bodies, checklists, scope agreements, labels, assignments and dependencies are preserved. Implementation evidence identifies where to review existing work; it does not manufacture missing human acceptance. This consolidated report avoids a batch of comments on individual tasks.

### Implemented work carried by the open UI review

The handoff links below point to the exact #434 source, so old standalone PR numbers in their historical sections do not imply another implementation is needed. Final combined review, not a new build of each feature, is the next action unless feedback identifies a defect.

| Issue | Implementation evidence | Remaining distinction |
|---|---|---|
| [#400 — Ghost-copy indicator](https://github.com/alfredoh7/l5r-emperor/issues/400) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-400-created-copy-seal-399c5eb9.json) | Created-copy seal; final human review remains. |
| [#377 — Opponent payment](https://github.com/alfredoh7/l5r-emperor/issues/377) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-377-opponent-payment-dae8fcc2.json) | Approved payment sidebar; expert review remains. |
| [#355 — Attack targeting](https://github.com/alfredoh7/l5r-emperor/issues/355) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-355-attack-targets-122077f3.json) | Straight brush pointer and sliding ranged/melee tab; combined review remains. |
| [#347 — Action-lock indicator](https://github.com/alfredoh7/l5r-emperor/issues/347) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-347-action-lock-94a9ed03.json) | Restriction seal and engine-disabled reasons; review remains. |
| [#346 — Movement destinations](https://github.com/alfredoh7/l5r-emperor/issues/346) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-346-movement-picker-5d15a2d0.json) | Engine-owned mover choices and table arrows; review remains. |
| [#345 — Multiple targets](https://github.com/alfredoh7/l5r-emperor/issues/345) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-345-multi-targets-e1d549fc.json) | Approved groups/board links; fresh native reruns delivered in #454. |
| [#343 — Either/Or](https://github.com/alfredoh7/l5r-emperor/issues/343) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-343-either-or-635092db.json) | Consequences/payment chooser; review remains. |
| [#342 — Copy wizard](https://github.com/alfredoh7/l5r-emperor/issues/342) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-342-copy-wizard-779992f0.json) | Copy tray and copied-effect details; review remains. |
| [#339 — Generic choice prompt](https://github.com/alfredoh7/l5r-emperor/issues/339) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-339-decision-chooser-7a41ccd4.json) | Prepared choice interface; review remains. |
| [#323 — Top Cards](https://github.com/alfredoh7/l5r-emperor/issues/323) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-323-review-revisions-7eb73ffa.json) | Corrected A trays + B deck-order labels approved at 58456f7; review remains. |
| [#315 — Shown cards](https://github.com/alfredoh7/l5r-emperor/issues/315) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-315-shown-card-history-c1becd4c.json) | Public shown-card history and inspection; review remains. |
| [#314 — Deck search](https://github.com/alfredoh7/l5r-emperor/issues/314) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-314-deck-search-34506041.json) | Approved immediate deck-button navigation; review remains. |
| [#311 — Battle progress](https://github.com/alfredoh7/l5r-emperor/issues/311) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-311-battle-progress-568a7a90.json) | Approved strip/expanded sequence; fresh native reruns delivered in #454. |
| [#291 — Regions and Celestials](https://github.com/alfredoh7/l5r-emperor/issues/291) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-291-regions-celestials-db3fcac9.json) | Approved paired Regions, Celestial frame and inspection; combined review remains. |
| [#274 — Stabilization](https://github.com/alfredoh7/l5r-emperor/issues/274) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-274-stabilization-0e03b604.json) | Scoped fixes carried by the review stack; do not imply every inherited finding was fixed. |
| [#213 — Permanent grants](https://github.com/alfredoh7/l5r-emperor/issues/213) | [Recorded implementation](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/tasks/codex-213-permanent-grants-f296da23.json) | Original seal landed with #268; granted-ability integration is in #434 and still needs review. |

For #213, the newer integration is additionally verified in [the attack-target handoff](https://github.com/alfredoh7/l5r-emperor/blob/659e015f0e3562bd4b83f5fa445efed1d3fd2892/docs/handoffs/2026-09-21-attack-targets-review.md) and `CardChanges.cs`: granted abilities retain source, duration, remaining uses and copied metadata. The original permanent-grant seal alone is not evidence for that expanded request.

### Older baselines with unresolved acceptance records

These are not instructions to repeat development. Their historical PRs/handoffs, current implementation and issue scope must be reconciled with recorded human acceptance before closure. The board was unavailable to this session because no token is configured; no claim, stage, dependency or acceptance was changed.

| Issue | Disposition — retained open |
|---|---|
| [#247 — UI consolidation](https://github.com/alfredoh7/l5r-emperor/issues/247) | Earlier consolidation landed through #268; newer work uses #434. Verify the umbrella acceptance record before closing. |
| [#245 — Home and Favor](https://github.com/alfredoh7/l5r-emperor/issues/245) | Approved baseline included in #268; retain the original inspection/shared-view acceptance scope. |
| [#243 — Clan placard / hand limit](https://github.com/alfredoh7/l5r-emperor/issues/243) | Baseline included in #268. No new human acceptance or hand-limit-discard fix is established by this audit. |
| [#233 — Fortress hall](https://github.com/alfredoh7/l5r-emperor/issues/233) | Baseline exists; latest issue milestone records M7 awaiting review. Native Windows/sustained profiling cannot be inferred. |
| [#221 — DeadCount / Fallen](https://github.com/alfredoh7/l5r-emperor/issues/221) | Baseline included in #268. Preserve scope-specific acceptance; this cleanup changes no public fields. |
| [#205 — Duel art / integration umbrella](https://github.com/alfredoh7/l5r-emperor/issues/205) | Live transition landed in #268, but #448 remains active and native Windows/full-match acceptance is separate. |
| [#188 — Removed piles](https://github.com/alfredoh7/l5r-emperor/issues/188) | Presentation baseline exists; compare original contextual-pile/gallery criteria with acceptance before closing. |
| [#168 — Lacquer menus and tooltips](https://github.com/alfredoh7/l5r-emperor/issues/168) | Integrated baseline exists; original scope and final acceptance require reconciliation. |
| [#164 — Lacquer sidebar](https://github.com/alfredoh7/l5r-emperor/issues/164) | Integrated baseline exists; original scope and final acceptance require reconciliation. |
| [#120 — Catalogue viewer](https://github.com/alfredoh7/l5r-emperor/issues/120) | Viewer and scan infrastructure exist; catalogue membership still does not mean playable support. Reconcile viewer acceptance. |
| [#81 — Token artwork and rails](https://github.com/alfredoh7/l5r-emperor/issues/81) | CardTokenRail and the approved rail handoff exist. Check original mixed-type/large-count/host coverage before closing the broad issue. |
| [#69 — Custom Practice fixtures](https://github.com/alfredoh7/l5r-emperor/issues/69) | Implementation landed with #262 inside #268; the board scope retains a distinct human acceptance step. |
| [#37 — Local name and priority clarity](https://github.com/alfredoh7/l5r-emperor/issues/37) | Implementation is recorded, but board guidance explicitly requires acceptance reconciliation rather than duplicated implementation. |
| [#34 — Tabletop HUD and piles](https://github.com/alfredoh7/l5r-emperor/issues/34) | Implementation is recorded, but board guidance explicitly requires acceptance reconciliation. |
| [#21 — Dishonorable status / Fallen](https://github.com/alfredoh7/l5r-emperor/issues/21) | Baseline included in #268 via #261; retain original orientation/gallery acceptance scope. |
| [#18 — Current versus printed statistics](https://github.com/alfredoh7/l5r-emperor/issues/18) | Implementation is recorded, but board guidance explicitly requires acceptance reconciliation. |
| [#11 — Active/priority cues](https://github.com/alfredoh7/l5r-emperor/issues/11) | Earlier cues exist; later battle-stage work is #66/#311/#434. Preserve the original sequencing dependencies and acceptance. |

The earlier baseline and integration evidence remains in [UI_CONSOLIDATION.md](../UI_CONSOLIDATION.md), [DUEL_MATCH_INTEGRATION.md](../DUEL_MATCH_INTEGRATION.md), [the token-rail handoff](tasks/codex-81-token-rails-fdc5e544.json), and the original issue discussions. [PROJECT_BOARD.md](../PROJECT_BOARD.md) explicitly separates implementation and acceptance. Dated handoffs were not rewritten to erase their earlier status.

### Delivery, remaining implementation and explicit acceptance work

| Issue | Disposition — retained open |
|---|---|
| [#297 — Windows review delivery](https://github.com/alfredoh7/l5r-emperor/issues/297) | Revision 2 was delivered and validated; #454 is still open. Packaging completion is separate from graphical/rules acceptance. |
| [#448 — Duel follow-ups](https://github.com/alfredoh7/l5r-emperor/issues/448) | Actual work: confirmed live-duel concession, paused Focus Effect choices/beats, and integrated native round trips. Leave the issue unchanged. |
| [#58 — Full in-game rules reference](https://github.com/alfredoh7/l5r-emperor/issues/58) | Actual work remains: a main-menu viewer sourced from the canonical rules document. Existing RulesHelp topics are not that viewer; no complete implementation was found in main or #434. |
| [#71 — Complete Lion/Crane matches](https://github.com/alfredoh7/l5r-emperor/issues/71) | Human acceptance remains explicitly pending; no full-match result is created by this cleanup. |
| [#66 — Battle-stage display](https://github.com/alfredoh7/l5r-emperor/issues/66) | Review #311/#434 implementation against this bounded board scope. Preserve #20/#15 dependencies and human acceptance; do not close from UI approval alone. |

For #58, the source audit found the concise `src/L5R.Client/RulesHelp.cs` help topics, but no client viewer/build-time source integration for `docs/rules/EMPEROR_EDITION_CORE_RULES.md` in main or the combined candidate. Do not close the full-reference request based on those topics.

## Scope and verification

- The cleanup edits only the current handoff/status summaries and adds this audit plus its task handoff. No game source, assets, workflow configuration, review-package bytes or historical handoffs change.
- GitHub mutation scope is the new cleanup issue/PR, the superseded #266 closure, and [a completion reference on our Windows delivery issue #297](https://github.com/alfredoh7/l5r-emperor/issues/297#issuecomment-5804084131). No issue is closed by this pass.
- Before/after comparison passed for all 24 open Mendon-assigned issues: titles, bodies, states, assignees, labels, milestones and even `updatedAt` are identical. All 38 original Alfredo issues remain open with their original bodies/titles. Existing #434/#454 metadata is identical, including heads, bases, draft flags, assignees and review requests. Only #266 changed from open to closed; its remote branch SHA was independently verified as unchanged.
- Existing branches and local worktrees are preserved. There is no merge, retarget, reassignment, board acceptance, targeted Mendon notification or alteration of his standing development authority.
- Validation for this documentation pass: live PR/issue/source evidence checked; all 38 issues occur exactly once in the inventory; 15 local Markdown links resolve; `git diff --check` and `collab.py audit --working` pass. Game tests and Windows graphical play are not rerun or claimed by this cleanup. The delivery evidence above belongs to the previous delivery task.

The cleanup itself is reviewable on its own task branch. Remaining game-review and acceptance work stays in the original issues and PRs; this audit does not move it into a new implementation task.
