# P4: the turn boundary (issue #690)

Status: **built, engine only** (no card wired, no scan, no art pack, no content wave: the cards this unlocks are wired together in a later wave). Lane: rules. Source: primitive P4 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668), as narrowed by the task for #690. Code: `src/L5R.Core/TurnBoundary.cs`, `TurnBoundaryGrants.cs`, `TurnBoundaryRules.cs`; tests: `tests/L5R.Core.Tests/TurnBoundaryTests.cs` (suite `turn-boundary`).

## What it is

1. **`EngineHooks.TurnBegins`**: the one place "after your turn begins", "after each player's turn begins" and "after another player's turn begins" traits resolve. It can stop for a decision and resume, including from a saved game.
2. **`TurnUntil`**: one vocabulary for durations that end, and effects that come due, as a turn begins: your next turn, your second turn from now, the next turn of anyone, the game's end. It drives the delay store, timed removal, an Event staying in play, player grants and standing rules.
3. **Player-level granted abilities**: "you have / each player has / his controller has the ability ...", for a duration, offered through the normal action list to that player only.
4. **Turn-scoped standing rules**: a store of rules that last for turns, with two kinds the engine consults itself (an end-of-turn extra draw conditional on a per-turn counter; a Limited action taken as an Open action) and any number of kinds a feature reads from its own code.

## Order as a turn begins

`StartTurn` runs as before up to and including `AdvanceStraightenHolds(TurnBegins)`, then calls `RunTurnBegins`, then the Straighten Phase:

1. what lasted "until that turn begins" ends: player grants, standing rules (`ApplyTurnBoundaryExpiry`);
2. the delayed effects waiting for that turn are applied, in the order they were recorded;
3. the `TurnBegins` callbacks run in hook priority order (then registration id), each walking its own cards in card id order;
4. the Straighten Phase begins.

Events in play (`ExpiresAtTurnsTaken`) and timed removals keep the place they already had earlier in `StartTurn`.

## The API a card-wiring agent calls

All members are on `MatchEngine` (internal) unless noted. Hook priorities are at least 10000 and unique to the feature, as for every other channel.

### 1. A turn-begin trait

```csharp
// "After each player's turn begins, straighten this Holding." (the shape of Abundant Farmlands)
EngineHooks.TurnBegins.Register("AbundantFarmlandsStraighten", 12500, static (engine, turn) =>
{
    foreach (var card in engine.TurnBeginCards("abundant-farmlands", turn, TurnBeginScope.EachPlayers)) engine.Straighten(card);
    return false; // nothing pending
});
```

`TurnBeginsContext(PlayerId, TurnNumber, TurnsTaken)` is the turn that is beginning. `TurnBeginScope` is `Yours`, `EachPlayers` or `AnotherPlayers`; `turn.Fires(scope, controllerId)` answers it for a rule that is not a card in play. `TurnBeginCards(effect, turn, scope)` returns the in-play cards carrying that catalogue Effect whose controller matches the scope, in card id order.

**A trait that needs a decision** opens the generic turn-begin choice and returns its result; a resolver registered on `EngineHooks.TurnBeginChoices` applies the answer:

```csharp
EngineHooks.TurnBegins.Register("ExampleChoose", 12501, static (engine, turn) =>
{
    foreach (var card in engine.TurnBeginCards("example-effect", turn, TurnBeginScope.Yours))
    {
        if (engine.PeriodCountOf("example-asked:" + card.Id, Period.Turn) > 0) continue; // asked already this turn
        engine.AddPeriodCount("example-asked:" + card.Id, Period.Turn);
        return engine.OpenTurnBeginChoice("example-choice", turn.PlayerId, card.Id, "Example: choose a player.",
            [new("me", "You", PlayerId: turn.PlayerId), new("them", "Your opponent", PlayerId: 1 - turn.PlayerId)]);
    }
    return false;
});
EngineHooks.TurnBeginChoices.Register("ExampleChooseResolve", 12501, static (engine, answer) =>
{
    if (answer.Key != "example-choice") return null;   // not mine
    /* apply answer.Option (Id, CardId, PlayerId) for answer.SourceCardId */
    return true;                                        // true: run my callback again (more of my cards); false: go on with the next callback
});
```

