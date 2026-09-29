# Separate nightly validation tier

Status: implemented for review in [PR #534](https://github.com/alfredoh7/l5r-emperor/pull/534), following Mendon's request to implement Rung 7 of [#509](https://github.com/alfredoh7/l5r-emperor/issues/509). Shared CI/process work, with optional courtesy review requested from Alfredo. This is not a new historical rules ruling.

## Decision

Keep extended game validation outside the normal PR path. A scheduled/manual hosted Windows workflow runs 32 consecutive reproducible seeds for both starter and current live published profiles, checks full-state and per-seat replay determinism, and probes concrete enabled commands across every embedded published card definition. The catalogue uses detached copies of bounded fixtures in Action, Battle and Dynasty for both owners/viewers. It records passive/unsatisfied cards with zero source commands rather than claiming every ability or interaction was exercised.

The validation job has read-only repository access and a 45-minute bound. A separate job reports failed main runs, including bootstrap/build/timeout failures with no runner summary, to one marker-owned `nightly-broken` issue. It retains first and latest evidence, reuses an existing issue, and never automatically closes a failure. Pause normal merges while a production failure is open; recovery can require an owner-directed repair merge, followed by a complete successful main nightly before closure. This is a team convention without branch protection.

## Evidence and limits

The deliberate local runner failure exited 1 and the actual GitHub reporter automatically opened [acceptance issue #535](https://github.com/alfredoh7/l5r-emperor/issues/535). Repeating the identical report reused it; the clearly marked acceptance issue was closed after verification. Eight reporter regressions cover creation, pagination, idempotence, first-evidence retention, acceptance separation, unrelated issues and API/metadata failures. This proves the runner/reporter path locally with real GitHub issue writes; it does not claim the new scheduled workflow ran before merge. GitHub enables its scheduled/manual triggers once the workflow reaches the default branch.

The initial complete catalogue sweep found two instances of the same real defect: Houken experienced's enabled Open action dereferenced a nonexistent battle province. The fix preserves its existing Battle behavior and moves it home outside battle, by guarding the existing movement case. Focused Open/Battle regressions both pass. Rung 6's required formatting of the touched legacy ability file is isolated in a separate whitespace-only commit; the semantic fix changes only the null-unsafe battlefield condition. The original movement dispatch and its source-derived movement-flag audit stay intact.

Starter seed 20260912 reached 1,600 accepted decisions without victory; after decision 468 both bots only passed/ended Dynasty. That heuristic limitation is recorded in [#536](https://github.com/alfredoh7/l5r-emperor/issues/536). The soak measures legality and determinism for the entire bounded sequence, counts completed/capped games separately, and preserves capped journals. It does not require every modest heuristic game to end in victory or suppress rejected commands/replay failures.

Complete baseline and rules-check results will be recorded before the PR is marked ready. The catalogue is a consistency probe, not expert rules acceptance or exhaustive card-combination coverage. The shared Mac runner, Godot imports, client flows and normal PR test loop are unchanged.

Affected files: `.github/workflows/nightly.yml`, `tests/L5R.Nightly.Tests/`, `tools/nightly/`, the Core test friend declaration, the guarded Houken dispatch and focused regressions, `AGENTS.md`, and [NIGHTLY_CHECKS.md](../NIGHTLY_CHECKS.md).
