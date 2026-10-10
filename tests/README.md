# Running focused checks

Run from the repository root. The Core and AI console runners accept `--list`
and `--filter TEXT`. Filters match the displayed suite, source method name, or
case description, case-insensitively. Repeat `--filter` to select **any** matching
case. Use suite prefixes to avoid incidental matches in long descriptions.
Listing selects cases without executing them. Invalid arguments or no matches
exit with code 2; failed tests exit with code 1.

```sh
# Discover cases, then run a small regression group.
dotnet run --project tests/L5R.Core.Tests -- --list --filter UnitView
dotnet run --project tests/L5R.Core.Tests -- --filter UnitView --filter FollowerForce
dotnet run --project tests/L5R.Core.Tests -- --filter core/Privacy --filter published/DetachedAbilities
dotnet run --project tests/L5R.Core.Tests -- --filter ActionQueries --filter OfferedActions
dotnet run --project tests/L5R.Core.Tests -- --filter published/CatalogueWaves

# Full-match simulations: core scripted training/live matches, then all AI seeds.
dotnet run --project tests/L5R.Core.Tests -- --filter core/CompleteMatch --filter published/Replay
dotnet run --project tests/L5R.AI.Tests -- --filter "seed "
# One seed, both training and live published fixtures:
dotnet run --project tests/L5R.AI.Tests -- --filter "seed 42:"

# No arguments still run every case in that executable.
dotnet run --project tests/L5R.Core.Tests
dotnet run --project tests/L5R.AI.Tests

# Required pre-merge checks remain unchanged; filters are not forwarded here.
bash tools/check.sh --skip-client
bash tools/check.sh  # also builds/imports/smoke-tests the native client
```

## Sharding and timings (issue #798)

PR validation runs the rules checks as parallel CI jobs. The console runners accept
`--shard I/N` (1-based): after any `--filter`, cases are dealt round-robin in registration
order, so the N shards of one build partition the cases exactly (`--list --shard 1/4` shows
what a shard runs). Every run ends with a summary of the slowest cases, every failing label
together, and the same text appended to the Actions job summary when `GITHUB_STEP_SUMMARY` is set.

`tools/check.sh` exposes the slices CI uses; no argument, `--skip-client` and `--client-only`
behave as before (the Mac job still runs everything):

```sh
bash tools/check.sh --part quick                  # tools' Python tests, comment lint, Server/Transport/Collection
bash tools/check.sh --part registration           # the two scratch-repo registration builds, run side by side
bash tools/check.sh --part core --shard 1/2       # half of L5R.Core.Tests
bash tools/check.sh --part ai --shard 2/2         # half of L5R.AI.Tests
```

`--part py` and `--part small` are the two halves of `quick`. The two registration tests are
skipped inside `py`/`quick` (they set `L5R_SKIP_REGISTRATION_BUILD`) and run only in
`registration`, or in the unpartitioned runs. Changing the matrix in `.github/workflows/check.yml`
must keep every part and shard present: dropping one silently drops those tests from PR validation.

For repeated runs after building, invoke the corresponding
`tests/<project>/bin/Debug/net10.0/<project>.dll` with `dotnet` and the same flags.
The test count can increase when a loop becomes independently reported cases;
it does not measure coverage or runtime improvements.

## Regression consolidation coverage map (issue #235)

| Original case | Replacement | Unique assertions preserved |
| --- | --- | --- |
| core `FollowerForce` | Same method, corrected description; shared Force battle fixture | Bowed host displays unit 2F; 1F enemy dies; attacker returns home |
| core `EliteFollowerForce` | Same method and shared fixture | Plain and Elite bowed Followers together; province destroyed; explicit 2–0 resolution added |
| core `ElitePersonalityForce` | Same method and shared fixture | Granted Elite on bowed Personality destroys province; explicit 9–0 resolution added |
| core `UnitViewBows` | Four `UnitViewBows/…` cases plus `UnitViewRemoval` | Explicit 5/0/5/7 contributions; detached Follower removes 5F; remaining 2F also shown at home |
| core `UnitViewResolution` | Same method, literal expected totals | Bowed Item adds once: Personality 4F, unit 9F, battle 9–0; returned bowed unit shows 0F |
| core `BowedPersonalityResolution` | Attacker and defender cases | Actual Kisho/two Guides; Force/Chi 3/3; bowed unit 10F, ready 13F; opposing death, own survival, province destruction only for attacker; fixture export retained |
| published `Attachments` | Retained | Actual Item Force/Chi, bowed state, paid equip, Armor/Weapon limits, rejection atomicity and disabled offers |
| published `Followers` | Three named card cases | Actual Veteran Skirmishers/Rank & File/Native Guide; 2/4/6 Gold payments, separate 3/4/5 Follower Force and host 0F |
| core/published `Privacy` | Separate cases using `HiddenIdsAbsent` | Training own hidden provinces, hidden IDs/deck order/Seed, private purchase quote; published enemy hidden provinces and hand |
| core `Detached`; published `Privacy` mutation | `Detached`; `DetachedAbilities` | Diagnostic state/card collection and province-array mutation; nested published definition ability-array mutation still yields `border-first` |
| core `ActionQueries` | Economy and starter cases | Every enabled exact command executes on a fresh replay; nonempty offers asserted |
| published `OfferedActions` | Action/battle × players 0/1 | Live-deck cards, opponent attachment, bowed performer, Favor/first-turn setup, fresh state per command and every alternate target |
| core/published `Setup` | Separate cases using `OpeningCounts` | Common 4 provinces/6 hand/36 Dynasty/34 Fate; training phase/version/face-up state/Bamboo owner; published first player/Honor/strengths/no training definitions |
| core `Bamboo`; published `Setup` timing | `Bamboo`; `BambooTiming` | Training end-Dynasty progression versus published pass progression; stays bowed first own turn, straightens second |
| published `Catalogue` | `Catalogue`, live/frozen `CatalogueDecks`, nine `CatalogueWaves/…` cases | Pool 273; deck/card counts 40; card costs/Chi/text/art/abilities; vanilla count 63/effect/text/no abilities/disjointness; each wave's original literal count/type/text/wiring and all five predecessor-disjointness checks |
| core/published `Accept`/`Reject` | Shared assertions | Accepted command result; rejected command and entire serialized state unchanged, now with command context |
| `Priority`, `CavalryAfterEmptyInfantry`, `AssignmentRollback` | Retained | Submitting empty assignments versus passing; invalid later assignment rolls back earlier assignment |
| `Never`, attachment cleanup cases, `Ancestor`, battle tests | Retained | Direct ability destruction, keyword/token cleanup, Halls timing versus resolution casualties |
| Core scripted, published scripted, AI matches | Retained with filtering | Training, frozen card fixtures and live-deck simulations remain separate; existing bounds, replay and invariant checks unchanged |
