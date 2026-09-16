# Shared project status

Updated: 2026-09-16. This is the current summary for human collaborators and AI sessions; historical documents describe earlier checkpoints.

## Approved UI integration candidate

Alfredo approved the Home/Favor presentation and requested consolidation of the existing UI branches. [PR #248](https://github.com/alfredoh7/l5r-emperor/pull/248) combines the catalogue, lacquer/Caudex menus, WeatheredQuill headings, battle flow and removed-card gallery, current fortress hall/table, hand-limit placards and Noble Crest Home/Favor. Existing deck-builder functionality stays included. The branch manifest and review state are in [UI_CONSOLIDATION.md](UI_CONSOLIDATION.md); this is an integration candidate until shared review and CI are complete.

The separate Duel Demo in #220 remains excluded for Alfredo and Mendon's own later acceptance. Main's existing duel mechanics are preserved. Do not launch older isolated UI worktrees as the latest full-game preview.

## Current playable baseline

- Desktop **0.3.10**, content profile `lion-crane-v1`, content version `emperor-lion-crane-0.2.0`.
- All 40 distinct definitions in the selected Lion/Crane 40/40 decks have implementations and period scans.
- Rules authority, automated payments/turn flow, choices, attachments, Favor, battles, victory, deterministic replays, a heuristic opponent and private-room networking exist.
- UI baseline: full card faces, physical piles/stacks, pan/zoom, optional fullscreen, enlarged navy mat, upright lanterns, bitmap phase bar with Musashi Brush headings. Preserve this aesthetic while Alfredo develops UI/QoL improvements.
- The published rules profile is a reproducible playtest profile, not a certified final historical tournament legality claim.

## Current focus

Alfredo has paused broad battle presentation work. The current delivery plan is [NEXT_GAME_PLAN.md](NEXT_GAME_PLAN.md): finish catalogue viewing, deck building/storage and custom Practice, then dueling, Spells and further wired-card waves. The live playtest deck and frozen automated-test deck are separated by merged PR #116; card behavior remains shared. Reference catalogue/scan coverage is broader than playable engine support. Older baseline counts below are historical, not a current catalogue count.

mdornbrook has accepted repository access and is playing/reviewing mechanics. Alfredo owns UI/QoL improvements. Both lanes progress on separate task branches, coordinated through AGENTS.md and docs/COLLABORATION.md. Validate complete matches and disputed interactions. Use the rules-review issue template and `docs/RULES_REVIEW.md`. Reproduce each accepted discrepancy as a focused engine/client case before changing behavior.

## Open acceptance gates

1. Full human matches with both Clans; corrections to rules, timing and interaction flow.
2. Native Windows play and a real Mac/Windows private match, including reconnection and suspended choices.
3. Scripted beginner tutorial using the existing engine/help text.
4. Additional verified decks/mechanics, then deck builder and wider Emperor coverage.

No internet match server has been deployed. The broader catalogue is not executable card coverage. GitHub access grants project/release access; it does not connect two game clients automatically.

## Evidence

- Existing deterministic baseline: 148 recorded tests; GitHub setup rerun is recorded in `docs/handoffs/GITHUB_BASELINE.md`.
- 0.3.10 Mac package was visually checked in 1920×1080 windowed and 3840×2160 fullscreen; 16:10/ultrawide source views and reduced-motion checks also passed.
- Windows package contents/runtime were validated; native Windows gameplay is still open.
- See `docs/PHASE_BAR_ART.md`, `docs/rules/LION_CRANE_COVERAGE.md`, and historical `docs/VALIDATION.md` for scope and limits.

## Collaboration protocol

Open issues own tasks and review findings; PRs own implementation and validation; this file owns shared milestone status. Update it when those facts change. Before starting, inspect the issue, recent PRs, branch and working tree. Before yielding, record what changed, evidence, remaining work and the branch/commit. Do not treat a previous AI chat as shared context.

## Project coordination board

The private [L5R Project Board](https://l5r-project-board.netlify.app) is deployed with GitHub sign-in and issue/PR synchronization. [PROJECT_BOARD.md](PROJECT_BOARD.md) describes agent claims, scope agreements and the outage fallback. The current product focus remains dependable Lion–Crane matches: #71 (complete matches), #72 (native Windows), and #73 (external Mac–Windows multiplayer) track acceptance without implying those tests have passed. The migration adds scoped UI and deck-setup children; merged implementation and human acceptance remain separate.
