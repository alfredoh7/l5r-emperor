# Akodo Kobi: a delayed, mandatory recruit at the next End Phase (issue #440 follow-up)

Requested by Mendon on 2026-09-22, as a follow-up to the action-effect-tracking pass (#437): evaluate whether Akodo Kobi - exp's (oracle-l5r-232) Reaction is now triggerable, and whether its own delayed effect needs a new engine primitive or can reuse an existing one -- surveying other delayed effects first rather than building something narrow.

**Result: the trigger needed nothing further; the delayed effect needed one small new primitive.** Her text is two elements:

1. **"After the resolution of your Tactical action, give Kobi +1F."** Already directly wirable before this pass -- one more entry in the existing `AfterOwnActionTraits` dict (`kobi-tactical-bonus`), the same shape Daidoji Ebizo's own Recon trait already uses.
2. **"Reaction: After another player's Battle action resolves which destroyed one of your Lion Clan Personalities at the current battlefield, before the current turn ends, if the destroyed Personality is dead, Recruit him, ignoring costs and Honor Requirements."** Two separable pieces:
   - The **trigger** ("after another player's Battle action resolves which destroyed one of your Lion Clan Personalities") is exactly the after-action-resolved window issue #437 built: `LastActionDestroyedOwnKeyword(playerId, "Lion Clan")` plus a `Timing == "Battle"` and `facts.PlayerId != playerId` check, the same shape Measure of Devotion already uses. "At the current battlefield" needed no separate check: a Battle action's own destruction always lands on a card that was there, so it holds by construction. **No change to ActionEffectTracking.cs was needed** -- the general primitive already covered this.
   - The **effect** is not immediate, though: "before the current turn ends" describes a standing deadline, not an immediate resolution. Using the Reaction now just *queues* a mandatory check for later, resolved automatically, not a further player choice ("this recruitment is not optional once the reaction has been triggered"). This is what needed a new primitive.

## Surveying existing delayed effects before adding one

Before building anything, the engine's other delayed/timed mechanisms were checked for a fit:

- **`DelayedDestruction.cs`** (`PendingDelayedDestructions`, Spirit of the Berserker): a list of card ids, drained unconditionally at the top of `EndTurn` ("before this turn ends"). Closest in *shape* (a queue drained at a turn-structure checkpoint) but fixed to one hardcoded effect (destroy) and one card list, not reusable for an arbitrary future effect.
- **`BeforeResolution.cs`**'s `DelayedBattleEffect`: resolved "before this battle resolves," a different checkpoint entirely (mid-battle, not turn end).
- **`StraightenHolds.cs`**: a *passive* suppression (a card will not straighten) released at a named point in the `StraightenPoint` enum (`TurnBegins`, `StraightenPhaseEnds`, `EventsRevealEnds`, `ActionPhaseEnds`, `WhileInPlay`) -- no `TurnEnds`/`Phase.End` point, and not a vehicle for an arbitrary effect (only ever "don't straighten").
- **`PendingTamokoHonorChecks`**: the closest working precedent for the exact shape needed -- a small list of typed records, drained unconditionally at the very top of `EndTurn`, "no one is deciding anything" (confirmed by `SeppukuTiming.cs`'s own classification of it as not an open decision).

None fired at "the next End Phase, any player's" specifically, and none carried enough data to perform an arbitrary later recruit. `BringDeadPersonalityIntoPlay` (used by At Any Cost, Goju Plot) already **is** the "ignoring costs and Honor Requirements" recruit primitive -- it makes no `QuotePurchase` call at all, so there was never a cost/Honor check to bypass. The only new piece is `MatchState.PendingEndPhaseRecruits` (`List<PendingEndPhaseRecruit>` of `(PlayerId, CardId, Via)`), drained by `MatchEngine.ResolvePendingEndPhaseRecruits` at the exact point `PendingTamokoHonorChecks`/`ResolveDelayedDestructions` already run in `EndTurn` -- which fires for *every* player's own turn ending, so "the next End Phase, any player's" falls out for free rather than needing its own scheduling logic. Each entry is checked for whether the card is still `Zone.Dead` at that moment (an intervening effect could already have brought him back some other way) before doing anything.

**Scoped narrow, not generalized further, on purpose**: only one effect kind (recruit) is supported today; a `Kind`-dispatched, multi-effect version was considered and deliberately not built, since nothing else currently needs it -- the same restraint the project has applied elsewhere (e.g. Mirumoto Katagi's still-unbuilt attachment-Transfer clause). The shape (a small list of typed records drained at a named checkpoint) is cheap to extend with a second `Kind` if a future card needs one.

**One deliberate simplification, called out for a future card to revisit**: unlike `BringDeadPersonalityIntoPlay`, the queued recruit does not open an entering-play Reaction window. Doing so mid-`EndTurn` would interrupt the turn's own transition to `Phase.End` with no way for `ResumeAfterDeferredWindow` to correctly resume it (it recognizes a handful of specific interrupted-state shapes, e.g. a mid-transition battle, but not "mid-EndTurn"). No Lion Clan Personality this pass concerns carries an entering-play Reaction, so nothing is lost today; a future card that does would need that resumption built first.

## Regenerated

`docs/CARD_INVENTORY.xlsx` (2165 rows, 601 wired).
