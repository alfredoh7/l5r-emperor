# Duel Demo lives within L5R

Product decision accepted by Alfredo in the September 16, 2026 implementation request: Play → Duel Demo opens a prepared encounter inside the normal L5R application. It is not a separate application or export. The default is solo Crane, with optional manual control of both seats and results held until Continue. The approved Crimson Standoff environment remains the visual target.

An isolated local MatchSession preserves offline practice. A private room must be left through confirmation before entering the demo. Core supplies named seeded prepared positions, legal decisions, private/public focus views and retained public reveal/result evidence. The screen cannot access diagnostic engine State. Synthetic fixtures are separate from pinned playable decks and visibly labeled.

The implemented shared contract is proposed for mdornbrook's mechanical review on PR #220: defaulted focus counts/duel identifier/LastDuel view records, legal stage command queries and Core scenario factory. Existing duel resolution semantics are preserved. Alfredo's task authorization is not expert rules acceptance.

The demo host owns setup, seed, bot, seat switching and restart. DuelScreen consumes session snapshots and emits commands/completion so the normal game can reuse it. Activate that normal-match path only after demo acceptance. The permanent demo entry remains available for regression and practice afterward. Grass/cloud AAA quality is a separate acceptance track.

Lane: shared. UI/product lead: alfredoh7. Mechanics lead: mdornbrook. Affected areas: L5R.Core duel views/scenarios/queries, L5R.AI duel policy, L5R.Client menu/session/demo/screen, regression and native probes.

Issue: https://github.com/alfredoh7/l5r-emperor/issues/205
PR: https://github.com/alfredoh7/l5r-emperor/pull/220
