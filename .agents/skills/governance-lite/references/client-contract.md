# Client contract

Review the specified diff and ledger read-only. Follow AGENTS.md and the requested scope; do not edit or publish.

Reconcile pending-choice kinds and answering commands; new/changed command/view fields; persistent state and backward loading; and changed meanings of existing payloads. Include decider specs, encoded TargetId/Choice values, CardIds, payment-source token IDs, action/event meanings and phase sequencing even when declarations do not change. The ledger is an index, not a completeness certificate.

For each item identify the authoritative Core/server producer, permitted viewers, submitted intention, and actual client handler or required follow-up. The client presents authoritative options/quotes; it must not recompute Gold or eligibility. A `#` payment token may require summing `Sources` rather than finding a physical card; a `spellId>hostId` target requires intentional selection handling. Keep engine-private state obligations separate from wire/view obligations.

Check `MatchEngine.ContentVersion` and any schema version: if existing ids, seeds, hashes or payload meanings changed, replays and journals (`MatchEngine.Replay`, server recovery, committed `*.replay.json` fixtures) must be versioned or deliberately dropped.

When a change populates or reinterprets an existing payload field for one producer, enumerate every Core producer of that type (for example each `new GoldQuote(` or `new ActionOption(` site, and each `chooseXxx` option builder) and report producers left behind; the ledger and the diff show only the producer that changed.

A client that reads a new or newly populated view field must tolerate its absence: older servers, stale views and replays omit it, so a null-forgiving `!` or an unguarded dereference of an additive field is a finding unless an invariant guarantees presence.

Review `tests/L5R.Client.Transport.Tests` and relevant client source. Transport checks prove payload integration, not graphical playability. Label native input/visual checks and human acceptance separately. A missing client file diff does not prove no client-lane work.

Return priority, file/line and triggering contract for defects, plus a concrete client-lane list including handled/outstanding status and evidence. Do not turn advisory review into an owner-approval requirement.
