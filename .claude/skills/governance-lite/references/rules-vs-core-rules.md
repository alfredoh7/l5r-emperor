# Rules versus core rules

Review the specified diff read-only. Follow AGENTS.md and the requested review scope; do not edit or publish.

Read the relevant sections of `docs/rules/EMPEROR_EDITION_CORE_RULES.md`, the selected printing/scan evidence, `docs/rules/RULINGS.md`, `docs/ROUTED_CONCERNS.md` and the pass decision. Verify card-specific text against that selected period printing; later Oracle wording does not silently supersede it. Confirmed owner rulings and frozen profile decisions must remain distinguishable from the reference rulebook and unanswered readings. Report conflicts rather than inventing a hierarchy to settle them.

Actually open the files named above before concluding; list those you read and those you could not. `RULINGS.md` and `ROUTED_CONCERNS.md` are small, so skipping them is not a saving.

If the change alters the meaning of existing ids, seeds, hashes, serialized state or payloads, confirm `MatchEngine.ContentVersion` (gate for `MatchEngine.Replay` and server journal recovery) moved, or that old replays, journals and committed fixtures under `docs/`, `tests/` and `previews/` are deliberately unsupported.

Trace announcement, eligibility, targeting, costs, resolution and optional choices in authoritative Core/server code. Check that catalogue membership is not represented as playable support, and UI does not derive legality, Gold or Honor. A card ability may explicitly override a core rule; cite the actual text before flagging a deviation.

Return priority, file/line, exact discrepancy, expected/actual behavior and evidence for each actionable finding. State which rules paths you checked and which evidence was unavailable. For tooling-only changes, explain whether the workflow preserves these boundaries; do not invent gameplay findings.
