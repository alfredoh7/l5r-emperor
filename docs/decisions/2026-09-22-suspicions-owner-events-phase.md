# Suspicions: the owner's Events Phase, and Events already face up in a province

**Decision:** Suspicions (`oracle-l5r-7675`) — "After you reveal cards in your Provinces at the start of your
turn, if this Event is face-up in one of them, until the game ends, before each of your Action Phases begins,
each player in turn order starting with you loses 1 Honor" — is read as follows:

1. **"After you reveal cards in your Provinces at the start of your turn" is the Events Phase.** It happens after
   the Straighten Phase and its decisions. "At the start of your turn" is wording for human readers, who think of
   the Straighten and Events Phases as "early" in the turn; the operative condition is *after you reveal cards in
   your provinces during your Events Phase*.
2. **"You" is the Event's owner, so it fires only on the owner's turns.** "Before each of your Action Phases" the
   owner and then the opponent each lose 1 Honor. It does not fire on the opponent's turn. `ResolveOngoingEvents`
   skips a Suspicions whose owner is not the active player.
3. **"If this Event is face-up in one of them" covers an Event that was already face up.** An Event refilled face
   up outside the Events Phase (Border Keep's `CycleProvinces`, the Kharmic replacement) is face up in one of the
   owner's provinces after that player's next Events Phase reveal. Events resolve only in the Events Phase, so
   `ContinueEventsReveal` resolves it there, the same way as a freshly revealed Event, instead of leaving it
   stuck in the province.

**Reason and evidence:** Rules lead (Mendon) ruling, 2026-09-22, while reconciling #268. It answers the Codex
review points on #248 (Suspicions triggering on both turns; face-up Events never resolving). Rules reference:
`docs/rules/EMPEROR_EDITION_CORE_RULES.md` §2 Events Phase ("Events resolve *only* in the Events Phase").

**Lane:** rules (mdornbrook). **Files:** `src/L5R.Core/MatchEngine.cs` (`ResolveOngoingEvents`,
`ContinueEventsReveal`); regressions `SuspicionsEventInPlay` and `FaceUpEventResolvesInEventsPhase` in
`tests/L5R.Core.Tests/Program.cs`. **PR:** #268 (commits 181c2a96, d3304582).

**Status:** Accepted. Supersedes the "at the end of every Events Phase (whoever's turn it is)" wording in
[2026-09-16-events-phase-and-ongoing-effect-zone.md](2026-09-16-events-phase-and-ongoing-effect-zone.md).