`OpenTurnBeginChoice` returns false (and opens nothing) when there is no option. The resolver may open a further choice with `OpenTurnBeginChoice`; the sequence then stays where it is. A callback that opens a pending choice of its own type instead (its own `EngineHooks.PendingChoice` handler) returns true and calls `engine.ResumeTurnBegins()` (or `ResumeTurnBegins(repeat: true)`) once its choice is cleared.

**Client and bot contract** (engine side only; nothing in the client was changed): `PendingChoice.Type` is `turnBeginsChoice` (`TurnBoundary.ChoiceType`), its `PlayerId` the chooser and `CardId` the source card. `QueryActions` offers the chooser one enabled option per answer of type `chooseTurnBegins` (`TurnBoundary.ChooseCommand`) whose command carries `Choice` (the option id) and, when the option names one, `TargetId` / `TargetPlayerId`; the other player is told to wait. The training bot answers an unknown decision with its first complete option, so it does not stall.

### 2. Durations

```csharp
var next   = engine.UntilOwnersNextTurnBegins(player.Id);      // "until your next turn begins"
var second = engine.UntilOwnersSecondTurnBegins(player.Id);    // "until your second turn from now begins"
var nth    = engine.UntilOwnersTurnBegins(player.Id, skip: 2); // "three turns from now"
var any    = engine.UntilTurnBegins();                         // "until the next turn begins", whoever's
var game   = TurnUntil.GameEnds();                             // "until the game ends"
engine.UntilPassed(next); engine.DescribeUntil(next);
```

