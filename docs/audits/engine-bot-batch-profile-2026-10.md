# Engine profile: seeded bot-vs-bot matches, October 2026 (issue #888)

Follows [engine-profile-2026-10.md](engine-profile-2026-10.md) (#863), whose conclusion 2 (remember payment tokens within one `QueryActions` call) this applies.

Measured on an Apple M5 Mac, Release build, one thread. The runner is `tests/L5R.AI.Tests/BatchProfile.cs`:

```sh
L5R_PROFILE_GAMES=20 [L5R_PROFILE_SEED=1] dotnet run -c Release --project tests/L5R.AI.Tests -- --filter "bot-profile"
# optional: L5R_PROFILE_ALLOC=1 (allocations by type), L5R_PROFILE_THROWS=1 (where exceptions are thrown)
```

It plays N seeded matches of two `TrainingOpponent` bots over the soak harness's random decks (the same decks as `bot-soak`), and times each decision as: engine
views, engine action queries, engine `Execute`, the harness's own JSON copies, and the bot's choice. Where the time goes was found by attaching `dotnet-trace`
(`dotnet-sampled-thread-time`) to the running process.

## Where the time went (before)

| Part of a decision | Share of time |
|---|---|
| `GetView` (twice per decision in the harness: seat 0, then the acting seat) | 35% |
| `QueryActions` | 43% |
| `Execute` | 10% |
| Harness JSON copies | 11% |
| The bot's choice | 1% |

At 6.2 ms and 4.4 MB allocated per decision, 20 matches took 48 s (0.41 matches/s, 160 decisions/s). The bot is not the cost; the engine's reads are.

- `GetView` called `ForgottenLegacyBatch5Force` for every Personality of every view, which computed `Attachments(...).Count(...) * PearlCount(owner)`. The product evaluated
  `PearlCount` (a scan of every card with a keyword test each) even when no Pearl of Rage was attached. A fifth of `GetView` went there.
- `QueryActions` asked for the Gold payment tokens a payer could name hundreds of times per call, and each ask ran every registered token hook over every card, sorting all of a
  player's cards by Id inside two of the hooks.
- `Keyword(card, ...)` built a `Concat(...).Contains(...)` LINQ chain on every call.
- `ValidateAbility` rejects through `Require`, which throws: about 120 `RuleViolation` exceptions per decision, about 18% of the time.

## What changed

- **Pearl count only when a Pearl of Rage is attached.** The product is 0 otherwise.
- **Payment tokens are remembered for the length of one `QueryActions` call** (`QueryScope.cs`) and dropped when it ends; `Execute` and every other path compute them afresh. The
  two sorting hooks now sort only the cards that offer a token.
- **`Keyword` tests printed and granted keywords with a loop**, and `Wave827Force` no longer copies the attachment list.
- **Tests.** `L5R_VERIFY_QUERY_SCOPE=1` (or `MatchEngine.VerifyQueryScopeForTests`) recomputes every remembered token list when it is reused and fails if it differs; a
  bot-profile test plays four seeded matches under it, and the whole core and AI suites pass under it too. A query is not byte-pure (it lazily seeds
  `PrintedHoldsSeeded`), which is why the guard compares token lists, not the whole match.

## After

Interleaved A/B, 20 matches each, three rounds (baseline is main at 70f10699 with the same runner):

| | Baseline | After |
|---|---|---|
| Wall time, 20 matches | 48.8 / 49.8 / 50.1 s | 36.6 / 36.9 / 37.2 s |
| Matches per second | 0.41 | 0.54 |
| Allocation, 20 matches | 46.1 GB | 30.8 GB |

About 33% more matches per second, 26% less wall time, a third less allocation, identical play (the suites and seeded replays are unchanged).

## Tried and dropped

- **One shared copy of each card definition per view.** A quarter of `ViewCard` looked like the per-card `CardDefinition` copy, but `published/DetachedAbilities` and the
  state-cloner suites pin the contract that every view owns its copy, and sharing it broke them. A faster hand-written copier saved only 5% of view time, so it was not kept.
- **Remembering `ValidateAbility` results.** Across 350,000 calls in a query scope, none repeated the same command, so a memo would never hit.
- **GC settings** (bigger gen0, non-concurrent, server GC): a 10x drop in collections changed nothing, so time is allocation volume and work, not pauses. Server GC gave about 10%
  on this batch; it is a runtime option for a batch runner, not something the game client should change.

## What remains

1. **Exceptions as control flow: about 18% of a decision.** `ValidateAbility` is about 1,750 lines of `Require` checks; `QueryActions` calls it about 290 times per decision
   and about 120 of those throw. The next step is a non-throwing `TryValidate` that returns the message the same checks produce, converted check by check and guarded by the
   existing listing/validation consistency tests (#842). That is a real refactor, not a patch.
2. `GetView` costs about 0.65 ms for about 200 cards; most of that is the `CardView` and its definition copy, so it only falls with fewer or lighter copies.
3. The harness copies views and actions through JSON on every decision (11%); a bot runner that does not need detached copies would save it.
