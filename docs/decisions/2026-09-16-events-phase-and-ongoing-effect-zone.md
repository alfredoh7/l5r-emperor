# Event resolution dispatch, and where an ongoing ("until the end of the game") Event effect lives

**Decision:** An Event, once revealed during the Events Phase, is removed from its province and
resolved immediately (before refilling), then discarded — *unless* its own printed effect is ongoing
("until the end of the game"), in which case it goes to a new zone, `Zone.EventInPlay`, instead of
the discard pile, for as long as that effect stays active. This is a physical reminder area next to a
player's Personalities (the client's own future "sideboard" rendering — UI-lane scope, not built by
this decision). `Zone.EventInPlay` is public information, exposed via `PlayerView.EventsInPlay` the
same way `Zone.RemovedFromGame` already is, with no visibility gate.

Dispatch is Effect-keyed, the same convention every other one-off card behavior in `MatchEngine.cs`
already uses: `MatchEngine.ResolveEvent` runs once per revealed Event, `MatchEngine.ResolveOngoingEvents`
re-checks every card currently sitting in `Zone.EventInPlay` at the end of every Events Phase (whoever's
turn it is). The card's own zone membership *is* the "effect still active" flag — no separate per-player
or per-match state tracks it, the same living-state approach `GrantedKeyword.Duration == "Permanent"`
already uses for a keyword grant. Nothing yet moves a card *out* of `Zone.EventInPlay` (no published
Event's effect ends), but the intended shape when one does is: move it to `Zone.DynastyDiscard`, same
as an ordinary Event.

**Suspicions** (Emperor Edition #24, `oracle-l5r-7675`, the first card wired through this dispatch):
"After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one
of them, until the game ends, before each of your Action Phases begins, each player in turn order
starting with you loses 1 Honor." The engine's own bookkeeping choice to move the physical card out of
its province and into `Zone.EventInPlay`, rather than leaving it face-up where it was revealed (which
is what "if this Event is face-up in one of them" literally describes), changes nothing about the
rules-visible effect — it is a clearer, dedicated reminder area instead, per the user's own direction
below. The trigger point ("before each of your Action Phases begins") lands at the very end of
`EventsPhase`, immediately before it calls `SetPhase(Phase.Action, ...)` — including the turn
Suspicions itself is revealed, since `ResolveEvent` already moved it into `Zone.EventInPlay` earlier in
that same call. "Each player in turn order starting with you" is preserved exactly: the active player's
own loss resolves first, then the other player's.

**A second design note, recorded but not implemented (no card needs it yet):** if some future Event's
own text causes it to be reshuffled back into the Dynasty deck while it still has an active ongoing
effect that is not itself ended or negated, the shape should be: create a *copy* of the card and place
that copy in the Dynasty deck (satisfying the reshuffle instruction), while the original stays in
`Zone.EventInPlay` with its effect still running. If something later ends that effect, the copy sitting
in `Zone.EventInPlay` (not the reshuffled one, which is a fresh, inert card again) is removed from the
game entirely, rather than moved to the discard pile like an ordinary ended Event. This mirrors the
`Zone.RemovedFromGame` precedent (issue #184, Discipline) for "leaves play and can never come back,"
not the ordinary discard path. Nothing in the engine implements this yet — recorded here so it is not
re-derived (or re-specified differently) whenever a card actually forces it, the same discipline as the
[Unique/Singular control-transfer decision](2026-09-12-unique-singular-control-transfer.md).

**A third design note, recorded but not implemented (no card needs it yet):** some Events' printed
text opens with an action-style "Limited:" precursor — the same prefix ordinary Limited-timing
abilities use elsewhere in this engine (`AbilityDefinition.Timing`, gated in `ExecuteAbilityEffects`).
On an Event, that precursor is decorative template text only: the Event still resolves automatically
and unconditionally when revealed during the Events Phase, exactly like every other Event (through
`ResolveEvent`/`ResolveOngoingEvents`), never as a player-activated `Timing:"Limited"` ability the way
a real `AbilityCatalog` entry would be. When such a card is eventually wired, its own dispatch case in
`ResolveEvent`'s switch should simply ignore the "Limited:" precursor (no activation command, no
once-per-turn gate, no `AbilityCatalog` entry at all) rather than route it through the Limited-ability
machinery. Its `CardDefinition.Text` should still transcribe the printed "Limited:" wording verbatim,
matching every other card's convention of keeping scanned text as-is — only the *runtime* behavior
ignores it, not the displayed card text.

**Reason and evidence:** User spec (2026-09-16), on Suspicions specifically: "Events are cards that
resolve when they are revealed during the Events phase and are then discarded. Some events have an
effect that is recurring or permanent however, unless the event says, 'place this card in play' it is
not considered 'in play.' Such an event is Suspicions. ... This is a permanent effect because it is
'until the end of the game'. If something were to cause it to be shuffled back into the dynasty deck
without ending its permanent effect it could be done. However, to remind the players of this ongoing
effect, place the card into play next in the sideboard area to the right of the personalities (that
Alfredo will eventually build). When the effect ends, place the card in the discard pile. If a card
states that the event should be reshuffled into the Dynasty deck (another event does this) but it has
an ongoing effect that is not ended or negated, create a copy of the card and place it in the dynasty
deck. If anything ends the effect from that point forward, remove the Event that is in the play area
from the game."

User spec (2026-09-16), on the "Limited:" precursor generally: "Some events read like an action card
and have 'Limited:' to start them. These events resolve as normal during the events phase and the
'Limited:' precursors is ignored."

No Event/ongoing-effect/in-play-zone infrastructure of any kind existed before this — `CardType.Event`
and `Phase.Events` were both pre-built placeholders (issue #10) with an explicit comment marking
`EventsPhase` as a stub waiting for exactly this. `docs/rules/EMPEROR_EDITION_CORE_RULES.md`'s own
errata section independently corroborates the general shape (a different card, *A Fever in the Blood*,
under a documented interim ruling: "Put this Event into play. Discard it after your next turn begins.").

**Lane:** Rules (mdornbrook). No client/UI change in this PR — `Zone.EventInPlay` compiles cleanly
against the existing client (every `switch` over `Zone` already has a wildcard fallback arm, the same
situation `Zone.Focused`/`Phase.Straighten`/`Phase.Events` were already in per issue #10's own
handoff), but nothing renders the new zone as a distinct "sideboard" area yet — that is Alfredo's own
UI-lane work, flagged rather than built here per the collaboration split.

**Affected files:** `src/L5R.Core/Models.cs` (`Zone.EventInPlay`, `PlayerView.EventsInPlay`),
`MatchEngine.cs` (`EventsPhase`, `ResolveEvent`, `ResolveOngoingEvents`), `MatchQueries.cs` (`GetView`),
`PublishedContent.cs`/`CardCatalog.cs`/`L5R.Core.csproj` (`EventWave1`), new
`tools/content/package_event_wave_1.py`. Future reshuffle-copy work (not yet needed): wherever a card's
own "reshuffle this Event into the Dynasty deck" text is eventually wired. Future "Limited:"-prefixed
Event work (not yet needed): the new card's own case inside `ResolveEvent`'s switch, plus whichever
`package_*.py` wave script packages it (Text transcribed verbatim; no `Abilities`/`AbilityCatalog`
entry).

**Issue:** https://github.com/alfredoh7/l5r-emperor/issues/241

**Status:** Accepted and implemented for Suspicions itself (the dispatch, the zone, the ongoing-effect
recheck). The reshuffle-copy and "Limited:"-precursor design notes are both accepted but not
implemented — no card exists yet that needs either.
