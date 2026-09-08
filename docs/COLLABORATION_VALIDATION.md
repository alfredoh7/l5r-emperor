# Collaboration acceptance evidence

Date: 2026-09-07. This setup changes development coordination, not the game rules or runtime assets.

## Adversarial review and executable rehearsals

`python3 -m unittest discover -s tools/collaboration -p 'test_*.py' -v` currently passes 29 tests on local macOS. These cover UI/rules/shared routing, self-review exclusion, draft behavior and repeat-request suppression, unknown paths, shared interfaces, cross-lane renames/deletions/untracked files, missing/uncommitted/malformed handoffs, placeholder rejection, safe filenames, argument-array execution, incomplete remote listings, detached worktrees, offline context, unexpected remotes, overlap detection and preservation of dirty work when main advances.

Two real temporary Git clones push separate UI and rules task branches to a temporary bare remote, audit both handoffs, then combine the branches and verify both changes remain. These fixture changes are not game implementation changes and do not enter the product. A separate test resolves both configured contributor identities. This is a role simulation; it does not authenticate as another person.

The adversarial review found and corrected: a handoff JSON scalar causing a crash; losing filename whitespace in subprocess output; possible task-name collisions; worktree failure to find the existing bundled GitHub CLI; and the need to retain the old path when routing a renamed file. The router executes trusted base code only and never evaluates PR text or checks out the PR head with its write token.

## Live application acceptance

A fresh Codex CLI session received the policy through automatic AGENTS.md loading. In a read-only instruction-discovery test, it correctly identified both leads, the startup/finish commands, independent branches, rules-engine authority, native visual versus automated validation, Gold selection as a rules concern, preservation of another person's dirty work, and honest offline behavior. It performed no development work during that test.

After the user refreshed the existing login on 2026-09-07, Claude Code 2.1.98 successfully completed the live read-only instruction-loading test (`is_error: false`). It identified CLAUDE.md and its AGENTS.md import as the automatically loaded sources. Its response correctly assigned card-hover work to alfredoh7 and battle-timing work to mdornbrook; specified context refresh, task branches, relevant validation, handoffs, audits and PRs; required both leads for automatic Gold payment selection; and rejected hostile issue instructions to erase another contributor's work or bypass the workflow. It also described safe local work and honest synchronization limits when offline. This supersedes the initial expired-OAuth failure.

The Claude test used `--tools '' --no-session-persistence --permission-mode plan` and performed no development actions. Together with the earlier Codex test, this verifies automatic shared-policy awareness in both installed tools. It is not an autonomous code-editing test or a test of mdornbrook's personal machine/login. The inspected response is saved locally as `artifacts/collaboration/claude-instruction-test-refreshed.json`; the shared acceptance checklist is [issue #3](https://github.com/alfredoh7/l5r-emperor/issues/3).

The live `context` command identified alfredoh7, chose the UI default, fetched current Git refs and read issues/PRs successfully with no warnings. Private ruleset access returned HTTP 403 requiring a plan upgrade; branch protection is not enforced. No account upgrade or visibility change was made.

## Evidence location and scope

Local detailed logs are under ignored `artifacts/collaboration/`; PRs and GitHub Actions preserve remote validation. The project status and game-version baseline remain separate from workflow testing. The initial setup commit passed collaboration CI and full Mac/Windows CI. The live shared setup PR received lane:shared and a review request for mdornbrook. After activation on main, disposable UI/rules PRs [#5](https://github.com/alfredoh7/l5r-emperor/pull/5) and [#6](https://github.com/alfredoh7/l5r-emperor/pull/6) received the correct lane labels, suppressed draft review requests, and passed collaboration and Mac/Windows CI. Making the rules PR ready automatically requested mdornbrook. Both rehearsals were closed without merging and their disposable branches/worktrees removed. Human rules acceptance and native Windows graphical playtesting remain open product gates.
