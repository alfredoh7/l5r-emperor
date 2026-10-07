# P17: additional-action scope (issue #721)

Status: **built, engine only** (no card wired, no scan, no art pack: the cards this unlocks are wired in a follow-up wave). Lane: rules. Source: primitive P17 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/AdditionalActionScope.cs`; tests: `tests/L5R.Core.Tests/AdditionalActionScopeTests.cs` (suite `additional-action-scope`, 7/7).

## What it is

| Piece | API | Where it reads |
|---|---|---|
| Grant scoped to abilities | `AdditionalActionGrant.ScopeAbilityIds` (appended last, passed by name; omitted from the saved state when unset) | `PerformerGrantMatches` (so `ConsumeAdditionalActionGrant` and `RequireRestrictedBurstAllows` both honour it); `RestrictedGrant` counts it as restricted |
| Preference | a matching ability-scoped grant is spent before a card-scoped or unscoped one | `ConsumeAdditionalActionGrant` ordering |
| Action-Phase Equip | `SpendEquipGrant(command, card)` | end of `Equip` |
| Equip bar | `HoldsOnlyAbilityScopedBurst(player)` | start of `Equip` |

A grant with `ScopeAbilityIds` is spent only by a `useAbility` command whose `AbilityId` is in the list. While a player holds nothing but restricted grants, an action outside every grant's scope is rejected with "The additional action must use one of the abilities this effect names." and the state does not change (the same shape as Koshin Keep's and Honor Never Falls' restrictions). A Reaction never spends or forfeits the burst, as before.

**Shared lines edited:** `Models.cs` (the grant record, one appended parameter), `CardAbilities.cs` (one `ThenByDescending` in `ConsumeAdditionalActionGrant`), `EmperorRemainingC.cs` (one term in `PerformerGrantMatches`), `EmperorStrongholdBatch1.cs` (`RestrictedGrant` and its message), `MatchEngine.cs` `Equip` (the bar at the top, the grant at the end).

## Action-Phase Equip

An Action-Phase Equip used to neither spend a grant nor forfeit one. It is now an action like any other there: it spends a grant that admits it and otherwise forfeits the held burst, and its player keeps priority while another grant is held. A grant admits an Equip when it is unscoped, or scoped (by card) to the equipped card. A grant scoped to abilities never admits an Equip, and while only such grants are held an Equip is rejected, as in a battle. When no grant is held, nothing is different. A battle Equip keeps its own code.

## Readings for Mendon to confirm or correct

- **An ordinary action forfeits an ability-scoped grant**, like every non-battle-bound grant (it is a burst grant, not a standing one). So "you may take an additional action to use X" is lost if the player does anything else, or passes.
- **Equip in the Action Phase now forfeits a held burst.** Before this change a held Action-Phase grant survived an Equip untouched (probably an oversight, since every other action forfeits it). If you want the old behaviour, delete the `RemoveAll` in `SpendEquipGrant`.
- **The Equip's entry window comes first.** As in a battle, when the equipped card opens an entry Reaction window the grant is not spent by that Equip (the window code returns before `SpendEquipGrant`).
- **Scope is by ability id only.** "His second printed ability" is `ScopeAbilityIds = [thatId]`; a copied or granted copy of the ability has a different id and is not admitted. A grant that must also name the performing card combines `ScopeAbilityIds` with `PerformerId`.

## Not done

- No card wired: Mirumoto Kalen - exp (a grant scoped to his second printed ability, Ranged 3, bow cost) and Yoritomo Harumi's second ability (her first line needs P19) are the wave's.
- No client change: the restriction is a rejection from the engine, which the client already shows as a disabled or rejected action.
