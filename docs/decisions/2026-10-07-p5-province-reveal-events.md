# P5: province-reveal Events (issue #711)

Status: **built, engine only** (no card wired, no scan, no art pack: A Dangerous Game, An Empire of Madness, Harbingers of War, Rejected Mediation, Revelations, Open Recruitment, The Jade Championship and The Onyx Championship are wired in a follow-up wave, each also needing the primitive its appendix row names). Lane: rules. Source: primitive P5 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/ProvinceRevealEvents.cs`; tests: `tests/L5R.Core.Tests/ProvinceRevealEventsTests.cs` (suite `province-reveal-events`).

## What it is

| Piece | API | Where it reads |
|---|---|---|
| Flag | `ProvinceRevealEvents.Register(effectId, resolve, answer)`: an Event is flagged by its **effect id**, no `CardDefinition` edit | `ResolveEvent` (first line) |
| Channel | `EngineHooks.AfterProvincesRevealed: Func<MatchEngine, AfterRevealContext, bool>`; `AfterRevealContext(Player, Events)` with `RevealedEvent(Card, Province)` | `ContinueEventsReveal`, as the reveal loop ends, before `AdvanceStraightenHolds(EventsRevealEnds)` |
| Built-in registrant | `P5DispatchRevealEvents`, priority 10000: stamps each owed Event, runs its `resolve`, opens its decision | the channel |
| Decision | `RevealEventAsk(Prompt, Candidates, Optional)`: no candidates is yes/no; else one answer per card and "none" when optional. Stored as `PendingEventChoice` kind `p5-reveal-event`, `Origin` `reveal` | `ResolveEventChoiceCommand` default arm, `EventChoiceActions` default arm |
| Once-per-turn stamp | `MatchState.ProvinceRevealResolved` (card id to turn number), `JsonPropertyOrder(2800)`, omitted until one fires | the built-in registrant |

A flagged Event is never moved: `ResolveEvent` returns false for it before the P3 "before an Event" window and every registered resolver, so a face-down reveal, an Event already face up in its province and the Otosan Uchi path all leave it where it is. After the loop the channel is called with the flagged Events that are face up in their owner's own provinces and have not fired this turn (province order). A decision pauses the Events Phase exactly as a Celestial's entry decision does, and the answer calls `ContinueEventsReveal`, which runs the channel again with the Events still owed. The channel is only called when at least one such Event is owed, so a match that never uses P5 behaves and serializes as before.

Shared lines edited: `MatchEngine.cs` (`ResolveEvent` one line, `ContinueEventsReveal` one line), `EmperorRemainingFEvents.cs` (one line in each default arm), `EngineHookStartup.cs` (`AfterProvincesRevealed`; `ResolveEvent` was already added by P6, #705).

## Readings for Mendon to confirm or correct

- **A flagged Event revealed this turn fires this turn.** "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them": the Event just turned face up by that reveal qualifies, so it fires in the same Events Phase and again each later one.
- **Once per Events Phase, by turn number.** The stamp is the turn number, not a reveal count; an Event that is flipped back or moved and returns in the same turn does not fire twice.
- **Only the active player's own provinces.** The Event of the player whose turn it is; the other player's flagged Event never fires in this one.
- **Its text is applied even if the Event cannot be moved or discarded afterwards** (nothing about it leaves the province). A province destroyed or an Event removed by other cards uses the ordinary rules: the destroyed province discards its card.
- **Hooks registered on the channel run again after a decision** with only the Events still owed. A new registrant must be idempotent (use the stamp or its own record).

## Not done

- No card wired. Revelations needs P20's definition-decider; A Dangerous Game's "you may discard your hand to draw four" is the first user of the yes/no ask.
- No client change: the Event decision is the existing `eventChoice` command and options.
