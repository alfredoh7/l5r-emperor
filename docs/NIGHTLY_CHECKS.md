# Nightly validation

Rung 7 of [the PR ladder](PR_LADDER.md), tracked in [#509](https://github.com/alfredoh7/l5r-emperor/issues/509), adds a separate scheduled/manual workflow. Normal PR-time game checks are unchanged. It uses a hosted Windows runner and the pinned rules SDK, without Godot imports or the shared Mac runner.

## Checks and reproduction

`.github/workflows/nightly.yml` runs daily at 03:23 UTC, or by manual dispatch. GitHub activates scheduled/manual workflows after the workflow is on the default branch; see [GitHub's trigger documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows). A manual branch run can exercise code, but only failed main runs create a production nightly-broken issue.

Build and run locally with the pinned .tools/dotnet SDK on PATH:

```sh
dotnet build tests/L5R.Nightly.Tests/L5R.Nightly.Tests.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
dotnet tests/L5R.Nightly.Tests/bin/Debug/net10.0/L5R.Nightly.Tests.dll
```

The default is 32 consecutive reproducible seeds beginning at 20260906, for 64 completed starter/current published Lion–Crane AI matches. Each match must finish within 1600 accepted decisions. Replay round-trips through JSON and is compared at every 100 decisions and completion, using the full authoritative state (including hidden state, RNG and pending choices) and both seats' permitted views. Only permitted views and offered actions are inputs to the AI. Failures preserve the accepted command journal for reproduction.

The catalogue check discovers every embedded published definition, including created-only definitions, automatically. It places each card into Action, Battle and Dynasty fixtures for both controllers and queries both viewers. Units and attachment hosts are explicit in-play synthetic support units; the tested card retains its actual published definition. The fixture has bounded decks, province cards and Gold sources. Every enabled option carrying a concrete command is executed against its own detached copy of the same state. The check does not advance the shared fixture between probes or suppress rejected commands/exceptions.

Coverage records per-card source-command counts, including zero for passive cards or requirements the fixture cannot satisfy. This is a finite offered-command consistency test, not proof of every possible card interaction, every target/payment combination or expert rules acceptance. Commandless UI metadata is not a concrete offered command. The nightly supplements focused card regressions; it does not replace them.

Use --mode soak or --mode catalogue to isolate a tier, --seeds N to vary soak length, --max-decisions N to reproduce a bound failure, and --output DIR for an ignored evidence directory. --inject-failure exits unsuccessfully with an explicitly marked acceptance summary; it does not alter engine behavior. The full scheduled run uses defaults, not a reduced acceptance sample. Do not rebuild a Windows runner's output directory while a process is using it; copy the built files to an ignored directory if testing a stable binary while editing.

Evidence includes run.log, summary.json, catalogue-coverage.json, catalogue-failures.json and the first failing command/state or match replay. CI retains artifacts for 14 days even when validation fails. These contain synthetic test state, not real room journals or seat credentials. The validation job has a 45-minute bound; timeout/bootstrap/build failures are failures, not passes.

## Failure reporting and merge convention

A separate reporting job runs after a failed main validation job, including job timeouts. It has issues:write permission; the test job has only contents:read. The reporter creates the nightly-broken label when necessary, opens one marker-owned issue, or updates that issue with the latest failure and original evidence. Repeated identical reports are idempotent. It does not overwrite unrelated labelled issues or silently treat API failures as success.

Check `gh issue list --state open --label nightly-broken` before merging. While a production nightly-broken issue is open, pause normal merges, investigate the evidence, fix the cause, rerun the complete nightly on main and close the issue after verifying success. Passing PR checks, partial samples and branch-only runs do not clear it. The issue is never automatically closed by a later successful sample. Without branch protection this remains a team convention; a specifically owner-directed recovery merge may be required to repair main.

To inspect/report an actual failure locally, run `python tools/nightly/report_failure.py --repository alfredoh7/l5r-emperor --commit COMMIT_SHA --run-url REPOSITORY_EVIDENCE_URL --summary artifacts/nightly/summary.json`. Missing summaries still report infrastructure failures with the run link. --acceptance uses a separate marker and a clearly labelled test issue, allowing deliberate failure testing without representing a defect on main. Close that temporary issue after verifying creation and reuse. Never run an injected failure as evidence of a game regression.

Reporter unit tests: `python -m unittest discover -s tools/nightly -p 'test_*.py' -v`. These are fast and run in the nightly workflow; the long runner is deliberately absent from tools/check.sh and the solution's normal test loop.
