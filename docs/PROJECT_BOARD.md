# Project board retired

Alfredo retired the optional L5R Project Board on **2026-10-17**. Coordinate
through [GitHub issues](https://github.com/l5r-emperor-dev/l5r-emperor/issues)
and [pull requests](https://github.com/l5r-emperor-dev/l5r-emperor/pulls).

- Issues record scope, assignments, dependencies and who is taking a task.
- PRs record implementation, checks, review and remaining work.
- Keep using `tools/collab.py context`, `handoff` and `audit`, then publish the
  handoff with the task branch. `handoff` remains local until pushed.
- Record human acceptance explicitly on the matching issue/PR. A passing check,
  merge or archived board claim does not provide it.

No board account, token, claim or Netlify service is required. Old `L5R_BOARD_*`
settings are ignored by current collaboration tools. `tools/board.py` is a local
retirement notice: legacy commands fail without sending requests or credentials.

The Netlify project is explicitly disabled rather than merely waiting for its
usage allowance to reset. Its configuration and data are retained, and its
separate private [source repository](https://github.com/alfredoh7/l5r-board) is
archived with Actions disabled. Other Netlify projects and account billing are
outside this retirement. No game issue, assignee, open PR or acceptance is changed
by archiving the board; its historical claims remain historical.

The [previous guide](archive/project-board-before-retirement.md) and Git history
are retained for reference. See the [accepted retirement decision](decisions/2026-10-17-retire-project-board.md)
and [task #994](https://github.com/l5r-emperor-dev/l5r-emperor/issues/994).
