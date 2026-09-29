# Changed-file formatting and nullable baseline

Rung 6 of the [PR ladder](PR_LADDER.md), tracked in [#509](https://github.com/alfredoh7/l5r-emperor/issues/509), adds a forward-looking whitespace check. It does not reformat existing source or add a new analyzer package.

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
