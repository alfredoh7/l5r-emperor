# Deck search review (#314)

## Scope and approval

Alfredo approved B’s full-card gallery inside A’s search panel. Implementation is in progress; no merge is authorized. Each task remains separately reviewable through a prepared Mac launcher.

Sources: parent checkout `artifacts/ui-eight-design-review-2026-09-20/images/04-deck-search-a.png` and `04-deck-search-b.png`. Reuse the approved lacquer frame, full period scans, and existing fonts.

This branch starts at current main 0d694f02 and includes the unmerged preview lineage through action restriction 5a8e9d6c. PR base is codex/347-action-lock to keep this task’s diff reviewable. Inherited shared rules candidates remain unapproved.

## Implementation contract

Use only the viewer’s PendingSearch and legal ActionOption commands. Grouped candidates and eligibility reasons belong to the engine; never obtain hidden instances or compute eligibility in the UI. Explicit selection/confirmation, engine-offered Pass, private list, deck choice, public reveal feedback, province/payment and hand-discard follow-ups.

## Validation / remaining

Not yet compiled or natively tested. Implementation, prepared launcher, native visual/input checks, privacy checks, source comparison and final review evidence remain. Project-board token unavailable; ownership recorded through GitHub.
