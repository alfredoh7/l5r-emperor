# Archetype tests and hypotheses A and B: results (issue #963, run locally 2026-10-15)

Seven default-off arms, each against the champion on the same 1,728 games (dev seeds 66-77 for S1-S4 and the combined arm, 78-89 for A and B). Pre-registrations: `docs/bots/experiments/2026-10-15-archetype.md` and `2026-10-15-split-and-trades.md`.

All seven are null: 49.7% to 50.0%, no bot stalls, no pair test near significance (best p = .50). None goes to the held-out confirmation, none is a promote-candidate.

What the nulls say: the flags are nearly inert. Out of 864 pairs per arm, S3 changed none, A changed 7, S4 4, S1 9, S2 15, B 19, all four S flags together 22. The decisions these flags touch (a +30 score, an attack margin, a choice between "gain" and "lose" Honor, a trade attack, a split of defenders) are rarely the decision that decides a game. A deck's route to victory is mostly set by its draws and by what its Personalities do in battle, not by these choices.

Where to look next (not yet tested, ranked by what the A/A data supports):
1. **Battle-ability play.** Phoenix wins only 31% and Allan Lion 27% as the bot plays them; 59% and 72% of those decks' wins are military. Their battle spells and abilities are where the games are won; the bot's choice among them is the least examined part. Needs a replay study of lost games, not another flag.
2. **Opening and Dynasty spending.** Decks with one or fewer attacks in a game win 5-22% of those games (Allan Lion, Crab, Phoenix, Scottish Lion), against 39-53% otherwise; the cause of having no army to attack with is upstream of the attack gate (recruiting, Gold, bowed units).
3. **Crab's dishonor engine.** 74% of its wins are dishonor kills, but it wins only 38% overall; a trace of its lost games would show whether the engine is too slow, too easily answered, or the army too weak to hold until it works.
