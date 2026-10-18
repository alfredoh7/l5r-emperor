# 2026-10-18 Cavalry last-stand defence (pre-registration, non-inferiority)

- Issue / branch / arm: #1023, `claude/1023-bot-arena-laststand`, arm `pre-laststand` (`UseCavalryLastStand = false`).
- Champion: `main` at 16d35c31 (flag **on**, merged as #1008 and #1015). The change is already the champion, so the arm is the *old* bot and is the **challenger**; a challenger rate above 50% means the new behaviour is worse.
- Also in this build and not isolated: the Lion Legacy preloaded deck (#1022) joins the pool, so the dev pool is 10 decks (90 ordered pairs). The held-out three are unchanged.
- **Hypothesis (Mendon).** Defending with everything when the attacker's Cavalry could destroy every remaining Province costs nothing measurable over a pool of games; it only matters in a few last-Province positions.
- **Expected effect:** the new bot's win rate 50% +/- 2 points; changes only a small share of pairs.
- **Decisive test:** mode `dev`, pairing `same`, `L5R_TOURN_SEED_START=200`, `L5R_TOURN_SEEDS=8` (720 pairs, 1,440 games). Fixed sample, no extension.
- **Decision rule:** "no harm" if the new bot's win rate is at least 48% **and** the old bot does not win more pairs than the new bot at one-sided exact sign p < 0.10 **and** the new bot has no more
  soundness findings than the old **and** no deck (>= 40 decided games) where the new bot is below 35% while the old bot is at 50% or more. Otherwise record harm or inconclusive and report; nothing is reverted automatically.

## Result (fill in after the single run; do not edit the plan above)

- Command: `L5R_TOURN_CHAL=pre-laststand L5R_TOURN_PAIRING=same L5R_TOURN_MODE=dev L5R_TOURN_SEED_START=200 L5R_TOURN_SEEDS=8` (1,440 games, all decided). Report: `2026-10-18-laststand-report.md`. Wall clock 0:24:10 on 10 cores (Mac). Tokens: none per game.
- **Outcome: no harm.** New bot won 723, old bot 717 = **50.2%** (Wilson 47.6-52.8%), above the 48% floor; sign test p = 0.90. Paired: new won both games of a pair 4 times, old 1, 715 split; the old bot did not win more pairs.
- Soundness: 0 stalls, illegal commands, exceptions, decision-cap, setup failures or out-of-time games. Mean turn 14.6, no turn-limit games; margins within +/-0.02.
- Per deck (challenger = old bot piloting): Crab 44%, Crane Dominance 56%, Crane preset 56%, Dragon 67%, Lion Legacy 26%, Lion preset 41%, Phoenix 28%, Scorpion 78%, Scottish Lion 26%, Spider 75%. No deck where the new bot is below 35% while the old is at 50% or more.
- Observation, not a finding: the new Lion Legacy deck wins only about 26% as a bot-piloted deck, as low as Scottish Lion and Phoenix; the bot pilots it poorly or it is a weak list.
- Decision: `UseCavalryLastStand` stays on. The Lion Legacy deck changes nothing about the comparison (same pool for both bots).
