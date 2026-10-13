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

## Phase 2: no more exceptions for ordinary refusals (issue #890)

`ValidateAbility` was a 1,750-line `void` that refused by throwing. It now returns the refusal as a string (`ValidateAbilityResult`, null when the command is legal):
`ValidateAbility` wraps it and throws the same message for every command path, and `TryValidateAbility` (message or null) serves the action listing. The 197 `Require(...)` /
`throw new RuleViolation(...)` statements that sit directly in the method became `if (!cond) return message;`, done by a one-off Roslyn rewrite (not in the repo) so no check was
retyped by hand; checks inside a lambda, local function, try body or finally were left throwing, since a conversion there would change what is caught. The same was done for the
"Choose a performing card" check, the Favor-discard check and the slot-candidate check, and the listing's catch sites now call `TryValidateAbility`.

| | Before | After |
|---|---|---|
| `RuleViolation` thrown per decision | 120 | 2.3 |

What is left are throws from registered validation hooks and helpers (`RegisteredValidateEarly`, `ValidateWielderCosts`, `RequireLegalAttackTarget`), a few hundred per thousand decisions.

**Speed.** Interleaved A/B against main after #889, 20 matches, three rounds: 35.3 / 34.5 / 34.1 s before, 32.3 / 31.9 / 33.4 s after, about 6% less wall time. That is less than
the sampler's 18% suggested: the trace charged some of the garbage collector's work to the throw sites.

**Proof it is the same engine.** `L5R_PROFILE_HASH=1` hashes the JSON of every action list the engine produces (every option, disabled reason and quote, in order). Main and this
branch give the same hash over 30 seeded matches (11,213 decisions, hash F57A99959BBCD681) and over 40 more (13,270 decisions, hash 324ED2B93F6BB9F0).

## What remains

1. **Hook-based throws.** The rest of the exceptions come from registered validation hooks and helpers; converting them the same way is mechanical but each hook is its own file.
2. `GetView` costs about 0.65 ms for about 200 cards; most of that is the `CardView` and its definition copy, so it only falls with fewer or lighter copies.
3. The harness copies views and actions through JSON on every decision (11%); a bot runner that does not need detached copies would save it.
