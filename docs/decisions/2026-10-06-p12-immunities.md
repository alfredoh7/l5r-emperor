# P12: the immunity ledger (issue #696)

Status: **built, engine only** (no card wired, no scan, no art pack: the cards this unlocks are wired together in a follow-up wave). Lane: rules/shared. Source: primitive P12 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668, corrected in #671). Code: `src/L5R.Core/ImmunityLedger.cs`; tests: `tests/L5R.Core.Tests/ImmunityLedgerTests.cs` (suite `immunities`).

## What it is

One ledger and one question, `IsImmune(card, kind, sourcePlayer, sourceCard?, playerId?, battlefield?)`, asked once at the choke point where an effect would happen. It is pure (nothing is spent or logged); the choke points log `immunityNegated` through `ImmuneNegates`.

An immunity is either **stored** (an `Immunity` record in `MatchState.Immunities`, created by an action) or **standing** (a `StandingImmunity` rule a card's feature file registers once on `EngineHooks.StandingImmunities`, read live while the carrier is in play). Both serialize omitted until used (`Immunities` is a nullable list, `ImmunitySequence` is omitted at zero; property orders 2200 and 2201), so a match that never had an immunity saves exactly as before.

## The API the wave calls

```csharp
// A card's feature file, in its module initializer (priority >= 10000):
EngineHooks.StandingImmunities.Register("EarthsArmorBow", 12700, static () => new StandingImmunity(
    Id: "earths-armor-bow",
    OwnerIs: StandingImmunity.OnEffect("earths-armor"),          // the carrier: any in-play card whose catalogue Effect matches (or any Func<CardDefinition,bool>)
    Kind: ImmunityKind.Bow,
    Scope: ImmunityScope.Self,                                    // Self | OwnersCards (TargetKeyword/TargetType) | AttachedFollowers | Player (province strength) | Battlefield (where the carrier stands)
    Source: ImmunitySource.OtherPlayers,                          // OtherPlayers | Any | OtherTerrains
    Condition: static (engine, ctx, carrier) => engine.IsDefendingUnit(carrier)));            // optional ("while defending"; the callback lives in the card's partial MatchEngine file, so it may call private helpers)

// An action that grants one ("negate other players' effects that prevent him ..."):
engine.GrantImmunity(card, ImmunityKind.Prevention, ImmunityUntil.Turn);                       // Turn (default) | Phase | TurnBoundary (boundary: UntilOwnersNextTurnBegins(me)) | Permanent | WhileInPlay (ownerCardId)
engine.GrantPlayerImmunity(playerId, ImmunityKind.ProvinceStrength, ...);                       // a player: his provinces, or with targetKeyword/targetType his cards
engine.GrantBattlefieldImmunity(battlefield, ImmunityKind.MovementAtBattlefield, ...);          // a battlefield
engine.RemoveImmunity(id);
```

`ImmunityKind`: `Bow`, `Destroy`, `DestroyInResolution`, `MoveNegation`, `Prevention`, `ForcePenalty`, `ProvinceStrength`, `TerrainDestroy`, `MovementAtBattlefield`.

## Choke points (one line each)

| Kind | Where | After |
|---|---|---|
| Bow | `StalwartNegatesBow` (every bow is per card, so a unit bow asks for each card: R-017) | the delay store, the shields |
| Destroy | `DestroyUnits`, `DestroyAttachment` | the delay store, the protections |
| DestroyInResolution | `TfResolutionDestroyable` (the one filter the three resolution destroy sites share) | Hitomi's Devotion |
| MoveNegation | `MovementNegationBarred` (the one predicate every movement negation already asks) and the two guards beside it (`MovementLockBarredBySled`, `SledBarsBattlefieldNegation`) | -- |
| Prevention | `HasActionLock` (action locks) and `MovementLocked` (movement locks, which also refuse assigning) | -- |
| ForcePenalty | `ApplyModifier` | the delay store, Tireless Efforts |
| ProvinceStrength | the writers `GiveProvinceStrengthThisTurn` / `GiveProvinceStrengthUntilResolution`; the readers `ProvinceSourceBonuses` and `StaticProvinceBonus` | -- |
| TerrainDestroy | `DiscardTerrain` | Interrogation |
| MovementAtBattlefield | `MoveHome`, `MovementToBattlefieldNegated` (so every move to a battlefield), the location switch | the shields |

Order everywhere is the one the prompt asked for: delay, protection, **immunity**, P7 watch, effect. An immune effect is negated, never delayed, and **a negated effect does not consume a P7 watch** (the immunity check returns before `TryConsumeWatch`).

An immunity stops only a **card effect**: `sourcePlayerId` is the acting player (the destroy's `actingPlayerId`, the running command's player otherwise). A cost, the rulebook and a battle's own resolution have none and are never stopped, except `DestroyInResolution`, which is the resolution's own destruction and ignores the source. An action whose bowing, destruction or movement "will not be negated" (Hiromi, Makito, Suitengu's Torch) is not stopped by a Bow, Destroy, TerrainDestroy, ForcePenalty or MovementAtBattlefield immunity, as it is not stopped by a watch.

## Readings for Mendon to confirm or correct

- **"Cannot be negated" is a negation of the negator.** A `MoveNegation` immunity makes `MovementNegationBarred` true for another player's negation (a lock, a shield, a Kitsune-style negation that already asks the predicate), so the movement happens. Negators that never ask that predicate (Hida Kaiji's own trait, the P4 shields) are not covered; the wave can route them through `MovementNegationBarred` as #670 did for the others.
- **`Prevention` ignores another player's lock, not an unknown one.** Action locks name their source card (so its controller is the source player); movement locks carry `ByPlayer`. A lock with no known source keeps binding (the Winter Sled convention). The Perfect Moment, Too Close to Home and Suikihime/Promo locks are not source-stamped and are not covered.
- **P11 stored restrictions (`NoAttack`, `NoDefend`) have no source player** and are therefore not stopped by `Prevention`. "Will not attack"/"may not attack" printed on the card itself is the card's own trait, not another player's effect; an action-made restriction that Prevention should stop needs a `SourcePlayerId` on `CardRestriction`, a P11 follow-up.
- **`ProvinceStrength`** stops a change another player's action makes (the timed writers) and does not count the contributions of another player's cards in play (source bonuses, static province bonuses). Direct permanent mutations (`province.Strength += n` by an own-turn card) are the owner's and untouched; the Flooded Pass terrain bonus (`RemATerrainProvinceBonus`) and a Region's own bonus are not filtered (a Region is the province's own).
- **`MovementAtBattlefield`** negates a movement made by a card effect from the battlefield a unit stands at, or to it. `MoveHome` and every `MoveToBattlefield` ask; the end-of-battle return home and assignment are not effects and are not negated (assignment is `Prevention`/movement-lock territory). A standing rule's battlefield is the carrier's province index (a card on the battlefield, or a Region attached to the province).
- **`TerrainDestroy`**'s `OtherTerrains` source is "the card whose action is running is a Terrain other than this one" (`EffectSourceCard()`); the battle's own discard of a played Terrain has no source and still happens.
- **A Destroy immunity is a negation, so the destroyed card does not count** (no Expendable draw, no Makito trigger), like P7's.
- **A stored immunity lapses with its subject card leaving play** and, for `WhileInPlay`, with the granting card.

## Not done

- No card is wired. The demonstration list in the prompt (The Earth's Armor, Susumu Haikaro, Rampant Paranoia, A Cleansing Breath, Oracle of the Void - exp's destruction line) is the follow-up wave's; "until your next turn begins" is `ImmunityUntil.TurnBoundary` with P4's `UntilOwnersNextTurnBegins`.
- No client change: nothing here touches the client; native checks are not required.
- A real battle-resolution run with a `DestroyInResolution` immunity (the opponent still gaining Honor for the other destroyed cards) is not driven end to end; the filter is tested directly and the rest is existing behaviour (an army minus the spared unit, as Hitomi's Devotion already does).
