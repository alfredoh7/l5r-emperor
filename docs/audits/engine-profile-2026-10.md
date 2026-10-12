# Engine profile (issue #863)

Measured with `tests/L5R.Core.Tests/PerfProbe.cs` (`L5R_PERF=1 dotnet run --project tests/L5R.Core.Tests -c Release -- --filter perf-probe`; a no-op otherwise) and a sampling trace
(`dotnet-trace collect --profile dotnet-sampled-thread-time`, analysed by inclusive time per frame). Four seeded published-deck games, 753 commands, random enabled choices.

## Numbers (M-series Mac, Release)

| Operation | Cost | Note |
|---|---|---|
| `Execute` | 2.8 ms / command | ~76% of it is the state copy (21.9 of 28.8 points of the whole trace); `Apply` itself is 6.2 |
| `QueryActions` | 2.4 ms / call (~4 enabled options) | ~half is payment-token computation: `BuildQuote` -> `QuoteAbilityGold` -> `PaymentSourcesFor` -> `PaymentTokens` (16 of 31.6 points); `MechEPaymentTokens` alone is 9.7 |
| `MatchEngine.State` | one full JSON round trip per read | `State => Copy(_state)`; 17% of the probe, which reads it once per step. Tests read it ~17,800 times |
| JSON copy | ~3 ms for a 245 KiB late-game state | `Copy<T>` = `JsonSerializer.Deserialize(Serialize(x))`; ~27% reading, ~23% writing |
| Post-command sweeps (#854) | 0.001 - 0.013 ms each | negligible: the hypothesis that they add up was wrong |

## Where validation time goes (full `check.sh --skip-client`, Core shards ~426 s in cases)

- Two audit tests are 145 s of it: `listing-consistency/ValidatedMeansListed` (104 s) and `multi-window-audit/ReportSingle` (41 s). Both are `QueryActions` loops, so payment-token cost is their cost.
- `tools/check.sh` already runs its stages side by side (issue #805), so local wall time is the slowest shard, not the sum: core-1 207 s, core-3 132 s, core-2 87 s. The 104 s audit sits in core-1, so splitting it into several cases would let the shards balance.

## Conclusions

1. Faster `Copy` helps every command and every `.State` read (tests, bots, server).
2. Memoising payment tokens inside one `QueryActions` call helps the listing and both audit tests.
3. Sweeps are not worth touching.
4. A parallel local runner is not needed (stages already run side by side). Shard balance is: splitting the two audit tests into several cases shortens the slowest shard without touching the engine.
