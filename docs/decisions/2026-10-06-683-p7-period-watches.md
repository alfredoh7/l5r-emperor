# P7: period stamps and negation watches (issue #683)

Status: **built, engine only** (no card wired, no scan, no art pack: the cards this unlocks are wired together in one follow-up wave). Lane: rules. Source: primitive P7 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668, corrected in #671). Code: `src/L5R.Core/PeriodWatches.cs`; tests: `tests/L5R.Core.Tests/PeriodWatchTests.cs`.

## What it is

1. **Period stamps.** `Period { Turn, Battle, Phase, Game }`. A use is one stamp in the `AbilityUses` dictionary every card and player already has, key `period:<Period>:<key>`, holding the number of the period instance it was spent in (the turn; the printed phase of the turn, so the Attack Phase's sub-steps are one phase; the battle's number, or one value for "no battle"; the game). It is compared on read, so nothing is ever cleared. `AbilityDefinition.UsesPer` makes a printed ability use it: `AbilityUseSpent` and `SpendAbilityUse` (AbilityLimits.cs) read it, and `ValidateAbility`, every Reaction/Trigger offer and the resolution all go through those two, in the same place `OncePerGame` is enforced. Counters (`AddPeriodCount`, `PeriodCountOf`) count within an instance and read zero once the period has moved on.
2. **Negation watches.** `TryConsumeWatch(kind, target, sourcePlayerId)` is the one question asked where an effect would bow, destroy, move, straighten or dishonor a card, and where a Political action is announced. A watch is *dynamic* (a `NegationWatch` record an action creates) or *standing* (a `StandingWatch` rule a card's feature file registers once on `EngineHooks.StandingWatches`).
3. Every negation is recorded (`WatchConsumptions`, this turn and the last) so a follow-up can ask `WatchNegated(kind, cardId)`.

## The API the waves call

```csharp
// A card's feature file, in its module initializer (priority >= 10000):
EngineHooks.StandingWatches.Register("KazutoshiLoyalDestroy", 12400, static () => new StandingWatch(
    Id: "kazutoshi-loyal-destroy",
    OwnerIs: StandingWatch.OnEffect("kazutoshi-exp2"),           // the carrier: any in-play card whose catalogue Effect matches (or any Func<CardDefinition,bool>)
    Kind: WatchKind.Destroy,
    Scope: WatchScope.OwnersCards, TargetKeyword: "Loyal", TargetType: CardType.Personality,
    Source: WatchSource.OtherPlayers,                          // another player's card's effect (WatchSource.Any: the controller's too)
    Period: Period.Turn,                                        // "the first time each turn" (Game: once per game for each carrier)
    Condition: static (engine, ctx, carrier) => true,           // optional ("if you have Reconnaissance")
    OnNegated: static (engine, ctx, carrier) => { /* move him home, bow him, dishonor him */ }));

// An action that creates a watch ("negate his next destruction this turn"):
engine.AddNegationWatch(new NegationWatch { OwnerPlayerId = p, Kind = WatchKind.Destroy, TargetCardId = id, LastTurn = engine.State.TurnNumber });

// A printed "once per battle" ability:  new AbilityDefinition(..., UsesPer: Period.Battle)
// A counter:                           engine.AddPeriodCount("pierce-copies", Period.Battle); engine.PeriodCountOf(...)
```

`WatchScope`: `Self`, `OwnersCards` (keyword and/or type), `AttachedFollowers` (Ikeuchi's Followers), `Player` (a Political action that names no card). A dynamic watch covers one `TargetCardId`, or every card of `OwnerPlayerId` with `TargetKeyword`, or (neither) the player. `Remaining` (negations left, negative: never), `Refresh` (at most once per period instance), `LastTurn`, `OwnerCardId` (the watch ends when that card leaves play).

## Choke points (one line each)

| Kind | Where | After |
|---|---|---|
| Destroy | `DestroyUnits` (MatchEngine.cs), `DestroyAttachment` | the delay store, Wide Valley, Negishi, Megumi's shield |
| Bow | `StalwartNegatesBow` | the delay store, the shields, Blessings |
| Move | `MoveHome`, `TryMoveToBattlefield` | the delay store, the one-shot shields |
| Straighten | `Straighten` (never during the Straighten Phase) | the delay store and Hida's Guidance |
| Dishonor | `Dishonor` | the delay store, Ji Yun, Suikihime |
| PoliticalAction | the announce chain, beside Kamalakar's negation | Protection stops the negation as it does for Kamalakar |

Order everywhere is the one the prompt asked for: delay, protection, watch, effect. `HonorGain` exists as a kind with **no choke point** (P14 owns the Honor-change registry).

A watch negates only a **card effect**: `sourcePlayerId` is the acting player (`DestroyUnits`' `actingPlayerId`, the resolving command's player for the others); a cost, the rulebook and a battle's own resolution have none and are never negated. `HiromiUnnegatable()` / `CurrentActionUnnegatable()` keep their precedence for bow, destroy and move.

## Readings for Mendon to confirm or correct

- **Standing watches persist as spent for the game.** What a standing rule has spent (`WatchSpent`, keyed rule + carrier) survives the carrier leaving play and returning, so "Negate Gyoken's first destruction each game" is spent once and for all. A card-owned *dynamic* watch, by contrast, ends with its owner.
- **A negated destruction is not "destroyed".** The negated card gets no Expendable draw, no Overconfident consequences and no Makito trigger, as for the existing negations. The follow-ups a card prints (Kazutoshi: move home, bow, dishonor) run from `OnNegated` after the negation, and each of those effects is itself a new effect that watches may meet.
- **A Battle period outside a battle is its own single instance** (per turn): "the first time each battle" used between battles is spent once for that stretch.
- **`Any` source includes a card's own controller's effects but never a cost**: a cost is not an effect (R-004).

## Not done

- No card is wired. The demonstration list in the prompt (Gyoken, Legion of the Fallen, Ikeuchi, Avenging the Fallen, Trade Embargo) is wired in the follow-up wave.
- `HonorGain` kind unhooked. The rulebook Straighten Phase is deliberately not a watch point.
- Native client checks are not required: nothing here touches the client.
