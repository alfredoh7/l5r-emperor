# Supply-chain and workflow checks

Rung 8 of [#509](https://github.com/alfredoh7/l5r-emperor/issues/509) adds dependency update proposals and workflow auditing. It changes no game rules, content or runtime dependency versions. Task PR: [#537](https://github.com/alfredoh7/l5r-emperor/pull/537).

## Dependency proposals

`.github/dependabot.yml` checks GitHub Actions and NuGet weekly on Monday, with at most five open update PRs per ecosystem. Each update remains a proposal requiring normal checks and human review; there is no auto-merge. All current remote workflow actions are pinned to commit hashes, with comments identifying their existing major version. Existing pins are resolved from the same major refs already used by the workflows; Rung 8 does not upgrade those majors. Dependabot can propose later version/hash updates.

The current projects use project references and the pinned Godot MSBuild SDK, with no ordinary `PackageReference` entries. NuGet updates are only available when the service detects a supported manifest/dependency and a newer version. Do not add a dummy package just to provoke a bot PR. Godot/.NET upgrades must remain consistent with `tools/bootstrap.py`, `global.json`, project files and validation; do not merge a bot proposal that breaks those pins independently.

Dependabot reads configuration from the default branch. Its first actual service run and update PRs are therefore post-merge acceptance, not something a feature-branch local test or manually authored PR proves. After merging, inspect Repository → Insights → Dependency graph → Dependabot (or the bot's PRs); verify both ecosystem logs and eligible update proposals. Availability may require the owner's repository settings. No billing, visibility or administrator settings were changed.

## Workflow audit

The small hosted Ubuntu `security.yml` job runs pinned zizmor 1.30.1 with `--offline --persona pedantic --format github .github/workflows`. It requires only `contents: read`, performs no SARIF upload, and gives the auditor no token. It runs independently of the Mac/game checks. Ordinary annotation output propagates a failing exit for findings; a green SARIF upload alone would not establish this.

Local reproduction:

```sh
python -m pip install -r tools/security/requirements.txt
python tools/security/check_policy.py
python -m unittest discover -s tools/security -p 'test_*.py' -v
zizmor --offline --persona pedantic --format github .github/workflows
```

The only inline exception is `dangerous-triggers` on review-routing's `pull_request_target`. This job must label/request reviews with a write token, but checks out the exact trusted base SHA, uses hash-pinned actions, and runs only the base's metadata router with the numeric PR identifier passed through the environment. It never checks out PR-head code or consumes PR artifacts. The policy checker/regressions reject a head/external checkout, credential persistence, additional actions/scripts or execution/environment overrides. New trigger warnings elsewhere still fail. Do not broaden the exception to silence an actual unsafe execution path.

Pedantic findings were resolved with hash pins, environment variables for shell arguments, job-scoped write permissions with reasons, and cancellation groups for duplicate read-only checks. The separate auditor persona still identifies the pre-existing self-hosted Mac runner as a risk requiring operator isolation; this rung neither provisions nor certifies that machine. The existing same-repository PR guard remains, and no new work executes there.

## Advisory ownership

`.github/CODEOWNERS` mirrors `.github/collaboration.json`: unknown/shared files list both leads, UI/rules paths list their lead, and shared exceptions take precedence. Per-task handoff records have no owner entry, matching `collab.py`'s exclusion from lane classification. GitHub may suggest/request reviewers; this file is advisory without branch protection and does not revoke Mendon's standing autonomous authority.

After changing the canonical collaboration map, run `python tools/security/check_policy.py --write-codeowners`. CI rejects drift. The checker must not become a second independent ownership policy.

## Conditional CodeQL

CodeQL C# was requested. This is a public, personally owned repository, so code scanning is available at no cost, but it was not enabled when this was written. The authenticated agent has push/triage but no administrator permission; the code-scanning default-setup API returns HTTP 403 with "Code scanning is not enabled for this repository." The repository API does not expose security entitlement details to this identity. No CodeQL/SARIF workflow has been added. The owner can enable code scanning default setup now that the repository is public. This is not a claim that C# vulnerability analysis ran.

References: [Dependabot configuration](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference), [zizmor integration and exit behavior](https://docs.zizmor.sh/integrations/), [private CodeQL requirements](https://docs.github.com/en/code-security/reference/code-scanning/troubleshoot-analysis-errors/private-repository-enablement), [CODEOWNERS precedence](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners).

## Rung 8 validation

The implementation commit `e07fc471` passed the [hosted workflow security/ownership job](https://github.com/alfredoh7/l5r-emperor/actions/runs/36552946645). Nine local and hosted regressions passed, including a real unpinned-action fixture that exits unsuccessfully. The local pedantic audit passed with the single documented routing exception. GitHub's CODEOWNERS errors API on the task branch returned an empty errors array. The first real Dependabot run/update proposals remain post-merge acceptance; no bot PR or CodeQL scan is claimed here.
