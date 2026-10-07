# P6: after-event records and windows (issue #705)

Status: **built, engine only** (no card wired, no scan, no art pack: the cards this unlocks are wired in a follow-up wave). Lane: rules. Source: primitive P6 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/AfterFacts.cs`; tests: `tests/L5R.Core.Tests/AfterFactsTests.cs` (suite `after-facts`, 12 tests).

## What it is

A **fact** is the after-action record for an event that is not an action's effect. `RecordFact` queues it at the one choke point of each event; the queue is drained at the safe points the engine already uses (the top of `OpenOverconfidentDrawWindow`, and after every accepted command). For each fact the registered **traits** run first (mandatory, no window); then the existing `afterActionResolvedReaction` window opens when, and only when, some player holds a Reaction that names the fact and whose condition holds. No legal Reaction means no window and no pass button (#639 / #664). The window is not re-opened from inside itself: two facts of one command open one window after the other.

A fact is queued only when somebody could care (a trait registered and active for its kind, or a card in the match with a Reaction naming it), so a match that uses none of this serialises exactly as before.

| Fact (`FactKinds`) | Recorded at | PlayerId / CardId / extra |
|---|---|---|
| `seppuku` | `CommitSeppuku`, `OrderSeppuku` | the player who committed it / the Personality |
| `proclaimed` | `FinishProclaim` | the Proclaimer / the Proclaimed Personality; `Arg = "negated"` when its effects were negated |
| `assigned-to-attack` | `OpenAssignedReactionWindow` (ungated) | the attacker / each assigned Personality; `Province` = its battlefield |
| `turned-face-up` | the post-command sweep (`SweepProvinceFaces`) | the province's owner / the card; `Province`. A card newly in a province face up (a face-up refill) counts |
| `terrain-destroyed` | `DiscardTerrain` | the Terrain's controller / the Terrain; `Province` = its battlefield |
| `celestial-entered` | `EnterCelestialPlay` | its controller / the Celestial |
| `left-play` | the zone-history sweep (P21's `EnteredPlayTurn` going from set to unset) | its controller / the card; `Arg` = the zone it went to |

## The API a wave calls (all `internal`, in `AfterFacts.cs`)

```csharp
// A Reaction names a fact: Trigger = FactTriggers.For(FactKinds.Seppuku)  (a catalogue entry in the card's feature file)
FactRegistry.Condition("KatsutaOnlyOthers", 12900, "my-ability-id", c => c.Fact.PlayerId != c.PlayerId);   // optional: beyond "held by its owner"
FactRegistry.Trait("KatsutaTrait", 12900, FactKinds.Seppuku, (engine, fact) => ...);                        // a mandatory trait; runs before any Reaction; no window
// A Celestial's entry decision (Natsu-Togumara: choose a Personality; Shrine to the Heavens: choose a Celestial it does not discard)
CelestialEntryRegistry.Register("NatsuChoice", 12900, (engine, ctx) => new CelestialEntryChoice(prompt, candidateIds, apply, Optional: true, SparesFromDiscard: false));
// "After you announce an action", with a performer filter (Magistrate's Stipend)
AnnounceRegistry.Register("StipendAnnounce", 12900, "stipend-reaction", a => a.PerformerId is not null && a.Ability.Timing != "Reaction");   // the Reaction: Trigger = AnnounceTriggers.WithPerformer
engine.ApplyUntilActionEnds(card, source, effect, "Chi", +1);                                                // a modifier that ends with the announced action
```

`FactConditionContext` gives the fact, the card holding the Reaction, who holds it and which Reaction; `AnnounceContext` gives the announced command, its ability, its source and its performer; `CelestialEntryChoice` is recomputed from its hook whenever it is asked or answered (only the hook's name is stored).

## Shared lines edited

- `ActionEffectTracking.cs`: `IsAfterActionResolvedReaction` includes fact triggers; `AfterActionResolvedApplies` asks `FactApplies` first (a fact's window offers only the Reactions that name it, an action's only the others); `FinishActionEffectTracking` ends "until the action ends" modifiers of the action that finished.
- `CardAbilities.cs`: two trigger arms in `ValidateAbility` (fact triggers; `AnnounceTriggers.WithPerformer`), the `DiscardTerrain` record.
- `MatchEngine.cs`: `Execute` runs `SweepFactsAfterCommand`; `OpenOverconfidentDrawWindow` drains facts; `OrderSeppuku`; `EndTurn`; the Events reveal loop pauses on a Celestial's decisions (`ResolveCelestialCore` / `EnterCelestialPlayCore`; the 3-parameter and 2-parameter methods keep their shapes); the Terrain-replacement path.
- `EmperorRemainingFEvents.cs`: two default arms (answering and listing a `p6-celestial` decision), the existing Event-decision machinery is reused (Origin "reveal" pauses the reveal, "deferred" resumes the window).
- `EmperorP3Priority.cs`: the announce watcher also asks registered watchers; `MatchQueries.cs`: the announce window lists them, one option per performer.
- `ActionNegation.cs`, `EmperorPersonalityBatch3.cs`, `SeedsOfDecayBatch2.cs`, `EmperorRemainingG.cs`, `ZoneExtensions.cs`, `Models.cs` (`ResolvedActionFacts.Fact`), `EngineHookStartup.cs` (five channels, plus `ResolveEvent`).

New persisted state (all omitted when unused): `QueuedFacts` (2600), `ProvinceFaceSnapshot` (2601), `CelestialDiscardDebts` (2602), `ActionScopedModifiers` (2603), `ResolvedActionFacts.Fact`. None is a `Pending*` decision: a queued fact is always drained at the end of the command that queued it, and the one real decision (a Celestial's entry choice) is a `PendingEventChoice`, already classified.

## Defects of the #668 survey

- **Order Seppuku skipped `FinishActionEffectTracking`**: fixed by clearing the action's accumulators right after the destruction (`ClearActionAccumulators`), so the cards it destroyed no longer leak onto the next action's record. It does *not* call `FinishActionEffectTracking`: that method and the hooks it runs index the ability catalogue by the action's id and Order Seppuku has no catalogue entry (the same reason Equip builds its own record). Reactions to "an action that destroyed your Personality" therefore still do not see Order Seppuku, exactly as before.
- **`ResolveEvent` missing from `EngineHookStartup.Build`**: fixed (it was the only channel the startup seal had forgotten).
- **The Terrain self-replacing path did not set `TerrainDestroyedThisBattle`**: fixed in `PlayStrategy` (the replaced Terrain counts as destroyed for Ring of Water), and tested.

## Readings for Mendon to confirm or correct

- **Traits run at the next safe point, not in the middle of the effect that caused the fact.** Yogo Katsuta's "after a Personality commits seppuku, his controller loses 1 Honor" is therefore applied right after the command that contained the seppuku (before the next player acts), not inside it. Everything a trait does is judged then.
- **A Proclaim whose effects were negated is still "Proclaimed"** (the fact carries `Arg = "negated"` so a Reaction may check); Proclaim was taken, and Rhetoric and the like may be played on it.
- **`assigned-to-attack` is recorded for attackers only**: Personalities on a battlefield owned by the active player when the assignment command finishes. A defender's assignment records nothing.
- **`turned-face-up` is a comparison per command**: a card that turns face up and back down inside one command is not seen, and a card that arrives in a province face up (a face-up refill) is. The comparison is kept only while some trait or Reaction names the fact.
- **`left-play` is seen by the sweep after the command**, from P21's entry stamps: a card that enters and leaves play inside one command is never stamped, so never seen; the fact does not say whether a battle or an action removed the card (R-022): a Reaction that must tell them apart reads the destroyed record of the action (`Affected`), as today.
- **A Celestial's entry decision is asked of its controller once the Celestial is in play and its province refilled.** When one decision spares a card (`SparesFromDiscard`), the discard of the controller's other Celestials waits for the answers, so for that moment two Celestials of theirs are in play; nothing can be done in between, because the decision is the very next thing. A decision with no candidates is not asked.
- **The announce window now exists for any phase** (it opens only for a registered watcher that can actually take a Reaction right now, asked exactly as the window will list it), and only for `useAbility` announcements. One watcher card is asked at a time, each once per announcement.
- **"Until the action ends"** belongs to the announced action when applied from an announce-window Reaction (so the Reaction's own tail does not end it), otherwise to the action now resolving; a negated action and the turn's end also end it.

## Not done

- No card is wired (the wave: Yogo Katsuta, Doji Hakuseki - exp3, Susumu Kuroko, Akumasa, Asahina Shigemitsu - exp, Unseasonable Weather, Open Recruitment, The Crystal Tears, Iweko Shibatsu, Benten's Blessing, Natsu-Togumara's Guidance, Shrine to the Heavens, Magistrate's Stipend, The Jade Championship).
- A Celestial entry decision for a Celestial that enters through a path that cannot pause (a Rosary's put-into-play, a delayed entry) is queued and asked at the next safe point (Origin "deferred") instead of pausing a reveal.
- Native graphical checks were not run: the client lists the new decision through the existing Event-decision options and no client file changed.
