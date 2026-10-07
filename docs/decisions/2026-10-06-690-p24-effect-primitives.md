# P24: small effect primitives (issue #690)

Status: **built, engine only**. No card is wired, no content wave is published and no scan is added: the cards these pieces unlock are wired together in a later wave. Lane: rules. Source: primitive P24 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668), with the coordinator's overrides for this pass (no demonstration cards; fixtures only).

Code: `src/L5R.Core/EffectPrimitives.cs` (state, fixtures, registrations), `EffectPrimitivesMoves.cs`, `EffectPrimitivesAttack.cs`, `EffectPrimitivesCosts.cs`, `EffectPrimitivesTokens.cs`. Tests: `tests/L5R.Core.Tests/EffectPrimitivesMoveTests.cs` (`p24-moves`), `EffectPrimitivesAttackTests.cs` (`p24-attacks`), `EffectPrimitivesCostTests.cs` (`p24-costs`), `EffectPrimitivesZoneTests.cs` (`p24-zones-tokens`).

Every test drives a fixture ability whose id starts with `training-p24-` (granted to a training card by the test) or a per-instance printed value (`CardInstance.Printed`). No catalogue card prints one.

## What existed already, and what was added

| # | Piece | Already on main | Added here |
|---|---|---|---|
| 1 | Switch locations | A switch tied to Sudden Movement's and Spirit of the Shadows' two slots (`SwitchPlan` / `SwitchBlocked`, ShadowsEmbraceBatch4.cs), asking four of the negations a move can meet. The two target categories `personality-with-switch-partner` and `personality-same-controller-as-first-elsewhere`. | `SwitchLocations(a, b, sourceName)`: any two units, every negation an ordinary move asks. |
| 2 | Adjacent battlefield | `MoveSpec.AdjacentUnresolved`: an ability's declared move to an unresolved battlefield next to the current one, R-002 included (`AdjacentBattlefields`). **Not rebuilt.** | `AdjacentUnresolvedBattlefields(battlefield)` and `MoveToAdjacentBattlefield(card, destination)` for an effect that is not a declared move; the mover `MoveSpec.Wielder`. |
| 3 | Attack scope and comparison | A per-action permission to hit the Defender's home (No Hiding Place, a Reaction's record); per-ability compare switches (Nobunori, Samuru). | `AttackSpec` on `AbilityDefinition`: `TargetScope`, `CompareAgainst`, `CompareOptional`. |
| 4 | Shuffles | `ShuffleDiscardsIntoDecks` (The Sun's Aspiration) and the category `own-nonunique-strategy-in-discard`: all Sorrowful Prayer needs. The actor-only `CostVerbs.ShuffleHand`. | `ShuffleIntoDeck(cards, source)` (a named entry over the existing helper) and `ShuffleHandsAndRedraw(playerIds, played)`. |
| 5 | Cost substitution | Nothing. | The substitution store, the cost-waiver channel, the offered option. |
| 6 | Cost verbs | **Both verbs exist**: `CostVerbs.RemoveSlotCards` (Mining Apparatus) removes every card of a cost-only slot from the game; `CostVerbs.Sacrifice` with `CostSubject.Slot` destroys the chosen card (Tactical Sacrifice). **Not rebuilt.** | The three slot categories they lacked and builders that return the slot and the cost together. |
| 7 | Three-stat token | One- and two-stat tokens. | `CardToken.Stat3` / `Amount3`, `CreateMultiStatToken`. |
| 8 | Straighten-triggered control return | `ControlReturnTriggers` TurnEnds, SourceLeavesPlay, AbilityAnnounced. | `ControlReturnTriggers.SourceStraightens`, `ReturnControlWhenSourceStraightens`. |
| – | "Pay Gold ... +8 unless" (Kitsuki Judgment) | **Exists**: `GoldCostRule.TargetUnitTotal()` plus a P2 ability cost term read off the command's target. | Nothing; a fixture and a test show the recipe (`training-p24-judgment`). |

## The API a card-wiring wave calls

All members are `MatchEngine` internals unless a type is named.

### 1. Switch (Shinjo Kinto - exp2, Iuchi Karasu - inexperienced)

```csharp
all[KintoSwitch] = new("Battle", "...", "slots",
    Slots: [new("first", "personality-with-switch-partner", "A Personality"),
            new("second", "personality-same-controller-as-first-elsewhere", "Another Personality of the same player's, at a different location")]);
// in the effect case:
var a = SlotCards(command, "first").Single(); var b = SlotCards(command, "second").Single();
if (SwitchLocations(a, b, name)) { /* "After they move, straighten their units if you do not control them" */ }
```

`SwitchLocations` returns true only when both moved. It moves both units or neither: for each side in turn it asks every question `MoveHome`, `TryMoveToBattlefield` and `MoveToBattlefield` ask (in their order); the first side negated spends what negated it and nothing moves. A test pins that list of questions against the three move functions, so a negation added to one of them fails the suite until the switch asks it too.

### 2. Adjacent battlefield (Befriend the Tides, Unexpected Discovery, A Moving Battle)

```csharp
// Unexpected Discovery: Move: new(MoveSpec.Target, MoveSpec.AdjacentUnresolved)   -- as today
// Befriend the Tides (a Spell: "Move this Shugenja ..."):
all[Tides] = new("Battle", "...", "none", Costs: ActionCost.BowSource, Move: new(MoveSpec.Wielder, MoveSpec.AdjacentUnresolved));
// in the effect case: ExecuteMove(command, ability, source, target);
// A trait or follow-up that is not a declared move:
foreach (var destination in AdjacentUnresolvedBattlefields(battlefield)) ...; MoveToAdjacentBattlefield(card, destination);
```

### 3. AttackSpec (Strength in the Earth, Ikoma Satoru - exp; Chiyurei's Axe in part)

```csharp
all[StrengthInTheEarth] = new("Battle", "...", "attack", Performer: ..., Attack: new(TargetScope: AttackTargetScope.DefenderHome));
all[SomeLimitedAttack]  = new("Limited", "...", "attack", Attack: new(TargetScope: AttackTargetScope.AnyLocation));
all[PrintedCompare]     = new("Battle", "...", "attack", Attack: new(CompareAgainst: AttackCompare.PrintedForce));                        // always
all[MayPrintedCompare]  = new("Battle", "...", "attack", Attack: new(CompareAgainst: AttackCompare.PrintedForce, CompareOptional: true)); // the player's choice
```

`AttackSpec` is the last parameter of `AbilityDefinition` and is always passed by name (`Attack:`). The ability still gives its kind and strength through the `AttackKind` / `AttackStrength*` hooks and resolves with `ResolveOrDeferAttack`. With `CompareOptional` the engine offers one action per comparison and the command's `Choice` is `AttackSpec.PrintedForceChoice` or `AttackSpec.ForceChoice`.

### 4. Shuffles (Sorrowful Prayer, Overwhelming Chaos)

```csharp
// Sorrowful Prayer: Slots: [new("cards", "own-nonunique-strategy-in-discard", "...", 1, 3)], Costs: ActionCost.RemoveSource (its own "remove this card from the game")
ShuffleIntoDeck(SlotCards(command, "cards"), source!);        // returns how many went in
// Overwhelming Chaos:
ShuffleHandsAndRedraw([player.Id, 1 - player.Id], source);   // returns the cards each player shuffled in; `source` (the card being played) is left out
```

### 5. Cost substitution and waiver (Harmony; Koan's Jingasa, Koan's Robes)

```csharp
// Harmony: "Permanently give him the trait, 'You may bow this card instead of bowing a Ring to pay a cost.'"
GrantBowInsteadOfRing(Card(command.PerformerId!), Definition(source!).Name);           // GrantDuration.Permanent by default; GrantDuration.Turn also

// Koan's Jingasa ("need not bow for his Spells with an odd Focus Value"), in its feature file's module initializer; Koan's Robes passes odd: false:
EngineHooks.CostWaivers.Register("KoansJingasaWaiver", 15100, static context => context.Engine.SpellFocusParityWaives(context, "koans-jingasa", odd: true));
```

A player pays with the substitute by naming it on the command: `GameCommand.CostOption = CostSubstitution.Option(holderId)`. `QueryActions` offers that action (label `... · Bow <name> instead of the Ring`, with its `AlternativeCost` view) for every unbowed holder the acting player controls, on an ability whose source is a Ring in play with a bow cost of its own, or whose bowed card is named by a category containing "ring" (`own-unbowed-ring`). Nothing else changes for the client: it is one more offered option.

`EngineHooks.CostWaivers` is a predicate channel (`Func<CostWaiverContext, bool>`): true means the declared cost is not owed and its subject need not be able to pay it. It is asked from `CostExempt`, where "...unless he is Void" is already answered.

### 6. Cost cards (Koan's Whisper, Scorching Lash, Tamori Tsuchiya)

```csharp
var (koans, removeKoans) = EffectPrimitiveCosts.RemoveOwnCardsWithKeyword("Koan", 5);            // "remove five Koan cards you control from the game"
var (fuel, removeFuel)   = EffectPrimitiveCosts.RemoveFromAnyDiscard(exceptType: CardType.Ring);  // "remove a non-Ring card in any discard pile from the game"
var (spell, destroySpell) = EffectPrimitiveCosts.DestroyOwnAttachment(CardType.Spell);            // "destroy one of his Spells"
all[Lash] = new("BattleOrOpen", "...", "slots", Slots: [fuel, new("target", "personality", "A Personality")], Costs: [removeFuel]);
```

Each builder returns a cost-only `TargetSlot` and the `ActionCost` that spends the cards chosen for it. The categories are also available on their own: `EffectPrimitiveTargets.OwnCardInPlayWithKeyword(keyword)`, `.CardInAnyDiscard(keyword, exceptType, ownOnly)`, `.AttachmentOfPerformer(type)`. They are slot categories (a slot lists its candidates from every card in play and every discard pile); as the single `Target` of an ability the discard-pile one would validate but not be offered.

### 7. Three-stat token (A Doomseeker's Tomb)

```csharp
CreateMultiStatToken(target!, "Tomb", source!.Id, ("Force", 1), ("Chi", 1), ("PersonalHonor", 1));
```

One to three stats, each once. Pass Force first, then Chi, then anything else (see "Not done").

### 8. Control return on straightening (P'an Ku - exp)

```csharp
if (TransferUnitControl(target!, player.Id, name, checkLoyal: true, fromEffect: true))
    ReturnControlWhenSourceStraightens(target!, source!, $"{name}: after he straightens or leaves play");
```

## Shared files edited

One-line calls unless noted.

| File | Where | What |
|---|---|---|
| `CardAbilities.cs` | `AbilityDefinition` | the last parameter `AttackSpec? Attack = null` |
| | `StatModifier` | one term: tokens' `Stat3` |
| | `ResolveAttackNow` | `|| AttackSpecReaches(abilityId, target)` in the hit condition |
| | `ValidateAbility`, the out-of-play target chain | one `else if` for the discard-pile category |
| | `AbilityActions` (the offered-ability loop) | one line after `costOptions`: the substitution offers |
| `AttackTargeting.cs` | `RequireLegalAttackTarget` | `|| AttackSpecReaches(ability.Attack, target)` in the location rule |
| | `AttackComparedValue` | the spec's value first |
| `EmperorRulingR5.cs` | `AttackCompareOptions` | the fallback arm reads the spec |
| `AbilityCosts.cs` | `CostExempt`, `PlannedCostsOf`, `PayAbilityCosts` (the Bow case) | waiver; the substitute as the payer; the substitute bowed |
| `AlternativeCosts.cs` | `ChosenCostOption`, `ValidateCostOption` (two lines), `PayCostOption` (the Bow case) | a substitution rides in `CostOption` |
| `MoveDestinations.cs` | `MoveSpec`, `MovementPreview`, `ExecuteMove` | the mover `Wielder` |
| `TargetWindows.cs` | `ActionMovers` | the wielder is a mover |
| `EmperorMechB.cs` | `ControlReturnTriggers`, `ControlReturn`, `ApplyControlReturnsAfterCommand` | the constant; `SourceSeenBowed`; one predicate term |
| `Models.cs` | `CardToken` | `Stat3`, `Amount3` |
| `EngineHookStartup.cs` | `Build` | `_ = CostWaivers.Items;` |

The changed-file format check also required whitespace fixes that were already owed in two of those files: `EmperorMechB.cs` (the `RunEmperorMechB` switch's braced cases, re-indented) and `EmperorRulingR5.cs` (one `record with { ... }` initializer split over lines). No code changed in those lines.

State: `MatchState.EffectPrimitives` (`JsonPropertyOrder(22000)`), holding the substitution grants. Hook registrations use ids starting `P24` at priority 16000.

## Readings for Mendon to confirm or correct

**Switch**
1. A card in a unit stands for its unit; a switch always moves whole units.
2. Units of two players can be switched: each goes to where the other stands, and "home" is always the mover's own controller's home. Two units at one battlefield, or both at home, have nothing to switch: neither moves and the action's other effects go on.
3. The two sides are asked in turn. The first side whose movement is negated spends what negated it (a one-shot shield, a watch, a "first time each turn" trait); the other side is then not asked, so a negation it could also have spent is kept.
4. A **delay** armed on one side's movement stops the whole switch and nothing is recorded for later: a movement applied later would move one unit alone.
5. A switch may put a unit at a battlefield that has already resolved (wherever the other unit stood). "Will not move to each other's battlefields" and the like are judged as for an ordinary move, while the other unit still stands there.

**Adjacent battlefield**
6. "An adjacent battlefield" on Befriend the Tides and Unexpected Discovery (which do not print "unresolved") is read as an adjacent **unresolved** battlefield, the same as A Moving Battle's printed "adjacent, unresolved". If a unit should be able to move to an adjacent battlefield that has already resolved, that is a new destination, not this one.

**Attacks**
7. "The Defender's home" is the home of the battle's Defender (the non-active player). The scope adds it to the current enemy army; it never makes the acting player's own cards targets, so the Defender taking such an action gains no target from it.
8. `AnyLocation` reaches an enemy card at any home or any battlefield, resolved or not, in a battle or outside one. Every other rule of an Attack target still applies in both scopes: a Follower, or a Personality without Followers (or with an exception), and every "may not be targeted" bar.
9. The hit is judged where the target stands when the Attack resolves: a target that has left the scope by then (a home target moved to a battlefield that is not the current one) is not destroyed.
10. "Printed Force" (Chiyurei's Axe's "base Force") is the Force printed on the targeted card itself: no modifier, token, attachment or trait. A "may compare" is the acting player's choice, made with the action (ruling R5).

**Shuffles**
11. Overwhelming Chaos: the acting player first, then the other; each shuffles and draws before the next begins. The card being played is not part of the hand it shuffles. A draw that cannot be made (an empty deck, The Scorpion Wall is Finished) is not made up later.
12. Sorrowful Prayer: a chosen card that has left the discard pile before the action resolves is skipped; the others are still shuffled in.

**Cost substitution (Harmony)**
13. The trait replaces the bowing of **one** Ring per action: the action's single way to pay, else its first declared bow of a Ring.
14. The Ring must still be a Ring the action could bow: a bowed Ring still cannot take its own action (a bowed card's ability is not used), and "your target unbowed Ring" still needs an unbowed Ring. The trait changes who bows, not what may be chosen.
15. The substitute must be in play, unbowed and controlled by the acting player, and is then the card that pays: a negation of its bowing blocks the cost (the whole action is negated, nothing paid), it cannot also be bowed for the action's Gold, and it cannot be a card the same action already bows as another cost.
16. A Ring played from the hand bows nothing, so there is nothing to substitute.
17. "Permanently" keeps the trait with that card for the rest of the game, including if it leaves play and returns (the reading already used for Only a Shadow's permanent trait).

**Cost waiver (Koan's Jingasa, Koan's Robes)**
18. "He need not bow for his Spells" waives a Spell's **"Bow this Shugenja"** cost (the wielder's bow). A Spell's own `:bow:` bows the Spell, not him, and is untouched.
19. "Need not" is applied for the player: he is never bowed for such a Spell, and need not be unbowed to use it. (No choice to bow anyway is offered.)
20. The Focus Value judged is the Spell's current one (printed plus any modifier); 0 is even, as printed.

**Cost cards**
21. "Koan cards you control" are cards in play the acting player controls with the keyword, printed or granted. Cards in a hand or a discard pile are not controlled.
22. "Any discard pile" is either player's Fate or Dynasty discard pile, a dead Personality included.
23. "One of his Spells" is a Spell attached to the performing Personality (the chosen performer, else the source, else the source's wielder), other than the card the action is taken from.

**Tokens**
24. Each stat of a multi-stat token is negated, or delayed, on its own (as for the two-stat Poison token); a delayed stat is recorded as a one-stat token of its own.

**Control return**
25. "After P'an Ku straightens or leaves play" is judged after each command, so the return happens when the command in which he straightened has finished, not in the middle of it. It needs an actual straightening: a source that was never bowed after the return was registered has not straightened. Like every return of control it is never negated, and one blocked by Unique/Singular waits.

## Not done, and why

- **Chiyurei's Axe is not fully served.** "Melee Attacks this Personality performs may be compared against the target's base Force" is a trait of the performer, on any Melee Attack he performs. `AttackSpec` belongs to one ability. The comparison itself is built (`AttackCompare.PrintedForce`), but the player's choice for a performer trait needs `AttackCompareOptions` to know the performer: it is a static function of the ability id, read in three places (`ValidateAbility`'s Choice gate, the offered-ability loop's `compareChoices` and its label, both in `CardAbilities.cs`, and `ValidateAttackCompareChoice` in `EmperorRulingR5.cs`). That is more than one-line calls, in the region sibling P4 is editing, so it was left.
- **Ikoma Satoru - exp** needs another player's decision before his Attack is made; `AttackTargetScope.AnyLocation` gives that Attack its reach, the decision step is not this primitive's.
- **Koan's Whisper's removal is optional and conditional** ("If its player has two or more Provinces, you may remove five Koan cards ..."). The builder makes an up-front cost of exactly N; the wave decides whether that is a second mode of the action or a slot of 0 or 5.
- **The existing switch is not migrated.** Sudden Movement and Spirit of the Shadows still use `SwitchPlan` / `SwitchBlocked`, which ask four of the negations. Moving them to `SwitchLocations` is a two-line change in `CardAbilities.cs` and `EmperorP2.cs` and would change two published cards, so it is left for a decision.
- **The offered default of a two-slot switch can be an illegal pair.** With three or more candidates the default selection takes the first two, which may stand at one location; the option is then listed disabled although legal pairs exist (seen in a rig; it affects Sudden Movement today). Kinto and Karasu need a default-selection rule like `TfStrategiesBDefaultSelection`.
- **The Harmony trait is not shown on the card.** `CardView.GrantedEffects` is built in `MatchQueries.cs`; one `.Concat(...)` there would show it. Left out to stay clear of the view code.
- **The client shows one stat per token.** `CardTokenRail` reads `Stat` / `Amount` only, as it already does for two-stat tokens; `Stat3` / `Amount3` are additive and optional.
- **Force and Chi penalty strippers know two slots.** Tireless Efforts' and Gift Armor's clean-ups (SeedsOfDecayBatch4.cs, TfStrategiesA.cs, EmperorRemainingG.cs) read `Stat` and `Stat2`. A token built with Force and Chi first is handled as before; a penalty placed in the third slot would not be stripped. No card needs one.
- **Removing an attached card as a cost does not re-check its host's Chi**, and removing a Personality would leave his attachments (existing behaviour of `RemoveSlotCards`; `CostVerbs.RemoveUnit` is the verb for a Personality). The Koan cards are Items and a Spell.
- **In-play cost categories obey the Battle rule of location**: a Battle action cannot name a card at home with `OwnCardInPlayWithKeyword`. Koan's Whisper is Limited.
- **A substitution cannot ride with a choice of cost option** (an ability with two or more `CostOptions`), nor replace a Ring bowed inside an effect case rather than as a declared cost (`AbilityDefinition.Costs` / `CostOptions`). The Rings' own actions and Chukage's "Bow your target Ring" declare theirs; other abilities were not surveyed.
- The Straighten Phase's straightening is caught by the same after-command check as any other; it has no test of its own.
- Native client checks are not required: nothing here touches the client.
