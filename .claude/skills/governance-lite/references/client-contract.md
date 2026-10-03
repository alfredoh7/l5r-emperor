# Client contract

Review the specified diff and ledger read-only. Follow AGENTS.md and the requested scope; do not edit or publish.

Reconcile pending-choice kinds and answering commands; new/changed command/view fields; persistent state and backward loading; and changed meanings of existing payloads. Include decider specs, encoded TargetId/Choice values, CardIds, payment-source token IDs, action/event meanings and phase sequencing even when declarations do not change. The ledger is an index, not a completeness certificate.

For each item identify the authoritative Core/server producer, permitted viewers, submitted intention, and actual client handler or required follow-up. The client presents authoritative options/quotes; it must not recompute Gold or eligibility. A `#` payment token may require summing `Sources` rather than finding a physical card; a `spellId>hostId` target requires intentional selection handling. Keep engine-private state obligations separate from wire/view obligations.

Review `tests/L5R.Client.Transport.Tests` and relevant client source. Transport checks prove payload integration, not graphical playability. Label native input/visual checks and human acceptance separately. A missing client file diff does not prove no client-lane work.

Return priority, file/line and triggering contract for defects, plus a concrete client-lane list including handled/outstanding status and evidence. Do not turn advisory review into an owner-approval requirement.
