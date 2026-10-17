# Contributing and rules review

This public repository (moved to `l5r-emperor-dev/l5r-emperor`; see the [copy-paste migration prompt for AI coding assistants](docs/REPO_MOVED.md#prompt-for-ai-coding-assistants) if your clone predates that) coordinates the owner, invited rules reviewers, Codex and Claude Code. It does not make separate AI conversations share memory. GitHub issues, branches, PRs and checked-in status are the durable record.

For playing and reporting mechanics, begin with `docs/RULES_REVIEW.md`. For development, read `AI_HANDOFF.md` and `docs/PROJECT_STATUS.md`.

## Automatic agent workflow

Alfredo leads UI/QoL; mdornbrook leads mechanics/rules. Codex and Claude Code load the shared `AGENTS.md` policy and handle context refresh, task branches, handoffs and PRs for you. See [the operating guide](docs/COLLABORATION.md). You should normally just describe the improvement.

Agents run `python3 tools/collab.py context` before implementation, write a per-task handoff with `handoff`, and run `audit --working` before the final commit. Use `python` on Windows if needed. Mendon may develop in any game area and merge his own PRs after tests/CI without Alfredo approval; other authors follow lane review routing; per-task handoffs avoid everyone editing one global memory file. GitHub labels/reviewer requests are automatic; the configured `Protect Main` ruleset and its enforcement limits are described in [PUBLIC_REPO.md](docs/PUBLIC_REPO.md).

## A shared task workflow

1. Open an issue using **Rules / gameplay discrepancy** or **Development task**. Capture a concrete expected outcome. An expert can report findings without writing code.
2. Assign or comment on the issue before starting. One person/agent should own a bounded task at a time. Create a branch from current `main`, named `codex/<issue>-<topic>`, `claude/<issue>-<topic>`, or `<name>/<issue>-<topic>`.
3. Concurrent sessions use separate clones or worktrees. Do not point two active agents at the same working directory. Coordinate shared engine/content files in the issue.
4. Reproduce before fixing. For a rules discrepancy, preserve the exact cards, printed versions, turn/phase/priority, costs and choices. Cite the period rules passage or selected printing. A later Oracle edit is not automatically the chosen Emperor rule.
5. Open a draft PR early enough to make work visible. Include the linked issue, behavioral change, tests actually run, and remaining uncertainty. Request human rules review on disputed mechanics. The repository, its issues and PRs are public: do not put secrets, seat tokens, room storage or unreleased material in them.
6. Merge through GitHub once relevant checks pass and the configured review policy is met. For Mendon’s PRs, Alfredo’s review is optional and no separate merge approval is needed. Refresh other branches from `main` before continuing. Do not force-push `main` or discard another session's edits.
7. Update `docs/PROJECT_STATUS.md` when a milestone/known blocker changes. Use `docs/handoffs/TEMPLATE.md` for interrupted or multi-session work, and link that handoff from the issue/PR.

A branch can be prepared with ordinary Git:

```sh
git fetch origin
git switch main
git pull --ff-only
git switch -c codex/123-payment-timing
# Edit, test, commit, then:
git push -u origin codex/123-payment-timing
```

Run `git status` first and handle existing local work before switching. Never blindly reset a dirty checkout.

For a separate concurrent checkout, from a clean repository:

```sh
git fetch origin
git worktree add -b claude/124-reconnect ../l5r-reconnect origin/main
```

Each worktree may need its own ignored bootstrap tools/caches; do not commit those directories. To share one Godot install across worktrees instead of bootstrapping each, set `L5R_GODOT` to the Godot executable (it overrides `.tools/godot`; `tools/env.sh` then copies the install's four bundled NuGet packages into the worktree's `.tools/godot-nuget` so restore works offline); scripts that need Godot stop with an explicit message when it is missing. See [known pitfalls](docs/PITFALLS.md) for traps a fresh clone/worktree or session commonly hits (Godot's first-run import, a locked `docs/CARD_INVENTORY.xlsx`, gitignored scan working copies, the catalogue art pack, stale PR bases, and `.tools`/`.cache` sizing) before spending time rediscovering them.

## Checks and evidence

- Rules/content/network changes: relevant regression plus `bash tools/check.sh --skip-client`.
- Add the tests that prove the behavior you changed, not tests that restate how it is wired. A test that only checks a card or hook is registered, or that a method calls another, costs every CI shard and nightly run time and catches nothing a behavioral case would not. (Forge's contributor guide makes the same point about coding agents.)
- Client changes: compile/import and the native UI/input checks relevant to the change. Inspect actual screenshots.
- Record platform, resolution and exact command. Say explicitly when a check was not run.
- GitHub Actions runs Mac/Windows foundation checks. A green Windows CI job does not verify real Windows GPU/gameplay behavior.
- Attach sanitized replays or minimal reproducible scenarios. Seat tokens, room databases and credentials remain local.

## Source versus release downloads

Source, runtime assets, the selected Lion/Crane source scans, data provenance, editable art and documentation belong in Git. `.tools/`, `.cache/`, build output and user profiles do not.

The larger optional Emperor research-image collection is excluded from Git and kept out of Git. Releases on this public repository are world-downloadable, so the owner decides whether it is published; see AGENTS.md. It is not required to run, test or build the existing Lion/Crane game. See `assets/reference/oracle/emperor-edition/README.md`. Release downloads also contain runnable Mac and Windows packages so reviewers do not need the SDK/editor.

Publish a development release from a commit/tag accepted under the configured review policy, build with the documented exporter, include package checksums and state which platforms were actually run. Do not silently replace release packages with untracked builds.
