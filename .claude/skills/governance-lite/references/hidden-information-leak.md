# Hidden-information leak

Review the specified diff read-only. Follow AGENTS.md and the requested review scope; do not edit or publish.

Trace new data from `MatchState` through `MatchQueries.GetView`, action options/quotes and server transport. Check both seats, the actor/decider and any supported non-seat view; preserve legitimate reveals and private look/search/copy choices. A hidden card can leak through instance/definition IDs, copied metadata, counts, target candidates, disabled-action reasons or event text as well as a visible face.

Opaque ids only hide identity if no other channel carries creation or deck order: check dictionary/collection enumeration in views and target lists, and whether a seed that derives ids can reach a client payload, room listing, replay or export. Open `MatchQueries.GetView` for each seat; do not infer it from the diff.

Enumeration order is a channel: `_state.Cards` is a Dictionary in setup order, so candidate builders over `_state.Cards.Values` (for example the `card-in-province` target list in `CardAbilities.cs`) leak deck-list order through option order and the default command unless sorted.

If the change alters the meaning of existing ids, seeds, hashes, serialized state or payloads, confirm `MatchEngine.ContentVersion` (gate for `MatchEngine.Replay` and server journal recovery) moved, or that old replays, journals and committed fixtures under `docs/`, `tests/` and `previews/` are deliberately unsupported.

Check that AI consumes only its allowed view/legal actions. Review reconnect, room persistence, replay/export and diagnostics for their intended audience; engine-private state is not a client payload just because it serializes. Internal tooling reading source/field names does not itself reveal a live match, but must not claim to verify per-seat secrecy.

Require a concrete projection/serialization path and an expected-visibility source for a leak finding. Return priority, file/line, affected viewer and a meaningful two-seat regression idea. State uncovered channels or unavailable evidence. No leak found is bounded to the paths inspected.
