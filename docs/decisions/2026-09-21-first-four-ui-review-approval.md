# First four UI previews — Alfredo acceptance and Mendon review

Status: Alfredo accepted the UI of previews 1–4 on 2026-09-21. Mendon review is pending. Nothing has been merged by this task.

## Decision

Alfredo approved (1) generic decisions / alternative costs, (2) shown-card history, (3) action restrictions and (4) deck search. For #4, pressing either large Dynasty/Fate button must open that deck's private card gallery immediately; remove the separate deck confirmation. Selecting a card and the later game decisions retain explicit confirmation.

Alfredo explicitly requested that these four demos be sent to Mendon. Merging them into the main game is contingent on Mendon approving them and the relevant checks passing. Ask Mendon to record any UI/QoL feedback so Alfredo can make the changes. This is conditional approval for these four tasks, not permission to merge now. Previews 5–8 remain awaiting Alfredo's review.

This decision supersedes the earlier blanket no-merge wording for previews 1–4 in the eight-preview plan and individual handoffs. The earlier evidence remains historical. It does not approve unrelated inherited changes: the preview base includes the preceding UI lineage and separately unapproved stabilization rules candidates. Integration must identify and resolve those dependencies with Mendon rather than interpreting this UI acceptance as rules acceptance.

## Review scope

| Preview | Issue | PR |
|---|---|---|
| 1. Generic choices / alternative costs | #339 | #392 |
| 2. Shown-card history | #315 | #393 |
| 3. Action restrictions | #347 | #395 |
| 4. Private deck search | #314 | #396 |

PR #396 supplies the small existing AlternativeCost wire forwarding dependency needed for #1 without importing the unapproved #5–8 UI. Core still owns costs and legality. Mendon should assess that transport change alongside the shared metadata in #392. All four PRs form a review stack; preview launchers run an isolated build and cannot merge anything.

Evidence is Alfredo's explicit request in the project task, recorded by his UI agent. This is not a claimed GitHub review by Alfredo or Mendon, native Windows playtest, or rules certification.
