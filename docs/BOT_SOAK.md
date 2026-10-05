# Bot soak test (issue #642)

Both seats of random-deck matches are played by `TrainingOpponent` through the real engine (views and actions are JSON round-tripped exactly as in the other AI tests). Decks are seeded, built only from cards the engine reports as implemented, accepted by `CustomPractice.Review`, and biased so each seed forces a rotating slice of the pool: over enough seeds every wired Dynasty/Fate card is in some deck.

Findings, all collected rather than thrown: `stall` (bot returned no command although its seat had an enabled non-concede action), `illegal` (engine rejected the command), `cap` (no result in 4000 decisions or the per-match wall time, or neither seat has an action; the last 20 commands are recorded), `exception` (engine or bot threw), `setup` (a generated deck was not Ready). Two more categories are informational and never fail the suite: `rejected` (the engine OFFERED an action and then refused it; the bot is told, as a player is when an action errors, and chooses again - each one is a gap in the engine's legal-action list worth fixing, and `illegal` is only raised when 24 answers in a row are refused) and `slow` (a match ran out of wall time, which depends on the machine). A match that reaches turn 80 (`L5R_SOAK_TURNS`) is a stalemate and ends as finished with the reason "turn limit"; `L5R_SOAK_CAP` changes the 4000-decision cap. The report also holds a per-ability census (offered enabled / chosen / accepted), the wired ability ids never offered, and the wired cards that were in no deck.

## Commands

Fast CI suite (12 seeds, about 2 minutes cap, fails on any finding unless `L5R_SOAK_ALLOW_FINDINGS=1`; coverage gaps only print a SKIP note unless `L5R_SOAK_STRICT=1`):

    dotnet run --project tests/L5R.AI.Tests -- --filter bot-soak

Heavy mode (parallel, never fails on findings, writes `<path>.json` and `<path>.md`):

    L5R_SOAK_MATCHES=500 L5R_SOAK_SEED=1 L5R_SOAK_REPORT=artifacts/soak dotnet run --project tests/L5R.AI.Tests -- --filter "bot-soak: random"

`L5R_SOAK_PARALLEL=0` forces a single thread. The report is deterministic per seed apart from wall-time caps.
