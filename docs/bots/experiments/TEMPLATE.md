# <date> <name> (pre-registration)

- Issue / branch / arm name:
- Champion commit (git rev-parse origin/main):
- **Hypothesis** (a weakness in the champion, from a logged loss or a BOT_LOGIC.md gap):
- **Change** (the `BotContext` flag, default off; what decision it alters; reads only the seat's own PlayerView):
- **Expected effect size** (win rate vs champion, and the metric that should move):
- **Decks / situations it should help** (and where it should be neutral):
- **Decisive test:** pairing `independent`, mode `dev`, `L5R_TOURN_SEEDS=<n>` (= <games> games, ~<decided> decided), primary metric win rate, one-sided exact sign p < 0.025,
  minimum effect <x>%, soundness findings = 0, no per-deck collapse. Stopping rule: fixed sample, no extension.
- **Confirmation:** mode `holdout`, `L5R_TOURN_SEEDS=<m>`, same rule.
- **Wall clock estimate / token budget:**
- Decision rule in one sentence:

## Result (fill in after the single run; do not edit the plan above)

- Command, output files, wall clock, tokens:
- Outcome: promote-candidate | negative | inconclusive. Numbers:
