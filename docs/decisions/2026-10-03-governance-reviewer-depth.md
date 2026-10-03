# Governance reviewer depth and model choice

Status: accepted for the recipe changes; model pins and `omitClaudeMd` proposed, not adopted. Lane: rules (Mendon). Issue: [#608](https://github.com/alfredoh7/l5r-emperor/issues/608).

## Decision

- Never skip a governance role because the ledger is empty or the diff is small.
- Every profile now carries the versioned-contract check (`MatchEngine.ContentVersion`, replays, journals, committed fixtures) and the reviewers return **Files read** / **Not read** lists.
- Reviewers search before reading (`Grep` with `files_with_matches`/`head_limit`, then `Read` with `offset`/`limit`) and never read `CardAbilities.cs`, `MatchEngine.cs` or `PublishedContent.cs` whole.
- Do not pin models in agent frontmatter yet; see the limits below.

## Evidence

One measurement subject: PR #584 (`dfd106eb~1`..`e39a0a6e`, 6.4 KB source diff, empty ledger). Tokens are the harness's `subagent_tokens`; per-turn transcripts were unavailable, so input and output are not separated. One run per cell.

| Reviewer | Baseline | Updated recipe (Sonnet) | Updated recipe (Opus) |
|---|---|---|---|
| rules-vs-core | 24.2k / 4 calls | 23.0k / 5 | not run |
| hidden-information | 19.0k / 4 | 36.9k / 7 | 49.8k / 11 |
| negation/edge-case | 22.6k / 4 | 24.5k / 6 | 51.1k / 12 |
| client-contract | 28.9k / 9 | 26.5k / 8 | not run |

Startup floor: 10.5k for a 4-tool governance agent, 27.8k Explore, 45.4k general-purpose.

Findings that mattered:

- Card ids changed meaning with no `ContentVersion` bump (old replays/journals load and diverge). Baseline: client-contract only. Updated: client-contract, negation (Sonnet), negation (Opus, which also traced `RoomManager.LoadAsync`). Hidden-information and rules-vs-core missed it because the check lived in `SKILL.md`, which reviewers do not read; it is now in each profile.
- `_state.Cards.Values` target lists (`CardAbilities.cs:11213`, `card-in-province`) expose deck-list order through option order and the default command. Found only by the Opus hidden-information run, with a concrete path and fix. Not fixed in this PR; it belongs to #584's follow-up.
- The 2000-id pool overflow was reported by every run; Opus negation showed Review lets such a deck through.
- The rules-vs-core reviewer skipped `RULINGS.md` in both baseline and updated runs. The agent wrapper now names those reads explicitly; not re-measured.

## Limits

One PR, one run per cell, Sonnet vs Opus compared only on two roles. Opus roughly doubled tokens and found the more concrete leak, but a single sample does not justify pinning. `omitClaudeMd` could not be tested: this Claude Code session does not discover agent files created mid-session, so it needs a fresh session. The docs summary of subagent context (no parent history; `CLAUDE.md`/`AGENTS.md` and git status loaded; no shared cache between parallel agents) came from a research subagent and was not independently verified.
