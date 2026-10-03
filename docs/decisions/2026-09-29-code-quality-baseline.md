# Rung 6: formatting and zero-warning baseline

Implemented for [#509](https://github.com/alfredoh7/l5r-emperor/issues/509) in [#533](https://github.com/alfredoh7/l5r-emperor/pull/533), starting from main after #532 merged. Scope: shared development tooling and CI; no gameplay changes or repository-wide reformat. [Usage and policy](../CODE_QUALITY.md) explain the checks and optional pre-commit hook. The workflow touches Alfredo's usual lane, so a courtesy review was requested; Mendon's standing authority makes that advisory.

## Decision

Formatting uses the SDK's whitespace formatter with an explicit list of changed C# files and --verify-no-changes. Full style/analyzer auto-fixes would broaden the diff beyond this rung. Folder mode avoids loading Godot or restoring projects. A separate Windows-hosted job runs concurrently with the existing desktop workflow; it does not use the shared Mac runner. Git selection is NUL-delimited, handles renames/deletions and local working changes, and fails when commits or selected files are missing. PR CI compares the merge result against current base, rather than attributing unrelated changes on main to a stale feature branch.

The measured compiler/analyzer baseline is zero warnings. All eight existing projects already enabled nullable analysis and treated warnings as errors. Shared props now inherit that policy for future projects; a target rejects project-level disabling before compilation. There are no warning allow-lists, new analyzer packages or suppressed diagnostics. Existing builds enforce new warnings as failures, so another diagnostic-count parser is unnecessary.

## Evidence

- Full solution rebuild: `dotnet build L5R.slnx --no-incremental --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo`, pinned SDK 10.0.400: all eight projects compiled, including the Godot client, with **0 warnings and 0 errors** (1m50.59s).
- Quality regression discovery with L5R_QUALITY_INTEGRATION=1: **7 tests passed**. The real formatter rejected an intentionally malformed changed file without changing its bytes or an untouched malformed legacy file. Formatting only the fixture changed file made verification pass while preserving legacy bytes. These tests also execute in quality CI.
- A minimal new project inherited the shared compiler policy: a CS8602 dereference failed the build; null-safe code passed. Overriding Nullable=disable or TreatWarningsAsErrors=false failed with the policy-target diagnostic.
- Git selection tests covered staged/unstaged/new files, committed rename/deletion, missing base, empty selection and missing sparse files. The workflow's actual sparse patterns were exercised in a local Git fixture: all three nested/root C# files remained available while artwork was excluded.
- Hooks: **26 passed**. Collaboration: **55 passed**. Actual branch formatting verification reported no changed C# files, correctly skipping a full-source format. Collaboration audit and diff whitespace checks passed.

Local build/test logs are ignored artifacts under artifacts/rung6. No existing C# file was modified. A client build is not graphical playtesting; this tooling-only change needs no new native game acceptance. Standard production desktop CI remains separate from the formatting fixture and compiler-policy regressions.
