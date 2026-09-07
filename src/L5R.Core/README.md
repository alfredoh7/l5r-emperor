# Rules engine foundation

This library has no Godot or networking dependency. It runs synthetic training scenarios and the **published Lion/Crane 40/40 decks** through complete matches. Every enabled card comes from the explicit catalogue; published definitions are loaded from the embedded, reviewed 40-card snapshot. The wider Emperor reference pool is not enabled. Use `MatchEngine.CreatePublishedMatch(seed)` for the published profile.

## Application contract

```csharp
var game = MatchEngine.CreateEconomyScenario(seed: 20260906);
var view = game.GetView(playerId: 0);
var proposed = game.QueryActions(playerId: 0);
var result = game.Execute(proposed.First(x => x.Enabled && x.Type == "purchase").Command!);
```

- Player IDs are `0` (Crane) and `1` (Lion). Initial state version is zero. Every accepted command increases the version once. Rejected commands change neither state, events, nor replay.
- `QueryActions` returns proposed exact commands, reasons for unavailable actions, legal targets and payment alternatives. A client must show choices and may replace `SourceIds`, `TargetId`, or the simultaneous `Assignments` mapping before confirmation. `Execute` always revalidates.
- A purchase opens a Proclaim reaction when eligible. The Personality is already in play. `proclaim` spends the player's use and passes priority; two consecutive `declineReaction`/`pass` commands close the window. The same use cannot be spent on another Personality that turn.
- `State` is a detached diagnostic snapshot containing secrets. Send only `GetView(seatId)` and that seat's action query across a client boundary. Enemy hands are omitted; face-down province identities are masked even from their owner. Public draw/refill events do not contain card IDs.
- The engine is single-threaded. Servers must serialize access per match and derive the actor from authenticated seats.
- `ExportReplay` includes the seed and accepted commands; treat it as secret during the match. `Replay` rejects incompatible versions. Never expose replay contents through ordinary player snapshots.

## Verified rule foundations

The source is the [AEG Emperor Edition rulebook](https://www.aftertheboardgame.com/_downloads/2bcbbadb334d8f13b9a6853e8460f1e0), using printed page numbers:

| Area | Pages |
|---|---|
| Starting cards, shuffle, provinces, initial hand | 48–53 |
| Payment-specific Gold and lost excess | 49–50 |
| Straightening, revealing provinces, action rounds | 54–59 |
| Default once-per-turn ability limit | 61 |
| Equipment | 37–39, 69 |
| Infantry assignment, battle order, presence/location | 71–79 |
| Force, ties, destruction, Honor and returning home | 80–82 |
| Dynasty purchase, Clan discount/allegiance, Proclaim | 83–85 |
| End-phase hand limit and victory timing | 87–89 |

Training Marketplace produces 2 Gold, increased to 3 for Crane. This conditional behavior also matches Oracle's Marketplace reference, card ID 4840. The starting Holding fixtures intentionally omit Border Keep's additional filtering abilities; they must not be presented as fully implemented published cards.

Published card effects, Border Keep, Imperial Favor, opponent choices, performer constraints, use limits and attachment restrictions are implemented for this closed pool. Not implemented outside that pool: general nested reactions, cavalry, duels, dishonor/rehonor interactions, spells, Rings, events, regions, deck legality or production deck construction. Additional cards require explicit definitions, timing support and interaction tests before being enabled. See `docs/rules/LION_CRANE_COVERAGE.md`.

## Validation

`tests/L5R.Core.Tests` is a dependency-free executable test suite. It checks payment combinations and conditional production; Clan options and Proclaim timing/use limits; atomic rejection; private views; turn flow; battle thresholds, ties, attachments and Honor; start/end victory timing; replay; and a complete scripted training match. Exit status is nonzero on failure.

## Ability commands

`useAbility` selects a curated `AbilityId` on an owned `CardId`, or one of the three rulebook Favor abilities with no source. `TargetId`, `PerformerId` and ordered `CardIds` are separate fields. Shared read-only validation is used by both ability discovery and command execution. Payment costs are validated before mutation; execution remains atomic. `chooseShinden` is accepted only from the controller named in its pending choice, and then restores the normal alternating action opportunity. Individual card and player ability usage is tracked by global turn number; Border Keep’s once-per-game entry remains spent across turns.

Published replay and client view include the selected scenario. The view exposes Favor ownership, effective Force/Chi and active straightening prevention. Definition arrays are copied at the view boundary. The engine version changed to prevent earlier saved commands from replaying under different card semantics.
