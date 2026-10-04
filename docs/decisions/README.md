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

- [Personality-destruction Reaction triggers](2026-09-29-personality-destruction-reactions.md) — confirmed play-to-dead trigger, with narrower action-source wording and no later discard-to-dead trigger.

- [Rename the "Fallen" UI label to "Dead"](2026-09-30-dead-not-fallen.md) — proposed, for Alfredo: "Fallen" is a printed keyword later in the Emperor Arc and collides with the client's own word for the dead-personality pile.

- [Regret and Embracing Virtue](2026-10-03-regret-embracing-virtue.md) — accepted rulings R-026 to R-030; multi-attach from a discard pile, each card its own equip, the end-of-turn return, and the client's two-slot contract.

- [Pass all battles](2026-10-04-pass-all-battles.md) — accepted: a button that passes a player's actions across every remaining battle; the engine passes and selects battles for them until the opponent acts or a decision needs them.

- [Catalogue check-box filters](2026-10-04-catalogue-checkbox-filters.md) — accepted: every Card Library and Deck Builder filter is an include/exclude check-box group with Select all and Deselect all; the default shape for new filters.

- [Proclaim flow](2026-10-04-proclaim-flow.md) — accepted: the Proclaim window closes when nothing is left to react with; the first Personality bought each turn defaults to Proclaim, later ones to the Clan discount, a missed Honor Requirement to allegiance.
