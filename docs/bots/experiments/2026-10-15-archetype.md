# 2026-10-15 archetype (pre-registration, S1-S4)

- Issue / branch / arms: #963, `claude/963-archetype-s0` (the S0 classifier, `DeckArchetype`, is on this branch). Arms `arch-attack` (S1), `arch-abilities` (S2), `arch-military` (S3), `arch-switch` (S4), `arch-all` (the four together). Each is a default-off `BotContext` flag; the tournament hands each bot the archetype of its own decklist (`BotContext.Archetype`), the same way the caller can supply the deck's average Personal Honor.
- Champion: `main` plus the S0 classifier (no behaviour change), all flags off.
- **Hypothesis.** The deck's own cards say how it wins (docs/bots/phase1/archetype-s0.md). A bot that plays to that route wins more than one that treats every deck alike.
  - S1: a dishonor-route deck (Crab Control) declares an attack only with at least 4 more Force than the defenders; it does not need marginal attacks, its engine wins the game. Expected: the Crab deck gains and keeps its Personalities.
  - S2: abilities that are the deck's engine score +30: Honor gain in an honor-primary deck (Unicorn Maidens), Honor taken from the opponent in a dishonor-primary deck (Crab Control).
  - S3: a deck with no honor or dishonor engine (Dragon, Mantis, Scorpion, Spider, Lion preset, Crane preset) does not get the +25 bonus for Honor-gain abilities; it spends its actions on the battlefield.
  - S4: a deck with an honor route chooses "gain Honor" over "the opponent loses Honor" once its own Honor is 28 or more; a dishonor-primary deck presses the opponent unless its own Honor is below 0 or the opponent is already at -8.
- Each change reads only the bot's own decklist and public Honor totals; no card or deck is named. The classifier decides which decks it touches, so most decks are untouched by any one flag: pooled effects are diluted by design, so the affected-deck subset is the primary read.
- **Decisive test (per arm).** Mode `dev`, pairing `same`, seeds 66-77 (a fresh block, shared by the five arms because the champion is deterministic) = 864 pairs, 1,728 games per arm. A family of five arms: **one-sided sign test on pairs won-both vs lost-both at p < 0.01** (Bonferroni of .05 over five). Secondary, reported for every arm: win rate; the same test restricted to games where the challenger pilots a deck the flag touches (S1 and S2-dishonor: Crab Control; S2-honor: Unicorn Maidens; S3: the military-only decks; S4: the decks with an honor route or a dishonor primary); zero bot stalls; no touched deck below 35% where the champion is at 50% or more; provinces-left and Personalities-in-play margins not negative.
- **Confirmation (only for an arm that passes dev):** mode `holdout`, pairing `same`, seeds 63-72 (held-out decks, fresh block), one-sided p < 0.10, win rate >= 50%, zero bot stalls.
- **Decision rule:** a promote-candidate only if dev and confirmation both pass. Nothing is promoted without Mendon's decision; a pass is reported with the evidence. Fixed samples, no peeking, no extension. Inconclusive is reported as inconclusive.

## Result (fill in after the single run; the plan above is not edited)

- Command: `L5R_TOURN_CHAL=<arm> L5R_TOURN_PAIRING=same L5R_TOURN_MODE=dev L5R_TOURN_SEED_START=66 L5R_TOURN_SEEDS=12` for each arm, from snapshot `artifacts/bots/snap-arch` (1,728 games per arm, 18-21 minutes each, run locally; GitHub Actions unavailable).
- Reports: `artifacts/bots/s-<arm>-dev.{md,json}` (not committed).

| Arm | Challenger - champion | Pairs won both / lost both | One-sided p (won-both >= observed) | Bot stalls |
| --- | --- | --- | --- | --- |
| arch-attack (S1) | 863 - 865 (49.9%) | 4 / 5 | .75 | 0 |
| arch-abilities (S2) | 861 - 867 (49.8%) | 6 / 9 | .85 | 0 |
| arch-military (S3) | 864 - 864 (50.0%) | 0 / 0 | 1.0 | 0 |
| arch-switch (S4) | 860 - 868 (49.8%) | 0 / 4 | 1.0 | 0 |
| arch-all (S1-S4) | 858 - 870 (49.7%) | 8 / 14 | .93 | 0 |

- **Outcome: no effect for any arm; none passes, no confirmation run.** The flags stay off.
- The finding that matters: the flags barely change any game. S3 changed no game result at all (0 of 864 pairs differ); S1 changed 9 pairs, S4 4, S2 15, the four together 22. The honor-gain bonus, the attack margin and the Honor-choice switch almost never decide a game in these decks. The deck's route is not set by these decisions; it is set by what the deck draws and recruits and how its Personalities are used in battle.
- The per-deck table was not informative: at most 22 pairs differed, so no deck's rate moved beyond noise.
