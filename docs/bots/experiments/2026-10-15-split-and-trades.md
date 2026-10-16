# 2026-10-15 split defence (A) and military trades (B) (pre-registration)

- Issue / branch / arms: #963, `claude/963-archetype-s0`; arms `split-defense` (`UseSplitDefense`, needs `UseInformedDefense`, which is on in the champion) and `military-trades` (`UseMilitaryTrades`). Both default off. Champion: the same binary with both flags off.
- **A. Hypothesis.** The informed defence sends every defender to one battlefield. When attackers sit at two or more of the bot's Provinces and the defenders can beat or hold more than one of them, splitting them gets more done: an attacking army destroyed, a Province saved. Change: with the flag on, an exhaustive search over assignments of defenders to the contested battlefields (or home), valued +3 for an army destroyed where the Province would have fallen, +1 where it would have stood, +2 (less .25 per defender) for a Province saved by a lost battle, -.5 per defender at a battle that is lost without saving anything; used only when the best assignment uses two or more battlefields. Reads only the public view. Expected: +1 point, in the games where the opponent attacks at two Provinces.
- **B. Hypothesis.** The pooled test of Mendon's trade attacks (an attack that cannot raze a Province but destroys the defending army, or trades well; `UseTradeAttacks`, off) found it lost more than it won (docs/parked). A trade costs a deck its units; that is wrong for a deck with an honor or dishonor engine but is what a deck whose only route is the battlefield does to win. Change: trade attacks on for decks the classifier calls military with no honor secondary (Dragon, Mantis, Scorpion, Spider, the Lion and Crane presets); everyone else unchanged. Expected: +1 to +2 points, or negative; this is the hypothesis most likely to fail.
- **Decisive test (per arm).** Mode `dev`, pairing `same`, seeds 78-89 (fresh block, shared by the two arms because the champion is deterministic) = 864 pairs, 1,728 games per arm. Two arms: **one-sided sign test on pairs won-both vs lost-both at p < 0.025.** Secondary, reported for every arm: win rate; zero bot stalls; no deck (>= 40 decided games piloted) below 35% where the champion is at 50% or more; for B, the same test restricted to games where the challenger pilots a military-only deck; provinces-left and Personalities-in-play margins not negative.
- **Confirmation (only for an arm that passes dev):** mode `holdout`, pairing `same`, seeds 73-82, one-sided p < 0.10, win rate >= 50%, zero bot stalls.
- **Decision rule:** a promote-candidate only if dev and confirmation both pass; nothing is promoted without Mendon's decision. Fixed samples, no extension.

## Result (fill in after the single run; the plan above is not edited)

- Command: `L5R_TOURN_CHAL=<arm> L5R_TOURN_PAIRING=same L5R_TOURN_MODE=dev L5R_TOURN_SEED_START=78 L5R_TOURN_SEEDS=12`, snapshot `artifacts/bots/snap-ab`, 1,728 games per arm (about 21 minutes), run locally.

| Arm | Challenger - champion | Pairs won both / lost both | One-sided p | Bot stalls |
| --- | --- | --- | --- | --- |
| split-defense (A) | 863 - 865 (49.9%) | 3 / 4 | .77 | 0 |
| military-trades (B) | 865 - 863 (50.0%) | 10 / 9 | .50 | 0 |

- **Outcome: no effect for either; neither passes, no confirmation run.** Flags stay off.
- Split defence changed only 7 of 864 pairs: the situation it targets (attackers at two or more Provinces where two groups can each win) is rare, and the single-battlefield rule it replaces is already reasonable there. Military trades changed 19 pairs, evenly won and lost, so trading is neither the lever nor a cost for military-only decks.
