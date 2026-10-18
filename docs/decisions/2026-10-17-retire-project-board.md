# Retire the optional project board

- Date: 2026-10-17
- Status: accepted by Alfredo's explicit request to sunset the board
- Lane: shared collaboration and operations; rules and gameplay are unchanged
- Issue: https://github.com/l5r-emperor-dev/l5r-emperor/issues/994

## Decision

Use GitHub issues, assignees, ownership notes, dependencies, PRs, checks and the
existing repository handoffs as the coordination workflow. Remove board claims,
board-token setup and Netlify availability as prerequisites. Preserve Mendon's
standing development authority and existing review/validation requirements.

The extra dashboard duplicates coordination records and adds a hosting and
maintenance dependency. Alfredo chose to retire it rather than restore that
service. Historical board claims must not automatically reassign GitHub tasks,
close issues, imply human acceptance or prevent contributors continuing their work.

Disable the board's hosting and archive its separate private source repository
without deleting its data or history. Do not change unrelated Netlify projects
or account-wide billing. Remove active client calls; keep a clear local retirement
response for older commands so no caller mistakes a failed claim for success.

## Affected surfaces

`AGENTS.md`, `AI_HANDOFF.md`, active collaboration/status/roadmap guidance,
`tools/collab.py`, the retired `tools/board.py` entry point and their regressions.
`docs/PROJECT_BOARD.md` now points to GitHub; the former guide is archived.
