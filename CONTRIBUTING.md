# Contributing and rules review

This private repository coordinates the owner, invited rules reviewers, Codex and Claude Code. It does not make separate AI conversations share memory. GitHub issues, branches, PRs and checked-in status are the durable record.

For playing and reporting mechanics, begin with `docs/RULES_REVIEW.md`. For development, read `AI_HANDOFF.md` and `docs/PROJECT_STATUS.md`.

## Automatic agent workflow

Alfredo leads UI/QoL; mdornbrook leads mechanics/rules. Codex and Claude Code load the shared `AGENTS.md` policy and handle context refresh, task branches, handoffs and PRs for you. See [the operating guide](docs/COLLABORATION.md). You should normally just describe the improvement.

Agents run `python3 tools/collab.py context` before implementation, write a per-task handoff with `handoff`, and run `audit --working` before the final commit. Use `python` on Windows if needed. Cross-lane work gets joint review; per-task handoffs avoid everyone editing one global memory file. GitHub labels/reviewer requests are automatic; protected-branch enforcement is unavailable on the current private plan.

## A shared task workflow

1. Open an issue using **Rules / gameplay discrepancy** or **Development task**. Capture a concrete expected outcome. An expert can report findings without writing code.
2. Assign or comment on the issue before starting. One person/agent should own a bounded task at a time. Create a branch from current `main`, named `codex/<issue>-<topic>`, `claude/<issue>-<topic>`, or `<name>/<issue>-<topic>`.
3. Concurrent sessions use separate clones or worktrees. Do not point two active agents at the same working directory. Coordinate shared engine/content files in the issue.
4. Reproduce before fixing. For a rules discrepancy, preserve the exact cards, printed versions, turn/phase/priority, costs and choices. Cite the period rules passage or selected printing. A later Oracle edit is not automatically the chosen Emperor rule.
5. Open a draft PR early enough to make work visible. Include the linked issue, behavioral change, tests actually run, and remaining uncertainty. Request human rules review on disputed mechanics. A public repository or public issue is not needed.
6. Merge a reviewed PR through GitHub once relevant checks pass. Refresh other branches from `main` before continuing. Do not force-push `main` or discard another session's edits.
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

Each worktree may need its own ignored bootstrap tools/caches; do not commit those directories.

## Checks and evidence

- Rules/content/network changes: relevant regression plus `bash tools/check.sh --skip-client`.
- Client changes: compile/import and the native UI/input checks relevant to the change. Inspect actual screenshots.
- Record platform, resolution and exact command. Say explicitly when a check was not run.
- GitHub Actions runs Mac/Windows foundation checks. A green Windows CI job does not verify real Windows GPU/gameplay behavior.
- Attach sanitized replays or minimal reproducible scenarios. Seat tokens, room databases and credentials remain local.

## Source versus release downloads

Source, runtime assets, the selected Lion/Crane source scans, data provenance, editable art and documentation belong in Git. `.tools/`, `.cache/`, build output and user profiles do not.

The larger optional Emperor research-image collection is excluded from Git and retained as a private Release ZIP. It is not required to run, test or build the existing Lion/Crane game. See `assets/reference/oracle/emperor-edition/README.md`. Release downloads also contain runnable Mac and Windows packages so reviewers do not need the SDK/editor.

Publish a development release from a reviewed commit/tag, build with the documented exporter, include package checksums and state which platforms were actually run. Do not silently replace release packages with untracked builds.
