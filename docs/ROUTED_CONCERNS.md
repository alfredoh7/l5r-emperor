# Routed concerns

Standing engine invariants that don't have one obvious home yet: a one-line statement of the
invariant plus a pointer to its full, authoritative explanation. This indexes rulings and
decisions that already exist elsewhere — it never restates or reinterprets them, so if a row and
its source ever seem to disagree, the source wins and this row needs fixing, not the other way
round.

| Invariant | Full explanation |
|---|---|
| An effect's duration defaults to until end of turn; it is permanent only if its printed text says so. | [Rulings register R-001](rules/RULINGS.md#r-001--durations-default-to-until-end-of-turn-not-permanent) |
| Using a Reaction is always optional; a player may always pass. | [Rulings register R-005](rules/RULINGS.md#r-005--using-a-reaction-is-always-optional-a-player-may-always-pass) |
| A Focus Effect is not an action (so action-scoped negation/timing rules don't apply to it). | [Rulings register R-013](rules/RULINGS.md#r-013--a-focus-effect-is-not-an-action) |
| An ordered search takes a qualifying card from the first named location only; the next location is searched only if the first has none, and a searched deck is always reshuffled. | [Rulings register R-011](rules/RULINGS.md#r-011--ordered-searches-take-from-the-first-named-location-only) |
| A partially negated action still counts towards a record of what it did; a wholly negated action does not. | [Rulings register R-004](rules/RULINGS.md#r-004--wholly-vs-partially-negated-actions-for-records) |
| Transferring control of a unit is not a recruit: no entering-play effects, no Proclaim; Loyal and Unique/Singular checks still apply. | [Mechanics batch decision doc, "Control transfer (6-10)"](decisions/2026-09-25-mechanics-batch.md) |

Add a row here only once its invariant is already stated in a `docs/decisions/*.md` file or
`docs/rules/RULINGS.md` — this table is an index, not a place to record a new ruling or decision.
