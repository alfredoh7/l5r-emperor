# PR ladder: adopting process and CI practices

A sequence of process and CI improvements, one PR per rung, merged in order. A rung starts only
when the one it depends on is on `main`. Sizes: S about a day, M a few days, L a week or more.

Tracked in the epic issue: [PR ladder: adopting process and CI practices (epic) — #509](https://github.com/alfredoh7/l5r-emperor/issues/509).

## Overview

| Rung | PR | Depends on | Size |
|---|---|---|---|
| 0 | Tracking epic, labels, rulings register | none | S |
| 1 | Stack guard in CI and `collab.py audit` | 0 | S |
| 2 | `.claude/` foundation, pitfalls and routed-concerns docs | 0 | S |
| 3 | Hooks and the comment-swallow lint | 2 | M |
| 4 | Registration refactor (4a data-driven waves, 4b engine hook registry) | 1, needs a freeze window | L |
| 5 | Repo skills | 2, better after 4 | M |
| 6 | Format and analyzer baseline | none | M |
| 7 | Nightly tier | none | M |
| 8 | Supply chain and security | none | S-M |
| 9 | Governance-lite | 2, 5 | M |

Rung 4 needs a quiet window away from a parallel card-wiring pass; the owner decides its timing
before that rung starts. Rungs 1-9 are tracked as checklist items on the epic issue only —
separate issues are opened one rung at a time, as each is taken up.

## Risks

- **Rung 4 vs. card passes.** A refactor touching shared registration files cannot overlap a
  parallel wiring pass; freeze card passes for it.
- **CI cost.** The self-hosted Mac runner is shared, so rungs 6 and 7 must not lengthen the PR
  path.
- **Enforcement.** Without branch protection, "no merge while nightly is broken" (rung 7) stays a
  convention, not an enforced gate.
- **Hook noise.** Rung 3's hooks warn at first and only block the generated-file case.
