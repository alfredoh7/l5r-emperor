# GitHub collaboration baseline

Date: 2026-09-07. Desktop 0.3.10 / `lion-crane-v1`.

## Completed

Prepared the existing game for a private GitHub repository with AI entrypoints, shared status, rules-review instructions, issue/PR templates, contribution workflow and optional research-art distribution. No gameplay or presentation behavior was changed in this handoff.

## Verification actually run

On the configured native macOS ARM64 development machine, `bash tools/check.sh` passed: 59 Python content tests, 36 foundation rules tests, 31 published rules tests, 13 AI tests, 5 server integration checks and 4 client transport integration checks (148 total). The C# client build, Godot headless import and smoke also passed. Local log: ignored `artifacts/github/baseline.log`.

The upload candidate files were scanned for common GitHub/AWS credentials and private key headers; no matches were found. Credential stores, local tools/caches, private room storage and build outputs are excluded. This pattern scan is not a full security audit.

Existing 0.3.10 Mac and Windows ZIPs are distributed with checksums. Previous native Mac/visual evidence remains documented in `docs/PHASE_BAR_ART.md`; the GitHub handoff did not repeat visual acceptance. Windows export validation is not native Windows gameplay acceptance. Fresh GitHub Actions results must be read on GitHub rather than inferred from this local run.

## Distribution

The source checkout includes all current runtime art and 40 selected Lion/Crane source scans. The 590 larger full-set research images are preserved in the optional private Release ZIP; metadata/receipts stay in Git. Extract that ZIP at the repository root only if researching additional content.

## Next task

Human expert review of complete Lion/Crane matches, then targeted regression fixes. Read `docs/RULES_REVIEW.md`, `docs/PROJECT_STATUS.md` and `AI_HANDOFF.md`. Keep future work isolated by branch/worktree and record findings in issues and validation in PRs.
