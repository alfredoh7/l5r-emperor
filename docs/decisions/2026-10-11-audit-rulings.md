# Rulings from the single-action audit (#833)

Status: accepted. Lane: rules (Mendon). Issue: #838.

The #833 audit left five wired cards with a rules question each. Mendon's answers are indexed as R-063 to R-066 in `docs/rules/RULINGS.md`.

- **Rata** — flavor text; nothing to wire. **Soul's Sacrifice** — the performer. Both were already as implemented.
- **Mastering the Elements** — supersedes the "persists and accumulates" reading of `2026-09-21-second-city-primitives.md`. The copy is lost when the card leaves play (any exit); it starts clean each time it enters play. `CardInstance.CopiedSpellRules` marks a copy owed; `SweepSpellCopies` clears `Printed` for a marked card out of play, before and after every command. Sly Deceiver's copy is unchanged (not covered by the ruling).
- **The Bestial Ones** — Engage is battlefield-specific: trigger `after-engaging-at-own-battlefield`, plus a target-at-the-battlefield requirement. Other Engage Reactions with card targets were not changed here.
- **Fallow Lands** — implemented as ruled; the Region-presence rule is recorded as R-065.

Validation: `copy-kata-targeting` (loss on every exit, fresh copy on re-entry), `single-reaction-4` (Bestial Ones at home / target elsewhere), full `tools/check.sh --skip-client`.
