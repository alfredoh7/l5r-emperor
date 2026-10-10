# PARKED: what makes a good L5R player, and so a good bot (bot battle policy and trade attacks)

Parked 2026-10-09 by Mendon so he can think about what "good" means before changing the bot further. Nothing here is merged. Pick it up from branch
`claude/788-bot-battle-policy` (issue #788). Everything below is evidence from our own arena runs, not a rules ruling.

## The open question

We have been tuning the practice bot by adding rules (pass when won, target priorities, send-home, Border Keep refresh, trade attacks) and measuring each against the bot
without it. The results say the rules are not obviously the bottleneck. Before more rules, decide what a *good* player does and how to measure that in the bot:

- What wins L5R games: Provinces, Honor, or board control? Which of those should the bot be pursuing by turn?
- Is a good player decided by one big choice (which deck plan, when to attack) or by many small correct ones (targets, timing, holding back)?
- What should "good" mean for a *practice* opponent: strong, or fun and readable for a human learner? A beatable bot may be the better product.
- Which deck/bot interactions are the bot's fault and which are the deck's? (Lion Legacy, Scottish Lion and Phoenix Talons win almost nothing with any version of the bot.)

## What was built (all on `claude/788-bot-battle-policy`)

1. **Six battle rules** (commit 7098ba5e, switch `BotContext.UseBattlePolicy`, default on): pass when the battle is won (with exceptions), Attack-ability target choice
   (highest Force it can destroy, ties to unit gold), bow target priority, a losing defender sends a non-contributing unit home, first-action reactions only when the battle
   is not already won, and a Border Keep refresh with 22+ Gold. Code: `src/L5R.AI/BattlePolicy.cs`, `TrainingOpponent.Battle.cs`. Tests: `tests/L5R.AI.Tests/BattlePolicyTests.cs`.
   Decision record: `docs/decisions/2026-10-09-bot-battle-policy.md`.
2. **Trade attacks** (switch `BotContext.UseTradeAttacks`, **default OFF**): when the bot cannot raze a Province it still attacks if it would beat every possible defender
   (destroying the whole defending army at no cost, since the engine destroys only the losing army) or tie favourably on Gold (a tie destroys both armies). A unit's worth is its
   printed Gold plus everything attached, plus 2 for a Personality. Code: `BattlePolicy.PlanTrade` / `Evaluate`, `TrainingOpponent.Battle.cs` (`TradeFor`, `TradeOf`, `TradeScore`).
3. **Arena support**: `BotArena.RunPolicy` plays every ordered pair of decks twice per seed with the bots swapped, from the same seed, so deck, seat and shuffle cancel.
   `L5R_ARENA_TRADE=only` compares trade attacks alone. Entry point: `BotSoak.cs` ("bot-arena: battle policy"). Run with
   `L5R_ARENA_DECKS=<folder of .txt decks> L5R_ARENA_REPORT=<file> L5R_ARENA_SEEDS=1 L5R_ARENA_SEED=1 dotnet run --no-build -- --filter "battle policy (issue #788), decks"`.

## Results

12 decks (Lion and Crane presets plus ten starter decks), 264 games per run, turn limit 80, 12-minute budget per game.

| Comparison | Wins | Decided | p (sign test) | Verdict |
| --- | --- | --- | --- | --- |
| Six battle rules (New) vs the bot before them (Prior) | 137 vs 117 (53.9%) | 254 of 264 | 0.2331 | Inconclusive; no sign of harm. |
| Trade attacks on vs the same bot with them off | 116 vs 144 (44.6%) | 260 of 264 | 0.0938 | Leans against; not significant. Trade attacks left OFF. |

Full reports: `2026-10-09-arena-six-battle-rules.md` and `2026-10-09-arena-trade-attacks.md` in this folder.

Detail worth keeping:

- Six rules: seat 0 won 63/126, seat 1 won 74/128. Mean win length turn 16.2 for both bots. Ten games had no winner (12-minute budget, turns 18-27); seven involved
  Crab Control, Mantis Manipulation or Unicorn Maidens.
- Trade attacks: most decks did slightly worse (Crab Control 4/21 vs 9/19, Lion preset 9/21 vs 16/22, Spider Conquer 13/22 vs 17/22); none clearly better. The bot's wins
  were shorter (turn 14.6 vs 16.0), so it was more aggressive but not more successful. Four games had no winner, all involving Crab Control (two out of time, two "no
  complete legal action was supplied" stuck decisions, one in each seat, so probably a separate pre-existing problem).
- Lion Legacy, Scottish Lion and Phoenix Talons win about 1 game in 4 or fewer with every bot version.

## Conclusions (hypotheses; no per-game traces were read)

- The six rules are roughly neutral to mildly positive. One seed is not enough to prove either way; about 4 more seeds (~1,000 games, 1-1.5 hours) would settle it.
- More aggression by itself did not help. Winning a battle without razing the Province kills defenders but sends the attackers home bowed, leaving the bot exposed to a
  counter-attack, and it does not move the win condition. Likely reasons it lost: lost tempo and an exposed home. Untested.
- The engine facts that shaped this: the loser's whole army is destroyed and the winner's is untouched; a tie destroys both; the Province falls only if attacker Force exceeds
  defenders plus Strength (`MatchEngine.ContinueResolveBattle`). So "a failed attack" can only destroy defenders in a win-without-razing or a tie.
- The weak decks may be a deck problem rather than a bot problem (their plans may not suit a generic bot). Counting attacks made per game in those decks would show which.

## Ideas not yet tried

1. Per-rule on/off switches for each of the six rules, then test each alone, so we know which help and which hurt.
2. Attack-count and action-count traces for the Lion decks and Phoenix Talons (the arena already prints actions per seat).
3. Time and trace the Crab Control / Mantis / Unicorn games that hit the time limit or got stuck; fix any decision loop.
4. A stricter trade attack: only when enough units stay home to hold the bot's own Provinces, or only for a large kill (for example 10 Gold or more of defenders).
5. Smarter "pass when won": still use free abilities that cannot reduce Force (draw, develop).
6. Use in-play abilities before hand cards outside battle too.
7. An "all-in" plan chosen when ahead on Honor or when the opponent has few Provinces left.

## Status when parked

- Branch `claude/788-bot-battle-policy`; the six-rule work and the trade-attack work are committed together with this note. No PR is open for #788.
- PR #787 (starter decks) is separate and still awaiting CI/merge.
- Before opening a PR: rerun `bash tools/check.sh --skip-client` on the final code and decide whether trade attacks stay in as a default-off switch.
