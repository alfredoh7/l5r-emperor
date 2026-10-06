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

- [Playtest fixes (#644)](2026-10-05-playtest-fixes-644.md) — accepted: entry-reaction windows close when nothing is left to take, a face-up Celestial enters play at once, Steel on Steel needs a performer at the battlefield, the bot always uses its own entering-play Reactions.

- [Promotional-Emperor pass (#653)](2026-10-05-promotional-emperor-pass.md) — accepted as readings: 106 of 134 unwired Promotional-Emperor cards wired, 28 blocked with notes; the rules readings and open questions for Mendon are listed.

- [Rejected cards (#667)](2026-10-05-rejected-cards.md) — accepted as readings: Peace Treaty, Wildfire and Green Nightshade Blend are deliberately not wired (Mendon).

- [Fusako, Gaijin Sorcerer, Sleep (#667)](2026-10-05-667-duel-sleep.md) — accepted as readings: bow-to-refuse duel, weighted Shugenja count, budgeted multi-select.

- [Mempo and the Favor window (#667)](2026-10-05-667-favor.md) — accepted as readings: after-Favor-discard reaction window, +1 Favor use, any-unit Favor Battle.

- [Coin Pouch, Near Miss, Profits (#667)](2026-10-05-667-payments.md) — accepted as readings: extra payment sources in the Gold flow.

- [Blade, Yumi, Winter Sled, Wyrmbone, Lying in Ambush (#667)](2026-10-05-667-performers.md) — accepted as readings: hand-played Items, unnegatable movement, created Ambusher.

- [The Deciding Moment and phase order (#667)](2026-10-05-667-phase-order.md) — accepted as readings: per-turn phase-order marker, allowed swaps, header animation.

- [Jade Embrace, Sea & Sky, Through the Breach, Colonial Farm (#667)](2026-10-05-667-removal.md) — accepted as readings: per-card ability/trait removal that resets on leaving play.

- [Gozu, Hyobuko, Masaya, Rosary, Topaz Armor (#667)](2026-10-05-667-search-play.md) — accepted as readings: owed play after a search, Celestial into play, keyword pair.
- [Remaining unwired cards and the minimal primitive set (#668)](2026-10-05-remaining-unwired-primitives.md) — proposed: 491 unwired cards triaged against the end-of-arc supplement and the engine; 206 wirable today, 25 ranked primitives, 18 rulings, with a [per-card appendix](2026-10-05-remaining-unwired-primitives-cards.md) and [one implementation prompt per primitive](2026-10-05-remaining-unwired-primitives-prompts.md). No card wired.
