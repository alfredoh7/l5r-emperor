# Collaboration acceptance evidence

Date: 2026-09-07. This setup changes development coordination, not the game rules or runtime assets.

## Adversarial review and executable rehearsals

`python3 -m unittest discover -s tools/collaboration -p 'test_*.py' -v` currently passes 29 tests on local macOS. These cover UI/rules/shared routing, self-review exclusion, draft behavior and repeat-request suppression, unknown paths, shared interfaces, cross-lane renames/deletions/untracked files, missing/uncommitted/malformed handoffs, placeholder rejection, safe filenames, argument-array execution, incomplete remote listings, detached worktrees, offline context, unexpected remotes, overlap detection and preservation of dirty work when main advances.

Two real temporary Git clones push separate UI and rules task branches to a temporary bare remote, audit both handoffs, then combine the branches and verify both changes remain. These fixture changes are not game implementation changes and do not enter the product. A separate test resolves both configured contributor identities. This is a role simulation; it does not authenticate as another person.

The adversarial review found and corrected: a handoff JSON scalar causing a crash; losing filename whitespace in subprocess output; possible task-name collisions; worktree failure to find the existing bundled GitHub CLI; and the need to retain the old path when routing a renamed file. The router executes trusted base code only and never evaluates PR text or checks out the PR head with its write token.

## Live application acceptance

A fresh Codex CLI session received the policy through automatic AGENTS.md loading. In a read-only instruction-discovery test, it correctly identified both leads, the startup/finish commands, independent branches, rules-engine authority, native visual versus automated validation, Gold selection as a rules concern, preservation of another person's dirty work, and honest offline behavior. It performed no development work during that test.

Claude Code is installed, but the initial live test returned an expired OAuth token. The CLAUDE.md import is structurally checked against the documented import mechanism; live Claude behavior remains pending until the existing login is refreshed. Do not describe that test as passed until the actual retry succeeds.

The live `context` command identified alfredoh7, chose the UI default, fetched current Git refs and read issues/PRs successfully with no warnings. Private ruleset access returned HTTP 403 requiring a plan upgrade; branch protection is not enforced. No account upgrade or visibility change was made.

## Evidence location and scope

Local detailed logs are under ignored `artifacts/collaboration/`; PRs and GitHub Actions preserve remote validation. The project status and game-version baseline remain separate from workflow testing. Final CI and live routing outcomes are recorded in the setup PR. Human rules acceptance and native Windows graphical playtesting remain open product gates.
