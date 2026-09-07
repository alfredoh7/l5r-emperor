# Shared project status

Updated: 2026-09-07. This is the current summary for human collaborators and AI sessions; historical documents describe earlier checkpoints.

## Current playable baseline

- Desktop **0.3.10**, content profile `lion-crane-v1`, content version `emperor-lion-crane-0.2.0`.
- All 40 distinct definitions in the selected Lion/Crane 40/40 decks have implementations and period scans.
- Rules authority, automated payments/turn flow, choices, attachments, Favor, battles, victory, deterministic replays, a heuristic opponent and private-room networking exist.
- UI baseline: full card faces, physical piles/stacks, pan/zoom, optional fullscreen, enlarged navy mat, upright lanterns, bitmap phase bar with Musashi Brush headings. Preserve this aesthetic while Alfredo develops UI/QoL improvements.
- The published rules profile is a reproducible playtest profile, not a certified final historical tournament legality claim.

## Current focus

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
