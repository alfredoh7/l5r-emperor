# Governance-lite

Rung 9 of [the PR ladder](PR_LADDER.md) supplies a review recipe, four reviewer profiles and a read-only contract ledger. Invoke `/governance-lite` in Claude or `$governance-lite` in Codex from a repository session. The canonical skill is `.claude/skills/governance-lite/SKILL.md`; `tools/skills/sync_skills.py` mirrors the skill and its profiles into `.agents/skills`. Claude also discovers four wrappers under `.claude/agents/governance-*.md` using its [project subagent format](https://code.claude.com/docs/en/sub-agents). These wrappers were inspected/validated here; an actual Claude session launch was not tested. The four-role acceptance review used Codex delegation.

The roles are rules-versus-core-rules, hidden-information leak, negation-and-edge-case and client contract. Each receives the same endpoints and evidence but follows its own profile. Limited concurrency means queuing roles, not omitting one. Without delegation, label four sequential passes honestly; never claim independent agents ran. Reviewers inspect and report; they do not edit, publish or settle rules. This is advisory evidence under AGENTS.md, not branch protection or a new owner-approval gate. Mendon's standing authority remains unchanged.

## Contract ledger

```sh
python tools/collab.py ledger --base origin/main --head HEAD --format markdown
python tools/collab.py ledger --base origin/main --working
```

JSON is the default; `--format markdown` produces a readable list. Use `python3` on Mac. Git and Python 3.12+ are the only dependencies. Ref names resolve to immutable commit IDs before analysis. Comparison uses the merge base, like a PR's three-dot diff, so unrelated new changes on main are excluded. The report records supplied base, actual merge base, head and whether working files were included. `--working` requires head to resolve to current HEAD and reads final staged/unstaged/untracked Core source, including deletions. It does not describe the index separately.

The command reads `src/L5R.Core/*.cs` recursively from raw Git blobs in memory, ignoring archive export attributes and committed symlinks. It never checks out a branch, extracts an archive, fetches, contacts GitHub, runs reviewed code, or writes a report/handoff. Redirect stdout explicitly when saving evidence. Missing refs/read failures exit nonzero with a recovery message; no partial success is claimed.

| Category | Detection |
|---|---|
| Pending choices | Literal or qualified string-constant `new PendingChoice(...)` and `PendingChoice = new(...)` types; unqualified names are not guessed |
| Commands | Explicit `new GameCommand(...)`, target-typed GameCommand initializers, the repository's `Command(...)` factory and cases in `switch (command.Type)` |
| Decider specs | Constants in `*Specs` registries providing `IsDeciderSpec` |
| View fields | Public positional record properties and declared public properties/fields reachable from MatchView/CommandResult or types ending View, Quote or Option |
| State fields | The same declaration inventory reachable from MatchState, including feature state and partial classes |
| Command fields | Public members reachable from GameCommand |
| Payload sites | Named Choice/TargetId/CardIds/SourceIds arguments on detected command calls and `command.Choice == ...` sites |

Compare member identity across all Core files, not added lines alone. Moving a property to a partial file or moving an unchanged constructor does not create a new field/command. Added, removed and changed declaration types are listed with source paths and one-based lines. Comments and literal strings containing code are not declarations. Generic arguments and multiline records/defaults are handled without treating methods/locals as DTO fields.

This is a heuristic inventory, not Roslyn, a wire-schema generator or a completeness certificate. It does not expand inheritance, macros/conditional compilation, arbitrary factories, custom serializers/attributes, dynamic kinds, or runtime string construction. It does not infer changed defaults/initializers, event semantics, visibility, timing, or UI handling. Existing payloads can acquire a new meaning without producing a ledger entry. Public source fields can be engine-private; a candidate view field may still be hidden by projection. Unsupported syntax may be missed. An empty ledger therefore does not prove no client impact. The four reviewers must inspect semantics and add a human-readable supplement.

The normal collaboration Python test discovery exercises extraction and temporary Git repositories; the normal hooks discovery validates skill mirrors and reviewer-profile links. The existing hosted collaboration job adds the committed PR ledger to its Actions summary, without a new job or self-hosted runner work. No new game suite, daemon or automatic external review is added. The PR template adds a client-lane checklist and asks for the actual items or an explained absence; the checkbox is not automatically enforced and is not native UI acceptance.

## #504 acceptance

The hand-written comparison source is [#504](https://github.com/l5r-emperor-dev/l5r-emperor/pull/504) and its [mechanics-batch client-lane section](decisions/2026-09-25-mechanics-batch.md#client-lane-alfredo). `tools/collaboration/fixtures/pr504-contract.json` records the expected named contract independently of the ledger and preserves its semantic supplement. The seven client-facing copy/payment additions are the five CardView copy fields and the two quote Sources fields. The additional EntryCostOption and PaymentSource members are supporting payload structure verified against the historical source.

Fetch the historical PR only when needed and authorized, then reproduce the read-only comparison:

```sh
git fetch origin refs/pull/504/head:refs/remotes/origin/pr-504
python tools/collab.py ledger --base 506a4c6b6b8d078c41be2662b1e62a9f42375228 --head 90aa0eeff14a873e130e2514db33ea3cd4efc08e
```

The expected fixture gives the actual historical base/head; do not compare against today's main, which already contains this pass. The acceptance record in [the Rung 9 decision](decisions/2026-09-29-rung9-governance-lite.md) distinguishes exact mechanical matches from semantic obligations the reviewers must supply. Matching named identifiers does not certify that #504's client implementation or proposed readings were accepted.
