# Compiled rules doc as canonical reference for card-engine work

Status: accepted (Mendon, rules lead).

Issues: [#47](https://github.com/alfredoh7/l5r-emperor/issues/47), [#54](https://github.com/alfredoh7/l5r-emperor/issues/54) (the compiled rules doc and its supplements); this decision governs how that doc gets used going forward, not a single implementation PR.

## Decision

`L5R.Core`'s card-ability engine stays a hand-coded, data-driven catalog (`AbilityDefinition`/`AbilityCatalog` in `src/L5R.Core/CardAbilities.cs`, one entry per distinct card mechanic, generalized into shared shapes like `Performer` when a pattern recurs across cards) rather than becoming a general natural-language interpreter of card text. A fully general interpreter that could correctly derive an arbitrary new card's rules behavior from its printed wording alone was considered and rejected as impractical — the wording is deliberately terse and leans on external rules knowledge (keyword definitions, universal player abilities, timing structure) that isn't recoverable from the card text in isolation.

What changes going forward: every new card mechanic implemented in `L5R.Core` should be checked against the canonical rule it invokes in `docs/rules/EMPEROR_EDITION_CORE_RULES.md` (Ranged/Melee Attack, Duel, Dishonor, Experienced/overlay, the relevant Supplement section, etc.) instead of being re-derived from the rulebook PDF or from first principles each time. The compiled doc is the shared source of truth for rules text; `docs/rules/LION_CRANE_COVERAGE.md` remains the per-card index of what's actually implemented and where.

## Reason

The per-card catalog approach is what's already been used successfully through the Vanilla and Stronghold waves this session, including the `Performer` generalization — it scales linearly with implementation effort but stays precise and testable per card. The alternative (built-in rules-text parsing) doesn't reduce that effort for genuinely novel mechanics and risks silently misinterpreting a card, which conflicts with this project's content-provenance discipline (every card's behavior must trace to a verified source, never inferred). Grounding each new implementation in the compiled rules doc gets the main benefit that was actually wanted — not re-deriving well-established rules (e.g. what "Ranged Attack" means) from scratch on every card — without pretending the engine can reason about arbitrary text.

## Scope

Applies to all future `L5R.Core` ability/mechanic work. Does not change anything about the existing implemented cards. Does not cover the in-game rules-viewer UI (menu-accessible rules text) — that's tracked separately and left to the UI lead's design judgment.
