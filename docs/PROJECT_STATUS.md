# Shared project status

Updated: 2026-09-24. This milestone snapshot distinguishes merged implementation, open review and human acceptance. Refresh live GitHub context before starting work; historical documents describe earlier checkpoints.

## Merged baseline and current review

The earlier approved UI consolidation and live-match duel integration landed in [PR #268](https://github.com/alfredoh7/l5r-emperor/pull/268), merged on 22 September 2026 as `86de986d`. This includes catalogue/deck-builder presentation, lacquer/Caudex menus, WeatheredQuill headings, fortress hall, placards, Home/Favor, the table-to-duel transition and Play → Duel Demo. [UI_CONSOLIDATION.md](UI_CONSOLIDATION.md) and [DUEL_MATCH_INTEGRATION.md](DUEL_MATCH_INTEGRATION.md) are historical implementation records, not current merge gates.

[PR #434](https://github.com/alfredoh7/l5r-emperor/pull/434) remains the unmerged combined review for newer UI work: choice/search/copy interfaces, Regions/Celestials, the corrected draggable Top Cards, ghost copies, multiple targets, attack tabs and battle progress. Alfredo's approvals are recorded there; Mendon's review remains pending. Superseded transition preview #266 is closed with its branch/history preserved. No game merge is part of this cleanup.

[PR #454](https://github.com/alfredoh7/l5r-emperor/pull/454) records the completed delivery of the [revision 2 Windows preview](https://github.com/alfredoh7/l5r-emperor/releases/tag/preview-pr434-659e015f0e-r2-windows). Game source is `659e015f`; delivery tooling is `ab284dbd`. The exact package passed Windows runtime/launcher checks and Mendon received the review notification. This pinned preview does not include later main-only content expansions. [Final delivery evidence](https://github.com/alfredoh7/l5r-emperor/pull/454#issuecomment-5803848934) supersedes the preparation handoff's publication-pending note. Delivery does not establish native Windows graphical or expert rules acceptance.

## Current playable baseline

- Desktop **0.3.10**, content profile `lion-crane-v1`, content version `emperor-lion-crane-0.5.0`.
- All 40 distinct definitions in the selected Lion/Crane 40/40 decks have implementations and period scans.
- Rules authority, automated payments/turn flow, choices, attachments, Favor, battles, victory, deterministic replays, a heuristic opponent and private-room networking exist.
- UI baseline: full card faces, physical piles/stacks, pan/zoom, optional fullscreen, enlarged navy mat, fortress lighting, bitmap phase bar with WeatheredQuill headings. Preserve this aesthetic while Alfredo develops UI/QoL improvements.
- The published rules profile is a reproducible playtest profile, not a certified final historical tournament legality claim.

## Current focus

Review the newer UI in #434 and address concrete findings on its exact build. Catalogue viewing, deck building/storage, custom Practice and live dueling already have implementations; [NEXT_GAME_PLAN.md](NEXT_GAME_PLAN.md) records their earlier delivery sequence. The [24 September task audit](handoffs/2026-09-24-ui-task-record-cleanup.md) maps all 38 previously open Alfredo-assigned issues to implementation, review or remaining acceptance, without changing scope or assignments. Duel follow-ups [#448](https://github.com/alfredoh7/l5r-emperor/issues/448) and the full in-game rules reference [#58](https://github.com/alfredoh7/l5r-emperor/issues/58) remain actionable work.

The live playtest deck and frozen automated-test deck are separated by merged PR #116; card behavior remains shared. Reference catalogue/scan coverage is broader than playable engine support. Rules/content expansion continues independently; current main includes Honor and Treachery PR #459. The profile and historical test counts below are not totals for current catalogue coverage or current tests.

mdornbrook has accepted repository access and is playing/reviewing mechanics. Alfredo owns UI/QoL improvements. Both lanes progress on separate task branches, coordinated through AGENTS.md and docs/COLLABORATION.md. Validate complete matches and disputed interactions. Use the rules-review issue template and `docs/RULES_REVIEW.md`. Reproduce each accepted discrepancy as a focused engine/client case before changing behavior.

The shared Crimson Standoff screen serves normal matches (merged #268). The isolated **Play → Duel Demo** sandbox was removed from the app and archived on 2026-10-13 ([archive note](archive/duel-demo/README.md), #926); [DUEL_DEMO.md](archive/duel-demo/DUEL_DEMO.md) and [DUEL_MECHANICAL_REVIEW.md](DUEL_MECHANICAL_REVIEW.md) retain the original review evidence. Current duel follow-ups belong to #448: live-duel concession controls, paused Focus Effect choices/beats and native round trips on the integrated build. The merged baseline does not close those follow-ups.

## Open acceptance gates

1. Full human matches with both Clans; corrections to rules, timing and interaction flow.
2. Native Windows play and a real Mac/Windows private match, including reconnection and suspended choices.
3. Scripted beginner tutorial using the existing engine/help text.
4. Scope-specific acceptance of existing deck/Practice UI and continued verified card/mechanic expansion; a merged baseline does not accept every related umbrella issue.

No internet match server has been deployed. The broader catalogue is not executable card coverage. GitHub access grants project/release access; it does not connect two game clients automatically.

## Evidence

- Historical deterministic baseline: 148 recorded tests; GitHub setup rerun is recorded in `docs/handoffs/GITHUB_BASELINE.md`. Use current PR/check records for current counts.
- 0.3.10 Mac package was visually checked in 1920×1080 windowed and 3840×2160 fullscreen; 16:10/ultrawide source views and reduced-motion checks also passed.
- Windows package contents/runtime were validated; native Windows gameplay is still open.
- See `docs/PHASE_BAR_ART.md`, `docs/rules/LION_CRANE_COVERAGE.md`, and historical `docs/VALIDATION.md` for scope and limits.

## Collaboration protocol

Open issues own tasks and review findings; PRs own implementation and validation; this file owns shared milestone status. Update it when those facts change. Before starting, inspect the issue, recent PRs, branch and working tree. Before yielding, record what changed, evidence, remaining work and the branch/commit. Do not treat a previous AI chat as shared context.

## Project coordination board

The private [L5R Project Board](https://l5r-project-board.netlify.app) is deployed with GitHub sign-in and issue/PR synchronization. [PROJECT_BOARD.md](PROJECT_BOARD.md) describes agent claims, scope agreements and the outage fallback. The current product focus remains dependable Lion–Crane matches: #71 (complete matches), #72 (native Windows), and #73 (external Mac–Windows multiplayer) track acceptance without implying those tests have passed. The migration adds scoped UI and deck-setup children; merged implementation and human acceptance remain separate.
