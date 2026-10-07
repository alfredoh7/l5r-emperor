# P25: decision infrastructure (issue #723)

Status: **built, engine, bot and client, no card wired** (Poison the Cup and Now We Are Enemies stay unwired; no packager, wave, scan, art pack, inventory change or blocker removal). Lane: rules/shared. Source: primitive P25 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/DecisionInfra.cs` (the feature), `DecisionInfraShowcase.cs` (review positions for the native probe), `TargetSlots.cs`, `EitherOr.cs`, `EffectChoice.cs`; client `Main.ShownChoice.cs`, `Main.Decisions.cs`, `Main.DecisionInfraCapture.cs`; tests `tests/L5R.Core.Tests/DecisionInfraTests.cs` (suite `decision-infra`) and `BotPolicyTests.DecisionInfraBot`.

## What it is

Four pieces that remove the need to edit shared files for every new kind of decision. Everything is **unused by default**: no new saved field exists until it is used (`PendingEitherOr.Min/Max/Picked` and `MatchState.PendingShownChoice` are omitted while default or null), no existing hook order or option list changes, and the full Core suite passed unchanged.

### 1. Multi-pick Either/Or

`PendingEitherOr.Min/Max` (Max 0 is the ordinary single pick). Open one with the two new optional parameters of `OpenEitherOr`:

```csharp
engine.OpenEitherOr(PoisonTheCupSpec, source, actingPlayerId, deciderId, target, "action", bowedBefore, min: 1, max: 1);
```

The decider is offered the options **not yet picked** plus a synthetic **Done** option (key `done`, label `Done (n chosen)`, legal once `Min` are picked, otherwise listed disabled with "Choose at least n."). Each pick is an ordinary `chooseEitherOr` with `Choice = key`, so the existing Either/Or client screen renders it unchanged (verified natively). The picks are kept (`PendingEitherOr.Picked`) and **applied together, in the order picked, when the decision ends**: on Done, when `Max` is reached, or when no legal option is left. Each pick goes through the spec's own `EngineHooks.ApplyEitherOr` hook as usual; then **one more call with the synthetic option `done`** lets a spec act on the whole set (or on an empty one: `p.Picked`). That final call is made through the hooks only; a built-in spec never sees it. `done` is reserved in a multi-pick decision. When every legal option is needed to reach `Min` (or none is legal), nothing is asked: the picks are made, applied, announced (`eitherOrAutoResolved`) and `OpenEitherOr` returns false, as for a single forced option. Options that cost Gold are listed disabled in a multi-pick decision ("cannot be combined").

### 2. `EngineHooks.PendingActions` and the two EffectChoice hooks

```csharp
// a feature file, in its metadata-only module initializer (ids start with the feature, priorities at least 10000):
EngineHooks.PendingActions.Register("PoisonCupActions", 16800, static (engine, context) =>
    context.Choice.Type != "poisonCupTokens" ? null                                   // not mine: null falls through to the ordinary chain
    : context.PlayerId != context.Choice.PlayerId ? []                                // mine, but this player has no decision: an EMPTY sequence
    : engine.PoisonCupOptions(context.PlayerId));                                     // IEnumerable<ActionOption>, each a complete command
