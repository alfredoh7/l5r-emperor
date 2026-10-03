## Change

Explain the problem and resulting behavior. Link the issue and `docs/handoffs/tasks/` record.

## Lane and coordination

UI / rules / shared. Explain any semantic effect on the other lane, even if the changed paths look isolated. Follow the review policy in AGENTS.md and .github/collaboration.json: Mendon has standing authority across all game areas and may merge his PR after relevant tests/CI without Alfredo approval; other authors request the other lead for shared changes. GitHub applies path-based lane labels automatically.

## Validation

List actual commands/results and human/visual evidence where relevant. Do not claim Windows gameplay from CI or expert rules acceptance from automated tests.

## Client-lane items

- [ ] Reviewed the contract ledger and semantic changes; listed client-lane items below, or explained why none apply.

For affected choices, commands, view fields, payment/target payloads and phase/event meanings: identify Core/server authority, permitted viewers, existing client handling or follow-up. Record persistence/replay state obligations separately. `python tools/collab.py ledger --base origin/main --format markdown` lists candidates; add `--working` for local edits. An empty ledger alone does not prove no client impact. Governance-lite supplies four focused review roles; its results are advisory evidence under AGENTS.md.

## Remaining work

State any unresolved decisions or limitations, or explicitly say none. The agent refreshes current main and runs the collaboration audit before asking to merge.
