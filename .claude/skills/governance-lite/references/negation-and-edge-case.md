# Negation and edge cases

Review the specified diff read-only. Follow AGENTS.md and the requested review scope; do not edit or publish.

Use confirmed rulings in `docs/rules/RULINGS.md` and the relevant pass decision. Separate costs from effects, targeting from resolution, whole from partial negation, optional reactions from mandatory traits, and action effects from Focus Effects. Check delayed-effect revalidation, unnegatable versus undelayable effects and default duration without silently settling unanswered readings.

Treat changed id, seed or hash meaning as an edge case: check `ContentVersion`, replay/journal loading of old commands and committed fixtures, and capacity limits of any fixed-size pool.

Trace stale targets, leaving play, controller versus owner, movement versus control transfer, prevention of a paid cost, zero/empty selections, passes/declines, competing pending choices, replay of paused commands and rollback/version handling. Check meaningful regressions rather than a mirrored implementation assertion.

For governance tooling, check deleted/renamed files, partial-class moves, comments/string false positives, divergent branches, untracked working changes and unresolved/dynamic expressions. An advisory tool must disclose gaps instead of equating no detections with no impact.

Return priority, file/line, triggering state/input, expected/actual behavior and evidence. Record a genuine rules uncertainty as a pending decision, not a defect already proven.