EngineHooks.PendingChoice.Register("PoisonCupAnswer", 16800, static (engine, type, command) => engine.HandlePoisonCup(type, command)); // the answer (already existed)
EngineHooks.DecisionState.Register(nameof(MatchState.PendingPoisonCup), 16800, static state => state.PendingPoisonCup is not null);   // already required for a state property
EngineHooks.BranchEffects.Register("PoisonCupBranch", 16800, static (engine, effect, command, source, target) => effect == "poison-cup-remove" && engine.RemoveChosenToken(command, source, target));
EngineHooks.EffectChoiceContinuations.Register("PoisonCupNext", 16800, static (engine, pending, option) => option.Id == "tokens" && engine.OpenPoisonCupTokens(pending)); // true: opened a further decision
```

- `PendingActions` is `Func<MatchEngine, PendingActionsContext, IEnumerable<ActionOption>?>`; `PendingActionsContext(PlayerId, Choice)`. `QueryActions` consults it **once, before its chain**, whenever a `PendingChoice` is open; the first non-null answer is the whole list for that player (the chain and the phase actions are skipped; Concede and the usual option filters still apply). A non-decider must be answered with an empty sequence, or the phase actions of the Action Phase would be listed for them.
- `BranchEffects`: `Func<MatchEngine, string, GameCommand, CardInstance, CardInstance?, bool>`, called by `ApplyBranchEffect` after every built-in effect and before its closing `switch`; return true when the effect string is yours.
- `EffectChoiceContinuations`: `Func<MatchEngine, PendingEffectChoice, EffectOption, bool>`, called by `ResolveEffectChoice` after the chosen effects applied and after the two existing continuations; return true when the option opened a further decision (the action then finishes with that decision, not here).
- Helpers for a feature's pending type: `engine.OpenGenericChoice(type, deciderId, cardId, prompt)` opens it, `engine.ResumeAfterDecision(playerId)` closes it and gives the action opportunity on, `MatchEngine.RefuseCommand(message)` refuses the command being answered (state unchanged; the engine already rolls back on any refusal).
- **Client:** `GenericDecisionActive` (`Main.Decisions.cs`) is now the old allowlist **or** "a pending type not in `KnownPendingTypes` for which the engine lists options (or that is another player's, to show the waiting panel)". `KnownPendingTypes` is the snapshot of every pending type that existed before P25, so each keeps the screen it had; a type a feature registers later is unknown by construction and is shown as engine-listed buttons, one selected, then "Confirm choice". No client edit is needed for a new decision kind.

### 3. `TargetSlot.CrossConstraint`

```csharp
new TargetSlot("second", "personality", "The second Personality")
{
    CrossConstraint = static (engine, cards) => cards.Select(c => CardCatalog.Get(c.DefinitionId).Clan).Distinct().Count() == cards.Count,
    CrossConstraintReason = "The two Personalities may not share a Clan alignment."
}
```

The predicate gets the cards chosen for **this slot and every earlier card slot, in order** (so it must tolerate a set smaller than the final one) and runs in `ValidateSlots` after all per-slot checks; a false answer refuses the whole command with `CrossConstraintReason`, and a refusal leaves the state unchanged. The **offer** reflects it: a candidate for the slot is offered while some legal earlier selection satisfies the predicate, so with `ProbeDefaultOnlyForLaterSlots` on the earlier slot the second slot lists exactly the candidates that fit its default; and the **default command** `QueryActions` offers is a pair that satisfies it, falling back to the first earlier alternative that has a partner when the default earlier choice has none (otherwise a bot or any legal-actions-driven client would see a disabled ability). It lives on the ability definition (code), never in the saved game. Mutual Support's existing aggregate check (`TfStrategiesB.cs`) is unchanged.

### 4. A decider's choice among shown cards

`MatchState.PendingShownChoice` (`JsonPropertyOrder(22700)`, omitted when null, registered as a decision state), `PendingChoice.Type == "shownChoice"`. Candidates are **positions in `MatchState.ShownCards`** (`ShownCard.Sequence`), never instance ids (the cards may still be in a deck): the pending state holds only sequence numbers.

```csharp
// P20's "Dark Audience: the other player chooses one": the searcher showed two attachments (they are in `MatchState.ShownCards`), the other player decides which is removed.
engine.OpenShownChoice("dark-audience", source, actingPlayerId, deciderId, shownSequences, min: 1, max: 1, "Dark Audience: choose one to remove.", paused: pausedEffectChoice);
EngineHooks.ShownChoiceApply.Register("DarkAudienceApply", 16800, static (engine, p) => p.SpecId == "dark-audience" && engine.ApplyDarkAudience(p)); // p.Chosen = sequences, in order chosen
```

The decider is offered one `chooseShown` action per candidate (`Choose · <definition name>`, `Command.Choice` = the position) and, for `Max > 1` or `Min == 0`, a `done`; the other player sees the same `PendingChoice` and waits. The choice ends at `Max`, on done, or when every candidate is chosen. With nothing to choose (no candidate, or all are needed) it resolves itself, announced, and `OpenShownChoice` returns false (the caller finishes its action). `paused` (an `PendingEffectChoice`, as `PendingDeciderChoice.Paused`) makes the engine finish the paused action after the apply hook unless the hook opened a further decision; without it the hook finishes things itself. The **client** (`Main.ShownChoice.cs`) draws each candidate's printed face from `MatchView.ShownCards` beside its option; selecting is a draft until "Confirm choice".

## Shared lines edited (all else is in the new feature files)

| File | Edit |
|---|---|
| `EitherOr.cs` | `using System.Text.Json.Serialization`; `PendingEitherOr.Min/Max/Picked` (`WhenWritingDefault`/`WhenWritingNull`); `OpenEitherOr` gets `int min = 0, int max = 0` and sets them; one line `if (max > 0) { if (AutoResolveMultiPick(...)) return false; }`; the prompt gets `+ MultiPickPromptSuffix(pending)`; `ResolveEitherOrCommand` gets one line routing a multi-pick decision to `ResolveMultiPickCommand`; `BuildEitherOrView` and `EitherOrActions` call `EitherOrOffered(p)` instead of `EitherOrOptions(p)` (identical for a single-pick decision) |
| `MatchQueries.cs` | `QueryActions`: `var registeredPending = RegisteredPendingActions(playerId);` and the chain's first `if` becomes `if (registeredPending is not null) result.AddRange(registeredPending); else if (...)` |
| `EffectChoice.cs` | one call in `ApplyBranchEffect` (before the closing `switch`), one in `ResolveEffectChoice` (after the P4 continuation) |
| `TargetSlots.cs` | `TargetSlot.CrossConstraint` and `.CrossConstraintReason`; a block in `ValidateSlots` before the Dislodge check; the candidate loop and the default selection in `BuildSlotOffer` |
| `EngineHookStartup.cs` | four `_ = X.Items;` lines |
| `DecisionShowcase.cs` | one line routing `p25-*` kinds to `DecisionInfraShowcase.cs` |
| `DeciderChoice.cs` | **not edited** (the shown choice is its own feature; decider choice already had Min/Max) |
| Client `Main.Decisions.cs` | `GenericDecisionActive`, `KnownPendingTypes`, `UnknownPendingDecision`, one `shownChoice` line; `Main.DecisionCapture.cs` one probe line |
| Client `Main.EitherOr.cs` | **not edited**: the Done option is an ordinary listed option |
| Bot `TrainingOpponent.cs` | `"done" => 61` in the `chooseEitherOr` scores: a multi-pick decision takes the fewest picks it must (Done is offered only once `Min` are made), never stalls; a registered pending type and `chooseShown` take the default first-complete-option score |
| `tools/check-decision-chooser.sh` | the `1280x800 p25` mode |

Hook ids and priorities: ids start with `P25`, priorities 16700 (the feature) and 16701 (the showcase). State: `MatchState.PendingShownChoice`, `JsonPropertyOrder(22700)` (22700-22799 is reserved for P25).

## Readings for Mendon to confirm or correct

- **A multi-pick Either/Or applies its picks together at the end, in the order picked**, not one at a time as they are chosen. Nothing observable between picks changes (the offered list is only reduced), so a "choose one then another" card cannot see the first effect before the second pick. Poison the Cup's single token pick is `min: 1, max: 1`; a card whose second pick must read the first's effect needs separate decisions (the Topaz Armor precedent).
- **A candidate for a cross-constrained slot is offered while some legal earlier selection admits it** (the client cannot re-query as the first slot is chosen), and the final pair is validated on submission. A viewer that wants only the candidates compatible with its current first pick must filter locally or mark the earlier slot `ProbeDefaultOnlyForLaterSlots`.
- **Done is offered as a decision option, not a separate command**, and an empty set (Min 0, Done at once) still triggers the spec's final `done` call.
- **The generic client screen applies to any pending type the client does not know**, including types whose actions are all disabled for the viewer (the waiting panel is shown for the non-decider). The snapshot list `KnownPendingTypes` was compiled from every pending type literal in `L5R.Core` and `L5R.Client`; a pre-P25 type that is built from a variable (none was found) would now be shown by the generic screen.
- **The Shown cards history panel opens first for the recipient of a new show** (existing behaviour, `ObserveShownCards`), so a decider asked about a card just shown sees the history with "Minimize to return to your decision"; the decision's own screen then draws the same faces beside the options.

## Not done

- No card is wired: Poison the Cup and Now We Are Enemies, and the P20 "other player chooses one" follow-up (Dark Audience) remain for their own waves. This PR proves each piece with test-only fixtures (specs, abilities and a pending type keyed on `training-p25-*`, registered by the test assembly's module initializer so no other suite sees them) and with the isolated `p25-*` showcase positions for the native probe.
- The honor-change replay facts (`PausedActionFacts`, `EmperorRulingR4Honor.cs`) have no case for `shownChoice`, as for most feature-owned pending types; an Honor change inside a shown choice's apply hook is not replay-tracked. Add a case there when a card needs it.
- Multi-pick Either/Or options with a Gold cost are refused, not paid per pick.
- Native checks: `tools/check-decision-chooser.sh` (all eleven modes, including the new `p25`) and `tools/check-either-or-ui.sh` (both modes) passed on this Windows machine with the shared Godot; this is a graphical run of the checkout, not Windows CI or expert playtesting.
