# Working together without repeating setup

Alfredo (`alfredoh7`) leads UI and quality of life. `mdornbrook` leads mechanics and rules. Open the cloned project in Codex or Claude Code and describe the improvement normally. On an existing installation, pull this setup once and start a fresh agent session so the new startup instructions load. Both tools then follow the same `AGENTS.md`; Claude imports it through `CLAUDE.md`.

The agent runs the workflow; people make product and rules decisions. One shared repository, short task branches, and separate local checkouts keep both lanes independent. A new issue identifies a task; the branch/draft PR shows who is working on it. Each agent refreshes current GitHub work before starting and before integration. A pushed per-task handoff records actual changes, tests and remaining work. Durable decisions get their own small file under `docs/decisions/`.

## Three agent commands

```sh
python3 tools/collab.py context
python3 tools/collab.py handoff --lane ui --summary "Explain the actual change." --validation "Record actual checks and results." --remaining "None; ready for review." --issue "ISSUE_URL"
python3 tools/collab.py audit --working
```

Use `python` on Windows if `python3` is unavailable. Python is already a project prerequisite; this helper uses only the standard library. `context` refreshes Git refs and reads GitHub, never switches branches, merges, edits code or publishes anything. It discovers system `gh`, or the existing ignored local CLI. The agents can use their GitHub connector plus Git instead if `gh` is unavailable. No additional account, token service, database, daemon, custom MCP server or global Git hook is required.

A handoff command writes a local JSON file. The agent commits and pushes it with the task, then includes its evidence in the PR. Automatic audit checks that the changed handoff matches the branch and affected lane, has meaningful summary/validation/remaining fields, and the branch includes the specified current base. It checks structure, not truth: fabricated test claims still require review. Agents should never invent results to satisfy the check.

## Ownership and review

The single executable path map is `.github/collaboration.json`. UI artwork/client presentation belongs to Alfredo; engine/server/card data belongs to mdornbrook. State/query models, client/server boundaries, build and workflow files, unknown paths and changes spanning lanes get shared review. A metadata-only GitHub Action applies `lane:ui`, `lane:rules` or `lane:shared` and requests the responsible lead(s), excluding the PR author. Drafts receive labels; reviewer requests happen when ready. Repeated pushes don't repeatedly request someone who already participated.

File ownership is a starting point. A UI change that alters legal targeting or automatic payments still needs the rules lead even if it touches only a UI file. For a shared change, describe the proposed contract in the issue first and ask the other lead through the PR. Each lane lead can accept their own isolated work with evidence; GitHub does not allow self-review. Do not make the other person a bottleneck for every minor change.

The current GitHub account returned HTTP 403 for private repository rulesets and requires an upgrade for that feature. Therefore **passing checks and reviews are a team policy, not an enforced merge lock**. The repository stays private. The automation cannot prevent a collaborator from directly pushing main or ignoring a failed check. It never merges or upgrades the account. If protection becomes available later, require the collaboration and desktop checks and block force pushes through repository settings.

## Recovery is the agent's responsibility

- **Dirty working tree:** preserve it. Continue the same task or create a separate worktree for unrelated work; never reset/stash someone else's work automatically.
- **New main changes:** fetch, inspect, integrate on the task branch and retest affected behavior. Never silently overwrite another contributor's work.
- **Overlapping tasks:** inspect the other PR/files, coordinate in the issue, or choose independent work. Issues are coordination records, not an atomic distributed lock.
- **Missing CLI/auth/network:** use a working connector, or continue safe local work with an explicit offline handoff. Do not claim fresh remote state, successful upload or merge while disconnected. User action is only necessary when their credentials/decision are actually missing.
- **Agent stops:** the next session reads the task branch, handoff and PR. Unpushed work is available only in that checkout. No tool can recover an explanation that was never recorded.
- **Already-open agent session:** instructions may have been loaded only at session start. Pull and start a fresh session once when installing this setup; future tasks must refresh shared context even within a long chat.
- **Other LLM tools:** configure that tool's supported project entrypoint to read `AGENTS.md`; generic chatbots don't automatically inspect repositories. This setup is specifically tested for Codex/Claude Code and GitHub, not every AI product.

## Safe automation

The review router runs on `pull_request_target` using **only the trusted base commit**, with no PR-head checkout or execution. PR titles, branch names and descriptions are not interpolated into shell commands. It reads filenames, applies labels and requests reviewers. The PR CI uses a read-only token and executes the ordinary candidate tests. New/untracked/deleted/renamed files are included in local auditing; renamed paths retain their old lane for review routing. No background process watches your computers.

Official behavior references: [Codex project instructions](https://learn.chatgpt.com/docs/agent-configuration/agents-md), [Claude project memory and AGENTS import](https://code.claude.com/docs/en/memory). See `docs/COLLABORATION_VALIDATION.md` for the actual tested scenarios and remaining limits.
