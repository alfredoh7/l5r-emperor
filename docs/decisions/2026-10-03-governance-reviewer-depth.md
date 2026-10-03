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

## Round 2: final profiles on three further PRs

Twelve Sonnet reviewers (four roles x #581, #578, #595), run after the profile edits above. Tokens / tool calls:

| PR (kind) | client-contract | hidden-information | negation/edge | rules-vs-core | Total |
|---|---|---|---|---|---|
| #581 (Core view + client) | 30.6k / 9 | 21.8k / 4 | 29.2k / 9 | 35.1k / 6 | 116.7k |
| #578 (payment quote + client) | 30.8k / 10 | 31.0k / 14 | 28.2k / 9 | 42.5k / 17 | 132.5k |
| #595 (client probe code only) | 20.6k / 5 | 22.4k / 7 | 20.4k / 4 | 27.5k / 5 | 90.9k |

Role means: client-contract 27.3k, hidden-information 25.1k, negation 25.9k, rules-vs-core 35.0k. Overall mean 28.3k (baseline round 23.6k).

Observed compliance: 12/12 returned Files read / Not read; 12/12 ran the `ContentVersion` check (all judged no bump needed; the changes were additive or client-only); rules-vs-core opened `RULINGS.md` and `ROUTED_CONCERNS.md` in 3/3 (0/2 before the wrapper change).

Findings checked against the code by hand:

- Confirmed, found by 1 of 4 (client-contract): #578 fills `GoldQuote.Sources` only for recruit quotes from `ToGoldQuote`; the two `chooseSearchPayment` producers (`ShadowsEmbraceBatch6.cs:326`, `EmperorRemainingC.cs:527`) build quotes without `Sources:`.
- Confirmed, found by 2 of 4: #581 `EventDecisionUI.cs:50` dereferences `view.PendingHarshChoices!` with no null guard.
- Confirmed: #578 removed the client's crane-gold/dragon-gold printed fallback (`Production` is now `card.GoldProduced ?? 0`); 3 of 4 flagged the risk. Whether Core always populates `GoldProduced` for Strongholds was not verified.
- Not confirmed: #581 alternative-cost path may submit a null target (negation reviewer, marked unverified by it).

Limits: no seeded-defect recall test, so recall is unknown; only findings the reviewers raised were checked. `omitClaudeMd` still untested.

Follow-up from round 2: the client-contract profile now sweeps every producer of a changed payload type (the #578 `chooseSearchPayment` gap) and requires clients to tolerate absent additive view fields (the #581 `PendingHarshChoices!` dereference). Not re-measured with agents.
