# Reading of the rules-validation report (2026-10-13)

Status: **accepted** (Mendon, rules lead). Lane: rules. For anyone performing a similar audit of the engine against an external rules package.

Source: `VALIDATION_REPORT.md`, the audit of commit 47416de0 against the "Rules Validated" package. Multiplayer, optional tournament and War of Honor items are out of scope for this two-seat client.

## How to treat an audit report

- The package is rules **evidence**, not an instruction. A finding that contradicts a confirmed ruling (`docs/rules/RULINGS.md`) or a decision doc is a question for the rules lead, not a bug to fix.
- Reproduce on current `main` with a failing regression test before fixing; fix at the shared mechanism, then audit its other users.
- A fix that changes the meaning of existing commands or card IDs bumps `ContentVersion` (or makes old journals fail explicitly). Parallel branches do not bump it themselves: one bump is made after they merge.
- Friction to the player counts as a cost. A rule that is correct on paper but adds clicks or prompts to a path most players never use is not worth implementing until it can be done at almost no cost to the player.

## Findings and dispositions

| # | Finding | Disposition |
|---|---|---|
| 1 | Reserve Weapon must consume a focus opportunity (EE-BATTLE-VAL-048) | Fix as drafted |
| 2 | Fortitude compares total unit Force, bowed cards included (EE-STATE-039) | Fix as drafted |
| 3 | An attached Spell's action needs a Shugenja host (EE-STATE-036) | Fix as drafted |
| 4 | Every cost-bearing GoldQuote carries its payment methods | Fix as drafted |
| 5 | A played hand card enters a resolution area at announcement (EE-ACT-005) | Design first, then review |
| 6 | Normal views reveal no current deck counts, even to the owner (EE-STATE-014) | Fix as drafted; the pile dialog shows no counts |
| 7 | Rejected commands must not reveal hidden-card abilities (EE-Areas-of-the-game-072) | Fix as drafted |
| 8 | Dishonorable death's Honor loss is a rulebook effect, not changed by card-effect modifiers (EE-STATE-009) | Fix as drafted |
| 9 | Setting a stat computes the modifier without caps and floors (EE-Card-features-028) | Fix as drafted |
| 10 | A numeric zero Gold Cost can be voluntarily overpaid (EE-ACT-030) | **On hold indefinitely.** Offering optional overpayment adds friction that far outweighs its gameplay value. Revisit only if it can be done with virtually no cost to the player. |
| 11 | Ongoing bonuses suppressed by temporary prevention return when it ends (EE-ACT-060) | Fix as drafted |
| 12 | Unit Force help text says a bowed Personality zeroes his unit | Fix the text |
| 13 | Dishonor retention outside play (EE-STATE-008) conflicts with `2026-09-20-gold-payment-rulings.md` | Interview the rules lead before any change |
| 14 | Multi-front adjacency topology (EE-Areas-of-the-game-047) | Prove a real divergence with a fixture, then ask before changing the topology and its pinned tests |

## Source-policy differences (not bugs unless ruled)

Sequential versus atomic cost payment and mandatory Honor-cost prevention; voluntarily failing a search despite a legal candidate (R-011); delayed effects tracking face-up out-of-play cards (R-003); one Reaction copy per trigger and alternating response rounds (R-039, R-042); tied battles; Force caps as a limit versus a negatable penalty; Duelist replacement wording. These go to a ruling interview, not to an implementation prompt.