`Owner` and `Skip` mean what they mean for a `StraightenHold` (Owner null: the turn in progress, whoever's; Skip: how many such turn beginnings go by first). They are stored as the absolute turn they name (`TurnUntil.At`: that player's `TurnsTaken`, or the game's `TurnNumber` when there is no owner), the way `TimedRemoval.AtTurnsTaken` and `CardInstance.ExpiresAtTurnsTaken` already are.

| Use | Call |
|---|---|
| Delay store ("delay ... until after your next turn begins") | pass `engine.DelayUntilOwnersNextTurnBegins(owner)`, `DelayUntilOwnersSecondTurnBegins(owner)` or `DelayUntilTurn(until)` wherever a `DelayUntil` constant goes (`ArmDelayForHeldAction`, `WithDelayScope`, `ScheduleDelayedEffect`) |
| Timed removal of a created card | `engine.ScheduleTimedRemovalUntil(card, until, sourceName)` (the point must name an owner) |
| An Event staying in play | `engine.SetEventExpiry(eventCard, until)` (the owner must be the Event's owner) |
| A player grant, a standing rule | the `until` argument of `GrantPlayerAbility` / `AddTurnScopedRule` |

"Until the game ends" schedules no removal, clears an Event's expiry, and, in the delay store (`DelayUntil.GameEnd`), records an effect that is never applied.

### 3. Player-level granted abilities

```csharp
// The ability: an ordinary catalogue ability with PlayerAbility: true (no source card; its uses are the player's), in the feature's Abilities callback ...
all["empire-of-madness-seppuku"] = new("Open", "Your target Personality with a Madness token commits Seppuku", "own-personality-with-token", PlayerAbility: true);
// ... registered as granted-only, in the module initializer:
EngineHooks.PlayerGrantAbilities.Register("EmpireOfMadnessGrantIds", 12502, static () => ["empire-of-madness-seppuku"]);
// When the Event resolves:
engine.GrantEachPlayerAbility("empire-of-madness-seppuku", TurnUntil.GameEnds(), sourceName);
// One player, for a duration, or while a card stays in play (Bonds of Hospitality: "his controller has the ability ..."):
engine.GrantPlayerAbility(playerId, abilityId, engine.UntilOwnersNextTurnBegins(playerId), sourceName);
engine.GrantPlayerAbility(controllerId, abilityId, TurnUntil.GameEnds(), sourceName, whileInPlay: bondsCard.Id);
engine.RevokePlayerAbility(playerId, abilityId, sourceName); engine.PlayerAbilityGranted(playerId, abilityId);
```

A registered id is dormant (not offered, and refused with "You do not have that ability.") for a player with no live grant. Grants live in `MatchState.PlayerAbilityGrants`, keyed by player: never on the Stronghold card. The same source granting again replaces its own grant; different sources stand side by side. The ability's effect is written like any other ability's (`EngineHooks.RunEffects`).

### 4. Turn-scoped standing rules

```csharp
// An End to Hostilities: "Until the game ends, each player draws two cards instead of one at the end of his turn if he put no other cards in his hand that turn."
engine.AddTurnScopedRule(TurnRuleKinds.EndTurnExtraDraw, playerId: null, TurnUntil.GameEnds(), sourceName, amount: 1, counter: TurnCounters.CardsIntoHand, max: 0);
// The Madness Spreads: "Until your second turn from now begins, once per turn, you may take a Limited action on a Strategy as an Open action."
engine.AddTurnScopedRule(TurnRuleKinds.LimitedAsOpen, player.Id, engine.UntilOwnersSecondTurnBegins(player.Id), sourceName, amount: 1, arg: "Strategy");
// A kind of the feature's own (Suck the Marrow: "Until the game ends, after each turn begins, give the target a -1F/-1C Poison token"):
engine.AddTurnScopedRule("suck-the-marrow", null, TurnUntil.GameEnds(), sourceName, cardId: target.Id);
// ... read from its TurnBegins callback:  foreach (var rule in engine.TurnScopedRulesFor("suck-the-marrow")) { ... rule.CardId ... }
```

`AddTurnScopedRule(kind, playerId, until, source, amount, counter, max, arg, cardId, whileInPlay)` returns the record; `playerId: null` is "each player". `TurnScopedRulesFor(kind, playerId)` lists the live ones for a player (or all), `RemoveTurnScopedRule(id)` ends one. The store does the expiry.

| Kind | The one choke point | Meaning |
|---|---|---|
| `EndTurnExtraDraw` | `EndTurn`, just before the End Phase's own `Draw(player)` | the player whose turn is ending draws `Amount` more, if `TurnCountOf(Counter, player) <= Max` at that moment (no `Counter`: always) |
| `LimitedAsOpen` | the Limited timing check (`ValidateAbility` and the offered list in `AbilityActions`) | `Amount` times per turn the player may take a Limited ability in an Action Phase on a turn that is not his; `Arg` narrows it to the source card's type |

Per-turn counters are P7's period counters under `p4:<counter>:<player>`: `TurnCountOf(counter, playerId)` / `AddTurnCount(counter, playerId)`. `TurnCounters.CardsIntoHand` is bumped by `NoteCardIntoHand` (every draw, search and return to hand that goes through it).

## Shared lines edited

| File | Function | Edit |
|---|---|---|
| `MatchEngine.cs` | `StartTurn` | one line: `if (RunTurnBegins(player)) return;` after `AdvanceStraightenHolds(TurnBegins)` |
| `MatchEngine.cs` | `EndTurn` | one line: `DrawTurnRuleEndTurnCards(player);` before the End Phase's `Draw(player)` |
| `CardAbilities.cs` | `ValidateAbility` (the `"Limited"` timing arm) | `|| TurnRuleLimitedAsOpen(command.PlayerId, command.CardId)` |
| `CardAbilities.cs` | `AbilityActions` | `|| PlayerGrantDormant(id, playerId)` in the dormant-player-ability line; `&& !TurnRuleLimitedAsOpen(playerId, source?.Id)` in the Limited line |
| `CardAbilities.cs` | `UseAbility` | one line: `NoteTurnRuleAnnouncement(command);` |
| `MatchQueries.cs` | `QueryActions` | one `else if` arm for `turnBeginsChoice` |
| `EmperorPersonalityBatch5.cs` | `NoteCardIntoHand` | one line: `AddTurnCount(TurnCounters.CardsIntoHand, playerId);` |
| `EmperorRulingR2.cs` | `DelayUntil` | two constants (`GameEnd`, `TurnBeginsPrefix`) and the fallback arm of `Describe` |
| `EngineHookStartup.cs` | `Build` | three lines (`TurnBegins`, `TurnBeginChoices`, `PlayerGrantAbilities`) |

`Models.cs` and `AbilityGrants.cs` are untouched: the grants are a `MatchState` property in `TurnBoundaryGrants.cs`, not a `PlayerState` one (that type has no explicit property orders and is not partial), and no parameter was added to `AbilityDefinition`.

State: `MatchState.PendingTurnBegins` (22100, registered as a decision state), `PlayerAbilityGrants` (22101), `TurnScopedRules` (22102), `TurnScopedRuleSequence` (22103). Registrations: `P4HandleTurnBeginChoice`, `P4ValidatePlayerGrant`, both priority 16100.

## Tests

`TurnBoundaryTests` (suite `turn-boundary`, twelve tests): the three scopes of a turn-begin trait, their timing and order; a choice that pauses, refuses a wrong answer, asks again and resumes; a copied state and a command replay taken while the choice is pending; the durations; the delay store; timed removal and an Event's expiry; a granted player ability's whole life, "each player" and "while in play"; the end-of-turn draw rule; the Limited-as-Open rule (with Plans Within Plans); the stores' round trip and the registry. The fixture callbacks and the fixture ability (`training-p4-*`) are registered by the test assembly and are inert in every other suite.

## Which unwired cards each piece is for

| Piece | Cards |
|---|---|
| `TurnBegins` | Abundant Farmlands (each player's turn), Doji Tatsuki - exp (another player's turn), Dagger of Crystal Wind (your turn), Suck the Marrow (each turn, with a standing rule holding the target) |
| "Until your next turn begins" | Invocation, Rampant Paranoia, Unseasonable Weather |
| "Until your second turn from now begins" | The Madness Spreads, Rejected Mediation, Open Recruitment |
| "Until the game ends" | An End to Hostilities, An Empire of Madness, Harbingers of War, The Heavens' Blessing, Suck the Marrow |
| Player grants | An Empire of Madness ("each player has the ability"), Bonds of Hospitality (`whileInPlay`) |
| `EndTurnExtraDraw` | An End to Hostilities |
| `LimitedAsOpen` | The Madness Spreads |
| A standing rule of the card's own kind | Suck the Marrow, Invocation (with P14), Rampant Paranoia (with P12), Rejected Mediation (with P7), Harbingers of War (with P19), Open Recruitment (with P5/P6), Unseasonable Weather (with P6), An Empire of Madness's loss condition |

Drain of Effort and Uncovering Treasures are on the plan's P4 list, but I found no turn boundary in their printed text. Drain of Effort's rule ends with the phase or after two discards: it can keep its record in this store and end it itself (`RemoveTurnScopedRule`), but its "after each action other than this one resolves" trigger is not built here. Uncovering Treasures ("Put this Strategy into play"; "Produce 2 Gold. Discard this Strategy from play") uses nothing of P4 that I can see.

## Left out, and why

- **The Heavens' Blessing's surcharge** ("a player must pay 2 Gold to draw a card from effects of his own Strategies during an Action Phase"). `Draw(player)` is called synchronously from the middle of several hundred effect sites, each of which carries on after it; a payment there is a decision in the middle of an effect. It needs either (a) the throw-and-replay mechanism `Execute` uses for Ten Thousand Temples (`HonorLossDecisionNeeded`: discard what the command did, ask, replay the command with the recorded answer) generalised to a "draw payment" decision, which is a block in `MatchEngine.Execute` plus a pending decision type, a `QueryActions` arm and a Gold-payment command, or (b) a per-ability declaration of how many cards a Strategy's effect draws so that the payment can be asked as the action is announced. Neither is a one-line call. The duration ("until the game ends") and the store are ready for it: the rule would be one more `TurnRuleKinds` constant.
- **No card is wired**, and no demonstration card: every piece is proven on fixture callbacks and per-instance definitions.
- **No view field** lists a player's granted abilities or the standing rules; the offered actions and the log events (`playerAbilityGranted`, `playerAbilityExpired`, `turnRuleSet`, `turnRuleEnded`, `turnRuleDraw`, `turnRuleUsed`) are what a client sees. A placard is client-lane work.
- **A granted player ability that is a Reaction** is offered only in the windows that list abilities through `AbilityActions`; the trigger scans that walk cards (`AbilityIdsOf`) do not see player abilities, granted or not. No card on the list needs one.
- **No "turn ends" hook.** An Empire of Madness's "any player who ends his turn with 20 or more Madness tokens loses the game" needs a call in `EndTurn` of its own.
- **A timed removal for "the next turn, whoever's"** is refused (`TimedRemoval` names a player); no card needs it.
- Native client checks were not run: nothing here touches the client.

## Readings for Mendon to confirm or correct

1. **"After your turn begins" resolves before the Straighten Phase.** The callbacks run after the turn-start bookkeeping and after the straighten holds that wait for "your turn begins" are released, and before anything straightens: a card of the turn's player is still bowed when its trait resolves. (So Doji Tatsuki straightens on the other player's turn and is then straight through it; Abundant Farmlands straightens on both.) The engine's phase marker still reads the previous turn's End Phase until the Straighten Phase begins.
2. **What ends "when your next turn begins" ends before the "after your turn begins" traits**, and the effects delayed until then are applied before them too, in the order they were recorded.
3. **Order between several turn-begin traits**: a fixed order (the hook priority of the card's feature, then card id), not chosen by the turn's player. The existing hard-coded turn-start effects (Losing the Way, Bayushi's Guidance, Blessings of the Shi-Tien Yen-Wang, Bayushi Rentatsu) keep their places before and after the hook.
4. **"Your next turn" said during your own turn is your following turn**; "your second turn from now" is two of your turn beginnings away, whoever's turn it is now (the count `ExpiresAtTurnsTaken` already used).
5. **A turn-begin choice stops everything**: only the chooser may answer; no action, Reaction or Seppuku is possible until he has.
6. **An End to Hostilities' count.** "No other cards in his hand that turn" is judged just before the End Phase's own draw: that draw is not an "other" card, a draw made "after the End Phase begins" (Eternal Victory Dojo) is, and the opening hand is not counted as cards put in a hand on the first turn. "Two instead of one" is one more card; two such rules would each add theirs.
7. **"A Limited action as an Open action"** means: in an Action Phase on a turn that is not his own, at an ordinary opportunity (nothing pending). The turn's own player needs no rule and spends no use. The use is spent as the action is announced, so a negated action has used it. If Plans Within Plans is armed at that opportunity, the action is Plans Within Plans' and the rule's use is kept.
8. **A player-level granted ability is offered in the published profile only**, as every player ability is (the scenario-only Strongholds have none); it uses the player's own use counters.
9. **"While this card remains in play, his controller has the ability"** ends the moment the card leaves play (it is then neither offered nor accepted); its record is cleared as the next turn begins.
10. **An effect delayed "until the game ends"** is never applied.
