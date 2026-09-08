# Mendon may develop across the whole game

Status: Accepted, 2026-09-08, by explicit request from repository owner Alfredo.
Lane: shared. Issue: https://github.com/alfredoh7/l5r-emperor/issues/60
PR: https://github.com/alfredoh7/l5r-emperor/pull/61

Mendon (`mdornbrook`) and agents working on his tasks may change all game areas, including menus, UI, shared functions, rules, server code and custom-deck practice setup, without Alfredo approval. After relevant tests and CI pass, his agent may merge his own task PR without separate confirmation. Other-lead review is optional/advisory. Roles describe normal focus, not exclusive file ownership.

The GitHub API already reports Mendon has repository write access. No administrator permission or billing/visibility change is needed. The previous restriction came from the shared agent instructions and reviewer routing. `.github/collaboration.json` now records the grant, `tools/collab.py` applies it to authenticated context and GitHub PR-author routing, and AGENTS.md/CLAUDE.md provide the same instructions to both agents.

Task branches, pull requests, truthful checks, handoffs and coordination of concrete concurrent edits remain. Engineering boundaries preserve authoritative rules in Core/server and the reproducible Lion/Crane option; they do not prohibit adding verified cards or custom-deck playtesting. This decision grants development authority; it does not implement the deck loader itself.

Alfredo's ordinary review routing is unchanged. The owner explicitly authorized this policy rollout, which may merge after CI without another approval; this does not authorize merging unrelated open game changes. Existing agent sessions should safely update from main and reload project instructions once to pick up the new policy.
