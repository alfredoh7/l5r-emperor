# Rung 5: repository skills

Tracked in [#509](https://github.com/alfredoh7/l5r-emperor/issues/509) and implemented by [#532](https://github.com/alfredoh7/l5r-emperor/pull/532), after Rung 4b merged in #531. The four recipes are `wire-card-wave`, `art-pack-release`, `card-pass-report`, and `ruling-interview`. See [usage and reproduction instructions](../REPO_SKILLS.md).

Canonical skills live in `.claude/skills`; byte-identical `.agents/skills` copies support Codex repository discovery without Windows symlinks or per-user configuration. `tools/skills/sync_skills.py` checks metadata, local references and mirror drift, preserving unexpected destination files rather than deleting them. Six regression tests run through existing hooks discovery and CI. AGENTS.md remains policy; skills grant no new authority and cannot turn silence or passing tests into a ruling.

The report recipe includes coverage, confirmed versus proposed readings, client contracts and observed ladder measures. Unavailable conflict, turnaround, CI or nightly measurements must remain unavailable, rather than being reported as zero. The interview recipe is original project guidance; the plan supplied no specific Yuzu implementation revision to copy or verify.

## Independent two-card acceptance

A fresh evaluator with no conversation history received only the wiring skill, raw reviewed synthetic inputs and execution boundaries. It could inspect maintained examples linked by the recipe. It received no intended packager, hook, tests or implementation answer. All fixture changes stayed in an ignored scratch checkout; production definitions, scans, registration files and inventory were untouched. No fixture content or art release was published.

The raw-input preparer is `tools/skills/prepare_wave_exercise.py`. It creates receipt-hashed solid-color JPEGs and catalogue records for a blank Lion Samurai and a second Lion Samurai with `Open: Gain 1 Honor.` These are test inputs, not historical printings or visual readings of card faces. It copies the maintained build dependencies, clears NuGet feeds and creates a local baseline Git commit with no remote. It refuses an existing destination and destinations outside the repository's ignored artifacts directory.

The evaluator produced `personality-wave-97`, source provenance, copied scans, a manifest pinned with the maintained tool, a feature-owned Abilities/RunEffects hook, three packaging tests and an automatically discovered four-case Core suite. No csproj, PublishedContent, CardCatalog, central ability/model chain, PublishedWaveOrder, legacy fixture or shared test list changed. The actual offered command gained one Honor without a bow cost; wrong phase/controller, bowed source, blank-card source and repeated use were rejected without mutation. A dishonorable performer rehonored instead of gaining Honor.

| Check | Observed result |
|---|---|
| `dotnet run --project tests/L5R.Core.Tests -- --filter fixture-wave-97` | 4/4 passed |
| Core test project build, offline pinned SDK | 0 warnings, 0 errors |
| Fixture-specific Python packaging discovery | 3/3 passed; deterministic rerun, selected printing/text and scan tampering checked |
| Manifest rerun | 0 manifests changed |
| Generated inventory, baseline to final | 2183 cards / 1419 wired to 2185 / 1421 |
| Broader scratch content discovery | 135 tests: 134 passed, one import error, no test failures |

The broader discovery error was `test_catalogue_art` importing `tools/ensure_catalogue_art.py`, which the minimal scratch preparer does not copy. Neither a complete catalogue-art installation nor a full production content/client run is claimed. Both maintained parallel-registration build/merge tests passed in the final discovery run. The original run also caught an uncompilable first version of the hook; the evaluator fixed it before the final run.

Two ordinary compilation iterations required inspecting maintained examples: returning the Abilities dictionary from its callback, and qualifying test assertion helpers that collide with the runner's top-level names. The evaluator completed both independently. Initial unittest execution used the default Windows temporary directory for auto-cleaned test repositories; the final run corrected TMP/TEMP to the scratch artifacts directory. These execution details are recorded rather than treated as flawless first-pass acceptance.

After acceptance, the recipe gained an explicit reminder about dictionary returns and helper-name qualification. This clarifies the maintained examples without changing the completed fixture implementation.

The final inventory was generated into ignored artifacts. The two fixture set labels were supplied only through an in-memory inventory override; no tracked workbook or historical catalogue changed. Logs and the evaluator's complete report are local artifacts under `artifacts/rung5/acceptance/artifacts/`; the checked-in preparer allows another fresh session to repeat the exercise without supplying its implementation.

## Repository validation

All eight skill directories (four canonical and four mirrors) passed the skill creator's metadata validator. Mirror verification passed. Existing hooks discovery passed 26 tests, collaboration discovery passed 55, lint tests passed 16, and the actual comment-swallow lint and `git diff --check` passed. The preparer was exercised successfully and a repeated invocation rejected the existing destination without changing its Git HEAD.

Acceptance establishes that a new session can package and execute a two-card wave from the skill and maintained examples. It does not substitute for a real wave's printed-card review, full production checks, optimized art coverage or client verification. This PR changes workflow recipes and their tooling, not production gameplay. Standard PR CI remains a separate check of the production checkout.
