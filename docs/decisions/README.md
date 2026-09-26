# Shared decisions

Record durable decisions here when they affect future work: an agreed period printing, a rules interpretation, an approved UI interaction, or a shared engine/client contract. The agent writes the record so users do not repeat the same explanation in new sessions.

Use one Markdown file per decision, named `YYYY-MM-DD-topic.md`. Include: decision; reason and evidence; relevant lane/lead; affected files; issue/PR; and whether proposed, accepted, or superseded. A proposal or old chat assertion is not automatically accepted. Link superseding decisions instead of silently replacing historical evidence.

Current standing decisions are in `AI_HANDOFF.md` and the pinned rules coverage report. Current work belongs in issues/PRs and `docs/handoffs/tasks/`, not this index. Private auto-memory can be a convenience but never overrides the checked-in team record or the user's latest instruction.

Mendon's confirmed rulings, once he has stated them, are also indexed by number in
[`docs/rules/RULINGS.md`](../rules/RULINGS.md) — check there before re-deriving a rules
interpretation this register's decision docs already settled.

- [Duel verdict with retained evidence](2026-09-17-duel-verdict-evidence.md) — centered result, authoritative reason and continuous card transitions.

- [Inspect every own focused card](2026-09-17-own-focus-inspection.md) — direct private inspection after commitment, from every supported source.

- [Shared live-match duel and sandbox](2026-09-17-live-duel-integration.md) — one approved screen, separate sessions, full table-to-duel round trip.

- [Suspicions on the owner's Events Phase](2026-09-22-suspicions-owner-events-phase.md) — fires only on its owner's turns; an Event already face up resolves in the Events Phase.
