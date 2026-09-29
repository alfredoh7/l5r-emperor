# Hidden-information leak

Review the specified diff read-only. Follow AGENTS.md and the requested review scope; do not edit or publish.

Trace new data from `MatchState` through `MatchQueries.GetView`, action options/quotes and server transport. Check both seats, the actor/decider and any supported non-seat view; preserve legitimate reveals and private look/search/copy choices. A hidden card can leak through instance/definition IDs, copied metadata, counts, target candidates, disabled-action reasons or event text as well as a visible face.

Check that AI consumes only its allowed view/legal actions. Review reconnect, room persistence, replay/export and diagnostics for their intended audience; engine-private state is not a client payload just because it serializes. Internal tooling reading source/field names does not itself reveal a live match, but must not claim to verify per-seat secrecy.

Require a concrete projection/serialization path and an expected-visibility source for a leak finding. Return priority, file/line, affected viewer and a meaningful two-seat regression idea. State uncovered channels or unavailable evidence. No leak found is bounded to the paths inspected.
