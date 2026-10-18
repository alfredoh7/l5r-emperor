# Changed-file formatting and nullable baseline

Rung 6 of the [PR ladder](PR_LADDER.md), tracked in [#509](https://github.com/l5r-emperor-dev/l5r-emperor/issues/509), adds a forward-looking whitespace check. It does not reformat existing source or add a new analyzer package.

## Formatting

Put the pinned .tools/dotnet SDK on PATH (or source tools/env.sh in Bash), then run:

```sh
python tools/quality/check_format.py
python tools/quality/check_format.py --list
```

The default compares the working checkout to the branch point with origin/main. It includes committed, staged, unstaged and untracked C# changes; it excludes deletions and ignored files. Missing diff commits or missing selected files fail instead of silently skipping the check. Paths are passed as individual arguments, including names with spaces. Optional --base and --head select exact committed endpoints; when --head is present, working changes are excluded.

The script runs `dotnet format whitespace --folder --verify-no-changes --include` with a nonempty, explicit file list. Folder mode needs no project restoration or Godot SDK. .editorconfig defines C# whitespace conventions and preserves single-line blocks/statements. Only changed files are checked, but the entire changed file must meet those conventions; touching an older compact file may require formatting that file in the same PR. Untouched files are not formatting obligations.

To fix a selected file deliberately, run `dotnet format whitespace . --folder --include path/to/file.cs`, then inspect its diff and rerun the verification. Never omit --include when fixing a bounded task. The gate itself never rewrites source.

The separate Windows-hosted **Changed-file quality** workflow runs alongside desktop checks and does not occupy the shared Mac runner. It uses the pinned SDK-only bootstrap. On PRs it compares the checked-out merge result to current base, so unrelated changes on a newer main do not become formatting obligations for an older head. Main pushes check before/after; manual dispatch checks the latest commit. Regression tests deliberately supply malformed fixture code and assert rejection and unchanged source bytes.

The optional .pre-commit-config.yaml is a local hook using the same script. Install pre-commit separately, run `pre-commit install`, and keep the pinned SDK on PATH. Nothing installs hooks or changes global Git configuration automatically. The hook checks the working checkout, including unstaged changes; it is not a staged-only formatter.

## Nullable and analyzer baseline

The baseline is **zero compiler/analyzer warnings across the eight existing solution projects**, including nullable warnings. Before this rung, every project already had TreatWarningsAsErrors=true and inherited or explicitly enabled Nullable. Rung 6 makes those defaults shared in Directory.Build.props and checks them before CoreCompile in Directory.Build.targets, covering new projects and rejecting project-level attempts to disable either policy. Existing SDK analyzers and warnings remain enabled; no suppressions or blanket NoWarn list were added.

Normal desktop CI builds enforce this baseline across Core, AI, server, transport tests and the Godot client. A separate diagnostic-count allow-list would only duplicate the existing zero-warning contract; there is no historic warning debt to grandfather in. Fix a new warning at its source. Deliberate suppression or a change to analysis policy requires an explicit, reviewable code/config change; the gate is not a prohibition on legitimate annotated APIs or targeted suppressions.

The integration fixture proves a new project inherits these defaults: CS8602 fails compilation, the corrected null-safe expression passes, and overriding Nullable or TreatWarningsAsErrors fails the policy target. These tests run with `L5R_QUALITY_INTEGRATION=1 python -m unittest discover -s tools/quality -p 'test_*.py' -v`. Ordinary tools/check.sh runs fast selection tests and skips SDK integration, which has its own CI job.

The implementation uses the [documented .NET formatter](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) and the repository's pinned SDK. Formatting, compilation, gameplay tests and native acceptance remain separate checks.

## Coverage trend, analyzers and hang diagnostics (issue #905)

From the [external practices assessment](audits/external-practices-2026-10.md).

- **Coverage of L5R.Core**: `bash tools/coverage.sh [--filter ...]` runs the Core suite under coverlet and writes `artifacts/coverage/Summary.txt` (ReportGenerator). It is a trend report for
  rules code, not a gate, and it is slow: instrumenting the Core assembly alone took about 7 minutes for a small slice (`core/Privacy`, 24.6% line coverage), so run it on demand or in a scheduled job, not per PR.
- **Meziantou.Analyzer** is referenced by Core, AI and Server from `Directory.Build.targets` (not the project files, so the registration tests' scratch repos, which have no package feed, build Core without it). Every one of its 39 rules that already fires on existing code (7,262 findings, led by string-equality style, file-name and named-argument
  rules) is held at suggestion in `src/L5R.Core/Meziantou.globalconfig` (referenced from `Directory.Build.props`), so the zero-warning gate is unchanged; any other rule of the package is an error for new code. `bash tools/analyzer-report.sh` lists the counts. Fix a rule's findings,
  then delete its `dotnet_diagnostic` line to enforce it. The rules worth fixing first are the ones that find bugs rather than style: culture-dependent parsing and formatting (MA0011 45, MA0075, MA0076 427),
  missing string comparison (MA0001, MA0074, MA0002 187) and unsafe regular expressions (MA0009, MA0023, MA0110, all in deck and recruit parsing).
- **A hung or crashed test run says where**: CI sets `L5R_TEST_TRACE=1`, so the runner prints `RUN <case>` before each case (the last one in a log that stops is the case that hung), and a crash of the
  .NET process writes a dump that CI keeps as an artifact for three days.
- **Desync soak through the server** (`L5R.Server.Tests`, "a seeded match played through the server stays equal to its authoritative replay"): two real sockets play 160 random enabled commands
  from a fixed seed. At checkpoints the room's saved commands are replayed into a fresh engine and the view and the action list each seat last received must equal that engine's, compared in the
  wire's own strict JSON. A dropped or changed field, a projection that differs by seat, or a live engine that disagrees with its own replay fails it (a seat mix-up in the comparison fails at
  command 0). It runs in about 4 seconds. Idea from Forge's network desync testing.
- **Local crash reports** (`CrashReports.cs`, `CrashBundle.cs`): an unhandled or unobserved .NET exception writes `user://crash-reports/crash-<time>.txt` with the exception, the last 60 engine
  errors, the content version and environment, and the position and public event log the local viewer sees. Nothing is sent, and nothing hidden is included (no seed, deck order or opponent
  hand); seat tokens and the home directory are scrubbed (`CrashBundleTests`). `--write-crash-report` writes one on demand to check the path works. A hosted service (Sentry) is a later step.
- **Movie Maker spike**: `godot --write-movie out.avi --fixed-fps 30 --quit-after N -- --perf=table` records the real client on `gl_compatibility` at a fixed timestep (90 frames in 5 s on the
  Mac, window size 1440x900 whatever `--resolution` says). Nothing uses it yet; it is the way to take timing-independent screenshots once a check needs them.

